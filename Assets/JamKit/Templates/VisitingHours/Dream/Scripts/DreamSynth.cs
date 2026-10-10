using System;
using UnityEngine;

namespace JamKit.DreamEscape
{
    /// <summary>
    /// PLACEHOLDER chase music and the static's buzz, synthesised once in code (the real score is composed with
    /// the creator). Three loops of the same length that play in sync: a soft pad (always), a pulse with a
    /// little kick (louder as the static closes in) and a music box (it goes out of tune when the static is
    /// close: <see cref="DreamChaseMusic"/> detunes it). D minor, i-VI-III-VII, 104 bpm, four bars. Each loop
    /// wraps its tails around so it repeats without a click. Cached for the session.
    /// </summary>
    public static class DreamSynth
    {
        public const int Rate = 32000;
        public const float Bpm = 104f;
        public const int Bars = 4;

        static AudioClip _pad, _pulse, _box, _buzz;

        public static float BeatSeconds => 60f / Bpm;
        public static int LoopSamples => (int)(BeatSeconds * 4 * Bars * Rate);

        // The chords, in MIDI notes: Dm9, Bbmaj7, F(add9), Csus2.
        static readonly int[][] Chords =
        {
            new[] { 50, 53, 57, 64 },
            new[] { 46, 50, 53, 57 },
            new[] { 53, 57, 60, 67 },
            new[] { 48, 50, 55, 64 },
        };

        static float Hz(int midi) => 440f * Mathf.Pow(2f, (midi - 69) / 12f);

        public static AudioClip Pad => _pad != null ? _pad : (_pad = Make("dream_chase_pad", BuildPad()));
        public static AudioClip Pulse => _pulse != null ? _pulse : (_pulse = Make("dream_chase_pulse", BuildPulse()));
        public static AudioClip MusicBox => _box != null ? _box : (_box = Make("dream_chase_box", BuildBox()));
        public static AudioClip Buzz => _buzz != null ? _buzz : (_buzz = Make("dream_wasp_buzz", BuildBuzz()));

        static AudioClip Make(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static void Add(float[] buf, int at, float v)
        {
            int n = buf.Length;
            int i = at % n;
            if (i < 0) i += n;
            buf[i] += v;
        }

        static float[] BuildPad()
        {
            int n = LoopSamples;
            var buf = new float[n];
            int bar = n / Bars;
            int attack = (int)(0.6f * Rate), release = (int)(0.9f * Rate);
            for (int b = 0; b < Bars; b++)
            {
                int start = b * bar;
                int len = bar + release;
                foreach (int note in Chords[b])
                {
                    float f = Hz(note);
                    for (int voice = -1; voice <= 1; voice += 2)
                    {
                        float ff = f * (1f + voice * 0.0016f);
                        double w = 2.0 * Math.PI * ff / Rate;
                        for (int i = 0; i < len; i++)
                        {
                            float env = i < attack ? i / (float)attack : i < bar ? 1f : 1f - (i - bar) / (float)release;
                            float tri = (float)(Math.Sin(w * i) + 0.12 * Math.Sin(3.0 * w * i));
                            Add(buf, start + i, tri * env * 0.045f);
                        }
                    }
                }
            }
            return Normalise(buf, 0.55f);
        }

        static float[] BuildPulse()
        {
            int n = LoopSamples;
            var buf = new float[n];
            int bar = n / Bars;
            int eighth = bar / 8;
            for (int b = 0; b < Bars; b++)
            {
                float f = Hz(Chords[b][0] - 12);
                double w = 2.0 * Math.PI * f / Rate;
                for (int k = 0; k < 8; k++)
                {
                    int start = b * bar + k * eighth;
                    int len = (int)(eighth * 0.95f);
                    float lp = 0f;
                    for (int i = 0; i < len; i++)
                    {
                        float t = i / (float)Rate;
                        float env = Mathf.Min(1f, t / 0.006f) * Mathf.Exp(-t * 7f);
                        float saw = (float)(Math.Sin(w * i) + 0.5 * Math.Sin(2 * w * i) + 0.33 * Math.Sin(3 * w * i) + 0.25 * Math.Sin(4 * w * i));
                        lp += (saw - lp) * 0.25f;
                        Add(buf, start + i, lp * env * 0.32f);
                    }
                    // A soft kick on the beats, a breath of noise on the off-beats.
                    if (k % 2 == 0)
                    {
                        double ph = 0;
                        for (int i = 0; i < (int)(0.16f * Rate); i++)
                        {
                            float t = i / (float)Rate;
                            float kf = 45f + 70f * Mathf.Exp(-t * 30f);
                            ph += 2.0 * Math.PI * kf / Rate;
                            Add(buf, start + i, (float)Math.Sin(ph) * Mathf.Exp(-t * 18f) * 0.55f);
                        }
                    }
                    else
                    {
                        var rng = new System.Random(b * 31 + k);
                        float hp = 0f, prev = 0f;
                        for (int i = 0; i < (int)(0.05f * Rate); i++)
                        {
                            float t = i / (float)Rate;
                            float x = (float)rng.NextDouble() * 2f - 1f;
                            hp = 0.85f * (hp + x - prev);
                            prev = x;
                            Add(buf, start + i, hp * Mathf.Exp(-t * 70f) * 0.12f);
                        }
                    }
                }
            }
            return Normalise(buf, 0.6f);
        }

        static float[] BuildBox()
        {
            int n = LoopSamples;
            var buf = new float[n];
            int bar = n / Bars;
            int six = bar / 16;
            int[] pattern = { 0, 1, 2, 3, 2, 1, 3, 2, 0, 2, 1, 3, 2, 3, 1, 2 };
            for (int b = 0; b < Bars; b++)
            {
                for (int k = 0; k < 16; k++)
                {
                    if (k % 4 == 3 && b % 2 == 1) continue; // breathe
                    int note = Chords[b][pattern[k]] + 24;
                    float f = Hz(note);
                    double w = 2.0 * Math.PI * f / Rate;
                    int start = b * bar + k * six;
                    float vel = k % 4 == 0 ? 1f : 0.7f;
                    for (int i = 0; i < (int)(0.9f * Rate); i++)
                    {
                        float t = i / (float)Rate;
                        float env = Mathf.Min(1f, t / 0.002f) * Mathf.Exp(-t * 4.2f);
                        float tone = (float)(Math.Sin(w * i) + 0.22 * Math.Sin(2.0 * w * i) * Math.Exp(-t * 9.0) + 0.08 * Math.Sin(5.4 * w * i) * Math.Exp(-t * 20.0));
                        Add(buf, start + i, tone * env * vel * 0.12f);
                    }
                }
            }
            return Normalise(buf, 0.5f);
        }

        static float[] BuildBuzz()
        {
            int n = (int)(1.2f * Rate);
            var buf = new float[n];
            var rng = new System.Random(7);
            double ph = 0, ph2 = 0;
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float vib = 8f * Mathf.Sin(2f * Mathf.PI * 25f * t);
                ph += 2.0 * Math.PI * (190.0 + vib) / Rate;
                ph2 += 2.0 * Math.PI * (285.0 + vib * 1.5) / Rate;
                float saw = (float)(Math.Sin(ph) + 0.5 * Math.Sin(2 * ph) + 0.3 * Math.Sin(3 * ph) + 0.4 * Math.Sin(ph2));
                float noise = (float)rng.NextDouble() * 2f - 1f;
                lp += (noise - lp) * 0.3f;
                float am = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * 5f * t);
                buf[i] = (saw * 0.6f + lp * 0.5f) * am;
            }
            return Normalise(buf, 0.5f);
        }

        static float[] Normalise(float[] buf, float peak)
        {
            float max = 1e-4f;
            for (int i = 0; i < buf.Length; i++) max = Math.Max(max, Math.Abs(buf[i]));
            float k = peak / max;
            for (int i = 0; i < buf.Length; i++) buf[i] *= k;
            return buf;
        }
    }
}
