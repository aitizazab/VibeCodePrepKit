using System;
using System.Collections.Generic;
using System.Numerics;

namespace JamKit.Skyline
{
    /// <summary>A closed, flat-shaded triangle mesh in world coordinates (the Unity builder subtracts the object's centre).</summary>
    public sealed class RampMesh
    {
        public Vector3[] positions = new Vector3[0];
        public Vector3[] normals = new Vector3[0];
        public int[] triangles = new int[0];

        /// <summary>The smallest box round the mesh.</summary>
        public CoverBox Bounds()
        {
            var min = new Vector3(float.MaxValue); var max = new Vector3(float.MinValue);
            foreach (var p in positions) { min = Vector3.Min(min, p); max = Vector3.Max(max, p); }
            return new CoverBox { min = min, max = max };
        }
    }

    /// <summary>Pure geometry for the floor builder (docs/SKYLINE-PLAN-FLOOR1.md Task B1): the wedge a ramp or an escalator
    /// is built from, so the Unity side only hands the numbers to a Mesh and a convex MeshCollider.</summary>
    public static class FloorMeshes
    {
        /// <summary>The wedge of a ramp solid: a prism with a flat bottom at <c>box.min.Y</c> and a top that rises linearly from
        /// <see cref="FloorSolid.rampTopStart"/> (at the box's minimum on the slope axis) to <see cref="FloorSolid.rampTopEnd"/>
        /// (at its maximum). Its bounds equal the solid's box whenever the box's Y range is the ramp's (F1's data always is).
        /// A face with no area (the end of a ramp that runs down to its bottom) is left out.</summary>
        public static RampMesh Wedge(FloorSolid s)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            if (!s.ramp) throw new ArgumentException("not a ramp: " + s.id, nameof(s));
            var b = s.box;
            float y0 = b.min.Y;
            float lo = s.rampAlongX ? b.min.X : b.min.Z, hi = s.rampAlongX ? b.max.X : b.max.Z;
            float c0 = s.rampAlongX ? b.min.Z : b.min.X, c1 = s.rampAlongX ? b.max.Z : b.max.X;
            Vector3 P(float a, float c, float y) => s.rampAlongX ? new Vector3(a, y, c) : new Vector3(c, y, a);
            // bottom corners 0..3 and top corners 4..7 (the top follows the slope)
            var v = new[]
            {
                P(lo, c0, y0), P(hi, c0, y0), P(hi, c1, y0), P(lo, c1, y0),
                P(lo, c0, s.rampTopStart), P(hi, c0, s.rampTopEnd), P(hi, c1, s.rampTopEnd), P(lo, c1, s.rampTopStart),
            };
            var inside = Vector3.Zero;
            foreach (var p in v) inside += p;
            inside /= v.Length;
            int[][] quads =
            {
                new[] { 0, 1, 2, 3 },   // bottom
                new[] { 4, 5, 6, 7 },   // top (the slope)
                new[] { 0, 1, 5, 4 },   // c0 side
                new[] { 3, 2, 6, 7 },   // c1 side
                new[] { 0, 3, 7, 4 },   // lo end
                new[] { 1, 2, 6, 5 },   // hi end
            };
            var pos = new List<Vector3>(); var nor = new List<Vector3>(); var tri = new List<int>();
            foreach (var q in quads)
            {
                var a = v[q[0]]; var bq = v[q[1]]; var c = v[q[2]]; var d = v[q[3]];
                var n = Vector3.Cross(bq - a, c - a) + Vector3.Cross(c - a, d - a);
                float area = n.Length() * 0.5f;
                if (area < 1e-4f) continue;                      // the zero-height end of a ramp that meets its floor
                n = Vector3.Normalize(n);
                var centre = (a + bq + c + d) * 0.25f;
                bool flip = Vector3.Dot(n, centre - inside) < 0f;   // the wedge is convex: outward is away from its middle
                if (flip) n = -n;
                int i0 = pos.Count;
                pos.Add(a); pos.Add(bq); pos.Add(c); pos.Add(d);
                for (int k = 0; k < 4; k++) nor.Add(n);
                // two triangles, either left out when it has no area (a side face of a ramp that meets its floor is a triangle)
                if (TriArea(a, bq, c) > 1e-5f) tri.AddRange(flip ? new[] { i0, i0 + 2, i0 + 1 } : new[] { i0, i0 + 1, i0 + 2 });
                if (TriArea(a, c, d) > 1e-5f) tri.AddRange(flip ? new[] { i0, i0 + 3, i0 + 2 } : new[] { i0, i0 + 2, i0 + 3 });
            }
            return new RampMesh { positions = pos.ToArray(), normals = nor.ToArray(), triangles = tri.ToArray() };
        }

        static float TriArea(Vector3 a, Vector3 b, Vector3 c) => Vector3.Cross(b - a, c - a).Length() * 0.5f;
    }

    /// <summary>One collider found in a built scene, reduced to what the match needs (the editor lint fills it).</summary>
    public sealed class PlacedCollider
    {
        public string path = "";
        public Vector3 min, max;
        /// <summary>The collider's MovementSurface says wall-runnable.</summary>
        public bool runnable;
        /// <summary>The collider is on the glass layer (sight and bullets pass).</summary>
        public bool glass;
    }

    /// <summary>The <c>FloorDataMatch</c> lint rule as a pure function (docs/SKYLINE-PLAN-FLOOR1.md Task B1): every
    /// <see cref="FloorSolid"/> has exactly one collider with equal bounds (to a centimetre), equal runnable flag and, for
    /// glass, on the glass layer; and every collider has a solid. The editor lint gathers the colliders and calls this.</summary>
    public static class FloorDataMatch
    {
        public const float Tolerance = 0.01f;

        public static List<string> Match(IList<FloorSolid> solids, IList<PlacedCollider> colliders)
        {
            var problems = new List<string>();
            var used = new bool[colliders.Count];
            foreach (var s in solids)
            {
                int best = -1, anyBounds = -1;
                for (int i = 0; i < colliders.Count; i++)
                {
                    if (used[i] || !SameBounds(s.box, colliders[i])) continue;
                    if (anyBounds < 0) anyBounds = i;
                    if (colliders[i].runnable == s.runnable && colliders[i].glass == s.glass) { best = i; break; }
                }
                if (best < 0 && anyBounds < 0)
                {
                    problems.Add("FloorDataMatch: no collider for solid " + s.id + " " + Describe(s.box));
                    continue;
                }
                if (best < 0)
                {
                    var c = colliders[anyBounds];
                    used[anyBounds] = true;
                    if (c.runnable != s.runnable)
                        problems.Add("FloorDataMatch: " + s.id + " runnable is " + c.runnable + " in the scene, " + s.runnable + " in the data (" + c.path + ")");
                    if (c.glass != s.glass)
                        problems.Add("FloorDataMatch: " + s.id + " is " + (s.glass ? "glass in the data but not on the glass layer" : "not glass in the data but on the glass layer") + " (" + c.path + ")");
                    continue;
                }
                used[best] = true;
            }
            for (int i = 0; i < colliders.Count; i++)
                if (!used[i]) problems.Add("FloorDataMatch: no solid for collider " + colliders[i].path + " " + Describe(colliders[i].min, colliders[i].max));
            return problems;
        }

        static bool SameBounds(CoverBox a, PlacedCollider c) =>
            Math.Abs(a.min.X - c.min.X) <= Tolerance && Math.Abs(a.min.Y - c.min.Y) <= Tolerance && Math.Abs(a.min.Z - c.min.Z) <= Tolerance &&
            Math.Abs(a.max.X - c.max.X) <= Tolerance && Math.Abs(a.max.Y - c.max.Y) <= Tolerance && Math.Abs(a.max.Z - c.max.Z) <= Tolerance;

        static string Describe(CoverBox b) => Describe(b.min, b.max);

        static string Describe(Vector3 min, Vector3 max) =>
            "(" + F(min.X) + "," + F(min.Y) + "," + F(min.Z) + ")-(" + F(max.X) + "," + F(max.Y) + "," + F(max.Z) + ")";

        static string F(float v) => v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
    }
}
