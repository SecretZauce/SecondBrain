using UnityEditor;

namespace SecretZauce.SecondBrain.Editor
{
    public class SecondBrainWindow : BrowserWindow
    {
        protected override IStructure HomeRoot => Motherbase.Home;
        
        [MenuItem("Window/Second Brain/New Second Brain Window (Home)")]
        static void OpenWindow()
        {
            OpenWindow<SecondBrainWindow>();
        }
    }
}