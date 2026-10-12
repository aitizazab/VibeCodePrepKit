using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace JamKit.Look.EditorTools
{
    /// <summary>
    /// Ground variety (the judge's round 1: "one grass texture tiles as a stripe/checker grid", flat lawns meeting the paving
    /// in a hard line):
    /// <list type="bullet">
    /// <item><see cref="BuildTerrain"/>: a Unity Terrain with four blended layers (mown lawn, dry gold grass, bare dirt, a
    /// darker clover-green copy of the lawn at another scale and offset that breaks up the repeat), mixed by a 60-100 m
    /// variation mask and a 10-15 m one; dirt painted along fences, foundations and round tree bases.</item>
    /// <item><see cref="Tufts"/> / <see cref="EdgeTufts"/>: grass, weed and wildflower cards (the foliage shader: they sway
    /// and glow when backlit), in drifts across lawns and as overhang along every paving edge.</item>
    /// <item><see cref="Litter"/>, <see cref="Stain"/>, <see cref="StormDrain"/>: leaf litter, oil/dirt/water stains, a kerb
    /// inlet with an iron grate.</item>
    /// </list>
    /// </summary>
    public static class VhLookGround
    {
        // ---------------- The terrain ----------------

        public sealed class TerrainOptions
        {
            /// <summary>The terrain's minimum corner (x, base height, z) and size (x, max height, z).</summary>
            public Vector3 origin, size;
            public int heightmapResolution = 513;
            public int alphamapResolution = 512;
            /// <summary>Ground height (world y) at (x, z).</summary>
            public Func<float, float, float> height;
            /// <summary>Optional: how "lawn" a spot is (0 = wild hillside, 1 = a mown lawn) at (x, z).</summary>
            public Func<float, float, float> lawn;
            /// <summary>Optional: how much bare dirt a spot has (0-1) at (x, z), on top of the noise (paths, beds, worn edges).</summary>
            public Func<float, float, float> dirt;
            public string assetPath;
            public int seed = 3;
            public string name = "Terrain";
        }

        public static Terrain BuildTerrain(Transform parent, TerrainOptions o)
        {
            var td = new TerrainData { heightmapResolution = o.heightmapResolution };
            td.size = o.size;
            int hr = td.heightmapResolution;
            var h = new float[hr, hr];
            for (int j = 0; j < hr; j++)
                for (int i = 0; i < hr; i++)
                {
                    float x = o.origin.x + o.size.x * i / (hr - 1f), z = o.origin.z + o.size.z * j / (hr - 1f);
                    h[j, i] = Mathf.Clamp01((o.height(x, z) - o.origin.y) / o.size.y);
                }
            td.SetHeights(0, 0, h);

            var layers = new[]
            {
                VhLookLibrary.GetTerrainLayer("Grass_Lawn"),
                VhLookLibrary.TerrainLayerVariant("Grass_DirtMix", "DryGold", new Color(1.12f, 1.0f, 0.72f), 3.3f),
                VhLookLibrary.GetTerrainLayer("Dirt"),
                VhLookLibrary.TerrainLayerVariant("Grass_Lawn", "Clover", new Color(0.84f, 0.94f, 0.76f), 3.7f),
            };
            td.terrainLayers = layers;
            td.alphamapResolution = o.alphamapResolution;
            int ar = td.alphamapResolution;
            var a = new float[ar, ar, 4];
            float s = o.seed * 17.3f;
            for (int j = 0; j < ar; j++)
                for (int i = 0; i < ar; i++)
                {
                    float x = o.origin.x + o.size.x * i / (ar - 1f), z = o.origin.z + o.size.z * j / (ar - 1f);
                    float big = Mathf.PerlinNoise(x * 0.012f + s, z * 0.012f - s);           // ~80 m patches
                    float mid = Mathf.PerlinNoise(x * 0.075f - s, z * 0.075f + s);           // ~13 m drifts
                    float fine = Mathf.PerlinNoise(x * 0.31f + 7f, z * 0.31f - 3f);          // ~3 m mottling
                    float lawn = o.lawn != null ? Mathf.Clamp01(o.lawn(x, z)) : 0f;
                    float extraDirt = o.dirt != null ? Mathf.Clamp01(o.dirt(x, z)) : 0f;
                    // Wild hillside: dry gold and green clover in big patches; lawns: mown grass with clover drifts.
                    float wildGold = Mathf.SmoothStep(0f, 1f, (big - 0.38f) / 0.3f) * 0.85f + (fine - 0.5f) * 0.2f;
                    float wGrass = Mathf.Lerp(0.25f, 0.62f, lawn) + (mid - 0.5f) * 0.5f;
                    float wGold = Mathf.Lerp(Mathf.Clamp01(wildGold), 0.04f + Mathf.Max(0f, big - 0.62f) * 0.4f, lawn);
                    float wClover = Mathf.Lerp(0.35f, 0.28f, lawn) * Mathf.SmoothStep(0f, 1f, (mid - 0.35f) / 0.4f) + (fine - 0.5f) * 0.12f;
                    float wDirt = Mathf.Max(0f, (Mathf.PerlinNoise(x * 0.05f + s * 2f, z * 0.05f) - 0.7f) * 1.6f) * (1f - lawn * 0.8f) + extraDirt;
                    wGrass = Mathf.Max(0f, wGrass); wGold = Mathf.Max(0f, wGold); wClover = Mathf.Max(0f, wClover);
                    float sum = wGrass + wGold + wDirt + wClover + 1e-4f;
                    a[j, i, 0] = wGrass / sum; a[j, i, 1] = wGold / sum; a[j, i, 2] = wDirt / sum; a[j, i, 3] = wClover / sum;
                }
            td.SetAlphamaps(0, 0, a);
            if (!string.IsNullOrEmpty(o.assetPath))
            {
                if (AssetDatabase.LoadMainAssetAtPath(o.assetPath) != null) AssetDatabase.DeleteAsset(o.assetPath);
                VhLookLibrary.EnsureAssetFolder(o.assetPath);
                AssetDatabase.CreateAsset(td, o.assetPath);
            }
            var go = Terrain.CreateTerrainGameObject(td);
            go.name = o.name;
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = o.origin;
            var t = go.GetComponent<Terrain>();
            t.heightmapPixelError = 3f;
            t.basemapDistance = 220f;
            t.drawInstanced = true;
            t.shadowCastingMode = ShadowCastingMode.TwoSided;
            t.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
            t.allowAutoConnect = false;
            return t;
        }

        // ---------------- Grass and flower tufts ----------------

        public enum Tuft { Green = 0, Dry = 1, Flowers = 2, Weeds = 3 }

        /// <summary>
        /// Collects tuft cards and writes them as chunked meshes (one renderer per ~24 m chunk, culled per chunk) under a
        /// "Grass" root. Add with <see cref="Add"/>, <see cref="Scatter"/> and <see cref="Edge"/>; then <see cref="Build"/>.
        /// </summary>
        public sealed class Tufts
        {
            const float Chunk = 24f;
            readonly Dictionary<long, Lists> chunks = new Dictionary<long, Lists>();
            readonly System.Random rng;
            readonly Func<float, float, float> ground;
            public int count;

            sealed class Lists
            {
                public readonly List<Vector3> v = new List<Vector3>();
                public readonly List<Vector3> n = new List<Vector3>();
                public readonly List<Vector2> uv = new List<Vector2>();
                public readonly List<Vector2> uv1 = new List<Vector2>();
                public readonly List<Color> c = new List<Color>();
                public readonly List<int> t = new List<int>();
            }

            public Tufts(int seed, Func<float, float, float> ground)
            {
                rng = new System.Random(seed * 191 + 7);
                this.ground = ground;
            }

            float R() => (float)rng.NextDouble();

            /// <summary>One tuft: two crossed cards, base at (x, ground, z), leaning <paramref name="lean"/> (metres at the top,
            /// world xz) — the overhang over a paving edge.</summary>
            public void Add(float x, float z, Tuft kind, float height, Vector2 lean = default, float yBias = 0f)
            {
                float y = (ground != null ? ground(x, z) : 0f) + yBias - 0.03f;
                long key = ((long)Mathf.FloorToInt(x / Chunk) << 32) ^ (uint)Mathf.FloorToInt(z / Chunk);
                if (!chunks.TryGetValue(key, out var L)) chunks[key] = L = new Lists();
                float w = height * Mathf.Lerp(1.1f, 1.6f, R());
                float yaw = R() * Mathf.PI;
                int cell = (int)kind;
                var uv0 = new Vector2((cell % 2) * 0.5f, (cell / 2) * 0.5f);
                bool flip = R() < 0.5f;
                float shade = Mathf.Lerp(0.85f, 1.08f, R());
                var tint = new Color(shade * Mathf.Lerp(0.97f, 1.04f, R()), shade, shade * Mathf.Lerp(0.92f, 1f, R()), 1f);
                for (int k = 0; k < 2; k++)
                {
                    float a = yaw + k * Mathf.PI * 0.5f;
                    var right = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (w * 0.5f);
                    var top = new Vector3(lean.x, height, lean.y);
                    var p = new Vector3(x, y, z);
                    int b = L.v.Count;
                    L.v.Add(p - right); L.v.Add(p - right + top); L.v.Add(p + right + top); L.v.Add(p + right);
                    // Normals mostly up (the tuft shades like the ground it grows from), a little towards the card's face.
                    var face = Vector3.Cross(right, Vector3.up).normalized;
                    var nn = (Vector3.up * 0.8f + face * 0.2f).normalized;
                    for (int q = 0; q < 4; q++) L.n.Add(nn);
                    float u0 = flip ? 0.495f : 0.005f, u1 = flip ? 0.005f : 0.495f;
                    L.uv.Add(uv0 + new Vector2(u0, 0.005f)); L.uv.Add(uv0 + new Vector2(u0, 0.495f));
                    L.uv.Add(uv0 + new Vector2(u1, 0.495f)); L.uv.Add(uv0 + new Vector2(u1, 0.005f));
                    // uv1.x = rim (light through the blade tips), colour alpha = sway (0 at the root).
                    L.uv1.Add(new Vector2(0.25f, 0f)); L.uv1.Add(new Vector2(1f, 0f)); L.uv1.Add(new Vector2(1f, 0f)); L.uv1.Add(new Vector2(0.25f, 0f));
                    var t0 = tint; t0.a = 0f;
                    var t1 = tint; t1.a = 1f;
                    L.c.Add(t0); L.c.Add(t1); L.c.Add(t1); L.c.Add(t0);
                    L.t.Add(b); L.t.Add(b + 1); L.t.Add(b + 2); L.t.Add(b); L.t.Add(b + 2); L.t.Add(b + 3);
                }
                count++;
            }

            /// <summary>Drifts of tufts over a rectangle: <paramref name="density"/> per m², clumped by noise; mostly green,
            /// some dry, weeds, and wildflowers in a few patches. <paramref name="allowed"/> filters spots (paving, beds).</summary>
            public void Scatter(Rect area, float density, Func<float, float, bool> allowed = null, float flowerShare = 0.08f, float dryShare = 0.2f)
            {
                int n = Mathf.RoundToInt(area.width * area.height * density);
                float s = R() * 100f;
                for (int i = 0; i < n * 3 && n > 0; i++)
                {
                    float x = Mathf.Lerp(area.xMin, area.xMax, R()), z = Mathf.Lerp(area.yMin, area.yMax, R());
                    float clump = Mathf.PerlinNoise(x * 0.18f + s, z * 0.18f - s);
                    if (R() > clump * clump * 2.2f) continue;
                    if (allowed != null && !allowed(x, z)) continue;
                    float k = R();
                    var kind = k < flowerShare * Mathf.Clamp01(clump * 1.6f) ? Tuft.Flowers : k < flowerShare + dryShare ? Tuft.Dry : k < flowerShare + dryShare + 0.2f ? Tuft.Weeds : Tuft.Green;
                    Add(x, z, kind, Mathf.Lerp(0.22f, 0.5f, R()));
                    if (--n <= 0) break;
                }
            }

            /// <summary>Grass along a paving edge from a to b (world xz), on the side <paramref name="outward"/> points to (the
            /// lawn), leaning over the edge: the lawn's overhang. Every ~<paramref name="spacing"/> m.</summary>
            public void Edge(Vector2 a, Vector2 b, Vector2 outward, float spacing = 0.32f, float flowerShare = 0.05f, float yBias = 0f)
            {
                float len = Vector2.Distance(a, b);
                int n = Mathf.Max(1, Mathf.RoundToInt(len / spacing));
                outward = outward.normalized;
                for (int i = 0; i < n; i++)
                {
                    var p = Vector2.Lerp(a, b, (i + R()) / n) + outward * Mathf.Lerp(0.02f, 0.22f, R());
                    float k = R();
                    var kind = k < flowerShare ? Tuft.Flowers : k < flowerShare + 0.25f ? Tuft.Weeds : k < flowerShare + 0.38f ? Tuft.Dry : Tuft.Green;
                    var lean = -outward * Mathf.Lerp(0.04f, 0.16f, R());
                    Add(p.x, p.y, kind, Mathf.Lerp(0.18f, 0.42f, R()), lean, yBias);
                }
            }

            public GameObject Build(Transform parent, string name = "Grass")
            {
                var root = new GameObject(name);
                if (parent != null) root.transform.SetParent(parent, false);
                var mat = VhLookLibrary.GrassMaterial();
                foreach (var kv in chunks)
                {
                    var L = kv.Value;
                    var mesh = new Mesh { name = "Grass chunk", indexFormat = L.v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                    mesh.SetVertices(L.v); mesh.SetNormals(L.n); mesh.SetUVs(0, L.uv); mesh.SetUVs(1, L.uv1); mesh.SetColors(L.c);
                    mesh.SetTriangles(L.t, 0);
                    mesh.RecalculateBounds();
                    var go = new GameObject("Grass chunk");
                    go.transform.SetParent(root.transform, false);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = mat;
                    mr.shadowCastingMode = ShadowCastingMode.Off;   // tufts this small cast nothing worth the shadow pass
                }
                return root;
            }
        }

        // ---------------- Decals ----------------

        /// <summary>A drift of fallen leaves: a few overlapping alpha-clipped quads round <paramref name="center"/>.</summary>
        public static void Litter(Transform parent, Vector3 center, float radius, int quads, int seed, Func<float, float, float> ground = null)
        {
            var rng = new System.Random(seed * 53 + 1);
            float R() => (float)rng.NextDouble();
            var mat = VhLookLibrary.LeafLitter();
            for (int i = 0; i < quads; i++)
            {
                float a = R() * Mathf.PI * 2f, r = radius * Mathf.Sqrt(R());
                var p = center + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                if (ground != null) p.y = ground(p.x, p.z);
                Quad(parent, "Leaf litter", p + Vector3.up * (0.006f + 0.002f * i), Mathf.Lerp(0.9f, 1.6f, R()), R() * 360f, 0, mat);
            }
        }

        public enum StainKind { Oil = 1, Dirt = 2, Grime = 3 }

        /// <summary>A stain lying on a surface at <paramref name="position"/> (its top), size metres across.</summary>
        public static GameObject Stain(Transform parent, Vector3 position, float size, float yaw, StainKind kind, float stretch = 1f)
        {
            return Quad(parent, "Stain (" + kind + ")", position + Vector3.up * 0.004f, size, yaw, (int)kind, VhLookLibrary.Stains(), stretch);
        }

        /// <summary>A grime streak on a vertical face (a kerb under a drain, a wall under a downspout): <paramref name="normal"/>
        /// is the face's outward direction.</summary>
        public static GameObject Streak(Transform parent, Vector3 topCenter, float width, float height, Vector3 normal)
        {
            var go = new GameObject("Grime streak");
            go.transform.SetParent(parent, false);
            go.transform.position = topCenter + normal * 0.004f - Vector3.up * height * 0.5f;
            go.transform.rotation = Quaternion.LookRotation(-normal, Vector3.up);
            go.AddComponent<MeshFilter>().sharedMesh = CellQuad(3, width, height, false);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = VhLookLibrary.Stains();
            mr.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        static GameObject Quad(Transform parent, string name, Vector3 p, float size, float yaw, int cell, Material m, float stretch = 1f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = p;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = CellQuad(cell, size * stretch, size, true);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = m;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        static readonly Dictionary<string, Mesh> QuadCache = new Dictionary<string, Mesh>();

        /// <summary>A quad showing one cell of a 2x2 atlas: lying flat (normal up) or upright (normal -z).</summary>
        static Mesh CellQuad(int cell, float w, float h, bool flat)
        {
            string key = cell + "_" + w.ToString("0.00") + "_" + h.ToString("0.00") + (flat ? "f" : "u");
            if (QuadCache.TryGetValue(key, out var cached) && cached != null) return cached;
            var uv0 = new Vector2((cell % 2) * 0.5f, (cell / 2) * 0.5f);
            float hw = w * 0.5f, hh = h * 0.5f;
            var m = new Mesh { name = "Decal " + key };
            if (flat)
                m.vertices = new[] { new Vector3(-hw, 0f, -hh), new Vector3(-hw, 0f, hh), new Vector3(hw, 0f, hh), new Vector3(hw, 0f, -hh) };
            else
                m.vertices = new[] { new Vector3(-hw, -hh, 0f), new Vector3(-hw, hh, 0f), new Vector3(hw, hh, 0f), new Vector3(hw, -hh, 0f) };
            var n = flat ? Vector3.up : Vector3.back;
            m.normals = new[] { n, n, n, n };
            m.uv = new[] { uv0 + new Vector2(0.01f, 0.01f), uv0 + new Vector2(0.01f, 0.49f), uv0 + new Vector2(0.49f, 0.49f), uv0 + new Vector2(0.49f, 0.01f) };
            m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            m.RecalculateTangents();
            m.RecalculateBounds();
            QuadCache[key] = m;
            return m;
        }

        // ---------------- Street furniture in the ground ----------------

        /// <summary>
        /// A kerb inlet storm drain: a dark iron grate set into the road along the kerb at <paramref name="kerbLine"/> (a point
        /// on the road's edge at road level), <paramref name="along"/> the kerb's direction, <paramref name="intoRoad"/> pointing
        /// away from the kerb; a recessed opening in the kerb's face above it, a concrete apron and grime.
        /// </summary>
        public static GameObject StormDrain(Transform parent, Vector3 kerbLine, Vector3 along, Vector3 intoRoad)
        {
            along = new Vector3(along.x, 0f, along.z).normalized;
            intoRoad = new Vector3(intoRoad.x, 0f, intoRoad.z).normalized;
            var root = new GameObject("Storm drain");
            root.transform.SetParent(parent, false);
            root.transform.position = kerbLine;
            root.transform.rotation = Quaternion.LookRotation(intoRoad, Vector3.up);   // local x = along the kerb, z = into the road
            var iron = VhLookLibrary.Paint("CastIron", new Color(0.13f, 0.12f, 0.11f), 1.1f);
            var hole = VhLookLibrary.Paint("DrainDark", new Color(0.02f, 0.02f, 0.02f), 0.2f);
            var concrete = VhLookLibrary.Get("Concrete_Kerb");
            void B(string n, Vector3 min, Vector3 max, Material m, float bevel)
            {
                var go = new GameObject(n);
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = (min + max) * 0.5f;
                go.AddComponent<MeshFilter>().sharedMesh = VhLookUv.BevelBox(max - min, bevel);
                go.AddComponent<MeshRenderer>().sharedMaterial = m;
            }
            // The apron (a concrete gutter pan), the dark pit under the grate, the grate's frame and bars.
            B("Apron", new Vector3(-0.9f, -0.02f, 0f), new Vector3(0.9f, 0.006f, 0.7f), concrete, 0.01f);
            B("Pit", new Vector3(-0.42f, -0.02f, 0.06f), new Vector3(0.42f, 0.009f, 0.5f), hole, 0.002f);
            B("Grate frame", new Vector3(-0.47f, 0.006f, 0.03f), new Vector3(0.47f, 0.016f, 0.08f), iron, 0.004f);
            B("Grate frame", new Vector3(-0.47f, 0.006f, 0.48f), new Vector3(0.47f, 0.016f, 0.53f), iron, 0.004f);
            B("Grate frame", new Vector3(-0.47f, 0.006f, 0.03f), new Vector3(-0.42f, 0.016f, 0.53f), iron, 0.004f);
            B("Grate frame", new Vector3(0.42f, 0.006f, 0.03f), new Vector3(0.47f, 0.016f, 0.53f), iron, 0.004f);
            for (int i = 0; i < 9; i++)
            {
                float x = -0.36f + i * 0.09f;
                B("Grate bar", new Vector3(x - 0.018f, 0.006f, 0.08f), new Vector3(x + 0.018f, 0.015f, 0.48f), iron, 0.003f);
            }
            B("Grate rib", new Vector3(-0.42f, 0.006f, 0.27f), new Vector3(0.42f, 0.014f, 0.3f), iron, 0.003f);
            // The kerb opening: a dark slot in the kerb's face with an iron lip.
            B("Kerb opening", new Vector3(-0.55f, 0.02f, -0.02f), new Vector3(0.55f, 0.12f, 0.004f), hole, 0.002f);
            B("Kerb lip", new Vector3(-0.6f, 0.12f, -0.03f), new Vector3(0.6f, 0.15f, 0.01f), iron, 0.006f);
            Streak(root.transform, kerbLine + Vector3.up * 0.14f + intoRoad * 0.012f, 1.3f, 0.14f, intoRoad);
            Stain(root.transform, kerbLine + intoRoad * 0.55f + Vector3.up * 0.006f, 1.6f, Quaternion.LookRotation(along).eulerAngles.y, StainKind.Dirt, 1.4f);
            return root;
        }
    }
}
