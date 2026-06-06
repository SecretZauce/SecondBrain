using UnityEditor;
using UnityEngine;
namespace SecretZauce.SecondBrain.Editor
{
    internal class ProfileNameDialog : EditorWindow
    {
        string inputName = "Profile";
        bool confirmed;
        bool focusField = true;
        const string FieldControlName = "ProfileNameField";
        static ProfileNameDialog instance;
        public static string Show(string title, string defaultName = "Profile")
        {
            if (instance != null) instance.Close();
            instance = CreateInstance<ProfileNameDialog>();
            instance.titleContent = new GUIContent(title);
            instance.inputName    = defaultName;
            instance.minSize      = new Vector2(300, 90);
            instance.maxSize      = new Vector2(400, 90);
            instance.ShowModal();
            string result = instance.confirmed ? instance.inputName : null;
            return string.IsNullOrWhiteSpace(result) ? null : result.Trim();
        }
        void OnGUI()
        {
            if (Event.current.type == EventType.KeyDown)
            {
                if (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter)
                { confirmed = true; Event.current.Use(); Close(); return; }
                if (Event.current.keyCode == KeyCode.Escape)
                { Event.current.Use(); Close(); return; }
            }
            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            GUILayout.Space(10);
            GUILayout.BeginVertical();
            GUILayout.Label("Profile Name:", EditorStyles.label);
            GUILayout.Space(4);
            if (focusField) { EditorGUI.FocusTextInControl(FieldControlName); focusField = false; }
            GUI.SetNextControlName(FieldControlName);
            inputName = EditorGUILayout.TextField(inputName);
            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Cancel", GUILayout.Width(70))) Close();
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(inputName)))
                if (GUILayout.Button("Create", GUILayout.Width(70))) { confirmed = true; Close(); }
            GUILayout.Space(10);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUILayout.Space(10);
            GUILayout.EndHorizontal();
        }
        void OnDisable() { instance = null; }
    }
}
