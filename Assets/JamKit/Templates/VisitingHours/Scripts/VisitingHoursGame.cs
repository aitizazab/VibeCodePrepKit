using System;
using System.Collections;
using System.Collections.Generic;
using Ink.Runtime;
using JamKit.Doomlike;
using JamKit.Flow;
using JamKit.Shell;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

namespace JamKit.VisitingHours
{
    /// <summary>
    /// The Visiting Hours slice: first person with no weapons (the DOOM-like's controller and
    /// billboards), a journal of facts, memories and deductions, and an argument battle. One run:
    /// Day 1 at St. Brigid's (chart him, find what's there), 11 PM in the booth with Mags (the rig,
    /// the journal), Night 1 on Walt's street (memories, deductions, the rushing check), Walt's
    /// argument, its outcome, and the end card. This component owns the frame: input, the walk,
    /// what's being looked at, the battle, the HUD. Files: VisitingHoursGame.cs (frame, input,
    /// looking), .Story.cs (ink, places, transitions, the end), .Battle.cs, .Debug.cs (debug skip,
    /// the smoke driver, test hooks).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed partial class VisitingHoursGame : MonoBehaviour
    {
        public enum Mode { Loading, Explore, Transition, Battle, End }

        [Header("Data")]
        public VisitingHoursConfig config;
        [Tooltip("Templates/VisitingHours/UI/VisitingHours.uss")]
        public StyleSheet style;

        [Header("Scene (set by the builder)")]
        public DoomPlayer player;
        public Camera cam;
        public Transform worldRoot;

        static VisitingHoursGame _current;
        static bool _fastTransitions;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _current = null;
            _fastTransitions = false;
        }

        /// <summary>The running slice, if its scene is loaded.</summary>
        public static VisitingHoursGame Current => _current;

        /// <summary>Title cards and fades take a moment instead of seconds (tests; batch mode always).</summary>
        public static bool FastTransitions
        {
            get => _fastTransitions;
            set => _fastTransitions = value;
        }

        static bool Fast => _fastTransitions || Application.isBatchMode;

        SliceState _state;
        VhHud _hud;
        VhJournalScreen _journal;
        VhBattleView _battleView;
        VhArea _area;
        Mode _mode = Mode.Loading;
        VhSpot _target;
        Func<bool> _skipHandler;
        Func<IEnumerator> _smokeDriver;
        Action<string> _onInkEvent;
        Action<string> _onDialogueEnded;
        Action<Story> _bindInk;
        AudioSource _monitor;
        bool _focused, _journalWasOpen, _walked;

        public SliceState State => _state;
        public Journal Journal => _state != null ? _state.journal : null;
        public Mode CurrentMode => _mode;
        public SliceArea Area => _area != null ? _area.area : SliceArea.Hospital;
        public VhArea BuiltArea => _area;
        public VhHud Hud => _hud;
        public VhJournalScreen JournalScreen => _journal;
        public VhBattleView BattleView => _battleView;
        /// <summary>What E would examine right now (null when nothing is in reach).</summary>
        public VhSpot Target => _target;

        // ---------- Setup ----------

        void Start()
        {
            if (!GameRoot.Exists || config == null || player == null || cam == null)
            {
                Debug.LogError("VisitingHoursGame: missing config or scene parts. Rebuild with JamKit > Rebuild Shell Scenes.");
                enabled = false;
                return;
            }
            _current = this;
            if (worldRoot == null) worldRoot = new GameObject("World").transform;
            player.Setup(config.player);
            player.NewRun(100f);
            _state = new SliceState(config.MakeBook());

            GameRoot.Hud.Show();
            GameRoot.Hud.ShowHealth(false);
            GameRoot.Hud.SetScoreText("");
            _hud = new VhHud(GameRoot.Hud.Root, style);
            _battleView = new VhBattleView(GameRoot.Hud.Root, style, config.walt != null ? config.walt.maxObjects : 2);
            _journal = VhJournalScreen.Ensure(style);
            _journal.Attach(this, config, _state.journal);

            _onInkEvent = OnInkEvent;
            _onDialogueEnded = OnDialogueEnded;
            _bindInk = BindInk;
            GameRoot.Dialogue.EventRaised += _onInkEvent;
            GameRoot.Dialogue.Ended += _onDialogueEnded;
            GameRoot.Dialogue.StoryCreated += _bindInk;
            _skipHandler = DebugSkip;
            DebugMenu.SkipHandler = _skipHandler;
            _smokeDriver = SmokeDrive;
            SmokeRunner.GameplayDriver = _smokeDriver;
            GameRoot.Run.Set("Slice", "Visiting Hours");

            StartCoroutine(BeginDay());
        }

        void OnDestroy()
        {
            if (_current == this) _current = null;
            if (Cursor.lockState != CursorLockMode.None)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            Shader.SetGlobalVector(FogParamsId, Vector4.zero);
            if (!GameRoot.Exists) return;
            GameRoot.Dialogue.EventRaised -= _onInkEvent;
            GameRoot.Dialogue.Ended -= _onDialogueEnded;
            GameRoot.Dialogue.StoryCreated -= _bindInk;
            if (DebugMenu.SkipHandler == _skipHandler) DebugMenu.SkipHandler = null;
            if (SmokeRunner.GameplayDriver == _smokeDriver) SmokeRunner.GameplayDriver = null;
            if (_journal != null) _journal.Close();
            _hud?.Remove();
            _battleView?.Root.RemoveFromHierarchy();
        }

        // ---------- Frame ----------

        static readonly int FogColorId = Shader.PropertyToID("_VHFogColor");
        static readonly int FogParamsId = Shader.PropertyToID("_VHFogParams");

        void Update()
        {
            if (_state == null || !GameRoot.Exists) return;
            float udt = Time.unscaledDeltaTime;
            float dt = Time.deltaTime;
            _hud.Tick(udt);
            bool journalOpen = _journal.IsOpen;
            if (_journalWasOpen && !journalOpen) OnJournalClosed();
            _journalWasOpen = journalOpen;

            bool playing = GameRoot.Flow.Current == GameState.Playing && !GameRoot.IsLoadingScene;
            UpdatePointerLock(playing && _mode == Mode.Explore && !journalOpen);
            FaceBillboards();

            if (_mode == Mode.Battle)
            {
                if (playing) TickBattle(dt);
                _battleView.Tick(udt);
                _hud.SetExploring(false);
                return;
            }

            bool exploring = playing && _mode == Mode.Explore && !journalOpen;
            _hud.SetExploring(_mode == Mode.Explore && !GameRoot.Dialogue.IsActive);
            _hud.SetOpenQuestions(_state.journal.OpenQuestions(_openScratch));
            if (playing && _mode == Mode.Explore && JournalPressed() && !_journalOpenedThisFrame)
            {
                if (journalOpen) _journal.Close();
                else _journal.Open(VhJournalScreen.Tab.Facts, false);
            }
            _journalOpenedThisFrame = false;
            if (!exploring)
            {
                _hud.SetPrompt("");
                return;
            }

            ReadInput(dt, out Vector2 move, out bool hurry, out bool use);
            if (player.TickMove(dt, move, !hurry) && GameRoot.Exists) GameRoot.Audio.Play("step", 0.22f);
            if (!_walked && player.DistanceMoved > 3f)
            {
                _walked = true;
                _hud.HideControls();
            }
            TickArea(dt);
            _target = PickTarget();
            _hud.SetPrompt(_target != null ? PromptFor(_target) : "");
            if (use && _target != null) Examine(_target.def.id);
        }

        readonly List<OpenQuestion> _openScratch = new List<OpenQuestion>();
        bool _journalOpenedThisFrame;

        static bool JournalPressed()
        {
            var kb = Keyboard.current;
            if (kb != null && (kb.jKey.wasPressedThisFrame || kb.tabKey.wasPressedThisFrame)) return true;
            var pad = Gamepad.current;
            return pad != null && pad.selectButton.wasPressedThisFrame;
        }

        void FaceBillboards()
        {
            if (_area == null) return;
            float yaw = player.Yaw;
            var faced = _area.faced;
            for (int i = 0; i < faced.Count; i++) faced[i].Face(yaw);
        }

        // ---------- Input and looking ----------

        void ReadInput(float dt, out Vector2 move, out bool hurry, out bool use)
        {
            var input = GameRoot.Input;
            move = input.MoveVector;
            hurry = input.GameplayAllowed && input.Sprint.IsPressed();
            use = input.Pressed(input.Interact);
            Vector2 look = input.LookDelta(GameRoot.Settings);
            var device = input.Look.activeControl != null ? input.Look.activeControl.device : null;
            var t = config.player;
            if (device is Gamepad)
            {
                // The shell's look binding scales the stick by 8 (see DefaultControls).
                float k = t.gamepadDegreesPerSecond * dt / 8f;
                player.Look(look.x * k, -look.y * k);
            }
            else if (_focused) player.Look(look.x * t.mouseDegreesPerPixel, -look.y * t.mouseDegreesPerPixel);
        }

        /// <summary>The nearest thing in reach that Nadia is facing.</summary>
        VhSpot PickTarget()
        {
            if (_area == null) return null;
            Vector3 eye = player.EyePosition;
            Vector3 fwd = cam.transform.forward;
            VhSpot best = null;
            float bestScore = float.MaxValue;
            var spots = _area.spots;
            for (int i = 0; i < spots.Count; i++)
            {
                var s = spots[i];
                Vector3 to = s.position - eye;
                Vector3 flat = new Vector3(to.x, 0f, to.z);
                float dist = flat.magnitude;
                float reach = config.useRange + s.radius;
                if (dist > reach) continue;
                float dot = to.sqrMagnitude > 1e-4f ? Vector3.Dot(to.normalized, fwd) : 1f;
                float need = dist < 1.6f ? 0.35f : 0.8f;
                if (dot < need) continue;
                float score = dist * (2f - dot);
                if (score < bestScore) { bestScore = score; best = s; }
            }
            return best;
        }

        static string PromptFor(VhSpot s) => s.def.id == "walt" ? "E  Talk to Walt" : "E  Look at " + s.def.label;

        // ---------- Pointer lock ----------

        /// <summary>The mouse is held while walking. A browser only allows that after a click
        /// ("Click to look around"); a desktop build takes it at once. Batch mode never locks.</summary>
        void UpdatePointerLock(bool want)
        {
            if (!want || Application.isBatchMode)
            {
                if (Cursor.lockState != CursorLockMode.None)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                _focused = Application.isBatchMode && want;
                return;
            }
            bool locked = Cursor.lockState == CursorLockMode.Locked;
            if (locked) { _focused = true; return; }
            var mouse = Mouse.current;
            bool click = mouse != null && mouse.leftButton.wasPressedThisFrame;
            if (Application.platform != RuntimePlatform.WebGLPlayer || click)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                _focused = true;
            }
            else
            {
                _focused = false;
                _hud.SetPrompt("Click to look around");
            }
        }

        // ---------- Look ----------

        void ApplyLook(SliceArea area)
        {
            Color fog;
            Vector3 p;
            switch (area)
            {
                case SliceArea.Street:
                    fog = config.dreamFog; p = config.dreamFogParams;
                    cam.backgroundColor = config.dreamFog;
                    _hud.SetVignette(config.dreamVignette, new Color(1f, 0.78f, 0.52f, 1f));
                    break;
                case SliceArea.Booth:
                    fog = new Color(0.04f, 0.05f, 0.07f); p = new Vector3(4f, 14f, 0.5f);
                    cam.backgroundColor = Color.black;
                    _hud.SetVignette(0.5f, Color.black);
                    break;
                default:
                    fog = config.hospitalFog; p = config.hospitalFogParams;
                    cam.backgroundColor = config.hospitalFog;
                    _hud.SetVignette(0.25f, Color.black);
                    break;
            }
            Shader.SetGlobalColor(FogColorId, fog);
            Shader.SetGlobalVector(FogParamsId, new Vector4(p.x, p.y, p.z, 0f));
        }
    }
}
