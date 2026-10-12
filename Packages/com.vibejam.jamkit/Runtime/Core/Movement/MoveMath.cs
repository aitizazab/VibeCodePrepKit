using System;
using System.Numerics;

namespace JamKit.Movement
{
    /// <summary>
    /// The Quake/Source movement formulas (docs/RESEARCH-MOVEMENT.md 2.1 and 4.2) and small vector
    /// helpers. Pure functions, no allocation.
    /// </summary>
    public static class MoveMath
    {
        public static readonly Vector3 Up = new Vector3(0f, 1f, 0f);

        /// <summary>Source <c>Accelerate</c>: adds speed along <paramref name="wishDir"/> until the
        /// projection of the velocity on it reaches <paramref name="wishSpeed"/>. Never pushes that
        /// projection past wishSpeed, and never slows you down.</summary>
        public static Vector3 Accelerate(Vector3 v, Vector3 wishDir, float wishSpeed, float accel, float dt)
        {
            float add = wishSpeed - Vector3.Dot(v, wishDir);
            if (add <= 0f) return v;
            return v + wishDir * Math.Min(accel * wishSpeed * dt, add);
        }

        /// <summary>Source <c>AirAccelerate</c>: the same, but the target projection is capped at
        /// <paramref name="cap"/> while the step size still uses the uncapped wish speed. Turning
        /// while strafing keeps the projection small, so speed builds up (air strafing).</summary>
        public static Vector3 AirAccelerate(Vector3 v, Vector3 wishDir, float wishSpeed, float accel, float cap, float dt)
        {
            float add = Math.Min(wishSpeed, cap) - Vector3.Dot(v, wishDir);
            if (add <= 0f) return v;
            return v + wishDir * Math.Min(accel * wishSpeed * dt, add);
        }

        /// <summary>Source <c>Friction</c> on a horizontal velocity: proportional to speed, constant below stopSpeed.</summary>
        public static Vector3 Friction(Vector3 vH, float friction, float stopSpeed, float dt)
        {
            float s = vH.Length();
            if (s < 1e-4f) return Vector3.Zero;
            float drop = Math.Max(s, stopSpeed) * friction * dt;
            return vH * (Math.Max(s - drop, 0f) / s);
        }

        /// <summary>Removes the part of <paramref name="v"/> going into the surface (keeps the rest:
        /// grazing a wall keeps the speed along it).</summary>
        public static Vector3 Clip(Vector3 v, Vector3 n)
        {
            float d = Vector3.Dot(v, n);
            return d < 0f ? v - n * d : v;
        }

        public static Vector3 Flat(Vector3 v) => new Vector3(v.X, 0f, v.Z);

        public static float FlatLength(Vector3 v) => (float)Math.Sqrt(v.X * v.X + v.Z * v.Z);

        /// <summary>The horizontal unit vector a yaw (degrees, Unity convention) looks along.</summary>
        public static Vector3 Forward(float yawDegrees)
        {
            double r = yawDegrees * Math.PI / 180.0;
            return new Vector3((float)Math.Sin(r), 0f, (float)Math.Cos(r));
        }

        /// <summary>Right of a horizontal direction (Unity's left-handed axes: right of +Z is +X).</summary>
        public static Vector3 RightOf(Vector3 dir) => new Vector3(dir.Z, 0f, -dir.X);

        /// <summary>Yaw (degrees) of a horizontal direction.</summary>
        public static float YawOf(Vector3 dir) => (float)(Math.Atan2(dir.X, dir.Z) * 180.0 / Math.PI);

        public static Vector3 NormalizeOr(Vector3 v, Vector3 fallback)
        {
            float l = v.Length();
            return l > 1e-5f ? v / l : fallback;
        }

        /// <summary>Turns the horizontal direction <paramref name="from"/> toward <paramref name="to"/> by at most <paramref name="maxRadians"/>.</summary>
        public static Vector3 RotateToward(Vector3 from, Vector3 to, float maxRadians)
        {
            float a = (float)Math.Atan2(from.X, from.Z);
            float b = (float)Math.Atan2(to.X, to.Z);
            float d = b - a;
            while (d > Math.PI) d -= (float)(2 * Math.PI);
            while (d < -Math.PI) d += (float)(2 * Math.PI);
            if (d > maxRadians) d = maxRadians;
            else if (d < -maxRadians) d = -maxRadians;
            float r = a + d;
            return new Vector3((float)Math.Sin(r), 0f, (float)Math.Cos(r));
        }

        public static float Clamp01(float x) => x < 0f ? 0f : x > 1f ? 1f : x;

        public static float Lerp(float a, float b, float t) => a + (b - a) * t;

        public static float SmoothStep(float e0, float e1, float x)
        {
            float t = Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        /// <summary>Caps the horizontal part of a velocity.</summary>
        public static Vector3 CapFlat(Vector3 v, float max)
        {
            float s = FlatLength(v);
            if (s <= max || s < 1e-5f) return v;
            float k = max / s;
            return new Vector3(v.X * k, v.Y, v.Z * k);
        }
    
        /// <summary>
        /// A critically damped spring towards <paramref name="target"/> (angular frequency <paramref name="w"/>), stepped
        /// exactly: the same curve whatever the frame length. (The camera's landing dip used a per-frame Euler step,
        /// which on a long frame at a hard landing overshot the wrong way by up to a third of a metre: 2026-10-12.)
        /// </summary>
        public static void CriticalSpring(ref float x, ref float v, float target, float w, float dt)
        {
            if (dt <= 0f) return;
            float x0 = x - target;
            float c = v + w * x0;
            float e = (float)Math.Exp(-w * dt);
            x = target + (x0 + c * dt) * e;
            v = (v - w * c * dt) * e;
        }

        /// <summary>
        /// A damped spring (any damping ratio) stepped in pieces of at most <paramref name="maxStep"/> s, so a long
        /// frame can't make it overshoot or blow up. No allocation.
        /// </summary>
        public static void Spring(ref float x, ref float v, float target, float w, float damping, float dt, float maxStep = 1f / 240f)
        {
            if (dt <= 0f) return;
            int n = Math.Min(64, Math.Max(1, (int)Math.Ceiling(dt / maxStep)));
            float h = dt / n;
            for (int i = 0; i < n; i++)
            {
                v += (w * w * (target - x) - 2f * damping * w * v) * h;
                x += v * h;
            }
        }
    }
}
