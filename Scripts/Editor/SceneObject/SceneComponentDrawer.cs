#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SecretZauce.SecondBrain.Editor
{
    [CustomPropertyDrawer(typeof(SceneComponent))]
    public class SceneComponentDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            property.serializedObject.Update();

            SerializedProperty idProp = property.FindPropertyRelative("globalId");

            var lastKnownPath = property.FindPropertyRelative("lastKnownPath")?.stringValue;
            var lastKnownSceneProp = property.FindPropertyRelative("lastKnownScene");
            var lastKnownSceneGuidProp = property.FindPropertyRelative("lastKnownSceneGuid");
            var lastKnownScene = lastKnownSceneProp?.stringValue;
            var lastKnownSceneGuid = lastKnownSceneGuidProp?.stringValue;
            var lastKnownGameObjectName = property.FindPropertyRelative("lastKnownGameObjectName")?.stringValue;
            var lastKnownComponentType = property.FindPropertyRelative("lastKnownComponentType")?.stringValue;
            var lastKnownComponentTypeName = property.FindPropertyRelative("lastKnownComponentTypeName")?.stringValue;

            // Resolve with the path fallback, not the bare GID, so components whose GlobalObjectId
            // stops resolving in Play mode — prefab instances, and anything moved to the
            // DontDestroyOnLoad scene — are still found instead of reported as Missing.
            Object current = null;
            if (idProp != null && !string.IsNullOrEmpty(idProp.stringValue))
                current = SceneObjectMap.ResolveComponent(idProp.stringValue, lastKnownPath,
                    lastKnownSceneGuid, lastKnownScene, lastKnownComponentType);

            EditorGUI.BeginProperty(position, label, property);
            Rect fieldRect = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

            float infoWidth = Mathf.Min(180f, fieldRect.width * 0.35f);
            float spacing = 4f;
            Rect objectRect = new Rect(fieldRect.x, fieldRect.y, fieldRect.width - infoWidth - spacing, fieldRect.height);
            Rect infoRect = new Rect(objectRect.xMax + spacing, fieldRect.y, infoWidth, fieldRect.height);

            Component resolved = current as Component;

            // Auto-update scene name if the scene was renamed (GUID matches but name differs)
            bool sceneWasRenamed = false;
            if (resolved != null && resolved.gameObject != null && !string.IsNullOrEmpty(lastKnownSceneGuid))
            {
                var currentScenePath = resolved.gameObject.scene.path;
                if (!string.IsNullOrEmpty(currentScenePath))
                {
                    var currentSceneGuid = AssetDatabase.AssetPathToGUID(currentScenePath);
                    if (currentSceneGuid == lastKnownSceneGuid && resolved.gameObject.scene.name != lastKnownScene)
                    {
                        sceneWasRenamed = true;
                        lastKnownScene = resolved.gameObject.scene.name;
                    }
                }
            }
            // Fallback: if no scene name stored, try to resolve from GUID
            else if (resolved == null && !string.IsNullOrEmpty(lastKnownSceneGuid) && string.IsNullOrEmpty(lastKnownScene))
            {
                var scenePath = AssetDatabase.GUIDToAssetPath(lastKnownSceneGuid);
                if (!string.IsNullOrEmpty(scenePath))
                {
                    lastKnownScene = System.IO.Path.GetFileNameWithoutExtension(scenePath);
                }
            }

            Component displayed;
            bool objectChanged = false;
            if (resolved != null)
            {
                EditorGUI.BeginChangeCheck();
                displayed = EditorGUI.ObjectField(objectRect, resolved, typeof(Component), true) as Component;
                objectChanged = EditorGUI.EndChangeCheck();
            }
            else
            {
                string objectLabel = SceneComponentRefUtils.GetDisplayName(
                    lastKnownComponentTypeName,
                    string.IsNullOrEmpty(lastKnownGameObjectName)
                        ? SceneComponentRefUtils.GetLastKnownGameObjectName(lastKnownPath)
                        : lastKnownGameObjectName);
                EditorGUI.LabelField(objectRect, string.IsNullOrEmpty(objectLabel) ? "-" : objectLabel);
                displayed = null;
            }

            bool isPickedMismatched = false;
            // A component whose GameObject has been moved to DontDestroyOnLoad at runtime always
            // looks "mismatched" — its scene and hierarchy path are both runtime-only values.
            // Treating that as a mismatch rewrote the ref's authored metadata with the pseudo-scene,
            // which then read as an unloaded scene back in Edit mode.
            if (displayed != null && !SceneObjectRefUtils.IsInRuntimeOnlyScene(displayed.gameObject))
            {
                string displayedPath = displayed.gameObject != null
                    ? SceneObjectRefUtils.GetGameObjectPath(displayed.gameObject)
                    : string.Empty;
                isPickedMismatched =
                    (lastKnownPath != null && lastKnownPath != displayedPath) ||
                    (lastKnownGameObjectName != null && lastKnownGameObjectName != displayed.gameObject.name) ||
                    (lastKnownComponentType != null && lastKnownComponentType != displayed.GetType().AssemblyQualifiedName) ||
                    (lastKnownComponentTypeName != null && lastKnownComponentTypeName != displayed.GetType().Name);
                
                // Only flag scene mismatch if GUID also differs (not just name)
                if (!string.IsNullOrEmpty(lastKnownSceneGuid) && displayed.gameObject != null)
                {
                    var currentScenePath = displayed.gameObject.scene.path;
                    if (!string.IsNullOrEmpty(currentScenePath))
                    {
                        var currentSceneGuid = AssetDatabase.AssetPathToGUID(currentScenePath);
                        if (currentSceneGuid != lastKnownSceneGuid)
                            isPickedMismatched = true;
                    }
                }
                else if (lastKnownScene != null && displayed.gameObject != null && lastKnownScene != displayed.gameObject.scene.name)
                {
                    // Fallback: if no GUID stored, check by name
                    isPickedMismatched = true;
                }
            }

            string status = "None";
            if (idProp != null && !string.IsNullOrEmpty(idProp.stringValue))
                status = current != null ? "Found" : "Missing";
            else if (resolved != null)
                status = "Assigned";

            string scenePart = string.IsNullOrEmpty(lastKnownScene) ? "-" : lastKnownScene;
            float dotSize = 10f;
            Rect dotRect = new Rect(infoRect.x, infoRect.y + (infoRect.height - dotSize) / 2f, dotSize, dotSize);
            float goButtonWidth = 36f;
            Rect goRect = new Rect(infoRect.xMax - goButtonWidth, infoRect.y, goButtonWidth, infoRect.height);
            Rect textRect = new Rect(dotRect.xMax + 6f, infoRect.y, goRect.x - (dotRect.xMax + 6f), infoRect.height);

            if (status == "Found" || status == "Missing")
            {
                var color = status == "Found" ? Color.green : Color.red;
                Handles.BeginGUI();
                var prevColor = Handles.color;
                Handles.color = color;
                Vector3 center = new Vector3(dotRect.x + dotRect.width * 0.5f, dotRect.y + dotRect.height * 0.5f, 0f);
                float radius = Mathf.Min(dotRect.width, dotRect.height) * 0.5f;
                Handles.DrawSolidDisc(center, Vector3.forward, radius);
                Handles.color = prevColor;
                Handles.EndGUI();
            }

            EditorGUI.LabelField(textRect, scenePart, EditorStyles.miniLabel);

            if (status == "Missing")
            {
                if (GUI.Button(goRect, "Go", EditorStyles.miniButton))
                    SceneComponentRefUtils.GoToSceneComponent(idProp?.stringValue, lastKnownScene, lastKnownSceneGuid, lastKnownPath);
            }

            if (objectChanged || isPickedMismatched || sceneWasRenamed)
            {
                var newPicked = displayed;
                string newGid = string.Empty;
                if (objectChanged && newPicked != null)
                    newGid = GlobalObjectId.GetGlobalObjectIdSlow(newPicked).ToString();

                var targets = property.serializedObject.targetObjects;
                foreach (var target in targets)
                {
                    if (target == null)
                        continue;

                    var so = new SerializedObject(target);
                    so.Update();
                    var rootProp = so.FindProperty(property.propertyPath);
                    if (rootProp == null)
                    {
                        so.ApplyModifiedProperties();
                        continue;
                    }

                    if (objectChanged)
                    {
                        var gidProp = rootProp.FindPropertyRelative("globalId");
                        if (gidProp != null)
                            gidProp.stringValue = newGid ?? string.Empty;
                    }

                    if (sceneWasRenamed)
                    {
                        // Update the scene name when rename detected
                        var sceneNameProp = rootProp.FindPropertyRelative("lastKnownScene");
                        if (sceneNameProp != null && newPicked != null && newPicked.gameObject != null)
                            sceneNameProp.stringValue = newPicked.gameObject.scene.name;
                    }

                    RecordFallbackInfo(rootProp, newPicked);
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(target);
                }

                property.serializedObject.Update();
                property.serializedObject.ApplyModifiedProperties();
            }

            EditorGUI.EndProperty();
        }

        static void RecordFallbackInfo(SerializedProperty rootProp, Component picked)
        {
            // The DontDestroyOnLoad scene (and any scene created at runtime) has no asset on disk and
            // disappears when Play mode ends. Recording it would overwrite the authored scene and
            // hierarchy path with values that cannot be resolved in Edit mode, so keep what is there.
            if (picked != null && SceneObjectRefUtils.IsInRuntimeOnlyScene(picked.gameObject))
                return;

            var lastKnownScene = rootProp.FindPropertyRelative("lastKnownScene");
            var lastKnownSceneGuid = rootProp.FindPropertyRelative("lastKnownSceneGuid");
            if (lastKnownScene != null)
            {
                if (picked != null && picked.gameObject != null)
                {
                    lastKnownScene.stringValue = picked.gameObject.scene.name;
                    
                    // Store scene GUID for rename detection
                    if (lastKnownSceneGuid != null && !string.IsNullOrEmpty(picked.gameObject.scene.path))
                    {
                        lastKnownSceneGuid.stringValue = AssetDatabase.AssetPathToGUID(picked.gameObject.scene.path);
                    }
                }
                else
                {
                    lastKnownScene.stringValue = string.Empty;
                    if (lastKnownSceneGuid != null)
                        lastKnownSceneGuid.stringValue = string.Empty;
                }
            }

            var lastKnownPath = rootProp.FindPropertyRelative("lastKnownPath");
            if (lastKnownPath != null)
                lastKnownPath.stringValue = picked != null && picked.gameObject != null
                    ? SceneObjectRefUtils.GetGameObjectPath(picked.gameObject)
                    : string.Empty;

            var lastKnownGameObjectName = rootProp.FindPropertyRelative("lastKnownGameObjectName");
            if (lastKnownGameObjectName != null)
                lastKnownGameObjectName.stringValue = picked != null && picked.gameObject != null ? picked.gameObject.name : string.Empty;

            var lastKnownComponentType = rootProp.FindPropertyRelative("lastKnownComponentType");
            if (lastKnownComponentType != null)
                lastKnownComponentType.stringValue = picked != null ? picked.GetType().AssemblyQualifiedName ?? string.Empty : string.Empty;

            var lastKnownComponentTypeName = rootProp.FindPropertyRelative("lastKnownComponentTypeName");
            if (lastKnownComponentTypeName != null)
                lastKnownComponentTypeName.stringValue = picked != null ? picked.GetType().Name : string.Empty;
        }
    }
}
#endif
