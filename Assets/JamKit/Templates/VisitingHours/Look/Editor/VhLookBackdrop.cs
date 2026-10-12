using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace JamKit.Look.EditorTools
{
    /// <summary>
    /// Closes the world (the judge's round 1: "the world ends", a hard ground edge at the horizon, a lone tree in the void):
    /// a ring of terrain from the edge of the play area out to 2-3 km, its near edge continuing the scene's own ground, rolling
    /// into farmland hills and wooded ridges at the rim, with stands of real trees in the near part of the ring and rounded
    /// canopy masses (a tree line) further out. The atmosphere's far haze (VH_GoldenHour: it thickens with distance and fades
    /// to the sky's own horizon colour) dissolves it into the sky.
    /// <code>
    /// VhLookBackdrop.Build(sceneRoot, new VhLookBackdrop.Options
    /// {
    ///     center = new Vector3(0f, 0f, 150f), innerRadius = 420f, outerRadius = 2800f,
    ///     nearHeight = MyGroundHeight,                       // (x, z) => ground height; continued outward, then blended away
    ///     hole = new Rect(-340f, -90f, 680f, 740f),           // the scene's own ground: the ring tucks just under it there
    /// });
    /// </code>
    /// Everything is under one "Backdrop (backdrop)" root (the probe bake skips "(backdrop)" roots). No colliders.
    /// </summary>
    public static class VhLookBackdrop
    {
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
            public int treeClusters = 40;
            /// <summary>Rounded canopy masses (woods, hedgerows, the ridge forests) further out.</summary>
            public int canopyBlobs = 2200;
            public int angularSegments = 288;
            public int radialRings = 56;
        }

        public static GameObject Build(Transform parent, Options o)
        {
            var root = new GameObject("Backdrop (backdrop)");
            if (parent != null) root.transform.SetParent(parent, false);
            Func<float, float, float> height = (x, z) => Height(o, x, z);
            Ground(root.transform, o, height);
            Trees(root.transform, o, height);
            Canopy(root.transform, o, height);
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
            // Rolling hills (fbm), stronger with distance; ridges at the rim, their crest line varying round the ring.
            float hills = (Mathf.PerlinNoise(x * 0.0023f + s, z * 0.0023f - s) - 0.45f) * 1.0f
                        + (Mathf.PerlinNoise(x * 0.006f - s, z * 0.006f + s) - 0.5f) * 0.45f
                        + (Mathf.PerlinNoise(x * 0.017f + 3f, z * 0.017f + s) - 0.5f) * 0.12f;
            float span = Mathf.Max(1f, o.outerRadius - o.innerRadius);
            float rimK = Mathf.SmoothStep(0f, 1f, (r - o.innerRadius - span * 0.35f) / (span * 0.6f));
            float ang = Mathf.Atan2(dz, dx);
            float crest = 0.65f + 0.35f * Mathf.PerlinNoise(ang * 2.2f + s, 0.5f) + 0.15f * Mathf.Sin(ang * 5f + s);
            float far = o.center.y + hills * o.hillHeight * Mathf.Lerp(0.5f, 1.4f, t) + rimK * o.ridgeHeight * crest;
            // Never dip far below the scene's level (no void below the haze).
            far = Mathf.Max(far, o.center.y - 10f);
            return Mathf.Lerp(near, far, t);
        }

        static void Ground(Transform root, Options o, Func<float, float, float> height)
        {
            var mat = VhLookLibrary.FarFields();
            int sectors = 16;
            int segPer = Mathf.Max(4, o.angularSegments / sectors);
            int rings = Mathf.Max(8, o.radialRings);
            // Rings spaced geometrically: fine near the scene, coarse at the rim.
            var radii = new float[rings + 1];
            float r0 = Mathf.Max(10f, o.innerRadius * 0.55f), r1 = o.outerRadius;
            for (int i = 0; i <= rings; i++) radii[i] = r0 * Mathf.Pow(r1 / r0, i / (float)rings);
            var grp = new GameObject("Ring ground").transform;
            grp.SetParent(root, false);
            for (int sct = 0; sct < sectors; sct++)
            {
                var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
                for (int i = 0; i <= rings; i++)
                    for (int j = 0; j <= segPer; j++)
                    {
                        float a = (sct * segPer + j) / (float)(sectors * segPer) * Mathf.PI * 2f;
                        float x = o.center.x + Mathf.Cos(a) * radii[i], z = o.center.z + Mathf.Sin(a) * radii[i];
                        v.Add(new Vector3(x, height(x, z), z));
                        uv.Add(new Vector2(x, z));
                    }
                int row = segPer + 1;
                for (int i = 0; i < rings; i++)
                    for (int j = 0; j < segPer; j++)
                    {
                        int a = i * row + j, b = a + 1, c = a + row, d = c + 1;
                        t.Add(a); t.Add(c); t.Add(b);
                        t.Add(b); t.Add(c); t.Add(d);
                    }
                var mesh = new Mesh { name = "Backdrop ground " + sct, indexFormat = IndexFormat.UInt32 };
                mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0);
                // Winding: make the faces point up.
                var tris = mesh.triangles;
                var verts = mesh.vertices;
                for (int k = 0; k < tris.Length; k += 3)
                {
                    var n = Vector3.Cross(verts[tris[k + 1]] - verts[tris[k]], verts[tris[k + 2]] - verts[tris[k]]);
                    if (n.y < 0f) { int tmp = tris[k + 1]; tris[k + 1] = tris[k + 2]; tris[k + 2] = tmp; }
                }
                mesh.triangles = tris;
                mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
                var go = new GameObject("Sector " + sct);
                go.transform.SetParent(grp, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        static void Trees(Transform root, Options o, Func<float, float, float> height)
        {
            if (o.treeClusters <= 0) return;
            var grp = new GameObject("Ring trees").transform;
            grp.SetParent(root, false);
            var rng = new System.Random(o.seed * 4111 + 3);
            float R() => (float)rng.NextDouble();
            int made = 0, tries = 0;
            while (made < o.treeClusters && tries++ < o.treeClusters * 10)
            {
                float a = R() * Mathf.PI * 2f;
                float r = Mathf.Lerp(o.innerRadius + 30f, o.innerRadius + 750f, Mathf.Sqrt(R()));
                var c = new Vector3(o.center.x + Mathf.Cos(a) * r, 0f, o.center.z + Mathf.Sin(a) * r);
                if (o.hole.HasValue && o.hole.Value.Contains(new Vector2(c.x, c.z))) continue;
                int count = 4 + rng.Next(9);
                VhLookTrees.Cluster(grp, c, 10f + count * 2f, count, rng.Next(), Mathf.Lerp(9f, 14f, R()), height, null, 4f,
                    p => !o.hole.HasValue || !o.hole.Value.Contains(new Vector2(p.x, p.z)), false);
                made++;
            }
        }

        /// <summary>Rounded canopy masses: woods in patches (by noise), hedgerow lines, and solid forest on the rim's ridges.
        /// Merged into one mesh per sector.</summary>
        static void Canopy(Transform root, Options o, Func<float, float, float> height)
        {
            if (o.canopyBlobs <= 0) return;
            var mat = VhLookLibrary.FarCanopy();
            var blob = Blob();
            var rng = new System.Random(o.seed * 7717 + 1);
            float R() => (float)rng.NextDouble();
            const int sectors = 16;
            var groups = new List<CombineInstance>[sectors];
            for (int i = 0; i < sectors; i++) groups[i] = new List<CombineInstance>();
            float s = o.seed * 5.3f;
            float span = o.outerRadius - o.innerRadius;
            int made = 0, tries = 0;
            while (made < o.canopyBlobs && tries++ < o.canopyBlobs * 12)
            {
                float a = R() * Mathf.PI * 2f;
                float r = Mathf.Lerp(o.innerRadius + 450f, o.outerRadius * 0.985f, Mathf.Pow(R(), 0.8f));
                float x = o.center.x + Mathf.Cos(a) * r, z = o.center.z + Mathf.Sin(a) * r;
                if (o.hole.HasValue && o.hole.Value.Contains(new Vector2(x, z))) continue;
                float rimK = Mathf.Clamp01((r - o.innerRadius - span * 0.45f) / (span * 0.4f));
                float woods = Mathf.PerlinNoise(x * 0.004f + s, z * 0.004f - s);
                float hedge = Mathf.Abs(Mathf.PerlinNoise(x * 0.0019f - s, z * 0.0019f + s) - 0.5f);
                bool keep = woods > 0.6f - rimK * 0.35f || hedge < 0.012f;
                if (!keep) continue;
                float size = Mathf.Lerp(5f, 10f, R()) * Mathf.Lerp(1f, 1.6f, rimK);
                float y = height(x, z);
                var m = Matrix4x4.TRS(new Vector3(x, y + size * 0.35f, z), Quaternion.Euler(0f, R() * 360f, 0f),
                    new Vector3(size, size * Mathf.Lerp(0.75f, 1.05f, R()), size * Mathf.Lerp(0.85f, 1.15f, R())));
                int sector = Mathf.Clamp((int)(Mathf.Repeat(a, Mathf.PI * 2f) / (Mathf.PI * 2f) * sectors), 0, sectors - 1);
                groups[sector].Add(new CombineInstance { mesh = blob, transform = m });
                made++;
            }
            var grp = new GameObject("Tree line").transform;
            grp.SetParent(root, false);
            for (int i = 0; i < sectors; i++)
            {
                if (groups[i].Count == 0) continue;
                var mesh = new Mesh { name = "Canopy " + i, indexFormat = IndexFormat.UInt32 };
                mesh.CombineMeshes(groups[i].ToArray(), true, true);
                mesh.RecalculateBounds();
                var go = new GameObject("Canopy " + i);
                go.transform.SetParent(grp, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        /// <summary>A lumpy unit dome (a subdivided icosahedron, the bottom flattened, the surface pushed in and out) whose
        /// UVs are metres-ish for the canopy material.</summary>
        static Mesh Blob()
        {
            // Icosahedron.
            float p = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var v = new List<Vector3>
            {
                new Vector3(-1, p, 0), new Vector3(1, p, 0), new Vector3(-1, -p, 0), new Vector3(1, -p, 0),
                new Vector3(0, -1, p), new Vector3(0, 1, p), new Vector3(0, -1, -p), new Vector3(0, 1, -p),
                new Vector3(p, 0, -1), new Vector3(p, 0, 1), new Vector3(-p, 0, -1), new Vector3(-p, 0, 1),
            };
            for (int i = 0; i < v.Count; i++) v[i] = v[i].normalized;
            var f = new List<int>
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            // One subdivision.
            var mid = new Dictionary<long, int>();
            int Mid(int a, int b)
            {
                long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                if (mid.TryGetValue(key, out int idx)) return idx;
                v.Add(((v[a] + v[b]) * 0.5f).normalized);
                mid[key] = v.Count - 1;
                return v.Count - 1;
            }
            var f2 = new List<int>();
            for (int i = 0; i < f.Count; i += 3)
            {
                int a = f[i], b = f[i + 1], c = f[i + 2];
                int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                f2.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }
            var verts = new Vector3[v.Count];
            var uvs = new Vector2[v.Count];
            for (int i = 0; i < v.Count; i++)
            {
                var d = v[i];
                float bump = 1f + 0.12f * Mathf.Sin(d.x * 7.1f + d.z * 3.3f) * Mathf.Cos(d.y * 5.7f - d.x * 2.1f);
                var q = d * bump * 0.5f;
                if (q.y < -0.2f) q.y = -0.2f;   // flattened underside (sunk into the ground)
                verts[i] = q;
                uvs[i] = new Vector2(d.x * 3f + d.z * 2f, d.y * 3f);
            }
            var mesh = new Mesh { name = "Canopy blob" };
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.SetTriangles(f2, 0);
            // Outward winding.
            var t = mesh.triangles;
            for (int i = 0; i < t.Length; i += 3)
            {
                var n = Vector3.Cross(verts[t[i + 1]] - verts[t[i]], verts[t[i + 2]] - verts[t[i]]);
                var c = (verts[t[i]] + verts[t[i + 1]] + verts[t[i + 2]]) / 3f;
                if (Vector3.Dot(n, c - new Vector3(0f, -0.05f, 0f)) < 0f) { int k = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = k; }
            }
            mesh.triangles = t;
            mesh.RecalculateNormals();   // no tangents: the far canopy material has no normal map (keeps the scene file lean)
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
