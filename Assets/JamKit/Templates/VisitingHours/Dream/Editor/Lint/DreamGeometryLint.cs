using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using JamKit.Movement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace JamKit.DreamEscape.EditorTools
{
    /// <summary>What <see cref="DreamGeometryLint.Run"/> looks at, and its thresholds. Defaults fit the dream levels'
    /// movement (docs/MOVEMENT.md: capsule radius 0.38 m, height 1.7 m, walkable up to 50 degrees).</summary>
    public sealed class LintOptions
    {
        static readonly MoveDef Def = new MoveDef();

        /// <summary>The area the grid checks (holes, walls beside drops, falls) cover. Null: the union of every solid
        /// collider's bounds, leaving out boundaries (anything under an object whose name has "Bound" or
        /// "Invisible wall" in it) and colliders under a <see cref="LintIgnore"/>.</summary>
        public Bounds? playableBounds;
        /// <summary>Grid cell, metres (raised automatically when the grid would pass <see cref="maxCells"/>).</summary>
        public float cell = 0.5f;
        public float playerRadius = Def.radius;
        public float playerHeight = Def.standHeight;
        /// <summary>Steepest walkable ground, degrees.</summary>
        public float slopeLimit = Def.slopeLimit;
        /// <summary>Overlaps up to this deep (metres) are contact, not butting in.</summary>
        public float tolerance = 0.04f;
        /// <summary>Renderers whose own name contains one of these (any case) are not solids: no-collider and the
        /// renderer half of butting-in skip them, and a collider whose GameObject draws only these is "invisible".</summary>
        public List<string> ignoreNames = new List<string> { "Decal", "Grass", "Leaves", "Interior", "Glass", "Smoke", "Spray", "Glow", "Card", "Halo", "Beam", "Sky" };
        /// <summary>Route polylines (world points) for not-runnable. Any MonoBehaviour in the scene with a public
        /// <c>Vector3[][] LintRoutes()</c> adds its routes too.</summary>
        public List<Vector3[]> routes = new List<Vector3[]>();
        /// <summary>Scan only these roots (and their children). Null or empty: every root of every loaded scene.</summary>
        public List<GameObject> roots;
        /// <summary>-1: read "Only Marked Walls" from the scene's MovementController (off when there is none); 0 off; 1 on.</summary>
        public int onlyMarkedWalls = -1;
        /// <summary>no-collider: a renderer counts when some part of it is this high or lower above a walkable surface.</summary>
        public float reach = 2.2f;
        /// <summary>holes: the widest gap (metres) between two walkable cells that still counts as a hole in one floor.</summary>
        public float holeMaxSpan = 2.5f;
        /// <summary>no-fall-volume: a drop must find ground within this many metres, or a DreamFallVolume.</summary>
        public float fallCatchDepth = 30f;
        /// <summary>not-runnable: wall size and how near (metres) a route or a drop it must be.</summary>
        public float wallMinLength = 3f, wallMinHeight = 2f, wallNear = 4f;
        /// <summary>not-runnable: a "drop" beside a wall is at least this deep.</summary>
        public float wallGapDrop = 1.5f;
        /// <summary>Grid size cap (cells); a bigger area gets a coarser cell and a note in the report.</summary>
        public int maxCells = 2000000;
        /// <summary>Which checks run.</summary>
        public bool buttingIn = true, noCollider = true, holes = true, notRunnable = true, noFallVolume = true;
    }

    /// <summary>One problem: the check that found it, how bad, where, what (plain words) and its numbers.</summary>
    [Serializable]
    public sealed class LintFinding
    {
        public string check;
        /// <summary>"high", "medium" or "low".</summary>
        public string severity;
        /// <summary>Hierarchy paths ("Scene:Root/Child") of the objects involved.</summary>
        public string[] paths;
        public Vector3 position;
        public string message;
        /// <summary>The main number (see each check: depth, size, area, length...) and a second one.</summary>
        public float value, value2;

        public int Rank => severity == "high" ? 0 : severity == "medium" ? 1 : 2;
    }

    [Serializable]
    public sealed class LintCount
    {
        public string check;
        public int count, high, medium, low;
    }

    /// <summary>Everything one run found, plus what it scanned.</summary>
    [Serializable]
    public sealed class LintReport
    {
        public string scene;
        public string time;
        public float seconds;
        public Vector3 boundsMin, boundsMax;
        public float cell;
        public int colliders, renderers, routes;
        public bool onlyMarkedWalls;
        public List<LintCount> counts = new List<LintCount>();
        public List<string> notes = new List<string>();
        /// <summary>Every LintIgnore in scope, with its reason.</summary>
        public List<string> ignored = new List<string>();
        /// <summary>Every finding, worst first within each check.</summary>
        public List<LintFinding> findings = new List<LintFinding>();

        public int Count(string check)
        {
            int n = 0;
            foreach (var f in findings) if (f.check == check) n++;
            return n;
        }

        public List<LintFinding> Of(string check)
        {
            var list = new List<LintFinding>();
            foreach (var f in findings) if (f.check == check) list.Add(f);
            return list;
        }

        public int Total => findings.Count;
    }

    /// <summary>
    /// The dream levels' geometry QA lint: five physical-integrity checks on the open scene(s), run by the World and
    /// House agents at every milestone. Read the report as a to-do list; each finding names the objects and the spot.
    /// <list type="number">
    /// <item><b>butting-in</b>: two solid, visible colliders of different objects overlap deeper than the tolerance
    /// (PhysX penetration; sampled for non-convex meshes and terrain), and visible renderers without a collider whose
    /// mesh vertices sit inside another object's visible collider. "One object" = the same GameObject, parent and child,
    /// the same Rigidbody, or the same group: the nearest ancestor with a <see cref="LintGroup"/> or a name ending in
    /// "[group]", else the outermost prefab instance root. A vertical overlap (something sunk into a floor) is low
    /// severity. Value: overlap depth, metres.</item>
    /// <item><b>no-collider</b>: a visible solid renderer (not ignored by name, not transparent or alpha-clipped) at
    /// least 0.15 m in some dimension, within reach of a walkable surface, whose volume is mostly in no collider: you
    /// would phase through it. Value: its largest size.</item>
    /// <item><b>holes</b>: a downward ray grid over the playable bounds. Holes: cells with nothing below (or only
    /// ground far under the lowest floor) between walkable cells at a similar height, merged into regions (value: area).
    /// Slots: a cell more than 1.2 m below walkable neighbours on both sides, narrower than the capsule (value: width).</item>
    /// <item><b>not-runnable</b>: a vertical face at least 3 m long and 2 m tall that the wallrun refuses (exactly the
    /// movement's rule: MovementSurface.runnable on the collider's GameObject, else !onlyMarkedWalls) within 4 m of a
    /// route or of a drop over 1.5 m in front of it. Box faces directly; mesh colliders by ray probes. Value: length,
    /// value2: height.</item>
    /// <item><b>no-fall-volume</b>: walkable edges whose drop finds no ground within 30 m and no trigger with a
    /// <see cref="DreamFallVolume"/> (or named "Fall volume"/"Kill") below: you can fall out of the world. Merged into
    /// regions; value: edge length.</item>
    /// </list>
    /// Batch: <c>Unity -batchmode -projectPath . -executeMethod JamKit.DreamEscape.EditorTools.DreamGeometryLint.RunFromCommandLine
    /// [-lintScene Assets/...unity]... [-lintOut dir]</c>. Exclude things with <see cref="LintIgnore"/> (with a reason).
    /// </summary>
    public static class DreamGeometryLint
    {
        public const string ButtingIn = "butting-in";
        public const string NoCollider = "no-collider";
        public const string Holes = "holes";
        public const string NotRunnable = "not-runnable";
        public const string NoFallVolume = "no-fall-volume";
        public static readonly string[] Checks = { ButtingIn, NoCollider, Holes, NotRunnable, NoFallVolume };
        public const string MenuPath = "JamKit/QA/Geometry Lint (open scene)";

        static readonly Dictionary<string, string> Described = new Dictionary<string, string>
        {
            { ButtingIn, "solids that interpenetrate (value: overlap depth, m)" },
            { NoCollider, "visible solids within reach that have no collider: you would walk through them (value: largest size, m)" },
            { Holes, "holes in walkable floors with nothing below (value: area, m2) and slots narrower than the capsule (value: width, m)" },
            { NotRunnable, "long, tall walls beside a route or a drop that the wallrun refuses (value: length, m; value2: height, m)" },
            { NoFallVolume, "walkable edges where a fall lands nowhere: no ground within 30 m and no DreamFallVolume (value: edge length, m)" },
        };

        [MenuItem(MenuPath)]
        static void Menu()
        {
            var scene = SceneManager.GetActiveScene();
            string name = string.IsNullOrEmpty(scene.name) ? "untitled" : scene.name;
            var report = Run(new LintOptions());
            report.scene = string.IsNullOrEmpty(scene.path) ? name : scene.path;
            string txt = Write(report, DefaultOutDir(), name);
            Debug.Log(Summary(report) + "\n" + txt);
            EditorUtility.RevealInFinder(txt);
        }

        /// <summary>Batch entry point: <c>-lintScene path</c> (repeatable; default: the Build Settings scenes whose
        /// names start with "Dream") and <c>-lintOut dir</c> (default out/lint/yyyyMMdd-HHmmss). Writes scene.json and
        /// scene.txt per scene, logs "JAMKIT_LINT scene check count" lines and "JAMKIT_LINT_DONE dir". Exit 0 (findings are
        /// not failures), 1 on an exception.</summary>
        public static void RunFromCommandLine()
        {
            int code = 0;
            try
            {
                var args = Environment.GetCommandLineArgs();
                var scenes = new List<string>();
                string outDir = null;
                for (int i = 0; i < args.Length - 1; i++)
                {
                    if (args[i] == "-lintScene") scenes.Add(args[i + 1]);
                    else if (args[i] == "-lintOut") outDir = args[i + 1];
                }
                if (scenes.Count == 0)
                    foreach (var s in EditorBuildSettings.scenes)
                        if (Path.GetFileNameWithoutExtension(s.path).StartsWith("Dream", StringComparison.Ordinal)) scenes.Add(s.path);
                outDir = string.IsNullOrEmpty(outDir) ? DefaultOutDir() : Path.GetFullPath(outDir);
                Directory.CreateDirectory(outDir);
                if (scenes.Count == 0) Debug.LogWarning("JAMKIT_LINT no scenes (pass -lintScene, or list Dream* scenes in Build Settings)");
                foreach (var path in scenes)
                {
                    EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    var report = Run(new LintOptions());
                    report.scene = path;
                    string name = Path.GetFileNameWithoutExtension(path);
                    Write(report, outDir, name);
                    foreach (var c in report.counts) Debug.Log("JAMKIT_LINT " + name + " " + c.check + " " + c.count);
                }
                Debug.Log("JAMKIT_LINT_DONE " + outDir);
            }
            catch (Exception e)
            {
                Debug.LogError("JAMKIT_LINT_FAIL " + e);
                code = 1;
            }
            EditorApplication.Exit(code);
        }

        /// <summary>Runs the checks on the loaded scene(s) (or <see cref="LintOptions.roots"/>).</summary>
        public static LintReport Run(LintOptions options)
        {
            var o = options ?? new LintOptions();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            Physics.SyncTransforms();
            var s = new Scan(o);
            s.Collect();
            if (o.buttingIn) s.CheckButting();
            if (o.noCollider) s.CheckNoCollider();
            if ((o.holes || o.notRunnable || o.noFallVolume) && s.BuildGrid())
            {
                if (o.holes) s.CheckHoles();
                if (o.notRunnable || o.noFallVolume) s.ScanEdges();
            }
            if (o.notRunnable) s.CheckWalls();
            var r = s.report;
            r.findings.Sort((a, b) =>
            {
                int c = Array.IndexOf(Checks, a.check).CompareTo(Array.IndexOf(Checks, b.check));
                if (c != 0) return c;
                c = a.Rank.CompareTo(b.Rank);
                return c != 0 ? c : b.value.CompareTo(a.value);
            });
            foreach (var check in Checks)
            {
                var n = new LintCount { check = check };
                foreach (var f in r.findings)
                {
                    if (f.check != check) continue;
                    n.count++;
                    if (f.Rank == 0) n.high++;
                    else if (f.Rank == 1) n.medium++;
                    else n.low++;
                }
                r.counts.Add(n);
            }
            r.time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            r.seconds = (float)watch.Elapsed.TotalSeconds;
            return r;
        }

        public static string DefaultOutDir()
        {
            string root = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(root, "out", "lint", DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));
        }

        /// <summary>Writes name.json (every finding) and name.txt (readable, grouped by check, worst first) into dir;
        /// returns the .txt path.</summary>
        public static string Write(LintReport r, string dir, string name)
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, name + ".json"), JsonUtility.ToJson(r, true));
            string txt = Path.Combine(dir, name + ".txt");
            File.WriteAllText(txt, ToText(r));
            return txt;
        }

        public static string Summary(LintReport r)
        {
            var sb = new StringBuilder("Geometry lint " + r.scene + ":");
            foreach (var c in r.counts) sb.Append(' ').Append(c.check).Append(' ').Append(c.count).Append(';');
            sb.Append(" (" + F(r.seconds, 1) + " s)");
            return sb.ToString();
        }

        public static string ToText(LintReport r)
        {
            const int MaxPerCheck = 400;
            var sb = new StringBuilder();
            sb.AppendLine("Geometry lint: " + r.scene);
            sb.AppendLine(r.time + ", " + F(r.seconds, 1) + " s; " + r.colliders + " colliders, " + r.renderers + " solid renderers, "
                + r.routes + " routes, grid cell " + F(r.cell, 2) + " m, Only Marked Walls " + (r.onlyMarkedWalls ? "on" : "off"));
            sb.AppendLine("Playable bounds " + V(r.boundsMin) + " .. " + V(r.boundsMax));
            sb.AppendLine();
            sb.AppendLine("COUNTS");
            foreach (var c in r.counts)
                sb.AppendLine("  " + c.check.PadRight(16) + c.count.ToString(CultureInfo.InvariantCulture).PadLeft(6)
                    + "   (high " + c.high + ", medium " + c.medium + ", low " + c.low + ")");
            if (r.notes.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("NOTES");
                foreach (var n in r.notes) sb.AppendLine("  " + n);
            }
            if (r.ignored.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("IGNORED (LintIgnore)");
                foreach (var n in r.ignored) sb.AppendLine("  " + n);
            }
            foreach (var check in Checks)
            {
                var list = r.Of(check);
                sb.AppendLine();
                sb.AppendLine("== " + check + " (" + list.Count + "): " + Described[check]);
                int shown = 0;
                foreach (var f in list)
                {
                    if (shown++ >= MaxPerCheck)
                    {
                        sb.AppendLine("  ... " + (list.Count - MaxPerCheck) + " more in the .json");
                        break;
                    }
                    sb.AppendLine("  [" + f.severity + "] " + F(f.value, 2) + (f.value2 != 0f ? " / " + F(f.value2, 2) : "") + "  at " + V(f.position) + "  " + f.message);
                    if (f.paths != null) foreach (var p in f.paths) sb.AppendLine("        " + p);
                }
            }
            return sb.ToString();
        }

        internal static string F(float v, int digits) => v.ToString("F" + digits, CultureInfo.InvariantCulture);
        internal static string V(Vector3 v) => "(" + F(v.x, 2) + ", " + F(v.y, 2) + ", " + F(v.z, 2) + ")";

        // =====================================================================================================

        sealed class Col
        {
            public Collider c;
            public Bounds b;
            public Transform key;
            public Rigidbody rb;
            public int kind;
            public bool visible, boundary, ignored;
        }

        const int KBox = 0, KSphere = 1, KCapsule = 2, KConvex = 3, KMesh = 4, KTerrain = 5, KOther = 6;

        /// <summary>Points in 4 m columns, for "what is near this box" queries.</summary>
        sealed class PointHash
        {
            public readonly List<Vector3> pts = new List<Vector3>();
            public readonly List<int> tags = new List<int>();
            readonly Dictionary<long, List<int>> map = new Dictionary<long, List<int>>();
            const float C = 4f;

            public void Add(Vector3 p, int tag)
            {
                long k = Key(Mathf.FloorToInt(p.x / C), Mathf.FloorToInt(p.z / C));
                if (!map.TryGetValue(k, out var l)) map[k] = l = new List<int>();
                l.Add(pts.Count);
                pts.Add(p);
                tags.Add(tag);
            }

            public void Near(Bounds b, List<int> result)
            {
                result.Clear();
                int x0 = Mathf.FloorToInt(b.min.x / C), x1 = Mathf.FloorToInt(b.max.x / C);
                int z0 = Mathf.FloorToInt(b.min.z / C), z1 = Mathf.FloorToInt(b.max.z / C);
                for (int x = x0; x <= x1; x++)
                    for (int z = z0; z <= z1; z++)
                        if (map.TryGetValue(Key(x, z), out var l))
                            foreach (int i in l)
                                if (b.Contains(pts[i])) result.Add(i);
            }
        }

        static long Key(int x, int z) => ((long)x << 32) ^ (uint)z;

        static readonly Vector3[] Axes = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };

        sealed class Scan
        {
            readonly LintOptions o;
            public readonly LintReport report = new LintReport();
            readonly List<Col> cols = new List<Col>();
            readonly List<Col> fallVolumes = new List<Col>();
            readonly List<Renderer> rends = new List<Renderer>();
            readonly List<GameObject> roots = new List<GameObject>();
            readonly Dictionary<Transform, Transform> keyCache = new Dictionary<Transform, Transform>();
            readonly Dictionary<Transform, bool> boundCache = new Dictionary<Transform, bool>();
            readonly Dictionary<Transform, string> pathCache = new Dictionary<Transform, string>();
            readonly Dictionary<Mesh, int[]> trisCache = new Dictionary<Mesh, int[]>();
            readonly Dictionary<Mesh, Vector3[]> vertsCache = new Dictionary<Mesh, Vector3[]>();
            readonly Dictionary<long, List<int>> colHash = new Dictionary<long, List<int>>();
            readonly List<int> bigCols = new List<int>();
            readonly List<int> scratch = new List<int>();
            readonly List<Vector3[]> routes = new List<Vector3[]>();
            const float HashCell = 4f;
            int mask;
            bool onlyMarked;
            Bounds play;
            bool hasPlay;
            float cosSlope;

            // The grid: per column up to L surfaces (top down) from a downward ray that restarts below each hit.
            const int L = 4;
            int nx, nz;
            float cell, x0, z0, top, bottom, lowest = float.PositiveInfinity;
            float[] gy;
            byte[] gn, gf;
            int[] gid;
            const byte FWalk = 1, FStand = 2;
            PointHash gaps;

            public Scan(LintOptions options)
            {
                o = options;
                cosSlope = Mathf.Cos(o.slopeLimit * Mathf.Deg2Rad);
            }

            // ---------------------------------------------------------------- collecting

            public void Collect()
            {
                if (o.roots != null && o.roots.Count > 0)
                {
                    foreach (var g in o.roots) if (g != null) roots.Add(g);
                }
                else
                {
                    for (int i = 0; i < SceneManager.sceneCount; i++)
                    {
                        var sc = SceneManager.GetSceneAt(i);
                        if (sc.isLoaded) roots.AddRange(sc.GetRootGameObjects());
                    }
                }
                MovementController mc = null;
                foreach (var root in roots)
                    if (mc == null) mc = root.GetComponentInChildren<MovementController>(true);
                onlyMarked = o.onlyMarkedWalls >= 0 ? o.onlyMarkedWalls == 1 : mc != null && mc.onlyMarkedWalls;
                mask = (mc != null ? mc.worldMask.value : ~0) & ~(1 << 2);
                report.onlyMarkedWalls = onlyMarked;
                if (mc == null) report.notes.Add("No MovementController in scope: the world mask is every layer but Ignore Raycast; Only Marked Walls " + (onlyMarked ? "on" : "off") + ".");

                var ignores = new HashSet<LintIgnore>();
                int offMask = 0;
                foreach (var root in roots)
                {
                    if (root == null || !root.activeInHierarchy) continue;
                    foreach (var c in root.GetComponentsInChildren<Collider>(false))
                    {
                        if (!c.enabled || c is CharacterController) continue;
                        var ig = c.GetComponentInParent<LintIgnore>(true);
                        if (ig != null) ignores.Add(ig);
                        if (c.isTrigger)
                        {
                            if (IsFallVolume(c)) fallVolumes.Add(new Col { c = c, b = c.bounds, kind = KindOf(c) });
                            continue;
                        }
                        if (((1 << c.gameObject.layer) & mask) == 0) { offMask++; continue; }
                        var col = new Col
                        {
                            c = c,
                            b = c.bounds,
                            kind = KindOf(c),
                            rb = c.attachedRigidbody,
                            boundary = IsBoundary(c.transform),
                            ignored = ig != null,
                            key = KeyOf(c.transform),
                        };
                        col.visible = !col.ignored && VisibleOn(c.gameObject);
                        cols.Add(col);
                    }
                    foreach (var r in root.GetComponentsInChildren<Renderer>(false))
                    {
                        if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
                        var ig = r.GetComponentInParent<LintIgnore>(true);
                        if (ig != null) { ignores.Add(ig); continue; }
                        if (SolidVisible(r)) rends.Add(r);
                    }
                    foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                    {
                        if (mb == null) continue;
                        var m = mb.GetType().GetMethod("LintRoutes", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                        if (m == null || m.ReturnType != typeof(Vector3[][])) continue;
                        try
                        {
                            var got = (Vector3[][])m.Invoke(mb, null);
                            if (got != null) foreach (var line in got) if (line != null && line.Length >= 2) routes.Add(line);
                        }
                        catch (Exception e) { report.notes.Add("LintRoutes() on " + PathOf(mb.transform) + " threw: " + (e.InnerException ?? e).Message); }
                    }
                }
                if (o.routes != null) foreach (var line in o.routes) if (line != null && line.Length >= 2) routes.Add(line);
                foreach (var ig in ignores) report.ignored.Add(PathOf(ig.transform) + ": " + (string.IsNullOrEmpty(ig.reason) ? "(no reason given)" : ig.reason));
                if (offMask > 0) report.notes.Add(offMask + " colliders are on layers the movement's world mask leaves out (the wallrun and ground casts never see them); not checked.");
                report.colliders = cols.Count;
                report.renderers = rends.Count;
                report.routes = routes.Count;

                if (o.playableBounds.HasValue) { play = o.playableBounds.Value; hasPlay = true; }
                else
                {
                    foreach (var c in cols)
                    {
                        if (c.boundary || c.ignored) continue;
                        if (!hasPlay) { play = c.b; hasPlay = true; }
                        else play.Encapsulate(c.b);
                    }
                }
                if (hasPlay) { report.boundsMin = play.min; report.boundsMax = play.max; }

                for (int i = 0; i < cols.Count; i++)
                {
                    var b = cols[i].b;
                    int ax = Mathf.FloorToInt(b.min.x / HashCell), bx = Mathf.FloorToInt(b.max.x / HashCell);
                    int az = Mathf.FloorToInt(b.min.z / HashCell), bz = Mathf.FloorToInt(b.max.z / HashCell);
                    if ((long)(bx - ax + 1) * (bz - az + 1) > 1024) { bigCols.Add(i); continue; }
                    for (int x = ax; x <= bx; x++)
                        for (int z = az; z <= bz; z++)
                        {
                            long k = Key(x, z);
                            if (!colHash.TryGetValue(k, out var l)) colHash[k] = l = new List<int>();
                            l.Add(i);
                        }
                }
            }

            /// <summary>Colliders whose bounds may hold p (fills <see cref="scratch"/>).</summary>
            List<int> CollidersAt(Vector3 p)
            {
                scratch.Clear();
                if (colHash.TryGetValue(Key(Mathf.FloorToInt(p.x / HashCell), Mathf.FloorToInt(p.z / HashCell)), out var l)) scratch.AddRange(l);
                scratch.AddRange(bigCols);
                return scratch;
            }

            static int KindOf(Collider c)
            {
                if (c is BoxCollider) return KBox;
                if (c is SphereCollider) return KSphere;
                if (c is CapsuleCollider) return KCapsule;
                if (c is MeshCollider mc) return mc.sharedMesh == null ? KOther : mc.convex ? KConvex : KMesh;
                if (c is TerrainCollider) return KTerrain;
                return KOther;
            }

            static bool Has(string s, string part) => s.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;

            static bool IsFallVolume(Collider c) => c.GetComponent<DreamFallVolume>() != null || Has(c.name, "fall volume") || Has(c.name, "kill");

            bool IsBoundary(Transform t)
            {
                if (t == null) return false;
                if (boundCache.TryGetValue(t, out bool v)) return v;
                v = Has(t.name, "bound") || Has(t.name, "invisible wall") || IsBoundary(t.parent);
                boundCache[t] = v;
                return v;
            }

            Transform KeyOf(Transform t)
            {
                if (keyCache.TryGetValue(t, out var k)) return k;
                k = null;
                for (var x = t; x != null; x = x.parent)
                    if (x.GetComponent<LintGroup>() != null || x.name.EndsWith("[group]", StringComparison.Ordinal)) { k = x; break; }
                if (k == null)
                {
                    var pr = PrefabUtility.GetOutermostPrefabInstanceRoot(t.gameObject);
                    k = pr != null ? pr.transform : t;
                }
                keyCache[t] = k;
                return k;
            }

            bool IgnoredName(string name)
            {
                if (o.ignoreNames == null) return false;
                foreach (var s in o.ignoreNames) if (!string.IsNullOrEmpty(s) && Has(name, s)) return true;
                return false;
            }

            static Mesh MeshOf(Renderer r)
            {
                if (r is SkinnedMeshRenderer smr) return smr.sharedMesh;
                var mf = r.GetComponent<MeshFilter>();
                return mf != null ? mf.sharedMesh : null;
            }

            bool SolidVisible(Renderer r)
            {
                if (!r.enabled || r.forceRenderingOff || r.shadowCastingMode == ShadowCastingMode.ShadowsOnly) return false;
                if (MeshOf(r) == null || IgnoredName(r.name)) return false;
                bool any = false;
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null) continue;
                    any = true;
                    if (m.renderQueue >= 2450) return false;
                    if (m.HasProperty("_Surface") && m.GetFloat("_Surface") > 0.5f) return false;
                    if (m.HasProperty("_AlphaClip") && m.GetFloat("_AlphaClip") > 0.5f) return false;
                }
                return any;
            }

            bool VisibleOn(GameObject g)
            {
                foreach (var r in g.GetComponents<Renderer>())
                    if ((r is MeshRenderer || r is SkinnedMeshRenderer) && SolidVisible(r)) return true;
                return false;
            }

            string PathOf(Transform t)
            {
                if (t == null) return "(none)";
                if (pathCache.TryGetValue(t, out var p)) return p;
                p = t.parent == null ? t.gameObject.scene.name + ":" + t.name : PathOf(t.parent) + "/" + t.name;
                pathCache[t] = p;
                return p;
            }

            static bool Related(Transform a, Transform b) => a == b || a.IsChildOf(b) || b.IsChildOf(a);

            bool Same(Col a, Col b) => a.key == b.key || (a.rb != null && a.rb == b.rb) || Related(a.c.transform, b.c.transform);

            void Add(string check, string severity, Vector3 at, string message, float value, float value2, params string[] paths)
            {
                report.findings.Add(new LintFinding { check = check, severity = severity, position = at, message = message, value = value, value2 = value2, paths = paths });
            }

            // ---------------------------------------------------------------- inside depth

            /// <summary>How deep p is inside the collider (metres; 0 or less: outside).</summary>
            float InsideDepth(Col col, Vector3 p)
            {
                var c = col.c;
                var t = c.transform;
                switch (col.kind)
                {
                    case KBox:
                    {
                        var bc = (BoxCollider)c;
                        Vector3 lp = t.InverseTransformPoint(p) - bc.center;
                        Vector3 s = t.lossyScale;
                        float dx = (bc.size.x * 0.5f - Mathf.Abs(lp.x)) * Mathf.Abs(s.x);
                        float dy = (bc.size.y * 0.5f - Mathf.Abs(lp.y)) * Mathf.Abs(s.y);
                        float dz = (bc.size.z * 0.5f - Mathf.Abs(lp.z)) * Mathf.Abs(s.z);
                        return Mathf.Min(dx, Mathf.Min(dy, dz));
                    }
                    case KSphere:
                    {
                        var sc = (SphereCollider)c;
                        Vector3 s = t.lossyScale;
                        float r = sc.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Max(Mathf.Abs(s.y), Mathf.Abs(s.z)));
                        return r - Vector3.Distance(p, t.TransformPoint(sc.center));
                    }
                    case KCapsule:
                    {
                        var cc = (CapsuleCollider)c;
                        Vector3 s = t.lossyScale;
                        s = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
                        Vector3 ax;
                        float sa, sr;
                        if (cc.direction == 0) { ax = t.right; sa = s.x; sr = Mathf.Max(s.y, s.z); }
                        else if (cc.direction == 1) { ax = t.up; sa = s.y; sr = Mathf.Max(s.x, s.z); }
                        else { ax = t.forward; sa = s.z; sr = Mathf.Max(s.x, s.y); }
                        float r = cc.radius * sr, half = Mathf.Max(0f, cc.height * 0.5f * sa - r);
                        Vector3 d = p - t.TransformPoint(cc.center);
                        float u = Mathf.Clamp(Vector3.Dot(d, ax), -half, half);
                        return r - (d - ax * u).magnitude;
                    }
                    case KConvex:
                    {
                        float outside = (c.ClosestPoint(p) - p).magnitude;
                        if (outside > 1e-4f) return -outside;
                        float len = col.b.size.magnitude + 0.2f, best = len;
                        foreach (var d in Axes)
                            if (c.Raycast(new Ray(p + d * len, -d), out var h, len)) best = Mathf.Min(best, len - h.distance);
                        return best;
                    }
                    case KMesh:
                        return MeshDepth(col, p);
                    case KTerrain:
                    {
                        var td = ((TerrainCollider)c).terrainData;
                        if (td == null) return -1f;
                        Vector3 lp = p - t.position, size = td.size;
                        if (lp.x < 0f || lp.z < 0f || lp.x > size.x || lp.z > size.z) return -1f;
                        return td.GetInterpolatedHeight(lp.x / size.x, lp.z / size.z) - lp.y;
                    }
                }
                return -1f;
            }

            /// <summary>Inside a closed non-convex mesh when the first hit in all six axis directions is a back face
            /// (from inside you see the backs of the walls). Depth: the nearest of those hits.</summary>
            float MeshDepth(Col col, Vector3 p)
            {
                var c = col.c;
                float len = col.b.size.magnitude + 0.2f, best = float.MaxValue;
                bool prev = Physics.queriesHitBackfaces;
                Physics.queriesHitBackfaces = true;
                try
                {
                    foreach (var d in Axes)
                    {
                        if (!c.Raycast(new Ray(p, d), out var h, len)) return -1f;
                        if (!BackFace((MeshCollider)c, h, d)) return -1f;
                        best = Mathf.Min(best, h.distance);
                    }
                }
                finally { Physics.queriesHitBackfaces = prev; }
                return best;
            }

            bool BackFace(MeshCollider mc, RaycastHit h, Vector3 dir)
            {
                var mesh = mc.sharedMesh;
                if (mesh != null && mesh.isReadable && h.triangleIndex >= 0)
                {
                    if (!trisCache.TryGetValue(mesh, out var tris)) { trisCache[mesh] = tris = mesh.triangles; vertsCache[mesh] = mesh.vertices; }
                    var verts = vertsCache[mesh];
                    int i = h.triangleIndex * 3;
                    if (i + 2 < tris.Length)
                    {
                        var m = mc.transform.localToWorldMatrix;
                        Vector3 a = m.MultiplyPoint3x4(verts[tris[i]]), b = m.MultiplyPoint3x4(verts[tris[i + 1]]), cc = m.MultiplyPoint3x4(verts[tris[i + 2]]);
                        Vector3 n = Vector3.Cross(b - a, cc - a);
                        Vector3 s = mc.transform.lossyScale;
                        if (s.x * s.y * s.z < 0f) n = -n;
                        return Vector3.Dot(n, dir) > 0f;
                    }
                }
                return Vector3.Dot(h.normal, dir) > 0f;
            }

            static bool Convexish(Col c) => c.kind == KBox || c.kind == KSphere || c.kind == KCapsule || c.kind == KConvex;

            // ---------------------------------------------------------------- 1. butting-in

            public void CheckButting()
            {
                float tol = o.tolerance;
                var order = new List<int>();
                for (int i = 0; i < cols.Count; i++)
                {
                    var c = cols[i];
                    if (c.visible && !c.boundary && !c.ignored && c.kind != KOther) order.Add(i);
                }
                order.Sort((a, b) => cols[a].b.min.x.CompareTo(cols[b].b.min.x));
                var active = new List<int>();
                foreach (int i in order)
                {
                    var bi = cols[i].b;
                    for (int a = active.Count - 1; a >= 0; a--)
                        if (cols[active[a]].b.max.x < bi.min.x + tol) { active[a] = active[active.Count - 1]; active.RemoveAt(active.Count - 1); }
                    foreach (int j in active)
                    {
                        var bj = cols[j].b;
                        if (Mathf.Min(bi.max.y, bj.max.y) - Mathf.Max(bi.min.y, bj.min.y) <= tol) continue;
                        if (Mathf.Min(bi.max.z, bj.max.z) - Mathf.Max(bi.min.z, bj.min.z) <= tol) continue;
                        if (Mathf.Min(bi.max.x, bj.max.x) - Mathf.Max(bi.min.x, bj.min.x) <= tol) continue;
                        if (Same(cols[i], cols[j])) continue;
                        Pair(cols[j], cols[i]);
                    }
                    active.Add(i);
                }

                // Renderers without a collider poking into another object's visible collider.
                var pts = new List<Vector3>();
                foreach (var r in rends)
                {
                    bool hasCol = false;
                    foreach (var c in r.GetComponents<Collider>()) if (c.enabled && !c.isTrigger) { hasCol = true; break; }
                    if (hasCol) continue;
                    SamplePoints(r, 64, pts);
                    var rt = r.transform;
                    var rkey = KeyOf(rt);
                    float best = tol;
                    Col hit = null;
                    Vector3 at = Vector3.zero;
                    foreach (var p in pts)
                        foreach (int idx in CollidersAt(p))
                        {
                            var col = cols[idx];
                            if (!col.visible || col.boundary || col.ignored || !col.b.Contains(p)) continue;
                            if (col.key == rkey || Related(rt, col.c.transform)) continue;
                            float d = InsideDepth(col, p);
                            if (d > best) { best = d; hit = col; at = p; }
                        }
                    if (hit == null) continue;
                    Add(ButtingIn, best > 0.15f ? "high" : "medium", at,
                        "'" + r.name + "' (no collider) pokes " + F(best, 2) + " m into '" + hit.c.name + "'", best, 0f,
                        PathOf(rt), PathOf(hit.c.transform));
                }
            }

            void Pair(Col a, Col b)
            {
                float tol = o.tolerance, depth;
                bool vertical;
                if (Convexish(a) && Convexish(b))
                {
                    var ta = a.c.transform;
                    var tb = b.c.transform;
                    if (!Physics.ComputePenetration(a.c, ta.position, ta.rotation, b.c, tb.position, tb.rotation, out var dir, out depth)) return;
                    vertical = Mathf.Abs(dir.y) > 0.85f;
                }
                else
                {
                    depth = SampledDepth(a, b, out vertical);
                }
                if (depth <= tol) return;
                var ib = Intersect(a.b, b.b);
                string sev = vertical ? "low" : depth > 0.1f ? "high" : "medium";
                string how = vertical ? " (one sunk into the other's top or bottom)" : "";
                Add(ButtingIn, sev, ib.center, "'" + a.c.name + "' and '" + b.c.name + "' overlap by " + F(depth, 2) + " m" + how, depth, 0f,
                    PathOf(a.c.transform), PathOf(b.c.transform));
            }

            static Bounds Intersect(Bounds a, Bounds b)
            {
                var min = Vector3.Max(a.min, b.min);
                var max = Vector3.Min(a.max, b.max);
                var r = new Bounds();
                r.SetMinMax(min, Vector3.Max(min, max));
                return r;
            }

            /// <summary>Penetration estimate where PhysX can't: the deepest point inside both (a grid over the bounds'
            /// intersection) is about half the overlap.</summary>
            float SampledDepth(Col a, Col b, out bool vertical)
            {
                var ib = Intersect(a.b, b.b);
                var size = ib.size;
                vertical = size.y <= Mathf.Min(size.x, size.z);
                bool bothHeavy = !Convexish(a) && !Convexish(b);
                int cap = bothHeavy ? 5 : 8;
                int nxs = Mathf.Clamp(Mathf.CeilToInt(size.x / 0.25f), 2, cap);
                int nys = Mathf.Clamp(Mathf.CeilToInt(size.y / 0.25f), 2, cap);
                int nzs = Mathf.Clamp(Mathf.CeilToInt(size.z / 0.25f), 2, cap);
                // The cheap (primitive) one first: most points fail it.
                Col first = Convexish(b) ? b : a, second = first == a ? b : a;
                float best = 0f;
                for (int i = 0; i < nxs; i++)
                    for (int j = 0; j < nys; j++)
                        for (int k = 0; k < nzs; k++)
                        {
                            var p = ib.min + Vector3.Scale(size, new Vector3((i + 0.5f) / nxs, (j + 0.5f) / nys, (k + 0.5f) / nzs));
                            float d1 = InsideDepth(first, p);
                            if (d1 <= best) continue;
                            float d2 = InsideDepth(second, p);
                            float d = Mathf.Min(d1, d2);
                            if (d > best) best = d;
                        }
                return best * 2f;
            }

            /// <summary>A mesh's vertices: read directly when it's readable, else through Unity's MeshData (the hill's
            /// generated meshes are not readable since World round 2, to save their CPU copy in a build); null if neither works.</summary>
            Vector3[] VerticesOf(Mesh mesh)
            {
                if (vertsCache.TryGetValue(mesh, out var verts)) return verts;
                verts = null;
                if (mesh.isReadable) verts = mesh.vertices;
                else
                {
                    try
                    {
                        using (var data = Mesh.AcquireReadOnlyMeshData(mesh))
                        {
                            var md = data[0];
                            if (md.vertexCount > 0)
                            {
                                var arr = new Unity.Collections.NativeArray<Vector3>(md.vertexCount, Unity.Collections.Allocator.Temp);
                                md.GetVertices(arr);
                                verts = arr.ToArray();
                                arr.Dispose();
                            }
                        }
                    }
                    catch (Exception) { verts = null; }
                }
                vertsCache[mesh] = verts;
                return verts;
            }

            /// <summary>Up to max points spread over the renderer's mesh vertices (world), or its bounds' corners and face
            /// centres when they can't be read (none at all for a big mesh: a merged chunk's bounds say nothing).</summary>
            void SamplePoints(Renderer r, int max, List<Vector3> pts)
            {
                pts.Clear();
                var mesh = MeshOf(r);
                var m = r.transform.localToWorldMatrix;
                var verts = mesh != null && mesh.vertexCount > 0 ? VerticesOf(mesh) : null;
                if (verts != null && verts.Length > 0)
                {
                    int step = Mathf.Max(1, verts.Length / max);
                    for (int i = 0; i < verts.Length && pts.Count < max; i += step) pts.Add(m.MultiplyPoint3x4(verts[i]));
                    return;
                }
                if (mesh != null && mesh.bounds.size.magnitude > 6f) return;
                var lb = mesh != null ? mesh.bounds : new Bounds(r.transform.InverseTransformPoint(r.bounds.center), Vector3.one * 0.01f);
                var c = lb.center;
                var e = lb.extents;
                for (int i = 0; i < 8; i++)
                    pts.Add(m.MultiplyPoint3x4(c + Vector3.Scale(e, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
                foreach (var d in Axes) pts.Add(m.MultiplyPoint3x4(c + Vector3.Scale(e, d)));
            }

            // ---------------------------------------------------------------- 2. no-collider

            public void CheckNoCollider()
            {
                var pts = new List<Vector3>(7);
                foreach (var r in rends)
                {
                    var wb = r.bounds;
                    var size = wb.size;
                    if (size.x < 0.15f && size.y < 0.15f && size.z < 0.15f) continue;
                    if (hasPlay && !play.Intersects(wb)) continue;
                    if (!InReach(wb)) continue;
                    // Centre and the six face centres pulled 10% in, on the mesh's own (oriented) box.
                    pts.Clear();
                    var mesh = MeshOf(r);
                    if (r is MeshRenderer && mesh != null)
                    {
                        var m = r.transform.localToWorldMatrix;
                        var lb = mesh.bounds;
                        pts.Add(m.MultiplyPoint3x4(lb.center));
                        foreach (var d in Axes) pts.Add(m.MultiplyPoint3x4(lb.center + Vector3.Scale(lb.extents * 0.9f, d)));
                    }
                    else
                    {
                        pts.Add(wb.center);
                        foreach (var d in Axes) pts.Add(wb.center + Vector3.Scale(wb.extents * 0.9f, d));
                    }
                    int open = 0;
                    foreach (var p in pts) if (!Covered(p)) open++;
                    if (open * 2 <= pts.Count) continue;
                    float big = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
                    string sev = big >= 1f && size.y >= 0.5f ? "high" : big >= 0.5f ? "medium" : "low";
                    Add(NoCollider, sev, wb.center, "'" + r.name + "' (" + F(size.x, 1) + " x " + F(size.y, 1) + " x " + F(size.z, 1)
                        + " m) is visible within reach but " + open + "/" + pts.Count + " of its sample points are in no collider: you walk through it",
                        big, 0f, PathOf(r.transform));
                }
            }

            bool Covered(Vector3 p)
            {
                if (Physics.CheckSphere(p, 0.05f, mask, QueryTriggerInteraction.Ignore)) return true;
                foreach (int idx in CollidersAt(p))
                {
                    var col = cols[idx];
                    if ((col.kind == KMesh || col.kind == KTerrain) && col.b.Contains(p) && InsideDepth(col, p) > -0.05f) return true;
                }
                return false;
            }

            /// <summary>Some part of the box is 0..reach above a walkable surface under or right beside it.</summary>
            bool InReach(Bounds wb)
            {
                var c = wb.center;
                var e = wb.extents;
                float side = o.playerRadius + 0.3f;
                float lo = wb.min.y - o.reach - 0.05f, hi = wb.max.y + 0.05f;
                for (int i = 0; i < 9; i++)
                {
                    float x = c.x, z = c.z;
                    if (i >= 1 && i <= 4) { x += e.x * 0.9f * ((i & 1) == 0 ? -1 : 1); z += e.z * 0.9f * (i <= 2 ? -1 : 1); }
                    else if (i == 5) x += e.x + side;
                    else if (i == 6) x -= e.x + side;
                    else if (i == 7) z += e.z + side;
                    else if (i == 8) z -= e.z + side;
                    var hits = Physics.RaycastAll(new Vector3(x, hi, z), Vector3.down, hi - lo, mask, QueryTriggerInteraction.Ignore);
                    foreach (var h in hits)
                        if (h.normal.y >= cosSlope && h.point.y >= lo && h.point.y <= hi) return true;
                }
                return false;
            }

            // ---------------------------------------------------------------- the grid

            float X(int i) => x0 + (i + 0.5f) * cell;
            float Z(int j) => z0 + (j + 0.5f) * cell;
            int Idx(int i, int j) => j * nx + i;

            public bool BuildGrid()
            {
                if (!hasPlay) { report.notes.Add("Nothing solid in scope: no grid checks."); return false; }
                cell = Mathf.Max(0.1f, o.cell);
                float sx = Mathf.Max(play.size.x, cell), sz = Mathf.Max(play.size.z, cell);
                long cells = (long)(Mathf.CeilToInt(sx / cell) + 2) * (Mathf.CeilToInt(sz / cell) + 2);
                if (cells > o.maxCells)
                {
                    float c2 = Mathf.Sqrt(sx * sz / o.maxCells);
                    c2 = Mathf.Ceil(c2 * 20f) / 20f;
                    report.notes.Add("The playable area needs " + cells + " cells at " + F(cell, 2) + " m; the grid uses " + F(c2, 2) + " m cells (pass a smaller playableBounds for detail).");
                    cell = c2;
                }
                report.cell = cell;
                nx = Mathf.CeilToInt(sx / cell) + 2;
                nz = Mathf.CeilToInt(sz / cell) + 2;
                x0 = play.min.x - cell;
                z0 = play.min.z - cell;
                top = play.max.y + 1f;
                bottom = play.min.y - o.fallCatchDepth - 5f;
                int n = nx * nz;
                gy = new float[n * L];
                gn = new byte[n];
                gf = new byte[n * L];
                gid = new int[n * L];
                for (int j = 0; j < nz; j++)
                    for (int i = 0; i < nx; i++)
                    {
                        int ci = Idx(i, j);
                        var origin = new Vector3(X(i), top, Z(j));
                        int k = 0;
                        while (k < L && origin.y > bottom
                            && Physics.Raycast(origin, Vector3.down, out var h, origin.y - bottom, mask, QueryTriggerInteraction.Ignore))
                        {
                            int s = ci * L + k;
                            gy[s] = h.point.y;
                            gid[s] = h.colliderInstanceID;
                            byte f = 0;
                            if (h.normal.y >= cosSlope && !IsBoundary(h.collider.transform))
                            {
                                f |= FWalk;
                                if (k == 0 || !Physics.CheckSphere(h.point + Vector3.up * 0.3f, 0.12f, mask, QueryTriggerInteraction.Ignore)) f |= FStand;
                            }
                            gf[s] = f;
                            if ((f & FStand) != 0 && h.point.y < lowest) lowest = h.point.y;
                            k++;
                            origin.y = h.point.y - 0.02f;
                        }
                        gn[ci] = (byte)k;
                    }
                return true;
            }

            bool Stand(int ci, int k) => (gf[ci * L + k] & FStand) != 0;

            bool AnyIn(int ci, float lo, float hi)
            {
                for (int k = 0; k < gn[ci]; k++)
                {
                    float y = gy[ci * L + k];
                    if (y >= lo && y <= hi) return true;
                }
                return false;
            }

            bool Void(int ci)
            {
                if (gn[ci] == 0) return true;
                return !float.IsPositiveInfinity(lowest) && gy[ci * L] < lowest - 3f;
            }

            /// <summary>How far down from height h you fall stepping into column (i, j): 0 when there is ground near h or
            /// a solid at h (a wall), +infinity when nothing is below.</summary>
            float Drop(int i, int j, float h)
            {
                int ci = Idx(i, j);
                if (AnyIn(ci, h - 0.5f, h + o.playerHeight)) return 0f;
                if (Physics.CheckSphere(new Vector3(X(i), h + 0.6f, Z(j)), 0.15f, mask, QueryTriggerInteraction.Ignore)) return 0f;
                float best = float.NegativeInfinity;
                for (int k = 0; k < gn[ci]; k++)
                {
                    float y = gy[ci * L + k];
                    if (y < h - 0.5f && y > best) best = y;
                }
                return float.IsNegativeInfinity(best) ? float.PositiveInfinity : h - best;
            }

            /// <summary>A fall volume below (x, z) from height h down.</summary>
            bool FallCaught(float x, float z, float h)
            {
                foreach (var fv in fallVolumes)
                {
                    var b = fv.b;
                    if (x < b.min.x || x > b.max.x || z < b.min.z || z > b.max.z) continue;
                    float hi = Mathf.Min(b.max.y, h), lo = b.min.y;
                    if (hi < lo) continue;
                    for (int s = 0; s < 8; s++)
                        if (InsideDepth(fv, new Vector3(x, Mathf.Lerp(hi, lo, s / 7f), z)) >= -0.01f) return true;
                }
                return false;
            }

            static readonly int[] DI = { 1, -1, 0, 0, 1, 1, -1, -1 };
            static readonly int[] DJ = { 0, 0, 1, -1, 1, -1, 1, -1 };

            // ---------------------------------------------------------------- 3. holes

            public void CheckHoles()
            {
                int n = nx * nz;
                var hole = new bool[n];
                var holeY = new float[n];
                int span = Mathf.Max(1, Mathf.CeilToInt(o.holeMaxSpan / cell));
                for (int j = 1; j < nz - 1; j++)
                    for (int i = 1; i < nx - 1; i++)
                    {
                        int ci = Idx(i, j);
                        if (!Void(ci)) continue;
                        if (HoleAlong(i, j, 1, 0, span, out float y) || HoleAlong(i, j, 0, 1, span, out y)) { hole[ci] = true; holeY[ci] = y; }
                    }
                var seen = new bool[n];
                var stack = new Stack<int>();
                for (int start = 0; start < n; start++)
                {
                    if (!hole[start] || seen[start]) continue;
                    int count = 0, imin = int.MaxValue, imax = int.MinValue, jmin = int.MaxValue, jmax = int.MinValue;
                    float sy = 0f;
                    stack.Push(start);
                    seen[start] = true;
                    while (stack.Count > 0)
                    {
                        int ci = stack.Pop();
                        int i = ci % nx, j = ci / nx;
                        count++;
                        sy += holeY[ci];
                        imin = Mathf.Min(imin, i); imax = Mathf.Max(imax, i);
                        jmin = Mathf.Min(jmin, j); jmax = Mathf.Max(jmax, j);
                        for (int d = 0; d < 4; d++)
                        {
                            int ii = i + DI[d], jj = j + DJ[d];
                            if (ii < 0 || jj < 0 || ii >= nx || jj >= nz) continue;
                            int ni = Idx(ii, jj);
                            if (hole[ni] && !seen[ni]) { seen[ni] = true; stack.Push(ni); }
                        }
                    }
                    float y = sy / count;
                    var at = new Vector3((X(imin) + X(imax)) * 0.5f, y, (Z(jmin) + Z(jmax)) * 0.5f);
                    float w = (imax - imin + 1) * cell, d2 = (jmax - jmin + 1) * cell, area = count * cell * cell;
                    bool caught = FallCaught(at.x, at.z, y);
                    Add(Holes, caught ? "medium" : "high", at, "hole about " + F(w, 1) + " x " + F(d2, 1) + " m (" + F(area, 2) + " m2) in a walkable floor at y "
                        + F(y, 2) + (caught ? ": a fall volume catches it" : ": nothing below, the capsule drops through"), area, 0f);
                }
                Slots();
            }

            bool HoleAlong(int i, int j, int dx, int dz, int span, out float y)
            {
                y = 0f;
                int a = -1, b = -1;
                for (int s = 1; s <= span; s++)
                {
                    int ii = i - s * dx, jj = j - s * dz;
                    if (ii < 0 || jj < 0) break;
                    int ci = Idx(ii, jj);
                    if (!Void(ci)) { a = ci; break; }
                }
                if (a < 0) return false;
                for (int s = 1; s <= span; s++)
                {
                    int ii = i + s * dx, jj = j + s * dz;
                    if (ii >= nx || jj >= nz) break;
                    int ci = Idx(ii, jj);
                    if (!Void(ci)) { b = ci; break; }
                }
                if (b < 0) return false;
                for (int ka = 0; ka < gn[a]; ka++)
                {
                    if (!Stand(a, ka)) continue;
                    for (int kb = 0; kb < gn[b]; kb++)
                    {
                        if (!Stand(b, kb)) continue;
                        float ya = gy[a * L + ka], yb = gy[b * L + kb];
                        if (Mathf.Abs(ya - yb) <= 0.6f) { y = (ya + yb) * 0.5f; return true; }
                    }
                }
                return false;
            }

            /// <summary>Narrow slots: a cell well below walkable cells on both sides, narrower than the capsule.</summary>
            void Slots()
            {
                int n = nx * nz;
                var slot = new bool[n];
                var slotW = new float[n];
                var slotY = new float[n];
                float diameter = o.playerRadius * 2f;
                int reachCells = Mathf.Max(1, Mathf.CeilToInt(diameter / cell));
                for (int j = 1; j < nz - 1; j++)
                    for (int i = 1; i < nx - 1; i++)
                    {
                        int ci = Idx(i, j);
                        if (Void(ci)) continue;
                        for (int axis = 0; axis < 2 && !slot[ci]; axis++)
                        {
                            int dx = axis == 0 ? 1 : 0, dz = 1 - dx;
                            int li = Idx(i - dx, j - dz);
                            for (int kl = 0; kl < gn[li] && !slot[ci]; kl++)
                            {
                                if (!Stand(li, kl)) continue;
                                float hl = gy[li * L + kl];
                                if (AnyIn(ci, hl - 1.2f, hl + 0.6f)) continue;
                                float hr = float.NaN;
                                for (int s = 1; s <= reachCells; s++)
                                {
                                    int ii = i + s * dx, jj = j + s * dz;
                                    if (ii >= nx || jj >= nz) break;
                                    int ri = Idx(ii, jj);
                                    if (!AnyIn(ri, hl - 1.2f, hl + 0.6f)) continue;
                                    for (int kr = 0; kr < gn[ri]; kr++)
                                    {
                                        float y = gy[ri * L + kr];
                                        if (Stand(ri, kr) && Mathf.Abs(y - hl) <= 0.6f) { hr = y; break; }
                                    }
                                    break;
                                }
                                if (float.IsNaN(hr)) continue;
                                float h = Mathf.Min(hl, hr);
                                var q = new Vector3(X(i), h - 0.3f, Z(j));
                                if (Physics.CheckSphere(q, 0.05f, mask, QueryTriggerInteraction.Ignore)) continue;
                                var dir = new Vector3(dx, 0f, dz);
                                float reach = (reachCells + 2) * cell;
                                if (!Physics.Raycast(q, dir, out var ha, reach, mask, QueryTriggerInteraction.Ignore)) continue;
                                if (!Physics.Raycast(q, -dir, out var hb, reach, mask, QueryTriggerInteraction.Ignore)) continue;
                                float w = ha.distance + hb.distance;
                                if (w < diameter && w > 0.02f) { slot[ci] = true; slotW[ci] = w; slotY[ci] = h; }
                            }
                        }
                    }
                var seen = new bool[n];
                var stack = new Stack<int>();
                for (int start = 0; start < n; start++)
                {
                    if (!slot[start] || seen[start]) continue;
                    int count = 0;
                    float sx = 0f, sz = 0f, sy = 0f, minW = float.MaxValue;
                    stack.Push(start);
                    seen[start] = true;
                    while (stack.Count > 0)
                    {
                        int ci = stack.Pop();
                        int i = ci % nx, j = ci / nx;
                        count++;
                        sx += X(i); sz += Z(j); sy += slotY[ci];
                        minW = Mathf.Min(minW, slotW[ci]);
                        for (int d = 0; d < 8; d++)
                        {
                            int ii = i + DI[d], jj = j + DJ[d];
                            if (ii < 0 || jj < 0 || ii >= nx || jj >= nz) continue;
                            int ni = Idx(ii, jj);
                            if (slot[ni] && !seen[ni] && Mathf.Abs(slotY[ni] - slotY[ci]) < 1f) { seen[ni] = true; stack.Push(ni); }
                        }
                    }
                    var at = new Vector3(sx / count, sy / count, sz / count);
                    Add(Holes, "medium", at, "narrow slot " + F(minW, 2) + " m wide (the capsule is " + F(diameter, 2) + " m), over 1.2 m deep, between walkable surfaces at y "
                        + F(at.y, 2) + ", about " + F(count * cell, 1) + " m long: feet catch in it", minW, count * cell);
                }
            }

            // ---------------------------------------------------------------- edges: drops beside walls, falls out of the world

            public void ScanEdges()
            {
                gaps = new PointHash();
                int n = nx * nz;
                var edge = new bool[n * L];
                var edgeVoid = new bool[n * L];
                for (int j = 1; j < nz - 1; j++)
                    for (int i = 1; i < nx - 1; i++)
                    {
                        int ci = Idx(i, j);
                        for (int k = 0; k < gn[ci]; k++)
                        {
                            if (!Stand(ci, k)) continue;
                            float h = gy[ci * L + k];
                            bool gapAdded = false;
                            for (int d = 0; d < 4; d++)
                            {
                                int ii = i + DI[d], jj = j + DJ[d];
                                float drop = Drop(ii, jj, h);
                                if (drop <= o.wallGapDrop) continue;
                                if (!gapAdded) { gaps.Add(new Vector3(X(i), h, Z(j)), gid[ci * L + k]); gapAdded = true; }
                                if (o.noFallVolume && drop > o.fallCatchDepth && !edge[ci * L + k] && !FallCaught(X(ii), Z(jj), h))
                                {
                                    edge[ci * L + k] = true;
                                    edgeVoid[ci * L + k] = float.IsPositiveInfinity(drop);
                                }
                            }
                        }
                    }
                if (!o.noFallVolume) return;
                var seen = new bool[n * L];
                var stack = new Stack<int>();
                for (int start = 0; start < n * L; start++)
                {
                    if (!edge[start] || seen[start]) continue;
                    int count = 0;
                    bool anyVoid = false;
                    float sx = 0f, sy = 0f, sz = 0f;
                    stack.Push(start);
                    seen[start] = true;
                    while (stack.Count > 0)
                    {
                        int s = stack.Pop();
                        int ci = s / L, i = ci % nx, j = ci / nx;
                        float y = gy[s];
                        count++;
                        sx += X(i); sy += y; sz += Z(j);
                        anyVoid |= edgeVoid[s];
                        for (int d = 0; d < 8; d++)
                        {
                            int ii = i + DI[d], jj = j + DJ[d];
                            if (ii < 0 || jj < 0 || ii >= nx || jj >= nz) continue;
                            int ni = Idx(ii, jj);
                            for (int k = 0; k < gn[ni]; k++)
                            {
                                int t = ni * L + k;
                                if (edge[t] && !seen[t] && Mathf.Abs(gy[t] - y) <= 1f) { seen[t] = true; stack.Push(t); }
                            }
                        }
                    }
                    var at = new Vector3(sx / count, sy / count, sz / count);
                    Add(NoFallVolume, "high", at, "edge of a walkable surface at y " + F(at.y, 2) + ", about " + F(count * cell, 1) + " m of it: falling off lands "
                        + (anyVoid ? "nowhere (nothing below)" : "no ground within " + F(o.fallCatchDepth, 0) + " m") + " and no DreamFallVolume catches it",
                        count * cell, 0f);
                }
            }

            // ---------------------------------------------------------------- 4. not-runnable

            bool Runnable(Collider c)
            {
                if (c.TryGetComponent<MovementSurface>(out var s)) return s.runnable;
                return !onlyMarked;
            }

            public void CheckWalls()
            {
                var routePts = new PointHash();
                for (int r = 0; r < routes.Count; r++)
                {
                    var line = routes[r];
                    for (int i = 0; i < line.Length - 1; i++)
                    {
                        float len = Vector3.Distance(line[i], line[i + 1]);
                        int steps = Mathf.Max(1, Mathf.CeilToInt(len / 0.5f));
                        for (int s = 0; s < steps; s++) routePts.Add(Vector3.Lerp(line[i], line[i + 1], s / (float)steps), r);
                    }
                    routePts.Add(line[line.Length - 1], r);
                }
                if (routePts.pts.Count == 0 && (gaps == null || gaps.pts.Count == 0))
                {
                    report.notes.Add("not-runnable: no routes and no drops found, so no wall was near anything.");
                    return;
                }
                var near = new List<int>();
                var faces = new List<Face>();
                foreach (var col in cols)
                {
                    if (col.boundary || col.ignored || Runnable(col.c)) continue;
                    var b = col.b;
                    if (b.size.y < o.wallMinHeight || Mathf.Max(b.size.x, b.size.z) < o.wallMinLength * 0.7f) continue;
                    var nb = b;
                    nb.Expand(o.wallNear * 2f);
                    bool any = false;
                    routePts.Near(nb, near);
                    any = near.Count > 0;
                    if (!any && gaps != null) { gaps.Near(nb, near); any = near.Count > 0; }
                    if (!any) continue;
                    faces.Clear();
                    if (col.kind == KBox) BoxFaces(col, faces);
                    else if (col.kind == KMesh || col.kind == KConvex) ProbeFaces(col, faces);
                    else continue;
                    foreach (var f in faces) Wall(col, f, routePts, near);
                }
            }

            struct Face
            {
                public Vector3 c, n, e1, e2;
                public float length, height;
            }

            void BoxFaces(Col col, List<Face> faces)
            {
                var bc = (BoxCollider)col.c;
                var t = bc.transform;
                Vector3 ctr = t.TransformPoint(bc.center);
                Vector3 ax = t.TransformVector(new Vector3(bc.size.x * 0.5f, 0f, 0f));
                Vector3 ay = t.TransformVector(new Vector3(0f, bc.size.y * 0.5f, 0f));
                Vector3 az = t.TransformVector(new Vector3(0f, 0f, bc.size.z * 0.5f));
                for (int a = 0; a < 3; a++)
                {
                    Vector3 h = a == 0 ? ax : a == 1 ? ay : az;
                    Vector3 e1 = a == 0 ? ay : ax, e2 = a == 2 ? ay : az;
                    if (h.sqrMagnitude < 1e-8f) continue;
                    for (int sgn = -1; sgn <= 1; sgn += 2)
                    {
                        Vector3 n = h.normalized * sgn;
                        if (Mathf.Abs(n.y) >= 0.25f) continue;
                        var f = new Face { c = ctr + h * sgn, n = n, e1 = e1, e2 = e2 };
                        Vector3 tan = Vector3.Cross(Vector3.up, new Vector3(n.x, 0f, n.z).normalized);
                        f.height = 2f * (Mathf.Abs(e1.y) + Mathf.Abs(e2.y));
                        f.length = 2f * (Mathf.Abs(Vector3.Dot(e1, tan)) + Mathf.Abs(Vector3.Dot(e2, tan)));
                        faces.Add(f);
                    }
                }
            }

            sealed class Cluster
            {
                public float nx, nz, off, amin = float.MaxValue, amax = float.MinValue, ymin = float.MaxValue, ymax = float.MinValue;
                public int count;
            }

            /// <summary>Vertical faces of a mesh collider found by horizontal rays from 8 directions, clustered by
            /// facing (15 degree bins) and plane offset (0.5 m).</summary>
            void ProbeFaces(Col col, List<Face> faces)
            {
                var c = col.c;
                var b = col.b;
                const float StepA = 1f, StepY = 0.75f;
                var clusters = new Dictionary<long, Cluster>();
                for (int di = 0; di < 8; di++)
                {
                    float ang = di * 45f * Mathf.Deg2Rad;
                    var d = new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang));
                    var t = new Vector3(d.z, 0f, -d.x);
                    float et = Mathf.Abs(t.x) * b.extents.x + Mathf.Abs(t.z) * b.extents.z;
                    float ed = Mathf.Abs(d.x) * b.extents.x + Mathf.Abs(d.z) * b.extents.z;
                    int na = Mathf.Min(48, Mathf.Max(1, Mathf.FloorToInt(2f * et / StepA)));
                    int ny = Mathf.Min(20, Mathf.Max(1, Mathf.FloorToInt((b.size.y - 0.4f) / StepY)));
                    for (int ia = 0; ia < na; ia++)
                    {
                        float sa = -et + (ia + 0.5f) * (2f * et / na);
                        for (int iy = 0; iy < ny; iy++)
                        {
                            float y = b.min.y + 0.3f + (iy + 0.5f) * ((b.size.y - 0.4f) / ny);
                            var origin = b.center + t * sa - d * (ed + 1f);
                            origin.y = y;
                            if (!c.Raycast(new Ray(origin, d), out var h, 2f * ed + 2f)) continue;
                            var nrm = h.normal;
                            if (Mathf.Abs(nrm.y) >= 0.25f || Vector3.Dot(nrm, -d) < 0.3f) continue;
                            float a = Mathf.Atan2(nrm.x, nrm.z) * Mathf.Rad2Deg;
                            int bin = ((Mathf.RoundToInt(a / 15f) % 24) + 24) % 24;
                            float ba = bin * 15f * Mathf.Deg2Rad;
                            var bn = new Vector3(Mathf.Sin(ba), 0f, Mathf.Cos(ba));
                            var bt = new Vector3(bn.z, 0f, -bn.x);
                            float off = Vector3.Dot(bn, h.point);
                            long key = ((long)bin << 32) ^ (uint)Mathf.RoundToInt(off / 0.5f);
                            if (!clusters.TryGetValue(key, out var cl)) clusters[key] = cl = new Cluster();
                            cl.count++;
                            cl.nx += bn.x; cl.nz += bn.z; cl.off += off;
                            float along = Vector3.Dot(bt, h.point);
                            cl.amin = Mathf.Min(cl.amin, along); cl.amax = Mathf.Max(cl.amax, along);
                            cl.ymin = Mathf.Min(cl.ymin, h.point.y); cl.ymax = Mathf.Max(cl.ymax, h.point.y);
                        }
                    }
                }
                foreach (var cl in clusters.Values)
                {
                    if (cl.count < 4) continue;
                    var n = new Vector3(cl.nx, 0f, cl.nz).normalized;
                    var t = new Vector3(n.z, 0f, -n.x);
                    float len = cl.amax - cl.amin + StepA, hgt = cl.ymax - cl.ymin + StepY;
                    // The bin's own axes were used for along/offset; with n the bin direction they agree.
                    float off = cl.off / cl.count, amid = (cl.amin + cl.amax) * 0.5f, ymid = (cl.ymin + cl.ymax) * 0.5f;
                    var centre = n * off + t * amid;
                    centre.y = ymid;
                    faces.Add(new Face { c = centre, n = n, e1 = t * (len * 0.5f), e2 = Vector3.up * (hgt * 0.5f), length = len, height = hgt });
                }
            }

            static float RectDistance(Face f, Vector3 p)
            {
                Vector3 d = p - f.c;
                float l1 = f.e1.magnitude, l2 = f.e2.magnitude;
                Vector3 u1 = l1 > 1e-6f ? f.e1 / l1 : Vector3.zero, u2 = l2 > 1e-6f ? f.e2 / l2 : Vector3.zero;
                var q = f.c + u1 * Mathf.Clamp(Vector3.Dot(d, u1), -l1, l1) + u2 * Mathf.Clamp(Vector3.Dot(d, u2), -l2, l2);
                return Vector3.Distance(p, q);
            }

            void Wall(Col col, Face f, PointHash routePts, List<int> near)
            {
                if (f.length < o.wallMinLength || f.height < o.wallMinHeight) return;
                // Buried: most of the space just in front of the face is solid (it backs onto another block).
                int covered = 0;
                for (int a = -1; a <= 1; a++)
                    for (int b = -1; b <= 1; b++)
                        if (Physics.CheckSphere(f.c + f.e1 * (0.6f * a) + f.e2 * (0.5f * b) + f.n * 0.3f, 0.1f, mask, QueryTriggerInteraction.Ignore)) covered++;
                if (covered >= 7) return;
                var box = new Bounds(f.c, Vector3.zero);
                box.Encapsulate(f.c + f.e1 + f.e2);
                box.Encapsulate(f.c - f.e1 - f.e2);
                box.Encapsulate(f.c + f.e1 - f.e2);
                box.Encapsulate(f.c - f.e1 + f.e2);
                box.Expand(o.wallNear * 2f);
                string why = null;
                bool byRoute = false;
                routePts.Near(box, near);
                float best = o.wallNear;
                foreach (int i in near)
                {
                    float d = RectDistance(f, routePts.pts[i]);
                    if (d <= best) { best = d; why = "within " + F(d, 1) + " m of route " + routePts.tags[i]; byRoute = true; }
                }
                if (why == null && gaps != null)
                {
                    int id = col.c.GetInstanceID();
                    gaps.Near(box, near);
                    foreach (int i in near)
                    {
                        var p = gaps.pts[i];
                        if (gaps.tags[i] == id || Vector3.Dot(p - f.c, f.n) <= 0.05f) continue;
                        float d = RectDistance(f, p);
                        if (d <= best) { best = d; why = F(d, 1) + " m from a drop of over " + F(o.wallGapDrop, 1) + " m at " + V(p); }
                    }
                }
                if (why == null) return;
                string rule;
                if (col.c.TryGetComponent<MovementSurface>(out _)) rule = "its MovementSurface says runnable = false";
                else
                {
                    rule = "it has no MovementSurface and the controller's Only Marked Walls is on";
                    var parent = col.c.transform.parent;
                    if (parent != null && parent.GetComponentInParent<MovementSurface>(true) != null) rule += " (a MovementSurface on a parent does not count)";
                }
                Add(NotRunnable, byRoute ? "high" : "medium", f.c, "wall " + F(f.length, 1) + " m long x " + F(f.height, 1) + " m tall facing " + V(f.n)
                    + " can't be wall-run (" + rule + "); " + why, f.length, f.height, PathOf(col.c.transform));
            }
        }
    }
}
