using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace JamKit.VisitingHours.EditorTools
{
    /// <summary>
    /// Stills of the opening's and the title's lighting at their key moments (docs/OPENING-AND-TITLE.md "Lighting"), for
    /// judging the lighting without playing the opening: builds both sets in an empty scene, poses each moment, renders it
    /// at 1920x1080 through URP (post-processing included) on the PC quality level and on the one WebGL uses, and writes PNGs
    /// to out/vh-lighting/&lt;time&gt;/. Batch mode needs graphics (no -nographics): tools/vh-lighting-capture.ps1.
    /// </summary>
    public static class VhLightingCapture
    {
        public const int Width = 1920, Height = 1080;

        [MenuItem("JamKit/Visiting Hours/Capture the Lighting")]
        static void Menu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string dir = Capture();
            EditorUtility.RevealInFinder(dir);
        }

        /// <summary>-executeMethod JamKit.VisitingHours.EditorTools.VhLightingCapture.CaptureFromCommandLine</summary>
        public static void CaptureFromCommandLine()
        {
            int code = 0;
            try
            {
                string dir = Capture();
                Debug.Log("JAMKIT_VH_CAPTURE_OK " + dir);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                code = 1;
            }
            EditorApplication.Exit(code);
        }

        public static string Capture()
        {
            string dir = Path.Combine(Directory.GetCurrentDirectory(), "out", "vh-lighting", DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(dir);
            int before = QualitySettings.GetQualityLevel();
            try
            {
                Variant(dir, "pc", Level("PC", before));
                Variant(dir, "webgl", Level("Mobile", 0));
            }
            finally
            {
                QualitySettings.SetQualityLevel(before, true);
            }
            return dir;
        }

        static int Level(string name, int fallback)
        {
            var names = QualitySettings.names;
            for (int i = 0; i < names.Length; i++) if (names[i] == name) return i;
            return fallback;
        }

        static void Variant(string dir, string tag, int level)
        {
            QualitySettings.SetQualityLevel(level, true);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var boot = VisitingHoursBoot.Load();
            var stage = VhStageBuilder.Build(boot, false);
            var rig = stage.GetComponent<VhLightingRig>();
            if (rig != null) rig.Engage();
            var rt = new RenderTexture(new RenderTextureDescriptor(Width, Height, RenderTextureFormat.ARGB32, 24) { sRGB = true, msaaSamples = 1 });
            try
            {
                // 1. The dark room: from her pillow, the streetlight's stripes on the ceiling, the phone ringing.
                stage.ResetForOpening();
                // A first render warms the pipeline up (shadow atlases, post-processing textures); it isn't kept.
                Shot(dir, tag, null, stage, rig, rt);
                Shot(dir, tag, "1-dark-room", stage, rig, rt);
                // 2. The phone lit on the nightstand.
                stage.MoveCamera("nightstand", 0f);
                stage.SetPhone("incoming", 0f, 1f);
                Shot(dir, tag, "2-phone-lit", stage, rig, rt);
                // 3. In her hand, close: the screen's light on her fingers.
                stage.PlayArm("lift", 0f);
                stage.SetPhone("playing", 0f, 1f);
                Shot(dir, tag, "3-phone-held", stage, rig, rt);
                // 4. The drop: from above, the phone back on the nightstand, still playing; Nadia under the blanket.
                stage.PlayArm("drop", 0f);
                stage.PlayArm("hide", 0f);
                stage.MoveCamera("overhead", 0f);
                stage.SetPhone("dim", 0f, 0.75f);
                Shot(dir, tag, "4-drop-overhead", stage, rig, rt);
                // 5. Towards "G'night": the room going dark, the screen last.
                stage.SetLights(0.3f, 0f);
                stage.SetPhone("dim", 0f, 0.35f);
                Shot(dir, tag, "5-goodnight", stage, rig, rt);
                // 6. The title while the report plays (every piece in, the tablet's red and blue on the table).
                stage.SetLights(1f, 0f);
                stage.MoveCamera("table", 0f);
                if (stage.table != null)
                    foreach (var p in stage.table.pieces) p.FadeTo(1f, 0f);
                stage.SetInteractive(false);
                Shot(dir, tag, "6-title-report", stage, rig, rt, 0.27f);
                // 7. The title at rest: interactive, the play button up.
                stage.ShowTitleFinal();
                Shot(dir, tag, "7-title-rest", stage, rig, rt);
            }
            finally
            {
                if (rig != null)
                {
                    rig.Release();
                    foreach (var o in rig.owned) UnityEngine.Object.DestroyImmediate(o);
                    rig.owned.Clear();
                }
                stage.DisposeOwned();
                UnityEngine.Object.DestroyImmediate(stage.gameObject);
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
            }
        }

        static readonly MethodInfo StageLateUpdate = typeof(VhStage).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);

        static void Shot(string dir, string tag, string name, VhStage stage, VhLightingRig rig, RenderTexture rt, float rigTime = 0.016f)
        {
            // Settle: the stage's per-frame work (camera, lights, ambient, the arm), then the lighting rig.
            for (int i = 0; i < 3; i++)
            {
                StageLateUpdate?.Invoke(stage, null);
                if (rig != null) rig.Tick(i == 2 ? rigTime : 0.016f);
            }
            var cam = stage.cam;
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, request)) RenderPipeline.SubmitRenderRequest(cam, request);
            else
            {
                cam.targetTexture = rt;
                cam.Render();
                cam.targetTexture = null;
            }
            if (name == null) return;
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            string path = Path.Combine(dir, tag + "-" + name + ".png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            Debug.Log("JAMKIT_VH_CAPTURE " + path);
        }
    }
}
