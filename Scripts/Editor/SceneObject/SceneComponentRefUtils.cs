using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
#if UNITY_6000_3_OR_NEWER
using UnityEngine.Assemblies;
#endif
using Object = UnityEngine.Object;

namespace SecretZauce.SecondBrain.Editor
{
    public static class SceneComponentRefUtils
    {
        static readonly Dictionary<string, Type> s_TypeCache = new Dictionary<string, Type>(StringComparer.Ordinal);

        public static SceneComponentRef CreateSceneComponentRef(Component component, string folder, Object parentAsset = null)
        {
            if (component == null)
                return null;

            var sceneComponentRef = ScriptableObject.CreateInstance<SceneComponentRef>();
            sceneComponentRef.name = SanitizeFileName("Link to " + GetDisplayName(component));

            var so = new SerializedObject(sceneComponentRef);
            so.Update();
            var sceneComponentProp = so.FindProperty("sceneComponent");
            if (sceneComponentProp != null)
            {
                var globalIdProp = sceneComponentProp.FindPropertyRelative("globalId");
                if (globalIdProp != null)
                    globalIdProp.stringValue = GlobalObjectId.GetGlobalObjectIdSlow(component).ToString();

                var lastKnownSceneProp = sceneComponentProp.FindPropertyRelative("lastKnownScene");
                if (lastKnownSceneProp != null)
                    lastKnownSceneProp.stringValue = component.gameObject.scene.name;

                var lastKnownSceneGuidProp = sceneComponentProp.FindPropertyRelative("lastKnownSceneGuid");
                if (lastKnownSceneGuidProp != null && !string.IsNullOrEmpty(component.gameObject.scene.path))
                    lastKnownSceneGuidProp.stringValue = AssetDatabase.AssetPathToGUID(component.gameObject.scene.path);

                var lastKnownPathProp = sceneComponentProp.FindPropertyRelative("lastKnownPath");
                if (lastKnownPathProp != null)
                    lastKnownPathProp.stringValue = SceneObjectRefUtils.GetGameObjectPath(component.gameObject);

                var lastKnownGameObjectNameProp = sceneComponentProp.FindPropertyRelative("lastKnownGameObjectName");
                if (lastKnownGameObjectNameProp != null)
                    lastKnownGameObjectNameProp.stringValue = component.gameObject.name;

                var lastKnownComponentTypeProp = sceneComponentProp.FindPropertyRelative("lastKnownComponentType");
                if (lastKnownComponentTypeProp != null)
                    lastKnownComponentTypeProp.stringValue = component.GetType().AssemblyQualifiedName ?? string.Empty;

                var lastKnownComponentTypeNameProp = sceneComponentProp.FindPropertyRelative("lastKnownComponentTypeName");
                if (lastKnownComponentTypeNameProp != null)
                    lastKnownComponentTypeNameProp.stringValue = component.GetType().Name;

                var wasTrackedDuringPlayModeProp = sceneComponentProp.FindPropertyRelative("wasTrackedDuringPlayMode");
                if (wasTrackedDuringPlayModeProp != null)
                    wasTrackedDuringPlayModeProp.boolValue = EditorApplication.isPlaying;
            }

            so.ApplyModifiedProperties();

            string parentAssetPath = parentAsset != null ? AssetDatabase.GetAssetPath(parentAsset) : null;
            if (!string.IsNullOrEmpty(parentAssetPath))
            {
                AssetDatabase.AddObjectToAsset(sceneComponentRef, parentAssetPath);
                Undo.RegisterCreatedObjectUndo(sceneComponentRef, "Create SceneComponentRef");
            }
            else
            {
                var assetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(folder,
                    sceneComponentRef.name + ".asset"));
                AssetDatabase.CreateAsset(sceneComponentRef, assetPath);
                AssetDatabase.ImportAsset(assetPath);
                Undo.RegisterCreatedObjectUndo(sceneComponentRef, "Create SceneComponentRef");
            }

            return sceneComponentRef;
        }

        public static Component Resolve(SceneComponent sceneComponent)
        {
            return SceneObjectMap.Resolve(sceneComponent);
        }

        public static Type GetComponentType(SceneComponent sceneComponent)
        {
            return ResolveComponentType(sceneComponent?.LastKnownComponentType);
        }

        public static Type ResolveComponentType(string componentTypeName)
        {
            if (string.IsNullOrEmpty(componentTypeName))
                return typeof(Component);

            if (s_TypeCache.TryGetValue(componentTypeName, out var cached))
                return cached;

            var resolvedType = Type.GetType(componentTypeName, false);
            if (resolvedType != null)
            {
                s_TypeCache[componentTypeName] = resolvedType;
                return resolvedType;
            }

#if UNITY_6000_3_OR_NEWER
            var loadedAssemblies = CurrentAssemblies.GetLoadedAssemblies();
#else
            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
#endif
            foreach (var assembly in loadedAssemblies)
            {
                try
                {
                    resolvedType = assembly.GetType(componentTypeName, false);
                    if (resolvedType != null)
                    {
                        s_TypeCache[componentTypeName] = resolvedType;
                        return resolvedType;
                    }

                    resolvedType = assembly.GetTypes().FirstOrDefault(type =>
                        string.Equals(type.FullName, componentTypeName, StringComparison.Ordinal) ||
                        string.Equals(type.Name, componentTypeName, StringComparison.Ordinal));
                    if (resolvedType != null)
                    {
                        s_TypeCache[componentTypeName] = resolvedType;
                        return resolvedType;
                    }
                }
                catch
                {
                    // Ignore dynamic or partially loaded assemblies.
                }
            }

            s_TypeCache[componentTypeName] = typeof(Component);
            return typeof(Component);
        }

        public static string GetDisplayName(SceneComponent sceneComponent)
        {
            if (sceneComponent == null)
                return "Scene Component";

            return GetDisplayName(
                sceneComponent.LastKnownComponentTypeName,
                string.IsNullOrEmpty(sceneComponent.LastKnownGameObjectName)
                    ? GetLastKnownGameObjectName(sceneComponent.LastKnownPath)
                    : sceneComponent.LastKnownGameObjectName);
        }

        public static string GetDisplayName(Component component)
        {
            if (component == null)
                return "Scene Component";

            return GetDisplayName(component.GetType().Name, component.gameObject != null ? component.gameObject.name : null);
        }

        public static string GetDisplayName(string componentTypeName, string gameObjectName)
        {
            if (string.IsNullOrEmpty(componentTypeName))
                componentTypeName = "Component";

            return string.IsNullOrEmpty(gameObjectName)
                ? componentTypeName
                : $"{gameObjectName} ({componentTypeName})";
        }

        public static string GetLastKnownGameObjectName(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            return path.Split('/').LastOrDefault() ?? string.Empty;
        }

        public static void GoToSceneComponent(string globalId, string sceneName, string sceneGuid, string lastKnownPath)
        {
            EditorGUIUtils.ShowNotificationOnActiveView(new GUIContent("Go to component in scene '" + sceneName + "'"));
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
                        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

                    if (!string.IsNullOrEmpty(globalId))
                    {
                        var component = SceneObjectMap.ResolveComponent(globalId);
                        if (component != null)
                        {
                            SelectionUtils.SetActiveAndPing(component);
                            return;
                        }
                    }

                    if (!string.IsNullOrEmpty(lastKnownPath))
                    {
                        var found = GameObject.Find(lastKnownPath);
                        if (found != null)
                        {
                            SelectionUtils.SetActiveAndPing(found);
                            return;
                        }

                        var name = GetLastKnownGameObjectName(lastKnownPath);
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

                    EditorUtility.DisplayDialog("Go to component", "Could not locate the component in the opened scene.", "OK");
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                    EditorUtility.DisplayDialog("Go to component", "An error occurred while trying to open/select the component: " + ex.Message, "OK");
                }
            };
        }

        static string SanitizeFileName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "SceneComponentRef";

            var invalidChars = Path.GetInvalidFileNameChars();
            var chars = name.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (Array.IndexOf(invalidChars, chars[i]) >= 0)
                    chars[i] = '_';
            }
            return new string(chars);
        }
    }
}
