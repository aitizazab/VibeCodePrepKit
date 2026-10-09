using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace JamKit.VisitingHours.EditorTools
{
    /// <summary>
    /// The slice's art, by the file names in docs/SLICE-VISITING-HOURS.md. Real art lives in
    /// Templates/VisitingHours/Art (another agent and the creator own those images: this code never
    /// writes there, it only sets import settings). Until a file exists, a neutral stand-in is drawn
    /// into Templates/VisitingHours/Generated/fallback_*.png so the scene builds, and the build logs
    /// "JAMKIT_VH art missing: ...". The next Rebuild Shell Scenes picks up the real file.
    /// </summary>
    public static class VisitingHoursArt
    {
        public const string ArtDir = VisitingHoursBuilder.Root + "/Art";
        public const string GeneratedDir = VisitingHoursBuilder.Root + "/Generated";

        public enum Kind { Portrait, Billboard, Prop, Flat, Tile, Sky, Screen }

        /// <summary>Every picture the slice uses, with how it's imported and drawn.</summary>
        public static readonly (string name, Kind kind)[] All =
        {
            ("portrait_walt_neutral", Kind.Portrait), ("portrait_walt_sad", Kind.Portrait), ("portrait_walt_firm", Kind.Portrait), ("portrait_mags", Kind.Portrait),
            ("billboard_walt", Kind.Billboard), ("billboard_neighbor_roses", Kind.Billboard), ("billboard_neighbor_grill", Kind.Billboard),
            ("billboard_dog", Kind.Billboard), ("billboard_elias_bed", Kind.Billboard),
            ("prop_family_photo", Kind.Prop), ("prop_chart", Kind.Prop), ("prop_effects_bag", Kind.Prop), ("prop_keyring", Kind.Prop),
            ("prop_memorial_card", Kind.Prop), ("prop_police_envelope", Kind.Prop), ("prop_newspaper", Kind.Prop), ("prop_wrench", Kind.Prop),
            ("flat_house_1", Kind.Flat), ("flat_house_2", Kind.Flat), ("flat_house_3", Kind.Flat), ("flat_bike_garage", Kind.Flat),
            ("tex_grass", Kind.Tile), ("tex_road", Kind.Tile), ("tex_sidewalk", Kind.Tile), ("tex_hospital_floor", Kind.Tile), ("tex_hospital_wall", Kind.Tile),
            ("sky_dream", Kind.Sky), ("sky_night", Kind.Sky),
            ("bg_ceiling_dark", Kind.Screen), ("ui_phone", Kind.Screen), ("bg_title_news", Kind.Screen), ("fx_static_wasp", Kind.Prop), ("journal_page", Kind.Screen),
        };

        public sealed class Set
        {
            public readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
            public readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            public readonly HashSet<string> fallbacks = new HashSet<string>(StringComparer.Ordinal);
            public Sprite blank;
        }

        public static Set MakeAll()
        {
            VisitingHoursBuilder.EnsureFolder(GeneratedDir);
            var set = new Set();
            var missing = new List<string>();
            foreach (var (name, kind) in All)
            {
                string real = ArtDir + "/" + name + ".png";
                string path;
                string stand = GeneratedDir + "/fallback_" + name + ".png";
                if (File.Exists(real))
                {
                    path = real;
                    // The real painting is here: its stand-in goes.
                    if (File.Exists(stand)) AssetDatabase.DeleteAsset(stand);
                }
                else
                {
                    missing.Add(name + ".png");
                    set.fallbacks.Add(name);
                    path = stand;
                    WritePng(path, Draw(name, kind, out int w, out int h), w, h);
                }
                Import(path, kind);
                set.textures[name] = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (kind != Kind.Tile && kind != Kind.Sky) set.sprites[name] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            string blank = GeneratedDir + "/portrait_blank.png";
            WritePng(blank, new Color32[16], 4, 4);
            Import(blank, Kind.Portrait);
            set.blank = AssetDatabase.LoadAssetAtPath<Sprite>(blank);
            if (missing.Count > 0) Debug.LogWarning("JAMKIT_VH art missing: " + string.Join(", ", missing) + " (neutral stand-ins in " + GeneratedDir + "; the next Rebuild Shell Scenes uses the real files)");
            else Debug.Log("JAMKIT_VH art: all " + All.Length + " files found in " + ArtDir);
            return set;
        }

        /// <summary>True for a texture that's one of the generated stand-ins.</summary>
        public static bool IsFallback(UnityEngine.Object o) => o != null && AssetDatabase.GetAssetPath(o).StartsWith(GeneratedDir + "/fallback_", StringComparison.Ordinal);

        static void WritePng(string path, Color32[] px, int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            tex.Apply();
            byte[] png = tex.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(tex);
            if (File.Exists(path) && File.ReadAllBytes(path).SequenceEqual(png)) return;
            File.WriteAllBytes(path, png);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }

        /// <summary>Import settings: paintings stay smooth (bilinear, mipmaps for the 3D ones, no pixel look),
        /// cut-outs keep their alpha, tiles and skies repeat.</summary>
        static void Import(string path, Kind kind)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) return;
            bool tile = kind == Kind.Tile || kind == Kind.Sky;
            var type = tile ? TextureImporterType.Default : TextureImporterType.Sprite;
            bool mips = kind != Kind.Portrait && kind != Kind.Screen;
            var wrap = kind == Kind.Tile ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            int max = kind == Kind.Screen || kind == Kind.Sky || kind == Kind.Flat ? 2048 : 1024;
            bool dirty = imp.textureType != type || imp.mipmapEnabled != mips || imp.wrapMode != wrap || imp.filterMode != FilterMode.Bilinear
                || imp.alphaIsTransparency != !tile || imp.maxTextureSize != max || imp.npotScale != TextureImporterNPOTScale.None
                || (type == TextureImporterType.Sprite && (imp.spriteImportMode != SpriteImportMode.Single || !Mathf.Approximately(imp.spritePixelsPerUnit, 100f)));
            if (kind == Kind.Sky && imp.wrapModeU != TextureWrapMode.Repeat) dirty = true;
            if (!dirty) return;
            imp.textureType = type;
            if (type == TextureImporterType.Sprite)
            {
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.spritePixelsPerUnit = 100f;
            }
            imp.mipmapEnabled = mips;
            imp.wrapMode = wrap;
            if (kind == Kind.Sky)
            {
                imp.wrapModeU = TextureWrapMode.Repeat;
                imp.wrapModeV = TextureWrapMode.Clamp;
            }
            imp.filterMode = FilterMode.Bilinear;
            imp.alphaIsTransparency = !tile;
            imp.maxTextureSize = max;
            imp.npotScale = TextureImporterNPOTScale.None;
            imp.textureCompression = TextureImporterCompression.Compressed;
            imp.SaveAndReimport();
        }

        // ---------- Neutral stand-ins ----------

        static Color32[] Draw(string name, Kind kind, out int w, out int h)
        {
            switch (kind)
            {
                case Kind.Portrait: w = 512; h = 512; return Portrait(name, w, h);
                case Kind.Billboard:
                    if (name == "billboard_dog") { w = 384; h = 256; return Blob(w, h, new Color32(176, 132, 72, 255), 0.42f, 0.32f); }
                    if (name == "billboard_elias_bed") { w = 640; h = 420; return Bed(w, h); }
                    w = 256; h = 512; return Person(name, w, h);
                case Kind.Prop: w = 256; h = 256; return Card(name, w, h);
                case Kind.Flat: w = 1024; h = 680; return House(name, w, h);
                case Kind.Tile: w = 256; h = 256; return Tile(name, w, h);
                case Kind.Sky: w = 1024; h = 384; return Sky(name == "sky_dream", w, h);
                default: w = 640; h = 360; return Screen(name, w, h);
            }
        }

        static uint Hash(string s)
        {
            uint x = 2166136261u;
            foreach (char c in s) x = (x ^ c) * 16777619u;
            return x;
        }

        static float Noise(int x, int y, uint seed)
        {
            uint n = (uint)(x * 374761393 + y * 668265263) ^ seed;
            n = (n ^ (n >> 13)) * 1274126177u;
            return ((n ^ (n >> 16)) & 0xffff) / 65535f;
        }

        static Color32 C(float r, float g, float b, float a = 1f) => new Color32((byte)(Mathf.Clamp01(r) * 255f), (byte)(Mathf.Clamp01(g) * 255f), (byte)(Mathf.Clamp01(b) * 255f), (byte)(Mathf.Clamp01(a) * 255f));

        static Color32 Tint(string name)
        {
            uint hsh = Hash(name);
            float hue = (hsh % 360) / 360f;
            var c = Color.HSVToRGB(hue, 0.25f, 0.62f);
            return C(c.r, c.g, c.b);
        }

        static Color32[] Portrait(string name, int w, int h)
        {
            var px = new Color32[w * h];
            var body = Tint(name.StartsWith("portrait_walt", StringComparison.Ordinal) ? "walt" : name);
            var skin = C(0.78f, 0.64f, 0.54f);
            if (name == "portrait_mags") skin = C(0.42f, 0.29f, 0.22f);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w, v = (y + 0.5f) / h;
                    float headD = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) * 1.2f + (v - 0.66f) * (v - 0.66f));
                    float shoulders = v < 0.42f ? Mathf.Abs(u - 0.5f) - (0.22f + (0.42f - v) * 0.9f) : 1f;
                    if (headD < 0.17f) px[y * w + x] = skin;
                    else if (shoulders < 0f) px[y * w + x] = body;
                }
            return px;
        }

        static Color32[] Person(string name, int w, int h)
        {
            var px = new Color32[w * h];
            var body = Tint(name);
            var skin = C(0.78f, 0.64f, 0.54f);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w, v = (y + 0.5f) / h;
                    float headD = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.88f) * (v - 0.88f) * 0.25f);
                    bool torso = v > 0.42f && v < 0.8f && Mathf.Abs(u - 0.5f) < 0.3f - (v - 0.42f) * 0.12f;
                    bool legs = v <= 0.42f && (Mathf.Abs(u - 0.38f) < 0.09f || Mathf.Abs(u - 0.62f) < 0.09f);
                    if (headD < 0.13f) px[y * w + x] = skin;
                    else if (torso || legs) px[y * w + x] = body;
                }
            return px;
        }

        static Color32[] Blob(int w, int h, Color32 color, float rx, float ry)
        {
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w - 0.5f, v = (y + 0.5f) / h - 0.45f;
                    if (u * u / (rx * rx) + v * v / (ry * ry) < 1f) px[y * w + x] = color;
                }
            return px;
        }

        static Color32[] Bed(int w, int h)
        {
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w, v = (y + 0.5f) / h;
                    if (v < 0.5f && u > 0.05f && u < 0.95f) px[y * w + x] = C(0.82f, 0.85f, 0.88f);
                    else if (v < 0.85f && u > 0.12f && u < 0.88f && v > 0.5f) px[y * w + x] = C(0.62f, 0.66f, 0.72f);
                }
            return px;
        }

        static Color32[] Card(string name, int w, int h)
        {
            var px = new Color32[w * h];
            var paper = C(0.86f, 0.82f, 0.72f);
            var edge = Tint(name);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w, v = (y + 0.5f) / h;
                    float m = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
                    if (m < 0.14f) continue;
                    px[y * w + x] = m < 0.2f ? edge : paper;
                }
            return px;
        }

        static Color32[] House(string name, int w, int h)
        {
            var px = new Color32[w * h];
            bool garage = name == "flat_bike_garage";
            var wall = Tint(name);
            var roof = C(0.38f, 0.26f, 0.22f);
            var trim = C(0.92f, 0.9f, 0.84f);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w, v = (y + 0.5f) / h;
                    float roofTop = garage ? 0.78f : 0.62f + (0.5f - Mathf.Abs(u - 0.5f)) * 0.7f;
                    if (u < 0.04f || u > 0.96f) continue;
                    if (v > roofTop) continue;
                    Color32 c = v > (garage ? 0.68f : 0.6f) ? roof : wall;
                    bool door = garage ? (Mathf.Abs(u - 0.5f) < 0.3f && v < 0.5f) : (Mathf.Abs(u - 0.5f) < 0.06f && v < 0.3f);
                    bool window = !garage && v > 0.32f && v < 0.5f && (Mathf.Abs(u - 0.25f) < 0.08f || Mathf.Abs(u - 0.75f) < 0.08f);
                    if (door) c = garage ? C(0.3f, 0.28f, 0.26f) : trim;
                    if (window) c = C(0.5f, 0.6f, 0.68f);
                    px[y * w + x] = c;
                }
            return px;
        }

        static Color32[] Tile(string name, int w, int h)
        {
            var px = new Color32[w * h];
            uint seed = Hash(name);
            Color baseC;
            switch (name)
            {
                case "tex_grass": baseC = new Color(0.36f, 0.52f, 0.24f); break;
                case "tex_road": baseC = new Color(0.26f, 0.26f, 0.27f); break;
                case "tex_sidewalk": baseC = new Color(0.68f, 0.66f, 0.62f); break;
                case "tex_hospital_floor": baseC = new Color(0.7f, 0.74f, 0.75f); break;
                default: baseC = new Color(0.72f, 0.77f, 0.74f); break;
            }
            bool grid = name == "tex_sidewalk" || name == "tex_hospital_floor";
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float n = Noise(x, y, seed) * 0.5f + Noise(x / 4, y / 4, seed + 7u) * 0.5f;
                    float k = 0.88f + n * 0.2f;
                    if (grid && (x % 128 < 2 || y % 128 < 2)) k *= 0.82f;
                    px[y * w + x] = C(baseC.r * k, baseC.g * k, baseC.b * k);
                }
            return px;
        }

        static Color32[] Sky(bool dream, int w, int h)
        {
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = (y + 0.5f) / h;
                Color c = dream
                    ? Color.Lerp(new Color(1f, 0.78f, 0.5f), new Color(0.48f, 0.66f, 0.86f), Mathf.SmoothStep(0.1f, 0.95f, v))
                    : Color.Lerp(new Color(0.08f, 0.1f, 0.18f), new Color(0.01f, 0.01f, 0.04f), v);
                for (int x = 0; x < w; x++)
                {
                    var p = c;
                    if (!dream && Noise(x, y, 99u) > 0.9985f) p = Color.white * 0.8f;
                    px[y * w + x] = C(p.r, p.g, p.b);
                }
            }
            return px;
        }

        static Color32[] Screen(string name, int w, int h)
        {
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w, v = (y + 0.5f) / h;
                    Color c;
                    switch (name)
                    {
                        case "bg_ceiling_dark":
                            c = new Color(0.05f, 0.05f, 0.07f);
                            if (Mathf.Abs((u - v * 0.5f) - 0.55f) < 0.03f) c = new Color(0.16f, 0.15f, 0.13f);
                            break;
                        case "ui_phone":
                            c = (u > 0.06f && u < 0.94f && v > 0.04f && v < 0.96f) ? new Color(0.12f, 0.13f, 0.15f) : new Color(0.02f, 0.02f, 0.02f);
                            break;
                        case "bg_title_news":
                            bool tv = u > 0.2f && u < 0.8f && v > 0.25f && v < 0.85f;
                            c = tv ? Color.Lerp(new Color(0.04f, 0.06f, 0.12f), new Color(0.1f, 0.12f, 0.2f), v) : new Color(0.03f, 0.03f, 0.04f);
                            if (tv && Mathf.Abs(u - 0.42f) < 0.02f && Mathf.Abs(v - 0.45f) < 0.02f) c = new Color(0.9f, 0.15f, 0.12f);
                            if (tv && Mathf.Abs(u - 0.58f) < 0.02f && Mathf.Abs(v - 0.45f) < 0.02f) c = new Color(0.15f, 0.3f, 0.9f);
                            break;
                        default:
                            c = new Color(0.93f, 0.9f, 0.82f);
                            if (Mathf.Repeat(v * 24f, 1f) < 0.04f) c *= 0.92f;
                            break;
                    }
                    px[y * w + x] = C(c.r, c.g, c.b);
                }
            return px;
        }
    }
}
