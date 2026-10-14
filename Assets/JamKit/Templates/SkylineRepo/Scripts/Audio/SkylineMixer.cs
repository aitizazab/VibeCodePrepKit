using UnityEngine;
using UnityEngine.SceneManagement;

namespace JamKit.SkylineRepo
{
    /// <summary>
    /// Runs <see cref="SkylineSounds"/>' mix every frame, on the "SkylineSounds" object. It is created by itself in every
    /// Skyline scene (a scene whose name starts with "Skyline", right after it loads), so no scene or player code has to
    /// hook it: it puts the master limiter (<see cref="SkylineMasterLimiter"/>) on the active AudioListener, a speech tap
    /// (<see cref="SkylineSpeechTap"/>) on the arena's teaching-line voice (and sets that voice to
    /// <see cref="SkylineMix.VoiceGain"/>), takes the bots' idle loops onto the ambience bus, steps the ducking and the
    /// voices (Update), and plays the frame's hit sounds, one a shot (LateUpdate). When the scene ends it logs
    /// <c>SKYLINE_MIX ...</c> (voices, steals, dropped hit sounds, time ducked).
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class SkylineMixer : MonoBehaviour
    {
        const float ListenerCheckSeconds = 0.5f, VoiceCheckSeconds = 1f, LoopCheckSeconds = 2f;

        AudioListener _listener;
        AudioSource _lineVoice;
        float _nextListener, _nextVoice, _nextLoops;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.IsValid() && scene.name.StartsWith("Skyline", System.StringComparison.Ordinal)) SkylineSounds.Ensure();
        }

        void Update()
        {
            float dt = Time.captureDeltaTime > 0f ? Time.captureDeltaTime : Time.unscaledDeltaTime;
            dt = Mathf.Clamp(dt, 0f, 0.1f);
            float now = Time.unscaledTime;

            if (now >= _nextListener || (_listener != null && !_listener.isActiveAndEnabled))
            {
                _nextListener = now + ListenerCheckSeconds;
                AttachLimiter();
            }
            if (!SkylineSounds.HasSpeechTap && now >= _nextVoice)
            {
                _nextVoice = now + VoiceCheckSeconds;
                FindVoice();
            }
            if (now >= _nextLoops)
            {
                _nextLoops = now + LoopCheckSeconds;
                SkylineSounds.AdoptLoops(FindObjectsByType<AudioSource>(FindObjectsSortMode.None));
            }

            // The player's options (the pause menu's Audio page): the master volume on the listener, and the teaching-line
            // voice at its level times the Voices slider (the barks take the slider where they play, SkylineBarks).
            SkylineSettings.ApplyMaster();
            if (_lineVoice != null)
            {
                float v = SkylineMix.VoiceGain * SkylineMix.UserGain(SkylineSoundCategory.Voices);
                if (!Mathf.Approximately(_lineVoice.volume, v)) _lineVoice.volume = v;
            }

            SkylineSounds.Tick(dt, _listener != null ? _listener.transform.position : Vector3.zero);
        }

        void LateUpdate()
        {
            SkylineSounds.FlushPending();
        }

        void AttachLimiter()
        {
            if (_listener == null || !_listener.isActiveAndEnabled)
            {
                _listener = null;
                foreach (var l in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                    if (l != null && l.isActiveAndEnabled) { _listener = l; break; }
            }
            if (_listener != null && _listener.GetComponent<SkylineMasterLimiter>() == null)
                _listener.gameObject.AddComponent<SkylineMasterLimiter>();
        }

        void FindVoice()
        {
            var director = FindAnyObjectByType<SkylineArenaDirector>();
            if (director == null || director.voice == null) return;
            var go = director.voice.gameObject;
            if (go.GetComponent<AudioListener>() != null) return;   // a tap there would hear the whole mix
            _lineVoice = director.voice;
            director.voice.volume = SkylineMix.VoiceGain * SkylineMix.UserGain(SkylineSoundCategory.Voices);
            var tap = go.GetComponent<SkylineSpeechTap>();
            if (tap == null) tap = go.AddComponent<SkylineSpeechTap>();
            SkylineSounds.SetSpeechTap(tap);
        }

        void OnDestroy()
        {
            if (SkylineSounds.Started > 0) { Debug.Log(SkylineSounds.Report()); Debug.Log(SkylineSounds.ReportSounds()); }
        }
    }
}
