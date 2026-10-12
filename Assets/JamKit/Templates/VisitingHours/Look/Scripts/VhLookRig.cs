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
    /// hillside gets long soft shadows, and puts them back when disabled, so the other templates keep theirs. Pass 3: the
    /// numbers asked for are capped by the active quality tier (<see cref="JamKit.Perf.VhPerfTiers.Current"/>, while a
    /// Visiting Hours scene has applied one), so the rig never asks for more than the tier allows.</item>
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
        public float shadowDistance = 160f;
        [Range(1, 4)] public int shadowCascades = 4;
        public Vector3 cascadeSplits = new Vector3(0.035f, 0.11f, 0.32f);
        public float cascadeBorder = 0.15f;
        public int shadowResolution = 4096;
        public float shadowDepthBias = 0.6f;
        public float shadowNormalBias = 0.45f;

        [Header("Ambient for the Look's own shaders (foliage, far land): linear, hemispherical")]
        [Tooltip("Ambient from straight up (the sky's average), from the horizon, and from below (sunlit ground bounce).")]
        public Color ambientSky = new Color(0.62f, 0.72f, 0.9f);
        public Color ambientEquator = new Color(0.62f, 0.6f, 0.62f);
        public Color ambientGround = new Color(0.16f, 0.17f, 0.11f);

        [Header("Environment")]
        [Tooltip("Set by VhLook.Bake once the scene's lighting (ambient, reflections, probes) is baked. If false, the sky's " +
                 "ambient and reflections are rebuilt from the skybox when the scene starts.")]
        public bool lightingBaked;

        static readonly int SunDirId = Shader.PropertyToID("_VhSunDir");
        static readonly int SunDiscDirId = Shader.PropertyToID("_VhSunDiscDir");
        static readonly int SunColorId = Shader.PropertyToID("_VhSunColor");
        static readonly int SkyRotationId = Shader.PropertyToID("_VhSkyRotation");
        static readonly int SkyMatrixId = Shader.PropertyToID("_VhSkyMatrix");
        static readonly int SkyTimeId = Shader.PropertyToID("_VhSkyTime");
        static readonly int AmbientSkyId = Shader.PropertyToID("_VhAmbientSky");
        static readonly int AmbientEquatorId = Shader.PropertyToID("_VhAmbientEquator");
        static readonly int AmbientGroundId = Shader.PropertyToID("_VhAmbientGround");

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
            // The sky is turned and tilted so the HDRI's own sun (its glow, its rays) sits exactly on the light, and the disc
            // is drawn there too: the disc, the lens flare (on the light) and the sun shafts all come from one point.
            Shader.SetGlobalVector(SunDiscDirId, toSun);
            Shader.SetGlobalMatrix(SkyMatrixId, SkyMatrix(toSun, sunAzimuth, hdriSunElevation, hdriSunAzimuth));
            Color c = sun != null ? sun.color * sun.intensity : Color.white;
            Shader.SetGlobalVector(SunColorId, new Vector4(c.r, c.g, c.b, 1f));
            Shader.SetGlobalFloat(SkyRotationId, sunAzimuth - hdriSunAzimuth);
            Shader.SetGlobalVector(AmbientSkyId, new Vector4(ambientSky.r, ambientSky.g, ambientSky.b, 1f));
            Shader.SetGlobalVector(AmbientEquatorId, new Vector4(ambientEquator.r, ambientEquator.g, ambientEquator.b, 1f));
            Shader.SetGlobalVector(AmbientGroundId, new Vector4(ambientGround.r, ambientGround.g, ambientGround.b, 1f));
            Shader.SetGlobalFloat(SkyTimeId, Application.isPlaying ? Time.time * cloudDrift : 0f);
        }

        /// <summary>
        /// World direction -> the HDRI's own frame: a yaw that brings the light's bearing round to the HDRI sun's, then the
        /// small tilt (a few degrees) that lifts or lowers the HDRI's sun onto the light's elevation. The sky shader's
        /// equirect lookup is done in that frame.
        /// </summary>
        public static Matrix4x4 SkyMatrix(Vector3 toSun, float sunAzimuthDeg, float hdriElevationDeg, float hdriAzimuthDeg)
        {
            float a = -(sunAzimuthDeg - hdriAzimuthDeg) * Mathf.Deg2Rad;
            float s = Mathf.Sin(a), c = Mathf.Cos(a);
            var yaw = Matrix4x4.identity;
            yaw.m00 = c; yaw.m01 = 0f; yaw.m02 = s;
            yaw.m10 = 0f; yaw.m11 = 1f; yaw.m12 = 0f;
            yaw.m20 = -s; yaw.m21 = 0f; yaw.m22 = c;
            Vector3 yawed = yaw.MultiplyVector(toSun);
            Vector3 native = Direction(hdriElevationDeg, hdriAzimuthDeg);
            var tilt = Matrix4x4.Rotate(Quaternion.FromToRotation(yawed, native));
            return tilt * yaw;
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
            float distance = shadowDistance;
            int cascades = shadowCascades, resolution = shadowResolution;
            if (JamKit.Perf.VhPerfTiers.Active)
            {
                var tier = JamKit.Perf.VhPerfTiers.Current;
                if (tier != null)
                {
                    distance = Mathf.Min(distance, tier.shadowDistance);
                    cascades = Mathf.Min(cascades, tier.shadowCascades);
                    resolution = Mathf.Min(resolution, tier.shadowResolution);
                }
            }
            _asset.shadowDistance = distance;
            _asset.shadowCascadeCount = Mathf.Clamp(cascades, 1, 4);
            _asset.cascade4Split = cascadeSplits;
            _asset.cascadeBorder = cascadeBorder;
            _asset.mainLightShadowmapResolution = resolution;
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
