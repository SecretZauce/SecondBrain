using System;
using System.Collections.Generic;
using System.Linq;
using Object = UnityEngine.Object;

namespace SecretZauce.SecondBrain.Editor
{
    /// <summary>
    /// Shared utilities for structure/tree traversal.
    /// </summary>
    public static class StructureUtils
    {
        /// <summary>
        /// Returns the UnityEngine.Object node at the specified path within the provided collections.
        /// If collections is null and root is provided, builds collections from root.ChildrenObjects filtered to IStructure.
        /// Returns null for invalid inputs or out-of-range indices.
        /// </summary>
        public static Object GetNodeAtPath(int[] path, List<IStructure> collections)
        {
            if (collections == null || path == null || path.Length == 0)
                return null;

            if (path[0] < 0 || path[0] >= collections.Count)
                return null;

            Object node = collections[path[0]] as Object;
            IStructure structure = collections[path[0]];

            for (int i = 1; i < path.Length && structure != null; i++)
            {
                var childrenObjects = structure.ChildrenObjects;
                if (childrenObjects == null || path[i] < 0 || path[i] >= childrenObjects.Count)
                    return null;

                node = childrenObjects[path[i]];
                structure = node as IStructure;
            }

            return node;
        }

        /// <summary>
        /// Compares two integer index-paths for equality.
        /// Returns true when both are null or contain the same sequence of integers.
        /// </summary>
        public static bool ArePathsEqual(int[] a, int[] b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        /// <summary>
        /// Recursively searches for a target child in the structure.
        /// </summary>
        static int[] FindPathInStructure(IStructure structure, Object target, List<int> currentPath)
        {
            if (structure == null || structure.ChildrenObjects == null)
                return null;

            var childrenObjects = structure.ChildrenObjects;
            for (int i = 0; i < childrenObjects.Count; i++)
            {
                var child = childrenObjects[i];
                if (child == target)
                {
                    var result = new List<int>(currentPath) {i};
                    return result.ToArray();
                }

                // If child is also IStructure, search deeper
                if (child is IStructure childStructure)
                {
                    var newPath = new List<int>(currentPath) {i};
                    var result = FindPathInStructure(childStructure, target, newPath);
                    if (result != null)
                        return result;
                }
            }

            return null;
        }

        /// <summary>
        /// Finds the path to a specific child in the hierarchy.
        /// </summary>
        public static int[] FindPathToChild(List<IStructure> collections, Object target)
        {
            if (collections == null || target == null)
                return null;

            // Search through the hierarchy
            for (int i = 0; i < collections.Count; i++)
            {
                var collection = collections[i];
                if (collection as Object == target)
                {
                    return new[] {i};
                }

                // Search recursively in children
                var path = StructureUtils.FindPathInStructure(collection, target, new List<int> {i});
                if (path != null)
                    return path;
            }

            return null;
        }

        /// <summary>
        /// Finds the parent path to select after removing items.
        /// If all items share the same parent, return that parent's path.
        /// Otherwise return the common ancestor.
        /// </summary>
        public static int[] FindParentForSelection(List<int[]> paths)
        {
            if (paths == null || paths.Count == 0)
                return Array.Empty<int>();
            
            // Single item: return its parent
            if (paths.Count == 1)
            {
                var path = paths[0];
                return path.Length > 1 ? path.Take(path.Length - 1).ToArray() : Array.Empty<int>();
            }
            
            // Multiple items: check if they all share the same parent
            int[] firstParent = paths[0].Length > 1 
                ? paths[0].Take(paths[0].Length - 1).ToArray() 
                : Array.Empty<int>();
            
            bool sameParent = true;
            foreach (var path in paths)
            {
                int[] pathParent = path.Length > 1 
                    ? path.Take(path.Length - 1).ToArray() 
                    : Array.Empty<int>();
                
                if (!pathParent.SequenceEqual(firstParent))
                {
                    sameParent = false;
                    break;
                }
            }
            
            // If same parent, return it
            if (sameParent)
                return firstParent;
            
            // Otherwise, find common ancestor (go one level up from common path)
            return FindCommonParent(paths);
        }

        /// <summary>
        /// Finds the common parent path for a list of paths
        /// </summary>
        public static int[] FindCommonParent(List<int[]> paths)
        {
            if (paths == null || paths.Count == 0)
                return Array.Empty<int>();
            
            if (paths.Count == 1)
            {
                var path = paths[0];
                return path.Length > 1 ? path.Take(path.Length - 1).ToArray() : new int[0];
            }
            
            // Find shortest path length
            int minLength = paths.Min(p => p.Length);
            
            // Find common prefix
            int commonDepth = 0;
            for (int i = 0; i < minLength; i++)
            {
                int firstValue = paths[0][i];
                if (paths.All(p => p[i] == firstValue))
                {
                    commonDepth = i + 1;
                }
                else
                {
                    break;
                }
            }
            
            // Return parent of common path
            if (commonDepth > 1)
                return paths[0].Take(commonDepth - 1).ToArray();
            
            return new int[0];
        }

        /// <summary>
        /// Checks if two paths represent sibling nodes (same parent, different last index)
        /// </summary>
        public static bool AreSiblings(int[] path1, int[] path2)
        {
            if (path1 == null || path2 == null)
                return false;

            if (path1.Length != path2.Length)
                return false;

            // Check if all indices except the last are the same (same parent)
            for (int i = 0; i < path1.Length - 1; i++)
            {
                if (path1[i] != path2[i])
                    return false;
            }

            // And last index is different (different children)
            return path1[^1] != path2[^1];
        }
    }
}
