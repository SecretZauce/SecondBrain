using System;
using UnityEditor;

namespace SecretZauce.SecondBrain.Editor
{
    public abstract class ActionItemHandlerBase
    {
        public abstract string TryCreateFromActionItem(Type childType, string defaultName);

        public abstract void AddToMenu(GenericMenu menu, Action<Type> onTypeSelected,
            string parentLabel = "Add Action");
    }
}