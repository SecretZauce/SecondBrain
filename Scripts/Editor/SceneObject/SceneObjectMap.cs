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

        // ── Hierarchy-path fallback negative caches ───────────────────────────────────
        // s_UnresolvableIds only guards the GlobalObjectId lookup. Without these, every ref
        // whose GID does not resolve — which per FindByHierarchyPath's remarks is every prefab
        // instance once Play mode has been entered — re-ran the full path walk on every
        // repaint: a scene-GUID AssetDatabase lookup plus one or more GetRootGameObjects()
        // array allocations per row. A tree full of SceneObjectRefs paid that on every
        // mouse move.
        //
        // Entries are keyed by GlobalId, falling back to the stored hierarchy path when a ref
        // has no GlobalId. Two refs in different scenes that share a hierarchy path and have
        // no GlobalId would share an entry; the only consequence is that one of them keeps
        // showing as missing until the next scene or hierarchy change clears the cache.
        // GameObjects and Components are tracked separately so a ref pair on the same object
        // cannot collide.
        static readonly HashSet<string> s_GameObjectPathWalkFailed = new HashSet<string>(System.StringComparer.Ordinal);
        static readonly HashSet<string> s_ComponentPathWalkFailed  = new HashSet<string>(System.StringComparer.Ordinal);

        // Scene GUID → asset path. TryGetLoadedScene called AssetDatabase.GUIDToAssetPath on
        // every path walk; the mapping only changes when the project changes.
        static readonly Dictionary<string, string> s_SceneGuidToPath =
            new Dictionary<string, string>(System.StringComparer.Ordinal);

        static SceneObjectMap()
        {
            EditorSceneManager.sceneClosed += OnSceneClosed;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            // Creating, renaming, deleting or reparenting a GameObject can make a previously
            // failed path walk succeed (or vice versa), so retry everything after any change.
            EditorApplication.hierarchyChanged += ClearPathWalkCaches;
            EditorApplication.projectChanged += s_SceneGuidToPath.Clear;
            // Runtime scene loads/unloads (SceneManager.LoadScene from play-mode code) do not raise
            // the EditorSceneManager events above, so without these a ref that was unresolvable
            // while the scene was being swapped stayed blacklisted for the rest of the session.
            SceneManager.sceneLoaded += OnRuntimeSceneLoaded;
            SceneManager.sceneUnloaded += OnRuntimeSceneUnloaded;
        }

        static void OnRuntimeSceneLoaded(Scene scene, LoadSceneMode mode) => InvalidateCaches();

        static void OnRuntimeSceneUnloaded(Scene scene) => InvalidateCaches();

        /// <summary>Lets refs whose hierarchy-path walk previously failed be retried.</summary>
        static void ClearPathWalkCaches()
        {
            s_GameObjectPathWalkFailed.Clear();
            s_ComponentPathWalkFailed.Clear();
            s_NameIndexByScene.Clear();
        }

        /// <summary>
        /// Cache key for the path-walk negative caches. Returns null when a ref carries neither
        /// a GlobalId nor a stored path, in which case the walk cannot succeed anyway.
        /// Deliberately allocation-free — this runs per row per repaint.
        /// </summary>
        static string PathWalkKey(string globalId, string lastKnownPath)
        {
            if (!string.IsNullOrEmpty(globalId)) return globalId;
            if (!string.IsNullOrEmpty(lastKnownPath)) return lastKnownPath;
            return null;
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
            ClearPathWalkCaches();

            // The DontDestroyOnLoad scene is created and destroyed with each Play session,
            // so the cached handle must not survive a transition.
            s_DontDestroyOnLoadScene = default;
            s_DontDestroyOnLoadProbed = false;
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
            ClearPathWalkCaches();
        }

        static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            // A new scene loaded — previously unresolvable refs in that scene can now resolve.
            s_UnresolvableIds.Clear();
            ClearPathWalkCaches();
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
                // The scene is mid-swap or closed — the native lookup would assert and return null.
                // Not blacklisted: the scene can come back, and the path walk still gets its turn.
                if (!CanResolveWithoutAssert(gid))
                    return null;

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

        /// <summary>GlobalObjectId.identifierType for an object that lives in a scene.</summary>
        const int SceneObjectIdentifierType = 2;

        /// <summary>The GUID an unsaved scene reports — it has no asset to point at yet.</summary>
        const string EmptyGuid = "00000000000000000000000000000000";

        /// <summary>
        /// True when <see cref="GlobalObjectId.GlobalObjectIdentifierToObjectSlow"/> is safe to call
        /// for <paramref name="gid"/>.
        ///
        /// For a scene object the native implementation reaches into the owning scene's
        /// PersistentManager. While that scene is closed — including the window in which a scene is
        /// being reloaded, when the old one is already gone and the new one is not yet loaded — the
        /// manager does not exist and Unity logs
        ///
        ///     Assertion failed on expression: 'manager != NULL'
        ///
        /// once per call. Nothing breaks (the call returns null), but a tree of SceneObjectRefs
        /// makes that call per row per repaint, so the console fills up during any scene reload.
        ///
        /// The lookup could not have succeeded anyway, so it is skipped. Non-scene identifiers
        /// (assets, and the Null type) go through untouched, as do scene objects whose scene GUID is
        /// unknown — an unsaved scene has no GUID to match, and refusing those would break
        /// resolution in scenes that have never been saved.
        /// </summary>
        static bool CanResolveWithoutAssert(GlobalObjectId gid)
        {
            if (gid.identifierType != SceneObjectIdentifierType)
                return true;

            var sceneGuid = gid.assetGUID.ToString();
            if (string.IsNullOrEmpty(sceneGuid) || sceneGuid == EmptyGuid)
                return true;

            return TryGetLoadedScene(sceneGuid, null, out _) || IsOpenPrefabStage(sceneGuid);
        }

        /// <summary>
        /// True when <paramref name="sceneGuid"/> is the prefab currently open in Prefab Mode.
        ///
        /// A prefab stage owns a preview scene that SceneManager does not enumerate, so the loaded
        /// scene check above cannot see it. Without this, every ref pointing into an open prefab
        /// stage would be treated as "scene closed" and skipped.
        /// </summary>
        static bool IsOpenPrefabStage(string sceneGuid)
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null || string.IsNullOrEmpty(stage.assetPath))
                return false;

            return AssetDatabase.AssetPathToGUID(stage.assetPath) == sceneGuid;
        }

        // ── Resolution with hierarchy-path fallback ───────────────────────────────────

        /// <summary>
        /// Resolves the GameObject for <paramref name="sceneObject"/>, falling back to its stored
        /// hierarchy path when the GlobalObjectId cannot be resolved.
        /// </summary>
        public static GameObject Resolve(SceneObject sceneObject)
        {
            if (sceneObject == null) return null;

            return Resolve(sceneObject.GlobalId, sceneObject.LastKnownPath,
                sceneObject.LastKnownSceneGuid, sceneObject.LastKnownScene);
        }

        /// <summary>
        /// Resolves a GameObject from its stored metadata, falling back to the hierarchy-path walk
        /// when the GlobalObjectId cannot be resolved. Use this from drawers, which hold the
        /// metadata as SerializedProperties rather than a <see cref="SceneObject"/> instance.
        /// </summary>
        public static GameObject Resolve(string globalId, string lastKnownPath,
            string lastKnownSceneGuid, string lastKnownScene)
        {
            var byId = Resolve(globalId);
            if (byId != null) return byId;

            // Skip the path walk for refs already known to be unreachable this way.
            var key = PathWalkKey(globalId, lastKnownPath);
            if (key != null && s_GameObjectPathWalkFailed.Contains(key))
                return null;

            var byPath = FindByHierarchyPath(lastKnownPath, lastKnownSceneGuid, lastKnownScene);

            if (byPath == null)
            {
                if (key != null) s_GameObjectPathWalkFailed.Add(key);
                return null;
            }

            if (!string.IsNullOrEmpty(globalId))
            {
                // Re-point the cache at the live object and lift the blacklist entry.
                SceneObjects[globalId] = byPath;
                s_UnresolvableIds.Remove(globalId);
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

            return ResolveComponent(sceneComponent.GlobalId, sceneComponent.LastKnownPath,
                sceneComponent.LastKnownSceneGuid, sceneComponent.LastKnownScene,
                sceneComponent.LastKnownComponentType);
        }

        /// <summary>
        /// Resolves a Component from its stored metadata, falling back to the hierarchy-path walk
        /// plus component-type match when the GlobalObjectId cannot be resolved. Use this from
        /// drawers, which hold the metadata as SerializedProperties.
        /// </summary>
        public static Component ResolveComponent(string globalId, string lastKnownPath,
            string lastKnownSceneGuid, string lastKnownScene, string componentTypeName)
        {
            var byId = ResolveComponent(globalId);
            if (byId != null) return byId;

            // Skip the path walk for refs already known to be unreachable this way.
            var key = PathWalkKey(globalId, lastKnownPath);
            if (key != null && s_ComponentPathWalkFailed.Contains(key))
                return null;

            Component match = null;
            var owner = string.IsNullOrEmpty(componentTypeName)
                ? null
                : FindByHierarchyPath(lastKnownPath, lastKnownSceneGuid, lastKnownScene);

            if (owner != null)
            {
                foreach (var component in owner.GetComponents<Component>())
                {
                    if (component == null) continue;
                    if (!ComponentTypeMatches(component.GetType(), componentTypeName)) continue;
                    match = component;
                    break;
                }
            }

            if (match == null)
            {
                if (key != null) s_ComponentPathWalkFailed.Add(key);
                return null;
            }

            if (!string.IsNullOrEmpty(globalId))
            {
                SceneObjects[globalId] = match;
                s_UnresolvableIds.Remove(globalId);
            }

            return match;
        }

        /// <summary>
        /// True when <paramref name="type"/> is the component type recorded in
        /// <paramref name="componentTypeName"/>.
        ///
        /// SceneComponentRefUtils records the type as an AssemblyQualifiedName, but this comparison
        /// originally only accepted FullName or Name, so a stored AssemblyQualifiedName never matched
        /// anything. The consequence was that the hierarchy-path fallback could never produce a
        /// component: once a GlobalObjectId stopped resolving — which is every prefab instance after
        /// Play mode is entered — the ref reported "(Missing)" for the whole session, while the
        /// equivalent SceneObjectRef recovered because it has no type check to fail.
        ///
        /// All three spellings are accepted because older refs and the drawers' fallbacks may hold a
        /// FullName or a bare type name instead.
        /// </summary>
        static bool ComponentTypeMatches(System.Type type, string componentTypeName)
        {
            if (type == null || string.IsNullOrEmpty(componentTypeName))
                return false;

            return type.AssemblyQualifiedName == componentTypeName
                || type.FullName == componentTypeName
                || type.Name == componentTypeName;
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
        ///
        /// When the object is not in its original scene, the DontDestroyOnLoad scene is searched as
        /// a last resort — see <see cref="FindInDontDestroyOnLoad"/>. When it is still in its scene
        /// but no longer at the recorded path, the relaxed walk takes over — see
        /// <see cref="FindByFlattenedPath"/>.
        /// </summary>
        static GameObject FindByHierarchyPath(string hierarchyPath, string sceneGuid, string sceneName)
        {
            if (string.IsNullOrEmpty(hierarchyPath))
                return null;

            var segments = hierarchyPath.Split('/');
            if (segments.Length == 0)
                return null;

            bool sceneLoaded = TryGetLoadedScene(sceneGuid, sceneName, out var scene);
            if (sceneLoaded)
            {
                var inOriginScene = FindInScene(scene, segments, 0);
                if (inOriginScene != null)
                    return inOriginScene;
            }

            var inDontDestroyOnLoad = FindInDontDestroyOnLoad(segments);
            if (inDontDestroyOnLoad != null)
                return inDontDestroyOnLoad;

            // Exact walks are exhausted — try the relaxed one, which tolerates ancestors that no
            // longer exist. Origin scene first; an object that was also carried into
            // DontDestroyOnLoad is the rarer case.
            if (sceneLoaded)
            {
                var flattened = FindByFlattenedPath(scene, segments);
                if (flattened != null)
                    return flattened;
            }

            return TryGetDontDestroyOnLoadScene(out var ddolScene)
                ? FindByFlattenedPath(ddolScene, segments)
                : null;
        }

        // ── Flattened hierarchies ─────────────────────────────────────────────────────

        /// <summary>
        /// Finds the linked object when some of its ancestors no longer exist, by matching the
        /// recorded path loosely: the live ancestor chain must appear in the recorded path in the
        /// same order, but the recorded path may contain extra ancestors the live object no longer
        /// has.
        ///
        /// This covers third-party hierarchy-folder assets, which organise the Edit-mode hierarchy
        /// under folder GameObjects and then flatten themselves on entering Play mode: each folder
        /// is destroyed and its children are reparented up. The linked object survives, but its path
        /// is now the recorded one minus the folder segments — at any depth, not just the leading
        /// ones — so the exact walk misses it and the row read "(Missing)" for the whole session.
        ///
        /// Ambiguity is treated as failure: if two objects fit the recorded path equally well, one
        /// is not preferred over the other, because silently pointing a ref at the wrong object is
        /// worse than reporting it missing.
        /// </summary>
        static GameObject FindByFlattenedPath(Scene scene, string[] segments)
        {
            if (!scene.IsValid() || !scene.isLoaded || segments.Length == 0)
                return null;

            if (!GetNameIndex(scene).TryGetValue(segments[segments.Length - 1], out var candidates))
                return null;

            GameObject best = null;
            int bestScore = -1;
            bool tied = false;

            foreach (var candidate in candidates)
            {
                if (candidate == null) continue;

                int score = MatchAncestors(candidate.transform, segments);
                if (score < 0) continue;

                if (score > bestScore)
                {
                    best = candidate;
                    bestScore = score;
                    tied = false;
                }
                else if (score == bestScore)
                {
                    tied = true;
                }
            }

            return tied ? null : best;
        }

        /// <summary>
        /// Scores how well <paramref name="leaf"/>'s live ancestor chain fits the ancestor part of
        /// <paramref name="segments"/> (everything but the last segment, which is the leaf name and
        /// is matched by the caller).
        ///
        /// Returns the number of live ancestors matched, or -1 when the chain is not an ordered
        /// subsequence of the recorded one. Only removals are tolerated: every live ancestor must
        /// still be named in the recorded path, so an unrelated object that merely shares the leaf
        /// name is rejected rather than scored low.
        ///
        /// The score is the live depth, so a deeper match — one that kept more of the recorded
        /// ancestry — wins over a shallower one.
        /// </summary>
        static int MatchAncestors(Transform leaf, string[] segments)
        {
            int matched = 0;
            // Recorded ancestors are consumed from the deepest backwards, mirroring the walk up the
            // live chain. -1 when the recorded path is a bare name with no ancestors at all.
            int next = segments.Length - 2;

            for (var ancestor = leaf.parent; ancestor != null; ancestor = ancestor.parent)
            {
                while (next >= 0 && segments[next] != ancestor.name)
                    next--;

                if (next < 0)
                    return -1;

                matched++;
                next--;
            }

            return matched;
        }

        /// <summary>
        /// Name → GameObjects for one loaded scene, built on demand and dropped along with the
        /// path-walk caches.
        ///
        /// <see cref="FindByFlattenedPath"/> has to start from the leaf name alone, and a fresh
        /// scan per ref would walk the entire scene once for every row that failed the exact walk.
        /// The index moves that to once per scene per hierarchy change.
        /// </summary>
        static readonly Dictionary<int, Dictionary<string, List<GameObject>>> s_NameIndexByScene =
            new Dictionary<int, Dictionary<string, List<GameObject>>>();

        static Dictionary<string, List<GameObject>> GetNameIndex(Scene scene)
        {
            if (s_NameIndexByScene.TryGetValue(scene.handle, out var index))
                return index;

            index = new Dictionary<string, List<GameObject>>(System.StringComparer.Ordinal);
            foreach (var root in scene.GetRootGameObjects())
                IndexRecursive(root.transform, index);

            s_NameIndexByScene[scene.handle] = index;
            return index;
        }

        static void IndexRecursive(Transform transform, Dictionary<string, List<GameObject>> index)
        {
            if (!index.TryGetValue(transform.name, out var bucket))
            {
                bucket = new List<GameObject>();
                index[transform.name] = bucket;
            }
            bucket.Add(transform.gameObject);

            for (int i = 0; i < transform.childCount; i++)
                IndexRecursive(transform.GetChild(i), index);
        }

        /// <summary>Walks <paramref name="segments"/> from <paramref name="startIndex"/> inside
        /// <paramref name="scene"/>, matching each segment by name. Returns null if any hop misses.</summary>
        static GameObject FindInScene(Scene scene, string[] segments, int startIndex)
        {
            if (!scene.IsValid() || !scene.isLoaded || startIndex >= segments.Length)
                return null;

            GameObject current = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name != segments[startIndex]) continue;
                current = root;
                break;
            }

            for (int i = startIndex + 1; i < segments.Length && current != null; i++)
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

        // ── DontDestroyOnLoad ─────────────────────────────────────────────────────────

        /// <summary>
        /// Searches the DontDestroyOnLoad scene for the linked object.
        ///
        /// An object moved there at runtime leaves its original scene entirely: its GlobalObjectId
        /// no longer resolves, and the path walk above searches a scene the object is no longer in,
        /// so the ref was reported as "(Missing)" for the whole Play session.
        ///
        /// The stored path is tried in full first (the object was carried along as a child of a
        /// persistent root, so its subtree path is intact), then with leading segments dropped one
        /// at a time. The suffix walk is needed because DontDestroyOnLoad only accepts root objects:
        /// a linked child is typically detached from its parents before the call and ends up as a
        /// root whose path is a suffix of the one recorded in Edit mode.
        /// </summary>
        static GameObject FindInDontDestroyOnLoad(string[] segments)
        {
            if (!TryGetDontDestroyOnLoadScene(out var ddolScene))
                return null;

            for (int start = 0; start < segments.Length; start++)
            {
                var found = FindInScene(ddolScene, segments, start);
                if (found != null)
                    return found;
            }

            return null;
        }

        // Handle of the current Play session's DontDestroyOnLoad scene. Reset by InvalidateCaches.
        static Scene s_DontDestroyOnLoadScene;
        static bool s_DontDestroyOnLoadProbed;

        /// <summary>
        /// Returns the DontDestroyOnLoad scene, which exists only while playing.
        ///
        /// Unity exposes no API for it and SceneManager.GetSceneAt does not enumerate it, so the
        /// handle is discovered by moving a throwaway GameObject into it and reading back its scene.
        /// The probe runs at most once per Play session; the handle stays valid for its duration.
        /// </summary>
        static bool TryGetDontDestroyOnLoadScene(out Scene scene)
        {
            scene = default;

            if (!EditorApplication.isPlaying)
                return false;

            if (!s_DontDestroyOnLoadProbed)
            {
                s_DontDestroyOnLoadProbed = true;

                var probe = new GameObject("SecondBrain_DontDestroyOnLoadProbe")
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                try
                {
                    Object.DontDestroyOnLoad(probe);
                    s_DontDestroyOnLoadScene = probe.scene;
                }
                finally
                {
                    Object.DestroyImmediate(probe);
                }
            }

            if (!s_DontDestroyOnLoadScene.IsValid() || !s_DontDestroyOnLoadScene.isLoaded)
                return false;

            scene = s_DontDestroyOnLoadScene;
            return true;
        }

        /// <summary>Scene GUID → asset path, cached until the project changes.</summary>
        static string GetScenePathCached(string sceneGuid)
        {
            if (s_SceneGuidToPath.TryGetValue(sceneGuid, out var path))
                return path;

            path = AssetDatabase.GUIDToAssetPath(sceneGuid);
            s_SceneGuidToPath[sceneGuid] = path;
            return path;
        }

        static bool TryGetLoadedScene(string sceneGuid, string sceneName, out Scene scene)
        {
            scene = default;

            string scenePath = string.IsNullOrEmpty(sceneGuid)
                ? null
                : GetScenePathCached(sceneGuid);

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
