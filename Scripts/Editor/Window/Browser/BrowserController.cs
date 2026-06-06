using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SecretZauce.SecondBrain.Editor
{
    /// <summary>
    /// Controller that centralizes non-UI state and actions for a BrowserWindow.
    /// Keeps serialized database, collections, selection state and lifecycle helpers.
    /// The UI (BrowserWindow) should instantiate this and use the public properties
    /// and methods to drive rendering and keyboard logic.
    /// </summary>
    public class BrowserController : IDisposable
    {
        public SerializedObject SerializedDatabase { get; private set; }
        public List<IStructure> Collections { get; private set; }
        public SelectionStateSO SelectionState => selectionState;
        public Dictionary<object, bool> LastFoldoutSnapshot { get; private set; }
        
        public event Action OnStructureChanged;
        public event Action<int[]> OnSelectionRequested;
        public event Action<Dictionary<object, bool>> OnFoldoutStateRestoreRequested;
        public event Action<int[]> OnExpansionRequested;

        StructureLifecycleManager LifecycleManager { get; }
        SelectionStateSO selectionState;
        public IStructure Root => hostWindow.Root;

        public UndoHelper UndoHelper => undoHelper;
        readonly UndoHelper undoHelper;

        public BrowserWindow HostWindow => hostWindow;
        readonly BrowserWindow hostWindow;
        
        // Track the last unity objects we set so we can distinguish programmatic selection changes
        // from user/external selection changes without relying on delayed resets or unsubscribing handlers.
        HashSet<Object> expectedUnitySelection;

        public BrowserController(BrowserWindow window, UndoHelper undoHelper, SelectionStateSO existingSelectionState = null)
        {
            this.undoHelper = undoHelper ?? new UndoHelper(Root);
            hostWindow = window;

            // If an existing selection state is provided (preserved across reinit), reuse it.
            if (existingSelectionState != null)
                selectionState = existingSelectionState;

            LifecycleManager = new StructureLifecycleManager(window, this.undoHelper);
            LifecycleManager.OnStructureChanged += () => {
                // Refresh serialized database and collections from root so consumers see fresh data
                RefreshFromRoot();
                OnStructureChanged?.Invoke();
            };
            LifecycleManager.OnSelectionRequested += (path) => OnSelectionRequested?.Invoke(path);
            LifecycleManager.OnFoldoutStateRestoreRequested += (snapshot) => OnFoldoutStateRestoreRequested?.Invoke(snapshot);
            // When the lifecycle manager requests an expansion (e.g. after creating a container),
            // mark the node as expanded in our LastFoldoutSnapshot so any delayed refresh that
            // reapplies foldouts will keep the newly-created container expanded.
            LifecycleManager.OnExpansionRequested += (path) =>
            {
                if (path != null)
                {
                    try
                    {
                        var node = StructureUtils.GetNodeAtPath(path, Collections);
                        if (node is IStructure)
                        {
                            if (LastFoldoutSnapshot == null)
                                LastFoldoutSnapshot = new Dictionary<object, bool>();
                            LastFoldoutSnapshot[node] = true;
                        }
                    }
                    catch
                    {
                        // Ignore path resolution errors — best-effort only
                    }
                }

                OnExpansionRequested?.Invoke(path);
            };

            EnsureSelectionStateSO();
            SelectionUtils.RegisterOnSelectionChange(OnUnitySelectionChanged);
        }

        void RefreshFromRoot()
        {
            var rootObject = Root as Object;
            if (rootObject == null)
            {
                SerializedDatabase = null;
                Collections = null;
                return;
            }

            SerializedDatabase = new SerializedObject(rootObject);
            Collections = Root.ChildrenObjects.Select(c => c as IStructure).Where(c => c != null).ToList();
        }

        void EnsureSelectionStateSO()
        {
            if (selectionState != null) 
                return;

            selectionState = ScriptableObject.CreateInstance<SelectionStateSO>();
            selectionState.hideFlags = HideFlags.DontSave;
        }

        /// <summary>
        /// Reads the provided root, builds a SerializedObject and a live list of IStructure collections.
        /// Does not create or manage TreeView (UI owns that).
        /// </summary>
        public void Initialize()
        {
            EnsureSelectionStateSO();
            RefreshFromRoot();
        }

        public void RestoreSelection()
        {
            // Safety check: ensure selection state exists
            if (selectionState == null)
            {
                EnsureSelectionStateSO();
                if (selectionState == null)
                {
                    // Still null - can't restore, just repaint
                    hostWindow?.Repaint();
                    return;
                }
            }
            
            var undoneWindow = WindowFocusManager.GetCurrentWindow();
          
            // Only update Unity selection if this window is the one that should be active
            // This prevents multiple windows from fighting over Unity's selection
            // Also check that undoneWindow still exists (it might have been closed)
            if (undoneWindow != null && undoneWindow == hostWindow)
            {
                UpdateUnitySelection(selectionState.GetAllPaths());
            }
            
            // Always repaint THIS window to show its own selection state
            // This ensures that when undo happens, ALL browser windows update their highlights correctly
            hostWindow?.Repaint();
            
            // Also repaint the undone window if it exists and is different from this window
            // This handles the case where we're restoring focus to a different window
            // Check if window still exists before trying to repaint it
            if (undoneWindow != null && undoneWindow != hostWindow)
            {
                try
                {
                    undoneWindow.Repaint();
                }
                catch
                {
                    // Window might have been destroyed, ignore
                }
            }
        }

        void UpdateUnitySelection(List<int[]> selectedPaths)
        {
            if (selectedPaths == null || selectedPaths.Count == 0)
            {
                expectedUnitySelection = new HashSet<Object>();
                SelectionUtils.SetObjects(Array.Empty<Object>());
                return;
            }

            List<Object> unitySelection = new List<Object>();
            foreach (var path in selectedPaths)
            {
                Object obj = GetObjectAtPath(path);
                if (obj != null)
                {
                    // If the selected object is a SceneObjectRef and its GameObject is live in an
                    // open scene, select the GameObject directly for a better UX.
                    if (obj is SceneObjectRef sceneObjRef && sceneObjRef.sceneObject != null)
                    {
                        var go = SceneObjectMap.Resolve(sceneObjRef.sceneObject.GlobalId);
                        if (go != null)
                            obj = go;
                    }
                    else if (obj is SceneComponentRef sceneComponentRef && sceneComponentRef.sceneComponent != null)
                    {
                        var component = SceneObjectMap.ResolveComponent(sceneComponentRef.sceneComponent.GlobalId);
                        if (component != null)
                            obj = component;
                    }

                    unitySelection.Add(obj);
                }
            }

            // Record the expected selection so the subsequent selectionChanged callback can confirm
            // whether the change originated from this controller or externally.
            expectedUnitySelection = new HashSet<Object>(unitySelection);
            SelectionUtils.SetObjects(unitySelection.Count > 0 ? unitySelection.ToArray() : Array.Empty<Object>());
        }

        public Object GetObjectAtPath(int[] path)
        {
            return StructureUtils.GetNodeAtPath(path, Collections);
        }

        // Called when Unity's Selection.objects changes. If the change was not initiated by the controller
        // clear the browser selection state so the UI reflects an external selection.
        void OnUnitySelectionChanged()
        {
            if (EditorWindow.focusedWindow is BrowserWindow)
            {
                // If the browser window is focused, assume selection changes are intentional and originated from this controller or user interactions within the window.
                // In this case, we do not want to clear the selection state as it may be a valid change initiated by the user.
                return;
            }
            
            // If we previously set Unity's selection, compare the current selection to what we expected.
            // If they match (ignoring order), this callback was triggered by our own UpdateUnitySelection and
            // should be ignored. If they don't match, treat as an external selection and clear browser state.
            var current = new HashSet<Object>(SelectionUtils.objects ?? Array.Empty<Object>());
            if (expectedUnitySelection != null && expectedUnitySelection.SetEquals(current))
            {
                // Reset expectation and ignore
                expectedUnitySelection = null;
                return;
            }

            // Ensure selection state exists
            if (selectionState == null)
                EnsureSelectionStateSO();

            // Clear the browser's selection state without modifying Unity's selection.
            selectionState.ClearSelectionWithoutNotify();
            EditorUtility.SetDirty(selectionState);

            // Request a repaint of the host window so the tree reflects the cleared selection
            hostWindow?.Repaint();
        }

        void Dispose()
        {
            SelectionUtils.UnregisterOnSelectionChange(OnUnitySelectionChanged);
            LifecycleManager?.DisposeMoveHandlers();
            LifecycleManager?.DisposeCreationHandlers();
        }

        public void RecordSelectionChange(string operationName)
        {
            if (selectionState == null)
                EnsureSelectionStateSO();
           
            RecordSelectionAndWindowFocus(operationName);
        }
        
        /// <summary>
        /// Records both selection state and window focus in the same undo group.
        /// This ensures that when selection changes in a different window, both the selection
        /// and the window focus can be undone together.
        /// </summary>
        void RecordSelectionAndWindowFocus(string operationName)
        {
            // Check if window is changing
            var currentWindow = WindowFocusManager.GetCurrentWindow();
            bool windowChanging = currentWindow != hostWindow;
            
            // Increment to start a fresh undo group for this operation
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(operationName);
            
            // Record BOTH objects in the same undo group
            Undo.RecordObject(selectionState, operationName);
            if (windowChanging)
            {
                //Debug.Log("[BrowserController] Window Change: " + hostWindow.GetInstanceID());
                Undo.RecordObject(WindowFocusManager.Instance, operationName);
            }
        }

        public void CaptureFoldoutSnapshot(TreeView treeView)
        {
            LastFoldoutSnapshot = treeView.GetFoldoutSnapshot();
        }

        public void CreateChild(Object parent, TreeView treeView)
        {
#if !SECOND_BRAIN_PRO
            if (IsSecondBaseBlocked(parent)) return;
#endif
            // Capture foldout state before creating
            CaptureFoldoutSnapshot(treeView);
            LifecycleManager.CreateChild(parent, treeView);
        }

        public void CreateChildOfType(Object parent, Type childType, TreeView treeView)
        {
#if !SECOND_BRAIN_PRO
            if (IsSecondBaseBlocked(parent)) return;
#endif
            CaptureFoldoutSnapshot(treeView);
            LifecycleManager.CreateChildOfType(parent, childType, treeView);
        }

        public void CreateChildUsingOption(Object parent, CreateChildOption option, TreeView treeView)
        {
#if !SECOND_BRAIN_PRO
            if (IsSecondBaseBlocked(parent)) return;
#endif
            CaptureFoldoutSnapshot(treeView);
            LifecycleManager.CreateChildUsingOption(parent, option, treeView);
        }

        /// <summary>
        /// Reads the system clipboard and, if it contains text, creates a TextAsset child
        /// under the given parent container. The name is derived from the content (URL → hostname,
        /// plain text → first ~25 chars). Does nothing if the clipboard is empty or the
        /// parent is not a valid container.
        /// </summary>
        public void PasteFromClipboard(Object parent, TreeView treeView)
        {
            string clipboardText = GUIUtility.systemCopyBuffer;
            if (string.IsNullOrEmpty(clipboardText))
                return;

            CaptureFoldoutSnapshot(treeView);
            LifecycleManager.PasteTextAsChild(parent, clipboardText, treeView);
        }

        /// <summary>
        /// Creates a child with a pre-validated, user-supplied name (called from the ghost creation session).
        /// </summary>
        public void CreateChildWithName(Object parent, Type childType, string name, TreeView treeView)
        {
#if !SECOND_BRAIN_PRO
            if (IsSecondBaseBlocked(parent)) return;
#endif
            CaptureFoldoutSnapshot(treeView);
            LifecycleManager.CreateChildWithName(parent, childType, name, treeView);
        }

        /// <summary>
        /// Creates a child using a specific CreateChildOption, applying the provided name.
        /// Used by ghost creation flow when the user picked an explicit option and typed a name.
        /// </summary>
        public void CreateChildUsingOptionWithName(Object parent, CreateChildOption option, string name, TreeView treeView)
        {
#if !SECOND_BRAIN_PRO
            if (IsSecondBaseBlocked(parent)) return;
#endif
            CaptureFoldoutSnapshot(treeView);
            LifecycleManager.CreateChildUsingOptionWithName(parent, option, name, treeView);
        }

        /// <summary>
        /// Try to apply a keyboard-driven selection change.
        /// Validates the candidate path against current Collections and only applies the selection if valid.
        /// If invalid and repaintIfInvalid is true, requests a repaint so UI can reflect foldout changes.
        /// Returns true when the selection was applied, false when ignored.
        /// </summary>
        public bool TryApplySelectionFromKeyboard(int[] candidatePath, TreeView treeView, bool repaintIfInvalid = true)
        {
            if (selectionState == null) EnsureSelectionStateSO();

            // Validate candidate path
            var nodeAtPath = StructureUtils.GetNodeAtPath(candidatePath, Collections);
            if (nodeAtPath == null)
            {
                if (repaintIfInvalid)
                    hostWindow?.Repaint();
                return false;
            }

            // Save foldout state before selection change
            if (treeView != null)
                CaptureFoldoutSnapshot(treeView);
           
            string itemName = nodeAtPath?.name ?? "Item";
            RecordSelectionAndWindowFocus($"Select '{itemName}' (Keyboard)");
            selectionState.SetSingleSelection(hostWindow, candidatePath);
            EditorUtility.SetDirty(selectionState);
            UpdateUnitySelection(selectionState.GetAllPaths());

            return true;
        }

        void DeleteItems(List<int[]> paths, TreeView treeView)
        {
            if (paths == null || paths.Count == 0)
                return;

            // Ask for confirmation if enabled
            if (BrowserSettings.AskBeforeDeletion)
            {
                int total = paths.Count;
                string message = total == 1
                    ? $"Are you sure you want to delete '{GetObjectAtPath(paths[0])?.name}'?"
                    : $"Are you sure you want to delete {total} items?";
                if (!EditorUtility.DisplayDialog("Delete Item(s)", message, "Delete", "Cancel"))
                    return;
            }

            // Capture foldout state
            CaptureFoldoutSnapshot(treeView);
            // Subscribe AFTER the existing relay (constructor) so selectionState is already
            // updated when we call UpdateUnitySelection. Must land before CollapseUndoOperations
            // so the Unity Selection.objects undo record is part of the delete group — otherwise
            // Unity treats it as a separate undo entry requiring a second Ctrl+Z.
            void onSelectionForDelete(int[] _) => UpdateUnitySelection(selectionState.GetAllPaths());
            LifecycleManager.OnSelectionRequested += onSelectionForDelete;
            try { LifecycleManager.DeleteItems(paths, treeView); }
            finally { LifecycleManager.OnSelectionRequested -= onSelectionForDelete; }
        }

        void RemoveItems(List<int[]> paths, TreeView treeView)
        {
            if (paths == null || paths.Count == 0)
                return;

            if (BrowserSettings.AskBeforeRemove)
            {
                int total = paths.Count;
                string message = total == 1
                    ? $"Remove '{GetObjectAtPath(paths[0])?.name}' from list (the object will not be deleted)?"
                    : $"Remove {total} items from list (the objects will not be deleted)?";
                if (!EditorUtility.DisplayDialog("Remove from List", message, "Remove", "Cancel"))
                    return;
            }

            CaptureFoldoutSnapshot(treeView);
            // Same pattern: UpdateUnitySelection inside the undo group via the selection event.
            void onSelectionForRemove(int[] _) => UpdateUnitySelection(selectionState.GetAllPaths());
            LifecycleManager.OnSelectionRequested += onSelectionForRemove;
            try { LifecycleManager.RemoveItems(paths, treeView, selectionState); }
            finally { LifecycleManager.OnSelectionRequested -= onSelectionForRemove; }
        }

        public void ReparentItems(
            List<int[]> itemPaths,
            List<Object> items,
            int[] targetPath,
            DragAndDropManager.DropPosition dropPosition,
            List<IStructure> collections,
            TreeView treeView,
            SelectionStateSO selectionStateSO)
        {
            // Capture foldout state
            CaptureFoldoutSnapshot(treeView);
            // Expand the drop target so dropped items are visible after the operation
            if (dropPosition == DragAndDropManager.DropPosition.Inside)
                ExpandDropTargetInSnapshot(targetPath, collections);
            LifecycleManager.ReparentItems(itemPaths, items, targetPath, dropPosition, collections, treeView, selectionStateSO);
        }

        public void AddExternalItems(
            List<Object> items,
            int[] targetPath,
            DragAndDropManager.DropPosition dropPosition,
            List<IStructure> collections,
            TreeView treeView,
            SelectionStateSO selectionStateSO)
        {
#if !SECOND_BRAIN_PRO
            // Block dragging Base assets into Motherbase when it already has a child (free limit).
            var mb = Motherbase.Home;
            if (mb != null && mb.Children.Count >= 1 && items != null && items.Exists(i => i is Base))
            {
                ProFeatureDialog.Show("Multiple Bases");
                return;
            }
#endif
            // Capture foldout state
            CaptureFoldoutSnapshot(treeView);
            // Expand the drop target so dropped items are visible after the operation
            if (dropPosition == DragAndDropManager.DropPosition.Inside)
                ExpandDropTargetInSnapshot(targetPath, collections);
            LifecycleManager.AddExternalItems(items, targetPath, dropPosition, collections, treeView, selectionStateSO);

            // Keep Unity selection in sync with browser selection after external drops.
            UpdateUnitySelection(selectionStateSO?.GetAllPaths());
        }

        /// <summary>
        /// Ensures the node at <paramref name="targetPath"/> is marked as expanded in
        /// <see cref="LastFoldoutSnapshot"/> so that items dropped inside a collapsed
        /// IStructure become visible immediately after the operation.
        /// </summary>
        void ExpandDropTargetInSnapshot(int[] targetPath, List<IStructure> collections)
        {
            if (LastFoldoutSnapshot == null || targetPath == null || collections == null)
                return;

            var targetNode = StructureUtils.GetNodeAtPath(targetPath, collections);
            if (targetNode is IStructure)
                LastFoldoutSnapshot[targetNode] = true;
        }

        /// <summary>
        /// Creates a new default Container under the current Base root and adds all external items to it.
        /// Used when dragging assets into a Base that currently has no Containers.
        /// </summary>
        public void CreateContainerAndAddExternalItems(
            List<Object> items,
            TreeView treeView,
            SelectionStateSO selectionStateSO)
        {
            CaptureFoldoutSnapshot(treeView);
            LifecycleManager.CreateContainerWithExternalItems(items, treeView, selectionStateSO);
        }

        /// <summary>
        /// Handles a drop onto empty space below all items (RootAfterAll).
        /// Containers are moved to depth-0; leaves are wrapped in a new Container at depth-0.
        /// </summary>
        public void DropItemsAtRootLevel(
            List<int[]> itemPaths,
            List<Object> items,
            List<IStructure> collections,
            TreeView treeView,
            SelectionStateSO selectionStateSO)
        {
            CaptureFoldoutSnapshot(treeView);
            LifecycleManager.DropItemsAtRootLevel(itemPaths, items, collections, treeView, selectionStateSO);
        }

        public void DeleteSelectedItems(TreeView treeView)
        {
            if (selectionState == null) EnsureSelectionStateSO();
            var paths = selectionState.GetAllPaths();
            if (paths == null || paths.Count == 0) return;
            CaptureFoldoutSnapshot(treeView);
            DeleteItems(paths, treeView);
            UpdateUnitySelection(selectionState.GetAllPaths());
        }

        public void RemoveSelectedItems(TreeView treeView)
        {
            if (selectionState == null) EnsureSelectionStateSO();
            var paths = selectionState.GetAllPaths();
            if (paths == null || paths.Count == 0) return;
            CaptureFoldoutSnapshot(treeView);
            RemoveItems(paths, treeView);
            UpdateUnitySelection(selectionState.GetAllPaths());
        }

        public bool HandleItemSelected(int[] path, bool isMultiSelect, bool isRangeSelect, TreeView treeView)
        {
            if (selectionState == null) 
                EnsureSelectionStateSO();

            // Save foldout state before selection change
            if (treeView != null)
                CaptureFoldoutSnapshot(treeView);

            bool selectionChanged = false;
            Object selectedObject = StructureUtils.GetNodeAtPath(path, Collections);
            string itemName = selectedObject?.name ?? "Item";

            if (isRangeSelect)
            {
                var fromPath = selectionState.GetLastSelectedPath();
                var visiblePaths = treeView != null ? new List<int[]>(treeView.visiblePaths) : new List<int[]>();
                RecordSelectionAndWindowFocus($"Select Range to '{itemName}'");
                selectionState.SetRangeSelection(hostWindow, fromPath, path, visiblePaths);
                selectionChanged = true;
            }
            else if (isMultiSelect)
            {
                RecordSelectionAndWindowFocus($"Toggle '{itemName}'");
                selectionState.ToggleSelection(hostWindow, path);
                selectionChanged = true;
            }
            else
            {
                // Single select: always set the selection to just this item, even if it's already selected.
                // This is important when clicking on an item in a multi-selection - we want to collapse
                // the multi-selection down to just this single item.
                RecordSelectionAndWindowFocus($"Select '{itemName}'");
                
                // Check if we actually have a multi-selection that needs to be collapsed
                var allPaths = selectionState.GetAllPaths();
                bool hasMultipleSelection = allPaths != null && allPaths.Count > 1;
                
                // If we have multiple items selected, always collapse to single selection
                // If we only have one item selected, only change if it's different
                if (hasMultipleSelection)
                {
                    selectionState.SetSingleSelection(hostWindow, path);
                    selectionChanged = true;
                }
                else
                {
                    var prev = selectionState.GetPrimaryPath();
                    if (!StructureUtils.ArePathsEqual(prev, path))
                    {
                        selectionState.SetSingleSelection(hostWindow, path);
                        selectionChanged = true;
                    }
                }
            }

            if (!selectionChanged) 
                return false;

            EditorUtility.SetDirty(selectionState);
            UpdateUnitySelection(selectionState.GetAllPaths());
            return true;
        }

        public void DuplicateAndSelect(List<int[]> paths, TreeView treeView, List<IStructure> collections)
        {
            if (paths == null || paths.Count == 0) return;

            // Capture foldout state
            CaptureFoldoutSnapshot(treeView);
            var duplicatedItems = LifecycleManager.DuplicateItems(paths, treeView, collections);
            if (duplicatedItems == null || duplicatedItems.Count == 0)
                return;

            // After duplication, update selection to the duplicated items
            if (selectionState == null)
                EnsureSelectionStateSO();
            
            // Build descriptive operation name with item names
            string operationName;
            if (paths.Count == 1)
            {
                var original = StructureUtils.GetNodeAtPath(paths[0], Collections);
                string itemName = original?.name ?? "Item";
                operationName = $"Duplicate '{itemName}'";
            }
            else
            {
                operationName = $"Duplicate {paths.Count} Items";
            }
            
            RecordSelectionAndWindowFocus(operationName);
            selectionState.ClearSelection(hostWindow);

            foreach (var duplicated in duplicatedItems)
            {
                var newPath = StructureUtils.FindPathToChild(collections, duplicated);
                if (newPath != null)
                {
                    selectionState.AddToSelection(hostWindow, newPath);
                    // Request expansion for UI (controller event handled by BrowserWindow)
                    OnExpansionRequested?.Invoke(newPath);
                }
            }

            EditorUtility.SetDirty(selectionState);
            UpdateUnitySelection(selectionState.GetAllPaths());
        }

        public void SelectAll()
        {
            if (selectionState == null)
                EnsureSelectionStateSO();

            if (Collections == null || Collections.Count == 0)
                return;

            RecordSelectionAndWindowFocus("Select All Items");

            // Clear existing selection
            selectionState.ClearSelectionWithoutNotify();

            // Reuse a single int[] buffer for path construction.
            // Each AddToSelection call receives its own array copy (unavoidable since paths
            // are stored), but we eliminate the per-node List<int> intermediate copies that
            // previously allocated O(depth) extra elements per recursion level.
            const int MaxDepth = 64;
            var pathBuf = new int[MaxDepth];

            for (int i = 0; i < Collections.Count; i++)
            {
                var collection = Collections[i];
                if (collection == null)
                    continue;

                pathBuf[0] = i;
                selectionState.AddToSelection(hostWindow, new[] { i });

                if (collection.ChildrenObjects != null)
                {
                    for (int c = 0; c < collection.ChildrenObjects.Count; c++)
                    {
                        pathBuf[1] = c;
                        var child = collection.ChildrenObjects[c];
                        if (child is IStructure childStruct)
                            BuildPathsRecursively(childStruct, pathBuf, 2);
                        else
                            selectionState.AddToSelection(hostWindow, new[] { i, c });
                    }
                }
            }

            EditorUtility.SetDirty(selectionState);
            UpdateUnitySelection(selectionState.GetAllPaths());
        }

        void BuildPathsRecursively(IStructure node, int[] pathBuf, int depth)
        {
            // Copy the current path and add the node to selection.
            var path = new int[depth];
            Array.Copy(pathBuf, path, depth);
            selectionState.AddToSelection(hostWindow, path);

            if (node.ChildrenObjects == null || depth >= pathBuf.Length) return;

            for (int i = 0; i < node.ChildrenObjects.Count; i++)
            {
                pathBuf[depth] = i;
                var child = node.ChildrenObjects[i];
                if (child is IStructure childStruct)
                {
                    BuildPathsRecursively(childStruct, pathBuf, depth + 1);
                }
                else
                {
                    var leafPath = new int[depth + 1];
                    Array.Copy(pathBuf, leafPath, depth + 1);
                    selectionState.AddToSelection(hostWindow, leafPath);
                }
            }
        }

        public void SetTargetWithUndo(Object newTarget)
        {
            UndoHelper.EnsureFocusHistorySO(Root);
            UndoHelper.RecordFocus(newTarget);
            
            // Record selection state before clearing so undo can restore it
            if (selectionState != null)
            {
                Undo.RecordObject(selectionState, "Change Focus");
                selectionState.ClearSelection(hostWindow);
            }
            
            hostWindow.SetTarget(newTarget as Base);
        }

        /// <summary>
        /// Moves all currently selected items to the specified target Base, removing them from
        /// their current parents in the active Base's hierarchy.
        /// </summary>
        public void MoveSelectedItemsToBase(Base targetBase, TreeView treeView)
        {
            if (targetBase == null) return;
            if (selectionState == null) EnsureSelectionStateSO();
            var paths = selectionState.GetAllPaths();
            if (paths == null || paths.Count == 0) return;

            CaptureFoldoutSnapshot(treeView);
            LifecycleManager.MoveItemsToBase(paths, targetBase, treeView);
            UpdateUnitySelection(selectionState.GetAllPaths());
        }

        /// <summary>
        /// Moves items identified by explicit <paramref name="paths"/> to <paramref name="targetBase"/>.
        /// Used by the Pro drag-out cross-window transfer to migrate items without requiring
        /// them to be currently selected.
        /// </summary>
        public void MoveItemsByPaths(List<int[]> paths, Base targetBase, TreeView treeView)
        {
            if (paths == null || paths.Count == 0 || targetBase == null) return;
            CaptureFoldoutSnapshot(treeView);
            LifecycleManager.MoveItemsToBase(paths, targetBase, treeView);
        }

        /// <summary>
        /// Clears the linked scene GUID from the provided Base.
        /// This uses Undo.RecordObject and persists the change then refreshes the controller state.
        /// </summary>
        public void ClearSceneLink(Base baseObj)
        {
            if (baseObj == null) return;
            try
            {
                Undo.RecordObject(baseObj, "Unlink Scene");
                baseObj.SceneGuid = string.Empty;
                EditorUtility.SetDirty(baseObj);
                AssetDatabase.SaveAssets();
            }
            catch { }

            // Refresh controller's cached serialized object/collections and notify listeners
            try { RefreshFromRoot(); } catch { }
            OnStructureChanged?.Invoke();
        }

        /// <summary>
        /// Clears the DefaultBase reference on the Motherbase if it points to the provided base.
        /// </summary>
        public void ClearDefaultBase(Base baseObj)
        {
            if (baseObj == null) return;
            try
            {
                var mother = Motherbase.Home;
                if (mother == null) return;
                Undo.RecordObject(mother, "Clear Default Base");
                if (mother.DefaultBase == baseObj)
                    mother.DefaultBase = null;
                EditorUtility.SetDirty(mother);
                AssetDatabase.SaveAssets();
            }
            catch { }

            try { RefreshFromRoot(); } catch { }
            OnStructureChanged?.Invoke();
        }

        public void RemoveMissingAtPath(int[] path, TreeView treeView)
        {
            LifecycleManager.RemoveMissingAtPath(path, treeView);
        }

        public void CleanMissingChildren(Object parent, TreeView treeView)
        {
            LifecycleManager.CleanMissingChildren(parent, treeView);
        }

        // Implement IDisposable explicitly so callers may rely on Dispose removing event listeners
        void IDisposable.Dispose()
        {
            Dispose();
        }

#if !SECOND_BRAIN_PRO
        /// <summary>
        /// Returns true (and shows the PRO upgrade dialog) when the parent is <see cref="Motherbase"/>
        /// and it already contains at least one Base — enforcing the free-tier single-Base limit.
        /// </summary>
        static bool IsSecondBaseBlocked(Object parent)
        {
            if (parent is Motherbase mb && mb.Children.Count >= 1)
            {
                ProFeatureDialog.Show("Multiple Bases");
                return true;
            }
            return false;
        }
#endif
    }
}
