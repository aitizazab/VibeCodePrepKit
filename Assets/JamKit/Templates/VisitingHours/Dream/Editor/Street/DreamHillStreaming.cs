using System;
using System.Collections.Generic;
using System.Globalization;
using JamKit.DeepBlock;
using JamKit.Look.EditorTools;
using JamKit.Perf.EditorTools;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace JamKit.DreamEscape.EditorTools
{
    /// <summary>
    /// How the hill is built within the memory cap (World round 2; docs/redesign/world.md "Memory and frame time"):
    /// <list type="bullet">
    /// <item><b>In bands.</b> The hill is drawn two chunk rows (80 m) at a time: each band's chunks are drawn, built, given
    /// their levels of detail (<see cref="HillLods"/>) and shadow rules, saved as one asset per chunk and unloaded, and the
    /// managed heap is collected, before the next band. Milestone 1 held every chunk's geometry, then every mesh, at once.</item>
    /// <item><b>Shared meshes.</b> Props are drawn once per kind, variant and size and placed as instances, each culled when
    /// it's too small to see (<see cref="Props"/>). Trees are the Look library's (<see cref="VhLookTrees"/>: four species,
    /// three variants each, shared meshes with LODs down to a billboard), except the climbable oak and the conifer.</item>
    /// <item><b>No graphics.</b> tools/street-capture.ps1 builds with -nographics: on DirectX 12 every mesh the build made was
    /// also uploaded through staging memory the batch editor never recycled (the 6.8 GB crash had only 0.7 GB in Unity's
    /// own allocators).</item>
    /// </list>
    /// </summary>
    public static partial class DreamStreetBuilder
    {
        /// <summary>Chunk rows drawn per band.</summary>
        public static int RowsPerBand = 2;

        struct ChunkRange { public int x0, x1, z0, z1; }

        static ChunkRange HillRange(LarchHill h)
        {
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var p in h.pieces)
            {
                if (p.kind == BlockKind.Boundary) continue;
                minX = Mathf.Min(minX, p.Min.X); maxX = Mathf.Max(maxX, p.Max.X);
                minZ = Mathf.Min(minZ, p.Min.Z); maxZ = Mathf.Max(maxZ, p.Max.Z);
            }
            return new ChunkRange { x0 = Chunks.Cell(minX - 2f), x1 = Chunks.Cell(maxX + 2f), z0 = Chunks.Cell(minZ - 2f), z1 = Chunks.Cell(maxZ + 2f) };
        }

        /// <summary>Draws the hill's chunks in <paramref name="range"/> under <paramref name="parent"/>, a band at a time.</summary>
        static int DrawHill(LarchHill h, PieceGrid grid, Transform parent, HillMeshStore store, HillBuildReport report, ChunkRange range, bool lods = true, bool unload = true)
        {
            var oak = h.trees.Find(t => t.kind == 4);
            var oakFoot = oak != null ? U(oak.foot) : Vector3.zero;
            var chunks = new Chunks();
            int built = 0;
            for (int z = range.z0; z <= range.z1; z += Mathf.Max(1, RowsPerBand))
            {
                int zTo = Mathf.Min(range.z1, z + Mathf.Max(1, RowsPerBand) - 1);
                chunks.Only(range.x0, range.x1, z, zTo);
                DrawGround(h, grid, chunks);
                DrawScatter(h, grid, chunks);
                DrawRoadMarkings(h, chunks);
                foreach (var hh in h.houses) if (chunks.Accepts(hh.center.X, hh.center.Z)) Safely("house " + hh.name, () => HillHouses.Draw(chunks.At(U(hh.center)), hh));
                foreach (var b in h.buildings)
                {
                    float cx = (b.min.X + b.max.X) * 0.5f, cz = (b.min.Y + b.max.Y) * 0.5f;
                    if (chunks.Accepts(cx, cz)) Safely("building " + b.name, () => HillLandmarks.Draw(chunks.At(cx, cz), b));
                }
                // The trees the Look library has no species for: the climbable oak (its limbs are the hill's colliders) and
                // the conifer.
                foreach (var t in h.trees)
                    if (!LookTree(t) && chunks.Accepts(t.foot.X, t.foot.Z)) Safely("tree " + t.name, () => HillTrees.Draw(chunks.At(U(t.foot)), t));
                DrawPieces(h, chunks, oakFoot);
                Safely("wires", () => DrawWires(h, chunks));
                DrawBunting(chunks);
                int n = 0;
                foreach (var m in chunks.All)
                {
                    if (m.Empty) continue;
                    BuildChunk(m, parent, store, lods);
                    n++;
                    report?.Sample();
                }
                chunks.Clear();
                store.Flush(unload);
                if (unload)
                {
                    GC.Collect();
                    EditorUtility.UnloadUnusedAssetsImmediate(false);
                }
                built += n;
                report?.Sample();
                Debug.Log("JAMKIT_STREET band z " + z + ".." + zTo + ": " + n + " chunks; commit " + (HillBuildReport.Commit() / (1024.0 * 1024 * 1024)).ToString("0.00", CultureInfo.InvariantCulture) + " GB");
            }
            return built;
        }

        static GameObject BuildChunk(HillMesh m, Transform parent, HillMeshStore store, bool lods)
        {
            store.Begin(m.name);
            var go = m.Build(parent, MeshFolder);
            if (lods) Safely("LODs of " + m.name, () => HillLods.Apply(go, store));
            VhPerfShadows.Apply(go.transform, VhPerfShadows.Rules.Outdoor);
            return go;
        }

        // ---------- Props: one mesh per kind, variant and size ----------

        static string PropKey(HillProp p) =>
            p.kind + "_" + p.variant + "_" + p.size.X.ToString("0.##", CultureInfo.InvariantCulture) + "x" + p.size.Y.ToString("0.##", CultureInfo.InvariantCulture)
            + "x" + p.size.Z.ToString("0.##", CultureInfo.InvariantCulture);

        sealed class PropKit
        {
            public Mesh mesh;
            public Material[] materials;
            public bool shadows;
        }

        /// <summary>The props as instances of shared meshes (drawn once per kind, variant and size), each culled at a
        /// distance that grows with its size. <paramref name="accept"/> filters by world position (null: all).</summary>
        static Transform Props(LarchHill h, Transform parent, HillMeshStore store, Func<float, float, bool> accept = null)
        {
            var root = new GameObject("Props (shared meshes)").transform;
            root.SetParent(parent, false);
            store.Begin("Props");
            var kits = new Dictionary<string, PropKit>();
            int placed = 0;
            foreach (var p in h.props)
            {
                if (accept != null && !accept(p.position.X, p.position.Z)) continue;
                string key = PropKey(p);
                if (!kits.TryGetValue(key, out var kit))
                {
                    kit = new PropKit();
                    kits[key] = kit;
                    var m = new HillMesh("Prop " + key);
                    var local = new HillProp { kind = p.kind, position = System.Numerics.Vector3.Zero, yaw = 0f, variant = p.variant, size = p.size, tag = p.tag };
                    try { HillProps.Draw(m, local); }
                    catch (Exception e) { Debug.LogWarning("JAMKIT_STREET couldn't draw prop " + key + ": " + e.Message); continue; }
                    kit.mesh = m.BuildShared("Prop " + key, out var ids);
                    if (kit.mesh == null) continue;
                    store.Add(kit.mesh);
                    kit.materials = new Material[ids.Length];
                    bool anyShadow = false;
                    for (int i = 0; i < ids.Length; i++)
                    {
                        kit.materials[i] = HillMaterials.Get(ids[i]);
                        var spec = HillMat.Find(ids[i]);
                        anyShadow |= spec == null || spec.shadows;
                    }
                    var s = U(p.Size);
                    kit.shadows = anyShadow && Mathf.Max(s.x, Mathf.Max(s.y, s.z)) >= 0.6f;
                }
                if (kit.mesh == null) continue;
                var go = new GameObject("Prop: " + p.kind + (string.IsNullOrEmpty(p.tag) ? "" : " (" + p.tag + ")"));
                go.transform.SetParent(root, false);
                int q = ((Mathf.RoundToInt(p.yaw / 90f) % 4) + 4) % 4;
                go.transform.SetPositionAndRotation(U(p.position), Quaternion.Euler(0f, q * 90f, 0f));
                go.AddComponent<MeshFilter>().sharedMesh = kit.mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterials = kit.materials;
                r.shadowCastingMode = kit.shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                r.lightProbeUsage = LightProbeUsage.BlendProbes;
                var size = U(p.Size);
                VhPerfLod.CullBeyond(go, Mathf.Clamp(Mathf.Max(size.x, Mathf.Max(size.y, size.z)) * 70f, 35f, 320f));
                placed++;
            }
            Debug.Log("JAMKIT_STREET props: " + placed + " placed from " + kits.Count + " shared meshes");
            return root;
        }

        // ---------- Trees: the Look library's ----------

        /// <summary>Kinds the Look library draws (broadleaf, tall, small); the climbable oak (4) and the conifer (3) are the hill's.</summary>
        static bool LookTree(HillTree t) => t.kind != 4 && t.kind != 3;

        static VhLookTrees.Species SpeciesOf(HillTree t)
        {
            switch (t.kind)
            {
                case 1: return t.seed % 3 == 0 ? VhLookTrees.Species.Birch : VhLookTrees.Species.Elm;
                case 2: return t.seed % 2 == 0 ? VhLookTrees.Species.Birch : VhLookTrees.Species.Maple;
                default: return t.seed % 2 == 0 ? VhLookTrees.Species.Maple : VhLookTrees.Species.Oak;
            }
        }

        /// <summary>The hill's trees from the Look library (shared meshes, LODs to a billboard). Their colliders are the
        /// core's trunk pieces (what you see is what you stand on), so the library's own trunk colliders are removed.</summary>
        static Transform Trees(LarchHill h, Transform parent, Func<float, float, bool> accept = null)
        {
            var root = new GameObject("Trees (the Look library's)").transform;
            root.SetParent(parent, false);
            int n = 0;
            foreach (var t in h.trees)
            {
                if (!LookTree(t) || accept != null && !accept(t.foot.X, t.foot.Z)) continue;
                GameObject go = null;
                try { go = VhLookTrees.Place(root, U(t.foot) + Vector3.down * 0.1f, SpeciesOf(t), Mathf.Abs(t.seed), t.height, false, Mathf.Repeat(t.seed * 47f, 360f)); }
                catch (Exception e) { Debug.LogWarning("JAMKIT_STREET couldn't place tree " + t.name + " (" + e.Message + "): drawn by the hill instead"); }
                if (go == null) continue;
                go.name = t.name + " (" + go.name + ")";
                foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                n++;
            }
            Debug.Log("JAMKIT_STREET trees: " + n + " from the Look library");
            return root;
        }

        // ---------- The world past the hill ----------

        /// <summary>The ground height the backdrop continues from past the hill's edges: the town falling away south of the
        /// far bank, the slopes rising with the hill east and west (milestone 1's backdrop ground); well under the hill inside.</summary>
        static float EdgeHeight(float x, float z)
        {
            if (z < LarchHill.SouthZ) return LarchHill.FarRiseY - (LarchHill.SouthZ - z) * 0.08f;
            return Mathf.Clamp((z + 20f) * 0.13f, 0f, 46f) - 4f;
        }

        /// <summary>The Look library's terrain ring round the hill (<see cref="VhLookBackdrop"/>): farmland, wooded ridges,
        /// stands of trees, a tree line; the far haze dissolves it into the sky.</summary>
        static GameObject Backdrop(LarchHill h, Transform parent)
        {
            var hole = new Rect(LarchHill.WestX, LarchHill.SouthZ, LarchHill.EastX - LarchHill.WestX, LarchHill.NorthZ - LarchHill.SouthZ);
            float Near(float x, float z) => hole.Contains(new Vector2(x, z)) ? -7.85f : EdgeHeight(x, z);
            var go = VhLookBackdrop.Build(parent, new VhLookBackdrop.Options
            {
                center = new Vector3(0f, 0f, (LarchHill.SouthZ + LarchHill.NorthZ) * 0.5f),
                innerRadius = 330f,
                outerRadius = 2800f,
                nearHeight = Near,
                hole = hole,
                ridgeHeight = 200f,
            });
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            return go;
        }

        /// <summary>Saves the meshes under <paramref name="root"/> that live only in the scene (the Look builders' tufts,
        /// ring, canopy) into one asset, so the scene file stays small and they can be unloaded.</summary>
        static int PersistMeshes(Transform root, HillMeshStore store, string container)
        {
            if (root == null) return 0;
            store.Begin(container);
            var done = new HashSet<int>();
            int n = 0;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var m = mf.sharedMesh;
                if (m == null || EditorUtility.IsPersistent(m) || !done.Add(m.GetInstanceID())) continue;
                long idx = 0;
                for (int s = 0; s < m.subMeshCount; s++) idx += m.GetIndexCount(s);
                HillMeshStore.Count(m.vertexCount, (int)idx, m.indexFormat == IndexFormat.UInt16);
                store.Add(m);
                n++;
            }
            return n;
        }

        /// <summary>Static for occlusion (occludee; big solids occluders) and reflection probes, through the Perf agent's
        /// MarkStatic, but not batching-static: the chunks are merged already and static batching would copy every shared
        /// prop and tree mesh once per instance into the build.</summary>
        static void MarkStatic(Transform root)
        {
            if (root == null) return;
            VhPerfBatching.MarkStatic(root);
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
                GameObjectUtility.SetStaticEditorFlags(r.gameObject, GameObjectUtility.GetStaticEditorFlags(r.gameObject) & ~StaticEditorFlags.BatchingStatic);
        }

        // ---------- The house's surroundings (for the House agent's own scene) ----------

        /// <summary>
        /// Larch Hill round the Lasnames' lot, for the House agent's DreamHouse scene (their judge, round 1: the lot ended in a
        /// cut dirt edge over a void, and Chapel Row was an empty plane). Draws every hill chunk within
        /// <paramref name="radius"/> m of the house (the ground, its walls and steps, the neighbours' houses, the props, the
        /// trees, Rowan Street below and Chapel Row above with its rows of houses) plus the colliders of the hill's pieces
        /// there, and the terrain ring round everything, in the house's own frame (the house root at <paramref name="root"/>'s
        /// origin, i.e. everything shifted by -HillAnchors.HouseOrigin). Inside the Lasnames' lot (HillAnchors.LotX0..LotZ1) the
        /// hill draws the ground, its fences and gates, as on the hill; only the house's stand-in (style "lasname_house") is
        /// left out, since the house builder's own house stands there.
        /// Call by name: <c>JamKit.DreamEscape.EditorTools.DreamStreetBuilder.BuildSurroundings(Transform, float)</c>.
        /// </summary>
        public static GameObject BuildSurroundings(Transform root, float radius = 140f)
        {
            var h = LarchHill.Build();
            h.pieces.RemoveAll(p => p.style == "lasname_house");
            var origin = new Vector3(h.houseOrigin.X, h.houseOrigin.Y, h.houseOrigin.Z);
            var hill = new GameObject("Larch Hill round the house (World's)");
            hill.transform.SetParent(root, false);
            hill.transform.localPosition = -origin;
            var colliders = new GameObject("Colliders").transform;
            colliders.SetParent(hill.transform, false);
            var drawn = new GameObject("Drawn").transform;
            drawn.SetParent(hill.transform, false);
            if (AssetDatabase.IsValidFolder(SurroundingsFolder)) AssetDatabase.DeleteAsset(SurroundingsFolder);
            EnsureFolder(SurroundingsFolder);
            var store = new HillMeshStore(SurroundingsFolder);
            var before = HillMeshStore.Current;
            HillMeshStore.Current = store;
            try
            {
                var range = new ChunkRange
                {
                    x0 = Chunks.Cell(origin.x - radius), x1 = Chunks.Cell(origin.x + radius),
                    z0 = Chunks.Cell(origin.z - radius), z1 = Chunks.Cell(origin.z + radius),
                };
                bool Near(float x, float z) => Chunks.Cell(x) >= range.x0 && Chunks.Cell(x) <= range.x1 && Chunks.Cell(z) >= range.z0 && Chunks.Cell(z) <= range.z1;
                var lotless = h;
                store.Begin("Colliders");
                foreach (var p in lotless.pieces)
                    if (p.solid && p.kind != BlockKind.Boundary && Near(p.center.X, p.center.Z) && p.switchIndex < 0) Collider(p, colliders);
                var grid = new PieceGrid(lotless.pieces);
                DrawHill(lotless, grid, drawn, store, null, range, true, false);
                Props(lotless, hill.transform, store, Near);
                Trees(lotless, hill.transform, Near);
                var grass = DrawTufts(lotless, grid, hill.transform, new Rect(origin.x - radius, origin.z - radius, radius * 2f, radius * 2f));
                PersistMeshes(grass.transform, store, "Grass");
                var ring = Backdrop(lotless, hill.transform);
                PersistMeshes(ring.transform, store, "Backdrop");
                store.Flush(false);
                MarkStatic(drawn);
                Debug.Log("JAMKIT_STREET_SURROUNDINGS built round the house: " + HillMeshStore.Meshes + " meshes so far");
            }
            finally { HillMeshStore.Current = before; }
            return hill;
        }

        /// <summary>The surroundings' generated meshes (the House agent's scene build writes them; World owns the folder).</summary>
        public const string SurroundingsFolder = HillMaterials.StreetRoot + "/Generated/HouseSurroundings";

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
