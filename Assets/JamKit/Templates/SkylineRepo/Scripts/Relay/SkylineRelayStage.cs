using System.Collections.Generic;
using JamKit.Skyline;
using UnityEngine;

namespace JamKit.SkylineRepo
{
    /// <summary>
    /// The Relay's one change to the hall's geometry (docs/SKYLINE-RELAY.md): the ramp up the west wall to the mezzanine
    /// (<see cref="RelayArena.RampSolids"/>), so the catwalk station can be walked to. Built at run time, one object per solid:
    /// wedges (a convex mesh and mesh collider) for the two ramps and a box for the landing, in flat InkLit colours.
    /// </summary>
    public static class SkylineRelayStage
    {
        static readonly Color RampColor = new Color(0.80f, 0.74f, 0.62f), LandingColor = new Color(0.86f, 0.85f, 0.82f);

        /// <summary>Builds the ramp pieces under a new "Relay Stage" object and returns it.</summary>
        public static Transform BuildRamp()
        {
            var root = new GameObject("Relay Stage").transform;
            foreach (var s in RelayArena.RampSolids())
            {
                var mat = SkylineInkRoles.Flat(s.ramp ? RampColor : LandingColor, SkylineInkRoles.None) ?? SkylineBotLook.Lit(RampColor);
                if (s.ramp) Wedge(root, s, mat);
                else
                {
                    var go = SkylineBotLook.Visual(PrimitiveType.Cube, s.id, root, mat);
                    Vector3 min = U(s.box.min), max = U(s.box.max);
                    go.transform.position = (min + max) * 0.5f;
                    go.transform.localScale = max - min;
                    go.AddComponent<BoxCollider>();
                }
            }
            return root;
        }

        static Vector3 U(System.Numerics.Vector3 v) => new Vector3(v.X, v.Y, v.Z);

        /// <summary>A ramp whose top slopes along Z from <c>rampTopStart</c> at the box's minimum z to <c>rampTopEnd</c> at its maximum,
        /// down to the box's bottom: a solid wedge (flat-shaded, six faces).</summary>
        static void Wedge(Transform parent, FloorSolid s, Material mat)
        {
            float x0 = s.box.min.X, x1 = s.box.max.X, z0 = s.box.min.Z, z1 = s.box.max.Z, yb = s.box.min.Y;
            float h0 = s.rampTopStart, h1 = s.rampTopEnd;
            var p = new[]
            {
                new Vector3(x0, yb, z0), new Vector3(x1, yb, z0), new Vector3(x1, yb, z1), new Vector3(x0, yb, z1),   // bottom 0..3
                new Vector3(x0, h0, z0), new Vector3(x1, h0, z0), new Vector3(x1, h1, z1), new Vector3(x0, h1, z1),   // top 4..7
            };
            Vector3 centre = Vector3.zero;
            foreach (var v in p) centre += v;
            centre /= p.Length;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            void Quad(int a, int b, int c, int d)
            {
                // flat face: four corners, wound so that the face looks outward (away from the solid's centre)
                Vector3 n = Vector3.Cross(p[b] - p[a], p[c] - p[a]);
                Vector3 mid = (p[a] + p[b] + p[c] + p[d]) * 0.25f;
                if (Vector3.Dot(n, mid - centre) < 0f) { int t = b; b = d; d = t; }
                int i = verts.Count;
                verts.Add(p[a]); verts.Add(p[b]); verts.Add(p[c]); verts.Add(p[d]);
                tris.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
            }
            Quad(0, 1, 2, 3);       // bottom
            Quad(4, 5, 6, 7);       // the sloped top
            Quad(0, 1, 5, 4);       // the low end (a sliver when the top starts at the bottom)
            Quad(3, 2, 6, 7);       // the high end
            Quad(0, 3, 7, 4);       // west side
            Quad(1, 2, 6, 5);       // east side
            var mesh = new Mesh { name = s.id };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            var uv = new Vector2[verts.Count];
            mesh.uv = uv;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var go = new GameObject(s.id);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            var col = go.AddComponent<MeshCollider>();
            col.sharedMesh = mesh;
            col.convex = true;
        }
    }
}
