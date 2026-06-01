using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SecretZauce.SecondBrain
{
    public enum DefaultExpandOption
    {
        // When there's no saved foldout state, treat nodes as expanded by default
        ExpandAsDefault = 0,
        // When there's no saved foldout state, treat nodes as collapsed by default
        CollapsedAsDefault = 1,
        // Always expand these nodes regardless of saved state
        AlwaysExpand = 2
    }

    [CreateAssetMenu(menuName = "ScriptableStructure/Container", fileName = "Container", order = 0)]
    public class Container : ScriptableObject, IObjectStructure, IHasEmoji, IHasColor, IHasChildViewPreference
    {
        public List<Object> Children => children;
        public Object CreateNew(Type type)
        {
           return CreateInstance<Container>(); 
        }

        public List<Object> children = new List<Object>();

        [SerializeField]
        private ChildViewMode preferredChildView = ChildViewMode.Foldouts;

        public ChildViewMode PreferredChildView
        {
            get => preferredChildView;
            set => preferredChildView = value;
        }

        [SerializeField]
        private string emojiIcon;
        public string EmojiIcon
        {
            get => emojiIcon;
            set => emojiIcon = value;
        }

        [SerializeField]
        [Tooltip("Determines default expand/collapse behavior for this container when there is no saved foldout history.")]
        private DefaultExpandOption defaultExpandOption = DefaultExpandOption.CollapsedAsDefault;

        /// <summary>
        /// When there is no saved foldout state for this container, this option controls
        /// whether nodes should be collapsed, expanded, or always expanded.
        /// </summary>
        public DefaultExpandOption DefaultExpand
        {
            get => defaultExpandOption;
            set => defaultExpandOption = value;
        }

        [SerializeField]
        private DefaultExpandOption childViewExpandOption = DefaultExpandOption.ExpandAsDefault;

        /// <summary>
        /// Default expand state for children shown in Quick Peek and ContainerChildrenInspector
        /// when no saved per-item foldout state exists.
        /// </summary>
        public DefaultExpandOption ChildViewExpand
        {
            get => childViewExpandOption;
            set => childViewExpandOption = value;
        }

        // PRO Version Only: Explicit "Disable Quick Peek" flag. When true, QuickPeek is disabled
        // for this container and all its descendants.
        [SerializeField]
        private bool disableQuickPeek = false;

        /// <summary>
        /// PRO Version Only: When true, Quick Peek is disabled for this container and all children, regardless of the global setting.
        /// </summary>
        public bool DisableQuickPeek
        {
            get => disableQuickPeek;
            set => disableQuickPeek = value;
        }

        [SerializeField]
        private bool hasLabelColor;
        [SerializeField]
        private Color labelColor = Color.white;
        [SerializeField]
        private ColorDisplayStyle labelColorStyle = ColorDisplayStyle.FontColor;
        [SerializeField]
        private bool labelColorFoldoutOnly = false;

        public bool HasLabelColor
        {
            get => hasLabelColor;
            set => hasLabelColor = value;
        }

        public Color LabelColor
        {
            get => labelColor;
            set => labelColor = value;
        }

        public ColorDisplayStyle LabelColorStyle
        {
            get => labelColorStyle;
            set => labelColorStyle = value;
        }

        public bool LabelColorFoldoutOnly
        {
            get => labelColorFoldoutOnly;
            set => labelColorFoldoutOnly = value;
        }

        public bool CanAcceptChild(Object child)
        {
            return child is not Base && child;
        }
    }
}