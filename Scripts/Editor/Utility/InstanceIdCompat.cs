using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SecretZauce.SecondBrain.Editor
{
    /// <summary>
    /// "Give me an int for this Object" hides two different needs, and Unity's EntityId
    /// migration makes conflating them fragile — obsolete severity for
    /// <see cref="Object.GetInstanceID"/> and <see cref="EditorUtility.InstanceIDToObject"/>
    /// varies per member and per Unity version (6000.3 already hard-errors on GetInstanceID but
    /// only warns on InstanceIDToObject, and <c>EntityId.ToULong</c>/<c>FromULong</c> don't exist
    /// yet at 6000.3 even though <c>EntityId</c> itself and
    /// <see cref="EditorUtility.EntityIdToObject"/> do).
    ///
    /// 1. A key for a dictionary/HashSet or an equality check. Don't mint an id for this —
    /// key the collection on the <see cref="Object"/> itself. Unity's <c>Object.Equals</c>
    /// compares native ids, so it is exact, whereas an int derived from
    /// <c>GetHashCode()</c> stops being unique once EntityId outgrows 32 bits (Unity's migration
    /// guide lists hash codes as a thing not to use as identifiers).
    ///
    /// 2. An int that has to survive as text — <see cref="GetSessionKey"/>, used only by the
    /// <c>"i:"</c> fallback of <see cref="AssetUtils.TryGetPersistentKeyForObject"/> for objects
    /// with no asset identity. Best effort by design: <see cref="TryResolveSessionKey"/> returns
    /// null after a domain reload, and a hash collision can resolve the wrong object, so nothing
    /// that must be correct may rely on it.
    ///
    /// 3. A real round-trip through Unity's own live-object table, where a self-held reference
    /// can't substitute — walking away from that check silently defeats the safety net it exists
    /// for (drag payload validation) or the undo/redo correctness it exists for (re-embedding a
    /// recreated sub-asset), and Unity's internal ProjectBrowser API takes the native id as-is.
    /// <see cref="GetStableNativeId"/>/<see cref="ResolveNativeId"/> hold the actual native id
    /// (EntityId or int, whichever the compiling Unity version has) rather than converting it to
    /// anything — sidestepping the ToULong/FromULong gap entirely, since only
    /// <c>Object.GetEntityId()</c> and <c>EditorUtility.EntityIdToObject(EntityId)</c> are
    /// needed, and both are already present wherever GetInstanceID is already broken.
    /// </summary>
    static class InstanceIdCompat
    {
        static readonly Dictionary<int, Object> s_SessionKeys = new Dictionary<int, Object>();

        /// <summary>Session-local int key for <paramref name="obj"/>, resolvable through
        /// <see cref="TryResolveSessionKey"/> until the next domain reload. Registers the object,
        /// so call it only where the key is actually stored — never on a draw path.</summary>
        public static int GetSessionKey(this Object obj)
        {
            int key = obj.GetHashCode();
            s_SessionKeys[key] = obj;
            return key;
        }

        /// <summary>Best-effort object for a key previously minted by <see cref="GetSessionKey"/>,
        /// or null if it was never registered this session (or the process reloaded).</summary>
        public static Object TryResolveSessionKey(int key)
        {
            return s_SessionKeys.TryGetValue(key, out var obj) ? obj : null;
        }

#if UNITY_6000_3_OR_NEWER
        public static EntityId GetStableNativeId(this Object obj) => obj.GetEntityId();
        public static Object ResolveNativeId(EntityId id) => EditorUtility.EntityIdToObject(id);
#else
        public static int GetStableNativeId(this Object obj) => obj.GetInstanceID();
        public static Object ResolveNativeId(int id) => EditorUtility.InstanceIDToObject(id);
#endif
    }
}
