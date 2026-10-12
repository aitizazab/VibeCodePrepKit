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
                switch (part.look)
                {
                    case HouseLook.Wall: WallFaces(h, part, B(key)); break;
                    case HouseLook.Slab: SlabFaces(h, part, B(key)); break;
                    case HouseLook.Chimney: B(key).Box(U(p.Min), U(p.Max), HouseMaterials.Brick()); break;
                    case HouseLook.Rail: B(key).Box(U(p.Min), U(p.Max), HouseMaterials.TrimWalnut()); break;
                    case HouseLook.Cupboard: B(key).Box(U(p.Min), U(p.Max), HouseMaterials.TrimWalnut()); break;
                    case HouseLook.Glass: Glass(part, B("Glass")); break;
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
                    case HouseLook.Hedge: B("Outside").Box(U(p.Min), U(p.Max), HouseMaterials.Hedge()); break;
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

        /// <summary>Real steps over a stair's ramp (the ramp is the collider): treads (wood, a carpet runner up the middle
        /// indoors), white risers, the soffit under it.</summary>
        static void Stair(DreamHouse h, HousePart part, MeshBuilder b)
        {
            var p = part.piece;
            bool outside = part.look != HouseLook.Stair;
            var tread = outside ? HouseMaterials.Deck() : HouseMaterials.TrimWalnut();
            var riser = outside ? HouseMaterials.Deck() : HouseMaterials.TrimWhite();
            var runner = outside ? null : HouseMaterials.Fabric("StairRunner", new Color(0.36f, 0.42f, 0.55f));
            float lo = Mathf.Min(p.rampTopStart, p.rampTopEnd), hi = Mathf.Max(p.rampTopStart, p.rampTopEnd);
            int n = Mathf.Max(3, Mathf.RoundToInt((hi - lo) / 0.19f));
            float rise = (hi - lo) / n;
            bool ax = part.rampAlongX;
            bool upPositive = p.rampTopEnd > p.rampTopStart;
            float a0 = ax ? p.Min.X : p.Min.Z, a1 = ax ? p.Max.X : p.Max.Z, run = (a1 - a0) / n;
            float w0 = ax ? p.Min.Z : p.Min.X, w1 = ax ? p.Max.Z : p.Max.X;
            for (int k = 0; k < n; k++)
            {
                // Step k (from the bottom): its tread at lo + (k+1) rise, its run from the bottom end.
                float top = lo + (k + 1) * rise;
                float s0 = upPositive ? a0 + k * run : a1 - (k + 1) * run, s1 = s0 + run;
                float bottom = top - rise - 0.06f;
                Vector3 mn = ax ? new Vector3(s0, bottom, w0) : new Vector3(w0, bottom, s0);
                Vector3 mx = ax ? new Vector3(s1, top, w1) : new Vector3(w1, top, s1);
                // The face toward the bottom of the flight is the riser.
                var faces = new Material[6];
                faces[3] = tread;
                int riserFace = ax ? (upPositive ? 0 : 1) : (upPositive ? 4 : 5);
                for (int f = 0; f < 6; f++) if (f != 3 && f != 2) faces[f] = f == riserFace ? riser : tread;
                b.Box(mn, mx, faces);
                if (runner != null)
                {
                    float inset = (w1 - w0) * 0.2f;
                    Vector3 rmn = ax ? new Vector3(s0, top, w0 + inset) : new Vector3(w0 + inset, top, s0);
                    Vector3 rmx = ax ? new Vector3(s1, top + 0.008f, w1 - inset) : new Vector3(w1 - inset, top + 0.008f, s1);
                    b.Box(rmn, rmx, new[] { runner, runner, null, runner, runner, runner });
                }
            }
            // The soffit: the slab under the slope.
            RampSlab(part, b, null, outside ? HouseMaterials.Deck() : HouseMaterials.Ceiling(), HouseMaterials.TrimWhite());
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
            var b = new MeshBuilder();
            foreach (var part in h.parts)
            {
                if (part.look != HouseLook.Wall || !part.piece.solid) continue;
                var p = part.piece;
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
            var b = new MeshBuilder();
            var trim = HouseMaterials.TrimWhite();
            foreach (var w in h.windows)
            {
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
