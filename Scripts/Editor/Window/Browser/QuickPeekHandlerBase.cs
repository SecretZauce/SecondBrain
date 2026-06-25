#if SECOND_BRAIN_PRO
using UnityEngine;

namespace SecretZauce.SecondBrain.Editor
{
    public abstract class QuickPeekHandlerBase
    {
        protected QuickPeekHandlerBase(BrowserWindow caller)
        {
        }

        public abstract void HandleQuickPeekCloseCheck();
        public abstract void UpdateQuickPeek();
        public abstract void CloseQuickPeek();
        public abstract bool IsOpenForPath(int[] path);
        public abstract void OpenFor(int[] selectedPath, BrowserWindow window);
        public abstract void DisposeQuickPeek();
        public abstract bool HasPendingShow { get; }
    }
}
#endif