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
                    // Resolved through the registry rather than Resources.FindObjectsOfTypeAll:
                    // this getter sits on the per-row draw path, and the old scan walked every
                    // loaded object in the project (all scene GameObjects included) on each call.
                    return BrowserWindowRegistry.FindByInstanceID(currentWindowInstanceID);
                }
                catch
                {
                    // In case of errors during lookup (e.g., during domain reload), return null
                    return null;
                }
            }
        }

        /// <summary>
        /// Sets the current focused window and stores its instance ID for serialization.
        /// </summary>
        public void SetCurrentWindow(BrowserWindow window)
        {
            currentWindowInstanceID = window != null ? window.GetStableInstanceId() : 0;
        }
    }
}


