using System.Collections.Generic;
using JamKit.Shell;
using UnityEngine;

namespace JamKit.Movement
{
    /// <summary>
    /// Music that gets more excited the faster you go (docs/MOVEMENT.md "Speed music"; SPEED-FEEL A1 and A2). Up to
    /// four stems of one length and tempo play in sync: <b>base</b> (always), <b>drive</b> (from the fast tier),
    /// <b>peak</b> (flying) and an optional <b>tension</b> stem the game drives itself (the dream escape's gap). Stems
    /// switch only on bar lines and fade in over about 1 s, out over about 2 s (<see cref="StemMixer"/>). Each stem
    /// also has a muffled (low-passed) copy playing in step; <see cref="SetSpeed"/>'s "open" crossfades from muffled
    /// at a standstill to full and bright at flying (works on WebGL: no real-time filter).
    /// <para>Where the stems come from, best first: <see cref="stems"/> set in the inspector; else the files
    /// <c>Resources/JamKitMusicStems/&lt;stemSet&gt;/base</c>, <c>drive</c>, <c>peak</c>, <c>tension</c> (any Resources
    /// folder; muffled copies may sit beside them as <c>base_muffled</c>..., otherwise they're made at load where the
    /// platform allows); else placeholders a template supplies (<see cref="SetPlaceholders"/>); else the
    /// code-made placeholder stems (<see cref="MoveMusicSynth"/>). MovementAudio drives the tier, the opening and the
    /// volume. Follows the shell's music volume and mute.</para>
    /// </summary>
    public sealed class MovementMusic : MonoBehaviour
    {
        public const string ResourceRoot = "JamKitMusicStems/";
        const float PlaceholderMuffleHz = 700f;

        [Tooltip("The folder under Resources/JamKitMusicStems/ holding base, drive, peak (and tension) stems. Empty: placeholders.")]
        public string stemSet = "";
        [Tooltip("Optional: stems set here win over the folder (base, drive, peak, tension).")]
        public AudioClip[] stems = new AudioClip[0];
        [Tooltip("Optional: their muffled copies, same order.")]
        public AudioClip[] muffledStems = new AudioClip[0];
        [Tooltip("Optional: AudioSources to play the full stems on (same order); made as children if missing.")]
        public AudioSource[] stemSources = new AudioSource[0];
        public float bpm = MoveMusicSynth.Bpm;
        public int beatsPerBar = MoveMusicSynth.BeatsPerBar;
        public bool playOnStart = true;
        [Range(0f, 1f)] public float volume = 0.5f;
        [Tooltip("Each stem's level: base, drive, peak, tension.")]
        public float[] stemLevels = { 1f, 0.85f, 0.7f, 0.7f };
        [Tooltip("The muffled copies' cutoff when they are made at load (Hz).")]
        public float muffledHz = PlaceholderMuffleHz;
        [Tooltip("Muffling takes some loudness away; this gives a little back.")]
        public float muffledGain = 1.25f;
        public StemMixDef mix = new StemMixDef();

        static readonly AudioClip[] DefaultFull = new AudioClip[StemMixer.Slots];
        static readonly AudioClip[] DefaultMuffled = new AudioClip[StemMixer.Slots];
        static readonly Dictionary<AudioClip, AudioClip> MuffledCache = new Dictionary<AudioClip, AudioClip>();

        readonly AudioClip[] _full = new AudioClip[StemMixer.Slots];
        readonly AudioClip[] _muffled = new AudioClip[StemMixer.Slots];
        readonly AudioClip[] _placeholders = new AudioClip[StemMixer.Slots];
        readonly AudioSource[] _src = new AudioSource[StemMixer.Slots];
        readonly AudioSource[] _msrc = new AudioSource[StemMixer.Slots];
        readonly float[] _pitch = { 1f, 1f, 1f, 1f };
        readonly float[] _floors = new float[StemMixer.Slots];
        StemMixer _mixer;
        bool _playing, _resolved;
        float _master, _masterTarget, _open, _openTarget, _openFloor, _dspStall;
        int _tier;
        double _startDsp, _clock, _lastDsp;
        bool _useDsp = true;

        public bool Playing => _playing;
        public int Tier => _tier;
        /// <summary>0 muffled .. 1 full and bright (smoothed).</summary>
        public float Open => _open;
        public StemMixer Mixer => _mixer;
        /// <summary>Stems switched on by speed now (base counts; 0 when not playing).</summary>
        public int ActiveStems => _playing && _mixer != null ? _mixer.ActiveCount : 0;
        /// <summary>The music's position in beats since the stems started.</summary>
        public double Beat => _clock * bpm / 60.0;
        public float Volume01(int slot) => _mixer != null ? _mixer.Gain(slot) : 0f;
        public AudioClip StemClip(int slot) { Resolve(); return _full[slot]; }
        public AudioClip MuffledClip(int slot) { Resolve(); return _muffled[slot]; }

        /// <summary>Template placeholders for slots with no real stem (base, drive, peak, tension; nulls skip).</summary>
        public void SetPlaceholders(params AudioClip[] clips)
        {
            for (int i = 0; i < StemMixer.Slots; i++) _placeholders[i] = clips != null && i < clips.Length ? clips[i] : null;
            _resolved = false;
        }

        /// <summary>Speed's say: the tier (0 running, 1 fast, 2 flying) and how open the music is (0..1).</summary>
        public void SetSpeed(int tier, float open)
        {
            _tier = tier;
            _openTarget = Mathf.Clamp01(open);
        }

        /// <summary>Keep the music at least this open whatever the speed (the escape's gap opens it partway).</summary>
        public void SetOpenFloor(float open) => _openFloor = Mathf.Clamp01(open);
        /// <summary>Keep a stem at least this loud whatever the tier (continuous, not quantised).</summary>
        public void SetFloor(int slot, float gain) => _floors[slot] = Mathf.Clamp01(gain);
        public void SetPitch(int slot, float pitch) => _pitch[slot] = pitch;

        /// <summary>Puts a stem back in step with the base (after a detune let it drift).</summary>
        public void Resync(int slot)
        {
            var b = _src[StemMixer.Base];
            if (b == null || !b.isPlaying || _src[slot] == null || !_src[slot].isPlaying) return;
            int at = b.timeSamples;
            if (Mathf.Abs(_src[slot].timeSamples - at) > 800) _src[slot].timeSamples = at;
            if (_msrc[slot] != null && _msrc[slot].isPlaying) _msrc[slot].timeSamples = at;
        }

        void Start()
        {
            if (playOnStart) Begin();
        }

        /// <summary>Starts every stem together (fading in over a second). Again while playing: just fades back in.</summary>
        public void Begin()
        {
            _masterTarget = 1f;
            if (_playing) return;
            Resolve();
            double at = AudioSettings.dspTime + 0.1;
            int length = -1;
            for (int slot = 0; slot < StemMixer.Slots; slot++)
            {
                if (_full[slot] == null) continue;
                if (length < 0) length = _full[slot].samples;
                else if (_full[slot].samples != length)
                    Debug.LogWarning("JamKit: the music stems differ in length (" + _full[slot].name + "); they will drift apart. Make every stem the same length.");
                if (_src[slot] == null) _src[slot] = slot < stemSources.Length && stemSources[slot] != null ? stemSources[slot] : Child("Stem " + StemMixer.SlotNames[slot]);
                Prepare(_src[slot], _full[slot]);
                _src[slot].PlayScheduled(at);
                if (_muffled[slot] != null)
                {
                    if (_msrc[slot] == null) _msrc[slot] = Child("Stem " + StemMixer.SlotNames[slot] + " (muffled)");
                    Prepare(_msrc[slot], _muffled[slot]);
                    _msrc[slot].PlayScheduled(at);
                }
            }
            _mixer = new StemMixer(mix);
            _startDsp = at;
            _lastDsp = AudioSettings.dspTime;
            _clock = -0.1;
            _dspStall = 0f;
            _useDsp = true;
            _master = 0f;
            _playing = true;
        }

        /// <summary>Fades the music out (over about 2 s), then stops.</summary>
        public void End() => _masterTarget = 0f;

        public void StopNow()
        {
            _masterTarget = 0f;
            _master = 0f;
            if (!_playing) return;
            for (int slot = 0; slot < StemMixer.Slots; slot++)
            {
                if (_src[slot] != null) _src[slot].Stop();
                if (_msrc[slot] != null) _msrc[slot].Stop();
            }
            _playing = false;
        }

        void Update()
        {
            if (!_playing) return;
            float udt = Time.unscaledDeltaTime;
            AdvanceClock(udt);
            for (int slot = 0; slot < StemMixer.Slots; slot++) _mixer.SetFloor(slot, _floors[slot]);
            _mixer.Tick(Beat, _tier, udt);
            _master = Mathf.MoveTowards(_master, _masterTarget, (_masterTarget > _master ? 1f : 0.5f) * udt);
            if (_master <= 0f && _masterTarget <= 0f) { StopNow(); return; }
            float target = Mathf.Max(_openTarget, _openFloor);
            _open = Mathf.MoveTowards(_open, target, (target > _open ? 1.2f : 0.5f) * udt);

            bool exists = GameRoot.Exists;
            float music = exists ? GameRoot.Settings.musicVolume : 0.7f;
            bool muted = exists && GameRoot.Audio.Muted;
            float m = volume * music * _master;
            for (int slot = 0; slot < StemMixer.Slots; slot++)
            {
                var s = _src[slot];
                if (s == null || s.clip == null) continue;
                float level = slot < stemLevels.Length ? stemLevels[slot] : 1f;
                float g = m * level * _mixer.Gain(slot);
                var ms = _msrc[slot];
                bool hasMuffled = ms != null && ms.clip != null;
                s.volume = hasMuffled ? g * _open : g;
                s.pitch = _pitch[slot];
                s.mute = muted;
                if (!hasMuffled) continue;
                ms.volume = Mathf.Clamp01(g * (1f - _open) * muffledGain);
                ms.pitch = _pitch[slot];
                ms.mute = muted;
            }
        }

        /// <summary>The audio clock when the audio system runs; real time when it doesn't (no output device).</summary>
        void AdvanceClock(float udt)
        {
            double dsp = AudioSettings.dspTime;
            if (dsp > _lastDsp)
            {
                _lastDsp = dsp;
                _dspStall = 0f;
            }
            else _dspStall += udt;
            if (_useDsp && _dspStall > 0.5f) _useDsp = false;
            if (_useDsp) _clock = dsp - _startDsp;
            else _clock += udt;
        }

        AudioSource Child(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.AddComponent<AudioSource>();
        }

        static void Prepare(AudioSource s, AudioClip clip)
        {
            s.clip = clip;
            s.loop = true;
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            s.dopplerLevel = 0f;
            s.volume = 0f;
            var filter = s.GetComponent<AudioLowPassFilter>();
            if (filter != null) filter.enabled = false; // the muffled copy does this job now
        }

        /// <summary>Finds every slot's clips (see the class summary for the order).</summary>
        public void Resolve()
        {
            if (_resolved) return;
            for (int slot = 0; slot < StemMixer.Slots; slot++)
            {
                string name = StemMixer.SlotNames[slot];
                AudioClip full = slot < stems.Length ? stems[slot] : null;
                AudioClip muffled = slot < muffledStems.Length ? muffledStems[slot] : null;
                if (full == null && !string.IsNullOrEmpty(stemSet))
                {
                    full = Resources.Load<AudioClip>(ResourceRoot + stemSet + "/" + name);
                    if (full != null && muffled == null) muffled = Resources.Load<AudioClip>(ResourceRoot + stemSet + "/" + name + "_muffled");
                }
                if (full == null) full = _placeholders[slot];
                if (full == null && slot < StemMixer.Tension)
                {
                    full = Default(slot);
                    if (muffled == null) muffled = DefaultMuffled[slot];
                }
                if (full != null && muffled == null) muffled = MakeMuffled(full, muffledHz);
                _full[slot] = full;
                _muffled[slot] = muffled;
            }
            _resolved = true;
        }

        /// <summary>The code-made placeholder stem for a slot (and its muffled copy), made once per session.</summary>
        public static AudioClip Default(int slot)
        {
            if (slot < 0 || slot >= StemMixer.Tension) return null;
            if (DefaultFull[slot] != null) return DefaultFull[slot];
            var data = MoveMusicSynth.Make(slot);
            DefaultFull[slot] = Make("speed_music_" + StemMixer.SlotNames[slot], data, 1);
            var lp = (float[])data.Clone();
            StemMixer.LowPassZeroPhase(lp, MoveMusicSynth.Rate, PlaceholderMuffleHz);
            DefaultMuffled[slot] = Make("speed_music_" + StemMixer.SlotNames[slot] + "_muffled", lp, 1);
            return DefaultFull[slot];
        }

        static AudioClip Make(string name, float[] data, int channels)
        {
            var clip = AudioClip.Create(name, data.Length / channels, channels, MoveMusicSynth.Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>A muffled copy of any readable clip (Decompress On Load, or made in code), or null where the
        /// samples can't be read (WebGL): then that stem just doesn't muffle.</summary>
        static AudioClip MakeMuffled(AudioClip clip, float hz)
        {
            if (clip == null) return null;
            if (MuffledCache.TryGetValue(clip, out var cached) && cached != null) return cached;
            if (Application.platform == RuntimePlatform.WebGLPlayer) return null;
            if (clip.loadType == AudioClipLoadType.Streaming) return null;
            clip.LoadAudioData();
            int ch = clip.channels, n = clip.samples;
            var data = new float[n * ch];
            if (!clip.GetData(data, 0)) return null;
            var one = new float[n];
            for (int c = 0; c < ch; c++)
            {
                for (int i = 0; i < n; i++) one[i] = data[i * ch + c];
                StemMixer.LowPassZeroPhase(one, clip.frequency, hz);
                for (int i = 0; i < n; i++) data[i * ch + c] = one[i];
            }
            var m = AudioClip.Create(clip.name + "_muffled", n, ch, clip.frequency, false);
            m.SetData(data, 0);
            MuffledCache[clip] = m;
            return m;
        }
    }
}
