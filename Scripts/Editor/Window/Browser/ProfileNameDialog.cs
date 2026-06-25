using UnityEditor;
using UnityEngine;

namespace SecretZauce.SecondBrain.Editor
{
    internal class ProfileNameDialog : EditorWindow
    {
        string inputName = "Profile";
        string confirmLabel = "Create";
        bool confirmed;
        bool focusField = true;
        const string FieldControlName = "ProfileNameField";

        // Written by OnDisable (while the instance is still alive) and read by Show after ShowModal returns.
        static string s_Result;

        public static string Show(string title, string defaultName = "Profile", string confirmButtonLabel = "Create")
        {
            s_Result = null;
            var dlg = CreateInstance<ProfileNameDialog>();
            dlg.titleContent = new GUIContent(title);
            dlg.inputName    = defaultName;
            dlg.confirmLabel = confirmButtonLabel;
            dlg.minSize      = new Vector2(300, 90);
            dlg.maxSize      = new Vector2(400, 90);
            dlg.ShowModal();
            return s_Result;
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
                if (GUILayout.Button(confirmLabel, GUILayout.Width(70))) { confirmed = true; Close(); }
            GUILayout.Space(10);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUILayout.Space(10);
            GUILayout.EndHorizontal();
        }

        void OnDisable()
        {
            if (confirmed && !string.IsNullOrWhiteSpace(inputName))
                s_Result = inputName.Trim();
        }
    }
}
