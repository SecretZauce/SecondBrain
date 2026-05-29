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

                var lastKnownPathProp = sceneObjProp.FindPropertyRelative("lastKnownPath");
                if (lastKnownPathProp != null)
                    lastKnownPathProp.stringValue = GetGameObjectPath(go);
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

        public static void GoToSceneObject(string globalId, string sceneName, string lastKnownPath)
        {
            EditorGUIUtils.ShowNotificationOnActiveView(new GUIContent("Go to '" + lastKnownPath + "' in scene '" + sceneName + "'"));
            // Run after current GUI event to avoid GUI state issues
            EditorApplication.delayCall += () =>
            {
                try
                {
                    // Find a scene asset that matches the stored scene name
                    string scenePath = null;
                    if (!string.IsNullOrEmpty(sceneName))
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
            var go = SceneObjectMap.Resolve(sceneObj?.GlobalId);
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
