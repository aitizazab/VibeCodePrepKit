using System.Collections.Generic;
using JamKit.Look.EditorTools;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace JamKit.DreamEscape.EditorTools
{
    /// <summary>
    /// Round things for the escape's dressing (smooth-shaded, merged by material like <see cref="MeshBuilder"/>): rods,
    /// cords, legs, balusters, knobs, shades, rings. Boxes stay in <see cref="MeshBuilder"/>.
    /// </summary>
    public sealed class HouseEscapeKit
    {
        readonly List<Material> _mats = new List<Material>();
        readonly List<List<Vector3>> _v = new List<List<Vector3>>();
        readonly List<List<Vector3>> _n = new List<List<Vector3>>();
        readonly List<List<Vector2>> _uv = new List<List<Vector2>>();
        readonly List<List<int>> _t = new List<List<int>>();

        public bool Empty => _mats.Count == 0;

        int Slot(Material m)
        {
            int i = _mats.IndexOf(m);
            if (i >= 0) return i;
            _mats.Add(m); _v.Add(new List<Vector3>()); _n.Add(new List<Vector3>()); _uv.Add(new List<Vector2>()); _t.Add(new List<int>());
            return _mats.Count - 1;
        }

        /// <summary>A rod from <paramref name="a"/> to <paramref name="b"/> (radii at each end), capped.</summary>
        public void Rod(Vector3 a, Vector3 b, float ra, float rb, Material m, int sides = 10)
        {
            if (m == null || (b - a).sqrMagnitude < 1e-8f) return;
            int s = Slot(m);
            VhLookTrees.Tube(_v[s], _n[s], _uv[s], _t[s], a, b, ra, rb, sides, 0f);
            Cap(s, a, a - b, ra, sides);
            Cap(s, b, b - a, rb, sides);
        }

        public void Rod(Vector3 a, Vector3 b, float r, Material m, int sides = 10) => Rod(a, b, r, r, m, sides);

        void Cap(int s, Vector3 c, Vector3 outward, float r, int sides)
        {
            var n = outward.normalized;
            var t1 = Vector3.Cross(n, Mathf.Abs(n.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            var t2 = Vector3.Cross(n, t1);
            var v = _v[s]; var nn = _n[s]; var uv = _uv[s]; var t = _t[s];
            int c0 = v.Count;
            v.Add(c); nn.Add(n); uv.Add(new Vector2(0.5f, 0.5f));
            for (int k = 0; k <= sides; k++)
            {
                float ang = k * Mathf.PI * 2f / sides;
                var d = t1 * Mathf.Cos(ang) + t2 * Mathf.Sin(ang);
                v.Add(c + d * r); nn.Add(n); uv.Add(new Vector2(0.5f + Mathf.Cos(ang) * 0.5f, 0.5f + Mathf.Sin(ang) * 0.5f));
            }
            for (int k = 0; k < sides; k++)
            {
                // Wind so the cap faces outward.
                var a = v[c0 + 1 + k]; var b = v[c0 + 2 + k];
                if (Vector3.Dot(Vector3.Cross(a - c, b - c), n) > 0f) { t.Add(c0); t.Add(c0 + 1 + k); t.Add(c0 + 2 + k); }
                else { t.Add(c0); t.Add(c0 + 2 + k); t.Add(c0 + 1 + k); }
            }
        }

        /// <summary>A sphere (a knob, a bulb, a ball finial).</summary>
        public void Ball(Vector3 c, float r, Material m, int seg = 10)
        {
            if (m == null) return;
            int s = Slot(m);
            var v = _v[s]; var nn = _n[s]; var uv = _uv[s]; var t = _t[s];
            int b0 = v.Count, rows = seg, cols = seg * 2;
            for (int i = 0; i <= rows; i++)
                for (int j = 0; j <= cols; j++)
                {
                    float th = Mathf.PI * i / rows, ph = Mathf.PI * 2f * j / cols;
                    var d = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph));
                    v.Add(c + d * r); nn.Add(d); uv.Add(new Vector2((float)j / cols, 1f - (float)i / rows));
                }
            for (int i = 0; i < rows; i++)
                for (int j = 0; j < cols; j++)
                {
                    int a = b0 + i * (cols + 1) + j, b = a + cols + 1;
                    t.Add(a); t.Add(a + 1); t.Add(b);
                    t.Add(a + 1); t.Add(b + 1); t.Add(b);
                }
        }

        /// <summary>A lathe round <paramref name="axis"/> through <paramref name="c"/>: profile points (radius, height).</summary>
        public void Lathe(Vector3 c, Vector3 axis, Vector2[] profile, Material m, int sides = 16, bool doubleSided = false)
        {
            if (m == null || profile == null || profile.Length < 2) return;
            int s = Slot(m);
            var up = axis.normalized;
            var t1 = Vector3.Cross(up, Mathf.Abs(up.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            var t2 = Vector3.Cross(up, t1);
            var v = _v[s]; var nn = _n[s]; var uv = _uv[s]; var t = _t[s];
            int b0 = v.Count;
            for (int i = 0; i < profile.Length; i++)
            {
                var pr = profile[i];
                var prev = profile[Mathf.Max(0, i - 1)]; var next = profile[Mathf.Min(profile.Length - 1, i + 1)];
                var tan = next - prev;
                var n2 = new Vector2(tan.y, -tan.x).normalized;   // outward in (radius, height)
                for (int k = 0; k <= sides; k++)
                {
                    float ang = k * Mathf.PI * 2f / sides;
                    var d = t1 * Mathf.Cos(ang) + t2 * Mathf.Sin(ang);
                    v.Add(c + d * pr.x + up * pr.y);
                    nn.Add((d * n2.x + up * n2.y).normalized);
                    uv.Add(new Vector2((float)k / sides, pr.y));
                }
            }
            for (int i = 0; i + 1 < profile.Length; i++)
                for (int k = 0; k < sides; k++)
                {
                    int a = b0 + i * (sides + 1) + k, b = a + sides + 1;
                    // Faces outward: check one triangle's winding against its normal.
                    var p0 = v[a]; var p1 = v[b]; var p2 = v[a + 1];
                    bool flip = Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), nn[a] + nn[b] + nn[a + 1]) < 0f;
                    if (!flip) { t.Add(a); t.Add(b); t.Add(a + 1); t.Add(a + 1); t.Add(b); t.Add(b + 1); }
                    else { t.Add(a); t.Add(a + 1); t.Add(b); t.Add(a + 1); t.Add(b + 1); t.Add(b); }
                    if (doubleSided)
                    {
                        if (!flip) { t.Add(a); t.Add(a + 1); t.Add(b); t.Add(a + 1); t.Add(b + 1); t.Add(b); }
                        else { t.Add(a); t.Add(b); t.Add(a + 1); t.Add(a + 1); t.Add(b); t.Add(b + 1); }
                    }
                }
        }

        /// <summary>A ring of rods (a round window's frame, a hoop): centre, the ring's plane normal, radius, tube radius.</summary>
        public void Ring(Vector3 c, Vector3 normal, float radius, float tube, Material m, int segments = 32)
        {
            var n = normal.normalized;
            var t1 = Vector3.Cross(n, Mathf.Abs(n.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            var t2 = Vector3.Cross(n, t1);
            for (int k = 0; k < segments; k++)
            {
                float a0 = k * Mathf.PI * 2f / segments, a1 = (k + 1) * Mathf.PI * 2f / segments;
                Rod(c + (t1 * Mathf.Cos(a0) + t2 * Mathf.Sin(a0)) * radius, c + (t1 * Mathf.Cos(a1) + t2 * Mathf.Sin(a1)) * radius, tube, m, 8);
            }
        }

        /// <summary>A flat disc (a round glass, a plate), facing <paramref name="normal"/>.</summary>
        public void Disc(Vector3 c, Vector3 normal, float r, Material m, int sides = 32)
        {
            if (m == null) return;
            int s = Slot(m);
            Cap(s, c, normal, r, sides);
        }

        public GameObject Build(string name, Transform parent, bool shadows = true, bool isStatic = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            if (Empty) return go;
            var mesh = new Mesh { name = name };
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>();
            var subs = new List<List<int>>();
            for (int s = 0; s < _mats.Count; s++)
            {
                int b0 = v.Count;
                v.AddRange(_v[s]); n.AddRange(_n[s]); uv.AddRange(_uv[s]);
                var t = new List<int>(_t[s].Count);
                foreach (var i in _t[s]) t.Add(i + b0);
                subs.Add(t);
            }
            if (v.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv);
            mesh.subMeshCount = subs.Count;
            for (int s = 0; s < subs.Count; s++) mesh.SetTriangles(subs[s], s);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = _mats.ToArray();
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            GameObjectUtility.SetStaticEditorFlags(go, isStatic ? (StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic) : 0);
            return go;
        }
    }
}
