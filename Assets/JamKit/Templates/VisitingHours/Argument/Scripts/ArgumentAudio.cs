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
            // The real-time battle (docs/redesign/battle.md): a shot, a glance off, a miss, a card burned, a showdown's
            // cut and swing, the phone of the three rings, the panic, a statement said, the last stand, a card turned,
            // a hold starting; and the voices' blips (one per speaker; a drawl plays its blips slower).
            "sfx_fire", "sfx_glance", "sfx_miss", "sfx_burn", "sfx_cut", "sfx_whiff", "sfx_ring", "sfx_panic",
            "sfx_statement", "sfx_stand", "sfx_cycle", "sfx_hold",
            "blip_lena", "blip_walt", "blip_nadia", "blip_mags",
        };
        const float FadeSeconds = 0.8f;

        static readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _clips.Clear();
            _blips.Clear();
        }

        AudioSource _music, _sfx, _voice;
        int _blipCount;
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
            _voice = gameObject.AddComponent<AudioSource>();
            _voice.playOnAwake = false;
            _voice.ignoreListenerPause = true;
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

        /// <summary>A voice blip for <paramref name="speaker"/> ("lena", "walt", "nadia", "mags"): a little pitch wobble
        /// so a line sounds spoken; slow for a drawl (lower, longer); quiet for a statement said again.</summary>
        public void Blip(string speaker, bool slow, bool quiet)
        {
            if (_voice == null) return;
            var clip = BlipClip(speaker);
            if (clip == null) return;
            _blipCount++;
            float wobble = ((_blipCount * 7919) % 13) / 13f - 0.5f;
            _voice.pitch = (slow ? 0.82f : 1f) * (1f + wobble * 0.12f);
            _voice.PlayOneShot(clip, quiet ? 0.35f : slow ? 0.8f : 0.6f);
        }

        // Each speaker's blip, found once (no string per blip: docs/redesign/perf.md, Battle 3).
        static readonly Dictionary<string, AudioClip> _blips = new Dictionary<string, AudioClip>();

        static AudioClip BlipClip(string speaker)
        {
            string key = string.IsNullOrEmpty(speaker) ? "lena" : speaker;
            if (_blips.TryGetValue(key, out var clip) && clip != null) return clip;
            clip = Clip("blip_" + key.ToLowerInvariant()) ?? Clip("blip_lena");
            _blips[key] = clip;
            return clip;
        }

        /// <summary>Loads each speaker's blip before the first line and plays it once, silent, so the first real blip
        /// doesn't hitch (docs/redesign/perf.md, Battle 3).</summary>
        public void Prewarm(params string[] speakers)
        {
            if (speakers == null) return;
            foreach (var s in speakers)
            {
                var clip = BlipClip(s);
                if (clip == null) continue;
                if (clip.loadState != AudioDataLoadState.Loaded) clip.LoadAudioData();
                if (_voice != null) _voice.PlayOneShot(clip, 0f);
            }
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
            if (_voice != null)
            {
                _voice.volume = master * sfx;
                _voice.mute = muted;
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
                case "sfx_fire": d = Mix(Noise(0.06f, 0.35f, 21), Sweep(0.12f, 500f, 1400f, 0.2f)); break;
                case "sfx_glance": d = Mix(Tone(0.08f, 1500f, 0.12f, -700f), Noise(0.05f, 0.2f, 5)); break;
                case "sfx_miss": d = Sweep(0.1f, 700f, 400f, 0.08f); break;
                case "sfx_burn": d = Mix(Noise(0.45f, 0.3f, 13), Tone(0.4f, 180f, 0.25f, -90f)); break;
                case "sfx_cut": d = Mix(Noise(0.09f, 0.55f, 17), Sweep(0.14f, 1800f, 600f, 0.18f)); break;
                case "sfx_whiff": d = Noise(0.12f, 0.18f, 19); break;
                case "sfx_ring": d = Mix(Mix(Tone(0.32f, 830f, 0.18f, 0f), Tone(0.32f, 1245f, 0.1f, 0f)), Delay(Mix(Tone(0.32f, 830f, 0.18f, 0f), Tone(0.32f, 1245f, 0.1f, 0f)), 0.4f)); break;
                case "sfx_panic": d = Mix(Sweep(1.6f, 120f, 60f, 0.35f), Noise(1.4f, 0.12f, 23)); break;
                case "sfx_statement": d = Sweep(0.22f, 300f, 520f, 0.06f); break;
                case "sfx_stand": d = Chord(1.1f, new[] { 147f, 175f, 220f }, 0.26f); break;
                case "sfx_cycle": d = Tone(0.04f, 990f, 0.12f, 0f); break;
                case "sfx_hold": d = Sweep(0.6f, 220f, 440f, 0.1f); break;
                case "blip_lena": d = Voice(0.05f, 330f); break;
                case "blip_walt": d = Voice(0.06f, 196f); break;
                case "blip_nadia": d = Voice(0.045f, 392f); break;
                case "blip_mags": d = Voice(0.045f, 440f); break;
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

        /// <summary>A soft spoken blip: a voiced tone with its octave and a breath of noise, quick in and out.</summary>
        static float[] Voice(float seconds, float hz)
        {
            var d = Mix(Tone(seconds, hz, 0.3f, hz * 0.08f), Tone(seconds, hz * 2f, 0.1f, 0f));
            return Mix(d, Noise(seconds * 0.6f, 0.04f, (int)hz));
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
