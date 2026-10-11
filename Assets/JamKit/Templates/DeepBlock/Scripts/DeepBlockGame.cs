using System.Collections.Generic;
using System.Globalization;
using System.Text;
using JamKit.DeepBlock;
using JamKit.Flow;
using JamKit.Movement;
using JamKit.Presenting;
using JamKit.Shell;
using JamKit.Stealth;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace JamKit.DeepBlockLab
{
    /// <summary>
    /// The Deep Block lab (docs/DEEP-BLOCK.md): a walkable greybox of layout A of docs/DENSE-STREET.md, built from core
    /// <see cref="DeepBlock.DeepBlock"/>. Chapters put you at the start of each of the four routes to the hill gate (the
    /// street, the back, the roofs, under) or start <b>hide and seek</b> in the gardens (Theo counts on the trampoline,
    /// then searches the eight hiding places while you find the six memories; found is a laugh and a recount, never a
    /// fail). Debug keys: <b>F7</b> (or LB + d-pad right) picks the night (1-4), which opens and shuts the eight switches;
    /// <b>F8</b> (or LB + d-pad left) lets the course autopilot run the current route; F3 / d-pad down shows or hides
    /// the panel. Reaching the hill gate's apron says how long the route took.
    /// </summary>
    public sealed class DeepBlockGame : MonoBehaviour
    {
        public enum Mode { FreeRoam, Route, HideAndSeek }

        public MovementController player;
        public SeekerAgent theo;
        public CollectibleCounter memories;
        public Transform switchRoot;
        [Range(1, DeepBlock.DeepBlock.NightCount)] public int night = 1;

        static DeepBlockGame _current;
        DeepBlock.DeepBlock _block;
        readonly List<BlockSwitchProp> _props = new List<BlockSwitchProp>();
        bool[] _open;
        Mode _mode = Mode.FreeRoam;
        int _route = -1;
        CourseAutopilot _pilot;
        float _routeTime;
        bool _timing;
        float _recount = -1f;
        int _theoFinds, _routesDone;
        StealthHud _stealthHud;
        VisualElement _panel;
        Label _panelText;
        float _panelT;
        bool _panelShown = true;
        readonly StringBuilder _sb = new StringBuilder(256);
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _current = null;

        public static DeepBlockGame Current => _current;
        public DeepBlock.DeepBlock Block => _block;
        public Mode CurrentMode => _mode;
        public int CurrentRoute => _route;
        public bool[] Open => _open;
        public bool Timing => _timing;
        public float RouteTime => _routeTime;
        public int RoutesDone => _routesDone;
        public int TheoFinds => _theoFinds;
        public bool AutopilotOn => player != null && player.Autopilot != null;
        public string PanelText => _panelText != null ? _panelText.text : "";
        public StealthHud StealthHud => _stealthHud;

        void Start()
        {
            if (!GameRoot.Exists || player == null)
            {
                Debug.LogError("DeepBlockGame: missing the player. Rebuild with JamKit > Rebuild Shell Scenes.");
                enabled = false;
                return;
            }
            _current = this;
            _block = DeepBlock.DeepBlock.Build();
            if (switchRoot != null) switchRoot.GetComponentsInChildren(true, _props);
            else _props.AddRange(FindObjectsByType<BlockSwitchProp>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            StealthLightZone.Ambient = 1f;
            GameRoot.Hud.Show();
            GameRoot.Hud.ShowHealth(false);
            GameRoot.Hud.SetScoreText("");
            MovementComfortScreen.Ensure();
            BuildPanel(GameRoot.Hud.Root);
            _stealthHud = new StealthHud(GameRoot.Hud.Root);
            _stealthHud.Show(false);
            if (theo != null) theo.Found += OnTheoFound;
            if (memories != null) memories.AllFound += OnAllMemories;
            DeepBlockPresenter.Register(PresenterMode.Chapters, this, this);
            GameRoot.Run.Set("Lab", "The deep block");
            SetNight(night);
            FreeRoam();
            GameRoot.Hud.Message("The deep block (layout A). F7: night. F8: autopilot on a route. Esc > chapters: the four routes, hide and seek.", 6f);
        }

        void OnDestroy()
        {
            if (_current == this) _current = null;
            if (theo != null) theo.Found -= OnTheoFound;
            if (memories != null) memories.AllFound -= OnAllMemories;
            if (_stealthHud != null) _stealthHud.Remove();
            if (_panel != null) _panel.RemoveFromHierarchy();
            if (GameRoot.Exists) PresenterMode.Unregister(this);
        }

        // ---------- Nights ----------

        public void SetNight(int n)
        {
            night = Mathf.Clamp(n, 1, DeepBlock.DeepBlock.NightCount);
            _open = DeepBlock.DeepBlock.Night(night);
            foreach (var p in _props) if (p != null) p.Apply(_open);
            _panelT = 0f;
        }

        // ---------- Modes ----------

        public void FreeRoam()
        {
            _mode = Mode.FreeRoam;
            _route = -1;
            _timing = false;
            SetAutopilot(false);
            Theo(false);
        }

        /// <summary>Puts the player at a route's start (night 1's switches unless another night is set), timing to the gate.</summary>
        public void StartRoute(int index)
        {
            if (_block == null || index < 0 || index >= _block.routes.Count) return;
            SetAutopilot(false);
            Theo(false);
            _mode = Mode.Route;
            _route = index;
            var r = _block.routes[index];
            player.Teleport(ToU(r.start), r.startYaw, Vector3.zero);
            _pilot = new CourseAutopilot(r.course);
            _pilot.Reset(0f);
            _routeTime = 0f;
            _timing = true;
            if (GameRoot.Exists) GameRoot.Hud.Message(r.name + ": to the hill gate. F8 lets the autopilot run it.", 4f);
        }

        public void StartHideAndSeek(float countSeconds = -1f)
        {
            SetAutopilot(false);
            _mode = Mode.HideAndSeek;
            _route = -1;
            _timing = false;
            if (night != 2) SetNight(2);
            player.Teleport(ToU(_block.playerHideStart), _block.playerHideYaw, Vector3.zero);
            if (memories != null) memories.ResetAll();
            Theo(true, countSeconds);
            if (GameRoot.Exists) GameRoot.Hud.Message("Hide and seek: Theo counts to ten on the trampoline. Hide, and find the six memories while he looks.", 6f);
        }

        void Theo(bool on, float countSeconds = -1f)
        {
            _recount = -1f;
            if (_stealthHud != null) _stealthHud.Show(on);
            if (theo == null) return;
            theo.gameObject.SetActive(on);
            if (on) theo.Restart(countSeconds);
            else theo.Frozen = true;
        }

        void OnTheoFound(SeekerAgent s)
        {
            _theoFinds++;
            s.Frozen = true;
            _recount = 3f;
            if (GameRoot.Exists)
            {
                GameRoot.Audio.Play("coin", 0.6f);
                GameRoot.Hud.Message("\"Found you!\" Theo laughs and counts again. " + (memories != null ? memories.Summary : ""), 3f);
            }
        }

        void OnAllMemories()
        {
            if (GameRoot.Exists) GameRoot.Hud.Message("All six memories found.", 4f);
        }

        // ---------- The autopilot ----------

        public void SetAutopilot(bool on)
        {
            if (!on || _pilot == null || _mode != Mode.Route) { if (player != null) player.Autopilot = null; return; }
            var r = _block.routes[_route];
            var p = player.transform.position;
            _pilot.Reset(r.course.route.Project(new System.Numerics.Vector3(p.x, p.y, p.z)) - 1f);
            player.Autopilot = _pilot.Next;
        }

        // ---------- Frame ----------

        void Update()
        {
            if (_block == null) return;
            if (_stealthHud != null && _mode == Mode.HideAndSeek) _stealthHud.Tick();
            bool allowed = GameRoot.Input.GameplayAllowed;
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            if (allowed)
            {
                bool lb = pad != null && pad.leftShoulder.isPressed;
                if ((kb != null && kb.f7Key.wasPressedThisFrame) || (lb && pad.dpad.right.wasPressedThisFrame))
                {
                    SetNight(night % DeepBlock.DeepBlock.NightCount + 1);
                    GameRoot.Hud.Message(DeepBlock.DeepBlock.NightWords(night), 3f);
                }
                if ((kb != null && kb.f8Key.wasPressedThisFrame) || (lb && pad.dpad.left.wasPressedThisFrame))
                {
                    if (_mode != Mode.Route) StartRoute(0);
                    SetAutopilot(!AutopilotOn);
                }
                if ((kb != null && kb.f3Key.wasPressedThisFrame) || (pad != null && !lb && pad.dpad.down.wasPressedThisFrame))
                {
                    _panelShown = !_panelShown;
                    _panel.style.display = _panelShown ? DisplayStyle.Flex : DisplayStyle.None;
                }
                if (GameRoot.Input.Pressed(GameRoot.Input.Restart))
                {
                    if (_mode == Mode.Route) StartRoute(_route);
                    else if (_mode == Mode.HideAndSeek) StartHideAndSeek();
                }
            }

            if (_mode == Mode.Route && _timing && GameRoot.Flow.Current == GameState.Playing)
            {
                _routeTime += Time.deltaTime;
                var r = _block.routes[_route];
                var p = player.transform.position;
                float s = r.course.route.Project(new System.Numerics.Vector3(p.x, p.y, p.z));
                if (s >= r.finishS - 0.5f && p.y > 5.9f)
                {
                    _timing = false;
                    _routesDone++;
                    SetAutopilot(false);
                    GameRoot.Audio.Play("win", 0.5f);
                    GameRoot.Hud.Message(r.name + ": the hill gate in " + _routeTime.ToString("0.0", Inv) + " s. R (View) to run it again.", 5f);
                }
            }
            if (_recount > 0f)
            {
                _recount -= Time.deltaTime;
                if (_recount <= 0f && theo != null && _mode == Mode.HideAndSeek) theo.Restart();
            }
            // A fall out of the world: back to the route's start, or the garden.
            if (player.transform.position.y < -8f)
            {
                if (_mode == Mode.Route) StartRoute(_route);
                else player.Teleport(ToU(_block.playerHideStart), _block.playerHideYaw, Vector3.zero);
            }
            _panelT -= Time.unscaledDeltaTime;
            if (_panelT <= 0f) { _panelT = 0.25f; WritePanel(); }
        }

        static Vector3 ToU(System.Numerics.Vector3 v) => new Vector3(v.X, v.Y, v.Z);

        // ---------- The panel ----------

        void BuildPanel(VisualElement root)
        {
            _panel = new VisualElement { name = "deep-block-panel", pickingMode = PickingMode.Ignore };
            _panel.style.position = Position.Absolute;
            _panel.style.left = 24; _panel.style.top = 110; _panel.style.width = 360;
            _panel.style.paddingLeft = 12; _panel.style.paddingRight = 12; _panel.style.paddingTop = 8; _panel.style.paddingBottom = 8;
            _panel.style.backgroundColor = new Color(0.12f, 0.08f, 0.06f, 0.58f);
            _panel.style.borderTopLeftRadius = 8; _panel.style.borderTopRightRadius = 8; _panel.style.borderBottomLeftRadius = 8; _panel.style.borderBottomRightRadius = 8;
            _panelText = new Label("") { pickingMode = PickingMode.Ignore };
            _panelText.style.fontSize = 14;
            _panelText.style.color = new Color(1f, 0.94f, 0.84f);
            _panelText.style.whiteSpace = WhiteSpace.Normal;
            _panel.Add(_panelText);
            root.Add(_panel);
        }

        void WritePanel()
        {
            if (_panelText == null || !_panelShown) return;
            _sb.Clear();
            _sb.Append("Night ").Append(night).Append("  (F7 / LB + d-pad right)\n");
            for (int i = 0; i < _block.switches.Count; i++)
            {
                var sw = _block.switches[i];
                _sb.Append(sw.id).Append(' ').Append(_open[i] ? "open " : "SHUT ").Append(sw.name).Append('\n');
            }
            _sb.Append("Sheets on the line: ").Append(_open[DeepBlock.DeepBlock.SheetsSwitch] ? "up" : "down").Append('\n');
            switch (_mode)
            {
                case Mode.Route:
                    _sb.Append("Route: ").Append(_block.routes[_route].name).Append("  ").Append(_routeTime.ToString("0.0", Inv)).Append(" s")
                        .Append(AutopilotOn ? "  (autopilot)" : "").Append("\nF8 / LB + d-pad left: autopilot");
                    break;
                case Mode.HideAndSeek:
                    _sb.Append("Hide and seek: Theo ").Append(theo != null ? theo.State.ToString().ToLowerInvariant() : "-")
                        .Append(", found you ").Append(_theoFinds).Append("x. ").Append(memories != null ? memories.Summary : "");
                    break;
                default:
                    _sb.Append("Free roam. Chapters (Esc): the four routes, hide and seek.");
                    break;
            }
            _panelText.text = _sb.ToString();
        }
    }
}
