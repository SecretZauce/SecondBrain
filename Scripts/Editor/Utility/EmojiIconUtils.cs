using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SecretZauce.SecondBrain.Editor
{
    /// <summary>
    /// Utility helpers for the combined emoji / Unity Editor Icon system.
    /// Values stored in <see cref="IHasEmoji.EmojiIcon"/> that begin with
    /// <see cref="EditorIconPrefix"/> are treated as Unity Editor icon paths
    /// All other non-empty values are treated as regular emoji characters.
    /// </summary>
    internal static class EmojiIconUtils
    {
        /// <summary>Prefix that marks a stored value as a Unity Editor icon path.</summary>
        const string EditorIconPrefix = "#icon:";

        /// <summary>Returns true when the stored value represents a Unity Editor icon.</summary>
        public static bool IsEditorIcon(string emojiIconValue)
            => !string.IsNullOrEmpty(emojiIconValue) && emojiIconValue.StartsWith(EditorIconPrefix);

        /// <summary>
        /// Converts an editor-icon path (without prefix) to the full stored value.
        /// </summary>
        public static string ToStoredValue(string iconPath) => EditorIconPrefix + iconPath;

        /// <summary>
        /// Extracts the icon path from a stored value that already has the prefix.
        /// Returns <c>null</c> when the value is not an editor icon.
        /// </summary>
        public static string GetIconPath(string emojiIconValue)
        {
            if (!IsEditorIcon(emojiIconValue))
                return null;
            return emojiIconValue.Substring(EditorIconPrefix.Length);
        }

        /// <summary>
        /// Scale factor to apply to editor icons so they visually match emoji size.
        /// </summary>
        const float EditorIconScale = 0.75f;

        // Cache of scaled icon textures by editor icon path.
        static readonly Dictionary<string, Texture> s_ScaledIconCache = new Dictionary<string, Texture>();

        /// <summary>
        /// Loads and returns the <see cref="Texture"/> for a stored editor icon value.
        /// Returns <c>null</c> when the value is not an editor icon or the icon cannot be found.
        /// Editor icons are scaled by <see cref="EditorIconScale"/> and cached to avoid
        /// re-allocating textures on every repaint.
        /// </summary>
        public static Texture GetEditorIconTexture(string emojiIconValue)
        {
            string path = GetIconPath(emojiIconValue);
            if (path == null)
                return null;

            if (s_ScaledIconCache.TryGetValue(path, out var cached))
                return cached;

            var content = EditorGUIUtility.IconContent(path);
            var original = content?.image;
            if (original == null)
            {
                s_ScaledIconCache[path] = null;
                return null;
            }

            // If the original texture is a Texture2D, create a smaller copy scaled by EditorIconScale.
            // For non-Texture2D textures, fall back to returning the original.
            try
            {
                if (original is Texture2D tex2D)
                {
                    int newW = Mathf.Max(1, Mathf.RoundToInt(tex2D.width * EditorIconScale));
                    int newH = Mathf.Max(1, Mathf.RoundToInt(tex2D.height * EditorIconScale));

                    // Render the source texture into a RenderTexture at the target size then read back.
                    var rt = RenderTexture.GetTemporary(newW, newH, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Linear);
                    var prev = RenderTexture.active;
                    Graphics.Blit(tex2D, rt);
                    RenderTexture.active = rt;

                    var scaled = new Texture2D(newW, newH, TextureFormat.RGBA32, false);
                    scaled.ReadPixels(new Rect(0, 0, newW, newH), 0, 0);
                    scaled.Apply();

                    RenderTexture.active = prev;
                    RenderTexture.ReleaseTemporary(rt);

                    s_ScaledIconCache[path] = scaled;
                    return scaled;
                }
            }
            catch
            {
                // If scaling fails for any reason, fall back to original image.
            }

            s_ScaledIconCache[path] = original;
            return original;
        }

        /// <summary>
        /// Attempts to draw an editor icon (from a stored editor-icon value) at the left
        /// edge of <paramref name="labelRect"/> and then draws <paramref name="text"/>
        /// to the right of the icon. Returns true when an editor icon was found and
        /// drawn; when false the caller should fall back to their normal drawing logic.
        /// The method also outputs the final text rect so callers can position other UI
        /// (for example the pencil button) relative to the drawn text.
        /// </summary>
        public static bool TryDrawEditorIconLabel(Rect labelRect, string emojiIconValue, GUIStyle labelStyle, string text, out Rect outTextRect, float iconShift = 2f, float gapAfterIcon = 2f)
        {
            outTextRect = labelRect;
            if (!IsEditorIcon(emojiIconValue)) return false;
            string path = GetIconPath(emojiIconValue);
            if (string.IsNullOrEmpty(path)) return false;

            var rawTex = EditorGUIUtility.IconContent(path).image;
            if (rawTex == null) return false;

            // Choose an icon draw height that matches the label's line height if possible so
            // the icon visually aligns with emoji/glyph size.
            float targetLineHeight = labelStyle != null && labelStyle.lineHeight > 0f ? labelStyle.lineHeight : labelRect.height * 0.75f;
            float drawH = Mathf.Min(targetLineHeight, labelRect.height * 0.85f);
            float aspect = rawTex.height > 0f ? (rawTex.width / (float)rawTex.height) : 1f;
            float drawW = drawH * aspect;

            var iconRect = new Rect(labelRect.x + iconShift, labelRect.y + (labelRect.height - drawH) * 0.5f, drawW, drawH);
            GUI.DrawTexture(iconRect, rawTex, ScaleMode.ScaleToFit, true);

            outTextRect = new Rect(iconRect.xMax + gapAfterIcon, labelRect.y, labelRect.xMax - (iconRect.xMax + gapAfterIcon), labelRect.height);
            EditorGUI.LabelField(outTextRect, new GUIContent(text), labelStyle);
            return true;
        }

        /// <summary>
        /// Attempts to draw only the editor icon (no text) inside <paramref name="labelRect"/>.
        /// Outputs the icon rect and the computed icon draw width so callers can layout text
        /// or padding accordingly. Returns true when an editor icon was found and drawn.
        /// </summary>
        public static bool TryDrawEditorIcon(Rect labelRect, string emojiIconValue, GUIStyle labelStyle, out Rect outIconRect, out float outIconWidth, float iconShift = 2f)
        {
            outIconRect = Rect.zero;
            outIconWidth = 0f;
            if (!IsEditorIcon(emojiIconValue)) return false;
            string path = GetIconPath(emojiIconValue);
            if (string.IsNullOrEmpty(path)) return false;

            var rawTex = EditorGUIUtility.IconContent(path).image;
            if (rawTex == null) return false;

            float targetLineHeight = labelStyle != null && labelStyle.lineHeight > 0f ? labelStyle.lineHeight : labelRect.height * 0.75f;
            float drawH = Mathf.Min(targetLineHeight, labelRect.height * 0.85f);
            float aspect = rawTex.height > 0f ? (rawTex.width / (float)rawTex.height) : 1f;
            float drawW = drawH * aspect;

            outIconRect = new Rect(labelRect.x + iconShift, labelRect.y + (labelRect.height - drawH) * 0.5f, drawW, drawH);
            GUI.DrawTexture(outIconRect, rawTex, ScaleMode.ScaleToFit, true);
            outIconWidth = drawW;
            return true;
        }

        /// <summary>
        /// Builds a <see cref="GUIContent"/> for displaying a node label that may
        /// have either an emoji or a Unity Editor icon assigned.
        /// </summary>
        /// <param name="nodeName">Plain display name of the node.</param>
        /// <param name="emojiIconValue">Value from <see cref="IHasEmoji.EmojiIcon"/>.</param>
        /// <param name="fallbackImage">Texture to use when neither emoji nor editor icon is set.</param>
        public static GUIContent BuildLabelContent(string nodeName, string emojiIconValue, Texture fallbackImage = null)
        {
            if (!string.IsNullOrEmpty(emojiIconValue))
            {
                if (IsEditorIcon(emojiIconValue))
                {
                    Texture iconTex = GetEditorIconTexture(emojiIconValue);
                    return new GUIContent(nodeName, iconTex ?? fallbackImage);
                }

                // Editors older than Unity 6 lay emoji out with zero width: prefixing the
                // name would only add a blank gap. Drop the glyph and explain via tooltip.
                if (!EmojiSupport.IsSupported)
                    return new GUIContent(nodeName, fallbackImage, EmojiSupport.UnsupportedTooltip);

                return new GUIContent(emojiIconValue + " " + nodeName, fallbackImage);
            }

            return new GUIContent(nodeName, fallbackImage);
        }
    }
}
