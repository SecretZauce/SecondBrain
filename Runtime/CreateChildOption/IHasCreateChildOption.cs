using System.Collections.Generic;

namespace SecretZauce.SecondBrain
{
    /// <summary>
    /// Implement this interface to provide selectable child creation options for the + button
    /// </summary>
    public interface IHasCreateChildOption
    {
        /// <summary>
        /// Returns a list of available create-child options that can be created under this object.
        /// Each option contains the target Type, a display name and an optional post-creation callback.
        /// </summary>
        IReadOnlyList<CreateChildOption> GetCreateChildOptions();
    }
}