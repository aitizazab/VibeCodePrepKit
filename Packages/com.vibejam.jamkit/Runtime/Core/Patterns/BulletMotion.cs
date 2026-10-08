using System;
using System.Numerics;

namespace JamKit.Patterns
{
    /// <summary>
    /// Bullet movement with constant acceleration (along the heading) and a constant turn rate.
    /// <see cref="Step"/> is the exact closed-form solution, not an Euler step, so one 0.1 s step
    /// lands where ten 0.01 s steps do. That makes the pooled bullets frame-rate independent and
    /// lets the emitter pre-advance a late-spawned bullet by its age with the very same call.
    /// Angles are radians, counter-clockwise from +X. Cost: two sin/cos pairs per call.
    /// </summary>
    public static class BulletMotion
    {
        // Below this much turn per step, use a Taylor series: the exact form divides by the turn
        // rate and loses precision to cancellation as it approaches zero.
        const double SeriesTurn = 1e-2;

        public static Vector2 Direction(float angle) =>
            new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));

        /// <summary>Advance one bullet by dt seconds. Deceleration brings a bullet to rest and holds
        /// it there (it keeps turning on the spot); it never flies backwards.</summary>
        public static void Step(ref Vector2 position, ref float angle, ref float speed,
            float acceleration, float angularVelocity, float dt)
        {
            if (dt <= 0f) return;
            double v0 = speed, a = acceleration, w = angularVelocity, t = dt;

            double tMove = t;
            if (a < 0) tMove = v0 > 0 ? Math.Min(t, -v0 / a) : 0;

            if (tMove > 0)
            {
                // Displacement in the bullet's starting frame: integral of (v0 + a s)(cos ws, sin ws) ds.
                double turn = w * tMove, fx, fy;
                if (Math.Abs(turn) < SeriesTurn)
                {
                    double t2 = tMove * tMove, turn2 = turn * turn;
                    fx = v0 * tMove * (1 - turn2 / 6) + a * t2 * (0.5 - turn2 / 8);
                    fy = v0 * w * t2 * (0.5 - turn2 / 24) + a * w * t2 * tMove * (1.0 / 3 - turn2 / 30);
                }
                else
                {
                    double s = Math.Sin(turn), c = Math.Cos(turn), inv = 1 / w;
                    fx = v0 * s * inv + a * (tMove * s * inv - (1 - c) * inv * inv);
                    fy = v0 * (1 - c) * inv + a * (s * inv * inv - tMove * c * inv);
                }
                double c0 = Math.Cos(angle), s0 = Math.Sin(angle);
                position.X += (float)(fx * c0 - fy * s0);
                position.Y += (float)(fx * s0 + fy * c0);
            }

            angle = (float)(angle + w * t);
            double v = v0 + a * t;
            speed = (float)(a < 0 && v < 0 ? 0 : v);
        }
    }
}
