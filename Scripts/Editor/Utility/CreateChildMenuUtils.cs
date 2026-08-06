using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SecretZauce.SecondBrain.Editor
{
    /// <summary>
    /// Utility for showing a type-specific child creation menu for IHasCreateChildOption.
    /// </summary>
    public static class CreateChildMenuUtils
    {
        /// <summary>
        /// Shows a GenericMenu for creating a child of a specific type, or calls CreateChild if not supported.
        /// </summary>
        /// <param name="parent">The parent object (should be IStructure)</param>
        /// <param name="window">The BrowserWindow to call CreateChild/CreateChildOfType on</param>
        /// <param name="buttonRect">The rect to show the menu at (optional, for dropdown)</param>
        public static void ShowCreateChildMenu(Object parent, BrowserWindow window, Rect? buttonRect = null)
        {
            if (parent is IHasCreateChildOption hasCreateChild)
            {
                var menu = new GenericMenu();
                var options = hasCreateChild.GetCreateChildOptions();
                if (options != null)
                {
                    foreach (var opt in options)
                    {
                        var option = opt; // local copy for closure
                        var displayName = option?.DisplayName ?? (option?.ChildType?.Name ?? "New");
                        menu.AddItem(new GUIContent(displayName), false, () => window.CreateChildUsingOption(parent, option));
                    }
                }
                // Close any open QuickPeek popup before showing the create-child menu so
                // the popup doesn't remain visible behind the menu (UX: user expects it to close).
                window.DisposeQuickPeek();

                if (buttonRect.HasValue)
                    menu.DropDown(buttonRect.Value);
                else
                    menu.ShowAsContext();
            }
            else
            {
                // Ensure quick peek is closed when invoking the create-child flow
                window.DisposeQuickPeek();
                window.CreateChild(parent);
            }
        }

        /// <summary>
        /// Adds IHasCreateChildOption provided entries into an existing GenericMenu under the given parent label.
        /// Returns true if any items were added.
        /// </summary>
        public static bool AddOptionsToMenu(GenericMenu menu, Object parent, BrowserWindow window, string parentLabel = "Create Child")
        {
            if (menu == null || parent == null || window == null)
                return false;

            if (parent is not IHasCreateChildOption hasCreate)
                return false;

            var options = hasCreate.GetCreateChildOptions();
            if (options == null || options.Count == 0)
                return false;

            foreach (var opt in options)
            {
                var option = opt; // capture
                var displayName = option?.DisplayName ?? (option?.ChildType?.Name ?? "New");
                menu.AddItem(new GUIContent(parentLabel + "/" + displayName), false, () => window.CreateChildUsingOption(parent, option));
            }

            return true;
        }
    }
}

