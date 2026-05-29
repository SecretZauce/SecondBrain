using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SecretZauce.SecondBrain.Editor
{
    /// <summary>
    /// Handles the right panel (detail view) of the Browser Window.
    /// Displays details for selected collections, groups, or configs.
    /// </summary>
    public class ItemDetailPanel
    {
        Vector2 scrollPosition;
        readonly string nameFieldControlName = "ItemDetailNameField";
        
        
        /// <summary>
        /// Draws the right panel showing details of the selected item(s) using generic index traversal.
        /// </summary>
        /// <param name="root">The root IStructure (e.g., collection list)</param>
        /// <param name="indexes">Array of indexes to traverse hierarchy (primary selection)</param>
        /// <param name="allPaths">All selected paths for multi-selection</param>
        /// <param name="onDeleteRequested">Callback when delete button is clicked</param>
        /// <param name="onRemoveRequested">Callback when remove from list button is clicked</param>
        /// <param name="preventFocus">If true, prevents focusing the name field (e.g., after rename)</param>
        /// <param name="onCreateChild">Callback to create a child on a given parent (optional)</param>
        /// <returns>The current scroll position</returns>
        public Vector2 Draw(int[] indexes, List<int[]> allPaths, Action onDeleteRequested, Action onRemoveRequested = null, bool preventFocus = false, Action<Object> onCreateChild = null)
        {
            GUILayout.BeginVertical();
            scrollPosition = GUILayout.BeginScrollView(scrollPosition);

            // Check if we have multi-selection
            if (allPaths is { Count: > 1 })
            {
                DrawMultiSelection(allPaths, onDeleteRequested, onRemoveRequested);
            }
            else
            {
                // Single selection (original behavior)
                // Special case: when primary selection is null or empty -> treat as root selected
                if ((indexes == null || indexes.Length == 0) && onCreateChild != null)
                {
                    
                }
                else
                {
                    DrawSingleSelection(indexes, onDeleteRequested, onRemoveRequested, preventFocus);
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            return scrollPosition;
        }
        
        /// <summary>
        /// Draws details for multiple selected items
        /// </summary>
        void DrawMultiSelection(List<int[]> allPaths, Action onDeleteRequested, Action onRemoveRequested)
        {
            EditorGUILayout.LabelField($"Multiple Items Selected ({allPaths.Count})", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            // Show remove and delete buttons for all selected items
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            // Remove from parent button (only if callback is provided)
            if (onRemoveRequested != null)
            {
                if (GUILayout.Button("Remove from Parent", GUILayout.Width(150), GUILayout.Height(24)))
                {
                    onRemoveRequested?.Invoke();
                    GUIUtility.ExitGUI();
                }
                GUILayout.Space(5);
            }
            
            if (GUILayout.Button("Delete All Selected", GUILayout.Width(150), GUILayout.Height(24)))
            {
                onDeleteRequested?.Invoke();
                GUIUtility.ExitGUI();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Selected Items:", EditorStyles.boldLabel);
            
            // List all selected items
            foreach (var path in allPaths)
            {
                var item = StructureUtils.GetNodeAtPath(path, null);
                if (item is Object unityObj)
                {
                    EditorGUILayout.BeginHorizontal("box");

                    // Item icon/type indicator
                    GUILayout.Label(EditorGUIUtility.IconContent("d_ScriptableObject Icon"), GUILayout.Width(20), GUILayout.Height(20));

                    // Item name
                    EditorGUILayout.LabelField(unityObj.name, GUILayout.ExpandWidth(true));

                    // Item type (smaller, gray)
                    GUIStyle typeStyle = new GUIStyle(EditorStyles.label);
                    typeStyle.fontSize = Mathf.Max(9, (int)(EditorStyles.label.fontSize * 0.85f));
                    typeStyle.normal.textColor = new Color(0.5f, 0.5f, 0.5f, 1f);
                    EditorGUILayout.LabelField(unityObj.GetType().Name, typeStyle, GUILayout.Width(100));

                    EditorGUILayout.EndHorizontal();
                }
            }
        }
        
        /// <summary>
        /// Draws details for a single selected item (original behavior)
        /// </summary>
        void DrawSingleSelection(int[] indexes, Action onDeleteRequested, Action onRemoveRequested, bool preventFocus = false)
        {
            // Use shared helper to get the target item; helper returns null for invalid paths
            var current = StructureUtils.GetNodeAtPath(indexes, null);
            if (current == null)
            {
                DrawNoSelectionMessage();
                return;
            }

            // Draw the final item (even if not IStructure)
            DrawTargetItem(current, onDeleteRequested, onRemoveRequested, preventFocus);
        }

        /// <summary>
        /// Sets the scroll position for the detail panel.
        /// </summary>
        public void SetScrollPosition(Vector2 position)
        {
            scrollPosition = position;
        }

        // Draws the final item generically
        void DrawTargetItem(object item, Action onDeleteRequested = null, Action onRemoveRequested = null, bool preventFocus = false)
        {
            // Always draw UnityEngine.Object
            if (item is Object unityObj)
            {
                // Name field, select button, remove button, and delete button on the same line
                EditorGUILayout.BeginHorizontal();
                // Tight select button (left of name)
                GUIStyle tightButton = new GUIStyle(GUI.skin.button)
                {
                    margin = new RectOffset(0, 0, 0, 0),
                    padding = new RectOffset(0, 0, 0, 0),
                    fixedWidth = 20,
                    fixedHeight = 20
                };
                // Use Inspector icon for select
                Texture selectIcon = EditorGUIUtility.IconContent("d_UnityEditor.InspectorWindow").image;
                if (GUILayout.Button(new GUIContent(selectIcon, "Select in Inspector"), tightButton))
                {
                    SelectionUtils.SetActiveAndPing(unityObj);
                }
                
                // Clear focus if rename just completed to prevent auto-focusing this field
                if (preventFocus)
                {
                    GUI.FocusControl(null);
                }
                
                // Use DelayedTextField to prevent warnings while typing
                // It only commits the value on Enter or focus loss
                EditorGUI.BeginChangeCheck();
                GUI.SetNextControlName(nameFieldControlName);
                GUILayoutOption[] nameFieldOptions = { GUILayout.ExpandWidth(true), GUILayout.Height(24) };
                string newName = EditorGUILayout.DelayedTextField(unityObj.name, nameFieldOptions);
                if (EditorGUI.EndChangeCheck() && newName != unityObj.name)
                {
                    // Use shared rename utility - handles validation, duplicate checking, and proper asset renaming
                    RenameUtils.RenameObject(unityObj, newName);
                }
                
                // Tight remove button (before delete button) - only if callback is provided
                if (onRemoveRequested != null)
                {
                    Texture minusIcon = EditorGUIUtility.IconContent("Toolbar Minus").image;
                    if (GUILayout.Button(new GUIContent(minusIcon, "Remove from Parent"), tightButton))
                    {
                        onRemoveRequested?.Invoke();
                        GUIUtility.ExitGUI();
                    }
                }
                
                // Tight delete button (right of name/remove)
                Texture binIcon = EditorGUIUtility.IconContent("TreeEditor.Trash").image;
                if (GUILayout.Button(new GUIContent(binIcon, "Delete"), tightButton))
                {
                    onDeleteRequested?.Invoke();
                    GUIUtility.ExitGUI();
                }
                EditorGUILayout.EndHorizontal();
                
                // Display type below the name, smaller and less dominant
                GUIStyle typeStyle = new GUIStyle(EditorStyles.label);
                typeStyle.fontSize = Mathf.Max(9, (int)(EditorStyles.label.fontSize * 0.85f));
                typeStyle.normal.textColor = new Color(0.5f, 0.5f, 0.5f, 1f); // gray
                EditorGUILayout.LabelField(unityObj.GetType().Name, typeStyle);

                EditorGUIUtils.DrawObjectInspector(unityObj);
                return;
            }
            EditorGUILayout.LabelField("Invalid selection.", EditorStyles.boldLabel);
        }

        void DrawNoSelectionMessage()
        {
            string helpMessage = "Choose an item from the left pane.";
            EditorGUILayout.HelpBox(helpMessage, MessageType.Info);
        }
    }
}
