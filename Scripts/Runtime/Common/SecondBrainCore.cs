using UnityEngine;

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
#endif

namespace SecretZauce.SecondBrain
{
#if UNITY_EDITOR
    public enum ProfileInitializationState
    {
        Uninitialized = 0,
        FreeVersionInitialized = 1,
        ProVersionInitialized = 2,
        InitializationCompleted = 3,
    }
#endif

    /// <summary>
    /// Singleton ScriptableObject that owns SecondBrain system configuration.
    /// Stored at Assets/Resources/SecondBrainCore.asset — included in player builds.
    /// Editor-only data (init flow, profile registry) is guarded by UNITY_EDITOR.
    /// </summary>
    public class SecondBrainCore : ScriptableObject
    {
        const string ResourcePath = "SecondBrainCore";
#if UNITY_EDITOR
        internal const string AssetPath    = "Assets/Resources/SecondBrainCore.asset";
        internal const string OldAssetPath = "Assets/Resources/Editor/SecondBrainCore.asset";
#endif

        // ── Build-time config ──────────────────────────────────────────────────

        [SerializeField] string defaultProfileName = "";

        // Empty string means "no profile loaded in builds" (user explicitly chose None).
        // Null/missing core asset falls back to the built-in constant inside Profile.LoadActiveProfile.
        public string DefaultProfileName => defaultProfileName;

#if UNITY_EDITOR
        // ── Initialization state ───────────────────────────────────────────────

        [SerializeField, HideInInspector] int initializationProgress;
        [SerializeField, HideInInspector] ProfileInitializationState initializationState;

        public ProfileInitializationState InitializationState
        {
            get => initializationState;
            set { initializationState = value; EditorUtility.SetDirty(this); }
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

        public void ClearInitializationStep(int stepFlag)
        {
            if (stepFlag <= 0) return;
            initializationProgress &= ~stepFlag;
            EditorUtility.SetDirty(this);
        }

        // ── Profile registry ───────────────────────────────────────────────────

        [SerializeField] List<Profile> profiles = new List<Profile>();

        public List<Profile> Profiles => profiles;

        public void RegisterProfile(Profile profile)
        {
            if (profile == null || profiles.Contains(profile)) return;
            profiles.Add(profile);
            EditorUtility.SetDirty(this);
        }

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
#endif

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

#if UNITY_EDITOR
            loaded = AssetDatabase.LoadAssetAtPath<SecondBrainCore>(AssetPath);
            if (loaded != null) return loaded;

            // Migrate from old editor-only location if present.
            var old = AssetDatabase.LoadAssetAtPath<SecondBrainCore>(OldAssetPath);
            if (old != null)
            {
                Profile.EnsureFolderPath("Assets/Resources");
                string err = AssetDatabase.MoveAsset(OldAssetPath, AssetPath);
                if (string.IsNullOrEmpty(err))
                {
                    AssetDatabase.SaveAssets();
                    return old;
                }
            }

            Profile.EnsureFolderPath("Assets/Resources");
            var newAsset = CreateInstance<SecondBrainCore>();
            AssetDatabase.CreateAsset(newAsset, AssetPath);
            AssetDatabase.SaveAssets();
            return newAsset;
#else
            return null;
#endif
        }
    }
}
