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
        /// Loads and returns the <see cref="Texture"/> for a stored editor icon value.
        /// Returns <c>null</c> when the value is not an editor icon or the icon cannot be found.
        /// </summary>
        public static Texture GetEditorIconTexture(string emojiIconValue)
        {
            string path = GetIconPath(emojiIconValue);
            if (path == null)
                return null;
            return EditorGUIUtility.IconContent(path).image;
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

                return new GUIContent(emojiIconValue + " " + nodeName, fallbackImage);
            }

            return new GUIContent(nodeName, fallbackImage);
        }
    }
}
