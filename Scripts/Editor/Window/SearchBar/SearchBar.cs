using UnityEngine;
using UnityEditor;
using Object = UnityEngine.Object;

namespace SecretZauce.SecondBrain.Editor
{
    /// <summary>
    /// Handles search bar UI and filtering logic for the tree view.
    /// </summary>
    public class SearchBar
    {
        string searchKeyword      = string.Empty;
        string searchKeywordLower = string.Empty;   // cached ToLowerInvariant — avoids per-node alloc
        public bool IsSearching => !string.IsNullOrEmpty(searchKeyword);

        // ── Style cache ───────────────────────────────────────────────────────
        // GUIStyles are allocated once and reused across repaints.
        // The cache is invalidated when the skin changes or the singleLineHeight changes
        // (e.g. after DPI / display-scale change) so the fixedHeight stays correct.
        static GUIStyle s_SearchFieldStyle;
        static GUIStyle s_CancelButtonStyle;
        static bool     s_StyleCacheProSkin;
        static float    s_StyleCacheLineHeight;
        static Texture  s_FilterIcon;
        static bool     s_FilterIconProSkin;

        static void EnsureSearchStyles()
        {
            bool  ps = EditorGUIUtility.isProSkin;
            float lh = EditorGUIUtility.singleLineHeight;
            if (s_SearchFieldStyle != null && s_StyleCacheProSkin == ps && Mathf.Approximately(s_StyleCacheLineHeight, lh))
                return;

            s_StyleCacheProSkin   = ps;
            s_StyleCacheLineHeight = lh;

            float h        = lh + 2f;
            var   baseStyle = GUI.skin.FindStyle("ToolbarSearchTextField") ?? GUI.skin.textField;
            s_SearchFieldStyle = new GUIStyle(baseStyle)
            {
                fixedHeight = h,
                padding     = new RectOffset(baseStyle.padding.left, baseStyle.padding.right, 4, 4),
            };
            s_CancelButtonStyle = GUI.skin.FindStyle("ToolbarSearchCancelButton") ?? GUI.skin.button;
        }

        static void EnsureFilterIcon()
        {
            bool ps = EditorGUIUtility.isProSkin;
            if (s_FilterIcon != null && s_FilterIconProSkin == ps) return;
            s_FilterIconProSkin = ps;
            s_FilterIcon = IconUtils.Load("filter");
        }

        // ── Internal keyword helpers ──────────────────────────────────────────

        void SetKeyword(string kw)
        {
            searchKeyword      = kw ?? string.Empty;
            searchKeywordLower = searchKeyword.ToLowerInvariant();
        }

        Rect lastSearchBarRect;
        static readonly string searchBarControlName = "SearchBarField_Unique";
        bool skipDrawNextFrame = false;
        bool requestFocusTreeView = false;
        bool requestFocusSearchBar = false;
        bool requestClosePopup = false;
        
        /// <summary>
        /// Draws the search bar UI.
        /// </summary>
        /// <returns>True if the search keyword changed</returns>
        public bool Draw()
        {
            EnsureSearchStyles();

            string previousKeyword = searchKeyword;
            float searchBarHeight  = EditorGUIUtility.singleLineHeight + 2f;

            GUILayout.BeginHorizontal();

            if (skipDrawNextFrame)
            {
                skipDrawNextFrame = false;
                GUILayoutUtility.GetRect(1, searchBarHeight, GUILayout.ExpandWidth(true));
                if (!string.IsNullOrEmpty(searchKeyword))
                {
                    GUILayoutUtility.GetRect(20, searchBarHeight); // filter button
                    GUILayoutUtility.GetRect(20, searchBarHeight); // clear button
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(4);
                return previousKeyword != searchKeyword;
            }

            bool preInterceptDownArrow = false;
            bool preInterceptEscape    = false;
            bool preInterceptCtrlSpace = false;
            {
                bool  preFocused = GUI.GetNameOfFocusedControl() == searchBarControlName;
                Event preEvent   = Event.current;
                if (preFocused && preEvent != null && preEvent.type == EventType.KeyDown)
                {
                    if (preEvent.keyCode == KeyCode.DownArrow)
                    {
                        preInterceptDownArrow = true;
                        preEvent.Use();
                    }
                    else if (preEvent.keyCode == KeyCode.Escape)
                    {
                        preInterceptEscape = true;
                        preEvent.Use();
                    }
                    else if (preEvent.keyCode == KeyCode.Space && (preEvent.control || preEvent.alt))
                    {
                        // Ctrl+Space (Windows/Linux) or Option+Space (Mac) to close popup
                        preInterceptCtrlSpace = true;
                        preEvent.Use();
                    }
                }
            }

            GUI.SetNextControlName(searchBarControlName);
            lastSearchBarRect = GUILayoutUtility.GetRect(new GUIContent(searchKeyword), s_SearchFieldStyle,
                GUILayout.ExpandWidth(true));
            
            // Handle focus request during draw (the proper time to focus controls)
            if (requestFocusSearchBar)
            {
                requestFocusSearchBar = false;
                GUI.FocusControl(searchBarControlName);
            }
            
            string newKeyword = GUI.TextField(lastSearchBarRect, searchKeyword, s_SearchFieldStyle);
            if (newKeyword != searchKeyword)
                SetKeyword(newKeyword);

            // Filter and clear buttons (shown only while searching)
            if (!string.IsNullOrEmpty(searchKeyword))
            {
                EnsureFilterIcon();

                // Filter button — tinted blue when a non-trivial subset is active
                bool filterActive = SearchFilterPopup.IsFilterActive;
                Color prevBg = GUI.backgroundColor;
                if (filterActive)
                    GUI.backgroundColor = new Color(0.45f, 0.72f, 1f, 1f);

                var filterContent = s_FilterIcon != null
                    ? new GUIContent(s_FilterIcon, "Filter search results by type")
                    : new GUIContent("≡", "Filter search results by type");

                Rect filterBtnRect = GUILayoutUtility.GetRect(20, searchBarHeight, GUILayout.Width(20));
                if (GUI.Button(filterBtnRect, filterContent, s_CancelButtonStyle))
                    SearchFilterPopup.TogglePopup(GUIUtility.GUIToScreenRect(filterBtnRect));

                GUI.backgroundColor = prevBg;

                // Clear button
                if (GUILayout.Button("×", s_CancelButtonStyle, GUILayout.Width(20),
                        GUILayout.Height(searchBarHeight)))
                {
                    SetKeyword(string.Empty);
                    GUIUtility.keyboardControl = 0;
                }
            }

            GUILayout.EndHorizontal();
            GUILayout.Space(4);

            bool isFocused = GUI.GetNameOfFocusedControl() == searchBarControlName;
            Event e = Event.current;
            if (isFocused)
            {
                // Option+Space while search bar is focused: close the popup.
                // The text field can consume the KeyDown event (marking it EventType.Used)
                // before HandleKeyboardNavigation sees it, so we cannot restrict to KeyDown only.
                // We DO exclude Layout/Repaint — those passes reflect the physical key state and
                // fire even when the user is not actively pressing anything, which caused premature
                // closes. All real input events (KeyDown, Used, etc.) are handled correctly.
                bool optionHeldKeyDown = e != null && e.alt
                    && e.type != EventType.Layout
                    && e.type != EventType.Repaint;
                if (optionHeldKeyDown)
                {
                    GUIUtility.keyboardControl = 0;
                    requestClosePopup = true;
                    skipDrawNextFrame = true;
                    e.Use();
                }
                else if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter))
                {
                    GUIUtility.keyboardControl = 0;
                    skipDrawNextFrame = true;
                    e.Use();
                }
                else if (e.type == EventType.MouseDown && !lastSearchBarRect.Contains(e.mousePosition))
                {
                    GUIUtility.keyboardControl = 0;
                    skipDrawNextFrame = true;
                }
            }

            if (preInterceptDownArrow)
            {
                requestFocusTreeView = true;
                GUIUtility.keyboardControl = 0;
                return false;
            }

            if (preInterceptEscape)
            {
                bool changed = !string.IsNullOrEmpty(searchKeyword);
                SetKeyword(string.Empty);
                GUIUtility.keyboardControl = 0;
                return changed;
            }

            if (preInterceptCtrlSpace)
            {
                // Request popup close - BrowserWindow will check for this in popup mode
                requestClosePopup = true;
                return false;
            }

            return previousKeyword != searchKeyword;
        }
        
        public bool Matches(Object target)
        {
            if (!IsSearching)
                return true;

            // Type filter: excluded types never count as direct matches.
            // Containers that own matching children will still appear via NodeOrDescendantsMatch.
            if (!SearchFilterPopup.PassesFilter(target))
                return false;

            // Use the cached lowercased keyword — no ToLowerInvariant alloc per row
            return ISearchable.MatchesSearch(target, searchKeywordLower);
        }

        public string GetSearchKeyword()
        {
            return searchKeyword;
        }

        public void SetSearchKeyword(string keyword)
        {
            SetKeyword(keyword);
        }

        /// <summary>
        /// If the user pressed DownArrow while focused in the search field, the TreeView
        /// will call this to consume the request and perform focus/selection change.
        /// Returns true once and clears the pending request.
        /// </summary>
        public bool ConsumeFocusRequest()
        {
            if (!requestFocusTreeView) return false;
            requestFocusTreeView = false;
            return true;
        }

        public bool IsFocused()
        {
            return GUI.GetNameOfFocusedControl() == searchBarControlName;
        }

        /// <summary>
        /// Requests focus on the search bar. The focus will be applied during the next Draw call.
        /// This is the proper way to focus a control in Unity Editor GUI.
        /// </summary>
        public void RequestFocus()
        {
            requestFocusSearchBar = true;
        }

        /// <summary>
        /// Checks if a popup-close shortcut was triggered while the search bar was focused
        /// (Ctrl+Space / Option+Space, or Option-held exit on Mac).
        /// Returns true once and clears the pending request.
        /// </summary>
        public bool ConsumeClosePopupRequest()
        {
            if (!requestClosePopup) return false;
            requestClosePopup = false;
            return true;
        }
    }
}
