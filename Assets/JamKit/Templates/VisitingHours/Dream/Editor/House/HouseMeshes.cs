using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace JamKit.DreamEscape.EditorTools
{
    /// <summary>
    /// Builds the house's meshes in the house's frame: quads with metre UVs (world-aligned, so the pattern runs on across
    /// pieces: 1 UV unit = 1 metre, like the Look library), one submesh per material. A <see cref="MeshBuilder"/> collects
    /// quads by material and makes one GameObject (MeshFilter + MeshRenderer) with them.
    /// </summary>
    public sealed class MeshBuilder
    {
        readonly List<Vector3> _v = new List<Vector3>();
        readonly List<Vector3> _n = new List<Vector3>();
        readonly List<Vector2> _uv = new List<Vector2>();
        readonly List<Material> _mats = new List<Material>();
        readonly List<List<int>> _tris = new List<List<int>>();

        int _quads;
        public int QuadCount => _quads;
        public bool Empty => _v.Count == 0;

        List<int> TrisFor(Material m)
        {
            int i = _mats.IndexOf(m);
            if (i >= 0) return _tris[i];
            _mats.Add(m);
            var l = new List<int>();
            _tris.Add(l);
            return l;
        }

        /// <summary>A quad a-b-c-d (counter-clockwise seen from the front: the side its normal points to), metre UVs.</summary>
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Material m)
        {
            if (m == null) return;
            var n = Vector3.Cross(b - a, d - a);
            if (n.sqrMagnitude < 1e-10f) return;
            n.Normalize();
            var t = TrisFor(m);
            int i = _v.Count;
            _v.Add(a); _v.Add(b); _v.Add(c); _v.Add(d);
            for (int k = 0; k < 4; k++) _n.Add(n);
            _uv.Add(Uv(a, n)); _uv.Add(Uv(b, n)); _uv.Add(Uv(c, n)); _uv.Add(Uv(d, n));
            t.Add(i); t.Add(i + 1); t.Add(i + 2);
            t.Add(i); t.Add(i + 2); t.Add(i + 3);
            _quads++;
        }

        public void Tri(Vector3 a, Vector3 b, Vector3 c, Material m)
        {
            if (m == null) return;
            var n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-10f) return;
            n.Normalize();
            var t = TrisFor(m);
            int i = _v.Count;
            _v.Add(a); _v.Add(b); _v.Add(c);
            for (int k = 0; k < 3; k++) _n.Add(n);
            _uv.Add(Uv(a, n)); _uv.Add(Uv(b, n)); _uv.Add(Uv(c, n));
            t.Add(i); t.Add(i + 1); t.Add(i + 2);
        }

        /// <summary>A quad turned to face away from <paramref name="centre"/> (the winding is flipped when its normal
        /// points at it): for faces built from computed corners whose order isn't known (slopes, prisms).</summary>
        public void QuadFacing(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Material m, Vector3 centre)
        {
            var n = Vector3.Cross(b - a, d - a);
            if (Vector3.Dot(n, (a + b + c + d) * 0.25f - centre) < 0f) Quad(d, c, b, a, m);
            else Quad(a, b, c, d, m);
        }

        public void TriFacing(Vector3 a, Vector3 b, Vector3 c, Material m, Vector3 centre)
        {
            var n = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(n, (a + b + c) / 3f - centre) < 0f) Tri(c, b, a, m);
            else Tri(a, b, c, m);
        }

        /// <summary>Adds a ready mesh (smooth shapes: <see cref="HouseShapes"/>): its vertices, normals, UVs and
        /// triangles (Unity's winding: clockwise seen from the front).</summary>
        public void Raw(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> tris, Material m)
        {
            if (m == null || v.Count == 0) return;
            var t = TrisFor(m);
            int i0 = _v.Count;
            _v.AddRange(v);
            _n.AddRange(n);
            _uv.AddRange(uv);
            for (int k = 0; k < tris.Count; k++) t.Add(i0 + tris[k]);
            if (Record != null) Record.Add(new HouseDrawn { name = Owner, min = N(Min(v)), max = N(Max(v)), kind = Kind, owner = OwnerPart });
        }

        static System.Numerics.Vector3 N(Vector3 v) => new System.Numerics.Vector3(v.x, v.y, v.z);
        static Vector3 Min(List<Vector3> v) { var m = v[0]; for (int i = 1; i < v.Count; i++) m = Vector3.Min(m, v[i]); return m; }
        static Vector3 Max(List<Vector3> v) { var m = v[0]; for (int i = 1; i < v.Count; i++) m = Vector3.Max(m, v[i]); return m; }

        // ---------- What was drawn, for the mesh lint (HouseMeshLint) ----------

        /// <summary>When set, every box and shape drawn is recorded here (in the builder's frame) with
        /// <see cref="Owner"/>, <see cref="OwnerPart"/> and <see cref="Kind"/>.</summary>
        public List<HouseDrawn> Record = DefaultRecord;

        /// <summary>New builders record into this while it's set (the house's build sets it for the mesh lint).</summary>
        public static List<HouseDrawn> DefaultRecord;
        public string Owner = "";
        public int OwnerPart = -1;
        public DrawnKind Kind = DrawnKind.Owned;

        /// <summary>World-aligned metre UVs: along the face's plane (walls: horizontal along the wall, vertical up).</summary>
        static Vector2 Uv(Vector3 p, Vector3 n)
        {
            float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
            if (ay >= ax && ay >= az) return new Vector2(p.x, p.z);
            if (ax >= az) return new Vector2(p.z * Mathf.Sign(n.x), p.y);
            return new Vector2(-p.x * Mathf.Sign(n.z), p.y);
        }

        /// <summary>An axis-aligned box's six faces (outward), each with its own material (null skips the face).
        /// Order: -X, +X, -Y, +Y, -Z, +Z.</summary>
        public void Box(Vector3 min, Vector3 max, Material[] faces)
        {
            if (Record != null) Record.Add(new HouseDrawn { name = Owner, min = N(min), max = N(max), kind = Kind, owner = OwnerPart });
            Vector3 a = min, b = max;
            var p000 = new Vector3(a.x, a.y, a.z); var p100 = new Vector3(b.x, a.y, a.z);
            var p010 = new Vector3(a.x, b.y, a.z); var p110 = new Vector3(b.x, b.y, a.z);
            var p001 = new Vector3(a.x, a.y, b.z); var p101 = new Vector3(b.x, a.y, b.z);
            var p011 = new Vector3(a.x, b.y, b.z); var p111 = new Vector3(b.x, b.y, b.z);
            Quad(p001, p011, p010, p000, faces[0]);   // -X
            Quad(p100, p110, p111, p101, faces[1]);   // +X
            Quad(p000, p100, p101, p001, faces[2]);   // -Y
            Quad(p010, p011, p111, p110, faces[3]);   // +Y
            Quad(p000, p010, p110, p100, faces[4]);   // -Z
            Quad(p101, p111, p011, p001, faces[5]);   // +Z
        }

        public void Box(Vector3 min, Vector3 max, Material m) => Box(min, max, new[] { m, m, m, m, m, m });

        /// <summary>A box rotated: its centre, its three half-axes (u, v, w scaled), six faces with one material each
        /// (order: -u, +u, -v, +v, -w, +w).</summary>
        public void Obb(Vector3 c, Vector3 u, Vector3 v, Vector3 w, Material[] faces)
        {
            if (Record != null)
            {
                var e = new Vector3(Mathf.Abs(u.x) + Mathf.Abs(v.x) + Mathf.Abs(w.x), Mathf.Abs(u.y) + Mathf.Abs(v.y) + Mathf.Abs(w.y), Mathf.Abs(u.z) + Mathf.Abs(v.z) + Mathf.Abs(w.z));
                Record.Add(new HouseDrawn { name = Owner, min = N(c - e), max = N(c + e), kind = Kind, owner = OwnerPart, oriented = true });
            }
            // A left-handed set of axes would turn every face inward: flip one (the box is the same box).
            if (Vector3.Dot(Vector3.Cross(u, v), w) < 0f)
            {
                u = -u;
                var f = (Material[])faces.Clone();
                faces = new[] { f[1], f[0], f[2], f[3], f[4], f[5] };
            }
            Vector3 P(float x, float y, float z) => c + u * x + v * y + w * z;
            Quad(P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1), P(-1, -1, -1), faces[0]);
            Quad(P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), P(1, -1, 1), faces[1]);
            Quad(P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), P(-1, -1, 1), faces[2]);
            Quad(P(-1, 1, -1), P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1), faces[3]);
            Quad(P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1), P(1, -1, -1), faces[4]);
            Quad(P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), P(-1, -1, 1), faces[5]);
        }

        /// <summary>The mesh as a GameObject under <paramref name="parent"/> (static, casting and receiving shadows).</summary>
        public GameObject Build(string name, Transform parent, bool shadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            if (Empty) return go;
            var mesh = new Mesh { name = name };
            if (_v.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(_v);
            mesh.SetNormals(_n);
            mesh.SetUVs(0, _uv);
            mesh.subMeshCount = _mats.Count;
            for (int i = 0; i < _mats.Count; i++) mesh.SetTriangles(_tris[i], i);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = _mats.ToArray();
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            r.receiveShadows = true;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);
            return go;
        }
    }
}
