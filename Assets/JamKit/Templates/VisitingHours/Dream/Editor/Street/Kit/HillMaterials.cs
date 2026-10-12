using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace JamKit.DreamEscape.EditorTools
{
    /// <summary>
    /// The hill's materials (docs/redesign/world.md "Materials"): <see cref="Get"/> turns a <see cref="HillMat"/> id into a
    /// URP Lit material (saved in <see cref="MatRoot"/>) from its texture pack in <see cref="TexRoot"/>/(pack)/:
    /// <list type="bullet">
    /// <item>(pack)_color.(jpg|png): the colour map (sRGB; alpha used by cutouts, decals and glass);</item>
    /// <item>(pack)_normal.(jpg|png): the normal map, OpenGL convention (+Y up, as Poly Haven's "nor_gl" and ambientCG's
    /// "NormalGL");</item>
    /// <item>(pack)_ao.(jpg|png), optional: ambient occlusion.</item>
    /// </list>
    /// A pack that isn't there yet falls back to a generated noise texture (never a flat colour). A material the Look agent
    /// provides at <see cref="LookRoot"/>/(id).mat replaces the generated one. Shaders: only URP's own Lit and Unlit.
    /// </summary>
    public static class HillMaterials
    {
        public const string StreetRoot = "Assets/JamKit/Templates/VisitingHours/Dream/Street";
        public const string TexRoot = StreetRoot + "/Textures";
        public const string MatRoot = StreetRoot + "/Materials";
        public const string LookRoot = "Assets/JamKit/Templates/VisitingHours/Look/Materials/Hill";
        const string LitShader = "Universal Render Pipeline/Lit";
        const string UnlitShader = "Universal Render Pipeline/Unlit";

        static readonly Dictionary<string, Material> _cache = new Dictionary<string, Material>();
        static readonly HashSet<string> _missing = new HashSet<string>();

        /// <summary>Forget the cached materials (each scene build starts fresh, so texture changes are picked up).</summary>
        public static void Reset()
        {
            _cache.Clear();
            _missing.Clear();
        }

        /// <summary>The packs that had no colour map at the last build (reported in the build log).</summary>
        public static IEnumerable<string> MissingPacks => _missing;

        public static Material Get(string id)
        {
            if (_cache.TryGetValue(id, out var cached) && cached != null) return cached;
            var look = AssetDatabase.LoadAssetAtPath<Material>(LookRoot + "/" + id + ".mat");
            if (look != null) { _cache[id] = look; return look; }
            var spec = HillMat.Find(id) ?? new HillMat.Spec(id, "concrete", Color.white, 2f);
            DreamEscapeBuilder.EnsureFolder(MatRoot);
            string path = MatRoot + "/Hill_" + id + ".mat";
            bool unlit = spec.kind == HillMat.Kind.Interior;
            var shader = Shader.Find(unlit ? UnlitShader : LitShader);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = "Hill_" + id };
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != shader) mat.shader = shader;
            Configure(mat, spec);
            EditorUtility.SetDirty(mat);
            _cache[id] = mat;
            return mat;
        }

        static void Configure(Material m, HillMat.Spec s)
        {
            var color = Tex(s.pack, "color", false, s.kind != HillMat.Kind.Opaque);
            if (color == null)
            {
                _missing.Add(s.pack);
                color = Fallback();
            }
            var normal = Tex(s.pack, "normal", true, false);
            var ao = Tex(s.pack, "ao", false, false, linear: true);
            float scale = 1f / Mathf.Max(0.05f, s.tile);
            m.SetTexture("_BaseMap", color);
            m.SetTextureScale("_BaseMap", new Vector2(scale, scale));
            m.mainTexture = color;
            var tint = s.tint;
            m.SetColor("_BaseColor", tint);
            m.color = tint;
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", s.smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", s.id == HillMat.Steel ? 0.6f : s.id == HillMat.Copper ? 0.3f : 0f);
            Keyword(m, "_NORMALMAP", normal != null);
            if (m.HasProperty("_BumpMap"))
            {
                m.SetTexture("_BumpMap", normal);
                m.SetFloat("_BumpScale", s.normalScale);
            }
            Keyword(m, "_OCCLUSIONMAP", ao != null);
            if (m.HasProperty("_OcclusionMap")) { m.SetTexture("_OcclusionMap", ao); m.SetFloat("_OcclusionStrength", 0.8f); }

            // Surface: opaque, cutout (two-sided), transparent (glass, water, decals).
            bool transparent = s.kind == HillMat.Kind.Glass || s.kind == HillMat.Kind.Water || s.kind == HillMat.Kind.Decal;
            bool cutout = s.kind == HillMat.Kind.Cutout;
            Set(m, "_Surface", transparent ? 1f : 0f);
            Set(m, "_Blend", 0f);
            Set(m, "_AlphaClip", cutout ? 1f : 0f);
            Set(m, "_Cutoff", 0.42f);
            Set(m, "_Cull", cutout ? 0f : 2f);
            Set(m, "_SrcBlend", transparent ? (float)BlendMode.SrcAlpha : (float)BlendMode.One);
            Set(m, "_DstBlend", transparent ? (float)BlendMode.OneMinusSrcAlpha : (float)BlendMode.Zero);
            Set(m, "_SrcBlendAlpha", transparent ? (float)BlendMode.One : (float)BlendMode.One);
            Set(m, "_DstBlendAlpha", transparent ? (float)BlendMode.OneMinusSrcAlpha : (float)BlendMode.Zero);
            Set(m, "_ZWrite", transparent ? 0f : 1f);
            Set(m, "_BlendModePreserveSpecular", transparent ? 1f : 0f);
            Keyword(m, "_SURFACE_TYPE_TRANSPARENT", transparent);
            Keyword(m, "_ALPHATEST_ON", cutout);
            Keyword(m, "_ALPHAPREMULTIPLY_ON", false);
            m.SetOverrideTag("RenderType", transparent ? "Transparent" : cutout ? "TransparentCutout" : "Opaque");
            m.renderQueue = transparent ? (s.kind == HillMat.Kind.Decal ? (int)RenderQueue.AlphaTest + 30 : (int)RenderQueue.Transparent)
                : cutout ? (int)RenderQueue.AlphaTest : (int)RenderQueue.Geometry;
            m.SetShaderPassEnabled("ShadowCaster", !transparent);
            m.SetShaderPassEnabled("DepthOnly", !transparent);
            if (s.kind == HillMat.Kind.Glass) { var c = tint; c.a = 0.42f; m.SetColor("_BaseColor", c); }
            if (s.kind == HillMat.Kind.Water) { var c = tint; c.a = 0.78f; m.SetColor("_BaseColor", c); }

            // Light: emissive lamps and signs; the rooms behind the glass draw unlit, a touch warm.
            bool emissive = s.kind == HillMat.Kind.Emissive;
            Keyword(m, "_EMISSION", emissive);
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", emissive ? tint * 2.2f : Color.black);
            m.globalIlluminationFlags = emissive ? MaterialGlobalIlluminationFlags.RealtimeEmissive : MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            if (s.kind == HillMat.Kind.Interior) m.SetColor("_BaseColor", new Color(0.95f, 0.95f, 0.95f, 1f));
            m.enableInstancing = true;
        }

        static void Set(Material m, string prop, float v)
        {
            if (m.HasProperty(prop)) m.SetFloat(prop, v);
        }

        static void Keyword(Material m, string k, bool on)
        {
            if (on) m.EnableKeyword(k); else m.DisableKeyword(k);
        }

        /// <summary>A pack's map (color, normal, ao), with its importer put right once (normal maps as normal maps, colour in
        /// sRGB, alpha kept for cutouts, anisotropic filtering for the ground).</summary>
        static Texture2D Tex(string pack, string map, bool normal, bool alpha, bool linear = false)
        {
            foreach (var ext in new[] { ".png", ".jpg", ".jpeg", ".tga" })
            {
                string path = TexRoot + "/" + pack + "/" + pack + "_" + map + ext;
                if (!File.Exists(path)) continue;
                var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp != null)
                {
                    bool dirty = false;
                    if (normal && imp.textureType != TextureImporterType.NormalMap) { imp.textureType = TextureImporterType.NormalMap; dirty = true; }
                    if (!normal && imp.textureType != TextureImporterType.Default) { imp.textureType = TextureImporterType.Default; dirty = true; }
                    bool srgb = !normal && !linear;
                    if (!normal && imp.sRGBTexture != srgb) { imp.sRGBTexture = srgb; dirty = true; }
                    if (alpha && !imp.alphaIsTransparency) { imp.alphaIsTransparency = true; dirty = true; }
                    if (imp.anisoLevel < 4) { imp.anisoLevel = 4; dirty = true; }
                    if (imp.maxTextureSize > 2048) { imp.maxTextureSize = 2048; dirty = true; }
                    if (imp.mipmapEnabled == false) { imp.mipmapEnabled = true; dirty = true; }
                    if (alpha && !imp.mipMapsPreserveCoverage) { imp.mipMapsPreserveCoverage = true; imp.alphaTestReferenceValue = 0.42f; dirty = true; }
                    if (dirty) imp.SaveAndReimport();
                }
                return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            return null;
        }

        /// <summary>A soft value-noise texture (never a flat colour), made once.</summary>
        static Texture2D Fallback()
        {
            string dir = TexRoot + "/gen_noise";
            string path = dir + "/gen_noise_color.png";
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (t != null) return t;
            Directory.CreateDirectory(dir);
            const int n = 256;
            var tex = new Texture2D(n, n, TextureFormat.RGB24, false);
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float v = 0.78f + 0.12f * Mathf.PerlinNoise(x * 0.05f, y * 0.05f) + 0.08f * Mathf.PerlinNoise(x * 0.23f + 40f, y * 0.23f + 9f);
                    px[y * n + x] = new Color(v, v * 0.98f, v * 0.96f);
                }
            tex.SetPixels(px);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
