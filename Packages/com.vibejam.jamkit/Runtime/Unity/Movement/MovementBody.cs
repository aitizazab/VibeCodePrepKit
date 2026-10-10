using UnityEngine;
using UnityEngine.Rendering;

namespace JamKit.Movement
{
    /// <summary>
    /// PLACEHOLDER first-person body, to be replaced by the art style's arms and legs: smooth capsule
    /// arms with round hands, and legs that show while sliding, in a soft palette. Animated by code
    /// from the movement state (docs/RESEARCH-MOVEMENT.md 8.1): the arms pump with the stride while
    /// running, lift on a jump and sweep back on a double jump, the hand on the wall's side reaches
    /// out and plants on the wall the moment a wallrun starts (the cue players time wall kicks by),
    /// both hands drop and spread on a slide with the legs out in front, and plant forward on a
    /// mantle. Put it on the camera; it builds its pieces at start (no colliders, no shadows).
    /// </summary>
    [DefaultExecutionOrder(60)]
    public sealed class MovementBody : MonoBehaviour
    {
        public MovementController body;
        [Tooltip("Materials for the placeholder (made at runtime from the colours below when empty).")]
        public Material sleeveMaterial, skinMaterial, legMaterial, shoeMaterial;
        public Color sleeveColor = new Color(0.62f, 0.58f, 0.84f);
        public Color skinColor = new Color(0.96f, 0.79f, 0.68f);
        public Color legColor = new Color(0.36f, 0.56f, 0.62f);
        public Color shoeColor = new Color(0.95f, 0.92f, 0.84f);
        public bool showArms = true;
        public bool showLegsWhenSliding = true;

        [Header("Proportions (metres, camera space)")]
        public float shoulderWidth = 0.24f;
        public float shoulderDrop = 0.30f;
        public float upperArm = 0.27f;
        public float forearm = 0.27f;
        public float armThickness = 0.085f;

        static Mesh _capsule, _sphere;

        Transform _root, _legRoot;
        readonly Transform[] _upper = new Transform[2], _fore = new Transform[2], _hand = new Transform[2];
        readonly Transform[] _thigh = new Transform[2], _shin = new Transform[2], _shoe = new Transform[2];
        readonly Vector3[] _handPos = new Vector3[2], _handVel = new Vector3[2];
        float _jumpLift, _sweep, _legs, _plant;
        bool _built;
        Camera _cam;

        /// <summary>The hand on the wall's side is on the wall (tests and the HUD read it).</summary>
        public bool HandOnWall => _handOnWall;
        bool _handOnWall;

        void Start() => Build();

        void Build()
        {
            if (_built) return;
            _built = true;
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

            _root = new GameObject("PLACEHOLDER Arms (replace with the art style's)").transform;
            _root.SetParent(transform, false);
            for (int i = 0; i < 2; i++)
            {
                _upper[i] = Piece(_root, "Upper arm", _capsule, sleeveMaterial);
                _fore[i] = Piece(_root, "Forearm", _capsule, sleeveMaterial);
                _hand[i] = Piece(_root, "Hand", _sphere, skinMaterial);
                _handPos[i] = Rest(i == 0 ? -1f : 1f);
            }
            var bodyT = body != null ? body.transform : transform.root;
            _legRoot = new GameObject("PLACEHOLDER Legs (replace with the art style's)").transform;
            _legRoot.SetParent(bodyT, false);
            for (int i = 0; i < 2; i++)
            {
                _thigh[i] = Piece(_legRoot, "Thigh", _capsule, legMaterial);
                _shin[i] = Piece(_legRoot, "Shin", _capsule, legMaterial);
                _shoe[i] = Piece(_legRoot, "Shoe", _sphere, shoeMaterial);
            }
            _legRoot.gameObject.SetActive(false);
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

        Vector3 Shoulder(float side) => new Vector3(side * shoulderWidth, -shoulderDrop, 0.02f);
        Vector3 Rest(float side) => new Vector3(side * 0.21f, -0.36f, 0.36f);

        void LateUpdate()
        {
            if (!_built || body == null || body.Sim == null) return;
            _root.gameObject.SetActive(showArms);
            // Keep the arms the same size on screen when the speed FOV widens (no overlay camera needed).
            if (_cam == null) _cam = GetComponent<Camera>();
            if (_cam != null)
            {
                float baseFov = JamKit.Shell.GameRoot.Exists ? JamKit.Shell.GameRoot.Settings.fieldOfView : 68f;
                float k = Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / Mathf.Tan(baseFov * 0.5f * Mathf.Deg2Rad);
                _root.localScale = Vector3.one * Mathf.Clamp(k, 0.8f, 1.4f);
            }
            float dt = Time.deltaTime;
            var sim = body.Sim;
            var mode = sim.Mode;
            var ev = body.FrameEvents;
            float speed = sim.HorizontalSpeed;
            float run = Mathf.Clamp01(speed / Mathf.Max(1f, body.Def.sprintSpeed));
            float ph = body.StridePhase * Mathf.PI;

            if (ev.jumped || ev.wallJumped) _jumpLift = 1f;
            if (ev.doubleJumped) _sweep = 1f;
            _jumpLift = Mathf.MoveTowards(_jumpLift, 0f, dt * 3f);
            _sweep = Mathf.MoveTowards(_sweep, 0f, dt / 0.15f * 0.5f);
            _plant = Mathf.MoveTowards(_plant, mode == MoveMode.Mantle ? 1f : 0f, dt * 8f);

            // The wall: how far it is to the side, for the hand to land on it (not in it).
            int wallSide = sim.WallSide;
            float wallReach = 0.55f;
            _handOnWall = false;
            if (wallSide != 0)
            {
                Vector3 n = new Vector3(sim.WallNormal.X, 0f, sim.WallNormal.Z);
                if (Physics.Raycast(transform.position, -n, out var hit, 1.5f, ~(1 << 2), QueryTriggerInteraction.Ignore))
                    wallReach = Mathf.Clamp(hit.distance - 0.05f, 0.3f, 0.8f);
            }

            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                Vector3 target = Rest(side);
                float stiffness = 18f;
                switch (mode)
                {
                    case MoveMode.Grounded:
                    case MoveMode.Sprint:
                    {
                        float swing = Mathf.Sin(ph) * side;
                        target += new Vector3(-side * 0.02f * run, 0.035f * Mathf.Max(0f, swing) * run, 0.11f * swing * run);
                        break;
                    }
                    case MoveMode.Air:
                        target = new Vector3(side * 0.29f, -0.27f, 0.33f);
                        break;
                    case MoveMode.Wallrun:
                        if ((int)side == wallSide)
                        {
                            // Reach out and plant the palm on the wall, at shoulder height, a little ahead.
                            target = new Vector3(side * wallReach, -0.06f, 0.34f);
                            stiffness = 40f;
                            _handOnWall = true;
                        }
                        else
                        {
                            float swing = Mathf.Sin(ph) * side;
                            target = new Vector3(side * 0.25f, -0.33f, 0.34f + 0.1f * swing);
                        }
                        break;
                    case MoveMode.Slide:
                        target = new Vector3(side * 0.36f, -0.38f, 0.26f);
                        break;
                    case MoveMode.Mantle:
                        target = new Vector3(side * 0.24f, -0.22f + 0.1f * (1f - _plant), 0.48f);
                        stiffness = 30f;
                        break;
                }
                target += new Vector3(0f, 0.05f * _jumpLift, 0f);
                target = Vector3.Lerp(target, new Vector3(side * 0.33f, -0.44f, 0.12f), _sweep);
                // Arms feel a touch heavy on landing: they take the dip a moment later.
                if (ev.landed && !ev.slideStarted) _handVel[i] += Vector3.down * Mathf.Clamp01((ev.landImpact - 4f) / 14f) * 1.2f;

                // A critically damped spring toward the target.
                if (dt > 0f)
                {
                    float w = stiffness;
                    _handVel[i] += (w * w * (target - _handPos[i]) - 2f * w * _handVel[i]) * dt;
                    _handPos[i] += _handVel[i] * dt;
                }
                PoseArm(i, side, _handPos[i], mode == MoveMode.Wallrun && (int)side == wallSide);
            }

            PoseLegs(mode, dt);
        }

        void PoseArm(int i, float side, Vector3 handTarget, bool palmOut)
        {
            Vector3 s = Shoulder(side);
            Vector3 to = handTarget - s;
            float a = upperArm, b = forearm;
            float d = Mathf.Clamp(to.magnitude, 0.05f, a + b - 0.001f);
            Vector3 dir = to.normalized;
            Vector3 h = s + dir * d;
            // Elbow out and down.
            Vector3 pole = new Vector3(side, -1f, -0.4f).normalized;
            pole = (pole - dir * Vector3.Dot(pole, dir)).normalized;
            float along = (a * a - b * b + d * d) / (2f * d);
            float up = Mathf.Sqrt(Mathf.Max(0f, a * a - along * along));
            Vector3 e = s + dir * along + pole * up;
            Bone(_upper[i], s, e, armThickness);
            Bone(_fore[i], e, h, armThickness * 0.88f);
            var hand = _hand[i];
            hand.localPosition = h + (h - e).normalized * 0.05f;
            if (palmOut)
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

        static void Bone(Transform t, Vector3 from, Vector3 to, float thickness)
        {
            Vector3 d = to - from;
            float len = d.magnitude;
            t.localPosition = (from + to) * 0.5f;
            t.localRotation = len > 1e-4f ? Quaternion.FromToRotation(Vector3.up, d / len) : Quaternion.identity;
            t.localScale = new Vector3(thickness, Mathf.Max(thickness * 0.5f, len * 0.5f + thickness * 0.25f), thickness);
        }

        void PoseLegs(MoveMode mode, float dt)
        {
            bool want = showLegsWhenSliding && mode == MoveMode.Slide;
            _legs = Mathf.MoveTowards(_legs, want ? 1f : 0f, dt * 7f);
            bool shown = _legs > 0.01f;
            if (_legRoot.gameObject.activeSelf != shown) _legRoot.gameObject.SetActive(shown);
            if (!shown) return;
            // Follow the interpolated body (the body transform itself is at the last fixed step).
            var bt = body.transform;
            _legRoot.localPosition = bt.InverseTransformVector(body.RenderPosition - bt.position);
            // In the body's frame (yaw only): hips under the camera, legs out in front, feet rising into view.
            float k = _legs;
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                Vector3 hip = new Vector3(side * 0.13f, 0.42f, -0.05f);
                Vector3 knee = new Vector3(side * 0.16f, Mathf.Lerp(0.2f, 0.48f, k), Mathf.Lerp(0.1f, 0.5f, k) + (i == 0 ? 0.08f : 0f));
                Vector3 foot = new Vector3(side * 0.19f, Mathf.Lerp(0.1f, 0.3f, k), Mathf.Lerp(0.2f, 0.95f, k) + (i == 0 ? 0.06f : -0.05f));
                Bone(_thigh[i], hip, knee, 0.15f);
                Bone(_shin[i], knee, foot, 0.12f);
                _shoe[i].localPosition = foot + new Vector3(0f, 0.02f, 0.06f);
                _shoe[i].localRotation = Quaternion.Euler(-25f, 0f, 0f);
                _shoe[i].localScale = new Vector3(0.12f, 0.1f, 0.24f);
            }
        }
    }
}
