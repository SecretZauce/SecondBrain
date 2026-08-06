namespace SecretZauce.SecondBrain.Editor
{
    public abstract class SceneLinkGUIBase
    {
        protected SceneLinkGUIBase(UnityEditor.Editor editorWindow)
        {
        }

        public abstract void Draw();
    }
}