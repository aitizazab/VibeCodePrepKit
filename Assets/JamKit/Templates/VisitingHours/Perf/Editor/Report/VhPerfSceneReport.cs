using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using JamKit.DeepBlock;
using JamKit.DreamEscape;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace JamKit.Perf.EditorTools
{
    /// <summary>
    /// The editor-side estimate (docs/redesign/perf.md "The editor estimate"): no GPU needed, so a builder can check a scene's
    /// budget before main builds. It opens a saved scene, takes viewpoints from the same flights the probe flies, and for
    /// each one counts what the camera would draw: renderers in the frustum, the LOD each LODGroup would pick (at lodBias
    /// 1), triangles per LOD band, draw calls (sub-meshes), shadow casters within the tier's shadow distance (approximate:
    /// it doesn't trace the light's frustum) and realtime lights in view. Plus the scene's totals and the audits
    /// (<see cref="VhPerfAudit"/>), and the editor's memory after opening it (the 4.6 GB cap).
    /// <code>
    /// Unity.exe -batchmode -nographics -projectPath . -executeMethod JamKit.Perf.EditorTools.VhPerfSceneReport.RunFromCommandLine
    ///     -perfScene all|&lt;scene path&gt; [-perfOut out/perf-report/&lt;time&gt;] -logFile out/perf-report.log -quit
    /// </code>
    /// Menu: JamKit > Perf > Report Open Scene. Writes &lt;scene&gt;.json and &lt;scene&gt;.txt; logs JAMKIT_PERF_REPORT_OK.
    /// </summary>
    public static class VhPerfSceneReport
    {
        /// <summary>The budget for one view at 120 fps on the RTX 3050 laptop (a guide until the probe calibrates it).</summary>
        public static class Budget
        {
            public const long Triangles = 1_500_000;
            public const int DrawCalls = 2500;
            public const int ShadowCasters = 1500;
            public const int RealtimeLights = 16;
            public const int ShadowedPointLights = 2;
        }

        public static readonly string[] KnownScenes =
        {
            "Assets/JamKit/Templates/VisitingHours/Dream/Scenes/DreamStreet.unity",
            "Assets/JamKit/Templates/VisitingHours/Dream/Scenes/DreamHouse.unity",
            "Assets/JamKit/Templates/VisitingHours/Dream/Scenes/DreamEscape.unity",
            "Assets/JamKit/Templates/VisitingHours/Scenes/VisitingHours.unity",
            "Assets/JamKit/Templates/VisitingHours/Argument/Scenes/ArgumentPrototype.unity",
            "Assets/JamKit/Templates/VisitingHours/Look/Scenes/LookLab.unity",
        };

        sealed class Item
        {
            public Renderer r;
            public Bounds b;
            public long tris;
            public int subs;
            public bool casts;
            public LODGroup group;
            public int lod = -1;
        }

        public static void RunFromCommandLine()
        {
            int code = 0;
            try
            {
                string which = VhPerfArgs.Get("-perfScene") ?? "all";
                string outDir = VhPerfArgs.Get("-perfOut") ?? Path.Combine("out", "perf-report", DateTime.Now.ToString("yyyyMMdd-HHmmss"));
                var scenes = which == "all" ? new List<string>(KnownScenes) : new List<string> { which };
                foreach (var s in scenes)
                {
                    if (!File.Exists(s)) { Debug.Log("JAMKIT_PERF report: no scene " + s + " (skipped)"); continue; }
                    EditorSceneManager.OpenScene(s, OpenSceneMode.Single);
                    Debug.Log("JAMKIT_PERF report written " + Run(outDir));
                }
                Debug.Log("JAMKIT_PERF_REPORT_OK " + Path.GetFullPath(outDir));
            }
            catch (Exception e) { Debug.LogException(e); Debug.Log("JAMKIT_PERF_REPORT_FAIL " + e.Message); code = 1; }
            EditorApplication.Exit(code);
        }

        [MenuItem("JamKit/Perf/Report Open Scene")]
        static void Menu() => Debug.Log("JAMKIT_PERF report written " + Run(Path.Combine("out", "perf-report", DateTime.Now.ToString("yyyyMMdd-HHmmss"))));

        /// <summary>Reports the open scene into <paramref name="outDir"/>; returns the text file's path.</summary>
        public static string Run(string outDir)
        {
            var scene = EditorSceneManager.GetActiveScene();
            long editorMem = Profiler.GetTotalAllocatedMemoryLong();
            long committed = 0;
            try { committed = System.Diagnostics.Process.GetCurrentProcess().PrivateMemorySize64; } catch (Exception) { }

            var renderers = new List<Renderer>();
            foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (r.enabled && !(r is ParticleSystemRenderer) && !(r is TrailRenderer) && !(r is LineRenderer)) renderers.Add(r);
            var items = new List<Item>(renderers.Count);
            var meshes = new HashSet<Mesh>();
            var mats = new HashSet<Material>();
            long totalTris = 0;
            foreach (var r in renderers)
            {
                Mesh m = null;
                if (r is MeshRenderer && r.TryGetComponent<MeshFilter>(out var mf)) m = mf.sharedMesh;
                else if (r is SkinnedMeshRenderer smr) m = smr.sharedMesh;
                var it = new Item { r = r, b = r.bounds, casts = r.shadowCastingMode != ShadowCastingMode.Off };
                if (m != null)
                {
                    meshes.Add(m);
                    for (int s = 0; s < m.subMeshCount; s++) it.tris += m.GetIndexCount(s) / 3;
                    it.subs = Mathf.Max(1, Mathf.Min(m.subMeshCount, r.sharedMaterials.Length));
                }
                else it.subs = 1;
                foreach (var sm in r.sharedMaterials) if (sm != null) mats.Add(sm);
                items.Add(it);
            }
            var groups = UnityEngine.Object.FindObjectsByType<LODGroup>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var byRenderer = new Dictionary<Renderer, Item>();
            foreach (var it in items) byRenderer[it.r] = it;
            int inGroups = 0;
            foreach (var g in groups)
            {
                var lods = g.GetLODs();
                for (int i = 0; i < lods.Length; i++)
                    foreach (var r in lods[i].renderers)
                        if (r != null && byRenderer.TryGetValue(r, out var it) && it.group == null) { it.group = g; it.lod = i; inGroups++; }
            }
            foreach (var it in items) if (it.group == null) totalTris += it.tris; else if (it.lod == 0) totalTris += it.tris;

            var lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var probes = UnityEngine.Object.FindObjectsByType<ReflectionProbe>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            // Viewpoints.
            var eyes = new List<Vector3>();
            var rots = new List<Quaternion>();
            var labels = new List<string>();
            string flightNote = Viewpoints(scene.name, eyes, rots, labels);
            var main = Camera.main;
            float fov = main != null ? main.fieldOfView : 65f;
            float far = main != null ? main.farClipPlane : 1000f;

            var camGo = new GameObject("Perf report camera") { hideFlags = HideFlags.HideAndDontSave };
            var cam = camGo.AddComponent<Camera>();
            cam.enabled = false;
            cam.fieldOfView = fov;
            cam.aspect = 16f / 9f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = far;
            float shadowDistance = VhPerfTiers.High.shadowDistance;
            var views = new List<string>();
            long maxTris = 0, sumTris = 0;
            int maxDraws = 0, maxCasters = 0, maxLights = 0, overViews = 0;
            var worst = new List<KeyValuePair<long, string>>();
            var bandTotals = new long[4];
            var planes = new Plane[6];
            var groupLod = new Dictionary<LODGroup, int>();
            for (int v = 0; v < eyes.Count; v++)
            {
                cam.transform.SetPositionAndRotation(eyes[v], rots[v]);
                GeometryUtility.CalculateFrustumPlanes(cam, planes);
                groupLod.Clear();
                long tris = 0;
                int draws = 0, casters = 0, rtLights = 0, shadowedPoints = 0;
                var band = new long[4];
                foreach (var it in items)
                {
                    if (it.group != null)
                    {
                        if (!groupLod.TryGetValue(it.group, out int sel)) { sel = SelectedLod(it.group, eyes[v], fov); groupLod[it.group] = sel; }
                        if (sel != it.lod) continue;
                    }
                    bool visible = GeometryUtility.TestPlanesAABB(planes, it.b);
                    float dist = Mathf.Sqrt(it.b.SqrDistance(eyes[v]));
                    if (it.casts && dist < shadowDistance) casters++;
                    if (!visible || dist > far) continue;
                    tris += it.tris;
                    draws += it.subs;
                    band[Mathf.Clamp(it.group == null ? 0 : it.lod, 0, 3)] += it.tris;
                }
                foreach (var l in lights)
                {
                    if (l.type == LightType.Directional || l.lightmapBakeType == LightmapBakeType.Baked) continue;
                    if (!GeometryUtility.TestPlanesAABB(planes, new Bounds(l.transform.position, Vector3.one * l.range * 2f))) continue;
                    rtLights++;
                    if (l.type == LightType.Point && l.shadows != LightShadows.None) shadowedPoints++;
                }
                bool over = tris > Budget.Triangles || draws > Budget.DrawCalls || casters > Budget.ShadowCasters || rtLights > Budget.RealtimeLights || shadowedPoints > Budget.ShadowedPointLights;
                if (over) overViews++;
                maxTris = Math.Max(maxTris, tris);
                sumTris += tris;
                maxDraws = Math.Max(maxDraws, draws);
                maxCasters = Math.Max(maxCasters, casters);
                maxLights = Math.Max(maxLights, rtLights);
                for (int i = 0; i < 4; i++) bandTotals[i] += band[i];
                string line = string.Format(CultureInfo.InvariantCulture,
                    "{{ \"view\": {0}, \"eye\": [{1:0.0}, {2:0.0}, {3:0.0}], \"triangles\": {4}, \"lod0\": {5}, \"lod1\": {6}, \"lod2\": {7}, \"lod3plus\": {8}, \"drawCalls\": {9}, \"shadowCasters\": {10}, \"realtimeLights\": {11}, \"shadowedPointLights\": {12}, \"overBudget\": {13} }}",
                    VhPerfRecorder.Q(labels[v]), eyes[v].x, eyes[v].y, eyes[v].z, tris, band[0], band[1], band[2], band[3], draws, casters, rtLights, shadowedPoints, over ? "true" : "false");
                views.Add(line);
                worst.Add(new KeyValuePair<long, string>(tris, labels[v] + string.Format(CultureInfo.InvariantCulture, " at ({0:0}, {1:0}, {2:0}): {3:0.00}M tris, {4} draws, {5} casters, {6} lights",
                    eyes[v].x, eyes[v].y, eyes[v].z, tris / 1e6, draws, casters, rtLights)));
            }
            UnityEngine.Object.DestroyImmediate(camGo);
            worst.Sort((a, b) => b.Key.CompareTo(a.Key));

            var mat = VhPerfAudit.Materials(renderers);
            var lit = VhPerfAudit.Lights(lights, probes);
            var mem = VhPerfAudit.Memory(renderers);

            var txt = new StringBuilder();
            txt.AppendLine("Perf report: " + scene.path + "  (" + DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + ", editor estimate, no GPU)");
            txt.AppendLine("Editor memory after opening: " + VhPerfAudit.Mb(editorMem) + " Unity-allocated, " + VhPerfAudit.Mb(committed) + " committed by the process (the cap is 4.6 GB).");
            txt.AppendLine("Totals: " + renderers.Count + " renderers (" + inGroups + " in " + groups.Length + " LOD groups), " + meshes.Count + " meshes, " + mats.Count + " materials, " +
                           (totalTris / 1e6).ToString("0.00", CultureInfo.InvariantCulture) + "M triangles at full detail, " + lights.Length + " lights.");
            txt.AppendLine("Viewpoints: " + eyes.Count + " (" + flightNote + "), camera " + fov.ToString("0") + " deg, far " + far.ToString("0") + " m, shadows to " + shadowDistance.ToString("0") + " m.");
            if (eyes.Count > 0)
            {
                txt.AppendLine("In view: max " + (maxTris / 1e6).ToString("0.00", CultureInfo.InvariantCulture) + "M tris (mean " + (sumTris / (double)eyes.Count / 1e6).ToString("0.00", CultureInfo.InvariantCulture) + "M), max " +
                               maxDraws + " draws, max " + maxCasters + " shadow casters, max " + maxLights + " realtime lights. " + overViews + " of " + eyes.Count + " views over budget.");
                txt.AppendLine("Budget per view: " + Budget.Triangles / 1e6 + "M tris, " + Budget.DrawCalls + " draws, " + Budget.ShadowCasters + " casters, " + Budget.RealtimeLights + " realtime lights, " + Budget.ShadowedPointLights + " shadowed point lights.");
                long all = bandTotals[0] + bandTotals[1] + bandTotals[2] + bandTotals[3];
                if (all > 0)
                    txt.AppendLine("Triangles in view by LOD band (all views): LOD0/no LOD " + Pct(bandTotals[0], all) + ", LOD1 " + Pct(bandTotals[1], all) + ", LOD2 " + Pct(bandTotals[2], all) + ", LOD3+ " + Pct(bandTotals[3], all) + ".");
                txt.AppendLine("Heaviest views:");
                for (int i = 0; i < Math.Min(8, worst.Count); i++) txt.AppendLine("  " + worst[i].Value);
            }
            txt.Append(VhPerfAudit.Write("Audits:", mat, lit, mem));

            Directory.CreateDirectory(outDir);
            string baseName = Path.Combine(outDir, string.IsNullOrEmpty(scene.name) ? "scene" : scene.name);
            File.WriteAllText(baseName + ".txt", txt.ToString());
            var json = new StringBuilder();
            json.Append("{\n  \"scene\": ").Append(VhPerfRecorder.Q(scene.path))
                .Append(",\n  \"editorAllocatedMB\": ").Append((editorMem >> 20)).Append(", \"processCommittedMB\": ").Append(committed >> 20)
                .Append(",\n  \"renderers\": ").Append(renderers.Count).Append(", \"lodGroups\": ").Append(groups.Length).Append(", \"renderersInLodGroups\": ").Append(inGroups)
                .Append(", \"meshes\": ").Append(meshes.Count).Append(", \"materials\": ").Append(mats.Count).Append(", \"trianglesFullDetail\": ").Append(totalTris)
                .Append(",\n  \"maxTrianglesInView\": ").Append(maxTris).Append(", \"maxDrawCalls\": ").Append(maxDraws).Append(", \"maxShadowCasters\": ").Append(maxCasters)
                .Append(", \"viewsOverBudget\": ").Append(overViews)
                .Append(",\n  \"findings\": [");
            bool first = true;
            foreach (var rep in new[] { mat, lit, mem })
                foreach (var f in rep.findings) { json.Append(first ? "\n    " : ",\n    ").Append(VhPerfRecorder.Q(f)); first = false; }
            json.Append("\n  ],\n  \"views\": [\n    ").Append(string.Join(",\n    ", views)).Append("\n  ]\n}\n");
            File.WriteAllText(baseName + ".json", json.ToString());
            return Path.GetFullPath(baseName + ".txt");
        }

        static string Pct(long part, long all) => (100.0 * part / all).ToString("0", CultureInfo.InvariantCulture) + "%";

        /// <summary>The LOD a group shows from <paramref name="eye"/> at lodBias 1 (-1: culled).</summary>
        public static int SelectedLod(LODGroup g, Vector3 eye, float fov)
        {
            var lods = g.GetLODs();
            var t = g.transform;
            var s = t.lossyScale;
            float size = g.size * Mathf.Max(Mathf.Abs(s.x), Mathf.Max(Mathf.Abs(s.y), Mathf.Abs(s.z)));
            float d = Vector3.Distance(eye, t.TransformPoint(g.localReferencePoint));
            float h = size / (2f * Mathf.Max(0.01f, d) * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad));
            for (int i = 0; i < lods.Length; i++) if (h >= lods[i].screenRelativeTransitionHeight) return i;
            return -1;
        }

        /// <summary>The probe's flights for the scene, else a Look perf path, else the main camera.</summary>
        static string Viewpoints(string sceneName, List<Vector3> eyes, List<Quaternion> rots, List<string> labels)
        {
            var flights = new List<VhPerfFlight>();
            if (sceneName == DreamStreetPresenter.Scene)
            {
                var g = UnityEngine.Object.FindAnyObjectByType<DreamStreetGame>();
                var h = LarchHill.Build();
                flights.Add(VhPerfScenes.Hill1Flight(h, g != null ? g.LintRoutes() : null));
                flights.Add(VhPerfScenes.Hill2Flight(h));
            }
            else if (sceneName == DreamHousePresenter.Scene)
            {
                var g = UnityEngine.Object.FindAnyObjectByType<DreamHouseGame>();
                flights.Add(VhPerfScenes.HouseFlight(g != null ? g.houseRoot : null));
                flights.Add(VhPerfScenes.EscapeFlight(g != null ? g.escapeRoot : null));
            }
            foreach (var f in flights) f.Viewpoints(eyes, rots, labels, 12f);
            if (eyes.Count > 0)
            {
                Thin(eyes, rots, labels, 120);
                return "the probe's flights";
            }
            // A Look perf path (LookLab), found by name so this doesn't depend on the Look assembly.
            foreach (var mb in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (mb == null || mb.GetType().Name != "VhPerfPath") continue;
                var fe = mb.GetType().GetField("eyes");
                var fl = mb.GetType().GetField("looks");
                if (fe == null || fl == null) continue;
                var pe = fe.GetValue(mb) as Vector3[];
                var pl = fl.GetValue(mb) as Vector3[];
                if (pe == null || pl == null) continue;
                for (int i = 0; i < Mathf.Min(pe.Length, pl.Length); i++)
                {
                    eyes.Add(pe[i]);
                    rots.Add(Quaternion.LookRotation((pl[i] - pe[i]).sqrMagnitude > 1e-4f ? pl[i] - pe[i] : Vector3.forward));
                    labels.Add("perf path " + i);
                }
                return "the scene's VhPerfPath";
            }
            var cam = Camera.main;
            if (cam != null)
            {
                for (int k = 0; k < 8; k++)
                {
                    eyes.Add(cam.transform.position);
                    rots.Add(Quaternion.Euler(0f, k * 45f, 0f) * Quaternion.Euler(cam.transform.eulerAngles.x, cam.transform.eulerAngles.y, 0f));
                    labels.Add("main camera, turned " + k * 45 + " deg");
                }
                return sceneName == "VisitingHours" ? "the main camera only: the ward is built at runtime, run the probe" : "the main camera, turning";
            }
            return "none";
        }

        static void Thin(List<Vector3> eyes, List<Quaternion> rots, List<string> labels, int max)
        {
            if (eyes.Count <= max) return;
            var e = new List<Vector3>(); var r = new List<Quaternion>(); var l = new List<string>();
            for (int i = 0; i < max; i++)
            {
                int k = i * eyes.Count / max;
                e.Add(eyes[k]); r.Add(rots[k]); l.Add(labels[k]);
            }
            eyes.Clear(); eyes.AddRange(e);
            rots.Clear(); rots.AddRange(r);
            labels.Clear(); labels.AddRange(l);
        }
    }
}
