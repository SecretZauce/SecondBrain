using UnityEditor;
using UnityEngine;

namespace SecretZauce.SecondBrain.Editor
{
    internal abstract class RowRendererBase
    {
        protected TreeView treeView;
        protected int[] path;
        protected Object node;
        protected Rect rowRect;
        protected Rect foldoutHeaderRect;
        protected Rect trueIndentedItemRect;
        protected Rect buttonRect;
        protected Rect arrowRect;
        protected string name;
        protected Texture icon;
        protected bool isSelected;
        protected bool showAddButton;
        protected float rightReservedSpace;
        protected float foldoutArrowWidth;

        // Cached circle texture used by DrawColorDot
        static Texture2D s_CircleTex;

        protected RowRendererBase(TreeView tv)
        {
            treeView = tv;
        }

        internal Color GetRowHoverColor()
        {
            return EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.06f) : new Color(0f, 0f, 0f, 0.04f);
        }

        protected void DrawLabelWithTempWidth(GUIContent content, GUIStyle style)
        {
            EditorGUIUtils.WithTemporaryLabelWidth(trueIndentedItemRect, 20f, () => { GUI.Label(trueIndentedItemRect, content, style); return true; });
        }

        /// <summary>
        /// Draws a filled, anti-aliased colored circle centered at (cx, cy) with the given radius.
        /// Uses a cached white circle texture tinted via GUI.color for efficiency.
        /// </summary>
        protected static void DrawColorDot(float cx, float cy, float radius, Color color)
        {
            if (Event.current == null || Event.current.type != EventType.Repaint) return;
            Texture2D tex = GetOrCreateCircleTex();
            float size = radius * 2f;
            Rect rect = new Rect(cx - radius, cy - radius, size, size);
            Color prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, tex);
            GUI.color = prev;
        }

        static Texture2D GetOrCreateCircleTex()
        {
            if (s_CircleTex != null)
                return s_CircleTex;

            const int size = 16;
            s_CircleTex = new Texture2D(size, size, TextureFormat.ARGB32, false);
            s_CircleTex.hideFlags = HideFlags.DontSave;
            float center = size / 2f;
            float radius = center - 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(center, center));
                    float a = Mathf.Clamp01(radius - dist + 0.5f);
                    s_CircleTex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            s_CircleTex.Apply();
            return s_CircleTex;
        }
    }
}