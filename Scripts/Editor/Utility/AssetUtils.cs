using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using Object = UnityEngine.Object;

namespace SecretZauce.SecondBrain.Editor
{
    public static class AssetUtils
    {
        /// <summary>
        /// Generates a unique name for a duplicate in Unity style: "Name", "Name (1)", "Name (2)", etc.
        /// Intelligently parses existing numbers to continue the sequence.
        /// </summary>
        public static string GenerateUniqueName(string originalName, IStructure parent)
        {
            if (parent?.ChildrenObjects == null)
                return originalName;
            
            // Extract base name and existing number if present
            string baseName = originalName;
            int startCounter = 1;
            
            // Check if name ends with " (N)" pattern
            if (originalName.EndsWith(")") && originalName.Contains(" ("))
            {
                int lastOpenParen = originalName.LastIndexOf(" (", StringComparison.Ordinal);
                if (lastOpenParen > 0)
                {
                    string numberPart = originalName.Substring(lastOpenParen + 2, originalName.Length - lastOpenParen - 3);
                    if (int.TryParse(numberPart, out int existingNumber))
                    {
                        // Name ends with " (N)" - extract base name and start from N+1
                        baseName = originalName.Substring(0, lastOpenParen);
                        startCounter = existingNumber + 1;
                    }
                }
            }
            
            // Get all existing names in the parent
            var existingNames = new HashSet<string>();
            var parentChildren = parent.ChildrenObjects;
            foreach (var child in parentChildren)
            {
                if (child != null)
                    existingNames.Add(child.name);
            }
            
            // Try base name first
            if (!existingNames.Contains(baseName))
                return baseName;
            
            // Find first available numbered name
            int counter = startCounter;
            string candidateName;
            do
            {
                candidateName = $"{baseName} ({counter})";
                counter++;
            }
            while (existingNames.Contains(candidateName) && counter < 1000); // Safety limit
            return candidateName;
        }

        /// <summary>
        /// Returns the asset folder path for the given Object, or "Assets" as fallback.
        /// </summary>
        public static string GetAssetFolder(Object obj)
        {
            if (obj == null)
                return "Assets";
            var assetPath = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(assetPath))
                return "Assets";
            var folder = Path.GetDirectoryName(assetPath);
            return !string.IsNullOrEmpty(folder) ? folder.Replace("\\", "/") : "Assets";
        }

        /// <summary>
        /// Returns true when the provided object (any asset or sub-asset) lives in a different
        /// .asset file than the provided target parent object. This is a broader check used
        /// to detect structure assets (e.g. Profile/Container instances) that are stored
        /// in other asset files and should not be added as children of the target parent.
        /// </summary>
        public static bool IsInDifferentAsset(Object obj, Object targetParent)
        {
            if (obj == null || targetParent == null)
                return false;

            try
            {
                // Exception: if the object is NOT an embedded sub-asset, treat it as not "in a different asset".
                // The caller is primarily interested in blocking embedded sub-assets that live inside
                // other .asset files. Standalone/top-level assets (and scene objects) should not be
                // considered as "different asset" for this check.
                if (!AssetDatabase.IsSubAsset(obj))
                    return false;

                string objPath = AssetDatabase.GetAssetPath(obj);
                string parentPath = AssetDatabase.GetAssetPath(targetParent);
                if (string.IsNullOrEmpty(objPath) || string.IsNullOrEmpty(parentPath))
                    return false;

                return !string.Equals(objPath, parentPath, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public static void CreateDuplicatedAsset(Object item, string uniqueName, Object duplicate)
        {
            var assetPath = AssetDatabase.GetAssetPath(item);
            if (string.IsNullOrEmpty(assetPath)) 
                return;
            
            var folder = Path.GetDirectoryName(assetPath);
            folder = !string.IsNullOrEmpty(folder) ? folder.Replace("\\", "/") : "Assets";

            // Manually generate unique file path to preserve our naming
            string fileName = uniqueName + ".asset";
            string newAssetPath = Path.Combine(folder, fileName);
                
            // If file exists, append numbers to filename (not the object name)
            int fileCounter = 1;
            while (File.Exists(newAssetPath) && fileCounter < 1000)
            {
                fileName = $"{uniqueName} {fileCounter}.asset";
                newAssetPath = Path.Combine(folder, fileName);
                fileCounter++;
            }

            AssetDatabase.CreateAsset(duplicate, newAssetPath);
            AssetDatabase.ImportAsset(newAssetPath);
                
            // Register undo for the created asset
            Undo.RegisterCreatedObjectUndo(duplicate, "Duplicate Item");
        }

        public static string TryGetPersistentKeyForObject(Object obj)
        {
            if (obj == null) return null;

            // Try GUID + localId when available (works for assets and sub-assets)
            try
            {
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out string guid, out long localId))
                {
                    if (!string.IsNullOrEmpty(guid))
                        return $"g:{guid}:{localId}";
                }
            }
            catch
            {
                // API may not be available in some Unity versions; ignore and try other fallbacks
            }

            // Fallback to asset path
            try
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (!string.IsNullOrEmpty(path))
                {
                    return $"p:{path}";
                }
            }
            catch
            {
                // ignore
            }

            // Last resort: instance id (not stable across sessions)
            try
            {
                return $"i:{obj.GetStableInstanceId()}";
            }
            catch
            {
                return null;
            }
        }

        public static Object ResolveObjectFromPersistentKey(string keyStr, IEnumerable<IStructure> rootCollections)
        {
            if (string.IsNullOrEmpty(keyStr)) return null;

            if (keyStr.StartsWith("g:"))
            {
                // g:{guid}:{localId}
                var parts = keyStr.Split(new[] {':'}, 3);
                if (parts.Length >= 2)
                {
                    var guid = parts[1];
                    long localId = 0;
                    if (parts.Length == 3)
                        long.TryParse(parts[2], out localId);

                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!string.IsNullOrEmpty(path))
                    {
                        // Avoid calling LoadAllAssetsAtPath for scene assets (ReadObjectThreaded warning).
                        // Detect scene assets via main asset type or file extension and only use
                        // LoadAssetAtPath in that case.
                        try
                        {
                            var mainType = AssetDatabase.GetMainAssetTypeAtPath(path);
                            bool isSceneAsset = mainType == typeof(SceneAsset) || path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase);

                            if (isSceneAsset)
                            {
                                // For scene files, don't load sub-assets; return the main scene asset only.
                                var mainScene = AssetDatabase.LoadAssetAtPath<Object>(path);
                                if (mainScene != null)
                                    return mainScene;
                            }
                            else
                            {
                                // Load all assets at path and find the one matching guid+localId
                                var assets = AssetDatabase.LoadAllAssetsAtPath(path) ?? Array.Empty<Object>();
                                foreach (var a in assets)
                                {
                                    try
                                    {
                                        if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(a, out string g2, out long id2))
                                        {
                                            if (string.Equals(g2, guid, StringComparison.OrdinalIgnoreCase) && id2 == localId)
                                                return a;
                                        }
                                    }
                                    catch
                                    {
                                        // ignore
                                    }
                                }

                                // If not found among sub-assets, try main asset
                                var main = AssetDatabase.LoadAssetAtPath<Object>(path);
                                if (main != null)
                                    return main;
                            }
                        }
                        catch
                        {
                            // Fall back to safe behavior: try main asset only
                            var main = AssetDatabase.LoadAssetAtPath<Object>(path);
                            if (main != null)
                                return main;
                        }
                    }
                }
            }
            else if (keyStr.StartsWith("p:"))
            {
                var path = keyStr.Substring(2);
                if (!string.IsNullOrEmpty(path))
                {
                        // Avoid LoadAllAssetsAtPath on scene files to prevent threaded read warnings.
                        try
                        {
                            var mainType = AssetDatabase.GetMainAssetTypeAtPath(path);
                            bool isSceneAsset = mainType == typeof(UnityEditor.SceneAsset) || path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase);
                            if (isSceneAsset)
                            {
                                var main = AssetDatabase.LoadAssetAtPath<Object>(path);
                                return main;
                            }
                        }
                        catch
                        {
                            // ignore and fall back to LoadAllAssetsAtPath
                        }

                        var assets = AssetDatabase.LoadAllAssetsAtPath(path) ?? Array.Empty<Object>();
                        if (assets.Length > 0) return assets[0];
                        var mainFallback = AssetDatabase.LoadAssetAtPath<Object>(path);
                        return mainFallback;
                }
            }
            else if (keyStr.StartsWith("i:"))
            {
                if (int.TryParse(keyStr.Substring(2), out int instanceId))
                {
                    try
                    {
                        var obj = InstanceIdCompat.TryResolveStableInstanceId(instanceId);
                        return obj as Object;
                    }
                    catch
                    {
                        // ignore
                    }
                }
            }

            // As a last resort, try to match by name within provided root collections (best-effort)
            if (rootCollections != null)
            {
                foreach (var col in rootCollections)
                {
                    var found = TryFindByNameRecursive(col, keyStr);
                    if (found != null) return found;
                }
            }

            return null;
        }

        static Object TryFindByNameRecursive(IStructure node, string nameKey)
        {
            if (node == null) return null;
            if (string.Equals(node.ToString(), nameKey, StringComparison.Ordinal))
                return node as Object;

            foreach (var child in node.ChildrenObjects ?? new List<Object>())
            {
                if (child == null) continue;
                if (string.Equals(child.name, nameKey, StringComparison.Ordinal))
                    return child;
                if (child is IStructure childStruct)
                {
                    var r = TryFindByNameRecursive(childStruct, nameKey);
                    if (r != null) return r;
                }
            }

            return null;
        }
    }
}