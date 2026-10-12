using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace JamKit.Look.EditorTools
{
    /// <summary>
    /// One-time project setup for the look (idempotent; the LookLab build runs it):
    /// <list type="bullet">
    /// <item>Adds <see cref="VhAtmosphereFeature"/> to the PC renderer (Assets/Settings/PC_Renderer.asset) the way URP's own
    /// inspector does (a sub-asset plus the feature map). Inert unless a VhAtmosphere override is active, so the other
    /// templates are unaffected.</item>
    /// <item>Pass 3 (the Perf agent's list): SSAO at half resolution from the depth texture (no DepthNormals prepass), medium
    /// (gaussian) blur. 0.5-1.5 ms GPU and 1-3 ms of render thread on the RTX 3050.</item>
    /// <item>Pass 3: <see cref="LowRenderer"/>, a copy of the PC renderer without SSAO for the "VH Low" tier (the Perf agent's
    /// VhPerfSetup points VH_Low_RPAsset at it).</item>
    /// </list>
    /// </summary>
    public static class VhLookSetup
    {
        public const string PcRenderer = "Assets/Settings/PC_Renderer.asset";
        /// <summary>The low tier's renderer: PC_Renderer's features without SSAO (the atmosphere stays: it is cheap and the
        /// look depends on it).</summary>
        public const string LowRenderer = "Assets/Settings/VH_Low_Renderer.asset";

        [MenuItem("JamKit/Look/Install Renderer Features")]
        public static void InstallRendererFeatures()
        {
            var data = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(PcRenderer);
            if (data == null) { Debug.LogError("JAMKIT_LOOK no renderer at " + PcRenderer); return; }
            InstallAtmosphere(data);
            TuneSsao(data);
            AssetDatabase.SaveAssets();
            EnsureLowRenderer();
            AssetDatabase.SaveAssets();
        }

        static void InstallAtmosphere(ScriptableRendererData data)
        {
            var shader = Shader.Find("JamKit/Look/Atmosphere");
            foreach (var f in data.rendererFeatures)
            {
                if (f is VhAtmosphereFeature existing)
                {
                    if (existing.shader != shader) { existing.shader = shader; EditorUtility.SetDirty(existing); }
                    Debug.Log("JAMKIT_LOOK atmosphere feature already installed");
                    return;
                }
            }
            var so = new SerializedObject(data);
            var features = so.FindProperty("m_RendererFeatures");
            var map = so.FindProperty("m_RendererFeatureMap");
            var feature = ScriptableObject.CreateInstance<VhAtmosphereFeature>();
            feature.name = "VhAtmosphere";
            feature.shader = shader;
            AssetDatabase.AddObjectToAsset(feature, data);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId);
            features.arraySize++;
            features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            so.ApplyModifiedProperties();
            data.SetDirty();
            EditorUtility.SetDirty(data);
            Debug.Log("JAMKIT_LOOK atmosphere feature installed in " + PcRenderer);
        }

        /// <summary>SSAO: half resolution (Downsample), the depth texture as its source (Source 0 = Depth: no DepthNormals
        /// prepass), medium blur (BlurQuality 1 = gaussian). URP's settings class is internal, so it's set through the
        /// serialized properties, as its inspector does.</summary>
        static void TuneSsao(ScriptableRendererData data)
        {
            foreach (var f in data.rendererFeatures)
            {
                if (f == null || f.GetType().Name != "ScreenSpaceAmbientOcclusion") continue;
                var so = new SerializedObject(f);
                bool changed = false;
                changed |= SetBool(so, "m_Settings.Downsample", true);
                changed |= SetEnum(so, "m_Settings.Source", 0);
                changed |= SetEnum(so, "m_Settings.BlurQuality", 1);
                changed |= SetEnum(so, "m_Settings.NormalSamples", 1);
                if (!changed) continue;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(f);
                Debug.Log("JAMKIT_LOOK SSAO set to half resolution, depth source, medium blur in " + AssetDatabase.GetAssetPath(data));
            }
        }

        static bool SetBool(SerializedObject so, string path, bool value)
        {
            var p = so.FindProperty(path);
            if (p == null || p.boolValue == value) return false;
            p.boolValue = value;
            return true;
        }

        static bool SetEnum(SerializedObject so, string path, int value)
        {
            var p = so.FindProperty(path);
            if (p == null) return false;
            if (p.propertyType == SerializedPropertyType.Enum)
            {
                if (p.enumValueIndex == value) return false;
                p.enumValueIndex = value;
                return true;
            }
            if (p.intValue == value) return false;
            p.intValue = value;
            return true;
        }

        /// <summary>The "VH Low" renderer: a copy of the PC renderer (made by Unity, so it gets a proper .meta) with SSAO
        /// switched off. Re-made from the PC renderer when their feature lists differ. Returns its path.</summary>
        [MenuItem("JamKit/Look/Make the Low-Tier Renderer")]
        public static string EnsureLowRenderer()
        {
            var pc = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(PcRenderer);
            if (pc == null) return null;
            var low = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(LowRenderer);
            if (low == null || low.rendererFeatures.Count != pc.rendererFeatures.Count)
            {
                if (low != null) AssetDatabase.DeleteAsset(LowRenderer);
                if (!AssetDatabase.CopyAsset(PcRenderer, LowRenderer)) { Debug.LogError("JAMKIT_LOOK could not copy " + PcRenderer); return null; }
                low = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(LowRenderer);
            }
            if (low == null) return null;
            foreach (var f in low.rendererFeatures)
            {
                if (f == null || f.GetType().Name != "ScreenSpaceAmbientOcclusion" || !f.isActive) continue;
                f.SetActive(false);
                EditorUtility.SetDirty(f);
            }
            low.SetDirty();
            EditorUtility.SetDirty(low);
            Debug.Log("JAMKIT_LOOK low-tier renderer (no SSAO): " + LowRenderer);
            return LowRenderer;
        }
    }
}
