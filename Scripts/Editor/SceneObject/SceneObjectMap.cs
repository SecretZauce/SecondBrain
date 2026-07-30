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
        // Cleared whenever any scene opens so refs become resolvable again.
        static readonly HashSet<string> s_UnresolvableIds = new HashSet<string>();

        static SceneObjectMap()
        {
            EditorSceneManager.sceneClosed += OnSceneClosed;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        /// <summary>
        /// Entering or leaving Play mode rebuilds the scene: every edit-mode instance is destroyed
        /// and replaced by a fresh runtime copy with new instance IDs.
        ///
        /// This is NOT covered by sceneOpened/sceneClosed — those are EditorSceneManager events and
        /// the Play mode swap does not raise them. Without this hook the caches survived the
        /// transition: cached objects went stale, and — worse — any GID that failed to resolve
        /// during the switch stayed in s_UnresolvableIds for the rest of the session, so the item
        /// rendered as "(Missing)" until a scene was manually opened or closed.
        /// </summary>
        static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            InvalidateCaches();
        }

        /// <summary>Drops destroyed entries and lets previously unresolvable GIDs be retried.</summary>
        static void InvalidateCaches()
        {
            var dead = new List<string>();
            foreach (var kvp in SceneObjects)
                if (kvp.Value == null) dead.Add(kvp.Key);
            foreach (var key in dead)
                SceneObjects.Remove(key);

            s_UnresolvableIds.Clear();
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

        // ── Resolution with hierarchy-path fallback ───────────────────────────────────

        /// <summary>
        /// Resolves the GameObject for <paramref name="sceneObject"/>, falling back to its stored
        /// hierarchy path when the GlobalObjectId cannot be resolved.
        /// </summary>
        public static GameObject Resolve(SceneObject sceneObject)
        {
            if (sceneObject == null) return null;

            var byId = Resolve(sceneObject.GlobalId);
            if (byId != null) return byId;

            var byPath = FindByHierarchyPath(
                sceneObject.LastKnownPath, sceneObject.LastKnownSceneGuid, sceneObject.LastKnownScene);

            if (byPath != null && !string.IsNullOrEmpty(sceneObject.GlobalId))
            {
                // Re-point the cache at the live object and lift the blacklist entry.
                SceneObjects[sceneObject.GlobalId] = byPath;
                s_UnresolvableIds.Remove(sceneObject.GlobalId);
            }

            return byPath;
        }

        /// <summary>
        /// Resolves the Component for <paramref name="sceneComponent"/>, falling back to its stored
        /// hierarchy path plus component type when the GlobalObjectId cannot be resolved.
        /// </summary>
        public static Component Resolve(SceneComponent sceneComponent)
        {
            if (sceneComponent == null) return null;

            var byId = ResolveComponent(sceneComponent.GlobalId);
            if (byId != null) return byId;

            var owner = FindByHierarchyPath(
                sceneComponent.LastKnownPath, sceneComponent.LastKnownSceneGuid, sceneComponent.LastKnownScene);
            if (owner == null) return null;

            var typeName = sceneComponent.LastKnownComponentType;
            if (string.IsNullOrEmpty(typeName)) return null;

            Component match = null;
            foreach (var component in owner.GetComponents<Component>())
            {
                if (component == null) continue;
                var type = component.GetType();
                if (type.FullName != typeName && type.Name != typeName) continue;
                match = component;
                break;
            }

            if (match != null && !string.IsNullOrEmpty(sceneComponent.GlobalId))
            {
                SceneObjects[sceneComponent.GlobalId] = match;
                s_UnresolvableIds.Remove(sceneComponent.GlobalId);
            }

            return match;
        }

        /// <summary>
        /// Walks a "Root/Child/Grandchild" path inside the loaded scene identified by
        /// <paramref name="sceneGuid"/> (falling back to <paramref name="sceneName"/>).
        ///
        /// This exists because GlobalObjectId resolution is not reliable for prefab instances once
        /// Play mode has been entered: the identifier of an instance carries a prefab reference,
        /// and GlobalObjectIdentifierToObjectSlow does not map it back to the runtime copy of the
        /// scene, so it returns null and the item is reported as missing. Plain (non-prefab) scene
        /// objects are unaffected, which is why only prefab instances showed the problem.
        ///
        /// Walks the hierarchy manually rather than using GameObject.Find so that inactive objects
        /// are still found — GameObject.Find skips them.
        /// </summary>
        static GameObject FindByHierarchyPath(string hierarchyPath, string sceneGuid, string sceneName)
        {
            if (string.IsNullOrEmpty(hierarchyPath))
                return null;

            if (!TryGetLoadedScene(sceneGuid, sceneName, out var scene))
                return null;

            var segments = hierarchyPath.Split('/');
            if (segments.Length == 0)
                return null;

            GameObject current = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name != segments[0]) continue;
                current = root;
                break;
            }

            for (int i = 1; i < segments.Length && current != null; i++)
            {
                GameObject next = null;
                var parent = current.transform;
                for (int c = 0; c < parent.childCount; c++)
                {
                    var child = parent.GetChild(c);
                    if (child.name != segments[i]) continue;
                    next = child.gameObject;
                    break;
                }
                current = next;
            }

            return current;
        }

        static bool TryGetLoadedScene(string sceneGuid, string sceneName, out Scene scene)
        {
            scene = default;

            string scenePath = string.IsNullOrEmpty(sceneGuid)
                ? null
                : AssetDatabase.GUIDToAssetPath(sceneGuid);

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var candidate = SceneManager.GetSceneAt(i);
                if (!candidate.isLoaded) continue;

                bool matches = !string.IsNullOrEmpty(scenePath)
                    ? candidate.path == scenePath
                    : !string.IsNullOrEmpty(sceneName) && candidate.name == sceneName;

                if (!matches) continue;

                scene = candidate;
                return true;
            }

            return false;
        }
    }
}
#endif
