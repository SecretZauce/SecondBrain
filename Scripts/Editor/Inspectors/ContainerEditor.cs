using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace SecretZauce.SecondBrain.Editor
{
    /// <summary>
    /// Custom inspector for <see cref="Container"/>.
    /// - PreferredChildView and DefaultExpandOption displayed as horizontal selectable tab bars.
    /// - Emoji icon and Label Color are shown on the same row; clicking them opens the
    ///   EmojiTray / ColorTray floating pickers.
    /// </summary>
    [CustomEditor(typeof(Container))]
    public class ContainerEditor : StructureInspectorBase
    {
        private static readonly string[] ChildViewLabels  = { "Tabs", "Foldouts" };
        private static readonly string[] ExpandOptionLabels = { "Expanded", "Collapsed", "Always Expand" };

        // Icon/color base sizes are computed at draw-time to respect the inspector window width
        // so controls scale when the window is resized.

        private ReorderableList childrenList;
        private SerializedProperty childrenProp;

        public override void OnInspectorGUI()
        {
            var container = (Container)target;
            serializedObject.Update();

            // Shared appearance row (emoji + color)
            DrawIconColorRow();

            EditorGUILayout.Space(30); // space below Appearance section

            DrawPreferredChildViewTabs(container);

            EditorGUILayout.Space(6);

            DrawDefaultExpandOptionTabs(container);

            EditorGUILayout.Space(6);

#if SECOND_BRAIN_PRO
            DrawDisableQuickPeek(container);
#endif
            EditorGUILayout.Space(10);

            DrawChildrenList();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawPreferredChildViewTabs(Container container)
        {
            EditorGUILayout.LabelField("Preferred Child View", EditorStyles.boldLabel, GUILayout.ExpandWidth(true));
            int current = (int)container.PreferredChildView;
            int selected = GUILayout.Toolbar(current, ChildViewLabels, GUILayout.ExpandWidth(true));
            if (selected != current)
            {
                Undo.RecordObject(container, "Change Preferred Child View");
                container.PreferredChildView = (ChildViewMode)selected;
                EditorUtility.SetDirty(container);
                AssetDatabase.SaveAssets();
            }
        }

        private void DrawDefaultExpandOptionTabs(Container container)
        {
            EditorGUILayout.LabelField("Default Expand Option", EditorStyles.boldLabel, GUILayout.ExpandWidth(true));
            int current = (int)container.DefaultExpand;
            int selected = GUILayout.Toolbar(current, ExpandOptionLabels, GUILayout.ExpandWidth(true));
            if (selected != current)
            {
                Undo.RecordObject(container, "Change Default Expand Option");
                container.DefaultExpand = (DefaultExpandOption)selected;
                EditorUtility.SetDirty(container);
                AssetDatabase.SaveAssets();
            }
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







