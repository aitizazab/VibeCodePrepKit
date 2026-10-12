using System;
using System.Collections.Generic;
using System.Numerics;
using JamKit.Movement;
using JamKit.Stealth;

namespace JamKit.DreamEscape
{
    /// <summary>What a house part is (the movement and stealth cores only see boxes; the builder picks the material from
    /// this and from the room each face looks into).</summary>
    public enum HouseLook
    {
        /// <summary>A wall: each face takes the finish of the room it faces (outside: the siding).</summary>
        Wall,
        /// <summary>A floor/ceiling slab: its top is the floor of the room above it, its underside the ceiling below.</summary>
        Slab,
        Stair, Glass, Rail, Furniture, DoorLeaf, Cupboard, Picture, Cloth, Rug,
        // Outside
        Lawn, Path, Hedge, Fence, Roof, Chimney, Tree, Wood, Trampoline, Porch, Street, GarageDoor, Shed,
        // The escape (the house as a fever dream): what each surface used to be.
        EscWallpaperFloor, EscFloorboardWall, EscCeiling, EscStairs, EscTable, EscDoor, EscCorridorFloor, EscCorridorWall,
        EscAtticBoards, EscSkyBoards, EscLastHall, EscFrontDoor, EscRain, EscFurniture,
    }

    /// <summary>A box of the house: the movement/collision piece, its look, and (furniture, props) the model the builder
    /// fits into it. <see cref="LabPiece.solid"/> is false only for the few see-through soft things (sheets on a line).</summary>
    public sealed class HousePart
    {
        public LabPiece piece;
        public HouseLook look;
        /// <summary>A model role from Dream/House/house-assets.json (e.g. "dining.table"), or null for a built box.</summary>
        public string model;
        /// <summary>Which way the model faces (degrees, 0 = +z).</summary>
        public float yaw;
        /// <summary>A ramp whose top slopes along x (from <see cref="LabPiece.rampTopStart"/> at its min x).</summary>
        public bool rampAlongX;
        public string room;
        /// <summary>Stairs: the ramp is the collider; the builder draws real steps over it.</summary>
        public bool steps;
    }

    /// <summary>A rectangle on the plan (x, z).</summary>
    public struct PlanRect
    {
        public float x0, x1, z0, z1;
        public PlanRect(float x0, float x1, float z0, float z1) { this.x0 = x0; this.x1 = x1; this.z0 = z0; this.z1 = z1; }
        public bool Contains(float x, float z) => x > x0 && x < x1 && z > z0 && z < z1;
        public float CX => (x0 + x1) * 0.5f;
        public float CZ => (z0 + z1) * 0.5f;
        public float Area => (x1 - x0) * (z1 - z0);
    }

    /// <summary>A room: its inner rectangles (to the wall faces), floor and ceiling heights, its finish and its light.</summary>
    public sealed class HouseRoom
    {
        public string id, name;
        public int storey;
        public float floorY, ceilingY;
        public readonly List<PlanRect> rects = new List<PlanRect>();
        /// <summary>The finish (the builder's material set): "oxblood" (the creator's regency stripe over walnut), "kitchen",
        /// "bath", "theo", "guest", "basement", "garage", "attic", "observatory", "study", "hall", "yard".</summary>
        public string style;
        /// <summary>Stealth light (0 dark .. 1 sunlit).</summary>
        public float light = 0.7f;
        /// <summary>Outside (the yard): no walls of its own, the sky overhead.</summary>
        public bool outside;
        public float x0 => rects.Count > 0 ? rects[0].x0 : 0f;
        public float x1 => rects.Count > 0 ? rects[0].x1 : 0f;
        public float z0 => rects.Count > 0 ? rects[0].z0 : 0f;
        public float z1 => rects.Count > 0 ? rects[0].z1 : 0f;
        public float ceiling => ceilingY - floorY;
        public float Area
        {
            get { float a = 0f; foreach (var r in rects) a += r.Area; return a; }
        }
        public bool Contains(float x, float z)
        {
            for (int i = 0; i < rects.Count; i++) if (rects[i].Contains(x, z)) return true;
            return false;
        }
        public bool Contains(Vector3 p) => p.Y >= floorY - 0.35f && p.Y < ceilingY + 0.05f && Contains(p.X, p.Z);
        public Vector3 Center => new Vector3(rects[0].CX, floorY, rects[0].CZ);
    }

    /// <summary>A memory to find (the hide-and-seek's collectibles): where it is, where she stands to take it, and the
    /// other places the house can hide it again when Theo catches her (the catch's cost).</summary>
    public sealed class HouseMemory
    {
        public string id, name, room;
        /// <summary>The picture shown when she takes it (Art/Dream, without extension), or null.</summary>
        public string picture;
        public Vector3 position, standAt;
        public readonly List<Vector3> altPositions = new List<Vector3>();
        public readonly List<Vector3> altStandAt = new List<Vector3>();
        public readonly List<string> altRooms = new List<string>();
        /// <summary>The place card: found by sitting down at the table, before the hide-and-seek.</summary>
        public bool atTable;
    }

    /// <summary>A stealth light zone.</summary>
    public sealed class HouseLight
    {
        public string name;
        public Vector3 center, size;
        public float level;
    }

    /// <summary>A lamp the builder lights (warm pendants, table lamps, the fire, the porch).</summary>
    public sealed class HouseLamp
    {
        public string name, kind;
        public Vector3 position;
        public float range = 6f, intensity = 1f;
    }

    /// <summary>A window in a wall: the builder frames it, and the sun comes through it.</summary>
    public sealed class HouseWindow
    {
        public string name;
        /// <summary>The wall runs along x (its faces look ±z).</summary>
        public bool alongX;
        public float c, a, b, y0, y1, thickness;
        public bool exterior;
    }

    /// <summary>A doorway (the builder cases it).</summary>
    public sealed class HouseDoor
    {
        public string name;
        public bool alongX;
        public float c, a, b, y0, y1, thickness;
        public bool exterior;
    }

    /// <summary>A flight of stairs (the ramp collider is a <see cref="HousePart"/>; this is for the drawn steps).</summary>
    public sealed class HouseStair
    {
        public string name;
        public PlanRect rect;
        public float bottomY, topY;
        /// <summary>It climbs along x (else along z).</summary>
        public bool alongX;
        /// <summary>+1: it climbs toward +x/+z; -1: toward -x/-z.</summary>
        public int up;
        public int rises;
    }

    /// <summary>A Theo walking point and its room.</summary>
    public struct TheoNode
    {
        public Vector3 p;
        public string room;
    }

    /// <summary>
    /// Night 2's house (the redesign of 2026-10-12, docs/redesign/house.md): the Lasname family home as data, in the house's
    /// own frame: the front door in the south wall at x 0 (the street is -z), the ground floor at y 0. Two floors, a
    /// basement, the attic with Lena's observatory tower, the attached garage and mudroom, the front garden and the back
    /// yard with Theo's treehouse, trampoline and shed. Every visible solid is a <see cref="HousePart"/> with a collider,
    /// and none of them overlap (<see cref="HouseLint"/>). Pancakes and the hide-and-seek happen here; the escape is
    /// <see cref="HouseEscape"/>, the fever-dream course the house throws her into.
    /// </summary>
    public sealed partial class DreamHouse
    {
        public const string ChapterArrival = "The house";
        public const string ChapterHideAndSeek = "Hide and seek";
        public const string ChapterArgument = "The argument";
        public const string ChapterTips = "The house tips";
        public const string ChapterFrontDoor = "The front door";

        // ---------- The plan's numbers (metres, the house's frame) ----------
        /// <summary>The main block's outer walls' centre lines.</summary>
        public const float X0 = -7.5f, X1 = 7.5f, Z0 = 0f, Z1 = 11f;
        public const float ExtT = 0.3f, IntT = 0.15f, SlabT = 0.3f;
        /// <summary>Finished floor levels: basement, ground, upper, attic.</summary>
        public const float Basement = -2.8f, Ground = 0f, Upper = 3.2f, Attic = 6.1f;
        /// <summary>Ceiling heights (each floor's ceiling is the underside of the next slab).</summary>
        public const float BasementH = 2.5f, GroundH = 2.9f, UpperH = 2.6f;
        /// <summary>The attic's knee walls and ridge (the ridge runs east-west over z <see cref="RidgeZ"/>).</summary>
        public const float KneeWall = 0.9f, RidgeHeight = 3.4f, RidgeZ = 5.5f;
        /// <summary>Lena's observatory tower over the attic's west end (the creator approved it), its ceiling
        /// <see cref="TowerH"/> over the attic floor, a crescent skylight over the telescope.</summary>
        public const float TowerX0 = X0, TowerX1 = -3.0f, TowerZ0 = 3.0f, TowerZ1 = 8.0f, TowerH = 5.0f;
        /// <summary>The dormer over the attic stairs' top, so she can stand up there.</summary>
        public const float DormerX0 = 1.7f, DormerX1 = 6.4f, DormerZ0 = 7.0f, DormerH = 2.4f;
        /// <summary>The garage and the mudroom (east of the main block).</summary>
        public const float GarageX1 = 12f, GarageZ1 = 7.5f, GarageFloor = 0f, GarageH = 2.8f, MudX1 = 10.5f;
        /// <summary>The ground outside (the lot's gardens: level with the front door, World's HillAnchors.LotGroundY).</summary>
        public const float Yard = 0f;
        /// <summary>The garden's fence (x) and the back yard's end (z); the front garden's edge.</summary>
        public const float YardX0 = -14f, YardX1 = 18f, YardZ1 = 22f, FrontZ0 = -6f;
        /// <summary>The street's far kerb (the arrival, a stand-in for the hill's street until the house sits on it).</summary>
        public const float StreetZ0 = -20f;
        public const float VoidBottom = -40f;

        // For the hill/street builders that place the house's outside: the front at z 0, the outside walls' x span, the
        // eaves over the ground floor, the attic floor.
        public const float OutX0 = X0 - ExtT * 0.5f, OutX1 = GarageX1 + ExtT * 0.5f, EaveY = Attic + KneeWall, AtticFloor = Attic;
        public const float FrontDoorZ = 0f;

        // ---------- The data ----------
        public readonly List<HousePart> parts = new List<HousePart>();
        public readonly List<HouseRoom> rooms = new List<HouseRoom>();
        public readonly List<HouseWindow> windows = new List<HouseWindow>();
        public readonly List<HouseDoor> doors = new List<HouseDoor>();
        public readonly List<HouseStair> stairs = new List<HouseStair>();
        public readonly List<HouseLamp> lamps = new List<HouseLamp>();
        public readonly List<HidingSpot> spots = new List<HidingSpot>();
        public readonly List<string> spotRooms = new List<string>();
        public readonly List<TheoNode> nodes = new List<TheoNode>();
        /// <summary>Walks between floors (the stairs, the treehouse's ladder, the porch steps): node index pairs.</summary>
        public readonly List<(int a, int b)> links = new List<(int, int)>();
        public readonly List<HouseLight> lights = new List<HouseLight>();
        public readonly List<HouseMemory> memories = new List<HouseMemory>();
        /// <summary>Places in the open (tests, the recorded run).</summary>
        public readonly List<Vector3> openFloor = new List<Vector3>();
        public float ambientLight = 0.75f;

        /// <summary>The arrival (the street), the front door's threshold, the table and the people at it.</summary>
        public Vector3 arrival, frontDoor, table, lenaAt, theoAt, nadiaChair;
        public float arrivalYaw, nadiaChairYaw;
        /// <summary>Theo counts at the table, his face in his arms.</summary>
        public Vector3 theoStart;
        public float theoCountYaw;
        /// <summary>The front hall breathing as she comes in (the dolly zoom that foreshadows the escape): from, to.</summary>
        public Vector3 hallBreathFrom, hallBreathTo;
        /// <summary>Lena watches from the tower's round window as she leaves (seen from the hill).</summary>
        public Vector3 lenaWindow;
        /// <summary>Theo's trampoline (his lookout in the yard).</summary>
        public Vector3 trampoline;

        public static DreamHouse Build()
        {
            var h = new DreamHouse();
            h.Make();
            return h;
        }

        // ---------- Queries ----------

        /// <summary>The room a point is in (feet, or a sample just off a wall's face), or null (outside the rooms).</summary>
        public HouseRoom RoomAt(Vector3 p)
        {
            HouseRoom best = null;
            float bestArea = float.MaxValue;
            for (int i = 0; i < rooms.Count; i++)
            {
                var r = rooms[i];
                if (!r.Contains(p)) continue;
                float area = r.Area;
                if (area < bestArea) { bestArea = area; best = r; }   // the smaller wins (a cupboard inside a room)
            }
            return best;
        }

        public HouseRoom Room(string id)
        {
            foreach (var r in rooms) if (r.id == id) return r;
            return null;
        }

        public int SpotAt(Vector3 feet)
        {
            for (int i = 0; i < spots.Count; i++) if (spots[i].Contains(feet)) return i;
            return -1;
        }

        public HouseMemory Memory(string id)
        {
            foreach (var m in memories) if (m.id == id) return m;
            return null;
        }

        /// <summary>A stealth world of the house: every solid blocks sight except glass (Theo can see through a window) and
        /// the stairs' ramps (their boxes aren't their shape); the light zones.</summary>
        public StealthBoxWorld StealthWorld()
        {
            var w = new StealthBoxWorld { ambient = ambientLight };
            foreach (var part in parts)
            {
                var p = part.piece;
                if (!p.solid || part.look == HouseLook.Glass) continue;
                if (p.kind == LabPieceKind.Ramp)
                {
                    // A ramp's box isn't its shape: the roof is over everything (skip it); a stair blocks in steps under its slope.
                    if (part.look == HouseLook.Roof) continue;
                    const int n = 6;
                    for (int k = 0; k < n; k++)
                    {
                        float a0 = (float)k / n, a1 = (float)(k + 1) / n;
                        var mn = p.Min; var mx = p.Max;
                        float lowTop = Math.Min(p.rampTopStart + (p.rampTopEnd - p.rampTopStart) * a0, p.rampTopStart + (p.rampTopEnd - p.rampTopStart) * a1);
                        Vector3 bmin, bmax;
                        if (part.rampAlongX) { bmin = new Vector3(mn.X + p.size.X * a0, mn.Y, mn.Z); bmax = new Vector3(mn.X + p.size.X * a1, lowTop, mx.Z); }
                        else { bmin = new Vector3(mn.X, mn.Y, mn.Z + p.size.Z * a0); bmax = new Vector3(mx.X, lowTop, mn.Z + p.size.Z * a1); }
                        if (bmax.Y - bmin.Y > 0.05f) w.AddBlocker((bmin + bmax) * 0.5f, bmax - bmin);
                    }
                    continue;
                }
                w.AddBlocker(p.center, p.size);
            }
            foreach (var l in lights) w.AddLight(l.center, l.size, l.level);
            return w;
        }

        /// <summary>Theo's walking map: points on each floor joined where the walk is clear (knee and chest height), only on
        /// the same floor; the stairs and the ladder by <see cref="links"/>.</summary>
        public WaypointGraph Graph(IStealthWorld world, float connectDistance = 8f)
        {
            var g = new WaypointGraph();
            foreach (var n in nodes) g.Add(n.p);
            for (int a = 0; a < nodes.Count; a++)
                for (int b = a + 1; b < nodes.Count; b++)
                {
                    var pa = nodes[a].p;
                    var pb = nodes[b].p;
                    if (Math.Abs(pa.Y - pb.Y) > 0.35f) continue;
                    if (Vector3.Distance(pa, pb) > connectDistance) continue;
                    if (WalkClear(world, pa, pb)) g.Connect(a, b);
                }
            foreach (var (a, b) in links) g.Connect(a, b);
            return g;
        }

        /// <summary>A walk is clear for a body (not just a line): the centre line and two lines either side of it (0.28 m out),
        /// at knee and chest height, so a walk can't thread a door's corner.</summary>
        public static bool WalkClear(IStealthWorld world, Vector3 a, Vector3 b, float halfWidth = 0.28f)
        {
            if (world == null) return true;
            if (!WaypointGraph.WalkClear(world, a, b)) return false;
            // Low things too (a fire pit, a step): an ankle-high line.
            var ankle = new Vector3(0f, 0.15f, 0f);
            if (!world.LineClear(a + ankle, b + ankle)) return false;
            var d = new Vector3(b.X - a.X, 0f, b.Z - a.Z);
            if (d.LengthSquared() < 1e-6f) return true;
            var side = Vector3.Normalize(new Vector3(-d.Z, 0f, d.X)) * halfWidth;
            return WaypointGraph.WalkClear(world, a + side, b + side) && WaypointGraph.WalkClear(world, a - side, b - side);
        }

        // ---------- The kit: walls with openings, slabs, stairs, furniture ----------

        string _room;

        HousePart Part(string name, LabPieceKind kind, HouseLook look, Vector3 min, Vector3 max, bool solid = true, bool runnable = false, string model = null, float yaw = 0f)
        {
            var p = new LabPiece { name = name, kind = kind, center = (min + max) * 0.5f, size = max - min, runnable = runnable, solid = solid };
            var part = new HousePart { piece = p, look = look, model = model, yaw = yaw, room = _room };
            parts.Add(part);
            return part;
        }

        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        /// <summary>A door, an archway, a low cupboard door or a window in a wall: from <see cref="a"/> to <see cref="b"/>
        /// along the wall, from <see cref="sill"/> to <see cref="head"/> above the wall's foot.</summary>
        public struct Opening
        {
            public float a, b, sill, head;
            public bool window;
            public string name;
            public static Opening Door(float a, float b, string name = "Door", float head = 2.1f) => new Opening { a = a, b = b, sill = 0f, head = head, name = name };
            public static Opening Arch(float a, float b, string name = "Archway", float head = 2.4f) => new Opening { a = a, b = b, sill = 0f, head = head, name = name };
            public static Opening Low(float a, float b, string name = "Low door") => new Opening { a = a, b = b, sill = 0f, head = 1.3f, name = name };
            public static Opening Window(float a, float b, float sill, float head, string name = "Window") => new Opening { a = a, b = b, sill = sill, head = head, window = true, name = name };
        }

        /// <summary>A wall running along x at z = <paramref name="z"/> (its centre line), from x <paramref name="x0"/> to
        /// <paramref name="x1"/>, from <paramref name="y0"/> to <paramref name="y1"/>, split round its openings: the pieces
        /// between, a header over each door, a sill and a head round each window with its glass. Every piece longer than
        /// 1.5 m is runnable (the creator: walls you'd expect to run on run).</summary>
        void WallX(string name, float z, float x0, float x1, float y0, float y1, float t, params Opening[] openings) =>
            Wall(name, true, z, x0, x1, y0, y1, t, openings);

        void WallZ(string name, float x, float z0, float z1, float y0, float y1, float t, params Opening[] openings) =>
            Wall(name, false, x, z0, z1, y0, y1, t, openings);

        void Wall(string name, bool alongX, float c, float a0, float a1, float y0, float y1, float t, Opening[] openings)
        {
            var list = new List<Opening>(openings ?? new Opening[0]);
            list.Sort((p, q) => p.a.CompareTo(q.a));
            bool exterior = t >= ExtT - 1e-3f;
            float at = a0;
            int seg = 0;
            foreach (var o in list)
            {
                if (o.a < a0 - 1e-3f || o.b > a1 + 1e-3f || o.a < at - 1e-3f)
                    throw new InvalidOperationException(name + ": the " + o.name + " is outside the wall or overlaps another opening");
                if (o.a - at > 1e-3f) WallBox(name + " " + (++seg), alongX, c, at, o.a, y0, y1, t);
                float head = Math.Min(y0 + o.head, y1);
                if (y1 - head > 1e-3f) WallBox(name + " (over the " + o.name.ToLowerInvariant() + ")", alongX, c, o.a, o.b, head, y1, t);
                if (o.window)
                {
                    float sill = y0 + o.sill;
                    if (sill - y0 > 1e-3f) WallBox(name + " (under the " + o.name.ToLowerInvariant() + ")", alongX, c, o.a, o.b, y0, sill, t);
                    // The glass: solid (a closed window), thin, in the wall's middle.
                    var gmin = alongX ? V(o.a, sill, c - 0.01f) : V(c - 0.01f, sill, o.a);
                    var gmax = alongX ? V(o.b, head, c + 0.01f) : V(c + 0.01f, head, o.b);
                    Part(name + " glass (" + o.name.ToLowerInvariant() + ")", LabPieceKind.Wall, HouseLook.Glass, gmin, gmax);
                    windows.Add(new HouseWindow { name = o.name, alongX = alongX, c = c, a = o.a, b = o.b, y0 = sill, y1 = head, thickness = t, exterior = exterior });
                }
                else doors.Add(new HouseDoor { name = o.name, alongX = alongX, c = c, a = o.a, b = o.b, y0 = y0, y1 = head, thickness = t, exterior = exterior });
                at = o.b;
            }
            if (a1 - at > 1e-3f) WallBox(name + (seg > 0 ? " " + (++seg) : ""), alongX, c, at, a1, y0, y1, t);
        }

        void WallBox(string name, bool alongX, float c, float a, float b, float y0, float y1, float t)
        {
            var min = alongX ? V(a, y0, c - t * 0.5f) : V(c - t * 0.5f, y0, a);
            var max = alongX ? V(b, y1, c + t * 0.5f) : V(c + t * 0.5f, y1, b);
            bool runnable = (b - a) >= 1.5f && (y1 - y0) >= 2f;
            Part(name, LabPieceKind.Wall, HouseLook.Wall, min, max, true, runnable);
        }

        /// <summary>A slab (the floor above it, the ceiling under it) with its top at <paramref name="top"/>, over a
        /// rectangle, with holes cut out (the stairwells).</summary>
        void Slab(string name, float x0, float x1, float z0, float z1, float top, params PlanRect[] holes)
        {
            int i = 0;
            foreach (var r in Subtract(new PlanRect(x0, x1, z0, z1), holes))
                Part(name + (i++ > 0 ? " " + i : ""), LabPieceKind.Floor, HouseLook.Slab, V(r.x0, top - SlabT, r.z0), V(r.x1, top, r.z1));
        }

        /// <summary>A rectangle minus holes, as rectangles that don't overlap.</summary>
        public static List<PlanRect> Subtract(PlanRect r, PlanRect[] holes)
        {
            var result = new List<PlanRect> { r };
            if (holes == null) return result;
            foreach (var h in holes)
            {
                var next = new List<PlanRect>();
                foreach (var q in result)
                {
                    if (h.x1 <= q.x0 || h.x0 >= q.x1 || h.z1 <= q.z0 || h.z0 >= q.z1) { next.Add(q); continue; }
                    float hz0 = Math.Max(q.z0, h.z0), hz1 = Math.Min(q.z1, h.z1), hx0 = Math.Max(q.x0, h.x0), hx1 = Math.Min(q.x1, h.x1);
                    if (hz0 > q.z0 + 1e-4f) next.Add(new PlanRect(q.x0, q.x1, q.z0, hz0));
                    if (hz1 < q.z1 - 1e-4f) next.Add(new PlanRect(q.x0, q.x1, hz1, q.z1));
                    if (hx0 > q.x0 + 1e-4f) next.Add(new PlanRect(q.x0, hx0, hz0, hz1));
                    if (hx1 < q.x1 - 1e-4f) next.Add(new PlanRect(hx1, q.x1, hz0, hz1));
                }
                result = next;
            }
            return result;
        }

        /// <summary>Stairs: a ramp collider (a 0.3 m slab under the slope: the space under the stairs stays open) and the
        /// record the builder draws the treads and risers from. It climbs along x or z, toward + (<paramref name="up"/> 1)
        /// or - (-1).</summary>
        HousePart Stairs(string name, PlanRect r, float bottom, float top, bool alongX, int up)
        {
            float atMin = up > 0 ? bottom : top, atMax = up > 0 ? top : bottom;
            var part = Part(name, LabPieceKind.Ramp, HouseLook.Stair, V(r.x0, Math.Min(bottom, top) - 0.3f, r.z0), V(r.x1, Math.Max(bottom, top), r.z1));
            part.piece.rampTopStart = atMin;
            part.piece.rampTopEnd = atMax;
            part.rampAlongX = alongX;
            part.steps = true;
            stairs.Add(new HouseStair { name = name, rect = r, bottomY = bottom, topY = top, alongX = alongX, up = up, rises = Math.Max(3, (int)Math.Round(Math.Abs(top - bottom) / 0.19f)) });
            return part;
        }

        /// <summary>The top of a ramp at (x, z).</summary>
        public static float RampTop(HousePart part, float x, float z)
        {
            var p = part.piece;
            float t = part.rampAlongX ? (x - p.Min.X) / Math.Max(1e-4f, p.size.X) : (z - p.Min.Z) / Math.Max(1e-4f, p.size.Z);
            t = Math.Max(0f, Math.Min(1f, t));
            return p.rampTopStart + (p.rampTopEnd - p.rampTopStart) * t;
        }

        /// <summary>Furniture standing on a floor: a solid box (its collider) with the model fitted into it.</summary>
        HousePart Furniture(string name, string model, float x0, float x1, float z0, float z1, float floor, float height, float yaw = 0f) =>
            Part(name, LabPieceKind.Ledge, HouseLook.Furniture, V(x0, floor, z0), V(x1, floor + height, z1), true, false, model, yaw);

        /// <summary>A prop on top of something (a lamp on a table, a kettle on the counter): solid, small.</summary>
        HousePart Prop(string name, string model, float cx, float cz, float baseY, float w, float d, float h, float yaw = 0f) =>
            Part(name, LabPieceKind.Ledge, HouseLook.Furniture, V(cx - w * 0.5f, baseY, cz - d * 0.5f), V(cx + w * 0.5f, baseY + h, cz + d * 0.5f), true, false, model, yaw);

        /// <summary>A door leaf standing open against a wall.</summary>
        HousePart DoorLeaf(string name, float x0, float x1, float z0, float z1, float floor, float height = 2.05f) =>
            Part(name, LabPieceKind.Wall, HouseLook.DoorLeaf, V(x0, floor, z0), V(x1, floor + height, z1));

        HouseRoom AddRoom(string id, string name, float floorY, float ceilingY, string style, float light, params PlanRect[] rects)
        {
            var r = new HouseRoom { id = id, name = name, floorY = floorY, ceilingY = ceilingY, style = style, light = light, storey = StoreyOf(floorY) };
            r.rects.AddRange(rects);
            rooms.Add(r);
            foreach (var rc in rects)
                lights.Add(new HouseLight { name = name, center = V(rc.CX, floorY + (ceilingY - floorY) * 0.5f, rc.CZ), size = V(rc.x1 - rc.x0, ceilingY - floorY + 0.4f, rc.z1 - rc.z0), level = light });
            return r;
        }

        public static int StoreyOf(float y) => y < Ground - 1f ? 0 : y < Upper - 0.5f ? 1 : y < Attic - 0.5f ? 2 : 3;

        void Spot(string name, HideKind kind, string room, Vector3 center, Vector3 size, Vector3 approach)
        {
            spots.Add(HidingSpot.Box(name, kind, center, size, approach));
            spotRooms.Add(room);
        }

        int Node(string room, float x, float y, float z)
        {
            nodes.Add(new TheoNode { p = V(x, y, z), room = room });
            return nodes.Count - 1;
        }

        void Link(int a, int b) => links.Add((a, b));

        HouseMemory Memory(string id, string name, string room, string picture, Vector3 at, Vector3 standAt)
        {
            var m = new HouseMemory { id = id, name = name, room = room, picture = picture, position = at, standAt = standAt };
            memories.Add(m);
            return m;
        }

        static void Alt(HouseMemory m, string room, Vector3 at, Vector3 standAt)
        {
            m.altPositions.Add(at);
            m.altStandAt.Add(standAt);
            m.altRooms.Add(room);
        }

        void Lamp(string name, string kind, Vector3 at, float range, float intensity) =>
            lamps.Add(new HouseLamp { name = name, kind = kind, position = at, range = range, intensity = intensity });
    }
}
