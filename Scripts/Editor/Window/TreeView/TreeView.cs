using System.Collections.Generic;
using System;
using UnityEditor;
using UnityEngine;
using System.Linq;
using Object = UnityEngine.Object;
using System.Runtime.InteropServices;

namespace SecretZauce.SecondBrain.Editor
{
    /// <summary>
    /// Handles the left panel tree view of the Browser Window.
    /// Displays a hierarchical tree of IStructure 
    /// </summary>
    public class TreeView
    {
        // Win32 API for cursor position (editor-Windows only)
#if UNITY_EDITOR_WIN
        [StructLayout(LayoutKind.Sequential)]
        struct POINT { public int X; public int Y; }

        [DllImport("user32.dll")]
        static extern bool GetCursorPos(out POINT lpPoint);
#endif
        public IStructure Root => OwnerWindow?.Root; 
        public readonly FoldoutState foldoutState;
        public DragAndDropManager DragDropManager { get; }
        public DrawContext Context { get; private set; }
        public ItemRenamer Renamer { get; }
        public TreeViewDragInput DragInput { get; }

        public BrowserWindow OwnerWindow { get; private set; }

        // Subscription flag for EditorApplication.update polling while dragging
        bool updateSubscribed = false;

        void SubscribeToEditorUpdate()
        {
            if (updateSubscribed)
                return;
            EditorApplication.update += OnEditorUpdate;
            updateSubscribed = true;
        }

        // Public helper so external classes (e.g. TreeViewDragInput) can ensure polling
        // is active when a potential drag or drag begins.
        public void StartDragLeavePolling()
        {
            SubscribeToEditorUpdate();
        }

        void UnsubscribeFromEditorUpdate()
        {
            if (!updateSubscribed)
                return;
            try
            {
                EditorApplication.update -= OnEditorUpdate;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }

            updateSubscribed = false;
        }

        void OnEditorUpdate()
        {
            // If no owner or no drag state, unsubscribe
            if (OwnerWindow == null || DragDropManager == null)
            {
                UnsubscribeFromEditorUpdate();
                return;
            }

            if (!DragDropManager.IsDragging && !DragDropManager.IsPotentialDrag)
            {
                UnsubscribeFromEditorUpdate();
                return;
            }

            // If the mouse is no longer over this window, cancel internal drags
            var windowUnderMouse = EditorWindow.mouseOverWindow;

            bool mouseOutsideWindow = false;
            if (windowUnderMouse != OwnerWindow)
            {
                // Additional robust check for Windows: use GetCursorPos to get global cursor position
                try
                {
#if UNITY_EDITOR_WIN
                    POINT p;
                    if (GetCursorPos(out p))
                    {
                        Vector2 cursor = new Vector2(p.X, p.Y);
                        Rect winRect = OwnerWindow.position;
                        if (!winRect.Contains(cursor))
                            mouseOutsideWindow = true;
                    }
                    else
                    {
                        mouseOutsideWindow = true; // conservative
                    }
#else
                    mouseOutsideWindow = true;
#endif
                }
                catch
                {
                    mouseOutsideWindow = true;
                }
            }

            if (mouseOutsideWindow)
            {
                if (DragDropManager.IsDragging && !DragDropManager.IsExternalDrag)
                {
                    DragDropManager.CancelDrag();
                    UnsubscribeFromEditorUpdate();
                    OwnerWindow?.Repaint();
                    return;
                }

                if (DragDropManager.IsPotentialDrag)
                {
                    DragDropManager.CancelPotentialDrag();
                    UnsubscribeFromEditorUpdate();
                    OwnerWindow?.Repaint();
                    return;
                }
            }
        }

        readonly TreeViewKeyInput keyInputHandler;
        readonly SearchBar searchBar;
        readonly List<IStructure> collections;
        readonly FoldoutAnimationState foldoutAnimState;

        Vector2 scrollPosition;
        public List<int[]> visiblePaths { get; } // Track all visible paths for range selection

        // Viewport rect of the scroll view, captured after EndScrollView during Repaint
        // Used to draw persistent peek zone column overlays.
        Rect _scrollViewRect;

        static GUIContent s_PeekInfoIcon;
        static GUIContent s_PeekBlockedIcon;

        // MouseMove throttle: only repaint when the cursor enters a different row slot
        int _lastHoveredRowSlot = -1;
        // Track drag state to reset hover slot and update wantsMouseMove when drag ends
        bool _wasDragging;

        // Deferred click handling to ensure visiblePaths is complete
        int[] pendingClickPath;
        bool pendingIsMultiSelect;
        bool pendingIsRangeSelect;
        bool hasPendingClick;

        // Double-click to rename state
        int[] lastClickPath;
        double lastClickTime;
        const double DOUBLE_CLICK_THRESHOLD = 0.3; // 300ms
        readonly ItemRenderer renderer;
        readonly TreeViewHeader header;

        // Ghost creation session (set when ForceNamingOnCreate is active)
        GhostCreationSession ghostSession;

        /// <summary>True while a ghost creation input row is active in the tree view.</summary>
        public bool HasGhostSession => ghostSession != null;

        public TreeView(List<IStructure> collections)
        {
            this.collections = collections;
            foldoutState = new FoldoutState();
            // AnimState is initialised with a lambda that always routes to the current OwnerWindow
            // so we don't need to update the callback when the window reference changes.
            foldoutAnimState = new FoldoutAnimationState(() => OwnerWindow?.Repaint());
            DragDropManager = new DragAndDropManager();
            DragDropManager.SetOwnerTreeView(this); // Set TreeView reference for root access
            keyInputHandler = new TreeViewKeyInput(this, foldoutState, collections);
            searchBar = new SearchBar();
            visiblePaths = new List<int[]>();
            Renamer = new ItemRenamer(this);
            DragInput = new TreeViewDragInput(this);
            renderer = new ItemRenderer(this);
            header = new TreeViewHeader(this);
            OwnerWindow = null; // Will be set in Draw method
        }

        /// <summary>
        /// Draws the left panel tree view.
        /// </summary>
        /// <returns>The current scroll position</returns>
        public Vector2 Draw(DrawContext drawContext, BrowserWindow window)
        {
            Context = drawContext;
            OwnerWindow = window;
            // Ensure we poll EditorApplication.update while a drag/potential-drag is active so
            // OnEditorUpdate can detect when the mouse leaves the window and cancel the drag.
            if (DragDropManager != null && (DragDropManager.IsDragging || DragDropManager.IsPotentialDrag))
                SubscribeToEditorUpdate();

            // Hover highlighting only requires MouseMove events when NOT dragging.
            // Disabling wantsMouseMove during drags suppresses the flood of move events
            // (and the repaints they trigger) while the user holds a drag.
            bool isDraggingNow = DragDropManager != null && (DragDropManager.IsDragging || DragDropManager.IsPotentialDrag);
            if (OwnerWindow != null)
                OwnerWindow.wantsMouseMove = !isDraggingNow;

            // When a drag just ended, reset the hover-slot cache so the first MouseMove
            // after the drag correctly triggers a repaint for the new hover position.
            if (_wasDragging && !isDraggingNow)
                _lastHoveredRowSlot = -1;
            _wasDragging = isDraggingNow;

            // Forward rename completion to commands
            Renamer.SetOnRenameCompleted(window.OnRenameCompleted);
            visiblePaths.Clear();
            hasPendingClick = false;

            // ── Earliest Escape intercept for header rename ────────────────────
            // Runs before searchBar.Draw() and header.Draw() so we see the raw KeyDown
            // event before Unity's IMGUI text-editor can mark it as Used.
            if (header.IsRenamingHeader && Event.current != null &&
                Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                header.CancelHeaderRename();
                EditorGUIUtility.editingTextField = false;
                GUI.FocusControl(null);
                GUIUtility.keyboardControl = 0;
                Event.current.Use();
                EditorApplication.delayCall += () => OwnerWindow?.Repaint();
            }

            // ── Earliest Escape intercept for ghost creation ───────────────────
            if (ghostSession != null && Event.current != null &&
                Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                CancelGhostCreation();
                EditorGUIUtility.editingTextField = false;
                GUI.FocusControl(null);
                GUIUtility.keyboardControl = 0;
                Event.current.Use();
                EditorApplication.delayCall += () => OwnerWindow?.Repaint();
            }

            bool searchChanged = searchBar.Draw();

            // If search changed, expand all foldouts to show matches
            if (searchChanged && IsSearching())
                ExpandAll();

            // Include null (missing) slots in the count so MISSING rows are drawn.
            int colCount = drawContext.Collections.Count;

            header.Draw(window);
            EditorGUIUtils.DrawSeparator();

            if (colCount > 0 || (ghostSession != null && ReferenceEquals(ghostSession.Parent, Root as Object)))
            {
                // Detect Unity DragAndDrop at scroll view level for immediate visual feedback
                Event e = Event.current;
                // Ensure hover highlighting updates smoothly as the mouse moves by repainting the owner window on MouseMove.
                // Skip repaint while renaming to avoid unnecessary UI churn.
                if (e is { type: EventType.MouseMove } && !Renamer.IsRenamingAny && OwnerWindow != null)
                {
                    // Only repaint when the cursor moves into a different row slot.
                    // This eliminates the per-pixel repaint flood that was the largest
                    // source of lag as item count grew.
                    float rowH = BrowserSettings.GetItemRowHeight();
                    int slot = rowH > 0f ? Mathf.FloorToInt(e.mousePosition.y / rowH) : -1;
                    if (slot != _lastHoveredRowSlot)
                    {
                        _lastHoveredRowSlot = slot;
                        OwnerWindow.Repaint();
                    }
                }

                if (e is { type: EventType.DragUpdated })
                {
                    if (DragAndDrop.objectReferences != null && DragAndDrop.objectReferences.Length > 0)
                    {
                        // Initialize external drag if not already dragging
                        // Do not start external drag while an item is being renamed
                        if (!Renamer.IsRenamingAny && !DragDropManager.IsDragging)
                            DragDropManager.BeginExternalDrag(Context.Collections);
                    }
                }

                DrawScrollViewContent(drawContext);

                // If the search bar requested navigation away (DownArrow pressed while focused),
                // consume the request now that visiblePaths has been populated and select the
                // first visible item in the tree.
                try
                {
                    if (searchBar != null && searchBar.ConsumeFocusRequest())
                    {
                        if (visiblePaths != null && visiblePaths.Count > 0)
                        {
                            var firstPath = visiblePaths[0];
                            // Trigger the same selection flow as a click (single selection)
                            Context.RaiseOnItemSelected(firstPath, false, false);
                            // Clear any focused control so the tree can receive keyboard input
                            GUI.FocusControl(null);
                            // Defer a repaint to ensure IMGUI state updates after selection change
                            EditorApplication.delayCall += () => OwnerWindow?.Repaint();
                        }
                    }

                    // Auto-select the first direct match whenever the search text changes
                    if (searchChanged && IsSearching())
                    {
                        var firstMatch = FindFirstDirectMatch();
                        if (firstMatch != null)
                        {
                            Context.RaiseOnItemSelected(firstMatch, false, false);
                            EditorApplication.delayCall += () => OwnerWindow?.Repaint();
                        }
                    }

                    // Activate the first direct match (or current selection) when Enter is pressed while the search bar is focused
                    if (searchBar != null && searchBar.ConsumeActivateFirstMatchRequest())
                    {
                        int[] pathToActivate = null;
                        if (IsSearching())
                        {
                            var firstMatch = FindFirstDirectMatch();
                            if (firstMatch != null)
                            {
                                Context.RaiseOnItemSelected(firstMatch, false, false);
                                pathToActivate = firstMatch;
                            }
                        }
                        else if (Context?.SelectedPaths?.Count > 0)
                        {
                            // Not searching: activate the currently selected item
                            pathToActivate = Context.SelectedPaths[0];
                        }
                        if (pathToActivate != null)
                            TryActivateItem(pathToActivate);
                    }
                }
                catch
                {
                    // swallow any safety exceptions to avoid breaking the tree draw loop
                }

                return HandlePostNodeDrawingInputs(out var vector2) ? vector2 : scrollPosition;
            }
            else
            {
                DrawEmptyState(window);
                return scrollPosition;
            }
        }

        public bool IsSearching()
        {
            return searchBar.IsSearching;
        }

        public bool IsSearchBarFocused()
        {
            return searchBar.IsFocused();
        }

        /// <summary>
        /// Requests focus on the search bar. The focus will be applied during the next draw cycle.
        /// </summary>
        public void RequestFocusSearchBar()
        {
            searchBar.RequestFocus();
        }

        /// <summary>
        /// Draws the empty state welcome screen when there are no collections to display.
        /// Shows different subtitle messages depending on whether we are at home or inside a Base.
        /// </summary>
        void DrawEmptyState(BrowserWindow window)
        {
            bool isAtHome = window?.IsAtHome() ?? true;
            bool isBase = !isAtHome && window?.Root is Base;

            // Signal Copy visual mode while dragging external assets over an empty Base
            Event e = Event.current;
            if (isBase && e != null && e.type == EventType.DragUpdated &&
                DragAndDrop.objectReferences != null && DragAndDrop.objectReferences.Length > 0)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                OwnerWindow?.Repaint();
            }

            GUILayout.FlexibleSpace();

            // Title
            var titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 22,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.8f, 0.8f, 0.8f, 0.6f) }
            };
            GUILayout.Label("Welcome", titleStyle);

            GUILayout.Space(8);

            // Subtitle
            string subtitle = isBase
                ? "Click  +  above to create a Container,\nor drag assets / GameObjects here"
                : "Click  +  above to create your first Base";

            var subtitleStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                normal = { textColor = new Color(0.7f, 0.7f, 0.7f, 0.45f) }
            };
            GUILayout.Label(subtitle, subtitleStyle);

            GUILayout.FlexibleSpace();
        }

        /// <summary>
        /// Handles post-node drawing input events, including drag events,
        /// mouse button releases, keyboard cancel actions, and deferred clicks.
        /// </summary>
        /// <param name="position">The resulting Vector2 value updated based on input handling.</param>
        /// <returns>Returns true if input processing was successful; otherwise, false.</returns>
        bool HandlePostNodeDrawingInputs(out Vector2 position)
        {
            position = Vector2.zero;
            // Draw floating preview on top of everything (outside scroll view)
            // Only for internal drags - Unity handles preview for external drags
            // We pass Event.current.mousePosition here (outside the scroll view) so the
            // preview is positioned in window coordinates, not in scroll-view-local coordinates.
            // Using the stored currentMousePos from DragDropManager would cause a positional
            // offset equal to the scroll view's layout origin + current scroll amount.
            if (DragDropManager.IsDragging && !DragDropManager.IsExternalDrag)
                DragDropManager.DrawFloatingPreview(Event.current.mousePosition);

            // Global MouseUp handler - CRITICAL for ensuring drag always ends
            // This catches MouseUp events even when not over a specific item
            // Handles both internal drags and potential drags that were never started
            if (Event.current.type == EventType.MouseUp && Event.current.button == 0)
            {
                if (DragDropManager.IsPotentialDrag)
                {
                    // Potential drag was never started, just cancel it
                    DragDropManager.CancelPotentialDrag();
                }
            }

            // Handle DragExited for external drags
            if (Event.current.type == EventType.DragExited && DragDropManager.IsExternalDrag)
            {
                DragDropManager.CancelDrag();
            }

            // If the mouse leaves the window while doing an internal reparent drag, cancel it.
            // This prevents a stuck reparent operation when the cursor moves outside the EditorWindow.
            if (Event.current.type == EventType.MouseLeaveWindow)
            {
                // Cancel active internal drag
                if (DragDropManager.IsDragging && !DragDropManager.IsExternalDrag)
                {
#if SECOND_BRAIN_PRO
                    // Transition to Unity's external DragAndDrop so items can be dropped
                    // on Scene View, Project Browser, or another BrowserWindow.
                    if (OwnerWindow != null && ProFeature.Provider != null)
                    {
                        var dragItems = DragDropManager.GetDraggedItems();
                        var dragPaths = DragDropManager.GetDraggedPaths();
                        if (dragItems.Count > 0)
                            ProFeature.Provider.BeginExternalDrag(OwnerWindow, dragItems, dragPaths);
                    }
#endif
                    DragDropManager.CancelDrag();
                    UnsubscribeFromEditorUpdate();
                    Event.current.Use();
                    position = scrollPosition;
                    return true;
                }

                // Cancel a potential (not-yet-started) drag as well
                if (DragDropManager.IsPotentialDrag)
                {
                    DragDropManager.CancelPotentialDrag();
                    UnsubscribeFromEditorUpdate();
                    Event.current.Use();
                    position = scrollPosition;
                    return true;
                }
            }

            // Handle Escape key to cancel any drag operation
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                if (DragDropManager.IsDragging || DragDropManager.IsPotentialDrag)
                {
                    DragDropManager.CancelDrag();
                    Event.current.Use();
                    position = scrollPosition;
                    return true;
                }
            }

            // Prefer EditorWindow.mouseOverWindow to detect when the mouse left this window.
            try
            {
                if (OwnerWindow != null && (DragDropManager.IsDragging || DragDropManager.IsPotentialDrag))
                {
                    var windowUnderMouse = EditorWindow.mouseOverWindow;
                    if (windowUnderMouse != OwnerWindow)
                    {
                        if (DragDropManager.IsDragging && !DragDropManager.IsExternalDrag)
                        {
                            DragDropManager.CancelDrag();
                            Event.current.Use();
                            position = scrollPosition;
                            return true;
                        }

                        if (DragDropManager.IsPotentialDrag)
                        {
                            DragDropManager.CancelPotentialDrag();
                            Event.current.Use();
                            position = scrollPosition;
                            return true;
                        }
                    }
                }
            }
            catch
            {
                // Fallback to previous screen-coordinate check if mouseOverWindow isn't available
                try
                {
                    if (OwnerWindow != null && (DragDropManager.IsDragging || DragDropManager.IsPotentialDrag))
                    {
                        Vector2 mouseScreen = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
                        Rect winRect = OwnerWindow.position; // in screen coordinates
                        if (!winRect.Contains(mouseScreen))
                        {
                            if (DragDropManager.IsDragging && !DragDropManager.IsExternalDrag)
                            {
                                DragDropManager.CancelDrag();
                                Event.current.Use();
                                position = scrollPosition;
                                return true;
                            }

                            if (DragDropManager.IsPotentialDrag)
                            {
                                DragDropManager.CancelPotentialDrag();
                                Event.current.Use();
                                position = scrollPosition;
                                return true;
                            }
                        }
                    }
                }
                catch
                {
                    // ignore
                }
            }

            // Process deferred click after all visible paths have been collected
            if (hasPendingClick)
            {
                // If the header is currently being renamed, cancel the header rename when the user selects an item.
                // This ensures selection actions don't leave the header rename UI active (regression fix).
                if (header.IsRenamingHeader)
                {
                    header.CancelHeaderRename();
                    EditorGUIUtility.editingTextField = false;
                    GUI.FocusControl(null);
                    GUIUtility.keyboardControl = 0;
                    // Defer repaint to avoid IMGUI timing issues
                    EditorApplication.delayCall += () => OwnerWindow?.Repaint();
                }

                Context.RaiseOnItemSelected(pendingClickPath, pendingIsMultiSelect, pendingIsRangeSelect);
                hasPendingClick = false;
            }

            return false;
        }

        void DrawScrollViewContent(DrawContext drawContext)
        {
            // Scroll view for collections
            // Clear previous hover state so TreeViewDragInput can recompute hover for this frame
            DragInput.ClearHover();


            scrollPosition = GUILayout.BeginScrollView(scrollPosition);
            int totalItems = drawContext.Collections.Count;
            for (int i = 0; i < totalItems; i++)
            {
                var collection = drawContext.Collections[i] as Object;
                int[] path = new int[] { i };
                DrawNode(path, collection);
            }

            // Draw ghost row at the root level if the session targets the Root object
            if (ghostSession != null && ReferenceEquals(ghostSession.Parent, Root as Object))
            {
                DrawGhostRow();
            }

            // Update drag state with final hover info (after all nodes processed)
            // This ensures dropPosition is calculated for current hover target
            // Always update mouse position so floating preview follows cursor
            // Apply drag update managed by dragInput
            DragInput.ApplyDragUpdate();

            // Draw drop indicator inside scroll view so it scrolls with content
            // ONLY draw if we have a valid drop target
            if (DragDropManager.IsDragging && DragDropManager.HasValidDropTarget())
            {
                if (DragInput.HasHover)
                {
                    // Use the stored indented x for drop indicator (matches label indent)
                    DragDropManager.DrawDropIndicator(DragInput.HoveredRect, DragInput.HoveredIndentX);
                }
                else if (DragDropManager.CurrentDropPosition == DragAndDropManager.DropPosition.RootAfterAll)
                {
                    // Draw indicator below the last row, flush with depth-0 indent
                    float indicatorY = DragInput.LastRowBottomY;
                    float startX = _scrollViewRect.width > 0f ? 0f : 0f;
                    float width = _scrollViewRect.width > 0f ? _scrollViewRect.width - 13f : 200f;
                    DragDropManager.DrawRootLevelDropIndicator(indicatorY, startX, width);
                }
            }

            GUILayout.EndScrollView();

            // Capture the scroll view viewport rect once per Repaint so the persistent
            // peek zone column overlay knows where to draw.
            if (Event.current != null && Event.current.type == EventType.Repaint)
                _scrollViewRect = GUILayoutUtility.GetLastRect();

            DrawPeekZoneColumnOverlay();
        }

        // Draws the persistent semi-transparent dark column over the right peek zone
        // and, when a peek zone is hovered, draws the info icon aligned with the popup or row.
        static bool IsQuickPeekAvailable()
        {
#if SECOND_BRAIN_PRO
            try { return ProFeature.Provider != null && BrowserSettings.EnableQuickPeek; }
            catch { return false; }
#else
            return false;
#endif
        }

        void DrawPeekZoneColumnOverlay()
        {
            if (Event.current == null || Event.current.type != EventType.Repaint)
                return;

            if (!IsQuickPeekAvailable())
                return;

            if (_scrollViewRect.width <= 0f)
                return;

            float pw = TreeViewDragInput.QuickPeekZoneWidth;

            // Determine scrollbar presence before drawing so both the dark column and the
            // info icon are positioned at the content area's right edge, not at the outer
            // scroll-view edge that includes scrollbar space.
            float scrollbarWidth = GUI.skin.verticalScrollbar.fixedWidth;
            bool hasVerticalScrollbar = scrollPosition.y > 0.1f;
            if (!hasVerticalScrollbar && _scrollViewRect.height > 0f && visiblePaths != null)
            {
                float estimatedContentHeight = visiblePaths.Count * BrowserSettings.GetItemRowHeight();
                hasVerticalScrollbar = estimatedContentHeight > _scrollViewRect.height;
            }
            float scrollbarOffset = hasVerticalScrollbar ? scrollbarWidth : 0f;

            // Draw dark column at the content area's right edge (not overlapping the scrollbar).
            float contentRightEdge = _scrollViewRect.xMax - scrollbarOffset;
            EditorGUI.DrawRect(new Rect(contentRightEdge - pw, _scrollViewRect.y, pw, _scrollViewRect.height), new Color(0f, 0f, 0f, 0.1f));

            var side = DragInput.QuickPeekHoveredSide;
            if (side == QuickPeekSide.None) 
                return;

            var hoveredPath = DragInput.QuickPeekHoveredPath;
            if (hoveredPath == null) 
                return;
           
            var obj = GetObjectAtPath(hoveredPath);
            if (side == QuickPeekSide.Left && obj is IStructure)
                return;
            
            if (s_PeekBlockedIcon == null)
                s_PeekBlockedIcon = new GUIContent(IconUtils.Load("block"));
            
            if (!OwnerWindow.IsQuickPeekOpenForPath(hoveredPath))
            {
                DrawPeekIcon(s_PeekBlockedIcon, pw, scrollbarOffset, side);
                return;
            }
            
            if (obj is Base)
            {
                DrawPeekIcon(s_PeekBlockedIcon, pw, scrollbarOffset, side);
                return;
            }

            if (s_PeekInfoIcon == null)
                s_PeekInfoIcon = EditorGUIUtility.IconContent("console.infoicon");
           
            DrawPeekIcon(s_PeekInfoIcon, pw, scrollbarOffset, side);
        }

        void DrawPeekIcon(GUIContent content, float pw, float scrollbarOffset, QuickPeekSide side)
        {
            if (content == null || content.image == null) 
                return;

            const float iconSize = 14f;

            // Adjust icon X position for scrollbar
            float iconGuiX = (side == QuickPeekSide.Left)
                ? _scrollViewRect.x + pw * 0.5f - iconSize * 0.5f
                : _scrollViewRect.xMax - scrollbarOffset - pw * 0.5f - iconSize * 0.5f;

            Rect hoveredScreenRect = DragInput.QuickPeekHoveredScreenRect;
            if (hoveredScreenRect.width <= 0f) return;
            var iconGuiY = GUIUtility.ScreenToGUIPoint(hoveredScreenRect.center).y - iconSize * 0.5f;

            Color prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.85f);
            GUI.DrawTexture(new Rect(iconGuiX, iconGuiY, iconSize, iconSize), content.image, ScaleMode.ScaleToFit);
            GUI.color = prev;
        }

        // DrawHeader logic has been moved to TreeViewHeader — called via header.Draw(window)

        /// <summary>
        /// Sets the scroll position for the tree view.
        /// </summary>
        public void SetScrollPosition(Vector2 position)
        {
            scrollPosition = position;
        }

        // ── Header rename (delegated to TreeViewHeader) ───────────────────────

        /// <summary>True while the header (target-name) rename field is open.</summary>
        public bool IsRenamingHeader => header.IsRenamingHeader;

        /// <summary>
        /// Cancels the header rename without saving — call this for Escape or navigation away.
        /// Public so TreeViewKeyInput and StartGhostCreation can invoke it.
        /// </summary>
        public void CancelHeaderRename()
        {
            header.CancelHeaderRename();
        }

        /// <summary>
        /// Cancels any active drag operation. Should be called before TreeView recreation or undo operations.
        /// </summary>
        public void CancelDrag()
        {
            DragDropManager?.CancelDrag();
        }

        /// <summary>
        /// Releases resources held by this TreeView instance. Must be called before discarding a
        /// TreeView to prevent EditorApplication.update and AnimBool listener leaks.
        /// </summary>
        public void Cleanup()
        {
            UnsubscribeFromEditorUpdate();
            foldoutAnimState.Clear();
            OwnerWindow = null;
        }

        /// <summary>
        /// Handles click on a node, detecting double-clicks for rename mode.
        /// Returns true if this was a double-click.
        /// </summary>
        bool HandleClick(int[] path)
        {
            double currentTime = EditorApplication.timeSinceStartup;
            bool isDoubleClick = StructureUtils.ArePathsEqual(path, lastClickPath) &&
                                 (currentTime - lastClickTime) < DOUBLE_CLICK_THRESHOLD;

            lastClickPath = path;
            lastClickTime = currentTime;
            return isDoubleClick;
        }

        /// <summary>
        /// Gets the Object at the given path
        /// </summary>
        public Object GetObjectAtPath(int[] path)
        {
            // Delegate to the shared traversal utility to avoid duplicated logic.
            // Prefer using the current DrawContext collections/root when available so view-specific filters are respected.
            var useCollections = Context != null ? Context.Collections : collections;
            return StructureUtils.GetNodeAtPath(path, useCollections);
        }

        /// <summary>
        /// Checks if a node or any of its descendants match the search criteria.
        /// </summary>
        bool NodeOrDescendantsMatch(Object node)
        {
            if (node == null)
                return false;

            // If the node itself matches, return true
            if (searchBar.Matches(node))
                return true;

            if (node is not IStructure structure || structure.ChildrenObjects == null)
                return false;

            // If it's an IStructure, check children recursively
            foreach (var child in structure.ChildrenObjects)
            {
                if (NodeOrDescendantsMatch(child))
                    return true;
            }

            return false;
        }

        // Unified recursive node drawing
        void DrawNode(int[] path, Object node, Color? inheritedColor = null, ColorDisplayStyle inheritedColorStyle = ColorDisplayStyle.FontColor, bool inheritedHideFoldout = false, bool inheritedForceExpand = false)
        {
            if (node == null)
            {
                // Don't hide missing entries when searching — they are always shown.
                DrawMissingNode(path);
                return;
            }

            // Only filter by search if searching - hide nodes with no matching descendants
            if (searchBar.IsSearching && !NodeOrDescendantsMatch(node))
                return;

            // Flat search mode: when Containers are excluded from the filter, skip the
            // container header row entirely and recurse into children at the same indent
            // level so all results appear flat (no indented parent containers).
            if (searchBar.IsSearching && node is IStructure flatStruct && node is not Base
                && !SearchFilterPopup.PassesFilter(node))
            {
                if (flatStruct.ChildrenObjects != null)
                {
                    int flatChildDepth = path.Length + 1;
                    int[] flatChildBuf = System.Buffers.ArrayPool<int>.Shared.Rent(flatChildDepth);
                    path.CopyTo(flatChildBuf, 0);
                    try
                    {
                        for (int i = 0; i < flatStruct.ChildrenObjects.Count; i++)
                        {
                            flatChildBuf[path.Length] = i;
                            int[] flatChildPath = new int[flatChildDepth];
                            Array.Copy(flatChildBuf, flatChildPath, flatChildDepth);
                            DrawNode(flatChildPath, flatStruct.ChildrenObjects[i],
                                inheritedColor, inheritedColorStyle, inheritedHideFoldout, inheritedForceExpand);
                        }
                    }
                    finally
                    {
                        System.Buffers.ArrayPool<int>.Shared.Return(flatChildBuf);
                    }
                }
                return;
            }

            // Track this path as visible
            visiblePaths.Add(path);

            // Centralized icon lookup
            Texture icon = ItemUtils.GetIconForNode(node);
            if (node is IStructure structureNode && node is not Base)
            {
                DrawStructureNode(path, node, icon, structureNode, inheritedColor, inheritedColorStyle, inheritedHideFoldout, inheritedForceExpand);
            }
            else
            {
                // Draw as leaf (DrawSubItem style)
                // Resolve own color if the leaf node supports it (e.g. Base implements IHasColor)
                Color? effectiveColor = inheritedColor;
                ColorDisplayStyle effectiveStyle = inheritedColorStyle;
                if (node is IHasColor hasColor && hasColor.HasLabelColor)
                {
                    effectiveColor = hasColor.LabelColor;
                    effectiveStyle = hasColor.LabelColorStyle;
                }
                DrawLeafNode(path, node, icon, effectiveColor, effectiveStyle, inheritedHideFoldout);
            }
        }

        void DrawStructureNode(int[] path, Object node, Texture icon, IStructure structureNode, Color? inheritedColor = null, ColorDisplayStyle inheritedColorStyle = ColorDisplayStyle.FontColor, bool inheritedHideFoldout = false, bool inheritedForceExpand = false)
        {
            // Determine the effective color: own assigned color takes priority over inherited.
            Color? effectiveColor = inheritedColor;
            ColorDisplayStyle effectiveStyle = inheritedColorStyle;
            bool foldoutOnly = false;
            if (node is IHasColor hasColor && hasColor.HasLabelColor)
            {
                effectiveColor = hasColor.LabelColor;
                effectiveStyle = hasColor.LabelColorStyle;
                foldoutOnly = hasColor.LabelColorFoldoutOnly;
            }

            bool isSelected = Context.IsPathSelected(OwnerWindow, path);
            // Determine foldout state with support for Container.DefaultExpand.
            // inheritedForceExpand forces expansion regardless of saved history.
            bool foldout = inheritedForceExpand ? true : foldoutState.Get(node);
            bool hideFoldoutArrow = inheritedHideFoldout;
            bool forceExpandForChildren = inheritedForceExpand;
            if (node is Container container)
            {
                if (container.DefaultExpand == DefaultExpandOption.AlwaysExpand)
                {
                    foldout = true; // override saved history
                    hideFoldoutArrow = true; // force hide for this node
                    forceExpandForChildren = true; // ensure descendants are forced expanded
                }
                else
                {
                    // Only apply Expand/Collapsed default when there is no saved history
                    if (!foldoutState.HasKey(node) && !inheritedForceExpand)
                    {
                        switch (container.DefaultExpand)
                        {
                            case DefaultExpandOption.CollapsedAsDefault:
                                foldout = false;
                                break;
                            case DefaultExpandOption.ExpandAsDefault:
                                foldout = true;
                                break;
                        }
                    }
                }
            }

            Rect rowRect = renderer.RenderFoldoutHeader(path, node, isSelected, icon, ref foldout, hideFoldoutArrow, effectiveColor, effectiveStyle);
            foldoutState.Set(node, foldout);

            // Handle click/selection (deferred) and rename via centralized helper
            ItemUtils.HandleRowClickAndSelection(
                path,
                rowRect,
                HandleClick,
                p => Renamer.StartRename(p),
                (p, multi, range) =>
                {
                    pendingClickPath = p;
                    pendingIsMultiSelect = multi;
                    pendingIsRangeSelect = range;
                    hasPendingClick = true;
                },
                () => DragDropManager.IsDragging,
                p => Renamer.IsRenaming(p),
                p => Context.IsPathSelected(OwnerWindow, p),
                () => Context.SelectedPaths != null && Context.SelectedPaths.Count > 1);

            bool hasGhostForNode = ghostSession != null && ReferenceEquals(ghostSession.Parent, node);

            // If a ghost session was just started for this node (e.g. user clicked "+"
            // on a collapsed container), force the foldout open so the ghost row is
            // visible in the same repaint cycle.  Without this, foldoutState.Set above
            // writes back the pre-click `false` value and the expansion is lost.
            if (hasGhostForNode && !foldout)
            {
                foldout = true;
                foldoutState.Set(node, true);
            }

            // ── Foldout expand / collapse animation ───────────────────────────────
            // Obtain (or lazily create) the AnimBool for this node and push the new
            // target.  The AnimBool drives a smooth height transition via
            // BeginFadeGroup / EndFadeGroup, mirroring Unity's own Hierarchy window.
            var animBool = foldoutAnimState.GetOrCreate(node, foldout);
            animBool.target = foldout;
            float faded = animBool.faded;

            // Fully collapsed and not mid-animation — skip children for performance.
            if (faded <= 0f)
                return;

            bool hasChildren = structureNode.ChildrenObjects is { Count: > 0 };
            if (!hasChildren && !hasGhostForNode)
                return;

            EditorGUI.indentLevel++;
            // BeginFadeGroup scales the layout rect height by `faded` (0..1) to produce
            // the slide animation.  Returns true unless faded is exactly 0.
            bool groupVisible = EditorGUILayout.BeginFadeGroup(faded);
            if (groupVisible)
            {
                // Use ArrayPool to avoid per-child heap allocations for child path arrays.
                // A rented buffer is used purely for the recursive DrawNode call; visiblePaths
                // stores its own independent copy (added inside DrawNode before any mutation).
                int childDepth = path.Length + 1;
                int[] childPathBuf = System.Buffers.ArrayPool<int>.Shared.Rent(childDepth);
                path.CopyTo(childPathBuf, 0);
                try
                {
                    for (int i = 0; i < structureNode.ChildrenObjects.Count; i++)
                    {
                        var child = structureNode.ChildrenObjects[i];
                        childPathBuf[path.Length] = i;
                        // Make a minimal-length copy for visiblePaths / selection; the rented buffer
                        // is reused across siblings so each stored path must be independent.
                        int[] childPath = new int[childDepth];
                        Array.Copy(childPathBuf, childPath, childDepth);
                        // When foldoutOnly is true, children receive no color from this container
                        Color? childColor = foldoutOnly ? null : effectiveColor;
                        ColorDisplayStyle childStyle = foldoutOnly ? ColorDisplayStyle.FontColor : effectiveStyle;
                        // Propagate hideFoldoutArrow and forceExpandForChildren to descendants so
                        // AlwaysExpand both hides arrows and forces expansion recursively.
                        DrawNode(childPath, child, childColor, childStyle, hideFoldoutArrow, forceExpandForChildren);
                    }
                }
                finally
                {
                    System.Buffers.ArrayPool<int>.Shared.Return(childPathBuf);
                }

                // Draw ghost creation row as the last child of this node
                if (hasGhostForNode)
                    DrawGhostRow();
            }
            EditorGUILayout.EndFadeGroup();
            EditorGUI.indentLevel--;
        }

        // Draws a leaf node (non-IStructure Object) with selection and label style
        void DrawLeafNode(int[] path, Object node, Texture icon, Color? labelColor = null, ColorDisplayStyle colorStyle = ColorDisplayStyle.FontColor, bool inheritedHideFoldout = false)
        {
            bool isSelected = Context.IsPathSelected(OwnerWindow, path);

            // Get rect with full width for proper text field sizing
            Rect itemRect =
                EditorGUILayout.GetControlRect(false, BrowserSettings.GetItemRowHeight(), GUILayout.ExpandWidth(true));
            Rect indentedItemRect = EditorGUI.IndentedRect(itemRect);

            // Background style: draw tinted background BEFORE selection/hover
            if (labelColor.HasValue && (colorStyle == ColorDisplayStyle.Background || colorStyle == ColorDisplayStyle.Gradient) && !isSelected)
            {
                var bgColorRect = itemRect;
                // Use minimal height extension that works across all font sizes
                bgColorRect.height += 1;
                if (colorStyle == ColorDisplayStyle.Background)
                {
                    EditorGUI.DrawRect(bgColorRect, new Color(labelColor.Value.r, labelColor.Value.g, labelColor.Value.b, 0.1f));
                }
                else
                {
                    // Gradient: fade to transparent on the right
                    Color start = new Color(labelColor.Value.r, labelColor.Value.g, labelColor.Value.b, 0.28f);
                    ItemUtils.DrawHorizontalFade(bgColorRect, start);
                }
            }

            // Draw hover highlight and selection background
            ItemUtils.DrawHoverAndSelection(itemRect, Renamer.IsRenaming(path), isSelected, DragDropManager.IsDragging,
                renderer.GetRowHoverColor());

            // Use EditorGUI.IndentedRect to get the properly indented rect that matches Unity's indentation system
            // This ensures perfect alignment at all nesting levels
            Rect trueIndentedItemRect = EditorGUI.IndentedRect(itemRect);
            trueIndentedItemRect.x += 13;
            trueIndentedItemRect.width -= 13; // compensate for x shift so the right edge stays within the row bounds
            float labelX = indentedItemRect.x; // True label x for leaf

            // Let dragInput process row-level events for leaf rows
            DragInput.HandleRow(itemRect, path, false, labelX);
            if (renderer.RenderLeafContent(path, node, icon, itemRect, trueIndentedItemRect, resetIndent: true, labelColor, colorStyle))
                return;

            if (IsQuickPeekAvailable())
                TreeViewDragInput.DrawPeekZoneIndicator(itemRect);

            ItemUtils.HandleRowClickAndSelection(
                path,
                itemRect,
                HandleClick,
                p => Renamer.StartRename(p),
                (p, multi, range) =>
                {
                    pendingClickPath = p;
                    pendingIsMultiSelect = multi;
                    pendingIsRangeSelect = range;
                    hasPendingClick = true;
                },
                () => DragDropManager.IsDragging,
                p => Renamer.IsRenaming(p),
                p => Context.IsPathSelected(OwnerWindow, p),
                () => Context.SelectedPaths != null && Context.SelectedPaths.Count > 1,
                p =>
                {
                    try
                    {
                        var nodeObj = GetObjectAtPath(p);
                        if (!LeafNodeActionHelper.TryEnterLeafNode(nodeObj, OwnerWindow?.Controller))
                            Renamer.StartRename(p); // fall back to rename if no enter action
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogException(ex);
                    }
                });
        }

        // ── Missing / null item row ───────────────────────────────────────────────

        static GUIStyle s_MissingLabelStyle;
        static GUIStyle s_MissingXStyle;
        static bool s_MissingStylesProSkin;

        static void EnsureMissingStyles()
        {
            bool ps = EditorGUIUtility.isProSkin;
            if (s_MissingLabelStyle != null && s_MissingStylesProSkin == ps) return;
            s_MissingStylesProSkin = ps;

            s_MissingLabelStyle = new GUIStyle(EditorStyles.label)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal    = { textColor = new Color(0.85f, 0.2f, 0.2f, 1f) }
            };

            s_MissingXStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = new Color(0.85f, 0.3f, 0.3f, 0.85f) },
                hover     = { textColor = new Color(1f, 0.2f, 0.2f, 1f) }
            };
        }

        void DrawMissingNode(int[] path)
        {
            EnsureMissingStyles();

            float rowHeight = BrowserSettings.GetItemRowHeight();
            Rect rowRect = EditorGUILayout.GetControlRect(false, rowHeight, GUILayout.ExpandWidth(true));

            // Reddish row tint
            EditorGUI.DrawRect(rowRect, new Color(0.75f, 0.1f, 0.1f, 0.12f));

            // X button on the right (same position convention as FoldoutHeaderRenderer's + button)
            const float btnSize = 14f;
            const float btnPad  = 3f;
            Rect xRect = new Rect(rowRect.xMax - btnSize - btnPad,
                                   rowRect.y + (rowRect.height - btnSize) * 0.5f,
                                   btnSize, btnSize);

            // "MISSING" label, indented to match real rows
            Rect indented = EditorGUI.IndentedRect(rowRect);
            indented.x    += 13f;
            indented.width  = Mathf.Max(0f, xRect.x - indented.x - 4f);

            // Keep styles hot because skin can change
            s_MissingLabelStyle.fontSize = BrowserSettings.GetItemFontSize() > 0
                ? BrowserSettings.GetItemFontSize() : s_MissingLabelStyle.fontSize;

            GUI.Label(indented, "MISSING", s_MissingLabelStyle);

            // X button — removes this null entry from its parent
            bool xHovered = Event.current != null && xRect.Contains(Event.current.mousePosition);
            Color xColor  = xHovered ? new Color(1f, 0.2f, 0.2f, 1f) : new Color(0.85f, 0.3f, 0.3f, 0.85f);
            s_MissingXStyle.normal.textColor = xColor;
            if (GUI.Button(xRect, "✕", s_MissingXStyle))
            {
                OwnerWindow?.RemoveMissingAtPath(path);
                Event.current?.Use();
                return;
            }

            // Right-click context menu on the MISSING row
            if (Event.current != null && Event.current.type == EventType.ContextClick
                && rowRect.Contains(Event.current.mousePosition))
            {
                var menu = new GenericMenu();
                int[] capturedPath = path;
                menu.AddItem(new GUIContent("Remove Missing Entry"), false,
                    () => OwnerWindow?.RemoveMissingAtPath(capturedPath));
                menu.ShowAsContext();
                Event.current.Use();
            }
        }

        // ── Ghost Creation (ForceNamingOnCreate) ──────────────────────────────────

        /// <summary>
        /// Starts a ghost creation session for the given parent. The ghost row will appear
        /// in the tree at the correct indent level with an input field. No asset is created
        /// until the user commits a valid name.
        /// </summary>
        public void StartGhostCreation(Object parent, Type childType, CreateChildOption option = null)
        {
            if (parent == null) return;

            // Cancel any conflicting states
            CancelHeaderRename();
            Renamer.CancelPublic();
            DragDropManager?.CancelPotentialDrag();
            CancelDrag();

            // Expand the parent node so the ghost row becomes visible.
            // Also expand all ancestors so the row is reachable in the tree.
            if (parent is IStructure)
            {
                foldoutState.Set(parent, true);
                // Ensure ancestor containers are also expanded so the ghost row is visible
                ExpandAncestorsOf(parent);
            }

            string defaultName = string.Empty;
#if SECOND_BRAIN_PRO
            var creator = ProFeature.Provider.CreateActionItemHandler();
            defaultName = creator.TryCreateFromActionItem(childType, defaultName);
#endif

            // If the ghost was started from a specific CreateChildOption, prefer the option's
            // display name as the default input text so the ghost row reflects the user's
            // chosen menu option.
            if (option != null && !string.IsNullOrEmpty(option.DisplayName))
            {
                defaultName = option.DisplayName;
            }

            ghostSession = new GhostCreationSession(parent, childType, defaultName, option);
            // Repaint twice: first to render the expanded container, second to apply focus.
            OwnerWindow?.Repaint();
            EditorApplication.delayCall += () => OwnerWindow?.Repaint();
        }

        /// <summary>Cancels the ghost creation session without creating any asset.</summary>
        public void CancelGhostCreation()
        {
            ghostSession = null;
            OwnerWindow?.Repaint();
        }

        /// <summary>
        /// Ensures all IStructure ancestors of <paramref name="target"/> in the current
        /// collections are expanded so the target node (and any ghost row it hosts) is
        /// reachable in the tree view without manual user interaction.
        /// </summary>
        void ExpandAncestorsOf(Object target)
        {
            if (target == null || Context?.Collections == null) return;

            void WalkAndExpand(IStructure node)
            {
                if (node?.ChildrenObjects == null) return;
                foreach (var child in node.ChildrenObjects)
                {
                    if (child == null) continue;
                    if (ReferenceEquals(child, target))
                    {
                        // This node is the direct parent — it is already expanded by the caller.
                        return;
                    }
                    if (child is IStructure childStruct)
                    {
                        // Check if target is somewhere inside this subtree
                        if (IStructure.ExistsInTree(childStruct, target))
                        {
                            foldoutState.Set(child, true);
                            WalkAndExpand(childStruct);
                            return;
                        }
                    }
                }
            }

            foreach (var col in Context.Collections)
            {
                if (col == null) continue;
                if (ReferenceEquals(col, target))
                    break;
                if (IStructure.ExistsInTree(col, target))
                {
                    foldoutState.Set(col, true);
                    WalkAndExpand(col);
                    break;
                }
            }
        }

        /// <summary>
        /// Draws the ghost creation input row at the current indent level.
        /// Handles Enter (commit) and Escape (cancel).
        /// </summary>
        void DrawGhostRow()
        {
            if (ghostSession == null) return;

            Rect rowRect = EditorGUILayout.GetControlRect(false, BrowserSettings.GetItemRowHeight(),
                GUILayout.ExpandWidth(true));
            rowRect.x += 13; 
            rowRect.width -= 26;
            
            // Reserve space for quick peek zone when clipping the input field
            float rightPeekOffset = 0f;
#if SECOND_BRAIN_PRO
            if (ProFeature.Provider != null && BrowserSettings.EnableQuickPeek)
                rightPeekOffset = TreeViewDragInput.QuickPeekZoneWidth;
#endif
            Rect inputRect = rowRect;
            inputRect.width -= rightPeekOffset;

            // Draw selection-style highlight so it looks like an active rename
            var ghostSelRect = rowRect;
            ghostSelRect.height += 1f; // increase ghost row highlight height by 1
            EditorGUI.DrawRect(ghostSelRect, new Color(0.24f, 0.48f, 0.90f, 0.25f));

            Event ev = Event.current;

            // Intercept Enter before the text field can consume the event
            bool enterPressed = ev != null && ev.type == EventType.KeyDown &&
                                (ev.keyCode == KeyCode.Return || ev.keyCode == KeyCode.KeypadEnter);

            GUI.SetNextControlName(ghostSession.ControlName);
            string newText = EditorGUI.TextField(inputRect, ghostSession.InputText);
            ghostSession.InputText = newText;

            // Focus field after it has been drawn so the control ID exists.
            // Keep requesting focus + repaint until the field actually has it,
            // so the caret is placed and the user can start typing immediately.
            if (ghostSession.FocusPending)
            {
                EditorGUI.FocusTextInControl(ghostSession.ControlName);
                if (GUI.GetNameOfFocusedControl() == ghostSession.ControlName)
                    ghostSession.FocusPending = false;
                else
                    OwnerWindow?.Repaint();
            }

            if (enterPressed)
            {
                ev.Use();
                EditorGUIUtility.editingTextField = false;
                GUI.FocusControl(null);
                GUIUtility.keyboardControl = 0;
                CommitGhostCreation();
                return;
            }

            // Click outside the row commits (consistent with ItemRenamer behaviour)
            if (ev != null && ev.type == EventType.MouseDown && !rowRect.Contains(ev.mousePosition))
            {
                EditorGUIUtility.editingTextField = false;
                GUI.FocusControl(null);
                GUIUtility.keyboardControl = 0;
                CommitGhostCreation();
                ev.Use();
            }
        }

        /// <summary>
        /// Validates the typed name and, when valid, triggers actual asset creation via the owner window.
        /// Keeps the ghost open and shows an error dialog when validation fails.
        /// </summary>
        void CommitGhostCreation()
        {
            if (ghostSession == null) return;

            string name = ghostSession.InputText?.Trim() ?? string.Empty;

            // Empty name → cancel silently (same as pressing Escape)
            if (string.IsNullOrEmpty(name))
            {
                CancelGhostCreation();
                EditorGUIUtility.editingTextField = false;
                GUI.FocusControl(null);
                GUIUtility.keyboardControl = 0;
                return;
            }

            // Validate for invalid file-path characters
            char[] invalidChars = System.IO.Path.GetInvalidFileNameChars();
            if (name.IndexOfAny(invalidChars) >= 0)
            {
                EditorApplication.delayCall += () =>
                    EditorUtility.DisplayDialog("Invalid Name",
                        "The name contains characters that are not allowed in asset names.", "OK");
                // Keep ghost open so the user can fix the name
                ghostSession.FocusPending = true;
                return;
            }

            // Check for duplicate names among siblings
            if (ghostSession.Parent is IStructure parentStruct)
            {
                var siblings = parentStruct.ChildrenObjects;
                if (siblings != null)
                {
                    foreach (var sibling in siblings)
                    {
                        if (sibling != null &&
                            string.Equals(sibling.name, name, StringComparison.OrdinalIgnoreCase))
                        {
                            EditorApplication.delayCall += () =>
                                EditorUtility.DisplayDialog("Name Already Exists",
                                    $"An item named \"{name}\" already exists here.\nPlease choose a different name.", "OK");
                            ghostSession.FocusPending = true;
                            return;
                        }
                    }
                }
            }

            // Capture session data, clear ghost, then create asset
            var capturedParent = ghostSession.Parent;
            var capturedType = ghostSession.ChildType;
            var capturedOption = ghostSession.Option;
            CancelGhostCreation();
            EditorGUIUtility.editingTextField = false;
            GUI.FocusControl(null);
            GUIUtility.keyboardControl = 0;

            OwnerWindow?.CreateChildWithName(capturedParent, capturedType, name, capturedOption);
        }

        /// <summary>
        /// Determines if most collections are currently expanded
        /// </summary>
        internal bool AreMostCollectionsExpanded()
        {
            if (Context?.Collections == null || Context.Collections.Count == 0)
                return false;

            int expandedCount = 0;
            int totalCount = Context.Collections.Count;

            foreach (var collection in Context.Collections)
            {
                if (foldoutState.Get(collection))
                    expandedCount++;
            }

            // Return true if more than half are expanded
            return expandedCount > totalCount / 2;
        }

        internal void ExpandAll()
        {
            int colCount = Context.Collections.Count;
            for (int i = 0; i < colCount; i++)
            {
                var colObjRef = Context.Collections[i];
                foldoutState.Set(colObjRef, true);
                var childrenObjects = colObjRef.ChildrenObjects;
                if (childrenObjects != null)
                {
                    foreach (var child in childrenObjects)
                        if (child is IStructure s) foldoutState.ExpandAll(new[] { s });
                }
            }
        }

        internal void CollapseAll()
        {
            int colCount = Context.Collections.Count;
            for (int i = 0; i < colCount; i++)
            {
                var colObjRef = Context.Collections[i];
                foldoutState.Set(colObjRef, false);
                var childrenObjects = colObjRef.ChildrenObjects;
                if (childrenObjects != null)
                {
                    foreach (var child in childrenObjects)
                        if (child is IStructure s) foldoutState.CollapseAll(new[] { s });
                }
            }
        }

        // Adapter to process global key events and return a KeyInputResult
        public KeyInputResult ProcessGlobalKeyEvent(List<int[]> allPaths, ref int[] primaryPath)
        {
            return keyInputHandler.ProcessGlobalKeyEvent(allPaths, ref primaryPath, searchBar.Matches);
        }

        // Adapter to process global drag events and return a DragInputResult
        public DragInputResult ProcessGlobalDragEvent()
        {
            return DragInput.ProcessGlobalDragEvent();
        }

        public Dictionary<object, bool> GetFoldoutSnapshot()
        {
            return foldoutState.Snapshot();
        }

        public void SetFoldoutSnapshot(Dictionary<object, bool> snapshot)
        {
            foldoutState.Restore(snapshot);
        }

        public string GetSearchKeyword()
        {
            return searchBar.GetSearchKeyword();
        }

        public void SetSearchKeyword(string keyword)
        {
            searchBar.SetSearchKeyword(keyword);
        }

        /// <summary>
        /// Checks if the search bar requested popup close (shortcut or Option-held exit).
        /// Returns true once and clears the pending request.
        /// </summary>
        public bool ConsumeClosePopupRequest()
        {
            return searchBar.ConsumeClosePopupRequest();
        }

        /// <summary>
        /// Returns the path of the first item in visiblePaths that directly matches the search
        /// (i.e. the item's own name satisfies the predicate, not just a descendant).
        /// Falls back to visiblePaths[0] when no direct match exists.
        /// </summary>
        int[] FindFirstDirectMatch()
        {
            if (visiblePaths == null || visiblePaths.Count == 0 || !IsSearching())
                return null;

            foreach (var path in visiblePaths)
            {
                var obj = GetObjectAtPath(path);
                if (obj != null && searchBar.Matches(obj))
                    return path;
            }

            return visiblePaths[0];
        }

        /// <summary>
        /// Triggers the "enter" action on the item at <paramref name="path"/>, mirroring the
        /// keyboard Enter handling in TreeViewKeyInput: opens the property editor in popup mode,
        /// or runs the leaf-node enter action in non-popup mode.
        /// </summary>
        void TryActivateItem(int[] path)
        {
            if (path == null || OwnerWindow == null) return;
            var targetObj = GetObjectAtPath(path);
            if (targetObj == null) return;

#if SECOND_BRAIN_PRO
            if (OwnerWindow.IsPopup)
            {
                OwnerWindow.OpenPropertyEditorFor(path, OwnerWindow);
                // Defer close: TryActivateItem is called during DrawMainContent(), so calling
                // Close() synchronously nulls serializedDatabase before OnGUI() line 739 runs.
                var win = OwnerWindow;
                EditorApplication.delayCall += () => { try { win?.TryCloseIfPopup(); } catch { } };
                return;
            }
#endif
            if (LeafNodeActionHelper.HasEnterAction(targetObj))
                LeafNodeActionHelper.TryEnterLeafNode(targetObj, OwnerWindow.Controller);
        }
    }
}
