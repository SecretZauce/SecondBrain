using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SecretZauce.SecondBrain.Editor
{
    /// <summary>
    /// Helper class for creating child Objects for IStructure implementations.
    /// Delegates child creation to the parent's CreateChild() method.
    /// </summary>
    public static class ChildCreationUtils
    {
        /// <summary>
        /// Creates a new child for the given parent IStructure object.
        /// </summary>
        /// <param name="parent">The parent Object that implements IStructure</param>
        /// <param name="onChildCreated">Optional callback invoked with the created child after successful creation</param>
        /// <returns>True if child was created successfully, false otherwise</returns>
        public static bool CreateNewChild(Object parent, Action<Object> onChildCreated = null)
        {
            if (parent == null)
            {
                Debug.LogError("Parent cannot be null");
                return false;
            }

            if (parent is not IStructure structure)
            {
                Debug.LogError($"Parent {parent.GetType().Name} does not implement IStructure");
                return false;
            }

            // Delegate child creation to the parent
            Object newChild = structure.CreateChildObject();
            if (newChild == null)
            {
                Debug.LogError($"Failed to create child for {parent.GetType().Name}");
                return false;
            }

            onChildCreated?.Invoke(newChild);
            return true;
        }

        /// <summary>
        /// Creates a new child of the specified type for the given parent IStructure object.
        /// </summary>
        /// <param name="parent">The parent Object that implements IStructure</param>
        /// <param name="childType">The type of child to create</param>
        /// <param name="onChildCreated">Optional callback invoked with the created child after successful creation</param>
        /// <returns>True if child was created successfully, false otherwise</returns>
        public static bool CreateNewChildOfType(Object parent, Type childType, Action<Object> onChildCreated = null)
        {
            if (parent == null)
            {
                Debug.LogError("Parent cannot be null");
                return false;
            }

            if (parent is not IStructure structure)
            {
                Debug.LogError($"Parent {parent.GetType().Name} does not implement IStructure");
                return false;
            }

            // Delegate child creation to the parent, specifying the type.
            // Strategy:
            //   1. When an explicit childType is a concrete ScriptableObject subtype, create it
            //      directly via CreateInstance — this avoids calling the parent's CreateNew which
            //      typically ignores the type argument and always returns its own default child type.
            //   2. Fall back to the parent's CreateNew / CreateChildObject only when childType is
            //      null or when direct instantiation is not possible.
            Object newChild;
            if (childType is {IsAbstract: false} && typeof(ScriptableObject).IsAssignableFrom(childType))
            {
                newChild = ScriptableObject.CreateInstance(childType);
            }
            else
            {
                var createNewMethod = parent.GetType().GetMethod("CreateNew", new[] { typeof(Type) });
                if (createNewMethod != null)
                    newChild = createNewMethod.Invoke(parent, new object[] { childType }) as Object;
                else
                    newChild = structure.CreateChildObject(childType);
            }

            if (newChild == null)
            {
                Debug.LogError($"Failed to create child of type {childType?.Name} for {parent.GetType().Name}");
                return false;
            }

            onChildCreated?.Invoke(newChild);
            return true; 
        }
    }
}