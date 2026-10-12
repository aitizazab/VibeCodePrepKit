using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace JamKit.DreamEscape.EditorTools
{
    /// <summary>
    /// The escape's surfaces (House agent, round 2): each room its own palette (house-1.md: "oxblood in nearly every room so
    /// they blur"): the dining room's sage stripe, the hall's oxblood (the creator's), the stairs' dusty blue, the corridor's
    /// butter trellis, Theo's sky with stars, the kitchen's butter plaster and tiles, the attic's boards, the last hall's
    /// oxblood again. The wallpapers are tiled with Repeat wrap (the creator's stripe came through DreamArt with Clamp wrap:
    /// one 0.5 m tile and then its edge pixels smeared to the walls, the "bright seam" down the escape's floors and
    /// ceilings). Clean enamel, brass and glass, and HDR glows for the lamps and the light round the doors.
    /// </summary>
    public static class HouseEscapeLook
    {
        public const string Dir = DreamEscapeBuilder.Root + "/House/Escape";
        public const string TexDir = Dir + "/Textures";
        public const string MatDir = Dir + "/Materials";
        const string LitShader = "Universal Render Pipeline/Lit", UnlitShader = "Universal Render Pipeline/Unlit";

        static readonly Dictionary<string, Material> _mats = new Dictionary<string, Material>();
        static readonly Dictionary<string, Palette> _palettes = new Dictionary<string, Palette>();

        public static void Clear()
        {
            _mats.Clear();
            _palettes.Clear();
        }

        /// <summary>A room's surfaces: its walls (the old walls: on its side they're the floor and the ceiling), the old
        /// floor and ceiling, the wainscot under the dado rail and its height, the trims, the lamps' colour.</summary>
        public sealed class Palette
        {
            public Material wall, oldFloor, oldCeiling, wainscot, rail, skirting, cornice, edge, rug, rugBorder;
            public float wainscotHeight = 0.9f;
            public Color lamp = new Color(1f, 0.78f, 0.55f);
        }

        public static Palette For(EscapeRoom room)
        {
            string theme = room != null ? room.theme : "hall";
            if (_palettes.TryGetValue(theme, out var p) && p.wall != null) return p;
            var oak = HouseMaterials.Surface("wood_floor", null, null, 0.42f);
            var walnut = HouseMaterials.TrimWalnut();
            var walnutPanel = HouseMaterials.Surface("wooden_panels", "Walnut", new Color(0.62f, 0.42f, 0.3f), 0.4f);
            var white = Clean("TrimWhite", new Color(0.93f, 0.91f, 0.86f), 0.45f);
            var plaster = HouseMaterials.Ceiling();
            switch (theme)
            {
                case "dining":
                    p = new Palette { wall = Wallpaper("Sage", "escape_wallpaper_sage.png", 0.6f), oldFloor = oak, oldCeiling = plaster, wainscot = walnutPanel, rail = walnut, skirting = walnut, cornice = white, edge = walnut,
                        rug = HouseMaterials.Fabric("EscRugDining", new Color(0.56f, 0.24f, 0.2f)), rugBorder = HouseMaterials.Fabric("EscRugBorder", new Color(0.86f, 0.76f, 0.56f)), lamp = new Color(1f, 0.8f, 0.58f) };
                    break;
                case "stairs":
                    p = new Palette { wall = Wallpaper("Blue", "escape_wallpaper_blue.png", 0.6f), oldFloor = oak, oldCeiling = plaster, wainscot = Clean("WainscotWhite", new Color(0.9f, 0.89f, 0.84f), 0.5f), rail = white, skirting = white, cornice = white, edge = white,
                        rug = HouseMaterials.Fabric("StairRunner", new Color(0.36f, 0.42f, 0.55f)), rugBorder = HouseMaterials.Fabric("EscRugBorder", new Color(0.86f, 0.76f, 0.56f)), lamp = new Color(1f, 0.82f, 0.62f) };
                    break;
                case "corridor":
                    p = new Palette { wall = Wallpaper("Trellis", "escape_wallpaper_trellis.png", 0.7f), oldFloor = oak, oldCeiling = plaster, wainscot = walnutPanel, rail = walnut, skirting = walnut, cornice = white, edge = walnut,
                        rug = HouseMaterials.Fabric("EscCorridorRunner", new Color(0.5f, 0.22f, 0.2f)), rugBorder = HouseMaterials.Fabric("EscRugBorder", new Color(0.86f, 0.76f, 0.56f)), lamp = new Color(1f, 0.76f, 0.5f) };
                    break;
                case "theo":
                    p = new Palette { wall = Wallpaper("Theo", "escape_wallpaper_theo.png", 0.9f), oldFloor = HouseMaterials.Fabric("TheoCarpet", new Color(0.55f, 0.66f, 0.82f)), oldCeiling = HouseMaterials.Look("Plaster_Painted", "EscTheoCeiling", new Color(0.86f, 0.9f, 0.96f)),
                        wainscot = Clean("WainscotWhite", new Color(0.9f, 0.89f, 0.84f), 0.5f), rail = white, skirting = white, cornice = white, edge = white,
                        rug = HouseMaterials.Fabric("EscRugTheo", new Color(0.92f, 0.6f, 0.26f)), rugBorder = HouseMaterials.Fabric("EscRugBorderTheo", new Color(0.3f, 0.44f, 0.7f)), lamp = new Color(0.86f, 0.92f, 1f) };
                    break;
                case "kitchen":
                    p = new Palette { wall = HouseMaterials.Look("Plaster_Interior", "KitchenButter", new Color(0.98f, 0.9f, 0.68f)), oldFloor = HouseMaterials.Surface("old_linoleum_flooring_01", null, new Color(1f, 0.96f, 0.9f), 0.35f), oldCeiling = plaster,
                        wainscot = HouseMaterials.Surface("Tiles133A", null, null, 0.7f), wainscotHeight = 1.4f, rail = white, skirting = white, cornice = white, edge = white,
                        rug = HouseMaterials.Fabric("EscRugKitchen", new Color(0.3f, 0.46f, 0.4f)), rugBorder = HouseMaterials.Fabric("EscRugBorder", new Color(0.86f, 0.76f, 0.56f)), lamp = new Color(1f, 0.86f, 0.66f) };
                    break;
                case "attic":
                    var planks = HouseMaterials.Planks();
                    p = new Palette { wall = planks, oldFloor = planks, oldCeiling = planks, wainscot = null, rail = walnut, skirting = walnut, cornice = walnut, edge = walnut,
                        rug = HouseMaterials.Fabric("EscRugAttic", new Color(0.5f, 0.3f, 0.26f)), rugBorder = HouseMaterials.Fabric("EscRugBorder", new Color(0.86f, 0.76f, 0.56f)), lamp = new Color(0.62f, 0.72f, 1f) };
                    break;
                default:   // "hall", "lasthall": the creator's oxblood regency stripe over walnut, the house's own hall
                    p = new Palette { wall = Oxblood(), oldFloor = oak, oldCeiling = plaster, wainscot = walnutPanel, rail = walnut, skirting = walnut, cornice = white, edge = walnut,
                        rug = HouseMaterials.Fabric("EscRugHall", new Color(0.36f, 0.42f, 0.55f)), rugBorder = HouseMaterials.Fabric("EscRugBorder", new Color(0.86f, 0.76f, 0.56f)), lamp = new Color(1f, 0.78f, 0.55f) };
                    break;
            }
            _palettes[theme] = p;
            return p;
        }

        // ---------- Materials ----------

        static void EnsureDirs()
        {
            DreamEscapeBuilder.EnsureFolder(Dir);
            DreamEscapeBuilder.EnsureFolder(TexDir);
            DreamEscapeBuilder.EnsureFolder(MatDir);
        }

        static Material Asset(string name, string shader)
        {
            EnsureDirs();
            string path = MatDir + "/VHE_" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            var sh = Shader.Find(shader);
            if (sh == null) { Debug.LogError("JAMKIT_HOUSE_ESCAPE no shader " + shader); sh = Shader.Find(LitShader); }
            if (m == null) { m = new Material(sh) { name = "VHE_" + name }; AssetDatabase.CreateAsset(m, path); }
            else if (m.shader != sh) m.shader = sh;
            return m;
        }

        /// <summary>A texture of ours, imported for tiling (Repeat, mipmaps, a little anisotropy).</summary>
        public static Texture2D Tex(string file, bool alpha = false)
        {
            string path = TexDir + "/" + file;
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (t == null && File.Exists(path)) { AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport); t = AssetDatabase.LoadAssetAtPath<Texture2D>(path); }
            if (AssetImporter.GetAtPath(path) is TextureImporter ti)
            {
                bool changed = ti.wrapMode != TextureWrapMode.Repeat || !ti.mipmapEnabled || ti.anisoLevel < 4 || ti.alphaIsTransparency != alpha || ti.maxTextureSize > 1024;
                if (changed)
                {
                    ti.textureType = TextureImporterType.Default;
                    ti.wrapMode = TextureWrapMode.Repeat;
                    ti.mipmapEnabled = true;
                    ti.anisoLevel = 4;
                    ti.alphaIsTransparency = alpha;
                    ti.sRGBTexture = true;
                    ti.maxTextureSize = 1024;
                    ti.SaveAndReimport();
                    t = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                }
            }
            if (t == null) Debug.LogWarning("JAMKIT_HOUSE_ESCAPE missing texture " + path + " (tools/art/vh_escape_textures.py makes it)");
            return t;
        }

        /// <summary>A wallpaper: our tile at <paramref name="tile"/> metres a repeat, on the plaster's normal.</summary>
        public static Material Wallpaper(string name, string file, float tile)
        {
            string key = "wallpaper:" + name;
            if (_mats.TryGetValue(key, out var c) && c != null) return c;
            var m = Asset("Wallpaper_" + name, LitShader);
            m.SetTexture("_BaseMap", Tex(file));
            m.SetTextureScale("_BaseMap", new Vector2(1f / tile, 1f / tile));
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Smoothness", 0.3f);
            m.SetFloat("_Metallic", 0f);
            var plaster = HouseMaterials.Surface("white_plaster_02");
            var n = plaster != null ? plaster.GetTexture("_BumpMap") : null;
            if (n != null) { m.SetTexture("_BumpMap", n); m.SetFloat("_BumpScale", 0.3f); m.EnableKeyword("_NORMALMAP"); }
            EditorUtility.SetDirty(m);
            _mats[key] = m;
            return m;
        }

        /// <summary>The creator's oxblood regency stripe, copied beside our textures so it can tile (Repeat).</summary>
        public static Material Oxblood()
        {
            const string file = "escape_wallpaper_oxblood.png";
            string dst = TexDir + "/" + file;
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(dst) == null)
            {
                EnsureDirs();
                var src = DreamArt.Get("dream_wall_oxblood-stripe", out bool placeholder);
                string srcPath = src != null ? AssetDatabase.GetAssetPath(src) : null;
                if (!string.IsNullOrEmpty(srcPath) && !placeholder) AssetDatabase.CopyAsset(srcPath, dst);
                else return HouseMaterials.Wallpaper();
            }
            return Wallpaper("Oxblood", file, 0.5f);
        }

        /// <summary>A clean painted or enamelled surface (no plaster noise): enamel, painted wood, chrome, brass.</summary>
        public static Material Clean(string name, Color c, float smoothness, float metallic = 0f)
        {
            string key = "clean:" + name;
            if (_mats.TryGetValue(key, out var cached) && cached != null) return cached;
            var m = Asset("Clean_" + name, LitShader);
            m.SetTexture("_BaseMap", null);
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            EditorUtility.SetDirty(m);
            _mats[key] = m;
            return m;
        }

        /// <summary>A light source's own glow (a lampshade, the glass round a door, the sky through a window): unlit, HDR, so
        /// the bloom catches it.</summary>
        public static Material Glow(string name, Color c, float intensity)
        {
            string key = "glow:" + name;
            if (_mats.TryGetValue(key, out var cached) && cached != null) return cached;
            var m = Asset("Glow_" + name, UnlitShader);
            m.SetColor("_BaseColor", new Color(c.r * intensity, c.g * intensity, c.b * intensity, 1f));
            EditorUtility.SetDirty(m);
            _mats[key] = m;
            return m;
        }

        /// <summary>Theo's glow-in-the-dark stars stuck on his ceiling (the floor, upside down): unlit, alpha-clipped.</summary>
        public static Material GlowStars()
        {
            const string key = "glowstars";
            if (_mats.TryGetValue(key, out var cached) && cached != null) return cached;
            var m = Asset("GlowStars", UnlitShader);
            m.SetTexture("_BaseMap", Tex("escape_glow_stars.png", true));
            m.SetTextureScale("_BaseMap", new Vector2(1f / 2.4f, 1f / 2.4f));
            m.SetColor("_BaseColor", new Color(0.86f, 1.15f, 0.72f, 1f));
            m.SetFloat("_AlphaClip", 1f);
            m.SetFloat("_Cutoff", 0.5f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.renderQueue = (int)RenderQueue.AlphaTest;
            EditorUtility.SetDirty(m);
            _mats[key] = m;
            return m;
        }

        /// <summary>The night over the attic (JamKit/House/NightSky, procedural: no texture).</summary>
        public static Material NightSky()
        {
            const string key = "nightsky";
            if (_mats.TryGetValue(key, out var cached) && cached != null) return cached;
            var m = Asset("NightSky", "JamKit/House/NightSky");
            m.renderQueue = 2600;
            EditorUtility.SetDirty(m);
            _mats[key] = m;
            return m;
        }

        public static Material Brass() => Clean("Brass", new Color(0.78f, 0.6f, 0.32f), 0.72f, 1f);
        public static Material Chrome() => Clean("Chrome", new Color(0.86f, 0.87f, 0.88f), 0.86f, 1f);
        public static Material Iron() => Clean("Iron", new Color(0.12f, 0.12f, 0.13f), 0.42f, 0.6f);
        public static Material Enamel(string name, Color c) => Clean("Enamel_" + name, c, 0.78f);
        public static Material DoorRed() => Clean("FrontDoorRed", new Color(0.56f, 0.12f, 0.1f), 0.62f);
    }
}
