using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace JamKit.Look.EditorTools
{
    /// <summary>
    /// Closes the world: a ring of terrain from the edge of the play area out to 2-3 km, its near edge continuing the scene's
    /// own ground, rolling into farmland hills and wooded ridges at the rim; the atmosphere's far haze dissolves it into the
    /// sky.
    /// <para><b>Pass 3</b> (judge round 2: "polka-dot backdrop": the tree masses read as boulders or hay bales scattered
    /// evenly over lime hills; "a hard seam where terrain meets the saturated backdrop"; "the road stops in a field"):</para>
    /// <list type="bullet">
    /// <item>The ground is a patchwork of fields (VH_FarFields: pasture, hay, stubble, crops, a little ploughed earth, 90-260 m
    /// fields in 250-450 m blocks, 2400 m a repeat, turned off the street's axis), on the FarLand shader; its colour starts as
    /// the scene's own lawn at the ring's inner edge and blends into the fields over ~250 m (no seam).</item>
    /// <item>Hedgerows and tree lines as continuous 3D strips along the field edges (the same layout as the painted ones,
    /// VH_FarFields.json), knobbly, with taller trees in them; woods as a lumpy canopy over whole fields; forest along the
    /// rim's ridges. All dark olive, all hazed by distance.</item>
    /// <item>Roads carry on into the ring (<see cref="Options.roads"/>), draped on the ring's own surface.</item>
    /// <item>Fewer stands of real (LOD) trees in the near part of the ring, kept off the roads.</item>
    /// </list>
    /// <code>
    /// VhLookBackdrop.Build(sceneRoot, new VhLookBackdrop.Options
    /// {
    ///     center = new Vector3(0f, 0f, 150f), innerRadius = 420f, outerRadius = 2800f,
    ///     nearHeight = MyGroundHeight,                       // (x, z) => ground height; continued outward, then blended away
    ///     hole = new Rect(-340f, -90f, 680f, 740f),           // the scene's own ground: the ring tucks just under it there
    ///     roads = { new VhLookBackdrop.FarRoad { points = new[] { new Vector2(335f, 0f), new Vector2(900f, -60f) }, startY = 0f } },
    /// });
    /// </code>
    /// Everything is under one "Backdrop (backdrop)" root (the probe bake skips "(backdrop)" roots). No colliders, no shadows.
    /// </summary>
    public static class VhLookBackdrop
    {
        /// <summary>A road leaving the scene into the ring: a polyline in world xz from the scene's road end outward.</summary>
        public sealed class FarRoad
        {
            public Vector2[] points;
            public float width = 8f;
            /// <summary>The scene road's surface height at the first point; the ribbon eases from it onto the ring's ground.</summary>
            public float startY;
        }

        public sealed class Options
        {
            public Vector3 center = Vector3.zero;
            /// <summary>Where the ring starts (it may overlap the scene's ground: see <see cref="hole"/>).</summary>
            public float innerRadius = 400f;
            public float outerRadius = 2800f;
            /// <summary>The scene's ground height at (x, z), continued outward past the scene's edge. Null = center.y.</summary>
            public Func<float, float, float> nearHeight;
            /// <summary>Distance beyond the inner radius over which the scene's ground gives way to the backdrop's hills.</summary>
            public float blendDistance = 600f;
            /// <summary>The scene's own ground (x/z): inside it the ring sits 0.15 m under <see cref="nearHeight"/>, hidden.</summary>
            public Rect? hole;
            /// <summary>Rolling farmland hills in the middle of the ring (metres of relief).</summary>
            public float hillHeight = 45f;
            /// <summary>Wooded ridges at the rim, above center.y: high enough to close the horizon from the play area's high points.</summary>
            public float ridgeHeight = 190f;
            public int seed = 7;
            /// <summary>Stands of real trees (LOD trees) in the near part of the ring.</summary>
            public int treeClusters = 24;
            /// <summary>Unused since pass 3 (the polka dots); hedgerows and woods replace the canopy blobs.</summary>
            public int canopyBlobs = 0;
            /// <summary>How the field patchwork is turned relative to the world axes (degrees).</summary>
            public float fieldRotation = 17f;
            /// <summary>3D hedgerows out to this distance from the centre (beyond it the painted ones carry the pattern).</summary>
            public float hedgeDistance = 1900f;
            public bool woods = true;
            public bool rimForest = true;
            public List<FarRoad> roads = new List<FarRoad>();
            public int angularSegments = 288;
            public int radialRings = 56;
        }

        public static GameObject Build(Transform parent, Options o)
        {
            var root = new GameObject("Backdrop (backdrop)");
            if (parent != null) root.transform.SetParent(parent, false);
            var grid = new RingGrid(o);
            Ground(root.transform, o, grid);
            var layout = LoadLayout();
            if (layout != null)
            {
                Hedges(root.transform, o, grid, layout);
                if (o.woods) Woods(root.transform, o, grid, layout);
            }
            if (o.rimForest) RimForest(root.transform, o, grid);
            foreach (var r in o.roads) Road(root.transform, o, grid, r);
            Trees(root.transform, o, grid);
            return root;
        }

        /// <summary>The ring's ground height at (x, z) (world).</summary>
        public static float Height(Options o, float x, float z)
        {
            float near = o.nearHeight != null ? o.nearHeight(x, z) : o.center.y;
            if (o.hole.HasValue && o.hole.Value.Contains(new Vector2(x, z))) return near - 0.15f;
            float dx = x - o.center.x, dz = z - o.center.z;
            float r = Mathf.Sqrt(dx * dx + dz * dz);
            float t = Mathf.SmoothStep(0f, 1f, (r - o.innerRadius) / Mathf.Max(1f, o.blendDistance));
            if (t <= 0f) return near;
            float s = o.seed * 13.7f;
            float hills = (Mathf.PerlinNoise(x * 0.0023f + s, z * 0.0023f - s) - 0.45f) * 1.0f
                        + (Mathf.PerlinNoise(x * 0.006f - s, z * 0.006f + s) - 0.5f) * 0.45f
                        + (Mathf.PerlinNoise(x * 0.017f + 3f, z * 0.017f + s) - 0.5f) * 0.12f;
            float span = Mathf.Max(1f, o.outerRadius - o.innerRadius);
            float rimK = Mathf.SmoothStep(0f, 1f, (r - o.innerRadius - span * 0.35f) / (span * 0.6f));
            float ang = Mathf.Atan2(dz, dx);
            float crest = 0.65f + 0.35f * Mathf.PerlinNoise(ang * 2.2f + s, 0.5f) + 0.15f * Mathf.Sin(ang * 5f + s);
            float far = o.center.y + hills * o.hillHeight * Mathf.Lerp(0.5f, 1.4f, t) + rimK * o.ridgeHeight * crest;
            far = Mathf.Max(far, o.center.y - 10f);
            return Mathf.Lerp(near, far, t);
        }

        /// <summary>
        /// The ring's mesh grid (geometric rings, fine near the scene and coarse at the rim) with its heights, so anything laid
        /// on the ring (roads, hedgerows, woods) can sit on the rendered surface exactly, not on the smooth height function
        /// between the grid's vertices.
        /// </summary>
        sealed class RingGrid
        {
            public readonly Options o;
            public readonly int segs, rings;
            public readonly float r0, r1;
            public readonly float[] radii;
            public readonly float[,] h;   // [ring, segment]

            public RingGrid(Options o)
            {
                this.o = o;
                segs = Mathf.Max(32, o.angularSegments);
                rings = Mathf.Max(8, o.radialRings);
                r0 = Mathf.Max(10f, o.innerRadius * 0.55f);
                r1 = o.outerRadius;
                radii = new float[rings + 1];
                for (int i = 0; i <= rings; i++) radii[i] = r0 * Mathf.Pow(r1 / r0, i / (float)rings);
                h = new float[rings + 1, segs + 1];
                for (int i = 0; i <= rings; i++)
                    for (int j = 0; j <= segs; j++)
                    {
                        var p = Point(i, j);
                        h[i, j] = Height(o, p.x, p.y);
                    }
            }

            public Vector2 Point(int i, int j)
            {
                float a = j / (float)segs * Mathf.PI * 2f;
                return new Vector2(o.center.x + Mathf.Cos(a) * radii[i], o.center.z + Mathf.Sin(a) * radii[i]);
            }

            public float Radius(float x, float z) => Mathf.Sqrt((x - o.center.x) * (x - o.center.x) + (z - o.center.z) * (z - o.center.z));

            /// <summary>The rendered surface's height at (x, z): the grid quad's triangle, interpolated as the GPU does.
            /// Outside the grid: the height function.</summary>
            public float SurfaceHeight(float x, float z)
            {
                float dx = x - o.center.x, dz = z - o.center.z;
                float r = Mathf.Sqrt(dx * dx + dz * dz);
                if (r <= r0 || r >= r1) return Height(o, x, z);
                float fi = Mathf.Log(r / r0) / Mathf.Log(r1 / r0) * rings;
                float a = Mathf.Repeat(Mathf.Atan2(dz, dx), Mathf.PI * 2f);
                float fj = a / (Mathf.PI * 2f) * segs;
                int i = Mathf.Clamp(Mathf.FloorToInt(fi), 0, rings - 1), j = Mathf.Clamp(Mathf.FloorToInt(fj), 0, segs - 1);
                // The quad's corners as actual points (the grid isn't rectangular in xz), split as the mesh is: (a, c, b), (b, c, d).
                var pa = Point(i, j); var pb = Point(i, j + 1); var pc = Point(i + 1, j); var pd = Point(i + 1, j + 1);
                var q = new Vector2(x, z);
                if (Bary(q, pa, pc, pb, out float u, out float v, out float w)) return u * h[i, j] + v * h[i + 1, j] + w * h[i, j + 1];
                if (Bary(q, pb, pc, pd, out u, out v, out w)) return u * h[i, j + 1] + v * h[i + 1, j] + w * h[i + 1, j + 1];
                return Height(o, x, z);
            }

            static bool Bary(Vector2 p, Vector2 a, Vector2 b, Vector2 c, out float u, out float v, out float w)
            {
                var v0 = b - a; var v1 = c - a; var v2 = p - a;
                float d00 = Vector2.Dot(v0, v0), d01 = Vector2.Dot(v0, v1), d11 = Vector2.Dot(v1, v1);
                float d20 = Vector2.Dot(v2, v0), d21 = Vector2.Dot(v2, v1);
                float den = d00 * d11 - d01 * d01;
                if (Mathf.Abs(den) < 1e-6f) { u = v = w = 0f; return false; }
                v = (d11 * d20 - d01 * d21) / den;
                w = (d00 * d21 - d01 * d20) / den;
                u = 1f - v - w;
                return u >= -0.02f && v >= -0.02f && w >= -0.02f;
            }

            public bool InHole(float x, float z, float margin = 0f)
            {
                if (!o.hole.HasValue) return false;
                var hr = o.hole.Value;
                return x > hr.xMin - margin && x < hr.xMax + margin && z > hr.yMin - margin && z < hr.yMax + margin;
            }

            /// <summary>Distance from the scene's ground (the hole) in xz; 0 inside it.</summary>
            public float HoleDistance(float x, float z)
            {
                if (!o.hole.HasValue) return Radius(x, z) - o.innerRadius;
                var hr = o.hole.Value;
                float ddx = Mathf.Max(hr.xMin - x, 0f, x - hr.xMax), ddz = Mathf.Max(hr.yMin - z, 0f, z - hr.yMax);
                return Mathf.Sqrt(ddx * ddx + ddz * ddz);
            }
        }

        // ---------------- The ground: fields ----------------

        /// <summary>The field patchwork's coordinates (metres in the texture's own frame) at a world point.</summary>
        static Vector2 FieldUv(Options o, float x, float z)
        {
            float a = o.fieldRotation * Mathf.Deg2Rad, c = Mathf.Cos(a), s = Mathf.Sin(a);
            float px = x - o.center.x + 1234f, pz = z - o.center.z + 777f;
            return new Vector2(px * c - pz * s, px * s + pz * c);
        }

        /// <summary>The inverse: a point in the field frame back to world xz.</summary>
        static Vector2 FieldToWorld(Options o, Vector2 f)
        {
            float a = -o.fieldRotation * Mathf.Deg2Rad, c = Mathf.Cos(a), s = Mathf.Sin(a);
            var p = new Vector2(f.x * c - f.y * s, f.x * s + f.y * c);
            return new Vector2(p.x - 1234f + o.center.x, p.y - 777f + o.center.z);
        }

        static void Ground(Transform root, Options o, RingGrid g)
        {
            var mat = VhLookLibrary.FarFields();
            const int sectors = 16;
            int segPer = g.segs / sectors;
            var grp = new GameObject("Ring ground").transform;
            grp.SetParent(root, false);
            for (int sct = 0; sct < sectors; sct++)
            {
                var v = new List<Vector3>(); var uv = new List<Vector2>(); var col = new List<Color>(); var t = new List<int>();
                for (int i = 0; i <= g.rings; i++)
                    for (int j = 0; j <= segPer; j++)
                    {
                        int jj = sct * segPer + j;
                        var p = g.Point(i, jj);
                        v.Add(new Vector3(p.x, g.h[i, jj], p.y));
                        uv.Add(FieldUv(o, p.x, p.y));
                        // The scene's lawn colour at its edge, the fields from ~60 m out to ~300 m (no seam at the hole).
                        float near = Mathf.SmoothStep(0f, 1f, (g.HoleDistance(p.x, p.y) - 40f) / 260f);
                        col.Add(new Color(1f, 1f, 1f, near));
                    }
                int row = segPer + 1;
                for (int i = 0; i < g.rings; i++)
                    for (int j = 0; j < segPer; j++)
                    {
                        int a = i * row + j, b = a + 1, c = a + row, d = c + 1;
                        t.Add(a); t.Add(c); t.Add(b);
                        t.Add(b); t.Add(c); t.Add(d);
                    }
                var mesh = new Mesh { name = "Backdrop ground " + sct, indexFormat = IndexFormat.UInt32 };
                mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetColors(col); mesh.SetTriangles(t, 0);
                FaceUp(mesh);
                mesh.RecalculateNormals(); mesh.RecalculateBounds();
                Add(grp, "Sector " + sct, mesh, mat);
            }
        }

        static void FaceUp(Mesh mesh)
        {
            var tris = mesh.triangles;
            var verts = mesh.vertices;
            for (int k = 0; k < tris.Length; k += 3)
            {
                var n = Vector3.Cross(verts[tris[k + 1]] - verts[tris[k]], verts[tris[k + 2]] - verts[tris[k]]);
                if (n.y < 0f) { int tmp = tris[k + 1]; tris[k + 1] = tris[k + 2]; tris[k + 2] = tmp; }
            }
            mesh.triangles = tris;
        }

        static void Add(Transform parent, string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        // ---------------- The field layout (hedgerows, woods) ----------------

        [Serializable]
        sealed class Layout
        {
            public float tile = 2400f;
            public float[] hedges;
            public float[] woods;
        }

        static Layout LoadLayout()
        {
            string path = VhLookLibrary.FarFieldsLayoutPath;
            if (!File.Exists(path)) { Debug.LogWarning("JAMKIT_LOOK no field layout (run tools/look/make-foliage-atlas.py): " + path); return null; }
            try { return JsonUtility.FromJson<Layout>(File.ReadAllText(path)); }
            catch (Exception e) { Debug.LogWarning("JAMKIT_LOOK field layout unreadable: " + e.Message); return null; }
        }

        /// <summary>Every copy of a layout item (tile coordinates, y down the image) in world xz within the ring.</summary>
        static IEnumerable<(Vector2 a, Vector2 b)> Copies(Options o, Layout l, float x0, float y0, float x1, float y1)
        {
            float T = l.tile;
            // Image rows run down; the texture's v runs up: tile (x, y) is field-frame (x, T - y).
            var fa = new Vector2(x0, T - y0);
            var fb = new Vector2(x1, T - y1);
            // The field-frame span the ring covers.
            float reach = o.outerRadius + 1500f;
            var c = FieldUv(o, o.center.x, o.center.z);
            int k0x = Mathf.FloorToInt((c.x - reach) / T), k1x = Mathf.CeilToInt((c.x + reach) / T);
            int k0y = Mathf.FloorToInt((c.y - reach) / T), k1y = Mathf.CeilToInt((c.y + reach) / T);
            for (int kx = k0x; kx <= k1x; kx++)
                for (int ky = k0y; ky <= k1y; ky++)
                {
                    var off = new Vector2(kx * T, ky * T);
                    yield return (FieldToWorld(o, fa + off), FieldToWorld(o, fb + off));
                }
        }

        static float DistanceToRoads(Options o, Vector2 p)
        {
            float best = float.MaxValue;
            foreach (var r in o.roads)
                for (int i = 0; i < r.points.Length - 1; i++)
                {
                    var a = r.points[i]; var b = r.points[i + 1];
                    var ab = b - a;
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
                    best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
                }
            return best;
        }

        // ---------------- Hedgerows ----------------

        /// <summary>
        /// Hedgerows and tree lines along the field edges: a continuous knobbly strip (a five-point arch in section, 4-6 m wide,
        /// 3.5-8 m tall, with a taller tree every ~25-40 m), its ends tapering into the ground, sampled every 6-25 m (finer
        /// near). Vertex colour darkens the base. One mesh per 16th of the ring.
        /// </summary>
        static void Hedges(Transform root, Options o, RingGrid g, Layout l)
        {
            if (l.hedges == null) return;
            var mat = VhLookLibrary.FarCanopy();
            const int sectors = 16;
            var lists = new MeshLists[sectors];
            for (int i = 0; i < sectors; i++) lists[i] = new MeshLists();
            float s = o.seed * 3.1f;
            int made = 0;
            for (int h = 0; h + 3 < l.hedges.Length; h += 4)
                foreach (var (a, b) in Copies(o, l, l.hedges[h], l.hedges[h + 1], l.hedges[h + 2], l.hedges[h + 3]))
                {
                    float len = Vector2.Distance(a, b);
                    if (len < 10f) continue;
                    var mid = (a + b) * 0.5f;
                    float rm = g.Radius(mid.x, mid.y);
                    if (rm > o.hedgeDistance + len * 0.5f || rm < o.innerRadius * 0.7f - len * 0.5f) continue;
                    var dir = (b - a) / len;
                    var side = new Vector2(-dir.y, dir.x);
                    // Walk it, starting and stopping pieces where it leaves the ring's band, enters the scene or meets a road.
                    var pts = new List<Vector3>(); var widths = new List<float>(); var heights = new List<float>();
                    float t = 0f;
                    while (t <= len)
                    {
                        var p = a + dir * t;
                        float r = g.Radius(p.x, p.y);
                        bool ok = r > o.innerRadius * 0.8f && r < o.hedgeDistance && r < o.outerRadius * 0.97f && !g.InHole(p.x, p.y, 25f) &&
                                  (o.roads.Count == 0 || DistanceToRoads(o, p) > 9f);
                        if (ok)
                        {
                            float n = Mathf.PerlinNoise(p.x * 0.05f + s, p.y * 0.05f - s);
                            // Pass 4 (round 3: "flat-topped extruded prisms"): crowns every 12-22 m, a knobbly top (a fast noise, +-30%),
                            // trees standing out of it, the line wandering a metre either side.
                            float tree = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t / Mathf.Lerp(18f, 30f, n) * Mathf.PI)), 4f);
                            float knob = Mathf.PerlinNoise(p.x * 0.31f + s, p.y * 0.31f - s);
                            float crown = Mathf.Abs(Mathf.Sin(t / Mathf.Lerp(12f, 22f, n) * Mathf.PI));
                            var wob = side * ((Mathf.PerlinNoise(p.x * 0.04f - s, p.y * 0.04f) - 0.5f) * 2f);
                            heights.Add((Mathf.Lerp(3.5f, 7f, n) * Mathf.Lerp(0.7f, 1.3f, knob) * Mathf.Lerp(0.8f, 1.1f, crown)) + tree * Mathf.Lerp(4f, 7f, n));
                            widths.Add(Mathf.Lerp(4f, 6f, n) * Mathf.Lerp(0.8f, 1.2f, crown) + tree * 2.5f);
                            pts.Add(new Vector3(p.x + wob.x, g.SurfaceHeight(p.x + wob.x, p.y + wob.y) - 0.6f, p.y + wob.y));
                        }
                        if ((!ok || t + 1e-3f >= len) && pts.Count >= 3)
                        {
                            int sector = Mathf.Clamp((int)(Mathf.Repeat(Mathf.Atan2(pts[pts.Count / 2].z - o.center.z, pts[pts.Count / 2].x - o.center.x), Mathf.PI * 2f) / (Mathf.PI * 2f) * sectors), 0, sectors - 1);
                            Strip(lists[sector], pts, widths, heights, side);
                            made++;
                        }
                        if (!ok || t + 1e-3f >= len) { pts.Clear(); widths.Clear(); heights.Clear(); }
                        float step = Mathf.Clamp(r * 0.006f, 4f, 16f);
                        if (t < len && t + step > len) t = len; else t += step;
                        if (t > len) break;
                    }
                }
            var grp = new GameObject("Hedgerows").transform;
            grp.SetParent(root, false);
            for (int i = 0; i < sectors; i++)
                if (lists[i].v.Count > 0) Add(grp, "Hedgerows " + i, lists[i].ToMesh("Hedgerows " + i), mat);
            Debug.Log("JAMKIT_LOOK backdrop: " + made + " hedgerow pieces");
        }

        sealed class MeshLists
        {
            public readonly List<Vector3> v = new List<Vector3>();
            public readonly List<Vector3> n = new List<Vector3>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<Color> c = new List<Color>();
            public readonly List<int> t = new List<int>();

            public Mesh ToMesh(string name)
            {
                var m = new Mesh { name = name, indexFormat = v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetColors(c); m.SetTriangles(t, 0);
                m.RecalculateBounds();
                return m;
            }
        }

        /// <summary>An arch strip along <paramref name="pts"/> (bases on the ground): five points in section, the ends pinched
        /// to the ground.</summary>
        static void Strip(MeshLists L, List<Vector3> pts, List<float> widths, List<float> heights, Vector2 side2)
        {
            var side = new Vector3(side2.x, 0f, side2.y);
            float[] across = { -1f, -0.75f, 0f, 0.75f, 1f };
            float[] up = { 0f, 0.72f, 1f, 0.72f, 0f };
            int start = L.v.Count, firstTri = L.t.Count;
            float along = 0f;
            for (int i = 0; i < pts.Count; i++)
            {
                if (i > 0) along += Vector3.Distance(pts[i - 1], pts[i]);
                float end = Mathf.Min(1f, Mathf.Min(i, pts.Count - 1 - i) / 1.5f);   // the ends sink into the ground
                float hw = widths[i] * 0.5f * Mathf.Lerp(0.5f, 1f, end), hh = heights[i] * end + 0.4f;
                for (int k = 0; k < 5; k++)
                {
                    var p = pts[i] + side * across[k] * hw + Vector3.up * (up[k] * hh + (k == 0 || k == 4 ? 0f : 0.6f));
                    L.v.Add(p);
                    var nn = (side * across[k] * 0.9f + Vector3.up * (0.35f + up[k])).normalized;
                    L.n.Add(nn);
                    L.uv.Add(new Vector2(along, across[k] * hw + up[k] * hh));
                    float shade = Mathf.Lerp(0.5f, 1f, up[k]);
                    L.c.Add(new Color(shade, shade, shade, 1f));
                }
            }
            for (int i = 0; i < pts.Count - 1; i++)
                for (int k = 0; k < 4; k++)
                {
                    int a = start + i * 5 + k, b = a + 1, c = a + 5, d = c + 1;
                    L.t.Add(a); L.t.Add(b); L.t.Add(c);
                    L.t.Add(b); L.t.Add(d); L.t.Add(c);
                }
            FixWinding(L, firstTri);
        }

        /// <summary>Makes every triangle from index <paramref name="firstTri"/> of the list on face along its vertices' normals
        /// (single-sided, facing out).</summary>
        static void FixWinding(MeshLists L, int firstTri)
        {
            for (int k = firstTri; k < L.t.Count; k += 3)
            {
                int a = L.t[k], b = L.t[k + 1], c = L.t[k + 2];
                var fn = Vector3.Cross(L.v[b] - L.v[a], L.v[c] - L.v[a]);
                if (Vector3.Dot(fn, L.n[a] + L.n[b] + L.n[c]) < 0f) { L.t[k + 1] = c; L.t[k + 2] = b; }
            }
        }

        // ---------------- Woods ----------------

        /// <summary>Woods over whole fields (the layout's wood rectangles): a lumpy canopy skin 11-17 m over the ground on an
        /// 18 m grid, its edge row dropping to the ground (a wood's edge), darker low down.</summary>
        static void Woods(Transform root, Options o, RingGrid g, Layout l)
        {
            if (l.woods == null) return;
            var mat = VhLookLibrary.FarCanopy();
            var L = new MeshLists();
            float s = o.seed * 1.7f;
            int made = 0;
            for (int w = 0; w + 3 < l.woods.Length; w += 4)
            {
                float x0 = l.woods[w], y0 = l.woods[w + 1], x1 = l.woods[w + 2], y1 = l.woods[w + 3];
                foreach (var (a, b) in Copies(o, l, x0, y0, x1, y1))
                {
                    // a and b are opposite corners in the world; walk the rectangle in the field frame instead.
                    var fa = FieldUv(o, a.x, a.y); var fb = FieldUv(o, b.x, b.y);
                    float fx0 = Mathf.Min(fa.x, fb.x), fx1 = Mathf.Max(fa.x, fb.x), fy0 = Mathf.Min(fa.y, fb.y), fy1 = Mathf.Max(fa.y, fb.y);
                    var mid = FieldToWorld(o, new Vector2((fx0 + fx1) * 0.5f, (fy0 + fy1) * 0.5f));
                    float rm = g.Radius(mid.x, mid.y);
                    if (rm < o.innerRadius + 60f || rm > o.outerRadius * 0.96f || g.InHole(mid.x, mid.y, 120f)) continue;
                    float step = Mathf.Clamp(rm * 0.012f, 12f, 30f);
                    int nx = Mathf.Max(2, Mathf.CeilToInt((fx1 - fx0) / step)), ny = Mathf.Max(2, Mathf.CeilToInt((fy1 - fy0) / step));
                    int start = L.v.Count;
                    for (int j = 0; j <= ny; j++)
                        for (int i = 0; i <= nx; i++)
                        {
                            var f = new Vector2(Mathf.Lerp(fx0, fx1, i / (float)nx), Mathf.Lerp(fy0, fy1, j / (float)ny));
                            var p = FieldToWorld(o, f);
                            bool edge = i == 0 || j == 0 || i == nx || j == ny;
                            float lump = Mathf.PerlinNoise(p.x * 0.06f + s, p.y * 0.06f - s);
                            float ht = edge ? -0.8f : Mathf.Lerp(11f, 17f, lump);
                            float ground = g.SurfaceHeight(p.x, p.y);
                            L.v.Add(new Vector3(p.x, ground + ht, p.y));
                            L.n.Add(Vector3.up);
                            L.uv.Add(new Vector2(p.x, p.y));
                            float shade = edge ? 0.5f : Mathf.Lerp(0.8f, 1.05f, lump);
                            L.c.Add(new Color(shade, shade, shade, 1f));
                        }
                    int row = nx + 1;
                    for (int j = 0; j < ny; j++)
                        for (int i = 0; i < nx; i++)
                        {
                            int i0 = start + j * row + i, i1 = i0 + 1, i2 = i0 + row, i3 = i2 + 1;
                            L.t.Add(i0); L.t.Add(i2); L.t.Add(i1);
                            L.t.Add(i1); L.t.Add(i2); L.t.Add(i3);
                        }
                    made++;
                }
            }
            if (L.v.Count == 0) return;
            var mesh = L.ToMesh("Woods");
            FaceUp(mesh);
            mesh.RecalculateNormals();
            var grp = new GameObject("Woods").transform;
            grp.SetParent(root, false);
            Add(grp, "Woods", mesh, mat);
            Debug.Log("JAMKIT_LOOK backdrop: " + made + " woods");
        }

        // ---------------- Forest on the rim's ridges ----------------

        /// <summary>Forest over the rim's ridges, in patches (noise), on the ring's own grid from 70% of its radius out: a
        /// canopy 12-16 m over the ground where the forest is, sunk 2 m under it where it isn't (quads entirely outside a
        /// patch are skipped).</summary>
        static void RimForest(Transform root, Options o, RingGrid g)
        {
            var mat = VhLookLibrary.FarCanopy();
            var L = new MeshLists();
            float s = o.seed * 2.3f;
            int iStart = 0;
            while (iStart < g.rings && g.radii[iStart] < o.outerRadius * 0.7f) iStart++;
            int rows = g.rings - iStart + 1;
            var mask = new float[rows, g.segs + 1];
            for (int i = 0; i < rows; i++)
                for (int j = 0; j <= g.segs; j++)
                {
                    var p = g.Point(iStart + i, j);
                    float rimK = Mathf.Clamp01((g.radii[iStart + i] - o.outerRadius * 0.7f) / (o.outerRadius * 0.18f));
                    float n = Mathf.PerlinNoise(p.x * 0.0035f + s, p.y * 0.0035f - s);
                    float m = Mathf.Clamp01((n - 0.42f + rimK * 0.25f) / 0.12f) * Mathf.Clamp01(rimK * 2f);
                    mask[i, j] = j == g.segs ? mask[i, 0] : m;
                    float ht = m > 0.01f ? Mathf.Lerp(-2f, Mathf.Lerp(12f, 16f, Mathf.PerlinNoise(p.x * 0.05f, p.y * 0.05f)), m) : -2f;
                    L.v.Add(new Vector3(p.x, g.h[iStart + i, j] + ht, p.y));
                    L.n.Add(Vector3.up);
                    L.uv.Add(p);
                    float shade = Mathf.Lerp(0.55f, 1f, m);
                    L.c.Add(new Color(shade, shade, shade, 1f));
                }
            int row = g.segs + 1;
            for (int i = 0; i < rows - 1; i++)
                for (int j = 0; j < g.segs; j++)
                {
                    if (mask[i, j] + mask[i, j + 1] + mask[i + 1, j] + mask[i + 1, j + 1] < 0.02f) continue;
                    int a = i * row + j, b = a + 1, c = a + row, d = c + 1;
                    L.t.Add(a); L.t.Add(c); L.t.Add(b);
                    L.t.Add(b); L.t.Add(c); L.t.Add(d);
                }
            if (L.t.Count == 0) return;
            var mesh = L.ToMesh("Rim forest");
            FaceUp(mesh);
            mesh.RecalculateNormals();
            var grp = new GameObject("Rim forest").transform;
            grp.SetParent(root, false);
            Add(grp, "Rim forest", mesh, mat);
        }

        // ---------------- Roads ----------------

        /// <summary>A road ribbon draped on the ring's rendered surface (an asphalt carriageway with dirt shoulders), resampled
        /// every 6-20 m, easing from the scene road's height over its first 30 m.</summary>
        static void Road(Transform root, Options o, RingGrid g, FarRoad r)
        {
            if (r.points == null || r.points.Length < 2) return;
            var mat = VhLookLibrary.FarRoad();
            var L = new MeshLists();
            var path = new List<Vector2>();
            for (int i = 0; i < r.points.Length - 1; i++)
            {
                var a = r.points[i]; var b = r.points[i + 1];
                float len = Vector2.Distance(a, b);
                float step = Mathf.Clamp(g.Radius(a.x, a.y) * 0.01f, 6f, 20f);
                int n = Mathf.Max(1, Mathf.CeilToInt(len / step));
                for (int k = 0; k < n; k++) path.Add(Vector2.Lerp(a, b, k / (float)n));
            }
            path.Add(r.points[r.points.Length - 1]);
            float along = 0f;
            float half = r.width * 0.5f, shoulder = 1.6f;
            float[] across = { -half - shoulder, -half, half, half + shoulder };
            for (int i = 0; i < path.Count; i++)
            {
                if (i > 0) along += Vector2.Distance(path[i - 1], path[i]);
                var dir = (path[Mathf.Min(i + 1, path.Count - 1)] - path[Mathf.Max(i - 1, 0)]).normalized;
                var side = new Vector2(-dir.y, dir.x);
                float lift = 0.12f + along * 0.0003f;
                for (int k = 0; k < 4; k++)
                {
                    var p = path[i] + side * across[k];
                    float y = Mathf.Max(g.SurfaceHeight(p.x, p.y), Height(o, p.x, p.y)) + lift;
                    if (along < 30f) y = Mathf.Lerp(r.startY + 0.005f, y, along / 30f);
                    L.v.Add(new Vector3(p.x, y, p.y));
                    L.n.Add(Vector3.up);
                    // u across the carriageway (0-1 asphalt, outside it the shoulder), v along it; colour: shoulders are
                    // the dry verge (alpha 0 = the material's near colour).
                    L.uv.Add(new Vector2(across[k], along));
                    L.c.Add(new Color(1f, 1f, 1f, k == 0 || k == 3 ? 0f : 1f));
                }
            }
            for (int i = 0; i < path.Count - 1; i++)
                for (int k = 0; k < 3; k++)
                {
                    int a = i * 4 + k, b = a + 1, c = a + 4, d = c + 1;
                    L.t.Add(a); L.t.Add(c); L.t.Add(b);
                    L.t.Add(b); L.t.Add(c); L.t.Add(d);
                }
            var mesh = L.ToMesh("Far road");
            FaceUp(mesh);
            mesh.RecalculateNormals();
            var grp = new GameObject("Far road").transform;
            grp.SetParent(root, false);
            Add(grp, "Far road", mesh, mat);
        }

        // ---------------- Near stands of real trees ----------------

        static void Trees(Transform root, Options o, RingGrid g)
        {
            if (o.treeClusters <= 0) return;
            var grp = new GameObject("Ring trees").transform;
            grp.SetParent(root, false);
            var rng = new System.Random(o.seed * 4111 + 3);
            float R() => (float)rng.NextDouble();
            int made = 0, tries = 0;
            Func<float, float, float> ground = (x, z) => g.SurfaceHeight(x, z);
            while (made < o.treeClusters && tries++ < o.treeClusters * 10)
            {
                float a = R() * Mathf.PI * 2f;
                float r = Mathf.Lerp(o.innerRadius + 30f, o.innerRadius + 650f, Mathf.Sqrt(R()));
                var c = new Vector3(o.center.x + Mathf.Cos(a) * r, 0f, o.center.z + Mathf.Sin(a) * r);
                if (g.InHole(c.x, c.z, 20f)) continue;
                if (o.roads.Count > 0 && DistanceToRoads(o, new Vector2(c.x, c.z)) < 30f) continue;
                int count = 4 + rng.Next(7);
                VhLookTrees.Cluster(grp, c, 10f + count * 2f, count, rng.Next(), Mathf.Lerp(9f, 14f, R()), ground, null, 4f,
                    p => !g.InHole(p.x, p.z, 4f) && (o.roads.Count == 0 || DistanceToRoads(o, new Vector2(p.x, p.z)) > 9f), false);
                made++;
            }
        }
    }
}
