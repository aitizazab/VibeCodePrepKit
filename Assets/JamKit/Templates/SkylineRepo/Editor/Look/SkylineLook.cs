using System;
using System.IO;
using JamKit.Look;
using JamKit.Look.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace JamKit.SkylineRepo.EditorTools
{
    /// <summary>
    /// Skyline Repo's afternoon look (Task 21; spec section 10 items 1-3; docs/SKYLINE-LOOK.md): a bright, sunny near-future
    /// afternoon over a glass mall atrium. Built on Visiting Hours' Look library: its rig's pattern copied, its side-effect-free
    /// calls used:
    /// <list type="bullet">
    /// <item><c>VhLook.ApplyGoldenHour</c>'s rig, copied with afternoon values (one Mixed sun with soft shadows, a shadowless
    /// bounce fill, Visiting Hours' runtime <see cref="VhLookRig"/> aiming them and the sky, a global post volume, the render
    /// settings);</item>
    /// <item>then the afternoon's own sky (a Poly Haven HDRI on Visiting Hours' sky shader, turned so its sun sits on the
    /// light), its own post profile (Look/SK_Afternoon.asset) and a shorter, cheaper shadow request;</item>
    /// <item><see cref="VhLook.SetupCamera"/> on every camera (HDR, post, SMAA, NaN guard; the far clip kept, since the
    /// viewmodel's rig sits 1 km below the arena);</item>
    /// <item>two box-projected reflection probes (the main hall, the warm-up hall) from <see cref="VhLook.AddReflectionProbe"/>,
    /// their cubemaps baked once at build time (the project's quality levels have realtime probes off).</item>
    /// </list>
    /// Nothing here writes into Visiting Hours' folders: their code is called only where it has no side effects
    /// (VhLookRig, VhLook.SetupCamera / AddReflectionProbe, VhLookLibrary.SetupLit / ValidateLit, VhLookUv) and their
    /// shader (JamKit/Look/Sky) and three prepared texture sets are read.
    /// </summary>
    public static class SkylineLook
    {
        /// <summary>The sun: 41 degrees up (mid-afternoon, long enough shadows to model the hall) from the west-south-west,
        /// so it rakes in over the south-west walls and lights the north half, the mezzanine and the east shops, and the
        /// skylight's beams stripe the floor.</summary>
        public const float SunElevation = 41f, SunAzimuth = 235f;
        public const float SunIntensity = 2.6f;
        public static readonly Color SunColor = new Color(1f, 0.95f, 0.87f);
        /// <summary>The ambient (linear): from above, round the horizon, from below (see ApplyAfternoon).</summary>
        public static readonly Color AmbientSky = new Color(0.6f, 0.66f, 0.78f), AmbientEquator = new Color(0.62f, 0.61f, 0.59f),
            AmbientGround = new Color(0.64f, 0.58f, 0.5f);
        public const string ProfilePath = SkylineLookLibrary.LookRoot + "SK_Afternoon.asset";

        public const string RootName = "Skyline Look (afternoon)";

        // ---- the comic-ink rig (docs/SKYLINE-STYLE.md; the afternoon rig above stays as the fallback)
        public const float InkSunIntensity = 1.7f, InkShadowDistance = 80f;   // fix round 2: 1.4 was too dim (round 1: 2.2 washed out)
        /// <summary>The ink rig's ambient (linear): darker than the afternoon's, so a room's look is decided by its own lights
        /// (rule 3) and the shade stays cool; the sunlit hall stays the bright, warm hero space.</summary>
        public static readonly Color InkAmbientSky = new Color(0.28f, 0.38f, 0.62f), InkAmbientEquator = new Color(0.30f, 0.35f, 0.46f),
            InkAmbientGround = new Color(0.28f, 0.27f, 0.28f);   // fix round 2: a cooler, bluer shade
        public const string InkProfilePath = SkylineLookLibrary.LookRoot + "Ink/SK_Ink.asset";
        public const string InkSkyPath = SkylineLookLibrary.LookRoot + "Ink/SK_Sky_Ink.mat";
        public const string InkRootName = "Skyline Look (ink)";

        /// <summary>
        /// Applies the afternoon to the open scene and returns the rig. This is <c>VhLook.ApplyGoldenHour</c>'s rig, copied
        /// (docs/redesign/look.md allows calling or copying): calling it would also rebuild Visiting Hours' own post profile
        /// and sky material, which are not in this tree, so it would write new files into their folders on every pass.
        /// Same parts, same runtime component (<see cref="VhLookRig"/>), Skyline's own assets.
        /// </summary>
        public static VhLookRig ApplyAfternoon(Scene scene)
        {
            // All or nothing: everything that can fail (imports, the sky's file and shader, the profile) is resolved before the
            // scene is touched, and a failure while building the rig removes the rig again, so the builder's fallback sun is
            // then the only sun.
            SkylineLookLibrary.ConfigureImporters();
            var info = SkylineLookLibrary.ReadSkyInfo();
            var skyMaterial = SkylineLookLibrary.SkyMaterial(info);
            var profile = Afternoon();
            if (!scene.IsValid()) scene = SceneManager.GetActiveScene();
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == RootName || go.name.StartsWith(VhLook.RootPrefix, StringComparison.Ordinal)) UnityEngine.Object.DestroyImmediate(go);

            var root = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            VhLookRig rig;
            Light sun;
            try
            {
                rig = BuildRig(root, info, profile, out sun);
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(root);
                throw;
            }
            foreach (var go in scene.GetRootGameObjects())
            {
                if (go == root) continue;
                foreach (var l in go.GetComponentsInChildren<Light>(true))
                    if (l.type == LightType.Directional && l.enabled) l.enabled = false;   // one sun
            }

            RenderSettings.skybox = skyMaterial;
            RenderSettings.sun = sun;
            // Fix round 1 (judge A 5): the sky's own ambient left the shaded walls at about a tenth of the light they need
            // and blue (RGB ~70/80/95 on white plaster in the build). A white atrium in sun is filled by its sunlit floor and
            // walls: a three-colour ambient, cool from above, warm-neutral round the horizon, warm from below (the sunlit
            // floor's bounce), so white in shade reads light warm grey-white, not slate. Linear values, handed over as sRGB
            // (Unity linearises the ambient colours; near 0.6 the two readings differ by only a few percent).
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = AmbientSky.gamma;
            RenderSettings.ambientEquatorColor = AmbientEquator.gamma;
            RenderSettings.ambientGroundColor = AmbientGround.gamma;
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.defaultReflectionResolution = 256;
            RenderSettings.reflectionIntensity = 1f;
            RenderSettings.reflectionBounces = 1;
            RenderSettings.fog = false;
            rig.Apply();
            DynamicGI.UpdateEnvironment();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("SKYLINE_LOOK afternoon applied (sun " + SunElevation + " deg up at bearing " + SunAzimuth + ", sky " +
                      SkylineLookLibrary.SkyName + ", HDRI sun " + info.sun_elevation.ToString("0.0") + "/" + info.sun_azimuth.ToString("0.0") + ")");
            return rig;
        }

        /// <summary>
        /// Applies the comic-ink look (docs/SKYLINE-STYLE.md) to the open scene and returns the rig: the afternoon's rig
        /// re-tuned. The sun as before (bearing <see cref="SunAzimuth"/>, <see cref="SunElevation"/> degrees up) at
        /// <see cref="InkSunIntensity"/> with hard shadows to <see cref="InkShadowDistance"/> m and no bounce fill; a darker
        /// Trilight ambient; the painted sky (<c>Skyline/InkSky</c>, which reads the rig's sun globals); the global post volume
        /// <c>SK_Ink</c> (neutral tonemapping, the bloom kept for the lights, a slight vignette, and the
        /// <see cref="SkylineInk"/> override on). The reflection probes stay (the builder bakes them). All or nothing, as
        /// <see cref="ApplyAfternoon"/>: a failure leaves the builder's fallback sun.
        /// </summary>
        public static VhLookRig ApplyInk(Scene scene)
        {
            SkylineLookLibrary.ConfigureImporters();   // the library's textures (props, products, signs) still need their import settings
            var skyMaterial = InkSkyMaterial();
            var profile = Ink();
            if (!scene.IsValid()) scene = SceneManager.GetActiveScene();
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == RootName || go.name == InkRootName || go.name.StartsWith(VhLook.RootPrefix, StringComparison.Ordinal))
                    UnityEngine.Object.DestroyImmediate(go);

            var root = new GameObject(InkRootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            VhLookRig rig;
            Light sun;
            try { rig = BuildInkRig(root, profile, out sun); }
            catch
            {
                UnityEngine.Object.DestroyImmediate(root);
                throw;
            }
            foreach (var go in scene.GetRootGameObjects())
            {
                if (go == root) continue;
                foreach (var l in go.GetComponentsInChildren<Light>(true))
                    if (l.type == LightType.Directional && l.enabled) l.enabled = false;   // one sun
            }
            RenderSettings.skybox = skyMaterial;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = InkAmbientSky.gamma;
            RenderSettings.ambientEquatorColor = InkAmbientEquator.gamma;
            RenderSettings.ambientGroundColor = InkAmbientGround.gamma;
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.defaultReflectionResolution = 256;
            RenderSettings.reflectionIntensity = 1f;
            RenderSettings.reflectionBounces = 1;
            RenderSettings.fog = false;
            rig.Apply();
            DynamicGI.UpdateEnvironment();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("SKYLINE_LOOK ink applied (sun " + SunElevation + " deg up at bearing " + SunAzimuth + ", intensity " + InkSunIntensity + ", hard shadows to " + InkShadowDistance + " m)");
            return rig;
        }

        static VhLookRig BuildInkRig(GameObject root, VolumeProfile profile, out Light sun)
        {
            sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(root.transform, false);
            sun.type = LightType.Directional;
            sun.color = SunColor;
            sun.intensity = InkSunIntensity;
            sun.shadows = LightShadows.Hard;
            sun.shadowStrength = 1f;
            sun.lightmapBakeType = LightmapBakeType.Mixed;
            sun.bounceIntensity = 0f;
            var sunData = sun.gameObject.AddComponent<UniversalAdditionalLightData>();
            sunData.usePipelineSettings = true;

            var rig = root.AddComponent<VhLookRig>();
            rig.sun = sun;
            rig.bounce = null;                      // the bounce fill is off: the ambient and the lights do the work
            rig.sunElevation = SunElevation;
            rig.sunAzimuth = SunAzimuth;
            rig.hasSky = false;                     // the painted sky is a procedural shader, not an HDRI
            rig.cloudDrift = 0.6f;                  // the InkSky's clouds read the rig's clock
            rig.lightingBaked = false;
            rig.shadowDistance = InkShadowDistance;
            rig.shadowCascades = 3;
            rig.cascadeSplits = new Vector3(0.06f, 0.18f, 0.42f);
            rig.shadowResolution = 2048;
            rig.ambientSky = InkAmbientSky;
            rig.ambientEquator = InkAmbientEquator;
            rig.ambientGround = InkAmbientGround;

            var volumeGo = new GameObject("Post (Skyline ink)");
            volumeGo.transform.SetParent(root.transform, false);
            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = profile;
            return rig;
        }

        /// <summary>The painted sky's material (Look/Ink/SK_Sky_Ink.mat on <c>Skyline/InkSky</c>), made once.</summary>
        static Material InkSkyMaterial()
        {
            var shader = Shader.Find("Skyline/InkSky");
            if (shader == null) throw new InvalidOperationException("Skyline/InkSky shader not found");
            var m = AssetDatabase.LoadAssetAtPath<Material>(InkSkyPath);
            if (m == null)
            {
                VhLookLibrary.EnsureAssetFolder(InkSkyPath);
                m = new Material(shader) { name = "SK_Sky_Ink" };
                AssetDatabase.CreateAsset(m, InkSkyPath);
            }
            else if (m.shader != shader) m.shader = shader;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>
        /// SK_Ink: neutral tonemapping (the flat palette must survive), exposure -0.15, contrast +8, saturation +4, bloom kept for
        /// the lights (threshold 1.1, intensity 0.8, scatter 0.6), a slight vignette (0.14), and the
        /// <see cref="SkylineInk"/> override on with its defaults (the renderer feature runs only under it).
        /// </summary>
        public static VolumeProfile Ink()
        {
            var p = Profile(InkProfilePath);
            Comp<Tonemapping>(p).mode.Override(TonemappingMode.Neutral);

            var ca = Comp<ColorAdjustments>(p);
            ca.postExposure.Override(-0.15f);   // fix round 2: -0.3 was too dark once the sun is stronger
            ca.contrast.Override(8f);
            ca.saturation.Override(4f);
            ca.colorFilter.Override(Color.white);

            var bloom = Comp<Bloom>(p);
            bloom.threshold.Override(1.1f);
            bloom.intensity.Override(0.8f);
            bloom.scatter.Override(0.6f);
            bloom.highQualityFiltering.Override(false);
            bloom.downscale.Override(BloomDownscaleMode.Quarter);

            var vig = Comp<Vignette>(p);
            vig.intensity.Override(0.14f);
            vig.smoothness.Override(0.5f);
            vig.color.Override(new Color(0.05f, 0.05f, 0.07f));

            var ink = Comp<SkylineInk>(p);
            ink.inkEnabled.Override(true);
            ink.lineWeightVariation.Override(1f);   // the creator's one pick of the five stylization options (2026-10-15): line weight
            EditorUtility.SetDirty(p);
            return p;
        }

        /// <summary>The rig's parts under <paramref name="root"/>: the sun, the bounce fill, <see cref="VhLookRig"/>, the post volume.</summary>
        static VhLookRig BuildRig(GameObject root, SkylineLookLibrary.SkyInfo info, VolumeProfile profile, out Light sun)
        {
            sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(root.transform, false);
            sun.type = LightType.Directional;
            sun.color = SunColor;
            sun.intensity = SunIntensity;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 1f;
            sun.lightmapBakeType = LightmapBakeType.Mixed;
            sun.bounceIntensity = 1f;
            var sunData = sun.gameObject.AddComponent<UniversalAdditionalLightData>();
            sunData.usePipelineSettings = true;
            sunData.softShadowQuality = SoftShadowQuality.Medium;   // the golden hour asks High; Medium is crisp enough at 80 m and cheaper

            var bounce = new GameObject("Bounce (sunlit floor, fill)").AddComponent<Light>();
            bounce.transform.SetParent(root.transform, false);
            bounce.type = LightType.Directional;
            bounce.color = new Color(1f, 0.93f, 0.82f);    // the sunlit cream floor's colour
            bounce.intensity = 0.42f;
            bounce.shadows = LightShadows.None;
            bounce.lightmapBakeType = LightmapBakeType.Realtime;
            bounce.bounceIntensity = 0f;
            bounce.gameObject.AddComponent<UniversalAdditionalLightData>();

            var rig = root.AddComponent<VhLookRig>();
            rig.sun = sun;
            rig.bounce = bounce;
            rig.sunElevation = SunElevation;
            rig.sunAzimuth = SunAzimuth;
            rig.hdriSunAzimuth = info.sun_azimuth;
            rig.hdriSunElevation = info.sun_elevation;
            rig.cloudDrift = 0.6f;
            rig.lightingBaked = false;
            // The arena is ~90 m end to end. Perf round 3: 60 m of shadow in three cascades at 2048 (was 80 m in four): the
            // fourth cascade re-drew the whole mall's casters every frame for the far 34-80 m; three cascades still split the
            // 2048 atlas into 1024 tiles, and the pipeline asset's three-cascade splits (10% / 30%) put them at 6 m and 18 m,
            // about where the first two were (4.8 m, 14.4 m), so near shadows keep their sharpness. Beyond 60 m (the far end
            // of the hall seen from the other) shadows fade out. The golden hour asks 160 m / 4096.
            rig.shadowDistance = 60f;
            rig.shadowCascades = 3;
            rig.cascadeSplits = new Vector3(0.06f, 0.18f, 0.42f);
            rig.shadowResolution = 2048;
            // For the Look's own shaders only (foliage, far land; none used here): a clear afternoon's hemisphere.
            rig.ambientSky = new Color(0.55f, 0.68f, 0.92f);
            rig.ambientEquator = new Color(0.66f, 0.68f, 0.7f);
            rig.ambientGround = new Color(0.3f, 0.29f, 0.27f);

            var volumeGo = new GameObject("Post (Skyline afternoon)");
            volumeGo.transform.SetParent(root.transform, false);
            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = profile;
            return rig;
        }

        // ------------------------------------------------------------------------------------------------ the post profile

        static VolumeProfile Profile(string path)
        {
            var p = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (p == null)
            {
                p = ScriptableObject.CreateInstance<VolumeProfile>();
                VhLookLibrary.EnsureAssetFolder(path);
                AssetDatabase.CreateAsset(p, path);
            }
            return p;
        }

        static T Comp<T>(VolumeProfile p) where T : VolumeComponent
        {
            if (!p.TryGet<T>(out var c))
            {
                c = p.Add<T>(false);
                c.name = typeof(T).Name;
                AssetDatabase.AddObjectToAsset(c, p);
            }
            c.active = true;
            return c;
        }

        /// <summary>
        /// SK_Afternoon: ACES; a touch of exposure and contrast for a crisp, bright stylized afternoon; saturation up a little
        /// (CONCIERGE's gold and the shop signs carry the colour); neutral-cool white balance (the sun is the only warm thing);
        /// split toning with sky-cool shadows; bloom on highlights only (threshold 1: sun glints, signs, light panels), no HQ
        /// filtering and a quarter-resolution start (Visiting Hours' perf recipe); a light vignette; no grain, no haze pass.
        /// </summary>
        public static VolumeProfile Afternoon()
        {
            var p = Profile(ProfilePath);
            Comp<Tonemapping>(p).mode.Override(TonemappingMode.ACES);

            var ca = Comp<ColorAdjustments>(p);
            // Sunlit white terrazzo is ~2.2 before the grade (sun 2.6 x sin 41 + the sky's ambient, x 0.85 albedo): +0.1 keeps it
            // just under ACES's shoulder, so sunlit white still shows its texture and the shade (~0.8) reads mid-grey-blue.
            ca.postExposure.Override(0.1f);
            ca.contrast.Override(12f);
            ca.saturation.Override(6f);
            ca.colorFilter.Override(Color.white);

            // Fix round 1 (judge A 5): the white walls in shade read slate blue; the grade no longer cools the shadows as much.
            var wb = Comp<WhiteBalance>(p);
            wb.temperature.Override(-1f);
            wb.tint.Override(0f);

            var st = Comp<SplitToning>(p);
            st.shadows.Override(new Color(0.47f, 0.49f, 0.53f));
            st.highlights.Override(new Color(0.53f, 0.51f, 0.48f));
            st.balance.Override(5f);

            var smh = Comp<ShadowsMidtonesHighlights>(p);
            smh.shadows.Override(new Vector4(1f, 1f, 1.02f, 0.02f));
            smh.midtones.Override(new Vector4(1f, 1f, 1f, 0f));
            smh.highlights.Override(new Vector4(1.02f, 1.01f, 0.98f, 0f));

            // Fix round 1 (judge A 6: in the sun the firm's orange read as the same lemon yellow as the bots' gold). ACES, like
            // every per-channel curve, slides a bright orange towards yellow as its red channel reaches the shoulder (the green
            // keeps climbing). Pulling the orange hues a little towards red BEFORE the curve (hue vs hue, in the grading
            // space) keeps them orange in the sun, and pulls gold less, so the two stay apart. Simulated with URP's own ACES
            // (tools/skyline/look/grade-check.py): the Breacher's orange (0.95, 0.45, 0.08) lit at 2.0-2.8 lands at hue 29-34
            // (was 38-43); the bots' gold (0.86, 0.66, 0.22) at 48-50 (was 49.5-51); white is unchanged.
            var curves = Comp<ColorCurves>(p);
            curves.hueVsHue.Override(new TextureCurve(HueVsHueKeys(), 0.5f, true, new Vector2(0f, 1f)));

            var bloom = Comp<Bloom>(p);
            // 1.6, not 1.0: with the library's albedo right (fix round 1), sunlit white sits at ~1.4 before the grade and
            // must not glow; signs (~2.3), light panels (~5) and sun glints still do.
            bloom.threshold.Override(1.6f);
            bloom.intensity.Override(0.65f);
            bloom.scatter.Override(0.62f);
            bloom.tint.Override(new Color(1f, 0.97f, 0.92f));
            bloom.highQualityFiltering.Override(false);
            bloom.downscale.Override(BloomDownscaleMode.Quarter);

            var vig = Comp<Vignette>(p);
            vig.intensity.Override(0.16f);
            vig.smoothness.Override(0.5f);
            vig.color.Override(new Color(0.05f, 0.05f, 0.07f));

            EditorUtility.SetDirty(p);
            return p;
        }

        /// <summary>The hue-vs-hue curve: hue (0..1) to 0.5 + shift (in turns). Orange (10-32 degrees in the grading space)
        /// moves up to 8 degrees towards red, gold (40-48) 2 degrees, nothing beyond 60 or below 0.</summary>
        static Keyframe[] HueVsHueKeys()
        {
            var pts = new[] { (0f, 0f), (10f, -4f), (22f, -8f), (32f, -6f), (40f, -2f), (48f, -2f), (60f, 0f) };
            var keys = new Keyframe[pts.Length];
            for (int i = 0; i < pts.Length; i++)
            {
                // Linear tangents between the points (a piecewise-linear curve, as simulated).
                float t = pts[i].Item1 / 360f, v = 0.5f + pts[i].Item2 / 360f;
                float inT = i > 0 ? (v - (0.5f + pts[i - 1].Item2 / 360f)) / (t - pts[i - 1].Item1 / 360f) : 0f;
                float outT = i < pts.Length - 1 ? ((0.5f + pts[i + 1].Item2 / 360f) - v) / (pts[i + 1].Item1 / 360f - t) : 0f;
                keys[i] = new Keyframe(t, v, inT, outT);
            }
            return keys;
        }

        // ------------------------------------------------------------------------------------------------ cameras

        /// <summary>HDR, post-processing, SMAA, NaN guard and dithering on every camera of the scene (the player's and the
        /// stills'), keeping each camera's own far clip.</summary>
        public static int SetupCameras(Scene scene)
        {
            int n = 0;
            foreach (var go in scene.GetRootGameObjects())
                foreach (var cam in go.GetComponentsInChildren<Camera>(true))
                {
                    VhLook.SetupCamera(cam, 0f);
                    // perf round 3: nothing in Skyline samples the camera's opaque copy, but the PC pipeline asset asks for one
                    // (a full-screen copy every frame at 1080p): the cameras opt out
                    var data = cam.GetComponent<UniversalAdditionalCameraData>();
                    if (data != null) data.requiresColorOption = CameraOverrideOption.Off;
                    n++;
                }
            return n;
        }

        // ------------------------------------------------------------------------------------------------ reflections

        /// <summary>
        /// The arena's reflections: one box-projected probe over the main hall and one over the warm-up hall
        /// (<see cref="VhLook.AddReflectionProbe"/>), each baked to a cubemap next to the generated scene and set as its custom
        /// texture, so the glass, the steel and the polished floors reflect the hall and the afternoon sky. Every renderer under
        /// <paramref name="staticRoots"/> is marked reflection-probe static first. A failed bake leaves the sky's reflection.
        /// </summary>
        public static void BakeReflections(Scene scene, string scenePath, params Transform[] staticRoots)
        {
            foreach (var root in staticRoots)
            {
                if (root == null) continue;
                foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var flags = GameObjectUtility.GetStaticEditorFlags(r.gameObject);
                    GameObjectUtility.SetStaticEditorFlags(r.gameObject, flags | StaticEditorFlags.ReflectionProbeStatic);
                }
            }
            var hall = VhLook.AddReflectionProbe(scene, new Vector3(0f, 6f, 0f), new Vector3(50.8f, 14.4f, 40.8f), 1, "Reflection probe (main hall)");
            var warm = VhLook.AddReflectionProbe(scene, new Vector3(0f, 3.5f, -34.2f), new Vector3(30.8f, 8.4f, 28.4f), 1, "Reflection probe (warm-up hall)");
            string dir = Path.GetDirectoryName(scenePath).Replace('\\', '/');
            string name = Path.GetFileNameWithoutExtension(scenePath);
            WarmUpPipeline();
            // The hall at 256 (judge A 13: at 128 the polished floor showed a red menu board as a soft red stain).
            Bake(hall, dir + "/" + name + "_Reflections_Hall.exr", 256);
            Bake(warm, dir + "/" + name + "_Reflections_Warmup.exr", 128);
        }

        static void Bake(ReflectionProbe p, string path, int resolution)
        {
            p.resolution = resolution;
            p.hdr = true;
            p.blendDistance = 1f;
            p.clearFlags = ReflectionProbeClearFlags.Skybox;
            p.shadowDistance = 60f;
            try
            {
                VhLookLibrary.EnsureAssetFolder(path);
                p.mode = ReflectionProbeMode.Baked;
                bool ok = Lightmapping.BakeReflectionProbe(p, path);
                if (ok)
                {
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    var cube = AssetDatabase.LoadAssetAtPath<Texture>(path);
                    if (cube != null)
                    {
                        p.mode = ReflectionProbeMode.Custom;
                        p.customBakedTexture = cube;
                        Debug.Log("SKYLINE_LOOK reflections baked: " + p.name + " -> " + path);
                        return;
                    }
                }
                Debug.LogWarning("SKYLINE_LOOK reflections NOT baked (the sky's reflection is used): " + p.name);
            }
            catch (Exception e)
            {
                Debug.LogWarning("SKYLINE_LOOK reflections bake failed (the sky's reflection is used): " + p.name + ": " + e.Message);
            }
            p.mode = ReflectionProbeMode.Baked;
        }

        /// <summary>One render through URP so the pipeline exists in a batch-mode editor before the probe bake (Visiting
        /// Hours' bake does the same).</summary>
        static void WarmUpPipeline()
        {
            var go = new GameObject("Skyline bake warm-up camera") { hideFlags = HideFlags.HideAndDontSave };
            var rt = new RenderTexture(64, 64, 24);
            try
            {
                var cam = go.AddComponent<Camera>();
                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
                if (RenderPipeline.SupportsRenderRequest(cam, request)) RenderPipeline.SubmitRenderRequest(cam, request);
                else { cam.targetTexture = rt; cam.Render(); cam.targetTexture = null; }
            }
            catch (Exception e) { Debug.LogWarning("SKYLINE_LOOK warm-up render failed: " + e.Message); }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
            }
        }
    }
}
