using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SecretZauce.SecondBrain
{
    public enum MotherbaseInitializationState
    {
        Uninitialized = 0,
        FreeVersionInitialized = 1,
        ProVersionInitialized = 2,
        InitializationCompleted = 3,
    }

    public class Motherbase : ScriptableObject, IStructure<Base>
    {
        const string MOTHERBASE_NAME = "Motherbase";
        public List<Base> Children => baseList;
        [SerializeField] List<Base> baseList = new List<Base>();
        [SerializeField, HideInInspector] int initializationProgress;
        [SerializeField, HideInInspector] MotherbaseInitializationState initializationState;

        public MotherbaseInitializationState InitializationState
        {
            get => initializationState;
            set
            {
                initializationState = value;
#if UNITY_EDITOR
                EditorUtility.SetDirty(this);
#endif
            }
        }
        
        // Optional reference to the Default Base. When set, this Base is considered
        // the project's default and can be used by UI or startup logic.
        [SerializeField]
        private Base defaultBase;

        /// <summary>
        /// The Base marked as the default for this Motherbase. May be null.
        /// </summary>
        public Base DefaultBase
        {
            get => defaultBase;
            set => defaultBase = value;
        }

        /// <summary>
        /// Returns <c>true</c> when all bits in <paramref name="stepFlag"/> are set in the
        /// initialization progress mask (for example: 1, 2, 4, 8).
        /// </summary>
        public bool HasInitializationStepCompleted(int stepFlag)
        {
            if (stepFlag <= 0)
                return false;
            return (initializationProgress & stepFlag) == stepFlag;
        }

        /// <summary>
        /// Marks one or more initialization step bits as completed.
        /// <paramref name="stepFlag"/> should use power-of-two values (for example: 1, 2, 4, 8).
        /// </summary>
        public void MarkInitializationStepCompleted(int stepFlag)
        {
            if (stepFlag <= 0)
                return;
            initializationProgress |= stepFlag;
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
        }
      
        public Base CreateNew(Type type)
        {
           return CreateInstance<Base>(); 
        }

        public bool CanAcceptChild(Object child)
        {
            return child is Base;
        }

        public static Motherbase Home
        {
            get
            {
                if (home == null)
                    home = LoadInstance();
                
                return home;
            }
        }
        
        public static Motherbase LoadInstance()
        {
#if UNITY_EDITOR
            return LoadInstanceEditor();
#else
            return Resources.Load<Motherbase>(MOTHERBASE_NAME);
#endif
        }

#if UNITY_EDITOR
        // Key shared with BrowserSettings so both read the same EditorPrefs entry.
        const string PREF_STORAGE_LOCATION  = "Browser_StorageLocation_v1";
        const string FOLDER_RESOURCES        = "Assets/Resources";
        const string FOLDER_EDITOR_RESOURCES = "Assets/Resources/Editor";

        static Motherbase LoadInstanceEditor()
        {
            bool useEditorFolder = EditorPrefs.GetInt(PREF_STORAGE_LOCATION, 0) == 1;
            string primaryLoad   = useEditorFolder ? "Editor/" + MOTHERBASE_NAME : MOTHERBASE_NAME;
            string fallbackLoad  = useEditorFolder ? MOTHERBASE_NAME : "Editor/" + MOTHERBASE_NAME;
            string primaryFolder = useEditorFolder ? FOLDER_EDITOR_RESOURCES : FOLDER_RESOURCES;

            // Try the configured location first, fall back to the other.
            var asset = Resources.Load<Motherbase>(primaryLoad);
            if (asset == null)
                asset = Resources.Load<Motherbase>(fallbackLoad);

            if (asset == null)
            {
                EnsureFolderPath(primaryFolder);
                asset = CreateInstance<Motherbase>();
                AssetDatabase.CreateAsset(asset, $"{primaryFolder}/{MOTHERBASE_NAME}.asset");
                AssetDatabase.SaveAssets();
                EditorUtility.SetDirty(asset);
                AssetDatabase.Refresh();
            }

            return asset;
        }

        static void EnsureFolderPath(string folderPath)
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

        /// <summary>Clears the cached Motherbase instance so the next Home access reloads from disk.</summary>
        public static void InvalidateCache() => home = null;

        /// <summary>Returns the asset folder that matches the current storage-location preference.</summary>
        public static string GetConfiguredFolder() =>
            EditorPrefs.GetInt(PREF_STORAGE_LOCATION, 0) == 1
                ? FOLDER_EDITOR_RESOURCES
                : FOLDER_RESOURCES;
#endif

        static Motherbase home;
    }
}
