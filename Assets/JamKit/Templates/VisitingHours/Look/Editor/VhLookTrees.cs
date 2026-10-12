using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace JamKit.Look.EditorTools
{
    /// <summary>
    /// Stylized trees for the hill and the yards (the kind over a 1990s suburban street), built in code from the library's
    /// bark and the painted leaf-cluster atlas (tools/look/make-foliage-atlas.py) with the JamKit/Look/Foliage shader.
    /// <para>Pass 2 (the judge's round 1: "lollipop trees"): four species with their own branching and crown shapes, low
    /// branching (limbs from 2.3-3 m on a 10 m tree), crowns of many small leaf cards built in clumps round the branch
    /// tips and kept inside the crown's outline (no stray clumps), olive and warm greens, a rim value per card so backlit
    /// crowns glow at their edges.</para>
    /// <para><b>Levels of detail</b> (the creator, 2026-10-12: "the tree in the garden" can't cost what "a tree you can barely
    /// see the branches of behind the hospital" costs; 120 fps target), cross-faded, thresholds in <see cref="Lods"/>:
    /// LOD0 full (~580 small cards, trunk, limbs, twigs, shadows); LOD1 simplified (a third of the cards, bigger; shadows);
    /// LOD2 far (~40 big cards and the bare trunk, no shadows); LOD3 an impostor: a crossed billboard of the painted whole
    /// tree (VH_TreeBillboards, the same foliage shader, so the backlit glow still reads; no shadows); culled beyond. All
    /// on two SRP Batcher-compatible, instancing-ready materials (bark, foliage).</para>
    /// <list type="bullet">
    /// <item><b>Maple</b>: a round, full crown on a short trunk (the lawn tree).</item>
    /// <item><b>Oak</b>: a broad, spreading crown on low, near-horizontal limbs; clumpy, with sky between the clumps.</item>
    /// <item><b>Birch</b>: tall and narrow, a pale trunk running up through a light, fine-leaved oval crown.</item>
    /// <item><b>Elm</b>: a vase: limbs fanning upward from 3 m into a wide, flat-topped crown (the street tree).</item>
    /// <item><b>Shrub</b>: a bush (foundation planting, hedges, the understorey of a stand of trees).</item>
    /// </list>
    /// Each species has three variants; a tree is a variant's meshes scaled to its height, so many trees share a few meshes.
    /// Meshes are cached as assets in Look/Library/Trees/.
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
        const int GeneratorVersion = 3;
        public const int Variants = 3;

        public enum Species { Maple, Oak, Birch, Elm, Shrub }

        /// <summary>
        /// LOD switch points as screen-relative heights (the share of the screen's height the tree covers): below
        /// <see cref="Lod1"/> the tree drops to LOD1, below <see cref="Lod2"/> to LOD2 and so on, below <see cref="Cull"/> it
        /// isn't drawn. For a 10 m tree at 1080p, 60 degree FOV and LOD bias 1: LOD1 from ~70 m, LOD2 from ~190 m, the
        /// billboard from ~460 m, culled past ~2.2 km. The Perf agent tunes these: change them, rebuild the scene (or call
        /// <see cref="RetuneLods"/> on an open scene).
        /// </summary>
        public static class Lods
        {
            public static float Lod1 = 0.12f, Lod2 = 0.045f, Lod3 = 0.019f, Cull = 0.004f;
            /// <summary>Shrubs: LOD1 and LOD2 points and the cull (they have no billboard).</summary>
            public static float ShrubLod1 = 0.06f, ShrubLod2 = 0.02f, ShrubCull = 0.006f;
        }

        /// <summary>Re-applies <see cref="Lods"/> to every tree's LODGroup under <paramref name="root"/> (a scene's root
        /// objects, say), so a tuning pass doesn't need a rebuild. Returns the number of trees changed.</summary>
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
            public float refHeight, fork, crownR, crownRy, crownY, limbUp, card, lobeR, trunkR;
            public int limbs, cards, cell;
            public bool leader;
            public Color tint;
        }

        static Spec SpecOf(Species s)
        {
            switch (s)
            {
                case Species.Oak:
                    return new Spec { refHeight = 10f, fork = 2.3f, crownR = 4.9f, crownRy = 2.9f, crownY = 6.7f, limbUp = 0.32f, card = 0.88f,
                        lobeR = 1.75f, trunkR = 0.42f, limbs = 6, cards = 620, cell = 1, tint = new Color(0.93f, 0.95f, 0.84f) };
                case Species.Birch:
                    return new Spec { refHeight = 10f, fork = 2.6f, crownR = 2.5f, crownRy = 3.7f, crownY = 6.2f, limbUp = 0.95f, card = 0.66f,
                        lobeR = 1.05f, trunkR = 0.2f, limbs = 8, cards = 520, cell = 2, leader = true, tint = new Color(1.04f, 1.04f, 0.9f) };
                case Species.Elm:
                    return new Spec { refHeight = 10f, fork = 3.0f, crownR = 4.4f, crownRy = 2.9f, crownY = 7.0f, limbUp = 1.15f, card = 0.84f,
                        lobeR = 1.55f, trunkR = 0.36f, limbs = 5, cards = 580, cell = 3, tint = new Color(0.92f, 0.98f, 0.9f) };
                case Species.Shrub:
                    return new Spec { refHeight = 1.6f, fork = 0.15f, crownR = 0.95f, crownRy = 0.75f, crownY = 0.85f, limbUp = 0.8f, card = 0.42f,
                        lobeR = 0.48f, trunkR = 0.05f, limbs = 4, cards = 150, cell = 0, tint = new Color(0.95f, 1f, 0.9f) };
                default:
                    return new Spec { refHeight = 10f, fork = 2.8f, crownR = 3.9f, crownRy = 3.3f, crownY = 6.4f, limbUp = 0.62f, card = 0.84f,
                        lobeR = 1.5f, trunkR = 0.36f, limbs = 5, cards = 580, cell = 0, tint = new Color(1f, 1f, 0.92f) };
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

            var bark = species == Species.Birch ? VhLookLibrary.Tinted("Tree_Bark", "Birch", new Color(2.3f, 2.25f, 2.1f)) : VhLookLibrary.Get("Tree_Bark");
            var leaves = VhLookLibrary.FoliageMaterial(dryCrown);
            var trunk = Part(go.transform, "Trunk", set.trunk, bark);
            var trunkFar = Part(go.transform, "Trunk (far)", set.trunkLow, bark);
            var c0 = Part(go.transform, "Crown", set.crown0, leaves);
            var c1 = Part(go.transform, "Crown (LOD1)", set.crown1, leaves);
            var c2 = Part(go.transform, "Crown (LOD2)", set.crown2, leaves);
            // The far levels cast no shadows (at that distance the shadow is a few pixels in the last cascade, or none).
            trunkFar.shadowCastingMode = ShadowCastingMode.Off;
            c2.shadowCastingMode = ShadowCastingMode.Off;
            MeshRenderer board = null;
            if (species != Species.Shrub)
            {
                board = Part(go.transform, "Billboard (LOD3)", Billboard(species), VhLookLibrary.TreeBillboardMaterial(dryCrown));
                board.shadowCastingMode = ShadowCastingMode.Off;
            }
            if (species != Species.Shrub)
            {
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
                // A gaussian-ish spread: most trees near the middle, some straggling out.
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
                set = Build(species, variant, key);
                VhLookLibrary.EnsureAssetFolder(path);
                AssetDatabase.CreateAsset(set.trunk, path);
                AssetDatabase.AddObjectToAsset(set.trunkLow, path);
                AssetDatabase.AddObjectToAsset(set.crown0, path);
                AssetDatabase.AddObjectToAsset(set.crown1, path);
                AssetDatabase.AddObjectToAsset(set.crown2, path);
                AssetDatabase.SaveAssets();
            }
            Cache[key] = set;
            return set;
        }

        struct Lobe
        {
            public Vector3 c;
            public float r;
        }

        static MeshSet Build(Species species, int variant, string key)
        {
            var spec = SpecOf(species);
            var rng = new System.Random((int)species * 1009 + variant * 7919 + 17);
            float R() => (float)rng.NextDouble();
            float H = spec.refHeight;

            // ---- Trunk, limbs and branches (LOD0/1), and a trunk with its main limbs only (LOD2) ----
            var tv = new List<Vector3>(); var tn = new List<Vector3>(); var tuv = new List<Vector2>(); var tt = new List<int>();
            var lv = new List<Vector3>(); var ln = new List<Vector3>(); var luv = new List<Vector2>(); var lt = new List<int>();
            float r0 = spec.trunkR;
            var lean = new Vector3((R() - 0.5f) * 0.14f, 1f, (R() - 0.5f) * 0.14f).normalized;
            var forkPoint = lean * spec.fork;
            var crownC = new Vector3((R() - 0.5f) * 0.1f * spec.crownR, spec.crownY, (R() - 0.5f) * 0.1f * spec.crownR);
            int sides = species == Species.Shrub ? 5 : 12;
            // The trunk in two bends (a straight cylinder reads as a pole), and the root flare.
            var bend = forkPoint * 0.5f + new Vector3((R() - 0.5f) * 0.18f, 0f, (R() - 0.5f) * 0.18f) * spec.fork / 3f;
            Tube(tv, tn, tuv, tt, Vector3.down * 0.3f, bend, r0 * 1.12f, r0 * 0.95f, sides, 0f);
            Tube(tv, tn, tuv, tt, bend, forkPoint, r0 * 0.95f, r0 * 0.8f, sides, spec.fork * 0.5f + 0.3f);
            Tube(lv, ln, luv, lt, Vector3.down * 0.3f, forkPoint, r0 * 1.1f, r0 * 0.8f, 6, 0f);
            if (species != Species.Shrub)
            {
                Tube(tv, tn, tuv, tt, Vector3.down * 0.25f, Vector3.up * 0.5f, r0 * 1.65f, r0 * 1.05f, sides, 0.3f);
                // Root buttresses: a few short flares out along the ground.
                int roots = 4 + rng.Next(2);
                for (int k = 0; k < roots; k++)
                {
                    float a = (k + R() * 0.5f) / roots * Mathf.PI * 2f;
                    var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    Tube(tv, tn, tuv, tt, Vector3.up * 0.35f + d * r0 * 0.5f, d * r0 * (1.9f + R() * 0.6f) + Vector3.down * 0.12f, r0 * 0.45f, r0 * 0.12f, 6, 0f);
                }
            }
            var leaderTop = new Vector3(lean.x * H * 0.8f, H * 0.86f, lean.z * H * 0.8f);
            if (spec.leader)
            {
                // Birch: the trunk carries on up through the crown.
                Tube(tv, tn, tuv, tt, forkPoint, leaderTop, r0 * 0.8f, r0 * 0.2f, sides, spec.fork + 0.3f);
                Tube(lv, ln, luv, lt, forkPoint, leaderTop, r0 * 0.8f, r0 * 0.25f, 6, spec.fork + 0.3f);
            }

            var lobes = new List<Lobe>();
            void AddLobe(Vector3 c, float r)
            {
                // Keep the whole lobe inside the crown's ellipsoid (shrunk by the lobe's radius): no clumps outside the outline.
                var d = c - crownC;
                float ax = Mathf.Max(0.2f, spec.crownR - 0.8f * r), ay = Mathf.Max(0.2f, spec.crownRy - 0.5f * r);
                float e = Mathf.Sqrt((d.x * d.x + d.z * d.z) / (ax * ax) + d.y * d.y / (ay * ay));
                if (e > 1f) c = crownC + d / e;
                lobes.Add(new Lobe { c = c, r = r });
            }

            for (int i = 0; i < spec.limbs; i++)
            {
                float a = (i + R() * 0.55f) / spec.limbs * Mathf.PI * 2f;
                var dirH = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 start = spec.leader
                    ? Vector3.Lerp(forkPoint, leaderTop, 0.1f + (i + 0.5f) / spec.limbs * 0.75f)
                    : forkPoint + Vector3.down * (0.35f * R()) * spec.fork / 3f;
                float reach = spec.crownR * Mathf.Lerp(0.55f, 0.82f, R());
                float lift = spec.limbUp + (R() - 0.5f) * 0.4f;
                var end = new Vector3(crownC.x, 0f, crownC.z) + dirH * reach;
                // Limb tips spread over the crown's height (some low and drooping, some high), so the crown reaches down
                // towards the fork instead of sitting on a bare pole; oaks keep theirs low and wide, elms high (a vase).
                float f = Mathf.Clamp01(Mathf.Repeat(i * 0.618f + R() * 0.25f, 1f) * 0.8f + (spec.limbUp - 0.6f) * 0.5f);
                end.y = spec.leader
                    ? start.y + reach * lift * 0.6f
                    : crownC.y + spec.crownRy * Mathf.Lerp(-0.6f, 0.4f, f);
                var mid = Vector3.Lerp(start, end, 0.45f) + Vector3.up * (0.25f + R() * 0.35f) * spec.crownRy / 3f;
                float lr = r0 * (spec.leader ? 0.32f : 0.6f);
                int ls = species == Species.Shrub ? 4 : 8;
                Tube(tv, tn, tuv, tt, start, mid, lr, lr * 0.7f, ls, 0f);
                Tube(tv, tn, tuv, tt, mid, end, lr * 0.7f, lr * 0.25f, Mathf.Max(4, ls - 1), 0f);
                if (!spec.leader && species != Species.Shrub) Tube(lv, ln, luv, lt, start, end, lr, lr * 0.3f, 5, 0f);
                AddLobe(end + Vector3.up * spec.lobeR * 0.25f, spec.lobeR * Mathf.Lerp(0.85f, 1.15f, R()));
                // Branches off each limb, each ending in its own clump.
                int branches = species == Species.Shrub ? 1 : 2 + rng.Next(2);
                for (int k = 0; k < branches; k++)
                {
                    var b0 = Vector3.Lerp(mid, end, 0.15f + 0.6f * R());
                    var outward = (dirH + new Vector3(R() - 0.5f, 0f, R() - 0.5f) * 1.3f).normalized;
                    var b1 = b0 + outward * spec.crownR * Mathf.Lerp(0.25f, 0.45f, R()) + Vector3.up * spec.crownRy * Mathf.Lerp(0.05f, 0.5f, R());
                    Tube(tv, tn, tuv, tt, b0, b1, lr * 0.32f, lr * 0.08f, 5, 0f);
                    AddLobe(b1, spec.lobeR * Mathf.Lerp(0.7f, 1.0f, R()));
                    // A twig.
                    var tw = b1 + new Vector3(R() - 0.5f, 0.6f, R() - 0.5f).normalized * spec.lobeR * 0.6f;
                    Tube(tv, tn, tuv, tt, Vector3.Lerp(b0, b1, 0.6f), tw, lr * 0.1f, lr * 0.03f, 4, 0f);
                }
            }
            // A crown top and an inner fill so the crown's middle isn't hollow.
            AddLobe(crownC + Vector3.up * spec.crownRy * 0.45f, spec.lobeR * 1.05f);
            AddLobe(crownC, spec.lobeR * 1.2f);

            var set = new MeshSet
            {
                trunk = MakeMesh(key + " trunk", tv, tn, tuv, tt),
                trunkLow = MakeMesh(key + " trunk far", lv, ln, luv, lt),
            };
            // ---- Crowns: LOD0 many small cards, LOD1 a third as many and bigger, LOD2 a few big cards ----
            int s0 = variant * 31 + (int)species * 97;
            set.crown0 = Crown(key + " crown0", spec, lobes, crownC, spec.cards, spec.card, new System.Random(s0 + 1));
            set.crown1 = Crown(key + " crown1", spec, lobes, crownC, Mathf.RoundToInt(spec.cards / 3.2f), spec.card * 1.65f, new System.Random(s0 + 2));
            set.crown2 = Crown(key + " crown2", spec, lobes, crownC, Mathf.Max(18, spec.cards / 14), spec.card * 3.0f, new System.Random(s0 + 3));
            return set;
        }

        static Mesh MakeMesh(string name, List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t)
        {
            var m = new Mesh { name = name };
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(t, 0);
            m.RecalculateTangents(); m.RecalculateBounds();
            return m;
        }

        static Mesh Crown(string name, Spec spec, List<Lobe> lobes, Vector3 crownC, int cards, float card, System.Random rng)
        {
            float R() => (float)rng.NextDouble();
            var cv = new List<Vector3>(cards * 4); var cn = new List<Vector3>(cards * 4); var cuv = new List<Vector2>(cards * 4);
            var cuv1 = new List<Vector2>(cards * 4); var cc = new List<Color>(cards * 4); var ct = new List<int>(cards * 6);
            // Lobes weighted by volume.
            var weights = new float[lobes.Count];
            float total = 0f;
            for (int i = 0; i < lobes.Count; i++) { total += lobes[i].r * lobes[i].r * lobes[i].r; weights[i] = total; }
            var cell = new Vector2((spec.cell % 2) * 0.5f, (spec.cell / 2) * 0.5f);
            float half = card * 0.5f;
            int made = 0, tries = 0;
            while (made < cards && tries++ < cards * 8)
            {
                float pick = R() * total;
                int li = 0;
                while (li < lobes.Count - 1 && weights[li] < pick) li++;
                var lobe = lobes[li];
                var d = new Vector3(R() * 2f - 1f, R() * 2f - 1f, R() * 2f - 1f);
                if (d.sqrMagnitude > 1f || d.sqrMagnitude < 1e-3f) continue;
                d.Normalize();
                // Mostly on the clump's outer shell (a full silhouette), some inside (depth when you look through a gap).
                float shell = 1f - 0.62f * R() * R();
                var p = lobe.c + d * Mathf.Max(0.05f, lobe.r - half * 0.6f) * shell;
                // Inside the crown's outline, card and all.
                var fromC = p - crownC;
                float ex = Mathf.Max(0.2f, spec.crownR - half * 0.4f), ey = Mathf.Max(0.2f, spec.crownRy - half * 0.4f);
                float e = Mathf.Sqrt((fromC.x * fromC.x + fromC.z * fromC.z) / (ex * ex) + fromC.y * fromC.y / (ey * ey));
                if (e > 1f) continue;
                // Shading normal: partly the clump's own (clumps read as clumps), partly the whole crown's.
                var ellN = new Vector3(fromC.x / (spec.crownR * spec.crownR), fromC.y / (spec.crownRy * spec.crownRy), fromC.z / (spec.crownR * spec.crownR)).normalized;
                var lobeN = (p - lobe.c).sqrMagnitude > 1e-4f ? (p - lobe.c).normalized : ellN;
                var n = (ellN * 0.55f + lobeN * 0.45f + Vector3.up * 0.12f).normalized;
                var facing = (n + new Vector3(R() - 0.5f, R() - 0.5f, R() - 0.5f) * 1.1f).normalized;
                var right = Vector3.Cross(facing, Mathf.Abs(facing.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
                var up = Vector3.Cross(right, facing);
                float twist = R() * Mathf.PI * 2f;
                float size = Mathf.Lerp(0.8f, 1.2f, R());
                var r = (right * Mathf.Cos(twist) + up * Mathf.Sin(twist)) * half * size;
                var u = (-right * Mathf.Sin(twist) + up * Mathf.Cos(twist)) * half * size;
                bool flip = R() < 0.5f;
                // Rim: 0 deep inside the crown, 1 on its outer shell (translucency and the painted occlusion use it).
                float rim = Mathf.Clamp01((e - 0.35f) / 0.55f) * Mathf.Lerp(0.6f, 1f, shell);
                // Tint: the species' colour, per-card variation, a little darker low in the crown and warmer at the top.
                float heightK = Mathf.InverseLerp(crownC.y - spec.crownRy, crownC.y + spec.crownRy, p.y);
                float v = Mathf.Lerp(0.8f, 1.06f, heightK) * Mathf.Lerp(0.88f, 1.08f, R());
                float warm = (R() - 0.4f) * 0.08f + heightK * 0.04f;
                var tint = new Color(spec.tint.r * v * (1f + warm), spec.tint.g * v, spec.tint.b * v * (1f - warm),
                    Mathf.Clamp01(new Vector2(fromC.x, fromC.z).magnitude / spec.crownR) * 0.8f + 0.2f);
                int b = cv.Count;
                cv.Add(p - r - u); cv.Add(p - r + u); cv.Add(p + r + u); cv.Add(p + r - u);
                float u0 = flip ? 0.49f : 0.01f, u1 = flip ? 0.01f : 0.49f;
                cuv.Add(cell + new Vector2(u0, 0.01f)); cuv.Add(cell + new Vector2(u0, 0.49f));
                cuv.Add(cell + new Vector2(u1, 0.49f)); cuv.Add(cell + new Vector2(u1, 0.01f));
                for (int k = 0; k < 4; k++) { cn.Add(n); cc.Add(tint); cuv1.Add(new Vector2(rim, 0f)); }
                ct.Add(b); ct.Add(b + 1); ct.Add(b + 2); ct.Add(b); ct.Add(b + 2); ct.Add(b + 3);
                made++;
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(cv); mesh.SetNormals(cn); mesh.SetUVs(0, cuv); mesh.SetUVs(1, cuv1); mesh.SetColors(cc); mesh.SetTriangles(ct, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A tapered tube from a to b (bark UVs in metres: u round the girth, v along the length).</summary>
        public static void Tube(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t, Vector3 a, Vector3 b, float ra, float rb, int sides, float vOffset)
        {
            var axis = (b - a);
            float len = axis.magnitude;
            if (len < 1e-4f) return;
            axis /= len;
            var side = Vector3.Cross(axis, Mathf.Abs(axis.y) > 0.95f ? Vector3.right : Vector3.up).normalized;
            var fwd = Vector3.Cross(side, axis);
            int start = v.Count;
            int rings = Mathf.Max(2, Mathf.CeilToInt(len / 0.8f) + 1);
            float circ = Mathf.Max(Mathf.PI * (ra + rb), 0.3f);
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
