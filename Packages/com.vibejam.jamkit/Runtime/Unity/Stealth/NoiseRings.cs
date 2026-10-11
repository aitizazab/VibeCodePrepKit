using UnityEngine;

namespace JamKit.Stealth
{
    /// <summary>
    /// The noise you make, made visible (docs/STEALTH.md, answer A; an accessibility aid): each noise of the
    /// <see cref="StealthPlayer"/> draws a ring on the ground that grows to the noise's radius and fades, so you learn
    /// what carries how far (a step, a sprint, a landing, a double jump that reaches past the guards' sight). A guard who
    /// hears it shows it too (the wary colour on its meter). A pool of flat ring meshes on the "JamKit/Stealth/Overlay"
    /// shader; no allocation after Awake.
    /// </summary>
    [DefaultExecutionOrder(55)]
    public sealed class NoiseRings : MonoBehaviour
    {
        public StealthPlayer player;
        public Material material;
        [Tooltip("Off: no rings (the noise still happens).")]
        public bool show = true;
        public float growTime = 0.35f, fadeTime = 0.9f, width = 0.18f;
        [Tooltip("Noises smaller than this aren't drawn (footsteps when walking).")]
        public float minRadius = 2f;
        public int pool = 10;
        const int Segments = 48;

        Transform[] _rings;
        MeshRenderer[] _renderers;
        Mesh[] _meshes;
        Vector3[][] _verts;
        Color[][] _colors;
        float[] _age, _radius;
        Color[] _tint;
        int _next;

        public int Active { get { int n = 0; if (_age != null) for (int i = 0; i < _age.Length; i++) if (_age[i] >= 0f) n++; return n; } }
        float _lastRadius;
        public float LastRadius => _lastRadius;

        void Awake()
        {
            if (player == null) player = GetComponent<StealthPlayer>();
            pool = Mathf.Clamp(pool, 1, 32);
            _rings = new Transform[pool];
            _renderers = new MeshRenderer[pool];
            _meshes = new Mesh[pool];
            _verts = new Vector3[pool][];
            _colors = new Color[pool][];
            _age = new float[pool];
            _radius = new float[pool];
            _tint = new Color[pool];
            var tris = new int[Segments * 6];
            for (int s = 0; s < Segments; s++)
            {
                int a = s * 2, b = ((s + 1) % Segments) * 2;
                tris[s * 6] = a; tris[s * 6 + 1] = b; tris[s * 6 + 2] = a + 1;
                tris[s * 6 + 3] = a + 1; tris[s * 6 + 4] = b; tris[s * 6 + 5] = b + 1;
            }
            for (int i = 0; i < pool; i++)
            {
                var go = new GameObject("Noise ring " + i);
                go.transform.SetParent(null, false);
                var mesh = new Mesh { name = "Noise ring" };
                mesh.MarkDynamic();
                _verts[i] = new Vector3[Segments * 2];
                _colors[i] = new Color[Segments * 2];
                mesh.vertices = _verts[i];
                mesh.colors = _colors[i];
                mesh.triangles = tris;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = material;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                go.SetActive(false);
                _rings[i] = go.transform;
                _renderers[i] = r;
                _meshes[i] = mesh;
                _age[i] = -1f;
            }
        }

        void OnDestroy()
        {
            if (_rings == null) return;
            for (int i = 0; i < _rings.Length; i++)
            {
                if (_rings[i] != null) Destroy(_rings[i].gameObject);
                if (_meshes[i] != null) Destroy(_meshes[i]);
            }
        }

        /// <summary>Draws a ring for a noise (the player's each frame; call it for others too).</summary>
        public void Ring(Vector3 at, float radius, NoiseKind kind)
        {
            if (!show || radius < minRadius || _rings == null) return;
            int i = _next;
            _next = (_next + 1) % _rings.Length;
            _age[i] = 0f;
            _radius[i] = radius;
            _tint[i] = kind == NoiseKind.Other || kind == NoiseKind.Wallrun ? new Color(1f, 0.55f, 0.3f) : kind == NoiseKind.Landing ? new Color(1f, 0.8f, 0.4f) : new Color(0.9f, 0.95f, 1f);
            _rings[i].position = at + Vector3.up * 0.06f;
            _rings[i].gameObject.SetActive(true);
            _lastRadius = radius;
        }

        void Update()
        {
            if (player != null && player.Noise.Any)
            {
                var n = player.Noise;
                Ring(new Vector3(n.position.X, n.position.Y, n.position.Z), n.radius, n.kind);
            }
            if (_rings == null) return;
            float dt = Time.deltaTime;
            for (int i = 0; i < _rings.Length; i++)
            {
                if (_age[i] < 0f) continue;
                _age[i] += dt;
                float life = growTime + fadeTime;
                if (_age[i] >= life || !show) { _age[i] = -1f; _rings[i].gameObject.SetActive(false); continue; }
                float r = _radius[i] * Mathf.Clamp01(_age[i] / Mathf.Max(0.01f, growTime));
                float alpha = _age[i] < growTime ? 0.75f : 0.75f * (1f - (_age[i] - growTime) / Mathf.Max(0.01f, fadeTime));
                var c = _tint[i];
                c.a = alpha;
                var v = _verts[i];
                var col = _colors[i];
                for (int s = 0; s < Segments; s++)
                {
                    float ang = s * Mathf.PI * 2f / Segments;
                    float x = Mathf.Cos(ang), z = Mathf.Sin(ang);
                    v[s * 2] = new Vector3(x * Mathf.Max(0f, r - width), 0f, z * Mathf.Max(0f, r - width));
                    v[s * 2 + 1] = new Vector3(x * r, 0f, z * r);
                    col[s * 2] = c;
                    col[s * 2 + 1] = c;
                }
                _meshes[i].vertices = v;
                _meshes[i].colors = col;
                _meshes[i].RecalculateBounds();
            }
        }
    }
}
