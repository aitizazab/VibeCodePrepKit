using System;
using System.Collections.Generic;
using System.Numerics;
using JamKit.Movement;
using JamKit.Stealth;

namespace JamKit.DreamEscape
{
    /// <summary>What a house piece looks like (the movement and stealth cores only see boxes; this is for the builder).</summary>
    public enum HouseLook
    {
        Lawn, Facade, Hallway, RunWall, Lintel, Stairs, Floor, Wall, Ceiling, Counter, Fridge, Table, Chair, Sofa, Bed,
        Desk, Closet, Curtain, TippedFloor, TippedWall, Porch, RainPath,
        // The architecture pass (2026-10-12): the house's own parts.
        Header, Chimney, Bookcase, WindowSeat, Bath, Railing, KneeWall, Gable, Roof, RoofStep, Storage,
    }

    /// <summary>A memory to find (the hide-and-seek's collectibles).</summary>
    public sealed class HouseMemory
    {
        public string id, name;
        public Vector3 position;
        /// <summary>Where she stands (feet) to take it.</summary>
        public Vector3 standAt;
    }

    /// <summary>A room of the house: its floor rectangle, floor height and ceiling height (above its floor).</summary>
    public sealed class HouseRoom
    {
        public string name;
        public float x0, x1, z0, z1, floorY, ceiling;
        public bool Contains(float x, float z) => x > x0 && x < x1 && z > z0 && z < z1;
    }

    /// <summary>A stealth light zone in the house.</summary>
    public sealed class HouseLight
    {
        public string name;
        public Vector3 center, size;
        public float level;
    }

    /// <summary>
    /// Night 2's house (the family home; docs/DREAM-HOUSE.md), as data in four parts along one straight route (+Z, s = z),
    /// on top of the Movement Lab's <see cref="LabCourse"/> so the course autopilot and the reach checks work on it:
    /// <list type="number">
    /// <item><b>The way in</b> (the house plays with her): the front door opens onto a hallway that stretches, its floor
    /// gone (run the gold-striped wall), then a door half open onto a slide down into the kitchen.</item>
    /// <item><b>The house</b> (pancakes and hide-and-seek; the architecture pass of 2026-10-12): a real family home's
    /// proportions. The main floor: the kitchen and dining room, the living room (bay window, fireplace), a narrow hall, the
    /// pantry, the parents' room and its bath, Theo's room; and the attic under the roof (Lena's observatory, a proposal,
    /// and the family's stored life), up steep stairs from Theo's room and down through a hatch into the hall. Hiding spots,
    /// light and shade, Theo's walking graph (the main floor), five memories to find. <see cref="rooms"/> lists the rooms.</item>
    /// <item><b>The house tips</b> (the escape): the hallway's floor is a wall to run, the doorframes are walls to kick
    /// between, the staircase is a slide; the house folds shut behind her.</item>
    /// <item><b>The front door</b>, into the rain: the umbrella beside it (a <see cref="DreamRainSpot"/>).</item>
    /// </list>
    /// Parallel to the course's pieces: <see cref="looks"/>. Sizes follow docs/RESEARCH-MOVEMENT.md 4.3 and the Movement
    /// Lab's proven offsets (the Gauntlet's wallrun and alley patterns).
    /// </summary>
    public sealed class DreamHouse
    {
        public const string ChapterArrival = "The house";
        public const string ChapterHideAndSeek = "Hide and seek";
        public const string ChapterArgument = "The argument";
        public const string ChapterTips = "The house tips";
        public const string ChapterFrontDoor = "The front door";

        /// <summary>The ground floor's height (the hide-and-seek rooms).</summary>
        public const float Ground = -4f;
        /// <summary>The house's footprint (the main floor and the attic over it): x and z bounds.</summary>
        public const float HubX0 = -7f, HubX1 = 8f, HubZ0 = 72f, HubZ1 = 84.4f;   // the north rooms 0.4 m deeper (round 4: the taller attic stairs need the run)
        /// <summary>The hall's walls (its south wall at z <see cref="HallZ0"/>, its north wall at <see cref="HallZ1"/>, 0.2 m each).</summary>
        public const float HallZ0 = 78f, HallZ1 = 80.6f;
        /// <summary>The living room's bay window.</summary>
        public const float BayX0 = 4f, BayX1 = 6.4f, BayZ0 = 71f;
        /// <summary>The attic stairs' slot (they climb south: their top at <see cref="StairZ0"/>, their foot at <see cref="StairZ1"/>).</summary>
        public const float StairX0 = 1.6f, StairX1 = 2.8f, StairZ0 = 80.8f, StairZ1 = HubZ1;
        /// <summary>The hatch in the hall's ceiling (the attic's way down).</summary>
        public const float HatchX0 = -1.2f, HatchX1 = -0.2f, HatchZ0 = 78.9f, HatchZ1 = 79.9f;
        /// <summary>The attic's floor is open over the stairs from <see cref="StairZ0"/> to here (a ceiling over their foot).</summary>
        public const float StairOpenZ1 = HubZ1 - 0.6f;
        /// <summary>The attic: its floor, its knee walls, its ridge (over z <see cref="RidgeZ"/>), its dormer.</summary>
        public const float AtticFloor = Ground + 3.3f + CeilingRaise, KneeWall = 0.8f, RidgeHeight = 3.4f, RidgeZ = (HubZ0 + HubZ1) * 0.5f;
        /// <summary>Round 4 (the creator picked +0.4 m): every main-floor ceiling is this much higher than the plan's numbers
        /// (<see cref="Ceiling"/> and the rooms add it), and the attic's floor with them; doors keep their height.</summary>
        public const float CeilingRaise = 0.4f;
        /// <summary>The dormer on the north slope, over the top of the attic stairs (round 4: it was over the observatory).</summary>
        public const float DormerX0 = 1.0f, DormerX1 = 3.4f, DormerZ0 = 80.6f, DormerHeight = 2.4f;
        /// <summary>Lena's observatory (round 4, the creator: "a cut-off in the sloped roof so it remains high-roofed in the
        /// observatory"): the attic west of <see cref="ObsX1"/> rises to a flat ceiling <see cref="ObsHeight"/> above the
        /// attic's floor, full height from wall to wall, under its own low roof with a skylight.</summary>
        public const float ObsX1 = -2.9f, ObsHeight = 4.2f;
        /// <summary>The skylight over the telescope.</summary>
        public const float SkyX0 = -6.6f, SkyX1 = -5.4f, SkyZ0 = 77.4f, SkyZ1 = 78.6f;
        public const float VoidBottom = -24f;
        /// <summary>The house seen from its front garden (round 4): the main floor's plan turned so the hall's east end, the
        /// front door, faces the garden (x = z - <see cref="FrontDoorZ"/>, z = 8.2 - x); the walls' outer faces and the eaves.</summary>
        /// The right side stands 0.8 m further out than the plan, so the stretched hallway's wall (x 4.5..5.5) stays inside.
        public const float FrontDoorZ = 79.4f, OutX0 = HubZ0 - 0.2f - FrontDoorZ, OutX1 = HubZ1 + 0.2f - FrontDoorZ + 0.8f, EaveY = AtticFloor - Ground + KneeWall;
        /// <summary>The house's back wall outside (z), and how far back the side gardens run.</summary>
        public const float OutZ1 = 8.2f - (HubX0 - 0.2f), SideGardenZ = 10.8f;

        public readonly LabCourse course = new LabCourse();
        public readonly List<HouseLook> looks = new List<HouseLook>();
        public readonly List<CourseBeat> beats = new List<CourseBeat>();
        // The stealth part (the ground floor).
        public readonly List<HidingSpot> spots = new List<HidingSpot>();
        public readonly List<Vector3> nodes = new List<Vector3>();
        public readonly List<HouseLight> lights = new List<HouseLight>();
        public readonly List<HouseMemory> memories = new List<HouseMemory>();
        public readonly List<HouseRoom> rooms = new List<HouseRoom>();
        /// <summary>Places in the open (one per main room) where Theo can see her.</summary>
        public readonly List<Vector3> openFloor = new List<Vector3>();
        public float ambientLight = 0.8f;
        public float connectDistance = 9f;
        /// <summary>Theo counts at the table, facing the kitchen's south wall.</summary>
        public Vector3 theoStart;
        public float theoYaw, theoCountYaw;
        /// <summary>Lena at the stove (a cut-out), and the table where they sit down to pancakes.</summary>
        public Vector3 lenaAt, table;
        public float tableRadius = 3.2f;
        /// <summary>Where Nadia stands when she's in the kitchen (the foot of the slide).</summary>
        public Vector3 kitchenStart;
        /// <summary>Past this (and down on the ground floor) she's in the kitchen: the pancakes begin.</summary>
        public float kitchenS = 73f;
        /// <summary>The hallway that stretches as she enters it: its far end moves from <see cref="stretchFrom"/> to
        /// <see cref="stretchTo"/> (s) when she passes <see cref="stretchTrigger"/>.</summary>
        public float stretchTrigger = 4f, stretchFrom = 26f, stretchTo = 56f;
        /// <summary>The escape: where she lands after the house throws her out, and the chase's span.</summary>
        public float escapeStartS, chaseStartS, chaseEndS;
        /// <summary>The front door, the umbrella and the rain path (normal; <see cref="DreamHouseStory.RainFor"/> adjusts it).</summary>
        public DreamRainSpot rain;
        public int arrivalCheckpoint, hubCheckpoint, escapeCheckpoint, frontDoorCheckpoint;

        public static DreamHouse Build()
        {
            var h = new DreamHouse();
            h.Make();
            return h;
        }

        public static bool InHub(Vector3 feet) =>
            feet.X > HubX0 && feet.X < HubX1 && feet.Z > HubZ0 && feet.Z < HubZ1 && feet.Y < Ground + 2.5f && feet.Y > Ground - 1f;

        public int CheckpointIndex(string chapter) => course.CheckpointIndex(chapter);

        public List<string> CheckReach(MoveDef def, float skin = 0.04f, float crouchStepOffset = 0f) => CourseReach.Check(beats, def, skin, crouchStepOffset);

        /// <summary>A stealth world of the ground floor: every solid piece blocks sight; the light zones.</summary>
        public StealthBoxWorld StealthWorld()
        {
            var w = new StealthBoxWorld { ambient = ambientLight };
            for (int i = 0; i < course.pieces.Count; i++)
            {
                var p = course.pieces[i];
                if (!p.solid || p.Max.Z < HubZ0 - 1f || p.Min.Z > HubZ1 + 1f) continue;
                if (looks[i] == HouseLook.Floor || looks[i] == HouseLook.RoofStep) continue; // under the eyes, or over the roof
                // The ceilings do block: the attic is over them (Theo on the main floor can't see her up there).
                w.AddBlocker(p.center, p.size);
            }
            foreach (var l in lights) w.AddLight(l.center, l.size, l.level);
            return w;
        }

        /// <summary>Theo's walking graph over <paramref name="world"/>.</summary>
        public WaypointGraph Graph(IStealthWorld world)
        {
            var g = new WaypointGraph();
            foreach (var n in nodes) g.Add(n);
            g.AutoConnect(world, connectDistance);
            return g;
        }

        public int SpotAt(Vector3 feet)
        {
            for (int i = 0; i < spots.Count; i++) if (spots[i].Contains(feet)) return i;
            return -1;
        }

        // ---------- Building ----------

        LabPiece Add(string name, LabPieceKind kind, HouseLook look, Vector3 min, Vector3 max, bool solid = true, bool runnable = false)
        {
            var p = new LabPiece { name = name, kind = kind, center = (min + max) * 0.5f, size = max - min, runnable = runnable, solid = solid };
            course.pieces.Add(p);
            looks.Add(look);
            return p;
        }

        LabPiece Ramp(string name, HouseLook look, float x0, float x1, float z0, float z1, float top0, float top1)
        {
            var p = Add(name, LabPieceKind.Ramp, look, new Vector3(x0, Math.Min(top0, top1) - 2f, z0), new Vector3(x1, Math.Max(top0, top1), z1));
            p.rampTopStart = top0;
            p.rampTopEnd = top1;
            return p;
        }

        /// <summary>A runnable wall (gold stripe), 1 m thick, its face at x = <paramref name="faceX"/>.</summary>
        LabPiece RunWall(string name, HouseLook look, float faceX, float z0, float z1, float top)
        {
            float x0 = faceX > 0f ? faceX : faceX - 1f;
            return Add(name, LabPieceKind.Wall, look, new Vector3(x0, VoidBottom, z0), new Vector3(x0 + 1f, top, z1), true, true);
        }

        /// <summary>A wall of the main floor (from the floor up to the attic's floor), or of any height and look.</summary>
        void Wall(string name, float x0, float x1, float z0, float z1) =>
            Add(name, LabPieceKind.Wall, HouseLook.Wall, new Vector3(x0, Ground, z0), new Vector3(x1, AtticFloor, z1));

        void Wall(string name, float x0, float x1, float z0, float z1, float y0, float y1, HouseLook look) =>
            Add(name, LabPieceKind.Wall, look, new Vector3(x0, y0, z0), new Vector3(x1, y1, z1));

        /// <summary>The wall over a doorway (or an archway, a pass-through): from <paramref name="height"/> above the floor up
        /// to the attic's floor.</summary>
        void Header(string name, float x0, float x1, float z0, float z1, float height) =>
            Add(name, LabPieceKind.Bar, HouseLook.Header, new Vector3(x0, Ground + height, z0), new Vector3(x1, AtticFloor, z1));

        /// <summary>A room's ceiling: solid from <paramref name="height"/> above the floor up to the attic's floor.</summary>
        void Ceiling(string name, float x0, float x1, float z0, float z1, float height) =>
            Add(name, LabPieceKind.Floor, HouseLook.Ceiling, new Vector3(x0, Ground + height + CeilingRaise, z0), new Vector3(x1, AtticFloor, z1));

        void Room(string name, float x0, float x1, float z0, float z1, float floorY, float ceiling) =>
            rooms.Add(new HouseRoom { name = name, x0 = x0, x1 = x1, z0 = z0, z1 = z1, floorY = floorY, ceiling = ceiling + (floorY == Ground ? CeilingRaise : 0f) });

        /// <summary>The attic roof's height over its floor at <paramref name="z"/> (the gable: knee walls at the eaves, the
        /// ridge over <see cref="RidgeZ"/>).</summary>
        public static float RoofAt(float z)
        {
            float half = Math.Max(RidgeZ - HubZ0, HubZ1 - RidgeZ);
            float t = 1f - Math.Min(1f, Math.Abs(z - RidgeZ) / half);
            return KneeWall + (RidgeHeight - KneeWall) * t;
        }

        /// <summary>Solid steps just behind one slope of the roof (three, the third the ridge cap), so a jump stays inside.</summary>
        void RoofSteps(float x0, float x1, bool south, int fromStep)
        {
            float half = RidgeZ - HubZ0;
            for (int i = fromStep; i <= 3; i++)
            {
                float h = KneeWall + (RidgeHeight - KneeWall) * i / 3f;
                float dz = half * i / 3f;
                float za = south ? HubZ0 : HubZ1 - dz, zb = south ? HubZ0 + dz : HubZ1;
                Add("Roof (" + (south ? "south" : "north") + ", step " + i + ")", LabPieceKind.Floor, HouseLook.RoofStep,
                    new Vector3(x0, AtticFloor + h, za), new Vector3(x1, AtticFloor + RidgeHeight + 0.4f, zb));
            }
        }

        void Furniture(string name, HouseLook look, float x0, float x1, float z0, float z1, float height) =>
            Add(name, LabPieceKind.Ledge, look, new Vector3(x0, Ground, z0), new Vector3(x1, Ground + height, z1));

        /// <summary>Stored things on the attic's floor (boxes, a covered wardrobe): solid, climbable, under the roof.</summary>
        void Storage(string name, float x0, float x1, float z0, float z1, float height) =>
            Add(name, LabPieceKind.Ledge, HouseLook.Storage, new Vector3(x0, AtticFloor, z0), new Vector3(x1, AtticFloor + height, z1));

        void Cue(LabCueKind kind, float s, float sEnd = 0f) => course.cues.Add(new LabCue { kind = kind, s = s, sEnd = sEnd });

        void Lane(float s, float x) => course.lane.Add(new Vector2(s, x));

        void Beat(CourseBeatKind kind, string name, float s, float size, float height = 0f) =>
            beats.Add(new CourseBeat { kind = kind, name = name, s = s, size = size, height = height });

        int Checkpoint(string name, float z, float floorTop, string chapter = "", float x = float.NaN)
        {
            course.checkpoints.Add(new LabCheckpoint { name = name, chapter = chapter, position = new Vector3(float.IsNaN(x) ? course.LaneAt(z) : x, floorTop + 0.02f, z), yaw = 0f, s = z });
            return course.checkpoints.Count - 1;
        }

        void Section(float s, string name) => course.sections.Add(new KeyValuePair<float, string>(s, name));

        void Spot(string name, HideKind kind, Vector3 center, Vector3 size, Vector3 approach) =>
            spots.Add(HidingSpot.Box(name, kind, center, size, approach));

        void Light(string name, Vector3 center, Vector3 size, float level) => lights.Add(new HouseLight { name = name, center = center, size = size, level = level });

        void Memory(string id, string name, Vector3 at, Vector3 standAt) => memories.Add(new HouseMemory { id = id, name = name, position = at, standAt = standAt });

        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        void Make()
        {
            var c = course;
            c.killY = -16f;
            const float F = Ground;

            // ===== 1. The way in: the front door opens onto a hallway that stretches (its floor gone: run the striped wall),
            // then a door half open onto a slide down into the kitchen.
            Section(-24f, "The front garden");
            Lane(-24f, 0f);
            Add("Front garden", LabPieceKind.Floor, HouseLook.Lawn, V(-14f, -2f, -24f), V(14f, 0f, 0f));
            arrivalCheckpoint = Checkpoint("The house", -16f, 0f, ChapterArrival);
            // The house from outside (round 4: it matches the inside, seen from its front, the hall's east end; the builder
            // draws the rest of it, DreamHouseExterior): its front wall up to the eaves, the outside chimney, and tall
            // hedges from its corners to the garden's edges.
            Add("House front (left)", LabPieceKind.Wall, HouseLook.Facade, V(OutX0, 0f, 0f), V(-1.5f, EaveY, 0.5f));
            Add("House front (right)", LabPieceKind.Wall, HouseLook.Facade, V(1.5f, 0f, 0f), V(OutX1, EaveY, 0.5f));
            Add("Front door lintel", LabPieceKind.Wall, HouseLook.Facade, V(-1.5f, 2.6f, 0f), V(1.5f, EaveY, 0.5f));
            Add("Chimney (outside, base)", LabPieceKind.Wall, HouseLook.RoofStep, V(-5.1f, 0f, -0.6f), V(-3.3f, 2f, 0f));
            Add("Chimney (outside, stack)", LabPieceKind.Wall, HouseLook.RoofStep, V(-4.8f, 2f, -0.55f), V(-3.6f, 7.0f, 0f));
            // The garden runs on round both sides of the house (so the bay and the sides show), to tall hedges level with
            // the observatory; the house's sides and its bay are solid.
            Add("Side garden (left)", LabPieceKind.Floor, HouseLook.Lawn, V(-14f, -2f, 0f), V(OutX0, 0f, SideGardenZ));
            Add("Side garden (right)", LabPieceKind.Floor, HouseLook.Lawn, V(OutX1, -2f, 0f), V(14f, 0f, SideGardenZ));
            Add("House side (left)", LabPieceKind.Wall, HouseLook.RoofStep, V(OutX0, 0f, 0f), V(OutX0 + 0.25f, 8f, OutZ1));
            Add("House side (right)", LabPieceKind.Wall, HouseLook.RoofStep, V(OutX1 - 0.25f, 0f, 0f), V(OutX1, 8f, OutZ1));
            Add("The bay (outside)", LabPieceKind.Wall, HouseLook.RoofStep, V(OutX0 - (HubZ0 - BayZ0), 0f, 8.2f - BayX1 - 0.2f), V(OutX0, 4.3f, 8.2f - BayX0 + 0.2f));
            Add("Garden hedge (left)", LabPieceKind.Wall, HouseLook.RoofStep, V(-14f, 0f, SideGardenZ - 0.8f), V(OutX0, 2.6f, SideGardenZ));
            Add("Garden hedge (right)", LabPieceKind.Wall, HouseLook.RoofStep, V(OutX1, 0f, SideGardenZ - 0.8f), V(14f, 2.6f, SideGardenZ));
            Section(0.5f, "The stretched hallway");
            Add("Hallway floor", LabPieceKind.Floor, HouseLook.Hallway, V(-4.5f, -2f, 0.5f), V(4.5f, 0f, 22f));
            Checkpoint("The hallway", 4f, 0f);
            Add("Hallway wall (left)", LabPieceKind.Wall, HouseLook.Wall, V(-5.5f, VoidBottom, 0.5f), V(-4.5f, 4f, 56f));
            Add("Hallway wall (right, before)", LabPieceKind.Wall, HouseLook.Wall, V(4.5f, VoidBottom, 0.5f), V(5.5f, 4f, 24.8f));
            Lane(2f, 0f); Lane(10f, 3.6f);
            Cue(LabCueKind.Jump, 21.2f);
            RunWall("The hallway wall (run it)", HouseLook.RunWall, 4.5f, 25f, 39f, 4f);
            Cue(LabCueKind.WallJump, 34.5f);
            Beat(CourseBeatKind.Wallrun, "The stretched hallway's wall", 21.2f, 14f, 4f);
            Lane(36f, 3.6f); Lane(42f, 0f);
            Add("Hallway wall (right, after)", LabPieceKind.Wall, HouseLook.Wall, V(4.5f, VoidBottom, 39.2f), V(5.5f, 4f, 56f));
            Add("Hallway floor (far end)", LabPieceKind.Floor, HouseLook.Hallway, V(-4.5f, -3f, 40f), V(4.5f, -1f, 56f));
            // The door half open onto the slide: under its lintel only sliding fits; the floor beyond is the slide down.
            Add("Slide door (left)", LabPieceKind.Wall, HouseLook.Wall, V(-5.5f, -1f, 56f), V(-1.5f, 3f, 56.5f));
            Add("Slide door (right)", LabPieceKind.Wall, HouseLook.Wall, V(1.5f, -1f, 56f), V(5.5f, 3f, 56.5f));
            Add("Slide door (half open: its lintel)", LabPieceKind.Bar, HouseLook.Lintel, V(-1.5f, -1f + 1.15f, 56f), V(1.5f, 3f, 56.5f));
            Cue(LabCueKind.Crouch, 52f, 72f);
            Beat(CourseBeatKind.Slide, "Under the half-open door", 52f, 0.5f, 1.15f);
            Ramp("The slide into the kitchen", HouseLook.Stairs, -2f, 2f, 56.5f, 72f, -1f, F);
            Beat(CourseBeatKind.SlideRamp, "The slide into the kitchen", 56.5f, 15.5f, 3f);
            Add("Slide wall (left)", LabPieceKind.Wall, HouseLook.Wall, V(-2.5f, F, 56.5f), V(-2f, 0f, 72f));
            Add("Slide wall (right)", LabPieceKind.Wall, HouseLook.Wall, V(2f, F, 56.5f), V(2.5f, 0f, 72f));

            // ===== 2. The house itself (architecture pass, 2026-10-12): a real family home's proportions, one main floor and
            // the attic under the roof. Pancakes, then hide-and-seek with Theo on the main floor, five memories to find.
            // Plan (z north, x east; walls 0.2 m): the kitchen and dining room (open plan, the slide lands in it) and the
            // living room (bay window, fireplace) across the south; a narrow hall; the parents' room with its en-suite bath,
            // the attic stairs' slot and Theo's room across the north; the pantry between the kitchen and the hall (two
            // doors: a loop). Ceilings differ per room; the attic is the one place the dream exaggerates.
            Section(72f, "The ground floor");
            Add("Ground floor", LabPieceKind.Floor, HouseLook.Floor, V(HubX0 - 0.4f, F - 2f, HubZ0 - 1.4f), V(HubX1 + 0.4f, F, HubZ1 + 0.4f));
            kitchenStart = V(0f, F, 73.2f);
            hubCheckpoint = Checkpoint("The kitchen", 73.2f, F, ChapterHideAndSeek, 0f);

            // --- The outer walls (the south one open where the slide comes in, and pushed out into the bay).
            Wall("Outer wall (west)", HubX0 - 0.2f, HubX0, HubZ0 - 0.2f, HubZ1 + 0.2f);
            Wall("Outer wall (east)", HubX1, HubX1 + 0.2f, HubZ0 - 0.2f, HubZ1 + 0.2f);
            Wall("Outer wall (north)", HubX0 - 0.2f, HubX1 + 0.2f, HubZ1, HubZ1 + 0.2f);
            Wall("Outer wall (south, kitchen)", HubX0 - 0.2f, -2f, HubZ0 - 0.2f, HubZ0);
            Wall("Outer wall (south, living)", 2f, BayX0, HubZ0 - 0.2f, HubZ0);
            Wall("Outer wall (south, east)", BayX1, HubX1 + 0.2f, HubZ0 - 0.2f, HubZ0);
            Wall("Bay window (west side)", BayX0 - 0.2f, BayX0, BayZ0, HubZ0 - 0.2f);
            Wall("Bay window (east side)", BayX1, BayX1 + 0.2f, BayZ0, HubZ0 - 0.2f);
            Wall("Bay window (front)", BayX0 - 0.2f, BayX1 + 0.2f, BayZ0 - 0.2f, BayZ0);

            // --- Kitchen and dining | living room: an archway, then a pass-through over a half wall, then a full wall.
            Wall("Kitchen | living (south end)", 2.6f, 2.8f, HubZ0, 72.6f);
            Header("Archway (kitchen | living)", 2.6f, 2.8f, 72.6f, 74.6f, 2.3f);
            Add("Half wall (the pass-through)", LabPieceKind.Ledge, HouseLook.Wall, V(2.6f, F, 74.6f), V(2.8f, F + 1.05f, 77.2f));
            Header("Pass-through (over the half wall)", 2.6f, 2.8f, 74.6f, 77.2f, 2.2f);
            Wall("Kitchen | living (north end)", 2.6f, 2.8f, 77.2f, HallZ0);

            // --- The hall's south wall: the pantry's kitchen door, the dining room's door, the living room's door.
            Wall("Hall wall (south, pantry)", HubX0, -6.6f, HallZ0, HallZ0 + 0.2f);
            Header("Pantry door (kitchen side)", -6.6f, -5.8f, HallZ0, HallZ0 + 0.2f, 2.0f);
            Wall("Hall wall (south, kitchen)", -5.8f, -3f, HallZ0, HallZ0 + 0.2f);
            Header("Door (dining | hall)", -3f, -2f, HallZ0, HallZ0 + 0.2f, 2.1f);
            Wall("Hall wall (south, middle)", -2f, 5f, HallZ0, HallZ0 + 0.2f);
            Header("Door (living | hall)", 5f, 6f, HallZ0, HallZ0 + 0.2f, 2.1f);
            Wall("Hall wall (south, east)", 6f, HubX1, HallZ0, HallZ0 + 0.2f);
            // The pantry (x -7..-5.6 in the hall's band): its hall door has a curtain.
            Wall("Pantry wall (hall side, south)", -5.6f, -5.4f, HallZ0 + 0.2f, 79f);
            Header("Pantry door (hall side)", -5.6f, -5.4f, 79f, 80f, 2.0f);
            Wall("Pantry wall (hall side, north)", -5.6f, -5.4f, 80f, HallZ1);
            Add("Pantry curtain", LabPieceKind.Wall, HouseLook.Curtain, V(-5.45f, F, 79f), V(-5.4f, F + 2f, 80f), solid: false);
            Furniture("Pantry shelves", HouseLook.Closet, HubX0, -6.6f, HallZ0 + 0.2f, HallZ1, 2.0f);
            Spot("The pantry", HideKind.Closet, V(-6.1f, F + 1f, 79.4f), V(1.0f, 2f, 2.2f), V(-4.7f, F, 79.4f));

            // --- The hall's north wall: the parents' door, the photo wall, the cupboard under the stairs, Theo's door.
            Wall("Hall wall (north, west)", HubX0, -5f, HallZ1, HallZ1 + 0.2f);
            Header("Door (hall | parents)", -5f, -4f, HallZ1, HallZ1 + 0.2f, 2.1f);
            Wall("Hall wall (north, middle)", -4f, StairX0, HallZ1, HallZ1 + 0.2f);
            Header("Cupboard under the stairs (its low door)", StairX0, StairX1, HallZ1, HallZ1 + 0.2f, 1.25f);
            Wall("Hall wall (north, by the stairs)", StairX1, 4f, HallZ1, HallZ1 + 0.2f);
            Header("Door (hall | Theo)", 4f, 5f, HallZ1, HallZ1 + 0.2f, 2.1f);
            Wall("Hall wall (north, east)", 5f, HubX1, HallZ1, HallZ1 + 0.2f);

            // --- The north rooms: the parents' room, its bath (en suite), the attic stairs' slot, Theo's room.
            Wall("Parents | bath", -1.4f, -1.2f, HallZ1 + 0.2f, 82f);
            Header("Door (parents | bath)", -1.4f, -1.2f, 82f, 83f, 2.1f);
            Wall("Parents | bath (north end)", -1.4f, -1.2f, 83f, HubZ1);
            Wall("Bath | attic stairs", 1.4f, StairX0, HallZ1 + 0.2f, HubZ1);
            Wall("Attic stairs | Theo", StairX1, 3f, HallZ1 + 0.2f, HubZ1 - 1f);
            Header("Door (Theo | the attic stairs)", StairX1, 3f, HubZ1 - 1f, HubZ1, 2.1f);
            // The attic stairs: steep, in their own slot, climbing south from a narrow door in Theo's room to the floor over
            // the hall; the cupboard under their high end opens from the hall (a low door: crouch in).
            Ramp("The attic stairs", HouseLook.Stairs, StairX0, StairX1, StairZ0, StairZ1, AtticFloor, F);
            Spot("The cupboard under the stairs", HideKind.Closet, V((StairX0 + StairX1) * 0.5f, F + 0.6f, StairZ0 + 0.45f), V(StairX1 - StairX0, 1.2f, 0.9f),
                V((StairX0 + StairX1) * 0.5f, F, 79.4f));

            // --- The ceilings (solid, from each room's height up to the attic's floor; the stairwell and the hatch open).
            Ceiling("Ceiling (kitchen and dining)", HubX0, 2.6f, HubZ0, HallZ0, 2.8f);
            Ceiling("Ceiling (living room)", 2.8f, HubX1, HubZ0, HallZ0, 3.0f);
            Ceiling("Ceiling (bay window)", BayX0, BayX1, BayZ0, HubZ0, 2.5f);
            Ceiling("Ceiling (pantry)", HubX0, -5.4f, HallZ0, HallZ1, 2.3f);
            Ceiling("Ceiling (hall, south)", -5.4f, HubX1, HallZ0 + 0.2f, HatchZ0, 2.4f);
            Ceiling("Ceiling (hall, west of the hatch)", -5.4f, HatchX0, HatchZ0, HatchZ1, 2.4f);
            Ceiling("Ceiling (hall, east of the hatch)", HatchX1, HubX1, HatchZ0, HatchZ1, 2.4f);
            Ceiling("Ceiling (hall, north)", -5.4f, HubX1, HatchZ1, HallZ1, 2.4f);
            Ceiling("Ceiling (parents' room)", HubX0, -1.4f, HallZ1, HubZ1, 2.7f);
            Ceiling("Ceiling (bath)", -1.4f, StairX0, HallZ1, HubZ1, 2.4f);
            Ceiling("Ceiling (the foot of the attic stairs)", StairX0, StairX1, StairOpenZ1, HubZ1, 2.6f);
            Ceiling("Ceiling (Theo's room)", StairX1, HubX1, HallZ1, HubZ1, 2.6f);

            // --- Furniture that counts (it blocks, Theo can't see through it, some of it is climbed).
            // The kitchen: an L of counter (the stove on its south run), the fridge (the calendar), the table set for four.
            Furniture("Counter and stove", HouseLook.Counter, HubX0, -3.6f, HubZ0, 72.65f, 0.95f);
            Furniture("Counter (west)", HouseLook.Counter, HubX0, -6.35f, 72.65f, 75.2f, 0.95f);
            Furniture("Fridge", HouseLook.Fridge, HubX0, -6.25f, 76.4f, 77.8f, 2f);
            Furniture("Dining table", HouseLook.Table, -4.8f, -2f, 75f, 76.4f, 0.78f);
            Furniture("Chair (Lena's)", HouseLook.Chair, -5.4f, -4.9f, 75.4f, 76f, 0.5f);
            Furniture("Chair (Nadia's: the fourth chair)", HouseLook.Chair, -1.9f, -1.4f, 75.4f, 76f, 0.5f);
            Furniture("Chair (Theo's)", HouseLook.Chair, -3.7f, -3.1f, 74.2f, 74.7f, 0.5f);
            Furniture("Chair (Elias's)", HouseLook.Chair, -3.7f, -3.1f, 76.7f, 77.2f, 0.5f);
            table = V(-3.4f, F, 75.7f);
            tableRadius = 2.4f;
            lenaAt = V(-5.2f, F, 73.25f);
            theoStart = V(-3.4f, F, 73.55f);
            theoYaw = 180f;
            theoCountYaw = 180f; // counting at the table, eyes on the kitchen's south wall
            // The living room: the sofa faces the fireplace across the coffee table; the armchair; the window seat in the bay.
            Add("Chimney breast", LabPieceKind.Wall, HouseLook.Chimney, V(7.4f, F, 74f), V(HubX1, AtticFloor, 76.4f));
            Furniture("Sofa", HouseLook.Sofa, 3.4f, 4.3f, 73.8f, 76.2f, 0.9f);
            Furniture("Coffee table", HouseLook.Table, 4.8f, 5.6f, 74.4f, 75.6f, 0.45f);
            Furniture("Armchair", HouseLook.Sofa, 6.6f, 7.4f, 76.8f, 77.7f, 0.9f);
            Furniture("Bookcase (living room)", HouseLook.Bookcase, 2.8f, 3.25f, 77.2f, HallZ0, 1.9f);
            Furniture("Window seat", HouseLook.WindowSeat, BayX0, BayX1, BayZ0, BayZ0 + 0.55f, 0.45f);
            Add("Curtains", LabPieceKind.Wall, HouseLook.Curtain, V(BayX0, F, HubZ0 - 0.15f), V(BayX1, F + 2.4f + CeilingRaise, HubZ0 - 0.05f), solid: false);
            Spot("The window seat", HideKind.Bush, V((BayX0 + BayX1) * 0.5f, F + 1.2f, (BayZ0 + HubZ0) * 0.5f), V(BayX1 - BayX0, 2.4f, HubZ0 - BayZ0), V(5.2f, F, 73.4f));
            // The hall: the front door at its east end (round 4: the outside matches the inside), the coats on hooks
            // along the north wall beside it.
            Add("Coats", LabPieceKind.Wall, HouseLook.Curtain, V(6.2f, F + 0.4f, HallZ1 - 0.7f), V(7.8f, F + 2.2f, HallZ1), solid: false);
            Spot("The coats", HideKind.Bush, V(7.0f, F + 1f, HallZ1 - 0.35f), V(1.6f, 2f, 0.8f), V(7.0f, F, 79.2f));
            // The parents' room: the bed, head to the north wall. The bath: the tub (its curtain a hiding spot).
            Furniture("Parents' bed", HouseLook.Bed, -6.2f, -4.2f, HubZ1 - 2f, HubZ1, 0.55f);
            Furniture("Bathtub", HouseLook.Bath, -1.2f, 1.4f, HubZ1 - 0.75f, HubZ1, 0.6f);
            Spot("The bath curtain", HideKind.Bush, V(0.1f, F + 1f, HubZ1 - 0.4f), V(2.4f, 2f, 0.8f), V(0.1f, F, 82.6f));
            // Theo's room: the bunk bed, his wardrobe (a closet) by the door, his tall bookcase with the desk beside it
            // (climb the desk to see what's on top of the bookcase).
            Furniture("Theo's bunk bed", HouseLook.Bed, 5.4f, 7.4f, HubZ1 - 0.9f, HubZ1, 1.6f);
            Furniture("Wardrobe side (west)", HouseLook.Closet, 5.4f, 5.6f, HallZ1 + 0.2f, 81.7f, 2.1f);
            Furniture("Wardrobe side (east)", HouseLook.Closet, 6.6f, 6.8f, HallZ1 + 0.2f, 81.7f, 2.1f);
            Add("Wardrobe roof", LabPieceKind.Ledge, HouseLook.Closet, V(5.4f, F + 2.1f, HallZ1 + 0.2f), V(6.8f, F + 2.25f, 81.7f));
            Spot("Theo's wardrobe", HideKind.Closet, V(6.1f, F + 1f, 81.25f), V(1.0f, 2f, 0.9f), V(6.1f, F, 82.4f));
            Furniture("Bookcase (Theo's room)", HouseLook.Bookcase, 7.4f, HubX1, 81f, 82.2f, 2.0f);
            Furniture("Theo's desk", HouseLook.Desk, 7.4f, HubX1, 82.3f, 83f, 0.75f);

            // ===== The attic: Lena's observatory (a proposal: STORY-PROSE's "a small observatory in the attic?") and the
            // family's stored life. Up the stairs from Theo's room; down through the hatch into the hall (a loop).
            // Under a gable roof, its ridge running east-west over the middle of the house.
            // Railings round the stairwell (she arrives at its south end and steps off onto the floor over the hall).
            Wall("Attic railing (stairwell, west)", StairX0 - 0.2f, StairX0, StairZ0, StairOpenZ1 + 0.2f, AtticFloor, AtticFloor + 0.9f, HouseLook.Railing);
            Wall("Attic railing (stairwell, east)", StairX1, StairX1 + 0.2f, StairZ0, StairOpenZ1 + 0.2f, AtticFloor, AtticFloor + 0.9f, HouseLook.Railing);
            Wall("Attic railing (stairwell, north)", StairX0 - 0.2f, StairX1 + 0.2f, StairOpenZ1, StairOpenZ1 + 0.2f, AtticFloor, AtticFloor + 0.9f, HouseLook.Railing);
            Wall("Attic knee wall (south)", ObsX1, HubX1, HubZ0, HubZ0 + 0.3f, AtticFloor, AtticFloor + KneeWall, HouseLook.KneeWall);
            Wall("Attic knee wall (north)", ObsX1, HubX1, HubZ1 - 0.3f, HubZ1, AtticFloor, AtticFloor + KneeWall, HouseLook.KneeWall);
            // The observatory: its walls rise full height to its flat ceiling (the roof cut and raised over it).
            Wall("Observatory wall (south)", HubX0, ObsX1, HubZ0, HubZ0 + 0.3f, AtticFloor, AtticFloor + ObsHeight, HouseLook.Gable);
            Wall("Observatory wall (north)", HubX0, ObsX1, HubZ1 - 0.3f, HubZ1, AtticFloor, AtticFloor + ObsHeight, HouseLook.Gable);
            Add("Observatory ceiling", LabPieceKind.Floor, HouseLook.RoofStep, V(HubX0 - 0.3f, AtticFloor + ObsHeight, HubZ0), V(ObsX1, AtticFloor + ObsHeight + 0.3f, HubZ1));
            Add("Attic gable (west, the observatory window)", LabPieceKind.Wall, HouseLook.Gable, V(HubX0 - 0.3f, AtticFloor, HubZ0), V(HubX0, AtticFloor + ObsHeight, HubZ1));
            Add("Attic gable (east)", LabPieceKind.Wall, HouseLook.Gable, V(HubX1, AtticFloor, HubZ0), V(HubX1 + 0.3f, AtticFloor + RidgeHeight + 0.3f, HubZ1));
            // The roof is drawn by the builder; these solid steps sit just behind its slopes so a jump can't leave the house.
            RoofSteps(ObsX1, HubX1, south: true, fromStep: 1);
            RoofSteps(ObsX1, DormerX0, south: false, fromStep: 1);
            RoofSteps(DormerX1, HubX1, south: false, fromStep: 1);
            RoofSteps(DormerX0, DormerX1, south: false, fromStep: 3);   // over the dormer only the ridge cap
            // The dormer on the north slope, over the top of the attic stairs: its own walls and roof, a window to the north.
            // (Its cheeks are only above the slope, drawn by the builder: under them the attic runs on, so the top of the stairs
            // opens onto the attic; the roof's steps either side still stop a jump.)
            Add("Dormer roof", LabPieceKind.Floor, HouseLook.Roof, V(DormerX0 - 0.2f, AtticFloor + DormerHeight, DormerZ0), V(DormerX1 + 0.2f, AtticFloor + DormerHeight + 0.2f, HubZ1));
            // What's stored up here (things to climb, too): a trunk, crates, Theo's old crib; Lena's observatory to the west.
            Add("Trunk", LabPieceKind.Ledge, HouseLook.Storage, V(3.2f, AtticFloor, 74.6f), V(4.4f, AtticFloor + 0.6f, 75.4f));
            Add("Crates", LabPieceKind.Ledge, HouseLook.Storage, V(4.8f, AtticFloor, 74.4f), V(5.8f, AtticFloor + 1.2f, 75.4f));
            Add("Theo's old crib", LabPieceKind.Ledge, HouseLook.Storage, V(0.6f, AtticFloor, 76f), V(2.0f, AtticFloor + 0.9f, 76.8f));
            Add("Observatory platform", LabPieceKind.Ledge, HouseLook.Storage, V(HubX0, AtticFloor, 76.4f), V(-5.6f, AtticFloor + 0.3f, 79.6f));
            // The chimney stack rising through the attic from the living room's chimney breast.
            Add("Chimney stack (attic)", LabPieceKind.Wall, HouseLook.Chimney, V(7.4f, AtticFloor, 74f), V(HubX1, AtticFloor + RoofAt(76.4f), 76.4f));
            // The stored life walls the attic into two (the architecture pass's second round, 2026-10-12): a wall of boxes and
            // an old wardrobe under a dust sheet runs across it, stepped down under the roof, with one gap hung with sheets on a
            // line: the storage side (the stairs and the hatch) and, through the sheets, the observatory (the reveal). The
            // low eaves on the storage side are filled with more boxes, so the walkable attic is the band under the ridge.
            float px0 = -2.9f, px1 = -2.2f;
            Storage("Boxes (the wall across, south eave)", px0, px1, HubZ0 + 0.3f, 73.4f, 0.8f);
            Storage("Boxes (the wall across, south)", px0, px1, 73.4f, 75f, 1.3f);
            Storage("Old wardrobe (attic, under a dust sheet)", px0, px1, 75f, 76.2f, 1.9f);
            Storage("Boxes (the wall across, by the sheets)", px0, px1, 76.2f, 77f, 1.4f);
            // (z 77..80: the sheets on their line: the way through)
            Storage("Boxes (the wall across, north)", px0, px1, 80f, 81.4f, 1.5f);
            Storage("Boxes (the wall across, north eave)", px0, px1, 81.4f, 82.8f, 1.1f);
            Storage("Boxes (the wall across, north eave, low)", px0, px1, 82.8f, HubZ1 - 0.3f, 0.75f);
            Storage("Old sofa (under a dust sheet)", 4.6f, 6.6f, 79.4f, 80.3f, 0.85f);
            Storage("Boxes (by the chimney: Christmas)", 6.2f, 7.4f, 76.6f, 77.6f, 1.2f);
            Storage("Boxes under the eaves (south, west)", px1, -0.6f, HubZ0 + 0.3f, 73.4f, 0.8f);
            Storage("Boxes under the eaves (south, east)", 1.0f, 7.4f, HubZ0 + 0.3f, 73.4f, 0.8f);
            Storage("Boxes under the eaves (north, west)", -0.6f, DormerX0 - 0.2f, 83.0f, HubZ1 - 0.3f, 0.75f);
            Storage("Boxes under the eaves (north, east)", DormerX1 + 0.2f, HubX1, 83.0f, HubZ1 - 0.3f, 0.75f);
            Spot("The attic", HideKind.Closet, V((HubX0 + HubX1) * 0.5f, AtticFloor + 1.6f, (HubZ0 + HubZ1) * 0.5f), V(HubX1 - HubX0, 3.2f, HubZ1 - HubZ0),
                V(3.6f, F, 83.3f));

            // Light and shade: the sunny living room and kitchen, the dim hall, the bedrooms, the dark attic.
            Light("Kitchen and dining", V(-2.2f, F + 1.5f, 75.1f), V(9.6f, 3.2f, 6.2f), 0.9f);
            Light("Living room", V(5.4f, F + 1.5f, 74.6f), V(5.2f, 3.2f, 7.2f), 1f);
            Light("Hall", V(1.3f, F + 1.5f, 79.4f), V(13.4f, 3.2f, 2.4f), 0.45f);
            Light("Pantry", V(-6.3f, F + 1.5f, 79.4f), V(1.4f, 3.2f, 2.4f), 0.2f);
            Light("Parents' room", V(-4.2f, F + 1.5f, 82.4f), V(5.6f, 3.2f, 3.2f), 0.5f);
            Light("Bath", V(0.1f, F + 1.5f, 82.4f), V(2.6f, 3.2f, 3.2f), 0.6f);
            Light("Theo's room", V(5.5f, F + 1.5f, 82.4f), V(5f, 3.2f, 3.2f), 0.55f);
            Light("Attic", V((HubX0 + HubX1) * 0.5f, AtticFloor + 1.6f, 78f), V(HubX1 - HubX0, 3.2f, HubZ1 - HubZ0), 0.3f);

            // Theo's walking graph: points in every room and at every doorway, on the main floor.
            foreach (var p in new[]
            {
                // kitchen and dining
                V(-5.4f, F, 73.6f), V(-1f, F, 73.4f), V(1.6f, F, 74.4f), V(-0.6f, F, 77.4f), V(-5.3f, F, 77.6f), V(-2.5f, F, 77.5f),
                // living room
                V(5.2f, F, 73.4f), V(6.5f, F, 75.1f), V(5.4f, F, 77.1f), V(3.8f, F, 77f), V(4f, F, 73.1f),
                // the hall and its doorways
                V(-2.5f, F, 79.0f), V(5.5f, F, 79.1f), V(-4.7f, F, 79.4f), V(-0.7f, F, 79.2f), V(2.2f, F, 79.4f), V(4.5f, F, 79.6f), V(6.5f, F, 79.1f),
                // the parents' room and the bath
                V(-4.5f, F, 81.5f), V(-3.0f, F, 82.4f), V(-6.5f, F, 81.4f), V(0.1f, F, 82.2f),
                // Theo's room
                V(4.5f, F, 81.6f), V(3.6f, F, 83.3f), V(6.4f, F, 82.5f), V(4.6f, F, 83.1f),
            }) nodes.Add(p);

            // The memories (the hide-and-seek's collectibles). Names are plain descriptions; what each shows is the creator's.
            // Each has the place she stands to take it (one is on top of Theo's bookcase: climb his desk).
            Memory("memory_height_marks", "The height marks on the door frame", V(-2.08f, F + 1.2f, HallZ0 + 0.1f), V(-2.5f, F, 77.45f));
            Memory("memory_calendar", "The fridge calendar", V(-6.1f, F + 1.4f, 77.1f), V(-5.3f, F, 77.1f));
            Memory("memory_fourth_chair", "The fourth chair", V(-1.65f, F + 0.9f, 75.7f), V(-0.9f, F, 75.7f));
            Memory("memory_drawing", "Theo's drawing", V(7.72f, F + 2.15f, 81.95f), V(7.6f, F + 0.75f, 82.65f));
            Memory("memory_photos", "The photo wall", V(-2.0f, F + 1.6f, HallZ1 - 0.15f), V(-2.0f, F, 79.95f));

            // The rooms (for the builder's walls, and the plan).
            Room("Kitchen and dining", HubX0, 2.6f, HubZ0, HallZ0, F, 2.8f);
            Room("Living room", 2.8f, HubX1, HubZ0, HallZ0, F, 3.0f);
            Room("Bay window", BayX0, BayX1, BayZ0, HubZ0, F, 2.5f);
            Room("Pantry", HubX0, -5.6f, HallZ0 + 0.2f, HallZ1, F, 2.3f);
            Room("Hall", -5.4f, HubX1, HallZ0 + 0.2f, HallZ1, F, 2.4f);
            Room("Parents' room", HubX0, -1.4f, HallZ1 + 0.2f, HubZ1, F, 2.7f);
            Room("Bath", -1.2f, 1.4f, HallZ1 + 0.2f, HubZ1, F, 2.4f);
            Room("Attic stairs", StairX0, StairX1, StairZ0, HubZ1, F, 2.6f);
            Room("Theo's room", 3f, HubX1, HallZ1 + 0.2f, HubZ1, F, 2.6f);
            Room("Attic", HubX0, HubX1, HubZ0, HubZ1, AtticFloor, RidgeHeight);

            // Somewhere to stand in the open in each main room (tests and the PlayMode run use them).
            openFloor.Add(V(6.5f, F, 75.1f));
            openFloor.Add(V(-3.0f, F, 82.4f));
            openFloor.Add(V(4.5f, F, 81.6f));

            // ===== 3. The house tips: the hallway's floor is a wall, the doorframes walls to kick between, the stairs a slide.
            const float t0 = 0f, t1 = -1f, t2 = -2f, t3 = -5f;
            Section(186f, "The house tips");
            Lane(186f, 0f);
            Add("The tipped kitchen (its wall is the floor)", LabPieceKind.Floor, HouseLook.TippedWall, V(-4.5f, t0 - 2f, 186f), V(4.5f, t0, 214f));
            escapeStartS = 190f;
            escapeCheckpoint = Checkpoint("The house tips", 190f, t0, ChapterTips);
            chaseStartS = 190f;
            Lane(192f, 0f); Lane(202f, 3.6f);
            Cue(LabCueKind.Jump, 213.2f);
            RunWall("The hallway's floor (now a wall)", HouseLook.TippedFloor, 4.5f, 217f, 231f, t0 + 6f);
            Cue(LabCueKind.WallJump, 226.5f);
            Beat(CourseBeatKind.Wallrun, "The hallway's floor", 213.2f, 14f, 6f);
            Lane(228f, 3.6f); Lane(234f, 0f);
            Add("The tipped hallway (landing)", LabPieceKind.Floor, HouseLook.TippedWall, V(-4.5f, t1 - 2f, 232f), V(4.5f, t1, 252f));
            Checkpoint("The doorframes", 236f, t1);
            const float e = 252f;
            Lane(e - 8f, -1.95f);
            RunWall("Doorframe (left)", HouseLook.TippedFloor, -2.5f, e + 1f, e + 13f, t1 + 6f);
            RunWall("Doorframe (right)", HouseLook.TippedFloor, 2.5f, e + 7f, e + 25f, t1 + 6f);
            Cue(LabCueKind.Jump, e - 0.8f);
            Cue(LabCueKind.WallJump, e + 1.6f);
            Cue(LabCueKind.WallJump, e + 20.5f);
            Cue(LabCueKind.DoubleJumpAtApex, e + 21f);
            Lane(e + 6f, -1.95f); Lane(e + 10f, 2.1f); Lane(e + 22f, 2.1f); Lane(e + 28f, 0f);
            Beat(CourseBeatKind.WallToWall, "The doorframes", e - 0.8f, 5f);
            Beat(CourseBeatKind.Wallrun, "The right doorframe", e + 1.6f, 18f, 6f);
            Add("The landing (on its side)", LabPieceKind.Floor, HouseLook.TippedWall, V(-4.5f, t2 - 2f, 278f), V(4.5f, t2, 292f));
            Checkpoint("The stairs", 282f, t2);
            Ramp("The staircase (now a slide)", HouseLook.Stairs, -4.5f, 4.5f, 292f, 308f, t2, t3);
            Cue(LabCueKind.Crouch, 290f, 310f);
            Beat(CourseBeatKind.SlideRamp, "The staircase", 292f, 16f, t2 - t3);
            Add("The entrance hall", LabPieceKind.Floor, HouseLook.Porch, V(-6f, t3 - 2f, 308f), V(6f, t3, 319f));
            chaseEndS = 316f;

            // ===== 4. The front door, into the rain: the umbrella beside it; the rain path beyond.
            Section(312f, "The front door");
            frontDoorCheckpoint = Checkpoint("The front door", 312f, t3, ChapterFrontDoor);
            Add("Front door wall (left)", LabPieceKind.Wall, HouseLook.Facade, V(-10f, t3, 318.5f), V(-1.5f, t3 + 6f, 319f));
            Add("Front door wall (right)", LabPieceKind.Wall, HouseLook.Facade, V(1.5f, t3, 318.5f), V(10f, t3 + 6f, 319f));
            Add("Front door lintel (the way out)", LabPieceKind.Wall, HouseLook.Facade, V(-1.5f, t3 + 2.6f, 318.5f), V(1.5f, t3 + 6f, 319f));
            Section(318.75f, "The rain");
            Add("The front path (rain)", LabPieceKind.Floor, HouseLook.RainPath, V(-4f, t3 - 2f, 319f), V(4f, t3, 362f));
            rain = new DreamRainSpot
            {
                umbrella = V(2.4f, t3 + 0.55f, 317.6f),
                umbrellaRadius = 0.6f,
                rainStartS = 318.75f,
                despairS = 318.75f + DreamHouseStory.DespairDepth,
                rainEndS = 352f,
                resetS = 300f,
                floorY = t3,
            };

            c.pursuer = new Pursuer { targetGap = 12f, k = 0.6f, minSpeed = 6f, maxSpeed = 11.5f };
            c.route = new PathLine(new[] { new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 380f) });
            c.finishS = rain.rainEndS;
            c.chaseStartS = chaseStartS;
            c.chaseEndS = chaseEndS;
            beats.Sort((a, b) => a.s.CompareTo(b.s));
        }
    }
}
