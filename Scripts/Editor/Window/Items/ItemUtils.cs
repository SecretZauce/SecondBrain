using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SecretZauce.SecondBrain.Editor
{
    public static class ItemUtils
    {
        // ── Icon caches ────────────────────────────────────────────────────────
        // Avoid calling EditorGUIUtility.IconContent / ObjectContent on every repaint
        // per row.  Caches are keyed by type (or editor-icon name) and are cleared
        // whenever BrowserSettings change (e.g. ShowIconsPerType toggle).
        static readonly Dictionary<System.Type, Texture>   s_IconByType        = new Dictionary<System.Type, Texture>();
        // Per-instance cache for non-ScriptableObject assets (Sprite, Texture2D, Material, etc.)
        // whose thumbnails differ per asset rather than per type.
        static readonly Dictionary<int, Texture>            s_IconByInstance    = new Dictionary<int, Texture>();
        static readonly Dictionary<string, Texture>         s_EditorIconByName  = new Dictionary<string, Texture>();
        static readonly Dictionary<string, Texture>         s_CustomIconByName  = new Dictionary<string, Texture>();
        static bool s_IconCacheSubscribed;

        static void EnsureIconCacheSubscription()
        {
            if (s_IconCacheSubscribed) return;
            s_IconCacheSubscribed = true;
            BrowserSettings.OnSettingsChanged += static () =>
            {
                s_IconByType.Clear();
                s_IconByInstance.Clear();
                s_EditorIconByName.Clear();
                s_CustomIconByName.Clear();
            };
        }

        // Centralize icon lookup for nodes to avoid duplicated logic
        public static Texture GetIconForNode(Object node)
        {
            if (!BrowserSettings.ShowIconsPerType || node == null || node is Container)
                return null;

            EnsureIconCacheSubscription();

            var nodeType = node.GetType();

            if (node is SceneComponentRef sceneComponentRef)
            {
                var component = SceneObjectMap.Resolve(sceneComponentRef.sceneComponent);
                if (component != null)
                {
                    // Resolved: cache icon by live component type.
                    var liveType = component.GetType();
                    if (!s_IconByType.TryGetValue(liveType, out var liveTex))
                    {
                        liveTex = EditorGUIUtility.ObjectContent(component, liveType).image;
                        s_IconByType[liveType] = liveTex;
                    }
                    return liveTex;
                }

                // Unresolved (scene not loaded): derive icon from stored type name and cache by type.
                var componentType = SceneComponentRefUtils.GetComponentType(sceneComponentRef.sceneComponent);
                var fallbackType = componentType ?? typeof(Component);
                if (!s_IconByType.TryGetValue(fallbackType, out var fallbackTex))
                {
                    fallbackTex = EditorGUIUtility.ObjectContent(null, fallbackType).image;
                    s_IconByType[fallbackType] = fallbackTex;
                }
                return fallbackTex;
            }

            // Prefer project-asset icons (IHasCustomIcon) loaded via IconUtils, then fall
            // back to Unity built-in editor icons (IHasEditorIcon) for other node types.
            if (node is IHasCustomIcon customIconOwner)
            {
                if (!string.IsNullOrEmpty(customIconOwner.CustomIcon))
                {
                    if (!s_CustomIconByName.TryGetValue(customIconOwner.CustomIcon, out var cachedCustomTex))
                    {
                        cachedCustomTex = IconUtils.Load(customIconOwner.CustomIcon);
                        s_CustomIconByName[customIconOwner.CustomIcon] = cachedCustomTex;
                    }
                    return cachedCustomTex;
                }
                // fall through when CustomIcon is empty
            }

            if (node is IHasEditorIcon editorIconOwner)
            {
                if (!string.IsNullOrEmpty(editorIconOwner.EditorIcon))
                {
                    if (!s_EditorIconByName.TryGetValue(editorIconOwner.EditorIcon, out var cachedEditorTex))
                    {
                        cachedEditorTex = EditorGUIUtility.IconContent(editorIconOwner.EditorIcon).image;
                        s_EditorIconByName[editorIconOwner.EditorIcon] = cachedEditorTex;
                    }
                    return cachedEditorTex;
                }
                // fall through to other logic when the EditorIcon is empty
            }

            // ScriptableObject subclasses (Container, Base, custom nodes) have type-level icons,
            // so cache by type. Non-ScriptableObject assets (Sprite, Texture2D, Material, etc.)
            // have per-asset thumbnails that differ between instances, so cache by instance ID.
            if (nodeType.IsSubclassOf(typeof(ScriptableObject)))
            {
                if (!s_IconByType.TryGetValue(nodeType, out var tex))
                {
                    if (string.Equals(nodeType.Name, "SceneObjectRef", StringComparison.Ordinal))
                        tex = EditorGUIUtility.ObjectContent(null, typeof(GameObject)).image;
                    else
                        tex = EditorGUIUtility.ObjectContent(node, nodeType).image;
                    s_IconByType[nodeType] = tex;
                }
                return tex;
            }
            else
            {
                int instanceId = node.GetStableInstanceId();
                if (!s_IconByInstance.TryGetValue(instanceId, out var tex))
                {
                    tex = EditorGUIUtility.ObjectContent(node, nodeType).image;
                    s_IconByInstance[instanceId] = tex;
                }
                return tex;
            }
        }

        /// <summary>Returns the display name for a node, resolving SceneObjectRef to its last-known scene object name.</summary>
        public static string GetDisplayName(Object node)
        {
            if (node is SceneObjectRef sceneRef)
            {
                string n = sceneRef.sceneObject?.LastKnownName;
                return !string.IsNullOrEmpty(n) ? n : node.name;
            }
            return node?.name ?? string.Empty;
        }

        /// <summary>Returns a GUIContent with icon and display name for use in tab bars.</summary>
        public static GUIContent BuildTabContent(Object child)
            => new GUIContent(GetDisplayName(child), GetIconForNode(child));

        static readonly Color DefaultSelectionColor = new Color(0.24f, 0.48f, 0.90f, 0.25f);

        // Draw hover highlight and selection background for a given row rect.
        // itemColor/colorStyle let a row's assigned Background/Gradient color tint the selection
        // highlight itself, so selecting a colored item doesn't lose its color entirely; rows
        // without that color style keep the plain default highlight.
        public static void DrawHoverAndSelection(Rect itemRect, bool isRenaming, bool isSelected, bool isDragging, Color hoverColor,
            Color? itemColor = null, ColorDisplayStyle colorStyle = ColorDisplayStyle.FontColor)
        {
            // Do not draw hover highlight when a floating picker tray (emoji/color) is open.
            // Keep drawing the selection background so existing selections remain visible.
            if (!isSelected && !isRenaming && Event.current != null && Event.current.type == EventType.Repaint && itemRect.Contains(Event.current.mousePosition) && !isDragging && !PickerTrayBase.IsAnyTrayOpen)
            {
                var hoverRect = itemRect;
                hoverRect.height += 1f; // increase hover highlight height by 1
                EditorGUI.DrawRect(hoverRect, hoverColor);
            }

            if (isSelected)
            {
                var selRect = itemRect;
                selRect.height += 1f; // increase selection highlight height by 1
                Color selectionColor = itemColor.HasValue && (colorStyle == ColorDisplayStyle.Background || colorStyle == ColorDisplayStyle.Gradient)
                    ? new Color(itemColor.Value.r, itemColor.Value.g, itemColor.Value.b, DefaultSelectionColor.a)
                    : DefaultSelectionColor;
                EditorGUI.DrawRect(selRect, selectionColor);
            }
        }

        /// <summary>
        /// Draws a horizontal fade from the provided color on the left to transparent on the right.
        /// Uses a cached 1xN white gradient texture tinted via GUI.color and stretched to the target rect.
        /// This avoids seams introduced by adjacent semi-transparent rects.
        /// </summary>
        static Texture2D s_HorizontalGradientTex;
        static void EnsureHorizontalGradientTex(int size = 128)
        {
            if (s_HorizontalGradientTex != null) return;
            int w = Mathf.Max(2, size);
            s_HorizontalGradientTex = new Texture2D(w, 1, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            for (int x = 0; x < w; x++)
            {
                float a = 1f - (x / (float)(w - 1));
                s_HorizontalGradientTex.SetPixel(x, 0, new Color(1f, 1f, 1f, a));
            }
            s_HorizontalGradientTex.Apply();
        }

        public static void DrawHorizontalFade(Rect rect, Color color, int textureSize = 128)
        {
            if (Event.current == null || Event.current.type != EventType.Repaint) return;
            EnsureHorizontalGradientTex(textureSize);
            if (s_HorizontalGradientTex == null)
            {
                // Fallback to solid fill if texture couldn't be created
                EditorGUI.DrawRect(rect, color);
                return;
            }

            Color prev = GUI.color;
            // Multiply the white gradient texture by the requested color (including its alpha)
            GUI.color = color;
            GUI.DrawTexture(rect, s_HorizontalGradientTex, ScaleMode.StretchToFill, true);
            GUI.color = prev;
        }

        /// <summary>
        /// Row interaction callbacks required by <see cref="HandleRowClickAndSelection"/>.
        ///
        /// This used to be nine <see cref="Func{T,TResult}"/>/<see cref="Action"/> parameters. Each
        /// captured the caller's state, so every one allocated a delegate (plus a closure) on every
        /// row on every IMGUI pass — a few hundred rows produced thousands of allocations per mouse
        /// move. An interface implemented by the owning TreeView costs nothing per call.
        /// </summary>
        public interface IRowInteractionHost
        {
            /// <summary>Records the click and returns true when it completes a double-click.</summary>
            bool HandleRowClick(int[] path);
            void StartRowRename(int[] path);
            void EnqueuePendingRowClick(int[] path, bool isMultiSelect, bool isRangeSelect);
            bool IsRowDragging { get; }
            bool IsRowRenaming(int[] path);
            bool IsRowSelected(int[] path);
            bool HasMultipleRowsSelected { get; }
            /// <summary>Invoked instead of rename on double-click when DoubleClickAction is Enter.</summary>
            void OnRowDoubleClickEnter(int[] path);
        }

        // Track last mouse-down path and whether it was a press on a multi-selected item.
        // Used to defer single-selection until MouseUp in that specific case so dragging
        // keeps the existing multi-selection.
        static int[] lastMouseDownPath;
        static bool lastMouseDownWasOnSelectedMulti;

        // Handle click/double-click selection behavior for a row. Defers selection via the host so
        // the caller can apply it once all visible paths are known.
        // supportsDoubleClickEnter: when false, double-click always falls back to rename.
        public static void HandleRowClickAndSelection(
            int[] path,
            Rect rowRect,
            IRowInteractionHost host,
            bool supportsDoubleClickEnter = false)
        {
            // Skip click handling while dragging, renaming, or when a picker tray is open
            // (emoji/color trays should block tree selection/hover interactions).
            if (host.IsRowDragging || host.IsRowRenaming(path) || Event.current == null || PickerTrayBase.IsAnyTrayOpen)
                return;

            // Handle MouseDown: select unselected items immediately (so drag can start as single-item),
            // but if the user mouse-downs on a member of a multi-selection (without modifiers) we defer
            // the single-select until MouseUp so dragging preserves the multi-selection.
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && rowRect.Contains(Event.current.mousePosition))
            {
                // If renaming or dragging, we should not do selection
                if (host.IsRowRenaming(path) || host.IsRowDragging)
                    return;

                bool multiModifier = Event.current.control || Event.current.command;
                bool rangeModifier = Event.current.shift;

                bool isCurrentlySelected = host.IsRowSelected(path);
                bool hasMultiple = host.HasMultipleRowsSelected;

                // If clicked path is already selected and there is a multi-selection, and no
                // modifiers are used, defer converting to single selection until MouseUp.
                if (!multiModifier && !rangeModifier && isCurrentlySelected && hasMultiple)
                {
                    lastMouseDownPath = path != null ? (int[])path.Clone() : null;
                    lastMouseDownWasOnSelectedMulti = true;
                    return;
                }

                // Default: enqueue selection on MouseDown so drag can begin with the new selection
                bool multi = Event.current.control || Event.current.command;
                bool range = Event.current.shift;
                host.EnqueuePendingRowClick(path, multi, range);
                lastMouseDownPath = path != null ? (int[])path.Clone() : null;
                lastMouseDownWasOnSelectedMulti = false;
                // Do NOT consume MouseDown so DragInput can observe it
                return;
            }

            // Handle MouseUp: process double-click and deferred single-select cases
            if (Event.current.type == EventType.MouseUp && Event.current.button == 0 && rowRect.Contains(Event.current.mousePosition))
            {
                // First, check double-click
                bool isDoubleClick = host.HandleRowClick(path);
                if (isDoubleClick)
                {
                    // When Enter mode is active and the row supports an enter action, use it.
                    // Otherwise fall back to rename.
                    if (BrowserSettings.DoubleClickAction == DoubleClickActionType.Enter && supportsDoubleClickEnter)
                        host.OnRowDoubleClickEnter(path);
                    else
                        host.StartRowRename(path);
                    Event.current.Use();
                    lastMouseDownPath = null;
                    lastMouseDownWasOnSelectedMulti = false;
                    return;
                }

                // If we deferred selection at MouseDown because it was a multi-selected press,
                // apply the single-select now unless a drag occurred.
                if (lastMouseDownWasOnSelectedMulti && lastMouseDownPath != null && StructureUtils.ArePathsEqual(lastMouseDownPath, path))
                {
                    if (!host.IsRowDragging)
                    {
                        bool multi = Event.current.control || Event.current.command;
                        bool range = Event.current.shift;
                        host.EnqueuePendingRowClick(path, multi, range);
                    }

                    lastMouseDownPath = null;
                    lastMouseDownWasOnSelectedMulti = false;
                }
            }
        }
    }
}
