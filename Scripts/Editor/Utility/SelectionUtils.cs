using System;
using System.Collections.Generic;
using UnityEditor;
using Object = UnityEngine.Object;

namespace SecretZauce.SecondBrain.Editor
{
    public static class SelectionUtils
    {
        public static IEnumerable<Object> objects => Selection.objects;
        public static void RegisterOnSelectionChange(Action callback)
        {
            Selection.selectionChanged += callback;
        }

        public static void UnregisterOnSelectionChange(Action callback)
        {
            Selection.selectionChanged -= callback;
        }
        
        public static void SetActiveObject(Object target)
        {
            Selection.activeObject = target;
            // Debug.Log("Active object set to: " + target);
        }

        public static void SetActiveAndPing(Object target)
        {
            Selection.activeObject = target;
            EditorGUIUtility.PingObject(target);
            //Debug.Log("Active object set to: " + target);
        }

        public static void SetObjects(Object[] objs)
        {
            Selection.objects = objs;
            //Debug.Log("objects set to: " + string.Join(", ", objs.Select(o => o.name)));
        }
    }
}