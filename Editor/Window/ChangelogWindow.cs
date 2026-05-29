using UnityEditor;
using UnityEngine;

namespace SecretZauce.SecondBrain.Editor
{
    internal class ChangelogWindow : EditorWindow
    {
        const string FreeChangelogPath = "SecondBrainChangelog";
        const string ProChangelogPath  = "SecondBrainProChangelog";

        string content;
        Vector2 scroll;

        public static void Open(bool includePro)
        {
            var win = GetWindow<ChangelogWindow>(utility: true, title: "Changelog", focus: true);
            win.minSize = new Vector2(460, 420);
            win.maxSize = new Vector2(600, 700);
            win.LoadContent(includePro);
            win.Show();
        }

        void LoadContent(bool includePro)
        {
            var free = Resources.Load<TextAsset>(FreeChangelogPath);
            var pro  = includePro ? Resources.Load<TextAsset>(ProChangelogPath) : null;

            if (free == null && pro == null)
            {
                content = "No changelog found.";
                return;
            }

            if (free != null && pro != null)
                content = pro.text + "\n\n---\n\n" + free.text;
            else if (pro != null)
                content = pro.text;
            else
                content = free.text;
        }

        void OnGUI()
        {
            // Title bar
            var titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize  = 13,
                alignment = TextAnchor.MiddleLeft,
                padding   = new RectOffset(12, 0, 8, 4),
            };
            GUILayout.Label("Changelog", titleStyle);

            // Divider
            var rect = GUILayoutUtility.GetRect(0, 1, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0.3f, 0.3f, 0.3f, 0.5f));
            GUILayout.Space(4);

            // Scrollable content
            scroll = GUILayout.BeginScrollView(scroll,
                GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            var textStyle = new GUIStyle(EditorStyles.label)
            {
                wordWrap  = true,
                richText  = false,
                padding   = new RectOffset(12, 12, 0, 8),
                fontSize  = 11,
            };
            GUILayout.Label(content ?? string.Empty, textStyle);
            GUILayout.EndScrollView();

            // Close button
            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close", GUILayout.Width(80), GUILayout.Height(24)))
                Close();
            GUILayout.Space(8);
            GUILayout.EndHorizontal();
            GUILayout.Space(6);
        }
    }
}
