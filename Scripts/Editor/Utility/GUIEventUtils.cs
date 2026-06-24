using UnityEditor;
using UnityEngine;

namespace SecretZauce.SecondBrain.Editor
{
    public static class GUIEventUtils
    {
        public static void OnRenameDone(bool useEvent)
        {
            EditorGUIUtility.editingTextField = false;
            GUI.FocusControl(null);
            GUIUtility.keyboardControl = 0;
            if (useEvent)
                Event.current.Use();
        }
    }
}