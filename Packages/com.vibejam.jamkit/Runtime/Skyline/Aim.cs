using System;
using System.Numerics;

namespace JamKit.Skyline
{
    /// <summary>How an enemy aims (docs/SKYLINE-DESIGN.md 2.5-2.6). Every enemy carries one.</summary>
    public enum AimModel { LeadsYou, FiresAtWhereYouAre, Area, Melee }

    public static class Aim
    {
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

        /// <summary>Advances the telegraph. Returns true on the step the shot fires; <paramref name="hit"/> says whether it
        /// landed. A step that isn't a positive number changes nothing.</summary>
        public bool Step(float dt, Vector3 target, out bool hit)
        {
            hit = false;
            if (!Active || !(dt > 0f) || float.IsInfinity(dt)) return false;
            _t += dt;
            if (!Locked)
            {
                Vector3 targetVel = (target - _lastTarget) / dt;
                float w = followRate;
                int n = Math.Min(64, Math.Max(1, (int)MathF.Ceiling(w * dt / 0.1f)));   // sub-steps keep it stable at any frame rate
                float h = dt / n;
                for (int i = 0; i < n; i++)
                {
                    Vector3 p = Vector3.Lerp(_lastTarget, target, (i + 1f) / n);
                    Vector3 acc = w * w * (p - _aim) + 2f * w * (targetVel - _aimVel);
                    _aimVel += acc * h;
                    _aim += _aimVel * h;
                }
                if (_t >= telegraph - lockBefore) Locked = true;
            }
            else
            {
                _aim += _aimVel * dt;
            }
            _lastTarget = target;
            if (_t >= telegraph)
            {
                Active = false;
                hit = Vector3.Distance(target, _aim) <= hitRadius;
                return true;
            }
            return false;
        }
    }
}
