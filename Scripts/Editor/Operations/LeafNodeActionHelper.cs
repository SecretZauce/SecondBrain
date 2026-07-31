using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SecretZauce.SecondBrain.Editor
{
    /// <summary>
    /// Utility class to handle the "enter" action for leaf nodes with '>' button.
    /// Centralizes logic for opening scene assets, prefabs, and Base objects.
    /// </summary>
    public static class LeafNodeActionHelper
    {
        /// <summary>
        /// Checks if a scene is currently loaded, using GUID for rename-safe detection.
        /// Falls back to name-based check for backward compatibility.
        /// </summary>
        static bool IsSceneLoaded(string sceneGuid, string sceneName)
            => SceneLoadUtils.IsSceneLoaded(sceneGuid, sceneName);

        /// <summary>
        /// Performs the action associated with the '>' button on a leaf node.
        /// Returns true if the action was handled, false otherwise.
        /// </summary>
        public static bool TryEnterLeafNode(Object node, BrowserController controller)
        {
            if (node == null)
                return false;

#if SECOND_BRAIN_PRO
            // Handle ActionItem — execute the action
            if (node is ActionItem actionItem)
            {
                try { actionItem.Execute(); }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                    return false; // action failed — allow caller to fall back to rename
                }
                return true;
            }
#endif
            
            // Handle SceneObjectRef — only navigate when the scene is not already loaded.
            if (node is SceneObjectRef sceneRef)
            {
                var sceneObj = sceneRef.sceneObject;
                string sceneName = sceneObj?.LastKnownScene;
                string sceneGuid = sceneObj?.LastKnownSceneGuid;
                bool sceneLoaded = IsSceneLoaded(sceneGuid, sceneName);
                if (sceneLoaded)
                    return false; // '>' button is hidden; Enter has no action when scene is open

                string assetPath = sceneObj?.LastKnownPath;
                string gid = sceneObj?.GlobalId;
                SceneObjectRefUtils.GoToSceneObject(gid, sceneName, sceneGuid, assetPath);
                return true;
            }

            if (node is SceneComponentRef sceneComponentRef)
            {
                var sceneComponent = sceneComponentRef.sceneComponent;
                string sceneName = sceneComponent?.LastKnownScene;
                string sceneGuid = sceneComponent?.LastKnownSceneGuid;
                bool sceneLoaded = IsSceneLoaded(sceneGuid, sceneName);
                if (sceneLoaded)
                    return false;

                SceneComponentRefUtils.GoToSceneComponent(
                    sceneComponent?.GlobalId,
                    sceneName,
                    sceneGuid,
                    sceneComponent?.LastKnownPath);
                return true;
            }

            // Handle URL TextAsset
            if (node is TextAsset textAsset && IsUrl(textAsset.text))
            {
                Application.OpenURL(textAsset.text.Trim());
                return true;
            }

            // Handle other types
            string objAssetPath = AssetDatabase.GetAssetPath(node);
            bool isSceneAsset = node is SceneAsset;
            bool isPrefabAsset = !string.IsNullOrEmpty(objAssetPath) && objAssetPath.EndsWith(".prefab");
            var baseTarget = node as Base;
            bool isBase = baseTarget != null;
            bool isFolder = node is DefaultAsset && !string.IsNullOrEmpty(objAssetPath) && AssetDatabase.IsValidFolder(objAssetPath);

            if (isFolder)
            {
                EditorGUIUtils.EnterFolderInProjectWindow(node);
                return true;
            }

            if (isSceneAsset)
            {
                if (!string.IsNullOrEmpty(objAssetPath))
                {
                    var notif = new GUIContent("Open Scene: " + node.name);
                    EditorGUIUtils.ShowNotificationOnActiveView(notif);
                    EditorSceneManager.OpenScene(objAssetPath, OpenSceneMode.Single);
                    EditorGUIUtils.FocusHierarchyWindowIfPresent();
                    return true;
                }
            }
            else if (isPrefabAsset)
            {
                if (!string.IsNullOrEmpty(objAssetPath))
                {
                    var notif = new GUIContent("Open Prefab: " + objAssetPath);
                    EditorGUIUtils.ShowNotificationOnActiveView(notif);
                    PrefabStageUtility.OpenPrefab(objAssetPath);
                    EditorGUIUtils.FocusHierarchyWindowIfPresent();
                    return true;
                }
            }
            else if (isBase)
            {
                controller?.SetTargetWithUndo(baseTarget);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Checks if a leaf node has an associated '>' button action.
        /// </summary>
        public static bool HasEnterAction(Object node)
        {
            if (node == null)
                return false;

#if SECOND_BRAIN_PRO
            // ActionItem always has an Enter action (execute)
            if (node is ActionItem)
                return true;
#endif
            
            // SceneObjectRef has an Enter action only when its scene is not loaded
            if (node is SceneObjectRef sor)
            {
                string sceneName = sor.sceneObject?.LastKnownScene;
                string sceneGuid  = sor.sceneObject?.LastKnownSceneGuid;
                return !IsSceneLoaded(sceneGuid, sceneName);
            }

            if (node is SceneComponentRef scr)
            {
                string sceneName = scr.sceneComponent?.LastKnownScene;
                string sceneGuid  = scr.sceneComponent?.LastKnownSceneGuid;
                return !IsSceneLoaded(sceneGuid, sceneName);
            }

            // Check for URL TextAsset
            if (node is TextAsset urlTextAsset && IsUrl(urlTextAsset.text))
                return true;

            // Check for scene assets, prefabs, Base objects, or folder assets
            string assetPath = AssetDatabase.GetAssetPath(node);
            bool isSceneAsset = node is SceneAsset;
            bool isPrefabAsset = !string.IsNullOrEmpty(assetPath) && assetPath.EndsWith(".prefab");
            bool isBase = node is Base;
            bool isFolder = node is DefaultAsset && !string.IsNullOrEmpty(assetPath) && AssetDatabase.IsValidFolder(assetPath);

            return isSceneAsset || isPrefabAsset || isBase || isFolder;
        }

        /// <summary>
        /// Opens the scene asset, sets it as active, and enters play mode.
        /// Prompts the user to save any modified scenes first.
        /// Shows a notification on the active SceneView or GameView.
        /// Returns true if the action was handled.
        /// </summary>
        public static bool TryPlayScene(Object node)
        {
            if (node == null || !(node is SceneAsset))
                return false;

            string sceneAssetPath = AssetDatabase.GetAssetPath(node);
            if (string.IsNullOrEmpty(sceneAssetPath))
                return false;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return false;

            var scene = EditorSceneManager.OpenScene(sceneAssetPath, OpenSceneMode.Single);
            if (!scene.IsValid() || !scene.isLoaded)
                return false;

            var notif = new GUIContent("▶ Play: " + node.name);
            EditorGUIUtils.ShowNotificationOnActiveView(notif);
            EditorApplication.isPlaying = true;
            return true;
        }

        /// <summary>
        /// Returns true when the given string is an absolute HTTP/HTTPS URL.
        /// </summary>
        public static bool IsUrl(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;
            var trimmed = text.Trim();
            return Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) &&
                   uri.Scheme is "http" or "https";
        }
    }
}
