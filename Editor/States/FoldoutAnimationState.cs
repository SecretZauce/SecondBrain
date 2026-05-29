using System.Collections.Generic;
using UnityEditor.AnimatedValues;
using UnityEngine.Events;

namespace SecretZauce.SecondBrain.Editor
{
    /// <summary>
    /// Manages per-node <see cref="AnimBool"/> instances to drive smooth
    /// expand / collapse animations in the tree view, similar to Unity's
    /// built-in Hierarchy window.
    ///
    /// Usage:
    ///   1. Construct with a repaint callback (usually <c>OwnerWindow.Repaint</c>).
    ///   2. In the draw loop, call <see cref="GetOrCreate"/> to obtain (or lazily
    ///      create) the <c>AnimBool</c> for a node, then set its <c>target</c>.
    ///   3. Use the returned <c>AnimBool.faded</c> value with
    ///      <c>EditorGUILayout.BeginFadeGroup</c> / <c>EndFadeGroup</c>.
    /// </summary>
    public sealed class FoldoutAnimationState
    {
        // Speed multiplier for the AnimBool easing — 4 ≈ ~250 ms, matching
        // the snappy feel of Unity's own hierarchy foldout animation.
        const float AnimationSpeed = 4f;

        readonly Dictionary<object, AnimBool> _animBools = new Dictionary<object, AnimBool>();

        // Single shared delegate so every AnimBool shares the same listener instance
        // (avoids repeated heap allocations and supports proper Add/Remove matching).
        readonly UnityAction _repaintAction;

        /// <param name="repaintCallback">
        /// Called whenever an animation value changes; should trigger a window repaint.
        /// </param>
        public FoldoutAnimationState(System.Action repaintCallback)
        {
            if (repaintCallback != null)
                _repaintAction = new UnityAction(repaintCallback);
        }

        /// <summary>
        /// Returns the <see cref="AnimBool"/> associated with <paramref name="key"/>,
        /// creating one (initialised to <paramref name="initialValue"/>) if absent.
        /// </summary>
        public AnimBool GetOrCreate(object key, bool initialValue)
        {
            if (key == null) return new AnimBool(initialValue); // safety: no caching for null keys

            if (!_animBools.TryGetValue(key, out var animBool))
            {
                animBool = new AnimBool(initialValue)
                {
                    speed = AnimationSpeed
                };
                if (_repaintAction != null)
                    animBool.valueChanged.AddListener(_repaintAction);
                _animBools[key] = animBool;
            }

            return animBool;
        }

        /// <summary>
        /// Removes the animation state for <paramref name="key"/> and unsubscribes
        /// its listener. Call when a node is deleted or the tree is rebuilt.
        /// </summary>
        public void Remove(object key)
        {
            if (key == null) return;
            if (_animBools.TryGetValue(key, out var animBool))
            {
                if (_repaintAction != null)
                    animBool.valueChanged.RemoveListener(_repaintAction);
                _animBools.Remove(key);
            }
        }

        /// <summary>
        /// Clears all animation state and removes all listeners.
        /// </summary>
        public void Clear()
        {
            if (_repaintAction != null)
            {
                foreach (var ab in _animBools.Values)
                    ab.valueChanged.RemoveListener(_repaintAction);
            }
            _animBools.Clear();
        }
    }
}

