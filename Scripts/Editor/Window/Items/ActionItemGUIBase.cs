#if SECOND_BRAIN_PRO
using UnityEngine;

namespace SecretZauce.SecondBrain.Editor
{
    public abstract class ActionItemGUIBase
    {
        protected ActionItemGUIBase(ActionItem actionItem, GUIStyle style, Rect arrowRect, Rect rowRect)
        {
        }

        public abstract bool TryDrawActionItem();
        public abstract GUIContent TryAppendActionItemDetail(GUIContent labelContent);
    }
}
#endif