using JamKit.Shell;
using UnityEngine;
using UnityEngine.Rendering;
using NVec3 = System.Numerics.Vector3;

namespace JamKit.Movement
{
    /// <summary>
    /// The first-person body and its feel (docs/MOVEMENT.md "The body"; evidence in RESEARCH-MOVEMENT 8.7).
    /// Each frame the core <see cref="BodyFeel"/> turns the movement (mode, speed, events, the stride
    /// phase the footsteps use, look turns, the camera's dip) into a pose: the pump in step with the
    /// footfalls, the wall hand reaching out before a wallrun (a look-ahead probe on her path) and planted
    /// on the contact frame, the one-handed wall pose, the wall kick's push, the slide's tilt (already in the
    /// air with crouch held), the hands on a ledge's lip, the look sway and strafe roll, vertical inertia,
    /// the double jump's held pose, the landing a frame after the camera, the legs. A real body draws it through <see cref="rig"/> (a
    /// <see cref="MovementBodyRig"/>, e.g. <see cref="MixamoBodyRig"/> on a template's character); without
    /// one, PLACEHOLDER capsule arms (and legs while sliding) do. Numbers: the tuning asset's body section.
    /// Never moves the camera: the camera is the source and the body follows it. F4 (editor and
    /// development builds) shows the body from an orbit camera. Put it on the camera.
    /// </summary>
    [DefaultExecutionOrder(60)]
    public sealed class MovementBody : MonoBehaviour
    {
        public MovementController body;
        [Tooltip("A real body (a MovementBodyRig on the character model). Empty or not ready: the placeholder capsules.")]
        public MovementBodyRig rig;
        [Tooltip("The camera feel (for the landing dip the body follows a moment later). Found in the parents when empty.")]
        public MovementCamera feelCamera;
        [Tooltip("Materials for the placeholder (made at runtime from the colours below when empty).")]
        public Material sleeveMaterial, skinMaterial, legMaterial, shoeMaterial;
        public Color sleeveColor = new Color(0.62f, 0.58f, 0.84f);
        public Color skinColor = new Color(0.96f, 0.79f, 0.68f);
        public Color legColor = new Color(0.36f, 0.56f, 0.62f);
        public Color shoeColor = new Color(0.95f, 0.92f, 0.84f);
        public bool showArms = true;
        public bool showLegsWhenSliding = true;
        [Tooltip("F4 shows the body from an orbit camera (editor and development builds).")]
        public bool debugOrbit = true;

        [Header("Placeholder proportions (metres, camera space)")]
        public float upperArm = 0.27f;
        public float forearm = 0.27f;
        public float armThickness = 0.085f;

        static Mesh _capsule, _sphere;
        static readonly BodyFeelDef DefaultFeel = new BodyFeelDef();

        Transform _root, _legRoot;
        readonly Transform[] _upper = new Transform[2], _fore = new Transform[2], _hand = new Transform[2];
        readonly Transform[] _thigh = new Transform[2], _shin = new Transform[2], _shoe = new Transform[2];
        bool _built, _haveLook;
        float _lastYaw, _lastPitch;
        Camera _cam;
        BodyFeel _feel;
        MovementDebugOrbit _orbit;
        bool _handOnWall;
        float _wallGap;

        /// <summary>How far short of the wall the planted hand is because her arm can't reach it yet (m; 0 = touching).</summary>
        public float WallGap => _wallGap;

        /// <summary>
        /// Called each frame between the feel's pose and drawing it (the rig or the capsules): a gameplay component may
        /// adjust the pose here (hands in camera space, legs in body space). Leave hands with <c>contact</c> set alone.
        /// </summary>
        public event System.Action<BodyFeelPose> PoseReady;

        Vector3 _gestureWorld;

        /// <summary>
        /// Lifts a hand (0 left, 1 right) towards a world point for <paramref name="duration"/> seconds: a quick raise, then
        /// a blend back (the dream tool's pulse). A contact cue on that hand (the wall hand, the ledge hands) wins.
        /// </summary>
        public void Gesture(int hand, Vector3 worldTarget, float duration = 0.2f)
        {
            if (!_built) Build();
            _gestureWorld = worldTarget;
            _feel.Gesture(hand, duration);
        }

        /// <summary>The hand on the wall's side is on the wall (tests and the HUD read it).</summary>
        public bool HandOnWall => _handOnWall;
        /// <summary>This frame's pose (null before the first frame).</summary>
        public BodyFeelPose Pose => _feel != null ? _feel.pose : null;
        public BodyFeel Feel => _feel;
        /// <summary>True while the real body draws (the capsules are hidden).</summary>
        public bool UsingRig => rig != null && rig.isActiveAndEnabled && rig.Ready;
        /// <summary>Where the pose puts a hand (world; 0 left, 1 right).</summary>
        public Vector3 HandTarget(int side) => _feel != null ? transform.TransformPoint(U(_feel.pose.hand[side])) : transform.position;
        public bool ThirdPerson => _orbit != null && _orbit.Active;

        static Vector3 U(NVec3 v) => new Vector3(v.X, v.Y, v.Z);
        static NVec3 N(Vector3 v) => new NVec3(v.x, v.y, v.z);

        void Start() => Build();

        void Build()
        {
            if (_built) return;
            _built = true;
            _feel = new BodyFeel(DefaultFeel);
            if (feelCamera == null) feelCamera = GetComponentInParent<MovementCamera>();
            if (_capsule == null || _sphere == null)
            {
                var c = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                _capsule = c.GetComponent<MeshFilter>().sharedMesh;
                DestroyImmediate(c);
                var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                _sphere = s.GetComponent<MeshFilter>().sharedMesh;
                DestroyImmediate(s);
            }
            if (sleeveMaterial == null) sleeveMaterial = MakeMaterial(sleeveColor);
            if (skinMaterial == null) skinMaterial = MakeMaterial(skinColor);
            if (legMaterial == null) legMaterial = MakeMaterial(legColor);
            if (shoeMaterial == null) shoeMaterial = MakeMaterial(shoeColor);

            _root = new GameObject("PLACEHOLDER Arms (when no body rig is wired)").transform;
            _root.SetParent(transform, false);
            for (int i = 0; i < 2; i++)
            {
                _upper[i] = Piece(_root, "Upper arm", _capsule, sleeveMaterial);
                _fore[i] = Piece(_root, "Forearm", _capsule, sleeveMaterial);
                _hand[i] = Piece(_root, "Hand", _sphere, skinMaterial);
            }
            var bodyT = body != null ? body.transform : transform.root;
            _legRoot = new GameObject("PLACEHOLDER Legs (when no body rig is wired)").transform;
            _legRoot.SetParent(bodyT, false);
            for (int i = 0; i < 2; i++)
            {
                _thigh[i] = Piece(_legRoot, "Thigh", _capsule, legMaterial);
                _shin[i] = Piece(_legRoot, "Shin", _capsule, legMaterial);
                _shoe[i] = Piece(_legRoot, "Shoe", _sphere, shoeMaterial);
            }
            _legRoot.gameObject.SetActive(false);
            if (debugOrbit && (Application.isEditor || Debug.isDebugBuild))
            {
                _orbit = gameObject.AddComponent<MovementDebugOrbit>();
                _orbit.body = body;
                _orbit.firstPerson = GetComponent<Camera>();
            }
        }

        static Material MakeMaterial(Color c)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var m = new Material(shader) { name = "Placeholder body", color = c };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.25f);
            return m;
        }

        static Transform Piece(Transform parent, string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            go.layer = 2;
            return go.transform;
        }

        void LateUpdate()
        {
            if (!_built || body == null || body.Sim == null) return;
            var def = body.tuning != null && body.tuning.body != null ? body.tuning.body : DefaultFeel;
            _feel.def = def;
            float dt = Time.deltaTime;
            var sim = body.Sim;
            var mode = sim.Mode;
            var s = GameRoot.Exists ? GameRoot.Settings : null;

            // Look turns this frame (the arms trail them).
            float dy = 0f, dp = 0f;
            if (_haveLook)
            {
                dy = Mathf.DeltaAngle(_lastYaw, body.Yaw);
                dp = Mathf.DeltaAngle(_lastPitch, body.Pitch);
            }
            _haveLook = true;
            _lastYaw = body.Yaw;
            _lastPitch = body.Pitch;

            var input = new BodyFeelInput
            {
                dt = dt,
                mode = mode,
                speed = sim.HorizontalSpeed,
                sprintSpeed = body.Def.sprintSpeed,
                velocityBody = N(body.transform.InverseTransformDirection(body.Velocity)),
                lookYaw = dy,
                lookPitch = dp,
                events = body.FrameEvents,
                stridePhase = body.StridePhase,
                crouchHeld = sim.CrouchHeld,
                ledgeHeight = sim.MantleRise,
                wallSide = sim.WallSide,
                motion = s != null ? s.motionEffects : 1f,
                cameraDip = feelCamera != null ? feelCamera.Dip : 0f,
                gestureTarget = N(transform.InverseTransformPoint(_gestureWorld)),
            };

            _wallGap = 0f;
            // The wall hand's spot: the wall point beside the shoulder, a little ahead along the run.
            if (sim.WallSide != 0)
            {
                Vector3 n = new Vector3(sim.WallNormal.X, 0f, sim.WallNormal.Z).normalized;
                if (Physics.Raycast(transform.position, -n, out var hit, 1.5f, ~(1 << 2), QueryTriggerInteraction.Ignore))
                {
                    Vector3 t = new Vector3(sim.WallTangent.X, 0f, sim.WallTangent.Z);
                    if (Vector3.Dot(t, body.Velocity) < 0f) t = -t;
                    t = t.sqrMagnitude > 1e-6f ? t.normalized : body.transform.forward;
                    Vector3 p0 = hit.point + n * 0.03f;
                    p0.y = transform.TransformPoint(new Vector3(0f, def.wallHandHeight, 0f)).y;
                    int arm = sim.WallSide > 0 ? 1 : 0;
                    Vector3 spot = p0 + t * ReachableAhead(arm, p0, t, def.wallHandAhead, Vector3.zero);
                    spot = WithinReach(arm, spot, Vector3.zero);
                    Vector3 local = transform.InverseTransformPoint(spot);
                    input.hasWallTarget = true;
                    input.wallHandTarget = N(local);
                }
            }
            // The lean-in: a runnable wall coming up beside her within wallLookAhead seconds.
            if (mode != MoveMode.Wallrun && mode != MoveMode.Mantle && mode != MoveMode.Slide) ProbeWall(def, ref input);
            // The ledge's lip: both hands on it, either side of the middle.
            if (mode == MoveMode.Mantle)
            {
                Vector3 lip = new Vector3(sim.MantleLip.X, sim.MantleLip.Y, sim.MantleLip.Z);
                Vector3 n = new Vector3(sim.MantleNormal.X, 0f, sim.MantleNormal.Z);
                if (n.sqrMagnitude > 1e-6f)
                {
                    n.Normalize();
                    Vector3 along = Vector3.Cross(Vector3.up, n);
                    Vector3 a = transform.InverseTransformPoint(lip - n * 0.06f + along * def.mantleHandSpread + Vector3.up * 0.02f);
                    Vector3 b = transform.InverseTransformPoint(lip - n * 0.06f - along * def.mantleHandSpread + Vector3.up * 0.02f);
                    input.hasLedge = true;
                    input.ledgeLeft = N(a.x < b.x ? a : b);
                    input.ledgeRight = N(a.x < b.x ? b : a);
                }
            }

            var pose = _feel.Tick(input);
            PoseReady?.Invoke(pose);
            int wallIdx = sim.WallSide > 0 ? 1 : 0;
            _handOnWall = mode == MoveMode.Wallrun && sim.WallSide != 0 && pose.contact[wallIdx];

            if (UsingRig)
            {
                if (_root.gameObject.activeSelf) _root.gameObject.SetActive(false);
                if (_legRoot.gameObject.activeSelf) _legRoot.gameObject.SetActive(false);
                var head = body.head != null ? body.head : transform;
                rig.Apply(pose, new MovementBodyFrame
                {
                    camera = transform,
                    bodyRotation = Quaternion.Euler(0f, body.Yaw, 0f),
                    feet = body.RenderPosition,
                    eyes = head.position,
                    pitch = body.Pitch,
                    thirdPerson = ThirdPerson,
                    lean = body.peek != null ? body.LeanFraction * body.peek.bodyBend : 0f,
                });
                return;
            }
            PosePlaceholder(pose);
        }

        /// <summary>
        /// The lean-in's look-ahead: where she'll be over the next wallLookAhead seconds (velocity, plus gravity in the
        /// air), probed the way the sim catches a wall (MoveSim.TryAttachWall: sideways and diagonal sphere casts from
        /// wallProbeHeight, out to radius + wallProbeReach): the first moment a runnable wall beside her comes within
        /// catch reach is the predicted contact. The hand's spot there is given relative to where her eyes will be,
        /// kept within her arm's reach.
        /// </summary>
        void ProbeWall(BodyFeelDef def, ref BodyFeelInput input)
        {
            Vector3 v = body.Velocity;
            Vector3 flat = new Vector3(v.x, 0f, v.z);
            float speed = flat.magnitude;
            var md = body.Def;
            if (speed < Mathf.Max(2f, md.wallMinSpeed * 0.8f) || def.wallLookAhead <= 0f) return;
            bool air = body.Sim.Mode == MoveMode.Air;
            Vector3 d = flat / speed;
            Vector3 r = new Vector3(d.z, 0f, -d.x); // the sim's RightOf
            float reach = md.radius + md.wallProbeReach;
            int mask = body.worldMask.value & ~(1 << 2);
            Vector3 feet = body.RenderPosition;
            const int steps = 6;
            for (int n = 0; n <= steps; n++)
            {
                float t = def.wallLookAhead * n / steps;
                Vector3 travel = v * t + (air ? Vector3.down * (0.5f * md.gravity * t * t) : Vector3.zero);
                Vector3 origin = feet + travel + Vector3.up * md.wallProbeHeight;
                for (int k = 0; k < 4; k++)
                {
                    Vector3 dir = k == 0 ? r : k == 1 ? -r : k == 2 ? (d + r).normalized : (d - r).normalized;
                    if (!Physics.SphereCast(origin, md.wallProbeRadius, dir, out var hit, reach, mask, QueryTriggerInteraction.Ignore)) continue;
                    Vector3 nrm = new Vector3(hit.normal.x, 0f, hit.normal.z);
                    if (Mathf.Abs(hit.normal.y) > md.wallMaxNormalY || nrm.sqrMagnitude < 0.25f) continue;
                    nrm.Normalize();
                    Vector3 along = Vector3.Cross(Vector3.up, nrm).normalized;
                    if (Vector3.Dot(along, d) < 0f) along = -along;
                    if (Vector3.Dot(d, along) < md.wallMinAlongDot) continue; // head on: not a wallrun
                    var surface = hit.collider != null ? hit.collider.GetComponent<MovementSurface>() : null;
                    bool runnable = surface != null ? surface.runnable : !body.onlyMarkedWalls;
                    if (!runnable) continue;
                    // The wall point beside her at that moment, level with the planted hand, then ahead along the run.
                    Vector3 eyeThen = transform.position + travel;
                    Vector3 p0 = hit.point + nrm * 0.03f;
                    p0.y = eyeThen.y + (transform.rotation * new Vector3(0f, def.wallHandHeight, 0f)).y;
                    int arm = Vector3.Dot(-nrm, body.transform.right) > 0f ? 1 : 0;
                    Vector3 spot = p0 + along * ReachableAhead(arm, p0, along, def.wallHandAhead, travel);
                    spot = WithinReach(arm, spot, travel);
                    input.approaching = true;
                    input.approachSide = arm == 1 ? 1 : -1;
                    input.approachCloseness = Mathf.Clamp01(1f - t / def.wallLookAhead);
                    input.approachTarget = N(Quaternion.Inverse(transform.rotation) * (spot - eyeThen));
                    return;
                }
            }
        }

        /// <summary>Her arm's root and reach (the rig's, or the placeholder's).</summary>
        void Arm(int side, out Vector3 shoulder, out float reach)
        {
            if (UsingRig && rig.ArmReach(side, out shoulder, out reach)) return;
            float k = _root != null ? _root.localScale.x : 1f;
            var def = _feel.def;
            shoulder = transform.TransformPoint(new Vector3((side == 0 ? -1f : 1f) * def.shoulderWidth, -def.shoulderDrop, 0.02f) * k);
            reach = (upperArm + forearm) * k;
        }

        /// <summary>A hand spot moved, if needed, to the edge of what her arm can reach (the catch can come from up to
        /// about a metre off the wall: the arm then stretches towards the wall until she's close enough to touch it).</summary>
        Vector3 WithinReach(int side, Vector3 spot, Vector3 shift)
        {
            Arm(side, out var shoulder, out var reach);
            shoulder += shift;
            reach *= 0.97f;
            Vector3 to = spot - shoulder;
            float len = to.magnitude;
            _wallGap = Mathf.Max(0f, len - reach);
            return len > reach && len > 1e-5f ? shoulder + to * (reach / len) : spot;
        }

        /// <summary>
        /// How far along the wall (from <paramref name="p0"/>, the wall point beside her, along <paramref name="along"/>) the
        /// hand can plant: <paramref name="want"/> (0.35 m in TF2's spot), or less when her arm can't reach that far
        /// (the hand must really touch the wall; a hand floating short of it would break the cue).
        /// <paramref name="shift"/> moves the shoulder to where it will be (the look-ahead).
        /// </summary>
        float ReachableAhead(int side, Vector3 p0, Vector3 along, float want, Vector3 shift)
        {
            Arm(side, out var shoulder, out var reach);
            shoulder += shift;
            reach *= 0.97f;
            float s0 = Vector3.Dot(shoulder - p0, along);
            float perp2 = (shoulder - (p0 + along * s0)).sqrMagnitude;
            if (perp2 >= reach * reach) return Mathf.Clamp(s0, 0f, want);
            return Mathf.Clamp(s0 + Mathf.Sqrt(reach * reach - perp2), 0f, want);
        }

        // ---------- The placeholder ----------

        void PosePlaceholder(BodyFeelPose pose)
        {
            _root.gameObject.SetActive(showArms);
            // Keep the arms the same size on screen when the speed FOV widens (no overlay camera needed).
            if (_cam == null) _cam = GetComponent<Camera>();
            if (_cam != null)
            {
                float baseFov = GameRoot.Exists ? GameRoot.Settings.fieldOfView : 68f;
                float k = Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / Mathf.Tan(baseFov * 0.5f * Mathf.Deg2Rad);
                _root.localScale = Vector3.one * Mathf.Clamp(k, 0.8f, 1.4f);
            }
            var def = _feel.def;
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                Vector3 s = new Vector3(side * def.shoulderWidth, -def.shoulderDrop, 0.02f);
                Vector3 h = U(pose.hand[i]);
                Vector3 to = h - s;
                float a = upperArm, b = forearm;
                float d = Mathf.Clamp(to.magnitude, 0.05f, a + b - 0.001f);
                Vector3 dir = to.normalized;
                h = s + dir * d;
                Vector3 pole = U(pose.elbowPole[i]);
                pole = (pole - dir * Vector3.Dot(pole, dir)).normalized;
                float along = (a * a - b * b + d * d) / (2f * d);
                float up = Mathf.Sqrt(Mathf.Max(0f, a * a - along * along));
                Vector3 e = s + dir * along + pole * up;
                Bone(_upper[i], s, e, armThickness);
                Bone(_fore[i], e, h, armThickness * 0.88f);
                var hand = _hand[i];
                hand.localPosition = h + (h - e).normalized * 0.05f;
                if (pose.contact[i])
                {
                    hand.localRotation = Quaternion.LookRotation(Vector3.forward, new Vector3(-side, 0.3f, 0f));
                    hand.localScale = new Vector3(0.11f, 0.035f, 0.13f); // a flat palm against the wall
                }
                else
                {
                    hand.localRotation = Quaternion.LookRotation(h - e, Vector3.up);
                    hand.localScale = new Vector3(0.085f, 0.07f, 0.11f);
                }
            }

            // Legs: only for the slide (and the double jump's tuck), the feet rising into the bottom of the screen.
            bool shown = showLegsWhenSliding && pose.legs > 0.01f;
            if (_legRoot.gameObject.activeSelf != shown) _legRoot.gameObject.SetActive(shown);
            if (!shown) return;
            var bt = body.transform;
            _legRoot.localPosition = bt.InverseTransformVector(body.RenderPosition - bt.position);
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                Vector3 hip = new Vector3(side * 0.13f, 0.42f, -0.05f);
                Vector3 knee = U(_feel.SlideKnee(i));
                Vector3 foot = U(pose.foot[i]);
                Bone(_thigh[i], hip, knee, 0.15f);
                Bone(_shin[i], knee, foot, 0.12f);
                _shoe[i].localPosition = foot + new Vector3(0f, 0.02f, 0.06f);
                _shoe[i].localRotation = Quaternion.Euler(-25f, 0f, 0f);
                _shoe[i].localScale = new Vector3(0.12f, 0.1f, 0.24f);
            }
        }

        static void Bone(Transform t, Vector3 from, Vector3 to, float thickness)
        {
            Vector3 d = to - from;
            float len = d.magnitude;
            t.localPosition = (from + to) * 0.5f;
            t.localRotation = len > 1e-4f ? Quaternion.FromToRotation(Vector3.up, d / len) : Quaternion.identity;
            t.localScale = new Vector3(thickness, Mathf.Max(thickness * 0.5f, len * 0.5f + thickness * 0.25f), thickness);
        }
    }

    /// <summary>
    /// F4 (editor and development builds): shows the first-person body from outside, on a camera orbiting
    /// the player (Q / E turn it, the mouse wheel moves it in and out), so its poses can be judged. The
    /// first-person camera keeps running underneath (the feel is unchanged).
    /// </summary>
    public sealed class MovementDebugOrbit : MonoBehaviour
    {
        public MovementController body;
        public Camera firstPerson;
        public float distance = 2.6f, height = 1.1f, yaw = 150f;

        Camera _cam;
        bool _active;

        public bool Active => _active;

        public void SetActive(bool on)
        {
            _active = on;
            if (on && _cam == null)
            {
                var go = new GameObject("Debug orbit camera (F4)");
                _cam = go.AddComponent<Camera>();
                if (firstPerson != null)
                {
                    _cam.CopyFrom(firstPerson);
                    _cam.depth = firstPerson.depth + 1f;
                }
                _cam.fieldOfView = 55f;
                _cam.nearClipPlane = 0.05f;
            }
            if (_cam != null) _cam.enabled = on;
        }

        void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;
            if (kb.f4Key.wasPressedThisFrame) SetActive(!_active);
            if (!_active) return;
            float dt = Time.unscaledDeltaTime;
            if (kb.qKey.isPressed) yaw -= 90f * dt;
            if (kb.eKey.isPressed) yaw += 90f * dt;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null) distance = Mathf.Clamp(distance - mouse.scroll.ReadValue().y * 0.002f, 1f, 8f);
        }

        void LateUpdate()
        {
            if (!_active || _cam == null || body == null) return;
            Vector3 centre = body.RenderPosition + Vector3.up * height;
            Quaternion r = Quaternion.Euler(12f, body.Yaw + yaw, 0f);
            _cam.transform.SetPositionAndRotation(centre - r * Vector3.forward * distance, r);
        }

        void OnDestroy()
        {
            if (_cam != null) Destroy(_cam.gameObject);
        }
    }
}
