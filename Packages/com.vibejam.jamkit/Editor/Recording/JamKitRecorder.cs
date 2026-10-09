#if JAMKIT_RECORDER
using System;
using System.Globalization;
using System.IO;
using JamKit.Shell;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEngine;

namespace JamKit.EditorTools.Recording
{
    /// <summary>
    /// Footage from the editor with Unity Recorder (it only works in the editor's Play mode, never in
    /// builds; record a build with OBS: tools/obs-notes.md). Menu <b>JamKit &gt; Record</b>:
    /// <list type="bullet">
    /// <item>Gameplay clip 1080p60 MP4: enters Play mode if needed and records the Game view until
    /// Stop recording (or leaving Play mode). Variable frame rate, so a human playing feels real time.</item>
    /// <item>Trailer frames 1440p PNG sequence: lossless frames at a constant 60 fps (the editor may run
    /// slower; the frames come out smooth). Assemble with ffmpeg (docs/PRESENTING.md).</item>
    /// <item>Attract mode clip: Play mode, attract mode, 60 s at a constant 60 fps, then stops.</item>
    /// </list>
    /// Everything goes to Recordings/ (git-ignored). The batch entry point
    /// <see cref="RecordAttractFromCommandLine"/> records attract mode headlessly from a script
    /// (tools/record-attract.ps1); it needs a graphics device, so never with -batchmode or -nographics.
    /// </summary>
    [InitializeOnLoad]
    public static class JamKitRecorder
    {
        const string Menu = "JamKit/Record/";
        // SessionState survives the domain reload of entering and leaving Play mode.
        const string PendingKey = "JamKit.Record.Pending";
        const string AttractKey = "JamKit.Record.Attract";
        const string SecondsKey = "JamKit.Record.Seconds";
        const string OutKey = "JamKit.Record.Out";
        const string ExitKey = "JamKit.Record.Exit";
        const string ResultKey = "JamKit.Record.Result";
        const string FileKey = "JamKit.Record.File";

        public enum Preset { Clip = 1, Frames = 2, AttractClip = 3 }

        static RecorderController _controller;
        static double _startAt, _deadline;
        static int _startFrame, _stopAfterFrames;
        static string _file = "";

        static JamKitRecorder()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorApplication.update += Tick;
        }

        /// <summary>The project's Recordings folder (git-ignored).</summary>
        public static string RecordingsDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Recordings"));

        public static bool IsRecording => _controller != null && _controller.IsRecording();

        [MenuItem(Menu + "Gameplay clip 1080p60 MP4", false, 1)]
        static void ClipMenu() => Begin(Preset.Clip, 0f, null, false);

        [MenuItem(Menu + "Trailer frames 1440p PNG sequence", false, 2)]
        static void FramesMenu() => Begin(Preset.Frames, 0f, null, false);

        [MenuItem(Menu + "Attract mode clip (60 s MP4)", false, 3)]
        static void AttractMenu() => Begin(Preset.AttractClip, 60f, null, false);

        [MenuItem(Menu + "Stop recording", false, 20)]
        static void StopMenu() => Finish(true);

        [MenuItem(Menu + "Stop recording", true)]
        static bool CanStop() => _controller != null;

        [MenuItem(Menu + "Open Recordings folder", false, 40)]
        static void OpenFolder()
        {
            Directory.CreateDirectory(RecordingsDir);
            EditorUtility.RevealInFinder(RecordingsDir);
        }

        /// <summary>
        /// Batch: <c>Unity.exe -projectPath . -executeMethod JamKit.EditorTools.Recording.JamKitRecorder.RecordAttractFromCommandLine
        /// -jamRecordSeconds 60 [-jamRecordOut Recordings/broll.mp4] -logFile out/record-attract.log</c> (no -batchmode,
        /// no -nographics: the Recorder needs the Game view). Enters Play mode, starts attract mode, records an
        /// MP4 at 1080p and a constant 60 fps for that many seconds of video, then quits: exit code 0 with
        /// JAMKIT_RECORD_OK and the file path in the log, 1 with JAMKIT_RECORD_FAIL.
        /// </summary>
        public static void RecordAttractFromCommandLine()
        {
            float seconds = 60f;
            string output = null;
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-jamRecordSeconds" && float.TryParse(args[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out float s) && s > 0f) seconds = s;
                if (args[i] == "-jamRecordOut") output = args[i + 1];
            }
            if (Application.isBatchMode)
            {
                Debug.LogError("JAMKIT_RECORD_FAIL the Recorder can't capture in -batchmode (no graphics pipeline). Launch the editor without -batchmode and -nographics (tools/record-attract.ps1).");
                EditorApplication.Exit(1);
                return;
            }
            Debug.Log("JAMKIT_RECORD batch: attract mode, " + seconds.ToString("0", CultureInfo.InvariantCulture) + " s");
            Begin(Preset.AttractClip, seconds, output, true);
        }

        static void Begin(Preset preset, float seconds, string output, bool exitWhenDone)
        {
            if (_controller != null) Finish(false);
            SessionState.SetInt(PendingKey, (int)preset);
            SessionState.SetFloat(SecondsKey, seconds);
            SessionState.SetString(OutKey, output ?? "");
            SessionState.SetBool(ExitKey, exitWhenDone);
            SessionState.SetBool(AttractKey, preset == Preset.AttractClip);
            SessionState.SetInt(ResultKey, 0);
            if (EditorApplication.isPlaying) _startAt = EditorApplication.timeSinceStartup + 0.5;
            else EditorApplication.EnterPlaymode();
            if (exitWhenDone) _deadline = EditorApplication.timeSinceStartup + seconds * 4f + 240f;
        }

        static void OnPlayMode(PlayModeStateChange change)
        {
            switch (change)
            {
                case PlayModeStateChange.EnteredPlayMode:
                    if (SessionState.GetInt(PendingKey, 0) != 0) _startAt = EditorApplication.timeSinceStartup + 1.0;
                    if (SessionState.GetBool(ExitKey, false) && _deadline <= 0) _deadline = EditorApplication.timeSinceStartup + SessionState.GetFloat(SecondsKey, 60f) * 4f + 240f;
                    break;
                case PlayModeStateChange.ExitingPlayMode:
                    if (_controller != null) Finish(false);
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    if (SessionState.GetBool(ExitKey, false))
                    {
                        SessionState.SetBool(ExitKey, false);
                        string file = SessionState.GetString(FileKey, "");
                        bool ok = SessionState.GetInt(ResultKey, 0) == 1 && Written(file);
                        if (ok) Debug.Log("JAMKIT_RECORD_OK " + file);
                        else Debug.LogError("JAMKIT_RECORD_FAIL no recording was written (" + file + ")");
                        EditorApplication.Exit(ok ? 0 : 1);
                    }
                    break;
            }
        }

        static void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            if (_startAt > 0 && now >= _startAt && EditorApplication.isPlaying && GameRoot.Exists && !GameRoot.IsLoadingScene)
            {
                _startAt = 0;
                StartPending();
            }
            if (_controller != null && _stopAfterFrames > 0 && Time.frameCount - _startFrame >= _stopAfterFrames) Finish(true);
            if (_deadline > 0 && now > _deadline)
            {
                _deadline = 0;
                Debug.LogError("JAMKIT_RECORD_FAIL timed out");
                if (_controller != null) Finish(false);
                SessionState.SetInt(ResultKey, 0);
                if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
                else EditorApplication.Exit(1);
            }
        }

        static void StartPending()
        {
            var preset = (Preset)SessionState.GetInt(PendingKey, 0);
            SessionState.EraseInt(PendingKey);
            if (preset == 0) return;
            float seconds = SessionState.GetFloat(SecondsKey, 0f);
            string output = SessionState.GetString(OutKey, "");
            if (SessionState.GetBool(AttractKey, false)) PresenterMode.Verbose = true; // the log shows the steps it filmed
            if (SessionState.GetBool(AttractKey, false) && !PresenterMode.StartAttract())
            {
                Debug.LogError("JAMKIT_RECORD_FAIL attract mode didn't start (does the active template have an attract script?)");
                SessionState.SetInt(ResultKey, 0);
                if (SessionState.GetBool(ExitKey, false)) EditorApplication.isPlaying = false;
                return;
            }
            try
            {
                _controller = Make(preset, output, out _file);
                _controller.PrepareRecording();
                _controller.StartRecording();
            }
            catch (Exception e)
            {
                Debug.LogError("JAMKIT_RECORD_FAIL the Recorder didn't start: " + e.Message);
                _controller = null;
                if (SessionState.GetBool(ExitKey, false)) EditorApplication.isPlaying = false;
                return;
            }
            SessionState.SetInt(ResultKey, 1);
            _startFrame = Time.frameCount;
            // Constant 60 fps: one frame is 1/60 s of video, so frames count the video's length exactly.
            _stopAfterFrames = seconds > 0f ? Mathf.CeilToInt(seconds * 60f) : 0;
            SessionState.SetString(FileKey, _file);
            Debug.Log("JAMKIT_RECORD started " + preset + " -> " + _file + (seconds > 0f ? " for " + seconds.ToString("0", CultureInfo.InvariantCulture) + " s" : " (JamKit > Record > Stop recording)"));
        }

        static RecorderController Make(Preset preset, string output, out string file)
        {
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            Directory.CreateDirectory(RecordingsDir);
            var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            var controller = new RecorderController(settings);
            settings.SetRecordModeToManual();
            settings.FrameRate = 60f;
            settings.CapFrameRate = true;
            if (preset == Preset.Frames)
            {
                string folder = Path.Combine(RecordingsDir, "frames_" + stamp);
                Directory.CreateDirectory(folder);
                var frames = ScriptableObject.CreateInstance<ImageRecorderSettings>();
                frames.name = "JamKit trailer frames";
                frames.Enabled = true;
                frames.OutputFormat = ImageRecorderSettings.ImageRecorderOutputFormat.PNG;
                frames.CaptureAlpha = false;
                frames.imageInputSettings = new GameViewInputSettings { OutputWidth = 2560, OutputHeight = 1440 };
                frames.OutputFile = Path.Combine(folder, "frame_") + DefaultWildcard.Frame;
                settings.FrameRatePlayback = FrameRatePlayback.Constant;
                settings.AddRecorderSettings(frames);
                file = folder;
                return controller;
            }
            var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name = "JamKit gameplay clip";
            movie.Enabled = true;
            movie.EncoderSettings = new CoreEncoderSettings
            {
                EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High,
                Codec = CoreEncoderSettings.OutputCodec.MP4
            };
            movie.CaptureAlpha = false;
            movie.ImageInputSettings = new GameViewInputSettings { OutputWidth = 1920, OutputHeight = 1080 };
            movie.AudioInputSettings.PreserveAudio = true;
            string path = string.IsNullOrEmpty(output)
                ? Path.Combine(RecordingsDir, (preset == Preset.AttractClip ? "attract_" : "clip_") + stamp)
                : Path.GetFullPath(output);
            if (path.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)) path = path.Substring(0, path.Length - 4);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            movie.OutputFile = path; // the Recorder adds .mp4
            // A human playing: variable (real time); attract mode: constant 60 (smooth even if the editor is slow).
            settings.FrameRatePlayback = preset == Preset.Clip ? FrameRatePlayback.Variable : FrameRatePlayback.Constant;
            settings.AddRecorderSettings(movie);
            RecorderOptions.VerboseMode = false;
            file = path + ".mp4";
            return controller;
        }

        /// <summary>The MP4 exists and isn't empty, or the frames folder has frames.</summary>
        static bool Written(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            if (File.Exists(path)) return new FileInfo(path).Length > 0;
            return Directory.Exists(path) && Directory.GetFiles(path, "*.png").Length > 0;
        }

        static void Finish(bool leavePlayModeIfBatch)
        {
            if (_controller == null) return;
            try { _controller.StopRecording(); }
            catch (Exception e) { Debug.LogError("JAMKIT_RECORD_FAIL stopping: " + e.Message); }
            _controller = null;
            _stopAfterFrames = 0;
            Debug.Log("JAMKIT_RECORD stopped -> " + _file);
            if (PresenterMode.AttractRunning) PresenterMode.StopAttract();
            if (leavePlayModeIfBatch && SessionState.GetBool(ExitKey, false) && EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        }
    }
}
#endif
