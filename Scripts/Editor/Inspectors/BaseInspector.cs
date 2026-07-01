using UnityEditor;

namespace SecretZauce.SecondBrain.Editor
{
    [CustomEditor(typeof(Base))]
    [CanEditMultipleObjects]
    public class BaseInspector : StructureInspectorBase
    {
#if SECOND_BRAIN_PRO
        SceneLinkGUIBase sceneLinkGUI;
        void OnEnable()
        {
            // Scene Link is inherently a per-object setting (it links one workspace to one
            // scene), so the picker GUI only supports a single target.
            if (targets.Length == 1)
                sceneLinkGUI = ProFeature.Provider.CreateSceneLinkGUI(this);
        }
#endif

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Shared appearance row (emoji + color)
            DrawIconColorRow();

            EditorGUILayout.Space(30);
            EditorGUILayout.LabelField("Scene Link", EditorStyles.boldLabel);
#if SECOND_BRAIN_PRO
            if (!BrowserSettings.EnableSceneLinking)
            {
                EditorGUILayout.HelpBox("Scene Linking is globally disabled. Enable it in Browser Settings (⚙).", MessageType.Warning);
                return;
            }
            if (targets.Length > 1)
                EditorGUILayout.HelpBox("Scene Link editing is not available when multiple objects are selected.", MessageType.Info);
            else
                sceneLinkGUI.Draw();
#else
            EditorGUILayout.HelpBox("Scene Linking is a PRO version feature.", MessageType.Info);
#endif
        }
    }
}

