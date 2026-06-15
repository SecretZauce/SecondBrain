using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace SecretZauce.SecondBrain.Editor
{
    public class SecondBrainWindow : BrowserWindow
    {
        protected override IStructure HomeRoot => Profile.Active;

        // Menu item: open Home (no default-base navigation)
        [MenuItem("Window/Second Brain (Home)")]
        static void OpenHomeMenu()
        {
            BrowserWindow.OpenWindow<SecondBrainWindow>();
        }

        // Menu item: open Default Base (if set) - falls back to Home when not assigned
        [MenuItem("Window/Second Brain (Default Base)")]
        static void OpenDefaultBaseMenu()
        {
            OpenDefaultOrHome();
        }

        // Shortcut: Shift+Q — focus existing window, or open one if none exists
        [Shortcut("Second Brain/Focus Window", KeyCode.W, ShortcutModifiers.Shift)]
        static void FocusWindow()
        {
            var existing = Resources.FindObjectsOfTypeAll<SecondBrainWindow>();
            if (existing != null && existing.Length > 0)
                existing[0].Focus();
            else
                BrowserWindow.OpenWindow<SecondBrainWindow>();
        }

        // Shortcut: Ctrl+Shift+W (Win) / Option+Shift+W (Mac) — open new floating window each time
#if UNITY_EDITOR_OSX
        [Shortcut("Second Brain/Open New Window", KeyCode.W, ShortcutModifiers.Shift | ShortcutModifiers.Alt)]
#else
        [Shortcut("Second Brain/Open New Window", KeyCode.W, ShortcutModifiers.Shift | ShortcutModifiers.Control)]
#endif
        static void OpenWindow()
        {
            try
            {
                var newWindow = CreateInstance<SecondBrainWindow>();
                newWindow.Show();
                newWindow.Focus();
                var profile = Profile.Active;
                if (profile != null && profile.DefaultBase != null)
                {
                    EditorApplication.delayCall += () =>
                    {
                        try { newWindow.SetTarget(profile.DefaultBase); } catch { }
                    };
                }
            }
            catch
            {
                BrowserWindow.OpenWindow<SecondBrainWindow>();
            }
        }

        static void OpenDefaultOrHome()
        {
            try
            {
                var profile = Profile.Active;
                BrowserWindow.OpenWindow<SecondBrainWindow>();
                var wnd = EditorWindow.GetWindow<SecondBrainWindow>();
                if (profile != null && profile.DefaultBase != null)
                {
                    try { wnd.SetTarget(profile.DefaultBase); } catch { }
                }
            }
            catch
            {
                BrowserWindow.OpenWindow<SecondBrainWindow>();
            }
        }
    }
}