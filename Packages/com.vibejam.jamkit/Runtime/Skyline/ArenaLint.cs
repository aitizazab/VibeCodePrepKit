using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace JamKit.Skyline
{
    /// <summary>What <see cref="ArenaLint.Run(IReadOnlyList{FloorSolid}, IReadOnlyList{SpawnDoor}, IReadOnlyList{SocketDef})"/> found.
    /// <see cref="problems"/> is empty when the layout passes; everything else is the measurement behind it.</summary>
    public sealed class LintResult
    {
        /// <summary>One line per failing cluster of points (or per failing socket, door or solid).</summary>
        public List<string> problems = new List<string>();
        /// <summary>Things to know that are not failures (for example a spawn door under a tow circle on another level).</summary>
        public List<string> notes = new List<string>();
        /// <summary>The standable nodes the rules were run on (exempt zones left out), and how many of those failed each rule.</summary>
        public int standable, failedExits, failedSight, failedCircles, failedDoors;
        /// <summary>Nodes left out of rules 1 and 2: those in an exempt zone (the arrival cab, the exit lift) and those outside the
        /// main walkable piece (islands and ledges only a jump and a mantle reach; the island rule reports them).</summary>
        public int exempt;
        /// <summary>Nodes outside the largest connected piece of the walkable graph.</summary>
        public int islandNodes;
        /// <summary>The largest share of the spawn doors any standable point sees, and where.</summary>
        public float worstSeenShare;
        public Vector3 worstSeenAt;
        /// <summary>Per socket (in the order given): the circle's exposure (the best score two sectors 90 degrees or more apart
        /// both reach, 0..1; passes at 0.5) and how many of its 8 sectors expose it at all.</summary>
        public float[] circleExposure = new float[0];
        public int[] circleSectors = new int[0];
        /// <summary>The fewest exits any non-exempt point has (capped at 2).</summary>
        public int minExits;
        /// <summary>Wall-clock seconds the lint took.</summary>
        public double seconds;
    }

    /// <summary>The lint's knobs. The defaults are the plan's numbers; a test or the main session can turn one to see what a
    /// rule would say under another reading.</summary>
    public sealed class LintOptions
    {
        /// <summary>Rule 3: viewers stand this far (flat metres) from the circle's centre, at least and at most.</summary>
        public float viewerMin = 8f, viewerMax = 30f;
        /// <summary>Rule 2: whether the skylight and roof vents count among the doors a point can see. Off by design (decision D23): a
        /// roof vent is overhead and seen from everywhere, and its drones fly to engage anyway; the sim camper/holder survey is the
        /// real "no safe corner" gate.</summary>
        public bool ventsCount = false;
        /// <summary>Rule 1: low props (tables, planters) up to this tall count as passable for the exits rule (the Unit hops them).</summary>
        public float hopHeight = ArenaNav.HopHeight;
        /// <summary>The navigation grid's spacing (the plan's default is 2 m; a finer grid finds fewer false pockets between props).</summary>
        public float cell = 2f;
    }

    /// <summary>
    /// The layout lint (docs/SKYLINE-PLAN-FLOOR1.md, task F3, and the four measured rules of docs/SKYLINE-FLOOR-REPOSSESS.md
    /// section 4), run over the arena's data. Rules 1 to 3 here; rule 4 (finishable with the base kit) is BaseKitRouteTests.
    /// <list type="number">
    /// <item><b>Exits:</b> from every standable node, with the node itself removed, at least two vertex-disjoint walks lead to
    /// nodes 10 m (3D) or more away. Props at or under <see cref="ArenaNav.HopHeight"/> (1.1 m: pit tables, planters) do not
    /// block those walks (you hop them).</item>
    /// <item><b>Seen share:</b> from eye height (feet + 1.6 m) to each ground-level spawn door's mouth (shop shutters, service
    /// hatches, rail ends; the roof vents are excluded by design), no standable node sees more than 70 percent of them (an
    /// unblocked line within 60 m; glass does not block sight).</item>
    /// <item><b>Circle exposure:</b> round each socket's circle, 8 sectors of 45 degrees; a sector exposes the circle when a
    /// standable node 8 to 30 m (flat) from the centre inside it sees at least 50 percent of 25 sample points on the disc; the
    /// circle passes when two exposing sectors are at least 90 degrees apart.</item>
    /// </list>
    /// Extra checks that read the same data: no island of walkable nodes (more than 4 nodes outside the largest piece); every
    /// circle has standable ground inside it, in the largest piece; every spawn door's body fits (vents and rail ends in open
    /// air, ground doors in front of their shutter or hatch, with a walkable node at the mouth); the surfaces the spec says are
    /// runnable (façade panes, the pit's rim) are. A spawn door under a tow circle on another level is a note.
    /// <para>Operationalisation choices, all deliberate: the arrival cab's doors and the exit lift's shutter are treated as
    /// open (the floor opens them); a door's sight target is its mouth, 1.5 m up for shutters and hatches and 0.35 m out of
    /// its face (vents and rail ends: the door's own position); the circle's sample height is 1 m above the ground the
    /// sample stands on (nearest node within the circle's 2.5 m height band; the socket's own height when none). The
    /// arrival cab and the exit lift are exempt from rules 1 and 2 when run through <see cref="Arena"/> (a one-door closet by
    /// design) and are listed as <see cref="LintResult.exempt"/>.</para>
    /// </summary>
    public static class ArenaLint
    {
        /// <summary>Zones left out of rules 1 and 2 on the real arena.</summary>
        public static readonly string[] ExemptZoneIds = { "cab", "exit-lift" };

        const int SectorCount = 8;
        const int CircleSamples = 24;
        const float DoorMouthUp = 1.5f, DoorMouthOut = 0.35f;

        // ------------------------------------------------------------------ entry points

        /// <summary>Lints a level given as solids, doors and sockets. No zone is exempt.</summary>
        public static LintResult Run(IReadOnlyList<FloorSolid> solids, IReadOnlyList<SpawnDoor> doors, IReadOnlyList<SocketDef> sockets) =>
            Run(solids, doors, sockets, null);

        /// <summary>Lints a level, leaving the nodes inside <paramref name="exempt"/> zones out of rules 1 and 2.</summary>
        public static LintResult Run(IReadOnlyList<FloorSolid> solids, IReadOnlyList<SpawnDoor> doors, IReadOnlyList<SocketDef> sockets,
            IReadOnlyList<FloorZone> exempt) =>
            RunCore(solids, doors, sockets, exempt, null, new LintOptions());

        /// <summary>The real arena: <see cref="RepossessArena.Solids"/>, <see cref="RepossessSites.Doors"/> and
        /// <see cref="RepossessSites.Sockets"/>, with the cab and the exit lift exempt and the zones named in the messages.</summary>
        public static LintResult Arena() => Arena(RepossessArena.Solids(), RepossessSites.Doors(), new LintOptions());

        /// <summary>The real arena's solids with other doors (to try a nudge without editing the data).</summary>
        public static LintResult Arena(IReadOnlyList<FloorSolid> solids, IReadOnlyList<SpawnDoor> doors) => Arena(solids, doors, new LintOptions());

        /// <summary>The real arena with other solids or doors and other options.</summary>
        public static LintResult Arena(IReadOnlyList<FloorSolid> solids, IReadOnlyList<SpawnDoor> doors, LintOptions options)
        {
            var zones = RepossessArena.Zones();
            var ex = new List<FloorZone>();
            foreach (var z in zones) if (Array.IndexOf(ExemptZoneIds, z.id) >= 0) ex.Add(z);
            return RunCore(solids, doors, RepossessSites.Sockets(), ex, zones, options ?? new LintOptions());
        }

        // ------------------------------------------------------------------ the solids as the lint sees them

        /// <summary>The solids with the floor's closed leaves open: the arrival cab's doors and the exit lift's shutter.</summary>
        public static List<FloorSolid> OpenState(IReadOnlyList<FloorSolid> solids)
        {
            var l = new List<FloorSolid>();
            foreach (var s in solids) if (s.id != RepossessArena.LiftShutterId && s.id != RepossessArena.CabDoorsId) l.Add(s);
            return l;
        }

        /// <summary>What stops sight: every solid but glass and the ground slabs; a ramp as stepped boxes under its lowest
        /// edge per 2 m slice (as <see cref="RepossessArena.SightBlockers"/> does, for any list of solids).</summary>
        public static List<CoverBox> BlockersOf(IReadOnlyList<FloorSolid> solids)
        {
            var l = new List<CoverBox>();
            foreach (var s in solids)
            {
                if (s.glass || s.role == "floor") continue;
                if (s.ramp) AddRampSlices(s, l); else l.Add(s.box);
            }
            return l;
        }

        static void AddRampSlices(FloorSolid s, List<CoverBox> l)
        {
            var b = s.box;
            float lo = s.rampAlongX ? b.min.X : b.min.Z, hi = s.rampAlongX ? b.max.X : b.max.Z;
            float len = hi - lo;
            if (len <= 1e-4f) return;
            int n = Math.Max(1, (int)Math.Ceiling(len / 2f));
            for (int i = 0; i < n; i++)
            {
                float a = lo + len * i / n, c = lo + len * (i + 1) / n;
                float ta = s.rampTopStart + (s.rampTopEnd - s.rampTopStart) * (a - lo) / len;
                float tc = s.rampTopStart + (s.rampTopEnd - s.rampTopStart) * (c - lo) / len;
                float top = Math.Min(ta, tc);
                if (top - b.min.Y < 0.05f) continue;
                var min = b.min; var max = b.max;
                if (s.rampAlongX) { min.X = a; max.X = c; } else { min.Z = a; max.Z = c; }
                max.Y = top;
                l.Add(new CoverBox { min = min, max = max });
            }
        }

        // ------------------------------------------------------------------ rule 1: exits

        /// <summary>The number of vertex-disjoint walks (0, 1 or 2: capped at 2) from <paramref name="node"/> to nodes at
        /// least <paramref name="radius"/> metres away (3D), the node itself removed. A walk ends at the first node it reaches
        /// outside the radius, and each such end node is used by one walk only.</summary>
        public static int Exits(ArenaNav nav, int node, float radius)
        {
            if (nav == null) throw new ArgumentNullException(nameof(nav));
            if (node < 0 || node >= nav.Count) return 0;
            Vector3 c = nav.PositionOf(node);
            var local = new Dictionary<int, int>();           // global node -> local index
            var verts = new List<int>();
            var inside = new List<bool>();
            var queue = new Queue<int>();
            int Local(int g)
            {
                if (local.TryGetValue(g, out int li)) return li;
                li = verts.Count;
                local[g] = li; verts.Add(g);
                bool ins = Vector3.Distance(nav.PositionOf(g), c) < radius;
                inside.Add(ins);
                if (ins) queue.Enqueue(li);
                return li;
            }
            int src = Local(node);
            inside[src] = true;
            // expand the inside nodes
            var edgesFrom = new List<int[]>();                 // by local index, filled as expanded
            while (edgesFrom.Count < 4096 && queue.Count > 0)
            {
                int u = queue.Dequeue();
                while (edgesFrom.Count <= u) edgesFrom.Add(null);
                var nb = nav.Neighbours(verts[u]);
                var arr = new int[nb.Count];
                for (int k = 0; k < nb.Count; k++) arr[k] = Local(nb[k]);
                edgesFrom[u] = arr;
            }
            int n = verts.Count;
            // residual graph: vertex v has in = 2v and out = 2v + 1; the sink is 2n
            int sink = 2 * n, nodes = 2 * n + 1;
            var head = new int[nodes];
            for (int i = 0; i < nodes; i++) head[i] = -1;
            var to = new List<int>(); var cap = new List<int>(); var next = new List<int>();
            void AddEdge(int a, int b)
            {
                to.Add(b); cap.Add(1); next.Add(head[a]); head[a] = to.Count - 1;
                to.Add(a); cap.Add(0); next.Add(head[b]); head[b] = to.Count - 1;
            }
            for (int v = 0; v < n; v++)
            {
                if (v == src) { }
                else if (inside[v]) AddEdge(2 * v, 2 * v + 1);
                else AddEdge(2 * v, sink);
                if (inside[v] && v < edgesFrom.Count && edgesFrom[v] != null)
                    foreach (int w in edgesFrom[v])
                        if (w != src) AddEdge(2 * v + 1, 2 * w);
            }
            int flow = 0;
            var prevEdge = new int[nodes];
            for (int round = 0; round < MinExitsCap; round++)
            {
                for (int i = 0; i < nodes; i++) prevEdge[i] = -2;
                var q = new Queue<int>();
                int start = 2 * src + 1;
                prevEdge[start] = -1;
                q.Enqueue(start);
                bool found = false;
                while (q.Count > 0 && !found)
                {
                    int u = q.Dequeue();
                    for (int e = head[u]; e >= 0; e = next[e])
                    {
                        if (cap[e] <= 0 || prevEdge[to[e]] != -2) continue;
                        prevEdge[to[e]] = e;
                        if (to[e] == sink) { found = true; break; }
                        q.Enqueue(to[e]);
                    }
                }
                if (!found) break;
                for (int v = sink; v != start;)
                {
                    int e = prevEdge[v];
                    cap[e] -= 1; cap[e ^ 1] += 1;
                    v = to[e ^ 1];
                }
                flow++;
            }
            return flow;
        }

        const int MinExitsCap = FloorRules.MinExits;

        // ------------------------------------------------------------------ rule 2: seen share

        /// <summary>The point a sight line to <paramref name="d"/> aims at (its mouth): a shutter or hatch 1.5 m up and 0.35 m
        /// out of its face along its yaw; a vent or a rail end exactly where it is.</summary>
        public static Vector3 DoorTarget(SpawnDoor d)
        {
            if (d.kind != DoorKind.ShopShutter && d.kind != DoorKind.ServiceHatch) return d.position;
            float yaw = d.yawDeg * (MathF.PI / 180f);
            return d.position + new Vector3(MathF.Sin(yaw) * DoorMouthOut, DoorMouthUp, MathF.Cos(yaw) * DoorMouthOut);
        }

        /// <summary>The share (0..1) of <paramref name="doors"/> whose mouth the point sees: within <paramref name="range"/>
        /// metres and not blocked by <paramref name="blockers"/>.</summary>
        public static float SeenShare(IReadOnlyList<CoverBox> blockers, Vector3 eyePoint, IReadOnlyList<SpawnDoor> doors, float range = 60f)
        {
            if (doors == null || doors.Count == 0) return 0f;
            var targets = new Vector3[doors.Count];
            for (int i = 0; i < targets.Length; i++) targets[i] = DoorTarget(doors[i]);
            return SeenShare(blockers, eyePoint, targets, range);
        }

        static float SeenShare(IReadOnlyList<CoverBox> blockers, Vector3 eye, Vector3[] targets, float range)
        {
            if (targets.Length == 0) return 0f;
            int seen = 0;
            for (int i = 0; i < targets.Length; i++)
            {
                if (Vector3.Distance(eye, targets[i]) > range) continue;
                if (!CoverGeometry.Blocked(eye, targets[i], blockers)) seen++;
            }
            return (float)seen / targets.Length;
        }

        // ------------------------------------------------------------------ rule 3: circle exposure

        /// <summary>The exposure of one socket's tow circle: 8 sectors of 45 degrees round the centre; for each, the best share
        /// of the 25 disc sample points (24 on the rim and the centre) any standable node 8 to 30 m away inside it sees. The
        /// result is the best score two sectors at least 90 degrees apart both reach (the circle passes at
        /// <see cref="FloorRules.MinCircleExposure"/>); <paramref name="exposedSectors"/> counts sectors at or over that share.</summary>
        public static float CircleExposure(IReadOnlyList<CoverBox> blockers, ArenaNav nav, SocketDef s, out int exposedSectors) =>
            CircleExposure(blockers, nav, s, out exposedSectors, out _, new LintOptions());

        /// <summary>The same with other viewer distances.</summary>
        public static float CircleExposure(IReadOnlyList<CoverBox> blockers, ArenaNav nav, SocketDef s, LintOptions options, out int exposedSectors) =>
            CircleExposure(blockers, nav, s, out exposedSectors, out _, options);

        static float CircleExposure(IReadOnlyList<CoverBox> blockers, ArenaNav nav, SocketDef s, out int exposedSectors, out float[] sectorBest, LintOptions o)
        {
            var samples = CircleSamplePoints(nav, s);
            sectorBest = new float[SectorCount];
            Vector3 c = s.position;
            for (int i = 0; i < nav.Count; i++)
            {
                Vector3 p = nav.PositionOf(i);
                float dx = p.X - c.X, dz = p.Z - c.Z;
                float flat = MathF.Sqrt(dx * dx + dz * dz);
                if (flat < o.viewerMin || flat > o.viewerMax) continue;
                int sector = SectorOf(dx, dz);
                if (sectorBest[sector] >= 1f) continue;
                var eye = new Vector3(p.X, p.Y + ArenaNav.EyeHeight, p.Z);
                int seen = 0;
                for (int k = 0; k < samples.Length; k++)
                    if (!CoverGeometry.Blocked(eye, samples[k], blockers)) seen++;
                float share = (float)seen / samples.Length;
                if (share > sectorBest[sector]) sectorBest[sector] = share;
            }
            exposedSectors = 0;
            for (int k = 0; k < SectorCount; k++) if (sectorBest[k] >= FloorRules.MinCircleExposure) exposedSectors++;
            float best = 0f;
            // sector centres are 45 degrees apart, so two or more sectors apart is at least 90 degrees
            for (int a = 0; a < SectorCount; a++)
                for (int b = a + 2; b < SectorCount; b++)
                {
                    int gap = Math.Min(b - a, SectorCount - (b - a));
                    if (gap < 2) continue;
                    best = Math.Max(best, Math.Min(sectorBest[a], sectorBest[b]));
                }
            return best;
        }

        /// <summary>The sector (0..7, 45 degrees each, sector 0 centred on north, clockwise) of a flat offset.</summary>
        static int SectorOf(float dx, float dz)
        {
            float bearing = MathF.Atan2(dx, dz) * (180f / MathF.PI);
            if (bearing < 0f) bearing += 360f;
            return (int)((bearing + 22.5f) / 45f) % SectorCount;
        }

        /// <summary>The 25 disc sample points: the centre and 24 on the rim, each 1 m above the ground it stands on.</summary>
        static Vector3[] CircleSamplePoints(ArenaNav nav, SocketDef s)
        {
            var pts = new Vector3[CircleSamples + 1];
            for (int i = 0; i <= CircleSamples; i++)
            {
                float x = s.position.X, z = s.position.Z;
                if (i < CircleSamples)
                {
                    float a = i * (2f * MathF.PI / CircleSamples);
                    x += MathF.Sin(a) * FloorRules.CircleRadius; z += MathF.Cos(a) * FloorRules.CircleRadius;
                }
                float ground = GroundNear(nav, x, z, s.position.Y);
                pts[i] = new Vector3(x, ground + 1f, z);
            }
            return pts;
        }

        /// <summary>The height of the node nearest (x, z) within the circle's height band round <paramref name="y"/>; y itself
        /// when none.</summary>
        static float GroundNear(ArenaNav nav, float x, float z, float y)
        {
            int n = nav.NodeAt(new Vector3(x, y, z));
            if (n < 0) return y;
            float ny = nav.PositionOf(n).Y;
            return Math.Abs(ny - y) <= Repossession.CircleHalfHeight ? ny : y;
        }

        // ------------------------------------------------------------------ the run

        static LintResult RunCore(IReadOnlyList<FloorSolid> solids, IReadOnlyList<SpawnDoor> doors, IReadOnlyList<SocketDef> sockets,
            IReadOnlyList<FloorZone> exempt, IReadOnlyList<FloorZone> nameZones, LintOptions o)
        {
            if (solids == null) throw new ArgumentNullException(nameof(solids));
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var r = new LintResult();
            doors = doors ?? new SpawnDoor[0];
            sockets = sockets ?? new SocketDef[0];
            var open = OpenState(solids);
            var nav = ArenaNav.Build(open, o.cell);
            var hopNav = o.hopHeight > 0f ? ArenaNav.Build(open, o.cell, o.hopHeight) : nav;       // rule 1 only: the same grid, the low props hopped
            var blockers = BlockersOf(open);
            var targetList = new List<Vector3>();
            for (int i = 0; i < doors.Count; i++)
                if (o.ventsCount || doors[i].kind != DoorKind.SkylightVent) targetList.Add(DoorTarget(doors[i]));
            var targets = targetList.ToArray();

            // islands
            int cut = 0;
            for (int i = 0; i < nav.Count; i++) if (nav.ComponentOf(i) != 0 && !nav.IsClimbable(i)) cut++;
            r.islandNodes = cut;
            if (cut > 4) r.problems.Add($"nav: {cut} walkable nodes are cut off from the main floor (more than 4 allowed; a ledge a jump and a mantle climb is not cut off)");

            // rules 1 and 2 on every non-exempt node
            var exitFail = new List<int>();
            var sightFail = new List<int>();
            r.minExits = FloorRules.MinExits;
            for (int i = 0; i < nav.Count; i++)
            {
                Vector3 p = nav.PositionOf(i);
                if (InAny(exempt, p) || nav.ComponentOf(i) != 0) { r.exempt++; continue; }    // islands and climbable ledges are not on the walking graph
                r.standable++;
                int exits = Exits(hopNav, ReferenceEquals(hopNav, nav) ? i : hopNav.NodeAt(p), FloorRules.ExitRadius);
                if (exits < r.minExits) r.minExits = exits;
                if (exits < FloorRules.MinExits) exitFail.Add(i);
                var eye = new Vector3(p.X, p.Y + ArenaNav.EyeHeight, p.Z);
                float share = SeenShare(blockers, eye, targets, 60f);
                if (share > r.worstSeenShare) { r.worstSeenShare = share; r.worstSeenAt = p; }
                if (share > FloorRules.MaxSeenShare + 1e-4f) sightFail.Add(i);
            }
            r.failedExits = exitFail.Count;
            r.failedSight = sightFail.Count;
            Cluster(nav, exitFail, nameZones, "rule 1 (exits): fewer than 2 exits from a 10 m radius", r.problems);
            var shareOf = new Dictionary<int, float>();
            foreach (int i in sightFail)
            {
                Vector3 p = nav.PositionOf(i);
                shareOf[i] = SeenShare(blockers, new Vector3(p.X, p.Y + ArenaNav.EyeHeight, p.Z), targets, 60f);
            }
            Cluster(nav, sightFail, nameZones, string.Format(CultureInfo.InvariantCulture, "rule 2 (seen share): sees more than {0:0%} of the ground spawn doors", FloorRules.MaxSeenShare), r.problems, i => shareOf[i]);

            // rule 3 on every socket
            r.circleExposure = new float[sockets.Count];
            r.circleSectors = new int[sockets.Count];
            for (int k = 0; k < sockets.Count; k++)
            {
                var s = sockets[k];
                r.circleExposure[k] = CircleExposure(blockers, nav, s, out r.circleSectors[k], out _, o);
                if (r.circleExposure[k] < FloorRules.MinCircleExposure)
                {
                    r.failedCircles++;
                    r.problems.Add($"rule 3 (circle exposure): {s.id} is exposed {r.circleExposure[k]:0%} from two bearings 90 degrees apart (needs {FloorRules.MinCircleExposure:0%}); {r.circleSectors[k]} of 8 sectors expose it at all");
                }
                // standable ground in the circle, in the main piece
                int inCircle = 0, inMain = 0;
                for (int i = 0; i < nav.Count; i++)
                {
                    Vector3 p = nav.PositionOf(i);
                    float dx = p.X - s.position.X, dz = p.Z - s.position.Z;
                    if (dx * dx + dz * dz > FloorRules.CircleRadius * FloorRules.CircleRadius || Math.Abs(p.Y - s.position.Y) > Repossession.CircleHalfHeight) continue;
                    inCircle++;
                    if (nav.ComponentOf(i) == 0) inMain++;
                }
                if (inMain < 3) r.problems.Add($"circle: {s.id} has only {inMain} walkable nodes (of {inCircle}) inside its tow circle in the main walkable piece");
            }

            // spawn doors: body clearance, a walkable mouth, and notes about doors under circles on another level
            for (int d = 0; d < doors.Count; d++) CheckDoor(open, nav, doors[d], r);
            for (int d = 0; d < doors.Count; d++)
                foreach (var s in sockets)
                {
                    float dx = doors[d].position.X - s.position.X, dz = doors[d].position.Z - s.position.Z;
                    float flat = MathF.Sqrt(dx * dx + dz * dz), dy = Math.Abs(doors[d].position.Y - s.position.Y);
                    if (doors[d].kind != DoorKind.SkylightVent && flat < 6f && dy > Repossession.CircleHalfHeight)
                        r.notes.Add($"door {doors[d].id} is {flat:0.0} m (flat) under/over the circle of {s.id}, {dy:0.0} m apart in height");
                }

            // the surfaces the spec says are runnable
            foreach (var s in open)
                if ((s.role == "facade-pane" || s.role == "rim") && !s.runnable)
                    r.problems.Add($"runnable: {s.id} ({s.role}) is not runnable");

            sw.Stop();
            r.seconds = sw.Elapsed.TotalSeconds;
            return r;
        }

        static bool InAny(IReadOnlyList<FloorZone> zones, Vector3 p)
        {
            if (zones == null) return false;
            for (int i = 0; i < zones.Count; i++)
            {
                var z = zones[i];
                if (p.X >= z.minX - 1e-3f && p.X <= z.maxX + 1e-3f && p.Z >= z.minZ - 1e-3f && p.Z <= z.maxZ + 1e-3f) return true;
            }
            return false;
        }

        static string ZoneNames(IReadOnlyList<FloorZone> zones, Vector3 p)
        {
            if (zones == null) return "";
            var sb = new StringBuilder();
            foreach (var z in zones)
            {
                if (p.X < z.minX - 1e-3f || p.X > z.maxX + 1e-3f || p.Z < z.minZ - 1e-3f || p.Z > z.maxZ + 1e-3f) continue;
                if (Math.Abs(z.y - p.Y) > 3.5f) continue;
                if (sb.Length > 0) sb.Append('/');
                sb.Append(z.id);
            }
            return sb.ToString();
        }

        /// <summary>Groups failing nodes into connected clusters (by the nav's edges) and adds one line per cluster.</summary>
        static void Cluster(ArenaNav nav, List<int> failing, IReadOnlyList<FloorZone> zones, string what, List<string> into, Func<int, float> value = null)
        {
            if (failing.Count == 0) return;
            var set = new HashSet<int>(failing);
            var seen = new HashSet<int>();
            foreach (int start in failing)
            {
                if (seen.Contains(start)) continue;
                var members = new List<int>();
                var q = new Queue<int>();
                q.Enqueue(start); seen.Add(start);
                while (q.Count > 0)
                {
                    int u = q.Dequeue();
                    members.Add(u);
                    foreach (int v in nav.Neighbours(u))
                        if (set.Contains(v) && seen.Add(v)) q.Enqueue(v);
                }
                Vector3 first = nav.PositionOf(members[0]);
                float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue, worst = 0f;
                foreach (int m in members)
                {
                    Vector3 p = nav.PositionOf(m);
                    minX = Math.Min(minX, p.X); maxX = Math.Max(maxX, p.X); minZ = Math.Min(minZ, p.Z); maxZ = Math.Max(maxZ, p.Z);
                    if (value != null) worst = Math.Max(worst, value(m));
                }
                string zone = ZoneNames(zones, first);
                string extra = value != null ? $", worst {worst:0%}" : "";
                into.Add(string.Format(CultureInfo.InvariantCulture, "{0}: {1} point(s) y {2:0.#}, x {3:0.#}..{4:0.#}, z {5:0.#}..{6:0.#}{7}{8}",
                    what, members.Count, first.Y, minX, maxX, minZ, maxZ, extra, zone.Length > 0 ? " [" + zone + "]" : ""));
            }
        }

        // ------------------------------------------------------------------ spawn doors

        /// <summary>The body radius of the largest bot allowed through the door.</summary>
        public static float DoorBodyRadius(SpawnDoor d)
        {
            float r = 0f;
            foreach (string id in new[] { SkylineEnemies.DroneId, SkylineEnemies.FloorCareId, SkylineEnemies.GondolaId, SkylineEnemies.SpotlightId })
                if (FloorRules.DoorAllows(d.kind, id)) r = Math.Max(r, SkylineEnemies.Find(id).bodyRadius);
            return r;
        }

        /// <summary>Where the door's body is tested: a vent or rail end at its position, a hatch at its position, a shutter
        /// just outside its face (the bot comes out of the shop).</summary>
        public static Vector3 DoorBodyCentre(SpawnDoor d)
        {
            float r = DoorBodyRadius(d);
            bool ground = d.kind == DoorKind.ShopShutter || d.kind == DoorKind.ServiceHatch;
            Vector3 c = d.position;
            if (ground) c.Y += r;
            if (d.kind == DoorKind.ShopShutter)
            {
                float yaw = d.yawDeg * (MathF.PI / 180f);
                c += new Vector3(MathF.Sin(yaw), 0f, MathF.Cos(yaw)) * (r + 0.05f);
            }
            return c;
        }

        static void CheckDoor(IReadOnlyList<FloorSolid> solids, ArenaNav nav, SpawnDoor d, LintResult r)
        {
            float rad = DoorBodyRadius(d);
            Vector3 c = DoorBodyCentre(d);
            foreach (var s in solids)
            {
                if (s.role == "skylight" && d.kind == DoorKind.SkylightVent) continue;       // the vent is a hole in the glazing
                float dist = DistanceToBox(c, s.box);
                if (dist < rad - 0.02f)
                {
                    r.failedDoors++;
                    r.problems.Add(string.Format(CultureInfo.InvariantCulture,
                        "door {0}: its body (radius {1:0.##}) at ({2:0.##}, {3:0.##}, {4:0.##}) is {5:0.00} m from {6} ({7}); it needs {1:0.##}",
                        d.id, rad, c.X, c.Y, c.Z, dist, s.id, s.role));
                    return;
                }
            }
            if (d.kind == DoorKind.ShopShutter || d.kind == DoorKind.ServiceHatch)
            {
                int node = nav.NodeAt(new Vector3(c.X, d.position.Y, c.Z));
                bool ok = node >= 0 && nav.ComponentOf(node) == 0;
                if (ok)
                {
                    Vector3 p = nav.PositionOf(node);
                    ok = Math.Abs(p.X - c.X) <= 1.6f && Math.Abs(p.Z - c.Z) <= 1.6f && Math.Abs(p.Y - d.position.Y) <= 0.5f;
                }
                if (!ok)
                {
                    r.failedDoors++;
                    r.problems.Add($"door {d.id}: no walkable node of the main floor within 1.6 m of its mouth");
                }
            }
        }

        static float DistanceToBox(Vector3 p, CoverBox b)
        {
            float dx = Math.Max(Math.Max(b.min.X - p.X, p.X - b.max.X), 0f);
            float dy = Math.Max(Math.Max(b.min.Y - p.Y, p.Y - b.max.Y), 0f);
            float dz = Math.Max(Math.Max(b.min.Z - p.Z, p.Z - b.max.Z), 0f);
            return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
        }
    }
}
