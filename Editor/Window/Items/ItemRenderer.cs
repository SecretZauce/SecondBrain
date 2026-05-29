using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SecretZauce.SecondBrain.Editor
{
    internal class ItemRenderer
    {
        readonly TreeView treeView;

        public ItemRenderer(TreeView treeView)
        {
            this.treeView = treeView;
        }

        public Color GetRowHoverColor()
        {
            return EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.06f) : new Color(0f, 0f, 0f, 0.04f);
        }

        public Rect RenderFoldoutHeader(int[] path, Object obj, bool isSelected, Texture icon, ref bool foldout, bool hideFoldoutArrow = false, Color? labelColor = null, ColorDisplayStyle colorStyle = ColorDisplayStyle.FontColor)
        {
            // Construct a renderer pre-configured for this row and render it.
            var renderer = new FoldoutHeaderRenderer(treeView, path, obj, isSelected, icon);
            return renderer.Render(ref foldout, hideFoldoutArrow, labelColor, colorStyle);
        }

        public bool RenderLeafContent(int[] path, Object node, Texture icon, Rect rowRect, Rect trueIndentedItemRect, bool resetIndent, Color? labelColor = null, ColorDisplayStyle colorStyle = ColorDisplayStyle.FontColor)
        {
            var renamer = treeView?.Renamer;
            var leaf = new LeafRenderer(treeView, path, node, icon, rowRect, trueIndentedItemRect);
            return leaf.Render(resetIndent, renamer, labelColor, colorStyle);
        }
    }
}
