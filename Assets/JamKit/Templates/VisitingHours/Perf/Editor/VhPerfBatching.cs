using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace JamKit.Perf.EditorTools
{
    /// <summary>
    /// Draw-call tools (docs/redesign/perf.md "Draw calls"). With the SRP Batcher on, a draw call is cheap when its shader
    /// variant matches the last one; what costs is renderer count (culling, per-object data) and variant switches. So: merge
    /// many small static renderers that share a material into one per chunk and distance band (culling still works per
    /// chunk), keep big or LOD'd objects separate, and mark static what doesn't move.
    /// <code>
    /// VhPerfBatching.MarkStatic(propsRoot);                                  // batching + occlusion + probes, not GI
    /// VhPerfBatching.MergeByMaterial(propsRoot, 40f, "Assets/.../Generated/Merged", maxSize: 2f); // small props, per 40 m chunk
    /// </code>
    /// </summary>
    public static class VhPerfBatching
    {
        public const StaticEditorFlags RenderStatic = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic;

        /// <summary>Marks every renderer under <paramref name="root"/> static for batching, occlusion (as an occludee; big
        /// solid things also as occluders) and reflection probes. Lightmap/GI static is left as it was (the Look agent's bake
        /// decides). Skip anything that moves.</summary>
        public static int MarkStatic(Transform root, float occluderMinSize = 3f)
        {
            if (root == null) return 0;
            int n = 0;
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var go = r.gameObject;
                var flags = GameObjectUtility.GetStaticEditorFlags(go) | RenderStatic;
                var s = r.bounds.size;
                bool solid = r.sharedMaterial != null && r.sharedMaterial.renderQueue < (int)RenderQueue.AlphaTest;
                if (solid && Mathf.Min(s.x, Mathf.Min(s.y, s.z)) > 0.15f && Mathf.Max(s.x, Mathf.Max(s.y, s.z)) >= occluderMinSize) flags |= StaticEditorFlags.OccluderStatic;
                GameObjectUtility.SetStaticEditorFlags(go, flags);
                n++;
            }
            return n;
        }

        /// <summary>
        /// Merges the static MeshRenderers under <paramref name="root"/> no bigger than <paramref name="maxSize"/> m, grouped
        /// by (chunk of <paramref name="chunk"/> m, material, shadow mode, layer), into one renderer per group under
        /// "<paramref name="root"/>/Merged"; the originals' renderers are disabled (colliders and scripts stay). Meshes are
        /// saved under <paramref name="assetFolder"/> (compacted, not readable in builds). Returns renderers merged.
        /// </summary>
        public static int MergeByMaterial(Transform root, float chunk, string assetFolder, float maxSize = 2f)
        {
            if (root == null) return 0;
            var groups = new Dictionary<string, List<MeshRenderer>>();
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>())
            {
                if (!r.enabled || r.GetComponentInParent<LODGroup>() != null) continue;
                if (!r.TryGetComponent<MeshFilter>(out var mf) || mf.sharedMesh == null || r.sharedMaterials.Length != 1) continue;
                if (mf.sharedMesh.subMeshCount != 1) continue;
                var b = r.bounds;
                if (Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)) > maxSize) continue;
                int cx = Mathf.FloorToInt(b.center.x / chunk), cz = Mathf.FloorToInt(b.center.z / chunk);
                string key = cx + "_" + cz + "|" + r.sharedMaterial.GetInstanceID() + "|" + (int)r.shadowCastingMode + "|" + r.gameObject.layer;
                if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<MeshRenderer>();
                list.Add(r);
            }
            var parent = new GameObject("Merged").transform;
            parent.SetParent(root, false);
            int merged = 0;
            foreach (var kv in groups)
            {
                if (kv.Value.Count < 2) continue;
                var combine = new List<CombineInstance>();
                int verts = 0;
                foreach (var r in kv.Value)
                {
                    var mesh = r.GetComponent<MeshFilter>().sharedMesh;
                    combine.Add(new CombineInstance { mesh = mesh, transform = r.localToWorldMatrix });
                    verts += mesh.vertexCount;
                }
                var m = new Mesh { name = "Merged " + kv.Key.Split('|')[0] + " " + kv.Value[0].sharedMaterial.name, indexFormat = verts > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                m.CombineMeshes(combine.ToArray(), true, true, false);
                m.RecalculateBounds();
                if (!string.IsNullOrEmpty(assetFolder)) VhPerfMesh.Save(m, assetFolder + "/" + Safe(m.name) + ".asset");
                var go = new GameObject(m.name) { layer = kv.Value[0].gameObject.layer };
                go.transform.SetParent(parent, true);
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                go.transform.localScale = Vector3.one;
                go.AddComponent<MeshFilter>().sharedMesh = m;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = kv.Value[0].sharedMaterial;
                mr.shadowCastingMode = kv.Value[0].shadowCastingMode;
                mr.receiveShadows = kv.Value[0].receiveShadows;
                mr.lightProbeUsage = kv.Value[0].lightProbeUsage;
                GameObjectUtility.SetStaticEditorFlags(go, RenderStatic);
                foreach (var r in kv.Value) r.enabled = false;
                merged += kv.Value.Count;
            }
            return merged;
        }

        static string Safe(string s)
        {
            var c = s.ToCharArray();
            for (int i = 0; i < c.Length; i++) if (!char.IsLetterOrDigit(c[i]) && c[i] != '_' && c[i] != '-') c[i] = '_';
            return new string(c);
        }
    }
}
