using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using NVec3 = System.Numerics.Vector3;

namespace JamKit.Movement
{
    /// <summary>
    /// A first-person body from any model with Mixamo bone names (mixamorig:Hips, Spine, Head, LeftArm,
    /// LeftForeArm, LeftHand, LeftUpLeg, LeftLeg, LeftFoot...): each frame it goes back to the bind pose,
    /// stands the model at the body's feet facing its yaw, leans the torso, puts the eyes on the camera's
    /// head, and reaches the hands and feet to the feel's targets with two-bone IK (elbows and knees towards
    /// their hints). In first person the neck follows only part of the view's pitch, and the visible copy is
    /// cut away inside one sphere around the posed head, hair and neck (measured from the mesh) and the camera
    /// (a material property, <see cref="cutoutProperty"/>), while a shadow-only copy, sharing the same bones,
    /// casts the whole body's shadow, head included. A frame it isn't posed in, the visible copy hides.
    /// No colliders; layer Ignore Raycast.
    /// </summary>
    [DefaultExecutionOrder(61)]
    public sealed class MixamoBodyRig : MovementBodyRig
    {
        [Tooltip("The model's root (this object by default).")]
        public Transform model;
        [Tooltip("Material property (xyz centre, w radius) the visible copy's shader cuts away around; JamKit/VisitingHours/Waking has it.")]
        public string cutoutProperty = "_CameraCutout";
        [Tooltip("The head, hair and neck as a sphere in the head bone's frame: its centre (measured from the mesh by MeasureHead when the builder wires her; estimated from the bones when it's empty).")]
        public Vector3 headCentre;
        [Tooltip("That sphere's radius (head bone units; 0: not measured yet).")]
        public float headRadius;
        [Tooltip("First person: the cut-away reaches this much past the head's sphere (m).")]
        public float cutoutMargin = 0.01f;
        [Tooltip("First person: the cut-away also keeps this much room around the camera (m): the neck and collar just under it.")]
        public float cameraClearance = 0.1f;
        [Tooltip("First person: how much of the view's pitch the neck follows (the shadow nods). A full nod swung her face out of the cut-away and into view when looking down.")]
        [Range(0f, 1f)] public float neckFollow = 0.25f;
        [Tooltip("First person: the most the neck turns from her torso (degrees).")]
        public float neckFollowMax = 20f;
        [Tooltip("The eyes, from the head bone: forward and up (m).")]
        public float eyeForward = 0.09f, eyeUp = 0.07f;
        [Tooltip("The ankle above the sole (m).")]
        public float ankleHeight = 0.08f;
        [Tooltip("How far the collarbone may turn to help a hand reach (degrees).")]
        public float shoulderAssist = 20f;
        [Tooltip("A shadow-only copy casts the whole body's shadow, head included.")]
        public bool castShadow = true;
        [Tooltip("Breathing swells the chest this much (fraction).")]
        public float breathChest = 0.02f;
        [Tooltip("Optional: a bone that swings with the lanyard spring (none on a model whose lanyard is part of the body mesh).")]
        public Transform lanyardBone;

        Transform _hips, _spine, _spine1, _chest, _neck, _head;
        readonly Transform[] _shoulder = new Transform[2], _arm = new Transform[2], _fore = new Transform[2], _hand = new Transform[2], _finger = new Transform[2];
        readonly Transform[] _up = new Transform[2], _leg = new Transform[2], _foot = new Transform[2], _toe = new Transform[2];
        Transform[] _bones;
        Vector3[] _bindPos;
        Quaternion[] _bindRot;
        Vector3[] _bindScale;
        Quaternion _faceFix = Quaternion.identity;
        readonly float[] _armLength = new float[2];
        Quaternion _lanyardBind;
        SkinnedMeshRenderer[] _visible = new SkinnedMeshRenderer[0];
        MaterialPropertyBlock _block;
        int _cutoutId;
        bool _ready;
        Vector3 _camPos;
        bool _third;
        Vector4 _cutout;
        int _posedFrame = -1;
        bool _shown = true;

        public override bool Ready => _ready;
        public override float CutoutRadius => _third ? 0f : _cutout.w;
        /// <summary>The last pose's cut-away (xyz centre, w radius; 0 in third person), as the visible copy's material got it.</summary>
        public Vector4 Cutout => _cutout;
        public override Vector3 HeadPosition => _head != null ? _head.position : transform.position;
        public override Vector3 HandPosition(int side) => _hand[side] != null ? _hand[side].position : Vector3.zero;
        public override Vector3 FootPosition(int side) => _foot[side] != null ? _foot[side].position : Vector3.zero;

        public override bool ArmReach(int side, out Vector3 shoulder, out float reach)
        {
            shoulder = _arm[side] != null ? _arm[side].position : Vector3.zero;
            reach = _armLength[side];
            return _ready && reach > 0f;
        }
        public IReadOnlyList<SkinnedMeshRenderer> VisibleRenderers => _visible;
        /// <summary>After the last pose: how far each hand and ankle ended from its target (m), and the head bone from the camera (tests).</summary>
        public readonly float[] HandError = new float[2], FootError = new float[2];
        /// <summary>After the last pose: each ankle in the body's frame (x right, y up from the soles, z forward).</summary>
        public readonly Vector3[] FootLocal = new Vector3[2];
        public float HeadDistance;
        /// <summary>After the last pose: how far inside the cut-away the head's sphere sits (m; negative: part of the head shows).</summary>
        public float HeadMargin;

        void Awake() => Bind();

        /// <summary>Finds the bones and makes the shadow copy; false (the capsules stay) when the arms or legs aren't there.</summary>
        public bool Bind()
        {
            if (_ready) return true;
            if (!FindBones()) return false;

            foreach (var a in model.GetComponentsInChildren<Animator>(true)) a.enabled = false;
            foreach (var c in model.GetComponentsInChildren<Collider>(true))
            {
                if (Application.isPlaying) Destroy(c);
                else DestroyImmediate(c);
            }
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 2;

            // Which way the model faces in the root's own frame (its forward from the bones).
            var rootRot = model.rotation;
            Vector3 up = Quaternion.Inverse(rootRot) * (_head.position - _hips.position);
            Vector3 right = Quaternion.Inverse(rootRot) * (_arm[1].position - _arm[0].position);
            Vector3 fwd = Vector3.Cross(right, up);
            if (fwd.sqrMagnitude > 1e-6f && up.sqrMagnitude > 1e-6f) _faceFix = Quaternion.Inverse(Quaternion.LookRotation(fwd.normalized, up.normalized));

            _bones = model.GetComponentsInChildren<Transform>(true);
            _bindPos = new Vector3[_bones.Length];
            _bindRot = new Quaternion[_bones.Length];
            _bindScale = new Vector3[_bones.Length];
            for (int i = 0; i < _bones.Length; i++)
            {
                _bindPos[i] = _bones[i].localPosition;
                _bindRot[i] = _bones[i].localRotation;
                _bindScale[i] = _bones[i].localScale;
            }
            if (lanyardBone != null) _lanyardBind = lanyardBone.localRotation;
            for (int i = 0; i < 2; i++)
                _armLength[i] = Vector3.Distance(_arm[i].position, _fore[i].position) + Vector3.Distance(_fore[i].position, _hand[i].position);

            var list = new List<SkinnedMeshRenderer>();
            foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr.name.EndsWith(" (shadow)", System.StringComparison.Ordinal)) continue;
                smr.updateWhenOffscreen = true;
                smr.receiveShadows = true;
                smr.shadowCastingMode = castShadow ? ShadowCastingMode.Off : ShadowCastingMode.On;
                list.Add(smr);
                if (!castShadow) continue;
                var go = new GameObject(smr.name + " (shadow)") { layer = 2 };
                go.transform.SetParent(smr.transform.parent, false);
                go.transform.localPosition = smr.transform.localPosition;
                go.transform.localRotation = smr.transform.localRotation;
                go.transform.localScale = smr.transform.localScale;
                var copy = go.AddComponent<SkinnedMeshRenderer>();
                copy.sharedMesh = smr.sharedMesh;
                copy.bones = smr.bones;
                copy.rootBone = smr.rootBone;
                copy.sharedMaterials = smr.sharedMaterials;
                copy.updateWhenOffscreen = true;
                copy.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            }
            _visible = list.ToArray();
            _block = new MaterialPropertyBlock();
            _cutoutId = Shader.PropertyToID(cutoutProperty);
            if (headRadius <= 0f && !MeasureHead()) EstimateHead();
            _ready = true;
            return true;
        }

        /// <summary>Finds the Mixamo bones; false when the arms, legs, hips or head aren't there.</summary>
        bool FindBones()
        {
            if (model == null) model = transform;
            _hips = Bone("Hips");
            _spine = Bone("Spine");
            _spine1 = Bone("Spine1");
            _chest = Bone("Spine2") ?? _spine1;
            _neck = Bone("Neck");
            _head = Bone("Head");
            string[] s = { "Left", "Right" };
            for (int i = 0; i < 2; i++)
            {
                _shoulder[i] = Bone(s[i] + "Shoulder");
                _arm[i] = Bone(s[i] + "Arm");
                _fore[i] = Bone(s[i] + "ForeArm");
                _hand[i] = Bone(s[i] + "Hand");
                _finger[i] = Bone(s[i] + "HandMiddle1");
                _up[i] = Bone(s[i] + "UpLeg");
                _leg[i] = Bone(s[i] + "Leg");
                _foot[i] = Bone(s[i] + "Foot");
                _toe[i] = Bone(s[i] + "ToeBase");
                if (_arm[i] == null || _fore[i] == null || _hand[i] == null || _up[i] == null || _leg[i] == null || _foot[i] == null) return false;
            }
            return _hips != null && _head != null;
        }

        /// <summary>
        /// Measures the head, the hair and the neck (the vertices skinned at least half to the Neck, the Head or the head's
        /// children) as a sphere in the head bone's frame: <see cref="headCentre"/>, <see cref="headRadius"/>. The first-person
        /// cut-away is centred on it wherever the head is posed. Needs the mesh's data: in the editor outside Play mode (the
        /// builders call it when they wire her, and it's saved with the scene) or a Read/Write mesh. False when it can't.
        /// </summary>
        public bool MeasureHead()
        {
            if (_head == null && !FindBones()) return false;
            var pts = new List<Vector3>();
            foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr.name.EndsWith(" (shadow)", System.StringComparison.Ordinal)) continue;
                var mesh = smr.sharedMesh;
                if (mesh == null || (!mesh.isReadable && Application.isPlaying)) continue;
                var bones = smr.bones;
                int headIdx = System.Array.IndexOf(bones, _head);
                if (headIdx < 0) continue;
                var inHead = new bool[bones.Length];
                for (int b = 0; b < bones.Length; b++)
                    inHead[b] = bones[b] != null && (bones[b] == _neck || bones[b].IsChildOf(_head));
                var bindposes = mesh.bindposes;
                var verts = mesh.vertices;
                var weights = mesh.boneWeights;
                if (bindposes.Length != bones.Length || weights.Length != verts.Length) continue;
                var toHead = bindposes[headIdx];
                for (int v = 0; v < verts.Length; v++)
                {
                    var bw = weights[v];
                    float w = (inHead[bw.boneIndex0] ? bw.weight0 : 0f) + (inHead[bw.boneIndex1] ? bw.weight1 : 0f)
                        + (inHead[bw.boneIndex2] ? bw.weight2 : 0f) + (inHead[bw.boneIndex3] ? bw.weight3 : 0f);
                    if (w >= 0.5f) pts.Add(toHead.MultiplyPoint3x4(verts[v]));
                }
            }
            if (pts.Count < 4) return false;
            // The smallest sphere around them, near enough (Badoiu-Clarkson: step towards the farthest point).
            Vector3 c = Vector3.zero;
            foreach (var p in pts) c += p;
            c /= pts.Count;
            for (int k = 0; k < 300; k++)
            {
                Vector3 far = c;
                float best = -1f;
                foreach (var p in pts)
                {
                    float d = (p - c).sqrMagnitude;
                    if (d > best) { best = d; far = p; }
                }
                c += (far - c) / (k + 2f);
            }
            float r = 0f;
            foreach (var p in pts) r = Mathf.Max(r, (p - c).magnitude);
            headCentre = c;
            headRadius = r;
            return true;
        }

        /// <summary>Without the mesh's data: a generous sphere from the neck to the top of the head (the head's farthest child).</summary>
        void EstimateHead()
        {
            Vector3 top = _head.position;
            float far = 0f;
            foreach (var t in _head.GetComponentsInChildren<Transform>(true))
            {
                float d = (t.position - _head.position).sqrMagnitude;
                if (d > far) { far = d; top = t.position; }
            }
            Vector3 bottom = _neck != null ? _neck.position : _head.position;
            if (far < 1e-6f) top = _head.position + (_head.position - bottom) * 2f;
            float len = Mathf.Max(0.15f, (top - bottom).magnitude);
            headCentre = _head.InverseTransformPoint((top + bottom) * 0.5f);
            headRadius = len * 0.6f / Mathf.Max(1e-4f, MaxScale(_head));
        }

        static float MaxScale(Transform t)
        {
            Vector3 s = t.lossyScale;
            return Mathf.Max(Mathf.Abs(s.x), Mathf.Max(Mathf.Abs(s.y), Mathf.Abs(s.z)));
        }

        /// <summary>The smallest sphere around two spheres (<paramref name="a"/>, <paramref name="ra"/>) and (<paramref name="b"/>, <paramref name="rb"/>).</summary>
        public static void Enclose(Vector3 a, float ra, Vector3 b, float rb, out Vector3 centre, out float radius)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len + rb <= ra) { centre = a; radius = ra; return; }
            if (len + ra <= rb) { centre = b; radius = rb; return; }
            radius = (len + ra + rb) * 0.5f;
            centre = a + d * ((radius - ra) / len);
        }

        /// <summary>A frame the body wasn't posed in (the MovementBody skipped it, or is off): the visible copy hides, so a stale
        /// pose, or the bind pose before the first one, never shows its head outside the cut-away.</summary>
        void LateUpdate()
        {
            if (!_ready) return;
            bool posed = _posedFrame == Time.frameCount;
            if (posed != _shown) Show(posed);
        }

        void Show(bool on)
        {
            _shown = on;
            for (int i = 0; i < _visible.Length; i++)
                if (_visible[i] != null) _visible[i].enabled = on;
        }

        Transform Bone(string name)
        {
            var root = model != null ? model : transform;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name;
                if (n == name || n.EndsWith(":" + name, System.StringComparison.Ordinal) || n.EndsWith("_" + name, System.StringComparison.Ordinal)) return t;
            }
            return null;
        }

        static Vector3 U(NVec3 v) => new Vector3(v.X, v.Y, v.Z);

        public override void Apply(BodyFeelPose pose, in MovementBodyFrame f)
        {
            if (!_ready || f.camera == null) return;
            _third = f.thirdPerson;
            for (int i = 0; i < _bones.Length; i++)
            {
                var b = _bones[i];
                b.localPosition = _bindPos[i];
                b.localRotation = _bindRot[i];
                b.localScale = _bindScale[i];
            }
            Vector3 fwd = f.bodyRotation * Vector3.forward, right = f.bodyRotation * Vector3.right;

            // Stand at the feet, facing the body's yaw.
            model.SetPositionAndRotation(f.feet, f.bodyRotation * _faceFix);

            // The torso: the speed tier's lean, the kick's shove, the lean into turns, and a peek's sideways bend (half each on
            // two spine bones).
            Quaternion lean = Quaternion.AngleAxis(pose.torsoPitch * 0.5f, right) * Quaternion.AngleAxis(-(pose.torsoRoll + f.lean) * 0.5f, fwd);
            if (_spine != null) _spine.rotation = lean * _spine.rotation;
            if (_spine1 != null) _spine1.rotation = lean * _spine1.rotation;
            // The eyes on the camera's head, the body taking its landing dip.
            Vector3 eyes = _head.position + fwd * eyeForward + Vector3.up * eyeUp;
            _hips.position += f.eyes + Vector3.up * pose.bodyDip - eyes;
            // Then the head looks where the camera looks (the shadow shows it). In first person the neck follows only part of
            // the pitch: a full nod swings the face forward and down around the neck, out past the eyes and into view.
            var neck = _neck != null ? _neck : _head;
            float nod = _third ? f.pitch - pose.torsoPitch
                : Mathf.Clamp(f.pitch * neckFollow - pose.torsoPitch, -neckFollowMax, neckFollowMax);
            neck.rotation = Quaternion.AngleAxis(nod, right) * neck.rotation;

            if (_chest != null) _chest.localScale = Vector3.Scale(_chest.localScale, Vector3.one * (1f + breathChest * pose.breath));
            if (lanyardBone != null) lanyardBone.localRotation = _lanyardBind * Quaternion.Euler(pose.lanyardPitch, 0f, pose.lanyardRoll);

            // Arms to the hands' targets (camera space) with the elbow hints; palms onto what they touch.
            var cam = f.camera;
            for (int i = 0; i < 2; i++)
            {
                Vector3 target = cam.TransformPoint(U(pose.hand[i]));
                Vector3 pole = cam.TransformDirection(U(pose.elbowPole[i]));
                float reach = Vector3.Distance(_arm[i].position, _fore[i].position) + Vector3.Distance(_fore[i].position, _hand[i].position);
                if (_shoulder[i] != null && shoulderAssist > 0f && Vector3.Distance(_arm[i].position, target) > reach * 0.95f)
                {
                    // The collarbone turns a little towards a far hand.
                    Vector3 from = _arm[i].position - _shoulder[i].position, to = target - _shoulder[i].position;
                    var q = Quaternion.FromToRotation(from, to);
                    q.ToAngleAxis(out float ang, out Vector3 axis);
                    if (ang > 180f) ang -= 360f;
                    _shoulder[i].rotation = Quaternion.AngleAxis(Mathf.Clamp(ang, -shoulderAssist, shoulderAssist), axis) * _shoulder[i].rotation;
                }
                TwoBone(_arm[i], _fore[i], _hand[i], target, pole);
                HandError[i] = Vector3.Distance(_hand[i].position, target);
                if (pose.contact[i] && _finger[i] != null)
                {
                    // Fingers forward and a little up along what she touches.
                    Vector3 want = (cam.forward + Vector3.up * 0.25f).normalized;
                    _hand[i].rotation = Quaternion.FromToRotation(_finger[i].position - _hand[i].position, want) * _hand[i].rotation;
                }
            }

            // Legs to the feet's targets (body space, from the soles), knees forward; feet along the body.
            for (int i = 0; i < 2; i++)
            {
                Vector3 ankle = f.feet + f.bodyRotation * U(pose.foot[i]) + Vector3.up * ankleHeight;
                Vector3 pole = f.bodyRotation * U(pose.kneePole[i]);
                TwoBone(_up[i], _leg[i], _foot[i], ankle, pole);
                FootError[i] = Vector3.Distance(_foot[i].position, ankle);
                FootLocal[i] = Quaternion.Inverse(f.bodyRotation) * (_foot[i].position - f.feet);
                if (_toe[i] != null)
                {
                    Vector3 toes = _toe[i].position - _foot[i].position;
                    Vector3 want = Vector3.ProjectOnPlane(fwd, Vector3.up).normalized * toes.magnitude;
                    if (pose.legs > 0f) want = Vector3.Slerp(want, (fwd + Vector3.up * 0.8f).normalized * toes.magnitude, pose.legs);
                    _foot[i].rotation = Quaternion.FromToRotation(toes, want) * _foot[i].rotation;
                }
            }

            // First person: cut the head, hair and neck away wherever they're posed, and the room right around the camera
            // (one sphere around both; the shadow copy keeps them).
            _camPos = cam.position;
            HeadDistance = Vector3.Distance(_head.position, _camPos);
            Vector3 hc = _head.TransformPoint(headCentre);
            float hr = headRadius * MaxScale(_head);
            Enclose(hc, hr + cutoutMargin, _camPos, cameraClearance, out var centre, out float radius);
            if (_third) radius = 0f;
            HeadMargin = radius - (Vector3.Distance(centre, hc) + hr);
            _cutout = new Vector4(centre.x, centre.y, centre.z, radius);
            var v = _cutout;
            for (int i = 0; i < _visible.Length; i++)
            {
                var r = _visible[i];
                r.GetPropertyBlock(_block);
                _block.SetVector(_cutoutId, v);
                r.SetPropertyBlock(_block);
            }
            _posedFrame = Time.frameCount;
            if (!_shown) Show(true);
        }

        /// <summary>Two-bone IK in world space: <paramref name="a"/> (shoulder or hip), <paramref name="b"/> (elbow or knee),
        /// <paramref name="c"/> (wrist or ankle) reaches <paramref name="target"/> (as near as the limb allows), the middle joint towards <paramref name="pole"/>.</summary>
        public static void TwoBone(Transform a, Transform b, Transform c, Vector3 target, Vector3 pole)
        {
            Vector3 pa = a.position, pb = b.position, pc = c.position;
            float la = Vector3.Distance(pa, pb), lb = Vector3.Distance(pb, pc);
            if (la < 1e-4f || lb < 1e-4f) return;
            Vector3 to = target - pa;
            float d = Mathf.Clamp(to.magnitude, Mathf.Abs(la - lb) + 1e-3f, la + lb - 1e-3f);
            Vector3 dir = to.sqrMagnitude > 1e-8f ? to.normalized : (pc - pa).normalized;
            float cosA = Mathf.Clamp((la * la + d * d - lb * lb) / (2f * la * d), -1f, 1f);
            float sinA = Mathf.Sqrt(1f - cosA * cosA);
            Vector3 perp = pole - dir * Vector3.Dot(pole, dir);
            if (perp.sqrMagnitude < 1e-8f) perp = Vector3.ProjectOnPlane(pb - pa, dir);
            if (perp.sqrMagnitude < 1e-8f) perp = Vector3.Cross(dir, Vector3.right);
            perp.Normalize();
            Vector3 elbow = pa + (dir * cosA + perp * sinA) * la;
            a.rotation = Quaternion.FromToRotation(pb - pa, elbow - pa) * a.rotation;
            pb = b.position;
            pc = c.position;
            b.rotation = Quaternion.FromToRotation(pc - pb, pa + dir * d - pb) * b.rotation;
        }
    }
}
