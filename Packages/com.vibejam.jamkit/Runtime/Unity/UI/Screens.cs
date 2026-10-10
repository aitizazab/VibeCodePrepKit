using System;
using System.Collections.Generic;
using JamKit.Flow;
using UnityEngine;
using UnityEngine.UIElements;

namespace JamKit.Shell
{
    public sealed class TitleScreen : ShellScreen
    {
        /// <summary>After the title screen fills in its texts: templates change them here (a story's title
        /// that changes after an ending), and show or hide their own buttons.</summary>
        public event Action<TitleScreen> Shown;

        public string TitleText { get { var l = Root.Q<Label>("title-label"); return l != null ? l.text : ""; } }
        public string TaglineText { get { var l = Root.Q<Label>("tagline-label"); return l != null ? l.text : ""; } }

        /// <summary>Overrides the title (null leaves it).</summary>
        public void SetTitle(string title)
        {
            var l = Root.Q<Label>("title-label");
            if (l != null && title != null) l.text = title;
        }

        /// <summary>Overrides the tagline (null leaves it; "" hides it).</summary>
        public void SetTagline(string tagline)
        {
            var l = Root.Q<Label>("tagline-label");
            if (l == null || tagline == null) return;
            l.text = tagline;
            SetShown(l, tagline.Length > 0);
        }

        /// <summary>Renames a button ("play" to "New game", or something stranger).</summary>
        public void SetButtonText(string name, string text)
        {
            var b = Root.Q<Button>(name);
            if (b != null && text != null) b.text = text;
        }

        string _playText;

        protected override void OnInit()
        {
            _playText = Root.Q<Button>("play")?.text;
            InitButtons();
        }

        void InitButtons()
        {
            Bind("play", () => GameRoot.Instance.StartGame());
            Bind("howto", () => Ui.OpenHowToPlay(null));
            Bind("settings", () => Ui.OpenSettings());
            Bind("credits", () => GameRoot.Instance.ShowCredits());
            var quit = Bind("quit", () => GameRoot.Instance.QuitApp());
            if (Application.platform == RuntimePlatform.WebGLPlayer) SetShown(quit, false);
        }

        protected override void OnShow()
        {
            var config = GameRoot.Config;
            var title = Root.Q<Label>("title-label");
            if (title != null) title.text = config.gameTitle;
            var tagline = Root.Q<Label>("tagline-label");
            if (tagline != null)
            {
                tagline.text = config.tagline;
                SetShown(tagline, !string.IsNullOrEmpty(config.tagline));
            }
            var best = Root.Q<Label>("best-label");
            if (best != null)
            {
                float t = GameRoot.Save.Data.bestTime;
                best.text = t > 0f ? "Best time: " + RunStats.FormatTimePrecise(t) : "";
                SetShown(best, t > 0f && config.showBestTime);
            }
            var play = Root.Q<Button>("play");
            if (play != null && _playText != null) play.text = _playText;
            Shown?.Invoke(this);
        }
    }

    /// <summary>
    /// Play's difficulty picker (GameConfig.chooseDifficultyOnPlay): three cards, Story / Normal / Hard,
    /// each with its one-line description. The remembered choice (Settings' difficulty, Normal the first
    /// time) has focus; picking a card saves it and starts the run; Back returns to the title.
    /// Buttons: "difficulty-story", "difficulty-normal", "difficulty-hard", "back".
    /// Built in code with the shell's theme classes (jk-difficulty-* in Shell.uss).
    /// </summary>
    public sealed class DifficultyScreen : ShellScreen
    {
        readonly Dictionary<Difficulty, Button> _options = new Dictionary<Difficulty, Button>();
        readonly Dictionary<Difficulty, Label> _badges = new Dictionary<Difficulty, Label>();

        protected override bool BuildsOwnContent => true;

        public static string ButtonName(Difficulty d) => "difficulty-" + DifficultyText.Name(d).ToLowerInvariant();

        /// <summary>The card that has focus when the picker opens: the last choice (Normal the first time).</summary>
        public Difficulty Preselected => GameRoot.Exists ? GameRoot.Settings.difficulty : Difficulty.Normal;

        /// <summary>The name of the card that has keyboard focus ("" if none): tests check the preselection.</summary>
        public string FocusedName
        {
            get
            {
                var f = Root.focusController != null ? Root.focusController.focusedElement as Button : null;
                return f != null ? f.name : "";
            }
        }

        protected override void OnInit()
        {
            var screen = new VisualElement { name = "difficulty-screen" };
            screen.AddToClassList("jk-screen");
            screen.AddToClassList("jk-scrim");
            var panel = new VisualElement();
            panel.AddToClassList("jk-panel");
            panel.AddToClassList("jk-difficulty-panel");
            screen.Add(panel);

            var kicker = new Label("NEW RUN");
            kicker.AddToClassList("jk-kicker");
            panel.Add(kicker);
            var heading = new Label("Choose your difficulty");
            heading.AddToClassList("jk-heading");
            panel.Add(heading);

            var list = new VisualElement();
            list.AddToClassList("jk-difficulty-list");
            panel.Add(list);
            foreach (var d in DifficultyText.All) list.Add(MakeOption(d));

            var note = new Label("You can change this any time in Settings.");
            note.AddToClassList("jk-footnote");
            note.AddToClassList("jk-dim");
            panel.Add(note);
            var back = new Button { name = "back", text = "Back" };
            back.AddToClassList("jk-button");
            back.AddToClassList("jk-button--back");
            back.AddToClassList("jk-button--small");
            panel.Add(back);
            var hint = new Label("Up / Down to choose, Enter or (A) to start, Esc or (B) to go back");
            hint.AddToClassList("jk-footnote");
            hint.AddToClassList("jk-dim");
            screen.Add(hint);
            Root.Add(screen);

            foreach (var d in DifficultyText.All)
            {
                var choice = d;
                Bind(ButtonName(d), () => Choose(choice));
            }
            Bind("back", () => Ui.CloseOverlay());
        }

        Button MakeOption(Difficulty d)
        {
            string lower = DifficultyText.Name(d).ToLowerInvariant();
            var b = new Button { name = ButtonName(d), text = "" };
            b.AddToClassList("jk-button");
            b.AddToClassList("jk-difficulty-option");
            b.AddToClassList("jk-difficulty-option--" + lower);
            var stripe = new VisualElement { pickingMode = PickingMode.Ignore };
            stripe.AddToClassList("jk-difficulty-stripe");
            b.Add(stripe);
            var texts = new VisualElement { pickingMode = PickingMode.Ignore };
            texts.AddToClassList("jk-difficulty-texts");
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList("jk-difficulty-row");
            var name = new Label(DifficultyText.Name(d)) { pickingMode = PickingMode.Ignore };
            name.AddToClassList("jk-difficulty-name");
            var badge = new Label("") { pickingMode = PickingMode.Ignore };
            badge.AddToClassList("jk-difficulty-badge");
            row.Add(name);
            row.Add(badge);
            var desc = new Label(DifficultyText.Describe(d)) { pickingMode = PickingMode.Ignore };
            desc.AddToClassList("jk-difficulty-desc");
            texts.Add(row);
            texts.Add(desc);
            b.Add(texts);
            _options[d] = b;
            _badges[d] = badge;
            return b;
        }

        protected override void OnShow()
        {
            var current = Preselected;
            bool playedBefore = GameRoot.Exists && GameRoot.Save.Data.runsStarted > 0;
            foreach (var d in DifficultyText.All)
            {
                string text = playedBefore && d == current ? "Last played" : DifficultyText.Badge(d);
                var badge = _badges[d];
                badge.text = text;
                SetShown(badge, text.Length > 0);
            }
        }

        protected override VisualElement FirstFocusable() =>
            _options.TryGetValue(Preselected, out var b) ? b : base.FirstFocusable();

        public override void FocusFirst()
        {
            // Always open on the remembered choice (not on whichever card had focus last time).
            Root.schedule.Execute(() =>
            {
                if (Root.style.display == DisplayStyle.None) return;
                FirstFocusable()?.Focus();
            });
            FirstFocusable()?.Focus();
        }

        void Choose(Difficulty d)
        {
            if (GameRoot.Exists) GameRoot.Instance.StartRun(d);
        }
    }

    public sealed class SettingsScreen : ShellScreen
    {
        Slider _master, _music, _sfx, _sensitivity, _shake, _textSpeed, _autoDelay;
        Toggle _invertY, _fullscreen, _pixelLook, _skipUnread;
        Button _difficulty, _controls;
        Label _difficultyInfo;

        static bool FullscreenAvailable => Application.platform != RuntimePlatform.WebGLPlayer;

        protected override void OnInit()
        {
            _master = Slider("master", 10f, v => S.masterVolume = v / 100f);
            _music = Slider("music", 10f, v => S.musicVolume = v / 100f);
            _sfx = Slider("sfx", 10f, v => S.sfxVolume = v / 100f);
            _sensitivity = Slider("sensitivity", 0.1f, v => S.mouseSensitivity = v);
            _shake = Slider("shake", 10f, v => S.screenShake = v / 100f);
            _invertY = Toggle("invert-y", v => S.invertY = v);
            // Dialogue settings (GameConfig.showTextSettings): optional in the UXML.
            _textSpeed = Root.Q<Slider>("text-speed") != null ? Slider("text-speed", 0.25f, v => S.textSpeed = v) : null;
            _autoDelay = Root.Q<Slider>("auto-delay") != null ? Slider("auto-delay", 0.2f, v => S.autoDelay = v) : null;
            _skipUnread = Root.Q<Toggle>("skip-unread") != null ? Toggle("skip-unread", v => S.skipUnread = v) : null;
            _fullscreen = Toggle("fullscreen", v => S.fullscreen = v);
            SetShown(_fullscreen, FullscreenAvailable);
            _pixelLook = Root.Q<Toggle>("pixel-look");
            _pixelLook?.RegisterValueChangedCallback(e =>
            {
                S.pixelLook = e.newValue;
                Changed();
            });

            _difficulty = Bind("difficulty", () => StepDifficulty(1));
            _difficulty?.RegisterCallback<NavigationMoveEvent>(e =>
            {
                if (e.direction != NavigationMoveEvent.Direction.Left && e.direction != NavigationMoveEvent.Direction.Right) return;
                StepDifficulty(e.direction == NavigationMoveEvent.Direction.Right ? 1 : -1);
                e.StopPropagation();
                _difficulty.focusController?.IgnoreEvent(e);
            });
            _difficultyInfo = Root.Q<Label>("difficulty-info");
            // Controls (rebinding): added in code, above Back, so every template's Settings has it.
            var back = Root.Q<Button>("back");
            if (back != null && back.parent != null && Root.Q<Button>("controls") == null)
            {
                var controls = new Button { name = "controls", text = "Controls  >" };
                controls.AddToClassList("jk-option");
                controls.AddToClassList("jk-stepper");
                back.parent.Insert(back.parent.IndexOf(back), controls);
            }
            _controls = Root.Q<Button>("controls") != null ? Bind("controls", () => Ui.OpenControls()) : null;
            Bind("back", () => Ui.CloseOverlay());
        }

        static Settings S => GameRoot.Settings;

        Slider Slider(string name, float navStep, Action<float> write)
        {
            var s = Root.Q<Slider>(name);
            if (s == null) { Debug.LogWarning("JamKit: Settings.uxml has no slider " + name); return null; }
            s.focusable = true;
            s.RegisterValueChangedCallback(e =>
            {
                write(e.newValue);
                Changed();
                Label(s);
            });
            // Bigger steps than the default 1% for keyboard and gamepad.
            s.RegisterCallback<NavigationMoveEvent>(e =>
            {
                if (e.direction != NavigationMoveEvent.Direction.Left && e.direction != NavigationMoveEvent.Direction.Right) return;
                float step = e.direction == NavigationMoveEvent.Direction.Right ? navStep : -navStep;
                s.value = Mathf.Clamp(s.value + step, s.lowValue, s.highValue);
                e.StopPropagation();
                s.focusController?.IgnoreEvent(e);
            }, TrickleDown.TrickleDown);
            return s;
        }

        Toggle Toggle(string name, Action<bool> write)
        {
            var t = Root.Q<Toggle>(name);
            if (t == null) { Debug.LogWarning("JamKit: Settings.uxml has no toggle " + name); return null; }
            t.RegisterValueChangedCallback(e =>
            {
                write(e.newValue);
                Changed();
            });
            return t;
        }

        void Changed()
        {
            GameRoot.Save.NotifySettingsChanged();
            Ui.PlayUiSound("ui_move");
        }

        void StepDifficulty(int step)
        {
            S.SetDifficulty(DifficultyText.Next(S.difficulty, step));
            GameRoot.Save.NotifySettingsChanged();
            RefreshDifficulty();
        }

        void RefreshDifficulty()
        {
            if (_difficulty != null) _difficulty.text = "Difficulty:   <  " + DifficultyText.Name(S.difficulty) + "  >";
            if (_difficultyInfo != null) _difficultyInfo.text = DifficultyText.Describe(S.difficulty);
        }

        static void Label(Slider s)
        {
            if (s == null) return;
            string baseLabel = s.userData as string;
            if (baseLabel == null) { baseLabel = s.label; s.userData = baseLabel; }
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            switch (s.name)
            {
                case "sensitivity": s.label = baseLabel + "   x" + s.value.ToString("0.0", inv); break;
                case "text-speed": s.label = baseLabel + "   " + (s.value >= Settings.InstantTextSpeed - 0.001f ? "Instant" : "x" + s.value.ToString("0.00", inv)); break;
                case "auto-delay": s.label = baseLabel + "   " + s.value.ToString("0.0", inv) + " s"; break;
                default: s.label = baseLabel + "   " + Mathf.RoundToInt(s.value) + "%"; break;
            }
        }

        protected override void OnShow()
        {
            var s = S;
            Set(_master, s.masterVolume * 100f);
            Set(_music, s.musicVolume * 100f);
            Set(_sfx, s.sfxVolume * 100f);
            Set(_sensitivity, s.mouseSensitivity);
            Set(_shake, s.screenShake * 100f);
            _invertY?.SetValueWithoutNotify(s.invertY);
            _fullscreen?.SetValueWithoutNotify(s.fullscreen);
            _pixelLook?.SetValueWithoutNotify(s.pixelLook);
            Set(_textSpeed, s.textSpeed);
            Set(_autoDelay, s.autoDelay);
            _skipUnread?.SetValueWithoutNotify(s.skipUnread);
            var config = GameRoot.Exists ? GameRoot.Config : null;
            SetShown(_pixelLook, config != null && config.showPixelLookSetting);
            bool text = config != null && config.showTextSettings;
            bool action = config == null || config.showActionSettings;
            SetShown(_textSpeed, text);
            SetShown(_autoDelay, text);
            SetShown(_skipUnread, text);
            SetShown(_sensitivity, action);
            SetShown(_invertY, action);
            SetShown(_difficulty, action);
            SetShown(_difficultyInfo, action);
            SetShown(_controls, action);
            RefreshDifficulty();
        }

        static void Set(Slider sl, float v)
        {
            if (sl == null) return;
            sl.SetValueWithoutNotify(v);
            Label(sl);
        }

        protected override VisualElement FirstFocusable() => (VisualElement)_master ?? base.FirstFocusable();

        protected override void OnHide() => GameRoot.Save.Save();
    }

    public sealed class PauseScreen : ShellScreen
    {
        protected override void OnInit()
        {
            Bind("resume", () => GameRoot.Instance.Resume());
            Bind("restart", () => GameRoot.Instance.Retry());
            Bind("settings", () => Ui.OpenSettings());
            Bind("howto", () => Ui.OpenHowToPlay(null));
            Bind("quit", () => GameRoot.Instance.QuitToTitle());
        }
    }

    /// <summary>Game over and victory: a heading plus the run's stats.</summary>
    public sealed class ResultScreen : ShellScreen
    {
        readonly bool _victory;

        public ResultScreen(bool victory) { _victory = victory; }

        protected override void OnInit()
        {
            if (_victory)
            {
                Bind("continue", () => GameRoot.Instance.ShowCredits());
            }
            else
            {
                Bind("retry", () => GameRoot.Instance.Retry());
                Bind("title", () => GameRoot.Instance.QuitToTitle());
            }
        }

        Label _quote;

        /// <summary>The story line on this screen ("" when none): GameRoot.Run.SetQuote(speaker, line).</summary>
        public string QuoteText => _quote != null && _quote.style.display != DisplayStyle.None ? _quote.text : "";

        void ShowQuote(RunStats run)
        {
            var stats = Root.Q<VisualElement>("stats");
            if (_quote == null && stats != null && stats.parent != null)
            {
                _quote = new Label { name = "end-quote" };
                _quote.AddToClassList("jk-end-quote");
                stats.parent.Insert(stats.parent.IndexOf(stats), _quote);
            }
            if (_quote == null) return;
            bool has = !string.IsNullOrEmpty(run.quote);
            _quote.text = !has ? "" : string.IsNullOrEmpty(run.quoteSpeaker)
                ? "“" + run.quote + "”"
                : "<b>" + run.quoteSpeaker + "</b>:  “" + run.quote + "”";
            SetShown(_quote, has);
        }

        protected override void OnShow()
        {
            ShowQuote(GameRoot.Run);
            var stats = Root.Q<VisualElement>("stats");
            if (stats == null) return;
            stats.Clear();
            var run = GameRoot.Run;
            AddStat(stats, "Time", RunStats.FormatTimePrecise(run.time), false);
            if (run.score != 0) AddStat(stats, "Score", run.score.ToString(), false);
            foreach (var extra in run.Extra) AddStat(stats, extra.label, extra.value, false);
            if (_victory)
            {
                float best = GameRoot.Save.Data.bestTime;
                if (GameRoot.Instance.LastRunWasBest) AddStat(stats, "Best time", "New best!", true);
                else if (best > 0f) AddStat(stats, "Best time", RunStats.FormatTimePrecise(best), false);
            }
        }

        static void AddStat(VisualElement parent, string label, string value, bool highlight)
        {
            var row = new VisualElement();
            row.AddToClassList("jk-stat");
            if (highlight) row.AddToClassList("jk-stat--highlight");
            var l = new Label(label);
            l.AddToClassList("jk-stat-label");
            var v = new Label(value);
            v.AddToClassList("jk-stat-value");
            row.Add(l);
            row.Add(v);
            parent.Add(row);
        }
    }

    public sealed class CreditsScreen : ShellScreen
    {
        public float scrollSpeed = 70f;
        VisualElement _viewport;
        Label _content;
        float _offset;

        protected override void OnInit()
        {
            _viewport = Root.Q<VisualElement>("credits-viewport");
            _content = Root.Q<Label>("credits-content");
            Bind("back", () => GameRoot.Instance.CloseCredits());
        }

        protected override void OnShow()
        {
            var asset = GameRoot.Config.credits;
            string text = asset != null ? asset.text : "Made with JamKit.";
            if (_content != null) _content.text = text.Replace("\r", "");
            _offset = 0f;
            Tick(0f);
        }

        public override bool OnBack()
        {
            GameRoot.Instance.CloseCredits();
            return true;
        }

        public override void Tick(float unscaledDt)
        {
            if (_content == null || _viewport == null) return;
            float viewH = _viewport.resolvedStyle.height;
            float contentH = _content.resolvedStyle.height;
            if (float.IsNaN(viewH) || float.IsNaN(contentH)) return;
            _offset += scrollSpeed * unscaledDt;
            float y = viewH - _offset;
            if (y < -contentH) _offset = 0f;
            _content.style.translate = new Translate(0f, y);
        }
    }

    public sealed class HowToPlayScreen : ShellScreen
    {
        int _page;
        Action _onClosed;
        Label _title, _body, _counter;
        VisualElement _image;
        Button _prev, _next, _close;

        public int Page => _page;
        public int PageCount => Pages.Count;

        static List<HowToPlayPage> Pages => GameRoot.Config.howToPlay ?? (GameRoot.Config.howToPlay = new List<HowToPlayPage>());

        protected override void OnInit()
        {
            _title = Root.Q<Label>("howto-title");
            _body = Root.Q<Label>("howto-body");
            _counter = Root.Q<Label>("howto-page");
            _image = Root.Q<VisualElement>("howto-image");
            _prev = Bind("prev", () => Go(_page - 1));
            _next = Bind("next", () => { if (_page >= Pages.Count - 1) Close(); else Go(_page + 1); });
            _close = Bind("close", Close);
        }

        public void Open(Action onClosed)
        {
            _onClosed = onClosed;
            _page = 0;
        }

        protected override void OnShow() => Refresh();

        void Go(int page)
        {
            _page = Mathf.Clamp(page, 0, Mathf.Max(0, Pages.Count - 1));
            Refresh();
            if (_page == 0 && _next != null) _next.Focus();
        }

        void Refresh()
        {
            var pages = Pages;
            if (pages.Count == 0)
            {
                if (_title != null) _title.text = "Nothing here yet";
                if (_body != null) _body.text = "Add How to Play pages in GameConfig.";
                SetShown(_image, false);
            }
            else
            {
                var p = pages[_page];
                if (_title != null) _title.text = p.title;
                if (_body != null) _body.text = p.body;
                bool hasImage = p.image != null;
                SetShown(_image, hasImage);
                if (hasImage && _image != null) _image.style.backgroundImage = new StyleBackground(p.image);
            }
            if (_counter != null) _counter.text = pages.Count > 1 ? (_page + 1) + " / " + pages.Count : "";
            SetShown(_prev, _page > 0);
            bool last = _page >= pages.Count - 1;
            if (_next != null) _next.text = last ? (_onClosed != null ? "Start!" : "Got it") : "Next";
            SetShown(_close, !last);
        }

        void Close()
        {
            var then = _onClosed;
            _onClosed = null;
            Ui.CloseOverlay();
            then?.Invoke();
        }

        /// <summary>Back closes without running the "then start the game" callback.</summary>
        public override bool OnBack()
        {
            _onClosed = null;
            Ui.CloseOverlay();
            return true;
        }

        protected override VisualElement FirstFocusable() => _next != null && _next.style.display != DisplayStyle.None ? _next : base.FirstFocusable();
    }
}
