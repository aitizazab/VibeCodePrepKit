using System.Collections.Generic;
using JamKit.Flow;
using JamKit.Shell;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace JamKit.VisitingHours
{
    /// <summary>
    /// Hooks the slice into the shell's title screen while Visiting Hours is the active template
    /// (GameConfig's first gameplay scene is the slice's scene): the first time the title comes up,
    /// the opening plays over it (no input until the title: the phone, the missed call, the
    /// voicemail, "...sleep in."); then the menu shows over a TV news report (the picture, a
    /// ticker, the anchor's voice) with the placeholder sad pad as title music. The opening is
    /// skipped in batch mode (tests, the smoke run) and can be skipped by holding Escape.
    /// </summary>
    public static class VhBoot
    {
        static VisitingHoursBoot _boot;
        static VhTitleRunner _runner;
        static bool _openingPlayed;
        static bool _skipOpening;

        /// <summary>Tests: don't play the opening on the next title.</summary>
        public static bool SkipOpening
        {
            get => _skipOpening;
            set => _skipOpening = value;
        }

        public static bool OpeningPlayed => _openingPlayed;
        public static VhOpening Opening => _runner != null ? _runner.opening : null;
        public static VisitingHoursBoot Config => _boot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Hook()
        {
            _boot = null;
            _runner = null;
            _openingPlayed = false;
            _skipOpening = false;
            GameRoot.Ready -= OnReady;
            GameRoot.Ready += OnReady;
        }

        static void OnReady(GameRoot root)
        {
            _boot = VisitingHoursBoot.Load();
            if (_boot == null) return;
            _runner = root.gameObject.GetComponent<VhTitleRunner>();
            if (_runner == null) _runner = root.gameObject.AddComponent<VhTitleRunner>();
            _runner.boot = _boot;
            GameRoot.Ui.Title.Shown -= OnTitleShown;
            GameRoot.Ui.Title.Shown += OnTitleShown;
        }

        /// <summary>True when Play starts the slice.</summary>
        public static bool IsActive => GameRoot.Exists && _boot != null && GameRoot.Config.firstGameplayScene == _boot.sceneName;

        static void OnTitleShown(TitleScreen title)
        {
            if (_runner == null) return;
            bool active = IsActive;
            _runner.SetTitleLook(title, active);
            if (!active) return;
            if (!_openingPlayed && !_skipOpening && !Application.isBatchMode)
            {
                _openingPlayed = true;
                _runner.PlayOpening(title);
            }
            else _runner.StartTitleVoice();
        }

        /// <summary>Plays the opening now (tests run it at a high speed).</summary>
        public static VhOpening PlayOpeningNow(float speed = 1f)
        {
            if (_runner == null || !GameRoot.Exists) return null;
            _openingPlayed = true;
            _runner.PlayOpening(GameRoot.Ui.Title);
            if (_runner.opening != null) _runner.opening.speed = speed;
            return _runner.opening;
        }
    }

    /// <summary>Ticks the opening and the title's ticker (lives on the GameRoot).</summary>
    public sealed class VhTitleRunner : MonoBehaviour
    {
        public VisitingHoursBoot boot;
        public VhOpening opening;
        VisualElement _titleScreen, _news, _tickerBar;
        Label _ticker;
        AudioSource _voice;
        float _tickerX;
        bool _look;

        AudioSource Voice
        {
            get
            {
                if (_voice == null)
                {
                    _voice = gameObject.AddComponent<AudioSource>();
                    _voice.playOnAwake = false;
                    _voice.ignoreListenerPause = true;
                    _voice.spatialBlend = 0f;
                }
                _voice.mute = GameRoot.Exists && GameRoot.Audio.Muted;
                return _voice;
            }
        }

        public void SetTitleLook(TitleScreen title, bool on)
        {
            _titleScreen = title.Root.Q<VisualElement>("title-screen");
            if (_titleScreen == null) return;
            if (boot.style != null && !title.Root.styleSheets.Contains(boot.style)) title.Root.styleSheets.Add(boot.style);
            if (_news == null)
            {
                _news = new VisualElement { name = "vh-title-news", pickingMode = PickingMode.Ignore };
                _news.AddToClassList("vh-title__news");
                if (boot.titleNews != null) _news.style.backgroundImage = new StyleBackground(boot.titleNews);
                var shade = new VisualElement { pickingMode = PickingMode.Ignore };
                shade.AddToClassList("vh-title__shade");
                _news.Add(shade);
                _tickerBar = new VisualElement { pickingMode = PickingMode.Ignore };
                _tickerBar.AddToClassList("vh-title__ticker-bar");
                _ticker = new Label(boot.ticker) { name = "vh-title-ticker", pickingMode = PickingMode.Ignore };
                _ticker.AddToClassList("vh-title__ticker");
                _tickerBar.Add(_ticker);
                _news.Add(_tickerBar);
                _titleScreen.Insert(0, _news);
            }
            _look = on;
            _news.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
            _titleScreen.EnableInClassList("vh-title", on);
            if (!on) StopVoice();
        }

        public void StartTitleVoice()
        {
            if (boot == null || boot.newsAnchor == null) return;
            var v = Voice;
            v.clip = boot.newsAnchor;
            v.loop = true;
            v.volume = boot.anchorVolume * (GameRoot.Exists ? GameRoot.Settings.sfxVolume : 1f);
            v.Play();
        }

        void StopVoice()
        {
            if (_voice != null && _voice.isPlaying && (opening == null || !opening.Running)) _voice.Stop();
        }

        public void PlayOpening(TitleScreen title)
        {
            opening?.Finish(false);
            opening = new VhOpening(boot, GameRoot.Ui.Root, title, Voice);
            opening.Finished += () => { opening = null; StartTitleVoice(); };
            opening.Start();
        }

        void Update()
        {
            if (!GameRoot.Exists) return;
            float udt = Time.unscaledDeltaTime;
            if (opening != null)
            {
                if (GameRoot.Flow.Current != GameState.Title) opening.Finish(false);
                else opening.Tick(udt);
            }
            bool onTitle = _look && GameRoot.Flow.Current == GameState.Title && _titleScreen != null && _titleScreen.panel != null;
            if (onTitle && _ticker != null)
            {
                float w = _ticker.resolvedStyle.width;
                float bar = _tickerBar.resolvedStyle.width;
                if (float.IsNaN(w) || w <= 0f) w = 1200f;
                if (float.IsNaN(bar) || bar <= 0f) bar = 1920f;
                _tickerX -= udt * 110f;
                if (_tickerX < -w) _tickerX = bar;
                _ticker.style.translate = new Translate(_tickerX, 0f);
            }
            else if (_voice != null && _voice.isPlaying && opening == null) _voice.Stop();
        }
    }

    /// <summary>
    /// The opening, drawn over the title (which waits, disabled, underneath): black, the phone
    /// ringing, Nadia's eyes opening on a dark ceiling, the phone's screen, the voicemail with
    /// subtitles (silence plus subtitles until the recording exists), her sitting up and sinking
    /// back, "...sleep in.", a fade to black, then the title. Driven by the core OpeningTimeline.
    /// </summary>
    public sealed class VhOpening
    {
        readonly VisitingHoursBoot _boot;
        readonly TitleScreen _title;
        readonly AudioSource _voice;
        readonly OpeningTimeline _timeline;
        readonly List<OpeningCue> _fired = new List<OpeningCue>(8);
        readonly VisualElement _root, _ceiling, _lidTop, _lidBottom, _phone, _black;
        readonly Label _phoneText, _subtitle, _skipHint;
        AudioSource _ring;
        bool _running, _done;
        float _eyes, _eyesTarget, _eyesSpeed = 1f, _lift, _liftTarget, _liftSpeed = 1f, _black01 = 1f, _blackTarget = 1f, _blackSpeed = 1f;
        float _holdSkip, _phonePulse;
        bool _ringing;

        /// <summary>Time multiplier (tests play it fast).</summary>
        public float speed = 1f;
        public event System.Action Finished;
        public bool Running => _running && !_done;
        public float Time => _timeline.Time;
        public string Subtitle => _subtitle.text;
        public string PhoneText => _phoneText.text;
        public bool PhoneShown => _phone.style.display == DisplayStyle.Flex;
        /// <summary>Cue kinds seen so far, in order (tests).</summary>
        public readonly List<OpeningCueKind> Seen = new List<OpeningCueKind>();

        public VhOpening(VisitingHoursBoot boot, VisualElement parent, TitleScreen title, AudioSource voice)
        {
            _boot = boot;
            _title = title;
            _voice = voice;
            _timeline = new OpeningTimeline(boot.opening);
            if (boot.fitSubtitlesToVoicemail && boot.voicemail != null)
                _timeline.ScaleSpan("vo_elias_voicemail", OpeningCueKind.PhoneHide, boot.voicemail.length + 1.2f);

            _root = new VisualElement { name = "vh-opening", pickingMode = PickingMode.Position };
            _root.AddToClassList("vh-opening");
            if (boot.style != null) _root.styleSheets.Add(boot.style);
            _ceiling = Add("vh-opening__ceiling");
            if (boot.ceiling != null) _ceiling.style.backgroundImage = new StyleBackground(boot.ceiling);
            _lidTop = Add("vh-opening__lid");
            _lidTop.AddToClassList("vh-opening__lid--top");
            _lidBottom = Add("vh-opening__lid");
            _lidBottom.AddToClassList("vh-opening__lid--bottom");
            _phone = Add("vh-opening__phone");
            if (boot.phone != null) _phone.style.backgroundImage = new StyleBackground(boot.phone);
            _phoneText = new Label("") { name = "vh-opening-phone-text", pickingMode = PickingMode.Ignore };
            _phoneText.AddToClassList("vh-opening__phone-text");
            _phone.Add(_phoneText);
            _phone.style.display = DisplayStyle.None;
            _subtitle = new Label("") { name = "vh-opening-subtitle", pickingMode = PickingMode.Ignore };
            _subtitle.AddToClassList("vh-opening__subtitle");
            _root.Add(_subtitle);
            _black = Add("vh-opening__black");
            _skipHint = new Label("Hold Esc to skip") { pickingMode = PickingMode.Ignore };
            _skipHint.AddToClassList("vh-opening__skip");
            _skipHint.style.opacity = 0f;
            _root.Add(_skipHint);
            parent.Add(_root);
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
            _title.Root.SetEnabled(false);
            _title.Root.style.opacity = 0f;
            if (GameRoot.Exists) GameRoot.Audio.StopMusic(0.05f);
            Apply();
        }

        public void Tick(float udt)
        {
            if (!Running) return;
            if (GameRoot.Exists && !string.IsNullOrEmpty(GameRoot.Audio.CurrentMusic)) GameRoot.Audio.StopMusic(0.05f);

            // Hold Escape to skip (only shown once someone presses it).
            var kb = Keyboard.current;
            bool hold = _boot.holdToSkipSeconds > 0f && kb != null && kb.escapeKey.isPressed;
            _holdSkip = hold ? _holdSkip + udt : 0f;
            _skipHint.style.opacity = hold ? 1f : Mathf.Max(0f, _skipHint.resolvedStyle.opacity - udt);
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

            _eyes = Mathf.MoveTowards(_eyes, _eyesTarget, dt * _eyesSpeed);
            _lift = Mathf.MoveTowards(_lift, _liftTarget, dt * _liftSpeed);
            _black01 = Mathf.MoveTowards(_black01, _blackTarget, dt * _blackSpeed);
            _phonePulse += dt;
            Apply();
        }

        void Do(OpeningCue c)
        {
            Seen.Add(c.kind);
            float secs = Mathf.Max(0.05f, c.seconds);
            switch (c.kind)
            {
                case OpeningCueKind.Ring:
                    _ringing = true;
                    PlayRing(true);
                    break;
                case OpeningCueKind.RingStop:
                    _ringing = false;
                    PlayRing(false);
                    break;
                case OpeningCueKind.EyesOpen:
                    _black01 = 0f;
                    _blackTarget = 0f;
                    _eyesTarget = 1f;
                    _eyesSpeed = 1f / secs;
                    break;
                case OpeningCueKind.PhoneText:
                    _phone.style.display = DisplayStyle.Flex;
                    _phoneText.text = c.text;
                    break;
                case OpeningCueKind.PhoneHide:
                    _phone.style.display = DisplayStyle.None;
                    break;
                case OpeningCueKind.Voice:
                    var clip = _boot.Voice(c.text);
                    if (clip != null && _voice != null)
                    {
                        _voice.loop = false;
                        _voice.volume = GameRoot.Exists ? GameRoot.Settings.sfxVolume : 1f;
                        _voice.PlayOneShot(clip);
                    }
                    else Debug.LogWarning("JAMKIT_VH audio missing: " + c.text + ".wav (silence + subtitles)");
                    break;
                case OpeningCueKind.Subtitle:
                    bool nadia = !string.IsNullOrEmpty(c.speaker);
                    _subtitle.text = nadia ? "<i>" + c.text + "</i>" : c.text;
                    _subtitle.EnableInClassList("vh-opening__subtitle--nadia", nadia);
                    break;
                case OpeningCueKind.ClearSubtitle:
                    _subtitle.text = "";
                    break;
                case OpeningCueKind.SitUp:
                    _liftTarget = 1f;
                    _liftSpeed = 1f / secs;
                    break;
                case OpeningCueKind.SinkBack:
                    _liftTarget = 0f;
                    _liftSpeed = 1f / secs;
                    _eyesTarget = 0.55f;
                    _eyesSpeed = 0.45f / secs;
                    break;
                case OpeningCueKind.FadeOut:
                    _blackTarget = 1f;
                    _blackSpeed = 1f / secs;
                    _eyesTarget = 0f;
                    _eyesSpeed = 0.55f / secs;
                    break;
                case OpeningCueKind.End:
                    Finish(false);
                    break;
            }
        }

        void PlayRing(bool on)
        {
            if (on)
            {
                if (_ring == null && _voice != null)
                {
                    _ring = _voice.gameObject.AddComponent<AudioSource>();
                    _ring.playOnAwake = false;
                    _ring.ignoreListenerPause = true;
                    _ring.loop = true;
                }
                if (_ring == null) return;
                _ring.clip = ProceduralAudio.Get("vh_phone_ring");
                _ring.mute = GameRoot.Exists && GameRoot.Audio.Muted;
                _ring.volume = 0.7f * (GameRoot.Exists ? GameRoot.Settings.sfxVolume : 1f);
                _ring.Play();
            }
            else if (_ring != null) _ring.Stop();
        }

        void Apply()
        {
            // Eyelids: half the screen each when shut, gone when open.
            float lid = (1f - Mathf.SmoothStep(0f, 1f, _eyes)) * 50f;
            _lidTop.style.height = Length.Percent(lid);
            _lidBottom.style.height = Length.Percent(lid);
            _ceiling.style.opacity = Mathf.Clamp01(_eyes * 1.3f);
            float lift = Mathf.SmoothStep(0f, 1f, _lift);
            _ceiling.style.translate = new Translate(0f, Length.Percent(-6f * lift));
            _ceiling.style.scale = new Scale(new Vector3(1.06f + 0.04f * lift, 1.06f + 0.04f * lift, 1f));
            _black.style.opacity = _black01;
            if (_ringing) _phoneText.style.opacity = 0.65f + 0.35f * Mathf.Sin(_phonePulse * 6f);
            else _phoneText.style.opacity = 1f;
        }

        /// <summary>Ends the opening: the title fades up. <paramref name="skipped"/> stops the voices.</summary>
        public void Finish(bool skipped)
        {
            if (_done) return;
            _done = true;
            _running = false;
            PlayRing(false);
            if (_ring != null) Object.Destroy(_ring);
            if (skipped && _voice != null) _voice.Stop();
            _root.RemoveFromHierarchy();
            _title.Root.SetEnabled(true);
            _title.Root.style.opacity = StyleKeyword.Null;
            _title.FocusFirst();
            if (GameRoot.Exists && GameRoot.Flow.Current == GameState.Title) GameRoot.Audio.PlayMusic(GameRoot.Config.titleMusic, 2.5f);
            Finished?.Invoke();
        }
    }
}
