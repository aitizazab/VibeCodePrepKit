using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using JamKit.DeepBlock;
using JamKit.Look;
using JamKit.Look.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace JamKit.DreamEscape.EditorTools
{
    /// <summary>
    /// Stills of Larch Hill for the judge (docs/redesign/world.md "Captures"): the 25 views core <see cref="LarchHill"/>
    /// lists, 1920 x 1080, lit by the scene's own Look rig (golden hour, sky, post, haze), to out/hill-captures/&lt;time&gt;/.
    /// "-hillDreamChanges all" captures with the bedside's changes on (the plank, the lit hospital, the ledger).
    /// <para>Round 2 after the judge (docs/redesign/judge/hill-1.md):</para>
    /// <list type="bullet">
    /// <item><b>Shaders are compiled first</b> (<see cref="WarmShaders"/>): every material the hill uses, on a small lit
    /// stage, before the hill is opened. The judged capture's magenta came from "out of memory during compilation": the
    /// shader compiler runs inside the session's memory cap, and compiling with the whole hill loaded ran out.</item>
    /// <item><b>The camera clears to the sky</b> (the player's camera cleared to a flat colour) and Nadia's own body is
    /// hidden (the arrival shot stood inside it).</item>
    /// <item><b>The cut-outs face each shot</b> (they turned to the nearest of all 25 eyes: Walt showed his back).</item>
    /// <item><b>Every still is checked</b>: magenta pixels (a broken shader) over <see cref="MagentaLimit"/> fail the run
    /// (exit 3; the stills are still written); the top band's variation (a flat clear colour: no sky) is logged.
    /// Results in check.txt.</item>
    /// </list>
    /// Needs the scene built and graphics: tools/street-capture.ps1. The recorded run is a PlayMode test
    /// (DreamStreetPlayTests.RecordTheNight1CriticalPath).
    /// </summary>
    public static class DreamCaptureStreet
    {
        public const int Width = 1920, Height = 1080;
        /// <summary>The share of a still's pixels that may be magenta (r and b high, g low) before the capture fails.</summary>
        public const float MagentaLimit = 0.001f;

        [MenuItem("JamKit/Dream Street/Capture Stills")]
        static void Menu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorUtility.RevealInFinder(CaptureStreet(false, out _));
        }

        /// <summary>-executeMethod JamKit.DreamEscape.EditorTools.DreamCaptureStreet.CaptureStreetFromCommandLine [-hillDreamChanges all]</summary>
        public static void CaptureStreetFromCommandLine()
        {
            int code = 0;
            try
            {
                bool changes = Array.IndexOf(Environment.GetCommandLineArgs(), "-hillDreamChanges") >= 0;
                string dir = CaptureStreet(changes, out bool passed);
                Debug.Log((passed ? "JAMKIT_DREAM_CAPTURE_OK " : "JAMKIT_DREAM_CAPTURE_FAILED_CHECKS ") + dir);
                if (!passed) code = 3;
            }
            catch (Exception e) { Debug.LogException(e); code = 1; }
            EditorApplication.Exit(code);
        }

        public static string CaptureStreet(bool dreamChanges, out bool passed)
        {
            var report = new HillBuildReport("capture");
            WarmShaders(report);
            var h = LarchHill.Build();
            string dir = Path.Combine(Directory.GetCurrentDirectory(), "out", dreamChanges ? "hill-captures-changed" : "hill-captures", DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(dir);
            var scene = EditorSceneManager.OpenScene(DreamStreetBuilder.ScenePath, OpenSceneMode.Single);
            report.Phase("set up", "the scene is open");
            report.SceneTotals(scene);

            var game = Object.FindFirstObjectByType<DreamStreetGame>(FindObjectsInactive.Include);
            if (game != null)
            {
                if (dreamChanges)
                {
                    if (game.creekPlank != null) game.creekPlank.SetActive(true);
                    if (game.hospitalLit != null) game.hospitalLit.SetActive(true);
                }
                if (game.ledger != null) game.ledger.SetActive(true);
                // Nadia's own body and arms are hers, not the scenery's (the arrival's eye is where she stands).
                if (game.player != null)
                    foreach (var r in game.player.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
                // The shutter part-way up, so it reads as a shutter in the mill's arch.
                if (game.shutter != null)
                {
                    float gap = LarchHill.ShutterOpening * 0.55f;
                    game.shutter.localScale = new Vector3(1f, LarchHill.ArchTop - LarchHill.LaneY - gap, 1f);
                    game.shutter.position = new Vector3(game.shutter.position.x, LarchHill.LaneY + gap, game.shutter.position.z);
                }
            }
            var cutouts = Object.FindObjectsByType<DreamCutoutView>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            VhLookRig.previewShadowsInEditor = true;
            var rigs = new List<VhLookRig>();
            foreach (var root in scene.GetRootGameObjects())
            {
                var rig = root.GetComponentInChildren<VhLookRig>(true);
                if (rig == null) continue;
                rigs.Add(rig);
                rig.Apply();
                rig.RefreshShadows();
                if (!rig.lightingBaked) DynamicGI.UpdateEnvironment();
            }
            var main = Camera.main;
            var go = new GameObject("VH capture camera");
            var cam = go.AddComponent<Camera>();
            if (main != null) { cam.CopyFrom(main); main.enabled = false; }
            VhLook.SetupCamera(cam);
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.nearClipPlane = 0.05f;
            var rt = new RenderTexture(new RenderTextureDescriptor(Width, Height, RenderTextureFormat.ARGB32, 24) { sRGB = true, msaaSamples = 1 });
            var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            var log = new List<string>();
            var check = new StringBuilder();
            passed = true;
            report.Phase("render " + h.views.Count + " shots");
            try
            {
                foreach (var v in h.views)
                {
                    var eye = new Vector3(v.eye.X, v.eye.Y, v.eye.Z);
                    var look = new Vector3(v.look.X, v.look.Y, v.look.Z);
                    go.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(look - eye, Vector3.up));
                    cam.fieldOfView = 70f;
                    // The cut-outs within sight turn to this shot (as they turn to watch her in play).
                    foreach (var f in cutouts)
                    {
                        if (f == null) continue;
                        var d = eye - f.transform.position;
                        d.y = 0f;
                        if (d.sqrMagnitude > 1e-4f && d.sqrMagnitude < 120f * 120f) f.transform.rotation = Quaternion.Euler(0f, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, 0f);
                    }
                    Render(cam, rt);
                    Render(cam, rt);
                    var prev = RenderTexture.active;
                    RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                    tex.Apply();
                    RenderTexture.active = prev;
                    string path = Path.Combine(dir, v.name + ".png");
                    File.WriteAllBytes(path, tex.EncodeToPNG());
                    Measure(tex, out float magenta, out float skyVariation);
                    bool ok = magenta <= MagentaLimit;
                    string line = v.name + ": magenta " + (magenta * 100f).ToString("0.00") + "%, top band variation " + skyVariation.ToString("0.0") + (ok ? "" : "  FAIL (magenta: a broken shader)");
                    check.AppendLine(line);
                    Debug.Log("JAMKIT_STREET_CHECK " + line);
                    if (!ok) passed = false;
                    log.Add(v.name + "  eye " + eye + "  look " + look + "  fov 70");
                    Debug.Log("JAMKIT_LOOK_CAPTURE " + path);
                    report.Sample();
                }
                File.WriteAllLines(Path.Combine(dir, "shots.txt"), log);
                check.AppendLine(passed ? "PASS" : "FAIL");
                File.WriteAllText(Path.Combine(dir, "check.txt"), check.ToString());
            }
            finally
            {
                VhLookRig.previewShadowsInEditor = false;
                foreach (var rig in rigs) if (rig != null) rig.RefreshShadows();
                if (main != null) main.enabled = true;
                rt.Release();
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(tex);
                Object.DestroyImmediate(go);
            }
            report.Finish();
            return dir;
        }

        static void Render(Camera cam, RenderTexture rt)
        {
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, request)) RenderPipeline.SubmitRenderRequest(cam, request);
            else { cam.targetTexture = rt; cam.Render(); cam.targetTexture = null; }
        }

        /// <summary>The share of magenta pixels (the error shader), and how much the top tenth of the frame varies (a
        /// flat clear colour there means no sky).</summary>
        public static void Measure(Texture2D tex, out float magenta, out float topVariation)
        {
            var px = tex.GetPixels32();
            int w = tex.width, h = tex.height, n = 0;
            for (int i = 0; i < px.Length; i++)
            {
                var c = px[i];
                if (c.r > 180 && c.b > 180 && c.g < 90) n++;
            }
            magenta = n / (float)px.Length;
            // The top band (GetPixels32 starts at the bottom row).
            double sum = 0, sum2 = 0;
            int count = 0;
            for (int y = h - h / 10; y < h; y += 2)
                for (int x = 0; x < w; x += 4)
                {
                    var c = px[y * w + x];
                    double l = 0.299 * c.r + 0.587 * c.g + 0.114 * c.b;
                    sum += l;
                    sum2 += l * l;
                    count++;
                }
            double mean = sum / Math.Max(1, count);
            topVariation = (float)Math.Sqrt(Math.Max(0.0, sum2 / Math.Max(1, count) - mean * mean));
        }

        /// <summary>
        /// Compiles the shader variants the hill needs before the hill is loaded: a small stage with the Look agent's golden
        /// hour (sun with cascaded shadows, the sky, post, haze), two shadowed point lights and a reflection probe, and a
        /// cube for every material the DreamStreet scene uses, rendered a few times. Variants compile while the editor is
        /// small, and the shader cache keeps them for the hill.
        /// </summary>
        public static void WarmShaders(HillBuildReport report = null)
        {
            var mats = new List<Material>();
            foreach (var dep in AssetDatabase.GetDependencies(DreamStreetBuilder.ScenePath, true))
                if (dep.EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
                {
                    var m = AssetDatabase.LoadAssetAtPath<Material>(dep);
                    if (m != null) mats.Add(m);
                }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var rig = VhLook.ApplyGoldenHour(scene, new VhLook.Options { sunAzimuth = 250f, sunElevation = 10f, hazeBaseHeight = 0f });
            var camGo = new GameObject("Warm-up camera");
            var cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<UniversalAdditionalCameraData>();
            VhLook.SetupCamera(cam);
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.fieldOfView = 70f;
            camGo.transform.SetPositionAndRotation(new Vector3(0f, 2f, -2f), Quaternion.Euler(10f, 0f, 0f));
            for (int i = 0; i < 2; i++)
            {
                var l = new GameObject("Warm-up lamp " + i).AddComponent<Light>();
                l.type = LightType.Point;
                l.range = 12f;
                l.intensity = 2f;
                l.shadows = LightShadows.Soft;
                l.transform.position = new Vector3(i == 0 ? -3f : 3f, 2.5f, 6f);
            }
            VhLook.AddReflectionProbe(scene, new Vector3(0f, 2f, 8f), new Vector3(30f, 10f, 30f));
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.localScale = new Vector3(4f, 1f, 4f);
            ground.transform.position = new Vector3(0f, 0f, 10f);
            const int cols = 16;
            var cubes = new List<GameObject>();
            for (int i = 0; i < mats.Count; i++)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(cube.GetComponent<Collider>());
                cube.transform.position = new Vector3((i % cols - cols * 0.5f) * 0.7f, 0.5f + (i / cols) * 0.7f, 8f);
                cube.transform.localScale = Vector3.one * 0.5f;
                var r = cube.GetComponent<MeshRenderer>();
                r.sharedMaterial = mats[i];
                r.shadowCastingMode = ShadowCastingMode.On;
                cube.SetActive(false);
                cubes.Add(cube);
            }
            if (rig != null) { rig.Apply(); rig.RefreshShadows(); }
            VhLookRig.previewShadowsInEditor = true;
            var rt = new RenderTexture(new RenderTextureDescriptor(640, 360, RenderTextureFormat.ARGB32, 24) { sRGB = true });
            try
            {
                // One material at a time (few compiler jobs at once: each needs memory), then all together.
                foreach (var c in cubes)
                {
                    c.SetActive(true);
                    Render(cam, rt);
                    c.SetActive(false);
                }
                foreach (var c in cubes) c.SetActive(true);
                for (int k = 0; k < 2; k++) Render(cam, rt);
            }
            finally
            {
                VhLookRig.previewShadowsInEditor = false;
                if (rig != null) rig.RefreshShadows();
                rt.Release();
                Object.DestroyImmediate(rt);
            }
            Debug.Log("JAMKIT_STREET shaders warmed on " + mats.Count + " materials");
            report?.Phase("open the scene", "shaders warmed on " + mats.Count + " materials");
        }
    }
}
