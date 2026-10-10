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
    /// <para>Speed (SPEED-FEEL A1, A2, A4, A5, A6): it follows the very tier the speed visuals tick
    /// (<see cref="SpeedFeel"/>'s, from the tuning asset's Speed Tiers), so the boom and the music agree on when flying
    /// starts; with no SpeedFeel in the scene it ticks a tier of its own from the same thresholds. It drives the speed music
    /// (<see cref="music"/>: stems by tier, muffled when slow and open at flying), a supersonic whistle while flying
    /// (leaning away from a wallrun wall), tunnel focus (world and ambient sounds duck while flying; the music and
    /// wind don't) and Nadia's breathing, quiet, quickening with time spent fast. Every level is in this component's
    /// inspector.</para>
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
        [Header("Speed (docs/MOVEMENT.md, Speed music)")]
        [Tooltip("The speed music this body drives (stems by tier). Optional.")]
        public MovementMusic music;
        [Tooltip("The speed visuals' hub whose tier the sound follows (found on the body's camera when empty). Without one: "
            + "a tier of its own, from the body's tuning asset (Speed Tiers).")]
        public SpeedFeel speedFeel;
        [Range(0f, 1f)] public float musicVolume = 0.45f;
        [Tooltip("A4: the supersonic whistle while flying.")]
        [Range(0f, 1f)] public float supersonicVolume = 0.14f;
        [Tooltip("A5: how far world and ambient sounds duck while flying (0 = off, 0.45 = down to 55 %).")]
        [Range(0f, 1f)] public float worldDuck = 0.45f;
        [Tooltip("A6: Nadia's breathing, kept low (a texture; 0 = off).")]
        [Range(0f, 0.3f)] public float breathVolume = 0.05f;
        [Tooltip("Seconds of flying for the breath to reach full panting (twice that while fast).")]
        public float breathBuild = 8f;
        [Tooltip("Seconds for the breath to calm again.")]
        public float breathRecover = 10f;

        /// <summary>The last one-shot id played (tests, debugging).</summary>
        public string LastSound = "";

        sealed class Bank
        {
            public string id;
            public AudioClip[] clips;
            public int last = -1;
        }

        AudioSource _wind, _windHigh, _slide, _wall, _supersonic, _breathCalm, _breathFast;
        float _windV, _windHighV, _slideV, _wallV, _pan, _exertion, _worldGain = 1f, _duckScan;
        SpeedTier _speed, _ownTier;
        float _speedNow;
        bool _pausedNow, _existsNow;
        readonly List<AudioSource> _duckSrc = new List<AudioSource>();
        readonly List<float> _duckBase = new List<float>();
        readonly List<float> _duckSet = new List<float>();
        readonly HashSet<int> _duckSeen = new HashSet<int>();
        AudioSource[] _voices;
        int _nextVoice;
        float _sfx = 0.8f;
        bool _muted;
        Bank _step, _heavy, _jump, _djump, _wallJump, _catch, _landSoft, _land, _landHard, _mantle, _kick, _slideIn, _boost;

        public float WindLevel => _windV + _windHighV;
        /// <summary>The speed tier the sound follows (null before the first frame): SpeedFeel's own when there is one.</summary>
        public SpeedTier Speed => _speed;
        /// <summary>True when the sound reads the speed visuals' tier (one tick, one source of truth).</summary>
        public bool SharesTheVisualTier => _speed != null && _ownTier == null;

        /// <summary>The speed visuals' hub for a body: on its camera (where the builders put it), else under it.</summary>
        public static SpeedFeel FindFeel(MovementController body)
        {
            if (body == null) return null;
            var feel = body.cam != null ? body.cam.GetComponent<SpeedFeel>() : null;
            return feel != null ? feel : body.GetComponentInChildren<SpeedFeel>(true);
        }

        SpeedTier ResolveTier()
        {
            if (speedFeel != null && speedFeel.State != null)
            {
                _ownTier = null;
                return speedFeel.State.Tier;
            }
            if (_ownTier == null) _ownTier = new SpeedTier(body != null && body.tuning != null ? body.tuning.speedTiers : null);
            return _ownTier;
        }
        /// <summary>A6: 0 calm .. 1 out of breath.</summary>
        public float Exertion => _exertion;
        /// <summary>A5: the gain on world and ambient sounds now (1 = no duck).</summary>
        public float WorldGain => _worldGain;

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
            _supersonic = Loop("mv_supersonic");
            _breathCalm = Loop("mv_breath_calm");
            _breathFast = Loop("mv_breath_fast");
            if (speedFeel == null) speedFeel = FindFeel(body);
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

            // The speed layers follow in LateUpdate, after SpeedFeel has ticked this frame's tier.
            _speedNow = speed;
            _pausedNow = paused;
            _existsNow = exists;

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

        void LateUpdate()
        {
            if (body == null || body.Sim == null || _wind == null) return;
            // Speed: the tier (SpeedFeel's, ticked in its LateUpdate just before this one; else our own), the music,
            // the supersonic whistle, the breath, the duck. Frozen while paused.
            _speed = ResolveTier();
            float gameDt = _pausedNow ? 0f : Time.deltaTime;
            if (_ownTier != null) _ownTier.Tick(_speedNow, gameDt);
            float flyingEnter = _speed.Def.flyingEnter;
            float fly = _pausedNow ? 0f : _speed.FlyingBlend;
            if (music != null)
            {
                music.volume = musicVolume;
                music.SetSpeed(_speed.Tier, StemMixer.Open(_speedNow, flyingEnter, _speed.FlyingBlend));
            }
            Set(_supersonic, supersonicVolume * fly * _sfx, MoveSoundMix.SupersonicPitch(_speedNow, flyingEnter), _pan * 1.2f);
            _exertion = MoveSoundMix.Exertion(_exertion, _speed.Tier, gameDt, breathBuild, breathRecover);
            float breath = _pausedNow ? 0f : breathVolume * _sfx;
            Set(_breathCalm, breath * 0.5f * (1f - _exertion) * MoveMath.Clamp01(_speedNow / 4f), 1f, 0f);
            Set(_breathFast, breath * _exertion, 1f + 0.06f * _exertion, 0f);
            _worldGain = MoveSoundMix.Duck(fly, worldDuck);
            if (_existsNow) GameRoot.Audio.WorldGain = _worldGain;

            // Tunnel focus for the scene's own sounds (the static's hiss and the like), after their owners set them.
            _duckScan -= Time.unscaledDeltaTime;
            if (_duckScan <= 0f)
            {
                _duckScan = 2f;
                ScanWorldSources();
            }
            for (int i = _duckSrc.Count - 1; i >= 0; i--)
            {
                var s = _duckSrc[i];
                if (s == null)
                {
                    _duckSrc.RemoveAt(i);
                    _duckBase.RemoveAt(i);
                    _duckSet.RemoveAt(i);
                    continue;
                }
                if (Mathf.Abs(s.volume - _duckSet[i]) > 1e-4f) _duckBase[i] = s.volume; // its owner set a new level
                s.volume = _duckBase[i] * _worldGain;
                _duckSet[i] = s.volume;
            }
        }

        /// <summary>Finds the scene's world sounds: every AudioSource that isn't this body's, the music's or the
        /// shell's (the shell's ambience and effects duck through AudioService.WorldGain). Every 2 s, not every frame.</summary>
        void ScanWorldSources()
        {
            var all = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
            var shell = GameRoot.Exists ? GameRoot.Instance.transform : null;
            foreach (var s in all)
            {
                if (s == null || !_duckSeen.Add(s.GetInstanceID())) continue;
                if (s.transform.IsChildOf(transform)) continue;
                if (shell != null && s.transform.IsChildOf(shell)) continue;
                if (s.GetComponentInParent<MovementMusic>(true) != null) continue;
                _duckSrc.Add(s);
                _duckBase.Add(s.volume);
                _duckSet.Add(s.volume);
            }
        }

        void OnDisable()
        {
            for (int i = 0; i < _duckSrc.Count; i++)
                if (_duckSrc[i] != null) _duckSrc[i].volume = _duckBase[i];
            _duckSrc.Clear();
            _duckBase.Clear();
            _duckSet.Clear();
            _duckSeen.Clear();
            _worldGain = 1f;
            if (GameRoot.Exists) GameRoot.Audio.WorldGain = 1f;
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
