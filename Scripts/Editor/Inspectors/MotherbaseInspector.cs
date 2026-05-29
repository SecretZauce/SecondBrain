using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace SecretZauce.SecondBrain.Editor
{
    /// <summary>
    /// Custom inspector for <see cref="Motherbase"/>.
    /// Free mode  : base list is read-only; a HelpBox advertises PRO.
    /// PRO mode   : full default inspector.
    /// </summary>
    [CustomEditor(typeof(Motherbase))]
    internal class MotherbaseInspector : UnityEditor.Editor
    {
        SerializedProperty baseListProp;
        ReorderableList readOnlyList;

        void OnEnable()
        {
            baseListProp = serializedObject.FindProperty("baseList");
#if !SECOND_BRAIN_PRO
            BuildReadOnlyList();
#endif
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

#if SECOND_BRAIN_PRO
            DrawDefaultInspector();
#else
            EditorGUILayout.HelpBox(
                "Multiple Bases is a SecondBrain PRO feature.\n" +
                "Adding or removing Bases from this list is disabled in the free version.\n" +
                "Use the SecondBrain browser to manage your base.",
                MessageType.Info);

            EditorGUILayout.Space(6);

            if (readOnlyList == null) BuildReadOnlyList();
            readOnlyList?.DoLayoutList();
#endif
            serializedObject.ApplyModifiedProperties();
        }
    }
}


