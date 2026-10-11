using System;
using System.Collections.Generic;
using JamKit.Flow;
using JamKit.Shell;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace JamKit.VisitingHours
{
    /// <summary>
    /// Plays the opening (docs/OPENING-AND-TITLE.md) on the 3D sets of a <see cref="VhStage"/>: the cue
    /// list from the boot asset, resolved against the real recordings (<see cref="OpeningScript"/>), fired
    /// in order by the core <see cref="OpeningTimeline"/>. A thin UI layer over the picture carries the
    /// black, the dark under the blanket, the subtitles (when on) and the "Hold Esc to skip" hint.
    /// Holding Escape (or B) skips to the title. Re-time mode (<see cref="RetimeMode"/>): tap Space at each
    /// of the voicemail's markers while it plays; the taps come out through <see cref="Retimed"/>.
    /// </summary>
    public sealed class VhOpening
    {
        /// <summary>Re-time the voicemail by tapping Space at each marker (JamKit > Visiting Hours > Re-time the Voicemail).</summary>
        public static bool RetimeMode;
        /// <summary>The taps (seconds into the voicemail, one per marker) and the recording's length, when a re-time completes.</summary>
        public static event Action<List<float>, float> Retimed;

        readonly VisitingHoursBoot _boot;
        readonly VhStage _stage;
        readonly TitleScreen _title;
        OpeningTimeline _timeline;
        readonly List<OpeningCue> _resolved;
        readonly List<OpeningCue> _fired = new List<OpeningCue>(16);
        readonly VisualElement _root, _black, _blanket;
        readonly Label _subtitle, _skipHint, _retime, _editHint;
        readonly VoiceTrack _voicemail;
        readonly List<float> _taps = new List<float>();
        float _black01 = 1f, _blackTarget = 1f, _blackSpeed = 1f;
        float _blanket01, _blanketTarget, _blanketSpeed = 1f;
        float _holdSkip, _vmStart = -1f, _speakerMix, _speakerTarget, _speakerSpeed = 4f;
        float _voiceLevel = 1f, _voiceLevelTarget = 1f, _voiceLevelSpeed = 1f;
        bool _running, _done, _musicStarted, _audible, _retimeReported;

        /// <summary>Time multiplier (tests and the smoke run play it fast; voices are silent above 1.5x).</summary>
        public float speed = 1f;
        /// <summary>The opening editor holds it still (no cues fire, the voices pause).</summary>
        public bool Paused;
        /// <summary>The opening editor lightens the black (and the dark under the blanket) so the set shows at dark moments (1 = as played).</summary>
        public float blackScale = 1f;
        bool _seeking;
        public event Action Finished;
        public bool Running => _running && !_done;
        public float Time => _timeline.Time;
        public float Length => _timeline.Length;
        public OpeningTimeline Timeline => _timeline;
        public string Subtitle => _subtitle.text;
        public float Black => _black01;
        public float UnderBlanket => _blanket01;
        public bool SpeakerOn => _speakerTarget > 0.5f;
        /// <summary>How loud the voicemail is now (0..1 of its volume): it fades as she drifts off.</summary>
        public float VoiceLevel => _voiceLevel;
        public bool Skipped;
        /// <summary>Cue kinds seen so far, in order (tests).</summary>
        public readonly List<OpeningCueKind> Seen = new List<OpeningCueKind>();
        public readonly List<string> Notes = new List<string>();

        public VhOpening(VisitingHoursBoot boot, VhStage stage, TitleScreen title)
        {
            _boot = boot;
            _stage = stage;
            _title = title;
            _resolved = boot.Resolve(Notes);
            _timeline = new OpeningTimeline(_resolved);
            foreach (var n in Notes) Debug.Log("JAMKIT_VH opening: " + n);
            _voicemail = boot.Track("voicemail");

            _root = new VisualElement { name = "vh-opening", pickingMode = PickingMode.Ignore };
            _root.AddToClassList("vh-opening");
            if (boot.style != null) _root.styleSheets.Add(boot.style);
            _blanket = Add("vh-opening__blanket");
            _subtitle = new Label("") { name = "vh-opening-subtitle", pickingMode = PickingMode.Ignore };
            _subtitle.AddToClassList("vh-opening__subtitle");
            _root.Add(_subtitle);
            _black = Add("vh-opening__black");
            // The first seconds: the editor is there (F9).
            _editHint = new Label("F9: edit this scene") { pickingMode = PickingMode.Ignore };
            _editHint.style.position = Position.Absolute;
            _editHint.style.left = 16;
            _editHint.style.bottom = 14;
            _editHint.style.fontSize = 14;
            _editHint.style.color = new Color(1f, 1f, 1f, 0.75f);
            _editHint.style.opacity = 0f;
            _root.Add(_editHint);
            _skipHint = new Label("Hold Esc to skip") { pickingMode = PickingMode.Ignore };
            _skipHint.AddToClassList("vh-opening__skip");
            _skipHint.style.opacity = 0f;
            _root.Add(_skipHint);
            _retime = new Label("") { pickingMode = PickingMode.Ignore };
            _retime.AddToClassList("vh-opening__retime");
            _retime.style.display = RetimeMode ? DisplayStyle.Flex : DisplayStyle.None;
            _root.Add(_retime);

            // Over the title screen's layer, under any overlay (Settings, the presenter panel).
            var layer = title.Root.parent;
            if (layer != null) layer.Insert(layer.IndexOf(title.Root) + 1, _root);
            else if (GameRoot.Exists) GameRoot.Ui.Root.Add(_root);
        }

        VisualElement Add(string cls)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            e.AddToClassList(cls);
            _root.Add(e);
            return e;
        }

        public void Start()
        {
            _running = true;
            _audible = speed <= 1.5f;
            _title.Root.SetEnabled(false);
            if (GameRoot.Exists) GameRoot.Audio.StopMusic(0.05f);
            _stage.ResetForOpening();
            Apply();
        }

        static float Volume(float v) => v * (GameRoot.Exists ? GameRoot.Settings.sfxVolume : 1f);

        static bool Muted => GameRoot.Exists && GameRoot.Audio.Muted;

        public void Tick(float udt)
        {
            if (!Running) return;
            if (Paused)
            {
                Apply();
                return;
            }
            if (_stage == null)
            {
                Finish(false);
                return;
            }
            // The shell starts the title music on the title; the opening keeps silence until its bloom.
            if (!_musicStarted && GameRoot.Exists && !string.IsNullOrEmpty(GameRoot.Audio.CurrentMusic)) GameRoot.Audio.StopMusic(0.05f);

            // Hold Escape (or B) to skip; the hint shows while it's held.
            var kb = Keyboard.current;
            var input = GameRoot.Exists ? GameRoot.Input : null;
            bool pad = input != null && (input.Synthetic != null ? input.Synthetic.IsHeld(input.Cancel) : input.Cancel.IsPressed());
            // (Not while the opening editor plays it: there Escape cancels and deselects.)
            bool editing = VhOpeningEditor.Instance != null && VhOpeningEditor.Instance.IsOpen;
            bool hold = !editing && _boot.holdToSkipSeconds > 0f && ((kb != null && kb.escapeKey.isPressed) || pad);
            _holdSkip = hold ? _holdSkip + udt : 0f;
            _skipHint.style.opacity = hold ? Mathf.Clamp01(0.4f + _holdSkip / Mathf.Max(0.01f, _boot.holdToSkipSeconds)) : Mathf.Max(0f, _skipHint.resolvedStyle.opacity - udt);
            if (_boot.holdToSkipSeconds > 0f && _holdSkip >= _boot.holdToSkipSeconds)
            {
                Finish(true);
                return;
            }

            float dt = udt * Mathf.Max(0.01f, speed);
            _fired.Clear();
            _timeline.Advance(dt, _fired);
            for (int i = 0; i < _fired.Count && Running; i++) Do(_fired[i]);
            if (!Running) return;

            _black01 = Mathf.MoveTowards(_black01, _blackTarget, dt * _blackSpeed);
            _blanket01 = Mathf.MoveTowards(_blanket01, _blanketTarget, dt * _blanketSpeed);
            if (_vmStart >= 0f)
            {
                float t = _timeline.Time - _vmStart;
                _stage.SetPlayback(t + (_voicemail != null ? _voicemail.trimStart : 0f));
                _speakerMix = Mathf.MoveTowards(_speakerMix, _speakerTarget, udt * _speakerSpeed);
                _voiceLevel = Mathf.MoveTowards(_voiceLevel, _voiceLevelTarget, dt * _voiceLevelSpeed);
                float vol = Volume(_boot.voicemailVolume) * _voiceLevel;
                if (_stage.voiceClean != null) _stage.voiceClean.volume = vol * (1f - _speakerMix);
                if (_stage.voicePhone != null) _stage.voicePhone.volume = vol * _boot.speaker.volume * _speakerMix;
                if (RetimeMode) TickRetime(t);
            }
            Apply();
        }

        void TickRetime(float t)
        {
            if (_voicemail == null || _retimeReported) return;
            int next = _taps.Count;
            var kb = Keyboard.current;
            if (kb != null && kb.spaceKey.wasPressedThisFrame && next < _voicemail.markers.Count) _taps.Add(t);
            next = _taps.Count;
            float len = OpeningScript.Playing(_voicemail, _boot.ClipLength);
            if (next < _voicemail.markers.Count)
            {
                var m = _voicemail.markers[next];
                _retime.text = "RE-TIME: tap Space as he starts \"" + (string.IsNullOrEmpty(m.subtitle) ? m.name : m.subtitle) + "\"  (" + (next + 1) + "/" + _voicemail.markers.Count + ")";
            }
            else _retime.text = "RE-TIME: all " + next + " markers tapped. They're saved when the voicemail ends.";
            if (t >= len)
            {
                _retimeReported = true;
                if (_taps.Count == _voicemail.markers.Count)
                {
                    _retime.text = "RE-TIME: saved. Every cue anchored to the voicemail follows the new times.";
                    Retimed?.Invoke(new List<float>(_taps), len);
                }
                else _retime.text = "RE-TIME: " + _taps.Count + " taps for " + _voicemail.markers.Count + " markers; nothing saved. Try again.";
            }
        }

        void Do(OpeningCue c)
        {
            Seen.Add(c.kind);
            // Durations run at the opening's speed (tests and the smoke run play it fast); a seek lands on each cue's end.
            float secs = _seeking ? 0f : Mathf.Max(0f, c.seconds) / Mathf.Max(0.01f, speed);
            switch (c.kind)
            {
                case OpeningCueKind.Fade:
                    _blackTarget = c.text == "out" ? 1f : 0f;
                    if (secs <= 0f) _black01 = _blackTarget;
                    else _blackSpeed = 1f / secs;
                    break;
                case OpeningCueKind.Camera:
                    _stage.MoveCamera(c.text, secs);
                    break;
                case OpeningCueKind.Ring:
                    if (_seeking) break;
                    PlayLoop(_stage.ring, 0.55f);
                    PlayLoop(_stage.vibrate, 0.8f);
                    break;
                case OpeningCueKind.RingStop:
                    if (_stage.ring != null) _stage.ring.Stop();
                    if (_stage.vibrate != null) _stage.vibrate.Stop();
                    break;
                case OpeningCueKind.Phone:
                    _stage.SetPhone(c.text, secs, c.value);
                    break;
                case OpeningCueKind.Arm:
                    _stage.PlayArm(c.text, secs);
                    break;
                case OpeningCueKind.Sfx:
                    if (_audible && !_seeking && GameRoot.Exists) GameRoot.Audio.Play(c.text, c.value);
                    break;
                case OpeningCueKind.Voice:
                    if (_seeking)
                    {
                        if (c.text == "voicemail") _vmStart = c.at;
                        break;
                    }
                    StartVoice(c.text);
                    break;
                case OpeningCueKind.Speaker:
                    _speakerTarget = Mathf.Clamp01(c.value);
                    _speakerSpeed = 1f / Mathf.Max(0.02f, _boot.speaker.blend);
                    // The phone is on speaker either way (its screen says so); value only sets how much we hear it through it.
                    _stage.SetSpeaker(true);
                    break;
                case OpeningCueKind.VoiceLevel:
                    _voiceLevelTarget = Mathf.Clamp01(c.value);
                    if (secs <= 0f) _voiceLevel = _voiceLevelTarget;
                    else _voiceLevelSpeed = Mathf.Abs(_voiceLevelTarget - _voiceLevel) / secs + 1e-4f;
                    break;
                case OpeningCueKind.Breathing:
                    _stage.SetBreathing(c.text != "off");
                    break;
                case OpeningCueKind.Blanket:
                    _blanketTarget = Mathf.Clamp01(c.value);
                    if (secs <= 0f) _blanket01 = _blanketTarget;
                    else _blanketSpeed = 1f / secs;
                    break;
                case OpeningCueKind.Lights:
                    _stage.SetLights(c.value, secs);
                    break;
                case OpeningCueKind.Music:
                    if (_seeking) break;
                    _musicStarted = true;
                    if (GameRoot.Exists)
                    {
                        string id = string.IsNullOrEmpty(c.text) ? GameRoot.Config.titleMusic : c.text;
                        if (!string.IsNullOrEmpty(id)) GameRoot.Audio.PlayMusic(id, Mathf.Max(0.1f, secs));
                    }
                    break;
                case OpeningCueKind.Show:
                    if (!_stage.ShowPiece(c.text, secs)) Debug.Log("JAMKIT_VH opening: no table piece \"" + c.text + "\"");
                    break;
                case OpeningCueKind.Interactive:
                    _stage.SetInteractive(true);
                    _title.Root.SetEnabled(true);
                    break;
                case OpeningCueKind.Subtitle:
                    _subtitle.text = c.text;
                    break;
                case OpeningCueKind.ClearSubtitle:
                    _subtitle.text = "";
                    break;
                case OpeningCueKind.End:
                    if (_seeking) break;
                    Finish(false);
                    break;
            }
        }

        void PlayLoop(AudioSource a, float volume)
        {
            if (a == null || !_audible) return;
            a.mute = Muted;
            a.volume = Volume(volume);
            a.Play();
        }

        void StartVoice(string track)
        {
            var tr = _boot.Track(track);
            var clip = tr != null ? _boot.Clip(tr.clip) : null;
            if (track == "voicemail") _vmStart = _timeline.Time;
            if (clip == null)
            {
                Debug.Log("JAMKIT_VH audio missing: " + (tr != null ? tr.clip : track) + ".wav (the opening runs on its timing, silent)");
                return;
            }
            if (!_audible) return;
            if (track == "voicemail")
            {
                double at = AudioSettings.dspTime + 0.05;
                foreach (var a in new[] { _stage.voiceClean, _stage.voicePhone })
                {
                    if (a == null) continue;
                    a.clip = clip;
                    a.mute = Muted;
                    a.time = Mathf.Clamp(tr.trimStart, 0f, Mathf.Max(0f, clip.length - 0.05f));
                    a.PlayScheduled(at);
                }
                _speakerMix = 0f;
                _speakerTarget = 0f;
                _voiceLevel = _voiceLevelTarget = 1f;
                if (_stage.voiceClean != null) _stage.voiceClean.volume = Volume(_boot.voicemailVolume);
                if (_stage.voicePhone != null) _stage.voicePhone.volume = 0f;
            }
            else if (track == "news" && _stage.news != null)
            {
                _stage.news.clip = clip;
                _stage.news.mute = Muted;
                _stage.news.volume = Volume(_boot.newsVolume);
                _stage.news.Play();
            }
        }

        void Apply()
        {
            // "F9: edit this scene" for the first seconds, while the editor exists and nothing is being edited.
            bool hint = VhOpeningEditor.Instance != null && !VhOpeningEditor.Instance.IsOpen && speed <= 1.5f && !Paused;
            float t = _timeline != null ? _timeline.Time : 0f;
            _editHint.style.opacity = hint ? Mathf.Clamp01(Mathf.Min(t / 0.5f, (7f - t) / 1.5f)) : 0f;
            _black.style.opacity = _black01 * Mathf.Clamp01(blackScale);
            // (The editor lightens the dark under the blanket as it does the black.)
            _blanket.style.opacity = VhUtil.Ease(_blanket01) * 0.97f * Mathf.Clamp01(blackScale);
        }

        /// <summary>The opening's cues with their times (the editor's beats).</summary>
        public IReadOnlyList<OpeningCue> Cues => _resolved;

        /// <summary>
        /// Jumps to <paramref name="time"/> (the opening editor): the set starts over and every cue up to then lands at once,
        /// at its end state; sounds stop. Stays paused when it was; <see cref="PlayFromHere"/> starts the sounds where they'd be.
        /// </summary>
        public void Seek(float time)
        {
            if (_done) return;
            foreach (var a in _stage.Sounds()) a.Stop();
            _stage.ResetForOpening();
            _black01 = _blackTarget = 1f;
            _blanket01 = _blanketTarget = 0f;
            _vmStart = -1f;
            _speakerMix = _speakerTarget = 0f;
            _voiceLevel = _voiceLevelTarget = 1f;
            _subtitle.text = "";
            _timeline = new OpeningTimeline(_resolved);
            _fired.Clear();
            _timeline.Advance(Mathf.Max(0f, time), _fired);
            _seeking = true;
            try
            {
                for (int i = 0; i < _fired.Count; i++) Do(_fired[i]);
            }
            finally
            {
                _seeking = false;
            }
            if (_vmStart >= 0f) _stage.SetPlayback(_timeline.Time - _vmStart + (_voicemail != null ? _voicemail.trimStart : 0f));
            Apply();
        }

        /// <summary>Plays on from where it is (the editor's "Play from here"): the ring, the voicemail and the report pick up mid-way.</summary>
        public void PlayFromHere()
        {
            Paused = false;
            if (!_audible) return;
            float t = _timeline.Time;
            var ring = _timeline.First(OpeningCueKind.Ring);
            var stop = _timeline.First(OpeningCueKind.RingStop);
            if (ring != null && ring.at <= t && (stop == null || stop.at > t))
            {
                PlayLoop(_stage.ring, 0.55f);
                PlayLoop(_stage.vibrate, 0.8f);
            }
            ResumeVoice("voicemail", t);
            ResumeVoice("news", t);
        }

        void ResumeVoice(string track, float t)
        {
            var cue = _timeline.First(OpeningCueKind.Voice, track);
            var tr = _boot.Track(track);
            var clip = tr != null ? _boot.Clip(tr.clip) : null;
            if (cue == null || clip == null || cue.at > t) return;
            float into = t - cue.at + (track == "voicemail" ? tr.trimStart : 0f);
            if (into >= clip.length - 0.05f) return;
            if (track == "voicemail")
            {
                foreach (var a in new[] { _stage.voiceClean, _stage.voicePhone })
                {
                    if (a == null) continue;
                    a.clip = clip;
                    a.mute = Muted;
                    a.time = into;
                    a.Play();
                }
            }
            else if (_stage.news != null)
            {
                _stage.news.clip = clip;
                _stage.news.mute = Muted;
                _stage.news.volume = Volume(_boot.newsVolume);
                _stage.news.time = into;
                _stage.news.Play();
            }
        }

        /// <summary>Ends the opening on the title, ready to play. <paramref name="skipped"/> stops the voices.</summary>
        public void Finish(bool skipped)
        {
            if (_done) return;
            _done = true;
            _running = false;
            Skipped = skipped;
            if (_stage != null)
            {
                if (_stage.ring != null) _stage.ring.Stop();
                if (_stage.vibrate != null) _stage.vibrate.Stop();
                if (skipped)
                {
                    if (_stage.voiceClean != null) _stage.voiceClean.Stop();
                    if (_stage.voicePhone != null) _stage.voicePhone.Stop();
                    if (_stage.news != null) _stage.news.Stop();
                }
                _stage.ShowTitleFinal();
            }
            _root.RemoveFromHierarchy();
            _title.Root.SetEnabled(true);
            if (!_musicStarted && GameRoot.Exists && GameRoot.Flow.Current == GameState.Title)
                GameRoot.Audio.PlayMusic(GameRoot.Config.titleMusic, 1.5f);
            Finished?.Invoke();
        }
    }
}
