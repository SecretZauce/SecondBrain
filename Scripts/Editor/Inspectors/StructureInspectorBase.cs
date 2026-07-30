using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SecretZauce.SecondBrain.Editor
{
    /// <summary>
    /// Base class for custom inspectors that share icon/emoji and label-color UX.
    /// ContainerEditor and BaseInspector should inherit from this to reuse the
    /// Emoji/Color row rendering and helper buttons.
    /// </summary>
    public abstract class StructureInspectorBase : UnityEditor.Editor
    {
        protected void DrawIconColorRow()
        {
            EditorGUILayout.LabelField("Appearance", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                // Compute sizes based on available inspector width so elements scale
                float viewWidth = EditorGUIUtility.currentViewWidth;
                float reserved = 60f;
                float usable = Mathf.Max(80f, viewWidth - reserved);

                float iconSize = Mathf.Clamp(usable * 0.12f, 28f, 56f);
                float colorSize = Mathf.Clamp(usable * 0.12f, 28f, 56f);

                var emojiTargets = new List<IHasEmoji>();
                var colorTargets = new List<IHasColor>();
                foreach (var t in targets)
                {
                    if (t is IHasEmoji e) emojiTargets.Add(e);
                    if (t is IHasColor c) colorTargets.Add(c);
                }

                if (emojiTargets.Count > 0)
                    DrawEmojiButton(emojiTargets, iconSize);
                else
                    GUILayout.Space(iconSize);

                GUILayout.Space(8);

                if (colorTargets.Count > 0)
                    DrawColorButton(colorTargets, colorSize);
                else
                    GUILayout.Space(colorSize);

                GUILayout.FlexibleSpace();
            }
        }

        void DrawEmojiButton(List<IHasEmoji> emojiTargets, float iconSize)
        {
            string emojiValue = emojiTargets[0].EmojiIcon;
            bool mixed = emojiTargets.Exists(t => t.EmojiIcon != emojiValue);

            GUIContent btnContent;
            if (mixed)
            {
                btnContent = new GUIContent("—", "Multiple different icons — click to set for all selected");
            }
            else if (!string.IsNullOrEmpty(emojiValue) && EmojiIconUtils.IsEditorIcon(emojiValue))
            {
                Texture tex = EmojiIconUtils.GetEditorIconTexture(emojiValue);
                btnContent = tex != null ? new GUIContent(tex, "Click to change icon") : new GUIContent("?", "Click to change icon");
            }
            else if (!string.IsNullOrEmpty(emojiValue) && !EmojiSupport.IsSupported)
            {
                // The glyph would draw at zero width here, leaving an empty-looking button.
                var warn = EditorGUIUtility.IconContent("console.warnicon.sml").image;
                string tip = EmojiSupport.UnsupportedTooltip + " Click to change icon.";
                btnContent = warn != null ? new GUIContent(warn, tip) : new GUIContent("!", tip);
            }
            else if (!string.IsNullOrEmpty(emojiValue))
            {
                btnContent = new GUIContent(emojiValue, "Click to change icon");
            }
            else
            {
                btnContent = new GUIContent("☺", "Click to set icon");
            }

            var emojiStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 22,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(2, 2, 2, 2)
            };

            Rect btnRect = GUILayoutUtility.GetRect(iconSize, iconSize, GUILayout.Width(iconSize), GUILayout.Height(iconSize));

            if (GUI.Button(btnRect, btnContent, emojiStyle))
            {
                var screenPos = GUIUtility.GUIToScreenPoint(new Vector2(btnRect.x, btnRect.yMax));
                EmojiTray.ShowForObjects(emojiTargets, screenPos);
            }

            var labelStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };
            EditorGUI.LabelField(new Rect(btnRect.x, btnRect.yMax + 1, btnRect.width, 14), "Icon", labelStyle);
        }

        void DrawColorButton(List<IHasColor> colorTargets, float colorSize)
        {
            var first = colorTargets[0];
            bool mixed = colorTargets.Exists(t =>
                t.HasLabelColor != first.HasLabelColor ||
                (first.HasLabelColor && t.LabelColor != first.LabelColor));

            Color swatchColor = !mixed && first.HasLabelColor
                ? first.LabelColor
                : (EditorGUIUtility.isProSkin ? new Color(0.3f, 0.3f, 0.3f) : new Color(0.75f, 0.75f, 0.75f));

            Rect btnRect = GUILayoutUtility.GetRect(colorSize, colorSize, GUILayout.Width(colorSize), GUILayout.Height(colorSize));

            Color borderColor = EditorGUIUtility.isProSkin ? new Color(0.1f, 0.1f, 0.1f) : new Color(0.4f, 0.4f, 0.4f);
            EditorGUI.DrawRect(btnRect, borderColor);
            Rect inner = new Rect(btnRect.x + 1, btnRect.y + 1, btnRect.width - 2, btnRect.height - 2);
            EditorGUI.DrawRect(inner, swatchColor);

            if (mixed)
            {
                var mixedStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                EditorGUI.LabelField(inner, "?", mixedStyle);
            }

            if (GUI.Button(btnRect, GUIContent.none, GUIStyle.none))
            {
                var screenPos = GUIUtility.GUIToScreenPoint(new Vector2(btnRect.x, btnRect.yMax));
                ColorTray.ShowForObjects(colorTargets, screenPos);
            }

            if (btnRect.Contains(Event.current.mousePosition))
            {
                EditorGUI.DrawRect(btnRect, new Color(1f, 1f, 1f, 0.12f));
                if (Event.current.type == EventType.MouseMove)
                    Repaint();
            }

            var colorLabelStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };
            EditorGUI.LabelField(new Rect(btnRect.x, btnRect.yMax + 1, btnRect.width, 14), "Color", colorLabelStyle);
        }
    }
}

