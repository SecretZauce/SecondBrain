using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace SecretZauce.SecondBrain.Editor
{
    /// <summary>
    /// Custom inspector for <see cref="Container"/>.
    /// Each control is a single horizontal row: label on the left, toolbar buttons on the right.
    /// Emoji icon and Label Color are shown on the same row via the shared base class helper.
    /// </summary>
    [CustomEditor(typeof(Container))]
    public class ContainerEditor : StructureInspectorBase
    {
        private static readonly string[] ChildViewLabels    = { "Tabs", "Foldouts" };
        private static readonly string[] ExpandOptionLabels = { "Expanded", "Collapsed", "Always Expand" };

        private ReorderableList childrenList;
        private SerializedProperty childrenProp;

        public override void OnInspectorGUI()
        {
            var container = (Container)target;
            serializedObject.Update();

            DrawIconColorRow();
            EditorGUILayout.Space(30);

            DrawOneLineToolbar("Container Expand", ExpandOptionLabels, (int)container.DefaultExpand, i =>
            {
                Undo.RecordObject(container, "Change Container Expand");
                container.DefaultExpand = (DefaultExpandOption)i;
                EditorUtility.SetDirty(container);
                AssetDatabase.SaveAssets();
            });

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Child View", EditorStyles.boldLabel);

            DrawOneLineToolbar("Preferred Child View", ChildViewLabels, (int)container.PreferredChildView, i =>
            {
                Undo.RecordObject(container, "Change Preferred Child View");
                container.PreferredChildView = (ChildViewMode)i;
                EditorUtility.SetDirty(container);
                AssetDatabase.SaveAssets();
            });

            DrawOneLineToolbar("Child View Expand", ExpandOptionLabels, (int)container.ChildViewExpand, i =>
            {
                Undo.RecordObject(container, "Change Child View Expand");
                container.ChildViewExpand = (DefaultExpandOption)i;
                EditorUtility.SetDirty(container);
                AssetDatabase.SaveAssets();
            });

            EditorGUILayout.Space(6);

#if SECOND_BRAIN_PRO
            DrawDisableQuickPeek(container);
#endif
            EditorGUILayout.Space(10);

            DrawChildrenList();
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawOneLineToolbar(string label, string[] options, int current, System.Action<int> onChange)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(EditorGUIUtility.labelWidth));
            int selected = GUILayout.Toolbar(current, options, GUILayout.ExpandWidth(true));
            EditorGUILayout.EndHorizontal();
            if (selected != current)
                onChange(selected);
        }

        private void EnsureChildrenList()
        {
            if (childrenProp == null)
                childrenProp = serializedObject.FindProperty("children");

            if (childrenList == null && childrenProp != null)
            {
                childrenList = new ReorderableList(serializedObject, childrenProp,
                    draggable: true, displayHeader: true,
                    displayAddButton: false, displayRemoveButton: true);

                childrenList.drawHeaderCallback = rect =>
                    EditorGUI.LabelField(rect, $"Children ({childrenProp.arraySize})", EditorStyles.boldLabel);

                childrenList.drawElementCallback = DrawChildElement;
                childrenList.elementHeight = EditorGUIUtility.singleLineHeight + 4f;
            }
        }

        private void DrawChildrenList()
        {
            EnsureChildrenList();
            if (childrenList == null)
                return;
            childrenList.DoLayoutList();
        }

        private void DrawChildElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            var element = childrenProp.GetArrayElementAtIndex(index);
            rect.y      += 2f;
            rect.height =  EditorGUIUtility.singleLineHeight;
            EditorGUI.ObjectField(rect, element, GUIContent.none);
        }

#if SECOND_BRAIN_PRO
        void DrawDisableQuickPeek(Container container)
        {
            EditorGUI.BeginChangeCheck();
            bool newVal = EditorGUILayout.Toggle(
                new GUIContent("Disable Quick Peek",
                    "When enabled, Quick Peek is disabled for this container and all its children."),
                container.DisableQuickPeek);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(container, "Toggle Disable Quick Peek");
                container.DisableQuickPeek = newVal;
                EditorUtility.SetDirty(container);
                AssetDatabase.SaveAssets();
            }
        }
#endif
    }
}
