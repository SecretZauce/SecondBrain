using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEditor;

namespace SecretZauce.SecondBrain.Editor
{
    /// <summary>
    /// Handles keyboard input processing for the Tree View.
    /// Manages navigation and expansion/collapse operations via keyboard.
    /// </summary>
    public class TreeViewKeyInput
    {
        readonly FoldoutState foldoutState;
        readonly List<IStructure> collections;
        readonly TreeView treeView;

        public TreeViewKeyInput(TreeView treeView, FoldoutState foldoutState, List<IStructure> collections)
        {
            this.treeView = treeView;
            this.foldoutState = foldoutState;
            this.collections = collections;
        }

        /// <summary>
        /// Processes global keyboard event decisions (shortcuts + navigation) and returns a result object
        /// describing what action should be taken by the caller (BrowserWindow).
        /// This method does not invoke controller-side actions; it only decides and updates the primaryPath
        /// when navigation occurs.
        /// </summary>
        public KeyInputResult ProcessGlobalKeyEvent(List<int[]> allPaths, ref int[] primaryPath,
            Func<Object, bool> searchPredicate = null)
        {
            var result = new KeyInputResult();
            if (Event.current == null || Event.current.type != EventType.KeyDown)
                return result;

            var key = Event.current.keyCode;
            bool ctrlOrCmd = Event.current.control || Event.current.command;

            // ── Header-rename is active — mirror ItemRenamer blocking behaviour ────────
            // When the header text-field is open, we intercept Escape here (earliest point
            // in the frame, before any IMGUI control can consume it) and block every other
            // shortcut so the user can type freely — exactly what Renamer.IsRenamingAny
            // does for tree-item rename sessions.
            if (treeView.IsRenamingHeader)
            {
                if (key == KeyCode.Escape)
                {
                    treeView.CancelHeaderRename();
                    GUIEventUtils.OnRenameDone(true); 
                    result.Handled = true;
                    result.NeedsRepaint = true;
                }
                // All other keys: return without consuming — the header text-field (drawn
                // later in DrawHeader) handles Enter, character input, and click-outside.
                return result;
            }

            // ── Ghost creation is active ──────────────────────────────────────────────
            // Block all tree-view shortcuts while the ghost input row is visible.
            // Escape is already intercepted at the top of TreeView.Draw() so just return here.
            if (treeView.HasGhostSession)
                return result;

            // If the search bar cursor is active (or any IMGUI text field is focused),
            // do not process tree-global shortcuts/navigation so text input keeps
            // all keystrokes and tree actions (e.g. Delete/Backspace) do not trigger.
            if (treeView.IsSearchBarFocused() || EditorGUIUtility.editingTextField)
            {
                return result;
            }
            
            // Enter / Return handling - enter leaf node OR expand container (hovered item has priority)
            // This needs to work even when searching, so it's outside the IsSearching check
            if (!ctrlOrCmd && !EditorGUIUtility.editingTextField &&
                (key == KeyCode.Return || key == KeyCode.KeypadEnter))
            {
                // Check for hovered path first (priority)
                int[] targetPath = null;
                if (treeView.DragInput is {HasHover: true})
                {
                    targetPath = treeView.DragInput.HoveredPath;
                }
                // Fall back to selected path if no hover
                else if (primaryPath is {Length: > 0})
                {
                    targetPath = primaryPath;
                }

                if (targetPath != null)
                {
                    // Get the actual object to determine if it's a container or leaf node
                    var targetObj = treeView.GetObjectAtPath(targetPath);

                    // Check if it's a container (IStructure and not Base)
                    bool isContainer = targetObj is IStructure and not Base;

                    // In popup mode, Enter on a Base navigates into it without closing the popup
                    if (targetObj is Base && treeView.OwnerWindow is { IsPopup: true })
                    {
                        result.Handled = true;
                        result.NavigateIntoBaseRequested = true;
                        result.NavigateIntoBasePath = targetPath;
                        result.NeedsRepaint = true;
                        Event.current.Use();
                        return result;
                    }

                    // For containers: let Return fall through to expand/collapse logic (don't return early)
                    // For leaf nodes: check if they have an enter action and trigger it
                    if (!isContainer && LeafNodeActionHelper.HasEnterAction(targetObj))
                    {
                        result.Handled = true;
                        result.EnterRequested = true;
                        result.EnterTargetPath = targetPath;
                        result.NeedsRepaint = true;
                        Event.current.Use();
                        return result;
                    }

                    // Non-popup browser: Enter on a leaf that has no dedicated enter action
                    // (e.g. a loaded SceneComponentRef/SceneObjectRef) → open its property editor.
                    if (!isContainer && targetObj != null && !treeView.OwnerWindow.IsPopup)
                    {
                        result.Handled = true;
                        result.OpenPropertyEditorRequested = true;
                        result.OpenPropertyEditorTargetPath = targetPath;
                        result.NeedsRepaint = true;
                        Event.current.Use();
                        return result;
                    }
                    // If it's a container, do NOT return - let execution continue to ProcessInputs
                    // which will handle KeyCode.Return for foldout expansion
                }
            }
            
            if (!treeView.IsSearching() && !treeView.Renamer.IsRenamingAny)
            {
                // Ctrl+Left/Right for browser back/forward navigation (before other shortcuts)
                if (ctrlOrCmd && key == KeyCode.LeftArrow)
                {
                    // Browser back requested
                    result.Handled = true;
                    result.BackRequested = true;
                    result.NeedsRepaint = true;
                    Event.current.Use();
                    return result;
                }

                if (ctrlOrCmd && key == KeyCode.RightArrow)
                {
                    // Browser forward requested
                    result.Handled = true;
                    result.ForwardRequested = true;
                    result.NeedsRepaint = true;
                    Event.current.Use();
                    return result;
                }

                // Global shortcuts handled here (decision only)
                if (ctrlOrCmd && key == KeyCode.D)
                {
                    // Duplicate requested (BrowserWindow will call Controller)
                    result.Handled = true;
                    result.DuplicateRequested = true;
                    result.NeedsRepaint = true;
                    Event.current.Use();
                    return result;
                }

                if (ctrlOrCmd && key == KeyCode.A)
                {
                    // Select All requested
                    result.Handled = true;
                    result.SelectAllRequested = true;
                    result.NeedsRepaint = true;
                    Event.current.Use();
                    return result;
                }

                if (ctrlOrCmd && key == KeyCode.R)
                {
                    // Rename primary selected item
                    result.Handled = true;
                    result.RenameRequested = true;
                    result.NeedsRepaint = true;
                    Event.current.Use();
                    return result;
                }

                if (ctrlOrCmd && key == KeyCode.V)
                {
                    // Paste clipboard text as TextAsset child of selected Container
                    result.Handled = true;
                    result.PasteRequested = true;
                    result.NeedsRepaint = true;
                    Event.current.Use();
                    return result;
                }

                // ESC — go home (navigate back to the home root)
                if (!ctrlOrCmd && !EditorGUIUtility.editingTextField && key == KeyCode.Escape)
                {
                    result.Handled = true;
                    result.GoHomeRequested = true;
                    result.NeedsRepaint = true;
                    Event.current.Use();
                    return result;
                }

                // Delete / Backspace handling - request deletion (handle only when no text field has focus)
                if (!ctrlOrCmd && !EditorGUIUtility.editingTextField &&
                    (key == KeyCode.Delete || key == KeyCode.Backspace))
                {
                    result.Handled = true;
                    result.DeleteRequested = true;
                    result.NeedsRepaint = true;
                    Event.current.Use();
                    return result;
                }
            }

            bool needsRepaint;
            bool handled;

            // When multiple items selected, prefer multi-select navigation/expand handling
            if (allPaths is { Count: > 1 })
            {
                handled = ProcessInputsMultiSelect(allPaths, ref primaryPath, out needsRepaint, searchPredicate);
                result.Handled = handled;
                result.NeedsRepaint = needsRepaint;
                // Left/Right/Return multi-select toggles foldouts only; do not change selection
                if (handled && (key == KeyCode.LeftArrow || key == KeyCode.RightArrow || key == KeyCode.Return || key == KeyCode.KeypadEnter))
                    result.FoldoutToggled = true;
                // Up/Down moves selection to item above topmost or below bottommost selection
                if (handled && key is KeyCode.UpArrow or KeyCode.DownArrow)
                {
                    result.SelectionChanged = true;
                    result.NewPrimaryPath = primaryPath;
                }
                return result;
            }

            // Single-selection navigation/left-right handling
            handled = ProcessInputs(ref primaryPath, out needsRepaint, searchPredicate);
            if (handled)
            {
                result.Handled = true;
                result.NeedsRepaint = needsRepaint;
                // Up/Down change the selection primary path
                if (key is KeyCode.UpArrow or KeyCode.DownArrow)
                {
                    result.SelectionChanged = true;
                    result.NewPrimaryPath = primaryPath;
                }

                // Single-selection left/right/return toggles foldout - inform caller for undo recording
                if (key is KeyCode.LeftArrow or KeyCode.RightArrow or KeyCode.Return or KeyCode.KeypadEnter)
                    result.FoldoutToggled = true;
            }

            return result;
        }

        /// <summary>
        /// Processes keyboard input for navigation.
        /// Returns true if the selection was changed and needs to be updated.
        /// </summary>
        bool ProcessInputs(
            ref int[] selectedPath,
            out bool needsRepaint,
            Func<Object, bool> searchPredicate = null)
        {
            needsRepaint = false;
            if (Event.current == null || Event.current.type != EventType.KeyDown)
                return false;

            var key = Event.current.keyCode;
#if SECOND_BRAIN_PRO
            if (treeView.OwnerWindow.IsPopup && (key is KeyCode.Return or KeyCode.KeypadEnter))
            {
                var window = treeView.OwnerWindow;
                window.OpenPropertyEditorFor(selectedPath, window);
                Event.current.Use();
                // Defer close so serializedDatabase stays valid through the rest of OnGUI
                var win = window;
                EditorApplication.delayCall += () => { try { win?.TryCloseIfPopup(); } catch { } };
                needsRepaint = true;
                return true;
            }
#endif
            
            if (key is KeyCode.UpArrow or KeyCode.DownArrow)
            {
                // Wrap the Func into ISearchPredicate for consistency with new APIs
                ISearchPredicate predicate = searchPredicate != null ? new FuncSearchPredicate(searchPredicate) : null;
                Func<Object, bool> matchFunc = predicate != null ? new Func<Object, bool>(predicate.Matches) : null;
                return ProcessUpDownNavigation(ref selectedPath, key, out needsRepaint, matchFunc);
            }

            if (key is KeyCode.RightArrow or KeyCode.LeftArrow or KeyCode.Return or KeyCode.KeypadEnter)
                return ProcessLeftRightNavigation(ref selectedPath, key, out needsRepaint);

            return false;
        }

        /// <summary>
        /// Processes keyboard input for multi-selection navigation.
        /// </summary>
        bool ProcessInputsMultiSelect(
            List<int[]> selectedPaths,
            ref int[] primaryPath,
            out bool needsRepaint,
            Func<Object, bool> searchPredicate = null)
        {
            needsRepaint = false;
            if (Event.current == null || Event.current.type != EventType.KeyDown)
                return false;

            var key = Event.current.keyCode;

            // Up/Down: navigate to item above topmost or below bottommost selection, then collapse to single
            if (key is KeyCode.UpArrow or KeyCode.DownArrow)
            {
                ISearchPredicate predicate = searchPredicate != null ? new FuncSearchPredicate(searchPredicate) : null;
                Func<Object, bool> matchFunc = predicate != null ? new Func<Object, bool>(predicate.Matches) : null;
                var anchor = FindMultiSelectAnchorPath(selectedPaths, key, matchFunc);
                if (anchor != null)
                    primaryPath = anchor;
                return ProcessUpDownNavigation(ref primaryPath, key, out needsRepaint, matchFunc);
            }

            // Left/Right/Return - expand/collapse all selected items
            if (key is KeyCode.RightArrow or KeyCode.LeftArrow or KeyCode.Return or KeyCode.KeypadEnter)
                return ProcessLeftRightNavigationMulti(selectedPaths, key, out needsRepaint);

            return false;
        }

        // Returns the path that should anchor Up/Down navigation for a multi-selection:
        // the topmost visible item for Up, the bottommost for Down.
        int[] FindMultiSelectAnchorPath(List<int[]> selectedPaths, KeyCode key, Func<Object, bool> searchPredicate)
        {
            List<int[]> visible;
            if (!treeView.IsSearching() && treeView.visiblePaths != null && treeView.visiblePaths.Count > 0)
                visible = treeView.visiblePaths;
            else
                visible = GenerateVisiblePaths(searchPredicate);

            int boundaryIndex = key == KeyCode.UpArrow ? int.MaxValue : int.MinValue;
            int[] boundaryPath = null;

            for (int vi = 0; vi < visible.Count; vi++)
            {
                foreach (var sp in selectedPaths)
                {
                    if (!StructureUtils.ArePathsEqual(visible[vi], sp))
                        continue;

                    if (key == KeyCode.UpArrow ? vi < boundaryIndex : vi > boundaryIndex)
                    {
                        boundaryIndex = vi;
                        boundaryPath = visible[vi];
                    }
                    break;
                }
            }

            return boundaryPath;
        }

        bool ProcessLeftRightNavigation(
            ref int[] selectedPath,
            KeyCode key,
            out bool needsRepaint)
        {
            needsRepaint = false;
            var node = StructureUtils.GetNodeAtPath(selectedPath, collections);
            if (node == null)
                return false;

            bool prev = foldoutState.Get(node);
            bool newValue = key is KeyCode.RightArrow or KeyCode.Return or KeyCode.KeypadEnter;

            // Check for Alt/Option modifier key for recursive expand/collapse
            bool isRecursive = Event.current.alt;

            if (isRecursive)
            {
                // Recursively expand/collapse all children
                foldoutState.SetRecursive(node, newValue);
            }
            else
            {
                // Normal single-level expand/collapse
                if (key == KeyCode.RightArrow || key == KeyCode.Return || key == KeyCode.KeypadEnter)
                    foldoutState.Set(node, true);
                else if (key == KeyCode.LeftArrow)
                    foldoutState.Set(node, false);
            }

            if (foldoutState.Get(node) == prev)
                return false;

            Event.current.Use();
            needsRepaint = true;
            return true;
        }

        bool ProcessLeftRightNavigationMulti(
            List<int[]> selectedPaths,
            KeyCode key,
            out bool needsRepaint)
        {
            needsRepaint = false;
            var handled = false;

            if (selectedPaths == null || selectedPaths.Count == 0)
                return false;

            bool newValue = key == KeyCode.RightArrow ||  key == KeyCode.Return || key == KeyCode.KeypadEnter;
            bool isRecursive = Event.current.alt;

            // Expand/collapse all selected IStructure nodes
            foreach (var path in selectedPaths)
            {
                var node = StructureUtils.GetNodeAtPath(path, collections);
                if (node != null && node is IStructure and not Base)
                {
                    bool prev = foldoutState.Get(node);

                    if (isRecursive)
                    {
                        // Recursively expand/collapse all children
                        foldoutState.SetRecursive(node, newValue);
                    }
                    else
                    {
                        switch (key)
                        {
                            // Normal single-level expand/collapse
                            case KeyCode.RightArrow:
                            case KeyCode.Return:
                            case KeyCode.KeypadEnter:
                                foldoutState.Set(node, true);
                                break;
                            case KeyCode.LeftArrow:
                                foldoutState.Set(node, false);
                                break;
                            default:
                                throw new ArgumentOutOfRangeException(nameof(key), key, null);
                        }
                    }

                    if (foldoutState.Get(node) != prev)
                    {
                        handled = true;
                        needsRepaint = true;
                    }
                }
            }

            if (handled)
                Event.current.Use();

            return handled;
        }

        bool ProcessUpDownNavigation(
            ref int[] selectedPath,
            KeyCode key,
            out bool needsRepaint,
            Func<Object, bool> searchPredicate)
        {
            needsRepaint = false;

            // Safety check
            if (collections == null || collections.Count == 0)
                return false;

            // Prefer the visible-path list that was already built by the last Repaint draw
            // pass.  This avoids a duplicate full-tree traversal on every Up/Down key press.
            // We fall back to generating a fresh list when:
            //   • The tree hasn't been drawn yet (visiblePaths is empty), OR
            //   • A search is active (the draw-pass list includes container parents of
            //     matching children which differ from what the search predicate alone would
            //     produce, so we use the dedicated generator for consistency with the
            //     existing search-navigation behaviour).
            List<int[]> visible;
            if (!treeView.IsSearching() && treeView.visiblePaths != null && treeView.visiblePaths.Count > 0)
            {
                visible = treeView.visiblePaths;
            }
            else
            {
                visible = GenerateVisiblePaths(searchPredicate);
            }

            if (visible.Count <= 0)
                return false;

            var newIndex = FindValidCurrentIndexFromVisibleList(key, visible, selectedPath);
            selectedPath = visible[newIndex];
            GUIUtility.keyboardControl = 0;
            Event.current.Use();
            needsRepaint = true;
            return true;
        }

        List<int[]> GenerateVisiblePaths(Func<Object, bool> searchPredicate)
        {
            var visible = new List<int[]>();
            int colCount = collections.Count;
            for (int i = 0; i < colCount; i++)
            {
                var colObj = collections[i];
                // Explicitly cast IStructure to Object (they must be Unity Objects)
                Object objNode = colObj as Object;
                if (objNode != null)
                {
                    AddVisibleRecursive(visible, new List<int> { i }, objNode, searchPredicate);
                }
            }

            return visible;
        }

        void AddVisibleRecursive(List<int[]> visible, List<int> path, Object node, Func<Object, bool> searchPredicate)
        {
            if (node == null) return;
            if (searchPredicate == null || searchPredicate(node))
                visible.Add(path.ToArray());

            if (!foldoutState.Get(node)) return;
            var structure = node as IStructure;
            if (structure?.ChildrenObjects == null || structure is Base)
                return;

            var childrenObjects = structure.ChildrenObjects;
            for (int c = 0; c < childrenObjects.Count; c++)
            {
                var child = childrenObjects[c];
                var newPath = new List<int>(path) { c };
                if (child is IStructure childIStructure and not Base)
                {
                    AddVisibleRecursive(visible, newPath, childIStructure as Object, searchPredicate);
                }
                else if (child != null && (searchPredicate == null || searchPredicate(child)))
                    visible.Add(newPath.ToArray());
            }
        }

        static int FindValidCurrentIndexFromVisibleList(
            KeyCode key,
            List<int[]> visible,
            int[] selectedPath)
        {
            int curIndex = -1;
            for (int vi = 0; vi < visible.Count; vi++)
            {
                var t = visible[vi];
                if (StructureUtils.ArePathsEqual(t, selectedPath))
                {
                    curIndex = vi;
                    break;
                }
            }

            int newIndex;
            if (curIndex == -1)
            {
                newIndex = (key == KeyCode.DownArrow) ? 0 : visible.Count - 1;
            }
            else
            {
                newIndex = key == KeyCode.DownArrow
                    ? Math.Min(visible.Count - 1, curIndex + 1)
                    : Math.Max(0, curIndex - 1);
            }

            return newIndex;
        }
    }
}
