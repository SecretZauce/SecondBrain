using UnityEditor;
using UnityEngine;

namespace SecretZauce.SecondBrain.Editor
{
    /// <summary>
    /// Base class for floating picker popup windows (e.g. EmojiTray, ColorTray).
    /// Handles shared lifecycle: singleton tracking, float-as-popup, and close-on-focus-loss.
    /// </summary>
    public abstract class PickerTrayBase : EditorWindow
    {
        // Tracks the single open picker across all subclasses so opening one closes another.
        protected static PickerTrayBase _currentTray;
        
        // When true, OnLostFocus will not automatically close the tray. Useful when a child
        // popup window has been opened and we want to keep the tray alive until the popup closes.
        static bool _suspendAutoClose = false;

        // Public accessor so other UI (e.g. BrowserWindow) can detect when any picker tray is open.
        public static bool IsAnyTrayOpen => _currentTray != null;

        protected static void CloseCurrentTray()
        {
            if (_currentTray != null)
            {
                _currentTray.Close();
                _currentTray = null;
            }
        }

        protected void ShowAsFloatingDialog(Vector2 screenPosition, int width, int height)
        {
            var rect = new Rect(screenPosition.x, screenPosition.y, width, height);
            position = rect;
            ShowPopup();
            Focus();
        }

        void OnLostFocus()
        {
            if (!_suspendAutoClose)
                Close();
        }

        protected static void SuspendAutoClose()
        {
            _suspendAutoClose = true;
        }

        protected static void ResumeAutoClose()
        {
            _suspendAutoClose = false;
        }

        void OnDestroy()
        {
            if (_currentTray == this)
                _currentTray = null;
        }
    }
}
