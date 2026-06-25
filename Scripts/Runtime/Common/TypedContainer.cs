using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SecretZauce.SecondBrain
{
    public abstract class TypedContainer<T> : ScriptableObject, IScriptableStructure<T>, IAllowMultipleParents where T : ScriptableObject
    {
        [SerializeField] List<T> itemList = new List<T>();
        public List<T> Children => itemList;

        // Unity Mono's scripting backend does not reliably perform virtual dispatch when
        // a default interface method (IStructure<T>.ChildrenObjects) calls a member
        // (Children) on a generic abstract class. This explicit implementation ensures
        // ChildrenObjects always returns the correct list so QuickPeek can preview children.
        List<Object> IStructure.ChildrenObjects =>
            itemList?.ConvertAll(item => (Object)item) ?? new List<Object>();
    }
}