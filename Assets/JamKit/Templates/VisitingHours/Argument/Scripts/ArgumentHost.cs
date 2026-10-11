using System;
using System.Collections.Generic;
using JamKit.Flow;
using JamKit.Presenting;
using JamKit.Shell;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Vec = System.Numerics.Vector2;

namespace JamKit.Argument
{
    public enum ArgueMode { Browse, PickEvidence, PickReason, Pressure, Ended }

    /// <summary>
    /// Runs one argument battle in whatever scene is loaded (docs/ARGUMENT-PROTOTYPE.md): the engine, its screen,
    /// keys and pads through the shell's rebindable actions, the save. The entry point for anyone who wants a battle:
    /// <code>ArgumentHost.Begin(DreamLenaDemo.Build(), new ArgumentSetup { allEvidence = true }, record => { ... });</code>
    /// While it runs, world gameplay is blocked (InputService.BlockWorld: the player stands still), the cursor is free,
    /// the d-pad steers Nadia's light, and Settings > Controls shows the argument's rows. When it ends the result goes
    /// into the save (<see cref="ArgumentLedger"/>, entry "vh_arguments"); Continue on the end card closes it and calls
    /// back with the record.
    ///
    /// Controls (defaults; rebindable): arrows / stick / d-pad pick a premise; 1 or X Ask, 2 or A Challenge (then pick
    /// evidence, then a reason; Enter / A confirms, Backspace / B goes back), 3 or Y Concede, hold 4 or B for 0.6 s to
    /// Press, 5 or View to Let it rest. Select the claim to concede it (3) or press it (hold 4). Pressure phases: move
    /// the light with the stick, WASD, arrows or the d-pad. Mouse: click anything.
    /// </summary>
    public sealed class ArgumentHost : MonoBehaviour
    {
        public const float PressHold = 0.6f;
        const string RetortItem = "\u0001retort";

        static ArgumentHost _current;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _current = null;

        /// <summary>The battle running now, or null.</summary>
        public static ArgumentHost Current => _current;
        public static bool Running => _current != null;

        ArgumentEngine _engine;
        ArgumentView _view;
        Action<ArgumentRecord> _onClosed;
        ArgumentRecord _record;
        ArgueMode _mode;
        bool _autopilot, _closed, _saved;
        float _pilotWait;
        readonly ArgumentPilot _pilot = new ArgumentPilot();
        int _keysVersion = -1;
        Gamepad _keysPad;

        // The tags: index 0 is the claim, then each visible premise (hidden ones shown as "?" and skipped).
        readonly List<string> _items = new List<string>(16);
        int _selected;
        // Choosing: the tray (plus the retort card), then the reasons.
        readonly List<EvidenceDef> _tray = new List<EvidenceDef>(16);
        readonly List<string> _trayIds = new List<string>(16);
        readonly List<ReasonCard> _cards = new List<ReasonCard>(5);
        int _trayIndex, _cardIndex;
        string _evidence;
        float _pressHeld;
        bool _pointerPress;
        string _statusLine = "";
        int _shownTranscript = -1, _shownHeart = -1, _shownHeat = -1, _shownAnger = -1, _shownSeconds = -1;
        ArgueMode _shownMode = (ArgueMode)(-1);
        bool _dirty = true;

        // Scratch lists for the view (filled on refresh only).
        readonly List<string> _labels = new List<string>(16), _texts = new List<string>(16), _statuses = new List<string>(16), _names = new List<string>(16);
        readonly List<bool> _selectable = new List<bool>(16);

        public ArgumentEngine Engine => _engine;
        public ArgumentView View => _view;
        public ArgueMode Mode => _mode;
        /// <summary>The selected tag's id (ArgumentEngine.ClaimId for the claim).</summary>
        public string Selected => _selected >= 0 && _selected < _items.Count ? _items[_selected] : "";
        public ArgumentRecord Result => _record;
        /// <summary>The reason cards on screen (PickReason), in their shown order.</summary>
        public IReadOnlyList<ReasonCard> Cards => _cards;
        /// <summary>The tray's ids on screen (PickEvidence), in their shown order (the retort card last, if any).</summary>
        public IReadOnlyList<string> TrayIds => _trayIds;
        /// <summary>True once its end card is up.</summary>
        public bool Ended => _engine != null && _engine.Ended;

        /// <summary>The autopilot plays it (ArgumentPilot and PressurePilot): attract mode, the "watch it play" chapter.</summary>
        public bool Autopilot
        {
            get => _autopilot;
            set => _autopilot = value;
        }

        /// <summary>Starts a battle now (replacing one already running). Null without the shell.</summary>
        public static ArgumentHost Begin(ArgumentDef def, ArgumentSetup setup, Action<ArgumentRecord> onClosed = null)
        {
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
            _view = new ArgumentView(GameRoot.Hud.Root.parent ?? GameRoot.Ui.Root);
            _view.Bind(this);
            var input = GameRoot.Input;
            input.BlockWorld(this, true);
            input.Bindings.SetMoveDpad(true);
            ShowControlRows(true);
            _mode = ArgueMode.Pressure;
            Rebuild();
            for (int i = 1; i < _items.Count; i++)
                if (Selectable(i)) { _selected = i; break; }
            _dirty = true;
        }

        /// <summary>The save's argument results (what later battles read).</summary>
        public static ArgumentLedger LoadLedger() =>
            GameRoot.Exists ? ArgumentLedger.Parse(GameRoot.Save.Data.GetEntry(ArgumentLedger.SaveKey)) : new ArgumentLedger();

        static void ShowControlRows(bool shown)
        {
            if (!GameRoot.Exists || GameRoot.Ui == null || GameRoot.Ui.Controls == null) return;
            foreach (var id in ControlBindings.ArgumentRows) GameRoot.Ui.Controls.SetRowShown(id, shown);
        }

        /// <summary>Ends it and removes its screen (the record is already saved if the battle finished).
        /// <paramref name="callBack"/> false: replaced, no callback.</summary>
        public void Close(bool callBack = true)
        {
            if (_closed) return;
            _closed = true;
            var cb = _onClosed;
            _onClosed = null;
            if (gameObject != null) Destroy(gameObject);
            Cleanup();
            if (callBack) cb?.Invoke(_record);
        }

        void OnDestroy() => Cleanup();

        void Cleanup()
        {
            if (_current == this) _current = null;
            if (_view != null) { _view.Remove(); _view = null; }
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
            var input = GameRoot.Input;
            bool allowed = InputAllowed;
            float dt = Time.deltaTime;

            if (_engine.Ended)
            {
                if (_mode != ArgueMode.Ended) EnterEnd();
                if (allowed && (input.Submit.WasPressedThisFrame() || input.ArgueChallenge.WasPressedThisFrame())) Continue();
            }
            else if (_engine.InPressure)
            {
                _mode = ArgueMode.Pressure;
                Vec move = default;
                if (_autopilot) move = _engine.Pressure != null ? PressurePilot.Steer(_engine.Pressure) : default;
                else if (allowed)
                {
                    Vector2 m = input.Move.ReadValue<Vector2>();
                    move = new Vec(m.x, m.y);
                }
                if (allowed || _autopilot) _engine.Tick(dt, move);
                if (!_engine.InPressure && !_engine.Ended) { _mode = ArgueMode.Browse; _dirty = true; }
                _view.SetPhase(_engine.Pressure);
            }
            else
            {
                if (_mode == ArgueMode.Pressure) { _mode = ArgueMode.Browse; _dirty = true; }
                if (_autopilot) TickPilot(dt);
                else if (allowed) TickInput(input, dt);
            }
            Refresh();
        }

        void LateUpdate()
        {
            // The cursor is hers while she argues (a scene's own pointer lock waits).
            if (_closed || !InputAllowed) return;
            if (UnityEngine.Cursor.lockState != CursorLockMode.None) UnityEngine.Cursor.lockState = CursorLockMode.None;
            if (!UnityEngine.Cursor.visible) UnityEngine.Cursor.visible = true;
        }

        void TickPilot(float dt)
        {
            _pilotWait -= dt;
            if (_pilotWait > 0f) return;
            _pilotWait = 1.6f; // slow enough to read
            if (_pilot.Step(_engine)) _statusLine = "Autopilot: " + _pilot.LastAction;
            _dirty = true;
        }

        void TickInput(InputService input, float dt)
        {
            Vector2 nav = Vector2.zero;
            if (input.Navigate.WasPressedThisFrame()) nav = input.Navigate.ReadValue<Vector2>();
            bool submit = input.Submit.WasPressedThisFrame();
            bool back = input.Cancel.WasPressedThisFrame();
            switch (_mode)
            {
                case ArgueMode.Browse:
                    if (nav.sqrMagnitude > 0.25f) Step(Mathf.Abs(nav.x) >= Mathf.Abs(nav.y) ? Math.Sign(nav.x) : -Math.Sign(nav.y) * 4);
                    if (input.ArgueAsk.WasPressedThisFrame()) DoAsk();
                    else if (input.ArgueChallenge.WasPressedThisFrame() || submit) DoChallenge();
                    else if (input.ArgueConcede.WasPressedThisFrame()) DoConcede();
                    else if (input.ArgueRest.WasPressedThisFrame()) DoRest();
                    TickPress(input.ArguePress.IsPressed() || _pointerPress, dt);
                    break;
                case ArgueMode.PickEvidence:
                    if (nav.sqrMagnitude > 0.25f && _trayIds.Count > 0)
                    {
                        int d = Mathf.Abs(nav.x) >= Mathf.Abs(nav.y) ? Math.Sign(nav.x) : -Math.Sign(nav.y) * 5;
                        _trayIndex = Mathf.Clamp(_trayIndex + d, 0, _trayIds.Count - 1);
                        _dirty = true;
                    }
                    if (submit || input.ArgueChallenge.WasPressedThisFrame()) ChooseEvidence(_trayIndex);
                    else if (back) { _mode = ArgueMode.Browse; _dirty = true; }
                    break;
                case ArgueMode.PickReason:
                    if (nav.sqrMagnitude > 0.25f && _cards.Count > 0)
                    {
                        _cardIndex = Mathf.Clamp(_cardIndex - Math.Sign(nav.y) + Math.Sign(nav.x), 0, _cards.Count - 1);
                        _dirty = true;
                    }
                    if (submit || input.ArgueChallenge.WasPressedThisFrame()) ChooseReason(_cardIndex);
                    else if (back) { _mode = ArgueMode.PickEvidence; _dirty = true; }
                    break;
            }
        }

        void TickPress(bool held, float dt)
        {
            if (!held)
            {
                if (_pressHeld > 0f) { _pressHeld = 0f; _view.SetPressProgress(0f); }
                return;
            }
            _pressHeld += dt;
            _view.SetPressProgress(Mathf.Clamp01(_pressHeld / PressHold));
            if (_pressHeld < PressHold) return;
            _pressHeld = 0f;
            _pointerPress = false;
            _view.SetPressProgress(0f);
            DoPress();
        }

        // ---------- Verbs (keys, clicks and tests all come here) ----------

        void Step(int d)
        {
            if (_items.Count == 0) return;
            int i = _selected;
            for (int n = 0; n < _items.Count; n++)
            {
                i = Mathf.Clamp(i + (d == 0 ? 1 : d), 0, _items.Count - 1);
                if (Selectable(i)) { _selected = i; break; }
                if (i == 0 || i == _items.Count - 1) break;
            }
            _dirty = true;
        }

        bool Selectable(int i) => i >= 0 && i < _items.Count && (i == 0 || _engine.Status(_items[i]) != PremiseStatus.Hidden);

        /// <summary>Selects a tag by premise id (or ArgumentEngine.ClaimId). Tests and clicks.</summary>
        public bool Select(string id)
        {
            Rebuild();
            int i = _items.IndexOf(id);
            if (!Selectable(i)) return false;
            _selected = i;
            if (_mode == ArgueMode.PickEvidence || _mode == ArgueMode.PickReason) _mode = ArgueMode.Browse;
            _dirty = true;
            return true;
        }

        public bool DoAsk() => Did(_engine.Ask(Selected), "Nothing to ask there.");

        public bool DoConcede()
        {
            if (Selected == ArgumentEngine.ClaimId) return Did(_engine.ConcedeClaim(), "");
            return Did(_engine.Concede(Selected), "That can't be conceded now.");
        }

        public bool DoPress() => Did(_engine.Press(Selected), "That can't be pressed now.");

        public bool DoRest() => Did(_engine.LetItRest(), "Not yet: hear every premise first (hidden ones too).");

        /// <summary>Opens the evidence tray for the selected premise (with the retort card if they asked a question).</summary>
        public bool DoChallenge()
        {
            string id = Selected;
            if (id == ArgumentEngine.ClaimId) { _statusLine = "The claim stands on its premises: challenge those."; _dirty = true; return false; }
            if (!_engine.CanChallenge(id)) { _statusLine = "That can't be challenged now."; _dirty = true; return false; }
            _tray.Clear();
            _tray.AddRange(_engine.Tray());
            _trayIds.Clear();
            foreach (var e in _tray) _trayIds.Add(e.id);
            if (_engine.CanRetort(id)) _trayIds.Add(RetortItem);
            _trayIndex = 0;
            _mode = ArgueMode.PickEvidence;
            _dirty = true;
            return true;
        }

        /// <summary>Picks tray card <paramref name="index"/>: the reasons come up, or they answer at once.</summary>
        public bool ChooseEvidence(int index)
        {
            if (_mode != ArgueMode.PickEvidence || index < 0 || index >= _trayIds.Count) return false;
            string id = Selected;
            string ev = _trayIds[index];
            if (ev == RetortItem)
            {
                _mode = ArgueMode.Browse;
                return Did(_engine.Retort(id), "");
            }
            var step = _engine.BeginChallenge(id, ev, _cards);
            if (step == ChallengeStep.Reasons)
            {
                _evidence = ev;
                _cardIndex = 0;
                _mode = ArgueMode.PickReason;
                _dirty = true;
                return true;
            }
            _mode = ArgueMode.Browse;
            return Did(step != ChallengeStep.NotAllowed, "That can't be challenged now.");
        }

        /// <summary>States reason card <paramref name="index"/>.</summary>
        public bool ChooseReason(int index)
        {
            if (_mode != ArgueMode.PickReason || index < 0 || index >= _cards.Count) return false;
            bool ok = _engine.ResolveChallenge(Selected, _evidence, _cards[index].reason);
            _mode = ArgueMode.Browse;
            return Did(ok, "");
        }

        /// <summary>The end card's Continue: closes and calls back with the record.</summary>
        public void Continue()
        {
            if (_engine != null && _engine.Ended) Close();
        }

        bool Did(bool ok, string whyNot)
        {
            _statusLine = ok ? "" : whyNot;
            _dirty = true;
            return ok;
        }

        // Clicks from the view.
        public void ClickPremise(int index)
        {
            if (_mode == ArgueMode.Pressure || _engine.Ended || !Selectable(index)) return;
            _selected = index;
            if (_mode != ArgueMode.Browse) _mode = ArgueMode.Browse;
            _dirty = true;
        }

        public void ClickVerb(string verb)
        {
            if (_mode == ArgueMode.Pressure || _engine.Ended || !InputAllowed) return;
            _mode = ArgueMode.Browse;
            switch (verb)
            {
                case "ask": DoAsk(); break;
                case "challenge": DoChallenge(); break;
                case "concede": DoConcede(); break;
                case "rest": DoRest(); break;
            }
        }

        public void PointerPress(bool down) => _pointerPress = down && _mode == ArgueMode.Browse;
        public void ClickEvidence(int index) { if (InputAllowed) ChooseEvidence(index); }
        public void ClickReason(int index) { if (InputAllowed) ChooseReason(index); }

        // ---------- The end ----------

        void EnterEnd()
        {
            _mode = ArgueMode.Ended;
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
            string gift = _record.truth.Length > 0 ? "\nTruth: “" + def.truthText + "”"
                : _record.personal.Length > 0 ? "\nPersonal: " + def.personalText : "";
            string body = def.opponent + " is " + ArgumentEngine.StanceWords(_record.stance) + "." + gift
                + "\nKey premises broken: " + _record.keysBroken + ".  Presses: " + _record.presses + ".  Anger: " + _record.anger + "."
                + "\n(Kept in the save: a later battle reads it.)";
            _view.ShowEnd(Title(_record.outcome), body);
            _dirty = true;
        }

        static string Title(ArgumentOutcome o)
        {
            switch (o)
            {
                case ArgumentOutcome.Convinced: return "Convinced";
                case ArgumentOutcome.HeardOut: return "Heard out";
                case ArgumentOutcome.Pressed: return "Pressed";
                case ArgumentOutcome.ComposureLost: return "Composure lost";
                case ArgumentOutcome.Yielded: return "Yielded";
                default: return "Over";
            }
        }

        // ---------- The view ----------

        void Rebuild()
        {
            _items.Clear();
            _items.Add(ArgumentEngine.ClaimId);
            foreach (var p in _engine.Def.premises) if (p != null) _items.Add(p.id);
            if (!Selectable(_selected)) _selected = 0;
        }

        void Refresh()
        {
            if (_view == null) return;
            var e = _engine;
            if (e.HeartRate != _shownHeart || e.Heat != _shownHeat || e.Anger != _shownAnger)
            {
                _shownHeart = e.HeartRate;
                _shownHeat = e.Heat;
                _shownAnger = e.Anger;
                _view.SetVitals(e.HeartRate, e.HeartStep, e.Heat, e.Anger);
            }
            var bindings = GameRoot.Input.Bindings;
            if (bindings.Version != _keysVersion || Gamepad.current != _keysPad) { _keysVersion = bindings.Version; _keysPad = Gamepad.current; _dirty = true; }
            if (e.Transcript.Count != _shownTranscript) { _shownTranscript = e.Transcript.Count; _dirty = true; }
            if (_mode != _shownMode) { _shownMode = _mode; _dirty = true; }
            if (_mode == ArgueMode.Pressure)
            {
                _view.Repaint();
                int left = e.Pressure != null ? Mathf.CeilToInt(e.Pressure.Duration - e.Pressure.Time) : 0;
                if (left != _shownSeconds) { _shownSeconds = left; _dirty = true; }
            }
            if (!_dirty) return;
            _dirty = false;

            Rebuild();
            string expression = "";
            for (int i = e.Transcript.Count - 1; i >= 0; i--)
                if (e.Transcript[i].speaker == Speaker.Opponent && e.Transcript[i].expression.Length > 0) { expression = e.Transcript[i].expression; break; }
            _view.SetHeader(e.Def.opponent, expression, e.Def.claim, e.Def.placeholderLines);
            _view.SetTranscript(e.Transcript);
            _view.ShowPressure(_mode == ArgueMode.Pressure);
            if (_mode == ArgueMode.Pressure) _view.SetPhase(e.Pressure);

            _labels.Clear(); _texts.Clear(); _statuses.Clear(); _selectable.Clear();
            for (int i = 0; i < _items.Count; i++)
            {
                if (i == 0)
                {
                    _labels.Add("CLAIM");
                    _texts.Add(e.Def.claim);
                    _statuses.Add(e.KeysLeft + " key premise" + (e.KeysLeft == 1 ? "" : "s") + " holding it up");
                    _selectable.Add(true);
                    continue;
                }
                var p = e.Def.Premise(_items[i]);
                var s = e.Status(p.id);
                bool hidden = s == PremiseStatus.Hidden;
                _labels.Add(hidden ? "?" : p.label + (p.kind == PremiseKind.Fact ? "  ■ fact" : "  ∞ link") + (p.key ? "  KEY" : ""));
                _texts.Add(hidden ? "(something they aren't saying)" : s == PremiseStatus.Broken ? "<s>" + p.text + "</s>" : p.text);
                _statuses.Add(StatusWords(s, e.IsHeard(p.id)));
                _selectable.Add(!hidden);
            }
            _view.SetPremises(_labels, _texts, _statuses, _selectable, _mode == ArgueMode.Pressure ? -1 : _selected);

            var style = ControlNames.DetectPadStyle();
            string sel = Selected;
            bool browse = _mode == ArgueMode.Browse && !e.Ended;
            bool claim = sel == ArgumentEngine.ClaimId;
            _view.SetVerb("ask", "Ask\n<size=70%>" + bindings.KeysText("argue-ask", style) + "</size>", browse && !claim && e.CanAsk(sel));
            _view.SetVerb("challenge", "Challenge\n<size=70%>" + bindings.KeysText("argue-challenge", style) + "</size>", browse && !claim && e.CanChallenge(sel));
            _view.SetVerb("concede", (claim ? "Concede the claim" : "Concede") + "\n<size=70%>" + bindings.KeysText("argue-concede", style) + "</size>", browse && (claim ? e.CanConcedeClaim : e.CanConcede(sel)));
            _view.SetVerb("press", (claim ? "Press the claim" : "Press") + " (hold)\n<size=70%>" + bindings.KeysText("argue-press", style) + "</size>", browse && e.CanPress(sel));
            _view.SetVerb("rest", "Let it rest\n<size=70%>" + bindings.KeysText("argue-rest", style) + "</size>", browse && e.CanLetItRest);

            _names.Clear();
            if (_mode == ArgueMode.PickEvidence)
            {
                foreach (var id in _trayIds)
                {
                    if (id == RetortItem) { _names.Add("<b>Retort</b> (no evidence): “" + e.Def.Premise(sel).retort.line + "”"); continue; }
                    var ev = e.Def.Evidence(id);
                    _names.Add(ev != null ? "<b>" + ev.name + "</b>  <size=75%>(" + ev.style + ")</size>" : id);
                }
            }
            _view.SetTray(_names, _trayIndex);

            if (_mode == ArgueMode.PickReason)
            {
                _names.Clear();
                foreach (var c in _cards) _names.Add((string.IsNullOrEmpty(c.supporter) ? "" : "<i>(" + c.supporter + ")</i> ") + c.reason.text);
                var p = e.Def.Premise(sel);
                var ev = e.Def.Evidence(_evidence);
                _view.SetReasons("You said “" + p.text + "”. " + (ev != null ? ev.name : _evidence) + " shows that it doesn't hold, because...", _names, _cardIndex);
            }
            else _view.SetReasons(null, null, -1);

            _view.SetPrompt(Prompt(e, sel));
            _view.SetStatus(_statusLine);
            _view.SetHint(Hint(bindings, style));
        }

        string Prompt(ArgumentEngine e, string sel)
        {
            switch (_mode)
            {
                case ArgueMode.Pressure:
                    var ph = e.Pressure;
                    return ph != null ? "Pressure: stay clear of " + e.Def.opponent + "'s feeling. " + Mathf.CeilToInt(ph.Duration - ph.Time) + " s" + (ph.Spec.anger > 0 ? "  (angrier: faster, denser)" : "") : "";
                case ArgueMode.PickEvidence: return "Show what? (pick evidence)";
                case ArgueMode.PickReason: return "Why does it answer that? (pick the reason that follows)";
                case ArgueMode.Ended: return "";
            }
            if (sel == ArgumentEngine.ClaimId) return "The claim. Break its key premises, or let it rest once you've heard them all.";
            var p = e.Def.Premise(sel);
            if (p == null) return "";
            return string.IsNullOrEmpty(p.question) || e.Status(sel) != PremiseStatus.Standing ? p.label + ": “" + p.text + "”" : p.label + ": she asks “" + p.question + "”";
        }

        static string Hint(ControlBindings b, PadStyle style) =>
            "Move: arrows / stick.  Ask " + b.KeysText("argue-ask", style) + ".  Challenge " + b.KeysText("argue-challenge", style)
            + ".  Concede " + b.KeysText("argue-concede", style) + ".  Press: hold " + b.KeysText("argue-press", style)
            + ".  Let it rest " + b.KeysText("argue-rest", style) + ".  Back: Backspace / " + ControlNames.PadName("buttonEast", style) + ".  Rebind: Settings > Controls.";

        static string StatusWords(PremiseStatus s, bool heard)
        {
            switch (s)
            {
                case PremiseStatus.Broken: return "BROKEN";
                case PremiseStatus.Conceded: return "✓ conceded";
                case PremiseStatus.ConcededFalse: return "✓ agreed (closed)";
                case PremiseStatus.Forced: return "FORCED";
                case PremiseStatus.Hidden: return "hidden";
                default: return heard ? "standing (heard)" : "standing";
            }
        }
    }
}
