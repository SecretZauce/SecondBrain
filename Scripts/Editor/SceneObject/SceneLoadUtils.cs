#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace SecretZauce.SecondBrain.Editor
{
    /// <summary>
    /// Answers "is the scene this ref points at currently loaded?" for every caller that gates UI on
    /// it — the tree rows, the leaf '>' action and QuickPeek's property rows.
    ///
    /// This existed as three near-identical private copies, which is how they drifted: only the tree
    /// row knew about the DontDestroyOnLoad pseudo-scene, so the same ref could render as loaded and
    /// still be refused by the action next to it.
    /// </summary>
    public static class SceneLoadUtils
    {
        /// <summary>Name Unity gives the runtime-only scene that holds DontDestroyOnLoad objects.</summary>
        public const string DontDestroyOnLoadSceneName = "DontDestroyOnLoad";

        // sceneGuid → isLoaded. Cleared whenever any scene opens or closes, and on every Play mode
        // transition — EditorSceneManager's events do not fire for those.
        static readonly Dictionary<string, bool> s_Cache = new Dictionary<string, bool>(StringComparer.Ordinal);

        static SceneLoadUtils()
        {
            EditorSceneManager.sceneOpened += (_, __) => s_Cache.Clear();
            EditorSceneManager.sceneClosed += _ => s_Cache.Clear();
            EditorApplication.playModeStateChanged += _ => s_Cache.Clear();
        }

        /// <summary>
        /// Checks whether the scene identified by <paramref name="sceneGuid"/> (falling back to
        /// <paramref name="sceneName"/>) is loaded. Results are cached per GUID until any scene opens
        /// or closes, because this is called for every row on every repaint.
        /// </summary>
        public static bool IsSceneLoaded(string sceneGuid, string sceneName)
        {
            // Prefer the GUID: it is stable across scene renames, and it is the only field that still
            // names the authored scene for a ref whose target was moved to DontDestroyOnLoad while
            // playing. Such refs can carry the pseudo-scene name alongside a valid GUID, and checking
            // the name first reported their scene as unloaded in Edit mode — so the row grayed out
            // even with the authoring scene open and the object sitting in it.
            if (!string.IsNullOrEmpty(sceneGuid))
            {
                if (s_Cache.TryGetValue(sceneGuid, out var cached))
                    return cached;

                var scenePath = AssetDatabase.GUIDToAssetPath(sceneGuid);
                if (!string.IsNullOrEmpty(scenePath))
                {
                    for (int i = 0; i < SceneManager.sceneCount; i++)
                    {
                        var loadedScene = SceneManager.GetSceneAt(i);
                        if (loadedScene.path != scenePath) continue;

                        s_Cache[sceneGuid] = loadedScene.isLoaded;
                        return loadedScene.isLoaded;
                    }
                }

                s_Cache[sceneGuid] = false;
                return false;
            }

            // A ref captured while its target already lived in DontDestroyOnLoad stores that
            // pseudo-scene, which has no asset GUID and is not enumerated by SceneManager.
            // It is live for the whole Play session and gone outside it.
            if (sceneName == DontDestroyOnLoadSceneName)
                return EditorApplication.isPlaying;

            // Fallback: name-based check, for backward compatibility or if the GUID is missing.
            if (!string.IsNullOrEmpty(sceneName))
                return SceneManager.GetSceneByName(sceneName).isLoaded;

            return false;
        }
    }
}
#endif
