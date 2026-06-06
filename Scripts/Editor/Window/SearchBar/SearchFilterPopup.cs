using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SecretZauce.SecondBrain.Editor
{
    [Flags]
    public enum SearchFilterFlags
    {
        None              = 0,
        Containers        = 1 << 0,
        SceneObjects      = 1 << 1,
        Scenes            = 1 << 2,
        TextAssets        = 1 << 3,
        ScriptableObjects = 1 << 4,
        Prefabs           = 1 << 5,
        Assets            = 1 << 6,
        All               = Containers | SceneObjects | Scenes | TextAssets | ScriptableObjects | Prefabs | Assets,
    }

    class SearchFilterPopup : EditorWindow
    {
        const string PrefsKey = "SecondBrain_SearchFilter";
        // Default: all types shown except Containers
        const SearchFilterFlags DefaultFilter = SearchFilterFlags.All & ~SearchFilterFlags.Containers;

        static SearchFilterPopup _instance;
        static double _lastClosedAt = double.NegativeInfinity;

        static SearchFilterFlags _filter;
        static bool _filterLoaded;

        const float PopupWidth  = 200f;
        const float PopupHeight = 224f;

        // ── Static state API ─────────────────────────────────────────────────

        static SearchFilterFlags Filter
        {
            get
            {
                if (!_filterLoaded) Load();
                return _filter;
            }
            set
            {
                _filter = value;
                _filterLoaded = true;
                Save();
            }
        }

        static void Load()
        {
            _filterLoaded = true;
            int stored = EditorPrefs.GetInt(PrefsKey, -1);
            _filter = stored == -1 ? DefaultFilter : (SearchFilterFlags)stored;
        }

        static void Save()
        {
            try { EditorPrefs.SetInt(PrefsKey, (int)_filter); } catch { }
        }

        /// <summary>
        /// Returns true when the object's type is allowed by the current filter.
        /// If filter is None (Clear All state), everything passes through.
        /// </summary>
        public static bool PassesFilter(Object obj)
        {
            var f = Filter;
            if (f == SearchFilterFlags.None) return true;
            return (f & GetTypeFlag(obj)) != 0;
        }

        static SearchFilterFlags GetTypeFlag(Object obj)
        {
            if (obj is Container)       return SearchFilterFlags.Containers;
            if (obj is SceneObject)     return SearchFilterFlags.SceneObjects;
            if (obj is SceneAsset)      return SearchFilterFlags.Scenes;
            if (obj is TextAsset)       return SearchFilterFlags.TextAssets;
            if (obj is GameObject)      return SearchFilterFlags.Prefabs;
            if (obj is ScriptableObject) return SearchFilterFlags.ScriptableObjects;
            return SearchFilterFlags.Assets;
        }

        /// <summary>
        /// True when a non-trivial subset is active (neither None nor All),
        /// used to highlight the filter button.
        /// </summary>
        public static bool IsFilterActive
        {
            get
            {
                var f = Filter;
                return f != SearchFilterFlags.None && f != SearchFilterFlags.All;
            }
        }

        // ── Popup lifecycle ───────────────────────────────────────────────────

        public static void TogglePopup(Rect buttonScreenRect)
        {
            if (_instance != null)
            {
                _instance.Close();
                _instance = null;
                return;
            }

            if (EditorApplication.timeSinceStartup - _lastClosedAt < 0.01d)
                return;

            var wnd = CreateInstance<SearchFilterPopup>();
            wnd.ShowAsDropDown(buttonScreenRect, new Vector2(PopupWidth, PopupHeight));
            _instance = wnd;
        }

        void OnDisable()
        {
            _lastClosedAt = EditorApplication.timeSinceStartup;
            _instance = null;
        }

        // ── Drawing ───────────────────────────────────────────────────────────

        void OnGUI()
        {
            EditorGUI.BeginChangeCheck();
            var filter = Filter;

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Search Filter", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Select All", EditorStyles.miniButton))
                filter = SearchFilterFlags.All;
            if (GUILayout.Button("Clear All", EditorStyles.miniButton))
                filter = SearchFilterFlags.None;
            EditorGUILayout.EndHorizontal();

            EditorGUIUtils.DrawSeparator();

            DrawToggle(ref filter, SearchFilterFlags.Containers,        "Containers");
            DrawToggle(ref filter, SearchFilterFlags.SceneObjects,      "Scene Objects");
            DrawToggle(ref filter, SearchFilterFlags.Scenes,            "Scenes");
            DrawToggle(ref filter, SearchFilterFlags.TextAssets,        "Text Assets");
            DrawToggle(ref filter, SearchFilterFlags.ScriptableObjects, "Scriptable Objects");
            DrawToggle(ref filter, SearchFilterFlags.Prefabs,           "Prefabs");
            DrawToggle(ref filter, SearchFilterFlags.Assets,            "Assets");

            if (EditorGUI.EndChangeCheck())
                Filter = filter;
        }

        static void DrawToggle(ref SearchFilterFlags filter, SearchFilterFlags flag, string label)
        {
            bool current = filter == SearchFilterFlags.None || (filter & flag) != 0;
            bool next = EditorGUILayout.ToggleLeft(label, current);
            if (next == current) return;
            if (filter == SearchFilterFlags.None)
            {
                // Unchecking from "show everything" state: switch to All then remove the flag
                filter = SearchFilterFlags.All & ~flag;
            }
            else if (next) filter |= flag;
            else           filter &= ~flag;
        }
    }
}
