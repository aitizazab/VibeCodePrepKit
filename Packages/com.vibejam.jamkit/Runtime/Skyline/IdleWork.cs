using System;
using System.Numerics;

namespace JamKit.Skyline
{
    /// <summary>
    /// The floor-care unit's idle work (fix round "idle life", 2026-10-14; spec 2.5: every enemy has an idle state with its own
    /// identity): before it wakes it cleans. A slow roomba pattern round its post: short straight runs, gentle turns, and a
    /// little bump-and-turn when it meets cover (a stop, a short back-off, a firm turn away). It never strays further than
    /// <see cref="EnemyDef.idleLeash"/> from its post, so where it wakes stays close to its measured post; at the leash it
    /// turns back in. Waking is unchanged: once awake the unit drives from wherever its cleaning left it.
    /// <para>Pure and seeded (its own stream): FightSim and the Unity unit (SkylineBot) run this same code with their own
    /// cover test, so the headless fight and the game clean alike.</para>
    /// </summary>
    public sealed class FloorCareIdle
    {
        /// <summary>A run's length (m): short, a roomba's.</summary>
        public const float RunMin = 0.45f, RunMax = 1.2f;
        /// <summary>A gentle turn: how far (degrees) and how fast (degrees a second); it creeps on at <see cref="ArcShare"/> of its
        /// speed while it turns, so the turn is a curve, not a spin.</summary>
        public const float TurnMin = 25f, TurnMax = 75f, TurnRate = 70f, ArcShare = 0.45f;
        /// <summary>The bump: a stop (s), a back-off (m, at <see cref="BackShare"/> of its speed), then a firm turn away (degrees,
        /// at <see cref="BumpTurnRate"/>).</summary>
        public const float BumpStop = 0.3f, BackOff = 0.12f, BackShare = 0.6f, BumpTurnMin = 100f, BumpTurnMax = 160f, BumpTurnRate = 150f;
        /// <summary>At the leash it turns back toward its post, give or take this many degrees.</summary>
        public const float HomeSpread = 50f;

        public enum Phase { Run, Turn, Bump, Back }

        readonly EnemyDef _def;
        readonly Rng _rng;
        Vector3 _flat;
        float _heading, _turnTo, _runLeft, _timer, _speed, _turnRate;
        bool _arc;

        public FloorCareIdle(EnemyDef def, ulong seed)
        {
            _def = def ?? throw new ArgumentNullException(nameof(def));
            _rng = new Rng(seed, 0xF1007C1EA7UL);
            _heading = _rng.Range(0f, 360f);
            StartRun();
        }

        /// <summary>Whether a bot of <paramref name="def"/> cleans while idle (a floor bot with an idle speed and leash in its data).</summary>
        public static bool Works(EnemyDef def) => def != null && def.moveSpeed > 0f && def.idleSpeed > 0f && def.idleLeash > 0f;

        /// <summary>Where it is relative to its post, on the floor plan (m; y is 0).</summary>
        public Vector3 Offset => _flat;
        /// <summary>Which way it faces (degrees, 0 = +z, 90 = +x: Unity's yaw).</summary>
        public float Yaw => _heading;
        /// <summary>Its speed this step (m/s; negative while it backs off a bump).</summary>
        public float Speed => _speed;
        public Phase State { get; private set; }
        /// <summary>Bumps so far (cover met).</summary>
        public int Bumps { get; private set; }

        Vector3 Forward => new Vector3(MathF.Sin(_heading * (MathF.PI / 180f)), 0f, MathF.Cos(_heading * (MathF.PI / 180f)));

        /// <summary>
        /// One step. <paramref name="post"/> is its measured spawn (the floor under it); <paramref name="blocked"/> says whether
        /// its body (the given radius) moved between two floor points meets cover (null: open floor).
        /// </summary>
        public void Tick(float dt, Vector3 post, Func<Vector3, Vector3, float, bool> blocked)
        {
            if (!(dt > 0f) || float.IsInfinity(dt)) return;
            float speed = _def.idleSpeed, leash = _def.idleLeash;
            _speed = 0f;
            switch (State)
            {
                case Phase.Run:
                {
                    float step = Math.Min(speed * dt, Math.Max(_runLeft, 0f));
                    Vector3 next = _flat + Forward * step;
                    if (Length(next) > leash) { TurnHome(); break; }
                    if (Blocked(blocked, post, next)) { Bump(); break; }
                    _flat = next;
                    _speed = speed;
                    _runLeft -= step;
                    if (_runLeft <= 1e-4f)
                    {
                        // a gentle turn, either way; once it is out past half its leash, the way that heads back toward
                        // its post, so its runs curl round the patch instead of ending at the leash in a turn on the spot
                        // (judge round 3: the idle read as turning on the spot). The same two draws either way.
                        float by = _rng.Range(TurnMin, TurnMax) * (_rng.NextFloat() < 0.5f ? -1f : 1f);
                        if (Length(_flat) > leash * 0.5f)
                        {
                            float home = DeltaAngle(_heading, Bearing(-_flat));
                            if (Math.Abs(home) > 1f) by = Math.Abs(by) * Math.Sign(home);
                        }
                        StartTurn(_heading + by, TurnRate, true);
                    }
                    break;
                }
                case Phase.Turn:
                {
                    float d = DeltaAngle(_heading, _turnTo);
                    float turn = _turnRate * dt;
                    _heading = Wrap(Math.Abs(d) <= turn ? _turnTo : _heading + Math.Sign(d) * turn);
                    if (_arc)
                    {
                        // the curve: it creeps on while it turns, unless that would leave the leash or meet cover
                        Vector3 next = _flat + Forward * (speed * ArcShare * dt);
                        if (Length(next) <= leash && !Blocked(blocked, post, next))
                        {
                            _flat = next;
                            _speed = speed * ArcShare;
                        }
                    }
                    if (Math.Abs(d) <= turn) StartRun();
                    break;
                }
                case Phase.Bump:
                    _timer -= dt;
                    if (_timer <= 0f)
                    {
                        State = Phase.Back;
                        _runLeft = BackOff;
                    }
                    break;
                case Phase.Back:
                {
                    float step = Math.Min(speed * BackShare * dt, Math.Max(_runLeft, 0f));
                    Vector3 next = _flat - Forward * step;
                    bool ok = step > 0f && Length(next) <= leash && !Blocked(blocked, post, next);
                    if (ok)
                    {
                        _flat = next;
                        _speed = -speed * BackShare;
                        _runLeft -= step;
                    }
                    if (!ok || _runLeft <= 1e-4f) TurnAway();
                    break;
                }
            }
        }

        bool Blocked(Func<Vector3, Vector3, float, bool> blocked, Vector3 post, Vector3 next) =>
            blocked != null && blocked(post + _flat, post + next, _def.bodyRadius);

        void StartRun()
        {
            State = Phase.Run;
            _runLeft = _rng.Range(RunMin, RunMax);
        }

        void StartTurn(float to, float rate, bool arc)
        {
            State = Phase.Turn;
            _turnTo = Wrap(to);
            _turnRate = rate;
            _arc = arc;
        }

        void Bump()
        {
            Bumps++;
            State = Phase.Bump;
            _timer = BumpStop;
        }

        /// <summary>After a bump: a firm turn, toward the side its post is on (the post is clear), else either way.</summary>
        void TurnAway()
        {
            float by = _rng.Range(BumpTurnMin, BumpTurnMax);
            float side = _rng.NextFloat() < 0.5f ? -1f : 1f;
            if (Length(_flat) > 0.2f)
            {
                float toPost = Bearing(-_flat);
                side = DeltaAngle(_heading, toPost) >= 0f ? 1f : -1f;
            }
            StartTurn(_heading + side * by, BumpTurnRate, false);
        }

        /// <summary>At the leash: turn (on the spot) back toward the post, give or take <see cref="HomeSpread"/>.</summary>
        void TurnHome()
        {
            float to = Bearing(-_flat) + _rng.Range(-HomeSpread, HomeSpread);
            // a curve where it can (it creeps on whenever that stays inside the leash), not a pivot
            StartTurn(to, TurnRate * 1.5f, true);
        }

        static float Bearing(Vector3 v) => MathF.Atan2(v.X, v.Z) * (180f / MathF.PI);
        static float Length(Vector3 v) => MathF.Sqrt(v.X * v.X + v.Z * v.Z);

        static float Wrap(float deg)
        {
            deg %= 360f;
            return deg < 0f ? deg + 360f : deg;
        }

        /// <summary>The shortest signed turn from <paramref name="from"/> to <paramref name="to"/> (degrees, -180..180).</summary>
        static float DeltaAngle(float from, float to)
        {
            float d = Wrap(to - from);
            return d > 180f ? d - 360f : d;
        }

        /// <summary>FightSim's cover test for a cleaning unit: a move whose end lies inside a floor box grown by the radius (and
        /// whose start didn't), or that leaves the hall (less the radius) further than it started.</summary>
        public static bool FloorBlocked(Vector3 a, Vector3 b, float r, CoverBox[] floorCover, FightArea? area = null)
        {
            FightArea ar = area ?? FightArea.Galleria;
            if (HoverPatrol.LeavesArea(a, b, ar.minX + r + 0.05f, ar.maxX - r - 0.05f, ar.minZ + r + 0.05f, ar.maxZ - r - 0.05f)) return true;
            if (floorCover == null) return false;
            foreach (var box in floorCover)
                if (Inside(b, box, r) && !Inside(a, box, r)) return true;
            return false;
        }

        static bool Inside(Vector3 p, CoverBox b, float r) =>
            p.X > b.min.X - r && p.X < b.max.X + r && p.Z > b.min.Z - r && p.Z < b.max.Z + r;
    }

    /// <summary>
    /// The window-washer gondola's idle work (fix round "idle life", 2026-10-14): before it wakes it washes the panes. It rides
    /// slowly back and forth along a short stretch of its rail from its spawn (up to <see cref="EnemyDef.idleLeash"/> m, at
    /// <see cref="EnemyDef.idleSpeed"/>, easing in and out), and stops a moment at each end as if finishing a pane; the Unity
    /// gondola swipes its squeegee over the glass all the while. Waking is unchanged: once awake it rides the rail at its full
    /// speed from where its washing left it (<see cref="GondolaRail.StartAt"/>), on toward the rail's end, and its mines go on
    /// the same rail points as before (<see cref="MineLayer"/> lays on the spacing's grid).
    /// <para>Pure and seeded (its own stream): FightSim and the Unity gondola run this same code.</para>
    /// </summary>
    public sealed class GondolaIdle
    {
        /// <summary>The stop at each end of the stretch (s).</summary>
        public const float PauseMin = 0.5f, PauseMax = 1.3f;
        /// <summary>The ride slows over this many metres before each end.</summary>
        public const float EaseMetres = 0.35f;
        /// <summary>The slowest it creeps while easing in (m/s), so it always arrives.</summary>
        const float Creep = 0.08f;

        readonly EnemyDef _def;
        readonly Rng _rng;
        readonly float _stretch;
        float _along, _goal, _pause, _speed;
        bool _out;

        /// <param name="railLength">The rail's length (m): the stretch never runs past its end.</param>
        public GondolaIdle(EnemyDef def, float railLength, ulong seed)
        {
            _def = def ?? throw new ArgumentNullException(nameof(def));
            _rng = new Rng(seed, 0x60D01A5C1EA4UL);
            float len = railLength > 0f && !float.IsInfinity(railLength) ? railLength : 0f;
            _stretch = Math.Max(0f, Math.Min(def.idleLeash, len));
            _pause = _rng.Range(0f, PauseMax);   // the gondolas of one wall don't all set off together
            _out = true;
            _goal = FarEnd();
        }

        /// <summary>Whether a bot of <paramref name="def"/> washes while idle (a rail bot with an idle speed and stretch in its data).</summary>
        public static bool Works(EnemyDef def) => def != null && def.layer == ThreatLayer.Walls && def.idleSpeed > 0f && def.idleLeash > 0f;

        /// <summary>Metres along the rail from its spawn (0..the stretch).</summary>
        public float Along => _along;
        /// <summary>The stretch it washes (m).</summary>
        public float Stretch => _stretch;
        /// <summary>Its speed along the rail this step (m/s; negative on the way back toward the spawn).</summary>
        public float Speed => _speed;
        /// <summary>Stopped at an end of the stretch.</summary>
        public bool Pausing => _pause > 0f;

        public void Tick(float dt)
        {
            if (!(dt > 0f) || float.IsInfinity(dt)) return;
            _speed = 0f;
            if (!(_stretch > 0f)) return;
            if (_pause > 0f)
            {
                _pause -= dt;
                return;
            }
            float d = _goal - _along;
            float dist = Math.Abs(d);
            float v = Math.Min(_def.idleSpeed, Math.Max(Creep, _def.idleSpeed * dist / EaseMetres));
            float step = v * dt;
            if (step >= dist)
            {
                _along = _goal;
                _speed = Math.Sign(d) * dist / dt;
                _pause = _rng.Range(PauseMin, PauseMax);
                _out = !_out;
                _goal = _out ? FarEnd() : NearEnd();
                return;
            }
            _along += Math.Sign(d) * step;
            _speed = Math.Sign(d) * v;
        }

        // each pass varies a little, as panes do
        float FarEnd() => _stretch * _rng.Range(0.7f, 1f);
        float NearEnd() => _stretch * _rng.Range(0f, 0.2f);
    }
}
