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

        /// <summary>The bed (or she) moved by <paramref name="delta"/> (world): her eyes, shoulder and rest pose move with it.</summary>
        public virtual void Carry(Matrix4x4 delta) { }

        /// <summary>
        /// Her fingers round the phone: <paramref name="curl"/> 0 open .. 1 closed round its edges, the thumb free, or pressing
        /// the screen by <paramref name="thumbPress"/>. <paramref name="phoneCentre"/>, <paramref name="phoneRotation"/> (+Y the
        /// screen, +Z its top) and <paramref name="phoneSize"/> describe the phone; <paramref name="held"/> 0 = taking it by its end
        /// on the nightstand, 1 = up in her hand. Her palm is always on its back. Called after <see cref="SetHand"/>.
        /// </summary>
        public virtual void SetGrip(Vector3 phoneCentre, Quaternion phoneRotation, Vector3 phoneSize, float curl, float thumbPress, float held) { }

        /// <summary>Her right shoulder (world) and her arm's full length, so what she holds can be kept within reach.
        /// False when the body doesn't know them.</summary>
        public virtual bool ArmReach(out Vector3 shoulder, out float length)
        {
            shoulder = Vector3.zero;
            length = 0f;
            return false;
        }

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

        public override void Carry(Matrix4x4 delta)
        {
            shoulder = delta.MultiplyPoint3x4(shoulder);
            restWrist = delta.MultiplyPoint3x4(restWrist);
            pole = delta.MultiplyPoint3x4(pole);
            restPalm = delta.rotation * restPalm;
        }

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
        // The ends of the four fingers and of the thumb (the Mixamo '4' bones), to measure where they reach.
        readonly Transform[] _fingerTips = new Transform[4];
        Transform _thumbTip;
        // The last SetHand, to re-seat the hand when it would sink into the phone.
        Vector3 _lastWrist;
        Quaternion _lastPalm = Quaternion.identity;
        float _lastWeight;
        readonly Quaternion[] _thumbBones = new Quaternion[3];
        Vector3 _chestScale = Vector3.one;
        Mode _mode = Mode.FirstPerson;
        float _upper, _lower;
        bool _armShown = true;

        public override bool IsModel => true;

        public override void Carry(Matrix4x4 delta)
        {
            eyes = delta.MultiplyPoint3x4(eyes);
            pole = delta.MultiplyPoint3x4(pole);
            restWrist = delta.MultiplyPoint3x4(restWrist);
            facingFirstPerson = delta.MultiplyVector(facingFirstPerson).normalized;
            facingAsleep = delta.MultiplyVector(facingAsleep).normalized;
            bodyUp = delta.MultiplyVector(bodyUp).normalized;
            SetMode(_mode);
        }

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
            {
                for (int j = 0; j < 3; j++) _fingers[f * 3 + j] = Bone(root, "RightHand" + names[f] + (j + 1));
                _fingerTips[f] = Bone(root, "RightHand" + names[f] + "4");
            }
            for (int j = 0; j < 3; j++) _thumb[j] = Bone(root, "RightHandThumb" + (j + 1));
            _thumbTip = Bone(root, "RightHandThumb4");
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
            _lastWrist = wrist;
            _lastPalm = palm;
            _lastWeight = weight;
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

        /// <summary>The most each finger joint bends (degrees) when the hand is closed and nothing stops it.</summary>
        static readonly float[] CurlDegrees = { 12f, 18f, 12f }; // small: her model's mitten-like fingers crumple when bent far
        const float FingerRadius = 0.008f, PalmClearance = 0.015f, CurlStep = 5f, EdgeMargin = 0.02f;

        /// <summary>Where the last grip seated her wrist (world): the grip's wrist, moved off and along the phone.</summary>
        public Vector3 SeatedWrist => _lastWrist;

        /// <summary>Held, a finger's end must stay behind the phone's back face (by a finger's thickness) while it's over
        /// the phone or within <see cref="EdgeMargin"/> of its outline: past the edge it shows from the side, never in front.</summary>
        bool FingerInFront(Vector3 e, float side)
        {
            Vector3 q = _phoneInv * (e - _phoneCentre);
            if (Mathf.Abs(q.x) > _phoneHalf.x + EdgeMargin || Mathf.Abs(q.z) > _phoneHalf.z + EdgeMargin) return false;
            return side * q.y < _phoneHalf.y + FingerRadius;
        }

        Vector3 _phoneCentre, _phoneHalf;
        Quaternion _phoneInv = Quaternion.identity;

        /// <summary>How far <paramref name="p"/> is outside the phone (negative inside): a box's signed distance.</summary>
        float PhoneDistance(Vector3 p)
        {
            Vector3 q = _phoneInv * (p - _phoneCentre);
            q = new Vector3(Mathf.Abs(q.x) - _phoneHalf.x, Mathf.Abs(q.y) - _phoneHalf.y, Mathf.Abs(q.z) - _phoneHalf.z);
            Vector3 outside = Vector3.Max(q, Vector3.zero);
            return outside.magnitude + Mathf.Min(Mathf.Max(q.x, Mathf.Max(q.y, q.z)), 0f);
        }

        /// <summary>
        /// How far the palm (the wrist, the knuckles and between them) must move along <paramref name="side"/> times the
        /// phone's normal to be clear of it on that side: every palm point over the phone's outline at least
        /// <see cref="PalmClearance"/> beyond that face. 0 when it already is.
        /// </summary>
        float PalmShortfall(float side)
        {
            float need = 0f;
            for (int i = 0; i < 9; i++)
            {
                Vector3 p;
                if (i == 0) p = rHand.position;
                else
                {
                    var k = _fingers[((i - 1) / 2) * 3];
                    if (k == null) continue;
                    p = (i & 1) == 1 ? k.position : (k.position + rHand.position) * 0.5f;
                }
                Vector3 q = _phoneInv * (p - _phoneCentre);
                if (Mathf.Abs(q.x) > _phoneHalf.x + 0.01f || Mathf.Abs(q.z) > _phoneHalf.z + 0.01f) continue;
                need = Mathf.Max(need, _phoneHalf.y + PalmClearance - side * q.y);
            }
            // The fingers too: every joint and tip over the phone's outline, or just past it (a finger is thick: its skin
            // would overlap the screen), clear of its face by a finger's thickness.
            for (int f = 0; f < 4; f++)
                for (int j = 1; j <= 3; j++)
                {
                    var t = j < 3 ? _fingers[f * 3 + j] : _fingerTips[f];
                    if (t == null) continue;
                    Vector3 q = _phoneInv * (t.position - _phoneCentre);
                    if (Mathf.Abs(q.x) > _phoneHalf.x + EdgeMargin || Mathf.Abs(q.z) > _phoneHalf.z + EdgeMargin) continue;
                    need = Mathf.Max(need, _phoneHalf.y + FingerRadius + 0.002f - side * q.y);
                }
            return need;
        }

        /// <summary>
        /// Seats the hand on the phone without passing through it: the palm moves off the phone (to the side it is on)
        /// until it clears it, then each finger joint bends towards the palm until that finger would touch the phone (or
        /// as far as <paramref name="curl"/> allows), so the fingers close round its edges; the thumb's tip rests at the
        /// screen's lower right edge, or presses the screen.
        /// </summary>
        public override void SetGrip(Vector3 phoneCentre, Quaternion phoneRotation, Vector3 phoneSize, float curl, float thumbPress, float held)
        {
            for (int i = 0; i < _fingers.Length; i++) if (_fingers[i] != null) _fingers[i].localRotation = _fingerBase[i];
            for (int i = 0; i < _thumb.Length; i++) if (_thumb[i] != null) _thumb[i].localRotation = _thumbBones[i];
            if (_mode != Mode.FirstPerson || !_armShown || _lastWeight <= 0.001f) return;
            _phoneCentre = phoneCentre;
            _phoneHalf = phoneSize * 0.5f;
            _phoneInv = Quaternion.Inverse(phoneRotation);
            Vector3 up = phoneRotation * Vector3.up, top = phoneRotation * Vector3.forward, right = phoneRotation * Vector3.right;

            // The palm off the phone, on the side the grip puts it (behind it once held, over the screen while it lies
            // there), and, when held, slid across its back so the knuckles line up with its far (left) edge: her hand is
            // bigger than the phone, and only the fingers should come round that edge.
            // Her palm is always on the phone's back (its +Y is the screen): never turned over onto the screen.
            const float side = -1f;
            float heldNow = Mathf.Clamp01(held);
            Vector3 wrist = _lastWrist;
            for (int pass = 0; pass < 4; pass++)
            {
                float need = PalmShortfall(side);
                Vector3 shift = need > 0.001f ? up * (side * (need + 0.002f)) : Vector3.zero;
                if (heldNow > 0.01f && _fingerTips[1] != null)
                {
                    // The fingers nearly straight along its back: slide the hand so the middle fingertip ends 1.5 cm past
                    // the far (left) edge, the rest of the hand behind the phone.
                    Vector3 d = _fingerTips[1].position - phoneCentre;
                    float slide = Mathf.Clamp(-(_phoneHalf.x + 0.015f) - Vector3.Dot(d, right), -0.08f, 0.08f);
                    slide *= heldNow;
                    if (Mathf.Abs(slide) > 0.003f) shift += right * slide;
                    // ...at about the middle of its height (not over the top corner).
                    float drop = Mathf.Clamp(-Vector3.Dot(d, top), -0.08f, 0.08f);
                    drop *= heldNow;
                    if (Mathf.Abs(drop) > 0.005f) shift += top * drop;
                }
                if (shift.sqrMagnitude < 1e-6f) break;
                wrist += shift;
                SetHand(wrist, _lastPalm, _lastWeight);
            }

            if (!HandFrame(out var fingers, out var palmNormal)) return;
            Vector3 across = Vector3.Cross(palmNormal, fingers).normalized;
            float sign = Vector3.Dot(Vector3.Cross(across, fingers), palmNormal) >= 0f ? 1f : -1f;
            float k = Mathf.Clamp01(curl) * _lastWeight;
            for (int f = 0; f < 4; f++)
                for (int j = 0; j < 3; j++)
                {
                    var bone = _fingers[f * 3 + j];
                    var end = j < 2 ? _fingers[f * 3 + j + 1] : _fingerTips[f];
                    if (bone == null || end == null) continue;
                    Quaternion start = bone.rotation;
                    float max = CurlDegrees[j] * k, best = 0f;
                    // Held: if the finger already points round to the screen's side, straighten it back a little.
                    if (side < 0f)
                        for (float a = -CurlStep; a >= -25f && FingerInFront(end.position, side); a -= CurlStep)
                        {
                            bone.rotation = Quaternion.AngleAxis(sign * a, across) * start;
                            best = a;
                        }
                    if (best < 0f) { bone.rotation = Quaternion.AngleAxis(sign * best, across) * start; continue; }
                    // Bend in small steps until the finger would touch the phone (it then rests against it).
                    for (float a = CurlStep; a <= max + 0.01f; a += CurlStep)
                    {
                        bone.rotation = Quaternion.AngleAxis(sign * a, across) * start;
                        Vector3 e = end.position, m = (bone.position + e) * 0.5f;
                        if (PhoneDistance(e) < FingerRadius || PhoneDistance(m) < FingerRadius) break;
                        // Not round the edge: held, the fingertips stay behind the phone (past its edge they show from
                        // the side, never over the screen's face).
                        if (side < 0f && FingerInFront(e, side)) break;
                        best = a;
                    }
                    bone.rotation = Quaternion.AngleAxis(sign * best, across) * start;
                }

            // The thumb, held: beside the phone's right edge (seen from the side, never across the screen), or folded
            // behind it if that can't be reached; pressing the screen near its lower right corner only while it taps.
            // Lying on the nightstand (the hand over it) the thumb just stays out of the phone.
            if (_thumb[0] == null || _thumbTip == null) return;
            bool pinching = heldNow < 0.5f;
            bool pressing = thumbPress > 0.3f;
            _thumbTargets[0] = phoneCentre + up * (_phoneHalf.y + 0.003f) + right * (_phoneHalf.x * 0.5f) - top * (_phoneHalf.z * 0.55f);
            _thumbTargets[1] = phoneCentre + right * (_phoneHalf.x + 0.010f) - top * (_phoneHalf.z * 0.25f);
            _thumbTargets[2] = phoneCentre - up * (_phoneHalf.y + 0.012f) + right * (_phoneHalf.x * 0.5f) - top * (_phoneHalf.z * 0.2f);
            // Taking it by its end: the thumb on its face just above the bottom edge (a pinch).
            _thumbTargets[3] = phoneCentre + up * (_phoneHalf.y + 0.006f) + right * (_phoneHalf.x * 0.35f) - top * (_phoneHalf.z - 0.015f);
            float w = Mathf.Clamp01(curl) * _lastWeight;
            for (int i = 0; i < 3; i++) _thumbStart[i] = _thumb[i] != null ? _thumb[i].localRotation : Quaternion.identity;
            bool done = false;
            for (int n = 0; n < 4 && !done; n++)
            {
                // The order to try: pressing the screen, or the pinch on its face, then beside the edge, then behind.
                int c = n == 0 ? (pressing ? 0 : pinching ? 3 : -1) : n;
                if (c < 0 || c > 3 || (n == 3 && c == 3)) continue;
                for (float k2 = 1f; k2 > 0.4f && !done; k2 -= 0.25f)
                {
                    AimThumb(_thumbTargets[c], w * k2);
                    done = ThumbClear(c == 0 || c == 3);
                }
            }
            if (!done) AimThumb(_thumbTargets[2], 1f);
        }

        readonly Quaternion[] _thumbStart = new Quaternion[3];
        readonly Vector3[] _thumbTargets = new Vector3[4];

        void AimThumb(Vector3 target, float weight)
        {
            for (int i = 0; i < 3; i++) if (_thumb[i] != null) _thumb[i].localRotation = _thumbStart[i];
            for (int j = 0; j < 2; j++)
            {
                var bone = _thumb[j];
                if (bone == null) continue;
                Quaternion aim = Quaternion.FromToRotation(_thumbTip.position - bone.position, target - bone.position);
                bone.rotation = Quaternion.Slerp(Quaternion.identity, aim, weight * (j == 0 ? 1f : 0.6f)) * bone.rotation;
            }
        }

        /// <summary>True when no part of the thumb is inside the phone and (unless <paramref name="screenAllowed"/>) none of
        /// it lies over the screen on its face's side.</summary>
        bool ThumbClear(bool screenAllowed)
        {
            Vector3 prev = _thumb[0].position;
            for (int i = 1; i <= 3; i++)
            {
                var t = i < 3 ? _thumb[i] : _thumbTip;
                if (t == null) continue;
                Vector3 p = t.position;
                Vector3 m = (p + prev) * 0.5f;
                if (PhoneDistance(p) < 0.006f || PhoneDistance(m) < 0.006f) return false;
                if (!screenAllowed && (OverScreen(p) || OverScreen(m))) return false;
                prev = p;
            }
            return true;
        }

        /// <summary>Over the screen on the screen's side of the phone.</summary>
        bool OverScreen(Vector3 p)
        {
            Vector3 q = _phoneInv * (p - _phoneCentre);
            return q.y > _phoneHalf.y && Mathf.Abs(q.x) < _phoneHalf.x && Mathf.Abs(q.z) < _phoneHalf.z;
        }

        public override void SetThumb(float press01) { }

        public override bool ArmReach(out Vector3 shoulder, out float length)
        {
            shoulder = rArm != null ? rArm.position : Vector3.zero;
            length = _upper + _lower;
            return rArm != null && length > 0.1f;
        }

        public override void SetBreath(float amount01)
        {
            if (chest != null) chest.localScale = _chestScale * (1f + 0.025f * amount01);
        }
    }
}
