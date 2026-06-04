#if SECOND_BRAIN_PRO
using System.Collections.Generic;
using SecretZauce.SecondBrain;
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