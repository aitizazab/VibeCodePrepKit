using System;
using System.Collections.Generic;
using System.Numerics;
using JamKit.Movement;

namespace JamKit.Skyline
{
    /// <summary>
    /// A walkable graph over the arena's tops (docs/SKYLINE-PLAN-FLOOR1.md, task F3), built from the same
    /// <see cref="FloorSolid"/> list the Unity builder and the headless sim read. One node per walkable surface at every cell
    /// centre of a square grid (centres at multiples of <see cref="Cell"/>, so a 2 m grid has its nodes on even coordinates);
    /// a node exists where a standing Unit (radius <see cref="Radius"/>, <see cref="StandHeight"/> tall) fits: nothing but the
    /// surface itself reaches into the column between <see cref="StepHeight"/> and <see cref="StandHeight"/> above its top.
    /// Edges join the 8 neighbours: a step up or down of at most <see cref="StepHeight"/>, or, where either end is on a ramp or
    /// an escalator, a slope of at most 45 degrees; every edge is checked for support and clearance along its length (so a wall
    /// in a doorway or a corner cut through a pillar breaks it). The slide chute is one-way (down only). Pure and deterministic.
    /// <para>Walkable surfaces are the roles <c>floor deck ramp escalator</c>. The low props (tables, planters, the kiosk) are
    /// obstacles here, not surfaces: nobody walks across a table.</para>
    /// </summary>
    public sealed class ArenaNav
    {
        /// <summary>The Unit's footprint radius used for clearance (the movement body is 0.38; a cell is coarser than that).</summary>
        public const float Radius = 0.3f;
        /// <summary>The tallest step that is walked up or down without a ramp.</summary>
        public const float StepHeight = 0.35f;
        /// <summary>Clear height over a node.</summary>
        public const float StandHeight = 1.8f;
        /// <summary>The steepest ramp edge, rise over run (45 degrees).</summary>
        public const float MaxSlope = 1f;
        /// <summary>Eye height above the feet used for the lint's sight rays.</summary>
        public const float EyeHeight = 1.6f;
        /// <summary>The highest ledge a standing Unit climbs with a jump and a mantle (jump 1.25 m, mantle up to 1.9 m, with a margin).</summary>
        public const float MaxClimb = 3f;
        /// <summary>The id prefix of the one-way slide chute.</summary>
        public const string ChutePrefix = "chute";
        /// <summary>The tallest low prop (a pit table, a planter) a standing Unit hops over, for the lint's exits rule (decision D23).</summary>
        public const float HopHeight = 1.1f;

        const float Bucket = 4f;

        public static bool IsSurfaceRole(string role) =>
            role == "floor" || role == "deck" || role == "ramp" || role == "escalator";

        readonly FloorSolid[] _solids;
        readonly float _cell, _hop;
        readonly int _kx0, _kz0, _nx, _nz;
        readonly List<int>[] _cellNodes;           // per grid cell: node indices
        readonly Vector3[] _pos;
        readonly bool[] _ramp, _chute;
        readonly int[][] _adj;
        readonly int[] _component;
        readonly int _largest;
        readonly HashSet<int> _climbable = new HashSet<int>();
        // obstacle index
        readonly Dictionary<long, List<int>> _bucket = new Dictionary<long, List<int>>();
        readonly List<int> _surface = new List<int>();

        /// <summary>The grid spacing in metres.</summary>
        public float Cell => _cell;
        public int Count => _pos.Length;
        /// <summary>The size of the largest connected piece (edges counted both ways).</summary>
        public int LargestComponentSize => _largest;
        /// <summary>Nodes outside the largest connected piece: islands.</summary>
        public int IslandNodes => _pos.Length - _largest;

        public Vector3 PositionOf(int node) => _pos[node];
        public bool IsRamp(int node) => _ramp[node];
        public bool IsChute(int node) => _chute[node];
        /// <summary>The connected piece (0, 1, ...) the node is in; piece 0 is the largest.</summary>
        public int ComponentOf(int node) => _component[node];
        /// <summary>The nodes you can step to from <paramref name="node"/> (outgoing edges: the chute is down only).</summary>
        public IReadOnlyList<int> Neighbours(int node) => _adj[node];

        /// <summary>Builds the graph. <paramref name="cell"/> is the grid spacing (2 m by default).</summary>
        public static ArenaNav Build(IReadOnlyList<FloorSolid> solids, float cell = 2f) => new ArenaNav(solids, cell, 0f);

        /// <summary>Builds the graph where the low props (roles table, planter and kiosk) no taller than <paramref name="hopHeight"/> do
        /// not block: the Unit hops them. 0 makes every prop an obstacle, as <see cref="Build(IReadOnlyList{FloorSolid}, float)"/> does.</summary>
        public static ArenaNav Build(IReadOnlyList<FloorSolid> solids, float cell, float hopHeight) => new ArenaNav(solids, cell, hopHeight);

        static bool IsLowProp(FloorSolid s) => s.role == "table" || s.role == "planter" || s.role == "kiosk";

        ArenaNav(IReadOnlyList<FloorSolid> solids, float cell, float hopHeight)
        {
            if (solids == null) throw new ArgumentNullException(nameof(solids));
            if (!(cell > 0.1f)) throw new ArgumentOutOfRangeException(nameof(cell));
            _cell = cell; _hop = hopHeight;
            _solids = new FloorSolid[solids.Count];
            for (int i = 0; i < _solids.Length; i++) _solids[i] = solids[i];

            if (_solids.Length == 0)
            {
                _pos = new Vector3[0]; _ramp = new bool[0]; _chute = new bool[0]; _adj = new int[0][]; _component = new int[0];
                _cellNodes = new List<int>[0];
                return;
            }

            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            for (int i = 0; i < _solids.Length; i++)
            {
                var b = _solids[i].box;
                minX = Math.Min(minX, b.min.X); maxX = Math.Max(maxX, b.max.X);
                minZ = Math.Min(minZ, b.min.Z); maxZ = Math.Max(maxZ, b.max.Z);
                if (IsSurfaceRole(_solids[i].role)) _surface.Add(i);
                AddToBuckets(i);
            }
            _kx0 = (int)Math.Ceiling(minX / cell - 1e-4); _kz0 = (int)Math.Ceiling(minZ / cell - 1e-4);
            int kx1 = (int)Math.Floor(maxX / cell + 1e-4), kz1 = (int)Math.Floor(maxZ / cell + 1e-4);
            _nx = Math.Max(0, kx1 - _kx0 + 1); _nz = Math.Max(0, kz1 - _kz0 + 1);
            _cellNodes = new List<int>[_nx * _nz];

            var pos = new List<Vector3>(); var ramp = new List<bool>(); var chute = new List<bool>();
            var tops = new List<float>(); var topRamp = new List<bool>(); var topChute = new List<bool>();
            for (int ix = 0; ix < _nx; ix++)
                for (int iz = 0; iz < _nz; iz++)
                {
                    float x = (_kx0 + ix) * cell, z = (_kz0 + iz) * cell;
                    CollectTops(x, z, tops, topRamp, topChute);
                    for (int k = 0; k < tops.Count; k++)
                    {
                        if (Blocked(x, z, tops[k])) continue;
                        int id = pos.Count;
                        pos.Add(new Vector3(x, tops[k], z)); ramp.Add(topRamp[k]); chute.Add(topChute[k]);
                        (_cellNodes[ix * _nz + iz] ?? (_cellNodes[ix * _nz + iz] = new List<int>())).Add(id);
                    }
                }
            _pos = pos.ToArray(); _ramp = ramp.ToArray(); _chute = chute.ToArray();

            _adj = new int[_pos.Length][];
            var tmp = new List<int>(8);
            for (int a = 0; a < _pos.Length; a++)
            {
                tmp.Clear();
                int ax = (int)Math.Round(_pos[a].X / cell) - _kx0, az = (int)Math.Round(_pos[a].Z / cell) - _kz0;
                for (int dx = -1; dx <= 1; dx++)
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        if (dx == 0 && dz == 0) continue;
                        int bx = ax + dx, bz = az + dz;
                        if (bx < 0 || bz < 0 || bx >= _nx || bz >= _nz) continue;
                        var list = _cellNodes[bx * _nz + bz];
                        if (list == null) continue;
                        for (int j = 0; j < list.Count; j++)
                            if (EdgeOk(a, list[j])) tmp.Add(list[j]);
                    }
                _adj[a] = tmp.ToArray();
            }

            _component = new int[_pos.Length];
            _largest = LabelComponents(_component);
            for (int i = 0; i < _pos.Length; i++)
                if (_component[i] != 0 && !_climbable.Contains(_component[i]) && NearMainBelow(i)) _climbable.Add(_component[i]);
        }

        // ------------------------------------------------------------------ the nodes

        void CollectTops(float x, float z, List<float> tops, List<bool> ramp, List<bool> chute)
        {
            tops.Clear(); ramp.Clear(); chute.Clear();
            for (int s = 0; s < _surface.Count; s++)
            {
                var sol = _solids[_surface[s]];
                var b = sol.box;
                if (x < b.min.X || x > b.max.X || z < b.min.Z || z > b.max.Z) continue;
                float top = sol.TopAt(x, z);
                bool isRamp = sol.ramp, isChute = sol.ramp && sol.id.StartsWith(ChutePrefix, StringComparison.Ordinal);
                int found = -1;
                for (int k = 0; k < tops.Count; k++) if (Math.Abs(tops[k] - top) < 0.05f) { found = k; break; }
                if (found < 0) { tops.Add(top); ramp.Add(isRamp); chute.Add(isChute); }
                else
                {
                    ramp[found] = ramp[found] || isRamp;
                    chute[found] = chute[found] && isChute;      // a flat floor at the same height makes it two-way
                }
            }
        }

        void AddToBuckets(int index)
        {
            var b = _solids[index].box;
            int x0 = BucketOf(b.min.X - 1f), x1 = BucketOf(b.max.X + 1f), z0 = BucketOf(b.min.Z - 1f), z1 = BucketOf(b.max.Z + 1f);
            for (int bx = x0; bx <= x1; bx++)
                for (int bz = z0; bz <= z1; bz++)
                {
                    long key = BucketKey(bx, bz);
                    if (!_bucket.TryGetValue(key, out var l)) _bucket[key] = l = new List<int>();
                    l.Add(index);
                }
        }

        static int BucketOf(float v) => (int)Math.Floor(v / Bucket);
        static long BucketKey(int bx, int bz) => ((long)bx << 32) ^ (uint)bz;

        /// <summary>True when something other than the surface itself reaches into the standing column over (x, z) at foot
        /// height <paramref name="y"/> (a footprint of <see cref="Radius"/>).</summary>
        bool Blocked(float x, float z, float y)
        {
            if (!_bucket.TryGetValue(BucketKey(BucketOf(x), BucketOf(z)), out var list)) return false;
            float lo = y + StepHeight, hi = y + StandHeight;
            for (int k = 0; k < list.Count; k++)
            {
                var s = _solids[list[k]];
                var b = s.box;
                if (_hop > 0f && IsLowProp(s) && b.max.Y - b.min.Y <= _hop + 1e-4f) continue;
                if (b.min.X >= x + Radius || b.max.X <= x - Radius || b.min.Z >= z + Radius || b.max.Z <= z - Radius) continue;
                float top = b.max.Y;
                if (s.ramp)
                {
                    // the ramp's highest point under the footprint
                    float a0 = s.rampAlongX ? x - Radius : z - Radius, a1 = s.rampAlongX ? x + Radius : z + Radius;
                    float h0 = s.TopAt(s.rampAlongX ? a0 : x, s.rampAlongX ? z : a0), h1 = s.TopAt(s.rampAlongX ? a1 : x, s.rampAlongX ? z : a1);
                    top = Math.Max(h0, h1);
                }
                if (top > lo && b.min.Y < hi) return true;
            }
            return false;
        }

        /// <summary>True when a walkable surface under (x, z) is within <paramref name="tolerance"/> of height y.</summary>
        bool Supported(float x, float z, float y, float tolerance)
        {
            for (int s = 0; s < _surface.Count; s++)
            {
                var sol = _solids[_surface[s]];
                var b = sol.box;
                if (x < b.min.X - 1e-4f || x > b.max.X + 1e-4f || z < b.min.Z - 1e-4f || z > b.max.Z + 1e-4f) continue;
                if (Math.Abs(sol.TopAt(x, z) - y) <= tolerance) return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ the edges

        bool EdgeOk(int a, int b)
        {
            Vector3 pa = _pos[a], pb = _pos[b];
            float dy = pb.Y - pa.Y;
            float run = MathF.Sqrt((pb.X - pa.X) * (pb.X - pa.X) + (pb.Z - pa.Z) * (pb.Z - pa.Z));
            bool rampEnd = _ramp[a] || _ramp[b];
            if (MathF.Abs(dy) > StepHeight)
            {
                if (!rampEnd || MathF.Abs(dy) / run > MaxSlope + 1e-4f) return false;
            }
            // the slide chute is one-way: never up it, never along its level from the foot
            if ((_chute[a] || _chute[b]) && dy > 0.05f) return false;
            float tolerance = StepHeight + (rampEnd ? 0.3f : 0f);
            int n = Math.Max(2, (int)Math.Ceiling(run / 0.5f));
            for (int i = 1; i < n; i++)
            {
                float t = (float)i / n;
                float x = pa.X + (pb.X - pa.X) * t, z = pa.Z + (pb.Z - pa.Z) * t, y = pa.Y + dy * t;
                if (Blocked(x, z, y) || !Supported(x, z, y, tolerance)) return false;
            }
            return true;
        }

        int LabelComponents(int[] label)
        {
            int n = _pos.Length;
            var parent = new int[n];
            for (int i = 0; i < n; i++) parent[i] = i;
            int Find(int v) { while (parent[v] != v) { parent[v] = parent[parent[v]]; v = parent[v]; } return v; }
            for (int a = 0; a < n; a++)
                foreach (int b in _adj[a]) { int ra = Find(a), rb = Find(b); if (ra != rb) parent[ra] = rb; }
            var size = new Dictionary<int, int>();
            for (int i = 0; i < n; i++) { int r = Find(i); size[r] = size.TryGetValue(r, out int c) ? c + 1 : 1; }
            // number the roots by size, largest first
            var roots = new List<int>(size.Keys);
            roots.Sort((p, q) => size[q] != size[p] ? size[q].CompareTo(size[p]) : p.CompareTo(q));
            var id = new Dictionary<int, int>();
            for (int i = 0; i < roots.Count; i++) id[roots[i]] = i;
            for (int i = 0; i < n; i++) label[i] = id[Find(i)];
            return roots.Count == 0 ? 0 : size[roots[0]];
        }

        // ------------------------------------------------------------------ queries

        /// <summary>True when a node outside the main piece belongs to a piece the base kit climbs onto: some node of that piece has a
        /// node of the main piece within 3 m (flat) and between 0.35 m and <see cref="MaxClimb"/> below it (a jump and a mantle
        /// from the floor). A node in the main piece is never "climbable" (false).</summary>
        public bool IsClimbable(int node) => _component[node] != 0 && _climbable.Contains(_component[node]);

        bool NearMainBelow(int node)
        {
            Vector3 p = _pos[node];
            int cx = (int)Math.Round(p.X / _cell) - _kx0, cz = (int)Math.Round(p.Z / _cell) - _kz0;
            int r = (int)Math.Ceiling(3f / _cell);
            for (int dx = -r; dx <= r; dx++)
                for (int dz = -r; dz <= r; dz++)
                {
                    int ix = cx + dx, iz = cz + dz;
                    if (ix < 0 || iz < 0 || ix >= _nx || iz >= _nz) continue;
                    var list = _cellNodes[ix * _nz + iz];
                    if (list == null) continue;
                    foreach (int m in list)
                    {
                        if (_component[m] != 0) continue;
                        float ddx = _pos[m].X - p.X, ddz = _pos[m].Z - p.Z, dy = p.Y - _pos[m].Y;
                        if (ddx * ddx + ddz * ddz <= 9f && dy > StepHeight && dy <= MaxClimb) return true;
                    }
                }
            return false;
        }

        /// <summary>The node nearest <paramref name="p"/> (height weighs three times a flat metre, so the right storey wins),
        /// searching a few cells round it; -1 when none is within about 3 cells.</summary>
        public int NodeAt(Vector3 p)
        {
            int cx = (int)Math.Round(p.X / _cell) - _kx0, cz = (int)Math.Round(p.Z / _cell) - _kz0;
            int best = -1;
            float bestD = float.MaxValue;
            for (int r = 0; r <= 3; r++)
            {
                for (int dx = -r; dx <= r; dx++)
                    for (int dz = -r; dz <= r; dz++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dz)) != r) continue;
                        int ix = cx + dx, iz = cz + dz;
                        if (ix < 0 || iz < 0 || ix >= _nx || iz >= _nz) continue;
                        var list = _cellNodes[ix * _nz + iz];
                        if (list == null) continue;
                        for (int j = 0; j < list.Count; j++)
                        {
                            Vector3 q = _pos[list[j]];
                            float ddx = q.X - p.X, ddz = q.Z - p.Z, ddy = (q.Y - p.Y) * 3f;
                            float d = ddx * ddx + ddz * ddz + ddy * ddy;
                            if (d < bestD) { bestD = d; best = list[j]; }
                        }
                    }
                if (best >= 0 && r >= 1) break;      // one ring beyond the first hit is enough
            }
            return best;
        }

        /// <summary>The shortest walk (A*, 3D edge lengths) from one node to another, both included; null when unreachable.</summary>
        public List<int> Path(int from, int to)
        {
            int n = _pos.Length;
            if (from < 0 || to < 0 || from >= n || to >= n) return null;
            if (from == to) return new List<int> { from };
            var g = new float[n];
            var prev = new int[n];
            var closed = new bool[n];
            for (int i = 0; i < n; i++) { g[i] = float.MaxValue; prev[i] = -1; }
            var heap = new Heap();
            g[from] = 0f;
            heap.Push(Vector3.Distance(_pos[from], _pos[to]), from);
            while (heap.Count > 0)
            {
                int u = heap.Pop();
                if (closed[u]) continue;
                closed[u] = true;
                if (u == to) break;
                foreach (int v in _adj[u])
                {
                    if (closed[v]) continue;
                    float ng = g[u] + Vector3.Distance(_pos[u], _pos[v]);
                    if (ng >= g[v]) continue;
                    g[v] = ng; prev[v] = u;
                    heap.Push(ng + Vector3.Distance(_pos[v], _pos[to]), v);
                }
            }
            if (!closed[to]) return null;
            var path = new List<int>();
            for (int c = to; c >= 0; c = prev[c]) path.Add(c);
            path.Reverse();
            return path;
        }

        /// <summary>True when a walk leads from one node to the other.</summary>
        public bool Reachable(int from, int to) => Path(from, to) != null;

        /// <summary>The path as waypoints (feet positions), keeping only the nodes where the direction or the slope changes.</summary>
        public List<Vector3> Simplify(IReadOnlyList<int> path)
        {
            var pts = new List<Vector3>();
            if (path == null || path.Count == 0) return pts;
            pts.Add(_pos[path[0]]);
            for (int i = 1; i < path.Count - 1; i++)
            {
                Vector3 d0 = Vector3.Normalize(_pos[path[i]] - _pos[path[i - 1]]), d1 = Vector3.Normalize(_pos[path[i + 1]] - _pos[path[i]]);
                if (Vector3.Dot(d0, d1) < 0.999f) pts.Add(_pos[path[i]]);
            }
            if (path.Count > 1) pts.Add(_pos[path[path.Count - 1]]);
            return pts;
        }

        // ------------------------------------------------------------------ a small binary heap (no PriorityQueue in Unity's runtime)

        sealed class Heap
        {
            float[] _key = new float[64];
            int[] _val = new int[64];
            public int Count { get; private set; }

            public void Push(float key, int value)
            {
                if (Count == _key.Length) { Array.Resize(ref _key, Count * 2); Array.Resize(ref _val, Count * 2); }
                int i = Count++;
                while (i > 0)
                {
                    int p = (i - 1) / 2;
                    if (_key[p] <= key) break;
                    _key[i] = _key[p]; _val[i] = _val[p];
                    i = p;
                }
                _key[i] = key; _val[i] = value;
            }

            public int Pop()
            {
                int top = _val[0];
                Count--;
                if (Count > 0)
                {
                    float key = _key[Count]; int val = _val[Count];
                    int i = 0;
                    while (true)
                    {
                        int c = 2 * i + 1;
                        if (c >= Count) break;
                        if (c + 1 < Count && _key[c + 1] < _key[c]) c++;
                        if (_key[c] >= key) break;
                        _key[i] = _key[c]; _val[i] = _val[c];
                        i = c;
                    }
                    _key[i] = key; _val[i] = val;
                }
                return top;
            }
        }
    }

    /// <summary>
    /// Walks a list of waypoints (feet positions) with the real movement engine's inputs, in the ScriptedRouteBot pattern of
    /// <see cref="EncounterDodge"/> but in 3D and with the base-kit hints of <see cref="RouteDef"/>: <b>Walk</b> steers and
    /// sprints at the waypoint; <b>Slide</b> holds crouch on the way to it and presses it once when fast enough (the chute);
    /// <b>Mantle</b> and <b>DoubleJump</b> jump when close to the waypoint and still below it (a double jump at the apex);
    /// <b>WallRun</b> steers along the leg with the stick turned a little toward the wall side and jumps once at the start of the
    /// leg (the sequence of the warm-up ledge; the exact timing of a wall kick is the pilot's job, task DM1). A waypoint counts as
    /// reached within <see cref="ArriveRadius"/> metres flat and a height tolerance. Pure.
    /// </summary>
    public sealed class RouteWalker : IPlayerBot
    {
        readonly Vector3[] _wp;
        readonly MoveHintKind[] _hint;
        bool _slidThisLeg, _jumpedThisLeg, _doubledThisLeg;
        int _legOfFlags = -1;

        /// <summary>Metres (flat) from a Mantle or DoubleJump waypoint at which the jump is made.</summary>
        public float JumpRange = 4f;
        /// <summary>Metres (flat) within which a waypoint counts as reached.</summary>
        public float ArriveRadius = 1.0f;
        /// <summary>Hold sprint while walking.</summary>
        public bool Sprint = true;
        /// <summary>The waypoint it is heading for.</summary>
        public int TargetIndex { get; private set; }
        /// <summary>The last waypoint has been reached.</summary>
        public bool Done { get; private set; }
        /// <summary>The way the Unit last faced (kept when done).</summary>
        public float LastYaw { get; private set; }

        public RouteWalker(IReadOnlyList<Vector3> waypoints, IReadOnlyList<MoveHint> hints = null)
        {
            if (waypoints == null || waypoints.Count == 0) throw new ArgumentException("a route needs at least one waypoint");
            _wp = new Vector3[waypoints.Count];
            for (int i = 0; i < _wp.Length; i++) _wp[i] = waypoints[i];
            _hint = new MoveHintKind[_wp.Length];
            if (hints != null)
                for (int i = 0; i < hints.Count; i++)
                    if (hints[i].atWaypoint >= 0 && hints[i].atWaypoint < _hint.Length) _hint[hints[i].atWaypoint] = hints[i].kind;
        }

        public RouteWalker(RouteDef route) : this(route.waypoints, route.hints) { }

        public MoveInput Next(MoveSim sim, float time, float h)
        {
            Vector3 pos = sim.Position;
            while (!Done)
            {
                Vector3 t = _wp[TargetIndex];
                float dx = t.X - pos.X, dz = t.Z - pos.Z;
                bool plain = _hint[TargetIndex] == MoveHintKind.Walk || _hint[TargetIndex] == MoveHintKind.Slide;
                float tolY = plain ? 1.2f : 2.5f;
                if (dx * dx + dz * dz <= ArriveRadius * ArriveRadius && Math.Abs(t.Y - pos.Y) <= tolY)
                {
                    if (++TargetIndex >= _wp.Length) { Done = true; TargetIndex = _wp.Length - 1; }
                }
                else break;
            }
            if (Done) return new MoveInput { yaw = LastYaw };

            Vector3 target = _wp[TargetIndex];
            float ddx = target.X - pos.X, ddz = target.Z - pos.Z;
            float flat = MathF.Sqrt(ddx * ddx + ddz * ddz);
            if (flat > 1e-3f) LastYaw = MathF.Atan2(ddx, ddz) * (180f / MathF.PI);
            if (_legOfFlags != TargetIndex) { _legOfFlags = TargetIndex; _slidThisLeg = _jumpedThisLeg = _doubledThisLeg = false; }

            var input = new MoveInput { move = new Vector2(0f, 1f), yaw = LastYaw, sprintHeld = Sprint };
            switch (_hint[TargetIndex])
            {
                case MoveHintKind.Slide:
                    input.crouchHeld = true;
                    if (!_slidThisLeg && sim.Mode != MoveMode.Slide && sim.HorizontalSpeed >= sim.def.slideMinSpeed) { input.crouchPressed = true; _slidThisLeg = true; }
                    break;
                case MoveHintKind.Mantle:
                case MoveHintKind.DoubleJump:
                    if (!_jumpedThisLeg && sim.Grounded && flat < JumpRange && target.Y - pos.Y > 0.3f)
                    {
                        input.jumpPressed = input.jumpHeld = true; _jumpedThisLeg = true;
                    }
                    else if (_hint[TargetIndex] == MoveHintKind.DoubleJump && _jumpedThisLeg && !_doubledThisLeg && !sim.Grounded
                             && sim.Mode == MoveMode.Air && sim.Velocity.Y <= 0f)
                    {
                        input.jumpPressed = input.jumpHeld = true; _doubledThisLeg = true;
                    }
                    break;
                case MoveHintKind.WallRun:
                    input.move = new Vector2(0.2f, 1f);
                    if (!_jumpedThisLeg && sim.Grounded) { input.jumpPressed = input.jumpHeld = true; _jumpedThisLeg = true; }
                    break;
            }
            return input;
        }
    }
}
