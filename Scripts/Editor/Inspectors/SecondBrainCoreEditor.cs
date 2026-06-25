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
        static GUIStyle s_RenameBtnStyle;

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
                s_RenameBtnStyle ??= new GUIStyle(GUIStyle.none)
                {
                    alignment = TextAnchor.MiddleCenter,
                    padding   = new RectOffset(1, 1, 1, 1),
                };

                var element = profilesProp.GetArrayElementAtIndex(index);
                rect.y      += 2f;
                rect.height  = EditorGUIUtility.singleLineHeight;

                const float btnW = 18f;
                const float gap  = 4f;
                Rect fieldRect  = new Rect(rect.x, rect.y, rect.width - 2 * (btnW + gap), rect.height);
                Rect renameRect = new Rect(rect.xMax - 2 * btnW - gap, rect.y, btnW, rect.height);
                Rect deleteRect = new Rect(rect.xMax - btnW, rect.y, btnW, rect.height);

                EditorGUI.ObjectField(fieldRect, element, typeof(Profile), GUIContent.none);

                var profile = element.objectReferenceValue as Profile;

                using (new EditorGUI.DisabledScope(profile == null))
                {
                    var renameIcon = EditorGUIUtility.IconContent("d_editicon.sml");
                    renameIcon.tooltip = "Rename profile asset";
                    if (GUI.Button(renameRect, renameIcon, s_RenameBtnStyle))
                        RenameProfileAt(index, profile);
                }

                var deleteIcon = EditorGUIUtility.IconContent("d_TreeEditor.Trash");
                deleteIcon.tooltip = "Delete profile asset from disk";
                if (GUI.Button(deleteRect, deleteIcon, s_DeleteBtnStyle))
                    DeleteProfileAt(index, profile);
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

        void RenameProfileAt(int index, Profile profile)
        {
            if (profile == null) return;

            string oldName = profile.name;
            string newName = ProfileNameDialog.Show("Rename Profile", oldName, "Rename");
            if (string.IsNullOrEmpty(newName) || newName == oldName) return;

            string path  = AssetDatabase.GetAssetPath(profile);
            string error = AssetDatabase.RenameAsset(path, newName);
            if (!string.IsNullOrEmpty(error))
            {
                EditorUtility.DisplayDialog("Rename Failed", error, "OK");
                return;
            }

            // Keep defaultProfileName in sync if it referenced the old name.
            if (defaultProfileNameProp.stringValue == oldName)
            {
                defaultProfileNameProp.stringValue = newName;
                serializedObject.ApplyModifiedProperties();
            }

            AssetDatabase.SaveAssets();
            serializedObject.Update();
            Repaint();
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

            // Index 0 is always "None" (empty string → no profile loaded in builds).
            var displayNames = new System.Collections.Generic.List<string> { "— None —" };
            displayNames.AddRange(validProfiles.Select(p =>
            {
                bool inBuild = Profile.GetCurrentLocation(p) == DataStorageLocation.Resources;
                return inBuild ? p.name : $"{p.name}  [Editor Only]";
            }));

            string currentName  = defaultProfileNameProp.stringValue;
            // +1 offset because index 0 is "None"
            int profileIndex = string.IsNullOrEmpty(currentName)
                ? -1
                : validProfiles.FindIndex(p => p.name == currentName);
            int currentIndex = profileIndex < 0 ? 0 : profileIndex + 1;

            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUILayout.Popup(
                new GUIContent("Default Profile",
                    "Profile loaded in player builds via Resources.Load. " +
                    "'None' means no profile is loaded. " +
                    "Must have 'Build' location to be accessible at runtime."),
                currentIndex, displayNames.ToArray());
            if (EditorGUI.EndChangeCheck())
                defaultProfileNameProp.stringValue = newIndex == 0 ? "" : validProfiles[newIndex - 1].name;

            // Warn if a specific editor-only profile is selected and offer a one-click fix.
            if (currentIndex > 0)
            {
                var chosen = validProfiles[currentIndex - 1];
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
}
