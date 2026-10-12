using System.Collections.Generic;
using System.IO;
using JamKit.Look.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace JamKit.VisitingHours.EditorTools
{
    /// <summary>
    /// Builds the ward's PBR materials (URP/Lit) and its window sky from <c>Ward/Textures</c> into <c>Ward/Materials</c>, and
    /// the <see cref="VhWardLibrary"/> asset in Resources that the ward loads at runtime. Idempotent: run it again after
    /// new textures land. Also sets the textures' import (normal maps as normal maps, masks linear). docs/redesign/day.md.
    /// </summary>
    public static class VhWardLibraryBuilder
    {
        public const string WardRoot = VisitingHoursBuilder.Root + "/Ward";
        public const string TexturesDir = WardRoot + "/Textures";
        public const string MaterialsDir = WardRoot + "/Materials";
        public const string LibraryPath = VisitingHoursBuilder.Root + "/Resources/JamKit/VhWardLibrary.asset";
        public const string SkyFile = "vh_ward_sky_hotel_rooftop_balcony_2k.hdr";

        /// <summary>key, texture name, metres per tile, smoothness scale, normal strength, the albedo's mean colour.</summary>
        struct Spec
        {
            public string key, tex;
            public float metres, smooth, bump;
            public Color mean;
            public Spec(string key, string tex, float metres, float smooth, float bump, Color mean)
            {
                this.key = key; this.tex = tex; this.metres = metres; this.smooth = smooth; this.bump = bump; this.mean = mean;
            }
        }

        static readonly Spec[] Specs =
        {
            new Spec("floor", "vh_floor_vinyl", 1.6f, 0.85f, 0.6f, new Color(0.843f, 0.841f, 0.852f)),
            new Spec("ceiling", "vh_ceiling_tile", 1.2f, 0.6f, 0.8f, new Color(0.957f, 0.957f, 0.957f)),
            new Spec("wall", "vh_wall_paint", 2.4f, 0.7f, 0.5f, new Color(0.74f, 0.742f, 0.728f)),
            new Spec("plastic", "vh_plastic_white", 0.8f, 0.9f, 0.5f, new Color(0.635f, 0.646f, 0.666f)),
            new Spec("plastic_dark", "vh_plastic_dark", 0.8f, 0.9f, 0.5f, new Color(0.209f, 0.212f, 0.22f)),
            new Spec("fabric", "vh_fabric_weave", 0.5f, 0.6f, 1f, new Color(0.37f, 0.369f, 0.37f)),
            new Spec("linen", "vh_fabric_linen", 0.45f, 0.6f, 0.8f, new Color(0.725f, 0.725f, 0.725f)),
            new Spec("carpet", "vh_carpet", 1.2f, 0.5f, 1f, new Color(0.247f, 0.268f, 0.314f)),
            new Spec("metal", "vh_metal_brushed", 0.6f, 1f, 0.6f, new Color(0.487f, 0.53f, 0.574f)),
            new Spec("wood", "vh_wood_oak", 1.4f, 0.8f, 0.7f, new Color(0.483f, 0.392f, 0.312f)),
            new Spec("tiles", "vh_tiles_white", 1.2f, 1f, 1f, new Color(0.974f, 0.973f, 0.972f)),
            new Spec("skin", "vh_skin", 0.12f, 0.75f, 0.5f, new Color(0.907f, 0.907f, 0.907f)),
        };

        [MenuItem("JamKit/Visiting Hours/Build the Ward Materials")]
        public static void BuildMenu()
        {
            var lib = Ensure();
            Debug.Log("JamKit: the ward library has " + (lib != null ? lib.surfaces.Count : 0) + " surfaces (" + LibraryPath + ")");
        }

        /// <summary>-executeMethod JamKit.VisitingHours.EditorTools.VhWardLibraryBuilder.BuildFromCommandLine (batch mode): the
        /// materials, Look's rig prefab and sky, the library. Logs JAMKIT_VH_WARD_LIBRARY_OK (or _FAIL) and exits.</summary>
        public static void BuildFromCommandLine()
        {
            VhWardLibrary lib = null;
            try { lib = Ensure(); }
            catch (System.Exception e) { Debug.LogException(e); }
            bool ok = lib != null && lib.surfaces.Count > 0;
            Debug.Log((ok ? "JAMKIT_VH_WARD_LIBRARY_OK " : "JAMKIT_VH_WARD_LIBRARY_FAIL ") + (lib != null ? lib.surfaces.Count : 0) +
                      " surfaces, Look rig " + (lib != null && lib.dayRig != null ? "yes" : "no") + ", sky " + (lib != null && lib.daySky != null ? lib.daySky.name : "none") +
                      ", baked probes " + (lib != null && lib.probes != null ? lib.probes.Count : 0));
            EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary>Makes (or refreshes) the materials and the library. Returns null when the textures aren't there.</summary>
        public static VhWardLibrary Ensure()
        {
            if (!AssetDatabase.IsValidFolder(TexturesDir)) return null;
            VisitingHoursBuilder.EnsureFolder(MaterialsDir);
            VisitingHoursBuilder.EnsureFolder(VisitingHoursBuilder.Root + "/Resources/JamKit");
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null)
            {
                Debug.LogError("JamKit: URP/Lit not found; the ward library isn't built.");
                return null;
            }

            var lib = AssetDatabase.LoadAssetAtPath<VhWardLibrary>(LibraryPath);
            if (lib == null)
            {
                lib = ScriptableObject.CreateInstance<VhWardLibrary>();
                AssetDatabase.CreateAsset(lib, LibraryPath);
            }
            lib.surfaces = new List<VhWardLibrary.Surface>();
            foreach (var s in Specs)
            {
                var albedo = Texture(s.tex + "_albedo.jpg", TextureImporterType.Default, true);
                var normal = Texture(s.tex + "_normal.jpg", TextureImporterType.NormalMap, false);
                var mask = Texture(s.tex + "_mask.png", TextureImporterType.Default, false);
                if (albedo == null)
                {
                    Debug.LogWarning("JamKit: ward texture missing: " + s.tex);
                    continue;
                }
                var mat = Material(s.key, lit);
                // Look's set-up (docs/redesign/look.md): URP Lit from albedo, normal and packed mask, the meshes' UVs in metres
                // (one repeat every s.metres), and Look's macro colour variation over it, so no surface reads flat or tiled
                // (judge/look-1.md and look-2.md).
                VhLookLibrary.SetupLit(mat, albedo, normal, mask, Color.white, s.bump, s.smooth, 1f, 1f / s.metres, true);
                if (s.key == "metal") mat.SetFloat("_Metallic", 1f);
                EditorUtility.SetDirty(mat);
                lib.surfaces.Add(new VhWardLibrary.Surface { key = s.key, material = mat, metresPerTile = s.metres, mean = s.mean });
            }

            // Look's library where it has the ward's surfaces (its plaster for the walls, its ceiling tiles, its terrazzo for
            // the corridor, the station and the lobby): the same materials the house and the hospital's exterior use.
            foreach (var pair in LookSurfaces)
            {
                var look = LookMaterial(pair.Value);
                if (look == null) continue;
                lib.surfaces.RemoveAll(x => x.key == pair.Key);
                lib.surfaces.Add(new VhWardLibrary.Surface { key = pair.Key, material = look, metresPerTile = VhLookLibrary.TileMetres(pair.Value), mean = LookMean(pair.Value) });
            }

            // Glass (window glazing, screens off): smooth and dark, no texture needed.
            var glass = Material("glass", lit);
            glass.SetColor("_BaseColor", new Color(0.16f, 0.2f, 0.22f));
            glass.SetFloat("_Smoothness", 0.96f);
            glass.SetFloat("_Metallic", 0.1f);
            EditorUtility.SetDirty(glass);
            lib.surfaces.Add(new VhWardLibrary.Surface { key = "glass", material = glass, metresPerTile = 1f, mean = new Color(0.16f, 0.2f, 0.22f) });

            // The sky through the windows: the HDRI on a panoramic skybox.
            var hdr = AssetDatabase.LoadAssetAtPath<Texture>(TexturesDir + "/" + SkyFile);
            var pano = Shader.Find("Skybox/Panoramic");
            if (hdr != null && pano != null)
            {
                var sky = Material("sky", pano);
                sky.SetTexture("_MainTex", hdr);
                sky.SetFloat("_Exposure", 1.1f);
                sky.SetFloat("_Rotation", 200f);
                EditorUtility.SetDirty(sky);
                lib.sky = sky;
            }
            // The pictures (signs, notices, the board, the TV, the book, the monitor's digits).
            lib.pictures = new List<Texture2D>();
            string pics = TexturesDir + "/Pictures";
            if (AssetDatabase.IsValidFolder(pics))
                foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { pics }))
                {
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guid));
                    if (tex != null) lib.pictures.Add(tex);
                }
            // Look's hospital by day: its rig as a prefab, and its sky (docs/redesign/look.md).
            try
            {
                lib.dayRig = BuildLookRig();
                lib.daySky = lib.dayRig != null ? VhLookSky.Material(HospitalSky) : null;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("JamKit: Look's hospital rig wasn't built (the ward keeps its own light): " + e.Message);
            }
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            return lib;
        }

        // ---------- Look (docs/redesign/look.md) ----------

        /// <summary>The ward's surface keys that come from Look's library, and the library entry for each.</summary>
        static readonly KeyValuePair<string, string>[] LookSurfaces =
        {
            new KeyValuePair<string, string>("wall", "Plaster_Interior"),
            new KeyValuePair<string, string>("ceiling", "Ceiling_Tile"),
            new KeyValuePair<string, string>("terrazzo", "Hospital_Terrazzo"),
        };

        /// <summary>The sky Look's ApplyHospitalDay uses (the bluest of its HDRIs).</summary>
        public const string HospitalSky = "Sky_EveningRoad01_2k";
        public const string GeneratedDir = WardRoot + "/Generated";
        public const string RigPath = GeneratedDir + "/VH_WardLookRig.prefab";
        public const string ProbesDir = GeneratedDir + "/Probes";

        static Material LookMaterial(string name)
        {
            if (!VhLookLibrary.TryFind(name, out _)) return null;
            if (!File.Exists(VhLookLibrary.TexRoot + name + "/" + name + "_Albedo.jpg")) return null;
            return VhLookLibrary.Get(name);
        }

        /// <summary>The library albedo's mean colour (sRGB), read from the file (the imported texture isn't readable).</summary>
        static Color LookMean(string name)
        {
            string path = VhLookLibrary.TexRoot + name + "/" + name + "_Albedo.jpg";
            if (!File.Exists(path)) return Color.white;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!tex.LoadImage(File.ReadAllBytes(path))) return Color.white;
                var px = tex.GetPixels32();
                double r = 0, g = 0, b = 0;
                int step = Mathf.Max(1, px.Length / 65536);
                int n = 0;
                for (int i = 0; i < px.Length; i += step) { r += px[i].r; g += px[i].g; b += px[i].b; n++; }
                // (The ward's Colour() replaces the entry's own tint with colour / mean, so the mean is the albedo's alone.)
                return new Color((float)(r / n / 255.0), (float)(g / n / 255.0), (float)(b / n / 255.0), 1f);
            }
            finally { Object.DestroyImmediate(tex); }
        }

        /// <summary>
        /// Look's <c>VhLook.ApplyHospitalDay</c> in a scratch scene with the ward's sun (24 degrees up from the west-south-west,
        /// down the corridor from its end window) and an indoor ambient, kept as a prefab the ward instantiates at runtime
        /// (the ward is built in code when a day starts, so Look's editor call can't run there).
        /// </summary>
        static GameObject BuildLookRig()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                var o = new VhLook.Options
                {
                    // Look's hospital day (VhLook.ApplyHospitalDay's own values), with the ward's late-afternoon sun angle.
                    sunElevation = 24f, sunAzimuth = VhWardLight.SunEuler.y + 180f, sky = HospitalSky, sunIntensity = 2.4f,
                    sunColor = new Color(1f, 0.96f, 0.9f), bounceIntensity = 0.12f, bounceColor = new Color(0.9f, 0.95f, 1f),
                    shadowDistance = 60f, lensFlare = false, ambientIntensity = 0.55f,
                };
                var rig = VhLook.ApplyHospitalDay(scene, o);
                if (rig == null) return null;
                VisitingHoursBuilder.EnsureFolder(GeneratedDir);
                var prefab = PrefabUtility.SaveAsPrefabAsset(rig.gameObject, RigPath);
                Debug.Log("JamKit: Look's hospital-day rig for the ward: " + RigPath);
                return prefab;
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        // ---------- The reflection probes, baked (perf.md, Day item 5) ----------

        /// <summary>
        /// Builds Day 1's ward in a scratch scene (all rooms shown, Look's light on) and bakes each of its box-projected
        /// reflection probes to a cubemap (Ward/Generated/Probes), kept in the library: the ward's probes use them instead
        /// of realtime ones (which never render: realtime probes are off in the quality levels). Needs a GPU (not
        /// -nographics). Run again after the ward or its materials change.
        /// </summary>
        [MenuItem("JamKit/Visiting Hours/Bake the Ward Probes")]
        public static void BakeProbes()
        {
            var lib = Ensure();
            if (lib == null) { Debug.LogError("JamKit: no ward library (the ward's textures are missing)"); return; }
            var config = FindConfig();
            if (config == null) { Debug.LogError("JamKit: no VisitingHoursConfig asset found; the ward can't be built to bake"); return; }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var previous = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(scene);
            bool realtime = QualitySettings.realtimeReflectionProbes;
            try
            {
                QualitySettings.realtimeReflectionProbes = true;
                var keep = lib.probes;
                lib.probes = new List<Texture>();   // build with realtime probes, then bake each
                var area = VhWorld.Build(SliceArea.Hospital, config, null);
                if (area.rooms != null) area.rooms.ShowAll();
                VisitingHoursBuilder.EnsureFolder(ProbesDir);
                var probes = area.root.GetComponentsInChildren<ReflectionProbe>(true);
                System.Array.Sort(probes, (a, b) => string.CompareOrdinal(a.name, b.name));
                var baked = new List<Texture>();
                for (int i = 0; i < probes.Length; i++)
                {
                    string path = ProbesDir + "/VH_WardProbe_" + i + ".exr";
                    probes[i].mode = ReflectionProbeMode.Baked;
                    if (!Lightmapping.BakeReflectionProbe(probes[i], path))
                    {
                        Debug.LogError("JamKit: the ward's probe " + i + " didn't bake (no GPU?)");
                        lib.probes = keep;
                        return;
                    }
                    AssetDatabase.ImportAsset(path);
                    baked.Add(AssetDatabase.LoadAssetAtPath<Texture>(path));
                }
                lib.probes = baked;
                EditorUtility.SetDirty(lib);
                AssetDatabase.SaveAssets();
                Debug.Log("JAMKIT_VH_PROBES_OK " + baked.Count + " probes baked into " + ProbesDir);
            }
            finally
            {
                QualitySettings.realtimeReflectionProbes = realtime;
                if (previous.IsValid()) SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        /// <summary>-executeMethod JamKit.VisitingHours.EditorTools.VhWardLibraryBuilder.BakeProbesFromCommandLine (batch mode,
        /// with graphics).</summary>
        public static void BakeProbesFromCommandLine()
        {
            BakeProbes();
            EditorApplication.Exit(0);
        }

        static VisitingHoursConfig FindConfig()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:VisitingHoursConfig"))
            {
                var c = AssetDatabase.LoadAssetAtPath<VisitingHoursConfig>(AssetDatabase.GUIDToAssetPath(guid));
                if (c != null) return c;
            }
            return null;
        }

        static Material Material(string key, Shader shader)
        {
            string path = MaterialsDir + "/VH_Ward_" + key + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = "VH_Ward_" + key };
                AssetDatabase.CreateAsset(mat, path);
            }
            if (mat.shader != shader) mat.shader = shader;
            return mat;
        }

        static Texture2D Texture(string file, TextureImporterType type, bool srgb)
        {
            string path = TexturesDir + "/" + file;
            if (!File.Exists(path)) return null;
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp != null && (imp.textureType != type || imp.sRGBTexture != srgb || imp.maxTextureSize != 1024))
            {
                imp.textureType = type;
                if (type != TextureImporterType.NormalMap) imp.sRGBTexture = srgb;
                imp.maxTextureSize = 1024;
                imp.mipmapEnabled = true;
                imp.anisoLevel = 4;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }

    /// <summary>The ward's textures import right the first time (normal maps as normal maps, masks linear).</summary>
    public sealed class VhWardTextureImport : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').StartsWith(VhWardLibraryBuilder.TexturesDir + "/")) return;
            var imp = (TextureImporter)assetImporter;
            imp.maxTextureSize = assetPath.EndsWith(".hdr") ? 2048 : 1024;
            imp.anisoLevel = 4;
            if (assetPath.EndsWith("_normal.jpg")) imp.textureType = TextureImporterType.NormalMap;
            else if (assetPath.EndsWith("_mask.png")) imp.sRGBTexture = false;
            if (assetPath.Contains("/Pictures/"))
            {
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.mipmapEnabled = true;
                imp.maxTextureSize = 2048;
                // The monitor copies its digits from this one at runtime.
                if (assetPath.EndsWith("monitor_digits.png"))
                {
                    imp.isReadable = true;
                    imp.textureCompression = TextureImporterCompression.Uncompressed;
                    imp.mipmapEnabled = false;
                    imp.alphaIsTransparency = true;
                }
            }
        }
    }
}
