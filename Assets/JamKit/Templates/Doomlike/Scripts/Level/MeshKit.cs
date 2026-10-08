using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace JamKit.Doomlike
{
    /// <summary>Collects quads for one material and turns them into a mesh (32-bit indices when big).</summary>
    public sealed class MeshBatch
    {
        readonly List<Vector3> _v = new List<Vector3>();
        readonly List<Vector2> _uv = new List<Vector2>();
        readonly List<Color32> _c = new List<Color32>();
        readonly List<int> _t = new List<int>();

        public int QuadCount => _t.Count / 6;

        /// <summary>a, b, c, d clockwise as seen from the front (bottom-left, top-left, top-right, bottom-right).</summary>
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud,
            Color32 bottom, Color32 top)
        {
            int i = _v.Count;
            _v.Add(a); _v.Add(b); _v.Add(c); _v.Add(d);
            _uv.Add(ua); _uv.Add(ub); _uv.Add(uc); _uv.Add(ud);
            _c.Add(bottom); _c.Add(top); _c.Add(top); _c.Add(bottom);
            _t.Add(i); _t.Add(i + 1); _t.Add(i + 2);
            _t.Add(i); _t.Add(i + 2); _t.Add(i + 3);
        }

        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name };
            if (_v.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(_v);
            m.SetUVs(0, _uv);
            m.SetColors(_c);
            m.SetTriangles(_t, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }

    /// <summary>Small meshes made in code: the billboard quad and boxes for doors.</summary>
    public static class MeshKit
    {
        static Mesh _quad;

        /// <summary>A 1 x 1 quad standing on its bottom edge (pivot at the feet), facing -Z, UV 0..1.</summary>
        public static Mesh BillboardQuad
        {
            get
            {
                if (_quad != null) return _quad;
                _quad = new Mesh { name = "Doomlike Billboard" };
                _quad.vertices = new[] { new Vector3(-0.5f, 0f, 0f), new Vector3(-0.5f, 1f, 0f), new Vector3(0.5f, 1f, 0f), new Vector3(0.5f, 0f, 0f) };
                _quad.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
                _quad.colors32 = new[] { new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255) };
                _quad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                _quad.RecalculateNormals();
                _quad.RecalculateBounds();
                _quad.hideFlags = HideFlags.DontSave;
                return _quad;
            }
        }

        /// <summary>A box centred on the origin with texture tiling of one repeat per
        /// <paramref name="texelWorld"/> metres, and vertex colours (sides shaded like the walls).</summary>
        public static Mesh Box(Vector3 size, float texelWorld, Color tint)
        {
            var b = new MeshBatch();
            Vector3 h = size * 0.5f;
            float k = 1f / Mathf.Max(0.01f, texelWorld);
            Color32 ns = tint * 0.82f, ew = tint, top = tint * 0.9f;
            ns.a = ew.a = top.a = 255;
            Color32 nsLow = (Color)ns * 0.85f, ewLow = (Color)ew * 0.85f;
            nsLow.a = ewLow.a = 255;
            float sx = size.x * k, sy = size.y * k, sz = size.z * k;
            // +Z face (seen from +Z looking -Z: right is -X)
            b.Quad(new Vector3(h.x, -h.y, h.z), new Vector3(h.x, h.y, h.z), new Vector3(-h.x, h.y, h.z), new Vector3(-h.x, -h.y, h.z),
                new Vector2(0, 0), new Vector2(0, sy), new Vector2(sx, sy), new Vector2(sx, 0), nsLow, ns);
            // -Z face
            b.Quad(new Vector3(-h.x, -h.y, -h.z), new Vector3(-h.x, h.y, -h.z), new Vector3(h.x, h.y, -h.z), new Vector3(h.x, -h.y, -h.z),
                new Vector2(0, 0), new Vector2(0, sy), new Vector2(sx, sy), new Vector2(sx, 0), nsLow, ns);
            // +X face (seen from +X: right is +Z)
            b.Quad(new Vector3(h.x, -h.y, -h.z), new Vector3(h.x, h.y, -h.z), new Vector3(h.x, h.y, h.z), new Vector3(h.x, -h.y, h.z),
                new Vector2(0, 0), new Vector2(0, sy), new Vector2(sz, sy), new Vector2(sz, 0), ewLow, ew);
            // -X face
            b.Quad(new Vector3(-h.x, -h.y, h.z), new Vector3(-h.x, h.y, h.z), new Vector3(-h.x, h.y, -h.z), new Vector3(-h.x, -h.y, -h.z),
                new Vector2(0, 0), new Vector2(0, sy), new Vector2(sz, sy), new Vector2(sz, 0), ewLow, ew);
            // Top (seen from above: up is +Z)
            b.Quad(new Vector3(-h.x, h.y, -h.z), new Vector3(-h.x, h.y, h.z), new Vector3(h.x, h.y, h.z), new Vector3(h.x, h.y, -h.z),
                new Vector2(0, 0), new Vector2(0, sz), new Vector2(sx, sz), new Vector2(sx, 0), top, top);
            // Bottom
            b.Quad(new Vector3(-h.x, -h.y, h.z), new Vector3(-h.x, -h.y, -h.z), new Vector3(h.x, -h.y, -h.z), new Vector3(h.x, -h.y, h.z),
                new Vector2(0, 0), new Vector2(0, sz), new Vector2(sx, sz), new Vector2(sx, 0), top, top);
            return b.ToMesh("Doomlike Box");
        }

        public static Color32 Shade(Color c, float k)
        {
            Color32 r = new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), 1f);
            return r;
        }
    }
}
