using UnityEngine;

namespace SecretZauce.SecondBrain
{
    [CreateAssetMenu(menuName = "ScriptableStructure/SceneComponentRef", fileName = "SceneComponentRef", order = 2)]
    public class SceneComponentRef : ScriptableObject
    {
        public SceneComponent sceneComponent = new SceneComponent();
    }
}
