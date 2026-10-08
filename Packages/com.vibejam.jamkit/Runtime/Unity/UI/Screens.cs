using System;
using System.Collections.Generic;
using JamKit.Flow;
using UnityEngine;
using UnityEngine.UIElements;

namespace JamKit.Shell
{
    public sealed class TitleScreen : ShellScreen
    {
        protected override void OnInit()
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
                SetShown(best, t > 0f);
            }
        }
    }

    public sealed class SettingsScreen : ShellScreen
    {
        Slider _master, _music, _sfx, _sensitivity, _shake;
        Toggle _invertY, _fullscreen, _pixelLook;
        Button _difficulty;
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
            s.label = s.name == "sensitivity"
                ? baseLabel + "   x" + s.value.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
                : baseLabel + "   " + Mathf.RoundToInt(s.value) + "%";
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
            SetShown(_pixelLook, GameRoot.Exists && GameRoot.Config.showPixelLookSetting);
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

        protected override void OnShow()
        {
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
