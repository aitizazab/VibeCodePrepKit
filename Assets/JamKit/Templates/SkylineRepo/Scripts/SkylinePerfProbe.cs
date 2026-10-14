using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using JamKit.Skyline;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace JamKit.SkylineRepo
{
    /// <summary>SkylinePass stage D: run the built player with <c>-skyline-perf &lt;dir&gt;</c>; after a 3 s warm-up it
    /// records frame times in the first scene (the arena) until the scripted demo ends (<c>SKYLINE_DEMO done</c>, or
    /// <c>SKYLINE_ARENA exit reached</c> plus 3 s), at most <see cref="MaxMeasure"/> s (<c>-skyline-perf-seconds &lt;s&gt;</c>
    /// overrides the cap), writes perf.json and quits.
    /// <para>Judge round 2 (E 11): the old 45 s window ended inside the first fight, so the facade fight, the skylight fight
    /// and every late-fight moment with a floor full of wrecks went unmeasured. The window now runs the whole demo and
    /// perf.json reports each stretch of it on its own (<c>phases</c>): the warm-up hall, each fight from its
    /// <c>SKYLINE_ARENA encounter &lt;id&gt; start</c> to its <c>cleared</c>, the walks between, and the lift. Each phase has
    /// its frames, average, 1% low and worst; <c>worstFightOnePercentLowMs</c> is the worst fight's 1% low (the number the
    /// blocker is about), <c>fightsMeasured</c> how many fights ran to their end inside the window, and
    /// <c>demoComplete</c> whether the demo finished before the cap.</para>
    /// <para>A frame belongs to the window only if it started after the warm-up: the frame that straddles the warm-up's end
    /// is the warm-up's (the scene's first frames build the graphics driver's pipeline states, and one of them can run for
    /// seconds and end just past 3 s). The warm-up's own slowest frame is still reported (<c>warmupWorstMs</c>), so a load
    /// stall stays visible; <c>measuredFromS</c> says where the window started.</para>
    /// <para>perf.json also lists the window's 10 slowest frames (<c>worst</c>, slowest first): each with its time
    /// (<c>ms</c>), when it ended (<c>at</c>, seconds since the probe started), how many garbage collections ran in it
    /// (<c>gcs</c>), the last <c>SKYLINE_</c> log line before it ended (<c>after</c>), its phase, and how many bots were alive
    /// and wrecks lay in the scene (<c>bots</c>, <c>wrecks</c>). Each is also logged as <c>SKYLINE_PERF worst &lt;n&gt; ...</c>,
    /// and each phase as <c>SKYLINE_PERF phase ...</c>. The settings the frames were drawn with are recorded too
    /// (<c>quality</c>, <c>pipeline</c>, <c>renderScale</c>, <c>upscaling</c>): round 2's build turned out to be drawing on
    /// Visiting Hours' quality level (an STP request in the log), not the one the look was made on.</para>
    /// <para>The run also traces the graphics pipeline states the game creates (a <see cref="GraphicsStateCollection"/>) and
    /// saves them as <c>&lt;dir&gt;/SkylineArena.graphicsstate</c>: copied to <c>Assets/StreamingAssets/Skyline/</c> before the
    /// next build, <see cref="SkylineStartCover"/> warms them up behind the start cover, so the first frame doesn't build
    /// them one by one (the 3 s start freeze).</para></summary>
    public sealed class SkylinePerfProbe : MonoBehaviour
    {
        const float Warmup = 3f, MaxMeasure = 150f, AfterExit = 3f;
        const int WorstCount = 10;
        public const string TraceFile = "SkylineArena.graphicsstate";

        struct Hitch
        {
            public float ms, at;
            public int gcs, bots, wrecks;
            public string after, phase;
        }

        sealed class Phase
        {
            public string name;
            public bool fight, ended;
            public float from, to, worstMs;
            public string worstAfter = "";
            public readonly List<float> frames = new List<float>(4096);
        }

        readonly List<float> _frames = new List<float>(32768);
        readonly List<Hitch> _worst = new List<Hitch>(WorstCount + 1);
        readonly List<Phase> _phases = new List<Phase>();
        Phase _phase;
        string _outDir;
        float _start, _lastT = -1f, _warmupWorst, _measuredFrom = -1f, _cap = MaxMeasure, _endAt = -1f;
        int _lastGc, _maxWrecks, _maxBots;
        string _lastLine = "";
        bool _demoDone;
        GraphicsStateCollection _trace;

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
            string cap = SkylineRunArgs.Value("-skyline-perf-seconds");
            if (cap != null && float.TryParse(cap, NumberStyles.Float, CultureInfo.InvariantCulture, out float c) && c > 0f) probe._cap = c;
            probe.Enter("warm-up hall", false, 0f);
            probe._trace = _earlyTrace;
            _earlyTrace = null;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            Application.runInBackground = true;      // this run only (the player's own setting, never the shared PlayerSettings asset): a window that loses focus must keep presenting
        }

        static GraphicsStateCollection _earlyTrace;

        /// <summary>Starts the pipeline-state trace before the first scene wakes (so the start cover's warm-up and the first
        /// frame are inside it), seeded with the trace the build already carries, so the saved file holds both.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void StartTrace()
        {
            _earlyTrace = null;
            if (Application.isEditor || SkylineRunArgs.Value("-skyline-perf") == null) return;
            try
            {
                var trace = new GraphicsStateCollection();
                string shipped = Path.Combine(Application.streamingAssetsPath, SkylineStartCover.TraceFolder, TraceFile);
                if (File.Exists(shipped)) trace.LoadFromFile(shipped);
                if (trace.BeginTrace()) _earlyTrace = trace;
            }
            catch (Exception e)
            {
                Debug.Log("SKYLINE_PERF no pipeline-state trace: " + e.Message);
            }
        }

        void SaveTrace()
        {
            if (_trace == null) return;
            try
            {
                _trace.EndTrace();
                string path = Path.Combine(_outDir, TraceFile);
                bool ok = _trace.SaveToFile(path);
                Debug.Log("SKYLINE_PERF pipeline states traced: " + _trace.totalGraphicsStateCount + (ok ? ", saved " + path : ", NOT saved"));
            }
            catch (Exception e)
            {
                Debug.Log("SKYLINE_PERF pipeline-state trace not saved: " + e.Message);
            }
            _trace = null;
        }

        void OnEnable() => Application.logMessageReceived += OnLog;

        void OnDisable() => Application.logMessageReceived -= OnLog;

        const string EncounterTag = "SKYLINE_ARENA encounter ";

        void OnLog(string message, string stackTrace, LogType type)
        {
            if (message == null || !message.StartsWith("SKYLINE_", StringComparison.Ordinal) || message.StartsWith("SKYLINE_PERF", StringComparison.Ordinal)) return;
            _lastLine = message;
            float t = Time.realtimeSinceStartup - _start;
            if (message.StartsWith(EncounterTag, StringComparison.Ordinal))
            {
                string rest = message.Substring(EncounterTag.Length);
                if (rest.EndsWith(" start", StringComparison.Ordinal)) Enter("fight " + rest.Substring(0, rest.Length - 6), true, t);
                else if (rest.EndsWith(" cleared", StringComparison.Ordinal))
                {
                    if (_phase != null && _phase.fight) _phase.ended = true;
                    Enter("after " + rest.Substring(0, rest.Length - 8), false, t);
                }
            }
            else if (message.StartsWith("SKYLINE_ARENA exit reached", StringComparison.Ordinal))
            {
                Enter("lift", false, t);
                if (_endAt < 0f) _endAt = t + AfterExit;
            }
            else if (message.StartsWith("SKYLINE_ARENA wrecked restart", StringComparison.Ordinal)) Enter("restart", false, t);
            else if (message.StartsWith("SKYLINE_DEMO done", StringComparison.Ordinal))
            {
                _demoDone = true;
                if (_endAt < 0f || _endAt > t + 0.5f) _endAt = t + 0.5f;
            }
        }

        void Enter(string name, bool fight, float t)
        {
            if (_phase != null) _phase.to = t;
            _phase = new Phase { name = name, fight = fight, from = t, to = t };
            _phases.Add(_phase);
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
            int bots = SkylineBot.Live.Count, wrecks = SkylineWreck.All.Count;
            if (bots > _maxBots) _maxBots = bots;
            if (wrecks > _maxWrecks) _maxWrecks = wrecks;
            _frames.Add(dt);
            float ms = dt * 1000f;
            if (_phase != null)
            {
                _phase.frames.Add(dt);
                _phase.to = t;
                if (ms > _phase.worstMs) { _phase.worstMs = ms; _phase.worstAfter = _lastLine; }
            }
            Keep(new Hitch { ms = ms, at = t, gcs = gcs, after = _lastLine, phase = _phase != null ? _phase.name : "", bots = bots, wrecks = wrecks });
            if ((_endAt >= 0f && t >= _endAt) || t >= _measuredFrom + _cap) Finish();
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
            enabled = false;
            SaveTrace();
            var s = FrameStats.Summarize(_frames);
            var worst = new StringBuilder();
            for (int i = 0; i < _worst.Count; i++)
            {
                var h = _worst[i];
                if (i > 0) worst.Append(',');
                worst.Append("{\"ms\":").Append(F(h.ms)).Append(",\"at\":").Append(F(h.at)).Append(",\"gcs\":").Append(h.gcs)
                    .Append(",\"after\":\"").Append(Esc(h.after)).Append("\",\"phase\":\"").Append(Esc(h.phase))
                    .Append("\",\"bots\":").Append(h.bots).Append(",\"wrecks\":").Append(h.wrecks).Append('}');
                Debug.Log("SKYLINE_PERF worst " + (i + 1) + " " + F(h.ms) + " ms at " + F(h.at) + " s, gcs " + h.gcs + ", phase " + h.phase +
                          ", bots " + h.bots + ", wrecks " + h.wrecks + ", after: " + h.after);
            }
            var phases = new StringBuilder();
            float worstFight = 0f;
            int fightsMeasured = 0, written = 0;
            foreach (var p in _phases)
            {
                if (p.frames.Count == 0) continue;
                var ps = FrameStats.Summarize(p.frames);
                if (p.fight)
                {
                    if (ps.onePercentLowMs > worstFight) worstFight = ps.onePercentLowMs;
                    if (p.ended) fightsMeasured++;
                }
                if (written++ > 0) phases.Append(',');
                phases.Append("{\"name\":\"").Append(Esc(p.name)).Append("\",\"fight\":").Append(p.fight ? "true" : "false")
                    .Append(",\"complete\":").Append(!p.fight || p.ended ? "true" : "false")
                    .Append(",\"fromS\":").Append(F(p.from)).Append(",\"toS\":").Append(F(p.to))
                    .Append(",\"frames\":").Append(ps.frames).Append(",\"avgMs\":").Append(F(ps.avgMs))
                    .Append(",\"onePercentLowMs\":").Append(F(ps.onePercentLowMs)).Append(",\"worstMs\":").Append(F(ps.worstMs))
                    .Append(",\"worstAfter\":\"").Append(Esc(p.worstAfter)).Append("\"}");
                Debug.Log("SKYLINE_PERF phase " + p.name + (p.fight && !p.ended ? " (cut off)" : "") + ": " + ps.frames + " frames, avg " + F(ps.avgMs) +
                          " ms, 1% low " + F(ps.onePercentLowMs) + " ms, worst " + F(ps.worstMs) + " ms (" + F(p.from) + "-" + F(p.to) + " s)");
            }
            Debug.Log("SKYLINE_PERF warm-up worst " + F(_warmupWorst * 1000f) + " ms; window from " + F(_measuredFrom) + " s; demo " +
                      (_demoDone ? "complete" : "NOT complete (cap " + F(_cap) + " s)") + "; fights measured " + fightsMeasured +
                      ", worst fight 1% low " + F(worstFight) + " ms; most wrecks " + _maxWrecks + ", most bots " + _maxBots +
                      "; " + SkylineQuality.Describe());
            var asset = SkylineQuality.Asset;
            int exitCode = 0;
            try
            {
                Directory.CreateDirectory(_outDir);
                File.WriteAllText(Path.Combine(_outDir, "perf.json"),
                    "{\"frames\":" + s.frames + ",\"avgMs\":" + F(s.avgMs) + ",\"onePercentLowMs\":" + F(s.onePercentLowMs) +
                    ",\"worstMs\":" + F(s.worstMs) + ",\"resolution\":\"" + Screen.width + "x" + Screen.height + "\",\"gpu\":\"" + Esc(SystemInfo.graphicsDeviceName) +
                    "\",\"scene\":\"" + Esc(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name) + "\"" +
                    ",\"warmupWorstMs\":" + F(_warmupWorst * 1000f) + ",\"measuredFromS\":" + F(_measuredFrom) +
                    ",\"demoComplete\":" + (_demoDone ? "true" : "false") + ",\"fightsMeasured\":" + fightsMeasured +
                    ",\"worstFightOnePercentLowMs\":" + F(worstFight) + ",\"mostWrecks\":" + _maxWrecks + ",\"mostBots\":" + _maxBots +
                    ",\"quality\":\"" + Esc(SkylineQuality.LevelName) + "\",\"pipeline\":\"" + Esc(asset != null ? asset.name : "") + "\"" +
                    ",\"renderScale\":" + F(asset != null ? asset.renderScale : 1f) + ",\"upscaling\":\"" + (asset != null ? asset.upscalingFilter.ToString() : "") + "\"" +
                    ",\"phases\":[" + phases + "]" +
                    ",\"worst\":[" + worst + "]}");
            }
            catch (Exception e)
            {
                Debug.LogError("SKYLINE_PERF could not write perf.json: " + e.Message);
                exitCode = 1;
            }
            Application.Quit(exitCode);
        }
    }
}
