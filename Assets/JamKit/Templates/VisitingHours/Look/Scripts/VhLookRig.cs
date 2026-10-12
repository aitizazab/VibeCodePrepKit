using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace JamKit.Look
{
    /// <summary>
    /// The scene's lighting rig at runtime (Visiting Hours' look, docs/redesign/look.md). Built by
    /// <c>VhLook.ApplyGoldenHour</c> / <c>ApplyHouseInterior</c> / <c>ApplyHospitalDay</c>; one per scene.
    /// <list type="bullet">
    /// <item>Aims the sun (and its bounce fill) down <see cref="sunElevation"/> / <see cref="sunAzimuth"/>, and turns the sky
    /// so the HDRI's own sun sits on the same bearing.</item>
    /// <item>Feeds the sky and the atmosphere pass their globals (sun direction and colour, sky rotation, time for the cloud drift).</item>
    /// <item>While enabled, overrides the active URP asset's main-light shadow settings (distance, cascades, resolution) so a
    /// hillside gets long soft shadows, and puts them back when disabled, so the other templates keep theirs.</item>
    /// </list>
    /// Azimuth is measured from world +Z towards +X (0 = the sun is due +Z, 90 = due +X).
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class VhLookRig : MonoBehaviour
    {
        public static VhLookRig Active;

        /// <summary>
        /// The shadow override runs in play mode and in builds. In the editor outside play mode it runs only while this is
        /// true (the capture rig turns it on for its renders), so the URP asset is never saved with the override in it.
        /// </summary>
        public static bool previewShadowsInEditor;

        [Header("Sun")]
        public Light sun;
        [Tooltip("Degrees above the horizon. Golden hour: 8-14.")]
        public float sunElevation = 10f;
        [Tooltip("Compass bearing of the sun, degrees from +Z towards +X.")]
        public float sunAzimuth = 250f;
        [Tooltip("A shadowless fill that stands in for light bounced off the sunlit ground (warm, from low on the anti-sun side).")]
        public Light bounce;
        public float bounceLift = 0.45f;

        [Header("Sky (set from the HDRI's analysis, ThirdParty/LookSky/skies.json)")]
        public float hdriSunAzimuth = 39.36f;
        public float hdriSunElevation = 6.98f;
        [Tooltip("Cloud drift speed multiplier (0 = still sky).")]
        public float cloudDrift = 1f;
        public bool hasSky = true;

        [Header("Shadows (main light; applied to the URP asset while this rig is enabled)")]
        public bool overrideShadows = true;
        public float shadowDistance = 200f;
        [Range(1, 4)] public int shadowCascades = 4;
        public Vector3 cascadeSplits = new Vector3(0.035f, 0.11f, 0.32f);
        public float cascadeBorder = 0.15f;
        public int shadowResolution = 4096;
        public float shadowDepthBias = 0.6f;
        public float shadowNormalBias = 0.45f;

        [Header("Environment")]
        [Tooltip("Set by VhLook.Bake once the scene's lighting (ambient, reflections, probes) is baked. If false, the sky's " +
                 "ambient and reflections are rebuilt from the skybox when the scene starts.")]
        public bool lightingBaked;

        static readonly int SunDirId = Shader.PropertyToID("_VhSunDir");
        static readonly int SunDiscDirId = Shader.PropertyToID("_VhSunDiscDir");
        static readonly int SunColorId = Shader.PropertyToID("_VhSunColor");
        static readonly int SkyRotationId = Shader.PropertyToID("_VhSkyRotation");
        static readonly int SkyTimeId = Shader.PropertyToID("_VhSkyTime");

        UniversalRenderPipelineAsset _asset;
        float _oldDistance, _oldBorder, _oldDepthBias, _oldNormalBias;
        int _oldCascades, _oldResolution;
        Vector3 _oldSplits;
        bool _applied;

        /// <summary>World direction towards the sun (unit).</summary>
        public Vector3 ToSun => Direction(sunElevation, sunAzimuth);

        public static Vector3 Direction(float elevationDeg, float azimuthDeg)
        {
            float el = elevationDeg * Mathf.Deg2Rad, az = azimuthDeg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(el) * Mathf.Sin(az), Mathf.Sin(el), Mathf.Cos(el) * Mathf.Cos(az));
        }

        void OnEnable()
        {
            Active = this;
            Apply();
            ApplyShadows();
            if (Application.isPlaying && !lightingBaked && hasSky) DynamicGI.UpdateEnvironment();
        }

        void OnDisable()
        {
            RestoreShadows();
            if (Active == this) Active = null;
        }

        void OnValidate()
        {
            if (!isActiveAndEnabled) return;
            Apply();
            RestoreShadows();
            ApplyShadows();
        }

        void Update()
        {
            Shader.SetGlobalFloat(SkyTimeId, Application.isPlaying ? Time.time * cloudDrift : 0f);
            // Someone may have swapped the pipeline asset (a quality change): keep the override on the active one.
            if (overrideShadows && _applied && !ReferenceEquals(_asset, GraphicsSettings.currentRenderPipeline))
            {
                RestoreShadows();
                ApplyShadows();
            }
        }

        /// <summary>Aims the lights and pushes the sky's globals. Safe to call any time (edit mode included).</summary>
        public void Apply()
        {
            var toSun = ToSun;
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.LookRotation(-toSun, Vector3.up);
                RenderSettings.sun = sun;
            }
            if (bounce != null)
            {
                var b = new Vector3(toSun.x, 0f, toSun.z);
                if (b.sqrMagnitude < 1e-4f) b = Vector3.forward;
                b = b.normalized;
                b.y = bounceLift;
                bounce.transform.rotation = Quaternion.LookRotation(b.normalized, Vector3.up);
            }
            Shader.SetGlobalVector(SunDirId, toSun);
            // The disc is drawn where the HDRI's own sun glow is (its elevation, our bearing): the light may sit a few
            // degrees higher for nicer shadows without a second glow appearing in the sky.
            Shader.SetGlobalVector(SunDiscDirId, Direction(hasSky ? hdriSunElevation : sunElevation, sunAzimuth));
            Color c = sun != null ? sun.color * sun.intensity : Color.white;
            Shader.SetGlobalVector(SunColorId, new Vector4(c.r, c.g, c.b, 1f));
            Shader.SetGlobalFloat(SkyRotationId, sunAzimuth - hdriSunAzimuth);
            Shader.SetGlobalFloat(SkyTimeId, Application.isPlaying ? Time.time * cloudDrift : 0f);
        }

        /// <summary>Re-applies (or removes) the shadow override after <see cref="previewShadowsInEditor"/> changes.</summary>
        public void RefreshShadows()
        {
            RestoreShadows();
            ApplyShadows();
        }

        void ApplyShadows()
        {
            if (!overrideShadows || _applied) return;
            if (!Application.isPlaying && !previewShadowsInEditor) return;
            _asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (_asset == null) return;
            _oldDistance = _asset.shadowDistance;
            _oldCascades = _asset.shadowCascadeCount;
            _oldSplits = _asset.cascade4Split;
            _oldBorder = _asset.cascadeBorder;
            _oldResolution = _asset.mainLightShadowmapResolution;
            _oldDepthBias = _asset.shadowDepthBias;
            _oldNormalBias = _asset.shadowNormalBias;
            _asset.shadowDistance = shadowDistance;
            _asset.shadowCascadeCount = shadowCascades;
            _asset.cascade4Split = cascadeSplits;
            _asset.cascadeBorder = cascadeBorder;
            _asset.mainLightShadowmapResolution = shadowResolution;
            _asset.shadowDepthBias = shadowDepthBias;
            _asset.shadowNormalBias = shadowNormalBias;
            _applied = true;
        }

        void RestoreShadows()
        {
            if (!_applied) return;
            if (_asset != null)
            {
                _asset.shadowDistance = _oldDistance;
                _asset.shadowCascadeCount = _oldCascades;
                _asset.cascade4Split = _oldSplits;
                _asset.cascadeBorder = _oldBorder;
                _asset.mainLightShadowmapResolution = _oldResolution;
                _asset.shadowDepthBias = _oldDepthBias;
                _asset.shadowNormalBias = _oldNormalBias;
            }
            _asset = null;
            _applied = false;
        }
    }
}
