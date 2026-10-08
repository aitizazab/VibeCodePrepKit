using System.Collections.Generic;
using JamKit.Flow;
using UnityEngine;

namespace JamKit.Shell
{
    /// <summary>
    /// Music with crossfades (two sources) and pooled one-shot sound effects with a little random
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

        /// <summary>The id of the music playing (or fading in) now; empty when silent.</summary>
        public string CurrentMusic => _musicId;
        /// <summary>The last sound effect id played, for tests and debugging.</summary>
        public string LastSfx;

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
            for (int i = 0; i < PoolSize; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.ignoreListenerPause = true;
                _pool.Add(src);
            }
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
            src.volume = Mathf.Clamp01(volume * (entry != null ? entry.volume : 1f) * _settings.sfxVolume);
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

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
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
        }

        bool Resolve(string id, out AudioClip clip, out AudioRegistry.Entry entry)
        {
            entry = _registry != null ? _registry.Find(id) : null;
            clip = entry != null && entry.clip != null ? entry.clip : ProceduralAudio.Get(id);
            if (clip != null) return true;
            if (!string.IsNullOrEmpty(id) && _warned.Add(id))
                Debug.LogWarning($"JamKit: no sound with id \"{id}\". Add it to the Audio Registry (Assets/JamKit/Audio).");
            return false;
        }
    }
}
