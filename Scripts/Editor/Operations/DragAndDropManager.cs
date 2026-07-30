using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SecretZauce.SecondBrain.Editor
{
    /// <summary>
    /// Manages drag-and-drop operations for reparenting items in the tree view.
    /// Handles visual feedback, drop target detection, and coordinate with lifecycle manager.
    /// Supports both internal dragging and external Unity Object dragging from Project tab.
    /// </summary>
    public class DragAndDropManager
    {
        // Drag state
        bool isDragging;
        bool isExternalDrag; // True if dragging from Unity Project tab
        bool hasUnsavedSceneObjects; // True when external drag contains GameObjects from unsaved scenes
        List<int[]> draggedPaths = new List<int[]>();
        readonly List<Object> draggedItems = new List<Object>();
        List<IStructure> collections;
        
        TreeView ownerTreeView;
        IStructure Root => ownerTreeView?.OwnerWindow?.Root;

        // Drag threshold to prevent accidental drags
        const float DRAG_THRESHOLD = 2f; // pixels
        bool isPotentialDrag; // True when mouse is down but hasn't exceeded threshold yet
        Vector2 mouseDownPos; 

        // Drop target state
        int[] dropTargetPath;
        DropPosition dropPosition = DropPosition.None;

        public enum DropPosition
        {
            None,
            Before,   // Insert before the target
            Inside,   // Add as child of target
            After,    // Insert after the target
            RootAfterAll // Drop into empty space below all items → moves/wraps to depth-0
        }

        public bool IsDragging => isDragging;
        public bool IsExternalDrag => isExternalDrag;
        public bool IsPotentialDrag => isPotentialDrag;
        public DropPosition CurrentDropPosition => dropPosition;
        /// <summary>True when the current external drag contains GameObjects from scenes that have never been saved.</summary>
        public bool HasUnsavedSceneObjects => hasUnsavedSceneObjects;

        /// <summary>Returns a snapshot copy of the currently dragged items (for Pro drag-out).</summary>
        public List<Object> GetDraggedItems() => new List<Object>(draggedItems);

        /// <summary>Returns deep-copied paths of the currently dragged items (for Pro drag-out).</summary>
        public List<int[]> GetDraggedPaths()
        {
            var result = new List<int[]>(draggedPaths.Count);
            foreach (var p in draggedPaths)
            {
                if (p == null) continue;
                var copy = new int[p.Length];
                Array.Copy(p, copy, p.Length);
                result.Add(copy);
            }
            return result;
        }

        /// <summary>
        /// Sets the owner TreeView so this manager can access the per-window Root.
        /// Called by TreeView after construction.
        /// </summary>
        public void SetOwnerTreeView(TreeView treeView)
        {
            ownerTreeView = treeView;
        }

        /// <summary>
        /// Called when mouse button is pressed down on a selected item.
        /// This marks the start of a potential drag operation.
        /// </summary>
        public void OnMouseDown(Vector2 mousePosition)
        {
            mouseDownPos = mousePosition;
            isPotentialDrag = true;
        }

        /// <summary>
        /// Checks if the mouse has moved far enough from mouseDownPos to exceed the drag threshold.
        /// </summary>
        public bool HasExceededDragThreshold(Vector2 currentMousePosition)
        {
            if (!isPotentialDrag)
                return false;

            float distance = Vector2.Distance(mouseDownPos, currentMousePosition);
            return distance >= DRAG_THRESHOLD;
        }

        /// <summary>
        /// Cancels a potential drag (e.g., when mouse button is released without dragging)
        /// </summary>
        public void CancelPotentialDrag()
        {
            isPotentialDrag = false;
        }

        /// <summary>
        /// Signals that the mouse is over empty space below all rows — drop will move/wrap items to depth-0.
        /// Uses an empty (non-null) array as a sentinel so EndDrag can distinguish from "no target".
        /// </summary>
        public void SetRootLevelDrop()
        {
            dropTargetPath = Array.Empty<int>();
            dropPosition = DropPosition.RootAfterAll;
        }

        /// <summary>
        /// Checks if we currently have a valid drop target
        /// </summary>
        public bool HasValidDropTarget()
        {
            if (hasUnsavedSceneObjects)
                return false;
            if (dropPosition == DropPosition.RootAfterAll)
                return true;
            return dropTargetPath != null && dropPosition != DropPosition.None;
        }

        /// <summary>
        /// Starts a drag operation with the selected items
        /// </summary>
        public void BeginDrag(List<int[]> selectedPaths, List<IStructure> collections)
        {
            this.collections = collections; // Store for type checking later
            isExternalDrag = false; // This is internal drag
            isPotentialDrag = false; // Clear potential drag state
            // Deep-copy incoming path arrays to avoid shared-array mutation issues
            draggedPaths = new List<int[]>();
            if (selectedPaths != null)
            {
                foreach (var p in selectedPaths)
                {
                    if (p == null) continue;
                    var copy = new int[p.Length];
                    Array.Copy(p, copy, p.Length);
                    draggedPaths.Add(copy);
                }
            }
            draggedItems.Clear();

            // Collect dragged items using our internal deep-copied path list
            foreach (var path in draggedPaths)
            {
                if (path == null) continue;
                var item = StructureUtils.GetNodeAtPath(path, this.collections);
                if (item != null)
                {
                    draggedItems.Add(item);
                }
            }

            isDragging = draggedItems.Count > 0;
        }

        /// <summary>
        /// Starts an external drag operation with Unity Objects from Project tab or Hierarchy window.
        /// </summary>
        public void BeginExternalDrag(List<IStructure> collections)
        {
            this.collections = collections;
            isExternalDrag = true;
            hasUnsavedSceneObjects = false;
            draggedPaths.Clear();
            draggedItems.Clear();

            draggedItems.AddRange(CollectExternalDragObjects());

            // Flag if any dragged GameObject belongs to a scene that has never been saved.
            // Such scenes have an empty path and cannot produce a stable GlobalObjectId.
            hasUnsavedSceneObjects = draggedItems.Any(SceneObjectRefUtils.IsSceneObjectFromUnsavedScene);

            isDragging = draggedItems.Count > 0;
        }

        /// <summary>
        /// Collects every object in the current Unity DragAndDrop payload that SecondBrain can receive.
        /// Covers <see cref="DragAndDrop.objectReferences"/> (Project tab and Hierarchy GameObject drags)
        /// as well as .unity entries in <see cref="DragAndDrop.paths"/> — dragging a scene header from the
        /// Hierarchy window populates only the paths and leaves objectReferences empty, so the SceneAsset
        /// has to be loaded from disk for it to flow through the normal drop pipeline.
        /// </summary>
        public static List<Object> CollectExternalDragObjects()
        {
            var items = new List<Object>();

            if (DragAndDrop.objectReferences != null)
            {
                foreach (var obj in DragAndDrop.objectReferences)
                {
                    if (obj != null)
                        items.Add(obj);
                }
            }

            if (DragAndDrop.paths != null)
            {
                foreach (var path in DragAndDrop.paths)
                {
                    if (string.IsNullOrEmpty(path) || !path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                        continue;
                    var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
                    if (sceneAsset != null && !items.Contains(sceneAsset))
                        items.Add(sceneAsset);
                }
            }

            return items;
        }

        /// <summary>
        /// Returns true when the current DragAndDrop payload contains anything SecondBrain can receive.
        /// Scene header drags from the Hierarchy window arrive as .unity paths with no objectReferences,
        /// so callers must never gate on objectReferences alone.
        /// </summary>
        public static bool HasExternalDragContent()
        {
            if (DragAndDrop.objectReferences != null && DragAndDrop.objectReferences.Length > 0)
                return true;
            if (DragAndDrop.paths != null)
            {
                foreach (var path in DragAndDrop.paths)
                {
                    if (!string.IsNullOrEmpty(path) && path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Updates drag state and determines drop target
        /// </summary>
        public void UpdateDrag(Vector2 mousePos, int[] hoveredPath, Rect hoveredRect, bool isIStructure,
            bool isExpanded = false)
        {

            if (!isDragging)
                return;

            // Defensive check: collections/root may be invalidated by undo operations
            if (collections == null || Root == null)
            {
                CancelDrag();
                return;
            }

            if (hoveredPath == null)
            {
                dropTargetPath = null;
                dropPosition = DropPosition.None;
                return;
            }

            // Check if dropping onto self or a child of dragged items (invalid)
            if (IsDropTargetInvalid(hoveredPath))
            {
                dropTargetPath = null;
                dropPosition = DropPosition.None;
                return;
            }

            // Calculate preliminary drop position based on hover location
            DropPosition preliminaryPosition;

            // If target is IStructure, allow dropping inside
            if (isIStructure)
            {
                // Divide the rect into three zones: top 25% (before), middle 50% (inside), bottom 25% (after)
                // IMPORTANT: When the item is expanded, the "After" zone should not place items as siblings
                // because the visual space below belongs to the children. Only allow "After" (sibling placement)
                // when the item is collapsed.
                float relativeY = mousePos.y - hoveredRect.y;
                float height = hoveredRect.height;

                if (relativeY < height * 0.25f)
                {
                    preliminaryPosition = DropPosition.Before;
                }
                else if (relativeY > height * 0.75f)
                {
                    // Only allow "After" (sibling placement) when the parent is collapsed
                    // When expanded, treat the bottom zone as "Inside" to add as first child
                    preliminaryPosition = isExpanded ? DropPosition.Inside : DropPosition.After;
                }
                else
                {
                    preliminaryPosition = DropPosition.Inside;
                }
            }
            else
            {
                // For leaf nodes, only allow before/after
                float relativeY = mousePos.y - hoveredRect.y;
                float height = hoveredRect.height;

                preliminaryPosition = relativeY < height * 0.5f ? DropPosition.Before : DropPosition.After;
            }

            // Normalize drop position to avoid showing invalid indicators
            // This prevents showing "After item[i]" when it's the same as "Before item[i+1]"
            NormalizeDropPosition(hoveredPath, preliminaryPosition, out dropTargetPath, out dropPosition);

            // Final validation: ensure the drop position doesn't point to where items already are
            if (dropPosition == DropPosition.Before || dropPosition == DropPosition.After)
            {
                if (IsDropPositionRedundant(dropTargetPath, dropPosition))
                {
                    dropTargetPath = null;
                    dropPosition = DropPosition.None;
                    return;
                }
            }

            // CRITICAL: Validate type compatibility before showing indicator
            // Get the target parent where items would be added
            IStructure targetParent = GetTargetParentForDrop(dropTargetPath, dropPosition);

            if (targetParent != null && draggedItems.Count > 0)
            {
                bool canAccept = targetParent.CanAcceptChild(draggedItems[0]);
                if (!canAccept)
                {
                    dropTargetPath = null;
                    dropPosition = DropPosition.None;
                }
            }
        }

        /// <summary>
        /// Ends the drag operation and returns drop information
        /// </summary>
        public bool EndDrag(out List<int[]> draggedPathsOut, out List<Object> draggedItemsOut,
            out int[] dropTargetPathOut, out DropPosition dropPositionOut)
        {
            // Make deep copies BEFORE clearing internal state so the caller can use them safely
            draggedPathsOut = new List<int[]>();
            if (draggedPaths != null)
            {
                foreach (var p in draggedPaths)
                {
                    if (p == null) continue;
                    var copy = new int[p.Length];
                    Array.Copy(p, copy, p.Length);
                    draggedPathsOut.Add(copy);
                }
            }
            draggedItemsOut = new List<Object>(draggedItems);
            dropTargetPathOut = dropTargetPath;
            dropPositionOut = dropPosition;

            // Basic validity: must be dragging and have a target position
            bool wasValid = isDragging && dropPosition != DropPosition.None && dropTargetPath != null;

            // Additional safety: ensure copied lists actually contain items (external drags use draggedItems only)
            if (!isExternalDrag && (draggedPathsOut == null || draggedPathsOut.Count == 0))
                wasValid = false;
            if (draggedItemsOut == null || draggedItemsOut.Count == 0)
                wasValid = false;

            // Final validation using current collections/root: ensure target parent can accept the first item.
            // RootAfterAll is validated by DropItemsAtRootLevel — skip type-check here.
            if (!wasValid)
                return false;

            if (dropPositionOut != DropPosition.RootAfterAll)
            {
                try
                {
                    var targetParent = GetTargetParentForDrop(dropTargetPathOut, dropPositionOut);
                    if (targetParent == null)
                    {
                        wasValid = false;
                    }
                    else
                    {
                        if (draggedItemsOut.Count > 0)
                        {
                            bool canAccept = targetParent.CanAcceptChild(draggedItemsOut[0]);
                            if (!canAccept)
                                wasValid = false;
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    wasValid = false;
                }
            }

            // Do NOT clear internal state here. Let the caller cancel the drag (so callers have control and
            // we avoid surprising side-effects mid-flow). The caller should call CancelDrag() after handling
            // the returned data (or immediately if the drop was invalid).
            return wasValid;
        }

        /// <summary>
        /// Cancels the current drag operation
        /// </summary>
        public void CancelDrag()
        {
            isDragging = false;
            isExternalDrag = false;
            isPotentialDrag = false;
            hasUnsavedSceneObjects = false;
            draggedPaths.Clear();
            draggedItems.Clear();
            dropTargetPath = null;
            dropPosition = DropPosition.None;
            collections = null;
        }

        /// <summary>
        /// Draws a floating preview of dragged items.
        /// <param name="windowMousePos">
        /// Mouse position in window (GUI) coordinates, captured OUTSIDE the scroll view.
        /// Passing it explicitly avoids the coordinate-space mismatch that occurs when
        /// the position is read from inside a GUILayout.BeginScrollView block,
        /// where Event.mousePosition is in scroll-view-local space rather than window space.
        /// </param>
        /// </summary>
        public void DrawFloatingPreview(Vector2 windowMousePos)
        {
            if (!isDragging || draggedItems.Count == 0)
                return;

            int displayCount = Mathf.Min(draggedItems.Count, 3);
            float previewHeight = displayCount * 20f;
            Rect previewRect = new Rect(windowMousePos.x + 10, windowMousePos.y + 10, 200, previewHeight);

            // Draw background
            EditorGUI.DrawRect(previewRect, new Color(0.2f, 0.2f, 0.2f, 0.9f));

            // Draw border
            Rect borderRect = new Rect(previewRect.x - 1, previewRect.y - 1, previewRect.width + 2,
                previewRect.height + 2);
            EditorGUI.DrawRect(borderRect, new Color(0.5f, 0.5f, 0.5f, 1f));
            EditorGUI.DrawRect(previewRect, new Color(0.2f, 0.2f, 0.2f, 0.95f));

            // Draw item names
            GUIStyle labelStyle = new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = Color.white },
                fontSize = 11
            };

            for (int i = 0; i < displayCount; i++)
            {
                Rect labelRect = new Rect(previewRect.x + 5, previewRect.y + i * 20 + 2, previewRect.width - 10, 18);
                GUI.Label(labelRect, draggedItems[i].name, labelStyle);
            }

            if (draggedItems.Count > 3)
            {
                Rect moreRect = new Rect(previewRect.x + 5, previewRect.y + 60, previewRect.width - 10, 18);
                GUI.Label(moreRect, $"... and {draggedItems.Count - 3} more", labelStyle);
            }
        }

        /// <summary>
        /// Draws a horizontal line indicator at the drop target position
        /// </summary>
        public void DrawDropIndicator(Rect targetRect, float startX)
        {
            if (!isDragging || dropPosition == DropPosition.None || dropTargetPath == null)
                return;

            Rect indicatorRect;
            Color indicatorColor = new Color(0.3f, 0.6f, 1f, 0.8f);
            float lineX = Mathf.Max(startX, targetRect.x); // Ensure we don't go left of the row
            float lineWidth = targetRect.xMax - lineX - 13f;

            switch (dropPosition)
            {
                case DropPosition.Before:
                    // Draw line at top of rect, starting at indented x
                    indicatorRect = new Rect(lineX, targetRect.y - 1, lineWidth, 2);
                    EditorGUI.DrawRect(indicatorRect, indicatorColor);
                    break;
                case DropPosition.After:
                    // Draw line at bottom of rect, starting at indented x
                    indicatorRect = new Rect(lineX, targetRect.y + targetRect.height - 1, lineWidth, 2);
                    EditorGUI.DrawRect(indicatorRect, indicatorColor);
                    break;
                case DropPosition.Inside:
                    // Draw highlight around entire rect
                    EditorGUI.DrawRect(targetRect, new Color(0.3f, 0.6f, 1f, 0.15f));
                    // Draw border (full width for clarity)
                    Rect borderRect = new Rect(targetRect.x, targetRect.y, targetRect.width, 1);
                    EditorGUI.DrawRect(borderRect, indicatorColor);
                    borderRect.y = targetRect.y + targetRect.height - 1;
                    EditorGUI.DrawRect(borderRect, indicatorColor);
                    break;
            }
        }

        /// <summary>
        /// Draws the drop indicator for RootAfterAll: a blue line at the given Y position.
        /// </summary>
        public void DrawRootLevelDropIndicator(float indicatorY, float startX, float width)
        {
            if (!isDragging || dropPosition != DropPosition.RootAfterAll) return;
            Color indicatorColor = new Color(0.3f, 0.6f, 1f, 0.8f);
            EditorGUI.DrawRect(new Rect(startX, indicatorY - 1, width, 2), indicatorColor);
        }

        /// <summary>
        /// Checks if the drop target is invalid (dropping onto self or descendant)
        /// </summary>
        bool IsDropTargetInvalid(int[] targetPath)
        {
            if (targetPath == null)
                return true;

            // External drags don't have source paths, so they can't drop on self/descendants
            if (isExternalDrag)
                return false;

            foreach (var draggedPath in draggedPaths)
            {
                // Can't drop onto self
                if (draggedPath.SequenceEqual(targetPath))
                    return true;

                // Can't drop onto a descendant of dragged item
                if (IsDescendant(targetPath, draggedPath))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Checks if potentialDescendant is a descendant of ancestor
        /// </summary>
        bool IsDescendant(int[] potentialDescendant, int[] ancestor)
        {
            if (potentialDescendant.Length <= ancestor.Length)
                return false;

            // Check if potentialDescendant starts with ancestor path
            for (int i = 0; i < ancestor.Length; i++)
            {
                if (potentialDescendant[i] != ancestor[i])
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Normalizes the drop position to avoid duplicate indicators and invalid positions.
        /// Converts "After item[i]" to "Before item[i+1]" when a next sibling exists.
        /// This ensures only ONE indicator per insertion point.
        /// </summary>
        void NormalizeDropPosition(int[] hoveredPath, DropPosition preliminaryPosition,
            out int[] normalizedPath, out DropPosition normalizedPosition)
        {
            // Default: use the preliminary values
            normalizedPath = hoveredPath;
            normalizedPosition = preliminaryPosition;

            // Only normalize Before/After positions (not Inside)
            if (preliminaryPosition != DropPosition.Before && preliminaryPosition != DropPosition.After)
                return;

            // Strategy: Always prefer "Before" representation
            // Convert "After item[i]" to "Before item[i+1]" if possible
            if (preliminaryPosition == DropPosition.After)
            {
                // Try to convert "After item[i]" to "Before item[i+1]"
                // We need to check if there's a next sibling
                if (TryGetNextSibling(hoveredPath, out int[] nextSiblingPath))
                {
                    // Next sibling exists, use "Before next sibling" instead
                    normalizedPath = nextSiblingPath;
                    normalizedPosition = DropPosition.Before;
                }
                else
                {
                    // No next sibling, keep as "After" (this is at the end of the list)
                    normalizedPosition = DropPosition.After;
                }
            }
        }

        /// <summary>
        /// Tries to get the path to the next sibling of the given node.
        /// Returns true if a next sibling exists.
        /// </summary>
        bool TryGetNextSibling(int[] path, out int[] nextSiblingPath)
        {
            nextSiblingPath = null;

            if (path == null || path.Length == 0 || collections == null)
                return false;

            // Get the parent to check sibling count
            int currentIndex = path[^1];
            if (path.Length == 1)
            {
                // Top-level item, parent is root/collections
                if (currentIndex + 1 < collections.Count)
                {
                    nextSiblingPath = new int[] { currentIndex + 1 };
                    return true;
                }

                return false;
            }

            // Get parent node
            int[] parentPath = new int[path.Length - 1];
            Array.Copy(path, parentPath, parentPath.Length);
            var parent = StructureUtils.GetNodeAtPath(parentPath, collections) as IStructure;

            if (parent == null)
                return false;

            // Check if next sibling exists
            if (currentIndex + 1 < parent.ChildrenObjects.Count)
            {
                // Build path to next sibling
                nextSiblingPath = new int[path.Length];
                Array.Copy(path, nextSiblingPath, path.Length);
                nextSiblingPath[^1] = currentIndex + 1;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Checks if the drop position would result in no actual movement.
        /// This prevents showing indicators when dropping items where they already are.
        /// </summary>
        bool IsDropPositionRedundant(int[] targetPath, DropPosition position)
        {
            // External drags are never redundant (they're adding new items)
            if (isExternalDrag)
                return false;

            if (draggedPaths == null || draggedPaths.Count == 0)
                return false;

            // Check if any dragged item is adjacent to the drop position
            foreach (var draggedPath in draggedPaths)
            {
                // Check if dropping "After" an item that's immediately before our dragged item
                if (position == DropPosition.After)
                {
                    // If target is our immediate previous sibling, this is redundant
                    if (StructureUtils.AreSiblings(draggedPath, targetPath))
                    {
                        int draggedIndex = draggedPath[^1];
                        int targetIndex = targetPath[^1];

                        // Dropping "After target[i]" when dragged is at [i+1] is redundant
                        if (targetIndex == draggedIndex - 1)
                            return true;
                    }
                }

                // Check if dropping "Before" an item that's immediately after our dragged item
                if (position == DropPosition.Before)
                {
                    // If target is our immediate next sibling, this is redundant
                    if (StructureUtils.AreSiblings(draggedPath, targetPath))
                    {
                        int draggedIndex = draggedPath[^1];
                        int targetIndex = targetPath[^1];

                        // Dropping "Before target[i]" when dragged is at [i-1] is redundant
                        if (targetIndex == draggedIndex + 1)
                            return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the parent structure that would receive dropped items based on drop position.
        /// Returns null if unable to determine parent.
        /// </summary>
        IStructure GetTargetParentForDrop(int[] targetPath, DropPosition position)
        {
            if (targetPath == null || collections == null)
                return null;

            if (position == DropPosition.Inside)
            {
                // Drop inside: target itself is the parent
                return StructureUtils.GetNodeAtPath(targetPath, collections) as IStructure;
            }

            if (position != DropPosition.Before && position != DropPosition.After)
                return null;

            // Drop before/after: target's parent is the parent
            if (targetPath.Length == 1)
            {
                // Top-level: return the root
                return Root;
            }

            // Get parent of target
            int[] parentPath = new int[targetPath.Length - 1];
            Array.Copy(targetPath, parentPath, parentPath.Length);
            return StructureUtils.GetNodeAtPath(parentPath, collections) as IStructure;
        }
    }
}
