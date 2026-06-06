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
            // Ensure the Profile asset exists, then run the state machine after domain reload settles.
            Profile.LoadActiveProfile();
            EditorApplication.delayCall += RunInitializationFlow;
        }

        // ── State machine entry point ─────────────────────────────────────────────

        static void RunInitializationFlow()
        {
            var profile = Profile.Active;
            if (profile == null)
                return;

            var state = profile.InitializationState;
            if (state == ProfileInitializationState.InitializationCompleted)
            {
                CleanupStaleProDefine();
                CleanStaleNullReferences(profile);
                return;
            }

            int progressId = Progress.Start(ProgressTitle, "Starting...", Progress.Options.None);
            try
            {
                switch (state)
                {
                    case ProfileInitializationState.Uninitialized:
                        InitializeFreeVersion(profile, progressId);
                        break;

                    case ProfileInitializationState.FreeVersionInitialized:
                        CheckForProVersion(profile, progressId);
                        break;

                    case ProfileInitializationState.ProVersionInitialized:
                        CompleteProInitialization(profile, progressId);
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

        static void InitializeFreeVersion(Profile profile, int progressId)
        {
            Progress.Report(progressId, 0.1f, "Setting up workspace...");
            Debug.Log("[SecondBrain] First-time setup started.");

            if (profile.Children.Count == 0)
            {
                Progress.Report(progressId, 0.25f, "Creating default workspace...");
                CreateDefaultBase(profile);
                Debug.Log("[SecondBrain] Created default workspace 'My Workspace'.");
            }

            profile.InitializationState = ProfileInitializationState.FreeVersionInitialized;
            AssetDatabase.SaveAssets();

            Progress.Report(progressId, 0.5f, "Checking for Pro version...");
            CheckForProVersion(profile, progressId);
        }

        static void CheckForProVersion(Profile profile, int progressId)
        {
            Progress.Report(progressId, 0.65f, "Scanning for Pro assembly...");
            Debug.Log("[SecondBrain] Checking for Pro assembly...");

            if (IsProAssemblyPresent())
            {
                Progress.Report(progressId, 0.85f, "Pro detected — enabling...");
                Debug.Log("[SecondBrain] Pro assembly detected. Enabling SECOND_BRAIN_PRO — editor will recompile.");

                profile.InitializationState = ProfileInitializationState.ProVersionInitialized;
                AssetDatabase.SaveAssets();
                Progress.Finish(progressId, Progress.Status.Succeeded);
                ProLicenseUtils.AddProDefine();
            }
            else
            {
                Progress.Report(progressId, 0.9f, "Free version ready.");
                Debug.Log("[SecondBrain] Free version setup complete.");

                profile.InitializationState = ProfileInitializationState.InitializationCompleted;
                AssetDatabase.SaveAssets();
                Progress.Finish(progressId, Progress.Status.Succeeded);
                EditorApplication.delayCall += InstallerWindow.Open;
            }
        }

        static void CompleteProInitialization(Profile profile, int progressId)
        {
            Progress.Report(progressId, 0.4f, "Activating Pro features...");
            Debug.Log("[SecondBrain] Pro initialization complete — finalizing.");

            profile.InitializationState = ProfileInitializationState.InitializationCompleted;
            AssetDatabase.SaveAssets();

            Progress.Report(progressId, 0.9f, "Opening SecondBrain...");
            Debug.Log("[SecondBrain] SecondBrain Pro is ready. Opening window.");
            Progress.Finish(progressId, Progress.Status.Succeeded);

            EditorApplication.delayCall += () => EditorWindow.GetWindow<SecondBrainWindow>();
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        static void CreateDefaultBase(Profile profile)
        {
            var profilePath = AssetDatabase.GetAssetPath(profile);
            if (string.IsNullOrEmpty(profilePath))
                return;

            var defaultBase = ScriptableObject.CreateInstance<Base>();
            defaultBase.name = "My Workspace";
            AssetDatabase.AddObjectToAsset(defaultBase, profilePath);

            if (profile is IStructure profileStruct)
                profileStruct.AddChild(defaultBase);

            profile.DefaultBase = defaultBase;
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            SubAssetRefreshUtils.ImportAndRegister(profilePath);
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

        // Walk the full Profile tree, strip null child references, then delete any
        // ScriptableObject sub-assets that are embedded in the host files but no longer
        // referenced by any node in the tree.  Runs silently on every domain reload.
        static void CleanStaleNullReferences(Profile profile)
        {
            if (profile == null) return;

            bool nullsRemoved   = CleanNullsRecursive(profile);
            bool orphansRemoved = CleanOrphanedSubAssets(profile);

            if (nullsRemoved || orphansRemoved)
            {
                AssetDatabase.SaveAssets();
                Debug.Log("[SecondBrain] Cleaned stale data from the Profile tree.");
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
                case Profile p:
                    for (int i = p.Children.Count - 1; i >= 0; i--)
                        if (p.Children[i] == null) { p.Children.RemoveAt(i); removed = true; }
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
        static bool CleanOrphanedSubAssets(Profile profile)
        {
            if (profile == null) return false;

            // Build the referenced-ID set and the set of host .asset paths in one pass.
            var referencedIds = new HashSet<int>();
            var hostPaths     = new HashSet<string>();
            CollectReferencedIds(profile as IStructure, referencedIds, hostPaths);

            // Always include the Profile file itself even when it has no children yet.
            string profilePath = AssetDatabase.GetAssetPath(profile);
            if (!string.IsNullOrEmpty(profilePath))
                hostPaths.Add(profilePath);

            if (hostPaths.Count == 0) return false;

            bool anyRemoved = false;
            foreach (var path in hostPaths)
            {
                if (string.IsNullOrEmpty(path)) continue;
                if (!path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)) continue;

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
                if (!string.IsNullOrEmpty(p) && p.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)) paths.Add(p);
            }

            var children = node.ChildrenObjects;
            if (children == null) return;
            foreach (var child in children)
            {
                if (child == null) continue;
                ids.Add(child.GetInstanceID());
                string cp = AssetDatabase.GetAssetPath(child);
                if (!string.IsNullOrEmpty(cp) && cp.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)) paths.Add(cp);
                if (child is IStructure childStruct)
                    CollectReferencedIds(childStruct, ids, paths);
            }
        }

#if SECOND_BRAIN_DEV
        // ── DEV simulation menu items ─────────────────────────────────────────────
        static int StartDevProgress(string phase) =>
            Progress.Start($"{ProgressTitle} DEV", $"Simulating: {phase}");

        [MenuItem("Tools/Second Brain/DEV ─ Init/Simulate: Uninitialized Phase")]
        static void Dev_SimulateUninitialized()
        {
            var p = Profile.Active;
            if (p == null) return;
            p.InitializationState = ProfileInitializationState.Uninitialized;
            AssetDatabase.SaveAssets();
            Debug.Log("[SecondBrain DEV] State → Uninitialized — running phase now.");
            int id = StartDevProgress("Uninitialized");
            try   { InitializeFreeVersion(p, id); }
            catch { Progress.Finish(id, Progress.Status.Failed); throw; }
        }

        [MenuItem("Tools/Second Brain/DEV ─ Init/Simulate: Check for Pro Phase")]
        static void Dev_SimulateFreeInitialized()
        {
            var p = Profile.Active;
            if (p == null) return;
            p.InitializationState = ProfileInitializationState.FreeVersionInitialized;
            AssetDatabase.SaveAssets();
            Debug.Log("[SecondBrain DEV] State → FreeVersionInitialized — running phase now.");
            int id = StartDevProgress("Check for Pro");
            try   { CheckForProVersion(p, id); }
            catch { Progress.Finish(id, Progress.Status.Failed); throw; }
        }

        [MenuItem("Tools/Second Brain/DEV ─ Init/Simulate: Complete Pro Phase")]
        static void Dev_SimulateProInitialized()
        {
            var p = Profile.Active;
            if (p == null) return;
            p.InitializationState = ProfileInitializationState.ProVersionInitialized;
            AssetDatabase.SaveAssets();
            Debug.Log("[SecondBrain DEV] State → ProVersionInitialized — running phase now.");
            int id = StartDevProgress("Complete Pro");
            try   { CompleteProInitialization(p, id); }
            catch { Progress.Finish(id, Progress.Status.Failed); throw; }
        }

        [MenuItem("Tools/Second Brain/DEV ─ Init/Run from Current State")]
        static void Dev_RunFromCurrentState()
        {
            var p = Profile.Active;
            if (p == null) return;
            Debug.Log($"[SecondBrain DEV] Running init flow from state: {p.InitializationState}");
            RunInitializationFlow();
        }

        [MenuItem("Tools/Second Brain/DEV ─ Init/Reset State to Uninitialized (next reload)")]
        static void Dev_ResetStateOnly()
        {
            var p = Profile.Active;
            if (p == null) return;
            p.InitializationState = ProfileInitializationState.Uninitialized;
            AssetDatabase.SaveAssets();
            Debug.Log("[SecondBrain DEV] Init state reset to Uninitialized — will trigger on next domain reload.");
        }
#endif
    }
}
