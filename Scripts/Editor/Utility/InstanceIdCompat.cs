using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SecretZauce.SecondBrain.Editor
{
    /// <summary>
    /// Unity 6000.5 turns <see cref="Object.GetInstanceID"/>, <see cref="EditorUtility.InstanceIDToObject"/>
    /// and the <c>SceneHandle</c>-to-<c>int</c> implicit conversion into hard compile errors, replacing
    /// them with a 64-bit <c>EntityId</c>. Every use of these ids in SecondBrain is transient,
    /// same-editor-session bookkeeping (GUI caches, drag payload checks, undo/redo handlers) — none of
    /// it is serialized across sessions or depends on the sign of the old int id — so instead of
    /// threading a new id type through the whole codebase, the low 32 bits of the EntityId are used
    /// as a drop-in replacement for the old int. That is safe as long as both sides of any comparison
    /// go through this same conversion, which is true everywhere it's used here.
    /// </summary>
    static class InstanceIdCompat
    {
        public static int GetStableInstanceId(this Object obj)
        {
#if UNITY_6000_5_OR_NEWER
            return unchecked((int)obj.GetEntityId().ToULong());
#else
            return obj.GetInstanceID();
#endif
        }

        public static Object ResolveInstanceId(int instanceId)
        {
#if UNITY_6000_5_OR_NEWER
            return EditorUtility.EntityIdToObject(EntityId.FromULong(unchecked((ulong)(uint)instanceId)));
#else
            return EditorUtility.InstanceIDToObject(instanceId);
#endif
        }

        public static int GetStableHandle(this Scene scene)
        {
#if UNITY_6000_5_OR_NEWER
            return unchecked((int)scene.handle.GetRawData());
#else
            return scene.handle;
#endif
        }
    }
}
