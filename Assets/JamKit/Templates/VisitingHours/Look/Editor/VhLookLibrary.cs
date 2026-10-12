using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace JamKit.Look.EditorTools
{
    /// <summary>
    /// The Look library: Visiting Hours' shared PBR materials (2K CC0 sets from Poly Haven and ambientCG, prepared by
    /// tools/look/prep-library.py into ThirdParty/LookLibrary/), terrain layers, the sky materials, the foliage and bark,
    /// and the props. Other agents pick materials by name: <c>VhLookLibrary.Get("Brick_Red")</c> (or <c>VhLook.Material</c>).
    /// <para><b>UV convention: 1 UV unit = 1 metre.</b> Each material's tiling is set from its real-world size (Entry.tile), so
    /// a mesh with metre UVs (<see cref="VhLookUv.BoxProject"/>, or ProBuilder's world-space auto UVs at scale 1) shows the
    /// texture at its true size. Catalogue: docs/redesign/look-library.md.</para>
    /// <para>Textures are found by path and their import settings are set here (normal maps, linear masks, coverage-preserving
    /// mips for the leaves), so Unity's own .meta files are all that's needed: none are hand-written.</para>
    /// </summary>
    public static class VhLookLibrary
    {
        public const string VhRoot = "Assets/JamKit/Templates/VisitingHours/";
        public const string TexRoot = VhRoot + "ThirdParty/LookLibrary/";
        public const string SkyRoot = VhRoot + "ThirdParty/LookSky/";
        public const string PropRoot = VhRoot + "ThirdParty/LookProps/";
        public const string LookRoot = VhRoot + "Look/";
        public const string MatRoot = LookRoot + "Library/";
        public const string MacroPath = TexRoot + "VH_MacroVariation.png";
        public const string LeafAtlasPath = LookRoot + "Textures/VH_LeafClusters.png";

        public struct Entry
        {
            public string name;
            public string purpose;
            /// <summary>Metres one texture repeat covers.</summary>
            public float tile;
            public Color tint;
            public float normalScale;
            /// <summary>Multiplies the mask's smoothness (URP Lit's _Smoothness).</summary>
            public float smoothness;
            public float occlusion;
            public bool terrainLayer;
            public Entry(string name, float tile, string purpose, Color tint, float normalScale = 1f, float smoothness = 1f,
                float occlusion = 1f, bool terrainLayer = false)
            {
                this.name = name; this.tile = tile; this.purpose = purpose; this.tint = tint;
                this.normalScale = normalScale; this.smoothness = smoothness; this.occlusion = occlusion; this.terrainLayer = terrainLayer;
            }
            /// <summary>Texels per metre of the 2K albedo.</summary>
            public float TexelDensity => 2048f / tile;
        }

        static Color C(float r, float g, float b) => new Color(r, g, b, 1f);

        /// <summary>The catalogue (keep docs/redesign/look-library.md in step).</summary>
        public static readonly Entry[] Catalogue =
        {
            new Entry("Asphalt", 4f, "Roads and driveways: worn asphalt with hairline cracks.", C(0.92f, 0.9f, 0.88f), 1f, 0.9f),
            new Entry("Road_LaneLine", 4f, "A road lane with worn white dashed centre line (run the dashes along V).", C(0.92f, 0.9f, 0.88f), 1f, 0.9f),
            new Entry("Concrete_Sidewalk", 3f, "Sidewalks and paths: square slabs with joints.", C(1f, 0.97f, 0.93f), 1f, 0.8f),
            new Entry("Concrete_Kerb", 2f, "Kerbs, steps, retaining-wall caps, plinths: smooth weathered concrete.", C(1f, 0.97f, 0.94f), 1f, 0.8f),
            new Entry("Grass_Lawn", 2.5f, "Mown lawns and verges (late summer).", C(0.95f, 1f, 0.82f), 1f, 0.6f, 1f, true),
            new Entry("Grass_DirtMix", 3f, "Worn grass: paths across lawns, verges, under trees, field edges.", C(1f, 1f, 0.9f), 1f, 0.6f, 1f, true),
            new Entry("Dirt", 3f, "Bare earth with pebbles: flower beds, the creek bank, the bike yard.", C(1f, 0.95f, 0.88f), 1f, 0.7f, 1f, true),
            new Entry("Gravel", 2f, "Gravel driveways, the creek path, roof ballast.", C(0.96f, 0.92f, 0.86f), 1f, 0.7f, 1f, true),
            new Entry("Brick_Red", 2f, "Red brick walls: shopfronts, chimneys, the school.", C(1f, 0.96f, 0.92f), 1f, 0.8f),
            new Entry("Brick_Dark", 2f, "Older dark brick: the church, the hospital's base, garden walls.", C(1f, 0.96f, 0.92f), 1f, 0.8f),
            new Entry("Siding_WoodBrown", 2f, "Stained wooden lap siding (natural brown houses, garages).", C(1f, 0.95f, 0.9f), 1f, 0.9f),
            new Entry("Siding_WoodRed", 2f, "Barn-red wooden siding (sheds, Walt's shop).", C(1f, 0.95f, 0.92f), 1f, 0.9f),
            new Entry("Clapboard_Painted", 2f, "Painted clapboard: the street's houses. Near-white: set the paint colour with Tinted().", C(0.93f, 0.91f, 0.86f), 1f, 1f),
            new Entry("Roof_ShinglesAsphalt", 2.5f, "Asphalt roof shingles (most houses).", C(0.95f, 0.92f, 0.92f), 1f, 0.8f),
            new Entry("Roof_ShinglesSlate", 2.5f, "Slate-grey shingles (the church, older houses).", C(1f, 1f, 1f), 1f, 0.8f),
            new Entry("Stucco", 2f, "Stucco walls (bungalows, the strip's shops). Near-white: tint it.", C(0.95f, 0.9f, 0.82f), 1f, 1f),
            new Entry("Deck_Planks", 2f, "Decks, porches, the bridge's boards.", C(1f, 0.96f, 0.9f), 1f, 0.9f),
            new Entry("Fence_Planks", 2f, "Fences and gates (vertical boards: run V up the fence).", C(1f, 0.96f, 0.9f), 1f, 0.9f),
            new Entry("Galvanized_Corrugated", 2f, "Corrugated galvanized sheet: sheds, the bike shop's lean-to, culverts.", C(1f, 1f, 1f), 1f, 1f),
            new Entry("Creek_Pebbles", 2f, "The creek bed and its shallows.", C(1f, 1f, 1f), 1f, 0.9f, 1f, true),
            new Entry("Creek_Rock", 3f, "Boulders, rock outcrops, the creek's banks.", C(1f, 0.97f, 0.94f), 1f, 0.8f, 1f, true),
            new Entry("Tree_Bark", 1.5f, "Tree trunks and branches (VhLookTrees uses it).", C(1f, 1f, 1f), 1.2f, 0.7f),
            new Entry("Hospital_Terrazzo", 2f, "The hospital's floors (terrazzo).", C(1f, 1f, 1f), 0.6f, 1.15f),
            new Entry("Ceiling_Tile", 2.4f, "Suspended ceiling tiles 0.6 m (the ward, offices).", C(1f, 1f, 1f), 0.8f, 0.9f),
            new Entry("Plaster_Painted", 2.5f, "Painted plaster walls, exterior or interior. Near-white: tint it.", C(0.94f, 0.92f, 0.88f), 0.8f, 1f),
            new Entry("Plaster_Interior", 2.5f, "Smooth interior walls (the house, the ward). Near-white: tint it.", C(0.94f, 0.92f, 0.88f), 0.6f, 1f),
        };

        public static bool TryFind(string name, out Entry entry)
        {
            foreach (var e in Catalogue)
                if (string.Equals(e.name, name, StringComparison.OrdinalIgnoreCase)) { entry = e; return true; }
            entry = default;
            return false;
        }

        /// <summary>Metres one repeat of the material covers (1 if unknown).</summary>
        public static float TileMetres(string name) => TryFind(name, out var e) ? e.tile : 1f;

        /// <summary>Makes sure the asset path's folder exists in the AssetDatabase (a folder made only on disk isn't
        /// known to Unity until a refresh, and CreateAsset into it fails: the scene rebuild on a fresh checkout, 2026-10-12).</summary>
        public static void EnsureAssetFolder(string assetPath)
        {
            string folder = System.IO.Path.GetDirectoryName(assetPath).Replace('\\', '/');
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder)) return;
            string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureAssetFolder(folder);   // its parent first
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(folder));
        }

        public static string MaterialPath(string name) => MatRoot + "VHM_" + name + ".mat";

        /// <summary>The library material by name (built on first use). Logs and returns null for an unknown name.</summary>
        public static Material Get(string name)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath(name));
            if (m != null) return m;
            if (!TryFind(name, out var e))
            {
                Debug.LogError("JAMKIT_LOOK unknown library material: " + name);
                return null;
            }
            ConfigureImporters();
            return BuildMaterial(e);
        }

        /// <summary>
        /// A tinted variant of a library material (a house's paint colour), saved as VHM_&lt;name&gt;__&lt;variant&gt;.mat so it
        /// batches with others of the same colour. The tint multiplies the entry's own.
        /// </summary>
        public static Material Tinted(string name, string variant, Color tint)
        {
            string path = MatRoot + "VHM_" + name + "__" + variant + ".mat";
            var baseMat = Get(name);
            if (baseMat == null) return null;
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(baseMat);
                { VhLookLibrary.EnsureAssetFolder(path); AssetDatabase.CreateAsset(m, path); }
            }
            else m.CopyPropertiesFromMaterial(baseMat);
            TryFind(name, out var e);
            m.SetColor("_BaseColor", e.tint * tint);
            EditorUtility.SetDirty(m);
            return m;
        }

        public static TerrainLayer GetTerrainLayer(string name)
        {
            string path = MatRoot + "VHT_" + name + ".terrainlayer";
            var t = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (t != null) return t;
            if (!TryFind(name, out var e)) return null;
            ConfigureImporters();
            return BuildTerrainLayer(e);
        }

        [MenuItem("JamKit/Look/Build the Look Library")]
        public static void Build()
        {
            Directory.CreateDirectory(MatRoot);
            ConfigureImporters();
            foreach (var e in Catalogue)
            {
                BuildMaterial(e);
                if (e.terrainLayer) BuildTerrainLayer(e);
            }
            BuildBarkAndFoliage();
            BuildProps();
            VhLookSky.BuildAll();
            AssetDatabase.SaveAssets();
            Debug.Log("JAMKIT_LOOK library built: " + Catalogue.Length + " materials in " + MatRoot);
        }

        // ---------- Import settings ----------

        public static void ConfigureImporters()
        {
            Directory.CreateDirectory(MatRoot);
            var changed = new List<string>();
            foreach (var e in Catalogue)
            {
                string d = TexRoot + e.name + "/" + e.name;
                Texture(d + "_Albedo.jpg", true, false, 2048, changed);
                Texture(d + "_Normal.jpg", false, true, 2048, changed);
                Texture(d + "_Mask.png", false, false, 1024, changed);
            }
            Texture(MacroPath, true, false, 512, changed);
            foreach (var p in PropNames)
            {
                string d = PropRoot + p + "/" + p;
                Texture(d + "_Albedo.jpg", true, false, 2048, changed);
                Texture(d + "_Normal.jpg", false, true, 2048, changed);
                Texture(d + "_Mask.png", false, false, 2048, changed);
                Model(d + ".fbx", changed);
            }
            Leaves(changed);
            VhLookSky.ConfigureImporters(changed);
            if (changed.Count > 0) Debug.Log("JAMKIT_LOOK import settings set on " + changed.Count + " files");
        }

        static void Texture(string path, bool srgb, bool normal, int maxSize, List<string> changed)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null)
            {
                Debug.LogWarning("JAMKIT_LOOK missing texture (run tools/look/prep-library.py?): " + path);
                return;
            }
            var type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            bool dirty = ti.textureType != type || ti.sRGBTexture != srgb || ti.maxTextureSize != maxSize || ti.anisoLevel != 8 ||
                         !ti.mipmapEnabled || ti.wrapMode != TextureWrapMode.Repeat || ti.textureCompression != TextureImporterCompression.Compressed;
            if (!dirty) return;
            ti.textureType = type;
            ti.sRGBTexture = srgb;
            ti.maxTextureSize = maxSize;
            ti.anisoLevel = 8;
            ti.mipmapEnabled = true;
            ti.wrapMode = TextureWrapMode.Repeat;
            ti.filterMode = FilterMode.Trilinear;
            ti.textureCompression = TextureImporterCompression.Compressed;
            ti.SaveAndReimport();
            changed.Add(path);
        }

        static void Leaves(List<string> changed)
        {
            var ti = AssetImporter.GetAtPath(LeafAtlasPath) as TextureImporter;
            if (ti == null) { Debug.LogWarning("JAMKIT_LOOK missing leaf atlas: " + LeafAtlasPath); return; }
            if (ti.mipMapsPreserveCoverage && ti.alphaIsTransparency && Mathf.Approximately(ti.alphaTestReferenceValue, 0.45f)) return;
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = true;
            ti.alphaSource = TextureImporterAlphaSource.FromInput;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = true;
            ti.mipMapsPreserveCoverage = true;
            ti.alphaTestReferenceValue = 0.45f;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.maxTextureSize = 1024;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.SaveAndReimport();
            changed.Add(LeafAtlasPath);
        }

        static void Model(string path, List<string> changed)
        {
            var mi = AssetImporter.GetAtPath(path) as ModelImporter;
            if (mi == null) { Debug.LogWarning("JAMKIT_LOOK missing model: " + path); return; }
            if (mi.materialImportMode == ModelImporterMaterialImportMode.None && !mi.importAnimation) return;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.importAnimation = false;
            mi.importCameras = false;
            mi.importLights = false;
            mi.SaveAndReimport();
            changed.Add(path);
        }

        // ---------- Materials ----------

        static Shader Lit => Shader.Find("Universal Render Pipeline/Lit");

        static Material BuildMaterial(Entry e)
        {
            string d = TexRoot + e.name + "/" + e.name;
            var path = MaterialPath(e.name);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool fresh = m == null;
            if (fresh) m = new Material(Lit) { name = "VHM_" + e.name };
            SetupLit(m, Load(d + "_Albedo.jpg"), Load(d + "_Normal.jpg"), Load(d + "_Mask.png"), e.tint, e.normalScale,
                e.smoothness, e.occlusion, 1f / e.tile, true);
            if (fresh) { VhLookLibrary.EnsureAssetFolder(path); AssetDatabase.CreateAsset(m, path); }
            EditorUtility.SetDirty(m);
            return m;
        }

        static Texture2D Load(string path) => AssetDatabase.LoadAssetAtPath<Texture2D>(path);

        /// <summary>Sets a URP Lit material from the library's texture layout (albedo, normal, packed mask).</summary>
        public static void SetupLit(Material m, Texture albedo, Texture normal, Texture mask, Color tint, float normalScale,
            float smoothness, float occlusion, float tiling, bool macro)
        {
            if (m.shader != Lit) m.shader = Lit;
            var st = new Vector2(tiling, tiling);
            m.SetTexture("_BaseMap", albedo);
            m.SetTextureScale("_BaseMap", st);
            m.SetTexture("_MainTex", albedo);
            m.SetTextureScale("_MainTex", st);
            m.SetColor("_BaseColor", tint);
            m.SetColor("_Color", tint);
            m.SetFloat("_WorkflowMode", 1f);
            m.SetFloat("_Surface", 0f);
            m.SetFloat("_Cull", (float)CullMode.Back);
            if (normal != null)
            {
                m.SetTexture("_BumpMap", normal);
                m.SetFloat("_BumpScale", normalScale);
                m.EnableKeyword("_NORMALMAP");
            }
            else m.DisableKeyword("_NORMALMAP");
            if (mask != null)
            {
                m.SetTexture("_MetallicGlossMap", mask);
                m.SetFloat("_Smoothness", smoothness);
                m.SetFloat("_GlossMapScale", smoothness);
                m.SetFloat("_Metallic", 0f);
                m.SetFloat("_SmoothnessTextureChannel", 0f);
                m.EnableKeyword("_METALLICSPECGLOSSMAP");
                m.SetTexture("_OcclusionMap", mask);
                m.SetFloat("_OcclusionStrength", occlusion);
                m.EnableKeyword("_OCCLUSIONMAP");
            }
            else
            {
                m.SetFloat("_Smoothness", 0.3f * smoothness);
                m.DisableKeyword("_METALLICSPECGLOSSMAP");
                m.DisableKeyword("_OCCLUSIONMAP");
            }
            var macroTex = macro ? Load(MacroPath) : null;
            if (macroTex != null)
            {
                // A low-frequency colour variation about seven repeats wide (an odd ratio, so it never lines up with the
                // texture's own repeat), breaking up the tiling at a distance.
                m.SetTexture("_DetailAlbedoMap", macroTex);
                m.SetTextureScale("_DetailAlbedoMap", new Vector2(0.137f, 0.137f));
                m.SetFloat("_DetailAlbedoMapScale", 1f);
                m.EnableKeyword("_DETAIL_MULX2");
            }
            else m.DisableKeyword("_DETAIL_MULX2");
            m.SetFloat("_EnvironmentReflections", 1f);
            m.SetFloat("_SpecularHighlights", 1f);
            m.enableInstancing = true;
            ValidateLit(m);
            if (macroTex != null) m.EnableKeyword("_DETAIL_MULX2");
        }

        static readonly Type BaseShaderGui = Type.GetType("UnityEditor.BaseShaderGUI, Unity.RenderPipelines.Universal.Editor");
        static readonly Type LitGui = Type.GetType("UnityEditor.Rendering.Universal.ShaderGUI.LitGUI, Unity.RenderPipelines.Universal.Editor");

        /// <summary>
        /// Runs URP's own material validation (keywords, blend modes, render queue) on a Lit material set from code, the way
        /// its inspector would. Reached by reflection, so the Look's editor assembly needs no reference to URP's editor one.
        /// </summary>
        public static void ValidateLit(Material m)
        {
            try
            {
                var set = BaseShaderGui?.GetMethod("SetMaterialKeywords", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                var lit = LitGui?.GetMethod("SetMaterialKeywords", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                    null, new[] { typeof(Material) }, null);
                if (set == null) return;
                Action<Material> litFn = null;
                if (lit != null) litFn = mm => lit.Invoke(null, new object[] { mm });
                set.Invoke(null, new object[] { m, litFn, null });
            }
            catch (Exception e)
            {
                Debug.LogWarning("JAMKIT_LOOK material validation skipped for " + m.name + ": " + e.Message);
            }
        }

        /// <summary>Window glass: transparent with preserved specular (it reflects the sky and the street, and shows the room
        /// behind it), very smooth.</summary>
        public static Material Glass()
        {
            string path = MatRoot + "VHM_Glass.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = new Material(Lit) { name = "VHM_Glass" };
            m.SetColor("_BaseColor", new Color(0.16f, 0.19f, 0.2f, 0.32f));
            m.SetFloat("_Smoothness", 0.96f);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_BlendModePreserveSpecular", 1f);
            m.SetFloat("_Cull", (float)CullMode.Back);
            ValidateLit(m);
            { VhLookLibrary.EnsureAssetFolder(path); AssetDatabase.CreateAsset(m, path); }
            return m;
        }

        /// <summary>A plain painted surface (trim, window frames, doors, gutters): the plaster set, tinted and a bit smoother.</summary>
        public static Material Paint(string variant, Color color, float smoothness = 1.25f)
        {
            var m = Tinted("Plaster_Painted", variant, color);
            if (m == null) return null;
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_BumpScale", 0.35f);
            m.SetTextureScale("_BaseMap", new Vector2(0.6f, 0.6f));
            EditorUtility.SetDirty(m);
            return m;
        }

        static TerrainLayer BuildTerrainLayer(Entry e)
        {
            string d = TexRoot + e.name + "/" + e.name;
            string path = MatRoot + "VHT_" + e.name + ".terrainlayer";
            var t = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            bool fresh = t == null;
            if (fresh) t = new TerrainLayer();
            t.diffuseTexture = Load(d + "_Albedo.jpg");
            t.normalMapTexture = Load(d + "_Normal.jpg");
            t.maskMapTexture = Load(d + "_Mask.png");
            t.tileSize = new Vector2(e.tile, e.tile);
            t.normalScale = e.normalScale;
            t.diffuseRemapMax = new Vector4(e.tint.r, e.tint.g, e.tint.b, 1f);
            t.maskMapRemapMin = Vector4.zero;
            t.maskMapRemapMax = new Vector4(1f, 1f, 1f, e.smoothness);
            if (fresh) { VhLookLibrary.EnsureAssetFolder(path); AssetDatabase.CreateAsset(t, path); }
            EditorUtility.SetDirty(t);
            return t;
        }

        // ---------- Bark, foliage, props ----------

        public const string FoliageMaterialPath = MatRoot + "VHM_Foliage.mat";
        public const string FoliageDryMaterialPath = MatRoot + "VHM_Foliage_Dry.mat";

        static void BuildBarkAndFoliage()
        {
            Foliage(FoliageMaterialPath, new Color(1f, 1f, 1f));
            Foliage(FoliageDryMaterialPath, new Color(1.08f, 0.98f, 0.72f));
        }

        static Material Foliage(string path, Color tint)
        {
            var shader = Shader.Find("JamKit/Look/Foliage");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool fresh = m == null;
            if (fresh) m = new Material(shader);
            else if (m.shader != shader) m.shader = shader;
            m.SetTexture("_BaseMap", Load(LeafAtlasPath));
            m.SetColor("_BaseColor", tint);
            m.enableInstancing = true;
            if (fresh) { VhLookLibrary.EnsureAssetFolder(path); AssetDatabase.CreateAsset(m, path); }
            EditorUtility.SetDirty(m);
            return m;
        }

        public static Material FoliageMaterial(bool dry = false)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(dry ? FoliageDryMaterialPath : FoliageMaterialPath);
            if (m == null)
            {
                ConfigureImporters();
                BuildBarkAndFoliage();
                m = AssetDatabase.LoadAssetAtPath<Material>(dry ? FoliageDryMaterialPath : FoliageMaterialPath);
            }
            return m;
        }

        /// <summary>Props downloaded with the library (Poly Haven models): CoveredCar, ManholeCover.</summary>
        public static readonly string[] PropNames = { "CoveredCar", "ManholeCover" };

        static void BuildProps()
        {
            foreach (var p in PropNames)
            {
                string d = PropRoot + p + "/" + p;
                string path = MatRoot + "VHM_Prop_" + p + ".mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                bool fresh = m == null;
                if (fresh) m = new Material(Lit);
                SetupLit(m, Load(d + "_Albedo.jpg"), Load(d + "_Normal.jpg"), Load(d + "_Mask.png"), Color.white, 1f, 1f, 1f, 1f, false);
                if (fresh) { VhLookLibrary.EnsureAssetFolder(path); AssetDatabase.CreateAsset(m, path); }
                EditorUtility.SetDirty(m);
            }
        }

        /// <summary>
        /// Instantiates a library prop (its FBX with the library material on every renderer, a box collider round it),
        /// scaled to real size. Returns null if the model isn't imported yet.
        /// </summary>
        public static GameObject Prop(string name, Transform parent, Vector3 position, float yaw)
        {
            string d = PropRoot + name + "/" + name;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(d + ".fbx");
            if (model == null) { Debug.LogWarning("JAMKIT_LOOK prop not imported: " + d + ".fbx"); return null; }
            var m = AssetDatabase.LoadAssetAtPath<Material>(MatRoot + "VHM_Prop_" + name + ".mat");
            if (m == null) { BuildProps(); m = AssetDatabase.LoadAssetAtPath<Material>(MatRoot + "VHM_Prop_" + name + ".mat"); }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
            go.name = name;
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.rotation = Quaternion.identity;
            var bounds = new Bounds(position, Vector3.zero);
            bool any = false;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = m;
                r.sharedMaterials = mats;
                if (!any) { bounds = r.bounds; any = true; } else bounds.Encapsulate(r.bounds);
            }
            if (any)
            {
                var col = go.AddComponent<BoxCollider>();
                col.center = go.transform.InverseTransformPoint(bounds.center);
                var s = go.transform.lossyScale;
                col.size = new Vector3(bounds.size.x / Mathf.Max(1e-4f, Mathf.Abs(s.x)), bounds.size.y / Mathf.Max(1e-4f, Mathf.Abs(s.y)),
                    bounds.size.z / Mathf.Max(1e-4f, Mathf.Abs(s.z)));
            }
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            return go;
        }
    }
}
