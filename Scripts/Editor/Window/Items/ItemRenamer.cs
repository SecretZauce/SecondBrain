using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SecretZauce.SecondBrain.Editor
{
    /// <summary>
    /// Manages inline rename sessions for TreeView.
    /// Responsible for starting/canceling/committing rename operations and drawing the text field.
    /// Does not perform the actual renaming of assets; calls RenameUtils for that.
    /// </summary>
    public class ItemRenamer
    {
        readonly TreeView ownerTreeView;
        int[]  renamingPath;
        string renamingText;
        bool   focusRenameField;
        bool   selectAllText;
        int    renameSessionId;
        // Pre-built control name for the active rename session — avoids repeated string.Join allocations.
        string renameControlName;

        Action onRenameCompleted;
        
        public ItemRenamer(TreeView treeView)
        {
            ownerTreeView = treeView ?? throw new ArgumentNullException(nameof(treeView));
        }

        public void SetOnRenameCompleted(Action callback)
        {
            onRenameCompleted = callback;
        }

        public bool IsRenamingAny => renamingPath != null;
        public bool IsRenaming(int[] path)
        {
            return renamingPath != null && StructureUtils.ArePathsEqual(path, renamingPath);
        }

        public void StartRename(int[] path)
        {
            if (path == null) return;
            var obj = ownerTreeView.GetObjectAtPath(path);
            if (obj == null) return;

            // For SceneObjectRef: only allow rename when the referenced scene is loaded.
            if (obj is SceneObjectRef sceneRef)
            {
                string sceneName = sceneRef.sceneObject?.LastKnownScene;
                bool sceneLoaded = !string.IsNullOrEmpty(sceneName) && SceneManager.GetSceneByName(sceneName).isLoaded;
                if (!sceneLoaded)
                {
                    EditorApplication.delayCall += () => EditorUtility.DisplayDialog("Rename Not Available",
                        "The scene containing this object is not currently open. Open the scene to rename the object.", "OK");
                    return;
                }

                // Allow rename – show the scene object's name (not the asset name "Link to X")
                renamingPath      = path;
                renamingText      = sceneRef.sceneObject?.LastKnownName ?? obj.name;
                focusRenameField  = true;
                selectAllText     = true;
                renameSessionId++;
                renameControlName = $"rename_{string.Join("_", path)}_{renameSessionId}";
                ownerTreeView.DragDropManager.CancelPotentialDrag();
                ownerTreeView.CancelDrag();
                return;
            }

            if (obj is SceneComponentRef)
            {
                EditorApplication.delayCall += () => EditorUtility.DisplayDialog("Rename Not Available",
                    "Scene component links use the referenced component identity and cannot be renamed.", "OK");
                return;
            }
            
            renamingPath      = path;
            renamingText      = obj.name;
            focusRenameField  = true;
            selectAllText     = true;
            renameSessionId++;  // new session → unique control name so IMGUI discards old editor state
            renameControlName = $"rename_{string.Join("_", path)}_{renameSessionId}";

            // Cancel any drag operations while renaming
            ownerTreeView.DragDropManager.CancelPotentialDrag();
            ownerTreeView.CancelDrag();
        }

        void CancelRename()
        {
            renamingPath = null;
            renamingText = null;
            focusRenameField = false;
            selectAllText = false;
        }

        /// <summary>Cancels any in-progress rename session without committing.</summary>
        public void CancelPublic()
        {
            CancelRename();
        }

        /// <summary>
        /// Draws and processes the rename text field for the given path/object.
        /// Returns true if the caller should exit early (e.g. after rename completion) to avoid further GUI work.
        /// </summary>
        public bool HandleRenamingField(int[] path, Object obj, Rect textFieldRect, bool resetIndent = false)
        {
            textFieldRect.width -= 13;
            if (obj == null)
                return false;

            if (!IsRenaming(path))
                return false;

            // Handle Escape key to cancel rename
            if (IsKeyDown(KeyCode.Escape))
            {
                CancelRename();
                GUI.FocusControl(null);
                GUIUtility.keyboardControl = 0;
                Event.current.Use();
                // Schedule a repaint to ensure UI updates immediately
                EditorApplication.delayCall += () => { EditorWindow.focusedWindow?.Repaint(); };
                return true;
            }

            // Focus the field on first draw
            if (focusRenameField)
            {
                EditorGUI.FocusTextInControl(renameControlName);
                focusRenameField = false;
            }

            int oldIndent = EditorGUI.indentLevel;
            if (resetIndent)
                EditorGUI.indentLevel = 0;

            GUI.SetNextControlName(renameControlName);
            string newText = EditorGUI.DelayedTextField(textFieldRect, renamingText);

            // Select all text once the text field has actually received keyboard focus.
            // Guard keyboardControl != 0: GetNameOfFocusedControl may return the "wanted" name from
            // FocusTextInControl before the control ID is assigned, so we'd get a dummy TextEditor at ID 0.
            if (selectAllText && GUI.GetNameOfFocusedControl() == renameControlName && GUIUtility.keyboardControl != 0)
            {
                TextEditor textEditor = (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);
                textEditor.SelectAll();
                selectAllText = false;
            }

            if (resetIndent)
                EditorGUI.indentLevel = oldIndent;

            // Commit on Enter or if delayed textfield changed
            if (newText != renamingText || IsKeyDown(KeyCode.Return))
            {
                renamingText = newText;
                CompleteRename(obj);
                GUI.FocusControl(null);
                GUIUtility.keyboardControl = 0;
                if (Event.current != null)
                {
                    Event.current.Use();
                    EditorApplication.delayCall += () => { EditorWindow.focusedWindow?.Repaint(); };
                }
                return true;
            }

            // Clicking outside the text field should complete rename
            if (Event.current != null && Event.current.type == EventType.MouseDown && !textFieldRect.Contains(Event.current.mousePosition))
            {
                CompleteRename(obj);
                Event.current.Use();
                EditorApplication.delayCall += () => { EditorWindow.focusedWindow?.Repaint(); };
                return true;
            }

            return false;
        }

        void CompleteRename(Object obj)
        {
            if (obj == null)
            {
                CancelRename();
                return;
            }

            // For SceneObjectRef: rename the underlying scene object and update the ref's metadata.
            if (obj is SceneObjectRef sceneRef)
            {
                bool success = SceneObjectRefUtils.RenameSceneObjectAndUpdateRef(sceneRef, renamingText);
                CancelRename();
                if (success)
                    onRenameCompleted?.Invoke();
                return;
            }
            
            bool renameSuccess = RenameUtils.RenameObject(obj, renamingText);
            CancelRename();

            if (renameSuccess)
            {
                onRenameCompleted?.Invoke();
            }
        }

        static bool IsKeyDown(KeyCode keyCode)
        {
            return Event.current != null && Event.current.type == EventType.KeyDown && Event.current.keyCode == keyCode;
        }
    }
}
