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
        [SerializeField] string lastKnownSceneGuid;
        [SerializeField] string lastKnownPath;
        [SerializeField] bool wasTrackedDuringPlayMode;

        public string GlobalId => globalId;
        public string LastKnownName => lastKnownName;
        public string LastKnownScene => lastKnownScene;
        public string LastKnownSceneGuid => lastKnownSceneGuid;
        public string LastKnownPath => lastKnownPath;
        public bool WasTrackedDuringPlayMode => wasTrackedDuringPlayMode;
    }
}
