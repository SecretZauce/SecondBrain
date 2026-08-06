#if SECOND_BRAIN_DEV
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace SecretZauce.SecondBrain.Editor
{
    /// <summary>
    /// DEV tool — sets Free version, Pro version and their package.json files in one operation.
    /// Opens via Window ▸ Second Brain ▸ DEV — Set Version.
    /// </summary>
    internal class VersionSetterWindow : EditorWindow
    {
        // ── File paths resolved on open ───────────────────────────────────────────
        string freeCsPath;
        string proCsPath;
        string freeJsonPath;
        string proJsonPath;

        // ── Versions read from disk ───────────────────────────────────────────────
        string currentFree;
        string currentPro;

        // ── User inputs ───────────────────────────────────────────────────────────
        string inputFree;
        string inputPro;

        bool freeValid = true;
        bool proValid  = true;

        // ── Regex patterns (capture prefix / value / suffix so replacement is safe)
        static readonly Regex CsRegex   = new Regex(@"(public const string Current\s*=\s*"")([^""]+)(""\s*;)");
        static readonly Regex JsonRegex = new Regex(@"(""version""\s*:\s*"")([^""]+)("")");

        // ─────────────────────────────────────────────────────────────────────────

        [MenuItem("Tools/Second Brain/DEV — Set Version")]
        static void Open()
        {
            var win = GetWindow<VersionSetterWindow>(utility: true, title: "DEV — Set Version", focus: true);
            win.minSize = new Vector2(500, 310);
            win.Show();
        }

        void OnEnable() => ResolveFiles();

        // ── File resolution ───────────────────────────────────────────────────────

        void ResolveFiles()
        {
            freeCsPath   = FindAssetByFilename("SecondBrainVersion.cs");
            // SecondBrainProVersion aliases this const, so the literal lives here.
            proCsPath    = FindAssetByFilename("ProBootstrapVersion.cs");
            freeJsonPath = FindPackageJsonNear(freeCsPath);
            proJsonPath  = FindPackageJsonNear(proCsPath);

            currentFree = ReadCsVersion(freeCsPath) ?? "—";
            currentPro  = ReadCsVersion(proCsPath)  ?? "—";

            inputFree = currentFree != "—" ? currentFree : "1.0.0";
            inputPro  = currentPro  != "—" ? currentPro  : "1.0.0";

            freeValid = true;
            proValid  = true;
        }

        // ── GUI ───────────────────────────────────────────────────────────────────

        void OnGUI()
        {
            GUILayout.Space(8);
            EditorGUILayout.LabelField("SecondBrain — Version Setter", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Changes take effect after the editor recompiles.", EditorStyles.miniLabel);
            GUILayout.Space(8);

            DrawRow("Free", freeCsPath, freeJsonPath, currentFree, ref inputFree, ref freeValid);
            GUILayout.Space(6);
            DrawRow("Pro",  proCsPath,  proJsonPath,  currentPro,  ref inputPro,  ref proValid);

            GUILayout.Space(14);

            bool freeChanged = freeCsPath != null && freeValid && inputFree != currentFree;
            bool proChanged  = proCsPath  != null && proValid  && inputPro  != currentPro;

            GUI.enabled = freeValid && proValid && (freeChanged || proChanged);
            if (GUILayout.Button("Apply Changes", GUILayout.Height(30)))
                ConfirmAndApply(freeChanged, proChanged);
            GUI.enabled = true;
        }

        void DrawRow(string label, string csPath, string jsonPath, string current,
                     ref string input, ref bool valid)
        {
            var boxStyle = EditorStyles.helpBox;
            GUILayout.BeginVertical(boxStyle);

            EditorGUILayout.LabelField(label + " Version", EditorStyles.boldLabel);

            // Current / new on one line
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Current:", GUILayout.Width(56));
            EditorGUILayout.LabelField(current, EditorStyles.boldLabel, GUILayout.Width(76));
            GUILayout.Space(6);
            EditorGUILayout.LabelField("New:", GUILayout.Width(34));

            GUI.enabled = csPath != null;
            var prev = input;
            input = EditorGUILayout.TextField(input, GUILayout.Width(104));
            if (input != prev)
                valid = SemanticVersion.TryParse(input, out _);
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            if (!valid)
                EditorGUILayout.HelpBox("Not a valid semantic version (e.g. 1.2.3)", MessageType.Error);

            // Files that will be touched
            DrawFileRow("C# source:",    csPath);
            DrawFileRow("package.json:", jsonPath);

            GUILayout.EndVertical();
        }

        static void DrawFileRow(string prefix, string path)
        {
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(prefix, EditorStyles.miniLabel, GUILayout.Width(78));
            EditorGUILayout.LabelField(path ?? "— not found —", EditorStyles.miniLabel);
            GUILayout.EndHorizontal();
        }

        // ── Apply ─────────────────────────────────────────────────────────────────

        void ConfirmAndApply(bool freeChanged, bool proChanged)
        {
            var sb = new StringBuilder("Apply the following version changes?\n");

            if (freeChanged)
            {
                sb.Append($"\nFree  {currentFree}  →  {inputFree}");
                if (freeCsPath   != null) sb.Append($"\n  • {freeCsPath}");
                if (freeJsonPath != null) sb.Append($"\n  • {freeJsonPath}");
            }
            if (proChanged)
            {
                sb.Append($"\nPro   {currentPro}  →  {inputPro}");
                if (proCsPath   != null) sb.Append($"\n  • {proCsPath}");
                if (proJsonPath != null) sb.Append($"\n  • {proJsonPath}");
            }

            if (!EditorUtility.DisplayDialog("Confirm Version Change", sb.ToString().Trim(), "Apply", "Cancel"))
                return;

            if (freeChanged)
            {
                WriteCsVersion(freeCsPath,    inputFree);
                WriteJsonVersion(freeJsonPath, inputFree);
            }
            if (proChanged)
            {
                WriteCsVersion(proCsPath,    inputPro);
                WriteJsonVersion(proJsonPath, inputPro);
            }

            AssetDatabase.Refresh();
            Debug.Log("[SecondBrain DEV] Version(s) updated — editor will recompile.");
            ResolveFiles();
        }

        // ── File helpers ──────────────────────────────────────────────────────────

        static string FindAssetByFilename(string filename)
        {
            string stem = Path.GetFileNameWithoutExtension(filename);
            foreach (var guid in AssetDatabase.FindAssets(stem, new[] { "Assets" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileName(path) == filename)
                    return path;
            }
            return null;
        }

        // Walks up the directory tree from the given asset path until it finds a package.json.
        static string FindPackageJsonNear(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return null;
            var dir = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            while (!string.IsNullOrEmpty(dir) && dir.StartsWith("Assets"))
            {
                string candidate = dir + "/package.json";
                if (File.Exists(candidate)) return candidate;
                var parent = Path.GetDirectoryName(dir)?.Replace('\\', '/');
                if (parent == dir) break;
                dir = parent;
            }
            return null;
        }

        static string ReadCsVersion(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            var m = CsRegex.Match(File.ReadAllText(path));
            return m.Success ? m.Groups[2].Value : null;
        }

        static void WriteCsVersion(string path, string version)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            var updated = CsRegex.Replace(File.ReadAllText(path), "${1}" + version + "${3}");
            File.WriteAllText(path, updated, Encoding.UTF8);
        }

        static void WriteJsonVersion(string path, string version)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            var updated = JsonRegex.Replace(File.ReadAllText(path), "${1}" + version + "${3}");
            File.WriteAllText(path, updated, Encoding.UTF8);
        }
    }
}
#endif
