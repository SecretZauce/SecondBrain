using System;
using Object = UnityEngine.Object;

namespace SecretZauce.SecondBrain.Editor
{
    /// <summary>
    /// Holds the state for an in-progress ghost item creation session.
    /// A ghost item is a temporary inline row with an input field that appears
    /// in the TreeView when the user triggers a "Create Child" action while
    /// the ForceNamingOnCreate browser setting is enabled.
    /// No asset is created until the user commits a non-empty, non-duplicate name.
    /// </summary>
    internal class GhostCreationSession
    {
        /// <summary>The parent IStructure object under which the new child will be created.</summary>
        public readonly Object Parent;

        /// <summary>
        /// The specific child type to create, or null to use the parent's default
        /// <see cref="IStructure.CreateChildObject()"/> method.
        /// </summary>
        public readonly Type ChildType;

        /// <summary>
        /// When the ghost was started from a specific CreateChildOption, this holds
        /// that option so the final creation can apply the option's PostCreate callback.
        /// </summary>
        public readonly CreateChildOption Option;

        /// <summary>Current text in the ghost input field.</summary>
        public string InputText;

        /// <summary>True on the first draw so the field receives keyboard focus.</summary>
        public bool FocusPending;

        /// <summary>Incremented each session so IMGUI discards stale editor state.</summary>
        public readonly int SessionId;

        static int nextSessionId;

        public GhostCreationSession(Object parent, Type childType, string defaultName = "", CreateChildOption option = null)
        {
            Parent = parent;
            ChildType = childType;
            Option = option;
            InputText = defaultName ?? string.Empty;
            FocusPending = true;
            SessionId = ++nextSessionId;
        }

        public string ControlName => $"ghost_create_{SessionId}";
    }
}
