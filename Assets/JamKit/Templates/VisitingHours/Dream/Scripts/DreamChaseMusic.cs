using JamKit.Shell;
using UnityEngine;

namespace JamKit.DreamEscape
{
    /// <summary>
    /// The chase's PLACEHOLDER music, tied to the gap: three synced loops from <see cref="DreamSynth"/>. The pad
    /// plays throughout (its low-pass opens as the static nears), the pulse grows from a murmur to full as the
    /// gap closes, and the music box gets louder and slides out of tune when the static is right behind you.
    /// <see cref="SetThreat"/> takes the pursuer's threat (0 far, 1 about to catch). Follows the shell's music
    /// volume and mute.
    /// </summary>
    public sealed class DreamChaseMusic : MonoBehaviour
    {
        public AudioSource pad, pulse, box;
        [Range(0f, 1f)] public float volume = 0.8f;

        float _threat, _target, _master, _masterTarget;
        bool _playing;
        AudioLowPassFilter _padFilter;

        public float Threat => _threat;
        public bool Playing => _playing;
        /// <summary>The pulse layer's current volume (0..1 before the master), for tests.</summary>
        public float PulseLevel => 0.12f + 0.88f * _threat;

        void Awake()
        {
            if (pad == null) pad = MakeSource("Pad");
            if (pulse == null) pulse = MakeSource("Pulse");
            if (box == null) box = MakeSource("Music box");
            _padFilter = pad.GetComponent<AudioLowPassFilter>();
            if (_padFilter == null) _padFilter = pad.gameObject.AddComponent<AudioLowPassFilter>();
        }

        AudioSource MakeSource(string name)
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
            s.volume = 0f;
        }

        /// <summary>Starts the loops together (a fresh start fades in over a second).</summary>
        public void Begin()
        {
            _masterTarget = 1f;
            if (_playing) return;
            Prepare(pad, DreamSynth.Pad);
            Prepare(pulse, DreamSynth.Pulse);
            Prepare(box, DreamSynth.MusicBox);
            double at = AudioSettings.dspTime + 0.1;
            pad.PlayScheduled(at);
            pulse.PlayScheduled(at);
            box.PlayScheduled(at);
            _playing = true;
            _master = 0f;
        }

        /// <summary>Fades everything out (the escape is over).</summary>
        public void End() => _masterTarget = 0f;

        public void StopNow()
        {
            _masterTarget = 0f;
            _master = 0f;
            if (!_playing) return;
            pad.Stop(); pulse.Stop(); box.Stop();
            _playing = false;
        }

        public void SetThreat(float threat) => _target = Mathf.Clamp01(threat);

        void Update()
        {
            if (!_playing) return;
            float dt = Time.unscaledDeltaTime;
            _threat = Mathf.MoveTowards(_threat, _target, (_target > _threat ? 1.6f : 0.5f) * dt);
            _master = Mathf.MoveTowards(_master, _masterTarget, (_masterTarget > _master ? 1f : 0.4f) * dt);
            if (_master <= 0f && _masterTarget <= 0f) { StopNow(); return; }
            float music = GameRoot.Exists ? GameRoot.Settings.musicVolume : 0.7f;
            bool muted = GameRoot.Exists && GameRoot.Audio.Muted;
            float m = volume * music * _master;
            pad.volume = m * (0.55f - 0.15f * _threat);
            pulse.volume = m * 0.75f * PulseLevel;
            box.volume = m * (0.35f + 0.4f * _threat);
            box.pitch = 1f - 0.035f * _threat * _threat + 0.006f * Mathf.Sin(Time.unscaledTime * 5.3f) * _threat;
            if (_padFilter != null) _padFilter.cutoffFrequency = Mathf.Lerp(900f, 6000f, _threat);
            // A detuned music box drifts off the beat; once the static has fallen back, put it back in step.
            if (_threat < 0.05f && pad.isPlaying && box.isPlaying && Mathf.Abs(box.timeSamples - pad.timeSamples) > DreamSynth.Rate / 40)
                box.timeSamples = pad.timeSamples;
            pad.mute = pulse.mute = box.mute = muted;
        }
    }
}
