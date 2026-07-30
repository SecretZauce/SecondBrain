using UnityEditor;
using UnityEngine;

namespace SecretZauce.SecondBrain.Editor
{
    /// <summary>
    /// Detects whether the running Unity Editor can actually draw emoji in IMGUI.
    ///
    /// Unity 6 (6000.0) generates IMGUI text through TextCore, which decodes surrogate
    /// pairs and falls back to an OS font (Segoe UI Emoji / Apple Color Emoji) for glyphs
    /// the editor font is missing. Unity 2022.3 and older use the legacy text path: it has
    /// no OS font fallback, so any codepoint missing from the editor font (Inter) is laid
    /// out with an advance of 0 — emoji do not render as a "tofu" box, they render as
    /// nothing at all, leaving a stray space in front of the label.
    ///
    /// Measured on Unity 2022.3.62f3 vs 6000.0.58f2 with <c>GUIStyle.CalcSize</c>:
    /// <code>
    ///   glyph                 2022.3    6000.0
    ///   "A"                    11.0      10.8
    ///   "❤" (heart)       16.0      16.4
    ///   "⭐" (star)         0.0      22.0
    ///   "\U0001F600" (grin)     0.0      22.0
    /// </code>
    /// </summary>
    public static class EmojiSupport
    {
        /// <summary>Lowest Unity version verified to render emoji in IMGUI.</summary>
        public const string MinimumVersionLabel = "Unity 6 (6000.0)";

        /// <summary>Astral-plane emoji used to measure whether text layout produces a glyph.</summary>
        const string ProbeGlyph = "\U0001F600";

        static bool? s_Supported;

        /// <summary>
        /// True when emoji characters are drawn with a real glyph in this editor.
        /// The result is cached once it can be measured; before the GUI skin exists
        /// (batch mode, very early domain reload) this reports <c>false</c> without
        /// caching, so the check runs again on the next GUI pass.
        /// </summary>
        public static bool IsSupported
        {
            get
            {
                if (s_Supported.HasValue)
                    return s_Supported.Value;

                bool? probed = Probe();
                if (probed.HasValue)
                    s_Supported = probed;
                return probed ?? false;
            }
        }

        /// <summary>
        /// Returns true/false when emoji rendering could be measured, or null when the
        /// GUI is not ready yet and the caller should try again later.
        /// </summary>
        static bool? Probe()
        {
#if UNITY_6000_0_OR_NEWER
            return true;
#else
            try
            {
                var style = EditorStyles.label;
                if (style == null)
                    return null;

                // CalcSize includes the style's padding, so measuring the glyph on its own
                // never reports 0. Measure how much width the glyph *adds* instead — the
                // padding cancels out and an unsupported editor yields a delta of 0.
                float plain = style.CalcSize(new GUIContent("A")).x;
                float withGlyph = style.CalcSize(new GUIContent("A" + ProbeGlyph)).x;

                // Sanity-check that text metrics are live before trusting a zero delta.
                if (plain < 1f)
                    return null;

                return (withGlyph - plain) > 1f;
            }
            catch
            {
                return null;
            }
#endif
        }

        /// <summary>Short reason shown next to disabled emoji UI.</summary>
        public static string UnsupportedMessage =>
            $"Emoji need {MinimumVersionLabel} or newer.\n" +
            $"This editor ({Application.unityVersion}) draws emoji with zero width, so they are " +
            "invisible in the Editor. Use Editor Icons instead, or upgrade the project.";

        /// <summary>Tooltip for a node whose assigned emoji cannot be drawn here.</summary>
        public static string UnsupportedTooltip =>
            $"This item uses an emoji icon. Emoji need {MinimumVersionLabel} or newer to display.";

        /// <summary>
        /// Prefixes <paramref name="text"/> with an emoji, or returns it unchanged when
        /// emoji cannot be rendered (avoids a leading blank gap).
        /// </summary>
        public static string Prefix(string emoji, string text)
            => IsSupported && !string.IsNullOrEmpty(emoji) ? emoji + " " + text : text;

        /// <summary>Draws the standard "upgrade Unity for emoji" warning box.</summary>
        public static void DrawUnsupportedHelpBox()
            => EditorGUILayout.HelpBox(UnsupportedMessage, MessageType.Warning);
    }
}
