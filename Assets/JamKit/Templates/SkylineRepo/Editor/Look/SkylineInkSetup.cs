using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace JamKit.SkylineRepo.EditorTools
{
    /// <summary>
    /// One-time project setup for Skyline's comic-ink look (idempotent; each Skyline scene build runs it): adds
    /// <see cref="SkylineInkFeature"/> to the PC renderer (Assets/Settings/PC_Renderer.asset) the way URP's own inspector
    /// does (a sub-asset plus the feature map), copied from Visiting Hours' VhLookSetup.InstallAtmosphere. The feature is
    /// inert unless a <see cref="SkylineInk"/> override is active, so the other templates sharing the renderer are unaffected.
    /// The composite shader is stored on the feature, so the build includes it.
    /// </summary>
    public static class SkylineInkSetup
    {
        public const string PcRenderer = "Assets/Settings/PC_Renderer.asset";
        public const string CompositeShader = "Hidden/Skyline/InkComposite";

        /// <summary>Adds the feature once. Returns true when it was added now, false when it was already there (its shader
        /// reference is refreshed either way) or the renderer asset is missing.</summary>
        [MenuItem("JamKit/Skyline/Install Ink Renderer Feature")]
        public static bool InstallFeature()
        {
            var data = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(PcRenderer);
            if (data == null) { Debug.LogError("SKYLINE_INK no renderer at " + PcRenderer); return false; }
            var shader = Shader.Find(CompositeShader);
            if (shader == null) Debug.LogError("SKYLINE_INK shader " + CompositeShader + " not found (a compile error in InkComposite.shader?)");
            foreach (var f in data.rendererFeatures)
            {
                if (f is SkylineInkFeature existing)
                {
                    if (existing.shader != shader) { existing.shader = shader; EditorUtility.SetDirty(existing); AssetDatabase.SaveAssets(); }
                    Debug.Log("SKYLINE_INK feature already installed");
                    return false;
                }
            }
            var so = new SerializedObject(data);
            var features = so.FindProperty("m_RendererFeatures");
            var map = so.FindProperty("m_RendererFeatureMap");
            var feature = ScriptableObject.CreateInstance<SkylineInkFeature>();
            feature.name = "SkylineInk";
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
            AssetDatabase.SaveAssets();
            Debug.Log("SKYLINE_INK feature installed in " + PcRenderer);
            return true;
        }

        /// <summary>The whole ink look for a small scene (the firing range, the enemy gallery): the ink rig (<see cref="SkylineLook.ApplyInk"/>,
        /// which switches the builder's plain sun off once its own sun stands), the renderer feature, the Resources materials,
        /// every scene material converted to its twin, and the cameras set up. Returns false (and logs) when the rig fails, the scene then keeping its plain sun.</summary>
        public static bool ApplyToScene(UnityEngine.SceneManagement.Scene scene)
        {
            try
            {
                SkylineLook.ApplyInk(scene);
                InstallFeature();
                SkylineInkConvert.EnsureResourceMaterials();
                SkylineInkConvert.ConvertScene(scene);
                SkylineLook.SetupCameras(scene);
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError("SKYLINE_INK the ink look failed for " + scene.name + ", the plain look is kept: " + e);
                return false;
            }
        }

        /// <summary>How many <see cref="SkylineInkFeature"/>s the PC renderer holds (the tests expect exactly one after an install).</summary>
        public static int FeatureCount()
        {
            var data = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(PcRenderer);
            if (data == null) return 0;
            int n = 0;
            foreach (var f in data.rendererFeatures) if (f is SkylineInkFeature) n++;
            return n;
        }
    }
}
