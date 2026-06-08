using UnityEditor;
using UnityEngine;

namespace SecretZauce.SecondBrain.Editor
{
    public static class IconUtils
    {
        const string IconsAssetPath = "Editor/Icons/";
        const string LightSuffix    = "_black";
        const string DarkSuffix     = "_white";

        public static Texture2D Load(string name)
        {
            string suffix = EditorGUIUtility.isProSkin ? DarkSuffix : LightSuffix;
            return Resources.Load<Texture2D>(IconsAssetPath + name + suffix);
        }
    }
}