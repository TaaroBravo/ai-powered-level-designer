using System;
using System.Collections.Generic;
using System.Linq;
using AILevelDesigner.Profiles;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AILevelDesigner.Building
{
    public static class SceneBuilder
    {
        public static void Build(LayoutData data, GameTypeProfile profile, Transform parent = null)
        {
            if (parent == null) parent = new GameObject("AILevel").transform;
            parent.position = Vector3.zero;
            parent.localScale = Vector3.one;

            if (profile.generateGroundPlane)
            {
                if (profile.coordinateSpace == CoordinateSpace.Grid)
                {
                    var w = profile.gridWidth * profile.cellSize;
                    var h = profile.gridHeight * profile.cellSize;
                    var center = profile.gridOriginMode == GridOriginMode.BottomLeft
                        ? profile.gridOrigin + new Vector3(w * 0.5f, 0f, h * 0.5f)
                        : profile.gridOrigin;
                    MakePlane(parent, center, w, h, profile.groundMaterial);
                }
                else
                {
                    MakePlane(parent, Vector3.zero, profile.arenaSize.x, profile.arenaSize.y, profile.groundMaterial);
                }
            }

            var fitScale = 1f;
            var fitOffset = Vector3.zero;
            if (profile.coordinateSpace == CoordinateSpace.World && profile.autoFitToArena && data != null &&
                data.objects != null &&
                data.objects.Count > 0) ComputeWorldFit(data, profile, out fitScale, out fitOffset);

            if (profile.coordinateSpace == CoordinateSpace.Grid) FixTDGrid(data, profile);

            foreach (var o in data.objects)
            {
                if (!profile.catalog.TryGet(o.id, out var entry)) continue;
                var pos = profile.coordinateSpace == CoordinateSpace.Grid
                    ? ResolveGridPosition(o.position, profile)
                    : ResolveWorldPosition(o.position, profile, fitScale, fitOffset);
                var rot = o.rotation.HasValue ? Quaternion.Euler(o.rotation.Value) : Quaternion.identity;
                var go = Object.Instantiate(entry.prefab, pos, rot, parent);
                if (o.scale.HasValue) go.transform.localScale = o.scale.Value;
                go.name = o.id;
            }
        }

        private static void MakePlane(Transform parent, Vector3 center, float width, float height, Material mat)
        {
            var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = "Ground";
            plane.transform.SetParent(parent, false);
            plane.transform.position = center;
            plane.transform.localScale = new Vector3(width / 10f, 1f, height / 10f);
            if (mat) plane.GetComponent<Renderer>().sharedMaterial = mat;
        }

        private static Vector3 ResolveGridPosition(Vector3 src, GameTypeProfile p)
        {
            var gx = Mathf.Clamp(Mathf.RoundToInt(src.x), 0, p.gridWidth - 1);
            var gz = Mathf.Clamp(Mathf.RoundToInt(src.z), 0, p.gridHeight - 1);
            var w = p.gridWidth * p.cellSize;
            var h = p.gridHeight * p.cellSize;
            var originBL = p.gridOriginMode == GridOriginMode.BottomLeft
                ? p.gridOrigin
                : p.gridOrigin - new Vector3(w * 0.5f, 0f, h * 0.5f);
            return originBL + new Vector3((gx + 0.5f) * p.cellSize, 0f, (gz + 0.5f) * p.cellSize);
        }

        private static Vector3 ResolveWorldPosition(Vector3 src, GameTypeProfile p, float fitScale, Vector3 fitOffset)
        {
            var pos = (src + fitOffset) * (p.worldScale * fitScale);
            pos.y = 0f;
            if (p.snapToStep && p.step > 0f)
            {
                pos.x = Mathf.Round(pos.x / p.step) * p.step;
                pos.z = Mathf.Round(pos.z / p.step) * p.step;
            }

            if (p.clampToArena)
            {
                var hx = p.arenaSize.x * 0.5f;
                var hz = p.arenaSize.y * 0.5f;
                var pad = Mathf.Max(0f, p.clampPadding);
                pos.x = Mathf.Clamp(pos.x, -hx + pad, hx - pad);
                pos.z = Mathf.Clamp(pos.z, -hz + pad, hz - pad);
            }

            return pos;
        }

        private static void ComputeWorldFit(LayoutData data, GameTypeProfile p, out float scale, out Vector3 offset)
        {
            var minX = float.PositiveInfinity;
            var maxX = float.NegativeInfinity;
            var minZ = float.PositiveInfinity;
            var maxZ = float.NegativeInfinity;
            foreach (var o in data.objects)
            {
                var v = o.position * p.worldScale;
                if (v.x < minX) minX = v.x;
                if (v.x > maxX) maxX = v.x;
                if (v.z < minZ) minZ = v.z;
                if (v.z > maxZ) maxZ = v.z;
            }

            var width = Mathf.Max(0.0001f, maxX - minX);
            var height = Mathf.Max(0.0001f, maxZ - minZ);
            var cx = (minX + maxX) * 0.5f;
            var cz = (minZ + maxZ) * 0.5f;
            offset = new Vector3(-cx, 0f, -cz);
            var sx = (p.arenaSize.x * p.fitMargin) / width;
            var sz = (p.arenaSize.y * p.fitMargin) / height;
            scale = Mathf.Min(sx, sz);
        }

        private static void FixTDGrid(LayoutData data, GameTypeProfile p)
        {
            var entries = p.catalog.entries.Where(e => e != null && !string.IsNullOrWhiteSpace(e.id)).ToList();
            var map = entries.ToDictionary(e => e.id, e => e, System.StringComparer.OrdinalIgnoreCase);

            Func<string, bool> isPathId = id =>
            {
                if (map.TryGetValue(id, out var e) && e.tags != null &&
                    e.tags.Any(t => string.Equals(t, "path", StringComparison.OrdinalIgnoreCase))) return true;
                var s = id ?? string.Empty;
                return s.IndexOf("path", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       s.IndexOf("tile", StringComparison.OrdinalIgnoreCase) >= 0;
            };
            Func<string, bool> isSlotId = id =>
            {
                if (map.TryGetValue(id, out var e) && e.tags != null && e.tags.Any(t =>
                        string.Equals(t, "towerSlot", StringComparison.OrdinalIgnoreCase))) return true;
                var s = id ?? string.Empty;
                return s.IndexOf("slot", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       s.IndexOf("tower", StringComparison.OrdinalIgnoreCase) >= 0;
            };
            Func<string, bool> isSpawnerId = id =>
            {
                if (map.TryGetValue(id, out var e) && e.tags != null && e.tags.Any(t =>
                        string.Equals(t, "spawner", StringComparison.OrdinalIgnoreCase))) return true;
                var s = id ?? string.Empty;
                return s.IndexOf("spawn", StringComparison.OrdinalIgnoreCase) >= 0;
            };
            Func<string, bool> isBaseId = id =>
            {
                if (map.TryGetValue(id, out var e) && e.tags != null &&
                    e.tags.Any(t => string.Equals(t, "base", StringComparison.OrdinalIgnoreCase))) return true;
                var s = id ?? string.Empty;
                return s.IndexOf("base", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       s.IndexOf("core", StringComparison.OrdinalIgnoreCase) >= 0;
            };

            Func<Vector3, (int x, int z)> cellOf = v => (Mathf.Clamp(Mathf.RoundToInt(v.x), 0, p.gridWidth - 1),
                Mathf.Clamp(Mathf.RoundToInt(v.z), 0, p.gridHeight - 1));

            var pathId = data.objects.Where(o => isPathId(o.id)).Select(o => o.id).FirstOrDefault()
                         ?? entries.FirstOrDefault(e => isPathId(e.id))?.id;

            var spawner = data.objects.FirstOrDefault(o => isSpawnerId(o.id));
            var goal = data.objects.FirstOrDefault(o => isBaseId(o.id));
            var slots = data.objects.Where(o => isSlotId(o.id)).ToList(); 

            if (spawner != null) spawner.position = ClampToEdge(cellOf(spawner.position), p);
            if (goal != null) goal.position = ClampToEdge(cellOf(goal.position), p);

            List<(int x, int z)> pathCells = null;
            if (spawner != null && goal != null)
            {
                var start = cellOf(spawner.position);
                var end = cellOf(goal.position);
                pathCells = BFSPath(start, end, p.gridWidth, p.gridHeight) ?? new List<(int, int)> {start, end};
                var straightX = pathCells.TrueForAll(c => c.x == pathCells[0].x);
                var straightZ = pathCells.TrueForAll(c => c.z == pathCells[0].z);
                if (straightX || straightZ)
                    pathCells = BuildCurvedPath(start, end, p.gridWidth, p.gridHeight, 2);

                data.objects.RemoveAll(o => isPathId(o.id));
                if (!string.IsNullOrEmpty(pathId))
                {
                    foreach (var c in pathCells)
                    {
                        if ((spawner != null && c == cellOf(spawner.position)) ||
                            (goal != null && c == cellOf(goal.position))) continue;
                        data.objects.Add(new LayoutObject {id = pathId, position = new Vector3(c.x, 0f, c.z)});
                    }
                }
            }

            var pathSet = pathCells != null ? new HashSet<(int, int)>(pathCells) : new HashSet<(int, int)>();
            var occupied = new HashSet<(int, int)>(pathSet);
            if (spawner != null) occupied.Add(cellOf(spawner.position));
            if (goal != null) occupied.Add(cellOf(goal.position));

            foreach (var s in slots)
            {
                var c = cellOf(s.position);
                var okAdj = IsAdjToPath(c, pathSet, p.gridWidth, p.gridHeight);
                if (occupied.Contains(c) || !okAdj)
                {
                    var target = NearestFreeAdjToPath(c, occupied, pathSet, p.gridWidth, p.gridHeight) ??
                                 NearestFree(c, occupied, p.gridWidth, p.gridHeight);
                    if (target.HasValue)
                    {
                        var t = target.Value;
                        s.position = new Vector3(t.x, 0f, t.z);
                        occupied.Add(t);
                    }
                }
                else
                {
                    occupied.Add(c);
                }
            }

            var seen = new HashSet<(string id, int x, int z)>();
            for (var i = data.objects.Count - 1; i >= 0; i--)
            {
                var o = data.objects[i];
                var c = cellOf(o.position);
                var k = (o.id, c.x, c.z);
                if (!seen.Add(k)) data.objects.RemoveAt(i);
            }

            var slotObjs = data.objects.Where(o => isSlotId(o.id)).ToList();
            foreach (var g in slotObjs.GroupBy(o => o.id))
            {
                var cap = CapForId(g.Key, map, 10);
                var ordered = g.Select(o =>
                    {
                        var c = cellOf(o.position);
                        var adj = IsAdjToPath(c, pathSet, p.gridWidth, p.gridHeight) ? 0 : 1000;
                        var score = adj + MinDistToPath(c, pathSet);
                        return (o, score);
                    })
                    .OrderBy(t => t.score)
                    .Select(t => t.o)
                    .ToList();
                var keep = new HashSet<LayoutObject>(ordered.Take(cap));
                for (var i = data.objects.Count - 1; i >= 0; i--)
                    if (isSlotId(data.objects[i].id) && !keep.Contains(data.objects[i]))
                        data.objects.RemoveAt(i);
            }

            Func<string, bool> isDecoId = id =>
            {
                if (map.TryGetValue(id, out var e) && e.tags != null && e.tags.Any(t =>
                        string.Equals(t, "decoration", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(t, "prop", StringComparison.OrdinalIgnoreCase))) return true;
                var s = id ?? string.Empty;
                return s.IndexOf("rock", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       s.IndexOf("tree", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       s.IndexOf("deco", StringComparison.OrdinalIgnoreCase) >= 0;
            };
            var deco = data.objects.Where(o => isDecoId(o.id)).ToList();
            if (deco.Count > 0)
            {
                var cap = CapForId(deco[0].id, map, 3);
                var ordered = deco.Select(o =>
                    {
                        var c = cellOf(o.position);
                        var score = -MinDistToPath(c, pathSet);
                        return (o, score);
                    })
                    .OrderBy(t => t.score)
                    .Select(t => t.o)
                    .ToList();
                var keep = new HashSet<LayoutObject>(ordered.Take(cap));
                for (var i = data.objects.Count - 1; i >= 0; i--)
                    if (isDecoId(data.objects[i].id) && !keep.Contains(data.objects[i]))
                        data.objects.RemoveAt(i);
            }

        }


        private static Vector3 ClampToEdge((int x, int z) cell, GameTypeProfile p)
        {
            var x = cell.x;
            var z = cell.z;
            var left = x;
            var right = (p.gridWidth - 1) - x;
            var bottom = z;
            var top = (p.gridHeight - 1) - z;
            var m = Mathf.Min(Mathf.Min(left, right), Mathf.Min(bottom, top));
            if (m == left) x = 0;
            else if (m == right) x = p.gridWidth - 1;
            else if (m == bottom) z = 0;
            else z = p.gridHeight - 1;
            return new Vector3(x, 0f, z);
        }

        private static List<(int x, int z)> BFSPath((int x, int z) start, (int x, int z) goal, int w, int h)
        {
            var dirs = new (int dx, int dz)[] {(1, 0), (-1, 0), (0, 1), (0, -1)};
            var s = (start.x, start.z);
            var g = (goal.x, goal.z);
            var q = new Queue<(int x, int z)>();
            var seen = new HashSet<(int x, int z)>();
            var prev = new Dictionary<(int x, int z), (int x, int z)>();
            q.Enqueue(s);
            seen.Add(s);
            while (q.Count > 0)
            {
                var u = q.Dequeue();
                if (u.Item1 == g.Item1 && u.Item2 == g.Item2) break;
                foreach (var d in dirs)
                {
                    var v = (u.Item1 + d.dx, u.Item2 + d.dz);
                    if (v.Item1 < 0 || v.Item1 >= w || v.Item2 < 0 || v.Item2 >= h) continue;
                    if (seen.Add(v))
                    {
                        prev[v] = u;
                        q.Enqueue(v);
                    }
                }
            }

            if (!(s.Item1 == g.Item1 && s.Item2 == g.Item2) && !prev.ContainsKey(g)) return null;
            var path = new List<(int, int)>();
            var cur = g;
            path.Add(cur);
            while (!(cur.Item1 == s.Item1 && cur.Item2 == s.Item2))
            {
                if (!prev.TryGetValue(cur, out var pcur)) break;
                cur = pcur;
                path.Add(cur);
            }

            path.Reverse();
            var res = new List<(int x, int z)>(path.Count);
            foreach (var c in path) res.Add((c.Item1, c.Item2));
            return res;
        }

        private static (int x, int z)? NearestFree((int x, int z) start, HashSet<(int, int)> blocked, int w, int h)
        {
            var dirs = new (int dx, int dz)[] {(1, 0), (-1, 0), (0, 1), (0, -1)};
            var s = (start.x, start.z);
            var q = new Queue<(int, int)>();
            var seen = new HashSet<(int, int)>();
            q.Enqueue(s);
            seen.Add(s);
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                if (!blocked.Contains(c)) return (c.Item1, c.Item2);
                foreach (var d in dirs)
                {
                    var n = (c.Item1 + d.dx, c.Item2 + d.dz);
                    if (n.Item1 < 0 || n.Item1 >= w || n.Item2 < 0 || n.Item2 >= h) continue;
                    if (seen.Add(n)) q.Enqueue(n);
                }
            }

            return null;
        }

        private static (int x, int z)? NearestFreeAdjToPath((int x, int z) start, HashSet<(int, int)> blocked,
            HashSet<(int, int)> path, int w, int h)
        {
            var dirs = new (int dx, int dz)[] {(1, 0), (-1, 0), (0, 1), (0, -1)};
            var q = new Queue<(int, int)>();
            var seen = new HashSet<(int, int)>();
            q.Enqueue(start);
            seen.Add(start);
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                if (!blocked.Contains(c) && !path.Contains(c))
                {
                    var adj = false;
                    for (var i = 0; i < 4; i++)
                    {
                        var n = (c.Item1 + dirs[i].dx, c.Item2 + dirs[i].dz);
                        if (n.Item1 < 0 || n.Item1 >= w || n.Item2 < 0 || n.Item2 >= h) continue;
                        if (path.Contains(n))
                        {
                            adj = true;
                            break;
                        }
                    }

                    if (adj) return c;
                }

                for (var i = 0; i < 4; i++)
                {
                    var n = (c.Item1 + dirs[i].dx, c.Item2 + dirs[i].dz);
                    if (n.Item1 < 0 || n.Item1 >= w || n.Item2 < 0 || n.Item2 >= h) continue;
                    if (seen.Add(n)) q.Enqueue(n);
                }
            }

            return null;
        }

        private static bool IsAdjToPath((int x, int z) c, HashSet<(int, int)> path, int w, int h)
        {
            var dirs = new (int dx, int dz)[] {(1, 0), (-1, 0), (0, 1), (0, -1)};
            var (cx, cz) = c;
            for (var i = 0; i < 4; i++)
            {
                var n = (cx + dirs[i].dx, cz + dirs[i].dz);
                var nx = n.Item1;
                var nz = n.Item2;
                if (nx < 0 || nx >= w || nz < 0 || nz >= h) continue;
                if (path.Contains((nx, nz))) return true;
            }

            return false;
        }

        private static List<(int x, int z)> BuildCurvedPath((int x, int z) start, (int x, int z) goal, int w, int h,
            int turns)
        {
            var waypoints = new List<(int x, int z)>();
            waypoints.Add(start);
            if (turns >= 1)
            {
                var t1x = Mathf.Clamp(w / 3, 0, w - 1);
                var r1 = (start.z < goal.z) ? Mathf.Clamp(2 * h / 3, 0, h - 1) : Mathf.Clamp(h / 3, 0, h - 1);
                waypoints.Add((t1x, r1));
            }

            if (turns >= 2)
            {
                var t2x = Mathf.Clamp(2 * w / 3, 0, w - 1);
                var r2 = (start.z < goal.z) ? Mathf.Clamp(h / 3, 0, h - 1) : Mathf.Clamp(2 * h / 3, 0, h - 1);
                waypoints.Add((t2x, r2));
            }

            waypoints.Add(goal);

            var path = new List<(int x, int z)>();
            for (var i = 0; i < waypoints.Count - 1; i++)
            {
                var seg = BFSPath(waypoints[i], waypoints[i + 1], w, h);
                if (seg == null || seg.Count == 0) continue;
                if (path.Count > 0) seg.RemoveAt(0);
                foreach (var c in seg) path.Add((c.x, c.z));
            }

            return path;
        }

        private static int MinDistToPath((int x, int z) c, HashSet<(int, int)> path)
        {
            var (cx, cz) = c;
            var best = int.MaxValue;
            foreach (var p in path)
            {
                var d = Mathf.Abs(p.Item1 - cx) + Mathf.Abs(p.Item2 - cz);
                if (d < best) best = d;
            }

            return best == int.MaxValue ? 0 : best;
        }

        private static int CapForId(string id, System.Collections.Generic.Dictionary<string, Entry> map, int fallback)
        {
            if (!string.IsNullOrEmpty(id) && map.TryGetValue(id, out var e) && e.maxPerLevel > 0) return e.maxPerLevel;
            return fallback;
        }

    }
}