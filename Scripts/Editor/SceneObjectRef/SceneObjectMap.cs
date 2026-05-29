#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SecretZauce.SecondBrain.Editor
{
    public static class SceneObjectMap
    {
        static readonly Dictionary<string, Object> SceneObjects = new Dictionary<string, Object>();

        static SceneObjectMap()
        {
            // Prune destroyed scene objects whenever a scene is unloaded so the static
            // cache does not accumulate stale entries across scene load/unload cycles.
            EditorSceneManager.sceneClosed += OnSceneClosed;
        }

        static void OnSceneClosed(Scene scene)
        {
            var dead = new List<string>();
            foreach (var kvp in SceneObjects)
                if (kvp.Value == null) dead.Add(kvp.Key);
            foreach (var key in dead)
                SceneObjects.Remove(key);
        }

        public static GameObject Resolve(string globalId)
        {
            return ResolveObject(globalId) as GameObject;
        }

        public static Component ResolveComponent(string globalId)
        {
            return ResolveObject(globalId) as Component;
        }

        public static Object ResolveObject(string globalId)
        {
            if (string.IsNullOrEmpty(globalId))
                return null;

            if (SceneObjects.TryGetValue(globalId, out var obj) && obj != null)
                return obj;

            if (GlobalObjectId.TryParse(globalId, out var gid))
            {
                obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gid) as Object;
                if (obj != null)
                {
                    SceneObjects[globalId] = obj;
                    return obj;
                }
            }
            return null;
        }
    }
}
#endif
