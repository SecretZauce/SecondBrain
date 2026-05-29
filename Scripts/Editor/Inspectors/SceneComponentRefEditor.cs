#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace SecretZauce.SecondBrain.Editor
{
    [CustomEditor(typeof(SceneComponentRef))]
    public class SceneComponentRefEditor : UnityEditor.Editor
    {
        UnityEditor.Editor resolvedEditor;
        Component resolvedComponent;

        void OnDisable()
        {
            ClearResolvedEditor();
        }

        public override void OnInspectorGUI()
        {
            if (targets == null || targets.Length != 1)
            {
                DrawDefaultInspector();
                return;
            }

            var sceneComponentRef = target as SceneComponentRef;
            var resolved = SceneComponentRefUtils.Resolve(sceneComponentRef?.sceneComponent);
            if (resolved != null)
            {
                EnsureResolvedEditor(resolved);
                if (resolvedEditor != null)
                {
                    resolvedEditor.OnInspectorGUI();
                    return;
                }
            }

            DrawDefaultInspector();
        }

        void EnsureResolvedEditor(Component component)
        {
            if (resolvedComponent == component && resolvedEditor != null)
                return;

            ClearResolvedEditor();
            resolvedComponent = component;
            resolvedEditor = CreateEditor(component);
        }

        void ClearResolvedEditor()
        {
            if (resolvedEditor != null)
                DestroyImmediate(resolvedEditor);

            resolvedEditor = null;
            resolvedComponent = null;
        }
    }
}
#endif
