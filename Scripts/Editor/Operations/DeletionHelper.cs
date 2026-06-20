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

            // Register undo on the parent and remove the reference from its children list
            Undo.RegisterCompleteObjectUndo(parent as Object, $"Delete {nameToStore}");
            parent.RemoveChild(targetChild);
            EditorUtility.SetDirty(parent as Object);

            // Recursively destroy the target and all its descendant assets (leaf → root order)
            if (targetChild != null)
                DestroyObjectRecursive(targetChild);

            // Defer the save and Project-window refresh to the next editor frame.
            // Calling AssetDatabase.SaveAssets() immediately after Undo.DestroyObjectImmediate
            // triggers OnPostprocessAllAssets while Unity's sub-asset registry is still settling,
            // causing a NullReferenceException in Unity's own MaterialPostprocessor. Deferring
            // one frame lets the registry fully settle before the save + import run.
            SubAssetRefreshUtils.RegisterAffectedAsset(parent as Object);
            SubAssetRefreshUtils.ScheduleRefreshAffectedAssets();
        }

        /// <summary>
        /// Recursively destroys <paramref name="obj"/> and all its descendants.
        /// Sub-assets are removed via <see cref="Undo.DestroyObjectImmediate"/> (undo-safe;
        /// the asset file is updated on the next deferred save). Standalone main assets
        /// (e.g. <c>SceneObjectRef</c> / <c>SceneComponentRef</c> created as separate .asset
        /// files) are deleted from disk via <see cref="AssetDatabase.DeleteAsset"/> because
        /// <c>Undo.DestroyObjectImmediate</c> only destroys the in-memory object and never
        /// removes a main-asset file.
        /// <para>
        /// Sub-asset nodes are destroyed BEFORE their children so that Unity's undo snapshot
        /// captures valid child references. Restoring the snapshot later gives back a fully
        /// linked parent rather than one with null/missing children.
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

            if (AssetDatabase.IsMainAsset(obj))
            {
                // Standalone .asset file (e.g. SceneObjectRef saved without a parent).
                // DeleteAsset removes the file from disk immediately.
                string path = AssetDatabase.GetAssetPath(obj);
                if (!string.IsNullOrEmpty(path))
                    AssetDatabase.DeleteAsset(path);
            }
            else
            {
                // Sub-asset embedded in a parent file — destroy in memory first so the undo
                // snapshot captures valid child references, then recurse into children.
                Undo.DestroyObjectImmediate(obj);
            }

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
