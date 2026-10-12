using System;
using System.Collections.Generic;
using System.Numerics;
using JamKit.Movement;
using JamKit.Stealth;

namespace JamKit.DreamEscape
{
    /// <summary>
    /// The plan (docs/redesign/house.md has the drawings). The house's frame: x east, z north, y up; the front door in the
    /// south wall at x 0. Centre lines below; the outer walls are 0.3 m, the inner 0.15 m; every wall ends at the face of the
    /// wall it meets, so no two solids overlap.
    /// <code>
    ///  UPPER (y 3.2)                                   GROUND (y 0)
    ///  z 11 +--------+-----+------+---------+          +--------------+------+-------------+--------+
    ///       | guest  | hall| attic stairs   |          | den           | back | kitchen  [P]| mud    |
    ///       |   +----+     +------+---------+          |               | hall |             | room   |
    ///       |   |bath|     |closet|en-suite |  z 6.0   +-----+--------+------+---+---------+--------+
    ///  z 5  +---+----+stair|------+---------+          | living room  | front | dining     | garage |
    ///       | Theo's room  | well | parents |          |  (fireplace) | hall  | (the table)|        |
    ///  z 0  +--------------+-----+---------+          +--------------+--door-+------------+--------+
    ///      x -7.5        -1.7    1.7       7.5        x -7.5        -1.7    1.7          7.5      14
    /// </code>
    /// The basement (y -2.8) under it all: the rec room, the furnace room, Elias's workshop, the laundry. The attic (y 6.1)
    /// under a gable roof: storage, the dormer at the top of the attic stairs, Lena's observatory tower at the west end.
    /// Outside: the front garden and porch, the driveway, the back yard (patio and barbecue, lawn, clothesline, trampoline,
    /// swing, sandbox, the shed, the oak with Theo's treehouse), fenced.
    /// </summary>
    public sealed partial class DreamHouse
    {
        const float e = ExtT * 0.5f, i = IntT * 0.5f;

        // The ceilings (each storey's ceiling is the underside of the next slab).
        const float GroundCeil = Upper - SlabT, UpperCeil = Attic - SlabT, BasementCeil = Ground - SlabT;

        /// <summary>The main stair (front hall, along its west wall, climbing north) and the basement stair (back hall,
        /// under it, climbing north from the basement), the attic stair (climbing east from the upper hall).</summary>
        public static readonly PlanRect MainStair = new PlanRect(-1.7f + i, -0.575f, 1.6f, 5.8f);
        public static readonly PlanRect BasementStair = new PlanRect(-1.7f + i, -0.575f, 5.2f, 9.0f);
        public static readonly PlanRect AtticStair = new PlanRect(1.7f + i, 5.2f, 9.75f + i, Z1 - e);
        /// <summary>The holes in the slabs over the stairs (where the head needs the room). Long enough that coming DOWN
        /// she never meets the slab's edge at head height (it read as a ledge: she mantled up onto the landing and
        /// stalled against the balustrade, the recorded run of 2026-10-12): 2.4 m clear over the slope at the edge.</summary>
        public static readonly PlanRect UpperHole = new PlanRect(-1.7f + i, -0.575f, 2.2f, 5.8f);
        public static readonly PlanRect GroundHole = new PlanRect(-1.7f + i, -0.575f, 6.0f + i, 9.0f);
        public static readonly PlanRect AtticHole = new PlanRect(1.7f + i + 0.08f, 5.2f, 9.75f + i, Z1 - e);

        /// <summary>The roof's underside over the attic floor at z (knee 0.9 at the walls, the ridge 3.4 over z 5.5).</summary>
        public static float RoofAt(float z) => KneeWall + (RidgeHeight - KneeWall) * (1f - Math.Min(1f, Math.Abs(z - RidgeZ) / (RidgeZ - Z0)));

        /// <summary>The main stair's underside at z (the cupboard under it is shaped by this).</summary>
        static float MainStairUnderside(float z) => (z - MainStair.z0) / (MainStair.z1 - MainStair.z0) * Upper - 0.4f;

        void Make()
        {
            Ground_();
            Upper_();
            Basement_();
            Attic_();
            Garage_();
            Outside_();
            Furnish();
            Stealth_();
            Memories_();
            Lamps_();
        }

        // =====================================================================================================
        // The ground floor
        // =====================================================================================================
        void Ground_()
        {
            const float y0 = Ground, y1 = Upper;   // outer walls run from floor to the next floor
            _room = null;
            // --- Outer walls (ground band). The south and north walls own the corners.
            WallX("South wall (ground)", Z0, X0 - e, X1 + e, y0, y1, ExtT,
                Opening.Window(-6.4f, -5.0f, 0.75f, 2.35f, "Living room window (west)"),
                Opening.Window(-4.0f, -2.6f, 0.75f, 2.35f, "Living room window (east)"),
                Opening.Door(-0.6f, 0.6f, "Front door", 2.25f),
                Opening.Window(2.6f, 4.0f, 0.85f, 2.35f, "Dining room window (west)"),
                Opening.Window(5.0f, 6.4f, 0.85f, 2.35f, "Dining room window (east)"));
            WallX("North wall (ground)", Z1, X0 - e, X1 + e, y0, y1, ExtT,
                Opening.Window(-6.2f, -4.6f, 0.95f, 2.35f, "Den window"),
                Opening.Door(-0.5f, 0.5f, "Back door"),
                Opening.Window(3.6f, 5.4f, 1.05f, 2.35f, "Kitchen window (over the sink)"));
            WallZ("West wall (ground)", X0, Z0 + e, Z1 - e, y0, y1, ExtT,
                Opening.Window(0.7f, 1.8f, 0.75f, 2.35f, "Living room window (by the fire, south)"),
                Opening.Window(4.6f, 5.7f, 0.75f, 2.35f, "Living room window (by the fire, north)"),
                Opening.Window(7.4f, 8.8f, 0.6f, 2.35f, "Den window (west)"));
            WallZ("East wall (ground)", X1, Z0 + e, Z1 - e, y0, y1, ExtT,
                Opening.Door(7.8f, 8.7f, "Mudroom door"));

            // --- Inner walls (ground floor: floor to the ceiling).
            const float c0 = Ground, c1 = GroundCeil;
            // x = -1.7: living | front hall, den | back hall.
            WallZ("Wall (living | hall, den | back hall)", -1.7f, Z0 + e, Z1 - e, c0, c1, IntT,
                Opening.Door(0.25f, 1.45f, "Living room door"),
                Opening.Door(9.2f, 10.2f, "Den door (back hall)"));
            // x = 1.7: front hall | dining, back hall | kitchen.
            WallZ("Wall (hall | dining, back hall | kitchen)", 1.7f, Z0 + e, Z1 - e, c0, c1, IntT,
                Opening.Arch(1.4f, 3.2f, "Dining room archway"),
                Opening.Door(6.3f, 7.4f, "Kitchen door (back hall)"));
            // z = 6.0: living | den (west), front hall | back hall (middle: open beside the stairs' top).
            WallX("Wall (living | den)", 6.0f, X0 + e, -1.7f - i, c0, c1, IntT, Opening.Door(-5.6f, -4.6f, "Den door (living room)"));
            WallX("Wall (front hall | back hall)", 6.0f, -1.7f + i, 1.7f - i, c0, c1, IntT, Opening.Arch(-0.35f, 1.55f, "Back hall archway"));
            // z = 5.4: dining | kitchen, wide open.
            WallX("Wall (dining | kitchen)", 5.4f, 1.7f + i, X1 - e, c0, c1, IntT, Opening.Arch(2.6f, 6.4f, "Kitchen opening", 2.45f));
            // The pantry in the kitchen's north-east corner.
            WallX("Pantry wall (south)", 9.4f, 6.0f - i, X1 - e, c0, c1, IntT, Opening.Door(6.1f, 6.95f, "Pantry door"));
            WallZ("Pantry wall (west)", 6.0f, 9.4f + i, Z1 - e, c0, c1, IntT);
            // The chimney breast (the living room's fireplace), floor to ceiling.
            _room = "living";
            Part("Chimney breast (living room)", LabPieceKind.Wall, HouseLook.Chimney, V(X0 + e, c0, 2.2f), V(-6.75f, c1, 4.2f), true, true);
            Part("Hearth", LabPieceKind.Ledge, HouseLook.Chimney, V(-6.75f, c0, 2.5f), V(-6.35f, c0 + 0.08f, 3.9f));

            // The under-stairs: stepped panelling under the main stair's slope along its open side (x -0.5), with the
            // cupboard's low door near the top; the low end closed off.
            _room = "hall";
            UnderStairs();

            // --- The floor slab (over the basement) with the basement stair's hole; the ceiling slab is the upper floor's.
            _room = null;
            Slab("Ground floor slab", X0 + e, X1 - e, Z0 + e, Z1 - e, Ground, GroundHole);
            // The rail round the basement stair's hole in the back hall.
            Part("Basement stair rail", LabPieceKind.Wall, HouseLook.Rail, V(-0.575f, Ground, GroundHole.z0), V(-0.495f, Ground + 1.0f, GroundHole.z1));
            Stairs("The main stairs", MainStair, Ground, Upper, false, 1);

            // --- The rooms (inner faces).
            AddRoom("living", "Living room", Ground, GroundCeil, "living", 0.95f, new PlanRect(X0 + e, -1.7f - i, Z0 + e, 6.0f - i));
            AddRoom("hall", "Front hall", Ground, GroundCeil, "hall", 0.6f, new PlanRect(-1.7f + i, 1.7f - i, Z0 + e, 6.0f - i));
            AddRoom("dining", "Dining room", Ground, GroundCeil, "dining", 0.95f, new PlanRect(1.7f + i, X1 - e, Z0 + e, 5.4f - i));
            AddRoom("kitchen", "Kitchen", Ground, GroundCeil, "kitchen", 0.9f, new PlanRect(1.7f + i, X1 - e, 5.4f + i, 9.4f - i), new PlanRect(1.7f + i, 6.0f - i, 9.4f - i, Z1 - e));
            AddRoom("pantry", "Pantry", Ground, GroundCeil, "pantry", 0.2f, new PlanRect(6.0f + i, X1 - e, 9.4f + i, Z1 - e));
            AddRoom("den", "Den", Ground, GroundCeil, "study", 0.6f, new PlanRect(X0 + e, -1.7f - i, 6.0f + i, Z1 - e));
            AddRoom("backhall", "Back hall", Ground, GroundCeil, "hall", 0.5f, new PlanRect(-1.7f + i, 1.7f - i, 6.0f + i, Z1 - e));
            AddRoom("understairs", "The cupboard under the stairs", Ground, 2.0f, "cupboard", 0.1f, new PlanRect(-1.7f + i, -0.575f, 4.3f, 6.0f - i));
        }

        /// <summary>Panelling under the main stair's open (east) side, in steps that stay under its slope, with the
        /// cupboard's low door, and the cupboard's ceiling: the space under the stairs' high end is the cupboard.</summary>
        void UnderStairs()
        {
            const float x0 = -0.575f, x1 = -0.495f;
            float z = MainStair.z0;
            // The low end (z 1.6 to 4.3): closed in steps.
            foreach (var zb in new[] { 2.6f, 3.4f, 4.3f })
            {
                float top = Math.Max(0.12f, MainStairUnderside(z) - 0.02f);
                if (top > 0.12f) Part("Under the stairs (panelling)", LabPieceKind.Wall, HouseLook.Cupboard, V(x0, Ground, z), V(x1, Ground + top, zb));
                else Part("Under the stairs (the low end)", LabPieceKind.Wall, HouseLook.Cupboard, V(x0, Ground, z), V(x1, Ground + 0.12f, zb));
                z = zb;
            }
            // The low end's back: a wall across under the stair at z 4.3 (the cupboard's south end).
            Part("Under the stairs (the cupboard's end)", LabPieceKind.Wall, HouseLook.Cupboard, V(-1.7f + i, Ground, 4.22f), V(x0, Ground + MainStairUnderside(4.22f) - 0.02f, 4.3f));
            // The cupboard (z 4.3 to 5.925): its panelling, the low door at z 4.6..5.4.
            Part("Cupboard panelling (south)", LabPieceKind.Wall, HouseLook.Cupboard, V(x0, Ground, 4.3f), V(x1, Ground + MainStairUnderside(4.3f) - 0.02f, 4.6f));
            Part("Cupboard door (over it)", LabPieceKind.Wall, HouseLook.Cupboard, V(x0, Ground + 1.3f, 4.6f), V(x1, Ground + MainStairUnderside(4.6f) - 0.02f, 5.4f));
            Part("Cupboard panelling (north)", LabPieceKind.Wall, HouseLook.Cupboard, V(x0, Ground, 5.4f), V(x1, Ground + MainStairUnderside(5.4f) - 0.02f, 6.0f - i));
            doors.Add(new HouseDoor { name = "Cupboard door (under the stairs)", alongX = false, c = (x0 + x1) * 0.5f, a = 4.6f, b = 5.4f, y0 = Ground, y1 = Ground + 1.3f, thickness = x1 - x0 });
        }

        // =====================================================================================================
        // The upper floor
        // =====================================================================================================
        void Upper_()
        {
            const float y0 = Upper, y1 = Attic;
            _room = null;
            WallX("South wall (upper)", Z0, X0 - e, X1 + e, y0, y1, ExtT,
                Opening.Window(-6.2f, -4.8f, 0.85f, 2.2f, "Theo's window (front)"),
                Opening.Window(-0.6f, 0.6f, 0.85f, 2.1f, "Landing window (over the front door)"),
                Opening.Window(2.8f, 4.2f, 0.85f, 2.2f, "Parents' window (west)"),
                Opening.Window(5.2f, 6.6f, 0.85f, 2.2f, "Parents' window (east)"));
            WallX("North wall (upper)", Z1, X0 - e, X1 + e, y0, y1, ExtT,
                Opening.Window(-6.6f, -5.4f, 0.85f, 2.2f, "Guest room window (west)"),
                Opening.Window(-3.4f, -2.2f, 0.85f, 2.2f, "Guest room window (east)"),
                Opening.Window(-0.6f, 0.6f, 0.85f, 2.2f, "Upper hall window"));
            WallZ("West wall (upper)", X0, Z0 + e, Z1 - e, y0, y1, ExtT,
                Opening.Window(0.6f, 1.8f, 0.85f, 2.2f, "Theo's window (side)"),
                Opening.Window(8.2f, 9.6f, 0.85f, 2.2f, "Guest room window (side)"));
            WallZ("East wall (upper)", X1, Z0 + e, Z1 - e, y0, y1, ExtT,
                Opening.Window(1.6f, 3.0f, 1.0f, 2.2f, "Parents' window (side)"),
                Opening.Window(7.6f, 8.6f, 1.3f, 2.2f, "En-suite window"));

            const float c0 = Upper, c1 = UpperCeil;
            WallZ("Wall (Theo, bath, guest | upper hall)", -1.7f, Z0 + e, Z1 - e, c0, c1, IntT,
                Opening.Door(1.0f, 1.9f, "Theo's door"),
                Opening.Door(6.2f, 7.0f, "Bathroom door"),
                Opening.Door(9.4f, 10.3f, "Guest room door"));
            WallZ("Wall (upper hall | parents, attic stairs)", 1.7f, Z0 + e, Z1 - e, c0, c1, IntT,
                Opening.Door(4.4f, 5.3f, "Parents' door"),
                Opening.Arch(9.8f + 0.05f, 10.8f, "Attic stairs archway (no header: coming down the steep flight her head would meet it)", UpperH));
            WallX("Wall (Theo | bath, guest)", 5.0f, X0 + e, -1.7f - i, c0, c1, IntT);
            WallZ("Wall (bath | guest)", -4.2f, 5.0f + i, 7.6f - i, c0, c1, IntT);
            WallX("Wall (bath | guest, north)", 7.6f, -4.2f - i, -1.7f - i, c0, c1, IntT);
            WallX("Wall (parents | closet, en-suite)", 6.6f, 1.7f + i, X1 - e, c0, c1, IntT,
                Opening.Door(2.3f, 3.1f, "Walk-in closet door"),
                Opening.Door(5.6f, 6.4f, "En-suite door"));
            WallZ("Wall (closet | en-suite)", 4.6f, 6.6f + i, 9.75f - i, c0, c1, IntT);
            WallX("Wall (closet, en-suite | attic stairs)", 9.75f, 1.7f + i, X1 - e, c0, c1, IntT);
            // The chimney breast through Theo's room.
            _room = "theo";
            Part("Chimney breast (Theo's room)", LabPieceKind.Wall, HouseLook.Chimney, V(X0 + e, c0, 2.2f), V(-6.75f, c1, 4.2f), true, true);

            _room = null;
            Slab("Upper floor slab", X0 + e, X1 - e, Z0 + e, Z1 - e, Upper, UpperHole);
            // The balustrade round the stairwell (its east side and its south end).
            Part("Stairwell balustrade (east)", LabPieceKind.Wall, HouseLook.Rail, V(-0.575f, Upper, UpperHole.z0 - 0.08f), V(-0.495f, Upper + 0.95f, UpperHole.z1));
            Part("Stairwell balustrade (south)", LabPieceKind.Wall, HouseLook.Rail, V(-1.7f + i, Upper, UpperHole.z0 - 0.08f), V(-0.575f, Upper + 0.95f, UpperHole.z0));
            Stairs("The attic stairs", AtticStair, Upper, Attic, true, 1);

            AddRoom("theo", "Theo's room", Upper, UpperCeil, "theo", 0.85f, new PlanRect(X0 + e, -1.7f - i, Z0 + e, 5.0f - i));
            AddRoom("bath", "Bathroom", Upper, UpperCeil, "bath", 0.75f, new PlanRect(-4.2f + i, -1.7f - i, 5.0f + i, 7.6f - i));
            AddRoom("guest", "The guest room (made up for her)", Upper, UpperCeil, "guest", 0.7f,
                new PlanRect(X0 + e, -1.7f - i, 7.6f + i, Z1 - e), new PlanRect(X0 + e, -4.2f - i, 5.0f + i, 7.6f + i));
            AddRoom("upperhall", "Upper hall", Upper, UpperCeil, "hall", 0.55f, new PlanRect(-1.7f + i, 1.7f - i, Z0 + e, Z1 - e));
            AddRoom("parents", "Elias and Lena's room", Upper, UpperCeil, "parents", 0.85f, new PlanRect(1.7f + i, X1 - e, Z0 + e, 6.6f - i));
            AddRoom("closet", "The walk-in closet", Upper, UpperCeil, "closet", 0.15f, new PlanRect(1.7f + i, 4.6f - i, 6.6f + i, 9.75f - i));
            AddRoom("ensuite", "The en-suite", Upper, UpperCeil, "bath", 0.7f, new PlanRect(4.6f + i, X1 - e, 6.6f + i, 9.75f - i));
            AddRoom("atticstairs", "The attic stairs", Upper, UpperCeil, "attic", 0.3f, new PlanRect(1.7f + i, X1 - e, 9.75f + i, Z1 - e));
        }

        // =====================================================================================================
        // The basement
        // =====================================================================================================
        void Basement_()
        {
            const float y0 = Basement - SlabT, y1 = Ground;
            _room = null;
            WallX("South wall (basement)", Z0, X0 - e, X1 + e, y0, y1, ExtT);
            WallX("North wall (basement)", Z1, X0 - e, X1 + e, y0, y1, ExtT);
            WallZ("West wall (basement)", X0, Z0 + e, Z1 - e, y0, y1, ExtT);
            WallZ("East wall (basement)", X1, Z0 + e, Z1 - e, y0, y1, ExtT);
            const float c0 = Basement, c1 = BasementCeil;
            WallZ("Wall (rec room, furnace room | corridor)", -1.7f, Z0 + e, Z1 - e, c0, c1, IntT,
                Opening.Arch(2.8f, 4.4f, "Rec room opening", 2.2f));
            WallZ("Wall (corridor | workshop, laundry)", 1.7f, Z0 + e, Z1 - e, c0, c1, IntT,
                Opening.Arch(2.0f, 3.4f, "Workshop opening", 2.2f),
                Opening.Door(7.4f, 8.3f, "Laundry door"));
            WallX("Wall (rec room | furnace room)", 6.5f, X0 + e, -1.7f - i, c0, c1, IntT, Opening.Door(-4.6f, -3.7f, "Furnace room door (rec room)"));
            WallX("Wall (workshop | laundry)", 6.0f, 1.7f + i, X1 - e, c0, c1, IntT, Opening.Door(4.2f, 5.1f, "Laundry door (workshop)"));
            // The basement stair boxed in on its east side (open at its foot).
            WallZ("Basement stair wall", -0.535f, 5.9f, Z1 - e, c0, c1, 0.08f);
            Part("Chimney (basement)", LabPieceKind.Wall, HouseLook.Chimney, V(X0 + e, c0, 2.2f), V(-6.75f, c1, 4.2f), true, true);
            Slab("Basement floor", X0 + e, X1 - e, Z0 + e, Z1 - e, Basement);
            Stairs("The basement stairs", BasementStair, Basement, Ground, false, 1);

            AddRoom("rec", "The rec room", Basement, BasementCeil, "basement", 0.45f, new PlanRect(X0 + e, -1.7f - i, Z0 + e, 6.5f - i));
            AddRoom("furnace", "The furnace room", Basement, BasementCeil, "basement", 0.12f, new PlanRect(X0 + e, -1.7f - i, 6.5f + i, Z1 - e));
            AddRoom("corridor", "The basement corridor", Basement, BasementCeil, "basement", 0.3f, new PlanRect(-1.7f + i, 1.7f - i, Z0 + e, Z1 - e));
            AddRoom("workshop", "Elias's workshop", Basement, BasementCeil, "basement", 0.55f, new PlanRect(1.7f + i, X1 - e, Z0 + e, 6.0f - i));
            AddRoom("laundry", "The laundry", Basement, BasementCeil, "basement", 0.4f, new PlanRect(1.7f + i, X1 - e, 6.0f + i, Z1 - e));
        }

        // =====================================================================================================
        // The attic: the roof, the dormer, the observatory tower
        // =====================================================================================================
        void Attic_()
        {
            _room = null;
            float knee = Attic + RoofAt(Z0 - e) - 0.02f;   // the knee walls stop under the roof's underside at their outer face
            // The knee walls (south, north) and the dormer's front.
            WallX("South knee wall (attic)", Z0, X0 - e, X1 + e, Attic, knee, ExtT);
            WallX("North knee wall (attic, west)", Z1, X0 - e, DormerX0 - i, Attic, knee, ExtT);
            WallX("Dormer front (attic)", Z1, DormerX0 - i, DormerX1 + i, Attic, Attic + DormerH, ExtT,
                Opening.Window(2.8f, 4.4f, 0.8f, 2.0f, "Dormer window"));
            WallX("North knee wall (attic, east)", Z1, DormerX1 + i, X1 + e, Attic, knee, ExtT);
            // The gables, in steps under the roof (the builder draws the triangle over them).
            Gable("East gable", X1, Z0 + e, Z1 - e);
            Gable("West gable (south of the tower)", X0, Z0 + e, TowerZ0 - e);
            Gable("West gable (north of the tower)", X0, TowerZ1 + e, Z1 - e);
            // The tower: four walls from the attic floor to its ceiling; a door from the attic; the round window west.
            float tt = Attic + TowerH;
            WallX("Tower wall (south)", TowerZ0, X0 - e, TowerX1 + e, Attic, tt, ExtT);
            // The round window looks north, up the hill (Lena watches from it as Nadia comes out on Chapel Row).
            WallX("Tower wall (north, the round window)", TowerZ1, X0 - e, TowerX1 + e, Attic, tt, ExtT,
                // Above the roof outside (it looked onto the roof's underside: house-1.md), a 1.6 m circle in the square
                // opening; Lena's dais under it puts her head and shoulders in it, seen from Chapel Row.
                Opening.Window(-6.05f, -4.45f, 2.35f, 3.95f, "The round window"));
            WallZ("Tower wall (west)", X0, TowerZ0 + e, TowerZ1 - e, Attic, tt, ExtT);
            WallZ("Tower wall (east, the door)", TowerX1, TowerZ0 + e, TowerZ1 - e, Attic, tt, ExtT,
                Opening.Door(4.9f, 5.9f, "Observatory door"));
            Part("Tower ceiling", LabPieceKind.Floor, HouseLook.Roof, V(X0 - e, tt, TowerZ0 - e), V(TowerX1 + e, tt + 0.3f, TowerZ1 + e));
            // The dormer: its cheeks and its south wall (an arch into the attic), its flat roof.
            WallZ("Dormer cheek (west)", DormerX0, DormerZ0 - i, Z1 - e, Attic, Attic + DormerH, IntT);
            WallZ("Dormer cheek (east)", DormerX1, DormerZ0 - i, Z1 - e, Attic, Attic + DormerH, IntT);
            WallX("Dormer wall (south)", DormerZ0, DormerX0 + i, DormerX1 - i, Attic, Attic + DormerH, IntT, Opening.Arch(3.0f, 4.6f, "Dormer arch", 2.05f));
            Part("Dormer roof", LabPieceKind.Floor, HouseLook.Roof, V(DormerX0 - i - 0.1f, Attic + DormerH, DormerZ0 - i), V(DormerX1 + i + 0.1f, Attic + DormerH + 0.25f, Z1 + e + 0.15f));
            // The roof's slopes (ramps: a 0.3 m slab under the slope's top), round the tower and the dormer.
            float over = 0.6f;
            RoofSlope("Roof (south)", X0 - e - 0.3f, X1 + e + 0.3f, Z0 - over, TowerZ0 - e);
            RoofSlope("Roof (south, over the attic)", TowerX1 + e, X1 + e + 0.3f, TowerZ0 - e, RidgeZ);
            RoofSlope("Roof (north, middle)", TowerX1 + e, DormerX0 - i, RidgeZ, Z1 + over);
            RoofSlope("Roof (north, east)", DormerX1 + i, X1 + e + 0.3f, RidgeZ, Z1 + over);
            RoofSlope("Roof (north, over the dormer)", DormerX0 - i, DormerX1 + i, RidgeZ, DormerZ0 - i);
            RoofSlope("Roof (north, west of the tower)", X0 - e - 0.3f, TowerX1 + e, TowerZ1 + e, Z1 + over);
            // The attic floor, the hole over the attic stairs and its rail.
            Slab("Attic floor", X0 + e, X1 - e, Z0 + e, Z1 - e, Attic, AtticHole);
            Part("Attic stair rail (south)", LabPieceKind.Wall, HouseLook.Rail, V(AtticHole.x0 - 0.08f, Attic, AtticHole.z0 - 0.08f), V(AtticHole.x1, Attic + 0.95f, AtticHole.z0));
            Part("Attic stair rail (west)", LabPieceKind.Wall, HouseLook.Rail, V(AtticHole.x0 - 0.08f, Attic, AtticHole.z0), V(AtticHole.x0, Attic + 0.95f, AtticHole.z1));
            // The chimney stack up through the attic and the roof (outside it's the brick stack).
            Part("Chimney (attic)", LabPieceKind.Wall, HouseLook.Chimney, V(X0 + e, Attic, 2.2f), V(-6.75f, Attic + 0.9f, 2.84f), true, false);
            Part("Chimney stack (through the roof)", LabPieceKind.Wall, HouseLook.Chimney, V(X0 + e, Attic + 0.9f, 2.2f), V(-6.75f, Attic + RoofAt(2.84f) + 1.3f, 2.84f), true, false);

            AddRoom("attic", "The attic", Attic, Attic + RidgeHeight, "attic", 0.25f, new PlanRect(TowerX1 + e, X1 - e, Z0 + e, Z1 - e), new PlanRect(X0 + e, TowerX1 + e, Z0 + e, TowerZ0 - e), new PlanRect(X0 + e, TowerX1 + e, TowerZ1 + e, Z1 - e));
            AddRoom("dormer", "The top of the attic stairs", Attic, Attic + DormerH, "attic", 0.35f, new PlanRect(DormerX0 + i, DormerX1 - i, DormerZ0 + i, Z1 - e));
            AddRoom("observatory", "Lena's observatory", Attic, Attic + TowerH, "observatory", 0.3f, new PlanRect(X0 + e, TowerX1 - e, TowerZ0 + e, TowerZ1 - e));
        }

        /// <summary>A gable end over the attic floor, as steps that stay under the roof (0.45 m wide).</summary>
        void Gable(string name, float x, float z0, float z1)
        {
            for (float z = z0; z < z1 - 1e-3f; z += 0.45f)
            {
                float zb = Math.Min(z1, z + 0.45f);
                float top = Attic + Math.Min(RoofAt(z), RoofAt(zb)) - 0.02f;
                // The roof's slab sits over the wall's outer edge too: keep under it there.
                top = Math.Min(top, Attic + Math.Min(RoofAt(z), RoofAt(zb)) - 0.02f);
                Part(name, LabPieceKind.Wall, HouseLook.Wall, V(x - e, Attic, z), V(x + e, top, zb), true, false);
            }
        }

        /// <summary>A slope of the roof between z0 and z1 (it rises toward the ridge): a ramp whose underside follows
        /// <see cref="RoofAt"/>.</summary>
        void RoofSlope(string name, float x0, float x1, float z0, float z1)
        {
            float t0 = Attic + RoofAt(z0) + 0.33f, t1 = Attic + RoofAt(z1) + 0.33f;
            // Beyond the walls (the eaves' overhang) the slope carries on down.
            float slope = (RidgeHeight - KneeWall) / (RidgeZ - Z0);
            if (z0 < Z0) t0 = Attic + KneeWall - (Z0 - z0) * slope + 0.33f;
            if (z1 > Z1) t1 = Attic + KneeWall - (z1 - Z1) * slope + 0.33f;
            var part = Part(name, LabPieceKind.Ramp, HouseLook.Roof, V(x0, Math.Min(t0, t1) - 0.33f, z0), V(x1, Math.Max(t0, t1), z1));
            part.piece.rampTopStart = t0;
            part.piece.rampTopEnd = t1;
        }


        // =====================================================================================================
        // The garage and the mudroom (east, one storey under lean-to roofs)
        // =====================================================================================================
        void Garage_()
        {
            _room = null;
            const float g0 = Ground - SlabT, g1 = GarageFloor + GarageH, lift = GarageFloor - g0;
            WallX("Garage wall (south)", Z0, X1 + e, GarageX1 + e, g0, g1, ExtT, Opening.Door(8.3f, 11.3f, "Garage door", 2.25f + lift));
            WallZ("Garage wall (east)", GarageX1, Z0 + e, GarageZ1 - e, g0, g1, ExtT, Opening.Door(5.2f, 6.1f, "Garage side door", 2.1f + lift));
            WallX("Garage wall (north)", GarageZ1, X1 + e, GarageX1 + e, g0, g1, ExtT, Opening.Door(8.3f, 9.2f, "Garage door (mudroom)", 2.1f + lift));
            // The garage door itself, closed (a panelled up-and-over door in the opening).
            Part("The garage door (closed)", LabPieceKind.Wall, HouseLook.GarageDoor, V(8.3f, g0, Z0 - 0.06f), V(11.3f, g0 + 2.25f + lift, Z0 + 0.06f), true, true);
            Slab("Garage floor", X1 + e, GarageX1 - e, Z0 + e, GarageZ1 - e, GarageFloor);
            Part("Garage ceiling", LabPieceKind.Floor, HouseLook.Slab, V(X1 + e, g1, Z0 - e), V(GarageX1 + e, g1 + 0.2f, GarageZ1 + e));
            // The mudroom: between the kitchen and the yard, the garage's back door.
            const float m1 = Ground + 2.6f;
            WallX("Mudroom wall (north)", Z1, X1 + e, MudX1 + e, g0, m1, ExtT, Opening.Door(8.8f, 9.7f, "Mudroom back door", 2.1f + lift));
            WallZ("Mudroom wall (east)", MudX1, GarageZ1 + e, Z1 - e, g0, m1, ExtT, Opening.Window(8.6f, 9.8f, 1.35f + lift, 2.3f + lift, "Mudroom window"));
            Slab("Mudroom floor", X1 + e, MudX1 - e, GarageZ1 + e, Z1 - e, Ground);
            Part("Mudroom ceiling", LabPieceKind.Floor, HouseLook.Slab, V(X1 + e, m1, GarageZ1 + e), V(MudX1 + e, m1 + 0.2f, Z1 + e));
            // The lean-to roofs over them, sloping down away from the house.
            var gr = Part("Garage roof", LabPieceKind.Ramp, HouseLook.Roof, V(X1 + e, g1 + 0.2f, Z0 - 0.5f), V(GarageX1 + e + 0.45f, g1 + 1.25f, GarageZ1 + e));
            gr.rampAlongX = true; gr.piece.rampTopStart = g1 + 1.25f; gr.piece.rampTopEnd = g1 + 0.55f;
            var mr = Part("Mudroom roof", LabPieceKind.Ramp, HouseLook.Roof, V(X1 + e, m1 + 0.2f, GarageZ1 + e), V(MudX1 + e + 0.45f, m1 + 1.1f, Z1 + e + 0.45f));
            mr.rampAlongX = true; mr.piece.rampTopStart = m1 + 1.1f; mr.piece.rampTopEnd = m1 + 0.5f;

            AddRoom("garage", "The garage", GarageFloor, g1, "garage", 0.3f, new PlanRect(X1 + e, GarageX1 - e, Z0 + e, GarageZ1 - e));
            AddRoom("mudroom", "The mudroom", Ground, m1, "mudroom", 0.6f, new PlanRect(X1 + e, MudX1 - e, GarageZ1 + e, Z1 - e));
        }

        // =====================================================================================================
        // Outside: the lot on Larch Hill (World's HillAnchors: the lot's ground is the front door's level; the front garden
        // runs to Rowan Street's footpath, the back garden to the middle band's first wall). On the hill the World builds
        // the ground, the fences and the gates; standing alone the house builds them itself, and stand-ins for Rowan Street
        // and Chapel Row.
        // =====================================================================================================
        void Outside_()
        {
            _room = "yard";
            const float gb = Yard - 1f;
            // The ground, round the house (never under it).
            Part("Front garden", LabPieceKind.Floor, HouseLook.Lawn, V(YardX0, gb, FrontZ0), V(YardX1, Yard, Z0 - e));
            Part("Side garden (west)", LabPieceKind.Floor, HouseLook.Lawn, V(YardX0, gb, Z0 - e), V(X0 - e, Yard, Z1 + e));
            Part("Side yard (by the garage)", LabPieceKind.Floor, HouseLook.Lawn, V(GarageX1 + e, gb, Z0 - e), V(YardX1, Yard, GarageZ1 + e));
            Part("Side yard (by the mudroom)", LabPieceKind.Floor, HouseLook.Lawn, V(MudX1 + e, gb, GarageZ1 + e), V(YardX1, Yard, Z1 + e));
            Part("Back yard", LabPieceKind.Floor, HouseLook.Lawn, V(YardX0, gb, Z1 + e), V(YardX1, Yard, YardZ1));
            // Rowan Street (a stand-in until the house stands on the hill): the footpath, the road.
            Part("Rowan Street's footpath", LabPieceKind.Floor, HouseLook.Path, V(-34f, StreetY - 1f, FrontZ0 - 3f), V(40f, StreetY, FrontZ0 - 0.25f));
            Part("Rowan Street", LabPieceKind.Floor, HouseLook.Street, V(-34f, StreetY - 1.15f, StreetZ0), V(40f, StreetY - 0.15f, FrontZ0 - 3f));
            Part("The lot's edge (a low wall at the footpath)", LabPieceKind.Wall, HouseLook.Path, V(YardX0, StreetY - 1f, FrontZ0 - 0.25f), V(-0.9f, Yard, FrontZ0));
            Part("The lot's edge (east of the gate)", LabPieceKind.Wall, HouseLook.Path, V(0.9f, StreetY - 1f, FrontZ0 - 0.25f), V(YardX1, Yard, FrontZ0));
            var ns = DeepBlock.HillAnchors.Night2Start - DeepBlock.HillAnchors.HouseOrigin;
            arrival = V(ns.X, StreetY, ns.Z);
            arrivalYaw = DeepBlock.HillAnchors.Night2StartYaw;

            // The front path, the porch (flush with the garden), the steps up from the footpath at the gate; the driveway.
            Part("Front path", LabPieceKind.Floor, HouseLook.Path, V(-0.75f, Yard, FrontZ0), V(0.75f, Yard + 0.02f, -2.2f));
            Part("Porch", LabPieceKind.Floor, HouseLook.Porch, V(-2.2f, Yard, -2.2f), V(2.2f, Yard + 0.02f, Z0 - e));
            foreach (float px in new[] { -2.05f, 2.05f })
                Part("Porch post", LabPieceKind.Wall, HouseLook.Wood, V(px - 0.08f, Yard + 0.02f, -2.15f), V(px + 0.08f, Ground + 2.6f, -1.99f));
            Part("Porch roof", LabPieceKind.Floor, HouseLook.Roof, V(-2.4f, Ground + 2.6f, -2.4f), V(2.4f, Ground + 2.8f, Z0 - e));
            var gs0 = Part("The gate's steps", LabPieceKind.Ramp, HouseLook.Porch, V(-0.9f, StreetY - 0.3f, FrontZ0 - 1.6f), V(0.9f, Yard, FrontZ0 - 0.25f));
            gs0.piece.rampTopStart = StreetY; gs0.piece.rampTopEnd = Yard; gs0.steps = true;
            Part("Driveway", LabPieceKind.Floor, HouseLook.Path, V(8.1f, Yard, FrontZ0), V(11.5f, Yard + 0.02f, Z0 - e));
            frontDoor = V(0f, Ground, Z0);

            // The back: the patio by the back door.
            Part("Patio", LabPieceKind.Floor, HouseLook.Path, V(-3.6f, Yard, Z1 + e), V(3.6f, Yard + 0.02f, 14.2f));

            // The fences (1.7 m boards round the back and sides, a low picket fence at the front with its gate open), the back
            // gate onto the Chapel Steps (shut: the yard is the game's edge).
            Part("Fence (west)", LabPieceKind.Wall, HouseLook.Fence, V(YardX0 - 0.1f, Yard, FrontZ0 + 0.1f), V(YardX0, Yard + 1.7f, YardZ1));
            Part("Fence (east)", LabPieceKind.Wall, HouseLook.Fence, V(YardX1, Yard, FrontZ0 + 0.1f), V(YardX1 + 0.1f, Yard + 1.7f, YardZ1));
            Part("Fence (back, west)", LabPieceKind.Wall, HouseLook.Fence, V(YardX0 - 0.1f, Yard, YardZ1), V(4.4f, Yard + 1.7f, YardZ1 + 0.1f));
            Part("The back gate (shut)", LabPieceKind.Wall, HouseLook.Fence, V(4.4f, Yard, YardZ1), V(5.6f, Yard + 1.7f, YardZ1 + 0.1f));
            Part("Fence (back, east)", LabPieceKind.Wall, HouseLook.Fence, V(5.6f, Yard, YardZ1), V(YardX1 + 0.1f, Yard + 1.7f, YardZ1 + 0.1f));
            Part("Picket fence (front, west)", LabPieceKind.Wall, HouseLook.Fence, V(YardX0 - 0.1f, Yard, FrontZ0), V(-0.9f, Yard + 0.9f, FrontZ0 + 0.1f));
            Part("Picket fence (front, east)", LabPieceKind.Wall, HouseLook.Fence, V(0.9f, Yard, FrontZ0), V(8.0f, Yard + 0.9f, FrontZ0 + 0.1f));
            Part("Picket fence (front, by the drive)", LabPieceKind.Wall, HouseLook.Fence, V(11.6f, Yard, FrontZ0), V(YardX1 + 0.1f, Yard + 0.9f, FrontZ0 + 0.1f));
            Part("Side gate (west)", LabPieceKind.Wall, HouseLook.Fence, V(YardX0, Yard, -0.3f), V(X0 - e, Yard + 1.7f, -0.2f));
            Part("Side gate (east)", LabPieceKind.Wall, HouseLook.Fence, V(GarageX1 + e, Yard, -0.3f), V(YardX1, Yard + 1.7f, -0.2f));

            // The back yard: the oak and Theo's treehouse (west), the clothesline (a place to hide), the trampoline (Theo's
            // lookout), the swing, the sandbox, the shed (east), hedges, the vegetable patch.
            Part("Hedge (east, back)", LabPieceKind.Wall, HouseLook.Hedge, V(YardX1 - 1.1f, Yard, 12.0f), V(YardX1, Yard + 2.1f, 16.6f));
            Part("Hedge (back, west)", LabPieceKind.Wall, HouseLook.Hedge, V(-6.4f, Yard, YardZ1 - 1.1f), V(-1.8f, Yard + 1.9f, YardZ1));
            Treehouse();
            trampoline = V(4.6f, Yard, 18.0f);
            Part("Trampoline", LabPieceKind.Ledge, HouseLook.Trampoline, V(2.8f, Yard, 16.2f), V(6.4f, Yard + 0.85f, 19.8f));
            Part("Swing frame (west legs)", LabPieceKind.Wall, HouseLook.Wood, V(-1.0f, Yard, 18.2f), V(-0.85f, Yard + 2.3f, 19.8f));
            Part("Swing frame (east legs)", LabPieceKind.Wall, HouseLook.Wood, V(1.15f, Yard, 18.2f), V(1.3f, Yard + 2.3f, 19.8f));
            Part("Swing frame (top bar)", LabPieceKind.Bar, HouseLook.Wood, V(-1.0f, Yard + 2.3f, 18.92f), V(1.3f, Yard + 2.42f, 19.08f));
            Part("Sandbox", LabPieceKind.Ledge, HouseLook.Wood, V(8.4f, Yard, 18.6f), V(10.8f, Yard + 0.3f, 21.0f));
            Part("Clothesline post (west)", LabPieceKind.Wall, HouseLook.Wood, V(-4.1f, Yard, 15.45f), V(-3.95f, Yard + 2.1f, 15.6f));
            Part("Clothesline post (east)", LabPieceKind.Wall, HouseLook.Wood, V(2.15f, Yard, 15.45f), V(2.3f, Yard + 2.1f, 15.6f));
            // The line itself, post to post (the sheets hang from it: before round 2 they hung from nothing).
            Part("Clothesline (the line)", LabPieceKind.Wall, HouseLook.Rail, V(-3.95f, Yard + 2.0f, 15.515f), V(2.15f, Yard + 2.02f, 15.535f), true, false, "yard.line");
            Part("Sheets on the line", LabPieceKind.Wall, HouseLook.Cloth, V(-3.7f, Yard + 0.35f, 15.5f), V(1.9f, Yard + 2.0f, 15.56f), false, false, "yard.sheets");
            Part("Vegetable patch", LabPieceKind.Ledge, HouseLook.Wood, V(-13.6f, Yard, 11.8f), V(-12.0f, Yard + 0.35f, 14.8f));
            Shed();
            Part("Barbecue", LabPieceKind.Ledge, HouseLook.Furniture, V(2.4f, Yard + 0.02f, 12.8f), V(3.2f, Yard + 1.02f, 13.6f), true, false, "yard.grill");
            Part("Patio table", LabPieceKind.Ledge, HouseLook.Furniture, V(-2.6f, Yard + 0.02f, 12.4f), V(-1.0f, Yard + 0.76f, 13.6f), true, false, "yard.patio_table");
            foreach (var (cx, cz) in new[] { (-3.15f, 13f), (-0.45f, 13f) })
                Part("Patio chair", LabPieceKind.Ledge, HouseLook.Furniture, V(cx - 0.27f, Yard + 0.02f, cz - 0.27f), V(cx + 0.27f, Yard + 0.9f, cz + 0.27f), true, false, "yard.patio_chair", cx < -1f ? 90f : -90f);

            AddRoom("frontgarden", "The front garden", Yard, Yard + 30f, "yard", 1f, new PlanRect(YardX0, YardX1, FrontZ0, Z0 - e)).outside = true;
            AddRoom("yard", "The back yard", Yard, Yard + 30f, "yard", 1f, new PlanRect(YardX0, YardX1, Z1 + e, YardZ1), new PlanRect(YardX0, X0 - e, Z0 - e, Z1 + e), new PlanRect(MudX1 + e, YardX1, GarageZ1 + e, Z1 + e), new PlanRect(GarageX1 + e, YardX1, Z0 - e, GarageZ1 + e)).outside = true;
            AddRoom("street", "Rowan Street", StreetY - 0.15f, StreetY + 30f, "yard", 1f, new PlanRect(-34f, 40f, StreetZ0, FrontZ0)).outside = true;

            // The escape comes out on Chapel Row above the house (World's EscapeExit), facing up the hill.
            var ex = DeepBlock.HillAnchors.EscapeExit - DeepBlock.HillAnchors.HouseOrigin;
            exitDoor = V(ex.X, ex.Y, ex.Z);
            exitYaw = DeepBlock.HillAnchors.EscapeExitYaw;
        }

        /// <summary>Where the escape comes out (Chapel Row, in the house's frame) and which way it faces.</summary>
        public Vector3 exitDoor;
        public float exitYaw;
        /// <summary>Rowan Street's level in the house's frame (World's: 0.6 m under the lot).</summary>
        public const float StreetY = DeepBlock.HillAnchors.RowanY - DeepBlock.HillAnchors.LotGroundY;

        /// <summary>The oak and Theo's treehouse (the west side garden): a platform 3 m up round the trunk, a hut you have to
        /// crouch into, a ladder of steep steps (Theo climbs it; she can, or jump and mantle).</summary>
        public const float TreeX = -11.0f, TreeZ = 17.0f, TreeFloor = Yard + 3.0f;
        void Treehouse()
        {
            Part("The oak (trunk)", LabPieceKind.Wall, HouseLook.Tree, V(TreeX - 0.45f, Yard, TreeZ - 0.45f), V(TreeX + 0.45f, TreeFloor - 0.2f, TreeZ + 0.45f), true, true, "yard.tree");
            Part("The oak (trunk, through the treehouse)", LabPieceKind.Wall, HouseLook.Tree, V(TreeX - 0.35f, TreeFloor + 1.75f, TreeZ - 0.35f), V(TreeX + 0.35f, TreeFloor + 6.0f, TreeZ + 0.35f), true, false);
            Part("Treehouse platform", LabPieceKind.Floor, HouseLook.Wood, V(TreeX - 1.8f, TreeFloor - 0.2f, TreeZ - 1.8f), V(TreeX + 1.8f, TreeFloor, TreeZ + 1.8f));
            float hx0 = TreeX - 1.7f, hx1 = TreeX + 0.7f, hz0 = TreeZ - 1.1f, hz1 = TreeZ + 1.1f, hy = TreeFloor + 1.6f;
            _room = "treehouse";
            WallZ("Treehouse wall (west)", hx0 + 0.05f, hz0, hz1, TreeFloor, hy, 0.1f, Opening.Window(TreeZ - 0.4f, TreeZ + 0.4f, 0.7f, 1.3f, "Treehouse window"));
            WallX("Treehouse wall (south)", hz0 + 0.05f, hx0 + 0.1f, hx1 - 0.1f, TreeFloor, hy, 0.1f);
            WallX("Treehouse wall (north)", hz1 - 0.05f, hx0 + 0.1f, hx1 - 0.1f, TreeFloor, hy, 0.1f);
            WallZ("Treehouse wall (east, the low door)", hx1 - 0.05f, hz0, hz1, TreeFloor, hy, 0.1f, Opening.Low(TreeZ - 0.45f, TreeZ + 0.45f, "Treehouse door"));
            Part("Treehouse roof", LabPieceKind.Floor, HouseLook.Roof, V(hx0 - 0.15f, hy, hz0 - 0.15f), V(hx1 + 0.15f, hy + 0.15f, hz1 + 0.15f));
            _room = "yard";
            Part("Treehouse rail (east)", LabPieceKind.Wall, HouseLook.Wood, V(TreeX + 1.72f, TreeFloor, TreeZ - 1.8f), V(TreeX + 1.8f, TreeFloor + 0.9f, TreeZ - 0.5f));
            Part("Treehouse rail (east, north)", LabPieceKind.Wall, HouseLook.Wood, V(TreeX + 1.72f, TreeFloor, TreeZ + 0.5f), V(TreeX + 1.8f, TreeFloor + 0.9f, TreeZ + 1.8f));
            // The ladder: steep steps from the lawn up to the deck's east edge (where the rail has its gap).
            var ladder = Stairs("The treehouse ladder", new PlanRect(TreeX + 1.8f, TreeX + 4.8f, TreeZ - 0.45f, TreeZ + 0.45f), Yard, TreeFloor, true, -1);
            ladder.look = HouseLook.Wood;
            AddRoom("treehouse", "Theo's treehouse", TreeFloor, hy, "treehouse", 0.4f, new PlanRect(hx0 + 0.1f, hx1 - 0.1f, hz0 + 0.1f, hz1 - 0.1f));
        }

        public const float ShedX0 = 13.6f, ShedX1 = 16.8f, ShedZ0 = 17.6f, ShedZ1 = 20.6f;
        void Shed()
        {
            _room = "shed";
            float y0 = Yard, y1 = Yard + 2.3f;
            WallX("Shed wall (south)", ShedZ0, ShedX0 - 0.06f, ShedX1 + 0.06f, y0, y1, 0.12f, Opening.Window(14.7f, 15.7f, 1.1f, 1.8f, "Shed window"));
            WallX("Shed wall (north)", ShedZ1, ShedX0 - 0.06f, ShedX1 + 0.06f, y0, y1, 0.12f);
            WallZ("Shed wall (west, the door)", ShedX0, ShedZ0 + 0.06f, ShedZ1 - 0.06f, y0, y1, 0.12f, Opening.Door(18.6f, 19.6f, "Shed door", 1.95f));
            WallZ("Shed wall (east)", ShedX1, ShedZ0 + 0.06f, ShedZ1 - 0.06f, y0, y1, 0.12f);
            var r = Part("Shed roof", LabPieceKind.Ramp, HouseLook.Roof, V(ShedX0 - 0.3f, y1, ShedZ0 - 0.3f), V(ShedX1 + 0.3f, y1 + 0.7f, ShedZ1 + 0.3f));
            r.piece.rampTopStart = y1 + 0.25f; r.piece.rampTopEnd = y1 + 0.7f;
            Part("Shed floor", LabPieceKind.Floor, HouseLook.Wood, V(ShedX0 + 0.06f, y0, ShedZ0 + 0.06f), V(ShedX1 - 0.06f, y0 + 0.05f, ShedZ1 - 0.06f));
            AddRoom("shed", "The shed", Yard + 0.05f, y1, "shed", 0.2f, new PlanRect(ShedX0 + 0.06f, ShedX1 - 0.06f, ShedZ0 + 0.06f, ShedZ1 - 0.06f));
            _room = "yard";
        }
    }
}
