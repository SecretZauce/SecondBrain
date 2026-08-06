using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SecretZauce.SecondBrain.Editor
{
    [InitializeOnLoad]
    internal static class InitializationFlow
    {
        const string ProAsmdefName          = "SecretZauce.SecondBrain.Pro.Editor";
        const string ProBootstrapAsmdefName = "SecretZauce.SecondBrain.Pro.Bootstrap";
        const string ProgressTitle          = "SecondBrain";

        // ── Pro-presence tracking ─────────────────────────────────────────────────
        // InitializationCompleted is a terminal state, and it does not record which edition it
        // was reached with. Without these flags, importing Pro into a project whose free setup
        // had already completed leaves the flow inert: ProBootstrapper adds SECOND_BRAIN_PRO and
        // recompiles, but nothing finalizes Pro or opens the Installer. Recording whether Pro was
        // present at completion turns a later Pro import into a detectable change.
        const int StepProPresenceRecorded = 1 << 0;
        const int StepProPresent          = 1 << 1;

        // Session-scoped so the "awaiting the define" notice is logged once per editor session
        // rather than on every domain reload while the define is deliberately withheld.
        const string ProActivationNoticeKey = "SecondBrain.Free.ProActivationNoticeShown";

        static InitializationFlow()
        {
            // Ensure the Profile asset exists, seed the registry, then run the state machine.
            var activeProfile = Profile.LoadActiveProfile();
            SecondBrainCore.Instance.RegisterProfile(activeProfile);
            EditorApplication.delayCall += RunInitializationFlow;
        }

        // ── State machine entry point ─────────────────────────────────────────────

        static void RunInitializationFlow()
        {
            var profile = Profile.Active;
            if (profile == null)
                return;

            var core = SecondBrainCore.Instance;
            var state = core.InitializationState;
            if (state == ProfileInitializationState.InitializationCompleted)
            {
                CleanupStaleProDefine();
                CleanStaleNullReferences(profile);
                ResumeForProEditionChange(core);
                return;
            }

            int progressId = Progress.Start(ProgressTitle, "Starting...", Progress.Options.None);
            try
            {
                switch (state)
                {
                    case ProfileInitializationState.Uninitialized:
                        InitializeFreeVersion(profile, core, progressId);
                        break;

                    case ProfileInitializationState.FreeVersionInitialized:
                        CheckForProVersion(core, progressId);
                        break;

                    case ProfileInitializationState.ProVersionInitialized:
                        CompleteProInitialization(core, progressId);
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

        static void InitializeFreeVersion(Profile profile, SecondBrainCore core, int progressId)
        {
            Progress.Report(progressId, 0.1f, "Setting up workspace...");
            Debug.Log("[SecondBrain] First-time setup started.");

            if (profile.Children.Count == 0)
            {
                Progress.Report(progressId, 0.25f, "Creating default workspace...");
                CreateDefaultBase(profile);
                Debug.Log("[SecondBrain] Created default workspace 'My Workspace'.");
            }

            core.InitializationState = ProfileInitializationState.FreeVersionInitialized;
            AssetDatabase.SaveAssets();

            Progress.Report(progressId, 0.5f, "Checking for Pro version...");
            CheckForProVersion(core, progressId);
        }

        static void CheckForProVersion(SecondBrainCore core, int progressId)
        {
            Progress.Report(progressId, 0.65f, "Scanning for Pro assembly...");
            Debug.Log("[SecondBrain] Checking for Pro assembly...");

            if (IsProAssemblyPresent())
            {
                Progress.Report(progressId, 0.85f, "Pro detected — enabling...");
                core.InitializationState = ProfileInitializationState.ProVersionInitialized;
                AssetDatabase.SaveAssets();

                // ProBootstrapper (Pro.Bootstrap assembly) is the single owner of the
                // SECOND_BRAIN_PRO define lifecycle. When the define is still absent it adds it,
                // and the resulting recompile brings us back here in the ProVersionInitialized
                // state. When the define is ALREADY set — the Pro-first install path, where
                // ProBootstrapper set it from Events.registeringPackages — no recompile is coming,
                // so waiting for one would strand the flow forever. Finish inline instead.
                if (ProLicenseUtils.IsProDefineActive())
                {
                    Debug.Log("[SecondBrain] Pro assembly detected and SECOND_BRAIN_PRO is already active — finalizing now.");
                    CompleteProInitialization(core, progressId);
                    return;
                }

                Debug.Log("[SecondBrain] Pro assembly detected — awaiting SECOND_BRAIN_PRO define from the Pro bootstrapper.");
                Progress.Finish(progressId, Progress.Status.Succeeded);
                EditorApplication.delayCall += EnsureProDefineWasAdded;
            }
            else
            {
                Progress.Report(progressId, 0.9f, "Free version ready.");
                Debug.Log("[SecondBrain] Free version setup complete.");

                core.InitializationState = ProfileInitializationState.InitializationCompleted;
                RecordProPresence(core, false);
                Progress.Finish(progressId, Progress.Status.Succeeded);
                EditorApplication.delayCall += InstallerWindow.Open;
            }
        }

        static void CompleteProInitialization(SecondBrainCore core, int progressId)
        {
            Progress.Report(progressId, 0.4f, "Activating Pro features...");
            Debug.Log("[SecondBrain] Pro initialization complete — finalizing.");

            core.InitializationState = ProfileInitializationState.InitializationCompleted;
            RecordProPresence(core, true);

            Progress.Report(progressId, 0.9f, "Opening SecondBrain...");
            Debug.Log("[SecondBrain] SecondBrain Pro is ready. Opening installer.");
            Progress.Finish(progressId, Progress.Status.Succeeded);

            EditorApplication.delayCall += InstallerWindow.Open;
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        // Re-entry point for the terminal InitializationCompleted state. Handles the edition
        // changing after setup already finished — most importantly Pro being imported into a
        // project where the free package was installed (and initialized) first. That import
        // produces no state transition of its own, so without this the run ends at
        // ProBootstrapper's "adding SECOND_BRAIN_PRO define" log and the Installer never opens.
        static void ResumeForProEditionChange(SecondBrainCore core)
        {
            bool proPresent      = IsProAssemblyPresent();
            bool proDefineActive = ProLicenseUtils.IsProDefineActive();

            if (!core.HasInitializationStepCompleted(StepProPresenceRecorded))
            {
                // First reload after upgrading to a build that tracks presence. Adopt whatever
                // the project already looks like, silently, so installs that were set up long
                // ago are not re-announced. The exception is Pro present without the define:
                // that is a genuine mid-activation, so it falls through to the resume path.
                if (!proPresent || proDefineActive)
                {
                    RecordProPresence(core, proPresent);
                    return;
                }
            }
            else if (proPresent == core.HasInitializationStepCompleted(StepProPresent))
            {
                return; // Nothing changed since the last reload.
            }

            if (!proPresent)
            {
                // Pro was removed — CleanupStaleProDefine has already stripped the define.
                RecordProPresence(core, false);
                return;
            }

            if (!proDefineActive)
            {
                // ProBootstrapper adds the define and the recompile it triggers brings us back
                // here with it active. Leave the recorded presence untouched so that pass still
                // sees a change.
                if (!SessionState.GetBool(ProActivationNoticeKey, false))
                {
                    SessionState.SetBool(ProActivationNoticeKey, true);
                    Debug.Log(
                        "[SecondBrain] Pro assembly detected after setup completed — awaiting " +
                        "SECOND_BRAIN_PRO define from the Pro bootstrapper.");
                    EditorApplication.delayCall += EnsureProDefineWasAdded;
                }
                return;
            }

            SessionState.SetBool(ProActivationNoticeKey, false);

            int progressId = Progress.Start(ProgressTitle, "Activating Pro features...", Progress.Options.None);
            try
            {
                CompleteProInitialization(core, progressId);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SecondBrain] Pro activation failed: {ex.Message}");
                Progress.Finish(progressId, Progress.Status.Failed);
            }
        }

        static void RecordProPresence(SecondBrainCore core, bool proPresent)
        {
            core.MarkInitializationStepCompleted(StepProPresenceRecorded);
            if (proPresent)
                core.MarkInitializationStepCompleted(StepProPresent);
            else
                core.ClearInitializationStep(StepProPresent);

            AssetDatabase.SaveAssets();
        }

        // Watchdog for the "awaiting the define" branches of CheckForProVersion and
        // ResumeForProEditionChange. ProBootstrapper normally adds SECOND_BRAIN_PRO in the same
        // delayCall cycle, and the recompile it triggers re-enters the flow. If Pro shipped
        // without its bootstrap assembly, nothing would ever advance the flow and the installer
        // would never appear — add the define here instead. When the bootstrap assembly *is*
        // present it owns the define and this stands down (see below).
        // ToggleDefine is idempotent, so a redundant call is a no-op.
        static void EnsureProDefineWasAdded()
        {
            if (ProLicenseUtils.IsProDefineActive()) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return; // bootstrapper's recompile is already in flight
            if (!IsProAssemblyPresent()) return;

            // The Pro bootstrap assembly compiles unconditionally and owns the define lifecycle,
            // including deliberately withholding the define when the Free/Pro compatibility
            // versions disagree. Setting it behind that gate's back would ping-pong recompiles.
            if (IsProBootstrapAssemblyPresent()) return;

            Debug.LogWarning("[SecondBrain] SECOND_BRAIN_PRO was not set by the Pro bootstrapper — adding it directly. Scripts will recompile.");
            ProLicenseUtils.AddProDefine();
        }

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

        static bool IsProBootstrapAssemblyPresent()
        {
            var guids = AssetDatabase.FindAssets(
                $"{ProBootstrapAsmdefName} t:AssemblyDefinitionAsset",
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

                // Collect candidates first instead of destroying inline. This lets us apply a
                // whole-file safety check before touching anything.
                var candidates = new List<Object>();
                int scriptableObjectSubAssetCount = 0;
                foreach (var asset in allAtPath)
                {
                    if (asset == null) continue;
                    if (!AssetDatabase.IsSubAsset(asset)) continue;
                    if (asset is not ScriptableObject) continue;

                    scriptableObjectSubAssetCount++;
                    if (!referencedIds.Contains(asset.GetInstanceID()))
                        candidates.Add(asset);
                }

                if (candidates.Count == 0) continue;

                // Safety check: if every single sub-asset in this file would be deleted, the
                // live tree we walked is almost certainly incomplete or resolved to the wrong
                // Profile/Base (e.g. a transient mis-resolution right after a domain reload)
                // rather than the file genuinely being all-orphaned. Refuse to wipe an entire
                // file in one pass — leave it untouched and surface it loudly so it can be
                // investigated instead of silently destroying data.
                if (candidates.Count == scriptableObjectSubAssetCount)
                {
                    Debug.LogError(
                        $"[SecondBrain] Skipped orphaned sub-asset cleanup for '{Path.GetFileName(path)}' — " +
                        $"all {scriptableObjectSubAssetCount} ScriptableObject sub-asset(s) appeared unreferenced, " +
                        "which usually means the active Profile was resolved incorrectly rather than the file genuinely containing only orphans. No data was removed; please verify this file manually.");
                    continue;
                }

                foreach (var asset in candidates)
                {
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
            var core = SecondBrainCore.Instance;
            core.InitializationState = ProfileInitializationState.Uninitialized;
            AssetDatabase.SaveAssets();
            Debug.Log("[SecondBrain DEV] State → Uninitialized — running phase now.");
            int id = StartDevProgress("Uninitialized");
            try   { InitializeFreeVersion(p, core, id); }
            catch { Progress.Finish(id, Progress.Status.Failed); throw; }
        }

        [MenuItem("Tools/Second Brain/DEV ─ Init/Simulate: Check for Pro Phase")]
        static void Dev_SimulateFreeInitialized()
        {
            var core = SecondBrainCore.Instance;
            core.InitializationState = ProfileInitializationState.FreeVersionInitialized;
            AssetDatabase.SaveAssets();
            Debug.Log("[SecondBrain DEV] State → FreeVersionInitialized — running phase now.");
            int id = StartDevProgress("Check for Pro");
            try   { CheckForProVersion(core, id); }
            catch { Progress.Finish(id, Progress.Status.Failed); throw; }
        }

        [MenuItem("Tools/Second Brain/DEV ─ Init/Simulate: Complete Pro Phase")]
        static void Dev_SimulateProInitialized()
        {
            var core = SecondBrainCore.Instance;
            core.InitializationState = ProfileInitializationState.ProVersionInitialized;
            AssetDatabase.SaveAssets();
            Debug.Log("[SecondBrain DEV] State → ProVersionInitialized — running phase now.");
            int id = StartDevProgress("Complete Pro");
            try   { CompleteProInitialization(core, id); }
            catch { Progress.Finish(id, Progress.Status.Failed); throw; }
        }

        // Reproduces "free installed and initialized first, Pro imported afterwards" without
        // reinstalling anything: the completed state is told it finished as Free-only, so the
        // Pro assembly already on disk registers as a new arrival.
        [MenuItem("Tools/Second Brain/DEV ─ Init/Simulate: Pro Imported After Setup")]
        static void Dev_SimulateProImportedAfterSetup()
        {
            var core = SecondBrainCore.Instance;
            core.InitializationState = ProfileInitializationState.InitializationCompleted;
            RecordProPresence(core, false);
            SessionState.SetBool(ProActivationNoticeKey, false);
            Debug.Log("[SecondBrain DEV] Recorded edition → Free-only — running edition-change check now.");
            ResumeForProEditionChange(core);
        }

        [MenuItem("Tools/Second Brain/DEV ─ Init/Run from Current State")]
        static void Dev_RunFromCurrentState()
        {
            Debug.Log($"[SecondBrain DEV] Running init flow from state: {SecondBrainCore.Instance.InitializationState}");
            RunInitializationFlow();
        }

        [MenuItem("Tools/Second Brain/DEV ─ Init/Reset State to Uninitialized (next reload)")]
        static void Dev_ResetStateOnly()
        {
            var core = SecondBrainCore.Instance;
            core.InitializationState = ProfileInitializationState.Uninitialized;
            AssetDatabase.SaveAssets();
            Debug.Log("[SecondBrain DEV] Init state reset to Uninitialized — will trigger on next domain reload.");
        }
#endif
    }
}
