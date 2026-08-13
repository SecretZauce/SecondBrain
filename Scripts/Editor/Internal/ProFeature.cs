using System.Collections.Generic;
using Object = UnityEngine.Object;

namespace SecretZauce.SecondBrain.Editor
{
    /// <summary>
    /// The free/Pro boundary. <see cref="Provider"/> is null in the free edition and non-null
    /// once the Pro assembly has registered itself — that null check is the only Pro test in the
    /// free assembly, which contains no <c>#if SECOND_BRAIN_PRO</c> anywhere.
    /// </summary>
    public static class ProFeature
    {
        public static IProFeatureProvider Provider { get; private set; }
        public static void RegisterResolver(IProFeatureProvider provider)
        {
            Provider = provider;
        }
    }

    /// <summary>
    /// Implemented by the Pro assembly. Every type named here lives in the free assembly, which
    /// is what lets free call Pro through a runtime null check rather than a compile-time define.
    ///
    /// THIS INTERFACE IS APPEND-ONLY. Free auto-updates from Git the moment a user asks for it;
    /// Pro ships through Asset Store review and is updated by hand, so a newer free routinely
    /// meets an older Pro. Changing or removing a member breaks that Pro build's compilation
    /// outright — not one feature, the whole project.
    ///
    /// When adding a member, give it a default implementation so an older Pro still compiles and
    /// the feature is simply inert there:
    ///
    ///     bool DoSomethingNew(BrowserWindow window) => false;
    ///
    /// Need a new parameter? Add an overload that forwards to the existing one; do not edit the
    /// existing signature. Only when a member is genuinely required does the asmdef's
    /// versionDefines lower bound move — and that stops every Pro build already in customers'
    /// hands from compiling until they update.
    /// </summary>
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

        /// <summary>
        /// Same question as <see cref="IsCrossWindowDragFromThisWindow"/>, but answered without
        /// reading Unity's DragAndDrop state. Unity keeps that state on the current GUIState, so
        /// the other member is only valid inside OnGUI; call this one from
        /// EditorApplication.update, delayCall and other non-GUI contexts.
        /// <para>
        /// Default: false. On a Pro build that predates this member the drag-out publishes its own
        /// payload from EditorApplication.update, so free must not drive it from OnGUI.
        /// </para>
        /// </summary>
        public bool HasActiveDragOutFrom(BrowserWindow window) => false;

        /// <summary>
        /// Publishes the drag-out payload for the window under the cursor.
        /// MUST be called from inside OnGUI — the source BrowserWindow does this while a drag-out
        /// it started is in progress. This is the only place the drag payload is written.
        /// <para>Default: inert; reached only when HasActiveDragOutFrom returned true.</para>
        /// </summary>
        public void ApplyDragPayloadForCurrentTarget() { }

        /// <summary>
        /// Abandons an in-progress drag-out (e.g. a structure change invalidated the dragged
        /// items). Safe from any context: the payload itself is dropped by the next OnGUI pass.
        /// <para>Default: inert; an older Pro cleans up through its own DragExited path.</para>
        /// </summary>
        public void CancelActiveDragOut() { }
    }
}