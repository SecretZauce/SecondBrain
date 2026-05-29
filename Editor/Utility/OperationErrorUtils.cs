namespace SecretZauce.SecondBrain.Editor
{
    public static class OperationErrorUtils
    {
        public static string GetDisplayedAddChildErrorMessage(AddChildResult result)
        {
            switch (result)
            {
                case AddChildResult.FailedInvalidType:
                    return "Type Mismatch";
                case AddChildResult.FailedExistsInGroup:
                    return "Already in Group";
                case AddChildResult.Success:
                case AddChildResult.FailedExistsInTree:
                    return "Already in Tree";
                case AddChildResult.InternalError:
                    return "Internal Error";
                default:
                    return "Unknown error occurred while adding child.";
            }
        }
    }
}