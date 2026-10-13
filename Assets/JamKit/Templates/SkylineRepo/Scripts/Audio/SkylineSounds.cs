using System.Collections.Generic;
using UnityEngine;

namespace JamKit.SkylineRepo
{
    /// <summary>Skyline's sounds by name ("writ/mag_out"): clips from Resources/SkylineAudio/&lt;sound&gt;, played on a small
    /// pool of AudioSources (2D for the viewmodel, 3D in the world). A missing clip logs <c>SKYLINE_SOUND missing &lt;sound&gt;</c>
    /// once and plays nothing.</summary>
    public static class SkylineSounds
    {
        const string Folder = "SkylineAudio/";
        const int ViewmodelVoices = 12, WorldVoices = 16;
        const float WorldMinDistance = 2f, WorldMaxDistance = 60f;

        static readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        static readonly HashSet<string> _missing = new HashSet<string>();
        static GameObject _host;
        static AudioSource[] _view, _world;
        static int _nextView, _nextWorld;

        /// <summary>The clip for <paramref name="sound"/>, or null (logged once).</summary>
        public static AudioClip Get(string sound)
        {
            if (string.IsNullOrEmpty(sound)) return null;
            if (_clips.TryGetValue(sound, out var c) && c != null) return c;
            if (_missing.Contains(sound)) return null;   // a known miss: no Resources.Load per request
            c = Resources.Load<AudioClip>(Folder + sound);
            if (c == null)
            {
                if (_missing.Add(sound)) Debug.Log("SKYLINE_SOUND missing " + sound);
                return null;
            }
            _clips[sound] = c;
            return c;
        }

        /// <summary>Plays a first-person sound (the guns' handling events): 2D, no distance.</summary>
        public static void PlayViewmodel(string sound, float volume = 1f)
        {
            var clip = Get(sound);
            if (clip == null) return;
            Ensure();
            var src = _view[_nextView];
            _nextView = (_nextView + 1) % _view.Length;
            src.PlayOneShot(clip, volume);
        }

        /// <summary>Plays a sound at a point in the world (3D).</summary>
        public static void PlayAt(string sound, Vector3 position, float volume = 1f)
        {
            var clip = Get(sound);
            if (clip == null) return;
            Ensure();
            var src = _world[_nextWorld];
            _nextWorld = (_nextWorld + 1) % _world.Length;
            src.transform.position = position;
            src.PlayOneShot(clip, volume);
        }

        static void Ensure()
        {
            if (_host != null) return;      // Unity's null: also rebuilt after a scene change destroyed it
            _host = new GameObject("SkylineSounds");
            _view = new AudioSource[ViewmodelVoices];
            for (int i = 0; i < _view.Length; i++)
            {
                var s = _host.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0f;
                _view[i] = s;
            }
            _world = new AudioSource[WorldVoices];
            for (int i = 0; i < _world.Length; i++)
            {
                var go = new GameObject("World voice " + i);
                go.transform.SetParent(_host.transform, false);
                var s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 1f;
                s.rolloffMode = AudioRolloffMode.Linear;
                s.minDistance = WorldMinDistance;
                s.maxDistance = WorldMaxDistance;
                _world[i] = s;
            }
            _nextView = 0;
            _nextWorld = 0;
        }
    }
}
