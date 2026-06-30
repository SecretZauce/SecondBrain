using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace SecretZauce.SecondBrain.Editor
{
    /// <summary>
    /// Discovers types annotated with <see cref="CreateChildAttribute"/> and
    /// adds them to a GenericMenu. Filters by parent compatibility using IStructure.CanAcceptChild.
    /// </summary>
    public static class CreateChildAttributeSelector
    {
        /// <summary>
        /// Adds attributed ScriptableObject types to an existing GenericMenu under a parent label.
        /// Returns true if any items were added.
        /// </summary>
        public static bool AddToMenu(GenericMenu menu, Action<Type> onTypeSelected, string parentLabel = "Create Child", IStructure parentFilter = null)
        {
            if (menu == null || onTypeSelected == null)
                return false;

            var types = TypeCache.GetTypesDerivedFrom<ScriptableObject>();
            bool hasAny = false;

            foreach (var type in types)
            {
                if (type.IsAbstract)
                    continue;

                try
                {
                    var attr = type.GetCustomAttribute<CreateChildAttribute>(true);
                    if (attr == null)
                        continue;

                    // Check compatibility with parent (if provided) by instantiating a temp instance
                    bool compatible = true;
                    if (parentFilter != null)
                    {
                        var tempInstance = ScriptableObject.CreateInstance(type);
                        if (tempInstance == null)
                        {
                            compatible = false;
                        }
                        else
                        {
                            try
                            {
                                compatible = parentFilter.CanAcceptChild(tempInstance);
                            }
                            finally
                            {
                                UnityEngine.Object.DestroyImmediate(tempInstance);
                            }
                        }
                    }

                    if (!compatible)
                        continue;

                    hasAny = true;
                    var capturedType = type;
                    var name = attr.DisplayName ?? type.Name;
                    string menuPath = string.IsNullOrEmpty(attr.MenuPath) ? name : (attr.MenuPath + "/" + name);
                    menu.AddItem(new GUIContent(parentLabel + "/" + menuPath), false, () => onTypeSelected(capturedType));
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[CreateChildAttributeSelector] Failed to process type '{type.Name}': {ex.Message}");
                }
            }

            return hasAny;
        }
    }
}

