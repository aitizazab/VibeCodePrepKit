using JamKit.Shell;
using UnityEngine;
using UnityEngine.Rendering;

namespace JamKit.Movement
{
    /// <summary>
    /// Subtle speed lines: faint streaks at the edges of the view that appear above about 12 m/s and
    /// are at their strongest at 20 (alpha 0.3 x Motion effects, x0.4 with "reduce effects"; off with Settings.speedLines).
    /// Never inside the central part of the screen. Contrast-proof (docs/SPEED-FEEL.md V3): each streak has a dark core
    /// and a light rim, so it shows on bright pastel and on dark scenes. Kept a touch subtler than before (0.35 -> 0.3)
    /// because the world-space streaks (<see cref="SpeedStreaks"/>) are now the main speed cue. One mesh built at start,
    /// animated by its shader (JamKit/Movement/SpeedLines), so nothing is rebuilt per frame. Put it on the camera.
    /// </summary>
    [DefaultExecutionOrder(55)]
    public sealed class SpeedLines : MonoBehaviour
    {
        public MovementController body;
        [Tooltip("The speed visuals' hub: its Motion effects x 'reduce effects' share scales the alpha. Empty: Motion effects alone.")]
        public SpeedFeel feel;
        [Tooltip("A material using JamKit/Movement/SpeedLines (made at runtime when empty).")]
        public Material material;
        public int streaks = 56;
        public float fromSpeed = 12f;
        public float fullSpeed = 20f;
        [Tooltip("Alpha at full speed (0.35 before the contrast-proof rework; subtler now that the world streaks carry speed).")]
        [Range(0f, 1f)] public float maxAlpha = 0.3f;
        [Tooltip("Streak width x this (the dark core and light rim need a few pixels).")]
        public float widthScale = 1.5f;

        Camera _cam;
        Transform _quad;
        Material _mat;
        float _level;
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");

        public float Level => _level;

        void Start()
        {
            _cam = GetComponent<Camera>();
            _mat = material;
            if (_mat == null)
            {
                var shader = Shader.Find("JamKit/Movement/SpeedLines");
                if (shader == null) { enabled = false; return; }
                _mat = new Material(shader);
            }
            var go = new GameObject("Speed lines");
            go.layer = 2;
            _quad = go.transform;
            _quad.SetParent(transform, false);
            _quad.localPosition = new Vector3(0f, 0f, 1f);
            go.AddComponent<MeshFilter>().sharedMesh = BuildMesh(streaks, widthScale);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = _mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            _mat.SetFloat(IntensityId, 0f);
        }

        /// <summary>Streaks around the view at 1 m in front of the camera (in units of the half-height of a 68 degree view).</summary>
        static Mesh BuildMesh(int count, float widthScale = 1f)
        {
            var rng = new System.Random(1234);
            var verts = new Vector3[count * 4];
            var uvs = new Vector2[count * 4];
            var cols = new Color[count * 4];
            var tris = new int[count * 6];
            float h = Mathf.Tan(34f * Mathf.Deg2Rad);
            for (int i = 0; i < count; i++)
            {
                float ang = (i + (float)rng.NextDouble() * 0.8f) / count * Mathf.PI * 2f;
                var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                var perp = new Vector2(-dir.y, dir.x);
                // Start outside the central ~45% of the view; wider on the sides of a 16:9 screen.
                float aspectStretch = Mathf.Lerp(1f, 1.6f, Mathf.Abs(dir.x));
                float inner = h * aspectStretch * (0.72f + 0.25f * (float)rng.NextDouble());
                float outer = h * aspectStretch * 1.9f;
                float w = h * (0.006f + 0.006f * (float)rng.NextDouble()) * widthScale;
                float seed = (float)rng.NextDouble();
                Vector2 a = dir * inner, b = dir * outer;
                int v = i * 4;
                verts[v] = a - perp * w; verts[v + 1] = a + perp * w;
                verts[v + 2] = b + perp * w * 2.5f; verts[v + 3] = b - perp * w * 2.5f;
                uvs[v] = new Vector2(0f, -1f); uvs[v + 1] = new Vector2(0f, 1f);
                uvs[v + 2] = new Vector2(1f, 1f); uvs[v + 3] = new Vector2(1f, -1f);
                for (int k = 0; k < 4; k++) cols[v + k] = new Color(1f, 1f, 1f, seed);
                int t = i * 6;
                tris[t] = v; tris[t + 1] = v + 1; tris[t + 2] = v + 2;
                tris[t + 3] = v; tris[t + 4] = v + 2; tris[t + 5] = v + 3;
            }
            var mesh = new Mesh { name = "Speed lines" };
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.colors = cols;
            mesh.triangles = tris;
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100f);
            return mesh;
        }

        void LateUpdate()
        {
            if (_quad == null || body == null || body.Sim == null) return;
            var s = GameRoot.Exists ? GameRoot.Settings : null;
            bool on = s == null || s.speedLines;
            float motion = feel != null ? feel.Levels.linesShare : s != null ? s.motionEffects : 1f;
            float target = on ? MoveMath.SmoothStep(fromSpeed, fullSpeed, body.Speed) * maxAlpha * motion : 0f;
            _level = Mathf.MoveTowards(_level, target, Time.unscaledDeltaTime * 1.5f);
            _mat.SetFloat(IntensityId, _level);
            _quad.gameObject.SetActive(_level > 0.002f);
            // Follow the field of view so the streaks stay at the screen edges.
            float k = _cam != null ? Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / Mathf.Tan(34f * Mathf.Deg2Rad) : 1f;
            _quad.localScale = new Vector3(k, k, 1f);
        }

        void OnDestroy()
        {
            if (material == null && _mat != null) Destroy(_mat);
        }
    }
}
