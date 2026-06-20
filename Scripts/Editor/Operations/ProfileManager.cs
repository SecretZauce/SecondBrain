using System;
using System.IO;
using System.Linq;
using SecretZauce.SecondBrain;
using UnityEditor;
using UnityEngine;

namespace SecretZauce.SecondBrain.Editor
{
    /// <summary>
    /// Editor-only utility for managing Profiles (the root workspace containers,
    /// formerly known as Motherbase). Handles multi-profile discovery, creation,
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

                // Defer one frame. During that frame, any internal OnStructureChanged
                // callback will have run RefreshFromRoot(), keeping Collections current.
                // If Collections are still stale when CheckAndNotify runs, it was git.
                if (_checkScheduled)
                    return;
                _checkScheduled = true;
                EditorApplication.delayCall += CheckAndNotify;
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
                var windows = Resources.FindObjectsOfTypeAll<BrowserWindow>();
                if (windows == null || windows.Length == 0)
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
                        if (targetBase == null || !profileChildren.Contains(targetBase))
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

