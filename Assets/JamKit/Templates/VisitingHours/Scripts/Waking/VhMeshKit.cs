using System;
using System.Collections.Generic;
using UnityEngine;

namespace JamKit.VisitingHours
{
    /// <summary>
    /// Simple meshes for the waking world, made in code: boxes, soft rounded boxes (pillows, the phone,
    /// the tablet), cylinders (the mug, the pen), tubes along a path (the lanyard, a mug handle, hair)
    /// and draped heightfields (the blanket over Nadia). Every mesh it makes is remembered so
    /// <see cref="Dispose"/> frees them with the set.
    /// </summary>
    public sealed class VhMeshKit
    {
        readonly List<Mesh> _meshes = new List<Mesh>();

        public void Dispose()
        {
            foreach (var m in _meshes) VhUtil.Kill(m);
            _meshes.Clear();
        }

        Mesh Finish(string name, List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t, List<Color> colors = null)
        {
            var m = new Mesh { name = name };
            if (v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(v);
            m.SetNormals(n);
            m.SetUVs(0, uv);
            if (colors != null) m.SetColors(colors);
            m.SetTriangles(t, 0);
            m.RecalculateBounds();
            // Tangents: the ward's PBR materials have normal maps (URP/Lit reads the mesh's tangents for them).
            if (uv.Count == v.Count) m.RecalculateTangents();
            _meshes.Add(m);
            return m;
        }

        /// <summary>A flat-shaded box centred on the origin. UVs are in metres times <paramref name="uvScale"/> (textures tile).</summary>
        public Mesh Box(Vector3 size, float uvScale = 1f)
        {
            var v = new List<Vector3>(24);
            var n = new List<Vector3>(24);
            var uv = new List<Vector2>(24);
            var t = new List<int>(36);
            Vector3 h = size * 0.5f;
            void Face(Vector3 normal, Vector3 u, Vector3 w, float su, float sw)
            {
                int i = v.Count;
                Vector3 c = Vector3.Scale(normal, h);
                v.Add(c - u - w); v.Add(c + u - w); v.Add(c + u + w); v.Add(c - u + w);
                for (int k = 0; k < 4; k++) n.Add(normal);
                uv.Add(new Vector2(0f, 0f)); uv.Add(new Vector2(su, 0f)); uv.Add(new Vector2(su, sw)); uv.Add(new Vector2(0f, sw));
                t.Add(i); t.Add(i + 2); t.Add(i + 1);
                t.Add(i); t.Add(i + 3); t.Add(i + 2);
            }
            Face(Vector3.right, new Vector3(0, 0, h.z), new Vector3(0, h.y, 0), size.z * uvScale, size.y * uvScale);
            Face(Vector3.left, new Vector3(0, 0, -h.z), new Vector3(0, h.y, 0), size.z * uvScale, size.y * uvScale);
            Face(Vector3.up, new Vector3(h.x, 0, 0), new Vector3(0, 0, h.z), size.x * uvScale, size.z * uvScale);
            Face(Vector3.down, new Vector3(h.x, 0, 0), new Vector3(0, 0, -h.z), size.x * uvScale, size.z * uvScale);
            Face(Vector3.forward, new Vector3(-h.x, 0, 0), new Vector3(0, h.y, 0), size.x * uvScale, size.y * uvScale);
            Face(Vector3.back, new Vector3(h.x, 0, 0), new Vector3(0, h.y, 0), size.x * uvScale, size.y * uvScale);
            return Finish("vh_box", v, n, uv, t);
        }

        /// <summary>
        /// A box with soft rounded edges and smooth normals (radius clamped to half the smallest side).
        /// <paramref name="puff"/> bulges the faces outwards (pillows, a mattress). The +Y face's UVs
        /// cover 0..1 (a texture on the top: the tablet's screen side is +Y).
        /// </summary>
        public Mesh RoundedBox(Vector3 size, float radius, int segments = 6, float puff = 0f)
        {
            Vector3 h = size * 0.5f;
            float r = Mathf.Clamp(radius, 0.0005f, Mathf.Min(h.x, Mathf.Min(h.y, h.z)) - 0.0001f);
            Vector3 inner = h - new Vector3(r, r, r);
            int s = Mathf.Max(2, segments);
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            void Face(Vector3 normal, Vector3 u, Vector3 w)
            {
                int start = v.Count;
                for (int j = 0; j <= s; j++)
                    for (int i = 0; i <= s; i++)
                    {
                        float a = (float)i / s * 2f - 1f, b = (float)j / s * 2f - 1f;
                        Vector3 p = Vector3.Scale(normal + u * a + w * b, h);
                        Vector3 q = new Vector3(Mathf.Clamp(p.x, -inner.x, inner.x), Mathf.Clamp(p.y, -inner.y, inner.y), Mathf.Clamp(p.z, -inner.z, inner.z));
                        Vector3 d = p - q;
                        Vector3 dir = d.sqrMagnitude > 1e-12f ? d.normalized : normal;
                        Vector3 pos = q + dir * r;
                        if (puff > 0f)
                        {
                            float bulge = (1f - a * a) * (1f - b * b);
                            pos += normal * (puff * bulge);
                        }
                        v.Add(pos);
                        n.Add(dir);
                        uv.Add(new Vector2((a + 1f) * 0.5f, (b + 1f) * 0.5f));
                    }
                for (int j = 0; j < s; j++)
                    for (int i = 0; i < s; i++)
                    {
                        int k = start + j * (s + 1) + i;
                        t.Add(k); t.Add(k + s + 1); t.Add(k + 1);
                        t.Add(k + 1); t.Add(k + s + 1); t.Add(k + s + 2);
                    }
            }
            Face(Vector3.up, Vector3.right, Vector3.forward);
            Face(Vector3.down, Vector3.right, Vector3.back);
            // Every face's u x w points inwards, so (i, j) -> (i, j+1) -> (i+1, j) winds clockwise from outside.
            Face(Vector3.right, Vector3.forward, Vector3.up);
            Face(Vector3.left, Vector3.back, Vector3.up);
            Face(Vector3.forward, Vector3.left, Vector3.up);
            Face(Vector3.back, Vector3.right, Vector3.up);
            var m = Finish("vh_rounded_box", v, n, uv, t);
            if (puff > 0f) m.RecalculateNormals();
            return m;
        }

        /// <summary>A cylinder (or a cone frustum) standing on the origin, smooth sides. <paramref name="inward"/> turns it inside out (a mug's inner wall).</summary>
        public Mesh Cylinder(float radiusBottom, float radiusTop, float height, int segments = 24, bool capTop = true, bool capBottom = true, bool inward = false)
        {
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            int seg = Mathf.Max(3, segments);
            float slope = (radiusBottom - radiusTop) / Mathf.Max(height, 1e-4f);
            for (int i = 0; i <= seg; i++)
            {
                float a = (float)i / seg * Mathf.PI * 2f;
                float cx = Mathf.Cos(a), sz = Mathf.Sin(a);
                Vector3 nn = new Vector3(cx, slope, sz).normalized * (inward ? -1f : 1f);
                v.Add(new Vector3(cx * radiusBottom, 0f, sz * radiusBottom)); n.Add(nn); uv.Add(new Vector2((float)i / seg, 0f));
                v.Add(new Vector3(cx * radiusTop, height, sz * radiusTop)); n.Add(nn); uv.Add(new Vector2((float)i / seg, 1f));
            }
            for (int i = 0; i < seg; i++)
            {
                int k = i * 2;
                if (!inward) { t.Add(k); t.Add(k + 1); t.Add(k + 2); t.Add(k + 2); t.Add(k + 1); t.Add(k + 3); }
                else { t.Add(k); t.Add(k + 2); t.Add(k + 1); t.Add(k + 2); t.Add(k + 3); t.Add(k + 1); }
            }
            void Cap(float y, float r, bool up)
            {
                int c = v.Count;
                Vector3 nn = up ? Vector3.up : Vector3.down;
                v.Add(new Vector3(0f, y, 0f)); n.Add(nn); uv.Add(new Vector2(0.5f, 0.5f));
                for (int i = 0; i <= seg; i++)
                {
                    float a = (float)i / seg * Mathf.PI * 2f;
                    v.Add(new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r)); n.Add(nn);
                    uv.Add(new Vector2(0.5f + Mathf.Cos(a) * 0.5f, 0.5f + Mathf.Sin(a) * 0.5f));
                }
                for (int i = 0; i < seg; i++)
                {
                    if (up) { t.Add(c); t.Add(c + 2 + i); t.Add(c + 1 + i); }
                    else { t.Add(c); t.Add(c + 1 + i); t.Add(c + 2 + i); }
                }
            }
            if (capTop) Cap(height, radiusTop, true);
            if (capBottom) Cap(0f, radiusBottom, false);
            return Finish("vh_cylinder", v, n, uv, t);
        }

        /// <summary>A rounded rectangle's outline in XZ (counter-clockwise seen from above), its corners of radius <paramref name="r"/>.</summary>
        static void Outline(List<Vector2> into, float width, float length, float r, int segs)
        {
            into.Clear();
            float hw = width * 0.5f, hl = length * 0.5f;
            r = Mathf.Clamp(r, 0.0002f, Mathf.Min(hw, hl) - 0.0001f);
            Vector2[] centres = { new Vector2(hw - r, hl - r), new Vector2(-hw + r, hl - r), new Vector2(-hw + r, -hl + r), new Vector2(hw - r, -hl + r) };
            for (int c = 0; c < 4; c++)
                for (int i = 0; i <= segs; i++)
                {
                    float a = (c * 90f + 90f * i / segs) * Mathf.Deg2Rad;
                    into.Add(centres[c] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
                }
        }

        /// <summary>
        /// A device body: a rounded rectangle in plan (<paramref name="width"/> along X, <paramref name="length"/> along Z, corner
        /// radius <paramref name="corner"/>) extruded <paramref name="thickness"/> up from y 0, its edges softened by a bevel
        /// (<paramref name="bevel"/>). The top face's UVs cover 0..1 (the top of the device along +Z).
        /// </summary>
        public Mesh RoundedSlab(float width, float length, float thickness, float corner, float bevel, int segs = 6)
        {
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            bevel = Mathf.Clamp(bevel, 0.0001f, thickness * 0.45f);
            var full = new List<Vector2>();
            var inset = new List<Vector2>();
            Outline(full, width, length, corner, segs);
            Outline(inset, width - 2f * bevel, length - 2f * bevel, Mathf.Max(0.0003f, corner - bevel), segs);
            int count = full.Count;
            // Rings bottom to top: the inset bottom edge, the full outline (twice: the straight side), the inset top edge.
            float[] ys = { 0f, bevel, thickness - bevel, thickness };
            for (int ring = 0; ring < 4; ring++)
            {
                var pts = ring == 0 || ring == 3 ? inset : full;
                for (int i = 0; i < count; i++)
                {
                    Vector2 a = full[(i + count - 1) % count], b = full[(i + 1) % count];
                    Vector2 tan = (b - a).normalized;
                    Vector3 outward = new Vector3(tan.y, 0f, -tan.x);
                    Vector3 nn = ring == 0 ? (outward + Vector3.down).normalized : ring == 3 ? (outward + Vector3.up).normalized : outward;
                    v.Add(new Vector3(pts[i].x, ys[ring], pts[i].y));
                    n.Add(nn);
                    uv.Add(new Vector2((float)i / count, ys[ring] / Mathf.Max(1e-4f, thickness)));
                }
            }
            for (int ring = 0; ring < 3; ring++)
                for (int i = 0; i < count; i++)
                {
                    int a0 = ring * count + i, a1 = ring * count + (i + 1) % count;
                    int b0 = a0 + count, b1 = a1 + count;
                    t.Add(a0); t.Add(b0); t.Add(b1);
                    t.Add(a0); t.Add(b1); t.Add(a1);
                }
            // Caps.
            void Cap(float y, bool up)
            {
                int c = v.Count;
                v.Add(new Vector3(0f, y, 0f)); n.Add(up ? Vector3.up : Vector3.down); uv.Add(new Vector2(0.5f, 0.5f));
                for (int i = 0; i < count; i++)
                {
                    v.Add(new Vector3(inset[i].x, y, inset[i].y));
                    n.Add(up ? Vector3.up : Vector3.down);
                    uv.Add(new Vector2(inset[i].x / width + 0.5f, inset[i].y / length + 0.5f));
                }
                for (int i = 0; i < count; i++)
                {
                    int p0 = c + 1 + i, p1 = c + 1 + (i + 1) % count;
                    if (up) { t.Add(c); t.Add(p1); t.Add(p0); }
                    else { t.Add(c); t.Add(p0); t.Add(p1); }
                }
            }
            Cap(thickness, true);
            Cap(0f, false);
            return Finish("vh_rounded_slab", v, n, uv, t);
        }

        /// <summary>A flat rounded rectangle facing +Y at y 0 (a screen, a glass front), UVs 0..1 across it (+Z is up the picture).</summary>
        public Mesh RoundedRect(float width, float length, float corner, int segs = 6)
        {
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            var o = new List<Vector2>();
            Outline(o, width, length, corner, segs);
            v.Add(Vector3.zero); n.Add(Vector3.up); uv.Add(new Vector2(0.5f, 0.5f));
            foreach (var p in o)
            {
                v.Add(new Vector3(p.x, 0f, p.y));
                n.Add(Vector3.up);
                uv.Add(new Vector2(p.x / width + 0.5f, p.y / length + 0.5f));
            }
            for (int i = 0; i < o.Count; i++) { t.Add(0); t.Add(1 + (i + 1) % o.Count); t.Add(1 + i); }
            return Finish("vh_rounded_rect", v, n, uv, t);
        }

        /// <summary>A flat disc facing +Y (the coffee's surface), UVs 0..1 across it.</summary>
        public Mesh Disc(float radius, int segments = 32) => Cylinder(radius, radius, 0f, segments, true, false);

        /// <summary>A flat quad in XY facing -Z (towards a camera looking down +Z), UVs 0..1.</summary>
        public Mesh Quad(float width, float height)
        {
            float x = width * 0.5f, y = height * 0.5f;
            var v = new List<Vector3> { new Vector3(-x, -y, 0f), new Vector3(x, -y, 0f), new Vector3(x, y, 0f), new Vector3(-x, y, 0f) };
            var n = new List<Vector3> { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            var uv = new List<Vector2> { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            var t = new List<int> { 0, 2, 1, 0, 3, 2 };
            return Finish("vh_quad", v, n, uv, t);
        }

        /// <summary>A round tube through <paramref name="path"/> (a lanyard, a handle, a strand of hair). Radius can taper to <paramref name="endRadius"/>.</summary>
        public Mesh Tube(IList<Vector3> path, float radius, int sides = 8, float endRadius = -1f)
        {
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            int count = path.Count;
            if (count < 2) return Finish("vh_tube", v, n, uv, t);
            if (endRadius < 0f) endRadius = radius;
            Vector3 prevUp = Vector3.up;
            for (int i = 0; i < count; i++)
            {
                Vector3 fwd = (i < count - 1 ? path[i + 1] - path[i] : path[i] - path[i - 1]);
                if (i > 0 && i < count - 1) fwd = path[i + 1] - path[i - 1];
                fwd = fwd.sqrMagnitude > 1e-12f ? fwd.normalized : Vector3.forward;
                Vector3 side = Vector3.Cross(prevUp, fwd);
                if (side.sqrMagnitude < 1e-8f) side = Vector3.Cross(Vector3.right, fwd);
                side.Normalize();
                Vector3 up = Vector3.Cross(fwd, side).normalized;
                prevUp = up;
                float r = Mathf.Lerp(radius, endRadius, (float)i / (count - 1));
                for (int s = 0; s <= sides; s++)
                {
                    float a = (float)s / sides * Mathf.PI * 2f;
                    Vector3 dir = side * Mathf.Cos(a) + up * Mathf.Sin(a);
                    v.Add(path[i] + dir * r);
                    n.Add(dir);
                    uv.Add(new Vector2((float)s / sides, (float)i / (count - 1)));
                }
            }
            for (int i = 0; i < count - 1; i++)
                for (int s = 0; s < sides; s++)
                {
                    int a = i * (sides + 1) + s, b = a + sides + 1;
                    t.Add(a); t.Add(a + 1); t.Add(b);
                    t.Add(a + 1); t.Add(b + 1); t.Add(b);
                }
            return Finish("vh_tube", v, n, uv, t);
        }

        /// <summary>
        /// A draped sheet over XZ (centred on the origin, <paramref name="width"/> along X, <paramref name="depth"/>
        /// along Z), its height at each point from <paramref name="height"/>(x, z). Smooth normals from the
        /// heights; UVs in metres times <paramref name="uvScale"/>. Seen from above.
        /// </summary>
        public Mesh Heightfield(float width, float depth, int nx, int nz, Func<float, float, float> height, float uvScale = 1f)
        {
            nx = Mathf.Max(1, nx);
            nz = Mathf.Max(1, nz);
            var v = new List<Vector3>((nx + 1) * (nz + 1));
            var n = new List<Vector3>((nx + 1) * (nz + 1));
            var uv = new List<Vector2>((nx + 1) * (nz + 1));
            var t = new List<int>(nx * nz * 6);
            float dx = width / nx, dz = depth / nz;
            for (int j = 0; j <= nz; j++)
                for (int i = 0; i <= nx; i++)
                {
                    float x = -width * 0.5f + i * dx, z = -depth * 0.5f + j * dz;
                    float y = height(x, z);
                    v.Add(new Vector3(x, y, z));
                    float hx = height(x + dx * 0.5f, z) - height(x - dx * 0.5f, z);
                    float hz = height(x, z + dz * 0.5f) - height(x, z - dz * 0.5f);
                    n.Add(new Vector3(-hx / dx, 1f, -hz / dz).normalized);
                    uv.Add(new Vector2(x * uvScale, z * uvScale));
                }
            for (int j = 0; j < nz; j++)
                for (int i = 0; i < nx; i++)
                {
                    int k = j * (nx + 1) + i;
                    t.Add(k); t.Add(k + nx + 1); t.Add(k + 1);
                    t.Add(k + 1); t.Add(k + nx + 1); t.Add(k + nx + 2);
                }
            return Finish("vh_drape", v, n, uv, t);
        }

        /// <summary>A mesh's copy with every vertex moved by <paramref name="delta"/>(position) as a blend shape named <paramref name="shape"/> (breathing).</summary>
        public static void AddBlendShape(Mesh mesh, string shape, Func<Vector3, Vector3> delta)
        {
            var verts = mesh.vertices;
            var deltas = new Vector3[verts.Length];
            for (int i = 0; i < verts.Length; i++) deltas[i] = delta(verts[i]);
            mesh.AddBlendShapeFrame(shape, 100f, deltas, null, null);
        }

        /// <summary>Parts smaller than this (metres, their longest side) cast no shadow, whatever the caller asked: a mug, a
        /// tick on a clock, a lever, a petal (docs/redesign/perf.md, Day item 2: 1-2 ms of shadow passes for nothing seen).</summary>
        public const float NoShadowBelow = 0.15f;

        /// <summary>A child object showing <paramref name="mesh"/> with <paramref name="mat"/>. Shadows only when asked and the
        /// part is at least <see cref="NoShadowBelow"/> long.</summary>
        public static MeshRenderer Show(Transform parent, string name, Mesh mesh, Material mat, Vector3 localPos, Quaternion localRot, bool shadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            if (shadows && mesh != null)
            {
                var s = mesh.bounds.size;
                if (Mathf.Max(s.x, Mathf.Max(s.y, s.z)) < NoShadowBelow) shadows = false;
            }
            r.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = true;
            return r;
        }

        /// <summary>
        /// Gives a box's faces UVs in metres from <paramref name="frame"/>'s axes (the place's own), by each face's direction:
        /// walls run on in x or z and up in y, floors and ceilings in x and z. Neighbouring boxes then meet without a seam in
        /// the texture (judge/look-2.md's "mismatched seams"). The mesh must be this box's own (the kit makes one per box).
        /// </summary>
        public static void PlaceUv(Mesh mesh, Transform t, Transform frame)
        {
            if (mesh == null || t == null) return;
            var v = mesh.vertices;
            var n = mesh.normals;
            if (n == null || n.Length != v.Length) return;
            var uv = new Vector2[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                Vector3 p = t.TransformPoint(v[i]);
                Vector3 d = t.TransformDirection(n[i]);
                if (frame != null)
                {
                    p = frame.InverseTransformPoint(p);
                    d = frame.InverseTransformDirection(d);
                }
                float ax = Mathf.Abs(d.x), ay = Mathf.Abs(d.y), az = Mathf.Abs(d.z);
                if (ay >= ax && ay >= az) uv[i] = new Vector2(p.x, p.z);
                else if (ax >= az) uv[i] = new Vector2(p.z, p.y);
                else uv[i] = new Vector2(p.x, p.y);
            }
            mesh.uv = uv;
            mesh.RecalculateTangents();
        }
    }

    public static class VhUtil
    {
        /// <summary>Destroy that works in edit mode too (EditMode tests build the sets).</summary>
        public static void Kill(UnityEngine.Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(o);
            else UnityEngine.Object.DestroyImmediate(o);
        }

        /// <summary>Smooth 0..1 ease (in and out).</summary>
        public static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}
