using System;
using System.Collections.Generic;
using System.Numerics;

namespace JamKit.Doomlike
{
    /// <summary>
    /// The numbers behind a shot: damage falloff with distance, deterministic pellet spread (the
    /// same seed gives the same pattern, so tests and replays agree), random damage rolls and
    /// splash damage. Angles in, radians out.
    /// </summary>
    public static class HitscanMath
    {
        /// <summary>Damage factor at <paramref name="distance"/>: 1 up to falloffStart, then down in a
        /// straight line to minFactor at range, 0 beyond range (range 0 or less = unlimited).</summary>
        public static float Falloff(float distance, float range, float falloffStart, float minFactor)
        {
            if (range > 0f && distance > range) return 0f;
            if (distance <= falloffStart) return 1f;
            if (range <= 0f || range <= falloffStart) return 1f;
            float t = (distance - falloffStart) / (range - falloffStart);
            if (t > 1f) t = 1f;
            float min = minFactor < 0f ? 0f : minFactor > 1f ? 1f : minFactor;
            return 1f + (min - 1f) * t;
        }

        /// <summary>
        /// Clears <paramref name="output"/> and adds one (yaw, pitch) offset in radians per pellet,
        /// spread uniformly over an ellipse of the given full widths in degrees. With
        /// <paramref name="firstPelletCentered"/>, pellet 0 flies dead centre (a pistol's first shot,
        /// the shotgun's core). Draws two numbers per pellet from <paramref name="rng"/>.
        /// </summary>
        public static void PelletOffsets(int pellets, float spreadDegrees, float verticalSpreadDegrees, Rng rng,
            List<Vector2> output, bool firstPelletCentered = false)
        {
            if (output == null) throw new ArgumentNullException(nameof(output));
            output.Clear();
            int n = Math.Max(1, pellets);
            float hx = Math.Max(0f, spreadDegrees) * 0.5f * GridMath.Deg;
            float hy = Math.Max(0f, verticalSpreadDegrees) * 0.5f * GridMath.Deg;
            for (int i = 0; i < n; i++)
            {
                float u = rng.NextFloat();
                float v = rng.NextFloat();
                if (i == 0 && firstPelletCentered) { output.Add(Vector2.Zero); continue; }
                float r = (float)Math.Sqrt(u);
                float a = v * GridMath.TwoPi;
                output.Add(new Vector2((float)Math.Cos(a) * r * hx, (float)Math.Sin(a) * r * hy));
            }
        }

        /// <summary>damage x (1 +- variance), e.g. variance 0.2 gives 80%..120%.</summary>
        public static float Roll(float damage, float variance, Rng rng)
        {
            if (variance <= 0f || rng == null) return damage;
            return damage * (1f + (rng.NextFloat() * 2f - 1f) * variance);
        }

        /// <summary>Splash damage at <paramref name="distance"/> from the blast: full at the centre,
        /// down to <paramref name="edgeFactor"/> at the radius, none beyond it.</summary>
        public static float Splash(float damage, float radius, float distance, float edgeFactor = 0.25f)
        {
            if (radius <= 0f || distance > radius) return 0f;
            float t = distance / radius;
            return damage * (1f + (edgeFactor - 1f) * t);
        }

        /// <summary>The direction for a (yaw, pitch) offset around a forward direction with the given
        /// right and up axes (all unit length).</summary>
        public static Vector3 Offset(Vector3 forward, Vector3 right, Vector3 up, Vector2 offset)
        {
            Vector3 d = forward + right * (float)Math.Tan(offset.X) + up * (float)Math.Tan(offset.Y);
            return Vector3.Normalize(d);
        }

        /// <summary>Chance that an enemy's hitscan shot at a moving target lands: accuracy at point
        /// blank, falling off with distance and with the target's speed (strafing works).</summary>
        public static float EnemyHitChance(float baseAccuracy, float distance, float targetSpeed,
            float distanceForHalf = 20f, float speedForHalf = 12f)
        {
            float a = baseAccuracy < 0f ? 0f : baseAccuracy > 1f ? 1f : baseAccuracy;
            float d = distanceForHalf > 0f ? 1f / (1f + Math.Max(0f, distance) / distanceForHalf) : 1f;
            float s = speedForHalf > 0f ? 1f / (1f + Math.Max(0f, targetSpeed) / speedForHalf) : 1f;
            return a * d * s;
        }
    }
}
