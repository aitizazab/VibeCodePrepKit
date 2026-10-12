using JamKit.Flow;
using UnityEngine;
using UnityEngine.InputSystem;

namespace JamKit.Shell
{
    /// <summary>
    /// One action asset with two maps, both always enabled: Gameplay (Move, Look, Fire, AltFire,
    /// Focus (no default keys), Sprint, Dash, Crouch, Interact, Pause, and the Movement Lab's Restart and
    /// Ghost) and UI (Navigate, Submit, Cancel, Point, Click, ScrollWheel).
    /// Keyboard + mouse and gamepad. Gameplay code reads e.g. <c>GameRoot.Input.Move.ReadValue&lt;Vector2&gt;()</c>
    /// and should check <see cref="GameplayAllowed"/> (false while paused, in dialogue or in menus).
    /// The player's rebinds (Settings > Controls) live in <see cref="Bindings"/>.
    /// </summary>
    public sealed class InputService
    {
        public readonly InputActionAsset Asset;
        public readonly InputActionMap Gameplay;
        public readonly InputActionMap Ui;

        public readonly InputAction Move, Look, Fire, AltFire, Focus, Sprint, SprintToggle, Dash, Crouch, CrouchToggle, Interact, Pause;
        /// <summary>Restart a timed run (R, View / Share) and show or hide the best run's ghost (G, d-pad left):
        /// the Movement Lab's Gauntlet. Their Controls rows are hidden unless a scene shows them.</summary>
        public readonly InputAction Restart, Ghost;
        /// <summary>Peeking: hold to lean the eyes left or right round a corner (Q / LB, F / RB). Off (unbound, rows hidden)
        /// unless a template turns them on with <see cref="ControlBindings.SetPeekOnBumpers"/>, which also takes LB off
        /// Sprint and RB off Jump (Visiting Hours does; L3 and Shift still sprint, A and Space still jump).</summary>
        public readonly InputAction PeekLeft, PeekRight;
        /// <summary>The argument battle's verb shortcuts (docs/ARGUMENT-PROTOTYPE.md; A / Enter picks from the verb row too): Ask (1, X / Square), Challenge (2, RB / R1),
        /// Concede (3, Y / Triangle), Press (hold 4, B / Circle), Let it rest (5, View / Share). Their Controls rows show
        /// while a battle runs; they never clash with walking-around keys (a separate context).</summary>
        public readonly InputAction ArgueAsk, ArgueChallenge, ArgueConcede, ArguePress, ArgueRest;
        public readonly InputAction Navigate, Submit, Cancel, Point, Click, ScrollWheel;

        /// <summary>Rebinding: the Controls screen's rows, conflicts, resets, the stick clicks' Hold / Toggle
        /// choice, and the saved overrides (GameRoot loads them at boot).</summary>
        public readonly ControlBindings Bindings;

        readonly GameFlow _flow;

        public InputService(InputActionAsset source, GameFlow flow)
        {
            _flow = flow;
            Asset = source != null ? Object.Instantiate(source) : DefaultControls.Create();
            Asset.name = "JamKitControls (runtime)";
            Gameplay = Asset.FindActionMap("Gameplay", true);
            Ui = Asset.FindActionMap("UI", true);
            Move = Gameplay.FindAction("Move", true);
            // The d-pad is not a gameplay Move binding (the left stick steers; menus keep the d-pad on Navigate),
            // so the Gauntlet's ghost and the lab's HUD can use it. Older assets lose it here; a 2D template that
            // steers with it opts back in with Bindings.SetMoveDpad(true).
            for (int i = Move.bindings.Count - 1; i >= 0; i--)
            {
                var b = Move.bindings[i];
                if (!b.isComposite && !b.isPartOfComposite && string.Equals(b.path, ControlBindings.Dpad, System.StringComparison.OrdinalIgnoreCase))
                    Move.ChangeBinding(i).Erase();
            }
            Look = Gameplay.FindAction("Look", true);
            Fire = Gameplay.FindAction("Fire", true);
            AltFire = Gameplay.FindAction("AltFire", true);
            Focus = Gameplay.FindAction("Focus", true);
            // Actions added here (an older asset lacks them) get fixed binding ids, so saved rebinds of them
            // find them again on the next launch.
            // Sprint (Left Shift / LB) took over Focus's keys; Focus has none by default (bind one to use it).
            Sprint = Gameplay.FindAction("Sprint", false);
            if (Sprint == null)
            {
                Sprint = Gameplay.AddAction("Sprint", InputActionType.Button);
                AddFixed(Sprint, "<Keyboard>/leftShift", 0);
                AddFixed(Sprint, "<Gamepad>/leftShoulder", 1);
            }
            // Sprint as a toggle (L3, Titanfall's stick-click layout with R3 crouch): click to sprint until you stop
            // moving forward, crouch, slide or respawn (the movement core keeps the toggle).
            SprintToggle = Gameplay.FindAction("SprintToggle", false);
            if (SprintToggle == null)
            {
                SprintToggle = Gameplay.AddAction("SprintToggle", InputActionType.Button);
                AddFixed(SprintToggle, "<Gamepad>/leftStickPress", 0);
            }
            Dash = Gameplay.FindAction("Dash", true);
            // Crouch / slide (Left Ctrl, C, B / Circle) for first-person movement; added when an older asset lacks it.
            Crouch = Gameplay.FindAction("Crouch", false);
            if (Crouch == null)
            {
                Crouch = Gameplay.AddAction("Crouch", InputActionType.Button);
                AddFixed(Crouch, "<Keyboard>/leftCtrl", 0);
                AddFixed(Crouch, "<Keyboard>/c", 1);
                AddFixed(Crouch, "<Gamepad>/buttonEast", 2);
            }
            // Crouch / slide as a toggle (R3, like Titanfall's stick-click layouts): press to crouch or slide, press
            // again or jump to stand. Holding a stick click while steering with it is awkward, so it toggles
            // (Settings > Controls can make R3 and L3 hold instead).
            CrouchToggle = Gameplay.FindAction("CrouchToggle", false);
            if (CrouchToggle == null)
            {
                CrouchToggle = Gameplay.AddAction("CrouchToggle", InputActionType.Button);
                AddFixed(CrouchToggle, "<Gamepad>/rightStickPress", 0);
            }
            Interact = Gameplay.FindAction("Interact", true);
            Pause = Gameplay.FindAction("Pause", true);
            // The Gauntlet's instant restart and its ghost toggle (Movement Lab); added when an older asset lacks them.
            Restart = Gameplay.FindAction("Restart", false);
            if (Restart == null)
            {
                Restart = Gameplay.AddAction("Restart", InputActionType.Button);
                AddFixed(Restart, "<Keyboard>/r", 0);
                AddFixed(Restart, "<Gamepad>/select", 1);
            }
            Ghost = Gameplay.FindAction("Ghost", false);
            if (Ghost == null)
            {
                Ghost = Gameplay.AddAction("Ghost", InputActionType.Button);
                AddFixed(Ghost, "<Keyboard>/g", 0);
                AddFixed(Ghost, "<Gamepad>/dpad/left", 1);
            }
            PeekLeft = Ensure("PeekLeft", "<Keyboard>/q", "<Gamepad>/leftShoulder");
            PeekRight = Ensure("PeekRight", "<Keyboard>/f", "<Gamepad>/rightShoulder");
            ArgueAsk = Ensure("ArgueAsk", "<Keyboard>/1", "<Gamepad>/buttonWest");
            ArgueChallenge = Ensure("ArgueChallenge", "<Keyboard>/2", "<Gamepad>/rightShoulder");
            ArgueConcede = Ensure("ArgueConcede", "<Keyboard>/3", "<Gamepad>/buttonNorth");
            ArguePress = Ensure("ArguePress", "<Keyboard>/4", "<Gamepad>/buttonEast");
            ArgueRest = Ensure("ArgueRest", "<Keyboard>/5", "<Gamepad>/select");
            Navigate = Ui.FindAction("Navigate", true);
            Submit = Ui.FindAction("Submit", true);
            Cancel = Ui.FindAction("Cancel", true);
            Point = Ui.FindAction("Point", true);
            Click = Ui.FindAction("Click", true);
            ScrollWheel = Ui.FindAction("ScrollWheel", true);
            // Before Enable: it adds the empty slots and the stick-click bindings.
            Bindings = new ControlBindings(this);
            Asset.Enable();
        }

        /// <summary>The asset's action, or a new one with these bindings (fixed ids) when an older asset lacks it.</summary>
        InputAction Ensure(string name, string key, string pad)
        {
            var a = Gameplay.FindAction(name, false);
            if (a != null) return a;
            a = Gameplay.AddAction(name, InputActionType.Button);
            AddFixed(a, key, 0);
            AddFixed(a, pad, 1);
            return a;
        }

        static void AddFixed(InputAction action, string path, int n)
        {
            action.AddBinding(new InputBinding
            {
                path = path,
                id = DefaultControls.StableId("jamkit/" + action.actionMap.name + "/" + action.name + "/" + n),
            });
        }

        SyntheticInput _synthetic;

        /// <summary>Attract mode's hands on the pad: while set, Move, Look, <see cref="Pressed"/> and
        /// <see cref="Held"/> read it instead of the real devices (null = real input).</summary>
        public SyntheticInput Synthetic
        {
            get => _synthetic;
            set => _synthetic = value;
        }

        public bool IsSynthetic => _synthetic != null;

        /// <summary>True while the player is in control: Playing, no scene load running, and presenter
        /// mode isn't holding the game (its panel open, a freeze frame).</summary>
        public bool GameplayAllowed => _flow.Current == GameState.Playing && !GameRoot.IsLoadingScene && !PresenterMode.BlocksGameplay && !WorldBlocked;

        readonly System.Collections.Generic.List<object> _worldBlockers = new System.Collections.Generic.List<object>(2);

        /// <summary>Something modal is running in the world (an argument battle: ArgumentHost): world gameplay
        /// stops (GameplayAllowed is false) while any owner blocks it. A destroyed owner stops counting.</summary>
        public void BlockWorld(object owner, bool blocked)
        {
            if (owner == null) return;
            if (!blocked) { _worldBlockers.Remove(owner); return; }
            if (!_worldBlockers.Contains(owner)) _worldBlockers.Add(owner);
        }

        /// <summary>True while an owner blocks world gameplay (see <see cref="BlockWorld"/>).</summary>
        public bool WorldBlocked
        {
            get
            {
                for (int i = _worldBlockers.Count - 1; i >= 0; i--)
                    if (_worldBlockers[i] is Object o && o == null) _worldBlockers.RemoveAt(i);
                return _worldBlockers.Count > 0;
            }
        }

        /// <summary>Move as a vector (zero when gameplay isn't allowed).</summary>
        public Vector2 MoveVector
        {
            get
            {
                if (!GameplayAllowed) return Vector2.zero;
                return Vector2.ClampMagnitude(_synthetic != null ? _synthetic.move : Move.ReadValue<Vector2>(), 1f);
            }
        }

        /// <summary>Look with the player's sensitivity and invert-Y applied (zero when gameplay isn't allowed).
        /// Synthetic look comes in gamepad units (the stick x 8, see DefaultControls) with no sensitivity.</summary>
        public Vector2 LookDelta(Settings s)
        {
            if (!GameplayAllowed) return Vector2.zero;
            if (_synthetic != null) return Vector2.ClampMagnitude(_synthetic.look, 1f) * 8f;
            Vector2 d = Look.ReadValue<Vector2>() * (s != null ? s.mouseSensitivity : 1f);
            if (s != null && s.invertY) d.y = -d.y;
            return d;
        }

        /// <summary>True when the look input is stick-like (scale it by time, not per pixel): a gamepad,
        /// or attract mode's synthetic stick.</summary>
        public bool LookFromGamepad => _synthetic != null || (Look.activeControl != null && Look.activeControl.device is Gamepad);

        /// <summary>Pressed this frame, and gameplay is allowed.</summary>
        public bool Pressed(InputAction action)
        {
            if (!GameplayAllowed) return false;
            return _synthetic != null ? _synthetic.WasTapped(action) : action.WasPressedThisFrame();
        }

        /// <summary>Held down, and gameplay is allowed (use this rather than action.IsPressed() so attract
        /// mode can drive it).</summary>
        public bool Held(InputAction action)
        {
            if (!GameplayAllowed) return false;
            return _synthetic != null ? _synthetic.IsHeld(action) : action.IsPressed();
        }

        /// <summary>GameRoot calls this first thing each frame: synthetic taps become visible for one frame, and the
        /// last pointing device is tracked (<see cref="PadMode"/>).</summary>
        public void BeginFrame()
        {
            _synthetic?.BeginFrame();
            TrackPointerDevice();
        }

        // ---------- Pad mode: which device the player is using to point ----------

        bool _padMode;

        /// <summary>True from the moment any gamepad is used (a button, the d-pad, a stick or trigger pushed past
        /// half) until the mouse really moves, clicks or scrolls. While it's true the shell hides the cursor
        /// (<see cref="PointerCursor"/>) and mouse hover mustn't move focus: hover handlers check
        /// <see cref="HoverAllowed"/>. Keyboard input doesn't change it.</summary>
        public bool PadMode => _padMode;

        /// <summary>False in pad mode: the hidden cursor's hover (a list scrolling under it) mustn't take focus.</summary>
        public bool HoverAllowed => !_padMode;

        /// <summary>Puts pad mode on or off by hand (tests; a scene that knows better).</summary>
        public void SetPadMode(bool on) => _padMode = on;

        /// <summary>A mouse must travel this far in a frame (pixels) to count as "moved": a nudged desk isn't a choice.</summary>
        public const float MouseMovePixels = 2f;

        void TrackPointerDevice()
        {
            if (!_padMode) { if (AnyPadUsed()) _padMode = true; }
            else if (MouseUsed()) _padMode = false;
        }

        static bool AnyPadUsed()
        {
            var pads = Gamepad.all;
            for (int i = 0; i < pads.Count; i++)
            {
                var p = pads[i];
                if (p.buttonSouth.wasPressedThisFrame || p.buttonEast.wasPressedThisFrame || p.buttonWest.wasPressedThisFrame
                    || p.buttonNorth.wasPressedThisFrame || p.leftShoulder.wasPressedThisFrame || p.rightShoulder.wasPressedThisFrame
                    || p.startButton.wasPressedThisFrame || p.selectButton.wasPressedThisFrame
                    || p.leftStickButton.wasPressedThisFrame || p.rightStickButton.wasPressedThisFrame) return true;
                if (p.dpad.ReadValue().sqrMagnitude > 0.25f) return true;
                if (p.leftStick.ReadValue().sqrMagnitude > 0.25f || p.rightStick.ReadValue().sqrMagnitude > 0.25f) return true;
                if (p.leftTrigger.ReadValue() > 0.5f || p.rightTrigger.ReadValue() > 0.5f) return true;
            }
            return false;
        }

        static bool MouseUsed()
        {
            var m = Mouse.current;
            if (m == null) return false;
            if (m.leftButton.wasPressedThisFrame || m.rightButton.wasPressedThisFrame || m.middleButton.wasPressedThisFrame) return true;
            if (m.scroll.ReadValue().sqrMagnitude > 0f) return true;
            return m.delta.ReadValue().sqrMagnitude > MouseMovePixels * MouseMovePixels;
        }

        public void Dispose()
        {
            Bindings.UseSave(null);
            Asset.Disable();
            if (Application.isPlaying) Object.Destroy(Asset);
            else Object.DestroyImmediate(Asset); // EditMode tests build one too
        }
    }

    /// <summary>
    /// Input that attract mode (or a test) sets from code: a stick for Move, a stick for Look, and
    /// buttons held or tapped. <see cref="Tap"/> makes <see cref="InputService.Pressed"/> true for
    /// exactly one frame (the next one GameRoot starts); <see cref="Hold"/> also taps on the way down.
    /// No allocation per frame.
    /// </summary>
    public sealed class SyntheticInput
    {
        /// <summary>Move stick, -1..1 each axis.</summary>
        public Vector2 move;
        /// <summary>Look stick, -1..1 each axis (x right, y up).</summary>
        public Vector2 look;

        readonly System.Collections.Generic.List<InputAction> _held = new System.Collections.Generic.List<InputAction>(8);
        readonly System.Collections.Generic.List<InputAction> _queued = new System.Collections.Generic.List<InputAction>(8);
        readonly System.Collections.Generic.List<InputAction> _active = new System.Collections.Generic.List<InputAction>(8);

        public void Hold(InputAction action, bool down)
        {
            if (action == null) return;
            bool was = _held.Contains(action);
            if (down && !was)
            {
                _held.Add(action);
                Tap(action);
            }
            else if (!down && was) _held.Remove(action);
        }

        public void Tap(InputAction action)
        {
            if (action != null && !_queued.Contains(action)) _queued.Add(action);
        }

        public bool IsHeld(InputAction action) => _held.Contains(action) || _active.Contains(action);

        public bool WasTapped(InputAction action) => _active.Contains(action);

        public void BeginFrame()
        {
            _active.Clear();
            for (int i = 0; i < _queued.Count; i++) _active.Add(_queued[i]);
            _queued.Clear();
        }

        /// <summary>Hands off the pad: sticks centred, nothing held.</summary>
        public void Clear()
        {
            move = Vector2.zero;
            look = Vector2.zero;
            _held.Clear();
            _queued.Clear();
            _active.Clear();
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
            move.AddBinding("<Gamepad>/leftStick"); // no d-pad: see InputService (a 2D template can opt in)

            var look = g.AddAction("Look", InputActionType.Value, expectedControlLayout: "Vector2");
            look.AddBinding("<Mouse>/delta");
            look.AddBinding("<Gamepad>/rightStick").WithProcessor("scaleVector2(x=8,y=8)");

            Button(g, "Fire", "<Mouse>/leftButton", "<Keyboard>/j", "<Gamepad>/rightTrigger");
            Button(g, "AltFire", "<Mouse>/rightButton", "<Keyboard>/k", "<Gamepad>/leftTrigger");
            g.AddAction("Focus", InputActionType.Button); // no default keys (the bullet hell's optional Focus mode)
            Button(g, "Sprint", "<Keyboard>/leftShift", "<Gamepad>/leftShoulder");
            Button(g, "Dash", "<Keyboard>/space", "<Gamepad>/buttonSouth", "<Gamepad>/rightShoulder");
            Button(g, "Crouch", "<Keyboard>/leftCtrl", "<Keyboard>/c", "<Gamepad>/buttonEast");
            Button(g, "CrouchToggle", "<Gamepad>/rightStickPress");
            Button(g, "SprintToggle", "<Gamepad>/leftStickPress");
            Button(g, "Interact", "<Keyboard>/e", "<Gamepad>/buttonWest");
            Button(g, "Pause", "<Keyboard>/escape", "<Keyboard>/p", "<Gamepad>/start");
            Button(g, "Restart", "<Keyboard>/r", "<Gamepad>/select");
            Button(g, "Ghost", "<Keyboard>/g", "<Gamepad>/dpad/left");
            Button(g, "PeekLeft", "<Keyboard>/q", "<Gamepad>/leftShoulder");
            Button(g, "PeekRight", "<Keyboard>/f", "<Gamepad>/rightShoulder");
            Button(g, "ArgueAsk", "<Keyboard>/1", "<Gamepad>/buttonWest");
            Button(g, "ArgueChallenge", "<Keyboard>/2", "<Gamepad>/rightShoulder");
            Button(g, "ArgueConcede", "<Keyboard>/3", "<Gamepad>/buttonNorth");
            Button(g, "ArguePress", "<Keyboard>/4", "<Gamepad>/buttonEast");
            Button(g, "ArgueRest", "<Keyboard>/5", "<Gamepad>/select");

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
            FixIds(asset);
            return asset;
        }

        static void Button(InputActionMap map, string name, params string[] paths)
        {
            var a = map.AddAction(name, InputActionType.Button);
            foreach (var p in paths) a.AddBinding(p);
        }

        /// <summary>Binding ids from "map/action/index", the same every time (and the same as InputService's
        /// for the actions it adds to older assets), so saved rebinds (they're stored by binding id) still
        /// apply when the asset is rebuilt from these defaults.</summary>
        static void FixIds(InputActionAsset asset)
        {
            foreach (var map in asset.actionMaps)
                foreach (var action in map.actions)
                    for (int i = 0; i < action.bindings.Count; i++)
                    {
                        var b = action.bindings[i];
                        b.id = StableId("jamkit/" + map.name + "/" + action.name + "/" + i);
                        action.ChangeBinding(i).To(b);
                    }
        }

        /// <summary>A Guid that only depends on <paramref name="key"/> (two 64-bit FNV-1a hashes).</summary>
        internal static System.Guid StableId(string key)
        {
            ulong a = 14695981039346656037UL, b = 0x9E3779B97F4A7C15UL;
            for (int i = 0; i < key.Length; i++)
            {
                a = (a ^ key[i]) * 1099511628211UL;
                b = (b ^ key[i]) * 0x100000001B3UL;
                b ^= b >> 29;
            }
            var bytes = new byte[16];
            for (int i = 0; i < 8; i++)
            {
                bytes[i] = (byte)(a >> (8 * i));
                bytes[8 + i] = (byte)(b >> (8 * i));
            }
            return new System.Guid(bytes);
        }
    }
}
