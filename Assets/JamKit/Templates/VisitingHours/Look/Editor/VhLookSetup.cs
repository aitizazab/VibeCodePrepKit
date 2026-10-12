using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace JamKit.Look.EditorTools
{
    /// <summary>
    /// One-time project setup for the look: adds <see cref="VhAtmosphereFeature"/> to the PC renderer
    /// (Assets/Settings/PC_Renderer.asset) the way URP's own inspector does (a sub-asset plus the feature map). The feature is
    /// inert unless a VhAtmosphere override is active, so the other templates are unaffected. Idempotent.
    /// </summary>
    public static class VhLookSetup
    {
        public const string PcRenderer = "Assets/Settings/PC_Renderer.asset";

        [MenuItem("JamKit/Look/Install Renderer Features")]
        public static void InstallRendererFeatures()
        {
            var data = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(PcRenderer);
            if (data == null) { Debug.LogError("JAMKIT_LOOK no renderer at " + PcRenderer); return; }
            var shader = Shader.Find("JamKit/Look/Atmosphere");
            foreach (var f in data.rendererFeatures)
            {
                if (f is VhAtmosphereFeature existing)
                {
                    if (existing.shader != shader) { existing.shader = shader; EditorUtility.SetDirty(existing); AssetDatabase.SaveAssets(); }
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
            AssetDatabase.SaveAssets();
            Debug.Log("JAMKIT_LOOK atmosphere feature installed in " + PcRenderer);
        }
    }
}
