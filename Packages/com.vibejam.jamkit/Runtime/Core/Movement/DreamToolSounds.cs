using System;

namespace JamKit.Movement
{
    /// <summary>
    /// The dream tool's placeholder sounds, made from code (pure C#, no Unity; the Movement Lab's DreamTool plays them):
    /// the pulse leaving her hand, the lock tick, a glass tile popping, a socket unfolding, the colour flip, and the soft
    /// puff of a shot at nothing. Mono, <see cref="Rate"/> Hz, faded at both ends (no clicks), peaking at -3 dBFS.
    /// Replace any of them with a recorded clip under the same id in the game's Audio Registry.
    /// </summary>
    public static class DreamToolSounds
    {
        public const int Rate = 32000;
        public const float Peak = 0.708f; // -3 dBFS

        public static readonly string[] Ids = { "dt_fire", "dt_lock", "dt_pop", "dt_build", "dt_flip", "dt_dry" };

        public static bool Knows(string id) => Array.IndexOf(Ids, id) >= 0;

        /// <summary>The samples of one sound, or null for an unknown id.</summary>
        public static float[] Make(string id)
        {
            uint seed = 0x2545F491u;
            switch (id)
            {
                case "dt_fire":
                {
                    // A soft "fwip" of light: a sine sweeping down from 1.4 kHz with a bright shimmer, over a breath of noise.
                    var b = new float[N(0.28f)];
                    Sweep(b, 0f, 0.22f, 1400f, 520f, 0.55f, 0.004f, 0.18f);
                    Sweep(b, 0f, 0.16f, 2800f, 1500f, 0.18f, 0.002f, 0.12f);
                    Noise(b, 0f, 0.12f, 0.6f, 0.25f, 0.001f, 0.1f, ref seed);
                    return Finish(b);
                }
                case "dt_lock":
                {
                    // A tiny two-note tick when the aim locks onto something new.
                    var b = new float[N(0.12f)];
                    Sweep(b, 0f, 0.05f, 2200f, 2200f, 0.35f, 0.002f, 0.04f);
                    Sweep(b, 0.045f, 0.06f, 2950f, 2950f, 0.3f, 0.002f, 0.05f);
                    return Finish(b, 0.45f);
                }
                case "dt_pop":
                {
                    // Glass popping: a crack of bright noise, then scattered tinkles (short high sines) falling away.
                    var b = new float[N(0.7f)];
                    Noise(b, 0f, 0.09f, 0.85f, 0.9f, 0.001f, 0.08f, ref seed);
                    Sweep(b, 0f, 0.1f, 180f, 90f, 0.35f, 0.002f, 0.09f);
                    for (int i = 0; i < 9; i++)
                    {
                        float at = 0.02f + 0.06f * i + 0.03f * Rand(ref seed);
                        float f = 2600f + 2600f * Rand(ref seed);
                        Sweep(b, at, 0.18f, f, f * 0.98f, 0.22f * (1f - i / 11f), 0.001f, 0.16f);
                    }
                    return Finish(b);
                }
                case "dt_build":
                {
                    // Unfolding: a rising whoosh of filtered noise over a climbing tone, landing on a soft wooden clunk.
                    var b = new float[N(0.75f)];
                    Noise(b, 0f, 0.42f, 0.3f, 0.55f, 0.25f, 0.12f, ref seed);
                    Sweep(b, 0f, 0.42f, 220f, 660f, 0.3f, 0.2f, 0.15f);
                    Sweep(b, 0.4f, 0.3f, 130f, 70f, 0.75f, 0.003f, 0.26f);
                    Noise(b, 0.4f, 0.06f, 0.5f, 0.35f, 0.001f, 0.05f, ref seed);
                    Sweep(b, 0.42f, 0.3f, 990f, 990f, 0.15f, 0.003f, 0.27f);
                    return Finish(b);
                }
                case "dt_flip":
                {
                    // The dream flips: two bell tones a fifth apart, swapped quickly, with a soft swell under them.
                    var b = new float[N(0.65f)];
                    Bell(b, 0f, 0.5f, 660f, 0.45f);
                    Bell(b, 0.08f, 0.5f, 990f, 0.4f);
                    Sweep(b, 0f, 0.4f, 110f, 165f, 0.3f, 0.12f, 0.25f);
                    return Finish(b);
                }
                case "dt_dry":
                {
                    // A shot at nothing: a short, dull puff.
                    var b = new float[N(0.18f)];
                    Sweep(b, 0f, 0.12f, 700f, 380f, 0.4f, 0.003f, 0.1f);
                    Noise(b, 0f, 0.1f, 0.35f, 0.3f, 0.001f, 0.08f, ref seed);
                    return Finish(b, 0.5f);
                }
            }
            return null;
        }

        static int N(float seconds) => (int)(seconds * Rate);

        static float Rand(ref uint s)
        {
            s ^= s << 13; s ^= s >> 17; s ^= s << 5;
            return (s & 0xFFFFFF) / (float)0x1000000;
        }

        /// <summary>A sine gliding from f0 to f1 (exponentially), with an attack and a release (seconds).</summary>
        static void Sweep(float[] b, float at, float len, float f0, float f1, float amp, float attack, float release)
        {
            int start = (int)(at * Rate), n = (int)(len * Rate);
            double phase = 0.0;
            for (int i = 0; i < n && start + i < b.Length; i++)
            {
                float t = i / (float)Rate, k = i / (float)Math.Max(1, n - 1);
                float f = f0 * (float)Math.Pow(f1 / f0, k);
                phase += 2.0 * Math.PI * f / Rate;
                b[start + i] += (float)Math.Sin(phase) * amp * Env(t, len, attack, release);
            }
        }

        /// <summary>A bell: a decaying sine with a slightly sharp, quieter partial.</summary>
        static void Bell(float[] b, float at, float len, float f, float amp)
        {
            int start = (int)(at * Rate), n = (int)(len * Rate);
            for (int i = 0; i < n && start + i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float e = (float)Math.Exp(-t * 7.0) * Math.Min(1f, t / 0.002f);
                b[start + i] += amp * e * ((float)Math.Sin(2.0 * Math.PI * f * t) + 0.3f * (float)Math.Sin(2.0 * Math.PI * f * 2.76 * t));
            }
        }

        /// <summary>Noise through a one-pole low-pass (<paramref name="bright"/> 0 dull .. 1 bright).</summary>
        static void Noise(float[] b, float at, float len, float amp, float bright, float attack, float release, ref uint seed)
        {
            int start = (int)(at * Rate), n = (int)(len * Rate);
            float a = 0.05f + 0.9f * bright, y = 0f;
            for (int i = 0; i < n && start + i < b.Length; i++)
            {
                float x = Rand(ref seed) * 2f - 1f;
                y += a * (x - y);
                b[start + i] += y * amp * Env(i / (float)Rate, len, attack, release);
            }
        }

        static float Env(float t, float len, float attack, float release)
        {
            float a = attack > 0f ? Math.Min(1f, t / attack) : 1f;
            float r = release > 0f ? Math.Min(1f, Math.Max(0f, (len - t) / release)) : 1f;
            return a * r;
        }

        /// <summary>Normalises to the peak (times <paramref name="level"/>) and fades the last 5 ms out.</summary>
        static float[] Finish(float[] b, float level = 1f)
        {
            float peak = 1e-6f;
            for (int i = 0; i < b.Length; i++) peak = Math.Max(peak, Math.Abs(b[i]));
            float g = Peak * level / peak;
            int fade = Math.Min(b.Length, (int)(0.005f * Rate));
            for (int i = 0; i < b.Length; i++)
            {
                float k = 1f;
                if (i < 32) k = i / 32f;
                int fromEnd = b.Length - 1 - i;
                if (fromEnd < fade) k *= fromEnd / (float)fade;
                b[i] *= g * k;
            }
            return b;
        }
    }
}
