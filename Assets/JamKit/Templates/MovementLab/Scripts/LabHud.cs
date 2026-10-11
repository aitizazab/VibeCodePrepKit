using System.Globalization;
using JamKit.Movement;
using JamKit.Shell;
using UnityEngine;
using UnityEngine.UIElements;

namespace JamKit.MovementLab
{
    /// <summary>
    /// The lab's debug HUD, so the creator can read momentum while feeling it: speed in m/s and km/h,
    /// the movement state (and wall side, slide boost, double jump), a speed graph of the last 8 s with
    /// guide lines at walk, wallrun goal, sprint and the cap, the section and run time, and the chase
    /// gap. F3 or the gamepad's View button shows or hides it. A small centre dot always stays (comfort).
    /// Built in code on the shell's HUD layer. Labels refresh 10 times a second.
    /// </summary>
    public sealed class LabHud
    {
        const int Samples = 240;
        const float SampleEvery = 1f / 30f;

        readonly VisualElement _root, _panel, _graph, _chase, _chaseFill, _dot;
        readonly Label _speed, _kmh, _state, _extra, _section, _chaseText, _hint;
        readonly float[] _history = new float[Samples];
        int _head;
        float _sampleT, _textT;
        bool _shown = true;
        float _gapShown = -1f;

        public bool Shown => _shown;
        public string SpeedText => _speed.text;
        public string StateText => _state.text;
        public string ExtraText => _extra.text;

        static readonly Color Ink = new Color(0.95f, 0.96f, 1f);
        static readonly Color Dim = new Color(0.75f, 0.78f, 0.86f);

        public LabHud(VisualElement parent)
        {
            _root = new VisualElement { name = "lab-hud", pickingMode = PickingMode.Ignore };
            _root.style.position = Position.Absolute;
            _root.style.left = 0; _root.style.right = 0; _root.style.top = 0; _root.style.bottom = 0;
            parent.Add(_root);

            _dot = new VisualElement { name = "lab-dot", pickingMode = PickingMode.Ignore };
            _dot.style.position = Position.Absolute;
            _dot.style.left = Length.Percent(50); _dot.style.top = Length.Percent(50);
            _dot.style.width = 5; _dot.style.height = 5;
            _dot.style.marginLeft = -2.5f; _dot.style.marginTop = -2.5f;
            SetRadius(_dot, 2.5f);
            _dot.style.backgroundColor = new Color(1f, 1f, 1f, 0.75f);
            _root.Add(_dot);

            _panel = new VisualElement { name = "lab-panel", pickingMode = PickingMode.Ignore };
            _panel.style.position = Position.Absolute;
            _panel.style.left = 28; _panel.style.top = 120;
            _panel.style.width = 330;
            _panel.style.paddingLeft = 14; _panel.style.paddingRight = 14; _panel.style.paddingTop = 10; _panel.style.paddingBottom = 10;
            _panel.style.backgroundColor = new Color(0.08f, 0.09f, 0.14f, 0.62f);
            SetRadius(_panel, 10f);
            _root.Add(_panel);

            _speed = Text(_panel, "lab-speed", 40, Ink, FontStyle.Bold);
            _kmh = Text(_panel, "lab-kmh", 18, Dim, FontStyle.Normal);
            _state = Text(_panel, "lab-state", 20, new Color(0.62f, 0.9f, 0.88f), FontStyle.Bold);
            _extra = Text(_panel, "lab-extra", 14, Dim, FontStyle.Normal);

            _graph = new VisualElement { name = "lab-graph", pickingMode = PickingMode.Ignore };
            _graph.style.height = 96;
            _graph.style.marginTop = 6; _graph.style.marginBottom = 6;
            _graph.style.backgroundColor = new Color(0f, 0f, 0f, 0.25f);
            SetRadius(_graph, 4f);
            _graph.generateVisualContent += DrawGraph;
            _panel.Add(_graph);

            _section = Text(_panel, "lab-section", 15, Ink, FontStyle.Normal);
            _hint = Text(_panel, "lab-hint", 12, new Color(0.7f, 0.72f, 0.8f, 0.8f), FontStyle.Normal);
            _hint.text = "F3 / View: hide this panel   Esc: pause (Comfort and camera)";

            _chase = new VisualElement { name = "lab-chase", pickingMode = PickingMode.Ignore };
            _chase.style.position = Position.Absolute;
            _chase.style.top = 26; _chase.style.left = Length.Percent(50);
            _chase.style.width = 420; _chase.style.marginLeft = -210;
            _chase.style.alignItems = Align.Center;
            _root.Add(_chase);
            _chaseText = Text(_chase, "lab-chase-text", 18, Ink, FontStyle.Bold);
            var bar = new VisualElement { pickingMode = PickingMode.Ignore };
            bar.style.width = 420; bar.style.height = 10;
            bar.style.backgroundColor = new Color(0f, 0f, 0f, 0.4f);
            SetRadius(bar, 5f);
            _chase.Add(bar);
            _chaseFill = new VisualElement { pickingMode = PickingMode.Ignore };
            _chaseFill.style.height = 10;
            SetRadius(_chaseFill, 5f);
            bar.Add(_chaseFill);
            _chase.style.display = DisplayStyle.None;
        }

        static void SetRadius(VisualElement e, float r)
        {
            e.style.borderTopLeftRadius = r; e.style.borderTopRightRadius = r;
            e.style.borderBottomLeftRadius = r; e.style.borderBottomRightRadius = r;
        }

        static Label Text(VisualElement parent, string name, int size, Color color, FontStyle style)
        {
            var l = new Label("") { name = name, pickingMode = PickingMode.Ignore };
            l.style.fontSize = size;
            l.style.color = color;
            l.style.unityFontStyleAndWeight = style;
            l.style.marginTop = 0; l.style.marginBottom = 0; l.style.paddingTop = 0; l.style.paddingBottom = 0;
            parent.Add(l);
            return l;
        }

        public void Toggle() => SetShown(!_shown);

        public void SetShown(bool shown)
        {
            _shown = shown;
            _panel.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void Remove() => _root.RemoveFromHierarchy();

        /// <summary>Called every frame by the game.</summary>
        public void Tick(MovementController body, string section, float runTime, float best, bool chasing, float gap, float threat, float dt)
        {
            var sim = body.Sim;
            if (sim == null) return;
            // The hop-cost preset (F6 / LB + d-pad up), kept in the save.
            if (HopCostSwitch.Tick()) _textT = 0f;
            // The movement profile (F7 / LB + d-pad right): feel the daytime movement here.
            if (MovementProfileSwitch.Tick(body)) _textT = 0f;
            float speed = sim.HorizontalSpeed;
            _sampleT += dt;
            while (_sampleT >= SampleEvery)
            {
                _sampleT -= SampleEvery;
                _history[_head] = speed;
                _head = (_head + 1) % Samples;
            }
            if (_shown) _graph.MarkDirtyRepaint();

            if (chasing != (_chase.style.display == DisplayStyle.Flex)) _chase.style.display = chasing ? DisplayStyle.Flex : DisplayStyle.None;

            _textT -= Time.unscaledDeltaTime;
            if (_textT > 0f) return;
            _textT = 0.1f;
            var inv = CultureInfo.InvariantCulture;
            if (_shown)
            {
                _speed.text = speed.ToString("0.0", inv) + " m/s";
                _kmh.text = (speed * 3.6f).ToString("0", inv) + " km/h   top of graph 22 m/s";
                string state = sim.Mode.ToString();
                if (sim.Mode == MoveMode.Wallrun) state += sim.WallSide > 0 ? "  (wall right)" : "  (wall left)";
                if (sim.Mode == MoveMode.Sprint) state = sim.SprintToggled ? "Sprint (toggled)" : "Sprint";
                _state.text = state;
                float cd = sim.SlideBoostCooldown;
                _extra.text = (cd > 0f ? "Slide boost in " + cd.ToString("0.0", inv) + " s" : "Slide boost ready")
                    + "   Double jump " + (sim.DoubleJumpAvailable ? "ready" : "used")
                    + "\n" + HopCostSwitch.HudLine + "\n" + MovementProfileSwitch.HudLine(body)
                    + "\n" + InputLine(sim, inv)
                    + (sim.LastCatchMiss != WallCatchMiss.None && sim.SinceCatchMiss < 6f
                        ? "\nWall not caught: " + CatchWords(sim.LastCatchMiss) + " (" + sim.SinceCatchMiss.ToString("0.0", inv) + " s ago)" : "");
                _section.text = section + "   " + runTime.ToString("0.0", inv) + " s" + (best > 0f ? "   best " + best.ToString("0.0", inv) + " s" : "");
            }
            if (chasing && Mathf.Abs(gap - _gapShown) > 0.25f)
            {
                _gapShown = gap;
                _chaseText.text = "The static is " + Mathf.Max(0f, gap).ToString("0", inv) + " m behind";
                _chaseFill.style.width = Length.Percent(Mathf.Clamp01(gap / 24f) * 100f);
                _chaseFill.style.backgroundColor = Color.Lerp(new Color(0.6f, 0.85f, 0.95f), new Color(1f, 0.4f, 0.5f), threat);
            }
        }

        /// <summary>The input device in use and how fast the view turns; above the air-strafe turn cap (a mouse sweeping
        /// faster than any stick) the share of the strafe gain that still counts. One standard for both devices: the
        /// speed cap above holds on either.</summary>
        static string InputLine(MoveSim sim, CultureInfo inv)
        {
            bool pad = GameRoot.Exists && GameRoot.Input.PadMode;
            float rate = sim.YawRate, cap = sim.def.airStrafeTurnCap;
            string line = (pad ? "Input: gamepad" : "Input: keyboard and mouse") + "   view turn " + rate.ToString("0", inv) + " deg/s";
            if (cap > 0f && rate > cap) line += "  (air strafe counts " + (cap / rate * 100f).ToString("0", inv) + "%)";
            return line;
        }

        /// <summary>The wall-catch diagnostic in words (why the last runnable wall nearby in the air wasn't caught).</summary>
        public static string CatchWords(WallCatchMiss why)
        {
            switch (why)
            {
                case WallCatchMiss.OutOfReach: return "just out of reach";
                case WallCatchMiss.TooSlow: return "too slow (under 5 m/s)";
                case WallCatchMiss.TooLow: return "too low (under 0.6 m above the ground)";
                case WallCatchMiss.Crouching: return "crouch held";
                case WallCatchMiss.HeadOn: return "too head-on (more than 70 degrees off the wall)";
                case WallCatchMiss.NoStick: return "no stick (hold forward to start a wallrun)";
                case WallCatchMiss.StickAway: return "the stick pointed away from the wall";
                case WallCatchMiss.StickNotForward: return "the stick wasn't forward along the wall";
                case WallCatchMiss.SameWall: return "the same wall, higher than you left it";
                default: return "";
            }
        }

        void DrawGraph(MeshGenerationContext mgc)
        {
            var r = _graph.contentRect;
            if (r.width < 4f || r.height < 4f) return;
            var p = mgc.painter2D;
            const float max = 22f;
            // Guide lines: walk 5, wallrun goal 8.64, sprint 9.5, slide boost cap 13, the 22 cap at the top.
            Guide(p, r, 5f, max, new Color(1f, 1f, 1f, 0.12f));
            Guide(p, r, 8.64f, max, new Color(0.62f, 0.9f, 0.88f, 0.35f));
            Guide(p, r, 9.5f, max, new Color(1f, 1f, 1f, 0.3f));
            Guide(p, r, 13f, max, new Color(1f, 0.8f, 0.45f, 0.3f));
            p.strokeColor = new Color(0.98f, 0.86f, 0.5f, 1f);
            p.lineWidth = 2f;
            p.BeginPath();
            for (int i = 0; i < Samples; i++)
            {
                float v = _history[(_head + i) % Samples];
                float x = r.xMin + r.width * i / (Samples - 1);
                float y = r.yMax - r.height * Mathf.Clamp01(v / max);
                if (i == 0) p.MoveTo(new Vector2(x, y));
                else p.LineTo(new Vector2(x, y));
            }
            p.Stroke();
        }

        static void Guide(Painter2D p, Rect r, float v, float max, Color c)
        {
            float y = r.yMax - r.height * (v / max);
            p.strokeColor = c;
            p.lineWidth = 1f;
            p.BeginPath();
            p.MoveTo(new Vector2(r.xMin, y));
            p.LineTo(new Vector2(r.xMax, y));
            p.Stroke();
        }
    }
}
