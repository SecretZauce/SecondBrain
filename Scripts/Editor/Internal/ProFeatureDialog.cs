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
        /// URL opened when the user clicks "What's on pro version?".
        /// Defaults to the free-vs-pro comparison; override to hide the affordance (e.g. in tests).
        /// </summary>
        public static string LearnMoreUrl { get; set; } = ProLicenseUtils.LEARN_MORE_URL;

        /// <summary>
        /// URL opened when the user clicks "Upgrade to PRO".
        /// Defaults to the Asset Store listing; override to hide the affordance (e.g. in tests).
        /// </summary>
        public static string UpgradeUrl { get; set; } = ProLicenseUtils.ASSET_STORE_URL;

        /// <summary>
        /// Show the PRO upgrade notice for the given feature name.
        /// </summary>
        /// <param name="featureName">Short display name of the gated feature, e.g. "Multiple Bases".</param>
        public static void Show(string featureName)
        {
            ProFeatureDialogWindow.Show(featureName, LearnMoreUrl, UpgradeUrl);
        }

#if SECOND_BRAIN_DEV
        [MenuItem("Tools/Second Brain/DEV ─ Dialogs/Preview: Pro License Dialog")]
        static void Dev_PreviewProLicenseDialog()
        {
            Show("Quick Browse");
        }
#endif
    }

    /// <summary>
    /// Internal custom dialog window for the PRO upgrade notice.
    /// </summary>
    internal class ProFeatureDialogWindow : EditorWindow
    {
        // ── Layout constants ───────────────────────────────────────────────────
        const float WindowWidth = 380f;
        const float Padding      = 20f;

        // ── State ──────────────────────────────────────────────────────────────
        string featureName;
        string learnMoreUrl;
        string upgradeUrl;
        Texture2D windowIcon;

        // ── Cached styles (created lazily inside OnGUI) ────────────────────────
        GUIStyle titleStyle;
        GUIStyle bodyStyle;

        // ── Entry point ────────────────────────────────────────────────────────
        public static void Show(string featureName, string learnMoreUrl, string upgradeUrl)
        {
            var wnd = CreateInstance<ProFeatureDialogWindow>();
            wnd.featureName   = featureName;
            wnd.learnMoreUrl  = learnMoreUrl;
            wnd.upgradeUrl    = upgradeUrl;
            wnd.windowIcon = Resources.Load<Texture2D>("Editor/Icons/second_brain_icon");
            wnd.titleContent = new GUIContent("SecondBrain PRO", wnd.windowIcon);
            wnd.minSize = new Vector2(WindowWidth, 100);
            wnd.maxSize = new Vector2(WindowWidth, 2000);

            // Horizontal centering; vertical settles on first Repaint
            var main = EditorGUIUtils.GetMainWindowRect();
            wnd.position = new Rect(
                main.x + (main.width - WindowWidth) * 0.5f,
                main.y + main.height * 0.35f,
                WindowWidth, 200);

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
        }

        // ── GUI ────────────────────────────────────────────────────────────────
        void OnGUI()
        {
            EnsureStyles();

            GUILayout.Space(Padding);

            // ── Icon + title row ───────────────────────────────────────────────────
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (windowIcon != null)
            {
                GUILayout.Label(windowIcon, GUILayout.Width(20), GUILayout.Height(20));
            }
            var headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 15, alignment = TextAnchor.MiddleLeft };
            GUILayout.Label("PRO Feature", headerStyle, GUILayout.Height(20), GUILayout.ExpandWidth(false));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            // ── Feature name ───────────────────────────────────────────────────
            GUILayout.Label($"\"{featureName}\" requires SecondBrain PRO.", bodyStyle);

            GUILayout.Space(4);

            // ── "What's on pro version?" link ─────────────────────────────────
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            DrawLinkButton("What's on pro version?", learnMoreUrl);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            // ── Spacer then button row ─────────────────────────────────────────
            GUILayout.Space(12);

            GUILayout.BeginHorizontal();
            GUILayout.Space(Padding);

            // Maybe Later
            if (GUILayout.Button("Maybe Later", GUILayout.Height(30)))
                Close();

            GUILayout.Space(8);

            // Upgrade to PRO
            using (new EditorGUI.DisabledGroupScope(string.IsNullOrEmpty(upgradeUrl)))
            {
                // ✨ (U+2728) is missing from the editor font and draws at zero width
                // before Unity 6, which would leave a dangling space in the label.
                string upgradeLabel = EmojiSupport.IsSupported ? "Upgrade to PRO ✨" : "Upgrade to PRO";
                if (GUILayout.Button(upgradeLabel, GUILayout.Height(30)))
                {
                    if (!string.IsNullOrEmpty(upgradeUrl))
                        Application.OpenURL(upgradeUrl);
                    Close();
                }
            }

            GUILayout.Space(Padding);
            GUILayout.EndHorizontal();

            GUILayout.Space(Padding);

            if (Event.current.type == EventType.Repaint)
            {
                float h = GUILayoutUtility.GetLastRect().yMax;
                if (Mathf.Abs(minSize.y - h) > 0.5f)
                {
                    minSize = maxSize = new Vector2(WindowWidth, h);
                    var main = EditorGUIUtils.GetMainWindowRect();
                    position = new Rect(
                        main.x + (main.width  - WindowWidth) * 0.5f,
                        main.y + (main.height - h) * 0.5f,
                        WindowWidth, h);
                }
            }
        }

        // ── Link helper (matches InstallerWindow.DrawLinkButton style) ─────────
        void DrawLinkButton(string label, string url)
        {
            bool hasUrl = !string.IsNullOrEmpty(url);
            var style = new GUIStyle(EditorStyles.miniLabel)
            {
                normal  = { textColor = hasUrl ? new Color(0.25f, 0.8f, 1f) : new Color(0.5f, 0.5f, 0.5f) },
                hover   = { textColor = new Color(0.45f, 0.70f, 1f) },
                padding = new RectOffset(0, 0, 2, 2),
                fontSize = 12
            };
            using (new EditorGUI.DisabledGroupScope(!hasUrl))
            {
                if (GUILayout.Button(label, style) && hasUrl)
                    Application.OpenURL(url);
            }
            var r = GUILayoutUtility.GetLastRect();
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - 1, r.width, 1),
                hasUrl ? new Color(0.25f, 0.55f, 1f, 0.6f) : new Color(0.5f, 0.5f, 0.5f, 0.3f));
        }
    }
}
