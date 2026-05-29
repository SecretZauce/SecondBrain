using UnityEngine;

namespace SecretZauce.SecondBrain
{
    [CreateAssetMenu(menuName = "ScriptableStructure/SceneObjectRef", fileName = "SceneObjectRef", order = 1)]
    public class SceneObjectRef : ScriptableObject
    {
        public SceneObject sceneObject = new SceneObject();
    }
}
