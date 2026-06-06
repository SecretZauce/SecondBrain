using System;
using System.Collections.Generic;
using System.IO;
using SecretZauce.SecondBrain;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SecretZauce.SecondBrain.Editor
{
    [InitializeOnLoad]
    internal static class InitializationFlow
    {
        const string ProAsmdefName = "SecretZauce.SecondBrain.Pro.Editor";
        const string ProgressTitle = "SecondBrain";

        static InitializationFlow()
        {
            // Ensure the Motherbase asset exists, then run the state machine after domain reload settles.
            Motherbase.LoadInstance();
            EditorApplication.delayCall += RunInitializationFlow;
        }

        // ── State machine entry point ─────────────────────────────────────────────

        static void RunInitializationFlow()
        {
            var motherbase = Motherbase.Home;
            if (motherbase == null)
                return;

            var state = motherbase.InitializationState;
            if (state == MotherbaseInitializationState.InitializationCompleted)
            {
                CleanupStaleProDefine();
                CleanStaleNullReferences(motherbase);
                return;
            }

            int progressId = Progress.Start(ProgressTitle, "Starting...", Progress.Options.None);
            try
            {
                switch (state)
                {
                    case MotherbaseInitializationState.Uninitialized:
                        InitializeFreeVersion(motherbase, progressId);
                        break;

                    case MotherbaseInitializationState.FreeVersionInitialized:
                        CheckForProVersion(motherbase, progressId);
                        break;

                    case MotherbaseInitializationState.ProVersionInitialized:
                        CompleteProInitialization(motherbase, progressId);
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SecondBrain] Initialization failed: {ex.Message}");
                Progress.Finish(progressId, Progress.Status.Failed);
            }
        }

        // ── Phase methods ─────────────────────────────────────────────────────────

        static void InitializeFreeVersion(Motherbase motherbase, int progressId)
        {
            Progress.Report(progressId, 0.1f, "Setting up workspace...");
            Debug.Log("[SecondBrain] First-time setup started.");

            if (motherbase.Children.Count == 0)
            {
                Progress.Report(progressId, 0.25f, "Creating default workspace...");
                CreateDefaultBase(motherbase);
                Debug.Log("[SecondBrain] Created default workspace 'My Workspace'.");
            }

            motherbase.InitializationState = MotherbaseInitializationState.FreeVersionInitialized;
            AssetDatabase.SaveAssets();

            Progress.Report(progressId, 0.5f, "Checking for Pro version...");
            CheckForProVersion(motherbase, progressId);
        }

        static void CheckForProVersion(Motherbase motherbase, int progressId)
        {
            Progress.Report(progressId, 0.65f, "Scanning for Pro assembly...");
            Debug.Log("[SecondBrain] Checking for Pro assembly...");

            if (IsProAssemblyPresent())
            {
                Progress.Report(progressId, 0.85f, "Pro detected — enabling...");
                Debug.Log("[SecondBrain] Pro assembly detected. Enabling SECOND_BRAIN_PRO — editor will recompile.");

                motherbase.InitializationState = MotherbaseInitializationState.ProVersionInitialized;
                AssetDatabase.SaveAssets();
                Progress.Finish(progressId, Progress.Status.Succeeded);
                ProLicenseUtils.AddProDefine();
            }
            else
            {
                Progress.Report(progressId, 0.9f, "Free version ready.");
                Debug.Log("[SecondBrain] Free version setup complete.");

                motherbase.InitializationState = MotherbaseInitializationState.InitializationCompleted;
                AssetDatabase.SaveAssets();
                Progress.Finish(progressId, Progress.Status.Succeeded);
                EditorApplication.delayCall += InstallerWindow.Open;
            }
        }

        static void CompleteProInitialization(Motherbase motherbase, int progressId)
        {
            Progress.Report(progressId, 0.4f, "Activating Pro features...");
            Debug.Log("[SecondBrain] Pro initialization complete — finalizing.");

            motherbase.InitializationState = MotherbaseInitializationState.InitializationCompleted;
            AssetDatabase.SaveAssets();

            Progress.Report(progressId, 0.9f, "Opening SecondBrain...");
            Debug.Log("[SecondBrain] SecondBrain Pro is ready. Opening window.");
            Progress.Finish(progressId, Progress.Status.Succeeded);

            EditorApplication.delayCall += () => EditorWindow.GetWindow<SecondBrainWindow>();
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        static void CreateDefaultBase(Motherbase motherbase)
        {
            var motherbasePath = AssetDatabase.GetAssetPath(motherbase);
            if (string.IsNullOrEmpty(motherbasePath))
                return;

            var defaultBase = ScriptableObject.CreateInstance<Base>();
            defaultBase.name = "My Workspace";
            AssetDatabase.AddObjectToAsset(defaultBase, motherbasePath);

            if (motherbase is IStructure motherbaseStruct)
                motherbaseStruct.AddChild(defaultBase);

            motherbase.DefaultBase = defaultBase;
            EditorUtility.SetDirty(motherbase);
            AssetDatabase.SaveAssets();
            SubAssetRefreshUtils.ImportAndRegister(motherbasePath);
        }

        static bool IsProAssemblyPresent()
        {
            var guids = AssetDatabase.FindAssets(
                $"{ProAsmdefName} t:AssemblyDefinitionAsset",
                new[] { "Assets" });
            return guids.Length > 0;
        }

        // If SECOND_BRAIN_PRO is defined but the Pro asmdef is gone (package removed),
        // strip the stale define so the next reload compiles cleanly without Pro.
        static void CleanupStaleProDefine()
        {
            if (ProLicenseUtils.IsProDefineActive() && !IsProAssemblyPresent())
            {
                Debug.LogWarning("[SecondBrain] Pro assembly not found but SECOND_BRAIN_PRO define is active — removing stale define.");
                ProLicenseUtils.RemoveProDefine();
            }
        }

        // Walk the full Motherbase tree, strip null child references, then delete any
        // ScriptableObject sub-assets that are embedded in the host files but no longer
        // referenced by any node in the tree.  Runs silently on every domain reload.
        static void CleanStaleNullReferences(Motherbase motherbase)
        {
            if (motherbase == null) return;

            bool nullsRemoved   = CleanNullsRecursive(motherbase);
            bool orphansRemoved = CleanOrphanedSubAssets(motherbase);

            if (nullsRemoved || orphansRemoved)
            {
                AssetDatabase.SaveAssets();
                Debug.Log("[SecondBrain] Cleaned stale data from the Motherbase tree.");
            }
        }

        // Returns true when at least one null entry was removed from this node or any descendant.
        static bool CleanNullsRecursive(IStructure node)
        {
            if (node == null) return false;
            bool removed = false;

            // Index-based removal so Unity fake-null objects are found correctly.
            switch (node)
            {
                case Motherbase m:
                    for (int i = m.Children.Count - 1; i >= 0; i--)
                        if (m.Children[i] == null) { m.Children.RemoveAt(i); removed = true; }
                    break;
                case Base b:
                    for (int i = b.Children.Count - 1; i >= 0; i--)
                        if (b.Children[i] == null) { b.Children.RemoveAt(i); removed = true; }
                    break;
                case Container c:
                    for (int i = c.children.Count - 1; i >= 0; i--)
                        if (c.children[i] == null) { c.children.RemoveAt(i); removed = true; }
                    break;
            }

            if (removed && node is UnityEngine.Object obj)
                EditorUtility.SetDirty(obj);

            // Recurse into remaining children.
            var children = node.ChildrenObjects;
            if (children != null)
            {
                foreach (var child in children)
                    if (child is IStructure childStruct)
                        removed |= CleanNullsRecursive(childStruct);
            }

            return removed;
        }

        // Delete ScriptableObject sub-assets embedded in the SecondBrain host files that
        // are no longer reachable from any node in the live tree.
        // Returns true when at least one orphan was removed.
        static bool CleanOrphanedSubAssets(Motherbase motherbase)
        {
            if (motherbase == null) return false;

            // Build the referenced-ID set and the set of host .asset paths in one pass.
            var referencedIds = new HashSet<int>();
            var hostPaths     = new HashSet<string>();
            CollectReferencedIds(motherbase as IStructure, referencedIds, hostPaths);

            // Always include the Motherbase file itself even when it has no children yet.
            string motherbasePath = AssetDatabase.GetAssetPath(motherbase);
            if (!string.IsNullOrEmpty(motherbasePath))
                hostPaths.Add(motherbasePath);

            if (hostPaths.Count == 0) return false;

            bool anyRemoved = false;
            foreach (var path in hostPaths)
            {
                if (string.IsNullOrEmpty(path)) continue;

                Object[] allAtPath;
                try { allAtPath = AssetDatabase.LoadAllAssetsAtPath(path); }
                catch { continue; }
                if (allAtPath == null) continue;

                foreach (var asset in allAtPath)
                {
                    if (asset == null) continue;
                    if (!AssetDatabase.IsSubAsset(asset)) continue;
                    if (asset is not ScriptableObject) continue;
                    if (referencedIds.Contains(asset.GetInstanceID())) continue;

                    Debug.Log($"[SecondBrain] Removing orphaned sub-asset '{asset.name}' ({asset.GetType().Name}) from {Path.GetFileName(path)}");
                    AssetDatabase.RemoveObjectFromAsset(asset);
                    Object.DestroyImmediate(asset, true);
                    anyRemoved = true;
                }
            }

            return anyRemoved;
        }

        // Recursively adds instance IDs of all live tree nodes to <paramref name="ids"/>
        // and the .asset file path of each node to <paramref name="paths"/>.
        static void CollectReferencedIds(IStructure node, HashSet<int> ids, HashSet<string> paths)
        {
            if (node == null) return;
            if (node is Object obj)
            {
                ids.Add(obj.GetInstanceID());
                string p = AssetDatabase.GetAssetPath(obj);
                if (!string.IsNullOrEmpty(p)) paths.Add(p);
            }

            var children = node.ChildrenObjects;
            if (children == null) return;
            foreach (var child in children)
            {
                if (child == null) continue;
                ids.Add(child.GetInstanceID());
                string cp = AssetDatabase.GetAssetPath(child);
                if (!string.IsNullOrEmpty(cp)) paths.Add(cp);
                if (child is IStructure childStruct)
                    CollectReferencedIds(childStruct, ids, paths);
            }
        }

#if SECOND_BRAIN_DEV
        // ── DEV simulation menu items ─────────────────────────────────────────────
        // Each item sets the state and immediately runs that phase with its own
        // progress item so the developer can observe output in the Console.

        static int StartDevProgress(string phase) =>
            Progress.Start($"{ProgressTitle} DEV", $"Simulating: {phase}");

        [MenuItem("Tools/Second Brain/DEV ─ Init/Simulate: Uninitialized Phase")]
        static void Dev_SimulateUninitialized()
        {
            var mb = Motherbase.Home;
            if (mb == null) return;
            mb.InitializationState = MotherbaseInitializationState.Uninitialized;
            AssetDatabase.SaveAssets();
            Debug.Log("[SecondBrain DEV] State → Uninitialized — running phase now.");
            int id = StartDevProgress("Uninitialized");
            try   { InitializeFreeVersion(mb, id); }
            catch { Progress.Finish(id, Progress.Status.Failed); throw; }
        }

        [MenuItem("Tools/Second Brain/DEV ─ Init/Simulate: Check for Pro Phase")]
        static void Dev_SimulateFreeInitialized()
        {
            var mb = Motherbase.Home;
            if (mb == null) return;
            mb.InitializationState = MotherbaseInitializationState.FreeVersionInitialized;
            AssetDatabase.SaveAssets();
            Debug.Log("[SecondBrain DEV] State → FreeVersionInitialized — running phase now.");
            int id = StartDevProgress("Check for Pro");
            try   { CheckForProVersion(mb, id); }
            catch { Progress.Finish(id, Progress.Status.Failed); throw; }
        }

        [MenuItem("Tools/Second Brain/DEV ─ Init/Simulate: Complete Pro Phase")]
        static void Dev_SimulateProInitialized()
        {
            var mb = Motherbase.Home;
            if (mb == null) return;
            mb.InitializationState = MotherbaseInitializationState.ProVersionInitialized;
            AssetDatabase.SaveAssets();
            Debug.Log("[SecondBrain DEV] State → ProVersionInitialized — running phase now.");
            int id = StartDevProgress("Complete Pro");
            try   { CompleteProInitialization(mb, id); }
            catch { Progress.Finish(id, Progress.Status.Failed); throw; }
        }

        [MenuItem("Tools/Second Brain/DEV ─ Init/Run from Current State")]
        static void Dev_RunFromCurrentState()
        {
            var mb = Motherbase.Home;
            if (mb == null) return;
            Debug.Log($"[SecondBrain DEV] Running init flow from state: {mb.InitializationState}");
            RunInitializationFlow();
        }

        [MenuItem("Tools/Second Brain/DEV ─ Init/Reset State to Uninitialized (next reload)")]
        static void Dev_ResetStateOnly()
        {
            var mb = Motherbase.Home;
            if (mb == null) return;
            mb.InitializationState = MotherbaseInitializationState.Uninitialized;
            AssetDatabase.SaveAssets();
            Debug.Log("[SecondBrain DEV] Init state reset to Uninitialized — will trigger on next domain reload.");
        }
#endif
    }
}
