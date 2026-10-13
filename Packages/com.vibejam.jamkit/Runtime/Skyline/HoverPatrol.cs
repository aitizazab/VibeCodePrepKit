using System;
using System.Numerics;

namespace JamKit.Skyline
{
    /// <summary>
    /// The mall-cop drone's bounded hover patrol (fix round "the drones move", 2026-10-14; the main session's ruling): a
    /// flying bot never hangs still. Around its post (its spawn, or wherever its search has taken it) it bobs gently and,
    /// while awake, strafes sideways across the line to its target in legs of a metre or three at <see cref="EnemyDef.patrolSpeed"/>,
    /// never further than <see cref="EnemyDef.patrolLeash"/> from the post; after each volley it starts a new leg, and a hit
    /// sends it on a short dash (<see cref="EnemyDef.patrolDashSpeed"/>) to the other side. Asleep it only drifts, slowly and
    /// close. Its measured placement (posts, ranges, bearings from the entry anchor) is unchanged: the patrol is an offset on
    /// top of the post, so the search rule and the balance's spawn data stay as they were.
    /// <para>Pure and seeded (its own stream, so it never shifts the enemies' aim errors): FightSim and the Unity drone
    /// (SkylineBot) run this same code with their own cover test, so the headless fight and the game move alike.</para>
    /// </summary>
    public sealed class HoverPatrol
    {
        /// <summary>How fast the strafe speeds up and slows down (m/s²): a drone banks into a leg, it doesn't teleport.</summary>
        public const float Accel = 9f;
        /// <summary>One bob up and down (s).</summary>
        public const float BobPeriod = 1.9f;
        /// <summary>Asleep (idle, not yet woken) its speed and leash shrink to this share: a gentle drift.</summary>
        public const float AsleepScale = 0.35f;
        /// <summary>While it telegraphs (the charge ball grows) it slows to this share of its speed, so the charge reads.</summary>
        public const float TelegraphSlow = 0.5f;
        /// <summary>A volley starts a new leg only once the current one is this old (s), so a fast fire cycle doesn't jitter it.</summary>
        public const float MinLegSeconds = 0.45f;
        /// <summary>How long a hit's dash lasts (s).</summary>
        public const float DashSeconds = 0.6f;
        /// <summary>A leg shorter than this (m) isn't worth flying.</summary>
        const float MinLegLength = 0.7f;

        readonly EnemyDef _def;
        readonly Rng _rng;
        Vector3 _flat, _vel, _leg;
        float _legAge, _pause, _dash, _bobPhase;
        bool _hasLeg, _repick, _forced;
        int _side;

        public HoverPatrol(EnemyDef def, ulong seed)
        {
            _def = def ?? throw new ArgumentNullException(nameof(def));
            _rng = new Rng(seed, 0x4E7A11C0FFEEUL);
            _bobPhase = _rng.Range(0f, 2f * MathF.PI);
            _side = _rng.NextFloat() < 0.5f ? -1 : 1;
        }

        /// <summary>Whether a bot of <paramref name="def"/> patrols (a speed and a leash in its data).</summary>
        public static bool Moves(EnemyDef def) => def != null && def.patrolSpeed > 0f && def.patrolLeash > 0f;

        /// <summary>Where it is relative to its post (m): the strafe on the floor plan plus the bob's height.</summary>
        public Vector3 Offset => new Vector3(_flat.X, Bob, _flat.Z);
        /// <summary>The strafe alone (y is 0).</summary>
        public Vector3 Flat => _flat;
        /// <summary>The strafe's velocity (m/s, y is 0).</summary>
        public Vector3 Velocity => _vel;
        public float Bob => _def.patrolBob * MathF.Sin(_bobPhase);
        /// <summary>The leg it is flying (an offset from the post), while it has one.</summary>
        public bool HasLeg => _hasLeg;
        public Vector3 Leg => _leg;
        public bool Dashing => _dash > 0f;

        /// <summary>It just fired: the next step starts a new leg (once the current one is <see cref="MinLegSeconds"/> old).</summary>
        public void OnVolley() => _repick = true;

        /// <summary>It was hit: a short dash to a new spot, at once.</summary>
        public void OnHit()
        {
            _dash = DashSeconds;
            _forced = true;
            _pause = 0f;
        }

        /// <summary>
        /// One step. <paramref name="post"/> is where the bot's logic has it (its spawn, or its search point);
        /// <paramref name="toTarget"/> points at what it shoots at (zero: none, it picks its own way); <paramref name="blocked"/>
        /// says whether a sphere of the given radius moved between two world points touches cover (null: open air).
        /// </summary>
        public void Tick(float dt, Vector3 post, bool awake, bool telegraphing, Vector3 toTarget, Func<Vector3, Vector3, float, bool> blocked)
        {
            if (!(dt > 0f) || float.IsInfinity(dt)) return;
            float r = _def.bodyRadius;
            float leash = _def.patrolLeash * (awake ? 1f : AsleepScale);
            float speed = _def.patrolSpeed * (awake ? 1f : AsleepScale);
            float accel = Accel;
            if (_dash > 0f)
            {
                _dash -= dt;
                speed = Math.Max(speed, _def.patrolDashSpeed);
                accel = Accel * 1.8f;
            }
            else if (telegraphing) speed *= TelegraphSlow;

            Vector3 oldOffset = Offset;
            _bobPhase += dt * (2f * MathF.PI / BobPeriod);
            if (_bobPhase > 2f * MathF.PI) _bobPhase -= 2f * MathF.PI;

            // the post moved under cover (a search leg, a knock): ease back over the post, which is always clear
            Vector3 here = post + oldOffset;
            if (blocked != null && blocked(here, here, r))
            {
                _flat = MoveTowards(_flat, Vector3.Zero, _def.patrolSpeed * dt);
                _vel = Vector3.Zero;
                _hasLeg = false;
                return;
            }

            _legAge += dt;
            bool want = !_hasLeg && _pause <= 0f;
            if (_forced) want = true;
            else if (_repick && _hasLeg && _legAge >= MinLegSeconds) want = true;
            if (Length(_flat) > leash + 0.05f && !_hasLeg) want = true;   // fell asleep far out: come back in
            if (want)
            {
                _repick = false;
                _forced = false;
                PickLeg(post, leash, toTarget, blocked, oldOffset, awake);
            }
            if (_pause > 0f) _pause -= dt;

            Vector3 desired = Vector3.Zero;
            if (_hasLeg)
            {
                Vector3 to = _leg - _flat;
                float d = Length(to);
                if (d < 0.05f)
                {
                    _hasLeg = false;
                    _pause = awake ? _rng.Range(0.05f, 0.25f) : _rng.Range(0.5f, 1.4f);
                }
                else desired = to / d * Math.Min(speed, d * 3.5f);   // eases in at the end of a leg
            }
            _vel = MoveTowards(_vel, desired, accel * dt);
            Vector3 next = _flat + _vel * dt;
            float len = Length(next);
            float hard = _def.patrolLeash;   // the leash never stretches, awake or not
            if (len > hard)
            {
                next *= hard / len;
                _vel = Vector3.Zero;
            }
            Vector3 nextOffset = new Vector3(next.X, Bob, next.Z);
            if (blocked != null && blocked(post + oldOffset, post + nextOffset, r))
            {
                // cover in the way: stop, and go the other way next
                _vel = Vector3.Zero;
                _hasLeg = false;
                _side = -_side;
                _pause = 0.1f;
                return;
            }
            _flat = next;
        }

        void PickLeg(Vector3 post, float leash, Vector3 toTarget, Func<Vector3, Vector3, float, bool> blocked, Vector3 from, bool awake)
        {
            var flatT = new Vector3(toTarget.X, 0f, toTarget.Z);
            float tl = Length(flatT);
            Vector3 fwd, lat;
            if (tl > 1e-3f && awake)
            {
                fwd = flatT / tl;
                lat = new Vector3(fwd.Z, 0f, -fwd.X);   // across the line to the target
            }
            else
            {
                float a = _rng.Range(0f, 2f * MathF.PI);
                lat = new Vector3(MathF.Sin(a), 0f, MathF.Cos(a));
                fwd = new Vector3(-lat.Z, 0f, lat.X);
            }
            bool dash = _dash > 0f;
            float minLeg = dash ? leash * 0.8f : MinLegLength;   // a hit sends it well clear, not a twitch
            for (int attempt = 0; attempt < 6; attempt++)
            {
                int side = attempt % 2 == 0 ? -_side : _side;   // prefer the other side: a strafe, not a creep
                float along = side * leash * (dash ? _rng.Range(0.8f, 1f) : _rng.Range(0.45f, 1f));
                float depth = leash * _rng.Range(-0.35f, 0.35f);
                Vector3 c = lat * along + fwd * depth;
                float cl = Length(c);
                if (cl > leash) c *= leash / cl;
                if (Length(c - _flat) < minLeg) continue;
                if (blocked != null && blocked(post + from, post + new Vector3(c.X, from.Y, c.Z), _def.bodyRadius)) continue;
                _leg = c;
                _side = side;
                _hasLeg = true;
                _legAge = 0f;
                return;
            }
            // nowhere clear to go: back over the post, or hold a moment
            if (Length(_flat) > 0.1f && (blocked == null || !blocked(post + from, post + new Vector3(0f, from.Y, 0f), _def.bodyRadius)))
            {
                _leg = Vector3.Zero;
                _hasLeg = true;
                _legAge = 0f;
            }
            else _pause = 0.2f;
        }

        /// <summary>The flight area's rule (the search's: inside the hall less 1 m, south of the mezzanine's edge, so a drone
        /// never tucks itself under the deck): a move from <paramref name="a"/> to <paramref name="b"/> that ends outside the
        /// box (x, z) and further outside than it started. A post that is already outside (a test's frame) may still patrol
        /// round itself; it just can't wander further out.</summary>
        public static bool LeavesArea(Vector3 a, Vector3 b, float minX, float maxX, float minZ, float maxZ)
        {
            float ob = Outside(b, minX, maxX, minZ, maxZ);
            return ob > 0f && ob > Outside(a, minX, maxX, minZ, maxZ) + 1e-5f;
        }

        static float Outside(Vector3 p, float minX, float maxX, float minZ, float maxZ) =>
            Math.Max(0f, Math.Max(minX - p.X, p.X - maxX)) + Math.Max(0f, Math.Max(minZ - p.Z, p.Z - maxZ));

        static float Length(Vector3 v) => MathF.Sqrt(v.X * v.X + v.Z * v.Z);

        static Vector3 MoveTowards(Vector3 a, Vector3 b, float maxStep)
        {
            Vector3 d = b - a;
            float l = d.Length();
            if (l <= maxStep || l < 1e-6f) return b;
            return a + d / l * maxStep;
        }
    }
}
