using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace JamKit.Look.EditorTools
{
    public static partial class VhLook
    {
        static void SetRenderSettings(Light sun, Preset preset, Options o)
        {
            RenderSettings.skybox = VhLookSky.Material(o.sky);
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = o.ambientIntensity;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.defaultReflectionResolution = 256;
            RenderSettings.reflectionIntensity = preset == Preset.HouseInterior ? 0.6f : 1f;
            RenderSettings.reflectionBounces = 1;
            RenderSettings.fog = false;   // the atmosphere pass does the haze (height + distance + sun tint)
            DynamicGI.UpdateEnvironment();
        }

        /// <summary>A baked box-projected reflection probe (a street, a room). Baked by <see cref="BakeScene"/>.</summary>
        public static ReflectionProbe AddReflectionProbe(Scene scene, Vector3 center, Vector3 size, int importance = 1, string name = null)
        {
            var go = new GameObject(name ?? "Reflection probe");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.position = center;
            var p = go.AddComponent<ReflectionProbe>();
            p.mode = ReflectionProbeMode.Baked;
            p.size = size;
            p.boxProjection = true;
            p.importance = importance;
            p.resolution = 256;
            p.hdr = true;
            p.blendDistance = 2f;
            return p;
        }

        /// <summary>
        /// Marks a hierarchy's static scenery for baked GI: ContributeGI on every mesh renderer that isn't under an Animator,
        /// and receive GI from the probes (Adaptive Probe Volumes: per pixel, no lightmap UVs needed). Leaves batching flags
        /// alone, so moving set pieces are still free to move.
        /// </summary>
        public static int MarkStaticForGI(GameObject root)
        {
            int n = 0;
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.GetComponentInParent<Animator>() != null) continue;
                if (IsBackdrop(r.transform)) continue;
                var mat = r.sharedMaterial;
                if (mat != null && mat.shader != null && mat.shader.name == "JamKit/Look/Foliage") continue;   // thin cards: lit by the sky
                var flags = GameObjectUtility.GetStaticEditorFlags(r.gameObject);
                GameObjectUtility.SetStaticEditorFlags(r.gameObject, flags | StaticEditorFlags.ContributeGI | StaticEditorFlags.ReflectionProbeStatic);
                r.receiveGI = ReceiveGI.LightProbes;
                n++;
            }
            return n;
        }

        /// <summary>
        /// Far scenery that stays out of the probe bake (it would fill the probe volume with probes for things seen only
        /// through haze): anything under a GameObject whose name contains "(backdrop)". It is lit by the sky.
        /// </summary>
        public static bool IsBackdrop(Transform t)
        {
            for (; t != null; t = t.parent)
                if (t.name.IndexOf("(backdrop)", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        /// <summary>-executeMethod JamKit.Look.EditorTools.VhLook.BakeFromCommandLine -vhScene Assets/.../X.unity</summary>
        public static void BakeFromCommandLine()
        {
            int code = 0;
            try
            {
                string path = VhLookArgs.Get("-vhScene");
                if (string.IsNullOrEmpty(path)) throw new ArgumentException("pass -vhScene <scene path>");
                string spacing = VhLookArgs.Get("-vhProbeSpacing");
                float probeSpacing = spacing != null && float.TryParse(spacing, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var f) ? f : 2f;
                if (!BakeScene(path, probeSpacing)) code = 2;
            }
            catch (Exception e) { Debug.LogException(e); code = 1; }
            EditorApplication.Exit(code);
        }

        /// <summary>
        /// Bakes a saved scene's lighting: the sky's ambient and reflections, the reflection probes, and Adaptive Probe Volumes
        /// (sun and sky bounce, per pixel) over the static scenery (every renderer outside the VH Look root is marked with
        /// <see cref="MarkStaticForGI"/> first). GPU work: main runs it (Unity with graphics). Saves the scene. Returns false if
        /// the bake failed (the scene still renders, with the sky's ambient and the bounce fill).
        /// </summary>
        public static bool BakeScene(string scenePath, float minProbeSpacing = 2f)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            int marked = 0;
            foreach (var go in scene.GetRootGameObjects())
                if (!go.name.StartsWith(RootPrefix, StringComparison.Ordinal)) marked += MarkStaticForGI(go);

            ProbeVolume pv = null;
            foreach (var go in scene.GetRootGameObjects())
                if (pv == null) pv = go.GetComponentInChildren<ProbeVolume>(true);
            if (pv == null)
            {
                // No volume of the scene's own: one global volume (a scene that places a local one, like LookLab round its
                // street, keeps it: probes only where the player is, not over a whole hillside).
                var pvGo = new GameObject("VH Probe Volume (global)");
                SceneManager.MoveGameObjectToScene(pvGo, scene);
                pv = pvGo.AddComponent<ProbeVolume>();
                pv.mode = ProbeVolume.Mode.Global;
            }
            string dir = Path.GetDirectoryName(scenePath).Replace('\\', '/');
            string name = Path.GetFileNameWithoutExtension(scenePath);
            string setPath = dir + "/" + name + "_ProbeSet.asset";
            var probeSet = AssetDatabase.LoadAssetAtPath<ProbeVolumeBakingSet>(setPath);
            if (probeSet == null)
            {
                probeSet = ScriptableObject.CreateInstance<ProbeVolumeBakingSet>();
                probeSet.skyOcclusion = true;
                { VhLookLibrary.EnsureAssetFolder(setPath); AssetDatabase.CreateAsset(probeSet, setPath); }
            }
            probeSet.simplificationLevels = 3;       // bricks of 1, 3, 9, 27 spacings: dense near geometry, sparse in the open
            probeSet.minDistanceBetweenProbes = minProbeSpacing;
            probeSet.TryAddScene(AssetDatabase.AssetPathToGUID(scenePath));
            EditorUtility.SetDirty(probeSet);

            string lightingPath = dir + "/" + name + "_Lighting.lighting";
            var lighting = AssetDatabase.LoadAssetAtPath<LightingSettings>(lightingPath);
            if (lighting == null)
            {
                lighting = new LightingSettings { name = name + " Lighting" };
                { VhLookLibrary.EnsureAssetFolder(lightingPath); AssetDatabase.CreateAsset(lighting, lightingPath); }
            }
            ConfigureLighting(lighting);
            Lightmapping.lightingSettings = lighting;
            EditorUtility.SetDirty(lighting);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);

            WarmUpPipeline();
            Debug.Log("JAMKIT_LOOK baking " + scenePath + " (" + marked + " static renderers, probes every " + minProbeSpacing + " m)");
            bool ok = Lightmapping.Bake();
            Debug.Log("JAMKIT_LOOK bake " + (ok ? "done" : "FAILED") + ": " + scenePath);
            foreach (var go in scene.GetRootGameObjects())
            {
                var rig = go.GetComponentInChildren<VhLookRig>(true);
                if (rig == null) continue;
                rig.lightingBaked = ok;
                // The probes now carry the real bounce: the fake fill steps down.
                if (ok && rig.bounce != null) rig.bounce.intensity *= 0.5f;
                EditorUtility.SetDirty(rig);
            }
            EditorSceneManager.SaveScene(scene);
            return ok;
        }

        /// <summary>One render through URP, so the pipeline (and with it the probe-volume system the bake needs) exists in a
        /// batch-mode editor that hasn't drawn anything yet.</summary>
        static void WarmUpPipeline()
        {
            var go = new GameObject("VH bake warm-up camera") { hideFlags = HideFlags.HideAndDontSave };
            var rt = new RenderTexture(64, 64, 24);
            try
            {
                var cam = go.AddComponent<Camera>();
                var request = new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = rt };
                if (RenderPipeline.SupportsRenderRequest(cam, request)) RenderPipeline.SubmitRenderRequest(cam, request);
                else { cam.targetTexture = rt; cam.Render(); cam.targetTexture = null; }
            }
            catch (Exception e) { Debug.LogWarning("JAMKIT_LOOK warm-up render failed: " + e.Message); }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
            }
        }

        /// <summary>Baked indirect from the mixed sun and the sky; no lightmaps (everything reads the probes).</summary>
        static void ConfigureLighting(LightingSettings s)
        {
            s.bakedGI = true;
            s.realtimeGI = false;
            s.mixedBakeMode = MixedLightingMode.IndirectOnly;
            s.lightmapper = LightingSettings.Lightmapper.ProgressiveGPU;
            s.indirectSampleCount = 256;
            s.maxBounces = 2;
            s.lightmapResolution = 2f;
            s.ao = false;
        }

        // ---------- The sun's lens flare (SRP data-driven) ----------

        /// <summary>A restrained flare for a low warm sun: a soft glow, a faint ring, and a few small ghosts along the axis.</summary>
        public static LensFlareDataSRP SunFlare()
        {
            var data = AssetDatabase.LoadAssetAtPath<LensFlareDataSRP>(FlarePath);
            bool fresh = data == null;
            if (fresh) data = ScriptableObject.CreateInstance<LensFlareDataSRP>();
            // Rebuilt every time, so a tuned value reaches an existing asset. Pass 3 (round 2: "a giant soft orange flare
            // disc over the road"): the wide warm-glow element is gone, the glow round the sun is tighter and fainter.
            var list = new List<LensFlareDataElementSRP>
            {
                Element(SRPLensFlareType.Circle, 0f, 0.5f, new Color(1f, 0.78f, 0.5f), 0.3f, 1.2f),         // glow round the sun
                Element(SRPLensFlareType.Ring, 0f, 1.0f, new Color(1f, 0.85f, 0.65f), 0.07f, 0.6f),         // a faint halo ring
                Element(SRPLensFlareType.Polygon, 0.45f, 0.08f, new Color(0.9f, 0.75f, 0.45f), 0.12f, 0.3f), // ghosts on the axis
                Element(SRPLensFlareType.Circle, 0.8f, 0.13f, new Color(0.55f, 0.75f, 0.9f), 0.08f, 0.2f),
                Element(SRPLensFlareType.Polygon, 1.3f, 0.22f, new Color(0.85f, 0.6f, 0.4f), 0.06f, 0.3f),
                Element(SRPLensFlareType.Circle, 1.7f, 0.06f, new Color(1f, 0.8f, 0.5f), 0.09f, 0.4f),
            };
            data.elements = list.ToArray();
            if (fresh) { VhLookLibrary.EnsureAssetFolder(FlarePath); AssetDatabase.CreateAsset(data, FlarePath); }
            else EditorUtility.SetDirty(data);
            return data;
        }

        static LensFlareDataElementSRP Element(SRPLensFlareType type, float position, float size, Color tint, float intensity, float fallOff)
        {
            var e = new LensFlareDataElementSRP
            {
                flareType = type,
                position = position,
                uniformScale = size * 10f,
                tint = tint,
                localIntensity = intensity,
                fallOff = fallOff,
                blendMode = SRPLensFlareBlendMode.Additive,
                modulateByLightColor = true,
                sideCount = 6,
                sdfRoundness = 0.4f,
            };
            if (type == SRPLensFlareType.Ring) e.ringThickness = 0.15f;
            return e;
        }
    }
}
