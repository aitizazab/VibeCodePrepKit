#if JAMKIT_RECORDER
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace JamKit.SkylineRepo.EditorTools.Recording
{
    /// <summary>SkylinePass stage B (docs/SKYLINE-DESIGN.md 9.4): a graphics editor session (not batch mode, so audio
    /// plays) that records each pipeline scene to MP4 with audio and takes a still, then exits.
    /// <c>Unity.exe -projectPath . -executeMethod JamKit.SkylineRepo.EditorTools.Recording.SkylineCapture.RunFromCommandLine -skylineOut &lt;dir&gt;</c>
    /// Entering and leaving Play mode reload the domain, so the whole state machine lives in SessionState and
    /// <see cref="Tick"/> (re-registered by the [InitializeOnLoad] constructor after each reload) drives it:
    /// Starting (waiting for Play mode and a short settle) -> Recording (counted in frames: the Recorder plays back at a
    /// constant 60 fps, so frames, not seconds, give a deterministic clip length) -> Exiting (waiting for edit mode)
    /// -> the next scene, or Finalizing (waiting for the files) -> capture.json.</summary>
    [InitializeOnLoad]
    public static class SkylineCapture
    {
        const string KeyIndex = "SkylineCapture.Index", KeyOut = "SkylineCapture.Out", KeyActive = "SkylineCapture.Active",
                     KeyResults = "SkylineCapture.Results", KeyStarted = "SkylineCapture.Started", KeyPhase = "SkylineCapture.Phase",
                     KeyRunInBackground = "SkylineCapture.RunInBackground";
        const int PhaseStarting = 0, PhaseRecording = 1, PhaseExiting = 2, PhaseFinalizing = 3;
        // Each scene records SkylineScenes.RecordSeconds of video at a constant 60 fps; the still at 2 s. The deadline sits
        // well under stage B's 900 s timeout (which also counts the editor's boot), so capture.json is always written.
        const int StillFrame = 2 * 60;
        const float Settle = 1f, Deadline = 720f, FileWait = 10f;
        static readonly string[] Scenes = SkylineScenes.All;

        static RecorderController _controller;
        static double _settleStart = -1, _finalizeStart = -1;
        static int _startFrame;
        static bool _stillTaken;

        static SkylineCapture()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        public static void RunFromCommandLine()
        {
            string outDir = Path.GetFullPath(SkylinePassBatch.Arg("-skylineOut") ?? Path.Combine("out", "skyline", "manual"));
            try
            {
                Directory.CreateDirectory(outDir);
                foreach (string scene in Scenes)                   // stale files must never pass the final check
                {
                    string name = Path.GetFileNameWithoutExtension(scene);
                    foreach (string f in new[] { RecPath(outDir, name), StillPath(outDir, name) }) if (File.Exists(f)) File.Delete(f);
                    foreach (string f in Directory.GetFiles(outDir, "still-" + name + "-*.png")) File.Delete(f);
                }
                string old = Path.Combine(outDir, "capture.json");
                if (File.Exists(old)) File.Delete(old);
            }
            catch (Exception) { /* Finish reports it */ }
            SessionState.SetString(KeyOut, outDir);
            SessionState.SetString(KeyResults, "");
            SessionState.SetInt(KeyIndex, 0);
            SessionState.SetInt(KeyPhase, PhaseStarting);
            SessionState.SetFloat(KeyStarted, (float)EditorApplication.timeSinceStartup);
            SessionState.SetBool(KeyActive, true);
            SessionState.SetBool(KeyRunInBackground, PlayerSettings.runInBackground);   // restored by Finish
            if (Application.isBatchMode)
            {
                Finish(false, "batch mode mutes audio and cannot record: launch the editor without -batchmode and -nographics");
                return;
            }
            try
            {
                SkylineScenes.BuildAll();
                OpenAndPlay(0);
            }
            catch (Exception e)
            {
                Finish(false, "setup: " + e.Message);
            }
        }

        static void OpenAndPlay(int index)
        {
            SessionState.SetInt(KeyPhase, PhaseStarting);
            _settleStart = -1;
            EditorSceneManager.OpenScene(Scenes[index], OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        static string RecPath(string dir, string name) => Path.Combine(dir, "rec-" + name + ".mp4");
        static string StillPath(string dir, string name) => Path.Combine(dir, "still-" + name + ".png");

        static void Tick()
        {
            if (!SessionState.GetBool(KeyActive, false)) return;
            try
            {
                double now = EditorApplication.timeSinceStartup;
                if (now - SessionState.GetFloat(KeyStarted, (float)now) > Deadline) { Finish(false, "deadline"); return; }
                switch (SessionState.GetInt(KeyPhase, PhaseStarting))
                {
                    case PhaseStarting: TickStarting(now); break;
                    case PhaseRecording: TickRecording(); break;
                    case PhaseExiting: TickExiting(); break;
                    case PhaseFinalizing: TickFinalizing(now); break;
                }
            }
            catch (Exception e)
            {
                Finish(false, "tick: " + e.Message);
            }
        }

        static void TickStarting(double now)
        {
            if (!EditorApplication.isPlaying || EditorApplication.isPaused) return;
            // Play mode only: keep presenting if the window loses focus (the recording must not stall). Finish puts the shared
            // PlayerSettings value back in case the editor mirrors this into the project asset.
            Application.runInBackground = true;
            if (_settleStart < 0) _settleStart = now;            // first tick in Play mode (statics reset by the reload)
            if (now - _settleStart < Settle) return;
            StartRecording();
        }

        static void StartRecording()
        {
            int index = SessionState.GetInt(KeyIndex, 0);
            string name = Path.GetFileNameWithoutExtension(Scenes[index]);
            string dir = SessionState.GetString(KeyOut, "");
            try
            {
                var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
                var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
                movie.name = "skyline-" + name;
                movie.Enabled = true;
                movie.EncoderSettings = new CoreEncoderSettings
                {
                    // "High" came out at about 6.6 Mbit/s and broke into blocks in fast motion (judges B and E, round 2): the
                    // judges and the creator read the game from these recordings, so they get a high fixed bitrate.
                    EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.Custom,
                    TargetBitRate = 24f,
                    GopSize = 60,
                    NumConsecutiveBFrames = 2,
                    Codec = CoreEncoderSettings.OutputCodec.MP4,
                };
                movie.ImageInputSettings = new GameViewInputSettings { OutputWidth = 1920, OutputHeight = 1080 };
                movie.AudioInputSettings.PreserveAudio = true;
                movie.OutputFile = Path.Combine(dir, "rec-" + name); // the Recorder adds .mp4
                settings.AddRecorderSettings(movie);
                settings.SetRecordModeToManual();
                settings.FrameRate = 60f;
                settings.FrameRatePlayback = FrameRatePlayback.Constant;
                settings.CapFrameRate = true;
                var controller = new RecorderController(settings);
                controller.PrepareRecording();
                if (!controller.StartRecording()) throw new InvalidOperationException("StartRecording returned false");
                _controller = controller;
                // When the Recorder took over, against the scene's own clocks: the directors wait for this capture step, so
                // a late start shows here (Task 17: the first pass's range director gave up before the Recorder started).
                Debug.Log("SKYLINE_CAPTURE recording " + name + " frame=" + Time.frameCount +
                          " realtime=" + Time.realtimeSinceStartup.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) +
                          " step=" + Time.captureDeltaTime.ToString("0.#####", System.Globalization.CultureInfo.InvariantCulture));
            }
            catch (Exception e)
            {
                _controller = null;
                Finish(false, "the Recorder did not start for " + name + ": " + e.Message);
                return;
            }
            _startFrame = Time.frameCount;
            _stillTaken = false;
            SessionState.SetInt(KeyPhase, PhaseRecording);
        }

        static void TickRecording()
        {
            if (_controller == null)                              // a domain reload mid-recording lost the recorder
            {
                Finish(false, "the recorder was lost during recording (domain reload)");
                return;
            }
            if (!EditorApplication.isPlaying) { Finish(false, "left Play mode during recording"); return; }
            if (EditorApplication.isPaused) return;
            int index = SessionState.GetInt(KeyIndex, 0);
            string name = Path.GetFileNameWithoutExtension(Scenes[index]);
            string dir = SessionState.GetString(KeyOut, "");
            int frames = Time.frameCount - _startFrame;
            if (!_stillTaken && frames >= StillFrame)
            {
                ScreenCapture.CaptureScreenshot(StillPath(dir, name));
                _stillTaken = true;
            }
            if (frames < SkylineScenes.RecordSeconds(Scenes[index]) * 60) return;
            var controller = _controller;
            _controller = null;
            try { controller.StopRecording(); }
            catch (Exception e) { Finish(false, "stopping the Recorder for " + name + ": " + e.Message); return; }
            try { TakeStills(dir, name); }
            catch (Exception e) { Finish(false, "stills for " + name + ": " + e.Message); return; }
            SessionState.SetString(KeyResults, SessionState.GetString(KeyResults, "") + name + ";");
            SessionState.SetInt(KeyIndex, index + 1);
            SessionState.SetInt(KeyPhase, PhaseExiting);          // before isPlaying = false, so no tick can start another recording
            EditorApplication.isPlaying = false;
        }

        /// <summary>Renders every SkylineStillCamera in the playing scene to still-&lt;scene&gt;-&lt;shot&gt;.png (1920x1080):
        /// through the pipeline's render request (URP's standalone path for a disabled Base camera), or Camera.Render where
        /// the pipeline doesn't take one. A still that comes out all black throws, which fails stage B.</summary>
        static void TakeStills(string dir, string scene)
        {
            int n = 0;
            foreach (var shot in UnityEngine.Object.FindObjectsByType<JamKit.SkylineRepo.SkylineStillCamera>(FindObjectsSortMode.None))
            {
                var cam = shot.Enforce();                         // disabled, Base
                if (cam == null) continue;
                // URP renders a camera with a target texture IN that texture's format (it replaces the HDR intermediate), so
                // an 8-bit target clamps the scene at 1.0 before the grade: no bloom, ACES tops out near 223. Render into a
                // half-float (linear) target like the player's HDR buffer, then convert to sRGB 8-bit for the PNG.
                var hdr = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
                var rt = new RenderTexture(1920, 1080, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
                var previousTarget = cam.targetTexture;
                var previousActive = RenderTexture.active;
                try
                {
                    shot.BeginShot();                             // the shot's display bot, a hidden shutter (put back below)
                    hdr.Create();
                    rt.Create();
                    var request = new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = hdr };
                    if (UnityEngine.Rendering.RenderPipeline.SupportsRenderRequest(cam, request))
                        UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(cam, request);
                    else
                    {
                        cam.targetTexture = hdr;
                        cam.Render();
                    }
                    Graphics.Blit(hdr, rt);                       // linear (already tonemapped by the post) -> sRGB
                    RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                    tex.Apply();
                    if (IsBlack(tex)) throw new InvalidOperationException("still '" + shot.shotName + "' rendered all black");
                    File.WriteAllBytes(Path.Combine(dir, "still-" + scene + "-" + shot.shotName + ".png"), tex.EncodeToPNG());
                    n++;
                }
                finally
                {
                    shot.EndShot();
                    cam.targetTexture = previousTarget;
                    RenderTexture.active = previousActive;
                    hdr.Release();
                    UnityEngine.Object.DestroyImmediate(hdr);
                    rt.Release();
                    UnityEngine.Object.DestroyImmediate(rt);
                    UnityEngine.Object.DestroyImmediate(tex);
                }
            }
            Debug.Log("SKYLINE_CAPTURE stills " + scene + " " + n);
        }

        /// <summary>True when no pixel rises above 2/255 in any channel (sampled on a grid).</summary>
        static bool IsBlack(Texture2D tex)
        {
            var px = tex.GetPixels32();
            for (int i = 0; i < px.Length; i += 7)
                if (px[i].r > 2 || px[i].g > 2 || px[i].b > 2) return false;
            return true;
        }

        static void TickExiting()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;   // still leaving Play mode
            int index = SessionState.GetInt(KeyIndex, 0);
            if (index >= Scenes.Length)
            {
                SessionState.SetInt(KeyPhase, PhaseFinalizing);
                _finalizeStart = -1;
                return;
            }
            OpenAndPlay(index);
        }

        /// <summary>Back in edit mode: wait (up to FileWait seconds) for every recording and still to be on disk.</summary>
        static void TickFinalizing(double now)
        {
            if (_finalizeStart < 0) _finalizeStart = now;
            string dir = SessionState.GetString(KeyOut, "");
            string missing = Missing(dir);
            if (missing.Length == 0) { Finish(true, null); return; }
            if (now - _finalizeStart > FileWait) Finish(false, "missing or empty output: " + missing);
        }

        static string Missing(string dir)
        {
            string missing = "";
            foreach (string scene in Scenes)
            {
                string name = Path.GetFileNameWithoutExtension(scene);
                string rec = RecPath(dir, name), still = StillPath(dir, name);
                if (!File.Exists(rec) || new FileInfo(rec).Length == 0) missing += Path.GetFileName(rec) + " ";
                if (!File.Exists(still)) missing += Path.GetFileName(still) + " ";
            }
            return missing.TrimEnd();
        }

        static void Finish(bool ok, string error)
        {
            if (_controller != null)                              // an error or deadline mid-recording: stop it first
            {
                try { _controller.StopRecording(); } catch (Exception) { /* exiting anyway */ }
                _controller = null;
            }
            string dir = SessionState.GetString(KeyOut, "");
            SessionState.SetBool(KeyActive, false);
            try
            {
                bool original = SessionState.GetBool(KeyRunInBackground, PlayerSettings.runInBackground);
                if (PlayerSettings.runInBackground != original) PlayerSettings.runInBackground = original;
            }
            catch (Exception) { /* the pass is ending anyway */ }
            try
            {
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                    File.WriteAllText(Path.Combine(dir, "capture.json"),
                        "{\"ok\":" + (ok ? "true" : "false") + ",\"recorded\":" + SkylinePassBatch.Quote(SessionState.GetString(KeyResults, "")) +
                        ",\"error\":" + (error == null ? "null" : SkylinePassBatch.Quote(error)) + "}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError("SKYLINE_CAPTURE could not write capture.json: " + e.Message);
            }
            Debug.Log("SKYLINE_CAPTURE " + (ok ? "OK" : "FAILED " + error));
            EditorApplication.Exit(ok ? 0 : 1);
        }
    }
}
#endif
