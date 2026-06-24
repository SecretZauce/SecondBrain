using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SecretZauce.SecondBrain.Editor
{
    public abstract class BrowserWindow : EditorWindow
    {
        Vector2 leftScroll;
        Vector2 rightScroll;

        public BrowserController Controller { get; private set; }
        ItemDetailPanel detailPanel;
        
        public TreeView TreeView => treeView;
        TreeView treeView;
        
        BrowserToolbar toolbar;
        Splitter splitter;

        // Stored handlers so we can subscribe/unsubscribe cleanly
        Action<int[]> selectionRequestedHandler;
        Action<Dictionary<object, bool>> foldoutRestoreHandler;

        SerializedObject serializedDatabase;
        SelectionStateSO selectionState;
      
        // Preserved UndoHelper across reinitializations to maintain focus history
        UndoHelper undoHelper;
        
        public List<IStructure> Collections => collections;
        List<IStructure> collections;

        string preservedSearchKeyword; // Preserve search across refresh
        bool justCompletedRename; // Track if rename just completed to prevent focus
        bool showDetailPanel;
        public bool hasDetailPanel;

        // Controls whether the top toolbar is visible for this BrowserWindow
        bool topToolbarVisible = true;
        public bool TopToolbarVisible
        {
            get => topToolbarVisible;
            set
            {
                if (topToolbarVisible == value) return;
                topToolbarVisible = value;
                try { Repaint(); } catch { }
            }
        }

        public bool IsPopup => isPopupMode;
        bool isPopupMode;
        // True when the current foldout state was produced by ExpandAllOnEnterBase rather than
        // by manual user interaction.  Used by SaveFoldoutStateToEditor to avoid persisting the
        // auto-expanded state so re-entry with the setting off sees per-container defaults.
        bool foldoutExpandedOnEntry;
        
#if SECOND_BRAIN_PRO
        QuickPeekHandlerBase quickPeekHandler;
#endif
        
        /// <summary>
        /// Persisted GUID that uniquely identifies this window instance across domain reloads.
        /// Used in EditorPrefs keys so multiple windows of the same type get independent state.
        /// </summary>
        [SerializeField] string windowGUID;

        /// <summary>
        /// Per-instance navigated target. Null means "home" (HomeRoot).
        /// </summary>
        IStructure targetRoot;

        /// <summary>
        /// The default root for this window type (e.g. Motherbase.Instance, APIDatabase.Instance).
        /// Subclasses must implement this instead of Root.
        /// </summary>
        protected abstract IStructure HomeRoot { get; }

        /// <summary>
        /// The current root for this window — returns the navigated target if set, otherwise the HomeRoot.
        /// </summary>
        public IStructure Root => targetRoot ?? HomeRoot;

        /// <summary>
        /// Gets or sets whether the right-side ItemDetailPanel is visible.
        /// When false, the TreeView fills the entire window width.
        /// </summary>
        protected internal bool ShowDetailPanel
        {
            get => showDetailPanel;
            private set
            {
                if (showDetailPanel == value) return;
                showDetailPanel = value;
                // If hiding the panel, reset any active resize state
                if (!showDetailPanel && splitter != null)
                    splitter.CancelResize();
                Repaint();
            }
        }

        public static void OpenWindow<T>() where T : BrowserWindow
        {
            var wnd = GetWindow<T>();
            InitializeWindow(wnd);
        }

        static void InitializeWindow(BrowserWindow wnd)
        {
            wnd.minSize = new Vector2(300, 300);
            wnd.RefreshSerializedDatabase();
        }

        /// <summary>
        /// Returns true when this window is viewing the HomeRoot (no sub-target navigation).
        /// </summary>
        public bool IsAtHome() => targetRoot == null;

        /// <summary>Exposes selection state for Pro cross-window drag-out operations.</summary>
        public SelectionStateSO SelectionState => selectionState;

        /// <summary>
        /// Rebuilds the TreeView from current asset state and repaints.
        /// Used by Pro drag-out to refresh both the source and destination windows
        /// after a cross-window item transfer.
        /// </summary>
        public void RefreshTree()
        {
            // Force the controller to rebuild Collections from the live asset state before
            // RefreshSerializedDatabase reads it. Needed when another controller mutated
            // the root (e.g. cross-window transfer ran on the source window's controller).
            Controller?.ForceRefreshFromRoot();
            RefreshSerializedDatabase();
            Repaint();
        }

        /// <summary>
        /// Navigates this window to the given target. Null means "go home".
        /// Saves foldout state, persists the target to EditorPrefs for this window instance,
        /// and reinitializes the controller for the new root.
        /// </summary>
        public void SetTarget(Base baseTarget)
        {
            // Persist current foldout state before switching targets
            SaveFoldoutStateToEditor();

            // Save the new target to EditorPrefs so we can restore it on window reopen
            try
            {
                var key = $"Browser_LastTarget_{EnsureWindowGUID()}";
                if (baseTarget != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                        baseTarget as ScriptableObject, out string guid, out long local))
                {
                    EditorPrefs.SetString(key, guid + "_" + local.ToString());
                }
                else
                {
                    // Going home — remove saved key
                    if (EditorPrefs.HasKey(key))
                        EditorPrefs.DeleteKey(key);
                }
            }
            catch
            {
                // Ignore EditorPrefs failures
            }

            targetRoot = baseTarget;

            // Invalidate QuickPeek foldout/layout history so stale saves from the previous
            // visit don't override the container's ChildViewExpand default on the next hover.
            if (baseTarget != null)
            {
                try { EditorPrefs.SetInt("QuickPeek_SessionToken", EditorPrefs.GetInt("QuickPeek_SessionToken", 0) + 1); }
                catch { }
            }

            // Reset the flag AFTER saving so SaveFoldoutStateToEditor can still act on the old value,
            // and BEFORE ReinitializeForRootChange so the new base starts with a clean flag.
            foldoutExpandedOnEntry = false;

            // Reinitialize for the new root
            ReinitializeForRootChange();

            // Expand or collapse all containers depending on the setting.
            // Runs after foldout state is loaded so the result always overrides any persisted state.
            // Does not apply when going home (baseTarget == null).
            if (baseTarget != null && treeView != null && collections != null)
            {
                if (BrowserSettings.ExpandAllOnEnterBase)
                {
                    treeView.foldoutState.ExpandAll(collections);
                    foldoutExpandedOnEntry = true;
                }
                else
                {
                    // Clear all saved entries so each container falls back to its own
                    // DefaultExpandOption (AlwaysExpand / ExpandAsDefault / CollapsedAsDefault).
                    treeView.foldoutState.Clear();
                }
            }
        }

        /// <summary>
        /// Closes the window if it is currently in popup mode.
        /// Used when an action is performed that should dismiss the popup (e.g., pressing Enter on a leaf node).
        /// </summary>
        public void TryCloseIfPopup()
        {
            if (isPopupMode)
            {
                Close();
            }
        }

        /// <summary>
        /// Attempt to restore the last known navigation target from EditorPrefs for this window instance.
        /// Called early during OnEnable so the controller initializes with the correct root.
        /// </summary>
        void RestoreTargetOnOpen()
        {
            try
            {
                var key = $"Browser_LastTarget_{EnsureWindowGUID()}";
                if (!EditorPrefs.HasKey(key))
                    return;

                var stored = EditorPrefs.GetString(key);
                if (string.IsNullOrEmpty(stored))
                    return;

                var parts = stored.Split('_');
                if (parts.Length != 2)
                    return;

                var guid = parts[0];
                if (!long.TryParse(parts[1], out var localId))
                    return;

                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(assetPath))
                    return;

                try
                {
                    // Avoid calling LoadAllAssetsAtPath for scene files to prevent threaded reads.
                    var mainType = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
                    bool isSceneAsset = mainType == typeof(UnityEditor.SceneAsset) || assetPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase);

                    if (isSceneAsset)
                    {
                        var main = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                        if (main is Base bMain)
                        {
                            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(bMain as ScriptableObject, out var g2, out var local2))
                            {
                                if (g2 == guid && local2 == localId)
                                {
                                    targetRoot = bMain;
                                    return;
                                }
                            }
                        }
                    }
                    else
                    {
                        // Load all assets at path and try to find the one matching GUID+local id
                        var all = AssetDatabase.LoadAllAssetsAtPath(assetPath) ?? Array.Empty<UnityEngine.Object>();
                        foreach (var obj in all)
                        {
                            if (obj is Base b)
                            {
                                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                                        b as ScriptableObject, out var g2, out var local2))
                                {
                                    if (g2 == guid && local2 == localId)
                                    {
                                        targetRoot = b;
                                        return;
                                    }
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // ignore restore failures
                }
            }
            catch
            {
                // ignore restore failures
            }
        }

        /// <summary>
        /// Ensures the window has a unique GUID for EditorPrefs key discrimination.
        /// Generated once and serialized so it survives domain reloads.
        /// </summary>
        string EnsureWindowGUID()
        {
            if (string.IsNullOrEmpty(windowGUID))
                windowGUID = System.Guid.NewGuid().ToString();
            return windowGUID;
        }

        /// <summary>
        /// Refreshes the window titleContent to reflect the current navigation target.
        /// Shows <see cref="WindowTitle"/> when at home, or "{WindowTitle} › {targetName}" when navigated.
        /// </summary>
        void UpdateWindowTitle()
        {
            if (s_WindowIcon == null)
                s_WindowIcon = Resources.Load<Texture2D>("Editor/Icons/second_brain_icon");

            var targetObj = Root as Object;
            var targetName = targetObj != null ? (targetObj == (Object)HomeRoot ? "Home" : targetObj.name) : null;
            if (Root is IHasEmoji hasEmoji && !string.IsNullOrEmpty(hasEmoji.EmojiIcon)
                && !EmojiIconUtils.IsEditorIcon(hasEmoji.EmojiIcon))
                targetName = hasEmoji.EmojiIcon + " " + targetName;
            string newTitle = targetName;
            titleContent = new GUIContent(newTitle, IsAtHome() ? s_WindowIcon : null);
        }

        protected virtual void OnEnable()
        {
#if !SECOND_BRAIN_PRO
            // Free mode: only one BrowserWindow instance is allowed.
            // Schedule a delayed check so all windows have finished their own OnEnable
            // before we evaluate the count (handles domain-reload restores correctly).
            var selfRef = this;
            EditorApplication.delayCall += () =>
            {
                if (selfRef == null) return;
                var all = Resources.FindObjectsOfTypeAll(selfRef.GetType());
                if (all.Length > 1)
                {
                    selfRef.Close();
                    return;
                }
            };
#else
            quickPeekHandler = ProFeature.Provider.CreateQuickPeekHandler(this);
#endif
            // Enable mouse move events for hover tracking and QuickPeek
            // This is especially important for popup windows to ensure events are properly received
            wantsMouseMove = true;
            wantsMouseEnterLeaveWindow = true;
            
            // Restore last used navigation target for this specific window instance
            try
            {
                RestoreTargetOnOpen();
            }
            catch
            {
                // ignore
            }

            InitializeControllerAndState();
        }

        protected virtual void OnDisable()
        {
            // Persist foldout state so it survives domain reloads (e.g. entering Play Mode)
            try
            {
                SaveFoldoutStateToEditor();
            }
            catch
            {
                // Ignore save failures during shutdown
            }

#if SECOND_BRAIN_PRO
            quickPeekHandler.CloseQuickPeek();
            // Bump after CloseQuickPeek so SaveFoldoutStateIfNeeded captures token T, then the
            // next browser open sees T+1 and treats all saved QuickPeek history as stale.
            try { EditorPrefs.SetInt("QuickPeek_SessionToken", EditorPrefs.GetInt("QuickPeek_SessionToken", 0) + 1); }
            catch { }
#endif
            DeinitializeControllerAndState();
        }

        public void ReinitializeForRootChange()
        {
            DeinitializeControllerAndState();
            InitializeControllerAndState();
            Repaint();
        }

        void InitializeControllerAndState()
        {
            if (Controller is IDisposable disposable)
                disposable.Dispose();

            // Reuse existing UndoHelper or create a new one to preserve focus history across root changes
            if (undoHelper == null)
                undoHelper = new UndoHelper(Root);

            Controller = new BrowserController(this, undoHelper, selectionState);
            selectionState = Controller.SelectionState;
            Controller.Initialize();
            serializedDatabase = Controller.SerializedDatabase;
            collections = Controller.Collections;

            Controller.OnStructureChanged += RefreshSerializedDatabase;
            selectionRequestedHandler = (path) =>
            {
                selectionState.SetSingleSelection(this, path);
                EditorUtility.SetDirty(selectionState);
                Repaint();
            };
            Controller.OnSelectionRequested += selectionRequestedHandler;

            foldoutRestoreHandler = (foldoutSnapshot) => { treeView?.SetFoldoutSnapshot(foldoutSnapshot); };
            Controller.OnFoldoutStateRestoreRequested += foldoutRestoreHandler;
            Controller.OnExpansionRequested += ProcessExpansion;

            RefreshSerializedDatabase();

            // Subscribe to editor update to poll mouse-over/focus during drags
            EditorApplication.update += BrowserWindowOnEditorUpdate;
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
            BrowserSettings.OnSettingsChanged += OnBrowserSettingsChanged;
            SelectionStateSO.OnSelectionChangedInternally += OnSelectionChangedInternally;
            ProfileManager.OnActiveProfileChanged += OnActiveProfileChanged;

            detailPanel = new ItemDetailPanel();
            detailPanel.SetScrollPosition(rightScroll);
            toolbar = new BrowserToolbar(this);
            splitter = new Splitter(initialLeftWidth: 300f, minPaneWidth: 150f, separatorWidth: 2f,
                separatorHitWidth: 8f);

            UpdateWindowTitle();
        }

        void DeinitializeControllerAndState()
        {
            SelectionStateSO.OnSelectionChangedInternally -= OnSelectionChangedInternally;
            ProfileManager.OnActiveProfileChanged -= OnActiveProfileChanged;
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            BrowserSettings.OnSettingsChanged -= OnBrowserSettingsChanged;

            if (Controller != null)
            {
                Controller.OnStructureChanged -= RefreshSerializedDatabase;
                if (selectionRequestedHandler != null)
                    Controller.OnSelectionRequested -= selectionRequestedHandler;
                if (foldoutRestoreHandler != null)
                    Controller.OnFoldoutStateRestoreRequested -= foldoutRestoreHandler;
                Controller.OnExpansionRequested -= ProcessExpansion;
                if (Controller is IDisposable disposable)
                    disposable.Dispose();
            }

            // Unsubscribe update handler
            try
            {
                EditorApplication.update -= BrowserWindowOnEditorUpdate;
            }
            catch
            {
            }
            
            Controller = null;
            serializedDatabase = null;
            collections = null;
        }

        void OnHierarchyChanged() => Repaint();

        void OnSelectionChangedInternally(BrowserWindow selectorWindow)
        {
            // Repaint this window if a different window changed its selection
            // This allows multiple windows to show their independent selection states
            if (selectorWindow != this) 
            {
                try
                {
                    Repaint();
                }
                catch
                {
                    // Window might be in the process of being destroyed, ignore
                }
            }
        }

        public void SaveFoldoutStateToEditor()
        {
            // If the current foldout state was produced by ExpandAllOnEnterBase, do NOT persist it.
            // Saving an all-expanded state would cause re-entry with the setting off to still show
            // everything expanded.  Instead, clear the stored key so the next visit falls back to
            // per-container DefaultExpand settings.
            if (foldoutExpandedOnEntry)
            {
                try
                {
                    string clearKey = GetFoldoutPrefsKey();
                    if (!string.IsNullOrEmpty(clearKey) && EditorPrefs.HasKey(clearKey))
                        EditorPrefs.DeleteKey(clearKey);
                }
                catch { }
                return;
            }

            try
            {
                var snapshot = treeView.GetFoldoutSnapshot();

                // Let FoldoutState handle conversion to EditorPrefs; create a temporary FoldoutState to reuse logic
                var temp = new FoldoutState();
                temp.Restore(snapshot);
                string key = GetFoldoutPrefsKey();
                if (!string.IsNullOrEmpty(key))
                {
                    temp.SaveToEditorPrefs(key);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to save TreeView foldout state: {ex.Message}");
            }
        }

        void BrowserWindowOnEditorUpdate()
        {
            try
            {
                if (treeView == null)
                    return;

#if SECOND_BRAIN_PRO
                // Runs every editor update frame so the popup closes promptly when the
                // mouse leaves both the BrowserWindow and the QuickPeekWindow.
                quickPeekHandler.HandleQuickPeekCloseCheck();
#endif
                
                var dm = treeView.DragDropManager;
                if (dm == null)
                    return;

                if (!dm.IsDragging && !dm.IsPotentialDrag)
                    return;

#if SECOND_BRAIN_PRO
                // Close peek while a drag is in progress
                if (dm.IsDragging)
                    quickPeekHandler.CloseQuickPeek();
#endif

                var windowUnderMouse = mouseOverWindow;
                bool mouseOutsideWindow = windowUnderMouse != this;
                if (!mouseOutsideWindow && windowUnderMouse == this)
                {
                    // mouse still over this window; nothing to do
                }
                else
                {
                    if (dm.IsDragging && !dm.IsExternalDrag)
                    {
#if SECOND_BRAIN_PRO
                        // Unity's external DnD is active — skip cancel; DragExited handles cleanup.
                        if (ProFeature.Provider?.IsCrossWindowDragFromThisWindow(this) == true)
                            return;
#endif
                        dm.CancelDrag();
                        Repaint();
                        return;
                    }

                    if (dm.IsPotentialDrag)
                    {
                        dm.CancelPotentialDrag();
                        Repaint();
                        return;
                    }
                }
            }
            catch
            {
                // ignore
            }
        }

        void OnBrowserSettingsChanged()
        {
            Repaint();
        }

        void OnActiveProfileChanged()
        {
            Debug.Log($"[SB-DEBUG] OnActiveProfileChanged fired — targetRoot was: {(targetRoot == null ? "null(C#)" : (targetRoot is UnityEngine.Object o && !o ? "fake-null" : targetRoot.ToString()))}");
            try
            {
                // Reset navigation target since we're switching to a different Profile
                targetRoot = null;
                ReinitializeForRootChange();
                Repaint();
            }
            catch
            {
                // ignore
            }
        }

        void OnUndoRedoPerformed()
        {
            // Cancel any active drag operation before refreshing
            treeView?.CancelDrag();

            // First, check if we need to restore focus to a different target
            // Do this BEFORE rebuilding UI to avoid double-rebuild
            bool focusChanged = false;
            try
            {
                focusChanged = Controller?.UndoHelper?.RestoreFocusFromUndo(Controller) ?? false;
            }
            catch
            {
                // Ignore focus restore errors
            }

            // If focus didn't change, we need to refresh the UI manually
            // (If focus changed, SetTarget already called ReinitializeForRootChange)
            if (!focusChanged)
            {
                // CRITICAL: Refresh controller's collections from root BEFORE rebuilding TreeView
                // Unity's undo restores serialized fields, but we need to rebuild the collections list
                Controller?.Initialize();
                RefreshSerializedDatabase();
                if (Controller?.LastFoldoutSnapshot != null)
                    TreeView.SetFoldoutSnapshot(Controller.LastFoldoutSnapshot);
            }

            // Targeted Project-window refresh: schedule a deferred save + reimport of only the
            // parent assets that were touched by the last mutation.
            // The call is deferred (via EditorApplication.delayCall inside Schedule…) so it runs
            // AFTER Unity has fully settled its internal undo state — particularly the AssetDatabase
            // sub-asset registry.  Calling SaveAssets/ImportAsset synchronously here, while still
            // inside the undoRedoPerformed callback, causes "NativeFormatImporter generated
            // inconsistent result" because the registry hasn't reflected the destroyed/restored
            // sub-assets yet.  One editor frame of delay is enough for that to settle.
            SubAssetRefreshUtils.ScheduleRefreshAffectedAssets();

            // Restore selection synchronously (no delay) to eliminate lag
            try
            {
                Controller?.RestoreSelection();
            }
            catch
            {
                // Ignore selection restore errors
            }

            // Refresh title in case a rename was undone/redone on the current target
            UpdateWindowTitle();

            // Single repaint at the end
            Repaint();
        }

        void RefreshSerializedDatabase()
        {
            serializedDatabase = Controller.SerializedDatabase;
            collections = Controller.Collections;

            // After a domain reload, the first AssetDatabase.StopAssetEditing() can trigger
            // a full reimport that destroys the ScriptableObject instance targetRoot points to,
            // turning it into a Unity "fake null" (non-null C# reference, destroyed native
            // object). BrowserController.RefreshFromRoot() sees a null root and returns null
            // Collections. IsAtHome() then returns false (C# null check misses the fake null),
            // so CheckAndNotify enters the navigated branch, finds targetBase == null via
            // Unity's overloaded == operator, and triggers a false home navigation.
            // Detect this and recover by re-loading the target from EditorPrefs.
            if (collections == null && targetRoot is UnityEngine.Object targetObj && !targetObj)
            {
                var staleRoot = targetRoot;   // keep fake-null as fallback
                targetRoot = null;
                RestoreTargetOnOpen();
                if (targetRoot != null)
                {
                    Controller.ForceRefreshFromRoot();
                    serializedDatabase = Controller.SerializedDatabase;
                    collections = Controller.Collections;
                }
                else
                {
                    // AssetDatabase GUID table hasn't settled yet (we're still inside
                    // StopAssetEditing's synchronous import). Revert to the stale ref so
                    // IsAtHome() stays false — CheckAndNotify's navigated-branch guard
                    // (targetBase == null → continue) will skip this window. The deferred
                    // RefreshSerializedDatabase queued by TryHandleDrop will retry once the
                    // GUID table is fully updated (next editor frame).
                    targetRoot = staleRoot;
                }
            }

            if (treeView != null)
            {
                treeView.CancelDrag();
                preservedSearchKeyword = treeView.GetSearchKeyword();
                treeView.Cleanup();
            }

#if SECOND_BRAIN_PRO
            // Close any open QuickPeek popup since the tree is about to be rebuilt
            quickPeekHandler.CloseQuickPeek();
#endif

            treeView = new TreeView(collections);
            treeView.SetScrollPosition(leftScroll);
            if (!string.IsNullOrEmpty(preservedSearchKeyword))
                treeView.SetSearchKeyword(preservedSearchKeyword);
            
            TryLoadEditorSavedFoldoutState();
        }

        void TryLoadEditorSavedFoldoutState()
        {
            try
            {
                string key = GetFoldoutPrefsKey();
                if (!string.IsNullOrEmpty(key))
                {
                    treeView.foldoutState.LoadFromEditorPrefs(key, collections);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to load TreeView foldout state: {ex.Message}");
            }
        }

        string GetFoldoutPrefsKey()
        {
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(Root as ScriptableObject,
                    out string guid, out long local))
                return null;

            return !string.IsNullOrEmpty(guid) ? $"Browser_Foldouts_{EnsureWindowGUID()}_{guid}_{local}" : null;
        }

        void OnGUI()
        {
            if (!TryPrepareBeforeDrawContent())
                return;
            
            if (TopToolbarVisible && toolbar != null)
            {
                toolbar.Draw();
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                // Maintain matching Begin/End layout calls even when toolbar is hidden
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.EndHorizontal();
            }

            // Handle separator resizing FIRST - before any controls can consume events
            // This ensures resize dragging works reliably
            if (showDetailPanel && splitter != null)
            {
                splitter.HandleEvents(Event.current, position, out var leftChanged);
                if (splitter.IsResizing && Event.current.type == EventType.MouseDrag)
                    return; // keep previous behavior: don't process other mouse events while dragging
                if (leftChanged)
                    Repaint();
            }

            // DragExited handling moved into TreeViewDragInput.ProcessGlobalDragEvent
            DrawMainContent();
            // Draw target-base tag line (one-liner at bottom) when navigated to a Base target
            DrawTargetBaseTagLine();

#if SECOND_BRAIN_PRO
            // After drawing the tree (hover state is now current), update the QuickPeek popup.
            if (SettingsWindow.IsVisible)
            {
                quickPeekHandler.CloseQuickPeek();
            }
            else
            {
                quickPeekHandler.UpdateQuickPeek();
            }
#endif

            if (TryProcessDragAndDrop())
                return;

            if (serializedDatabase != null && serializedDatabase.hasModifiedProperties)
            {
                serializedDatabase.ApplyModifiedProperties();
                RefreshSerializedDatabase();
            }

            if (treeView.DragDropManager.IsDragging)
                Repaint();
        }

        // (QuickPeek handling implemented further down in the file; keep a single
        // UpdateQuickPeek/CloseQuickPeek implementation to avoid duplicate members.)


        void DrawMainContent()
        {
            // Draw left pane (fixed width when detail panel is visible, full width otherwise)
            GUILayout.BeginHorizontal();
            if (hasDetailPanel && showDetailPanel)
            {
                GUILayout.BeginVertical(GUILayout.Width(splitter?.LeftPaneWidth ?? 300f));
            }
            else
            {
                GUILayout.BeginVertical();
            }

            DrawLeftPane();
            GUILayout.EndVertical();

            // Draw resizable separator and right pane only when detail panel is visible
            if (hasDetailPanel && showDetailPanel)
            {
                // Draw separator visual using splitter
                splitter?.DrawSeparator(position);
                GUILayout.BeginVertical();
                DrawRightPane();
                GUILayout.EndVertical();
            }

            GUILayout.EndHorizontal();
        }

        bool TryPrepareBeforeDrawContent()
        {
            // Handle keyboard navigation FIRST, before any controls consume the events
            if (Event.current != null && Event.current.type == EventType.KeyDown)
                HandleKeyboardNavigation();

            if (serializedDatabase == null || collections == null)
            {
                EditorGUILayout.HelpBox(
                    "The root resource not found in Resources. Create or place an asset.",
                    MessageType.Warning);
                if (GUILayout.Button("Refresh"))
                    RefreshSerializedDatabase();
                return false;
            }

            serializedDatabase.Update();
            return true;
        }

        bool TryProcessDragAndDrop()
        {
            if (Event.current == null)
                return false;

            // If mouse leaves the entire EditorWindow, cancel any internal drag or potential drag.
            if (Event.current.type == EventType.MouseLeaveWindow)
            {
                if (treeView != null)
                {
                    var dm = treeView.DragDropManager;
                    if (dm != null)
                    {
                        if (dm.IsDragging && !dm.IsExternalDrag)
                        {
#if SECOND_BRAIN_PRO
                            if (ProFeature.Provider?.IsCrossWindowDragFromThisWindow(this) != true)
#endif
                            {
                                dm.CancelDrag();
                                Event.current.Use();
                                Repaint();
                                return true;
                            }
                        }

                        if (dm.IsPotentialDrag)
                        {
                            dm.CancelPotentialDrag();
                            Event.current.Use();
                            Repaint();
                            return true;
                        }
                    }
                }
            }

            // Fallback: if MouseLeaveWindow isn't delivered, detect cursor leaving the window using screen coordinates
            try
            {
                if (treeView != null)
                {
                    var dm2 = treeView.DragDropManager;
                    if (dm2 != null && (dm2.IsDragging || dm2.IsPotentialDrag))
                    {
                        // Prefer EditorWindow.mouseOverWindow which directly reports the window under the cursor
                        var windowUnderMouse = EditorWindow.mouseOverWindow;
                        if (windowUnderMouse != this)
                        {
                            if (dm2.IsDragging && !dm2.IsExternalDrag)
                            {
#if SECOND_BRAIN_PRO
                                if (ProFeature.Provider?.IsCrossWindowDragFromThisWindow(this) != true)
#endif
                                {
                                    dm2.CancelDrag();
                                    Event.current.Use();
                                    Repaint();
                                    return true;
                                }
                            }

                            if (dm2.IsPotentialDrag)
                            {
                                dm2.CancelPotentialDrag();
                                Event.current.Use();
                                Repaint();
                                return true;
                            }
                        }
                    }
                }
            }
            catch
            {
                // ignore
            }

#if SECOND_BRAIN_PRO
            // Handle DragExited so the source window can run post-drop actions
            // (e.g. "Create Prefab" dialog when a SceneObjectRef is dropped on the Project Browser).
            // Skip the startup DragExited that Unity fires the instant DragAndDrop.StartDrag() is
            // called — that one fires while IsDragging is still true. The real post-drop DragExited
            // fires after CancelDrag() clears IsDragging, so that one still reaches HandleDragExited.
            if (Event.current.type == EventType.DragExited && !treeView.DragDropManager.IsDragging)
                ProFeature.Provider?.HandleDragExited(this);

            // Reject a cross-window drag from re-entering its own source window to prevent
            // accidental duplication. Only guard after the drag has actually left this window;
            // while it hasn't left yet it may still be an internal reparent drag.
            if (Event.current.type == EventType.DragUpdated
                && ProFeature.Provider?.HasDragLeftSourceWindow(this) == true)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                return false;
            }
#endif

            // Handle external drag into empty Base (no containers yet) — create a default
            // Container on drop and add the dragged assets to it.
            if (Root is Base && (collections == null || collections.Count == 0))
            {
                if (Event.current.type == EventType.DragUpdated &&
                    DragAndDrop.objectReferences != null && DragAndDrop.objectReferences.Length > 0)
                {
                    bool hasUnsavedScene = DragAndDrop.objectReferences.Any(
                        SceneObjectRefUtils.IsSceneObjectFromUnsavedScene);
                    DragAndDrop.visualMode = hasUnsavedScene
                        ? DragAndDropVisualMode.Rejected
                        : DragAndDropVisualMode.Copy;
                    Repaint();
                    return true;
                }

                if (Event.current.type == EventType.DragPerform &&
                    DragAndDrop.objectReferences != null && DragAndDrop.objectReferences.Length > 0)
                {
                    DragAndDrop.AcceptDrag();
                    var droppedItems = DragAndDrop.objectReferences
                        .Where(o => o != null)
                        .ToList();

                    bool hasUnsavedScene = droppedItems.Any(
                        SceneObjectRefUtils.IsSceneObjectFromUnsavedScene);

                    if (hasUnsavedScene)
                    {
                        ShowNotification(new GUIContent(SceneObjectRefUtils.UnsavedSceneNotification));
                    }
                    else if (droppedItems.Count > 0)
                    {
                        Controller.CreateContainerAndAddExternalItems(droppedItems, treeView, selectionState);
                        EditorApplication.delayCall += () =>
                        {
                            RefreshSerializedDatabase();
                            if (Controller.LastFoldoutSnapshot != null)
                                treeView.SetFoldoutSnapshot(Controller.LastFoldoutSnapshot);
                            Repaint();
                        };
                    }

                    Event.current.Use();
                    Repaint();
                    return true;
                }
            }

            // External drag over empty space in a non-empty Base: show Copy/Move cursor so the
            // user knows they can drop here (the tree still has rows, but the mouse is below them).
            if (Root is Base && Event.current.type == EventType.DragUpdated
                && DragAndDrop.objectReferences != null && DragAndDrop.objectReferences.Length > 0
                && treeView.DragDropManager.IsExternalDrag && !treeView.DragInput.HasHover)
            {
#if SECOND_BRAIN_PRO
                if (ProFeature.Provider?.IsCrossWindowDragFromAnotherWindow(this) == true)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Move;
                    Repaint();
                }
                else
#endif
                {
                    bool hasUnsaved = DragAndDrop.objectReferences.Any(
                        SceneObjectRefUtils.IsSceneObjectFromUnsavedScene);
                    DragAndDrop.visualMode = hasUnsaved
                        ? DragAndDropVisualMode.Rejected
                        : DragAndDropVisualMode.Copy;
                    Repaint();
                }
            }

#if SECOND_BRAIN_PRO
            // Override visual mode to Move for cross-window drags landing on a valid row target.
            if (Event.current.type == EventType.DragUpdated
                && ProFeature.Provider?.IsCrossWindowDragFromAnotherWindow(this) == true
                && treeView.DragDropManager.HasValidDropTarget())
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Move;
            }
#endif

            // Let TreeView's DragInput handle global drag decisions first
            var dragResult = treeView.ProcessGlobalDragEvent();
            if (dragResult != null && dragResult.Handled)
            {
                if (dragResult.Cancelled)
                {
                    // External drag dropped on empty space in a Base.
                    // We detect "empty space" by checking that no row was hovered (HasHover == false).
                    if (dragResult.IsExternal && Root is Base && !treeView.DragInput.HasHover
                        && dragResult.DraggedItems != null && dragResult.DraggedItems.Count > 0)
                    {
#if SECOND_BRAIN_PRO
                        // Cross-window: move items from source window to dest Base root
                        if (ProFeature.Provider?.ExecuteCrossWindowTransfer(
                                this, null, (int)DragAndDropManager.DropPosition.None,
                                treeView, selectionState, collections) == true)
                        {
                            Repaint();
                            return true;
                        }
#endif
                        // Normal external drag → create new Container for the dropped assets
                        bool hasUnsaved = dragResult.DraggedItems.Any(
                            SceneObjectRefUtils.IsSceneObjectFromUnsavedScene);
                        if (hasUnsaved)
                        {
                            ShowNotification(new GUIContent(SceneObjectRefUtils.UnsavedSceneNotification));
                        }
                        else
                        {
                            DragAndDrop.AcceptDrag();
                            Controller.CreateContainerAndAddExternalItems(
                                dragResult.DraggedItems, treeView, selectionState);
                            EditorApplication.delayCall += () =>
                            {
                                RefreshSerializedDatabase();
                                if (Controller.LastFoldoutSnapshot != null)
                                    treeView.SetFoldoutSnapshot(Controller.LastFoldoutSnapshot);
                                Repaint();
                            };
                        }
                        Repaint();
                        return true;
                    }

                    treeView.DragDropManager.CancelDrag();
                    Event.current.Use();
                    Repaint();
                    return true;
                }

                if (dragResult.DropPerformed)
                {
                    // Save reference to current drag manager so we can cancel it even if controller causes a UI refresh
                    var originalDragManager = treeView.DragDropManager;

                    if (dragResult.IsExternal)
                    {
#if SECOND_BRAIN_PRO
                        // Cross-window SecondBrain drag: migrate items via MoveItemsByPaths
                        // instead of AddExternalItems (which blocks cross-asset IStructures).
                        if (ProFeature.Provider?.ExecuteCrossWindowTransfer(
                                this, dragResult.DropTargetPath, dragResult.DropPosition,
                                treeView, selectionState, collections) == true)
                        {
                            originalDragManager.CancelDrag();
                            Event.current.Use();
                            Repaint();
                            return true;
                        }
#endif
                        // External drop - add external items
                        Controller.AddExternalItems(
                            dragResult.DraggedItems,
                            dragResult.DropTargetPath,
                            (DragAndDropManager.DropPosition)dragResult.DropPosition,
                            collections,
                            treeView,
                            selectionState);

                        // Delayed fallback to refresh & restore foldouts
                        EditorApplication.delayCall += () =>
                        {
                            RefreshSerializedDatabase();
                            if (Controller.LastFoldoutSnapshot != null)
                                treeView.SetFoldoutSnapshot(Controller.LastFoldoutSnapshot);
                            Repaint();
                        };

                        originalDragManager.CancelDrag();
                        Event.current.Use();
                        Repaint();
                        return true;
                    }
                    else if ((DragAndDropManager.DropPosition)dragResult.DropPosition == DragAndDropManager.DropPosition.RootAfterAll)
                    {
                        // Drop into empty space → move containers to depth-0, wrap leaves in new Container
                        Controller.DropItemsAtRootLevel(
                            dragResult.DraggedPaths,
                            dragResult.DraggedItems,
                            collections,
                            treeView,
                            selectionState);

                        EditorApplication.delayCall += () =>
                        {
                            treeView.CancelDrag();
                            RefreshSerializedDatabase();
                            if (Controller.LastFoldoutSnapshot != null)
                                treeView.SetFoldoutSnapshot(Controller.LastFoldoutSnapshot);
                            Repaint();
                        };

                        originalDragManager.CancelDrag();
                        Event.current.Use();
                        Repaint();
                        return true;
                    }
                    else
                    {
                        // Internal drop - reparent items
                        Controller.ReparentItems(
                            dragResult.DraggedPaths,
                            dragResult.DraggedItems,
                            dragResult.DropTargetPath,
                            (DragAndDropManager.DropPosition)dragResult.DropPosition,
                            collections,
                            treeView,
                            selectionState);

                        // Delayed fallback to refresh & restore foldouts
                        EditorApplication.delayCall += () =>
                        {
                            // Ensure drag state is fully cleared before refresh
                            treeView.CancelDrag();
                            RefreshSerializedDatabase();
                            if (Controller.LastFoldoutSnapshot != null)
                                treeView.SetFoldoutSnapshot(Controller.LastFoldoutSnapshot);
                            Repaint();
                        };

                        originalDragManager.CancelDrag();
                        Event.current.Use();
                        Repaint();
                        return true;
                    }
                }
            }

            if (Event.current.type == EventType.DragPerform)
            {
                if (TryHandleExternalDrag())
                    return true;
            }

            // Handle drop operation completion AFTER GUI is drawn (so hover state is updated)
            if (Event.current.type == EventType.MouseUp && Event.current.button == 0)
            {
                if (TryHandleDrop())
                    return true;
            }

            // Cancel drag on Escape key
            if (Event.current.type != EventType.KeyDown || Event.current.keyCode != KeyCode.Escape)
                return false;

            if (!treeView.DragDropManager.IsDragging)
                return false;

            treeView.DragDropManager.CancelDrag();
            Event.current.Use();
            Repaint();
            return true;
        }

        bool TryHandleDrop()
        {
            if (!treeView.DragDropManager.IsDragging)
                return false;

            if (treeView.DragDropManager.EndDrag(
                    out var draggedPaths,
                    out var draggedItems,
                    out var dropTargetPath,
                    out var dropPosition))
            {
                var originalDragManager = treeView.DragDropManager;

                if (dropPosition == DragAndDropManager.DropPosition.RootAfterAll)
                {
                    // Drop into empty space → move containers to depth-0, wrap leaves in new Container
                    Controller.DropItemsAtRootLevel(
                        draggedPaths,
                        draggedItems,
                        collections,
                        treeView,
                        selectionState);
                }
                else
                {
                    // Valid drop - perform reparenting
                    Controller.ReparentItems(
                        draggedPaths,
                        draggedItems,
                        dropTargetPath,
                        dropPosition,
                        collections,
                        treeView,
                        selectionState);
                }

                // NOTE: Do NOT force immediate controller.Initialize()/RefreshSerializedDatabase() here -
                // Lifecycle events from the lifecycle manager will already trigger a refresh and foldout restore.
                // Schedule a conservative delayed fallback to refresh & reapply foldouts in case of timing edge-cases.
                EditorApplication.delayCall += () =>
                {
                    // Ensure drag state is fully cleared before refresh
                    treeView.CancelDrag();
                    RefreshSerializedDatabase();
                    if (Controller.LastFoldoutSnapshot != null)
                        treeView.SetFoldoutSnapshot(Controller.LastFoldoutSnapshot);
                    Repaint();
                };

                // Cancel the original drag manager (it may differ from current treeView.dragDropManager if Refresh happened)
                originalDragManager.CancelDrag();
                Event.current.Use();
                Repaint();
                return true;
            }

            // Invalid drop or cancelled
            treeView.DragDropManager.CancelDrag();
            Repaint();
            return true;
        }

        bool TryHandleExternalDrag()
        {
            if (!treeView.DragDropManager.IsDragging || !treeView.DragDropManager.IsExternalDrag)
                return false;

            // Accept the drag operation
            DragAndDrop.AcceptDrag();
            Controller.CaptureFoldoutSnapshot(treeView);

            if (treeView.DragDropManager.EndDrag(
                    out _,
                    out var draggedItems,
                    out var dropTargetPath,
                    out var dropPosition))
            {
                // Valid drop - add external items
                var originalDragManager = treeView.DragDropManager;
                Controller.AddExternalItems(
                    draggedItems,
                    dropTargetPath,
                    dropPosition,
                    collections,
                    treeView,
                    selectionState);
                // NOTE: Lifecycle events will refresh and restore foldouts. Add a delayed fallback that reapplies the saved snapshot.
                EditorApplication.delayCall += () =>
                {
                    RefreshSerializedDatabase();
                    if (Controller.LastFoldoutSnapshot != null)
                        treeView.SetFoldoutSnapshot(Controller.LastFoldoutSnapshot);
                    Repaint();
                };
                // Cancel the original drag manager instance
                originalDragManager.CancelDrag();
                Event.current.Use();
                Repaint();
                return true;
            }

            if (treeView.DragDropManager.HasUnsavedSceneObjects)
                ShowNotification(new GUIContent(SceneObjectRefUtils.UnsavedSceneNotification));
            treeView.DragDropManager.CancelDrag();
            Repaint();
            return true;
        }

        public void PopOutNewWindow()
        {
#if !SECOND_BRAIN_PRO
            ProFeatureDialog.Show("Multiple Tabs");
#else
            var windowType = GetType();
            // Create a new instance instead of using GetWindow so we don't just focus the existing window
            var newWindow = CreateInstance(windowType) as EditorWindow;
            if (newWindow == null)
                return;

            InitializeWindow(newWindow as BrowserWindow);
            bool dockedSuccessfully = TryAddAsTabInSameParent(newWindow) || AddTabToSameDockArea(newWindow);
            if (!dockedSuccessfully)
            {
                // Fall back to a floating window slightly offset from the current one
                try
                {
                    newWindow.position = new Rect(position.x + 40, position.y + 40, position.width, position.height);
                }
                catch
                {
                    // ignored
                }

                newWindow.Show();
            }
#endif
        }

        /// <summary>
        /// Attempts to add <paramref name="newWindow"/> as a new tab in the same DockArea
        /// that this window currently lives in, using Unity's internal reflection API.
        /// Returns true when the tab was successfully added; false when the window is not
        /// docked or when reflection fails (caller should fall back to floating window).
        /// </summary>
        bool TryAddAsTabInSameParent(EditorWindow newWindow)
        {
            if (!docked)
                return false;

            return TryAddTabToDockArea(this, newWindow);
        }

        /// <summary>
        /// Attempts to add <paramref name="newWindow"/> as a new tab in the same DockArea
        /// that this window currently lives in.
        /// Unlike <see cref="TryAddAsTabInSameParent"/>, this overload also works when the
        /// anchor window is a floating (non-docked) window that still has an internal DockArea
        /// parent — which is the case for freshly shown standalone windows.
        /// Returns true when the tab was successfully added.
        /// </summary>
        public bool AddTabToSameDockArea(EditorWindow newWindow)
        {
            return TryAddTabToDockArea(this, newWindow);
        }

        static bool TryAddTabToDockArea(EditorWindow anchor, EditorWindow newWindow)
        {
            try
            {
                // m_Parent is the HostView / DockArea that owns this EditorWindow tab
                var parentField = typeof(EditorWindow).GetField(
                    "m_Parent",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                var dockArea = parentField?.GetValue(anchor);
                if (dockArea == null)
                    return false;

                // Only proceed when the parent is actually a DockArea (not a plain HostView)
                if (!dockArea.GetType().Name.Contains("DockArea"))
                    return false;

                // Unity's internal DockArea.AddTab(EditorWindow pane, bool sendPaneEvents)
                var addTabMethod = dockArea.GetType().GetMethod(
                    "AddTab",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
                    null,
                    new[] { typeof(EditorWindow), typeof(bool) },
                    null);

                if (addTabMethod != null)
                {
                    addTabMethod.Invoke(dockArea, new object[] { newWindow, true });
                    return true;
                }

                // Fallback: try the single-argument overload AddTab(EditorWindow)
                addTabMethod = dockArea.GetType().GetMethod(
                    "AddTab",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
                    null,
                    new[] { typeof(EditorWindow) },
                    null);

                if (addTabMethod != null)
                {
                    addTabMethod.Invoke(dockArea, new object[] { newWindow });
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BrowserWindow] Could not dock new tab via reflection: {ex.Message}");
            }

            return false;
        }

        void HandleKeyboardNavigation()
        {
            // Intercept the popup-close shortcut before any treeView access that could throw.
            // When in popup mode, intercept the toggle shortcut (Option+Space on macOS,
            // Ctrl+Space elsewhere) to close the popup. ShowPopup windows on macOS do not
            // route key events through Unity's shortcut system, so we must handle it here.
            if (isPopupMode)
            {
                var ev = Event.current;
                bool isToggleShortcut = ev.keyCode == KeyCode.Space && (ev.control || ev.alt);
#if UNITY_EDITOR_OSX
                isToggleShortcut |= ev.keyCode == KeyCode.None && ev.alt && ev.character == ' ';
                // Option+W
                isToggleShortcut |= ev.keyCode == KeyCode.W && ev.alt;
#else
                // Alt+W
                isToggleShortcut |= ev.keyCode == KeyCode.W && ev.alt;
#endif
                if (ev.type == EventType.KeyDown && isToggleShortcut)
                {
                    ev.Use();
                    // Defer close by one frame so the [Shortcut] handler fires while the window
                    // is still alive — it will find _activePopup non-null and close it (no reopen).
                    var self = this;
                    EditorApplication.delayCall += () => { if (self != null) self.Close(); };
                    return;
                }
            }

            // Skip remaining keyboard input during drag
            if (treeView.DragDropManager.IsDragging)
                return;

            var allPaths = selectionState.GetAllPaths();
            var pathArray = selectionState.GetPrimaryPath();

#if SECOND_BRAIN_PRO
            // Popup (QuickBrowse) mode: intercept Enter while the search bar has keyboard focus.
            // SearchBar.Draw runs later in the same frame and would consume the event via its
            // preInterceptReturn block — we must act here before that happens.
            if (isPopupMode && treeView.IsSearchBarFocused() &&
                Event.current.type == EventType.KeyDown &&
                !Event.current.control && !Event.current.command &&
                (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter))
            {
                int[] targetPath = (treeView.DragInput?.HasHover == true)
                    ? treeView.DragInput.HoveredPath
                    : pathArray;

                if (targetPath != null)
                {
                    var targetObj = treeView.GetObjectAtPath(targetPath);
                    if (targetObj != null)
                    {
                        Event.current.Use();

                        // Base: navigate into it (keep popup open)
                        if (targetObj is Base popupBase)
                        {
                            SetTarget(popupBase);
                            Repaint();
                            return;
                        }

                        // Scene / Prefab / ActionItem: fire the leaf enter action
                        if (LeafNodeActionHelper.HasEnterAction(targetObj))
                        {
                            bool ok = LeafNodeActionHelper.TryEnterLeafNode(targetObj, Controller);
                            Repaint();
                            if (ok) TryCloseIfPopup();
                            return;
                        }

                        // SceneComponentRef, notes, etc.: open QuickPeek and close popup
                        OpenPropertyEditorFor(targetPath, this);
                        TryCloseIfPopup();
                        return;
                    }
                }
            }
#endif

            // Delegate decision-making to TreeView's key input handler
            var keyResult = treeView.ProcessGlobalKeyEvent(allPaths, ref pathArray);

            if (!keyResult.Handled)
                return;

            // Handle browser navigation (back/forward) - Ctrl+Left/Right
            if (keyResult.BackRequested)
            {
                if (Controller.UndoHelper.CanGoBack)
                {
                    Controller.UndoHelper.GoBack(Controller);
                    Event.current.Use();
                    Repaint();
                }
                return;
            }

            if (keyResult.ForwardRequested)
            {
                if (Controller.UndoHelper.CanGoForward)
                {
                    Controller.UndoHelper.GoForward(Controller);
                    Event.current.Use();
                    Repaint();
                }
                return;
            }

            // Handle Enter on a Base in popup mode: navigate into it without closing
            if (keyResult.NavigateIntoBaseRequested && keyResult.NavigateIntoBasePath != null)
            {
                var baseObj = treeView.GetObjectAtPath(keyResult.NavigateIntoBasePath) as Base;
                if (baseObj != null)
                {
                    SetTarget(baseObj);
                    Event.current.Use();
                    Repaint();
                }
                return;
            }

            // Handle Enter key request to "enter" a leaf node (hovered has priority)
            if (keyResult.EnterRequested && keyResult.EnterTargetPath != null)
            {
                var targetObj = treeView.GetObjectAtPath(keyResult.EnterTargetPath);
                if (targetObj != null && LeafNodeActionHelper.HasEnterAction(targetObj))
                {
                    bool actionSucceeded = LeafNodeActionHelper.TryEnterLeafNode(targetObj, Controller);
                    Event.current.Use();
                    Repaint();
                    
                    // Close popup window if the enter action succeeded
                    if (actionSucceeded && isPopupMode)
                    {
                        Close();
                    }
                }
                return;
            }

            // Handle Enter on a leaf that has no dedicated enter action (e.g. loaded SceneComponentRef)
            if (keyResult.OpenPropertyEditorRequested && keyResult.OpenPropertyEditorTargetPath != null)
            {
                OpenPropertyEditorForPath(keyResult.OpenPropertyEditorTargetPath);
                Event.current.Use();
                Repaint();
                return;
            }

            // Handle duplicate/select-all which require controller-side actions
            if (keyResult.DuplicateRequested)
            {
                if (allPaths.Count > 0)
                {
                    Controller.DuplicateAndSelect(allPaths, treeView, collections);
                    Event.current.Use();
                    Repaint();
                }

                return;
            }

            if (keyResult.SelectAllRequested)
            {
                Controller.SelectAll();
                Event.current.Use();
                Repaint();
                return;
            }

            // Handle Ctrl+R rename request
            if (keyResult.RenameRequested)
            {
                BeginRenamingSelectedItem();
                Repaint();
                return;
            }

            // Handle Ctrl+V paste request — create a TextAsset child under the selected Container
            if (keyResult.PasteRequested)
            {
                var pasteTarget = treeView.GetObjectAtPath(selectionState.GetPrimaryPath());
                if (pasteTarget is IStructure and not Base)
                {
                    Controller.PasteFromClipboard(pasteTarget, treeView);
                    Event.current.Use();
                    Repaint();
                }

                return;
            }

            // Handle ESC key: go home if navigated, or close popup if already at home
            if (keyResult.GoHomeRequested)
            {
                if (isPopupMode)
                {
                    // Close popup window when ESC is pressed and already at home
                    Close();
                    Event.current.Use();
                }
                else if (!IsAtHome())
                {
                    SetTarget(null);
                    Repaint();
                }
                return;
            }

            // Handle delete key request
            if (keyResult.DeleteRequested)
            {
                var allPathsDel = selectionState.GetAllPaths();
                if (allPathsDel.Count > 0)
                {
                    Controller.DeleteSelectedItems(treeView);
                    Event.current.Use();
                    Repaint();
                }

                return;
            }

            if (Controller == null)
                return;

            // If key toggled foldouts, record undo for foldout change
            if (keyResult.FoldoutToggled)
            {
                Controller.RecordSelectionChange("Toggle Foldout");
            }

            // If navigation resulted in selection change, try applying via Controller
            if (keyResult.SelectionChanged && keyResult.NewPrimaryPath != null)
            {
                var applied = Controller.TryApplySelectionFromKeyboard(keyResult.NewPrimaryPath, treeView);
                if (applied)
                {
                    Event.current.Use();
                    GUI.FocusControl(null);
                    Repaint();
                }
            }

            if (keyResult.NeedsRepaint)
                Repaint();
        }

        void ProcessExpansion(int[] path)
        {
            if (path == null || path.Length == 0 || collections == null || path[0] < 0 || path[0] >= collections.Count)
                return;

            var colObj = collections[path[0]];
            if (colObj != null)
                treeView.foldoutState.Set(colObj, true);

            IStructure node = colObj;
            for (int i = 1; i < path.Length && node != null; i++)
            {
                var childrenObjects = node.ChildrenObjects;
                if (path[i] < 0 || path[i] >= childrenObjects.Count) break;
                var next = childrenObjects[path[i]] as IStructure;
                if (next == null) break;
                treeView.foldoutState.Set(next, true);
                node = next;
            }
        }

        void OnLostFocus()
        {
            // When the window loses focus, ensure we cancel any active drag state to avoid stuck drags.
            try
            {
                if (treeView == null)
                    return;

                var dm = treeView.DragDropManager;
                if (dm == null)
                    return;

                if (dm.IsDragging && !dm.IsExternalDrag)
                {
                    dm.CancelDrag();
                    Repaint();
                    return;
                }

                if (dm.IsPotentialDrag)
                {
                    dm.CancelPotentialDrag();
                    Repaint();
                }
            }
            catch
            {
                // ignore
            }
        }

        void DrawLeftPane()
        {
            if (collections == null || selectionState == null)
                return;

            var context = new DrawContext(
                collections,
                selectionState,
                OnItemSelected,
                () => Controller.RecordSelectionChange("Selection Changed")
            );
            leftScroll = treeView.Draw(context, this);

            // Check if search bar requested popup close (shortcut or Option-held exit)
            if (treeView.ConsumeClosePopupRequest() && isPopupMode)
            {
                EditorApplication.delayCall += Close;
            }
        }

        public void OnRenameCompleted()
        {
            // Set flag to prevent ItemDetailPanel from focusing the name field
            justCompletedRename = true;
            // Refresh the title bar in case the renamed object is the current navigation target
            UpdateWindowTitle();
            Repaint();
        }


        void OnItemSelected(int[] path, bool isMultiSelect, bool isRangeSelect)
        {
            // Delegate selection handling to controller which manages selection state and undo records
            var changed = Controller.HandleItemSelected(path, isMultiSelect, isRangeSelect, treeView);
            if (!changed)
                return;

            // Keep focus on the search bar when selection is updated silently (e.g. auto-select on search change)
            // so the user can keep typing without having to re-click the search field.
            if (treeView == null || !treeView.IsSearchBarFocused())
                GUI.FocusControl(null);
            Event.current.Use();
            Repaint();
        }

        void DrawRightPane()
        {
            rightScroll = detailPanel.Draw(
                selectionState.GetPrimaryPath(),
                selectionState.GetAllPaths(),
                () => Controller.DeleteSelectedItems(treeView),
                () => Controller.RemoveSelectedItems(treeView),
                justCompletedRename
            );

            if (justCompletedRename)
                justCompletedRename = false;
        }

        // Cached styles and icons for the tag line — allocated once, reused every repaint.
        static Texture2D s_WindowIcon;
        static GUIStyle s_TagStarStyle;
        static GUIStyle s_TagXStyle;
        static Texture  s_TagDefaultBaseIcon;
        static bool     s_TagDefaultBaseIconProSkin;

        // A small, reusable tag descriptor used to render the one-line tag area.
        class BaseTag
        {
            public string Label;
            public string Tooltip;
            public Action OnRemove;
        }

        /// <summary>
        /// Draws a single horizontal one-line area at the bottom of the BrowserWindow when
        /// this window is currently targeting a Base. Shows tags for special conditions
        /// (Scene linking, Default Base, etc.) and exposes an 'x' button to remove the condition.
        /// The implementation is intentionally small, reusable and easily extended with new tags.
        /// </summary>
        void DrawTargetBaseTagLine()
        {
            try
            {
                // Only show when navigated to a specific Base target (not when at home)
                if (targetRoot == null) return;
                if (!(targetRoot is Base baseTarget)) return;

                var tags = new List<BaseTag>();

                // Default Base tag
#if SECOND_BRAIN_PRO
                try
                {
                    var mother = Profile.Active;
                    if (mother != null && mother.DefaultBase == baseTarget)
                    {
                        tags.Add(new BaseTag
                        {
                            Label = "Default Base",
                            Tooltip = "This Base is the project's Default Base. Click × to clear.",
                            OnRemove = () =>
                            {
                                try { Controller.ClearDefaultBase(baseTarget); } catch { }
                                try { ShowNotification(new GUIContent("Cleared Default Base")); } catch { }
                            }
                        });
                    }
                }
                catch { }
#endif
                
                // Scene linking tag
                try
                {
                    if (BrowserSettings.EnableSceneLinking && !string.IsNullOrEmpty(baseTarget.SceneGuid))
                    {
                        var scenePath = AssetDatabase.GUIDToAssetPath(baseTarget.SceneGuid);
                        var sceneName = string.IsNullOrEmpty(scenePath)
                            ? "Missing Scene"
                            : System.IO.Path.GetFileNameWithoutExtension(scenePath);
                        tags.Add(new BaseTag
                        {
                            Label = sceneName,
                            Tooltip = "This Base is linked to a scene. Click × to unlink.",
                            OnRemove = () =>
                            {
                                try { Controller.ClearSceneLink(baseTarget); } catch { }
                                try { ShowNotification(new GUIContent("Unlinked Scene")); } catch { }
                            }
                        });
                    }
                }
                catch { }
                
                if (tags.Count == 0)
                    return;

                // Reserve a small separated area at the bottom
                GUILayout.Space(6);

                // Draw a thin separator line at the top of the tag area
                var sepRect = GUILayoutUtility.GetRect(1, 1, GUILayout.ExpandWidth(true));
                sepRect.height = 1;
                sepRect.x += 6; // small left padding
                sepRect.width -= 12; // small horizontal padding
                EditorGUI.DrawRect(sepRect, EditorGUIUtility.isProSkin ? new Color(0f, 0f, 0f, 0.15f) : new Color (0.8f, 0.8f, 0.8f, 0.5f));

                // Container for tags (left-aligned)
                EditorGUILayout.BeginHorizontal();

                // Left padding inside window
                GUILayout.Space(6);

                // Per-tag visuals: rounded-like background, colored circular icon at left, label, and circular X at right
                foreach (var t in tags)
                {
                    // Compute label width and total tag width
                    var labelContent = new GUIContent(t.Label);
                    Vector2 labelSize = EditorStyles.label.CalcSize(labelContent);
                    float iconSize = t.Label == "Default Base" ? 16f : 12f;
                    const float xBtnSize = 16f;
                    const float paddingH = 6f; // horizontal padding inside tag (narrower)
                    float totalWidth = paddingH + iconSize + 6f + labelSize.x + 6f + xBtnSize + paddingH;

                    // Reserve a rect for this tag without expanding width
                    Rect tagRect = GUILayoutUtility.GetRect(totalWidth, 26f, GUILayout.ExpandWidth(false));

                    // Background (rounded)
                    var bg = new Rect(tagRect.x, tagRect.y + 3, tagRect.width, tagRect.height - 6);
                    // Make background more visible: darker gray and more rounded (pill shape)
                    DrawRoundedRect(bg, EditorGUIUtility.isProSkin ? new Color(0.18f, 0.18f, 0.18f, 1f) : new Color(0.6f, 0.6f, 0.6f , 1f), bg.height * 0.5f);

                    // Draw left icon: use per-tag visuals
                    var iconRect = new Rect(bg.x + paddingH, bg.y + (bg.height - iconSize) * 0.5f, iconSize, iconSize);
                    try
                    {
                        // Default Base tag: draw the custom default_base_on icon in blue
                        if (t.Label == "Default Base")
                        {
                            bool ps = EditorGUIUtility.isProSkin;
                            if (s_TagDefaultBaseIcon == null || s_TagDefaultBaseIconProSkin != ps)
                            {
                                s_TagDefaultBaseIconProSkin = ps;
                                s_TagDefaultBaseIcon = IconUtils.Load("default_base_on");
                            }
                            if (s_TagDefaultBaseIcon != null)
                            {
                                var prevColor = GUI.color;
                                GUI.color = new Color(0.14f, 0.45f, 0.90f, 1f);
                                GUI.DrawTexture(iconRect, s_TagDefaultBaseIcon, ScaleMode.ScaleToFit);
                                GUI.color = prevColor;
                            }
                            else
                            {
                                if (s_TagStarStyle == null)
                                {
                                    s_TagStarStyle = new GUIStyle(GUIStyle.none)
                                    {
                                        alignment = TextAnchor.MiddleCenter,
                                        fontSize = 12
                                    };
                                    s_TagStarStyle.normal.textColor = new Color(0.14f, 0.45f, 0.90f, 1f);
                                }
                                GUI.Label(iconRect, new GUIContent("★", t.Tooltip), s_TagStarStyle);
                            }
                        }
                        // Scene linking tag: use the SceneAsset icon
                        else
                        {
                            Texture sceneIcon = EditorGUIUtility.ObjectContent(null, typeof(SceneAsset)).image;
                            if (sceneIcon != null)
                            {
                                // Draw the scene asset icon centered in the icon rect
                                GUI.DrawTexture(iconRect, sceneIcon, ScaleMode.ScaleToFit);
                            }
                            else
                            {
                                // Fallback: draw a small colored disc
                                Handles.BeginGUI();
                                var iconCenter = new Vector3(iconRect.x + iconRect.width * 0.5f, iconRect.y + iconRect.height * 0.5f, 0f);
                                Handles.color = new Color(0.40f, 0.75f, 0.32f, 1f);
                                Handles.DrawSolidDisc(iconCenter, Vector3.forward, iconSize * 0.5f);
                                Handles.EndGUI();
                            }
                        }
                    }
                    catch
                    {
                        // Failsafe: draw the simple disc if anything goes wrong
                        Handles.BeginGUI();
                        var iconCenter = new Vector3(bg.x + paddingH + iconSize * 0.5f, bg.y + bg.height * 0.5f, 0f);
                        Handles.color = new Color(0.40f, 0.75f, 0.32f, 1f);
                        Handles.DrawSolidDisc(iconCenter, Vector3.forward, iconSize * 0.5f);
                        Handles.EndGUI();
                    }

                    // Label position (to the right of icon)
                    var labelRect = new Rect(bg.x + paddingH + iconSize + 6f, bg.y, labelSize.x, bg.height);
                    GUI.Label(labelRect, new GUIContent(t.Label, t.Tooltip), EditorStyles.label);

                    // Draw circular X button on the right (draw a small disc and overlay an invisible button)
                    var xRect = new Rect(bg.x + bg.width - paddingH - xBtnSize, bg.y + (bg.height - xBtnSize) / 2f, xBtnSize, xBtnSize);
                    Handles.BeginGUI();
                    Handles.color = new Color(0.3f, 0.3f, 0.3f, 0.5f);
                    var xCenter = new Vector3(xRect.x + xRect.width * 0.5f, xRect.y + xRect.height * 0.5f, 0f);
                    Handles.DrawSolidDisc(xCenter, Vector3.forward, xBtnSize * 0.5f);
                    Handles.EndGUI();

                    // X label over the circular button (centered)
                    var xLabelRect = new Rect(xRect.x, xRect.y, xRect.width, xRect.height);
                    if (s_TagXStyle == null)
                    {
                        s_TagXStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleCenter };
                        s_TagXStyle.normal.textColor = Color.white;
                    }
                    // Invisible button (exact same rect) to capture clicks; draw label on top so it's centered
                    if (GUI.Button(xLabelRect, GUIContent.none, GUIStyle.none))
                    {
                        try { t.OnRemove?.Invoke(); } catch { }
                        Repaint();
                    }
                    GUI.Label(xLabelRect, "×", s_TagXStyle);

                    // Small spacing between tags
                    GUILayout.Space(6);
                }

                // Fill remaining horizontal space
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(4);
            }
            catch
            {
                // Fail-safe: do not break the entire window if tag rendering fails
            }
        }

        // Draws a filled rounded rectangle by composing rects and corner discs.
        // This is an Editor-only helper to approximate rounded backgrounds without external textures.
        void DrawRoundedRect(Rect rect, Color color, float radius)
        {
            try
            {
                radius = Mathf.Min(radius, rect.width * 0.5f, rect.height * 0.5f);

                // Center band
                var center = new Rect(rect.x + radius, rect.y, rect.width - 2f * radius, rect.height);
                EditorGUI.DrawRect(center, color);

                // Left and right bands (leave vertical space for corner discs)
                var left = new Rect(rect.x, rect.y + radius, radius, rect.height - 2f * radius);
                var right = new Rect(rect.x + rect.width - radius, rect.y + radius, radius, rect.height - 2f * radius);
                EditorGUI.DrawRect(left, color);
                EditorGUI.DrawRect(right, color);

                // Corner discs
                Handles.BeginGUI();
                var prev = Handles.color;
                Handles.color = color;
                Handles.DrawSolidDisc(new Vector3(rect.x + radius, rect.y + radius, 0f), Vector3.forward, radius);
                Handles.DrawSolidDisc(new Vector3(rect.x + rect.width - radius, rect.y + radius, 0f), Vector3.forward, radius);
                Handles.DrawSolidDisc(new Vector3(rect.x + radius, rect.y + rect.height - radius, 0f), Vector3.forward, radius);
                Handles.DrawSolidDisc(new Vector3(rect.x + rect.width - radius, rect.y + rect.height - radius, 0f), Vector3.forward, radius);
                Handles.color = prev;
                Handles.EndGUI();
            }
            catch
            {
                // Failsafe: fallback to plain rect
                try { EditorGUI.DrawRect(rect, color); } catch { }
            }
        }


        public void ToggleDetail()
        {
            ShowDetailPanel = !ShowDetailPanel;
        }

        public void CreateChild(Object rootObj)
        {
            if (BrowserSettings.ForceNamingOnCreate && treeView != null)
            {
                treeView.StartGhostCreation(rootObj, null);
                return;
            }
            Controller.CreateChild(rootObj, treeView);
        }

        public void CreateChildOfType(Object parent, Type childType)
        {
            if (BrowserSettings.ForceNamingOnCreate && treeView != null)
            {
                treeView.StartGhostCreation(parent, childType);
                return;
            }
            Controller.CreateChildOfType(parent, childType, treeView);
        }

        public void CreateChildUsingOption(Object parent, CreateChildOption option)
        {
            if (BrowserSettings.ForceNamingOnCreate && treeView != null)
            {
                treeView.StartGhostCreation(parent, option?.ChildType, option);
                return;
            }
            Controller.CreateChildUsingOption(parent, option, treeView);
        }

        /// <summary>
        /// Creates a child with a pre-specified name (used by the ghost creation session after
        /// the user has typed a valid, non-duplicate name and pressed Enter).
        /// </summary>
        public void CreateChildWithName(Object parent, Type childType, string name)
        {
            Controller.CreateChildWithName(parent, childType, name, treeView);
        }

        /// <summary>
        /// Creates a child with a pre-specified name using an optional CreateChildOption.
        /// Used by the ghost creation session to apply option-specific post-create callbacks.
        /// </summary>
        public void CreateChildWithName(Object parent, Type childType, string name, CreateChildOption option)
        {
            if (option != null)
            {
                Controller.CreateChildUsingOptionWithName(parent, option, name, treeView);
            }
            else
            {
                Controller.CreateChildWithName(parent, childType, name, treeView);
            }
        }

        public void DeleteSelectedItems()
        {
            Controller.DeleteSelectedItems(treeView);
        }

        public void RemoveSelectedItemsFromList()
        {
            Controller.RemoveSelectedItems(treeView);
        }

        public void ShowPropertiesForSelectedItem()
        {
            var allPaths = selectionState.GetAllPaths();
            if (allPaths == null || allPaths.Count == 0)
                return;

            var objects = allPaths
                .Select(p => Controller.GetObjectAtPath(p))
                .Where(o => o != null)
                .ToList();

            foreach (var obj in objects)
            {
                var toOpen = obj;
                if (obj is SceneObjectRef sceneRef)
                {
                    var go = SceneObjectMap.Resolve(sceneRef.sceneObject?.GlobalId);
                    if (go != null)
                        toOpen = go;
                }
                else if (obj is SceneComponentRef sceneComponentRef)
                {
                    var component = SceneObjectMap.ResolveComponent(sceneComponentRef.sceneComponent?.GlobalId);
                    if (component != null)
                        toOpen = component;
                }

                EditorApplication.delayCall += () =>
                {
                    try
                    {
                        EditorUtility.OpenPropertyEditor(toOpen);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"Failed to open property editor: {ex.Message}");
                    }
                };
            }
        }

        void OpenPropertyEditorForPath(int[] path)
        {
            if (path == null) return;
            var obj = treeView.GetObjectAtPath(path);
            if (obj == null) return;

            Object toOpen = obj;
            if (obj is SceneObjectRef sceneRef)
            {
                var go = SceneObjectMap.Resolve(sceneRef.sceneObject?.GlobalId);
                if (go != null) toOpen = go;
            }
            else if (obj is SceneComponentRef sceneComponentRef)
            {
                var component = SceneObjectMap.ResolveComponent(sceneComponentRef.sceneComponent?.GlobalId);
                if (component != null) toOpen = component;
            }

            var capturedToOpen = toOpen;
            EditorApplication.delayCall += () =>
            {
                try { EditorUtility.OpenPropertyEditor(capturedToOpen); }
                catch (Exception ex) { Debug.LogWarning($"Failed to open property editor: {ex.Message}"); }
            };
        }

        public void BeginRenamingSelectedItem()
        {
            var primaryPath = selectionState.GetPrimaryPath();
            if (primaryPath == null)
                return;

            treeView.Renamer.StartRename(primaryPath);
        }

        public void DuplicateSelectedItems()
        {
            var allPaths = selectionState.GetAllPaths();
            if (allPaths == null || allPaths.Count == 0)
                return;

            Controller.DuplicateAndSelect(allPaths, treeView, collections);
        }

        /// <summary>
        /// Moves all currently selected items to the specified target Base, removing them from
        /// their current parent in the active Base.
        /// </summary>
        public void RemoveMissingAtPath(int[] path)
        {
            Controller?.RemoveMissingAtPath(path, treeView);
        }

        public void CleanMissingChildren(Object parent)
        {
            Controller?.CleanMissingChildren(parent, treeView);
        }

        public void MoveSelectedItemsToBase(Base targetBase)
        {
            if (targetBase == null)
                return;

            Controller.MoveSelectedItemsToBase(targetBase, treeView);
        }

        /// <summary>
        /// Focuses the search bar in the toolbar. Should be called on a delay after window operations.
        /// </summary>
        public void FocusSearchBar()
        {
            try
            {
                if (treeView != null)
                {
                    treeView.RequestFocusSearchBar();
                    Repaint();
                }
            }
            catch { }
        }

        public bool IsQuickPeekOpenForPath(int[] path)
        {
#if SECOND_BRAIN_PRO
            return quickPeekHandler != null && quickPeekHandler.IsOpenForPath(path);
#else
            return false;
#endif
        }

#if SECOND_BRAIN_PRO
        public void OpenPropertyEditorFor(int[] selectedPath, BrowserWindow window)
        {
           quickPeekHandler.OpenFor(selectedPath, window);
        }

        public void DisposeQuickPeek()
        {
            quickPeekHandler.DisposeQuickPeek();
        }
#endif
    }
}
