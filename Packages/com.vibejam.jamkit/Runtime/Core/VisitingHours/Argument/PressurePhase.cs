using System;
using System.Collections.Generic;
using System.Numerics;

namespace JamKit.Argument
{
    /// <summary>
    /// How hard one pressure phase is (doc 3.3). Heat (0..3) sets the base; anger (0..3, from antagonising replies)
    /// makes it noticeably harder: faster, denser, narrower gaps, and one gap instead of two. The Long Night's
    /// "scripted but beatable" setting is harder again: the gap only just fits Nadia's light.
    /// </summary>
    public struct PressureSpec
    {
        public int level;
        public int anger;
        public ArgumentDifficulty difficulty;
        public float duration;
        /// <summary>Hazard speed, surface units per second.</summary>
        public float speed;
        /// <summary>Seconds between spawns.</summary>
        public float interval;
        /// <summary>The free space in a wall or ring (surface units).</summary>
        public float gap;
        /// <summary>Gaps per wall or ring: 2 when calm, 1 once they're angry (and in the Long Night).</summary>
        public int gaps;
        /// <summary>False in Story mode: nothing hurts, slips collect themselves.</summary>
        public bool canHurt;
        /// <summary>Seconds every attack is shown as a harmless warning before it can hurt (the standing rule,
        /// <see cref="PressureRule"/>): 0.65 s on Normal, 1 s in Story.</summary>
        public float telegraph;
        /// <summary>The Long Night only: no telegraphs (its walls come in from outside the box, which the rule
        /// allows) and no fairness check on spawns ("scripted but beatable", proved by its own pilot).</summary>
        public bool exemptFromRule;
        public PressureShape[] shapes;

        /// <summary>The narrowest gap outside the Long Night: room for her light and 0.4 to spare (it was 0.12, which
        /// left a 0.16-wide lane to stop in while plates came at nearly 5 units a second: unfair). It only binds when
        /// heat and anger are both high; calmer gaps are wider anyway.</summary>
        public const float MinGapNormal = PressurePhase.LightRadius * 2f + 0.4f;
        public const float MinGapScripted = PressurePhase.LightRadius * 2f + 0.05f;
        public const float TelegraphNormal = 0.65f;
        public const float TelegraphStory = 1f;

        public static PressureSpec For(int level, int anger, ArgumentDifficulty difficulty, float speedSetting, bool painful, IList<PressureShape> shapes)
        {
            level = Clamp(painful ? 3 : level, 0, 3);
            anger = Clamp(anger, 0, 3);
            speedSetting = speedSetting <= 0f ? 1f : Math.Min(1f, Math.Max(0.5f, speedSetting));
            var s = new PressureSpec
            {
                level = level,
                anger = anger,
                difficulty = difficulty,
                duration = painful ? 8f : Math.Min(8f, 5f + level),
                speed = (1.5f + 0.35f * level) * (1f + 0.3f * anger) * speedSetting,
                interval = (1.5f - 0.22f * level) / (1f + 0.4f * anger),
                gap = Math.Max(MinGapNormal, (0.95f - 0.1f * level) * (1f - 0.15f * anger)),
                gaps = anger == 0 ? 2 : 1,
                canHurt = difficulty != ArgumentDifficulty.Story,
                telegraph = difficulty == ArgumentDifficulty.Story ? TelegraphStory : TelegraphNormal,
            };
            var list = new List<PressureShape>();
            if (shapes != null) foreach (var sh in shapes) list.Add(sh);
            if (list.Count == 0) list.Add(PressureShape.Wall);
            if (difficulty == ArgumentDifficulty.ScriptedButBeatable)
            {
                // Every phase at full heat, faster and twice as dense, with a gap that only just fits; walls only,
                // so a path always exists (each next gap is placed within reach: technically winnable).
                s.level = 3;
                s.duration = 8f;
                s.speed = (1.5f + 0.35f * 3f) * 1.6f * (1f + 0.15f * anger) * speedSetting;
                s.interval = (1.5f - 0.66f) * 0.5f / (1f + 0.15f * anger);
                s.gap = MinGapScripted;
                s.gaps = 1;
                s.telegraph = 0f;
                s.exemptFromRule = true;
                list.Clear();
                list.Add(PressureShape.Wall);
            }
            s.shapes = list.ToArray();
            return s;
        }

        static int Clamp(int v, int min, int max) => v < min ? min : v > max ? max : v;
    }

    /// <summary>
    /// The standing rule for every attack (the creator, 2026-10-12): an attack either comes in from outside the
    /// box, or is shown as a harmless warning (a telegraph) long enough to react to before it can hurt. The Long
    /// Night is the only exception, flagged by <see cref="PressureSpec.exemptFromRule"/>. Every spawn goes through
    /// <see cref="Admit"/>, which refuses an attack that starts inside the box without one.
    ///
    /// On top of the rule every spawn outside the Long Night must leave an escape: <see cref="PressurePhase"/>
    /// plans one (<see cref="DodgePlanner"/>) for a player slower than Nadia really is
    /// (<see cref="PlannerSpeed"/>), who notices it late (<see cref="Reaction"/>), and drops the attack if there's none.
    /// </summary>
    public static class PressureRule
    {
        /// <summary>The shortest telegraph an attack inside the box may have.</summary>
        public const float MinTelegraph = 0.5f;
        /// <summary>The time a player takes to notice a new attack (taken off its telegraph when checking it's fair).</summary>
        public const float Reaction = 0.25f;
        /// <summary>The share of Nadia's speed the fairness check allows the player.</summary>
        public const float PlannerSpeed = 0.8f;
        /// <summary>Extra room around her light the fairness check keeps clear (on top of half a step's move).</summary>
        public const float PlannerPad = 0.03f;
        /// <summary>The longest the fairness check looks ahead (seconds). It always looks until every attack has left
        /// the box (a slow ring takes over four seconds to sweep it, longer on the half-speed setting); this only caps it.</summary>
        public const float PlannerHorizon = 12f;
        /// <summary>The fairness check's grid spacing (surface units): coarse, so a check costs about a millisecond.</summary>
        public const float PlannerCell = 0.12f;
        /// <summary>The fairness check's time step: one diagonal grid move at <see cref="PlannerSpeed"/>.</summary>
        public const float PlannerStep = 0.071f;
        /// <summary>The fairness tests' player: a finer grid stepping every three 60 fps frames.</summary>
        public const float TestCell = 0.084f, TestStep = 0.05f;

        /// <summary>True if the attack may spawn: it starts outside the box, or its telegraph is long enough, or
        /// the phase is exempt (the Long Night).</summary>
        public static bool Admit(bool startsInsideBox, float telegraph, in PressureSpec spec)
        {
            if (!startsInsideBox || spec.exemptFromRule) return true;
            return telegraph >= MinTelegraph - 0.02f; // a tick's lateness is absorbed into the telegraph
        }
    }

    /// <summary>
    /// The attacks of a phase as they sweep through one stretch of time, flattened into plain arrays
    /// (<see cref="PressurePhase.Sweep"/>), so many spots can be tested against them quickly. A spot is hurt if a
    /// light held there would be touched at any moment of the stretch. Arrays grow as needed (not per test).
    /// </summary>
    public sealed class PressureSweep
    {
        int _walls, _rings, _blobs;
        float _gapFree;
        float[] _wx0 = new float[16], _wx1 = new float[16], _wg0 = new float[16], _wg1 = new float[16];
        int[] _wgc = new int[16];
        Vector2[] _rc = new Vector2[8], _e0a = new Vector2[8], _e0b = new Vector2[8], _e1a = new Vector2[8], _e1b = new Vector2[8];
        float[] _rlo = new float[8], _rhi = new float[8];
        int[] _rgc = new int[8];
        Vector2[] _ba = new Vector2[16], _bs = new Vector2[16];

        public int Count => _walls + _rings + _blobs;

        internal void Begin(float gapFree)
        {
            _walls = _rings = _blobs = 0;
            _gapFree = gapFree;
        }

        internal void AddWall(float x0, float x1, float g0, float g1, int gaps)
        {
            if (_walls == _wx0.Length)
            {
                int n = _walls * 2;
                Array.Resize(ref _wx0, n); Array.Resize(ref _wx1, n); Array.Resize(ref _wg0, n); Array.Resize(ref _wg1, n); Array.Resize(ref _wgc, n);
            }
            _wx0[_walls] = Math.Min(x0, x1);
            _wx1[_walls] = Math.Max(x0, x1);
            _wg0[_walls] = g0;
            _wg1[_walls] = g1;
            _wgc[_walls] = gaps;
            _walls++;
        }

        internal void AddRing(Vector2 c, float lo, float hi, Vector2 e0a, Vector2 e0b, Vector2 e1a, Vector2 e1b, int gaps)
        {
            if (_rings == _rc.Length)
            {
                int n = _rings * 2;
                Array.Resize(ref _rc, n); Array.Resize(ref _e0a, n); Array.Resize(ref _e0b, n); Array.Resize(ref _e1a, n); Array.Resize(ref _e1b, n);
                Array.Resize(ref _rlo, n); Array.Resize(ref _rhi, n); Array.Resize(ref _rgc, n);
            }
            _rc[_rings] = c; _rlo[_rings] = lo; _rhi[_rings] = hi;
            _e0a[_rings] = e0a; _e0b[_rings] = e0b; _e1a[_rings] = e1a; _e1b[_rings] = e1b;
            _rgc[_rings] = gaps;
            _rings++;
        }

        internal void AddBlob(Vector2 a, Vector2 seg)
        {
            if (_blobs == _ba.Length)
            {
                int n = _blobs * 2;
                Array.Resize(ref _ba, n); Array.Resize(ref _bs, n);
            }
            _ba[_blobs] = a;
            _bs[_blobs] = seg;
            _blobs++;
        }

        /// <summary>True if a light held at <paramref name="p"/> would be touched (with <paramref name="pad"/> extra room).</summary>
        public bool Hurts(Vector2 p, float pad)
        {
            float wallReach = PressurePhase.PlateRadius + PressurePhase.LightRadius + pad;
            float free = _gapFree - pad;
            for (int i = 0; i < _walls; i++)
            {
                float near = p.X < _wx0[i] ? _wx0[i] : p.X > _wx1[i] ? _wx1[i] : p.X;
                if (Math.Abs(p.X - near) >= wallReach) continue;
                if (Math.Abs(p.Y - _wg0[i]) <= free) continue;
                if (_wgc[i] > 1 && Math.Abs(p.Y - _wg1[i]) <= free) continue;
                return true;
            }
            float band = PressurePhase.RingThickness * 0.5f + PressurePhase.LightRadius + pad;
            for (int i = 0; i < _rings; i++)
            {
                var d = p - _rc[i];
                float dist = d.Length();
                float near = dist < _rlo[i] ? _rlo[i] : dist > _rhi[i] ? _rhi[i] : dist;
                if (Math.Abs(dist - near) >= band) continue;
                if (Cross(_e0a[i], d) >= pad && Cross(d, _e0b[i]) >= pad) continue;
                if (_rgc[i] > 1 && Cross(_e1a[i], d) >= pad && Cross(d, _e1b[i]) >= pad) continue;
                return true;
            }
            float blob = PressurePhase.BlobRadius + PressurePhase.LightRadius + pad;
            for (int i = 0; i < _blobs; i++)
            {
                var a = _ba[i];
                var seg = _bs[i];
                float len2 = seg.LengthSquared();
                float u = len2 > 1e-8f ? Math.Max(0f, Math.Min(1f, Vector2.Dot(p - a, seg) / len2)) : 0f;
                if (Vector2.DistanceSquared(a + seg * u, p) < blob * blob) return true;
            }
            return false;
        }

        static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;
    }

    /// <summary>A slip waiting in a phase: the hidden premise it reveals and how it looks.</summary>
    public struct SlipSeed
    {
        public string premise;
        public string name;
    }

    /// <summary>
    /// One pressure phase on the arena's surface (doc 3.3): Nadia's presence (a small light) dodges the opponent's
    /// feeling for 5–8 s. Walls of plates slide across with gaps, flour rings bloom out with gaps, batter is lobbed
    /// at her. Every attack is first shown as a harmless warning (<see cref="PressureSpec.telegraph"/>: <c>warn</c>
    /// on each hazard) and only spawns if an escape exists (<see cref="PressureRule"/>). Hits raise her heart rate
    /// (the engine counts them); catching a slip reveals what they aren't saying.
    /// Surface units: x -HalfWidth..HalfWidth, y -HalfHeight..HalfHeight (y up). Nothing allocated per tick.
    /// </summary>
    public sealed class PressurePhase
    {
        public const float HalfWidth = 3f;
        public const float HalfHeight = 1.6f;
        public const float LightRadius = 0.1f;
        public const float LightSpeed = 3f;
        public const float PlateRadius = 0.15f;
        public const float RingThickness = 0.16f;
        public const float BlobRadius = 0.13f;
        public const float SlipRadius = 0.17f;
        public const float Invulnerability = 0.9f;
        /// <summary>Story mode: a slip collects itself after this long.</summary>
        public const float SlipAutoCollect = 3f;
        /// <summary>A ring never blooms closer than this to her.</summary>
        public const float RingMinDistance = 1f;
        const float EntryMargin = 0.4f;
        const int SpawnTries = 4;

        /// <summary>A column of plates sliding across, with its gaps (centre y; free space <see cref="PressureSpec.gap"/>).
        /// While <c>warn</c> &gt; 0 it waits outside the box, harmless (drawn as a warning at the edge).</summary>
        public struct Wall
        {
            public float x, vx;
            public float gap0, gap1;
            public int gapCount;
            public float warn, age;
            public int serial;
            public bool active;
        }

        /// <summary>A flour ring blooming out from a point, with its gaps (directions in radians). While <c>warn</c>
        /// &gt; 0 it hasn't started (radius 0), harmless (drawn as a ghost with its gaps).</summary>
        public struct Ring
        {
            public Vector2 center;
            public float radius, growth;
            public float gapAngle0, gapAngle1, gapHalfAngle;
            public int gapCount;
            public float warn, age;
            public int serial;
            public bool active;
            // Each gap's two edges as unit directions (cheap collision tests: inside a gap = between its edges).
            public Vector2 edge0a, edge0b, edge1a, edge1b;
        }

        /// <summary>A lob of batter. While <c>warn</c> &gt; 0 it waits outside the box (drawn as its path).</summary>
        public struct Blob
        {
            public Vector2 position, velocity;
            public float warn, age;
            public int serial;
            public bool active;
        }

        public struct Slip
        {
            public Vector2 position, velocity;
            public string premise, name;
            public float age;
            public bool active, caught;
        }

        readonly PressureSpec _spec;
        readonly Rng _rng;
        readonly List<Wall> _walls = new List<Wall>(32);
        readonly List<Ring> _rings = new List<Ring>(16);
        readonly List<Blob> _blobs = new List<Blob>(32);
        readonly List<Slip> _slips = new List<Slip>(2);
        readonly SlipSeed[] _slipSeeds;
        readonly DodgePlanner _check;
        // One fairness check shared by every phase (its buffers are large; phases run one at a time).
        static DodgePlanner _sharedCheck;
        readonly PressureSweep _reactSweep = new PressureSweep();
        int _slipsSpawned;
        float _time, _nextSpawn, _invulnerable;
        int _hits, _hitsTaken, _spawned, _skipped, _refused, _serial, _touches, _telegraphs;
        bool _touching;
        Vector2 _light, _lightVelocity;
        string _caught;
        // The Long Night's reachability: the last wall's gap and when it crosses x = 0.
        float _lastGapY, _lastArrival = -10f;

        public PressurePhase(PressureSpec spec, ulong seed, IList<SlipSeed> slips)
        {
            _spec = spec;
            _rng = new Rng(seed == 0 ? 1UL : seed);
            int n = slips != null ? Math.Min(2, slips.Count) : 0;
            _slipSeeds = new SlipSeed[n];
            for (int i = 0; i < n; i++) _slipSeeds[i] = slips[i];
            _nextSpawn = 0.6f;
            if (!spec.exemptFromRule)
                _check = _sharedCheck ?? (_sharedCheck = new DodgePlanner(PressureRule.PlannerCell, PressureRule.PlannerStep, PressureRule.PlannerHorizon, LightSpeed * PressureRule.PlannerSpeed, PressureRule.PlannerPad, true));
        }

        public PressureSpec Spec => _spec;
        public float Time => _time;
        public float Duration => _spec.duration;
        public bool Finished => _time >= _spec.duration;
        public Vector2 Light => _light;
        public float Invulnerable => _invulnerable;
        /// <summary>Hits in this phase so far.</summary>
        public int Hits => _hits;
        /// <summary>Times her light touched an attack, counted even when it can't hurt (Story, just hit): tests.</summary>
        public int Touches => _touches;
        /// <summary>Walls, rings and blobs spawned so far (density, for tests).</summary>
        public int Spawned => _spawned;
        /// <summary>Spawns dropped because no escape could be found for them (fairness).</summary>
        public int Skipped => _skipped;
        /// <summary>Spawns <see cref="PressureRule.Admit"/> refused (a bug if ever non-zero).</summary>
        public int Refused => _refused;
        /// <summary>Warnings shown so far (the host plays a sound for each new one).</summary>
        public int Telegraphs => _telegraphs;
        public IReadOnlyList<Wall> Walls => _walls;
        public IReadOnlyList<Ring> Rings => _rings;
        public IReadOnlyList<Blob> Blobs => _blobs;
        public IReadOnlyList<Slip> Slips => _slips;

        /// <summary>Places her light (tests start it in any spot; the engine starts it in the middle).</summary>
        public void PlaceLight(Vector2 p)
        {
            p.X = Math.Max(-HalfWidth + LightRadius, Math.Min(HalfWidth - LightRadius, p.X));
            p.Y = Math.Max(-HalfHeight + LightRadius, Math.Min(HalfHeight - LightRadius, p.Y));
            _light = p;
        }

        /// <summary>Hits since the last call (the engine turns each into a heart-rate step).</summary>
        public int TakeHits()
        {
            int n = _hits - _hitsTaken;
            _hitsTaken = _hits;
            return n;
        }

        /// <summary>The premise of a slip caught since the last call, or null.</summary>
        public string TakeCaughtSlip()
        {
            string s = _caught;
            _caught = null;
            return s;
        }

        /// <summary>Moves the light by <paramref name="input"/> (-1..1 each axis) and everything else by dt.</summary>
        public void Tick(float dt, Vector2 input)
        {
            if (Finished || dt <= 0f) return;
            _time += dt;
            if (_invulnerable > 0f) _invulnerable -= dt;

            if (input.LengthSquared() > 1f) input = Vector2.Normalize(input);
            var before = _light;
            _light += input * (LightSpeed * dt);
            PlaceLight(_light);
            _lightVelocity = (_light - before) / dt;

            // The Long Night spawns first (as it always has); everything else after the attacks have moved, so the
            // fairness check sees them where they really are.
            if (_spec.exemptFromRule) SpawnDue();
            SpawnSlips();

            bool hit = false;
            for (int i = 0; i < _walls.Count; i++)
            {
                var w = _walls[i];
                if (!w.active) continue;
                w.age += dt;
                float live = Live(ref w.warn, dt);
                if (live > 0f)
                {
                    w.x += w.vx * live;
                    if (Math.Abs(w.x) > HalfWidth + EntryMargin + 0.1f && Math.Sign(w.x) == Math.Sign(w.vx)) w.active = false;
                    else if (WallHits(w, w.x, _light, 0f)) hit = true;
                }
                _walls[i] = w;
            }
            for (int i = 0; i < _rings.Count; i++)
            {
                var r = _rings[i];
                if (!r.active) continue;
                r.age += dt;
                float live = Live(ref r.warn, dt);
                if (live > 0f)
                {
                    r.radius += r.growth * live;
                    if (r.radius > 8f) r.active = false;
                    else if (RingHits(r, r.radius, _light, 0f)) hit = true;
                }
                _rings[i] = r;
            }
            for (int i = 0; i < _blobs.Count; i++)
            {
                var b = _blobs[i];
                if (!b.active) continue;
                b.age += dt;
                float live = Live(ref b.warn, dt);
                if (live > 0f)
                {
                    b.position += b.velocity * live;
                    if (Math.Abs(b.position.X) > HalfWidth + 1f || Math.Abs(b.position.Y) > HalfHeight + 1f) b.active = false;
                    else if (BlobHits(b.position, _light, 0f)) hit = true;
                }
                _blobs[i] = b;
            }
            for (int i = 0; i < _slips.Count; i++)
            {
                var s = _slips[i];
                if (!s.active) continue;
                s.age += dt;
                s.position += s.velocity * dt;
                if (Math.Abs(s.position.X) > HalfWidth - SlipRadius) { s.velocity.X = -s.velocity.X; s.position.X = Math.Sign(s.position.X) * (HalfWidth - SlipRadius); }
                if (Math.Abs(s.position.Y) > HalfHeight - SlipRadius) { s.velocity.Y = -s.velocity.Y; s.position.Y = Math.Sign(s.position.Y) * (HalfHeight - SlipRadius); }
                bool touched = Vector2.DistanceSquared(s.position, _light) < (SlipRadius + LightRadius) * (SlipRadius + LightRadius);
                if (touched || (!_spec.canHurt && s.age >= SlipAutoCollect))
                {
                    s.active = false;
                    s.caught = true;
                    if (_caught == null) _caught = s.premise;
                }
                _slips[i] = s;
            }

            if (!_spec.exemptFromRule) SpawnDue();

            if (hit && !_touching) _touches++;
            _touching = hit;
            if (hit && _spec.canHurt && _invulnerable <= 0f)
            {
                _hits++;
                _invulnerable = Invulnerability;
            }
        }

        void SpawnDue()
        {
            // Spawns stop a little before the end, so the surface clears as the phase closes.
            while (_time >= _nextSpawn && _nextSpawn < _spec.duration - 1.2f)
            {
                Spawn(_nextSpawn);
                _nextSpawn += Math.Max(0.15f, _spec.interval);
            }
        }

        /// <summary>Counts down a telegraph; returns the part of dt the attack is live for (0 while it's a warning).</summary>
        static float Live(ref float warn, float dt)
        {
            if (warn <= 0f) return dt;
            warn -= dt;
            if (warn > 0f) return 0f;
            float live = -warn;
            warn = 0f;
            return live;
        }

        // ---------- Collisions (analytic: a wall is a column with gaps, a ring an annulus with gaps) ----------
        // pad widens the attack (the fairness check keeps that much extra room); 0 in play.

        bool WallHits(in Wall w, float x, Vector2 p, float pad)
        {
            if (Math.Abs(p.X - x) >= PlateRadius + LightRadius + pad) return false;
            return !InGap(w.gap0, p.Y, pad) && !(w.gapCount > 1 && InGap(w.gap1, p.Y, pad));
        }

        bool InGap(float centre, float y, float pad) => Math.Abs(y - centre) <= _spec.gap * 0.5f - LightRadius - pad;

        static bool RingHits(in Ring r, float radius, Vector2 p, float pad)
        {
            var d = p - r.center;
            float dist = d.Length();
            if (Math.Abs(dist - radius) >= RingThickness * 0.5f + LightRadius + pad) return false;
            // In a gap when the light's centre is between the gap's two edges (at least pad from each).
            if (Cross(r.edge0a, d) >= pad && Cross(d, r.edge0b) >= pad) return false;
            if (r.gapCount > 1 && Cross(r.edge1a, d) >= pad && Cross(d, r.edge1b) >= pad) return false;
            return true;
        }

        static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;

        static Vector2 Dir(float angle) => new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));

        static bool BlobHits(Vector2 position, Vector2 p, float pad)
        {
            float rr = BlobRadius + LightRadius + pad;
            return Vector2.DistanceSquared(position, p) < rr * rr;
        }

        /// <summary>True if a gap of wall <paramref name="w"/> covers height <paramref name="y"/> (tests, the pilot, the view).</summary>
        public bool WallGapAt(Wall w, float y) => InGap(w.gap0, y, 0f) || (w.gapCount > 1 && InGap(w.gap1, y, 0f));

        // ---------- Looking ahead (the fairness check and the planning dodger) ----------

        /// <summary>True if an attack would touch a light at <paramref name="p"/> <paramref name="ahead"/> seconds
        /// from now, with <paramref name="pad"/> extra room. Only attacks at least <paramref name="minAge"/> old count
        /// (the ones a player has had time to notice). Everything moves in straight lines, so this is exact.</summary>
        public bool HurtsAt(Vector2 p, float ahead, float pad, float minAge) => HurtsDuring(p, ahead, ahead, pad, minAge);

        /// <summary>True if an attack would touch a light held at <paramref name="p"/> at any moment between
        /// <paramref name="from"/> and <paramref name="to"/> seconds from now (the sweep, not just the two ends).</summary>
        public bool HurtsDuring(Vector2 p, float from, float to, float pad, float minAge)
        {
            for (int i = 0; i < _walls.Count; i++)
            {
                var w = _walls[i];
                if (!w.active || w.age < minAge) continue;
                float live1 = to - w.warn;
                if (live1 < 0f) continue;
                float live0 = Math.Max(0f, from - w.warn);
                float xa = w.x + w.vx * live0, xb = w.x + w.vx * live1;
                float near = Math.Max(Math.Min(xa, xb), Math.Min(Math.Max(xa, xb), p.X));
                if (WallHits(w, near, p, pad)) return true;
            }
            for (int i = 0; i < _rings.Count; i++)
            {
                var r = _rings[i];
                if (!r.active || r.age < minAge) continue;
                float live1 = to - r.warn;
                if (live1 < 0f) continue;
                float live0 = Math.Max(0f, from - r.warn);
                float lo = r.radius + r.growth * live0, hi = r.radius + r.growth * live1;
                float dist = Vector2.Distance(p, r.center);
                float near = Math.Max(lo, Math.Min(hi, dist));
                if (RingHits(r, near, p, pad)) return true;
            }
            for (int i = 0; i < _blobs.Count; i++)
            {
                var b = _blobs[i];
                if (!b.active || b.age < minAge) continue;
                float live1 = to - b.warn;
                if (live1 < 0f) continue;
                float live0 = Math.Max(0f, from - b.warn);
                var a = b.position + b.velocity * live0;
                var seg = b.velocity * (live1 - live0);
                float len2 = seg.LengthSquared();
                float u = len2 > 1e-8f ? Math.Max(0f, Math.Min(1f, Vector2.Dot(p - a, seg) / len2)) : 0f;
                if (BlobHits(a + seg * u, p, pad)) return true;
            }
            return false;
        }

        /// <summary>Fills <paramref name="sweep"/> with the attacks at least <paramref name="minAge"/> old as they
        /// sweep through <paramref name="from"/>..<paramref name="to"/> seconds from now (the planner tests many spots
        /// against one stretch of time; this works each attack out once).</summary>
        public void Sweep(PressureSweep sweep, float from, float to, float minAge)
        {
            sweep.Begin(_spec.gap * 0.5f - LightRadius);
            for (int i = 0; i < _walls.Count; i++)
            {
                var w = _walls[i];
                if (!w.active || w.age < minAge) continue;
                float live1 = to - w.warn;
                if (live1 < 0f) continue;
                float live0 = Math.Max(0f, from - w.warn);
                sweep.AddWall(w.x + w.vx * live0, w.x + w.vx * live1, w.gap0, w.gap1, w.gapCount);
            }
            for (int i = 0; i < _rings.Count; i++)
            {
                var r = _rings[i];
                if (!r.active || r.age < minAge) continue;
                float live1 = to - r.warn;
                if (live1 < 0f) continue;
                float live0 = Math.Max(0f, from - r.warn);
                sweep.AddRing(r.center, r.radius + r.growth * live0, r.radius + r.growth * live1, r.edge0a, r.edge0b, r.edge1a, r.edge1b, r.gapCount);
            }
            for (int i = 0; i < _blobs.Count; i++)
            {
                var b = _blobs[i];
                if (!b.active || b.age < minAge) continue;
                float live1 = to - b.warn;
                if (live1 < 0f) continue;
                float live0 = Math.Max(0f, from - b.warn);
                sweep.AddBlob(b.position + b.velocity * live0, b.velocity * (live1 - live0));
            }
        }

        /// <summary>Seconds from now until no attack at least <paramref name="minAge"/> old can touch the box.</summary>
        public float ThreatEnd(float minAge)
        {
            float end = 0f;
            const float reachX = HalfWidth + LightRadius, reachY = HalfHeight + LightRadius;
            for (int i = 0; i < _walls.Count; i++)
            {
                var w = _walls[i];
                if (!w.active || w.age < minAge || w.vx == 0f) continue;
                float ahead = Math.Sign(w.vx) * w.x;
                float t = w.warn + Math.Max(0f, (reachX + PlateRadius - ahead) / Math.Abs(w.vx));
                if (t > end) end = t;
            }
            for (int i = 0; i < _rings.Count; i++)
            {
                var r = _rings[i];
                if (!r.active || r.age < minAge || r.growth <= 0f) continue;
                float fx = HalfWidth + Math.Abs(r.center.X), fy = HalfHeight + Math.Abs(r.center.Y);
                float far = (float)Math.Sqrt(fx * fx + fy * fy) + RingThickness * 0.5f + LightRadius;
                float t = r.warn + Math.Max(0f, (far - r.radius) / r.growth);
                if (t > end) end = t;
            }
            for (int i = 0; i < _blobs.Count; i++)
            {
                var b = _blobs[i];
                if (!b.active || b.age < minAge) continue;
                float tx = Exit(b.position.X, b.velocity.X, reachX + BlobRadius);
                float ty = Exit(b.position.Y, b.velocity.Y, reachY + BlobRadius);
                float t = b.warn + Math.Min(tx, ty);
                if (t > end) end = t;
            }
            return end;
        }

        static float Exit(float pos, float vel, float bound)
        {
            if (vel > 1e-5f) return Math.Max(0f, (bound - pos) / vel);
            if (vel < -1e-5f) return Math.Max(0f, (pos + bound) / -vel);
            return float.MaxValue;
        }

        /// <summary>The newest attack at least <paramref name="minAge"/> old (its serial; 0 if none): a planning
        /// dodger re-plans when this changes.</summary>
        public int NewestNoticed(float minAge)
        {
            int s = 0;
            for (int i = 0; i < _walls.Count; i++) if (_walls[i].active && _walls[i].age >= minAge && _walls[i].serial > s) s = _walls[i].serial;
            for (int i = 0; i < _rings.Count; i++) if (_rings[i].active && _rings[i].age >= minAge && _rings[i].serial > s) s = _rings[i].serial;
            for (int i = 0; i < _blobs.Count; i++) if (_blobs[i].active && _blobs[i].age >= minAge && _blobs[i].serial > s) s = _blobs[i].serial;
            return s;
        }

        // ---------- Spawning ----------

        void Spawn(float at)
        {
            var shape = _spec.shapes[_rng.Range(0, _spec.shapes.Length)];
            bool done;
            switch (shape)
            {
                case PressureShape.Wall: done = SpawnWall(at); break;
                case PressureShape.Bloom: done = SpawnRing(at); break;
                default: done = SpawnBlob(at); break;
            }
            if (done) _spawned++;
            else _skipped++;
        }

        /// <summary>The telegraph a spawn gets, less the moment it's late (the tick's length).</summary>
        float Warn(float at) => Math.Max(0f, _spec.telegraph - (_time - at));

        /// <summary>Seconds a player has to move before an attack that can reach a spot in <paramref name="seconds"/> does.</summary>
        static float Reach(float seconds) => LightSpeed * PressureRule.PlannerSpeed * Math.Max(0f, seconds - PressureRule.Reaction);

        bool SpawnWall(float at)
        {
            float speed = Math.Max(0.2f, _spec.speed);
            if (_spec.exemptFromRule) return SpawnLongNightWall(at, speed);
            float lo = -HalfHeight + _spec.gap * 0.5f, hi = HalfHeight - _spec.gap * 0.5f;
            float warn = Warn(at);
            for (int attempt = 0; attempt < SpawnTries; attempt++)
            {
                bool fromLeft = _rng.Chance(0.5f);
                float startX = fromLeft ? -(HalfWidth + EntryMargin) : HalfWidth + EntryMargin;
                // A gap she can reach before the wall reaches her (where she is now), the first tries; then anywhere.
                float meets = warn + Math.Max(0f, Math.Abs(startX - _light.X) - PlateRadius - LightRadius) / speed;
                float reach = attempt < SpawnTries / 2 ? Reach(meets) : 2f * HalfHeight;
                float a = Math.Max(lo, _light.Y - reach), b = Math.Min(hi, _light.Y + reach);
                float g0 = a <= b ? _rng.Range(a, b) : Math.Max(lo, Math.Min(hi, _light.Y));
                float g1 = g0;
                if (_spec.gaps > 1)
                    g1 = g0 > 0f ? _rng.Range(lo, Math.Max(lo, g0 - _spec.gap * 2f)) : _rng.Range(Math.Min(hi, g0 + _spec.gap * 2f), hi);
                float vx = fromLeft ? speed : -speed;
                var wall = new Wall { x = startX, vx = vx, gap0 = g0, gap1 = g1, gapCount = Math.Max(1, _spec.gaps), warn = warn, active = true };
                if (!PressureRule.Admit(Math.Abs(startX) - PlateRadius < HalfWidth, warn, _spec)) { _refused++; return false; }
                int slot = Put(_walls, wall);
                if (Fair(_walls, slot)) { Commit(_walls, slot); return true; }
                Drop(_walls, slot);
            }
            return false;
        }

        /// <summary>The Long Night's walls (exempt from the rule: they come in from outside with no warning), each
        /// next gap within reach of the last one: technically winnable, near impossible.</summary>
        bool SpawnLongNightWall(float at, float speed)
        {
            bool fromLeft = _rng.Chance(0.5f);
            float startX = fromLeft ? -(HalfWidth + EntryMargin) : HalfWidth + EntryMargin;
            float arrival = at + (HalfWidth + EntryMargin) / speed;
            // The light can always reach this gap from the last one: it only has the time between the two walls
            // crossing the middle, less the time a wall takes to pass.
            float pass = 2f * (PlateRadius + LightRadius) / speed;
            float reach = LightSpeed * Math.Max(0f, arrival - _lastArrival - pass) * 0.75f;
            float lo = -HalfHeight + _spec.gap * 0.5f, hi = HalfHeight - _spec.gap * 0.5f;
            float g0 = _rng.Range(Math.Max(lo, _lastGapY - reach), Math.Min(hi, _lastGapY + reach));
            float g1 = g0;
            if (_spec.gaps > 1)
                g1 = g0 > 0f ? _rng.Range(lo, Math.Max(lo, g0 - _spec.gap * 2f)) : _rng.Range(Math.Min(hi, g0 + _spec.gap * 2f), hi);
            _lastGapY = g0;
            _lastArrival = arrival;
            float vx = fromLeft ? speed : -speed;
            // Spawned a moment after its slot (the tick's length): start where it would be by now.
            var wall = new Wall { x = startX + vx * (_time - at), vx = vx, gap0 = g0, gap1 = g1, gapCount = Math.Max(1, _spec.gaps), active = true };
            if (!PressureRule.Admit(Math.Abs(wall.x) - PlateRadius < HalfWidth, 0f, _spec)) { _refused++; return false; }
            Commit(_walls, Put(_walls, wall));
            return true;
        }

        bool SpawnRing(float at)
        {
            float warn = Warn(at);
            float growth = Math.Max(0.2f, _spec.speed * 0.8f);
            // The gap's arc is the spec's gap at a radius of 1.5 (narrower near the centre, wider out).
            float half = Math.Min(1.2f, _spec.gap * 0.5f / 1.5f);
            const float band = RingThickness * 0.5f + LightRadius;
            for (int attempt = 0; attempt < SpawnTries; attempt++)
            {
                var c = new Vector2(_rng.Range(-HalfWidth * 0.8f, HalfWidth * 0.8f), _rng.Range(-HalfHeight * 0.7f, HalfHeight * 0.7f));
                // Never on top of her or right beside her.
                if (Vector2.Distance(c, _light) < RingMinDistance) c = new Vector2(-c.X, -c.Y);
                if (Vector2.Distance(c, _light) < RingMinDistance) continue;
                // A gap she can reach before the ring reaches her: aimed within reach of where she is now, so the
                // gap is never off the edge of the box on her side. (The fairness check below has the last word.)
                var toHer = _light - c;
                float dist = toHer.Length();
                float her = (float)Math.Atan2(toHer.Y, toHer.X);
                float reach = Reach(warn + Math.Max(0f, dist - band) / growth) * 0.6f;
                float spread = reach >= 2f * dist ? (float)Math.PI : 2f * (float)Math.Asin(reach / (2f * dist));
                float a0 = her + _rng.Range(-spread, spread);
                var ring = new Ring
                {
                    center = c, radius = 0f, growth = growth,
                    gapAngle0 = a0, gapAngle1 = a0 + (float)Math.PI, gapHalfAngle = half, gapCount = Math.Max(1, _spec.gaps),
                    edge0a = Dir(a0 - half), edge0b = Dir(a0 + half),
                    edge1a = Dir(a0 + (float)Math.PI - half), edge1b = Dir(a0 + (float)Math.PI + half),
                    warn = warn, active = true,
                };
                if (!PressureRule.Admit(true, warn, _spec)) { _refused++; return false; }
                int slot = Put(_rings, ring);
                if (Fair(_rings, slot)) { Commit(_rings, slot); return true; }
                Drop(_rings, slot);
            }
            return false;
        }

        bool SpawnBlob(float at)
        {
            float warn = Warn(at);
            for (int attempt = 0; attempt < SpawnTries; attempt++)
            {
                // Lobbed from an edge towards where she is.
                float side = _rng.Chance(0.5f) ? -1f : 1f;
                var from = new Vector2(side * (HalfWidth + 0.3f), _rng.Range(-HalfHeight, HalfHeight));
                var dir = _light - from;
                if (dir.LengthSquared() < 0.0001f) dir = new Vector2(-side, 0f);
                dir = Vector2.Normalize(dir);
                var blob = new Blob { position = from, velocity = dir * Math.Max(0.2f, _spec.speed * 0.9f), warn = warn, active = true };
                if (!PressureRule.Admit(Math.Abs(from.X) - BlobRadius < HalfWidth, warn, _spec)) { _refused++; return false; }
                int slot = Put(_blobs, blob);
                if (Fair(_blobs, slot)) { Commit(_blobs, slot); return true; }
                Drop(_blobs, slot);
            }
            return false;
        }

        // A candidate goes into a free slot, is checked, then committed (or dropped). No allocation once the lists
        // have grown to the phase's peak.

        static int Put(List<Wall> list, Wall item)
        {
            for (int i = 0; i < list.Count; i++) if (!list[i].active) { list[i] = item; return i; }
            list.Add(item);
            return list.Count - 1;
        }

        static int Put(List<Ring> list, Ring item)
        {
            for (int i = 0; i < list.Count; i++) if (!list[i].active) { list[i] = item; return i; }
            list.Add(item);
            return list.Count - 1;
        }

        static int Put(List<Blob> list, Blob item)
        {
            for (int i = 0; i < list.Count; i++) if (!list[i].active) { list[i] = item; return i; }
            list.Add(item);
            return list.Count - 1;
        }

        void Commit(List<Wall> list, int slot) { var w = list[slot]; w.serial = ++_serial; list[slot] = w; if (w.warn > 0f) _telegraphs++; }
        void Commit(List<Ring> list, int slot) { var r = list[slot]; r.serial = ++_serial; list[slot] = r; if (r.warn > 0f) _telegraphs++; }
        void Commit(List<Blob> list, int slot) { var b = list[slot]; b.serial = ++_serial; list[slot] = b; if (b.warn > 0f) _telegraphs++; }
        static void Drop(List<Wall> list, int slot) { var w = list[slot]; w.active = false; list[slot] = w; }
        static void Drop(List<Ring> list, int slot) { var r = list[slot]; r.active = false; list[slot] = r; }
        static void Drop(List<Blob> list, int slot) { var b = list[slot]; b.active = false; list[slot] = b; }

        // The fairness check: with the candidate in place, is there still a way out for a player slower than she
        // really is, who notices it late? Checked twice: from where she is, with the candidate's warning cut by the
        // reaction time; and from where her current motion carries her during that reaction time (a player keeps
        // moving until they notice), starting then.
        bool Fair(List<Wall> list, int slot)
        {
            if (_check == null) return true;
            var w = list[slot]; float warn = w.warn;
            w.warn = Math.Max(0f, warn - PressureRule.Reaction); list[slot] = w;
            bool ok = EscapeFromHere();
            w.warn = warn; list[slot] = w;
            return ok && EscapeAfterReacting();
        }

        bool Fair(List<Ring> list, int slot)
        {
            if (_check == null) return true;
            var r = list[slot]; float warn = r.warn;
            r.warn = Math.Max(0f, warn - PressureRule.Reaction); list[slot] = r;
            bool ok = EscapeFromHere();
            r.warn = warn; list[slot] = r;
            return ok && EscapeAfterReacting();
        }

        bool Fair(List<Blob> list, int slot)
        {
            if (_check == null) return true;
            var b = list[slot]; float warn = b.warn;
            b.warn = Math.Max(0f, warn - PressureRule.Reaction); list[slot] = b;
            bool ok = EscapeFromHere();
            b.warn = warn; list[slot] = b;
            return ok && EscapeAfterReacting();
        }

        bool EscapeFromHere() => _check.Plan(this, _light, 0f, _light);

        // A player who hasn't noticed yet keeps doing what they were doing (standing, or moving the same way) for
        // the reaction time: that stretch must be safe, and from where it ends there must be a way out.
        bool EscapeAfterReacting()
        {
            const float t = PressureRule.Reaction;
            var p = _light + _lightVelocity * t;
            p.X = Math.Max(-HalfWidth + LightRadius, Math.Min(HalfWidth - LightRadius, p.X));
            p.Y = Math.Max(-HalfHeight + LightRadius, Math.Min(HalfHeight - LightRadius, p.Y));
            const int parts = 5;
            float pad = PressureRule.PlannerPad + Vector2.Distance(_light, p) / parts * 0.5f;
            for (int k = 0; k < parts; k++)
            {
                var q = Vector2.Lerp(_light, p, (k + 0.5f) / parts);
                Sweep(_reactSweep, t * k / parts, t * (k + 1) / parts, 0f);
                if (_reactSweep.Hurts(q, pad)) return false;
            }
            return _check.Plan(this, p, 0f, p, t);
        }

        void SpawnSlips()
        {
            if (_slipsSpawned >= _slipSeeds.Length) return;
            // The first at 1.5 s, a second (if there is one) at 4 s.
            float at = _slipsSpawned == 0 ? 1.5f : 4f;
            if (_time < at || at >= _spec.duration - 1f) return;
            var seed = _slipSeeds[_slipsSpawned++];
            var pos = new Vector2(_rng.Range(-HalfWidth * 0.7f, HalfWidth * 0.7f), _rng.Range(-HalfHeight * 0.6f, HalfHeight * 0.6f));
            float ang = _rng.Range(0f, 2f * (float)Math.PI);
            _slips.Add(new Slip
            {
                position = pos, velocity = new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang)) * 0.45f,
                premise = seed.premise, name = seed.name, active = true,
            });
        }

    }

    /// <summary>
    /// A perfect dodger for walls (the smoke run, attract mode, the Long Night's proof): it heads for the gap of the
    /// next wall to cross it, and drifts back to the middle column. Against the Long Night's walls-only phases it
    /// never gets hit, which is the proof that "scripted but beatable" really is beatable.
    /// </summary>
    public static class PressurePilot
    {
        public static Vector2 Steer(PressurePhase phase)
        {
            var light = phase.Light;
            float bestTime = float.MaxValue;
            float target = light.Y;
            bool found = false;
            var walls = phase.Walls;
            for (int i = 0; i < walls.Count; i++)
            {
                var w = walls[i];
                if (!w.active || w.vx == 0f) continue;
                // Time until this wall's far edge has passed the light (it's no threat once that's negative).
                float edge = PressurePhase.PlateRadius + PressurePhase.LightRadius;
                float passed = w.warn + ((light.X - w.x) * Math.Sign(w.vx) + edge) / Math.Abs(w.vx);
                if (passed < 0f) continue;
                if (passed < bestTime)
                {
                    bestTime = passed;
                    float g = w.gap0;
                    if (w.gapCount > 1 && Math.Abs(w.gap1 - light.Y) < Math.Abs(w.gap0 - light.Y)) g = w.gap1;
                    target = g;
                    found = true;
                }
            }
            float dy = target - light.Y;
            float y = found ? Clamp(dy * 12f) : 0f;
            float x = Clamp(-light.X * 2f);
            return new Vector2(x, y);
        }

        static float Clamp(float v) => v < -1f ? -1f : v > 1f ? 1f : v;
    }
}
