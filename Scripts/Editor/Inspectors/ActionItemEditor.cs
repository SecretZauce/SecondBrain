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
    [CanEditMultipleObjects]
    public class ActionItemEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.Space(4);

            string buttonLabel = targets.Length > 1 ? $"▶  Execute ({targets.Length})" : "▶  Execute";
            using (new EditorGUI.DisabledScope(false))
            {
                if (GUILayout.Button(buttonLabel, GUILayout.Height(28)))
                {
                    foreach (var t in targets)
                        (t as ActionItem)?.Execute();
                }
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(string.Empty, GUI.skin.horizontalSlider);
            EditorGUILayout.Space(2);

            DrawDefaultInspector();
        }
    }
}