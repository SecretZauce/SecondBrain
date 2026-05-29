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
            Object current = null;
            if (idProp != null && !string.IsNullOrEmpty(idProp.stringValue))
                current = SceneObjectMap.ResolveComponent(idProp.stringValue);

            EditorGUI.BeginProperty(position, label, property);
            Rect fieldRect = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

            float infoWidth = Mathf.Min(180f, fieldRect.width * 0.35f);
            float spacing = 4f;
            Rect objectRect = new Rect(fieldRect.x, fieldRect.y, fieldRect.width - infoWidth - spacing, fieldRect.height);
            Rect infoRect = new Rect(objectRect.xMax + spacing, fieldRect.y, infoWidth, fieldRect.height);

            var lastKnownPath = property.FindPropertyRelative("lastKnownPath")?.stringValue;
            var lastKnownScene = property.FindPropertyRelative("lastKnownScene")?.stringValue;
            var lastKnownGameObjectName = property.FindPropertyRelative("lastKnownGameObjectName")?.stringValue;
            var lastKnownComponentType = property.FindPropertyRelative("lastKnownComponentType")?.stringValue;
            var lastKnownComponentTypeName = property.FindPropertyRelative("lastKnownComponentTypeName")?.stringValue;
            Component resolved = current as Component;

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
            if (displayed != null)
            {
                string displayedPath = displayed.gameObject != null
                    ? SceneObjectRefUtils.GetGameObjectPath(displayed.gameObject)
                    : string.Empty;
                isPickedMismatched =
                    (lastKnownScene != null && lastKnownScene != displayed.gameObject.scene.name) ||
                    (lastKnownPath != null && lastKnownPath != displayedPath) ||
                    (lastKnownGameObjectName != null && lastKnownGameObjectName != displayed.gameObject.name) ||
                    (lastKnownComponentType != null && lastKnownComponentType != displayed.GetType().AssemblyQualifiedName) ||
                    (lastKnownComponentTypeName != null && lastKnownComponentTypeName != displayed.GetType().Name);
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
                    SceneComponentRefUtils.GoToSceneComponent(idProp?.stringValue, lastKnownScene, lastKnownPath);
            }

            if (objectChanged || isPickedMismatched)
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
            var lastKnownScene = rootProp.FindPropertyRelative("lastKnownScene");
            if (lastKnownScene != null)
                lastKnownScene.stringValue = picked != null && picked.gameObject != null ? picked.gameObject.scene.name : string.Empty;

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
