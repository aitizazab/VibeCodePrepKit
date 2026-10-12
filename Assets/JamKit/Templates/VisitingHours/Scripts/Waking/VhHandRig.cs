using UnityEngine;

namespace JamKit.VisitingHours
{
    /// <summary>
    /// A hand with fingers that bend (docs/redesign/day.md, the bedside): the palm, four fingers of three joints, a thumb of
    /// two, and the forearm, built from soft rounded pieces on the ward's skin material. Its root is the wrist: +Z towards
    /// the fingers, +Y the back of the hand, the thumb on <see cref="thumbSide"/>. Elias's lies on the blanket and answers
    /// her (<see cref="Twitch"/>: his fingers move); Nadia's comes into frame and holds his (<see cref="curl"/>). The joints
    /// are only turned when the pose changes (nothing allocated per frame).
    /// </summary>
    public sealed class VhHandRig : MonoBehaviour
    {
        [Range(0f, 1f)] public float curl = 0.15f;
        [Range(0f, 1f)] public float thumbCurl = 0.2f;
        public float thumbSide = 1f;
        /// <summary>A slow stroke of the thumb (0 none, 1 full): her thumb moving on the back of his hand.</summary>
        [Range(0f, 1f)] public float stroke;

        Transform[,] _joints;
        Transform[] _thumb;
        float _twitch, _twitchLeft, _twitchLength, _strokeClock;
        float _appliedCurl = -1f, _appliedThumb = -1f, _appliedTwitch = -1f;

        // Index, middle, ring, little: the knuckles across the palm and the lengths of their three joints.
        static readonly float[] KnuckleX = { 0.031f, 0.011f, -0.009f, -0.028f };
        static readonly float[,] Lengths = { { 0.042f, 0.026f, 0.02f }, { 0.046f, 0.029f, 0.021f }, { 0.043f, 0.027f, 0.02f }, { 0.034f, 0.021f, 0.018f } };
        static readonly float[] Widths = { 0.0185f, 0.0195f, 0.018f, 0.0155f };

        /// <summary>Builds the hand under this transform (the wrist). <paramref name="sleeve"/>: a sleeve's material from
        /// <paramref name="sleeveFrom"/> metres up the forearm (null: bare to the elbow).</summary>
        public void Build(VhMeshKit meshes, Material skin, Material sleeve, float scale, float side, float forearm, float sleeveFrom = 0.18f)
        {
            thumbSide = side;
            _joints = new Transform[4, 3];
            _thumb = new Transform[2];
            float s = scale;
            VhMeshKit.Show(transform, "Palm", meshes.RoundedBox(new Vector3(0.084f, 0.028f, 0.094f) * s, 0.012f * s, 4, 0.004f * s), skin, new Vector3(0f, 0f, 0.05f) * s, Quaternion.identity);
            VhMeshKit.Show(transform, "Heel of the hand", meshes.RoundedBox(new Vector3(0.07f, 0.03f, 0.04f) * s, 0.013f * s, 4), skin, new Vector3(-side * 0.005f, -0.004f, 0.012f) * s, Quaternion.identity);
            for (int f = 0; f < 4; f++)
            {
                Transform parent = transform;
                var at = new Vector3(KnuckleX[f] * side, 0.002f, 0.094f) * s;
                for (int j = 0; j < 3; j++)
                {
                    var joint = new GameObject("Finger " + f + " joint " + j).transform;
                    joint.SetParent(parent, false);
                    joint.localPosition = at;
                    float len = Lengths[f, j] * s, w = Widths[f] * s * (1f - j * 0.08f);
                    VhMeshKit.Show(joint, "Segment", meshes.RoundedBox(new Vector3(w, w * 0.86f, len + w * 0.5f), w * 0.45f, 4), skin, new Vector3(0f, 0f, len * 0.5f), Quaternion.identity);
                    _joints[f, j] = joint;
                    parent = joint;
                    at = new Vector3(0f, 0f, len);
                }
            }
            // The thumb: from the side of the palm, out and forward, two joints.
            var tb = new GameObject("Thumb base").transform;
            tb.SetParent(transform, false);
            tb.localPosition = new Vector3(side * 0.04f, -0.006f, 0.028f) * s;
            tb.localRotation = Quaternion.Euler(18f, side * 42f, side * -30f);
            VhMeshKit.Show(tb, "Segment", meshes.RoundedBox(new Vector3(0.022f, 0.02f, 0.044f) * s, 0.0095f * s, 4), skin, new Vector3(0f, 0f, 0.02f) * s, Quaternion.identity);
            var t1 = new GameObject("Thumb tip").transform;
            t1.SetParent(tb, false);
            t1.localPosition = new Vector3(0f, 0f, 0.04f) * s;
            VhMeshKit.Show(t1, "Segment", meshes.RoundedBox(new Vector3(0.02f, 0.018f, 0.032f) * s, 0.0088f * s, 4), skin, new Vector3(0f, 0f, 0.014f) * s, Quaternion.identity);
            _thumb[0] = tb;
            _thumb[1] = t1;
            // The forearm, back from the wrist (a sleeve over its upper part when there is one).
            float bare = sleeve != null ? Mathf.Min(forearm, sleeveFrom) : forearm;
            VhMeshKit.Show(transform, "Forearm", meshes.Cylinder(0.034f * s, 0.027f * s, bare, 14), skin, new Vector3(0f, -0.002f, -bare), Quaternion.Euler(90f, 0f, 0f));
            if (sleeve != null && forearm > sleeveFrom)
                VhMeshKit.Show(transform, "Sleeve", meshes.Cylinder(0.05f * s, 0.044f * s, forearm - sleeveFrom, 14), sleeve, new Vector3(0f, -0.002f, -forearm), Quaternion.Euler(90f, 0f, 0f));
            Apply(true);
        }

        /// <summary>His fingers move against hers: the index and middle fingers close by <paramref name="amount"/> and open
        /// again over <paramref name="seconds"/>.</summary>
        public void Twitch(float amount, float seconds)
        {
            _twitch = Mathf.Clamp01(amount);
            _twitchLength = Mathf.Max(0.2f, seconds);
            _twitchLeft = _twitchLength;
        }

        void Update() => Apply(false);

        void Apply(bool force)
        {
            if (_joints == null) return;
            float dt = Time.deltaTime;
            float tw = 0f;
            if (_twitchLeft > 0f)
            {
                _twitchLeft -= dt;
                float u = 1f - Mathf.Clamp01(_twitchLeft / _twitchLength);
                tw = _twitch * Mathf.Sin(u * Mathf.PI);
            }
            float thumb = thumbCurl;
            if (stroke > 0f)
            {
                _strokeClock += dt;
                thumb += Mathf.Sin(_strokeClock * 1.8f) * 0.25f * stroke;
            }
            if (!force && Mathf.Abs(curl - _appliedCurl) < 0.002f && Mathf.Abs(thumb - _appliedThumb) < 0.002f && Mathf.Abs(tw - _appliedTwitch) < 0.002f) return;
            _appliedCurl = curl;
            _appliedThumb = thumb;
            _appliedTwitch = tw;
            for (int f = 0; f < 4; f++)
            {
                float c = Mathf.Clamp01(curl + (f < 2 ? tw : tw * 0.35f));
                // The knuckle bends least, the middle joint most; the little finger curls a touch more than the rest.
                float k = 1f + f * 0.06f;
                _joints[f, 0].localRotation = Quaternion.Euler(c * 62f * k, (f - 1.5f) * -2.5f * thumbSide * (1f - c), 0f);
                _joints[f, 1].localRotation = Quaternion.Euler(c * 88f * k, 0f, 0f);
                _joints[f, 2].localRotation = Quaternion.Euler(c * 58f * k, 0f, 0f);
            }
            float t = Mathf.Clamp01(thumb);
            _thumb[0].localRotation = Quaternion.Euler(18f + t * 22f, thumbSide * (42f - t * 26f), thumbSide * -30f);
            _thumb[1].localRotation = Quaternion.Euler(t * 48f, 0f, 0f);
        }
    }
}
