using System.Collections.Generic;
using UnityEngine;

namespace JamKit.DreamEscape.EditorTools
{
    /// <summary>
    /// Smooth shapes for the house's own props (house-1.md blocker 4: built-in-place parts read as grey-box primitives):
    /// rounded boxes (mattresses, duvets, pillows, cushions, appliance bodies, worktops), lathed solids (a telescope's
    /// tube and fittings, a water heater, lamp shades, a tap, an umbrella, pots), tubes along a path (rails, a washing
    /// line, coat hooks, legs, a tap's spout), and hanging cloth with folds (curtains, sheets, coats). All with smooth
    /// normals and metre UVs, added to a <see cref="MeshBuilder"/> (one mesh per material, recorded for the mesh lint).
    /// Every triangle's winding is checked against its intended normal, so nothing renders inside out.
    /// </summary>
    public static class HouseShapes
    {
        static readonly List<Vector3> V = new List<Vector3>(4096);
        static readonly List<Vector3> N = new List<Vector3>(4096);
        static readonly List<Vector2> T = new List<Vector2>(4096);
        static readonly List<int> I = new List<int>(8192);

        static void Begin() { V.Clear(); N.Clear(); T.Clear(); I.Clear(); }

        static void Tri(int a, int b, int c)
        {
            var n = Vector3.Cross(V[b] - V[a], V[c] - V[a]);
            if (n.sqrMagnitude < 1e-14f) return;
            if (Vector3.Dot(n, N[a] + N[b] + N[c]) < 0f) { I.Add(a); I.Add(c); I.Add(b); }
            else { I.Add(a); I.Add(b); I.Add(c); }
        }

        static void End(MeshBuilder b, Material m) => b.Raw(V, N, T, I, m);

        static Vector2 BoxUv(Vector3 p, Vector3 n)
        {
            float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
            if (ay >= ax && ay >= az) return new Vector2(p.x, p.z);
            if (ax >= az) return new Vector2(p.z, p.y);
            return new Vector2(p.x, p.y);
        }

        // =====================================================================================================

        /// <summary>A box with rounded edges and corners (<paramref name="radius"/>, clamped to half the smallest side),
        /// <paramref name="seg"/> steps round each edge, turned by <paramref name="rot"/> about its centre, optionally
        /// deformed (<paramref name="puff"/>: the top and bottom bulge by that much at the middle: a pillow).</summary>
        public static void RoundedBox(MeshBuilder b, Vector3 center, Vector3 size, float radius, Material m, Quaternion? rot = null, int seg = 3, float puff = 0f)
        {
            if (m == null) return;
            Begin();
            var h = size * 0.5f;
            float r = Mathf.Min(radius, Mathf.Min(h.x, Mathf.Min(h.y, h.z)) * 0.999f);
            var inner = h - Vector3.one * r;
            var q = rot ?? Quaternion.identity;
            // Grid coordinates along an axis: -h, the rounded steps to -inner, then +inner, the steps to +h.
            float[] Coords(float hh, float ii)
            {
                var c = new List<float>();
                for (int k = 0; k <= seg; k++) c.Add(-hh + (hh - ii) * k / seg);
                for (int k = 0; k <= seg; k++) c.Add(ii + (hh - ii) * k / seg);
                return c.ToArray();
            }
            var cx = Coords(h.x, inner.x); var cy = Coords(h.y, inner.y); var cz = Coords(h.z, inner.z);
            void Face(int axis, int sign)
            {
                float[] ca = axis == 0 ? cy : axis == 1 ? cx : cx, cb = axis == 0 ? cz : axis == 1 ? cz : cy;
                int na = ca.Length, nb = cb.Length, start = V.Count;
                for (int i = 0; i < na; i++)
                    for (int j = 0; j < nb; j++)
                    {
                        Vector3 p = axis == 0 ? new Vector3(sign * h.x, ca[i], cb[j]) : axis == 1 ? new Vector3(ca[i], sign * h.y, cb[j]) : new Vector3(ca[i], cb[j], sign * h.z);
                        var c = new Vector3(Mathf.Clamp(p.x, -inner.x, inner.x), Mathf.Clamp(p.y, -inner.y, inner.y), Mathf.Clamp(p.z, -inner.z, inner.z));
                        var d = p - c;
                        var n = d.sqrMagnitude > 1e-12f ? d.normalized : (axis == 0 ? Vector3.right * sign : axis == 1 ? Vector3.up * sign : Vector3.forward * sign);
                        var pos = c + n * r;
                        if (puff != 0f && Mathf.Abs(pos.y) > 1e-5f)
                        {
                            float ux = Mathf.Clamp01(1f - (pos.x / h.x) * (pos.x / h.x)), uz = Mathf.Clamp01(1f - (pos.z / h.z) * (pos.z / h.z));
                            pos.y += Mathf.Sign(pos.y) * puff * ux * uz;
                        }
                        var w = center + q * pos;
                        var wn = q * n;
                        V.Add(w); N.Add(wn); T.Add(BoxUv(w, wn));
                    }
                for (int i = 0; i < na - 1; i++)
                    for (int j = 0; j < nb - 1; j++)
                    {
                        int a = start + i * nb + j;
                        Tri(a, a + nb, a + nb + 1);
                        Tri(a, a + nb + 1, a + 1);
                    }
            }
            for (int s = -1; s <= 1; s += 2) { Face(0, s); Face(1, s); Face(2, s); }
            if (puff != 0f) RecalcNormals();
            End(b, m);
        }

        /// <summary>Smooth normals from the faces (after a deformation).</summary>
        static void RecalcNormals()
        {
            var acc = new Vector3[V.Count];
            for (int k = 0; k < I.Count; k += 3)
            {
                var n = Vector3.Cross(V[I[k + 1]] - V[I[k]], V[I[k + 2]] - V[I[k]]);
                acc[I[k]] += n; acc[I[k + 1]] += n; acc[I[k + 2]] += n;
            }
            for (int i = 0; i < V.Count; i++) if (acc[i].sqrMagnitude > 1e-12f) N[i] = acc[i].normalized;
        }

        /// <summary>A solid of revolution: <paramref name="profile"/> (radius, height) from bottom to top round the local
        /// y axis, placed at <paramref name="origin"/> and turned by <paramref name="rot"/> (so a lathe can lie along any
        /// direction: a telescope's tube). A radius of 0 closes the end.</summary>
        public static void Lathe(MeshBuilder b, Vector3 origin, Quaternion rot, Vector2[] profile, Material m, int sides = 20)
        {
            if (m == null || profile.Length < 2) return;
            Begin();
            float vAcc = 0f;
            for (int k = 0; k < profile.Length; k++)
            {
                if (k > 0) vAcc += Vector2.Distance(profile[k], profile[k - 1]);
                // The profile's tangent for the normal.
                var t = (k == 0 ? profile[1] - profile[0] : k == profile.Length - 1 ? profile[k] - profile[k - 1] : profile[k + 1] - profile[k - 1]).normalized;
                var n2 = new Vector2(t.y, -t.x);   // outward (radius grows with x)
                for (int s = 0; s <= sides; s++)
                {
                    float a = s * Mathf.PI * 2f / sides;
                    var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    var p = dir * profile[k].x + Vector3.up * profile[k].y;
                    var n = (dir * n2.x + Vector3.up * n2.y).normalized;
                    V.Add(origin + rot * p); N.Add(rot * n);
                    T.Add(new Vector2(a * Mathf.Max(0.02f, profile[k].x), vAcc));
                }
            }
            int row = sides + 1;
            for (int k = 0; k < profile.Length - 1; k++)
                for (int s = 0; s < sides; s++)
                {
                    int a = k * row + s;
                    Tri(a, a + row, a + row + 1);
                    Tri(a, a + row + 1, a + 1);
                }
            End(b, m);
        }

        /// <summary>A cylinder from <paramref name="a"/> to <paramref name="c"/> (closed ends).</summary>
        public static void Rod(MeshBuilder b, Vector3 a, Vector3 c, float radius, Material m, int sides = 10)
        {
            var d = c - a;
            if (d.sqrMagnitude < 1e-8f) return;
            var rot = Quaternion.FromToRotation(Vector3.up, d.normalized);
            float len = d.magnitude;
            Lathe(b, a, rot, new[] { new Vector2(0f, 0f), new Vector2(radius, 0f), new Vector2(radius, len), new Vector2(0f, len) }, m, sides);
        }

        /// <summary>A round tube along <paramref name="path"/> (a hook, a spout, a rail, a line), its ends closed.</summary>
        public static void Tube(MeshBuilder b, IList<Vector3> path, float radius, Material m, int sides = 8)
        {
            if (m == null || path.Count < 2) return;
            Begin();
            var up = Vector3.up;
            Vector3 prevSide = Vector3.zero;
            float v = 0f;
            for (int k = 0; k < path.Count; k++)
            {
                var t = (k == 0 ? path[1] - path[0] : k == path.Count - 1 ? path[k] - path[k - 1] : path[k + 1] - path[k - 1]).normalized;
                var side = k == 0 ? Vector3.Cross(Mathf.Abs(Vector3.Dot(t, up)) > 0.95f ? Vector3.right : up, t).normalized : Vector3.Cross(t, Vector3.Cross(prevSide, t)).normalized;
                if (side.sqrMagnitude < 1e-6f) side = Vector3.Cross(t, Vector3.right).normalized;
                prevSide = side;
                var other = Vector3.Cross(t, side);
                if (k > 0) v += Vector3.Distance(path[k], path[k - 1]);
                for (int s = 0; s <= sides; s++)
                {
                    float a = s * Mathf.PI * 2f / sides;
                    var n = side * Mathf.Cos(a) + other * Mathf.Sin(a);
                    V.Add(path[k] + n * radius); N.Add(n); T.Add(new Vector2(a * radius, v));
                }
            }
            int row = sides + 1;
            for (int k = 0; k < path.Count - 1; k++)
                for (int s = 0; s < sides; s++)
                {
                    int a = k * row + s;
                    Tri(a, a + row, a + row + 1);
                    Tri(a, a + row + 1, a + 1);
                }
            // End caps (fans).
            for (int e = 0; e < 2; e++)
            {
                int ring = e == 0 ? 0 : (path.Count - 1) * row;
                var dir = e == 0 ? (path[0] - path[1]).normalized : (path[path.Count - 1] - path[path.Count - 2]).normalized;
                int c = V.Count;
                V.Add(path[e == 0 ? 0 : path.Count - 1]); N.Add(dir); T.Add(Vector2.zero);
                int first = V.Count;
                for (int s = 0; s <= sides; s++) { V.Add(V[ring + s]); N.Add(dir); T.Add(new Vector2(Mathf.Cos(s * 6.2832f / sides), Mathf.Sin(s * 6.2832f / sides)) * radius); }
                for (int s = 0; s < sides; s++) Tri(c, first + s, first + s + 1);
            }
            End(b, m);
        }

        /// <summary>Hanging cloth: a panel from its top edge (<paramref name="topLeft"/>, running <paramref name="along"/>
        /// for <paramref name="width"/>), hanging <paramref name="height"/>, with <paramref name="folds"/> soft vertical
        /// folds <paramref name="depth"/> deep (deeper toward the hem), facing <paramref name="facing"/>; the top edge sags
        /// by <paramref name="sag"/> at its middle (a sheet on a line). Both sides drawn.</summary>
        public static void Cloth(MeshBuilder b, Vector3 topLeft, Vector3 along, Vector3 facing, float width, float height, int folds, float depth, Material m, float sag = 0f, float hemFlare = 0.4f)
        {
            if (m == null) return;
            Begin();
            along = along.normalized; facing = facing.normalized;
            int nx = Mathf.Max(8, folds * 6), ny = 8;
            for (int side = 0; side < 2; side++)
            {
                int start = V.Count;
                for (int j = 0; j <= ny; j++)
                {
                    float v = j / (float)ny;
                    for (int i = 0; i <= nx; i++)
                    {
                        float u = i / (float)nx;
                        float fold = Mathf.Sin(u * folds * Mathf.PI * 2f) * depth * (1f + hemFlare * v);
                        float sagY = sag * Mathf.Sin(u * Mathf.PI) * (1f - 0.6f * v);
                        var p = topLeft + along * (u * width) - Vector3.up * (v * height + sagY) + facing * fold;
                        // The normal: across the fold's slope.
                        float slope = Mathf.Cos(u * folds * Mathf.PI * 2f) * depth * (1f + hemFlare * v) * folds * Mathf.PI * 2f / width;
                        var n = (facing - along * slope).normalized * (side == 0 ? 1f : -1f);
                        V.Add(p); N.Add(n); T.Add(new Vector2(u * width, -v * height));
                    }
                }
                int row = nx + 1;
                for (int j = 0; j < ny; j++)
                    for (int i = 0; i < nx; i++)
                    {
                        int a = start + j * row + i;
                        Tri(a, a + row, a + row + 1);
                        Tri(a, a + row + 1, a + 1);
                    }
            }
            End(b, m);
        }
    }
}
