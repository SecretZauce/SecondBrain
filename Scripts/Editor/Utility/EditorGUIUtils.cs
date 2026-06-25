using System;
using UnityEditor;
using UnityEngine;
using System.Reflection;
using Object = UnityEngine.Object;

namespace SecretZauce.SecondBrain.Editor
{
    public static class EditorGUIUtils
    {
        // Unity default notification fade duration is 4s; halved here.
        private const double NotificationDuration = 2.0;

        public static void ShowNotification(EditorWindow window, GUIContent content)
        {
            window?.ShowNotification(content, NotificationDuration);
        }

        public static void ShowNotification(EditorWindow window, string message)
        {
            window?.ShowNotification(new GUIContent(message), NotificationDuration);
        }


        /// <summary>
        /// Attempts to locate the main Unity Editor window via internal container windows and
        /// returns its position on screen. Uses reflection to inspect internal types and
        /// falls back to sensible defaults when not available.
        /// </summary>
        public static Rect GetMainWindowRect()
        {
            try
            {
                var asm = typeof(UnityEditor.Editor).Assembly;
                var containerType = asm.GetType("UnityEditor.ContainerWindow");
                if (containerType != null)
                {
                    var all = Resources.FindObjectsOfTypeAll(containerType);
                    if (all != null)
                    {
                        var showModeField = containerType.GetField("m_ShowMode", BindingFlags.NonPublic | BindingFlags.Instance);
                        var positionProp = containerType.GetProperty("position", BindingFlags.Public | BindingFlags.Instance);
                        foreach (var win in all)
                        {
                            if (showModeField == null || positionProp == null)
                                break;

                            var modeObj = showModeField.GetValue(win);
                            if (modeObj is 4) // 4 == main editor window (common convention)
                            {
                                var rect = (Rect)positionProp.GetValue(win, null);
                                return rect;
                            }
                        }
                    }
                }
            }
            catch
            {
                // ignore reflection failures and fall through to fallbacks
            }

            // Fallbacks: prefer the focused window if available, otherwise use display resolution
            if (EditorWindow.focusedWindow != null)
                return EditorWindow.focusedWindow.position;

            var resolution = Screen.currentResolution;
            return new Rect(0, 0, resolution.width, resolution.height);
        }
        public static void DrawObjectInspector(Object obj)
        {
            if (obj == null)
            {
                EditorGUILayout.LabelField("(null)");
                return;
            }

            EditorGUILayout.BeginVertical("box");
            
            try
            {
                var ed = UnityEditor.Editor.CreateEditor(obj);
                if (ed != null)
                {
                    try
                    {
                        // Check if this is a custom editor specifically made for this type
                        // Unity's default editors start with "UnityEditor."
                        // Odin's editors start with "Sirenix." - treat them like default editors
                        string editorFullName = ed.GetType().FullName;
                        bool isDefaultOrOdinEditor = editorFullName != null && (editorFullName.StartsWith("UnityEditor.") || 
                            editorFullName.StartsWith("Sirenix."));
                        
                        if (!isDefaultOrOdinEditor)
                        {
                            // Use the custom editor's OnInspectorGUI
                            ed.OnInspectorGUI();
                        }
                        else
                        {
                            // For default Unity editors or Odin, manually iterate through serialized properties
                            ed.serializedObject.Update();
                            
                            SerializedProperty prop = ed.serializedObject.GetIterator();
                            bool hasVisibleProperties = false;
                            if (prop.NextVisible(true))
                            {
                                do
                                {
                                    hasVisibleProperties = true;
                                    
                                    // Draw script field as disabled
                                    if (prop.propertyPath == "m_Script")
                                    {
                                        using (new EditorGUI.DisabledScope(true))
                                        {
                                            EditorGUILayout.PropertyField(prop, new GUIContent(prop.displayName), true);
                                        }
                                    }
                                    else
                                    {
                                        // Draw with explicit label and includeChildren=true
                                        EditorGUILayout.PropertyField(prop, new GUIContent(prop.displayName), true);
                                    }
                                }
                                while (prop.NextVisible(false));
                            }
                            
                            if (!hasVisibleProperties)
                            {
                                EditorGUILayout.LabelField("No serialized properties found.", EditorStyles.miniLabel);
                            }
                            
                            ed.serializedObject.ApplyModifiedProperties();
                        }
                    }
                    finally
                    {
                        Object.DestroyImmediate(ed);
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("Could not create editor.", EditorStyles.miniLabel);
                }
            }
            catch (Exception ex)
            {
                EditorGUILayout.HelpBox($"Error: {ex.Message}", MessageType.Error);
            }

            EditorGUILayout.EndVertical();
        }

        public static void ShowNotificationOnActiveView(GUIContent notif)
        {
            // First, prefer the currently focused window if it's a SceneView or the GameView.
            var focused = EditorWindow.focusedWindow;

            // GameView is internal; fetch its type via reflection.
            var gameViewType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");

            if (focused != null)
            {
                // If focused is a SceneView, show there.
                if (focused is SceneView svFocused)
                {
                    svFocused.ShowNotification(notif, NotificationDuration);
                    return;
                }

                // If focused is the GameView (internal type), show notification there.
                if (gameViewType != null && gameViewType.IsAssignableFrom(focused.GetType()))
                {
                    focused.ShowNotification(notif, NotificationDuration);
                    return;
                }
            }

            // If nothing focused, fall back to the last active SceneView if available.
            var lastSv = SceneView.lastActiveSceneView;
            if (lastSv == null)
                return;
            lastSv.ShowNotification(notif, NotificationDuration);
        }

        public static void FocusHierarchyWindowIfPresent()
        {
            var asm = typeof(UnityEditor.Editor).Assembly;
            // Try known internal names for the Hierarchy window type
            string[] candidates = new[] { "UnityEditor.SceneHierarchyWindow", "UnityEditor.HierarchyWindow", "UnityEditor.Hierarchy" };

            foreach (var name in candidates)
            {
                var type = asm.GetType(name);
                if (type == null) continue;

                var found = Resources.FindObjectsOfTypeAll(type);
                if (found is {Length: > 0})
                {
                    var win = found[0] as EditorWindow;
                    if (win == null) continue;
                    win.Focus();
                    return; // exit after focusing
                }
            }
        }

        public static void DrawSeparator()
        {
            // Draw a thin horizontal separator between the header and the list items
            // Use a slightly different color for Pro/Personal skin to match Unity editor visuals
            Rect separatorRect = EditorGUILayout.GetControlRect(false, 1f, GUILayout.ExpandWidth(true));
            Color separatorColor = EditorGUIUtility.isProSkin ? new Color(0.08f, 0.08f, 0.08f, 1f) : new Color(0.75f, 0.75f, 0.75f, 1f);
            EditorGUI.DrawRect(separatorRect, separatorColor);
            // Tiny padding for optical separation
            GUILayout.Space(2f);
        }

        public static T WithTemporaryLabelWidth<T>(Rect rect, float reservedSpace, Func<T> draw)
        {
            float prevLabelWidth = EditorGUIUtility.labelWidth;
            float desiredLabelWidth = Mathf.Max(prevLabelWidth, rect.width - reservedSpace);
            try
            {
                EditorGUIUtility.labelWidth = desiredLabelWidth;
                return draw();
            }
            finally
            {
                EditorGUIUtility.labelWidth = prevLabelWidth;
            }
        }

        public static void EnterFolderInProjectWindow(Object folder)
        {
            if (folder == null) return;
            var projectBrowserType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.ProjectBrowser");
            if (projectBrowserType == null) return;
            var browsers = Resources.FindObjectsOfTypeAll(projectBrowserType);
            if (browsers.Length == 0)
            {
                EditorApplication.ExecuteMenuItem("Window/General/Project");
                // Defer: the newly-opened browser hasn't run its first Update/OnGUI yet,
                // so calling ShowFolderContents on it immediately causes a null ref inside
                // Unity's internal ProjectBrowser initialization code.
                var capturedFolder = folder;
                EditorApplication.delayCall += () => EnterFolderInProjectWindow(capturedFolder);
                return;
            }
            var method = projectBrowserType.GetMethod("ShowFolderContents",
                BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var browser in browsers)
            {
                try { method?.Invoke(browser, new object[] { folder.GetInstanceID(), true }); }
                catch { }
            }
        }
    }
}