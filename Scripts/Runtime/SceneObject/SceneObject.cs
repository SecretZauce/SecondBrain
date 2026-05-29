using System; 
using UnityEngine;

namespace SecretZauce.SecondBrain
{
    [Serializable]
    public class SceneObject
    {
        [SerializeField] string globalId;
        [SerializeField] string lastKnownName;
        [SerializeField] string lastKnownScene;
        [SerializeField] string lastKnownPath;

        public string GlobalId => globalId;
        public string LastKnownName => lastKnownName;
        public string LastKnownScene => lastKnownScene;
        public string LastKnownPath => lastKnownPath;
    }
}
