using UnityEngine;

namespace SecretZauce.SecondBrain
{
    /// <summary>
    /// PRO version only:
    /// Abstract base class for executable action items that can be added as children
    /// inside Containers
    ///
    /// Subclass this to define any custom executable logic. Override <see cref="ActionPath"/>
    /// to group your actions into nested sub-menus inside the Action Item Selector popup
    /// (e.g. "Editor/Play Mode" will nest the item under an "Editor > Play Mode" sub-menu).
    /// </summary>
    public abstract class ActionItem : ScriptableObject, IHasCustomIcon
    {
        public virtual string DefaultName => GetType().Name;

        /// <summary>
        /// Project icon name loaded via IconUtils (Resources/Icons/). Override to
        /// provide a custom icon for this ActionItem subclass.
        /// </summary>
        public virtual string CustomIcon => "action";

        /// <summary>
        /// Optional grouping path shown in the ActionItemSelector popup.
        /// Use "/" as separator for nested groups, e.g. "Editor/Play Mode".
        /// Leave empty (the default) to place the item at the top level.
        /// </summary>
        public virtual string ActionPath => string.Empty;

        /// <summary>
        /// Optional single-line detail shown next to the item label 
        /// </summary>
        public virtual string GetDetailDisplay() => string.Empty;
        
        public abstract void Execute();
    }
}