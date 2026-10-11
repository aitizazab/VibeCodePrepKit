using UnityEngine;

namespace JamKit.Stealth
{
    /// <summary>
    /// What a seeker shows the player (docs/STEALTH.md "Readable feedback"): its vision cone on the ground (the bright
    /// cone and the fainter peripheral band, cut short by walls at hip height) and a detection meter above its head that
    /// fills as it sees you, coloured by alert state (white unaware, amber suspicious, orange searching, red found).
    /// While it counts in hide-and-seek the cone is hidden and the meter shows the count. Two small meshes rebuilt in
    /// place each frame (no allocation); <see cref="coneMaterial"/> and <see cref="meterMaterial"/> use the
    /// "JamKit/Stealth/Overlay" shader (vertex colours).
    /// </summary>
    [DefaultExecutionOrder(70)]
    public sealed class SeekerOverlay : MonoBehaviour
    {
        public SeekerAgent seeker;
        public Material coneMaterial, meterMaterial;
        [Tooltip("Rays across the cone (more: smoother edges against walls).")]
        public int segments = 32;
        public float coneHeight = 0.04f, rayHeight = 0.9f;
        public float meterHeight = 2.35f, meterWidth = 0.9f, meterThickness = 0.12f;
        public LayerMask mask = ~(1 << 2);

        public static readonly Color Unaware = new Color(1f, 0.97f, 0.85f);
        public static readonly Color Suspicious = new Color(1f, 0.8f, 0.25f);
        public static readonly Color Searching = new Color(1f, 0.55f, 0.15f);
        public static readonly Color FoundColor = new Color(1f, 0.22f, 0.2f);
        /// <summary>Calm but wary (it heard something a while ago).</summary>
        public static readonly Color WaryColor = new Color(1f, 0.92f, 0.45f);

        Mesh _cone, _meter;
        Transform _coneT, _meterT;
        Vector3[] _coneV, _meterV;
        Color[] _coneC, _meterC;
        readonly RaycastHit[] _hits = new RaycastHit[8];

        public static Color ColorOf(AlertState s)
        {
            switch (s)
            {
                case AlertState.Suspicious: return Suspicious;
                case AlertState.Searching: return Searching;
                case AlertState.Found: return FoundColor;
                default: return Unaware;
            }
        }

        void Awake()
        {
            if (seeker == null) seeker = GetComponent<SeekerAgent>();
            segments = Mathf.Clamp(segments, 4, 128);
            _cone = new Mesh { name = "Vision cone" };
            _cone.MarkDynamic();
            _coneV = new Vector3[segments + 2];
            _coneC = new Color[segments + 2];
            var tris = new int[segments * 3];
            for (int i = 0; i < segments; i++) { tris[i * 3] = 0; tris[i * 3 + 1] = i + 1; tris[i * 3 + 2] = i + 2; }
            _cone.vertices = _coneV;
            _cone.colors = _coneC;
            _cone.triangles = tris;
            _coneT = Child("Vision cone", _cone, coneMaterial);

            _meter = new Mesh { name = "Detection meter" };
            _meter.MarkDynamic();
            _meterV = new Vector3[8];
            _meterC = new Color[8];
            _meter.vertices = _meterV;
            _meter.colors = _meterC;
            _meter.triangles = new[] { 0, 1, 2, 0, 2, 3, 4, 5, 6, 4, 6, 7 };
            _meterT = Child("Detection meter", _meter, meterMaterial);
        }

        Transform Child(string n, Mesh mesh, Material mat)
        {
            var go = new GameObject(n);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go.transform;
        }

        void OnDestroy()
        {
            if (_cone != null) Destroy(_cone);
            if (_meter != null) Destroy(_meter);
        }

        void LateUpdate()
        {
            if (seeker == null || seeker.Brain == null) return;
            var b = seeker.Brain;
            var def = seeker.Def;
            var color = b.State == AlertState.Unaware && b.Wary ? WaryColor : ColorOf(b.State);
            bool counting = b.Counting;

            // The cone, in world space under an unrotated child.
            _coneT.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            _coneT.gameObject.SetActive(!counting);
            if (!counting)
            {
                var origin = transform.position + Vector3.up * coneHeight;
                var eye = transform.position + Vector3.up * rayHeight;
                _coneV[0] = origin;
                _coneC[0] = new Color(color.r, color.g, color.b, 0.4f);
                float widen = b.Wary ? def.waryConeScale : 1f; // a wary guard looks wider
                float half = Mathf.Min(178f, def.peripheralHalfAngle * widen);
                for (int i = 0; i <= segments; i++)
                {
                    float a = -half + 2f * half * i / segments;
                    var dir = Quaternion.Euler(0f, b.Yaw + a, 0f) * Vector3.forward;
                    float len = def.sightRange;
                    int n = Physics.RaycastNonAlloc(eye, dir, _hits, len, mask, QueryTriggerInteraction.Ignore);
                    for (int k = 0; k < n; k++)
                    {
                        var t = _hits[k].collider.transform;
                        if (t == transform || t.IsChildOf(transform)) continue;
                        if (StealthPlayer.Current != null && t.IsChildOf(StealthPlayer.Current.transform)) continue;
                        if (_hits[k].distance < len) len = _hits[k].distance;
                    }
                    bool inCone = Mathf.Abs(a) <= def.coneHalfAngle * widen;
                    _coneV[i + 1] = origin + dir * len;
                    _coneC[i + 1] = new Color(color.r, color.g, color.b, inCone ? 0.16f : 0.06f);
                }
                _cone.vertices = _coneV;
                _cone.colors = _coneC;
                _cone.RecalculateBounds();
            }

            // The meter above the head, facing the camera.
            float fill = counting ? Mathf.Clamp01(b.CountLeft / Mathf.Max(0.1f, def.countTime)) : b.Meter;
            bool show = counting || fill > 0.01f || b.State != AlertState.Unaware || b.Wary;
            _meterT.gameObject.SetActive(show);
            if (!show) return;
            var cam = Camera.main;
            _meterT.position = transform.position + Vector3.up * meterHeight;
            if (cam != null) _meterT.rotation = cam.transform.rotation;
            float w = meterWidth * 0.5f, h = meterThickness * 0.5f, x1 = -w + meterWidth * Mathf.Clamp01(fill);
            var back = new Color(0.08f, 0.08f, 0.12f, 0.55f);
            var front = counting ? new Color(0.75f, 0.85f, 1f, 0.9f) : new Color(color.r, color.g, color.b, 0.95f);
            Quad(0, -w - 0.03f, w + 0.03f, h + 0.03f, back);
            Quad(4, -w, x1, h, front);
            _meter.vertices = _meterV;
            _meter.colors = _meterC;
            _meter.RecalculateBounds();
        }

        void Quad(int at, float x0, float x1, float h, Color c)
        {
            _meterV[at] = new Vector3(x0, -h, 0f);
            _meterV[at + 1] = new Vector3(x0, h, 0f);
            _meterV[at + 2] = new Vector3(x1, h, 0f);
            _meterV[at + 3] = new Vector3(x1, -h, 0f);
            for (int i = 0; i < 4; i++) _meterC[at + i] = c;
        }
    }
}
