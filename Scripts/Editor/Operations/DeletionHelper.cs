using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using Object = UnityEngine.Object;

namespace SecretZauce.SecondBrain.Editor
{
    /// <summary>
    /// Helper class to handle deletion of collections, groups, and configs in the Browser.
    /// </summary>
    public class DeletionHelper
    {
        readonly BrowserWindow window;
        IStructure Root => window?.Root;

        public DeletionHelper(BrowserWindow window)
        {
            this.window = window;
        }

        /// <summary>
        /// Deletes the selected item (collection, group, or config) and records for undo.
        /// Recursively destroys the target asset and all descendant assets so nothing
        /// is left orphaned on disk.
        /// </summary>
        public void DeleteItem(int[] path)
        {
            if (path == null || path.Length == 0) 
                return;

            // Traverse hierarchy using IStructure
            IStructure parent = null;
            IStructure current = null;
            int lastIndex = -1;
            for (int i = 0; i < path.Length; i++)
            {
                if (i == 0)
                {
                    // Top-level collection
                    var collections = Root.ChildrenObjects;
                    if (collections == null || path[0] < 0 || path[0] >= collections.Count) return;
                    current = collections[path[0]] as IStructure;
                    lastIndex = path[0];
                    parent = null;
                }
                else
                {
                    parent = current;
                    // Use path[i] not i to index into children
                    var currentChildren = parent?.ChildrenObjects;
                    if (currentChildren == null || path[i] < 0 || path[i] >= currentChildren.Count) return;
                    current = currentChildren[path[i]] as IStructure;
                    lastIndex = path[i];
                }
            }

            if (parent == null)
                parent = Root;

            var parentChildren = parent.ChildrenObjects;
            var targetChild = parentChildren[lastIndex];
            string nameToStore = targetChild?.name;

            // Register undo on the parent and remove the reference from its children list
            Undo.RegisterCompleteObjectUndo(parent as Object, $"Delete {nameToStore}");
            parent.RemoveChild(targetChild);
            EditorUtility.SetDirty(parent as Object);

            // The asset file that owns all the sub-assets we are allowed to destroy.
            // Sub-assets embedded in any other file must only be unlinked, not destroyed.
            string ownerAssetPath = AssetDatabase.GetAssetPath(parent as Object);

            // Recursively destroy the target and all its descendant assets (leaf → root order)
            if (targetChild != null)
                DestroyObjectRecursive(targetChild, ownerAssetPath);

            // Defer the save and Project-window refresh to the next editor frame.
            // Calling AssetDatabase.SaveAssets() immediately after Undo.DestroyObjectImmediate
            // triggers OnPostprocessAllAssets while Unity's sub-asset registry is still settling,
            // causing a NullReferenceException in Unity's own MaterialPostprocessor. Deferring
            // one frame lets the registry fully settle before the save + import run.
            SubAssetRefreshUtils.RegisterAffectedAsset(parent as Object);
            SubAssetRefreshUtils.ScheduleRefreshAffectedAssets();
        }

        /// <summary>
        /// Recursively destroys sub-asset descendants of <paramref name="obj"/> that live
        /// inside <paramref name="ownerAssetPath"/>.
        /// <para>
        /// Only objects that are sub-assets of the Profile file at
        /// <paramref name="ownerAssetPath"/> are destroyed via
        /// <see cref="Undo.DestroyObjectImmediate"/> so the deferred save removes them from
        /// the .asset file. The removal is undo-safe: restoring the parent's undo snapshot
        /// brings back a fully linked parent with valid child references.
        /// </para>
        /// <para>
        /// Standalone main assets, GameObjects, and sub-assets that belong to a different
        /// .asset file are left untouched — only the reference in the parent's children list
        /// is removed (captured in the parent's undo snapshot).
        /// </para>
        /// </summary>
        static void DestroyObjectRecursive(Object obj, string ownerAssetPath)
        {
            if (obj == null) return;

            // Leave GameObjects and standalone asset files untouched — just unlink them.
            if (obj is UnityEngine.GameObject || AssetDatabase.IsMainAsset(obj))
                return;

            // Leave sub-assets that belong to a different .asset file untouched.
            // They may be referenced by other structures we must not corrupt.
            if (!string.Equals(AssetDatabase.GetAssetPath(obj), ownerAssetPath,
                    System.StringComparison.OrdinalIgnoreCase))
                return;

            // Snapshot children BEFORE destroying this node so we can reach them after
            // the parent's native object is gone.
            List<Object> children = null;
            if (obj is IStructure structure)
                children = structure.ChildrenObjects?.ToList();

            // Destroy this sub-asset first while children are still live so the undo
            // snapshot captures valid child references for correct restoration on undo.
            Undo.DestroyObjectImmediate(obj);

            if (children != null)
            {
                foreach (var child in children)
                {
                    if (child != null)
                        DestroyObjectRecursive(child, ownerAssetPath);
                }
            }
        }
    }
}
