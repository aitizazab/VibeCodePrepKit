using JamKit.Movement;
using UnityEngine;

namespace JamKit.DreamEscape
{
    /// <summary>
    /// The chase's music: the speed music (<see cref="MovementMusic"/>) with the static's tension on top. Two things
    /// drive it, each with its own job:
    /// <list type="bullet">
    /// <item><b>Speed</b> (the player's MovementAudio): which stems play. The pad (base) always; the pulse (drive)
    /// from the fast tier; the bright arpeggio (peak) while flying. Switched on bar lines; muffled when slow, open at
    /// flying.</item>
    /// <item><b>The gap</b> (<see cref="SetThreat"/>: 0 far, 1 about to catch): the tension. The pulse is pushed in
    /// as the static closes even when you're slow; the music box (the tension stem, never speed's) gets louder and
    /// slides out of tune; the music opens partway.</item>
    /// </list>
    /// PLACEHOLDER stems: the pad, pulse and music box come from <see cref="DreamSynth"/>, the peak from
    /// <see cref="MoveMusicSynth"/> (same key, tempo and length). Real stems dropped into
    /// Resources/JamKitMusicStems/dream/ (base, drive, peak, tension) replace them. Follows the music volume and mute.
    /// </summary>
    public sealed class DreamChaseMusic : MonoBehaviour
    {
        public const string StemSet = "dream";

        /// <summary>The full-stem sources: pad = base, pulse = drive, box = tension (kept from the first chase music).</summary>
        public AudioSource pad, pulse, box;
        public MovementMusic music;

        float _threat, _target;

        public float Threat => _threat;
        public bool Playing => music != null && music.Playing;
        /// <summary>The tension's floor under the pulse (0..1), for tests.</summary>
        public float PulseLevel => _threat;

        void Awake()
        {
            if (pad == null) pad = MakeSource("Pad");
            if (pulse == null) pulse = MakeSource("Pulse");
            if (box == null) box = MakeSource("Music box");
            if (music == null) music = GetComponent<MovementMusic>();
            if (music == null) music = gameObject.AddComponent<MovementMusic>();
            music.playOnStart = false;
            if (string.IsNullOrEmpty(music.stemSet)) music.stemSet = StemSet;
            music.stemSources = new[] { pad, pulse, null, box };
        }

        AudioSource MakeSource(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.AddComponent<AudioSource>();
        }

        /// <summary>Starts the stems together (a fresh start fades in over a second).</summary>
        public void Begin()
        {
            if (!music.Playing)
                music.SetPlaceholders(DreamSynth.Pad, DreamSynth.Pulse, null, DreamSynth.MusicBox);
            LinkPlayer();
            music.Begin();
        }

        /// <summary>Fades everything out (the escape is over).</summary>
        public void End() => music.End();

        public void StopNow() => music.StopNow();

        public void SetThreat(float threat) => _target = Mathf.Clamp01(threat);

        /// <summary>Older scenes: point the player's sound at this music if nothing else drives it.</summary>
        void LinkPlayer()
        {
            var sound = FindAnyObjectByType<MovementAudio>();
            if (sound != null && sound.music == null) sound.music = music;
        }

        void Update()
        {
            if (!music.Playing) return;
            float dt = Time.unscaledDeltaTime;
            _threat = Mathf.MoveTowards(_threat, _target, (_target > _threat ? 1.6f : 0.5f) * dt);
            // The gap's tension, on top of whatever speed has switched on.
            music.SetFloor(StemMixer.Drive, _threat);
            music.SetFloor(StemMixer.Tension, 0.5f + 0.5f * _threat);
            music.SetOpenFloor(0.6f * _threat);
            music.SetPitch(StemMixer.Tension, 1f - 0.035f * _threat * _threat + 0.006f * Mathf.Sin(Time.unscaledTime * 5.3f) * _threat);
            // A detuned music box drifts off the beat; once the static has fallen back, put it back in step.
            if (_threat < 0.05f) music.Resync(StemMixer.Tension);
        }
    }
}
