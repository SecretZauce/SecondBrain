using System;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace SecretZauce.SecondBrain.Editor
{
    /// <summary>
    /// Custom inspector for <see cref="Container"/>.
    /// Each control is a single horizontal row: label on the left, toolbar buttons on the right.
    /// Emoji icon and Label Color are shown on the same row via the shared base class helper.
    /// Supports multi-object editing: toolbar rows show a "mixed" state (no option highlighted)
    /// when selected containers disagree, and applying a value updates every selected container.
    /// The children list is only shown when a single container is selected, since each
    /// container's children are inherently distinct.
    /// </summary>
    [CustomEditor(typeof(Container))]
    [CanEditMultipleObjects]
    public class ContainerEditor : StructureInspectorBase
    {
        private static readonly string[] ChildViewLabels    = { "Foldouts", "Tabs" };
        private static readonly string[] ExpandOptionLabels = { "Expand", "Collapse", "Always" };

        private ReorderableList childrenList;
        private SerializedProperty childrenProp;

        private Container[] Containers => targets.Cast<Container>().ToArray();

        public override void OnInspectorGUI()
        {
            var container = (Container)target;
            var containers = Containers;
            serializedObject.Update();

            DrawIconColorRow();
            EditorGUILayout.Space(30);

            DrawOneLineToolbar("Container Expand", ExpandOptionLabels, (int)container.DefaultExpand,
                IsMixed(containers, c => (int)c.DefaultExpand), i =>
            {
                ApplyToAll(containers, "Change Container Expand", c => c.DefaultExpand = (DefaultExpandOption)i);
            });

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Child View / Quick Peek", EditorStyles.boldLabel);

            DrawOneLineToolbar("Preferred Child View", ChildViewLabels, (int)container.PreferredChildView,
                IsMixed(containers, c => (int)c.PreferredChildView), i =>
            {
                ApplyToAll(containers, "Change Preferred Child View", c => c.PreferredChildView = (ChildViewMode)i);
            });

            DrawOneLineToolbar("Child View Expand", ExpandOptionLabels, (int)container.ChildViewExpand,
                IsMixed(containers, c => (int)c.ChildViewExpand), i =>
            {
                ApplyToAll(containers, "Change Child View Expand", c => c.ChildViewExpand = (DefaultExpandOption)i);
            });

            EditorGUILayout.Space(6);

            if (ProFeature.Provider != null)
                DrawDisableQuickPeek(containers);
            EditorGUILayout.Space(10);

            if (containers.Length > 1)
                EditorGUILayout.HelpBox("Children list editing is not available when multiple containers are selected.", MessageType.Info);
            else
                DrawChildrenList();

            serializedObject.ApplyModifiedProperties();
        }

        private static bool IsMixed(Container[] containers, Func<Container, int> selector)
        {
            if (containers.Length < 2) return false;
            int first = selector(containers[0]);
            return containers.Any(c => selector(c) != first);
        }

        private static void ApplyToAll(Container[] containers, string undoLabel, Action<Container> apply)
        {
            foreach (var c in containers)
            {
                Undo.RecordObject(c, undoLabel);
                apply(c);
                EditorUtility.SetDirty(c);
            }
            AssetDatabase.SaveAssets();
        }

        private void DrawOneLineToolbar(string label, string[] options, int current, bool mixed, System.Action<int> onChange)
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

            // When values disagree across the selection, show no option as active; any click
            // then unambiguously applies that option to every selected container.
            int displayCurrent = mixed ? -1 : current;

            int selected;
            if (availableForControl < controlMinWidth)
            {
                // Narrow: use a Popup (dropdown)
                selected = EditorGUILayout.Popup(displayCurrent, options, GUILayout.ExpandWidth(true));
            }
            else
            {
                // Wide enough: use the tab-style toolbar
                selected = GUILayout.Toolbar(displayCurrent, options, GUILayout.MinWidth(90f));
            }

            EditorGUILayout.EndHorizontal();
            if (selected != displayCurrent)
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

        void DrawDisableQuickPeek(Container[] containers)
        {
            bool first = containers[0].DisableQuickPeek;
            bool mixed = containers.Any(c => c.DisableQuickPeek != first);

            EditorGUI.showMixedValue = mixed;
            EditorGUI.BeginChangeCheck();
            bool newVal = EditorGUILayout.Toggle(
                new GUIContent("Disable Quick Peek",
                    "When enabled, Quick Peek is disabled for this container and all its children."),
                first);
            EditorGUI.showMixedValue = false;
            if (EditorGUI.EndChangeCheck())
            {
                ApplyToAll(containers, "Toggle Disable Quick Peek", c => c.DisableQuickPeek = newVal);
            }
        }
    }
}
