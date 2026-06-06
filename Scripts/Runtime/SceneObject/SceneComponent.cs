using System;
using UnityEngine;

namespace SecretZauce.SecondBrain
{
    [Serializable]
    public class SceneComponent
    {
        [SerializeField] string globalId;
        [SerializeField] string lastKnownScene;
        [SerializeField] string lastKnownSceneGuid;
        [SerializeField] string lastKnownPath;
        [SerializeField] string lastKnownGameObjectName;
        [SerializeField] string lastKnownComponentType;
        [SerializeField] string lastKnownComponentTypeName;
        [SerializeField] bool wasTrackedDuringPlayMode;

        public string GlobalId => globalId;
        public string LastKnownScene => lastKnownScene;
        public string LastKnownSceneGuid => lastKnownSceneGuid;
        public string LastKnownPath => lastKnownPath;
        public string LastKnownGameObjectName => lastKnownGameObjectName;
        public string LastKnownComponentType => lastKnownComponentType;
        public string LastKnownComponentTypeName => lastKnownComponentTypeName;
        public bool WasTrackedDuringPlayMode => wasTrackedDuringPlayMode;
    }
}
