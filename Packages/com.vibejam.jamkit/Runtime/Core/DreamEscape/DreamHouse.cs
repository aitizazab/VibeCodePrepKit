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
    }

    /// <summary>A memory to find (the hide-and-seek's collectibles).</summary>
    public sealed class HouseMemory
    {
        public string id, name;
        public Vector3 position;
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
    /// <item><b>The ground floor</b> (pancakes and hide-and-seek): kitchen and dining room, living room, hallway, Theo's
    /// room, the study; hiding spots (the pantry, the curtains, the coats, Theo's wardrobe), light and shade, Theo's
    /// walking graph, five memories to find.</item>
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
        /// <summary>The ground floor's rooms: x and z bounds.</summary>
        public const float HubX0 = -12f, HubX1 = 12f, HubZ0 = 72f, HubZ1 = 100f;
        public const float VoidBottom = -24f;

        public readonly LabCourse course = new LabCourse();
        public readonly List<HouseLook> looks = new List<HouseLook>();
        public readonly List<CourseBeat> beats = new List<CourseBeat>();
        // The stealth part (the ground floor).
        public readonly List<HidingSpot> spots = new List<HidingSpot>();
        public readonly List<Vector3> nodes = new List<Vector3>();
        public readonly List<HouseLight> lights = new List<HouseLight>();
        public readonly List<HouseMemory> memories = new List<HouseMemory>();
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
                if (!p.solid || p.Max.Z < HubZ0 - 1f || p.Min.Z > HubZ1 + 1f || looks[i] == HouseLook.Ceiling) continue;
                if (looks[i] == HouseLook.Floor) continue; // the floor never blocks (it's under the eyes)
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

        /// <summary>An interior wall of the ground floor (3 m, to the ceiling).</summary>
        void Wall(string name, float x0, float x1, float z0, float z1) =>
            Add(name, LabPieceKind.Wall, HouseLook.Wall, new Vector3(x0, Ground, z0), new Vector3(x1, Ground + 3f, z1));

        void Furniture(string name, HouseLook look, float x0, float x1, float z0, float z1, float height) =>
            Add(name, LabPieceKind.Ledge, look, new Vector3(x0, Ground, z0), new Vector3(x1, Ground + height, z1));

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

        void Memory(string id, string name, Vector3 at) => memories.Add(new HouseMemory { id = id, name = name, position = at });

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
            Add("House front (left)", LabPieceKind.Wall, HouseLook.Facade, V(-14f, 0f, 0f), V(-1.5f, 7f, 0.5f));
            Add("House front (right)", LabPieceKind.Wall, HouseLook.Facade, V(1.5f, 0f, 0f), V(14f, 7f, 0.5f));
            Add("Front door lintel", LabPieceKind.Wall, HouseLook.Facade, V(-1.5f, 2.6f, 0f), V(1.5f, 7f, 0.5f));
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

            // ===== 2. The ground floor: pancakes, then hide-and-seek with Theo, five memories to find.
            Section(72f, "The ground floor");
            Add("Ground floor", LabPieceKind.Floor, HouseLook.Floor, V(HubX0 - 0.5f, F - 2f, HubZ0 - 0.5f), V(HubX1 + 0.5f, F, HubZ1 + 0.5f));
            Add("Ceiling", LabPieceKind.Floor, HouseLook.Ceiling, V(HubX0 - 0.5f, F + 3f, HubZ0 - 0.5f), V(HubX1 + 0.5f, F + 3.2f, HubZ1 + 0.5f));
            kitchenStart = V(0f, F, 74f);
            hubCheckpoint = Checkpoint("The kitchen", 74f, F, ChapterHideAndSeek, 0f);
            // Outer walls (the south one open where the slide comes in).
            Wall("Outer wall (west)", HubX0 - 0.5f, HubX0, HubZ0 - 0.5f, HubZ1 + 0.5f);
            Wall("Outer wall (east)", HubX1, HubX1 + 0.5f, HubZ0 - 0.5f, HubZ1 + 0.5f);
            Wall("Outer wall (north)", HubX0 - 0.5f, HubX1 + 0.5f, HubZ1, HubZ1 + 0.5f);
            Wall("Outer wall (south, left)", HubX0 - 0.5f, -2f, HubZ0 - 0.5f, HubZ0);
            Wall("Outer wall (south, right)", 2f, HubX1 + 0.5f, HubZ0 - 0.5f, HubZ0);
            // The front rooms | the hallway (doorways at x -8..-6.4 and 5..6.6); kitchen | living (a half wall).
            Wall("Wall (front rooms, west)", HubX0, -8f, 86f, 86.2f);
            Wall("Wall (front rooms, middle)", -6.4f, 5f, 86f, 86.2f);
            Wall("Wall (front rooms, east)", 6.6f, HubX1, 86f, 86.2f);
            Wall("Wall (kitchen | living)", 1f, 1.2f, 80f, 86f);
            // The hallway | the back rooms (doorways at x -6..-4.4 to Theo's room, 4.4..6 to the study); Theo's | the study.
            Wall("Wall (back rooms, west)", HubX0, -6f, 89f, 89.2f);
            Wall("Wall (back rooms, middle)", -4.4f, 4.4f, 89f, 89.2f);
            Wall("Wall (back rooms, east)", 6f, HubX1, 89f, 89.2f);
            Wall("Wall (Theo's room | study)", -0.1f, 0.1f, 89.2f, HubZ1);
            // The kitchen: the counter and stove, the fridge (the calendar), the pantry (a closet), the table set for four.
            Furniture("Counter and stove", HouseLook.Counter, HubX0, -11.2f, 72.5f, 80f, 0.95f);
            Furniture("Fridge", HouseLook.Fridge, HubX0, -11.1f, 81f, 82.4f, 2f);
            Furniture("Pantry side (south)", HouseLook.Closet, HubX0, -10.4f, 83f, 83.2f, 2.3f);
            Furniture("Pantry side (north)", HouseLook.Closet, HubX0, -10.4f, 85.2f, 85.4f, 2.3f);
            Add("Pantry roof", LabPieceKind.Ledge, HouseLook.Closet, V(HubX0, F + 2.3f, 83f), V(-10.4f, F + 2.5f, 85.4f));
            Add("Pantry curtain", LabPieceKind.Wall, HouseLook.Curtain, V(-10.45f, F, 83.2f), V(-10.4f, F + 2.2f, 85.2f), solid: false);
            Spot("The pantry", HideKind.Closet, V(-11.2f, F + 1f, 84.2f), V(1.6f, 2f, 1.9f), V(-9.4f, F, 84.2f));
            Furniture("Dining table", HouseLook.Table, -8f, -4f, 76f, 79f, 0.78f);
            Furniture("Chair (Lena's)", HouseLook.Chair, -9.3f, -8.7f, 77.2f, 77.8f, 0.5f);
            Furniture("Chair (Nadia's: the fourth chair)", HouseLook.Chair, -3.3f, -2.7f, 77.2f, 77.8f, 0.5f);
            Furniture("Chair (Theo's)", HouseLook.Chair, -6.3f, -5.7f, 74.9f, 75.5f, 0.5f);
            Furniture("Chair (Elias's)", HouseLook.Chair, -6.3f, -5.7f, 79.5f, 80.1f, 0.5f);
            table = V(-6f, F, 77.5f);
            lenaAt = V(-10.5f, F, 75.5f);
            theoStart = V(-6f, F, 81f);
            theoYaw = 180f;
            theoCountYaw = 180f; // counting at the table, eyes on the kitchen's south wall
            // The living room: the sofa (low cover), the curtains (a bush-like spot).
            Furniture("Sofa", HouseLook.Sofa, 4f, 9f, 77f, 78f, 0.9f);
            Add("Curtains", LabPieceKind.Wall, HouseLook.Curtain, V(11.2f, F, 73f), V(12f, F + 2.6f, 77f), solid: false);
            Spot("The curtains", HideKind.Bush, V(11.4f, F + 1.2f, 75f), V(1.2f, 2.4f, 4f), V(9.6f, F, 75f));
            // The hallway: the coats at its east end (a bush-like spot).
            Add("Coats", LabPieceKind.Wall, HouseLook.Curtain, V(10.6f, F + 0.4f, 86.4f), V(12f, F + 2.4f, 88.8f), solid: false);
            Spot("The coats", HideKind.Bush, V(11.1f, F + 1f, 87.6f), V(1.8f, 2f, 2.4f), V(9f, F, 87.6f));
            // Theo's room: the bed, the desk (his drawings), the wardrobe (a closet).
            Furniture("Theo's bed", HouseLook.Bed, -11.6f, -9.6f, 95f, 99f, 0.55f);
            Furniture("Theo's desk", HouseLook.Desk, -8f, -6f, 89.4f, 90.4f, 0.75f);
            Furniture("Wardrobe side (west)", HouseLook.Closet, -4f, -3.8f, 98.2f, HubZ1, 2.3f);
            Furniture("Wardrobe side (east)", HouseLook.Closet, -2.6f, -2.4f, 98.2f, HubZ1, 2.3f);
            Add("Wardrobe roof", LabPieceKind.Ledge, HouseLook.Closet, V(-4f, F + 2.3f, 98.2f), V(-2.4f, F + 2.5f, HubZ1));
            Spot("Theo's wardrobe", HideKind.Closet, V(-3.2f, F + 1f, 99.1f), V(1.2f, 2f, 1.6f), V(-3.2f, F, 97f));
            // The study: a desk; the photo wall on its north wall.
            Furniture("Study desk", HouseLook.Desk, 6f, 9f, 95f, 96f, 0.75f);

            // Light and shade: the sunny living room, the kitchen, the dim hallway, Theo's room, the dark study.
            Light("Kitchen", V(-5.5f, F + 1.5f, 79f), V(13f, 3.2f, 14f), 0.9f);
            Light("Living room", V(6.5f, F + 1.5f, 79f), V(11f, 3.2f, 14f), 1f);
            Light("Hallway", V(0f, F + 1.5f, 87.6f), V(24f, 3.2f, 3f), 0.45f);
            Light("Theo's room", V(-6f, F + 1.5f, 94.6f), V(12f, 3.2f, 11f), 0.55f);
            Light("Study", V(6f, F + 1.5f, 94.6f), V(12f, 3.2f, 11f), 0.35f);

            // Theo's walking graph: points in every room, every doorway, the hallway.
            foreach (var p in new[]
            {
                V(0f, F, 74.5f), V(-2.5f, F, 74f), V(-9.5f, F, 74f), V(-9.5f, F, 81.5f), V(-3f, F, 82f), V(-7.2f, F, 84.4f),
                V(4f, F, 74f), V(9.4f, F, 75.5f), V(3f, F, 81f), V(8f, F, 82.5f), V(5.8f, F, 84.6f),
                V(-7.2f, F, 86.1f), V(5.8f, F, 86.1f),
                V(-9.5f, F, 87.6f), V(-5.2f, F, 87.6f), V(0f, F, 87.6f), V(5.2f, F, 87.6f), V(8.8f, F, 87.6f),
                V(-5.2f, F, 89.1f), V(5.2f, F, 89.1f),
                V(-5.2f, F, 91f), V(-8.6f, F, 92.4f), V(-7f, F, 97f), V(-3.2f, F, 96.8f), V(-1.5f, F, 93f),
                V(5.2f, F, 91f), V(2f, F, 93f), V(9.6f, F, 93f), V(3f, F, 98.4f), V(9f, F, 98.4f),
            }) nodes.Add(p);

            // The memories (the hide-and-seek's collectibles). Names are plain descriptions; what each shows is the creator's.
            Memory("memory_height_marks", "The height marks on the door frame", V(-5.2f, F + 1.2f, 89.1f));
            Memory("memory_calendar", "The fridge calendar", V(-10.9f, F + 1.4f, 81.7f));
            Memory("memory_fourth_chair", "The fourth chair", V(-3f, F + 0.9f, 77.5f));
            Memory("memory_drawing", "Theo's drawing", V(-7f, F + 1f, 90.7f));
            Memory("memory_photos", "The photo wall", V(7.5f, F + 1.6f, 99.6f));

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
