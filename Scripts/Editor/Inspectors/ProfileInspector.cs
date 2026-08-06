using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace SecretZauce.SecondBrain.Editor
{
    /// <summary>
    /// Custom inspector for <see cref="Profile"/>.
    /// Free mode  : base list is read-only; a HelpBox advertises PRO.
    /// PRO mode   : full default inspector.
    /// </summary>
    [CustomEditor(typeof(Profile))]
    [CanEditMultipleObjects]
    internal class ProfileInspector : UnityEditor.Editor
    {
        SerializedProperty baseListProp;
        ReorderableList readOnlyList;

        void OnEnable()
        {
            baseListProp = serializedObject.FindProperty("baseList");
            // The read-only base list is bound to a single Profile's array; it doesn't make
            // sense to show a merged view when multiple Profiles are selected.
            if (targets.Length == 1 && ProFeature.Provider == null)
                BuildReadOnlyList();
        }

        void BuildReadOnlyList()
        {
            if (baseListProp == null) return;

            readOnlyList = new ReorderableList(
                serializedObject, baseListProp,
                draggable: false, displayHeader: true,
                displayAddButton: false, displayRemoveButton: false);

            readOnlyList.drawHeaderCallback = rect =>
                EditorGUI.LabelField(rect, $"Bases  ({baseListProp.arraySize})", EditorStyles.boldLabel);

            readOnlyList.drawElementCallback = (rect, index, _, _) =>
            {
                var element = baseListProp.GetArrayElementAtIndex(index);
                rect.y      += 2f;
                rect.height =  EditorGUIUtility.singleLineHeight;
                using (new EditorGUI.DisabledScope(true))
                    EditorGUI.ObjectField(rect, element, GUIContent.none);
            };

            readOnlyList.elementHeight = EditorGUIUtility.singleLineHeight + 4f;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            if (ProFeature.Provider != null)
            {
                DrawDefaultInspector();
                return;
            }

            EditorGUILayout.HelpBox(
                "Multiple Bases is a SecondBrain PRO feature.\n" +
                "Adding or removing Bases from this list is disabled in the free version.\n" +
                "Use the SecondBrain browser to manage your base.",
                MessageType.Info);

            EditorGUILayout.Space(6);

            if (targets.Length > 1)
            {
                EditorGUILayout.HelpBox("Base list editing is not available when multiple Profiles are selected.", MessageType.Info);
            }
            else
            {
                if (readOnlyList == null) BuildReadOnlyList();
                readOnlyList?.DoLayoutList();
            }
            serializedObject.ApplyModifiedProperties();
        }
    }
}
