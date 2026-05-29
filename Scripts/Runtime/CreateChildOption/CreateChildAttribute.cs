using System;

namespace SecretZauce.SecondBrain
{
    /// <summary>
    /// Marks a ScriptableObject type as creatable via the Browser "Create Child" menu.
    /// Use MenuPath to nest entries (e.g. "Configs/Enemies") and DisplayName to override
    /// the visible item name. Placed in runtime so non-editor code can annotate types.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class CreateChildAttribute : Attribute
    {
        public string MenuPath { get; }
        public string DisplayName { get; }

        public CreateChildAttribute(string menuPath = null, string displayName = null)
        {
            MenuPath = menuPath;
            DisplayName = displayName;
        }
    }
}

