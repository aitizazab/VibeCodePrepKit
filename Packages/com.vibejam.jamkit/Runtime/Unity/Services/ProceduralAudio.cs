using System.Collections.Generic;
using JamKit.Audio;
using JamKit.Movement;
using UnityEngine;

namespace JamKit.Shell
{
    /// <summary>
    /// Placeholder sounds made from code (core <see cref="ToneSynth"/>), so every id the shell uses
    /// plays something before real assets exist. Replace one by adding a clip with the same id to the
    /// Audio Registry. Clips are made once and cached.
    /// <para>A recording in any <c>Resources/JamKitSounds/</c> folder named after an id (<c>vh_breath.wav</c>) wins over
    /// the code-made sound: the Visiting Hours opening's breath, ring, buzz, rustles, set-down and tap are CC0
    /// recordings there (tools/audio/make-vh-sfx.py). Delete one and the generated sound plays again.</para>
    /// </summary>
    public static class ProceduralAudio
    {
        const int Rate = 22050;
        /// <summary>The Resources folder whose clips (named by id) replace the generated sounds.</summary>
        public const string RecordedFolder = "JamKitSounds/";
        static readonly Dictionary<string, AudioClip> Cache = new Dictionary<string, AudioClip>();

        /// <summary>
        /// The Visiting Hours sounds came out far too quiet (a phone ring peaking at 0.44, a breath at 0.05, the
        /// creator couldn't hear the opening), so each is scaled to a set peak when it's made. The peaks keep the
        /// mix's order: the ring and impacts loudest, then the voices' bed, then breath and ambience.
        /// </summary>
        static readonly Dictionary<string, float> Peaks = new Dictionary<string, float>
        {
            { "vh_phone_ring", 0.75f }, { "vh_vibrate", 0.6f }, { "vh_clunk", 0.75f }, { "vh_tap", 0.6f },
            { "vh_rustle", 0.55f }, { "vh_rustle_big", 0.6f }, { "vh_breath", 0.35f }, { "vh_monitor", 0.45f },
            { "vh_crack", 0.7f }, { "vh_wrong", 0.6f }, { "vh_hit", 0.7f }, { "vh_journal", 0.6f },
            { "amb_vh_hospital", 0.35f }, { "amb_vh_suburb", 0.35f }, { "music_vh_sad_pad", 0.6f },
        };

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
            // Used by the story template: music, ambience beds (loops) and effects.
            "music_story_calm", "music_story_tense", "music_story_sad", "music_story_warm",
            "amb_rain", "amb_hum", "amb_wind",
            "door_knock", "page", "drawer", "unlock", "glitch", "heartbeat", "chime", "train_horn",
            "train_pass", "thunder", "footsteps", "click", "voice_low", "voice_high",
            // Used by the Visiting Hours slice. music_vh_sad_pad is a PLACEHOLDER (the creator composes the score).
            "vh_phone_ring", "vh_monitor", "amb_vh_hospital", "amb_vh_suburb", "music_vh_sad_pad",
            "vh_crack", "vh_wrong", "vh_hit", "vh_journal",
            // The Visiting Hours opening (PLACEHOLDERS): the phone's buzz (loops with vh_phone_ring), sheets,
            // the phone set down, a screen tap, Nadia's slow breathing (loops).
            "vh_vibrate", "vh_rustle", "vh_rustle_big", "vh_clunk", "vh_tap", "vh_breath",
            // First-person movement (Runtime/Unity/Movement, the Movement Lab), made by the core's MoveSounds
            // (MovementAudio prefers the recorded CC0 variants in Runtime/Audio/Movement). Loops: mv_wind,
            // mv_wind_high, mv_slide, mv_wallrun, mv_supersonic, mv_breath_calm, mv_breath_fast, mv_static (the
            // dream-static, still a placeholder); the rest are
            // one-shots, with MoveSounds.Variants round-robin variants each (GetVariant).
            "mv_wind", "mv_wind_high", "mv_slide", "mv_wallrun", "mv_supersonic", "mv_breath_calm", "mv_breath_fast", "mv_static",
            "mv_step", "mv_step_heavy", "mv_jump", "mv_djump", "mv_walljump", "mv_catch",
            "mv_land_soft", "mv_land", "mv_land_hard", "mv_boost", "mv_mantle", "mv_kick", "mv_slidein",
            "mv_collapse", "mv_checkpoint", "mv_caught",
        };

        public static bool Knows(string id) => System.Array.IndexOf(Ids, id) >= 0;

        public static AudioClip Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (MoveSounds.Knows(id)) return GetVariant(id, 0);
            if (Cache.TryGetValue(id, out var clip) && clip != null) return clip;
            clip = Recorded(id);
            if (clip != null)
            {
                Cache[id] = clip;
                return clip;
            }
            float[] data = Make(id);
            if (data == null) return null;
            if (Peaks.TryGetValue(id, out float peak)) MoveSounds.Normalize(data, peak);
            clip = AudioClip.Create("gen_" + id, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            Cache[id] = clip;
            return clip;
        }

        /// <summary>The recording for an id in Resources/JamKitSounds/, or null (then the generated sound plays).</summary>
        public static AudioClip Recorded(string id) => string.IsNullOrEmpty(id) ? null : Resources.Load<AudioClip>(RecordedFolder + id);

        /// <summary>How many generated variants an id has (the movement one-shots have several; others 1, unknown 0).</summary>
        public static int VariantCount(string id) => MoveSounds.Knows(id) ? MoveSounds.VariantCount(id) : Knows(id) ? 1 : 0;

        /// <summary>One generated variant of an id (round-robin for the movement one-shots; other ids ignore the index).</summary>
        public static AudioClip GetVariant(string id, int variant)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (!MoveSounds.Knows(id)) return Get(id);
            int count = MoveSounds.VariantCount(id);
            int v = ((variant % count) + count) % count;
            string key = v == 0 ? id : id + "#" + v;
            if (Cache.TryGetValue(key, out var clip) && clip != null) return clip;
            float[] data = MoveSounds.Make(id, v);
            if (data == null) return null;
            clip = AudioClip.Create("gen_" + key, data.Length, 1, MoveSounds.Rate, false);
            clip.SetData(data, 0);
            Cache[key] = clip;
            return clip;
        }

        static ToneSynth.Note N(Waveform w, float a, float b, float s, float v = 0.4f) => new ToneSynth.Note(w, a, b, s, v);

        static ToneSynth.Note N(Waveform w, float a, float b, float s, float v, float attack) => new ToneSynth.Note(w, a, b, s, v, attack);

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
                case "music_story_calm": return Loop(new[] { -9, -5, -2, -5, -12, -5, -2, 0, -14, -9, -5, -9, -16, -9, -5, -4 }, 0.42f, 0.09f, Waveform.Sine);
                case "music_story_tense": return Loop(new[] { -21, -21, -20, -21, -21, -21, -18, -21, -21, -21, -20, -21, -23, -23, -22, -23 }, 0.22f, 0.1f, Waveform.Triangle);
                case "music_story_sad": return Loop(new[] { -4, -5, -7, -9, -12, -9, -7, -5, -9, -10, -12, -14, -17, -14, -12, -10 }, 0.5f, 0.08f, Waveform.Sine);
                case "music_story_warm": return Loop(new[] { 3, 7, 10, 7, 5, 8, 12, 8, 0, 3, 7, 3, -2, 2, 5, 10 }, 0.32f, 0.09f, Waveform.Triangle);
                case "amb_rain": return Bed(4f, N(Waveform.Noise, 11000f, 11000f, 4f, 0.07f), N(Waveform.Noise, 2400f, 2400f, 4f, 0.05f), N(Waveform.Noise, 500f, 500f, 4f, 0.025f));
                case "amb_hum": return Bed(4f, N(Waveform.Sine, 60f, 60f, 4f, 0.12f), N(Waveform.Sine, 120f, 120f, 4f, 0.05f), N(Waveform.Noise, 300f, 300f, 4f, 0.012f));
                case "amb_wind": return Bed(6f, N(Waveform.Noise, 700f, 700f, 6f, 0.05f), N(Waveform.Noise, 260f, 260f, 6f, 0.06f));
                case "door_knock": return ToneSynth.Sequence(Rate, 67, N(Waveform.Noise, 700f, 120f, 0.09f, 0.5f), N(Waveform.Sine, 0f, 0f, 0.12f, 0f), N(Waveform.Noise, 700f, 120f, 0.09f, 0.5f), N(Waveform.Sine, 0f, 0f, 0.12f, 0f), N(Waveform.Noise, 700f, 120f, 0.1f, 0.55f));
                case "page": return ToneSynth.Sequence(Rate, 71, N(Waveform.Noise, 6000f, 2500f, 0.18f, 0.18f));
                case "drawer": return ToneSynth.Sequence(Rate, 73, N(Waveform.Noise, 900f, 400f, 0.35f, 0.25f), N(Waveform.Noise, 1500f, 200f, 0.08f, 0.4f));
                case "unlock": return ToneSynth.Sequence(Rate, 79, N(Waveform.Noise, 4000f, 1500f, 0.05f, 0.35f), N(Waveform.Square, 1400f, 1400f, 0.04f, 0.15f), N(Waveform.Noise, 3000f, 800f, 0.07f, 0.3f));
                case "glitch": return ToneSynth.Sequence(Rate, 83, N(Waveform.Square, 90f, 2400f, 0.05f, 0.25f), N(Waveform.Noise, 9000f, 9000f, 0.06f, 0.3f), N(Waveform.Square, 1800f, 60f, 0.07f, 0.2f), N(Waveform.Saw, 300f, 320f, 0.08f, 0.2f));
                case "heartbeat": return ToneSynth.Sequence(Rate, 1, N(Waveform.Sine, 70f, 45f, 0.14f, 0.7f), N(Waveform.Sine, 0f, 0f, 0.08f, 0f), N(Waveform.Sine, 62f, 40f, 0.18f, 0.55f));
                case "chime": return Arpeggio(new[] { 7, 3, 10, -2 }, 0.32f, 0.9f, Waveform.Sine);
                case "train_horn": return ToneSynth.Sequence(Rate, 1, N(Waveform.Saw, 233f, 230f, 0.7f, 0.18f), N(Waveform.Saw, 196f, 193f, 1.1f, 0.18f));
                case "train_pass": return ToneSynth.Sequence(Rate, 89, N(Waveform.Noise, 300f, 900f, 1.4f, 0.35f), N(Waveform.Noise, 900f, 200f, 1.8f, 0.4f));
                case "thunder": return ToneSynth.Sequence(Rate, 97, N(Waveform.Noise, 180f, 40f, 2.2f, 0.6f));
                case "footsteps": return ToneSynth.Sequence(Rate, 101, N(Waveform.Noise, 600f, 150f, 0.07f, 0.35f), N(Waveform.Sine, 0f, 0f, 0.25f, 0f), N(Waveform.Noise, 550f, 150f, 0.07f, 0.3f), N(Waveform.Sine, 0f, 0f, 0.25f, 0f), N(Waveform.Noise, 600f, 150f, 0.07f, 0.32f));
                case "click": return ToneSynth.Sequence(Rate, 103, N(Waveform.Noise, 5000f, 2000f, 0.02f, 0.3f));
                case "voice_low": return ToneSynth.Sequence(Rate, 1, N(Waveform.Triangle, 190f, 170f, 0.045f, 0.16f));
                case "voice_high": return ToneSynth.Sequence(Rate, 1, N(Waveform.Triangle, 520f, 470f, 0.04f, 0.13f));
                case "vh_phone_ring": return PhoneRing();
                case "vh_monitor": return Monitor();
                case "vh_vibrate": return Vibrate();
                case "vh_rustle": return ToneSynth.Sequence(Rate, 181, N(Waveform.Noise, 2600f, 900f, 0.5f, 0.12f, 0.12f));
                case "vh_rustle_big": return RustleBig();
                case "vh_clunk": return ToneSynth.Sequence(Rate, 191, N(Waveform.Sine, 170f, 70f, 0.16f, 0.45f, 0.002f), N(Waveform.Noise, 900f, 200f, 0.08f, 0.12f));
                case "vh_tap": return ToneSynth.Sequence(Rate, 193, N(Waveform.Noise, 7000f, 3000f, 0.025f, 0.18f, 0.001f));
                case "vh_breath": return Breath();
                case "amb_vh_hospital": return Bed(4f, N(Waveform.Sine, 60f, 60f, 4f, 0.07f), N(Waveform.Sine, 120f, 120f, 4f, 0.025f), N(Waveform.Noise, 260f, 260f, 4f, 0.01f), N(Waveform.Sine, 7400f, 7400f, 4f, 0.002f));
                case "amb_vh_suburb": return Suburb();
                case "music_vh_sad_pad": return SadPad();
                case "vh_crack": return ToneSynth.Sequence(Rate, 107, N(Waveform.Noise, 7000f, 1500f, 0.09f, 0.35f), N(Waveform.Sine, 880f, 1320f, 0.5f, 0.22f));
                case "vh_wrong": return ToneSynth.Sequence(Rate, 1, N(Waveform.Triangle, 220f, 196f, 0.28f, 0.25f));
                case "vh_hit": return ToneSynth.Sequence(Rate, 109, N(Waveform.Sine, 330f, 140f, 0.22f, 0.45f), N(Waveform.Noise, 1200f, 300f, 0.1f, 0.15f));
                case "vh_journal": return ToneSynth.Sequence(Rate, 113, N(Waveform.Noise, 5200f, 2600f, 0.16f, 0.14f), N(Waveform.Sine, 990f, 990f, 0.12f, 0.08f));
                // Movement: mv_wind, mv_slide, mv_step ... are made by MoveSounds (see Get). These four belong to
                // the Movement Lab's and the dream escape's own events and stay as they were.
                case "mv_static": return Bed(3f, N(Waveform.Noise, 9000f, 9000f, 3f, 0.08f), N(Waveform.Noise, 2200f, 2200f, 3f, 0.05f), N(Waveform.Noise, 90f, 90f, 3f, 0.08f));
                case "mv_collapse": return ToneSynth.Sequence(Rate, 173, N(Waveform.Noise, 260f, 35f, 1.8f, 0.6f), N(Waveform.Noise, 1400f, 100f, 0.6f, 0.25f));
                case "mv_checkpoint": return Arpeggio(new[] { 0, 7, 12 }, 0.07f, 0.35f, Waveform.Sine);
                case "mv_caught": return ToneSynth.Sequence(Rate, 179, N(Waveform.Noise, 9000f, 600f, 0.5f, 0.45f), N(Waveform.Sine, 220f, 55f, 0.6f, 0.3f));
                case "music_boss": return Loop(new[] { -12, -9, -5, -9, -12, -9, -4, -9, -14, -10, -7, -10, -14, -10, -5, -2 }, 0.14f, 0.12f);
                default: return null;
            }
        }

        /// <summary>A phone's double ring (two short bursts of two tones), then quiet: loops every 3 s.</summary>
        static float[] PhoneRing()
        {
            var buffer = new float[ToneSynth.SampleCount(3f, Rate)];
            for (int burst = 0; burst < 2; burst++)
            {
                int at = ToneSynth.SampleCount(burst * 0.55f, Rate);
                var a = N(Waveform.Sine, 440f, 440f, 0.42f, 0.22f);
                var b = N(Waveform.Sine, 480f, 480f, 0.42f, 0.22f);
                a.attack = b.attack = 0.02f;
                ToneSynth.Mix(buffer, at, a, Rate);
                ToneSynth.Mix(buffer, at, b, Rate);
            }
            return buffer;
        }

        /// <summary>A phone buzzing on a wooden nightstand, in step with <see cref="PhoneRing"/> (loops every 3 s).</summary>
        static float[] Vibrate()
        {
            var buffer = new float[ToneSynth.SampleCount(3f, Rate)];
            for (int burst = 0; burst < 2; burst++)
            {
                int at = ToneSynth.SampleCount(burst * 0.55f, Rate);
                var hum = N(Waveform.Square, 150f, 148f, 0.42f, 0.07f, 0.01f);
                hum.duty = 0.3f;
                ToneSynth.Mix(buffer, at, hum, Rate);
                ToneSynth.Mix(buffer, at, N(Waveform.Noise, 300f, 280f, 0.42f, 0.05f, 0.01f), Rate, 197u + (uint)burst);
            }
            return buffer;
        }

        /// <summary>Rolling over and pulling the blanket up: two long swishes of cloth.</summary>
        static float[] RustleBig()
        {
            var buffer = new float[ToneSynth.SampleCount(1.5f, Rate)];
            ToneSynth.Mix(buffer, 0, N(Waveform.Noise, 3000f, 1100f, 0.75f, 0.16f, 0.2f), Rate, 199u);
            ToneSynth.Mix(buffer, ToneSynth.SampleCount(0.55f, Rate), N(Waveform.Noise, 2200f, 700f, 0.9f, 0.14f, 0.25f), Rate, 211u);
            return buffer;
        }

        /// <summary>Slow sleeping breath, in and out (loops every 4.8 s).</summary>
        static float[] Breath()
        {
            var buffer = new float[ToneSynth.SampleCount(4.8f, Rate)];
            ToneSynth.Mix(buffer, 0, N(Waveform.Noise, 500f, 800f, 1.7f, 0.05f, 1.1f), Rate, 223u);
            ToneSynth.Mix(buffer, ToneSynth.SampleCount(1.9f, Rate), N(Waveform.Noise, 800f, 350f, 2.4f, 0.06f, 0.5f), Rate, 227u);
            return buffer;
        }

        /// <summary>A heart monitor: one soft beep a second (loops).</summary>
        static float[] Monitor()
        {
            var buffer = new float[ToneSynth.SampleCount(1f, Rate)];
            ToneSynth.Mix(buffer, 0, N(Waveform.Sine, 1046f, 1046f, 0.13f, 0.2f), Rate);
            return buffer;
        }

        /// <summary>A summer afternoon: breeze, a sprinkler's hiss and a few birds (loops, 6 s).</summary>
        static float[] Suburb()
        {
            var buffer = Bed(6f, N(Waveform.Noise, 520f, 520f, 6f, 0.03f), N(Waveform.Noise, 9000f, 9000f, 6f, 0.008f));
            float[] chirps = { 0.7f, 0.82f, 0.94f, 3.1f, 3.22f, 4.6f, 4.7f, 4.8f, 4.9f };
            for (int i = 0; i < chirps.Length; i++)
            {
                float hz = 2900f + (i % 3) * 420f;
                ToneSynth.Mix(buffer, ToneSynth.SampleCount(chirps[i], Rate), N(Waveform.Sine, hz, hz * 1.25f, 0.07f, 0.05f), Rate);
            }
            return buffer;
        }

        /// <summary>PLACEHOLDER score: a slow, sad pad (A minor, F, C, G, four seconds each) that loops
        /// without a seam. The creator composes the real music; replace it in the Audio Registry.</summary>
        static float[] SadPad()
        {
            int[][] chords =
            {
                new[] { -24, -12, -9, -5 },  // A minor
                new[] { -28, -16, -12, -9 }, // F
                new[] { -21, -17, -14, -9 }, // C
                new[] { -26, -14, -10, -7 }, // G
            };
            const float chord = 4f;
            int loop = ToneSynth.SampleCount(chord * chords.Length, Rate);
            var buffer = new float[loop + ToneSynth.SampleCount(chord, Rate)];
            for (int c = 0; c < chords.Length; c++)
            {
                int at = ToneSynth.SampleCount(c * chord, Rate);
                for (int k = 0; k < chords[c].Length; k++)
                {
                    float hz = ToneSynth.NoteHz(chords[c][k]);
                    var n = N(k == 0 ? Waveform.Sine : Waveform.Triangle, hz, hz, chord * 1.4f, k == 0 ? 0.09f : 0.035f);
                    n.attack = chord * 0.5f;
                    ToneSynth.Mix(buffer, at, n, Rate);
                }
            }
            // Fold the tail that rings past the loop back onto its start: a seamless loop.
            var looped = new float[loop];
            for (int i = 0; i < buffer.Length; i++) looped[i % loop] += buffer[i];
            return looped;
        }

        static float[] Arpeggio(int[] semitones, float noteSeconds, float lastSeconds, Waveform wave = Waveform.Square)
        {
            var notes = new ToneSynth.Note[semitones.Length];
            for (int i = 0; i < semitones.Length; i++)
            {
                float hz = ToneSynth.NoteHz(semitones[i]);
                notes[i] = N(wave, hz, hz, i == semitones.Length - 1 ? lastSeconds : noteSeconds, 0.3f);
                notes[i].duty = 0.25f;
            }
            return ToneSynth.Sequence(Rate, 1, notes);
        }

        /// <summary>A steady bed that loops without a seam (no envelope): rain, hum, wind.</summary>
        static float[] Bed(float seconds, params ToneSynth.Note[] layers)
        {
            var buffer = new float[ToneSynth.SampleCount(seconds, Rate)];
            uint seed = 7u;
            foreach (var n in layers) ToneSynth.Mix(buffer, 0, n, Rate, seed++, false);
            return buffer;
        }

        /// <summary>A seamless loop: a plucked melody (triangle by default) over a soft bass note per bar of four.</summary>
        static float[] Loop(int[] semitones, float step, float volume, Waveform wave = Waveform.Triangle)
        {
            int perStep = ToneSynth.SampleCount(step, Rate);
            var buffer = new float[perStep * semitones.Length];
            for (int i = 0; i < semitones.Length; i++)
            {
                float hz = ToneSynth.NoteHz(semitones[i]);
                ToneSynth.Mix(buffer, i * perStep, N(wave, hz, hz, step * 0.95f, volume), Rate);
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
