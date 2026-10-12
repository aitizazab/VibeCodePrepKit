using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace JamKit.Look.EditorTools
{
    /// <summary>
    /// Stylized trees for the hill and the yards (the kind over a 1990s suburban street), built in code from the library's
    /// bark and the painted leaf-clump atlas (tools/look/make-foliage-atlas.py) with the JamKit/Look/Foliage shader.
    /// <para><b>Pass 3</b> (judge round 2: autumn colour, "twig spikes out of crowns", "loose floating leaf cards", "pale
    /// seam rings at limb joints", "straight cylinder limbs forking from one point", "birch trunks read as bright yellow
    /// sticks with tiny crowns", and ~580 alpha cards per near crown):</para>
    /// <list type="bullet">
    /// <item>Wood is one swept tube per trunk, limb and branch (parallel-transported rings, continuous bark UVs, no joints
    /// along it), tapering and bending along a curve; limbs leave the trunk at staggered heights up a leader (a vase of
    /// limbs from 3 m for the elm), their bases sunk inside the trunk.</item>
    /// <item>Every leaf clump sits on a branch tip, and every branch tip ends inside its clump: nothing pokes out, nothing
    /// floats. The tips are kept inside the species' crown outline.</item>
    /// <item>Crowns are fewer, larger, well-shaped clumps: each clump is 5-6 cards (a dense painted clump of leaves each)
    /// facing outward round its centre. A near maple has ~110 cards (pass 2: ~580), about 30% less card area in all, and
    /// the cards are drawn outside-in (the crown's shell first), so most of the inner ones fail the depth test.</item>
    /// <item>Birch: chalk-white bark (its own texture), a wider, fuller crown reaching lower.</item>
    /// </list>
    /// <para><b>Levels of detail</b> (cross-faded, thresholds in <see cref="Lods"/>): LOD0 full (clumps of 5-6 cards, trunk,
    /// limbs, branches, shadows); LOD1 (2 bigger cards a clump, the same wood; shadows); LOD2 (one big card for every other
    /// clump, a 5-sided trunk and limbs, no shadows); LOD3 a crossed billboard of the painted whole tree (no shadows);
    /// culled beyond. Two SRP Batcher-compatible, instancing-ready materials (bark, foliage).</para>
    /// <code>
    /// VhLookTrees.Place(parent, position, VhLookTrees.Species.Oak, variant: 1, height: 11f);
    /// VhLookTrees.Place(parent, position, seed: 3, height: 9f);                         // pass 1's call still works
    /// VhLookTrees.Cluster(parent, center, radius: 14f, count: 7, seed: 5, ground: HeightAt);   // a stand, not a grid
    /// VhLookTrees.Scatter(parent, min, max, clusters: 30, seed: 2, ground: HeightAt, allowed: p => !OnRoad(p));
    /// </code>
    /// </summary>
    public static class VhLookTrees
    {
        public const string Dir = VhLookLibrary.MatRoot + "Trees/";
        /// <summary>Bumped whenever the generator changes, so cached meshes from an older generator are never reused.</summary>
        const int GeneratorVersion = 5;
        public const int Variants = 3;

        public enum Species { Maple, Oak, Birch, Elm, Shrub }

        /// <summary>
        /// LOD switch points as screen-relative heights. For a 10 m tree at 1080p, 60 degree FOV and LOD bias 1: LOD1 from
        /// ~70 m, LOD2 from ~190 m, the billboard from ~460 m, culled past ~2.2 km. The Perf agent tunes these: change them,
        /// rebuild the scene (or call <see cref="RetuneLods"/> on an open scene).
        /// </summary>
        public static class Lods
        {
            public static float Lod1 = 0.12f, Lod2 = 0.045f, Lod3 = 0.019f, Cull = 0.004f;
            /// <summary>Shrubs: LOD1 and LOD2 points and the cull (they have no billboard).</summary>
            public static float ShrubLod1 = 0.06f, ShrubLod2 = 0.02f, ShrubCull = 0.006f;
        }

        /// <summary>Re-applies <see cref="Lods"/> to every tree's LODGroup under <paramref name="root"/>. Returns the number of
        /// trees changed.</summary>
        public static int RetuneLods(GameObject root)
        {
            int n = 0;
            foreach (var g in root.GetComponentsInChildren<LODGroup>(true))
            {
                var lods = g.GetLODs();
                if (lods.Length == 4) { lods[0].screenRelativeTransitionHeight = Lods.Lod1; lods[1].screenRelativeTransitionHeight = Lods.Lod2; lods[2].screenRelativeTransitionHeight = Lods.Lod3; lods[3].screenRelativeTransitionHeight = Lods.Cull; }
                else if (lods.Length == 3 && g.name.StartsWith("Shrub", StringComparison.Ordinal)) { lods[0].screenRelativeTransitionHeight = Lods.ShrubLod1; lods[1].screenRelativeTransitionHeight = Lods.ShrubLod2; lods[2].screenRelativeTransitionHeight = Lods.ShrubCull; }
                else continue;
                g.SetLODs(lods);
                n++;
            }
            return n;
        }

        struct Spec
        {
            public float refHeight, fork, crownR, crownRy, crownY, clumpR, trunkR, leaderTop, limbLift, limbStartTop;
            public int limbs, branches, cardsPerClump, cell;
            public bool vase;
        }

        static Spec SpecOf(Species s)
        {
            switch (s)
            {
                case Species.Oak:
                    // Broad and spreading: low, near-horizontal limbs off a short leader; big clumps with sky between.
                    return new Spec { refHeight = 10f, fork = 2.2f, crownR = 4.8f, crownRy = 2.9f, crownY = 6.5f, clumpR = 1.6f, trunkR = 0.42f,
                        leaderTop = 5.2f, limbStartTop = 4.6f, limbLift = 0.2f, limbs = 6, branches = 3, cardsPerClump = 5, cell = 1 };
                case Species.Birch:
                    // A pale leader running up through a full oval crown that reaches down to ~3 m.
                    return new Spec { refHeight = 10f, fork = 2.5f, crownR = 3.3f, crownRy = 3.8f, crownY = 6.0f, clumpR = 1.4f, trunkR = 0.2f,
                        leaderTop = 8.4f, limbStartTop = 7.6f, limbLift = 0.55f, limbs = 8, branches = 2, cardsPerClump = 5, cell = 2 };
                case Species.Elm:
                    // A vase: limbs fanning up from 3 m into a wide, flat-topped crown (the street tree).
                    return new Spec { refHeight = 10f, fork = 3.0f, crownR = 4.4f, crownRy = 2.8f, crownY = 7.0f, clumpR = 1.5f, trunkR = 0.36f,
                        leaderTop = 4.3f, limbStartTop = 4.0f, limbLift = 1.0f, limbs = 5, branches = 3, cardsPerClump = 5, cell = 0, vase = true };
                case Species.Shrub:
                    return new Spec { refHeight = 1.6f, fork = 0.05f, crownR = 1.0f, crownRy = 0.75f, crownY = 0.8f, clumpR = 0.5f, trunkR = 0.045f,
                        leaderTop = 0.4f, limbStartTop = 0.2f, limbLift = 0.7f, limbs = 5, branches = 0, cardsPerClump = 4, cell = 3 };
                default:
                    // Maple: a round full crown on a short trunk (the lawn tree).
                    return new Spec { refHeight = 10f, fork = 2.5f, crownR = 3.9f, crownRy = 3.3f, crownY = 6.3f, clumpR = 1.4f, trunkR = 0.36f,
                        leaderTop = 7.4f, limbStartTop = 6.0f, limbLift = 0.45f, limbs = 6, branches = 3, cardsPerClump = 5, cell = 0 };
            }
        }

        public static float ReferenceHeight(Species s) => SpecOf(s).refHeight;

        // ---------------- Placing ----------------

        /// <summary>Pass 1's call: a tree with its base at <paramref name="position"/>. The seed picks the species and variant
        /// (heights under 3.5 m are shrubs).</summary>
        public static GameObject Place(Transform parent, Vector3 position, int seed = 1, float height = 9f, bool dryCrown = false, float yaw = -1f)
        {
            var species = height < 3.5f ? Species.Shrub : (Species)(Mathf.Abs(seed) % 4);
            return Place(parent, position, species, Mathf.Abs(seed / 4) % Variants, height, dryCrown, yaw >= 0f ? yaw : (seed * 47f) % 360f);
        }

        /// <summary>A tree of the given species and variant (0-2), scaled to <paramref name="height"/> metres, base at
        /// <paramref name="position"/>. A capsule collider on the trunk (a box round a shrub).</summary>
        public static GameObject Place(Transform parent, Vector3 position, Species species, int variant, float height, bool dryCrown = false, float yaw = -1f)
        {
            variant = Mathf.Abs(variant) % Variants;
            var set = Get(species, variant);
            var spec = SpecOf(species);
            float scale = height / spec.refHeight;
            var go = new GameObject(species + " " + variant + " (" + height.ToString("0.#") + " m)");
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0f, yaw >= 0f ? yaw : Mathf.Repeat(variant * 131f + position.x * 7.3f + position.z * 3.1f, 360f), 0f);
            go.transform.localScale = Vector3.one * scale;

            var bark = species == Species.Birch ? VhLookLibrary.BirchBark() : VhLookLibrary.Get("Tree_Bark");
            var leaves = VhLookLibrary.FoliageMaterial(dryCrown);
            var trunk = Part(go.transform, "Trunk", set.trunk, bark);
            var trunkFar = Part(go.transform, "Trunk (far)", set.trunkLow, bark);
            var c0 = Part(go.transform, "Crown", set.crown0, leaves);
            var c1 = Part(go.transform, "Crown (LOD1)", set.crown1, leaves);
            var c2 = Part(go.transform, "Crown (LOD2)", set.crown2, leaves);
            trunkFar.shadowCastingMode = ShadowCastingMode.Off;
            c2.shadowCastingMode = ShadowCastingMode.Off;
            MeshRenderer board = null;
            if (species != Species.Shrub)
            {
                board = Part(go.transform, "Billboard (LOD3)", Billboard(species), VhLookLibrary.TreeBillboardMaterial(dryCrown));
                board.shadowCastingMode = ShadowCastingMode.Off;
                var cap = trunk.gameObject.AddComponent<CapsuleCollider>();
                cap.radius = spec.trunkR * 1.05f;
                cap.height = spec.fork + 0.6f;
                cap.center = new Vector3(0f, (spec.fork + 0.6f) * 0.5f - 0.2f, 0f);
            }
            else
            {
                var box = c0.gameObject.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, spec.crownY * 0.9f, 0f);
                box.size = new Vector3(spec.crownR * 1.5f, spec.crownY * 1.7f, spec.crownR * 1.5f);
            }
            var lod = go.AddComponent<LODGroup>();
            lod.fadeMode = LODFadeMode.CrossFade;
            lod.animateCrossFading = true;
            if (board != null)
                lod.SetLODs(new[]
                {
                    new LOD(Lods.Lod1, new Renderer[] { trunk, c0 }),
                    new LOD(Lods.Lod2, new Renderer[] { trunk, c1 }),
                    new LOD(Lods.Lod3, new Renderer[] { trunkFar, c2 }),
                    new LOD(Lods.Cull, new Renderer[] { board }),
                });
            else
                lod.SetLODs(new[]
                {
                    new LOD(Lods.ShrubLod1, new Renderer[] { trunk, c0 }),
                    new LOD(Lods.ShrubLod2, new Renderer[] { trunk, c1 }),
                    new LOD(Lods.ShrubCull, new Renderer[] { c2 }),
                });
            lod.RecalculateBounds();
            return go;
        }

        static MeshRenderer Part(Transform parent, string name, Mesh mesh, Material m)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = m;
            return r;
        }

        /// <summary>
        /// A stand of trees round <paramref name="center"/>: denser in the middle, a dominant species with others mixed in,
        /// heights ±30% round <paramref name="meanHeight"/>, a few shrubs at the edge. <paramref name="ground"/> gives the
        /// ground height at (x, z) (null = center.y); trees are sunk 0.2 m into it.
        /// </summary>
        public static List<GameObject> Cluster(Transform parent, Vector3 center, float radius, int count, int seed, float meanHeight = 10f,
            Func<float, float, float> ground = null, Species[] mix = null, float minSpacing = 3.2f, Func<Vector3, bool> allowed = null, bool shrubs = true)
        {
            var rng = new System.Random(seed * 7349 + 11);
            float R() => (float)rng.NextDouble();
            mix = mix ?? new[] { Species.Maple, Species.Oak, Species.Elm, Species.Birch };
            var dominant = mix[rng.Next(mix.Length)];
            var placed = new List<Vector3>();
            var result = new List<GameObject>();
            int tries = 0;
            while (placed.Count < count && tries++ < count * 30)
            {
                float a = R() * Mathf.PI * 2f, r = radius * Mathf.Sqrt(-2f * Mathf.Log(Mathf.Max(1e-4f, R()))) * 0.45f;
                if (r > radius * 1.25f) continue;
                var p = new Vector3(center.x + Mathf.Cos(a) * r, 0f, center.z + Mathf.Sin(a) * r);
                bool clear = true;
                foreach (var q in placed)
                {
                    var dq = new Vector2(q.x - p.x, q.z - p.z);
                    if (dq.sqrMagnitude < minSpacing * minSpacing) { clear = false; break; }
                }
                if (!clear) continue;
                p.y = ground != null ? ground(p.x, p.z) - 0.2f : center.y;
                if (allowed != null && !allowed(p)) continue;
                placed.Add(p);
                var sp = R() < 0.6f ? dominant : mix[rng.Next(mix.Length)];
                float h = meanHeight * Mathf.Lerp(0.7f, 1.3f, R()) * (sp == Species.Birch ? 1.1f : 1f);
                result.Add(Place(parent, p, sp, rng.Next(Variants), h, R() < 0.12f, R() * 360f));
            }
            if (shrubs)
            {
                int n = Mathf.Max(1, count / 2);
                for (int i = 0; i < n; i++)
                {
                    float a = R() * Mathf.PI * 2f, r = radius * Mathf.Lerp(0.6f, 1.1f, R());
                    var p = new Vector3(center.x + Mathf.Cos(a) * r, 0f, center.z + Mathf.Sin(a) * r);
                    p.y = ground != null ? ground(p.x, p.z) - 0.1f : center.y;
                    if (allowed != null && !allowed(p)) continue;
                    result.Add(Place(parent, p, Species.Shrub, rng.Next(Variants), Mathf.Lerp(1.2f, 2.4f, R()), false, R() * 360f));
                }
            }
            return result;
        }

        /// <summary>
        /// Stands of trees over a rectangle (x/z from <paramref name="min"/> to <paramref name="max"/>): clusters of 3-9 with
        /// open ground between them, plus a few lone trees. <paramref name="allowed"/> filters positions (roads, lots).
        /// Returns the number of trees placed.
        /// </summary>
        public static int Scatter(Transform parent, Vector2 min, Vector2 max, int clusters, int seed, Func<float, float, float> ground,
            Func<Vector3, bool> allowed = null, float meanHeight = 10f, Species[] mix = null)
        {
            var rng = new System.Random(seed * 3571 + 5);
            float R() => (float)rng.NextDouble();
            int n = 0;
            for (int c = 0; c < clusters; c++)
            {
                var centre = new Vector3(Mathf.Lerp(min.x, max.x, R()), 0f, Mathf.Lerp(min.y, max.y, R()));
                int count = 3 + rng.Next(7);
                n += Cluster(parent, centre, 6f + count * 1.6f, count, rng.Next(), meanHeight, ground, mix, 3.4f, allowed, R() < 0.6f).Count;
            }
            int lone = Mathf.Max(1, clusters / 3);
            mix = mix ?? new[] { Species.Maple, Species.Oak, Species.Elm, Species.Birch };
            for (int i = 0; i < lone; i++)
            {
                var p = new Vector3(Mathf.Lerp(min.x, max.x, R()), 0f, Mathf.Lerp(min.y, max.y, R()));
                p.y = ground != null ? ground(p.x, p.z) - 0.2f : 0f;
                if (allowed != null && !allowed(p)) continue;
                Place(parent, p, mix[rng.Next(mix.Length)], rng.Next(Variants), meanHeight * Mathf.Lerp(0.8f, 1.3f, R()), false, R() * 360f);
                n++;
            }
            return n;
        }

        // ---------------- Meshes ----------------

        static readonly Dictionary<Species, Mesh> Boards = new Dictionary<Species, Mesh>();

        /// <summary>
        /// The impostor: two crossed upright quads showing the species' painted whole tree (a cell of VH_TreeBillboards, 10 m
        /// square at the reference height, ground at the bottom). Normals lean outward from the trunk's axis and upward, so
        /// the flat cards shade like a round crown; rim 0.8 so the backlit glow reads; no sway at the base.
        /// </summary>
        public static Mesh Billboard(Species species)
        {
            if (Boards.TryGetValue(species, out var cached) && cached != null) return cached;
            string path = Dir + "VH_TreeBoard" + GeneratorVersion + "_" + species + ".asset";
            var m = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (m == null)
            {
                int cell = (int)species;
                var uv0 = new Vector2((cell % 2) * 0.5f, (cell / 2) * 0.5f);
                var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var uv1 = new List<Vector2>();
                var c = new List<Color>(); var t = new List<int>();
                const float half = 5f, bottom = -0.25f, top = 9.75f;
                for (int k = 0; k < 2; k++)
                {
                    float a = k * Mathf.PI * 0.5f + 0.4f;
                    var right = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    var face = Vector3.Cross(Vector3.up, right);
                    int b = v.Count;
                    v.Add(-right * half + Vector3.up * bottom); v.Add(-right * half + Vector3.up * top);
                    v.Add(right * half + Vector3.up * top); v.Add(right * half + Vector3.up * bottom);
                    n.Add((-right * 0.7f + face * 0.2f + Vector3.up * 0.3f).normalized); n.Add((-right * 0.6f + face * 0.2f + Vector3.up * 0.7f).normalized);
                    n.Add((right * 0.6f + face * 0.2f + Vector3.up * 0.7f).normalized); n.Add((right * 0.7f + face * 0.2f + Vector3.up * 0.3f).normalized);
                    uv.Add(uv0 + new Vector2(0.005f, 0.005f)); uv.Add(uv0 + new Vector2(0.005f, 0.495f));
                    uv.Add(uv0 + new Vector2(0.495f, 0.495f)); uv.Add(uv0 + new Vector2(0.495f, 0.005f));
                    for (int q = 0; q < 4; q++) uv1.Add(new Vector2(0.8f, 0f));
                    c.Add(new Color(1f, 1f, 1f, 0f)); c.Add(new Color(1f, 1f, 1f, 0.6f)); c.Add(new Color(1f, 1f, 1f, 0.6f)); c.Add(new Color(1f, 1f, 1f, 0f));
                    t.Add(b); t.Add(b + 1); t.Add(b + 2); t.Add(b); t.Add(b + 2); t.Add(b + 3);
                }
                m = new Mesh { name = "Tree billboard " + species };
                m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetUVs(1, uv1); m.SetColors(c); m.SetTriangles(t, 0);
                m.RecalculateBounds();
                VhLookLibrary.EnsureAssetFolder(path);
                AssetDatabase.CreateAsset(m, path);
            }
            Boards[species] = m;
            return m;
        }

        public sealed class MeshSet
        {
            public Mesh trunk, trunkLow, crown0, crown1, crown2;
            /// <summary>Cards per crown level and the LOD0 crown's total card area (m², at the reference height).</summary>
            public int cards0, cards1, cards2;
            public float area0;
        }

        static readonly Dictionary<string, MeshSet> Cache = new Dictionary<string, MeshSet>();

        /// <summary>The meshes for a species' variant (built and saved on first use).</summary>
        public static MeshSet Get(Species species, int variant)
        {
            string key = "VH_Tree" + GeneratorVersion + "_" + species + "_" + variant;
            string path = Dir + key + ".asset";
            if (Cache.TryGetValue(key, out var cached) && cached.trunk != null) return cached;
            var set = new MeshSet();
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (!(o is Mesh m)) continue;
                if (m.name.EndsWith(" trunk", StringComparison.Ordinal)) set.trunk = m;
                else if (m.name.EndsWith(" trunk far", StringComparison.Ordinal)) set.trunkLow = m;
                else if (m.name.EndsWith(" crown0", StringComparison.Ordinal)) set.crown0 = m;
                else if (m.name.EndsWith(" crown1", StringComparison.Ordinal)) set.crown1 = m;
                else if (m.name.EndsWith(" crown2", StringComparison.Ordinal)) set.crown2 = m;
            }
            if (set.trunk == null || set.trunkLow == null || set.crown0 == null || set.crown1 == null || set.crown2 == null)
            {
                if (AssetDatabase.LoadMainAssetAtPath(path) != null) AssetDatabase.DeleteAsset(path);
                PurgeOlderGenerators();
                set = Build(species, variant, key);
                VhLookLibrary.EnsureAssetFolder(path);
                AssetDatabase.CreateAsset(set.trunk, path);
                AssetDatabase.AddObjectToAsset(set.trunkLow, path);
                AssetDatabase.AddObjectToAsset(set.crown0, path);
                AssetDatabase.AddObjectToAsset(set.crown1, path);
                AssetDatabase.AddObjectToAsset(set.crown2, path);
                AssetDatabase.SaveAssets();
                Debug.Log("JAMKIT_LOOK tree " + species + " " + variant + ": LOD0 " + set.cards0 + " cards (" + set.area0.ToString("0") +
                          " m2 of card at 10 m), LOD1 " + set.cards1 + ", LOD2 " + set.cards2 + "; " + set.trunk.vertexCount + " wood vertices");
            }
            else
            {
                set.cards0 = set.crown0.vertexCount / 4; set.cards1 = set.crown1.vertexCount / 4; set.cards2 = set.crown2.vertexCount / 4;
            }
            Cache[key] = set;
            return set;
        }

        static bool _purged;

        /// <summary>Deletes cached tree meshes from older generators (they'd sit in the library unused).</summary>
        static void PurgeOlderGenerators()
        {
            if (_purged) return;
            _purged = true;
            string folder = Dir.TrimEnd('/');
            if (!AssetDatabase.IsValidFolder(folder)) return;
            foreach (var guid in AssetDatabase.FindAssets("t:Mesh", new[] { folder }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                string file = System.IO.Path.GetFileNameWithoutExtension(p);
                for (int v = 1; v < GeneratorVersion; v++)
                    if (file.StartsWith("VH_Tree" + v + "_", StringComparison.Ordinal) || file.StartsWith("VH_TreeBoard" + v + "_", StringComparison.Ordinal))
                    {
                        AssetDatabase.DeleteAsset(p);
                        break;
                    }
            }
        }

        struct Clump
        {
            public Vector3 c;
            public float r;
        }

        sealed class Wood
        {
            public readonly List<Vector3> v = new List<Vector3>();
            public readonly List<Vector3> n = new List<Vector3>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<int> t = new List<int>();
        }

        static MeshSet Build(Species species, int variant, string key)
        {
            var spec = SpecOf(species);
            var rng = new System.Random((int)species * 1009 + variant * 7919 + 17);
            float R() => (float)rng.NextDouble();
            float H = spec.refHeight;
            var near = new Wood();
            var far = new Wood();
            var crownC = new Vector3((R() - 0.5f) * 0.12f * spec.crownR, spec.crownY, (R() - 0.5f) * 0.12f * spec.crownR);
            var clumps = new List<Clump>();
            bool shrub = species == Species.Shrub;

            // Keeps a point inside the crown's ellipsoid, shrunk by a margin (a clump's radius): no clump outside the outline.
            Vector3 Inside(Vector3 p, float margin)
            {
                var d = p - crownC;
                float ax = Mathf.Max(0.15f, spec.crownR - margin), ay = Mathf.Max(0.15f, spec.crownRy - margin * 0.7f);
                float e = Mathf.Sqrt((d.x * d.x + d.z * d.z) / (ax * ax) + d.y * d.y / (ay * ay));
                return e > 1f ? crownC + d / e : p;
            }

            // ---- The trunk: one swept tube from below the ground, through a bend and the fork, up the leader ----
            var lean = new Vector3((R() - 0.5f) * 0.12f, 1f, (R() - 0.5f) * 0.12f).normalized;
            var trunkPts = new List<Vector3>();
            var trunkR = new List<float>();
            float r0 = spec.trunkR;
            if (shrub)
            {
                trunkPts.Add(Vector3.down * 0.1f); trunkPts.Add(Vector3.up * spec.leaderTop);
                trunkR.Add(r0); trunkR.Add(r0 * 0.6f);
            }
            else
            {
                float top = spec.leaderTop;
                var wob = new Vector3((R() - 0.5f) * 0.3f, 0f, (R() - 0.5f) * 0.3f);
                int steps = Mathf.Max(4, Mathf.CeilToInt(top / 0.9f));
                for (int i = 0; i <= steps; i++)
                {
                    float k = i / (float)steps;
                    float y = Mathf.Lerp(-0.3f, top, k);
                    // A gentle S along the height (a straight cylinder reads as a pole).
                    var p = lean * y + wob * Mathf.Sin(k * Mathf.PI * 1.3f) * (top / 6f);
                    p.y = y;
                    trunkPts.Add(p);
                    // Root flare at the base, a steady taper up the trunk, thinning faster above the fork (the limbs took
                    // their share).
                    float flare = 1f + 0.55f * Mathf.Exp(-Mathf.Max(0f, y + 0.3f) / 0.45f);
                    float above = Mathf.Max(0f, y - spec.fork) / Mathf.Max(0.5f, top - spec.fork);
                    float taper = y < spec.fork ? Mathf.Lerp(1f, 0.85f, (y + 0.3f) / (spec.fork + 0.3f)) : Mathf.Lerp(0.85f, spec.vase ? 0.55f : 0.18f, above);
                    trunkR.Add(r0 * flare * taper);
                }
            }
            Sweep(near, trunkPts, trunkR, shrub ? 5 : 12, 0f);
            Sweep(far, trunkPts, trunkR, 5, 0f);
            if (!shrub)
            {
                // Root buttresses: short flares out along the ground, sunk into the trunk's base.
                int roots = 4 + rng.Next(2);
                for (int k = 0; k < roots; k++)
                {
                    float a = (k + R() * 0.5f) / roots * Mathf.PI * 2f;
                    var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    var pts = new List<Vector3> { Vector3.up * 0.4f + d * r0 * 0.3f, Vector3.up * 0.12f + d * r0 * 1.2f, d * r0 * (2f + R() * 0.6f) + Vector3.down * 0.14f };
                    Sweep(near, pts, new List<float> { r0 * 0.5f, r0 * 0.32f, r0 * 0.1f }, 6, 0f);
                }
            }

            float TrunkRadiusAt(float y)
            {
                for (int i = 0; i < trunkPts.Count - 1; i++)
                    if (y <= trunkPts[i + 1].y)
                        return Mathf.Lerp(trunkR[i], trunkR[i + 1], Mathf.InverseLerp(trunkPts[i].y, trunkPts[i + 1].y, y));
                return trunkR[trunkR.Count - 1];
            }

            Vector3 TrunkAt(float y)
            {
                for (int i = 0; i < trunkPts.Count - 1; i++)
                    if (y <= trunkPts[i + 1].y)
                        return Vector3.Lerp(trunkPts[i], trunkPts[i + 1], Mathf.InverseLerp(trunkPts[i].y, trunkPts[i + 1].y, y));
                return trunkPts[trunkPts.Count - 1];
            }

            // ---- Limbs: from staggered heights up the trunk, curving out to tips inside the crown ----
            float golden = Mathf.PI * (3f - Mathf.Sqrt(5f));
            var limbs = new List<List<Vector3>>();
            float az0 = R() * Mathf.PI * 2f;
            for (int i = 0; i < spec.limbs; i++)
            {
                float az = az0 + i * golden + (R() - 0.5f) * 0.5f;
                var dirH = new Vector3(Mathf.Cos(az), 0f, Mathf.Sin(az));
                float hk = spec.limbs > 1 ? i / (float)(spec.limbs - 1) : 0.5f;
                float sy = Mathf.Lerp(spec.fork, spec.limbStartTop, hk) + (R() - 0.5f) * 0.3f;   // staggered up the trunk, never one fork point
                sy = Mathf.Clamp(sy, spec.fork - 0.2f, spec.leaderTop - 0.2f);
                float baseR = TrunkRadiusAt(sy);
                var start = TrunkAt(sy) + dirH * baseR * 0.25f;   // sunk inside the trunk: no open ring at the joint
                // The tip: out towards the crown's edge at this azimuth, its height spread over the crown (low limbs droop
                // to the crown's underside, high ones reach the top), kept a clump's radius inside the outline.
                float reach = spec.crownR * Mathf.Lerp(0.55f, 0.85f, R());
                float tipY = shrub ? crownC.y + spec.crownRy * Mathf.Lerp(-0.2f, 0.5f, R())
                    : crownC.y + spec.crownRy * Mathf.Lerp(-0.55f, 0.55f, Mathf.Clamp01(hk * 0.8f + R() * 0.35f + (spec.vase ? 0.2f : 0f)));
                var tip = Inside(new Vector3(crownC.x + dirH.x * reach, tipY, crownC.z + dirH.z * reach), spec.clumpR * 0.75f);
                float limbR = shrub ? r0 * 0.7f : Mathf.Min(baseR * 0.62f, r0 * (spec.vase ? 0.6f : 0.5f)) * Mathf.Lerp(0.85f, 1.1f, R());
                var limb = Curve(start, tip, (dirH * (1f - spec.limbLift) + Vector3.up * (0.25f + spec.limbLift)).normalized, rng, 6);
                limbs.Add(limb);
                var radii = Taper(limb, limbR, limbR * 0.22f);
                Sweep(near, limb, radii, shrub ? 4 : 8, sy);
                if (!shrub) Sweep(far, Thin(limb), Thin(radii), 4, sy);
                AddClump(clumps, tip + Vector3.up * spec.clumpR * 0.15f, spec.clumpR * Mathf.Lerp(0.9f, 1.15f, R()));
                // Branches off the limb's outer half, each ending inside its own clump.
                for (int k = 0; k < spec.branches; k++)
                {
                    float bt = Mathf.Lerp(0.4f, 0.8f, (k + R()) / Mathf.Max(1, spec.branches));
                    int bi = Mathf.Clamp(Mathf.RoundToInt(bt * (limb.Count - 1)), 1, limb.Count - 2);
                    var b0 = limb[bi];
                    var along = (limb[bi + 1] - limb[bi - 1]).normalized;
                    var side = Vector3.Cross(along, Vector3.up);
                    if (side.sqrMagnitude < 1e-3f) side = Vector3.right;
                    side = side.normalized * (R() < 0.5f ? -1f : 1f);
                    var bdir = (along * 0.45f + side * 0.75f + Vector3.up * Mathf.Lerp(0.15f, 0.6f, R())).normalized;
                    float blen = spec.crownR * Mathf.Lerp(0.28f, 0.42f, R());
                    var btip = Inside(b0 + bdir * blen, spec.clumpR * 0.8f);
                    if ((btip - b0).magnitude < spec.clumpR * 0.5f) continue;
                    float br = radii[bi] * 0.5f;
                    var branch = Curve(b0 - bdir * br, btip, bdir, rng, 4);
                    Sweep(near, branch, Taper(branch, br, br * 0.2f), 5, 0f);
                    float cr = spec.clumpR * Mathf.Lerp(0.78f, 1.0f, R());
                    // The branch ends a third of the way into its clump (no tip poking out).
                    AddClump(clumps, btip + bdir * cr * 0.3f, cr);
                }
            }
            if (!spec.vase && !shrub)
            {
                // The leader's top carries the crown's top clump; one more clump fills the crown's heart from below.
                var top = trunkPts[trunkPts.Count - 1];
                AddClump(clumps, Inside(top + Vector3.up * spec.clumpR * 0.5f, spec.clumpR * 0.7f), spec.clumpR * 1.1f);
                AddClump(clumps, Inside(TrunkAt(Mathf.Lerp(spec.fork, spec.leaderTop, 0.7f)) + Vector3.up * 0.2f, spec.clumpR), spec.clumpR * 1.15f);
            }
            if (shrub)
            {
                // A shrub's middle, on top of its stems.
                AddClump(clumps, crownC + Vector3.up * spec.crownRy * 0.25f, spec.clumpR * 1.25f);
            }
            if (spec.vase)
            {
                // The vase's open top: a couple of clumps across the middle, carried on short branches off the limbs above.
                for (int k = 0; k < 2; k++)
                {
                    float a = az0 + k * Mathf.PI + 0.7f;
                    var p = Inside(crownC + new Vector3(Mathf.Cos(a) * spec.crownR * 0.25f, spec.crownRy * 0.35f, Mathf.Sin(a) * spec.crownR * 0.25f), spec.clumpR);
                    var from = TrunkAt(spec.leaderTop);
                    var pts = Curve(from, p, Vector3.up, rng, 4);
                    Sweep(near, pts, Taper(pts, r0 * 0.3f, r0 * 0.08f), 5, 0f);
                    AddClump(clumps, p, spec.clumpR * 1.05f);
                }
            }

            if (!shrub) FillShell(near, spec, crownC, clumps, limbs, rng);
            var set = new MeshSet
            {
                trunk = MakeMesh(key + " trunk", near),
                trunkLow = MakeMesh(key + " trunk far", far),
            };
            int s0 = variant * 31 + (int)species * 97;
            set.crown0 = Crown(key + " crown0", spec, clumps, crownC, spec.cardsPerClump, 1.08f, 1, new System.Random(s0 + 1), out set.cards0, out set.area0, true);
            set.crown1 = Crown(key + " crown1", spec, clumps, crownC, 2, 1.5f, 1, new System.Random(s0 + 2), out set.cards1, out _, true);
            set.crown2 = Crown(key + " crown2", spec, clumps, crownC, 1, 2.2f, 2, new System.Random(s0 + 3), out set.cards2, out _, true);
            return set;
        }

        static void AddClump(List<Clump> clumps, Vector3 c, float r)
        {
            // Two clumps nearly on top of each other double the overdraw for nothing: merge them.
            for (int i = 0; i < clumps.Count; i++)
            {
                var o = clumps[i];
                if ((o.c - c).sqrMagnitude < (0.45f * Mathf.Min(o.r, r)) * (0.45f * Mathf.Min(o.r, r)))
                {
                    // Grow the clump that's there to cover the new one instead of moving it: a moved clump left its own
                    // branch tip poking out (round 3's "twig spikes").
                    clumps[i] = new Clump { c = o.c, r = Mathf.Max(o.r, (o.c - c).magnitude + r * 0.8f) };
                    return;
                }
            }
            clumps.Add(new Clump { c = c, r = r });
        }

        /// <summary>
        /// Fills holes in the crown's shell (round 3: "separate clumps with sky between, topiary"): points spread evenly over
        /// the crown's outline (a Fibonacci sphere, the underside left open) that are far from every clump get a clump of
        /// their own, carried on a short branch from the nearest point of the nearest limb (never floating).
        /// </summary>
        static void FillShell(Wood near, Spec spec, Vector3 crownC, List<Clump> clumps, List<List<Vector3>> limbs, System.Random rng)
        {
            if (limbs.Count == 0) return;
            const int samples = 40;
            float golden = Mathf.PI * (3f - Mathf.Sqrt(5f));
            for (int k = 0; k < samples; k++)
            {
                float y = 1f - (k + 0.5f) / samples * 2f;
                if (y < -0.45f) continue;   // leave the underside open: you see up into a tree
                float rr = Mathf.Sqrt(1f - y * y), a = k * golden;
                var p = crownC + new Vector3(Mathf.Cos(a) * rr * spec.crownR, y * spec.crownRy, Mathf.Sin(a) * rr * spec.crownR) * 0.72f;
                float gap = float.MaxValue;
                foreach (var c in clumps) gap = Mathf.Min(gap, (c.c - p).magnitude - c.r);
                if (gap < spec.clumpR * 0.15f) continue;
                // The nearest point on any limb's outer two thirds.
                Vector3 best = Vector3.zero, bestDir = Vector3.up;
                float bd = float.MaxValue;
                foreach (var limb in limbs)
                    for (int i = limb.Count / 3; i < limb.Count; i++)
                    {
                        float d = (limb[i] - p).sqrMagnitude;
                        if (d < bd) { bd = d; best = limb[i]; bestDir = (limb[Mathf.Min(i + 1, limb.Count - 1)] - limb[Mathf.Max(i - 1, 0)]).normalized; }
                    }
                var dir = (p - best).normalized;
                float cr = spec.clumpR * Mathf.Lerp(0.85f, 1.05f, (float)rng.NextDouble());
                var end = p - dir * cr * 0.35f;
                if ((end - best).magnitude > 0.2f)
                {
                    var pts = Curve(best, end, (bestDir * 0.5f + dir).normalized, rng, 3);
                    Sweep(near, pts, Taper(pts, spec.trunkR * 0.16f, spec.trunkR * 0.05f), 4, 0f);
                }
                clumps.Add(new Clump { c = p, r = cr });
            }
        }

        /// <summary>A bent path from <paramref name="a"/> to <paramref name="b"/> leaving <paramref name="a"/> along
        /// <paramref name="startDir"/>: a cubic Bezier with a little sideways wander, <paramref name="n"/> segments.</summary>
        static List<Vector3> Curve(Vector3 a, Vector3 b, Vector3 startDir, System.Random rng, int n)
        {
            float R() => (float)rng.NextDouble();
            float len = Vector3.Distance(a, b);
            var toB = (b - a) / Mathf.Max(len, 1e-4f);
            var wander = new Vector3(R() - 0.5f, (R() - 0.5f) * 0.4f, R() - 0.5f) * len * 0.12f;
            var c1 = a + startDir * len * 0.38f + wander;
            var endDir = (toB + Vector3.down * 0.15f).normalized;   // tips settle a little (weight of the leaves)
            var c2 = b - endDir * len * 0.3f - wander * 0.5f;
            var pts = new List<Vector3>(n + 1);
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n, u = 1f - t;
                pts.Add(u * u * u * a + 3f * u * u * t * c1 + 3f * u * t * t * c2 + t * t * t * b);
            }
            return pts;
        }

        static List<float> Taper(List<Vector3> pts, float r0, float r1)
        {
            float total = 0f;
            for (int i = 1; i < pts.Count; i++) total += Vector3.Distance(pts[i - 1], pts[i]);
            var radii = new List<float>(pts.Count);
            float acc = 0f;
            for (int i = 0; i < pts.Count; i++)
            {
                if (i > 0) acc += Vector3.Distance(pts[i - 1], pts[i]);
                float k = total > 0f ? acc / total : 0f;
                radii.Add(Mathf.Lerp(r0, r1, Mathf.Pow(k, 0.8f)));
            }
            return radii;
        }

        static List<T> Thin<T>(List<T> list)
        {
            var o = new List<T>();
            for (int i = 0; i < list.Count; i += 2) o.Add(list[i]);
            if ((list.Count - 1) % 2 != 0) o.Add(list[list.Count - 1]);
            return o;
        }

        /// <summary>
        /// One tube swept along a path: rings with parallel-transported frames (no twist), continuous bark UVs (u round the
        /// girth in metres, v along the length from <paramref name="vOffset"/>), normals out from the axis.
        /// </summary>
        static void Sweep(Wood w, List<Vector3> pts, List<float> radii, int sides, float vOffset)
        {
            if (pts.Count < 2) return;
            int start = w.v.Count;
            var t0 = (pts[1] - pts[0]).normalized;
            var normal = Vector3.Cross(t0, Mathf.Abs(t0.y) > 0.95f ? Vector3.right : Vector3.up).normalized;
            var prevT = t0;
            float vAcc = vOffset;
            float circ = Mathf.Max(Mathf.PI * 2f * radii[0], 0.25f);
            for (int i = 0; i < pts.Count; i++)
            {
                var tan = i == 0 ? t0 : i == pts.Count - 1 ? (pts[i] - pts[i - 1]).normalized : (pts[i + 1] - pts[i - 1]).normalized;
                if (i > 0)
                {
                    normal = Quaternion.FromToRotation(prevT, tan) * normal;
                    vAcc += Vector3.Distance(pts[i - 1], pts[i]);
                }
                prevT = tan;
                normal = Vector3.ProjectOnPlane(normal, tan).normalized;
                var bin = Vector3.Cross(normal, tan);   // this sense + the (i0, i2, i1) winding face outward (Unity: clockwise = front)
                for (int s = 0; s <= sides; s++)
                {
                    float ang = s / (float)sides * Mathf.PI * 2f;
                    var dir = normal * Mathf.Cos(ang) + bin * Mathf.Sin(ang);
                    w.v.Add(pts[i] + dir * radii[i]);
                    w.n.Add(dir);
                    w.uv.Add(new Vector2(s / (float)sides * circ, vAcc));
                }
            }
            int row = sides + 1;
            for (int i = 0; i < pts.Count - 1; i++)
                for (int s = 0; s < sides; s++)
                {
                    int i0 = start + i * row + s, i1 = i0 + 1, i2 = i0 + row, i3 = i2 + 1;
                    w.t.Add(i0); w.t.Add(i2); w.t.Add(i1);
                    w.t.Add(i1); w.t.Add(i2); w.t.Add(i3);
                }
            // A rounded cap on a thick open end (the elm's trunk top under its vase of limbs, seen from the hill).
            int last = pts.Count - 1;
            if (radii[last] > 0.03f)
            {
                int capCentre = w.v.Count;
                w.v.Add(pts[last] + prevT * radii[last] * 0.6f);
                w.n.Add(prevT);
                w.uv.Add(new Vector2(circ * 0.5f, vAcc + radii[last]));
                int ring = start + last * row;
                for (int s = 0; s < sides; s++)
                {
                    w.t.Add(ring + s + 1); w.t.Add(ring + s); w.t.Add(capCentre);
                }
            }
        }

        static Mesh MakeMesh(string name, Wood w)
        {
            var m = new Mesh { name = name };
            if (w.v.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(w.v); m.SetNormals(w.n); m.SetUVs(0, w.uv); m.SetTriangles(w.t, 0);
            m.RecalculateTangents(); m.RecalculateBounds();
            return m;
        }

        struct Card
        {
            public Vector3 p, r, u, n;
            public float rim, sortKey;
            public Color tint;
            public bool flip;
        }

        /// <summary>
        /// A crown: <paramref name="perClump"/> cards round each clump (every <paramref name="clumpStep"/>-th clump), each card
        /// <paramref name="sizeK"/> x the clump's radius wide, facing outward from the clump with some scatter, shaded with a
        /// normal between the clump's and the whole crown's (clumps read as clumps, the crown as one volume). Cards are
        /// written outermost first. Returns the card count and total card area.
        /// </summary>
        static Mesh Crown(string name, Spec spec, List<Clump> clumps, Vector3 crownC, int perClump, float sizeK, int clumpStep,
            System.Random rng, out int cardCount, out float area, bool core = false)
        {
            float R() => (float)rng.NextDouble();
            var cards = new List<Card>();
            var cell = new Vector2((spec.cell % 2) * 0.5f, (spec.cell / 2) * 0.5f);
            area = 0f;
            for (int ci = 0; ci < clumps.Count; ci += clumpStep)
            {
                var cl = clumps[ci];
                float r = cl.r * (clumpStep > 1 ? 1.2f : 1f);
                var fromC = cl.c - crownC;
                var ellN = new Vector3(fromC.x / (spec.crownR * spec.crownR), fromC.y / (spec.crownRy * spec.crownRy), fromC.z / (spec.crownR * spec.crownR));
                ellN = ellN.sqrMagnitude > 1e-6f ? ellN.normalized : Vector3.up;
                float e = Mathf.Sqrt((fromC.x * fromC.x + fromC.z * fromC.z) / (spec.crownR * spec.crownR) + fromC.y * fromC.y / (spec.crownRy * spec.crownRy));
                // A per-clump tint: some clumps a little darker or more olive (the crown isn't one flat green).
                var clumpTint = new Color(Mathf.Lerp(0.92f, 1.04f, R()), Mathf.Lerp(0.95f, 1.05f, R()), Mathf.Lerp(0.9f, 1.02f, R()));
                for (int k = 0; k < perClump; k++)
                {
                    // Directions spread round the clump, biased outward from the crown and upward (the outer shell).
                    var rnd = new Vector3(R() * 2f - 1f, R() * 2f - 1f, R() * 2f - 1f);
                    if (perClump > 1)
                    {
                        float a = (k + R() * 0.6f) / perClump * Mathf.PI * 2f;
                        rnd = new Vector3(Mathf.Cos(a), (R() - 0.35f) * 1.2f, Mathf.Sin(a));
                    }
                    var dir = (ellN * 0.85f + rnd.normalized * 0.75f + Vector3.up * 0.15f).normalized;
                    var p = cl.c + dir * r * (perClump > 1 ? Mathf.Lerp(0.25f, 0.45f, R()) : 0.1f);
                    var n = (ellN * 0.6f + dir * 0.4f + Vector3.up * 0.1f).normalized;
                    var facing = (dir + new Vector3(R() - 0.5f, R() - 0.5f, R() - 0.5f) * 0.5f).normalized;
                    var right = Vector3.Cross(facing, Mathf.Abs(facing.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
                    var up = Vector3.Cross(right, facing);
                    float twist = (R() - 0.5f) * 1.2f;   // mostly upright: the clump's twig base points down
                    float half = r * sizeK * 0.5f * Mathf.Lerp(0.88f, 1.12f, R());
                    var rr = (right * Mathf.Cos(twist) + up * Mathf.Sin(twist)) * half;
                    var uu = (-right * Mathf.Sin(twist) + up * Mathf.Cos(twist)) * half;
                    float heightK = Mathf.InverseLerp(crownC.y - spec.crownRy, crownC.y + spec.crownRy, p.y);
                    float v = Mathf.Lerp(0.82f, 1.05f, heightK) * Mathf.Lerp(0.9f, 1.06f, R());
                    float sway = Mathf.Clamp01(new Vector2(p.x, p.z).magnitude / Mathf.Max(0.5f, spec.crownR)) * 0.8f + 0.2f;
                    var tint = new Color(clumpTint.r * v, clumpTint.g * v, clumpTint.b * v, sway);
                    float ep = Mathf.Sqrt(((p - crownC).x * (p - crownC).x + (p - crownC).z * (p - crownC).z) / (spec.crownR * spec.crownR) +
                                          (p - crownC).y * (p - crownC).y / (spec.crownRy * spec.crownRy));
                    cards.Add(new Card
                    {
                        p = p, r = rr, u = uu, n = n, tint = tint, flip = R() < 0.5f,
                        rim = Mathf.Clamp01((Mathf.Max(e, ep) - 0.3f) / 0.6f),
                        sortKey = -ep,
                    });
                    area += (2f * half) * (2f * half);
                }
            }
            // Outermost first: the shell is drawn before the cards behind it, which then fail the depth test early.
            cards.Sort((x, y) => x.sortKey.CompareTo(y.sortKey));
            cardCount = cards.Count;
            var cv = new List<Vector3>(cards.Count * 4); var cn = new List<Vector3>(cards.Count * 4); var cuv = new List<Vector2>(cards.Count * 4);
            var cuv1 = new List<Vector2>(cards.Count * 4); var cc = new List<Color>(cards.Count * 4); var ct = new List<int>(cards.Count * 6);
            foreach (var cd in cards)
            {
                int b = cv.Count;
                cv.Add(cd.p - cd.r - cd.u); cv.Add(cd.p - cd.r + cd.u); cv.Add(cd.p + cd.r + cd.u); cv.Add(cd.p + cd.r - cd.u);
                float u0 = cd.flip ? 0.495f : 0.005f, u1 = cd.flip ? 0.005f : 0.495f;
                cuv.Add(cell + new Vector2(u0, 0.005f)); cuv.Add(cell + new Vector2(u0, 0.495f));
                cuv.Add(cell + new Vector2(u1, 0.495f)); cuv.Add(cell + new Vector2(u1, 0.005f));
                for (int k = 0; k < 4; k++) { cn.Add(cd.n); cc.Add(cd.tint); cuv1.Add(new Vector2(cd.rim, 0f)); }
                ct.Add(b); ct.Add(b + 1); ct.Add(b + 2); ct.Add(b); ct.Add(b + 2); ct.Add(b + 3);
            }
            if (core) Core(spec, clumps, crownC, cell, cv, cn, cuv, cuv1, cc, ct);
            var mesh = new Mesh { name = name };
            mesh.SetVertices(cv); mesh.SetNormals(cn); mesh.SetUVs(0, cuv); mesh.SetUVs(1, cuv1); mesh.SetColors(cc); mesh.SetTriangles(ct, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// The crown's inner leaf volume (round 3: sky showing straight through the middle of every crown): one small lumpy
        /// ball inside each clump (60% of its radius), textured from the dense middle of the clump cell, dark (painted
        /// occlusion: rim 0), drawn after the cards. Gaps between the cards show leaves, not sky; the cards keep the
        /// silhouette. 24 triangles a clump.
        /// </summary>
        static void Core(Spec spec, List<Clump> clumps, Vector3 crownC, Vector2 cell, List<Vector3> cv, List<Vector3> cn, List<Vector2> cuv,
            List<Vector2> cuv1, List<Color> cc, List<int> ct)
        {
            // An octahedron split once (18 vertices, 32 faces would be more; this is 6 + 12 = a 24-face ball).
            var dirs = new List<Vector3> { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            int[] faces = { 0, 2, 4, 4, 2, 1, 1, 2, 5, 5, 2, 0, 0, 4, 3, 4, 1, 3, 1, 5, 3, 5, 0, 3 };
            var mid = new Dictionary<long, int>();
            var tris = new List<int>();
            int Mid(int a, int b)
            {
                long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                if (mid.TryGetValue(key, out int i)) return i;
                dirs.Add((dirs[a] + dirs[b]).normalized);
                mid[key] = dirs.Count - 1;
                return dirs.Count - 1;
            }
            for (int f = 0; f < faces.Length; f += 3)
            {
                int a = faces[f], b = faces[f + 1], c = faces[f + 2];
                int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                tris.AddRange(new[] { a, ab, ca, ab, b, bc, ca, bc, c, ab, bc, ca });
            }
            foreach (var cl in clumps)
            {
                int b0 = cv.Count;
                float r = cl.r * 0.6f;
                for (int i = 0; i < dirs.Count; i++)
                {
                    var d = dirs[i];
                    float lump = 1f + 0.15f * Mathf.Sin(d.x * 5.1f + cl.c.x) * Mathf.Cos(d.z * 4.3f + cl.c.z);
                    cv.Add(cl.c + d * r * lump);
                    var fromC = cl.c - crownC;
                    cn.Add((d * 0.5f + new Vector3(fromC.x / (spec.crownR * spec.crownR), fromC.y / (spec.crownRy * spec.crownRy), fromC.z / (spec.crownR * spec.crownR)).normalized * 0.5f).normalized);
                    // The clump cell's dense middle (u, v 0.16-0.34 of the cell's 0.5): leaves, few holes.
                    cuv.Add(cell + new Vector2(0.25f + d.x * 0.08f + d.z * 0.03f, 0.25f + d.y * 0.08f));
                    cuv1.Add(new Vector2(0.15f, 0f));
                    cc.Add(new Color(0.8f, 0.85f, 0.8f, 0.3f));
                }
                // Outward winding (the foliage shader is double-sided, so this only matters for consistency).
                foreach (var t in tris) ct.Add(b0 + t);
            }
        }

        /// <summary>A tapered straight tube from a to b (bark UVs in metres: u round the girth, v along the length). Kept for
        /// other builders; the trees use swept tubes.</summary>
        public static void Tube(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t, Vector3 a, Vector3 b, float ra, float rb, int sides, float vOffset)
        {
            var w = new Wood();
            Sweep(w, new List<Vector3> { a, Vector3.Lerp(a, b, 0.5f), b }, new List<float> { ra, (ra + rb) * 0.5f, rb }, sides, vOffset);
            int baseIndex = v.Count;
            v.AddRange(w.v); n.AddRange(w.n); uv.AddRange(w.uv);
            foreach (var i in w.t) t.Add(i + baseIndex);
        }
    }
}
