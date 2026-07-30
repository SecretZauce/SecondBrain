using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SecretZauce.SecondBrain.Editor
{
    /// <summary>
    /// Floating color picker popup that lets the user assign a label color to one or more
    /// Container objects implementing <see cref="IHasColor"/>.
    /// </summary>
    public partial class ColorTray : PickerTrayBase
    {
        // Palette: each entry is (display name, color).
        // A null color entry is used for the "Clear" option.

        private const int SwatchSize = 28;
        private const int SwatchPadding = 4;
        private const int Columns = 6;
        private const int TooltipWidth = 60;
        private const int TooltipHeight = 16;

        private List<IHasColor> _targets;
        
        // Custom colors persisted in EditorPrefs and shown alongside the palette.
        private readonly List<Color> _customColors = new List<Color>();
        
        private Color _newCustomColor = Color.white;
        private const string CustomColorsKey = "SecondBrain.ColorTray.CustomColors_v1";
        private const int MaxCustomColors = 12;
        
        // Persisted default palette (modifiable after first run). Stored in EditorPrefs so users
        // can remove default entries. Format stored by SaveDefaultPaletteToPrefs/Load...
        readonly List<(string label, Color? color)> defaultPalette = new List<(string, Color?)>();
        
        private const string DefaultPaletteKey = "SecondBrain.ColorTray.DefaultPalette_v1";
        
        // Selected settings mirrored from targets (may be mixed)
        private ColorDisplayStyle _selectedStyle = ColorDisplayStyle.FontColor;
        private bool _selectedFoldoutOnly = false;
        private bool _styleMixed = false;
        private bool _foldoutMixed = false;

        public static void ShowForObjects(List<IHasColor> targets, Vector2? screenPosition)
        {
            CloseCurrentTray();
            var window = CreateInstance<ColorTray>();
            _currentTray = window;
            window._targets = targets != null ? new List<IHasColor>(targets) : null;

            // Load persisted custom colors and persisted default palette. If no saved default
            // palette exists, save the static Palette as the persisted default so it can be
            // modified later by the user.
            try { window.LoadCustomColorsFromPrefs(); } catch { }
            try { window.LoadDefaultPaletteFromPrefs(); } catch { }

            // Initialize setting values and mixed-state flags from targets
            if (window._targets is {Count: > 0})
            {
                var first = window._targets[0];
                window._selectedStyle = first.LabelColorStyle;
                window._selectedFoldoutOnly = first.LabelColorFoldoutOnly;

                window._styleMixed = false;
                window._foldoutMixed = false;
                for (int i = 1; i < window._targets.Count; i++)
                {
                    var t = window._targets[i];
                    if (t.LabelColorStyle != window._selectedStyle) window._styleMixed = true;
                    if (t.LabelColorFoldoutOnly != window._selectedFoldoutOnly) window._foldoutMixed = true;
                    if (window._styleMixed && window._foldoutMixed) break;
                }
            }

            int totalSwatches = window.defaultPalette.Count + window._customColors.Count;
            int rows = Mathf.CeilToInt((float)totalSwatches / Columns);
            int width = Columns * (SwatchSize + SwatchPadding) + SwatchPadding;
          
            // Add extra vertical space to accommodate the custom picker + settings controls rendered below the palette
            int controlHeight = 18;
            int controlPadding = 6;
            int customPickerHeight = 56; // taller to accommodate in-tray HSV sliders
           
            // Compute a tighter extra controls height to avoid unused space beneath the "Foldout only" control.
            // This includes: top padding, the custom picker row, two control rows (Style + Foldout) and small bottom padding.
            int extraControlsHeight = controlPadding + customPickerHeight + (controlHeight * 2) + 8;
            int height = rows * (SwatchSize + SwatchPadding) + SwatchPadding + 4 + extraControlsHeight;
            var pos = screenPosition ?? new Vector2(Screen.width / 2, Screen.height / 2);
            window.ShowAsFloatingDialog(pos, width, height);
        }

        void OnGUI()
        {
            // Repaint on mouse move for hover highlights
            if (Event.current.type == EventType.MouseMove)
                Repaint();

            int totalSwatches = defaultPalette.Count + _customColors.Count;
            int rows = Mathf.CeilToInt((float)totalSwatches / Columns);
            float startX = SwatchPadding;
            float startY = SwatchPadding / 2f;

            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < Columns; col++)
                {
                    int idx = row * Columns + col;
                    if (idx >= totalSwatches) break;

                    string label;
                    Color? color;
                    if (idx < defaultPalette.Count)
                    {
                        var t = defaultPalette[idx];
                        label = t.label;
                        color = t.color;
                    }
                    else
                    {
                        int ci = idx - defaultPalette.Count;
                        label = $"Custom {ci + 1}";
                        color = _customColors[ci];
                    }
                    float x = startX + col * (SwatchSize + SwatchPadding);
                    float y = startY + row * (SwatchSize + SwatchPadding);
                    Rect swatchRect = new Rect(x, y, SwatchSize, SwatchSize);

                    bool isHovering = swatchRect.Contains(Event.current.mousePosition);

                    if (color.HasValue)
                    {
                        // Draw filled color swatch
                        EditorGUI.DrawRect(swatchRect, color.Value);
                    }
                    else
                    {
                        // "Clear" swatch — draw an X pattern to indicate removal
                        EditorGUI.DrawRect(swatchRect, new Color(0.2f, 0.2f, 0.2f, 1f));
                        var labelStyle = new GUIStyle(EditorStyles.miniLabel)
                        {
                            alignment = TextAnchor.MiddleCenter,
                            normal = { textColor = new Color(0.8f, 0.8f, 0.8f) },
                            fontSize = 10,
                            fontStyle = FontStyle.Bold,
                        };
                        GUI.Label(swatchRect, "✕", labelStyle);
                    }

                    // Tooltip
                    if (isHovering)
                    {
                        var tooltipStyle = new GUIStyle(EditorStyles.helpBox)
                        {
                            alignment = TextAnchor.MiddleCenter,
                            fontSize = 9,
                        };
                        Rect tooltipRect = new Rect(swatchRect.x, swatchRect.yMax + 2, TooltipWidth, TooltipHeight);
                        GUI.Label(tooltipRect, label, tooltipStyle);
                    }

                    // Click → apply but do NOT close the tray immediately
                    // Right-click on custom swatch -> show remove menu
                    if (Event.current.type == EventType.MouseDown && Event.current.button == 1 && swatchRect.Contains(Event.current.mousePosition))
                    {
                        // Right-click on custom swatch -> show remove menu
                        if (idx >= defaultPalette.Count)
                        {
                            int ci = idx - defaultPalette.Count;
                            var menu = new GenericMenu();
                            menu.AddItem(new GUIContent("Remove Custom Color"), false, () => RemoveCustomColorAt(ci));
                            menu.ShowAsContext();
                            Event.current.Use();
                            continue;
                        }

                        // Right-click on default swatch -> allow removal of default colors (but do not allow removing "Clear")
                        if (idx < defaultPalette.Count)
                        {
                            var t = defaultPalette[idx];
                            if (t.color.HasValue)
                            {
                                int di = idx;
                                var menu = new GenericMenu();
                                menu.AddItem(new GUIContent("Remove Color"), false, () => RemoveDefaultColorAt(di));
                                menu.ShowAsContext();
                                Event.current.Use();
                                continue;
                            }
                        }
                    }

                    // Left-click → apply but do NOT close the tray immediately
                    if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && swatchRect.Contains(Event.current.mousePosition))
                    {
                        ApplyColor(color, closeAfter: false);
                        Event.current.Use();
                    }
                }
            }

            float controlsY = startY + rows * (SwatchSize + SwatchPadding) + SwatchPadding;
            float customPickerHeight = 18f;
            float addBtnW = 90f;
            float resetBtnW = 96f;
            float desiredGap = 6f; 
            float availableWidth = Mathf.Max(0f, position.width - SwatchPadding * 2f);
            float minResetW = 60f;
            if (addBtnW + desiredGap + resetBtnW > availableWidth)
            {
                float newResetW = availableWidth - addBtnW - desiredGap;
                resetBtnW = Mathf.Max(minResetW, Mathf.Max(40f, newResetW));
                if (addBtnW + desiredGap + resetBtnW > availableWidth)
                    desiredGap = Mathf.Max(2f, availableWidth - addBtnW - resetBtnW);
            }

            var addColorRect = new Rect(SwatchPadding, controlsY + (customPickerHeight - 18f) / 2f, addBtnW, 18f);
            if (GUI.Button(addColorRect, "Add Color"))
            {
                // Show a small popup color picker window anchored to the Add button; on confirm it will call AddCustomColor
                var anchorScreenRect = new Rect(this.position.x + addColorRect.x, this.position.y + addColorRect.y, addColorRect.width, addColorRect.height);
                ColorPickerPopup.Show(anchorScreenRect, _newCustomColor, (c) => { _newCustomColor = c; AddCustomColor(c); });
            }

            // Reset default palette button (restores the built-in static Palette)
            var resetRect = new Rect(addColorRect.xMax + desiredGap, controlsY + (customPickerHeight - 18f) / 2f, resetBtnW, 18f);
            
            // Ensure reset button does not overflow; if it does, anchor it to the right edge.
            if (resetRect.xMax > position.width - SwatchPadding)
            {
                resetRect.x = position.width - SwatchPadding - resetRect.width;
            }
            if (GUI.Button(resetRect, "Reset Colors"))
            {
                if (EditorUtility.DisplayDialog("Restore Default Colors",
                    "Restore the built-in default color palette? This will replace your saved default palette and remove any custom colors.",
                    "Restore", "Cancel"))
                {
                    RestoreDefaultPaletteToBuiltIn();
                }
            }

            controlsY += customPickerHeight + 6f;
            float controlHeight = 18f;
            float padding = 2f;
          
            // Styles group
            // Draw a subtle boxed group labeled "Styles" containing the Style popup and Foldout-only toggle.
            float labelW = Mathf.Min(120f, position.width * 0.5f); // limit how wide the label can grow
            float spacing = 6f;

            var groupRect = new Rect(SwatchPadding - 2f, controlsY - 4f, position.width - SwatchPadding * 2f + 4f, controlHeight * 2 + 12f);
            EditorGUI.DrawRect(new Rect(groupRect.x, groupRect.y, groupRect.width, groupRect.height), new Color(0.0f, 0.0f, 0.0f, 0.0f));

            var styleLabelRect = new Rect(SwatchPadding, controlsY + padding + 2f, labelW, controlHeight);
            var styleFieldRect = new Rect(styleLabelRect.xMax + spacing, styleLabelRect.y, position.width - styleLabelRect.xMax - SwatchPadding - spacing, controlHeight);

            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = _styleMixed;
            EditorGUI.LabelField(styleLabelRect, "Style");
            var newStyle = (ColorDisplayStyle)EditorGUI.EnumPopup(styleFieldRect, _selectedStyle);
            EditorGUI.showMixedValue = false;
            if (EditorGUI.EndChangeCheck())
            {
                _selectedStyle = newStyle;
                // Apply immediately when user changes the display style
                ApplySettings(closeAfter: false);
            }

            // Foldout-only toggle
            var toggleLabelRect = new Rect(SwatchPadding, controlsY + padding + controlHeight + 6, labelW, controlHeight);
            var toggleFieldRect = new Rect(toggleLabelRect.xMax + spacing, toggleLabelRect.y, position.width - toggleLabelRect.xMax - SwatchPadding - spacing, controlHeight);
            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = _foldoutMixed;
            EditorGUI.LabelField(toggleLabelRect, "Foldout only");
            var newFoldout = EditorGUI.Toggle(toggleFieldRect, _selectedFoldoutOnly);
            EditorGUI.showMixedValue = false;
            if (EditorGUI.EndChangeCheck())
            {
                _selectedFoldoutOnly = newFoldout;
                // Apply immediately when user changes the foldout-only setting
                ApplySettings(closeAfter: false);
            }

            controlsY += groupRect.height + 4f;

            // Apply Styles to All button (applies style/foldout-only to all Containers on the current Base)
            var applyAllBtnW = Mathf.Min(180f, position.width - SwatchPadding * 2f);
            var applyAllRect = new Rect((position.width - applyAllBtnW) / 2f, position.height - 34f, applyAllBtnW, 24f);
            if (GUI.Button(applyAllRect, "Apply Styles to All"))
            {
                if (EditorUtility.DisplayDialog("Apply Styles to All Containers",
                    "Apply the current Style and Foldout-only setting to all Containers in the current Base?",
                    "Apply", "Cancel"))
                {
                    ApplyStylesToAllInCurrentBase();
                }
            }
        }

        private void ApplyColor(Color? color, bool closeAfter = false)
        {
            if (_targets == null || _targets.Count == 0)
            {
                if (closeAfter) Close();
                return;
            }

            foreach (var target in _targets)
            {
                if (target is Object obj)
                {
                    Undo.RecordObject(obj, "Set Label Color / Settings");

                    // Color assignment
                    if (color.HasValue)
                    {
                        target.HasLabelColor = true;
                        target.LabelColor = color.Value;
                    }
                    else
                    {
                        target.HasLabelColor = false;
                        target.LabelColor = Color.white;
                    }

                    // Apply style and foldout-only settings from the tray
                    target.LabelColorStyle = _selectedStyle;
                    target.LabelColorFoldoutOnly = _selectedFoldoutOnly;

                    EditorUtility.SetDirty(obj);
                }
            }

            AssetDatabase.SaveAssets();

            // Preserve current foldout state for all open BrowserWindows BEFORE they refresh.
            try
            {
                var openWindows = BrowserWindowRegistry.AllOfType<BrowserWindow>();
                foreach (var w in openWindows)
                {
                    try
                    {
                        // Save foldout state to EditorPrefs so the refresh can restore it
                        w.SaveFoldoutStateToEditor();
                    }
                    catch { }
                }
            }
            catch { }

            // Ensure any BrowserWindow instances refresh to pick up the updated color settings.
            EditorApplication.delayCall += () =>
            {
                try
                {
                    var open = BrowserWindowRegistry.AllOfType<BrowserWindow>();
                    foreach (var w in open)
                    {
                        try
                        {
                            // Reinitialize the BrowserWindow so it rebuilds its TreeView and reads new properties
                            w.ReinitializeForRootChange();
                            // If the new controller carries a LastFoldoutSnapshot (set by lifecycle ops), apply it
                            if (w.Controller?.LastFoldoutSnapshot != null && w.TreeView != null)
                            {
                                try { w.TreeView.SetFoldoutSnapshot(w.Controller.LastFoldoutSnapshot); }
                                catch { }
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            };

            if (closeAfter)
                Close();
        }

        private void ApplySettings(bool closeAfter)
        {
            if (_targets == null || _targets.Count == 0)
            {
                if (closeAfter) Close();
                return;
            }

            foreach (var target in _targets)
            {
                if (target is Object obj)
                {
                    Undo.RecordObject(obj, "Set Label Color Settings");
                    target.LabelColorStyle = _selectedStyle;
                    target.LabelColorFoldoutOnly = _selectedFoldoutOnly;
                    EditorUtility.SetDirty(obj);
                }
            }

            AssetDatabase.SaveAssets();

            // Refresh open BrowserWindow instances so TreeView reads updated properties
            // Preserve current foldout state for all open BrowserWindows BEFORE they refresh.
            try
            {
                var openWindows = BrowserWindowRegistry.AllOfType<BrowserWindow>();
                foreach (var w in openWindows)
                {
                    try
                    {
                        // Save foldout state to EditorPrefs so the refresh can restore it
                        w.SaveFoldoutStateToEditor();
                    }
                    catch { }
                }
            }
            catch { }

            // Refresh open BrowserWindow instances so TreeView reads updated properties
            EditorApplication.delayCall += () =>
            {
                try
                {
                    var open = BrowserWindowRegistry.AllOfType<BrowserWindow>();
                    foreach (var w in open)
                    {
                        try
                        {
                            w.ReinitializeForRootChange();
                            if (w.Controller?.LastFoldoutSnapshot != null && w.TreeView != null)
                            {
                                try { w.TreeView.SetFoldoutSnapshot(w.Controller.LastFoldoutSnapshot); }
                                catch { }
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            };

            if (closeAfter)
                Close();
        }

        private void ApplyStylesToAllInCurrentBase()
        {
            Base targetBase = null;

            // Try to locate a BrowserWindow whose Root contains one of the target containers
            try
            {
                if (_targets != null && _targets.Count > 0)
                {
                    foreach (var t in _targets)
                    {
                        if (t is Container c)
                        {
                            var open = BrowserWindowRegistry.AllOfType<BrowserWindow>();
                            foreach (var w in open)
                            {
                                try
                                {
                                    var rb = w.Root as Base;
                                    if (rb != null && rb.Children != null && rb.Children.Contains(c))
                                    {
                                        targetBase = rb;
                                        break;
                                    }
                                }
                                catch { }
                            }
                        }
                        if (targetBase != null) break;
                    }
                }
            }
            catch { }

            // Fallback: use focused BrowserWindow's Root if it's a Base
            if (targetBase == null)
            {
                try
                {
                    var focused = EditorWindow.focusedWindow as BrowserWindow;
                    if (focused != null)
                        targetBase = focused.Root as Base;
                }
                catch { }
            }

            if (targetBase == null)
            {
                EditorUtility.DisplayDialog("Apply Styles to All", "Could not determine the current Base to apply styles to. Open a BrowserWindow targeting the desired Base and try again.", "OK");
                return;
            }

            int changed = 0;
            try
            {
                foreach (var container in targetBase.Children)
                {
                    if (container == null) continue;
                    var asObj = container as Object;
                    Undo.RecordObject(asObj, "Apply Label Color Settings to All Containers");
                    container.LabelColorStyle = _selectedStyle;
                    container.LabelColorFoldoutOnly = _selectedFoldoutOnly;
                    EditorUtility.SetDirty(asObj);
                    changed++;
                }
            }
            catch { }

            if (changed > 0)
                AssetDatabase.SaveAssets();

            // Preserve foldout state then refresh relevant BrowserWindow instances
            try
            {
                var openWindows = BrowserWindowRegistry.AllOfType<BrowserWindow>();
                foreach (var w in openWindows)
                {
                    try { w.SaveFoldoutStateToEditor(); } catch { }
                }
            }
            catch { }

            EditorApplication.delayCall += () =>
            {
                try
                {
                    var open = BrowserWindowRegistry.AllOfType<BrowserWindow>();
                    foreach (var w in open)
                    {
                        try
                        {
                            if (w.Root as Base == targetBase)
                            {
                                w.ReinitializeForRootChange();
                                if (w.Controller?.LastFoldoutSnapshot != null && w.TreeView != null)
                                {
                                    try { w.TreeView.SetFoldoutSnapshot(w.Controller.LastFoldoutSnapshot); } catch { }
                                }
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            };
        }

        private static void DrawBorder(Rect rect, Color color, float thickness)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        private void LoadCustomColorsFromPrefs()
        {
            _customColors.Clear();
            var raw = EditorPrefs.GetString(CustomColorsKey, "");
            if (string.IsNullOrEmpty(raw)) return;
            var parts = raw.Split(',');
            foreach (var p in parts)
            {
                if (string.IsNullOrWhiteSpace(p)) continue;
                Color col;
                if (ColorUtility.TryParseHtmlString(p.Trim(), out col))
                    _customColors.Add(col);
            }
        }

        private void SaveCustomColorsToPrefs()
        {
            var parts = new List<string>();
            foreach (var c in _customColors)
            {
                parts.Add("#" + ColorUtility.ToHtmlStringRGBA(c));
            }
            var raw = string.Join(",", parts.ToArray());
            EditorPrefs.SetString(CustomColorsKey, raw);
        }

        private void AddCustomColor(Color c)
        {
            // Insert at front so newest appear first
            _customColors.Insert(0, c);
            // Trim
            if (_customColors.Count > MaxCustomColors)
                _customColors.RemoveRange(MaxCustomColors, _customColors.Count - MaxCustomColors);
            SaveCustomColorsToPrefs();
            Repaint();
        }

        private void RemoveCustomColorAt(int index)
        {
            if (index < 0 || index >= _customColors.Count) return;
            _customColors.RemoveAt(index);
            SaveCustomColorsToPrefs();
            Repaint();
        }

        private void LoadDefaultPaletteFromPrefs()
        {
            defaultPalette.Clear();
            var raw = EditorPrefs.GetString(DefaultPaletteKey, "");
            if (string.IsNullOrEmpty(raw))
            {
                // First run: initialize persisted default palette from the static Palette
                foreach (var p in Palette)
                    defaultPalette.Add((p.label, p.color));
                SaveDefaultPaletteToPrefs();
                return;
            }

            var entries = raw.Split(new string[] { ";;" }, System.StringSplitOptions.None);
            foreach (var e in entries)
            {
                if (string.IsNullOrWhiteSpace(e)) continue;
                var parts = e.Split(new string[] { "||" }, System.StringSplitOptions.None);
                var label = parts.Length > 0 ? parts[0] : "";
                var colorStr = parts.Length > 1 ? parts[1] : "null";
                if (colorStr == "null")
                {
                    defaultPalette.Add((label, null));
                }
                else
                {
                    Color col;
                    if (ColorUtility.TryParseHtmlString(colorStr, out col))
                        defaultPalette.Add((label, col));
                    else
                        defaultPalette.Add((label, null));
                }
            }
        }

        private void SaveDefaultPaletteToPrefs()
        {
            var parts = new List<string>();
            foreach (var t in defaultPalette)
            {
                var colorStr = t.color.HasValue ? ("#" + ColorUtility.ToHtmlStringRGBA(t.color.Value)) : "null";
                parts.Add(t.label + "||" + colorStr);
            }
            var raw = string.Join(";;", parts.ToArray());
            EditorPrefs.SetString(DefaultPaletteKey, raw);
        }

        private void RemoveDefaultColorAt(int index)
        {
            if (index < 0 || index >= defaultPalette.Count) return;
            // Don't allow removing the "Clear" entry (null color) to preserve clear action
            if (!defaultPalette[index].color.HasValue) return;
            defaultPalette.RemoveAt(index);
            SaveDefaultPaletteToPrefs();
            Repaint();
        }

        private void RestoreDefaultPaletteToBuiltIn()
        {
            defaultPalette.Clear();
            foreach (var p in Palette)
                defaultPalette.Add((p.label, p.color));
            SaveDefaultPaletteToPrefs();
            // Also clear any custom colors when performing a full reset
            _customColors.Clear();
            SaveCustomColorsToPrefs();
            Repaint();
        }
    }
}
