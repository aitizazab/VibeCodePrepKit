using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace JamKit.Look.EditorTools
{
    /// <summary>
    /// Stylized deciduous trees for the hill (the kind on a 1990s suburban street: maples and oaks over the lawns), built in
    /// code from the library's bark and the painted leaf-cluster atlas with the JamKit/Look/Foliage shader. Each tree is a
    /// tapered trunk with a few limbs (bark, a capsule collider) and a crown of leaf cards whose normals point out of the
    /// crown, so the crown shades as one soft lit volume and glows when the sun is behind it.
    /// <para>Meshes are cached as assets per (seed, height) in Look/Library/Trees/, so many trees share a few meshes and batch.</para>
    /// <code>VhLookTrees.Place(parent, position, seed: 3, height: 9f);</code>
    /// </summary>
    public static class VhLookTrees
    {
        public const string Dir = VhLookLibrary.MatRoot + "Trees/";

        /// <summary>Places a tree (trunk + crown) with its base at <paramref name="position"/>. Seeds 1-8 give distinct shapes.</summary>
        public static GameObject Place(Transform parent, Vector3 position, int seed = 1, float height = 9f, bool dryCrown = false, float yaw = -1f)
        {
            Get(seed, height, out var trunkMesh, out var crownMesh, out float trunkRadius);
            var go = new GameObject("Tree " + seed + " (" + height.ToString("0.#") + " m)");
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0f, yaw >= 0f ? yaw : (seed * 47f) % 360f, 0f);

            var trunk = new GameObject("Trunk");
            trunk.transform.SetParent(go.transform, false);
            trunk.AddComponent<MeshFilter>().sharedMesh = trunkMesh;
            trunk.AddComponent<MeshRenderer>().sharedMaterial = VhLookLibrary.Get("Tree_Bark");
            var cap = trunk.AddComponent<CapsuleCollider>();
            cap.radius = trunkRadius * 1.1f;
            cap.height = height * 0.55f;
            cap.center = new Vector3(0f, height * 0.275f, 0f);

            var crown = new GameObject("Crown");
            crown.transform.SetParent(go.transform, false);
            crown.AddComponent<MeshFilter>().sharedMesh = crownMesh;
            var mr = crown.AddComponent<MeshRenderer>();
            mr.sharedMaterial = VhLookLibrary.FoliageMaterial(dryCrown);
            return go;
        }

        public static void Get(int seed, float height, out Mesh trunk, out Mesh crown, out float trunkRadius)
        {
            Directory.CreateDirectory(Dir);
            string key = "VH_Tree_" + seed + "_" + Mathf.RoundToInt(height * 10f);
            string tp = Dir + key + "_Trunk.asset", cp = Dir + key + "_Crown.asset";
            trunkRadius = TrunkRadius(height);
            trunk = AssetDatabase.LoadAssetAtPath<Mesh>(tp);
            crown = AssetDatabase.LoadAssetAtPath<Mesh>(cp);
            if (trunk != null && crown != null) return;
            Build(seed, height, out trunk, out crown);
            AssetDatabase.CreateAsset(trunk, tp);
            AssetDatabase.CreateAsset(crown, cp);
        }

        static float TrunkRadius(float height) => 0.035f * height + 0.06f;

        static void Build(int seed, float height, out Mesh trunkMesh, out Mesh crownMesh)
        {
            var rng = new System.Random(seed * 7919 + 13);
            float R() => (float)rng.NextDouble();

            // ---- Trunk and limbs ----
            var tv = new List<Vector3>(); var tn = new List<Vector3>(); var tuv = new List<Vector2>(); var tt = new List<int>();
            float r0 = TrunkRadius(height);
            float fork = height * (0.42f + 0.1f * R());
            var lean = new Vector3((R() - 0.5f) * 0.12f, 1f, (R() - 0.5f) * 0.12f).normalized;
            Vector3 forkPoint = lean * fork;
            Tube(tv, tn, tuv, tt, Vector3.down * 0.3f, forkPoint, r0 * 1.15f, r0 * 0.78f, 10, 0f);
            // Root flare: a short wide cone at the base.
            Tube(tv, tn, tuv, tt, Vector3.down * 0.2f, Vector3.up * 0.45f, r0 * 1.6f, r0 * 1.05f, 10, 0.3f);

            float crownR = height * (0.3f + 0.06f * R());
            float crownRy = height * (0.25f + 0.05f * R());
            var crownC = new Vector3((R() - 0.5f) * 0.6f, height - crownRy * 1.05f, (R() - 0.5f) * 0.6f);
            int limbs = 3 + rng.Next(0, 3);
            var limbEnds = new List<Vector3>();
            for (int i = 0; i < limbs; i++)
            {
                float a = (i + R() * 0.6f) / limbs * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var end = crownC + dir * crownR * (0.45f + 0.25f * R()) + Vector3.up * crownRy * (0.05f + 0.4f * R());
                var start = forkPoint + Vector3.down * (0.2f * R());
                var mid = Vector3.Lerp(start, end, 0.5f) + Vector3.up * 0.4f;
                Tube(tv, tn, tuv, tt, start, mid, r0 * 0.62f, r0 * 0.42f, 7, 0f);
                Tube(tv, tn, tuv, tt, mid, end, r0 * 0.42f, r0 * 0.12f, 6, 0f);
                limbEnds.Add(end);
                // A couple of twigs off each limb.
                for (int k = 0; k < 2; k++)
                {
                    var tw0 = Vector3.Lerp(mid, end, 0.3f + 0.4f * R());
                    var tw1 = tw0 + new Vector3(R() - 0.5f, 0.5f + 0.4f * R(), R() - 0.5f).normalized * crownR * 0.45f;
                    Tube(tv, tn, tuv, tt, tw0, tw1, r0 * 0.16f, r0 * 0.05f, 5, 0f);
                }
            }
            trunkMesh = new Mesh { name = "Tree trunk " + seed };
            trunkMesh.SetVertices(tv); trunkMesh.SetNormals(tn); trunkMesh.SetUVs(0, tuv); trunkMesh.SetTriangles(tt, 0);
            trunkMesh.RecalculateTangents(); trunkMesh.RecalculateBounds();

            // ---- Crown: leaf-cluster cards in lobes round the limb ends ----
            var cv = new List<Vector3>(); var cn = new List<Vector3>(); var cuv = new List<Vector2>(); var cc = new List<Color>(); var ct = new List<int>();
            var lobes = new List<Vector4>();   // xyz centre, w radius
            lobes.Add(new Vector4(crownC.x, crownC.y, crownC.z, crownR * 0.75f));
            foreach (var e in limbEnds) lobes.Add(new Vector4(e.x, e.y + crownRy * 0.15f, e.z, crownR * (0.5f + 0.15f * R())));
            int cards = Mathf.RoundToInt(70 + height * 9f);
            float card = Mathf.Clamp(height * 0.17f, 0.9f, 2.2f);
            for (int i = 0; i < cards; i++)
            {
                var lobe = lobes[rng.Next(lobes.Count)];
                var lc = new Vector3(lobe.x, lobe.y, lobe.z);
                // A point in the lobe's outer shell (cards mostly near the surface: a full silhouette, a hollow-ish inside).
                var d = new Vector3(R() * 2f - 1f, R() * 2f - 1f, R() * 2f - 1f);
                if (d.sqrMagnitude < 1e-3f) d = Vector3.up;
                d = d.normalized;
                d.y *= crownRy / crownR;
                float shell = Mathf.Lerp(0.55f, 1.0f, Mathf.Sqrt(R()));
                var p = lc + d * lobe.w * shell;
                // Shading normal: out of the whole crown (an ellipsoid), a little of the lobe's.
                var fromCrown = p - crownC;
                var ellN = new Vector3(fromCrown.x / (crownR * crownR), fromCrown.y / (crownRy * crownRy), fromCrown.z / (crownR * crownR)).normalized;
                var lobeN = (p - lc).normalized;
                var n = (ellN * 0.75f + lobeN * 0.25f).normalized;
                // Card orientation: facing roughly outward with a random twist, so the crown looks full from any side.
                var facing = (n + new Vector3(R() - 0.5f, R() - 0.5f, R() - 0.5f) * 1.2f).normalized;
                var right = Vector3.Cross(facing, Mathf.Abs(facing.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
                float twist = R() * Mathf.PI * 2f;
                var up = Vector3.Cross(right, facing);
                var r = (right * Mathf.Cos(twist) + up * Mathf.Sin(twist)) * card * 0.5f;
                var u = (-right * Mathf.Sin(twist) + up * Mathf.Cos(twist)) * card * 0.5f;
                int cell = rng.Next(4);
                var uv0 = new Vector2((cell % 2) * 0.5f, (cell / 2) * 0.5f);
                // Tint: per card variation, darker low in the crown and inside it (a cheap occlusion), sway grows outward.
                float heightK = Mathf.InverseLerp(crownC.y - crownRy, crownC.y + crownRy, p.y);
                float inner = Mathf.InverseLerp(0.5f, 1f, shell);
                float v = (0.82f + 0.2f * R()) * Mathf.Lerp(0.72f, 1.05f, heightK * 0.6f + inner * 0.4f);
                var tint = new Color(v * (0.96f + 0.08f * R()), v, v * (0.9f + 0.1f * R()), Mathf.Clamp01(new Vector2(p.x, p.z).magnitude / crownR) * 0.8f + 0.2f);
                int b = cv.Count;
                cv.Add(p - r - u); cv.Add(p - r + u); cv.Add(p + r + u); cv.Add(p + r - u);
                cuv.Add(uv0 + new Vector2(0.01f, 0.01f)); cuv.Add(uv0 + new Vector2(0.01f, 0.49f));
                cuv.Add(uv0 + new Vector2(0.49f, 0.49f)); cuv.Add(uv0 + new Vector2(0.49f, 0.01f));
                for (int k = 0; k < 4; k++) { cn.Add(n); cc.Add(tint); }
                ct.Add(b); ct.Add(b + 1); ct.Add(b + 2); ct.Add(b); ct.Add(b + 2); ct.Add(b + 3);
            }
            crownMesh = new Mesh { name = "Tree crown " + seed };
            crownMesh.SetVertices(cv); crownMesh.SetNormals(cn); crownMesh.SetUVs(0, cuv); crownMesh.SetColors(cc); crownMesh.SetTriangles(ct, 0);
            crownMesh.RecalculateBounds();
        }

        /// <summary>A tapered tube from a to b (bark UVs in metres: u round the girth, v along the length).</summary>
        static void Tube(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t, Vector3 a, Vector3 b, float ra, float rb, int sides, float vOffset)
        {
            var axis = (b - a);
            float len = axis.magnitude;
            if (len < 1e-4f) return;
            axis /= len;
            var side = Vector3.Cross(axis, Mathf.Abs(axis.y) > 0.95f ? Vector3.right : Vector3.up).normalized;
            var fwd = Vector3.Cross(side, axis);
            int start = v.Count;
            int rings = Mathf.Max(2, Mathf.CeilToInt(len / 0.8f) + 1);
            float circ = Mathf.PI * (ra + rb);
            for (int ri = 0; ri < rings; ri++)
            {
                float k = ri / (float)(rings - 1);
                float r = Mathf.Lerp(ra, rb, k);
                var c = a + axis * len * k;
                for (int s = 0; s <= sides; s++)
                {
                    float ang = s / (float)sides * Mathf.PI * 2f;
                    var dir = side * Mathf.Cos(ang) + fwd * Mathf.Sin(ang);
                    v.Add(c + dir * r);
                    n.Add(dir);
                    uv.Add(new Vector2(s / (float)sides * circ, vOffset + len * k));
                }
            }
            int row = sides + 1;
            for (int ri = 0; ri < rings - 1; ri++)
                for (int s = 0; s < sides; s++)
                {
                    int i0 = start + ri * row + s, i1 = i0 + 1, i2 = i0 + row, i3 = i2 + 1;
                    t.Add(i0); t.Add(i2); t.Add(i1);
                    t.Add(i1); t.Add(i2); t.Add(i3);
                }
        }
    }
}
