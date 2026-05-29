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

                var colorTarget = target as IHasColor;

                if (target is IHasEmoji emojiTarget)
                    DrawEmojiButton(emojiTarget, iconSize);
                else
                    GUILayout.Space(iconSize);

                GUILayout.Space(8);

                if (colorTarget != null)
                    DrawColorButton(colorTarget, colorSize);
                else
                    GUILayout.Space(colorSize);

                GUILayout.FlexibleSpace();
            }
        }

        void DrawEmojiButton(IHasEmoji emojiTarget, float iconSize)
        {
            string emojiValue = emojiTarget.EmojiIcon;
            GUIContent btnContent;

            if (!string.IsNullOrEmpty(emojiValue) && EmojiIconUtils.IsEditorIcon(emojiValue))
            {
                Texture tex = EmojiIconUtils.GetEditorIconTexture(emojiValue);
                btnContent = tex != null ? new GUIContent(tex, "Click to change icon") : new GUIContent("?", "Click to change icon");
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
                EmojiTray.ShowForObject((Object)emojiTarget, screenPos);
            }

            var labelStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };
            EditorGUI.LabelField(new Rect(btnRect.x, btnRect.yMax + 1, btnRect.width, 14), "Icon", labelStyle);
        }

        void DrawColorButton(IHasColor colorTarget, float colorSize)
        {
            Color swatchColor = colorTarget.HasLabelColor ? colorTarget.LabelColor : (EditorGUIUtility.isProSkin ? new Color(0.3f, 0.3f, 0.3f) : new Color(0.75f, 0.75f, 0.75f));

            Rect btnRect = GUILayoutUtility.GetRect(colorSize, colorSize, GUILayout.Width(colorSize), GUILayout.Height(colorSize));

            Color borderColor = EditorGUIUtility.isProSkin ? new Color(0.1f, 0.1f, 0.1f) : new Color(0.4f, 0.4f, 0.4f);
            EditorGUI.DrawRect(btnRect, borderColor);
            Rect inner = new Rect(btnRect.x + 1, btnRect.y + 1, btnRect.width - 2, btnRect.height - 2);
            EditorGUI.DrawRect(inner, swatchColor);

            if (GUI.Button(btnRect, GUIContent.none, GUIStyle.none))
            {
                var screenPos = GUIUtility.GUIToScreenPoint(new Vector2(btnRect.x, btnRect.yMax));
                var colorTargets = new List<IHasColor> { colorTarget };
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

