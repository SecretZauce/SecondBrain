using System;
using Object = UnityEngine.Object;

namespace SecretZauce.SecondBrain
{
    /// <summary>
    /// Represents a single option shown in a create-child menu. Encapsulates the
    /// child Type, a display name, and an optional post-creation callback that
    /// will be invoked after the child has been created and persisted.
    /// </summary>
    public sealed class CreateChildOption
    {
        public Type ChildType { get; }
        public string DisplayName { get; }
        public Action<Object, Type> PostCreate { get; }

        public CreateChildOption(Type childType, string displayName = null, Action<Object, Type> postCreate = null)
        {
            ChildType = childType ?? throw new ArgumentNullException(nameof(childType));
            DisplayName = displayName ?? childType.Name;
            PostCreate = postCreate;
        }
    }
}