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
    /// Stills of the dream scenes' art, for checking it without playing: Night 2's house, hide-and-seek and escape
    /// (<see cref="DreamHouseCapture.Stills"/>, with shots.txt), then the DreamEscape scene at fixed spots (the street,
    /// barbecue, garage, rooftops, the front door in the rain); turns the cut-outs to face the camera, renders 1920x1080
    /// through URP and writes PNGs to out/dream-captures/&lt;time&gt;/. Needs the scenes built (Rebuild Shell Scenes) and graphics
    /// (no -nographics): tools/dream-capture.ps1.
    /// </summary>
    public static class DreamCapture
    {
        public const int Width = 1920, Height = 1080;
        [MenuItem("JamKit/Dream/Capture Stills (house and escape)")]
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

            // Night 2 (the house, its hide-and-seek and its escape): DreamHouseCapture, into the same folder.
            DreamHouseCapture.Stills(dir);

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
            apply?.Invoke();
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
