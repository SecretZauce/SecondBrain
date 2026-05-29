#if SECOND_BRAIN_PRO
namespace SecretZauce.SecondBrain.Editor
{
    public static class ProFeature
    {
        public static IProFeatureProvider Provider { get; private set; }
        public static void RegisterResolver(IProFeatureProvider provider)
        {
            Provider = provider;
        }
    }

    public interface IProFeatureProvider
    {
        public SceneLinkGUIBase CreateSceneLinkGUI(UnityEditor.Editor editor);
        public ActionItemGUIBase CreateActionItemGUI(object node, UnityEngine.GUIStyle style, UnityEngine.Rect arrowRect, UnityEngine.Rect rowRect);
        public ActionItemHandlerBase CreateActionItemHandler();
        public QuickPeekHandlerBase CreateQuickPeekHandler(BrowserWindow window);
    }
}
#endif