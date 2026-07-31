using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SecretZauce.SecondBrain.Editor
{
    /// <summary>
    /// Editor-only utility for managing Profiles (the root workspace containers,
    /// Handles multi-profile discovery, creation,
    /// switching, and storage-location migration.
    /// </summary>
    public static class ProfileManager
    {
        /// <summary>Fired when the active profile changes. All open BrowserWindows subscribe.</summary>
        public static event Action OnActiveProfileChanged;

        // ── Discovery ──────────────────────────────────────────────────────────

        /// <summary>
        /// Returns all Profile assets registered in <see cref="SecondBrainCore"/>.
        /// Syncs the registry first to pick up any assets added outside the API.
        /// </summary>
        public static Profile[] GetAllProfiles()
        {
            var core = SecondBrainCore.Instance;
            core.SyncProfiles();
            return core.Profiles.ToArray();
        }

        // ── Active profile ─────────────────────────────────────────────────────

        /// <summary>Returns the currently active Profile (shortcut for <see cref="Profile.Active"/>).</summary>
        public static Profile GetActiveProfile() => Profile.Active;

        /// <summary>
        /// Switches the active profile to <paramref name="profile"/>, persists the choice
        /// in EditorPrefs, invalidates the cache, and notifies all subscribers.
        /// </summary>
        public static void SetActiveProfile(Profile profile)
        {
            if (profile == null) return;
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(profile, out string guid, out long _))
                return;

            EditorPrefs.SetString(Profile.PREF_ACTIVE_PROFILE_GUID, guid);
            Profile.InvalidateCache();
            NotifyChanged();
        }

        // ── Location helpers ───────────────────────────────────────────────────

        /// <summary>Returns the storage location of a Profile derived from its on-disk path.</summary>
        public static DataStorageLocation GetProfileLocation(Profile profile)
            => Profile.GetCurrentLocation(profile);

        /// <summary>Returns a human-readable label for a <see cref="DataStorageLocation"/>.</summary>
        public static string GetLocationLabel(DataStorageLocation loc)
            => loc == DataStorageLocation.EditorResources ? "Editor-Only" : "In-Build";

        // ── Move ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Moves <paramref name="profile"/> to the folder that corresponds to
        /// <paramref name="newLocation"/>.  Shows a confirmation dialog first.
        /// Returns true on success.
        /// </summary>
        public static bool MoveProfileToLocation(Profile profile, DataStorageLocation newLocation)
        {
            if (profile == null) return false;

            DataStorageLocation current = GetProfileLocation(profile);
            if (current == newLocation) return true;

            string label = GetLocationLabel(newLocation);
            bool confirm = EditorUtility.DisplayDialog(
                "Switch Storage Location",
                $"Move '{profile.name}' to {label}?\n\n" +
                (newLocation == DataStorageLocation.EditorResources
                    ? "The asset will be placed in Assets/Resources/Editor/ and excluded from player builds."
                    : "The asset will be placed in Assets/Resources/ and included in player builds."),
                "Move", "Cancel");

            if (!confirm) return false;

            string targetFolder = newLocation == DataStorageLocation.EditorResources
                ? Profile.FOLDER_EDITOR_RESOURCES
                : Profile.FOLDER_RESOURCES;

            string currentPath = AssetDatabase.GetAssetPath(profile);
            if (string.IsNullOrEmpty(currentPath)) return false;

            string fileName = Path.GetFileName(currentPath);
            string newPath  = targetFolder + "/" + fileName;

            if (string.Equals(currentPath, newPath, StringComparison.OrdinalIgnoreCase))
                return true;

            Profile.EnsureFolderPath(targetFolder);

            string error = AssetDatabase.MoveAsset(currentPath, newPath);
            if (!string.IsNullOrEmpty(error))
            {
                EditorUtility.DisplayDialog("Move Failed",
                    $"Could not move Profile asset:\n{error}", "OK");
                return false;
            }

            AssetDatabase.SaveAssets();
            Profile.InvalidateCache();
            NotifyChanged();
            return true;
        }

        // ── Creation ───────────────────────────────────────────────────────────

        /// <summary>
        /// Creates a new Profile asset with <paramref name="profileName"/> in the folder
        /// that corresponds to <paramref name="location"/>.
        /// Returns the new Profile or null on failure.
        /// </summary>
        public static Profile CreateNewProfile(DataStorageLocation location, string profileName)
        {
            if (string.IsNullOrWhiteSpace(profileName))
                profileName = "Profile";

            string folder = location == DataStorageLocation.EditorResources
                ? Profile.FOLDER_EDITOR_RESOURCES
                : Profile.FOLDER_RESOURCES;

            Profile.EnsureFolderPath(folder);

            string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{profileName}.asset");

            var newProfile = ScriptableObject.CreateInstance<Profile>();
            newProfile.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(newProfile, path);

            // Bootstrap the new profile with a default workspace Base
            CreateDefaultBase(newProfile, path);

            SecondBrainCore.Instance.RegisterProfile(newProfile);
            AssetDatabase.SaveAssets();
            return newProfile;
        }

        // ── Move Base across Profiles ──────────────────────────────────────────

        /// <summary>
        /// Moves <paramref name="baseToMove"/> from its current Profile to <paramref name="targetProfile"/>.
        /// Migrates all embedded sub-assets and updates both profiles' child lists.
        /// Shows a confirmation dialog first. Returns true on success.
        /// Note: this operation cannot be undone.
        /// </summary>
        public static bool MoveBaseToProfile(Base baseToMove, Profile targetProfile)
            => MoveBasesToProfile(new[] { baseToMove }, targetProfile);

        /// <summary>
        /// Moves every Base in <paramref name="basesToMove"/> to <paramref name="targetProfile"/>.
        /// The Bases may originate from different source Profiles; each one is migrated with all
        /// its embedded sub-assets, and every touched Profile asset is saved and reimported once.
        /// Shows a single confirmation dialog covering the whole batch. Returns true on success.
        /// Note: this operation cannot be undone.
        /// </summary>
        public static bool MoveBasesToProfile(IList<Base> basesToMove, Profile targetProfile)
        {
            if (basesToMove == null || basesToMove.Count == 0 || targetProfile == null) return false;

            string targetProfilePath = AssetDatabase.GetAssetPath(targetProfile);
            if (string.IsNullOrEmpty(targetProfilePath))
            {
                EditorUtility.DisplayDialog("Move Failed",
                    "Could not determine the asset path for the target Profile.", "OK");
                return false;
            }

            // Resolve the source Profile of every Base up front so the batch either
            // moves as a whole or not at all.
            var moves = ResolveMoves(basesToMove, targetProfile, out var unresolved);

            if (unresolved.Count > 0)
            {
                EditorUtility.DisplayDialog("Move Failed",
                    "Could not determine the source Profile for: " + string.Join(", ", unresolved), "OK");
                return false;
            }

            if (moves.Count == 0) return true;

            string summary = moves.Count == 1
                ? $"Move '{moves[0].Base.name}' from '{moves[0].Source.name}' to '{targetProfile.name}'?"
                : $"Move {moves.Count} Bases to '{targetProfile.name}'?\n\n"
                  + string.Join("\n", moves.Select(m => $"  • {m.Base.name}  (from '{m.Source.name}')"));

            bool confirm = EditorUtility.DisplayDialog(
                moves.Count == 1 ? "Move Base to Profile" : "Move Bases to Profile",
                $"{summary}\n\nThis operation cannot be undone.",
                "Move", "Cancel");
            if (!confirm) return false;

            var touchedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { targetProfilePath };

            foreach (var (baseToMove, sourceProfile, sourceProfilePath) in moves)
            {
                // Remove from source profile's children list; clear DefaultBase if needed
                (sourceProfile as IStructure).RemoveChild(baseToMove);
                if (sourceProfile.DefaultBase == baseToMove)
                    sourceProfile.DefaultBase = sourceProfile.Children.Count > 0 ? sourceProfile.Children[0] : null;
                EditorUtility.SetDirty(sourceProfile);

                // Migrate the Base and all its embedded sub-assets to the target profile asset
                MigrateObjectToProfileAsset(baseToMove, targetProfilePath, sourceProfilePath);

                // Add to target profile's children list
                (targetProfile as IStructure).AddChild(baseToMove, -1);

                touchedPaths.Add(sourceProfilePath);
            }

            EditorUtility.SetDirty(targetProfile);

            // Suspend auto-import during save so Unity reconciles the sub-asset list atomically
            AssetDatabase.StartAssetEditing();
            try
            {
                AssetDatabase.SaveAssets();
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            foreach (string path in touchedPaths)
                SubAssetRefreshUtils.ImportAndRegister(path);

            NotifyChanged();
            return true;
        }

        /// <summary>
        /// Pairs each Base with the Profile that currently owns it and that Profile's asset path.
        /// Bases already owned by <paramref name="targetProfile"/> and duplicates are skipped;
        /// Bases whose owner or asset path cannot be determined are reported in
        /// <paramref name="unresolvedNames"/>.
        /// </summary>
        static List<(Base Base, Profile Source, string SourcePath)> ResolveMoves(
            IList<Base> basesToMove, Profile targetProfile, out List<string> unresolvedNames)
        {
            var moves = new List<(Base Base, Profile Source, string SourcePath)>();
            unresolvedNames = new List<string>();
            var seen = new HashSet<Base>();

            foreach (var baseToMove in basesToMove)
            {
                if (baseToMove == null || !seen.Add(baseToMove)) continue;

                Profile sourceProfile = FindProfileContaining(baseToMove);
                // Already in the target Profile — nothing to do for this one
                if (ReferenceEquals(sourceProfile, targetProfile)) continue;

                string sourceProfilePath = sourceProfile != null
                    ? AssetDatabase.GetAssetPath(sourceProfile)
                    : null;

                if (string.IsNullOrEmpty(sourceProfilePath))
                    unresolvedNames.Add(baseToMove.name);
                else
                    moves.Add((baseToMove, sourceProfile, sourceProfilePath));
            }

            return moves;
        }

        /// <summary>
        /// Returns the Profile whose Children list contains <paramref name="baseToFind"/>,
        /// or null if none of the registered profiles owns it.
        /// </summary>
        static Profile FindProfileContaining(Base baseToFind)
        {
            var allProfiles = GetAllProfiles();
            foreach (var profile in allProfiles)
            {
                if (profile?.Children != null && profile.Children.Contains(baseToFind))
                    return profile;
            }
            return null;
        }

        /// <summary>
        /// Recursively migrates <paramref name="obj"/> and all its IStructure descendants
        /// from <paramref name="sourceAssetPath"/> to <paramref name="targetAssetPath"/>.
        /// Only sub-assets that currently reside in <paramref name="sourceAssetPath"/> are moved.
        /// </summary>
        static void MigrateObjectToProfileAsset(Object obj, string targetAssetPath, string sourceAssetPath)
        {
            if (obj == null) return;

            // Recurse into children first so they are in the target file before their parent is moved
            if (obj is IStructure structure)
            {
                var children = structure.ChildrenObjects?.ToList();
                if (children != null)
                    foreach (var child in children)
                        MigrateObjectToProfileAsset(child, targetAssetPath, sourceAssetPath);
            }

            // Only relocate sub-assets that originate from the source profile file
            if (!AssetDatabase.IsSubAsset(obj)) return;
            string currentPath = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(currentPath) || currentPath == targetAssetPath) return;
            if (!string.Equals(currentPath, sourceAssetPath, StringComparison.OrdinalIgnoreCase)) return;

            AssetDatabase.RemoveObjectFromAsset(obj);
            AssetDatabase.AddObjectToAsset(obj, targetAssetPath);
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        static void CreateDefaultBase(Profile profile, string profilePath)
        {
            var defaultBase = ScriptableObject.CreateInstance<Base>();
            defaultBase.name = "My Workspace";
            AssetDatabase.AddObjectToAsset(defaultBase, profilePath);
            (profile as IStructure).AddChild(defaultBase);
            profile.DefaultBase = defaultBase;
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            SubAssetRefreshUtils.ImportAndRegister(profilePath);
        }

        static void NotifyChanged()
        {
            try { OnActiveProfileChanged?.Invoke(); }
            catch (Exception ex) { Debug.LogWarning($"[SecondBrain] ProfileManager notification error: {ex.Message}"); }
        }

        // ── External-change detection ──────────────────────────────────────────
        //
        // When the user switches git branches or checks out a different commit, Unity
        // reimports the changed .asset files but does NOT trigger a domain reload (unless
        // .cs files also changed). The Profile ScriptableObject is updated in-place by
        // Unity's deserializer, but BrowserController.Collections is a snapshot built at
        // Initialize() time and goes stale.
        //
        // We cannot distinguish a git-triggered reimport from a SecondBrain-internal save
        // (delete, rename, reorder, color change, etc.) inside OnPostprocessAllAssets alone,
        // because both call paths ultimately invoke AssetDatabase.SaveAssets() / ImportAsset().
        //
        // The solution: defer the check by one editor frame via delayCall. By then, any
        // internal OnStructureChanged → BrowserController.RefreshFromRoot() has already
        // updated Collections to match the new profile state. If Collections are still
        // out of sync after that frame, the reimport was external — notify all windows.

        class ProfileAssetWatcher : AssetPostprocessor
        {
            static bool _checkScheduled;

            static void OnPostprocessAllAssets(
                string[] importedAssets,
                string[] deletedAssets,
                string[] movedAssets,
                string[] movedFromAssetPaths)
            {
                // Quick exit: no .asset files touched.
                if (!Array.Exists(importedAssets, p => p.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)))
                    return;

                // Do NOT call Profile.Active here — it auto-creates a profile if none
                // exists, which would corrupt state in the middle of an import transaction.
                if (!Profile.IsActiveProfileCached)
                    return;

                var profile = Profile.Active;
                if (profile == null)
                    return;

                string profilePath = AssetDatabase.GetAssetPath(profile);
                if (string.IsNullOrEmpty(profilePath))
                    return;

                if (!Array.Exists(importedAssets,
                        p => string.Equals(p, profilePath, StringComparison.OrdinalIgnoreCase)))
                    return;

                // Defer TWO frames before comparing. The first frame lets internal
                // OnStructureChanged → RefreshFromRoot() calls complete on ALL open windows,
                // including the destination window of a cross-window drag whose RefreshTree
                // deferred call is queued AFTER this postprocessor runs (same first frame).
                // The second frame runs the actual comparison once all Collections are current.
                // Git branch switches have no OnStructureChanged at all, so Collections are
                // still stale in frame 2 → correctly triggers NotifyChanged.
                if (_checkScheduled)
                    return;
                _checkScheduled = true;
                EditorApplication.delayCall += () => EditorApplication.delayCall += CheckAndNotify;
            }

            static void CheckAndNotify()
            {
                _checkScheduled = false;

                if (!Profile.IsActiveProfileCached)
                    return;

                var profile = Profile.Active;
                if (profile == null)
                    return;

                var profileChildren = profile.Children; // List<Base>

                // Find all open BrowserWindows and check whether any has stale Collections.
                var windows = BrowserWindowRegistry.AllOfType<BrowserWindow>();
                if (windows.Count == 0)
                    return;

                bool needsRefresh = false;
                foreach (var w in windows)
                {
                    if (w == null)
                        continue;

                    var wc = w.Collections; // List<IStructure>, rebuilt by RefreshFromRoot

                    if (w.IsAtHome())
                    {
                        // Home view: Collections mirrors Profile.Children.
                        if (wc == null || wc.Count != profileChildren.Count)
                        {
                            needsRefresh = true;
                            break;
                        }
                        for (int i = 0; i < profileChildren.Count; i++)
                        {
                            if (!ReferenceEquals(wc[i], profileChildren[i]))
                            {
                                needsRefresh = true;
                                break;
                            }
                        }
                    }
                    else
                    {
                        // Navigated view: Collections mirrors targetBase.ChildrenObjects.
                        var targetBase = w.Root as Base;

                        // targetBase is null when targetRoot is a Unity "fake null":
                        // a non-null C# reference whose native ScriptableObject was destroyed
                        // by a post-domain-reload reimport. IsAtHome() returns false because
                        // the C# null check misses fake nulls, but Unity's == returns null.
                        // This is a transient state — BrowserWindow.RefreshSerializedDatabase
                        // will recover the target. Skip this window to avoid a false home
                        // navigation; don't set needsRefresh here.
                        if (targetBase == null)
                            continue;

                        if (!profileChildren.Contains(targetBase))
                        {
                            // The Base the window was viewing no longer exists in this branch.
                            needsRefresh = true;
                            break;
                        }

                        var baseChildren = targetBase.Children;
                        if (wc == null || wc.Count != baseChildren.Count)
                        {
                            needsRefresh = true;
                            break;
                        }
                        for (int i = 0; i < baseChildren.Count; i++)
                        {
                            if (!ReferenceEquals(wc[i], baseChildren[i]))
                            {
                                needsRefresh = true;
                                break;
                            }
                        }
                    }

                    if (needsRefresh)
                        break;
                }

                if (needsRefresh)
                    NotifyChanged();
            }
        }
    }
}

