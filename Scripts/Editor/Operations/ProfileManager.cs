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
        // Initialize() time and goes stale. This AssetPostprocessor fires after every
        // import batch and tells all open BrowserWindows to rebuild when the active
        // Profile's file is among the reimported assets.

        class ProfileAssetWatcher : AssetPostprocessor
        {
            static void OnPostprocessAllAssets(
                string[] importedAssets,
                string[] deletedAssets,
                string[] movedAssets,
                string[] movedFromAssetPaths)
            {
                // Skip quickly if no .asset files were touched.
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

                bool profileReimported = Array.Exists(importedAssets,
                    p => string.Equals(p, profilePath, StringComparison.OrdinalIgnoreCase));

                if (!profileReimported)
                    return;

                // Defer by one frame so the import transaction fully settles before we
                // rebuild the UI — mirrors the pattern used in SubAssetRefreshUtils.
                EditorApplication.delayCall += NotifyChanged;
            }
        }
    }
}

