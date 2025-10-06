using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AILevelDesigner
{
    public static class LayoutSanitizer
    {
         public static LayoutData PruneToCatalogCaps(LayoutData data, Profiles.GameTypeProfile profile)
        {
            if (data == null || data.objects == null || profile == null || profile.catalog == null)
                return data;

            var entries = profile.catalog.entries
                .Where(e => e != null && !string.IsNullOrWhiteSpace(e.id))
                .ToDictionary(e => e.id, e => e, StringComparer.OrdinalIgnoreCase);

            var pathCells = new List<Vector3>();
            foreach (var o in data.objects)
                if (o.id != null && o.id.ToLower().Contains("path"))
                    pathCells.Add(o.position);

            var keep = new List<LayoutObject>();

            foreach (var group in data.objects.GroupBy(o => o.id, StringComparer.OrdinalIgnoreCase))
            {
                var id = group.Key ?? "";
                var cap = entries.TryGetValue(id, out var entry) && entry.maxPerLevel > 0
                    ? entry.maxPerLevel
                    : int.MaxValue;

                if (cap == int.MaxValue)
                {
                    keep.AddRange(group);
                    continue;
                }

                var objs = group.ToList();

                if (pathCells.Count > 0 && (id.ToLower().Contains("tower") || id.ToLower().Contains("slot") || id.ToLower().Contains("decor")))
                {
                    objs = objs
                        .OrderBy(o => DistanceToNearestPath(o.position, pathCells))
                        .Take(cap)
                        .ToList();
                }
                else
                {
                    objs = (profile.coordinateSpace == Profiles.CoordinateSpace.Grid)
                        ? objs.OrderBy(o => o.position.z).ThenBy(o => o.position.x).Take(cap).ToList()
                        : objs.Take(cap).ToList();
                }

                keep.AddRange(objs);
            }

            data.objects = keep;
            return data;
        }

        private static float DistanceToNearestPath(Vector3 pos, List<Vector3> pathCells)
        {
            var min = float.MaxValue;
            foreach (var p in pathCells)
            {
                var d = Mathf.Abs(pos.x - p.x) + Mathf.Abs(pos.z - p.z);
                if (d < min) min = d;
            }
            return min;
        }
    }
}