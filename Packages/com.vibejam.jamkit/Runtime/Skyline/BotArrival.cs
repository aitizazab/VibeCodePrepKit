using System;
using System.Collections.Generic;
using System.Numerics;

namespace JamKit.Skyline
{
    /// <summary>One place a drone could hold: where, which preference band it came from (0 is the 12..28 m ring the plan asks
    /// for, 1 and 2 are the closer fallbacks used when a spot has no clear line from the far ring), and its bearing from the
    /// player in degrees (0 = +Z, clockwise from above).</summary>
    public struct EngageCandidate
    {
        public Vector3 point;
        public int band;
        public float bearingDeg;
    }

    /// <summary>
    /// Where a door-spawned bot goes to work (task W4, docs/SKYLINE-PLAN-FLOOR1.md decision D6). Pure and seeded. The drone
    /// holds a point 12..28 m from the player (flat), 3.8 m above the player's ground, with a clear line to the player's chest,
    /// re-picked every 6..9 s (each bot has its own period). The floor-care unit drives straight at the player. Spotlights and
    /// the gondola (which rides its rail) have no engagement point: <see cref="For"/> answers the player's own position.
    /// <para>"The player" is the player's FEET (the controller's origin); the chest is <see cref="ChestHeight"/> above.</para>
    /// </summary>
    public static class EngagePoints
    {
        /// <summary>The drone hovers this far above the player's ground (m).</summary>
        public const float DroneAltitude = 3.8f;
        /// <summary>The chest a drone must see (m above the feet).</summary>
        public const float ChestHeight = 1.25f;
        /// <summary>The drone's ring round the player, flat metres.</summary>
        public const float MinRange = 12f, MaxRange = 28f;
        /// <summary>A bot re-picks every MinRepickSeconds..MaxRepickSeconds (its own seeded value).</summary>
        public const float MinRepickSeconds = 6f, MaxRepickSeconds = 9f;
        /// <summary>The number of preference bands <see cref="Candidates"/> fills (the 12..28 m ring, then two closer ones).</summary>
        public const int Bands = 3;

        // band 0 sits just inside MinRange..MaxRange so the 3D distance to the chest stays inside 12..28 too
        static readonly float[] BandLo = { 12.2f, 7f, 3f }, BandHi = { 27.6f, 12f, 7f };
        static readonly int[] BandCount = { 48, 24, 16 };

        /// <summary>True for the bots that fly to a hover point (the mall-cop drone: air layer with a hover patrol).</summary>
        public static bool Flies(EnemyDef def) => def != null && def.layer == ThreatLayer.Air && HoverPatrol.Moves(def);

        /// <summary>True for the bots that drive at the player along the floor (the floor-care unit).</summary>
        public static bool Rolls(EnemyDef def) => def != null && def.moveSpeed > 0f && def.layer == ThreatLayer.Floor;

        /// <summary>This bot's re-pick period (s), 6..9 from its 0..1 seed.</summary>
        public static float RepickSeconds(float seed01) =>
            MinRepickSeconds + (MaxRepickSeconds - MinRepickSeconds) * Math.Min(1f, Math.Max(0f, float.IsNaN(seed01) ? 0f : seed01));

        /// <summary>Which re-pick period of the bot's life <paramref name="now"/> falls in.</summary>
        public static int Bucket(float seed01, float now) => now > 0f ? (int)(now / RepickSeconds(seed01)) : 0;

        /// <summary>
        /// The point the bot should work from right now. Drone: a point 12..28 m from the player with a clear line to the
        /// chest, at the player's ground + 3.8 m (the first of <see cref="Candidates"/>; when the spot has no clear line from
        /// that far it falls back to a closer ring, and last to straight above the player). Floor-care: the player's own spot,
        /// or the nearest cover-free point round it when that is inside cover. Same inputs, same point.
        /// </summary>
        public static Vector3 For(EnemyDef def, Vector3 player, IReadOnlyList<CoverBox> blockers, float seed01, float now)
        {
            if (Flies(def))
            {
                var list = new List<EngageCandidate>();
                Candidates(def, player, blockers, seed01, now, list);
                if (list.Count > 0) return list[0].point;
                float r = def.bodyRadius;
                Vector3 chest = player + new Vector3(0f, ChestHeight, 0f);
                foreach (float lift in new[] { DroneAltitude, 2.6f, 2.0f })
                {
                    var p = player + new Vector3(0f, lift, 0f);
                    if (!Inside(p, r, blockers) && !CoverGeometry.Blocked(p, chest, blockers)) return p;
                }
                return player + new Vector3(0f, DroneAltitude, 0f);
            }
            if (Rolls(def)) return FloorApproach(def, player, blockers, seed01);
            return player;
        }

        /// <summary>
        /// Appends the drone's candidate hover points to <paramref name="into"/>: band 0 (12..28 m flat from the player), then
        /// bands 1 (7..12 m) and 2 (3..7 m). Each is at the player's ground + 3.8 m, outside every blocker (grown by the
        /// bot's radius), with a clear line to the chest, and accepted by <paramref name="accept"/> (the flight area), in a
        /// fixed order that depends only on the seed and the re-pick period (<paramref name="now"/>). Returns how many it added.
        /// </summary>
        public static int Candidates(EnemyDef def, Vector3 player, IReadOnlyList<CoverBox> blockers, float seed01, float now,
            List<EngageCandidate> into, Func<Vector3, bool> accept = null)
        {
            if (!Flies(def) || into == null) return 0;
            int before = into.Count;
            float r = def.bodyRadius, y = player.Y + DroneAltitude;
            Vector3 chest = player + new Vector3(0f, ChestHeight, 0f);
            int bucket = Bucket(seed01, now);
            float a0 = H01(Key(seed01, bucket, 1)) * 360f;
            for (int band = 0; band < Bands; band++)
            {
                float lo = BandLo[band], hi = BandHi[band], phase = H01(Key(seed01, bucket, 2 + band));
                for (int i = 0; i < BandCount[band]; i++)
                {
                    float angle = a0 + i * 137.50776f;
                    float f = (i * 0.6180339f + phase) % 1f;
                    float d = lo + (hi - lo) * f;
                    double rad = angle * Math.PI / 180.0;
                    var p = new Vector3(player.X + (float)Math.Sin(rad) * d, y, player.Z + (float)Math.Cos(rad) * d);
                    if (accept != null && !accept(p)) continue;
                    if (Inside(p, r, blockers) || CoverGeometry.Blocked(p, chest, blockers)) continue;
                    float bearing = angle % 360f;
                    if (bearing < 0f) bearing += 360f;
                    into.Add(new EngageCandidate { point = p, band = band, bearingDeg = bearing });
                }
            }
            return into.Count - before;
        }

        /// <summary>True when a sphere of <paramref name="radius"/> at <paramref name="p"/> touches a blocker.</summary>
        public static bool Inside(Vector3 p, float radius, IReadOnlyList<CoverBox> blockers)
        {
            if (blockers == null) return false;
            for (int i = 0; i < blockers.Count; i++)
            {
                var b = blockers[i];
                if (p.X >= b.min.X - radius && p.X <= b.max.X + radius && p.Y >= b.min.Y - radius && p.Y <= b.max.Y + radius
                    && p.Z >= b.min.Z - radius && p.Z <= b.max.Z + radius) return true;
            }
            return false;
        }

        /// <summary>True when a floor unit's body band (ankle to knee, 0.35..1.0 m over the floor at <paramref name="y"/>) at
        /// (x, z) touches a blocker grown by <paramref name="radius"/> on the floor plan.</summary>
        public static bool BodyInside(float x, float z, float y, float radius, IReadOnlyList<CoverBox> blockers)
        {
            if (blockers == null) return false;
            for (int i = 0; i < blockers.Count; i++)
            {
                var b = blockers[i];
                if (b.max.Y <= y + 0.35f || b.min.Y >= y + 1.0f) continue;
                if (x >= b.min.X - radius && x <= b.max.X + radius && z >= b.min.Z - radius && z <= b.max.Z + radius) return true;
            }
            return false;
        }

        static Vector3 FloorApproach(EnemyDef def, Vector3 feet, IReadOnlyList<CoverBox> blockers, float seed01)
        {
            float r = def.bodyRadius;
            if (!BodyInside(feet.X, feet.Z, feet.Y, r, blockers)) return feet;
            float a0 = H01(Key(seed01, 0, 9)) * 360f;
            foreach (float d in new[] { 1.5f, 2.5f, 3.5f })
                for (int k = 0; k < 8; k++)
                {
                    double rad = (a0 + k * 45f) * Math.PI / 180.0;
                    float x = feet.X + (float)Math.Sin(rad) * d, z = feet.Z + (float)Math.Cos(rad) * d;
                    if (!BodyInside(x, z, feet.Y, r, blockers)) return new Vector3(x, feet.Y, z);
                }
            return feet;
        }

        // ---- a small deterministic hash (SplitMix64) so no engage point needs a stateful generator ----

        static ulong Key(float seed01, int bucket, int salt) =>
            unchecked(((ulong)(uint)BitConverter.SingleToInt32Bits(float.IsNaN(seed01) ? 0f : seed01) << 32) ^ (uint)bucket ^ ((ulong)salt << 56));

        static ulong Mix(ulong x)
        {
            unchecked
            {
                x += 0x9E3779B97F4A7C15UL;
                x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
                x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
                return x ^ (x >> 31);
            }
        }

        internal static float H01(ulong key) => (Mix(key) >> 40) * (1f / 16777216f);
    }

    /// <summary>
    /// A door-spawned bot's way from its door to work, per bot, from the moment it leaves the door (task W4, D6). Pure,
    /// seeded and deterministic; the headless sim and the Unity bot call the same <see cref="Step"/>.
    /// <list type="bullet">
    /// <item><b>Drone.</b> Appears at its door (<see cref="StartPosition"/>: a vent is 13.6 m up; a shop shutter is on the ring)
    /// and flies to an engage point (<see cref="EngagePoints"/>) at up to <see cref="DroneTopSpeed"/> m/s along a short route
    /// that never enters a blocker: straight when the way is clear, else up to a flight lane (just under the skylight), across
    /// and down the column over the engage point. Of the points with a feasible route it takes the nearest, with a seeded
    /// preference for one bearing so a wave does not stack on one spot. <see cref="Engaged"/> turns true when it gets there;
    /// the brain attacks only then. After that <see cref="Step"/> keeps working: every 6..9 s (or once the point has lost its
    /// line or its distance) it re-picks and drifts there at <see cref="DroneRepositionSpeed"/>, never jumping.</item>
    /// <item><b>Floor-care.</b> Drives at <c>moveSpeed</c> straight at the player along <see cref="ArenaNav"/> paths
    /// (ramps, escalators; re-planned every 0.8 s or when the player has moved 2.5 m), stops half its sweep radius short;
    /// without a nav it steers straight round the cover. <see cref="Engaged"/> turns true within
    /// <see cref="FloorEngageDistance"/> of the player on the same level, and stays true.</item>
    /// <item>Anything else (the spotlight, the gondola, which rides its rail): engaged at once, <see cref="Step"/> returns the
    /// position unchanged.</item>
    /// </list>
    /// The flight area is passed as a <see cref="FightArea"/> or as its four edges. Points outside it are never chosen; a drone that starts outside (a vent at the very edge) just
    /// flies in.
    /// </summary>
    public sealed class BotArrival
    {
        /// <summary>A drone's arrival speed (m/s) and acceleration (m/s²); about 4.5 s for 40 m of route.</summary>
        public const float DroneTopSpeed = 10f, DroneAccel = 18f;
        /// <summary>A drone that has arrived drifts to a re-picked point at this speed (m/s).</summary>
        public const float DroneRepositionSpeed = 5.5f;
        /// <summary>The floor-care unit counts as engaged within this flat distance (m) of the player and within
        /// <see cref="FloorEngageHeight"/> of their level.</summary>
        public const float FloorEngageDistance = 4f, FloorEngageHeight = 1.5f;
        /// <summary>The flight lane under the skylight (m): the vents sit at 13.6, the roof's underside is 15.6.</summary>
        public const float LaneY = 13.2f;
        /// <summary>The flat-metre cost of one degree away from the bot's preferred bearing when it picks among engage points.</summary>
        const float BearingCost = 0.04f;
        const float CheckInterval = 0.25f, RepickGap = 0.5f, GiveUpSeconds = 2.5f, ReplanSeconds = 0.8f, ReplanMove = 2.5f;
        const int MaxCandidatesPlanned = 14;

        readonly EnemyDef _def;
        readonly SpawnDoor _door;
        readonly ArenaNav _nav;
        readonly float _seed01, _prefBearing, _r;
        readonly bool _flies, _rolls;

        float _t, _check, _nextPick, _lastPick = -10f, _speed, _failFor, _pickCooldown;
        bool _hasTarget;
        readonly List<Vector3> _route = new List<Vector3>();
        int _wp;
        Vector3 _vel, _start;
        // the blockers minus the pieces the start already touches (a vent beside a wall, a shutter in its frame)
        IReadOnlyList<CoverBox> _activeFor;
        List<CoverBox> _active = new List<CoverBox>(), _thin = new List<CoverBox>();
        readonly List<EngageCandidate> _cands = new List<EngageCandidate>();
        readonly List<Vector3> _tmpRoute = new List<Vector3>();
        // rolling
        List<Vector3> _path;
        int _pi;
        float _repath;
        Vector3 _pathGoal;

        /// <param name="nav">The floor-care unit's walkable graph (<c>ArenaNav.Build(RepossessArena.Solids())</c>); null: it
        /// steers straight round the cover.</param>
        /// <param name="blockers">When given, the start is moved sideways or further out of its door until it is clear of cover
        /// (the north row's middle shutter opens straight onto a gallery column).</param>
        public BotArrival(EnemyDef def, SpawnDoor door, ulong seed, ArenaNav nav = null, IReadOnlyList<CoverBox> blockers = null)
        {
            _def = def ?? throw new ArgumentNullException(nameof(def));
            _door = door;
            _nav = nav;
            _seed01 = EngagePoints.H01(seed ^ 0xA77124A1UL);
            _prefBearing = EngagePoints.H01(seed ^ 0x5EA21FUL) * 360f;
            _r = def.bodyRadius;
            _flies = EngagePoints.Flies(def);
            _rolls = EngagePoints.Rolls(def);
            _start = door != null ? ClearStart(def, door, blockers) : Vector3.Zero;
            Engaged = !(_flies || _rolls);
            Target = _start;
        }

        /// <summary>Whether <see cref="BotArrival"/> moves a bot of this def (the drone and the floor-care unit).</summary>
        public static bool Applies(EnemyDef def) => EngagePoints.Flies(def) || EngagePoints.Rolls(def);

        /// <summary>Where a bot of <paramref name="def"/> appears at <paramref name="door"/>: a ground bot a metre out of its
        /// door (the way the door faces), a drone at a ring-level shutter 1.6 m up and 1.2 m out, a vent drone at the vent.
        /// Anything else (the gondola) at the door itself.</summary>
        public static Vector3 StartPosition(EnemyDef def, SpawnDoor door)
        {
            if (door == null) return Vector3.Zero;
            double yaw = door.yawDeg * Math.PI / 180.0;
            var dir = new Vector3((float)Math.Sin(yaw), 0f, (float)Math.Cos(yaw));
            if (EngagePoints.Flies(def))
                return door.kind == DoorKind.SkylightVent ? door.position : door.position + dir * 1.2f + new Vector3(0f, 1.6f, 0f);
            if (EngagePoints.Rolls(def)) return door.position + dir * 1.0f;
            return door.position;
        }

        /// <summary><see cref="StartPosition"/>, moved clear of <paramref name="blockers"/> when it touches some: further out
        /// (1.2, 1.8, 2.6 m) and sideways (0, then 1, 1.6, 2.2 m to either side). A vent cannot move, and a bot with no blockers
        /// or no free spot keeps the plain start.</summary>
        public static Vector3 ClearStart(EnemyDef def, SpawnDoor door, IReadOnlyList<CoverBox> blockers)
        {
            Vector3 plain = StartPosition(def, door);
            bool flies = EngagePoints.Flies(def), rolls = EngagePoints.Rolls(def);
            if (blockers == null || door == null || !(flies || rolls) || door.kind == DoorKind.SkylightVent) return plain;
            float r = def.bodyRadius;
            double yaw = door.yawDeg * Math.PI / 180.0;
            var dir = new Vector3((float)Math.Sin(yaw), 0f, (float)Math.Cos(yaw));
            var lat = new Vector3(dir.Z, 0f, -dir.X);
            Vector3 lift = flies ? new Vector3(0f, 1.6f, 0f) : Vector3.Zero;
            foreach (float outward in new[] { flies ? 1.2f : 1.0f, 1.8f, 2.6f })
                foreach (float side in new[] { 0f, 1f, -1f, 1.6f, -1.6f, 2.2f, -2.2f })
                {
                    Vector3 p = door.position + dir * outward + lat * side + lift;
                    bool touches = flies ? EngagePoints.Inside(p, r, blockers) : EngagePoints.BodyInside(p.X, p.Z, p.Y, r, blockers);
                    if (!touches) return p;
                }
            return plain;
        }

        /// <summary>The door this bot came through.</summary>
        public SpawnDoor Door => _door;
        /// <summary>Where <see cref="StartPosition"/> put it.</summary>
        public Vector3 Start => _start;
        /// <summary>It has reached a hover point (drone) or come within <see cref="FloorEngageDistance"/> of the player
        /// (floor-care) and is handed to the normal brain. Latches true.</summary>
        public bool Engaged { get; private set; }
        /// <summary>No route to any engage point was found for a while; it was engaged where it hovers.</summary>
        public bool Failed { get; private set; }
        /// <summary>The point it is going to, or the player (floor-care).</summary>
        public Vector3 Target { get; private set; }
        /// <summary>Its velocity at the last step (m/s).</summary>
        public Vector3 Velocity => _vel;
        /// <summary>The way it is moving, degrees (0 = +Z, clockwise from above); 0 when still.</summary>
        public float HeadingDeg => _vel.X * _vel.X + _vel.Z * _vel.Z > 1e-6f ? (float)(Math.Atan2(_vel.X, _vel.Z) * 180.0 / Math.PI) : 0f;
        /// <summary>Seconds since it left the door.</summary>
        public float Elapsed => _t;
        /// <summary>The seconds between this bot's re-picks (6..9).</summary>
        public float RepickSeconds => EngagePoints.RepickSeconds(_seed01);

        /// <summary>
        /// One step of <paramref name="dt"/> seconds from <paramref name="pos"/>; returns the new position. <paramref name="player"/>
        /// is the player's feet; <paramref name="blockers"/> is the arena's sight blockers (constant between calls). The area is
        /// the flight area's edges (x, z).
        /// </summary>
        public Vector3 Step(Vector3 pos, Vector3 player, IReadOnlyList<CoverBox> blockers, float dt, float areaMinX, float areaMaxX, float areaMinZ, float areaMaxZ)
        {
            if (!(dt > 0f) || float.IsInfinity(dt) || !Finite(pos) || !Finite(player)) return pos;
            _t += dt;
            if (_flies) return StepFlying(pos, player, blockers, dt, areaMinX, areaMaxX, areaMinZ, areaMaxZ);
            if (_rolls) return StepRolling(pos, player, blockers, dt);
            _vel = Vector3.Zero;
            return pos;
        }

        /// <summary>The plan's signature (task W4): <see cref="Step(Vector3, Vector3, IReadOnlyList{CoverBox}, float, float, float, float, float)"/>
        /// with the flight area as a <see cref="FightArea"/>.</summary>
        public Vector3 Step(Vector3 pos, Vector3 player, IReadOnlyList<CoverBox> blockers, float dt, FightArea area) =>
            Step(pos, player, blockers, dt, area.minX, area.maxX, area.minZ, area.maxZ);

        // ------------------------------------------------------------------ the drone

        Vector3 StepFlying(Vector3 pos, Vector3 player, IReadOnlyList<CoverBox> blockers, float dt, float minX, float maxX, float minZ, float maxZ)
        {
            EnsureActive(blockers);
            Vector3 chest = player + new Vector3(0f, EngagePoints.ChestHeight, 0f);
            _check -= dt;
            _pickCooldown -= dt;
            bool pick = !_hasTarget && _pickCooldown <= 0f;
            if (_hasTarget && _check <= 0f)
            {
                _check = CheckInterval;
                if (!TargetGood(player, chest) && _t - _lastPick >= RepickGap) pick = true;
            }
            if (_hasTarget && Engaged && _t >= _nextPick && _t - _lastPick >= RepickGap) pick = true;
            if (pick)
            {
                if (!TryPick(pos, player, minX, maxX, minZ, maxZ)) _pickCooldown = CheckInterval;
            }
            if (!_hasTarget)
            {
                _failFor += dt;
                if (_failFor >= GiveUpSeconds && !Engaged)
                {
                    Engaged = true;
                    Failed = true;
                    Target = pos;
                }
                _vel = Vector3.Zero;
                _speed = 0f;
                return pos;
            }
            return Advance(pos, dt);
        }

        bool TargetGood(Vector3 player, Vector3 chest)
        {
            float dx = Target.X - player.X, dz = Target.Z - player.Z;
            float flat = (float)Math.Sqrt(dx * dx + dz * dz);
            if (flat < EngagePoints.MinRange - 3f || flat > EngagePoints.MaxRange + 3f) return false;
            return !CoverGeometry.Blocked(Target, chest, _active);
        }

        void EnsureActive(IReadOnlyList<CoverBox> blockers)
        {
            if (ReferenceEquals(blockers, _activeFor)) return;
            _activeFor = blockers;
            _active = new List<CoverBox>();
            _thin = new List<CoverBox>();
            if (blockers == null) return;
            for (int i = 0; i < blockers.Count; i++)
            {
                var b = blockers[i];
                // (a vent sits exactly one body radius off its wall by design, D23: compare with a millimetre of slack so that float vs double
                // intermediates, Unity's Mono against CoreCLR, cannot flip "touching" on the equality)
                const float slack = 1e-3f;
                bool touchesStart = _start.X >= b.min.X - _r - slack && _start.X <= b.max.X + _r + slack && _start.Y >= b.min.Y - _r - slack && _start.Y <= b.max.Y + _r + slack
                    && _start.Z >= b.min.Z - _r - slack && _start.Z <= b.max.Z + _r + slack;
                if (touchesStart) _thin.Add(b); else _active.Add(b);
            }
        }

        bool TryPick(Vector3 pos, Vector3 player, float minX, float maxX, float minZ, float maxZ)
        {
            _cands.Clear();
            const float margin = 1f;
            EngagePoints.Candidates(_def, player, _active, _seed01, _t, _cands,
                p => p.X >= minX + margin && p.X <= maxX - margin && p.Z >= minZ + margin && p.Z <= maxZ - margin);
            if (_cands.Count == 0) return false;
            BuildPrefix(pos);
            for (int band = 0; band < EngagePoints.Bands; band++)
            {
                // score lower bounds: the straight distance plus the bearing preference; plan the cheapest first and stop when
                // the next one cannot beat the best route found
                var order = new List<(float lb, int idx)>();
                for (int i = 0; i < _cands.Count; i++)
                {
                    if (_cands[i].band != band) continue;
                    float diff = Math.Abs(_cands[i].bearingDeg - _prefBearing) % 360f;
                    if (diff > 180f) diff = 360f - diff;
                    order.Add((Vector3.Distance(pos, _cands[i].point) + BearingCost * diff, i));
                }
                if (order.Count == 0) continue;
                order.Sort((a, b) => a.lb.CompareTo(b.lb));
                float best = float.MaxValue;
                int bestIdx = -1;
                var bestRoute = new List<Vector3>();
                int planned = 0;
                foreach (var o in order)
                {
                    if (o.lb >= best || planned >= MaxCandidatesPlanned) break;
                    planned++;
                    float len = PlanRoute(_cands[o.idx].point, _tmpRoute);
                    if (len < 0f) continue;
                    float diff = Math.Abs(_cands[o.idx].bearingDeg - _prefBearing) % 360f;
                    if (diff > 180f) diff = 360f - diff;
                    float score = len + BearingCost * diff;
                    if (score >= best) continue;
                    best = score;
                    bestIdx = o.idx;
                    bestRoute.Clear();
                    bestRoute.AddRange(_tmpRoute);
                }
                if (bestIdx < 0) continue;
                _route.Clear();
                _route.AddRange(bestRoute);
                _wp = 0;
                Target = _cands[bestIdx].point;
                _hasTarget = true;
                _failFor = 0f;
                _lastPick = _t;
                _nextPick = _t + EngagePoints.RepickSeconds(_seed01);
                _check = CheckInterval;
                if (_route.Count == 0) Engaged = true;
                return true;
            }
            return false;
        }

        // The route graph. Prefix nodes belong to the start and do not depend on the engage point, so the edge checks between
        // them are made once per pick and shared by every candidate: the start; for a drone still at a ring-level door, a ring of
        // exits round it (8 headings from the door's, 3 m and 6 m out: it may have to leave from under a gallery or round an
        // escalator before it can climb); and above each of those, a column up to the flight lanes. An edge is allowed along a
        // column, or between two nodes at the same height. The engage point adds itself and its own columns the same way, and is
        // also joined straight to every prefix node (a diagonal).
        readonly List<Vector3> _pre = new List<Vector3>();
        readonly List<int> _preCol = new List<int>();
        sbyte[] _preEdge = new sbyte[0];
        readonly List<Vector3> _bn = new List<Vector3>();

        void BuildPrefix(Vector3 from)
        {
            _pre.Clear();
            _preCol.Clear();
            _pre.Add(from);
            _preCol.Add(0);
            var cols = new List<Vector3> { from };
            if (!Engaged && _door != null && _door.kind != DoorKind.SkylightVent && from.Y < 5f)
                for (int k = 0; k < 8; k++)
                {
                    double a = (_door.yawDeg + k * 45f) * Math.PI / 180.0;
                    var dir = new Vector3((float)Math.Sin(a), 0f, (float)Math.Cos(a));
                    foreach (float d in new[] { 3f, 6f }) cols.Add(from + dir * d);
                }
            for (int c = 1; c < cols.Count; c++) { _pre.Add(cols[c]); _preCol.Add(c); }
            foreach (float h in new[] { LaneY, 9.5f })
                for (int c = 0; c < cols.Count; c++)
                    if (Math.Abs(h - cols[c].Y) > 0.6f) { _pre.Add(new Vector3(cols[c].X, h, cols[c].Z)); _preCol.Add(c); }
            _preEdge = new sbyte[_pre.Count * _pre.Count];
        }

        bool Clear(Vector3 a, Vector3 b) => !(CoverGeometry.BlockedThick(a, b, _active, _r) || CoverGeometry.Blocked(a, b, _thin));

        /// <summary>The shortest feasible route (waypoints after the start) from the start to <paramref name="to"/>, every edge
        /// swept with the bot's radius; the length, or -1 when there is none. <see cref="BuildPrefix"/> first.</summary>
        float PlanRoute(Vector3 to, List<Vector3> route)
        {
            route.Clear();
            int np = _pre.Count;
            _bn.Clear();
            _bn.Add(to);
            foreach (float h in new[] { LaneY, 9.5f, _pre[0].Y })
            {
                bool dupe = Math.Abs(h - to.Y) <= 0.6f;
                for (int j = 1; j < _bn.Count; j++) if (Math.Abs(_bn[j].Y - h) < 0.6f) dupe = true;
                if (!dupe) _bn.Add(new Vector3(to.X, h, to.Z));
            }
            int n = np + _bn.Count;
            int goal = np;
            var dist = new float[n];
            var prev = new int[n];
            var done = new bool[n];
            for (int i = 0; i < n; i++) { dist[i] = float.MaxValue; prev[i] = -1; }
            dist[0] = 0f;
            for (;;)
            {
                int u = -1;
                for (int i = 0; i < n; i++) if (!done[i] && dist[i] < float.MaxValue && (u < 0 || dist[i] < dist[u])) u = i;
                if (u < 0) return -1f;
                if (u == goal) break;
                done[u] = true;
                Vector3 pu = Pt(u, np);
                for (int v = 0; v < n; v++)
                {
                    if (done[v]) continue;
                    Vector3 pv = Pt(v, np);
                    float len = Vector3.Distance(pu, pv);
                    if (dist[u] + len >= dist[v]) continue;
                    if (!Allowed(u, v, np, pu, pv)) continue;
                    bool clear;
                    if (u < np && v < np)
                    {
                        int e = u * np + v;
                        if (_preEdge[e] == 0) _preEdge[e] = _preEdge[v * np + u] = (sbyte)(Clear(pu, pv) ? 1 : -1);
                        clear = _preEdge[e] > 0;
                    }
                    else clear = Clear(pu, pv);
                    if (!clear) continue;
                    dist[v] = dist[u] + len;
                    prev[v] = u;
                }
            }
            var back = new List<Vector3>();
            for (int c = goal; c > 0; c = prev[c]) back.Add(Pt(c, np));
            back.Reverse();
            route.AddRange(back);
            return dist[goal];
        }

        Vector3 Pt(int i, int np) => i < np ? _pre[i] : _bn[i - np];

        /// <summary>The graph's edges: along a column, between nodes at one height, and from the engage point to anywhere.</summary>
        bool Allowed(int u, int v, int np, Vector3 pu, Vector3 pv)
        {
            bool bu = u >= np, bv = v >= np;
            if (!bu && !bv) return _preCol[u] == _preCol[v] || Math.Abs(pu.Y - pv.Y) < 0.3f;
            if (bu && bv) return u == np || v == np;                       // the engage point and its own columns
            return (bu ? u : v) == np || Math.Abs(pu.Y - pv.Y) < 0.3f;     // the engage point to anywhere; its columns level
        }

        Vector3 Advance(Vector3 pos, float dt)
        {
            if (_wp >= _route.Count)
            {
                _vel = Vector3.Zero;
                _speed = 0f;
                if (!Engaged) Engaged = true;
                return _route.Count > 0 && Vector3.Distance(pos, _route[_route.Count - 1]) < 0.01f ? _route[_route.Count - 1] : pos;
            }
            float rem = Vector3.Distance(pos, _route[_wp]);
            for (int i = _wp + 1; i < _route.Count; i++) rem += Vector3.Distance(_route[i - 1], _route[i]);
            float vmax = Engaged ? DroneRepositionSpeed : DroneTopSpeed;
            _speed = Math.Min(vmax, _speed + DroneAccel * dt);
            float brake = (float)Math.Sqrt(2f * DroneAccel * 0.7f * rem);
            _speed = Math.Min(_speed, Math.Max(brake, 0.8f));
            float step = _speed * dt;
            Vector3 p = pos, lastDir = _vel;
            while (step > 1e-6f && _wp < _route.Count)
            {
                Vector3 d = _route[_wp] - p;
                float len = d.Length();
                if (len <= step)
                {
                    p = _route[_wp];
                    step -= len;
                    if (len > 1e-6f) lastDir = d / len * _speed;
                    _wp++;
                }
                else
                {
                    lastDir = d / len * _speed;
                    p += d / len * step;
                    step = 0f;
                }
            }
            _vel = lastDir;
            if (_wp >= _route.Count)
            {
                Engaged = true;
                _speed = 0f;
                _vel = Vector3.Zero;
            }
            return p;
        }

        // ------------------------------------------------------------------ the floor-care unit

        Vector3 StepRolling(Vector3 pos, Vector3 player, IReadOnlyList<CoverBox> blockers, float dt)
        {
            float dx = player.X - pos.X, dz = player.Z - pos.Z, dy = player.Y - pos.Y;
            float flat = (float)Math.Sqrt(dx * dx + dz * dz);
            bool sameLevel = Math.Abs(dy) <= FloorEngageHeight;
            if (flat <= FloorEngageDistance && sameLevel) Engaged = true;
            Target = player;
            if (flat < _def.sweepRadius * 0.5f && sameLevel)
            {
                _vel = Vector3.Zero;
                return pos;
            }
            float step = _def.moveSpeed * dt;
            Vector3 next;
            if (_nav != null && FollowPath(pos, player, dt, step, out next)) { }
            else next = Direct(pos, player, blockers, step);
            Vector3 move = next - pos;
            _vel = move / dt;
            return next;
        }

        bool FollowPath(Vector3 pos, Vector3 player, float dt, float step, out Vector3 next)
        {
            next = pos;
            _repath -= dt;
            if (_path == null || _repath <= 0f || Vector3.Distance(_pathGoal, player) > ReplanMove) Replan(pos, player);
            if (_path == null) return false;
            Vector3 p = pos;
            while (step > 1e-6f && _pi < _path.Count)
            {
                Vector3 d = _path[_pi] - p;
                float len = d.Length();
                if (len <= step) { p = _path[_pi]; step -= len; _pi++; }
                else { p += d / len * step; step = 0f; }
            }
            next = p;
            return true;
        }

        void Replan(Vector3 pos, Vector3 player)
        {
            _repath = ReplanSeconds;
            _pathGoal = player;
            _path = null;
            _pi = 0;
            int from = _nav.NodeAt(pos), to = _nav.NodeAt(player);
            if (from < 0 || to < 0) return;
            var nodes = _nav.Path(from, to);
            if (nodes == null) return;
            var pts = _nav.Simplify(nodes);
            if (pts.Count == 0) return;
            // the first node is up to a cell away: skip it when the bot is already past it
            if (pts.Count > 1 && Vector3.Distance(pos, pts[1]) < Vector3.Distance(pts[0], pts[1])) pts.RemoveAt(0);
            // the last node is beside the player, not under them: finish on the player's own spot when it is on that level
            Vector3 last = pts[pts.Count - 1];
            float ddx = player.X - last.X, ddz = player.Z - last.Z;
            if (ddx * ddx + ddz * ddz < 3.2f * 3.2f && Math.Abs(player.Y - last.Y) < 1.2f) pts.Add(new Vector3(player.X, last.Y, player.Z));
            _path = pts;
        }

        const float DetourMargin = 0.4f;
        bool _hasDetour;
        CoverBox _detour;
        int _corner;

        /// <summary>No nav (or no path: the player is in the air, or behind a gap): a straight drive at the player; when a piece of
        /// cover stands on the line it makes for the corner of that piece (just outside) that gives the shortest way round, kept
        /// until the line is clear (the same rule the Unity floor-care unit drives by), and slides along a face it still meets.</summary>
        Vector3 Direct(Vector3 pos, Vector3 player, IReadOnlyList<CoverBox> blockers, float step)
        {
            var tgt = new Vector3(player.X, pos.Y, player.Z);
            int hit = FirstBlocker(pos, tgt, blockers, -1);
            Vector3 aim = tgt;
            if (hit >= 0)
            {
                var box = blockers[hit];
                if (!_hasDetour || !SameBox(_detour, box))
                {
                    _detour = box;
                    _hasDetour = true;
                    _corner = BestCorner(box, pos, tgt);
                }
                aim = Corner(_detour, _corner, _r + DetourMargin, pos.Y);
            }
            else _hasDetour = false;
            Vector3 to = aim - pos;
            to.Y = 0f;
            float len = to.Length();
            if (!(len > 1e-4f)) return pos;
            Vector3 move = to / len * Math.Min(step, len);
            if (!BodyHit(pos + move, blockers)) return pos + move;
            // a face in the way: slide along whichever axis is free
            var mx = new Vector3(move.X, 0f, 0f);
            var mz = new Vector3(0f, 0f, move.Z);
            if (Math.Abs(move.X) >= Math.Abs(move.Z))
            {
                if (!BodyHit(pos + mx, blockers)) return pos + mx;
                if (!BodyHit(pos + mz, blockers)) return pos + mz;
            }
            else
            {
                if (!BodyHit(pos + mz, blockers)) return pos + mz;
                if (!BodyHit(pos + mx, blockers)) return pos + mx;
            }
            return pos;
        }

        bool BodyHit(Vector3 p, IReadOnlyList<CoverBox> blockers) => EngagePoints.BodyInside(p.X, p.Z, p.Y, _r * 0.98f, blockers);

        static bool SameBox(CoverBox a, CoverBox b) => a.min == b.min && a.max == b.max;

        /// <summary>The first blocker along the floor-plan line, sampled every 0.25 m; -1 when clear. <paramref name="only"/>: test
        /// that one blocker alone (-1: all).</summary>
        int FirstBlocker(Vector3 a, Vector3 b, IReadOnlyList<CoverBox> blockers, int only)
        {
            if (blockers == null) return -1;
            float dx = b.X - a.X, dz = b.Z - a.Z;
            float len = (float)Math.Sqrt(dx * dx + dz * dz);
            int n = Math.Max(1, (int)(len / 0.25f));
            for (int s = 1; s <= n; s++)
            {
                float x = a.X + dx * s / n, z = a.Z + dz * s / n;
                for (int i = 0; i < blockers.Count; i++)
                {
                    if (only >= 0 && i != only) continue;
                    var c = blockers[i];
                    if (c.max.Y <= a.Y + 0.35f || c.min.Y >= a.Y + 1.0f) continue;
                    float r = _r * 0.98f;
                    if (x >= c.min.X - r && x <= c.max.X + r && z >= c.min.Z - r && z <= c.max.Z + r) return i;
                }
            }
            return -1;
        }

        static Vector3 Corner(CoverBox b, int i, float m, float y) =>
            new Vector3((i & 1) == 0 ? b.min.X - m : b.max.X + m, y, (i & 2) == 0 ? b.min.Z - m : b.max.Z + m);

        int BestCorner(CoverBox box, Vector3 p, Vector3 tgt)
        {
            var single = new[] { box };
            int best = 0;
            float bestLen = float.MaxValue;
            for (int i = 0; i < 4; i++)
            {
                Vector3 c = Corner(box, i, _r + DetourMargin, p.Y);
                float len = Flat(p, c) + Flat(c, tgt);
                if (FirstBlocker(c, tgt, single, 0) >= 0) len += 1000f;                       // still behind it from that corner
                if (FirstBlocker(p, c, single, 0) >= 0) len += Math.Max(box.max.X - box.min.X, box.max.Z - box.min.Z);   // the far side
                if (len < bestLen) { bestLen = len; best = i; }
            }
            return best;
        }

        static float Flat(Vector3 a, Vector3 b) => (float)Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z));

        static bool Finite(Vector3 v) => !(float.IsNaN(v.X) || float.IsInfinity(v.X) || float.IsNaN(v.Y) || float.IsInfinity(v.Y) || float.IsNaN(v.Z) || float.IsInfinity(v.Z));
    }
}
