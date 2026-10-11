using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using JamKit.Flow;
using JamKit.Presenting;
using JamKit.Shell;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Vec = System.Numerics.Vector2;

namespace JamKit.Argument
{
    /// <summary>The host's coarse state (kept for callers and tests); <see cref="ArgumentHost.Screen"/> is the fine one.</summary>
    public enum ArgueMode { Browse, PickEvidence, PickReason, Pressure, Ended }

    /// <summary>Which screen of the battle is up.</summary>
    public enum ArgueScreen { Intro, Browse, Verbs, PickEvidence, PickReason, Transcript, BoxIn, Pressure, BoxOut, End }

    /// <summary>
    /// Runs one argument battle in whatever scene is loaded (docs/ARGUMENT-PROTOTYPE.md): the engine, its screen,
    /// sound, keys and pads through the shell's actions, the save. The entry point:
    /// <code>ArgumentHost.Begin(DreamLenaDemo.Build(), new ArgumentSetup { allEvidence = true }, record => { ... });</code>
    /// While it runs: world gameplay is blocked (InputService.BlockWorld), the world's ambience and music give way to
    /// the battle's (ArgumentAudio), the cursor is free, the d-pad steers Nadia's light, the argument's Controls rows
    /// show. When it ends the result goes into the save (ArgumentLedger, "vh_arguments"); Continue on the end card
    /// calls back with the record at once and the screen fades back to the world by itself.
    ///
    /// One focus model everywhere (ArgueNav): stick / d-pad / arrows move, A / Enter chooses, B / Backspace goes back.
    /// The premises: choose one (A) to open its verbs (Ask, Challenge, Concede, Press by holding A, Let it rest,
    /// Transcript); Challenge opens the evidence, then the reasons. Shortcuts on the premise list: Ask X / 1,
    /// Challenge RB / 2, Concede Y / 3, Press hold B / 4, Let it rest View / 5 (all rebindable, Settings > Controls).
    /// </summary>
    public sealed class ArgumentHost : MonoBehaviour
    {
        public const float PressHold = 0.6f;
        const string RetortId = "\u0001retort";

        static ArgumentHost _current;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _current = null;

        /// <summary>The battle running now, or null.</summary>
        public static ArgumentHost Current => _current;
        public static bool Running => _current != null;

        ArgumentEngine _engine;
        ArgumentView _view;
        ArgumentAudio _audio;
        Action<ArgumentRecord> _onClosed;
        ArgumentRecord _record;
        ArgueScreen _screen = ArgueScreen.Intro;
        bool _autopilot, _closed, _saved;
        float _pilotWait = 0.8f;
        readonly ArgumentPilot _pilot = new ArgumentPilot();
        readonly ArgueNav _nav = new ArgueNav();
        readonly NavInput _navInput = new NavInput();
        string _selected = ArgumentEngine.ClaimId;
        readonly List<EvidenceDef> _tray = new List<EvidenceDef>(16);
        readonly List<string> _trayIds = new List<string>(16);
        readonly List<ReasonCard> _cards = new List<ReasonCard>(5);
        string _evidence = "";
        float _holdTime;
        VisualElement _pointerHold;
        int _lastHeart, _lastVisible, _shownTranscript = -1, _shownSeconds = -1, _keysVersion = -1;
        Gamepad _keysPad;
        bool _dirty = true;
        readonly StringBuilder _sb = new StringBuilder(256);

        public ArgumentEngine Engine => _engine;
        public ArgumentView View => _view;
        public ArgumentAudio Audio => _audio;
        public ArgueScreen Screen => _screen;
        public ArgumentRecord Result => _record;
        /// <summary>The premise (or ArgumentEngine.ClaimId) the verbs act on: the focused card, or the one chosen.</summary>
        public string Selected => _selected;
        /// <summary>The id of the focused item (a premise id, "claim", a verb, an evidence id, "reason0".., "continue").</summary>
        public string FocusedId => _nav.FocusedId;
        public bool Ended => _engine != null && _engine.Ended;
        /// <summary>The reason cards on screen (PickReason), in their shown order.</summary>
        public IReadOnlyList<ReasonCard> Cards => _cards;
        /// <summary>The tray's ids on screen (PickEvidence), in their shown order (the retort card last, if any).</summary>
        public IReadOnlyList<string> TrayIds => _trayIds;

        public ArgueMode Mode
        {
            get
            {
                switch (_screen)
                {
                    case ArgueScreen.PickEvidence: return ArgueMode.PickEvidence;
                    case ArgueScreen.PickReason: return ArgueMode.PickReason;
                    case ArgueScreen.Intro:
                    case ArgueScreen.BoxIn:
                    case ArgueScreen.Pressure:
                    case ArgueScreen.BoxOut: return ArgueMode.Pressure;
                    case ArgueScreen.End: return ArgueMode.Ended;
                    default: return ArgueMode.Browse;
                }
            }
        }

        /// <summary>The autopilot plays it (ArgumentPilot and PressurePilot): attract mode, "watch it play", tests.</summary>
        public bool Autopilot
        {
            get => _autopilot;
            set => _autopilot = value;
        }

        static ArgumentStrings S => ArgumentStrings.Shared;

        /// <summary>Starts a battle now (replacing one already running). Null without the shell.</summary>
        public static ArgumentHost Begin(ArgumentDef def, ArgumentSetup setup, Action<ArgumentRecord> onClosed = null)
        {
            ArgumentContent.Ensure();
            if (!GameRoot.Exists || def == null) return null;
            if (_current != null) _current.Close(false);
            var go = new GameObject("Argument: " + def.id);
            var host = go.AddComponent<ArgumentHost>();
            host.Init(def, setup ?? new ArgumentSetup { allEvidence = true }, onClosed);
            return host;
        }

        void Init(ArgumentDef def, ArgumentSetup setup, Action<ArgumentRecord> onClosed)
        {
            _current = this;
            _onClosed = onClosed;
            if (setup.ledger == null) setup.ledger = LoadLedger();
            _engine = new ArgumentEngine(def, setup);
            _engine.Start();
            _lastHeart = _engine.HeartStep;
            _view = new ArgumentView(GameRoot.Hud.Root.parent ?? GameRoot.Ui.Root);
            _view.Hovered = OnHover;
            _view.Clicked = OnClick;
            _view.PointerHeld = OnPointerHeld;
            _audio = gameObject.AddComponent<ArgumentAudio>();
            _audio.Begin();
            var input = GameRoot.Input;
            input.BlockWorld(this, true);
            input.Bindings.SetMoveDpad(true);
            ShowControlRows(true);
            for (int i = 0; i < def.premises.Count; i++)
                if (_engine.Status(def.premises[i].id) != PremiseStatus.Hidden) { _selected = def.premises[i].id; break; }
            _screen = ArgueScreen.Intro;
            Refresh();
            _view.PlayIntro(() => { _screen = ArgueScreen.Browse; Enter(ArgueScreen.Browse); });
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
        /// frame); one closed before it ended calls back with null. The screen fades back to the world by itself.
        /// <paramref name="callBack"/> false: replaced, no callback.</summary>
        public void Close(bool callBack = true)
        {
            if (_closed) return;
            if (_engine != null && _engine.Ended && _screen != ArgueScreen.End) EnterEnd();
            _closed = true;
            var cb = _onClosed;
            _onClosed = null;
            if (_view != null) { _view.PlayOutro(); _view = null; }
            _audio?.End();
            Cleanup();
            if (gameObject != null) Destroy(gameObject, 1.2f); // the battle loop fades out first
            if (callBack) cb?.Invoke(_record);
        }

        void OnDestroy()
        {
            if (!_closed) { _view?.Remove(); _view = null; _audio?.End(); }
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

        // ---------- Per frame ----------

        static bool InputAllowed =>
            GameRoot.Exists && GameRoot.Flow.Current == GameState.Playing && !GameRoot.IsLoadingScene && GameRoot.Ui.Top == null && !PresenterMode.BlocksGameplay;

        void Update()
        {
            if (_engine == null || _closed) return;
            float dt = Time.deltaTime;
            _view.Tick(Time.unscaledDeltaTime);
            var input = GameRoot.Input;
            bool allowed = InputAllowed;

            if (!_view.Busy)
            {
                if (_engine.Ended && _screen != ArgueScreen.End && _screen != ArgueScreen.Pressure) EnterEnd();
                else if (_engine.InPressure && _screen != ArgueScreen.Pressure && _screen != ArgueScreen.BoxIn && _screen != ArgueScreen.Intro && _screen != ArgueScreen.End)
                {
                    _screen = ArgueScreen.BoxIn;
                    _nav.Clear();
                    _audio.Play("sfx_pressure_in");
                    _view.OpenBox(() => { _screen = ArgueScreen.Pressure; _dirty = true; });
                }
            }

            switch (_screen)
            {
                case ArgueScreen.Pressure:
                    TickPressure(input, allowed, dt);
                    break;
                case ArgueScreen.Intro:
                case ArgueScreen.BoxIn:
                case ArgueScreen.BoxOut:
                    break;
                default:
                    if (_view.Busy) break;
                    if (_autopilot) TickPilot(dt);
                    else if (allowed) TickInput(input, dt);
                    break;
            }
            Sounds();
            Refresh();
        }

        void LateUpdate()
        {
            // The cursor is freed while she argues (a scene's own pointer lock waits). Shown for the mouse; on the pad
            // the shell keeps it hidden (PointerCursor), so it isn't shown here then.
            if (_closed || !InputAllowed) return;
            if (UnityEngine.Cursor.lockState != CursorLockMode.None) UnityEngine.Cursor.lockState = CursorLockMode.None;
            if (!UnityEngine.Cursor.visible && GameRoot.Input.HoverAllowed) UnityEngine.Cursor.visible = true;
        }

        void TickPressure(InputService input, bool allowed, float dt)
        {
            Vec move = default;
            if (_autopilot) move = _engine.Pressure != null ? PressurePilot.Steer(_engine.Pressure) : default;
            else if (allowed)
            {
                Vector2 m = input.Move.ReadValue<Vector2>();
                move = new Vec(m.x, m.y);
            }
            if (allowed || _autopilot) _engine.Tick(dt, move);
            _view.SetPhase(_engine.Pressure);
            if (_engine.InPressure) return;
            // The phase is over (or her heart gave out): the box collapses, the claims come back.
            _screen = ArgueScreen.BoxOut;
            _audio.Play("sfx_pressure_out");
            _view.SetPhase(null);
            _view.CloseBox(() =>
            {
                if (_engine.Ended) EnterEnd();
                else Enter(ArgueScreen.Browse);
            });
        }

        void TickPilot(float dt)
        {
            if (_screen == ArgueScreen.End) return;
            _pilotWait -= dt;
            if (_pilotWait > 0f) return;
            _pilotWait = 1.2f; // slow enough to read
            if (_pilot.Step(_engine))
            {
                _audio.Play("sfx_confirm");
                Enter(ArgueScreen.Browse);
            }
            _dirty = true;
        }

        void TickInput(InputService input, float dt)
        {
            var step = _navInput.Step(input, Time.unscaledDeltaTime);
            bool submit = input.Submit.WasPressedThisFrame();
            bool submitHeld = input.Submit.IsPressed();
            bool back = input.Cancel.WasPressedThisFrame();

            if (_screen == ArgueScreen.Transcript)
            {
                if (step.y != 0) _view.ScrollTranscript(step.y);
                if (back) { _audio.Play("sfx_back"); Enter(ArgueScreen.Verbs); }
                return;
            }

            if (step != Vector2Int.zero && _nav.Move(step.x, step.y))
            {
                _audio.Play("sfx_nav");
                if (_screen == ArgueScreen.Browse) _selected = _nav.FocusedId;
                _dirty = true;
            }

            var focused = _nav.Focused;
            // Press: hold A on the Press verb, or the Press shortcut (hold B / 4) on the premise list, or the mouse.
            bool holding = (focused != null && focused.hold && (submitHeld || _pointerHold == focused.element))
                           || (_screen == ArgueScreen.Browse && input.ArguePress.IsPressed());
            TickHold(holding, dt);

            if (_screen == ArgueScreen.Browse)
            {
                // The shortcuts act on the focused premise.
                if (input.ArgueAsk.WasPressedThisFrame()) { Do("ask"); return; }
                if (input.ArgueConcede.WasPressedThisFrame()) { Do("concede"); return; }
                if (input.ArgueRest.WasPressedThisFrame()) { Do("rest"); return; }
                if (input.ArgueChallenge.WasPressedThisFrame()) { Do("challenge"); return; }
            }
            if (submit && focused != null && !focused.hold) { Activate(focused); return; }
            if (back) Back();
        }

        void TickHold(bool held, float dt)
        {
            if (!held)
            {
                if (_holdTime > 0f) { _holdTime = 0f; _view.SetPressProgress("press", 0f); }
                return;
            }
            _holdTime += dt;
            _view.SetPressProgress("press", Mathf.Clamp01(_holdTime / PressHold));
            if (_holdTime < PressHold) return;
            _holdTime = 0f;
            _pointerHold = null;
            _view.SetPressProgress("press", 0f);
            Do("press");
        }

        // ---------- Screens ----------

        /// <summary>Opens a screen: builds its focus lines and focuses the right item (never left without one).</summary>
        void Enter(ArgueScreen screen)
        {
            if (_engine.Ended && screen != ArgueScreen.End) { EnterEnd(); return; }
            if (_engine.InPressure && screen == ArgueScreen.Browse) { _screen = ArgueScreen.Browse; _dirty = true; return; }
            _screen = screen;
            _nav.Clear();
            _navInput.Reset();
            _view.HideOverlays();
            var def = _engine.Def;
            switch (screen)
            {
                case ArgueScreen.Browse:
                {
                    _nav.AddLine(new NavItem { id = ArgumentEngine.ClaimId, element = _view.ClaimCard, activate = () => Choose(ArgumentEngine.ClaimId) });
                    var line = new List<NavItem>(2);
                    for (int i = 0; i < def.premises.Count && i < ArgumentView.MaxPremises; i++)
                    {
                        var p = def.premises[i];
                        bool visible = _engine.Status(p.id) != PremiseStatus.Hidden;
                        string id = p.id;
                        line.Add(new NavItem { id = id, element = _view.PremiseCard(i), activate = () => Choose(id), enabled = visible });
                        if (line.Count == 2) { _nav.AddLine(line); line.Clear(); }
                    }
                    if (line.Count > 0) _nav.AddLine(line);
                    if (!_nav.Focus(_selected)) _nav.FocusFirst();
                    _selected = _nav.FocusedId;
                    break;
                }
                case ArgueScreen.Verbs:
                {
                    string sel = _selected;
                    bool claim = sel == ArgumentEngine.ClaimId;
                    _nav.AddLine(
                        new NavItem { id = "ask", element = _view.Verb("ask"), activate = () => Do("ask"), enabled = !claim && _engine.CanAsk(sel) },
                        new NavItem { id = "challenge", element = _view.Verb("challenge"), activate = () => Do("challenge"), enabled = !claim && _engine.CanChallenge(sel) },
                        new NavItem { id = "concede", element = _view.Verb("concede"), activate = () => Do("concede"), enabled = claim ? _engine.CanConcedeClaim : _engine.CanConcede(sel) },
                        new NavItem { id = "press", element = _view.Verb("press"), hold = true, enabled = _engine.CanPress(sel) },
                        new NavItem { id = "rest", element = _view.Verb("rest"), activate = () => Do("rest"), enabled = _engine.CanLetItRest },
                        new NavItem { id = "transcript", element = _view.Verb("transcript"), activate = () => Enter(ArgueScreen.Transcript) });
                    if (!(_nav.Focus("challenge") || _nav.Focus("ask") || _nav.Focus("concede") || _nav.Focus("rest"))) _nav.Focus("transcript");
                    break;
                }
                case ArgueScreen.PickEvidence:
                {
                    var names = new List<string>();
                    var notes = new List<string>();
                    foreach (var id in _trayIds)
                    {
                        if (id == RetortId) { names.Add(S["screen.retortCard"]); notes.Add("“" + def.Premise(_selected).retort.line + "”"); continue; }
                        var ev = def.Evidence(id);
                        names.Add(ev != null ? ev.name : id);
                        notes.Add(ev != null ? ev.note : "");
                    }
                    _view.ShowEvidence(S.Format("screen.prompt.pickEvidence", "premise", def.Premise(_selected).text), names, notes);
                    var line = new List<NavItem>(3);
                    for (int i = 0; i < _trayIds.Count; i++)
                    {
                        int index = i;
                        line.Add(new NavItem { id = _trayIds[i] == RetortId ? "retort" : _trayIds[i], element = _view.EvidenceCard(i), activate = () => ChooseEvidence(index) });
                        if (line.Count == 3) { _nav.AddLine(line); line.Clear(); }
                    }
                    if (line.Count > 0) _nav.AddLine(line);
                    _nav.FocusFirst();
                    break;
                }
                case ArgueScreen.PickReason:
                {
                    var texts = new List<string>();
                    foreach (var c in _cards) texts.Add((string.IsNullOrEmpty(c.supporter) ? "" : "<i>(" + c.supporter + ")</i>  ") + c.reason.text);
                    var ev = def.Evidence(_evidence);
                    _view.ShowReasons(S.Format("screen.prompt.reasonHeading", "premise", def.Premise(_selected).text, "evidence", ev != null ? ev.name : _evidence), texts);
                    for (int i = 0; i < _cards.Count; i++)
                    {
                        int index = i;
                        _nav.AddLine(new NavItem { id = "reason" + i, element = _view.ReasonCard(i), activate = () => ChooseReason(index) });
                    }
                    _nav.FocusFirst();
                    break;
                }
                case ArgueScreen.Transcript:
                    _view.ShowTranscript(S["screen.transcriptTitle"], _engine.Transcript);
                    break;
                case ArgueScreen.End:
                    _nav.AddLine(new NavItem { id = "continue", element = _view.ContinueButton, activate = Continue });
                    _nav.FocusFirst();
                    break;
            }
            _nav.Highlight();
            _dirty = true;
        }

        void Back()
        {
            switch (_screen)
            {
                case ArgueScreen.Verbs: _audio.Play("sfx_back"); Enter(ArgueScreen.Browse); break;
                case ArgueScreen.PickEvidence: _audio.Play("sfx_back"); Enter(ArgueScreen.Verbs); break;
                case ArgueScreen.PickReason: _audio.Play("sfx_back"); Enter(ArgueScreen.PickEvidence); break;
                case ArgueScreen.Transcript: _audio.Play("sfx_back"); Enter(ArgueScreen.Verbs); break;
            }
        }

        void Activate(NavItem item)
        {
            if (item == null || !item.enabled || item.activate == null) return;
            item.activate();
        }

        /// <summary>A premise (or the claim) chosen on the list: its verbs open.</summary>
        void Choose(string id)
        {
            _selected = id;
            _audio.Play("sfx_confirm");
            Enter(ArgueScreen.Verbs);
        }

        // ---------- Verbs (keys, clicks, tests and the menus all come here) ----------

        /// <summary>Selects a premise by id (or ArgumentEngine.ClaimId) and focuses it on the list. Tests and clicks.</summary>
        public bool Select(string id)
        {
            if (id != ArgumentEngine.ClaimId && (_engine.Def.Premise(id) == null || _engine.Status(id) == PremiseStatus.Hidden)) return false;
            _selected = id;
            if (_screen != ArgueScreen.Pressure && _screen != ArgueScreen.BoxIn && _screen != ArgueScreen.BoxOut && _screen != ArgueScreen.End && _screen != ArgueScreen.Intro)
                Enter(ArgueScreen.Browse);
            return true;
        }

        public bool DoAsk() => Do("ask");
        public bool DoConcede() => Do("concede");
        public bool DoPress() => Do("press");
        public bool DoRest() => Do("rest");
        public bool DoChallenge() => Do("challenge");

        bool Do(string verb)
        {
            string id = _selected;
            bool claim = id == ArgumentEngine.ClaimId;
            bool ok;
            switch (verb)
            {
                case "ask":
                    ok = !claim && _engine.Ask(id);
                    return After(ok, ok ? "sfx_confirm" : "sfx_back", "screen.why.nothingToAsk");
                case "concede":
                    ok = claim ? _engine.ConcedeClaim() : _engine.Concede(id);
                    return After(ok, ok ? "sfx_confirm" : "sfx_back", "screen.why.cantConcede");
                case "press":
                    ok = _engine.Press(id);
                    return After(ok, ok ? "sfx_angry" : "sfx_back", "screen.why.cantPress");
                case "rest":
                    ok = _engine.LetItRest();
                    return After(ok, ok ? "sfx_confirm" : "sfx_back", "screen.why.notYet");
                case "challenge":
                    if (claim) return After(false, "sfx_back", "screen.why.claimNoChallenge");
                    if (!_engine.CanChallenge(id)) return After(false, "sfx_back", "screen.why.cantChallenge");
                    _tray.Clear();
                    _tray.AddRange(_engine.Tray());
                    _trayIds.Clear();
                    foreach (var e in _tray) _trayIds.Add(e.id);
                    if (_engine.CanRetort(id)) _trayIds.Add(RetortId);
                    _audio.Play("sfx_confirm");
                    Enter(ArgueScreen.PickEvidence);
                    return true;
            }
            return false;
        }

        string _why = "";

        bool After(bool ok, string sound, string whyNotKey)
        {
            _why = ok ? "" : S[whyNotKey];
            _audio.Play(sound);
            if (ok) Enter(ArgueScreen.Browse);
            _dirty = true;
            return ok;
        }

        /// <summary>Picks tray card <paramref name="index"/>: the reasons come up, or she answers at once.</summary>
        public bool ChooseEvidence(int index)
        {
            if (_screen != ArgueScreen.PickEvidence || index < 0 || index >= _trayIds.Count) return false;
            string id = _selected;
            string ev = _trayIds[index];
            _audio.Play("sfx_pick");
            if (ev == RetortId)
            {
                bool ok = _engine.Retort(id);
                if (ok) _audio.Play("sfx_angry");
                Enter(ArgueScreen.Browse);
                return ok;
            }
            var step = _engine.BeginChallenge(id, ev, _cards);
            if (step == ChallengeStep.Reasons)
            {
                _evidence = ev;
                Enter(ArgueScreen.PickReason);
                return true;
            }
            _audio.Play(step == ChallengeStep.NotAllowed ? "sfx_back" : "sfx_wrong");
            Enter(ArgueScreen.Browse);
            return step != ChallengeStep.NotAllowed;
        }

        /// <summary>States reason card <paramref name="index"/>.</summary>
        public bool ChooseReason(int index)
        {
            if (_screen != ArgueScreen.PickReason || index < 0 || index >= _cards.Count) return false;
            string id = _selected;
            var reason = _cards[index].reason;
            bool ok = _engine.ResolveChallenge(id, _evidence, reason);
            _audio.Play("sfx_pick");
            if (ok)
            {
                if (_engine.Status(id) == PremiseStatus.Broken) _audio.Play("sfx_break");
                else _audio.Play(reason.kind == ReasonKind.Antagonise ? "sfx_angry" : "sfx_wrong");
            }
            Enter(ArgueScreen.Browse);
            return ok;
        }

        /// <summary>The end card's Continue: closes and calls back with the record (the fade back to the world follows).</summary>
        public void Continue()
        {
            if (_engine != null && _engine.Ended) Close();
        }

        // ---------- Mouse ----------

        void OnHover(VisualElement e)
        {
            // On the pad the hidden cursor's hover mustn't take focus (a list moving under it).
            if (_view == null || _view.Busy || _autopilot || !GameRoot.Input.HoverAllowed) return;
            var item = _nav.Find(e);
            if (item == null || !item.enabled || _nav.Focused == item) return;
            _nav.Focus(item);
            if (_screen == ArgueScreen.Browse) _selected = item.id;
            _dirty = true;
        }

        void OnClick(VisualElement e)
        {
            if (_view == null || _view.Busy || _autopilot || !InputAllowed) return;
            var item = _nav.Find(e);
            if (item == null || !item.enabled || item.hold) return;
            _nav.Focus(item);
            Activate(item);
        }

        void OnPointerHeld(VisualElement e, bool down)
        {
            if (!down) { if (_pointerHold == e) _pointerHold = null; return; }
            var item = _nav.Find(e);
            _pointerHold = item != null && item.hold && item.enabled ? e : null;
        }

        // ---------- The end ----------

        void EnterEnd()
        {
            if (_screen == ArgueScreen.End) return;
            _record = _engine.Record();
            if (!_saved && GameRoot.Exists)
            {
                _saved = true;
                var ledger = LoadLedger();
                ledger.Set(_record);
                GameRoot.Save.Data.SetEntry(ArgumentLedger.SaveKey, ledger.Serialize());
                GameRoot.Save.Save();
                _record = ledger.Get(_record.id).Clone();
            }
            var def = _engine.Def;
            _sb.Clear();
            _sb.Append(S.Format("screen.end.stance", "opponent", def.opponent, "stance", ArgumentEngine.StanceWords(_record.stance, S)));
            if (_record.truth.Length > 0) _sb.Append('\n').Append(S.Format("screen.end.truth", "reason", def.truthText));
            else if (_record.personal.Length > 0) _sb.Append('\n').Append(S.Format("screen.end.personal", "reason", def.personalText));
            _sb.Append("\n\n").Append(S.Format("screen.end.tally", "n", _record.keysBroken.ToString(CultureInfo.InvariantCulture), "keys", _record.presses.ToString(CultureInfo.InvariantCulture)));
            _sb.Append('\n').Append(S["screen.end.saved"]);
            bool good = _record.outcome == ArgumentOutcome.Convinced || _record.outcome == ArgumentOutcome.HeardOut;
            _audio.Play(good ? "sfx_end_good" : "sfx_end_bad");
            _view?.ShowEnd(S["outcome." + _record.outcome], _sb.ToString(), S["screen.end.continue"]);
            if (_view != null) Enter(ArgueScreen.End);
            else _screen = ArgueScreen.End;
        }

        // ---------- Sound that follows the state ----------

        void Sounds()
        {
            int heart = _engine.HeartStep;
            if (heart > _lastHeart)
            {
                if (_screen == ArgueScreen.Pressure) _audio.Play("sfx_hit");
                _audio.Play("sfx_heartbeat", 0.9f);
            }
            _lastHeart = heart;
            int visible = _engine.VisiblePremises.Count;
            if (visible > _lastVisible && _screen == ArgueScreen.Pressure) _audio.Play("sfx_slip");
            _lastVisible = visible;
        }

        // ---------- The view ----------

        void Refresh()
        {
            if (_view == null) return;
            var e = _engine;
            var bindings = GameRoot.Input.Bindings;
            if (bindings.Version != _keysVersion || Gamepad.current != _keysPad) { _keysVersion = bindings.Version; _keysPad = Gamepad.current; _dirty = true; }
            if (e.Transcript.Count != _shownTranscript) { _shownTranscript = e.Transcript.Count; _dirty = true; }
            if (_screen == ArgueScreen.Pressure)
            {
                _view.Repaint();
                int left = e.Pressure != null ? Mathf.CeilToInt(e.Pressure.Duration - e.Pressure.Time) : 0;
                if (left != _shownSeconds) { _shownSeconds = left; _dirty = true; }
            }
            if (!_dirty) return;
            _dirty = false;
            var def = e.Def;
            var style = ControlNames.DetectPadStyle();

            // The opponent: name, expression (the last one they showed; the antagonistic opening pose before any), sprite.
            string expression = ArgumentArt.Opening;
            for (int i = e.Transcript.Count - 1; i >= 0; i--)
                if (e.Transcript[i].speaker == Speaker.Opponent && e.Transcript[i].expression.Length > 0) { expression = e.Transcript[i].expression; break; }
            _view.SetOpponent(def.opponent, expression, expression, S["screen.placeholder"]);
            _view.SetPlaceholderBadge(def.placeholderLines ? S["screen.placeholder"] : "");
            _view.SetVitals(S.Format("screen.heart", "bpm", e.HeartRate.ToString(CultureInfo.InvariantCulture)) + "  " + Pips(e.HeartStep, 5),
                e.HeartStep >= 4, S["screen.heat"] + " " + Pips(e.Heat, 3), e.Anger > 0 ? S["screen.anger"] + " " + Pips(e.Anger, 3) : "");

            int keys = e.KeysLeft;
            _view.SetClaim(S["screen.claim"], def.claim, keys == 1 ? S["screen.keyHolding"] : S.Format("screen.keysHolding", "n", keys.ToString(CultureInfo.InvariantCulture)));

            for (int i = 0; i < ArgumentView.MaxPremises; i++)
            {
                if (i >= def.premises.Count) { _view.SetPremise(i, null, null, null, null, null, PremiseStatus.Hidden); continue; }
                var p = def.premises[i];
                var st = e.Status(p.id);
                if (st == PremiseStatus.Hidden) { _view.SetPremise(i, S["screen.hidden"], "", "", S["screen.status.hidden"], S["screen.hiddenText"], st); continue; }
                _view.SetPremise(i, p.label, p.kind == PremiseKind.Fact ? S["screen.fact"] : S["screen.link"], p.key ? S["screen.key"] : "",
                    StatusWords(st, e.IsHeard(p.id)), p.text, st);
            }

            // The focus panel: the selected (or focused) premise, in full, with what she said about it.
            string focusId = _screen == ArgueScreen.Browse ? _nav.FocusedId : _selected;
            if (focusId == ArgumentEngine.ClaimId || def.Premise(focusId) == null)
                _view.SetFocusPanel(PromptFor(), S["screen.claim"], def.claim, "", "");
            else
            {
                var p = def.Premise(focusId);
                string said = e.TimesAsked(p.id) >= 2 && p.askAgainReply.Length > 0 ? p.askReply + "\n" + p.askAgainReply : e.TimesAsked(p.id) >= 1 ? p.askReply : "";
                if (p.question.Length > 0 && e.Status(p.id) == PremiseStatus.Standing) said = (said.Length > 0 ? said + "\n" : "") + S.Format("screen.prompt.question", "question", p.question);
                _view.SetFocusPanel(PromptFor(), p.label + "  ·  " + (p.kind == PremiseKind.Fact ? S["screen.fact"] : S["screen.link"]) + (p.key ? "  ·  " + S["screen.key"] : "") + "  ·  " + StatusWords(e.Status(p.id), e.IsHeard(p.id)),
                    p.text, S["screen.saidAbout"], said);
            }

            // The verbs, with their current keys.
            bool claim = _selected == ArgumentEngine.ClaimId;
            Verb("ask", S["screen.verb.ask"], "argue-ask", bindings, style);
            Verb("challenge", S["screen.verb.challenge"], "argue-challenge", bindings, style);
            Verb("concede", claim ? S["screen.verb.concedeClaim"] : S["screen.verb.concede"], "argue-concede", bindings, style);
            Verb("press", claim ? S["screen.verb.pressClaim"] : S["screen.verb.press"], "argue-press", bindings, style);
            Verb("rest", S["screen.verb.rest"], "argue-rest", bindings, style);
            _view.SetVerb("transcript", S["screen.verb.transcript"]);
            // What's possible for the focused (or chosen) premise, also while browsing: the row previews it.
            string vid = focusId;
            bool vclaim = vid == ArgumentEngine.ClaimId;
            VerbOn("ask", !vclaim && e.CanAsk(vid));
            VerbOn("challenge", !vclaim && e.CanChallenge(vid));
            VerbOn("concede", vclaim ? e.CanConcedeClaim : e.CanConcede(vid));
            VerbOn("press", e.CanPress(vid));
            VerbOn("rest", e.CanLetItRest);
            VerbOn("transcript", true);

            // The dialogue box: the last thing each said, and the step as a sentence.
            string opp = "", nadia = "", logic = "";
            for (int i = e.Transcript.Count - 1; i >= 0 && (opp.Length == 0 || nadia.Length == 0 || logic.Length == 0); i--)
            {
                var x = e.Transcript[i];
                if (x.speaker == Speaker.Opponent && opp.Length == 0) opp = "<b>" + x.who + ":</b>  " + x.text;
                else if (x.speaker == Speaker.Nadia && nadia.Length == 0) nadia = "<b>Nadia:</b>  " + x.text;
                else if (x.speaker == Speaker.Logic && logic.Length == 0) logic = x.text;
                else if (x.speaker == Speaker.Supporter && nadia.Length == 0) nadia = "<b>" + x.who + ":</b>  " + x.text;
            }
            _view.SetDialogue(opp, nadia, _why.Length > 0 ? _why : logic);
            _view.SetHint(Hint(bindings, style));
            if (_screen == ArgueScreen.Pressure && e.Pressure != null)
                _view.SetPressureLabel(S.Format(e.Pressure.Spec.anger > 0 ? "screen.prompt.pressureAngry" : "screen.prompt.pressure", "n", _shownSeconds.ToString(CultureInfo.InvariantCulture)));
            _nav.Highlight();
        }

        void VerbOn(string id, bool on)
        {
            var v = _view.Verb(id);
            if (v != null && _screen != ArgueScreen.Verbs) v.style.opacity = on ? 1f : 0.38f;
        }

        void Verb(string id, string name, string row, ControlBindings b, PadStyle style) =>
            _view.SetVerb(id, name + "\n<size=72%>" + b.KeysText(row, style) + "</size>");

        string PromptFor()
        {
            var def = _engine.Def;
            switch (_screen)
            {
                case ArgueScreen.PickEvidence: return S.Format("screen.prompt.pickEvidence", "premise", def.Premise(_selected)?.text);
                case ArgueScreen.PickReason: return S.Format("screen.prompt.pickReason", "evidence", def.Evidence(_evidence)?.name);
            }
            string id = _screen == ArgueScreen.Browse ? _nav.FocusedId : _selected;
            if (id == ArgumentEngine.ClaimId) return S["screen.prompt.claim"];
            var p = def.Premise(id);
            return p == null ? "" : S.Format("screen.prompt.premise", "premise", p.text);
        }

        string Hint(ControlBindings b, PadStyle style)
        {
            switch (_screen)
            {
                case ArgueScreen.Browse:
                    _sb.Clear();
                    _sb.Append(S["screen.verb.ask"]).Append(' ').Append(b.KeysText("argue-ask", style)).Append("   ");
                    _sb.Append(S["screen.verb.challenge"]).Append(' ').Append(b.KeysText("argue-challenge", style)).Append("   ");
                    _sb.Append(S["screen.verb.concede"]).Append(' ').Append(b.KeysText("argue-concede", style)).Append("   ");
                    _sb.Append(S["screen.verb.press"]).Append(' ').Append(b.KeysText("argue-press", style)).Append("   ");
                    _sb.Append(S["screen.verb.rest"]).Append(' ').Append(b.KeysText("argue-rest", style));
                    return S.Format("screen.hint.browse", "keys", _sb.ToString());
                case ArgueScreen.Verbs: return S["screen.hint.verbs"];
                case ArgueScreen.PickEvidence:
                case ArgueScreen.PickReason: return S["screen.hint.pick"];
                case ArgueScreen.Transcript: return S["screen.hint.transcript"];
                case ArgueScreen.Pressure: return S["screen.hint.pressure"];
                case ArgueScreen.End: return S["screen.hint.end"];
                default: return "";
            }
        }

        static string Pips(int n, int max)
        {
            var c = new char[max];
            for (int i = 0; i < max; i++) c[i] = i < n ? '●' : '○';
            return new string(c);
        }

        static string StatusWords(PremiseStatus s, bool heard)
        {
            switch (s)
            {
                case PremiseStatus.Broken: return S["screen.status.broken"];
                case PremiseStatus.Conceded: return S["screen.status.conceded"];
                case PremiseStatus.ConcededFalse: return S["screen.status.agreed"];
                case PremiseStatus.Forced: return S["screen.status.forced"];
                case PremiseStatus.Hidden: return S["screen.status.hidden"];
                default: return heard ? S["screen.status.heard"] : S["screen.status.standing"];
            }
        }
    }
}
