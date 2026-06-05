using UnityEditor;

namespace SecretZauce.SecondBrain.Editor
{
    public class SecondBrainWindow : BrowserWindow
    {
        protected override IStructure HomeRoot => Motherbase.Home;
        
        [MenuItem("Window/Second Brain (Home)")]
        static void OpenWindow()
        {
            OpenWindow<SecondBrainWindow>();
        }
    }
}