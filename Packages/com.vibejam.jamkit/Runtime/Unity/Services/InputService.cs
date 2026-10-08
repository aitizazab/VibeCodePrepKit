using JamKit.Flow;
using UnityEngine;
using UnityEngine.InputSystem;

namespace JamKit.Shell
{
    /// <summary>
    /// One action asset with two maps, both always enabled: Gameplay (Move, Look, Fire, AltFire,
    /// Focus (no default keys), Sprint, Dash, Interact, Pause) and UI (Navigate, Submit, Cancel, Point, Click, ScrollWheel).
    /// Keyboard + mouse and gamepad. Gameplay code reads e.g. <c>GameRoot.Input.Move.ReadValue&lt;Vector2&gt;()</c>
    /// and should check <see cref="GameplayAllowed"/> (false while paused, in dialogue or in menus).
    /// </summary>
    public sealed class InputService
    {
        public readonly InputActionAsset Asset;
        public readonly InputActionMap Gameplay;
        public readonly InputActionMap Ui;

        public readonly InputAction Move, Look, Fire, AltFire, Focus, Sprint, Dash, Interact, Pause;
        public readonly InputAction Navigate, Submit, Cancel, Point, Click, ScrollWheel;

        readonly GameFlow _flow;

        public InputService(InputActionAsset source, GameFlow flow)
        {
            _flow = flow;
            Asset = source != null ? Object.Instantiate(source) : DefaultControls.Create();
            Asset.name = "JamKitControls (runtime)";
            Gameplay = Asset.FindActionMap("Gameplay", true);
            Ui = Asset.FindActionMap("UI", true);
            Move = Gameplay.FindAction("Move", true);
            Look = Gameplay.FindAction("Look", true);
            Fire = Gameplay.FindAction("Fire", true);
            AltFire = Gameplay.FindAction("AltFire", true);
            Focus = Gameplay.FindAction("Focus", true);
            // Sprint (Left Shift / LB) took over Focus's keys; Focus has none by default (bind one to use it).
            Sprint = Gameplay.FindAction("Sprint", false);
            if (Sprint == null)
            {
                Sprint = Gameplay.AddAction("Sprint", InputActionType.Button);
                Sprint.AddBinding("<Keyboard>/leftShift");
                Sprint.AddBinding("<Gamepad>/leftShoulder");
            }
            Dash = Gameplay.FindAction("Dash", true);
            Interact = Gameplay.FindAction("Interact", true);
            Pause = Gameplay.FindAction("Pause", true);
            Navigate = Ui.FindAction("Navigate", true);
            Submit = Ui.FindAction("Submit", true);
            Cancel = Ui.FindAction("Cancel", true);
            Point = Ui.FindAction("Point", true);
            Click = Ui.FindAction("Click", true);
            ScrollWheel = Ui.FindAction("ScrollWheel", true);
            Asset.Enable();
        }

        /// <summary>True while the player is in control: Playing and no scene load running.</summary>
        public bool GameplayAllowed => _flow.Current == GameState.Playing && !GameRoot.IsLoadingScene;

        /// <summary>Move as a vector (zero when gameplay isn't allowed).</summary>
        public Vector2 MoveVector => GameplayAllowed ? Vector2.ClampMagnitude(Move.ReadValue<Vector2>(), 1f) : Vector2.zero;

        /// <summary>Look with the player's sensitivity and invert-Y applied (zero when gameplay isn't allowed).</summary>
        public Vector2 LookDelta(Settings s)
        {
            if (!GameplayAllowed) return Vector2.zero;
            Vector2 d = Look.ReadValue<Vector2>() * (s != null ? s.mouseSensitivity : 1f);
            if (s != null && s.invertY) d.y = -d.y;
            return d;
        }

        /// <summary>Pressed this frame, and gameplay is allowed.</summary>
        public bool Pressed(InputAction action) => GameplayAllowed && action.WasPressedThisFrame();

        public void Dispose()
        {
            Asset.Disable();
            Object.Destroy(Asset);
        }
    }

    /// <summary>
    /// The default bindings, in code. SceneBuilder writes them to
    /// Assets/JamKit/Input/JamKitControls.inputactions (once; edit that asset freely afterwards),
    /// and the runtime falls back to them if the asset is missing.
    /// </summary>
    public static class DefaultControls
    {
        public static InputActionAsset Create()
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.name = "JamKitControls";

            var g = asset.AddActionMap("Gameplay");
            var move = g.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");
            move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s").With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow").With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            move.AddBinding("<Gamepad>/leftStick");
            move.AddBinding("<Gamepad>/dpad");

            var look = g.AddAction("Look", InputActionType.Value, expectedControlLayout: "Vector2");
            look.AddBinding("<Mouse>/delta");
            look.AddBinding("<Gamepad>/rightStick").WithProcessor("scaleVector2(x=8,y=8)");

            Button(g, "Fire", "<Mouse>/leftButton", "<Keyboard>/j", "<Gamepad>/rightTrigger");
            Button(g, "AltFire", "<Mouse>/rightButton", "<Keyboard>/k", "<Gamepad>/leftTrigger");
            g.AddAction("Focus", InputActionType.Button); // no default keys (the bullet hell's optional Focus mode)
            Button(g, "Sprint", "<Keyboard>/leftShift", "<Gamepad>/leftShoulder");
            Button(g, "Dash", "<Keyboard>/space", "<Gamepad>/buttonSouth", "<Gamepad>/rightShoulder");
            Button(g, "Interact", "<Keyboard>/e", "<Gamepad>/buttonWest");
            Button(g, "Pause", "<Keyboard>/escape", "<Keyboard>/p", "<Gamepad>/start");

            var ui = asset.AddActionMap("UI");
            var nav = ui.AddAction("Navigate", InputActionType.PassThrough, expectedControlLayout: "Vector2");
            nav.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow").With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            nav.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s").With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            nav.AddBinding("<Gamepad>/leftStick");
            nav.AddBinding("<Gamepad>/dpad");
            Button(ui, "Submit", "<Keyboard>/enter", "<Keyboard>/numpadEnter", "<Keyboard>/space", "<Gamepad>/buttonSouth");
            Button(ui, "Cancel", "<Keyboard>/escape", "<Keyboard>/backspace", "<Gamepad>/buttonEast");
            var point = ui.AddAction("Point", InputActionType.PassThrough, expectedControlLayout: "Vector2");
            point.AddBinding("<Pointer>/position");
            var click = ui.AddAction("Click", InputActionType.PassThrough, expectedControlLayout: "Button");
            click.AddBinding("<Mouse>/leftButton");
            click.AddBinding("<Pen>/tip");
            click.AddBinding("<Touchscreen>/primaryTouch/press");
            var scroll = ui.AddAction("ScrollWheel", InputActionType.PassThrough, expectedControlLayout: "Vector2");
            scroll.AddBinding("<Mouse>/scroll");
            return asset;
        }

        static void Button(InputActionMap map, string name, params string[] paths)
        {
            var a = map.AddAction(name, InputActionType.Button);
            foreach (var p in paths) a.AddBinding(p);
        }
    }
}
