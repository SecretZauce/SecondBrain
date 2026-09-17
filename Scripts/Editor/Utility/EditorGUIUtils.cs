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
        // Editors used by the detail panel, keyed by target object.
        //
        // DrawObjectInspector runs on every OnGUI pass. Creating an Editor and destroying it again
        // each pass is expensive — for a GameObject or prefab target it rebuilds GameObjectInspector
        // and its preview scene every frame — so instances are kept alive and reused instead.
        // The cache is capped and evicts the least recently used entry. Unity destroys the cached
        // Editors on domain reload, which the null checks below absorb.
        const int InspectorCacheCapacity = 8;
        static readonly Dictionary<Object, UnityEditor.Editor> InspectorCache = new Dictionary<Object, UnityEditor.Editor>();
        static readonly Dictionary<Object, long> InspectorCacheLastUse = new Dictionary<Object, long>();
        static readonly List<Object> InspectorCacheScratch = new List<Object>();
        static long inspectorCacheClock;

        static UnityEditor.Editor GetCachedEditor(Object obj)
        {
            if (InspectorCache.TryGetValue(obj, out var cached))
            {
                if (cached != null && cached.target == obj)
                {
                    InspectorCacheLastUse[obj] = ++inspectorCacheClock;
                    return cached;
                }

                DestroyCachedEditor(obj);
            }

            var created = UnityEditor.Editor.CreateEditor(obj);
            if (created == null)
                return null;

            InspectorCache[obj] = created;
            InspectorCacheLastUse[obj] = ++inspectorCacheClock;
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

            foreach (var key in InspectorCacheScratch)
                DestroyCachedEditor(key);

            while (InspectorCache.Count > InspectorCacheCapacity)
            {
                Object oldestKey = null;
                long oldestUse = long.MaxValue;
                foreach (var kvp in InspectorCacheLastUse)
                {
                    if (kvp.Value >= oldestUse)
                        continue;

                    oldestUse = kvp.Value;
                    oldestKey = kvp.Key;
                }

                // Compared by reference: a destroyed target is still a valid key here, and
                // Unity's overloaded == would treat it as null.
                if (ReferenceEquals(oldestKey, null))
                    break;

                DestroyCachedEditor(oldestKey);
            }
        }

        static void DestroyCachedEditor(Object key)
        {
            if (InspectorCache.TryGetValue(key, out var editor) && editor != null)
                Object.DestroyImmediate(editor);

            InspectorCache.Remove(key);
            InspectorCacheLastUse.Remove(key);
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

        static bool s_ShowFolderContentsFailureLogged;

        static void LogShowFolderContentsFailureOnce(Exception ex)
        {
            if (s_ShowFolderContentsFailureLogged) return;
            s_ShowFolderContentsFailureLogged = true;
            if (ex is TargetInvocationException { InnerException: not null } tie) ex = tie.InnerException;
            Debug.LogWarning($"[SecondBrain] Could not open folder in the Project window (ProjectBrowser.ShowFolderContents): {ex.GetType().Name}: {ex.Message}");
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
            var isTwoColumnsMethod = projectBrowserType.GetMethod("IsTwoColumns",
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
            const BindingFlags memberFlags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
            var isLockedProperty = projectBrowserType.GetProperty("isLocked", memberFlags);
            var isLockedField = isLockedProperty == null ? projectBrowserType.GetField("m_IsLocked", memberFlags) : null;
            foreach (var browser in browsers)
            {
                // A locked Project window is pinned by the user; leave its folder alone.
                var isLocked = isLockedProperty != null ? isLockedProperty.GetValue(browser) : isLockedField?.GetValue(browser);
                if (isLocked is bool locked && locked)
                    continue;

                // ShowFolderContents logs an error and bails in one-column layout; ping the folder there instead.
                if (isTwoColumnsMethod != null && isTwoColumnsMethod.Invoke(browser, null) is bool isTwoColumns && !isTwoColumns)
                {
                    EditorGUIUtility.PingObject(folder);
                    continue;
                }

                // Unity's own internal method — it needs the real native id (int pre-migration,
                // EntityId once GetInstanceID is obsolete). Its signature isn't public API, so a
                // mismatch on some Unity version must surface rather than silently do nothing.
                try { method?.Invoke(browser, new object[] { folder.GetStableNativeId(), true }); }
                catch (Exception ex) { LogShowFolderContentsFailureOnce(ex); }
            }
        }
    }
}