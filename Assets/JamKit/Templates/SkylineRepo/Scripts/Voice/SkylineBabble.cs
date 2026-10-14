using System;
using System.Collections.Generic;
using JamKit.Audio;
using JamKit.Skyline;
using UnityEngine;

namespace JamKit.SkylineRepo
{
    /// <summary>
    /// Undertale-style babble (spec 3.6): every letter a short beep from <see cref="ToneSynth"/>, one voice per speaker and per
    /// bot. Spaces are short rests, punctuation longer ones; each letter's pitch wobbles around the voice's base by a seeded
    /// amount tied to the letter, so a line always sounds the same. Dot's voice is band-passed like a radio. Voice names: the
    /// speakers ("dot", "concierge", "nan") and the bot ids. Clips are cached by voice and text.
    /// </summary>
    public static class SkylineBabble
    {
        const int SampleRate = 44100;

        sealed class Voice
        {
            public Waveform wave;
            public float baseHz, spreadSemis, blip, gap, volume, duty = 0.5f, lowPassHz;
            public bool radio;
        }

        static readonly Dictionary<string, Voice> _voices = new Dictionary<string, Voice>
        {
            { "dot", new Voice { wave = Waveform.Square, baseHz = 620f, spreadSemis = 5f, blip = 0.045f, gap = 0.018f, volume = 0.32f, duty = 0.35f, radio = true } },
            { "concierge", new Voice { wave = Waveform.Sine, baseHz = 440f, spreadSemis = 2f, blip = 0.06f, gap = 0.02f, volume = 0.4f } },
            { "nan", new Voice { wave = Waveform.Triangle, baseHz = 330f, spreadSemis = 4f, blip = 0.07f, gap = 0.03f, volume = 0.42f } },
            { SkylineEnemies.DroneId, new Voice { wave = Waveform.Square, baseHz = 520f, spreadSemis = 2f, blip = 0.04f, gap = 0.012f, volume = 0.28f, duty = 0.5f, lowPassHz = SkylineMix.BarkLowPassHz } },
            { SkylineEnemies.FloorCareId, new Voice { wave = Waveform.Triangle, baseHz = 760f, spreadSemis = 7f, blip = 0.05f, gap = 0.015f, volume = 0.4f, lowPassHz = SkylineMix.BarkLowPassHz } },
            { SkylineEnemies.SpotlightId, new Voice { wave = Waveform.Saw, baseHz = 240f, spreadSemis = 1f, blip = 0.06f, gap = 0.02f, volume = 0.26f, lowPassHz = SkylineMix.BarkLowPassHz } },
            { SkylineEnemies.GondolaId, new Voice { wave = Waveform.Square, baseHz = 380f, spreadSemis = 5f, blip = 0.055f, gap = 0.02f, volume = 0.28f, duty = 0.25f, lowPassHz = SkylineMix.BarkLowPassHz } },
        };

        static readonly Dictionary<string, AudioClip> _cache = new Dictionary<string, AudioClip>();

        public static string VoiceOf(Speaker speaker) => speaker.ToString().ToLowerInvariant();

        /// <summary>The babble for <paramref name="text"/> in <paramref name="voice"/> (an unknown voice gets a neutral one).</summary>
        public static AudioClip Make(string voice, string text, int seed)
        {
            if (string.IsNullOrEmpty(text)) return null;
            string key = (voice ?? "") + "|" + seed + "|" + text;
            if (_cache.TryGetValue(key, out var cached) && cached != null) return cached;
            if (voice == null || !_voices.TryGetValue(voice, out var v))
                v = new Voice { wave = Waveform.Square, baseHz = 480f, spreadSemis = 3f, blip = 0.05f, gap = 0.018f, volume = 0.3f };

            var notes = new List<ToneSynth.Note>();
            uint h = (uint)seed * 2654435761u + 0x9E3779B9u;
            foreach (char ch in text)
            {
                if (char.IsWhiteSpace(ch)) { notes.Add(Rest(v.gap * 2.5f)); continue; }
                if (ch == '.' || ch == '!' || ch == '?' || ch == ',' || ch == ';' || ch == ':')
                {
                    notes.Add(Rest(ch == ',' ? 0.08f : 0.14f));
                    continue;
                }
                if (!char.IsLetterOrDigit(ch)) continue;
                h = h * 1664525u + 1013904223u + char.ToLowerInvariant(ch);
                float semis = ((h >> 8) & 0xFFFF) / 65535f * 2f - 1f;
                float hz = v.baseHz * Mathf.Pow(2f, semis * v.spreadSemis / 12f);
                if (ch == char.ToUpperInvariant(ch) && char.IsLetter(ch)) hz *= 1.06f;   // capitals lift a little
                notes.Add(new ToneSynth.Note(v.wave, hz, hz * 0.97f, v.blip, v.volume, 0.004f) { duty = v.duty });
                notes.Add(Rest(v.gap));
            }
            if (notes.Count == 0) return null;
            float[] data = ToneSynth.Sequence(SampleRate, (uint)(seed == 0 ? 1 : seed), notes.ToArray());
            if (v.lowPassHz > 0f)   // the bots' square and saw blips were most of the 2-5 kHz energy of a bark (the mix pass, 2026-10-14)
            {
                var lp = new SkylineBiquad();
                lp.SetLowPass(SampleRate, v.lowPassHz);
                lp.ProcessMono(data);
            }
            if (v.radio) Radio(data, (uint)(seed + 7));
            var clip = AudioClip.Create("babble-" + (voice ?? "x"), data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            _cache[key] = clip;
            return clip;
        }

        static readonly Dictionary<AudioClip, AudioClip> _capped = new Dictionary<AudioClip, AudioClip>();
        const float CapFade = 0.08f;

        /// <summary>Seconds a teaching line lasts: its reading time (<see cref="TeachingQueue.LineSeconds"/>: 15 characters a
        /// second plus a 0.5 s lead, 2 to 10 s). The spoken babble is cut to it and the subtitle stays up for it.</summary>
        public static float LineSeconds(string text) => TeachingQueue.LineSeconds(text);

        /// <summary>Speaks <paramref name="text"/> in <paramref name="speaker"/>'s voice from <paramref name="source"/>,
        /// <paramref name="delay"/> seconds from now (after CONCIERGE's PA chime), cut to the line's reading time
        /// (<see cref="LineSeconds"/>) with a short fade. The source says one line at a time: a line that cuts in stops the one
        /// before. <paramref name="startAt"/> (seconds) resumes a line that was cut, from where it was cut; past the voice's
        /// end, nothing plays.</summary>
        public static void Speak(Speaker speaker, string text, AudioSource source, float delay = 0f, float startAt = 0f)
        {
            if (source == null) return;
            var clip = Make(VoiceOf(speaker), text, (int)speaker + 11);
            if (clip == null) return;
            source.Stop();
            var c = Cap(clip, LineSeconds(text));
            if (startAt >= c.length - 0.05f) return;
            source.clip = c;
            source.time = Mathf.Max(0f, startAt);
            if (delay > 0f) source.PlayDelayed(delay);
            else source.Play();
        }

        /// <summary>Makes (and caches) the clip <see cref="Speak"/> would play for this line, so speaking it later costs
        /// nothing: lines are synthesized ahead, one per frame, by the arena's director.</summary>
        public static void Prepare(Speaker speaker, string text)
        {
            var clip = Make(VoiceOf(speaker), text, (int)speaker + 11);
            if (clip != null) Cap(clip, LineSeconds(text));
        }

        static AudioClip Cap(AudioClip clip, float seconds)
        {
            if (clip.length <= seconds + 0.01f) return clip;
            if (_capped.TryGetValue(clip, out var c) && c != null) return c;
            int n = Mathf.Max(1, Mathf.Min(clip.samples, Mathf.RoundToInt(seconds * clip.frequency)));
            var data = new float[n * clip.channels];
            clip.GetData(data, 0);
            int fade = Mathf.Min(n, Mathf.RoundToInt(CapFade * clip.frequency));
            for (int i = 0; i < fade; i++)
            {
                float g = (float)i / fade;
                int f = n - 1 - i;
                for (int ch = 0; ch < clip.channels; ch++) data[f * clip.channels + ch] *= g;
            }
            c = AudioClip.Create(clip.name + "-cut", n, clip.channels, clip.frequency, false);
            c.SetData(data, 0);
            _capped[clip] = c;
            return c;
        }

        static ToneSynth.Note Rest(float seconds) => new ToneSynth.Note(Waveform.Sine, 1f, 1f, seconds, 0f, 0f);

        /// <summary>A radio's band (about 400 Hz to 3 kHz, two one-pole stages each side), a little hiss and a soft clip.</summary>
        static void Radio(float[] d, uint seed)
        {
            float dt = 1f / SampleRate;
            float aLow = dt / (1f / (2f * Mathf.PI * 3000f) + dt);          // low-pass coefficient
            float rcHigh = 1f / (2f * Mathf.PI * 400f), aHigh = rcHigh / (rcHigh + dt);
            float lp1 = 0f, lp2 = 0f, hpIn = 0f, hp1 = 0f, hpIn2 = 0f, hp2 = 0f;
            uint s = seed == 0 ? 1u : seed;
            for (int i = 0; i < d.Length; i++)
            {
                s ^= s << 13; s ^= s >> 17; s ^= s << 5;
                float x = d[i] + ((s / (float)uint.MaxValue) * 2f - 1f) * 0.012f;
                lp1 += aLow * (x - lp1);
                lp2 += aLow * (lp1 - lp2);
                hp1 = aHigh * (hp1 + lp2 - hpIn); hpIn = lp2;
                hp2 = aHigh * (hp2 + hp1 - hpIn2); hpIn2 = hp1;
                float y = hp2 * 2.2f;
                d[i] = (float)Math.Tanh(y);
            }
        }
    }
}
