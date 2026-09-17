using UnityEngine;

namespace SecretZauce.SecondBrain.Editor
{
    /// <summary>
    /// ScriptableObject that tracks the current focused BrowserWindow.
    /// Supports undo/redo for window focus changes.
    /// This SO is always marked DontSave, so it doesn't persist across domain reloads.
    /// </summary>
    public class WindowFocusHistorySO : ScriptableObject
    {
        // Serialized as an object reference so undo/redo can restore it — no instance ID to
        // mint, store or look up.
        [SerializeField] BrowserWindow currentWindow;

        /// <summary>
        /// Gets the currently focused BrowserWindow.
        /// Returns null if no window is stored or if the stored window is no longer registered.
        /// </summary>
        public BrowserWindow CurrentWindow
        {
            get
            {
                // Registry check keeps the old semantics: a window that was closed, or is disabled
                // mid domain reload, counts as gone. Non-allocating, as this getter sits on the
                // per-row draw path. Returns a real null, never a destroyed-object wrapper.
                return BrowserWindowRegistry.Contains(currentWindow) ? currentWindow : null;
            }
        }

        /// <summary>
        /// Sets the current focused window for serialization.
        /// </summary>
        public void SetCurrentWindow(BrowserWindow window)
        {
            currentWindow = window != null ? window : null;
        }
    }
}


