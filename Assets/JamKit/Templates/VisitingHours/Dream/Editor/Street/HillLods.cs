using System.Collections.Generic;
using JamKit.Perf.EditorTools;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace JamKit.DreamEscape.EditorTools
{
    /// <summary>
    /// Levels of detail for a chunk of the hill (World round 2; the creator: "there's no way the tree in the garden is the
    /// same as a tree you can barely see the branches of behind the hospital"). Built on the Perf agent's mesh tools
    /// (<see cref="VhPerfMesh.Simplify"/>, <see cref="VhPerfMesh.ThinCards"/>, <see cref="VhPerfLod.ScreenHeight"/>), but
    /// sharing renderers between levels so a chunk gains at most two renderers per heavy material (Perf's ChunkLods adds two
    /// for every material, tripling the hill's object count):
    /// <list type="bullet">
    /// <item>decals, flowers and anything small (under <see cref="SmallVertices"/> vertices): full detail only;</item>
    /// <item>cards (grass, leaves, ivy): one card in three, half as big again, at LOD1; gone at LOD2;</item>
    /// <item>solids: a 0.2 m vertex-clustered copy at LOD1 and a 0.9 m one at LOD2 when the mesh is big enough to gain
    /// (<see cref="SimplifyFrom"/> vertices); the full mesh otherwise. Only LOD0 and big LOD1 solids cast shadows.</item>
    /// </list>
    /// Distances in metres from the chunk (<see cref="Lod1At"/>, <see cref="Lod2At"/>, <see cref="CullAt"/>): tune them
    /// against the captures and the probe. The generated meshes go into the chunk's own asset, compacted, not readable.
    /// </summary>
    public static class HillLods
    {
        public static float Lod1At = 70f, Lod2At = 190f, CullAt = 0f;
        public static float Lod1Cell = 0.2f, Lod2Cell = 0.9f;
        public static int SmallVertices = 300, SimplifyFrom = 800;
        /// <summary>Material ids (HillMat) drawn at full detail only.</summary>
        static readonly HashSet<string> NearOnly = new HashSet<string> { HillMat.Flowers, HillMat.Cracks, HillMat.Stains, HillMat.Leaflets, HillMat.Manhole, HillMat.Drain };

        public static int Made;

        /// <summary>The material id of a hill renderer ("Hill_brick_red" or a Look override named by the id).</summary>
        public static string IdOf(Material m)
        {
            if (m == null) return "";
            string n = m.name;
            return n.StartsWith("Hill_", System.StringComparison.Ordinal) ? n.Substring(5) : n;
        }

        public static LODGroup Apply(GameObject chunkRoot, HillMeshStore store)
        {
            var lod0 = new List<Renderer>();
            var lod1 = new List<Renderer>();
            var lod2 = new List<Renderer>();
            foreach (var mf in chunkRoot.GetComponentsInChildren<MeshFilter>())
            {
                var r = mf.GetComponent<MeshRenderer>();
                var mesh = mf.sharedMesh;
                if (r == null || mesh == null) continue;
                lod0.Add(r);
                string id = IdOf(r.sharedMaterial);
                var spec = HillMat.Find(id);
                int nv = mesh.vertexCount;
                if (NearOnly.Contains(id) || spec != null && spec.kind == HillMat.Kind.Decal) continue;
                if (spec != null && spec.kind == HillMat.Kind.Cutout)
                {
                    var thin = nv >= SmallVertices ? VhPerfMesh.ThinCards(mesh, 3, 1.5f) : null;
                    if (thin != null) lod1.Add(Level(mf, thin, "LOD1", ShadowCastingMode.Off, store));
                    else lod1.Add(r);
                    continue;
                }
                if (nv < SmallVertices && mesh.bounds.size.magnitude < 3f) { lod1.Add(r); continue; }
                if (nv >= SimplifyFrom)
                {
                    var m1 = VhPerfMesh.Simplify(mesh, Lod1Cell);
                    var m2 = VhPerfMesh.Simplify(mesh, Lod2Cell);
                    if (m1 != null && m1.vertexCount < nv * 0.85f) lod1.Add(Level(mf, m1, "LOD1", r.shadowCastingMode, store));
                    else { if (m1 != null) Object.DestroyImmediate(m1); lod1.Add(r); }
                    if (m2 != null) lod2.Add(Level(mf, m2, "LOD2", ShadowCastingMode.Off, store));
                }
                else { lod1.Add(r); lod2.Add(r); }
            }
            if (lod0.Count == 0) return null;
            var g = chunkRoot.GetComponent<LODGroup>();
            if (g == null) g = chunkRoot.AddComponent<LODGroup>();
            g.fadeMode = LODFadeMode.CrossFade;
            g.animateCrossFading = true;
            g.SetLODs(new[] { new LOD(0.5f, lod0.ToArray()) });
            g.RecalculateBounds();
            float size = Mathf.Max(1f, g.size);
            float h1 = VhPerfLod.ScreenHeight(size, Lod1At), h2 = Mathf.Min(VhPerfLod.ScreenHeight(size, Lod2At), h1 * 0.98f);
            float hc = CullAt > 0f ? Mathf.Min(VhPerfLod.ScreenHeight(size, CullAt), h2 * 0.98f) : 0.0002f;
            g.SetLODs(new[] { new LOD(h1, lod0.ToArray()), new LOD(h2, lod1.ToArray()), new LOD(hc, lod2.ToArray()) });
            g.RecalculateBounds();
            return g;
        }

        static Renderer Level(MeshFilter source, Mesh mesh, string tag, ShadowCastingMode shadows, HillMeshStore store)
        {
            mesh.name = source.sharedMesh.name + " (" + tag + ")";
            VhPerfMesh.Save(mesh, null, source.sharedMesh);
            store?.Track(mesh);
            Made++;
            HillMeshStore.Count(mesh.vertexCount, (int)mesh.GetIndexCount(0), mesh.indexFormat == IndexFormat.UInt16);
            var go = new GameObject(source.gameObject.name + " (" + tag + ")") { layer = source.gameObject.layer };
            go.transform.SetParent(source.transform.parent, false);
            go.transform.localPosition = source.transform.localPosition;
            go.transform.localRotation = source.transform.localRotation;
            go.transform.localScale = source.transform.localScale;
            GameObjectUtility.SetStaticEditorFlags(go, GameObjectUtility.GetStaticEditorFlags(source.gameObject) & ~StaticEditorFlags.OccluderStatic);
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
    }
}
