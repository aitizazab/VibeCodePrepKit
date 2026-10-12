using System;
using System.Collections.Generic;
using JamKit.DeepBlock;
using JamKit.Look.EditorTools;
using UnityEngine;
using Random = System.Random;

namespace JamKit.DreamEscape.EditorTools
{
    /// <summary>
    /// The hill's drawing (docs/redesign/world.md "How the hill is drawn"): the ground tiles' tops (grass, paving, asphalt,
    /// cobbles, steps) and their exposed sides (stone retaining walls with copings, kerbs), the scatter on them (grass
    /// tufts, flowers, fallen leaves, cracks, manholes, drains), road markings, the party's things and the other pieces
    /// with their own style, and the backdrop past the edges. Everything goes into 40 m chunks (<see cref="HillMesh"/>),
    /// one renderer per material per chunk. Houses, buildings, props, trees and fences are the kit's (HillHouses,
    /// HillLandmarks, HillProps, HillTrees, HillFences).
    /// </summary>
    public static partial class DreamStreetBuilder
    {
        const float ChunkSize = 40f;

        /// <summary>
        /// The hill's 40 m chunks, drawn a few at a time (World round 2, the memory fix): <see cref="Only"/> limits drawing to
        /// a range of chunks (a band of rows for the scene build, an area for the house's surroundings); everything that
        /// falls outside goes to a sink that keeps nothing. The drawing code asks <see cref="Overlaps"/> to skip whole
        /// pieces, houses and props outside the range.
        /// </summary>
        sealed class Chunks
        {
            readonly Dictionary<long, HillMesh> _map = new Dictionary<long, HillMesh>();
            readonly HillMesh _discard = HillMesh.Discarding("(outside the chunks being drawn)");
            int _x0 = int.MinValue, _x1 = int.MaxValue, _z0 = int.MinValue, _z1 = int.MaxValue;

            public static int Cell(float v) => Mathf.FloorToInt(v / ChunkSize);

            /// <summary>Draw only chunks x0..x1, z0..z1 (inclusive, chunk coordinates).</summary>
            public void Only(int x0, int x1, int z0, int z1) { _x0 = x0; _x1 = x1; _z0 = z0; _z1 = z1; }

            public bool Accepts(float x, float z)
            {
                int cx = Cell(x), cz = Cell(z);
                return cx >= _x0 && cx <= _x1 && cz >= _z0 && cz <= _z1;
            }

            /// <summary>Whether a world box (x/z extents, with a metre's margin) touches the chunks being drawn.</summary>
            public bool Overlaps(float minX, float maxX, float minZ, float maxZ) =>
                Cell(maxX + 1f) >= _x0 && Cell(minX - 1f) <= _x1 && Cell(maxZ + 1f) >= _z0 && Cell(minZ - 1f) <= _z1;

            public bool Overlaps(BlockPiece p) => Overlaps(p.Min.X, p.Max.X, p.Min.Z, p.Max.Z);

            public HillMesh At(float x, float z)
            {
                int cx = Cell(x), cz = Cell(z);
                if (cx < _x0 || cx > _x1 || cz < _z0 || cz > _z1) return _discard;
                long key = ((long)cx << 32) ^ (uint)cz;
                if (!_map.TryGetValue(key, out var m)) { m = new HillMesh("Chunk " + cx + "," + cz); _map[key] = m; }
                return m;
            }
            public HillMesh At(Vector3 p) => At(p.x, p.z);
            public IEnumerable<HillMesh> All => _map.Values;
            public void Clear() => _map.Clear();
        }

        /// <summary>A coarse grid over the hill's pieces, for the drawing's height questions (TopAt over thousands of pieces).</summary>
        sealed class PieceGrid
        {
            const float Cell = 8f;
            readonly Dictionary<long, List<BlockPiece>> _cells = new Dictionary<long, List<BlockPiece>>();
            static long Key(int x, int z) => ((long)x << 32) ^ (uint)z;

            public PieceGrid(IEnumerable<BlockPiece> pieces)
            {
                foreach (var p in pieces)
                {
                    if (!p.solid || p.kind == BlockKind.Boundary) continue;
                    int x0 = Mathf.FloorToInt(p.Min.X / Cell), x1 = Mathf.FloorToInt(p.Max.X / Cell);
                    int z0 = Mathf.FloorToInt(p.Min.Z / Cell), z1 = Mathf.FloorToInt(p.Max.Z / Cell);
                    for (int x = x0; x <= x1; x++)
                        for (int z = z0; z <= z1; z++)
                        {
                            if (!_cells.TryGetValue(Key(x, z), out var list)) { list = new List<BlockPiece>(); _cells[Key(x, z)] = list; }
                            list.Add(p);
                        }
                }
            }

            /// <summary>The highest solid top at (x, z) at or below <paramref name="maxY"/> (open switches: Walt aside, the shutter up).</summary>
            public float TopAt(float x, float z, float maxY = 500f, Predicate<BlockPiece> only = null)
            {
                float best = -50f;
                if (!_cells.TryGetValue(Key(Mathf.FloorToInt(x / Cell), Mathf.FloorToInt(z / Cell)), out var list)) return best;
                foreach (var b in list)
                {
                    if (b.switchIndex >= 0) continue;
                    if (x < b.Min.X || x > b.Max.X || z < b.Min.Z || z > b.Max.Z) continue;
                    if (only != null && !only(b)) continue;
                    float t = b.TopAt(x, z);
                    if (t <= maxY && t > best) best = t;
                }
                return best;
            }

            /// <summary>Something solid other than ground stands at (x, z) between <paramref name="y0"/> and <paramref name="y1"/>.</summary>
            public bool Occupied(float x, float z, float y0, float y1, float pad = 0f)
            {
                if (!_cells.TryGetValue(Key(Mathf.FloorToInt(x / Cell), Mathf.FloorToInt(z / Cell)), out var list)) return false;
                foreach (var b in list)
                {
                    if (b.style.Contains("|")) continue;
                    if (x < b.Min.X - pad || x > b.Max.X + pad || z < b.Min.Z - pad || z > b.Max.Z + pad) continue;
                    if (b.Max.Y < y0 || b.Min.Y > y1) continue;
                    return true;
                }
                return false;
            }
        }

        static Vector3 U(System.Numerics.Vector3 v) => new Vector3(v.X, v.Y, v.Z);

        static string SurfaceMat(string surface)
        {
            switch (surface)
            {
                case "grass": return HillMat.Grass;
                case "dirt": return HillMat.Dirt;
                case "gravel": return HillMat.Gravel;
                case "asphalt": return HillMat.Asphalt;
                case "paving": return HillMat.Paving;
                case "cobbles": return HillMat.Cobbles;
                case "deck": return HillMat.Deck;
                case "pool": return HillMat.Plaster;
                case "creek_bed": return HillMat.CreekBed;
                case "steps": return HillMat.Concrete;
                default: return HillMat.Concrete;
            }
        }

        // ---------- The ground ----------

        static void DrawGround(LarchHill h, PieceGrid grid, Chunks chunks)
        {
            foreach (var p in h.pieces)
            {
                if (p.kind != BlockKind.Ground || !p.style.Contains("|") || !chunks.Overlaps(p)) continue;
                var parts = p.style.Split('|');
                string surface = parts[0], side = parts.Length > 1 ? parts[1] : "stone_retaining";
                if (surface == "steps") DrawSteps(p, chunks);
                else DrawTop(p, SurfaceMat(surface), chunks);
                DrawSides(p, side, grid, chunks);
            }
        }

        /// <summary>A tile's top in cells of at most 20 m (each cell in its chunk), following a ramp's slope.</summary>
        static void DrawTop(BlockPiece p, string mat, Chunks chunks)
        {
            int nx = Mathf.Max(1, Mathf.CeilToInt(p.size.X / 20f)), nz = Mathf.Max(1, Mathf.CeilToInt(p.size.Z / 20f));
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    float x0 = p.Min.X + p.size.X * i / nx, x1 = p.Min.X + p.size.X * (i + 1) / nx;
                    float z0 = p.Min.Z + p.size.Z * j / nz, z1 = p.Min.Z + p.size.Z * (j + 1) / nz;
                    var m = chunks.At((x0 + x1) * 0.5f, (z0 + z1) * 0.5f);
                    m.Quad(mat, new Vector3(x0, p.TopAt(x0, z0), z0), new Vector3(x0, p.TopAt(x0, z1), z1), new Vector3(x1, p.TopAt(x1, z1), z1),
                        new Vector3(x1, p.TopAt(x1, z0), z0), Vector3.up);
                }
        }

        /// <summary>A flight of steps over a ramp tile: treads and risers (about 16 cm each), a nosing on every tread.</summary>
        static void DrawSteps(BlockPiece p, Chunks chunks)
        {
            if (p.rampAxis == 0) { DrawTop(p, HillMat.Concrete, chunks); return; }
            bool alongX = p.rampAxis == 1;
            float len = alongX ? p.size.X : p.size.Z, rise = p.rampHigh - p.rampLow;
            int n = Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(rise) / 0.16f));
            float a0 = alongX ? p.Min.X : p.Min.Z, w0 = alongX ? p.Min.Z : p.Min.X, w1 = alongX ? p.Max.Z : p.Max.X;
            for (int i = 0; i < n; i++)
            {
                float s0 = a0 + len * i / n, s1 = a0 + len * (i + 1) / n;
                float y = p.rampLow + rise * (i + 0.5f) / n;
                float yPrev = p.rampLow + rise * (i - 0.5f) / n;
                var m = chunks.At(alongX ? (s0 + s1) * 0.5f : (w0 + w1) * 0.5f, alongX ? (w0 + w1) * 0.5f : (s0 + s1) * 0.5f);
                Vector3 P(float s, float w, float yy) => alongX ? new Vector3(s, yy, w) : new Vector3(w, yy, s);
                m.Quad(HillMat.Concrete, P(s0, w0, y), P(s0, w1, y), P(s1, w1, y), P(s1, w0, y), Vector3.up);
                // The riser between this tread and the one below (on the low side).
                float low = rise > 0f ? yPrev : p.rampLow + rise * (i + 1.5f) / n;
                float sr = rise > 0f ? s0 : s1;
                if (i > 0 || rise < 0f)
                {
                    var outward = alongX ? new Vector3(rise > 0f ? -1f : 1f, 0f, 0f) : new Vector3(0f, 0f, rise > 0f ? -1f : 1f);
                    float yLow = Mathf.Min(y, rise > 0f ? yPrev : p.rampLow + rise * (i + 1.5f) / n);
                    m.Quad(HillMat.Concrete, P(sr, w0, yLow), P(sr, w0, y), P(sr, w1, y), P(sr, w1, yLow), outward);
                    _ = low;
                }
                // A nosing strip (a little lip of darker stone) along the tread's front.
                var nose = alongX ? new Vector3(rise > 0f ? s0 + 0.03f : s1 - 0.03f, y + 0.006f, (w0 + w1) * 0.5f) : new Vector3((w0 + w1) * 0.5f, y + 0.006f, rise > 0f ? s0 + 0.03f : s1 - 0.03f);
                m.BoxC(HillMat.StoneCoping, nose, alongX ? new Vector3(0.06f, 0.012f, w1 - w0) : new Vector3(w1 - w0, 0.012f, 0.06f));
            }
        }

        /// <summary>A tile's sides where it stands above its neighbours: a retaining wall (stone, with a coping on anything
        /// over 0.6 m) or a kerb, in 2 m segments down to the neighbour's top. The copings and kerb edges run on as one
        /// piece while their height holds (round 2: one 2 m bevelled coping stone per segment was 1.2M of the hill's 5.3M
        /// vertices).</summary>
        static void DrawSides(BlockPiece p, string side, PieceGrid grid, Chunks chunks)
        {
            for (int s = 0; s < 4; s++)
            {
                bool alongX = s < 2;
                float fixedC = s == 0 ? p.Min.Z : s == 1 ? p.Max.Z : s == 2 ? p.Min.X : p.Max.X;
                float outSign = s == 0 || s == 2 ? -1f : 1f;
                float a0 = alongX ? p.Min.X : p.Min.Z, a1 = alongX ? p.Max.X : p.Max.Z;
                int n = Mathf.Max(1, Mathf.CeilToInt((a1 - a0) / 2f));
                var outward = alongX ? new Vector3(0f, 0f, outSign) : new Vector3(outSign, 0f, 0f);
                // The run being collected: kind 1 a coping, 2 a kerb's edge; from ra0 to ra1 at height ry.
                int runKind = 0;
                float ra0 = 0f, ra1 = 0f, ry = 0f;
                void Flush()
                {
                    if (runKind == 0) return;
                    float mid = (ra0 + ra1) * 0.5f, len = ra1 - ra0;
                    var m = chunks.At(alongX ? mid : fixedC, alongX ? fixedC : mid);
                    if (runKind == 1)
                    {
                        var c = alongX ? new Vector3(mid, ry + 0.015f, fixedC - outSign * 0.09f) : new Vector3(fixedC - outSign * 0.09f, ry + 0.015f, mid);
                        m.BoxC(HillMat.StoneCoping, c, alongX ? new Vector3(len, 0.2f, 0.34f) : new Vector3(0.34f, 0.2f, len), 0.02f);
                    }
                    else
                    {
                        var c = alongX ? new Vector3(mid, ry - 0.03f, fixedC) : new Vector3(fixedC, ry - 0.03f, mid);
                        m.BoxC(HillMat.Kerb, c, alongX ? new Vector3(len, 0.06f, 0.16f) : new Vector3(0.16f, 0.06f, len), 0.025f);
                    }
                    runKind = 0;
                }
                void Run(int kind, float b0, float b1, float y)
                {
                    if (runKind == kind && Mathf.Abs(b0 - ra1) < 0.01f && Mathf.Abs(y - ry) < 0.02f && ra1 - ra0 < 24f) { ra1 = b1; return; }
                    Flush();
                    runKind = kind; ra0 = b0; ra1 = b1; ry = y;
                }
                for (int i = 0; i < n; i++)
                {
                    float b0 = a0 + (a1 - a0) * i / n, b1 = a0 + (a1 - a0) * (i + 1) / n, mid = (b0 + b1) * 0.5f;
                    float ox = alongX ? mid : fixedC + outSign * 0.05f, oz = alongX ? fixedC + outSign * 0.05f : mid;
                    float t0 = alongX ? p.TopAt(b0, fixedC) : p.TopAt(fixedC, b0), t1 = alongX ? p.TopAt(b1, fixedC) : p.TopAt(fixedC, b1);
                    float top = Mathf.Min(t0, t1);
                    float nb = grid.TopAt(ox, oz, top - 0.02f, q => q.style.Contains("|") || q.kind == BlockKind.House);
                    if (nb < p.Min.Y) nb = p.Min.Y;
                    float hgt = Mathf.Max(t0, t1) - nb;
                    if (hgt < 0.03f) { Flush(); continue; }
                    bool kerb = side == "kerb" || hgt < 0.35f;
                    string mat = kerb ? HillMat.Kerb : HillMat.StoneWall;
                    var m = chunks.At(alongX ? mid : fixedC, alongX ? fixedC : mid);
                    Vector3 P(float a, float y) => alongX ? new Vector3(a, y, fixedC) : new Vector3(fixedC, y, a);
                    m.Quad(mat, P(b0, nb), P(b0, t0), P(b1, t1), P(b1, nb), outward);
                    float cy = (t0 + t1) * 0.5f;
                    bool flat = Mathf.Abs(t0 - t1) < 0.05f;
                    // The coping (a bevelled cap along the top edge, overhanging the face by 4 cm) or a kerb's rounded edge.
                    if (!kerb && hgt > 0.6f && flat) Run(1, b0, b1, cy);
                    else if (kerb && hgt < 0.35f && flat) Run(2, b0, b1, cy);
                    else Flush();
                }
                Flush();
            }
        }

        // ---------- Scatter: grass, flowers, leaves, cracks, manholes, drains, road markings ----------

        /// <summary>The hard ground's wear: cracks, a few fallen leaves and stains (decals; each tile seeded by its index, so a
        /// tile drawn in two bands of chunks comes out the same in both). The grass is the Look library's tufts
        /// (<see cref="DrawTufts"/>).</summary>
        static void DrawScatter(LarchHill h, PieceGrid grid, Chunks chunks)
        {
            for (int pi = 0; pi < h.pieces.Count; pi++)
            {
                var p = h.pieces[pi];
                if (p.kind != BlockKind.Ground || !p.style.Contains("|") || !chunks.Overlaps(p)) continue;
                var r = new Random(1234 + pi * 7919);
                string surface = p.style.Split('|')[0];
                float area = p.size.X * p.size.Z;
                if (surface == "asphalt" || surface == "paving" || surface == "cobbles" || surface == "concrete")
                {
                    int cracks = Mathf.Min(120, Mathf.RoundToInt(area * (surface == "asphalt" ? 0.008f : 0.005f)));
                    int leaves = Mathf.Min(80, Mathf.RoundToInt(area * 0.004f));
                    int stains = surface == "asphalt" ? Mathf.Min(30, Mathf.RoundToInt(area * 0.0015f)) : 0;
                    for (int i = 0; i < cracks + leaves + stains; i++)
                    {
                        float x = p.Min.X + 0.6f + (float)r.NextDouble() * Mathf.Max(0.1f, p.size.X - 1.2f), z = p.Min.Z + 0.6f + (float)r.NextDouble() * Mathf.Max(0.1f, p.size.Z - 1.2f);
                        float y = p.TopAt(x, z);
                        if (grid.TopAt(x, z, y + 3f) > y + 0.05f) continue;
                        string mat = i < cracks ? HillMat.Cracks : i < cracks + leaves ? HillMat.Leaflets : HillMat.Stains;
                        float size = mat == HillMat.Cracks ? 1.2f + (float)r.NextDouble() * 1.4f : 0.9f + (float)r.NextDouble() * 1.2f;
                        Decal(chunks.At(x, z), mat, new Vector3(x, y, z), size, (float)r.NextDouble() * 360f, HillAtlas.Decal(r.Next(4)), p);
                    }
                }
            }
        }

        /// <summary>
        /// The lawns' grass: the Look library's tufts (<see cref="VhLookGround.Tufts"/>: drifts clumped by noise, mostly
        /// green with dry, weeds and a few wildflowers; no shadows; chunked per 24 m), on every grass tile clear of what
        /// stands on it. Replaces milestone 1's evenly spread grass and flower cards (the judges' "confetti").
        /// </summary>
        static GameObject DrawTufts(LarchHill h, PieceGrid grid, Transform parent, Rect? only = null)
        {
            float Ground(float x, float z) => grid.TopAt(x, z, 200f, q => q.style.Contains("|"));
            var tufts = new VhLookGround.Tufts(17, Ground);
            foreach (var p in h.pieces)
            {
                if (p.kind != BlockKind.Ground || !p.style.StartsWith("grass|", StringComparison.Ordinal)) continue;
                var rect = new Rect(p.Min.X + 0.3f, p.Min.Z + 0.3f, p.size.X - 0.6f, p.size.Z - 0.6f);
                if (rect.width <= 0f || rect.height <= 0f) continue;
                if (only.HasValue && !only.Value.Overlaps(rect)) continue;
                var tile = p;
                tufts.Scatter(rect, 0.16f, (x, z) =>
                {
                    float y = tile.TopAt(x, z);
                    return grid.TopAt(x, z, y + 3f) <= y + 0.05f && !grid.Occupied(x, z, y, y + 1.6f, 0.25f);
                }, 0.06f, 0.22f);
            }
            var go = tufts.Build(parent, "Grass (tufts)");
            Debug.Log("JAMKIT_STREET tufts: " + tufts.count);
            return go;
        }

        /// <summary>A flat decal 1.5 cm over the ground (following a ramp's slope).</summary>
        static void Decal(HillMesh m, string mat, Vector3 at, float size, float yaw, Rect uv, BlockPiece onto)
        {
            var q = Quaternion.Euler(0f, yaw, 0f);
            var right = q * Vector3.right * size * 0.5f;
            var fwd = q * Vector3.forward * size * 0.5f;
            Vector3 P(Vector3 o) { var w = at + o; w.y = onto.TopAt(w.x, w.z) + 0.015f; return w; }
            var pts = new[] { P(-right - fwd), P(-right + fwd), P(right + fwd), P(right - fwd) };
            var uvs = new[] { new Vector2(uv.xMin, uv.yMin), new Vector2(uv.xMin, uv.yMax), new Vector2(uv.xMax, uv.yMax), new Vector2(uv.xMax, uv.yMin) };
            m.PolyUV(mat, pts, uvs, Vector3.up, Vector3.up);
        }

        /// <summary>The roads' centre lines (dashed), manholes along them, drains at the kerbs.</summary>
        static void DrawRoadMarkings(LarchHill h, Chunks chunks)
        {
            foreach (var p in h.pieces)
            {
                if (p.kind != BlockKind.Ground || !p.style.StartsWith("asphalt|") || !chunks.Overlaps(p)) continue;
                if (p.name.Contains("(footpath)") || p.name.Contains("forecourt") || p.name.Contains("grounds") || p.name.Contains("car park")) continue;
                bool alongX = p.size.X >= p.size.Z;
                float len = alongX ? p.size.X : p.size.Z, width = alongX ? p.size.Z : p.size.X;
                if (width < 5f || len < 10f) continue;
                float c = alongX ? p.center.Z : p.center.X;
                float a0 = alongX ? p.Min.X : p.Min.Z;
                for (float a = a0 + 2f; a + 3f < a0 + len - 1f; a += 6f)
                {
                    Vector3 P(float s, float w) { var v = alongX ? new Vector3(s, 0f, w) : new Vector3(w, 0f, s); v.y = p.TopAt(v.x, v.z) + 0.012f; return v; }
                    chunks.At(P(a, c)).Quad(HillMat.RoadPaint, P(a, c - 0.06f), P(a + 3f, c - 0.06f), P(a + 3f, c + 0.06f), P(a, c + 0.06f), Vector3.up);
                }
                for (float a = a0 + 9f; a < a0 + len - 4f; a += 37f)
                {
                    var at = alongX ? new Vector3(a, 0f, c + width * 0.22f) : new Vector3(c + width * 0.22f, 0f, a);
                    at.y = p.TopAt(at.x, at.z);
                    Decal(chunks.At(at), HillMat.Manhole, at, 0.75f, 0f, HillAtlas.Whole, p);
                }
                for (float a = a0 + 5f; a < a0 + len - 2f; a += 21f)
                {
                    var at = alongX ? new Vector3(a, 0f, p.Min.Z + 0.35f) : new Vector3(p.Min.X + 0.35f, 0f, a);
                    at.y = p.TopAt(at.x, at.z);
                    Decal(chunks.At(at), HillMat.Drain, at, 0.6f, alongX ? 0f : 90f, HillAtlas.Whole, p);
                }
            }
        }

        // ---------- Pieces with their own look ----------

        /// <summary>Draws every solid piece nobody else draws (not a tile, not a house's, building's, prop's or tree's part):
        /// fences and walls (the kit's HillFences), the oak's limbs, the party's things, Walt's shop's walls and roof, the
        /// scaffold, the bus, the treehouse, the Old Mill's bridge; anything else as a bevelled box in its kind's material.</summary>
        static void DrawPieces(LarchHill h, Chunks chunks, Vector3 oakFoot)
        {
            foreach (var p in h.pieces)
            {
                if (!p.solid || p.kind == BlockKind.Boundary || p.style.StartsWith("@") || p.style.Contains("|") || !chunks.Accepts(p.center.X, p.center.Z)) continue;
                if (p.style == "walt" || p.style == "shutter" || p.style == "lasname_house") continue;
                var m = chunks.At(U(p.center));
                try { DrawPiece(m, p, oakFoot); }
                catch (Exception e) { Debug.LogWarning("JAMKIT_STREET couldn't draw '" + p.name + "': " + e.Message); BevelBox(m, p, HillMat.Concrete); }
            }
            // Water surfaces (the creek, the pool, the ditch): not solid, drawn as a sheet at their top.
            foreach (var p in h.pieces)
            {
                if (p.kind != BlockKind.Water || !chunks.Overlaps(p)) continue;
                DrawTop(new BlockPiece { center = p.center, size = p.size, name = p.name }, HillMat.Water, chunks);
            }
        }

        static void BevelBox(HillMesh m, BlockPiece p, string mat, float bevel = 0.03f) =>
            m.Box(mat, U(p.Min), U(p.Max), Mathf.Min(bevel, Mathf.Min(p.size.X, Mathf.Min(p.size.Y, p.size.Z)) * 0.3f));

        static void DrawPiece(HillMesh m, BlockPiece p, Vector3 oakFoot)
        {
            var mn = U(p.Min);
            var mx = U(p.Max);
            switch (p.style)
            {
                case "limb": HillTrees.DrawLimb(m, p, oakFoot); return;
                case "garage_door":
                    m.Box(HillMat.Corrugated, mn, mx, 0.01f);
                    m.BoxC(HillMat.MetalDark, new Vector3(p.center.X, mn.y + 0.06f, mn.z - 0.02f), new Vector3(mx.x - mn.x, 0.08f, 0.04f), 0.01f);
                    return;
                case "counter":
                    m.Box(HillMat.TrimDark, mn, new Vector3(mx.x, mx.y - 0.05f, mx.z), 0.02f);
                    m.Box(HillMat.Deck, new Vector3(mn.x - 0.04f, mx.y - 0.05f, mn.z - 0.04f), new Vector3(mx.x + 0.04f, mx.y, mx.z + 0.04f), 0.015f);
                    return;
                case "pergola_beam":
                    m.Box(HillMat.FenceWood, mn, mx, 0.03f);
                    HillKit.Ivy(m, new Random(5), p.center.X, mx.y, 0.6f, 0.4f);
                    return;
                case "valance":
                    m.Box(HillMat.CanvasRed, mn, mx, 0.01f);
                    return;
                case "gazebo":
                {
                    // A row of party tents across the street (the collider is one flat roof over all of them; drawn as one
                    // canvas it read as a 25 m awning): three canopies with gaps, each with its valance.
                    const int tents = 3;
                    const float gap = 0.35f;
                    float depth = (mx.z - mn.z - gap * (tents - 1)) / tents;
                    for (int i = 0; i < tents; i++)
                    {
                        float z0 = mn.z + i * (depth + gap), z1 = z0 + depth;
                        string canvas = i % 2 == 0 ? HillMat.CanvasGreen : HillMat.Canvas;
                        m.Box(canvas, new Vector3(mn.x, mn.y, z0), new Vector3(mx.x, mx.y, z1), 0.02f);
                        m.Box(HillMat.CanvasRed, new Vector3(mn.x - 0.02f, mn.y - 0.28f, z0), new Vector3(mn.x, mn.y, z1), 0f, HillMesh.Side.YPos);
                        m.Box(HillMat.CanvasRed, new Vector3(mx.x, mn.y - 0.28f, z0), new Vector3(mx.x + 0.02f, mn.y, z1), 0f, HillMesh.Side.YPos);
                        m.Box(HillMat.Steel, new Vector3(mn.x, mn.y - 0.06f, z0), new Vector3(mx.x, mn.y, z1), 0.01f, HillMesh.Side.YPos);
                    }
                    return;
                }
                case "gazebo_leg": m.Box(HillMat.Steel, mn, mx, 0.01f); return;
                case "hay": Hay(m, mn, mx); return;
                case "truck": Truck(m, mn, mx); return;
                case "bouncy_castle": Bouncy(m, mn, mx); return;
                case "skip":
                    m.Box(HillMat.MetalDark, mn, mx, 0.03f);
                    m.Box(HillMat.Rubber, new Vector3(mn.x - 0.03f, mx.y - 0.08f, mn.z - 0.03f), new Vector3(mx.x + 0.03f, mx.y, mx.z + 0.03f), 0.01f);
                    return;
                case "crates": Crates(m, mn, mx); return;
                case "stall":
                    m.Box(HillMat.Deck, mn, new Vector3(mx.x, mn.y + 1f, mx.z), 0.02f);
                    m.Box(HillMat.CanvasBlue, new Vector3(mn.x, mx.y - 0.25f, mn.z), mx, 0.02f);
                    foreach (var c in new[] { new Vector2(mn.x + 0.06f, mn.z + 0.06f), new Vector2(mx.x - 0.06f, mn.z + 0.06f), new Vector2(mn.x + 0.06f, mx.z - 0.06f), new Vector2(mx.x - 0.06f, mx.z - 0.06f) })
                        m.Box(HillMat.FenceWood, new Vector3(c.x - 0.05f, mn.y + 1f, c.y - 0.05f), new Vector3(c.x + 0.05f, mx.y - 0.25f, c.y + 0.05f));
                    return;
                case "stage":
                    m.Box(HillMat.TrimDark, mn, new Vector3(mx.x, mx.y - 0.06f, mx.z), 0.02f);
                    m.Box(HillMat.Deck, new Vector3(mn.x, mx.y - 0.06f, mn.z), mx, 0.01f);
                    return;
                case "bus": Bus(m, mn, mx); return;
                case "scaffold": Scaffold(m, mn, mx); return;
                case "scaffold_bar": m.Cylinder(HillMat.Steel, new Vector3(p.center.X, p.center.Y, mn.z), new Vector3(p.center.X, p.center.Y, mx.z), Mathf.Min(p.size.X, p.size.Y) * 0.5f, 10); return;
                case "treehouse_floor": m.Box(HillMat.Deck, mn, mx, 0.02f); return;
                case "treehouse_wall": m.Box(HillMat.FenceWood, mn, mx, 0.02f); return;
                case "treehouse_roof": m.Box(HillMat.RoofGreen, mn, mx, 0.03f); return;
                case "mill_bridge": MillBridge(m, mn, mx); return;
                case "roof_metal":
                case "roof_shingle_red": RoofSlab(m, p, p.style == "roof_metal" ? HillMat.RoofMetal : HillMat.RoofRed); return;
                case "brick_red":
                    m.Box(HillMat.BrickRed, mn, mx, 0.02f);
                    return;
            }
            switch (p.kind)
            {
                case BlockKind.Fence:
                case BlockKind.Picket:
                case BlockKind.Hedge:
                case BlockKind.BrickWall:
                case BlockKind.Gate:
                    HillFences.Draw(m, p);
                    return;
                case BlockKind.Deck: BevelBox(m, p, HillMat.Deck); return;
                case BlockKind.Plank: BevelBox(m, p, HillMat.FenceWood); return;
                case BlockKind.Post: BevelBox(m, p, HillMat.MetalPaint, 0.01f); return;
                case BlockKind.Roof: BevelBox(m, p, HillMat.RoofGrey); return;
                case BlockKind.House: BevelBox(m, p, HillMat.BrickRed); return;
                case BlockKind.Shop: BevelBox(m, p, HillMat.BrickRed, 0.02f); return;
                case BlockKind.Door: BevelBox(m, p, HillMat.DoorGreen, 0.01f); return;
                case BlockKind.Tree: HillTrees.DrawLimb(m, p, oakFoot); return;
                default: BevelBox(m, p, HillMat.Concrete); return;
            }
        }

        /// <summary>A roof slope piece (a ramp from its eaves to its ridge): the sheet on the slope, its fascia and a gutter.</summary>
        static void RoofSlab(HillMesh m, BlockPiece p, string mat)
        {
            float x0 = p.Min.X, x1 = p.Max.X, z0 = p.Min.Z, z1 = p.Max.Z;
            m.Quad(mat, new Vector3(x0, p.TopAt(x0, z0), z0), new Vector3(x0, p.TopAt(x0, z1), z1), new Vector3(x1, p.TopAt(x1, z1), z1), new Vector3(x1, p.TopAt(x1, z0), z0), Vector3.up);
            // The underside and the ends, so it isn't paper-thin from below.
            m.Quad(HillMat.TrimWhite, new Vector3(x0, p.Min.Y, z0), new Vector3(x1, p.Min.Y, z0), new Vector3(x1, p.Min.Y, z1), new Vector3(x0, p.Min.Y, z1), Vector3.down);
            bool alongZ = p.rampAxis == 2;
            if (alongZ)
            {
                float lowZ = p.rampLow <= p.rampHigh ? z0 : z1, lowY = Mathf.Min(p.rampLow, p.rampHigh);
                var outward = new Vector3(0f, 0f, lowZ == z0 ? -1f : 1f);
                m.Quad(HillMat.TrimWhite, new Vector3(x0, p.Min.Y, lowZ), new Vector3(x0, lowY, lowZ), new Vector3(x1, lowY, lowZ), new Vector3(x1, p.Min.Y, lowZ), outward);
                m.Cylinder(HillMat.Gutter, new Vector3(x0, lowY - 0.08f, lowZ + outward.z * 0.08f), new Vector3(x1, lowY - 0.08f, lowZ + outward.z * 0.08f), 0.07f, 8);
                foreach (float x in new[] { x0, x1 })
                    m.Poly(HillMat.TrimWhite, new[] { new Vector3(x, p.Min.Y, z0), new Vector3(x, p.TopAt(x, z0), z0), new Vector3(x, p.TopAt(x, z1), z1), new Vector3(x, p.Min.Y, z1) }, new Vector3(x == x0 ? -1f : 1f, 0f, 0f));
            }
            else
            {
                float lowX = p.rampLow <= p.rampHigh ? x0 : x1, lowY = Mathf.Min(p.rampLow, p.rampHigh);
                var outward = new Vector3(lowX == x0 ? -1f : 1f, 0f, 0f);
                m.Quad(HillMat.TrimWhite, new Vector3(lowX, p.Min.Y, z0), new Vector3(lowX, lowY, z0), new Vector3(lowX, lowY, z1), new Vector3(lowX, p.Min.Y, z1), outward);
                m.Cylinder(HillMat.Gutter, new Vector3(lowX + outward.x * 0.08f, lowY - 0.08f, z0), new Vector3(lowX + outward.x * 0.08f, lowY - 0.08f, z1), 0.07f, 8);
                foreach (float z in new[] { z0, z1 })
                    m.Poly(HillMat.TrimWhite, new[] { new Vector3(x0, p.Min.Y, z), new Vector3(x0, p.TopAt(x0, z), z), new Vector3(x1, p.TopAt(x1, z), z), new Vector3(x1, p.Min.Y, z) }, new Vector3(0f, 0f, z == z0 ? -1f : 1f));
            }
        }

        static void Hay(HillMesh m, Vector3 mn, Vector3 mx)
        {
            const float bl = 1.1f, bh = 0.44f;
            var r = new Random(7);
            for (float y = mn.y; y + bh <= mx.y + 0.01f; y += bh)
                for (float z = mn.z; z < mx.z - 0.05f; z += bl)
                {
                    float z1 = Mathf.Min(mx.z, z + bl);
                    float jitter = (float)r.NextDouble() * 0.06f;
                    m.Box(HillMat.GrassDry, new Vector3(mn.x + jitter, y, z + 0.02f), new Vector3(mx.x - jitter, y + bh - 0.01f, z1 - 0.02f), 0.08f);
                }
        }

        static void Crates(HillMesh m, Vector3 mn, Vector3 mx)
        {
            for (float y = mn.y; y < mx.y - 0.1f; y += 0.4f)
                for (float z = mn.z; z < mx.z - 0.1f; z += 0.6f)
                    m.Box(HillMat.FenceWood, new Vector3(mn.x + 0.02f, y, z + 0.02f), new Vector3(mx.x - 0.02f, Mathf.Min(mx.y, y + 0.4f) - 0.01f, Mathf.Min(mx.z, z + 0.6f) - 0.02f), 0.02f);
        }

        static void Truck(HillMesh m, Vector3 mn, Vector3 mx)
        {
            float len = mx.x - mn.x, cabX = mx.x - 2.6f;
            // The box body (rear), the cab (front, east), wheels, a bumper; the body down near the road (no gap under the collider).
            m.Box(HillMat.MetalPaint, new Vector3(mn.x, mn.y + 0.55f, mn.z), new Vector3(cabX - 0.1f, mx.y, mx.z), 0.06f);
            m.Box(HillMat.DoorBlue, new Vector3(cabX, mn.y + 0.55f, mn.z + 0.1f), new Vector3(mx.x, mx.y - 0.55f, mx.z - 0.1f), 0.1f);
            m.Box(HillMat.DoorBlue, new Vector3(cabX, mx.y - 0.55f, mn.z + 0.2f), new Vector3(mx.x - 0.5f, mx.y, mx.z - 0.2f), 0.12f);
            m.Box(HillMat.Glass, new Vector3(mx.x - 0.02f, mn.y + 1.6f, mn.z + 0.25f), new Vector3(mx.x + 0.01f, mn.y + 2.45f, mx.z - 0.25f));
            m.Box(HillMat.Rubber, new Vector3(mn.x, mn.y + 0.12f, mn.z + 0.08f), new Vector3(mx.x, mn.y + 0.55f, mx.z - 0.08f), 0.05f);
            foreach (float x in new[] { mn.x + 1.2f, mn.x + 2.4f, mx.x - 1.4f })
                foreach (float z in new[] { mn.z - 0.02f, mx.z + 0.02f })
                {
                    float side = z < (mn.z + mx.z) * 0.5f ? 0.3f : -0.3f;
                    m.Cylinder(HillMat.Rubber, new Vector3(x, mn.y + 0.5f, z), new Vector3(x, mn.y + 0.5f, z + side), 0.5f, 14);
                }
            m.Box(HillMat.Steel, new Vector3(mx.x - 0.05f, mn.y + 0.25f, mn.z), new Vector3(mx.x + 0.12f, mn.y + 0.5f, mx.z), 0.03f);
            _ = len;
        }

        static void Bouncy(HillMesh m, Vector3 mn, Vector3 mx)
        {
            m.Box(HillMat.CanvasBlue, mn, new Vector3(mx.x, mn.y + 0.7f, mx.z), 0.2f);
            m.Box(HillMat.CanvasRed, new Vector3(mn.x, mn.y + 0.7f, mn.z), new Vector3(mn.x + 0.6f, mx.y - 0.6f, mx.z), 0.2f);
            m.Box(HillMat.CanvasRed, new Vector3(mx.x - 0.6f, mn.y + 0.7f, mn.z), new Vector3(mx.x, mx.y - 0.6f, mx.z), 0.2f);
            m.Box(HillMat.CanvasRed, new Vector3(mn.x, mn.y + 0.7f, mx.z - 0.6f), new Vector3(mx.x, mx.y - 0.6f, mx.z), 0.2f);
            m.Box(HillMat.Canvas, new Vector3(mn.x, mn.y + 0.7f, mn.z), new Vector3(mx.x, mx.y - 1.2f, mn.z + 0.5f), 0.2f);
            foreach (var c in new[] { new Vector3(mn.x + 0.4f, 0f, mn.z + 0.4f), new Vector3(mx.x - 0.4f, 0f, mn.z + 0.4f), new Vector3(mn.x + 0.4f, 0f, mx.z - 0.4f), new Vector3(mx.x - 0.4f, 0f, mx.z - 0.4f) })
            {
                m.Cylinder(HillMat.CanvasGreen, new Vector3(c.x, mn.y + 0.7f, c.z), new Vector3(c.x, mx.y - 0.4f, c.z), 0.4f, 12);
                m.Ellipsoid(HillMat.CanvasRed, new Vector3(c.x, mx.y - 0.35f, c.z), new Vector3(0.42f, 0.35f, 0.42f), 10);
            }
        }

        static void Bus(HillMesh m, Vector3 mn, Vector3 mx)
        {
            m.Box(HillMat.DoorGreen, new Vector3(mn.x, mn.y + 0.35f, mn.z), new Vector3(mx.x, mn.y + 1.4f, mx.z), 0.08f);
            m.Box(HillMat.TrimCream, new Vector3(mn.x, mn.y + 2.4f, mn.z), new Vector3(mx.x, mx.y, mx.z), 0.15f);
            m.Box(HillMat.Glass, new Vector3(mn.x + 0.02f, mn.y + 1.4f, mn.z + 0.02f), new Vector3(mx.x - 0.02f, mn.y + 2.4f, mx.z - 0.02f));
            for (float z = mn.z + 0.2f; z < mx.z; z += 1.3f)
                m.Box(HillMat.TrimCream, new Vector3(mn.x - 0.01f, mn.y + 1.4f, z), new Vector3(mx.x + 0.01f, mn.y + 2.4f, z + 0.12f));
            m.Rect(HillMat.SignBoard, new Vector3((mn.x + mx.x) * 0.5f, mx.y - 0.35f, mn.z - 0.01f), new Vector3(mx.x - mn.x - 0.4f, 0f, 0f), new Vector3(0f, 0.3f, 0f), Vector3.back, HillAtlas.Sign(HillAtlas.SignBusStop));
            foreach (float z in new[] { mn.z + 1.8f, mx.z - 2.2f })
                foreach (float x in new[] { mn.x - 0.02f, mx.x + 0.02f })
                    m.Cylinder(HillMat.Rubber, new Vector3(x, mn.y + 0.5f, z), new Vector3(x + (x < (mn.x + mx.x) * 0.5f ? 0.3f : -0.3f), mn.y + 0.5f, z), 0.5f, 14);
        }

        static void Scaffold(HillMesh m, Vector3 mn, Vector3 mx)
        {
            for (float z = mn.z + 0.1f; z <= mx.z - 0.1f; z += 1.95f)
                foreach (float x in new[] { mn.x + 0.06f, mx.x - 0.06f })
                    m.Cylinder(HillMat.Steel, new Vector3(x, mn.y, z), new Vector3(x, mx.y, z), 0.025f, 6, false);
            foreach (float y in new[] { mn.y + 0.05f, mx.y - 0.05f })
                foreach (float x in new[] { mn.x + 0.06f, mx.x - 0.06f })
                    m.Cylinder(HillMat.Steel, new Vector3(x, y, mn.z), new Vector3(x, y, mx.z), 0.025f, 6, false);
            m.Box(HillMat.Deck, new Vector3(mn.x, mx.y - 0.06f, mn.z), mx, 0.01f);
            m.Box(HillMat.FenceWood, new Vector3(mn.x + 0.02f, mn.y + 0.5f, mn.z), new Vector3(mx.x - 0.02f, mn.y + 0.75f, mx.z), 0.01f);
        }

        static void MillBridge(HillMesh m, Vector3 mn, Vector3 mx)
        {
            m.Box(HillMat.BrickDark, mn, mx, 0.04f);
            // The arch's soffit and a brick band over it; windows on its east face.
            m.Box(HillMat.StoneCoping, new Vector3(mn.x - 0.05f, mn.y - 0.02f, mn.z), new Vector3(mx.x + 0.05f, mn.y + 0.3f, mx.z), 0.03f);
            for (float z = mn.z + 1.2f; z + 1.2f < mx.z; z += 2.2f)
                for (float y = mn.y + 1.2f; y + 1.4f < mx.y; y += 3.4f)
                {
                    m.Box(HillMat.TrimCream, new Vector3(mx.x - 0.02f, y - 0.08f, z - 0.08f), new Vector3(mx.x + 0.06f, y + 1.48f, z + 1.08f), 0.01f);
                    m.Rect(HillMat.Interior, new Vector3(mx.x + 0.065f, y + 0.7f, z + 0.5f), new Vector3(0f, 0f, 1f), new Vector3(0f, 1.4f, 0f), Vector3.right, HillAtlas.Interior(7));
                    m.Rect(HillMat.Glass, new Vector3(mx.x + 0.07f, y + 0.7f, z + 0.5f), new Vector3(0f, 0f, 1f), new Vector3(0f, 1.4f, 0f), Vector3.right);
                }
        }

        // ---------- Wires between the power poles ----------

        static void DrawWires(LarchHill h, Chunks chunks)
        {
            var poles = h.props.FindAll(p => p.kind == HillPropKind.PowerPole);
            poles.Sort((a, b) => a.position.Z != b.position.Z ? a.position.Z.CompareTo(b.position.Z) : a.position.X.CompareTo(b.position.X));
            for (int i = 0; i + 1 < poles.Count; i++)
            {
                var a = poles[i];
                var b = poles[i + 1];
                if (Math.Abs(a.position.Z - b.position.Z) > 0.5f || Math.Abs(a.position.X - b.position.X) > 45f) continue;
                var ia = HillProps.PowerPoleInsulators(a);
                var ib = HillProps.PowerPoleInsulators(b);
                for (int k = 0; k < Mathf.Min(ia.Length, ib.Length); k++)
                {
                    var path = new List<Vector3>();
                    var rad = new List<float>();
                    for (int s = 0; s <= 10; s++)
                    {
                        float t = s / 10f;
                        var p = Vector3.Lerp(ia[k], ib[k], t);
                        p.y -= 0.7f * 4f * t * (1f - t);
                        path.Add(p);
                        rad.Add(0.012f);
                    }
                    chunks.At((ia[k] + ib[k]) * 0.5f).Tube(HillMat.MetalDark, path, rad, 4);
                }
            }
        }

        // ---------- Bunting over the party ----------

        static void DrawBunting(Chunks chunks)
        {
            string[] flags = { HillMat.CanvasRed, HillMat.CanvasBlue, HillMat.CanvasGreen, HillMat.Canvas };
            int f = 0;
            for (float x = -104f; x < -8f; x += 12f)
            {
                var a = new Vector3(x, LarchHill.RowanY + 4.6f, LarchHill.RowanHedgeZ1 + 0.3f);
                var b = new Vector3(x + 6f, LarchHill.RowanY + 4.6f, LarchHill.RowanNorthZ - 0.4f);
                var m = chunks.At((a + b) * 0.5f);
                for (int i = 0; i < 18; i++)
                {
                    float t = (i + 0.5f) / 18f;
                    var p = Vector3.Lerp(a, b, t);
                    p.y -= 0.9f * 4f * t * (1f - t);
                    var along = (b - a).normalized * 0.18f;
                    m.Poly(flags[f++ % flags.Length], new[] { p - along, p + along, p + Vector3.down * 0.32f }, Vector3.Cross(along, Vector3.up));
                    m.Poly(flags[f % flags.Length], new[] { p + along, p - along, p + Vector3.down * 0.32f }, -Vector3.Cross(along, Vector3.up));
                }
            }
        }
    }
}
