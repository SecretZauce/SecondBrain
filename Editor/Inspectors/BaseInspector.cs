using UnityEditor;

namespace SecretZauce.SecondBrain.Editor
{
    [CustomEditor(typeof(Base))]
    public class BaseInspector : StructureInspectorBase
    {
#if SECOND_BRAIN_PRO
        SceneLinkGUIBase sceneLinkGUI;
        void OnEnable()
        {
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
            sceneLinkGUI.Draw();
#else
            EditorGUILayout.HelpBox("Scene Linking is a PRO version feature.", MessageType.Info);
#endif
        }
    }
}

