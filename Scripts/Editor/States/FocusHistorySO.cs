using System.Collections.Generic;
using UnityEngine;

namespace SecretZauce.SecondBrain.Editor
{
    [System.Serializable]
    public class FocusHistoryEntry
    {
        public Object target;
    }

    public class FocusHistorySO : ScriptableObject
    {
        [SerializeField] List<FocusHistoryEntry> backStack = new List<FocusHistoryEntry>();
        [SerializeField] List<FocusHistoryEntry> forwardStack = new List<FocusHistoryEntry>();
        [SerializeField] FocusHistoryEntry currentEntry = new FocusHistoryEntry();

        public bool CanGoBack => backStack.Count > 0;
        public bool CanGoForward => forwardStack.Count > 0;

        public void SetTarget(Object newTarget)
        {
            if (currentEntry != null && ReferenceEquals(currentEntry.target, newTarget))
                return;

            if (currentEntry != null)
                backStack.Add(currentEntry);

            forwardStack.Clear();

            var entry = new FocusHistoryEntry { target = newTarget };
            currentEntry = entry;
        }

        public Object GoBack()
        {
            if (!CanGoBack)
                return currentEntry?.target;

            forwardStack.Insert(0, currentEntry);
            var prev = backStack[^1];
            backStack.RemoveAt(backStack.Count - 1);
            currentEntry = prev;
            return currentEntry?.target;
        }

        public Object GoForward()
        {
            if (!CanGoForward)
                return currentEntry?.target;

            backStack.Add(currentEntry);
            var next = forwardStack[0];
            forwardStack.RemoveAt(0);
            currentEntry = next;
            return currentEntry?.target;
        }

        public void Initialize(Object startingTarget)
        {
            currentEntry = new FocusHistoryEntry { target = startingTarget };
        }

        public Object CurrentTarget => currentEntry?.target;
    }
}
