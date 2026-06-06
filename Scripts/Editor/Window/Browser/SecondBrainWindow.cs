using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace SecretZauce.SecondBrain.Editor
{
    public class SecondBrainWindow : BrowserWindow
    {
        protected override IStructure HomeRoot => Motherbase.Home;
        
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

        // Shortcut: Option+Q (Mac) / Alt+Q (Win) — focus existing window, or open one if none exists
        [Shortcut("Second Brain/Focus Window", KeyCode.Q, ShortcutModifiers.Alt)]
        static void FocusWindow()
        {
            var existing = Resources.FindObjectsOfTypeAll<SecondBrainWindow>();
            if (existing != null && existing.Length > 0)
                existing[0].Focus();
            else
                BrowserWindow.OpenWindow<SecondBrainWindow>();
        }

        // Shortcut: Ctrl+Shift+Q (Win) / Option+Shift+Q (Mac) — open new floating window each time
#if UNITY_EDITOR_OSX
        [Shortcut("Second Brain/Open New Window", KeyCode.Q, ShortcutModifiers.Shift | ShortcutModifiers.Alt)]
#else
        [Shortcut("Second Brain/Open New Window", KeyCode.Q, ShortcutModifiers.Shift | ShortcutModifiers.Control)]
#endif
        static void OpenWindow()
        {
            try
            {
                var newWindow = CreateInstance<SecondBrainWindow>();
                newWindow.Show();
                newWindow.Focus();
                var mother = Motherbase.Home;
                if (mother != null && mother.DefaultBase != null)
                {
                    EditorApplication.delayCall += () =>
                    {
                        try { newWindow.SetTarget(mother.DefaultBase); } catch { }
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
                var mother = Motherbase.Home;
                BrowserWindow.OpenWindow<SecondBrainWindow>();
                var wnd = EditorWindow.GetWindow<SecondBrainWindow>();
                if (mother != null && mother.DefaultBase != null)
                {
                    try { wnd.SetTarget(mother.DefaultBase); } catch { }
                }
            }
            catch
            {
                BrowserWindow.OpenWindow<SecondBrainWindow>();
            }
        }
    }
}