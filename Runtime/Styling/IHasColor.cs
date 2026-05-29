using UnityEngine;

namespace SecretZauce.SecondBrain
{
    /// <summary>
    /// Defines how a container's label color is displayed.
    /// </summary>
    public enum ColorDisplayStyle
    {
        Gradient,
        FontColor,
        CircleDot,
        Background,
    }

    /// <summary>
    /// Interface for objects that can have a custom label color assigned.
    /// </summary>
    public interface IHasColor
    {
        bool HasLabelColor { get; set; }
        Color LabelColor { get; set; }
        ColorDisplayStyle LabelColorStyle { get; set; }

        /// <summary>When true, the color only applies to the container row; children are not colored.</summary>
        bool LabelColorFoldoutOnly { get; set; }
    }
}
