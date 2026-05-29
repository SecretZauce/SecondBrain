using System;
using Object = UnityEngine.Object;

namespace SecretZauce.SecondBrain.Editor
{
    public interface ISearchPredicate
    {
        bool Matches(Object node);
    }

    public class FuncSearchPredicate : ISearchPredicate
    {
        readonly Func<Object, bool> func;
        public FuncSearchPredicate(Func<Object, bool> func)
        {
            this.func = func ?? (_ => true);
        }

        public bool Matches(Object node) => func(node);
    }
}

