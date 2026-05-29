using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SecretZauce.SecondBrain.Editor
{
    /// <summary>
    /// Helper class to manage undo/redo operations for the Browser window.
    /// Tracks deletions and attempts to restore selections when undo is performed.
    /// </summary>
    public class UndoHelper
    {
        FocusHistorySO focusHistory;
        bool focusHasBeenRecorded; // Track if RecordFocus has been called in this session
        
        public bool CanGoBack
        {
            get
            {
                EnsureFocusHistorySO();
                return focusHistory != null && focusHistory.CanGoBack;
            }
        }

        public bool CanGoForward
        {
            get
            {
                EnsureFocusHistorySO();
                return focusHistory != null && focusHistory.CanGoForward;
            }
        }
        
        public UndoHelper(IStructure currentTarget)
        {
            EnsureFocusHistorySO(currentTarget);
            focusHasBeenRecorded = false;
        }

        int RecordUndo(Object obj, string operationName)
        {
            Undo.IncrementCurrentGroup();
            Undo.RecordObject(obj, operationName);
            Undo.SetCurrentGroupName(operationName);
            return Undo.GetCurrentGroup();
        }

        /// <summary>
        /// When undo/redo occurs we may need to re-apply the focus based on the focus history's current target.
        /// This method will set the window's target to the focusHistory's current target without recording a new undo.
        /// </summary>
        public bool RestoreFocusFromUndo(BrowserController browserController)
        {
            if (focusHistory == null)
                return false;
            
            // Only restore focus if RecordFocus was actually called in this session
            // This prevents false positives where Unity's undo system restores focusHistory
            // to an earlier state when we're only undoing selection changes
            if (!focusHasBeenRecorded)
                return false;
            
            var stored = focusHistory.CurrentTarget;
            var currentBase = browserController.Root;
            if (ReferenceEquals(currentBase, stored))
                return false;
            
            browserController.HostWindow.SetTarget(stored as Base);
            return true;
        }

        public void GoForward(BrowserController browserController)
        {
            EnsureFocusHistorySO();
            if (!focusHistory.CanGoForward) 
                return;
            RecordUndo(focusHistory, "Forward Focus");
            var next = focusHistory.GoForward();
            EditorUtility.SetDirty(focusHistory);
            focusHasBeenRecorded = true;
            browserController.HostWindow.SetTarget(next as Base);
        }

        public void GoBack(BrowserController browserController)
        {
            EnsureFocusHistorySO();
            if (!focusHistory.CanGoBack) 
                return;
            
            RecordUndo(focusHistory, "Back Focus");
            var prev = focusHistory.GoBack();
            EditorUtility.SetDirty(focusHistory);
            focusHasBeenRecorded = true;
            browserController.HostWindow.SetTarget(prev as Base);
        }

        public void EnsureFocusHistorySO(IStructure currentRoot = null)
        {
            if (focusHistory != null) 
                return;
            
            focusHistory = ScriptableObject.CreateInstance<FocusHistorySO>();
            focusHistory.name = "Focus History"; // Set explicit name to control undo display
            focusHistory.Initialize(currentRoot as Object);
            focusHistory.hideFlags = HideFlags.DontSave;
        }

        public void RecordFocus(Object newTarget)
        {
            RecordUndo(focusHistory, "Change Focus");
            focusHistory.SetTarget(newTarget);
            EditorUtility.SetDirty(focusHistory);
            focusHasBeenRecorded = true;
        }
    }
}
