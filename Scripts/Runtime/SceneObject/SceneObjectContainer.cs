using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SecretZauce.SecondBrain
{
    [CreateAssetMenu(menuName = "ScriptableStructure/SceneObjectContainer", fileName = "SceneObjectContainer", order = 0)]
    public class SceneObjectContainer : ScriptableObject, IStructure<Object>
    {
        public List<Object> Children => children.ConvertAll<Object>(child => null);
        public List<SceneObject> children = new List<SceneObject>();
        public Object CreateNew(Type type)
        {
            return CreateInstance<SceneObjectContainer>();
        }
    }
}