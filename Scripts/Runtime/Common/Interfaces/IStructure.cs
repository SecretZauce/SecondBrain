using System;
using System.Collections.Generic;
using Object = UnityEngine.Object;

namespace SecretZauce.SecondBrain
{
    public interface IStructure<T> : IStructure where T : Object
    {
        // Guard against a null Children list (can occur with corrupted/partially-initialised Unity assets)
        List<Object> IStructure.ChildrenObjects => Children?.ConvertAll(child => (Object)child) ?? new List<Object>();
        public List<T> Children { get; }
        
        AddChildResult IStructure.AddChild(Object child, int index)
        {
            var castedChild = child as T;
            if (castedChild == null)
                return AddChildResult.FailedInvalidType;

            if (child is IStructure && child is not IAllowMultipleParents && ExistsInTree(GetRoot(), castedChild))
                return AddChildResult.FailedExistsInTree;

            if (ExistsInChildren(this, castedChild))
                return AddChildResult.FailedExistsInGroup;
            
            if (index < 0 || index >= Children.Count)
            {
                Children.Add(castedChild);
            }
            else
            {
                Children.Insert(index, castedChild);
            }
            return AddChildResult.Success;
        }
        
        bool IStructure.RemoveChild(Object child)
        {
            var castedChild = child as T;
            if (castedChild == null)
                return false;
            
            return Children.Remove(castedChild);
        }

        bool IStructure.CanAcceptChild(Object child)
        {
            return child is T;
        }
        
        Object IStructure.CreateChildObject(Type t)
        {
            return CreateNew(typeof(T));
        }

        T CreateNew(Type type);
    }
    public interface IStructure
    {
        List<Object> ChildrenObjects { get; }
        Object CreateChildObject(Type type = null);
        AddChildResult AddChild(Object child, int index = -1);
        bool RemoveChild(Object child);
        bool CanAcceptChild(Object child);

        IStructure GetRoot() => Profile.Active;

        /// <summary>
        /// Checks if the given object exists anywhere in the tree rooted at node.
        /// </summary>
        public static bool ExistsInTree(IStructure node, Object target)
        {
            if (node == null || target == null)
                return false;
            var children = node.ChildrenObjects;
            if (children == null)
                return false;
            foreach (var c in children)
            {
                if (ReferenceEquals(c, target))
                    return true;
                if (c is IStructure childStruct)
                {
                    if (ExistsInTree(childStruct, target))
                        return true;
                }
            }
            return false;
        }

        public static bool ExistsInChildren(IStructure node, Object target)
        {
            if (node == null || target == null)
                return false;

            foreach (var c in node.ChildrenObjects)
            {
                if (ReferenceEquals(c, target))
                    return true;
            }

            return false;
        }
    }

    public enum AddChildResult
    {
        Success, FailedExistsInGroup, FailedExistsInTree, FailedInvalidType, InternalError
    }

    /// <summary>
    /// Child view mode for editors that display children (tabs vs foldouts).
    /// Stored on structures that wish to remember the user's preferred view.
    /// </summary>
    public enum ChildViewMode
    {
        Foldouts = 0,
        Tabs = 1
    }

    /// <summary>
    /// Implement this on structures that want to persist a preferred child view mode.
    /// </summary>
    public interface IHasChildViewPreference
    {
        ChildViewMode PreferredChildView { get; set; }
    }

    /// <summary>
    /// Opt out of the single-parent constraint: structures implementing this interface
    /// may be added to the tree even if they already exist elsewhere in the hierarchy.
    /// </summary>
    public interface IAllowMultipleParents { }
}