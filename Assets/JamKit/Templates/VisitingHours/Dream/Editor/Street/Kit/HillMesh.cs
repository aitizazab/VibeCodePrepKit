using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace JamKit.DreamEscape.EditorTools
{
    /// <summary>
    /// A mesh sink for the hill's procedural geometry (docs/redesign/world.md "How the hill is drawn"): every primitive is
    /// added under the current transform (<see cref="Push"/>/<see cref="Pop"/>) into one sub-mesh per material id
    /// (<see cref="HillMat"/>), and <see cref="Build"/> turns them into one renderer per material (a chunk of the hill is
    /// one HillMesh: few draw calls, frustum-culled by chunk).
    /// <para><b>Winding:</b> Unity's front faces are clockwise as seen from the front. Every primitive here takes an
    /// outward direction (or computes it) and orders its triangles to face it, so callers never think about winding.</para>
    /// <para><b>UVs</b> are in world metres unless given: a face's u runs along its horizontal tangent
    /// (cross(up, normal); along X for floors) and v up the face (up the slope on a roof), from the world position, so
    /// textures run on across neighbouring faces and HillMaterials sets the tiling (1 / Spec.tile).</para>
    /// <para>Visual only: no colliders (the hill's colliders are the core pieces' boxes).</para>
    /// </summary>
    public sealed class HillMesh
    {
        sealed class Part
        {
            public readonly List<Vector3> v = new List<Vector3>();
            public readonly List<Vector3> n = new List<Vector3>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<int> t = new List<int>();
        }

        [Flags]
        public enum Side { None = 0, XNeg = 1, XPos = 2, YNeg = 4, YPos = 8, ZNeg = 16, ZPos = 32, All = 63 }

        public readonly string name;
        readonly Dictionary<string, Part> _parts = new Dictionary<string, Part>();
        readonly Stack<Matrix4x4> _stack = new Stack<Matrix4x4>();
        Matrix4x4 _m = Matrix4x4.identity;
        int _vertexCount;
        readonly bool _discard;

        public HillMesh(string name) { this.name = name; }

        HillMesh(string name, bool discard) { this.name = name; _discard = discard; }

        /// <summary>A sink that keeps nothing (the builder draws the hill in bands of chunks: whatever falls outside the
        /// band being built goes here and costs no memory).</summary>
        public static HillMesh Discarding(string name) => new HillMesh(name, true);

        public bool Discards => _discard;

        /// <summary>
        /// The bevel rule (World round 2, the memory and frame-time fix): a chamfer of less than <see cref="MinBevel"/>, or
        /// on a box thinner than <see cref="MinBevelThickness"/> in any direction (trim, boards, rails, sills), is drawn
        /// sharp. A bevelled box is 96 vertices, a sharp one 24: on milestone 1's hill the chamfers of copings, trim and
        /// fence boards were three quarters of all 5.3M vertices, and a 1 cm chamfer doesn't read past a few metres.
        /// </summary>
        public static float MinBevel = 0.015f, MinBevelThickness = 0.1f;

        public Matrix4x4 Matrix => _m;
        public int VertexCount => _vertexCount;
        public bool Empty => _vertexCount == 0;
        public IEnumerable<string> Materials => _parts.Keys;

        /// <summary>Vertices drawn into material <paramref name="mat"/> so far.</summary>
        public int VerticesOf(string mat) => _parts.TryGetValue(mat, out var p) ? p.v.Count : 0;

        // ---------- Transform ----------

        public void Push(Matrix4x4 local)
        {
            _stack.Push(_m);
            _m = _m * local;
        }

        public void Push(Vector3 position, Quaternion rotation) => Push(Matrix4x4.TRS(position, rotation, Vector3.one));
        public void Push(Vector3 position, float yaw) => Push(Matrix4x4.TRS(position, Quaternion.Euler(0f, yaw, 0f), Vector3.one));

        public void Pop() => _m = _stack.Count > 0 ? _stack.Pop() : Matrix4x4.identity;

        Vector3 P(Vector3 local) => _m.MultiplyPoint3x4(local);
        Vector3 D(Vector3 local) => _m.MultiplyVector(local);

        Part Get(string mat)
        {
            if (!_parts.TryGetValue(mat, out var p)) { p = new Part(); _parts[mat] = p; }
            return p;
        }

        // ---------- UVs ----------

        /// <summary>The world-metre UV of a world point on a face with world normal <paramref name="n"/>.</summary>
        public static Vector2 PlanarUV(Vector3 world, Vector3 n)
        {
            Vector3 t = Mathf.Abs(n.y) > 0.9f ? Vector3.right : Vector3.Cross(Vector3.up, n).normalized;
            Vector3 b = Vector3.Cross(n, t);
            if (Mathf.Abs(n.y) > 0.9f) b = Vector3.forward;   // floors: v along +Z either way up
            else if (b.y < 0f) b = -b;                          // walls and roofs: v goes up
            return new Vector2(Vector3.Dot(world, t), Vector3.Dot(world, b));
        }

        // ---------- Polygons ----------

        /// <summary>A planar convex polygon (local points) facing <paramref name="outward"/> (local; zero: as ordered,
        /// clockwise from the front). UVs world-planar.</summary>
        public void Poly(string mat, IList<Vector3> pts, Vector3 outward = default)
        {
            if (_discard || pts.Count < 3) return;
            var w = new Vector3[pts.Count];
            for (int i = 0; i < w.Length; i++) w[i] = P(pts[i]);
            Vector3 n = Normal(w);
            if (outward != Vector3.zero && Vector3.Dot(n, D(outward)) < 0f) { Array.Reverse(w); n = -n; }
            var part = Get(mat);
            int b0 = part.v.Count;
            for (int i = 0; i < w.Length; i++)
            {
                part.v.Add(w[i]);
                part.n.Add(n);
                part.uv.Add(PlanarUV(w[i], n));
            }
            for (int i = 1; i + 1 < w.Length; i++) { part.t.Add(b0); part.t.Add(b0 + i); part.t.Add(b0 + i + 1); }
            _vertexCount += w.Length;
        }

        /// <summary>A planar convex polygon with its own UVs (cards, signs, the rooms behind windows).</summary>
        public void PolyUV(string mat, IList<Vector3> pts, IList<Vector2> uvs, Vector3 outward = default, Vector3 normalOverride = default)
        {
            if (_discard || pts.Count < 3) return;
            var w = new Vector3[pts.Count];
            var u = new Vector2[pts.Count];
            for (int i = 0; i < w.Length; i++) { w[i] = P(pts[i]); u[i] = uvs[i]; }
            Vector3 n = Normal(w);
            if (outward != Vector3.zero && Vector3.Dot(n, D(outward)) < 0f) { Array.Reverse(w); Array.Reverse(u); n = -n; }
            Vector3 nn = normalOverride != Vector3.zero ? D(normalOverride).normalized : n;
            var part = Get(mat);
            int b0 = part.v.Count;
            for (int i = 0; i < w.Length; i++) { part.v.Add(w[i]); part.n.Add(nn); part.uv.Add(u[i]); }
            for (int i = 1; i + 1 < w.Length; i++) { part.t.Add(b0); part.t.Add(b0 + i); part.t.Add(b0 + i + 1); }
            _vertexCount += w.Length;
        }

        static Vector3 Normal(Vector3[] w)
        {
            // Newell's method on Unity's clockwise-front convention: Cross(b - a, c - a) faces the viewer.
            Vector3 n = Vector3.zero;
            for (int i = 1; i + 1 < w.Length; i++) n += Vector3.Cross(w[i] - w[0], w[i + 1] - w[0]);
            return n.sqrMagnitude > 1e-12f ? n.normalized : Vector3.up;
        }

        public void Quad(string mat, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward = default) => Poly(mat, new[] { a, b, c, d }, outward);
        public void Tri(string mat, Vector3 a, Vector3 b, Vector3 c, Vector3 outward = default) => Poly(mat, new[] { a, b, c }, outward);

        /// <summary>A flat rectangle centred at <paramref name="center"/>, spanning <paramref name="right"/> x
        /// <paramref name="up"/> (half extents are half their lengths), facing <paramref name="facing"/>; optional UV rect.</summary>
        public void Rect(string mat, Vector3 center, Vector3 right, Vector3 up, Vector3 facing, Rect? uv = null)
        {
            var a = center - right * 0.5f - up * 0.5f;
            var b = center - right * 0.5f + up * 0.5f;
            var c = center + right * 0.5f + up * 0.5f;
            var d = center + right * 0.5f - up * 0.5f;
            if (uv == null) { Quad(mat, a, b, c, d, facing); return; }
            var r = uv.Value;
            PolyUV(mat, new[] { a, b, c, d }, new[] { new Vector2(r.xMin, r.yMin), new Vector2(r.xMin, r.yMax), new Vector2(r.xMax, r.yMax), new Vector2(r.xMax, r.yMin) }, facing);
        }

        /// <summary>A two-sided alpha card (grass, leaves, flowers): its own UV rect, its normal bent to
        /// <paramref name="normal"/> (foliage shading) when given.</summary>
        public void Card(string mat, Vector3 center, Vector3 right, Vector3 up, Rect uv, Vector3 normal = default)
        {
            var a = center - right * 0.5f;
            var b = center - right * 0.5f + up;
            var c = center + right * 0.5f + up;
            var d = center + right * 0.5f;
            var facing = Vector3.Cross(up, right);
            var uvs = new[] { new Vector2(uv.xMin, uv.yMin), new Vector2(uv.xMin, uv.yMax), new Vector2(uv.xMax, uv.yMax), new Vector2(uv.xMax, uv.yMin) };
            PolyUV(mat, new[] { a, b, c, d }, uvs, facing, normal);
        }

        // ---------- Boxes ----------

        /// <summary>An axis-aligned (in local space) box from <paramref name="min"/> to <paramref name="max"/>, its edges
        /// chamfered by <paramref name="bevel"/> m (0: sharp), leaving out the sides in <paramref name="skip"/>.</summary>
        public void Box(string mat, Vector3 min, Vector3 max, float bevel = 0f, Side skip = Side.None)
        {
            if (_discard) return;
            var c = (min + max) * 0.5f;
            var h = (max - min) * 0.5f;
            if (h.x <= 0f || h.y <= 0f || h.z <= 0f) return;
            float thinnest = Mathf.Min(h.x, Mathf.Min(h.y, h.z));
            if (bevel < MinBevel || thinnest * 2f < MinBevelThickness) bevel = 0f;
            float b = Mathf.Clamp(bevel, 0f, thinnest * 0.45f);
            if (b < 1e-4f)
            {
                Face(mat, c, h, 0, +1, skip); Face(mat, c, h, 0, -1, skip);
                Face(mat, c, h, 1, +1, skip); Face(mat, c, h, 1, -1, skip);
                Face(mat, c, h, 2, +1, skip); Face(mat, c, h, 2, -1, skip);
                return;
            }
            // The six faces, inset by the bevel.
            for (int axis = 0; axis < 3; axis++)
                for (int s = -1; s <= 1; s += 2)
                {
                    if (Skipped(skip, axis, s)) continue;
                    var pts = new Vector3[4];
                    int k = 0;
                    foreach (var (u, v) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) })
                        pts[k++] = FacePoint(c, h, b, axis, s, u, v);
                    Poly(mat, pts, AxisDir(axis) * s);
                }
            // The twelve edge strips and the eight corners (only where both faces are drawn).
            for (int a0 = 0; a0 < 3; a0++)
                for (int a1 = a0 + 1; a1 < 3; a1++)
                {
                    int a2 = 3 - a0 - a1;
                    for (int s0 = -1; s0 <= 1; s0 += 2)
                        for (int s1 = -1; s1 <= 1; s1 += 2)
                        {
                            if (Skipped(skip, a0, s0) || Skipped(skip, a1, s1)) continue;
                            var p0 = Corner(c, h, b, a0, s0, a1, s1, a2, -1, a0);
                            var p1 = Corner(c, h, b, a0, s0, a1, s1, a2, +1, a0);
                            var p2 = Corner(c, h, b, a0, s0, a1, s1, a2, +1, a1);
                            var p3 = Corner(c, h, b, a0, s0, a1, s1, a2, -1, a1);
                            Poly(mat, new[] { p0, p1, p2, p3 }, AxisDir(a0) * s0 + AxisDir(a1) * s1);
                        }
                }
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                    {
                        if (Skipped(skip, 0, sx) || Skipped(skip, 1, sy) || Skipped(skip, 2, sz)) continue;
                        var px = new Vector3(c.x + sx * h.x, c.y + sy * (h.y - b), c.z + sz * (h.z - b));
                        var py = new Vector3(c.x + sx * (h.x - b), c.y + sy * h.y, c.z + sz * (h.z - b));
                        var pz = new Vector3(c.x + sx * (h.x - b), c.y + sy * (h.y - b), c.z + sz * h.z);
                        Poly(mat, new[] { px, py, pz }, new Vector3(sx, sy, sz));
                    }
        }

        public void BoxC(string mat, Vector3 center, Vector3 size, float bevel = 0f, Side skip = Side.None) =>
            Box(mat, center - size * 0.5f, center + size * 0.5f, bevel, skip);

        static bool Skipped(Side skip, int axis, int s)
        {
            Side side = axis == 0 ? (s > 0 ? Side.XPos : Side.XNeg) : axis == 1 ? (s > 0 ? Side.YPos : Side.YNeg) : (s > 0 ? Side.ZPos : Side.ZNeg);
            return (skip & side) != 0;
        }

        static Vector3 AxisDir(int axis) => axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward;

        static float Comp(Vector3 v, int axis) => axis == 0 ? v.x : axis == 1 ? v.y : v.z;

        static Vector3 With(Vector3 v, int axis, float value)
        {
            if (axis == 0) v.x = value; else if (axis == 1) v.y = value; else v.z = value;
            return v;
        }

        /// <summary>A point of the face on <paramref name="axis"/> side <paramref name="s"/>, at (u, v) = (+-1, +-1) over the
        /// other two axes, inset by the bevel.</summary>
        static Vector3 FacePoint(Vector3 c, Vector3 h, float b, int axis, int s, int u, int v)
        {
            int ua = (axis + 1) % 3, va = (axis + 2) % 3;
            var p = c;
            p = With(p, axis, Comp(c, axis) + s * Comp(h, axis));
            p = With(p, ua, Comp(c, ua) + u * (Comp(h, ua) - b));
            p = With(p, va, Comp(c, va) + v * (Comp(h, va) - b));
            return p;
        }

        /// <summary>The corner of the face on axis <paramref name="onFace"/> near the edge of axes a0 (s0) and a1 (s1), at
        /// the end s2 of the third axis.</summary>
        static Vector3 Corner(Vector3 c, Vector3 h, float b, int a0, int s0, int a1, int s1, int a2, int s2, int onFace)
        {
            var p = c;
            p = With(p, a0, Comp(c, a0) + s0 * (Comp(h, a0) - (onFace == a0 ? 0f : b)));
            p = With(p, a1, Comp(c, a1) + s1 * (Comp(h, a1) - (onFace == a1 ? 0f : b)));
            p = With(p, a2, Comp(c, a2) + s2 * (Comp(h, a2) - b));
            return p;
        }

        void Face(string mat, Vector3 c, Vector3 h, int axis, int s, Side skip)
        {
            if (Skipped(skip, axis, s)) return;
            var pts = new Vector3[4];
            int k = 0;
            foreach (var (u, v) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) })
                pts[k++] = FacePoint(c, h, 0f, axis, s, u, v);
            Poly(mat, pts, AxisDir(axis) * s);
        }

        /// <summary>A box between two points (a beam, a rail, a post) with a square section <paramref name="width"/> x
        /// <paramref name="height"/>; its "up" is world up unless the beam is vertical.</summary>
        public void Beam(string mat, Vector3 a, Vector3 b, float width, float height, float bevel = 0f)
        {
            var d = b - a;
            float len = d.magnitude;
            if (len < 1e-4f) return;
            var dir = d / len;
            var up = Mathf.Abs(Vector3.Dot(dir, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;
            var rot = Quaternion.LookRotation(dir, up);
            Push(Matrix4x4.TRS((a + b) * 0.5f, rot, Vector3.one));
            Box(mat, new Vector3(-width * 0.5f, -height * 0.5f, -len * 0.5f), new Vector3(width * 0.5f, height * 0.5f, len * 0.5f), bevel);
            Pop();
        }

        // ---------- Extrusions ----------

        /// <summary>A convex outline (local XZ, any order) extruded from <paramref name="y0"/> to <paramref name="y1"/>:
        /// its sides, and its top (and bottom) cap.</summary>
        public void Prism(string mat, IList<Vector2> outline, float y0, float y1, bool top = true, bool bottom = false, string capMat = null)
        {
            int n = outline.Count;
            if (n < 3) return;
            var centre = Vector2.zero;
            foreach (var o in outline) centre += o;
            centre /= n;
            for (int i = 0; i < n; i++)
            {
                var a = outline[i];
                var b = outline[(i + 1) % n];
                var mid = (a + b) * 0.5f - centre;
                Quad(mat, new Vector3(a.x, y0, a.y), new Vector3(a.x, y1, a.y), new Vector3(b.x, y1, b.y), new Vector3(b.x, y0, b.y), new Vector3(mid.x, 0f, mid.y));
            }
            var capTop = new Vector3[n];
            var capBottom = new Vector3[n];
            for (int i = 0; i < n; i++) { capTop[i] = new Vector3(outline[i].x, y1, outline[i].y); capBottom[i] = new Vector3(outline[i].x, y0, outline[i].y); }
            if (top) Poly(capMat ?? mat, capTop, Vector3.up);
            if (bottom) Poly(capMat ?? mat, capBottom, Vector3.down);
        }

        /// <summary>A tube along <paramref name="path"/> (local points) with a radius per point and smooth normals (trunks,
        /// branches, pipes, railings); <paramref name="vScale"/> m of length per V repeat.</summary>
        public void Tube(string mat, IList<Vector3> path, IList<float> radius, int sides = 8, bool capEnd = false, float vScale = 1f)
        {
            if (_discard || path.Count < 2) return;
            var part = Get(mat);
            Vector3 prevSide = Vector3.zero;
            float along = 0f;
            int rings = path.Count;
            int start = part.v.Count;
            for (int i = 0; i < rings; i++)
            {
                Vector3 fwd = i == 0 ? path[1] - path[0] : i == rings - 1 ? path[i] - path[i - 1] : (path[i + 1] - path[i - 1]);
                fwd = fwd.sqrMagnitude > 1e-10f ? fwd.normalized : Vector3.up;
                Vector3 side = i == 0 ? Vector3.Cross(fwd, Mathf.Abs(fwd.y) > 0.9f ? Vector3.right : Vector3.up).normalized
                                      : Vector3.Cross(fwd, Vector3.Cross(prevSide, fwd)).normalized;
                if (side.sqrMagnitude < 1e-6f) side = Vector3.Cross(fwd, Vector3.right).normalized;
                prevSide = side;
                Vector3 up2 = Vector3.Cross(side, fwd);
                if (i > 0) along += (path[i] - path[i - 1]).magnitude;
                float r = radius[Mathf.Min(i, radius.Count - 1)];
                for (int k = 0; k <= sides; k++)
                {
                    float a = k / (float)sides * Mathf.PI * 2f;
                    Vector3 dir = side * Mathf.Cos(a) + up2 * Mathf.Sin(a);
                    var wp = P(path[i] + dir * r);
                    part.v.Add(wp);
                    part.n.Add(D(dir).normalized);
                    part.uv.Add(new Vector2(k / (float)sides * Mathf.Max(0.3f, r * 2f * Mathf.PI), along / Mathf.Max(0.01f, vScale)));
                }
            }
            for (int i = 0; i + 1 < rings; i++)
                for (int k = 0; k < sides; k++)
                {
                    int a = start + i * (sides + 1) + k, b = a + 1, c = a + sides + 1, d = c + 1;
                    // Outward-facing, clockwise from outside: check the first quad's winding once.
                    part.t.Add(a); part.t.Add(c); part.t.Add(b);
                    part.t.Add(b); part.t.Add(c); part.t.Add(d);
                }
            FixWinding(part, start, part.v.Count);
            _vertexCount += rings * (sides + 1);
            if (capEnd)
            {
                var ring = new Vector3[sides];
                int last = start + (rings - 1) * (sides + 1);
                for (int k = 0; k < sides; k++) ring[k] = Inverse(part.v[last + k]);
                Poly(mat, ring, path[rings - 1] - path[rings - 2]);
            }
        }

        Vector3 Inverse(Vector3 world) => _m.inverse.MultiplyPoint3x4(world);

        /// <summary>Makes the triangles added from vertex <paramref name="from"/> face their vertex normals.</summary>
        static void FixWinding(Part part, int from, int to)
        {
            for (int i = part.t.Count - 3; i >= 0; i -= 3)
            {
                int a = part.t[i], b = part.t[i + 1], c = part.t[i + 2];
                if (a < from || a >= to) break;
                var face = Vector3.Cross(part.v[b] - part.v[a], part.v[c] - part.v[a]);
                if (Vector3.Dot(face, part.n[a] + part.n[b] + part.n[c]) < 0f) { part.t[i + 1] = c; part.t[i + 2] = b; }
            }
        }

        public void Cylinder(string mat, Vector3 a, Vector3 b, float radius, int sides = 10, bool caps = true)
        {
            Tube(mat, new[] { a, b }, new[] { radius, radius }, sides, caps, 1f);
            if (caps)
            {
                var dir = (b - a).normalized;
                var side = Vector3.Cross(dir, Mathf.Abs(dir.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
                var up2 = Vector3.Cross(side, dir);
                var ring = new Vector3[sides];
                for (int k = 0; k < sides; k++)
                {
                    float t = k / (float)sides * Mathf.PI * 2f;
                    ring[k] = a + (side * Mathf.Cos(t) + up2 * Mathf.Sin(t)) * radius;
                }
                Poly(mat, ring, -dir);
            }
        }

        /// <summary>An ellipsoid (smooth normals; <paramref name="displace"/> moves each local unit-sphere point, for
        /// lumpy foliage masses and stones).</summary>
        public void Ellipsoid(string mat, Vector3 center, Vector3 radii, int segments = 10, Func<Vector3, float> displace = null, bool sphereNormals = true)
        {
            if (_discard) return;
            var part = Get(mat);
            int start = part.v.Count;
            int rows = Mathf.Max(3, segments / 2), cols = Mathf.Max(4, segments);
            for (int r = 0; r <= rows; r++)
            {
                float phi = Mathf.PI * r / rows;
                for (int c = 0; c <= cols; c++)
                {
                    float th = Mathf.PI * 2f * c / cols;
                    var unit = new Vector3(Mathf.Sin(phi) * Mathf.Cos(th), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(th));
                    float k = displace != null ? displace(unit) : 1f;
                    var local = center + Vector3.Scale(unit * k, radii);
                    var wp = P(local);
                    part.v.Add(wp);
                    var nrm = sphereNormals ? D(unit).normalized : D(new Vector3(unit.x / radii.x, unit.y / radii.y, unit.z / radii.z)).normalized;
                    part.n.Add(nrm);
                    part.uv.Add(new Vector2(c / (float)cols * 2f, r / (float)rows));
                }
            }
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    int a = start + r * (cols + 1) + c, b = a + 1, d = a + cols + 1, e = d + 1;
                    part.t.Add(a); part.t.Add(b); part.t.Add(d);
                    part.t.Add(b); part.t.Add(e); part.t.Add(d);
                }
            FixWinding(part, start, part.v.Count);
            _vertexCount += (rows + 1) * (cols + 1);
        }

        // ---------- Merging ----------

        /// <summary>Adds another sink's geometry (its world positions, under this one's current transform).</summary>
        public void Append(HillMesh other)
        {
            if (_discard) return;
            foreach (var kv in other._parts)
            {
                var dst = Get(kv.Key);
                int b0 = dst.v.Count;
                var src = kv.Value;
                for (int i = 0; i < src.v.Count; i++)
                {
                    var wp = P(src.v[i]);
                    dst.v.Add(wp);
                    dst.n.Add(D(src.n[i]).normalized);
                    dst.uv.Add(src.uv[i]);
                }
                for (int i = 0; i < src.t.Count; i++) dst.t.Add(b0 + src.t[i]);
                _vertexCount += src.v.Count;
            }
        }

        // ---------- Building ----------

        /// <summary>One child per material under <paramref name="parent"/> (named "<see cref="name"/> (material)"). Its mesh
        /// goes into <see cref="HillMeshStore.Current"/> when the builder has one (one asset per chunk, freed after saving),
        /// else as its own asset in <paramref name="assetFolder"/> when given. Returns the chunk's root.</summary>
        public GameObject Build(Transform parent, string assetFolder = null, int layer = 0)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.layer = layer;
            foreach (var kv in _parts)
            {
                var part = kv.Value;
                if (part.t.Count == 0) continue;
                var mesh = MakeMesh(name + " (" + kv.Key + ")", part);
                Save(mesh, assetFolder, Safe(name) + "__" + Safe(kv.Key));
                var go = new GameObject(name + " (" + kv.Key + ")") { layer = layer };
                go.transform.SetParent(root.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                var spec = HillMat.Find(kv.Key);
                r.sharedMaterial = HillMaterials.Get(kv.Key);
                r.shadowCastingMode = spec == null || spec.shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                r.receiveShadows = true;
                r.lightProbeUsage = LightProbeUsage.BlendProbes;
                r.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.ReflectionProbeStatic | StaticEditorFlags.OccludeeStatic);
            }
            return root;
        }

        /// <summary>Everything drawn as ONE mesh with a sub-mesh per material (a shared kit piece: a prop drawn once and
        /// placed many times). <paramref name="materialIds"/> lists the sub-meshes' material ids in order. Null if empty.</summary>
        public Mesh BuildShared(string meshName, out string[] materialIds)
        {
            var ids = new List<string>();
            foreach (var kv in _parts) if (kv.Value.t.Count > 0) ids.Add(kv.Key);
            materialIds = ids.ToArray();
            if (ids.Count == 0) return null;
            var all = new Part();
            var starts = new int[ids.Count];
            var counts = new int[ids.Count];
            for (int s = 0; s < ids.Count; s++)
            {
                var p = _parts[ids[s]];
                int b0 = all.v.Count;
                starts[s] = all.t.Count;
                all.v.AddRange(p.v);
                all.n.AddRange(p.n);
                all.uv.AddRange(p.uv);
                for (int i = 0; i < p.t.Count; i++) all.t.Add(b0 + p.t[i]);
                counts[s] = p.t.Count;
            }
            return MakeMesh(meshName, all, starts, counts);
        }

        static void Save(Mesh mesh, string assetFolder, string fileName)
        {
            if (HillMeshStore.Current != null) { HillMeshStore.Current.Add(mesh); return; }
            if (string.IsNullOrEmpty(assetFolder)) return;
            AssetDatabase.CreateAsset(mesh, AssetDatabase.GenerateUniqueAssetPath(assetFolder + "/" + fileName + ".asset"));
        }

        // The compact vertex (36 bytes: position float3, normal and tangent half4, uv float2), written straight into the
        // mesh's vertex buffer: no 48-byte readable copy first and no re-layout afterwards (the old path made both, plus
        // eight managed copies of every mesh's arrays in VhPerfMesh.Compact).
        [StructLayout(LayoutKind.Sequential)]
        struct Vtx
        {
            public Vector3 p;
            public ushort nx, ny, nz, nw;
            public ushort tx, ty, tz, tw;
            public Vector2 uv;
        }

        static readonly VertexAttributeDescriptor[] Layout =
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float16, 4),
            new VertexAttributeDescriptor(VertexAttribute.Tangent, VertexAttributeFormat.Float16, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2),
        };

        /// <summary>Bytes a vertex takes in the meshes this sink makes.</summary>
        public const int VertexBytes = 36;

        // Scratch buffers reused across meshes (the build makes thousands; none allocates its own).
        static Vtx[] _vb = new Vtx[0];
        static Vector3[] _tan = new Vector3[0], _bit = new Vector3[0];
        static ushort[] _ib16 = new ushort[0];
        static int[] _ib32 = new int[0];

        const MeshUpdateFlags NoChecks = MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontNotifyMeshUsers;

        static Mesh MakeMesh(string meshName, Part part, int[] starts = null, int[] counts = null)
        {
            int nv = part.v.Count, ni = part.t.Count;
            if (_vb.Length < nv)
            {
                int cap = Mathf.NextPowerOfTwo(nv);
                _vb = new Vtx[cap];
                _tan = new Vector3[cap];
                _bit = new Vector3[cap];
            }
            Tangents(part, nv, ni);
            var bounds = new Bounds(part.v[0], Vector3.zero);
            for (int i = 0; i < nv; i++)
            {
                var n = part.n[i];
                var t = _tan[i] - n * Vector3.Dot(n, _tan[i]);
                if (t.sqrMagnitude < 1e-12f) t = Mathf.Abs(n.y) < 0.99f ? Vector3.Cross(Vector3.up, n) : Vector3.right;
                t.Normalize();
                float w = Vector3.Dot(Vector3.Cross(n, t), _bit[i]) < 0f ? -1f : 1f;
                _vb[i] = new Vtx
                {
                    p = part.v[i],
                    nx = Mathf.FloatToHalf(n.x), ny = Mathf.FloatToHalf(n.y), nz = Mathf.FloatToHalf(n.z), nw = 0,
                    tx = Mathf.FloatToHalf(t.x), ty = Mathf.FloatToHalf(t.y), tz = Mathf.FloatToHalf(t.z), tw = Mathf.FloatToHalf(w),
                    uv = part.uv[i],
                };
                bounds.Encapsulate(part.v[i]);
            }
            var mesh = new Mesh { name = meshName };
            mesh.SetVertexBufferParams(nv, Layout);
            mesh.SetVertexBufferData(_vb, 0, 0, nv, 0, NoChecks);
            bool small = nv <= 65535;
            mesh.SetIndexBufferParams(ni, small ? IndexFormat.UInt16 : IndexFormat.UInt32);
            if (small)
            {
                if (_ib16.Length < ni) _ib16 = new ushort[Mathf.NextPowerOfTwo(ni)];
                for (int i = 0; i < ni; i++) _ib16[i] = (ushort)part.t[i];
                mesh.SetIndexBufferData(_ib16, 0, 0, ni, NoChecks);
            }
            else
            {
                if (_ib32.Length < ni) _ib32 = new int[Mathf.NextPowerOfTwo(ni)];
                part.t.CopyTo(_ib32);
                mesh.SetIndexBufferData(_ib32, 0, 0, ni, NoChecks);
            }
            if (starts == null) { starts = new[] { 0 }; counts = new[] { ni }; }
            mesh.subMeshCount = counts.Length;
            for (int s = 0; s < counts.Length; s++)
                mesh.SetSubMesh(s, new SubMeshDescriptor(starts[s], counts[s], MeshTopology.Triangles) { bounds = bounds, firstVertex = 0, vertexCount = nv }, NoChecks);
            mesh.bounds = bounds;
            HillMeshStore.Count(nv, ni, small);
            return mesh;
        }

        /// <summary>Per-vertex tangents from the UVs (Lengyel's method), into the scratch arrays.</summary>
        static void Tangents(Part part, int nv, int ni)
        {
            Array.Clear(_tan, 0, nv);
            Array.Clear(_bit, 0, nv);
            for (int i = 0; i + 2 < ni; i += 3)
            {
                int a = part.t[i], b = part.t[i + 1], c = part.t[i + 2];
                Vector3 e1 = part.v[b] - part.v[a], e2 = part.v[c] - part.v[a];
                Vector2 d1 = part.uv[b] - part.uv[a], d2 = part.uv[c] - part.uv[a];
                float det = d1.x * d2.y - d2.x * d1.y;
                if (Mathf.Abs(det) < 1e-12f) continue;
                float r = 1f / det;
                var sdir = (e1 * d2.y - e2 * d1.y) * r;
                var tdir = (e2 * d1.x - e1 * d2.x) * r;
                _tan[a] += sdir; _tan[b] += sdir; _tan[c] += sdir;
                _bit[a] += tdir; _bit[b] += tdir; _bit[c] += tdir;
            }
        }

        static string Safe(string s)
        {
            var chars = s.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
                if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '_' && chars[i] != '-') chars[i] = '_';
            return new string(chars);
        }
    }
}
