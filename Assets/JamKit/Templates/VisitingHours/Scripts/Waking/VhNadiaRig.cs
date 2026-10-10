using UnityEngine;

namespace JamKit.VisitingHours
{
    /// <summary>
    /// The seam between the opening and Nadia's body. The opening only says where her hand should be
    /// (<see cref="SetHand"/>, weight 0 = resting under the blanket), how far her thumb presses, whether
    /// we see her from her own eyes (<see cref="Mode.FirstPerson"/>: her arm reaches into view) or from
    /// above (<see cref="Mode.Asleep"/>: a shape under the blanket with her hair on the pillow), and how
    /// much she breathes. Two bodies fill it:
    /// <list type="bullet">
    /// <item><see cref="VhModelNadia"/>: her rigged Tripo model (Mixamo bones; Models/Nadia/Nadia.fbx), lying
    /// in the bed, head hidden in first person, the right arm posed by two-bone IK;</item>
    /// <item><see cref="VhStandInNadia"/>: a stylised arm and a hair shape made in code, used when the boot
    /// asset has no model (or the model has no Mixamo arm bones).</item>
    /// </list>
    /// A new body (another model, an animated arm) is one more subclass; the opening doesn't change.
    /// </summary>
    public abstract class VhNadiaRig : MonoBehaviour
    {
        public enum Mode { FirstPerson, Asleep }

        public abstract bool IsModel { get; }
        public abstract void SetMode(Mode mode);
        /// <summary>Where her wrist goes and which way her palm faces (world), blended from rest by <paramref name="weight"/>.</summary>
        public abstract void SetHand(Vector3 wrist, Quaternion palm, float weight);
        public abstract void SetThumb(float press01);
        /// <summary>0..1: the depth of her breath right now.</summary>
        public abstract void SetBreath(float amount01);
        public abstract void ShowArm(bool shown);

        /// <summary>
        /// Her fingers round the phone: <paramref name="curl"/> 0 open .. 1 closed round its edges, the thumb free, or pressing
        /// the screen by <paramref name="thumbPress"/>. <paramref name="phoneCentre"/>, <paramref name="phoneRotation"/> (+Y the
        /// screen, +Z its top) and <paramref name="phoneSize"/> describe the phone. Called after <see cref="SetHand"/>.
        /// </summary>
        public virtual void SetGrip(Vector3 phoneCentre, Quaternion phoneRotation, Vector3 phoneSize, float curl, float thumbPress) { }

        /// <summary>
        /// Two-bone IK in world space: puts the elbow on the side of <paramref name="pole"/> so the wrist
        /// reaches <paramref name="target"/> (or as close as the arm's length allows).
        /// </summary>
        public static void SolveTwoBone(Vector3 shoulder, float upper, float lower, Vector3 target, Vector3 pole, out Vector3 elbow, out Vector3 wrist)
        {
            Vector3 toT = target - shoulder;
            float d = Mathf.Clamp(toT.magnitude, Mathf.Abs(upper - lower) + 1e-3f, upper + lower - 1e-3f);
            Vector3 dir = toT.sqrMagnitude > 1e-8f ? toT.normalized : Vector3.forward;
            float cosA = Mathf.Clamp((upper * upper + d * d - lower * lower) / (2f * upper * d), -1f, 1f);
            float sinA = Mathf.Sqrt(1f - cosA * cosA);
            Vector3 toPole = pole - shoulder;
            Vector3 perp = toPole - dir * Vector3.Dot(toPole, dir);
            if (perp.sqrMagnitude < 1e-8f) perp = Vector3.Cross(dir, Vector3.right);
            perp.Normalize();
            elbow = shoulder + (dir * cosA + perp * sinA) * upper;
            wrist = shoulder + dir * d;
        }
    }

    /// <summary>The stand-in body: a sleeve-and-hand arm reaching from below the camera, and dark hair on the pillow.</summary>
    public sealed class VhStandInNadia : VhNadiaRig
    {
        public Transform upperArm, foreArm, hand, thumb, hair;
        public Vector3 shoulder;
        public Vector3 restWrist;
        public Quaternion restPalm = Quaternion.identity;
        public Vector3 pole;
        public float upperLength = 0.29f, lowerLength = 0.27f;

        Mode _mode = Mode.FirstPerson;
        bool _armShown = true;

        public override bool IsModel => false;

        public override void SetMode(Mode mode)
        {
            _mode = mode;
            Apply();
        }

        public override void ShowArm(bool shown)
        {
            _armShown = shown;
            Apply();
        }

        void Apply()
        {
            bool arm = _armShown && _mode == Mode.FirstPerson;
            if (upperArm != null) upperArm.gameObject.SetActive(arm);
            if (foreArm != null) foreArm.gameObject.SetActive(arm);
            if (hand != null) hand.gameObject.SetActive(arm);
            if (hair != null) hair.gameObject.SetActive(_mode == Mode.Asleep);
        }

        public override void SetHand(Vector3 wrist, Quaternion palm, float weight)
        {
            Vector3 target = Vector3.Lerp(restWrist, wrist, weight);
            Quaternion rot = Quaternion.Slerp(restPalm, palm, weight);
            SolveTwoBone(shoulder, upperLength, lowerLength, target, pole, out var elbow, out var w);
            Place(upperArm, shoulder, elbow);
            Place(foreArm, elbow, w);
            if (hand != null)
            {
                hand.position = w;
                hand.rotation = rot;
            }
        }

        /// <summary>A unit-length segment (pivot at its start, length along +Y) stretched from a to b.</summary>
        static void Place(Transform t, Vector3 a, Vector3 b)
        {
            if (t == null) return;
            Vector3 d = b - a;
            t.position = a;
            t.rotation = Quaternion.FromToRotation(Vector3.up, d.sqrMagnitude > 1e-8f ? d.normalized : Vector3.up);
            t.localScale = new Vector3(1f, d.magnitude, 1f);
        }

        public override void SetThumb(float press01)
        {
            if (thumb != null) thumb.localRotation = Quaternion.Euler(0f, 0f, -38f * Mathf.Clamp01(press01));
        }

        public override void SetBreath(float amount01) { }
    }

    /// <summary>
    /// Nadia's rigged model (Mixamo bone names). Placed lying in the bed with her head on the pillow:
    /// on her left side facing the nightstand for the first-person shots (head hidden, the right arm by
    /// IK), turned away for the overhead shot (head shown: her hair on the pillow). Her arms rest along
    /// her body; the blanket hides the rest. Breathing swells her chest a little.
    /// </summary>
    public sealed class VhModelNadia : VhNadiaRig
    {
        public Transform model;
        public Transform hips, spine, chest, head, rArm, rFore, rHand, rThumb, lArm, lFore;
        /// <summary>Where her eyes go (the first-person camera) and the pillow under her head.</summary>
        public Vector3 eyes;
        public Vector3 facingFirstPerson = Vector3.left, facingAsleep = Vector3.right, bodyUp = Vector3.forward;
        public Vector3 pole;
        public Vector3 restWrist;

        Quaternion _rArmBase, _rForeBase, _rHandBase, _thumbBase;
        // The right hand's fingers (index, middle, ring, pinky: three bones each) and thumb (three bones), and their rest rotations.
        readonly Transform[] _fingers = new Transform[12];
        readonly Quaternion[] _fingerBase = new Quaternion[12];
        readonly Transform[] _thumb = new Transform[3];
        readonly Quaternion[] _thumbBones = new Quaternion[3];
        Vector3 _chestScale = Vector3.one;
        Mode _mode = Mode.FirstPerson;
        float _upper, _lower;
        bool _armShown = true;

        public override bool IsModel => true;

        /// <summary>Finds the Mixamo bones under <paramref name="root"/>; false if the arm isn't there (use the stand-in).</summary>
        public bool Bind(Transform root)
        {
            model = root;
            hips = Bone(root, "Hips");
            spine = Bone(root, "Spine");
            chest = Bone(root, "Spine2") ?? Bone(root, "Spine1");
            head = Bone(root, "Head");
            rArm = Bone(root, "RightArm");
            rFore = Bone(root, "RightForeArm");
            rHand = Bone(root, "RightHand");
            rThumb = Bone(root, "RightHandThumb1");
            lArm = Bone(root, "LeftArm");
            lFore = Bone(root, "LeftForeArm");
            if (hips == null || head == null || rArm == null || rFore == null || rHand == null) return false;
            if (chest != null) _chestScale = chest.localScale;
            string[] names = { "Index", "Middle", "Ring", "Pinky" };
            for (int f = 0; f < 4; f++)
                for (int j = 0; j < 3; j++) _fingers[f * 3 + j] = Bone(root, "RightHand" + names[f] + (j + 1));
            for (int j = 0; j < 3; j++) _thumb[j] = Bone(root, "RightHandThumb" + (j + 1));
            return true;
        }

        public static Transform Bone(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name;
                if (n == name || n.EndsWith(":" + name, System.StringComparison.Ordinal) || n.EndsWith("_" + name, System.StringComparison.Ordinal)) return t;
            }
            return null;
        }

        /// <summary>Her height from the soles to the top of the head (world), from the bones.</summary>
        public float Height()
        {
            var r = model.GetComponentsInChildren<Renderer>(true);
            if (r.Length == 0) return 0f;
            var b = r[0].bounds;
            for (int i = 1; i < r.Length; i++) b.Encapsulate(r[i].bounds);
            return Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        }

        public override void SetMode(Mode mode)
        {
            _mode = mode;
            Lay(mode == Mode.FirstPerson ? facingFirstPerson : facingAsleep);
            if (head != null) head.localScale = mode == Mode.FirstPerson ? Vector3.one * 0.001f : Vector3.one;
            ShowArm(_armShown);
        }

        /// <summary>Turns her so she lies facing <paramref name="facing"/> with her head towards <see cref="bodyUp"/>,
        /// arms along her body, and moves her so her eyes are at <see cref="eyes"/>.</summary>
        void Lay(Vector3 facing)
        {
            // Her own frame from the bones: up from hips to head, right from the left shoulder to the right.
            Vector3 up = (head.position - hips.position).normalized;
            Vector3 right = lArm != null ? (rArm.position - lArm.position).normalized : model.right;
            Vector3 fwd = Vector3.Cross(right, up).normalized;
            if (fwd.sqrMagnitude < 0.5f) fwd = model.forward;
            Quaternion now = Quaternion.LookRotation(fwd, up);
            Quaternion want = Quaternion.LookRotation(facing, bodyUp);
            model.rotation = want * Quaternion.Inverse(now) * model.rotation;

            // Arms down along her body (from the A-pose), elbows a little bent.
            Vector3 down = -bodyUp;
            ArmDown(rArm, rFore, rHand, down, facing);
            ArmDown(lArm, lFore, Bone(model, "LeftHand"), down, facing);
            _rArmBase = rArm.localRotation;
            _rForeBase = rFore.localRotation;
            _rHandBase = rHand.localRotation;
            if (rThumb != null) _thumbBase = rThumb.localRotation;
            for (int i = 0; i < _fingers.Length; i++) if (_fingers[i] != null) _fingerBase[i] = _fingers[i].localRotation;
            for (int i = 0; i < _thumb.Length; i++) if (_thumb[i] != null) _thumbBones[i] = _thumb[i].localRotation;
            _upper = Vector3.Distance(rArm.position, rFore.position);
            _lower = Vector3.Distance(rFore.position, rHand.position);

            // Eyes: a little in front of and above the head bone.
            Vector3 eyeNow = head.position + facing * 0.07f + bodyUp * 0.06f;
            model.position += eyes - eyeNow;
        }

        static void ArmDown(Transform arm, Transform fore, Transform hand, Vector3 down, Vector3 facing)
        {
            if (arm == null || fore == null) return;
            Vector3 want = (down * 0.95f + facing * 0.3f).normalized;
            arm.rotation = Quaternion.FromToRotation(fore.position - arm.position, want) * arm.rotation;
            if (hand != null)
            {
                Vector3 wantFore = (down * 0.6f + facing * 0.8f).normalized;
                fore.rotation = Quaternion.FromToRotation(hand.position - fore.position, wantFore) * fore.rotation;
            }
        }

        public override void ShowArm(bool shown)
        {
            _armShown = shown;
        }

        public override void SetHand(Vector3 wrist, Quaternion palm, float weight)
        {
            rArm.localRotation = _rArmBase;
            rFore.localRotation = _rForeBase;
            rHand.localRotation = _rHandBase;
            if (_mode != Mode.FirstPerson || weight <= 0.001f || !_armShown) return;
            Vector3 rest = rHand.position;
            Vector3 target = Vector3.Lerp(rest, wrist, weight);
            SolveTwoBone(rArm.position, _upper, _lower, target, pole, out var elbow, out var w);
            rArm.rotation = Quaternion.FromToRotation(rFore.position - rArm.position, elbow - rArm.position) * rArm.rotation;
            rFore.rotation = Quaternion.FromToRotation(rHand.position - rFore.position, w - rFore.position) * rFore.rotation;
            // The hand: fingers along palm's forward, the back of the hand along palm's up (the palm faces the other way).
            Quaternion before = rHand.rotation;
            if (HandFrame(out var fingers, out var palmNormal))
            {
                Vector3 wantFwd = palm * Vector3.forward, wantPalm = -(palm * Vector3.up);
                rHand.rotation = Quaternion.FromToRotation(fingers, wantFwd) * rHand.rotation;
                if (HandFrame(out fingers, out palmNormal))
                {
                    Vector3 a = Vector3.ProjectOnPlane(palmNormal, wantFwd), b = Vector3.ProjectOnPlane(wantPalm, wantFwd);
                    if (a.sqrMagnitude > 1e-8f && b.sqrMagnitude > 1e-8f)
                        rHand.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(a, b, wantFwd), wantFwd) * rHand.rotation;
                }
                rHand.rotation = Quaternion.Slerp(before, rHand.rotation, weight);
            }
        }

        /// <summary>The right hand's fingers direction (wrist to the middle knuckle) and its palm's normal, from the bones.</summary>
        bool HandFrame(out Vector3 fingers, out Vector3 palmNormal)
        {
            fingers = palmNormal = Vector3.zero;
            var index = _fingers[0];
            var middle = _fingers[3];
            var pinky = _fingers[9];
            if (middle == null || index == null || pinky == null) return false;
            fingers = middle.position - rHand.position;
            Vector3 across = index.position - pinky.position; // the right hand: from the little finger to the index
            palmNormal = Vector3.Cross(fingers, across);
            if (fingers.sqrMagnitude < 1e-8f || palmNormal.sqrMagnitude < 1e-10f) return false;
            fingers.Normalize();
            palmNormal.Normalize();
            return true;
        }

        static readonly float[] CurlDegrees = { 45f, 55f, 35f };

        public override void SetGrip(Vector3 phoneCentre, Quaternion phoneRotation, Vector3 phoneSize, float curl, float thumbPress)
        {
            for (int i = 0; i < _fingers.Length; i++) if (_fingers[i] != null) _fingers[i].localRotation = _fingerBase[i];
            for (int i = 0; i < _thumb.Length; i++) if (_thumb[i] != null) _thumb[i].localRotation = _thumbBones[i];
            if (_mode != Mode.FirstPerson || !_armShown || !HandFrame(out var fingers, out var palmNormal)) return;
            Vector3 across = Vector3.Cross(palmNormal, fingers).normalized;
            // Curl each finger joint towards the palm (round the phone's edge): about 45, 55 and 35 degrees when closed.
            float sign = Vector3.Dot(Vector3.Cross(across, fingers), palmNormal) >= 0f ? 1f : -1f;
            for (int f = 0; f < 4; f++)
                for (int j = 0; j < 3; j++)
                {
                    var bone = _fingers[f * 3 + j];
                    if (bone == null) continue;
                    bone.rotation = Quaternion.AngleAxis(sign * CurlDegrees[j] * Mathf.Clamp01(curl), across) * bone.rotation;
                }
            // The thumb: free beside the phone, or pressing the screen just below its middle.
            if (_thumb[0] != null && _thumb[2] != null && thumbPress > 0f)
            {
                Vector3 up = phoneRotation * Vector3.up, top = phoneRotation * Vector3.forward;
                Vector3 target = phoneCentre + up * (phoneSize.y * 0.5f + 0.004f) - top * (phoneSize.z * 0.15f);
                Vector3 tip = _thumb[2].childCount > 0 ? _thumb[2].GetChild(0).position : _thumb[2].position;
                Quaternion aim = Quaternion.FromToRotation(tip - _thumb[0].position, target - _thumb[0].position);
                _thumb[0].rotation = Quaternion.Slerp(Quaternion.identity, aim, Mathf.Clamp01(thumbPress)) * _thumb[0].rotation;
            }
        }

        public override void SetThumb(float press01) { }

        public override void SetBreath(float amount01)
        {
            if (chest != null) chest.localScale = _chestScale * (1f + 0.025f * amount01);
        }
    }
}
