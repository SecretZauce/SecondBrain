using UnityEditor;
using UnityEngine;

namespace SecretZauce.SecondBrain.Editor
{
    /// <summary>
    /// Shows a consistently-styled "SecondBrain PRO required" dialog.
    /// Call <see cref="Show"/> from any editor code that needs to advertise a PRO feature
    /// to a free user.
    /// </summary>
    public static class ProFeatureDialog
    {
        /// <summary>
        /// URL opened when the user clicks "Upgrade to PRO" or "What's on pro version?".
        /// Set this once at startup (e.g. from an <see cref="InitializeOnLoadAttribute"/> class)
        /// or leave it null to hide those affordances.
        /// </summary>
        public static string LearnMoreUrl { get; set; } = null;

        /// <summary>
        /// Show the PRO upgrade notice for the given feature name.
        /// </summary>
        /// <param name="featureName">Short display name of the gated feature, e.g. "Multiple Bases".</param>
        public static void Show(string featureName)
        {
            ProFeatureDialogWindow.Show(featureName, LearnMoreUrl);
        }
    }

    /// <summary>
    /// Internal custom dialog window for the PRO upgrade notice.
    /// </summary>
    internal class ProFeatureDialogWindow : EditorWindow
    {
        // ── Layout constants ───────────────────────────────────────────────────
        const float WindowWidth  = 380f;
        const float WindowHeight = 210f;
        const float Padding      = 20f;

        // ── State ──────────────────────────────────────────────────────────────
        string featureName;
        string upgradeUrl;

        // ── Cached styles (created lazily inside OnGUI) ────────────────────────
        GUIStyle titleStyle;
        GUIStyle bodyStyle;
        GUIStyle linkStyle;

        // ── Entry point ────────────────────────────────────────────────────────
        public static void Show(string featureName, string upgradeUrl)
        {
            var wnd = CreateInstance<ProFeatureDialogWindow>();
            wnd.featureName = featureName;
            wnd.upgradeUrl  = upgradeUrl;
            wnd.titleContent = new GUIContent("SecondBrain PRO");
            wnd.minSize = wnd.maxSize = new Vector2(WindowWidth, WindowHeight);

            // Centre over the main editor window
            var main = EditorGUIUtils.GetMainWindowRect();
            wnd.position = new Rect(
                main.x + (main.width  - WindowWidth)  * 0.5f,
                main.y + (main.height - WindowHeight)  * 0.5f,
                WindowWidth, WindowHeight);

            wnd.ShowUtility();   // modal-ish: stays on top, no docking
        }

        // ── Style helpers ──────────────────────────────────────────────────────
        void EnsureStyles()
        {
            if (titleStyle != null) return;

            titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize  = 15,
                alignment = TextAnchor.MiddleCenter,
                wordWrap  = true
            };

            bodyStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
            {
                fontSize  = 12,
                alignment = TextAnchor.MiddleCenter
            };

            linkStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize        = 11,
                alignment       = TextAnchor.MiddleCenter,
                normal          = { textColor = new Color(0.25f, 0.55f, 1f) },
                hover           = { textColor = new Color(0.40f, 0.70f, 1f) },
                stretchWidth    = false
            };
        }

        // ── GUI ────────────────────────────────────────────────────────────────
        void OnGUI()
        {
            EnsureStyles();

            GUILayout.Space(Padding);

            // ── Icon row (star emoji as a stand-in; lightweight, no asset needed) ──
            var iconStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize  = 28,
                alignment = TextAnchor.MiddleCenter
            };
            GUILayout.Label("⭐ PRO Feature", iconStyle, GUILayout.Height(36));

            GUILayout.Space(6);

            // ── Feature name ───────────────────────────────────────────────────
            GUILayout.Label($"\"{featureName}\" requires SecondBrain PRO.", bodyStyle);

            GUILayout.Space(4);

            // ── "What's on pro version?" link ─────────────────────────────────
            {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                var linkContent = new GUIContent("What's on pro version?");
                var linkRect    = GUILayoutUtility.GetRect(linkContent, linkStyle);

                bool hasUrl = !string.IsNullOrEmpty(upgradeUrl);

                // Underline
                if (Event.current.type == EventType.Repaint)
                {
                    var underlineColor = hasUrl
                        ? new Color(0.25f, 0.55f, 1f, 0.7f)
                        : new Color(0.5f, 0.5f, 0.5f, 0.4f);
                    EditorGUI.DrawRect(new Rect(linkRect.x, linkRect.yMax - 1f, linkRect.width, 1f), underlineColor);
                }

                if (hasUrl)
                {
                    EditorGUIUtility.AddCursorRect(linkRect, MouseCursor.Link);
                    if (GUI.Button(linkRect, linkContent, linkStyle))
                        Application.OpenURL(upgradeUrl);
                }
                else
                {
                    // No URL configured — render as a dimmed non-interactive label
                    var dimStyle = new GUIStyle(linkStyle)
                    {
                        normal = { textColor = new Color(0.5f, 0.5f, 0.5f) }
                    };
                    GUI.Label(linkRect, linkContent, dimStyle);
                }

                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }

            // ── Spacer then button row ─────────────────────────────────────────
            GUILayout.FlexibleSpace();

            GUILayout.BeginHorizontal();
            GUILayout.Space(Padding);

            // Maybe Later
            if (GUILayout.Button("Maybe Later", GUILayout.Height(30)))
                Close();

            GUILayout.Space(8);

            // Upgrade to PRO
            using (new EditorGUI.DisabledGroupScope(string.IsNullOrEmpty(upgradeUrl)))
            {
                if (GUILayout.Button("Upgrade to PRO ✨", GUILayout.Height(30)))
                {
                    if (!string.IsNullOrEmpty(upgradeUrl))
                        Application.OpenURL(upgradeUrl);
                    Close();
                }
            }

            GUILayout.Space(Padding);
            GUILayout.EndHorizontal();

            GUILayout.Space(Padding);
        }
    }
}
