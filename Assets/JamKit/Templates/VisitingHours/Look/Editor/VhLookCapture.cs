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
    /// The capture rig (for the judge, and reusable by the World and House agents): opens a saved scene, puts a camera at fixed
    /// shots, renders each at 1920x1080 through URP with post-processing (the scene's own volumes, the atmosphere pass, the
    /// lens flare) and writes PNGs to out/&lt;folder&gt;/&lt;time&gt;/. Batch mode needs graphics (no -nographics).
    /// <code>
    /// var shots = new List&lt;VhLookCapture.Shot&gt; { new VhLookCapture.Shot("street-wide", eye, lookAt, 60f) };
    /// string dir = VhLookCapture.Capture("Assets/.../Hill.unity", shots, "hill-captures");
    /// </code>
    /// The scene's player camera (Camera.main) is disabled for the shots and its settings copied, so the capture matches the game.
    /// </summary>
    public static class VhLookCapture
    {
        public const int Width = 1920, Height = 1080;

        public struct Shot
        {
            public string name;
            public Vector3 eye, look;
            public float fov;
            public Shot(string name, Vector3 eye, Vector3 look, float fov = 60f) { this.name = name; this.eye = eye; this.look = look; this.fov = fov; }
        }

        /// <summary>Captures the shots; returns the output folder. <paramref name="prepare"/> runs after the scene opens.</summary>
        public static string Capture(string scenePath, IList<Shot> shots, string folder, Action<Scene> prepare = null, int width = Width, int height = Height)
        {
            string dir = Path.Combine(Directory.GetCurrentDirectory(), "out", folder, DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(dir);
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            prepare?.Invoke(scene);
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
            var rt = new RenderTexture(new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGB32, 24) { sRGB = true, msaaSamples = 1 });
            var log = new List<string>();
            try
            {
                foreach (var s in shots)
                {
                    go.transform.position = s.eye;
                    go.transform.rotation = Quaternion.LookRotation(s.look - s.eye, Vector3.up);
                    cam.fieldOfView = s.fov;
                    // Two warm-up renders per shot: shadow atlases, the volume stack and the lens-flare occlusion settle.
                    Render(cam, rt);
                    Render(cam, rt);
                    string path = Path.Combine(dir, s.name + ".png");
                    Save(rt, path, width, height);
                    log.Add(s.name + "  eye " + s.eye + "  look " + s.look + "  fov " + s.fov);
                    Debug.Log("JAMKIT_LOOK_CAPTURE " + path);
                }
                File.WriteAllLines(Path.Combine(dir, "shots.txt"), log);
            }
            finally
            {
                VhLookRig.previewShadowsInEditor = false;
                foreach (var rig in rigs) if (rig != null) rig.RefreshShadows();
                if (main != null) main.enabled = true;
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
                UnityEngine.Object.DestroyImmediate(go);
            }
            return dir;
        }

        static void Render(Camera cam, RenderTexture rt)
        {
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, request)) RenderPipeline.SubmitRenderRequest(cam, request);
            else { cam.targetTexture = rt; cam.Render(); cam.targetTexture = null; }
        }

        static void Save(RenderTexture rt, string path, int width, int height)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }
    }
}
