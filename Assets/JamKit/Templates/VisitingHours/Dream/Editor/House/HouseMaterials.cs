using System.Collections.Generic;
using System.IO;
using JamKit.Look.EditorTools;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace JamKit.DreamEscape.EditorTools
{
    /// <summary>
    /// The house's materials (URP Lit, PBR, 1 UV unit = 1 metre like the Look library): the Look agent's library where it
    /// has the surface (painted plaster, ceilings, clapboard, roof shingles, brick, decking, fence boards, paving, asphalt,
    /// glass, trim paint), and the house's own CC0 surfaces from Dream/House/Textures (oak floorboards, carpet, kitchen
    /// lino, bathroom tiles, wall tiles, panelling, concrete, attic planks, lawn) made into Lit materials here; the creator's
    /// oxblood regency stripe (Art/Dream) as the wallpaper over a plaster normal. And each room's finish (floor, walls,
    /// wainscot, ceiling) by its style.
    /// </summary>
    public static class HouseMaterials
    {
        public const string HouseDir = DreamEscapeBuilder.Root + "/House";
        public const string TexDir = HouseDir + "/Textures";
        public const string ModelDir = HouseDir + "/Models";
        public const string MatDir = DreamEscapeBuilder.MaterialsDir + "/House";
        const string LitShader = "Universal Render Pipeline/Lit";

        static readonly Dictionary<string, Material> _cache = new Dictionary<string, Material>();

        /// <summary>The house's own surfaces: id → metres one texture repeat covers (from house-assets.json's size_m).</summary>
        static readonly Dictionary<string, float> Tiles = new Dictionary<string, float>
        {
            { "wood_floor", 1.7f }, { "wood_floor_worn", 2.0f }, { "white_plaster_02", 1.0f }, { "beige_wall_001", 3.0f },
            { "wooden_panels", 2.1f }, { "Tiles133A", 1.0f }, { "old_linoleum_flooring_01", 2.0f }, { "interior_tiles", 1.9f },
            { "Carpet016", 1.7f }, { "garage_floor", 1.9f }, { "red_brick", 1.4f }, { "white_planks_clean", 1.8f },
            { "RoofingTiles009", 3.2f }, { "Grass004", 1.4f }, { "farm_soil", 2.0f }, { "concrete_pavers", 1.92f },
        };

        public static void Clear() => _cache.Clear();

        /// <summary>A house surface from Dream/House/Textures/<paramref name="id"/>, tinted (a variant name keeps tints apart).</summary>
        public static Material Surface(string id, string variant = null, Color? tint = null, float smoothness = 0.3f, float normal = 1f)
        {
            string key = id + (variant != null ? "__" + variant : "");
            if (_cache.TryGetValue(key, out var cached) && cached != null) return cached;
            DreamEscapeBuilder.EnsureFolder(MatDir);
            string path = MatDir + "/VHH_" + key + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find(LitShader)) { name = "VHH_" + key };
                AssetDatabase.CreateAsset(m, path);
            }
            string dir = TexDir + "/" + id;
            var albedo = Find(dir, "_diff", "_Color", "albedo", "diff");
            var nrm = Find(dir, "nor_gl", "NormalGL", "normal");
            var ao = Find(dir, "_ao", "AmbientOcclusion");
            if (nrm != null) NormalImport(AssetDatabase.GetAssetPath(nrm));
            float tile = Tiles.TryGetValue(id, out float t) ? t : 2f;
            m.SetTexture("_BaseMap", albedo);
            m.SetTextureScale("_BaseMap", new Vector2(1f / tile, 1f / tile));
            m.SetColor("_BaseColor", tint ?? Color.white);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", 0f);
            if (nrm != null) { m.SetTexture("_BumpMap", nrm); m.SetFloat("_BumpScale", normal); m.EnableKeyword("_NORMALMAP"); }
            if (ao != null) { m.SetTexture("_OcclusionMap", ao); m.SetFloat("_OcclusionStrength", 0.8f); m.EnableKeyword("_OCCLUSIONMAP"); }
            if (albedo == null) Debug.LogWarning("JAMKIT_HOUSE no albedo for surface " + id + " (import Dream/House/Textures first)");
            EditorUtility.SetDirty(m);
            _cache[key] = m;
            return m;
        }

        static Texture2D Find(string dir, params string[] keys)
        {
            if (!AssetDatabase.IsValidFolder(dir)) return null;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { dir }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                string n = Path.GetFileNameWithoutExtension(p);
                foreach (var k in keys)
                    if (n.IndexOf(k, System.StringComparison.OrdinalIgnoreCase) >= 0) return AssetDatabase.LoadAssetAtPath<Texture2D>(p);
            }
            return null;
        }

        /// <summary>Sets a texture to import as a normal map (once).</summary>
        public static void NormalImport(string path)
        {
            if (AssetImporter.GetAtPath(path) is TextureImporter ti && ti.textureType != TextureImporterType.NormalMap)
            {
                ti.textureType = TextureImporterType.NormalMap;
                ti.SaveAndReimport();
            }
        }

        /// <summary>A Look library material (or a tint of it).</summary>
        public static Material Look(string name, string variant = null, Color? tint = null)
        {
            string key = "look:" + name + ":" + variant;
            if (_cache.TryGetValue(key, out var c) && c != null) return c;
            var m = variant != null && tint.HasValue ? VhLookLibrary.Tinted(name, variant, tint.Value) : VhLookLibrary.Get(name);
            _cache[key] = m;
            return m;
        }

        public static Material Paint(string variant, Color colour, float smoothness = 1.25f)
        {
            string key = "paint:" + variant;
            if (_cache.TryGetValue(key, out var c) && c != null) return c;
            var m = VhLookLibrary.Paint(variant, colour, smoothness);
            _cache[key] = m;
            return m;
        }

        // ---------- The house's palette ----------

        public static readonly Color Walnut = new Color(0.42f, 0.27f, 0.18f), Cream = new Color(0.96f, 0.92f, 0.84f), WarmWhite = new Color(0.98f, 0.96f, 0.92f);
        public static Material Glass() => VhLookLibrary.Glass();
        public static Material TrimWhite() => Paint("HouseTrim", new Color(0.95f, 0.93f, 0.88f));
        public static Material TrimWalnut() => Surface("wood_floor", "Walnut", Walnut, 0.45f);
        public static Material Ceiling() => Look("Plaster_Painted", "HouseCeiling", new Color(0.98f, 0.96f, 0.92f));
        public static Material Siding() => Look("Clapboard_Painted", "LasnameSage", new Color(0.72f, 0.79f, 0.68f));
        public static Material Roof() => Look("Roof_ShinglesSlate");
        public static Material Brick() => Look("Brick_Red");
        public static Material Foundation() => Look("Concrete_Kerb");
        public static Material Deck() => Look("Deck_Planks");
        public static Material FenceBoards() => Look("Fence_Planks");
        public static Material Paving() => Look("Concrete_Sidewalk");
        public static Material Asphalt() => Look("Asphalt");
        public static Material Lawn() => Surface("Grass004", null, new Color(0.92f, 1f, 0.86f), 0.12f);
        public static Material Soil() => Surface("farm_soil", null, null, 0.1f);
        public static Material Hedge() => Look("Plaster_Painted", "HedgeLeaves", new Color(0.3f, 0.45f, 0.24f));
        public static Material Bark() => Look("Tree_Bark");
        public static Material Planks() => Surface("wood_floor_worn", null, null, 0.25f);
        public static Material Concrete() => Surface("garage_floor", null, null, 0.25f);
        public static Material Metal() => Paint("HouseMetal", new Color(0.55f, 0.56f, 0.58f), 1.6f);
        public static Material Fabric(string variant, Color c) => Surface("Carpet016", variant, c, 0.05f, 0.6f);

        /// <summary>The creator's wallpaper (the oxblood regency stripe, round 4's pick): the stripe on a plaster normal.</summary>
        public static Material Wallpaper()
        {
            const string key = "wallpaper";
            if (_cache.TryGetValue(key, out var c) && c != null) return c;
            string path = MatDir + "/VHH_Wallpaper_Oxblood.mat";
            DreamEscapeBuilder.EnsureFolder(MatDir);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find(LitShader)) { name = "VHH_Wallpaper_Oxblood" }; AssetDatabase.CreateAsset(m, path); }
            // A Repeat copy beside the escape's papers: DreamArt imports its art Clamped, so one 0.5 m tile showed and its
            // edge smeared (the flat oxblood walls, the escape's bright seam).
            const string file = "escape_wallpaper_oxblood.png";
            string dst = HouseEscapeLook.TexDir + "/" + file;
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(dst) == null)
            {
                var src = DreamArt.Get("dream_wall_oxblood-stripe", out bool placeholder);
                string srcPath = src != null ? AssetDatabase.GetAssetPath(src) : null;
                if (!string.IsNullOrEmpty(srcPath) && !placeholder) { DreamEscapeBuilder.EnsureFolder(HouseEscapeLook.TexDir); AssetDatabase.CopyAsset(srcPath, dst); }
            }
            var tex = HouseEscapeLook.Tex(file) ?? DreamArt.Get("dream_wall_oxblood-stripe", out _);
            m.SetTexture("_BaseMap", tex);
            m.SetTextureScale("_BaseMap", new Vector2(1f / 0.5f, 1f / 0.5f));
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Smoothness", 0.32f);
            var plaster = Surface("white_plaster_02");
            var n = plaster != null ? plaster.GetTexture("_BumpMap") : null;
            if (n != null) { m.SetTexture("_BumpMap", n); m.SetFloat("_BumpScale", 0.35f); m.EnableKeyword("_NORMALMAP"); }
            EditorUtility.SetDirty(m);
            _cache[key] = m;
            return m;
        }

        /// <summary>A room's finish.</summary>
        public sealed class Finish
        {
            public Material floor, wall, wainscot, ceiling, skirting, rail;
            public float wainscotHeight;
            public bool crown;
        }

        static readonly Dictionary<string, Finish> _finishes = new Dictionary<string, Finish>();

        public static Finish For(string style)
        {
            if (style == null) style = "oxblood";
            if (_finishes.TryGetValue(style, out var f) && f.floor != null) return f;
            var oak = Surface("wood_floor", null, null, 0.42f);
            var walnutPanel = Surface("wooden_panels", "Walnut", new Color(0.62f, 0.42f, 0.3f), 0.4f);
            var plaster = Look("Plaster_Interior", "HouseCream", Cream);
            var whitePanel = Surface("wooden_panels", "PaintedWhite", new Color(0.93f, 0.92f, 0.88f), 0.45f);
            switch (style)
            {
                // Each room its own colour (house-1.md: oxblood in nearly every room blurred them together). The creator's
                // oxblood stripe stays in the halls, where the house starts.
                case "living":
                    f = new Finish { floor = oak, wall = HouseEscapeLook.Wallpaper("HouseSage", "escape_wallpaper_sage.png", 0.6f), wainscot = walnutPanel, wainscotHeight = 0.9f, ceiling = Ceiling(), skirting = TrimWalnut(), rail = TrimWalnut(), crown = true };
                    break;
                case "dining":
                    f = new Finish { floor = oak, wall = HouseEscapeLook.Wallpaper("HouseBlue", "escape_wallpaper_blue.png", 0.6f), wainscot = whitePanel, wainscotHeight = 0.95f, ceiling = Ceiling(), skirting = TrimWhite(), rail = TrimWhite(), crown = true };
                    break;
                case "study":
                    f = new Finish { floor = oak, wall = Look("Plaster_Interior", "DenGreen", new Color(0.42f, 0.52f, 0.42f)), wainscot = walnutPanel, wainscotHeight = 0.9f, ceiling = Ceiling(), skirting = TrimWalnut(), rail = TrimWalnut(), crown = true };
                    break;
                case "parents":
                    f = new Finish { floor = oak, wall = Look("Plaster_Interior", "ParentsDove", new Color(0.82f, 0.78f, 0.82f)), wainscot = whitePanel, wainscotHeight = 0.9f, ceiling = Ceiling(), skirting = TrimWhite(), rail = TrimWhite(), crown = true };
                    break;
                case "kitchen":
                    f = new Finish { floor = Surface("old_linoleum_flooring_01", null, new Color(1f, 0.96f, 0.9f), 0.35f), wall = Look("Plaster_Interior", "KitchenButter", new Color(0.98f, 0.9f, 0.68f)), wainscot = Surface("Tiles133A", null, null, 0.7f), wainscotHeight = 1.4f, ceiling = Ceiling(), skirting = TrimWhite(), crown = true };
                    break;
                case "pantry":
                case "mudroom":
                    f = new Finish { floor = Surface("old_linoleum_flooring_01", null, new Color(0.95f, 0.92f, 0.86f), 0.3f), wall = plaster, wainscot = walnutPanel, wainscotHeight = 0.9f, ceiling = Ceiling(), skirting = TrimWalnut() };
                    break;
                case "bath":
                    f = new Finish { floor = Surface("interior_tiles", null, null, 0.6f), wall = Look("Plaster_Interior", "BathSeafoam", new Color(0.8f, 0.92f, 0.88f)), wainscot = Surface("Tiles133A", null, null, 0.7f), wainscotHeight = 1.5f, ceiling = Ceiling(), skirting = TrimWhite() };
                    break;
                case "theo":
                    f = new Finish { floor = Fabric("TheoCarpet", new Color(0.55f, 0.66f, 0.82f)), wall = HouseEscapeLook.Wallpaper("HouseTheo", "escape_wallpaper_theo.png", 0.7f), wainscot = whitePanel, wainscotHeight = 0.9f, ceiling = Ceiling(), skirting = TrimWhite(), rail = TrimWhite(), crown = true };
                    break;
                case "guest":
                    f = new Finish { floor = Fabric("GuestCarpet", new Color(0.82f, 0.74f, 0.64f)), wall = HouseEscapeLook.Wallpaper("HouseTrellis", "escape_wallpaper_trellis.png", 0.6f), wainscot = whitePanel, wainscotHeight = 0.9f, ceiling = Ceiling(), skirting = TrimWhite(), rail = TrimWhite(), crown = true };
                    break;
                case "closet":
                    f = new Finish { floor = Fabric("GuestCarpet", new Color(0.82f, 0.74f, 0.64f)), wall = plaster, wainscot = null, ceiling = Ceiling(), skirting = TrimWhite(), crown = false };
                    break;
                case "basement":
                    f = new Finish { floor = Concrete(), wall = Look("Plaster_Painted", "BasementGrey", new Color(0.78f, 0.76f, 0.72f)), wainscot = null, ceiling = Look("Plaster_Painted", "BasementCeiling", new Color(0.7f, 0.68f, 0.64f)), skirting = null };
                    break;
                case "garage":
                    f = new Finish { floor = Concrete(), wall = Look("Plaster_Painted", "GarageWall", new Color(0.82f, 0.8f, 0.76f)), ceiling = Planks(), skirting = null };
                    break;
                case "attic":
                case "treehouse":
                case "shed":
                case "cupboard":
                    f = new Finish { floor = Planks(), wall = Planks(), ceiling = Planks(), skirting = null };
                    break;
                case "observatory":
                    f = new Finish { floor = Planks(), wall = Surface("wooden_panels", "Observatory", new Color(0.36f, 0.24f, 0.18f), 0.55f), ceiling = Surface("wooden_panels", "ObservatoryCeiling", new Color(0.18f, 0.16f, 0.24f), 0.5f), skirting = TrimWalnut() };
                    break;
                case "yard":
                    f = new Finish { floor = Lawn(), wall = Siding(), ceiling = Siding() };
                    break;
                default:   // the creator's oxblood stripe over walnut panelling: the living rooms, the halls, the den, their room
                    f = new Finish { floor = oak, wall = Wallpaper(), wainscot = walnutPanel, wainscotHeight = 0.9f, ceiling = Ceiling(), skirting = TrimWalnut(), rail = TrimWalnut(), crown = true };
                    break;
            }
            _finishes[style] = f;
            return f;
        }

        public static void ClearFinishes() => _finishes.Clear();

        // ---------- Models (Dream/House/Models, house-assets.json; Kenney's kit) ----------

        sealed class ModelEntry { public string id, role, fbx; public Vector3 dims; public List<string> roles = new List<string>(); public FitSpec fit = new FitSpec(); }

        /// <summary>How a model sits in its box (house-assets.json "fit"; Kenney's in "kenney_fit" by file name): the
        /// model's own up and front after Unity's import (Poly Haven: +y up, front +z, the FBX root carries Blender's axis
        /// conversion, which the fit keeps), an extra turn, how far each axis may stretch past the uniform fit to fill the
        /// box, and its real size (for the mesh lint's upright and scale checks).</summary>
        public sealed class FitSpec
        {
            /// <summary>An extra rotation (Euler degrees, in the model's frame) when its import isn't +y up, front +z.</summary>
            public Vector3 rotate;
            /// <summary>Degrees added to the part's yaw (a model whose front isn't +z).</summary>
            public float yaw;
            /// <summary>How far one axis may grow past the uniform fit (0.12: 12 %), so a bed fills its box.</summary>
            public float stretch = 0.12f;
            /// <summary>The real size (x, y, z metres, upright, front +z), or zero when unknown.</summary>
            public Vector3 dims;
        }

        static List<ModelEntry> _models;
        static Dictionary<string, string> _kenney;
        static Dictionary<string, FitSpec> _kenneyFit;

        static void LoadManifest()
        {
            if (_models != null) return;
            _models = new List<ModelEntry>();
            _kenney = new Dictionary<string, string>();
            _kenneyFit = new Dictionary<string, FitSpec>();
            string path = HouseDir + "/house-assets.json";
            if (!File.Exists(path)) { Debug.LogWarning("JAMKIT_HOUSE no " + path); return; }
            var root = MiniJson.Parse(File.ReadAllText(path)) as Dictionary<string, object>;
            if (root == null) return;
            if (root.TryGetValue("models", out var ms) && ms is List<object> list)
                foreach (var o in list)
                {
                    if (!(o is Dictionary<string, object> d)) continue;
                    var e = new ModelEntry { id = d["id"] as string, role = d["role"] as string, fbx = d["fbx"] as string };
                    if (d.TryGetValue("dims_m", out var dm) && dm is List<object> dl && dl.Count == 3)
                        e.dims = new Vector3(System.Convert.ToSingle(dl[0]), System.Convert.ToSingle(dl[1]), System.Convert.ToSingle(dl[2]));
                    e.roles.Add(e.role);
                    if (d.TryGetValue("alt_roles", out var ar) && ar is List<object> al) foreach (var r in al) e.roles.Add(r as string);
                    e.fit = ReadFit(d.TryGetValue("fit", out var fo) ? fo as Dictionary<string, object> : null);
                    e.fit.dims = e.dims;
                    _models.Add(e);
                }
            if (root.TryGetValue("kenney_files", out var kf) && kf is Dictionary<string, object> kd)
                foreach (var kv in kd) _kenney[kv.Key] = "Assets/JamKit/Templates/VisitingHours/" + kv.Value;
            if (root.TryGetValue("kenney_fit", out var kfit) && kfit is Dictionary<string, object> kfd)
                foreach (var kv in kfd)
                {
                    var kfs = ReadFit(kv.Value as Dictionary<string, object>);
                    if (!(kv.Value is Dictionary<string, object> kvd && kvd.ContainsKey("yaw"))) kfs.yaw = 180f;
                    _kenneyFit["kenney:" + kv.Key] = kfs;
                }
        }

        static FitSpec ReadFit(Dictionary<string, object> d)
        {
            var f = new FitSpec();
            if (d == null) return f;
            if (d.TryGetValue("rotate", out var r) && r is List<object> rl && rl.Count == 3)
                f.rotate = new Vector3(System.Convert.ToSingle(rl[0]), System.Convert.ToSingle(rl[1]), System.Convert.ToSingle(rl[2]));
            if (d.TryGetValue("yaw", out var y)) f.yaw = System.Convert.ToSingle(y);
            if (d.TryGetValue("stretch", out var st)) f.stretch = System.Convert.ToSingle(st);
            if (d.TryGetValue("dims_m", out var dm) && dm is List<object> dl && dl.Count == 3)
                f.dims = new Vector3(System.Convert.ToSingle(dl[0]), System.Convert.ToSingle(dl[1]), System.Convert.ToSingle(dl[2]));
            return f;
        }

        /// <summary>The fit settings for a model id from <see cref="Model"/> (defaults: no turn, 12 % stretch).</summary>
        public static FitSpec Fit(string id)
        {
            LoadManifest();
            if (id == null) return new FitSpec();
            if (_kenneyFit.TryGetValue(id, out var k)) return k;
            foreach (var e in _models) if (e.id == id) return e.fit;
            // Kenney's kit faces -z (a chair's back, a bed's headboard and a toilet's tank are at +z: measured from the
            // FBX geometry): turned round so 0 faces +z like the rest.
            if (id.StartsWith("kenney:")) return new FitSpec { yaw = 180f };
            return new FitSpec();
        }

        /// <summary>The model for a role (a Poly Haven model with its own textures, or Kenney's), or null.</summary>
        public static GameObject Model(string role, out string id)
        {
            LoadManifest();
            id = null;
            if (string.IsNullOrEmpty(role)) return null;
            foreach (var e in _models)
                if (e.roles.Contains(role) || e.roles.Contains(role + ".1") || e.role.StartsWith(role + "."))
                {
                    var go = AssetDatabase.LoadAssetAtPath<GameObject>(HouseDir + "/" + e.fbx);
                    if (go != null) { id = e.id; return go; }
                }
            if (_kenney.TryGetValue(role, out var k) || _kenney.TryGetValue(role + ".2", out k))
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(k);
                if (go != null) { id = "kenney:" + Path.GetFileNameWithoutExtension(k); return go; }
            }
            return null;
        }

        /// <summary>A Lit material for a Poly Haven model, from its textures folder (albedo, GL normal, AO).</summary>
        public static Material ModelMaterial(string id, string sourceName)
        {
            string key = "model:" + id + ":" + sourceName;
            if (_cache.TryGetValue(key, out var c) && c != null) return c;
            string dir = ModelDir + "/" + id + "/textures";
            if (!AssetDatabase.IsValidFolder(dir)) return null;
            // A model with several materials names its maps after them; try the material's own name first.
            string stem = sourceName != null ? sourceName.Replace(" ", "_") : id;
            var albedo = Find(dir, stem + "_diff") ?? Find(dir, "_diff");
            if (albedo == null) return null;
            var nrm = Find(dir, stem + "_nor_gl") ?? Find(dir, "nor_gl");
            var ao = Find(dir, stem + "_ao") ?? Find(dir, "_ao");
            DreamEscapeBuilder.EnsureFolder(MatDir);
            string path = MatDir + "/VHH_Model_" + id + "_" + Safe(stem) + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find(LitShader)) { name = "VHH_Model_" + id }; AssetDatabase.CreateAsset(m, path); }
            m.SetTexture("_BaseMap", albedo);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Smoothness", 0.3f);
            if (nrm != null) { NormalImport(AssetDatabase.GetAssetPath(nrm)); m.SetTexture("_BumpMap", nrm); m.EnableKeyword("_NORMALMAP"); }
            if (ao != null) { m.SetTexture("_OcclusionMap", ao); m.EnableKeyword("_OCCLUSIONMAP"); }
            if (albedo.name.Contains("alpha") || id.Contains("shrub") || id.Contains("plant"))
            {
                m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", 0.45f); m.EnableKeyword("_ALPHATEST_ON");
                m.renderQueue = (int)RenderQueue.AlphaTest;
            }
            EditorUtility.SetDirty(m);
            _cache[key] = m;
            return m;
        }

        static string Safe(string s)
        {
            var chars = s.ToCharArray();
            for (int i = 0; i < chars.Length; i++) if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '_') chars[i] = '_';
            return new string(chars);
        }
    }

    /// <summary>A small JSON reader (objects, arrays, strings, numbers, true/false/null) for the house's asset manifest.</summary>
    public static class MiniJson
    {
        public static object Parse(string s)
        {
            int i = 0;
            return Value(s, ref i);
        }

        static void Ws(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

        static object Value(string s, ref int i)
        {
            Ws(s, ref i);
            if (i >= s.Length) return null;
            char c = s[i];
            if (c == '{')
            {
                var d = new Dictionary<string, object>();
                i++;
                Ws(s, ref i);
                if (s[i] == '}') { i++; return d; }
                while (i < s.Length)
                {
                    Ws(s, ref i);
                    string key = Str(s, ref i);
                    Ws(s, ref i);
                    i++; // :
                    d[key] = Value(s, ref i);
                    Ws(s, ref i);
                    if (s[i] == ',') { i++; continue; }
                    i++; // }
                    break;
                }
                return d;
            }
            if (c == '[')
            {
                var l = new List<object>();
                i++;
                Ws(s, ref i);
                if (s[i] == ']') { i++; return l; }
                while (i < s.Length)
                {
                    l.Add(Value(s, ref i));
                    Ws(s, ref i);
                    if (s[i] == ',') { i++; continue; }
                    i++; // ]
                    break;
                }
                return l;
            }
            if (c == '"') return Str(s, ref i);
            int start = i;
            while (i < s.Length && ",}] \t\r\n".IndexOf(s[i]) < 0) i++;
            string tok = s.Substring(start, i - start);
            if (tok == "true") return true;
            if (tok == "false") return false;
            if (tok == "null") return null;
            double.TryParse(tok, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double num);
            return num;
        }

        static string Str(string s, ref int i)
        {
            var sb = new System.Text.StringBuilder();
            i++; // "
            while (i < s.Length && s[i] != '"')
            {
                if (s[i] == '\\' && i + 1 < s.Length)
                {
                    i++;
                    char e = s[i];
                    if (e == 'u' && i + 4 < s.Length) { sb.Append((char)System.Convert.ToInt32(s.Substring(i + 1, 4), 16)); i += 4; }
                    else sb.Append(e == 'n' ? '\n' : e == 't' ? '\t' : e);
                }
                else sb.Append(s[i]);
                i++;
            }
            i++; // "
            return sb.ToString();
        }
    }
}
