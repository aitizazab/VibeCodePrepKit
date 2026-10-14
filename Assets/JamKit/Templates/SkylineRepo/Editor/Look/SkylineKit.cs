using System.Collections.Generic;
using JamKit.Look.EditorTools;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace JamKit.SkylineRepo.EditorTools
{
    /// <summary>
    /// Visual-only dressing geometry for the arena (Task 21), merged per material so hundreds of trims, mullions, panels and
    /// signs cost a few draw calls. Everything a kit makes is a renderer with no collider, under a root that carries
    /// <see cref="SkylineNoCollider"/>: the arena's solids stay exactly <c>GalleriaArena</c>'s boxes. The builder keeps every
    /// piece inside a solid's volume or at most a few centimetres proud of a wall (or out of reach overhead).
    /// <para>UVs: boxes get the Look library's metre UVs, box-projected in world space (1 UV unit = 1 m, so the material's
    /// tiling shows each texture at its real size and neighbouring pieces continue the pattern); quads take an explicit UV
    /// rectangle (atlas cells: signs, posters, shelves).</para>
    /// </summary>
    public sealed class SkylineKit
    {
        sealed class Buf
        {
            public readonly List<Vector3> v = new List<Vector3>();
            public readonly List<Vector3> n = new List<Vector3>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<int> t = new List<int>();
        }

        readonly Dictionary<Material, Buf> _bufs = new Dictionary<Material, Buf>();
        readonly List<Material> _order = new List<Material>();

        Buf Get(Material m)
        {
            if (!_bufs.TryGetValue(m, out var b))
            {
                b = new Buf();
                _bufs[m] = b;
                _order.Add(m);
            }
            return b;
        }

        static Vector2 WorldUv(Vector3 p, Vector3 n)
        {
            var a = new Vector3(Mathf.Abs(n.x), Mathf.Abs(n.y), Mathf.Abs(n.z));
            if (a.y >= a.x && a.y >= a.z) return new Vector2(p.x, p.z);
            if (a.x >= a.z) return new Vector2(p.z, p.y);
            return new Vector2(p.x, p.y);
        }

        /// <summary>A quad from four corners (counter-clockwise or clockwise; the winding is fixed to face <paramref name="normal"/>).</summary>
        void QuadRaw(Buf b, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 normal, Vector2 u0, Vector2 u1, Vector2 u2, Vector2 u3)
        {
            int i = b.v.Count;
            b.v.Add(p0); b.v.Add(p1); b.v.Add(p2); b.v.Add(p3);
            for (int k = 0; k < 4; k++) b.n.Add(normal);
            b.uv.Add(u0); b.uv.Add(u1); b.uv.Add(u2); b.uv.Add(u3);
            // Unity's front face: Cross(b - a, c - a) points at the viewer, i.e. along the normal.
            bool ok = Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), normal) > 0f;
            if (ok) { b.t.Add(i); b.t.Add(i + 1); b.t.Add(i + 2); b.t.Add(i); b.t.Add(i + 2); b.t.Add(i + 3); }
            else { b.t.Add(i); b.t.Add(i + 2); b.t.Add(i + 1); b.t.Add(i); b.t.Add(i + 3); b.t.Add(i + 2); }
        }

        /// <summary>An axis-aligned box from <paramref name="min"/> to <paramref name="max"/> (world), metre UVs. With
        /// <paramref name="bevel"/> &gt; 0 its edges are chamfered (VhLookUv.BevelBox), so they catch the light.</summary>
        public void Box(Material m, Vector3 min, Vector3 max, float bevel = 0f)
        {
            if (m == null) return;
            var lo = Vector3.Min(min, max);
            var hi = Vector3.Max(min, max);
            var size = hi - lo;
            if (size.x <= 1e-4f || size.y <= 1e-4f || size.z <= 1e-4f) return;
            if (bevel > 0f)
            {
                var mesh = VhLookUv.BevelBox(size, bevel);
                Append(m, mesh, Matrix4x4.Translate((lo + hi) * 0.5f));
                Object.DestroyImmediate(mesh);
                return;
            }
            var b = Get(m);
            Face(b, lo, hi, 0, -1); Face(b, lo, hi, 0, 1);
            Face(b, lo, hi, 1, -1); Face(b, lo, hi, 1, 1);
            Face(b, lo, hi, 2, -1); Face(b, lo, hi, 2, 1);
        }

        /// <summary>An axis-aligned box whose every face takes the material <paramref name="pick"/> returns for its outward
        /// normal (the city's towers: a sunlit, a slanting and a shaded version of one facade), metre UVs.</summary>
        public void Box(System.Func<Vector3, Material> pick, Vector3 min, Vector3 max)
        {
            if (pick == null) return;
            var lo = Vector3.Min(min, max);
            var hi = Vector3.Max(min, max);
            var size = hi - lo;
            if (size.x <= 1e-4f || size.y <= 1e-4f || size.z <= 1e-4f) return;
            for (int axis = 0; axis < 3; axis++)
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    var n = Vector3.zero;
                    n[axis] = sign;
                    var m = pick(n);
                    if (m != null) Face(Get(m), lo, hi, axis, sign);
                }
        }

        /// <summary><see cref="BoxYaw(Material, Vector3, Vector3, float)"/> with a material per face, as
        /// <see cref="Box(System.Func{Vector3, Material}, Vector3, Vector3)"/>.</summary>
        public void BoxYaw(System.Func<Vector3, Material> pick, Vector3 center, Vector3 size, float yawDeg)
        {
            if (pick == null) return;
            var rot = Quaternion.Euler(0f, yawDeg, 0f);
            var h = size * 0.5f;
            for (int axis = 0; axis < 3; axis++)
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    var nl = Vector3.zero;
                    nl[axis] = sign;
                    var n = rot * nl;
                    var m = pick(n);
                    if (m == null) continue;
                    int a1 = (axis + 1) % 3, a2 = (axis + 2) % 3;
                    var c = new Vector3[4];
                    for (int k = 0; k < 4; k++)
                    {
                        var p = Vector3.zero;
                        p[axis] = sign * h[axis];
                        p[a1] = (k == 1 || k == 2) ? h[a1] : -h[a1];
                        p[a2] = (k >= 2) ? h[a2] : -h[a2];
                        c[k] = center + rot * p;
                    }
                    QuadRaw(Get(m), c[0], c[1], c[2], c[3], n, WorldUv(c[0], n), WorldUv(c[1], n), WorldUv(c[2], n), WorldUv(c[3], n));
                }
        }

        void Face(Buf b, Vector3 lo, Vector3 hi, int axis, int sign)
        {
            var n = Vector3.zero;
            n[axis] = sign;
            int a1 = (axis + 1) % 3, a2 = (axis + 2) % 3;
            var c = new Vector3[4];
            float fixedV = sign > 0 ? hi[axis] : lo[axis];
            for (int k = 0; k < 4; k++)
            {
                var p = Vector3.zero;
                p[axis] = fixedV;
                p[a1] = (k == 1 || k == 2) ? hi[a1] : lo[a1];
                p[a2] = (k >= 2) ? hi[a2] : lo[a2];
                c[k] = p;
            }
            QuadRaw(b, c[0], c[1], c[2], c[3], n, WorldUv(c[0], n), WorldUv(c[1], n), WorldUv(c[2], n), WorldUv(c[3], n));
        }

        /// <summary>A box turned <paramref name="yawDeg"/> about the vertical through its centre (floor inlay strips), metre UVs.</summary>
        public void BoxYaw(Material m, Vector3 center, Vector3 size, float yawDeg)
        {
            if (m == null) return;
            var b = Get(m);
            var rot = Quaternion.Euler(0f, yawDeg, 0f);
            var h = size * 0.5f;
            for (int axis = 0; axis < 3; axis++)
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    var nl = Vector3.zero;
                    nl[axis] = sign;
                    int a1 = (axis + 1) % 3, a2 = (axis + 2) % 3;
                    var c = new Vector3[4];
                    for (int k = 0; k < 4; k++)
                    {
                        var p = Vector3.zero;
                        p[axis] = sign * h[axis];
                        p[a1] = (k == 1 || k == 2) ? h[a1] : -h[a1];
                        p[a2] = (k >= 2) ? h[a2] : -h[a2];
                        c[k] = center + rot * p;
                    }
                    var n = rot * nl;
                    QuadRaw(b, c[0], c[1], c[2], c[3], n, WorldUv(c[0], n), WorldUv(c[1], n), WorldUv(c[2], n), WorldUv(c[3], n));
                }
        }

        /// <summary>A one-sided quad centred on <paramref name="center"/>, <paramref name="width"/> along <paramref name="right"/>
        /// and <paramref name="height"/> along <paramref name="up"/>, facing Cross(up, right) (towards a viewer who sees
        /// <paramref name="right"/> as their right), showing <paramref name="uv"/> of its texture.</summary>
        public void Quad(Material m, Vector3 center, Vector3 right, Vector3 up, float width, float height, Rect uv)
        {
            if (m == null) return;
            right = right.normalized;
            up = up.normalized;
            var n = Vector3.Cross(up, right).normalized;
            var r = right * (width * 0.5f);
            var u = up * (height * 0.5f);
            QuadRaw(Get(m), center - r - u, center + r - u, center + r + u, center - r + u, n,
                new Vector2(uv.xMin, uv.yMin), new Vector2(uv.xMax, uv.yMin), new Vector2(uv.xMax, uv.yMax), new Vector2(uv.xMin, uv.yMax));
        }

        /// <summary>A flat disc facing <paramref name="normal"/> (downlights), UVs 0..1 across it.</summary>
        public void Disc(Material m, Vector3 center, Vector3 normal, float radius, int segments = 12)
        {
            if (m == null) return;
            var b = Get(m);
            normal = normal.normalized;
            var t1 = Vector3.Cross(normal, Mathf.Abs(normal.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
            var t2 = Vector3.Cross(normal, t1);
            int c0 = b.v.Count;
            b.v.Add(center); b.n.Add(normal); b.uv.Add(new Vector2(0.5f, 0.5f));
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                var d = t1 * Mathf.Cos(a) + t2 * Mathf.Sin(a);
                b.v.Add(center + d * radius); b.n.Add(normal); b.uv.Add(new Vector2(0.5f + 0.5f * Mathf.Cos(a), 0.5f + 0.5f * Mathf.Sin(a)));
            }
            for (int i = 0; i < segments; i++)
            {
                int p = c0 + 1 + i, q = c0 + 1 + (i + 1) % segments;
                bool ok = Vector3.Dot(Vector3.Cross(b.v[p] - center, b.v[q] - center), normal) > 0f;
                if (ok) { b.t.Add(c0); b.t.Add(p); b.t.Add(q); } else { b.t.Add(c0); b.t.Add(q); b.t.Add(p); }
            }
        }

        /// <summary>Appends a mesh (its vertices transformed by <paramref name="toWorld"/>), with world metre UVs.</summary>
        public void Append(Material m, Mesh mesh, Matrix4x4 toWorld)
        {
            var b = Get(m);
            int baseIndex = b.v.Count;
            var verts = mesh.vertices;
            var norms = mesh.normals;
            var normalMatrix = toWorld.inverse.transpose;
            for (int i = 0; i < verts.Length; i++)
            {
                var p = toWorld.MultiplyPoint3x4(verts[i]);
                var n = norms.Length == verts.Length ? normalMatrix.MultiplyVector(norms[i]).normalized : Vector3.up;
                b.v.Add(p); b.n.Add(n); b.uv.Add(WorldUv(p, n));
            }
            foreach (int i in mesh.triangles) b.t.Add(baseIndex + i);
        }

        /// <summary>
        /// Writes everything added since the last flush as one renderer per material under a new
        /// <paramref name="name"/> group (marked <see cref="SkylineNoCollider"/>, reflection-probe static), and clears the kit.
        /// Glass and glowing materials never cast shadows; the rest follow <paramref name="shadows"/>.
        /// </summary>
        public Transform Flush(Transform parent, string name, ShadowCastingMode shadows = ShadowCastingMode.On)
        {
            var root = new GameObject(name).transform;
            if (parent != null) root.SetParent(parent, false);
            root.gameObject.AddComponent<SkylineNoCollider>();
            foreach (var m in _order)
            {
                var b = _bufs[m];
                if (b.v.Count == 0) continue;
                var mesh = new Mesh { name = name + " / " + m.name };
                if (b.v.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(b.v);
                mesh.SetNormals(b.n);
                mesh.SetUVs(0, b.uv);
                mesh.SetTriangles(b.t, 0);
                mesh.RecalculateBounds();
                mesh.RecalculateTangents();
                var go = new GameObject(m.name);
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = m;
                bool noShadow = m.name.StartsWith("SK_Glass", System.StringComparison.Ordinal) ||
                                m.name.StartsWith("SK_Emissive", System.StringComparison.Ordinal);
                r.shadowCastingMode = noShadow ? ShadowCastingMode.Off : shadows;
                r.receiveShadows = true;
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.ReflectionProbeStatic);
            }
            _bufs.Clear();
            _order.Clear();
            return root;
        }
    }
}
