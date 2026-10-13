using System;
using JamKit.Shell;
using UnityEngine;
using UnityEngine.InputSystem;
using NVec3 = System.Numerics.Vector3;
using NVec2 = System.Numerics.Vector2;

namespace JamKit.Movement
{
    /// <summary>
    /// Titanfall-style first-person movement on Unity's CharacterController (docs/MOVEMENT.md).
    /// The rules live in the pure core (<see cref="MoveSim"/>, run at a fixed 120 Hz by
    /// <see cref="MoveDriver"/>); this component is the world it moves in (CharacterController.Move
    /// plus sphere casts, <see cref="IMoveWorld"/>), reads the shell's input, turns the view per frame,
    /// and places the head at the interpolated position so the camera is smooth at any frame rate.
    /// Hierarchy: this body (yaw) > <see cref="head"/> (pitch, eye height) > rig (MovementCamera's
    /// bob, roll and dip) > camera. Controls: move WASD / left stick, look mouse / right stick, jump
    /// Space / A, crouch-slide Left Ctrl or C / B, sprint Shift / L3; wallrun and mantle are automatic. Peeking (hold
    /// Q / F or LB / RB, when the template turns it on): the eyes lean sideways round a corner, stopped short of walls
    /// (<see cref="PeekLean"/>).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [DefaultExecutionOrder(-40)]
    public sealed class MovementController : MonoBehaviour, IMoveWorld
    {
        public CharacterController controller;
        [Tooltip("Pitches up and down; sits at the eye height. The camera rig hangs under it.")]
        public Transform head;
        public Camera cam;
        [Tooltip("Shared numbers. Empty: the Def below is used.")]
        public MovementTuning tuning;
        public MoveDef def = new MoveDef();
        [Tooltip("Dream: everything. Daytime: the waking world's grounded movement (the tuning's daytime profile: no double jump, wallrun or slide, the air strafe down, jump fatigue).")]
        public MoveProfileKind profile = MoveProfileKind.Dream;

        [Header("Look")]
        public float mouseDegreesPerPixel = 0.09f;
        public float gamepadDegreesPerSecond = 190f;
        [Tooltip("Holding the stick fully over for a moment turns up to this much faster.")]
        public float gamepadTurnBoost = 1.6f;
        public float maxPitch = 85f;
        [Tooltip("Lock and hide the mouse while playing (not in batch mode, tests or attract mode).")]
        public bool managePointerLock = true;

        [Header("Peek")]
        [Tooltip("Hold Peek left / right (Q / F; LB / RB once the template turns peeking on) to lean the eyes round a corner, standing, crouched or walking slowly. A sprint, a jump or a slide lets it ease back.")]
        public bool allowPeek = true;
        public PeekLeanDef peek = new PeekLeanDef();

        [Header("World")]
        [Tooltip("Off: every wall is runnable unless a MovementSurface says no. On: only walls with a runnable MovementSurface.")]
        public bool onlyMarkedWalls;
        public LayerMask worldMask = ~(1 << 2);
        [Tooltip("The CharacterController's skin: it rests this far off what it touches.")]
        public float skinWidth = 0.04f;
        [Tooltip("Small steps the standing body walks up by itself.")]
        public float stepOffset = 0.35f;
        [Tooltip("The step offset while crouched or sliding. The controller's step-up pass lifts the capsule by the step " +
                 "offset before its forward sweep, so with 0.35 a slide stops dead at the face of a 1.15 m bar it fits under.")]
        public float crouchStepOffset = 0f;

        /// <summary>The step offset for a capsule of this height (crouched: <see cref="crouchStepOffset"/>).</summary>
        public float StepOffsetFor(float height) => height < Def.standHeight - 0.01f ? crouchStepOffset : stepOffset;

        MoveSim _sim;
        MoveDriver _driver;
        float _yaw, _pitch;
        float _eye;
        float _turnHeld;
        bool _crouchToggled; // R3: press to crouch or slide, press again or jump to stand
        Vector3[] _collect;
        int _collectCount;
        bool _collecting;
        readonly NVec3[] _hitsN = new NVec3[8];
        float _phase;
        bool _stepped;
        bool _focused;
        readonly PeekLean _lean = new PeekLean();
        int _leanWant;
        bool _leanAllowed, _airJumped;
        float _leanOffset, _airTime;
        /// <summary>A step off a kerb or down a stair is a moment in the air: the peek holds through this much of it (not a jump).</summary>
        const float LeanAirGrace = 0.12f;

        /// <summary>Drives the body instead of the player (smoke run, tests, attract mode). Called once a frame.</summary>
        public Func<MoveSim, MoveInput> Autopilot;
        /// <summary>Off: the body ignores input (cutscenes, menus); it still falls and lands.</summary>
        public bool InputEnabled = true;
        /// <summary>Opt-in (Skyline Repo, docs/SKYLINE-DESIGN.md 8): when set, it drives the body instead of the shell's input
        /// and the autopilot, with or without GameRoot (Skyline's scenes remove it). It gets the sim and the frame's dt and
        /// returns the step's input; that input's yaw becomes the view's. Null (the default) changes nothing. (The outside
        /// push is the sim's own: <see cref="MoveSim.OutsidePushEnabled"/> and <see cref="MoveSim.AddPush"/> on <see cref="Sim"/>.)</summary>
        public Func<MoveSim, float, MoveInput> InputOverride;

        public MoveSim Sim => _sim;
        public MoveDriver Driver => _driver;
        /// <summary>The tuning in use: this controller's, else the shared one (<see cref="MovementTuning.Shared"/>).</summary>
        public MovementTuning Tuning => tuning != null ? tuning : MovementTuning.Shared;
        public MoveDef Def => Tuning != null ? Tuning.def : def;
        public MoveProfileKind Profile => profile;

        /// <summary>Switches the movement profile (a daytime section, a dream). Takes effect at once.</summary>
        public void SetProfile(MoveProfileKind kind)
        {
            profile = kind;
            if (_sim != null) _sim.SetProfile(ProfileFor(kind));
        }

        /// <summary>A profile from outside for now (the stealth kit's lucidity fade); null goes back to <see cref="Profile"/>'s.</summary>
        public void OverrideProfile(MoveProfile p)
        {
            if (_sim != null) _sim.SetProfile(p ?? ProfileFor(profile));
        }

        MoveProfile ProfileFor(MoveProfileKind kind)
        {
            if (kind == MoveProfileKind.Dream) return null;
            var t = Tuning;
            return t != null && t.daytime != null ? t.daytime : MoveProfile.Daytime();
        }
        public MoveMode Mode => _sim != null ? _sim.Mode : MoveMode.Air;
        public Vector3 Velocity => _sim != null ? ToU(_sim.Velocity) : Vector3.zero;
        public float Speed => _sim != null ? _sim.HorizontalSpeed : 0f;
        public float Yaw => _yaw;
        public float Pitch => _pitch;
        /// <summary>The eye height now (blends between standing and sliding).</summary>
        public float EyeHeight => _eye;
        /// <summary>What happened in this frame's steps (jumps, landings, wallrun start/end...).</summary>
        public MoveEvents FrameEvents => _driver != null ? _driver.FrameEvents : default;
        /// <summary>Running cycle, in steps (each whole number is a footfall). Arms and head bob follow it.</summary>
        public float StridePhase => _phase;
        /// <summary>A foot landed this frame (on the ground or on a wall).</summary>
        public bool Stepped => _stepped;
        /// <summary>The peek: the eyes' sideways offset now (m, + right of the body), already stopped short of walls.</summary>
        public float LeanOffset => _leanOffset;
        /// <summary>The peek as a share of a full lean (-1 full left .. 1 full right).</summary>
        public float LeanFraction => peek != null && peek.distance > 1e-4f ? _leanOffset / peek.distance : 0f;
        /// <summary>The peek's offset in the world (horizontal): where the head is from the body's centre line. Stealth sight
        /// looks for her head there.</summary>
        public Vector3 LeanWorld => Quaternion.Euler(0f, _yaw, 0f) * new Vector3(_leanOffset, 0f, 0f);
        /// <summary>The peek's view roll toward the lean (degrees, + right), before the comfort settings.</summary>
        public float LeanRoll => peek != null ? _lean.Roll(peek) : 0f;
        public PeekLean Lean => _lean;
        /// <summary>Where the eyes are (the head, lean included).</summary>
        public Vector3 EyePosition => head != null ? head.position : RenderPosition + Vector3.up * _eye + LeanWorld;

        /// <summary>Where the camera sees the body this frame (interpolated between steps).</summary>
        public Vector3 RenderPosition => _driver != null ? ToU(_driver.InterpolatedPosition) : transform.position;

        static Vector3 ToU(NVec3 v) => new Vector3(v.X, v.Y, v.Z);
        static NVec3 ToN(Vector3 v) => new NVec3(v.x, v.y, v.z);

        void Awake()
        {
            if (controller == null) controller = GetComponent<CharacterController>();
            gameObject.layer = 2; // Ignore Raycast: our own casts skip the body
            Setup();
        }

        void Setup()
        {
            var d = Def;
            controller.radius = d.radius;
            controller.height = d.standHeight;
            controller.center = new Vector3(0f, d.standHeight * 0.5f, 0f);
            controller.stepOffset = StepOffsetFor(d.standHeight);
            controller.slopeLimit = d.slopeLimit;
            controller.skinWidth = skinWidth;
            controller.minMoveDistance = 0f;
            _sim = new MoveSim(this, d);
            _sim.SetProfile(ProfileFor(profile));
            _driver = new MoveDriver(_sim);
            _yaw = transform.eulerAngles.y;
            _eye = d.eyeHeight;
            _driver.Teleport(ToN(transform.position), _yaw, NVec3.Zero);
            ApplyView();
        }

        /// <summary>Puts the body somewhere, facing <paramref name="yaw"/>, moving at <paramref name="velocity"/>.</summary>
        public void Teleport(Vector3 feet, float yaw, Vector3 velocity, float pitch = 0f)
        {
            if (_sim == null) Setup();
            _crouchToggled = false;
            _lean.Reset();
            _leanOffset = 0f;
            controller.enabled = false;
            transform.position = feet;
            controller.enabled = true;
            _yaw = yaw;
            _pitch = pitch;
            _sim.def = Def;
            _driver.Teleport(ToN(feet), yaw, ToN(velocity));
            _eye = Def.eyeHeight;
            ApplyView();
        }

        public void SetLook(float yaw, float pitch)
        {
            _yaw = yaw;
            _pitch = Mathf.Clamp(pitch, -maxPitch, maxPitch);
            ApplyView();
        }

        void Update()
        {
            if (_sim == null) return;
            _sim.def = Def; // live tuning
            float dt = Time.deltaTime;
            var input = ReadInput(dt);
            _driver.Frame(dt, input);
            if (_sim.Mode == MoveMode.Air)
            {
                _airTime += dt;
                var ev = _driver.FrameEvents;
                if (ev.jumped || ev.doubleJumped || ev.wallJumped) _airJumped = true;
            }
            else { _airTime = 0f; _airJumped = false; }
            UpdateStride(dt);
            var d = Def;
            float rate = Mathf.Abs(d.eyeHeight - d.slideEyeHeight) / Mathf.Max(0.01f, d.eyeBlendTime);
            _eye = Mathf.MoveTowards(_eye, _sim.EyeHeightTarget, rate * dt);
            TickLean(dt);
            ApplyView();
        }

        // ---------- The peek ----------

        void TickLean(float dt)
        {
            if (peek == null) return;
            int side = _lean.ProbeSide(_leanWant);
            float room = side != 0 ? LeanRoom(side) : 0f;
            _lean.Tick(dt, _leanWant, _leanAllowed, side, room, peek);
            _leanOffset = _lean.Offset(peek);
        }

        /// <summary>Free metres beside the eyes toward <paramref name="side"/>: a sphere of the clearance's radius swept from
        /// the eyes on the body's centre line out to a full lean, so the eyes stop short of any wall.</summary>
        float LeanRoom(int side)
        {
            Vector3 eye = RenderPosition + Vector3.up * _eye;
            Vector3 dir = Quaternion.Euler(0f, _yaw, 0f) * new Vector3(side, 0f, 0f);
            float r = Mathf.Max(0.02f, peek.clearance);
            if (Physics.CheckSphere(eye, r, Mask, QueryTriggerInteraction.Ignore)) return 0f;
            return Physics.SphereCast(eye, r, dir, out var hit, peek.distance, Mask, QueryTriggerInteraction.Ignore) ? hit.distance : peek.distance;
        }

        MoveInput ReadInput(float dt)
        {
            _leanWant = 0;
            _leanAllowed = false;
            var input = new MoveInput { yaw = _yaw };
            if (InputOverride != null)
            {
                var o = InputOverride(_sim, dt);
                _yaw = o.yaw;
                return o;
            }
            bool shell = GameRoot.Exists;
            bool allowed = shell && GameRoot.Input.GameplayAllowed;
            if (Autopilot != null)
            {
                if (!allowed) return input;
                var a = Autopilot(_sim);
                _yaw = a.yaw; // exactly what the autopilot asks (the dotnet course tests drive it the same way)
                _pitch = Mathf.MoveTowards(_pitch, 4f, 60f * dt);
                return a;
            }
            if (managePointerLock) UpdatePointerLock(allowed && InputEnabled);
            if (!allowed || !InputEnabled) return input;

            var io = GameRoot.Input;
            Vector2 look = io.LookDelta(GameRoot.Settings);
            if (io.LookFromGamepad)
            {
                // The shell scales the stick by 8 (DefaultControls): back to -1..1, a response curve, and a
                // boost after holding it fully over so big turns don't take forever.
                Vector2 st = look / 8f;
                float full = Mathf.Abs(st.x) > 0.9f ? 1f : 0f;
                _turnHeld = full > 0f ? Mathf.Min(_turnHeld + dt, 0.6f) : 0f;
                float boost = 1f + (gamepadTurnBoost - 1f) * Mathf.Clamp01((_turnHeld - 0.25f) / 0.35f);
                float cx = Mathf.Sign(st.x) * Mathf.Pow(Mathf.Abs(st.x), 1.6f);
                float cy = Mathf.Sign(st.y) * Mathf.Pow(Mathf.Abs(st.y), 1.6f);
                _yaw += cx * gamepadDegreesPerSecond * boost * dt;
                _pitch -= cy * gamepadDegreesPerSecond * 0.7f * dt;
            }
            else if (_focused || !managePointerLock)
            {
                _yaw += look.x * mouseDegreesPerPixel;
                _pitch -= look.y * mouseDegreesPerPixel;
            }
            _yaw = Mathf.Repeat(_yaw, 360f);
            _pitch = Mathf.Clamp(_pitch, -maxPitch, maxPitch);

            Vector2 mv = io.MoveVector;
            input.move = new NVec2(mv.x, mv.y);
            input.yaw = _yaw;
            input.jumpHeld = io.Held(io.Dash);
            input.jumpPressed = io.Pressed(io.Dash);
            bool togglePressed = io.Pressed(io.CrouchToggle);
            if (togglePressed) _crouchToggled = !_crouchToggled;
            if (input.jumpPressed) _crouchToggled = false;
            input.crouchHeld = io.Held(io.Crouch) || _crouchToggled;
            input.crouchPressed = io.Pressed(io.Crouch) || (togglePressed && _crouchToggled);
            input.sprintHeld = io.Held(io.Sprint);
            input.sprintTogglePressed = io.Pressed(io.SprintToggle);

            // The peek: held left or right (both: neither); on the ground and not sprinting; a jump lets it go.
            if (allowPeek && peek != null)
            {
                _leanWant = (io.Held(io.PeekLeft) ? -1 : 0) + (io.Held(io.PeekRight) ? 1 : 0);
                bool sprinting = input.sprintHeld || input.sprintTogglePressed || _sim.SprintToggled || _sim.Mode == MoveMode.Sprint;
                // A step down (a moment in the air, not a jump) doesn't count as leaving the ground.
                var mode = _sim.Mode == MoveMode.Air && !_airJumped && _airTime < LeanAirGrace ? MoveMode.Grounded : _sim.Mode;
                _leanAllowed = PeekLean.Allowed(mode, sprinting) && !input.jumpPressed;
                // Leaning, she only walks slowly.
                if (_leanAllowed && (_leanWant != 0 || _lean.Leaning)) input.move = PeekLean.LimitMove(input.move, Def.stickFullAt, peek);
            }
            return input;
        }

        void UpdatePointerLock(bool want)
        {
            bool scripted = Application.isBatchMode || (GameRoot.Exists && GameRoot.Input.IsSynthetic);
            if (!want || scripted)
            {
                if (Cursor.lockState != CursorLockMode.None)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                _focused = scripted && want;
                return;
            }
            if (Cursor.lockState == CursorLockMode.Locked) { _focused = true; return; }
            var mouse = Mouse.current;
            bool click = mouse != null && mouse.leftButton.wasPressedThisFrame;
            if (Application.platform != RuntimePlatform.WebGLPlayer || click)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                _focused = true;
            }
            else _focused = false;
        }

        void OnDisable()
        {
            if (managePointerLock && Cursor.lockState != CursorLockMode.None)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        void UpdateStride(float dt)
        {
            _stepped = false;
            var mode = _sim.Mode;
            bool ground = mode == MoveMode.Grounded || mode == MoveMode.Sprint;
            bool wall = mode == MoveMode.Wallrun;
            if (!ground && !wall) return;
            float speed = _sim.HorizontalSpeed;
            if (speed < 0.5f) return;
            // Longer strides when faster; 1.5x the cadence on a wall.
            float stride = 1.0f + 0.1f * speed; // about 4.9 steps a second at sprint (RESEARCH-MOVEMENT 8.3)
            if (wall) stride /= 1.5f;
            float before = _phase;
            _phase += speed * dt / stride;
            if (Mathf.Floor(_phase) != Mathf.Floor(before)) _stepped = true;
        }

        void ApplyView()
        {
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            if (head == null) return;
            // Interpolated position: the body is at the last step; the head sits where the body is "now".
            Vector3 offset = RenderPosition - transform.position;
            head.localPosition = new Vector3(_leanOffset, _eye, 0f) + transform.InverseTransformVector(offset);
            head.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        // ---------- IMoveWorld ----------

        int Mask => worldMask.value & ~(1 << 2);

        NVec3 IMoveWorld.Move(NVec3 feet, NVec3 delta, float radius, float height, NVec3[] normals, out int hitCount)
        {
            if (!Mathf.Approximately(controller.height, height) || !Mathf.Approximately(controller.radius, radius))
            {
                controller.radius = radius;
                controller.height = height;
                controller.center = new Vector3(0f, height * 0.5f, 0f);
                controller.stepOffset = Mathf.Min(StepOffsetFor(height), height);
            }
            Vector3 pos = ToU(feet);
            if ((transform.position - pos).sqrMagnitude > 1e-8f)
            {
                controller.enabled = false;
                transform.position = pos;
                controller.enabled = true;
            }
            _collecting = true;
            _collectCount = 0;
            if (controller.enabled) controller.Move(ToU(delta));
            _collecting = false;
            hitCount = Mathf.Min(_collectCount, normals.Length);
            for (int i = 0; i < hitCount; i++) normals[i] = _hitsN[i];
            return ToN(transform.position);
        }

        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (!_collecting) return;
            var n = ToN(hit.normal);
            for (int i = 0; i < _collectCount; i++)
                if (NVec3.Dot(_hitsN[i], n) > 0.99f) return;
            if (_collectCount < _hitsN.Length) _hitsN[_collectCount++] = n;
        }

        bool Runnable(Collider c)
        {
            if (c == null) return false;
            if (c.TryGetComponent<MovementSurface>(out var s)) return s.runnable;
            return !onlyMarkedWalls;
        }

        static SurfaceHit From(RaycastHit h, float distance, bool runnable) => new SurfaceHit
        {
            point = ToN(h.point),
            normal = ToN(h.normal),
            distance = distance,
            id = h.colliderInstanceID,
            runnable = runnable,
        };

        bool IMoveWorld.CastDown(NVec3 feet, float radius, float maxDistance, out SurfaceHit hit)
        {
            const float lift = 0.1f;
            Vector3 origin = ToU(feet) + Vector3.up * (radius + lift);
            if (Physics.SphereCast(origin, radius, Vector3.down, out var h, maxDistance + lift, Mask, QueryTriggerInteraction.Ignore))
            {
                hit = From(h, Mathf.Max(0f, h.distance - lift), Runnable(h.collider));
                return true;
            }
            hit = default;
            return false;
        }

        bool IMoveWorld.CastWall(NVec3 origin, NVec3 direction, float radius, float maxDistance, out SurfaceHit hit)
        {
            Vector3 o = ToU(origin);
            if (Physics.SphereCast(o, radius, ToU(direction), out var h, maxDistance, Mask, QueryTriggerInteraction.Ignore))
            {
                hit = From(h, h.distance, Runnable(h.collider));
                // A sphere touching a box's edge reports a rounded normal (halfway between two faces). The rules
                // need the face's own normal (a wall's tangent from a corner normal would point 45 degrees off),
                // so ask a ray aimed at the contact point, level with the probe.
                Vector3 to = h.point - o;
                to.y = 0f;
                float len = to.magnitude;
                if (len > 1e-3f && Physics.Raycast(o, to / len, out var face, len + 0.3f, Mask, QueryTriggerInteraction.Ignore) && face.collider == h.collider)
                    hit.normal = ToN(face.normal);
                else if (Mathf.Abs(h.normal.y) < 0.5f)
                {
                    // The ray missed the edge: snap the rounded normal to the box's nearest face direction.
                    var box = h.collider as BoxCollider;
                    if (box != null) hit.normal = ToN(NearestFaceNormal(box.transform, h.normal));
                }
                return true;
            }
            hit = default;
            return false;
        }

        static Vector3 NearestFaceNormal(Transform t, Vector3 n)
        {
            Vector3 best = n;
            float bestDot = -2f;
            for (int i = 0; i < 6; i++)
            {
                Vector3 a = i < 2 ? t.right : i < 4 ? t.forward : t.up;
                if ((i & 1) == 1) a = -a;
                float d = Vector3.Dot(a, n);
                if (d > bestDot) { bestDot = d; best = a; }
            }
            return best;
        }

        bool IMoveWorld.CastLedge(NVec3 origin, float maxDistance, out SurfaceHit hit)
        {
            if (Physics.Raycast(ToU(origin), Vector3.down, out var h, maxDistance, Mask, QueryTriggerInteraction.Ignore))
            {
                hit = From(h, h.distance, Runnable(h.collider));
                return true;
            }
            hit = default;
            return false;
        }

        bool IMoveWorld.CapsuleFits(NVec3 feet, float radius, float height)
        {
            Vector3 f = ToU(feet);
            float r = radius * 0.98f;
            return !Physics.CheckCapsule(f + Vector3.up * (r + 0.02f), f + Vector3.up * Mathf.Max(r + 0.03f, height - r), r, Mask, QueryTriggerInteraction.Ignore);
        }
    }
}
