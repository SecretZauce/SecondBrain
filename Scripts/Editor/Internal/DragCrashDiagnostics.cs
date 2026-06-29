// ─────────────────────────────────────────────────────────────────────────────
// TEMPORARY DIAGNOSTIC — remove once the transfer→undo→drag crash is root-caused.
//
// Writes a flush-per-line trail to Logs/sb-dragdiag.log so the log survives a
// native Unity crash (Size overflow in allocator / FetchDataFromDrag). The last
// lines before the crash name the exact SaveAssets / import / undo that preceded
// it, with the C# call stack of every SaveAssets that touches Motherbase.asset.
//
// To remove: delete this file (and its .meta).
// ─────────────────────────────────────────────────────────────────────────────
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SecretZauce.SecondBrain.Editor
{
    public static class DragCrashDiagnostics
    {
        // Match the Motherbase asset (and any future sub-asset parent file) by name.
        public const string TargetMarker = "Motherbase";

        static readonly string LogPath =
            Path.Combine(Directory.GetCurrentDirectory(), "Logs", "sb-dragdiag.log");

        public static void Log(string msg)
        {
            try
            {
                var dir = Path.GetDirectoryName(LogPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                // Open/append/flush/close every line so nothing is buffered when the
                // process is killed by the native allocator fault.
                using var fs = new FileStream(LogPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                using var sw = new StreamWriter(fs);
                sw.WriteLine($"[{DateTime.Now:HH:mm:ss.fff} f{Time.frameCount}] {msg}");
                sw.Flush();
                fs.Flush(true);
            }
            catch { /* diagnostics must never throw */ }
        }
    }

    [InitializeOnLoad]
    static class DragCrashLifecycle
    {
        static DragCrashLifecycle()
        {
            DragCrashDiagnostics.Log("================ DOMAIN RELOAD / session start ================");
            Undo.undoRedoPerformed += () =>
                DragCrashDiagnostics.Log($"undoRedoPerformed\nSTACK:\n{Environment.StackTrace}");
        }
    }

    /// <summary>Logs the call stack of every SaveAssets / save that includes Motherbase.</summary>
    class DragCrashSaveWatcher : UnityEditor.AssetModificationProcessor
    {
        static string[] OnWillSaveAssets(string[] paths)
        {
            if (paths != null)
            {
                foreach (var p in paths)
                {
                    if (p != null && p.Contains(DragCrashDiagnostics.TargetMarker))
                    {
                        DragCrashDiagnostics.Log(
                            $"OnWillSaveAssets Motherbase. paths=[{string.Join(", ", paths)}]\nSTACK:\n{Environment.StackTrace}");
                        break;
                    }
                }
            }
            return paths;
        }
    }

    /// <summary>Logs every Motherbase import (each entry = one reimport pass).</summary>
    class DragCrashImportWatcher : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(
            string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (importedAssets == null) return;
            foreach (var p in importedAssets)
                if (p != null && p.Contains(DragCrashDiagnostics.TargetMarker))
                    DragCrashDiagnostics.Log($"OnPostprocessAllAssets IMPORTED: {p}");
        }
    }
}
