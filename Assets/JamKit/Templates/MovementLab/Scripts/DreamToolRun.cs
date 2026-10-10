using System.Globalization;
using System.Text;
using JamKit.Movement;
using JamKit.Presenting;
using JamKit.Shell;
using UnityEngine;
using UnityEngine.UIElements;

namespace JamKit.MovementLab
{
    /// <summary>
    /// The dream tool chapter's run loop (docs/MOVEMENT.md "Dream tool prototype"), on top of <see cref="MovementLabGame"/>:
    /// it owns the tool's state (core <see cref="DreamToolState"/>: tiles popped, sockets built, the solid colour) and
    /// keeps the pieces' views and colliders in step with it; it runs the chapter's own clock, a core
    /// <see cref="GauntletClock"/> on the chapter's <see cref="DreamToolCourse.run"/> (the gate, three splits with deltas
    /// against your best, the finish, the best kept in the save under its own key); and it shows the HUD: the time, the
    /// targets hit, which colour is solid, the board (the developer's real best from Data/DeveloperRecords.json, Mags's at
    /// 1.5x, yours) and the end screen. It records your run as a ghost and races you against your best (or the
    /// developer's, once one is shipped); the Ghost action cycles it, as in the Gauntlet. A fall
    /// puts back what's ahead of the checkpoint (and its colour). The shell's Restart action (R or the pad's View by default,
    /// rebindable, as for the Gauntlet) restarts the chapter from its start pad. Autopilot runs don't count.
    /// </summary>
    public sealed class DreamToolRun : MonoBehaviour
    {
        public const string SaveKey = "movementlab_dreamtool";

        public MovementLabGame game;
        public DreamTool tool;
        public DreamToolPieceView[] pieceViews;
        public DreamToolTargetView[] targetViews;
        /// <summary>The ghost's figure (its own, beside the Gauntlet's).</summary>
        public Transform ghost;
        [Tooltip("The developer's best runs (Data/DeveloperRecords.json).")]
        public TextAsset developerRecords;

        static DreamToolRun _current;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _current = null;

        public static DreamToolRun Current => _current;

        DreamToolCourse _d;
        DreamToolState _state;
        GauntletClock _clock;
        int _version = -1;
        bool _autopiloted;
        readonly GhostTrack _live = new GhostTrack();
        RunGhost _ghost;
        GauntletRecord _dev;
        VisualElement _root, _board, _end;
        Label _timer, _sub, _split, _boardText, _endText, _hint;
        float _splitShow, _hintShow, _uiT, _timerT;
        readonly StringBuilder _sb = new StringBuilder(512);
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static readonly Color Green = new Color(0.45f, 0.95f, 0.55f), Red = new Color(1f, 0.45f, 0.42f), Ink = new Color(0.96f, 0.97f, 1f);
        static readonly Color PinkInk = new Color(1f, 0.66f, 0.8f), BlueInk = new Color(0.62f, 0.8f, 1f);

        public DreamToolCourse Course => _d;
        public DreamToolState State => _state;
        public GauntletClock Clock => _clock;
        public bool Autopiloted => _autopiloted;
        public GhostTrack LiveGhost => _live;
        public GhostTrack BestGhost => _ghost?.best;
        public RunGhost Ghost => _ghost;
        public GhostChoice GhostChoice => _ghost != null ? _ghost.Choice : GhostChoice.Off;
        public float DeveloperTime => _dev != null ? _dev.best : 0f;
        public float MagsTime => DeveloperRecords.MagsTimeFor(DeveloperTime);
        /// <summary>What the target board says (for tests).</summary>
        public string BoardShown => _boardText != null ? _boardText.text : null;
        public bool EndScreenShown => _end != null && _end.style.display == DisplayStyle.Flex;

        void Start()
        {
            if (game == null || !GameRoot.Exists) { enabled = false; return; }
            _current = this;
            TryInit();
        }

        void TryInit()
        {
            if (_state != null || game.DreamCourse == null) return;
            _d = game.DreamCourse;
            _state = new DreamToolState(_d);
            _clock = new GauntletClock(_d.run);
            _ghost = new RunGhost(ghost);
            LoadRecord();
            BuildUi();
            game.Placed += OnPlaced;
            Sync(true);
        }

        void OnDestroy()
        {
            if (_current == this) _current = null;
            if (game != null) game.Placed -= OnPlaced;
            _root?.RemoveFromHierarchy();
        }

        public DreamToolTargetView TargetView(int i) => targetViews != null && i >= 0 && i < targetViews.Length ? targetViews[i] : null;

        // ---------- The save ----------

        void LoadRecord()
        {
            string json = GameRoot.Save.Data.GetEntry(SaveKey);
            GauntletRecord rec = null;
            if (!string.IsNullOrEmpty(json))
            {
                try { rec = JsonUtility.FromJson<GauntletRecord>(json); }
                catch (System.Exception e) { Debug.LogWarning("Dream tool: the saved record was damaged, starting fresh. " + e.Message); }
            }
            _clock.record = rec ?? new GauntletRecord();
            _ghost.best.LoadFrom(_clock.record.ghost, _clock.record.ghostRate);
            _dev = DeveloperRecordsAsset.DreamTool(developerRecords);
            if (_dev != null) _ghost.developer.LoadFrom(_dev.ghost, _dev.ghostRate);
            _ghost.PickDefault();
        }

        void SaveRecord()
        {
            // The ghost goes with the record (the clock copied a new best into it at the finish).
            GameRoot.Save.Data.SetEntry(SaveKey, JsonUtility.ToJson(_clock.record));
            GameRoot.Save.Save();
        }

        public void ClearRecord()
        {
            _clock.record = new GauntletRecord();
            _ghost?.best.Clear();
            GameRoot.Save.Data.SetEntry(SaveKey, null);
        }

        // ---------- The run ----------

        /// <summary>Back on the chapter's start pad, standing: the clock at 0, every tile and socket back, pink solid.</summary>
        public void Restart() => game.PlaceAtCheckpoint(_d.startCheckpoint, false);

        void OnPlaced(int checkpoint, bool running)
        {
            if (_state == null) return;
            var cp = game.Course.checkpoints[checkpoint];
            if (checkpoint <= _d.startCheckpoint)
            {
                // Anywhere before the chapter, or its start pad: a fresh chapter.
                _state.ResetFrom(float.MinValue, DreamToolCourse.Pink);
                _clock.Reset();
                _live.Clear();
                _ghost.Hide();
                _autopiloted = game.AutopilotOn;
                if (checkpoint == _d.startCheckpoint) _hintShow = 8f;
            }
            else _state.ResetFrom(cp.s, _d.ColourAt(checkpoint));
            if (tool != null) tool.ResetPilot(cp.s);
            Sync(true);
        }

        /// <summary>The tool's shot landed (or missed): the views follow the state at once.</summary>
        public void OnShot(DreamShot shot)
        {
            if (shot.target >= 0) TargetView(shot.target)?.Flash();
            Sync(false);
        }

        void Sync(bool instant)
        {
            if (_state == null || (!instant && _state.Version == _version)) return;
            _version = _state.Version;
            if (pieceViews != null)
                for (int i = 0; i < pieceViews.Length; i++)
                    if (pieceViews[i] != null) pieceViews[i].Set(_state.Solid(pieceViews[i].index), instant);
            if (targetViews != null)
                for (int i = 0; i < targetViews.Length; i++)
                    if (targetViews[i] != null) targetViews[i].Show(_state.Done(targetViews[i].index), _state.Colour);
        }

        void Update()
        {
            if (_state == null) { TryInit(); if (_state == null) return; }
            Sync(false);
            float dt = Time.deltaTime;
            bool inChapter = DreamToolCourse.InChapter(game.S);
            if (inChapter && GameRoot.Input.Pressed(GameRoot.Input.Restart)) Restart();
            if (inChapter && GameRoot.Input.Pressed(GameRoot.Input.Ghost)) _ghost.Cycle();
            if (game.AutopilotOn) _autopiloted = true;
            if (GameRoot.Flow.Current == JamKit.Flow.GameState.Playing && inChapter)
            {
                var p = game.player.transform.position;
                var ev = _clock.Tick(dt, new System.Numerics.Vector3(p.x, p.y, p.z), game.S, game.player.Def.radius, game.player.Sim.Height, !_autopiloted && !PresenterMode.AttractRunning);
                if (_clock.State == GauntletState.Running && !game.Respawning) _live.Record(dt, new System.Numerics.Vector3(p.x, p.y, p.z), game.player.Yaw);
                Handle(ev);
            }
            _ghost.Place(_clock.State == GauntletState.Running && inChapter, _clock.Time);
            UpdateUi(dt, inChapter);
        }

        void Handle(GauntletEvents ev)
        {
            var audio = GameRoot.Audio;
            if (ev.started) { _live.Clear(); audio.Play("ui_select", 0.6f); _hintShow = 0f; }
            if (ev.split >= 0 && !ev.finished)
            {
                audio.Play("blip", 0.5f);
                SplitText(ev.split);
            }
            if (ev.finished)
            {
                audio.Play("level_done", 0.8f);
                if (!_autopiloted)
                {
                    if (_clock.NewBest)
                    {
                        // The best run's ghost (the chapter saved none before 2026-10-10: the clock's record was cleared here).
                        _ghost.best.CopyFrom(_live);
                        _live.CopyTo(_clock.record.ghost);
                        _clock.record.ghostRate = _live.rate;
                    }
                    SaveRecord();
                }
                GameRoot.Run.Set("Dream tool best", _clock.record.best > 0f ? Fmt(_clock.record.best) : "none");
                Debug.Log("JAMKIT_DREAMTOOL finished " + _clock.Total.ToString("0.00", Inv) + " s, targets " + _state.RequiredDone + "/" + _d.RequiredCount
                    + ", shots " + _state.Shots + (_autopiloted ? " by the autopilot" : _clock.NewBest ? ", a new best" : ""));
            }
        }

        // ---------- The HUD ----------

        static string Fmt(float seconds)
        {
            int m = (int)(seconds / 60f);
            float s = seconds - m * 60f;
            return m > 0 ? m.ToString(Inv) + ":" + s.ToString("00.00", Inv) : s.ToString("0.00", Inv);
        }

        static string Delta(float d) => (d < 0f ? "-" : "+") + Mathf.Abs(d).ToString("0.00", Inv);

        void SplitText(int i)
        {
            string text = _d.run.sections[i].name + "  " + Fmt(_clock.Split(i));
            Color c = Ink;
            if (_clock.HasBestSplit(i))
            {
                float d = _clock.SplitDelta(i);
                text += "   " + Delta(d);
                c = d <= 0f ? Green : Red;
            }
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
            _root = new VisualElement { name = "dream-tool-hud", pickingMode = PickingMode.Ignore };
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
            _board.style.right = 24; _board.style.top = 24; _board.style.width = 290;
            _boardText = MakeLabel(_board, 17, TextAnchor.UpperLeft);
            _boardText.style.unityFontStyleAndWeight = FontStyle.Normal;
            _end = Panel(_root);
            _end.style.left = Length.Percent(50); _end.style.top = Length.Percent(18);
            _end.style.width = 480; _end.style.marginLeft = -240;
            _endText = MakeLabel(_end, 18, TextAnchor.UpperLeft);
            _endText.style.unityFontStyleAndWeight = FontStyle.Normal;
            _end.style.display = DisplayStyle.None;
            _root.style.display = DisplayStyle.None;
        }

        void UpdateUi(float dt, bool inChapter)
        {
            if (_root == null) return;
            var want = inChapter ? DisplayStyle.Flex : DisplayStyle.None;
            if (_root.style.display != want) _root.style.display = want;
            if (!inChapter) return;
            _splitShow -= dt;
            _hintShow -= dt;
            _split.style.opacity = Mathf.Clamp01(_splitShow * 2f);
            var state = _clock.State;
            _timerT -= dt;
            if (_timerT <= 0f) { _timerT = 0.05f; _timer.text = Fmt(state == GauntletState.Finished ? _clock.Total : _clock.Time); }
            _uiT -= dt;
            if (_uiT <= 0f)
            {
                _uiT = 0.1f;
                int sec = _d.run.SectionAt(game.S);
                bool pink = _state.Colour == DreamToolCourse.Pink;
                _sub.text = state == GauntletState.Ready ? "Cross the gate to start the clock"
                    : "Targets " + _state.RequiredDone + "/" + _d.RequiredCount + (sec >= 0 ? "   " + _d.run.sections[sec].name : "")
                      + (sec == 2 ? (pink ? "   Pink is solid" : "   Blue is solid") : "")
                      + "   " + _ghost.Label;
                _sub.style.color = sec == 2 ? (pink ? PinkInk : BlueInk) : Ink;
                _hint.text = state == GauntletState.Ready && _hintShow > 0f
                    ? "The dream tool: left mouse or RT. Point roughly: it locks on (the gold bracket). Pop the glass, build the sockets, flip the colours. R or View restarts."
                    : "";
                BoardText();
                if (state == GauntletState.Finished) EndText();
            }
            var board = state != GauntletState.Running ? DisplayStyle.Flex : DisplayStyle.None;
            if (_board.style.display != board) _board.style.display = board;
            var end = state == GauntletState.Finished ? DisplayStyle.Flex : DisplayStyle.None;
            if (_end.style.display != end) _end.style.display = end;
        }

        void BoardText()
        {
            _sb.Clear();
            _sb.Append("Dream tool: target times\n");
            _sb.Append("The developer               ").Append(DeveloperTime > 0f ? Fmt(DeveloperTime) : "-").Append('\n');
            _sb.Append("Mags                        ").Append(MagsTime > 0f ? Fmt(MagsTime) : "-").Append('\n');
            _sb.Append("You                         ").Append(_clock.record.best > 0f ? Fmt(_clock.record.best) : "-");
            _boardText.text = _sb.ToString();
        }

        void EndText()
        {
            _sb.Clear();
            _sb.Append("Dream tool  ").Append(Fmt(_clock.Total));
            _sb.Append("   targets ").Append(_state.RequiredDone).Append('/').Append(_d.RequiredCount).Append(", shots ").Append(_state.Shots);
            if (_autopiloted) _sb.Append("\nThe autopilot's run (not counted)");
            else if (_clock.NewBest) _sb.Append("\nA new best!");
            else if (_clock.record.best > 0f) _sb.Append("\nBest ").Append(Fmt(_clock.record.best)).Append("   ").Append(Delta(_clock.Total - _clock.record.best));
            _sb.Append("\n\n");
            for (int i = 0; i < _d.run.sections.Count; i++)
            {
                _sb.Append(_d.run.sections[i].name).Append("   ").Append(Fmt(_clock.Split(i)));
                if (_clock.HasBestSplit(i) && !_clock.NewBest) _sb.Append("   ").Append(Delta(_clock.SplitDelta(i)));
                _sb.Append('\n');
            }
            _sb.Append("\nR or View: run it again    G: ghost (yours, the developer's, off)    Esc: menu");
            _endText.text = _sb.ToString();
        }
    }
}
