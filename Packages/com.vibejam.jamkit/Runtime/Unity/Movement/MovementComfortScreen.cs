using System;
using System.Globalization;
using JamKit.Flow;
using JamKit.Shell;
using UnityEngine;
using UnityEngine.UIElements;

namespace JamKit.Movement
{
    /// <summary>
    /// "Comfort and camera": the first-person movement's comfort settings (docs/MOVEMENT.md): field of
    /// view, camera roll, head bob and landing dip, motion effects (every speed visual), speed
    /// lines on or off, and "reduce effects" (no edge blur or chromatic pulse, subtle streaks). They're saved with the rest of the settings. A scene with movement calls
    /// <see cref="Ensure"/> in Start (adds this overlay and a "Comfort and camera" button on the pause
    /// screen) and <see cref="Remove"/> in OnDestroy. Built in code: no UXML.
    /// </summary>
    public sealed class MovementComfortScreen : ShellScreen
    {
        public const string PauseButton = "movement-comfort";

        static MovementComfortScreen _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _instance = null;

        public static MovementComfortScreen Ensure()
        {
            if (!GameRoot.Exists) return null;
            if (_instance == null || _instance.Root == null || _instance.Root.panel == null)
                _instance = GameRoot.Ui.AddScreen(new MovementComfortScreen(), null);
            GameRoot.Ui.Pause.AddButton(PauseButton, "Comfort and camera", () => GameRoot.Ui.OpenOverlay(_instance), "howto");
            GameRoot.Ui.Pause.SetButtonShown(PauseButton, true);
            return _instance;
        }

        public static void Remove()
        {
            if (!GameRoot.Exists || GameRoot.Ui == null || GameRoot.Ui.Pause == null) return;
            GameRoot.Ui.Pause.SetButtonShown(PauseButton, false);
            if (_instance != null && GameRoot.Ui.IsOverlayOpen(_instance)) GameRoot.Ui.CloseOverlay(false);
        }

        protected override bool BuildsOwnContent => true;

        Slider _fov, _roll, _bob, _motion;
        Toggle _lines, _reduce;

        static Settings S => GameRoot.Settings;

        protected override void OnInit()
        {
            Root.pickingMode = PickingMode.Position;
            var screen = new VisualElement { name = "movement-comfort-screen" };
            screen.AddToClassList("jk-screen");
            screen.AddToClassList("jk-scrim");
            Root.Add(screen);
            var panel = new VisualElement();
            panel.AddToClassList("jk-panel");
            panel.AddToClassList("jk-panel--wide");
            screen.Add(panel);
            var title = new Label("Comfort and camera");
            title.AddToClassList("jk-heading");
            panel.Add(title);
            _fov = AddSlider(panel, "comfort-fov", "Field of view", Settings.MinFieldOfView, Settings.MaxFieldOfView, 1f, v => S.fieldOfView = v);
            _roll = AddSlider(panel, "comfort-roll", "Camera roll (wallrun lean)", 0f, 100f, 10f, v => S.cameraRoll = v / 100f);
            _bob = AddSlider(panel, "comfort-bob", "Head bob and landing dip", 0f, 100f, 10f, v => S.headBob = v / 100f);
            _motion = AddSlider(panel, "comfort-motion", "Speed effects (FOV kick, streaks, speed lines)", 0f, 100f, 10f, v => S.motionEffects = v / 100f);
            _lines = new Toggle("Speed lines") { name = "comfort-lines" };
            _lines.AddToClassList("jk-option");
            _lines.RegisterValueChangedCallback(e => { S.speedLines = e.newValue; Changed(); });
            panel.Add(_lines);
            // docs/SPEED-FEEL.md: no edge zoom blur, no chromatic pulse, subtle streaks and lines.
            _reduce = new Toggle("Reduce effects (no blur or colour flash)") { name = "comfort-reduce" };
            _reduce.AddToClassList("jk-option");
            _reduce.RegisterValueChangedCallback(e => { S.reduceEffects = e.newValue; Changed(); });
            panel.Add(_reduce);
            var note = new Label("Lower these if fast movement makes you queasy. A centre dot stays on screen.");
            note.AddToClassList("jk-option-info");
            panel.Add(note);
            var back = new Button { name = "back", text = "Back" };
            back.AddToClassList("jk-button");
            back.AddToClassList("jk-button--back");
            panel.Add(back);
            Bind("back", () => Ui.CloseOverlay());
        }

        Slider AddSlider(VisualElement panel, string name, string label, float low, float high, float navStep, Action<float> write)
        {
            var s = new Slider(label, low, high) { name = name, focusable = true, userData = label };
            s.AddToClassList("jk-option");
            panel.Add(s);
            s.RegisterValueChangedCallback(e =>
            {
                write(e.newValue);
                Changed();
                Label(s);
            });
            s.RegisterCallback<NavigationMoveEvent>(e =>
            {
                if (e.direction != NavigationMoveEvent.Direction.Left && e.direction != NavigationMoveEvent.Direction.Right) return;
                float step = e.direction == NavigationMoveEvent.Direction.Right ? navStep : -navStep;
                s.value = Mathf.Clamp(s.value + step, s.lowValue, s.highValue);
                e.StopPropagation();
                s.focusController?.IgnoreEvent(e);
            }, TrickleDown.TrickleDown);
            return s;
        }

        static void Label(Slider s)
        {
            string baseLabel = s.userData as string ?? s.label;
            s.label = s.name == "comfort-fov"
                ? baseLabel + "   " + Mathf.RoundToInt(s.value).ToString(CultureInfo.InvariantCulture) + " deg"
                : baseLabel + "   " + Mathf.RoundToInt(s.value).ToString(CultureInfo.InvariantCulture) + "%";
        }

        void Changed()
        {
            GameRoot.Save.NotifySettingsChanged();
            Ui.PlayUiSound("ui_move");
        }

        protected override void OnShow()
        {
            var s = S;
            Set(_fov, s.fieldOfView);
            Set(_roll, s.cameraRoll * 100f);
            Set(_bob, s.headBob * 100f);
            Set(_motion, s.motionEffects * 100f);
            _lines?.SetValueWithoutNotify(s.speedLines);
            _reduce?.SetValueWithoutNotify(s.reduceEffects);
        }

        static void Set(Slider sl, float v)
        {
            if (sl == null) return;
            sl.SetValueWithoutNotify(v);
            Label(sl);
        }
    }
}
