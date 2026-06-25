using UnityEditor;
using UnityEngine;

namespace SecretZauce.SecondBrain.Editor
{
    /// <summary>
    /// Settings popup to control browser flags.
    /// Shown as a dropdown popup anchored to the BrowserToolbar settings button.
    /// Closes automatically when the user clicks outside.
    /// </summary>
    class SettingsWindow : EditorWindow
    {
        static SettingsWindow instance;
        static double lastDisabledAt = double.NegativeInfinity;

        public static bool IsVisible => instance != null;

        static GUIStyle s_CollapsibleHeaderStyle;

        // Persisted foldout prefs keys
        const string PrefInteraction = "SettingsWindow_Interaction";
        const string PrefGeneral = "SettingsWindow_General";
        const string PrefConfirmation = "SettingsWindow_Confirmation";
        const string PrefNewContainer = "SettingsWindow_NewContainer";
        const string PrefSceneLinking = "SettingsWindow_SceneLinking";

        // Foldout states (persisted to EditorPrefs)
        bool interactionFoldout = true;
        bool generalFoldout = true;
        bool confirmationFoldout = true;
        bool newContainerFoldout = true;
        bool sceneLinkingFoldout = true;

        // Scroll position for the settings content
        Vector2 scrollPos;

        // Popup dimensions
        const float PopupWidth = 320f;
        const float PopupHeight = 380f;

        /// <summary>
        /// Toggle the settings popup open/closed.
        /// <paramref name="buttonRect"/> must be the settings button rect in local GUI space
        /// (as returned by <see cref="GUILayoutUtility.GetLastRect"/>); it is converted to
        /// screen coordinates internally.
        /// </summary>
        public static void TogglePopup(Rect windowRect, Rect buttonRect)
        {
            // Toggle: close if already open
            if (instance != null)
            {
                instance.Close();
                instance = null;
                return;
            }

            // Prevent immediate reopen after auto-close or manual close.
            if (EditorApplication.timeSinceStartup - lastDisabledAt < 0.01d)
                return;

            var wnd = CreateInstance<SettingsWindow>();

            // Convert button rect from local GUI space to screen coordinates.
            // Pass a zero-height activator rect at the button's top so ShowAsDropDown
            // places the popup's top-left at the button's top-left corner.
            var screenRect = GUIUtility.GUIToScreenRect(buttonRect);
            wnd.ShowAsDropDown(new Rect(windowRect.x + windowRect.width, screenRect.y + EditorGUIUtility.singleLineHeight, screenRect.width, 0), new Vector2(PopupWidth, PopupHeight));

            // Assign after ShowAsDropDown succeeds so the instance reference is only
            // set when the window is actually visible (guards the toggle check above).
            instance = wnd;
        }

        void OnEnable()
        {
            // Load persisted foldout states (default to expanded)
            try { interactionFoldout = EditorPrefs.GetInt(PrefInteraction, 1) == 1; } catch { interactionFoldout = true; }
            try { generalFoldout = EditorPrefs.GetInt(PrefGeneral, 1) == 1; } catch { generalFoldout = true; }
            try { confirmationFoldout = EditorPrefs.GetInt(PrefConfirmation, 1) == 1; } catch { confirmationFoldout = true; }
            try { newContainerFoldout = EditorPrefs.GetInt(PrefNewContainer, 1) == 1; } catch { newContainerFoldout = true; }
            try { sceneLinkingFoldout = EditorPrefs.GetInt(PrefSceneLinking, 1) == 1; } catch { sceneLinkingFoldout = true; }
        }

        void OnDisable()
        {
            lastDisabledAt = EditorApplication.timeSinceStartup;
            instance = null;

            // Ensure foldout states are saved when the window is closed
            try { EditorPrefs.SetInt(PrefInteraction, interactionFoldout ? 1 : 0); } catch { }
            try { EditorPrefs.SetInt(PrefGeneral, generalFoldout ? 1 : 0); } catch { }
            try { EditorPrefs.SetInt(PrefConfirmation, confirmationFoldout ? 1 : 0); } catch { }
            try { EditorPrefs.SetInt(PrefNewContainer, newContainerFoldout ? 1 : 0); } catch { }
            try { EditorPrefs.SetInt(PrefSceneLinking, sceneLinkingFoldout ? 1 : 0); } catch { }
        }

        void OnGUI()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            EditorGUI.BeginChangeCheck();


            // ── Interaction ────────────────────────────────────────────────────
            // Local copies of settings so we can still apply changes from
            // collapsed sections correctly when the user opens them.
            var doubleClickAction = BrowserSettings.DoubleClickAction;
            var searchAutoSelect = BrowserSettings.SearchAutoSelect;
            bool quickPeek = BrowserSettings.EnableQuickPeek;

            if (DrawCollapsibleHeader(ref interactionFoldout, "Interaction", PrefInteraction))
            {
                EditorGUI.indentLevel++;
                doubleClickAction = (DoubleClickActionType)EditorGUILayout.EnumPopup(
                    new GUIContent("Double Click Action", "What happens when you double-click a TreeView item.\n• Rename: begin inline rename (original behaviour).\n• Enter: trigger the item's enter action (open scene, enter base, execute action, …). Falls back to rename if no enter action."),
                    doubleClickAction);

                searchAutoSelect = (SearchAutoSelectMode)EditorGUILayout.EnumPopup(
                    new GUIContent("Search Auto-Select", "Controls when typing in the search bar automatically selects the first matching result.\n• Always — always select the first match as you type.\n• Quick Browse Mode (Pro) — only auto-select when using the Quick Browse popup.\n• Off — never auto-select; navigate manually with the arrow keys."),
                    searchAutoSelect);

#if SECOND_BRAIN_PRO
                quickPeek = EditorGUILayout.ToggleLeft(
                    new GUIContent("Enable Quick Peek on hover", "Show the floating Quick Peek inspector when hovering over items."),
                    quickPeek);
#endif
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.Space(6);

            // ── General ────────────────────────────────────────────────────────
            bool icons = BrowserSettings.ShowIconsPerType;
            bool forceNaming = BrowserSettings.ForceNamingOnCreate;
            int itemFontSize = BrowserSettings.ItemFontSize;
            bool expandAllOnEnterBase = BrowserSettings.ExpandAllOnEnterBase;

            if (DrawCollapsibleHeader(ref generalFoldout, "General", PrefGeneral))
            {
                EditorGUI.indentLevel++;
                icons = EditorGUILayout.ToggleLeft(
                    new GUIContent("Show icons per type", "Show small icons beside each item based on its type."),
                    icons);

                forceNaming = EditorGUILayout.ToggleLeft(
                    new GUIContent("Force naming on create", "Show an inline name input before creating a new item."),
                    forceNaming);

                expandAllOnEnterBase = EditorGUILayout.ToggleLeft(
                    new GUIContent("Expand all on enter base", "When enabled, all containers in a Base are automatically expanded when you navigate into it."),
                    expandAllOnEnterBase);

                itemFontSize = EditorGUILayout.IntSlider(
                    new GUIContent("Font Size", "Controls TreeView item font size. Row height scales with the value."),
                    itemFontSize,
                    BrowserSettings.MinItemFontSize,
                    BrowserSettings.MaxItemFontSize);

                EditorGUI.indentLevel--;
            }
            EditorGUILayout.Space(6);
            
            // ── New Container Defaults ─────────────────────────────────────────
            ColorDisplayStyle colorStyle = BrowserSettings.DefaultColorStyle;
            bool foldoutOnly = BrowserSettings.DefaultColorFoldoutOnly;
            var expandOption = BrowserSettings.DefaultExpandOption;
#if SECOND_BRAIN_PRO
            var quickPeekLayout = BrowserSettings.DefaultQuickPeekLayout;
#endif

            if (DrawCollapsibleHeader(ref newContainerFoldout, "New Container Defaults", PrefNewContainer))
            {
                EditorGUI.indentLevel++;
                colorStyle = (ColorDisplayStyle)EditorGUILayout.EnumPopup(
                    new GUIContent("Color Style", "Default color display style applied to newly created Containers."),
                    colorStyle);

                foldoutOnly = EditorGUILayout.ToggleLeft(
                    new GUIContent("Foldout only", "When enabled, color is applied only to the Container row and not to its children."),
                    foldoutOnly);

                expandOption = (DefaultExpandOption)EditorGUILayout.EnumPopup(
                    new GUIContent("Container Expand", "Default expand/collapse state for the container node itself in the tree view."),
                    expandOption);

#if SECOND_BRAIN_PRO
                quickPeekLayout = (ChildViewMode)EditorGUILayout.EnumPopup(
                    new GUIContent("Preferred Child View", "Default layout for the child view (Quick Peek / Container Children) when no per-container preference has been set.\n• Tabs — one child per tab.\n• Foldouts — all children stacked as foldouts."),
                    quickPeekLayout);
#endif


                EditorGUI.indentLevel--;
            }
            EditorGUILayout.Space(6);

#if SECOND_BRAIN_PRO
            // ── Scene Linking ──────────────────────────────────────────────────
            bool enableSceneLinking = BrowserSettings.EnableSceneLinking;
            bool closeOnSceneClose = BrowserSettings.CloseOnSceneClose;

            if (DrawCollapsibleHeader(ref sceneLinkingFoldout, "Scene Linking", PrefSceneLinking))
            {
                EditorGUI.indentLevel++;
                enableSceneLinking = EditorGUILayout.ToggleLeft(
                    new GUIContent("Enable Scene Linking", "When enabled, opening a scene will automatically open SecondBrainWindow windows for linked Bases."),
                    enableSceneLinking);

                EditorGUI.indentLevel++;
                using (new EditorGUI.DisabledScope(!enableSceneLinking))
                {
                    closeOnSceneClose = EditorGUILayout.ToggleLeft(
                        new GUIContent("Close on scene close", "When enabled, windows opened by Scene Linking are automatically closed when the associated scene is closed."),
                        closeOnSceneClose);
                }
                EditorGUI.indentLevel--;
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.Space(6);
#endif

            // Apply all setting changes in one place after drawing the full UI so
            // clicks/toggles don't cause the popup to close before changes are recorded.
            if (EditorGUI.EndChangeCheck())
            {
                BrowserSettings.DoubleClickAction = doubleClickAction;
                BrowserSettings.SearchAutoSelect = searchAutoSelect;
                BrowserSettings.EnableQuickPeek = quickPeek;
                BrowserSettings.ShowIconsPerType = icons;
                BrowserSettings.ForceNamingOnCreate = forceNaming;
                BrowserSettings.ExpandAllOnEnterBase = expandAllOnEnterBase;
                BrowserSettings.ItemFontSize = itemFontSize;
                BrowserSettings.DefaultColorStyle = colorStyle;
                BrowserSettings.DefaultColorFoldoutOnly = foldoutOnly;
                BrowserSettings.DefaultExpandOption = expandOption;
#if SECOND_BRAIN_PRO
                BrowserSettings.DefaultQuickPeekLayout = quickPeekLayout;
                BrowserSettings.EnableSceneLinking = enableSceneLinking;
                BrowserSettings.CloseOnSceneClose = closeOnSceneClose;
#endif
            }

            EditorGUILayout.EndScrollView();
        }


        // Draw a collapsible section header that visually matches the
        // foldout-style headers used in QuickPeekWindow (bold foldout text with
        // a subtle background). The state is persisted using EditorPrefs.
        bool DrawCollapsibleHeader(ref bool state, string text, string prefsKey)
        {
            float headerHeight = EditorGUIUtility.singleLineHeight + 6f;
            Rect headerRect = GUILayoutUtility.GetRect(0, headerHeight, GUILayout.ExpandWidth(true));
            Color headerBg = EditorGUIUtility.isProSkin
                ? new Color(0.16f, 0.16f, 0.16f)
                : new Color(0.83f, 0.83f, 0.83f);
            EditorGUI.DrawRect(headerRect, headerBg);

            if (s_CollapsibleHeaderStyle == null)
                s_CollapsibleHeaderStyle = new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold };

            Rect foldRect = new Rect(headerRect.x + 4, headerRect.y + 3, headerRect.width - 8, EditorGUIUtility.singleLineHeight);
            bool newState = EditorGUI.Foldout(foldRect, state, text, true, s_CollapsibleHeaderStyle);
            if (newState != state)
            {
                state = newState;
                try { EditorPrefs.SetInt(prefsKey, state ? 1 : 0); } catch { }
            }

            return state;
        }
    }
}