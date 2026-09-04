using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SecretZauce.SecondBrain.Editor
{
    /// <summary>
    /// Validation for objects that are about to be handed to Unity's <see cref="DragAndDrop"/>.
    ///
    /// Anything placed in <c>DragAndDrop.objectReferences</c> is serialized into the OS drag data
    /// object by <c>ApplyQueuedStartDrag</c> and read back by <c>DragAndDrop::FetchDataFromDrag</c>
    /// on the first <c>DragEnter</c>. If an entry's native object is gone — or is alive but no
    /// longer a member of the .asset file it claims to live in — Unity serializes a dangling PPtr
    /// and the read back produces garbage: an editor-killing SIGSEGV or a multi-exabyte allocation,
    /// with no managed exception in between. SecondBrain items are sub-assets that get destroyed
    /// and rebuilt by every add/delete reimport, so they routinely reach that state.
    ///
    /// Unity's <c>==</c> operator alone is not enough: it only catches a freed native object, not a
    /// wrapper whose instance ID no longer maps back, nor a sub-asset that has been unlinked from
    /// its owning file but is still held alive by the undo buffer.
    /// </summary>
    public static class DragPayloadUtils
    {
        /// <summary>
        /// True when <paramref name="o"/> can safely be written to <c>DragAndDrop.objectReferences</c>.
        /// </summary>
        public static bool IsDragSafe(Object o)
        {
            // Unity's overloaded == — destroyed native object.
            if (o == null)
                return false;

            // A wrapper left behind by a reimport (or held alive by the undo buffer after
            // Undo.DestroyObjectImmediate) no longer round-trips through the instance ID table.
            var nativeId = o.GetStableNativeId();
            if (InstanceIdCompat.ResolveNativeId(nativeId) != o)
                return false;

            // Scene objects (GameObjects resolved from SceneObjectRef) have no asset identity.
            if (!EditorUtility.IsPersistent(o))
                return true;

            if (!AssetDatabase.Contains(o))
                return false;

            // A sub-asset that has been unlinked from its owning file still answers IsPersistent,
            // but has no local file identifier left to serialize.
            return AssetDatabase.TryGetGUIDAndLocalFileIdentifier(o, out string guid, out long localId)
                   && !string.IsNullOrEmpty(guid)
                   && localId != 0;
        }

        /// <summary>True when any entry would be unsafe to serialize into a drag payload.</summary>
        public static bool AnyUnsafe(IReadOnlyList<Object> items)
        {
            if (items == null)
                return false;
            for (int i = 0; i < items.Count; i++)
                if (!IsDragSafe(items[i]))
                    return true;
            return false;
        }

        /// <summary>
        /// Returns the entries that are safe to serialize. Never returns null.
        /// <paramref name="context"/> is used only for the warning logged when items are dropped.
        /// </summary>
        public static Object[] FilterSafe(IReadOnlyList<Object> items, string context = null)
        {
            if (items == null || items.Count == 0)
                return System.Array.Empty<Object>();

            var safe = new List<Object>(items.Count);
            int dropped = 0;
            for (int i = 0; i < items.Count; i++)
            {
                if (IsDragSafe(items[i]))
                    safe.Add(items[i]);
                else
                    dropped++;
            }

            if (dropped > 0 && !string.IsNullOrEmpty(context))
            {
                Debug.LogWarning(
                    $"[SecondBrain] {context}: skipped {dropped} item(s) whose asset identity is no longer valid " +
                    "(destroyed or unlinked by a recent add/delete). They were left out of the drag payload.");
            }

            return safe.ToArray();
        }
    }
}
