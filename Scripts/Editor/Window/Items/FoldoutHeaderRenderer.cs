using UnityEditor;
using UnityEngine;

namespace SecretZauce.SecondBrain.Editor
{
    internal class FoldoutHeaderRenderer : RowRendererBase
    {
        // ── Static style cache (Option-A invalidation) ─────────────────────────
        // Rebuilt whenever the skin (pro/personal) or the configured font size changes.
        static bool   s_StylesValid;
        static bool   s_CacheProSkin;
        static int    s_CacheFontSize;
        static GUIStyle s_FoldStyle;      // bold foldout base
        static GUIStyle s_HiddenFoldLabelStyle; // label used when foldout arrow is hidden
        static GUIStyle s_PlusStyle;      // + button base (textColor set per-call)
        static GUIStyle s_CountStyle;     // child-count badge base (fontStyle/textColor set per-call)
        static Texture  s_CreateIcon;

        static void EnsureStyles()
        {
            bool ps = EditorGUIUtility.isProSkin;
            int  fs = BrowserSettings.GetItemFontSize();
            if (s_StylesValid && s_CacheProSkin == ps && s_CacheFontSize == fs)
                return;

            s_CacheProSkin  = ps;
            s_CacheFontSize = fs;

            s_FoldStyle = new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold };
            if (fs > 0) s_FoldStyle.fontSize = fs;

            s_HiddenFoldLabelStyle = new GUIStyle(EditorStyles.label) { fontStyle = FontStyle.Bold };
            if (fs > 0) s_HiddenFoldLabelStyle.fontSize = fs;

            s_PlusStyle = new GUIStyle(GUI.skin.label)
            {
                alignment  = TextAnchor.MiddleCenter,
                fontSize   = 15,
                fontStyle  = FontStyle.Bold,
            };
            s_PlusStyle.hover.textColor = Color.white;

            s_CountStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize  = 10,
            };

            s_CreateIcon = IconUtils.Load("create");

            s_StylesValid = true;
        }
        public FoldoutHeaderRenderer(TreeView tv) : base(tv) { }

        /// <summary>
        /// Points this renderer at the next row. A single instance is reused for every row
        /// (see ItemRenderer) instead of allocating one per row per IMGUI pass.
        /// </summary>
        public void Reset(int[] rowPath, Object nodeObj, bool selected, Texture iconTexture)
        {
            path = rowPath;
            node = nodeObj;
            name = nodeObj != null ? nodeObj.name : "<unnamed>";
            icon = iconTexture;
            isSelected = selected;
            // Derived rects and flags are recomputed by Render; clear them so a stale value
            // from the previous row can never be observed.
            rowRect = default;
            arrowRect = default;
            buttonRect = default;
            foldoutHeaderRect = default;
            trueIndentedItemRect = default;
            showAddButton = false;
            rightReservedSpace = 0f;
            foldoutArrowWidth = 0f;
        }

        // A single shared control name for all foldout rows. Uniqueness per row is not required
        // because focus is set immediately after click and IMGUI processes one event at a time.
        const string k_FoldoutControlName = "SB_Foldout";

        public Rect Render(ref bool foldout, bool hideFoldoutArrow = false, Color? labelColor = null, ColorDisplayStyle colorStyle = ColorDisplayStyle.FontColor)
        {
            // Rebuild style cache if skin or font size changed (Option-A invalidation).
            EnsureStyles();

            GUIStyle foldStyle = s_FoldStyle;
            float rowHeight = BrowserSettings.GetItemRowHeight();
            rowRect = EditorGUILayout.GetControlRect(false, rowHeight, GUILayout.ExpandWidth(true));
            // Reserve space for the foldout arrow so labels remain aligned with other rows.
            // Even when the arrow is hidden we keep the same horizontal spacing so the
            // container and the object below align with other folder headers.
            foldoutArrowWidth = 12f;

            bool isRenaming = treeView != null && treeView.Renamer.IsRenaming(this.path);

            // Background style: draw a tinted background BEFORE selection/hover so they remain visible on top
            // Do not draw the background tint if the row is selected — selected state should take visual precedence.
            // Only draw the thin separator lines when this node is the one that actually has a color
            // assigned to it (avoid drawing separators for rows that merely inherit color from an ancestor).
            if (labelColor.HasValue && (colorStyle == ColorDisplayStyle.Background || colorStyle == ColorDisplayStyle.Gradient) && !isSelected)
            {
                var bgRect = rowRect;
                bgRect.height += 1;
                // Background: solid dimmed fill. Gradient: fade from color to transparent on the right.
                if (colorStyle == ColorDisplayStyle.Background)
                {
                    Color dimmed = new Color(labelColor.Value.r, labelColor.Value.g, labelColor.Value.b, 0.3f);
                    EditorGUI.DrawRect(bgRect, dimmed);
                }
                else // Gradient
                {
                    // Use a slightly stronger alpha at the left so the fade is visible
                    Color start = new Color(labelColor.Value.r, labelColor.Value.g, labelColor.Value.b, 0.45f);
                    ItemUtils.DrawHorizontalFade(bgRect, start);
                }

                if (node is IHasColor hasOwnColor && hasOwnColor.HasLabelColor)
                {
                    // Draw thin separator lines at the top and bottom using the original (undimmed) color
                    // to improve visual separation between rows.
                    Color separator = new Color(labelColor.Value.r, labelColor.Value.g, labelColor.Value.b, 1f);
                    float lineHeight = 1f;
                    var topLine = new Rect(bgRect.x, bgRect.y, bgRect.width, lineHeight);
                    var bottomLine = new Rect(bgRect.x, bgRect.yMax - lineHeight, bgRect.width, lineHeight);
                    EditorGUI.DrawRect(topLine, separator);
                    EditorGUI.DrawRect(bottomLine, separator);
                }
            }

            // Draw hover highlight and selection background (mirrors the behaviour of leaf rows).
            ItemUtils.DrawHoverAndSelection(rowRect, isRenaming, isSelected, treeView != null && treeView.DragDropManager.IsDragging, GetRowHoverColor());

            if (treeView != null)
            {
                showAddButton = !treeView.DragDropManager.IsDragging && !isRenaming && !treeView.HasGhostSession &&
                                this.node is IStructure && this.isSelected && treeView.Context.SelectedPaths.Count == 1;

                int childCount = 0;
                if (this.node is IStructure {ChildrenObjects: not null} structure)
                    childCount = structure.ChildrenObjects.Count;

                float buttonWidth = 16f;
                float buttonPadding = 1f;
                float rightPeekOffset = 0f;
                if (ProFeature.Provider != null && BrowserSettings.EnableQuickPeek)
                    rightPeekOffset = TreeViewDragInput.QuickPeekZoneWidth;
                rightReservedSpace = showAddButton ? buttonWidth + buttonPadding + rightPeekOffset : 0f;

                buttonRect = new Rect(rowRect.xMax - buttonWidth - buttonPadding - rightPeekOffset,
                    rowRect.y + (rowRect.height - buttonWidth) * 0.5f,
                    buttonWidth, buttonWidth);
                foldoutHeaderRect = new Rect(rowRect.x + 1, rowRect.y, rowRect.width - rightReservedSpace,
                    rowRect.height);
                trueIndentedItemRect = EditorGUI.IndentedRect(foldoutHeaderRect);
                treeView.DragInput.HandleRow(rowRect, this.path, this.node is IStructure, trueIndentedItemRect.x,
                    foldout);

                if (isRenaming)
                {
                    // assign to instance arrowRect to avoid shadowing the protected field
                    // When hiding the foldout arrow, do not draw a toggle. Keep foldout value as-is
                    if (hideFoldoutArrow)
                    {
                        this.arrowRect = new Rect(foldoutHeaderRect.x, foldoutHeaderRect.y, 0f,
                            foldoutHeaderRect.height);
                    }
                    else
                    {
                        this.arrowRect = new Rect(foldoutHeaderRect.x, foldoutHeaderRect.y, foldoutArrowWidth,
                            foldoutHeaderRect.height);
                        bool prevFold = foldout;
                        foldout = EditorGUI.Foldout(this.arrowRect, foldout, "", false);
                        if (foldout != prevFold)
                        {
                            if (Event.current != null && Event.current.alt)
                                treeView.foldoutState.SetRecursive(this.node, foldout);

                            treeView.Context.RaiseRecordSelectionChange();
                        }
                    }

                    Rect textFieldRect = new Rect(
                        trueIndentedItemRect.x + foldoutArrowWidth,
                        foldoutHeaderRect.y,
                        foldoutHeaderRect.width - foldoutArrowWidth,
                        foldoutHeaderRect.height
                    );
                    // Clamp the right side so the rename field never extends beyond the row boundary
                    if (textFieldRect.xMax > rowRect.xMax)
                        textFieldRect.width = rowRect.xMax - textFieldRect.x;

                    if (treeView.Renamer.HandleRenamingField(this.path, this.node, textFieldRect, resetIndent: false))
                        return rowRect;
                }
                else
                {
                    // Only apply color to label text when using FontColor style
                    Color? fontColor = (colorStyle == ColorDisplayStyle.FontColor) ? labelColor : null;
                    GUI.SetNextControlName(k_FoldoutControlName);
                    bool prevFold = foldout;
                    bool currentFold = foldout;
                    if (!treeView.DragDropManager.IsDragging)
                    {
                        bool newFold;
                        using (EditorGUIUtils.TemporaryLabelWidth(foldoutHeaderRect, 20f))
                            newFold = DrawLabel(ref currentFold, foldStyle, fontColor, hideFoldoutArrow);
                        foldout = newFold;
                    }
                    else
                    {
                        using (EditorGUIUtils.TemporaryLabelWidth(foldoutHeaderRect, 20f))
                            DrawLabel(ref currentFold, foldStyle, fontColor, hideFoldoutArrow);
                    }

                    if (foldout != prevFold)
                    {
                        if (Event.current != null && Event.current.alt)
                            treeView.foldoutState.SetRecursive(this.node, foldout);

                        treeView.Context.RaiseRecordSelectionChange();
                        GUI.FocusControl(k_FoldoutControlName);
                        if (Event.current != null) Event.current.Use();
                    }
                }

                if (!isRenaming)
                {
                    Color? dotColor = (labelColor.HasValue && colorStyle == ColorDisplayStyle.CircleDot)
                        ? labelColor
                        : null;
                    // Pass labelColor & colorStyle so the child count badge can use a dimmer
                    // version of the assigned color when the foldout uses a Background style.
                    DrawChildCountBadge(childCount, dotColor, labelColor, colorStyle);
                    DrawAddButtonIfNeeded();
                }
            }
            if (!isRenaming && treeView != null && ProFeature.Provider != null && BrowserSettings.EnableQuickPeek)
                TreeViewDragInput.DrawPeekZoneIndicator(rowRect, skipLeftZone: true);
            return rowRect;
        }

        // Reusable GUIStyle instances for colored rows — avoids allocating a new GUIStyle per row per frame.
        // IMGUI draws sequentially so a single instance reused across rows is safe.
        static GUIStyle s_ColoredFoldStyle;
        static GUIStyle s_ColoredHiddenLabelStyle;
        static GUIStyle s_ColoredPaddedStyle;
        static Color    s_LastFoldColor;
        static Color    s_LastHiddenLabelColor;

        bool DrawLabel(ref bool currentFold, GUIStyle foldStyle, Color? labelColor, bool hideFoldoutArrow)
        {
            // Reuse a single cached colored style instead of allocating per row.
            GUIStyle workingFoldStyle;
            if (labelColor.HasValue)
            {
                if (s_ColoredFoldStyle == null || s_ColoredFoldStyle.font != foldStyle.font || s_ColoredFoldStyle.fontSize != foldStyle.fontSize)
                    s_ColoredFoldStyle = new GUIStyle(foldStyle);
                if (s_LastFoldColor != labelColor.Value)
                {
                    s_LastFoldColor = labelColor.Value;
                    ApplyColorToAllStates(s_ColoredFoldStyle, labelColor.Value);
                }
                workingFoldStyle = s_ColoredFoldStyle;
            }
            else
            {
                workingFoldStyle = foldStyle;
            }

            if (hideFoldoutArrow)
            {
                // Reuse a single cached colored label style instead of allocating per row.
                GUIStyle labelStyle;
                if (labelColor.HasValue)
                {
                    if (s_ColoredHiddenLabelStyle == null || s_ColoredHiddenLabelStyle.font != s_HiddenFoldLabelStyle.font || s_ColoredHiddenLabelStyle.fontSize != s_HiddenFoldLabelStyle.fontSize)
                        s_ColoredHiddenLabelStyle = new GUIStyle(s_HiddenFoldLabelStyle);
                    if (s_LastHiddenLabelColor != labelColor.Value)
                    {
                        s_LastHiddenLabelColor = labelColor.Value;
                        ApplyColorToAllStates(s_ColoredHiddenLabelStyle, labelColor.Value);
                    }
                    labelStyle = s_ColoredHiddenLabelStyle;
                }
                else
                {
                    labelStyle = s_HiddenFoldLabelStyle;
                }

                var labelRect = new Rect(trueIndentedItemRect.x + foldoutArrowWidth, foldoutHeaderRect.y, foldoutHeaderRect.width - foldoutArrowWidth, foldoutHeaderRect.height);

                if (node is IHasEmoji hasEmoji2 && !string.IsNullOrEmpty(hasEmoji2.EmojiIcon) && BrowserSettings.ShowIconsPerType)
                {
                    if (EmojiIconUtils.IsEditorIcon(hasEmoji2.EmojiIcon))
                    {
                        Rect computedTextRect;
                        if (!EmojiIconUtils.TryDrawEditorIconLabel(labelRect, hasEmoji2.EmojiIcon, labelStyle, name, out computedTextRect))
                        {
                            EditorGUI.LabelField(labelRect, new GUIContent(name, BrowserSettings.ShowIconsPerType ? icon : null), labelStyle);
                        }
                    }
                    else
                    {
                        EditorGUI.LabelField(labelRect, EmojiIconUtils.BuildLabelContent(name, hasEmoji2.EmojiIcon), labelStyle);
                    }
                }
                else
                {
                    EditorGUI.LabelField(labelRect, new GUIContent(name, BrowserSettings.ShowIconsPerType ? icon : null), labelStyle);
                }

                return currentFold;
            }

                if (node is IHasEmoji hasEmoji && !string.IsNullOrEmpty(hasEmoji.EmojiIcon) && BrowserSettings.ShowIconsPerType)
                {
                    if (EmojiIconUtils.IsEditorIcon(hasEmoji.EmojiIcon))
                    {
                        // Draw only the icon and compute its width so we can add left padding to the Foldout style
                        Rect iconRect;
                        float iconWidth;
                        var iconLabelRect = new Rect(trueIndentedItemRect.x + foldoutArrowWidth, foldoutHeaderRect.y, foldoutHeaderRect.width - foldoutArrowWidth, foldoutHeaderRect.height);
                        if (EmojiIconUtils.TryDrawEditorIcon(iconLabelRect, hasEmoji.EmojiIcon, workingFoldStyle, out iconRect, out iconWidth))
                        {
                            // Reuse a single cached padded style to avoid per-row allocation.
                            if (s_ColoredPaddedStyle == null || s_ColoredPaddedStyle.font != workingFoldStyle.font || s_ColoredPaddedStyle.fontSize != workingFoldStyle.fontSize)
                                s_ColoredPaddedStyle = new GUIStyle(workingFoldStyle);
                            else
                            {
                                s_ColoredPaddedStyle.normal.textColor = workingFoldStyle.normal.textColor;
                                s_ColoredPaddedStyle.onNormal.textColor = workingFoldStyle.onNormal.textColor;
                                s_ColoredPaddedStyle.focused.textColor = workingFoldStyle.focused.textColor;
                                s_ColoredPaddedStyle.onFocused.textColor = workingFoldStyle.onFocused.textColor;
                            }
                            GUIStyle paddedStyle = s_ColoredPaddedStyle;
                            int pad = Mathf.CeilToInt(iconWidth + 4f);
                            if (paddedStyle.padding == null) paddedStyle.padding = new RectOffset();
                            paddedStyle.padding.left = foldStyle.padding.left + pad;

                            return EditorGUI.Foldout(foldoutHeaderRect, currentFold, new GUIContent(name), false, paddedStyle);
                        }
                    }
                    else
                    {
                        return EditorGUI.Foldout(foldoutHeaderRect, currentFold,
                            new GUIContent(EmojiSupport.Prefix(hasEmoji.EmojiIcon, name),
                                EmojiSupport.IsSupported ? null : icon), false, workingFoldStyle);
                    }
                }

            return EditorGUI.Foldout(foldoutHeaderRect, currentFold,
                new GUIContent(name, BrowserSettings.ShowIconsPerType ? icon : null), false, workingFoldStyle);
        }

        static void ApplyColorToAllStates(GUIStyle style, Color color)
        {
            style.normal.textColor = color;
            style.onNormal.textColor = color;
            style.focused.textColor = color;
            style.onFocused.textColor = color;
        }

        void DrawChildCountBadge(int childCount, Color? dotColor = null, Color? labelColor = null, ColorDisplayStyle colorStyle = ColorDisplayStyle.FontColor)
        {
            float gapFromPlus = 4f;
            float dotGap = 4f;

            // Compute where the count badge sits (we need it even when childCount == 0 to position the dot)
            Rect badgeRect = Rect.zero;

            if (childCount > 0)
            {
                string countText = childCount.ToString();

                // Re-use the cached style; set per-call properties without allocating a new GUIStyle.
                var isProSkin = EditorGUIUtility.isProSkin;
                s_CountStyle.fontStyle = isSelected ? FontStyle.Bold : FontStyle.Normal;
                s_CountStyle.normal.textColor = isSelected
                    ? (isProSkin ? Color.white : Color.black)
                    : (labelColor.HasValue && colorStyle == ColorDisplayStyle.Background
                        ? new Color(labelColor.Value.r, labelColor.Value.g, labelColor.Value.b, 1f)
                        : (isProSkin ? new Color(0.6f, 0.6f, 0.6f, 1f) : new Color(0.3f, 0.3f, 0.3f, 1f)));

                Vector2 textSize = s_CountStyle.CalcSize(new GUIContent(countText));
                float paddingH = 8f;
                var badgeWidth = Mathf.Clamp(textSize.x + paddingH, 16f, 80f);
                var badgeHeight = Mathf.Max(18f, textSize.y + 4f);
                badgeRect = new Rect(buttonRect.x - badgeWidth - gapFromPlus, buttonRect.y + (buttonRect.height - badgeHeight) / 2f, badgeWidth, badgeHeight);
                GUI.Label(badgeRect, countText, s_CountStyle);
            }

            // Draw the CircleDot to the left of the count badge (or to the left of the button if no badge)
            if (dotColor.HasValue)
            {
                float dotRadius = 5f;
                float dotX;
                if (childCount > 0)
                    dotX = badgeRect.x - dotRadius * 2f - dotGap;
                else
                    dotX = buttonRect.x - dotRadius * 2f - gapFromPlus;
                float dotCx = dotX + dotRadius;
                float dotCy = buttonRect.y + buttonRect.height / 2f;
                DrawColorDot(dotCx, dotCy, dotRadius, dotColor.Value);
            }
        }

        void DrawAddButtonIfNeeded()
        {
            if (!showAddButton) return;
            bool isHovering = Event.current != null && buttonRect.Contains(Event.current.mousePosition);
            if (isHovering)
            {
                EditorGUI.DrawRect(buttonRect, new Color(0.3f, 0.3f, 0.3f, 0.5f));
            }

            // Re-use the cached style; only the text color differs between hover states.
            s_PlusStyle.normal.textColor = isHovering ? new Color(1f, 1f, 1f, 1f) : new Color(0.6f, 0.6f, 0.6f, 0.6f);

            Rect btn = buttonRect;

            // Add tooltip to the plus button so users get affordance on hover.
            GUIContent plusContent = s_CreateIcon != null
                ? new GUIContent(s_CreateIcon, "Create child group")
                : new GUIContent("+", "Create child group");
            if (GUI.Button(btn, plusContent, s_PlusStyle))
            {
                // Use the shared CreateChildMenuUtils to show the menu or call CreateChild
                CreateChildMenuUtils.ShowCreateChildMenu(node, treeView.OwnerWindow, btn);
                Event.current?.Use();
            }
        }
    }
}