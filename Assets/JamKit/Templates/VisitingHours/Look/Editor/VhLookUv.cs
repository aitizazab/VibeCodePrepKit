using System.Collections.Generic;
using UnityEngine;

namespace JamKit.Look.EditorTools
{
    /// <summary>
    /// Metre UVs for the Look library's convention (1 UV unit = 1 metre; the material's tiling turns metres into repeats).
    /// <see cref="BoxProject"/> rewrites a mesh's UVs by box projection in the object's scaled space, so a stretched cube
    /// (a wall, a kerb, a slab) shows its texture at true size on every face. The mesh is copied (the shared primitive is
    /// never touched) and lives in the scene. Also builds simple bevelled boxes so edges catch the light.
    /// </summary>
    public static class VhLookUv
    {
        /// <summary>
        /// Box-projects UVs (in metres) onto the GameObject's mesh, measured in world scale: X/Z faces use (z or x, y), top and
        /// bottom use (x, z). <paramref name="worldAligned"/> uses world position too, so neighbouring pieces continue the
        /// pattern seamlessly (good for long walls and roads built from many boxes).
        /// </summary>
        public static void BoxProject(GameObject go, bool worldAligned = true, float rotateTopDeg = 0f)
        {
            var mf = go.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return;
            var src = mf.sharedMesh;
            var mesh = Object.Instantiate(src);
            mesh.name = src.name + " (metre UVs)";
            var t = go.transform;
            var verts = mesh.vertices;
            var normals = mesh.normals;
            var uvs = new Vector2[verts.Length];
            Vector3 scale = t.lossyScale;
            float rot = rotateTopDeg * Mathf.Deg2Rad;
            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 p = worldAligned ? t.TransformPoint(verts[i]) : Vector3.Scale(verts[i], scale);
                Vector3 n = worldAligned ? t.TransformDirection(normals[i]) : normals[i];
                n = new Vector3(Mathf.Abs(n.x), Mathf.Abs(n.y), Mathf.Abs(n.z));
                Vector2 uv;
                if (n.y >= n.x && n.y >= n.z)
                {
                    uv = new Vector2(p.x, p.z);
                    if (rot != 0f) uv = new Vector2(uv.x * Mathf.Cos(rot) - uv.y * Mathf.Sin(rot), uv.x * Mathf.Sin(rot) + uv.y * Mathf.Cos(rot));
                }
                else if (n.x >= n.z) uv = new Vector2(p.z, p.y);
                else uv = new Vector2(p.x, p.y);
                uvs[i] = uv;
            }
            mesh.uv = uvs;
            mesh.RecalculateTangents();
            mf.sharedMesh = mesh;
        }

        /// <summary>
        /// A box with bevelled edges (a chamfer of <paramref name="bevel"/> metres on every edge), centred on the origin, with
        /// metre UVs and tangents. Bevels catch the low sun as a bright line, which is most of what makes blockwork read as
        /// built rather than primitive.
        /// </summary>
        public static Mesh BevelBox(Vector3 size, float bevel)
        {
            bevel = Mathf.Clamp(bevel, 0.001f, Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.45f);
            Vector3 h = size * 0.5f;
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                var n = Vector3.Cross(b - a, d - a).normalized;
                int i = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
                for (int k = 0; k < 4; k++) norms.Add(n);
                var an = new Vector3(Mathf.Abs(n.x), Mathf.Abs(n.y), Mathf.Abs(n.z));
                foreach (var p in new[] { a, b, c, d })
                {
                    if (an.y >= an.x && an.y >= an.z) uvs.Add(new Vector2(p.x, p.z));
                    else if (an.x >= an.z) uvs.Add(new Vector2(p.z, p.y));
                    else uvs.Add(new Vector2(p.x, p.y));
                }
                tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
                tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
            }

            void Tri(Vector3 a, Vector3 b, Vector3 c)
            {
                var n = Vector3.Cross(b - a, c - a).normalized;
                int i = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c);
                for (int k = 0; k < 3; k++) norms.Add(n);
                uvs.Add(new Vector2(a.x + a.z, a.y)); uvs.Add(new Vector2(b.x + b.z, b.y)); uvs.Add(new Vector2(c.x + c.z, c.y));
                tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            }

            float bx = h.x - bevel, by = h.y - bevel, bz = h.z - bevel;
            // Six faces, inset by the bevel.
            Quad(new Vector3(-bx, h.y, -bz), new Vector3(-bx, h.y, bz), new Vector3(bx, h.y, bz), new Vector3(bx, h.y, -bz));      // top
            Quad(new Vector3(-bx, -h.y, bz), new Vector3(-bx, -h.y, -bz), new Vector3(bx, -h.y, -bz), new Vector3(bx, -h.y, bz));  // bottom
            Quad(new Vector3(-bx, -by, -h.z), new Vector3(-bx, by, -h.z), new Vector3(bx, by, -h.z), new Vector3(bx, -by, -h.z));  // -Z
            Quad(new Vector3(bx, -by, h.z), new Vector3(bx, by, h.z), new Vector3(-bx, by, h.z), new Vector3(-bx, -by, h.z));      // +Z
            Quad(new Vector3(-h.x, -by, bz), new Vector3(-h.x, by, bz), new Vector3(-h.x, by, -bz), new Vector3(-h.x, -by, -bz));  // -X
            Quad(new Vector3(h.x, -by, -bz), new Vector3(h.x, by, -bz), new Vector3(h.x, by, bz), new Vector3(h.x, -by, bz));      // +X
            // Twelve edge chamfers.
            for (int sy = -1; sy <= 1; sy += 2)
            {
                float y0 = sy * h.y, y1 = sy * by;
                // Along X (front/back edges of top/bottom).
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    var a = new Vector3(-bx, y0, sz * bz); var b = new Vector3(bx, y0, sz * bz);
                    var c = new Vector3(bx, y1, sz * h.z); var d = new Vector3(-bx, y1, sz * h.z);
                    if (sy * sz > 0) Quad(a, d, c, b); else Quad(a, b, c, d);
                }
                // Along Z (left/right edges of top/bottom).
                for (int sx = -1; sx <= 1; sx += 2)
                {
                    var a = new Vector3(sx * bx, y0, -bz); var b = new Vector3(sx * bx, y0, bz);
                    var c = new Vector3(sx * h.x, y1, bz); var d = new Vector3(sx * h.x, y1, -bz);
                    if (sy * sx > 0) Quad(a, b, c, d); else Quad(a, d, c, b);
                }
                // Corners.
                for (int sx = -1; sx <= 1; sx += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                    {
                        var a = new Vector3(sx * bx, y0, sz * bz);
                        var b = new Vector3(sx * h.x, y1, sz * bz);
                        var c = new Vector3(sx * bx, y1, sz * h.z);
                        bool flip = (sx * sy * sz) > 0;
                        if (flip) Tri(a, c, b); else Tri(a, b, c);
                    }
            }
            // Vertical edges.
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    var a = new Vector3(sx * h.x, -by, sz * bz); var b = new Vector3(sx * h.x, by, sz * bz);
                    var c = new Vector3(sx * bx, by, sz * h.z); var d = new Vector3(sx * bx, -by, sz * h.z);
                    if (sx * sz > 0) Quad(a, b, c, d); else Quad(a, d, c, b);
                }

            var mesh = new Mesh { name = "Bevel box " + size.x.ToString("0.##") + "x" + size.y.ToString("0.##") + "x" + size.z.ToString("0.##") };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            // Make every triangle face outward, whatever winding the loops above produced.
            FixWinding(mesh);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Flips any triangle whose winding disagrees with its stored normal (for convex shapes built by hand).</summary>
        public static void FixWinding(Mesh mesh)
        {
            var v = mesh.vertices;
            var n = mesh.normals;
            var t = mesh.triangles;
            for (int i = 0; i < t.Length; i += 3)
            {
                var a = v[t[i]]; var b = v[t[i + 1]]; var c = v[t[i + 2]];
                var face = Vector3.Cross(b - a, c - a);
                var centre = (a + b + c) / 3f;
                // For a convex box centred on the origin, outward = away from the centre.
                if (Vector3.Dot(face, centre) < 0f)
                {
                    int tmp = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = tmp;
                }
                var outward = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]).normalized;
                n[t[i]] = outward; n[t[i + 1]] = outward; n[t[i + 2]] = outward;
            }
            mesh.triangles = t;
            mesh.normals = n;
        }

        /// <summary>A bevelled box GameObject with a mesh collider-free box collider, metre UVs and the given material.</summary>
        public static GameObject Block(string name, Transform parent, Vector3 center, Vector3 size, Material material, float bevel = 0.02f, float yaw = 0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = BevelBox(size, bevel);
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            var col = go.AddComponent<BoxCollider>();
            col.size = size;
            return go;
        }
    }
}
