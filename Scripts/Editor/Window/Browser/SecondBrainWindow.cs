using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace SecretZauce.SecondBrain.Editor
{
    public class SecondBrainWindow : BrowserWindow
    {
        protected override IStructure HomeRoot => Profile.Active;

        // [MenuItem] is a compile-time attribute, so it cannot be gated on a runtime
        // Provider check. Pro's extra entry points ("Home" / "Default Base", which only make
        // sense once multiple Profiles/Bases exist) are declared by the Pro assembly instead.
        [MenuItem("Window/Second Brain Window")]
        static void OpenWindowMenu()
        {
            OpenDefaultBase();
        }

        // Shortcut: Shift+Q — focus existing window, or open one if none exists
        [Shortcut("Second Brain/Focus Window", KeyCode.W, ShortcutModifiers.Shift)]
        static void FocusWindow()
        {
            var existing = BrowserWindowRegistry.AllOfType<SecondBrainWindow>();
            if (existing.Count > 0)
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

        // Called by the Pro assembly's "Default Base" menu item — public because there is no
        // InternalsVisibleTo between the free and Pro assemblies.
        public static void OpenDefaultOrHome()
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

        /// <summary>
        /// Opens (or reveals) the window and navigates straight into the active Profile's
        /// Default Base, falling back to its first Base if no default is assigned. Uses
        /// SetTarget directly (no undo) — this is an entry point, not an in-window navigation
        /// action, and Free only ever has the one Base to land on.
        /// </summary>
        internal static void OpenDefaultBase()
        {
            try
            {
                var profile = Profile.Active;
                BrowserWindow.OpenWindow<SecondBrainWindow>();
                var wnd = EditorWindow.GetWindow<SecondBrainWindow>();
                var target = profile != null ? profile.DefaultBase : null;
                if (target == null && profile != null && profile.Children.Count > 0)
                    target = profile.Children[0];
                if (target != null)
                {
                    try { wnd.SetTarget(target); } catch { }
                }
            }
            catch
            {
                BrowserWindow.OpenWindow<SecondBrainWindow>();
            }
        }
    }
}