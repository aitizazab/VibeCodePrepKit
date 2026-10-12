using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

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
                mat.SetTexture("_BaseMap", albedo);
                mat.SetColor("_BaseColor", Color.white);
                // The meshes' UVs are in metres: one repeat every s.metres.
                mat.SetTextureScale("_BaseMap", Vector2.one / s.metres);
                if (normal != null)
                {
                    mat.SetTexture("_BumpMap", normal);
                    mat.SetFloat("_BumpScale", s.bump);
                    mat.EnableKeyword("_NORMALMAP");
                }
                if (mask != null)
                {
                    mat.SetTexture("_MetallicGlossMap", mask);
                    mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                    mat.SetTexture("_OcclusionMap", mask);
                    mat.SetFloat("_OcclusionStrength", 1f);
                    mat.EnableKeyword("_OCCLUSIONMAP");
                    mat.SetFloat("_SmoothnessTextureChannel", 0f);
                }
                mat.SetFloat("_Smoothness", s.smooth);
                mat.SetFloat("_GlossMapScale", s.smooth);
                mat.SetFloat("_Metallic", s.key == "metal" ? 1f : 0f);
                mat.enableInstancing = true;
                EditorUtility.SetDirty(mat);
                lib.surfaces.Add(new VhWardLibrary.Surface { key = s.key, material = mat, metresPerTile = s.metres, mean = s.mean });
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
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            return lib;
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
