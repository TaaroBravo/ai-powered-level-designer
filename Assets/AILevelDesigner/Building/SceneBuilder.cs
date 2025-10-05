using AILevelDesigner.Profiles;
using UnityEngine;

namespace AILevelDesigner.Building
{
    public static class SceneBuilder
    {
  public static void Build(LayoutData data, GameTypeProfile profile, Transform parent = null)
        {
            if (parent == null) 
                parent = new GameObject("AILevel").transform;

            if (profile.generateGroundPlane)
            {
                var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
                plane.name = "Ground";
                plane.transform.SetParent(parent, false);
                plane.transform.localScale = new Vector3(profile.arenaSize.x / 10f, 1f, profile.arenaSize.y / 10f);
                if (profile.groundMaterial) plane.GetComponent<Renderer>().sharedMaterial = profile.groundMaterial;
            }

            var catalog = profile.catalog;

            foreach (var o in data.objects)
            {
                if (!catalog.TryGet(o.id, out var entry)) continue;

                var pos = ResolvePosition(o.position, profile);
                var rot = o.rotation.HasValue ? Quaternion.Euler(o.rotation.Value) : Quaternion.identity;

                var go = Object.Instantiate(entry.prefab, pos, rot, parent);
                if (o.scale.HasValue) go.transform.localScale = o.scale.Value;
                go.name = o.id;
            }
        }

        private static Vector3 ResolvePosition(Vector3 src, GameTypeProfile profile)
        {
            if (profile.coordinateSpace == CoordinateSpace.Grid)
            {
                var gx = Mathf.RoundToInt(src.x);
                var gz = Mathf.RoundToInt(src.z);
                gx = Mathf.Clamp(gx, 0, profile.gridWidth  - 1);
                gz = Mathf.Clamp(gz, 0, profile.gridHeight - 1);

                var world = profile.gridOrigin + new Vector3(gx * profile.cellSize, 0f, gz * profile.cellSize);
                return world;
            }
            
            var p = src * profile.worldScale;
            p.y = 0f;
            return p;
        }
    }
}