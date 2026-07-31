using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SecretZauce.SecondBrain.Editor
{
    /// <summary>
    /// Controls the visual size of items in the TreeView (font size + row height).
    /// </summary>
    public enum ItemSizeOption
    {
        Tiny = 0,
        Small = 1,
        Medium = 2,
        Large = 3,
        ExtraLarge = 4,
    }


    /// <summary>
    /// Determines what happens when the user double-clicks a TreeView item.
    /// </summary>
    public enum DoubleClickActionType
    {
        /// <summary>Double-click triggers the Enter action on items that support it (Base, SceneAsset, Prefab, ActionItem, …).
        /// Falls back to rename on items without an Enter action.</summary>
        Enter = 0,
        /// <summary>Double-click begins an inline rename (original behaviour).</summary>
        Rename = 1,
    }

    /// <summary>
    /// Controls when searching auto-selects the first matching result.
    /// </summary>
    public enum SearchAutoSelectMode
    {
        /// <summary>Always auto-select the first result when the search text changes.</summary>
        Always = 0,
        /// <summary>PRO only — auto-select only when the window is in Quick Browse (popup) mode.</summary>
        QuickBrowseOnly = 1,
        /// <summary>Never auto-select; user must navigate manually.</summary>
        Off = 2,
    }

    /// <summary>
    /// Simple persistent editor settings for the Browser window.
    /// Settings are stored in EditorPrefs so they persist between editor sessions.
    /// Other parts of the code can listen to OnSettingsChanged to react.
    /// </summary>
    public static class BrowserSettings
    {
        const string KeyFolderFocusPrefix = "Browser_FolderFocus_";

        // Per-folder focus flags are read once per folder row and then served from memory.
        // EditorPrefs is a native call; the TreeView reads these every repaint for every
        // visible folder, which showed up as measurable interop cost on large trees.
        static readonly Dictionary<string, bool> s_FolderFocusCache =
            new Dictionary<string, bool>(StringComparer.Ordinal);

        public static bool GetFolderFocusOnSelect(string folderGuid)
        {
            if (string.IsNullOrEmpty(folderGuid)) return false;
            if (s_FolderFocusCache.TryGetValue(folderGuid, out bool cached))
                return cached;

            bool stored = EditorPrefs.GetBool(KeyFolderFocusPrefix + folderGuid, false);
            s_FolderFocusCache[folderGuid] = stored;
            return stored;
        }

        public static void SetFolderFocusOnSelect(string folderGuid, bool value)
        {
            if (string.IsNullOrEmpty(folderGuid)) return;
            if (value) EditorPrefs.SetBool(KeyFolderFocusPrefix + folderGuid, true);
            else EditorPrefs.DeleteKey(KeyFolderFocusPrefix + folderGuid);
            s_FolderFocusCache[folderGuid] = value;
        }

        const string KeyShowIconsPerType = "Browser_ShowIconsPerType_v1";
        const string KeyForceNamingOnCreate = "Browser_ForceNamingOnCreate_v1";
        const string KeyDefaultColorStyle = "Browser_DefaultColorStyle_v1";
        const string KeyDefaultColorFoldoutOnly = "Browser_DefaultColorFoldoutOnly_v1";
        const string KeyEnableQuickPeek = "Browser_EnableQuickPeek_v1";
        const string KeyEnableSceneLinking = "Browser_EnableSceneLinking_v1";
        const string KeyCloseOnSceneClose = "Browser_CloseOnSceneClose_v1";
        const string KeyDefaultExpandOption = "Browser_DefaultExpandOption_v1";
        const string KeyDefaultQuickPeekLayout = "Browser_DefaultQuickPeekLayout_v1";
        const string KeyDoubleClickAction = "Browser_DoubleClickAction_v1";
        const string KeyItemSize = "Browser_ItemSize_v1";
        const string KeyItemFontSize = "Browser_ItemFontSize_v1";
        const string KeyExpandAllOnEnterBase = "Browser_ExpandAllOnEnterBase_v1";
        const string KeySearchAutoSelect = "Browser_SearchAutoSelect_v1";

        public const int MinItemFontSize = 9;
        public const int MaxItemFontSize = 15;

        public static event Action OnSettingsChanged;

        // ── In-memory cache ───────────────────────────────────────────────────
        // Every getter below used to hit EditorPrefs directly. The TreeView reads
        // several of them per row per IMGUI pass (row height, font size, icon
        // toggle, quick-peek toggle), so a few hundred rows produced tens of
        // thousands of native EditorPrefs calls per mouse move.
        //
        // Values are loaded once per domain reload and kept current by the setters,
        // which write through to both EditorPrefs and the cache. BrowserSettings is
        // the only writer of these keys, so the cache cannot go stale on its own.
        static bool s_CacheLoaded;

        static int  s_DoubleClickAction;
        static bool s_ShowIconsPerType;
        static bool s_ForceNamingOnCreate;
        static int  s_DefaultColorStyle;
        static bool s_DefaultColorFoldoutOnly;
        static bool s_EnableQuickPeek;
        static int  s_SearchAutoSelect;
        static bool s_ExpandAllOnEnterBase;
        static bool s_EnableSceneLinking;
        static bool s_CloseOnSceneClose;
        static int  s_DefaultExpandOption;
        static int  s_DefaultQuickPeekLayout;
        static int  s_ItemSize;
        static int  s_ItemFontSize;
        static bool s_HasItemFontSizeKey;

        static void EnsureCacheLoaded()
        {
            if (s_CacheLoaded) return;
            s_CacheLoaded = true;

            s_DoubleClickAction      = EditorPrefs.GetInt(KeyDoubleClickAction, (int)DoubleClickActionType.Enter);
            s_ShowIconsPerType       = EditorPrefs.GetBool(KeyShowIconsPerType, true);
            s_ForceNamingOnCreate    = EditorPrefs.GetBool(KeyForceNamingOnCreate, true);
            s_DefaultColorStyle      = EditorPrefs.GetInt(KeyDefaultColorStyle, (int)ColorDisplayStyle.Gradient);
            s_DefaultColorFoldoutOnly= EditorPrefs.GetBool(KeyDefaultColorFoldoutOnly, false);
            s_EnableQuickPeek        = EditorPrefs.GetBool(KeyEnableQuickPeek, true);
            s_SearchAutoSelect       = EditorPrefs.GetInt(KeySearchAutoSelect, (int)SearchAutoSelectMode.Always);
            s_ExpandAllOnEnterBase   = EditorPrefs.GetBool(KeyExpandAllOnEnterBase, false);
            s_EnableSceneLinking     = EditorPrefs.GetBool(KeyEnableSceneLinking, true);
            s_CloseOnSceneClose      = EditorPrefs.GetBool(KeyCloseOnSceneClose, false);
            s_DefaultExpandOption    = EditorPrefs.GetInt(KeyDefaultExpandOption, (int)DefaultExpandOption.ExpandAsDefault);
            s_DefaultQuickPeekLayout = EditorPrefs.GetInt(KeyDefaultQuickPeekLayout, (int)ChildViewMode.Foldouts);
            s_ItemSize               = EditorPrefs.GetInt(KeyItemSize, (int)ItemSizeOption.Medium);
            s_HasItemFontSizeKey     = EditorPrefs.HasKey(KeyItemFontSize);
            s_ItemFontSize           = s_HasItemFontSizeKey
                ? Mathf.Clamp(EditorPrefs.GetInt(KeyItemFontSize, 12), MinItemFontSize, MaxItemFontSize)
                : 0;
        }

        /// <summary>
        /// Drops the in-memory cache so the next read re-loads from EditorPrefs.
        /// Only needed if something outside this class edits the backing keys.
        /// </summary>
        public static void ReloadFromPrefs()
        {
            s_CacheLoaded = false;
            s_FolderFocusCache.Clear();
            EnsureCacheLoaded();
        }

        /// <summary>
        /// Determines what happens when the user double-clicks a TreeView item.
        /// </summary>
        public static DoubleClickActionType DoubleClickAction
        {
            get { EnsureCacheLoaded(); return (DoubleClickActionType)s_DoubleClickAction; }
            set
            {
                if (DoubleClickAction == value) return;
                EditorPrefs.SetInt(KeyDoubleClickAction, (int)value);
                s_DoubleClickAction = (int)value;
                OnSettingsChanged?.Invoke();
            }
        }

        public static bool ShowIconsPerType
        {
            get { EnsureCacheLoaded(); return s_ShowIconsPerType; }
            set
            {
                if (ShowIconsPerType == value) return;
                EditorPrefs.SetBool(KeyShowIconsPerType, value);
                s_ShowIconsPerType = value;
                OnSettingsChanged?.Invoke();
            }
        }

        public static bool ForceNamingOnCreate
        {
            get { EnsureCacheLoaded(); return s_ForceNamingOnCreate; }
            set
            {
                if (ForceNamingOnCreate == value) return;
                EditorPrefs.SetBool(KeyForceNamingOnCreate, value);
                s_ForceNamingOnCreate = value;
                OnSettingsChanged?.Invoke();
            }
        }

        public static ColorDisplayStyle DefaultColorStyle
        {
            get { EnsureCacheLoaded(); return (ColorDisplayStyle)s_DefaultColorStyle; }
            set
            {
                if (DefaultColorStyle == value) return;
                EditorPrefs.SetInt(KeyDefaultColorStyle, (int)value);
                s_DefaultColorStyle = (int)value;
                OnSettingsChanged?.Invoke();
            }
        }

        public static bool DefaultColorFoldoutOnly
        {
            get { EnsureCacheLoaded(); return s_DefaultColorFoldoutOnly; }
            set
            {
                if (DefaultColorFoldoutOnly == value) return;
                EditorPrefs.SetBool(KeyDefaultColorFoldoutOnly, value);
                s_DefaultColorFoldoutOnly = value;
                OnSettingsChanged?.Invoke();
            }
        }

        /// <summary>
        /// PRO Version Only: Global toggle to enable or disable the Quick Peek popup on hover.
        /// Stored in EditorPrefs so it persists between sessions.
        /// </summary>
        public static bool EnableQuickPeek
        {
            get { EnsureCacheLoaded(); return s_EnableQuickPeek; }
            set
            {
                if (EnableQuickPeek == value) return;
                EditorPrefs.SetBool(KeyEnableQuickPeek, value);
                s_EnableQuickPeek = value;
                OnSettingsChanged?.Invoke();
            }
        }

        /// <summary>
        /// Controls when searching auto-selects the first matching result.
        /// QuickBrowseOnly requires the PRO edition.
        /// </summary>
        public static SearchAutoSelectMode SearchAutoSelect
        {
            get { EnsureCacheLoaded(); return (SearchAutoSelectMode)s_SearchAutoSelect; }
            set
            {
                if (SearchAutoSelect == value) return;
                EditorPrefs.SetInt(KeySearchAutoSelect, (int)value);
                s_SearchAutoSelect = (int)value;
                OnSettingsChanged?.Invoke();
            }
        }

        // Gate verbose undo/redo debug logs
        public static bool DebugUndoLogging { get; set; } = false;

        /// <summary>
        /// When true, all containers in a Base are expanded when the user navigates into it.
        /// Does not affect foldout state restored on window reopen (domain reload / session restore).
        /// </summary>
        public static bool ExpandAllOnEnterBase
        {
            get { EnsureCacheLoaded(); return s_ExpandAllOnEnterBase; }
            set
            {
                if (ExpandAllOnEnterBase == value) return;
                EditorPrefs.SetBool(KeyExpandAllOnEnterBase, value);
                s_ExpandAllOnEnterBase = value;
                OnSettingsChanged?.Invoke();
            }
        }

        /// <summary>
        /// When true, all containers in a Base are expanded
        /// opening a scene will auto-open SecondBrainWindow windows for linked Bases.
        /// </summary>
        public static bool EnableSceneLinking
        {
            get { EnsureCacheLoaded(); return s_EnableSceneLinking; }
            set
            {
                if (EnableSceneLinking == value) return;
                EditorPrefs.SetBool(KeyEnableSceneLinking, value);
                s_EnableSceneLinking = value;
                OnSettingsChanged?.Invoke();
            }
        }

        /// <summary>
        /// When true, SecondBrainWindow windows opened by Scene Linking will be
        /// automatically closed when the associated scene is closed.
        /// </summary>
        public static bool CloseOnSceneClose
        {
            get { EnsureCacheLoaded(); return s_CloseOnSceneClose; }
            set
            {
                if (CloseOnSceneClose == value) return;
                EditorPrefs.SetBool(KeyCloseOnSceneClose, value);
                s_CloseOnSceneClose = value;
                OnSettingsChanged?.Invoke();
            }
        }

        /// <summary>
        /// Default expand option applied to newly-created containers when no per-container
        /// preference has been configured. Stored in EditorPrefs as an int backing the
        /// DefaultExpandOption enum.
        /// </summary>
        public static DefaultExpandOption DefaultExpandOption
        {
            get { EnsureCacheLoaded(); return (DefaultExpandOption)s_DefaultExpandOption; }
            set
            {
                if (DefaultExpandOption == value) return;
                EditorPrefs.SetInt(KeyDefaultExpandOption, (int)value);
                s_DefaultExpandOption = (int)value;
                OnSettingsChanged?.Invoke();
            }
        }

        /// <summary>
        /// PRO Version Only: Default layout mode (Tabs / Foldouts) used by Quick Peek and
        /// ContainerChildrenInspector when no per-container preference exists.
        /// </summary>
        public static ChildViewMode DefaultQuickPeekLayout
        {
            get { EnsureCacheLoaded(); return (ChildViewMode)s_DefaultQuickPeekLayout; }
            set
            {
                if (DefaultQuickPeekLayout == value) return;
                EditorPrefs.SetInt(KeyDefaultQuickPeekLayout, (int)value);
                s_DefaultQuickPeekLayout = (int)value;
                OnSettingsChanged?.Invoke();
            }
        }

        /// <summary>
        /// Controls the visual density of TreeView items (font size + row height).
        /// </summary>
        public static ItemSizeOption ItemSize
        {
            get { EnsureCacheLoaded(); return (ItemSizeOption)s_ItemSize; }
            set
            {
                if (ItemSize == value) return;
                EditorPrefs.SetInt(KeyItemSize, (int)value);
                s_ItemSize = (int)value;
                OnSettingsChanged?.Invoke();
            }
        }

        /// <summary>
        /// Controls the TreeView font size in points.
        /// Falls back to legacy ItemSize when the slider value has never been saved.
        /// </summary>
        public static int ItemFontSize
        {
            get
            {
                EnsureCacheLoaded();
                return s_HasItemFontSizeKey ? s_ItemFontSize : GetLegacyFontSize(ItemSize);
            }
            set
            {
                EnsureCacheLoaded();
                int clamped = Mathf.Clamp(value, MinItemFontSize, MaxItemFontSize);
                if (s_HasItemFontSizeKey && s_ItemFontSize == clamped)
                    return;

                EditorPrefs.SetInt(KeyItemFontSize, clamped);
                s_HasItemFontSizeKey = true;
                s_ItemFontSize = clamped;
                OnSettingsChanged?.Invoke();
            }
        }

        static int GetLegacyFontSize(ItemSizeOption itemSize)
        {
            switch (itemSize)
            {
                case ItemSizeOption.Tiny:       return 9;
                case ItemSizeOption.Small:      return 11;
                case ItemSizeOption.Medium:     return 12;
                case ItemSizeOption.Large:      return 13;
                case ItemSizeOption.ExtraLarge: return 15;
                default:                        return 12;
            }
        }

        /// <summary>
        /// Returns the font size (in points) for the current ItemSize setting.
        /// A value of 0 means "use the editor default".
        /// </summary>
        public static int GetItemFontSize()
        {
            return ItemFontSize;
        }

        /// <summary>
        /// Returns the row height (in pixels) for the current ItemSize setting.
        /// </summary>
        public static float GetItemRowHeight()
        {
            EnsureCacheLoaded();
            if (s_HasItemFontSizeKey)
                return Mathf.Max(EditorGUIUtility.singleLineHeight, s_ItemFontSize + 9f);

            switch (ItemSize)
            {
                case ItemSizeOption.Tiny:       return 16f;
                case ItemSizeOption.Small:      return 18f;
                case ItemSizeOption.Medium:     return EditorGUIUtility.singleLineHeight;
                case ItemSizeOption.Large:      return 22f;
                case ItemSizeOption.ExtraLarge: return 26f;
                default:                        return EditorGUIUtility.singleLineHeight;
            }
        }
    }
}
