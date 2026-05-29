using System.IO;
using UnityEditor;
using UnityEngine;

namespace SecretZauce.SecondBrain.Editor
{
    /// <summary>
    /// Shared utility for renaming Unity objects with proper asset file handling.
    /// Prevents filename mismatch warnings by renaming asset files first.
    /// </summary>
    public static class RenameUtils
    {
        static RenameUtils()
        {
            Undo.undoRedoPerformed -= ValidateFileNameOnUndoRedo;
            Undo.undoRedoPerformed += ValidateFileNameOnUndoRedo;
        }

        /// <summary>
        /// Renames a Unity Object with proper validation and asset file handling.
        /// Shows dialog if name already exists in the same folder.
        /// </summary>
        /// <param name="obj">The object to rename</param>
        /// <param name="newName">The new name (will be trimmed)</param>
        /// <returns>True if rename succeeded, false if cancelled or failed</returns>
        public static bool RenameObject(Object obj, string newName)
        {
            if (obj == null || string.IsNullOrEmpty(newName))
                return false;

            // Reset affected-asset tracking so this rename operation is tracked cleanly.
            SubAssetRefreshUtils.ClearAffectedAssets();

            // Trim whitespace
            newName = newName.Trim();

            // Validate name is not empty after trim
            if (string.IsNullOrEmpty(newName))
            {
                EditorUtility.DisplayDialog("Invalid Name", 
                    "Name cannot be empty or whitespace only.", 
                    "OK");
                return false;
            }

            if (obj.name == newName)
                return false; // No change needed

            string assetPath = AssetDatabase.GetAssetPath(obj);
            bool isMainAsset = !string.IsNullOrEmpty(assetPath) && AssetDatabase.IsMainAsset(obj);

            // If it's a main asset, check for duplicate names in the same folder
            if (isMainAsset)
            {
                string folderPath = Path.GetDirectoryName(assetPath);
                string extension = Path.GetExtension(assetPath);
                if (folderPath != null)
                {
                    string potentialNewPath = Path.Combine(folderPath, newName + extension);

                    // Check if an asset with this name already exists
                    if (AssetDatabase.LoadAssetAtPath<Object>(potentialNewPath) != null)
                    {
                        EditorUtility.DisplayDialog(
                            "Name Already Exists",
                            $"An asset named '{newName}' already exists in this folder.\n\nDo you want to choose a different name?",
                            "Choose Different Name",
                            "Cancel");

                        return false; // Don't proceed with rename
                    }
                }
            }

            // Start undo group for atomic operation
            int undoGroup = Undo.GetCurrentGroup();
            Undo.RecordObject(obj, "Rename Object");

            // Perform rename based on asset type
            if (isMainAsset)
            {
                // Register OLD path before rename so undo/redo can target the pre-rename location.
                SubAssetRefreshUtils.RegisterAffectedAsset(obj);

                // Rename the asset file FIRST - this prevents filename mismatch warnings
                // AssetDatabase.RenameAsset() automatically updates obj.name as well
                string error = AssetDatabase.RenameAsset(assetPath, newName);
                
                if (!string.IsNullOrEmpty(error))
                {
                    // Show error dialog
                    EditorUtility.DisplayDialog("Rename Failed", 
                        $"Failed to rename asset:\n{error}", 
                        "OK");
                    
                    // Undo the operation
                    Undo.RevertAllInCurrentGroup();
                    return false;
                }

                // Register NEW path so redo can also target the correct location.
                SubAssetRefreshUtils.RegisterAffectedAsset(obj);
                // File rename also updates the object name automatically
                EditorUtility.SetDirty(obj);
            }
            else
            {
                // Sub-asset (or non-persistent object): rename in-memory and persist the
                // containing parent asset immediately so the Project window updates.
                obj.name = newName;
                // MarkDirtyAndSave: SetDirty(obj) + SaveAssets() + ImportAsset(parentPath, ForceUpdate)
                // For sub-assets, GetAssetPath returns the containing asset's file path.
                SubAssetRefreshUtils.MarkDirtyAndSave(obj);
            }

            // Collapse undo operations into single step
            Undo.CollapseUndoOperations(undoGroup);

            return true;
        }

        /// <summary>
        /// Validates that asset file names match their object names after an undo/redo operation.
        /// Scoped to the assets tracked by SubAssetRefreshUtils to avoid scanning all loaded objects.
        /// </summary>
        static void ValidateFileNameOnUndoRedo()
        {
            // Only check assets that were touched by a mutation tracked via SubAssetRefreshUtils.
            // This avoids the expensive global Resources.FindObjectsOfTypeAll scan.
            var affectedPaths = SubAssetRefreshUtils.AffectedParentPaths;
            if (affectedPaths.Count == 0)
                return;

            foreach (var assetPath in affectedPaths)
            {
                if (string.IsNullOrEmpty(assetPath))
                    continue;

                // Only main assets have file names that can diverge from their object name.
                var obj = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
                if (obj == null || !AssetDatabase.IsMainAsset(obj))
                    continue;

                string fileName = Path.GetFileNameWithoutExtension(assetPath);
                if (fileName == obj.name)
                    continue;

                // After undo/redo, obj.name has been reverted in-memory but the asset file still
                // carries the old filename.  Unity's RenameAsset internally warns when the main
                // object name doesn't match the file name at the time of the call.  Temporarily
                // align obj.name to the current file name so that check passes cleanly, then let
                // RenameAsset rename the file (and restore obj.name) to the desired target name.
                string targetName = obj.name;
                obj.name = fileName; // match file → no mismatch warning inside RenameAsset
                string error = AssetDatabase.RenameAsset(assetPath, targetName);
                if (!string.IsNullOrEmpty(error))
                {
                    obj.name = targetName; // restore in-memory name if the file rename failed
                    Debug.LogWarning($"Asset rename warning (undo/redo): {error}");
                }
            }
        }
    }
}

