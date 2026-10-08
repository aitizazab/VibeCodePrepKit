using System.Collections.Generic;

namespace JamKit.Doomlike
{
    /// <summary>What the validator found out about a map besides its problems.</summary>
    public sealed class MapReport
    {
        public readonly MapProblems problems = new MapProblems();
        public bool exitReachable;
        /// <summary>Key colours in the order the player can first collect them.</summary>
        public readonly List<KeyColor> keyOrder = new List<KeyColor>();
        /// <summary>Per cell: reachable from the start with every key the map lets you collect.</summary>
        public bool[] reachable = new bool[0];
        public int enemies, reachableEnemies, pickups, secrets, triggers;

        public bool Ok => problems.Ok;
    }

    /// <summary>
    /// Checks that a level is finishable: one start, an exit, and the exit reachable by walking,
    /// honouring locked doors in key order (a key behind its own door, or a door with no key on the
    /// map, is an error). Doors must sit between two walls. Unreachable enemies and pickups, and
    /// ambush triggers with nothing to open, are warnings.
    /// </summary>
    public static class MapValidator
    {
        /// <summary>Cells around a trigger (in each direction) whose closet walls and enemies it wakes.</summary>
        public const int DefaultAmbushRadius = 8;

        static readonly int[] Dx = { 1, -1, 0, 0 };
        static readonly int[] Dy = { 0, 0, 1, -1 };

        public static MapReport Validate(MapData map, int ambushRadius = DefaultAmbushRadius)
        {
            var report = new MapReport();
            var p = report.problems;
            if (map == null || map.width == 0 || map.height == 0)
            {
                p.Error("The map is empty.");
                return report;
            }
            if (map.startX < 0) p.Error($"No player start ('{MapLegend.Start}').");
            if (map.exits.Count == 0) p.Error($"No exit ('{MapLegend.Exit}').");

            CheckDoors(map, p);
            CheckKeysExist(map, p);

            if (map.startX >= 0)
            {
                Reach(map, report);
                CheckReachability(map, report);
            }
            CheckAmbushes(map, ambushRadius, report);

            report.enemies = map.CountThings(EntityCategory.Enemy);
            report.pickups = map.CountThings(EntityCategory.Pickup) + map.CountThings(EntityCategory.Weapon);
            report.secrets = map.Count(CellKind.Secret);
            report.triggers = map.Count(CellKind.Trigger);
            if (report.enemies == 0) p.Warn("There are no enemies on this map.");
            return report;
        }

        static void CheckDoors(MapData map, MapProblems p)
        {
            for (int y = 0; y < map.height; y++)
                for (int x = 0; x < map.width; x++)
                {
                    var k = map.KindAt(x, y);
                    if (k != CellKind.Door && k != CellKind.LockedDoor) continue;
                    bool ew = map.IsSolidGeometry(x - 1, y) && map.IsSolidGeometry(x + 1, y);
                    bool ns = map.IsSolidGeometry(x, y - 1) && map.IsSolidGeometry(x, y + 1);
                    if (!ew && !ns)
                        p.Error($"The door at {MapData.Where(x, y)} needs walls on both sides (left and right, or above and below).");
                    else if (ew && ns)
                        p.Error($"The door at {MapData.Where(x, y)} is walled in on all four sides.");
                }
        }

        static void CheckKeysExist(MapData map, MapProblems p)
        {
            for (int color = 0; color < 3; color++)
            {
                int doors = 0, keys = 0, fx = 0, fy = 0;
                for (int y = 0; y < map.height; y++)
                    for (int x = 0; x < map.width; x++)
                    {
                        var e = map.Get(x, y).entry;
                        if (e.kind == CellKind.LockedDoor && e.variant == color)
                        {
                            if (doors == 0) { fx = x; fy = y; }
                            doors++;
                        }
                        else if (e.kind == CellKind.Key && e.variant == color) keys++;
                    }
                string name = MapLegend.KeyName(color);
                if (doors > 0 && keys == 0)
                    p.Error($"The {name} door at {MapData.Where(fx, fy)} has no {name} key anywhere on the map.");
                if (keys > 1) p.Warn($"There are {keys} {name} keys; one is enough.");
                if (keys > 0 && doors == 0) p.Warn($"There's a {name} key but no {name} door.");
            }
        }

        /// <summary>Can someone holding <paramref name="keys"/> (bit per colour) walk through this cell?</summary>
        public static bool Passable(MapData map, int x, int y, int keys)
        {
            var e = map.Get(x, y).entry;
            switch (e.kind)
            {
                case CellKind.Void:
                case CellKind.Wall:
                    return false;
                case CellKind.LockedDoor:
                    return (keys & (1 << e.variant)) != 0;
                case CellKind.Entity:
                    return !e.solid;
                default:
                    return true;
            }
        }

        /// <summary>Flood fill from the start with a key set. Fills <paramref name="seen"/>.</summary>
        public static void Flood(MapData map, int keys, bool[] seen, Queue<int> queue)
        {
            for (int i = 0; i < seen.Length; i++) seen[i] = false;
            queue.Clear();
            int start = map.startY * map.width + map.startX;
            seen[start] = true;
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int c = queue.Dequeue();
                int cx = c % map.width, cy = c / map.width;
                for (int d = 0; d < 4; d++)
                {
                    int nx = cx + Dx[d], ny = cy + Dy[d];
                    if (!map.InBounds(nx, ny)) continue;
                    int n = ny * map.width + nx;
                    if (seen[n] || !Passable(map, nx, ny, keys)) continue;
                    seen[n] = true;
                    queue.Enqueue(n);
                }
            }
        }

        static void Reach(MapData map, MapReport report)
        {
            var seen = new bool[map.width * map.height];
            var queue = new Queue<int>();
            int keys = 0;
            while (true)
            {
                Flood(map, keys, seen, queue);
                int found = 0;
                for (int i = 0; i < map.things.Count; i++)
                {
                    var t = map.things[i];
                    if (t.entry.kind != CellKind.Key || !seen[t.y * map.width + t.x]) continue;
                    int bit = 1 << t.entry.variant;
                    if ((keys & bit) != 0 || (found & bit) != 0) continue;
                    found |= bit;
                    report.keyOrder.Add((KeyColor)t.entry.variant);
                }
                if (found == 0) break;
                keys |= found;
            }
            report.reachable = seen;
            for (int i = 0; i < map.exits.Count; i++)
                if (seen[map.exits[i].y * map.width + map.exits[i].x]) report.exitReachable = true;
        }

        static void CheckReachability(MapData map, MapReport report)
        {
            var p = report.problems;
            var seen = report.reachable;
            if (map.exits.Count > 0 && !report.exitReachable)
            {
                string keys = report.keyOrder.Count == 0 ? "no keys" : "only the " + KeyList(report.keyOrder) + " key" + (report.keyOrder.Count > 1 ? "s" : "");
                p.Error($"The exit can't be reached from the start (with {keys}). Check the walls and locked doors on the way.");
            }

            // Keys the player can never pick up: behind their own door, or walled off.
            var scratch = new bool[seen.Length];
            var queue = new Queue<int>();
            for (int i = 0; i < map.things.Count; i++)
            {
                var t = map.things[i];
                if (t.entry.kind != CellKind.Key || seen[t.y * map.width + t.x]) continue;
                string name = MapLegend.KeyName(t.entry.variant);
                Flood(map, 7, scratch, queue);
                if (scratch[t.y * map.width + t.x])
                    p.Error($"The {name} key at {MapData.Where(t.x, t.y)} is only reachable through a door it (or a later key) opens.");
                else
                    p.Error($"The {name} key at {MapData.Where(t.x, t.y)} can't be reached at all.");
            }

            int unreachableEnemies = 0, unreachablePickups = 0;
            for (int i = 0; i < map.things.Count; i++)
            {
                var t = map.things[i];
                if (t.entry.kind != CellKind.Entity) continue;
                bool ok = seen[t.y * map.width + t.x] || NextToReachable(map, seen, t.x, t.y);
                if (t.entry.category == EntityCategory.Enemy)
                {
                    if (ok) report.reachableEnemies++;
                    else unreachableEnemies++;
                }
                else if (!ok && (t.entry.category == EntityCategory.Pickup || t.entry.category == EntityCategory.Weapon)) unreachablePickups++;
            }
            if (unreachableEnemies > 0) p.Warn($"{unreachableEnemies} enemies can't be reached (the kill count can't reach 100%).");
            if (unreachablePickups > 0) p.Warn($"{unreachablePickups} pickups can't be reached.");
        }

        static bool NextToReachable(MapData map, bool[] seen, int x, int y)
        {
            for (int d = 0; d < 4; d++)
            {
                int nx = x + Dx[d], ny = y + Dy[d];
                if (map.InBounds(nx, ny) && seen[ny * map.width + nx]) return true;
            }
            return false;
        }

        static void CheckAmbushes(MapData map, int radius, MapReport report)
        {
            var p = report.problems;
            for (int y = 0; y < map.height; y++)
                for (int x = 0; x < map.width; x++)
                {
                    var k = map.KindAt(x, y);
                    if (k == CellKind.Trigger)
                    {
                        if (!AnyWithin(map, x, y, radius, CellKind.ClosetWall) && !AnyEnemyWithin(map, x, y, radius))
                            p.Warn($"The ambush trigger at {MapData.Where(x, y)} has no closet wall or enemy within {radius} cells.");
                    }
                    else if (k == CellKind.ClosetWall)
                    {
                        if (!AnyWithin(map, x, y, radius, CellKind.Trigger))
                            p.Warn($"The closet wall at {MapData.Where(x, y)} has no ambush trigger ('!') within {radius} cells, so it never opens.");
                    }
                }
        }

        static bool AnyWithin(MapData map, int cx, int cy, int r, CellKind kind)
        {
            for (int y = cy - r; y <= cy + r; y++)
                for (int x = cx - r; x <= cx + r; x++)
                    if (map.KindAt(x, y) == kind) return true;
            return false;
        }

        static bool AnyEnemyWithin(MapData map, int cx, int cy, int r)
        {
            for (int i = 0; i < map.things.Count; i++)
            {
                var t = map.things[i];
                if (t.entry.kind == CellKind.Entity && t.entry.category == EntityCategory.Enemy
                    && System.Math.Abs(t.x - cx) <= r && System.Math.Abs(t.y - cy) <= r) return true;
            }
            return false;
        }

        static string KeyList(List<KeyColor> keys)
        {
            var names = new List<string>();
            foreach (var k in keys) names.Add(MapLegend.KeyName((int)k));
            return string.Join(", ", names);
        }
    }
}
