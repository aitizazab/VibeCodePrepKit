using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using JamKit.Skyline;
using UnityEngine;

namespace JamKit.SkylineRepo
{
    /// <summary>SkylinePass stage D: run the built player with <c>-skyline-perf &lt;dir&gt;</c>; after a 3 s warm-up it
    /// records 45 s of frame times in the first scene (the arena: about 45 s covers its
    /// demo, docs/SKYLINE-PLAN-M1.md amendment 9), writes perf.json and quits.
    /// <para>A frame belongs to the window only if it started after the warm-up: the frame that straddles the warm-up's end
    /// is the warm-up's (the scene's first frames build the graphics driver's pipeline states, and one of them can run for
    /// seconds and end just past 3 s). The warm-up's own slowest frame is still reported (<c>warmupWorstMs</c>), so a load
    /// stall stays visible; <c>measuredFromS</c> says where the window started.</para>
    /// <para>So the next pass names its own hitches, perf.json also lists the window's 10 slowest frames (<c>worst</c>,
    /// slowest first): each with its time (<c>ms</c>), when it ended (<c>at</c>, seconds since the probe started), how many
    /// garbage collections ran in it (<c>gcs</c>), and the last <c>SKYLINE_</c> log line before it ended (<c>after</c>: what
    /// the game was doing). Each is also logged as <c>SKYLINE_PERF worst &lt;n&gt; ...</c>. The earlier fields are
    /// unchanged.</para></summary>
    public sealed class SkylinePerfProbe : MonoBehaviour
    {
        const float Warmup = 3f, Measure = 45f;
        const int WorstCount = 10;

        struct Hitch
        {
            public float ms, at;
            public int gcs;
            public string after;
        }

        readonly List<float> _frames = new List<float>(16384);
        readonly List<Hitch> _worst = new List<Hitch>(WorstCount + 1);
        string _outDir;
        float _start, _lastT = -1f, _warmupWorst, _measuredFrom = -1f;
        int _lastGc;
        string _lastLine = "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            string dir = SkylineRunArgs.Value("-skyline-perf");
            if (dir == null) return;
            try { File.Delete(Path.Combine(dir, "perf.json")); } catch (Exception) { /* a stale file must not pass for this run */ }
            var go = new GameObject("SkylinePerfProbe");
            DontDestroyOnLoad(go);
            var probe = go.AddComponent<SkylinePerfProbe>();
            probe._outDir = dir;
            probe._start = Time.realtimeSinceStartup;
            probe._lastGc = GC.CollectionCount(0);
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            Application.runInBackground = true;      // this run only (the player's own setting, never the shared PlayerSettings asset): a window that loses focus must keep presenting
        }

        void OnEnable() => Application.logMessageReceived += OnLog;

        void OnDisable() => Application.logMessageReceived -= OnLog;

        void OnLog(string message, string stackTrace, LogType type)
        {
            if (message != null && message.StartsWith("SKYLINE_", StringComparison.Ordinal) && !message.StartsWith("SKYLINE_PERF", StringComparison.Ordinal))
                _lastLine = message;
        }

        static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
        static string Esc(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", " ");

        void Update()
        {
            float t = Time.realtimeSinceStartup - _start;
            float dt = Time.unscaledDeltaTime;
            int gc = GC.CollectionCount(0);
            int gcs = gc - _lastGc;
            _lastGc = gc;
            bool startedAfterWarmup = _lastT >= Warmup;
            _lastT = t;
            if (!startedAfterWarmup)
            {
                if (dt > _warmupWorst) _warmupWorst = dt;
                return;
            }
            if (_measuredFrom < 0f) _measuredFrom = t - dt;
            _frames.Add(dt);
            Keep(new Hitch { ms = dt * 1000f, at = t, gcs = gcs, after = _lastLine });
            if (t < _measuredFrom + Measure) return;
            Finish();
        }

        /// <summary>Keeps the slowest <see cref="WorstCount"/> frames, slowest first.</summary>
        void Keep(Hitch h)
        {
            if (_worst.Count == WorstCount && h.ms <= _worst[WorstCount - 1].ms) return;
            int i = 0;
            while (i < _worst.Count && _worst[i].ms >= h.ms) i++;
            _worst.Insert(i, h);
            if (_worst.Count > WorstCount) _worst.RemoveAt(WorstCount);
        }

        void Finish()
        {
            var s = FrameStats.Summarize(_frames);
            var worst = new StringBuilder();
            for (int i = 0; i < _worst.Count; i++)
            {
                var h = _worst[i];
                if (i > 0) worst.Append(',');
                worst.Append("{\"ms\":").Append(F(h.ms)).Append(",\"at\":").Append(F(h.at)).Append(",\"gcs\":").Append(h.gcs)
                    .Append(",\"after\":\"").Append(Esc(h.after)).Append("\"}");
                Debug.Log("SKYLINE_PERF worst " + (i + 1) + " " + F(h.ms) + " ms at " + F(h.at) + " s, gcs " + h.gcs + ", after: " + h.after);
            }
            Debug.Log("SKYLINE_PERF warm-up worst " + F(_warmupWorst * 1000f) + " ms; window from " + F(_measuredFrom) + " s");
            int exitCode = 0;
            try
            {
                Directory.CreateDirectory(_outDir);
                File.WriteAllText(Path.Combine(_outDir, "perf.json"),
                    "{\"frames\":" + s.frames + ",\"avgMs\":" + F(s.avgMs) + ",\"onePercentLowMs\":" + F(s.onePercentLowMs) +
                    ",\"worstMs\":" + F(s.worstMs) + ",\"resolution\":\"" + Screen.width + "x" + Screen.height + "\",\"gpu\":\"" + Esc(SystemInfo.graphicsDeviceName) +
                    "\",\"scene\":\"" + Esc(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name) + "\"" +
                    ",\"warmupWorstMs\":" + F(_warmupWorst * 1000f) + ",\"measuredFromS\":" + F(_measuredFrom) +
                    ",\"worst\":[" + worst + "]}");
            }
            catch (Exception e)
            {
                Debug.LogError("SKYLINE_PERF could not write perf.json: " + e.Message);
                exitCode = 1;
            }
            enabled = false;
            Application.Quit(exitCode);
        }
    }
}
