using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace JamKit.DreamEscape.EditorTools
{
    /// <summary>
    /// Stills of the dream scenes' art, for checking it without playing (art passes of 2026-10-11): opens the built
    /// DreamHouse and DreamEscape scenes, puts a camera at fixed spots (the kitchen from the slide's landing, Lena and
    /// Theo, Theo's room, the sofa, the photo wall, the fridge, both front doors, the umbrella closed and open, the tipped
    /// hallway; the escape's street, barbecue, garage and rooftops), turns the cut-outs to face it, renders 1920x1080 through
    /// URP and writes PNGs to out/dream-captures/&lt;time&gt;/. Needs the scenes built (Rebuild Shell Scenes) and graphics
    /// (no -nographics): tools/dream-capture.ps1.
    /// </summary>
    public static partial class DreamCapture
    {
        public const int Width = 1920, Height = 1080;
        const float F = DreamHouse.Ground;
        /// <summary>The camera stands where Nadia's eyes are (MoveDef.eyeHeight, 1.5 m above her feet). Until 2026-10-12 the
        /// house stills were taken from 1.6-1.65 m, which made her look taller than Lena.</summary>
        const float Eye = DreamHouseBuilder.NadiaEye;

        [MenuItem("JamKit/Dream House/Capture Stills")]
        static void Menu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorUtility.RevealInFinder(Capture());
        }

        /// <summary>-executeMethod JamKit.DreamEscape.EditorTools.DreamCapture.CaptureFromCommandLine</summary>
        public static void CaptureFromCommandLine()
        {
            int code = 0;
            try { Debug.Log("JAMKIT_DREAM_CAPTURE_OK " + Capture()); }
            catch (Exception e) { Debug.LogException(e); code = 1; }
            EditorApplication.Exit(code);
        }

        struct Spot
        {
            public string name; public Vector3 eye, look; public string show;
            public Spot(string name, Vector3 eye, Vector3 look, string show = null) { this.name = name; this.eye = eye; this.look = look; this.show = show; }
        }

        public static string Capture()
        {
            string dir = Path.Combine(Directory.GetCurrentDirectory(), "out", "dream-captures", DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(dir);

            var house = new List<Spot>
            {
                new Spot("house-01-front-garden-door", new Vector3(0f, Eye, -9f), new Vector3(0f, 1.6f, 0.5f)),
                new Spot("house-02-hallway-from-door", new Vector3(0f, Eye, -1.5f), new Vector3(0f, 1.2f, 20f)),
                // The house's rooms (the architecture pass, 2026-10-12).
                new Spot("house-03-kitchen-from-slide", new Vector3(0f, F + Eye, 72.6f), new Vector3(-3.4f, F + 0.8f, 76.5f)),
                new Spot("house-04-table-and-lena", new Vector3(-0.6f, F + Eye, 77.6f), new Vector3(-4.8f, F + 1.0f, 73.8f)),
                new Spot("house-05-counter-stove", new Vector3(-0.8f, F + Eye, 75.4f), new Vector3(-6.6f, F + 1.1f, 72.8f)),
                new Spot("house-06-fridge-calendar", new Vector3(-4.4f, F + Eye, 77.1f), new Vector3(-6.2f, F + 1.3f, 77.1f)),
                new Spot("house-07-theo-counting", new Vector3(0.8f, F + Eye, 76.2f), new Vector3(-3.4f, F + 0.9f, 73.6f)),
                new Spot("house-08-living-room", new Vector3(3.2f, F + Eye, 72.9f), new Vector3(7.4f, F + 1.0f, 76.4f)),
                new Spot("house-08b-fireplace", new Vector3(4.6f, F + Eye, 75.2f), new Vector3(7.4f, F + 1.2f, 75.2f)),
                new Spot("house-08c-bay-window", new Vector3(5.6f, F + Eye, 76.8f), new Vector3(5.2f, F + 0.9f, 71.3f)),
                new Spot("house-08d-pass-through", new Vector3(-0.8f, F + Eye, 76.4f), new Vector3(5.5f, F + 1.2f, 75.3f)),
                new Spot("house-09-hall", new Vector3(-5.0f, F + Eye, 79.4f), new Vector3(8f, F + 1.2f, 79.3f)),
                new Spot("house-09b-photo-wall", new Vector3(-2.0f, F + Eye, 78.55f), new Vector3(-2.0f, F + 1.55f, 80.6f)),
                new Spot("house-09c-hatch-from-below", new Vector3(0.8f, F + Eye, 78.7f), new Vector3(-0.7f, F + 2.7f, 79.4f)),
                new Spot("house-10-parents-room", new Vector3(-2.0f, F + Eye, 81.2f), new Vector3(-5.2f, F + 0.8f, 83.4f)),
                new Spot("house-10b-bath", new Vector3(-0.8f, F + Eye, 81.2f), new Vector3(0.9f, F + 0.8f, 83.2f)),
                new Spot("house-11-theos-room", new Vector3(3.4f, F + Eye, 83.4f), new Vector3(7.6f, F + 1.2f, 82.6f)),
                new Spot("house-11b-theos-desk-bookcase", new Vector3(4.4f, F + Eye, 83.6f), new Vector3(7.7f, F + 1.7f, 82.0f)),
                new Spot("house-11c-attic-stairs-door", new Vector3(5.2f, F + Eye, 82.2f), new Vector3(2.9f, F + 1.0f, 83.5f)),
                new Spot("house-12-attic-stairs-up", new Vector3(2.2f, F + Eye, DreamHouse.StairZ1 - 0.2f), new Vector3(2.2f, DreamHouse.AtticFloor + 0.3f, 80.6f)),
                new Spot("house-12b-attic-arrival", new Vector3(2.2f, DreamHouse.AtticFloor + Eye, 80.4f), new Vector3(-6.5f, DreamHouse.AtticFloor + 1.5f, 78f)),
                new Spot("house-12c-attic-observatory", new Vector3(-3.4f, DreamHouse.AtticFloor + Eye, 78.4f), new Vector3(-7f, DreamHouse.AtticFloor + 1.6f, 78f)),
                new Spot("house-12bc-attic-through-the-sheets", new Vector3(0.4f, DreamHouse.AtticFloor + Eye, 78.5f), new Vector3(-6.6f, DreamHouse.AtticFloor + 1.6f, 78.3f)),
                new Spot("house-12d-attic-storage", new Vector3(-0.6f, DreamHouse.AtticFloor + Eye, 78.6f), new Vector3(5.0f, DreamHouse.AtticFloor + 0.6f, 75f)),
                new Spot("house-12e-attic-hatch", new Vector3(1.2f, DreamHouse.AtticFloor + Eye, 77.6f), new Vector3(-0.7f, DreamHouse.AtticFloor, 79.4f)),
                // Round 4: the observatory under its raised roof, the front door from the hall, the house from outside.
                new Spot("house-12f-observatory-skylight", new Vector3(-3.6f, DreamHouse.AtticFloor + Eye, 79.6f), new Vector3(-6.0f, DreamHouse.AtticFloor + 4.3f, 78.0f)),
                new Spot("house-12g-observatory-across", new Vector3(-3.3f, DreamHouse.AtticFloor + Eye, 73.2f), new Vector3(-6.6f, DreamHouse.AtticFloor + 2.2f, 82.6f)),
                new Spot("house-09d-front-door-inside", new Vector3(4.0f, F + Eye, 79.4f), new Vector3(8f, F + 1.2f, 79.4f)),
                new Spot("exterior-01-from-the-street", new Vector3(-9f, Eye, -20f), new Vector3(-1f, 3.2f, 5f)),
                new Spot("exterior-02-garden-left", new Vector3(-12.5f, Eye, -6f), new Vector3(-6f, 3.5f, 8f)),
                new Spot("exterior-03-garden-right", new Vector3(12.5f, Eye, -6f), new Vector3(3f, 4f, 7f)),
                new Spot("exterior-04-porch", new Vector3(2.5f, Eye, -5f), new Vector3(-1f, 2.2f, 0f)),
                new Spot("exterior-05-observatory-roof", new Vector3(11.5f, 2.6f, 5f), new Vector3(0f, 7.6f, 13f)),
                new Spot("exterior-06-side-garden-left", new Vector3(-12.5f, Eye, 8.5f), new Vector3(-7.5f, 2.6f, 2.2f)),
                new Spot("exterior-07-observatory-from-the-side", new Vector3(-12.8f, 2.4f, 9.8f), new Vector3(-4f, 7.2f, 14f)),
                new Spot("house-13-tipped-hallway", new Vector3(0f, Eye, 190f), new Vector3(1.5f, 1.2f, 214f)),
                new Spot("house-14-way-out-door", new Vector3(0f, -3.3f, 311.5f), new Vector3(0f, -3.6f, 319f)),
                new Spot("house-15-umbrella-closed", new Vector3(1.2f, -3.75f, 315.6f), new Vector3(2.4f, -4.45f, 317.6f)),
                new Spot("house-16-umbrella-open", Vector3.zero, Vector3.zero, "umbrella-open"),
            };
            // -houseVariants all|walls,ceilings,cornices,heights,attic (tools/dream-capture.ps1 -Variants ...): the finish
            // options for the creator to choose from; "only" captures just those, skipping the usual stills.
            string variants = VariantsArg();
            if (variants == "only") { HouseVariants(dir, "all"); return dir; }
            Scene(DreamHouseBuilder.ScenePath, house, dir);
            if (!string.IsNullOrEmpty(variants)) HouseVariants(dir, variants);

            var escape = new List<Spot>
            {
                new Spot("escape-01-walts-driveway", new Vector3(-9f, 1.7f, -5f), new Vector3(-5f, 1.0f, 30f)),
                new Spot("escape-02-street-side", new Vector3(6f, 3f, 10f), new Vector3(-6f, 1.0f, 35f)),
                new Spot("escape-03-car-roofs", new Vector3(-1.5f, 2.4f, 95.5f), new Vector3(-4.3f, -0.3f, 106f)),
                new Spot("escape-04-barbecue", new Vector3(-4f, 1.7f, 121f), new Vector3(3f, 0f, 140f)),
                new Spot("escape-05-garage", new Vector3(0f, -1.0f, 200f), new Vector3(-8f, -0.6f, 205f)),
                new Spot("escape-06-rooftops", new Vector3(0f, 4.5f, 238f), new Vector3(0f, 2f, 270f)),
                new Spot("escape-07-front-door-rain", new Vector3(1.5f, -2.2f, 464f), new Vector3(0f, -3.6f, 486f)),
            };
            Scene(DreamEscapeBuilder.ScenePath, escape, dir);
            return dir;
        }

        static void Scene(string path, List<Spot> spots, string dir, Action apply = null)
        {
            if (!File.Exists(path)) { Debug.LogError("JAMKIT_DREAM_CAPTURE missing scene (rebuild the shell scenes first): " + path); return; }
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            apply?.Invoke();   // a finish variant (DreamCaptureVariants), applied to the open scene only: never saved
            var main = Camera.main;
            var body = new List<Renderer>();
            if (main != null) body.AddRange(main.transform.root.GetComponentsInChildren<Renderer>(true));
            var go = new GameObject("Capture camera");
            var cam = go.AddComponent<Camera>();
            if (main != null) { cam.CopyFrom(main); main.enabled = false; }
            cam.fieldOfView = 70f;
            cam.nearClipPlane = 0.05f;
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = main != null && main.GetComponent<UniversalAdditionalCameraData>() is { } md && md.renderPostProcessing;
            var rt = new RenderTexture(new RenderTextureDescriptor(Width, Height, RenderTextureFormat.ARGB32, 24) { sRGB = true, msaaSamples = 1 });
            try
            {
                Shot(cam, rt, null, dir);   // warm-up
                foreach (var s in spots)
                {
                    Vector3 eye = s.eye, look = s.look;
                    foreach (var r in body) if (r != null) r.enabled = s.show == "umbrella-open";
                    if (s.show == "umbrella-open")
                    {
                        var open = FindByName("Umbrella (open, over her)");
                        if (open == null) { Debug.LogWarning("JAMKIT_DREAM_CAPTURE no open umbrella"); continue; }
                        open.SetActive(true);
                        look = open.transform.position;
                        eye = look + new Vector3(2.4f, -0.4f, -2.4f);
                    }
                    go.transform.position = eye;
                    go.transform.rotation = Quaternion.LookRotation(look - eye, Vector3.up);
                    FaceCutOuts(eye);
                    Shot(cam, rt, s.name, dir);
                }
            }
            finally
            {
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        static GameObject FindByName(string name)
        {
            foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name == name) return t.gameObject;
            return null;
        }

        /// <summary>The cut-outs turn to the player's camera at runtime; in the editor they're turned here.</summary>
        static void FaceCutOuts(Vector3 eye)
        {
            var turners = new List<Transform>();
            foreach (var f in UnityEngine.Object.FindObjectsByType<DreamFaceCamera>(FindObjectsSortMode.None)) turners.Add(f.transform);
            foreach (var c in UnityEngine.Object.FindObjectsByType<DreamCutoutView>(FindObjectsSortMode.None)) turners.Add(c.transform);
            foreach (var t in turners)
            {
                var d = eye - t.position;
                d.y = 0f;
                if (d.sqrMagnitude > 1e-4f) t.rotation = Quaternion.Euler(0f, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, 0f);
            }
        }

        static void Shot(Camera cam, RenderTexture rt, string name, string dir)
        {
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, request)) RenderPipeline.SubmitRenderRequest(cam, request);
            else { cam.targetTexture = rt; cam.Render(); cam.targetTexture = null; }
            if (name == null) return;
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            string path = Path.Combine(dir, name + ".png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            Debug.Log("JAMKIT_DREAM_CAPTURE " + path);
        }
    }
}
