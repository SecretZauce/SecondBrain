using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SecretZauce.SecondBrain.Editor
{
    public enum QuickPeekSide { None, Left, Right }

    public class TreeViewDragInput
    {
        readonly DragAndDropManager dragDropManager;
        readonly TreeView ownerTreeView;

        int[] hoveredPath;
        Rect hoveredRect;
        Rect hoveredScreenRect;
        bool hoveredIsIStructure;
        bool hoveredIsExpanded;
        float hoveredIndentX;
        int[] potentialDragStartPath;

        // Quick Peek zone state — only set when mouse is in left/right edge zones
        int[] quickPeekHoveredPath;
        Rect quickPeekHoveredScreenRect;
        QuickPeekSide quickPeekHoveredSide;

        // Width of each peek trigger zone (left edge and right edge of every row)
        public const float QuickPeekZoneWidth = 18f;


        public TreeViewDragInput(TreeView treeView)
        {
            ownerTreeView = treeView ?? throw new ArgumentNullException(nameof(treeView));
            dragDropManager = treeView.DragDropManager ?? throw new ArgumentNullException(nameof(dragDropManager));
        }

        /// <summary>
        /// Clear any stored hover state for this frame. Should be called before rows are processed.
        /// </summary>
        public void ClearHover()
        {
            hoveredPath = null;
            hoveredRect = new Rect();
            hoveredScreenRect = new Rect();
            hoveredIsIStructure = false;
            hoveredIsExpanded = false;
            hoveredIndentX = 0f;
            quickPeekHoveredPath = null;
            quickPeekHoveredScreenRect = new Rect();
            quickPeekHoveredSide = QuickPeekSide.None;
        }

        public bool HasHover => hoveredPath != null;
        public Rect HoveredRect => hoveredRect;
        /// <summary>Screen-space rect of the currently hovered row. Valid during and after OnGUI row rendering.</summary>
        public Rect HoveredScreenRect => hoveredScreenRect;
        public float HoveredIndentX => hoveredIndentX;
        // Expose the hovered path so callers can query the hovered item
        public int[] HoveredPath => hoveredPath;

        // Quick Peek zone state — only populated when mouse is in a left/right edge zone
        public int[] QuickPeekHoveredPath => quickPeekHoveredPath;
        public QuickPeekSide QuickPeekHoveredSide => quickPeekHoveredSide;
        public Rect QuickPeekHoveredScreenRect => quickPeekHoveredScreenRect;

        /// <summary>
        /// Handle drag & drop related events for a single row.
        /// This was previously TreeView.HandleDragEvents.
        /// </summary>
        public void HandleRow(Rect rect, int[] path, bool isIStructure, float indentX, bool isExpanded = false)
        {
            Event e = Event.current;
            if (e == null)
                return;

            // Track hover state for the row when the mouse is over it (works even when not dragging).
            // This enables keyboard shortcuts that operate on the hovered item (e.g., Q to open property editor).
            if (rect.Contains(e.mousePosition))
            {
                hoveredPath = path;
                hoveredRect = rect;
                // Capture the screen-space position of the row for QuickPeekWindow positioning.
                // GUIUtility.GUIToScreenPoint accounts for scroll-view offsets, so this gives the
                // correct visible screen position even when the tree is scrolled.
                hoveredScreenRect = new Rect(GUIUtility.GUIToScreenPoint(rect.position), rect.size);
                hoveredIsIStructure = isIStructure;
                hoveredIsExpanded = isExpanded; // Track expanded state for drop zone logic
                hoveredIndentX = indentX; // Store the indented x value

                // Determine Quick Peek zone: only trigger when mouse is in left/right edge strip
                var side = GetPeekZone(rect, e.mousePosition);
                if (side != QuickPeekSide.None)
                {
                    quickPeekHoveredPath = path;
                    quickPeekHoveredSide = side;
                    quickPeekHoveredScreenRect = new Rect(GUIUtility.GUIToScreenPoint(rect.position), rect.size);
                }
            }

            // Handle mouse down to track potential drag start
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                // Track potential drag when mouse is pressed over the row (even if not selected).
                // This allows selecting-on-mousedown + immediate drag without waiting for MouseUp.
                if (!ownerTreeView.Renamer.IsRenamingAny && rect.Contains(e.mousePosition))
                {
                    dragDropManager.OnMouseDown(e.mousePosition);
                    // Record which path the mouse was pressed on so a drag can begin for that
                    // path if it wasn't previously selected.
                    potentialDragStartPath = (int[])path.Clone();
                    // Ensure TreeView polls for mouse-leave while a potential drag is active
                    try
                    {
                        ownerTreeView.StartDragLeavePolling();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                    }
                    // Don't consume the event - let selection handling (deferred) proceed
                }
            }
         
            // Handle right-click context menu (button == 1)
            if (e.type == EventType.MouseDown && e.button == 1 && rect.Contains(e.mousePosition))
            {
                // Before showing the context menu, ensure the clicked item is selected as a single selection
                // unless it already belongs to a multi-selection. Use the controller API to mutate selection state.
                try
                {
                    var wnd = ownerTreeView.OwnerWindow;
                    var controller = wnd?.Controller;
                    var ctx = ownerTreeView.Context;

                    bool isSelected = ctx != null && ctx.IsPathSelected(ownerTreeView.OwnerWindow, path);

                    // If the clicked path is not selected, select it (single select).
                    // If it is selected and part of a multi-selection, leave the selection intact.
                    if (controller != null && !isSelected)
                    {
                        controller.HandleItemSelected(path, isMultiSelect: false, isRangeSelect: false, ownerTreeView);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
                
                Object obj = ownerTreeView.GetObjectAtPath(path);
                ItemContextMenu.ShowContextMenu(ownerTreeView.OwnerWindow, path, obj, isIStructure);
                e.Use();
            }

            // Handle mouse up to cancel potential drag if it didn't start
            if (e.type == EventType.MouseUp && e.button == 0)
            {
                if (dragDropManager.IsPotentialDrag)
                {
                    dragDropManager.CancelPotentialDrag();
                }
                potentialDragStartPath = null;
            }

            // Handle internal mouse drag to initiate drag operation (with threshold)
            if (e.type == EventType.MouseDrag && e.button == 0)
            {
                if (ownerTreeView.Renamer.IsRenamingAny || dragDropManager.IsDragging)
                    return;

                if (!rect.Contains(e.mousePosition))
                    return;

                // Only start drag if threshold exceeded
                if (!dragDropManager.HasExceededDragThreshold(e.mousePosition))
                    return;

                List<int[]> pathsToDrag = null;

                if (ownerTreeView.Context.IsPathSelected(ownerTreeView.OwnerWindow, path))
                {
                    // Drag current multi-selection. Make a deep copy of the paths to avoid
                    // any mutation or shared-array issues further down the pipeline.
                    var src = ownerTreeView.Context.SelectedPaths;
                    pathsToDrag = new List<int[]>();
                    if (src != null)
                    {
                        foreach (var p in src)
                        {
                            if (p == null) continue;
                            var copy = new int[p.Length];
                            Array.Copy(p, copy, p.Length);
                            pathsToDrag.Add(copy);
                        }
                    }
                }
                else if (potentialDragStartPath != null && StructureUtils.ArePathsEqual(potentialDragStartPath, path))
                {
                    // Mouse was pressed on this path but it wasn't selected; start a single-item drag
                    var singleCopy = new int[path.Length];
                    Array.Copy(path, singleCopy, path.Length);
                    pathsToDrag = new List<int[]> { singleCopy };
                }

                if (pathsToDrag != null && pathsToDrag.Count > 0)
                {
                    dragDropManager.BeginDrag(pathsToDrag, ownerTreeView.Context.Collections);
                    // Set initial hover state including expanded state
                    hoveredPath = path;
                    hoveredRect = rect;
                    hoveredIsIStructure = isIStructure;
                    hoveredIsExpanded = isExpanded;
                    potentialDragStartPath = null;
                    // Ensure TreeView polls for mouse-leave while a drag is active
                    try
                    {
                        ownerTreeView.StartDragLeavePolling();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                    }
                    e.Use();
                }
            }

            // Handle Unity's DragAndDrop events (external assets from Project tab)
            // Only process when over this rect
            if (rect.Contains(e.mousePosition))
            {
                switch (e.type)
                {
                    case EventType.DragUpdated:
                        // Check if we have draggable objects
                        if (DragAndDrop.objectReferences != null && DragAndDrop.objectReferences.Length > 0)
                        {
                            // Start external drag if not already dragging
                            // Do not start external drag while renaming any item
                            if (!ownerTreeView.Renamer.IsRenamingAny && !dragDropManager.IsDragging)
                            {
                                var ctx = ownerTreeView.Context;
                                dragDropManager.BeginExternalDrag(ctx.Collections);
                                // Notify the user immediately when the drag contains objects from
                                // scenes that have never been saved — the reference can't be resolved.
                                if (dragDropManager.HasUnsavedSceneObjects)
                                {
                                    ownerTreeView.OwnerWindow?.ShowNotification(
                                        new GUIContent(SceneObjectRefUtils.UnsavedSceneNotification));
                                }
                            }
                            
                            // Update hover state including expanded state
                            hoveredPath = path;
                            hoveredRect = rect;
                            hoveredIsIStructure = isIStructure;
                            hoveredIsExpanded = isExpanded;
                            
                            // Immediately validate this drop target
                            dragDropManager.UpdateDrag(e.mousePosition, path, rect, isIStructure, isExpanded);
                            
                            // Set visual mode based on validation result
                            if (dragDropManager.HasValidDropTarget())
                            {
                                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                            }
                            else
                            {
                                DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                            }
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// Should be called after all rows have been processed for this frame.
        /// Applies a final drag update using the stored hovered row (if any).
        /// </summary>
        public void ApplyDragUpdate()
        {
            Event e = Event.current;
            if (e == null)
                return;

            if (!dragDropManager.IsDragging) 
                return;
            
            if (hoveredPath != null)
            {
                dragDropManager.UpdateDrag(e.mousePosition, hoveredPath, hoveredRect, hoveredIsIStructure, hoveredIsExpanded);
            }
            else
            {
                // No valid hover target - update with null to clear dropPosition, but still update mouse position
                dragDropManager.UpdateDrag(e.mousePosition, null, new Rect(), false, false);

                // Set visual mode to Rejected when no hover target during external drag update
                if (dragDropManager.IsExternalDrag && e.type == EventType.DragUpdated)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                }
            }
        }

        /// <summary>Returns which Quick Peek zone the mouse is in for a given row rect.</summary>
        public static QuickPeekSide GetPeekZone(Rect rowRect, Vector2 mousePos)
        {
            if (!rowRect.Contains(mousePos)) return QuickPeekSide.None;
            if (mousePos.x <= rowRect.x + QuickPeekZoneWidth) return QuickPeekSide.Left;
            if (mousePos.x >= rowRect.xMax - QuickPeekZoneWidth) return QuickPeekSide.Right;
            return QuickPeekSide.None;
        }

        /// <summary>
        /// Draws a gray info indicator in the peek zone when the mouse is hovering over it.
        /// Should be called during the Repaint pass after row content has been drawn.
        /// skipRightZone should be true when a button already occupies the right edge (e.g. the + button).
        /// </summary>
        public static void DrawPeekZoneIndicator(Rect rowRect, bool skipRightZone = false, bool skipLeftZone = false)
        {
            if (Event.current == null || Event.current.type != EventType.Repaint) return;
            var side = GetPeekZone(rowRect, Event.current.mousePosition);
            if (side == QuickPeekSide.None) return;
            if (side == QuickPeekSide.Right && skipRightZone) return;
            if (side == QuickPeekSide.Left && skipLeftZone) return;

            Rect zoneRect = side == QuickPeekSide.Left
                ? new Rect(rowRect.x, rowRect.y, QuickPeekZoneWidth, rowRect.height)
                : new Rect(rowRect.xMax - QuickPeekZoneWidth, rowRect.y, QuickPeekZoneWidth, rowRect.height);

            EditorGUI.DrawRect(zoneRect, new Color(0.0f, 0.0f, 0.0f, 0.22f));
        }

        /// <summary>
        /// Processes global drag events that are not tied to a specific row.
        /// Returns a DragInputResult describing actions the caller should take.
        /// This method does not call Controller; it only interacts with dragDropManager.
        /// </summary>
        public DragInputResult ProcessGlobalDragEvent()
        {
            var result = new DragInputResult();
            Event e = Event.current;
            if (e == null)
                return result;

            // Handle DragExited for internal drags (window exit)
            if (e.type == EventType.DragExited && dragDropManager.IsDragging && !dragDropManager.IsExternalDrag)
            {
                dragDropManager.CancelDrag();
                result.Handled = true;
                result.Cancelled = true;
                result.NeedsRepaint = true;
                e.Use();
                return result;
            }

            // Global MouseUp handler - ensure potential drag cancels
            if (e.type == EventType.MouseUp && e.button == 0)
            {
                if (dragDropManager.IsPotentialDrag)
                {
                    dragDropManager.CancelPotentialDrag();
                    result.Handled = true;
                    result.NeedsRepaint = true;
                    return result;
                }
            }

            // Handle Escape key to cancel any drag operation
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                if (dragDropManager.IsDragging || dragDropManager.IsPotentialDrag)
                {
                    dragDropManager.CancelDrag();
                    e.Use();
                    result.Handled = true;
                    result.Cancelled = true;
                    result.NeedsRepaint = true;
                    return result;
                }
            }

            // Handle drag perform (drop)
            if (e.type == EventType.DragPerform)
            {
                if (!dragDropManager.IsDragging)
                    return result;

                // Attempt to end drag and get payload
                if (dragDropManager.EndDrag(
                        out var draggedPaths,
                        out var draggedItems,
                        out var dropTargetPath,
                        out var dropPosition))
                {
                    result.Handled = true;
                    result.DropPerformed = true;
                    result.IsExternal = dragDropManager.IsExternalDrag;
                    result.DraggedPaths = draggedPaths;
                    result.DraggedItems = draggedItems;
                    result.DropTargetPath = dropTargetPath;
                    result.DropPosition = (int)dropPosition;
                    result.NeedsRepaint = true;
                    e.Use();
                    return result;
                }

                // Invalid drop — capture external state before clearing drag
                bool wasExternal = dragDropManager.IsExternalDrag;
                List<Object> externalObjs = null;
                if (wasExternal && DragAndDrop.objectReferences != null)
                {
                    externalObjs = new List<Object>();
                    foreach (var o in DragAndDrop.objectReferences)
                        if (o != null) externalObjs.Add(o);
                }
                dragDropManager.CancelDrag();
                result.Handled = true;
                result.Cancelled = true;
                result.IsExternal = wasExternal;
                result.DraggedItems = externalObjs;
                result.NeedsRepaint = true;
                e.Use();
                return result;
            }

            return result;
        }
    }
}
