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
        public PressureShape[] shapes;

        public const float MinGapNormal = PressurePhase.LightRadius * 2f + 0.12f;
        public const float MinGapScripted = PressurePhase.LightRadius * 2f + 0.05f;

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
                list.Clear();
                list.Add(PressureShape.Wall);
            }
            s.shapes = list.ToArray();
            return s;
        }

        static int Clamp(int v, int min, int max) => v < min ? min : v > max ? max : v;
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
    /// at her. Hits raise her heart rate (the engine counts them); catching a slip reveals what they aren't saying.
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
        const float EntryMargin = 0.4f;

        /// <summary>A column of plates sliding across, with its gaps (centre y; free space <see cref="PressureSpec.gap"/>).</summary>
        public struct Wall
        {
            public float x, vx;
            public float gap0, gap1;
            public int gapCount;
            public bool active;
        }

        /// <summary>A flour ring blooming out from a point, with its gaps (directions in radians).</summary>
        public struct Ring
        {
            public Vector2 center;
            public float radius, growth;
            public float gapAngle0, gapAngle1, gapHalfAngle;
            public int gapCount;
            public bool active;
        }

        public struct Blob
        {
            public Vector2 position, velocity;
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
        int _slipsSpawned;
        float _time, _nextSpawn, _invulnerable;
        int _hits, _hitsTaken, _spawned;
        Vector2 _light;
        string _caught;
        // Reachability: the last wall's gap and when it crosses x = 0.
        float _lastGapY, _lastArrival = -10f;

        public PressurePhase(PressureSpec spec, ulong seed, IList<SlipSeed> slips)
        {
            _spec = spec;
            _rng = new Rng(seed == 0 ? 1UL : seed);
            int n = slips != null ? Math.Min(2, slips.Count) : 0;
            _slipSeeds = new SlipSeed[n];
            for (int i = 0; i < n; i++) _slipSeeds[i] = slips[i];
            _nextSpawn = 0.6f;
        }

        public PressureSpec Spec => _spec;
        public float Time => _time;
        public float Duration => _spec.duration;
        public bool Finished => _time >= _spec.duration;
        public Vector2 Light => _light;
        public float Invulnerable => _invulnerable;
        /// <summary>Hits in this phase so far.</summary>
        public int Hits => _hits;
        /// <summary>Walls, rings and blobs spawned so far (density, for tests).</summary>
        public int Spawned => _spawned;
        public IReadOnlyList<Wall> Walls => _walls;
        public IReadOnlyList<Ring> Rings => _rings;
        public IReadOnlyList<Blob> Blobs => _blobs;
        public IReadOnlyList<Slip> Slips => _slips;

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
            _light += input * (LightSpeed * dt);
            _light.X = Math.Max(-HalfWidth + LightRadius, Math.Min(HalfWidth - LightRadius, _light.X));
            _light.Y = Math.Max(-HalfHeight + LightRadius, Math.Min(HalfHeight - LightRadius, _light.Y));

            // Spawns stop a little before the end, so the surface clears as the phase closes.
            while (_time >= _nextSpawn && _nextSpawn < _spec.duration - 1.2f)
            {
                Spawn(_nextSpawn);
                _nextSpawn += Math.Max(0.15f, _spec.interval);
            }
            SpawnSlips();

            bool hit = false;
            for (int i = 0; i < _walls.Count; i++)
            {
                var w = _walls[i];
                if (!w.active) continue;
                w.x += w.vx * dt;
                if (Math.Abs(w.x) > HalfWidth + EntryMargin + 0.1f && Math.Sign(w.x) == Math.Sign(w.vx)) w.active = false;
                else if (Touches(w, _light)) hit = true;
                _walls[i] = w;
            }
            for (int i = 0; i < _rings.Count; i++)
            {
                var r = _rings[i];
                if (!r.active) continue;
                r.radius += r.growth * dt;
                if (r.radius > 8f) r.active = false;
                else if (Touches(r, _light)) hit = true;
                _rings[i] = r;
            }
            for (int i = 0; i < _blobs.Count; i++)
            {
                var b = _blobs[i];
                if (!b.active) continue;
                b.position += b.velocity * dt;
                if (Math.Abs(b.position.X) > HalfWidth + 1f || Math.Abs(b.position.Y) > HalfHeight + 1f) b.active = false;
                else if (Vector2.DistanceSquared(b.position, _light) < (BlobRadius + LightRadius) * (BlobRadius + LightRadius)) hit = true;
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

            if (hit && _spec.canHurt && _invulnerable <= 0f)
            {
                _hits++;
                _invulnerable = Invulnerability;
            }
        }

        // ---------- Collisions (analytic: a wall is a column with gaps, a ring an annulus with gaps) ----------

        bool Touches(Wall w, Vector2 p)
        {
            if (Math.Abs(p.X - w.x) >= PlateRadius + LightRadius) return false;
            return !InGap(w.gap0, p.Y) && !(w.gapCount > 1 && InGap(w.gap1, p.Y));
        }

        bool InGap(float centre, float y) => Math.Abs(y - centre) <= _spec.gap * 0.5f - LightRadius;

        bool Touches(Ring r, Vector2 p)
        {
            var d = p - r.center;
            float dist = d.Length();
            if (Math.Abs(dist - r.radius) >= RingThickness * 0.5f + LightRadius) return false;
            float a = (float)Math.Atan2(d.Y, d.X);
            if (AngleDiff(a, r.gapAngle0) <= r.gapHalfAngle) return false;
            if (r.gapCount > 1 && AngleDiff(a, r.gapAngle1) <= r.gapHalfAngle) return false;
            return true;
        }

        static float AngleDiff(float a, float b)
        {
            float d = Math.Abs(a - b) % (2f * (float)Math.PI);
            return d > Math.PI ? 2f * (float)Math.PI - d : d;
        }

        /// <summary>True if a gap of wall <paramref name="w"/> covers height <paramref name="y"/> (tests, the pilot).</summary>
        public bool WallGapAt(Wall w, float y) => InGap(w.gap0, y) || (w.gapCount > 1 && InGap(w.gap1, y));

        // ---------- Spawning ----------

        void Spawn(float at)
        {
            _spawned++;
            var shape = _spec.shapes[_rng.Range(0, _spec.shapes.Length)];
            switch (shape)
            {
                case PressureShape.Wall: SpawnWall(at); break;
                case PressureShape.Bloom: SpawnRing(); break;
                default: SpawnBlob(); break;
            }
        }

        void SpawnWall(float at)
        {
            float speed = Math.Max(0.2f, _spec.speed);
            bool fromLeft = _rng.Chance(0.5f);
            float startX = fromLeft ? -(HalfWidth + EntryMargin) : HalfWidth + EntryMargin;
            float arrival = at + (HalfWidth + EntryMargin) / speed;
            // The light can always reach this gap from the last one: it only has the time between the two walls
            // crossing the middle, less the time a wall takes to pass. So every setting is beatable by construction.
            float pass = 2f * (PlateRadius + LightRadius) / speed;
            float reach = LightSpeed * Math.Max(0f, arrival - _lastArrival - pass) * 0.75f;
            float lo = -HalfHeight + _spec.gap * 0.5f, hi = HalfHeight - _spec.gap * 0.5f;
            float g0 = _rng.Range(Math.Max(lo, _lastGapY - reach), Math.Min(hi, _lastGapY + reach));
            float g1 = g0;
            if (_spec.gaps > 1)
            {
                // The second gap well away from the first.
                g1 = g0 > 0f ? _rng.Range(lo, Math.Max(lo, g0 - _spec.gap * 2f)) : _rng.Range(Math.Min(hi, g0 + _spec.gap * 2f), hi);
            }
            _lastGapY = g0;
            _lastArrival = arrival;
            float vx = fromLeft ? speed : -speed;
            // Spawned a moment after its slot (the tick's length): start where it would be by now.
            var wall = new Wall { x = startX + vx * (_time - at), vx = vx, gap0 = g0, gap1 = g1, gapCount = Math.Max(1, _spec.gaps), active = true };
            for (int i = 0; i < _walls.Count; i++)
                if (!_walls[i].active) { _walls[i] = wall; return; }
            _walls.Add(wall);
        }

        void SpawnRing()
        {
            var c = new Vector2(_rng.Range(-HalfWidth * 0.8f, HalfWidth * 0.8f), _rng.Range(-HalfHeight * 0.7f, HalfHeight * 0.7f));
            // Not right on top of her.
            if (Vector2.Distance(c, _light) < 0.8f) c = new Vector2(-c.X, -c.Y);
            float a0 = _rng.Range(-(float)Math.PI, (float)Math.PI);
            // The gap's arc is the spec's gap at a radius of 1.5 (narrower near the centre, wider out).
            float half = Math.Min(1.2f, _spec.gap * 0.5f / 1.5f);
            var ring = new Ring
            {
                center = c, radius = 0f, growth = Math.Max(0.2f, _spec.speed * 0.8f),
                gapAngle0 = a0, gapAngle1 = a0 + (float)Math.PI, gapHalfAngle = half, gapCount = Math.Max(1, _spec.gaps), active = true,
            };
            for (int i = 0; i < _rings.Count; i++)
                if (!_rings[i].active) { _rings[i] = ring; return; }
            _rings.Add(ring);
        }

        void SpawnBlob()
        {
            // Lobbed from an edge towards where she is.
            float side = _rng.Chance(0.5f) ? -1f : 1f;
            var from = new Vector2(side * (HalfWidth + 0.3f), _rng.Range(-HalfHeight, HalfHeight));
            var dir = _light - from;
            if (dir.LengthSquared() < 0.0001f) dir = new Vector2(-side, 0f);
            dir = Vector2.Normalize(dir);
            var blob = new Blob { position = from, velocity = dir * Math.Max(0.2f, _spec.speed * 0.9f), active = true };
            for (int i = 0; i < _blobs.Count; i++)
                if (!_blobs[i].active) { _blobs[i] = blob; return; }
            _blobs.Add(blob);
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
    /// A perfect dodger for walls (tests, the smoke run, attract mode): it heads for the gap of the next wall to
    /// cross it, and drifts back to the middle column. Against the Long Night's walls-only phases it never gets
    /// hit, which is the proof that "scripted but beatable" really is beatable.
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
                float passed = ((light.X - w.x) * Math.Sign(w.vx) + edge) / Math.Abs(w.vx);
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
