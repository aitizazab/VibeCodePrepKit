using System.Collections.Generic;
using UnityEngine;

namespace JamKit.SkylineRepo
{
    /// <summary>
    /// The Breacher's PBR surfaces (judge round 3, A14 / C: "flat, untextured colour next to the textured Writ"), made in code
    /// at scene start so they need no texture assets, import settings or metas:
    /// <list type="bullet">
    /// <item>the firm's orange (and the cell's and plates' hazard yellow, and the stripes' black) as painted metal: a
    /// mottled coat, chipped and scuffed through to bright bare steel along every edge of every part (the model's UVs span
    /// each flat face 0..1 and each cylinder's length 0..1, so the texture's border is the part's edge), a few scratches
    /// and chips across the faces, and grime; the chips are recessed in the normal map;</item>
    /// <item>dark gunmetal for the barrel, shroud, ribs, sights and rails: brushed streaks, edges worn bright;</item>
    /// <item>a dark stippled polymer for the grip, pump and standoff.</item>
    /// </list>
    /// The colours are the model's own (the orange is the deep, red one that holds its hue in the sun). Every material is
    /// URP Lit with a base map and a normal map (_NORMALMAP is carried by the arena's own library materials, so the variant
    /// is in the build). <see cref="Apply"/> swaps copies onto the gun's renderers; <see cref="Dispose"/> frees them.
    /// </summary>
    public sealed class SkylineBreacherLook
    {
        const int Size = 256;

        // sRGB colours of the model's materials (build_breacher.py): the paint, the bare steel under it
        static readonly Color Orange = new Color(0.854f, 0.476f, 0.155f);
        static readonly Color Yellow = new Color(1f, 0.9f, 0.35f);
        static readonly Color Black = new Color(0.09f, 0.09f, 0.1f);
        static readonly Color Bare = new Color(0.62f, 0.62f, 0.64f);
        static readonly Color Gunmetal = new Color(0.27f, 0.28f, 0.3f);
        static readonly Color Polymer = new Color(0.12f, 0.12f, 0.13f);

        readonly List<Object> _owned = new List<Object>();
        readonly Dictionary<string, Material> _made = new Dictionary<string, Material>();
        float[] _chip, _scratch, _grime;
        Texture2D _paintNormal, _steelNormal, _polyNormal;

        /// <summary>Puts textured copies of the gun's Breacher_* materials on every renderer under <paramref name="gunRoot"/>
        /// (the glow window and the seams are left as they are).</summary>
        public void Apply(Transform gunRoot)
        {
            if (gunRoot == null) return;
            foreach (var r in gunRoot.GetComponentsInChildren<Renderer>(true))
            {
                var shared = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < shared.Length; i++)
                {
                    var copy = For(shared[i]);
                    if (copy != null) { shared[i] = copy; changed = true; }
                }
                if (changed) r.sharedMaterials = shared;
            }
        }

        public void Dispose()
        {
            for (int i = 0; i < _owned.Count; i++) if (_owned[i] != null) Object.Destroy(_owned[i]);
            _owned.Clear();
            _made.Clear();
        }

        Material For(Material original)
        {
            if (original == null || !original.name.StartsWith("Breacher_", System.StringComparison.Ordinal)) return null;
            string key = original.name;
            int space = key.IndexOf(' ');
            if (space > 0) key = key.Substring(0, space);
            if (_made.TryGetValue(key, out var have)) return have;
            Texture2D albedo, normal;
            float metallic, smooth;
            switch (key)
            {
                case "Breacher_Orange": albedo = Paint(Orange); normal = PaintNormal(); metallic = 0.25f; smooth = 0.42f; break;
                case "Breacher_Cell": albedo = Paint(Yellow); normal = PaintNormal(); metallic = 0.25f; smooth = 0.45f; break;
                case "Breacher_Stripe": albedo = Paint(Black); normal = PaintNormal(); metallic = 0.2f; smooth = 0.35f; break;
                case "Breacher_Steel": albedo = Steel(); normal = SteelNormal(); metallic = 0.85f; smooth = 0.55f; break;
                case "Breacher_Dark": albedo = Poly(); normal = PolyNormal(); metallic = 0f; smooth = 0.28f; break;
                default: return null;
            }
            var m = new Material(original) { name = key + " (look)" };
            _owned.Add(m);
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", albedo);
            m.mainTexture = albedo;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
            m.color = Color.white;
            if (m.HasProperty("_BumpMap"))
            {
                m.SetTexture("_BumpMap", normal);
                if (m.HasProperty("_BumpScale")) m.SetFloat("_BumpScale", 1f);
                m.EnableKeyword("_NORMALMAP");
            }
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            _made[key] = m;
            return m;
        }

        // ---------------------------------------------------------------- the shared wear masks

        /// <summary>A smooth value noise (0..1) at a cell size in pixels, seeded.</summary>
        static float Noise(float x, float y, float cell, int seed)
        {
            x /= cell;
            y /= cell;
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Hash(x0, y0, seed), b = Hash(x0 + 1, y0, seed), c = Hash(x0, y0 + 1, seed), d = Hash(x0 + 1, y0 + 1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 362437);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }

        static float Fbm(float x, float y, float cell, int seed) =>
            0.55f * Noise(x, y, cell, seed) + 0.3f * Noise(x, y, cell * 0.5f, seed + 7) + 0.15f * Noise(x, y, cell * 0.25f, seed + 13);

        /// <summary>Chips (1 where the paint is gone), scratches and grime, all 0..1, shared by every painted part.</summary>
        void Masks()
        {
            if (_chip != null) return;
            _chip = new float[Size * Size];
            _scratch = new float[Size * Size];
            _grime = new float[Size * Size];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    int i = y * Size + x;
                    float u = (x + 0.5f) / Size, v = (y + 0.5f) / Size;
                    float edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));        // 0 at the part's edge
                    float n = Fbm(x, y, 22f, 11);
                    // worn along the edges (a broken band about 6% deep), plus a few chips on the faces
                    float wear = Mathf.Clamp01((0.075f - edge) / 0.075f) * 1.25f + (n - 0.5f) * 0.9f;
                    float face = Fbm(x, y, 9f, 31) > 0.78f ? 1f : 0f;
                    _chip[i] = Mathf.Clamp01((wear - 0.55f) * 6f) + face * 0.9f;
                    _chip[i] = Mathf.Clamp01(_chip[i]);
                    _grime[i] = Mathf.Clamp01(Fbm(x, y, 64f, 51) * 0.8f + Mathf.Clamp01((0.12f - edge) / 0.12f) * 0.35f);
                }
            // scratches: short straight strokes across the faces
            var rng = new System.Random(19);
            for (int k = 0; k < 26; k++)
            {
                float x0 = rng.Next(Size), y0 = rng.Next(Size);
                float ang = (float)(rng.NextDouble() * Mathf.PI);
                float len = 12f + (float)rng.NextDouble() * 45f;
                float dx = Mathf.Cos(ang), dy = Mathf.Sin(ang);
                for (float t = 0f; t < len; t += 0.5f)
                {
                    int px = Mathf.RoundToInt(x0 + dx * t), py = Mathf.RoundToInt(y0 + dy * t);
                    if (px < 0 || py < 0 || px >= Size || py >= Size) break;
                    float strength = 0.6f + 0.4f * Mathf.Sin(t / len * Mathf.PI);
                    int i = py * Size + px;
                    _scratch[i] = Mathf.Max(_scratch[i], strength);
                }
            }
        }

        Texture2D NewTex(string name, bool linear)
        {
            var t = new Texture2D(Size, Size, TextureFormat.RGBA32, true, linear)
            {
                name = "Breacher " + name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4,
            };
            _owned.Add(t);
            return t;
        }

        static Color32 C(Color c) => new Color32((byte)(Mathf.Clamp01(c.r) * 255f), (byte)(Mathf.Clamp01(c.g) * 255f), (byte)(Mathf.Clamp01(c.b) * 255f), 255);

        /// <summary>Painted metal: the coat (slightly mottled, darker with grime), a dark rim where the paint breaks, bright
        /// bare steel in the chips, and lighter scratches.</summary>
        Texture2D Paint(Color paint)
        {
            Masks();
            var tex = NewTex("paint", false);
            var px = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    int i = y * Size + x;
                    float mottle = 0.93f + 0.12f * Fbm(x, y, 14f, 71);
                    Color c = paint * mottle * (1f - 0.18f * _grime[i]);
                    float chip = _chip[i];
                    float rim = Mathf.Clamp01(chip * (1f - chip) * 4f);                         // the broken paint's edge
                    Color bare = Bare * (0.85f + 0.25f * Noise(x, y, 3f, 91));
                    c = Color.Lerp(c, bare, chip) * (1f - 0.35f * rim);
                    c = Color.Lerp(c, Color.Lerp(c, Bare, 0.55f), _scratch[i] * 0.8f);
                    px[i] = C(c);
                }
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return tex;
        }

        Texture2D PaintNormal()
        {
            if (_paintNormal != null) return _paintNormal;
            Masks();
            var h = new float[Size * Size];
            for (int i = 0; i < h.Length; i++) h[i] = 1f - 0.6f * _chip[i] - 0.25f * _scratch[i];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++) h[y * Size + x] += 0.04f * Noise(x, y, 4f, 17);   // the coat's orange peel
            return _paintNormal = Normal(h, 3.5f, "paint normal");
        }

        /// <summary>Dark gunmetal: brushed streaks along the part, edges worn bright.</summary>
        Texture2D Steel()
        {
            Masks();
            var tex = NewTex("gunmetal", false);
            var px = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    int i = y * Size + x;
                    float brush = 0.85f + 0.3f * Noise(x, y * 0.08f, 2.5f, 41);              // long streaks across x
                    Color c = Gunmetal * brush * (1f - 0.15f * _grime[i]);
                    c = Color.Lerp(c, Bare, Mathf.Clamp01(_chip[i] * 0.7f + _scratch[i] * 0.4f));
                    px[i] = C(c);
                }
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return tex;
        }

        Texture2D SteelNormal()
        {
            if (_steelNormal != null) return _steelNormal;
            Masks();
            var h = new float[Size * Size];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    int i = y * Size + x;
                    h[i] = 0.08f * Noise(x, y * 0.08f, 2.5f, 41) - 0.2f * _scratch[i] - 0.15f * _chip[i];
                }
            return _steelNormal = Normal(h, 3f, "gunmetal normal");
        }

        /// <summary>Dark polymer: a fine stipple, grime in the low spots, edges scuffed lighter.</summary>
        Texture2D Poly()
        {
            Masks();
            var tex = NewTex("polymer", false);
            var px = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    int i = y * Size + x;
                    float stip = Noise(x, y, 1.6f, 61);
                    Color c = Polymer * (0.85f + 0.3f * stip) * (1f - 0.2f * _grime[i]);
                    c = Color.Lerp(c, Polymer * 2.2f, Mathf.Clamp01(_chip[i] * 0.5f));
                    px[i] = C(c);
                }
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return tex;
        }

        Texture2D PolyNormal()
        {
            if (_polyNormal != null) return _polyNormal;
            var h = new float[Size * Size];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++) h[y * Size + x] = 0.35f * Noise(x, y, 1.6f, 61);
            return _polyNormal = Normal(h, 2.5f, "polymer normal");
        }

        /// <summary>A tangent-space normal map from a height field (RGB = normal × 0.5 + 0.5, alpha 1; URP's unpack reads RG or
        /// AG from it).</summary>
        Texture2D Normal(float[] h, float strength, string name)
        {
            var tex = NewTex(name, true);
            var px = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    int xl = Mathf.Max(0, x - 1), xr = Mathf.Min(Size - 1, x + 1), yd = Mathf.Max(0, y - 1), yu = Mathf.Min(Size - 1, y + 1);
                    float dx = (h[y * Size + xr] - h[y * Size + xl]) * strength;
                    float dy = (h[yu * Size + x] - h[yd * Size + x]) * strength;
                    var n = new Vector3(-dx, -dy, 1f).normalized;
                    px[y * Size + x] = new Color32((byte)((n.x * 0.5f + 0.5f) * 255f), (byte)((n.y * 0.5f + 0.5f) * 255f), (byte)((n.z * 0.5f + 0.5f) * 255f), 255);
                }
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return tex;
        }
    }
}
