using System;

namespace JamKit.Movement
{
    /// <summary>
    /// PLACEHOLDER speed music, made in code so the stems' behaviour can be judged before the real music exists (it
    /// is composed with the creator; drop the real stems in as docs/MOVEMENT.md "Speed music" says). Three stems of
    /// one length that loop without a seam: <b>base</b> (a soft pad and a sub bass), <b>drive</b> (an eighth-note
    /// bass pulse, a kick, hats and a clap) and <b>peak</b> (a bright sixteenth-note arpeggio and a high shimmer).
    /// D minor, i-VI-III-VII (Dm9, Bbmaj7, F add9, Csus2), 104 bpm, four bars of 4/4: the same tune, tempo and loop
    /// length as the dream escape's chase music, so the escape can mix these with its own. Every note's tail wraps
    /// around the loop.
    /// </summary>
    public static class MoveMusicSynth
    {
        public const int Rate = 32000;
        public const float Bpm = 104f;
        public const int Bars = 4;
        public const int BeatsPerBar = 4;

        public static float BeatSeconds => 60f / Bpm;
        public static int LoopSamples => (int)(BeatSeconds * 4 * Bars * Rate);

        static readonly int[][] Chords =
        {
            new[] { 50, 53, 57, 64 },
            new[] { 46, 50, 53, 57 },
            new[] { 53, 57, 60, 67 },
            new[] { 48, 50, 55, 64 },
        };

        /// <summary>The samples of a stem slot (<see cref="StemMixer.Base"/>, Drive or Peak), or null (no tension placeholder).</summary>
        public static float[] Make(int slot)
        {
            switch (slot)
            {
                case StemMixer.Base: return Base();
                case StemMixer.Drive: return Drive();
                case StemMixer.Peak: return Peak();
                default: return null;
            }
        }

        static double Hz(int midi) => 440.0 * Math.Pow(2.0, (midi - 69) / 12.0);

        static void Add(float[] buf, int at, float v)
        {
            int n = buf.Length;
            int i = at % n;
            if (i < 0) i += n;
            buf[i] += v;
        }

        static float[] Base()
        {
            int n = LoopSamples, bar = n / Bars;
            var buf = new float[n];
            int attack = (int)(0.5f * Rate), release = (int)(1.0f * Rate);
            for (int b = 0; b < Bars; b++)
            {
                int start = b * bar, len = bar + release;
                foreach (int note in Chords[b])
                    for (int voice = -1; voice <= 1; voice += 2)
                    {
                        double w = 2.0 * Math.PI * Hz(note) * (1.0 + voice * 0.0018) / Rate;
                        for (int i = 0; i < len; i++)
                        {
                            float env = i < attack ? i / (float)attack : i < bar ? 1f : 1f - (i - bar) / (float)release;
                            Add(buf, start + i, (float)(Math.Sin(w * i) + 0.1 * Math.Sin(3.0 * w * i)) * env * 0.04f);
                        }
                    }
                // Sub bass: the root two octaves down, a whole note.
                double wb = 2.0 * Math.PI * Hz(Chords[b][0] - 24) / Rate;
                for (int i = 0; i < len; i++)
                {
                    float env = Math.Min(1f, i / (0.05f * Rate)) * (i < bar ? 1f : 1f - (i - bar) / (float)release);
                    Add(buf, start + i, (float)Math.Sin(wb * i) * env * 0.16f);
                }
            }
            return Normalize(buf, 0.5f);
        }

        static float[] Drive()
        {
            int n = LoopSamples, bar = n / Bars, eighth = bar / 8;
            var buf = new float[n];
            uint seed = 12345u;
            for (int b = 0; b < Bars; b++)
            {
                double w = 2.0 * Math.PI * Hz(Chords[b][0] - 12) / Rate;
                for (int k = 0; k < 8; k++)
                {
                    int start = b * bar + k * eighth;
                    // The bass pulse: a softened saw, plucked.
                    float lp = 0f;
                    for (int i = 0; i < (int)(eighth * 0.9f); i++)
                    {
                        float t = i / (float)Rate;
                        float env = Math.Min(1f, t / 0.005f) * (float)Math.Exp(-t * 8f);
                        float saw = (float)(Math.Sin(w * i) + 0.5 * Math.Sin(2 * w * i) + 0.33 * Math.Sin(3 * w * i) + 0.25 * Math.Sin(4 * w * i));
                        lp += (saw - lp) * 0.3f;
                        Add(buf, start + i, lp * env * 0.22f);
                    }
                    if (k % 2 == 0)
                    {
                        // Kick on the beats.
                        double ph = 0;
                        for (int i = 0; i < (int)(0.18f * Rate); i++)
                        {
                            float t = i / (float)Rate;
                            ph += 2.0 * Math.PI * (48.0 + 80.0 * Math.Exp(-t * 32.0)) / Rate;
                            Add(buf, start + i, (float)Math.Sin(ph) * (float)Math.Exp(-t * 16f) * 0.6f);
                        }
                    }
                    // Hats on every eighth (accented off-beats), a clap on beats 2 and 4.
                    float hp = 0f, prev = 0f;
                    float hatVol = k % 2 == 1 ? 0.12f : 0.06f;
                    for (int i = 0; i < (int)(0.05f * Rate); i++)
                    {
                        float t = i / (float)Rate;
                        float x = Noise(ref seed);
                        hp = 0.8f * (hp + x - prev);
                        prev = x;
                        Add(buf, start + i, hp * (float)Math.Exp(-t * 80f) * hatVol);
                    }
                    if (k == 2 || k == 6)
                    {
                        float bp = 0f, bp2 = 0f;
                        for (int i = 0; i < (int)(0.15f * Rate); i++)
                        {
                            float t = i / (float)Rate;
                            float x = Noise(ref seed);
                            bp += (x - bp) * 0.35f;
                            bp2 += (bp - bp2) * 0.35f;
                            Add(buf, start + i, (bp - bp2) * (float)Math.Exp(-t * 28f) * 0.5f);
                        }
                    }
                }
            }
            return Normalize(buf, 0.55f);
        }

        static float[] Peak()
        {
            int n = LoopSamples, bar = n / Bars, six = bar / 16;
            var buf = new float[n];
            int[] pattern = { 0, 1, 2, 3, 2, 3, 1, 2, 0, 2, 3, 1, 3, 2, 1, 3 };
            for (int b = 0; b < Bars; b++)
            {
                // The arpeggio: chord tones two octaves up, a bright plucked tone.
                for (int k = 0; k < 16; k++)
                {
                    double w = 2.0 * Math.PI * Hz(Chords[b][pattern[k]] + 24) / Rate;
                    int start = b * bar + k * six;
                    float vel = k % 4 == 0 ? 1f : k % 2 == 0 ? 0.8f : 0.65f;
                    for (int i = 0; i < (int)(0.45f * Rate); i++)
                    {
                        float t = i / (float)Rate;
                        float env = Math.Min(1f, t / 0.003f) * (float)Math.Exp(-t * 7f);
                        float tone = (float)(Math.Sin(w * i) + 0.35 * Math.Sin(2 * w * i) * Math.Exp(-t * 6.0) + 0.15 * Math.Sin(3 * w * i) * Math.Exp(-t * 10.0));
                        Add(buf, start + i, tone * env * vel * 0.1f);
                    }
                }
                // The shimmer: the chord three octaves up, trembling in eighths.
                int len = bar;
                foreach (int note in Chords[b])
                {
                    double w = 2.0 * Math.PI * Hz(note + 36) / Rate;
                    for (int i = 0; i < len; i++)
                    {
                        float x = i / (float)len;
                        float env = (float)Math.Sin(Math.PI * x);
                        float trem = 0.6f + 0.4f * (float)Math.Sin(2.0 * Math.PI * 8.0 * x);
                        Add(buf, b * bar + i, (float)Math.Sin(w * i) * env * trem * 0.012f);
                    }
                }
            }
            return Normalize(buf, 0.45f);
        }

        static float Noise(ref uint s)
        {
            s ^= s << 13;
            s ^= s >> 17;
            s ^= s << 5;
            return (s >> 8) / 8388608f - 1f;
        }

        static float[] Normalize(float[] b, float peak)
        {
            MoveSounds.Normalize(b, peak);
            return b;
        }
    }
}
