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

        // HashSet for O(1) path-selected lookups instead of O(selections × path_length) per row.
        // Keyed on the path array itself rather than a formatted string: IsPathSelected runs
        // twice per row per IMGUI pass, and building a key string there allocated on every call.
        readonly HashSet<int[]> _selectedPathKeys;

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

            // Build HashSet from selected paths for O(1) lookups during draw.
            _selectedPathKeys = new HashSet<int[]>(SelectedPaths.Count, StructureUtils.PathComparer.Instance);
            for (int i = 0; i < SelectedPaths.Count; i++)
            {
                var p = SelectedPaths[i];
                if (p != null)
                    _selectedPathKeys.Add(p);
            }
        }

        /// <summary>
        /// Checks if a path is selected
        /// </summary>
        public bool IsPathSelected(BrowserWindow window, int[] path)
        {
            if (path == null || _selectedPathKeys.Count == 0)
                return false;

            // Resolved live, NOT cached per draw pass. Selection handlers call
            // WindowFocusManager.SetCurrentWindow mid-OnGUI (see SelectionStateSO), so a value
            // captured when this DrawContext was built goes stale within the very pass that uses
            // it — which is exactly when a cross-window drag decides what to pick up.
            var focusedWindow = WindowFocusManager.GetCurrentWindow();
            if (focusedWindow != null && focusedWindow != window)
                return false;

            return _selectedPathKeys.Contains(path);
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