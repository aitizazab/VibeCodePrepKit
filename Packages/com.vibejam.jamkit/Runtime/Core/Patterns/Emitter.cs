using System;
using System.Collections.Generic;
using System.Numerics;

namespace JamKit.Patterns
{
    /// <summary>
    /// Plays a <see cref="PatternDef"/> or <see cref="PatternSequence"/>: each <see cref="Tick"/>
    /// appends the bullets that fell due during it. Frame-rate independent: volleys fire at their
    /// scheduled times (not "this frame"), and a bullet scheduled partway through a tick comes out
    /// already advanced by its age, so 30 fps, 240 fps and one long hitch give the same bullets.
    /// Volleys from overlapping steps fire in time order, so the seeded rolls (jitter, shotgun)
    /// don't depend on the frame rate either.
    /// The definition is read as each volley fires, so inspector tweaks show live; call
    /// <see cref="Reset"/> after changing timing or adding steps.
    /// </summary>
    public sealed class Emitter
    {
        /// <summary>Shortest repeat period, so a zero-length endless or looping pattern can't make
        /// one Tick fire forever.</summary>
        public const float MinRepeatInterval = 0.001f;
        const double Deg = Math.PI / 180.0;

        readonly PatternSequence _sequence;
        readonly ulong _seed;
        Rng _rng;
        PatternDef[] _defs;
        double[] _offsets;
        int[] _nextVolley;
        int[] _pass;
        bool[] _done;
        double _period;
        double _time;
        float _speedScale = 1f;
        float _densityScale = 1f;

        public Emitter(PatternDef pattern, Rng rng) : this(PatternSequence.Single(pattern), rng) { }

        /// <summary>The emitter draws its own seed from <paramref name="rng"/> once, so
        /// <see cref="Reset"/> replays the same bullets. A null rng means seed 0.</summary>
        public Emitter(PatternSequence sequence, Rng rng)
        {
            _sequence = sequence ?? throw new ArgumentNullException(nameof(sequence));
            _seed = rng == null ? 0UL : ((ulong)rng.NextUInt() << 32) | rng.NextUInt();
            Reset();
        }

        /// <summary>Seconds since start (or the last <see cref="Reset"/>).</summary>
        public float Time => (float)_time;

        /// <summary>True once every volley has fired. Endless and looping emitters never finish.</summary>
        public bool Finished
        {
            get
            {
                for (int i = 0; i < _done.Length; i++)
                    if (!_done[i]) return false;
                return true;
            }
        }

        /// <summary>Difficulty assist: scales bullet pace while keeping each path's shape (speed x k,
        /// acceleration x k^2, turn rate x k, lifetime / k). Volley timing is unchanged. Min 0.01.</summary>
        public float SpeedScale
        {
            get => _speedScale;
            set => _speedScale = Math.Max(0.01f, value);
        }

        /// <summary>Difficulty assist: scales bullets per volley (see <see cref="ScaledCount"/>).</summary>
        public float DensityScale
        {
            get => _densityScale;
            set => _densityScale = Math.Max(0f, value);
        }

        /// <summary>count x density, rounded half away from zero, never below 1.</summary>
        public static int ScaledCount(int count, float density)
        {
            int n = (int)Math.Round(Math.Max(1, count) * (double)density, MidpointRounding.AwayFromZero);
            return Math.Max(1, n);
        }

        public void Reset()
        {
            var steps = _sequence.steps;
            int n = steps.Count;
            if (_defs == null || _defs.Length != n)
            {
                _defs = new PatternDef[n];
                _offsets = new double[n];
                _nextVolley = new int[n];
                _pass = new int[n];
                _done = new bool[n];
            }
            for (int i = 0; i < n; i++)
            {
                var step = steps[i];
                _defs[i] = step?.pattern;
                _offsets[i] = step == null ? 0 : Math.Max(0f, step.time);
                _nextVolley[i] = 0;
                _pass[i] = 0;
                _done[i] = _defs[i] == null;
            }
            float duration = _sequence.Duration;
            _period = _sequence.loop && !float.IsInfinity(duration)
                ? Math.Max(MinRepeatInterval, duration + Math.Max(0f, _sequence.loopDelay))
                : double.PositiveInfinity;
            _time = 0;
            _rng = new Rng(_seed);
        }

        /// <summary>Advance by dt and append every bullet scheduled up to the new time. origin and
        /// target are sampled now, at the end of the tick. Allocation-free as long as
        /// <paramref name="output"/> has room (pre-size it).</summary>
        public void Tick(float dt, Vector2 origin, Vector2 target, List<BulletSpawn> output)
        {
            double end = _time + Math.Max(0f, dt);
            while (true)
            {
                int best = -1;
                double bestTime = double.PositiveInfinity;
                for (int s = 0; s < _defs.Length; s++)
                {
                    if (_done[s]) continue;
                    double t = VolleyTime(s);
                    if (t <= end && t < bestTime) { best = s; bestTime = t; }
                }
                if (best < 0) break;
                FireVolley(_defs[best], _nextVolley[best], (float)(end - bestTime), origin, target, output);
                AdvanceStep(best);
            }
            _time = end;
        }

        double VolleyTime(int s)
        {
            var def = _defs[s];
            double interval = def.shots <= 0
                ? Math.Max(MinRepeatInterval, def.shotInterval)
                : Math.Max(0f, def.shotInterval);
            double passStart = _pass[s] == 0 ? 0 : _pass[s] * _period;
            return passStart + _offsets[s] + Math.Max(0f, def.startDelay) + _nextVolley[s] * interval;
        }

        void AdvanceStep(int s)
        {
            var def = _defs[s];
            _nextVolley[s]++;
            if (def.shots <= 0 || _nextVolley[s] < def.shots) return;
            if (double.IsInfinity(_period)) { _done[s] = true; return; }
            _pass[s]++;
            _nextVolley[s] = 0;
        }

        void FireVolley(PatternDef def, int volley, float age, Vector2 origin, Vector2 target,
            List<BulletSpawn> output)
        {
            int n = ScaledCount(def.count, _densityScale);
            float k = _speedScale;
            float accel = def.acceleration * k * k;
            float turnRate = (float)(def.angularVelocityDegrees * Deg) * k;
            float lifetime = def.lifetime > 0f ? def.lifetime / k : def.lifetime;

            // The mirror axis excludes the per-volley rotation, so a mirrored spiral counter-rotates.
            double axis = def.baseAngleDegrees * Deg;
            if (def.aimAtTarget || def.shape == PatternShape.Aimed)
            {
                Vector2 to = target - origin;
                if (to.LengthSquared() > 0f) axis += Math.Atan2(to.Y, to.X);
            }
            double centre = axis + def.rotatePerShotDegrees * Deg * volley;
            double spread = def.spreadDegrees * Deg;
            double jitter = def.jitterDegrees * Deg;
            Vector2 axisDir = BulletMotion.Direction((float)axis);
            Vector2 centreDir = BulletMotion.Direction((float)centre);
            Vector2 side = new Vector2(-centreDir.Y, centreDir.X);

            for (int b = 0; b < n; b++)
            {
                double angle;
                switch (def.shape)
                {
                    case PatternShape.Ring:
                    case PatternShape.Spiral:
                        angle = centre + 2 * Math.PI * b / n;
                        break;
                    case PatternShape.Spread:
                    case PatternShape.Aimed:
                        angle = n == 1 ? centre : centre - spread / 2 + spread * b / (n - 1);
                        break;
                    case PatternShape.Shotgun:
                        angle = centre + _rng.Range(-0.5f, 0.5f) * spread;
                        break;
                    default:
                        angle = centre;
                        break;
                }
                if (jitter > 0) angle += _rng.Range(-1f, 1f) * jitter;

                Vector2 offset;
                if (def.shape == PatternShape.Line)
                {
                    float lateral = n == 1 ? 0f : def.lineWidth * ((float)b / (n - 1) - 0.5f);
                    offset = centreDir * def.spawnRadius + side * lateral;
                }
                else
                {
                    offset = BulletMotion.Direction((float)angle) * def.spawnRadius;
                }

                float speed = (def.speed + def.speedStep * b) * k;
                Emit(output, origin + offset, (float)angle, speed, accel, turnRate, lifetime, def.styleIndex, age);
                if (def.mirror)
                {
                    Vector2 reflected = 2f * Vector2.Dot(offset, axisDir) * axisDir - offset;
                    Emit(output, origin + reflected, (float)(2 * axis - angle), speed, accel, -turnRate,
                        lifetime, def.styleIndex, age);
                }
            }
        }

        static void Emit(List<BulletSpawn> output, Vector2 position, float angle, float speed, float accel,
            float turnRate, float lifetime, int style, float age)
        {
            var spawn = new BulletSpawn
            {
                position = position,
                angle = angle,
                speed = speed,
                acceleration = accel,
                angularVelocity = turnRate,
                lifetime = lifetime,
                styleIndex = style,
                age = age,
            };
            BulletMotion.Step(ref spawn.position, ref spawn.angle, ref spawn.speed, accel, turnRate, age);
            output.Add(spawn);
        }
    }
}
