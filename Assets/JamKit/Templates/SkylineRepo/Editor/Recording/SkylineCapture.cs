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
    /// <c>Unity.exe -projectPath . -executeMethod JamKit.SkylineRepo.EditorTools.Recording.SkylineCapture.RunFromCommandLine -skylineOut &lt;dir&gt;</c></summary>
    [InitializeOnLoad]
    public static class SkylineCapture
    {
        const string KeyIndex = "SkylineCapture.Index", KeyOut = "SkylineCapture.Out", KeyActive = "SkylineCapture.Active",
                     KeyResults = "SkylineCapture.Results", KeyStarted = "SkylineCapture.Started";
        const float RecordSeconds = 8f, StillAt = 2f, Settle = 1f, Deadline = 300f;
        static readonly string[] Scenes = { SkylineScenes.FiringRange, SkylineScenes.Gallery };

        static RecorderController _controller;
        static double _phaseStart = -1;
        static bool _stillTaken;

        static SkylineCapture()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        public static void RunFromCommandLine()
        {
            string outDir = Path.GetFullPath(SkylinePassBatch.Arg("-skylineOut") ?? Path.Combine("out", "skyline", "manual"));
            Directory.CreateDirectory(outDir);
            SkylineScenes.BuildAll();
            SessionState.SetString(KeyOut, outDir);
            SessionState.SetInt(KeyIndex, 0);
            SessionState.SetString(KeyResults, "");
            SessionState.SetFloat(KeyStarted, (float)EditorApplication.timeSinceStartup);
            SessionState.SetBool(KeyActive, true);
            OpenAndPlay(0);
        }

        static void OpenAndPlay(int index)
        {
            EditorSceneManager.OpenScene(Scenes[index], OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        static void Tick()
        {
            if (!SessionState.GetBool(KeyActive, false)) return;
            double now = EditorApplication.timeSinceStartup;
            if (now - SessionState.GetFloat(KeyStarted, (float)now) > Deadline) { Finish(false, "deadline"); return; }
            if (!EditorApplication.isPlaying || EditorApplication.isPaused) return;
            if (_phaseStart < 0) _phaseStart = now;           // first tick in Play mode (statics reset by the reload)
            if (_controller == null)
            {
                if (now - _phaseStart < Settle) return;
                StartRecording();
                return;
            }
            double rec = now - _phaseStart;
            int index = SessionState.GetInt(KeyIndex, 0);
            string name = Path.GetFileNameWithoutExtension(Scenes[index]);
            string dir = SessionState.GetString(KeyOut, "");
            if (!_stillTaken && rec >= StillAt)
            {
                ScreenCapture.CaptureScreenshot(Path.Combine(dir, "still-" + name + ".png"));
                _stillTaken = true;
            }
            if (rec < RecordSeconds) return;
            _controller.StopRecording();
            _controller = null;
            _phaseStart = -1;
            SessionState.SetString(KeyResults, SessionState.GetString(KeyResults, "") + name + ";");
            index++;
            SessionState.SetInt(KeyIndex, index);
            EditorApplication.isPlaying = false;
            if (index >= Scenes.Length) EditorApplication.delayCall += () => Finish(true, null);
            else EditorApplication.delayCall += () => OpenAndPlay(index);
        }

        static void StartRecording()
        {
            int index = SessionState.GetInt(KeyIndex, 0);
            string name = Path.GetFileNameWithoutExtension(Scenes[index]);
            string dir = SessionState.GetString(KeyOut, "");
            var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name = "skyline-" + name;
            movie.Enabled = true;
            movie.EncoderSettings = new CoreEncoderSettings
            {
                EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High,
                Codec = CoreEncoderSettings.OutputCodec.MP4,
            };
            movie.ImageInputSettings = new GameViewInputSettings { OutputWidth = 1920, OutputHeight = 1080 };
            movie.AudioInputSettings.PreserveAudio = true;
            movie.OutputFile = Path.Combine(dir, "rec-" + name); // the Recorder adds .mp4
            settings.AddRecorderSettings(movie);
            settings.SetRecordModeToManual();
            settings.FrameRate = 60f;
            settings.CapFrameRate = true;
            _controller = new RecorderController(settings);
            _controller.PrepareRecording();
            _controller.StartRecording();
            _phaseStart = EditorApplication.timeSinceStartup;
            _stillTaken = false;
        }

        static void Finish(bool ok, string error)
        {
            string dir = SessionState.GetString(KeyOut, "");
            SessionState.SetBool(KeyActive, false);
            if (!string.IsNullOrEmpty(dir))
                File.WriteAllText(Path.Combine(dir, "capture.json"),
                    "{\"ok\":" + (ok ? "true" : "false") + ",\"recorded\":" + SkylinePassBatch.Quote(SessionState.GetString(KeyResults, "")) +
                    ",\"error\":" + (error == null ? "null" : SkylinePassBatch.Quote(error)) + "}");
            Debug.Log("SKYLINE_CAPTURE " + (ok ? "OK" : "FAILED " + error));
            EditorApplication.Exit(ok ? 0 : 1);
        }
    }
}
