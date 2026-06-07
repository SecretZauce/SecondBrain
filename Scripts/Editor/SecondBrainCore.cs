using System.Collections.Generic;
using SecretZauce.SecondBrain;
using UnityEditor;
using UnityEngine;

namespace SecretZauce.SecondBrain.Editor
{
    public enum ProfileInitializationState
    {
        Uninitialized = 0,
        FreeVersionInitialized = 1,
        ProVersionInitialized = 2,
        InitializationCompleted = 3,
    }

    /// <summary>
    /// Editor-only singleton ScriptableObject that owns:
    ///   - Initialization progress and state for the first-time setup flow.
    ///   - The registry of all known Profile assets.
    /// Stored at Assets/Resources/Editor/SecondBrainCore.asset (excluded from player builds).
    /// </summary>
    public class SecondBrainCore : ScriptableObject
    {
        const string ResourcePath = "Editor/SecondBrainCore";
        internal const string AssetPath = "Assets/Resources/Editor/SecondBrainCore.asset";

        [SerializeField, HideInInspector] int initializationProgress;
        [SerializeField, HideInInspector] ProfileInitializationState initializationState;
        [SerializeField] List<Profile> profiles = new List<Profile>();

        // ── Initialization state ───────────────────────────────────────────────

        public ProfileInitializationState InitializationState
        {
            get => initializationState;
            set
            {
                initializationState = value;
                EditorUtility.SetDirty(this);
            }
        }

        public bool HasInitializationStepCompleted(int stepFlag)
        {
            if (stepFlag <= 0) return false;
            return (initializationProgress & stepFlag) == stepFlag;
        }

        public void MarkInitializationStepCompleted(int stepFlag)
        {
            if (stepFlag <= 0) return;
            initializationProgress |= stepFlag;
            EditorUtility.SetDirty(this);
        }

        // ── Profile registry ───────────────────────────────────────────────────

        public List<Profile> Profiles => profiles;

        /// <summary>
        /// Ensures <paramref name="profile"/> is tracked in the registry.
        /// Silently no-ops when already present or null.
        /// </summary>
        public void RegisterProfile(Profile profile)
        {
            if (profile == null || profiles.Contains(profile)) return;
            profiles.Add(profile);
            EditorUtility.SetDirty(this);
        }

        /// <summary>
        /// Scans both Resource folders for Profile assets, adds any not yet registered,
        /// and removes stale null entries. Returns true when the list changed.
        /// </summary>
        public bool SyncProfiles()
        {
            bool changed = profiles.RemoveAll(p => p == null) > 0;

            var guids = AssetDatabase.FindAssets("t:Profile",
                new[] { Profile.FOLDER_EDITOR_RESOURCES, Profile.FOLDER_RESOURCES });

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;
                var p = AssetDatabase.LoadAssetAtPath<Profile>(path);
                if (p == null || profiles.Contains(p)) continue;
                profiles.Add(p);
                changed = true;
            }

            if (changed)
                EditorUtility.SetDirty(this);

            return changed;
        }

        // ── Singleton ──────────────────────────────────────────────────────────

        static SecondBrainCore instance;

        public static SecondBrainCore Instance
        {
            get
            {
                if (instance == null)
                    instance = LoadOrCreate();
                return instance;
            }
        }

        public static void InvalidateCache() => instance = null;

        static SecondBrainCore LoadOrCreate()
        {
            var loaded = Resources.Load<SecondBrainCore>(ResourcePath);
            if (loaded != null) return loaded;

            loaded = AssetDatabase.LoadAssetAtPath<SecondBrainCore>(AssetPath);
            if (loaded != null) return loaded;

            Profile.EnsureFolderPath(Profile.FOLDER_EDITOR_RESOURCES);
            var newAsset = CreateInstance<SecondBrainCore>();
            AssetDatabase.CreateAsset(newAsset, AssetPath);
            AssetDatabase.SaveAssets();
            return newAsset;
        }
    }
}
