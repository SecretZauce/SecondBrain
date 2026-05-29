using System.Collections.Generic;
using UnityEngine;

namespace SecretZauce.SecondBrain
{
    public class Base : ScriptableObject, IScriptableStructure<Container>, IHasEditorIcon, IHasEmoji, IHasColor
    {
        public List<Container> Children => children; 
        [SerializeField] List<Container> children = new List<Container>();
        public string EditorIcon => "";

        /// <summary>
        /// GUID of the scene that this Base is linked to.
        /// When this scene is opened in the Unity Editor the SecondBrainWindow
        /// will automatically open a window targeting this Base.
        /// </summary>
        [SerializeField]
        private string sceneGuid;

        public string SceneGuid
        {
            get => sceneGuid;
            set => sceneGuid = value;
        }

        [SerializeField]
        private string emojiIcon;
        public string EmojiIcon
        {
            get => emojiIcon;
            set => emojiIcon = value;
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
    }
}