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
    /// the clock still going (the lab's respawn). R or the pad's View (Select) button restarts at once, from the
    /// start pad. Your best time, splits and ghost are kept in the shell's save; the ghost of your best run plays
    /// beside you (G or the pad's d-pad left hides it). The target board shows the developer's time (the
    /// autopilot's), Mags's and yours; the end screen your time and splits.
    /// Input is read directly here (a stopgap until the shell's InputService has a Restart action).
    /// </summary>
    public sealed class GauntletRun : MonoBehaviour
    {
        public MovementLabGame game;
        public Transform ghost;
        public GauntletMarkerView[] markerViews;
        public bool ghostOn = true;

        static GauntletRun _current;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _current = null;

        public static GauntletRun Current => _current;

        GauntletCourse _g;
        GauntletClock _clock;
        readonly GhostTrack _live = new GhostTrack();
        readonly GhostTrack _best = new GhostTrack();
        bool _autopiloted;
        Renderer[] _ghostRenderers;
        VisualElement _root, _board, _end;
        Label _timer, _sub, _split, _boardText, _endText, _hint;
        float _splitShow, _hintShow;
        float _uiT, _timerT;
        readonly StringBuilder _sb = new StringBuilder(512);
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static readonly Color Green = new Color(0.45f, 0.95f, 0.55f), Red = new Color(1f, 0.45f, 0.42f), Ink = new Color(0.96f, 0.97f, 1f);

        public GauntletClock Clock => _clock;
        public GhostTrack LiveGhost => _live;
        public GhostTrack BestGhost => _best;
        public bool GhostVisible => ghost != null && ghost.gameObject.activeSelf;
        public bool EndScreenShown => _end != null && _end.style.display == DisplayStyle.Flex;
        /// <summary>The last run was (partly) driven by the autopilot, so it doesn't count for your best.</summary>
        public bool Autopiloted => _autopiloted;

        void Start()
        {
            if (game == null || !GameRoot.Exists) { enabled = false; return; }
            _current = this;
            TryInit();
        }

        /// <summary>Sets up once the lab has built its course (its Start may run after this one).</summary>
        void TryInit()
        {
            if (_clock != null || game.Gauntlet == null) return;
            _g = game.Gauntlet;
            _clock = new GauntletClock(_g);
            LoadRecord();
            if (ghost != null)
            {
                _ghostRenderers = ghost.GetComponentsInChildren<Renderer>(true);
                ghost.gameObject.SetActive(false);
            }
            BuildUi();
            game.OnPlaced = OnPlaced;
        }

        void OnDestroy()
        {
            if (_current == this) _current = null;
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
            _best.LoadFrom(_clock.record.ghost, _clock.record.ghostRate);
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
            _best.Clear();
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
            if (ghost != null) ghost.gameObject.SetActive(false);
            _hintShow = 6f;
        }

        void Update()
        {
            if (_clock == null) { TryInit(); if (_clock == null) return; }
            float dt = Time.deltaTime;
            bool allowed = GameRoot.Input.GameplayAllowed;
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            bool inGauntlet = _g.InGauntlet(game.S);
            if (allowed && inGauntlet && ((kb != null && kb.rKey.wasPressedThisFrame) || (pad != null && pad.selectButton.wasPressedThisFrame))) Restart();
            if (allowed && ((kb != null && kb.gKey.wasPressedThisFrame) || (pad != null && pad.dpad.left.wasPressedThisFrame))) ghostOn = !ghostOn;

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
                    _best.CopyFrom(_live);
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
            if (ghost == null) return;
            bool show = ghostOn && _clock.State == GauntletState.Running && _best.Count > 1;
            Vector3 at = Vector3.zero;
            float yaw = 0f;
            if (show)
            {
                show = _best.Sample(_clock.Time, out var p, out yaw);
                at = new Vector3(p.X, p.Y, p.Z);
            }
            if (ghost.gameObject.activeSelf != show) ghost.gameObject.SetActive(show);
            if (show) ghost.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
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
                int sec = _g.SectionAt(game.S);
                _sub.text = state == GauntletState.Ready ? "Cross the gate to start the clock"
                    : "Markers " + _clock.MarkersHit + "/" + _clock.MarkerCount + (sec >= 0 ? "   " + _g.sections[sec].name : "")
                      + (ghostOn && _best.Count > 1 ? "" : "   (ghost off: G)");
                _hint.text = state == GauntletState.Ready && _hintShow > 0f ? "R or View restarts at any time. G hides the ghost of your best run." : "";
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
            _sb.Append("The developer (autopilot)   ").Append(Fmt(GauntletCourse.DeveloperTime)).Append('\n');
            _sb.Append("Mags                        ").Append(Fmt(GauntletCourse.MagsTime)).Append('\n');
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
            _sb.Append("\nR or View: run it again    G: ghost on or off    Esc: menu");
            _endText.text = _sb.ToString();
        }
    }
}
