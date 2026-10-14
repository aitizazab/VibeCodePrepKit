using System.Collections.Generic;
using UnityEngine;

namespace JamKit.SkylineRepo
{
    /// <summary>A shot's tracer: a thin bright bar from the muzzle to where the shot ended, gone in a few frames. Pooled
    /// renderer-only cubes (no colliders made or destroyed) with one shared lit material per colour (no per-tracer material
    /// copies, no shader lookups), filled up front by <see cref="Prewarm"/>. The Writ's tracer is the ink stroke
    /// (<see cref="SkylineInkTracer"/>), not this bar.</summary>
    public sealed class SkylineTracer : MonoBehaviour
    {
        const float Life = 0.06f, Thickness = 0.012f;
        const int PoolSize = 24;

        static readonly List<SkylineTracer> _pool = new List<SkylineTracer>();
        static int _next;

        Renderer _renderer;
        float _left;

        /// <summary>Draws one tracer from <paramref name="from"/> to <paramref name="to"/>.</summary>
        public static void Spawn(Vector3 from, Vector3 to, Color color)
        {
            Vector3 d = to - from;
            float len = d.magnitude;
            if (!(len > 0.05f)) return;
            var t = Take();
            t.transform.SetPositionAndRotation(from + d * 0.5f, Quaternion.LookRotation(d / len));
            t.transform.localScale = new Vector3(Thickness, Thickness, len);
            t._renderer.sharedMaterial = SkylineBotLook.Lit(color);
            t._left = Life;
            t.gameObject.SetActive(true);
        }

        /// <summary>Fills the pool now (at scene start), so no tracer is ever created mid-fight.</summary>
        public static void Prewarm()
        {
            Purge();
            while (_pool.Count < PoolSize) Make().gameObject.SetActive(false);
            SkylineInkTracer.Prewarm();
        }

        static void Purge()
        {
            for (int i = _pool.Count - 1; i >= 0; i--) if (_pool[i] == null) _pool.RemoveAt(i);   // a scene change destroyed it
        }

        static SkylineTracer Make()
        {
            var go = SkylineBotLook.Visual(PrimitiveType.Cube, "Tracer", null, SkylineBotLook.Lit(Color.white), false);
            var t = go.AddComponent<SkylineTracer>();
            t._renderer = go.GetComponent<Renderer>();
            _pool.Add(t);
            return t;
        }

        static SkylineTracer Take()
        {
            Purge();
            if (_pool.Count < PoolSize) return Make();
            _next = (_next + 1) % _pool.Count;
            return _pool[_next];
        }

        void Update()
        {
            _left -= Time.deltaTime;
            if (_left <= 0f) gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// The Writ's tracer: ink from a quill (the creator, 2026-10-14: the old line looked like Chamber's Headhunter, a thin
    /// bright golden line). One stroke of dark indigo ink (#2A1F5C) drawn from the muzzle outward in 0.04 s like a pen
    /// stroke: thick at the start and tapering to a point, its edge slightly irregular (a brush), a wet lighter sheen along
    /// its upper edge that dries first, a near-black ink outline under it (the style's dark outline, docs/SKYLINE-STYLE.md),
    /// and a few ink droplets that flick off along it, fall a little and fade. The whole stroke dries and fades in
    /// <see cref="Life"/> seconds. Never bright, never gold: it must read against the cream scenery and the sky.
    /// <para>One mesh a stroke, rebuilt each frame in world space as a ribbon facing the camera (about 170 vertices; no
    /// allocation). The mesh is pooled and the material is Sprites/Default (in the always-included shaders, so in every
    /// build: unlit, alpha-blended, vertex-coloured, no depth write), with a soft round texture made here for the droplets;
    /// no keyword is turned on at run time.</para>
    /// </summary>
    public sealed class SkylineInkTracer : MonoBehaviour
    {
        public const float Life = 0.35f, DrawSeconds = 0.04f;
        const int Segments = 20, Droplets = 5, PoolSize = 8;
        /// <summary>Half the stroke's width at the muzzle (m); the width tapers to nothing at the far end.</summary>
        const float StartHalfWidth = 0.011f;
        /// <summary>The thinnest the stroke gets on screen: half a width of about 1.1 px at 1080p (radians per pixel), so a far
        /// stroke stays a line, not a sub-pixel.</summary>
        const float PixelHalfWidth = 0.0011f;
        /// <summary>The outline's margin beyond the ink (m), about 1.3 px at arm's length; it grows with distance.</summary>
        const float OutlineMargin = 0.0013f;

        /// <summary>The ink, its wet sheen and its outline, as the style sheet gives them (sRGB). The vertex colours are linear.</summary>
        public static readonly Color InkColor = new Color32(0x2A, 0x1F, 0x5C, 255);
        public static readonly Color SheenColor = new Color32(0x5B, 0x4B, 0xB4, 255);
        public static readonly Color OutlineColor = new Color32(0x0B, 0x08, 0x17, 255);

        static readonly List<SkylineInkTracer> _pool = new List<SkylineInkTracer>();
        static int _next;
        static Material _material;
        static Texture2D _dot;

        // vertex layout: the outline strip (2 a ring), the ink strip (4 a ring: dark, dark, dark, sheen), the droplets (2 quads each)
        const int OutlineBase = 0;
        const int InkBase = OutlineBase + 2 * (Segments + 1);
        const int DropBase = InkBase + 4 * (Segments + 1);
        const int VertexCount = DropBase + 8 * Droplets;

        Mesh _mesh;
        Vector3[] _v;
        Color[] _c;
        Vector2[] _uv;
        Color _ink, _sheen, _outline;

        float _age;
        Vector3 _from, _dir, _side, _camPos;
        float _len;
        readonly float[] _jl = new float[Segments + 1], _jr = new float[Segments + 1];
        readonly Vector3[] _dropOrigin = new Vector3[Droplets], _dropVel = new Vector3[Droplets];
        readonly float[] _dropDelay = new float[Droplets], _dropLife = new float[Droplets], _dropSize = new float[Droplets];
        float _gravity;

        /// <summary>Draws one ink stroke from <paramref name="from"/> (the muzzle) to <paramref name="to"/> (where the shot ended).</summary>
        public static void Spawn(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            float len = d.magnitude;
            if (!(len > 0.3f)) return;
            var t = Take();
            t.Begin(from, d / len, len);
            t.gameObject.SetActive(true);
        }

        /// <summary>Fills the pool now (at scene start), so no stroke, mesh or texture is made mid-fight.</summary>
        public static void Prewarm()
        {
            Purge();
            EnsureMaterial();
            while (_pool.Count < PoolSize) Make().gameObject.SetActive(false);
        }

        static void Purge()
        {
            for (int i = _pool.Count - 1; i >= 0; i--) if (_pool[i] == null) _pool.RemoveAt(i);
        }

        static void EnsureMaterial()
        {
            if (_material != null) return;
            if (_dot == null)
            {
                // a soft round dot, white with alpha: the droplets sample all of it, the stroke only its solid middle
                const int n = 32;
                _dot = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "Ink dot", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                        float r = Mathf.Sqrt(u * u + v * v);
                        float a = Mathf.Clamp01((0.92f - r) / 0.18f);
                        px[y * n + x] = new Color(1f, 1f, 1f, a);
                    }
                _dot.SetPixels32(px);
                _dot.Apply(false, true);
            }
            var sh = Shader.Find("Sprites/Default");
            _material = sh != null ? new Material(sh) : new Material(SkylineBotLook.Glow(InkColor));
            _material.name = "Ink tracer";
            _material.mainTexture = _dot;
        }

        static SkylineInkTracer Make()
        {
            EnsureMaterial();
            var go = new GameObject("Ink tracer");
            var mf = go.AddComponent<MeshFilter>();
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = _material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            var t = go.AddComponent<SkylineInkTracer>();
            t.Init(mf);
            _pool.Add(t);
            return t;
        }

        static SkylineInkTracer Take()
        {
            Purge();
            if (_pool.Count < PoolSize) return Make();
            _next = (_next + 1) % _pool.Count;
            return _pool[_next];
        }

        void Init(MeshFilter mf)
        {
            _mesh = new Mesh { name = "Ink tracer" };
            _mesh.MarkDynamic();
            _v = new Vector3[VertexCount];
            _c = new Color[VertexCount];
            _uv = new Vector2[VertexCount];
            var tri = new List<int>();
            // outline strip, then the ink strip (its three bands), then each droplet's outline quad and ink quad: drawn in index order
            for (int i = 0; i < Segments; i++)
            {
                int a = OutlineBase + 2 * i;
                Quad(tri, a, a + 1, a + 2, a + 3);
            }
            for (int i = 0; i < Segments; i++)
                for (int k = 0; k < 3; k++)
                {
                    int a = InkBase + 4 * i + k;
                    Quad(tri, a, a + 1, a + 4, a + 5);
                }
            for (int d = 0; d < Droplets; d++)
            {
                Quad(tri, DropBase + 8 * d, DropBase + 8 * d + 1, DropBase + 8 * d + 2, DropBase + 8 * d + 3);
                Quad(tri, DropBase + 8 * d + 4, DropBase + 8 * d + 5, DropBase + 8 * d + 6, DropBase + 8 * d + 7);
            }
            // the stroke samples the dot's solid middle; the droplets' quads sample all of it
            for (int i = 0; i < DropBase; i++) _uv[i] = new Vector2(0.5f, 0.5f);
            for (int d = 0; d < Droplets; d++)
                for (int q = 0; q < 2; q++)
                {
                    int b = DropBase + 8 * d + 4 * q;
                    _uv[b] = new Vector2(0f, 0f);
                    _uv[b + 1] = new Vector2(1f, 0f);
                    _uv[b + 2] = new Vector2(0f, 1f);
                    _uv[b + 3] = new Vector2(1f, 1f);
                }
            _mesh.vertices = _v;
            _mesh.uv = _uv;
            _mesh.colors = _c;
            _mesh.SetTriangles(tri, 0);
            mf.sharedMesh = _mesh;
            _ink = InkColor.linear;
            _sheen = SheenColor.linear;
            _outline = OutlineColor.linear;
        }

        /// <summary>A quad on four corners laid out (0 1 / 2 3): two triangles.</summary>
        static void Quad(List<int> tri, int a, int b, int c, int d)
        {
            tri.Add(a); tri.Add(c); tri.Add(b);
            tri.Add(b); tri.Add(c); tri.Add(d);
        }

        void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
        }

        void Begin(Vector3 from, Vector3 dir, float len)
        {
            _age = 0f;
            _from = from;
            _dir = dir;
            _len = len;
            var cam = Camera.main;
            _camPos = cam != null ? cam.transform.position : from - dir * 0.5f;
            Vector3 camUp = cam != null ? cam.transform.up : Vector3.up;
            _side = FaceSide(_from + dir * (len * 0.5f));
            if (Vector3.Dot(_side, camUp) < 0f) _side = -_side;   // the wet sheen sits on the upper edge (light from above)

            // a brush's irregular edge: each side wanders on its own, smoothed so it reads as bristles, not noise
            float l = 1f, r = 1f;
            for (int i = 0; i <= Segments; i++)
            {
                l = 0.5f * l + 0.5f * Random.Range(0.78f, 1.22f);
                r = 0.5f * r + 0.5f * Random.Range(0.78f, 1.22f);
                _jl[i] = l;
                _jr[i] = r;
            }

            // a few droplets flick off along the first part of the stroke
            float f = Mathf.Max(1f, Vector3.Distance(_camPos, from + dir * (len * 0.3f)) / 4f);
            _gravity = 3.5f * f;
            for (int d = 0; d < Droplets; d++)
            {
                float s = Random.Range(0.06f, 0.6f);
                Vector3 p = from + dir * (len * s);
                float dist = Vector3.Distance(_camPos, p);
                float sign = Random.value < 0.5f ? -1f : 1f;
                _dropOrigin[d] = p + _side * (HalfWidth(s, dist) * Random.Range(-0.8f, 0.8f));
                _dropVel[d] = (_side * (sign * Random.Range(0.5f, 1.2f)) + dir * Random.Range(0.4f, 1.4f) + Vector3.up * Random.Range(0.2f, 0.6f)) * Mathf.Pow(f, 0.7f);
                _dropDelay[d] = s * DrawSeconds + Random.Range(0f, 0.03f);
                _dropLife[d] = Random.Range(0.2f, 0.3f);
                _dropSize[d] = Mathf.Max(0.005f, dist * 0.0022f) * Random.Range(0.7f, 1.3f);
            }
            Rebuild();
        }

        /// <summary>The ribbon's width direction at <paramref name="at"/>: across both the stroke and the line of sight, so the
        /// ribbon faces the camera.</summary>
        Vector3 FaceSide(Vector3 at)
        {
            Vector3 s = Vector3.Cross(_dir, _camPos - at);
            if (s.sqrMagnitude < 1e-8f)
            {
                var cam = Camera.main;
                s = cam != null ? cam.transform.right : Vector3.right;
            }
            return s.normalized;
        }

        /// <summary>Half the ink's width at <paramref name="s"/> (0 at the muzzle, 1 at the far end) for a point
        /// <paramref name="dist"/> metres from the camera: thick at the start, tapering to a point, never thinner than a pixel
        /// until its last few percent.</summary>
        static float HalfWidth(float s, float dist)
        {
            float taper = Mathf.Pow(1f - s, 0.85f);
            float blunt = Mathf.Lerp(0.7f, 1f, Mathf.Clamp01(s / 0.06f));    // the nib's blunt start
            return Mathf.Max(StartHalfWidth * taper * blunt, dist * PixelHalfWidth * Mathf.Clamp01((1f - s) / 0.08f));
        }

        void LateUpdate()
        {
            _age += Time.deltaTime;
            if (_age >= Life) { gameObject.SetActive(false); return; }
            Rebuild();
        }

        void Rebuild()
        {
            var cam = Camera.main;
            Vector3 camRight = cam != null ? cam.transform.right : Vector3.right;
            Vector3 camUp = cam != null ? cam.transform.up : Vector3.up;
            if (cam != null) _camPos = cam.transform.position;
            float u = _age / Life;
            float draw = Mathf.Clamp01(_age / DrawSeconds);                 // the stroke is drawn out from the muzzle
            float fade = Mathf.Pow(1f - u, 1.4f);                           // drying: it fades whole
            float wet = Mathf.Clamp01(1f - u * 1.7f);                       // the sheen dries first
            float shrink = 1f - 0.3f * u;

            Color ink = _ink; ink.a = fade;
            Color outline = _outline; outline.a = fade;
            Color sheen = Color.Lerp(_ink, _sheen, wet); sheen.a = fade;

            for (int i = 0; i <= Segments; i++)
            {
                float s = (float)i / Segments;
                Vector3 p = _from + _dir * (_len * s * draw);
                float dist = Vector3.Distance(_camPos, p);
                float hw = HalfWidth(s, dist) * shrink;
                float hl = hw * _jl[i], hr = hw * _jr[i];
                float margin = OutlineMargin * Mathf.Max(1f, dist * 0.35f) * Mathf.Clamp01((1f - s) / 0.05f);
                float tailFade = 1f - 0.2f * s;
                int o = OutlineBase + 2 * i, k = InkBase + 4 * i;
                _v[o] = p - _side * (hl + margin);
                _v[o + 1] = p + _side * (hr + margin);
                _v[k] = p - _side * hl;
                _v[k + 1] = p - _side * (hl * 0.55f);
                _v[k + 2] = p + _side * (hr * 0.4f);
                _v[k + 3] = p + _side * hr;
                var oc = outline; oc.a *= tailFade;
                _c[o] = oc; _c[o + 1] = oc;
                var ic = ink; ic.a *= tailFade;
                var sc = sheen; sc.a *= tailFade;
                _c[k] = ic; _c[k + 1] = ic; _c[k + 2] = ic; _c[k + 3] = sc;
            }

            for (int d = 0; d < Droplets; d++)
            {
                float a = _age - _dropDelay[d];
                int b = DropBase + 8 * d;
                if (a < 0f || a >= _dropLife[d])
                {
                    for (int q = 0; q < 8; q++) { _v[b + q] = _from; _c[b + q] = Color.clear; }
                    continue;
                }
                float life01 = a / _dropLife[d];
                Vector3 p = _dropOrigin[d] + _dropVel[d] * a + Vector3.down * (0.5f * _gravity * a * a);
                float size = _dropSize[d] * (1f - 0.4f * life01);
                float da = Mathf.Pow(1f - life01, 1.2f) * (0.6f + 0.4f * fade);
                Color dc = _ink; dc.a = da;
                Color doc = _outline; doc.a = da;
                float os = size * 1.35f;
                _v[b] = p - camRight * os - camUp * os; _v[b + 1] = p + camRight * os - camUp * os;
                _v[b + 2] = p - camRight * os + camUp * os; _v[b + 3] = p + camRight * os + camUp * os;
                _v[b + 4] = p - camRight * size - camUp * size; _v[b + 5] = p + camRight * size - camUp * size;
                _v[b + 6] = p - camRight * size + camUp * size; _v[b + 7] = p + camRight * size + camUp * size;
                for (int q = 0; q < 4; q++) { _c[b + q] = doc; _c[b + 4 + q] = dc; }
            }
            _mesh.vertices = _v;
            _mesh.colors = _c;
            _mesh.RecalculateBounds();
        }
    }
}
