using System;
using System.Collections.Generic;
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
        // Resolved lazily and reused. Resources.FindObjectsOfTypeAll walks every loaded object in
        // the project — including all scene GameObjects — so in a large scene each call is costly
        // enough to be worth avoiding, even on user-triggered paths. Cached references go null when
        // the window closes or after a domain reload, which triggers a fresh scan.
        static Object cachedMainContainerWindow;
        static PropertyInfo cachedContainerPositionProp;

        public static Rect GetMainWindowRect()
        {
            try
            {
                var asm = typeof(UnityEditor.Editor).Assembly;
                var containerType = asm.GetType("UnityEditor.ContainerWindow");
                if (containerType != null)
                {
                    if (cachedMainContainerWindow == null || cachedContainerPositionProp == null)
                    {
                        cachedMainContainerWindow = null;
                        cachedContainerPositionProp = containerType.GetProperty("position", BindingFlags.Public | BindingFlags.Instance);
                        var showModeField = containerType.GetField("m_ShowMode", BindingFlags.NonPublic | BindingFlags.Instance);

                        if (showModeField != null && cachedContainerPositionProp != null)
                        {
                            var all = Resources.FindObjectsOfTypeAll(containerType);
                            if (all != null)
                            {
                                foreach (var win in all)
                                {
                                    var modeObj = showModeField.GetValue(win);
                                    if (modeObj is 4) // 4 == main editor window (common convention)
                                    {
                                        cachedMainContainerWindow = win;
                                        break;
                                    }
                                }
                            }
                        }
                    }

                    if (cachedMainContainerWindow != null && cachedContainerPositionProp != null)
                        return (Rect)cachedContainerPositionProp.GetValue(cachedMainContainerWindow, null);
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
        // Editors used by the detail panel, keyed by target instance ID.
        //
        // DrawObjectInspector runs on every OnGUI pass. Creating an Editor and destroying it again
        // each pass is expensive — for a GameObject or prefab target it rebuilds GameObjectInspector
        // and its preview scene every frame — so instances are kept alive and reused instead.
        // The cache is capped and evicts the least recently used entry. Unity destroys the cached
        // Editors on domain reload, which the null checks below absorb.
        const int InspectorCacheCapacity = 8;
        static readonly Dictionary<int, UnityEditor.Editor> InspectorCache = new Dictionary<int, UnityEditor.Editor>();
        static readonly Dictionary<int, long> InspectorCacheLastUse = new Dictionary<int, long>();
        static readonly List<int> InspectorCacheScratch = new List<int>();
        static long inspectorCacheClock;

        static UnityEditor.Editor GetCachedEditor(Object obj)
        {
            int id = obj.GetStableInstanceId();

            if (InspectorCache.TryGetValue(id, out var cached))
            {
                if (cached != null && cached.target == obj)
                {
                    InspectorCacheLastUse[id] = ++inspectorCacheClock;
                    return cached;
                }

                DestroyCachedEditor(id);
            }

            var created = UnityEditor.Editor.CreateEditor(obj);
            if (created == null)
                return null;

            InspectorCache[id] = created;
            InspectorCacheLastUse[id] = ++inspectorCacheClock;
            TrimInspectorCache();
            return created;
        }

        static void TrimInspectorCache()
        {
            // Release entries whose Editor or target has died before evicting anything still valid.
            InspectorCacheScratch.Clear();
            foreach (var kvp in InspectorCache)
                if (kvp.Value == null || kvp.Value.target == null)
                    InspectorCacheScratch.Add(kvp.Key);

            foreach (var id in InspectorCacheScratch)
                DestroyCachedEditor(id);

            while (InspectorCache.Count > InspectorCacheCapacity)
            {
                int oldestId = 0;
                long oldestUse = long.MaxValue;
                foreach (var kvp in InspectorCacheLastUse)
                {
                    if (kvp.Value >= oldestUse)
                        continue;

                    oldestUse = kvp.Value;
                    oldestId = kvp.Key;
                }

                DestroyCachedEditor(oldestId);
            }
        }

        static void DestroyCachedEditor(int id)
        {
            if (InspectorCache.TryGetValue(id, out var editor) && editor != null)
                Object.DestroyImmediate(editor);

            InspectorCache.Remove(id);
            InspectorCacheLastUse.Remove(id);
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
                var ed = GetCachedEditor(obj);
                if (ed != null)
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

        // See the note on cachedMainContainerWindow — same reasoning, same invalidation rule.
        static EditorWindow cachedHierarchyWindow;

        public static void FocusHierarchyWindowIfPresent()
        {
            if (cachedHierarchyWindow != null)
            {
                cachedHierarchyWindow.Focus();
                return;
            }

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
                    cachedHierarchyWindow = win;
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

        /// <summary>
        /// Widens EditorGUIUtility.labelWidth for the lifetime of the scope, restoring the previous
        /// value on dispose.
        ///
        /// This replaces a callback-taking helper. Every call site sat in the TreeView row loop and
        /// passed a lambda that captured local state, so each row allocated a delegate and a closure
        /// on every IMGUI pass. A struct scope allocates nothing:
        ///
        /// <code>
        /// using (EditorGUIUtils.TemporaryLabelWidth(rect, 20f))
        ///     GUI.Label(rect, content, style);
        /// </code>
        /// </summary>
        public readonly struct LabelWidthScope : IDisposable
        {
            readonly float previousLabelWidth;

            public LabelWidthScope(Rect rect, float reservedSpace)
            {
                previousLabelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = Mathf.Max(previousLabelWidth, rect.width - reservedSpace);
            }

            public void Dispose() => EditorGUIUtility.labelWidth = previousLabelWidth;
        }

        public static LabelWidthScope TemporaryLabelWidth(Rect rect, float reservedSpace)
            => new LabelWidthScope(rect, reservedSpace);

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
                try { method?.Invoke(browser, new object[] { folder.GetStableInstanceId(), true }); }
                catch { }
            }
        }
    }
}