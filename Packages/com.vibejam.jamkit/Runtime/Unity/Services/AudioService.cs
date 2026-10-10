using System.Collections.Generic;
using JamKit.Flow;
using UnityEngine;

namespace JamKit.Shell
{
    /// <summary>
    /// Music with crossfades (two sources), a looping ambience bed (rain, hum: <c>PlayAmbience("amb_rain")</c>)
    /// and pooled one-shot sound effects with a little random
    /// pitch, all by id: <c>GameRoot.Audio.Play("coin")</c>, <c>GameRoot.Audio.PlayMusic("music_game")</c>.
    /// Clips come from the Audio Registry, else from <see cref="ProceduralAudio"/>. Fades run on real
    /// time, so they work while the game is paused.
    /// </summary>
    public sealed class AudioService : MonoBehaviour
    {
        const int PoolSize = 16;

        AudioRegistry _registry;
        Settings _settings = new Settings();
        readonly AudioSource[] _music = new AudioSource[2];
        readonly float[] _musicEntryVolume = { 1f, 1f };
        readonly float[] _fade = { 0f, 0f };
        readonly float[] _fadeTarget = { 0f, 0f };
        int _activeMusic;
        float _fadeSpeed = 1f;
        string _musicId = "";
        readonly List<AudioSource> _pool = new List<AudioSource>(PoolSize);
        int _next;
        readonly HashSet<string> _warned = new HashSet<string>();
        AudioSource _ambience;
        float _ambienceFade, _ambienceTarget, _ambienceSpeed = 1f, _ambienceEntryVolume = 1f;
        string _ambienceId = "";

        /// <summary>The ambience id playing (or fading in) now; empty when silent.</summary>
        public string CurrentAmbience => _ambienceId;

        /// <summary>The id of the music playing (or fading in) now; empty when silent.</summary>
        public string CurrentMusic => _musicId;
        /// <summary>The last sound effect id played, for tests and debugging.</summary>
        public string LastSfx;

        /// <summary>A gain on the world: the ambience bed and new sound effects (not the music). 1 = untouched. The
        /// movement's tunnel focus lowers it while flying (MovementAudio, docs/MOVEMENT.md "Speed music").</summary>
        public float WorldGain = 1f;

        bool _muted;

        /// <summary>Every sound plays silently (on in batch mode: test runs and the smoke run stay quiet).
        /// Volumes and the Settings screen work as usual.</summary>
        public bool Muted
        {
            get => _muted;
            set
            {
                _muted = value;
                foreach (var m in _music) if (m != null) m.mute = value;
                if (_ambience != null) _ambience.mute = value;
                foreach (var p in _pool) if (p != null) p.mute = value;
            }
        }

        public void Init(AudioRegistry registry)
        {
            _registry = registry;
            for (int i = 0; i < 2; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.loop = true;
                src.ignoreListenerPause = true;
                src.volume = 0f;
                _music[i] = src;
            }
            _ambience = gameObject.AddComponent<AudioSource>();
            _ambience.playOnAwake = false;
            _ambience.loop = true;
            _ambience.ignoreListenerPause = true;
            _ambience.volume = 0f;
            for (int i = 0; i < PoolSize; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.ignoreListenerPause = true;
                _pool.Add(src);
            }
            // Batch mode (Unity test runs, the -jamSmoke player) still plays audio: keep it silent.
            Muted = Application.isBatchMode;
        }

        public void ApplySettings(Settings s)
        {
            _settings = s ?? new Settings();
            AudioListener.volume = Mathf.Clamp01(_settings.masterVolume);
            UpdateMusicVolumes();
        }

        /// <summary>Plays a sound effect. Missing ids warn once and stay silent.</summary>
        public void Play(string id, float volume = 1f)
        {
            if (!Resolve(id, out var clip, out var entry)) return;
            var src = _pool[_next];
            _next = (_next + 1) % _pool.Count;
            float variance = entry != null ? entry.pitchVariance : 0.05f;
            src.pitch = 1f + Random.Range(-variance, variance);
            src.volume = Mathf.Clamp01(volume * (entry != null ? entry.volume : 1f) * _settings.sfxVolume * WorldGain);
            src.clip = clip;
            src.Play();
            LastSfx = id;
        }

        /// <summary>Crossfades to the given music. Same id = keeps playing. Empty or "stop" fades out.</summary>
        public void PlayMusic(string id, float fadeSeconds = 1f)
        {
            if (string.IsNullOrEmpty(id) || id == "stop" || id == "none") { StopMusic(fadeSeconds); return; }
            if (id == _musicId) return;
            if (!Resolve(id, out var clip, out var entry)) return;
            _musicId = id;
            _fadeSpeed = fadeSeconds > 0.01f ? 1f / fadeSeconds : 1000f;
            _fadeTarget[_activeMusic] = 0f;
            _activeMusic = 1 - _activeMusic;
            var src = _music[_activeMusic];
            src.clip = clip;
            src.loop = entry == null || entry.loop;
            _musicEntryVolume[_activeMusic] = entry != null ? entry.volume : 1f;
            _fade[_activeMusic] = 0f;
            _fadeTarget[_activeMusic] = 1f;
            src.Play();
            UpdateMusicVolumes();
        }

        public void StopMusic(float fadeSeconds = 1f)
        {
            _musicId = "";
            _fadeSpeed = fadeSeconds > 0.01f ? 1f / fadeSeconds : 1000f;
            _fadeTarget[0] = 0f;
            _fadeTarget[1] = 0f;
        }

        /// <summary>Fades a looping background sound in (replacing the one playing). Empty or "stop" fades it out.
        /// Plays at the sound-effects volume.</summary>
        public void PlayAmbience(string id, float fadeSeconds = 1.5f)
        {
            if (string.IsNullOrEmpty(id) || id == "stop" || id == "none") { StopAmbience(fadeSeconds); return; }
            if (id == _ambienceId) return;
            if (!Resolve(id, out var clip, out var entry)) return;
            _ambienceId = id;
            _ambienceSpeed = fadeSeconds > 0.01f ? 1f / fadeSeconds : 1000f;
            _ambience.clip = clip;
            _ambience.loop = true;
            _ambienceEntryVolume = entry != null ? entry.volume : 1f;
            _ambienceFade = 0f;
            _ambienceTarget = 1f;
            _ambience.Play();
            UpdateMusicVolumes();
        }

        public void StopAmbience(float fadeSeconds = 1.5f)
        {
            _ambienceId = "";
            _ambienceSpeed = fadeSeconds > 0.01f ? 1f / fadeSeconds : 1000f;
            _ambienceTarget = 0f;
        }

        /// <summary>The clip an id plays (the registry's, else the generated placeholder), or null.</summary>
        public AudioClip ClipFor(string id) => Resolve(id, out var clip, out _, false) ? clip : null;

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (_ambience != null)
            {
                _ambienceFade = Mathf.MoveTowards(_ambienceFade, _ambienceTarget, _ambienceSpeed * dt);
                if (_ambienceFade <= 0f && _ambienceTarget <= 0f && _ambience.isPlaying) _ambience.Stop();
            }
            for (int i = 0; i < 2; i++)
            {
                _fade[i] = Mathf.MoveTowards(_fade[i], _fadeTarget[i], _fadeSpeed * dt);
                if (_fade[i] <= 0f && _fadeTarget[i] <= 0f && _music[i].isPlaying) _music[i].Stop();
            }
            UpdateMusicVolumes();
        }

        void UpdateMusicVolumes()
        {
            for (int i = 0; i < 2; i++)
                if (_music[i] != null) _music[i].volume = _fade[i] * _musicEntryVolume[i] * _settings.musicVolume;
            if (_ambience != null) _ambience.volume = _ambienceFade * _ambienceEntryVolume * _settings.sfxVolume * WorldGain;
        }

        bool Resolve(string id, out AudioClip clip, out AudioRegistry.Entry entry, bool warn = true)
        {
            entry = _registry != null ? _registry.Find(id) : null;
            clip = entry != null && entry.clip != null ? entry.clip : ProceduralAudio.Get(id);
            if (clip != null) return true;
            if (warn && !string.IsNullOrEmpty(id) && _warned.Add(id))
                Debug.LogWarning($"JamKit: no sound with id \"{id}\". Add it to the Audio Registry (Assets/JamKit/Audio).");
            return false;
        }
    }
}
