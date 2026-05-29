using UnityEditor;
using UnityEngine;

namespace SecretZauce.SecondBrain.Editor
{
    /// <summary>
    /// Encapsulates the resizable separator / splitter logic used by BrowserWindow.
    /// Keeps left pane width and resizing state in one place so it can be unit-tested / reused.
    /// </summary>
    internal class Splitter
    {
        public float LeftPaneWidth { get; private set; }
        public bool IsResizing { get; private set; }

        readonly float separatorWidth;
        readonly float separatorHitWidth;
        readonly float minPaneWidth;

        public Splitter(float initialLeftWidth = 300f, float minPaneWidth = 150f, float separatorWidth = 2f, float separatorHitWidth = 8f)
        {
            LeftPaneWidth = initialLeftWidth;
            this.minPaneWidth = minPaneWidth;
            this.separatorWidth = separatorWidth;
            this.separatorHitWidth = separatorHitWidth;
        }

        public void CancelResize()
        {
            IsResizing = false;
        }

        public void DrawSeparator(Rect windowPosition)
        {
            var singleLineHeight = EditorGUIUtility.singleLineHeight + 2;
            Rect separatorRect = new Rect(LeftPaneWidth, 0, separatorWidth, windowPosition.height - singleLineHeight);
            separatorRect.y += singleLineHeight;
            EditorGUI.DrawRect(separatorRect, new Color(0.5f, 0.5f, 0.5f, 0.5f));
            EditorGUIUtility.AddCursorRect(separatorRect, MouseCursor.ResizeHorizontal);
        }

        /// <summary>
        /// Handle mouse events related to the splitter.
        /// Returns true if the event was used. The out parameter indicates whether the left width changed
        /// (useful to decide if a Repaint should be requested).
        /// </summary>
        public bool HandleEvents(Event e, Rect windowPosition, out bool leftWidthChanged)
        {
            leftWidthChanged = false;
            if (e == null)
                return false;

            float hitAreaX = LeftPaneWidth - (separatorHitWidth - separatorWidth) / 2f;
            Rect hitRect = new Rect(hitAreaX, 0, separatorHitWidth, windowPosition.height);

            // Always show resize cursor on hover
            EditorGUIUtility.AddCursorRect(hitRect, MouseCursor.ResizeHorizontal);

            switch (e.type)
            {
                case EventType.MouseDown:
                    if (e.button == 0 && hitRect.Contains(e.mousePosition))
                    {
                        IsResizing = true;
                        e.Use();
                        return true;
                    }

                    break;

                case EventType.MouseUp:
                    if (IsResizing)
                    {
                        IsResizing = false;
                        e.Use();
                        return true;
                    }

                    break;

                case EventType.MouseDrag:
                    if (IsResizing)
                    {
                        float newLeft = Mathf.Clamp(e.mousePosition.x, minPaneWidth, windowPosition.width - minPaneWidth - separatorWidth);
                        if (!Mathf.Approximately(newLeft, LeftPaneWidth))
                        {
                            LeftPaneWidth = newLeft;
                            leftWidthChanged = true;
                        }

                        e.Use();
                        return true;
                    }

                    break;
            }

            return false;
        }
    }
}

