using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace JamKit.Perf.EditorTools
{
    /// <summary>
    /// Visiting Hours' central performance settings (docs/redesign/perf.md "Central settings"). Run once after a pull that
    /// changes them, and before each build (VhPerfBuild does):
    /// <code>
    /// Unity.exe -batchmode -nographics -projectPath . -executeMethod JamKit.Perf.EditorTools.VhPerfSetup.ApplyFromCommandLine
    ///     [-perfNoTextures] -logFile out/perf/setup.log -quit
    /// </code>
    /// <list type="number">
    /// <item><b>Tier URP assets</b>: Perf/Settings/VH_High_RPAsset.asset and VH_Low_RPAsset.asset, copies of
    /// Assets/Settings/PC_RPAsset.asset (so the Look agent's choices carry over: renderer, HDR, colour grading, probes) with
    /// each tier's quality fields (<see cref="VhPerfTiers"/>): render scale with STP, MSAA off, no opaque texture, shadow
    /// distance, cascades, map size, soft-shadow quality, additional-light shadows. Re-synced from PC_RPAsset every run.</item>
    /// <item><b>Quality levels</b> "VH High" and "VH Low" pointed at them (other templates keep "PC").</item>
    /// <item><b>Layers</b> "PerfSmall" and "PerfDetail" for per-layer cull distances (ProjectSettings/TagManager.asset).</item>
    /// <item><b>Texture budgets</b> on Visiting Hours' 3D textures: mipmap streaming on; normal and mask maps at most 1024
    /// (albedo keeps 2048, characters keep 2048); compressed; not readable unless a tool needs it. Sprites, UI and 2D art
    /// are left alone.</item>
    /// <item><b>Model imports</b> for props: no cameras, lights or animation rig (an Animator on every chair otherwise).</item>
    /// </list>
    /// Logs JAMKIT_PERF_SETUP_OK / _FAIL.
    /// </summary>
    public static class VhPerfSetup
    {
        public const string Root = "Assets/JamKit/Templates/VisitingHours";
        public const string SettingsFolder = Root + "/Perf/Settings";
        public const string SourceAsset = "Assets/Settings/PC_RPAsset.asset";
        public const string HighAsset = SettingsFolder + "/VH_High_RPAsset.asset";
        public const string LowAsset = SettingsFolder + "/VH_Low_RPAsset.asset";

        [MenuItem("JamKit/Perf/Set Up Quality Tiers and Budgets")]
        static void Menu() => Apply(true);

        public static void ApplyFromCommandLine()
        {
            int code = 1;
            try
            {
                Apply(!VhPerfArgs.Has("-perfNoTextures"));
                code = 0;
                Debug.Log("JAMKIT_PERF_SETUP_OK");
            }
            catch (Exception e) { Debug.LogException(e); Debug.Log("JAMKIT_PERF_SETUP_FAIL " + e.Message); }
            EditorApplication.Exit(code);
        }

        public static void Apply(bool textures)
        {
            EnsureFolder(SettingsFolder);
            var high = TierAsset(HighAsset, VhPerfTiers.High);
            var low = TierAsset(LowAsset, VhPerfTiers.Low);
            SetQualityLevel(VhPerfTiers.High.qualityLevel, high, VhPerfTiers.High);
            SetQualityLevel(VhPerfTiers.Low.qualityLevel, low, VhPerfTiers.Low);
            EnsureLayers(VhPerfCullDistances.SmallLayer, VhPerfCullDistances.DetailLayer);
            if (textures)
            {
                int t = ApplyTextureBudgets(Root);
                int m = ApplyModelImports(Root);
                Debug.Log("JAMKIT_PERF texture budgets changed " + t + " texture(s), model imports " + m);
            }
            AssetDatabase.SaveAssets();
        }

        // ---------- Tier URP assets ----------

        static UniversalRenderPipelineAsset TierAsset(string path, VhPerfTier tier)
        {
            var source = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(SourceAsset);
            if (source == null) throw new InvalidOperationException("no " + SourceAsset);
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (asset == null)
            {
                if (!AssetDatabase.CopyAsset(SourceAsset, path)) throw new InvalidOperationException("couldn't copy " + SourceAsset + " to " + path);
                asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            }
            else
            {
                string name = asset.name;
                EditorUtility.CopySerialized(source, asset);
                asset.name = name;
            }
            var so = new SerializedObject(asset);
            Set(so, "m_RenderScale", tier.renderScale);
            Set(so, "m_UpscalingFilter", (int)UpscalingFilterSelection.STP);
            Set(so, "m_MSAA", 1);
            Set(so, "m_RequireOpaqueTexture", false);   // nothing samples _CameraOpaqueTexture (checked 2026-10-12)
            Set(so, "m_RequireDepthTexture", true);     // SSAO and the atmosphere pass read depth
            Set(so, "m_ShadowDistance", tier.shadowDistance);
            Set(so, "m_ShadowCascadeCount", tier.shadowCascades);
            Set(so, "m_MainLightShadowmapResolution", tier.shadowResolution);
            Set(so, "m_SoftShadowQuality", tier.softShadowQuality);
            Set(so, "m_AdditionalLightShadowsSupported", tier.additionalLightShadows);
            Set(so, "m_AdditionalLightsShadowmapResolution", tier.additionalLightShadows ? 2048 : 512);
            Set(so, "m_AdditionalLightsCookieResolution", 1024);
            Set(so, "m_EnableLODCrossFade", true);
            if (tier.shadowCascades == 2) Set(so, "m_Cascade2Split", 0.25f);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return asset;
        }

        static void Set(SerializedObject so, string field, float v) { var p = so.FindProperty(field); if (p != null) p.floatValue = v; else Missing(field); }
        static void Set(SerializedObject so, string field, int v) { var p = so.FindProperty(field); if (p != null) p.intValue = v; else Missing(field); }
        static void Set(SerializedObject so, string field, bool v) { var p = so.FindProperty(field); if (p != null) p.boolValue = v; else Missing(field); }
        static void Missing(string field) => Debug.LogWarning("JAMKIT_PERF the URP asset has no field " + field + " (URP changed?)");

        // ---------- Quality levels ----------

        static void SetQualityLevel(string name, RenderPipelineAsset asset, VhPerfTier tier)
        {
            var qs = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset");
            if (qs == null || qs.Length == 0) throw new InvalidOperationException("no QualitySettings.asset");
            var so = new SerializedObject(qs[0]);
            var levels = so.FindProperty("m_QualitySettings");
            SerializedProperty level = null;
            for (int i = 0; i < levels.arraySize; i++)
                if (levels.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue == name) level = levels.GetArrayElementAtIndex(i);
            if (level == null)
            {
                // Start from "PC" (index of the Standalone default) and rename.
                levels.InsertArrayElementAtIndex(levels.arraySize - 1);
                level = levels.GetArrayElementAtIndex(levels.arraySize - 1);
                level.FindPropertyRelative("name").stringValue = name;
            }
            level.FindPropertyRelative("customRenderPipeline").objectReferenceValue = asset;
            Num(level, "lodBias", tier.lodBias);
            Num(level, "globalTextureMipmapLimit", tier.textureMipLimit);
            Num(level, "streamingMipmapsActive", 1);
            Num(level, "streamingMipmapsMemoryBudget", tier.streamingBudgetMB);
            Num(level, "realtimeReflectionProbes", 0);
            Num(level, "skinWeights", tier == VhPerfTiers.High ? 4 : 2);
            Num(level, "vSyncCount", 0);
            Num(level, "anisotropicTextures", tier == VhPerfTiers.High ? 2 : 1);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Sets a number-like property whatever its serialized type (bool, int, enum, float).</summary>
        static void Num(SerializedProperty parent, string name, double v)
        {
            var p = parent.FindPropertyRelative(name);
            if (p == null) { Missing(name); return; }
            switch (p.propertyType)
            {
                case SerializedPropertyType.Boolean: p.boolValue = v != 0; break;
                case SerializedPropertyType.Float: p.floatValue = (float)v; break;

                default: p.intValue = (int)v; break;
            }
        }

        // ---------- Layers ----------

        public static void EnsureLayers(params string[] names)
        {
            var tm = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (tm == null || tm.Length == 0) return;
            var so = new SerializedObject(tm[0]);
            var layers = so.FindProperty("layers");
            foreach (var n in names)
            {
                bool have = false;
                for (int i = 0; i < layers.arraySize; i++) if (layers.GetArrayElementAtIndex(i).stringValue == n) have = true;
                if (have) continue;
                for (int i = 31; i >= 8; i--)
                {
                    var p = layers.GetArrayElementAtIndex(i);
                    if (!string.IsNullOrEmpty(p.stringValue)) continue;
                    p.stringValue = n;
                    Debug.Log("JAMKIT_PERF added layer " + n + " at " + i);
                    break;
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- Texture budgets ----------

        /// <summary>True for paths whose textures are 2D art (sprites, UI, the battle's pictures, the opening's props that
        /// tools read): left alone.</summary>
        public static bool Is2D(string path) =>
            path.Contains("/UI/") || path.Contains("/Art/") || path.Contains("/Argument/") || path.Contains("/Resources/") || path.Contains("/Sprites/");

        public static bool IsNormal(string path, TextureImporter ti) =>
            ti.textureType == TextureImporterType.NormalMap || Contains(path, "normal") || Contains(path, "_nrm") || Contains(path, "_nor_");

        public static bool IsMask(string path) =>
            Contains(path, "_ao") || Contains(path, "rough") || Contains(path, "_arm") || Contains(path, "metal") || Contains(path, "_mask") ||
            Contains(path, "height") || Contains(path, "displacement") || Contains(path, "_disp") || Contains(path, "occlusion") || Contains(path, "specular");

        static bool Contains(string s, string what) => s.IndexOf(what, StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>The budget pass over <paramref name="folder"/>. Returns how many importers changed.</summary>
        public static int ApplyTextureBudgets(string folder)
        {
            int changed = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Is2D(path) || path.EndsWith(".hdr", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".exr", StringComparison.OrdinalIgnoreCase)) continue;
                if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) continue;
                if (ti.textureType == TextureImporterType.Sprite || ti.textureType == TextureImporterType.GUI || ti.textureType == TextureImporterType.Cookie) continue;
                if (ti.isReadable) continue;   // a tool reads it (the opening's badge, the monitor's digits): leave it
                bool dirty = false;
                bool character = path.Contains("/Models/") || path.Contains("/Characters/");
                int cap = character ? 2048 : IsNormal(path, ti) || IsMask(path) ? 1024 : 2048;
                if (ti.maxTextureSize > cap) { ti.maxTextureSize = cap; dirty = true; }
                if (ti.mipmapEnabled && !ti.streamingMipmaps) { ti.streamingMipmaps = true; dirty = true; }
                if (ti.textureCompression == TextureImporterCompression.Uncompressed) { ti.textureCompression = TextureImporterCompression.Compressed; dirty = true; }
                if (Contains(path, "normal") && ti.textureType == TextureImporterType.Default && !Contains(path, "normalized")) { ti.textureType = TextureImporterType.NormalMap; dirty = true; }
                if (!dirty) continue;
                ti.SaveAndReimport();
                changed++;
            }
            return changed;
        }

        /// <summary>Props' model imports: no cameras, lights or animation (characters and animation clips are left alone).</summary>
        public static int ApplyModelImports(string folder)
        {
            int changed = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("/Models/") && !path.Contains("/House/Models/") || path.Contains("/Characters/") || Contains(path, "anim")) continue;
                if (!(AssetImporter.GetAtPath(path) is ModelImporter mi)) continue;
                bool dirty = false;
                if (mi.importCameras) { mi.importCameras = false; dirty = true; }
                if (mi.importLights) { mi.importLights = false; dirty = true; }
                if (mi.isReadable) { mi.isReadable = false; dirty = true; }
                if (mi.animationType != ModelImporterAnimationType.None && mi.clipAnimations.Length == 0 && mi.defaultClipAnimations.Length == 0)
                { mi.animationType = ModelImporterAnimationType.None; mi.importAnimation = false; dirty = true; }
                if (!dirty) continue;
                mi.SaveAndReimport();
                changed++;
            }
            return changed;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }

    /// <summary>New 3D textures under Visiting Hours import with mipmap streaming on (the budget pass does the rest).</summary>
    public sealed class VhPerfTextureImport : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(VhPerfSetup.Root + "/", StringComparison.Ordinal) || VhPerfSetup.Is2D(assetPath)) return;
            var ti = (TextureImporter)assetImporter;
            if (ti.textureType == TextureImporterType.Sprite || ti.textureType == TextureImporterType.GUI || ti.isReadable || !ti.mipmapEnabled) return;
            ti.streamingMipmaps = true;
        }
    }
}
