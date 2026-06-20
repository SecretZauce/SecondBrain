using System;
using System.IO;
using Object = UnityEngine.Object;

namespace SecretZauce.SecondBrain.Editor
{
    /// <summary>
    /// Outcome of validating a name typed into the ghost (inline) creation row.
    /// </summary>
    public enum GhostNameValidationResult
    {
        /// <summary>Name is non-empty, contains no illegal characters, and is unique among siblings.</summary>
        Valid,

        /// <summary>Name is empty/whitespace — creation is cancelled silently.</summary>
        Empty,

        /// <summary>Name contains characters not allowed in asset file names.</summary>
        InvalidCharacters,

        /// <summary>A sibling under the same parent already uses this name (case-insensitive).</summary>
        DuplicateName
    }

    /// <summary>
    /// Pure validation of a ghost-row name, independent of any IMGUI/editor UI.
    /// Used by <c>TreeView.CommitGhostCreation</c>; extracted so the rules can be unit tested
    /// (manual cases 4.6 "Submitting an empty name ... does not create an asset" and
    /// "Submitting a duplicate name ... does not create an asset").
    /// </summary>
    public static class GhostNameValidation
    {
        public static GhostNameValidationResult Validate(string rawName, Object parent)
        {
            string name = rawName?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(name))
                return GhostNameValidationResult.Empty;

            char[] invalidChars = Path.GetInvalidFileNameChars();
            if (name.IndexOfAny(invalidChars) >= 0)
                return GhostNameValidationResult.InvalidCharacters;

            if (parent is IStructure parentStruct)
            {
                var siblings = parentStruct.ChildrenObjects;
                if (siblings != null)
                {
                    foreach (var sibling in siblings)
                    {
                        if (sibling != null &&
                            string.Equals(sibling.name, name, StringComparison.OrdinalIgnoreCase))
                        {
                            return GhostNameValidationResult.DuplicateName;
                        }
                    }
                }
            }

            return GhostNameValidationResult.Valid;
        }
    }
}
