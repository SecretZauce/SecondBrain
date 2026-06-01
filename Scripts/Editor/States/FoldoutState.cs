using System.Collections.Generic;
using System.Text;
using System;
using UnityEditor;
using UnityEngine;

namespace SecretZauce.SecondBrain.Editor
{
    public class FoldoutState 
    {
        readonly Dictionary<object, bool> foldouts = new Dictionary<object, bool>();
        /// <summary>
        /// Returns true when this FoldoutState contains an explicit entry for the given key.
        /// Useful for distinguishing "no history" from an explicit collapsed/expanded value.
        /// </summary>
        public bool HasKey(object key) => key != null && foldouts.ContainsKey(key);
        public bool Get(object key) => foldouts.GetValueOrDefault(key, false);
        public void Set(object key, bool value) 
        {
            if (key == null) return; // Prevent null keys
            foldouts[key] = value;
        }
        
        public void ExpandAll(IEnumerable<IStructure> nodes)
        {
            foreach (var node in nodes)
                ExpandAllRecursive(node);
        }

        void ExpandAllRecursive(IStructure node)
        {
            if (node == null) return;
            Set(node, true);
            if (node.ChildrenObjects == null) return;
            foreach (var child in node.ChildrenObjects)
                if (child is IStructure s) ExpandAllRecursive(s);
        }

        /// <summary>
        /// Removes all entries so every node falls back to its per-container
        /// <see cref="DefaultExpandOption"/> the next time it is rendered.
        /// </summary>
        public void Clear() => foldouts.Clear();

        public void CollapseAll(IEnumerable<IStructure> nodes)
        {
            foreach (var node in nodes)
                CollapseAllRecursive(node);
        }

        void CollapseAllRecursive(IStructure node)
        {
            if (node == null) return;
            Set(node, false);
            if (node.ChildrenObjects == null) return;
            foreach (var child in node.ChildrenObjects)
                if (child is IStructure s) CollapseAllRecursive(s);
        }
        
        /// <summary>
        /// Recursively sets foldout state for a node and all its descendants
        /// </summary>
        public void SetRecursive(object node, bool value)
        {
            if (node == null) return;
            Set(node, value);
            
            if (node is IStructure structure)
            {
                var childrenObjects = structure.ChildrenObjects;
                if (childrenObjects != null)
                {
                    foreach (var child in childrenObjects)
                    {
                        if (child is IStructure)
                        {
                            SetRecursive(child, value);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Removes entries whose keys are destroyed Unity Objects.
        /// Call after node deletions to prevent the dictionary from accumulating
        /// C# wrappers for native objects that are no longer alive.
        /// </summary>
        public void PruneDeadEntries()
        {
            List<object> dead = null;
            foreach (var key in foldouts.Keys)
            {
                if (key is UnityEngine.Object obj && obj == null)
                {
                    dead ??= new List<object>();
                    dead.Add(key);
                }
            }
            if (dead == null) return;
            foreach (var key in dead)
                foldouts.Remove(key);
        }

        public Dictionary<object, bool> Snapshot()
        {
            PruneDeadEntries();
            return new Dictionary<object, bool>(foldouts);
        }

        public void Restore(Dictionary<object, bool> snapshot)
        {
            foldouts.Clear();
            if (snapshot == null) return;
            foreach (var kvp in snapshot)
            {
                // Skip entries for destroyed Unity Objects so dead wrappers never re-enter.
                if (kvp.Key is UnityEngine.Object obj && obj == null)
                    continue;
                foldouts[kvp.Key] = kvp.Value;
            }
        }
        
        /// <summary>
        /// Save current foldout state to EditorPrefs under the given storageKey.
        /// The storage format is newline-separated records of base64(key)=0|1 where key
        /// is one of the stable key forms described above.
        /// </summary>
        public void SaveToEditorPrefs(string storageKey)
        {
            if (string.IsNullOrEmpty(storageKey)) 
                return;

            var lines = new List<string>();
            foreach (var kvp in foldouts)
            {
                string keyStr = AssetUtils.TryGetPersistentKeyForObject(kvp.Key as UnityEngine.Object);
                if (string.IsNullOrEmpty(keyStr))
                    continue; // skip keys we cannot identify

                var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(keyStr));
                var val = kvp.Value ? "1" : "0";
                lines.Add(b64 + ":" + val);
            }

            string serialized = string.Join("\n", lines);
            try
            {
                EditorPrefs.SetString(storageKey, serialized);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to save foldout state to EditorPrefs key '{storageKey}': {e.Message}");
            }
        }

        /// <summary>
        /// Load foldout state from EditorPrefs and apply to the current foldouts dictionary.
        /// Requires the caller to pass current top-level collections so we can resolve saved keys
        /// back to live UnityEngine.Object instances.
        ///
        /// Fail-safes added:
        /// - Limits on total serialized size and number of entries to avoid resource exhaustion
        /// - Robust parsing with try/catch per-entry so a single malformed entry can't break the whole load
        /// - Verification that resolved objects belong to the provided rootCollections (best-effort)
        /// </summary>
        public void LoadFromEditorPrefs(string storageKey, IEnumerable<IStructure> rootCollections)
        {
            if (string.IsNullOrEmpty(storageKey)) 
                return;
            
            if (!EditorPrefs.HasKey(storageKey))
                return;

            string serialized = EditorPrefs.GetString(storageKey, string.Empty);
            if (string.IsNullOrEmpty(serialized)) 
                return;
            
            // Fail-safe limits
            const int MAX_TOTAL_CHARS = 200_000; // total characters to process
            const int MAX_LINES = 2_000; // maximum lines/entries to process
            if (serialized.Length > MAX_TOTAL_CHARS)
            {
                Debug.LogWarning($"FoldoutState: stored data for key '{storageKey}' is too large ({serialized.Length} chars); skipping load for safety.");
                return;
            }

            // Build a flat HashSet of all nodes in the tree for O(1) membership checks.
            // This replaces the previous O(entries × tree_size) IStructure.ExistsInTree loop.
            var knownNodes = new HashSet<object>();
            if (rootCollections != null)
            {
                foreach (var root in rootCollections)
                    CollectAllNodes(root, knownNodes);
            }

            var lines = serialized.Split(new[] {'\n'}, StringSplitOptions.RemoveEmptyEntries);
            int processed = 0;
            foreach (var rawLine in lines)
            {
                if (processed >= MAX_LINES) break;
                processed++;

                // protect against very long single-line entries
                if (rawLine.Length > 10_000)
                    continue;

                try
                {
                    var parts = rawLine.Split(new[] {':'}, 2);
                    if (parts.Length != 2) continue;
                    string b64 = parts[0];
                    string valStr = parts[1];
                    bool val = valStr == "1";
                    string keyStr;
                    try
                    {
                        keyStr = Encoding.UTF8.GetString(Convert.FromBase64String(b64));
                    }
                    catch
                    {
                        continue; // skip malformed base64
                    }

                    var obj = AssetUtils.ResolveObjectFromPersistentKey(keyStr, rootCollections);
                    if (obj == null)
                        continue;

                    // O(1) membership check against the pre-built set
                    if (!knownNodes.Contains(obj))
                        continue;

                    Set(obj, val);
                }
                catch (Exception ex)
                {
                    // Swallow exceptions per-entry to avoid breaking entire load
                    Debug.LogWarning($"FoldoutState: skipping malformed foldout entry while loading key '{storageKey}': {ex.Message}");
                    continue;
                }
            }
        }

        /// <summary>
        /// Recursively adds every node reachable from <paramref name="node"/> into
        /// <paramref name="set"/>. Used by LoadFromEditorPrefs to build an O(1) membership
        /// lookup that replaces the previous per-entry IStructure.ExistsInTree traversal.
        /// </summary>
        static void CollectAllNodes(IStructure node, HashSet<object> set)
        {
            if (node == null || !set.Add(node)) return;
            if (node.ChildrenObjects == null) return;
            foreach (var child in node.ChildrenObjects)
                if (child is IStructure s) CollectAllNodes(s, set);
                else if (child != null) set.Add(child);
        }
    }
}

