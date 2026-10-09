using System;
using System.Collections.Generic;
using JamKit.Shell;
using UnityEngine;
using UnityEngine.UIElements;

namespace JamKit.Narrative
{
    /// <summary>
    /// Draws the <see cref="StageState"/>: backgrounds (crossfade, cut, flash, through black),
    /// characters with layered expressions (fade, slide, move, dim when someone else speaks), CGs,
    /// and the camera effects (shake, flash, fade, zoom, blur, colour filters, glitch), plus the
    /// chapter card, fake pop-up and fake freeze. All UI Toolkit, inside a 16:9 frame that letterboxes
    /// on other screen shapes. Built in code; its look is in Templates/Story/UI/Story.uss.
    /// </summary>
    public sealed class StoryStage
    {
        sealed class CharView
        {
            public string id;
            public VisualElement root, baseLayer, exprLayer;
            public CharacterDef def;
            public float aspect = 0.66f;
            public float x;
            public bool flip, leaving;
        }

        public readonly VisualElement Root;
        /// <summary>The 16:9 area: hotspots and the text box are children of this.</summary>
        public readonly VisualElement Frame;
        readonly VisualElement _camera, _bgA, _bgB, _chars, _cg, _cgB, _fade, _flash, _glitch, _chapter, _popup, _freeze, _ui;
        readonly Label _chapterLabel, _popupTitle, _popupText, _freezeTitle;
        readonly Button _popupOk;
        readonly List<VisualElement> _glitchBars = new List<VisualElement>();
        readonly Dictionary<string, CharView> _views = new Dictionary<string, CharView>(StringComparer.OrdinalIgnoreCase);
        readonly Tweens _tweens = new Tweens();
        readonly StoryConfig _config;
        readonly System.Random _rng = new System.Random(7);

        string _shownBg = "", _shownCg = "", _speaker = "";
        float _frameW = 1920f, _frameH = 1080f;
        float _shake, _shakeTime, _shakeStrength;
        float _glitchLeft, _glitchStrength, _glitchClock;
        float _freezeLeft;
        bool _popupOpen;
        float _zoom = 1f, _zoomX = 0.5f, _zoomY = 0.5f, _blur;
        string _filter = "";
        float _filterAmount = 1f;

        /// <summary>The next line waits while true (a transition, the chapter card, a pop-up, the fake freeze).</summary>
        public bool Busy => _tweens.Busy || _popupOpen || _freezeLeft > 0f;
        public bool PopupOpen => _popupOpen;
        public bool Frozen => _freezeLeft > 0f;
        public bool Glitching => _glitchLeft > 0f;
        public float GlitchStrength => _glitchLeft > 0f ? _glitchStrength : 0f;
        public string ShownBackground => _shownBg;
        public string ShownCg => _shownCg;
        public int CharacterCount { get { int n = 0; foreach (var v in _views.Values) if (!v.leaving) n++; return n; } }
        public float FrameWidth => _frameW;
        public float FrameHeight => _frameH;
        /// <summary>For the text box, hotspots and the inventory (inside the frame, above the picture and effects).</summary>
        public VisualElement UiLayer => _ui;

        public bool HasCharacter(string id) => _views.TryGetValue(id ?? "", out var v) && !v.leaving;

        public StoryStage(VisualElement parent, StoryConfig config, StyleSheet style)
        {
            _config = config;
            Root = El("st-root", null);
            Root.name = "story-stage";
            Root.pickingMode = PickingMode.Position;
            if (style != null) Root.styleSheets.Add(style);
            parent.Insert(0, Root);

            Frame = El("st-frame", Root);
            _camera = El("st-camera", Frame);
            _bgA = El("st-bg", _camera);
            _bgB = El("st-bg", _camera);
            _bgB.style.opacity = 0f;
            _chars = El("st-chars", _camera);
            _cg = El("st-cg", _camera);
            _cgB = El("st-cg", _camera);
            Hide(_cg);
            Hide(_cgB);
            // Fade and glitch cover the picture but not the text (a line can be read on a black screen);
            // the flash and the cards go over everything.
            _fade = El("st-fade", Frame);
            _fade.style.opacity = 0f;
            _glitch = El("st-glitch", Frame);
            for (int i = 0; i < 7; i++) _glitchBars.Add(El("st-glitch-bar", _glitch));
            Hide(_glitch);
            _ui = El("st-ui", Frame);
            _flash = El("st-flash", Frame);
            _flash.style.opacity = 0f;
            // Presenter mode's Hide HUD/UI keeps the picture and its effects, and hides the text box,
            // cards and pop-ups.
            _camera.AddToClassList(UiHider.KeepClass);
            _fade.AddToClassList(UiHider.KeepClass);
            _glitch.AddToClassList(UiHider.KeepClass);
            _flash.AddToClassList(UiHider.KeepClass);

            _chapter = El("st-chapter", Frame);
            _chapterLabel = new Label { name = "chapter-title" };
            _chapterLabel.AddToClassList("st-chapter-title");
            _chapter.Add(_chapterLabel);
            _chapter.style.opacity = 0f;
            Hide(_chapter);

            _freeze = El("st-freeze", Frame);
            var freezeBar = El("st-freeze-bar", _freeze);
            _freezeTitle = new Label();
            _freezeTitle.AddToClassList("st-freeze-title");
            freezeBar.Add(_freezeTitle);
            Hide(_freeze);

            _popup = El("st-popup-scrim", Frame);
            var win = El("st-popup", _popup);
            var bar = El("st-popup-bar", win);
            _popupTitle = new Label();
            _popupTitle.AddToClassList("st-popup-title");
            bar.Add(_popupTitle);
            _popupText = new Label();
            _popupText.AddToClassList("st-popup-text");
            win.Add(_popupText);
            _popupOk = new Button(ClosePopup) { name = "popup-ok", text = "OK" };
            _popupOk.AddToClassList("st-popup-button");
            win.Add(_popupOk);
            Hide(_popup);

            Root.RegisterCallback<GeometryChangedEvent>(_ => Layout());
        }

        static VisualElement El(string cls, VisualElement parent)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            e.AddToClassList(cls);
            parent?.Add(e);
            return e;
        }

        static void Hide(VisualElement e) => e.style.display = DisplayStyle.None;
        static void Show(VisualElement e) => e.style.display = DisplayStyle.Flex;

        public void Remove() => Root.RemoveFromHierarchy();

        // ---------- Layout ----------

        void Layout()
        {
            float w = Root.resolvedStyle.width, h = Root.resolvedStyle.height;
            if (float.IsNaN(w) || float.IsNaN(h) || w < 1f || h < 1f) return;
            float fw = Mathf.Min(w, h * 16f / 9f);
            float fh = fw * 9f / 16f;
            Frame.style.width = fw;
            Frame.style.height = fh;
            Frame.style.left = (w - fw) * 0.5f;
            Frame.style.top = (h - fh) * 0.5f;
            _frameW = fw;
            _frameH = fh;
            foreach (var v in _views.Values) PlaceSize(v);
        }

        // ---------- Whole state (loading a save, starting) ----------

        /// <summary>Draws a state at once, no transitions (loading a save).</summary>
        public void ShowState(StageState s)
        {
            _tweens.CompleteAll();
            SetBackgroundNow(s.background);
            foreach (var v in _views.Values) v.root.RemoveFromHierarchy();
            _views.Clear();
            foreach (var c in s.characters) ShowCharacterNow(c);
            SetCgNow(s.cg);
            _zoom = s.zoom; _zoomX = s.zoomX; _zoomY = s.zoomY;
            ApplyZoom(_zoom);
            _blur = s.blur;
            _filter = s.filter ?? "";
            _filterAmount = s.filterAmount;
            ApplyFilters();
            _fade.style.backgroundColor = ColourOf(s.fadeColor, Color.black);
            _fade.style.opacity = s.fadedOut ? 1f : 0f;
            _flash.style.opacity = 0f;
            _glitchLeft = 0f;
            Hide(_glitch);
            _freezeLeft = 0f;
            Hide(_freeze);
            ClosePopup();
            Hide(_chapter);
            _speaker = "";
            Dim();
        }

        // ---------- Commands ----------

        /// <summary>Plays one stage command (the state already has it applied). Instant when <paramref name="instant"/>.</summary>
        public void Play(StoryCommand cmd, StageState state, bool instant)
        {
            switch (cmd.kind)
            {
                case StoryCommandKind.Background:
                    Background(cmd.id, cmd.transition, cmd.SecondsOr(DefaultSeconds(cmd.transition)), instant);
                    break;
                case StoryCommandKind.Scene:
                    foreach (var v in new List<string>(_views.Keys)) HideCharacter(v, StageTransition.Fade, 0.3f, instant);
                    if (_shownCg.Length > 0) Cg("", StageTransition.Fade, 0.3f, instant);
                    Background(cmd.id, cmd.transition == StageTransition.Default ? StageTransition.Black : cmd.transition, cmd.SecondsOr(1.2f), instant);
                    break;
                case StoryCommandKind.Show:
                {
                    var c = state.Find(cmd.id);
                    if (c != null) ShowCharacter(c, cmd.transition, cmd.SecondsOr(cmd.transition == StageTransition.Slide ? 0.6f : 0.35f), instant);
                    break;
                }
                case StoryCommandKind.Hide:
                    HideCharacter(cmd.id, cmd.transition, cmd.SecondsOr(0.35f), instant);
                    break;
                case StoryCommandKind.HideAll:
                    foreach (var v in new List<string>(_views.Keys)) HideCharacter(v, cmd.transition, cmd.SecondsOr(0.35f), instant);
                    break;
                case StoryCommandKind.Move:
                {
                    var c = state.Find(cmd.id);
                    if (c != null) MoveCharacter(c, cmd.SecondsOr(0.6f), instant);
                    break;
                }
                case StoryCommandKind.Cg:
                    Cg(cmd.id, cmd.transition, cmd.SecondsOr(0.8f), instant);
                    break;
                case StoryCommandKind.CgOff:
                    Cg("", cmd.transition, cmd.SecondsOr(0.6f), instant);
                    break;
                case StoryCommandKind.Shake:
                    Shake(cmd.AmountOr(0.5f), cmd.SecondsOr(0.5f));
                    break;
                case StoryCommandKind.Flash:
                    Flash(ColourOf(cmd.text, Color.white), cmd.SecondsOr(0.35f));
                    break;
                case StoryCommandKind.Fade:
                    Fade(cmd.id == "out", ColourOf(cmd.text, ColourOf(state.fadeColor, Color.black)), cmd.SecondsOr(1f), instant);
                    break;
                case StoryCommandKind.Zoom:
                    Zoom(state.zoom, state.zoomX, state.zoomY, cmd.SecondsOr(0.8f), instant);
                    break;
                case StoryCommandKind.Blur:
                    BlurTo(state.blur, cmd.SecondsOr(0.6f), instant);
                    break;
                case StoryCommandKind.Filter:
                    _filter = state.filter ?? "";
                    FilterTo(state.filterAmount, cmd.SecondsOr(0.6f), instant);
                    break;
                case StoryCommandKind.Glitch:
                    Glitch(cmd.AmountOr(0.6f), cmd.SecondsOr(0.8f));
                    break;
            }
        }

        static float DefaultSeconds(StageTransition t)
        {
            switch (t)
            {
                case StageTransition.Cut: return 0f;
                case StageTransition.Flash: return 0.5f;
                case StageTransition.Black: return 1.2f;
                default: return 0.8f;
            }
        }

        /// <summary>Skips every running transition to its end (the player pressed to go on).</summary>
        public void Hurry()
        {
            _tweens.CompleteAll();
            if (_popupOpen) ClosePopup();
        }

        // ---------- Background ----------

        void Background(string id, StageTransition t, float seconds, bool instant)
        {
            if (instant || t == StageTransition.Cut || seconds <= 0f) { SetBackgroundNow(id); return; }
            switch (t)
            {
                case StageTransition.Flash:
                    _flash.style.backgroundColor = Color.white;
                    SetBackgroundNow(id);
                    _tweens.Start("flash", seconds, k => _flash.style.opacity = 1f - k, null, true);
                    break;
                case StageTransition.Black:
                {
                    var oldFade = _fade.resolvedStyle.opacity;
                    _fade.style.backgroundColor = Color.black;
                    _tweens.Start("bg", seconds * 0.5f, k => _fade.style.opacity = Mathf.Max(oldFade, k), () =>
                    {
                        SetBackgroundNow(id);
                        _tweens.Start("bg2", seconds * 0.5f, k => _fade.style.opacity = 1f - k, null, true);
                    }, true);
                    break;
                }
                default:
                    // bgB sits just above bgA: fade it in, then copy it down and hide it again.
                    Paint(_bgB, BackgroundSprite(id), BackgroundColour(id));
                    _bgB.style.opacity = 0f;
                    _shownBg = id ?? "";
                    _tweens.Start("bg", seconds, k => _bgB.style.opacity = k, () =>
                    {
                        Paint(_bgA, BackgroundSprite(id), BackgroundColour(id));
                        _bgB.style.opacity = 0f;
                    }, true);
                    break;
            }
        }

        void SetBackgroundNow(string id)
        {
            _tweens.Finish("bg");
            _shownBg = id ?? "";
            Paint(_bgA, BackgroundSprite(id), BackgroundColour(id));
            _bgB.style.opacity = 0f;
        }

        Sprite BackgroundSprite(string id) => _config != null ? _config.FindBackground(id)?.sprite : null;

        Color BackgroundColour(string id)
        {
            if (string.IsNullOrEmpty(id)) return Color.black;
            var b = _config != null ? _config.FindBackground(id) : null;
            return b != null ? b.color : new Color(0.15f, 0.15f, 0.18f);
        }

        static void Paint(VisualElement e, Sprite sprite, Color colour)
        {
            if (sprite != null)
            {
                e.style.backgroundImage = new StyleBackground(sprite);
                e.style.backgroundColor = Color.black;
            }
            else
            {
                e.style.backgroundImage = StyleKeyword.None;
                e.style.backgroundColor = colour;
            }
        }

        // ---------- Characters ----------

        CharView MakeView(string id)
        {
            var def = _config != null ? _config.FindCharacter(id) : null;
            var v = new CharView { id = id, def = def };
            v.root = El("st-char", _chars);
            v.root.name = "char-" + id;
            v.baseLayer = El("st-char-layer", v.root);
            v.exprLayer = El("st-char-layer", v.root);
            var baseSprite = def != null && def.portrait != null ? def.portrait.baseSprite : null;
            if (baseSprite != null)
            {
                v.baseLayer.style.backgroundImage = new StyleBackground(baseSprite);
                var r = baseSprite.rect;
                if (r.height > 0f) v.aspect = r.width / r.height;
            }
            else
            {
                v.baseLayer.style.backgroundColor = new Color(0.5f, 0.5f, 0.55f, 0.8f);
            }
            _views[id] = v;
            return v;
        }

        void SetExpression(CharView v, string expression)
        {
            var portrait = v.def != null ? v.def.portrait : null;
            var sprite = portrait != null ? portrait.FindExpression(expression) : null;
            if (sprite != null)
            {
                v.exprLayer.style.backgroundImage = new StyleBackground(sprite);
                Show(v.exprLayer);
            }
            else
            {
                v.exprLayer.style.backgroundImage = StyleKeyword.None;
                Hide(v.exprLayer);
            }
        }

        void PlaceSize(CharView v)
        {
            float hFrac = v.def != null ? v.def.height : 0.9f;
            float h = _frameH * hFrac;
            float w = h * v.aspect;
            v.root.style.height = h;
            v.root.style.width = w;
            v.root.style.bottom = (v.def != null ? v.def.yOffset : 0f) * _frameH;
            v.root.style.marginLeft = -w * 0.5f;
        }

        void PlaceX(CharView v, float x)
        {
            v.x = x;
            v.root.style.left = Length.Percent(x * 100f);
        }

        void SetFlip(CharView v, bool flip)
        {
            v.flip = flip;
            v.root.style.scale = new Scale(new Vector3(flip ? -1f : 1f, 1f, 1f));
        }

        void ShowCharacterNow(StageCharacter c)
        {
            var v = MakeView(c.id);
            PlaceSize(v);
            PlaceX(v, c.x);
            SetFlip(v, c.flip);
            SetExpression(v, c.expression);
            v.root.style.opacity = 1f;
        }

        void ShowCharacter(StageCharacter c, StageTransition t, float seconds, bool instant)
        {
            if (_views.TryGetValue(c.id, out var v) && !v.leaving)
            {
                SetExpression(v, c.expression);
                SetFlip(v, c.flip);
                if (!Mathf.Approximately(v.x, c.x)) MoveCharacter(c, seconds, instant);
                Dim();
                return;
            }
            if (v != null) { _tweens.Finish("char-" + c.id); }
            v = MakeView(c.id);
            PlaceSize(v);
            SetFlip(v, c.flip);
            SetExpression(v, c.expression);
            if (instant || t == StageTransition.Cut || seconds <= 0f)
            {
                PlaceX(v, c.x);
                v.root.style.opacity = 1f;
            }
            else if (t == StageTransition.Slide)
            {
                float from = c.x < 0.5f ? -0.3f : 1.3f;
                var view = v;
                _tweens.Start("char-" + c.id, seconds, k => { PlaceX(view, Mathf.Lerp(from, c.x, k)); view.root.style.opacity = 1f; }, null, true);
            }
            else
            {
                PlaceX(v, c.x);
                var view = v;
                _tweens.Start("char-" + c.id, seconds, k => view.root.style.opacity = k, null, false);
            }
            Dim();
        }

        void HideCharacter(string id, StageTransition t, float seconds, bool instant)
        {
            if (!_views.TryGetValue(id ?? "", out var v)) return;
            _tweens.Finish("char-" + v.id);
            if (instant || t == StageTransition.Cut || seconds <= 0f)
            {
                v.root.RemoveFromHierarchy();
                _views.Remove(v.id);
                return;
            }
            v.leaving = true;
            var view = v;
            if (t == StageTransition.Slide)
            {
                float from = v.x, to = v.x < 0.5f ? -0.3f : 1.3f;
                _tweens.Start("char-" + v.id, seconds, k => PlaceX(view, Mathf.Lerp(from, to, k)), () => Drop(view), true);
            }
            else
            {
                _tweens.Start("char-" + v.id, seconds, k => view.root.style.opacity = 1f - k, () => Drop(view), false);
            }
        }

        void Drop(CharView v)
        {
            v.root.RemoveFromHierarchy();
            if (_views.TryGetValue(v.id, out var cur) && cur == v) _views.Remove(v.id);
        }

        void MoveCharacter(StageCharacter c, float seconds, bool instant)
        {
            if (!_views.TryGetValue(c.id, out var v)) return;
            SetFlip(v, c.flip);
            if (instant || seconds <= 0f) { PlaceX(v, c.x); return; }
            float from = v.x;
            var view = v;
            _tweens.Start("move-" + c.id, seconds, k => PlaceX(view, Mathf.Lerp(from, c.x, k)), null, false);
        }

        /// <summary>Who is speaking (a character id or display name): the others dim a little.</summary>
        public void SetSpeaker(string idOrName)
        {
            _speaker = idOrName ?? "";
            Dim();
        }

        void Dim()
        {
            var speaking = _config != null ? _config.FindCharacter(_speaker) : null;
            foreach (var v in _views.Values)
            {
                bool dim = speaking != null && !string.Equals(v.id, speaking.id, StringComparison.OrdinalIgnoreCase);
                v.root.EnableInClassList("st-char--dim", dim);
            }
        }

        // ---------- CG ----------

        void Cg(string id, StageTransition t, float seconds, bool instant)
        {
            id = id ?? "";
            if (instant || t == StageTransition.Cut || seconds <= 0f) { SetCgNow(id); return; }
            if (id.Length == 0)
            {
                _shownCg = "";
                _tweens.Start("cg", seconds, k => _cg.style.opacity = 1f - k, () => Hide(_cg), false);
                return;
            }
            var def = _config != null ? _config.FindCg(id) : null;
            Paint(_cgB, def != null ? def.sprite : null, def != null ? def.color : Color.gray);
            Show(_cgB);
            _cgB.BringToFront();
            _cgB.style.opacity = 0f;
            _shownCg = id;
            _tweens.Start("cg", seconds, k => _cgB.style.opacity = k, () =>
            {
                Paint(_cg, def != null ? def.sprite : null, def != null ? def.color : Color.gray);
                Show(_cg);
                _cg.style.opacity = 1f;
                _cg.BringToFront();
                Hide(_cgB);
            }, true);
        }

        void SetCgNow(string id)
        {
            _tweens.Finish("cg");
            _shownCg = id ?? "";
            Hide(_cgB);
            if (_shownCg.Length == 0) { Hide(_cg); return; }
            var def = _config != null ? _config.FindCg(id) : null;
            Paint(_cg, def != null ? def.sprite : null, def != null ? def.color : Color.gray);
            Show(_cg);
            _cg.style.opacity = 1f;
        }

        // ---------- Camera effects ----------

        public void Shake(float strength, float seconds)
        {
            float scale = GameRoot.Exists ? GameRoot.Settings.screenShake : 1f;
            _shakeStrength = Mathf.Clamp01(strength) * scale;
            _shakeTime = Mathf.Max(0.05f, seconds);
            _shake = _shakeTime;
        }

        public void Flash(Color c, float seconds)
        {
            _flash.style.backgroundColor = new Color(c.r, c.g, c.b, 1f);
            float a = Mathf.Clamp01(c.a) * 0.85f;
            _tweens.Start("flash", Mathf.Max(0.05f, seconds), k => _flash.style.opacity = a * (1f - k), null, false);
        }

        void Fade(bool @out, Color c, float seconds, bool instant)
        {
            _fade.style.backgroundColor = c;
            float from = _fade.resolvedStyle.opacity;
            if (float.IsNaN(from)) from = @out ? 0f : 1f;
            float to = @out ? 1f : 0f;
            if (instant || seconds <= 0f) { _fade.style.opacity = to; return; }
            _tweens.Start("fade", seconds, k => _fade.style.opacity = Mathf.Lerp(from, to, k), null, true);
        }

        void Zoom(float scale, float x, float y, float seconds, bool instant)
        {
            float from = _zoom;
            _camera.style.transformOrigin = new TransformOrigin(Length.Percent(x * 100f), Length.Percent(y * 100f));
            _zoomX = x; _zoomY = y;
            if (instant || seconds <= 0f) { _zoom = scale; ApplyZoom(scale); return; }
            _tweens.Start("zoom", seconds, k => { _zoom = Mathf.Lerp(from, scale, k); ApplyZoom(_zoom); }, null, false);
        }

        void ApplyZoom(float z)
        {
            _camera.style.transformOrigin = new TransformOrigin(Length.Percent(_zoomX * 100f), Length.Percent(_zoomY * 100f));
            _camera.style.scale = new Scale(new Vector3(z, z, 1f));
        }

        void BlurTo(float px, float seconds, bool instant)
        {
            float from = _blur;
            if (instant || seconds <= 0f) { _blur = px; ApplyFilters(); return; }
            _tweens.Start("blur", seconds, k => { _blur = Mathf.Lerp(from, px, k); ApplyFilters(); }, null, false);
        }

        void FilterTo(float amount, float seconds, bool instant)
        {
            if (instant || seconds <= 0f) { _filterAmount = amount; ApplyFilters(); return; }
            _tweens.Start("filter", seconds, k => { _filterAmount = amount * k; ApplyFilters(); }, null, false);
        }

        readonly List<FilterFunction> _filters = new List<FilterFunction>();
        bool _filterSet;

        static bool _filtersEnabled = !Application.isBatchMode;

        /// <summary>UI Toolkit filters (blur, colour filters) are optional polish: off in batch mode (nothing
        /// is drawn there), and switched off for good if setting one ever fails. The rest of the stage
        /// works the same without them.</summary>
        public static bool FiltersEnabled
        {
            get => _filtersEnabled;
            set => _filtersEnabled = value;
        }

        /// <summary>Blur and the colour filter as UI Toolkit filters on the picture (not on the text).</summary>
        void ApplyFilters()
        {
            if (!_filtersEnabled) return;
            _filters.Clear();
            float a = Mathf.Clamp01(_filterAmount);
            if (_blur > 0.05f) _filters.Add(F(FilterFunctionType.Blur, _blur));
            switch (_filter)
            {
                case "sepia": _filters.Add(F(FilterFunctionType.Sepia, a)); break;
                case "gray": _filters.Add(F(FilterFunctionType.Grayscale, a)); break;
                case "invert": _filters.Add(F(FilterFunctionType.Invert, a)); break;
                case "night":
                    _filters.Add(FC(FilterFunctionType.Tint, Color.Lerp(Color.white, new Color(0.55f, 0.65f, 1f), a)));
                    _filters.Add(F(FilterFunctionType.Contrast, 1f + 0.15f * a));
                    break;
                case "dream":
                    _filters.Add(F(FilterFunctionType.Sepia, 0.3f * a));
                    _filters.Add(F(FilterFunctionType.Contrast, 1f - 0.2f * a));
                    if (_blur <= 0.05f) _filters.Add(F(FilterFunctionType.Blur, 1.5f * a));
                    break;
            }
            try
            {
                if (_filters.Count == 0)
                {
                    // Never the None keyword: Unity 6.3 then fails reading resolvedStyle.filter on every
                    // layout. Clearing the inline value (Null) leaves the default, an empty list.
                    if (_filterSet) _camera.style.filter = StyleKeyword.Null;
                    _filterSet = false;
                }
                else
                {
                    _camera.style.filter = new StyleList<FilterFunction>(new List<FilterFunction>(_filters));
                    _filterSet = true;
                }
            }
            catch (Exception)
            {
                // A platform without UI filters: the picture just stays sharp and uncoloured.
                _filtersEnabled = false;
            }
        }

        static FilterFunction F(FilterFunctionType type, float value)
        {
            var f = new FilterFunction(type);
            f.AddParameter(new FilterParameter(value));
            return f;
        }

        static FilterFunction FC(FilterFunctionType type, Color c)
        {
            var f = new FilterFunction(type);
            f.AddParameter(new FilterParameter(c));
            return f;
        }

        public void Glitch(float strength, float seconds)
        {
            _glitchStrength = Mathf.Clamp(strength, 0.05f, 1f);
            _glitchLeft = Mathf.Max(0.1f, seconds);
            _glitchClock = 0f;
            Show(_glitch);
        }

        // ---------- Cards and fourth-wall overlays ----------

        /// <summary>A chapter title over the picture: in, hold, out (the next line waits for it).</summary>
        public void Chapter(string title, float hold = 1.6f)
        {
            _chapterLabel.text = title ?? "";
            Show(_chapter);
            _chapter.BringToFront();
            _tweens.Start("chapter", 0.5f, k => _chapter.style.opacity = k, () =>
                _tweens.Start("chapter", hold, null, () =>
                    _tweens.Start("chapter", 0.6f, k => _chapter.style.opacity = 1f - k, () => Hide(_chapter), true), true), true);
        }

        public bool ChapterShown => _chapter.style.display == DisplayStyle.Flex;

        /// <summary>A system-style message box the player must close (#meta:popup).</summary>
        public void Popup(string title, string text)
        {
            _popupTitle.text = title ?? "";
            _popupText.text = text ?? "";
            Show(_popup);
            _popup.BringToFront();
            _popupOpen = true;
            _popup.pickingMode = PickingMode.Position;
            _popupOk.Focus();
            _popupOk.schedule.Execute(() => { if (_popupOpen) _popupOk.Focus(); });
        }

        public void ClosePopup()
        {
            if (!_popupOpen && _popup.style.display == DisplayStyle.None) return;
            _popupOpen = false;
            Hide(_popup);
            if (GameRoot.Exists) GameRoot.Audio.Play("click");
        }

        /// <summary>The game pretends to hang for a moment (#meta:fakecrash): a washed-out screen and
        /// "(Not Responding)" in a title bar. Input waits until it "recovers".</summary>
        public void FakeFreeze(string windowTitle, float seconds)
        {
            _freezeTitle.text = (windowTitle ?? "") + " (Not Responding)";
            Show(_freeze);
            _freeze.BringToFront();
            _freezeLeft = Mathf.Max(0.3f, seconds);
        }

        // ---------- Frame ----------

        public void Tick(float dt)
        {
            _tweens.Tick(dt);
            Vector2 offset = Vector2.zero;
            if (_shake > 0f)
            {
                _shake -= dt;
                float k = Mathf.Clamp01(_shake / _shakeTime);
                float amp = _shakeStrength * 28f * k * k;
                offset += new Vector2((float)(_rng.NextDouble() * 2 - 1) * amp, (float)(_rng.NextDouble() * 2 - 1) * amp);
            }
            if (_glitchLeft > 0f)
            {
                _glitchLeft -= dt;
                _glitchClock -= dt;
                if (_glitchClock <= 0f)
                {
                    _glitchClock = 0.05f;
                    RandomizeGlitch();
                }
                offset.x += (float)(_rng.NextDouble() * 2 - 1) * 18f * _glitchStrength;
                if (_glitchLeft <= 0f) Hide(_glitch);
            }
            _camera.style.translate = new Translate(offset.x, offset.y);
            if (_freezeLeft > 0f)
            {
                _freezeLeft -= dt;
                if (_freezeLeft <= 0f) Hide(_freeze);
            }
        }

        void RandomizeGlitch()
        {
            for (int i = 0; i < _glitchBars.Count; i++)
            {
                var bar = _glitchBars[i];
                bool on = _rng.NextDouble() < 0.45 + 0.5 * _glitchStrength;
                bar.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
                if (!on) continue;
                bar.style.top = Length.Percent((float)_rng.NextDouble() * 95f);
                bar.style.height = Length.Percent(1f + (float)_rng.NextDouble() * 7f * _glitchStrength);
                bar.style.left = Length.Percent((float)(_rng.NextDouble() * 20 - 10));
                bar.style.right = Length.Percent((float)(_rng.NextDouble() * 20 - 10));
                int pick = _rng.Next(4);
                Color c = pick == 0 ? new Color(1f, 0.1f, 0.4f) : pick == 1 ? new Color(0.1f, 0.9f, 1f) : pick == 2 ? Color.white : Color.black;
                c.a = 0.25f + 0.5f * _glitchStrength * (float)_rng.NextDouble();
                bar.style.backgroundColor = c;
            }
        }

        // ---------- Colours ----------

        /// <summary>A colour word (white, black, red...), a palette name, or <paramref name="fallback"/>.</summary>
        public Color ColourOf(string word, Color fallback)
        {
            if (string.IsNullOrEmpty(word)) return fallback;
            if (_config != null && _config.TryPalette(word, out var p)) return p;
            switch (word.ToLowerInvariant())
            {
                case "white": return Color.white;
                case "black": return Color.black;
                case "red": return new Color(0.85f, 0.1f, 0.12f);
                case "green": return new Color(0.2f, 0.75f, 0.3f);
                case "blue": return new Color(0.2f, 0.35f, 0.9f);
                case "yellow": return new Color(1f, 0.85f, 0.25f);
                case "orange": return new Color(1f, 0.55f, 0.15f);
                case "purple": return new Color(0.55f, 0.25f, 0.75f);
                case "gray": case "grey": return new Color(0.5f, 0.5f, 0.5f);
                case "pink": return new Color(1f, 0.55f, 0.75f);
                case "cyan": return new Color(0.3f, 0.9f, 0.95f);
            }
            return ColorUtility.TryParseHtmlString(word, out var c) ? c : fallback;
        }

        /// <summary>For tests: the stage's camera scale now.</summary>
        public float Zoom01 => _zoom;
        public float Blur => _blur;
    }
}
