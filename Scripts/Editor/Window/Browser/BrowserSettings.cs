using System;
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
    /// Where Motherbase.asset (and all Base sub-assets) are stored on disk.
    /// <see cref="EditorResources"/> places them under Assets/Resources/Editor/,
    /// which Unity excludes from player builds.
    /// </summary>
    public enum DataStorageLocation
    {
        /// <summary>Assets/Resources/Editor/ — editor-only, excluded from player builds.</summary>
        EditorResources = 0,
        /// <summary>Assets/Resources/ — included in player builds (original behaviour).</summary>
        Resources = 1,
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
    /// Simple persistent editor settings for the Browser window.
    /// Settings are stored in EditorPrefs so they persist between editor sessions.
    /// Other parts of the code can listen to OnSettingsChanged to react.
    /// </summary>
    public static class BrowserSettings
    {
        const string KeyAskBeforeDeletion = "Browser_AskBeforeDeletion_v1";
        const string KeyAskBeforeRemove = "Browser_AskBeforeRemove_v1";
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
        // Must match the constant in Motherbase.cs so both read the same EditorPrefs entry.
        const string KeyStorageLocation = "Browser_StorageLocation_v1";
        const string KeyExpandAllOnEnterBase = "Browser_ExpandAllOnEnterBase_v1";

        public const int MinItemFontSize = 9;
        public const int MaxItemFontSize = 15;

        public static event Action OnSettingsChanged;

        public static bool AskBeforeDeletion
        {
            get => EditorPrefs.GetBool(KeyAskBeforeDeletion, true);
            set
            {
                if (AskBeforeDeletion == value) return;
                EditorPrefs.SetBool(KeyAskBeforeDeletion, value);
                OnSettingsChanged?.Invoke();
            }
        }

        /// <summary>
        /// When true, a confirmation dialog is shown before removing items from their parent list.
        /// Removing does not delete the underlying asset.
        /// </summary>
        public static bool AskBeforeRemove
        {
            get => EditorPrefs.GetBool(KeyAskBeforeRemove, true);
            set
            {
                if (AskBeforeRemove == value) return;
                EditorPrefs.SetBool(KeyAskBeforeRemove, value);
                OnSettingsChanged?.Invoke();
            }
        }

        /// <summary>
        /// Determines what happens when the user double-clicks a TreeView item.
        /// </summary>
        public static DoubleClickActionType DoubleClickAction
        {
            get => (DoubleClickActionType)EditorPrefs.GetInt(KeyDoubleClickAction, (int)DoubleClickActionType.Enter);
            set
            {
                if (DoubleClickAction == value) return;
                EditorPrefs.SetInt(KeyDoubleClickAction, (int)value);
                OnSettingsChanged?.Invoke();
            }
        }

        public static bool ShowIconsPerType
        {
            get => EditorPrefs.GetBool(KeyShowIconsPerType, true);
            set
            {
                if (ShowIconsPerType == value) return;
                EditorPrefs.SetBool(KeyShowIconsPerType, value);
                OnSettingsChanged?.Invoke();
            }
        }

        public static bool ForceNamingOnCreate
        {
            get => EditorPrefs.GetBool(KeyForceNamingOnCreate, true);
            set
            {
                if (ForceNamingOnCreate == value) return;
                EditorPrefs.SetBool(KeyForceNamingOnCreate, value);
                OnSettingsChanged?.Invoke();
            }
        }

        public static ColorDisplayStyle DefaultColorStyle
        {
            get => (ColorDisplayStyle)EditorPrefs.GetInt(KeyDefaultColorStyle, (int)ColorDisplayStyle.Gradient);
            set
            {
                if (DefaultColorStyle == value) return;
                EditorPrefs.SetInt(KeyDefaultColorStyle, (int)value);
                OnSettingsChanged?.Invoke();
            }
        }

        public static bool DefaultColorFoldoutOnly
        {
            get => EditorPrefs.GetBool(KeyDefaultColorFoldoutOnly, false);
            set
            {
                if (DefaultColorFoldoutOnly == value) return;
                EditorPrefs.SetBool(KeyDefaultColorFoldoutOnly, value);
                OnSettingsChanged?.Invoke();
            }
        }

        /// <summary>
        /// PRO Version Only: Global toggle to enable or disable the Quick Peek popup on hover.
        /// Stored in EditorPrefs so it persists between sessions.
        /// </summary>
        public static bool EnableQuickPeek
        {
            get => EditorPrefs.GetBool(KeyEnableQuickPeek, true);
            set
            {
                if (EnableQuickPeek == value) return;
                EditorPrefs.SetBool(KeyEnableQuickPeek, value);
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
            get => EditorPrefs.GetBool(KeyExpandAllOnEnterBase, false);
            set
            {
                if (ExpandAllOnEnterBase == value) return;
                EditorPrefs.SetBool(KeyExpandAllOnEnterBase, value);
                OnSettingsChanged?.Invoke();
            }
        }

        /// <summary>
        /// Where Motherbase.asset and all Base sub-assets are stored on disk.
        /// Changing this does not move existing assets; use the Settings window move button.
        /// </summary>
        public static DataStorageLocation StorageLocation
        {
            get => (DataStorageLocation)EditorPrefs.GetInt(KeyStorageLocation, (int)DataStorageLocation.EditorResources);
            set
            {
                if (StorageLocation == value) return;
                EditorPrefs.SetInt(KeyStorageLocation, (int)value);
                OnSettingsChanged?.Invoke();
            }
        }

        /// <summary>
        /// When true, the Scene Linking feature is globally enabled:
        /// opening a scene will auto-open SecondBrainWindow windows for linked Bases.
        /// </summary>
        public static bool EnableSceneLinking
        {
            get => EditorPrefs.GetBool(KeyEnableSceneLinking, true);
            set
            {
                if (EnableSceneLinking == value) return;
                EditorPrefs.SetBool(KeyEnableSceneLinking, value);
                OnSettingsChanged?.Invoke();
            }
        }

        /// <summary>
        /// When true, SecondBrainWindow windows opened by Scene Linking will be
        /// automatically closed when the associated scene is closed.
        /// </summary>
        public static bool CloseOnSceneClose
        {
            get => EditorPrefs.GetBool(KeyCloseOnSceneClose, false);
            set
            {
                if (CloseOnSceneClose == value) return;
                EditorPrefs.SetBool(KeyCloseOnSceneClose, value);
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
            get => (DefaultExpandOption)EditorPrefs.GetInt(KeyDefaultExpandOption, (int)DefaultExpandOption.ExpandAsDefault);
            set
            {
                if (DefaultExpandOption == value) return;
                EditorPrefs.SetInt(KeyDefaultExpandOption, (int)value);
                OnSettingsChanged?.Invoke();
            }
        }

        /// <summary>
        /// PRO Version Only: Default layout mode (Tabs / Foldouts) used by Quick Peek and
        /// ContainerChildrenInspector when no per-container preference exists.
        /// </summary>
        public static ChildViewMode DefaultQuickPeekLayout
        {
            get => (ChildViewMode)EditorPrefs.GetInt(KeyDefaultQuickPeekLayout, (int)ChildViewMode.Foldouts);
            set
            {
                if (DefaultQuickPeekLayout == value) return;
                EditorPrefs.SetInt(KeyDefaultQuickPeekLayout, (int)value);
                OnSettingsChanged?.Invoke();
            }
        }

        /// <summary>
        /// Controls the visual density of TreeView items (font size + row height).
        /// </summary>
        public static ItemSizeOption ItemSize
        {
            get => (ItemSizeOption)EditorPrefs.GetInt(KeyItemSize, (int)ItemSizeOption.Medium);
            set
            {
                if (ItemSize == value) return;
                EditorPrefs.SetInt(KeyItemSize, (int)value);
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
                if (EditorPrefs.HasKey(KeyItemFontSize))
                    return Mathf.Clamp(EditorPrefs.GetInt(KeyItemFontSize, 12), MinItemFontSize, MaxItemFontSize);

                return GetLegacyFontSize(ItemSize);
            }
            set
            {
                int clamped = Mathf.Clamp(value, MinItemFontSize, MaxItemFontSize);
                bool hasStoredValue = EditorPrefs.HasKey(KeyItemFontSize);
                if (hasStoredValue && ItemFontSize == clamped)
                    return;

                EditorPrefs.SetInt(KeyItemFontSize, clamped);
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
            if (EditorPrefs.HasKey(KeyItemFontSize))
                return Mathf.Max(EditorGUIUtility.singleLineHeight, ItemFontSize + 9f);

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
