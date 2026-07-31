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

        // GIDs that returned null from GlobalObjectIdentifierToObjectSlow — scene not loaded.
        // Cleared whenever any scene opens or play mode transitions so refs become resolvable again.
        static readonly HashSet<string> s_UnresolvableIds = new HashSet<string>();

        static SceneObjectMap()
        {
            EditorSceneManager.sceneClosed += OnSceneClosed;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            // EditorSceneManager events do NOT fire during Play Mode transitions; Unity reloads
            // scenes through the runtime SceneManager when entering/exiting Play Mode.  We must
            // subscribe to playModeStateChanged and clear both caches at the Exiting* transitions
            // so that stale Object references and "unresolvable" GID entries from the reload
            // window do not persist and cause refs to show as Missing after the scene settles.
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        static void OnSceneClosed(Scene scene)
        {
            var dead = new List<string>();
            foreach (var kvp in SceneObjects)
                if (kvp.Value == null) dead.Add(kvp.Key);
            foreach (var key in dead)
                SceneObjects.Remove(key);
            // Refs that were unresolvable may now resolve (or stay unresolvable) — re-check next access.
            s_UnresolvableIds.Clear();
        }

        static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            // A new scene loaded — previously unresolvable refs in that scene can now resolve.
            s_UnresolvableIds.Clear();
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            // At both Exiting transitions the current scene context is invalidated:
            //   ExitingEditMode  — edit-mode objects are about to be destroyed/reloaded as play-mode clones.
            //   ExitingPlayMode  — play-mode clone objects are being destroyed; edit-mode scene restores next.
            // Clearing both caches here ensures that the first Resolve() call after the new scene
            // context is ready goes through GlobalObjectIdentifierToObjectSlow rather than returning
            // a stale null from the reload window, and that previously unresolvable GIDs are retried.
            if (state == PlayModeStateChange.ExitingEditMode ||
                state == PlayModeStateChange.ExitingPlayMode)
            {
                SceneObjects.Clear();
                s_UnresolvableIds.Clear();
            }
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

            // Skip the expensive slow lookup for GIDs already known to be unresolvable
            // (scene not loaded). Cleared on any scene open/close so staleness is bounded.
            if (s_UnresolvableIds.Contains(globalId))
                return null;

            if (GlobalObjectId.TryParse(globalId, out var gid))
            {
                obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gid) as Object;
                if (obj != null)
                {
                    SceneObjects[globalId] = obj;
                    return obj;
                }
            }

            s_UnresolvableIds.Add(globalId);
            return null;
        }
    }
}
#endif
