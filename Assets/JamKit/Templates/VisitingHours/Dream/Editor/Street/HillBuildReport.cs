using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;

namespace JamKit.DreamEscape.EditorTools
{
    /// <summary>
    /// The hill build's memory and size report (World round 2): one line per phase in the build log
    /// ("JAMKIT_STREET_MEM ..."): the process's committed memory (what the session's memory cap counts) and its peak,
    /// Unity's native allocations, the managed heap, the graphics driver, the meshes made so far (count, vertices,
    /// bytes); and at the end the scene's totals (objects, renderers, unique meshes, vertices, triangles, LOD groups,
    /// colliders, lights) and the generated assets on disk. Also written to out/street-report/&lt;time&gt;.txt.
    /// </summary>
    public sealed class HillBuildReport
    {
        readonly StringBuilder _text = new StringBuilder();
        readonly Stopwatch _clock = Stopwatch.StartNew();
        long _peakCommit, _peakManaged, _peakNative;
        string _phase = "start";
        readonly string _what;

        public HillBuildReport(string what)
        {
            _what = what;
            Line("JAMKIT_STREET_MEM " + what + ": cap 30% of RAM = " + Gb(SystemInfo.systemMemorySize * 1024L * 1024L * 3 / 10) + " (physical " + SystemInfo.systemMemorySize + " MB), graphics " + SystemInfo.graphicsDeviceType);
            Phase("start");
        }

        static string Gb(long bytes) => (bytes / (1024.0 * 1024 * 1024)).ToString("0.00") + " GB";
        static string Mb(long bytes) => (bytes / (1024.0 * 1024)).ToString("0") + " MB";

        // Unity's Mono returns 0 for Process.PrivateMemorySize64 on Windows (the judged report read "0.00 GB" throughout):
        // ask Windows directly (psapi GetProcessMemoryInfo: PrivateUsage is the commit charge, PeakPagefileUsage its peak).
        [StructLayout(LayoutKind.Sequential)]
        struct MemoryCounters
        {
            public uint cb, pageFaultCount;
            public UIntPtr peakWorkingSet, workingSet, quotaPeakPagedPool, quotaPagedPool, quotaPeakNonPagedPool, quotaNonPagedPool,
                pagefileUsage, peakPagefileUsage, privateUsage;
        }

        [DllImport("psapi.dll", SetLastError = true)]
        static extern bool GetProcessMemoryInfo(IntPtr process, out MemoryCounters counters, uint size);

        [DllImport("kernel32.dll")]
        static extern IntPtr GetCurrentProcess();

        static bool Counters(out MemoryCounters c)
        {
            c = default;
            try
            {
                if (Application.platform != RuntimePlatform.WindowsEditor) return false;
                return GetProcessMemoryInfo(GetCurrentProcess(), out c, (uint)Marshal.SizeOf<MemoryCounters>());
            }
            catch { return false; }
        }

        /// <summary>The process's committed (private) memory: what the Job Object cap counts.</summary>
        public static long Commit()
        {
            if (Counters(out var c)) return (long)c.privateUsage.ToUInt64();
            try { using (var p = Process.GetCurrentProcess()) return p.PrivateMemorySize64; }
            catch { return 0; }
        }

        static long PeakCommit()
        {
            if (Counters(out var c)) return (long)c.peakPagefileUsage.ToUInt64();
            try { using (var p = Process.GetCurrentProcess()) return p.PeakPagedMemorySize64; }
            catch { return 0; }
        }

        /// <summary>Samples memory (call between chunks so the phase's peaks are real).</summary>
        public void Sample()
        {
            _peakCommit = Math.Max(_peakCommit, Commit());
            _peakManaged = Math.Max(_peakManaged, Profiler.GetMonoUsedSizeLong());
            _peakNative = Math.Max(_peakNative, Profiler.GetTotalAllocatedMemoryLong());
        }

        /// <summary>Ends the current phase (logging its numbers) and starts <paramref name="next"/>.</summary>
        public void Phase(string next, string note = null)
        {
            Sample();
            var line = "JAMKIT_STREET_MEM " + _phase + " done at " + (_clock.ElapsedMilliseconds / 1000.0).ToString("0.0") + " s: commit " + Gb(Commit())
                + " (phase peak " + Gb(_peakCommit) + ", process peak " + Gb(PeakCommit()) + "), native " + Mb(Profiler.GetTotalAllocatedMemoryLong())
                + " of " + Mb(Profiler.GetTotalReservedMemoryLong()) + " reserved (phase peak " + Mb(_peakNative) + "), managed " + Mb(Profiler.GetMonoUsedSizeLong())
                + " of heap " + Mb(Profiler.GetMonoHeapSizeLong()) + " (phase peak " + Mb(_peakManaged) + "), gfx driver " + Mb(Profiler.GetAllocatedMemoryForGraphicsDriver())
                + "; meshes made " + HillMeshStore.Meshes + ", " + HillMeshStore.Vertices + " vertices, " + HillMeshStore.Indices / 3 + " triangles, " + Mb(HillMeshStore.Bytes)
                + (string.IsNullOrEmpty(note) ? "" : "; " + note);
            Line(line);
            _phase = next;
            _peakCommit = _peakManaged = _peakNative = 0;
        }

        void Line(string s)
        {
            Debug.Log(s);
            _text.AppendLine(s);
        }

        /// <summary>The scene's totals (everything under the roots, as the capture and the game will load it).</summary>
        public void SceneTotals(UnityEngine.SceneManagement.Scene scene)
        {
            int objects = 0, renderers = 0, lodGroups = 0, colliders = 0, lights = 0, shadowCasters = 0, readable = 0;
            long verts = 0, tris = 0, meshBytes = 0;
            var meshes = new HashSet<int>();
            var byGroup = new Dictionary<string, long>();
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) objects++;
                lodGroups += root.GetComponentsInChildren<LODGroup>(true).Length;
                colliders += root.GetComponentsInChildren<Collider>(true).Length;
                lights += root.GetComponentsInChildren<Light>(true).Length;
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    renderers++;
                    if (r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off) shadowCasters++;
                }
                foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    var m = mf.sharedMesh;
                    if (m == null || !meshes.Add(m.GetInstanceID())) continue;
                    long v = m.vertexCount;
                    verts += v;
                    long idx = 0;
                    for (int s = 0; s < m.subMeshCount; s++) idx += m.GetIndexCount(s);
                    tris += idx / 3;
                    long bytes = JamKit.Perf.EditorTools.VhPerfMesh.Bytes(m);
                    meshBytes += bytes;
                    if (m.isReadable) readable++;
                    string g = Group(mf.transform);
                    byGroup.TryGetValue(g, out long gb);
                    byGroup[g] = gb + v;
                }
            }
            Line("JAMKIT_STREET_SIZE scene: " + objects + " game objects, " + renderers + " renderers (" + shadowCasters + " cast shadows), " + lodGroups + " LOD groups, "
                + colliders + " colliders, " + lights + " lights; " + meshes.Count + " unique meshes (" + readable + " readable), " + verts + " vertices, " + tris + " triangles, "
                + Mb(meshBytes) + " of mesh data (one copy)");
            var groups = new List<KeyValuePair<string, long>>(byGroup);
            groups.Sort((a, b) => b.Value.CompareTo(a.Value));
            var sb = new StringBuilder("JAMKIT_STREET_SIZE vertices by part:");
            foreach (var kv in groups) sb.Append(" ").Append(kv.Key).Append(" ").Append(kv.Value).Append(";");
            Line(sb.ToString());
        }

        static string Group(Transform t)
        {
            // The second level under the scene root names the part ("Drawn", "Props", "Trees", "The Lasname house", ...).
            Transform a = t, b = null;
            while (a.parent != null) { b = a; a = a.parent; }
            return b != null ? b.name : a.name;
        }

        /// <summary>Files and bytes under an asset folder (the generated meshes) and the scene file.</summary>
        public void Disk(string folder, string scenePath)
        {
            long bytes = 0;
            int files = 0;
            try
            {
                if (Directory.Exists(folder))
                    foreach (var f in Directory.GetFiles(folder, "*.asset", SearchOption.AllDirectories)) { files++; bytes += new FileInfo(f).Length; }
            }
            catch (Exception e) { Debug.LogWarning("JAMKIT_STREET disk size: " + e.Message); }
            long scene = File.Exists(scenePath) ? new FileInfo(scenePath).Length : 0;
            Line("JAMKIT_STREET_SIZE disk: " + files + " mesh asset files, " + Mb(bytes) + " in " + folder + "; the scene file " + Mb(scene));
        }

        /// <summary>Ends the report: the last phase, and the text file next to the logs.</summary>
        public void Finish()
        {
            Phase("end");
            try
            {
                string dir = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "out", "street-report");
                Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + _what + ".txt");
                File.WriteAllText(file, _text.ToString());
                Debug.Log("JAMKIT_STREET_MEM report " + file);
            }
            catch (Exception e) { Debug.LogWarning("JAMKIT_STREET couldn't write the report: " + e.Message); }
        }
    }
}
