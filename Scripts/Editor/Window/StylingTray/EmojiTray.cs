using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace SecretZauce.SecondBrain.Editor
{
    [System.Serializable]
    internal class EmojiEntry
    {
        public string emoji;
        public string name;
    }

    [System.Serializable]
    internal class EmojiCategory
    {
        public string id;
        public string label;
        public List<EmojiEntry> emojis;
    }

    [System.Serializable]
    internal class EmojiDatabase
    {
        public List<EmojiCategory> categories;
    }

    public class EmojiTray : PickerTrayBase
    {
        private const string EmojiResourcePath = "unity_icons_emojis";
        private const string EditorIconResourcePath = "unity_editor_icons";
        private const string EmojiPrefsKey = "EmojiTray_LastUsed_Emojis";
        private const string EditorIconPrefsKey = "EmojiTray_LastUsed_EditorIcons";
        private const int MaxHistory = 30;

        // Loaded from JSON
        private static EmojiDatabase _emojiDb;
        private static string[] _emojiTabLabels;
        private static EmojiDatabase _editorIconDb;
        private static string[] _editorIconTabLabels;

        // Top-level mode: 0 = Emojis, 1 = Editor Icons
        int selectedMode = 0;
        int selectedEmojiTab = 0;
        int selectedEditorIconTab = 0;
        SearchBar _emojiSearchBar = new SearchBar();
        SearchBar _editorIconSearchBar = new SearchBar();
        Vector2 scrollPos;
        const int EmojiSize = 32;
        const int IconSize = 32;
        const int Padding = 2;
        const int GridColumns = 18;

        private static readonly string[] ModeLabels = { "😀 Emojis", "🎨 Editor Icons" };

        private IHasEmoji _target;
        private List<IHasEmoji> _targets;

        static GUIStyle _emojiButtonStyle;
        static GUIStyle _iconButtonStyle;

        // Show the tray and assign emoji to a target object implementing IHasEmoji at a specific screen position
        public static void ShowForObject(Object target, Vector2? screenPosition)
        {
            CloseCurrentTray();
            var window = CreateInstance<EmojiTray>();
            EnsureDatabasesLoaded();
            _currentTray = window;
            window._target = target as IHasEmoji;
            window._targets = null;
            var pos = screenPosition ?? new Vector2(Screen.width / 2, Screen.height / 2);
            const int width = (EmojiSize * GridColumns) + Padding * 2;
            window.ShowAsFloatingDialog(pos, width, 360);
        }


        // Show the tray and assign emoji to a list of IHasEmoji objects at a specific screen position
        public static void ShowForObjects(List<IHasEmoji> targets, Vector2? screenPosition)
        {
            CloseCurrentTray();
            EnsureDatabasesLoaded();
            var window = CreateInstance<EmojiTray>();
            _currentTray = window;
            window._targets = targets != null ? new System.Collections.Generic.List<IHasEmoji>(targets) : null;
            window._target = null;
            var pos = screenPosition ?? new Vector2(Screen.width / 2, Screen.height / 2);
            const int width = (EmojiSize * GridColumns) + Padding * 2;
            window.ShowAsFloatingDialog(pos, width, 360);
        }

        private static void EnsureDatabasesLoaded()
        {
            LoadDatabase(EmojiResourcePath, ref _emojiDb, ref _emojiTabLabels, "[EmojiTray] Emoji");
            LoadDatabase(EditorIconResourcePath, ref _editorIconDb, ref _editorIconTabLabels, "[EmojiTray] Editor Icons");
        }

        private static void LoadDatabase(string resourcePath, ref EmojiDatabase db, ref string[] tabLabels, string errorLabel)
        {
            var asset = Resources.Load<TextAsset>(resourcePath);
            if (asset == null)
            {
                Debug.LogError($"{errorLabel} database could not be loaded from Resources/{resourcePath}.json.");
                db = new EmojiDatabase { categories = new List<EmojiCategory>() };
                // Always expose the "Last Used" tab even when DB is empty
                tabLabels = new string[] { "Last Used" };
                return;
            }

            db = JsonUtility.FromJson<EmojiDatabase>(asset.text);
            if (db.categories == null)
                db.categories = new List<EmojiCategory>();

            // Prepend a special "Last Used" tab at index 0, then category labels
            tabLabels = new string[db.categories.Count + 1];
            tabLabels[0] = "Last Used";
            for (int i = 0; i < db.categories.Count; i++)
                tabLabels[i + 1] = db.categories[i].label;
        }

        void OnGUI()
        {
            _emojiButtonStyle = new GUIStyle
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 24,
                normal = { textColor = EditorGUIUtility.isProSkin ? Color.white : Color.black }
            };
            _iconButtonStyle = new GUIStyle(GUIStyle.none)
            {
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(2, 2, 2, 2)
            };

            // Top-level mode selector: Emojis | Editor Icons
            int newMode = GUILayout.Toolbar(selectedMode, ModeLabels);
            if (newMode != selectedMode)
            {
                selectedMode = newMode;
                scrollPos = Vector2.zero;
            }

            GUILayout.Space(4);

            if (selectedMode == 0)
            {
                // ── Emoji mode ────────────────────────────────────────────────
                if (_emojiDb == null || _emojiDb.categories.Count == 0)
                {
                    EditorGUILayout.HelpBox("Emoji database could not be loaded.", MessageType.Error);
                    return;
                }
                // Search (above tabs)
                _emojiSearchBar.Draw();
                GUILayout.Space(4);

                if (!_emojiSearchBar.IsSearching)
                {
                    DrawCategoryTabs(_emojiTabLabels, ref selectedEmojiTab);
                    GUILayout.Space(4);
                }

                DrawEmojiGrid();
            }
            else
            {
                if (_editorIconDb == null || _editorIconDb.categories.Count == 0)
                {
                    EditorGUILayout.HelpBox("Editor Icons database could not be loaded.", MessageType.Error);
                    return;
                }
                // Search (above tabs)
                _editorIconSearchBar.Draw();
                GUILayout.Space(4);

                if (!_editorIconSearchBar.IsSearching)
                {
                    DrawCategoryTabs(_editorIconTabLabels, ref selectedEditorIconTab);
                    GUILayout.Space(4);
                }

                DrawEditorIconGrid();
            }
        }

        void DrawCategoryTabs(string[] labels, ref int selectedIndex)
        {
            if (labels != null && labels.Length > 0)
                selectedIndex = GUILayout.Toolbar(selectedIndex, labels);
        }

        void DrawEmojiGrid()
        {
            List<EmojiEntry> emojis = new List<EmojiEntry>();

            if (_emojiSearchBar.IsSearching)
            {
                // Aggregate across all data (Last Used + all categories)
                var last = LoadLastUsedList(EmojiPrefsKey);
                foreach (var v in last)
                {
                    if (EmojiIconUtils.IsEditorIcon(v))
                        continue;
                    emojis.Add(new EmojiEntry { emoji = v, name = string.Empty });
                }

                if (_emojiDb != null && _emojiDb.categories != null)
                {
                    foreach (var cat in _emojiDb.categories)
                        if (cat?.emojis != null)
                            foreach (var e in cat.emojis)
                                emojis.Add(e);
                }

                // Deduplicate by emoji text
                var seen = new HashSet<string>();
                var dedup = new List<EmojiEntry>();
                foreach (var e in emojis)
                {
                    if (e == null || string.IsNullOrEmpty(e.emoji))
                        continue;
                    if (seen.Add(e.emoji))
                        dedup.Add(e);
                }

                var q = _emojiSearchBar.GetSearchKeyword().ToLowerInvariant();
                emojis = dedup.FindAll(e =>
                    (!string.IsNullOrEmpty(e.name) && e.name.ToLowerInvariant().Contains(q)) ||
                    (!string.IsNullOrEmpty(e.emoji) && e.emoji.ToLowerInvariant().Contains(q)));
            }
            else
            {
                // No search: render selected tab
                if (selectedEmojiTab == 0)
                {
                    var last = LoadLastUsedList(EmojiPrefsKey);
                    foreach (var v in last)
                    {
                        if (EmojiIconUtils.IsEditorIcon(v))
                            continue;
                        emojis.Add(new EmojiEntry { emoji = v, name = string.Empty });
                    }
                }
                else
                {
                    var category = _emojiDb.categories[selectedEmojiTab - 1];
                    if (category?.emojis != null)
                        emojis.AddRange(category.emojis);
                }
            }

            int columns = GridColumns;
            int rows = Mathf.CeilToInt((float)Mathf.Max(1, emojis.Count) / columns);
            var totalWidth = (columns * EmojiSize) + ((columns - 1) * Padding);

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Width(totalWidth), GUILayout.ExpandHeight(true));

            for (int row = 0; row < rows; row++)
            {
                EditorGUILayout.BeginHorizontal(GUILayout.Height(EmojiSize + Padding));

                for (int col = 0; col < columns; col++)
                {
                    int idx = row * columns + col;
                    if (idx < emojis.Count)
                    {
                        var entry = emojis[idx];
                        var content = new GUIContent(entry.emoji, entry.name);

                        var buttonRect = GUILayoutUtility.GetRect(EmojiSize, EmojiSize,
                            GUILayout.Width(EmojiSize), GUILayout.Height(EmojiSize));

                        // Draw hover background
                        if (buttonRect.Contains(Event.current.mousePosition))
                        {
                            EditorGUI.DrawRect(buttonRect, new Color(0.3f, 0.5f, 0.8f, 0.2f));
                        }

                        // Draw emoji as an actual button to use built-in IMGUI click handling.
                        if (GUI.Button(buttonRect, content, _emojiButtonStyle))
                        {
                            AddToLastUsed(EmojiPrefsKey, entry.emoji);
                            ApplyIcon(entry.emoji);
                        }
                    }
                    else
                    {
                        GUILayout.Space(EmojiSize);
                    }
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        void DrawEditorIconGrid()
        {
            List<EmojiEntry> iconEntries = new List<EmojiEntry>();

            if (_editorIconSearchBar.IsSearching)
            {
                // Aggregate last used editor icons + all categories
                var last = LoadLastUsedList(EditorIconPrefsKey);
                foreach (var v in last)
                {
                    if (!EmojiIconUtils.IsEditorIcon(v))
                        continue;
                    var path = EmojiIconUtils.GetIconPath(v);
                    iconEntries.Add(new EmojiEntry { emoji = path, name = string.Empty });
                }

                if (_editorIconDb != null && _editorIconDb.categories != null)
                {
                    foreach (var cat in _editorIconDb.categories)
                        if (cat?.emojis != null)
                            foreach (var e in cat.emojis)
                                iconEntries.Add(e);
                }

                // Deduplicate by icon path
                var seen = new HashSet<string>();
                var dedup = new List<EmojiEntry>();
                foreach (var e in iconEntries)
                {
                    if (e == null || string.IsNullOrEmpty(e.emoji))
                        continue;
                    if (seen.Add(e.emoji))
                        dedup.Add(e);
                }

                var q = _editorIconSearchBar.GetSearchKeyword().ToLowerInvariant();
                iconEntries = dedup.FindAll(e =>
                    (!string.IsNullOrEmpty(e.name) && e.name.ToLowerInvariant().Contains(q)) ||
                    (!string.IsNullOrEmpty(e.emoji) && e.emoji.ToLowerInvariant().Contains(q)));
            }
            else
            {
                if (selectedEditorIconTab == 0)
                {
                    var last = LoadLastUsedList(EditorIconPrefsKey);
                    foreach (var v in last)
                    {
                        if (!EmojiIconUtils.IsEditorIcon(v))
                            continue;
                        var path = EmojiIconUtils.GetIconPath(v);
                        iconEntries.Add(new EmojiEntry { emoji = path, name = string.Empty });
                    }
                }
                else
                {
                    var category = _editorIconDb.categories[selectedEditorIconTab - 1];
                    if (category?.emojis != null)
                        iconEntries.AddRange(category.emojis);
                }
            }

            int columns = GridColumns;
            int rows = Mathf.CeilToInt((float)Mathf.Max(1, iconEntries.Count) / columns);
            var totalWidth = (columns * IconSize) + ((columns - 1) * Padding);

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Width(totalWidth), GUILayout.ExpandHeight(true));

            for (int row = 0; row < rows; row++)
            {
                EditorGUILayout.BeginHorizontal(GUILayout.Height(IconSize + Padding));

                for (int col = 0; col < columns; col++)
                {
                    int idx = row * columns + col;
                    if (idx < iconEntries.Count)
                    {
                        var entry = iconEntries[idx];
                        var iconTex = EditorGUIUtility.IconContent(entry.emoji).image;
                        var content = iconTex != null
                            ? new GUIContent(iconTex, entry.name)
                            : new GUIContent("?", entry.name);

                        var buttonRect = GUILayoutUtility.GetRect(IconSize, IconSize,
                            GUILayout.Width(IconSize), GUILayout.Height(IconSize));

                        // Draw hover background
                        if (buttonRect.Contains(Event.current.mousePosition))
                        {
                            EditorGUI.DrawRect(buttonRect, new Color(0.3f, 0.5f, 0.8f, 0.2f));
                        }

                        if (GUI.Button(buttonRect, content, _iconButtonStyle))
                        {
                            var stored = EmojiIconUtils.ToStoredValue(entry.emoji);
                            AddToLastUsed(EditorIconPrefsKey, stored);
                            ApplyIcon(stored);
                        }
                    }
                    else
                    {
                        GUILayout.Space(IconSize);
                    }
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        [System.Serializable]
        private class StringListWrapper { public List<string> items; }

        private static List<string> LoadLastUsedList(string key)
        {
            var json = EditorPrefs.GetString(key, string.Empty);
            if (string.IsNullOrEmpty(json))
                return new List<string>();
            try
            {
                var w = JsonUtility.FromJson<StringListWrapper>(json);
                return w != null && w.items != null ? new List<string>(w.items) : new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }

        private static void SaveLastUsedList(string key, List<string> values)
        {
            var w = new StringListWrapper { items = values };
            var json = JsonUtility.ToJson(w);
            EditorPrefs.SetString(key, json);
        }

        private static void AddToLastUsed(string key, string value)
        {
            if (string.IsNullOrEmpty(value))
                return;
            var list = LoadLastUsedList(key);
            // remove any existing occurrences and add to front
            list.RemoveAll(s => s == value);
            list.Insert(0, value);
            if (list.Count > MaxHistory)
                list.RemoveRange(MaxHistory, list.Count - MaxHistory);
            SaveLastUsedList(key, list);
        }

        private void ApplyIcon(string iconValue)
        {
            // Record the selection in last-used history (emoji vs editor icon)
            if (EmojiIconUtils.IsEditorIcon(iconValue))
                AddToLastUsed(EditorIconPrefsKey, iconValue);
            else
                AddToLastUsed(EmojiPrefsKey, iconValue);

            if (_targets != null && _targets.Count > 0)
            {
                foreach (var t in _targets)
                {
                    Undo.RecordObject(t as Object, "Set Icon");
                    t.EmojiIcon = iconValue;
                    EditorUtility.SetDirty(t as UnityEngine.Object);
                }
                AssetDatabase.SaveAssets();
                Close();
            }
            else if (_target != null)
            {
                Undo.RecordObject(_target as Object, "Set Icon");
                _target.EmojiIcon = iconValue;
                EditorUtility.SetDirty(_target as Object);
                AssetDatabase.SaveAssets();
                Close();
            }
            else
            {
                // Strip the internal prefix so the clipboard gets a human-readable value:
                // editor icon paths copy as their bare path; emojis copy as-is.
                EditorGUIUtility.systemCopyBuffer = EmojiIconUtils.IsEditorIcon(iconValue)
                    ? EmojiIconUtils.GetIconPath(iconValue)
                    : iconValue;
            }
        }
    }
}
