using System;
using UnityEditor;
using UnityEngine;

namespace SecretZauce.SecondBrain.Editor
{
    /// <summary>
    /// Responsible for drawing the TreeView header bar and managing header-level
    /// rename state (target name inline rename).
    /// Extracted from TreeView to keep the main class focused on tree-node rendering.
    /// </summary>
    internal class TreeViewHeader
    {
        readonly TreeView treeView;

        public TreeViewHeader(TreeView owner)
        {
            treeView = owner;
        }

        // ── Public rename state ────────────────────────────────────────────────
        /// <summary>True while the header (target-name) rename field is open.</summary>
        public bool IsRenamingHeader => isRenamingHeader;

        // ── Header (target) rename state ──────────────────────────────────────
        bool isRenamingHeader;
        string headerRenameText;
        bool focusHeaderRenameField;
        int headerRenameSessionId;

        // Cached control name — recomputed only when the session ID changes.
        string _cachedHeaderRenameControlName;
        int    _cachedHeaderRenameSessionId = -1;
        string HeaderRenameControlName
        {
            get
            {
                if (_cachedHeaderRenameSessionId != headerRenameSessionId)
                {
                    _cachedHeaderRenameSessionId  = headerRenameSessionId;
                    _cachedHeaderRenameControlName = $"TreeViewHeaderRename_{headerRenameSessionId}";
                }
                return _cachedHeaderRenameControlName;
            }
        }

        // ── Static style / icon cache (Option-A: invalidate on skin change) ───
        static bool     s_HeaderStylesValid;
        static bool     s_HeaderCacheProSkin;
        // Icons (never change across skin — but we rebuild on skin change to be safe)
        static Texture  s_HomeIcon;
        static Texture  s_PencilIcon;
        static Texture  s_HamburgerIcon;
        static Texture  s_BreadcrumbIcon;
        static Texture  s_DefaultBaseOnIcon;
        static Texture  s_CreateIcon;
        static Texture  s_ExpandIcon;
        static Texture  s_CollapseIcon;
        // Styles
        static GUIStyle s_BoldLabelStyle;
        static GUIStyle s_PencilStyle;
        static GUIStyle s_StarStyle;
        static GUIStyle s_HamStyle;
        static GUIStyle s_HeaderPlusStyle;
        static GUIStyle s_ToggleStyle;

        static void EnsureHeaderStyles()
        {
            bool ps = EditorGUIUtility.isProSkin;
            if (s_HeaderStylesValid && s_HeaderCacheProSkin == ps) return;
            s_HeaderCacheProSkin = ps;

            s_HomeIcon          = IconUtils.Load("home");
            s_DefaultBaseOnIcon = IconUtils.Load("default_base_on");
            s_PencilIcon = IconUtils.Load("rename");
            s_HamburgerIcon  = IconUtils.Load("properties");
            s_BreadcrumbIcon = IconUtils.Load("enter");
            s_CreateIcon  = IconUtils.Load("create");
            s_ExpandIcon  = IconUtils.Load("expand");
            s_CollapseIcon = IconUtils.Load("collapse");

            s_BoldLabelStyle = new GUIStyle(EditorStyles.boldLabel);

            s_PencilStyle = new GUIStyle(GUIStyle.none)
            {
                alignment = TextAnchor.MiddleCenter,
                padding   = new RectOffset(1, 1, 1, 1),
            };

            s_StarStyle = new GUIStyle(GUIStyle.none)
            {
                alignment = TextAnchor.MiddleCenter,
                padding   = new RectOffset(1, 1, 1, 1),
            };

            s_HamStyle = new GUIStyle(GUIStyle.none)
            {
                alignment = TextAnchor.MiddleCenter,
                padding   = new RectOffset(1, 1, 1, 1),
            };

            s_HeaderPlusStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize  = 14,
                fontStyle = FontStyle.Bold,
            };
            s_HeaderPlusStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f, 0.9f);

            s_ToggleStyle = new GUIStyle(GUIStyle.none)
            {
                alignment = TextAnchor.MiddleCenter,
                padding   = new RectOffset(1, 1, 1, 1),
            };

            s_HeaderStylesValid = true;
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Cancels the header rename without saving — call this for Escape or navigation away.
        /// </summary>
        public void CancelHeaderRename()
        {
            isRenamingHeader = false;
            headerRenameText = null;
            focusHeaderRenameField = false;
        }

        /// <summary>
        /// Draws the header bar (home button, breadcrumb label, pencil rename, hamburger,
        /// default-star, collapse/expand foldout and + button).
        /// Must be called inside an active GUILayout pass.
        /// </summary>
        public void Draw(BrowserWindow window)
        {
            var dragDropManager = treeView.DragDropManager;
            var renamer = treeView.Renamer;
            var root = treeView.Root;

            bool interactive = !dragDropManager.IsDragging && !renamer.IsRenamingAny;

            // If header renaming started, block other interactions
            if (isRenamingHeader)
                interactive = false;

            // Block header + button while a ghost creation session is active
            if (treeView.HasGhostSession)
                interactive = false;

            // Use a single layout rect for the header to avoid multiple GUILayout entries that can
            // get out of sync between Layout and Repaint.
            Rect headerRect = EditorGUILayout.GetControlRect(false, 20);

            // Draw everything in an absolute group to avoid GUILayout mismatches between Layout and Repaint
            GUI.BeginGroup(headerRect);
            try
            {
                DrawHeaderContents(headerRect, window, root, dragDropManager, renamer, interactive);
            }
            finally
            {
                GUI.EndGroup();
            }
        }

        // ── Private drawing helpers ────────────────────────────────────────────

        void DrawHeaderContents(Rect headerRect, BrowserWindow window, IStructure root,
            DragAndDropManager dragDropManager, ItemRenamer renamer, bool interactive)
        {
            EnsureHeaderStyles();

            // Inset header content by the same width as the tree row peek zones so buttons
            // and labels align visually with the tree view content area.
            float peekInset = TreeViewDragInput.QuickPeekZoneWidth;

            // Local coordinates inside the group
            float iconSize = 14f;
            Rect iconRect = new Rect(2, ((headerRect.height - iconSize) / 2) - 2, iconSize, iconSize);

            bool showingBaseTarget = root is Base;
            const float pencilWidth = 18f;

            // Reserve right-side space
            float hamburgerWidth = 18f;
            float rightReserved = showingBaseTarget ? 180f : 110f;
            rightReserved += hamburgerWidth + 4f;
            float labelWidth = Mathf.Max(150, headerRect.width - rightReserved - iconSize - 6 - peekInset * 2f);
            Rect labelRect = new Rect(iconRect.xMax + 6, 0, labelWidth, headerRect.height);

            // Pre-calc right-side positions (inset from right by peekInset to match tree rows)
            float plusWidth = 18f;
            float toggleWidth = 18f;
            Rect plusRect   = new Rect(headerRect.width - plusWidth - peekInset, 1, plusWidth, headerRect.height - 2);
            Rect toggleRect = new Rect(plusRect.x - toggleWidth, 2, toggleWidth, headerRect.height - 4);

            // Home button (cached icon)
            bool homeInteractive = !(window?.IsAtHome() ?? true) && !dragDropManager.IsDragging && !renamer.IsRenamingAny;
            EditorGUI.BeginDisabledGroup(!homeInteractive);
            if (GUI.Button(iconRect, new GUIContent(s_HomeIcon, "Go to Home"), GUIStyle.none))
            {
                CancelHeaderRename();
                window?.Controller.SetTargetWithUndo(null);
                Event.current.Use();
            }
            EditorGUI.EndDisabledGroup();

            if (showingBaseTarget)
            {
                DrawBreadcrumbArrow(labelRect, out labelRect);
                DrawTargetLabel(labelRect, window, root, pencilWidth, toggleRect, dragDropManager, renamer);
            }

            // Base settings (hamburger + default star) — only when header shows a Base
            if (showingBaseTarget)
            {
                DrawBaseControls(headerRect, toggleRect, hamburgerWidth, root, dragDropManager, renamer, interactive);
            }

            // Collapse/expand foldout and + button
            DrawFoldoutAndPlus(toggleRect, plusRect, window, root, showingBaseTarget, interactive);
        }

        void DrawBreadcrumbArrow(Rect labelRect, out Rect adjustedLabelRect)
        {
            var nextRect = new Rect(labelRect.x - 4f, 2, 15f, 20f);
            // Use cached breadcrumb icon content
            GUI.Label(nextRect, new GUIContent(s_BreadcrumbIcon));
            adjustedLabelRect = labelRect;
            adjustedLabelRect.x     += 14f;
            adjustedLabelRect.width -= 14f;
        }

        void DrawTargetLabel(Rect labelRect, BrowserWindow window, IStructure root,
            float pencilWidth, Rect toggleRect, DragAndDropManager dragDropManager, ItemRenamer renamer)
        {
            var rootObj  = root as UnityEngine.Object;
            string rootName = rootObj?.name ?? string.Empty;

            GUIContent headerLabelContent;
            if (root is IHasEmoji hasEmoji && !string.IsNullOrEmpty(hasEmoji.EmojiIcon))
                headerLabelContent = EmojiIconUtils.BuildLabelContent(rootName, hasEmoji.EmojiIcon);
            else
                headerLabelContent = new GUIContent(rootName);

            if (isRenamingHeader)
            {
                DrawHeaderRenameField(labelRect, window);
            }
            else
            {
                // Reuse cached bold label style (no alloc)
                float nameWidth  = s_BoldLabelStyle.CalcSize(headerLabelContent).x;
                float textPadding = 4f;
                Rect textRect = new Rect(labelRect.x, labelRect.y,
                    Mathf.Min(nameWidth + textPadding, labelRect.width), labelRect.height);
                EditorGUI.LabelField(textRect, headerLabelContent, s_BoldLabelStyle);

                float desiredPencilX  = textRect.xMax + 4f;
                float rightLimitForPencil = toggleRect.x - 8f;
                if (desiredPencilX + pencilWidth > rightLimitForPencil)
                    desiredPencilX = Math.Max(textRect.xMax + 2f, rightLimitForPencil - pencilWidth - 4f);

                Rect pencilRect = new Rect(desiredPencilX, 2, pencilWidth, labelRect.height - 4);

                bool pencilInteractive = !dragDropManager.IsDragging && !renamer.IsRenamingAny;
                bool pencilHover       = pencilRect.Contains(Event.current.mousePosition);

                EditorGUI.BeginDisabledGroup(!pencilInteractive);
                if (pencilHover && pencilInteractive)
                    EditorGUI.DrawRect(pencilRect, new Color(0.3f, 0.3f, 0.3f, 0.5f));

                GUIContent pencilContent = s_PencilIcon != null
                    ? new GUIContent(s_PencilIcon, "Rename")
                    : new GUIContent("✎", "Rename");

                if (GUI.Button(pencilRect, pencilContent, s_PencilStyle))
                {
                    StartHeaderRename();
                    Event.current.Use();
                }
                EditorGUI.EndDisabledGroup();
            }
        }

        void DrawBaseControls(Rect headerRect, Rect toggleRect, float hamburgerWidth,
            IStructure root, DragAndDropManager dragDropManager, ItemRenamer renamer, bool interactive)
        {
            var baseObj = root as Base;
            Rect hamburgerRect = new Rect(toggleRect.x - hamburgerWidth - 4f, 2, hamburgerWidth, headerRect.height - 4);

            if (baseObj == null) return;

#if SECOND_BRAIN_PRO
            float defaultSize = 16f;
            Rect defaultRect = new Rect(hamburgerRect.x - 4 - defaultSize, 0, defaultSize, headerRect.height);

            var  mother    = Motherbase.Home;
            bool isDefault = mother != null && mother.DefaultBase == baseObj;

            bool  defaultHover = defaultRect.Contains(Event.current.mousePosition);
            Color iconColor    = isDefault
                ? new Color(0.14f, 0.45f, 0.90f, 1f)
                : (defaultHover ? new Color(1f, 1f, 1f, 0.9f) : new Color(0.65f, 0.65f, 0.65f, 0.7f));

            GUIContent defContent = s_DefaultBaseOnIcon != null
                ? new GUIContent(s_DefaultBaseOnIcon, isDefault ? "Default Base" : "Set as Default Base")
                : new GUIContent(isDefault ? "★" : "☆", isDefault ? "Default Base" : "Set as Default Base");

            var prevColor = GUI.color;
            GUI.color = iconColor;
            if (GUI.Button(defaultRect, defContent, s_StarStyle))
            {
                try
                {
                    if (mother != null)
                    {
                        Undo.RecordObject(mother, isDefault ? "Clear Default Base" : "Set Default Base");
                        mother.DefaultBase = isDefault ? null : baseObj;
                        EditorUtility.SetDirty(mother);
                        AssetDatabase.SaveAssets();
                        try { treeView.OwnerWindow?.ShowNotification(new GUIContent(isDefault ? "Cleared Default Base" : "Set Default Base")); } catch { }
                    }
                }
                catch { }
                EditorApplication.delayCall += () => treeView.OwnerWindow?.Repaint();
                Event.current.Use();
            }
            GUI.color = prevColor;
#endif

            // Hamburger button — reuse cached style
            bool hamInteractive = !dragDropManager.IsDragging && !renamer.IsRenamingAny && interactive;
            EditorGUI.BeginDisabledGroup(!hamInteractive);
            bool hamHover = hamburgerRect.Contains(Event.current.mousePosition);
            if (hamHover && hamInteractive) EditorGUI.DrawRect(hamburgerRect, new Color(0.3f, 0.3f, 0.3f, 0.5f));
            if (GUI.Button(hamburgerRect, new GUIContent(s_HamburgerIcon, "Base Settings"), s_HamStyle))
            {
                try { EditorUtility.OpenPropertyEditor(baseObj); } catch { }
                Event.current.Use();
            }
            EditorGUI.EndDisabledGroup();
        }

        void DrawFoldoutAndPlus(Rect toggleRect, Rect plusRect, BrowserWindow window,
            IStructure root, bool showingBaseTarget, bool interactive)
        {
            bool mostAreExpanded = treeView.AreMostCollectionsExpanded();
            EditorGUI.BeginDisabledGroup(!interactive);
            Texture foldIcon = mostAreExpanded ? s_CollapseIcon : s_ExpandIcon;
            string  foldTip  = mostAreExpanded ? "Collapse All" : "Expand All";
            if (foldIcon != null)
            {
                if (GUI.Button(toggleRect, new GUIContent(foldIcon, foldTip), s_ToggleStyle))
                {
                    if (mostAreExpanded) treeView.CollapseAll(); else treeView.ExpandAll();
                }
            }
            else
            {
                bool newMostAreExpanded = EditorGUI.Foldout(toggleRect, mostAreExpanded,
                    new GUIContent(string.Empty), true, EditorStyles.foldout);
                if (newMostAreExpanded != mostAreExpanded)
                {
                    if (newMostAreExpanded) treeView.ExpandAll(); else treeView.CollapseAll();
                }
            }

            // Reuse cached plus style; update hover color in-place.
            bool isHovering = plusRect.Contains(Event.current.mousePosition);
            s_HeaderPlusStyle.normal.textColor = isHovering
                ? new Color(1f, 1f, 1f, 1f)
                : new Color(0.6f, 0.6f, 0.6f, 0.9f);
            if (isHovering && interactive) EditorGUI.DrawRect(plusRect, new Color(0.3f, 0.3f, 0.3f, 0.5f));

            string plusTooltip = showingBaseTarget ? "Create New Container" : "Create New Base";
            GUIContent plusContent = s_CreateIcon != null
                ? new GUIContent(s_CreateIcon, plusTooltip)
                : new GUIContent("+", plusTooltip);
            if (GUI.Button(plusRect, plusContent, s_HeaderPlusStyle))
            {
#if !SECOND_BRAIN_PRO
                // In free mode, block creating a second Base and show a PRO upgrade notice.
                if (!showingBaseTarget && Motherbase.Home.Children.Count >= 1)
                {
                    ProFeatureDialog.Show("Multiple Bases");
                    Event.current?.Use();
                    return;
                }
#endif
                if (root is UnityEngine.Object rootObj)
                {
                    if (rootObj is IHasCreateChildOption)
                    {
                        CreateChildMenuUtils.ShowCreateChildMenu(rootObj, window, plusRect);
                        Event.current?.Use();
                    }
                    else
                    {
                        window?.CreateChild(rootObj);
                        Event.current?.Use();
                    }
                }
            }
            EditorGUI.EndDisabledGroup();
        }

        // ── Rename helpers ─────────────────────────────────────────────────────

        void StartHeaderRename()
        {
            if (treeView.Root is not UnityEngine.Object rootObj)
                return;

            headerRenameSessionId++;
            isRenamingHeader = true;
            headerRenameText = rootObj.name;
            focusHeaderRenameField = true;
            treeView.DragDropManager?.CancelPotentialDrag();
            treeView.CancelDrag();
        }

        void CommitHeaderRename(BrowserWindow window)
        {
            var rootObj = treeView.Root as UnityEngine.Object;

            if (rootObj == null || string.IsNullOrWhiteSpace(headerRenameText))
            {
                CancelHeaderRename();
                EditorGUIUtility.editingTextField = false;
                GUI.FocusControl(null);
                GUIUtility.keyboardControl = 0;
                return;
            }

            bool success = RenameUtils.RenameObject(rootObj, headerRenameText);
            CancelHeaderRename();

            if (success)
                window?.OnRenameCompleted();

            EditorGUIUtility.editingTextField = false;
            GUI.FocusControl(null);
            GUIUtility.keyboardControl = 0;
            EditorApplication.delayCall += () => EditorWindow.focusedWindow?.Repaint();
        }

        /// <summary>
        /// Draws the inline rename text field for the header target name.
        /// </summary>
        void DrawHeaderRenameField(Rect fieldRect, BrowserWindow window)
        {
            // Fallback Escape check (primary intercept runs at the top of TreeView.Draw)
            if (Event.current != null && Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                CancelHeaderRename();
                EditorGUIUtility.editingTextField = false;
                GUI.FocusControl(null);
                GUIUtility.keyboardControl = 0;
                Event.current.Use();
                EditorApplication.delayCall += () => EditorWindow.focusedWindow?.Repaint();
                return;
            }

            bool enterPressed = Event.current != null && Event.current.type == EventType.KeyDown &&
                                (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);

            // Selection highlight behind the field
            var headerSelRect = fieldRect;
            headerSelRect.height += 1f;
            EditorGUI.DrawRect(headerSelRect, new Color(0.24f, 0.48f, 0.90f, 0.25f));

            if (focusHeaderRenameField)
            {
                EditorGUI.FocusTextInControl(HeaderRenameControlName);
                focusHeaderRenameField = false;
            }

            GUI.SetNextControlName(HeaderRenameControlName);
            string newText = EditorGUI.TextField(fieldRect, headerRenameText);
            headerRenameText = newText;

            if (enterPressed)
            {
                Event.current?.Use();
                CommitHeaderRename(window);
                return;
            }

            // Commit when the user clicks outside the field
            if (Event.current != null && Event.current.type == EventType.MouseDown &&
                !fieldRect.Contains(Event.current.mousePosition))
            {
                CommitHeaderRename(window);
                Event.current.Use();
            }
        }
    }
}

