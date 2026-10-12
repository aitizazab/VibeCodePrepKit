using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace JamKit.Perf.EditorTools
{
    /// <summary>
    /// Levels of detail for procedural content (docs/redesign/perf.md "LOD"): near things full detail, mid things simplified,
    /// far things a cross-billboard or a coarse shell, beyond that culled. Distances are given in metres and turned into
    /// LODGroup screen heights for a reference camera (<see cref="ReferenceFov"/>, lodBias 1), so builders think in metres.
    /// <code>
    /// // A merged 40 m chunk of the hill (one renderer per material under it): full detail to 60 m, simplified to 160 m,
    /// // coarse beyond; cards thinned; small-detail materials (grass, flowers, litter) gone after LOD0.
    /// VhPerfLod.ChunkLods(chunkRoot, VhPerfLod.ChunkSpec.Hill, detail: id => id.Contains("grass") || id.Contains("flower"));
    /// // One object (a house, a prop, a tree): simplified levels made from its own meshes.
    /// VhPerfLod.AddLods(houseGo, new VhPerfLod.Level(40f, 0.15f), new VhPerfLod.Level(120f, 0.6f), cullAt: 400f);
    /// // Small things vanish early (by their size on screen, so a big thing far away stays and a cup goes).
    /// VhPerfLod.CullBeyond(cupGo, 25f);
    /// </code>
    /// Generated meshes are saved as sub-assets of the LOD0 mesh's asset when it has one (no new files), compacted, and
    /// not readable in builds. Cross-fade is on (URP's dither), so the switch doesn't pop at golden-hour contrast.
    /// </summary>
    public static class VhPerfLod
    {
        /// <summary>The vertical field of view the metre distances are converted for (the game's cameras are 60-75).</summary>
        public const float ReferenceFov = 65f;

        /// <summary>A level: from <see cref="fromMetres"/> on, the meshes simplified to <see cref="cell"/> metres.</summary>
        public struct Level
        {
            public float fromMetres, cell;
            public bool castShadows;
            public Level(float fromMetres, float cell, bool castShadows = false) { this.fromMetres = fromMetres; this.cell = cell; this.castShadows = castShadows; }
        }

        /// <summary>Settings for a merged chunk.</summary>
        public sealed class ChunkSpec
        {
            /// <summary>Full detail until here (m from the chunk's centre).</summary>
            public float lod1At = 60f;
            public float lod2At = 160f;
            /// <summary>0: never culled (the hill's chunks stay as coarse shells to the horizon).</summary>
            public float cullAt;
            public float lod1Cell = 0.2f, lod2Cell = 0.9f;
            /// <summary>Cards kept at LOD1 (one in N) and how much each grows.</summary>
            public int lod1Cards = 2, lod2Cards = 5;
            public float lod1Grow = 1.35f, lod2Grow = 2f;
            public bool lod1Shadows = true;
            /// <summary>Card meshes drop out at LOD2 entirely (grass and flowers far away read as the ground's colour).</summary>
            public bool dropCardsAtLod2;

            public static ChunkSpec Hill => new ChunkSpec();
        }

        /// <summary>Screen-relative height for an object of <paramref name="size"/> m seen from <paramref name="metres"/>.</summary>
        public static float ScreenHeight(float size, float metres)
        {
            float h = size / (2f * Mathf.Max(0.01f, metres) * Mathf.Tan(ReferenceFov * 0.5f * Mathf.Deg2Rad));
            return Mathf.Clamp(h, 0.0001f, 1f);
        }

        static float GroupSize(LODGroup g)
        {
            g.RecalculateBounds();
            return Mathf.Max(0.01f, g.size * MaxScale(g.transform));
        }

        static float MaxScale(Transform t)
        {
            var s = t.lossyScale;
            return Mathf.Max(Mathf.Abs(s.x), Mathf.Max(Mathf.Abs(s.y), Mathf.Abs(s.z)));
        }

        // ---------- A merged chunk ----------

        /// <summary>
        /// Adds a LODGroup to <paramref name="chunkRoot"/> (a chunk: renderers with one material each under it, as
        /// HillMesh.Build makes). For each renderer: LOD1 and LOD2 meshes (cards thinned, other meshes simplified);
        /// <paramref name="detail"/> (by material name) marks small detail that appears at LOD0 only. Returns the group.
        /// </summary>
        public static LODGroup ChunkLods(GameObject chunkRoot, ChunkSpec spec, Func<string, bool> detail = null)
        {
            if (chunkRoot == null) return null;
            spec = spec ?? ChunkSpec.Hill;
            var lod0 = new List<Renderer>();
            var lod1 = new List<Renderer>();
            var lod2 = new List<Renderer>();
            foreach (var mf in chunkRoot.GetComponentsInChildren<MeshFilter>())
            {
                var r = mf.GetComponent<MeshRenderer>();
                var src = mf.sharedMesh;
                if (r == null || src == null || mf.gameObject.name.EndsWith(")", StringComparison.Ordinal) && mf.gameObject.name.Contains("(LOD")) continue;
                lod0.Add(r);
                string mat = r.sharedMaterial != null ? r.sharedMaterial.name : mf.gameObject.name;
                if (detail != null && detail(mat)) continue;
                float scale = MaxScale(mf.transform);
                bool cards = VhPerfMesh.IsCards(src);
                var m1 = cards ? VhPerfMesh.ThinCards(src, spec.lod1Cards, spec.lod1Grow) : VhPerfMesh.Simplify(src, spec.lod1Cell, scale);
                var m2 = cards ? (spec.dropCardsAtLod2 ? null : VhPerfMesh.ThinCards(src, spec.lod2Cards, spec.lod2Grow)) : VhPerfMesh.Simplify(src, spec.lod2Cell, scale);
                var r1 = MakeLevel(mf, m1, "LOD1", spec.lod1Shadows ? r.shadowCastingMode : ShadowCastingMode.Off);
                var r2 = MakeLevel(mf, m2, "LOD2", ShadowCastingMode.Off);
                if (r1 != null) lod1.Add(r1);
                if (r2 != null) lod2.Add(r2);
            }
            if (lod0.Count == 0) return null;
            var g = chunkRoot.GetComponent<LODGroup>(); if (g == null) g = chunkRoot.AddComponent<LODGroup>();   // not ??: a missing component is Unity's fake null
            g.fadeMode = LODFadeMode.CrossFade;
            g.animateCrossFading = true;
            g.SetLODs(new[] { new LOD(1f, lod0.ToArray()) });
            float size = GroupSize(g);
            float h1 = ScreenHeight(size, spec.lod1At), h2 = ScreenHeight(size, spec.lod2At);
            float hc = spec.cullAt > 0f ? ScreenHeight(size, spec.cullAt) : 0.0001f;
            g.SetLODs(new[]
            {
                new LOD(h1, lod0.ToArray()),
                new LOD(Mathf.Min(h2, h1 * 0.99f), lod1.ToArray()),
                new LOD(Mathf.Min(hc, h2 * 0.99f), lod2.ToArray()),
            });
            g.RecalculateBounds();
            return g;
        }

        static Renderer MakeLevel(MeshFilter source, Mesh mesh, string tag, ShadowCastingMode shadows)
        {
            if (mesh == null) return null;
            var go = new GameObject(source.gameObject.name + " (" + tag + ")") { layer = source.gameObject.layer };
            go.transform.SetParent(source.transform.parent, false);
            go.transform.localPosition = source.transform.localPosition;
            go.transform.localRotation = source.transform.localRotation;
            go.transform.localScale = source.transform.localScale;
            GameObjectUtility.SetStaticEditorFlags(go, GameObjectUtility.GetStaticEditorFlags(source.gameObject) & ~StaticEditorFlags.OccluderStatic);
            VhPerfMesh.Save(mesh, null, source.sharedMesh);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var src = source.GetComponent<MeshRenderer>();
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = src.sharedMaterials;
            r.shadowCastingMode = shadows;
            r.receiveShadows = src.receiveShadows;
            r.lightProbeUsage = src.lightProbeUsage;
            r.reflectionProbeUsage = src.reflectionProbeUsage;
            return r;
        }

        // ---------- One object ----------

        /// <summary>
        /// LODs for one object from its own meshes: LOD0 is what it has; each <paramref name="levels"/> entry adds a simplified
        /// copy of every mesh under it from that distance; <paramref name="cullAt"/> (m, 0: never) culls it. Extra
        /// renderers (an impostor from <see cref="VhPerfImpostor"/>) can be given for the last level in
        /// <paramref name="farRenderers"/>. Returns the group.
        /// </summary>
        public static LODGroup AddLods(GameObject root, Level[] levels, float cullAt = 0f, Renderer[] farRenderers = null)
        {
            if (root == null) return null;
            var lod0 = new List<Renderer>();
            foreach (var r in root.GetComponentsInChildren<Renderer>())
                if (!(r is ParticleSystemRenderer) && (farRenderers == null || Array.IndexOf(farRenderers, r) < 0)) lod0.Add(r);
            if (lod0.Count == 0) return null;
            var g = root.GetComponent<LODGroup>(); if (g == null) g = root.AddComponent<LODGroup>();   // not ??: a missing component is Unity's fake null
            g.fadeMode = LODFadeMode.CrossFade;
            g.animateCrossFading = true;
            g.SetLODs(new[] { new LOD(1f, lod0.ToArray()) });
            float size = GroupSize(g);
            var lods = new List<LOD>();
            float prevFrom = 0f;
            var current = lod0.ToArray();
            var filters = root.GetComponentsInChildren<MeshFilter>();
            levels = levels ?? new Level[0];
            for (int i = 0; i < levels.Length; i++)
            {
                lods.Add(new LOD(ScreenHeight(size, levels[i].fromMetres), current));
                var made = new List<Renderer>();
                foreach (var mf in filters)
                {
                    if (mf.sharedMesh == null || mf.GetComponent<MeshRenderer>() == null || lod0.IndexOf(mf.GetComponent<MeshRenderer>()) < 0) continue;
                    var m = VhPerfMesh.IsCards(mf.sharedMesh)
                        ? VhPerfMesh.ThinCards(mf.sharedMesh, 2 << i, 1.35f + 0.4f * i)
                        : VhPerfMesh.Simplify(mf.sharedMesh, levels[i].cell, MaxScale(mf.transform));
                    var r = MakeLevel(mf, m, "LOD" + (i + 1), levels[i].castShadows ? mf.GetComponent<MeshRenderer>().shadowCastingMode : ShadowCastingMode.Off);
                    if (r != null) made.Add(r);
                }
                if (i == levels.Length - 1 && farRenderers != null) made.AddRange(farRenderers);
                current = made.ToArray();
                prevFrom = levels[i].fromMetres;
            }
            float last = cullAt > 0f ? ScreenHeight(size, cullAt) : 0.0001f;
            lods.Add(new LOD(last, current));
            // Heights must fall from level to level.
            var arr = lods.ToArray();
            for (int i = 1; i < arr.Length; i++) arr[i].screenRelativeTransitionHeight = Mathf.Min(arr[i].screenRelativeTransitionHeight, arr[i - 1].screenRelativeTransitionHeight * 0.99f);
            g.SetLODs(arr);
            g.RecalculateBounds();
            _ = prevFrom;
            return g;
        }

        /// <summary>
        /// Culls <paramref name="go"/> once it is about as small on screen as it would be at <paramref name="metres"/> (a
        /// single-level LODGroup, no new meshes). Use on small props: cups, cans, toys, flowers, litter, small decals.
        /// Its shadow goes with it. Typical: 20-30 m for hand-sized things, 50-60 m for chair-sized, 120 m for car-sized.
        /// </summary>
        public static LODGroup CullBeyond(GameObject go, float metres)
        {
            if (go == null) return null;
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return null;
            var g = go.GetComponent<LODGroup>(); if (g == null) g = go.AddComponent<LODGroup>();   // not ??: a missing component is Unity's fake null
            g.fadeMode = LODFadeMode.CrossFade;
            g.animateCrossFading = true;
            g.SetLODs(new[] { new LOD(1f, rs) });
            float size = GroupSize(g);
            g.SetLODs(new[] { new LOD(ScreenHeight(size, metres), rs) });
            return g;
        }

        /// <summary>
        /// <see cref="CullBeyond"/> for every renderer under <paramref name="root"/> whose bounds are under
        /// <paramref name="maxSize"/> m (its largest side), at <paramref name="metresPerMetre"/> m of distance per metre of
        /// size (default: a 0.3 m cup goes at 24 m, a 1 m chair at 80 m). Skips anything already in a LODGroup. Returns
        /// how many it set.
        /// </summary>
        public static int CullSmall(Transform root, float maxSize = 1.2f, float metresPerMetre = 80f, float minMetres = 15f)
        {
            if (root == null) return 0;
            int n = 0;
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer || r.GetComponentInParent<LODGroup>() != null) continue;
                var s = r.bounds.size;
                float big = Mathf.Max(s.x, Mathf.Max(s.y, s.z));
                if (big <= 0f || big > maxSize) continue;
                if (CullBeyond(r.gameObject, Mathf.Max(minMetres, big * metresPerMetre)) != null) n++;
            }
            return n;
        }
    }
}
