using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace JamKit.Look
{
    /// <summary>
    /// The <c>-perf-probe</c> player flag: flies the main camera along the scene's <see cref="VhPerfPath"/> (or round a slow
    /// circle if there is none), records every frame's time after a warm-up, then logs the percentiles and writes them to a
    /// text file, and quits. vSync is off and the frame rate uncapped while it runs, so the numbers are what the GPU can do.
    /// <code>
    /// VisitingHours.exe -perf-probe [-perf-seconds 40] [-perf-warmup 4] [-perf-out out/perf/lookLab.txt] [-perf-stay]
    ///     -screen-width 1920 -screen-height 1080 -screen-fullscreen 1 -logFile out/perf/player.log
    /// </code>
    /// Log lines start with <c>JAMKIT_PERF</c>: frames, mean, p50, p90, p95, p99, p99.9 and worst frame time (ms), the share of
    /// frames over 16.7 ms and over 25 ms (the redesign's blocker), the GPU, the resolution. Also runs in the editor's play mode
    /// (the numbers are then the editor's, not a build's). No per-frame allocation while recording.
    /// </summary>
    public sealed class VhPerfProbe : MonoBehaviour
    {
        public float seconds = 40f;
        public float warmup = 4f;
        public string outPath;
        public bool stay;

        float[] _frames;
        int _count;
        float _t;
        Camera _cam;
        VhPerfPath _path;
        Vector3 _orbitCenter;
        bool _done;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!HasArg("-perf-probe")) return;
            var go = new GameObject("VH Perf Probe");
            DontDestroyOnLoad(go);
            var p = go.AddComponent<VhPerfProbe>();
            if (float.TryParse(Arg("-perf-seconds"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var s)) p.seconds = s;
            if (float.TryParse(Arg("-perf-warmup"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var w)) p.warmup = w;
            p.outPath = Arg("-perf-out");
            p.stay = HasArg("-perf-stay");
        }

        void Start()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            _frames = new float[Mathf.CeilToInt(Mathf.Max(1f, seconds) * 600f)];
            _path = FindAnyObjectByType<VhPerfPath>();
            _cam = Camera.main;
            if (_cam != null) _orbitCenter = _cam.transform.position;
            if (_path != null && _cam != null) _cam.fieldOfView = _path.fieldOfView;
            Debug.Log("JAMKIT_PERF start: " + (_path != null ? _path.eyes.Length + " waypoints" : "orbit (no VhPerfPath)") +
                      ", " + seconds + " s after a " + warmup + " s warm-up, " + Screen.width + "x" + Screen.height + ", " +
                      SystemInfo.graphicsDeviceName + ", quality " + QualitySettings.names[QualitySettings.GetQualityLevel()]);
        }

        void LateUpdate()
        {
            if (_done) return;
            float dt = Time.unscaledDeltaTime;
            _t += dt;
            if (_cam == null) _cam = Camera.main;
            if (_cam != null) Fly(_t);
            if (_t < warmup) return;
            if (_count < _frames.Length) _frames[_count++] = dt * 1000f;
            if (_t >= warmup + seconds || _count >= _frames.Length) Finish();
        }

        void Fly(float t)
        {
            var tr = _cam.transform;
            if (_path != null && _path.eyes != null && _path.eyes.Length >= 2 && _path.looks != null && _path.looks.Length == _path.eyes.Length)
            {
                int n = _path.eyes.Length;
                float u = t / Mathf.Max(0.1f, _path.secondsPerLeg);
                int i = Mathf.FloorToInt(u) % n;
                int j = (i + 1) % n;
                float k = Mathf.SmoothStep(0f, 1f, u - Mathf.Floor(u));
                var eye = Vector3.Lerp(_path.eyes[i], _path.eyes[j], k);
                var look = Vector3.Lerp(_path.looks[i], _path.looks[j], k);
                tr.SetPositionAndRotation(eye, Quaternion.LookRotation((look - eye).sqrMagnitude > 1e-4f ? look - eye : Vector3.forward, Vector3.up));
                return;
            }
            float a = t * 0.15f;
            tr.SetPositionAndRotation(_orbitCenter, Quaternion.Euler(0f, a * Mathf.Rad2Deg, 0f));
        }

        void Finish()
        {
            _done = true;
            var sorted = new float[_count];
            Array.Copy(_frames, sorted, _count);
            Array.Sort(sorted);
            double sum = 0;
            int over16 = 0, over25 = 0;
            for (int i = 0; i < _count; i++)
            {
                sum += sorted[i];
                if (sorted[i] > 16.7f) over16++;
                if (sorted[i] > 25f) over25++;
            }
            float P(float q) => _count == 0 ? 0f : sorted[Mathf.Clamp(Mathf.CeilToInt(q * _count) - 1, 0, _count - 1)];
            var sb = new StringBuilder();
            sb.AppendLine("JAMKIT_PERF scene " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            sb.AppendLine("JAMKIT_PERF gpu " + SystemInfo.graphicsDeviceName + " | " + Screen.width + "x" + Screen.height + " | quality " +
                          QualitySettings.names[QualitySettings.GetQualityLevel()] + " | " + (Application.isEditor ? "editor" : "player"));
            sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "JAMKIT_PERF frames {0} | mean {1:0.00} ms ({2:0} fps) | p50 {3:0.00} | p90 {4:0.00} | p95 {5:0.00} | p99 {6:0.00} | p99.9 {7:0.00} | worst {8:0.00} ms",
                _count, _count > 0 ? sum / _count : 0, _count > 0 ? 1000.0 / (sum / _count) : 0, P(0.5f), P(0.9f), P(0.95f), P(0.99f), P(0.999f),
                _count > 0 ? sorted[_count - 1] : 0f));
            sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "JAMKIT_PERF over 16.7 ms: {0:0.0}% | over 25 ms: {1:0.0}% ({2} frames){3}",
                _count > 0 ? 100.0 * over16 / _count : 0, _count > 0 ? 100.0 * over25 / _count : 0, over25,
                P(0.99f) > 25f ? " | BLOCKER: p99 over 25 ms" : ""));
            string report = sb.ToString();
            Debug.Log(report);
            try
            {
                string path = !string.IsNullOrEmpty(outPath) ? outPath
                    : Path.Combine(Application.isEditor ? Directory.GetCurrentDirectory() : Path.GetDirectoryName(Application.dataPath) ?? ".",
                        "out", "perf", "perf-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt");
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
                File.WriteAllText(path, report);
                Debug.Log("JAMKIT_PERF written " + Path.GetFullPath(path));
            }
            catch (Exception e) { Debug.LogWarning("JAMKIT_PERF could not write the report: " + e.Message); }
            if (stay) return;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit(0);
#endif
        }

        static bool HasArg(string name)
        {
            foreach (var a in Environment.GetCommandLineArgs()) if (a == name) return true;
            return false;
        }

        static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++) if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
