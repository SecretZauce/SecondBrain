using System.Collections.Generic;
using UnityEngine;

namespace SecretZauce.SecondBrain
{
    public abstract class TypedContainer<T> : ScriptableObject, IScriptableStructure<T>, IAllowMultipleParents where T : ScriptableObject
    {
        [SerializeField] List<T> itemList = new List<T>();
        public List<T> Children => itemList;
    }
}