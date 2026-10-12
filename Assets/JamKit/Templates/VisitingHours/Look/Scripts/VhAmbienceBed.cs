using UnityEngine;
using UnityEngine.Audio;

namespace JamKit.Look
{
    /// <summary>
    /// The 2D ambience bed (the hill's golden-hour Saturday air: far birds, far suburban hum, the odd far car). A looping
    /// stereo clip, not positional, that fades in on start from a random point in the loop. Its level is
    /// <see cref="volume"/> x <see cref="VhAmbienceMix.Level"/>; <see cref="FadeTo"/> fades it on its own.
    /// Built by <c>VhLookAmbience.AddBed</c>. No per-frame allocation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VhAmbienceBed : MonoBehaviour
    {
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 0.8f;
        [Tooltip("Seconds to fade in when the scene starts.")]
        public float fadeIn = 4f;
        [Tooltip("Start somewhere random in the loop, so a restart doesn't repeat the same first seconds.")]
        public bool randomStart = true;
        public AudioMixerGroup output;
        [Tooltip("0..256, lower is more important; ambience yields voices to gameplay sounds.")]
        public int priority = 64;

        AudioSource _source;
        float _gain;
        float _fadeFrom;
        float _fadeTo = 1f;
        float _fadeStart;
        float _fadeDuration;

        /// <summary>The AudioSource this bed plays through (made in Awake).</summary>
        public AudioSource Source { get { return _source; } }

        void Awake()
        {
            _source = gameObject.AddComponent<AudioSource>();
            _source.hideFlags = HideFlags.DontSave;
            _source.playOnAwake = false;
            _source.loop = true;
            _source.spatialBlend = 0f;
            _source.dopplerLevel = 0f;
            _source.priority = priority;
            _source.outputAudioMixerGroup = output;
            _source.clip = clip;
            _source.volume = 0f;
        }

        void OnEnable()
        {
            if (_source == null || clip == null) return;
            _source.clip = clip;
            _source.Play();
            if (randomStart && clip.samples > 0) _source.timeSamples = Random.Range(0, clip.samples);
            _fadeFrom = 0f;
            _fadeTo = 1f;
            _fadeStart = Time.time;
            _fadeDuration = Mathf.Max(0.01f, fadeIn);
            Apply();
        }

        void OnDisable()
        {
            if (_source != null) _source.Stop();
        }

        /// <summary>Fade this bed (on top of <see cref="volume"/>) to <paramref name="level"/> (0..1) over <paramref name="seconds"/>.</summary>
        public void FadeTo(float level, float seconds)
        {
            _fadeFrom = _gain;
            _fadeTo = Mathf.Clamp01(level);
            _fadeStart = Time.time;
            _fadeDuration = Mathf.Max(0.01f, seconds);
        }

        void Update()
        {
            Apply();
        }

        void Apply()
        {
            if (_source == null) return;
            float u = Mathf.Clamp01((Time.time - _fadeStart) / _fadeDuration);
            _gain = Mathf.Lerp(_fadeFrom, _fadeTo, u * u * (3f - 2f * u));
            _source.volume = volume * _gain * VhAmbienceMix.Level;
        }
    }
}
