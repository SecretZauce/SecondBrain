using UnityEngine;
using UnityEditor;

namespace SecretZauce.SecondBrain.Editor
{
    /// <summary>
    /// Static class that manages access to the WindowFocusHistorySO singleton.
    /// Provides centralized initialization and getter for the current window focus state.
    /// </summary>
    public static class WindowFocusManager
    {
        static WindowFocusHistorySO instance;

        /// <summary>
        /// Gets the WindowFocusHistorySO instance, creating it if necessary.
        /// </summary>
        public static WindowFocusHistorySO Instance
        {
            get
            {
                EnsureInstance();
                return instance;
            }
        }

        /// <summary>
        /// Ensures the WindowFocusHistorySO instance exists.
        /// </summary>
        static void EnsureInstance()
        {
            if (instance != null)
                return;

            instance = ScriptableObject.CreateInstance<WindowFocusHistorySO>();
            instance.hideFlags = HideFlags.DontSave;
        }

        /// <summary>
        /// Gets the currently focused BrowserWindow.
        /// </summary>
        public static BrowserWindow GetCurrentWindow()
        {
            EnsureInstance();
            return instance.CurrentWindow;
        }

        /// <summary>
        /// Sets the current focused window and marks the instance dirty.
        /// Does not record undo - caller is responsible for undo recording.
        /// </summary>
        public static void SetCurrentWindow(BrowserWindow window)
        {
            EnsureInstance();
            instance.SetCurrentWindow(window);
            EditorUtility.SetDirty(instance);
        }
    }
}

