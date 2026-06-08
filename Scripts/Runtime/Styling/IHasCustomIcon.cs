namespace SecretZauce.SecondBrain
{
    public interface IHasCustomIcon
    {
        /// <summary>
        /// Name of the project icon to load via IconUtils.Load(name).
        /// Corresponds to a light/dark icon pair under Resources/Editor/Icons/.
        /// </summary>
        string CustomIcon { get; }
    }
}
