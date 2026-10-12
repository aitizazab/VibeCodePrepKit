using System;
using System.Collections.Generic;
using System.Numerics;
using JamKit.Movement;

namespace JamKit.DeepBlock
{
    /// <summary>A place on the hill a camera or a test looks from: the eye and what it looks at.</summary>
    public sealed class HillView
    {
        public string name;
        public Vector3 eye, look;
    }

    /// <summary>A spot the story uses (a memory, a character, a trigger box).</summary>
    public sealed class HillSpot
    {
        public string id, name;
        public Vector3 position;
        /// <summary>Its trigger box (the feet inside it), centred on <see cref="position"/>; zero for a point.</summary>
        public Vector3 size;
        public float yaw;

        public bool Contains(Vector3 feet)
        {
            var d = feet - position;
            return Math.Abs(d.X) <= size.X * 0.5f && Math.Abs(d.Y) <= size.Y * 0.5f && Math.Abs(d.Z) <= size.Z * 0.5f;
        }
    }

    /// <summary>One band of the hill (docs/redesign/world.md "The bands"): a fall inside it lands you on its floor, and the
    /// way back up is the band's climb. <see cref="floorY"/> is its lowest walkable level.</summary>
    public sealed class HillBand
    {
        public string id, name;
        public float floorY;
        /// <summary>Where you start again if you ever fall out of the world from this band (it costs the climb).</summary>
        public Vector3 recover;
        public float recoverYaw;
    }

    /// <summary>
    /// Larch Hill (docs/redesign/world.md): the one hillside neighbourhood Nights 1, 2 and 3 share, as data: every collider
    /// (ground tiles, retaining walls, fences, houses, buildings, props, tree trunks and limbs), the houses, trees, props and
    /// big buildings the editor draws on those colliders, the story's spots, the critical path's legs with their autopilot
    /// cues, the camera views, and the bands. The editor's DreamHillBuilder makes the scene from it; the core tests run the
    /// autopilot over the same boxes. Coordinates: X east, Z north (up the hill), Y up; the creek's north bank is Y 0.
    /// <list type="bullet">
    /// <item><b>The foot:</b> the far bank (the arrival, the Lindqvists' loop), the creek (the first double jump), Walt's
    /// bike shop (the first slide) and yard (Walt's argument), then <b>the back way</b> west through the Row A yards: the
    /// lessons (mantle, wall-run, slide, jump-and-mantle, the wall-jump chimney up to Mill Street).</item>
    /// <item><b>The lower band:</b> Mill Street and Larch Green (the rest and the view of the house), Back Lane B west (the
    /// first combination test: roofs, the ditch, the shutter on the loop's timer, the wall-run-slide-mantle line), the old
    /// oak up to Rowan Street, and Rowan Street's block party east to the Lasname house: <b>the Rewind</b>, the set piece.</item>
    /// <item><b>The middle band</b> (Night 2's escape and after): the terraces (Chapel Row, School Lane, Bell Row, Upper Row)
    /// with their retaining walls, the Larch Steps, St. Anne's church, the primary school, Hill Road and its Route 9 bus.</item>
    /// <item><b>The top</b> (Night 3): Crest Road's shops and St. Brigid's hospital crowning the hill.</item>
    /// </list>
    /// </summary>
    public sealed partial class LarchHill
    {
        public readonly List<BlockPiece> pieces = new List<BlockPiece>();
        public readonly List<BlockRoute> routes = new List<BlockRoute>();
        public readonly List<HillView> views = new List<HillView>();
        public readonly List<HillSpot> spots = new List<HillSpot>();
        public readonly List<HillHouse> houses = new List<HillHouse>();
        public readonly List<HillTree> trees = new List<HillTree>();
        public readonly List<HillProp> props = new List<HillProp>();
        public readonly List<HillBuilding> buildings = new List<HillBuilding>();
        public readonly List<HillBand> bands = new List<HillBand>();

        /// <summary>The way in: from the arrival over the creek, under the garage door, through the shop to Walt.</summary>
        public BlockRoute wayIn;
        /// <summary>Night 1's critical path after Walt, leg by leg (the back way, the green, the back lane, the oak, the
        /// Rewind); each is also in <see cref="routes"/>.</summary>
        public readonly List<BlockRoute> night1 = new List<BlockRoute>();
        /// <summary>Night 2's line through the middle band, from the escape's exit to the bus stop.</summary>
        public BlockRoute escapeLine;
        /// <summary>Where the night starts (feet) and which way she faces.</summary>
        public Vector3 arrival;
        public float arrivalYaw;
        /// <summary>The house's root: its front door's threshold (<see cref="HillAnchors.HouseOrigin"/>).</summary>
        public Vector3 houseOrigin;
        /// <summary>The landmark: a point on the hospital's tower near its sign (what "visible from everywhere" means).</summary>
        public Vector3 hospitalPoint;
        /// <summary>A point on the Lasname house's upper storey (the near goal of Night 1).</summary>
        public Vector3 housePoint;

        /// <summary>The Rewind (Night 1's set piece): the wave sweeps east along Rowan Street from <see cref="RewindFromX"/>
        /// at <see cref="RewindSpeed"/> m/s, starting <see cref="RewindDelay"/> s after she climbs onto the street; it catches
        /// her when it passes her feet. Every catch slows the next wave a little (never below <see cref="RewindMinSpeed"/>).</summary>
        public const float RewindFromX = -116f, RewindSpeed = 7.2f, RewindMinSpeed = 5.6f, RewindSlowPerCatch = 0.4f, RewindDelay = 2.5f;
        /// <summary>Where the Rewind ends (the Lasnames' front gate): passing this X is safe.</summary>
        public const float RewindSafeX = 22f;

        // ---------- Levels (Y of the ground) ----------
        public const float FarBankY = 0f, FarRiseY = 3f, FootY = 0f, MillY = HillAnchors.MillY, LaneY = HillAnchors.BackLaneY, RowanY = HillAnchors.RowanY;
        public const float CreekBed = -4.6f, CreekWater = -3.4f;
        /// <summary>The world's edges.</summary>
        public const float WestX = -140f, EastX = 140f, SouthZ = -100f, NorthZ = 404f;

        // ---------- The creek and the foot ----------
        public const float CreekZ0 = -16f, CreekZ1 = -8f;
        /// <summary>Walt's bike shop and yard (X 84-110), the apron in front (Z -8 to -2).</summary>
        public const float WaltX0 = 84f, WaltX1 = 110f, ShopZ0 = -2f, ShopZ1 = 14f, YardZ1 = 40f;
        /// <summary>The half-open garage door: its bottom edge's height (slide under it).</summary>
        public const float GarageDoorGap = 1.15f;
        /// <summary>The Row A yards (the back way), between the Row A houses' backs and Mill Street's wall.</summary>
        public const float YardZ0 = 14f, MillWallZ0 = 40f, MillWallZ1 = 42f;

        // ---------- Mill Street, the green, Back Lane B, Rowan Street ----------
        public const float MillRoadZ0 = 44f, MillRoadZ1 = 52f, RowBZ0 = 54f, LaneZ0 = 94f, LaneZ1 = 100f, RowanWallZ1 = 102f;
        public const float RowanRoadZ0 = 105f, RowanRoadZ1 = 113f, RowanNorthZ = 116f;
        public const float GreenX0 = -8f, GreenX1 = 8f;

        // ---------- Story spots (ids) ----------
        public const string SpotWalt = "walt", SpotWaltAside = "walt_aside", SpotYard = "yard", SpotLedger = "ledger", SpotPaper = "paper",
            SpotSprinkler = "sprinkler", SpotNeighbour = "neighbour", SpotDog = "dog", SpotBallFrom = "ball_from", SpotBallTo = "ball_to",
            SpotLoopZone = "loop_zone", SpotGarageDoor = "garage_door", SpotCreekEdge = "creek_edge", SpotCreekWater = "creek_water",
            SpotGate = "garden_gate", SpotBarbecue = "barbecue", SpotElias = "elias", SpotWindow = "window", SpotDoor = "front_door",
            SpotPorchLight = "porch_light", SpotPatchedBike = "patched_bike", SpotLessonMantle = "lesson_mantle", SpotLessonWallrun = "lesson_wallrun",
            SpotLessonSlide = "lesson_slide", SpotLessonClimb = "lesson_climb", SpotLessonChimney = "lesson_chimney", SpotMillStreet = "mill_street",
            SpotGreen = "green", SpotLane = "back_lane", SpotDitch = "ditch", SpotShutter = "shutter", SpotOak = "oak", SpotTreehouse = "treehouse",
            SpotRowan = "rowan", SpotNail = "the_nail", SpotPool = "pool", SpotChatter = "chatter", SpotParty = "party";

        public const string ChapterArrival = "The creek", ChapterWalt = "Walt", ChapterBackWay = "The back way", ChapterLane = "Back Lane B",
            ChapterOak = "The old oak", ChapterRewind = "The Rewind", ChapterHouse = "The house", ChapterEscapeLine = "Night 2: the middle band";

        public static LarchHill Build()
        {
            var h = new LarchHill();
            h.Make();
            return h;
        }

        /// <summary>Walt's gate (a switch): before his argument, Walt sits on the low section of his yard's wall.</summary>
        public const int WaltGateSwitch = 0;
        /// <summary>The shutter over Back Lane B's arch (a switch the loop's timer works: open, then shut).</summary>
        public const int ShutterSwitch = 1;
        /// <summary>The old plank across the creek (a switch: there only when the bedside's "the creek" topic put it back,
        /// docs/redesign/day.md, DreamChanges n1_creek_plank): a walk across instead of the double jump.</summary>
        public const int CreekPlankSwitch = 2;
        public const int SwitchCount = 3;

        /// <summary>The switches for a moment of the night: Walt aside (his low wall open), the lane's shutter up, the plank.</summary>
        public static bool[] Gates(bool waltAside, bool shutterOpen = true, bool plank = false) => new[] { waltAside, shutterOpen, plank };

        /// <summary>The spot with this id, or null. No allocation (the game asks several times a frame: the Perf agent's
        /// find, a lambda per call before).</summary>
        public HillSpot Spot(string id)
        {
            for (int i = 0; i < spots.Count; i++)
                if (string.Equals(spots[i].id, id, StringComparison.Ordinal)) return spots[i];
            return null;
        }
        public HillBand Band(string id) => bands.Find(b => b.id == id);

        // ---------- The shutter's timer (the loop's clock) ----------

        /// <summary>The shutter's cycle (s): up for <see cref="ShutterOpenFor"/>, coming down, shut, going up.</summary>
        public const float ShutterPeriod = 12f, ShutterOpenFor = 5f, ShutterMoveFor = 1.6f;
        /// <summary>The shutter's bottom edge above the lane at <paramref name="t"/> s into the loop (3.4 open .. 0 shut).</summary>
        public static float ShutterGap(float t)
        {
            float p = t % ShutterPeriod;
            if (p < 0f) p += ShutterPeriod;
            const float open = ShutterOpening;
            if (p < ShutterOpenFor) return open;
            if (p < ShutterOpenFor + ShutterMoveFor) return open * (1f - (p - ShutterOpenFor) / ShutterMoveFor);
            float shutUntil = ShutterPeriod - ShutterMoveFor;
            if (p < shutUntil) return 0f;
            return open * (p - shutUntil) / ShutterMoveFor;
        }
        public const float ShutterOpening = 3.2f;

        // ---------- Building helpers ----------

        string _layer = "";

        BlockPiece Box(string name, BlockKind kind, float x0, float z0, float x1, float z1, float y0, float y1,
            bool runnable = true, bool usable = false, bool solid = true, string style = "")
        {
            var mn = new Vector3(Math.Min(x0, x1), Math.Min(y0, y1), Math.Min(z0, z1));
            var mx = new Vector3(Math.Max(x0, x1), Math.Max(y0, y1), Math.Max(z0, z1));
            var p = new BlockPiece { name = name, kind = kind, center = (mn + mx) * 0.5f, size = mx - mn, runnable = runnable, usable = usable, solid = solid, layer = _layer, style = style };
            pieces.Add(p);
            return p;
        }

        /// <summary>A ground tile with its top at <paramref name="top"/> (solid down to 8 m under the foot). Tiles never
        /// overlap; a tile's sides that stand above its neighbours are drawn as retaining walls (<paramref name="side"/>).</summary>
        BlockPiece Ground(string name, float x0, float z0, float x1, float z1, float top, string surface = "grass", string side = "stone_retaining")
        {
            var p = Box(name, BlockKind.Ground, x0, z0, x1, z1, -8f, top, runnable: true, style: surface + "|" + side);
            return p;
        }

        /// <summary>A tile whose top slopes along X from <paramref name="atX0"/> at x0 to <paramref name="atX1"/> at x1.</summary>
        BlockPiece RampX(string name, float x0, float z0, float x1, float z1, float atX0, float atX1, string surface = "asphalt", string side = "stone_retaining")
        {
            var p = Box(name, BlockKind.Ground, x0, z0, x1, z1, -8f, Math.Max(atX0, atX1), style: surface + "|" + side);
            p.rampAxis = 1;
            p.rampLow = x0 < x1 ? atX0 : atX1;
            p.rampHigh = x0 < x1 ? atX1 : atX0;
            return p;
        }

        /// <summary>A tile whose top slopes along Z from <paramref name="atZ0"/> at z0 to <paramref name="atZ1"/> at z1 (a
        /// street up the hill, a flight of steps: surface "steps").</summary>
        BlockPiece RampZ(string name, float x0, float z0, float x1, float z1, float atZ0, float atZ1, string surface = "asphalt", string side = "stone_retaining")
        {
            var p = Box(name, BlockKind.Ground, x0, z0, x1, z1, -8f, Math.Max(atZ0, atZ1), style: surface + "|" + side);
            p.rampAxis = 2;
            p.rampLow = z0 < z1 ? atZ0 : atZ1;
            p.rampHigh = z0 < z1 ? atZ1 : atZ0;
            return p;
        }

        /// <summary>A fence or wall along X (constant Z), standing on <paramref name="y0"/>.</summary>
        BlockPiece FenceX(string name, string style, float x0, float x1, float z, float y0, float height, float thick = 0.12f, bool runnable = true)
        {
            var kind = style == "hedge" ? BlockKind.Hedge : style.StartsWith("brick") || style.Contains("wall") || style.StartsWith("stone") ? BlockKind.BrickWall
                : style == "picket" ? BlockKind.Picket : BlockKind.Fence;
            return Box(name, kind, x0, z - thick * 0.5f, x1, z + thick * 0.5f, y0, y0 + height, runnable: runnable, style: style);
        }

        /// <summary>A fence or wall along Z (constant X).</summary>
        BlockPiece FenceZ(string name, string style, float x, float z0, float z1, float y0, float height, float thick = 0.12f, bool runnable = true)
        {
            var kind = style == "hedge" ? BlockKind.Hedge : style.StartsWith("brick") || style.Contains("wall") || style.StartsWith("stone") ? BlockKind.BrickWall
                : style == "picket" ? BlockKind.Picket : BlockKind.Fence;
            return Box(name, kind, x - thick * 0.5f, z0, x + thick * 0.5f, z1, y0, y0 + height, runnable: runnable, style: style);
        }

        HillSpot AddSpot(string id, string name, float x, float y, float z, float sx = 0f, float sy = 0f, float sz = 0f, float yaw = 0f)
        {
            var s = new HillSpot { id = id, name = name, position = new Vector3(x, y, z), size = new Vector3(sx, sy, sz), yaw = yaw };
            spots.Add(s);
            return s;
        }

        /// <summary>Adds a house and its colliders.</summary>
        HillHouse House(HillHouse h)
        {
            houses.Add(h);
            int from = pieces.Count;
            HillColliders.House(h, pieces, _layer);
            Mark(from, "@house");
            return h;
        }

        HillBuilding Building(HillBuilding b)
        {
            buildings.Add(b);
            int from = pieces.Count;
            HillColliders.Building(b, pieces, _layer);
            Mark(from, "@building");
            return b;
        }

        /// <summary>Marks the pieces from <paramref name="from"/> on as drawn by someone else (a house's, a building's, a
        /// prop's or a tree's drawing): the builder gives them colliders only.</summary>
        void Mark(int from, string owner)
        {
            for (int i = from; i < pieces.Count; i++) pieces[i].style = owner;
        }

        HillProp Prop(HillPropKind kind, float x, float y, float z, float yaw = 0f, int variant = 0, string tag = "")
        {
            var p = new HillProp { kind = kind, position = new Vector3(x, y, z), yaw = yaw, variant = variant, tag = tag };
            props.Add(p);
            var c = HillColliders.Prop(p, _layer);
            if (c != null) { c.style = "@prop"; pieces.Add(c); }
            return p;
        }

        HillTree Tree(string name, float x, float y, float z, int kind = 0, float height = 9f, float canopy = 3.5f, int seed = 0)
        {
            var t = new HillTree { name = name, foot = new Vector3(x, y, z), kind = kind, height = height, canopy = canopy, trunk = kind == 4 ? 0.65f : kind == 2 ? 0.18f : 0.3f, seed = seed };
            trees.Add(t);
            var trunk = HillColliders.Trunk(t, _layer);
            trunk.style = "@tree";
            pieces.Add(trunk);
            return t;
        }

        /// <summary>The top of the solid ground under (x, z), at or below <paramref name="maxY"/>.</summary>
        public float TopAt(float x, float z, float maxY = 300f, bool[] open = null)
        {
            open = open ?? Gates(true);
            float best = -50f;
            foreach (var b in pieces)
            {
                if (!b.solid || !b.PresentOn(open)) continue;
                if (x < b.Min.X || x > b.Max.X || z < b.Min.Z || z > b.Max.Z) continue;
                float t = b.TopAt(x, z);
                if (t <= maxY && t > best) best = t;
            }
            return best;
        }

        /// <summary>A point is inside a solid piece (ramps by their sloped top): what blocks a line of sight.</summary>
        public bool Solid(Vector3 p, bool[] open = null) => SolidPiece(p, open) != null;

        public BlockPiece SolidPiece(Vector3 p, bool[] open = null)
        {
            open = open ?? Gates(true);
            foreach (var b in pieces)
            {
                if (!b.BlocksSight || b.kind == BlockKind.Boundary || !b.PresentOn(open)) continue;
                if (p.X <= b.Min.X || p.X >= b.Max.X || p.Z <= b.Min.Z || p.Z >= b.Max.Z || p.Y <= b.Min.Y) continue;
                if (p.Y < b.TopAt(p.X, p.Z)) return b;
            }
            return null;
        }

        public bool LineClear(Vector3 a, Vector3 b, float step = 0.5f) => Blocker(a, b, step) == null;

        public string Blocker(Vector3 a, Vector3 b, float step = 0.5f)
        {
            float len = Vector3.Distance(a, b);
            int n = Math.Max(1, (int)(len / step));
            for (int i = 1; i < n; i++)
            {
                var p = Vector3.Lerp(a, b, i / (float)n);
                var piece = SolidPiece(p);
                if (piece != null) return piece.name + " at (" + p.X.ToString("0") + ", " + p.Y.ToString("0.0") + ", " + p.Z.ToString("0") + ")";
            }
            return null;
        }

        /// <summary>The landmark (the hospital) is in sight from <paramref name="eye"/>: any of a few points on its tower and
        /// its upper floors.</summary>
        public bool SeesHospital(Vector3 eye)
        {
            foreach (var dx in new[] { 0f, -18f, 18f, -40f, 40f })
                foreach (var dy in new[] { 0f, -8f })
                    if (LineClear(eye, hospitalPoint + new Vector3(dx, dy, 0f), 1f)) return true;
            return false;
        }

        /// <summary>The house is in sight from <paramref name="eye"/> (its upper storey, the turret or the roof).</summary>
        public bool SeesHouse(Vector3 eye)
        {
            foreach (var dx in new[] { 0f, -4f, 4f })
                foreach (var dy in new[] { 0f, 2.5f })
                    if (LineClear(eye, housePoint + new Vector3(dx, dy, 0f), 0.5f)) return true;
            return false;
        }

        // ---------- The hill ----------

        void Make()
        {
            houseOrigin = HillAnchors.HouseOrigin;
            housePoint = houseOrigin + new Vector3(0f, 5.5f, 0.4f);
            hospitalPoint = new Vector3(0f, HillAnchors.SummitY + 34f, 322f + 2f);
            MakeFarBank();
            MakeCreek();
            MakeWalt();
            MakeRowA();
            MakeMillStreet();
            MakeGreen();
            MakeRowB();
            MakeBackLane();
            MakeRowan();
            MakeLasnames();
            MakeMiddle();
            MakeTop();
            MakeEdges();
            MakeStory();
            MakeRoutes();
            MakeViews();
            MakeBands();
        }
    }
}
