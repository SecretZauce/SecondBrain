using System.Collections.Generic;
using UnityEngine;

namespace SecretZauce.SecondBrain.Editor
{
    // Result of processing a global keyboard event inside the tree view
    public class KeyInputResult
    {
        public bool Handled { get; set; }
        public bool NeedsRepaint { get; set; }
        public bool DuplicateRequested { get; set; }
        public bool SelectAllRequested { get; set; }
        // Request to delete currently selected items (via Delete/Backspace)
        public bool DeleteRequested { get; set; }
        // Request to "enter" a leaf node (via Enter key) - triggers '>' button action
        public bool EnterRequested { get; set; }
        public int[] EnterTargetPath { get; set; }
        // Request to navigate back in browser history (via Ctrl+Left)
        public bool BackRequested { get; set; }
        // Request to navigate forward in browser history (via Ctrl+Right)
        public bool ForwardRequested { get; set; }
        public bool FoldoutToggled { get; set; }
        public bool SelectionChanged { get; set; }
        public int[] NewPrimaryPath { get; set; }
        // Request to rename the primary selected item (via Ctrl+R)
        public bool RenameRequested { get; set; }
        // Request to navigate to the home root (via Escape)
        public bool GoHomeRequested { get; set; }
        // Request to paste clipboard text as a TextAsset child of the selected Container (via Ctrl+V)
        public bool PasteRequested { get; set; }
        // Request to navigate into a Base (popup mode: Enter on a Base at Home level)
        public bool NavigateIntoBaseRequested { get; set; }
        public int[] NavigateIntoBasePath { get; set; }
        // Request to open the property editor for a leaf that has no dedicated Enter action
        // (e.g. SceneComponentRef / SceneObjectRef with scene loaded, in non-popup browser).
        public bool OpenPropertyEditorRequested { get; set; }
        public int[] OpenPropertyEditorTargetPath { get; set; }
        // Request to wrap all selected items in a new Container (Ctrl+G)
        public bool WrapGroupRequested { get; set; }
        // Request to create a new Container — child of selected container, or at root if none (Ctrl+N)
        public bool CreateContainerRequested { get; set; }
    }

    // Result of processing global drag-related events inside the tree view
    public class DragInputResult
    {
        public bool Handled { get; set; }
        public bool Cancelled { get; set; }
        public bool DropPerformed { get; set; }
        public bool IsExternal { get; set; }
        public List<int[]> DraggedPaths { get; set; }
        public List<Object> DraggedItems { get; set; }
        public int[] DropTargetPath { get; set; }
        public int DropPosition { get; set; }
        public bool NeedsRepaint { get; set; }
    }
}

