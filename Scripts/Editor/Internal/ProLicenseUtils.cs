using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace SecretZauce.SecondBrain.Editor
{
    /// <summary>
    /// Asset Store URL, plus the scripting-define toggles used by the DEV edition mocks.
    ///
    /// The define is NOT how Pro is enabled in a real install — the Pro asmdef's versionDefines
    /// entry is (see CLAUDE.md, "What sets SECOND_BRAIN_PRO"). These toggles remain because in
    /// this dev repo, where free is not a UPM package, PlayerSettings is what compiles Pro.
    /// To ask whether Pro is active, check <c>ProFeature.Provider != null</c>.
    /// </summary>
    public static class ProLicenseUtils
    {
        /// <summary>
        /// Unity Asset Store page for SecondBrain PRO.
        /// </summary>
        public const string ASSET_STORE_URL = "https://assetstore.unity.com/packages/slug/383598";

        const string ProDefine = "SECOND_BRAIN_PRO";

        /// <summary>
        /// Adds SECOND_BRAIN_PRO to all build platforms, triggering a recompile. DEV mock only.
        /// </summary>
        public static void AddProDefine()
        {
            ToggleDefine(ProDefine, add: true);
#if SECOND_BRAIN_DEV
            Debug.Log($"[SecondBrain DEV] '{ProDefine}' added to all platforms — scripts will recompile.");
#endif
        }

        /// <summary>
        /// Removes SECOND_BRAIN_PRO from all build platforms, triggering a recompile. DEV mock only.
        /// </summary>
        public static void RemoveProDefine()
        {
            ToggleDefine(ProDefine, add: false);
#if SECOND_BRAIN_DEV
            Debug.Log($"[SecondBrain DEV] '{ProDefine}' removed from all platforms — scripts will recompile.");
#endif
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
