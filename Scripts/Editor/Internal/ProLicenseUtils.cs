using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace SecretZauce.SecondBrain.Editor
{
    /// <summary>
    /// Centralises PRO licence helpers:
    ///  - Asset Store URL (update when published).
    ///  - Scripting-define toggle for enabling/disabling the Pro assembly.
    ///  - DEV + PRO "Rollback to Free" menu item.
    /// </summary>
    public static class ProLicenseUtils
    {
        /// <summary>
        /// Unity Asset Store page for SecondBrain PRO.
        /// </summary>
        public const string ASSET_STORE_URL = "https://assetstore.unity.com/packages/slug/383598";

        const string ProDefine = "SECOND_BRAIN_PRO";

        /// <summary>
        /// Adds SECOND_BRAIN_PRO to all build platforms, triggering a recompile.
        /// Called automatically when the Pro assembly is detected during initialization.
        /// </summary>
        public static void AddProDefine()
        {
            ToggleDefine(ProDefine, add: true);
#if SECOND_BRAIN_DEV
            Debug.Log($"[SecondBrain DEV] '{ProDefine}' added to all platforms — scripts will recompile.");
#endif
        }

        /// <summary>
        /// Removes SECOND_BRAIN_PRO from all build platforms, triggering a recompile.
        /// </summary>
        public static void RemoveProDefine()
        {
            ToggleDefine(ProDefine, add: false);
#if SECOND_BRAIN_DEV
            Debug.Log($"[SecondBrain DEV] '{ProDefine}' removed from all platforms — scripts will recompile.");
#endif
        }

        /// <summary>
        /// Returns true if SECOND_BRAIN_PRO is set on the active build target.
        /// </summary>
        public static bool IsProDefineActive()
        {
            try
            {
                var target  = NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
                var defines = PlayerSettings.GetScriptingDefineSymbols(target);
                return defines.Contains(ProDefine);
            }
            catch
            {
                return false;
            }
        }

        static void ToggleDefine(string define, bool add)
        {
            var validGroups = ((BuildTargetGroup[])Enum.GetValues(typeof(BuildTargetGroup)))
                .Where(g => g != BuildTargetGroup.Unknown)
                .ToArray();

            foreach (var group in validGroups)
            {
                try
                {
                    var namedTarget = NamedBuildTarget.FromBuildTargetGroup(group);
                    var current = PlayerSettings.GetScriptingDefineSymbols(namedTarget);
                    var symbols = current
                        .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .ToList();

                    if (add && !symbols.Contains(define))
                    {
                        symbols.Add(define);
                        PlayerSettings.SetScriptingDefineSymbols(namedTarget, string.Join(";", symbols));
                    }
                    else if (!add && symbols.Contains(define))
                    {
                        symbols.Remove(define);
                        PlayerSettings.SetScriptingDefineSymbols(namedTarget, string.Join(";", symbols));
                    }
                }
                catch
                {
                    // Unsupported groups may throw — skip silently.
                }
            }

            AssetDatabase.SaveAssets();
        }

    }
}
