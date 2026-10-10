using System.Collections.Generic;
using JamKit.Flow;
using UnityEngine;
using UnityEngine.UIElements;

namespace JamKit.Shell
{
    /// <summary>
    /// All shell UI in one UIDocument, in layers (bottom to top): HUD, dialogue, menus, floating
    /// text, flash, fade, debug. Menus follow GameFlow (Title, Paused, GameOver, Victory, Credits);
    /// Settings and How to Play open on top as overlays and Back closes them.
    /// </summary>
    public sealed class ShellUI : MonoBehaviour
    {
        UIDocument _doc;
        VisualElement _root;
        VisualElement _hudLayer, _dialogueLayer, _screenLayer, _floatLayer, _flash, _fade, _debugLayer, _presenterLayer;

        public TitleScreen Title;
        public SettingsScreen Settings;
        public PauseScreen Pause;
        public ResultScreen GameOver;
        public ResultScreen Victory;
        public CreditsScreen Credits;
        public HowToPlayScreen HowToPlay;
        /// <summary>Settings > Controls: rebind keys and pad buttons.</summary>
        public ControlsScreen Controls;
        /// <summary>Play's Story / Normal / Hard picker (GameConfig.chooseDifficultyOnPlay).</summary>
        public DifficultyScreen DifficultyPicker;
        public HudView Hud;
        public DialogueView Dialogue;
        public DebugMenu DebugPanel;
        /// <summary>Presenter mode's panel (Ctrl+Shift+P, or hold L3+R3 on a pad): an overlay like Settings.</summary>
        public PresenterScreen Presenter;

        readonly UiHider _hider = new UiHider();

        ShellScreen _base;
        readonly List<ShellScreen> _overlays = new List<ShellScreen>();
        readonly List<ShellScreen> _all = new List<ShellScreen>();

        float _flashLeft, _flashDuration, _flashAlpha;

        struct Floater
        {
            public Label label;
            public Vector3 world;
            public float age;
            public float life;
        }

        readonly List<Floater> _floaters = new List<Floater>();
        readonly Stack<Label> _floatPool = new Stack<Label>();

        public VisualElement Root => _root;
        public IPanel Panel => _root != null ? _root.panel : null;
        /// <summary>The screen on top: an overlay, else the flow's screen, else null (gameplay).</summary>
        public ShellScreen Top => _overlays.Count > 0 ? _overlays[_overlays.Count - 1] : _base;

        public void Init(ShellAssets assets, Transform parent)
        {
            var go = new GameObject("Shell UI");
            go.SetActive(false);
            go.transform.SetParent(parent, false);
            _doc = go.AddComponent<UIDocument>();
            _doc.panelSettings = assets != null ? assets.panelSettings : null;
            if (_doc.panelSettings == null)
            {
                var ps = ScriptableObject.CreateInstance<PanelSettings>();
                ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                ps.referenceResolution = new Vector2Int(1920, 1080);
                ps.match = 0.5f;
                _doc.panelSettings = ps;
            }
            _doc.sortingOrder = 100;
            go.SetActive(true);

            var docRoot = _doc.rootVisualElement;
            _root = new VisualElement { name = "jamkit-root", pickingMode = PickingMode.Ignore };
            _root.AddToClassList("jk-root");
            _root.AddToClassList("jk-layer");
            if (assets != null)
                foreach (var sheet in assets.styleSheets)
                    if (sheet != null) _root.styleSheets.Add(sheet);
            docRoot.Add(_root);

            _hudLayer = Layer("hud-layer");
            _dialogueLayer = Layer("dialogue-layer");
            _screenLayer = Layer("screen-layer");
            _floatLayer = Layer("float-layer");
            _flash = Layer("flash");
            _flash.AddToClassList("jk-flash");
            _flash.style.display = DisplayStyle.None;
            _fade = Layer("fade");
            _fade.AddToClassList("jk-fade");
            _fade.style.display = DisplayStyle.None;
            _debugLayer = Layer("debug-layer");
            _presenterLayer = Layer("presenter-layer");

            Title = Add(new TitleScreen(), assets != null ? assets.title : null);
            Pause = Add(new PauseScreen(), assets != null ? assets.pause : null);
            GameOver = Add(new ResultScreen(false), assets != null ? assets.gameOver : null);
            Victory = Add(new ResultScreen(true), assets != null ? assets.victory : null);
            Credits = Add(new CreditsScreen(), assets != null ? assets.credits : null);
            Settings = Add(new SettingsScreen(), assets != null ? assets.settings : null);
            HowToPlay = Add(new HowToPlayScreen(), assets != null ? assets.howToPlay : null);
            DifficultyPicker = Add(new DifficultyScreen(), null);
            Controls = Add(new ControlsScreen(), null);

            Hud = new HudView(assets != null ? assets.hud : null, _hudLayer);
            Dialogue = new DialogueView(assets != null ? assets.dialogue : null, _dialogueLayer);
            DebugPanel = new DebugMenu(assets != null ? assets.debugMenu : null, _debugLayer);
            Presenter = Add(new PresenterScreen(), null);
            PresenterIndicator = new Label { name = "presenter-indicator", pickingMode = PickingMode.Ignore };
            PresenterIndicator.AddToClassList("jk-presenter-indicator");
            PresenterIndicator.style.display = DisplayStyle.None;
            _presenterLayer.Add(PresenterIndicator);
        }

        /// <summary>Presenter mode's small status line (only when its Indicator toggle is on).</summary>
        public Label PresenterIndicator;

        // ---------- Presenter: hide the UI ----------

        /// <summary>True while presenter mode hides the HUD, dialogue box, floating text and debug overlay
        /// (the picture stays: anything a template marks with UiHider.KeepClass, the flash and fade; menus
        /// such as Pause still show when opened).</summary>
        public bool UiHidden => _hider.Active;

        public void SetUiHidden(bool hidden)
        {
            if (hidden == _hider.Active) return;
            if (hidden)
            {
                _hiderRoots.Clear();
                _hiderRoots.Add(_hudLayer);
                _hiderRoots.Add(_dialogueLayer);
                _hiderRoots.Add(_floatLayer);
                _hiderRoots.Add(_debugLayer);
                _hider.Hide(_hiderRoots);
            }
            else _hider.Restore();
        }

        readonly List<VisualElement> _hiderRoots = new List<VisualElement>(5);

        VisualElement Layer(string name)
        {
            var e = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            e.AddToClassList("jk-layer");
            _root.Add(e);
            return e;
        }

        /// <summary>Adds a template's own screen (save/load, a gallery...). Open it with <see cref="OpenOverlay"/>;
        /// Back closes it like Settings. Built from <paramref name="tree"/> (UXML) or, with null, an empty layer
        /// the screen fills in its OnInit.</summary>
        public T AddScreen<T>(T screen, VisualTreeAsset tree) where T : ShellScreen => Add(screen, tree);

        T Add<T>(T screen, VisualTreeAsset tree) where T : ShellScreen
        {
            screen.Init(this, tree, _screenLayer);
            _all.Add(screen);
            return screen;
        }

        // ---------- Flow-driven screens ----------

        public void OnFlowChanged(GameState from, GameState to)
        {
            switch (to)
            {
                case GameState.Title: SetBase(Title); break;
                case GameState.Paused: SetBase(Pause); break;
                case GameState.GameOver: SetBase(GameOver); break;
                case GameState.Victory: SetBase(Victory); break;
                case GameState.Credits: SetBase(Credits); break;
                default: SetBase(null); break;
            }
        }

        void SetBase(ShellScreen screen)
        {
            CloseAllOverlays();
            if (_base == screen) { if (screen != null && !screen.IsVisible) screen.Show(); return; }
            if (_base != null) _base.Hide();
            _base = screen;
            if (screen != null) screen.Show();
            else _root.focusController?.focusedElement?.Blur();
        }

        public void OpenSettings() => OpenOverlay(Settings);

        /// <summary>Opens the Controls screen (rebinding) on top of the current one; Back returns.</summary>
        public void OpenControls() => OpenOverlay(Controls);

        /// <summary>Opens Play's difficulty picker (choosing a card starts the run; Back closes it).</summary>
        public void OpenDifficultyPicker() => OpenOverlay(DifficultyPicker);

        public void OpenHowToPlay(System.Action onClosed)
        {
            HowToPlay.Open(onClosed);
            OpenOverlay(HowToPlay);
        }

        /// <summary>Opens a screen on top of the current one (Back/Escape closes it).</summary>
        public void OpenOverlay(ShellScreen screen)
        {
            if (_overlays.Contains(screen)) return;
            var below = Top;
            if (below != null) below.Hide();
            _overlays.Add(screen);
            screen.Show();
        }

        /// <summary>Closes the top overlay and gives focus back to the screen below.</summary>
        public void CloseOverlay() => CloseOverlay(true);

        /// <summary>Closes the top overlay; <paramref name="backSound"/> false when it closes because
        /// something was chosen (the select sound already played).</summary>
        public void CloseOverlay(bool backSound)
        {
            if (_overlays.Count == 0) return;
            var top = _overlays[_overlays.Count - 1];
            _overlays.RemoveAt(_overlays.Count - 1);
            top.Hide();
            if (backSound) PlayUiSound("ui_back");
            var below = Top;
            if (below != null)
            {
                below.Root.style.display = DisplayStyle.Flex;
                below.RestoreFocus();
            }
        }

        /// <summary>Closes every overlay (Settings, How to Play, a template's screens, the presenter panel)
        /// and shows the flow's screen under them again (the title, the pause menu), if any.</summary>
        public void CloseOverlays()
        {
            if (_overlays.Count == 0) return;
            CloseAllOverlays();
            if (_base == null) return;
            _base.Root.style.display = DisplayStyle.Flex;
            _base.RestoreFocus();
        }

        void CloseAllOverlays()
        {
            for (int i = _overlays.Count - 1; i >= 0; i--) _overlays[i].Hide();
            _overlays.Clear();
        }

        public bool IsOverlayOpen(ShellScreen s) => _overlays.Contains(s);

        // ---------- Per frame ----------

        void Update()
        {
            if (!GameRoot.Exists || _root == null) return;
            var game = GameRoot.Instance;
            float dt = Time.unscaledDeltaTime;
            var input = GameRoot.Input;

            DebugPanel.Tick(dt);
            if (!game.SceneService.IsLoading && !DebugPanel.IsOpen)
            {
                bool back = input.Cancel.WasPressedThisFrame();
                bool pause = input.Pause.WasPressedThisFrame();
                // Ctrl+Shift+P is the presenter chord, not a pause; Escape during attract mode stops it.
                if (pause && PresenterMode.ChordKeysDown) pause = false;
                if ((back || pause) && PresenterMode.AttractRunning && !IsOverlayOpen(Presenter))
                {
                    PresenterMode.StopAttract();
                    back = pause = false;
                }
                if (back || pause) HandleBack(back, pause);
                // A mouse click on empty space can drop focus; any keyboard or pad press brings it back.
                var top = Top;
                if (top != null && (input.Navigate.WasPressedThisFrame() || input.Submit.WasPressedThisFrame()))
                {
                    var focused = _root.focusController?.focusedElement as VisualElement;
                    if (focused == null || focused.panel == null || !IsInside(focused, top.Root)) top.FocusFirst();
                }
            }

            var t = Top;
            if (t != null) t.Tick(dt);
            Hud.Tick(dt, GameRoot.Flow.InRun && !game.SceneService.IsLoading);
            TickFlash(dt);
            TickFloaters(dt);
            _hider.Tick(dt);
        }

        static bool IsInside(VisualElement e, VisualElement ancestor)
        {
            for (var p = e; p != null; p = p.parent)
                if (p == ancestor) return true;
            return false;
        }

        /// <summary>What Cancel (Escape, B) does right now: close an overlay, resume, leave credits.</summary>
        public void Back() => HandleBack(true, false);

        /// <summary>What Pause (Escape, P, Start) does right now: pause, resume or close an overlay.</summary>
        public void PressPause() => HandleBack(false, true);

        void HandleBack(bool back, bool pause)
        {
            var flow = GameRoot.Flow;
            if (_overlays.Count > 0)
            {
                var top = _overlays[_overlays.Count - 1];
                if (!top.OnBack()) CloseOverlay();
                return;
            }
            if (flow.Current == GameState.Paused) { GameRoot.Instance.Resume(); return; }
            if (pause && (flow.Current == GameState.Playing || flow.Current == GameState.Dialogue)) { GameRoot.Instance.Pause(); return; }
            if (back && _base != null) _base.OnBack();
        }

        public void PlayUiSound(string id)
        {
            if (GameRoot.Exists) GameRoot.Audio.Play(id);
        }

        // ---------- Fade ----------

        float _fadeAlpha;

        public void SetFade(float alpha)
        {
            alpha = Mathf.Clamp01(alpha);
            _fadeAlpha = alpha;
            _fade.style.opacity = alpha;
            _fade.style.display = alpha > 0.001f ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public float FadeAlpha => _fadeAlpha;

        /// <summary>While a scene loads the fade layer swallows clicks.</summary>
        public void SetFadeBlocking(bool blocking) => _fade.pickingMode = blocking ? PickingMode.Position : PickingMode.Ignore;

        // ---------- Flash ----------

        public void Flash(Color color, float seconds)
        {
            _flash.style.backgroundColor = new Color(color.r, color.g, color.b, 1f);
            _flashAlpha = Mathf.Clamp01(color.a);
            _flashDuration = Mathf.Max(0.02f, seconds);
            _flashLeft = _flashDuration;
            _flash.style.opacity = _flashAlpha;
            _flash.style.display = DisplayStyle.Flex;
        }

        public bool IsFlashing => _flashLeft > 0f;

        /// <summary>Ends a running flash at once (presenter jumps start clean).</summary>
        public void ClearFlash()
        {
            _flashLeft = 0f;
            _flash.style.display = DisplayStyle.None;
        }

        void TickFlash(float dt)
        {
            if (_flashLeft <= 0f) return;
            _flashLeft -= dt;
            if (_flashLeft <= 0f)
            {
                _flash.style.display = DisplayStyle.None;
                return;
            }
            _flash.style.opacity = _flashAlpha * (_flashLeft / _flashDuration);
        }

        // ---------- Floating text ----------

        public void FloatText(Vector3 world, string text, Color color)
        {
            var label = _floatPool.Count > 0 ? _floatPool.Pop() : NewFloatLabel();
            label.text = text;
            label.style.color = color;
            label.style.display = DisplayStyle.Flex;
            label.style.opacity = 1f;
            _floaters.Add(new Floater { label = label, world = world, age = 0f, life = 0.9f });
            Place(label, world);
        }

        public int FloatingTextCount => _floaters.Count;

        Label NewFloatLabel()
        {
            var l = new Label { pickingMode = PickingMode.Ignore };
            l.AddToClassList("jk-float");
            _floatLayer.Add(l);
            return l;
        }

        void TickFloaters(float dt)
        {
            for (int i = _floaters.Count - 1; i >= 0; i--)
            {
                var f = _floaters[i];
                f.age += dt;
                if (f.age >= f.life)
                {
                    f.label.style.display = DisplayStyle.None;
                    _floatPool.Push(f.label);
                    _floaters.RemoveAt(i);
                    continue;
                }
                float k = f.age / f.life;
                Place(f.label, f.world + Vector3.up * (0.8f * k));
                f.label.style.opacity = 1f - k * k;
                _floaters[i] = f;
            }
        }

        void Place(Label label, Vector3 world)
        {
            var cam = Camera.main;
            var panel = Panel;
            if (cam == null || panel == null) return;
            Vector2 p = RuntimePanelUtils.CameraTransformWorldToPanel(panel, world, cam);
            label.style.left = p.x;
            label.style.top = p.y;
        }
    }
}
