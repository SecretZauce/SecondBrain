#if SECOND_BRAIN_PRO
using System.Collections.Generic;
using Object = UnityEngine.Object;

namespace SecretZauce.SecondBrain.Editor
{
    public static class ProFeature
    {
        public static IProFeatureProvider Provider { get; private set; }
        public static void RegisterResolver(IProFeatureProvider provider)
        {
            Provider = provider;
        }
    }

    public interface IProFeatureProvider
    {
        public SceneLinkGUIBase CreateSceneLinkGUI(UnityEditor.Editor editor);
        public ActionItemGUIBase CreateActionItemGUI(object node, UnityEngine.GUIStyle style, UnityEngine.Rect arrowRect, UnityEngine.Rect rowRect);
        public ActionItemHandlerBase CreateActionItemHandler();
        public QuickPeekHandlerBase CreateQuickPeekHandler(BrowserWindow window);

        // ── Drag-out (Pro) ───────────────────────────────────────────────────────────

        /// <summary>
        /// Transitions an active internal browser drag to Unity's DragAndDrop so items
        /// can be dropped on Scene View, Project Browser, or another BrowserWindow.
        /// Call when the mouse leaves the source BrowserWindow during a reparent drag.
        /// </summary>
        public void BeginExternalDrag(BrowserWindow source, List<Object> items, List<int[]> paths);

        /// <summary>
        /// Handle <c>EventType.DragExited</c> in the source BrowserWindow.
        /// Triggers Project Browser dialogs (Create Prefab / Move-Variant-Original / Move-Copy)
        /// when the drag was not caught by another BrowserWindow.
        /// </summary>
        public void HandleDragExited(BrowserWindow source);

        /// <summary>
        /// Returns true when an external drag started from a <em>different</em> BrowserWindow
        /// is currently in progress and targets <paramref name="thisWindow"/> as destination.
        /// </summary>
        public bool IsCrossWindowDragFromAnotherWindow(BrowserWindow thisWindow);

        /// <summary>
        /// Returns true when an external drag started from <paramref name="thisWindow"/> itself
        /// is still in progress (used to reject re-entry into the source window).
        /// </summary>
        public bool IsCrossWindowDragFromThisWindow(BrowserWindow thisWindow);

        /// <summary>
        /// Same question as <see cref="IsCrossWindowDragFromThisWindow"/>, but answered without
        /// reading Unity's DragAndDrop state. Unity keeps that state on the current GUIState, so
        /// the other overload is only valid inside OnGUI; call this one from
        /// EditorApplication.update, delayCall and other non-GUI contexts.
        /// </summary>
        public bool HasActiveDragOutFrom(BrowserWindow window);

        /// <summary>
        /// Publishes the drag-out payload for the window under the cursor.
        /// MUST be called from inside OnGUI — the source BrowserWindow does this while a drag-out
        /// it started is in progress. This is the only place the drag payload is written.
        /// </summary>
        public void ApplyDragPayloadForCurrentTarget();

        /// <summary>
        /// Abandons an in-progress drag-out (e.g. a structure change invalidated the dragged
        /// items). Safe from any context: the payload itself is dropped by the next OnGUI pass.
        /// </summary>
        public void CancelActiveDragOut();

        /// <summary>
        /// Returns true when a drag that originated from <paramref name="thisWindow"/> has
        /// actually moved to at least one other window. False while the drag is still purely
        /// within <paramref name="thisWindow"/> (i.e. it may still be an internal reparent).
        /// Use this to guard the re-entry rejection so internal reparent drags are unaffected.
        /// </summary>
        public bool HasDragLeftSourceWindow(BrowserWindow thisWindow);

        /// <summary>
        /// Unity fires one DragExited to the source window the instant DragAndDrop.StartDrag()
        /// is called, while the internal drag is still alive. Returns true exactly once per
        /// drag-out session for that startup event so the caller can ignore it; any later
        /// DragExited is a real session end and returns false. (Used by the Windows-only
        /// deferred drag-out handoff.)
        /// </summary>
        public bool ConsumeStartupDragExited(BrowserWindow thisWindow);

        /// <summary>
        /// Attempts to execute a cross-window item transfer into <paramref name="dest"/>.
        /// Calls <c>DragAndDrop.AcceptDrag()</c> internally when transfer succeeds.
        /// Returns true when the transfer was handled (caller should Use the event and return).
        /// </summary>
        public bool ExecuteCrossWindowTransfer(
            BrowserWindow dest,
            int[] dropTargetPath,
            int dropPosition,
            TreeView treeView,
            SelectionStateSO selection,
            List<IStructure> collections);
    }
}
#endif