using System;
using System.Collections.Generic;
using System.Numerics;
using JamKit.Movement;

namespace JamKit.DreamEscape
{
    /// <summary>What a piece looks like in the dream (the movement core only sees boxes; this is for the builder).</summary>
    public enum DreamLook
    {
        Street, SunkenLawn, House, Facade, GardenWall, Fence, SideFence, Car, GarageDoor, GarageShell,
        Porch, PorchRoof, Roof, RoofSlope, ExitPorch, Barbecue, PicnicTable, RoseBush, Tree, Mailbox, Flat,
    }

    /// <summary>How a collapsing piece goes: drops away, folds flat like a felled fence, or peels off like paper.</summary>
    public enum DreamCollapseStyle { Drop, FoldFlat, Peel }

    /// <summary>The dream's cast as cut-out billboards (they exist only in the dream).</summary>
    public enum DreamCast { Walt, NeighbourRoses, NeighbourGrill, Dog }

    public sealed class DreamBillboard
    {
        public string name;
        public DreamCast who;
        /// <summary>Feet position.</summary>
        public Vector3 position;
        /// <summary>Height of the cut-out in metres.</summary>
        public float height;
    }


    /// <summary>A collapse's wave: pieces in it go one after another, from <see cref="startOffset"/> metres
    /// relative to the trigger point, travelling forward at <see cref="speed"/> m/s.</summary>
    public sealed class DreamWave
    {
        public DreamCollapseStyle style;
        public float startOffset;
        public float speed = 18f;
    }

    /// <summary>
    /// The first escape of Visiting Hours: the dream version of Walt's street, chased by the dream's
    /// immune system (the static). Pure data on top of the Movement Lab's <see cref="LabCourse"/> (pieces,
    /// route, lane, cues, checkpoints, collapses, pursuer), so the same <see cref="CourseAutopilot"/> runs
    /// it in the dotnet tests' fake world and on Unity's CharacterController. Parallel to the course's
    /// pieces: <see cref="looks"/>, <see cref="tints"/> and <see cref="collapseStyles"/>. The route runs along
    /// +Z (s = z) down the street; the lane moves across it. Sizes follow docs/RESEARCH-MOVEMENT.md 4.3 and
    /// the Movement Lab's proven offsets (docs/DREAM-ESCAPE.md has the route with every distance).
    /// </summary>
    public sealed class DreamEscapeCourse
    {
        public const string ChapterStart = "Escape start";
        public const string ChapterRooftops = "Rooftops";
        public const string ChapterFinal = "Final run";

        public const float StreetHalfWidth = 30f;
        public const float FaceX = 13f;          // house fronts stand at x = +-13
        public const float FenceHeight = 0.85f;
        /// <summary>A car roof's height above its street.</summary>
        public const float CarRoof = 1.05f;
        public const float VoidBottom = -20f;

        /// <summary>The first rooftops' height (the porch climb's top).</summary>
        public float RoofLevel;
        /// <summary>The lit porch at the end: a metre below the last roof, so the last jump lands down into the light.</summary>
        public float ExitTop;

        public readonly LabCourse course = new LabCourse();
        public readonly List<DreamLook> looks = new List<DreamLook>();
        public readonly List<int> tints = new List<int>();
        public readonly List<DreamCollapseStyle> collapseStyles = new List<DreamCollapseStyle>();
        public readonly List<DreamWave> waves = new List<DreamWave>();
        public readonly List<DreamBillboard> billboards = new List<DreamBillboard>();
        public readonly List<CourseBeat> beats = new List<CourseBeat>();
        /// <summary>The safe light at the end (the exit), at floor level.</summary>
        public Vector3 exitLight;
        /// <summary>The front door: the doorway in the house front past the light (rain from here on).</summary>
        public float rainStartS;
        /// <summary>Going on past this into the rain without the umbrella is the Despair choice.</summary>
        public float despairS;
        /// <summary>The end of the rain path: she leaves the dream here.</summary>
        public float rainEndS;
        /// <summary>The closed umbrella leaning by the front door (its middle), and its size for the dream tool's aim.</summary>
        public Vector3 umbrella;
        public float umbrellaRadius = 0.6f;
        /// <summary>Placed before this (a respawn, a chapter, a new run) and the umbrella is back by the door.</summary>
        public float umbrellaResetS;

        /// <summary>The front door's umbrella and rain as a <see cref="DreamRainSpot"/> (a new copy each time).</summary>
        public DreamRainSpot RainSpot => new DreamRainSpot { umbrella = umbrella, umbrellaRadius = umbrellaRadius, rainStartS = rainStartS, despairS = despairS, rainEndS = rainEndS, resetS = umbrellaResetS, floorY = ExitTop };
        /// <summary>Seconds a player may stand at the start before the static moves anyway.</summary>
        public float wakeAfter = 2.5f;
        /// <summary>After this many catches or falls at one checkpoint, the static quietly gives more room.</summary>
        public int easeAfterFails = 2;
        public float easeGap = 4f;

        public static DreamEscapeCourse Build()
        {
            var d = new DreamEscapeCourse();
            d.Make();
            return d;
        }

        // ---------- Queries ----------

        public int PieceCount => course.pieces.Count;

        /// <summary>The seconds after a collapse fires before piece <paramref name="index"/> goes (its wave reaches it).</summary>
        public float CollapseDelay(int index)
        {
            var p = course.pieces[index];
            if (p.collapse < 0 || p.collapse >= waves.Count) return 0f;
            var w = waves[p.collapse];
            float origin = course.collapses[p.collapse].triggerS + w.startOffset;
            float d = p.Min.Z - origin;
            return d > 0f && w.speed > 0f ? d / w.speed : 0f;
        }

        /// <summary>The top of the highest walkable piece under (x, z) at or below <paramref name="maxY"/>; false over the void.</summary>
        public bool FloorTopAt(float x, float z, float maxY, out float top)
        {
            top = float.MinValue;
            bool found = false;
            for (int i = 0; i < course.pieces.Count; i++)
            {
                var p = course.pieces[i];
                if (!p.solid || !IsWalkable(looks[i])) continue;
                if (x < p.Min.X || x > p.Max.X || z < p.Min.Z || z > p.Max.Z) continue;
                float t = p.kind == LabPieceKind.Ramp ? RampTop(p, z) : p.Max.Y;
                if (t > maxY || t <= top) continue;
                top = t;
                found = true;
            }
            return found;
        }

        public static float RampTop(LabPiece p, float z)
        {
            float t = (z - p.Min.Z) / Math.Max(1e-4f, p.Max.Z - p.Min.Z);
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            return p.rampTopStart + (p.rampTopEnd - p.rampTopStart) * t;
        }

        public static bool IsWalkable(DreamLook look) =>
            look == DreamLook.Street || look == DreamLook.SunkenLawn || look == DreamLook.Car || look == DreamLook.Porch
            || look == DreamLook.PorchRoof || look == DreamLook.Roof || look == DreamLook.RoofSlope || look == DreamLook.ExitPorch;

        /// <summary>Decoration: solid or not, it must stay off the route's lane.</summary>
        public static bool IsDecor(DreamLook look) =>
            look == DreamLook.House || look == DreamLook.Barbecue || look == DreamLook.PicnicTable || look == DreamLook.RoseBush
            || look == DreamLook.Tree || look == DreamLook.Mailbox || look == DreamLook.Flat || look == DreamLook.SideFence;

        // ---------- Reach (docs/RESEARCH-MOVEMENT.md 4.3; shared with the Movement Lab's Gauntlet) ----------

        public static float SingleJumpReach(MoveDef def, float speed) => CourseReach.SingleJumpReach(def, speed);
        public static float DoubleJumpReach(MoveDef def, float speed) => CourseReach.DoubleJumpReach(def, speed);
        public const float SlideMargin = CourseReach.SlideMargin;
        public static float SlideGapNeeded(MoveDef def, float skin, float crouchStepOffset) => CourseReach.SlideGapNeeded(def, skin, crouchStepOffset);

        /// <summary>Every beat that doesn't fit the movement's measured reach with a margin, in words (empty = all good).
        /// <paramref name="skin"/> and <paramref name="crouchStepOffset"/> are the Unity controller's (MovementController
        /// skinWidth and crouchStepOffset).</summary>
        public List<string> CheckReach(MoveDef def, float skin = 0.04f, float crouchStepOffset = 0f) => CourseReach.Check(beats, def, skin, crouchStepOffset);

        // ---------- Building ----------

        string _section = "";
        int _tint;

        void Section(float s, string name)
        {
            _section = name;
            course.sections.Add(new KeyValuePair<float, string>(s, name));
        }

        LabPiece Add(string name, LabPieceKind kind, DreamLook look, Vector3 min, Vector3 max, bool solid = true, bool runnable = false, int tint = -1)
        {
            var p = new LabPiece { name = name, kind = kind, center = (min + max) * 0.5f, size = max - min, runnable = runnable, solid = solid, section = _section };
            course.pieces.Add(p);
            looks.Add(look);
            tints.Add(tint >= 0 ? tint : (_tint++ * 7 + 3) % 12);
            collapseStyles.Add(look == DreamLook.Fence || look == DreamLook.SideFence ? DreamCollapseStyle.FoldFlat
                : look == DreamLook.Facade || look == DreamLook.Flat ? DreamCollapseStyle.Peel : DreamCollapseStyle.Drop);
            return p;
        }

        LabPiece Floor(string name, float z0, float z1, float top, DreamLook look = DreamLook.Street, float x0 = -StreetHalfWidth, float x1 = StreetHalfWidth) =>
            Add(name, LabPieceKind.Floor, look, new Vector3(x0, top - 2f, z0), new Vector3(x1, top, z1));

        /// <summary>A house standing on a side of the street (side -1 left, +1 right), its front at x = side x 13.</summary>
        LabPiece House(string name, int side, float z0, float z1, float bottom, float top, float depth = 11f, float front = FaceX)
        {
            float a = side * front, b = side * (front + depth);
            return Add(name, LabPieceKind.Wall, DreamLook.House, new Vector3(Math.Min(a, b), bottom, z0), new Vector3(Math.Max(a, b), top, z1));
        }

        /// <summary>A runnable wall, its face at x = <paramref name="faceX"/> (the wall is on the right when faceX &gt; lane).</summary>
        LabPiece RunWall(string name, DreamLook look, float faceX, float z0, float z1, float bottom, float top, float thickness = 0.6f)
        {
            float x0 = faceX > 0f ? faceX : faceX - thickness;
            return Add(name, LabPieceKind.Wall, look, new Vector3(x0, bottom, z0), new Vector3(x0 + thickness, top, z1), true, true);
        }

        void Checkpoint(string name, float z, float floorTop, string chapter = "")
        {
            course.checkpoints.Add(new LabCheckpoint { name = name, chapter = chapter, position = new Vector3(course.LaneAt(z), floorTop + 0.02f, z), yaw = 0f, s = z });
        }

        void Cue(LabCueKind kind, float s, float sEnd = 0f, float x = 0f) => course.cues.Add(new LabCue { kind = kind, s = s, sEnd = sEnd, x = x });

        void Lane(float s, float x) => course.lane.Add(new Vector2(s, x));

        void Beat(CourseBeatKind kind, string name, float s, float size, float height = 0f) =>
            beats.Add(new CourseBeat { kind = kind, name = name, s = s, size = size, height = height });

        void Billboard(string name, DreamCast who, float x, float y, float z, float height) =>
            billboards.Add(new DreamBillboard { name = name, who = who, position = new Vector3(x, y, z), height = height });

        /// <summary>A picket fence across the street (a hop; a missed hop is a mantle).</summary>
        LabPiece Fence(string name, float z, float floor)
        {
            var p = Add(name, LabPieceKind.Ledge, DreamLook.Fence, new Vector3(-FaceX, floor, z - 0.08f), new Vector3(FaceX, floor + FenceHeight, z + 0.08f), tint: 0);
            Cue(LabCueKind.Jump, z - 3f);
            Beat(CourseBeatKind.FenceHop, name, z - 3f, 0.16f, FenceHeight);
            return p;
        }

        /// <summary>A parked toy car whose roof is a step of the hop chain (over the peeled road).</summary>
        LabPiece Car(string name, float x, float z0, float street, int tint)
        {
            const float length = 4.4f, width = 2.4f;
            return Add(name, LabPieceKind.Ledge, DreamLook.Car, new Vector3(x - width * 0.5f, street - 0.6f, z0), new Vector3(x + width * 0.5f, street + CarRoof, z0 + length), tint: tint);
        }

        /// <summary>A row of houses down one side of a stretch of street (or as pillars in the void when bottom &lt; 0).</summary>
        void HouseRow(int side, float z0, float z1, float bottom, string prefix)
        {
            float[] widths = { 10f, 9f, 11f, 8.5f };
            float[] heights = { 5.5f, 6.2f, 5f, 6.8f };
            int i = (int)Math.Abs(z0 * 0.37f) % widths.Length;
            float z = z0;
            while (z + 6f <= z1)
            {
                float w = Math.Min(widths[i % widths.Length], z1 - z);
                float top = (bottom < 0f ? 0f : bottom) + heights[i % heights.Length];
                House(prefix + " " + (i + 1), side, z, z + w, bottom, top);
                z += w + 4f;
                i++;
            }
        }

        void Make()
        {
            var c = course;
            c.killY = -14f;
            // Floor heights: the street sags a metre after each wallrun, so every wall jump lands down onto
            // the next stretch (a landing at the take-off's height comes up short and mantles, losing the speed).
            const float yA = 0f, yLawn = -2.6f, yB = -1f, yD = -2f;
            const float r = yD + 1f + 1.6f + 1.6f;  // the roofs: up the porch (1 m) and two 1.6 m mantles
            const float r4 = r - 1f;
            const float r5 = r4 - 1f;               // the alley drops you a metre too
            const float low = r5 - 3f;              // the low roof at the bottom of the roof slope
            const float exit = low - 1f;            // the lit porch, a metre below: the last jump lands down into the light
            RoofLevel = r;
            ExitTop = exit;

            // ===== A. Walt's driveway: the start. The static wakes behind, at Walt's house.
            Lane(-30f, -9f);
            Lane(0f, -9f);
            Section(-30f, "Walt's driveway");
            Floor("Street A", -30f, 57f, yA);
            Checkpoint("Walt's driveway", 0f, yA, ChapterStart);
            c.chaseStartS = 2f;
            House("Walt's house", -1, -18f, -5f, yA, 5.6f, 12f);
            Add("Walt's garage", LabPieceKind.Wall, DreamLook.House, new Vector3(-21f, yA, -4.5f), new Vector3(-FaceX, 3.6f, 3.2f), tint: 2);
            HouseRow(-1, 5f, 17f, yA, "Left house A");
            House("The rose house", -1, 20f, 28f, yA, 5.2f);
            HouseRow(-1, 32f, 41f, yA, "Left house B");
            HouseRow(-1, 44f, 52f, yA, "Left house C");
            HouseRow(1, -18f, 56f, yA, "Right house A");
            for (float z = -12f; z < 54f; z += 13f)
                Add("Mailbox", LabPieceKind.Sign, DreamLook.Mailbox, new Vector3(6.1f, yA, z), new Vector3(6.5f, 1.2f, z + 0.4f), false);
            Add("Tree A", LabPieceKind.Wall, DreamLook.Tree, new Vector3(9.5f, yA, 10f), new Vector3(10.3f, 5.5f, 10.8f));
            Add("Tree B", LabPieceKind.Wall, DreamLook.Tree, new Vector3(-5.2f, yA, -14f), new Vector3(-4.4f, 5f, -13.2f));
            Billboard("Walt", DreamCast.Walt, -12.2f, yA, 6.5f, 1.9f);

            // ===== B. The fences: three picket fences across the street to hop (a miss mantles over).
            // Collapse 0: once you're past the first, the static pushes the fences down in a wave that
            // overtakes you along the right-hand pavement.
            Section(12f, "The fences");
            var f1 = Fence("Fence 1", 18f, yA);
            Fence("Fence 2", 30f, yA);
            Fence("Fence 3", 42f, yA);
            c.collapses.Add(new LabCollapse { name = "The fences fold", triggerS = 25f });
            waves.Add(new DreamWave { style = DreamCollapseStyle.FoldFlat, startOffset = -13f, speed = 17f });
            f1.collapse = 0;
            for (float z = 2f; z < 56f; z += 3f)
            {
                var side = Add("Side fence", LabPieceKind.Sign, DreamLook.SideFence, new Vector3(5.6f, yA, z), new Vector3(5.75f, yA + 0.9f, z + 2.9f), false, tint: 0);
                side.collapse = 0;
            }
            Add("Rose bushes", LabPieceKind.Sign, DreamLook.RoseBush, new Vector3(-FaceX + 0.2f, yA, 21f), new Vector3(-FaceX + 0.9f, 0.8f, 27f), false);
            Billboard("Neighbour with roses", DreamCast.NeighbourRoses, -12f, yA, 24f, 1.7f);

            // ===== C. The missing lawn: the lawn has sunk away; run along the flat house front on the left.
            // A miss drops you into the sunken lawn and you mantle out at its far side (slow, not a fail).
            Section(48f, "The missing lawn");
            Lane(45f, -9f); Lane(53f, -12.1f);
            Floor("Sunken lawn", 57f, 75f, yLawn, DreamLook.SunkenLawn);
            RunWall("Flat house front", DreamLook.Facade, -FaceX, 60f, 74f, yLawn, yA + 6.5f);
            Cue(LabCueKind.Jump, 56.2f);
            Cue(LabCueKind.WallJump, 69.5f);
            Beat(CourseBeatKind.Wallrun, "The missing lawn's house front", 56.2f, 14f, 6.5f);
            Lane(71f, -12.1f); Lane(77f, -9f);
            HouseRow(1, 59f, 74f, yLawn, "Right house B");

            // ===== D. Car roofs: the road has peeled up; three parked toy cars are the way over the void.
            Section(75f, "Car roofs");
            Floor("Street B", 75f, 98f, yB);
            Lane(80f, -9f); Lane(92f, -4.3f);
            HouseRow(-1, 77f, 97f, yB, "Left house D");
            HouseRow(1, 77f, 97f, yB, "Right house D");
            const float cx = -4.3f;
            Car("Car 1", cx, 100f, yB, 1);
            Car("Car 2", cx, 107.4f, yB, 4);
            Car("Car 3", cx, 114.8f, yB, 8);
            Cue(LabCueKind.Jump, 96.4f);
            Cue(LabCueKind.Jump, 103.2f);
            Cue(LabCueKind.Jump, 110.6f);
            Cue(LabCueKind.Jump, 118f);
            Beat(CourseBeatKind.CarHop, "Onto car 1", 96.4f, 2f, CarRoof);
            Beat(CourseBeatKind.CarHop, "Car 1 to car 2", 103.2f, 3f, CarRoof);
            Beat(CourseBeatKind.CarHop, "Car 2 to car 3", 110.6f, 3f, CarRoof);
            Beat(CourseBeatKind.Gap, "Car 3 to the street", 118f, 3.8f);
            HouseRow(-1, 100f, 121f, VoidBottom, "Left pillar");
            HouseRow(1, 100f, 121f, VoidBottom, "Right pillar");
            Billboard("The dog", DreamCast.Dog, -8.2f, yB, 95.6f, 0.75f);

            // ===== E. The barbecue: across the street on the ground, past the neighbour at his grill.
            Section(122f, "The barbecue");
            Floor("Street C", 123f, 160f, yB);
            Lane(126f, -4.3f); Lane(156f, 7.6f);
            Checkpoint("The barbecue", 128f, yB);
            HouseRow(-1, 124f, 159f, yB, "Left house E");
            House("The grill house", 1, 128f, 140f, yB, yB + 5.8f);
            HouseRow(1, 144f, 159f, yB, "Right house E");
            Add("Barbecue", LabPieceKind.Wall, DreamLook.Barbecue, new Vector3(8.9f, yB, 142.2f), new Vector3(9.7f, yB + 1.0f, 142.9f));
            Add("Picnic table", LabPieceKind.Wall, DreamLook.PicnicTable, new Vector3(10.6f, yB, 134.5f), new Vector3(12.4f, yB + 0.75f, 137.5f));
            Billboard("Neighbour at the grill", DreamCast.NeighbourGrill, 10.6f, yB, 141f, 1.75f);

            // ===== E2. The garden wall: the next stretch is gone; run the tall garden wall on the right.
            Section(158f, "The garden wall");
            RunWall("Garden wall", DreamLook.GardenWall, 8.5f, 163f, 177f, VoidBottom, yB + 4.4f);
            Cue(LabCueKind.Jump, 159.2f);
            Cue(LabCueKind.WallJump, 172.5f);
            Beat(CourseBeatKind.Wallrun, "The garden wall", 159.2f, 14f, 4.4f);
            Lane(174f, 7.6f); Lane(180f, 4f);
            HouseRow(-1, 162f, 177f, VoidBottom, "Left pillar B");
            House("Behind the garden wall", 1, 163f, 177f, VoidBottom, yB + 6f);

            // ===== F. The garage: Walt's garage, grown across the whole street; its door is half open: slide.
            Section(178f, "The garage");
            Floor("Street D", 178f, 230f, yD);
            Lane(190f, 0f);
            HouseRow(-1, 180f, 199f, yD, "Left house F");
            HouseRow(1, 180f, 199f, yD, "Right house F");
            const float door = 206f, inside = 4.4f;
            Add("Garage door", LabPieceKind.Bar, DreamLook.GarageDoor, new Vector3(-14f, yD + 1.15f, door - 0.2f), new Vector3(14f, yD + inside, door + 0.2f), tint: 3);
            Add("Garage roof", LabPieceKind.Wall, DreamLook.GarageShell, new Vector3(-14.6f, yD + inside, door - 0.2f), new Vector3(14.6f, yD + inside + 0.6f, 222f), tint: 3);
            Add("Garage left wall", LabPieceKind.Wall, DreamLook.GarageShell, new Vector3(-14.6f, yD, door - 0.2f), new Vector3(-14f, yD + inside, 222f), tint: 3);
            Add("Garage right wall", LabPieceKind.Wall, DreamLook.GarageShell, new Vector3(14f, yD, door - 0.2f), new Vector3(14.6f, yD + inside, 222f), tint: 3);
            House("Beside the garage (left)", -1, 200f, 226f, yD, yD + 6.5f, 15.4f, 14.6f);
            House("Beside the garage (right)", 1, 200f, 226f, yD, yD + 6.5f, 15.4f, 14.6f);
            Cue(LabCueKind.Crouch, 200f, door + 2f);
            Beat(CourseBeatKind.Slide, "Under the garage door", 200f, 16f, 1.15f);

            // ===== G. The porch: the street curls up. Mantle onto the porch, its roof, the roofs.
            Section(225f, "The porch");
            Add("Porch", LabPieceKind.Ledge, DreamLook.Porch, new Vector3(-7f, VoidBottom, 230f), new Vector3(7f, yD + 1f, 236f));
            Add("Porch roof", LabPieceKind.Ledge, DreamLook.PorchRoof, new Vector3(-7f, VoidBottom, 236f), new Vector3(7f, yD + 2.6f, 242f));
            Beat(CourseBeatKind.Mantle, "Onto the porch", 230f, 0f, 1f);
            Beat(CourseBeatKind.Mantle, "Onto the porch roof", 236f, 0f, 1.6f);
            Beat(CourseBeatKind.Mantle, "Onto the roofs", 242f, 0f, 1.6f);

            // ===== H. Rooftops: the street has fallen away below; only the houses stand, as pillars.
            Section(240f, "Rooftops");
            Add("Roof 1", LabPieceKind.Ledge, DreamLook.Roof, new Vector3(-6f, VoidBottom, 242f), new Vector3(6f, r, 262f), tint: 5);
            Checkpoint("Rooftops", 246f, r, ChapterRooftops);
            Cue(LabCueKind.Jump, 261.2f);
            Beat(CourseBeatKind.Gap, "Roof 1 to roof 2", 261.2f, 5.5f);
            Add("Roof 2", LabPieceKind.Ledge, DreamLook.Roof, new Vector3(-6f, VoidBottom, 267.5f), new Vector3(6f, r, 282f), tint: 9);
            Cue(LabCueKind.Jump, 281.2f);
            Cue(LabCueKind.DoubleJumpAtApex, 282f);
            Beat(CourseBeatKind.DoubleGap, "Roof 2 to roof 3", 281.2f, 8.5f);
            Add("Roof 3", LabPieceKind.Ledge, DreamLook.Roof, new Vector3(-6f, VoidBottom, 290.5f), new Vector3(6f, r, 306f), tint: 2);
            Billboard("Neighbour with roses (roof)", DreamCast.NeighbourRoses, -4.8f, r, 255f, 1.7f);

            // The peeling street: run the flat house front on the right while the fronts on the left peel
            // off like paper in a wave (collapse 1), showing nothing behind them.
            Section(300f, "The peeling street");
            Lane(292f, 0f); Lane(301f, 3.6f);
            RunWall("Paper house front", DreamLook.Facade, 4.5f, 309f, 323f, VoidBottom, r + 6f);
            Cue(LabCueKind.Jump, 305.2f);
            Cue(LabCueKind.WallJump, 318.5f);
            Beat(CourseBeatKind.Wallrun, "The paper house front", 305.2f, 14f, 6f);
            Lane(320f, 3.6f); Lane(326f, 0f);
            Add("Roof 4", LabPieceKind.Ledge, DreamLook.Roof, new Vector3(-6f, VoidBottom, 324f), new Vector3(6f, r4, 344f), tint: 7);
            c.collapses.Add(new LabCollapse { name = "The house fronts peel away", triggerS = 303f });
            waves.Add(new DreamWave { style = DreamCollapseStyle.Peel, startOffset = -10f, speed = 16f });
            float[] flatTops = { 3.3f, 4.8f, 3.8f, 5.3f, 2.8f };
            for (int i = 0; i < 5; i++)
            {
                float z0 = 293f + i * 12f;
                var flat = Add("Paper house " + (i + 1), LabPieceKind.Sign, DreamLook.Flat, new Vector3(-12.4f, r - 8f, z0), new Vector3(-12f, r + flatTops[i], z0 + 10.5f), false);
                flat.collapse = 1;
            }
            Billboard("The dog (roof)", DreamCast.Dog, 4.6f, r4, 338f, 0.75f);

            // The alley: a left wall and a right wall over the void; kick from one to the other.
            Section(340f, "The alley");
            const float e = 344f;
            Lane(e - 16f, 0f); Lane(e - 8f, -1.95f);
            RunWall("Alley wall (left)", DreamLook.Facade, -2.5f, e + 1f, e + 13f, VoidBottom, r4 + 6f);
            RunWall("Alley wall (right)", DreamLook.Facade, 2.5f, e + 7f, e + 25f, VoidBottom, r4 + 6f);
            Cue(LabCueKind.Jump, e - 0.8f);
            Cue(LabCueKind.WallJump, e + 1.6f);
            Cue(LabCueKind.WallJump, e + 20.5f);
            Cue(LabCueKind.DoubleJumpAtApex, e + 21f);
            Lane(e + 6f, -1.95f); Lane(e + 10f, 2.1f); Lane(e + 22f, 2.1f); Lane(e + 28f, 0f);
            Beat(CourseBeatKind.WallToWall, "The alley", e - 0.8f, 5f);
            Beat(CourseBeatKind.Wallrun, "The alley's right wall", e + 1.6f, 18f, 6f);

            // ===== I. Final run: slide down a roof, then the last wall, and jump off it down into the light.
            Add("Roof 5", LabPieceKind.Ledge, DreamLook.Roof, new Vector3(-6f, VoidBottom, 369.2f), new Vector3(6f, r5, 388f), tint: 1);
            Section(372f, "Final run");
            Checkpoint("Final run", 374f, r5, ChapterFinal);
            var slope = Add("Roof slope", LabPieceKind.Ramp, DreamLook.RoofSlope, new Vector3(-6f, low - 2f, 388f), new Vector3(6f, r5, 404f), tint: 1);
            slope.rampTopStart = r5;
            slope.rampTopEnd = low;
            Cue(LabCueKind.Crouch, 386f, 404f);
            Beat(CourseBeatKind.SlideRamp, "Down the roof", 388f, 16f, r5 - low);
            Add("Garage roof (low)", LabPieceKind.Ledge, DreamLook.Roof, new Vector3(-6f, VoidBottom, 404f), new Vector3(6f, low, 418f), tint: 3);
            Lane(396f, 0f); Lane(408f, -3.6f);
            RunWall("The last wall", DreamLook.Facade, -4.5f, 419f, 440f, VoidBottom, low + 6.5f);
            Cue(LabCueKind.Jump, 416.6f);
            Cue(LabCueKind.WallJump, 431f);
            Cue(LabCueKind.DoubleJumpAtApex, 431.5f);
            Beat(CourseBeatKind.Wallrun, "The last wall", 416.6f, 21f, 6.5f);
            Lane(433f, -3.6f); Lane(439f, 0f);

            Section(440f, "The light");
            Add("The lit porch", LabPieceKind.Floor, DreamLook.ExitPorch, new Vector3(-6f, VoidBottom, 440.5f), new Vector3(6f, exit, 462f), tint: 6);
            exitLight = new Vector3(0f, exit, 452f);
            Billboard("The dog (by the light)", DreamCast.Dog, 3.4f, exit, 457f, 0.75f);
            c.finishS = 446f;
            c.chaseEndS = 446f;

            // ===== J. The front door, into the rain (Night 2's house escape ends this way; there is no house escape yet, so
            // its ending lives here). A closed umbrella leans by the door: the dream tool takes it. Outside, the rain soaks
            // her unless it's open; going on without it, past the point of no return, is the Despair route.
            umbrellaResetS = 440f;
            rainStartS = exitLight.Z + 9.6f;
            umbrella = new Vector3(2.75f, exit + 0.55f, rainStartS - 0.6f);
            Section(rainStartS, "The rain");
            Add("The garden path (rain)", LabPieceKind.Floor, DreamLook.Street, new Vector3(-4f, exit - 2f, 462f), new Vector3(4f, exit, 500f), tint: 0);
            despairS = rainStartS + 8f;
            rainEndS = 494f;

            // Houses standing in the void on both sides of the rooftops and the final run.
            for (float z = 236f; z < 470f; z += 15f)
            {
                float hl = r - 3f + (float)((z * 0.13) % 7.0), hr = r - 2f + (float)((z * 0.29) % 6.0);
                if (z < 288f || z > 356f) Add("Far house (left)", LabPieceKind.Wall, DreamLook.House, new Vector3(-26f, VoidBottom, z), new Vector3(-15f, hl, z + 10f));
                Add("Far house (right)", LabPieceKind.Wall, DreamLook.House, new Vector3(14f, VoidBottom, z), new Vector3(25f, hr, z + 10f));
            }

            c.pursuer = new Pursuer { targetGap = 12f, k = 0.6f, minSpeed = 6f, maxSpeed = 11.5f };
            // s = z (the first segment extends straight back past 0, so Walt's driveway reads as negative s).
            c.route = new PathLine(new[] { new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 520f) });
        }
    }
}
