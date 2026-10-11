using System.Collections.Generic;
using System.Text;
using JamKit.Movement;
using JamKit.Presenting;
using JamKit.Shell;
using JamKit.Stealth;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace JamKit.StealthLab
{
    /// <summary>
    /// The Stealth Lab (docs/STEALTH.md "The Stealth Lab"): a grey-box yard (core <see cref="StealthYard"/>) to test the
    /// stealth kit with the movement kit. Two rounds, picked by presenter chapter: <b>hide and seek</b> (the seeker counts
    /// to ten facing away, then searches every hiding spot and corner; found means the round's over) and <b>patrol</b>
    /// (a guard walks a loop; get spotted and it chases, lose it and it searches, then gives up). Either way there are
    /// five things to collect, each reached a different way (a mantle, a wallrun, a slide, the closet, the dark alley).
    /// R / View restarts the round. The stealth HUD shows the detection meter and what's left to find.
    /// <para>The answers to "the movement trivializes stealth" (docs/STEALTH.md "When the movement outruns the guards"),
    /// each switchable and combinable, shown on the panel: <b>1</b> loud movement (A), <b>2</b> the dream pushes back
    /// (B), <b>3</b> a daytime section (C1), <b>4</b> lucidity fades when watched (C2), <b>5</b> three guards; on a pad,
    /// LB + d-pad up / right / down / left and LB + Y. The presenter has a chapter for each.</para>
    /// </summary>
    public sealed class StealthLabGame : MonoBehaviour
    {
        public enum Round { HideAndSeek, Patrol }

        public MovementController player;
        public SeekerAgent seeker;
        [Tooltip("Guards 2 and 3 (the three-guard option; patrol rounds only).")]
        public List<SeekerAgent> extraGuards = new List<SeekerAgent>();
        public CollectibleCounter collectibles;
        public Vector3 playerStart;
        public float playerYaw;
        [Range(0f, 1f)] public float ambientLight = 0.75f;

        static StealthLabGame _current;
        StealthHud _hud;
        Round _round = Round.HideAndSeek;
        bool _over, _threeGuards;
        int _founds, _rounds, _calls, _heard;
        VisualElement _panel;
        Label _panelText;
        float _panelT;
        readonly StringBuilder _sb = new StringBuilder(256);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _current = null;

        public static StealthLabGame Current => _current;
        public Round CurrentRound => _round;
        public bool RoundOver => _over;
        public int Founds => _founds;
        public int Calls => _calls;
        public int Heard => _heard;
        public StealthHud Hud => _hud;
        public bool ThreeGuards => _threeGuards;
        public StealthRules Rules => StealthRules.Shared;
        public string PanelText => _panelText != null ? _panelText.text : "";

        void Start()
        {
            if (!GameRoot.Exists || player == null || seeker == null)
            {
                Debug.LogError("StealthLabGame: missing the player or the seeker. Rebuild with JamKit > Rebuild Shell Scenes.");
                enabled = false;
                return;
            }
            _current = this;
            StealthRules.ResetShared();
            StealthLightZone.Ambient = ambientLight;
            if (collectibles == null) collectibles = CollectibleCounter.Current;
            GameRoot.Hud.Show();
            GameRoot.Hud.ShowHealth(false);
            GameRoot.Hud.SetScoreText("");
            _hud = new StealthHud(GameRoot.Hud.Root);
            BuildPanel(GameRoot.Hud.Root);
            MovementComfortScreen.Ensure();
            StealthLabPresenter.Register(PresenterMode.Chapters, this, this);
            GameRoot.Run.Set("Lab", "Stealth Lab");
            foreach (var g in Guards()) Hook(g);
            collectibles.AllFound += OnAllFound;
            Begin(Round.HideAndSeek);
        }

        void OnDestroy()
        {
            if (_current == this) _current = null;
            foreach (var g in Guards()) Unhook(g);
            if (collectibles != null) collectibles.AllFound -= OnAllFound;
            if (_hud != null) _hud.Remove();
            if (_panel != null) _panel.RemoveFromHierarchy();
            StealthRules.ResetShared();
            if (GameRoot.Exists) PresenterMode.Unregister(this);
        }

        IEnumerable<SeekerAgent> Guards()
        {
            if (seeker != null) yield return seeker;
            foreach (var g in extraGuards) if (g != null) yield return g;
        }

        void Hook(SeekerAgent g) { g.Found += OnFound; g.Called += OnCalled; g.Heard += OnHeard; }
        void Unhook(SeekerAgent g) { if (g == null) return; g.Found -= OnFound; g.Called -= OnCalled; g.Heard -= OnHeard; }

        /// <summary>Starts a round from the beginning: the player at the start, every collectible back, the guards home
        /// (counting, for hide and seek). <paramref name="countSeconds"/> &lt; 0: the tuning's count.</summary>
        public void Begin(Round round, float countSeconds = -1f)
        {
            _round = round;
            _over = false;
            _rounds++;
            player.Teleport(playerStart, playerYaw, Vector3.zero);
            if (StealthPlayerRules.Current != null) StealthPlayerRules.Current.ResetFade();
            if (collectibles != null) collectibles.ResetAll();
            seeker.SetMode(round == Round.HideAndSeek ? SeekerMode.HideAndSeek : SeekerMode.Patrol, countSeconds);
            bool extra = _threeGuards && round == Round.Patrol;
            foreach (var g in extraGuards)
            {
                if (g == null) continue;
                g.gameObject.SetActive(extra);
                if (extra) g.SetMode(SeekerMode.Patrol);
            }
            _panelT = 0f;
            if (!GameRoot.Exists) return;
            GameRoot.Hud.Message(round == Round.HideAndSeek
                ? "Hide and seek: it counts to ten, then comes looking. Hide (bushes, under the deck, the closet), or keep moving and grab all five."
                : (extra ? "Patrol, three guards: " : "Patrol: a guard walks the yard. ") + "Grab all five without being found. Crouch behind low walls, keep to the dark.", 6f);
        }

        // ---------- The answers ----------

        public void SetLoudMovement(bool on) { StealthRules.Shared.loudMovement = on; _panelT = 0f; }
        public void SetDreamPushesBack(bool on) { StealthRules.Shared.dreamPushesBack = on; _panelT = 0f; }
        public void SetDaytime(bool on) { StealthRules.Shared.daytimeSection = on; _panelT = 0f; }
        public void SetLucidity(bool on) { StealthRules.Shared.lucidityFade = on; _panelT = 0f; }

        public void SetThreeGuards(bool on)
        {
            _threeGuards = on;
            _panelT = 0f;
            if (_round == Round.Patrol) Begin(Round.Patrol);
        }

        /// <summary>A chapter: set the answers, then a patrol round.</summary>
        public void Try(bool loud, bool pushBack, bool daytime, bool lucidity, bool three)
        {
            SetLoudMovement(loud);
            SetDreamPushesBack(pushBack);
            SetDaytime(daytime);
            SetLucidity(lucidity);
            _threeGuards = three;
            Begin(Round.Patrol);
        }

        void Toggle(string what, bool on)
        {
            if (GameRoot.Exists) GameRoot.Hud.Message(what + (on ? ": on" : ": off"), 2f);
        }

        // ---------- Events ----------

        void OnFound(SeekerAgent s)
        {
            _founds++;
            _over = true;
            foreach (var g in Guards()) g.Frozen = true;
            if (!GameRoot.Exists) return;
            GameRoot.Audio.Play("ui_back", 0.8f);
            GameRoot.Hud.Message((_round == Round.HideAndSeek ? "Found you! " : "Spotted! ") + collectibles.Summary + ". R (View) to play again.", 5f);
        }

        void OnCalled(SeekerAgent s, int came)
        {
            _calls++;
            if (came > 0 && GameRoot.Exists) GameRoot.Hud.Message(s.name + " calls out: " + came + (came == 1 ? " guard comes" : " guards come"), 2f);
        }

        void OnHeard(SeekerAgent s)
        {
            _heard++;
            if (GameRoot.Exists && s.Brain.Events.becameWary) GameRoot.Audio.Play("blip", 0.45f);
        }

        void OnAllFound()
        {
            if (_over) return;
            _over = true;
            foreach (var g in Guards()) g.Frozen = true;
            if (GameRoot.Exists) GameRoot.Hud.Message("All five, never found. R (View) to play again.", 5f);
        }

        // ---------- Frame ----------

        void Update()
        {
            if (_hud != null) _hud.Tick();
            _panelT -= Time.unscaledDeltaTime;
            if (_panelT <= 0f) { _panelT = 0.25f; WritePanel(); }
            if (!GameRoot.Exists || !GameRoot.Input.GameplayAllowed) return;
            if (GameRoot.Input.Pressed(GameRoot.Input.Restart)) Begin(_round);
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            bool lb = pad != null && pad.leftShoulder.isPressed;
            var r = StealthRules.Shared;
            if ((kb != null && kb.digit1Key.wasPressedThisFrame) || (lb && pad.dpad.up.wasPressedThisFrame)) { SetLoudMovement(!r.loudMovement); Toggle("A: loud movement", r.loudMovement); }
            if ((kb != null && kb.digit2Key.wasPressedThisFrame) || (lb && pad.dpad.right.wasPressedThisFrame)) { SetDreamPushesBack(!r.dreamPushesBack); Toggle("B: the dream pushes back", r.dreamPushesBack); }
            if ((kb != null && kb.digit3Key.wasPressedThisFrame) || (lb && pad.dpad.down.wasPressedThisFrame)) { SetDaytime(!r.daytimeSection); Toggle("C1: a daytime section", r.daytimeSection); }
            if ((kb != null && kb.digit4Key.wasPressedThisFrame) || (lb && pad.dpad.left.wasPressedThisFrame)) { SetLucidity(!r.lucidityFade); Toggle("C2: being watched dims her lucidity", r.lucidityFade); }
            if ((kb != null && kb.digit5Key.wasPressedThisFrame) || (lb && pad.buttonNorth.wasPressedThisFrame)) { SetThreeGuards(!_threeGuards); Toggle("Three guards", _threeGuards); }
        }

        void BuildPanel(VisualElement root)
        {
            _panel = new VisualElement { name = "stealth-lab-panel", pickingMode = PickingMode.Ignore };
            _panel.style.position = Position.Absolute;
            _panel.style.left = 24; _panel.style.bottom = 24; _panel.style.width = 390;
            _panel.style.paddingLeft = 12; _panel.style.paddingRight = 12; _panel.style.paddingTop = 8; _panel.style.paddingBottom = 8;
            _panel.style.backgroundColor = new Color(0.06f, 0.07f, 0.12f, 0.58f);
            _panel.style.borderTopLeftRadius = 8; _panel.style.borderTopRightRadius = 8; _panel.style.borderBottomLeftRadius = 8; _panel.style.borderBottomRightRadius = 8;
            _panelText = new Label("") { pickingMode = PickingMode.Ignore };
            _panelText.style.fontSize = 14;
            _panelText.style.color = new Color(0.92f, 0.94f, 1f);
            _panelText.style.whiteSpace = WhiteSpace.Normal;
            _panel.Add(_panelText);
            root.Add(_panel);
        }

        void WritePanel()
        {
            if (_panelText == null) return;
            var r = StealthRules.Shared;
            _sb.Clear();
            _sb.Append("Answers to fast movement (keys 1-5, or LB + d-pad / Y)\n");
            Line("1  A  loud movement", r.loudMovement);
            Line("2  B  the dream pushes back (calls, speed noticed)", r.dreamPushesBack);
            Line("3  C1 a daytime section (no dream powers)", r.daytimeSection);
            Line("4  C2 being watched dims her lucidity", r.lucidityFade);
            Line("5  three guards (patrol)", _threeGuards);
            _sb.Append(_round == Round.HideAndSeek ? "Round: hide and seek" : "Round: patrol").Append("   heard ").Append(_heard).Append("   calls ").Append(_calls);
            _panelText.text = _sb.ToString();
        }

        void Line(string what, bool on) => _sb.Append(on ? "[on]  " : "[  ]  ").Append(what).Append('\n');
    }
}
