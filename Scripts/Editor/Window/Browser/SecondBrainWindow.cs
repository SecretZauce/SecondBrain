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

        // Shortcut: Shift+Q — open Default Base if assigned, otherwise open Home
        [Shortcut("Second Brain/Open Home Window", KeyCode.Q, ShortcutModifiers.Shift)]
        static void OpenWindow()
        {
            OpenDefaultOrHome();
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
                // fallback: ensure window opens
                BrowserWindow.OpenWindow<SecondBrainWindow>();
            }
        }
    }
}