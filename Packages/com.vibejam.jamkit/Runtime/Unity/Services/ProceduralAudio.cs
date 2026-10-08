using System.Collections.Generic;
using JamKit.Audio;
using UnityEngine;

namespace JamKit.Shell
{
    /// <summary>
    /// Placeholder sounds made from code (core <see cref="ToneSynth"/>), so every id the shell uses
    /// plays something before real assets exist. Replace one by adding a clip with the same id to the
    /// Audio Registry. Clips are made once and cached.
    /// </summary>
    public static class ProceduralAudio
    {
        const int Rate = 22050;
        static readonly Dictionary<string, AudioClip> Cache = new Dictionary<string, AudioClip>();

        /// <summary>The ids this class can make.</summary>
        public static readonly string[] Ids =
        {
            "ui_move", "ui_select", "ui_back", "blip", "coin", "hurt", "win", "lose",
            "shoot", "dash", "explosion", "music_title", "music_game",
            // Used by the bullet-hell template (harmless for other games).
            "graze", "bomb", "enemy_hit", "enemy_die", "boss_phase", "powerup", "music_boss",
            // Used by the DOOM-like template.
            "punch", "pistol", "shotgun", "rapid", "launcher", "door", "item", "key", "step",
            "alert", "pain", "enemy_shot", "bite", "secret", "weapon_up", "level_done",
        };

        public static bool Knows(string id) => System.Array.IndexOf(Ids, id) >= 0;

        public static AudioClip Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (Cache.TryGetValue(id, out var clip) && clip != null) return clip;
            float[] data = Make(id);
            if (data == null) return null;
            clip = AudioClip.Create("gen_" + id, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            Cache[id] = clip;
            return clip;
        }

        static ToneSynth.Note N(Waveform w, float a, float b, float s, float v = 0.4f) => new ToneSynth.Note(w, a, b, s, v);

        static float[] Make(string id)
        {
            switch (id)
            {
                case "ui_move": return ToneSynth.Sequence(Rate, 1, N(Waveform.Square, 660f, 700f, 0.04f, 0.18f));
                case "ui_select": return ToneSynth.Sequence(Rate, 1, N(Waveform.Square, 660f, 660f, 0.05f, 0.22f), N(Waveform.Square, 990f, 990f, 0.08f, 0.22f));
                case "ui_back": return ToneSynth.Sequence(Rate, 1, N(Waveform.Square, 520f, 330f, 0.09f, 0.2f));
                case "blip": return ToneSynth.Sequence(Rate, 1, N(Waveform.Square, 880f, 880f, 0.025f, 0.08f));
                case "coin": return ToneSynth.Sequence(Rate, 1, N(Waveform.Square, 988f, 988f, 0.06f, 0.3f), N(Waveform.Square, 1319f, 1319f, 0.22f, 0.3f));
                case "hurt": return ToneSynth.Sequence(Rate, 9, N(Waveform.Noise, 3000f, 300f, 0.18f, 0.5f), N(Waveform.Square, 180f, 90f, 0.12f, 0.3f));
                case "shoot": return ToneSynth.Sequence(Rate, 1, N(Waveform.Square, 1200f, 300f, 0.1f, 0.25f));
                case "dash": return ToneSynth.Sequence(Rate, 5, N(Waveform.Noise, 6000f, 800f, 0.15f, 0.25f));
                case "explosion": return ToneSynth.Sequence(Rate, 3, N(Waveform.Noise, 1500f, 60f, 0.6f, 0.6f));
                case "win": return Arpeggio(new[] { 3, 7, 10, 15 }, 0.12f, 0.45f);
                case "lose": return Arpeggio(new[] { 3, -2, -6, -9 }, 0.16f, 0.5f);
                case "music_title": return Loop(new[] { -9, -5, -2, 3, -12, -5, -2, 3, -14, -7, -2, 2, -16, -9, -5, 0 }, 0.25f, 0.12f);
                case "music_game": return Loop(new[] { -9, 3, -2, 3, -5, 3, -2, 3, -7, 2, -3, 2, -5, 2, -3, 7 }, 0.18f, 0.12f);
                case "graze": return ToneSynth.Sequence(Rate, 1, N(Waveform.Square, 1900f, 2300f, 0.02f, 0.07f));
                case "bomb": return ToneSynth.Sequence(Rate, 11, N(Waveform.Square, 140f, 40f, 0.25f, 0.45f), N(Waveform.Noise, 2600f, 70f, 0.85f, 0.6f));
                case "enemy_hit": return ToneSynth.Sequence(Rate, 1, N(Waveform.Square, 950f, 720f, 0.025f, 0.08f));
                case "enemy_die": return ToneSynth.Sequence(Rate, 13, N(Waveform.Square, 420f, 120f, 0.08f, 0.3f), N(Waveform.Noise, 2200f, 200f, 0.18f, 0.35f));
                case "boss_phase": return ToneSynth.Sequence(Rate, 17, N(Waveform.Square, 220f, 880f, 0.3f, 0.35f), N(Waveform.Noise, 3000f, 400f, 0.35f, 0.4f));
                case "powerup": return Arpeggio(new[] { 0, 4, 7, 12 }, 0.07f, 0.22f);
                case "punch": return ToneSynth.Sequence(Rate, 19, N(Waveform.Noise, 900f, 200f, 0.08f, 0.45f), N(Waveform.Square, 120f, 60f, 0.06f, 0.3f));
                case "pistol": return ToneSynth.Sequence(Rate, 23, N(Waveform.Noise, 3200f, 500f, 0.07f, 0.45f), N(Waveform.Square, 700f, 180f, 0.08f, 0.25f));
                case "shotgun": return ToneSynth.Sequence(Rate, 29, N(Waveform.Noise, 2600f, 90f, 0.32f, 0.65f), N(Waveform.Square, 140f, 50f, 0.12f, 0.3f));
                case "rapid": return ToneSynth.Sequence(Rate, 31, N(Waveform.Noise, 3600f, 900f, 0.045f, 0.35f));
                case "launcher": return ToneSynth.Sequence(Rate, 37, N(Waveform.Square, 260f, 900f, 0.14f, 0.3f), N(Waveform.Noise, 1800f, 300f, 0.2f, 0.35f));
                case "door": return ToneSynth.Sequence(Rate, 41, N(Waveform.Square, 80f, 105f, 0.4f, 0.25f), N(Waveform.Noise, 500f, 200f, 0.12f, 0.3f));
                case "item": return ToneSynth.Sequence(Rate, 1, N(Waveform.Square, 660f, 990f, 0.07f, 0.25f));
                case "key": return Arpeggio(new[] { 0, 7, 12 }, 0.08f, 0.2f);
                case "step": return ToneSynth.Sequence(Rate, 43, N(Waveform.Noise, 500f, 160f, 0.05f, 0.18f));
                case "alert": return ToneSynth.Sequence(Rate, 47, N(Waveform.Square, 220f, 330f, 0.12f, 0.25f), N(Waveform.Noise, 1200f, 600f, 0.15f, 0.2f));
                case "pain": return ToneSynth.Sequence(Rate, 53, N(Waveform.Square, 520f, 240f, 0.12f, 0.25f));
                case "enemy_shot": return ToneSynth.Sequence(Rate, 59, N(Waveform.Noise, 2000f, 400f, 0.12f, 0.4f), N(Waveform.Square, 600f, 260f, 0.08f, 0.2f));
                case "bite": return ToneSynth.Sequence(Rate, 61, N(Waveform.Noise, 1300f, 300f, 0.1f, 0.45f), N(Waveform.Square, 210f, 90f, 0.08f, 0.25f));
                case "secret": return Arpeggio(new[] { 0, 4, 7, 11, 14 }, 0.07f, 0.3f);
                case "weapon_up": return Arpeggio(new[] { 0, 5, 9, 12 }, 0.06f, 0.2f);
                case "level_done": return Arpeggio(new[] { 0, 4, 7, 12, 16 }, 0.1f, 0.4f);
                case "music_boss": return Loop(new[] { -12, -9, -5, -9, -12, -9, -4, -9, -14, -10, -7, -10, -14, -10, -5, -2 }, 0.14f, 0.12f);
                default: return null;
            }
        }

        static float[] Arpeggio(int[] semitones, float noteSeconds, float lastSeconds)
        {
            var notes = new ToneSynth.Note[semitones.Length];
            for (int i = 0; i < semitones.Length; i++)
            {
                float hz = ToneSynth.NoteHz(semitones[i]);
                notes[i] = N(Waveform.Square, hz, hz, i == semitones.Length - 1 ? lastSeconds : noteSeconds, 0.3f);
                notes[i].duty = 0.25f;
            }
            return ToneSynth.Sequence(Rate, 1, notes);
        }

        /// <summary>A seamless loop: a plucked triangle melody over a soft bass note per bar of four.</summary>
        static float[] Loop(int[] semitones, float step, float volume)
        {
            int perStep = ToneSynth.SampleCount(step, Rate);
            var buffer = new float[perStep * semitones.Length];
            for (int i = 0; i < semitones.Length; i++)
            {
                float hz = ToneSynth.NoteHz(semitones[i]);
                ToneSynth.Mix(buffer, i * perStep, N(Waveform.Triangle, hz, hz, step * 0.95f, volume), Rate);
                if (i % 4 == 0)
                {
                    float bass = ToneSynth.NoteHz(semitones[i] - 12);
                    ToneSynth.Mix(buffer, i * perStep, N(Waveform.Sine, bass, bass, step * 3.9f, volume * 1.2f), Rate);
                }
            }
            return buffer;
        }
    }
}
