using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SecretZauce.SecondBrain.Editor
{
    /// <summary>
    /// Serializable wrapper for a path (list of integers)
    /// </summary>
    [Serializable]
    public class SerializablePath
    {
        public List<int> Path = new List<int>();
        
        public SerializablePath() { }
        
        public SerializablePath(int[] path)
        {
            if (path != null)
                Path = new List<int>(path);
        }
        
        public int[] ToArray()
        {
            return Path?.ToArray() ?? Array.Empty<int>();
        }
    }
    
    public class SelectionStateSO : ScriptableObject
    {
        [SerializeField] List<SerializablePath> selectedPaths = new List<SerializablePath>();
        
        public static Action<BrowserWindow> OnSelectionChangedInternally;

        // Parallel HashSet for O(1) containment checks. Rebuilt lazily when dirty.
        [System.NonSerialized] HashSet<string> _pathKeySet;
        [System.NonSerialized] int _pathKeySetVersion = -1; // tracks when to rebuild
        [System.NonSerialized] int _listVersion;            // incremented on every mutation

        HashSet<string> PathKeySet
        {
            get
            {
                if (_pathKeySet == null || _pathKeySetVersion != _listVersion)
                {
                    _pathKeySet ??= new HashSet<string>(StringComparer.Ordinal);
                    _pathKeySet.Clear();
                    foreach (var sp in selectedPaths)
                    {
                        if (sp?.Path != null)
                            _pathKeySet.Add(PathToKey(sp.Path));
                    }
                    _pathKeySetVersion = _listVersion;
                }
                return _pathKeySet;
            }
        }

        static string PathToKey(List<int> path)
        {
            if (path == null || path.Count == 0) return "";
            if (path.Count == 1) return path[0].ToString();
            if (path.Count == 2) return path[0].ToString() + "," + path[1].ToString();
            var sb = new System.Text.StringBuilder(path.Count * 3);
            sb.Append(path[0]);
            for (int i = 1; i < path.Count; i++) { sb.Append(','); sb.Append(path[i]); }
            return sb.ToString();
        }

        static string PathToKey(int[] path)
        {
            if (path == null || path.Length == 0) return "";
            if (path.Length == 1) return path[0].ToString();
            if (path.Length == 2) return path[0].ToString() + "," + path[1].ToString();
            var sb = new System.Text.StringBuilder(path.Length * 3);
            sb.Append(path[0]);
            for (int i = 1; i < path.Length; i++) { sb.Append(','); sb.Append(path[i]); }
            return sb.ToString();
        }

        bool ContainsPath(int[] path)
        {
            return PathKeySet.Contains(PathToKey(path));
        }
        
        /// <summary>
        /// Gets the primary selected path (first in multi-selection or legacy single path)
        /// </summary>
        public int[] GetPrimaryPath()
        {
            if (selectedPaths != null && selectedPaths.Count > 0)
                return selectedPaths[0].ToArray();
            return Array.Empty<int>();
        }
        
        /// <summary>
        /// Gets the last selected path (anchor for range selection).
        /// This is the most recently clicked item, used as the starting point for Shift+Click.
        /// </summary>
        public int[] GetLastSelectedPath()
        {
            return selectedPaths.Count > 0 ? selectedPaths[selectedPaths.Count - 1].ToArray() : Array.Empty<int>();
        }
        
        /// <summary>
        /// Gets all selected paths
        /// </summary>
        public List<int[]> GetAllPaths()
        {
            if (selectedPaths is { Count: > 0 })
                return selectedPaths.Select(p => p.ToArray()).ToList();
            return new List<int[]>();
        }
        
        /// <summary>
        /// Sets a single selection (clears multi-selection)
        /// </summary>
        public void SetSingleSelection(BrowserWindow window, int[] path)
        {
            selectedPaths.Clear();
            if (path is { Length: > 0 })
            {
                selectedPaths.Add(new SerializablePath(path));
            }
            _listVersion++;
            NotifySelectionChangedInternally(window);
        }
        
        /// <summary>
        /// Adds a path to the multi-selection
        /// </summary>
        public void AddToSelection(BrowserWindow window, int[] path)
        {
            if (!TryAddToSelectionWithoutNotify(path)) 
                return;
            NotifySelectionChangedInternally(window);
        }

        public bool TryAddToSelectionWithoutNotify(int[] path)
        {
            if (path == null || path.Length == 0)
                return false;

            if (ContainsPath(path))
                return true;

            selectedPaths.Add(new SerializablePath(path));
            _listVersion++;
            return true;
        }

        /// <summary>
        /// Removes a path from the multi-selection
        /// </summary>
        public void RemoveFromSelection(BrowserWindow window, int[] path)
        {
            if (path == null || path.Length == 0)
                return;

            selectedPaths.RemoveAll(p => PathsEqual(p.Path, path));
            _listVersion++;
            NotifySelectionChangedInternally(window);
        }
        
        /// <summary>
        /// Toggles a path in the multi-selection
        /// </summary>
        public void ToggleSelection(BrowserWindow window, int[] path)
        {
            if (path == null || path.Length == 0)
                return;

            if (ContainsPath(path))
                RemoveFromSelection(window, path);
            else
                AddToSelection(window, path);
        }
        
        /// <summary>
        /// Clears all selections
        /// </summary>
        public void ClearSelection(BrowserWindow window)
        {
            ClearSelectionWithoutNotify();
            NotifySelectionChangedInternally(window);
        }

        public void ClearSelectionWithoutNotify()
        {
            selectedPaths.Clear();
            _listVersion++;
        }

        /// <summary>
        /// Adds a range selection from the current primary selection to the target path.
        /// Preserves existing selections and adds the range to them.
        /// </summary>
        public void SetRangeSelection(BrowserWindow window, int[] fromPath, int[] toPath, List<int[]> allVisiblePaths)
        {
            if (allVisiblePaths == null || allVisiblePaths.Count == 0)
                return;
            
            // Find indices of from and to paths in the visible list
            int fromIndex = -1;
            int toIndex = -1;
            
            for (int i = 0; i < allVisiblePaths.Count; i++)
            {
                if (fromPath != null && StructureUtils.ArePathsEqual(allVisiblePaths[i], fromPath))
                    fromIndex = i;
                if (toPath != null && StructureUtils.ArePathsEqual(allVisiblePaths[i], toPath))
                    toIndex = i;
            }
            
            // If either not found, just add the to path to selection
            if (fromIndex == -1 || toIndex == -1)
            {
                AddToSelection(window, toPath);
                return;
            }
            
            // Ensure fromIndex is the smaller one
            if (fromIndex > toIndex)
            {
                (fromIndex, toIndex) = (toIndex, fromIndex);
            }
            
            // Add range to existing selection (don't clear)
            for (int i = fromIndex; i <= toIndex; i++)
            {
                var path = allVisiblePaths[i];
                if (!ContainsPath(path))
                    selectedPaths.Add(new SerializablePath(path));
            }
            _listVersion++;
            NotifySelectionChangedInternally(window);
        }

        static bool PathsEqual(List<int> list, int[] arr)
        {
            if (list.Count != arr.Length) return false;
            for (int i = 0; i < arr.Length; i++)
                if (list[i] != arr[i]) return false;
            return true;
        }

        void NotifySelectionChangedInternally(BrowserWindow window)
        {
            // Update the WindowFocusManager to track which window is making the selection change
            // This is important for undo/redo to restore focus to the correct window
            try
            {
                var currentWindow = WindowFocusManager.GetCurrentWindow();
                if (currentWindow != window)
                {
                    WindowFocusManager.SetCurrentWindow(window);
                }
            }
            catch
            {
                // In rare cases WindowFocusManager might fail (e.g., during domain reload)
                // Continue with the notification even if focus tracking fails
            }
            
            // Notify all browser windows that a selection changed
            // Each window will repaint if it's not the selector window
            OnSelectionChangedInternally?.Invoke(window);
        }
    }
}



