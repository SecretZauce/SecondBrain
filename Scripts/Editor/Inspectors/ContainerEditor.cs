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
        private static readonly string[] ChildViewLabels    = { "Foldouts", "Tabs" };
        private static readonly string[] ExpandOptionLabels = { "Expand", "Collapse", "Always" };

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
            EditorGUILayout.LabelField("Child View / Quick Peek", EditorStyles.boldLabel);

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
            // Responsive toolbar: show a Popup when the inspector is too narrow for a tab-style toolbar
            const float controlMinWidth = 180f; // minimum width needed to display toolbar comfortably
            float viewWidth = EditorGUIUtility.currentViewWidth;
            float labelWidth = EditorGUIUtility.labelWidth;
            float availableForControl = viewWidth - labelWidth - 16f; // small padding

            EditorGUILayout.BeginHorizontal();
            // If label itself would consume most of the width, stack the label above the control to avoid overlap
            if (labelWidth >= viewWidth - 80f)
            {
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.LabelField(label);
                EditorGUILayout.BeginHorizontal();
            }

            EditorGUILayout.LabelField(label, GUILayout.Width(labelWidth));

            int selected;
            if (availableForControl < controlMinWidth)
            {
                // Narrow: use a Popup (dropdown)
                selected = EditorGUILayout.Popup(current, options, GUILayout.ExpandWidth(true));
            }
            else
            {
                // Wide enough: use the tab-style toolbar
                selected = GUILayout.Toolbar(current, options, GUILayout.MinWidth(90f));
            }

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
