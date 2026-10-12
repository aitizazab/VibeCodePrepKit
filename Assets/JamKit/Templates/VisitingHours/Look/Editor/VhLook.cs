using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace JamKit.Look.EditorTools
{
    /// <summary>
    /// Visiting Hours' look, for the scene builders (World, House, Day, ...). Call from editor code after building a scene:
    /// <code>
    /// var rig = VhLook.ApplyGoldenHour(scene, new VhLook.Options { sunAzimuth = 250f });   // the hill
    /// VhLook.SetupCamera(playerCamera);                                                     // HDR, post, AA, far clip
    /// var brick = VhLook.Material("Brick_Red");                                             // the library (metre UVs)
    /// VhLookUv.BoxProject(wallGameObject);                                                  // metre UVs on a box mesh
    /// VhLook.AddReflectionProbe(scene, center, size);                                       // per street / room
    /// // save the scene, then (main runs it, GPU): VhLook.BakeScene(scenePath)
    /// </code>
    /// Everything lives under one root ("VH Look ...") that Apply* replaces, so it is safe to re-run. See docs/redesign/look.md.
    /// </summary>
    public static partial class VhLook
    {
        public const string RootPrefix = "VH Look";
        public const string FlarePath = VhLookLibrary.MatRoot + "VH_SunFlare.asset";

        public enum Preset { GoldenHour, HouseInterior, HospitalDay }

        /// <summary>Lighting options. Defaults are the golden hour of the hill.</summary>
        public sealed class Options
        {
            /// <summary>Degrees above the horizon (golden hour 8-14; the shadows are long and soft at 10).</summary>
            public float sunElevation = 10f;
            /// <summary>Compass bearing of the sun, degrees from world +Z towards +X.</summary>
            public float sunAzimuth = 250f;
            public string sky = VhLookSky.Default;
            public float sunIntensity = 3.2f;
            public Color sunColor = new Color(1f, 0.84f, 0.66f);   // pass 4: the only warm thing (round 3: everything orange)
            public float bounceIntensity = 0.35f;
            public Color bounceColor = new Color(0.9f, 0.92f, 0.82f);
            public float ambientIntensity = 1f;
            public float shadowDistance = 160f;
            public bool lensFlare = false;   // pass 4: off (round 3: a hard orange disc on the hills); the sky draws its own glow
            /// <summary>Height (world y) where the haze is densest: the bottom of the valley.</summary>
            public float hazeBaseHeight = 0f;
            /// <summary>Disable other directional lights already in the scene (they would double the sun).</summary>
            public bool disableOtherSuns = true;
        }

        public static Material Material(string name) => VhLookLibrary.Get(name);

        /// <summary>The hill at golden hour: HDRI sky, warm low sun with long soft shadows, bounce fill, haze, sun shafts, the
        /// golden-hour grade, the sun's lens flare.</summary>
        public static VhLookRig ApplyGoldenHour(Scene scene, Options o = null) => Apply(scene, Preset.GoldenHour, o ?? new Options());

        /// <summary>The Lasname house inside: the same evening sun (pools through the windows) and sky, darker ambient, warm
        /// interior grade, shorter and finer shadows, no haze. Lamps are the House agent's (warm 2700 K point/spot lights).</summary>
        public static VhLookRig ApplyHouseInterior(Scene scene, Options o = null)
        {
            o = o ?? new Options { ambientIntensity = 0.55f, shadowDistance = 45f, bounceIntensity = 0.15f, lensFlare = false };
            return Apply(scene, Preset.HouseInterior, o);
        }

        /// <summary>The hospital by day: a higher, whiter sun, the bluest of the skies, a cool grade. Fluorescent panels are the
        /// Day agent's (cool white 5000 K area/point lights).</summary>
        public static VhLookRig ApplyHospitalDay(Scene scene, Options o = null)
        {
            o = o ?? new Options
            {
                sunElevation = 38f, sunAzimuth = 200f, sky = "Sky_EveningRoad01_2k", sunIntensity = 2.4f,
                sunColor = new Color(1f, 0.96f, 0.9f), bounceIntensity = 0.12f, bounceColor = new Color(0.9f, 0.95f, 1f),
                shadowDistance = 60f, lensFlare = false,
            };
            return Apply(scene, Preset.HospitalDay, o);
        }

        static VhLookRig Apply(Scene scene, Preset preset, Options o)
        {
            if (!scene.IsValid()) scene = SceneManager.GetActiveScene();
            var previous = SceneManager.GetActiveScene();
            if (previous != scene) SceneManager.SetActiveScene(scene);
            try
            {
                foreach (var go in scene.GetRootGameObjects())
                    if (go.name.StartsWith(RootPrefix, StringComparison.Ordinal)) UnityEngine.Object.DestroyImmediate(go);
                if (o.disableOtherSuns)
                {
                    foreach (var go in scene.GetRootGameObjects())
                        foreach (var l in go.GetComponentsInChildren<Light>(true))
                            if (l.type == LightType.Directional && l.enabled)
                            {
                                l.enabled = false;
                                Debug.Log("JAMKIT_LOOK disabled an older sun: " + l.name);
                            }
                }

                var root = new GameObject(RootPrefix + " (" + preset + ")");
                SceneManager.MoveGameObjectToScene(root, scene);

                var sun = new GameObject("Sun").AddComponent<Light>();
                sun.transform.SetParent(root.transform, false);
                sun.type = LightType.Directional;
                sun.color = o.sunColor;
                sun.intensity = o.sunIntensity;
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = 1f;
                sun.lightmapBakeType = LightmapBakeType.Mixed;
                sun.bounceIntensity = 1f;
                var sunData = sun.gameObject.AddComponent<UniversalAdditionalLightData>();
                sunData.usePipelineSettings = true;
                sunData.softShadowQuality = SoftShadowQuality.High;
                if (o.lensFlare)
                {
                    var flare = sun.gameObject.AddComponent<LensFlareComponentSRP>();
                    flare.lensFlareData = SunFlare();
                    flare.intensity = 1f;
                    flare.useOcclusion = true;
                    // A radius about the sun's own: partly hidden behind leaves, the flare flickers and dims instead of
                    // switching off (pass 1's 0.02 was a point test, all or nothing).
                    flare.occlusionRadius = 0.15f;
                    flare.sampleCount = 32;
                    flare.allowOffScreen = false;
                    flare.scale = 1f;
                }

                var bounce = new GameObject("Bounce (sunlit ground, fill)").AddComponent<Light>();
                bounce.transform.SetParent(root.transform, false);
                bounce.type = LightType.Directional;
                bounce.color = o.bounceColor;
                bounce.intensity = o.bounceIntensity;
                bounce.shadows = LightShadows.None;
                bounce.lightmapBakeType = LightmapBakeType.Realtime;
                bounce.bounceIntensity = 0f;
                bounce.gameObject.AddComponent<UniversalAdditionalLightData>();

                VhLookSky.TryFind(o.sky, out var sky);
                var rig = root.AddComponent<VhLookRig>();
                rig.sun = sun;
                rig.bounce = bounce;
                rig.sunElevation = o.sunElevation;
                rig.sunAzimuth = o.sunAzimuth;
                rig.hdriSunAzimuth = sky.sunAzimuth;
                rig.hdriSunElevation = sky.sunElevation;
                rig.shadowDistance = o.shadowDistance;
                if (o.shadowDistance < 80f) rig.cascadeSplits = new Vector3(0.08f, 0.22f, 0.48f);
                rig.lightingBaked = false;

                var volumeGo = new GameObject("Post (" + preset + ")");
                volumeGo.transform.SetParent(root.transform, false);
                var volume = volumeGo.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 10f;
                volume.sharedProfile = preset == Preset.GoldenHour ? VhLookProfiles.BuildGoldenHour(o.sky)
                    : preset == Preset.HouseInterior ? VhLookProfiles.BuildHouseInterior()
                    : VhLookProfiles.BuildHospitalDay();
                if (preset == Preset.GoldenHour && volume.sharedProfile.TryGet<VhAtmosphere>(out var atmo))
                    atmo.hazeBaseHeight.Override(o.hazeBaseHeight);

                SetRenderSettings(sun, preset, o);
                rig.Apply();
                EditorSceneManager.MarkSceneDirty(scene);
                Debug.Log("JAMKIT_LOOK applied " + preset + " to " + scene.name + " (sky " + o.sky + ", sun " + o.sunElevation +
                          " deg up at bearing " + o.sunAzimuth + ")");
                return rig;
            }
            finally
            {
                if (previous.IsValid() && previous != scene) SceneManager.SetActiveScene(previous);
            }
        }

        /// <summary>A camera path for the <c>-perf-probe</c> player flag (VhPerfProbe flies it and logs frame-time percentiles):
        /// eye positions and look-at points, a leg every <paramref name="secondsPerLeg"/> seconds.</summary>
        public static VhPerfPath AddPerfPath(Transform parent, Vector3[] eyes, Vector3[] looks, float secondsPerLeg = 4f, float fieldOfView = 60f)
        {
            var go = new GameObject("Perf path");
            if (parent != null) go.transform.SetParent(parent, false);
            var path = go.AddComponent<VhPerfPath>();
            path.eyes = eyes;
            path.looks = looks;
            path.secondsPerLeg = secondsPerLeg;
            path.fieldOfView = fieldOfView;
            return path;
        }

        /// <summary>The player's (or any gameplay) camera: HDR, post-processing on, SMAA high, NaN guard, dithering, far clip
        /// 4 km (the terrain ring, VhLookBackdrop, reaches 2-3 km).</summary>
        public static void SetupCamera(Camera cam, float farClip = 4000f)
        {
            if (cam == null) return;
            cam.allowHDR = true;
            cam.farClipPlane = Mathf.Max(cam.farClipPlane, farClip);
            cam.nearClipPlane = Mathf.Min(cam.nearClipPlane, 0.05f);
            var data = cam.GetComponent<UniversalAdditionalCameraData>();
            if (data == null) data = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            data.stopNaN = true;
            data.dithering = true;
            data.renderShadows = true;
        }
    }
}
