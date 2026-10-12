using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Unity.Profiling;
using UnityEngine;

namespace JamKit.Perf
{
    /// <summary>
    /// Records one scene's frames for the perf probe: frame time, CPU main / render thread and GPU time (FrameTimingManager,
    /// when the build has it: development builds, or Frame Timing Stats on), the render counters (draw calls, SetPass calls,
    /// batches, triangles, vertices, shadow casters: development builds and the editor only) and memory (release builds
    /// too). Everything is preallocated; <see cref="Frame"/> allocates nothing. <see cref="ToJson"/> and
    /// <see cref="SummaryLine"/> run once at the end.
    /// </summary>
    public sealed class VhPerfRecorder : IDisposable
    {
        public const float TargetMs = 1000f / 120f, FloorMs = 1000f / 60f;

        readonly int _capacity;
        readonly float[] _ms, _cpu, _gpu, _render;
        readonly int[] _draws, _setpass, _batches, _casters;
        readonly long[] _tris, _verts;
        readonly short[] _leg;
        readonly FrameTiming[] _timing = new FrameTiming[1];
        int _count;
        int _hitchCount;
        float _hitchWorst;
        string _hitchWorstLeg = "";
        long _peakTotal, _peakSystem, _peakGfx, _peakGc;
        int _gcStart;

        ProfilerRecorder _rDraws, _rSetPass, _rBatches, _rTris, _rVerts, _rCasters;
        ProfilerRecorder _mTotal, _mReserved, _mSystem, _mGfx, _mGc;

        public readonly string id;
        public bool frameTimingAvailable;

        public int Count => _count;

        public VhPerfRecorder(string id, int capacity)
        {
            this.id = id;
            _capacity = Mathf.Max(64, capacity);
            _ms = new float[_capacity];
            _cpu = new float[_capacity];
            _gpu = new float[_capacity];
            _render = new float[_capacity];
            _draws = new int[_capacity];
            _setpass = new int[_capacity];
            _batches = new int[_capacity];
            _casters = new int[_capacity];
            _tris = new long[_capacity];
            _verts = new long[_capacity];
            _leg = new short[_capacity];
        }

        public void Start()
        {
            _count = 0;
            _hitchCount = 0;
            _hitchWorst = 0f;
            _peakTotal = _peakSystem = _peakGfx = _peakGc = 0;
            _gcStart = GC.CollectionCount(0);
            _rDraws = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            _rSetPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            _rBatches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            _rTris = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            _rVerts = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Vertices Count");
            _rCasters = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Shadow Casters Count");
            _mTotal = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory");
            _mReserved = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Reserved Memory");
            _mSystem = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory");
            _mGfx = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Gfx Used Memory");
            _mGc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Used Memory");
            frameTimingAvailable = FrameTimingManager.IsFeatureEnabled();
        }

        /// <summary>Called once a frame while flying. <paramref name="afterCut"/>: within the settle time after a cut (kept
        /// out of the steady numbers, reported as hitches).</summary>
        public void Frame(float ms, int leg, bool afterCut, string legLabel)
        {
            float cpu = 0f, gpu = 0f, render = 0f;
            if (frameTimingAvailable)
            {
                FrameTimingManager.CaptureFrameTimings();
                if (FrameTimingManager.GetLatestTimings(1, _timing) > 0)
                {
                    cpu = (float)_timing[0].cpuMainThreadFrameTime;
                    render = (float)_timing[0].cpuRenderThreadFrameTime;
                    gpu = (float)_timing[0].gpuFrameTime;
                }
            }
            Peak(ref _peakTotal, _mTotal);
            Peak(ref _peakSystem, _mSystem);
            Peak(ref _peakGfx, _mGfx);
            Peak(ref _peakGc, _mGc);
            if (afterCut)
            {
                _hitchCount++;
                if (ms > _hitchWorst) { _hitchWorst = ms; _hitchWorstLeg = legLabel; }
                return;
            }
            if (_count >= _capacity) return;
            int i = _count++;
            _ms[i] = ms;
            _cpu[i] = cpu;
            _gpu[i] = gpu;
            _render[i] = render;
            _draws[i] = (int)Value(_rDraws);
            _setpass[i] = (int)Value(_rSetPass);
            _batches[i] = (int)Value(_rBatches);
            _casters[i] = (int)Value(_rCasters);
            _tris[i] = Value(_rTris);
            _verts[i] = Value(_rVerts);
            _leg[i] = (short)Mathf.Clamp(leg, 0, short.MaxValue);
        }

        static long Value(ProfilerRecorder r) => r.Valid ? r.LastValue : -1;

        static void Peak(ref long peak, ProfilerRecorder r)
        {
            if (!r.Valid) return;
            long v = r.LastValue;
            if (v > peak) peak = v;
        }

        public void Dispose()
        {
            _rDraws.Dispose(); _rSetPass.Dispose(); _rBatches.Dispose(); _rTris.Dispose(); _rVerts.Dispose(); _rCasters.Dispose();
            _mTotal.Dispose(); _mReserved.Dispose(); _mSystem.Dispose(); _mGfx.Dispose(); _mGc.Dispose();
        }

        // ---------- Results (once, at the end) ----------

        public sealed class Stats
        {
            public int frames;
            public float mean, p50, p90, p99, worst1, worst, over8, over16;
            public float cpu, gpu, render, gpuP99;
            public double draws, setpass, batches, casters, tris, verts;
            public long drawsMax, setpassMax, trisMax, castersMax;
        }

        public Stats Compute(int legFilter = -1)
        {
            var sel = new List<int>();
            for (int i = 0; i < _count; i++) if (legFilter < 0 || _leg[i] == legFilter) sel.Add(i);
            var s = new Stats { frames = sel.Count };
            if (sel.Count == 0) return s;
            var ms = new float[sel.Count];
            var gpu = new List<float>();
            double sum = 0, cpu = 0, render = 0, gpuSum = 0;
            int over8 = 0, over16 = 0, timed = 0;
            for (int k = 0; k < sel.Count; k++)
            {
                int i = sel[k];
                ms[k] = _ms[i];
                sum += _ms[i];
                if (_ms[i] > TargetMs) over8++;
                if (_ms[i] > FloorMs) over16++;
                if (_gpu[i] > 0f || _cpu[i] > 0f) { timed++; cpu += _cpu[i]; render += _render[i]; gpuSum += _gpu[i]; gpu.Add(_gpu[i]); }
                s.draws += _draws[i]; s.setpass += _setpass[i]; s.batches += _batches[i]; s.casters += _casters[i];
                s.tris += _tris[i]; s.verts += _verts[i];
                s.drawsMax = Math.Max(s.drawsMax, _draws[i]); s.setpassMax = Math.Max(s.setpassMax, _setpass[i]);
                s.trisMax = Math.Max(s.trisMax, _tris[i]); s.castersMax = Math.Max(s.castersMax, _casters[i]);
            }
            int n = sel.Count;
            s.draws /= n; s.setpass /= n; s.batches /= n; s.casters /= n; s.tris /= n; s.verts /= n;
            Array.Sort(ms);
            s.mean = (float)(sum / n);
            s.p50 = P(ms, 0.5f);
            s.p90 = P(ms, 0.9f);
            s.p99 = P(ms, 0.99f);
            int worstN = Math.Max(1, n / 100);
            double w = 0;
            for (int k = n - worstN; k < n; k++) w += ms[k];
            s.worst1 = (float)(w / worstN);
            s.worst = ms[n - 1];
            s.over8 = 100f * over8 / n;
            s.over16 = 100f * over16 / n;
            if (timed > 0)
            {
                s.cpu = (float)(cpu / timed);
                s.render = (float)(render / timed);
                s.gpu = (float)(gpuSum / timed);
                gpu.Sort();
                s.gpuP99 = P(gpu, 0.99f);
            }
            return s;
        }

        static float P(float[] sorted, float q) => sorted.Length == 0 ? 0f : sorted[Mathf.Clamp(Mathf.CeilToInt(q * sorted.Length) - 1, 0, sorted.Length - 1)];
        static float P(List<float> sorted, float q) => sorted.Count == 0 ? 0f : sorted[Mathf.Clamp(Mathf.CeilToInt(q * sorted.Count) - 1, 0, sorted.Count - 1)];

        /// <summary>"TARGET MET" when the mean and the 90th percentile are within 120 fps; the floor is the 1% low (the mean
        /// of the slowest 1% of frames) within 60 fps.</summary>
        public static string Verdict(Stats s)
        {
            if (s.frames == 0) return "NO DATA";
            string target = s.mean <= TargetMs && s.p90 <= TargetMs * 1.15f ? "TARGET MET" : "under target";
            string floor = s.worst1 <= FloorMs ? "floor ok" : "FLOOR FAIL (blocker)";
            return target + ", " + floor;
        }

        static string F(double v, string fmt = "0.00") => v.ToString(fmt, CultureInfo.InvariantCulture);
        public static string Q(string s) => "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        static string Mb(long bytes) => bytes <= 0 ? "-1" : F(bytes / (1024.0 * 1024.0), "0");

        public long PeakTotalBytes => _peakTotal;

        public string SummaryLine(Stats s)
        {
            var sb = new StringBuilder();
            sb.Append(id.PadRight(7)).Append(" avg ").Append(F(s.mean)).Append(" ms (").Append(F(s.mean > 0 ? 1000.0 / s.mean : 0, "0")).Append(" fps)");
            sb.Append(" | p90 ").Append(F(s.p90)).Append(" | 1% low ").Append(F(s.worst1)).Append(" ms (").Append(F(s.worst1 > 0 ? 1000.0 / s.worst1 : 0, "0")).Append(" fps)");
            sb.Append(" | worst ").Append(F(s.worst, "0.0"));
            if (s.gpu > 0f || s.cpu > 0f)
                sb.Append(" | CPU ").Append(F(s.cpu, "0.0")).Append(" GPU ").Append(F(s.gpu, "0.0")).Append(s.gpu > s.cpu ? " (GPU-bound)" : " (CPU-bound)");
            if (_rDraws.Valid)
                sb.Append(" | draws ").Append(F(s.draws, "0")).Append(" setpass ").Append(F(s.setpass, "0")).Append(" tris ").Append(F(s.tris / 1e6, "0.00")).Append("M casters ").Append(F(s.casters, "0"));
            sb.Append(" | mem ").Append(Mb(_peakTotal)).Append(" MB");
            sb.Append(" | ").Append(Verdict(s));
            return sb.ToString();
        }

        /// <summary>The worst legs by mean frame time, most expensive first.</summary>
        public List<KeyValuePair<string, Stats>> WorstLegs(VhPerfFlight flight, int max)
        {
            var list = new List<KeyValuePair<string, Stats>>();
            for (int leg = 0; flight != null && leg < flight.legs.Count; leg++)
            {
                var st = Compute(leg);
                if (st.frames < 5) continue;
                list.Add(new KeyValuePair<string, Stats>(leg + " " + flight.legs[leg].label, st));
            }
            list.Sort((a, b) => b.Value.mean.CompareTo(a.Value.mean));
            if (list.Count > max) list.RemoveRange(max, list.Count - max);
            return list;
        }

        public string ToJson(VhPerfFlight flight, string machine, string scene, string problem)
        {
            var s = Compute();
            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"id\": ").Append(Q(id)).Append(",\n");
            sb.Append("  \"scene\": ").Append(Q(scene)).Append(",\n");
            sb.Append("  \"what\": ").Append(Q(VhPerfScenes.Describe(id))).Append(",\n");
            sb.Append("  \"problem\": ").Append(problem == null ? "null" : Q(problem)).Append(",\n");
            sb.Append("  \"machine\": ").Append(machine).Append(",\n");
            sb.Append("  \"verdict\": ").Append(Q(Verdict(s))).Append(",\n");
            AppendStats(sb, "frame", s, "  ");
            sb.Append(",\n  \"hitches\": { \"framesAfterCuts\": ").Append(_hitchCount).Append(", \"worstMs\": ").Append(F(_hitchWorst)).Append(", \"worstAt\": ").Append(Q(_hitchWorstLeg)).Append(" },\n");
            sb.Append("  \"memoryPeakMB\": { \"totalUsed\": ").Append(Mb(_peakTotal)).Append(", \"system\": ").Append(Mb(_peakSystem))
              .Append(", \"gfx\": ").Append(Mb(_peakGfx)).Append(", \"gcHeap\": ").Append(Mb(_peakGc))
              .Append(", \"reservedAtEnd\": ").Append(Mb(_mReserved.Valid ? _mReserved.LastValue : -1)).Append(" },\n");
            sb.Append("  \"gcCollections\": ").Append(GC.CollectionCount(0) - _gcStart).Append(",\n");
            sb.Append("  \"legs\": [\n");
            bool first = true;
            for (int leg = 0; flight != null && leg < flight.legs.Count; leg++)
            {
                var st = Compute(leg);
                if (st.frames == 0) continue;
                if (!first) sb.Append(",\n");
                first = false;
                var l = flight.legs[leg];
                sb.Append("    { \"leg\": ").Append(leg).Append(", \"label\": ").Append(Q(l.label)).Append(", ");
                sb.Append("\"eye\": [").Append(F(l.eye0.x, "0.0")).Append(", ").Append(F(l.eye0.y, "0.0")).Append(", ").Append(F(l.eye0.z, "0.0")).Append("], ");
                sb.Append("\"frames\": ").Append(st.frames).Append(", \"meanMs\": ").Append(F(st.mean)).Append(", \"p99Ms\": ").Append(F(st.p99))
                  .Append(", \"worstMs\": ").Append(F(st.worst)).Append(", \"gpuMs\": ").Append(F(st.gpu)).Append(", \"cpuMs\": ").Append(F(st.cpu))
                  .Append(", \"draws\": ").Append(F(st.draws, "0")).Append(", \"tris\": ").Append(F(st.tris, "0")).Append(", \"casters\": ").Append(F(st.casters, "0")).Append(" }");
            }
            sb.Append("\n  ]\n}\n");
            return sb.ToString();
        }

        void AppendStats(StringBuilder sb, string name, Stats s, string indent)
        {
            sb.Append(indent).Append("\"").Append(name).Append("\": { \"frames\": ").Append(s.frames)
              .Append(", \"meanMs\": ").Append(F(s.mean)).Append(", \"fps\": ").Append(F(s.mean > 0 ? 1000.0 / s.mean : 0, "0.0"))
              .Append(", \"p50Ms\": ").Append(F(s.p50)).Append(", \"p90Ms\": ").Append(F(s.p90)).Append(", \"p99Ms\": ").Append(F(s.p99))
              .Append(", \"onePercentLowMs\": ").Append(F(s.worst1)).Append(", \"worstMs\": ").Append(F(s.worst))
              .Append(", \"over8_3msPct\": ").Append(F(s.over8, "0.0")).Append(", \"over16_7msPct\": ").Append(F(s.over16, "0.0"))
              .Append(", \"frameTiming\": ").Append(frameTimingAvailable ? "true" : "false")
              .Append(", \"cpuMainMs\": ").Append(F(s.cpu)).Append(", \"cpuRenderMs\": ").Append(F(s.render)).Append(", \"gpuMs\": ").Append(F(s.gpu)).Append(", \"gpuP99Ms\": ").Append(F(s.gpuP99))
              .Append(", \"counters\": ").Append(_rDraws.Valid ? "true" : "false")
              .Append(", \"drawCalls\": ").Append(F(s.draws, "0")).Append(", \"drawCallsMax\": ").Append(s.drawsMax)
              .Append(", \"setPassCalls\": ").Append(F(s.setpass, "0")).Append(", \"setPassMax\": ").Append(s.setpassMax)
              .Append(", \"batches\": ").Append(F(s.batches, "0"))
              .Append(", \"triangles\": ").Append(F(s.tris, "0")).Append(", \"trianglesMax\": ").Append(s.trisMax)
              .Append(", \"vertices\": ").Append(F(s.verts, "0"))
              .Append(", \"shadowCasters\": ").Append(F(s.casters, "0")).Append(", \"shadowCastersMax\": ").Append(s.castersMax)
              .Append(" }");
        }
    }
}
