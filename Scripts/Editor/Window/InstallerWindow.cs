using System.Collections.Generic;
using SecretZauce.SecondBrain;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.PackageManager;
using UnityEngine;

namespace SecretZauce.SecondBrain.Editor
{
    public class InstallerWindow : EditorWindow
    {
        // ── URLs ──────────────────────────────────────────────────────────────────
        const string DocumentationUrl  = "https://secretzauce.gitbook.io/second-brain";
        const string DiscordUrl        = "https://discord.gg/secretzauce";
        const string ReportBugUrl      = "https://github.com/SecretZauce/second-brain/issues";
        const string ProFeaturesUrl    = ProLicenseUtils.ASSET_STORE_URL;
        const string FreePackageGitUrl = "https://github.com/SecretZauce/second-brain-free.git";

        // ── Assembly names ────────────────────────────────────────────────────────
        const string FreeAsmdefName = "SecretZauce.SecondBrain.Editor";
        const string ProAsmdefName  = "SecretZauce.SecondBrain.Pro.Editor";

        // ── Capsule tag colours ───────────────────────────────────────────────────
        static readonly Color TagVersion = new Color(0.22f, 0.22f, 0.22f);
        static readonly Color TagFree    = new Color(0.30f, 0.30f, 0.30f);
        static readonly Color TagPro     = new Color(0.15f, 0.38f, 0.72f);
        static readonly Color TagReady   = new Color(0.13f, 0.49f, 0.25f);
        static readonly Color TagWarning = new Color(0.65f, 0.42f, 0.06f);

        static readonly Dictionary<Color, Texture2D> capsuleCache = new Dictionary<Color, Texture2D>();

        // ── State ─────────────────────────────────────────────────────────────────
        Texture2D bannerTexture;
        bool freePresent;
        bool proPresent;
        bool proEnabled;

#if SECOND_BRAIN_DEV
        static bool s_devPreviewMissingFree;
#endif

        // ─────────────────────────────────────────────────────────────────────────

        [MenuItem("Window/Second Brain/Installer")]
        public static void Open()
        {
            var win = GetWindow<InstallerWindow>(utility: true, title: "SecondBrain Installer", focus: true);
            win.minSize = new Vector2(400, 460);
            win.Show();
        }

#if SECOND_BRAIN_DEV
        [MenuItem("Window/Second Brain/DEV ─ Dialogs/Preview: Missing Free Version")]
        static void Dev_PreviewMissingFreeVersion()
        {
            s_devPreviewMissingFree = true;
            Open();
        }
#endif

        void OnEnable()
        {
            bannerTexture = Resources.Load<Texture2D>("SecondBrainBanner");
        }

#if SECOND_BRAIN_DEV
        void OnDisable()
        {
            s_devPreviewMissingFree = false;
        }
#endif

        void OnGUI()
        {
            RefreshState();

            DrawBanner();
            DrawTagRow();
            GUILayout.Space(6);
            DrawLinkRow();
            GUILayout.Space(12);
            DrawMainButtons();

            if (freePresent && !proPresent)
            {
                GUILayout.Space(12);
                DrawGetProSection();
            }

            if (proPresent && !freePresent)
            {
                GUILayout.Space(12);
                DrawSetupSection();
            }
        }

        // ── State ─────────────────────────────────────────────────────────────────

        void RefreshState()
        {
#if SECOND_BRAIN_DEV
            if (s_devPreviewMissingFree)
            {
                freePresent = false;
                proPresent  = true;
                proEnabled  = false;
                return;
            }
#endif
            freePresent = IsFreeAssemblyPresent();
            proPresent  = IsProAssemblyPresent();
            proEnabled  = IsProEnabled();
        }

        // ── Sections ──────────────────────────────────────────────────────────────

        void DrawBanner()
        {
            if (bannerTexture != null)
            {
                float aspect = (float)bannerTexture.width / bannerTexture.height;
                float w      = position.width - 20f;
                float h      = w / aspect;
                var   rect   = GUILayoutUtility.GetRect(w, h);
                rect.x      += 10f;
                rect.width   = w;
                GUI.DrawTexture(rect, bannerTexture, ScaleMode.ScaleToFit);
                GUILayout.Space(4);
            }
            else
            {
                GUILayout.Space(8);
                var style = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize  = 22,
                    alignment = TextAnchor.MiddleCenter,
                };
                GUILayout.Label("SecondBrain", style);
                GUILayout.Space(4);
            }
        }

        // Replaces old DrawVersionBadge + DrawSetupStatus — all info as capsule tags.
        void DrawTagRow()
        {
            bool   isPro        = proEnabled && proPresent;
            string editionText  = isPro ? "Pro" : "Free";
            Color  editionColor = isPro ? TagPro : TagFree;

            string statusText;
            Color  statusColor;
            if (!freePresent && proPresent)
            {
                statusText  = "Setup Required";
                statusColor = TagWarning;
            }
            else if (freePresent && proPresent && proEnabled)
            {
                statusText  = "Ready";
                statusColor = TagReady;
            }
            else if (freePresent && proPresent && !proEnabled)
            {
                statusText  = "Enabling…";
                statusColor = TagWarning;
            }
            else
            {
                statusText  = "Installed";
                statusColor = TagReady;
            }

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            DrawCapsuleTag($"v{SecondBrainVersion.Current}", TagVersion, Color.white);
            GUILayout.Space(4);
            DrawCapsuleTag(editionText, editionColor, Color.white);
            GUILayout.Space(4);
            DrawCapsuleTag(statusText, statusColor, Color.white);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        void DrawLinkRow()
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            DrawLinkButton("Documentation", () => Application.OpenURL(DocumentationUrl));
            DrawLinkSeparator();
            DrawLinkButton("Discord", () => Application.OpenURL(DiscordUrl));
            DrawLinkSeparator();
            DrawLinkButton("Report a Bug", () => Application.OpenURL(ReportBugUrl));
            DrawLinkSeparator();
            DrawLinkButton("Changelog", () => ChangelogWindow.Open(proPresent));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        void DrawMainButtons()
        {
            GUILayout.BeginVertical(EditorStyles.helpBox);

            if (GUILayout.Button("Open SecondBrain Window", GUILayout.Height(30)))
                GetWindow<SecondBrainWindow>();

            GUILayout.EndVertical();
        }

        void DrawGetProSection()
        {
            GUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Upgrade to Pro", EditorStyles.boldLabel);
            GUILayout.Label(
                "Unlock ActionItems, QuickBrowse, QuickPeek, and SceneLink.",
                EditorStyles.wordWrappedLabel);
            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Get PRO Version", GUILayout.Height(26)))
                Application.OpenURL(ProLicenseUtils.ASSET_STORE_URL);
            if (GUILayout.Button("See Pro Features", GUILayout.Height(26)))
                Application.OpenURL(ProFeaturesUrl);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        void DrawSetupSection()
        {
            GUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("One-Time Setup Required", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "SecondBrain Pro requires the free package to be installed first. " +
                "Click below to install it automatically via the Package Manager.",
                MessageType.Warning);
            GUILayout.Space(4);

            var urlStyle = new GUIStyle(EditorStyles.label)
                { normal = { textColor = new Color(0.2f, 0.5f, 1f) } };
            if (GUILayout.Button($"Source: {FreePackageGitUrl}", urlStyle))
                Application.OpenURL(FreePackageGitUrl);

            GUILayout.Space(6);
            if (GUILayout.Button("Install Free Package", GUILayout.Height(28)))
            {
                Client.Add(FreePackageGitUrl);
                Close();
            }
            GUILayout.EndVertical();
        }

        // ── Link row helpers ──────────────────────────────────────────────────────

        static void DrawLinkButton(string label, System.Action onClick)
        {
            var style = new GUIStyle(EditorStyles.miniLabel)
            {
                normal  = { textColor = new Color(0.25f, 0.8f, 1f) },
                hover   = { textColor = new Color(0.45f, 0.70f, 1f) },
                padding = new RectOffset(0, 0, 2, 2),
                fontSize = 12
            };
            if (GUILayout.Button(label, style))
                onClick?.Invoke();
            var r = GUILayoutUtility.GetLastRect();
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - 1, r.width, 1), new Color(0.25f, 0.55f, 1f, 0.6f));
        }

        static void DrawLinkSeparator()
        {
            var style = new GUIStyle(EditorStyles.miniLabel)
            {
                normal  = { textColor = new Color(0.5f, 0.5f, 0.5f) },
                padding = new RectOffset(3, 3, 2, 2),
            };
            GUILayout.Label("|", style);
        }

        // ── Capsule tag rendering ─────────────────────────────────────────────────

        static void DrawCapsuleTag(string text, Color bgColor, Color textColor)
        {
            var style = new GUIStyle(EditorStyles.miniLabel)
            {
                normal    = { background = GetCapsuleTexture(bgColor), textColor = textColor },
                padding   = new RectOffset(10, 10, 3, 3),
                border    = new RectOffset(12, 12, 0, 0),
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize  = 10,
            };
            GUILayout.Label(text, style);
        }

        static Texture2D GetCapsuleTexture(Color color)
        {
            if (capsuleCache.TryGetValue(color, out var cached) && cached != null)
                return cached;

            var tex = BuildCapsuleTexture(48, 20, color);
            capsuleCache[color] = tex;
            return tex;
        }

        static Texture2D BuildCapsuleTexture(int w, int h, Color color)
        {
            var   tex    = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            float r      = (h - 1) / 2f;
            float cx1    = r;
            float cx2    = w - 1 - r;
            float cy     = (h - 1) / 2f;
            var   pixels = new Color32[w * h];
            byte  rb     = (byte)(color.r * 255);
            byte  gb     = (byte)(color.g * 255);
            byte  bb     = (byte)(color.b * 255);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float nearX = Mathf.Clamp(x, cx1, cx2);
                    float dx    = x - nearX;
                    float dy    = y - cy;
                    float dist  = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(r - dist + 0.8f) * color.a;
                    pixels[y * w + x] = new Color32(rb, gb, bb, (byte)(alpha * 255));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        // ── Presence / define helpers ─────────────────────────────────────────────

        static bool IsFreeAssemblyPresent()
            => AssetDatabase.FindAssets($"{FreeAsmdefName} t:AssemblyDefinitionAsset").Length > 0;

        static bool IsProAssemblyPresent()
            => AssetDatabase.FindAssets(
                $"{ProAsmdefName} t:AssemblyDefinitionAsset",
                new[] { "Assets" }).Length > 0;

        static bool IsProEnabled()
        {
            try
            {
                var target  = NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
                var defines = PlayerSettings.GetScriptingDefineSymbols(target);
                return defines.Contains("SECOND_BRAIN_PRO");
            }
            catch
            {
                return false;
            }
        }
    }
}
