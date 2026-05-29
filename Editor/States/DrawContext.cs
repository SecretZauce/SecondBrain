using System;
using System.Collections.Generic;
using System.Linq;

namespace SecretZauce.SecondBrain.Editor
{
    /// <summary>
    /// Context class to hold drawing state and callbacks
    /// </summary>
    public class DrawContext
    {
        public List<IStructure> Collections { get; }
        public List<int[]> SelectedPaths { get; } 
        public event Action<int[], bool, bool> OnItemSelected; // path, isMultiSelect (Ctrl/Cmd), isRangeSelect (Shift)
        public event Action RecordSelectionChange;
        
        public DrawContext(
            List<IStructure> items,
            SelectionStateSO selectionState,
            Action<int[], bool, bool> onItemSelected,
            Action recordSelectionChange)
        {
            Collections = items;
            SelectedPaths = selectionState.GetAllPaths() ?? new List<int[]>();
            if (onItemSelected != null) OnItemSelected += onItemSelected;
            if (recordSelectionChange != null) RecordSelectionChange += recordSelectionChange;
        }
        
        /// <summary>
        /// Checks if a path is selected
        /// </summary>
        public bool IsPathSelected(BrowserWindow window, int[] path)
        {
            var currentWindow = WindowFocusManager.GetCurrentWindow();
            if (currentWindow != window && currentWindow != null)
                return false;
            
            if (path == null || SelectedPaths == null)
                return false;
            
            return SelectedPaths.Any(p => p != null && p.SequenceEqual(path));
        }

        /// <summary>
        /// Helper to raise the OnItemSelected event from external callers.
        /// Events themselves can only be invoked from inside the declaring class; use this helper.
        /// </summary>
        public void RaiseOnItemSelected(int[] path, bool isMultiSelect, bool isRangeSelect)
        {
            OnItemSelected?.Invoke(path, isMultiSelect, isRangeSelect);
        }

        /// <summary>
        /// Helper to raise the RecordSelectionChange event from external callers.
        /// </summary>
        public void RaiseRecordSelectionChange()
        {
            RecordSelectionChange?.Invoke();
        }
    }
}