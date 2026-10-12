using System;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

namespace JamKit.DreamEscape.EditorTools
{
    /// <summary>
    /// Shared drawing pieces of the hill's kit (HillHouses, HillLandmarks, HillProps, HillFences, HillTrees): conversions
    /// from the core's System.Numerics data, seeded randomness, frames, a planar wall face with rectangular holes, the
    /// window (reveal, frame, glass, muntins, casing, sill, the lit or dim room behind it), the door, signs that read the
    /// right way round in mirrored frames, extrusions, leaf and grass cards, painted numerals.
    /// <para><b>Wall frames:</b> the window, door and wall routines draw in a frame whose x runs along the wall, y up, and
    /// z out of the wall (the wall's outer face at z = 0). Push such a frame first (<see cref="WallFrame"/>).</para>
    /// </summary>
    public static class HillKit
    {
        // ---------- Data and randomness ----------

        public static Vector3 U(System.Numerics.Vector3 v) => new Vector3(v.X, v.Y, v.Z);
        public static Vector2 U(System.Numerics.Vector2 v) => new Vector2(v.X, v.Y);

        public static float R(Random r, float a, float b) => a + (float)r.NextDouble() * (b - a);
        public static bool Chance(Random r, float p) => r.NextDouble() < p;
        public static T Pick<T>(Random r, IList<T> list) => list[r.Next(list.Count)];
        public static Vector3 OnSphere(Random r)
        {
            for (int i = 0; i < 16; i++)
            {
                var v = new Vector3(R(r, -1f, 1f), R(r, -1f, 1f), R(r, -1f, 1f));
                float s = v.sqrMagnitude;
                if (s > 0.01f && s <= 1f) return v / Mathf.Sqrt(s);
            }
            return Vector3.up;
        }

        /// <summary>A cheap smooth 3D noise in [-1, 1] (sum of sines), for lumpy foliage and stones.</summary>
        public static float Noise(Vector3 p, float seed)
        {
            return (Mathf.Sin(p.x * 1.7f + seed) * Mathf.Sin(p.y * 2.3f + seed * 1.3f) * Mathf.Sin(p.z * 1.9f + seed * 0.7f)
                + 0.5f * Mathf.Sin(p.x * 3.9f - seed * 0.4f + p.z * 2.1f) * Mathf.Sin(p.y * 4.3f + seed)) / 1.5f;
        }

        // ---------- Frames ----------

        /// <summary>A matrix with the given axes as its columns (any handedness) and its origin.</summary>
        public static Matrix4x4 Frame(Vector3 origin, Vector3 x, Vector3 y, Vector3 z)
        {
            var m = Matrix4x4.identity;
            m.SetColumn(0, new Vector4(x.x, x.y, x.z, 0f));
            m.SetColumn(1, new Vector4(y.x, y.y, y.z, 0f));
            m.SetColumn(2, new Vector4(z.x, z.y, z.z, 0f));
            m.SetColumn(3, new Vector4(origin.x, origin.y, origin.z, 1f));
            return m;
        }

        /// <summary>A wall frame in the current space: origin <paramref name="origin"/> (on the wall's outer face, at its
        /// left end, y = 0), x along <paramref name="along"/> (horizontal), y up, z = <paramref name="outward"/>.</summary>
        public static Matrix4x4 WallFrame(Vector3 origin, Vector3 along, Vector3 outward) =>
            Frame(origin, along.normalized, Vector3.up, outward.normalized);

        // ---------- Signs and panels ----------

        /// <summary>A rectangle with an atlas cell that reads the right way round whatever the frame's handedness
        /// (house frames are mirrored): u runs to the viewer's right.</summary>
        public static void Panel(HillMesh m, string mat, Vector3 center, Vector3 right, Vector3 up, Vector3 facing, Rect uv)
        {
            var M = m.Matrix;
            Vector3 wr = M.MultiplyVector(right), wu = M.MultiplyVector(up), wf = M.MultiplyVector(facing);
            if (Vector3.Dot(wr, Vector3.Cross(wf, wu)) < 0f) uv = new Rect(uv.xMax, uv.yMin, -uv.width, uv.height);
            m.Rect(mat, center, right, up, facing, uv);
        }

        /// <summary>A sub-rectangle of an atlas cell (fractions of the cell).</summary>
        public static Rect Sub(Rect r, float u0, float v0, float u1, float v1) =>
            new Rect(r.xMin + r.width * u0, r.yMin + r.height * v0, r.width * (u1 - u0), r.height * (v1 - v0));

        // ---------- Extrusions ----------

        /// <summary>A convex outline (2D, in the plane of <paramref name="ax"/> and <paramref name="ay"/>) extruded along
        /// <paramref name="az"/> from <paramref name="z0"/> to <paramref name="z1"/>: sides and both caps (caps optional).</summary>
        public static void Extrude(HillMesh m, string mat, IList<Vector2> outline, Vector3 ax, Vector3 ay, Vector3 az, float z0, float z1,
            bool cap0 = true, bool cap1 = true, string sideMat = null)
        {
            int n = outline.Count;
            if (n < 3) return;
            var c = Vector2.zero;
            foreach (var o in outline) c += o;
            c /= n;
            Vector3 P(Vector2 p, float z) => ax * p.x + ay * p.y + az * z;
            for (int i = 0; i < n; i++)
            {
                var a = outline[i];
                var b = outline[(i + 1) % n];
                if ((b - a).sqrMagnitude < 1e-8f) continue;
                var mid = (a + b) * 0.5f - c;
                m.Quad(sideMat ?? mat, P(a, z0), P(a, z1), P(b, z1), P(b, z0), ax * mid.x + ay * mid.y);
            }
            var c0 = new Vector3[n];
            var c1 = new Vector3[n];
            for (int i = 0; i < n; i++) { c0[i] = P(outline[i], z0); c1[i] = P(outline[i], z1); }
            if (cap0) m.Poly(mat, c0, -az * Mathf.Sign(z1 - z0));
            if (cap1) m.Poly(mat, c1, az * Mathf.Sign(z1 - z0));
        }

        /// <summary>A thin flat strip between two points, seen from both sides (two quads): wires, chain-link strands.</summary>
        public static void Strip(HillMesh m, string mat, Vector3 a, Vector3 b, float width, Vector3 facing)
        {
            var d = b - a;
            var side = Vector3.Cross(facing, d).normalized * (width * 0.5f);
            m.Quad(mat, a - side, a + side, b + side, b - side, facing);
            m.Quad(mat, a - side, a + side, b + side, b - side, -facing);
        }

        /// <summary>A torus (tyres, hose coils, rings): <paramref name="seg"/> segments round, <paramref name="sides"/> round
        /// the tube, in the plane perpendicular to <paramref name="axis"/>.</summary>
        public static void Torus(HillMesh m, string mat, Vector3 center, Vector3 axis, float radius, float tube, int seg = 16, int sides = 6)
        {
            axis = axis.normalized;
            var u = Vector3.Cross(axis, Mathf.Abs(axis.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
            var v = Vector3.Cross(axis, u);
            var path = new Vector3[seg + 1];
            var rad = new float[seg + 1];
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                path[i] = center + (u * Mathf.Cos(a) + v * Mathf.Sin(a)) * radius;
                rad[i] = tube;
            }
            m.Tube(mat, path, rad, sides, false, 1f);
        }

        /// <summary>A disc (regular polygon) facing <paramref name="normal"/>.</summary>
        public static void Disc(HillMesh m, string mat, Vector3 center, Vector3 normal, float radius, int sides = 12)
        {
            normal = normal.normalized;
            var u = Vector3.Cross(normal, Mathf.Abs(normal.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
            var v = Vector3.Cross(normal, u);
            var pts = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float a = i / (float)sides * Mathf.PI * 2f;
                pts[i] = center + (u * Mathf.Cos(a) + v * Mathf.Sin(a)) * radius;
            }
            m.Poly(mat, pts, normal);
        }

        /// <summary>A planar roof slab: its top polygon (convex) on the collider's slope, its underside
        /// <paramref name="th"/> below, the edges flagged in <paramref name="outer"/> faced in <paramref name="edge"/>
        /// (edge i runs from point i to point i + 1).</summary>
        public static void Slab(HillMesh m, IList<Vector3> top, bool[] outer, string topMat, string underMat, string edge, float th)
        {
            int n = top.Count;
            var bot = new Vector3[n];
            var c = Vector3.zero;
            for (int i = 0; i < n; i++) { bot[i] = top[i] - Vector3.up * th; c += top[i]; }
            c /= n;
            m.Poly(topMat, top, Vector3.up);
            m.Poly(underMat, bot, Vector3.down);
            for (int i = 0; i < n; i++)
            {
                if (outer != null && !outer[i]) continue;
                int j = (i + 1) % n;
                var mid = (top[i] + top[j]) * 0.5f;
                m.Quad(edge, top[i], top[j], bot[j], bot[i], new Vector3(mid.x - c.x, 0f, mid.z - c.z));
            }
        }

        /// <summary>A pointed (equilateral) arch's outline from (x0, sill) to (x1, sill), springing at
        /// <paramref name="spring"/>: the two jambs' feet, then the right arc up to the apex and the left arc down.</summary>
        public static List<Vector2> LancetOutline(float x0, float x1, float sill, float spring, int seg = 6)
        {
            float w = x1 - x0;
            var pts = new List<Vector2> { new Vector2(x0, sill), new Vector2(x1, sill) };
            for (int i = 0; i <= seg; i++)
            {
                float a = Mathf.Deg2Rad * (60f * i / seg);
                pts.Add(new Vector2(x0 + Mathf.Cos(a) * w, spring + Mathf.Sin(a) * w));
            }
            for (int i = 1; i <= seg; i++)
            {
                float a = Mathf.Deg2Rad * (120f + 60f * i / seg);
                pts.Add(new Vector2(x1 + Mathf.Cos(a) * w, spring + Mathf.Sin(a) * w));
            }
            return pts;
        }

        /// <summary>
        /// A pointed-arch window or doorway in a wall frame (cut its bounding rectangle out of the wall face): the wall
        /// filled back round the arch, the reveal (jambs and arch) <paramref name="depth"/> deep, a stone sill, the glass
        /// on the lancet outline and the room behind it; returns the outline (for tracery).
        /// </summary>
        public static List<Vector2> Lancet(HillMesh m, float x0, float x1, float sill, float spring, float depth, string wall, string reveal,
            string glass, string room, Rect roomUV, bool sillStone = true)
        {
            float w = x1 - x0, apex = spring + w * 0.8660254f, cx = (x0 + x1) * 0.5f;
            var o = LancetOutline(x0, x1, sill, spring);
            int seg = (o.Count - 2) / 2;
            // Fill the corners above the springing back to the wall (fans from the hole's top corners).
            var tl = new Vector3(x0, apex, 0f);
            var tr = new Vector3(x1, apex, 0f);
            for (int i = 2; i < 2 + seg; i++)
                m.Tri(wall, tr, new Vector3(o[i].x, o[i].y, 0f), new Vector3(o[i + 1].x, o[i + 1].y, 0f), Vector3.forward);
            for (int i = 2 + seg; i < o.Count - 1; i++)
                m.Tri(wall, tl, new Vector3(o[i].x, o[i].y, 0f), new Vector3(o[i + 1].x, o[i + 1].y, 0f), Vector3.forward);
            // The reveal round the outline (not along the sill).
            var ring = new List<Vector2> { new Vector2(x1, sill) };
            for (int i = 2; i < o.Count; i++) ring.Add(o[i]);
            ring.Add(new Vector2(x0, spring));
            ring.Add(new Vector2(x0, sill));
            for (int i = 0; i + 1 < ring.Count; i++)
            {
                var a = ring[i];
                var b = ring[i + 1];
                var mid = (a + b) * 0.5f;
                var inward = new Vector2(cx, Mathf.Min(mid.y, spring)) - mid;
                if (inward.sqrMagnitude < 1e-6f) inward = Vector2.down;
                m.Quad(reveal, new Vector3(a.x, a.y, 0f), new Vector3(b.x, b.y, 0f), new Vector3(b.x, b.y, -depth), new Vector3(a.x, a.y, -depth), new Vector3(inward.x, inward.y, 0f));
            }
            if (sillStone) m.Box(HillMat.StoneCoping, new Vector3(x0 - 0.05f, sill - 0.08f, -depth), new Vector3(x1 + 0.05f, sill, 0.06f), 0.012f);
            else m.Quad(reveal, new Vector3(x0, sill, 0f), new Vector3(x1, sill, 0f), new Vector3(x1, sill, -depth), new Vector3(x0, sill, -depth), Vector3.up);
            if (room != null) Room(m, room, roomUV, false, x0, x1, sill, apex, -depth - 0.03f, 1.2f);
            if (glass != null)
            {
                var g = new Vector3[o.Count];
                for (int i = 0; i < o.Count; i++) g[i] = new Vector3(o[i].x, o[i].y, -depth + 0.02f);
                m.Poly(glass, g, Vector3.forward);
            }
            return o;
        }

        /// <summary>A clock face on a wall frame's face, centred at (<paramref name="cx"/>, <paramref name="cy"/>), the
        /// hands at ten to seven (the loop's golden hour).</summary>
        public static void Clock(HillMesh m, float cx, float cy, float radius)
        {
            var c = new Vector3(cx, cy, 0f);
            void Bar(Vector3 a, Vector3 b, float w)
            {
                var side = Vector3.Cross(Vector3.forward, b - a).normalized * (w * 0.5f);
                m.Quad(HillMat.MetalDark, a - side, a + side, b + side, b - side, Vector3.forward);
            }
            m.Cylinder(HillMat.MetalDark, c, c + Vector3.forward * 0.05f, radius * 1.08f, 20, false);
            Disc(m, HillMat.Plaster, c + Vector3.forward * 0.055f, Vector3.forward, radius, 20);
            Torus(m, HillMat.DoorYellow, c + Vector3.forward * 0.06f, Vector3.forward, radius * 1.02f, radius * 0.04f, 20, 4);
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI / 6f;
                var d = new Vector3(Mathf.Sin(a), Mathf.Cos(a), 0f);
                float l = i % 3 == 0 ? 0.16f : 0.09f;
                Bar(c + d * radius * (0.92f - l) + Vector3.forward * 0.062f, c + d * radius * 0.92f + Vector3.forward * 0.062f, radius * 0.045f);
            }
            float hA = (6f + 50f / 60f) / 12f * Mathf.PI * 2f, mA = 50f / 60f * Mathf.PI * 2f;
            Bar(c + Vector3.forward * 0.07f, c + new Vector3(Mathf.Sin(hA), Mathf.Cos(hA), 0f) * radius * 0.52f + Vector3.forward * 0.07f, radius * 0.07f);
            Bar(c + Vector3.forward * 0.078f, c + new Vector3(Mathf.Sin(mA), Mathf.Cos(mA), 0f) * radius * 0.8f + Vector3.forward * 0.078f, radius * 0.045f);
            m.Cylinder(HillMat.DoorYellow, c + Vector3.forward * 0.06f, c + Vector3.forward * 0.1f, radius * 0.06f, 8, true);
        }

        /// <summary>A diagonal lattice of slats over the rectangle (x0..x1, y0..y1) in the plane z, facing +z (porch
        /// skirts, trellis tops); the second direction sits 6 mm in front of the first.</summary>
        public static void Lattice(HillMesh m, string mat, float x0, float x1, float y0, float y1, float z, float spacing, float width)
        {
            float hw = width * 0.70710678f;
            for (int dir = 0; dir < 2; dir++)
            {
                float zz = z + dir * 0.006f;
                float cMin = dir == 0 ? x0 - y1 : x0 + y0, cMax = dir == 0 ? x1 - y0 : x1 + y1;
                for (float c = cMin + spacing * 0.5f; c < cMax; c += spacing)
                {
                    float xa, xb;
                    if (dir == 0) { xa = Mathf.Max(x0, y0 + c); xb = Mathf.Min(x1, y1 + c); }
                    else { xa = Mathf.Max(x0, c - y1); xb = Mathf.Min(x1, c - y0); }
                    if (xb - xa < 0.01f) continue;
                    float ya = dir == 0 ? xa - c : c - xa, yb = dir == 0 ? xb - c : c - xb;
                    // A strip of constant horizontal width (clipped at the rectangle's edges by its endpoints).
                    m.Quad(mat, new Vector3(xa - hw, ya, zz), new Vector3(xa + hw, ya, zz), new Vector3(xb + hw, yb, zz), new Vector3(xb - hw, yb, zz), Vector3.forward);
                }
            }
        }

        // ---------- Plants ----------

        /// <summary>A leaf-cluster card centred at <paramref name="p"/>, its face toward <paramref name="facing"/>, spun by
        /// <paramref name="roll"/> radians, shaded with <paramref name="normal"/> (bent outward for soft foliage).</summary>
        public static void LeafCard(HillMesh m, string mat, Vector3 p, Vector3 facing, float size, float roll, Rect uv, Vector3 normal)
        {
            facing = facing.sqrMagnitude > 1e-6f ? facing.normalized : Vector3.up;
            var t = Vector3.Cross(facing, Mathf.Abs(facing.y) > 0.95f ? Vector3.forward : Vector3.up).normalized;
            var b = Vector3.Cross(facing, t);
            var right = (t * Mathf.Cos(roll) + b * Mathf.Sin(roll)) * size;
            var up = Vector3.Cross(right.normalized, facing) * size;   // Cross(up, right) = facing
            m.Card(mat, p - up * 0.5f, right, up, uv, normal);
        }

        /// <summary>A crossed pair of upright cards rooted at <paramref name="root"/> (grass tufts, flowers).</summary>
        public static void Tuft(HillMesh m, string mat, Vector3 root, float width, float height, float yaw, Rect uv, float lean = 0f)
        {
            var d1 = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw));
            var d2 = new Vector3(-d1.z, 0f, d1.x);
            var up = new Vector3(d2.x * lean, 1f, d2.z * lean).normalized * height;
            m.Card(mat, root, d1 * width, up, uv, Vector3.up);
            var up2 = new Vector3(d1.x * lean, 1f, d1.z * lean).normalized * height;
            m.Card(mat, root, d2 * width, up2, uv, Vector3.up);
        }

        // ---------- Walls with openings ----------

        /// <summary>
        /// A planar wall face in a wall frame (z = 0, facing +z): a convex <paramref name="outline"/> (x along the wall,
        /// y up) with rectangular <paramref name="holes"/> (each inside the outline, not overlapping), tessellated in
        /// horizontal bands so no triangle crosses a hole.
        /// </summary>
        public static void WallFace(HillMesh m, string mat, IList<Vector2> outline, IList<Rect> holes, float z = 0f)
        {
            if (outline.Count < 3) return;
            float yMin = float.MaxValue, yMax = float.MinValue;
            var ys = new List<float>();
            foreach (var p in outline) { yMin = Mathf.Min(yMin, p.y); yMax = Mathf.Max(yMax, p.y); ys.Add(p.y); }
            if (holes != null)
                foreach (var h in holes) { ys.Add(Mathf.Clamp(h.yMin, yMin, yMax)); ys.Add(Mathf.Clamp(h.yMax, yMin, yMax)); }
            ys.Sort();
            var levels = new List<float>();
            foreach (var y in ys) if (levels.Count == 0 || y - levels[levels.Count - 1] > 1e-4f) levels.Add(y);
            var inBand = new List<Rect>();
            for (int i = 0; i + 1 < levels.Count; i++)
            {
                float ya = levels[i], yb = levels[i + 1];
                var sa = Span(outline, ya + 1e-5f);
                var sb = Span(outline, yb - 1e-5f);
                inBand.Clear();
                if (holes != null)
                    foreach (var h in holes)
                        if (h.yMin <= ya + 1e-4f && h.yMax >= yb - 1e-4f) inBand.Add(h);
                inBand.Sort((p, q) => p.xMin.CompareTo(q.xMin));
                float la = sa.x, lb = sb.x;
                foreach (var h in inBand)
                {
                    if (h.xMin > Mathf.Min(la, lb) + 1e-4f)
                        m.Quad(mat, new Vector3(la, ya, z), new Vector3(lb, yb, z), new Vector3(h.xMin, yb, z), new Vector3(h.xMin, ya, z), Vector3.forward);
                    la = lb = h.xMax;
                }
                if (sa.y > la + 1e-4f || sb.y > lb + 1e-4f)
                {
                    if (Mathf.Abs(sa.y - la) < 1e-4f)
                        m.Tri(mat, new Vector3(la, ya, z), new Vector3(lb, yb, z), new Vector3(sb.y, yb, z), Vector3.forward);
                    else if (Mathf.Abs(sb.y - lb) < 1e-4f)
                        m.Tri(mat, new Vector3(la, ya, z), new Vector3(lb, yb, z), new Vector3(sa.y, ya, z), Vector3.forward);
                    else
                        m.Quad(mat, new Vector3(la, ya, z), new Vector3(lb, yb, z), new Vector3(sb.y, yb, z), new Vector3(sa.y, ya, z), Vector3.forward);
                }
            }
        }

        /// <summary>The x range of a convex outline at height y.</summary>
        public static Vector2 Span(IList<Vector2> outline, float y)
        {
            float lo = float.MaxValue, hi = float.MinValue;
            for (int i = 0; i < outline.Count; i++)
            {
                var a = outline[i];
                var b = outline[(i + 1) % outline.Count];
                if ((y < Mathf.Min(a.y, b.y) - 1e-5f) || (y > Mathf.Max(a.y, b.y) + 1e-5f)) continue;
                if (Mathf.Abs(b.y - a.y) < 1e-6f) { lo = Mathf.Min(lo, Mathf.Min(a.x, b.x)); hi = Mathf.Max(hi, Mathf.Max(a.x, b.x)); continue; }
                float x = a.x + (b.x - a.x) * (y - a.y) / (b.y - a.y);
                lo = Mathf.Min(lo, x);
                hi = Mathf.Max(hi, x);
            }
            if (lo > hi) return new Vector2(0f, 0f);
            return new Vector2(lo, hi);
        }

        /// <summary>Whether a rectangle lies inside a convex outline with a margin.</summary>
        public static bool Fits(IList<Vector2> outline, Rect r, float margin)
        {
            foreach (float y in new[] { r.yMin - margin, r.yMax + margin })
            {
                var s = Span(outline, y);
                if (s.y - s.x < 1e-3f || r.xMin - margin < s.x || r.xMax + margin > s.y) return false;
            }
            return true;
        }

        // ---------- Windows ----------

        public sealed class Win
        {
            public float x0, x1, y0, y1;
            /// <summary>How deep the glass sits in the wall (the reveal).</summary>
            public float depth = 0.12f;
            /// <summary>0 plain, 1 sash (meeting rail), 2 sash with grids, 3 casement mullion, 4 casement and transom, 5 a
            /// grid of small panes (cottage), 6 a three-light picture window.</summary>
            public int muntins = 1;
            /// <summary>0 wood casing with a head (sidings), 1 brick lintel and stone sill, 2 a rendered surround, 3 frame
            /// only (modern), 4 nothing on the wall (dormers, turrets: the caller trims).</summary>
            public int casing;
            public string wall = HillMat.Stucco, reveal = HillMat.TrimWhite, trim = HillMat.TrimWhite, frame = HillMat.TrimWhite,
                glass = HillMat.Glass, room = HillMat.Interior, curtain = HillMat.Canvas, shutter = HillMat.TrimDark, box = HillMat.FenceWood;
            public Rect roomUV = new Rect(0f, 0f, 1f, 1f);
            public bool roomFlip, curtains, headCap = true, sill = true, shutters, flowerBox;
            public float roomDepth = 0.9f, frameW = 0.055f;
            public float Width => x1 - x0;
            public float Height => y1 - y0;
            public Rect Hole => new Rect(x0, y0, x1 - x0, y1 - y0);
            /// <summary>The rectangle the casing, shutters and flower box need on the wall (for layout checks).</summary>
            public Rect Footprint
            {
                get
                {
                    float side = casing == 0 ? 0.11f : casing == 1 ? 0.12f : casing == 2 ? 0.12f : 0.02f;
                    if (shutters) side += Width * 0.5f + 0.04f;
                    float below = flowerBox ? 0.34f : casing == 0 ? 0.24f : 0.1f;
                    float above = casing == 0 ? (headCap ? 0.2f : 0.15f) : casing == 1 ? 0.24f : 0.12f;
                    return new Rect(x0 - side, y0 - below, Width + side * 2f, Height + below + above);
                }
            }
        }

        /// <summary>A window in a wall frame (the hole <see cref="Win.Hole"/> must be left out of the wall face).</summary>
        public static void Window(HillMesh m, Win w, Random r)
        {
            float x0 = w.x0, x1 = w.x1, y0 = w.y0, y1 = w.y1, d = w.depth, f = w.frameW;
            float W = x1 - x0, H = y1 - y0;
            if (W < 0.15f || H < 0.15f) return;
            // The reveal.
            m.Quad(w.reveal, new Vector3(x0, y0, 0f), new Vector3(x0, y1, 0f), new Vector3(x0, y1, -d), new Vector3(x0, y0, -d), Vector3.right);
            m.Quad(w.reveal, new Vector3(x1, y0, 0f), new Vector3(x1, y1, 0f), new Vector3(x1, y1, -d), new Vector3(x1, y0, -d), Vector3.left);
            m.Quad(w.reveal, new Vector3(x0, y1, 0f), new Vector3(x1, y1, 0f), new Vector3(x1, y1, -d), new Vector3(x0, y1, -d), Vector3.down);
            if (!w.sill || w.casing == 4)
                m.Quad(w.reveal, new Vector3(x0, y0, 0f), new Vector3(x1, y0, 0f), new Vector3(x1, y0, -d), new Vector3(x0, y0, -d), Vector3.up);
            // The frame.
            float fz0 = -d - 0.012f, fz1 = -d + 0.05f;
            const HillMesh.Side back = HillMesh.Side.ZNeg;
            m.Box(w.frame, new Vector3(x0, y0, fz0), new Vector3(x0 + f, y1, fz1), 0f, back | HillMesh.Side.XNeg);
            m.Box(w.frame, new Vector3(x1 - f, y0, fz0), new Vector3(x1, y1, fz1), 0f, back | HillMesh.Side.XPos);
            m.Box(w.frame, new Vector3(x0 + f, y1 - f, fz0), new Vector3(x1 - f, y1, fz1), 0f, back | HillMesh.Side.XNeg | HillMesh.Side.XPos | HillMesh.Side.YPos);
            m.Box(w.frame, new Vector3(x0 + f, y0, fz0), new Vector3(x1 - f, y0 + f, fz1), 0f, back | HillMesh.Side.XNeg | HillMesh.Side.XPos | HillMesh.Side.YNeg);
            float gx0 = x0 + f, gx1 = x1 - f, gy0 = y0 + f, gy1 = y1 - f, gz = -d + 0.018f;
            // The room behind (a box opening out from the glass: back wall the room picture, sides its edges).
            Room(m, w.room, w.roomUV, w.roomFlip, gx0, gx1, gy0, gy1, fz0 - 0.004f, w.roomDepth);
            if (w.curtains)
            {
                float cw = W * R(r, 0.12f, 0.24f);
                float cz0 = -d - 0.1f, cz1 = -d - 0.06f;
                m.Box(w.curtain, new Vector3(gx0, gy0 + 0.04f, cz0), new Vector3(gx0 + cw, gy1, cz1), 0f, back);
                m.Box(w.curtain, new Vector3(gx1 - cw * R(r, 0.7f, 1.1f), gy0 + 0.04f, cz0), new Vector3(gx1, gy1, cz1), 0f, back);
            }
            // The glass and its bars.
            m.Quad(w.glass, new Vector3(gx0, gy0, gz), new Vector3(gx0, gy1, gz), new Vector3(gx1, gy1, gz), new Vector3(gx1, gy0, gz), Vector3.forward);
            Muntins(m, w.frame, w.muntins, gx0, gx1, gy0, gy1, gz - 0.008f, gz + 0.022f);
            // On the wall.
            switch (w.casing)
            {
                case 0:
                {
                    float cw = 0.09f;
                    m.Box(w.trim, new Vector3(x0 - cw, y0, 0f), new Vector3(x0, y1, 0.025f), 0f, back);
                    m.Box(w.trim, new Vector3(x1, y0, 0f), new Vector3(x1 + cw, y1, 0.025f), 0f, back);
                    m.Box(w.trim, new Vector3(x0 - cw - 0.015f, y1, 0f), new Vector3(x1 + cw + 0.015f, y1 + 0.14f, 0.032f), 0.006f, back);
                    if (w.headCap) m.Box(w.trim, new Vector3(x0 - cw - 0.04f, y1 + 0.14f, 0f), new Vector3(x1 + cw + 0.04f, y1 + 0.185f, 0.065f), 0.012f, back);
                    if (w.sill)
                    {
                        m.Box(w.trim, new Vector3(x0 - cw - 0.035f, y0 - 0.05f, -d), new Vector3(x1 + cw + 0.035f, y0, 0.065f), 0.01f);
                        if (!w.flowerBox) m.Box(w.trim, new Vector3(x0 - cw + 0.02f, y0 - 0.19f, 0f), new Vector3(x1 + cw - 0.02f, y0 - 0.05f, 0.02f), 0f, back);
                    }
                    break;
                }
                case 1:
                    m.Box(HillMat.StoneCoping, new Vector3(x0 - 0.1f, y1, 0f), new Vector3(x1 + 0.1f, y1 + 0.2f, 0.018f), 0.008f, back);
                    if (w.sill) m.Box(HillMat.StoneCoping, new Vector3(x0 - 0.06f, y0 - 0.07f, -d), new Vector3(x1 + 0.06f, y0, 0.055f), 0.01f);
                    break;
                case 2:
                {
                    float cw = 0.1f;
                    m.Box(w.trim, new Vector3(x0 - cw, y0, 0f), new Vector3(x0, y1 + cw, 0.016f), 0.004f, back);
                    m.Box(w.trim, new Vector3(x1, y0, 0f), new Vector3(x1 + cw, y1 + cw, 0.016f), 0.004f, back);
                    m.Box(w.trim, new Vector3(x0, y1, 0f), new Vector3(x1, y1 + cw, 0.016f), 0f, back | HillMesh.Side.XNeg | HillMesh.Side.XPos);
                    if (w.sill) m.Box(HillMat.StoneCoping, new Vector3(x0 - cw - 0.03f, y0 - 0.06f, -d), new Vector3(x1 + cw + 0.03f, y0, 0.05f), 0.01f);
                    break;
                }
                case 3:
                    if (w.sill) m.Box(HillMat.MetalPaint, new Vector3(x0 - 0.02f, y0 - 0.03f, -d), new Vector3(x1 + 0.02f, y0, 0.05f), 0.005f);
                    break;
            }
            if (w.shutters)
            {
                float gap = w.casing == 0 ? 0.11f : 0.04f;
                float sw = W * 0.5f - 0.01f;
                foreach (int s in new[] { -1, 1 })
                {
                    float sx0 = s < 0 ? x0 - gap - sw : x1 + gap, sx1 = sx0 + sw;
                    m.Box(w.shutter, new Vector3(sx0, y0 + 0.01f, 0.004f), new Vector3(sx1, y1 - 0.01f, 0.034f), 0.006f, back);
                    float ph = (H - 0.12f) * 0.5f;
                    for (int k = 0; k < 2; k++)
                    {
                        float py0 = y0 + 0.05f + k * (ph + 0.02f);
                        m.Box(w.shutter, new Vector3(sx0 + 0.05f, py0, 0.034f), new Vector3(sx1 - 0.05f, py0 + ph - 0.02f, 0.046f), 0f, back);
                    }
                }
            }
            if (w.flowerBox)
            {
                float by1 = y0 - 0.07f, by0 = by1 - 0.22f;
                m.Box(w.box, new Vector3(x0 - 0.04f, by0, 0f), new Vector3(x1 + 0.04f, by1, 0.11f), 0.01f, back | HillMesh.Side.YPos);
                m.Box(w.box, new Vector3(x0 - 0.04f, by1 - 0.02f, 0f), new Vector3(x1 + 0.04f, by1, 0.11f), 0f, back | HillMesh.Side.YNeg);
                m.Quad(HillMat.Dirt, new Vector3(x0 - 0.02f, by1 - 0.03f, 0.01f), new Vector3(x1 + 0.02f, by1 - 0.03f, 0.01f),
                    new Vector3(x1 + 0.02f, by1 - 0.03f, 0.1f), new Vector3(x0 - 0.02f, by1 - 0.03f, 0.1f), Vector3.up);
                int n = Mathf.Max(2, Mathf.RoundToInt(W / 0.22f));
                for (int i = 0; i < n; i++)
                {
                    float x = x0 + (i + 0.5f) / n * W + R(r, -0.04f, 0.04f);
                    float hgt = R(r, 0.22f, 0.34f);
                    m.Card(HillMat.Flowers, new Vector3(x, by1 - 0.03f, R(r, 0.035f, 0.075f)), new Vector3(R(r, 0.2f, 0.28f), 0f, R(r, -0.05f, 0.05f)),
                        new Vector3(R(r, -0.04f, 0.04f), hgt, 0.04f), HillAtlas.Tuft(r.Next(4)), Vector3.forward + Vector3.up);
                }
            }
        }

        /// <summary>Glazing bars over a pane from (x0, y0) to (x1, y1), between depths z0 and z1.</summary>
        public static void Muntins(HillMesh m, string mat, int style, float x0, float x1, float y0, float y1, float z0, float z1)
        {
            float W = x1 - x0, H = y1 - y0, b = 0.03f;
            const HillMesh.Side back = HillMesh.Side.ZNeg;
            void V(float x, float ya, float yb, float w) => m.Box(mat, new Vector3(x - w * 0.5f, ya, z0), new Vector3(x + w * 0.5f, yb, z1), 0f, back);
            void Hz(float y, float xa, float xb, float h) => m.Box(mat, new Vector3(xa, y - h * 0.5f, z0), new Vector3(xb, y + h * 0.5f, z1), 0f, back);
            switch (style)
            {
                case 1:
                    Hz(y0 + H * 0.5f, x0, x1, 0.05f);
                    break;
                case 2:
                {
                    float ym = y0 + H * 0.5f;
                    Hz(ym, x0, x1, 0.05f);
                    int cols = W > 0.95f ? 3 : 2;
                    for (int i = 1; i < cols; i++) V(x0 + W * i / cols, y0, y1, b);
                    Hz(y0 + H * 0.25f, x0, x1, b);
                    Hz(y0 + H * 0.75f, x0, x1, b);
                    break;
                }
                case 3:
                    if (W > 0.75f) V(x0 + W * 0.5f, y0, y1, 0.06f);
                    break;
                case 4:
                    if (W > 0.75f) V(x0 + W * 0.5f, y0, y1 - H * 0.27f, 0.06f);
                    Hz(y1 - H * 0.27f, x0, x1, 0.06f);
                    break;
                case 5:
                {
                    int cols = Mathf.Max(2, Mathf.RoundToInt(W / 0.3f)), rows = Mathf.Max(2, Mathf.RoundToInt(H / 0.35f));
                    for (int i = 1; i < cols; i++) V(x0 + W * i / cols, y0, y1, b);
                    for (int j = 1; j < rows; j++) Hz(y0 + H * j / rows, x0, x1, b);
                    break;
                }
                case 6:
                    V(x0 + W * 0.27f, y0, y1, 0.07f);
                    V(x1 - W * 0.27f, y0, y1, 0.07f);
                    break;
            }
        }

        /// <summary>The room seen through an opening: a box from the opening rectangle at <paramref name="zNear"/> opening
        /// out to a back wall <paramref name="depth"/> further in, the room's picture on the back and its edges on the
        /// sides (unlit material: the room glows faintly or sits dim by the picture's own light).</summary>
        public static void Room(HillMesh m, string mat, Rect uv, bool flip, float x0, float x1, float y0, float y1, float zNear, float depth)
        {
            float W = x1 - x0, H = y1 - y0;
            float ex = Mathf.Min(depth * 0.45f, W * 0.6f), ey = Mathf.Min(depth * 0.3f, H * 0.35f);
            float zf = zNear - depth;
            var n00 = new Vector3(x0, y0, zNear); var n01 = new Vector3(x0, y1, zNear); var n11 = new Vector3(x1, y1, zNear); var n10 = new Vector3(x1, y0, zNear);
            var f00 = new Vector3(x0 - ex, y0 - ey, zf); var f01 = new Vector3(x0 - ex, y1 + ey, zf); var f11 = new Vector3(x1 + ex, y1 + ey, zf); var f10 = new Vector3(x1 + ex, y0 - ey, zf);
            float u0 = flip ? uv.xMax : uv.xMin, u1 = flip ? uv.xMin : uv.xMax, v0 = uv.yMin, v1 = uv.yMax;
            float su = (u1 - u0) * 0.07f, sv = (v1 - v0) * 0.07f;
            m.PolyUV(mat, new[] { f00, f01, f11, f10 }, new[] { new Vector2(u0, v0), new Vector2(u0, v1), new Vector2(u1, v1), new Vector2(u1, v0) }, Vector3.forward);
            m.PolyUV(mat, new[] { n00, n01, f01, f00 }, new[] { new Vector2(u0 + su, v0), new Vector2(u0 + su, v1), new Vector2(u0, v1), new Vector2(u0, v0) }, Vector3.right);
            m.PolyUV(mat, new[] { n10, n11, f11, f10 }, new[] { new Vector2(u1 - su, v0), new Vector2(u1 - su, v1), new Vector2(u1, v1), new Vector2(u1, v0) }, Vector3.left);
            m.PolyUV(mat, new[] { n01, n11, f11, f01 }, new[] { new Vector2(u0, v1 - sv), new Vector2(u1, v1 - sv), new Vector2(u1, v1), new Vector2(u0, v1) }, Vector3.down);
            m.PolyUV(mat, new[] { n00, n10, f10, f00 }, new[] { new Vector2(u0, v0 + sv), new Vector2(u1, v0 + sv), new Vector2(u1, v0), new Vector2(u0, v0) }, Vector3.up);
        }

        // ---------- Doors ----------

        public sealed class DoorSpec
        {
            public float x0, x1, height = 2.1f, depth = 0.14f;
            /// <summary>0 six panels, 1 half glass with bars, 2 craftsman (three top lights), 3 a tall narrow light, 4 a
            /// shop door (glass in a painted frame).</summary>
            public int style;
            public bool transom, sidelights;
            /// <summary>0 wood casing with a cap, 1 stone lintel, 2 rendered surround, 3 none.</summary>
            public int casing;
            public string wall = HillMat.Stucco, reveal = HillMat.TrimWhite, trim = HillMat.TrimWhite, leaf = HillMat.DoorRed,
                glass = HillMat.Glass, room = HillMat.Interior;
            public Rect hallUV = HillAtlas.Interior(3);
            public float SideW => sidelights ? 0.34f : 0f;
            public float TransomH => transom ? 0.42f : 0f;
            public Rect Hole => new Rect(x0 - SideW, 0f, x1 - x0 + SideW * 2f, height + TransomH);
        }

        public static void Door(HillMesh m, DoorSpec s, Random r)
        {
            var hole = s.Hole;
            float hx0 = hole.xMin, hx1 = hole.xMax, top = hole.yMax, d = s.depth;
            const HillMesh.Side back = HillMesh.Side.ZNeg;
            m.Quad(s.reveal, new Vector3(hx0, 0f, 0f), new Vector3(hx0, top, 0f), new Vector3(hx0, top, -d), new Vector3(hx0, 0f, -d), Vector3.right);
            m.Quad(s.reveal, new Vector3(hx1, 0f, 0f), new Vector3(hx1, top, 0f), new Vector3(hx1, top, -d), new Vector3(hx1, 0f, -d), Vector3.left);
            m.Quad(s.reveal, new Vector3(hx0, top, 0f), new Vector3(hx1, top, 0f), new Vector3(hx1, top, -d), new Vector3(hx0, top, -d), Vector3.down);
            m.Box(HillMat.StoneCoping, new Vector3(hx0 - 0.03f, -0.04f, -d - 0.02f), new Vector3(hx1 + 0.03f, 0.02f, 0.04f), 0.008f);
            float x0 = s.x0, x1 = s.x1, h = s.height, f = 0.06f, lz0 = -d, lz1 = -d + 0.045f;
            // The frame round the leaf (and the sidelights' and transom's bars).
            m.Box(s.trim, new Vector3(x0 - f, 0f, lz0 - 0.01f), new Vector3(x0, top, lz1 + 0.012f), 0f, back);
            m.Box(s.trim, new Vector3(x1, 0f, lz0 - 0.01f), new Vector3(x1 + f, top, lz1 + 0.012f), 0f, back);
            m.Box(s.trim, new Vector3(x0 - f, h, lz0 - 0.01f), new Vector3(x1 + f, h + f, lz1 + 0.012f), 0f, back);
            if (s.sidelights)
                foreach (int side in new[] { -1, 1 })
                {
                    float a = side < 0 ? hx0 : x1 + f, b = side < 0 ? x0 - f : hx1;
                    m.Box(s.leaf, new Vector3(a, 0f, lz0), new Vector3(b, 0.85f, lz1), 0.006f, back);
                    m.Box(s.leaf, new Vector3(a + 0.04f, 0.12f, lz1), new Vector3(b - 0.04f, 0.72f, lz1 + 0.012f), 0.005f, back);
                    Room(m, s.room, Sub(s.hallUV, side < 0 ? 0f : 0.8f, 0.1f, side < 0 ? 0.2f : 1f, 0.9f), false, a, b, 0.85f, h, lz0 - 0.01f, 1.2f);
                    m.Quad(s.glass, new Vector3(a, 0.85f, lz0 + 0.02f), new Vector3(a, h, lz0 + 0.02f), new Vector3(b, h, lz0 + 0.02f), new Vector3(b, 0.85f, lz0 + 0.02f), Vector3.forward);
                    Muntins(m, s.trim, 5, a, b, 0.85f, h, lz0 + 0.01f, lz0 + 0.04f);
                }
            if (s.transom)
            {
                float ty0 = h + f, ty1 = top;
                Room(m, s.room, Sub(s.hallUV, 0.2f, 0.75f, 0.8f, 1f), false, hx0, hx1, ty0, ty1, lz0 - 0.01f, 1.2f);
                m.Quad(s.glass, new Vector3(hx0, ty0, lz0 + 0.02f), new Vector3(hx0, ty1, lz0 + 0.02f), new Vector3(hx1, ty1, lz0 + 0.02f), new Vector3(hx1, ty0, lz0 + 0.02f), Vector3.forward);
                int bars = Mathf.Max(2, Mathf.RoundToInt((hx1 - hx0) / 0.3f));
                for (int i = 1; i < bars; i++)
                {
                    float bx = hx0 + (hx1 - hx0) * i / bars;
                    m.Box(s.trim, new Vector3(bx - 0.015f, ty0, lz0 + 0.01f), new Vector3(bx + 0.015f, ty1, lz0 + 0.04f), 0f, back);
                }
            }
            // The leaf.
            float W = x1 - x0;
            void Panel3(float px0, float py0, float px1, float py1) =>
                m.Box(s.leaf, new Vector3(px0, py0, lz1), new Vector3(px1, py1, lz1 + 0.016f), 0.008f, back);
            void Glazed(float gx0, float gy0, float gx1, float gy1, int bars)
            {
                Room(m, s.room, Sub(s.hallUV, 0.15f, 0.1f, 0.85f, 0.95f), Chance(r, 0.5f), gx0, gx1, gy0, gy1, lz0 - 0.01f, 1.4f);
                m.Quad(s.glass, new Vector3(gx0, gy0, lz0 + 0.022f), new Vector3(gx0, gy1, lz0 + 0.022f), new Vector3(gx1, gy1, lz0 + 0.022f), new Vector3(gx1, gy0, lz0 + 0.022f), Vector3.forward);
                if (bars > 0) Muntins(m, s.leaf, bars, gx0, gx1, gy0, gy1, lz0 + 0.012f, lz0 + 0.04f);
            }
            float st = 0.12f;   // stiles and rails
            switch (s.style)
            {
                case 1:
                case 3:
                case 4:
                {
                    float gy0 = s.style == 1 ? 1.0f : s.style == 4 ? 0.3f : 0.25f, gy1 = h - st;
                    float gx0 = s.style == 3 ? x0 + W * 0.36f : x0 + st, gx1 = s.style == 3 ? x1 - W * 0.36f : x1 - st;
                    m.Box(s.leaf, new Vector3(x0, 0f, lz0), new Vector3(gx0, h, lz1), 0f, back);
                    m.Box(s.leaf, new Vector3(gx1, 0f, lz0), new Vector3(x1, h, lz1), 0f, back);
                    m.Box(s.leaf, new Vector3(gx0, 0f, lz0), new Vector3(gx1, gy0, lz1), 0f, back);
                    m.Box(s.leaf, new Vector3(gx0, gy1, lz0), new Vector3(gx1, h, lz1), 0f, back);
                    Glazed(gx0, gy0, gx1, gy1, s.style == 1 ? 5 : 0);
                    if (s.style == 1)
                    {
                        Panel3(x0 + 0.1f, 0.15f, x0 + W * 0.5f - 0.04f, 0.88f);
                        Panel3(x0 + W * 0.5f + 0.04f, 0.15f, x1 - 0.1f, 0.88f);
                    }
                    if (s.style == 4) m.Box(HillMat.Steel, new Vector3(x0 + 0.1f, 0.95f, lz1), new Vector3(x1 - 0.1f, 1.0f, lz1 + 0.04f), 0.01f, back);
                    break;
                }
                case 2:
                {
                    float gy0 = h - 0.62f, gy1 = h - 0.14f;
                    m.Box(s.leaf, new Vector3(x0, 0f, lz0), new Vector3(x1, gy0, lz1), 0f, back);
                    m.Box(s.leaf, new Vector3(x0, gy1, lz0), new Vector3(x1, h, lz1), 0f, back);
                    m.Box(s.leaf, new Vector3(x0, gy0, lz0), new Vector3(x0 + st, gy1, lz1), 0f, back);
                    m.Box(s.leaf, new Vector3(x1 - st, gy0, lz0), new Vector3(x1, gy1, lz1), 0f, back);
                    Glazed(x0 + st, gy0, x1 - st, gy1, 0);
                    float pw = (W - st * 2f) / 3f;
                    for (int i = 1; i < 3; i++)
                        m.Box(s.leaf, new Vector3(x0 + st + pw * i - 0.025f, gy0, lz0), new Vector3(x0 + st + pw * i + 0.025f, gy1, lz1), 0f, back);
                    m.Box(s.trim, new Vector3(x0 + 0.04f, gy0 - 0.06f, lz1), new Vector3(x1 - 0.04f, gy0, lz1 + 0.04f), 0.008f, back);
                    Panel3(x0 + 0.12f, 0.18f, x0 + W * 0.5f - 0.04f, gy0 - 0.18f);
                    Panel3(x0 + W * 0.5f + 0.04f, 0.18f, x1 - 0.12f, gy0 - 0.18f);
                    break;
                }
                default:
                {
                    m.Box(s.leaf, new Vector3(x0, 0f, lz0), new Vector3(x1, h, lz1), 0f, back);
                    float pw = (W - 0.3f) * 0.5f;
                    float[] rows = { 0.15f, 0.82f, 0.98f, 1.5f, 1.62f, h - 0.13f };
                    for (int k = 0; k < 3; k++)
                    {
                        Panel3(x0 + 0.1f, rows[k * 2], x0 + 0.1f + pw, rows[k * 2 + 1]);
                        Panel3(x1 - 0.1f - pw, rows[k * 2], x1 - 0.1f, rows[k * 2 + 1]);
                    }
                    break;
                }
            }
            // Hardware: knob and plate, letter plate, kick plate.
            float hx = x1 - 0.1f;
            m.Box(HillMat.Steel, new Vector3(hx - 0.025f, 0.9f, lz1), new Vector3(hx + 0.025f, 1.12f, lz1 + 0.01f), 0.004f, back);
            m.Cylinder(HillMat.Steel, new Vector3(hx, 1.0f, lz1 + 0.01f), new Vector3(hx, 1.0f, lz1 + 0.065f), 0.028f, 8);
            if (s.style == 0 || s.style == 2)
            {
                m.Box(HillMat.Steel, new Vector3(x0 + W * 0.5f - 0.15f, 0.88f, lz1), new Vector3(x0 + W * 0.5f + 0.15f, 0.93f, lz1 + 0.025f), 0.005f, back);
                m.Box(HillMat.Steel, new Vector3(x0 + 0.03f, 0.02f, lz1), new Vector3(x1 - 0.03f, 0.2f, lz1 + 0.006f), 0f, back);
            }
            // On the wall: the casing.
            switch (s.casing)
            {
                case 0:
                {
                    float cw = 0.11f;
                    m.Box(s.trim, new Vector3(hx0 - cw, 0f, 0f), new Vector3(hx0, top, 0.03f), 0f, back);
                    m.Box(s.trim, new Vector3(hx1, 0f, 0f), new Vector3(hx1 + cw, top, 0.03f), 0f, back);
                    m.Box(s.trim, new Vector3(hx0 - cw - 0.02f, top, 0f), new Vector3(hx1 + cw + 0.02f, top + 0.17f, 0.035f), 0.006f, back);
                    m.Box(s.trim, new Vector3(hx0 - cw - 0.05f, top + 0.17f, 0f), new Vector3(hx1 + cw + 0.05f, top + 0.22f, 0.075f), 0.014f, back);
                    m.Box(s.trim, new Vector3(hx0 - cw - 0.01f, 0f, 0f), new Vector3(hx0 + 0.005f, 0.2f, 0.045f), 0.006f, back);
                    m.Box(s.trim, new Vector3(hx1 - 0.005f, 0f, 0f), new Vector3(hx1 + cw + 0.01f, 0.2f, 0.045f), 0.006f, back);
                    break;
                }
                case 1:
                    m.Box(HillMat.StoneCoping, new Vector3(hx0 - 0.12f, top, 0f), new Vector3(hx1 + 0.12f, top + 0.24f, 0.022f), 0.008f, back);
                    break;
                case 2:
                {
                    float cw = 0.12f;
                    m.Box(s.trim, new Vector3(hx0 - cw, 0f, 0f), new Vector3(hx0, top + cw, 0.018f), 0.004f, back);
                    m.Box(s.trim, new Vector3(hx1, 0f, 0f), new Vector3(hx1 + cw, top + cw, 0.018f), 0.004f, back);
                    m.Box(s.trim, new Vector3(hx0, top, 0f), new Vector3(hx1, top + cw, 0.018f), 0f, back | HillMesh.Side.XNeg | HillMesh.Side.XPos);
                    break;
                }
            }
        }

        // ---------- Numerals ----------

        static readonly int[] Seg = { 0x3F, 0x06, 0x5B, 0x4F, 0x66, 0x6D, 0x7D, 0x07, 0x7F, 0x6F };

        /// <summary>Painted or cast numerals (seven strokes each) on a wall frame's face, bottom-left at
        /// <paramref name="at"/>, <paramref name="h"/> tall, standing <paramref name="proud"/> off the face.</summary>
        public static void Digits(HillMesh m, string mat, string text, Vector3 at, float h, float proud = 0.012f)
        {
            float w = h * 0.55f, t = h * 0.13f, x = at.x;
            foreach (char ch in text)
            {
                if (ch < '0' || ch > '9') { x += w * 0.6f; continue; }
                int bits = Seg[ch - '0'];
                float y0 = at.y, ym = at.y + h * 0.5f, y1 = at.y + h;
                void Hs(float y) => m.Box(mat, new Vector3(x + t * 0.5f, y - t * 0.5f, at.z), new Vector3(x + w - t * 0.5f, y + t * 0.5f, at.z + proud), 0f, HillMesh.Side.ZNeg);
                void Vs(float xx, float ya, float yb) => m.Box(mat, new Vector3(xx - t * 0.5f, ya, at.z), new Vector3(xx + t * 0.5f, yb, at.z + proud), 0f, HillMesh.Side.ZNeg);
                if ((bits & 1) != 0) Hs(y1 - t * 0.5f);
                if ((bits & 2) != 0) Vs(x + w - t * 0.5f, ym, y1);
                if ((bits & 4) != 0) Vs(x + w - t * 0.5f, y0, ym);
                if ((bits & 8) != 0) Hs(y0 + t * 0.5f);
                if ((bits & 16) != 0) Vs(x + t * 0.5f, y0, ym);
                if ((bits & 32) != 0) Vs(x + t * 0.5f, ym, y1);
                if ((bits & 64) != 0) Hs(ym);
                x += w + t;
            }
        }

        // ---------- Ivy ----------

        /// <summary>A patch of ivy cards on a wall frame's face, centred at (<paramref name="cx"/>, <paramref name="cy"/>),
        /// roughly <paramref name="w"/> x <paramref name="h"/>, climbing from the bottom.</summary>
        public static void Ivy(HillMesh m, Random r, float cx, float cy, float w, float h, float density = 6f)
        {
            int n = Mathf.Clamp(Mathf.RoundToInt(w * h * density), 4, 60);
            for (int i = 0; i < n; i++)
            {
                float u = R(r, -0.5f, 0.5f), v = R(r, 0f, 1f);
                float width = w * (1f - v * 0.6f);   // narrower toward the top
                var p = new Vector3(cx + u * width, cy - h * 0.5f + v * h, 0.015f + (i % 30) * 0.0024f);
                float size = R(r, 0.35f, 0.65f);
                float a = R(r, 0f, Mathf.PI * 2f);
                var right = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * size;
                var up = new Vector3(-Mathf.Sin(a), Mathf.Cos(a), 0f) * size;
                m.Card(HillMat.Ivy, p - up * 0.5f, right, up, HillAtlas.Leaf(r.Next(4)), new Vector3(R(r, -0.3f, 0.3f), R(r, 0f, 0.4f), 1f));
            }
        }
    }
}
