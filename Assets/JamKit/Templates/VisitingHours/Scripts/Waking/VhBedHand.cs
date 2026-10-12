using UnityEngine;

namespace JamKit.VisitingHours
{
    /// <summary>
    /// Elias's near hand on the People agent's 3D model (EliasBed, docs/redesign/people.md), so it can answer her at the
    /// bedside (docs/redesign/day.md): after the animation (the "lie" clip) poses the hand, its fingers are turned towards
    /// the palm by <see cref="curl"/>, and <see cref="Twitch"/> moves them against hers (a small flex, then another). The
    /// humanoid avatar's finger bones (whatever the rig names them); each finger's bend axis is worked out once from the
    /// posed hand, and the bones are set absolutely from that rest every frame (a culled animator can't make it pile up).
    /// Nothing allocated per frame.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VhBedHand : MonoBehaviour
    {
        public Animator animator;
        [Tooltip("His left hand (true) or right hand: the one on the chair's side.")]
        public bool left = true;
        [Tooltip("A resting bend added to the clip's own (0 none, 1 a loose fist).")]
        [Range(0f, 1f)] public float curl = 0.1f;
        [Tooltip("Degrees each joint bends at curl 1.")]
        public float maxDegrees = 50f;

        // Index, middle, ring, little, thumb: three joints each.
        readonly Transform[] _bones = new Transform[15];
        readonly Quaternion[] _rest = new Quaternion[15];
        readonly Vector3[] _axis = new Vector3[15];
        Transform _hand, _middle, _thumb;
        bool _cached;
        float _twitchLeft, _twitchLength, _twitchAmount;

        static readonly HumanBodyBones[] LeftBones =
        {
            HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.LeftIndexDistal,
            HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.LeftMiddleDistal,
            HumanBodyBones.LeftRingProximal, HumanBodyBones.LeftRingIntermediate, HumanBodyBones.LeftRingDistal,
            HumanBodyBones.LeftLittleProximal, HumanBodyBones.LeftLittleIntermediate, HumanBodyBones.LeftLittleDistal,
            HumanBodyBones.LeftThumbProximal, HumanBodyBones.LeftThumbIntermediate, HumanBodyBones.LeftThumbDistal,
        };
        static readonly HumanBodyBones[] RightBones =
        {
            HumanBodyBones.RightIndexProximal, HumanBodyBones.RightIndexIntermediate, HumanBodyBones.RightIndexDistal,
            HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightMiddleIntermediate, HumanBodyBones.RightMiddleDistal,
            HumanBodyBones.RightRingProximal, HumanBodyBones.RightRingIntermediate, HumanBodyBones.RightRingDistal,
            HumanBodyBones.RightLittleProximal, HumanBodyBones.RightLittleIntermediate, HumanBodyBones.RightLittleDistal,
            HumanBodyBones.RightThumbProximal, HumanBodyBones.RightThumbIntermediate, HumanBodyBones.RightThumbDistal,
        };

        /// <summary>The model has the hand and at least one finger bone (the avatar is humanoid).</summary>
        public bool Ready => _hand != null && (_bones[3] != null || _bones[0] != null);
        public Transform Hand => _hand;

        /// <summary>Finds the bones (call again after changing <see cref="left"/>). False: no humanoid hand.</summary>
        public bool Bind(Animator a, bool leftHand)
        {
            animator = a;
            left = leftHand;
            _cached = false;
            _hand = _middle = _thumb = null;
            for (int i = 0; i < _bones.Length; i++) _bones[i] = null;
            if (animator == null || !animator.isHuman) return false;
            _hand = animator.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            var ids = left ? LeftBones : RightBones;
            for (int i = 0; i < ids.Length; i++) _bones[i] = animator.GetBoneTransform(ids[i]);
            _middle = _bones[3] != null ? _bones[3] : _bones[0];
            _thumb = _bones[12];
            return Ready;
        }

        /// <summary>Which way his fingers point (world), from the posed hand.</summary>
        public Vector3 Fingers
        {
            get
            {
                if (_hand == null || _middle == null) return transform.forward;
                var d = _middle.position - _hand.position;
                return d.sqrMagnitude > 1e-8f ? d.normalized : transform.forward;
            }
        }

        /// <summary>The back of his hand (world normal), from the posed hand (thumb and fingers, mirrored for the left).</summary>
        public Vector3 Back
        {
            get
            {
                if (_hand == null) return Vector3.up;
                var f = Fingers;
                var t = _thumb != null ? _thumb.position - _hand.position : Vector3.Cross(f, Vector3.up);
                t = Vector3.ProjectOnPlane(t, f);
                if (t.sqrMagnitude < 1e-8f) return Vector3.up;
                var back = left ? Vector3.Cross(f, t.normalized) : Vector3.Cross(t.normalized, f);
                return back.sqrMagnitude > 1e-8f ? back.normalized : Vector3.up;
            }
        }

        /// <summary>Where her hand rests on his: the back of his hand, between the wrist and the knuckles (world).</summary>
        public Vector3 HoldPoint => _hand == null ? transform.position : _hand.position + Fingers * 0.05f + Back * 0.028f;

        /// <summary>His fingers move against hers: <paramref name="amount"/> (0..1) of a bend, over <paramref name="seconds"/>.</summary>
        public void Twitch(float amount, float seconds)
        {
            _twitchAmount = Mathf.Clamp01(amount);
            _twitchLength = Mathf.Max(0.2f, seconds);
            _twitchLeft = _twitchLength;
        }

        void LateUpdate()
        {
            if (!Ready) return;
            if (!_cached) Cache();
            float extra = 0f;
            if (_twitchLeft > 0f)
            {
                _twitchLeft -= Time.deltaTime;
                // Two small flexes, the second weaker: a squeeze, a pause, a smaller one.
                float u = 1f - Mathf.Clamp01(_twitchLeft / _twitchLength);
                float a = Mathf.Sin(Mathf.Clamp01(u / 0.45f) * Mathf.PI);
                float b = u > 0.55f ? Mathf.Sin(Mathf.Clamp01((u - 0.55f) / 0.45f) * Mathf.PI) * 0.55f : 0f;
                extra = _twitchAmount * Mathf.Max(a, b);
            }
            float c = Mathf.Clamp01(curl + extra);
            for (int i = 0; i < _bones.Length; i++)
            {
                var bone = _bones[i];
                if (bone == null) continue;
                // The proximal joints bend least, the middle most; the thumb half as much.
                int joint = i % 3;
                float k = joint == 0 ? 0.7f : joint == 1 ? 1f : 0.75f;
                if (i >= 12) k *= 0.5f;
                bone.localRotation = _rest[i] * Quaternion.AngleAxis(c * maxDegrees * k, _axis[i]);
            }
        }

        /// <summary>The clip's pose of each finger joint, and the axis it bends about (in the joint's own frame).</summary>
        void Cache()
        {
            var palm = -Back;
            for (int i = 0; i < _bones.Length; i++)
            {
                var bone = _bones[i];
                if (bone == null) continue;
                _rest[i] = bone.localRotation;
                Vector3 dir;
                var next = (i % 3) < 2 ? _bones[i + 1] : null;
                if (next != null) dir = next.position - bone.position;
                else if (bone.childCount > 0) dir = bone.GetChild(0).position - bone.position;
                else dir = bone.position - bone.parent.position;
                if (dir.sqrMagnitude < 1e-10f) dir = Fingers;
                var worldAxis = Vector3.Cross(dir.normalized, palm);
                if (worldAxis.sqrMagnitude < 1e-8f) worldAxis = Vector3.Cross(Fingers, palm);
                _axis[i] = Quaternion.Inverse(bone.rotation) * worldAxis.normalized;
            }
            _cached = true;
        }
    }
}
