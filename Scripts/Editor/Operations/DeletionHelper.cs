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
        readonly UndoHelper undoHelper;
        readonly BrowserWindow window;
        IStructure Root => window?.Root;

        public DeletionHelper(UndoHelper undoHelper, BrowserWindow window)
        {
            this.undoHelper = undoHelper;
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

            // Capture the parent's asset path BEFORE destruction for targeted Project-window refresh.
            // The parent is NOT destroyed, so this is safe to call before or after, but doing it
            // before avoids any potential null-ref if the asset is later unloaded.
            string parentAssetPath = AssetDatabase.GetAssetPath(parent as Object);

            // Register undo on the parent and remove the reference from its children list
            Undo.RegisterCompleteObjectUndo(parent as Object, $"Delete {nameToStore}");
            parent.RemoveChild(targetChild);
            EditorUtility.SetDirty(parent as Object);

            // Recursively destroy the target and all its descendant assets (leaf → root order)
            if (targetChild != null)
                DestroyObjectRecursive(targetChild);

            AssetDatabase.SaveAssets();

            // Targeted Project-window refresh: reimport only the parent asset that lost a child.
            // This is much cheaper than the global AssetDatabase.Refresh() and updates the
            // Project window immediately without requiring a manual save.
            SubAssetRefreshUtils.ImportAndRegister(parentAssetPath);
        }

        /// <summary>
        /// Recursively destroys <paramref name="obj"/> and all its descendants using
        /// <see cref="Undo.DestroyObjectImmediate"/> so the operation is undo-safe.
        /// <para>
        /// The parent is destroyed FIRST (while its children are still live) so that
        /// Unity's undo snapshot captures valid child references in the parent's children
        /// list. If children were destroyed first, their references in the parent's list
        /// would already be null/missing when the parent is captured, causing the restored
        /// parent to have broken (null) children after undo.
        /// </para>
        /// </summary>
        static void DestroyObjectRecursive(Object obj)
        {
            if (obj == null) return;

            // GameObjects from Prefab Assets cannot be destroyed via Undo.DestroyObjectImmediate.
            // The reference was already removed from the parent's children list by RemoveChild,
            // so we just leave the prefab asset on disk untouched.
            if (obj is UnityEngine.GameObject)
                return;

            // Snapshot children BEFORE destroying anything so we can reach them after
            // the parent's native object is gone.
            List<Object> children = null;
            if (obj is IStructure structure)
                children = structure.ChildrenObjects?.ToList();

            // Destroy the parent first — children are still live, so their references
            // are valid inside the undo snapshot Unity captures here.
            Undo.DestroyObjectImmediate(obj);

            // Now destroy each child. They are still alive (snapshotted above) and their
            // own undo snapshots will also capture valid descendant references.
            if (children != null)
            {
                foreach (var child in children)
                {
                    if (child != null)
                        DestroyObjectRecursive(child);
                }
            }
        }
    }
}
