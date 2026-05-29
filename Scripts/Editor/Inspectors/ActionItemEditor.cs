#if SECOND_BRAIN_PRO
using UnityEditor;
using UnityEngine;

namespace SecretZauce.SecondBrain.Editor
{
    /// <summary>
    /// Custom inspector for <see cref="ActionItem"/> assets.
    /// Displays an "Execute" button at the top of the inspector so the user can
    /// run the action directly from the Inspector
    /// </summary>
    [CustomEditor(typeof(ActionItem), true)]
    public class ActionItemEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var actionItem = (ActionItem)target;

            EditorGUILayout.Space(4);

            using (new EditorGUI.DisabledScope(false))
            {
                if (GUILayout.Button("▶  Execute", GUILayout.Height(28)))
                {
                    actionItem.Execute();
                }
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(string.Empty, GUI.skin.horizontalSlider);
            EditorGUILayout.Space(2);

            DrawDefaultInspector();
        }
    }
}
#endif