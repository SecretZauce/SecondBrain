using System.Linq;
using SecretZauce.SecondBrain;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace SecretZauce.SecondBrain.Editor
{
    [CustomEditor(typeof(SecondBrainCore))]
    internal class SecondBrainCoreEditor : UnityEditor.Editor
    {
        SerializedProperty defaultProfileNameProp;
        SerializedProperty profilesProp;
        ReorderableList    profileList;

        static GUIStyle s_DeleteBtnStyle;

        void OnEnable()
        {
            defaultProfileNameProp = serializedObject.FindProperty("defaultProfileName");
            profilesProp           = serializedObject.FindProperty("profiles");
            BuildProfileList();
        }

        void BuildProfileList()
        {
            profileList = new ReorderableList(serializedObject, profilesProp,
                draggable: true, displayHeader: true,
                displayAddButton: true, displayRemoveButton: false);

            profileList.drawHeaderCallback = rect =>
                EditorGUI.LabelField(rect, "Profiles", EditorStyles.boldLabel);

            profileList.elementHeightCallback = _ => EditorGUIUtility.singleLineHeight + 4f;

            profileList.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                s_DeleteBtnStyle ??= new GUIStyle(GUIStyle.none)
                {
                    alignment = TextAnchor.MiddleCenter,
                    padding   = new RectOffset(1, 1, 1, 1),
                };

                var element = profilesProp.GetArrayElementAtIndex(index);
                rect.y      += 2f;
                rect.height  = EditorGUIUtility.singleLineHeight;

                const float deleteBtnW = 18f;
                const float gap        = 4f;
                Rect fieldRect  = new Rect(rect.x, rect.y, rect.width - deleteBtnW - gap, rect.height);
                Rect deleteRect = new Rect(rect.xMax - deleteBtnW, rect.y, deleteBtnW, rect.height);

                EditorGUI.ObjectField(fieldRect, element, typeof(Profile), GUIContent.none);

                var deleteIcon = EditorGUIUtility.IconContent("d_TreeEditor.Trash");
                deleteIcon.tooltip = "Delete profile asset from disk";
                if (GUI.Button(deleteRect, deleteIcon, s_DeleteBtnStyle))
                    DeleteProfileAt(index, element.objectReferenceValue as Profile);
            };

            profileList.onAddDropdownCallback = (buttonRect, _) =>
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("Create Editor-Only Profile…"), false, () =>
                {
                    string name = ProfileNameDialog.Show("New Editor-Only Profile", "Profile");
                    if (string.IsNullOrEmpty(name)) return;
                    ProfileManager.CreateNewProfile(DataStorageLocation.EditorResources, name);
                    serializedObject.Update();
                    Repaint();
                });
                menu.AddItem(new GUIContent("Create In-Build Profile…"), false, () =>
                {
                    string name = ProfileNameDialog.Show("New In-Build Profile", "Profile");
                    if (string.IsNullOrEmpty(name)) return;
                    ProfileManager.CreateNewProfile(DataStorageLocation.Resources, name);
                    serializedObject.Update();
                    Repaint();
                });
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("Add Existing…"), false, () =>
                {
                    profilesProp.arraySize++;
                    serializedObject.ApplyModifiedProperties();
                });
                menu.DropDown(buttonRect);
            };
        }

        void DeleteProfileAt(int index, Profile profile)
        {
            string path = profile != null ? AssetDatabase.GetAssetPath(profile) : null;
            bool hasAsset = !string.IsNullOrEmpty(path);

            bool confirmed = !hasAsset || EditorUtility.DisplayDialog(
                "Delete Profile",
                $"Permanently delete '{profile!.name}' from disk?\n\nThis cannot be undone.",
                "Delete", "Cancel");

            if (!confirmed) return;

            var element = profilesProp.GetArrayElementAtIndex(index);
            if (element.objectReferenceValue != null)
                element.objectReferenceValue = null;
            profilesProp.DeleteArrayElementAtIndex(index);
            serializedObject.ApplyModifiedProperties();

            if (hasAsset)
                AssetDatabase.DeleteAsset(path);

            GUIUtility.ExitGUI();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Build Settings", EditorStyles.boldLabel);
            DrawDefaultProfileDropdown();

            EditorGUILayout.Space(8);
            profileList.DoLayoutList();

            serializedObject.ApplyModifiedProperties();
        }

        void DrawDefaultProfileDropdown()
        {
            var core          = (SecondBrainCore)target;
            var validProfiles = core.Profiles.Where(p => p != null).ToList();

            if (validProfiles.Count == 0)
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.Popup(
                        new GUIContent("Default Profile", "No profiles registered yet."),
                        0, new[] { "— none —" });
                return;
            }

            // Show all profiles; tag editor-only ones so the user knows they won't work in builds.
            string[] displayNames = validProfiles.Select(p =>
            {
                bool inBuild = Profile.GetCurrentLocation(p) == DataStorageLocation.Resources;
                return inBuild ? p.name : $"{p.name}  [Editor Only]";
            }).ToArray();

            string currentName  = defaultProfileNameProp.stringValue;
            int    currentIndex = validProfiles.FindIndex(p => p.name == currentName);
            if (currentIndex < 0) currentIndex = 0;

            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUILayout.Popup(
                new GUIContent("Default Profile",
                    "Profile loaded in player builds via Resources.Load. " +
                    "Must have 'Build' location to be accessible at runtime."),
                currentIndex, displayNames);
            if (EditorGUI.EndChangeCheck())
                defaultProfileNameProp.stringValue = validProfiles[newIndex].name;

            // Warn if the selected profile is editor-only and offer a one-click fix.
            var chosen = validProfiles[currentIndex];
            if (chosen != null && Profile.GetCurrentLocation(chosen) != DataStorageLocation.Resources)
            {
                EditorGUILayout.HelpBox(
                    $"'{chosen.name}' is stored in Resources/Editor/ and will not be available in builds. " +
                    "Move it to 'Build' location or choose a different profile.",
                    MessageType.Warning);

                if (GUILayout.Button("Move to Build Location", EditorStyles.miniButton))
                {
                    if (ProfileManager.MoveProfileToLocation(chosen, DataStorageLocation.Resources))
                        Repaint();
                }
            }
        }
    }
}
