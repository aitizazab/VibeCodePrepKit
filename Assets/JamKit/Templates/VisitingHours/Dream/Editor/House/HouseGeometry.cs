using System.Collections.Generic;
using JamKit.Movement;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using NVec3 = System.Numerics.Vector3;

namespace JamKit.DreamEscape.EditorTools
{
    /// <summary>
    /// The house's structure, drawn from core <see cref="DreamHouse"/>'s parts (the house's frame = the root's local space):
    /// every wall face in the finish of the room it faces (split where it passes from one room to the next; the creator's
    /// oxblood stripe over walnut panelling, tiles in the kitchen and bathrooms, painted plaster below stairs), skirting,
    /// chair rails and cornices, the floors and ceilings room by room, the stairs as real treads and risers, glass with frames,
    /// sills and glazing bars, door casings, the roofs and gables, and a collider for every solid part (runnable walls marked).
    /// Outside faces are clapboard over a concrete plinth.
    /// </summary>
    public static class HouseGeometry
    {
        static Vector3 U(NVec3 v) => new Vector3(v.X, v.Y, v.Z);

        /// <summary>Builds the house's structure and its colliders under <paramref name="root"/>.</summary>
        public static void Build(DreamHouse h, Transform root, bool withOutside)
        {
            var structure = new GameObject("Structure").transform;
            structure.SetParent(root, false);
            var colliders = new GameObject("Colliders").transform;
            colliders.SetParent(root, false);
            // One mesh per storey band and outside (fewer draw calls, still culled in pieces).
            var builders = new Dictionary<string, MeshBuilder>();
            var hedges = new List<HousePart>();
            MeshBuilder B(string key)
            {
                if (!builders.TryGetValue(key, out var b)) { b = new MeshBuilder(); builders[key] = b; }
                return b;
            }
            for (int i = 0; i < h.parts.Count; i++)
            {
                var part = h.parts[i];
                var p = part.piece;
                bool outside = IsOutsidePart(h, part);
                if (outside && !withOutside) continue;
                string key = Band(p.center.Y) + (outside ? " (outside)" : "");
                var mb = part.look == HouseLook.Glass ? B("Glass") : part.look == HouseLook.Roof ? B("Roofs") : part.look == HouseLook.Lawn || part.look == HouseLook.Path || part.look == HouseLook.Street ? B("Ground")
                    : part.look == HouseLook.Porch || part.look == HouseLook.Fence || part.look == HouseLook.Hedge || part.look == HouseLook.Wood || part.look == HouseLook.GarageDoor ? B("Outside") : B(key);
                mb.Owner = p.name; mb.OwnerPart = i; mb.Kind = DrawnKind.Owned;
                switch (part.look)
                {
                    case HouseLook.Wall: WallFaces(h, part, B(key)); break;
                    case HouseLook.Slab: SlabFaces(h, part, B(key)); break;
                    case HouseLook.Chimney: B(key).Box(U(p.Min), U(p.Max), HouseMaterials.Brick()); break;
                    case HouseLook.Rail: Rail(part, B(key)); break;
                    case HouseLook.Cupboard:
                        if (p.name.StartsWith("Under the stairs") || p.name.StartsWith("Cupboard")) Spandrel(h, part, B(key));
                        else B(key).Box(U(p.Min), U(p.Max), HouseMaterials.TrimWalnut());
                        break;
                    case HouseLook.Glass:
                        if (p.name.Contains("round window")) RoundWindow(h, part, B("Glass"), mb == B("Glass") ? B(key) : mb);
                        else Glass(part, B("Glass"));
                        break;
                    case HouseLook.Stair: Stair(h, part, B(key)); break;
                    case HouseLook.Roof:
                        if (p.kind == LabPieceKind.Ramp) RampSlab(part, B("Roofs"), HouseMaterials.Roof(), HouseMaterials.Planks(), HouseMaterials.TrimWhite());
                        else B("Roofs").Box(U(p.Min), U(p.Max), new[] { HouseMaterials.TrimWhite(), HouseMaterials.TrimWhite(), HouseMaterials.Planks(), HouseMaterials.Roof(), HouseMaterials.TrimWhite(), HouseMaterials.TrimWhite() });
                        break;
                    case HouseLook.Lawn: B("Ground").Box(U(p.Min), U(p.Max), new[] { HouseMaterials.Soil(), HouseMaterials.Soil(), null, HouseMaterials.Lawn(), HouseMaterials.Soil(), HouseMaterials.Soil() }); break;
                    case HouseLook.Path: B("Ground").Box(U(p.Min), U(p.Max), HouseMaterials.Paving()); break;
                    case HouseLook.Street: B("Ground").Box(U(p.Min), U(p.Max), HouseMaterials.Asphalt()); break;
                    case HouseLook.Porch:
                        if (p.kind == LabPieceKind.Ramp) Stair(h, part, B("Outside"));
                        else B("Outside").Box(U(p.Min), U(p.Max), HouseMaterials.Deck());
                        break;
                    case HouseLook.Fence: Fence(part, B("Outside")); break;
                    case HouseLook.Hedge:
                        // A dark core (so no gaps show through) under a row of Look's shrubs (a green box before).
                        B("Outside").Box(U(p.Min) + new Vector3(0.18f, 0f, 0.18f), U(p.Max) - new Vector3(0.18f, 0.25f, 0.18f), HouseMaterials.Look("Plaster_Painted", "HedgeCore", new Color(0.12f, 0.2f, 0.1f)));
                        hedges.Add(part);
                        break;
                    case HouseLook.Wood:
                        if (p.kind == LabPieceKind.Ramp) Stair(h, part, B("Outside"));
                        else B("Outside").Box(U(p.Min), U(p.Max), HouseMaterials.Deck());
                        break;
                    case HouseLook.GarageDoor: GarageDoor(part, B("Outside")); break;
                    case HouseLook.Trampoline:
                    case HouseLook.Tree:
                        break;   // the furnishing draws them
                }
                if (p.solid) Collider(part, colliders);
            }
            foreach (var kv in builders) kv.Value.Build("Structure: " + kv.Key, structure, kv.Key != "Glass");
            // The glass doesn't cast shadows (the sun comes in) and it's on the layer sight ignores.
            var glass = structure.Find("Structure: Glass");
            if (glass != null) { glass.gameObject.layer = 2; var r = glass.GetComponent<MeshRenderer>(); if (r != null) r.shadowCastingMode = ShadowCastingMode.Off; }
            Trims(h, structure);
            Openings(h, structure);
            if (hedges.Count > 0)
            {
                var hroot = new GameObject("Hedges").transform;
                hroot.SetParent(root, false);
                int seed = 11;
                foreach (var part in hedges)
                {
                    var p = part.piece;
                    bool alongX = p.size.X >= p.size.Z;
                    float len = alongX ? p.size.X : p.size.Z;
                    int n = Mathf.Max(2, Mathf.RoundToInt(len / 1.0f));
                    for (int k = 0; k < n; k++)
                    {
                        float t = (k + 0.5f) / n;
                        var at = alongX ? new Vector3(p.Min.X + p.size.X * t, p.Min.Y, p.center.Z) : new Vector3(p.center.X, p.Min.Y, p.Min.Z + p.size.Z * t);
                        var shrub = Look.EditorTools.VhLookTrees.Place(hroot, at, Look.EditorTools.VhLookTrees.Species.Shrub, seed++ % 3, p.size.Y, false, (seed * 47) % 360);
                        if (shrub == null) continue;
                        foreach (var c in shrub.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                    }
                }
            }
        }

        static string Band(float y) => y < -0.4f ? "basement" : y < 3.0f ? "ground" : y < 5.9f ? "upper" : "attic";

        /// <summary>The lot's ground, its fences and gates, the street and its footpath: the hill builds its own (World's
        /// HillAnchors: nothing of the hill's stands in the lot but those). Everything else in the lot is the house's.</summary>
        public static bool IsOutsidePart(DreamHouse h, HousePart part)
        {
            var n = part.piece.name;
            return part.look == HouseLook.Lawn || part.look == HouseLook.Street || part.look == HouseLook.Fence
                || n.StartsWith("Rowan Street") || n.StartsWith("The lot's edge") || n.StartsWith("The gate's steps");
        }

        // =====================================================================================================
        // Walls
        // =====================================================================================================

        /// <summary>A wall box's faces: the long faces in their rooms' finishes (segment by segment), the ends painted (door
        /// and window reveals), the top capped where nothing sits on it.</summary>
        static void WallFaces(DreamHouse h, HousePart part, MeshBuilder b)
        {
            var p = part.piece;
            Vector3 mn = U(p.Min), mx = U(p.Max);
            bool alongX = p.size.X >= p.size.Z;
            var trim = HouseMaterials.TrimWhite();
            if (alongX)
            {
                LongFace(h, b, mn, mx, -1, true);
                LongFace(h, b, mn, mx, +1, true);
                b.Quad(new Vector3(mn.x, mn.y, mx.z), new Vector3(mn.x, mx.y, mx.z), new Vector3(mn.x, mx.y, mn.z), new Vector3(mn.x, mn.y, mn.z), trim);
                b.Quad(new Vector3(mx.x, mn.y, mn.z), new Vector3(mx.x, mx.y, mn.z), new Vector3(mx.x, mx.y, mx.z), new Vector3(mx.x, mn.y, mx.z), trim);
            }
            else
            {
                LongFace(h, b, mn, mx, -1, false);
                LongFace(h, b, mn, mx, +1, false);
                b.Quad(new Vector3(mn.x, mn.y, mn.z), new Vector3(mn.x, mx.y, mn.z), new Vector3(mx.x, mx.y, mn.z), new Vector3(mx.x, mn.y, mn.z), trim);
                b.Quad(new Vector3(mx.x, mn.y, mx.z), new Vector3(mx.x, mx.y, mx.z), new Vector3(mn.x, mx.y, mx.z), new Vector3(mn.x, mn.y, mx.z), trim);
            }
            // The underside where it's a header over a door or a window (it shows), and the top where nothing sits on it.
            if (mn.y > -3.05f && !Touches(h, part, false)) b.Quad(new Vector3(mn.x, mn.y, mn.z), new Vector3(mx.x, mn.y, mn.z), new Vector3(mx.x, mn.y, mx.z), new Vector3(mn.x, mn.y, mx.z), trim);
            if (!Touches(h, part, true))
            {
                var room = h.RoomAt(new NVec3((mn.x + mx.x) * 0.5f, mx.y - 0.3f, (mn.z + mx.z) * 0.5f));
                var cap = room == null ? HouseMaterials.Siding() : HouseMaterials.TrimWalnut();
                b.Quad(new Vector3(mn.x, mx.y, mn.z), new Vector3(mn.x, mx.y, mx.z), new Vector3(mx.x, mx.y, mx.z), new Vector3(mx.x, mx.y, mn.z), cap);
            }
        }

        /// <summary>Whether another solid sits on (or under) this part across most of its face.</summary>
        static bool Touches(DreamHouse h, HousePart part, bool top)
        {
            var p = part.piece;
            float y = top ? p.Max.Y : p.Min.Y;
            float area = 0f, own = p.size.X * p.size.Z;
            foreach (var o in h.parts)
            {
                if (o == part || !o.piece.solid || o.piece.kind == LabPieceKind.Ramp) continue;
                var q = o.piece;
                if (top ? System.Math.Abs(q.Min.Y - y) > 0.02f : System.Math.Abs(q.Max.Y - y) > 0.02f) continue;
                float ox = System.Math.Min(p.Max.X, q.Max.X) - System.Math.Max(p.Min.X, q.Min.X);
                float oz = System.Math.Min(p.Max.Z, q.Max.Z) - System.Math.Max(p.Min.Z, q.Min.Z);
                if (ox > 0f && oz > 0f) area += ox * oz;
            }
            return area > own * 0.8f;
        }

        /// <summary>One long face of a wall (side -1: the face at min, +1: at max), split along its length where the room
        /// it faces changes, and up its height at the wainscot.</summary>
        static void LongFace(DreamHouse h, MeshBuilder b, Vector3 mn, Vector3 mx, int side, bool alongX)
        {
            float c = side < 0 ? (alongX ? mn.z : mn.x) : (alongX ? mx.z : mx.x);
            float a0 = alongX ? mn.x : mn.z, a1 = alongX ? mx.x : mx.z;
            float y0 = mn.y, y1 = mx.y;
            const float step = 0.1f;
            float segStart = a0;
            HouseRoom segRoom = null;
            bool first = true;
            for (float a = a0; ; a += step)
            {
                bool end = a >= a1 - 1e-4f;
                float am = Mathf.Min(a + step * 0.5f, a1 - 0.01f);
                var room = RoomFacing(h, alongX, c + side * 0.06f, am, y0, y1);
                if (first) { segRoom = room; first = false; }
                if (end || room != segRoom)
                {
                    float segEnd = end ? a1 : a;
                    Segment(b, alongX, side, c, segStart, segEnd, y0, y1, segRoom);
                    if (end) break;
                    segStart = a;
                    segRoom = room;
                }
            }
        }

        static HouseRoom RoomFacing(DreamHouse h, bool alongX, float c, float a, float y0, float y1)
        {
            // Sample a metre up (or the middle of a short face), then lower down if that's above a room.
            foreach (float k in new[] { 0.35f, 0.15f, 0.7f })
            {
                float y = Mathf.Min(y0 + Mathf.Max(0.3f, (y1 - y0) * k), y1 - 0.05f);
                var p = alongX ? new NVec3(a, y, c) : new NVec3(c, y, a);
                var r = h.RoomAt(p);
                if (r != null && !r.outside) return r;
            }
            return null;
        }

        static void Segment(MeshBuilder b, bool alongX, int side, float c, float a0, float a1, float y0, float y1, HouseRoom room)
        {
            if (a1 - a0 < 1e-3f) return;
            if (room == null)
            {
                // Outside: hidden below the ground; a concrete plinth up to 0.35 m over it, clapboard above.
                float g = DreamHouse.Yard;
                if (y1 <= g + 0.01f) return;
                float ys = Mathf.Max(y0, g - 0.05f), yp = Mathf.Clamp(g + 0.35f, ys, y1);
                FaceQuad(b, alongX, side, c, a0, a1, ys, yp, HouseMaterials.Foundation());
                FaceQuad(b, alongX, side, c, a0, a1, yp, y1, HouseMaterials.Siding());
                return;
            }
            var f = HouseMaterials.For(room.style);
            float w = f.wainscot != null ? room.floorY + f.wainscotHeight : y0;
            if (w > y0 + 1e-3f && w < y1) { FaceQuad(b, alongX, side, c, a0, a1, y0, w, f.wainscot); FaceQuad(b, alongX, side, c, a0, a1, w, y1, f.wall); }
            else if (w >= y1) FaceQuad(b, alongX, side, c, a0, a1, y0, y1, f.wainscot ?? f.wall);
            else FaceQuad(b, alongX, side, c, a0, a1, y0, y1, f.wall);
        }

        /// <summary>A vertical quad on a wall's face, facing out of the wall on <paramref name="side"/>.</summary>
        static void FaceQuad(MeshBuilder b, bool alongX, int side, float c, float a0, float a1, float y0, float y1, Material m)
        {
            if (y1 - y0 < 1e-3f || a1 - a0 < 1e-3f) return;
            if (alongX)
            {
                if (side < 0) b.Quad(new Vector3(a0, y0, c), new Vector3(a0, y1, c), new Vector3(a1, y1, c), new Vector3(a1, y0, c), m);
                else b.Quad(new Vector3(a1, y0, c), new Vector3(a1, y1, c), new Vector3(a0, y1, c), new Vector3(a0, y0, c), m);
            }
            else
            {
                if (side < 0) b.Quad(new Vector3(c, y0, a1), new Vector3(c, y1, a1), new Vector3(c, y1, a0), new Vector3(c, y0, a0), m);
                else b.Quad(new Vector3(c, y0, a0), new Vector3(c, y1, a0), new Vector3(c, y1, a1), new Vector3(c, y0, a1), m);
            }
        }

        // =====================================================================================================
        // Floors and ceilings
        // =====================================================================================================

        static void SlabFaces(DreamHouse h, HousePart part, MeshBuilder b)
        {
            var p = part.piece;
            Vector3 mn = U(p.Min), mx = U(p.Max);
            var plaster = HouseMaterials.Ceiling();
            // The top: each room's floor where a room stands on it.
            foreach (var room in h.rooms)
            {
                if (room.outside || System.Math.Abs(room.floorY - mx.y) > 0.02f) continue;
                var f = HouseMaterials.For(room.style);
                foreach (var r in room.rects)
                {
                    float x0 = Mathf.Max(r.x0, mn.x), x1 = Mathf.Min(r.x1, mx.x), z0 = Mathf.Max(r.z0, mn.z), z1 = Mathf.Min(r.z1, mx.z);
                    if (x1 - x0 < 1e-3f || z1 - z0 < 1e-3f) continue;
                    b.Quad(new Vector3(x0, mx.y, z0), new Vector3(x0, mx.y, z1), new Vector3(x1, mx.y, z1), new Vector3(x1, mx.y, z0), f.floor);
                }
            }
            // The underside: each room's ceiling.
            foreach (var room in h.rooms)
            {
                if (room.outside || System.Math.Abs(room.ceilingY - mn.y) > 0.02f) continue;
                var f = HouseMaterials.For(room.style);
                foreach (var r in room.rects)
                {
                    float x0 = Mathf.Max(r.x0, mn.x), x1 = Mathf.Min(r.x1, mx.x), z0 = Mathf.Max(r.z0, mn.z), z1 = Mathf.Min(r.z1, mx.z);
                    if (x1 - x0 < 1e-3f || z1 - z0 < 1e-3f) continue;
                    b.Quad(new Vector3(x0, mn.y, z0), new Vector3(x1, mn.y, z0), new Vector3(x1, mn.y, z1), new Vector3(x0, mn.y, z1), f.ceiling);
                }
            }
            // The edges (only the stairwells' show).
            var faces = new[] { plaster, plaster, null, null, plaster, plaster };
            b.Box(mn, mx, faces);
        }

        // =====================================================================================================
        // Stairs, ramps, roofs
        // =====================================================================================================

        /// <summary>Real stairs over a stair's ramp (the ramp is the hidden collider; nothing of it is drawn but the soffit
        /// under it). Every visible piece sits clear of every other (no shared faces, so nothing z-fights; house-1.md and
        /// the recorded run's stair): thin treads with a nosing and risers standing on the slope line (the collider's top
        /// runs through each step's inner corner), a closed stringer each side that hides the step ends (on a wall side
        /// only its room face is drawn: the wall is behind it), the soffit 6 cm under the slope line and inside the
        /// stringers, a carpet runner up the middle indoors. The treehouse's flight is a ladder instead: two rails and open
        /// slatted treads.</summary>
        static void Stair(DreamHouse h, HousePart part, MeshBuilder b)
        {
            var p = part.piece;
            bool outside = part.look != HouseLook.Stair;
            bool ladder = p.name.IndexOf("ladder", System.StringComparison.OrdinalIgnoreCase) >= 0;
            var tread = outside ? HouseMaterials.Deck() : HouseMaterials.TrimWalnut();
            var riser = outside ? HouseMaterials.Deck() : HouseMaterials.TrimWhite();
            var stringer = outside ? HouseMaterials.Deck() : HouseMaterials.TrimWhite();
            var runner = outside ? null : HouseMaterials.Fabric("StairRunner", new Color(0.36f, 0.42f, 0.55f));
            float lo = Mathf.Min(p.rampTopStart, p.rampTopEnd), hi = Mathf.Max(p.rampTopStart, p.rampTopEnd);
            int n = Mathf.Max(3, Mathf.RoundToInt((hi - lo) / 0.19f));
            float rise = (hi - lo) / n;
            bool ax = part.rampAlongX;
            bool upPositive = p.rampTopEnd > p.rampTopStart;
            float a0 = ax ? p.Min.X : p.Min.Z, a1 = ax ? p.Max.X : p.Max.Z, L = a1 - a0, run = L / n, slope = (hi - lo) / L;
            float w0 = ax ? p.Min.Z : p.Min.X, w1 = ax ? p.Max.Z : p.Max.X;
            // (u from the bottom end along the run, w across, y up) to the house's frame.
            System.Func<float, float, float, Vector3> P = (u, w, y) =>
            {
                float s = upPositive ? a0 + u : a1 - u;
                return ax ? new Vector3(s, y, w) : new Vector3(w, y, s);
            };
            void BoxU(float u0, float u1, float wa, float wb, float y0, float y1, Material m)
            {
                Vector3 m0 = P(u0, wa, y0), m1 = P(u1, wb, y1);
                b.Box(Vector3.Min(m0, m1), Vector3.Max(m0, m1), new[] { m, m, null, m, m, m });
            }
            if (ladder)
            {
                // Two rails along the slope (running on past the deck: handholds), open slats between them, no risers.
                const float rw = 0.07f;
                SlopeBoard(b, P, 0f, L, w0 + 0.02f, w0 + 0.02f + rw, lo, slope, -0.09f, 0.05f, 0.9f, tread);
                SlopeBoard(b, P, 0f, L, w1 - 0.02f - rw, w1 - 0.02f, lo, slope, -0.09f, 0.05f, 0.9f, tread);
                for (int k = 0; k < n; k++)
                {
                    float yT = lo + (k + 1) * rise, uBack = (k + 1) * run;
                    BoxU(uBack - 0.24f, uBack - 0.01f, w0 + 0.02f + rw + 0.003f, w1 - 0.02f - rw - 0.003f, yT - 0.04f, yT, tread);
                }
                return;
            }
            const float st = 0.045f;
            bool wallLow = SideIsWall(h, part, ax, w0, -1), wallHigh = SideIsWall(h, part, ax, w1, +1);
            float wi0 = w0 + st - 0.012f, wi1 = w1 - st + 0.012f;   // the steps' ends bed 1.2 cm into the stringers
            for (int k = 0; k < n; k++)
            {
                float yT = lo + (k + 1) * rise, u0 = k * run, u1 = (k + 1) * run;
                // The tread: a 3.5 cm board with a 2.5 cm nosing over the riser.
                BoxU(Mathf.Max(0f, u0 - 0.025f), u1, wi0, wi1, yT - 0.035f, yT, tread);
                // The riser: a 2 cm board from the slope line up under the tread.
                BoxU(u0, u0 + 0.02f, wi0, wi1, lo + k * rise, yT - 0.035f, riser);
                if (runner != null)
                {
                    float inset = (w1 - w0) * 0.2f;
                    BoxU(Mathf.Max(0f, u0 - 0.025f), u1 - 0.002f, w0 + inset, w1 - inset, yT, yT + 0.006f, runner);
                    if (u0 > 0.04f) BoxU(u0 - 0.031f, u0 - 0.025f, w0 + inset, w1 - inset, lo + k * rise + 0.03f, yT, runner);
                }
            }
            // The stringers: closed, from 30 cm under the slope line to a hand above the nosings, cut level at both ends.
            StringerSide(b, P, L, slope, lo, hi, rise, w0, w0 + st, stringer, wallLow ? -1 : 0);
            StringerSide(b, P, L, slope, lo, hi, rise, w1 - st, w1, stringer, wallHigh ? +1 : 0);
            // The soffit: 6 cm under the slope line, inside the stringers (its edges 1 cm into them): only its underside.
            var soffit = outside ? HouseMaterials.Deck() : HouseMaterials.Ceiling();
            SlopeBoard(b, P, 0.4f, L - 0.05f, w0 + st - 0.01f, w1 - st + 0.01f, lo, slope, -0.30f, -0.06f, 0f, null, soffit);
        }

        /// <summary>The panelling under the main stair's open side: painted panels whose top follows the flight's slope
        /// (12 cm under the slope line, tucked against the stringer) instead of dark stepped blocks; the stepped parts stay
        /// its colliders. A walnut capping strip along the slope, a skirting at the floor.</summary>
        static void Spandrel(DreamHouse h, HousePart part, MeshBuilder b)
        {
            var p = part.piece;
            HousePart stair = null;
            foreach (var o in h.parts) if (o.look == HouseLook.Stair && o.piece.name == "The main stairs") { stair = o; break; }
            Vector3 mn = U(p.Min), mx = U(p.Max);
            if (stair == null)
            {
                b.Box(mn, mx, HouseMaterials.TrimWhite());
                return;
            }
            float Top(float z) => Mathf.Max(p.Max.Y, DreamHouse.RampTop(stair, -1.1f, z) - 0.12f);
            bool alongZ = p.size.Z >= p.size.X;
            var paint = HouseMaterials.Paint("UnderStairsPanel", new Color(0.93f, 0.91f, 0.86f));
            var cap = HouseMaterials.TrimWalnut();
            float x0 = mn.x, x1 = mx.x, y0 = mn.y;
            if (!alongZ)
            {
                // A cross wall under the flight (the cupboard's end): flat-topped, as it is.
                b.Box(mn, mx, paint);
                return;
            }
            float z0 = mn.z, z1 = mx.z, t0 = Top(z0), t1 = Top(z1);
            var c = (mn + mx) * 0.5f;
            var a = new Vector3(x0, y0, z0); var bb = new Vector3(x0, t0, z0); var cc = new Vector3(x0, t1, z1); var d = new Vector3(x0, y0, z1);
            var a2 = new Vector3(x1, y0, z0); var b2 = new Vector3(x1, t0, z0); var c2 = new Vector3(x1, t1, z1); var d2 = new Vector3(x1, y0, z1);
            b.QuadFacing(a2, b2, c2, d2, paint, c);   // the hall's face
            b.QuadFacing(a, bb, cc, d, paint, c);     // the stair's side (behind the stringer)
            b.QuadFacing(bb, b2, c2, cc, cap, c);     // the sloped top
            b.QuadFacing(a, a2, b2, bb, paint, c);    // ends
            b.QuadFacing(d, d2, c2, cc, paint, c);
            // The capping strip proud of the hall face along the slope, and a skirting.
            b.QuadFacing(new Vector3(x1 + 0.012f, t0 - 0.06f, z0), new Vector3(x1 + 0.012f, t0, z0), new Vector3(x1 + 0.012f, t1, z1), new Vector3(x1 + 0.012f, t1 - 0.06f, z1), cap, c);
            if (y0 < 0.01f) b.Box(new Vector3(x1, y0, z0), new Vector3(x1 + 0.015f, y0 + 0.12f, z1), cap);
        }

        /// <summary>A rail: a washing line is a cord from end to end; a balustrade a rounded handrail on turned
        /// balusters over a base rail, with newel posts at its ends (it read as a solid walnut slab).</summary>
        static void Rail(HousePart part, MeshBuilder b)
        {
            var p = part.piece;
            Vector3 mn = U(p.Min), mx = U(p.Max);
            var size = mx - mn;
            bool alongX = size.x >= size.z;
            if (part.model != null && part.model.EndsWith(".line"))
            {
                var c = (mn + mx) * 0.5f;
                var a = alongX ? new Vector3(mn.x, c.y, c.z) : new Vector3(c.x, c.y, mn.z);
                var z = alongX ? new Vector3(mx.x, c.y, c.z) : new Vector3(c.x, c.y, mx.z);
                HouseShapes.Rod(b, a, z, Mathf.Min(0.006f, size.y * 0.4f), HouseEscapeLook.Clean("Washing line", new Color(0.92f, 0.9f, 0.84f), 0.3f), 6);
                return;
            }
            var wood = HouseMaterials.TrimWalnut();
            var white = HouseMaterials.TrimWhite();
            float len = alongX ? size.x : size.z, th = alongX ? size.z : size.x, h = size.y;
            Vector3 P(float t, float y) => alongX ? new Vector3(mn.x + t, mn.y + y, (mn.z + mx.z) * 0.5f) : new Vector3((mn.x + mx.x) * 0.5f, mn.y + y, mn.z + t);
            Vector3 S(float l, float hh, float w) => alongX ? new Vector3(l, hh, w) : new Vector3(w, hh, l);
            if (h < 0.4f) { b.Box(mn, mx, wood); return; }
            HouseShapes.RoundedBox(b, P(len * 0.5f, h - 0.035f), S(len, 0.07f, th), 0.025f, wood);
            b.Box(Vector3.Min(P(0f, 0f), P(len, 0f)) - (alongX ? new Vector3(0, 0, th * 0.4f) : new Vector3(th * 0.4f, 0, 0)), Vector3.Max(P(0f, 0.06f), P(len, 0.06f)) + (alongX ? new Vector3(0, 0, th * 0.4f) : new Vector3(th * 0.4f, 0, 0)), white);
            int n = Mathf.Max(2, Mathf.RoundToInt(len / 0.12f));
            for (int k = 1; k < n; k++)
            {
                float t = len * k / n;
                HouseShapes.Lathe(b, P(t, 0.06f), Quaternion.identity, new[] { new Vector2(0f, 0f), new Vector2(0.018f, 0f), new Vector2(0.018f, 0.08f), new Vector2(0.012f, 0.12f), new Vector2(0.016f, (h - 0.13f) * 0.5f), new Vector2(0.011f, h - 0.2f), new Vector2(0.017f, h - 0.1f), new Vector2(0f, h - 0.1f) }, white, 8);
            }
            foreach (float t in new[] { Mathf.Min(0.045f, len * 0.5f), Mathf.Max(len - 0.045f, len * 0.5f) })
                HouseShapes.RoundedBox(b, P(t, h * 0.5f), S(Mathf.Min(0.08f, len), h, th), 0.01f, wood);
        }

        /// <summary>Whether a wall stands against the stair's side at <paramref name="w"/> (side -1 the low w, +1 the high).</summary>
        static bool SideIsWall(DreamHouse h, HousePart stair, bool ax, float w, int side)
        {
            var p = stair.piece;
            float a0 = ax ? p.Min.X : p.Min.Z, a1 = ax ? p.Max.X : p.Max.Z;
            foreach (var o in h.parts)
            {
                if (o == stair || !o.piece.solid || o.look != HouseLook.Wall && o.look != HouseLook.Chimney) continue;
                var q = o.piece;
                float qa0 = ax ? q.Min.X : q.Min.Z, qa1 = ax ? q.Max.X : q.Max.Z, qw0 = ax ? q.Min.Z : q.Min.X, qw1 = ax ? q.Max.Z : q.Max.X;
                if (Mathf.Min(qa1, a1) - Mathf.Max(qa0, a0) < (a1 - a0) * 0.3f) continue;
                if (side < 0 ? Mathf.Abs(qw1 - w) < 0.02f : Mathf.Abs(qw0 - w) < 0.02f) return true;
            }
            return false;
        }

        /// <summary>A board along a flight's slope: from <paramref name="u0"/> to <paramref name="u1"/> (and
        /// <paramref name="extend"/> metres higher past the top: a ladder's handholds), between <paramref name="wa"/> and
        /// <paramref name="wb"/>, from <paramref name="below"/> to <paramref name="above"/> the slope line (vertical
        /// offsets). Faces <paramref name="m"/> (null: only the underside, in <paramref name="under"/>).</summary>
        static void SlopeBoard(MeshBuilder b, System.Func<float, float, float, Vector3> P, float u0, float u1, float wa, float wb, float lo, float slope, float below, float above, float extend, Material m, Material under = null)
        {
            float u1e = u1 + extend / Mathf.Max(0.05f, slope);
            Vector3 A(float u, float w, float off) => P(u, w, lo + u * slope + off);
            var c000 = A(u0, wa, below); var c100 = A(u1e, wa, below); var c010 = A(u0, wa, above); var c110 = A(u1e, wa, above);
            var c001 = A(u0, wb, below); var c101 = A(u1e, wb, below); var c011 = A(u0, wb, above); var c111 = A(u1e, wb, above);
            var centre = (c000 + c111) * 0.5f;
            b.QuadFacing(c000, c100, c101, c001, under ?? m, centre);   // underside
            if (m == null) return;
            b.QuadFacing(c010, c011, c111, c110, m, centre);   // top
            b.QuadFacing(c000, c010, c110, c100, m, centre);   // wa side
            b.QuadFacing(c001, c101, c111, c011, m, centre);   // wb side
            b.QuadFacing(c000, c001, c011, c010, m, centre);   // low end
            b.QuadFacing(c100, c110, c111, c101, m, centre);   // high end
        }

        /// <summary>A closed stringer (a hexagonal board in the flight's side plane): its foot on the lower floor, its
        /// bottom edge 30 cm under the slope line to under the landing's slab, cut level at the landing 6 cm above the
        /// floor, its top edge a hand above the nosings. <paramref name="wallSide"/> -1 / +1: a wall stands on that side
        /// (that face isn't drawn); 0: both faces.</summary>
        static void StringerSide(MeshBuilder b, System.Func<float, float, float, Vector3> P, float L, float slope, float lo, float hi, float rise, float wa, float wb, Material m, int wallSide)
        {
            float top = rise + 0.05f;
            var poly = new[]
            {
                new Vector2(0f, lo), new Vector2(Mathf.Min(L, 0.3f / slope), lo), new Vector2(L, hi - 0.3f),
                new Vector2(L, hi + 0.06f), new Vector2(Mathf.Max(0f, L - (top - 0.06f) / slope), hi + 0.06f), new Vector2(0f, lo + top),
            };
            int k = poly.Length;
            var fa = new Vector3[k]; var fb = new Vector3[k];
            for (int i = 0; i < k; i++) { fa[i] = P(poly[i].x, wa, poly[i].y); fb[i] = P(poly[i].x, wb, poly[i].y); }
            var centre = (P(L * 0.5f, wa, (lo + hi) * 0.5f) + P(L * 0.5f, wb, (lo + hi) * 0.5f)) * 0.5f;
            if (wallSide >= 0) for (int i = 1; i < k - 1; i++) b.TriFacing(fa[0], fa[i], fa[i + 1], m, centre);
            if (wallSide <= 0) for (int i = 1; i < k - 1; i++) b.TriFacing(fb[0], fb[i], fb[i + 1], m, centre);
            for (int i = 0; i < k; i++)
            {
                int j = (i + 1) % k;
                b.QuadFacing(fa[i], fa[j], fb[j], fb[i], m, centre);
            }
        }

        /// <summary>A ramp's slab (<see cref="HouseLint.RampThickness"/> under its slope) as a rotated box: its top, its
        /// underside, its edges (null skips a face).</summary>
        static void RampSlab(HousePart part, MeshBuilder b, Material top, Material under, Material edge)
        {
            var o = HouseLint.Of(part.piece, part.rampAlongX);
            var u = U(o.u0) * o.h.X; var v = U(o.u1) * o.h.Y; var w = U(o.u2) * o.h.Z;
            b.Obb(U(o.c), u, v, w, new[] { edge, edge, edge, edge, under, top });
        }

        // =====================================================================================================
        // Glass, fences, the garage door
        // =====================================================================================================

        static void Glass(HousePart part, MeshBuilder b)
        {
            var p = part.piece;
            b.Box(U(p.Min), U(p.Max), HouseMaterials.Glass());
        }

        /// <summary>Lena's round window (house-1.md: it was square and looked onto the roof): a 1.5 m circle in the square
        /// opening, the corners filled (clapboard outside, the observatory's panelling inside), a round reveal through the
        /// wall, white ring frames both sides, the glass disc with a cross of glazing bars. Its collider stays the square
        /// glass part.</summary>
        static void RoundWindow(DreamHouse h, HousePart part, MeshBuilder glassB, MeshBuilder b)
        {
            var p = part.piece;
            bool alongX = p.size.X >= p.size.Z;
            var c = U(p.center);
            float half = Mathf.Min(alongX ? p.size.X : p.size.Z, p.size.Y) * 0.5f, r = half - 0.06f;
            const float t = 0.15f;   // half the tower wall
            var right = alongX ? Vector3.right : Vector3.forward;
            var n = alongX ? Vector3.forward : Vector3.right;   // +n: outside (the tower's north wall faces +z)
            var outside = HouseMaterials.Siding();
            var inside = HouseMaterials.For("observatory").wall;
            var trim = HouseMaterials.TrimWhite();
            const int seg = 32;
            for (int side = -1; side <= 1; side += 2)
            {
                var face = c + n * (side * t);
                var m = side > 0 ? outside : inside;
                for (int i = 0; i < seg; i++)
                {
                    float a0 = i * Mathf.PI * 2f / seg, a1 = (i + 1) * Mathf.PI * 2f / seg;
                    Vector3 D(float a) => right * Mathf.Cos(a) + Vector3.up * Mathf.Sin(a);
                    Vector3 Sq(float a) { var d = D(a); float k = half / Mathf.Max(Mathf.Abs(Mathf.Cos(a)), Mathf.Abs(Mathf.Sin(a))); return d * k; }
                    var q0 = face + D(a0) * r; var q1 = face + D(a1) * r; var s1 = face + Sq(a1); var s0 = face + Sq(a0);
                    b.QuadFacing(q0, q1, s1, s0, m, face - n * side);
                    // The reveal through the wall, facing the window's axis.
                    var r0 = c + n * t + D(a0) * r; var r1 = c + n * t + D(a1) * r; var r2 = c - n * t + D(a1) * r; var r3 = c - n * t + D(a0) * r;
                    if (side > 0) b.QuadFacing(r0, r1, r2, r3, trim, c + D((a0 + a1) * 0.5f) * (r * 3f));
                }
                var rot = Quaternion.FromToRotation(Vector3.up, n * side);
                HouseShapes.Lathe(b, face, rot, new[] { new Vector2(r - 0.07f, 0f), new Vector2(r + 0.05f, 0f), new Vector2(r + 0.05f, 0.025f), new Vector2(r - 0.07f, 0.025f) }, trim, seg);
            }
            var disc = Quaternion.FromToRotation(Vector3.up, n);
            HouseShapes.Lathe(glassB, c - n * 0.004f, disc, new[] { new Vector2(0f, 0f), new Vector2(r, 0f), new Vector2(r, 0.008f), new Vector2(0f, 0.008f) }, HouseMaterials.Glass(), seg);
            HouseShapes.Rod(b, c - right * r, c + right * r, 0.016f, trim, 8);
            HouseShapes.Rod(b, c - Vector3.up * r, c + Vector3.up * r, 0.016f, trim, 8);
        }

        static void Fence(HousePart part, MeshBuilder b)
        {
            var p = part.piece;
            var boards = HouseMaterials.FenceBoards();
            b.Box(U(p.Min), U(p.Max), boards);
        }

        static void GarageDoor(HousePart part, MeshBuilder b)
        {
            var p = part.piece;
            var paint = HouseMaterials.Paint("GarageDoor", new Color(0.93f, 0.91f, 0.86f));
            b.Box(U(p.Min), U(p.Max), paint);
            // Its panels: four horizontal grooves on the outside.
            for (int k = 1; k < 4; k++)
            {
                float y = p.Min.Y + 0.3f + (p.size.Y - 0.3f) * k / 4f;
                b.Box(new Vector3(p.Min.X + 0.1f, y - 0.015f, p.Min.Z - 0.012f), new Vector3(p.Max.X - 0.1f, y + 0.015f, p.Min.Z), HouseMaterials.TrimWhite());
            }
        }

        // =====================================================================================================
        // Colliders
        // =====================================================================================================

        static void Collider(HousePart part, Transform parent)
        {
            var p = part.piece;
            var go = new GameObject("Collider: " + p.name);
            go.transform.SetParent(parent, false);
            if (p.kind == LabPieceKind.Ramp)
            {
                var o = HouseLint.Of(p, part.rampAlongX);
                go.transform.localPosition = U(o.c);
                go.transform.localRotation = Quaternion.LookRotation(U(o.u1), U(o.u2));
                var bc = go.AddComponent<BoxCollider>();
                // local axes: x = right (u0 or its opposite), y = up (u2), z = forward (u1)
                bc.size = new Vector3(o.h.X * 2f, o.h.Z * 2f, o.h.Y * 2f);
            }
            else
            {
                go.transform.localPosition = U(p.center);
                go.AddComponent<BoxCollider>().size = U(p.size);
            }
            go.AddComponent<MovementSurface>().runnable = p.runnable;
            // Glass blocks her like any wall (sight ignores it: core StealthWorld skips glass).
            GameObjectUtility.SetStaticEditorFlags(go, 0);
        }

        // =====================================================================================================
        // Trims: skirting, chair rails, cornices
        // =====================================================================================================

        static void Trims(DreamHouse h, Transform structure)
        {
            var b = new MeshBuilder { Kind = DrawnKind.Mounted };
            for (int pi = 0; pi < h.parts.Count; pi++)
            {
                var part = h.parts[pi];
                if (part.look != HouseLook.Wall || !part.piece.solid) continue;
                var p = part.piece;
                b.Owner = "Trim on " + p.name; b.OwnerPart = pi;
                bool alongX = p.size.X >= p.size.Z;
                for (int side = -1; side <= 1; side += 2)
                {
                    float c = side < 0 ? (alongX ? p.Min.Z : p.Min.X) : (alongX ? p.Max.Z : p.Max.X);
                    float a0 = alongX ? p.Min.X : p.Min.Z, a1 = alongX ? p.Max.X : p.Max.Z;
                    if (a1 - a0 < 0.3f) continue;
                    var room = RoomFacing(h, alongX, c + side * 0.06f, (a0 + a1) * 0.5f, p.Min.Y, p.Max.Y);
                    if (room == null) continue;
                    var f = HouseMaterials.For(room.style);
                    if (f.skirting != null && System.Math.Abs(p.Min.Y - room.floorY) < 0.03f) Strip(b, alongX, side, c, a0, a1, room.floorY, room.floorY + 0.13f, 0.018f, f.skirting);
                    float w = room.floorY + f.wainscotHeight;
                    if (f.rail != null && f.wainscot != null && w > p.Min.Y + 0.05f && w < p.Max.Y - 0.05f) Strip(b, alongX, side, c, a0, a1, w - 0.025f, w + 0.025f, 0.025f, f.rail);
                    if (f.crown && System.Math.Abs(p.Max.Y - room.ceilingY) < 0.03f) Strip(b, alongX, side, c, a0, a1, room.ceilingY - 0.11f, room.ceilingY, 0.07f, HouseMaterials.TrimWhite());
                }
            }
            b.Build("Trims (skirting, rails, cornices)", structure);
        }

        static void Strip(MeshBuilder b, bool alongX, int side, float c, float a0, float a1, float y0, float y1, float depth, Material m)
        {
            float c0 = side < 0 ? c - depth : c, c1 = side < 0 ? c : c + depth;
            if (alongX) b.Box(new Vector3(a0, y0, c0), new Vector3(a1, y1, c1), m);
            else b.Box(new Vector3(c0, y0, a0), new Vector3(c1, y1, a1), m);
        }

        // =====================================================================================================
        // Windows (frames, sills, glazing bars) and doors (casings)
        // =====================================================================================================

        static void Openings(DreamHouse h, Transform structure)
        {
            var b = new MeshBuilder { Kind = DrawnKind.Mounted, Owner = "Window frames and door casings" };
            var trim = HouseMaterials.TrimWhite();
            foreach (var w in h.windows)
            {
                if (w.name == "The round window") continue;   // drawn round (RoundWindow)
                float half = w.thickness * 0.5f;
                for (int side = -1; side <= 1; side += 2)
                {
                    float face = w.c + side * half;
                    Casing(b, w.alongX, side, face, w.a, w.b, w.y0, w.y1, 0.07f, 0.02f, trim, true);
                    // The sill: a board under the window, a little proud and wider.
                    Board(b, w.alongX, side, face, w.a - 0.06f, w.b + 0.06f, w.y0 - 0.04f, w.y0, 0.06f, trim);
                }
                // Glazing bars (a cross; six-over-six on the big ones), in the glass's plane.
                int cols = (w.b - w.a) > 1.2f ? 3 : 2, rows = (w.y1 - w.y0) > 1.3f ? 2 : 1;
                for (int i = 1; i < cols; i++)
                {
                    float a = w.a + (w.b - w.a) * i / cols;
                    Bar(b, w.alongX, w.c, a - 0.015f, a + 0.015f, w.y0, w.y1, 0.03f, trim);
                }
                for (int j = 1; j <= rows; j++)
                {
                    float y = w.y0 + (w.y1 - w.y0) * j / (rows + 1);
                    Bar(b, w.alongX, w.c, w.a, w.b, y - 0.015f, y + 0.015f, 0.03f, trim);
                }
            }
            foreach (var d in h.doors)
            {
                float half = d.thickness * 0.5f;
                if (d.thickness < 0.09f) continue;
                for (int side = -1; side <= 1; side += 2)
                    Casing(b, d.alongX, side, d.c + side * half, d.a, d.b, d.y0, d.y1, 0.08f, 0.02f, trim, false);
            }
            b.Build("Window frames and door casings", structure);
        }

        /// <summary>A casing round an opening on one face: two jambs and a head (and a foot for windows).</summary>
        static void Casing(MeshBuilder b, bool alongX, int side, float face, float a0, float a1, float y0, float y1, float width, float depth, Material m, bool foot)
        {
            Board(b, alongX, side, face, a0 - width, a0, y0, y1 + width, depth, m);
            Board(b, alongX, side, face, a1, a1 + width, y0, y1 + width, depth, m);
            Board(b, alongX, side, face, a0, a1, y1, y1 + width, depth, m);
            if (foot) Board(b, alongX, side, face, a0, a1, y0 - width, y0, depth * 0.5f, m);
        }

        static void Board(MeshBuilder b, bool alongX, int side, float face, float a0, float a1, float y0, float y1, float depth, Material m)
        {
            float c0 = side < 0 ? face - depth : face, c1 = side < 0 ? face : face + depth;
            if (alongX) b.Box(new Vector3(a0, y0, c0), new Vector3(a1, y1, c1), m);
            else b.Box(new Vector3(c0, y0, a0), new Vector3(c1, y1, a1), m);
        }

        static void Bar(MeshBuilder b, bool alongX, float c, float a0, float a1, float y0, float y1, float depth, Material m)
        {
            if (alongX) b.Box(new Vector3(a0, y0, c - depth * 0.5f), new Vector3(a1, y1, c + depth * 0.5f), m);
            else b.Box(new Vector3(c - depth * 0.5f, y0, a0), new Vector3(c + depth * 0.5f, y1, a1), m);
        }
    }
}
