using System.Collections.Generic;
using JamKit.Shell;
using UnityEngine;

namespace JamKit.Movement
{
    /// <summary>
    /// The movement's sound (docs/MOVEMENT.md, "Sound"). Loops, made in code by <see cref="MoveSounds"/>: a wind body
    /// that comes in from 5 m/s and a bright air rush on top from 11 m/s (pitch up with speed, a little louder in the
    /// air, leaning away from a wallrun wall), a slide scrape that fades as the slide slows, a wallrun shoe rub.
    /// One-shots, recorded (Kenney CC0, Runtime/Audio/Movement) with round-robin variants, random pitch and volume:
    /// footsteps on the stride whose weight follows speed (a low thump joins them toward sprint; harder and brighter
    /// on a wall), the sprint kick's push-off, a jump swish, a distinct double-jump puff, the wall kick's snap, the
    /// wallrun catch (hand plant and lean-in), a slide drop, the boost whoosh, the mantle, and landings by fall speed
    /// (soft, normal, hard). Where a clip is missing the generated variants play; a clip in the game's Audio Registry
    /// under the same id replaces both. Loops play on their own sources at the effects volume; one-shots on a small
    /// pool of 2D voices here. Silent in batch mode like the shell.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public sealed class MovementAudio : MonoBehaviour
    {
        /// <summary>The Resources path of the recorded variants: <c>JamKitMovementAudio/mv_step_0</c>, <c>_1</c>...</summary>
        public const string ResourceFolder = "JamKitMovementAudio/";
        public const int MaxVariants = 8;
        const int Voices = 10;

        public MovementController body;
        [Header("Loops")]
        [Range(0f, 1f)] public float windVolume = 0.5f;
        [Range(0f, 1f)] public float windHighVolume = 0.4f;
        [Range(0f, 1f)] public float slideVolume = 0.45f;
        [Range(0f, 1f)] public float wallVolume = 0.3f;
        [Header("One-shots")]
        [Range(0f, 1f)] public float stepVolume = 0.32f;
        [Tooltip("Jumps, landings, the catch, kicks, the mantle, the boost.")]
        [Range(0f, 1f)] public float effectsVolume = 0.8f;
        [Tooltip("Random pitch per play, e.g. 0.05 = up to 5 % higher or lower.")]
        [Range(0f, 0.2f)] public float pitchVariance = 0.05f;
        [Tooltip("Random volume cut per play, e.g. 0.12 = up to 12 % quieter.")]
        [Range(0f, 0.4f)] public float volumeVariance = 0.12f;

        /// <summary>The last one-shot id played (tests, debugging).</summary>
        public string LastSound = "";

        sealed class Bank
        {
            public string id;
            public AudioClip[] clips;
            public int last = -1;
        }

        AudioSource _wind, _windHigh, _slide, _wall;
        float _windV, _windHighV, _slideV, _wallV, _pan;
        AudioSource[] _voices;
        int _nextVoice;
        float _sfx = 0.8f;
        bool _muted;
        Bank _step, _heavy, _jump, _djump, _wallJump, _catch, _landSoft, _land, _landHard, _mantle, _kick, _slideIn, _boost;

        public float WindLevel => _windV + _windHighV;

        /// <summary>The clips an id plays, best first: the game's Audio Registry clip, else the recorded variants in
        /// Resources (<see cref="ResourceFolder"/>), else the generated ones. Never empty for a movement id.</summary>
        public static AudioClip[] ClipsFor(string id, out bool recorded)
        {
            recorded = false;
            if (GameRoot.Exists && GameRoot.Config != null && GameRoot.Config.audio != null)
            {
                var entry = GameRoot.Config.audio.Find(id);
                if (entry != null && entry.clip != null)
                {
                    recorded = true;
                    return new[] { entry.clip };
                }
            }
            var found = new List<AudioClip>();
            for (int i = 0; i < MaxVariants; i++)
            {
                var c = Resources.Load<AudioClip>(ResourceFolder + id + "_" + i);
                if (c == null) break;
                found.Add(c);
            }
            if (found.Count > 0)
            {
                recorded = true;
                return found.ToArray();
            }
            int n = Mathf.Max(1, ProceduralAudio.VariantCount(id));
            var made = new List<AudioClip>(n);
            for (int i = 0; i < n; i++)
            {
                var c = ProceduralAudio.GetVariant(id, i);
                if (c != null) made.Add(c);
            }
            return made.ToArray();
        }

        void Start()
        {
            _wind = Loop("mv_wind");
            _windHigh = Loop("mv_wind_high");
            _slide = Loop("mv_slide");
            _wall = Loop("mv_wallrun");
            _voices = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f;
                src.dopplerLevel = 0f;
                src.priority = 64;
                _voices[i] = src;
            }
            _step = Load("mv_step");
            _heavy = Load("mv_step_heavy");
            _jump = Load("mv_jump");
            _djump = Load("mv_djump");
            _wallJump = Load("mv_walljump");
            _catch = Load("mv_catch");
            _landSoft = Load("mv_land_soft");
            _land = Load("mv_land");
            _landHard = Load("mv_land_hard");
            _mantle = Load("mv_mantle");
            _kick = Load("mv_kick");
            _slideIn = Load("mv_slidein");
            _boost = Load("mv_boost");
        }

        static Bank Load(string id) => new Bank { id = id, clips = ClipsFor(id, out _) };

        AudioSource Loop(string id)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = true;
            src.spatialBlend = 0f;
            src.dopplerLevel = 0f;
            src.volume = 0f;
            var clips = ClipsFor(id, out _);
            src.clip = clips.Length > 0 ? clips[0] : null;
            if (src.clip != null)
            {
                // Start each loop at a different point so the layers don't line up.
                src.timeSamples = (int)(src.clip.samples * Random.value) % Mathf.Max(1, src.clip.samples);
                src.Play();
            }
            return src;
        }

        void Update()
        {
            if (body == null || body.Sim == null || _wind == null) return;
            bool exists = GameRoot.Exists;
            _sfx = exists ? GameRoot.Settings.sfxVolume : 0.8f;
            _muted = exists && GameRoot.Audio.Muted;
            bool paused = Time.timeScale <= 0f;
            float udt = Time.unscaledDeltaTime;
            var sim = body.Sim;
            var mode = sim.Mode;
            float speed = sim.HorizontalSpeed;
            var ev = body.FrameEvents;

            // Air: the wind lifts a little off the ground; on a wall it leans to the open side.
            float lift = mode == MoveMode.Air ? 1.12f : 1f;
            float wind = MoveSoundMix.WindLow(speed) * windVolume * lift;
            float windHigh = MoveSoundMix.WindHigh(speed) * windHighVolume * lift;
            float slide = mode == MoveMode.Slide ? slideVolume * MoveSoundMix.SlideGain(speed) : 0f;
            float wall = mode == MoveMode.Wallrun ? wallVolume * (0.6f + 0.4f * MoveMath.Clamp01(speed / 8.64f)) : 0f;
            if (paused) wind = windHigh = slide = wall = 0f;
            // Rises quickly, falls slowly, so a stumble doesn't drop the speed sound at once.
            _windV = Mathf.MoveTowards(_windV, wind, udt * (wind > _windV ? 2f : 0.9f));
            _windHighV = Mathf.MoveTowards(_windHighV, windHigh, udt * (windHigh > _windHighV ? 2f : 0.9f));
            _slideV = Mathf.MoveTowards(_slideV, slide, udt * 8f);
            _wallV = Mathf.MoveTowards(_wallV, wall, udt * 10f);
            _pan = Mathf.MoveTowards(_pan, -0.3f * sim.WallSide, udt * 2f);
            float windPitch = MoveSoundMix.WindPitch(speed);
            Set(_wind, _windV * _sfx, windPitch, _pan);
            Set(_windHigh, _windHighV * _sfx, windPitch, _pan);
            Set(_slide, _slideV * _sfx, MoveSoundMix.SlidePitch(speed), 0f);
            Set(_wall, _wallV * _sfx, 0.9f + 0.2f * MoveMath.Clamp01(speed / 12f), 0f);

            if (!exists || paused) return;
            float fx = effectsVolume;
            bool foot = false; // a footstep sound already came with an event this frame
            if (ev.wallrunStarted) Play(_catch, 0.85f * fx, 1f);
            if (ev.wallJumped)
            {
                // A wall kick (jump within 83 ms of contact) snaps harder and a little higher.
                Play(_wallJump, (ev.wallKick ? 1f : 0.85f) * fx, ev.wallKick ? 1.06f : 1f);
                foot = true;
            }
            else if (ev.doubleJumped) Play(_djump, 0.75f * fx, 1f);
            else if (ev.jumped)
            {
                Play(_jump, 0.6f * fx, 1f);
                foot = true;
            }
            if (ev.slideStarted)
            {
                Play(_slideIn, (0.5f + 0.5f * MoveSoundMix.SlideGain(speed)) * 0.8f * fx, 1f);
                foot = true;
            }
            if (ev.slideBoosted) Play(_boost, 0.6f * fx, 1f);
            if (ev.mantleStarted) Play(_mantle, 0.75f * fx, 1f);
            if (ev.sprintKick)
            {
                Play(_kick, 0.9f * fx, 1f);
                Play(_boost, 0.22f * fx, 0.9f);
                foot = true;
            }
            if (ev.landed && !ev.slideStarted)
            {
                float gain = MoveSoundMix.LandGain(ev.landImpact) * fx;
                switch (MoveSoundMix.Landing(ev.landImpact))
                {
                    case MoveSoundMix.LandTier.Step: FootStep(speed, false); break;
                    case MoveSoundMix.LandTier.Soft: Play(_landSoft, gain * 0.75f, 1f); break;
                    case MoveSoundMix.LandTier.Normal: Play(_land, gain, 1f); break;
                    default: Play(_landHard, gain, 1f); break;
                }
                foot = true;
            }
            if (body.Stepped && !foot) FootStep(speed, mode == MoveMode.Wallrun);
        }

        void FootStep(float speed, bool onWall)
        {
            if (onWall)
            {
                // On a wall: 1.5x the cadence (MovementController), harder and brighter, no thump.
                Play(_step, stepVolume * 1.1f, 1.1f);
                return;
            }
            var def = body.Def;
            float w = MoveSoundMix.StepWeight(speed, def != null ? def.walkSpeed : 5f, def != null ? def.sprintSpeed : 9.5f);
            Play(_step, stepVolume * MoveSoundMix.StepVolume(w), MoveSoundMix.StepPitch(w));
            float heavy = MoveSoundMix.StepHeavy(w);
            if (heavy > 0.02f) Play(_heavy, stepVolume * heavy * 1.6f, 1f);
        }

        void Play(Bank bank, float volume, float pitch)
        {
            if (bank == null || bank.clips == null || bank.clips.Length == 0 || _voices == null) return;
            int k = MoveSoundMix.NextVariant(bank.clips.Length, bank.last, Random.value);
            bank.last = k;
            var clip = bank.clips[k];
            if (clip == null) return;
            var src = _voices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % _voices.Length;
            // A single clip (an Audio Registry override) gets a wider pitch spread to hide the repeat.
            float pv = bank.clips.Length >= 3 ? pitchVariance : pitchVariance * 1.6f;
            src.pitch = pitch * (1f + Random.Range(-pv, pv));
            src.volume = Mathf.Clamp01(volume * (1f - Random.value * volumeVariance) * _sfx);
            src.mute = _muted;
            src.clip = clip;
            src.Play();
            LastSound = bank.id;
        }

        void Set(AudioSource s, float volume, float pitch, float pan)
        {
            if (s == null) return;
            s.volume = volume;
            s.pitch = pitch;
            s.panStereo = pan;
            s.mute = _muted;
        }
    }
}
