using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using JamKit.Skyline;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using Debug = UnityEngine.Debug;

namespace JamKit.SkylineRepo
{
    /// <summary>SkylinePass stage D: run the built player with <c>-skyline-perf &lt;dir&gt;</c>; after a 3 s warm-up it
    /// records frame times in the first scene (the arena) until the scripted demo ends (<c>SKYLINE_DEMO done</c>, or
    /// <c>SKYLINE_ARENA exit reached</c> plus 3 s), at most <see cref="MaxMeasure"/> s (<c>-skyline-perf-seconds &lt;s&gt;</c>
    /// overrides the cap), writes perf.json and quits.
    /// <para>Judge round 2 (E 11): the old 45 s window ended inside the first fight. The window now runs the whole demo and
    /// perf.json reports each stretch of it on its own (<c>phases</c>): the warm-up hall, each fight from its
    /// <c>SKYLINE_ARENA encounter &lt;id&gt; start</c> to its <c>cleared</c>, the walks between, and the lift. Each phase has
    /// its frames, average, 1% low and worst; <c>worstFightOnePercentLowMs</c> is the worst fight's 1% low (the number the
    /// blocker is about), <c>fightsMeasured</c> how many fights ran to their end inside the window, and
    /// <c>demoComplete</c> whether the demo finished before the cap. Perf round 3: the cap was 150 s and the build's demo now
    /// takes longer (its warm-up hall alone ran 41 s), so the skylight fight was cut off; the cap is 215 s (the pass's
    /// stage timeout is 240 s, the player's load a few seconds).</para>
    /// <para><b>What a slow frame was made of</b> (perf round 3: the average is 5 ms but 1% of fight frames take 18-25 ms, and
    /// without a profiler the cause was guesswork). Each frame is split into: <c>cpuMs</c>, the main thread from the first
    /// Update to the end of the frame (scripts, animation, culling and the render submission); <c>physMs</c>, the fixed steps
    /// before it (physics and FixedUpdate); and <c>waitMs</c>, the rest of the frame (mostly waiting for the GPU to present
    /// the previous frame, so a GPU-bound frame shows up here). Each phase reports the averages of the three over all its
    /// frames and over its slowest 1% (<c>slow</c>), and each of the 10 worst frames carries them too, with the render
    /// counters of the frame before (<c>batches</c>, <c>setPass</c>, <c>shadowCasters</c>, <c>tris</c>: Unity's release-player
    /// counters) and, when the player has frame-timing stats on, the GPU's own time (<c>gpuMs</c>, else -1).</para>
    /// <para>A frame belongs to the window only if it started after the warm-up (the frame that straddles the warm-up's end
    /// is the warm-up's). The warm-up's own slowest frame is still reported (<c>warmupWorstMs</c>); <c>measuredFromS</c> says
    /// where the window started. Each worst frame has when it ended (<c>at</c>), its garbage collections (<c>gcs</c>), the
    /// last <c>SKYLINE_</c> log line before it (<c>after</c>), its phase, and the live bots and wrecks. The drawing settings are
    /// recorded too (<c>quality</c>, <c>pipeline</c>, <c>renderScale</c>, <c>upscaling</c>).</para>
    /// <para>The run also traces the graphics pipeline states the game creates (a <see cref="GraphicsStateCollection"/>) and
    /// saves them as <c>&lt;dir&gt;/SkylineArena.graphicsstate</c>: copied to <c>Assets/StreamingAssets/Skyline/</c> before the
    /// next build, <see cref="SkylineStartCover"/> warms them up behind the start cover. Perf round 3: the first trace was
    /// empty and labelled "VH High": it began before the quality level was pinned, so the trace now pins it first.</para></summary>
    [DefaultExecutionOrder(-32000)]
    public sealed class SkylinePerfProbe : MonoBehaviour
    {
        const float Warmup = 3f, MaxMeasure = 215f, AfterExit = 3f;
        const int WorstCount = 10;
        public const string TraceFile = "SkylineArena.graphicsstate";

        struct Hitch
        {
            public float ms, at, cpu, phys, wait, gpu;
            public int gcs, bots, wrecks;
            public long batches, setPass, casters, tris;
            public string after, phase;
        }

        sealed class Phase
        {
            public string name;
            public bool fight, ended;
            public float from, to, worstMs;
            public string worstAfter = "";
            public readonly List<float> frames = new List<float>(4096), cpu = new List<float>(4096), phys = new List<float>(4096), wait = new List<float>(4096);
        }

        readonly List<float> _frames = new List<float>(32768);
        readonly List<Hitch> _worst = new List<Hitch>(WorstCount + 1);
        // perf round 5: how the slow frames are spread (a 1% low of 29 ms can be 30 stalls or 200 mid-size frames), and the first
        // SpikeKeep frames over SpikeMs with what had just happened, so the next pass can say which events they follow
        const float SpikeMs = 25f;
        const int SpikeKeep = 80;
        readonly List<Hitch> _spikes = new List<Hitch>(SpikeKeep);
        int _over17, _over25, _over33, _over50, _over100;
        bool _lastCoverDone;
        readonly List<Phase> _phases = new List<Phase>();
        Phase _phase;
        string _outDir;
        float _start, _lastT = -1f, _warmupWorst, _measuredFrom = -1f, _cap = MaxMeasure, _endAt = -1f;
        int _lastGc, _maxWrecks, _maxBots;
        string _lastLine = "";
        bool _demoDone;
        GraphicsStateCollection _trace;

        // the frame split: stopwatch ticks
        static readonly double TickMs = 1000.0 / Stopwatch.Frequency;
        long _fixedStart = -1, _updateAt = -1;
        float _curPhys, _prevCpu, _prevPhys;
        ProfilerRecorder _batches, _setPass, _casters, _tris;
        readonly FrameTiming[] _timing = new FrameTiming[1];
        bool _timingOn;

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
            SkylineInkFeature.ForceOff = SkylineRunArgs.Has("-skyline-no-ink");   // measurement only: the cost of the ink passes
            Debug.Log("SKYLINE_PERF switches: ink off " + SkylineInkFeature.ForceOff + ", " + SkylinePerfSwitches.Json());
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
        /// frame are inside it), after pinning the quality level (a trace belongs to one level), seeded with the trace the build
        /// already carries, so the saved file holds both.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void StartTrace()
        {
            _earlyTrace = null;
            if (Application.isEditor || SkylineRunArgs.Value("-skyline-perf") == null) return;
            SkylineQuality.EnsurePinned();
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
                Debug.Log("SKYLINE_PERF pipeline states traced: " + _trace.totalGraphicsStateCount + " (level " + SkylineQuality.LevelName + ")" +
                          (ok ? ", saved " + path : ", NOT saved"));
            }
            catch (Exception e)
            {
                Debug.Log("SKYLINE_PERF pipeline-state trace not saved: " + e.Message);
            }
            _trace = null;
        }

        void OnEnable()
        {
            Application.logMessageReceived += OnLog;
            _batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            _setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            _casters = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Shadow Casters Count");
            _tris = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            _timingOn = FrameTimingManager.IsFeatureEnabled();
            StartCoroutine(EndOfFrames());
        }

        void OnDisable()
        {
            Application.logMessageReceived -= OnLog;
            _batches.Dispose();
            _setPass.Dispose();
            _casters.Dispose();
            _tris.Dispose();
        }

        void FixedUpdate()
        {
            if (_fixedStart < 0) _fixedStart = Stopwatch.GetTimestamp();
        }

        IEnumerator EndOfFrames()
        {
            var eof = new WaitForEndOfFrame();
            while (true)
            {
                yield return eof;
                long now = Stopwatch.GetTimestamp();
                _prevCpu = _updateAt >= 0 ? (float)((now - _updateAt) * TickMs) : 0f;
                _prevPhys = _curPhys;
                if (_timingOn) FrameTimingManager.CaptureFrameTimings();
            }
        }

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
                // perf round 4: the run reached its goal; the window ends 3 s later, often before the demo's own "done"
                // line, which used to leave demoComplete false on a finished run
                _demoDone = true;
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
            long nowTicks = Stopwatch.GetTimestamp();
            _curPhys = _fixedStart >= 0 ? (float)((nowTicks - _fixedStart) * TickMs) : 0f;
            _fixedStart = -1;
            _updateAt = nowTicks;

            float t = Time.realtimeSinceStartup - _start;
            float dt = Time.unscaledDeltaTime;
            int gc = GC.CollectionCount(0);
            int gcs = gc - _lastGc;
            _lastGc = gc;
            // the window starts after the warm-up seconds AND once the start cover has begun to lift (the scene's load-time
            // drawings run behind it; in the editor there is no cover), judged on the previous frame so the straddling frame is out
            bool startedAfterWarmup = _lastT >= Warmup && _lastCoverDone;
            _lastT = t;
            _lastCoverDone = SkylineStartCover.Done;
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
            if (ms > 16.7f) _over17++;
            if (ms > 25f) _over25++;
            if (ms > 33.4f) _over33++;
            if (ms > 50f) _over50++;
            if (ms > 100f) _over100++;
            // the frame that just ended (unscaledDeltaTime's frame) is the one whose split EndOfFrames stored
            float cpu = _prevCpu, phys = _prevPhys, wait = Mathf.Max(0f, ms - cpu - phys);
            if (_phase != null)
            {
                _phase.frames.Add(dt);
                _phase.cpu.Add(cpu);
                _phase.phys.Add(phys);
                _phase.wait.Add(wait);
                _phase.to = t;
                if (ms > _phase.worstMs) { _phase.worstMs = ms; _phase.worstAfter = _lastLine; }
            }
            if (ms >= SpikeMs && _spikes.Count < SpikeKeep)
                _spikes.Add(new Hitch { ms = ms, at = t, gcs = gcs, after = _lastLine, phase = _phase != null ? _phase.name : "", bots = bots, wrecks = wrecks, cpu = cpu, phys = phys, wait = wait, gpu = -1f });
            if (_worst.Count < WorstCount || ms > _worst[WorstCount - 1].ms)
            {
                float gpu = -1f;
                if (_timingOn && FrameTimingManager.GetLatestTimings(1, _timing) > 0) gpu = (float)_timing[0].gpuFrameTime;
                Keep(new Hitch
                {
                    ms = ms, at = t, gcs = gcs, after = _lastLine, phase = _phase != null ? _phase.name : "", bots = bots, wrecks = wrecks,
                    cpu = cpu, phys = phys, wait = wait, gpu = gpu,
                    batches = _batches.Valid ? _batches.LastValue : -1, setPass = _setPass.Valid ? _setPass.LastValue : -1,
                    casters = _casters.Valid ? _casters.LastValue : -1, tris = _tris.Valid ? _tris.LastValue : -1,
                });
            }
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

        static float Mean(List<float> v)
        {
            if (v.Count == 0) return 0f;
            double s = 0;
            for (int i = 0; i < v.Count; i++) s += v[i];
            return (float)(s / v.Count);
        }

        /// <summary>The mean cpu / phys / wait of the phase's slowest 1% of frames.</summary>
        static void SlowSplit(Phase p, out float cpu, out float phys, out float wait)
        {
            int n = p.frames.Count;
            var idx = new int[n];
            for (int i = 0; i < n; i++) idx[i] = i;
            Array.Sort(idx, (a, b) => p.frames[b].CompareTo(p.frames[a]));
            int slow = Math.Max(1, n / 100);
            double c = 0, ph = 0, w = 0;
            for (int i = 0; i < slow; i++) { c += p.cpu[idx[i]]; ph += p.phys[idx[i]]; w += p.wait[idx[i]]; }
            cpu = (float)(c / slow);
            phys = (float)(ph / slow);
            wait = (float)(w / slow);
        }

        void Finish()
        {
            enabled = false;
            StopAllCoroutines();
            SaveTrace();
            var s = FrameStats.Summarize(_frames);
            var worst = new StringBuilder();
            for (int i = 0; i < _worst.Count; i++)
            {
                var h = _worst[i];
                if (i > 0) worst.Append(',');
                worst.Append("{\"ms\":").Append(F(h.ms)).Append(",\"at\":").Append(F(h.at)).Append(",\"gcs\":").Append(h.gcs)
                    .Append(",\"after\":\"").Append(Esc(h.after)).Append("\",\"phase\":\"").Append(Esc(h.phase))
                    .Append("\",\"bots\":").Append(h.bots).Append(",\"wrecks\":").Append(h.wrecks)
                    .Append(",\"cpuMs\":").Append(F(h.cpu)).Append(",\"physMs\":").Append(F(h.phys)).Append(",\"waitMs\":").Append(F(h.wait))
                    .Append(",\"gpuMs\":").Append(F(h.gpu)).Append(",\"batches\":").Append(h.batches).Append(",\"setPass\":").Append(h.setPass)
                    .Append(",\"shadowCasters\":").Append(h.casters).Append(",\"tris\":").Append(h.tris).Append('}');
                Debug.Log("SKYLINE_PERF worst " + (i + 1) + " " + F(h.ms) + " ms at " + F(h.at) + " s (cpu " + F(h.cpu) + ", physics " + F(h.phys) +
                          ", wait " + F(h.wait) + ", gpu " + F(h.gpu) + "; batches " + h.batches + ", setpass " + h.setPass + ", casters " + h.casters +
                          ", tris " + h.tris + "), gcs " + h.gcs + ", phase " + h.phase + ", bots " + h.bots + ", wrecks " + h.wrecks + ", after: " + h.after);
            }
            var spikes = new StringBuilder();
            for (int i = 0; i < _spikes.Count; i++)
            {
                var h = _spikes[i];
                if (i > 0) spikes.Append(',');
                spikes.Append("{\"ms\":").Append(F(h.ms)).Append(",\"at\":").Append(F(h.at)).Append(",\"gcs\":").Append(h.gcs)
                    .Append(",\"after\":\"").Append(Esc(h.after)).Append("\",\"phase\":\"").Append(Esc(h.phase))
                    .Append("\",\"bots\":").Append(h.bots).Append(",\"wrecks\":").Append(h.wrecks)
                    .Append(",\"cpuMs\":").Append(F(h.cpu)).Append(",\"physMs\":").Append(F(h.phys)).Append(",\"waitMs\":").Append(F(h.wait)).Append('}');
                Debug.Log("SKYLINE_PERF spike " + F(h.ms) + " ms at " + F(h.at) + " s (cpu " + F(h.cpu) + ", physics " + F(h.phys) + ", wait " + F(h.wait) +
                          "), gcs " + h.gcs + ", phase " + h.phase + ", bots " + h.bots + ", wrecks " + h.wrecks + ", after: " + h.after);
            }
            Debug.Log("SKYLINE_PERF frames over 16.7 ms: " + _over17 + ", over 25 ms: " + _over25 + ", over 33 ms: " + _over33 + ", over 50 ms: " + _over50 +
                      ", over 100 ms: " + _over100 + " of " + _frames.Count + " (the first " + _spikes.Count + " over " + F(SpikeMs) + " ms are listed as spikes)");
            var phases = new StringBuilder();
            float worstFight = 0f;
            int fightsMeasured = 0, written = 0;
            foreach (var p in _phases)
            {
                if (p.frames.Count == 0) continue;
                var ps = FrameStats.Summarize(p.frames);
                SlowSplit(p, out float sc, out float sp, out float sw);
                float ac = Mean(p.cpu), ap = Mean(p.phys), aw = Mean(p.wait);
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
                    .Append(",\"worstAfter\":\"").Append(Esc(p.worstAfter)).Append('"')
                    .Append(",\"avg\":{\"cpuMs\":").Append(F(ac)).Append(",\"physMs\":").Append(F(ap)).Append(",\"waitMs\":").Append(F(aw)).Append('}')
                    .Append(",\"slow\":{\"cpuMs\":").Append(F(sc)).Append(",\"physMs\":").Append(F(sp)).Append(",\"waitMs\":").Append(F(sw)).Append("}}");
                Debug.Log("SKYLINE_PERF phase " + p.name + (p.fight && !p.ended ? " (cut off)" : "") + ": " + ps.frames + " frames, avg " + F(ps.avgMs) +
                          " ms (cpu " + F(ac) + ", physics " + F(ap) + ", wait " + F(aw) + "), 1% low " + F(ps.onePercentLowMs) + " ms (cpu " + F(sc) +
                          ", physics " + F(sp) + ", wait " + F(sw) + "), worst " + F(ps.worstMs) + " ms (" + F(p.from) + "-" + F(p.to) + " s)");
            }
            Debug.Log("SKYLINE_PERF warm-up worst " + F(_warmupWorst * 1000f) + " ms; window from " + F(_measuredFrom) + " s; demo " +
                      (_demoDone ? "complete" : "NOT complete (cap " + F(_cap) + " s)") + "; fights measured " + fightsMeasured +
                      ", worst fight 1% low " + F(worstFight) + " ms; most wrecks " + _maxWrecks + ", most bots " + _maxBots +
                      "; gpu timing " + (_timingOn ? "on" : "off") + "; " + SkylineQuality.Describe());
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
                    ",\"shadows\":\"" + Esc(asset != null ? asset.shadowDistance.ToString("0", CultureInfo.InvariantCulture) + " m x" + asset.shadowCascadeCount : "") + "\"" +
                    ",\"gpuTiming\":" + (_timingOn ? "true" : "false") +
                    ",\"inkOff\":" + (SkylineInkFeature.ForceOff ? "true" : "false") +
                    "," + SkylinePerfSwitches.Json() +
                    ",\"over\":{\"ms16_7\":" + _over17 + ",\"ms25\":" + _over25 + ",\"ms33\":" + _over33 + ",\"ms50\":" + _over50 + ",\"ms100\":" + _over100 + "}" +
                    ",\"spikes\":[" + spikes + "]" +
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
