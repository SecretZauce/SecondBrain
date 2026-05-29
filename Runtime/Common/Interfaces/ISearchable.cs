using UnityEngine;

namespace SecretZauce.SecondBrain
{
    public interface ISearchable
    {
        bool MatchesSearch(string searchKeyword);

        public static bool MatchesSearch(Object obj, string searchKeyword)
        {
            // For non-ScriptableObject types, only match by name
            if (!(obj is ScriptableObject scriptableObj))
                return obj != null && obj.name.ToLower().Contains(searchKeyword.ToLower());
            
            // For ScriptableObject, use full search logic
            if (scriptableObj.name.ToLower().Contains(searchKeyword.ToLower()))
                return true;

            if (scriptableObj is ISearchable searchable && searchable.MatchesSearch(searchKeyword))
                return true;
            
            if (scriptableObj is IStructure structure)
            {
                var children = structure.ChildrenObjects;
                if (children != null)
                {
                    foreach (var child in children)
                    {
                        if (MatchesSearch(child, searchKeyword))
                            return true;
                    }
                }
            }
            
            return false;
        }
    }
}