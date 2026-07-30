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
        /// Marks the object dirty, saves that one asset, and force-imports its path so the
        /// Project window reflects the change immediately.
        /// Also registers the path as affected for undo/redo tracking.
        /// </summary>
        public static void MarkDirtyAndSave(Object obj)
        {
            if (obj == null) return;
            EditorUtility.SetDirty(obj);
            string path = RegisterAffectedAsset(obj);
            if (!string.IsNullOrEmpty(path))
            {
                // SaveAssetIfDirty, not SaveAssets: see the note on SaveOnly below.
                AssetDatabase.SaveAssetIfDirty(obj);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
        }

        /// <summary>
        /// Writes only the given asset paths, and only if they are actually dirty.
        ///
        /// Every save in this class routes through here rather than calling
        /// <c>AssetDatabase.SaveAssets()</c>. SaveAssets is project-global: it flushes EVERY
        /// dirty asset in the project, not just SecondBrain's own. That made the window
        /// reach far outside its own data — an undo performed with the window open would
        /// write and reimport whatever unrelated asset happened to be dirty at that moment
        /// (a RenderTexture, a material), which looked like SecondBrain randomly reimporting
        /// one specific asset. It also silently persisted edits the user had not chosen to save.
        ///
        /// Wrapped in StartAssetEditing/StopAssetEditing so the writes flush as one atomic
        /// import pass, for the reasons documented on <see cref="RefreshAffectedAssets"/>.
        /// </summary>
        static void SaveOnly(IEnumerable<string> assetPaths)
        {
            if (assetPaths == null) return;

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var path in assetPaths)
                {
                    if (string.IsNullOrEmpty(path)) continue;

                    var mainAsset = AssetDatabase.LoadMainAssetAtPath(path);
                    if (mainAsset == null) continue;

                    // No-ops when the asset is clean, which is what keeps a selection-only
                    // undo from touching the disk at all.
                    AssetDatabase.SaveAssetIfDirty(mainAsset);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
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
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }

        // Guard flag for SaveDirtyAssetsDeferred — collapses a burst of edits into one save.
        static bool _deferredSaveScheduled;

        // Assets awaiting the next deferred save. Kept separate from _affectedParentPaths, which
        // persists across undo/redo cycles; this set is consumed and cleared on each flush.
        static readonly HashSet<string> _pendingSavePaths = new HashSet<string>();

        /// <summary>
        /// Saves dirty assets once on the next editor tick, with automatic import suspended.
        ///
        /// Use this for lightweight property edits made from interactive UI — label colours,
        /// emoji icons — where the old behaviour of calling <c>AssetDatabase.SaveAssets()</c>
        /// synchronously on every click was the cause of a visible asset import per click:
        ///   • SaveAssets() writes the owning asset and immediately runs NativeFormatImporter
        ///     on it. For a Profile holding many sub-assets that reimport is slow enough to
        ///     stall the editor and flash the Project window on each colour swatch pressed.
        ///   • Deferring to delayCall means click-dragging across a palette, or applying a
        ///     style to every container in a Base, produces exactly one save instead of N.
        ///   • StartAssetEditing/StopAssetEditing suspends import during the write and flushes
        ///     it atomically afterwards, for the same reasons documented on
        ///     <see cref="RefreshAffectedAssets"/>.
        ///
        /// The edit is already live in memory and recorded with Undo before this runs, so the
        /// window reflects the change immediately regardless of when the save lands.
        /// </summary>
        public static void SaveDirtyAssetsDeferred()
        {
            if (_deferredSaveScheduled)
                return;

            _deferredSaveScheduled = true;
            EditorApplication.delayCall += ExecuteDeferredSave;
        }

        /// <summary>
        /// Records the asset owning <paramref name="obj"/> as needing a deferred save.
        /// Call this alongside EditorUtility.SetDirty before SaveDirtyAssetsDeferred so the
        /// save stays scoped to SecondBrain's own assets — see <see cref="SaveOnly"/>.
        /// </summary>
        public static void RegisterPendingSave(Object obj)
        {
            if (obj == null) return;
            string path = AssetDatabase.GetAssetPath(obj);
            if (!string.IsNullOrEmpty(path))
                _pendingSavePaths.Add(path);
        }

        static void ExecuteDeferredSave()
        {
            _deferredSaveScheduled = false;

            if (_pendingSavePaths.Count == 0)
                return;

            SaveOnly(_pendingSavePaths);
            _pendingSavePaths.Clear();
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
        /// Saves the registered affected assets — and only those — refreshing the Project window
        /// for them.
        ///
        /// Why the save is scoped to the tracked paths:
        ///   • This runs from OnUndoRedoPerformed on EVERY undo, including one that only moved
        ///     the selection. The path set is deliberately never cleared here so repeated
        ///     undo/redo cycles keep working, which means it stays populated for the rest of the
        ///     session after the first structural edit — so this method really does run on plain
        ///     selection undos.
        ///   • It previously called AssetDatabase.SaveAssets(), which is project-global. On a
        ///     selection undo none of SecondBrain's own assets were dirty, but any unrelated
        ///     dirty asset in the user's project got written and reimported as collateral.
        ///   • Saving per-path via SaveAssetIfDirty makes a selection-only undo a no-op, since
        ///     nothing SecondBrain owns is dirty.
        ///
        /// Why StartAssetEditing / StopAssetEditing (inside <see cref="SaveOnly"/>):
        ///   • Saving does not only write files to disk, it also triggers Unity's internal
        ///     NativeFormatImporter on each saved file immediately.  When an undo operation
        ///     removes a sub-asset, the importer detects that the sub-asset set in the file
        ///     now differs from the GUID-database cache and emits
        ///     "NativeFormatImporter generated inconsistent result".
        ///   • Wrapping the writes in StartAssetEditing()/StopAssetEditing() suspends all
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

            SaveOnly(_affectedParentPaths);
        }
    }
}

