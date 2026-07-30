using System;
using System.Collections.Generic;

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
            // Cheapest rejections first: with an empty selection — the common case — nothing else
            // needs to be evaluated. The manual comparison loop avoids the LINQ closure and
            // enumerator that would otherwise be allocated for every row, every GUI event.
            if (path == null || SelectedPaths == null || SelectedPaths.Count == 0)
                return false;

            // Resolved live, NOT cached per draw pass. Selection handlers call
            // WindowFocusManager.SetCurrentWindow mid-OnGUI (see SelectionStateSO), so a value
            // captured when this DrawContext was built goes stale within the very pass that uses
            // it — which is exactly when a cross-window drag decides what to pick up. The lookup
            // is a short walk over the open-window registry, so calling it per row is cheap.
            var focusedWindow = WindowFocusManager.GetCurrentWindow();
            if (focusedWindow != null && focusedWindow != window)
                return false;

            for (int i = 0; i < SelectedPaths.Count; i++)
            {
                var candidate = SelectedPaths[i];
                if (candidate == null || candidate.Length != path.Length)
                    continue;

                bool equal = true;
                for (int j = 0; j < path.Length; j++)
                {
                    if (candidate[j] == path[j])
                        continue;

                    equal = false;
                    break;
                }

                if (equal)
                    return true;
            }

            return false;
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