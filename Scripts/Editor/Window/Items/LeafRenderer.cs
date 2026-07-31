using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SecretZauce.SecondBrain.Editor
{
    internal class LeafRenderer : RowRendererBase
    {
        static HashSet<int> s_loggedDetails = new HashSet<int>();

        // ── Static color constants to avoid per-row Color struct allocations ───
        static readonly Color s_ArrowHoverPro       = new Color(1f, 1f, 1f, 1f);
        static readonly Color s_ArrowHoverPersonal  = new Color(0f, 0f, 0f, 1f);
        static readonly Color s_ArrowNormalPro      = new Color(0.6f, 0.6f, 0.6f, 0.6f);
        static readonly Color s_ArrowNormalPersonal = new Color(0.2f, 0.2f, 0.2f, 1f);
        static readonly Color s_DimmedPro           = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        static readonly Color s_DimmedPersonal      = new Color(0.3f, 0.3f, 0.3f, 0.8f);
        static readonly Color s_MissingPro          = new Color(1f, 0.45f, 0.1f, 1f);
        static readonly Color s_MissingPersonal     = new Color(0.85f, 0.3f, 0f, 1f);
        static readonly Color s_UnloadedPro         = new Color(0.75f, 0.75f, 0.75f, 0.8f);
        static readonly Color s_UnloadedPersonal    = new Color(0.25f, 0.25f, 0.25f, 0.8f);
        static readonly Color s_FocusOnPro          = new Color(0.4f, 0.8f, 1f, 1f);
        static readonly Color s_FocusOnPersonal     = new Color(0.1f, 0.5f, 0.9f, 1f);
        static readonly Color s_FocusHoverPro       = new Color(1f, 1f, 1f, 1f);
        static readonly Color s_FocusHoverPersonal  = new Color(0f, 0f, 0f, 1f);
        static readonly Color s_FocusNormalPro      = new Color(0.6f, 0.6f, 0.6f, 0.4f);
        static readonly Color s_FocusNormalPersonal = new Color(0.2f, 0.2f, 0.2f, 0.5f);

        // ── Static style cache (Option-A invalidation) ─────────────────────────
        static bool     s_StylesValid;
        static bool     s_CacheProSkin;
        static int      s_CacheFontSize;
        static Texture s_EnterIcon;
        static Texture s_PlayIcon;
        static Texture s_FocusIcon;
        static GUIStyle s_ItemStyle;   // EditorStyles.label, MiddleLeft, configured font size
        static GUIStyle s_ArrowStyle;  // GUI.skin.label, MiddleCenter, fontSize=14 (shared by arrow/exec/play buttons)

        // Per-type asset path cache – avoids calling AssetDatabase.GetAssetPath on every repaint.
        // Keyed by instance ID; cleared when the AssetDatabase is modified.
        static readonly Dictionary<int, string> s_AssetPathCache = new Dictionary<int, string>();
        static bool s_AssetPathCacheSubscribed;

        // Scene-loaded state cache: sceneGuid → isLoaded.
        // Cleared on any scene open/close so it never goes stale.
        static readonly Dictionary<string, bool> s_SceneLoadedCache = new Dictionary<string, bool>(StringComparer.Ordinal);
        static bool s_SceneLoadedCacheSubscribed;

        static void EnsureStyles()
        {
            bool ps = EditorGUIUtility.isProSkin;
            int  fs = BrowserSettings.GetItemFontSize();
            if (s_StylesValid && s_CacheProSkin == ps && s_CacheFontSize == fs)
                return;

            s_CacheProSkin  = ps;
            s_CacheFontSize = fs;

            s_ItemStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleLeft };
            if (fs > 0) s_ItemStyle.fontSize = fs;

            s_ArrowStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize  = 10,
                fontStyle = FontStyle.Normal,
                padding = new RectOffset(2, 0, 0, 0)
            };
            s_ArrowStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f, 0.6f);
            s_ArrowStyle.hover.textColor  = Color.white;

            s_StylesValid = true;

            s_EnterIcon = IconUtils.Load("enter");
            s_PlayIcon = IconUtils.Load("play");
            s_FocusIcon = IconUtils.Load("focus");
        }

        static void EnsureAssetPathCacheSubscription()
        {
            if (s_AssetPathCacheSubscribed) return;
            s_AssetPathCacheSubscribed = true;
            EditorApplication.projectChanged += () => s_AssetPathCache.Clear();
        }

        static void EnsureSceneLoadedCacheSubscription()
        {
            if (s_SceneLoadedCacheSubscribed) return;
            s_SceneLoadedCacheSubscribed = true;
            EditorSceneManager.sceneOpened   += (_, __) => s_SceneLoadedCache.Clear();
            EditorSceneManager.sceneClosed   += _       => s_SceneLoadedCache.Clear();
            // EditorSceneManager events do not fire during Play Mode transitions; clear the
            // loaded-state cache on play mode changes so IsSceneLoaded() re-evaluates against
            // the current (play-mode or edit-mode) scene set after each transition.
            EditorApplication.playModeStateChanged += _ => s_SceneLoadedCache.Clear();
        }

        /// <summary>Returns the cached asset path for <paramref name="obj"/>, calling
        /// AssetDatabase.GetAssetPath only on the first lookup per object.</summary>
        static string GetAssetPathCached(Object obj)
        {
            int id = obj.GetInstanceID();
            if (!s_AssetPathCache.TryGetValue(id, out string path))
            {
                path = AssetDatabase.GetAssetPath(obj);
                s_AssetPathCache[id] = path;
            }
            return path;
        }

        /// <summary>
        /// Checks if a scene is currently loaded, using GUID for rename-safe detection.
        /// Falls back to name-based check for backward compatibility.
        /// Result is cached per scene GUID for the duration of the current repaint batch;
        /// the cache is invalidated whenever any scene opens or closes.
        /// </summary>
        static bool IsSceneLoaded(string sceneGuid, string sceneName)
        {
            // Prefer GUID-based lookup (stable across renames)
            if (!string.IsNullOrEmpty(sceneGuid))
            {
                if (s_SceneLoadedCache.TryGetValue(sceneGuid, out var cached))
                    return cached;

                var scenePath = AssetDatabase.GUIDToAssetPath(sceneGuid);
                if (!string.IsNullOrEmpty(scenePath))
                {
                    for (int i = 0; i < SceneManager.sceneCount; i++)
                    {
                        var loadedScene = SceneManager.GetSceneAt(i);
                        if (loadedScene.path == scenePath)
                        {
                            s_SceneLoadedCache[sceneGuid] = loadedScene.isLoaded;
                            return loadedScene.isLoaded;
                        }
                    }
                }

                s_SceneLoadedCache[sceneGuid] = false;
                return false;
            }

            // Fallback: name-based check (for backward compatibility or if GUID is missing)
            if (!string.IsNullOrEmpty(sceneName))
            {
                var scene = SceneManager.GetSceneByName(sceneName);
                return scene.isLoaded;
            }

            return false;
        }

        public LeafRenderer(TreeView tv, int[] rowPath, Object nodeObj, Texture iconTexture, Rect rowRectIn, Rect trueIndentedItemRectIn) : base(tv)
        {
            path = rowPath;
            node = nodeObj;
            name = nodeObj != null ? nodeObj.name : string.Empty;
            icon = iconTexture;
            rowRect = rowRectIn;
            trueIndentedItemRect = trueIndentedItemRectIn;
            isSelected = false;
        }

        public bool Render(bool resetIndent, ItemRenamer renamer, Color? labelColor = null, ColorDisplayStyle colorStyle = ColorDisplayStyle.FontColor)
        {
            if (node == null) 
                return false;

            // Rebuild style cache if skin or font size changed (Option-A invalidation).
            EnsureStyles();
            EnsureAssetPathCacheSubscription();
            EnsureSceneLoadedCacheSubscription();

            float arrowSize = 14f;
            float plusButtonWidth = 16f;
            float plusButtonPadding = 1f;
            float rightPeekOffset = 0f;
#if SECOND_BRAIN_PRO
            // Always keep the ">" nav arrow left of the right peek zone when QuickPeek is enabled,
            // so hovering the button never triggers QuickPeek — same approach as FoldoutHeaderRenderer.
            if (ProFeature.Provider != null && BrowserSettings.EnableQuickPeek)
                rightPeekOffset = TreeViewDragInput.QuickPeekZoneWidth;
#endif
            float arrowX = rowRect.x + 1 + rowRect.width - plusButtonWidth - plusButtonPadding - rightPeekOffset + (plusButtonWidth - arrowSize) / 2f;
            arrowRect = new Rect(arrowX, rowRect.y + (rowRect.height - arrowSize) / 2f, arrowSize, arrowSize);

            if (renamer != null && renamer.IsRenaming(this.path))
            {
                if (renamer.HandleRenamingField(this.path, this.node, this.trueIndentedItemRect, resetIndent))
                    return true;
                return false;
            }
            
            // For FontColor style, apply color to label text; other styles leave text at default
            Color? fontColor = (colorStyle == ColorDisplayStyle.FontColor) ? labelColor : null;
            if (fontColor.HasValue)
                fontColor = new Color(fontColor.Value.r, fontColor.Value.g, fontColor.Value.b, fontColor.Value.a * 0.8f);

            // For CircleDot: draw a small colored dot at the right side of the leaf row (before arrow area)
            if (labelColor.HasValue && colorStyle == ColorDisplayStyle.CircleDot)
            {
                float dotRadius = 3.5f;
                float dotCX = arrowRect.x - dotRadius * 2f - 4f + dotRadius;
                float dotCY = rowRect.y + rowRect.height / 2f;
                
                labelColor = new Color (labelColor.Value.r, labelColor.Value.g, labelColor.Value.b, labelColor.Value.a * 0.5f);
                DrawColorDot(dotCX, dotCY, dotRadius, labelColor.Value);
            }

            bool isHoveringArrow = Event.current != null && this.arrowRect.Contains(Event.current.mousePosition);
            // Reuse cached arrow style; set the hover-sensitive color each frame.
            var isProSkin = EditorGUIUtility.isProSkin;
            s_ArrowStyle.normal.textColor = isHoveringArrow
                ? (isProSkin ? s_ArrowHoverPro : s_ArrowHoverPersonal)
                : (isProSkin ? s_ArrowNormalPro : s_ArrowNormalPersonal);

            var dimmedAlpha = 0.8f;
            if (node is SceneObjectRef sceneRef)
            {
                var sceneObj = sceneRef.sceneObject;
                string displayName = !string.IsNullOrEmpty(sceneObj?.LastKnownName) ? sceneObj.LastKnownName : this.node.name;
                string sceneName = sceneObj?.LastKnownScene;
                string sceneGuid = sceneObj?.LastKnownSceneGuid;
                string assetPath = sceneObj?.LastKnownPath;
                string gid = sceneObj?.GlobalId;
                bool sceneLoaded = IsSceneLoaded(sceneGuid, sceneName);

                // Reuse cached item style; apply per-call properties.
                s_ItemStyle.normal.textColor = EditorStyles.label.normal.textColor; // reset to default
                int itemFontSize = BrowserSettings.GetItemFontSize();
                if (itemFontSize > 0) s_ItemStyle.fontSize = itemFontSize;
                
                // Apply container label color to font when FontColor style
                if (fontColor.HasValue)
                    s_ItemStyle.normal.textColor = fontColor.Value;
                
                if (sceneLoaded)
                {
                    // When the scene is loaded, resolve the GO to check its state.
                    // Pass the SceneObject, not the bare GID, so the hierarchy-path fallback can
                    // recover prefab instances whose GlobalObjectId stops resolving in Play mode.
                    var resolvedGo = SceneObjectMap.Resolve(sceneObj);
                    bool isMissing = resolvedGo == null;
                    bool isInactiveInHierarchy = resolvedGo != null && !resolvedGo.activeInHierarchy;

                    if (isMissing)
                    {
                        bool wasPlayMode = sceneObj?.WasTrackedDuringPlayMode ?? false;
                        if (wasPlayMode && !EditorApplication.isPlaying)
                        {
                            // Ref was created during Play mode; object no longer exists in Edit mode.
                            string playLabel = string.IsNullOrEmpty(sceneName)
                                ? displayName
                                : $"{displayName} (Was in Play Mode of {sceneName})";
                            s_ItemStyle.normal.textColor = isProSkin
                               ? s_DimmedPro
                               : s_DimmedPersonal;
                            var prevGuiColor = GUI.color;
                            GUI.color = new Color(prevGuiColor.r, prevGuiColor.g, prevGuiColor.b, prevGuiColor.a * 0.6f);
                            DrawLabelWithTempWidth(new GUIContent(playLabel, BrowserSettings.ShowIconsPerType ? icon : null), s_ItemStyle);
                            GUI.color = prevGuiColor;
                        }
                        else
                        {
                            // Target GameObject was deleted in the active scene.
                            string missingLabel = $"⚠ {displayName} (Missing)";
                            s_ItemStyle.normal.textColor = isProSkin
                               ? s_MissingPro
                               : s_MissingPersonal;
                            DrawLabelWithTempWidth(new GUIContent(missingLabel, BrowserSettings.ShowIconsPerType ? icon : null), s_ItemStyle);
                        }
                    }
                    else if (isInactiveInHierarchy)
                    {
                        // GameObject is disabled: gray-out similar to the unloaded-scene look.
                        if (fontColor.HasValue)
                        {
                            var fc = fontColor.Value;
                            s_ItemStyle.normal.textColor = new Color(fc.r, fc.g, fc.b, fc.a * 0.5f);
                        }
                        else
                        {
                            s_ItemStyle.normal.textColor = isProSkin ? s_DimmedPro : s_DimmedPersonal;
                        }

                        var prevGuiColor = GUI.color;
                        GUI.color = new Color(prevGuiColor.r, prevGuiColor.g, prevGuiColor.b, prevGuiColor.a * 0.6f);
                        DrawLabelWithTempWidth(new GUIContent(displayName, BrowserSettings.ShowIconsPerType ? icon : null), s_ItemStyle);
                        GUI.color = prevGuiColor;
                    }
                    else
                    {
                        // Scene is loaded and GO is active: draw normally (use provided fontColor if any)
                        DrawLabelWithTempWidth(new GUIContent(displayName, BrowserSettings.ShowIconsPerType ? icon : null), s_ItemStyle);
                    }
                    if (!isMissing)
                    {
                        Rect focusRectNudged = this.arrowRect;
                        focusRectNudged.y -= 1;
                        bool isFocusOn = sceneRef.isFocusOnSelect;
                        Color focusIconColor = isFocusOn
                            ? (isProSkin ? s_FocusOnPro : s_FocusOnPersonal)
                            : (isHoveringArrow
                                ? (isProSkin ? s_FocusHoverPro : s_FocusHoverPersonal)
                                : (isProSkin ? s_FocusNormalPro : s_FocusNormalPersonal));
                        var prevFocusColor = GUI.color;
                        GUI.color = focusIconColor;
                        bool focusClicked = GUI.Button(focusRectNudged, new GUIContent(s_FocusIcon, "Focus scene view camera when selected"), s_ArrowStyle);
                        GUI.color = prevFocusColor;
                        if (focusClicked)
                        {
                            Undo.RecordObject(sceneRef, "Toggle Focus On Select");
                            sceneRef.isFocusOnSelect = !isFocusOn;
                            EditorUtility.SetDirty(sceneRef);
                            if (sceneRef.isFocusOnSelect)
                            {
                                var go = SceneObjectMap.Resolve(sceneRef.sceneObject);
                                if (go != null && System.Array.IndexOf(Selection.objects, go) >= 0)
                                    EditorApplication.delayCall += () => { SceneView.FrameLastActiveSceneView();};
                            }
                        }
                    }
                }
                else
                {
                    // Scene is not loaded: show a dimmer look for both text and icon.
                    string label = string.IsNullOrEmpty(sceneName) ? displayName : $"{displayName} ({sceneName})";

                    // If a font color is provided, keep its hue but reduce its alpha to make it appear dimmer.
                    if (fontColor.HasValue)
                    {
                        var fc = fontColor.Value;
                        s_ItemStyle.normal.textColor = new Color(fc.r, fc.g, fc.b, fc.a * dimmedAlpha);
                    }
                    else
                    {
                        // Default dimmed gray for unloaded scenes
                        s_ItemStyle.normal.textColor = isProSkin ? s_UnloadedPro
                                : s_UnloadedPersonal;
                    }

                    // To also dim the icon, temporarily reduce GUI.color's alpha while drawing the label.
                    var prevGuiColor = GUI.color;
                    GUI.color = new Color(prevGuiColor.r, prevGuiColor.g, prevGuiColor.b, prevGuiColor.a * 0.75f);
                    DrawLabelWithTempWidth(new GUIContent(label, BrowserSettings.ShowIconsPerType ? icon : null), s_ItemStyle);
                    GUI.color = prevGuiColor;

                    // Show navigation button to open/locate the scene object when the scene is not loaded
                    Rect arrowRectNudged = this.arrowRect;
                    arrowRectNudged.y -= 1;
                    if (GUI.Button(arrowRectNudged, new GUIContent(s_EnterIcon), s_ArrowStyle))
                    {
                        SceneObjectRefUtils.GoToSceneObject(gid, sceneName, sceneGuid, assetPath);
                        Event.current?.Use();
                        treeView.OwnerWindow?.TryCloseIfPopup();
                        return true; // Handled: prevent row selection from also occurring
                    }
                }
            }
            else if (node is SceneComponentRef sceneComponentRef)
            {
                var sceneComponent = sceneComponentRef.sceneComponent;
                string displayName = SceneComponentRefUtils.GetDisplayName(sceneComponent);
                string sceneName = sceneComponent?.LastKnownScene;
                string sceneGuid = sceneComponent?.LastKnownSceneGuid;
                string assetPath = sceneComponent?.LastKnownPath;
                string gid = sceneComponent?.GlobalId;
                bool sceneLoaded = IsSceneLoaded(sceneGuid, sceneName);

                s_ItemStyle.normal.textColor = EditorStyles.label.normal.textColor;
                int itemFontSize = BrowserSettings.GetItemFontSize();
                if (itemFontSize > 0) s_ItemStyle.fontSize = itemFontSize;
                if (fontColor.HasValue)
                    s_ItemStyle.normal.textColor = fontColor.Value;

                if (sceneLoaded)
                {
                    var resolvedComponent = SceneObjectMap.Resolve(sceneComponent);
                    bool isMissing = resolvedComponent == null;
                    bool isDisabled = resolvedComponent is Behaviour behaviour && !behaviour.enabled;

                    if (isMissing)
                    {
                        bool wasPlayMode = sceneComponent?.WasTrackedDuringPlayMode ?? false;
                        if (wasPlayMode && !EditorApplication.isPlaying)
                        {
                            // Ref was created during Play mode; component no longer exists in Edit mode.
                            string playLabel = string.IsNullOrEmpty(sceneName)
                                ? displayName
                                : $"{displayName} (Was in Play Mode of {sceneName})";
                            s_ItemStyle.normal.textColor = isProSkin
                                ? s_DimmedPro
                                : s_DimmedPersonal;
                            var prevGuiColor = GUI.color;
                            GUI.color = new Color(prevGuiColor.r, prevGuiColor.g, prevGuiColor.b, prevGuiColor.a * 0.6f);
                            DrawLabelWithTempWidth(new GUIContent(playLabel, BrowserSettings.ShowIconsPerType ? icon : null), s_ItemStyle);
                            GUI.color = prevGuiColor;
                        }
                        else
                        {
                            // Target component/GameObject was deleted in the active scene.
                            string missingLabel = $"⚠ {displayName} (Missing)";
                            s_ItemStyle.normal.textColor = isProSkin
                                ? s_MissingPro
                                : s_MissingPersonal;
                            DrawLabelWithTempWidth(new GUIContent(missingLabel, BrowserSettings.ShowIconsPerType ? icon : null), s_ItemStyle);
                        }
                    }
                    else if (isDisabled)
                    {
                        if (fontColor.HasValue)
                        {
                            var fc = fontColor.Value;
                            s_ItemStyle.normal.textColor = new Color(fc.r, fc.g, fc.b, fc.a * 0.5f);
                        }
                        else
                        {
                            s_ItemStyle.normal.textColor = isProSkin ? s_DimmedPro :
                                    s_DimmedPersonal;
                        }

                        var prevGuiColor = GUI.color;
                        GUI.color = new Color(prevGuiColor.r, prevGuiColor.g, prevGuiColor.b, prevGuiColor.a * 0.6f);
                        DrawLabelWithTempWidth(new GUIContent(displayName, BrowserSettings.ShowIconsPerType ? icon : null), s_ItemStyle);
                        GUI.color = prevGuiColor;
                    }
                    else
                    {
                        DrawLabelWithTempWidth(new GUIContent(displayName, BrowserSettings.ShowIconsPerType ? icon : null), s_ItemStyle);
                    }

                    if (!isMissing)
                    {
                        Rect focusRectNudged = this.arrowRect;
                        focusRectNudged.y -= 1;
                        bool isFocusOn = sceneComponentRef.isFocusOnSelect;
                        Color focusIconColor = isFocusOn
                            ? (isProSkin ? s_FocusOnPro : s_FocusOnPersonal)
                            : (isHoveringArrow
                                ? (isProSkin ? s_FocusHoverPro : s_FocusHoverPersonal)
                                : (isProSkin ? s_FocusNormalPro : s_FocusNormalPersonal));
                        var prevFocusColor = GUI.color;
                        GUI.color = focusIconColor;
                        bool focusClicked = GUI.Button(focusRectNudged, new GUIContent(s_FocusIcon, "Focus scene view camera when selected"), s_ArrowStyle);
                        GUI.color = prevFocusColor;
                        if (focusClicked)
                        {
                            Undo.RecordObject(sceneComponentRef, "Toggle Focus On Select");
                            sceneComponentRef.isFocusOnSelect = !isFocusOn;
                            EditorUtility.SetDirty(sceneComponentRef);
                            if (sceneComponentRef.isFocusOnSelect)
                            {
                                var component = SceneObjectMap.Resolve(sceneComponentRef.sceneComponent);
                                if (component != null && System.Array.IndexOf(Selection.objects, component.gameObject) >= 0)
                                    EditorApplication.delayCall += () => { SceneView.FrameLastActiveSceneView(); };
                            }
                        }
                    }
                }
                else
                {
                    string label = string.IsNullOrEmpty(sceneName) ? displayName : $"{displayName} ({sceneName})";

                    if (fontColor.HasValue)
                    {
                        var fc = fontColor.Value;
                        s_ItemStyle.normal.textColor = new Color(fc.r, fc.g, fc.b, fc.a * dimmedAlpha);
                    }
                    else
                    {
                        s_ItemStyle.normal.textColor = isProSkin ? s_UnloadedPro
                                : s_UnloadedPersonal;
                    }

                    var prevGuiColor = GUI.color;
                    GUI.color = new Color(prevGuiColor.r, prevGuiColor.g, prevGuiColor.b, prevGuiColor.a * 0.75f);
                    DrawLabelWithTempWidth(new GUIContent(label, BrowserSettings.ShowIconsPerType ? icon : null), s_ItemStyle);
                    GUI.color = prevGuiColor;

                    Rect arrowRectNudged = this.arrowRect;
                    arrowRectNudged.y -= 1;
                    if (GUI.Button(arrowRectNudged, new GUIContent(s_EnterIcon), s_ArrowStyle))
                    {
                        SceneComponentRefUtils.GoToSceneComponent(gid, sceneName, sceneGuid, assetPath);
                        Event.current?.Use();
                        treeView.OwnerWindow?.TryCloseIfPopup();
                        return true;
                    }
                }
            }
            else
            {
                // Determine asset info early so SceneAsset rows can receive special styling.
                // Use the per-instance cached path to avoid calling AssetDatabase on every repaint.
                string assetPath = GetAssetPathCached(this.node);
                bool isSceneAsset = node is SceneAsset;
                bool isPrefabAsset = !string.IsNullOrEmpty(assetPath) && assetPath.EndsWith(".prefab");
                bool isFolder = node is DefaultAsset && !string.IsNullOrEmpty(assetPath) && AssetDatabase.IsValidFolder(assetPath);

                var baseTarget = node as Base;
                var isBase = baseTarget != null;

                bool isUrlTextAsset = node is TextAsset ta && LeafNodeActionHelper.IsUrl(ta.text);

                // Reuse cached item style; set per-call properties before each draw.
                s_ItemStyle.normal.textColor = EditorStyles.label.normal.textColor; // reset to default
                {
                    int itemFontSize2 = BrowserSettings.GetItemFontSize();
                    if (itemFontSize2 > 0) s_ItemStyle.fontSize = itemFontSize2;
                }
                if (fontColor.HasValue)
                    s_ItemStyle.normal.textColor = fontColor.Value;
                
                var sceneLoaded = false;
                var isEditorPlaying = false;
                if (isSceneAsset && !string.IsNullOrEmpty(assetPath))
                {
                    // Determine if the scene is loaded in the Editor
                    var s = EditorSceneManager.GetSceneByPath(assetPath);
                    if (s.IsValid())
                        sceneLoaded = s.isLoaded;

                    // Determine active/play state
                    isEditorPlaying = EditorApplication.isPlaying;
                    bool isActiveScene = false;
                    if (sceneLoaded)
                    {
                        var active = EditorSceneManager.GetActiveScene();
                        isActiveScene = active.path == assetPath || active.name == System.IO.Path.GetFileNameWithoutExtension(assetPath);
                    }

                    // Theme-aware play-mode blue
                    Color playModeBlue = EditorGUIUtility.isProSkin ? new Color(0.22f, 0.48f, 0.9f, 1f) : new Color(0.12f, 0.4f, 0.85f, 1f);
                    Color defaultLabelColor = EditorStyles.label.normal.textColor;

                    if (isEditorPlaying && sceneLoaded)
                    {
                        s_ItemStyle.normal.textColor = playModeBlue;
                        if (isActiveScene)
                            s_ItemStyle.fontStyle = FontStyle.Bold;
                    }
                    else if (isActiveScene)
                    {
                        if (fontColor.HasValue && colorStyle == ColorDisplayStyle.FontColor)
                        {
                            var fc = fontColor.Value;
                            s_ItemStyle.normal.textColor = new Color(fc.r, fc.g, fc.b, 1f);
                        }
                        else
                        {
                            s_ItemStyle.normal.textColor = defaultLabelColor;
                        }
                        s_ItemStyle.fontStyle = FontStyle.Bold;
                    }
                    else if (sceneLoaded)
                    {
                        if (fontColor.HasValue && colorStyle == ColorDisplayStyle.FontColor)
                        {
                            var fc = fontColor.Value;
                            s_ItemStyle.normal.textColor = new Color(fc.r, fc.g, fc.b, 1f);
                        }
                        else
                        {
                            s_ItemStyle.normal.textColor = defaultLabelColor;
                        }
                    }
                    else
                    {
                        // Unloaded scene: dim label and icon, show navigation button to open scene
                        string label = node.name;

                        // Dim color
                        if (fontColor.HasValue && colorStyle == ColorDisplayStyle.FontColor)
                        {
                            var fc = fontColor.Value;
                            s_ItemStyle.normal.textColor = new Color(fc.r, fc.g, fc.b, fc.a * dimmedAlpha);
                        }
                        else
                        {
                            s_ItemStyle.normal.textColor = isProSkin ? s_UnloadedPro
                                    : s_UnloadedPersonal;
                        }

                        var prevGuiColor = GUI.color;
                        GUI.color = new Color(prevGuiColor.r, prevGuiColor.g, prevGuiColor.b, prevGuiColor.a * 0.75f);
                        DrawLabelWithTempWidth(new GUIContent(label, BrowserSettings.ShowIconsPerType ? icon : null), s_ItemStyle);
                        GUI.color = prevGuiColor;

                        Rect arrowRectNudged = this.arrowRect;
                        arrowRectNudged.y -= 1;
                        if (GUI.Button(arrowRectNudged, new GUIContent(s_EnterIcon), s_ArrowStyle))
                        {
                            EditorSceneManager.OpenScene(assetPath, OpenSceneMode.Single);
                            EditorGUIUtils.FocusHierarchyWindowIfPresent();
                            Event.current?.Use();
                            treeView.OwnerWindow?.TryCloseIfPopup();
                            return true;
                        }

                        // Reset fontStyle before early return so cached style stays clean.
                        s_ItemStyle.fontStyle = FontStyle.Normal;
                        // Early return after drawing the unloaded scene label
                        return false;
                    }
                }

                // Check if node implements IHasEmoji and has an emoji
                string displayName = this.node.name;
                var isURL = node is TextAsset urlAsset && LeafNodeActionHelper.IsUrl(urlAsset.text) && BrowserSettings.ShowIconsPerType;

                GUIContent labelContent;
                if (node is IHasEmoji hasEmoji && !string.IsNullOrEmpty(hasEmoji.EmojiIcon) && BrowserSettings.ShowIconsPerType)
                {
                    // When viewing the Home browser (top-level list of Bases), avoid showing
                    // both the per-type icon image AND a regular emoji. Regular emoji should
                    // appear as text only. Editor icons (stored via the EditorIconPrefix)
                    // still render as images.
                    Texture fallback = this.icon;
                    bool isRegularEmoji = !EmojiIconUtils.IsEditorIcon(hasEmoji.EmojiIcon);
                    bool isBaseAtHome = node is Base && (treeView?.OwnerWindow?.IsAtHome() ?? false);
                    // Keep the per-type icon when the emoji itself cannot be drawn,
                    // otherwise the row would lose every visual marker.
                    if (isRegularEmoji && isBaseAtHome && EmojiSupport.IsSupported)
                        fallback = null;

                    labelContent = EmojiIconUtils.BuildLabelContent(this.node.name, hasEmoji.EmojiIcon, fallback);
                }
                else
                {
                    if (isURL && EmojiSupport.IsSupported)
                    {
                        displayName = "🔗 " + this.node.name;
                    }

                    // Don't show icon before label at all for Base with no custom emoji.
                    // URL rows normally replace the icon with the 🔗 glyph — when emoji
                    // cannot be drawn, fall back to the per-type icon instead.
                    var isShowIcon = BrowserSettings.ShowIconsPerType && !(isURL && EmojiSupport.IsSupported) && !(node is Base);
                    labelContent = new GUIContent(displayName, isShowIcon ? this.icon : null);
                }

#if SECOND_BRAIN_PRO
                var actionItemGUI = ProFeature.Provider.CreateActionItemGUI(node, s_ItemStyle, arrowRect, rowRect); 
                labelContent = actionItemGUI.TryAppendActionItemDetail(labelContent);
#endif

                // Draw the (possibly appended) label. Clamp width so it doesn't overlap controls.
                float availableWidth = Math.Max(0f, arrowRect.x - 20f - trueIndentedItemRect.x); // space between indent and controls

                // If this row uses an editor icon (stored emoji value with editor-icon path), attempt to draw
                // icon + text using the shared helper. Fall back to the default GUI.Label when the helper
                // cannot draw (for example when the icon texture cannot be found).
                bool drewManual = false;
                if (node is IHasEmoji hasEmoji2 && !string.IsNullOrEmpty(hasEmoji2.EmojiIcon) && BrowserSettings.ShowIconsPerType && EmojiIconUtils.IsEditorIcon(hasEmoji2.EmojiIcon))
                {
                    var labelRectForHelper = new Rect(trueIndentedItemRect.x, rowRect.y, availableWidth, rowRect.height);
                    Rect computedTextRect;
                    if (EmojiIconUtils.TryDrawEditorIconLabel(labelRectForHelper, hasEmoji2.EmojiIcon, s_ItemStyle, labelContent.text, out computedTextRect))
                    {
                        drewManual = true;
                    }
                }

                if (!drewManual)
                {
                    Vector2 labelSizeForDraw = s_ItemStyle.CalcSize(labelContent);
                    float drawLabelWidth = Math.Min(labelSizeForDraw.x, availableWidth);
                    var labelRect = new Rect(trueIndentedItemRect.x, rowRect.y, drawLabelWidth, rowRect.height);
                    GUI.Label(labelRect, labelContent, s_ItemStyle);
                }

                // Reset fontStyle on the cached style so the next leaf row starts clean.
                s_ItemStyle.fontStyle = FontStyle.Normal;
#if SECOND_BRAIN_PRO
                if (actionItemGUI.TryDrawActionItem())
                    return true;
#endif

                if (isFolder)
                {
                    string folderGuid = AssetDatabase.AssetPathToGUID(assetPath);
                    bool isFolderFocusOn = BrowserSettings.GetFolderFocusOnSelect(folderGuid);
                    Color folderFocusColor = isFolderFocusOn
                        ? (isProSkin ? new Color(0.4f, 0.8f, 1f, 1f) : new Color(0.1f, 0.5f, 0.9f, 1f))
                        : (isHoveringArrow
                            ? (isProSkin ? new Color(1f, 1f, 1f, 1f) : new Color(0f, 0f, 0f, 1f))
                            : (isProSkin ? new Color(0.6f, 0.6f, 0.6f, 0.4f) : new Color(0.2f, 0.2f, 0.2f, 0.5f)));
                    Rect folderFocusRect = this.arrowRect;
                    folderFocusRect.y -= 1;
                    var prevFolderFocusColor = GUI.color;
                    GUI.color = folderFocusColor;
                    bool folderFocusClicked = GUI.Button(folderFocusRect, new GUIContent(s_FocusIcon, "Enter folder in Project window when selected"), s_ArrowStyle);
                    GUI.color = prevFolderFocusColor;
                    if (folderFocusClicked)
                    {
                        bool newValue = !isFolderFocusOn;
                        BrowserSettings.SetFolderFocusOnSelect(folderGuid, newValue);
                        if (newValue && System.Array.IndexOf(Selection.objects, node) >= 0)
                            EditorApplication.delayCall += () => EditorGUIUtils.EnterFolderInProjectWindow(node);
                    }
                }

                if (isSceneAsset && !isEditorPlaying && !string.IsNullOrEmpty(assetPath))
                {
                    float playButtonSize = 16f;
                    float playButtonPadding = 2f;
                    float playButtonX = arrowRect.x - playButtonSize - playButtonPadding;
                    Rect playButtonRect = new Rect(playButtonX, rowRect.y + (rowRect.height - playButtonSize) / 2f, playButtonSize, playButtonSize);

                    bool isHoveringPlay = Event.current != null && playButtonRect.Contains(Event.current.mousePosition);
                    // Reuse cached arrow style with per-call color.
                    s_ArrowStyle.normal.textColor = isHoveringPlay
                        ?
                        isProSkin ? new Color(1f, 1f, 1f, 1f) : new Color(0, 0, 0, 1)
                        :
                        isProSkin
                            ? new Color(0.6f, 0.6f, 0.6f, 0.6f)
                            : new Color(0.2f, 0.2f, 0.2f, 1);

                    Rect playRectNudged = playButtonRect;
                    playRectNudged.y -= 1;
                    if (GUI.Button(playRectNudged, new GUIContent(s_PlayIcon), s_ArrowStyle))
                    {
                        LeafNodeActionHelper.TryPlayScene(this.node);
                        Event.current?.Use();
                        return true;
                    }
                }

                if (isSceneAsset || isPrefabAsset || isBase || isUrlTextAsset)
                {
                    Rect arrowRectNudged2 = this.arrowRect;
                    arrowRectNudged2.y -= 1;
                    // Restore arrow style color to the per-row hover value for the navigation arrow.
                    s_ArrowStyle.normal.textColor = isHoveringArrow ? new Color(1f, 1f, 1f, 1f) : new Color(0.6f, 0.6f, 0.6f, 0.6f);
                    if (!sceneLoaded && GUI.Button(arrowRectNudged2, new GUIContent(s_EnterIcon), s_ArrowStyle))
                    {
                        if (isUrlTextAsset)
                        {
                            Application.OpenURL(((TextAsset)node).text.Trim());
                        }
                        else if (isSceneAsset)
                        {
                            var sceneAssetPath = assetPath;
                                if (!string.IsNullOrEmpty(sceneAssetPath))
                                {
                                    var notif = new GUIContent("Open Scene: " + this.node.name);
                                    EditorGUIUtils.ShowNotificationOnActiveView(notif);
                                    EditorSceneManager.OpenScene(sceneAssetPath, OpenSceneMode.Single);
                                    EditorGUIUtils.FocusHierarchyWindowIfPresent();
                                }
                        }
                        else if (isPrefabAsset)
                        {
                            var prefabAssetPath = assetPath;
                                if (!string.IsNullOrEmpty(prefabAssetPath))
                                {
                                    var notif = new GUIContent("Open Prefab: " + prefabAssetPath);
                                    EditorGUIUtils.ShowNotificationOnActiveView(notif);
                                    PrefabStageUtility.OpenPrefab(prefabAssetPath);
                                    EditorGUIUtils.FocusHierarchyWindowIfPresent();
                                }
                        }
                        else
                        {
                            treeView.OwnerWindow.Controller.SetTargetWithUndo(baseTarget);
                        }

                        Event.current?.Use();
                        return true; // Handled: prevent row selection from also occurring
                    }
                }
            }

            return false;
        }
    }
}
