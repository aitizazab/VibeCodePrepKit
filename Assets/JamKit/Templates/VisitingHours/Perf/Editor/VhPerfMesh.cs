using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace JamKit.Perf.EditorTools
{
    /// <summary>
    /// Mesh tools for builders (docs/redesign/perf.md "API"):
    /// <list type="bullet">
    /// <item><see cref="Simplify"/>: a crack-free vertex-clustering simplifier for far levels of procedural meshes (merged
    /// chunks, houses, props): vertices in the same grid cell share one position (so faces meeting there stay joined),
    /// while normals and UVs are kept per face direction (hard edges stay hard).</item>
    /// <item><see cref="ThinCards"/>: for meshes of alpha cards (grass, flowers, leaf clusters: four vertices, two triangles
    /// each): keeps one card in N, grown so the cover stays about the same.</item>
    /// <item><see cref="Compact"/>: a smaller vertex layout (half-precision normals and tangents: 36 bytes a vertex instead of
    /// 48), a quarter less memory and disk for every generated mesh.</item>
    /// <item><see cref="MarkNotReadable"/>: drops the CPU copy of a saved mesh in builds (half the mesh memory).</item>
    /// </list>
    /// </summary>
    public static class VhPerfMesh
    {
        // ---------- Simplify ----------

        static readonly Vector3[] Directions =
        {
            new Vector3(1, 0, 0), new Vector3(-1, 0, 0), new Vector3(0, 1, 0), new Vector3(0, -1, 0), new Vector3(0, 0, 1), new Vector3(0, 0, -1),
            new Vector3(1, 1, 1).normalized, new Vector3(1, 1, -1).normalized, new Vector3(1, -1, 1).normalized, new Vector3(1, -1, -1).normalized,
            new Vector3(-1, 1, 1).normalized, new Vector3(-1, 1, -1).normalized, new Vector3(-1, -1, 1).normalized, new Vector3(-1, -1, -1).normalized,
        };

        static int Bucket(Vector3 n)
        {
            int best = 0;
            float bd = float.MinValue;
            for (int i = 0; i < Directions.Length; i++)
            {
                float d = Vector3.Dot(n, Directions[i]);
                if (d > bd) { bd = d; best = i; }
            }
            return best;
        }

        /// <summary>
        /// A simplified copy of <paramref name="source"/>: vertices snapped to a grid of <paramref name="cell"/> metres (in the
        /// mesh's own space; pass the object's lossy scale in <paramref name="scale"/> when it isn't 1). Triangles that collapse
        /// are dropped. Returns null when nothing is left (the whole mesh is smaller than a cell: cull it at that level).
        /// Sub-meshes are kept (one material each).
        /// </summary>
        public static Mesh Simplify(Mesh source, float cell, float scale = 1f, string name = null)
        {
            if (source == null || cell <= 0f) return null;
            var pos = source.vertices;
            if (pos.Length == 0) return null;
            var nrm = source.normals;
            bool hasNormals = nrm != null && nrm.Length == pos.Length;
            var uv = new List<Vector2>();
            source.GetUVs(0, uv);
            bool hasUv = uv.Count == pos.Length;
            float inv = scale / cell;

            // Cells: every vertex's grid cell; the cell's position is the mean of all its vertices.
            var cellOf = new int[pos.Length];
            var cellIds = new Dictionary<long, int>(pos.Length / 2 + 1);
            var cellSum = new List<Vector3>();
            var cellCount = new List<int>();
            for (int i = 0; i < pos.Length; i++)
            {
                var p = pos[i] * inv;
                long key = Key(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y), Mathf.FloorToInt(p.z));
                if (!cellIds.TryGetValue(key, out int c)) { c = cellSum.Count; cellIds[key] = c; cellSum.Add(Vector3.zero); cellCount.Add(0); }
                cellSum[c] += pos[i];
                cellCount[c]++;
                cellOf[i] = c;
            }

            // Clusters: (cell, face direction); normal and UV averaged within.
            var clusterOf = new int[pos.Length];
            var clusterIds = new Dictionary<long, int>(pos.Length / 2 + 1);
            var cCell = new List<int>();
            var cNormal = new List<Vector3>();
            var cUv = new List<Vector2>();
            var cCount = new List<int>();
            for (int i = 0; i < pos.Length; i++)
            {
                var n = hasNormals ? nrm[i] : Vector3.up;
                long key = (long)cellOf[i] * 16 + Bucket(n);
                if (!clusterIds.TryGetValue(key, out int k))
                {
                    k = cCell.Count;
                    clusterIds[key] = k;
                    cCell.Add(cellOf[i]); cNormal.Add(Vector3.zero); cUv.Add(Vector2.zero); cCount.Add(0);
                }
                cNormal[k] += n;
                if (hasUv) cUv[k] += uv[i];
                cCount[k]++;
                clusterOf[i] = k;
            }

            // Triangles: keep those whose three corners are in three different cells.
            int subs = source.subMeshCount;
            var tris = new List<int>[subs];
            var used = new int[cCell.Count];
            for (int i = 0; i < used.Length; i++) used[i] = -1;
            var outPos = new List<Vector3>();
            var outNrm = new List<Vector3>();
            var outUv = new List<Vector2>();
            var seen = new HashSet<long>();
            int kept = 0;
            for (int s = 0; s < subs; s++)
            {
                tris[s] = new List<int>();
                var t = source.GetTriangles(s);
                seen.Clear();
                for (int i = 0; i + 2 < t.Length; i += 3)
                {
                    int a = clusterOf[t[i]], b = clusterOf[t[i + 1]], c = clusterOf[t[i + 2]];
                    if (cCell[a] == cCell[b] || cCell[b] == cCell[c] || cCell[a] == cCell[c]) continue;
                    // The same triangle twice (two faces that collapsed onto each other): once is enough.
                    int lo = Mathf.Min(a, Mathf.Min(b, c)), hi = Mathf.Max(a, Mathf.Max(b, c)), mid = a + b + c - lo - hi;
                    long tk = ((long)lo * 2097152L + mid) * 2097152L + hi;
                    if (!seen.Add(tk)) continue;
                    tris[s].Add(Emit(a)); tris[s].Add(Emit(b)); tris[s].Add(Emit(c));
                    kept++;
                }
            }
            if (kept == 0) return null;

            int Emit(int k)
            {
                if (used[k] >= 0) return used[k];
                int c = cCell[k];
                used[k] = outPos.Count;
                outPos.Add(cellSum[c] / cellCount[c]);
                var n = cNormal[k];
                outNrm.Add(n.sqrMagnitude > 1e-12f ? n.normalized : Vector3.up);
                outUv.Add(hasUv ? cUv[k] / cCount[k] : Vector2.zero);
                return used[k];
            }

            var mesh = new Mesh { name = name ?? source.name + " (simplified " + cell.ToString("0.##") + " m)" };
            if (outPos.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(outPos);
            mesh.SetNormals(outNrm);
            if (hasUv) mesh.SetUVs(0, outUv);
            mesh.subMeshCount = subs;
            for (int s = 0; s < subs; s++) mesh.SetTriangles(tris[s], s, false);
            mesh.RecalculateBounds();
            if (hasUv && source.tangents != null && source.tangents.Length == pos.Length) mesh.RecalculateTangents();
            return mesh;
        }

        static long Key(int x, int y, int z) => ((long)(x + 1048576) * 2097152L + (y + 1048576)) * 2097152L + (z + 1048576);

        // ---------- Cards ----------

        /// <summary>True when every triangle pair is a four-vertex card (HillMesh.Card / PolyUV quads, leaf crowns).</summary>
        public static bool IsCards(Mesh mesh)
        {
            if (mesh == null || mesh.subMeshCount != 1) return false;
            var t = mesh.triangles;
            if (t.Length == 0 || t.Length % 6 != 0) return false;
            for (int i = 0; i < t.Length; i += 6)
            {
                int b0 = Mathf.Min(Mathf.Min(t[i], t[i + 1]), t[i + 2]);
                for (int k = 0; k < 6; k++) if (t[i + k] < b0 || t[i + k] > b0 + 3) return false;
            }
            return true;
        }

        /// <summary>
        /// A copy keeping one card in <paramref name="keepOneIn"/> (chosen evenly, not by position), each grown by
        /// <paramref name="grow"/> about its own centre so the cover stays about the same. Null if <paramref name="source"/>
        /// isn't all cards (see <see cref="IsCards"/>).
        /// </summary>
        public static Mesh ThinCards(Mesh source, int keepOneIn, float grow, string name = null)
        {
            if (!IsCards(source) || keepOneIn < 1) return null;
            var pos = source.vertices;
            var nrm = source.normals;
            var tan = source.tangents;
            var uv = new List<Vector2>();
            source.GetUVs(0, uv);
            var t = source.triangles;
            var oPos = new List<Vector3>(); var oNrm = new List<Vector3>(); var oTan = new List<Vector4>(); var oUv = new List<Vector2>(); var oTri = new List<int>();
            var map = new Dictionary<int, int>();
            int card = 0;
            for (int i = 0; i < t.Length; i += 6, card++)
            {
                // A stable spread: keep card 0, N, 2N... after a fixed shuffle of the order (so a row of grass doesn't lose a side).
                if ((uint)(card * 2654435761u) % (uint)keepOneIn != 0) continue;
                map.Clear();
                var centre = Vector3.zero;
                int b0 = Mathf.Min(Mathf.Min(t[i], t[i + 1]), t[i + 2]);
                for (int v = 0; v < 4; v++) centre += pos[b0 + v];
                centre *= 0.25f;
                for (int k = 0; k < 6; k++)
                {
                    int src = t[i + k];
                    if (!map.TryGetValue(src, out int dst))
                    {
                        dst = oPos.Count;
                        map[src] = dst;
                        oPos.Add(centre + (pos[src] - centre) * grow);
                        if (nrm != null && nrm.Length == pos.Length) oNrm.Add(nrm[src]);
                        if (tan != null && tan.Length == pos.Length) oTan.Add(tan[src]);
                        if (uv.Count == pos.Length) oUv.Add(uv[src]);
                    }
                    oTri.Add(dst);
                }
            }
            if (oTri.Count == 0) return null;
            var mesh = new Mesh { name = name ?? source.name + " (1 card in " + keepOneIn + ")" };
            if (oPos.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(oPos);
            if (oNrm.Count == oPos.Count) mesh.SetNormals(oNrm);
            if (oTan.Count == oPos.Count) mesh.SetTangents(oTan);
            if (oUv.Count == oPos.Count) mesh.SetUVs(0, oUv);
            mesh.SetTriangles(oTri, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // ---------- Memory and disk ----------

        /// <summary>
        /// Re-lays <paramref name="mesh"/>'s vertices compactly: position 32-bit, normal and tangent 16-bit (half), UV0 and UV1
        /// 32-bit (world-metre UVs need the precision). 48 bytes a vertex become 36; the same on disk. Call before
        /// AssetDatabase.CreateAsset. Colours and other channels are dropped only if absent. On any failure the mesh is left
        /// as it was (a warning is logged).
        /// </summary>
        public static Mesh Compact(Mesh mesh)
        {
            if (mesh == null || mesh.vertexCount == 0) return mesh;
            if (mesh.blendShapeCount > 0 || mesh.bindposeCount > 0) return mesh;   // skinned / morphing meshes keep theirs
            var original = mesh.GetVertexAttributes();
            Vector3[] keepV = null, keepN = null;
            Vector4[] keepT = null;
            var keepUv = new List<Vector2>();
            try
            {
                keepV = mesh.vertices;
                keepN = mesh.normals;
                keepT = mesh.tangents;
                mesh.GetUVs(0, keepUv);
                var v = mesh.vertices;
                var n = mesh.normals;
                var t = mesh.tangents;
                var c = mesh.colors32;
                var uv0 = new List<Vector2>();
                var uv1 = new List<Vector2>();
                mesh.GetUVs(0, uv0);
                mesh.GetUVs(1, uv1);
                int count = v.Length;
                bool hasN = n != null && n.Length == count, hasT = t != null && t.Length == count, hasC = c != null && c.Length == count;
                bool hasUv0 = uv0.Count == count, hasUv1 = uv1.Count == count;
                var layout = new List<VertexAttributeDescriptor> { new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3) };
                if (hasN) layout.Add(new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float16, 4));
                if (hasT) layout.Add(new VertexAttributeDescriptor(VertexAttribute.Tangent, VertexAttributeFormat.Float16, 4));
                if (hasC) layout.Add(new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4));
                if (hasUv0) layout.Add(new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2));
                if (hasUv1) layout.Add(new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 2));
                mesh.SetVertexBufferParams(count, layout.ToArray());
                mesh.vertices = v;
                if (hasN) mesh.normals = n;
                if (hasT) mesh.tangents = t;
                if (hasC) mesh.colors32 = c;
                if (hasUv0) mesh.SetUVs(0, uv0);
                if (hasUv1) mesh.SetUVs(1, uv1);
                mesh.RecalculateBounds();
            }
            catch (Exception e)
            {
                Debug.LogWarning("JAMKIT_PERF couldn't compact " + mesh.name + " (put back as it was): " + e.Message);
                try
                {
                    mesh.SetVertexBufferParams(keepV != null ? keepV.Length : mesh.vertexCount, original);
                    if (keepV != null) mesh.vertices = keepV;
                    if (keepN != null && keepN.Length == keepV.Length) mesh.normals = keepN;
                    if (keepT != null && keepT.Length == keepV.Length) mesh.tangents = keepT;
                    if (keepV != null && keepUv.Count == keepV.Length) mesh.SetUVs(0, keepUv);
                    mesh.RecalculateBounds();
                }
                catch (Exception e2) { Debug.LogError("JAMKIT_PERF couldn't restore " + mesh.name + ": " + e2.Message); }
            }
            return mesh;
        }

        /// <summary>
        /// Marks a saved mesh asset not readable: the player uploads it to the GPU and frees the CPU copy (half its memory in
        /// a build). The editor can still read it, but <c>mesh.isReadable</c> turns false, so don't use this on meshes that an
        /// editor tool raycasts through mesh data (World's geometry lint reads readable meshes only).
        /// </summary>
        public static void MarkNotReadable(Mesh mesh)
        {
            if (mesh == null || !EditorUtility.IsPersistent(mesh)) return;
            var so = new SerializedObject(mesh);
            var p = so.FindProperty("m_IsReadable");
            if (p == null || !p.boolValue) return;
            p.boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(mesh);
        }

        /// <summary>Saves a generated mesh: as a sub-asset of <paramref name="container"/>'s asset when that is an asset (no
        /// new file), else as its own file at <paramref name="path"/>. Compacted first.</summary>
        public static Mesh Save(Mesh mesh, string path, UnityEngine.Object container = null, bool notReadable = true)
        {
            if (mesh == null) return null;
            Compact(mesh);
            string containerPath = container != null ? AssetDatabase.GetAssetPath(container) : null;
            if (!string.IsNullOrEmpty(containerPath)) AssetDatabase.AddObjectToAsset(mesh, containerPath);
            else if (!string.IsNullOrEmpty(path)) AssetDatabase.CreateAsset(mesh, AssetDatabase.GenerateUniqueAssetPath(path));
            if (notReadable) MarkNotReadable(mesh);
            return mesh;
        }

        /// <summary>Bytes a mesh takes (vertex buffer + index buffer), CPU or GPU copy each.</summary>
        public static long Bytes(Mesh mesh)
        {
            if (mesh == null) return 0;
            long vb = 0;
            for (int s = 0; s < mesh.vertexBufferCount; s++) vb += (long)mesh.GetVertexBufferStride(s) * mesh.vertexCount;
            long ib = 0;
            for (int s = 0; s < mesh.subMeshCount; s++) ib += (long)mesh.GetIndexCount(s) * (mesh.indexFormat == IndexFormat.UInt32 ? 4 : 2);
            return vb + ib;
        }
    }
}
