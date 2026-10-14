using System;
using System.Collections.Generic;

namespace JamKit.Skyline
{
    /// <summary>One bot as the music sees it: what it is, how far from the player, whether it is alerted.</summary>
    public struct ThreatSample
    {
        public string enemyId;
        public float distance;
        public bool alerted;

        public ThreatSample(string enemyId, float distance, bool alerted)
        {
            this.enemyId = enemyId;
            this.distance = distance;
            this.alerted = alerted;
        }
    }

    /// <summary>The threat the music reacts to (docs/SKYLINE-MUSIC.md, adaptive music): alerted bots in range, each weighted
    /// by its type and by how near it is (full weight within <see cref="NearRange"/>, fading to nothing at
    /// <see cref="FarRange"/>), summed and saturated into 0..1 (<see cref="FullAt"/> weighted bots near is full danger). Unalerted
    /// bots count for nothing. Starting numbers, tuned with the creator.</summary>
    public static class ThreatScore
    {
        public const float NearRange = 12f, FarRange = 40f, FullAt = 4f;

        /// <summary>The weight of one near, alerted bot of this type.</summary>
        public static float WeightOf(string enemyId)
        {
            switch (enemyId)
            {
                case SkylineEnemies.SpotlightId: return 1.5f;   // a hitscan at where you are now
                case SkylineEnemies.GondolaId: return 0.7f;     // area denial, slow
                default: return 1f;                              // drone, floor-care and anything new
            }
        }

        public static float Compute(IReadOnlyList<ThreatSample> bots)
        {
            if (bots == null) return 0f;
            float sum = 0f;
            for (int i = 0; i < bots.Count; i++)
            {
                var b = bots[i];
                if (!b.alerted || b.distance >= FarRange) continue;
                float near = b.distance <= NearRange ? 1f : 1f - (b.distance - NearRange) / (FarRange - NearRange);
                sum += WeightOf(b.enemyId) * near;
            }
            float s = sum / FullAt;
            return s > 1f ? 1f : s;
        }
    }

    /// <summary>A value that eases toward a target in seconds, not frames: an exponential approach, so it covers 63% of the
    /// distance in <c>seconds</c> however the time is cut into frames (the same trip taken in one step or in a hundred ends
    /// at the same value). (Not a second-order critically damped spring: that would not reach 63% at one time constant.)</summary>
    public sealed class LayerSmoother
    {
        public float Value { get; private set; }

        public LayerSmoother(float start = 0f) { Value = start; }

        public void Set(float v) { Value = v; }

        /// <summary>Moves toward <paramref name="target"/>; <paramref name="seconds"/> at or under 0 snaps. A negative
        /// or zero <paramref name="dt"/> changes nothing.</summary>
        public void Tick(float target, float dt, float seconds)
        {
            if (dt <= 0f) return;
            if (seconds <= 0f) { Value = target; return; }
            Value += (target - Value) * (1f - (float)Math.Exp(-dt / seconds));
        }

        /// <summary>Rises with <paramref name="riseSeconds"/>, falls with <paramref name="fallSeconds"/>.</summary>
        public void Tick(float target, float dt, float riseSeconds, float fallSeconds)
        {
            Tick(target, dt, target >= Value ? riseSeconds : fallSeconds);
        }
    }

    /// <summary>The vertical layers over the always-playing base stem (docs/SKYLINE-MUSIC.md): <see cref="motion"/> picks up
    /// with the player's speed and settles when they stop; <see cref="danger"/> follows the threat score and lingers after
    /// it; <see cref="light"/> comes up when the threat is low or the place is traversal (parkour between sections). All 0..1,
    /// all smoothed over seconds so a single bot never flickers the music. The numbers are data for the tuning pass.</summary>
    public sealed class MusicLayers
    {
        /// <summary>m/s at or under which the motion layer is silent, and at or over which it is full.</summary>
        public const float MotionIdleSpeed = 2f, MotionFullSpeed = 8f;
        /// <summary>The threat at or above which light is out, and at or below which it is full.</summary>
        public const float LightOutAtThreat = 0.5f, LightFullBelowThreat = 0.1f;
        public const float MotionRiseSeconds = 1.5f, MotionFallSeconds = 3f;
        public const float DangerRiseSeconds = 2f, DangerFallSeconds = 6f;
        public const float LightRiseSeconds = 4f, LightFallSeconds = 2f;

        public float motion, danger, light;

        readonly LayerSmoother _motion = new LayerSmoother(), _danger = new LayerSmoother(), _light = new LayerSmoother();

        /// <summary>Settles back to silence (a wreck, a restart).</summary>
        public void Reset()
        {
            _motion.Set(0f); _danger.Set(0f); _light.Set(0f);
            motion = danger = light = 0f;
        }

        /// <param name="speed">The player's speed (m/s).</param>
        /// <param name="threat"><see cref="ThreatScore.Compute"/>, 0..1.</param>
        /// <param name="traversal">True in a section that is parkour rather than a fight.</param>
        public void Tick(float speed, float threat, bool traversal, float dt)
        {
            float m = Saturate((speed - MotionIdleSpeed) / (MotionFullSpeed - MotionIdleSpeed));
            float d = Saturate(threat);
            float l = traversal ? 1f : Saturate((LightOutAtThreat - d) / (LightOutAtThreat - LightFullBelowThreat));
            _motion.Tick(m, dt, MotionRiseSeconds, MotionFallSeconds);
            _danger.Tick(d, dt, DangerRiseSeconds, DangerFallSeconds);
            _light.Tick(l, dt, LightRiseSeconds, LightFallSeconds);
            motion = _motion.Value;
            danger = _danger.Value;
            light = _light.Value;
        }

        static float Saturate(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
