using System;
using System.Collections.Generic;
using System.Numerics;
using JamKit.Movement;

namespace JamKit.Skyline
{
    /// <summary>Stands where it is and shoots back (the sim aims for it): the "standing still" check.</summary>
    public sealed class StandAndShootBot : IFightBot
    {
        public FightInput Next(FightSim fight, float h) => new FightInput { move = new MoveInput { yaw = fight.Player.Yaw } };
    }

    /// <summary>How a <see cref="TacticalBot"/> times things. <see cref="Human"/> is the gate (Task 11b fix round): seeded
    /// peeks of 1.0-1.8 s, hides of 2-4 s, a 0.15-0.25 s reaction to what it can see (a beam starting, a beam locking, a
    /// floor-care telegraph), and no reading of any bot's internal clock. <see cref="Perfect"/> is the reference: the
    /// drones' own numbers (a 1 s peek, 3.2 s in cover), no reaction delay, and it reads the brains' clocks.</summary>
    public sealed class FightTiming
    {
        public bool readsClocks;
        public float peekMin = 1f, peekMax = 1.8f, hideMin = 2f, hideMax = 4f, reactMin = 0.15f, reactMax = 0.25f;
        /// <summary>Plating lost in one peek that sends it back to cover (after its reaction delay).</summary>
        public float duckDamage = 15f;
        /// <summary>How far (m) either side of its peek point it strafes while a shooter sees it, flipping every 0.6-0.9 s. The
        /// drones aim at where you were 0.2 s ago (the creator, 2026-10-14), so a peeker who stands still is hit every time and one
        /// who moves across their fire is missed; a skilled player moves. 0: it stands.</summary>
        public float peekStrafe = 3f;

        public static FightTiming Human() => new FightTiming();

        public static FightTiming Perfect() => new FightTiming
        {
            readsClocks = true, peekMin = 1f, peekMax = 1f, hideMin = 3.2f, hideMax = 3.2f, reactMin = 0f, reactMax = 0f, duckDamage = 10f,
        };
    }

    /// <summary>
    /// The skilled line (Task 11b; the creator: split the groups with cover, take them a few at a time). No script: it reads
    /// the fight as it stands. It hides at the nearest floor point that no live drone or spotlight sees, even with its eye
    /// raised 2 m by a jump; it peeks from the nearby point seen by the fewest shooters (so it takes one group, or part of
    /// one, at a time), shoots for a peek's length, and goes back to cover, reloading there. Shooters drift to see where they
    /// last saw it, so a hide that becomes seen is left for another. It strips fresh wrecks near it while the plating has
    /// room and the wreck is in sight of at most one shooter; it ducks when a beam starts on it and sidesteps when one
    /// locks; it jumps the acid. With no shooter left it walks toward whatever still stands.
    /// <para>What it knows that a player doesn't quite: it plans with the sim's exact sight lines (which floor points each shooter
    /// sees), and it reads each shooter's exact "sees the player" flag for its exposure timing and duck triggers, including
    /// shooters behind it, and each floor-care unit's telegraph without a visibility check. Its timing is human; its
    /// knowledge of the map is not.</para>
    /// <para>With <c>useCover = false</c> it is the negative control: it never hides or ducks, and it presses the fight
    /// across open floor (3 m or more from any piece), closing on the nearest shooter (it still jumps the acid).</para>
    /// </summary>
    public sealed class TacticalBot : IFightBot
    {
        public enum Phase { Hide, Peek, Strip, Hunt }

        /// <summary>How far (m) it looks for a hide or a peek point.</summary>
        public const float HideRadius = 12f, PeekRadius = 8f;
        /// <summary>A hide must stay unseen with the eye this much higher (m): a double jump over the acid.</summary>
        public const float JumpEyeLift = 2f;
        /// <summary>The control fights only from floor at least this far (m) from any cover piece (the open stand's rule).</summary>
        public const float OpenFloorClearance = 3f;
        List<CoverBox> _floorCover;
        const float Grid = 1.5f, Arrive = 0.5f;

        readonly FightTiming _t;
        readonly Rng _rng;
        readonly bool _useCover;
        List<Vector2> _grid;
        Phase _phase = Phase.Hide;
        Vector2 _spot;
        bool _hasSpot;
        Vector2 _huntSpot;
        float _tryClock;
        float _huntAge, _nextHidePick;
        bool _hasHunt;
        float _huntClock;
        float _timer, _len, _exposed, _platingAtPeek, _idle, _checkClock, _duckAt = -1f, _dodgeUntil = -1f, _dodgeAt = -1f;
        Vector2 _dodgeDir;
        float _strafeClock;
        float _strafeSign = 1f;
        bool[] _beamActive = new bool[0], _beamLocked = new bool[0], _telegraphing = new bool[0];
        float[] _telegraphSeen = new float[0], _telegraphReact = new float[0];

        /// <summary>Peek points that gave it nothing to shoot (a grazing line), each left alone until its time.</summary>
        readonly List<KeyValuePair<Vector2, float>> _badPeeks = new List<KeyValuePair<Vector2, float>>();
        float _peekAge;

        void MarkBadPeek(Vector2 p, float until)
        {
            _badPeeks.RemoveAll(kv => kv.Value <= until - 5f);
            _badPeeks.Add(new KeyValuePair<Vector2, float>(p, until));
        }

        bool IsBadPeek(Vector2 p, float now)
        {
            foreach (var kv in _badPeeks) if (now < kv.Value && Vector2.Distance(p, kv.Key) < 1f) return true;
            return false;
        }

        public Phase Current => _phase;
        /// <summary>Called with each hide it picks: the point, and whether it judged it unseen by every live shooter (eye raised included).</summary>
        public Action<Vector2, bool> HidePicked;

        public TacticalBot(ulong seed, FightTiming timing = null, bool useCover = true)
        {
            _t = timing ?? FightTiming.Human();
            _rng = new Rng(seed, 0xB0B5UL);
            _useCover = useCover;
            _len = Draw(_t.hideMin, _t.hideMax) * 0.5f;   // it comes in from the entry looking for cover first
        }

        float Draw(float a, float b) => b > a ? _rng.Range(a, b) : a;
        float React() => Draw(_t.reactMin, _t.reactMax);

        static Vector2 Flat(Vector3 v) => new Vector2(v.X, v.Z);
        static Vector3 Feet(Vector2 p) => new Vector3(p.X, 0f, p.Y);

        public FightInput Next(FightSim f, float h)
        {
            if (_grid == null) _grid = BuildGrid(f);
            if (_floorCover == null) { _floorCover = new List<CoverBox>(); foreach (var b in f.Cover) if (b.min.Y <= 0.5f) _floorCover.Add(b); }
            int n = f.Enemies.Count;
            if (_beamActive.Length != n)
            {
                _beamActive = new bool[n]; _beamLocked = new bool[n]; _telegraphing = new bool[n]; _telegraphSeen = new float[n]; _telegraphReact = new float[n];
            }
            var input = new FightInput();
            Vector2 me = Flat(f.Player.Position);
            WatchBeams(f);
            int mag = f.Gun.Def.weapon.magazineSize;
            bool anyShooter = false;
            foreach (var e in f.Enemies) if (FightSim.IsShooter(e)) { anyShooter = true; break; }
            if (!anyShooter && _phase != Phase.Strip) _phase = Phase.Hunt;

            Vector2 goal = me;
            for (int guard = 0; guard < 4; guard++)
            {
                if (_phase == Phase.Hide)
                {
                    _checkClock -= h;
                    if (!_hasSpot || _checkClock <= 0f)
                    {
                        _checkClock = 0.25f;
                        if (!_hasSpot || (f.Time >= _nextHidePick && !Unseen(f, _spot, JumpEyeLift)))
                        {
                            _hasSpot = PickHide(f, me, out _spot, out bool cleanHide);
                            HidePicked?.Invoke(_spot, cleanHide);
                            _nextHidePick = f.Time + 1f;   // a found hide is looked for again at most once a second
                        }
                    }
                    if (!_useCover) { _phase = Phase.Peek; StartPeek(f, me); continue; }
                    goal = _hasSpot ? _spot : me;
                    bool there = Vector2.Distance(me, goal) < Arrive;
                    _timer += h;   // counts from the decision: a hide it can't quite reach never strands it
                    input.reload = f.Gun.Ammo < mag && f.Target == null;
                    _tryClock -= h;
                    if (_timer >= _len && f.Gun.Ammo == mag && !f.Gun.State.IsReloading && _tryClock <= 0f)
                    {
                        _tryClock = 0.25f;
                        if (TryStrip(f, me)) continue;
                        if (StartPeek(f, me)) continue;
                        // nothing to shoot from near here: move toward the nearest bot, then look again
                        if (PickHunt(f, me, out _huntSpot)) { _phase = Phase.Hunt; _huntAge = 0f; _huntClock = 0.5f; continue; }
                    }
                    break;
                }
                if (_phase == Phase.Peek)
                {
                    goal = _spot;
                    bool seen = false;
                    foreach (var e in f.Enemies) if (FightSim.IsShooter(e) && e.SeesPlayer) { seen = true; break; }
                    if (seen) _exposed += h;
                    _peekAge += h;
                    if (_exposed <= 0f && _peekAge > 2.5f)
                    {
                        // no shooter has seen it here: a dud of a peek point (a grazing line); leave it alone for a while
                        MarkBadPeek(_spot, f.Time + 5f);
                        if (!StartPeek(f, me)) { if (_useCover) { GoHide(); _timer = _len; } }
                        continue;
                    }
                    bool there = Vector2.Distance(me, goal) < Arrive;
                    _idle = there && f.Target == null ? _idle + h : 0f;
                    if (_useCover && _duckAt < 0f && _platingAtPeek - f.Armor.Plating >= _t.duckDamage) _duckAt = f.Time + React();
                    bool duck = _duckAt >= 0f && f.Time >= _duckAt;
                    if (_useCover && (_exposed >= _len || f.Gun.Ammo == 0 || duck))
                    {
                        GoHide();
                        continue;
                    }
                    if (_idle >= 0.5f)
                    {
                        _idle = 0f;
                        MarkBadPeek(_spot, f.Time + 5f);
                        if (!StartPeek(f, me)) { if (_useCover) { GoHide(); continue; } }
                    }
                    break;
                }
                if (_phase == Phase.Strip)
                {
                    var w = NearestFresh(f, me, 12f);
                    if (w == null || f.Armor.Plating >= f.Armor.PlatingMax - 1f) { GoHide(); _timer = _len; continue; }
                    goal = Flat(w.Position);
                    break;
                }
                // Hunt
                _huntAge += h;
                if (anyShooter)
                {
                    // push toward the nearest bot, looking for a peek every half second; no going back to the old hide
                    _huntClock -= h;
                    if (_huntClock <= 0f || Vector2.Distance(me, _huntSpot) < Arrive)
                    {
                        _huntClock = 0.5f;
                        if (StartPeek(f, me)) continue;
                        if (!PickHunt(f, me, out _huntSpot)) { GoHide(); continue; }
                    }
                    goal = _huntSpot;
                    break;
                }
                var fresh = NearestFresh(f, me, 12f);
                if (fresh != null && f.Armor.Plating < f.Armor.PlatingMax - 5f) { goal = Flat(fresh.Position); break; }
                _huntClock -= h;
                if (_huntClock <= 0f) { _huntClock = 0.5f; _hasHunt = PickHunt(f, me, out _huntSpot, true); }
                if (f.Target == null && _hasHunt) goal = _huntSpot;
                break;
            }

            if (_dodgeAt >= 0f && f.Time >= _dodgeAt)
            {
                _dodgeAt = -1f;
                _dodgeUntil = f.Time + 0.35f;
            }
            if (_phase == Phase.Peek && _useCover && _t.peekStrafe > 0f && StrafeGoal(f, me, h, out Vector2 strafeTo)) goal = strafeTo;
            input.move = f.Time < _dodgeUntil ? MoveTo(f, me, me + _dodgeDir * 5f) : MoveTo(f, me, goal);
            JumpTheAcid(f, ref input.move);
            return input;
        }

        /// <summary>While a shooter sees the peeking bot at its spot: a point to one side of the spot, across that shooter's line, the
        /// side flipping now and then (the walking strafe that makes the drones' lagged aim miss).</summary>
        bool StrafeGoal(FightSim f, Vector2 me, float h, out Vector2 to)
        {
            to = _spot;
            if (Vector2.Distance(me, _spot) > _t.peekStrafe + 1f) return false;
            FightEnemy seer = null;
            foreach (var e in f.Enemies) if (FightSim.IsShooter(e) && e.SeesPlayer && e.Def.aimLagSeconds > 0f) { seer = e; break; }   // only a lagging drone is strafed against
            if (seer == null) return false;
            _strafeClock -= h;
            if (_strafeClock <= 0f)
            {
                _strafeClock = Draw(0.6f, 0.9f);
                _strafeSign = -_strafeSign;
            }
            Vector3 d = f.Player.Position - seer.Position;
            var side = new Vector2(d.Z, -d.X);
            if (side.LengthSquared() < 1e-6f) return false;
            to = _spot + Vector2.Normalize(side) * (_strafeSign * _t.peekStrafe);
            return true;
        }

        void GoHide()
        {
            _phase = Phase.Hide;
            _timer = 0f;
            _len = Draw(_t.hideMin, _t.hideMax);
            _hasSpot = false;
            _duckAt = -1f;
        }

        bool StartPeek(FightSim f, Vector2 me)
        {
            if (!PickPeek(f, me, out var p)) return false;
            _phase = Phase.Peek;
            _spot = p;
            _hasSpot = true;
            _exposed = 0f;
            _idle = 0f;
            _len = Draw(_t.peekMin, _t.peekMax);
            _peekAge = 0f;
            _platingAtPeek = f.Armor.Plating;
            _duckAt = -1f;
            return true;
        }

        bool TryStrip(FightSim f, Vector2 me)
        {
            if (f.Armor.Plating > f.Armor.PlatingMax - 10f) return false;
            var w = NearestFresh(f, me, 10f);
            if (w == null || SeenCount(f, Flat(w.Position), 0f) > 1) return false;
            _phase = Phase.Strip;
            return true;
        }

        FightWreck NearestFresh(FightSim f, Vector2 me, float within)
        {
            FightWreck best = null;
            float bestD = within;
            foreach (var w in f.Wrecks)
            {
                if (!w.Wreck.Fresh(f.Time)) continue;
                float d = Vector2.Distance(me, Flat(w.Position));
                if (d < bestD) { bestD = d; best = w; }
            }
            return best;
        }

        /// <summary>How many live shooters see a player standing at <paramref name="p"/> (with the eye also raised by <paramref name="lift"/>).</summary>
        static int SeenCount(FightSim f, Vector2 p, float lift)
        {
            int c = 0;
            var feet = Feet(p);
            foreach (var e in f.Enemies) if (FightSim.IsShooter(e) && f.CanSee(e, feet, lift)) c++;
            return c;
        }

        /// <summary>Whether no live shooter sees a player standing at <paramref name="p"/> (nor its eye raised by <paramref name="lift"/>).</summary>
        static bool Unseen(FightSim f, Vector2 p, float lift) => Unseen(f, p, lift, 0f);

        /// <summary>As <see cref="Unseen(FightSim, Vector2, float)"/>, and also from <paramref name="slack"/> m either way of each
        /// shooter that searches (drifts), on both floor axes: a deep hide that a drone's first few metres of searching won't find.</summary>
        static bool Unseen(FightSim f, Vector2 p, float lift, float slack)
        {
            var feet = Feet(p);
            foreach (var e in f.Enemies)
            {
                if (!FightSim.IsShooter(e)) continue;
                if (f.CanSee(e, feet, lift)) return false;
                if (!(slack > 0f) || !(e.Def.searchSpeed > 0f)) continue;
                foreach (var o in Offsets)
                    if (f.CanSeeFrom(e, e.Position + o * slack, feet, lift)) return false;
            }
            return true;
        }

        static readonly Vector3[] Offsets = { new Vector3(1f, 0f, 0f), new Vector3(-1f, 0f, 0f), new Vector3(0f, 0f, 1f), new Vector3(0f, 0f, -1f) };

        /// <summary>How far (m) a searching drone may drift before a deep hide counts as found.</summary>
        public const float DeepHideSlack = 3f;

        readonly List<KeyValuePair<float, Vector2>> _near = new List<KeyValuePair<float, Vector2>>();

        void Near(Vector2 me, float radius)
        {
            _near.Clear();
            foreach (var c in _grid)
            {
                float d = Vector2.Distance(me, c);
                if (d <= radius) _near.Add(new KeyValuePair<float, Vector2>(d, c));
            }
            _near.Sort((a, b) => a.Key.CompareTo(b.Key));
        }

        /// <summary>The nearest floor point (within <see cref="HideRadius"/>, reachable in a straight line) no live shooter sees,
        /// even with the eye raised by a jump; failing that, the nearby point the fewest see.</summary>
        public bool PickHide(FightSim f, Vector2 me, out Vector2 spot) => PickHide(f, me, out spot, out _);

        /// <param name="unseen">True when the hide is unseen by every live shooter (eye raised included); false when it is only the
        /// nearby point the fewest see (no clean hide within reach).</param>
        public bool PickHide(FightSim f, Vector2 me, out Vector2 spot, out bool unseen)
        {
            unseen = true;
            if (_grid == null) _grid = BuildGrid(f);
            spot = me;
            Near(me, HideRadius);
            for (int i = 0; i < _near.Count && i < 30; i++)
            {
                var c = _near[i].Value;
                if (Unseen(f, c, JumpEyeLift) && Unseen(f, c, JumpEyeLift, DeepHideSlack) && Reachable(f, me, c)) { spot = c; return true; }
            }
            foreach (var kv in _near)
                if (Unseen(f, kv.Value, JumpEyeLift) && Reachable(f, me, kv.Value)) { spot = kv.Value; return true; }
            unseen = false;
            int best = int.MaxValue;
            for (int i = 0; i < _near.Count && i < 20; i++)
            {
                int seen = SeenCount(f, _near[i].Value, 0f);
                if (seen < best && Reachable(f, me, _near[i].Value)) { best = seen; spot = _near[i].Value; }
            }
            return best != int.MaxValue;
        }

        /// <summary>The nearby point (within <see cref="PeekRadius"/>) seen by the fewest live shooters, at least one, that it can
        /// shoot one of them from; nearer is better.</summary>
        bool PickPeek(FightSim f, Vector2 me, out Vector2 spot)
        {
            spot = me;
            float best = float.PositiveInfinity;
            Near(me, PeekRadius);
            foreach (var kv in _near)
            {
                float d = kv.Key;
                if (_useCover && d + 6f >= best) break;   // sorted by distance: nothing further can beat it
                int seen = 0;
                bool shot = false;
                var feet = Feet(kv.Value);
                foreach (var e in f.Enemies)
                {
                    if (!FightSim.IsShooter(e) || !f.CanSee(e, feet)) continue;
                    seen++;
                    if (!shot && f.CanShoot(feet, e)) shot = true;
                }
                if (seen == 0 || !shot) continue;
                // the control doesn't use cover to choose either: it presses the fight, closing on the nearest shooter across open floor
                float score = _useCover ? seen * 6f + d : NearestShooter(f, kv.Value);
                if (IsBadPeek(kv.Value, f.Time)) continue;
                if (!_useCover && CoverGeometry.DistanceToCover(Feet(kv.Value), _floorCover) < OpenFloorClearance) continue;   // the control fights only from open floor
                if (score < best && Reachable(f, me, kv.Value)) { best = score; spot = kv.Value; }
            }
            return !float.IsPositiveInfinity(best);
        }

        static float NearestShooter(FightSim f, Vector2 p)
        {
            float best = float.PositiveInfinity;
            foreach (var e in f.Enemies) if (FightSim.IsShooter(e)) best = Math.Min(best, Vector2.Distance(p, Flat(e.Position)));
            return best;
        }

        /// <summary>A step toward the nearest live bot: the reachable grid point within 9 m closest to it.</summary>
        bool PickHunt(FightSim f, Vector2 me, out Vector2 spot, bool mopUp = false)
        {
            spot = me;
            FightEnemy near = null;
            float nd = float.PositiveInfinity;
            foreach (var e in f.Enemies)
            {
                if (e.Wrecked) continue;
                float d = Vector2.Distance(me, Flat(e.Position));
                if (d < nd) { nd = d; near = e; }
            }
            if (near == null) return false;
            Vector2 target = Flat(near.Position);
            // the mop-up (no shooter left, only floor-care units and gondolas): a unit it can neither walk to nor shoot from
            // here is gone round by the grid's way, not chased into the piece between them
            if (mopUp && !Reachable(f, me, target) && !f.CanShoot(Feet(me), near) && Detour(f, me, target, near, out spot)) return true;
            float best = Vector2.Distance(me, target) - 0.5f;
            bool found = false;
            foreach (var c in _grid)
            {
                if (Vector2.Distance(me, c) > 9f || !Reachable(f, me, c)) continue;
                float d = Vector2.Distance(c, target);
                if (d < best) { best = d; spot = c; found = true; }
            }
            if (found) return true;
            // a local minimum: the bot is just behind a piece (a floor-care unit pressed to the far side of the booth's wall,
            // the line inside the booth's U). Go round: a breadth-first walk over the grid to the nearest cell from which the
            // bot can be walked to or shot, and the furthest cell along that way it can walk to straight.
            return Detour(f, me, target, near, out spot);
        }

        bool Detour(FightSim f, Vector2 me, Vector2 target, FightEnemy near, out Vector2 spot)
        {
            spot = me;
            int n = _grid.Count;
            var from = new int[n];
            for (int i = 0; i < n; i++) from[i] = -2;   // -2 unvisited, -1 a start cell
            var queue = new Queue<int>();
            for (int i = 0; i < n; i++)
                if (Vector2.Distance(me, _grid[i]) <= Grid * 1.5f && Reachable(f, me, _grid[i])) { from[i] = -1; queue.Enqueue(i); }
            int goal = -1;
            while (queue.Count > 0 && goal < 0)
            {
                int i = queue.Dequeue();
                var c = _grid[i];
                if (Reachable(f, c, target) || f.CanShoot(Feet(c), near)) { goal = i; break; }
                for (int j = 0; j < n; j++)
                {
                    if (from[j] != -2) continue;
                    var d = _grid[j] - c;
                    if (Math.Abs(d.X) > Grid * 1.01f || Math.Abs(d.Y) > Grid * 1.01f || !Reachable(f, c, _grid[j])) continue;
                    from[j] = i;
                    queue.Enqueue(j);
                }
            }
            if (goal < 0) return false;
            // the furthest cell on the way back from the goal that is straight-reachable from here
            for (int k = goal; k >= 0; k = from[k])
                if (Reachable(f, me, _grid[k]) && Vector2.Distance(me, _grid[k]) >= Arrive) { spot = _grid[k]; return true; }
            return false;
        }

        static bool Reachable(FightSim f, Vector2 a, Vector2 b)
        {
            const float r = 0.45f;
            var grow = new Vector3(r, 0f, r);
            foreach (var box in f.Cover)
            {
                if (box.min.Y > 0.5f) continue;   // the deck is overhead
                var g = new CoverBox { min = new Vector3(box.min.X - r, -1f, box.min.Z - r), max = new Vector3(box.max.X + r, 1f, box.max.Z + r) };
                if (a.X > g.min.X && a.X < g.max.X && a.Y > g.min.Z && a.Y < g.max.Z)
                {
                    // already touching it: sliding along it is fine, but not a line through the piece itself (from a wall's
                    // inner face, the far side of the wall isn't reachable)
                    var solid = new CoverBox { min = new Vector3(box.min.X + 0.02f, -1f, box.min.Z + 0.02f), max = new Vector3(box.max.X - 0.02f, 1f, box.max.Z - 0.02f) };
                    if (CoverGeometry.SegmentHitsBox(new Vector3(a.X, 0f, a.Y), new Vector3(b.X, 0f, b.Y), solid)) return false;
                    continue;
                }
                if (CoverGeometry.SegmentHitsBox(new Vector3(a.X, 0f, a.Y), new Vector3(b.X, 0f, b.Y), g)) return false;
            }
            return true;
        }

        static List<Vector2> BuildGrid(FightSim f)
        {
            var floor = new List<CoverBox>();
            foreach (var b in f.Cover) if (b.min.Y <= 0.5f) floor.Add(b);
            var grid = new List<Vector2>();
            for (float x = f.Area.minX + 1f; x <= f.Area.maxX - 1f; x += Grid)
                for (float z = f.Area.minZ + 1f; z <= f.Area.maxZ - 1f; z += Grid)
                    if (CoverGeometry.DistanceToCover(new Vector3(x, 0f, z), floor) >= 0.7f) grid.Add(new Vector2(x, z));
            return grid;
        }

        static MoveInput MoveTo(FightSim f, Vector2 me, Vector2 goal)
        {
            Vector2 d = goal - me;
            float dist = d.Length();
            if (dist < 0.15f) return new MoveInput { yaw = f.Player.Yaw };
            float yaw = MathF.Atan2(d.X, d.Y) * (180f / MathF.PI);
            return new MoveInput { move = new Vector2(0f, Math.Min(1f, dist / 1.2f)), yaw = yaw, sprintHeld = dist > 2.5f };
        }

        /// <summary>Beams it can see: one starting on it makes it duck (after its reaction) if it is peeking; one locking makes it
        /// sidestep across the line of fire (after its reaction).</summary>
        void WatchBeams(FightSim f)
        {
            for (int i = 0; i < f.Enemies.Count; i++)
            {
                var e = f.Enemies[i];
                bool active = e.Beam != null && !e.Wrecked && e.Beam.Active, locked = active && e.Beam.Locked;
                if (active && !_beamActive[i] && e.SeesPlayer && _useCover && _phase == Phase.Peek) _duckAt = f.Time + React();
                if (locked && !_beamLocked[i] && e.SeesPlayer && _useCover)
                {
                    Vector3 to = f.Player.Position - e.Position;
                    var side = Vector2.Normalize(new Vector2(to.Z, -to.X) + new Vector2(1e-4f, 0f));
                    _dodgeDir = _rng.NextFloat() < 0.5f ? side : -side;
                    _dodgeAt = f.Time + React();
                }
                _beamActive[i] = active;
                _beamLocked[i] = locked;
            }
        }

        /// <summary>Jumps so its feet are above the acid jet as it passes. A human (no clock reading) times it from when it saw the
        /// telegraph start plus the telegraph's known length, and can't act before its reaction delay; a perfect bot reads the
        /// brain's clock. A double jump bridges two sweeps in a row.</summary>
        void JumpTheAcid(FightSim f, ref MoveInput m)
        {
            var p = f.Player.Position;
            float now = f.Time, soonest = float.PositiveInfinity, latest = float.NegativeInfinity;
            for (int i = 0; i < f.Enemies.Count; i++)
            {
                var e = f.Enemies[i];
                var d = e.Def;
                bool tele = !e.Wrecked && d.moveSpeed > 0f && e.Brain.State == BotState.Telegraph;
                if (tele && !_telegraphing[i]) { _telegraphSeen[i] = now; _telegraphReact[i] = React(); }
                _telegraphing[i] = tele;
                if (e.Wrecked || d.moveSpeed <= 0f) continue;
                float dx = p.X - e.Position.X, dz = p.Z - e.Position.Z;
                float flat = MathF.Sqrt(dx * dx + dz * dz);
                if (flat > d.sweepRadius + 1.5f) continue;
                float bearing = MathF.Atan2(dx, dz) * (180f / MathF.PI);
                float start, yaw, canAct;
                if (e.Sweeping)
                {
                    start = now - e.SweepTime;   // the jet is in plain sight: where it points shows how far it has swept
                    yaw = e.SweepYaw;
                    canAct = _t.readsClocks ? now : _telegraphSeen[i] + _telegraphReact[i];
                }
                else if (tele)
                {
                    start = _t.readsClocks ? now + Math.Max(0f, d.telegraphSeconds - e.Brain.StateTime) : _telegraphSeen[i] + d.telegraphSeconds;
                    yaw = bearing;
                    canAct = _t.readsClocks ? now : _telegraphSeen[i] + _telegraphReact[i];
                }
                else continue;
                float ws, we;
                if (flat < 0.8f) { ws = start; we = start + d.sweepSeconds; }
                else
                {
                    float rel = (bearing - (yaw - d.sweepArcDeg * 0.5f)) % 360f;
                    if (rel < 0f) rel += 360f;
                    float half = d.sweepWidthDeg * 0.5f + 10f;
                    if (rel > d.sweepArcDeg + half && rel < 360f - half) continue;
                    if (rel > 180f) rel -= 360f;
                    float tc = start + rel / d.sweepArcDeg * d.sweepSeconds, w = half / d.sweepArcDeg * d.sweepSeconds;
                    ws = tc - w;
                    we = tc + w;
                }
                if (we < now || now < canAct) continue;
                soonest = Math.Min(soonest, ws);
                latest = Math.Max(latest, we);
            }
            if (float.IsPositiveInfinity(soonest)) return;
            if (f.Player.Grounded)
            {
                if (soonest - now <= 0.1f) { m.jumpPressed = true; m.jumpHeld = true; }
            }
            else if (f.Player.DoubleJumpAvailable && f.Player.Velocity.Y < 0f && p.Y < 0.75f && latest > now + 0.05f)
            {
                m.jumpPressed = true;
                m.jumpHeld = true;
            }
        }
    }

    /// <summary>Drives a <see cref="FightSim"/> with one of the dodge harness's lane runners (<see cref="StraightLineBot"/>,
    /// <see cref="JinkingBot"/>); the sim shoots back for it as for any bot.</summary>
    public sealed class LaneRunnerBot : IFightBot
    {
        readonly IPlayerBot _runner;

        public LaneRunnerBot(IPlayerBot runner) { _runner = runner ?? throw new ArgumentNullException(nameof(runner)); }

        public FightInput Next(FightSim fight, float h) => new FightInput { move = _runner.Next(fight.Player, fight.Time, h) };
    }

    /// <summary>The dodge band measured in a real fight (the creator, 2026-10-13: every balance check simulates the player
    /// shooting back).</summary>
    public struct FightDodgeResult
    {
        /// <summary>Hits per second of exposure, pooled over the seeds. Exposure is live-shooter seconds with the runner in sight
        /// (a second facing two live drones counts two), so a runner that wrecks shooters sooner isn't credited for it.</summary>
        public float straightRate, jinkRate;
        public float straightHits, jinkHits, straightExposure, jinkExposure;
        public int straightZeroSeeds, jinkZeroSeeds, seeds;
        /// <summary>The ratio's 95% interval: a bootstrap over the seeds (1000 resamples, seeded).</summary>
        public float ratioLow, ratioHigh;
        public float Ratio => jinkRate > 0f ? straightRate / jinkRate : float.PositiveInfinity;
    }

    /// <summary>
    /// The dodge band in the fight sim. The straight-line and jinking runners each fight the same awake shooters on open floor,
    /// firing the Writ, until the shooters are wrecked or the span ends. The runners take no damage while measuring (hits are
    /// counted, not applied), so neither stops early. Three separate random sequences per seed: the enemies' aim error (both
    /// runs draw it in the same order), the player's hit rolls, and the jinker's reversals (<see cref="JinkStream"/>).
    /// </summary>
    public static class FightDodge
    {
        /// <summary>Mixed into the seed for the jinking runner's reversals, so they don't share a sequence with the enemies' aim error
        /// (which is the sim's own stream of that seed).</summary>
        public const ulong JinkStream = 0x9E3779B97F4A7C15UL;

        /// <summary>The point <paramref name="distance"/> m (on the floor plan) from the centre of <paramref name="group"/>,
        /// toward the hall's middle: the middle of the runners' lane for that group.</summary>
        public static Vector3 StandOff(EncounterDef e, int group, float distance)
        {
            Vector3 sum = Vector3.Zero;
            int n = 0;
            foreach (var s in e.spawns) if (s.group == group) { sum += s.position; n++; }
            if (n == 0) return e.anchor;
            var c = new Vector2(sum.X / n, sum.Z / n);
            var toMiddle = c.Length() > 1e-3f ? -Vector2.Normalize(c) : new Vector2(0f, -1f);
            var o = c + toMiddle * distance;
            return new Vector3(o.X, 0f, o.Y);
        }

        /// <summary>The encounter's drones and spotlights (all of them, or one group's) in the runners' frame. The runners run
        /// along the frame's z, 15 m either way of its origin, which is <paramref name="laneMiddle"/> in the hall. With
        /// <paramref name="laneAlongX"/> the lane runs along the hall's x instead (the frame turned a quarter turn), so it
        /// crosses a line of fire that comes from the north or south.</summary>
        public static EncounterDef Framed(EncounterDef e, Vector3 laneMiddle, bool laneAlongX, Func<string, EnemyDef> lookup = null, int group = -1)
        {
            lookup = lookup ?? SkylineEnemies.Find;
            var list = new List<Spawn>();
            foreach (var s in e.spawns)
            {
                var d = lookup(s.enemy);
                if (d == null || (d.aim != AimModel.LeadsYou && d.aim != AimModel.FiresAtWhereYouAre)) continue;
                if (group >= 0 && s.group != group) continue;
                Vector3 p = s.position - laneMiddle;
                if (laneAlongX) p = new Vector3(-p.Z, p.Y, p.X);   // the hall's +x becomes the frame's +z
                list.Add(new Spawn(s.enemy, p, 0));   // one group: awake together from the start
            }
            return new EncounterDef { id = e.id + (group >= 0 ? "/group " + group : "/shooters"), anchor = Vector3.Zero, spawns = list.ToArray() };
        }

        public static FightDodgeResult Run(EncounterDef framed, Func<IMoveWorld> makeWorld, float span, ulong seed, int seeds,
            DifficultyDef difficulty = null, Func<string, EnemyDef> lookup = null)
        {
            if (seeds < 1) seeds = 1;
            var r = new FightDodgeResult { seeds = seeds };
            var sh = new float[seeds]; var se = new float[seeds]; var jh = new float[seeds]; var je = new float[seeds];
            for (int i = 0; i < seeds; i++)
            {
                ulong s = seed + (ulong)i;
                var a = new FightSim(framed, makeWorld(), new LaneRunnerBot(new StraightLineBot()), null, Vector3.Zero, s, difficulty, lookup) { Invulnerable = true };
                var b = new FightSim(framed, makeWorld(), new LaneRunnerBot(new JinkingBot(s ^ JinkStream)), null, Vector3.Zero, s, difficulty, lookup) { Invulnerable = true };
                sh[i] = a.Run(span).hitsTaken; se[i] = a.ShooterSecondsInSight;
                jh[i] = b.Run(span).hitsTaken; je[i] = b.ShooterSecondsInSight;
                if (sh[i] == 0f) r.straightZeroSeeds++;
                if (jh[i] == 0f) r.jinkZeroSeeds++;
            }
            r.straightHits = Sum(sh); r.straightExposure = Sum(se); r.jinkHits = Sum(jh); r.jinkExposure = Sum(je);
            r.straightRate = r.straightExposure > 0f ? r.straightHits / r.straightExposure : 0f;
            r.jinkRate = r.jinkExposure > 0f ? r.jinkHits / r.jinkExposure : 0f;
            // the bootstrap: resample the seeds (pairs kept together), recompute the ratio of pooled rates
            var rng = new Rng(seed ^ 0xB00757A9UL);
            var ratios = new float[1000];
            for (int k = 0; k < ratios.Length; k++)
            {
                float a1 = 0f, a2 = 0f, b1 = 0f, b2 = 0f;
                for (int i = 0; i < seeds; i++)
                {
                    int j = rng.Range(0, seeds);
                    a1 += sh[j]; a2 += se[j]; b1 += jh[j]; b2 += je[j];
                }
                ratios[k] = b1 > 0f && a2 > 0f && b2 > 0f ? (a1 / a2) / (b1 / b2) : float.PositiveInfinity;
            }
            Array.Sort(ratios);
            r.ratioLow = ratios[(int)(ratios.Length * 0.025f)];
            r.ratioHigh = ratios[(int)(ratios.Length * 0.975f) - 1];
            return r;
        }

        static float Sum(float[] v)
        {
            float s = 0f;
            foreach (float x in v) s += x;
            return s;
        }
    }
}
