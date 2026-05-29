using UnityEngine;

namespace SecretZauce.SecondBrain.Editor
{
    public partial class ColorTray
    {
        static readonly (string label, Color? color)[] Palette = new (string, Color?)[]
        {
            ("Clear", null),
            ("White", new Color(1.00f, 1.00f, 1.00f)),
            ("Light Gray", new Color(0.75f, 0.75f, 0.75f)),
            ("Gray", new Color(0.55f, 0.55f, 0.55f)),
            ("Red", new Color(0.95f, 0.35f, 0.35f)),
            ("Orange", new Color(0.95f, 0.60f, 0.20f)),
            ("Yellow", new Color(0.95f, 0.90f, 0.25f)),
            ("Lime", new Color(0.50f, 0.90f, 0.30f)),
            ("Green", new Color(0.25f, 0.75f, 0.40f)),
            ("Teal", new Color(0.20f, 0.80f, 0.80f)),
            ("Cyan", new Color(0.30f, 0.85f, 0.95f)),
            ("Sky Blue", new Color(0.40f, 0.65f, 0.95f)),
            ("Blue", new Color(0.25f, 0.45f, 0.95f)),
            ("Indigo", new Color(0.45f, 0.30f, 0.90f)),
            ("Purple", new Color(0.70f, 0.30f, 0.90f)),
            ("Pink", new Color(0.95f, 0.45f, 0.75f)),
            ("Rose", new Color(0.95f, 0.30f, 0.55f)),
            ("Brown", new Color(0.65f, 0.40f, 0.20f)),
        };
    }
}