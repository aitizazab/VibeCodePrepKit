using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace JamKit.DreamEscape.EditorTools
{
    /// <summary>
    /// Small story props built as real meshes in code (dream art pass 2, 2026-10-11), drawn in the dream's
    /// <c>JamKit/Dream/PastelMesh</c> look: the umbrella (open: eight scalloped panels on ribs; closed: furled folds),
    /// its shaft and hooked handle. Decoration only: no colliders. The meshes live in the generated scene.
    /// </summary>
    public static class DreamProps
    {
        /// <summary>The dream's mesh look in a colour (one material per name).</summary>
        public static Material MeshLook(string name, Color color, float outline = 0.01f)
        {
            var m = DreamEscapeBuilder.Mat("Mesh_" + name, "JamKit/Dream/PastelMesh");
            m.SetColor("_Color", color);
            m.SetVector("_SunDir", DreamEscapeBuilder.SunDir);
            m.SetFloat("_OutlineWidth", outline);
            return m;
        }

        /// <summary>A mesh object under <paramref name="parent"/> (local placement), no collider, no shadows.</summary>
        public static GameObject Show(string name, Mesh mesh, Material mat, Transform parent, Vector3 localPos, Quaternion localRot)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            GameObjectUtility.SetStaticEditorFlags(go, 0);
            return go;
        }

        /// <summary>The open canopy: <paramref name="ribs"/> panels from the tip (at y = height) down to a scalloped rim
        /// (y = 0, radius <paramref name="radius"/>), each panel bowing out between its ribs. Both sides are drawn, so
        /// looking up from under it shows the underside.</summary>
        public static Mesh OpenCanopy(float radius = 0.95f, float height = 0.42f, int ribs = 8)
        {
            const int radial = 7, around = 6;
            var v = new List<Vector3>();
            var tris = new List<int>();
            for (int p = 0; p < ribs; p++)
            {
                int start = v.Count;
                for (int i = 0; i <= radial; i++)
                {
                    float t = i / (float)radial;
                    for (int j = 0; j <= around; j++)
                    {
                        float u = j / (float)around;
                        float ang = (p + u) / ribs * Mathf.PI * 2f;
                        float bow = Mathf.Sin(Mathf.PI * u);                       // 0 on the ribs, 1 mid-panel
                        float r = radius * t * (1f - 0.07f * bow * t);               // the scallop pulls the rim in
                        float y = height * (1f - Mathf.Pow(t, 1.55f)) + 0.05f * bow * t * t;  // and up, between ribs
                        r += 0.03f * bow * Mathf.Sin(Mathf.PI * t);                  // the cloth bellies out
                        v.Add(new Vector3(Mathf.Cos(ang) * r, y, Mathf.Sin(ang) * r));
                    }
                }
                for (int i = 0; i < radial; i++)
                    for (int j = 0; j < around; j++)
                    {
                        int a = start + i * (around + 1) + j, b = a + 1, c = a + around + 1, d = c + 1;
                        tris.AddRange(new[] { a, c, b, b, c, d });
                    }
            }
            return DoubleSided("Umbrella canopy (open)", v, tris);
        }

        /// <summary>The closed canopy: a furled cone of folds around the shaft, from y 0 (the tip end) to
        /// <paramref name="length"/>, fattest a third of the way up, wrapped with a slight twist.</summary>
        public static Mesh FurledCanopy(float length = 0.62f, float girth = 0.085f, int folds = 8)
        {
            const int rings = 14, around = 32;
            var v = new List<Vector3>();
            var tris = new List<int>();
            for (int i = 0; i <= rings; i++)
            {
                float t = i / (float)rings;
                float profile = Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Clamp01(t * 0.85f + 0.08f)), 0.8f) * (1f - 0.35f * t);
                for (int j = 0; j <= around; j++)
                {
                    float ang = j / (float)around * Mathf.PI * 2f;
                    float fold = 1f + 0.22f * Mathf.Abs(Mathf.Sin(folds * 0.5f * ang + t * 2.4f));
                    float r = girth * profile * fold;
                    v.Add(new Vector3(Mathf.Cos(ang) * r, t * length, Mathf.Sin(ang) * r));
                }
            }
            for (int i = 0; i < rings; i++)
                for (int j = 0; j < around; j++)
                {
                    int a = i * (around + 1) + j, b = a + 1, c = a + around + 1, d = c + 1;
                    tris.AddRange(new[] { a, c, b, b, c, d });
                }
            return Finish("Umbrella canopy (furled)", v, tris);
        }

        /// <summary>A tube along a path (the shaft, the hooked handle).</summary>
        public static Mesh Tube(string name, IList<Vector3> path, float radius, int sides = 10)
        {
            var v = new List<Vector3>();
            var tris = new List<int>();
            for (int i = 0; i < path.Count; i++)
            {
                Vector3 fwd = (path[Mathf.Min(i + 1, path.Count - 1)] - path[Mathf.Max(i - 1, 0)]).normalized;
                Vector3 side = Vector3.Cross(fwd, Mathf.Abs(fwd.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
                Vector3 up = Vector3.Cross(side, fwd);
                for (int j = 0; j <= sides; j++)
                {
                    float a = j / (float)sides * Mathf.PI * 2f;
                    v.Add(path[i] + (side * Mathf.Cos(a) + up * Mathf.Sin(a)) * radius);
                }
            }
            for (int i = 0; i < path.Count - 1; i++)
                for (int j = 0; j < sides; j++)
                {
                    int a = i * (sides + 1) + j, b = a + 1, c = a + sides + 1, d = c + 1;
                    tris.AddRange(new[] { a, b, c, b, d, c });
                }
            return DoubleSided(name, v, tris);   // either winding shows: thin tubes are cheap
        }

        /// <summary>The shaft from <paramref name="bottom"/> up <paramref name="length"/>, ending in a J hook.</summary>
        public static Mesh ShaftWithHook(float length, float radius = 0.013f, float hook = 0.055f)
        {
            var path = new List<Vector3>();
            for (int i = 0; i <= 6; i++) path.Add(new Vector3(0f, length * i / 6f, 0f));
            for (int i = 1; i <= 10; i++)
            {
                float a = i / 10f * Mathf.PI * 1.1f;                                 // a hook curling over and down
                path.Add(new Vector3(hook - Mathf.Cos(a) * hook, length + Mathf.Sin(a) * hook, 0f));
            }
            return Tube("Umbrella shaft and handle", path, radius);
        }

        static Mesh DoubleSided(string name, List<Vector3> v, List<int> tris)
        {
            int n = v.Count;
            var all = new List<Vector3>(v);
            all.AddRange(v);
            var t2 = new List<int>(tris);
            for (int i = 0; i < tris.Count; i += 3) t2.AddRange(new[] { tris[i] + n, tris[i + 2] + n, tris[i + 1] + n });
            return Finish(name, all, t2);
        }

        static Mesh Finish(string name, List<Vector3> v, List<int> tris)
        {
            var m = new Mesh { name = name };
            m.SetVertices(v);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }
}
