using System;
using UnityEngine;

namespace SecretZauce.SecondBrain
{
    public interface IScriptableStructure<T> : IStructure<T> where T : ScriptableObject
    {
        T IStructure<T>.CreateNew(Type type)
        {
            return ScriptableObject.CreateInstance<T>(); 
        }
    }
}