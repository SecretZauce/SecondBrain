#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace SecretZauce.SecondBrain.Editor
{
    [CustomPropertyDrawer(typeof(SceneObject))]
    public class SceneObjectDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // Ensure serialized data is up-to-date
            property.serializedObject.Update();

            // Get the current serialized value for the inspector display
            SerializedProperty idProp = property.FindPropertyRelative("globalId");

            // Show object picker when the object resolves; otherwise display fallback name label
            var lastKnownPath = property.FindPropertyRelative("lastKnownPath")?.stringValue;
            var lastKnownSceneProp = property.FindPropertyRelative("lastKnownScene");
            var lastKnownSceneGuidProp = property.FindPropertyRelative("lastKnownSceneGuid");
            var lastKnownScene = lastKnownSceneProp?.stringValue;
            var lastKnownSceneGuid = lastKnownSceneGuidProp?.stringValue;
            var lastKnownName = property.FindPropertyRelative("lastKnownName")?.stringValue;

            // Resolve with the path fallback, not the bare GID, so objects whose GlobalObjectId
            // stops resolving in Play mode — prefab instances, and anything moved to the
            // DontDestroyOnLoad scene — are still found instead of reported as Missing.
            Object current = null;
            if (idProp != null && !string.IsNullOrEmpty(idProp.stringValue))
                current = SceneObjectMap.Resolve(idProp.stringValue, lastKnownPath, lastKnownSceneGuid, lastKnownScene);

            EditorGUI.BeginProperty(position, label, property);
            Rect fieldRect = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

            // Reserve a small area on the right for scene + status (no path)
            float infoWidth = Mathf.Min(180f, fieldRect.width * 0.35f);
            float spacing = 4f;
            Rect objectRect = new Rect(fieldRect.x, fieldRect.y, fieldRect.width - infoWidth - spacing, fieldRect.height);
            Rect infoRect = new Rect(objectRect.xMax + spacing, fieldRect.y, infoWidth, fieldRect.height);

            GameObject resolved = current as GameObject;

            // Auto-update scene name if the scene was renamed (GUID matches but name differs)
            bool sceneWasRenamed = false;
            if (resolved != null && !string.IsNullOrEmpty(lastKnownSceneGuid))
            {
                var currentScenePath = resolved.scene.path;
                if (!string.IsNullOrEmpty(currentScenePath))
                {
                    var currentSceneGuid = AssetDatabase.AssetPathToGUID(currentScenePath);
                    if (currentSceneGuid == lastKnownSceneGuid && resolved.scene.name != lastKnownScene)
                    {
                        sceneWasRenamed = true;
                        lastKnownScene = resolved.scene.name;
                    }
                }
            }
            // Fallback: if no GUID stored, try to resolve current scene name from stored scene name
            else if (resolved == null && !string.IsNullOrEmpty(lastKnownSceneGuid) && string.IsNullOrEmpty(lastKnownScene))
            {
                var scenePath = AssetDatabase.GUIDToAssetPath(lastKnownSceneGuid);
                if (!string.IsNullOrEmpty(scenePath))
                {
                    lastKnownScene = System.IO.Path.GetFileNameWithoutExtension(scenePath);
                }
            }

            // If we have a resolved object, render an ObjectField so user can inspect/reassign it.
            // If not resolved, render a readonly label with the last known name.
            GameObject displayed;
            bool objectChanged = false;
            if (resolved != null)
            {
                EditorGUI.BeginChangeCheck();
                displayed = EditorGUI.ObjectField(objectRect, resolved, typeof(GameObject), true) as GameObject;
                objectChanged = EditorGUI.EndChangeCheck();
            }
            else
            {
                string objectLabel = string.IsNullOrEmpty(lastKnownName) ? "-" : lastKnownName;
                EditorGUI.LabelField(objectRect, objectLabel);
                displayed = null;
            }

            var isPickedMismatched = false;
            // An object that has been moved to DontDestroyOnLoad at runtime always looks
            // "mismatched" — its scene and hierarchy path are both runtime-only values. Treating
            // that as a mismatch rewrote the ref's authored metadata with the pseudo-scene, which
            // then read as an unloaded scene back in Edit mode.
            if (displayed != null && !SceneObjectRefUtils.IsInRuntimeOnlyScene(displayed))
            {
                isPickedMismatched = (lastKnownName != null && lastKnownName != displayed.name)
                    || (lastKnownPath != null && lastKnownPath != SceneObjectRefUtils.GetGameObjectPath(displayed));

                // Only flag scene mismatch if GUID also differs (not just name)
                if (!string.IsNullOrEmpty(lastKnownSceneGuid))
                {
                    var currentScenePath = displayed.scene.path;
                    if (!string.IsNullOrEmpty(currentScenePath))
                    {
                        var currentSceneGuid = AssetDatabase.AssetPathToGUID(currentScenePath);
                        if (currentSceneGuid != lastKnownSceneGuid)
                            isPickedMismatched = true;
                    }
                }
                else if (lastKnownScene != null && lastKnownScene != displayed.scene.name)
                {
                    // Fallback: if no GUID stored, check by name
                    isPickedMismatched = true;
                }
            }

            // Compute inline info text and status (scene only, no path)
            string status = "None";
            if (idProp != null && !string.IsNullOrEmpty(idProp.stringValue))
            {
                status = (current != null) ? "Found" : "Missing";
            }
            else if (resolved != null)
            {
                status = "Assigned";
            }

            string scenePart = string.IsNullOrEmpty(lastKnownScene) ? "-" : lastKnownScene;
            string displayText = scenePart;
            
            // Draw a small colored dot (square) to the left of the scene name for Found/Missing
            float dotSize = 10f;
            Rect dotRect = new Rect(infoRect.x, infoRect.y + (infoRect.height - dotSize) / 2f, dotSize, dotSize);
            
            // Reserve space for a small 'Go' button on the right when Missing
            float goButtonWidth = 36f;
            Rect goRect = new Rect(infoRect.xMax - goButtonWidth, infoRect.y, goButtonWidth, infoRect.height);
            
            // Compute text rect between dot and optional button
            Rect textRect = new Rect(dotRect.xMax + 6f, infoRect.y, goRect.x - (dotRect.xMax + 6f), infoRect.height);

            // Draw a circular status indicator (filled disc) instead of a square
            if (status == "Found" || status == "Missing")
            {
                var color = (status == "Found") ? Color.green : Color.red;
                // Use Handles in GUI mode to draw a filled circle
                Handles.BeginGUI();
                var prevColor = Handles.color;
                Handles.color = color;
                Vector3 center = new Vector3(dotRect.x + dotRect.width * 0.5f, dotRect.y + dotRect.height * 0.5f, 0f);
                float radius = Mathf.Min(dotRect.width, dotRect.height) * 0.5f;
                Handles.DrawSolidDisc(center, Vector3.forward, radius);
                Handles.color = prevColor;
                Handles.EndGUI();
            }

            GUIStyle style = EditorStyles.miniLabel;
            EditorGUI.LabelField(textRect, displayText, style);

            // If Missing, draw a small 'Go' button to attempt opening the scene and selecting the object
            if (status == "Missing")
            {
                if (GUI.Button(goRect, "Go", EditorStyles.miniButton))
                {
                    // Try open scene and select the object using available metadata
                    SceneObjectRefUtils.GoToSceneObject(idProp?.stringValue, lastKnownScene, lastKnownSceneGuid, lastKnownPath);
                }
            }

            // If the live object was changed by the user or its metadata doesn't match, update stored data.
            // Also update if scene was renamed (GUID matches but name differs).
            if (objectChanged || isPickedMismatched || sceneWasRenamed)
            {
                var newPicked = displayed; // may be null if user cleared the field
                string newGid = string.Empty;
                if (objectChanged && newPicked != null)
                {
                    var gid = GlobalObjectId.GetGlobalObjectIdSlow(newPicked);
                    newGid = gid.ToString();
                }

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
                        Debug.LogWarning($"SceneObjectDrawer: Could not find property path '{property.propertyPath}' on target={target.name} ({target.GetType().Name}).");
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
                        if (sceneNameProp != null && newPicked != null)
                            sceneNameProp.stringValue = newPicked.scene.name;
                    }

                    RecordFallbackInfo(rootProp, newPicked);

                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(target);
                }

                // Finally update the current serialized object shown in the inspector
                property.serializedObject.Update();
                property.serializedObject.ApplyModifiedProperties();
            }

            EditorGUI.EndProperty();
        }

        void RecordFallbackInfo(SerializedProperty rootProp, Object picked)
        {
            var lastKnownName = rootProp.FindPropertyRelative("lastKnownName");
            if (lastKnownName != null)
                lastKnownName.stringValue = picked != null ? picked.name : string.Empty;
                    
            var pickedGo = picked as GameObject;

            // The DontDestroyOnLoad scene (and any scene created at runtime) has no asset on disk and
            // disappears when Play mode ends. Recording it would overwrite the authored scene and
            // hierarchy path with values that cannot be resolved in Edit mode, so keep what is there.
            if (SceneObjectRefUtils.IsInRuntimeOnlyScene(pickedGo))
                return;

            var lastKnownScene = rootProp.FindPropertyRelative("lastKnownScene");
            var lastKnownSceneGuid = rootProp.FindPropertyRelative("lastKnownSceneGuid");
            if (lastKnownScene != null)
            {
                // Guard: only read scene name if the cast succeeded
                if (pickedGo != null)
                {
                    lastKnownScene.stringValue = pickedGo.scene.name;

                    // Store scene GUID for rename detection
                    if (lastKnownSceneGuid != null && !string.IsNullOrEmpty(pickedGo.scene.path))
                    {
                        lastKnownSceneGuid.stringValue = AssetDatabase.AssetPathToGUID(pickedGo.scene.path);
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
            {
                // Guard: only compute path when we have a GameObject
                lastKnownPath.stringValue = pickedGo != null
                    ? SceneObjectRefUtils.GetGameObjectPath(pickedGo)
                    : string.Empty;
            }
        }
    }
}
#endif
