using UnityEngine;
using UnityEngine.Audio;

namespace JamKit.Look
{
    /// <summary>
    /// A positional ambience source on the hill: a 3D loop (birds in a tree, a lawnmower, a sprinkler, the ball game's
    /// crowd) and/or one-shots picked at random from <see cref="oneShots"/> at random intervals, in bursts if asked (a dog's
    /// barks), each with a little pitch and level variation.
    /// <list type="bullet">
    /// <item>The loop starts at a random point and a slightly different pitch, so two emitters on one clip never phase.</item>
    /// <item><see cref="wander"/>: PingPong walks back and forth between two points (a mower doing a lawn); Pass drives
    /// once from one end to the other per active stretch, alternating direction (the ice-cream truck going by).</item>
    /// <item><see cref="intermittent"/>: active stretches and silent stretches, faded, so a sound comes and goes.</item>
    /// </list>
    /// Its AudioSources are made once in Awake (one loop, two one-shot voices); Update allocates nothing. Level =
    /// <see cref="volume"/> x the clip's own volume x the fades x <see cref="VhAmbienceMix.Level"/>. Built by
    /// <c>VhLookAmbience.AddEmitter</c>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VhAmbienceEmitter : MonoBehaviour
    {
        public enum WanderMode { None, PingPong, Pass }

        [Header("Level")]
        [Range(0f, 1f)] public float volume = 1f;
        public AudioMixerGroup output;
        [Tooltip("0..256, lower is more important; ambience yields voices to gameplay sounds.")]
        public int priority = 160;

        [Header("Loop")]
        public AudioClip loopClip;
        [Range(0f, 1f)] public float loopVolume = 1f;
        [Tooltip("Random pitch offset picked once per emitter (0.03 = up to +/-3%).")]
        public float loopPitchJitter = 0.03f;

        [Header("One-shots")]
        public AudioClip[] oneShots;
        [Range(0f, 1f)] public float oneShotVolume = 1f;
        [Tooltip("Seconds between bursts (random in range).")]
        public Vector2 oneShotInterval = new Vector2(6f, 18f);
        [Tooltip("Shots per burst (random in range, inclusive).")]
        public Vector2Int burstCount = new Vector2Int(1, 1);
        [Tooltip("Seconds between the shots of one burst.")]
        public Vector2 burstGap = new Vector2(0.4f, 1.0f);
        public Vector2 pitchRange = new Vector2(0.95f, 1.05f);
        [Tooltip("Random level cut per shot, 0..1 (0.25 = each shot plays at 75-100%).")]
        [Range(0f, 1f)] public float volumeJitter = 0.25f;

        [Header("3D")]
        public float minDistance = 5f;
        public float maxDistance = 200f;
        [Range(0f, 360f)] public float spread = 60f;
        public AudioRolloffMode rolloff = AudioRolloffMode.Logarithmic;

        [Header("Wander (offsets from where the emitter was placed, in its parent's space)")]
        public WanderMode wander = WanderMode.None;
        public Vector3 wanderFrom;
        public Vector3 wanderTo;
        [Tooltip("Metres per second along the line.")]
        public float wanderSpeed = 1f;

        [Header("Only sometimes")]
        public bool intermittent;
        [Tooltip("Seconds a sound stretch lasts (ignored in Pass mode: a stretch is one pass).")]
        public Vector2 activeDuration = new Vector2(20f, 60f);
        public Vector2 silentDuration = new Vector2(40f, 120f);
        [Tooltip("Fade in/out at the edges of a stretch, seconds.")]
        public float stretchFade = 3f;
        [Tooltip("Start in a silent stretch (else in an active one).")]
        public bool startSilent;

        [Header("Random")]
        [Tooltip("0 = a different sequence every run.")]
        public int seed;

        AudioSource _loop;
        AudioSource _shotA;
        AudioSource _shotB;
        bool _useA;
        uint _rng = 1u;
        float _loopPitch = 1f;
        Vector3 _home;
        bool _active = true;
        float _stretchEnd = float.MaxValue;
        float _gate = 1f;
        float _nextShot;
        int _burstLeft;
        float _passStart;
        float _passDuration = 1f;
        bool _passForward = true;
        float _wanderStart;

        void Awake()
        {
            _home = transform.localPosition;
            _rng = seed != 0 ? (uint)seed * 2654435761u : (uint)GetInstanceID() * 2654435761u ^ (uint)System.Environment.TickCount;
            if (_rng == 0u) _rng = 0x9E3779B9u;
            _loopPitch = 1f + (Rand01() * 2f - 1f) * loopPitchJitter;
            _loop = MakeSource();
            _loop.loop = true;
            _loop.pitch = _loopPitch;
            _shotA = MakeSource();
            _shotB = MakeSource();
        }

        AudioSource MakeSource()
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.hideFlags = HideFlags.DontSave;
            s.playOnAwake = false;
            s.spatialBlend = 1f;
            s.dopplerLevel = 0f;
            s.rolloffMode = rolloff;
            s.minDistance = minDistance;
            s.maxDistance = maxDistance;
            s.spread = spread;
            s.priority = priority;
            s.outputAudioMixerGroup = output;
            s.volume = 0f;
            return s;
        }

        void OnEnable()
        {
            if (_loop == null) return;
            float now = Time.time;
            _wanderStart = now;
            _burstLeft = 0;
            _nextShot = now + Range(oneShotInterval) * Rand01();
            if (intermittent || wander == WanderMode.Pass)
            {
                _active = !startSilent;
                _gate = 0f;
                if (_active) BeginActive(now);
                else _stretchEnd = now + Range(silentDuration) * (0.3f + 0.7f * Rand01());
            }
            else
            {
                _active = true;
                _gate = 0f;
                _stretchEnd = float.MaxValue;
                StartLoop();
            }
            UpdateWander(now);
            ApplyLoopVolume();
        }

        void OnDisable()
        {
            if (_loop != null) _loop.Stop();
            if (_shotA != null) _shotA.Stop();
            if (_shotB != null) _shotB.Stop();
        }

        void BeginActive(float now)
        {
            _active = true;
            if (wander == WanderMode.Pass)
            {
                float len = (wanderTo - wanderFrom).magnitude;
                _passDuration = Mathf.Max(1f, len / Mathf.Max(0.01f, wanderSpeed));
                _passStart = now;
                _stretchEnd = now + _passDuration;
            }
            else
            {
                _stretchEnd = intermittent ? now + Range(activeDuration) : float.MaxValue;
            }
            _nextShot = now + Range(oneShotInterval) * (0.2f + 0.5f * Rand01());
            StartLoop();
        }

        void StartLoop()
        {
            if (loopClip == null || _loop == null) return;
            _loop.clip = loopClip;
            _loop.Play();
            if (loopClip.samples > 0) _loop.timeSamples = (int)(Rand01() * (loopClip.samples - 1));
        }

        void Update()
        {
            float now = Time.time;
            float dt = Time.deltaTime;

            if ((intermittent || wander == WanderMode.Pass) && now >= _stretchEnd)
            {
                if (_active)
                {
                    _active = false;
                    _stretchEnd = now + Range(silentDuration);
                    if (wander == WanderMode.Pass) _passForward = !_passForward;
                }
                else
                {
                    BeginActive(now);
                }
            }

            float fade = Mathf.Max(0.01f, stretchFade);
            _gate = Mathf.MoveTowards(_gate, _active ? 1f : 0f, dt / fade);
            if (!_active && _gate <= 0f && _loop.isPlaying) _loop.Stop();

            UpdateWander(now);
            ApplyLoopVolume();

            if (_active && oneShots != null && oneShots.Length > 0 && now >= _nextShot)
            {
                PlayShot();
                if (_burstLeft <= 0) _burstLeft = Mathf.Max(1, Random32(burstCount.x, burstCount.y));
                _burstLeft--;
                _nextShot = now + (_burstLeft > 0 ? Range(burstGap) : Range(oneShotInterval));
            }
        }

        void UpdateWander(float now)
        {
            if (wander == WanderMode.None) return;
            float u;
            if (wander == WanderMode.PingPong)
            {
                float len = Mathf.Max(0.01f, (wanderTo - wanderFrom).magnitude);
                float phase = Mathf.PingPong((now - _wanderStart) * wanderSpeed / len, 1f);
                u = 0.5f - 0.5f * Mathf.Cos(phase * Mathf.PI); // slows into each turn
            }
            else
            {
                u = Mathf.Clamp01((now - _passStart) / _passDuration);
                if (!_passForward) u = 1f - u;
            }
            transform.localPosition = _home + Vector3.Lerp(wanderFrom, wanderTo, u);
        }

        void ApplyLoopVolume()
        {
            if (_loop == null) return;
            float edge = 1f;
            if (wander == WanderMode.Pass)
            {
                // a pass swells in from far and fades out at its far end, on top of the stretch gate
                float u = Mathf.Clamp01((Time.time - _passStart) / _passDuration);
                edge = Mathf.Clamp01(Mathf.Min(u, 1f - u) * 6f);
            }
            _loop.volume = volume * loopVolume * _gate * edge * VhAmbienceMix.Level;
        }

        void PlayShot()
        {
            AudioClip c = oneShots[Random32(0, oneShots.Length - 1)];
            if (c == null) return;
            AudioSource s = _useA ? _shotA : _shotB;
            _useA = !_useA;
            s.clip = c;
            s.pitch = Mathf.Lerp(pitchRange.x, pitchRange.y, Rand01());
            s.volume = volume * oneShotVolume * (1f - volumeJitter * Rand01()) * _gate * VhAmbienceMix.Level;
            s.Play();
        }

        float Rand01()
        {
            uint x = _rng;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _rng = x;
            return (x & 0xFFFFFFu) / 16777216f;
        }

        float Range(Vector2 r)
        {
            return Mathf.Lerp(r.x, r.y, Rand01());
        }

        int Random32(int minInclusive, int maxInclusive)
        {
            if (maxInclusive <= minInclusive) return minInclusive;
            int v = minInclusive + (int)(Rand01() * (maxInclusive - minInclusive + 1));
            return v > maxInclusive ? maxInclusive : v;
        }

        void OnDrawGizmosSelected()
        {
            Vector3 p = transform.position;
            Gizmos.color = new Color(1f, 0.8f, 0.3f, 0.8f);
            Gizmos.DrawWireSphere(p, minDistance);
            Gizmos.color = new Color(1f, 0.8f, 0.3f, 0.15f);
            Gizmos.DrawWireSphere(p, maxDistance);
            if (wander != WanderMode.None)
            {
                Transform t = transform.parent;
                Vector3 home = Application.isPlaying ? _home : transform.localPosition;
                Vector3 a = home + wanderFrom;
                Vector3 b = home + wanderTo;
                if (t != null) { a = t.TransformPoint(a); b = t.TransformPoint(b); }
                Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.9f);
                Gizmos.DrawLine(a, b);
            }
        }
    }
}
