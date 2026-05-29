namespace SecretZauce.SecondBrain
{
    public interface IHasEditorIcon
    {
        /// <summary>
        /// The name of the editor icon to be used for this object.
        /// This should correspond to an icon available in the Unity Editor.
        /// </summary>
        string EditorIcon { get; }
    }
}