using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SecretZauce.SecondBrain.Editor
{
    /// <summary>
    /// Manages creation and deletion of IStructure children with automatic selection and foldout state preservation.
    /// Coordinates between ChildCreationHelper, DeletionHelper, and the BrowserWindow state.
    /// </summary>
    public class StructureLifecycleManager
    {
        readonly DeletionHelper deletionHelper;
        readonly BrowserWindow window;
        IStructure Root => window?.Root;
        
        public event Action OnStructureChanged;
        public event Action<int[]> OnSelectionRequested;
        public event Action<Dictionary<object, bool>> OnFoldoutStateRestoreRequested;
        public event Action<int[]> OnExpansionRequested;

        public StructureLifecycleManager(BrowserWindow window, UndoHelper undoHelper)
        {
            this.window = window;
            deletionHelper = new DeletionHelper(undoHelper, window);
        }

        /// <summary>
        /// Creates a new child for the given parent and handles selection/expansion.
        /// </summary>
        public void CreateChild(Object parent, TreeView treeView)
        {
            // Reset affected-asset tracking for this new operation so undo/redo refresh
            // targets only the assets touched here.
            SubAssetRefreshUtils.ClearAffectedAssets();

            // Save foldout state before refresh
            Dictionary<object, bool> foldoutSnapshot = null;
            if (treeView != null)
            {
                foldoutSnapshot = treeView.GetFoldoutSnapshot();
            }

            // Create the child (construction only). LifecycleManager will finish persistence/wiring.
            ChildCreationUtils.CreateNewChild(parent, (newChild) =>
            {
                // Finalize persistence: embed as sub-asset when appropriate and add to parent's children list.
                try
                {
                    FinalizeNewChild(parent, newChild, null);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error finalizing child creation: {ex}");
                }

                // Invoke optional post-creation setup defined by the parent (try to match a provided option)
                try
                {
                    InvokePostCreateCallback(parent, newChild, null);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error invoking create-child callback for parent '{parent?.name}': {ex}");
                }

                OnStructureChanged?.Invoke();
                if (foldoutSnapshot != null)
                { 
                    OnFoldoutStateRestoreRequested?.Invoke(foldoutSnapshot);
                }

                // Build a fresh collections list from root to ensure we have the latest state
                List<IStructure> freshCollections = null;
                if (Root is { ChildrenObjects: not null })
                    freshCollections = Root.ChildrenObjects.OfType<IStructure>().ToList();

                // Find and select the new child using fresh collections list
                int[] newPath = null;
                if (freshCollections != null)
                    newPath = StructureUtils.FindPathToChild(freshCollections, newChild);
                
                if (newPath is { Length: > 0 })
                {
                    OnSelectionRequested?.Invoke(newPath);
                    OnExpansionRequested?.Invoke(newPath);
                    // Set Unity Selection to the new child
                    if (newChild != null)
                    {
                        SelectionUtils.SetActiveObject(newChild);
                    }
                }
            });
        }

        /// <summary>
        /// Creates a new child of the specified type for the given parent and handles selection/expansion.
        /// </summary>
        public void CreateChildOfType(Object parent, Type childType, TreeView treeView)
        {
            // Reset affected-asset tracking for this new operation.
            SubAssetRefreshUtils.ClearAffectedAssets();

            // Save foldout state before refresh
            Dictionary<object, bool> foldoutSnapshot = null;
            if (treeView != null)
            {
                foldoutSnapshot = treeView.GetFoldoutSnapshot();
            }

            // Create the child (construction only). LifecycleManager will finish persistence/wiring.
            ChildCreationUtils.CreateNewChildOfType(parent, childType, (newChild) =>
            {
                try
                {
                    FinalizeNewChild(parent, newChild, null);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error finalizing child creation: {ex}");
                }

                // Invoke optional post-creation setup defined by the parent (try to match a provided option)
                try
                {
                    InvokePostCreateCallback(parent, newChild, childType);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error invoking create-child callback for parent '{parent?.name}': {ex}");
                }

                OnStructureChanged?.Invoke();
                if (foldoutSnapshot != null)
                {
                    OnFoldoutStateRestoreRequested?.Invoke(foldoutSnapshot);
                }

                // Build a fresh collections list from root to ensure we have the latest state
                List<IStructure> freshCollections = null;
                if (Root is { ChildrenObjects: not null })
                    freshCollections = Root.ChildrenObjects.OfType<IStructure>().ToList();

                // Find and select the new child using fresh collections list
                int[] newPath = null;
                if (freshCollections != null)
                    newPath = StructureUtils.FindPathToChild(freshCollections, newChild);

                if (newPath is { Length: > 0 })
                {
                    OnSelectionRequested?.Invoke(newPath);
                    OnExpansionRequested?.Invoke(newPath);
                    // Set Unity Selection to the new child
                    if (newChild != null)
                    {
                        SelectionUtils.SetActiveObject(newChild);
                    }
                }
            });
        }

        /// <summary>
        /// Creates a TextAsset from clipboard text and adds it as a child of the given parent container.
        /// Handles selection and foldout state after creation.
        /// </summary>
        public void PasteTextAsChild(Object parent, string clipboardText, TreeView treeView)
        {
            if (parent == null || string.IsNullOrEmpty(clipboardText))
                return;

            if (parent is not ScriptableObject)
                return;

            SubAssetRefreshUtils.ClearAffectedAssets();

            Dictionary<object, bool> foldoutSnapshot = treeView?.GetFoldoutSnapshot();

            string shortName = ShortenClipboardText(clipboardText);
            var textAsset = new TextAsset(clipboardText);
            textAsset.name = shortName;
            
            // Finalize embedding and wiring of the TextAsset as a child of the parent
            FinalizeNewChild(parent, textAsset, shortName);

            OnStructureChanged?.Invoke();
            if (foldoutSnapshot != null)
                OnFoldoutStateRestoreRequested?.Invoke(foldoutSnapshot);

            List<IStructure> freshCollections = null;
            if (Root is { ChildrenObjects: not null })
                freshCollections = Root.ChildrenObjects.OfType<IStructure>().ToList();

            int[] newPath = null;
            if (freshCollections != null)
                newPath = StructureUtils.FindPathToChild(freshCollections, textAsset);

            if (newPath is { Length: > 0 })
            {
                OnSelectionRequested?.Invoke(newPath);
                OnExpansionRequested?.Invoke(newPath);
                SelectionUtils.SetActiveObject(textAsset);
            }
        }

        /// <summary>
        /// Returns a short display name derived from clipboard text.
        /// For URLs, uses the hostname. For plain text, truncates at ~25 characters.
        /// </summary>
        static string ShortenClipboardText(string text)
        {
            text = text.Trim();
            if (LeafNodeActionHelper.IsUrl(text) &&
                Uri.TryCreate(text, UriKind.Absolute, out var uri))
            {
                return uri.Host;
            }

            const int maxLen = 25;
            if (text.Length <= maxLen)
                return text;
            int lastSpace = text.LastIndexOf(' ', maxLen - 1);
            return lastSpace > 0 ? text.Substring(0, lastSpace) : text.Substring(0, maxLen);
        }

        /// <summary>
        /// Creates a new child with a pre-validated user-supplied name.
        /// Called from the ghost creation session after the user has typed a valid, unique name.
        /// </summary>
        public void CreateChildWithName(Object parent, Type childType, string name, TreeView treeView)
        {
            SubAssetRefreshUtils.ClearAffectedAssets();

            Dictionary<object, bool> foldoutSnapshot = treeView?.GetFoldoutSnapshot();

            Action<Object> onCreated = (newChild) =>
            {
                // Ensure persistence is finalized with the user-supplied name
                try
                {
                    FinalizeNewChild(parent, newChild, name);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error finalizing child creation: {ex}");
                }

                // Invoke optional post-creation setup defined by the parent (try to match a provided option)
                try
                {
                    InvokePostCreateCallback(parent, newChild, childType);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error invoking create-child callback for parent '{parent?.name}': {ex}");
                }

                OnStructureChanged?.Invoke();
                if (foldoutSnapshot != null)
                    OnFoldoutStateRestoreRequested?.Invoke(foldoutSnapshot);

                List<IStructure> freshCollections = null;
                if (Root is { ChildrenObjects: not null })
                    freshCollections = Root.ChildrenObjects.OfType<IStructure>().ToList();

                int[] newPath = null;
                if (freshCollections != null)
                    newPath = StructureUtils.FindPathToChild(freshCollections, newChild);

                if (newPath is { Length: > 0 })
                {
                    OnSelectionRequested?.Invoke(newPath);
                    OnExpansionRequested?.Invoke(newPath);
                    if (newChild != null)
                        SelectionUtils.SetActiveObject(newChild);
                }
            };

            if (childType != null)
                ChildCreationUtils.CreateNewChildOfType(parent, childType, onCreated);
            else
                ChildCreationUtils.CreateNewChild(parent, onCreated);
        }

        /// <summary>
        /// Creates a new child using a specific CreateChildOption supplied by the UI.
        /// The option contains the requested Type, display name and an optional PostCreate callback.
        /// </summary>
        public void CreateChildUsingOption(Object parent, CreateChildOption option, TreeView treeView)
        {
            if (option == null)
            {
                // Fall back to default creation
                CreateChild(parent, treeView);
                return;
            }

            SubAssetRefreshUtils.ClearAffectedAssets();

            Dictionary<object, bool> foldoutSnapshot = treeView?.GetFoldoutSnapshot();

            Action<Object> onCreated = (newChild) =>
            {
                try
                {
                    FinalizeNewChild(parent, newChild, null);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error finalizing child creation: {ex}");
                }

                // Invoke the option's PostCreate callback if present and persist changes
                try
                {
                    option.PostCreate?.Invoke(newChild, option.ChildType);
                    try { EditorUtility.SetDirty(newChild); AssetDatabase.SaveAssets(); } catch { }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error in CreateChildOption.PostCreate for parent '{parent?.name}': {ex}");
                }

                OnStructureChanged?.Invoke();
                if (foldoutSnapshot != null)
                    OnFoldoutStateRestoreRequested?.Invoke(foldoutSnapshot);

                // Select the new child
                List<IStructure> freshCollections = null;
                if (Root is { ChildrenObjects: not null })
                    freshCollections = Root.ChildrenObjects.OfType<IStructure>().ToList();

                int[] newPath = null;
                if (freshCollections != null)
                    newPath = StructureUtils.FindPathToChild(freshCollections, newChild);

                if (newPath is { Length: > 0 })
                {
                    OnSelectionRequested?.Invoke(newPath);
                    OnExpansionRequested?.Invoke(newPath);
                    if (newChild != null)
                        SelectionUtils.SetActiveObject(newChild);
                }
            };

            if (option.ChildType != null)
                ChildCreationUtils.CreateNewChildOfType(parent, option.ChildType, onCreated);
            else
                ChildCreationUtils.CreateNewChild(parent, onCreated);
        }

        /// <summary>
        /// Creates a new child using a specific CreateChildOption supplied by the UI,
        /// using the user-provided name. Applies the option's PostCreate callback when present.
        /// </summary>
        public void CreateChildUsingOptionWithName(Object parent, CreateChildOption option, string name, TreeView treeView)
        {
            if (option == null)
            {
                // Fall back to default creation with name
                CreateChildWithName(parent, null, name, treeView);
                return;
            }

            SubAssetRefreshUtils.ClearAffectedAssets();

            Dictionary<object, bool> foldoutSnapshot = treeView?.GetFoldoutSnapshot();

            Action<Object> onCreated = (newChild) =>
            {
                try
                {
                    FinalizeNewChild(parent, newChild, name);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error finalizing child creation: {ex}");
                }

                // Invoke the option's PostCreate callback if present and persist changes
                try
                {
                    option.PostCreate?.Invoke(newChild, option.ChildType);
                    try { EditorUtility.SetDirty(newChild); AssetDatabase.SaveAssets(); } catch { }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error in CreateChildOption.PostCreate for parent '{parent?.name}': {ex}");
                }

                OnStructureChanged?.Invoke();
                if (foldoutSnapshot != null)
                    OnFoldoutStateRestoreRequested?.Invoke(foldoutSnapshot);

                // Select the new child
                List<IStructure> freshCollections = null;
                if (Root is { ChildrenObjects: not null })
                    freshCollections = Root.ChildrenObjects.OfType<IStructure>().ToList();

                int[] newPath = null;
                if (freshCollections != null)
                    newPath = StructureUtils.FindPathToChild(freshCollections, newChild);

                if (newPath is { Length: > 0 })
                {
                    OnSelectionRequested?.Invoke(newPath);
                    OnExpansionRequested?.Invoke(newPath);
                    if (newChild != null)
                        SelectionUtils.SetActiveObject(newChild);
                }
            };

            if (option.ChildType != null)
                ChildCreationUtils.CreateNewChildOfType(parent, option.ChildType, onCreated);
            else
                ChildCreationUtils.CreateNewChild(parent, onCreated);
        }

        void InvokePostCreateCallback(Object parent, Object newChild, Type childType)
        {
            if (parent is not IHasCreateChildOption hasCreateChild)
                return;

            var options = hasCreateChild.GetCreateChildOptions();
            if (options == null)
                return;

            CreateChildOption matched = null;
            // Prefer exact type match when available
            foreach (var opt in options)
            {
                if (opt == null) continue;
                if (childType != null)
                {
                    if (opt.ChildType == childType) { matched = opt; break; }
                }
                else if (opt.ChildType != null && newChild != null)
                {
                    if (opt.ChildType.IsAssignableFrom(newChild.GetType())) { matched = opt; break; }
                }
            }

            // If we didn't find a matching option, do not attempt a generic callback
            if (matched == null)
                return;

            try
            {
                matched.PostCreate?.Invoke(newChild, matched.ChildType);
                try { EditorUtility.SetDirty(newChild); AssetDatabase.SaveAssets(); } catch { }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error in CreateChildOption.PostCreate for parent '{parent?.name}': {ex}");
            }
        }

        /// <summary>
        /// Creates a new default Container directly under the current Base root, then adds
        /// the supplied external items to it. Used when dragging assets into an empty Base.
        /// </summary>
        public void CreateContainerWithExternalItems(
            List<Object> items,
            TreeView treeView,
            SelectionStateSO selectionState)
        {
            if (items == null || items.Count == 0)
                return;

            var baseRoot = Root as Base;
            if (baseRoot == null)
                return;

            SubAssetRefreshUtils.ClearAffectedAssets();

            // Start a single undo group so creating the container + adding items can be undone together
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Create Container and Add Items");

            Dictionary<object, bool> foldoutSnapshot = treeView?.GetFoldoutSnapshot();

            // Create a new Container child under the base
            ChildCreationUtils.CreateNewChild(baseRoot as Object, (newChild) =>
            {
                // Finalize persistence so the container is embedded/registered before adding external items
                try
                {
                    FinalizeNewChild(baseRoot as Object, newChild, null);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error finalizing container creation: {ex}");
                }

                if (newChild is not IStructure newContainer)
                    return;

                // Build fresh collections after container creation
                List<IStructure> freshCollections = null;
                if (Root is { ChildrenObjects: not null })
                    freshCollections = Root.ChildrenObjects.OfType<IStructure>().ToList();

                if (freshCollections == null || freshCollections.Count == 0)
                    return;

                // Find the path to the new container inside the freshly built collections
                int[] containerPath = StructureUtils.FindPathToChild(freshCollections, newChild);

                // Add external items to the new container
                string parentAssetFolder = AssetUtils.GetAssetFolder(newChild as Object);
                List<Object> validItems = new List<Object>();
                bool blockedEmbedded = false;
                bool blockedUnsavedScene = false;
                bool blockedAlreadyInTree = false;
                foreach (var item in items)
                {
                    if (item is GameObject go && !EditorUtility.IsPersistent(go))
                    {
                        if (SceneObjectRefUtils.IsSceneObjectFromUnsavedScene(item))
                        {
                            blockedUnsavedScene = true;
                            continue;
                        }

                        var sceneRef = SceneObjectRefUtils.CreateSceneObjectRef(go, parentAssetFolder, newChild as Object);
                        if (sceneRef != null && newContainer.CanAcceptChild(sceneRef))
                            validItems.Add(sceneRef);
                    }
                    else if (item is Component component && !EditorUtility.IsPersistent(component))
                    {
                        if (SceneObjectRefUtils.IsSceneObjectFromUnsavedScene(item))
                        {
                            blockedUnsavedScene = true;
                            continue;
                        }

                        var sceneComponentRef = SceneComponentRefUtils.CreateSceneComponentRef(component, parentAssetFolder, newChild as Object);
                        if (sceneComponentRef != null && newContainer.CanAcceptChild(sceneComponentRef))
                            validItems.Add(sceneComponentRef);
                    }
                    else
                    {
                        try
                        {
                            if (item is IStructure && AssetUtils.IsInDifferentAsset(item, newChild as Object))
                            {
                                blockedEmbedded = true;
                                continue;
                            }
                        }
                        catch { }

                        if (item is IStructure && item is not IAllowMultipleParents
                            && IStructure.ExistsInTree(Root, item))
                        {
                            blockedAlreadyInTree = true;
                            continue;
                        }

                        if (newContainer.CanAcceptChild(item))
                        {
                            validItems.Add(item);
                        }
                    }
                }

                if (validItems.Count == 0 && blockedUnsavedScene)
                {
                    EditorGUIUtils.ShowNotification(window, new GUIContent(SceneObjectRefUtils.UnsavedSceneNotification));
                }
                else if (validItems.Count == 0 && blockedEmbedded)
                {
                    EditorGUIUtils.ShowNotification(window, new GUIContent("Use 'Move to' in Item context menu instead"));
                }
                else if (validItems.Count == 0 && blockedAlreadyInTree)
                {
                    EditorGUIUtils.ShowNotification(window, new GUIContent("Already in Tree"));
                }

                if (validItems.Count == 0)
                {
                    Undo.CollapseUndoOperations(undoGroup);
                    Undo.PerformUndo();
                    return;
                }

                Undo.RegisterCompleteObjectUndo(newChild as Object, "Create Container and Add Items");
                foreach (var item in validItems)
                    newContainer.AddChild(item);
                EditorUtility.SetDirty(newChild as Object);
                AssetDatabase.SaveAssets();
                SubAssetRefreshUtils.ImportAndRegister(AssetDatabase.GetAssetPath(newChild as Object));

                // Rebuild fresh collections so item-path lookups reflect the new children
                if (Root is { ChildrenObjects: not null })
                    freshCollections = Root.ChildrenObjects.OfType<IStructure>().ToList();

                OnStructureChanged?.Invoke();
                if (foldoutSnapshot != null)
                    OnFoldoutStateRestoreRequested?.Invoke(foldoutSnapshot);

                // Expand the new container and select the first added item (or the container)
                if (containerPath != null)
                    OnExpansionRequested?.Invoke(containerPath);

                if (validItems.Count > 0)
                {
                    Undo.RegisterCompleteObjectUndo(selectionState, "Create Container and Add Items");
                    selectionState.ClearSelection(window);
                    foreach (var item in validItems)
                    {
                        var itemPath = StructureUtils.FindPathToChild(freshCollections, item);
                        if (itemPath != null)
                        {
                            selectionState.AddToSelection(window, itemPath);
                            OnExpansionRequested?.Invoke(itemPath);
                        }
                    }
                    EditorUtility.SetDirty(selectionState);
                }

                Undo.CollapseUndoOperations(undoGroup);
            });
        }

        /// <summary>
        /// Deletes the item at the specified path and handles selection/foldout state.
        /// </summary>
        void DeleteItem(int[] path, TreeView treeView)
        {
            // Start a single undo group for the delete operation
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Delete Selection");

            // CRITICAL: Record selection state BEFORE deletion so undo can restore it.
            // Use RegisterCompleteObjectUndo (not RecordObject) because RecordObject computes
            // its diff lazily at end-of-frame, which means the selectionState change would NOT
            // be included in the collapsed undo group created by CollapseUndoOperations below.
            // RegisterCompleteObjectUndo captures the full state immediately.
            var selectionState = window.Controller.SelectionState;
            if (selectionState != null)
            {
                Undo.RegisterCompleteObjectUndo(selectionState, "Delete Selection");
            }

            // Save foldout state before deletion (for both immediate restore and undo)
            Dictionary<object, bool> foldoutSnapshot = null;
            if (treeView != null)
                foldoutSnapshot = treeView.GetFoldoutSnapshot();

            deletionHelper.DeleteItem(path);

            // Update selection to parent (this changes the selection state, which is already recorded)
            int[] newPath = path.Length > 1 ? path.Take(path.Length - 1).ToArray() : new int[0];
            OnSelectionRequested?.Invoke(newPath);
            
            // Mark selection state as dirty so changes are persisted
            if (selectionState != null)
            {
                EditorUtility.SetDirty(selectionState);
            }
            
            OnStructureChanged?.Invoke();

            // Restore foldout state after refresh
            if (foldoutSnapshot != null)
                OnFoldoutStateRestoreRequested?.Invoke(foldoutSnapshot);
            
            // Collapse all operations into a single undo entry
            Undo.CollapseUndoOperations(undoGroup);
        }
        
        /// <summary>
        /// Deletes multiple items at the specified paths and handles selection/foldout state.
        /// </summary>
        public void DeleteItems(List<int[]> paths, TreeView treeView)
        {
            if (paths == null || paths.Count == 0)
                return;
            
            // Reset affected-asset tracking so this operation's paths are tracked cleanly.
            SubAssetRefreshUtils.ClearAffectedAssets();
            
            if (paths.Count == 1)
            {
                DeleteItem(paths[0], treeView);
                return;
            }
            
            // Start a single undo group for the delete operation
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Delete Multiple Selections");
            
            // CRITICAL: Record selection state BEFORE deletion so undo can restore it.
            // Use RegisterCompleteObjectUndo (not RecordObject) because RecordObject computes
            // its diff lazily at end-of-frame, which means the selectionState change would NOT
            // be included in the collapsed undo group created by CollapseUndoOperations below.
            var selectionState = window.Controller.SelectionState;
            if (selectionState != null)
            {
                Undo.RegisterCompleteObjectUndo(selectionState, "Delete Multiple Selections");
            }
            
            Dictionary<object, bool> foldoutSnapshot = null;
            if (treeView != null)
                foldoutSnapshot = treeView.GetFoldoutSnapshot();

            // Sort paths by depth (deepest first) to avoid index shifting issues
            var sortedPaths = paths.OrderByDescending(p => p.Length)
                                   .ThenByDescending(p => string.Join(",", p))
                                   .ToList();

            foreach (var path in sortedPaths)
                deletionHelper.DeleteItem(path);

            int[] newPath = StructureUtils.FindCommonParent(paths);
            OnSelectionRequested?.Invoke(newPath);
            
            // Mark selection state as dirty so changes are persisted
            if (selectionState != null)
            {
                EditorUtility.SetDirty(selectionState);
            }
            
            OnStructureChanged?.Invoke();
           
            if (foldoutSnapshot != null)
                OnFoldoutStateRestoreRequested?.Invoke(foldoutSnapshot);
            
            // Collapse all operations into a single undo entry
            Undo.CollapseUndoOperations(undoGroup);
        }
        
        /// <summary>
        /// Duplicates the selected items and places them after the originals.
        /// Returns the list of duplicated Objects.
        /// </summary>
        public List<Object> DuplicateItems(List<int[]> paths, TreeView treeView, List<IStructure> collections)
        {
            List<Object> duplicatedItems = new List<Object>();
            
            if (paths == null || paths.Count == 0)
                return duplicatedItems;

            // Reset affected-asset tracking for this operation.
            SubAssetRefreshUtils.ClearAffectedAssets();

            // Deduplicate paths to prevent duplicating the same item multiple times
            var uniquePaths = new List<int[]>();
            var pathStrings = new HashSet<string>();
            foreach (var path in paths)
            {
                string pathStr = string.Join(",", path);
                if (pathStrings.Add(pathStr))
                {
                    uniquePaths.Add(path);
                }
            }

            Dictionary<object, bool> foldoutSnapshot = null;
            if (treeView != null)
                foldoutSnapshot = treeView.GetFoldoutSnapshot();

            // Sort paths in reverse order (deepest first, then by full path descending)
            // This prevents index shifting issues when inserting duplicates
            var sortedPaths = uniquePaths.OrderByDescending(p => p.Length)
                                   .ThenByDescending(p => string.Join(",", p))
                                   .ToList();
            
            // Duplicate each item in reverse order to avoid index shifting
            foreach (var path in sortedPaths)
            {
                var duplicated = DuplicateSingleItem(path, collections);
                if (duplicated != null)
                    duplicatedItems.Add(duplicated);
            }

            OnStructureChanged?.Invoke();
            if (foldoutSnapshot != null)
                OnFoldoutStateRestoreRequested?.Invoke(foldoutSnapshot);

            return duplicatedItems;
        }
        
        /// <summary>
        /// Duplicates a single item at the specified path.
        /// Returns the duplicated item.
        /// </summary>
        Object DuplicateSingleItem(int[] path, List<IStructure> collections)
        {
            if (path == null || path.Length == 0 || collections == null)
                return null;
            
            var item = StructureUtils.GetNodeAtPath(path, collections);
            if (item == null || item is IStructure) // Structure duplication not allowed
                return null;

            // Block folder assets (DefaultAsset directories)
            string itemAssetPath = AssetDatabase.GetAssetPath(item);
            if (!string.IsNullOrEmpty(itemAssetPath) && AssetDatabase.IsValidFolder(itemAssetPath))
                return null;

            // Get the parent
            IStructure parent = GetParentAtPath(path, collections);
            if (parent == null)
                return null;

            // Block sub-assets that live in an external file (e.g. AudioMixerGroup inside an AudioMixer)
            if (AssetUtils.IsInDifferentAsset(item, parent as Object))
                return null;
            
            // Get the insertion index (right after the original)
            int insertIndex = path[^1] + 1;
            var duplicate = Object.Instantiate(item);
            string uniqueName = AssetUtils.GenerateUniqueName(item.name, parent);
            duplicate.name = uniqueName;

            AssetUtils.CreateDuplicatedAsset(item, uniqueName, duplicate);

            // Add to parent
            Undo.RegisterCompleteObjectUndo(parent as Object, "Duplicate Item");
            TryAddChild(parent, duplicate, insertIndex);
            
            // Set the name AFTER adding to parent to ensure it sticks
            duplicate.name = uniqueName;
            
            EditorUtility.SetDirty(parent as Object);
            EditorUtility.SetDirty(duplicate);
            AssetDatabase.SaveAssets();
            // Targeted refresh so the Project window reflects the new duplicate immediately.
            SubAssetRefreshUtils.ImportAndRegister(AssetDatabase.GetAssetPath(parent as Object));
            
            return duplicate;
        }

        /// <summary>
        /// Reparents items to a new parent at the specified drop position.
        /// Supports Before, After, and Inside positioning.
        /// </summary>
        public void ReparentItems(
            List<int[]> itemPaths,
            List<Object> items,
            int[] targetPath,
            DragAndDropManager.DropPosition dropPosition,
            List<IStructure> collections,
            TreeView treeView,
            SelectionStateSO selectionState)
        {
            if (items == null || items.Count == 0 || targetPath == null)
            {
                EditorGUIUtils.ShowNotification(window, new GUIContent("Invalid drag or target."));
                return;
            }
            
            // Reset affected-asset tracking for this reparent operation.
            SubAssetRefreshUtils.ClearAffectedAssets();
            
            // Start a single undo group for the entire reparent operation
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Reparent Items");
            
            // Save foldout state
            Dictionary<object, bool> foldoutSnapshot = null;
            if (treeView != null)
                foldoutSnapshot = treeView.GetFoldoutSnapshot();

            IStructure targetParent;
            int insertIndex = -1;
            
            if (dropPosition == DragAndDropManager.DropPosition.Inside)
            {
                // Drop inside: add to target's children
                targetParent = StructureUtils.GetNodeAtPath(targetPath, collections) as IStructure;
                insertIndex = -1; // Add at end
            }
            else // Before or After
            {
                // Drop before/after: add to target's parent
                if (targetPath.Length == 1)
                {
                    // Top-level: parent is root
                    targetParent = Root; 
                    insertIndex = targetPath[0];
                    if (dropPosition == DragAndDropManager.DropPosition.After)
                        insertIndex++;
                }
                else
                {
                    // Get parent of target
                    int[] parentPath = new int[targetPath.Length - 1];
                    Array.Copy(targetPath, parentPath, parentPath.Length);
                    targetParent = StructureUtils.GetNodeAtPath(parentPath, collections) as IStructure;
                    insertIndex = targetPath[targetPath.Length - 1];
                    if (dropPosition == DragAndDropManager.DropPosition.After)
                        insertIndex++;
                }
            }
            
            if (targetParent == null)
            {
                EditorGUIUtils.ShowNotification(window, new GUIContent("Target parent is invalid."));
                return;
            }
            
            // CRITICAL: Validate type compatibility BEFORE removing items
            // Check if the first item can be added to the target parent
            if (!targetParent.CanAcceptChild(items[0]))
            {
                EditorGUIUtils.ShowNotification(window, new GUIContent("Type mismatch")); 
                return;
            }
            
            // Check if we're moving within the same parent
            bool sameParent = false;
            IStructure firstItemParent = GetParentAtPath(itemPaths[0], collections);
            if (firstItemParent == targetParent)
            {
                sameParent = true;
            }

            // Prevent moving structure assets (IStructure) that live in a different .asset file than the target.
            // Do this BEFORE any RemoveChild calls so we never end up in a partial state.
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                try
                {
                    if (it is IStructure && AssetUtils.IsInDifferentAsset(it, targetParent as Object))
                    {
                        string fileName = Path.GetFileName(AssetDatabase.GetAssetPath(it));
                        EditorGUIUtils.ShowNotification(window, new GUIContent($"Use 'Move to' in Item context menu instead"));
                        // Revert undo group and abort
                        Undo.RevertAllInCurrentGroup();
                        return;
                    }
                }
                catch { }
            }

            // Compute insertion order using an element-wise lexicographic comparator on itemPaths.
            // This orders items by their original position in the tree (ancestors before descendants,
            // siblings by index), which works across different depths.
            var pathComparer = Comparer<int[]>.Create((a, b) =>
            {
                if (a == null && b == null) return 0;
                if (a == null) return -1;
                if (b == null) return 1;
                int min = Math.Min(a.Length, b.Length);
                for (int k = 0; k < min; k++)
                {
                    if (a[k] != b[k])
                        return a[k].CompareTo(b[k]);
                }
                // shorter path (ancestor) comes first
                return a.Length.CompareTo(b.Length);
            });

            var insertionOrderByPath = Enumerable.Range(0, items.Count)
                .OrderBy(i => itemPaths[i], pathComparer)
                .ThenBy(i => i)
                .ToList();
            
             // Build a pre-order index map for the current document/tree BEFORE removals so we can sort items by their
             // actual visual/document order (pre-order traversal). This ensures removed items are found in the map.
             var preOrderIndex = new Dictionary<Object, int>();
             int preCounter = 0;

             void PreWalk(IStructure node)
             {
                 if (node == null || node.ChildrenObjects == null)
                     return;

                 foreach (var child in node.ChildrenObjects)
                 {
                     if (child == null)
                         continue;

                     if (!preOrderIndex.ContainsKey(child as Object))
                         preOrderIndex[child as Object] = preCounter++;

                     if (child is IStructure childStruct)
                         PreWalk(childStruct);
                 }
             }

             if (collections != null)
             {
                 foreach (var col in collections)
                 {
                     if (col == null) continue;
                     if (!preOrderIndex.ContainsKey(col as Object))
                         preOrderIndex[col as Object] = preCounter++;
                     PreWalk(col);
                 }
             }

             // Sort items by reverse depth to avoid index shifting during removal
             var sortedIndices = Enumerable.Range(0, items.Count)
                 .OrderByDescending(i => itemPaths[i].Length)
                 .ThenByDescending(i => string.Join(",", itemPaths[i]))
                 .ToList();
            
            // Track how many items we're removing before the insert index (if same parent)
            int itemsRemovedBeforeInsert = 0;
            if (sameParent && insertIndex >= 0)
            {
                foreach (var idx in sortedIndices)
                {
                    var path = itemPaths[idx];
                    if (path.Length > 0)
                    {
                        int oldIndex = path[path.Length - 1];
                        if (oldIndex < insertIndex)
                        {
                            itemsRemovedBeforeInsert++;
                        }
                    }
                }
            }
            
            // Remove items from old parents (record undo for each)
            List<IStructure> oldParents = new List<IStructure>();
            foreach (var idx in sortedIndices)
            {
                var path = itemPaths[idx];
                var item = items[idx];
                
                IStructure oldParent = GetParentAtPath(path, collections);
                if (oldParent != null)
                {
                    Undo.RegisterCompleteObjectUndo(oldParent as Object, "Reparent Items");
                    oldParent.RemoveChild(item);
                    oldParents.Add(oldParent);
                    EditorUtility.SetDirty(oldParent as Object);
                }
            }
            
            // Adjust insert index if we removed items before it in same parent
            if (sameParent && insertIndex >= 0)
                insertIndex -= itemsRemovedBeforeInsert;
            
            // Add items to new parent (in original order)
            Undo.RegisterCompleteObjectUndo(targetParent as Object, "Reparent Items");
            int currentInsertIndex = insertIndex;

            // Use the insertion order computed from the original item paths
            var insertionOrder = insertionOrderByPath;

            foreach (var idx in insertionOrder)
            {
                var item = items[idx];
                TryAddChild(targetParent, item, currentInsertIndex);
                if (currentInsertIndex >= 0)
                    currentInsertIndex++; // Increment for next item
            }
            EditorUtility.SetDirty(targetParent as Object);
            // Reordering never adds or removes sub-assets, so a per-file ForceUpdate reimport is
            // unnecessary overhead. Batch the save atomically instead to avoid a stall per parent.
            AssetDatabase.StartAssetEditing();
            try { AssetDatabase.SaveAssets(); }
            finally { AssetDatabase.StopAssetEditing(); }

            // Register paths for undo/redo tracking only (no force-reimport needed).
            foreach (var op in oldParents)
            {
                if (op == null) continue;
                SubAssetRefreshUtils.RegisterAffectedAsset(op as Object);
            }
            SubAssetRefreshUtils.RegisterAffectedAsset(targetParent as Object);
            
            // Build a fresh collections snapshot from authoritative root so path lookups are accurate
            List<IStructure> freshCollections = null;
            try
            {
                var rootObj = Root;
                if (rootObj is { ChildrenObjects: not null })
                    freshCollections = rootObj.ChildrenObjects.OfType<IStructure>().ToList();
            }
            catch { }

             // Find new paths for moved items using fresh collections
             List<int[]> newPaths = new List<int[]>();
             // Use the same insertion order when resolving new paths and building selection so the
             // final selection order matches the visual insertion order.
             foreach (var idx in insertionOrder)
             {
                 var item = items[idx];
                 var newPath = StructureUtils.FindPathToChild(freshCollections ?? collections, item);
                 if (newPath != null)
                 {
                     newPaths.Add(newPath);
                 }
             }

             OnStructureChanged?.Invoke();
             if (foldoutSnapshot != null)
                 OnFoldoutStateRestoreRequested?.Invoke(foldoutSnapshot);
             
             // Select the moved items at their new locations
             // Record selection state as part of the same undo group.
             // Use RegisterCompleteObjectUndo (not RecordObject) because RecordObject computes
             // its diff lazily, which would miss the CollapseUndoOperations below.
             Undo.RegisterCompleteObjectUndo(selectionState, "Reparent Items");
             selectionState.ClearSelection(window);
             foreach (var newPath in newPaths)
             {
                 selectionState.AddToSelection(window, newPath);
             }
             EditorUtility.SetDirty(selectionState);
             
             // Collapse all operations into a single undo entry
             Undo.CollapseUndoOperations(undoGroup);
         }
        
        /// <summary>
        /// Handles a drop onto empty space below all items (RootAfterAll).
        /// Containers are moved to depth-0; leaf items are wrapped in a new Container at depth-0.
        /// All sub-steps are collapsed into a single undo group.
        /// </summary>
        public void DropItemsAtRootLevel(
            List<int[]> draggedPaths,
            List<Object> draggedItems,
            List<IStructure> collections,
            TreeView treeView,
            SelectionStateSO selectionState)
        {
            if (draggedItems == null || draggedItems.Count == 0 || Root == null) return;

            SubAssetRefreshUtils.ClearAffectedAssets();
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Drop at Root Level");

            var foldoutSnapshot = treeView?.GetFoldoutSnapshot();
            Undo.RegisterCompleteObjectUndo(selectionState, "Drop at Root Level");

            // Separate containers (IStructure) from leaf items
            var containerPairs = new List<(int[] path, Object item)>();
            var leafPairs = new List<(int[] path, Object item)>();
            for (int i = 0; i < draggedItems.Count; i++)
            {
                if (draggedItems[i] is IStructure)
                    containerPairs.Add((draggedPaths[i], draggedItems[i]));
                else
                    leafPairs.Add((draggedPaths[i], draggedItems[i]));
            }

            // Remove all dragged items from current parents (deepest-first to avoid index shifting)
            var removedParents = new HashSet<IStructure>();
            var allPairs = containerPairs.Concat(leafPairs)
                .OrderByDescending(t => t.path.Length)
                .ThenByDescending(t => string.Join(",", t.path))
                .ToList();

            foreach (var (path, item) in allPairs)
            {
                var oldParent = GetParentAtPath(path, collections);
                if (oldParent == null) continue;
                if (removedParents.Add(oldParent))
                    Undo.RegisterCompleteObjectUndo(oldParent as Object, "Drop at Root Level");
                oldParent.RemoveChild(item);
                EditorUtility.SetDirty(oldParent as Object);
            }

            // Record Root BEFORE any additions so undo restores the pre-operation state.
            // This single recording covers both the container re-adds and any new wrapper container.
            Undo.RegisterCompleteObjectUndo(Root as Object, "Drop at Root Level");

            // Re-add containers to Root at the end (preserve original tree order)
            foreach (var (_, item) in containerPairs.OrderBy(t => string.Join(",", t.path)))
            {
                var r = Root.AddChild(item, -1);
                if (r != AddChildResult.Success)
                    EditorGUIUtils.ShowNotification(window, new GUIContent(OperationErrorUtils.GetDisplayedAddChildErrorMessage(r)));
            }

            // Wrap leaf items in a new Container at root.
            // ChildCreationUtils.CreateNewChild invokes FinalizeNewChild inside the callback,
            // which handles sub-asset embedding, unique naming, and AddChild to Root.
            Object newContainerObj = null;
            if (leafPairs.Count > 0)
            {
                ChildCreationUtils.CreateNewChild(Root as Object, (newChild) =>
                {
                    try
                    {
                        FinalizeNewChild(Root as Object, newChild, null);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"DropItemsAtRootLevel: failed to create wrapper container: {ex}");
                        return;
                    }

                    if (newChild is not IStructure newChildStruct) return;
                    newContainerObj = newChild;

                    // Add leaves to the new container (in original tree order)
                    Undo.RegisterCompleteObjectUndo(newChild, "Drop at Root Level");
                    foreach (var (_, leaf) in leafPairs.OrderBy(t => string.Join(",", t.path)))
                    {
                        var r = newChildStruct.AddChild(leaf, -1);
                        if (r != AddChildResult.Success)
                            EditorGUIUtils.ShowNotification(window, new GUIContent(OperationErrorUtils.GetDisplayedAddChildErrorMessage(r)));
                    }
                    EditorUtility.SetDirty(newChild);
                });
            }

            EditorUtility.SetDirty(Root as Object);
            AssetDatabase.SaveAssets();

            foreach (var op in removedParents)
            {
                if (op == null) continue;
                SubAssetRefreshUtils.ImportAndRegister(AssetDatabase.GetAssetPath(op as Object));
            }
            SubAssetRefreshUtils.ImportAndRegister(AssetDatabase.GetAssetPath(Root as Object));

            List<IStructure> freshCollections = null;
            if (Root is { ChildrenObjects: not null })
                freshCollections = Root.ChildrenObjects.OfType<IStructure>().ToList();

            OnStructureChanged?.Invoke();
            if (foldoutSnapshot != null)
                OnFoldoutStateRestoreRequested?.Invoke(foldoutSnapshot);

            selectionState.ClearSelection(window);
            var resolveCollections = freshCollections ?? collections;

            foreach (var (_, item) in containerPairs)
            {
                var p = StructureUtils.FindPathToChild(resolveCollections, item);
                // Don't expand moved containers — foldout snapshot already preserves their state.
                if (p != null) { selectionState.AddToSelection(window, p); }
            }
            if (newContainerObj != null)
            {
                var p = StructureUtils.FindPathToChild(resolveCollections, newContainerObj);
                if (p != null) { selectionState.AddToSelection(window, p); OnExpansionRequested?.Invoke(p); }
            }

            EditorUtility.SetDirty(selectionState);
            Undo.CollapseUndoOperations(undoGroup);
        }

        /// <summary>
        /// Wraps all selected items in a new Container.
        /// When all items share the same parent the new Container is created there;
        /// otherwise it is created at depth-0 under Root.
        /// </summary>
        public void WrapSelectedInContainer(
            List<int[]> selectedPaths,
            List<IStructure> collections,
            TreeView treeView,
            SelectionStateSO selectionState)
        {
            if (selectedPaths == null || selectedPaths.Count == 0 || Root == null) return;

            // Resolve items, filtering out nulls
            var pairs = new List<(int[] path, Object item)>();
            foreach (var path in selectedPaths)
            {
                var obj = StructureUtils.GetNodeAtPath(path, collections);
                if (obj != null)
                    pairs.Add((path, obj));
            }
            if (pairs.Count == 0) return;

            SubAssetRefreshUtils.ClearAffectedAssets();
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Wrap in Container");

            var foldoutSnapshot = treeView?.GetFoldoutSnapshot();
            Undo.RegisterCompleteObjectUndo(selectionState, "Wrap in Container");

            // Determine container parent: common parent if all items share one, else Root
            IStructure containerParent = null;
            bool sameParent = true;
            IStructure firstParent = GetParentAtPath(pairs[0].path, collections);
            foreach (var (path, _) in pairs)
            {
                var p = GetParentAtPath(path, collections);
                if (p != firstParent) { sameParent = false; break; }
            }
            containerParent = sameParent ? firstParent : Root;
            if (containerParent == null) return;

            // Sort deepest-first to avoid index shifting when removing
            var sortedPairs = pairs
                .OrderByDescending(t => t.path.Length)
                .ThenByDescending(t => string.Join(",", t.path))
                .ToList();

            // Record undo for all affected parents before any removal
            var affectedParents = new HashSet<IStructure>();
            foreach (var (path, _) in sortedPairs)
            {
                var parent = GetParentAtPath(path, collections);
                if (parent != null && affectedParents.Add(parent))
                    Undo.RegisterCompleteObjectUndo(parent as Object, "Wrap in Container");
            }

            // Remove items from their current parents
            foreach (var (path, item) in sortedPairs)
            {
                var parent = GetParentAtPath(path, collections);
                if (parent == null) continue;
                parent.RemoveChild(item);
                EditorUtility.SetDirty(parent as Object);
            }

            // Record container parent before adding the new Container
            Undo.RegisterCompleteObjectUndo(containerParent as Object, "Wrap in Container");

            // Create new Container under containerParent and add the items to it
            Object newContainerObj = null;
            ChildCreationUtils.CreateNewChild(containerParent as Object, (newChild) =>
            {
                try { FinalizeNewChild(containerParent as Object, newChild, null); }
                catch (Exception ex)
                {
                    Debug.LogError($"WrapSelectedInContainer: failed to create wrapper container: {ex}");
                    return;
                }

                if (newChild is not IStructure newContainer) return;
                newContainerObj = newChild;

                Undo.RegisterCompleteObjectUndo(newChild, "Wrap in Container");

                // Add items in original tree order (ascending path)
                foreach (var (_, item) in pairs.OrderBy(t => string.Join(",", t.path)))
                {
                    var r = newContainer.AddChild(item, -1);
                    if (r != AddChildResult.Success)
                        window.ShowNotification(new GUIContent(OperationErrorUtils.GetDisplayedAddChildErrorMessage(r)));
                }
                EditorUtility.SetDirty(newChild);
            });

            EditorUtility.SetDirty(containerParent as Object);
            AssetDatabase.SaveAssets();

            foreach (var parent in affectedParents)
            {
                if (parent == null) continue;
                SubAssetRefreshUtils.ImportAndRegister(AssetDatabase.GetAssetPath(parent as Object));
            }
            SubAssetRefreshUtils.ImportAndRegister(AssetDatabase.GetAssetPath(containerParent as Object));

            List<IStructure> freshCollections = null;
            if (Root is { ChildrenObjects: not null })
                freshCollections = Root.ChildrenObjects.OfType<IStructure>().ToList();

            OnStructureChanged?.Invoke();
            if (foldoutSnapshot != null)
                OnFoldoutStateRestoreRequested?.Invoke(foldoutSnapshot);

            selectionState.ClearSelection(window);

            if (newContainerObj != null)
            {
                var resolveCollections = freshCollections ?? collections;
                var p = StructureUtils.FindPathToChild(resolveCollections, newContainerObj);
                if (p != null)
                {
                    selectionState.AddToSelection(window, p);
                    OnExpansionRequested?.Invoke(p);
                    OnSelectionRequested?.Invoke(p);
                }
            }

            EditorUtility.SetDirty(selectionState);
            Undo.CollapseUndoOperations(undoGroup);
        }

        /// <summary>
        /// Adds external Unity Objects (from Project tab) to the specified drop position.
        /// Supports Before, After, and Inside positioning.
        /// </summary>
        public void AddExternalItems(
            List<Object> items,
            int[] targetPath,
            DragAndDropManager.DropPosition dropPosition,
            List<IStructure> collections,
            TreeView treeView,
            SelectionStateSO selectionState)
        {
            if (items == null || items.Count == 0 || targetPath == null)
            {
                EditorGUIUtils.ShowNotification(window, new GUIContent("Invalid drag or target."));
                return;
            }
            
            // Reset affected-asset tracking for this operation.
            SubAssetRefreshUtils.ClearAffectedAssets();
            
            // Start a single undo group for the entire operation
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Add External Items");
            
            // Save foldout state
            Dictionary<object, bool> foldoutSnapshot = null;
            if (treeView != null)
                foldoutSnapshot = treeView.GetFoldoutSnapshot();
            
            // Get target parent and insert index based on drop position
            IStructure targetParent = null;
            int insertIndex = -1;
            
            if (dropPosition == DragAndDropManager.DropPosition.Inside)
            {
                // Drop inside: add to target's children
                targetParent = StructureUtils.GetNodeAtPath(targetPath, collections) as IStructure;
                insertIndex = -1; // Add at end
            }
            else // Before or After
            {
                // Drop before/after: add to target's parent
                if (targetPath.Length == 1)
                {
                    // Top-level: parent is root
                    targetParent = Root;
                    insertIndex = targetPath[0];
                    if (dropPosition == DragAndDropManager.DropPosition.After)
                        insertIndex++;
                }
                else
                {
                    // Get parent of target
                    int[] parentPath = new int[targetPath.Length - 1];
                    Array.Copy(targetPath, parentPath, parentPath.Length);
                    targetParent = StructureUtils.GetNodeAtPath(parentPath, collections) as IStructure;
                    insertIndex = targetPath[targetPath.Length - 1];
                    if (dropPosition == DragAndDropManager.DropPosition.After)
                        insertIndex++;
                }
            }
            
            if (targetParent == null)
            {
                EditorGUIUtils.ShowNotification(window, new GUIContent("Target parent is invalid."));
                return;
            }
            
            // Validate type compatibility for each item, wrapping scene GameObjects/Components in scene refs
            List<Object> validItems = new List<Object>();
            string parentAssetFolder = AssetUtils.GetAssetFolder(targetParent as Object);
            bool blockedEmbedded = false;
            bool blockedUnsavedScene = false;
            foreach (var item in items)
            {
                // Wrap scene GameObjects (non-persistent) in a SceneObjectRef.
                // Embed it as a sub-asset inside the parent's asset file when possible.
                if (item is GameObject go && !EditorUtility.IsPersistent(go))
                {
                    // Block game objects whose scene has no path on disk — they cannot produce a
                    // stable GlobalObjectId and the reference would be unresolvable.
                    if (SceneObjectRefUtils.IsGameObjectFromUnsavedScene(item))
                    {
                        blockedUnsavedScene = true;
                        continue;
                    }
                    var sceneObjectRef = SceneObjectRefUtils.CreateSceneObjectRef(go, parentAssetFolder, targetParent as Object);
                    if (sceneObjectRef != null && targetParent.CanAcceptChild(sceneObjectRef))
                        validItems.Add(sceneObjectRef);
                }
                else if (item is Component component && !EditorUtility.IsPersistent(component))
                {
                    if (SceneObjectRefUtils.IsSceneObjectFromUnsavedScene(item))
                    {
                        blockedUnsavedScene = true;
                        continue;
                    }

                    var sceneComponentRef = SceneComponentRefUtils.CreateSceneComponentRef(component, parentAssetFolder, targetParent as Object);
                    if (sceneComponentRef != null && targetParent.CanAcceptChild(sceneComponentRef))
                        validItems.Add(sceneComponentRef);
                }
                else
                {
                    // If item is an embedded sub-asset that belongs to a different asset file than the
                    // target parent, block it and skip adding — surface notification after validation.
                    try
                    {
                        // Only block structure assets (Profile/Container/IStructure) that belong to a different file.
                        if (item is IStructure && AssetUtils.IsInDifferentAsset(item, targetParent as Object))
                        {
                            blockedEmbedded = true;
                            continue;
                        }
                    }
                    catch { }

                    if (targetParent.CanAcceptChild(item))
                        validItems.Add(item);
                }
            }

            if (validItems.Count == 0)
            {
                if (blockedUnsavedScene)
                {
                    EditorGUIUtils.ShowNotification(window, new GUIContent(SceneObjectRefUtils.UnsavedSceneNotification));
                }
                else if (blockedEmbedded)
                {
                    EditorGUIUtils.ShowNotification(window, new GUIContent("Use 'Move to' in Item context menu instead"));
                }
                else
                {
                    EditorGUIUtils.ShowNotification(window, new GUIContent("Type mismatch or duplicated."));
                }
                return;
            }
            
            // Add items to parent and track only successful inserts for post-refresh selection.
            Undo.RegisterCompleteObjectUndo(targetParent as Object, "Add External Items");
            int currentInsertIndex = insertIndex;
            var addedItems = new List<Object>();
            
            foreach (var item in validItems)
            {
                if (TryAddChild(targetParent, item, currentInsertIndex))
                {
                    addedItems.Add(item);
                }

                if (currentInsertIndex >= 0)
                    currentInsertIndex++;
            }

            if (addedItems.Count == 0)
            {
                Undo.RevertAllInCurrentGroup();
                return;
            }
            
            EditorUtility.SetDirty(targetParent as Object);
            AssetDatabase.SaveAssets();
            // Targeted refresh so the Project window reflects the newly added external items.
            SubAssetRefreshUtils.ImportAndRegister(AssetDatabase.GetAssetPath(targetParent as Object));
            OnStructureChanged?.Invoke();
            if (foldoutSnapshot != null)
                OnFoldoutStateRestoreRequested?.Invoke(foldoutSnapshot);
            
            
            // Select the added items - record selection as part of the same undo group.
            // Use RegisterCompleteObjectUndo (not RecordObject) because RecordObject computes
            // its diff lazily, which would miss the CollapseUndoOperations below.
            Undo.RegisterCompleteObjectUndo(selectionState, "Add External Items");
            selectionState.ClearSelection(window);

            // Rebuild collections from root after mutation so SceneObjectRef path lookup is always current.
            List<IStructure> freshCollections = null;
            if (Root is { ChildrenObjects: not null })
                freshCollections = Root.ChildrenObjects.OfType<IStructure>().ToList();
            
            // Find paths for added items
            foreach (var item in addedItems)
            {
                var newPath = StructureUtils.FindPathToChild(freshCollections ?? collections, item);
                if (newPath != null)
                {
                    selectionState.AddToSelection(window, newPath);
                    OnExpansionRequested?.Invoke(newPath);
                }
            }
            
            EditorUtility.SetDirty(selectionState);
            
            // Collapse all operations into a single undo entry
            Undo.CollapseUndoOperations(undoGroup);
        }

        bool TryAddChild(IStructure parent, Object objToAdd, int index)
        {
            try
            {
                // Prevent adding structure assets that belong to a different .asset file than the parent.
                if (objToAdd is IStructure && AssetUtils.IsInDifferentAsset(objToAdd, parent as Object))
                {
                    string fileName = Path.GetFileName(AssetDatabase.GetAssetPath(objToAdd));
                    EditorGUIUtils.ShowNotification(window, new GUIContent($"Use 'Move to' in Item context menu instead"));
                    return false;
                }
            }
            catch { }

            var result = parent.AddChild(objToAdd, index);
            if (result != AddChildResult.Success)
            {
                // Show notification instead of blocking dialog
                EditorGUIUtils.ShowNotification(window, new GUIContent(OperationErrorUtils.GetDisplayedAddChildErrorMessage(result)));
            }

            return result == AddChildResult.Success;
        }

        /// <summary>
        /// Removes multiple items from their parents without destroying them.
        /// Items are removed from the hierarchy but not deleted.
        /// </summary>
        public void RemoveItems(List<int[]> paths, TreeView treeView, SelectionStateSO selectionState)
        {
            if (paths == null || paths.Count == 0)
                return;

            // Reset affected-asset tracking for this operation.
            SubAssetRefreshUtils.ClearAffectedAssets();
            
            // Start a single undo group for the entire operation
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Remove Items from Parent");
            
            // Record selection state BEFORE removal so undo can restore it.
            // Use RegisterCompleteObjectUndo (not RecordObject) because the group is
            // collapsed with CollapseUndoOperations before RecordObject's deferred diff runs.
            if (selectionState != null)
            {
                Undo.RegisterCompleteObjectUndo(selectionState, "Remove Items from Parent");
            }
            
            Dictionary<object, bool> foldoutSnapshot = null;
            if (treeView != null)
                foldoutSnapshot = treeView.GetFoldoutSnapshot();

            // Sort paths by depth (deepest first) to avoid index shifting issues
            var sortedPaths = paths.OrderByDescending(p => p.Length)
                                   .ThenByDescending(p => string.Join(",", p))
                                   .ToList();

            // Perform removals — each call registers its parent path in SubAssetRefreshUtils.
            foreach (var path in sortedPaths)
                RemoveSingleItem(path);

            // One SaveAssets call covers all dirty parents marked inside RemoveSingleItem.
            AssetDatabase.SaveAssets();
            // Targeted Project-window refresh for every parent that lost a child.
            SubAssetRefreshUtils.RefreshAffectedAssets();

            // Select the parent of the removed items
            // If all items share the same parent, select that parent
            // Otherwise select the common ancestor
            int[] newPath = StructureUtils.FindParentForSelection(paths);
            OnSelectionRequested?.Invoke(newPath);
            OnStructureChanged?.Invoke();
            if (foldoutSnapshot != null)
                OnFoldoutStateRestoreRequested?.Invoke(foldoutSnapshot);
            
            // Collapse all operations into a single undo entry
            Undo.CollapseUndoOperations(undoGroup);
        }

        /// <summary>
        /// Removes a single item from its parent without destroying it.
        /// Registers the parent's asset path in SubAssetRefreshUtils for batched refresh by RemoveItems.
        /// </summary>
        void RemoveSingleItem(int[] path)
        {
            if (path == null || path.Length == 0)
                return;
            
            // Get parent and item
            IStructure parent = null;
            Object item = null;
            
            if (path.Length == 1)
            {
                // Top-level item
                parent = Root; 
                var parentChildren = parent.ChildrenObjects;
                if (parentChildren != null && path[0] >= 0 && path[0] < parentChildren.Count)
                {
                    item = parentChildren[path[0]];
                }
            }
            else
            {
                // Get parent at path[0..Length-2]
                int[] parentPath = new int[path.Length - 1];
                Array.Copy(path, parentPath, parentPath.Length);
                var rootChildren = new List<IStructure>();
                foreach (var c in Root.ChildrenObjects)
                    if (c is IStructure s) rootChildren.Add(s);
                parent = StructureUtils.GetNodeAtPath(parentPath, rootChildren) as IStructure;
                
                if (parent != null)
                {
                    var parentChildren = parent.ChildrenObjects;
                    if (parentChildren != null)
                    {
                        int childIndex = path[^1];
                        if (childIndex >= 0 && childIndex < parentChildren.Count)
                        {
                            item = parentChildren[childIndex];
                        }
                    }
                }
            }
            
            if (parent == null || item == null)
                return;
            
            // Register undo and remove (but don't destroy)
            Undo.RegisterCompleteObjectUndo(parent as UnityEngine.Object, "Remove from Parent");
            parent.RemoveChild(item);
            EditorUtility.SetDirty(parent as UnityEngine.Object);
            // Register the parent path so RemoveItems can do one batched save + refresh.
            SubAssetRefreshUtils.RegisterAffectedAsset(parent as Object);
        }
        
        // ──────────────────────────────────────────────────────────────────────────────
        // Missing / null entry cleanup
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Removes the null/missing entry at <paramref name="path"/> from its parent's children list.
        /// Uses index-based removal because Unity fake-null objects cannot be removed with
        /// List.Remove(null).
        /// </summary>
        public void RemoveMissingAtPath(int[] path, TreeView treeView)
        {
            if (path == null || path.Length == 0) return;

            List<IStructure> collections = null;
            if (Root is { ChildrenObjects: not null })
                collections = Root.ChildrenObjects.OfType<IStructure>().ToList();

            IStructure parent;
            int childIndex;

            if (path.Length == 1)
            {
                parent = Root;
                childIndex = path[0];
            }
            else
            {
                int[] parentPath = path[..^1];
                parent = collections != null
                    ? StructureUtils.GetNodeAtPath(parentPath, collections) as IStructure
                    : null;
                childIndex = path[^1];
            }

            if (parent == null) return;

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Remove Missing Entry");

            var foldoutSnapshot = treeView?.GetFoldoutSnapshot();

            Undo.RegisterCompleteObjectUndo(parent as Object, "Remove Missing Entry");
            RemoveAtIndex(parent, childIndex);
            EditorUtility.SetDirty(parent as Object);
            AssetDatabase.SaveAssets();
            SubAssetRefreshUtils.ImportAndRegister(AssetDatabase.GetAssetPath(parent as Object));

            int[] newSelection = path.Length > 1 ? path[..^1] : System.Array.Empty<int>();
            OnSelectionRequested?.Invoke(newSelection);
            OnStructureChanged?.Invoke();
            if (foldoutSnapshot != null) OnFoldoutStateRestoreRequested?.Invoke(foldoutSnapshot);

            Undo.CollapseUndoOperations(undoGroup);
        }

        /// <summary>
        /// Removes all null/missing entries from <paramref name="parent"/>'s children list.
        /// </summary>
        public void CleanMissingChildren(Object parent, TreeView treeView)
        {
            if (parent == null) return;
            if (parent is not IStructure parentStruct) return;

            var children = parentStruct.ChildrenObjects;
            if (children == null || !children.Any(c => c == null)) return;

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Clean Up Missing Items");

            var foldoutSnapshot = treeView?.GetFoldoutSnapshot();

            Undo.RegisterCompleteObjectUndo(parent, "Clean Up Missing Items");

            // Iterate backwards so index removal doesn't shift remaining indices.
            for (int i = children.Count - 1; i >= 0; i--)
            {
                if (children[i] == null)
                    RemoveAtIndex(parentStruct, i);
            }

            EditorUtility.SetDirty(parent);
            AssetDatabase.SaveAssets();
            SubAssetRefreshUtils.ImportAndRegister(AssetDatabase.GetAssetPath(parent));

            OnStructureChanged?.Invoke();
            if (foldoutSnapshot != null) OnFoldoutStateRestoreRequested?.Invoke(foldoutSnapshot);

            Undo.CollapseUndoOperations(undoGroup);
        }

        /// <summary>
        /// Removes the child at <paramref name="index"/> from <paramref name="structure"/>'s
        /// underlying typed list. Uses direct index removal to handle Unity fake-null references
        /// that IStructure.RemoveChild cannot find via object equality.
        /// </summary>
        static void RemoveAtIndex(IStructure structure, int index)
        {
            switch (structure)
            {
                case Container c when index >= 0 && index < c.children.Count:
                    c.children.RemoveAt(index);
                    break;
                case Base b when index >= 0 && index < b.Children.Count:
                    b.Children.RemoveAt(index);
                    break;
                case Profile p when index >= 0 && index < p.Children.Count:
                    p.Children.RemoveAt(index);
                    break;
            }
        }

        /// <summary>
        /// Gets the parent structure at the given path
        /// </summary>
        IStructure GetParentAtPath(int[] path, List<IStructure> collections)
        {
            if (path == null || path.Length == 0)
                return null;
            
            if (path.Length == 1)
            {
                // Parent is root
                return Root;
            }
            
            // Get parent at path[0..Length-2]
            int[] parentPath = new int[path.Length - 1];
            Array.Copy(path, parentPath, parentPath.Length);
            return StructureUtils.GetNodeAtPath(parentPath, collections) as IStructure;
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // Move Items to Base
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Tracks a completed Move-to-Base operation so that sub-asset relocation can be
        /// reversed (on Undo) or re-applied (on Redo).
        /// AssetDatabase.RemoveObjectFromAsset / AddObjectToAsset are NOT part of Unity's
        /// Undo system, so we hook into <see cref="Undo.undoRedoPerformed"/> to mirror them.
        /// </summary>
        class MoveOperationRecord
        {
            /// <summary>Objects physically migrated: (obj, fromPath) where <c>fromPath</c>
            /// is the .asset file the object lived in BEFORE the move.
            /// Ordered children-first (same order as the original migration).</summary>
            public List<(Object obj, string fromPath)> migratedObjects;
            /// <summary>.asset path of the target Base file.</summary>
            public string targetBasePath;
            /// <summary>Used to check its children list after undo/redo fires.</summary>
            public Base targetBase;
            /// <summary>Set when the target is a Container (not a Base root).
            /// The undo handler checks this parent's ChildrenObjects instead of targetBase.</summary>
            public IStructure targetParent;
            /// <summary>Top-level items that were moved.</summary>
            public List<Object> movedItems;
            /// <summary>true after move/redo; false after undo.</summary>
            public bool isCurrentlyInTarget = true;
        }

        /// <summary>
        /// Moves the items at the specified paths from their current parents in the active Base
        /// to the root children list of <paramref name="targetBase"/>.
        /// Each moved Container (and all its recursive sub-objects) is re-embedded as a sub-asset
        /// inside the target Base's .asset file.  Undo/Redo are fully supported.
        /// </summary>
        public void MoveItemsToBase(List<int[]> paths, Base targetBase, TreeView treeView)
        {
            if (paths == null || paths.Count == 0 || targetBase == null)
                return;

            string targetBasePath = AssetDatabase.GetAssetPath(targetBase);
            if (string.IsNullOrEmpty(targetBasePath))
            {
                EditorGUIUtils.ShowNotification(window, new GUIContent("Target Base has no asset path."));
                return;
            }

            // Reset affected-asset tracking for this operation.
            SubAssetRefreshUtils.ClearAffectedAssets();

            // Start a single undo group for the entire move operation
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Move Items to Base");

            // Record selection state BEFORE mutation so undo can restore it.
            var selectionState = window.Controller.SelectionState;
            if (selectionState != null)
                Undo.RegisterCompleteObjectUndo(selectionState, "Move Items to Base");

            // Save foldout state
            Dictionary<object, bool> foldoutSnapshot = null;
            if (treeView != null)
                foldoutSnapshot = treeView.GetFoldoutSnapshot();

            // Build current collections from root
            List<IStructure> collections = null;
            if (Root is { ChildrenObjects: not null })
                collections = Root.ChildrenObjects.OfType<IStructure>().ToList();

            if (collections == null)
            {
                Undo.RevertAllInCurrentGroup();
                return;
            }

            // Deduplicate and sort deepest-first to avoid index-shift during removal
            var uniquePaths = paths
                .GroupBy(p => string.Join(",", p))
                .Select(g => g.First())
                .OrderByDescending(p => p.Length)
                .ThenByDescending(p => string.Join(",", p))
                .ToList();

            // Gather valid items to move
            // Track srcPath alongside each item so we can add to dest in ascending
            // (source) order after removal is done in descending order.
            var itemsToMove = new List<(Object item, IStructure parent, int[] srcPath)>();
            foreach (var path in uniquePaths)
            {
                var item = StructureUtils.GetNodeAtPath(path, collections);
                if (item == null)
                    continue;

                IStructure parent = GetParentAtPath(path, collections);
                if (parent == null)
                    continue;

                var baseAsStructure = targetBase as IStructure;
                if (!baseAsStructure.CanAcceptChild(item))
                {
                    EditorGUIUtils.ShowNotification(window, new GUIContent($"Cannot move '{item.name}': type not accepted by target Base."));
                    continue;
                }

                itemsToMove.Add((item, parent, path));
            }

            if (itemsToMove.Count == 0)
            {
                Undo.RevertAllInCurrentGroup();
                return;
            }

            // --- Record undo for ALL affected objects BEFORE any mutation ---
            // This must happen before RemoveChild, RemoveObjectFromAsset, or AddObjectToAsset
            // so Unity can fully restore the serialized fields on undo.
            Undo.RegisterCompleteObjectUndo(targetBase, "Move Items to Base");
            var uniqueParents = new HashSet<IStructure>();
            foreach (var (item, parent, _) in itemsToMove)
            {
                if (uniqueParents.Add(parent))
                    Undo.RegisterCompleteObjectUndo(parent as Object, "Move Items to Base");

                RegisterUndoRecursive(item, "Move Items to Base");
            }

            // --- Remove items from their old parent lists ---
            foreach (var (item, parent, _) in itemsToMove)
            {
                parent.RemoveChild(item);
                EditorUtility.SetDirty(parent as Object);
            }

            // --- Migrate sub-assets from old Base file to the new Base file ---
            // MigrateSubAssetsRecursive returns the exact (obj, fromPath) pairs it relocated
            // so we can reverse the migration precisely on Undo.
            // Use the PARENT's asset path as the allowed-source filter, not the item's own path.
            // The parent is always a Profile-managed node (its file IS the profile file), so only
            // objects that live in that same profile file are eligible for migration. This prevents
            // sub-assets of unrelated files (AudioMixerGroup in a mixer, Materials in a model)
            // from being pulled into the profile even when dragged directly as the moved item.
            var allMigrated = new List<(Object obj, string fromPath)>();
            foreach (var (item, parent, _) in itemsToMove)
            {
                string sourceAssetPath = AssetDatabase.GetAssetPath(parent as Object);
                allMigrated.AddRange(MigrateSubAssetsRecursive(item, targetBasePath, sourceAssetPath));
            }

            // --- Add items to the target Base's children list in ascending source-path order ---
            // uniquePaths was sorted descending for safe removal (avoids index shifts).
            // Appending in that same reversed order would land items at dest in reverse
            // source order. Re-sort ascending (pre-order: ancestor before descendant,
            // siblings by index) so items arrive at dest top-to-bottom = source order.
            var targetAsStructure = targetBase as IStructure;
            var srcPathCmp = Comparer<int[]>.Create((a, b) =>
            {
                int min = Math.Min(a.Length, b.Length);
                for (int k = 0; k < min; k++)
                    if (a[k] != b[k]) return a[k].CompareTo(b[k]);
                return a.Length.CompareTo(b.Length);
            });
            foreach (var (item, _, _) in itemsToMove.OrderBy(x => x.srcPath, srcPathCmp))
                targetAsStructure.AddChild(item, -1);

            EditorUtility.SetDirty(targetBase);
            AssetDatabase.SaveAssets();

            // Targeted Project-window refresh for each modified parent and the target Base
            foreach (var parent in uniqueParents)
                SubAssetRefreshUtils.ImportAndRegister(AssetDatabase.GetAssetPath(parent as Object));
            SubAssetRefreshUtils.ImportAndRegister(targetBasePath);

            // Register an Undo.undoRedoPerformed hook so sub-asset migration is kept in
            // sync with Unity's undo/redo of the serialized children-list changes.
            var record = new MoveOperationRecord
            {
                targetBase           = targetBase,
                targetBasePath       = targetBasePath,
                migratedObjects      = allMigrated,
                movedItems           = itemsToMove.Select(x => x.item).ToList(),
                isCurrentlyInTarget  = true
            };
            RegisterMoveUndoRedoHandler(record);

            OnStructureChanged?.Invoke();
            if (foldoutSnapshot != null)
                OnFoldoutStateRestoreRequested?.Invoke(foldoutSnapshot);

            // Clear the browser selection — moved items are no longer in this window's tree
            if (selectionState != null)
            {
                selectionState.ClearSelection(window);
                EditorUtility.SetDirty(selectionState);
            }

            // Collapse all undo operations into a single entry
            Undo.CollapseUndoOperations(undoGroup);
        }

        /// <summary>
        /// Moves items at the specified paths from their current parents into a Container
        /// in another Base's .asset file. Sub-assets are migrated to the Container's parent
        /// .asset; parent-child relationship targets the Container, not the Base root.
        /// Used by cross-window transfer when the drop target is (or resolves to) a Container.
        /// Undo/Redo are fully supported.
        /// </summary>
        public void MoveItemsToContainer(List<int[]> paths, Container targetContainer, TreeView treeView)
        {
            if (paths == null || paths.Count == 0 || targetContainer == null) return;

            string targetAssetPath = AssetDatabase.GetAssetPath(targetContainer);
            if (string.IsNullOrEmpty(targetAssetPath))
            {
                EditorGUIUtils.ShowNotification(window, new GUIContent("Target Container has no asset path."));
                return;
            }

            SubAssetRefreshUtils.ClearAffectedAssets();

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Move Items to Container");

            var selectionState = window.Controller.SelectionState;
            if (selectionState != null)
                Undo.RegisterCompleteObjectUndo(selectionState, "Move Items to Container");

            Dictionary<object, bool> foldoutSnapshot = null;
            if (treeView != null)
                foldoutSnapshot = treeView.GetFoldoutSnapshot();

            List<IStructure> collections = null;
            if (Root is { ChildrenObjects: not null })
                collections = Root.ChildrenObjects.OfType<IStructure>().ToList();

            if (collections == null) { Undo.RevertAllInCurrentGroup(); return; }

            var uniquePaths = paths
                .GroupBy(p => string.Join(",", p))
                .Select(g => g.First())
                .OrderByDescending(p => p.Length)
                .ThenByDescending(p => string.Join(",", p))
                .ToList();

            var targetAsStructure = targetContainer as IStructure;
            var itemsToMove = new List<(Object item, IStructure parent, int[] srcPath)>();
            foreach (var path in uniquePaths)
            {
                var item = StructureUtils.GetNodeAtPath(path, collections);
                if (item == null) continue;
                IStructure parent = GetParentAtPath(path, collections);
                if (parent == null) continue;
                if (!targetAsStructure.CanAcceptChild(item))
                {
                    EditorGUIUtils.ShowNotification(window, new GUIContent($"Cannot move '{item.name}': type not accepted by target Container."));
                    continue;
                }
                itemsToMove.Add((item, parent, path));
            }

            if (itemsToMove.Count == 0) { Undo.RevertAllInCurrentGroup(); return; }

            Undo.RegisterCompleteObjectUndo(targetContainer, "Move Items to Container");
            var uniqueParents = new HashSet<IStructure>();
            foreach (var (item, parent, _) in itemsToMove)
            {
                if (uniqueParents.Add(parent))
                    Undo.RegisterCompleteObjectUndo(parent as Object, "Move Items to Container");
                RegisterUndoRecursive(item, "Move Items to Container");
            }

            foreach (var (item, parent, _) in itemsToMove)
            {
                parent.RemoveChild(item);
                EditorUtility.SetDirty(parent as Object);
            }

            var allMigrated = new List<(Object obj, string fromPath)>();
            foreach (var (item, parent, _) in itemsToMove)
            {
                string sourceAssetPath = AssetDatabase.GetAssetPath(parent as Object);
                allMigrated.AddRange(MigrateSubAssetsRecursive(item, targetAssetPath, sourceAssetPath));
            }

            var srcPathCmpC = Comparer<int[]>.Create((a, b) =>
            {
                int min = Math.Min(a.Length, b.Length);
                for (int k = 0; k < min; k++)
                    if (a[k] != b[k]) return a[k].CompareTo(b[k]);
                return a.Length.CompareTo(b.Length);
            });
            foreach (var (item, _, _) in itemsToMove.OrderBy(x => x.srcPath, srcPathCmpC))
                targetAsStructure.AddChild(item, -1);

            EditorUtility.SetDirty(targetContainer);
            AssetDatabase.SaveAssets();

            foreach (var parent in uniqueParents)
                SubAssetRefreshUtils.ImportAndRegister(AssetDatabase.GetAssetPath(parent as Object));
            SubAssetRefreshUtils.ImportAndRegister(targetAssetPath);

            var record = new MoveOperationRecord
            {
                targetBase      = null,
                targetParent    = targetContainer,
                targetBasePath  = targetAssetPath,
                migratedObjects = allMigrated,
                movedItems      = itemsToMove.Select(x => x.item).ToList(),
                isCurrentlyInTarget = true
            };
            RegisterMoveUndoRedoHandler(record);

            OnStructureChanged?.Invoke();
            if (foldoutSnapshot != null)
                OnFoldoutStateRestoreRequested?.Invoke(foldoutSnapshot);

            if (selectionState != null)
            {
                selectionState.ClearSelection(window);
                EditorUtility.SetDirty(selectionState);
            }

            Undo.CollapseUndoOperations(undoGroup);
        }

        // Tracks all live MoveUndoRedoHandler instances so they can be bulk-unregistered
        // when the lifecycle manager is disposed (window closes or reinitialises).
        readonly List<MoveUndoRedoHandler> moveHandlers = new List<MoveUndoRedoHandler>();

        /// <summary>
        /// Unregisters all accumulated <see cref="MoveUndoRedoHandler"/> instances from
        /// <see cref="Undo.undoRedoPerformed"/>. Must be called when the window closes or
        /// reinitialises to prevent handlers from accumulating across sessions.
        /// </summary>
        public void DisposeMoveHandlers()
        {
            foreach (var h in moveHandlers)
                Undo.undoRedoPerformed -= h.OnUndoRedo;
            moveHandlers.Clear();
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // Creation Undo-Redo Handler
        // ──────────────────────────────────────────────────────────────────────────────

        readonly List<CreationUndoRedoHandler> creationHandlers = new List<CreationUndoRedoHandler>();

        /// <summary>
        /// Unregisters all accumulated <see cref="CreationUndoRedoHandler"/> instances.
        /// Must be called when the window closes to prevent handlers from accumulating.
        /// </summary>
        public void DisposeCreationHandlers()
        {
            foreach (var h in creationHandlers)
                Undo.undoRedoPerformed -= h.OnUndoRedo;
            creationHandlers.Clear();
        }

        void RegisterCreationUndoRedoHandler(int childInstanceId, string parentAssetPath)
        {
            var handler = new CreationUndoRedoHandler(childInstanceId, parentAssetPath);
            creationHandlers.Add(handler);
            Undo.undoRedoPerformed += handler.OnUndoRedo;
        }

        /// <summary>
        /// Handles <see cref="Undo.undoRedoPerformed"/> for a single child-creation operation.
        /// Unity's undo system does not automatically re-embed a sub-asset in its parent
        /// .asset file when <see cref="Undo.RegisterCreatedObjectUndo"/> is redone; this
        /// handler detects the redo and calls <see cref="AssetDatabase.AddObjectToAsset"/>
        /// so the child is properly persisted and the next deferred refresh finds it in the file.
        /// </summary>
        class CreationUndoRedoHandler
        {
            readonly int childInstanceId;
            readonly string parentAssetPath;
            bool isCurrentlyPresent = true;

            public CreationUndoRedoHandler(int childInstanceId, string parentAssetPath)
            {
                this.childInstanceId = childInstanceId;
                this.parentAssetPath = parentAssetPath;
            }

            public void OnUndoRedo()
            {
                var child = EditorUtility.InstanceIDToObject(childInstanceId) as ScriptableObject;

                if (isCurrentlyPresent && child == null)
                {
                    // Undo detected: child was destroyed by the undo system.
                    // The deferred SaveAssets + reimport in OnUndoRedoPerformed removes it from
                    // the file automatically — no AssetDatabase op needed here.
                    isCurrentlyPresent = false;
                }
                else if (!isCurrentlyPresent && child != null)
                {
                    // Redo detected: child was re-created by the undo system.
                    // Re-embed in the parent asset file if Unity's redo did not do so.
                    string currentPath = AssetDatabase.GetAssetPath(child);
                    if (string.IsNullOrEmpty(currentPath) && !string.IsNullOrEmpty(parentAssetPath))
                    {
                        try { AssetDatabase.AddObjectToAsset(child, parentAssetPath); }
                        catch (Exception ex) { Debug.LogWarning($"CreationUndoRedoHandler: could not re-embed child on redo — {ex.Message}"); }
                    }
                    // Ensure the parent path is tracked so the deferred ScheduleRefreshAffectedAssets
                    // (already queued by OnUndoRedoPerformed) will save and reimport the file.
                    SubAssetRefreshUtils.RegisterAffectedAsset(child);
                    isCurrentlyPresent = true;
                }
            }
        }

        /// <summary>
        /// Subscribes to <see cref="Undo.undoRedoPerformed"/> and re-runs or reverses the
        /// sub-asset file migration whenever Unity's undo system reverts/re-applies the
        /// serialized children-list changes recorded by <see cref="MoveItemsToBase"/>.
        /// </summary>
        void RegisterMoveUndoRedoHandler(MoveOperationRecord record)
        {
            // A dedicated class is used so that its method group is implicitly compatible with
            // Undo.UndoRedoCallback (avoids System.Action / delegate type-mismatch errors) and
            // so that it can unsubscribe itself without a self-referencing lambda.
            var handler = new MoveUndoRedoHandler(record, this);
            moveHandlers.Add(handler);
            Undo.undoRedoPerformed += handler.OnUndoRedo;
        }

        /// <summary>
        /// Handles <see cref="Undo.undoRedoPerformed"/> for a single Move-to-Base operation.
        /// Detects whether an undo or redo just happened by comparing the target Base's
        /// children list with the last known logical state, then mirrors the sub-asset
        /// file relocation that the AssetDatabase does not undo automatically.
        /// </summary>
        class MoveUndoRedoHandler
        {
            readonly MoveOperationRecord record;
            readonly WeakReference<StructureLifecycleManager> weakManager;

            public MoveUndoRedoHandler(MoveOperationRecord record, StructureLifecycleManager manager)
            {
                this.record = record;
                weakManager = new WeakReference<StructureLifecycleManager>(manager);
            }

            public void OnUndoRedo()
            {
                // Auto-unregister if the lifecycle manager was collected (window closed).
                if (!weakManager.TryGetTarget(out var self))
                {
                    Undo.undoRedoPerformed -= OnUndoRedo;
                    return;
                }

                // Auto-unregister if the target asset was deleted.
                // For Container-target moves targetParent is set; for Base-target moves targetBase is set.
                var effectiveParent = record.targetParent ?? (record.targetBase as IStructure);
                if ((effectiveParent as Object) == null)
                {
                    Undo.undoRedoPerformed -= OnUndoRedo;
                    return;
                }

                var validItems = record.movedItems.Where(item => item != null).ToList();

                if (validItems.Count == 0)
                {
                    Undo.undoRedoPerformed -= OnUndoRedo;
                    return;
                }

                // Check the full subtree, not just direct children. After a cross-window
                // drag's ReparentItems step, moved containers may sit inside a sub-Container
                // of effectiveParent rather than as its direct children. A direct-children
                // check would see anyItemInTarget=false and falsely trigger the undo path,
                // corrupting isCurrentlyInTarget and misfiring sub-asset migrations.
                bool anyItemInTarget  = validItems.Any(item => IsInSubtree(item, effectiveParent));
                bool allItemsInTarget = validItems.All(item => IsInSubtree(item, effectiveParent));

                if (record.isCurrentlyInTarget && !anyItemInTarget)
                {
                    // ── UNDO detected ─────────────────────────────────────────────────
                    // Items are no longer in the target Base's children list.
                    // Flip the logical flag NOW (so rapid undo/redo reads correct state),
                    // but DEFER the AssetDatabase relocation by one editor frame.
                    //
                    // CRITICAL: RemoveObjectFromAsset / AddObjectToAsset / SaveAssets MUST NOT
                    // run synchronously inside undoRedoPerformed. At this point Unity's internal
                    // sub-asset / native-object registry has NOT yet settled the objects the undo
                    // just destroyed/restored. Mutating it here corrupts native asset state; the
                    // corruption surfaces later as a native heap crash ("Size overflow in
                    // allocator" in DragAndDrop::FetchDataFromDrag) on the NEXT drag — the
                    // transfer→undo→drag repro. BrowserWindow.OnUndoRedoPerformed defers its own
                    // SaveAssets for exactly this reason; this handler must do the same.
                    record.isCurrentlyInTarget = false;
                    var migrated = record.migratedObjects;
                    EditorApplication.delayCall += () =>
                    {
                        // Reverse the sub-asset migration in REVERSE order so that parents are
                        // returned to the source file before their children (safe removal order).
                        for (int i = migrated.Count - 1; i >= 0; i--)
                        {
                            var (obj, fromPath) = migrated[i];
                            if (obj == null || string.IsNullOrEmpty(fromPath)) continue;
                            string currentPath = AssetDatabase.GetAssetPath(obj);
                            if (string.IsNullOrEmpty(currentPath) || currentPath == fromPath) continue;
                            AssetDatabase.RemoveObjectFromAsset(obj);
                            AssetDatabase.AddObjectToAsset(obj, fromPath);
                        }
                        AssetDatabase.SaveAssets();
                        self.OnStructureChanged?.Invoke();
                    };
                }
                else if (!record.isCurrentlyInTarget && allItemsInTarget)
                {
                    // ── REDO detected ─────────────────────────────────────────────────
                    // Items are back in the target Base's children list.
                    // Flip the flag now; defer the relocation (see UNDO branch for why a
                    // synchronous AssetDatabase mutation here corrupts native state).
                    record.isCurrentlyInTarget = true;
                    var migrated = record.migratedObjects;
                    string targetBasePath = record.targetBasePath;
                    EditorApplication.delayCall += () =>
                    {
                        // Re-apply the sub-asset migration in children-first order.
                        foreach (var (obj, _) in migrated)
                        {
                            if (obj == null) continue;
                            string currentPath = AssetDatabase.GetAssetPath(obj);
                            if (string.IsNullOrEmpty(currentPath) || currentPath == targetBasePath) continue;
                            AssetDatabase.RemoveObjectFromAsset(obj);
                            AssetDatabase.AddObjectToAsset(obj, targetBasePath);
                        }
                        AssetDatabase.SaveAssets();
                        self.OnStructureChanged?.Invoke();
                    };
                }
                // else: unrelated undo/redo event — nothing to do.
            }
        }

        /// <summary>
        /// Calls <see cref="Undo.RegisterCompleteObjectUndo"/> on <paramref name="obj"/> and
        /// every IStructure descendant so that the full prior serialized state is captured
        /// before any mutations are applied.
        /// </summary>
        static void RegisterUndoRecursive(Object obj, string operationName)
        {
            if (obj == null) return;
            Undo.RegisterCompleteObjectUndo(obj, operationName);
            if (obj is IStructure structure)
            {
                var children = structure.ChildrenObjects;
                if (children == null) return;
                foreach (var child in children)
                    RegisterUndoRecursive(child, operationName);
            }
        }

        /// <summary>
        /// Re-embeds <paramref name="obj"/> and all of its IStructure descendants into
        /// <paramref name="targetAssetPath"/> when they currently live in a different asset file.
        /// Only objects whose current asset path matches <paramref name="sourceAssetPath"/> are
        /// relocated — sub-assets from unrelated files (e.g. AudioMixerGroup inside a Unity mixer,
        /// Materials embedded in a model) are left in place.
        /// Children are migrated before their parent (safe order for Unity's asset database).
        /// Returns the list of <c>(obj, fromPath)</c> pairs for every object that was actually
        /// relocated — this list is used to reverse the migration on Undo.
        /// </summary>
        static List<(Object obj, string fromPath)> MigrateSubAssetsRecursive(
            Object obj, string targetAssetPath, string sourceAssetPath)
        {
            var migrated = new List<(Object, string)>();
            MigrateSubAssetsRecursiveInternal(obj, targetAssetPath, sourceAssetPath, migrated);
            return migrated;
        }

        /// <summary>
        /// Returns true when <paramref name="item"/> appears anywhere in the IStructure
        /// subtree rooted at <paramref name="root"/> (direct child or deeper).
        /// Used by <see cref="MoveUndoRedoHandler"/> to distinguish "item was undone out of
        /// the subtree" from "item was reparented within the subtree by ReparentItems".
        /// </summary>
        static bool IsInSubtree(Object item, IStructure root)
        {
            if (root == null || item == null) return false;
            var children = root.ChildrenObjects;
            if (children == null) return false;
            foreach (var child in children)
            {
                if (ReferenceEquals(child, item)) return true;
                if (child is IStructure childStructure && IsInSubtree(item, childStructure))
                    return true;
            }
            return false;
        }

        static void MigrateSubAssetsRecursiveInternal(Object obj, string targetAssetPath,
                                                       string sourceAssetPath,
                                                       List<(Object, string)> migrated)
        {
            if (obj == null || string.IsNullOrEmpty(targetAssetPath)) return;

            // Children first so they are in the target file before their parent is moved.
            if (obj is IStructure structure)
            {
                var children = structure.ChildrenObjects?.ToList();
                if (children != null)
                    foreach (var child in children)
                        MigrateSubAssetsRecursiveInternal(child, targetAssetPath, sourceAssetPath, migrated);
            }

            // Only relocate embedded sub-assets; standalone .asset files stay where they are.
            if (!AssetDatabase.IsSubAsset(obj)) return;

            string currentPath = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(currentPath) || currentPath == targetAssetPath) return;

            // Guard: only migrate sub-assets that originated in the same source file as the
            // top-level item being moved. Sub-assets from unrelated files — AudioMixerGroup
            // inside a Unity AudioMixer, Materials embedded in a model, etc. — must not be
            // ripped out of their owner and embedded into a SecondBrain profile file.
            if (!string.Equals(currentPath, sourceAssetPath, StringComparison.OrdinalIgnoreCase))
                return;

            // Record before relocating so we can reverse this step on Undo.
            migrated.Add((obj, currentPath));
            AssetDatabase.RemoveObjectFromAsset(obj);
            AssetDatabase.AddObjectToAsset(obj, targetAssetPath);
        }

        /// <summary>
        /// Finalizes a newly-constructed child by embedding it into the parent's asset (when applicable),
        /// adding it to the parent's children list, registering undo, and persisting changes.
        /// This centralizes asset/undo wiring in the lifecycle manager instead of scattered utilities.
        /// </summary>
        void FinalizeNewChild(Object parentObj, Object newChild, string specifiedName = null)
        {
            if (parentObj == null || newChild == null) return;

            if (parentObj is not IStructure parentStruct)
                return;

            try
            {
                // If both parent and child are ScriptableObjects, embed the child as a sub-asset of the parent file
                if (parentObj is ScriptableObject scriptableParent && newChild is ScriptableObject scriptableChild)
                {
                    var assetPath = AssetDatabase.GetAssetPath(scriptableParent);

                    // Apply specified name or generate a unique one
                    if (!string.IsNullOrEmpty(specifiedName))
                        scriptableChild.name = specifiedName;
                    else
                    {
                        var baseName = string.IsNullOrEmpty(scriptableChild.name) ? scriptableChild.GetType().Name : scriptableChild.name;
                        scriptableChild.name = AssetUtils.GenerateUniqueName(baseName, parentStruct);
                    }

                    if (!string.IsNullOrEmpty(assetPath))
                    {
                        AssetDatabase.AddObjectToAsset(scriptableChild, assetPath);
                        // Register the child BEFORE the parent so undo processes them in the
                        // correct order: parent is undone first (child still alive → redo snapshot
                        // captures parent.children with a live reference), child is undone second.
                        // On redo the order reverses: child re-created first, then parent.children
                        // restored with the live reference.
                        Undo.RegisterCreatedObjectUndo(scriptableChild, "Create Child Asset");
                        RegisterCreationUndoRedoHandler(scriptableChild.GetInstanceID(), assetPath);
                    }
                }
                else if (parentObj is ScriptableObject scriptableParentForAsset && !(newChild is ScriptableObject))
                {
                    // Non-ScriptableObject children (e.g. TextAsset) must also be embedded as
                    // sub-assets so they are persisted in the parent's .asset file and have a
                    // valid asset path. Without this call GetAssetPath returns "" and the object
                    // is lost on the next domain reload.
                    var assetPath = AssetDatabase.GetAssetPath(scriptableParentForAsset);

                    if (!string.IsNullOrEmpty(specifiedName))
                        newChild.name = specifiedName;
                    else if (string.IsNullOrEmpty(newChild.name))
                        newChild.name = newChild.GetType().Name;

                    if (!string.IsNullOrEmpty(assetPath))
                    {
                        AssetDatabase.AddObjectToAsset(newChild, assetPath);
                        Undo.RegisterCreatedObjectUndo(newChild, "Create Child Asset");
                        RegisterCreationUndoRedoHandler(newChild.GetInstanceID(), assetPath);
                    }
                }

                // Apply default color settings from BrowserSettings to new containers
                if (newChild is IHasColor colorable)
                {
                    colorable.LabelColorStyle = BrowserSettings.DefaultColorStyle;
                    colorable.LabelColorFoldoutOnly = BrowserSettings.DefaultColorFoldoutOnly;
                }

                // Apply default expand option to newly-created Containers so they inherit the
                // user's global preference from BrowserSettings.
                try
                {
                    if (newChild is Container cont)
                    {
                        cont.DefaultExpand = BrowserSettings.DefaultExpandOption;
                        EditorUtility.SetDirty(cont as Object);
                    }
                }
                catch { }

                // Record parent undo AFTER the child's undo record so that on undo the parent
                // record is reversed first (while the child is still alive).  This ensures the
                // redo snapshot Unity captures for the parent contains a live child reference.
                Undo.RegisterCompleteObjectUndo(parentObj as Object, "Add Child");

                // Add to parent's children list (in-memory) using the IStructure API
                var addResult = parentStruct.AddChild(newChild);
                if (addResult != AddChildResult.Success)
                {
                    // Surface a notification to the window when the add failed
                    try { EditorGUIUtils.ShowNotification(window, new GUIContent(OperationErrorUtils.GetDisplayedAddChildErrorMessage(addResult))); } catch { }
                }

                EditorUtility.SetDirty(parentObj as Object);
                AssetDatabase.SaveAssets();

                // Targeted import so the Project window reflects the updated sub-assets
                try
                {
                    var parentPath = AssetDatabase.GetAssetPath(parentObj as Object);
                    if (!string.IsNullOrEmpty(parentPath))
                        SubAssetRefreshUtils.ImportAndRegister(parentPath);
                }
                catch { }
            }
            catch (Exception ex)
            {
                Debug.LogError($"FinalizeNewChild failed: {ex}");
            }
        }
    }
}
