using System;

namespace JamKit.Audio
{
    public enum Waveform { Sine, Square, Triangle, Saw, Noise }

    /// <summary>
    /// A tiny synthesiser for placeholder sounds, so a game has real audio before any asset exists.
    /// Fills float sample buffers (mono, -1..1); the Unity side wraps them in AudioClips.
    /// Deterministic: noise uses a seed, so the same recipe always makes the same sound.
    /// </summary>
    public static class ToneSynth
    {
        /// <summary>One note: a pitch sweep from startHz to endHz with an attack/decay envelope.</summary>
        public struct Note
        {
            public Waveform wave;
            public float startHz;
            public float endHz;
            public float seconds;
            public float volume;
            /// <summary>Seconds to fade in; the rest decays to silence.</summary>
            public float attack;
            /// <summary>Square-wave duty cycle, 0..1 (0.5 = classic square).</summary>
            public float duty;

            public Note(Waveform wave, float startHz, float endHz, float seconds, float volume = 0.5f, float attack = 0.005f)
            {
                this.wave = wave;
                this.startHz = startHz;
                this.endHz = endHz;
                this.seconds = seconds;
                this.volume = volume;
                this.attack = attack;
                duty = 0.5f;
            }
        }

        public static int SampleCount(float seconds, int sampleRate) =>
            seconds > 0f ? (int)Math.Ceiling(seconds * sampleRate) : 0;

        /// <summary>Renders notes one after another into a new buffer.</summary>
        public static float[] Sequence(int sampleRate, uint seed, params Note[] notes)
        {
            int total = 0;
            foreach (var n in notes) total += SampleCount(n.seconds, sampleRate);
            var buffer = new float[total];
            int offset = 0;
            uint state = seed == 0 ? 1u : seed;
            foreach (var n in notes)
            {
                Render(buffer, offset, n, sampleRate, ref state, true);
                offset += SampleCount(n.seconds, sampleRate);
            }
            return buffer;
        }

        /// <summary>Adds (mixes) a note into the buffer starting at <paramref name="offset"/>, clipped to the buffer.</summary>
        public static void Mix(float[] buffer, int offset, Note n, int sampleRate, uint seed = 1u, bool envelope = true)
        {
            uint state = seed == 0 ? 1u : seed;
            Render(buffer, offset, n, sampleRate, ref state, envelope);
        }

        static void Render(float[] buffer, int offset, Note n, int sampleRate, ref uint state, bool envelope)
        {
            int count = SampleCount(n.seconds, sampleRate);
            double phase = 0.0;
            float duty = n.duty > 0f && n.duty < 1f ? n.duty : 0.5f;
            float noiseHold = 0f;
            for (int i = 0; i < count; i++)
            {
                int at = offset + i;
                if (at < 0) continue;
                if (at >= buffer.Length) break;
                float t = (float)i / sampleRate;
                float k = count > 1 ? (float)i / (count - 1) : 0f;
                double hz = n.startHz + (n.endHz - n.startHz) * k;
                double prev = phase;
                phase += hz / sampleRate;
                phase -= Math.Floor(phase);
                float v;
                switch (n.wave)
                {
                    case Waveform.Square: v = phase < duty ? 1f : -1f; break;
                    case Waveform.Triangle: v = (float)(phase < 0.5 ? 4.0 * phase - 1.0 : 3.0 - 4.0 * phase); break;
                    case Waveform.Saw: v = (float)(2.0 * phase - 1.0); break;
                    case Waveform.Noise:
                        // New random value each cycle: pitch controls the noise "colour".
                        if (phase < prev || i == 0) noiseHold = NextSigned(ref state);
                        v = noiseHold;
                        break;
                    default: v = (float)Math.Sin(2.0 * Math.PI * phase); break;
                }
                float env = 1f;
                if (envelope)
                {
                    if (n.attack > 0f && t < n.attack) env = t / n.attack;
                    else
                    {
                        float rest = n.seconds - n.attack;
                        float d = rest > 0f ? (t - n.attack) / rest : 1f;
                        env = 1f - d;
                        env *= env;
                    }
                }
                buffer[at] = Clamp(buffer[at] + v * env * n.volume);
            }
        }

        /// <summary>Equal-tempered pitch: semitones from A4 (440 Hz). 3 = C5, -9 = C4.</summary>
        public static float NoteHz(int semitonesFromA4) => 440f * (float)Math.Pow(2.0, semitonesFromA4 / 12.0);

        /// <summary>Largest absolute sample: 0 means silence.</summary>
        public static float Peak(float[] buffer)
        {
            float p = 0f;
            for (int i = 0; i < buffer.Length; i++)
            {
                float a = Math.Abs(buffer[i]);
                if (a > p) p = a;
            }
            return p;
        }

        static float Clamp(float v) => v < -1f ? -1f : (v > 1f ? 1f : v);

        static float NextSigned(ref uint state)
        {
            // xorshift32
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return (state / (float)uint.MaxValue) * 2f - 1f;
        }
    }
}
