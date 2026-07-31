using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SecretZauce.SecondBrain.Editor
{
    public static class SceneObjectRefUtils
    {
        /// <summary>
        /// Notification message shown when a drag from an unsaved scene is blocked.
        /// </summary>
        public const string UnsavedSceneNotification =
            "Cannot link objects from unsaved scenes. Save the scene first.";

        /// <summary>
        /// Returns true if <paramref name="obj"/> is a scene GameObject whose containing scene has
        /// never been saved to disk (its path is empty).  Such objects cannot produce a stable
        /// <see cref="GlobalObjectId"/> and must not be linked as <see cref="SceneObjectRef"/> assets.
        /// </summary>
        public static bool IsSceneObjectFromUnsavedScene(Object obj)
        {
            if (obj == null || EditorUtility.IsPersistent(obj))
                return false;

            if (obj is GameObject go)
                return string.IsNullOrEmpty(go.scene.path);

            if (obj is Component component && component.gameObject != null)
                return string.IsNullOrEmpty(component.gameObject.scene.path);

            return false;
        }

        public static bool IsGameObjectFromUnsavedScene(Object obj)
        {
            return obj is GameObject && IsSceneObjectFromUnsavedScene(obj);
        }

        /// <summary>
        /// True when <paramref name="go"/> currently lives in a scene that exists only while playing —
        /// the DontDestroyOnLoad pseudo-scene, or a scene created at runtime — and therefore has no
        /// asset on disk.
        ///
        /// Such a scene must never be written into a ref's last-known metadata: it names a location
        /// that cannot exist in Edit mode, and the object's hierarchy path there is unrelated to the
        /// one it has in its authored scene (DontDestroyOnLoad only accepts roots, so a linked child
        /// is detached first and its recorded path collapses to a suffix).
        ///
        /// A prefab asset is deliberately excluded: its scene handle is invalid rather than merely
        /// path-less, and clearing the metadata is the right response when one is assigned.
        /// </summary>
        public static bool IsInRuntimeOnlyScene(GameObject go)
        {
            return go != null && go.scene.IsValid() && string.IsNullOrEmpty(go.scene.path);
        }

        /// <summary>
        /// Creates a SceneObjectRef for the given scene GameObject.
        /// When <paramref name="parentAsset"/> is provided and is a saved asset, the ref is embedded
        /// as a sub-asset inside the parent's asset file.
        /// Otherwise falls back to creating a standalone .asset file in <paramref name="folder"/>.
        /// Returns the created asset, or null on failure.
        /// </summary>
        public static SceneObjectRef CreateSceneObjectRef(GameObject go, string folder, Object parentAsset = null)
        {
            var sceneObjectRef = ScriptableObject.CreateInstance<SceneObjectRef>();
            sceneObjectRef.name = SanitizeFileName("Link to " + go.name);

            // Populate sceneObject fields via SerializedObject so private [SerializeField] fields are set
            var so = new SerializedObject(sceneObjectRef);
            so.Update();
            var sceneObjProp = so.FindProperty("sceneObject");
            if (sceneObjProp != null)
            {
                var globalIdProp = sceneObjProp.FindPropertyRelative("globalId");
                if (globalIdProp != null)
                {
                    var gid = GlobalObjectId.GetGlobalObjectIdSlow(go);
                    globalIdProp.stringValue = gid.ToString();
                }

                var lastKnownNameProp = sceneObjProp.FindPropertyRelative("lastKnownName");
                if (lastKnownNameProp != null)
                    lastKnownNameProp.stringValue = go.name;

                var lastKnownSceneProp = sceneObjProp.FindPropertyRelative("lastKnownScene");
                if (lastKnownSceneProp != null)
                    lastKnownSceneProp.stringValue = go.scene.name;

                var lastKnownSceneGuidProp = sceneObjProp.FindPropertyRelative("lastKnownSceneGuid");
                if (lastKnownSceneGuidProp != null && !string.IsNullOrEmpty(go.scene.path))
                    lastKnownSceneGuidProp.stringValue = AssetDatabase.AssetPathToGUID(go.scene.path);

                var lastKnownPathProp = sceneObjProp.FindPropertyRelative("lastKnownPath");
                if (lastKnownPathProp != null)
                    lastKnownPathProp.stringValue = GetGameObjectPath(go);

                var wasTrackedDuringPlayModeProp = sceneObjProp.FindPropertyRelative("wasTrackedDuringPlayMode");
                if (wasTrackedDuringPlayModeProp != null)
                    wasTrackedDuringPlayModeProp.boolValue = EditorApplication.isPlaying;
            }

            so.ApplyModifiedProperties();

            // Prefer embedding as a sub-asset inside the parent's asset file
            string parentAssetPath = parentAsset != null ? AssetDatabase.GetAssetPath(parentAsset) : null;
            if (!string.IsNullOrEmpty(parentAssetPath))
            {
                AssetDatabase.AddObjectToAsset(sceneObjectRef, parentAssetPath);
                Undo.RegisterCreatedObjectUndo(sceneObjectRef, "Create SceneObjectRef");
            }
            else
            {
                // Fallback: create a standalone .asset file in the specified folder
                var assetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(folder,
                    sceneObjectRef.name + ".asset"));
                AssetDatabase.CreateAsset(sceneObjectRef, assetPath);
                AssetDatabase.ImportAsset(assetPath);
                Undo.RegisterCreatedObjectUndo(sceneObjectRef, "Create SceneObjectRef");
            }

            return sceneObjectRef;
        }

        /// <summary>
        /// Rewrites the scene and hierarchy path recorded on <paramref name="refAsset"/> from the
        /// object it currently resolves to — but only when what is recorded is the DontDestroyOnLoad
        /// pseudo-scene.
        ///
        /// Refs captured before that pseudo-scene was rejected at capture time still carry it on
        /// disk, and it names a place that cannot exist in Edit mode: the row rendered as "scene not
        /// loaded" with a "(DontDestroyOnLoad)" suffix, and the go-to action had no scene to open.
        /// Repairing on the first Edit-mode resolve clears it for good — afterwards the guard below
        /// is false, so this writes at most once per ref.
        ///
        /// <paramref name="structProperty"/> is the name of the SceneObject/SceneComponent field on
        /// the ref asset ("sceneObject" or "sceneComponent").
        /// </summary>
        public static void RepairPseudoSceneMetadata(Object refAsset, string structProperty, GameObject resolved)
        {
            // Never touch assets while playing: the object may legitimately be in DontDestroyOnLoad
            // right now, and any edit would be lost when the Play session tears down anyway.
            if (EditorApplication.isPlaying || refAsset == null || resolved == null)
                return;

            // Only repair into a real, saved scene — never record another transient one.
            var scenePath = resolved.scene.path;
            if (string.IsNullOrEmpty(scenePath))
                return;

            var so = new SerializedObject(refAsset);
            var structProp = so.FindProperty(structProperty);
            var sceneProp = structProp?.FindPropertyRelative("lastKnownScene");
            if (sceneProp == null || sceneProp.stringValue != SceneLoadUtils.DontDestroyOnLoadSceneName)
                return;

            sceneProp.stringValue = resolved.scene.name;

            var guidProp = structProp.FindPropertyRelative("lastKnownSceneGuid");
            if (guidProp != null)
                guidProp.stringValue = AssetDatabase.AssetPathToGUID(scenePath);

            // The recorded path is the collapsed one the object had as a DontDestroyOnLoad root;
            // the live object gives us its real place in the authored hierarchy back.
            var pathProp = structProp.FindPropertyRelative("lastKnownPath");
            if (pathProp != null)
                pathProp.stringValue = GetGameObjectPath(resolved);

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(refAsset);
        }

        /// <summary>
        /// Replaces characters invalid for file/asset names with underscores.
        /// </summary>
        static string SanitizeFileName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "SceneObjectRef";
            var invalidChars = Path.GetInvalidFileNameChars();
            var chars = name.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (System.Array.IndexOf(invalidChars, chars[i]) >= 0)
                    chars[i] = '_';
            }
            return new string(chars);
        }

        public static void GoToSceneObject(string globalId, string sceneName, string sceneGuid, string lastKnownPath)
        {
            EditorGUIUtils.ShowNotificationOnActiveView(new GUIContent("Go to '" + lastKnownPath + "' in scene '" + sceneName + "'"));
            // Run after current GUI event to avoid GUI state issues
            EditorApplication.delayCall += () =>
            {
                try
                {
                    // Find a scene asset - prefer GUID lookup for renamed scenes
                    string scenePath = null;
                    if (!string.IsNullOrEmpty(sceneGuid))
                    {
                        scenePath = AssetDatabase.GUIDToAssetPath(sceneGuid);
                    }
                    
                    // Fallback: search by scene name
                    if (string.IsNullOrEmpty(scenePath) && !string.IsNullOrEmpty(sceneName))
                    {
                        var guids = AssetDatabase.FindAssets("t:Scene");
                        foreach (var guid in guids)
                        {
                            var path = AssetDatabase.GUIDToAssetPath(guid);
                            if (Path.GetFileNameWithoutExtension(path) == sceneName)
                            {
                                scenePath = path;
                                break;
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(scenePath))
                    {
                        var opened = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                    }

                    // After opening (or if no scene found), try to resolve via globalId first
                    if (!string.IsNullOrEmpty(globalId))
                    {
                        var go = SceneObjectMap.Resolve(globalId);
                        if (go != null)
                        {
                            SelectionUtils.SetActiveAndPing(go);
                            return;
                        }
                    }

                    // Fallback: try to find by stored path inside scene
                    if (!string.IsNullOrEmpty(lastKnownPath))
                    {
                        // GameObject.Find accepts paths with '/'
                        var found = GameObject.Find(lastKnownPath);
                        if (found != null)
                        {
                            SelectionUtils.SetActiveAndPing(found);
                            return;
                        }

                        // Try by name (last segment)
                        var name = lastKnownPath.Split('/').LastOrDefault();
                        if (!string.IsNullOrEmpty(name))
                        {
                            var foundByName = GameObject.Find(name);
                            if (foundByName != null)
                            {
                                SelectionUtils.SetActiveAndPing(foundByName);
                                return;
                            }
                        }
                    }

                    EditorUtility.DisplayDialog("Go to object", "Could not locate the object in the opened scene.", "OK");
                }
                catch (System.Exception ex)
                {
                    Debug.LogException(ex);
                    EditorUtility.DisplayDialog("Go to object", "An error occurred while trying to open/select the scene object: " + ex.Message, "OK");
                }
            };
        }

        public static string GetGameObjectPath(GameObject go)
        {
            string path = go.name;
            Transform current = go.transform;
            while (current.parent != null)
            {
                current = current.parent;
                path = current.name + "/" + path;
            }
            return path;
        }

        /// <summary>
        /// Renames the scene GameObject referenced by <paramref name="sceneRef"/> and updates the ref's
        /// stored metadata (lastKnownName, lastKnownPath) and asset name accordingly.
        /// Requires the object's scene to be currently loaded.
        /// Returns true on success.
        /// </summary>
        public static bool RenameSceneObjectAndUpdateRef(SceneObjectRef sceneRef, string newName)
        {
            if (sceneRef == null || string.IsNullOrWhiteSpace(newName))
                return false;

            newName = newName.Trim();
            if (string.IsNullOrEmpty(newName))
                return false;

            var sceneObj = sceneRef.sceneObject;
            var go = SceneObjectMap.Resolve(sceneObj);
            if (go == null)
            {
                EditorUtility.DisplayDialog("Rename Failed", "Could not locate the scene object. Make sure its scene is open.", "OK");
                return false;
            }

            if (go.name == newName && sceneObj.LastKnownName == newName &&
                sceneRef.name == SanitizeFileName("Link to " + newName))
                return false; // nothing to change

            int undoGroup = Undo.GetCurrentGroup();

            // Rename the scene GameObject
            Undo.RecordObject(go, "Rename Scene Object");
            go.name = newName;
            EditorUtility.SetDirty(go);

            // Update the SceneObjectRef metadata via SerializedObject so [SerializeField] fields are set
            var so = new SerializedObject(sceneRef);
            so.Update();
            var sceneObjProp = so.FindProperty("sceneObject");
            if (sceneObjProp != null)
            {
                var lastKnownNameProp = sceneObjProp.FindPropertyRelative("lastKnownName");
                if (lastKnownNameProp != null)
                    lastKnownNameProp.stringValue = newName;

                var lastKnownPathProp = sceneObjProp.FindPropertyRelative("lastKnownPath");
                if (lastKnownPathProp != null)
                    lastKnownPathProp.stringValue = GetGameObjectPath(go);
            }
            so.ApplyModifiedProperties();

            // Rename the SceneObjectRef asset to "Link to <newName>"
            string newAssetName = SanitizeFileName("Link to " + newName);
            string assetPath = AssetDatabase.GetAssetPath(sceneRef);
            if (!string.IsNullOrEmpty(assetPath))
            {
                if (AssetDatabase.IsMainAsset(sceneRef))
                {
                    AssetDatabase.RenameAsset(assetPath, newAssetName);
                }
                else
                {
                    sceneRef.name = newAssetName;
                    SubAssetRefreshUtils.MarkDirtyAndSave(sceneRef);
                }
            }

            Undo.CollapseUndoOperations(undoGroup);
            AssetDatabase.SaveAssets();

            return true;
        }
    }
}
