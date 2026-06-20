using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SecretZauce.SecondBrain.Editor
{
    /// <summary>
    /// Centralized helper for refreshing the Unity Project window after sub-asset structural changes.
    /// Tracks affected parent asset paths so undo/redo handlers can perform targeted imports
    /// instead of expensive global repaints.
    ///
    /// Usage pattern:
    ///   1. Call ClearAffectedAssets() at the start of each mutation operation.
    ///   2. During mutation, call ImportAndRegister(path) or MarkDirtyAndSave(obj) for each modified asset.
    ///   3. In undo/redo handlers, call ScheduleRefreshAffectedAssets() — NOT RefreshAffectedAssets() directly —
    ///      so the save/import runs after Unity has fully settled its internal undo state.
    /// </summary>
    public static class SubAssetRefreshUtils
    {
        static readonly HashSet<string> _affectedParentPaths = new HashSet<string>();

        // Guard flag: prevents stacking multiple identical deferred refresh calls when the
        // user presses Ctrl+Z/Ctrl+Y rapidly.
        static bool _refreshScheduled;

        /// <summary>
        /// True while this class is executing a SaveAssets / ImportAsset / StopAssetEditing
        /// call that it initiated internally. ProfileAssetWatcher reads this to skip the
        /// OnActiveProfileChanged notification for editor-originated imports (rename, delete,
        /// reorder) and only fire it for external file changes (e.g. git branch switch).
        /// </summary>
        internal static bool InternalImportInProgress;

        /// <summary>
        /// Read-only view of the currently tracked affected asset paths.
        /// Used by undo/redo handlers and scoped validation routines.
        /// </summary>
        public static IReadOnlyCollection<string> AffectedParentPaths => _affectedParentPaths;

        /// <summary>
        /// Clears the tracked affected asset paths.
        /// Call at the start of each mutation operation (create/rename/delete/reparent)
        /// to start fresh path tracking for that operation.
        /// Do NOT call from undo/redo handlers — paths must persist across undo/redo cycles.
        /// </summary>
        public static void ClearAffectedAssets() => _affectedParentPaths.Clear();

        /// <summary>
        /// Registers the asset path of the given object as affected.
        /// For sub-assets, this records the containing parent asset's path.
        /// Returns the resolved path (or null) for convenience.
        /// </summary>
        public static string RegisterAffectedAsset(Object obj)
        {
            if (obj == null) return null;
            string path = AssetDatabase.GetAssetPath(obj);
            if (!string.IsNullOrEmpty(path))
                _affectedParentPaths.Add(path);
            return string.IsNullOrEmpty(path) ? null : path;
        }

        /// <summary>
        /// Marks the object dirty, saves all dirty assets, and force-imports the object's
        /// asset path so the Project window reflects the change immediately.
        /// Also registers the path as affected for undo/redo tracking.
        /// </summary>
        public static void MarkDirtyAndSave(Object obj)
        {
            if (obj == null) return;
            EditorUtility.SetDirty(obj);
            string path = RegisterAffectedAsset(obj);
            if (!string.IsNullOrEmpty(path))
            {
                InternalImportInProgress = true;
                try
                {
                    AssetDatabase.SaveAssets();
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                }
                finally
                {
                    InternalImportInProgress = false;
                }
            }
        }

        /// <summary>
        /// Registers an asset path as affected and force-imports it so the Project window
        /// reflects the change immediately. Does NOT call SaveAssets — use after the caller
        /// has already saved.
        /// </summary>
        public static void ImportAndRegister(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return;
            _affectedParentPaths.Add(assetPath);
            InternalImportInProgress = true;
            try
            {
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            }
            finally
            {
                InternalImportInProgress = false;
            }
        }

        /// <summary>
        /// Schedules a deferred call to RefreshAffectedAssets() via EditorApplication.delayCall.
        ///
        /// Use this from undo/redo handlers (OnUndoRedoPerformed) instead of calling
        /// RefreshAffectedAssets() directly.  The deferral is critical: when undoRedoPerformed
        /// fires, Unity's internal AssetDatabase sub-asset registry has NOT yet been updated to
        /// reflect the destroyed/restored sub-asset objects.  Calling SaveAssets or ImportAsset
        /// synchronously at that point causes "NativeFormatImporter generated inconsistent result"
        /// because the importer sees a different sub-asset set than the registry expects.
        ///
        /// By deferring one editor frame the registry has fully settled, and the subsequent
        /// SaveAssets + ImportAsset find a consistent state with no warning.
        ///
        /// A guard flag prevents duplicate deferred calls when the user presses Ctrl+Z rapidly.
        /// </summary>
        public static void ScheduleRefreshAffectedAssets()
        {
            if (_affectedParentPaths.Count == 0)
                return;

            if (_refreshScheduled)
                return;

            _refreshScheduled = true;
            EditorApplication.delayCall += ExecuteDeferredRefresh;
        }

        static void ExecuteDeferredRefresh()
        {
            _refreshScheduled = false;
            RefreshAffectedAssets();
        }

        /// <summary>
        /// Saves all dirty assets and refreshes the Project window for all registered affected paths.
        ///
        /// Why StartAssetEditing / StopAssetEditing:
        ///   • SaveAssets() not only writes files to disk but also triggers Unity's internal
        ///     NativeFormatImporter on each saved file immediately.  When an undo operation
        ///     removes a sub-asset, the importer detects that the sub-asset set in the file
        ///     now differs from the GUID-database cache and emits
        ///     "NativeFormatImporter generated inconsistent result".
        ///   • Wrapping SaveAssets() in StartAssetEditing()/StopAssetEditing() suspends all
        ///     automatic import during the save, then flushes the queued imports atomically
        ///     in StopAssetEditing().  Unity reconciles the GUID database and the on-disk
        ///     sub-asset list in one pass during that flush, which avoids the warning.
        ///   • A separate explicit ImportAsset() call is NOT needed and is intentionally
        ///     omitted: it would just trigger a redundant second consistency check.
        ///
        /// Does NOT clear the path set so that repeated undo/redo cycles keep working.
        /// </summary>
        public static void RefreshAffectedAssets()
        {
            if (_affectedParentPaths.Count == 0)
                return;

            // Suspend automatic import so SaveAssets() only writes to disk.
            InternalImportInProgress = true;
            AssetDatabase.StartAssetEditing();
            try
            {
                AssetDatabase.SaveAssets();
            }
            finally
            {
                // Flush all queued imports in one atomic pass.
                // Unity reconciles the GUID database here without the "inconsistent result" warning.
                // OnPostprocessAllAssets fires synchronously inside StopAssetEditing — the flag
                // must still be true at that point so ProfileAssetWatcher skips the notification.
                AssetDatabase.StopAssetEditing();
                InternalImportInProgress = false;
            }
        }
    }
}

