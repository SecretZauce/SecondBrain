using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SecretZauce.SecondBrain.Editor
{
    internal class ItemRenderer
    {
        readonly TreeView treeView;

        // One renderer instance each, reused for every row. These used to be allocated per row
        // per IMGUI pass, which on a large tree meant thousands of short-lived objects per mouse
        // move. Reuse is safe because rows render strictly one at a time: a container header
        // finishes rendering before its children are drawn, so no two rows are ever in flight.
        readonly FoldoutHeaderRenderer foldoutRenderer;
        readonly LeafRenderer leafRenderer;

        public ItemRenderer(TreeView treeView)
        {
            this.treeView = treeView;
            foldoutRenderer = new FoldoutHeaderRenderer(treeView);
            leafRenderer = new LeafRenderer(treeView);
        }

        public Color GetRowHoverColor()
        {
            return EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.06f) : new Color(0f, 0f, 0f, 0.04f);
        }

        public Rect RenderFoldoutHeader(int[] path, Object obj, bool isSelected, Texture icon, ref bool foldout, bool hideFoldoutArrow = false, Color? labelColor = null, ColorDisplayStyle colorStyle = ColorDisplayStyle.FontColor)
        {
            // Point the shared renderer at this row and render it.
            foldoutRenderer.Reset(path, obj, isSelected, icon);
            return foldoutRenderer.Render(ref foldout, hideFoldoutArrow, labelColor, colorStyle);
        }

        public bool RenderLeafContent(int[] path, Object node, Texture icon, Rect rowRect, Rect trueIndentedItemRect, bool resetIndent, Color? labelColor = null, ColorDisplayStyle colorStyle = ColorDisplayStyle.FontColor)
        {
            var renamer = treeView?.Renamer;
            leafRenderer.Reset(path, node, icon, rowRect, trueIndentedItemRect);
            return leafRenderer.Render(resetIndent, renamer, labelColor, colorStyle);
        }
    }
}
