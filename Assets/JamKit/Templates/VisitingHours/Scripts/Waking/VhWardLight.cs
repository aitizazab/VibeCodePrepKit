using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace JamKit.VisitingHours
{
    /// <summary>
    /// The ward's light (docs/redesign/day.md, "The look"): a late-afternoon sun low in the west, pouring down the corridor
    /// from its end window and through the side rooms' windows; the sky (an HDRI) through every window; cool fluorescent
    /// panels inside; a soft trilight ambient; realtime box-projected reflection probes so the polished vinyl and the steel
    /// pick up the windows; and a grade (ACES, a little bloom, warm white balance, a light vignette). The booth gets the
    /// night version. Everything it makes hangs under the place's root and goes with it; the render settings are set again
    /// by the next place. Until the Look agent's hospital-day rig lands (docs/redesign/look.md), this is the ward's.
    /// </summary>
    public static class VhWardLight
    {
        public static readonly Color SunColor = new Color(1f, 0.84f, 0.64f);
        /// <summary>The sun: 24 degrees up, from the west-south-west (it travels east down the corridor).</summary>
        public static readonly Vector3 SunEuler = new Vector3(24f, 76f, 0f);

        /// <summary>The ward by day: the sun, the sky, the ambient, the probes and the grade. Returns the bedside's
        /// depth-of-field volume (weight 0 until the bedside raises it).</summary>
        public static Volume Day(Transform root, VhWardLibrary lib, Bounds[] probeRooms)
        {
            var cam = MainCamera();
            var sunGo = new GameObject("Sun (late afternoon)");
            sunGo.transform.SetParent(root, false);
            sunGo.transform.rotation = Quaternion.Euler(SunEuler);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = SunColor;
            sun.intensity = 2.6f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.95f;
            sun.shadowBias = 0.04f;
            sun.shadowNormalBias = 0.3f;
            RenderSettings.sun = sun;

            RenderSettings.skybox = lib != null ? lib.sky : null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.50f, 0.56f, 0.64f);
            RenderSettings.ambientEquatorColor = new Color(0.46f, 0.45f, 0.42f);
            RenderSettings.ambientGroundColor = new Color(0.24f, 0.22f, 0.2f);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.fog = false;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = 0.8f;

            if (cam != null)
            {
                cam.clearFlags = lib != null && lib.sky != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.62f, 0.68f, 0.74f);
                cam.allowHDR = true;
                var cd = cam.GetUniversalAdditionalCameraData();
                cd.renderPostProcessing = true;
                cd.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                cd.antialiasingQuality = AntialiasingQuality.High;
                cd.stopNaN = true;
                cd.renderShadows = true;
            }

            // Realtime reflection probes, rendered once the place is built (box-projected to each space).
            if (probeRooms != null)
                foreach (var b in probeRooms)
                {
                    var pg = new GameObject("Reflection probe");
                    pg.transform.SetParent(root, false);
                    pg.transform.localPosition = new Vector3(b.center.x, 1.5f, b.center.z);
                    var p = pg.AddComponent<ReflectionProbe>();
                    p.mode = ReflectionProbeMode.Realtime;
                    p.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
                    p.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
                    p.boxProjection = true;
                    p.size = new Vector3(b.size.x, 3.2f, b.size.z);
                    p.center = new Vector3(0f, 0f, 0f);
                    p.resolution = 128;
                    p.hdr = true;
                    p.intensity = 1f;
                    p.blendDistance = 1f;
                    p.clearFlags = ReflectionProbeClearFlags.Skybox;
                    pg.AddComponent<RenderOnce>();
                }

            var grade = Profile(root, "Ward day grade");
            grade.Add<Tonemapping>().mode.Override(TonemappingMode.ACES);
            var ca = grade.Add<ColorAdjustments>();
            ca.postExposure.Override(0.35f);
            ca.contrast.Override(10f);
            ca.saturation.Override(-2f);
            var wb = grade.Add<WhiteBalance>();
            wb.temperature.Override(7f);
            wb.tint.Override(2f);
            var smh = grade.Add<ShadowsMidtonesHighlights>();
            smh.shadows.Override(new Vector4(0.96f, 1f, 1.06f, 0f));
            smh.highlights.Override(new Vector4(1.04f, 1.01f, 0.96f, 0f));
            var bloom = grade.Add<Bloom>();
            bloom.threshold.Override(1.05f);
            bloom.intensity.Override(0.45f);
            bloom.scatter.Override(0.68f);
            bloom.clamp.Override(20f);
            var vig = grade.Add<Vignette>();
            vig.intensity.Override(0.2f);
            vig.smoothness.Override(0.5f);
            Volume(root, "Ward day grade (volume)", grade, 1f, 20f);

            // The bedside's shallow focus (her hand and his; the rest of the room soft).
            var dofProfile = Profile(root, "Bedside focus");
            var dof = dofProfile.Add<DepthOfField>();
            dof.mode.Override(DepthOfFieldMode.Gaussian);
            dof.gaussianStart.Override(1.3f);
            dof.gaussianEnd.Override(4.5f);
            dof.gaussianMaxRadius.Override(0.8f);
            dof.highQualitySampling.Override(true);
            return Volume(root, "Bedside focus (volume)", dofProfile, 0f, 30f);
        }

        /// <summary>The booth at 11 PM: no sun, no sky, a dark cool ambient, the grade of a night room.</summary>
        public static void Night(Transform root)
        {
            var cam = MainCamera();
            RenderSettings.sun = null;
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.10f, 0.12f, 0.17f);
            RenderSettings.ambientEquatorColor = new Color(0.08f, 0.08f, 0.1f);
            RenderSettings.ambientGroundColor = new Color(0.04f, 0.04f, 0.05f);
            RenderSettings.fog = false;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.reflectionIntensity = 0.3f;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                cam.allowHDR = true;
                var cd = cam.GetUniversalAdditionalCameraData();
                cd.renderPostProcessing = true;
                cd.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                cd.stopNaN = true;
            }
            var grade = Profile(root, "Booth night grade");
            grade.Add<Tonemapping>().mode.Override(TonemappingMode.ACES);
            var ca = grade.Add<ColorAdjustments>();
            ca.postExposure.Override(0.55f);
            ca.contrast.Override(12f);
            ca.saturation.Override(-6f);
            grade.Add<WhiteBalance>().temperature.Override(-4f);
            var bloom = grade.Add<Bloom>();
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(0.6f);
            bloom.clamp.Override(20f);
            var vig = grade.Add<Vignette>();
            vig.intensity.Override(0.3f);
            Volume(root, "Booth night grade (volume)", grade, 1f, 20f);
        }

        /// <summary>A place that isn't the ward or the booth (the slice's own street): the camera as the scene had it.</summary>
        public static void Leave(Camera cam)
        {
            RenderSettings.sun = null;
            RenderSettings.skybox = null;
            if (cam == null) return;
            cam.clearFlags = CameraClearFlags.SolidColor;
            var cd = cam.GetUniversalAdditionalCameraData();
            cd.renderPostProcessing = false;
        }

        static Camera MainCamera()
        {
            var g = VisitingHoursGame.Current;
            if (g != null && g.cam != null) return g.cam;
            return Camera.main;
        }

        static VolumeProfile Profile(Transform root, string name)
        {
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            p.name = name;
            var keeper = root.GetComponent<Owned>();
            if (keeper == null) keeper = root.gameObject.AddComponent<Owned>();
            keeper.profiles.Add(p);
            return p;
        }

        static Volume Volume(Transform root, string name, VolumeProfile profile, float weight, float priority)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            var v = go.AddComponent<Volume>();
            v.isGlobal = true;
            v.priority = priority;
            v.sharedProfile = profile;
            v.weight = weight;
            return v;
        }

        /// <summary>Destroys the place's volume profiles with it.</summary>
        sealed class Owned : MonoBehaviour
        {
            public readonly System.Collections.Generic.List<VolumeProfile> profiles = new System.Collections.Generic.List<VolumeProfile>();

            void OnDestroy()
            {
                foreach (var p in profiles) VhUtil.Kill(p);
            }
        }

        /// <summary>Renders its reflection probe once, a frame after the place is built (everything's in by then).</summary>
        sealed class RenderOnce : MonoBehaviour
        {
            int _frames;

            void Update()
            {
                if (++_frames < 2) return;
                var p = GetComponent<ReflectionProbe>();
                if (p != null) p.RenderProbe();
                enabled = false;
            }
        }
    }
}
