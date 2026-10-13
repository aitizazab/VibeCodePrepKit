using System;

namespace JamKit.Skyline
{
    /// <summary>
    /// A hand on a mouse, for the arena's scripted demo (fix round "the demo plays like a human", 2026-10-14). The demo used to
    /// set its view straight onto the target every frame (an instant flick, perfect tracking), which the creator read as a
    /// turret. This turns the view toward what it wants to look at like a player's wrist: an underdamped spring (a fast turn
    /// that overshoots a little and settles), a cap on the turn rate, a short reaction beat before it starts toward a new
    /// target, and so a small lag behind anything that moves. Angles are degrees, Unity's convention (yaw round up, pitch
    /// positive looking down). Pure and deterministic: the same inputs give the same turn.
    /// <para>Defaults: a 60 degree switch is on target (within 2 degrees, and stays there) in about 0.27 s plus the reaction
    /// beat, overshooting by about 3.5 degrees; a 120 degree turn takes about 0.35 s; a target crossing at 30 degrees a
    /// second is followed about 2 degrees behind.</para>
    /// </summary>
    public sealed class HumanAim
    {
        /// <summary>The spring's natural frequency (rad/s): how hard the wrist pulls toward the target.</summary>
        public float frequency = 20f;
        /// <summary>The spring's damping ratio: under 1 overshoots a little and settles.</summary>
        public float damping = 0.65f;
        /// <summary>The fastest the view turns (degrees a second, yaw and pitch together).</summary>
        public float maxRate = 650f;
        /// <summary>The step the spring is integrated at (s), whatever the frame time.</summary>
        public const float SubStep = 1f / 240f;

        float _hold;

        public float Yaw { get; private set; }
        public float Pitch { get; private set; }
        public float YawRate { get; private set; }
        public float PitchRate { get; private set; }
        /// <summary>The turn rate now (degrees a second).</summary>
        public float Rate => MathF.Sqrt(YawRate * YawRate + PitchRate * PitchRate);
        /// <summary>The reaction beat still to run (s): the view holds its course until it ends.</summary>
        public float Holding => _hold;

        /// <summary>Puts the view at (<paramref name="yaw"/>, <paramref name="pitch"/>) at rest (a restart, a scripted pose).</summary>
        public void Snap(float yaw, float pitch)
        {
            Yaw = Wrap(yaw);
            Pitch = pitch;
            YawRate = PitchRate = 0f;
            _hold = 0f;
        }

        /// <summary>A new target: the view keeps its course for <paramref name="seconds"/> (a player's reaction) before it turns.</summary>
        public void React(float seconds) => _hold = Math.Max(_hold, seconds);

        /// <summary>One frame toward (<paramref name="wantYaw"/>, <paramref name="wantPitch"/>).</summary>
        public void Step(float dt, float wantYaw, float wantPitch)
        {
            if (!(dt > 0f) || float.IsInfinity(dt)) return;
            int n = Math.Max(1, (int)MathF.Ceiling(dt / SubStep));
            float h = dt / n;
            float w2 = frequency * frequency, c = 2f * damping * frequency;
            for (int i = 0; i < n; i++)
            {
                if (_hold > 0f)
                {
                    // the reaction beat: no new pull, the turn already under way runs on and damps out
                    _hold -= h;
                    YawRate -= c * YawRate * h * 0.5f;
                    PitchRate -= c * PitchRate * h * 0.5f;
                }
                else
                {
                    float ey = DeltaAngle(Yaw, wantYaw), ep = wantPitch - Pitch;
                    YawRate += (w2 * ey - c * YawRate) * h;
                    PitchRate += (w2 * ep - c * PitchRate) * h;
                }
                float rate = MathF.Sqrt(YawRate * YawRate + PitchRate * PitchRate);
                if (rate > maxRate)
                {
                    float k = maxRate / rate;
                    YawRate *= k;
                    PitchRate *= k;
                }
                Yaw = Wrap(Yaw + YawRate * h);
                Pitch += PitchRate * h;
            }
        }

        /// <summary>How far (degrees) the view is from (<paramref name="yaw"/>, <paramref name="pitch"/>).</summary>
        public float ErrorTo(float yaw, float pitch)
        {
            float ey = DeltaAngle(Yaw, yaw), ep = pitch - Pitch;
            return MathF.Sqrt(ey * ey + ep * ep);
        }

        /// <summary>The shortest signed turn from <paramref name="from"/> to <paramref name="to"/> (degrees, -180..180).</summary>
        public static float DeltaAngle(float from, float to)
        {
            float d = (to - from) % 360f;
            if (d > 180f) d -= 360f;
            else if (d < -180f) d += 360f;
            return d;
        }

        static float Wrap(float a)
        {
            a %= 360f;
            return a < 0f ? a + 360f : a;
        }
    }
}
