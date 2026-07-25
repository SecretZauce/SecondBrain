using System;
using System.Collections.Generic;
using UnityEngine;

using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SecretZauce.SecondBrain
{
    /// <summary>
    /// Where a Profile asset (and its Base sub-assets) is stored on disk.
    /// EditorResources places them under Assets/Resources/Editor/ which Unity
    /// excludes from player builds. Resources includes them in builds.
    /// </summary>
    public enum DataStorageLocation
    {
        /// <summary>Assets/Resources/Editor/ — editor-only, excluded from player builds.</summary>
        EditorResources = 0,
        /// <summary>Assets/Resources/ — included in player builds.</summary>
        Resources = 1,
    }

    /// <summary>
    /// Root ScriptableObject that acts as the top-level workspace container.
    /// </summary>
    public class Profile : ScriptableObject, IStructure<Base>
    {
        const string PROFILE_NAME = "Default Profile";

        public List<Base> Children => baseList;
        [SerializeField] List<Base> baseList = new List<Base>();

        // Optional reference to the Default Base. When set, this Base is considered
        // the project's default and can be used by UI or startup logic.
        [SerializeField]
        private Base defaultBase;

        /// <summary>
        /// The Base marked as the default for this Profile. May be null.
        /// </summary>
        public Base DefaultBase
        {
            get => defaultBase;
            set => defaultBase = value;
        }

        public Base CreateNew(Type type)
        {
            return CreateInstance<Base>();
        }

        public bool CanAcceptChild(Object child)
        {
            return child is Base;
        }

        /// <summary>
        /// Returns the currently active Profile. In Pro mode, the active profile is
        /// determined by the GUID stored in EditorPrefs. In Free mode, returns the single
        /// available profile, loading or creating it as needed.
        /// </summary>
        public static Profile Active
        {
            get
            {
                if (active == null)
                    active = LoadActiveProfile();
                return active;
            }
        }

        /// <summary>
        /// Legacy accessor — forwards to <see cref="Active"/> for backward compatibility.
        /// </summary>
        public static Profile Home => Active;

        public static Profile LoadActiveProfile()
        {
#if UNITY_EDITOR
            return LoadActiveProfileEditor();
#else
            var core = Resources.Load<SecondBrainCore>("SecondBrainCore");
            if (core != null)
            {
                // Core exists — honour its setting. Empty means the user chose "None" → no profile in builds.
                string name = core.DefaultProfileName;
                return string.IsNullOrEmpty(name) ? null : Resources.Load<Profile>(name);
            }
            // Core asset missing → fall back to the built-in constant for backward compatibility.
            return Resources.Load<Profile>(PROFILE_NAME);
#endif
        }

#if UNITY_EDITOR
        // EditorPrefs key storing the GUID of the currently selected profile (Pro only).
        public const string PREF_ACTIVE_PROFILE_GUID = "SecondBrain_ActiveProfileGUID_v1";
        public const string FOLDER_RESOURCES        = "Assets/Resources";
        public const string FOLDER_EDITOR_RESOURCES = "Assets/Resources/Editor";

        static Profile LoadActiveProfileEditor()
        {
            // ── 1. Try the GUID stored in EditorPrefs (Pro profile switching) ──────
            string activeGuid = EditorPrefs.GetString(PREF_ACTIVE_PROFILE_GUID, "");
            if (!string.IsNullOrEmpty(activeGuid))
            {
                string guidPath = AssetDatabase.GUIDToAssetPath(activeGuid);
                if (!string.IsNullOrEmpty(guidPath))
                {
                    var byGuid = AssetDatabase.LoadAssetAtPath<Profile>(guidPath);
                    if (byGuid != null)
                        return byGuid;
                }
            }

            // ── 2. Try both resource folders in priority order (EditorResources first) ──
            string[] searchPaths = { FOLDER_EDITOR_RESOURCES, FOLDER_RESOURCES };
            foreach (var folder in searchPaths)
            {
                // New name first
                string prefix = folder == FOLDER_EDITOR_RESOURCES ? "Editor/" : "";
                var asset = Resources.Load<Profile>(prefix + PROFILE_NAME);
                if (asset != null)
                    return asset;
            }

            // ── 2b. Safety net: Resources.Load relies on Unity's resource index, which can be
            // transiently stale right after a domain reload (e.g. immediately following a
            // scripting-define change such as Pro activation). Before concluding no Profile
            // exists, do a direct path-based load — this bypasses the resource index entirely
            // and catches the case where the file is present on disk but Resources.Load missed it.
            foreach (var folder in searchPaths)
            {
                string directPath = $"{folder}/{PROFILE_NAME}.asset";
                var asset = AssetDatabase.LoadAssetAtPath<Profile>(directPath);
                if (asset != null)
                {
                    Debug.LogWarning(
                        $"[SecondBrain] Resources.Load missed an existing Profile at '{directPath}' " +
                        "(stale resource index after reload) — recovered it via direct path load " +
                        "instead of creating a new one.");
                    return asset;
                }
            }

            // ── 3. Create a new Profile asset in EditorResources (default) ──────────
            EnsureFolderPath(FOLDER_EDITOR_RESOURCES);
            string newPath = $"{FOLDER_EDITOR_RESOURCES}/{PROFILE_NAME}.asset";

            // Guard against AssetDatabase.CreateAsset silently clobbering a file that already
            // exists at this path (which would happen if the checks above still failed to find
            // a Profile that is genuinely there, e.g. it exists but is a different/corrupt type).
            // Fall back to a uniquely-numbered path instead of overwriting whatever is there —
            // callers rely on Profile.Active always being non-null, so we still return a usable
            // Profile, just without touching the pre-existing file.
            if (AssetDatabase.LoadMainAssetAtPath(newPath) != null)
            {
                Debug.LogError(
                    $"[SecondBrain] An asset already exists at '{newPath}' but could not be loaded " +
                    "as a Profile — creating a new Profile under a different name instead of " +
                    "overwriting it. Please check the existing file manually.");
                newPath = AssetDatabase.GenerateUniqueAssetPath(newPath);
            }

            var newAsset = CreateInstance<Profile>();
            AssetDatabase.CreateAsset(newAsset, newPath);
            AssetDatabase.SaveAssets();
            EditorUtility.SetDirty(newAsset);
            AssetDatabase.Refresh();
            return newAsset;
        }

        public static void EnsureFolderPath(string folderPath)
        {
            string[] parts = folderPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        /// <summary>Clears the cached active Profile so the next Active access reloads from disk.</summary>
        public static void InvalidateCache() => active = null;

        /// <summary>
        /// Returns true when a Profile is already held in the static cache, without triggering
        /// a load or auto-creation. Safe to call from AssetPostprocessor callbacks.
        /// </summary>
        public static bool IsActiveProfileCached => active != null;

        /// <summary>
        /// Returns the folder path that the given Profile currently resides in.
        /// </summary>
        public static string GetCurrentFolder(Profile profile)
        {
            if (profile == null) return FOLDER_EDITOR_RESOURCES;
            string path = AssetDatabase.GetAssetPath(profile);
            if (string.IsNullOrEmpty(path)) return FOLDER_EDITOR_RESOURCES;
            return System.IO.Path.GetDirectoryName(path)?.Replace("\\", "/") ?? FOLDER_EDITOR_RESOURCES;
        }

        /// <summary>
        /// Returns the <see cref="DataStorageLocation"/> inferred from the Profile's on-disk folder.
        /// </summary>
        public static DataStorageLocation GetCurrentLocation(Profile profile)
        {
            string folder = GetCurrentFolder(profile);
            return folder.EndsWith("Editor", StringComparison.OrdinalIgnoreCase)
                ? DataStorageLocation.EditorResources
                : DataStorageLocation.Resources;
        }
#endif

        static Profile active;
    }
}
