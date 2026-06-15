using System;
using UnityEditor;
using UnityEngine;

namespace SecretZauce.SecondBrain.Editor
{
    /// <summary>
    /// Encapsulates the toolbar UI used by BrowserWindow.
    /// Responsibilities:
    /// - Render toolbar buttons
    /// - Toggle detail panel visibility
    /// - Trigger pop-out window
    /// - Position and open the settings popup
    /// </summary>
    public class BrowserToolbar
    {
        readonly BrowserWindow ownerWindow;

        static readonly GUIContent ShowDetailsContent = new ("Show Details", "Show the right detail panel");
        static readonly GUIContent HideDetailsContent = new ("Hide Details", "Hide the right detail panel");

        static GUIContent s_PrevContent;
        static GUIContent s_NextContent;
        static GUIStyle   s_PlusButtonStyle;
        static Texture    s_SettingsIcon;
        static Texture    s_NewTabIcon;
        static Texture    s_BackIcon;
        static Texture    s_ForwardIcon;
        static Texture    s_CloseIcon;
        static bool       s_SettingsIconProSkin;
        static GUIStyle   s_HintStyle;

#if !SECOND_BRAIN_PRO
        static GUIStyle _upgradeLinkStyle;
        static GUIStyle UpgradeLinkStyle
        {
            get
            {
                if (_upgradeLinkStyle == null)
                {
                    _upgradeLinkStyle = new GUIStyle(EditorStyles.linkLabel)
                    {
                        fontSize = 11,
                        fontStyle = FontStyle.Normal,
                        alignment = TextAnchor.MiddleCenter,
                        padding = new RectOffset(4, 4, 0, 6),
                    };
                    _upgradeLinkStyle.normal.textColor = new Color(0.33f, 0.66f, 1f);
                    _upgradeLinkStyle.hover.textColor = new Color(0.5f, 0.8f, 1f);
                }
                return _upgradeLinkStyle;
            }
        }
#endif

        public BrowserToolbar(BrowserWindow window)
        {
            ownerWindow = window ?? throw new ArgumentNullException(nameof(window));
        }
        
        public void Draw()
        {
            bool iconPs = EditorGUIUtility.isProSkin;
            if (s_SettingsIcon == null || s_SettingsIconProSkin != iconPs)
            {
                s_SettingsIconProSkin = iconPs;
                s_SettingsIcon = IconUtils.Load("settings");
                s_NewTabIcon   = IconUtils.Load("new_tab");
                s_BackIcon     = IconUtils.Load("back");
                s_ForwardIcon  = IconUtils.Load("forward");
                s_CloseIcon    = IconUtils.Load("close");
                s_PrevContent  = new GUIContent(s_BackIcon    ?? (Texture)EditorGUIUtility.IconContent("d_tab_prev@2x").image, "Go Back");
                s_NextContent  = new GUIContent(s_ForwardIcon ?? (Texture)EditorGUIUtility.IconContent("d_tab_next@2x").image, "Go Forward");
            }

            var tv = ownerWindow.TreeView;
            bool blocked = tv != null && (tv.HasGhostSession || tv.Renamer.IsRenamingAny || tv.IsRenamingHeader);

            // Draw navigation and toolbar
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            EditorGUI.BeginDisabledGroup(blocked || !ownerWindow.Controller.UndoHelper.CanGoBack);
            if (GUILayout.Button(s_PrevContent, EditorStyles.toolbarButton, GUILayout.Width(24)))
                ownerWindow.Controller.UndoHelper.GoBack(ownerWindow.Controller);
            EditorGUI.EndDisabledGroup();
            EditorGUI.BeginDisabledGroup(blocked || !ownerWindow.Controller.UndoHelper.CanGoForward);
            if (GUILayout.Button(s_NextContent, EditorStyles.toolbarButton, GUILayout.Width(24)))
                ownerWindow.Controller.UndoHelper.GoForward(ownerWindow.Controller);
            EditorGUI.EndDisabledGroup();
            float toolbarHeight = EditorGUIUtility.singleLineHeight + 6f;
            EditorGUI.BeginDisabledGroup(blocked);
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.Height(toolbarHeight));
            
            if (ownerWindow.IsPopup)
            {
                s_HintStyle ??= new GUIStyle(EditorStyles.miniLabel)
                {
                    normal = { textColor = new Color(0.5f, 0.5f, 0.5f) },
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(4, 4, 0, 0),
                };
                var mainKey = "Alt+W";
#if UNITY_EDITOR_OSX
                mainKey = "Option+W";
#endif
                GUILayout.Label("Press " + mainKey +" (or ESC twice) to close", s_HintStyle);
            }

            GUILayout.FlexibleSpace();

            var visible = ownerWindow.ShowDetailPanel;

            if (ownerWindow.hasDetailPanel)
            {
                if (GUILayout.Button(visible ? HideDetailsContent : ShowDetailsContent,
                        EditorStyles.toolbarButton, GUILayout.Width(90)))
                {
                    ownerWindow.ToggleDetail();
                } 
            }
            
#if !SECOND_BRAIN_PRO
            // "Upgrade to PRO" — link-style, no background
            var upgradeLabel = new GUIContent("✦ Upgrade to PRO", "Get SecondBrain PRO");
            if (GUILayout.Button(upgradeLabel, UpgradeLinkStyle, GUILayout.Height(toolbarHeight - 2)))
            {
#if SECOND_BRAIN_DEV
                // DEV mock: add the scripting define directly so PRO code paths activate immediately.
                if (EditorUtility.DisplayDialog("Upgrade to PRO (Mock — DEV only)",
                        "This will define SECOND_BRAIN_PRO for all build platforms and recompile scripts.\n\nContinue?",
                        "Yes, Upgrade", "Cancel"))
                {
                    ProLicenseUtils.AddProDefine();
                }
#else
                // Production: send user to the Asset Store page.
                UnityEngine.Application.OpenURL(ProLicenseUtils.ASSET_STORE_URL);
#endif
            }
            // Reset cursor — linkLabel sets a beam cursor we don't want here
            EditorGUIUtility.AddCursorRect(GUILayoutUtility.GetLastRect(), MouseCursor.Arrow);
#endif

            s_PlusButtonStyle ??= new GUIStyle(EditorStyles.toolbarButton)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(0, 0, 0, 0)
            };
#if SECOND_BRAIN_PRO
            if (GUILayout.Button(new GUIContent(s_NewTabIcon, "New Tab"), s_PlusButtonStyle, GUILayout.Width(30), GUILayout.Height(toolbarHeight - 2)))
            {
                ownerWindow.PopOutNewWindow();
            }
#else
            if (GUILayout.Button(new GUIContent(s_NewTabIcon, "New Tab (SecondBrain PRO)"), s_PlusButtonStyle, GUILayout.Width(30), GUILayout.Height(toolbarHeight - 2)))
            {
                ProFeatureDialog.Show("Multiple Tabs");
            }
#endif
           
            Texture settingsTexture = (Texture)s_SettingsIcon ?? EditorGUIUtility.IconContent("d_Settings").image;
            var settingsContent = new GUIContent(settingsTexture, "Settings");
            var settingsRect = GUILayoutUtility.GetRect(settingsContent, EditorStyles.toolbarButton, GUILayout.Width(30));
            var settingsControlId = GUIUtility.GetControlID(FocusType.Passive, settingsRect);
            var settingsEvent = Event.current;
            switch (settingsEvent.GetTypeForControl(settingsControlId))
            {
                case EventType.MouseDown:
                    if (!blocked && settingsEvent.button == 0 && settingsRect.Contains(settingsEvent.mousePosition))
                    {
                        ShowSettings(settingsRect);
                        settingsEvent.Use();
                    }
                    break;
                case EventType.Repaint:
                    EditorStyles.toolbarButton.Draw(settingsRect, settingsContent, settingsControlId, false);
                    break;
            }
            
            var closeContent = s_CloseIcon != null ? new GUIContent(s_CloseIcon, "Close") : new GUIContent("✕", "Close");
            if (GUILayout.Button(closeContent, EditorStyles.toolbarButton, GUILayout.Width(30)))
            {
                EditorApplication.delayCall += ownerWindow.Close;
            }

            EditorGUILayout.EndHorizontal();
            EditorGUI.EndDisabledGroup(); // blocked group for inner horizontal
        }

        void ShowSettings(Rect btnRect)
        {
            SettingsWindow.TogglePopup(ownerWindow.position, btnRect);
        }
    }
}

