using JamKit.Flow;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace JamKit.Shell
{
    /// <summary>
    /// Backquote (`) or F1: god mode, time scale, instant win/lose, skip to the next scene and an FPS
    /// counter. The menu opens in the editor and in development builds, or anywhere when GameConfig
    /// allows it. The actions themselves (<see cref="Win"/>, <see cref="Lose"/>...) always work from
    /// code, which is how the smoke test drives a release build.
    /// </summary>
    public sealed class DebugMenu
    {
        static bool _godMode;
        static float _timeScale = 1f;

        /// <summary>The player takes no damage (Damageable with isPlayer honours this).</summary>
        public static bool GodMode
        {
            get => _godMode;
            set => _godMode = value;
        }

        /// <summary>True while the player must take no damage: the debug god mode, presenter mode's
        /// Invincible, or attract mode. Player damage code checks this (not <see cref="GodMode"/>).</summary>
        public static bool PlayerInvincible => JamKit.Presenting.Invincibility.Blocks(true, _godMode, PresenterMode.InvincibleNow);

        /// <summary>Multiplies game time while playing (0.25, 1 or 2 from the menu).</summary>
        public static float TimeScale
        {
            get => _timeScale;
            set => _timeScale = Mathf.Clamp(value, 0f, 10f);
        }

        public readonly VisualElement Root;
        readonly VisualElement _panel;
        readonly Label _fpsLabel;
        readonly Button _god, _fps;
        bool _open, _showFps;
        float _fpsClock;
        int _fpsFrames;

        public bool IsOpen => _open;

        /// <summary>Whether the hotkeys open the menu in this build.</summary>
        public static bool Allowed =>
            Application.isEditor || Debug.isDebugBuild || (GameRoot.Exists && GameRoot.Config.allowDebugMenuInRelease);

        public DebugMenu(VisualTreeAsset tree, VisualElement parent)
        {
            Root = tree != null ? (VisualElement)tree.Instantiate() : new VisualElement();
            Root.AddToClassList("jk-layer");
            Root.pickingMode = PickingMode.Ignore;
            parent.Add(Root);
            _panel = Root.Q<VisualElement>("debug-panel");
            _fpsLabel = Root.Q<Label>("debug-fps-label");
            _god = Wire("debug-god", () => { GodMode = !GodMode; Refresh(); });
            Wire("debug-time-slow", () => TimeScale = 0.25f);
            Wire("debug-time-normal", () => TimeScale = 1f);
            Wire("debug-time-fast", () => TimeScale = 2f);
            Wire("debug-win", Win);
            Wire("debug-lose", Lose);
            Wire("debug-next", NextScene);
            _fps = Wire("debug-fps", () => { _showFps = !_showFps; Refresh(); });
            Wire("debug-close", () => SetOpen(false));
            if (_panel != null) _panel.style.display = DisplayStyle.None;
            if (_fpsLabel != null) _fpsLabel.style.display = DisplayStyle.None;
        }

        Button Wire(string name, System.Action action)
        {
            var b = Root.Q<Button>(name);
            if (b != null) b.clicked += action;
            return b;
        }

        public void SetOpen(bool open)
        {
            _open = open;
            if (_panel != null) _panel.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            Refresh();
            if (open) _god?.Focus();
            else
            {
                var top = GameRoot.Exists ? GameRoot.Ui.Top : null;
                if (top != null) top.RestoreFocus();
                else Root.focusController?.focusedElement?.Blur();
            }
        }

        void Refresh()
        {
            if (_god != null) _god.text = "God mode: " + (GodMode ? "ON" : "off");
            if (_fps != null) _fps.text = "FPS counter: " + (_showFps ? "on" : "off");
            if (_fpsLabel != null) _fpsLabel.style.display = _showFps ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void Tick(float unscaledDt)
        {
            var kb = Keyboard.current;
            if (kb != null && Allowed && (kb.backquoteKey.wasPressedThisFrame || kb.f1Key.wasPressedThisFrame)) SetOpen(!_open);
            if (_open && GameRoot.Exists && GameRoot.Input.Cancel.WasPressedThisFrame()) SetOpen(false);

            if (!_showFps || _fpsLabel == null) return;
            _fpsFrames++;
            _fpsClock += unscaledDt;
            if (_fpsClock >= 0.5f)
            {
                _fpsLabel.text = Mathf.RoundToInt(_fpsFrames / _fpsClock) + " fps";
                _fpsFrames = 0;
                _fpsClock = 0f;
            }
        }

        // ---------- Actions (usable from code in any build) ----------

        public static void Win()
        {
            if (!GameRoot.Exists) return;
            var flow = GameRoot.Flow;
            if (flow.Current == GameState.Paused) flow.Resume();
            GameRoot.Win();
        }

        public static void Lose()
        {
            if (!GameRoot.Exists) return;
            var flow = GameRoot.Flow;
            if (flow.Current == GameState.Paused) flow.Resume();
            GameRoot.Lose();
        }

        /// <summary>Loads the next scene in Build Settings; from the last one, wins the run.
        /// A game with its own idea of "next" (the bullet hell skips a room) sets <see cref="SkipHandler"/>.</summary>
        public static void NextScene()
        {
            if (!GameRoot.Exists) return;
            var skip = _skipHandler;
            if (skip != null && skip()) return;
            if (GameRoot.Instance.SceneService.LoadNext()) return;
            Win();
        }

        static System.Func<bool> _skipHandler;

        /// <summary>Replaces "Next scene" while a game is running: return true when handled (e.g. the
        /// room or boss was skipped). Set it in Start, clear it in OnDestroy. Cleared on play-mode start.</summary>
        public static System.Func<bool> SkipHandler
        {
            get => _skipHandler;
            set => _skipHandler = value;
        }
    }
}
