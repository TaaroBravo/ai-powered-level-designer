using AILevelDesigner.Profiles;
using UnityEngine;

namespace AILevelDesigner.Building
{
    public static class SceneBuilder
    {
        public static void Build(LayoutData data, GameTypeProfile profile, Transform parent = null)
        {
            var catalog = profile.catalog;
            foreach (var o in data.objects)
            {
                if (!catalog.TryGet(o.id, out var entry)) 
                    continue;
                
                var rot = o.rotation.HasValue ? Quaternion.Euler(o.rotation.Value) : Quaternion.identity;
                var go = Object.Instantiate(entry.prefab, o.position, rot, parent);
                if (o.scale.HasValue) 
                    go.transform.localScale = o.scale.Value;
                go.name = $"{o.id}";
            }
        }
    }
}