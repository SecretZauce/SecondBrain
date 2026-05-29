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
        // Store the instance ID of the current window so it can be serialized for undo/redo
        [SerializeField] int currentWindowInstanceID;

        /// <summary>
        /// Gets the currently focused BrowserWindow by looking up the stored instance ID.
        /// Returns null if no window is stored or if the stored window no longer exists.
        /// </summary>
        public BrowserWindow CurrentWindow
        {
            get
            {
                if (currentWindowInstanceID == 0)
                    return null;
                
                try
                {
                    // Look up the EditorWindow by instance ID
                    var allWindows = Resources.FindObjectsOfTypeAll<BrowserWindow>();
                    foreach (var window in allWindows)
                    {
                        if (window != null && window.GetInstanceID() == currentWindowInstanceID)
                            return window;
                    }
                }
                catch
                {
                    // In case of errors during lookup (e.g., during domain reload), return null
                    return null;
                }
                
                // Window with stored ID no longer exists
                return null;
            }
        }

        /// <summary>
        /// Sets the current focused window and stores its instance ID for serialization.
        /// </summary>
        public void SetCurrentWindow(BrowserWindow window)
        {
            currentWindowInstanceID = window != null ? window.GetInstanceID() : 0;
        }
    }
}


