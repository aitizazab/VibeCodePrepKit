using System;
using System.Collections.Generic;
using JamKit.Shell;
using UnityEngine;

namespace JamKit.Argument
{
    /// <summary>
    /// The battle's sound (docs/ARGUMENT-PROTOTYPE.md, "Sound"). On Begin it fades out the world's ambience bed and music
    /// (remembering them) and starts the battle loop; on End it stops the loop and brings the world's back.
    /// Everything follows the player's volumes and the shell's mute (batch mode).
    ///
    /// Swappable files, in Assets/JamKit/Templates/VisitingHours/Argument/Resources/ArgumentAudio/:
    /// argument_battle_loop (the music; the placeholder is the round-2 sketch 05-argument-battle.loop), and one per
    /// sound: sfx_nav, sfx_confirm, sfx_pick, sfx_back, sfx_break, sfx_wrong, sfx_angry, sfx_hit, sfx_heartbeat,
    /// sfx_pressure_in, sfx_pressure_out, sfx_slip, sfx_end_good, sfx_end_bad, sfx_warn (an attack's warning). A sound with no file is generated
    /// (a placeholder made in code).
    /// </summary>
    public sealed class ArgumentAudio : MonoBehaviour
    {
        public const string Folder = "ArgumentAudio/";
        public const string MusicFile = "argument_battle_loop";
        public static readonly string[] Sounds =
        {
            "sfx_nav", "sfx_confirm", "sfx_pick", "sfx_back", "sfx_break", "sfx_wrong", "sfx_angry", "sfx_hit",
            "sfx_heartbeat", "sfx_pressure_in", "sfx_pressure_out", "sfx_slip", "sfx_end_good", "sfx_end_bad", "sfx_warn",
        };
        const float FadeSeconds = 0.8f;

        static readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _clips.Clear();

        AudioSource _music, _sfx;
        string _worldAmbience = "", _worldMusic = "";
        float _musicFade, _musicTarget;
        bool _ended;
        string _last = "";

        /// <summary>The world's ambience and music ids as they were when the battle began (restored at the end).</summary>
        public string WorldAmbience => _worldAmbience;
        public string WorldMusic => _worldMusic;
        /// <summary>The battle loop is playing (or fading).</summary>
        public bool MusicPlaying => _music != null && _music.isPlaying;
        /// <summary>The battle loop was started (true even where there's no audio device: batch test runs).</summary>
        public bool MusicStarted => _musicStarted;
        bool _musicStarted;
        /// <summary>The last sound played (tests).</summary>
        public string LastSound => _last;

        /// <summary>Takes over the soundscape: the world's fades out, the battle loop fades in.</summary>
        public void Begin()
        {
            _music = gameObject.AddComponent<AudioSource>();
            _music.playOnAwake = false;
            _music.loop = true;
            _music.ignoreListenerPause = true;
            _music.clip = Clip(MusicFile);
            _sfx = gameObject.AddComponent<AudioSource>();
            _sfx.playOnAwake = false;
            _sfx.ignoreListenerPause = true;
            if (GameRoot.Exists)
            {
                var a = GameRoot.Audio;
                _worldAmbience = a.CurrentAmbience ?? "";
                _worldMusic = a.CurrentMusic ?? "";
                a.StopAmbience(FadeSeconds);
                a.StopMusic(FadeSeconds);
            }
            if (_music.clip != null)
            {
                _musicFade = 0f;
                _musicTarget = 1f;
                _music.volume = 0f;
                _music.Play();
                _musicStarted = true;
            }
            Apply();
        }

        /// <summary>Hands the soundscape back: the battle loop fades out, the world's ambience and music fade back in.</summary>
        public void End()
        {
            if (_ended) return;
            _ended = true;
            _musicTarget = 0f;
            if (!GameRoot.Exists) return;
            var a = GameRoot.Audio;
            if (_worldAmbience.Length > 0) a.PlayAmbience(_worldAmbience, FadeSeconds);
            if (_worldMusic.Length > 0) a.PlayMusic(_worldMusic, FadeSeconds);
        }

        /// <summary>Plays one of <see cref="Sounds"/> at the player's effects volume.</summary>
        public void Play(string id, float volume = 1f)
        {
            if (_sfx == null) return;
            var clip = Clip(id);
            if (clip == null) return;
            _last = id;
            _sfx.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        void Update()
        {
            if (_music == null) return;
            _musicFade = Mathf.MoveTowards(_musicFade, _musicTarget, Time.unscaledDeltaTime / FadeSeconds);
            if (_musicFade <= 0f && _musicTarget <= 0f && _music.isPlaying) _music.Stop();
            Apply();
        }

        void Apply()
        {
            float master = 1f, music = 1f, sfx = 1f;
            bool muted = false;
            if (GameRoot.Exists)
            {
                var s = GameRoot.Settings;
                master = s.masterVolume;
                music = s.musicVolume;
                sfx = s.sfxVolume;
                muted = GameRoot.Audio.Muted;
            }
            if (_music != null)
            {
                _music.volume = _musicFade * master * music * 0.8f;
                _music.mute = muted;
            }
            if (_sfx != null)
            {
                _sfx.volume = master * sfx;
                _sfx.mute = muted;
            }
        }

        // ---------- Clips: a file in Resources, else a generated placeholder ----------

        /// <summary>The clip for an id: Resources/ArgumentAudio/&lt;id&gt; if there is one, else a generated placeholder
        /// (the music has no placeholder: without its file the battle is silent but for its sounds).</summary>
        public static AudioClip Clip(string id)
        {
            if (_clips.TryGetValue(id, out var c) && c != null) return c;
            c = Resources.Load<AudioClip>(Folder + id);
            if (c == null && id != MusicFile) c = Generate(id);
            _clips[id] = c;
            return c;
        }

        const int Rate = 44100;

        static AudioClip Generate(string id)
        {
            float[] d;
            switch (id)
            {
                case "sfx_nav": d = Tone(0.05f, 880f, 0.18f, 0f); break;
                case "sfx_confirm": d = Mix(Tone(0.09f, 660f, 0.3f, 0f), Delay(Tone(0.12f, 990f, 0.25f, 0f), 0.05f)); break;
                case "sfx_pick": d = Mix(Tone(0.08f, 520f, 0.28f, 0f), Delay(Tone(0.1f, 780f, 0.22f, 0f), 0.04f)); break;
                case "sfx_back": d = Mix(Tone(0.08f, 700f, 0.24f, 0f), Delay(Tone(0.1f, 470f, 0.22f, 0f), 0.05f)); break;
                case "sfx_break": d = Mix(Noise(0.18f, 0.5f, 7), Delay(Chord(0.9f, new[] { 523f, 659f, 784f }, 0.22f), 0.08f)); break;
                case "sfx_wrong": d = Mix(Tone(0.35f, 140f, 0.45f, -40f), Tone(0.35f, 147f, 0.3f, -40f)); break;
                case "sfx_angry": d = Mix(Noise(0.4f, 0.35f, 3), Tone(0.45f, 98f, 0.5f, -20f)); break;
                case "sfx_hit": d = Mix(Noise(0.12f, 0.6f, 11), Tone(0.22f, 110f, 0.6f, -60f)); break;
                case "sfx_heartbeat": d = Mix(Thump(0f), Thump(0.22f)); break;
                case "sfx_pressure_in": d = Sweep(0.5f, 200f, 900f, 0.25f); break;
                case "sfx_pressure_out": d = Sweep(0.6f, 900f, 260f, 0.22f); break;
                case "sfx_slip": d = Mix(Tone(0.25f, 1320f, 0.2f, 0f), Delay(Tone(0.3f, 1760f, 0.18f, 0f), 0.08f)); break;
                case "sfx_end_good": d = Chord(1.6f, new[] { 392f, 494f, 587f, 784f }, 0.2f); break;
                case "sfx_end_bad": d = Chord(1.6f, new[] { 196f, 233f, 294f }, 0.24f); break;
                case "sfx_warn": d = Mix(Tone(0.05f, 1180f, 0.14f, 0f), Delay(Tone(0.05f, 1180f, 0.11f, 0f), 0.09f)); break;
                default: return null;
            }
            var clip = AudioClip.Create("argument_" + id, d.Length, 1, Rate, false);
            clip.SetData(d, 0);
            return clip;
        }

        static float[] Tone(float seconds, float hz, float gain, float slideHz)
        {
            int n = (int)(seconds * Rate);
            var d = new float[n];
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                phase += 2 * Math.PI * (hz + slideHz * t) / Rate;
                float env = Mathf.Min(1f, i / (0.004f * Rate)) * (1f - t) * (1f - t);
                d[i] = (float)Math.Sin(phase) * gain * env;
            }
            return d;
        }

        static float[] Chord(float seconds, float[] hz, float gain)
        {
            float[] d = new float[(int)(seconds * Rate)];
            foreach (var h in hz) d = Mix(d, Tone(seconds, h, gain / hz.Length * 2f, 0f));
            return d;
        }

        static float[] Noise(float seconds, float gain, int seed)
        {
            int n = (int)(seconds * Rate);
            var d = new float[n];
            var rng = new System.Random(seed);
            float last = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                last = Mathf.Lerp(last, (float)(rng.NextDouble() * 2 - 1), 0.35f);
                d[i] = last * gain * (1f - t) * (1f - t);
            }
            return d;
        }

        static float[] Thump(float at)
        {
            var beat = Tone(0.16f, 62f, 0.9f, -20f);
            return Delay(beat, at);
        }

        static float[] Sweep(float seconds, float from, float to, float gain)
        {
            int n = (int)(seconds * Rate);
            var d = new float[n];
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                phase += 2 * Math.PI * Mathf.Lerp(from, to, t) / Rate;
                float env = Mathf.Sin(t * Mathf.PI);
                d[i] = (float)Math.Sin(phase) * gain * env;
            }
            return d;
        }

        static float[] Delay(float[] d, float seconds)
        {
            int off = (int)(seconds * Rate);
            var r = new float[d.Length + off];
            Array.Copy(d, 0, r, off, d.Length);
            return r;
        }

        static float[] Mix(float[] a, float[] b)
        {
            var r = new float[Math.Max(a.Length, b.Length)];
            for (int i = 0; i < r.Length; i++) r[i] = (i < a.Length ? a[i] : 0f) + (i < b.Length ? b[i] : 0f);
            return r;
        }
    }
}
