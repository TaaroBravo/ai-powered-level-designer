using System;
using System.Collections.Generic;
using UnityEngine;

namespace AILevelDesigner.Profiles
{
   [CreateAssetMenu(fileName = "PrefabCatalog", menuName = "AILevelDesigner/Prefab Catalog")]
   public class PrefabCatalog : ScriptableObject
   {
       public List<Entry> entries = new List<Entry>();
   
       public bool TryGet(string id, out Entry entry)
       {
           entry = entries.Find(e => string.Equals(e.id, id, StringComparison.OrdinalIgnoreCase));
           return entry != null && entry.prefab != null;
       }
   
       public bool IsAllowed(string id) => 
           entries.Exists(e => string.Equals(e.id, id, StringComparison.OrdinalIgnoreCase));
   }
    
    [Serializable]
    public class Entry
    {
        public string id; 
        public GameObject prefab; 
        public int maxPerLevel = 999;
        public string[] tags;
    }
}