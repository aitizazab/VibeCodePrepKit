using System;
using System.Numerics;

namespace JamKit.Skyline
{
    /// <summary>How an enemy aims (docs/SKYLINE-DESIGN.md 2.5-2.6). Every enemy carries one.</summary>
    public enum AimModel { LeadsYou, FiresAtWhereYouAre, Area, Melee }

    public static class Aim
    {
        /// <summary>The drone's aim (the creator, 2026-10-14): where the target was <paramref name="lagSeconds"/> ago, from its
        /// <paramref name="trail"/> (<paramref name="now"/> is the trail's clock); the target's position now while the trail is
        /// empty or the lag is 0. No leading: a mover is missed by what it covers in the lag plus the bolt's flight.</summary>
        public static Vector3 LaggedPoint(PositionTrail trail, float now, float lagSeconds, Vector3 targetNow)
        {
            if (trail == null || !(lagSeconds > 0f)) return targetNow;
            return trail.At(now - lagSeconds, targetNow);
        }

        /// <summary>Where a shot at <paramref name="speed"/> m/s must go to meet a target moving at
        /// <paramref name="targetVel"/>, with the velocity scaled by <paramref name="leadFactor"/> (0 = at the target now,
        /// 1 = a perfect intercept). Falls back to the target's position when no intercept exists.</summary>
        public static Vector3 LeadPoint(Vector3 shooter, Vector3 target, Vector3 targetVel, float speed, float leadFactor)
        {
            if (speed <= 0f || leadFactor <= 0f) return target;
            Vector3 v = targetVel * leadFactor;
            Vector3 d = target - shooter;
            float a = Vector3.Dot(v, v) - speed * speed;
            float b = 2f * Vector3.Dot(d, v);
            float c = Vector3.Dot(d, d);
            float t;
            if (MathF.Abs(a) < 1e-6f)
            {
                if (MathF.Abs(b) < 1e-6f) return target;
                t = -c / b;
            }
            else
            {
                float disc = b * b - 4f * a * c;
                if (disc < 0f) return target;
                float s = MathF.Sqrt(disc);
                float t1 = (-b - s) / (2f * a), t2 = (-b + s) / (2f * a);
                t = t1 > 0f && t2 > 0f ? MathF.Min(t1, t2) : MathF.Max(t1, t2);
            }
            if (!(t > 0f) || float.IsInfinity(t)) return target;
            return target + v * t;
        }
    }

    /// <summary>A short history of where a target was, for the drone's lagged aim (docs/SKYLINE-BOTS.md, "How the drone aims"):
    /// <see cref="Record"/> the target's position every tick, then ask <see cref="At"/> where it was a moment ago. A fixed ring,
    /// so recording allocates nothing; a sample that jumps further than <see cref="TeleportDistance"/> (a respawn, a restart) clears
    /// the history so the drone never aims at the place the player teleported away from.</summary>
    public sealed class PositionTrail
    {
        const int Capacity = 128;
        /// <summary>A step longer than this (m) in one sample is a teleport: the history before it is forgotten.</summary>
        public const float TeleportDistance = 8f;

        readonly float[] _t = new float[Capacity];
        readonly Vector3[] _p = new Vector3[Capacity];
        int _newest = -1, _count;

        public int Count => _count;

        public void Clear()
        {
            _newest = -1;
            _count = 0;
        }

        /// <summary>Adds the position at <paramref name="time"/>. A time that hasn't moved on replaces the newest sample, one that
        /// runs backwards (a restarted clock) clears the history, and a position that isn't finite is ignored.</summary>
        public void Record(float time, Vector3 position)
        {
            if (!IsFinite(position) || float.IsNaN(time) || float.IsInfinity(time)) return;
            if (_count > 0)
            {
                float last = _t[_newest];
                if (time < last - 1e-6f) Clear();
                else if (time <= last + 1e-6f) { _p[_newest] = position; return; }
                else if (Vector3.Distance(_p[_newest], position) > TeleportDistance) Clear();
            }
            _newest = (_newest + 1) % Capacity;
            _t[_newest] = time;
            _p[_newest] = position;
            if (_count < Capacity) _count++;
        }

        /// <summary>Where the target was at <paramref name="time"/> (interpolated between the two samples round it). Before the
        /// oldest sample: the oldest; after the newest: the newest; with no history: <paramref name="fallback"/>.</summary>
        public Vector3 At(float time, Vector3 fallback)
        {
            if (_count == 0) return fallback;
            if (time >= _t[_newest]) return _p[_newest];
            int i = _newest;
            for (int n = 1; n < _count; n++)
            {
                int prev = (i + Capacity - 1) % Capacity;
                if (time >= _t[prev])
                {
                    float span = _t[i] - _t[prev];
                    float f = span > 1e-9f ? (time - _t[prev]) / span : 1f;
                    return Vector3.Lerp(_p[prev], _p[i], f);
                }
                i = prev;
            }
            return _p[i];
        }

        static bool IsFinite(Vector3 v) =>
            !(float.IsNaN(v.X) || float.IsInfinity(v.X) || float.IsNaN(v.Y) || float.IsInfinity(v.Y) || float.IsNaN(v.Z) || float.IsInfinity(v.Z));
    }

    /// <summary>The never-mix rule (2.6): an encounter never combines shots that lead you with shots at where you are now
    /// (both continuing and changing direction would be punished). Exceptions (Baja, the end of the game) bypass this.</summary>
    public static class AimRules
    {
        public static bool CanShareEncounter(params AimModel[] models)
        {
            if (models == null) return true;
            bool leads = false, now = false;
            foreach (var m in models)
            {
                if (m == AimModel.LeadsYou) leads = true;
                if (m == AimModel.FiresAtWhereYouAre) now = true;
            }
            return !(leads && now);
        }
    }

    /// <summary>The spotlight's shot (2.5): a telegraph of <see cref="telegraph"/> seconds whose beam follows the target with a
    /// lag, locks <see cref="lockBefore"/> seconds before the shot, then a hitscan where the beam is. The beam is a
    /// critically damped follow with the target's velocity fed forward, so it settles onto a steady run; at the lock it
    /// keeps sweeping at the velocity it had. Running straight stays in the beam; changing direction in the last
    /// <see cref="lockBefore"/> seconds leaves it. Line of sight is the caller's: a shot that fires behind cover misses.</summary>
    public sealed class TelegraphedShot
    {
        public float telegraph = 0.8f;
        public float lockBefore = 0.25f;
        /// <summary>How tightly the beam tracks (1/s): it settles onto a steady run within about 4 / followRate seconds.</summary>
        public float followRate = 10f;
        /// <summary>How far from the beam's point still counts as a hit (m).</summary>
        public float hitRadius = 0.6f;

        float _t;
        Vector3 _aim, _aimVel, _lastTarget;

        public bool Active { get; private set; }
        public bool Locked { get; private set; }
        public Vector3 AimPoint => _aim;
        /// <summary>How fast the beam's point is sweeping (m/s).</summary>
        public Vector3 AimVelocity => _aimVel;

        public void Start(Vector3 targetNow)
        {
            Active = true;
            Locked = false;
            _t = 0f;
            _aim = targetNow;
            _aimVel = Vector3.Zero;
            _lastTarget = targetNow;
        }

        /// <summary>The fastest the beam's point may sweep, and the fastest target velocity it will believe (m/s); well above the
        /// player's top speed, so a teleport or a bad frame can't throw the beam.</summary>
        public const float MaxBeamSpeed = 40f;
        /// <summary>The longest stretch of one step the beam integrates (s); a longer hitch still advances the telegraph's clock.</summary>
        public const float MaxIntegrateSeconds = 0.5f;

        /// <summary>Advances the telegraph. Returns true on the step the shot fires; <paramref name="hit"/> says whether it
        /// landed. A step that isn't a positive number changes nothing; a target that isn't a finite point is ignored for that
        /// step (the beam coasts, the clock still runs, and a shot fired then misses).</summary>
        public bool Step(float dt, Vector3 target, out bool hit)
        {
            hit = false;
            if (!Active || !(dt > 0f) || float.IsInfinity(dt)) return false;
            _t += dt;
            bool good = IsFinite(target);
            float idt = Math.Min(dt, MaxIntegrateSeconds);
            if (!Locked)
            {
                if (good)
                {
                    Vector3 targetVel = ClampSpeed((target - _lastTarget) / dt);
                    float w = followRate;
                    int n = Math.Min(256, Math.Max(1, (int)MathF.Ceiling(w * idt / 0.1f)));   // sub-steps keep it stable at any frame rate
                    float h = idt / n;
                    for (int i = 0; i < n; i++)
                    {
                        Vector3 p = Vector3.Lerp(_lastTarget, target, (i + 1f) / n);
                        Vector3 acc = w * w * (p - _aim) + 2f * w * (targetVel - _aimVel);
                        _aimVel = ClampSpeed(_aimVel + acc * h);
                        _aim += _aimVel * h;
                    }
                }
                else _aim += _aimVel * idt;
                if (_t >= telegraph - lockBefore) Locked = true;
            }
            else
            {
                _aim += _aimVel * idt;
            }
            if (good) _lastTarget = target;
            if (_t >= telegraph)
            {
                Active = false;
                hit = good && Vector3.Distance(target, _aim) <= hitRadius;
                return true;
            }
            return false;
        }

        static bool IsFinite(Vector3 v) =>
            !(float.IsNaN(v.X) || float.IsInfinity(v.X) || float.IsNaN(v.Y) || float.IsInfinity(v.Y) || float.IsNaN(v.Z) || float.IsInfinity(v.Z));

        static Vector3 ClampSpeed(Vector3 v)
        {
            float len = v.Length();
            if (float.IsNaN(len)) return Vector3.Zero;
            return len > MaxBeamSpeed ? v * (MaxBeamSpeed / len) : v;
        }
    }
}
