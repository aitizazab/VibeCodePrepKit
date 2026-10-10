using System.Globalization;
using System.Text;
using JamKit.Movement;
using JamKit.Presenting;
using JamKit.Shell;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace JamKit.MovementLab
{
    /// <summary>
    /// The Gauntlet's run loop (docs/MOVEMENT.md "The Gauntlet"), on top of <see cref="MovementLabGame"/>: the
    /// clock (core <see cref="GauntletClock"/>) starts at the gate and stops at the finish line; touch markers
    /// light up as you pass over them (each missed one adds 2 s); every section's end shows a split with its
    /// delta against your best (green faster, red slower); falls respawn you at the section's start, running, with
    /// the clock still going (the lab's respawn). The shell's Restart action (R or the pad's View / Share by default)
    /// restarts at once, from the start pad. Your best time, splits and ghost are kept in the shell's save; the ghost
    /// of your best run plays beside you (the Ghost action, G or d-pad left, hides it). Both are rebindable in
    /// Settings > Controls, whose "Restart run" and "Show ghost" rows show while this exists; the hints name the
    /// current keys. The Ghost action cycles the ghost: your best, the developer's (the creator's shipped run), off.
    /// The target board shows the developer's time (their real best, from Data/DeveloperRecords.json; the autopilot's
    /// time is a test driver's and only appears in tests and logs), Mags's (1.5x, rounded) and yours; the end
    /// screen your time and splits. Without the shell (a bare test scene) it reads R / View and G / d-pad left.
    /// </summary>
    public sealed class GauntletRun : MonoBehaviour
    {
        public MovementLabGame game;
        public Transform ghost;
        public GauntletMarkerView[] markerViews;
        [Tooltip("The developer's best runs (Data/DeveloperRecords.json): the board's developer time and the developer's ghost.")]
        public TextAsset developerRecords;

        static GauntletRun _current;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _current = null;

        public static GauntletRun Current => _current;

        GauntletCourse _g;
        GauntletClock _clock;
        readonly GhostTrack _live = new GhostTrack();
        RunGhost _ghost;
        GauntletRecord _dev;
        bool _autopiloted;

        VisualElement _root, _board, _end;
        Label _timer, _sub, _split, _boardText, _endText, _hint;
        float _splitShow, _hintShow;
        float _uiT, _timerT;
        readonly StringBuilder _sb = new StringBuilder(512);
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static readonly Color Green = new Color(0.45f, 0.95f, 0.55f), Red = new Color(1f, 0.45f, 0.42f), Ink = new Color(0.96f, 0.97f, 1f);

        public GauntletClock Clock => _clock;
        public GhostTrack LiveGhost => _live;
        public GhostTrack BestGhost => _ghost?.best;
        public GhostTrack DeveloperGhost => _ghost?.developer;
        public RunGhost Ghost => _ghost;
        /// <summary>Which ghost the Ghost action has picked (your best, the developer's, off).</summary>
        public GhostChoice GhostChoice => _ghost != null ? _ghost.Choice : GhostChoice.Off;
        public bool ghostOn => GhostChoice != GhostChoice.Off;
        /// <summary>The developer's best (the shipped record), 0 without one; penalties included.</summary>
        public float DeveloperTime => _dev != null ? _dev.best : 0f;
        public int DeveloperMissed => _dev != null ? _dev.bestMissed : 0;
        public float MagsTime => DeveloperRecords.MagsTimeFor(DeveloperTime);
        public bool GhostVisible => _ghost != null && _ghost.Visible;
        /// <summary>What the target board says (for tests).</summary>
        public string BoardShown => _boardText != null ? _boardText.text : null;
        public bool EndScreenShown => _end != null && _end.style.display == DisplayStyle.Flex;
        /// <summary>The last run was (partly) driven by the autopilot, so it doesn't count for your best.</summary>
        public bool Autopiloted => _autopiloted;

        void Start()
        {
            if (game == null || !GameRoot.Exists) { enabled = false; return; }
            _current = this;
            ShowControlRows(true);
            TryInit();
        }

        static void ShowControlRows(bool shown)
        {
            if (!GameRoot.Exists || GameRoot.Ui == null || GameRoot.Ui.Controls == null) return;
            GameRoot.Ui.Controls.SetRowShown("restart", shown);
            GameRoot.Ui.Controls.SetRowShown("ghost", shown);
        }

        // ---------- The keys (rebindable) ----------

        int _keysVersion = -1;
        Gamepad _keysPad;
        string _restartKeys = "R or View", _ghostKeys = "G";
        string _readyHint = "", _ghostOffNote = "", _endKeys = "";

        /// <summary>Rebuilds the hint texts when the bindings or the pad change (not every frame).</summary>
        void RefreshKeyNames()
        {
            var pad = Gamepad.current;
            int version = GameRoot.Exists ? GameRoot.Input.Bindings.Version : 0;
            if (version == _keysVersion && pad == _keysPad && _readyHint.Length > 0) return;
            _keysVersion = version;
            _keysPad = pad;
            if (GameRoot.Exists)
            {
                var style = ControlNames.DetectPadStyle();
                _restartKeys = GameRoot.Input.Bindings.KeysText("restart", style);
                _ghostKeys = GameRoot.Input.Bindings.KeysText("ghost", style);
                if (_restartKeys.Length == 0) _restartKeys = "(no key: Settings > Controls)";
                if (_ghostKeys.Length == 0) _ghostKeys = "(no key)";
            }
            _readyHint = _restartKeys + " restarts at any time. " + _ghostKeys + " picks the ghost: your best, the developer's, or none.";
            _ghostOffNote = "   (" + _ghostKeys + ": change)";
            _endKeys = "\n" + _restartKeys + ": run it again    " + _ghostKeys + ": ghost (yours, the developer's, off)    Esc: menu";
        }

        /// <summary>This frame's presses: the shell's rebindable actions (gameplay allowed), or the devices without a shell.</summary>
        static void ReadKeys(out bool restart, out bool ghost)
        {
            if (GameRoot.Exists)
            {
                var input = GameRoot.Input;
                restart = input.Pressed(input.Restart);
                ghost = input.Pressed(input.Ghost);
                return;
            }
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            restart = (kb != null && kb.rKey.wasPressedThisFrame) || (pad != null && pad.selectButton.wasPressedThisFrame);
            ghost = (kb != null && kb.gKey.wasPressedThisFrame) || (pad != null && pad.dpad.left.wasPressedThisFrame);
        }

        /// <summary>Sets up once the lab has built its course (its Start may run after this one).</summary>
        void TryInit()
        {
            if (_clock != null || game.Gauntlet == null) return;
            _g = game.Gauntlet;
            _clock = new GauntletClock(_g);
            _ghost = new RunGhost(ghost);
            LoadRecord();
            _dev = DeveloperRecordsAsset.Gauntlet(developerRecords);
            if (_dev != null) _ghost.developer.LoadFrom(_dev.ghost, _dev.ghostRate);
            _ghost.PickDefault();
            BuildUi();
            game.OnPlaced = OnPlaced;
        }

        void OnDestroy()
        {
            if (_current == this)
            {
                _current = null;
                ShowControlRows(false);
            }
            if (game != null && game.OnPlaced == (System.Action<int, bool>)OnPlaced) game.OnPlaced = null;
            _root?.RemoveFromHierarchy();
        }

        // ---------- The save ----------

        void LoadRecord()
        {
            string json = GameRoot.Save.Data.GetEntry(GauntletClock.SaveKey);
            GauntletRecord rec = null;
            if (!string.IsNullOrEmpty(json))
            {
                try { rec = JsonUtility.FromJson<GauntletRecord>(json); }
                catch (System.Exception e) { Debug.LogWarning("Gauntlet: the saved record was damaged, starting fresh. " + e.Message); }
            }
            _clock.record = rec ?? new GauntletRecord();
            _ghost.best.LoadFrom(_clock.record.ghost, _clock.record.ghostRate);
        }

        public static string ToJson(GauntletRecord rec) => JsonUtility.ToJson(rec);
        public static GauntletRecord FromJson(string json) => JsonUtility.FromJson<GauntletRecord>(json);

        void SaveRecord()
        {
            GameRoot.Save.Data.SetEntry(GauntletClock.SaveKey, ToJson(_clock.record));
            GameRoot.Save.Save();
        }

        /// <summary>Forgets your best time, splits and ghost (tests, a fresh start).</summary>
        public void ClearRecord()
        {
            _clock.record = new GauntletRecord();
            _ghost.best.Clear();
            GameRoot.Save.Data.SetEntry(GauntletClock.SaveKey, null);
        }

        // ---------- The run ----------

        /// <summary>The instant restart: back on the start pad, standing, the clock at 0 and every marker waiting.</summary>
        public void Restart()
        {
            game.PlaceAtCheckpoint(_g.startCheckpoint, false);
        }

        void OnPlaced(int checkpoint, bool running)
        {
            // Back to the start pad (restart, a chapter jump, a fall before the gate): a fresh run.
            if (checkpoint != _g.startCheckpoint) return;
            _clock.Reset();
            _live.Clear();
            _autopiloted = game.AutopilotOn;
            if (markerViews != null) foreach (var v in markerViews) if (v != null) v.Show(GauntletMarkerView.Look.Waiting);
            _ghost.Hide();
            _hintShow = 6f;
        }

        void Update()
        {
            if (_clock == null) { TryInit(); if (_clock == null) return; }
            float dt = Time.deltaTime;
            bool inGauntlet = _g.InGauntlet(game.S);
            ReadKeys(out bool restartPressed, out bool ghostPressed);
            if (restartPressed && inGauntlet) Restart();
            if (ghostPressed && inGauntlet) _ghost.Cycle();

            if (game.AutopilotOn) _autopiloted = true;
            var player = game.player;
            if (GameRoot.Flow.Current == JamKit.Flow.GameState.Playing && inGauntlet)
            {
                var p = player.transform.position;
                var feet = new System.Numerics.Vector3(p.x, p.y, p.z);
                var ev = _clock.Tick(dt, feet, game.S, player.Def.radius, player.Sim.Height, !_autopiloted && !PresenterMode.AttractRunning);
                if (_clock.State == GauntletState.Running && !game.Respawning) _live.Record(dt, feet, player.Yaw);
                Handle(ev);
            }
            PlaceGhost();
            UpdateUi(dt, inGauntlet);
        }

        void Handle(GauntletEvents ev)
        {
            var audio = GameRoot.Audio;
            if (ev.started)
            {
                _live.Clear();
                audio.Play("ui_select", 0.6f);
                _hintShow = 0f;
            }
            if (ev.markerHit >= 0)
            {
                audio.Play("coin", 0.55f);
                if (markerViews != null && ev.markerHit < markerViews.Length && markerViews[ev.markerHit] != null) markerViews[ev.markerHit].Show(GauntletMarkerView.Look.Touched);
            }
            if (ev.markerMissed >= 0)
            {
                audio.Play("ui_back", 0.5f);
                if (markerViews != null && ev.markerMissed < markerViews.Length && markerViews[ev.markerMissed] != null) markerViews[ev.markerMissed].Show(GauntletMarkerView.Look.Missed);
                ShowSplit("Missed: " + _g.markers[ev.markerMissed].name + "  +" + GauntletCourse.MissPenalty.ToString("0", Inv) + " s", Red);
            }
            if (ev.split >= 0 && !ev.finished)
            {
                audio.Play("blip", 0.5f);
                SplitText(ev.split);
            }
            if (ev.finished)
            {
                audio.Play("level_done", 0.8f);
                if (_clock.NewBest && !_autopiloted)
                {
                    _ghost.best.CopyFrom(_live);
                    _live.CopyTo(_clock.record.ghost);
                    _clock.record.ghostRate = _live.rate;
                    SaveRecord();
                }
                else if (!_autopiloted) SaveRecord(); // the run count
                GameRoot.Run.Set("Gauntlet best", _clock.record.best > 0f ? Fmt(_clock.record.best) : "none");
                Debug.Log("JAMKIT_GAUNTLET finished " + _clock.Total.ToString("0.00", Inv) + " s (penalty " + _clock.Penalty.ToString("0", Inv)
                    + ", markers " + _clock.MarkersHit + "/" + _clock.MarkerCount + ")" + (_autopiloted ? " by the autopilot" : _clock.NewBest ? ", a new best" : ""));
            }
        }

        void PlaceGhost()
        {
            _ghost.Place(_clock.State == GauntletState.Running && _g.InGauntlet(game.S), _clock.Time);
        }

        // ---------- The HUD, the board, the end screen ----------

        static string Fmt(float seconds)
        {
            int m = (int)(seconds / 60f);
            float s = seconds - m * 60f;
            return m > 0 ? m.ToString(Inv) + ":" + s.ToString("00.00", Inv) : s.ToString("0.00", Inv);
        }

        static string Delta(float d) => (d < 0f ? "-" : "+") + Mathf.Abs(d).ToString("0.00", Inv);

        void SplitText(int i)
        {
            string text = _g.sections[i].name + "  " + Fmt(_clock.Split(i));
            Color c = Ink;
            if (_clock.HasBestSplit(i))
            {
                float d = _clock.SplitDelta(i);
                text += "   " + Delta(d);
                c = d <= 0f ? Green : Red;
            }
            ShowSplit(text, c);
        }

        void ShowSplit(string text, Color c)
        {
            _split.text = text;
            _split.style.color = c;
            _splitShow = 2.5f;
        }

        static Label MakeLabel(VisualElement parent, int size, TextAnchor align)
        {
            var l = new Label { pickingMode = PickingMode.Ignore };
            l.style.fontSize = size;
            l.style.color = Ink;
            l.style.unityTextAlign = align;
            l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.style.textShadow = new TextShadow { offset = new Vector2(1.5f, 1.5f), blurRadius = 2f, color = new Color(0f, 0f, 0f, 0.6f) };
            parent.Add(l);
            return l;
        }

        static VisualElement Panel(VisualElement parent)
        {
            var p = new VisualElement { pickingMode = PickingMode.Ignore };
            p.style.position = Position.Absolute;
            p.style.backgroundColor = new Color(0.07f, 0.08f, 0.13f, 0.72f);
            p.style.paddingLeft = 16; p.style.paddingRight = 16; p.style.paddingTop = 12; p.style.paddingBottom = 12;
            p.style.borderTopLeftRadius = 10; p.style.borderTopRightRadius = 10; p.style.borderBottomLeftRadius = 10; p.style.borderBottomRightRadius = 10;
            parent.Add(p);
            return p;
        }

        void BuildUi()
        {
            _root = new VisualElement { name = "gauntlet-hud", pickingMode = PickingMode.Ignore };
            _root.style.position = Position.Absolute;
            _root.style.left = 0; _root.style.right = 0; _root.style.top = 0; _root.style.bottom = 0;
            GameRoot.Hud.Root.Add(_root);

            var top = new VisualElement { pickingMode = PickingMode.Ignore };
            top.style.position = Position.Absolute;
            top.style.left = 0; top.style.right = 0; top.style.top = 18;
            top.style.alignItems = Align.Center;
            _root.Add(top);
            _timer = MakeLabel(top, 44, TextAnchor.MiddleCenter);
            _sub = MakeLabel(top, 18, TextAnchor.MiddleCenter);
            _split = MakeLabel(top, 22, TextAnchor.MiddleCenter);
            _hint = MakeLabel(top, 18, TextAnchor.MiddleCenter);

            _board = Panel(_root);
            _board.style.right = 24; _board.style.top = 24; _board.style.width = 270;
            _boardText = MakeLabel(_board, 17, TextAnchor.UpperLeft);
            _boardText.style.unityFontStyleAndWeight = FontStyle.Normal;

            _end = Panel(_root);
            _end.style.left = Length.Percent(50); _end.style.top = Length.Percent(18);
            _end.style.width = 460; _end.style.marginLeft = -230;
            _endText = MakeLabel(_end, 18, TextAnchor.UpperLeft);
            _endText.style.unityFontStyleAndWeight = FontStyle.Normal;
            _end.style.display = DisplayStyle.None;
        }

        void UpdateUi(float dt, bool inGauntlet)
        {
            if (_root == null) return;
            _root.style.display = inGauntlet ? DisplayStyle.Flex : DisplayStyle.None;
            if (!inGauntlet) return;
            _splitShow -= dt;
            _hintShow -= dt;
            _split.style.opacity = Mathf.Clamp01(_splitShow * 2f);
            var state = _clock.State;
            float shown = state == GauntletState.Finished ? _clock.Total : _clock.Time;
            _timerT -= dt;
            if (_timerT <= 0f) { _timerT = 0.05f; _timer.text = Fmt(shown); } // 20 a second: no string every frame
            _uiT -= dt;
            if (_uiT <= 0f)
            {
                // Words refresh ten times a second (no string building every frame).
                _uiT = 0.1f;
                RefreshKeyNames();
                int sec = _g.SectionAt(game.S);
                _sub.text = state == GauntletState.Ready ? "Cross the gate to start the clock"
                    : "Markers " + _clock.MarkersHit + "/" + _clock.MarkerCount + (sec >= 0 ? "   " + _g.sections[sec].name : "")
                      + "   " + _ghost.Label + _ghostOffNote;
                _hint.text = state == GauntletState.Ready && _hintShow > 0f ? _readyHint : "";
                BoardText();
                if (state == GauntletState.Finished) EndText();
            }
            _board.style.display = state != GauntletState.Running ? DisplayStyle.Flex : DisplayStyle.None;
            _end.style.display = state == GauntletState.Finished ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void BoardText()
        {
            _sb.Clear();
            _sb.Append("Target times\n");
            _sb.Append("The developer               ").Append(DeveloperTime > 0f ? Fmt(DeveloperTime) : "-");
            if (DeveloperMissed > 0) _sb.Append("  (").Append(DeveloperMissed).Append(DeveloperMissed == 1 ? " plate missed)" : " plates missed)");
            _sb.Append('\n');
            _sb.Append("Mags                        ").Append(MagsTime > 0f ? Fmt(MagsTime) : "-").Append('\n');
            _sb.Append("You                         ").Append(_clock.record.best > 0f ? Fmt(_clock.record.best) : "-");
            _boardText.text = _sb.ToString();
        }

        void EndText()
        {
            _sb.Clear();
            _sb.Append("Gauntlet  ").Append(Fmt(_clock.Total));
            if (_clock.Missed > 0) _sb.Append("   (").Append(_clock.Missed).Append(" missed: +").Append(_clock.Penalty.ToString("0", Inv)).Append(" s)");
            if (_autopiloted) _sb.Append("\nThe autopilot's run (not counted)");
            else if (_clock.NewBest) _sb.Append("\nA new best!");
            else if (_clock.record.best > 0f) _sb.Append("\nBest ").Append(Fmt(_clock.record.best)).Append("   ").Append(Delta(_clock.Total - _clock.record.best));
            _sb.Append("\n\n");
            for (int i = 0; i < _g.sections.Count; i++)
            {
                _sb.Append(_g.sections[i].name).Append("   ").Append(Fmt(_clock.Split(i)));
                if (_clock.HasBestSplit(i) && !_clock.NewBest) _sb.Append("   ").Append(Delta(_clock.SplitDelta(i)));
                _sb.Append('\n');
            }
            RefreshKeyNames();
            _sb.Append(_endKeys);
            _endText.text = _sb.ToString();
        }
    }
}
