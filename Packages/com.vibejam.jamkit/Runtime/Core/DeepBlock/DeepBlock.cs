using System;
using System.Collections.Generic;
using System.Numerics;
using JamKit.Movement;
using JamKit.Stealth;

namespace JamKit.DeepBlock
{
    public enum BlockKind
    {
        Ground, Lane, Roof, House, Garage, Shop, BrickWall, BlockWall, Fence, Picket, Hedge, Ledge, Deck, Post, Prop,
        Gate, Door, Plank, Scaffold, Glass, Sheet, Water, Tree, Deco,
        /// <summary>Painted on the ground (road, pavement, lane tarmac): seen, not solid.</summary>
        Paint,
        /// <summary>The world's edge: solid, never drawn.</summary>
        Boundary,
    }

    /// <summary>One box of the deep block, in world metres. A ramp's top slopes along X (a roof pitch) or Z (the lane)
    /// from <see cref="rampLow"/> at the axis's min to <see cref="rampHigh"/> at its max. A piece with a switch exists only
    /// when that switch is open (<see cref="presentWhenOpen"/>) or shut.</summary>
    public sealed class BlockPiece
    {
        public string name;
        public BlockKind kind;
        public Vector3 center, size;
        public bool solid = true, runnable, usable;
        /// <summary>Blocks sight without blocking movement (sheets on the line).</summary>
        public bool sightOnly;
        /// <summary>0 flat, 1 slopes along X, 2 along Z.</summary>
        public int rampAxis;
        public float rampLow, rampHigh;
        /// <summary>-1: always there. 0-7: S1-S8. 8: the washing-line sheets.</summary>
        public int switchIndex = -1;
        public bool presentWhenOpen = true;
        public string layer = "";
        /// <summary>How the hill draws it (a fence's or wall's style: "picket", "board", "stone_retaining"...; "" the kind's default).</summary>
        public string style = "";

        public Vector3 Min => center - size * 0.5f;
        public Vector3 Max => center + size * 0.5f;
        public bool BlocksSight => (solid && kind != BlockKind.Glass) || sightOnly;

        public bool PresentOn(bool[] open)
        {
            if (switchIndex < 0 || open == null || switchIndex >= open.Length) return true;
            return open[switchIndex] == presentWhenOpen;
        }

        /// <summary>The top at a point (ramps slope; boxes are flat).</summary>
        public float TopAt(float x, float z)
        {
            if (rampAxis == 0) return Max.Y;
            float t = rampAxis == 1 ? (x - Min.X) / Math.Max(1e-4f, size.X) : (z - Min.Z) / Math.Max(1e-4f, size.Z);
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            return rampLow + (rampHigh - rampLow) * t;
        }
    }

    public sealed class BlockSwitch
    {
        public string id, name;
        /// <summary>The map position (for the HUD and the gizmo).</summary>
        public Vector3 position;
    }

    /// <summary>One of the four routes to the hill gate: an autopilot course (route, lane, cues, start and finish) and
    /// the beats <see cref="CourseReach"/> checks.</summary>
    public sealed class BlockRoute
    {
        public string name, chapter, layer;
        public LabCourse course;
        public readonly List<CourseBeat> beats = new List<CourseBeat>();
        public Vector3 start;
        public float startYaw;
        public float finishS;
        /// <summary>The doc's rough time for the route (s).</summary>
        public float docSeconds;
    }

    /// <summary>
    /// Layout A of docs/DENSE-STREET.md, "the deep block", as data: Larch Street (120 m) with its front, back, roof and
    /// under layers, the eight per-night switches, the four routes from Walt's shop to the hill gate, and the
    /// hide-and-seek zone (gardens 1 and 3 plus the lane's west half) with Theo's trampoline, eight hiding places and six
    /// memories. The builder makes the Deep Block lab from it; the core tests run the autopilot along each route and
    /// Theo's search through it. Coordinates: the doc's map has x along the street (0 = Walt's shop, east = up the
    /// street) and y from the north boundary (0) south to the creek (112); the world has <b>Z = map x</b>,
    /// <b>X = map y</b> (so looking up the street, south is on the right) and Y up, ground at 0. docs/DEEP-BLOCK.md lists
    /// every place this greybox departs from the doc's plan and why.
    /// </summary>
    public sealed class DeepBlock
    {
        public readonly List<BlockPiece> pieces = new List<BlockPiece>();
        public readonly List<BlockSwitch> switches = new List<BlockSwitch>();
        public readonly List<BlockRoute> routes = new List<BlockRoute>();

        // Hide-and-seek
        public readonly List<HidingSpot> spots = new List<HidingSpot>();
        public readonly List<Vector3> memories = new List<Vector3>();
        public readonly List<Vector3> theoNodes = new List<Vector3>();
        public Vector3 trampoline, playerHideStart;
        public float theoCountYaw, playerHideYaw;
        public float trampolineHeight = 0.9f, bounceHeight = 1.3f, bouncePeriod = 0.8f;
        /// <summary>The zone, on the map: x 0.5-42.5, y 5.5-34.3 (42 x 29 m).</summary>
        public Vector3 zoneMin, zoneMax;
        public Vector3 sunsetFrom;

        public const int SwitchCount = 8;
        public const int SheetsSwitch = 8;
        public const int NightCount = 4;
        public const float LaneFlatUntil = 40f, LaneTop = 2f, LaneEnd = 100f;
        public const float Eaves = 5.2f, Ridge = 8f;

        public const string ChapterStreet = "Street route";
        public const string ChapterBack = "Back route";
        public const string ChapterRoof = "Roof route";
        public const string ChapterUnder = "Under route";
        public const string ChapterHideAndSeek = "Hide and seek";

        /// <summary>World point from map coordinates.</summary>
        public static Vector3 W(float mx, float my, float h = 0f) => new Vector3(my, h, mx);

        public static DeepBlock Build()
        {
            var b = new DeepBlock();
            b.MakeSwitches();
            b.MakeGround();
            b.MakeNorth();
            b.MakeSouth();
            b.MakeStreetAndHill();
            b.MakeHideAndSeek();
            b.MakeRoutes();
            return b;
        }

        // ---------- Nights ----------

        /// <summary>Which switches are open on night <paramref name="night"/> (1-4; index 8: the sheets are up). docs/DEEP-BLOCK.md.</summary>
        public static bool[] Night(int night)
        {
            // S1 garage door G3, S2 side gate A, S3 house 3's back door, S4 scaffold ladder (open = down),
            // S5 plank over the creek, S6 the lane's top gate, S7 the snicket gate, S8 Walt's door (open = half open).
            var o = new bool[SwitchCount + 1];
            for (int i = 0; i < SwitchCount; i++) o[i] = true;
            o[SheetsSwitch] = false;
            if (night >= 3)
            {
                o[0] = false; o[1] = false; o[5] = false; o[6] = false; o[SheetsSwitch] = true;
            }
            if (night >= 4)
            {
                o[2] = false; o[3] = false; o[4] = false; o[7] = false;
            }
            return o;
        }

        public static string NightWords(int night)
        {
            switch (night)
            {
                case 1: return "Night 1: everything open; nobody hunts";
                case 2: return "Night 2: everything open; hide-and-seek with Theo";
                case 3: return "Night 3: the lane's top gate, the garage, side gate A and the snicket shut; sheets up";
                default: return "Night 4: the back layer shut; the scaffold ladder up, no plank, Walt's door shut";
            }
        }

        void MakeSwitches()
        {
            void Sw(string id, string name, float mx, float my) => switches.Add(new BlockSwitch { id = id, name = name, position = W(mx, my) });
            Sw("S1", "garage door G3 (garden to lane)", 29f, 16.2f);
            Sw("S2", "side gate A", 22.25f, 52f);
            Sw("S3", "house 3's back door", 32f, 34f);
            Sw("S4", "the scaffold ladder", 70.8f, 33f);
            Sw("S5", "the plank over the creek", 72f, 106f);
            Sw("S6", "the lane's top gate", 97.75f, 7.5f);
            Sw("S7", "the snicket gate", 46f, 64f);
            Sw("S8", "Walt's door", 8.5f, 70f);
        }

        // ---------- Building ----------

        string _layer = "";

        BlockPiece Rect(string name, BlockKind kind, float mx0, float my0, float mx1, float my1, float bottom, float top,
            bool runnable = false, bool usable = false, bool solid = true)
        {
            var mn = new Vector3(Math.Min(my0, my1), bottom, Math.Min(mx0, mx1));
            var mx = new Vector3(Math.Max(my0, my1), top, Math.Max(mx0, mx1));
            var p = new BlockPiece { name = name, kind = kind, center = (mn + mx) * 0.5f, size = mx - mn, runnable = runnable, usable = usable, solid = solid, layer = _layer };
            pieces.Add(p);
            return p;
        }

        /// <summary>A slab of ground with its top at <paramref name="top"/>, solid down to 3 m below the street (or 3 m
        /// under its top, for the creek beds).</summary>
        BlockPiece Ground(string name, float mx0, float my0, float mx1, float my1, float top = 0f) =>
            Rect(name, BlockKind.Ground, mx0, my0, mx1, my1, Math.Min(top - 3f, -3f), top);

        /// <summary>A thin fence or wall along a map line (x0, y0)-(x1, y1) (axis-aligned), <paramref name="thick"/> m thick.</summary>
        BlockPiece Line(string name, BlockKind kind, float mx0, float my0, float mx1, float my1, float height, float thick,
            bool runnable = false, bool usable = false, float bottom = 0f)
        {
            float h = thick * 0.5f;
            if (Math.Abs(my0 - my1) < 1e-3f) return Rect(name, kind, mx0, my0 - h, mx1, my1 + h, bottom, bottom + height, runnable, usable);
            return Rect(name, kind, mx0 - h, my0, mx1 + h, my1, bottom, bottom + height, runnable, usable);
        }

        /// <summary>The flat strip along each eaves (the gutter) before a pitch rises: a mantle lands on it, not on the slope.</summary>
        public const float EavesStrip = 0.8f;

        /// <summary>A pitched roof over a house footprint, its ridge along the street (map x): two pitches sloping along X,
        /// each starting <see cref="EavesStrip"/> in from its wall.</summary>
        void PitchedRoof(string name, float mx0, float my0, float mx1, float my1)
        {
            float mid = (my0 + my1) * 0.5f;
            var a = Rect(name + " (north pitch)", BlockKind.Roof, mx0, my0 + EavesStrip, mx1, mid, Eaves, Ridge);
            a.rampAxis = 1; a.rampLow = Eaves; a.rampHigh = Ridge;
            var c = Rect(name + " (south pitch)", BlockKind.Roof, mx0, mid, mx1, my1 - EavesStrip, Eaves, Ridge);
            c.rampAxis = 1; c.rampLow = Ridge; c.rampHigh = Eaves;
        }

        BlockPiece Switched(BlockPiece p, int index, bool presentWhenOpen)
        {
            p.switchIndex = index;
            p.presentWhenOpen = presentWhenOpen;
            return p;
        }

        void MakeGround()
        {
            _layer = "ground";
            // The creek (map y 103-109) and the channel north from it (x 86-92) under the street to the pond (x 80-98,
            // y 28-42) are 2 m below the ground; the street over the channel is the culvert (1.15 m clear).
            Ground("Ground (Birch Lane strip)", 0f, 0f, 100f, 5f);
            Ground("Ground (gardens, west)", 0f, 10f, 80f, 52f);
            Ground("Ground (by the pond, north)", 80f, 10f, 98f, 28f);
            Ground("Ground (by the pond, west)", 80f, 42f, 86f, 52f);
            Ground("Ground (by the pond, east)", 92f, 42f, 98f, 52f);
            Ground("Ground (east of the pond)", 98f, 10f, 100f, 52f);
            Ground("Ground (street and south, west)", 0f, 52f, 86f, 103f);
            Ground("Ground (street and south, east)", 92f, 52f, 124f, 103f);
            Ground("Ground (south of the creek)", 0f, 109f, 124f, 112f);
            Rect("Culvert (the street over the channel)", BlockKind.Ground, 86f, 52f, 92f, 64f, -0.85f, 0f);
            Ground("Creek bed", 0f, 103f, 124f, 109f, -2f);
            Ground("Channel bed", 86f, 42f, 92f, 103f, -2f);
            Ground("Pond bed", 80f, 28f, 98f, 42f, -2f);
            // The lane: flat to x 40, then rising 2 m to the hill end (it falls toward Walt's: a slide lane).
            Ground("Lane (flat west half)", 0f, 5f, LaneFlatUntil, 10f);
            var lane = Rect("Lane (the slope)", BlockKind.Lane, LaneFlatUntil, 5f, LaneEnd, 10f, -3f, LaneTop);
            lane.rampAxis = 2; lane.rampLow = 0f; lane.rampHigh = LaneTop;
            // Water (no collider): the creek, the channel and the pond, 0.3 m deep over the beds.
            _layer = "under";
            Rect("Creek water", BlockKind.Water, 0f, 103f, 124f, 109f, -2f, -1.7f, solid: false);
            Rect("Pond water", BlockKind.Water, 80f, 28f, 98f, 42f, -2f, -1.7f, solid: false);
            Rect("Pond step", BlockKind.Ledge, 96f, 38f, 98f, 42f, -2f, -0.6f, usable: true);
            Switched(Rect("Plank over the creek", BlockKind.Plank, 71f, 103f, 73f, 109f, -0.15f, 0f, usable: true), 4, true);
        }

        void MakeNorth()
        {
            _layer = "back";
            // The boundary: the brick backs of Birch Lane's garages, 3.2 m, runnable along the lane.
            Rect("Birch Lane garages (boundary wall)", BlockKind.BrickWall, 0f, 0f, 100f, 5f, 0f, 3.2f, runnable: true, usable: true);
            // Garages along the lane (6 x 6, flat roofs 2.7). The second (x 8-14) is open both ends with its lane door
            // half open (slide under); G3's (x 26-32) opens to its garden through S1; the first (x 2-8) is open to the lane.
            void SolidGarage(float x0) => Rect("Garage " + x0, BlockKind.Garage, x0, 10f, x0 + 6f, 16f, 0f, 2.7f, usable: true);
            void HollowGarage(string name, float x0, bool backOpen)
            {
                Rect(name + " west wall", BlockKind.Garage, x0, 10f, x0 + 0.3f, 16f, 0f, 2.4f);
                Rect(name + " east wall", BlockKind.Garage, x0 + 5.7f, 10f, x0 + 6f, 16f, 0f, 2.4f);
                Rect(name + " roof", BlockKind.Garage, x0, 10f, x0 + 6f, 16f, 2.4f, 2.7f, usable: true);
                if (!backOpen) Rect(name + " back wall", BlockKind.Garage, x0, 15.7f, x0 + 6f, 16f, 0f, 2.4f);
            }
            HollowGarage("Garage 2 (open to the lane)", 2f, false);
            HollowGarage("Garage 8 (the through garage)", 8f, true);
            Rect("Garage 8 lane door (half open)", BlockKind.Door, 8.3f, 10f, 13.7f, 10.3f, 1.15f, 2.4f, usable: true);
            HollowGarage("Garage G3", 26f, true);
            Switched(Rect("Garage G3 back door (S1 shut)", BlockKind.Door, 26.3f, 15.7f, 31.7f, 16f, 0f, 2.4f), 0, false);
            SolidGarage(34f); SolidGarage(47f); SolidGarage(53f);

            // The garden fences (1.8: a mantle from the ground). The back fence along y 16.2 has gaps at the hollow
            // garages; the plot dividers have a garden gate (Theo's way between the gardens).
            float fy = 16.2f;
            Line("Back fence (x 0-8)", BlockKind.Fence, 0f, fy, 8f, fy, 1.8f, 0.12f, usable: true);
            Line("Back fence (x 14-26)", BlockKind.Fence, 14f, fy, 26f, fy, 1.8f, 0.12f, usable: true);
            Line("Back fence (x 32-100)", BlockKind.Fence, 32f, fy, 100f, fy, 1.8f, 0.12f, usable: true);
            Line("Fence 1|3 (south)", BlockKind.Fence, 22.25f, 16.2f, 22.25f, 19.5f, 1.8f, 0.12f, usable: true);
            Line("Fence 1|3 (north)", BlockKind.Fence, 22.25f, 21.5f, 22.25f, 34f, 1.8f, 0.12f, usable: true);
            Line("Fence 3|5", BlockKind.Fence, 42.75f, 16.2f, 42.75f, 34f, 1.8f, 0.12f, usable: true);
            Line("Fence 5|lot", BlockKind.Fence, 61f, 16.2f, 61f, 32f, 1.8f, 0.12f, usable: true);
            Line("Hedge (garden 1)", BlockKind.Hedge, 1f, 18f, 13f, 18f, 1.2f, 0.6f, usable: true);

            // The north houses: eaves 5.2, ridge 8; porch roofs 2.9; side passages A and B (4.5 m) between them.
            foreach (var h in new[] { (4f, "House 1"), (24.5f, "House 3"), (45f, "House 5") })
            {
                float x0 = h.Item1;
                Rect(h.Item2, BlockKind.House, x0, 34f, x0 + 16f, 46f, 0f, Eaves, runnable: true);
                PitchedRoof(h.Item2 + " roof", x0, 34f, x0 + 16f, 46f);
                Rect(h.Item2 + " porch roof", BlockKind.Ledge, x0 + 3f, 46f, x0 + 10f, 48f, 0f, 2.9f, usable: true);
            }
            Switched(Rect("House 3 back door (S3 shut)", BlockKind.Door, 31f, 33.85f, 33f, 34f, 0f, 2.1f), 2, false);
            Switched(Rect("House 3 back door (open)", BlockKind.Door, 33f, 32.95f, 33.1f, 33.85f, 0f, 2.1f, solid: false), 2, true);

            // Garden 1 (Mrs. Lindqvist): the greenhouse (glass), the oak and its treehouse, the deck (crawl under).
            Rect("Greenhouse", BlockKind.Glass, 3f, 20f, 9f, 26f, 0f, 2.4f, solid: false);
            Rect("Oak", BlockKind.Tree, 14.6f, 23.6f, 15.4f, 24.4f, 0f, 6f);
            Rect("Oak canopy", BlockKind.Tree, 11.5f, 20.5f, 18.5f, 27.5f, 4.6f, 7.2f, solid: false);
            Rect("Treehouse floor", BlockKind.Ledge, 13.5f, 22.5f, 16.5f, 25.5f, 2.8f, 3.0f, usable: true);
            Rect("Treehouse walls", BlockKind.Deco, 13.5f, 22.5f, 16.5f, 25.5f, 3.0f, 4.4f, solid: false);
            Rect("Treehouse stump", BlockKind.Ledge, 16.5f, 25.6f, 17.5f, 26.6f, 0f, 1.4f, usable: true);
            Deck("Deck, house 1", 4f, 31f, 20f, 34f);
            // Garden 3: the trampoline (Theo's base), the washing line, the shed, the deck, the bins in passage A.
            Rect("Trampoline", BlockKind.Deco, 30.2f, 22.2f, 33.8f, 25.8f, 0f, 0.9f, solid: false);
            Line("Washing line", BlockKind.Deco, 26f, 29f, 38f, 29f, 0.03f, 0.03f, bottom: 2f).solid = false;
            var sheets = Rect("Sheets on the line", BlockKind.Sheet, 26.5f, 28.95f, 37.5f, 29.05f, 0.25f, 2f, solid: false);
            sheets.sightOnly = true;
            Switched(sheets, SheetsSwitch, true);
            Rect("Shed (garden 3) north wall", BlockKind.Prop, 37.5f, 18f, 42.5f, 18.15f, 0f, 2.2f);
            Rect("Shed (garden 3) south wall", BlockKind.Prop, 37.5f, 21.85f, 42.5f, 22f, 0f, 2.2f);
            Rect("Shed (garden 3) east wall", BlockKind.Prop, 42.35f, 18f, 42.5f, 22f, 0f, 2.2f);
            Rect("Shed (garden 3) west wall (by the door)", BlockKind.Prop, 37.5f, 18f, 37.65f, 19.4f, 0f, 2.2f);
            Rect("Shed (garden 3) roof", BlockKind.Ledge, 37.5f, 18f, 42.5f, 22f, 2.2f, 2.35f, usable: true);
            Deck("Deck, house 3", 24.5f, 31f, 40.5f, 34f);
            Rect("Bins", BlockKind.Prop, 22.4f, 28.5f, 23.6f, 30.5f, 0f, 1.2f);
            // Garden 5, the builders': pallets, sand, the half-built extension (block walls, no roof) with its scaffold
            // (decks 6, 4 and 2 stepping east, the ladder S4), and the long block wall of its unfinished wing.
            Rect("Pallets", BlockKind.Ledge, 47f, 20f, 52f, 23f, 0f, 1.0f, usable: true);
            Rect("Sand", BlockKind.Deco, 54f, 22f, 58f, 26f, 0f, 0.15f, solid: false);
            Rect("Extension north wall (west)", BlockKind.BlockWall, 61f, 32f, 64.5f, 32.3f, 0f, 3f);
            Rect("Extension north wall (east)", BlockKind.BlockWall, 67.5f, 32f, 71f, 32.3f, 0f, 3f);
            Rect("Extension east wall (north)", BlockKind.BlockWall, 70.7f, 32f, 71f, 35f, 0f, 3f);
            Rect("Extension east wall (south)", BlockKind.BlockWall, 70.7f, 38.5f, 71f, 46f, 0f, 3f);
            Rect("Extension south wall", BlockKind.BlockWall, 61f, 45.7f, 71f, 46f, 0f, 3f);
            _layer = "roof";
            Rect("Scaffold deck 6", BlockKind.Scaffold, 61.2f, 35f, 64.2f, 38.5f, 5.8f, 6.0f, usable: true);
            Rect("Scaffold deck 4", BlockKind.Scaffold, 64.2f, 35f, 67.2f, 38.5f, 3.8f, 4.0f, usable: true);
            Rect("Scaffold deck 2", BlockKind.Scaffold, 67.2f, 35f, 70.7f, 38.5f, 1.8f, 2.0f, usable: true);
            foreach (var (px, py) in new[] { (61.3f, 35.1f), (61.3f, 38.4f), (64.1f, 35.1f), (64.1f, 38.4f), (67.3f, 35.1f), (67.3f, 38.4f), (70.5f, 35.1f), (70.5f, 38.4f) })
                Rect("Scaffold pole", BlockKind.Post, px - 0.05f, py - 0.05f, px + 0.05f, py + 0.05f, 0f, 6f);
            Switched(Rect("Scaffold ladder step 1", BlockKind.Scaffold, 68.6f, 33.5f, 70.6f, 35f, 0f, 1.0f, usable: true), 3, true);
            Rect("Unfinished wing (block wall)", BlockKind.BlockWall, 72f, 20f, 86f, 20.5f, 0f, 3.6f, runnable: true, usable: true);
            Rect("Skip", BlockKind.Ledge, 89f, 17f, 94f, 19.5f, 0f, 1.6f, usable: true);
            _layer = "back";
            // The lane's top gate (S6): shut, as tall as the boundary wall.
            Switched(Rect("Lane top gate (S6 shut)", BlockKind.Gate, 97.5f, 5f, 98f, 10f, 0f, 3.2f), 5, false);
        }

        void Deck(string name, float x0, float y0, float x1, float y1)
        {
            // Top 1.3, underside 1.15: crawl or slide under (the Movement Lab's bar height); posts at the corners.
            Rect(name, BlockKind.Deck, x0, y0, x1, y1, 1.15f, 1.3f, usable: true);
            foreach (var (px, py) in new[] { (x0 + 0.15f, y0 + 0.15f), (x1 - 0.15f, y0 + 0.15f), (x0 + 0.15f, y1 - 0.15f), (x1 - 0.15f, y1 - 0.15f) })
                Rect(name + " post", BlockKind.Post, px - 0.1f, py - 0.1f, px + 0.1f, py + 0.1f, 0f, 1.15f);
        }

        void MakeSouth()
        {
            _layer = "under";
            // Walt's shop (flat roof 4.0): hollow, the front door (S8: half open = slide under, or shut), the back door
            // to his yard, a skylight; the lean-to (2.4) beside it.
            Rect("Walt's shop west wall", BlockKind.Shop, 2f, 70f, 2.3f, 82f, 0f, 4f, runnable: true);
            Rect("Walt's shop east wall", BlockKind.Shop, 19.7f, 70f, 20f, 82f, 0f, 4f, runnable: true);
            Rect("Walt's shop front (west of the door)", BlockKind.Shop, 2f, 70f, 7f, 70.3f, 0f, 4f);
            Rect("Walt's shop front (east of the door)", BlockKind.Shop, 10f, 70f, 20f, 70.3f, 0f, 4f);
            Rect("Walt's shop front (over the door)", BlockKind.Shop, 7f, 70f, 10f, 70.3f, 3f, 4f);
            Rect("Walt's shop back (west of the door)", BlockKind.Shop, 2f, 81.7f, 14f, 82f, 0f, 4f);
            Rect("Walt's shop back (east of the door)", BlockKind.Shop, 16f, 81.7f, 20f, 82f, 0f, 4f);
            Rect("Walt's shop back (over the door)", BlockKind.Shop, 14f, 81.7f, 16f, 82f, 2.4f, 4f);
            Rect("Walt's shop roof (west)", BlockKind.Shop, 2f, 70f, 12f, 82f, 3.7f, 4f, usable: true);
            Rect("Walt's shop roof (east)", BlockKind.Shop, 14f, 70f, 20f, 82f, 3.7f, 4f, usable: true);
            Rect("Walt's shop roof (by the skylight)", BlockKind.Shop, 12f, 70f, 14f, 74f, 3.7f, 4f);
            Rect("Walt's shop roof (by the skylight, back)", BlockKind.Shop, 12f, 76f, 14f, 82f, 3.7f, 4f);
            Switched(Rect("Walt's door (half open)", BlockKind.Door, 7f, 69.95f, 10f, 70.05f, 1.15f, 3f, usable: true), 7, true);
            Switched(Rect("Walt's door (shut)", BlockKind.Door, 7f, 69.95f, 10f, 70.05f, 0f, 3f), 7, false);
            Rect("Lean-to", BlockKind.Ledge, 20f, 72f, 26f, 80f, 0f, 2.4f, usable: true);
            Line("Shop front path fence (west)", BlockKind.Picket, 0f, 64f, 6.5f, 64f, 0.85f, 0.08f);
            // South houses 2 and 4 with the snicket between (4 m, runnable walls, the gate S7).
            Rect("House 2", BlockKind.House, 28f, 70f, 44f, 82f, 0f, Eaves, runnable: true);
            PitchedRoof("House 2 roof", 28f, 70f, 44f, 82f);
            Rect("House 4", BlockKind.House, 48f, 70f, 64f, 82f, 0f, Eaves, runnable: true);
            PitchedRoof("House 4 roof", 48f, 70f, 64f, 82f);
            Switched(Rect("Snicket gate (S7 shut)", BlockKind.Gate, 44f, 63.95f, 48f, 64.1f, 0f, 2.2f), 6, false);
            Line("South fence (Walt | house 2)", BlockKind.Fence, 26.5f, 82f, 26.5f, 100f, 1.8f, 0.12f, usable: true);
            Line("South fence (house 2 | 4)", BlockKind.Fence, 46f, 82f, 46f, 100f, 1.8f, 0.12f, usable: true);
            Deck("Stilt deck (house 2)", 30f, 86f, 40f, 90f);
            Rect("Shed (house 4)", BlockKind.Prop, 52f, 88f, 56f, 91f, 0f, 2.2f);
            for (int i = 0; i < 5; i++)
                Line("Bean row " + (i + 1), BlockKind.Deco, 67f, 86f + i * 2.5f, 83f, 86f + i * 2.5f, 1.8f, 0.3f).solid = false;
            Rect("Cold frames", BlockKind.Prop, 67f, 97f, 75f, 98f, 0f, 0.5f);
            // The pickets along the street (0.85: hop), with gaps at the paths and the culvert.
            Line("Picket (south, Walt to house 2)", BlockKind.Picket, 11f, 64f, 44f, 64f, 0.85f, 0.08f);
            Line("Picket (south, house 4 to the channel)", BlockKind.Picket, 48f, 64f, 86f, 64f, 0.85f, 0.08f);
        }

        void MakeStreetAndHill()
        {
            _layer = "street";
            Rect("Road", BlockKind.Paint, 0f, 54f, 124f, 62f, 0f, 0.02f, solid: false);
            Rect("Pavement (north)", BlockKind.Paint, 0f, 52f, 124f, 54f, 0f, 0.03f, solid: false);
            Rect("Pavement (south)", BlockKind.Paint, 0f, 62f, 86f, 64f, 0f, 0.03f, solid: false);
            Rect("Pavement (south, east)", BlockKind.Paint, 92f, 62f, 124f, 64f, 0f, 0.03f, solid: false);
            Rect("Lane tarmac (flat)", BlockKind.Paint, 0f, 5f, LaneFlatUntil, 10f, 0f, 0.02f, solid: false);
            // The world's edge.
            Rect("Edge (west)", BlockKind.Boundary, -1f, -1f, 0f, 113f, -6f, 12f);
            Rect("Edge (east)", BlockKind.Boundary, 124f, -1f, 125f, 113f, -6f, 12f);
            Rect("Edge (north)", BlockKind.Boundary, 0f, -1f, 124f, 0f, -6f, 12f);
            Rect("Edge (south)", BlockKind.Boundary, 0f, 112f, 124f, 113f, -6f, 12f);
            // The north pickets (y 52) with side gate A (S2) at passage A's mouth.
            Line("Picket (north, west)", BlockKind.Picket, 0f, 52f, 20f, 52f, 0.85f, 0.08f);
            Line("Picket (north, middle)", BlockKind.Picket, 24.5f, 52f, 86f, 52f, 0.85f, 0.08f);
            Switched(Rect("Side gate A (S2 shut)", BlockKind.Gate, 20f, 51.9f, 24.5f, 52.1f, 0f, 2.2f), 1, false);
            // The block party on the road: trestles, the marquee (roof 2.6), the bunting posts, the sawhorses across the
            // pavement at each end (0.85: hop them).
            Rect("Trestle (west)", BlockKind.Prop, 41f, 56.5f, 50f, 58f, 0f, 0.8f);
            Rect("Trestle (east)", BlockKind.Prop, 62f, 56.5f, 71f, 58f, 0f, 0.8f);
            Rect("Marquee roof", BlockKind.Ledge, 52f, 55f, 60f, 61f, 2.45f, 2.6f, usable: true);
            foreach (var (px, py) in new[] { (52.1f, 55.1f), (59.9f, 55.1f), (52.1f, 60.9f), (59.9f, 60.9f) })
                Rect("Marquee pole", BlockKind.Post, px - 0.06f, py - 0.06f, px + 0.06f, py + 0.06f, 0f, 2.45f);
            Rect("Bunting post (west)", BlockKind.Post, 36.8f, 55f, 37.2f, 55.4f, 0f, 3f);
            Rect("Bunting post (east)", BlockKind.Post, 82.8f, 55f, 83.2f, 55.4f, 0f, 3f);
            Rect("Bunting", BlockKind.Deco, 37f, 55.15f, 83f, 55.25f, 2.6f, 2.8f, solid: false);
            Rect("Sawhorse (west)", BlockKind.Prop, 36.8f, 60f, 37.2f, 64f, 0f, 0.85f, usable: true);
            Rect("Sawhorse (east)", BlockKind.Prop, 82.8f, 60f, 83.2f, 64f, 0f, 0.85f, usable: true);

            // The hill path: a mantle stair of 1.5 m terraces up to the gate's apron (6.0); the lot behind the gate at
            // the lane's height (2.0) with steps up the west side for the lane and the builders' yard.
            _layer = "hill";
            Ground("Hill terrace 1", 100f, 42f, 124f, 48f, 1.5f).usable = true;
            Ground("Hill terrace 2", 100f, 36f, 124f, 42f, 3f).usable = true;
            Ground("Hill terrace 3", 100f, 30f, 124f, 36f, 4.5f).usable = true;
            Ground("Hill gate apron", 100f, 24f, 124f, 30f, 6f).usable = true;
            Ground("Hill ground (by the street)", 100f, 48f, 124f, 52f, 0f);
            Ground("Upper lot (lane level)", 100f, 0f, 124f, 15f, LaneTop);
            Ground("Upper lot step 1", 100f, 15f, 104f, 20f, 3.2f).usable = true;
            Ground("Upper lot step 2", 100f, 20f, 104f, 24f, 4.6f).usable = true;
            Ground("Hill (behind the gate)", 104f, 15f, 124f, 24f, 6f);
            Rect("Hill house gate (chained)", BlockKind.Gate, 108f, 22f, 118f, 23f, 6f, 9f);
            Rect("Hill house gate pillar (west)", BlockKind.BrickWall, 104f, 21.5f, 108f, 23.5f, 6f, 10f);
            Rect("Hill house gate pillar (east)", BlockKind.BrickWall, 118f, 21.5f, 122f, 23.5f, 6f, 10f);
            Rect("Builders' crate", BlockKind.Ledge, 98f, 17f, 100f, 20f, 0f, 1.6f, usable: true);
        }

        // ---------- Hide-and-seek ----------

        void MakeHideAndSeek()
        {
            zoneMin = W(0.5f, 5.5f);
            zoneMax = W(42.5f, 34.3f, 6f);
            trampoline = W(32f, 24f);
            theoCountYaw = 0f; // facing up the street (map +x = world +Z), away from garden 1
            playerHideStart = W(26f, 26f);
            playerHideYaw = -90f;
            void Spot(string name, HideKind kind, float x0, float y0, float x1, float y1, float bottom, float top, float ax, float ay)
            {
                var c = W((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, (bottom + top) * 0.5f);
                var size = new Vector3(Math.Abs(y1 - y0), top - bottom, Math.Abs(x1 - x0));
                spots.Add(HidingSpot.Box(name, kind, c, size, W(ax, ay)));
            }
            Spot("H1 under house 1's deck", HideKind.Under, 4.5f, 31.2f, 19.5f, 33.8f, 0f, 1.15f, 12f, 29.5f);
            Spot("H2 the treehouse", HideKind.Closet, 13.5f, 22.5f, 16.5f, 25.5f, 3.0f, 4.5f, 15f, 27.6f);
            Spot("H3 the greenhouse", HideKind.Bush, 3.2f, 20.2f, 8.8f, 25.8f, 0f, 2.4f, 10.5f, 23f);
            Spot("H4 under house 3's deck", HideKind.Under, 25f, 31.2f, 40f, 33.8f, 0f, 1.15f, 36f, 30f);
            Spot("H5 the shed", HideKind.Closet, 37.7f, 18.2f, 42.3f, 21.8f, 0f, 2.2f, 36f, 20.25f);
            Spot("H6 behind the bins", HideKind.Bush, 23.6f, 28.4f, 25f, 30.6f, 0f, 1.6f, 26.5f, 27.5f);
            Spot("H7 on the garage roof", HideKind.Bush, 26.5f, 10.5f, 31.5f, 15.5f, 2.7f, 4.3f, 29f, 18f);
            Spot("H8 in garage 2", HideKind.Closet, 2.4f, 10.4f, 7.6f, 15.6f, 0f, 2.4f, 5f, 8f);
            // Six memories, one near each kind of place (the doc's slots).
            memories.Add(W(9f, 19.4f, 0.45f));
            memories.Add(W(13f, 33f, 0.45f));
            memories.Add(W(30f, 29.6f, 0.45f));
            memories.Add(W(43.6f, 12.5f, 0.45f));
            memories.Add(W(17f, 7.5f, 0.45f));
            memories.Add(W(22.25f, 45f, 0.45f));
            // Theo's walking points (the trampoline first).
            foreach (var (x, y) in new[]
            {
                (32f, 24f), (28f, 20f), (36f, 26f), (28f, 27f), (23.5f, 20.5f), (21f, 20.5f), (18f, 21f), (12f, 28.4f),
                (10.5f, 23f), (16f, 19.5f), (29f, 18f), (29f, 13f), (29f, 7.5f), (11f, 7.5f), (5f, 8f), (40f, 7.5f),
                (20f, 7.5f),
                (36f, 20.25f), (26.5f, 27.5f), (36f, 30f), (15f, 27.6f), (11f, 13f), (18f, 29.5f),
            })
                theoNodes.Add(W(x, y));
        }

        /// <summary>Theo's numbers: a child's eye (1.1 m) that doesn't look up (±15°), counting on the trampoline.</summary>
        public static StealthDef TheoDef()
        {
            var d = new StealthDef
            {
                eyeHeight = 1.1f,
                verticalHalfAngle = 15f,
                searchSpeed = 3f,
                investigateSpeed = 2.4f,
                countTime = 10f,
                spotSearchRadius = 12f,
            };
            return d;
        }

        /// <summary>The garden zone as a stealth world (solid pieces block sight; the sheets too when up).</summary>
        public StealthBoxWorld StealthWorld(bool[] open)
        {
            var w = new StealthBoxWorld { ambient = 1f };
            foreach (var p in pieces)
            {
                if (!p.BlocksSight || !p.PresentOn(open) || p.kind == BlockKind.Ground || p.kind == BlockKind.Lane) continue;
                if (p.kind == BlockKind.Roof) { w.AddBlocker(new Vector3(p.center.X, (p.Min.Y + Ridge) * 0.5f, p.center.Z), new Vector3(p.size.X, Ridge - Eaves, p.size.Z)); continue; }
                w.AddBlocker(p.center, p.size);
            }
            return w;
        }

        public WaypointGraph TheoGraph(IStealthWorld world)
        {
            var g = new WaypointGraph();
            foreach (var n in theoNodes) g.Add(n);
            g.AutoConnect(world, 14f);
            return g;
        }

        public SeekerBrain Theo(bool[] open, out StealthBoxWorld world)
        {
            world = StealthWorld(open);
            var brain = new SeekerBrain(TheoDef(), world, TheoGraph(world), spots, trampoline, theoCountYaw);
            brain.SetLookout(trampoline, trampolineHeight, bounceHeight, bouncePeriod, 3, 4f);
            return brain;
        }

        public int SpotAt(Vector3 feet)
        {
            for (int i = 0; i < spots.Count; i++) if (spots[i].Contains(feet)) return i;
            return -1;
        }

        // ---------- Routes ----------

        /// <summary>The height of the walkable top under a map point (the highest present solid top at or below
        /// <paramref name="maxY"/>).</summary>
        public float TopAt(float mx, float my, float maxY = 50f, bool[] open = null)
        {
            var p = W(mx, my);
            float best = -10f;
            foreach (var b in pieces)
            {
                if (!b.solid || !b.PresentOn(open)) continue;
                if (p.X < b.Min.X || p.X > b.Max.X || p.Z < b.Min.Z || p.Z > b.Max.Z) continue;
                float t = b.TopAt(p.X, p.Z);
                if (t <= maxY && t > best) best = t;
            }
            return best;
        }

        BlockRoute NewRoute(string name, string chapter, string layer, float docSeconds, params (float x, float y)[] path)
        {
            var c = new LabCourse();
            var pts = new List<Vector3>();
            foreach (var (x, y) in path) pts.Add(W(x, y));
            c.route = new PathLine(pts);
            c.lane.Add(new Vector2(0f, 0f));
            c.killY = -6f;
            var r = new BlockRoute { name = name, chapter = chapter, layer = layer, course = c, docSeconds = docSeconds };
            r.start = W(path[0].x, path[0].y, TopAt(path[0].x, path[0].y) + 0.02f);
            var d = c.route.DirectionAt(0f);
            r.startYaw = (float)(Math.Atan2(d.X, d.Z) * 180.0 / Math.PI);
            r.finishS = c.route.Length - 0.5f;
            c.finishS = r.finishS;
            c.checkpoints.Add(new LabCheckpoint { name = name + " start", chapter = chapter, position = r.start, yaw = r.startYaw, s = 0f });
            routes.Add(r);
            return r;
        }

        static float S(BlockRoute r, float mx, float my) => r.course.route.Project(W(mx, my));
        static void Cue(BlockRoute r, LabCueKind kind, float s, float sEnd = 0f) => r.course.cues.Add(new LabCue { kind = kind, s = s, sEnd = sEnd });
        static void Lane(BlockRoute r, float s, float x) => r.course.lane.Add(new Vector2(s, x));
        static void Beat(BlockRoute r, CourseBeatKind kind, string name, float s, float size, float height) =>
            r.beats.Add(new CourseBeat { kind = kind, name = name, s = s, size = size, height = height });

        void MakeRoutes()
        {
            // The street: the south pavement, hop the party's sawhorses, across the road, up the hill path's mantle stair.
            var st = NewRoute("The street", ChapterStreet, "street", 13f,
                (6f, 63f), (34f, 63f), (86f, 63f), (96f, 59f), (106f, 51f), (106f, 26.5f));
            Cue(st, LabCueKind.Jump, S(st, 35.6f, 63f));
            Cue(st, LabCueKind.Jump, S(st, 81.6f, 63f));
            Beat(st, CourseBeatKind.FenceHop, "Sawhorse (west)", S(st, 37f, 63f), 0.4f, 0.85f);
            Beat(st, CourseBeatKind.FenceHop, "Sawhorse (east)", S(st, 83f, 63f), 0.4f, 0.85f);
            for (int i = 0; i < 3; i++) Beat(st, CourseBeatKind.Mantle, "Hill terrace " + (i + 1), S(st, 106f, 48f - 6f * i), 6f, 1.5f);
            Beat(st, CourseBeatKind.Mantle, "Hill gate apron", S(st, 106f, 30f), 6f, 1.5f);

            // The back: up side passage A, garden 1, over the hedge, through the garage (slide under its half-open lane
            // door), up the lane and through its top gate, up the lot's steps.
            var bk = NewRoute("The back", ChapterBack, "back", 22f,
                (21.4f, 63f), (21.4f, 34f), (21f, 29f), (18f, 23f), (11f, 20.5f), (11f, 13f), (11f, 7.5f), (16f, 7.5f),
                (101.5f, 7.5f), (102f, 12f), (102f, 26.5f));
            Cue(bk, LabCueKind.Crouch, S(bk, 11f, 11.2f), S(bk, 11f, 8.6f));
            Beat(bk, CourseBeatKind.Mantle, "Hedge (garden 1)", S(bk, 11f, 18f), 0.6f, 1.2f);
            Beat(bk, CourseBeatKind.Slide, "Garage 8's half-open door", S(bk, 11f, 10f), 0.3f, 1.15f);
            Beat(bk, CourseBeatKind.Mantle, "Upper lot step 1", S(bk, 102f, 15f), 5f, 1.2f);
            Beat(bk, CourseBeatKind.Mantle, "Upper lot step 2", S(bk, 102f, 20f), 4f, 1.4f);
            Beat(bk, CourseBeatKind.Mantle, "Hill gate apron", S(bk, 102f, 24f), 6f, 1.4f);
            Beat(bk, CourseBeatKind.RunUp, "The lane", S(bk, 16f, 7.5f), 85f, 0f);

            // The roofs: house 1's porch roof, its eaves, over the ridge to the back pitch, along it jumping passages A and
            // B, down the scaffold's decks, a wallrun along the unfinished wing's block wall, a double jump to the skip,
            // the builders' crate, up the lot's steps.
            var rf = NewRoute("The roofs", ChapterRoof, "roof", 18f,
                (10.5f, 51f), (10.5f, 41f), (12f, 37f), (57f, 37f), (63f, 36.75f), (70f, 36.75f), (72.5f, 36.75f),
                (70f, 28f), (69.5f, 21.5f), (72.5f, 19.6f), (86f, 19.4f), (91f, 18.3f), (99f, 18.5f), (101.5f, 18.5f),
                (102f, 26.5f));
            Cue(rf, LabCueKind.Jump, S(rf, 10.5f, 49.4f));        // onto the porch roof (2.9)
            Cue(rf, LabCueKind.Jump, S(rf, 10.5f, 46.9f));        // up to the eaves (5.2)
            Cue(rf, LabCueKind.Jump, S(rf, 18.6f, 37f));          // passage A (4.5 m)
            Cue(rf, LabCueKind.Jump, S(rf, 39.1f, 37f));          // passage B (4.5 m)
            Cue(rf, LabCueKind.Jump, S(rf, 70.6f, 20.9f));        // up at the block wall's face
            Lane(rf, 0f, 0f);
            Lane(rf, S(rf, 70f, 21.2f), 0f);
            Lane(rf, S(rf, 74f, 19.4f), 0.15f);
            Lane(rf, S(rf, 86f, 19.4f), 0.15f);
            Lane(rf, S(rf, 88f, 19f), 0f);
            Cue(rf, LabCueKind.WallJump, S(rf, 85.4f, 19.4f));
            Cue(rf, LabCueKind.DoubleJumpAtApex, S(rf, 86.2f, 19.3f));
            Beat(rf, CourseBeatKind.Mantle, "House 1's porch roof (jump + mantle, 2.9)", S(rf, 10.5f, 48f), 2f, 2.9f - 1.25f);
            Beat(rf, CourseBeatKind.Mantle, "House 1's eaves from the porch (jump + mantle, 2.3)", S(rf, 10.5f, 46f), 2f, Eaves - 2.9f - 1.25f + 0.4f);
            Beat(rf, CourseBeatKind.Gap, "Passage A (roof to roof)", S(rf, 22.25f, 37f), 4.5f, 0f);
            Beat(rf, CourseBeatKind.Gap, "Passage B (roof to roof)", S(rf, 42.75f, 37f), 4.5f, 0f);
            Beat(rf, CourseBeatKind.Wallrun, "The unfinished wing's block wall", S(rf, 72f, 19.4f), 14f, 3.6f);
            Beat(rf, CourseBeatKind.Mantle, "Builders' crate", S(rf, 98f, 18.5f), 2f, 1.6f);
            Beat(rf, CourseBeatKind.Mantle, "Upper lot step 1 (from the crate)", S(rf, 100f, 18.5f), 4f, 1.6f);
            Beat(rf, CourseBeatKind.Mantle, "Upper lot step 2 (from step 1)", S(rf, 102f, 20f), 4f, 1.4f);

            // Under: through Walt's half-open door (slide), out his back door, down the creek bank, along the creek bed,
            // north up the channel and through the culvert under the street (slide), the pond, out by its step, up the
            // hill path's mantle stair.
            var un = NewRoute("Under", ChapterUnder, "under", 25f,
                (8.5f, 64.5f), (8.5f, 79f), (15f, 80f), (15f, 99f), (20f, 106f), (89f, 106f), (89f, 44f), (95f, 40f),
                (99.5f, 44f), (102f, 45f), (102f, 26.5f));
            Cue(un, LabCueKind.Crouch, S(un, 8.5f, 68.6f), S(un, 8.5f, 71.2f));
            Cue(un, LabCueKind.Crouch, S(un, 89f, 65.5f), S(un, 89f, 50.5f));
            Beat(un, CourseBeatKind.Slide, "Walt's half-open door", S(un, 8.5f, 70f), 0.1f, 1.15f);
            Beat(un, CourseBeatKind.Slide, "The culvert", S(un, 89f, 64f), 12f, 1.15f);
            Beat(un, CourseBeatKind.Mantle, "The pond's step", S(un, 96f, 40f), 2f, 1.4f);
            Beat(un, CourseBeatKind.Mantle, "Out of the pond", S(un, 98f, 41f), 2f, 0.6f);
            for (int i = 0; i < 3; i++) Beat(un, CourseBeatKind.Mantle, "Hill terrace " + (i + 1), S(un, 102f, 42f - 6f * i), 6f, 1.5f);
        }
    }
}
