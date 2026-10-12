using UnityEngine;

namespace JamKit.Look
{
    /// <summary>
    /// One master level for the whole ambience (the bed and every <see cref="VhAmbienceEmitter"/>), with a timed fade, so a
    /// beat can pull the Saturday down under dialogue or let it swell back without finding every source. Read by the
    /// components each frame; no allocation.
    /// </summary>
    public static class VhAmbienceMix
    {
        static float s_from = 1f;
        static float s_to = 1f;
        static float s_start;
        static float s_duration;

        /// <summary>The master level right now (0..1).</summary>
        public static float Level
        {
            get
            {
                if (s_duration <= 0f) return s_to;
                float u = Mathf.Clamp01((Time.time - s_start) / s_duration);
                return Mathf.Lerp(s_from, s_to, u * u * (3f - 2f * u));
            }
        }

        /// <summary>Fade the master level to <paramref name="level"/> over <paramref name="seconds"/> (0 = at once).</summary>
        public static void FadeTo(float level, float seconds)
        {
            s_from = Level;
            s_to = Mathf.Clamp01(level);
            s_start = Time.time;
            s_duration = Mathf.Max(0f, seconds);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            s_from = s_to = 1f;
            s_duration = 0f;
        }
    }
}
