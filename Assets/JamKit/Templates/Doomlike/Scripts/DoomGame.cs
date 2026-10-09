using System;
using System.Collections;
using System.Collections.Generic;
using JamKit.Flow;
using JamKit.Roguelite;
using JamKit.Shell;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

namespace JamKit.Doomlike
{
    /// <summary>
    /// The DOOM-like: a run through the config's levels in order. Each level is built from its text
    /// map when it starts (LevelBuilder: geometry, doors, items, NavMesh), enemies spawn on it, and
    /// reaching an exit shows the tally, then (optionally) a 1-of-3 upgrade draft, then the next
    /// level. The last exit wins (GameRoot.Win); dying loses (GameRoot.Lose).
    /// This component owns the per-frame order: input, player, gun, enemies, shots, effects, doors,
    /// pickups, map cells, HUD. Files: DoomGame.cs (frame, input, pointer lock),
    /// DoomGame.Combat.cs (shooting, damage, blasts, noise), DoomGame.Levels.cs (levels, tally,
    /// draft, death, debug and smoke).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed partial class DoomGame : MonoBehaviour
    {
        public enum Phase { Loading, Playing, Dying, Tally, Draft, Finished }

        [Header("Data")]
        public DoomlikeConfig config;
        [Tooltip("Templates/Doomlike/UI/Doomlike.uss")]
        public StyleSheet hudStyle;

        [Header("Scene (set by the builder)")]
        public DoomPlayer player;
        public Camera cam;
        public RetroCamera retro;
        public ProjectileField projectiles;
        public FxField fx;
        public Transform levelRoot;

        static DoomGame _current;
        static ulong _seedOverride;
        static bool _skipClickToPlay;
        static int _startLevel;

        /// <summary>The running game, if a DOOM-like scene is loaded.</summary>
        public static DoomGame Current => _current;

        /// <summary>Seed for the next run (tests, bug reports). 0 = a new random seed each run.</summary>
        public static ulong SeedOverride
        {
            get => _seedOverride;
            set => _seedOverride = value;
        }

        /// <summary>Play without the "click to play" pointer lock (tests). Batch mode always skips it.</summary>
        public static bool SkipClickToPlay
        {
            get => _skipClickToPlay;
            set => _skipClickToPlay = value;
        }

        /// <summary>Start the next run on this level (0-based; tests and level designers).</summary>
        public static int StartLevel
        {
            get => _startLevel;
            set => _startLevel = Math.Max(0, value);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _current = null;
            _seedOverride = 0;
            _skipClickToPlay = false;
            _startLevel = 0;
        }

        Phase _phase = Phase.Loading;
        float _phaseTime;
        DoomHud _hud;
        DoomChoicePanel _choice;
        WeaponView _view;
        Button _giveAllButton;
        Func<bool> _skipHandler;
        Func<IEnumerator> _smokeDriver;
        bool _focused, _padFocus, _fireNeedsRelease;
        float _flash;
        ulong _seed;
        int _shownKills = -1, _shownEnemies = -1;

        public Phase CurrentPhase => _phase;
        public DoomHud Hud => _hud;
        public DoomChoicePanel Choice => _choice;
        public WeaponView View => _view;
        /// <summary>Gameplay runs (false behind the "click to play" overlay).</summary>
        public bool Focused => _focused;
        public ulong Seed => _seed;
        public float CameraYaw => player != null ? player.Yaw : 0f;
        public Vector3 CameraPosition => cam != null ? cam.transform.position : Vector3.zero;

        static bool AutoFocus => _skipClickToPlay || Application.isBatchMode || (GameRoot.Exists && GameRoot.Input.IsSynthetic);

        // ---------- Setup ----------

        void Start()
        {
            if (!GameRoot.Exists || config == null || player == null || cam == null || projectiles == null || fx == null)
            {
                Debug.LogError("DoomGame: missing config or scene parts. Rebuild with JamKit > Rebuild Shell Scenes.");
                enabled = false;
                return;
            }
            _current = this;
            if (levelRoot == null) levelRoot = new GameObject("Levels").transform;
            player.Setup(config.player);
            fx.Init(config.spriteMaterial, config.fxTexture);
            projectiles.Init(config.spriteMaterial, config.fxTexture);
            _view = WeaponView.Create(cam, config.weaponMaterial);
            if (retro != null)
            {
                retro.screenMaterial = config.screenMaterial;
                retro.look = config.retro;
            }

            GameRoot.Hud.Show();
            GameRoot.Hud.ShowHealth(false);
            _hud = new DoomHud(GameRoot.Hud.Root, hudStyle);
            _choice = new DoomChoicePanel(_hud.Root);
            AddDebugButton();

            GameRoot.Save.SettingsChanged += OnSettingsChanged;
            OnSettingsChanged(GameRoot.Settings);
            _skipHandler = DebugSkip;
            DebugMenu.SkipHandler = _skipHandler;
            _smokeDriver = SmokeDrive;
            SmokeRunner.GameplayDriver = _smokeDriver;
            SetupPresenter();

            SetupRun();
            BeginRun();
        }

        void OnDestroy()
        {
            if (_current == this) _current = null;
            if (Cursor.lockState != CursorLockMode.None)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            Shader.SetGlobalFloat(FlashId, 0f);
            if (!GameRoot.Exists) return;
            GameRoot.Save.SettingsChanged -= OnSettingsChanged;
            if (DebugMenu.SkipHandler == _skipHandler) DebugMenu.SkipHandler = null;
            if (SmokeRunner.GameplayDriver == _smokeDriver) SmokeRunner.GameplayDriver = null;
            TeardownPresenter();
            if (_giveAllButton != null) _giveAllButton.RemoveFromHierarchy();
            _hud?.Remove();
        }

        void OnSettingsChanged(Settings s)
        {
            if (retro != null) retro.SetOn(s == null || s.pixelLook);
            if (player != null && player.Health != null && s != null) player.Health.DamageTakenScale = s.damageTakenScale;
        }

        void AddDebugButton()
        {
            var panel = GameRoot.Ui != null && GameRoot.Ui.DebugPanel != null ? GameRoot.Ui.DebugPanel.Root.Q<VisualElement>("debug-panel") : null;
            if (panel == null) return;
            _giveAllButton = new Button(GiveAll) { text = "Give all (weapons, keys, ammo)" };
            _giveAllButton.AddToClassList("jk-debug-button");
            var fps = panel.Q<Button>("debug-fps");
            int at = fps != null ? panel.IndexOf(fps) : panel.childCount;
            panel.Insert(Mathf.Max(0, at), _giveAllButton);
        }

        // ---------- Frame ----------

        static readonly int FogColorId = Shader.PropertyToID("_DLFogColor");
        static readonly int FogParamsId = Shader.PropertyToID("_DLFogParams");
        static readonly int FlashId = Shader.PropertyToID("_DLFlash");

        void Update()
        {
            float udt = Time.unscaledDeltaTime;
            _hud?.Tick(udt);
            _choice?.Tick(udt);
            if (_level == null || !GameRoot.Exists) return;
            UpdatePointerLock();
            bool playing = GameRoot.Flow.Current == GameState.Playing && !GameRoot.IsLoadingScene;
            float dt = Time.deltaTime;
            if (playing && (_phase == Phase.Playing || _phase == Phase.Dying) && (_focused || _phase == Phase.Dying))
                Simulate(dt);

            _flash = Mathf.Max(0f, _flash - udt * 14f);
            Shader.SetGlobalFloat(FlashId, _flash);
            FaceScenery();
            if (_view != null) _view.Tick(dt, player.BobOffset, _arsenal.Lowered01, player.Alive && _phase != Phase.Tally && _phase != Phase.Draft);
            _hud.SetCrosshair(_phase == Phase.Playing && player.Alive);
            _hud.Refresh(player, _arsenal, config);
            if (_tally.kills != _shownKills || _tally.totalEnemies != _shownEnemies)
            {
                _shownKills = _tally.kills;
                _shownEnemies = _tally.totalEnemies;
                GameRoot.Hud.SetScoreText("Kills " + _shownKills + " / " + _shownEnemies);
            }
        }

        void Simulate(float dt)
        {
            _phaseTime += dt;
            ReadInput(dt, out Vector2 move, out bool walk, out bool fireHeld, out bool firePressed, out bool use);
            player.Tick(dt, move, walk, this);
            if (_phase == Phase.Playing && player.Alive)
            {
                FireWeapon(dt, fireHeld, firePressed);
                if (use) Use();
            }
            for (int i = 0; i < _enemies.Count; i++) _enemies[i].Tick(dt, this);
            _noiseCount = 0;
            CountKills();
            projectiles.Tick(dt, this, CameraYaw);
            fx.Tick(dt, CameraYaw);
            TickDoors(dt);
            TickItems(dt);
            if (_phase == Phase.Playing && player.Alive) TickCells(dt);
            if (_phase == Phase.Playing) _tally.time += dt;
            TickBoss();
            if (_phase == Phase.Dying && _phaseTime >= 1.6f) FinishDeath();
        }

        void FaceScenery()
        {
            float yaw = CameraYaw;
            var scenery = _level.scenery;
            for (int i = 0; i < scenery.Count; i++) scenery[i].Face(yaw);
        }

        // ---------- Input ----------

        void ReadInput(float dt, out Vector2 move, out bool walk, out bool fireHeld, out bool firePressed, out bool use)
        {
            var input = GameRoot.Input;
            move = input.MoveVector;
            walk = input.Held(input.Sprint); // the Shift / LB action
            fireHeld = input.Held(input.Fire);
            firePressed = input.Pressed(input.Fire);
            use = input.Pressed(input.Interact) || input.Pressed(input.Dash);
            if (_fireNeedsRelease)
            {
                if (!fireHeld) _fireNeedsRelease = false;
                fireHeld = firePressed = false;
            }
            if (!player.Alive) return;

            Vector2 look = input.LookDelta(GameRoot.Settings);
            var t = config.player;
            if (input.LookFromGamepad)
            {
                // The shell's look binding scales the stick by 8 (see DefaultControls).
                float k = t.gamepadDegreesPerSecond * dt / 8f;
                player.Look(look.x * k, -look.y * k);
            }
            else player.Look(look.x * t.mouseDegreesPerPixel, -look.y * t.mouseDegreesPerPixel);

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame) _arsenal.SelectSlot(1);
                if (kb.digit2Key.wasPressedThisFrame) _arsenal.SelectSlot(2);
                if (kb.digit3Key.wasPressedThisFrame) _arsenal.SelectSlot(3);
                if (kb.digit4Key.wasPressedThisFrame) _arsenal.SelectSlot(4);
                if (kb.digit5Key.wasPressedThisFrame) _arsenal.SelectSlot(5);
                if (kb.digit6Key.wasPressedThisFrame) _arsenal.SelectSlot(6);
                if (kb.digit7Key.wasPressedThisFrame) _arsenal.SelectSlot(7);
                if (kb.qKey.wasPressedThisFrame) _arsenal.Cycle(-1);
            }
            var mouse = Mouse.current;
            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (scroll > 0.5f) _arsenal.Cycle(-1);
                else if (scroll < -0.5f) _arsenal.Cycle(1);
            }
            var pad = Gamepad.current;
            if (pad != null && pad.buttonNorth.wasPressedThisFrame) _arsenal.Cycle(1);
        }

        // ---------- Pointer lock ("click to play") ----------

        /// <summary>Browsers only lock the mouse after a click, so play starts (and resumes after a
        /// pause) behind a "click to play" overlay; a gamepad button works too. The cursor is free
        /// whenever a menu, the tally or the draft is up.</summary>
        void UpdatePointerLock()
        {
            bool want = GameRoot.Flow.Current == GameState.Playing && !GameRoot.IsLoadingScene && _phase == Phase.Playing
                && !_choice.IsOpen && !_hud.TallyShown && !(GameRoot.Ui.DebugPanel != null && GameRoot.Ui.DebugPanel.IsOpen) && GameRoot.Ui.Top == null;
            if (!want)
            {
                if (Cursor.lockState != CursorLockMode.None)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                _focused = false;
                _padFocus = false;
                _hud.SetClickOverlay(false);
                return;
            }
            if (AutoFocus)
            {
                _focused = true;
                _hud.SetClickOverlay(false);
                return;
            }
            bool locked = Cursor.lockState == CursorLockMode.Locked;
            if (_focused && !locked && !_padFocus) _focused = false; // the browser let go (Esc, alt-tab)
            var mouse = Mouse.current;
            bool click = mouse != null && mouse.leftButton.wasPressedThisFrame;
            if (!locked && click)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                if (!_focused) _fireNeedsRelease = true;
                _focused = true;
                _padFocus = false;
            }
            else if (!_focused)
            {
                var pad = Gamepad.current;
                if (pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.rightTrigger.wasPressedThisFrame))
                {
                    _focused = true;
                    _padFocus = true;
                    _fireNeedsRelease = true;
                }
            }
            _hud.SetClickOverlay(!_focused);
        }
    }
}
