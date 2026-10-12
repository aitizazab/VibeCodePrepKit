using System;
using System.Globalization;
using System.Text;
using JamKit.Flow;
using JamKit.Presenting;
using JamKit.Shell;
using UnityEngine;
using UnityEngine.InputSystem;
using Vec = System.Numerics.Vector2;

namespace JamKit.Argument
{
    /// <summary>
    /// Runs one argument battle in whatever scene is loaded (docs/redesign/battle.md): the rules and the real-time play
    /// (<see cref="BattleFlow"/>), its stage (<see cref="ArgumentStage"/>), sound, keys and pads through the shell's
    /// actions, the save. The entry points: <see cref="BeginBattle"/> (by id, from the game) and <see cref="Begin"/>.
    /// While it runs: world gameplay is blocked (InputService.BlockWorld), the world's ambience and music give way to the
    /// battle's (ArgumentAudio), the cursor is free (the light follows the mouse over the table), the argument's Controls
    /// rows show. When it ends the result goes into the save (ArgumentLedger, "vh_arguments"); Continue on the end card
    /// calls back with the record and the stage fades back to the world by itself.
    ///
    /// The controls (rebindable rows "Argue: ..." where they exist): move the light (stick, d-pad, WASD, arrows, or the
    /// mouse over the table); A / Enter / left click shows the card in hand; X / 1 / right click asks "Why?"; Y / 3 says
    /// "That's true" (held on the claim: give in); hold B / 4 presses; LB / RB, Q / E, 2 or the wheel change the card;
    /// View / 5 opens the case file (the battle waits). In "Because...", up / down and A, or click a card. In the
    /// showdown, A cuts. A hurries a line.
    ///
    /// The presenter (docs/redesign/battle.md, round 2): the sections (arguing and dodging take turns; the default) or
    /// the debate (milestone 1), so the creator can play both: <see cref="DefaultPresenter"/> (kept in PlayerPrefs; the
    /// prototype's menu switches it), or <c>-battle-presenter debate|sections</c> on the command line. In the sections the
    /// stick (or the mouse) picks one of her statements and the same buttons act on it; A catches her words as she says
    /// them. Heard out with Lena sets the save's keepsake flag: her watch is Nadia's (<see cref="ArgumentBattles.KeepsakeFlag"/>).
    /// </summary>
    public sealed class ArgumentHost : MonoBehaviour
    {
        public const float PressHold = BattleFlow.PressHold;
        /// <summary>The fast autopilot (tests, the house's skip) plays this many times real speed.</summary>
        public const int FastSteps = 4;

        static ArgumentHost _current;
        static string _expressionOverride;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _current = null;
            _expressionOverride = null;
            _presenterFromArgs = -2;
        }

        /// <summary>The battle running now, or null.</summary>
        public static ArgumentHost Current => _current;
        public static bool Running => _current != null;

        public const string PresenterPref = "jamkit.argument.presenter", PresenterFlag = "-battle-presenter";
        static int _presenterFromArgs = -2;

        /// <summary>How battles are played unless a caller says otherwise: the command line's <c>-battle-presenter</c>, else
        /// the player's choice (PlayerPrefs, set from the prototype's menu), else the sections. Setting it keeps it.</summary>
        public static ArgumentPresenter DefaultPresenter
        {
            get
            {
                if (_presenterFromArgs == -2) _presenterFromArgs = PresenterArg(Environment.GetCommandLineArgs());
                if (_presenterFromArgs >= 0) return (ArgumentPresenter)_presenterFromArgs;
                return PlayerPrefs.GetInt(PresenterPref, (int)ArgumentPresenter.Sections) == (int)ArgumentPresenter.Debate ? ArgumentPresenter.Debate : ArgumentPresenter.Sections;
            }
            set
            {
                _presenterFromArgs = -1;
                PlayerPrefs.SetInt(PresenterPref, (int)value);
                PlayerPrefs.Save();
            }
        }

        /// <summary>Plays every battle in <paramref name="presenter"/> for this session only (tests, captures), not kept;
        /// null goes back to the command line's or the player's choice.</summary>
        public static void UsePresenterForSession(ArgumentPresenter? presenter) => _presenterFromArgs = presenter.HasValue ? (int)presenter.Value : -2;

        /// <summary>The presenter named on a command line (-battle-presenter debate|sections), or -1.</summary>
        public static int PresenterArg(string[] args)
        {
            if (args == null) return -1;
            for (int i = 0; i + 1 < args.Length; i++)
            {
                if (args[i] != PresenterFlag) continue;
                string v = args[i + 1].Trim('"').ToLowerInvariant();
                if (v == "debate") return (int)ArgumentPresenter.Debate;
                if (v == "sections") return (int)ArgumentPresenter.Sections;
            }
            return -1;
        }

        /// <summary>The expression she's held on (null: none): captures.</summary>
        public static string ExpressionOverride => _expressionOverride;

        /// <summary>Holds her on one expression whatever she says (null lets it follow her again): the boil capture.</summary>
        public static void OverrideExpression(string expression) => _expressionOverride = string.IsNullOrEmpty(expression) ? null : expression;

        ArgumentEngine _engine;
        BattleFlow _flow;
        ArgumentStage _stage;
        ArgumentAudio _audio;
        Action<ArgumentRecord> _onClosed;
        ArgumentRecord _record;
        bool _closed, _saved, _introDone, _endShown, _case;
        BattlePilot _pilot;
        bool _pilotFast;
        float _endAt, _beat;
        int _pick = -1;
        bool _continueClicked;
        readonly NavInput _navInput = new NavInput();
        string _hints = "", _argueHints = "", _quotePrompt = "", _voiceId = "", _voiceLower = "lena";
        int _keysVersion = -1;
        Gamepad _keysPad;
        PadStyle _style;
        readonly StringBuilder _sb = new StringBuilder(256);

        public ArgumentEngine Engine => _engine;
        public BattleFlow Flow => _flow;
        public ArgumentStage Stage => _stage;
        public ArgumentAudio Audio => _audio;
        public ArgumentRecord Result => _record;
        public bool Ended => _engine != null && _engine.Ended;
        /// <summary>The end card is up (waiting for Continue).</summary>
        public bool EndCardShown => _endShown;
        public BattleState State => _flow != null ? _flow.State : BattleState.Intro;
        public bool CaseFileOpen => _case;

        /// <summary>The autopilot plays it (fast: tests, the house's skips). <see cref="SetAutopilot"/> for a human-paced one
        /// with a first-time player's mistakes (the prototype's "watch it play", the recorded run).</summary>
        public bool Autopilot
        {
            get => _pilot != null;
            set => SetAutopilot(value, false);
        }

        public void SetAutopilot(bool on, bool human, params BattlePilot.Mistake[] mistakes)
        {
            _pilot = on ? new BattlePilot(human, mistakes) : null;
            _pilotFast = on && !human;
            _pilotSteps = _pilotFast ? FastSteps : 1;
        }

        /// <summary>How many battle frames the autopilot plays per real frame (captures speed up to a moment, then play
        /// it at 1).</summary>
        public int PilotSteps
        {
            get => _pilotSteps;
            set => _pilotSteps = Mathf.Clamp(value, 1, 12);
        }

        int _pilotSteps = 1;

        public BattlePilot Pilot => _pilot;

        static ArgumentStrings S => ArgumentStrings.Shared;

        /// <summary>
        /// The game's hand-off (docs/ARGUMENT-PROTOTYPE.md, "Starting a battle from the game"): starts battle
        /// <paramref name="battleId"/> (<see cref="ArgumentBattles.Ids"/>: "walt_night1", "lena_night2") with what the game
        /// passes in (the journal ids found, which gate the cards; Force; Despair), and calls <paramref name="done"/> with
        /// the result once the end card is closed. The result is already in the save's ledger. Null for an unknown id or
        /// without the shell.
        /// </summary>
        public static ArgumentHost BeginBattle(string battleId, ArgumentContext context, Action<ArgumentResult> done)
        {
            ArgumentContent.Ensure();
            var def = ArgumentBattles.Build(battleId);
            if (def == null)
            {
                Debug.LogWarning("Argument: no battle \"" + battleId + "\" (known: " + string.Join(", ", ArgumentBattles.Ids) + ")");
                return null;
            }
            return Begin(def, ArgumentBattles.Setup(context), record => done?.Invoke(ArgumentBattles.Result(def, record)));
        }

        /// <summary>Starts a battle now (replacing one already running), in <paramref name="presenter"/> (null: the
        /// <see cref="DefaultPresenter"/>). Null without the shell.</summary>
        public static ArgumentHost Begin(ArgumentDef def, ArgumentSetup setup, Action<ArgumentRecord> onClosed = null, ArgumentPresenter? presenter = null)
        {
            ArgumentContent.Ensure();
            if (!GameRoot.Exists || def == null) return null;
            if (_current != null) _current.Close(false);
            var go = new GameObject("Argument: " + def.id);
            var host = go.AddComponent<ArgumentHost>();
            host.Init(def, setup ?? new ArgumentSetup { allEvidence = true }, onClosed, presenter ?? DefaultPresenter);
            return host;
        }

        void Init(ArgumentDef def, ArgumentSetup setup, Action<ArgumentRecord> onClosed, ArgumentPresenter presenter)
        {
            _current = this;
            _onClosed = onClosed;
            if (setup.ledger == null) setup.ledger = LoadLedger();
            _engine = new ArgumentEngine(def, setup);
            _flow = new BattleFlow(_engine, presenter);
            _flow.Expand = ExpandKeys;
            _stage = new ArgumentStage(GameRoot.Hud.Root.parent ?? GameRoot.Ui.Root);
            _stage.ReasonClicked = i => _pick = i;
            _stage.ContinueClicked = () => _continueClicked = true;
            _audio = gameObject.AddComponent<ArgumentAudio>();
            _audio.Begin();
            // The first blip of every voice loaded before the first line (docs/redesign/perf.md, Battle 3).
            _audio.Prewarm(def.opponent, "nadia", "mags");
            var input = GameRoot.Input;
            input.BlockWorld(this, true);
            input.Bindings.SetMoveDpad(true);
            ShowControlRows(true);
            _stage.PlayIntro(() => _introDone = true);
            Debug.Log("JAMKIT_ARGUMENT begin " + def.id + " (" + setup.difficulty + ", " + presenter + ")");
        }

        /// <summary>The save's argument results (what later battles read).</summary>
        public static ArgumentLedger LoadLedger() =>
            GameRoot.Exists ? ArgumentLedger.Parse(GameRoot.Save.Data.GetEntry(ArgumentLedger.SaveKey)) : new ArgumentLedger();

        static void ShowControlRows(bool shown)
        {
            if (!GameRoot.Exists || GameRoot.Ui == null || GameRoot.Ui.Controls == null) return;
            foreach (var id in ControlBindings.ArgumentRows) GameRoot.Ui.Controls.SetRowShown(id, shown);
        }

        /// <summary>Ends it. A finished battle is saved and its record passed to the callback (even if it ended this
        /// frame); one closed before it ended calls back with null. The stage fades back to the world by itself.
        /// <paramref name="callBack"/> false: replaced, no callback.</summary>
        public void Close(bool callBack = true)
        {
            if (_closed) return;
            if (_engine != null && _engine.Ended) Save();
            _closed = true;
            var cb = _onClosed;
            _onClosed = null;
            if (_stage != null) { _stage.PlayOutro(); _stage = null; }
            _audio?.End();
            Cleanup();
            Debug.Log("JAMKIT_ARGUMENT end " + (_engine != null ? _engine.Def.id + " " + _engine.Outcome : "?") + (_flow != null
                ? " after " + (_flow.ActiveSeconds + _flow.TalkSeconds).ToString("0", CultureInfo.InvariantCulture) + " s (" + _flow.ActiveSeconds.ToString("0", CultureInfo.InvariantCulture) + " s in play"
                  + (_flow.Sections ? ": " + _flow.ArgueSeconds.ToString("0", CultureInfo.InvariantCulture) + " s arguing, " + _flow.DodgeSeconds.ToString("0", CultureInfo.InvariantCulture) + " s dodging" : "") + ")" : ""));
            if (gameObject != null) Destroy(gameObject, 1.2f); // the battle loop fades out first
            if (callBack) cb?.Invoke(_record);
        }

        void OnDestroy()
        {
            if (!_closed) { _stage?.Remove(); _stage = null; _audio?.End(); }
            Cleanup();
        }

        void Cleanup()
        {
            if (_current == this) _current = null;
            if (!GameRoot.Exists) return;
            GameRoot.Input.BlockWorld(this, false);
            GameRoot.Input.Bindings.SetMoveDpad(false);
            ShowControlRows(false);
        }

        /// <summary>The end card's Continue: closes and calls back with the record (the fade back to the world follows).</summary>
        public void Continue()
        {
            if (_engine != null && _engine.Ended) Close();
        }

        // ---------- Per frame ----------

        static bool InputAllowed =>
            GameRoot.Exists && GameRoot.Flow.Current == GameState.Playing && !GameRoot.IsLoadingScene && GameRoot.Ui.Top == null && !PresenterMode.BlocksGameplay;

        void Update()
        {
            if (_engine == null || _closed) return;
            float dt = Mathf.Min(0.1f, Time.unscaledDeltaTime);
            bool allowed = InputAllowed;
            BoilKeys(allowed);
            RefreshHints();
            var settings = GameRoot.Exists ? GameRoot.Settings : null;
            _flow.TextSpeed = settings != null ? Mathf.Max(0.5f, settings.textSpeed) : 1f;

            if (_case)
            {
                CaseInput(allowed);
            }
            else if (_introDone || _pilotFast)
            {
                if (_pilot != null)
                {
                    // Not paused under the shell's menus (the house's tests run it with the game playing).
                    if (allowed || _pilotFast)
                    {
                        int steps = _pilotSteps;
                        for (int i = 0; i < steps && !_flow.Over; i++)
                        {
                            _flow.Tick(dt, _pilot.Steer(_flow, dt));
                            Sounds(i == steps - 1);
                        }
                    }
                }
                else if (allowed)
                {
                    var input = ReadInput(dt);
                    _flow.Tick(dt, input);
                    Sounds(true);
                }
            }
            Heartbeat(dt);

            if (_flow.Over) EnterEnd();
            if (_endShown && (allowed || _pilot != null))
            {
                var input = GameRoot.Input;
                bool go = _continueClicked || (Time.unscaledTime - _endAt > 0.6f && (input.Submit.WasPressedThisFrame() || input.Fire.WasPressedThisFrame()));
                _continueClicked = false;
                _stage?.FocusContinue(true);
                if (go) { Close(); return; }
            }
            _stage?.Render(_flow, dt, _hints, _expressionOverride, settings != null && settings.reduceEffects, settings != null ? settings.screenShake : 1f);
        }

        void LateUpdate()
        {
            // The cursor is freed while she argues (a scene's own pointer lock waits). Shown for the mouse; on the pad
            // the shell keeps it hidden (PointerCursor), so it isn't shown here then.
            if (_closed || !InputAllowed) return;
            if (UnityEngine.Cursor.lockState != CursorLockMode.None) UnityEngine.Cursor.lockState = CursorLockMode.None;
            if (!UnityEngine.Cursor.visible && GameRoot.Input.HoverAllowed) UnityEngine.Cursor.visible = true;
        }

        /// <summary>F10 overrides her mood's boil with each preset in turn (then back to her mood), Shift+F10 goes back to
        /// her mood (debug keys; the preset and its values show over her).</summary>
        void BoilKeys(bool allowed)
        {
            var kb = Keyboard.current;
            if (kb == null || !allowed || _stage == null) return;
            if (!kb[ArgumentBoil.CycleKey].wasPressedThisFrame) return;
            if (kb.shiftKey.isPressed) ArgumentBoil.SetReacting(true);
            else ArgumentBoil.Next();
            _stage.ShowBoilLabel();
        }

        BattleInput ReadInput(float dt)
        {
            var input = GameRoot.Input;
            var b = BattleInput.None;
            Vector2 m = input.Move.ReadValue<Vector2>();
            b.move = new Vec(m.x, m.y);
            b.fire = input.Submit.WasPressedThisFrame() || input.Fire.WasPressedThisFrame();
            b.ask = input.ArgueAsk.WasPressedThisFrame() || input.AltFire.WasPressedThisFrame();
            b.concede = input.ArgueConcede.WasPressedThisFrame();
            b.concedeHeld = input.ArgueConcede.IsPressed();
            b.pressHeld = input.ArguePress.IsPressed();
            if (input.ArgueChallenge.WasPressedThisFrame()) b.cycle = 1;
            var pad = Gamepad.current;
            if (pad != null && pad.leftShoulder.wasPressedThisFrame) b.cycle = -1;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.qKey.wasPressedThisFrame) b.cycle = -1;
                if (kb.eKey.wasPressedThisFrame) b.cycle = 1;
            }
            var mouse = Mouse.current;
            if (mouse != null)
            {
                float wheel = mouse.scroll.ReadValue().y;
                if (wheel > 0.1f) b.cycle = -1;
                else if (wheel < -0.1f) b.cycle = 1;
                // Over the table, the light follows the mouse (at its own speed: the mouse can't teleport it).
                if (_stage != null && _stage.PointerOnTable && input.HoverAllowed && mouse.delta.ReadValue().sqrMagnitude > 0.01f) _followMouse = true;
                if (_followMouse && _stage != null && _stage.PointerOnTable && input.HoverAllowed) { b.hasPoint = true; b.point = _stage.PointerUnits; }
                if (b.move.LengthSquared() > 0.04f) _followMouse = false;
            }
            var step = _navInput.Step(input, dt);
            b.navY = _flow.State == BattleState.Because ? step.y : 0;
            if (_pick >= 0) { b.pick = _pick; _pick = -1; }
            if (input.ArgueRest.WasPressedThisFrame() && (_flow.State == BattleState.Debate || _flow.State == BattleState.Talk || _flow.State == BattleState.Argue)) OpenCase(true);
            return b;
        }

        bool _followMouse;

        void CaseInput(bool allowed)
        {
            if (!allowed || _stage == null) return;
            var input = GameRoot.Input;
            var step = _navInput.Step(input, Time.unscaledDeltaTime);
            if (step.y != 0) _stage.ScrollCaseFile(step.y);
            if (input.ArgueRest.WasPressedThisFrame() || input.Cancel.WasPressedThisFrame() || input.Submit.WasPressedThisFrame()) OpenCase(false);
        }

        /// <summary>Opens or closes the case file (the battle waits while it's open).</summary>
        public void OpenCase(bool open)
        {
            if (_stage == null || _case == open) return;
            _case = open;
            _stage.ShowCaseFile(_flow, open);
            _audio.Play(open ? "sfx_confirm" : "sfx_back");
        }

        // ---------- Sound that follows the play ----------

        void Sounds(bool blips)
        {
            var cues = _flow.Cues;
            for (int i = 0; i < cues.Count; i++)
            {
                var c = cues[i];
                switch (c.kind)
                {
                    case CueKind.Blip:
                    case CueKind.SlowBlip:
                        if (blips) _audio.Blip(Voice(c), c.kind == CueKind.SlowBlip, _flow.Delivery.Current.repeat || _flow.Delivery.Current.statement != 0);
                        break;
                    case CueKind.LineStart: if (c.value != 0) _audio.Play("sfx_statement", 0.6f); break;
                    case CueKind.Fire: _audio.Play("sfx_fire", 0.7f); break;
                    case CueKind.Miss: _audio.Play("sfx_miss", 0.6f); break;
                    case CueKind.Glance: _audio.Play("sfx_glance"); break;
                    case CueKind.Burned: _audio.Play("sfx_burn", 0.7f); break;
                    case CueKind.WrongCard: _audio.Play("sfx_wrong"); break;
                    case CueKind.HeartUp: _audio.Play("sfx_hit"); _audio.Play("sfx_heartbeat", 0.9f); break;
                    case CueKind.HeartDown: _audio.Play("sfx_confirm", 0.4f); break;
                    case CueKind.Break: _audio.Play("sfx_break"); break;
                    case CueKind.BecauseOpen: _audio.Play("sfx_pick"); break;
                    case CueKind.BecauseMove: _audio.Play("sfx_nav"); break;
                    case CueKind.BecausePick: _audio.Play("sfx_confirm"); break;
                    case CueKind.Hesitate: _audio.Play("sfx_wrong"); break;
                    case CueKind.ShowdownStart: _audio.Play("sfx_angry"); break;
                    case CueKind.Cut: _audio.Play("sfx_cut"); break;
                    case CueKind.Whiff: _audio.Play("sfx_whiff"); break;
                    case CueKind.WordLanded: _audio.Play("sfx_hit", 0.8f); break;
                    case CueKind.ShowdownWon: _audio.Play("sfx_angry", 0.8f); break;
                    case CueKind.PressureIn: _audio.Play("sfx_pressure_in"); break;
                    case CueKind.PressureOut: _audio.Play("sfx_pressure_out"); break;
                    case CueKind.Warn: _audio.Play("sfx_warn", 0.45f); break;
                    case CueKind.SlipCaught: _audio.Play("sfx_slip"); break;
                    case CueKind.Ring: _audio.Play("sfx_ring"); break;
                    case CueKind.Panic: _audio.Play("sfx_panic"); break;
                    case CueKind.End: _audio.Play(_engine.Outcome == ArgumentOutcome.Convinced || _engine.Outcome == ArgumentOutcome.HeardOut ? "sfx_end_good" : "sfx_end_bad"); break;
                    case CueKind.Cycle: _audio.Play("sfx_cycle", 0.6f); break;
                    case CueKind.HoldStart: _audio.Play("sfx_hold", 0.6f); break;
                    case CueKind.Ask:
                    case CueKind.Concede:
                    case CueKind.Rest: _audio.Play("sfx_confirm"); break;
                    case CueKind.Retort: _audio.Play("sfx_angry"); break;
                    case CueKind.Yield: _audio.Play("sfx_back"); break;
                    case CueKind.Unbreakable: _audio.Play("sfx_wrong", 0.5f); break;
                    case CueKind.Idle: _audio.Play("sfx_angry", 0.5f); break;
                    // The sections presenter.
                    case CueKind.Select: _audio.Play("sfx_nav", 0.5f); break;
                    case CueKind.QuoteOpen: _audio.Play("sfx_warn", 0.35f); break;
                    case CueKind.QuoteCaught: _audio.Play("sfx_slip"); break;
                    case CueKind.QuoteMissed: if (c.value == 0) _audio.Play("sfx_miss", 0.6f); break;
                    case CueKind.Aside: _audio.Play("sfx_confirm", 0.6f); break;
                    case CueKind.SkyOpens: _audio.Play("sfx_end_good", 0.55f); break;
                    case CueKind.Hush: _audio.Play("sfx_pressure_out", 0.5f); break;
                }
            }
        }

        string Voice(in Cue c)
        {
            switch (c.speaker)
            {
                case Speaker.Nadia: return "nadia";
                case Speaker.Supporter: return "mags";
                default:
                    if (string.IsNullOrEmpty(c.id)) return "lena";
                    // Lowered once per speaker, not per blip (docs/redesign/perf.md, Battle 3).
                    if (!ReferenceEquals(c.id, _voiceId)) { _voiceId = c.id; _voiceLower = c.id.ToLowerInvariant(); }
                    return _voiceLower;
            }
        }

        /// <summary>Her heart, heard: once it's up, a beat at its rate, louder as it climbs.</summary>
        void Heartbeat(float dt)
        {
            int step = _engine.HeartStep;
            bool panic = _flow.State == BattleState.Panic;
            if (step < 2 && !panic) { _beat = 0f; return; }
            _beat -= dt;
            if (_beat > 0f) return;
            _beat = 60f / Mathf.Max(60, _engine.HeartRate);
            _audio.Play("sfx_heartbeat", panic ? 1f : 0.25f + 0.15f * step);
        }

        // ---------- Keys in the hints and Mags's lines ----------

        void RefreshHints()
        {
            var bindings = GameRoot.Input.Bindings;
            if (bindings.Version == _keysVersion && Gamepad.current == _keysPad) return;
            _keysVersion = bindings.Version;
            _keysPad = Gamepad.current;
            _style = ControlNames.DetectPadStyle();
            _hints = S.Format("screen.hint.debate", "ask", Keys("ask"), "concede", Keys("concede"), "show", Keys("show"), "press", Keys("press"), "cards", Keys("cards"), "case", Keys("case"));
            _argueHints = S.Format("screen.hint.argue", "move", Keys("pick"), "ask", Keys("ask"), "concede", Keys("concede"), "show", Keys("show"), "press", Keys("press"), "cards", Keys("cards"), "case", Keys("case"));
            _quotePrompt = S.Format("screen.quote.prompt", "show", Keys("show"));
            if (_stage != null) { _stage.ArgueHints = _argueHints; _stage.QuotePrompt = _quotePrompt; }
        }

        string Keys(string what)
        {
            var b = GameRoot.Input.Bindings;
            bool ps = _style == PadStyle.PlayStation;
            switch (what)
            {
                case "ask": return Or(b.KeysText("argue-ask", _style), "1 or X");
                case "concede": return Or(b.KeysText("argue-concede", _style), "3 or Y");
                case "press": return Or(b.KeysText("argue-press", _style), "4 or B");
                case "cards": return "Q / E or " + (ps ? "L1 / R1" : "LB / RB");
                case "case": return Or(b.KeysText("argue-rest", _style), "5 or View");
                case "show": return "Enter or " + (ps ? "Cross" : "A");
                case "move": return "WASD or the left stick";
                case "pick": return "WASD, the arrows, the d-pad or the left stick";
                default: return what;
            }
        }

        static string Or(string a, string b) => string.IsNullOrEmpty(a) ? b : a;

        string ExpandKeys(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('{') < 0) return text;
            return text.Replace("{ask}", Keys("ask")).Replace("{concede}", Keys("concede")).Replace("{show}", Keys("show"))
                .Replace("{press}", Keys("press")).Replace("{cards}", Keys("cards")).Replace("{move}", Keys("move"));
        }

        // ---------- The end ----------

        void Save()
        {
            if (_saved) return;
            _record = _engine.Record();
            if (!GameRoot.Exists) return;
            _saved = true;
            var ledger = LoadLedger();
            ledger.Set(_record);
            GameRoot.Save.Data.SetEntry(ArgumentLedger.SaveKey, ledger.Serialize());
            // Heard out with Lena: her watch is Nadia's (the save's flag the nights and the ward read).
            if (ArgumentBattles.RecordKeepsake(GameRoot.Save.Data, _record)) Debug.Log("JAMKIT_ARGUMENT keepsake: Lena's watch is Nadia's (" + ArgumentBattles.KeepsakeFlag + ")");
            GameRoot.Save.Save();
            _record = ledger.Get(_record.id).Clone();
        }

        void EnterEnd()
        {
            if (_endShown) return;
            _endShown = true;
            _endAt = Time.unscaledTime;
            Save();
            var def = _engine.Def;
            var r = _record;
            _sb.Clear();
            _sb.Append(S.Format("screen.end.stance", "opponent", def.opponent, "stance", ArgumentEngine.StanceWords(r.stance, S)));
            if (r.truth.Length > 0) _sb.Append('\n').Append(S.Format("screen.end.truth", "reason", def.truthText));
            else if (r.personal.Length > 0) _sb.Append('\n').Append(S.Format("screen.end.personal", "reason", def.personalText));
            _sb.Append("\n\n").Append(S.Format("screen.end.tally", "n", r.keysBroken.ToString(CultureInfo.InvariantCulture), "keys", r.presses.ToString(CultureInfo.InvariantCulture)));
            _sb.Append('\n').Append(S.Format("screen.end.time", "n", Mathf.RoundToInt((_flow.ActiveSeconds + _flow.TalkSeconds) / 60f).ToString(CultureInfo.InvariantCulture)));
            _sb.Append('\n').Append(S["screen.end.saved"]);
            var result = ArgumentBattles.Result(def, r);
            string cost = "";
            if (r.outcome == ArgumentOutcome.ComposureLost) cost = S.Format("screen.end.left", "opponent", def.opponent);
            if (result.despairAdded > 0) cost += (cost.Length > 0 ? "\n" : "") + S.Format("screen.end.despair", "n", result.despairAdded.ToString(CultureInfo.InvariantCulture));
            if (result.forceAdded > 0) cost += (cost.Length > 0 ? "\n" : "") + S.Format("screen.end.force", "n", result.forceAdded.ToString(CultureInfo.InvariantCulture));
            bool good = r.outcome == ArgumentOutcome.Convinced || r.outcome == ArgumentOutcome.HeardOut;
            _stage?.ShowEnd(S["outcome." + r.outcome], _sb.ToString(), cost, S["screen.end.continue"], good);
        }
    }
}
