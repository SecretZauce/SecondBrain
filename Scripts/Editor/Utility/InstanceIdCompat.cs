using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SecretZauce.SecondBrain.Editor
{
    /// <summary>
    /// "Give me an int for this Object" hides two different needs, and Unity's EntityId
    /// migration makes conflating them fragile — obsolete severity for
    /// <see cref="Object.GetInstanceID"/>, <see cref="EditorUtility.InstanceIDToObject"/> and the
    /// <c>SceneHandle</c>-to-<c>int</c> conversion varies per member and per Unity version (6000.3
    /// already hard-errors on GetInstanceID and the SceneHandle conversion but only warns on
    /// InstanceIDToObject, and <c>EntityId.ToULong</c>/<c>FromULong</c>/<c>SceneHandle.GetRawData</c>
    /// don't exist yet at 6000.3 even though <c>EntityId</c> itself and
    /// <see cref="EditorUtility.EntityIdToObject"/> do).
    ///
    /// 1. A stable, session-local key for a dictionary/HashSet or an equality check.
    /// Every such use in SecondBrain is transient, same-editor-session bookkeeping — never
    /// serialized across sessions, never inverted back through Unity's own id table.
    /// <see cref="GetStableInstanceId"/> covers this everywhere via <c>Object.GetHashCode()</c>
    /// (never deprecated, and — per Unity's own migration notes — implemented the same way
    /// EntityId's equality/hashing is), plus a same-session registry for the one caller
    /// (<see cref="TryResolveStableInstanceId"/>) that wants a best-effort object back for a key
    /// it minted itself, with no need to detect staleness against Unity's own table.
    ///
    /// 2. A real round-trip through Unity's own live-object table, where a self-held reference
    /// can't substitute — walking away from that check silently defeats the safety net it exists
    /// for (drag payload validation) or the undo/redo correctness it exists for (re-embedding a
    /// recreated sub-asset). <see cref="GetStableNativeId"/>/<see cref="ResolveNativeId"/> cover
    /// those two call sites by holding the actual native id (EntityId or int, whichever the
    /// compiling Unity version has) rather than converting it to anything — sidestepping the
    /// ToULong/FromULong/GetRawData gap entirely, since only <c>Object.GetEntityId()</c> and
    /// <c>EditorUtility.EntityIdToObject(EntityId)</c> are needed, and both are already present
    /// wherever GetInstanceID is already broken.
    /// </summary>
    static class InstanceIdCompat
    {
        static readonly Dictionary<int, Object> s_Registry = new Dictionary<int, Object>();

        public static int GetStableInstanceId(this Object obj)
        {
            int id = obj.GetHashCode();
            s_Registry[id] = obj;
            return id;
        }

        /// <summary>Best-effort object for an id previously minted by <see cref="GetStableInstanceId"/>,
        /// or null if it was never registered this session (or the process reloaded).</summary>
        public static Object TryResolveStableInstanceId(int id)
        {
            return s_Registry.TryGetValue(id, out var obj) ? obj : null;
        }

        public static int GetStableHandle(this Scene scene) => scene.handle.GetHashCode();

#if UNITY_6000_3_OR_NEWER
        public static EntityId GetStableNativeId(this Object obj) => obj.GetEntityId();
        public static Object ResolveNativeId(EntityId id) => EditorUtility.EntityIdToObject(id);
#else
        public static int GetStableNativeId(this Object obj) => obj.GetInstanceID();
        public static Object ResolveNativeId(int id) => EditorUtility.InstanceIDToObject(id);
#endif
    }
}
