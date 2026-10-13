using System;
using System.Collections.Generic;
using System.IO;
using JamKit.Look.EditorTools;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace JamKit.SkylineRepo.EditorTools
{
    /// <summary>
    /// Skyline Repo's material library (Task 21; catalogue: docs/SKYLINE-LOOK.md). Built on Visiting Hours' Look library's
    /// recipe and calls: the same texture layout (2K albedo, OpenGL normal, packed mask; tools/skyline/look/prep.py runs
    /// tools/look/prep-library.py's functions), the same metre-UV convention, and <see cref="VhLookLibrary.SetupLit"/> /
    /// <see cref="VhLookLibrary.ValidateLit"/> to set the URP Lit materials. Three sets are Visiting Hours' own prepared
    /// textures, read by path and never changed (the ceiling tiles, the pale concrete, the soil).
    /// <para>Materials are assets under Look/Library/ (SKL_&lt;name&gt;.mat; glass SK_Glass_*, glowing SK_Emissive_*, which the
    /// lint accepts untextured). They are rebuilt on every arena build, so a value tuned here reaches them. Import settings
    /// are set from code (normal maps, linear masks, the HDR sky): no hand-written .meta files.</para>
    /// </summary>
    public static class SkylineLookLibrary
    {
        public const string Root = "Assets/JamKit/Templates/SkylineRepo/";
        public const string LibTex = Root + "ThirdParty/Library/";
        public const string PropRoot = Root + "ThirdParty/Props/";
        public const string SkyRoot = Root + "ThirdParty/Sky/";
        public const string LookRoot = Root + "Look/";
        public const string MatRoot = LookRoot + "Library/";
        public const string TexRoot = LookRoot + "Textures/";
        public const string MacroPath = LibTex + "SK_MacroVariation.png";
        public const string SkyName = "Sky_Kloofendal38d_4k";
        public const string SkyHdrPath = SkyRoot + SkyName + ".hdr";
        public const string SkyInfoPath = SkyRoot + "sky.json";
        public const string SignsPath = TexRoot + "SK_Signs.png";
        public const string PostersPath = TexRoot + "SK_Posters.png";
        public const string ProductsPath = TexRoot + "SK_Products.png";
        public const string EntryMarkerPath = TexRoot + "SK_EntryMarker.png";
        public static readonly string[] FacadeKinds = { "Blue", "White", "Green", "Dark" };

        public enum Source { Library, VisitingHours, Brushed, Hazard }

        public struct Entry
        {
            public string name, texture, purpose;
            public Source source;
            /// <summary>Metres one texture repeat covers.</summary>
            public float tile;
            public Color tint;
            public float normalScale, smoothness;
            public Entry(string name, Source source, string texture, float tile, Color tint, float normalScale, float smoothness, string purpose)
            {
                this.name = name; this.source = source; this.texture = texture; this.tile = tile; this.tint = tint;
                this.normalScale = normalScale; this.smoothness = smoothness; this.purpose = purpose;
            }
        }

        static Color C(float r, float g, float b) => new Color(r, g, b, 1f);

        /// <summary>The catalogue (keep docs/SKYLINE-LOOK.md in step).</summary>
        public static readonly Entry[] Catalogue =
        {
            new Entry("Terrazzo_White", Source.Library, "Terrazzo_White", 2f, C(1f, 1f, 0.99f), 0.5f, 1.35f, "The main hall's polished white terrazzo floor and the mezzanine's deck (CONCIERGE's white)."),
            new Entry("Marble_Cream", Source.Library, "Marble_Cream", 1.5f, C(1f, 0.99f, 0.97f), 0.5f, 1.25f, "The warm-up hall's floor, kiosks, the info booth's base, planters, wainscots."),
            new Entry("Plaster_White", Source.Library, "Plaster_White", 2f, C(0.97f, 0.96f, 0.94f), 0.35f, 0.9f, "White cladding: walls, pillars, cornices, the deck's body, beams. Near-white: tint it (Variant)."),
            new Entry("Wood_Pine", Source.Library, "Wood_Pine", 1.2f, C(1.06f, 0.93f, 0.8f), 0.8f, 1f, "Food-stall walls and shop shelving: light honey pine."),
            new Entry("Wood_Cherry", Source.Library, "Wood_Cherry", 1f, C(1f, 0.95f, 0.92f), 0.6f, 1f, "Lacquered counters and display tables in the shops."),
            new Entry("Parquet_Herringbone", Source.Library, "Parquet_Herringbone", 3.4f, C(1f, 0.97f, 0.94f), 0.7f, 1f, "Shop floors behind the glass."),
            new Entry("Shutter_Painted", Source.Library, "Shutter_Painted", 2f, C(0.86f, 0.88f, 0.9f), 0.8f, 0.9f, "The warm-up hall's closed shop shutters (painted pale grey). Near-white: tint it."),
            new Entry("Ceiling_Tile", Source.VisitingHours, "Ceiling_Tile", 2.4f, C(1f, 1f, 1f), 0.8f, 0.9f, "Ceilings: the mezzanine's soffit, shop ceilings (Visiting Hours' prepared set, ACG OfficeCeiling001)."),
            new Entry("Concrete", Source.VisitingHours, "Concrete_Kerb", 2f, C(0.92f, 0.92f, 0.93f), 1f, 0.8f, "Pale concrete: shop roofs, the city's ground outside, the monorail (Visiting Hours' Concrete_Kerb, PH concrete_floor_02)."),
            new Entry("Soil", Source.VisitingHours, "Dirt", 1.5f, C(0.7f, 0.62f, 0.55f), 1f, 0.4f, "Planter soil (Visiting Hours' Dirt, PH forest_ground_04), darker and finer."),
            new Entry("Steel_Brushed", Source.Brushed, "SK_BrushedSteel", 1f, C(0.86f, 0.87f, 0.89f), 0.5f, 1f, "Brushed stainless: shopfront frames, mullions, rails, kick plates (procedural)."),
            new Entry("Gold_Brushed", Source.Brushed, "SK_BrushedSteel", 1f, C(1f, 0.78f, 0.43f), 0.5f, 1.05f, "CONCIERGE's gold: reveals, capitals, frames, inlay strips (the brushed maps under a gold tint)."),
            new Entry("Steel_Dark", Source.Brushed, "SK_BrushedSteel", 1f, C(0.2f, 0.21f, 0.23f), 0.5f, 0.85f, "Graphite metal: fascia bands, skirting, transoms, sign frames."),
            new Entry("Hazard_Orange", Source.Hazard, "SK_Hazard", 1f, C(1f, 1f, 1f), 0.4f, 1f, "Kestrel & Daughters' orange with black hazard stripes: the firm's kit only (the practice gantry)."),
        };

        static readonly Dictionary<string, Material> _built = new Dictionary<string, Material>();

        /// <summary>Starts a build: every material is set again on its first use.</summary>
        public static void BeginBuild() => _built.Clear();

        public static bool TryFind(string name, out Entry e)
        {
            foreach (var x in Catalogue)
                if (string.Equals(x.name, name, StringComparison.OrdinalIgnoreCase)) { e = x; return true; }
            e = default;
            return false;
        }

        static Texture2D Load(string path) => AssetDatabase.LoadAssetAtPath<Texture2D>(path);

        static void Paths(Entry e, out string albedo, out string normal, out string mask)
        {
            switch (e.source)
            {
                case Source.VisitingHours:
                    string vd = VhLookLibrary.TexRoot + e.texture + "/" + e.texture;
                    albedo = vd + "_Albedo.jpg"; normal = vd + "_Normal.jpg"; mask = vd + "_Mask.png";
                    return;
                case Source.Brushed:
                    albedo = TexRoot + "SK_BrushedSteel_Albedo.png"; normal = TexRoot + "SK_BrushedSteel_Normal.png"; mask = TexRoot + "SK_BrushedSteel_Mask.png";
                    return;
                case Source.Hazard:
                    albedo = TexRoot + "SK_Hazard_Albedo.png"; normal = TexRoot + "SK_BrushedSteel_Normal.png"; mask = TexRoot + "SK_Hazard_Mask.png";
                    return;
                default:
                    string d = LibTex + e.texture + "/" + e.texture;
                    albedo = d + "_Albedo.jpg"; normal = d + "_Normal.jpg"; mask = d + "_Mask.png";
                    return;
            }
        }

        static Material Asset(string path, Shader shader, string name)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader) { name = name };
                VhLookLibrary.EnsureAssetFolder(path);
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != shader) m.shader = shader;
            return m;
        }

        static Shader Lit => Shader.Find("Universal Render Pipeline/Lit");
        static Shader Unlit => Shader.Find("Universal Render Pipeline/Unlit");

        /// <summary>The library material by name (catalogue above). Logs and returns null for an unknown name.</summary>
        public static Material Get(string name) => Variant(name, null, Color.white);

        /// <summary>A tinted variant (SKL_&lt;name&gt;__&lt;variant&gt;.mat): the tint multiplies the entry's own (shop wall colours).</summary>
        public static Material Variant(string name, string variant, Color tint)
        {
            string key = variant == null ? name : name + "__" + variant;
            if (_built.TryGetValue(key, out var cached) && cached != null) return cached;
            if (!TryFind(name, out var e))
            {
                Debug.LogError("SKYLINE_LOOK unknown library material: " + name);
                return null;
            }
            Paths(e, out var albedo, out var normal, out var mask);
            var m = Asset(MatRoot + "SKL_" + key + ".mat", Lit, "SKL_" + key);
            VhLookLibrary.SetupLit(m, Load(albedo), Load(normal), Load(mask), e.tint * tint, e.normalScale, e.smoothness, 1f, 1f / e.tile, false);
            Macro(m);
            EditorUtility.SetDirty(m);
            _built[key] = m;
            if (m.GetTexture("_BaseMap") == null) Debug.LogWarning("SKYLINE_LOOK " + m.name + " has no albedo (missing " + albedo + ")");
            return m;
        }

        /// <summary>Skyline's own low-frequency colour variation (the detail map, about seven repeats wide), breaking the tiling
        /// at a distance (Visiting Hours' recipe, Skyline's own noise file).</summary>
        static void Macro(Material m)
        {
            var macro = Load(MacroPath);
            if (macro == null) { m.DisableKeyword("_DETAIL_MULX2"); return; }
            m.SetTexture("_DetailAlbedoMap", macro);
            m.SetTextureScale("_DetailAlbedoMap", new Vector2(0.137f, 0.137f));
            m.SetFloat("_DetailAlbedoMapScale", 1f);
            m.EnableKeyword("_DETAIL_MULX2");
        }

        /// <summary>A plain textured Lit material for atlas quads and one-off textures (UVs set by the mesh, tiling 1).</summary>
        public static Material Atlas(string name, string texturePath, float smoothness, Color tint)
        {
            string key = "atlas:" + name;
            if (_built.TryGetValue(key, out var cached) && cached != null) return cached;
            var m = Asset(MatRoot + "SKL_" + name + ".mat", Lit, "SKL_" + name);
            VhLookLibrary.SetupLit(m, Load(texturePath), null, null, tint, 1f, 1f, 1f, 1f, false);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(m);
            _built[key] = m;
            return m;
        }

        public static Material Products() => Atlas("Products", ProductsPath, 0.45f, Color.white);
        public static Material EntryMarker() => Atlas("EntryMarker", EntryMarkerPath, 0.55f, Color.white);

        /// <summary>
        /// A glowing material (URP Unlit, HDR so bloom catches it), named SK_Emissive_&lt;name&gt;. With a texture it shows it
        /// (the sign and poster atlases); the colour multiplies it. Material colours are given in sRGB, as everywhere in this
        /// project (Unity converts them), so 1.5 here is about 2.4 in the linear image.
        /// </summary>
        public static Material Emissive(string name, string texturePath, Color hdr)
        {
            string key = "emissive:" + name;
            if (_built.TryGetValue(key, out var cached) && cached != null) return cached;
            var m = Asset(MatRoot + "SK_Emissive_" + name + ".mat", Unlit, "SK_Emissive_" + name);
            var tex = texturePath != null ? Load(texturePath) : null;
            m.SetTexture("_BaseMap", tex);
            m.SetTexture("_MainTex", tex);
            m.SetColor("_BaseColor", hdr);
            m.SetColor("_Color", hdr);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            _built[key] = m;
            return m;
        }

        public static Material Signs() => Emissive("Signs", SignsPath, new Color(1.45f, 1.45f, 1.45f));
        public static Material Posters() => Emissive("Posters", PostersPath, new Color(1.25f, 1.25f, 1.25f));
        public static Material LightPanel() => Emissive("LightPanel", null, new Color(2.1f, 2.05f, 1.9f));
        public static Material GoldGlow() => Emissive("GoldGlow", null, new Color(1.9f, 1.45f, 0.75f));

        /// <summary>
        /// Glass (named SK_Glass_&lt;name&gt;): URP Lit, transparent, very smooth, specular preserved so it reflects the
        /// arena's reflection probe and the sky and still shows what is behind it (Visiting Hours' window recipe).
        /// </summary>
        public static Material Glass(string name, Color color, float smoothness = 0.95f)
        {
            string key = "glass:" + name;
            if (_built.TryGetValue(key, out var cached) && cached != null) return cached;
            var m = Asset(MatRoot + "SK_Glass_" + name + ".mat", Lit, "SK_Glass_" + name);
            m.SetColor("_BaseColor", color);
            m.SetColor("_Color", color);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_BlendModePreserveSpecular", 1f);
            m.SetFloat("_EnvironmentReflections", 1f);
            m.SetFloat("_SpecularHighlights", 1f);
            m.SetFloat("_Cull", (float)CullMode.Back);   // glass is built as thin boxes: one face towards the viewer, not two
            m.SetFloat("_ZWrite", 0f);
            VhLookLibrary.ValidateLit(m);
            m.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            _built[key] = m;
            return m;
        }

        /// <summary>
        /// A far tower's curtain wall, hazed: aerial perspective baked into a Lit material, so it costs nothing at run time and
        /// needs no renderer feature. The surface is scaled by (1 - haze) and the haze colour added as emission
        /// (final = surface x (1 - h) + haze x h), so far towers fade into the sky's own horizon colour.
        /// </summary>
        public static Material Facade(string kind, int band, float haze, Color hazeLinear) =>
            Hazed("Facade_" + kind + "_Haze" + band, TexRoot + "SK_Facade_" + kind + ".png", 1f / 13f, haze, hazeLinear, Color.white, 0.35f);

        /// <summary>The city's ground outside (Visiting Hours' pale concrete, 2 m a repeat), hazed the same way.</summary>
        public static Material HazedGround(float haze, Color hazeLinear) =>
            Hazed("Ground_Hazed", VhLookLibrary.TexRoot + "Concrete_Kerb/Concrete_Kerb_Albedo.jpg", 0.5f, haze, hazeLinear, new Color(0.9f, 0.9f, 0.9f), 0.2f);

        static Material Hazed(string name, string texture, float tiling, float haze, Color hazeLinear, Color tint, float smoothness)
        {
            string key = "hazed:" + name;
            if (_built.TryGetValue(key, out var cached) && cached != null) return cached;
            var m = Asset(MatRoot + "SKL_" + name + ".mat", Lit, "SKL_" + name);
            float keep = 1f - haze;
            VhLookLibrary.SetupLit(m, Load(texture), null, null, new Color(tint.r * keep, tint.g * keep, tint.b * keep, 1f).gamma, 1f, 1f, 1f, tiling, false);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_EnvironmentReflections", 0f);
            m.SetFloat("_ReceiveShadows", 0f);
            m.SetColor("_EmissionColor", (hazeLinear * haze).gamma);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            VhLookLibrary.ValidateLit(m);
            m.EnableKeyword("_EMISSION");
            m.EnableKeyword("_RECEIVE_SHADOWS_OFF");
            EditorUtility.SetDirty(m);
            _built[key] = m;
            return m;
        }

        // ---------------------------------------------------------------------------------------------------- props

        /// <summary>The library's props (Poly Haven models, CC0): name -> (alpha-clipped leaves?).</summary>
        public static readonly string[] PropNames = { "Calathea", "Fern", "WetFloorSign" };
        static bool IsPlant(string name) => name == "Calathea" || name == "Fern";

        public static Material PropMaterial(string name)
        {
            string key = "prop:" + name;
            if (_built.TryGetValue(key, out var cached) && cached != null) return cached;
            string d = PropRoot + name + "/" + name;
            var m = Asset(MatRoot + "SKL_Prop_" + name + ".mat", Lit, "SKL_Prop_" + name);
            VhLookLibrary.SetupLit(m, Load(d + "_Albedo.png"), Load(d + "_Normal.jpg"), Load(d + "_Mask.png"), Color.white, 1f, 1f, 1f, 1f, false);
            if (IsPlant(name))
            {
                m.SetFloat("_AlphaClip", 1f);
                m.SetFloat("_Cutoff", 0.4f);
                m.SetFloat("_Cull", (float)CullMode.Off);
                VhLookLibrary.ValidateLit(m);
                m.EnableKeyword("_ALPHATEST_ON");
            }
            EditorUtility.SetDirty(m);
            _built[key] = m;
            return m;
        }

        /// <summary>
        /// Places a library prop scaled so its height is <paramref name="height"/> m, standing on <paramref name="position"/>
        /// (its lowest point there), turned <paramref name="yaw"/>. Visual only (no collider; the caller keeps it out of the
        /// play space or under a SkylineNoCollider root). Returns null if the model isn't imported yet.
        /// </summary>
        public static GameObject Prop(string name, Transform parent, Vector3 position, float yaw, float height)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(PropRoot + name + "/" + name + ".fbx");
            if (model == null) { Debug.LogWarning("SKYLINE_LOOK prop not imported: " + name); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
            go.name = name;
            if (parent != null) go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = Vector3.one;
            var m = PropMaterial(name);
            Bounds bounds = default;
            bool any = false;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = m;
                r.sharedMaterials = mats;
                r.shadowCastingMode = ShadowCastingMode.On;
                if (!any) { bounds = r.bounds; any = true; } else bounds.Encapsulate(r.bounds);
            }
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(c);
            if (any && bounds.size.y > 1e-3f)
            {
                float s = height / bounds.size.y;
                go.transform.localScale = Vector3.one * s;
                // Re-measure after scaling and stand it on the point.
                bool first = true;
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    if (first) { bounds = r.bounds; first = false; } else bounds.Encapsulate(r.bounds);
                }
                go.transform.position = position + new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
            }
            else go.transform.position = position;
            return go;
        }

        // ---------------------------------------------------------------------------------------------------- the sky

        [Serializable]
        public sealed class SkyInfo
        {
            public float sun_elevation = 38f, sun_azimuth = 36f;
            public float[] sky_average = { 0.45f, 0.49f, 0.55f }, horizon = { 0.39f, 0.42f, 0.45f }, ground = { 0.24f, 0.25f, 0.28f };
            public Color Average => Col(sky_average);
            public Color Horizon => Col(horizon);
            public Color Ground => Col(ground);
            static Color Col(float[] a) => a != null && a.Length >= 3 ? new Color(a[0], a[1], a[2], 1f) : Color.grey;
        }

        /// <summary>The afternoon sky's measurements (tools/look/hdri-prep.py: the sun's line, the sky's colours).</summary>
        public static SkyInfo ReadSkyInfo()
        {
            try
            {
                if (File.Exists(SkyInfoPath)) return JsonUtility.FromJson<SkyInfo>(File.ReadAllText(SkyInfoPath)) ?? new SkyInfo();
            }
            catch (Exception e) { Debug.LogWarning("SKYLINE_LOOK sky.json unreadable: " + e.Message); }
            return new SkyInfo();
        }

        /// <summary>The sky's exposure: normalised to the same brightness as Visiting Hours' skies (luminance ~0.94).</summary>
        public static float SkyExposure(SkyInfo s)
        {
            var a = s.Average;
            float lum = a.r * 0.2126f + a.g * 0.7152f + a.b * 0.0722f;
            return Mathf.Clamp(0.94f / Mathf.Max(lum, 1e-3f), 0.5f, 4f);
        }

        /// <summary>The sky's tint (bluer than Visiting Hours' golden hour: a clear afternoon).</summary>
        public static readonly Color SkyTint = new Color(0.86f, 0.9f, 1f, 1f);

        /// <summary>The haze colour (linear) the far towers fade into: the sky's horizon at its exposure and tint.</summary>
        public static Color HazeLinear(SkyInfo s)
        {
            var h = Color.Lerp(s.Horizon, s.Average, 0.35f) * SkyExposure(s);
            return new Color(h.r * SkyTint.r, h.g * SkyTint.g, h.b * SkyTint.b, 1f);
        }

        /// <summary>
        /// The afternoon sky on Visiting Hours' sky shader (JamKit/Look/Sky: the HDRI turned so its sun sits on the light, a
        /// drawn disc and glow, slow cloud drift), as Look/SK_Sky_Kloofendal38d.mat. The values follow VhLookSky.Material's
        /// recipe, made bluer and crisper for a clear afternoon.
        /// </summary>
        public static Material SkyMaterial(SkyInfo s)
        {
            var shader = Shader.Find("JamKit/Look/Sky");
            if (shader == null) throw new InvalidOperationException("JamKit/Look/Sky shader not found");
            var m = Asset(LookRoot + "SK_Sky_Kloofendal38d.mat", shader, "SK_Sky_Kloofendal38d");
            m.SetTexture("_SkyTex", Load(SkyHdrPath));
            float exposure = SkyExposure(s);
            m.SetFloat("_Exposure", exposure);
            m.SetColor("_HorizonColor", s.Horizon * exposure);
            m.SetColor("_GroundColor", Color.Lerp(s.Ground, new Color(0.46f, 0.45f, 0.44f), 0.5f) * exposure * 0.85f);
            m.SetColor("_Tint", SkyTint);
            m.SetColor("_WarmSide", new Color(1f, 0.99f, 0.97f, 1f));
            m.SetFloat("_WarmFocus", 4f);
            m.SetFloat("_SunSideDim", 0.8f);
            m.SetFloat("_HorizonHaze", 0.18f);
            m.SetFloat("_HorizonHeight", 0.04f);
            m.SetFloat("_Saturation", 1.2f);
            m.SetColor("_SunDiscColor", new Color(1f, 0.96f, 0.88f, 1f));
            m.SetFloat("_SunDiscIntensity", 140f);
            m.SetFloat("_SunDiscSize", 0.5f);
            m.SetColor("_SunGlowColor", new Color(1f, 0.92f, 0.78f, 1f));
            m.SetFloat("_SunGlow", 1f);
            m.SetFloat("_SunGlowFalloff", 500f);
            m.SetFloat("_SunHalo", 0.05f);
            m.SetFloat("_SunHaloFalloff", 14f);
            m.SetFloat("_DriftDegrees", 1.2f);
            EditorUtility.SetDirty(m);
            return m;
        }

        // ---------------------------------------------------------------------------------------------------- imports

        /// <summary>Sets every Skyline look file's import settings (normal maps, linear masks, alpha for the leaves, the HDR
        /// sky) and reimports the ones that changed. Visiting Hours' files are left as they are.</summary>
        public static void ConfigureImporters()
        {
            var changed = new List<string>();
            foreach (var e in Catalogue)
            {
                if (e.source != Source.Library) continue;
                string d = LibTex + e.texture + "/" + e.texture;
                Tex(d + "_Albedo.jpg", true, false, 2048, true, false, changed);
                Tex(d + "_Normal.jpg", false, true, 2048, true, false, changed);
                Tex(d + "_Mask.png", false, false, 1024, true, false, changed);
            }
            Tex(MacroPath, true, false, 512, true, false, changed);
            Tex(TexRoot + "SK_BrushedSteel_Albedo.png", true, false, 1024, true, false, changed);
            Tex(TexRoot + "SK_BrushedSteel_Normal.png", false, true, 1024, true, false, changed);
            Tex(TexRoot + "SK_BrushedSteel_Mask.png", false, false, 1024, true, false, changed);
            Tex(TexRoot + "SK_Hazard_Albedo.png", true, false, 1024, true, false, changed);
            Tex(TexRoot + "SK_Hazard_Mask.png", false, false, 1024, true, false, changed);
            Tex(SignsPath, true, false, 2048, false, false, changed, TextureImporterCompression.CompressedHQ);
            Tex(PostersPath, true, false, 2048, false, false, changed);
            Tex(ProductsPath, true, false, 1024, true, false, changed);
            Tex(EntryMarkerPath, true, false, 1024, false, false, changed, TextureImporterCompression.CompressedHQ);
            foreach (var k in FacadeKinds) Tex(TexRoot + "SK_Facade_" + k + ".png", true, false, 512, true, false, changed);
            foreach (var p in PropNames)
            {
                string d = PropRoot + p + "/" + p;
                Tex(d + "_Albedo.png", true, false, 1024, true, IsPlant(p), changed);
                Tex(d + "_Normal.jpg", false, true, 1024, true, false, changed);
                Tex(d + "_Mask.png", false, false, 1024, true, false, changed);
                Model(d + ".fbx", changed);
            }
            Sky(changed);
            if (changed.Count > 0) Debug.Log("SKYLINE_LOOK import settings set on " + changed.Count + " files");
        }

        static void Tex(string path, bool srgb, bool normal, int maxSize, bool repeat, bool alphaClip, List<string> changed,
            TextureImporterCompression compression = TextureImporterCompression.Compressed)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) { Debug.LogWarning("SKYLINE_LOOK missing texture (run tools/skyline/look/*.py?): " + path); return; }
            var type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            var wrap = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            bool dirty = ti.textureType != type || ti.sRGBTexture != srgb || ti.maxTextureSize != maxSize || ti.anisoLevel != 8 ||
                         !ti.mipmapEnabled || ti.wrapMode != wrap || ti.textureCompression != compression ||
                         (alphaClip && (!ti.alphaIsTransparency || !ti.mipMapsPreserveCoverage));
            if (!dirty) return;
            ti.textureType = type;
            ti.sRGBTexture = srgb;
            ti.maxTextureSize = maxSize;
            ti.anisoLevel = 8;
            ti.mipmapEnabled = true;
            ti.wrapMode = wrap;
            ti.filterMode = FilterMode.Trilinear;
            ti.textureCompression = compression;
            if (alphaClip)
            {
                ti.alphaSource = TextureImporterAlphaSource.FromInput;
                ti.alphaIsTransparency = true;
                ti.mipMapsPreserveCoverage = true;
                ti.alphaTestReferenceValue = 0.4f;
            }
            ti.SaveAndReimport();
            changed.Add(path);
        }

        static void Model(string path, List<string> changed)
        {
            var mi = AssetImporter.GetAtPath(path) as ModelImporter;
            if (mi == null) { Debug.LogWarning("SKYLINE_LOOK missing model: " + path); return; }
            if (mi.materialImportMode == ModelImporterMaterialImportMode.None && !mi.importAnimation && !mi.importCameras && !mi.importLights) return;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.importAnimation = false;
            mi.importCameras = false;
            mi.importLights = false;
            mi.SaveAndReimport();
            changed.Add(path);
        }

        static void Sky(List<string> changed)
        {
            var ti = AssetImporter.GetAtPath(SkyHdrPath) as TextureImporter;
            if (ti == null) { Debug.LogWarning("SKYLINE_LOOK missing sky: " + SkyHdrPath); return; }
            if (ti.textureShape == TextureImporterShape.Texture2D && !ti.mipmapEnabled && ti.maxTextureSize == 4096 &&
                ti.textureCompression == TextureImporterCompression.CompressedHQ && ti.wrapModeV == TextureWrapMode.Clamp) return;
            ti.textureType = TextureImporterType.Default;
            ti.textureShape = TextureImporterShape.Texture2D;
            ti.mipmapEnabled = false;
            ti.wrapModeU = TextureWrapMode.Repeat;
            ti.wrapModeV = TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Bilinear;
            ti.maxTextureSize = 4096;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.SaveAndReimport();
            changed.Add(SkyHdrPath);
        }
    }
}
