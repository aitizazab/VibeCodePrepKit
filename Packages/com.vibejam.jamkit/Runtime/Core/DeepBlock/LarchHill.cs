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

    /// <summary>
    /// Night 1's street (docs/NIGHTS-1-2.md "The dive: Larch Street, on a hill"; docs/LARCH-HILL.md): layout A of the
    /// deep block laid on a hill, as data. The creator didn't love the flat deep block ("it didn't reveal how large the
    /// setting was") and offered "work vertically and set it on a hill", so:
    /// <list type="bullet">
    /// <item><b>The bottom:</b> Nadia arrives on the creek's south bank. Across the 8 m creek (the first double jump) is
    /// <b>Walt's bike shop</b>; its half-open garage door is the first slide; through the shop is its yard, where Walt
    /// stands in the gate to the hill (<see cref="WaltGateSwitch"/>: shut until his argument).</item>
    /// <item><b>The hill:</b> four terraces 12 m apart (the foot at 0, then 12, 24 and the top at 36) climbing north. From
    /// the foot square behind Walt, layout A's four routes go up: <b>the road</b> in two switchbacks (three legs along the
    /// terraces, 10° at most), <b>the garden stairs and back lanes</b> (three hedged flights, a garden wall at each lane),
    /// <b>the rooftops</b> (a row of houses stepping up the hill, each roof 3.6 m above the last: a chain of double jumps)
    /// and <b>the creek ravine</b> (down from the creek lane, a slide through the culvert under the road's first
    /// hairpin, then up the falls).</item>
    /// <item><b>The top:</b> the Lasname house with its tower (the round 5 house, built by its own builder) on a knoll,
    /// the landmark you see from everywhere; its garden gate is where the night ends.</item>
    /// </list>
    /// Coordinates are the world's: X east, Z north (up the hill), Y up; the foot is at Y 0.
    /// </summary>
    public sealed class LarchHill
    {
        public readonly List<BlockPiece> pieces = new List<BlockPiece>();
        public readonly List<BlockRoute> routes = new List<BlockRoute>();
        public readonly List<HillView> views = new List<HillView>();
        public readonly List<HillSpot> spots = new List<HillSpot>();
        /// <summary>The way in: from the arrival over the creek, through the shop, to Walt (its own autopilot course).</summary>
        public BlockRoute wayIn;
        /// <summary>Where the night starts (feet) and which way she faces.</summary>
        public Vector3 arrival;
        public float arrivalYaw;
        /// <summary>The house's origin: its front door's threshold at ground level, facing -Z (down the hill).</summary>
        public Vector3 houseOrigin;
        /// <summary>The landmark: a point on the tower's upper storey (what "visible from everywhere" is tested against).</summary>
        public Vector3 towerPoint;
        /// <summary>The beacon's foot (on the tower); it stands <see cref="BeaconHeight"/> m tall.</summary>
        public Vector3 beaconFoot;
        public const float BeaconHeight = 110f;

        /// <summary>The landmark is in sight from <paramref name="eye"/>: the tower itself, or its beacon above the roofs.</summary>
        public bool SeesLandmark(Vector3 eye)
        {
            if (LineClear(eye, towerPoint)) return true;
            for (float up = 6f; up <= BeaconHeight; up += 13f)
                if (LineClear(eye, beaconFoot + new Vector3(0f, up, 0f), 1f)) return true;
            return false;
        }

        // Levels and terraces
        public const float L0 = 0f, L1 = 12f, L2 = 24f, Top = 36f, Knoll = 40f, Rise = 12f;
        public const float TerraceDepth = 34f;
        /// <summary>The terraces' south edges (their risers): the foot runs Z 0-34, then 34-68, 68-102, the top from 102.</summary>
        public const float Z1 = 34f, Z2 = 68f, Z3 = 102f, ZEnd = 176f;
        public const float WestX = -80f, EastX = 58f;
        /// <summary>The middle strip (the road and the terraced houses) runs X -42 to 48; the road's legs X -30 to 36.</summary>
        public const float MidX0 = -42f, MidX1 = 48f, LegX0 = -30f, LegX1 = 36f;
        /// <summary>The ravine: X 50-58, with its wall at 48-50.</summary>
        public const float RavX0 = 50f, RavX1 = 58f, RavineBed = -2f, CreekBed = -2.5f;
        /// <summary>The creek: Z -8 to 0, 8 m wide (the first double jump).</summary>
        public const float CreekZ0 = -8f, CreekZ1 = 0f;
        /// <summary>The road's legs are 10 m wide; the culvert under the first hairpin is 1.15 m clear and 10 m long.</summary>
        public const float CulvertClear = 1.15f;
        public const float HouseFloor = 4.4f, HouseTop = 6.2f;
        /// <summary>The lookout at the top's edge (the vista): its west end and its top.</summary>
        public const float LookoutX0 = -16f, LookoutTop = Top + 4.8f;

        /// <summary>Walt's gate (the switch index of the piece that shuts the way up until his argument).</summary>
        public const int WaltGateSwitch = 0;
        public const int SwitchCount = 1;

        // The rooftops: a shed and nine houses stepping up, Z 35 + 7j to 39 + 7j, each roof 3.6 m above the last.
        public const float RoofX0 = -62f, RoofX1 = -44f, ShedRoof = 4.25f, RoofStep = 3.6f, RoofDepth = 4f, RoofPitch = 7f;
        public const int RoofHouses = 9;

        // The stairs: X -80 to -64.
        public const float StairX0 = -80f, StairX1 = -64f;

        public const string RouteRoad = "The road", RouteStairs = "The garden stairs", RouteRoofs = "The rooftops", RouteRavine = "The creek ravine";
        public const string ChapterArrival = "The creek", ChapterWalt = "Walt", ChapterRoad = "The road", ChapterStairs = "The garden stairs",
            ChapterRoofs = "The rooftops", ChapterRavine = "The creek ravine", ChapterTop = "The top";

        // Story spots (ids)
        public const string SpotWalt = "walt", SpotWaltAside = "walt_aside", SpotYard = "yard", SpotLedger = "ledger", SpotPaper = "paper",
            SpotSprinkler = "sprinkler", SpotNeighbour = "neighbour", SpotDog = "dog", SpotBallFrom = "ball_from", SpotBallTo = "ball_to",
            SpotLoopZone = "loop_zone", SpotGarageDoor = "garage_door", SpotCreekEdge = "creek_edge", SpotTopArrive = "top", SpotGate = "garden_gate",
            SpotBarbecue = "barbecue", SpotElias = "elias", SpotWindow = "window", SpotDoor = "front_door", SpotPorchLight = "porch_light",
            SpotFoot = "foot", SpotChatter = "chatter";

        public static LarchHill Build()
        {
            var h = new LarchHill();
            h.Make();
            return h;
        }

        /// <summary>The switches for a moment of the night: Walt's gate shut (index 0 false) until he steps aside.</summary>
        public static bool[] Gates(bool waltAside) => new[] { waltAside };

        public HillSpot Spot(string id) => spots.Find(s => s.id == id);

        // ---------- Helpers ----------

        string _layer = "";

        BlockPiece Box(string name, BlockKind kind, float x0, float z0, float x1, float z1, float y0, float y1,
            bool runnable = false, bool usable = false, bool solid = true)
        {
            var mn = new Vector3(Math.Min(x0, x1), Math.Min(y0, y1), Math.Min(z0, z1));
            var mx = new Vector3(Math.Max(x0, x1), Math.Max(y0, y1), Math.Max(z0, z1));
            var p = new BlockPiece { name = name, kind = kind, center = (mn + mx) * 0.5f, size = mx - mn, runnable = runnable, usable = usable, solid = solid, layer = _layer };
            pieces.Add(p);
            return p;
        }

        /// <summary>Solid ground with its top at <paramref name="top"/> (down to 6 m under the foot).</summary>
        BlockPiece Ground(string name, float x0, float z0, float x1, float z1, float top) =>
            Box(name, BlockKind.Ground, x0, z0, x1, z1, Math.Min(top - 2f, -6f), top);

        /// <summary>A slab whose top runs from <paramref name="atX0"/> at x0 to <paramref name="atX1"/> at x1 (a road leg).</summary>
        BlockPiece RampX(string name, BlockKind kind, float x0, float z0, float x1, float z1, float atX0, float atX1)
        {
            var p = Box(name, kind, x0, z0, x1, z1, -6f, Math.Max(atX0, atX1));
            p.rampAxis = 1;
            p.rampLow = x0 < x1 ? atX0 : atX1;
            p.rampHigh = x0 < x1 ? atX1 : atX0;
            return p;
        }

        /// <summary>A slab whose top runs from <paramref name="atZ0"/> at z0 to <paramref name="atZ1"/> at z1 (a stair flight).</summary>
        BlockPiece RampZ(string name, BlockKind kind, float x0, float z0, float x1, float z1, float atZ0, float atZ1)
        {
            var p = Box(name, kind, x0, z0, x1, z1, -6f, Math.Max(atZ0, atZ1));
            p.rampAxis = 2;
            p.rampLow = z0 < z1 ? atZ0 : atZ1;
            p.rampHigh = z0 < z1 ? atZ1 : atZ0;
            return p;
        }

        /// <summary>A wall along X (constant Z) or Z (constant X), <paramref name="thick"/> m thick, from y0 to y1.</summary>
        BlockPiece WallX(string name, BlockKind kind, float x0, float x1, float z, float y0, float y1, float thick = 0.4f) =>
            Box(name, kind, x0, z - thick * 0.5f, x1, z + thick * 0.5f, y0, y1);

        BlockPiece WallZ(string name, BlockKind kind, float x, float z0, float z1, float y0, float y1, float thick = 0.4f) =>
            Box(name, kind, x - thick * 0.5f, z0, x + thick * 0.5f, z1, y0, y1);

        HillSpot AddSpot(string id, string name, float x, float y, float z, float sx = 0f, float sy = 0f, float sz = 0f, float yaw = 0f)
        {
            var s = new HillSpot { id = id, name = name, position = new Vector3(x, y, z), size = new Vector3(sx, sy, sz), yaw = yaw };
            spots.Add(s);
            return s;
        }

        /// <summary>The top of the solid ground under (x, z), at or below <paramref name="maxY"/> (with the gates open).</summary>
        public float TopAt(float x, float z, float maxY = 200f, bool[] open = null)
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

        /// <summary>The piece a point is inside (ramps by their sloped top), or null.</summary>
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

        /// <summary>A clear line between two points (sampled every <paramref name="step"/> m; ramps by their tops).</summary>
        public bool LineClear(Vector3 a, Vector3 b, float step = 0.5f) => Blocker(a, b, step) == null;

        /// <summary>What blocks the line between two points ("" when clear): the piece's name and where.</summary>
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

        // ---------- The hill ----------

        void Make()
        {
            MakeBottom();
            MakeTerraces();
            MakeRoad();
            MakeStairs();
            MakeRoofs();
            MakeRavine();
            MakeTop();
            MakeBounds();
            MakeStory();
            MakeRoutes();
            MakeViews();
        }

        void MakeBottom()
        {
            _layer = "bottom";
            // The south bank (where she arrives), the creek, and the shop's apron on the far side.
            Ground("The south bank", WestX, -44f, 40f, CreekZ0, L0);
            Ground("The creek bed", WestX, CreekZ0, RavX1, CreekZ1, CreekBed);
            Box("The creek (water)", BlockKind.Water, WestX, CreekZ0, RavX1, CreekZ1, CreekBed, CreekBed + 0.6f, solid: false);
            // The neighbours' house on the far bank, its front garden toward the creek (the loop plays in it).
            _layer = "farbank";
            Box("The neighbours' house (the far bank)", BlockKind.House, -40f, -38f, -28f, -29f, L0, L0 + HouseFloor);
            Box("The neighbours' house (the far bank) (roof)", BlockKind.Roof, -40.3f, -38.3f, -27.7f, -28.7f, L0 + HouseFloor, L0 + HouseTop);
            _layer = "bottom";
            // A grate across the creek and a fence on the bank at X 40: the creek can't take her round Walt.
            Box("The grate (across the creek)", BlockKind.Fence, 39.6f, CreekZ0, 40.4f, CreekZ1, CreekBed, 6f);
            WallZ("The bank hedge", BlockKind.Hedge, 40f, -44f, CreekZ0, L0, 6f, 1.2f);

            // The foot's ground (Z 0-34) across the middle strip, the shop's apron and the yard; a filler west of the shop.
            Ground("The foot (ground)", MidX0, CreekZ1, RavX0, Z1, L0);
            Ground("The shop's apron", -62f, CreekZ1, -44f, 24f, L0);
            Box("The boatshed (west of the shop)", BlockKind.BrickWall, WestX, CreekZ1, -62f, 24f, -6f, 5.5f);
            Ground("The foot square", WestX, 24f, MidX0, Z1, L0);
            // The flood wall along the creek's north bank: drawn 1.2 m (so the climb shows from the far bank), held to 5 m
            // by the dream above it, so the shop's apron is the only landing (stills of 2026-10-12: at 5 m it hid the foot).
            Box("The flood wall", BlockKind.BrickWall, -44f, CreekZ1, RavX0, CreekZ1 + 1f, L0, 1.2f);
            Box("The flood wall (held higher by the dream)", BlockKind.Boundary, -44f, CreekZ1, RavX0, CreekZ1 + 1f, 1.2f, 5f);

            // Walt's bike shop: 18 x 12 m, one storey, its garage door half open onto the apron, a back door to the yard.
            _layer = "shop";
            const float sx0 = -62f, sx1 = -44f, sz0 = 3f, sz1 = 15f, sh = 4.5f;
            Box("The shop (front wall, west)", BlockKind.Shop, sx0, sz0, -56f, sz0 + 0.3f, L0, sh);
            Box("The shop (front wall, east)", BlockKind.Shop, -50f, sz0, sx1, sz0 + 0.3f, L0, sh);
            Box("The shop (over the garage door)", BlockKind.Shop, -56f, sz0, -50f, sz0 + 0.3f, 4.2f, sh);
            Box("The garage door (half open)", BlockKind.Door, -56f, sz0 + 0.02f, -50f, sz0 + 0.28f, CulvertClear, 4.2f, usable: true);
            Box("The shop (west wall)", BlockKind.Shop, sx0, sz0, sx0 + 0.3f, sz1, L0, sh);
            Box("The shop (east wall)", BlockKind.Shop, sx1 - 0.3f, sz0, sx1, sz1, L0, sh);
            Box("The shop (back wall, west)", BlockKind.Shop, sx0, sz1 - 0.3f, -50f, sz1, L0, sh);
            Box("The shop (back wall, east)", BlockKind.Shop, -47.5f, sz1 - 0.3f, sx1, sz1, L0, sh);
            Box("The shop (over the back door)", BlockKind.Shop, -50f, sz1 - 0.3f, -47.5f, sz1, 2.4f, sh);
            Box("The shop (roof)", BlockKind.Roof, sx0 - 0.3f, sz0 - 0.3f, sx1 + 0.3f, sz1 + 0.3f, sh, sh + 0.3f);
            Box("The workbench", BlockKind.Deck, sx0 + 0.4f, 6f, sx0 + 2.2f, 12f, L0, 0.9f);
            Box("Walt's yard bench", BlockKind.Deck, -60f, 20.4f, -57f, 21.6f, L0, 0.45f);
            // The yard behind the shop: walled, with the gate to the hill in its north wall (Walt stands in it).
            _layer = "yard";
            Box("The yard (east wall)", BlockKind.BrickWall, -44f, CreekZ1 + 1f, MidX0, 24f, L0, 5f);
            WallX("The yard (north wall, west)", BlockKind.BrickWall, sx0, -54f, 24f, L0, 5f, 0.6f);
            WallX("The yard (north wall, east)", BlockKind.BrickWall, -51f, sx1, 24f, L0, 5f, 0.6f);
            var gate = WallX("Walt's gate (shut until his argument)", BlockKind.Gate, -54f, -51f, 24f, L0, 5f, 0.6f);
            gate.switchIndex = WaltGateSwitch;
            gate.presentWhenOpen = false;
        }

        /// <summary>The middle strip's terraces: each 34 m deep, 12 m above the last: front gardens with a parapet over
        /// the drop, four houses, back gardens, and the road's leg along the riser behind them.</summary>
        void MakeTerraces()
        {
            _layer = "terraces";
            float[] houseX = { -26f, -15f, -11f, 0f, 4f, 15f, 19f, 30f };
            for (int k = 0; k < 3; k++)
            {
                float y = k * Rise, z = k * TerraceDepth;
                string level = k == 0 ? "the foot" : k == 1 ? "terrace 1" : "terrace 2";
                if (k > 0)
                {
                    // Terrace 1 stops where the east hairpin's road begins; terrace 2 where the west hairpin's does (no two
                    // tops in one plane: the road and the lawn would flicker).
                    if (k == 1) Ground("Terrace 1 (ground)", MidX0, z, LegX1, z + TerraceDepth, y);
                    else Ground("Terrace 2 (ground)", LegX0, z, MidX1, z + TerraceDepth, y);
                    // A parapet along the terrace's front edge (the drop to the road below), with the front gardens behind it.
                    WallX("Terrace " + k + " (parapet)", BlockKind.Picket, LegX0, MidX1 - 12f, z + 0.25f, y, y + 0.95f, 0.5f);
                }
                for (int i = 0; i < 4; i++)
                {
                    float hx0 = houseX[i * 2], hx1 = houseX[i * 2 + 1];
                    float hz0 = z + 8f, hz1 = z + 17f;
                    if (k == 0) { hz0 = 9f; hz1 = 18f; }
                    Box("House " + (i + 1) + ", " + level, BlockKind.House, hx0, hz0, hx1, hz1, y, y + HouseFloor);
                    Box("House " + (i + 1) + ", " + level + " (roof)", BlockKind.Roof, hx0 - 0.3f, hz0 - 0.3f, hx1 + 0.3f, hz1 + 0.3f, y + HouseFloor, y + HouseTop);
                    Box("House " + (i + 1) + ", " + level + " (porch)", BlockKind.Deck, (hx0 + hx1) * 0.5f - 2f, hz0 - 1.6f, (hx0 + hx1) * 0.5f + 2f, hz0, y, y + 0.25f);
                    // Fences between the back gardens.
                    if (i < 3) WallZ("Garden fence, " + level, BlockKind.Fence, (hx1 + houseX[i * 2 + 2]) * 0.5f, hz1, z + 24f, y, y + 1.8f, 0.15f);
                }
            }
        }

        /// <summary>The road: three legs (10 m wide) along the terraces' risers, two hairpins. Leg 1 climbs east from the
        /// foot square, the east hairpin (over the ravine's culvert) turns it north, leg 2 climbs west, the west hairpin, leg 3
        /// east onto the top.</summary>
        void MakeRoad()
        {
            _layer = "road";
            // Leg 1 (the foot): X -30 at 0 to X 36 at 12, along Z 24-34.
            RampX("The road, leg 1", BlockKind.Lane, LegX0, 24f, LegX1, Z1, L0, L1);
            // The east hairpin: an embankment X 36-48 from Z 24 (on the foot) north to leg 2, at 12.
            Box("The east hairpin (embankment)", BlockKind.Ground, LegX1, 24f, MidX1, Z1, -6f, L1);
            Box("The east hairpin", BlockKind.Ground, LegX1, Z1, MidX1, Z2, -6f, L1);
            // Leg 2 (terrace 1): X 36 at 12 to X -30 at 24, along Z 58-68.
            RampX("The road, leg 2", BlockKind.Lane, LegX0, 58f, LegX1, Z2, L2, L1);
            // The west hairpin: X -42 to -30 from Z 58 to leg 3, at 24.
            Box("The west hairpin (embankment)", BlockKind.Ground, MidX0, 58f, LegX0, Z2, -6f, L2);
            Box("The west hairpin", BlockKind.Ground, MidX0, Z2, LegX0, Z3, -6f, L2);
            // Leg 3 (terrace 2): X -30 at 24 to X 36 at 36, along Z 92-102; the east end onto the top.
            RampX("The road, leg 3", BlockKind.Lane, LegX0, 92f, LegX1, Z3, L2, Top);
            Box("The top of the road", BlockKind.Ground, LegX1, 92f, MidX1, Z3, -6f, Top);
        }

        /// <summary>The garden stairs and back lanes: three hedged flights (29-33°) up the west edge, each ending on a back lane
        /// (a landing running across the gardens) with a garden wall to climb (1.5 m) onto the next flight's foot.</summary>
        void MakeStairs()
        {
            _layer = "stairs";
            // Flights alternate sides: west (X -80 to -71), east (-73 to -64), west.
            RampZ("Garden stairs, flight 1", BlockKind.Ground, StairX0, Z1, -71f, 53f, L0, 10.5f);
            Ground("Gardens beside flight 1", -71f, Z1, StairX1, 53f, 10.5f);
            Ground("Back lane 1", StairX0, 53f, StairX1, 56f, 10.5f);
            Ground("Above back lane 1 (garden wall, 1.5)", StairX0, 56f, StairX1, 59f, L1);
            RampZ("Garden stairs, flight 2", BlockKind.Ground, -73f, 59f, StairX1, 78f, L1, 22.5f);
            Ground("Gardens beside flight 2", StairX0, 59f, -73f, 78f, 22.5f);
            Ground("Back lane 2", StairX0, 78f, StairX1, 81f, 22.5f);
            Ground("Above back lane 2 (garden wall, 1.5)", StairX0, 81f, StairX1, 84f, L2);
            RampZ("Garden stairs, flight 3", BlockKind.Ground, StairX0, 84f, -71f, 99f, L2, 34.5f);
            Ground("Gardens beside flight 3", -71f, 84f, StairX1, 99f, 34.5f);
            Ground("Back lane 3", StairX0, 99f, StairX1, Z3, 34.5f);
            // Hedges along the gardens beside the flights: the flights are hedged in.
            Box("Hedge (flight 1)", BlockKind.Hedge, -71f, Z1, -70.4f, 53f, 10.5f, 12.3f);
            Box("Hedge (flight 2)", BlockKind.Hedge, -73.6f, 59f, -73f, 78f, 22.5f, 24.3f);
            Box("Hedge (flight 3)", BlockKind.Hedge, -71f, 84f, -70.4f, 99f, 34.5f, 36.3f);
            // The wall between the stairs and the rooftops' row, stepping up with them.
            for (int j = 0; j < 7; j++)
            {
                float z0 = Z1 + j * 10f, z1 = Math.Min(Z3, z0 + 10f);
                float top = Math.Max(TopAt(-66f, z1 - 0.01f), TopAt(-66f, z0)) + 1.6f;
                WallZ("The stairs' east wall " + (j + 1), BlockKind.BrickWall, -63f, z0, z1, -6f, top, 2f);
            }
        }

        /// <summary>The rooftops: a row of houses stepping up the hill (each roof 3.6 m above the last, 3 m alleys between),
        /// from the bike shed by the foot square to the top: jump onto the shed off a crate, then double jump up roof to roof.</summary>
        void MakeRoofs()
        {
            _layer = "roofs";
            Box("A crate by the shed", BlockKind.Ledge, -56f, 29.5f, -53f, 32f, L0, 1.4f, usable: true);
            Box("The bike shed", BlockKind.House, RoofX0, Z1, RoofX1, Z1 + 5f, -6f, ShedRoof, usable: true);
            for (int j = 1; j <= RoofHouses; j++)
            {
                float z0 = 35f + 7f * j, z1 = 39f + 7f * j;
                float roof = ShedRoof + RoofStep * j, below = ShedRoof + RoofStep * (j - 1);
                Box("Row house " + j, BlockKind.House, RoofX0, z0, RoofX1, z1, -6f, roof, usable: true);
                // The alley in front of it: low enough to fall into, high enough to climb back onto the roof below.
                Ground("Alley before row house " + j, RoofX0, z0 - 3f, RoofX1, z0, below - 2.8f);
            }
            // The wall between the row and the middle strip.
            for (int j = 0; j <= RoofHouses; j++)
            {
                float z0 = j == 0 ? Z1 : 32f + 7f * j, z1 = 39f + 7f * j;
                float top = Math.Max(ShedRoof + RoofStep * j, TopAt(-38f, z1 - 0.01f)) + 1.4f;
                WallZ("The row's east wall " + (j + 1), BlockKind.BrickWall, -43f, z0, z1, -6f, top, 2f);
            }
        }

        /// <summary>The creek ravine: down from the creek lane into the bed (2 m), north under the east hairpin through the
        /// culvert (1.15 m clear, 10 m: slide), then up 28 falls (1.36 m, 4 m apart) to the top.</summary>
        void MakeRavine()
        {
            _layer = "ravine";
            Ground("The ravine bed (the mouth)", RavX0, CreekZ1, RavX1, 36f, RavineBed);
            Box("The culvert's crown (under the east hairpin)", BlockKind.BrickWall, MidX1, 24f, RavX1, Z1, RavineBed + CulvertClear, L1 + 1.2f);
            Box("The ravine (water)", BlockKind.Water, RavX0 + 2.5f, CreekZ1, RavX1 - 2.5f, 36f, RavineBed, RavineBed + 0.1f, solid: false);
            const int falls = 28;
            float step = (Top - RavineBed) / falls;
            for (int k = 0; k < falls; k++)
            {
                float z0 = 36f + 4f * k, z1 = z0 + 4f;
                Ground("The ravine, fall " + (k + 1), RavX0, z0, RavX1, z1, RavineBed + step * (k + 1));
            }
            // The ravine's west wall (a parapet over the hairpin street and the terraces).
            WallZ("The ravine's west wall (by the hairpin)", BlockKind.BrickWall, 49f, Z1, Z2, -6f, L1 + 1.2f, 2f);
            WallZ("The ravine's west wall (terrace 2)", BlockKind.BrickWall, 49f, Z2, Z3, -6f, L2 + 1.2f, 2f);
            WallZ("The ravine's west wall (the top)", BlockKind.BrickWall, 49f, Z3, 148f, -6f, Top + 1.2f, 2f);
        }

        void MakeTop()
        {
            _layer = "top";
            Ground("The top", WestX, Z3, MidX1, 148f, Top);
            Ground("The top (east, past the ravine)", WestX, 148f, EastX, ZEnd, Top);
            // A low wall along the top's edge over the road's last leg: the lookout over the whole street.
            WallX("The lookout wall", BlockKind.Picket, LegX0 - 8f, LegX1, Z3 + 0.25f, Top, Top + 0.9f, 0.5f);
            // The lookout: a stone platform at the edge (4.8 m, four steps up to it), where the whole street opens below.
            Box("The lookout", BlockKind.Ledge, LookoutX0, Z3 + 0.5f, LookoutX0 + 8f, Z3 + 4f, Top, LookoutTop, usable: true);
            for (int i = 0; i < 4; i++)
                Box("The lookout's step " + (4 - i), BlockKind.Ledge, LookoutX0 + 2.5f, Z3 + 4f + 1.4f * i, LookoutX0 + 5.5f, Z3 + 5.4f + 1.4f * i, Top, LookoutTop - 1.2f * (i + 1), usable: true);
            // The knoll the house stands on (4 m), a path up its front, the front garden's picket fence and its gate.
            Ground("The knoll", -20f, 110f, 16f, 146f, Knoll);
            RampZ("The path up the knoll", BlockKind.Ground, -5f, 103f, 1f, 110f, Top, Knoll);
            WallX("The front garden fence (west)", BlockKind.Picket, -16f, -3.5f, 110.4f, Knoll, Knoll + 0.85f, 0.15f);
            WallX("The front garden fence (east)", BlockKind.Picket, -0.5f, 12f, 110.4f, Knoll, Knoll + 0.85f, 0.15f);
            houseOrigin = new Vector3(-2f, Knoll, 116f);
            // The house (its outside is the house builder's; this is its solid body) and the tower.
            Box("The Lasname house", BlockKind.House, houseOrigin.X - 7.6f, houseOrigin.Z + 0.3f, houseOrigin.X + 6f, houseOrigin.Z + 15.4f, Knoll, Knoll + 4.5f);
            Box("The tower", BlockKind.House, houseOrigin.X - 4f, houseOrigin.Z + 11.1f, houseOrigin.X + 1.6f, houseOrigin.Z + 15.7f, Knoll, Knoll + 10.9f);
            // On the tower's front face, its upper storey (the face is at 11.1 m behind the door).
            towerPoint = houseOrigin + new Vector3(-1.2f, 10.2f, 10.8f);
            // The beacon (main's call, docs/LARCH-HILL.md): a soft column of the dream's gold light standing up out of the
            // tower's skylight into the sky, so the house is found from anywhere on the hill, even from under its walls.
            beaconFoot = houseOrigin + new Vector3(-1.2f, 12.5f, 13.4f);
        }

        void MakeBounds()
        {
            // The world's edges as you see them (the stills of 2026-10-12 showed sky under the hill past them): the far bank
            // past its hedge, a stone wall up the west and east sides at each level's height, a tall hedge behind the top.
            _layer = "edges";
            Ground("The far bank (past the hedge)", 40.6f, -44f, EastX + 4f, CreekZ0, L0);
            Ground("The creek bed (past the grate)", RavX1, CreekZ0, EastX + 4f, CreekZ1, CreekBed);
            float[] zs = { CreekZ1, Z1, Z2, Z3, ZEnd + 4f };
            float[] east = { 2f, L1 + 1.2f, L2 + 1.2f, Top + 1.2f };
            for (int i = 0; i < 4; i++)
                Box("The ravine's east wall " + (i + 1), BlockKind.BrickWall, EastX, zs[i], EastX + 4f, zs[i + 1], -6f, east[i]);
            for (float z = -44f; z < ZEnd; z += 6f)
            {
                float z1 = Math.Min(ZEnd + 4f, z + 6f);
                float top = Math.Max(Math.Max(TopAt(WestX + 1f, z), TopAt(WestX + 1f, z1 - 0.01f)), 0f) + 2f;
                Box("The west wall " + (int)((z + 44f) / 6f + 1f), BlockKind.BrickWall, WestX - 4f, z, WestX, z1, -6f, top);
            }
            Box("The tall hedge behind the top", BlockKind.Hedge, WestX - 4f, ZEnd, EastX + 4f, ZEnd + 4f, Top, Top + 7f);
            Box("The hedge along the far bank", BlockKind.Hedge, WestX - 4f, -48f, EastX + 4f, -44f, L0, L0 + 3f);
            _layer = "bounds";
            _layer = "bounds";
            Box("Bound (west)", BlockKind.Boundary, WestX - 4f, -44f, WestX, ZEnd + 4f, -6f, 70f);
            Box("Bound (east)", BlockKind.Boundary, EastX, -44f, EastX + 4f, ZEnd + 4f, -6f, 70f);
            Box("Bound (south)", BlockKind.Boundary, WestX, -48f, EastX, -44f, -6f, 70f);
            Box("Bound (north)", BlockKind.Boundary, WestX, ZEnd, EastX, ZEnd + 4f, -6f, 70f);
            // The middle strip's west edge between the hub corridor and the yard (the foot) is the yard's wall; past the
            // bank fence the creek's far side is the ravine's mouth (behind the grate).
            Box("Bound (south bank, past the fence)", BlockKind.Boundary, 40f, -44f, EastX, CreekZ0, -6f, 70f);
        }

        void MakeStory()
        {
            arrival = new Vector3(-53f, L0, -16f);
            arrivalYaw = 0f;
            AddSpot(SpotCreekEdge, "the creek's edge", -53f, L0 + 1f, -9.5f, 12f, 3f, 4f);
            AddSpot(SpotGarageDoor, "the half-open garage door", -53f, L0 + 1f, 1.5f, 8f, 3f, 3f);
            AddSpot(SpotLedger, "Walt's ledger (on the workbench)", -60.6f, 1.0f, 9f);
            AddSpot(SpotYard, "the shop's yard", -53f, L0 + 1f, 19.5f, 18f, 3f, 9f);
            AddSpot(SpotWalt, "Walt, in the gate", -52.5f, L0, 23.2f, yaw: 180f);
            AddSpot(SpotWaltAside, "Walt, stepped aside", -56.5f, L0, 26.5f, yaw: 135f);
            AddSpot(SpotFoot, "the foot square", -45f, L0, 29f, 36f, 4f, 10f);
            // Both halves of "the same Saturday" are below Walt's gate (his optional premise is part of the tutorial, so it
            // must be breakable on the first pass): the neighbours' loop in the garden on the far bank where she arrives,
            // Saturday's paper on the bench in Walt's yard.
            AddSpot(SpotPaper, "Saturday's paper (on Walt's yard bench)", -58.5f, L0 + 0.5f, 21f);
            AddSpot(SpotSprinkler, "the sprinkler", -40f, L0, -21.5f);
            AddSpot(SpotNeighbour, "the neighbour (her roses)", -43f, L0, -20.8f, yaw: -60f);
            AddSpot(SpotDog, "the dog", -48.5f, L0, -24f, yaw: 90f);
            AddSpot(SpotBallFrom, "the ball (thrown)", -43.8f, L0 + 1.2f, -21.4f);
            AddSpot(SpotBallTo, "the ball (lands)", -57f, L0, -25f);
            AddSpot(SpotLoopZone, "within earshot of the neighbours", -50f, L0 + 2f, -24f, 44f, 8f, 32f);
            // Terrace 1 keeps a neighbour at his grill by the east hairpin, who chats as she passes.
            AddSpot(SpotChatter, "the neighbour at his grill (terrace 1)", 24.5f, L1, 38.5f, 18f, 6f, 12f, yaw: 160f);
            // The top.
            AddSpot(SpotTopArrive, "the top", -20f, Top + 3f, 140f, 160f, 8f, 76f);
            AddSpot(SpotGate, "the garden gate", -2f, Knoll + 1f, 109.5f, 4f, 3f, 3f);
            AddSpot(SpotBarbecue, "the barbecue (Elias)", 9f, Knoll, 118.5f, yaw: 200f);
            AddSpot(SpotElias, "Elias, at the stove (through the window)", houseOrigin.X - 4.3f, Knoll, houseOrigin.Z + 2.2f, yaw: 180f);
            AddSpot(SpotWindow, "the front window", houseOrigin.X - 4.3f, Knoll + 1.65f, houseOrigin.Z);
            AddSpot(SpotDoor, "the front door", houseOrigin.X, Knoll, houseOrigin.Z);
            AddSpot(SpotPorchLight, "the porch light", houseOrigin.X + 1.85f, Knoll + 2.3f, houseOrigin.Z - 0.1f);
        }

        // ---------- Routes ----------

        BlockRoute NewRoute(string name, string chapter, string layer, float docSeconds, params (float x, float z)[] path)
        {
            var c = new LabCourse();
            var pts = new List<Vector3>();
            foreach (var (x, z) in path) pts.Add(new Vector3(x, 0f, z));
            c.route = new PathLine(pts);
            c.lane.Add(new Vector2(0f, 0f));
            c.killY = -6f;
            var r = new BlockRoute { name = name, chapter = chapter, layer = layer, course = c, docSeconds = docSeconds };
            r.start = new Vector3(path[0].x, TopAt(path[0].x, path[0].z, 60f) + 0.02f, path[0].z);
            var d = c.route.DirectionAt(0f);
            r.startYaw = (float)(Math.Atan2(d.X, d.Z) * 180.0 / Math.PI);
            r.finishS = c.route.Length - 0.5f;
            c.finishS = r.finishS;
            c.checkpoints.Add(new LabCheckpoint { name = name + " start", chapter = chapter, position = r.start, yaw = r.startYaw, s = 0f });
            return r;
        }

        static float S(BlockRoute r, float x, float z) => r.course.route.Project(new Vector3(x, 0f, z));
        static void Cue(BlockRoute r, LabCueKind kind, float s, float sEnd = 0f) => r.course.cues.Add(new LabCue { kind = kind, s = s, sEnd = sEnd });
        static void Beat(BlockRoute r, CourseBeatKind kind, string name, float s, float size, float height) =>
            r.beats.Add(new CourseBeat { kind = kind, name = name, s = s, size = size, height = height });

        void MakeRoutes()
        {
            // The way in: across the creek (the first double jump), under the shop's half-open garage door (the first slide),
            // through the shop and out of its back door to Walt.
            var w = NewRoute("The way in", ChapterArrival, "bottom", 10f, (-53f, -16f), (-53f, 1.6f), (-53f, 9.5f), (-48.75f, 13f), (-48.75f, 17.5f), (-52.5f, 21.8f));
            Cue(w, LabCueKind.Jump, S(w, -53f, -8.7f));
            Cue(w, LabCueKind.DoubleJumpAtApex, S(w, -53f, -7.6f));
            Cue(w, LabCueKind.Crouch, S(w, -53f, 1.0f), S(w, -53f, 4.8f));
            Beat(w, CourseBeatKind.DoubleGap, "The creek (the first double jump)", S(w, -53f, -4f), CreekZ1 - CreekZ0, 0f);
            Beat(w, CourseBeatKind.Slide, "Walt's half-open garage door (the first slide)", S(w, -53f, 3.15f), 0.3f, CulvertClear);
            wayIn = w;

            // The road: up leg 1, round the east hairpin, up leg 2, the west hairpin, up leg 3 onto the top.
            var rd = NewRoute(RouteRoad, ChapterRoad, "road", 40f,
                (-40f, 29f), (-28f, 29f), (34f, 29f), (42f, 31f), (42f, 61f), (34f, 63f), (-28f, 63f), (-36f, 65f), (-36f, 95f),
                (-28f, 97f), (34f, 97f), (42f, 99f), (42f, 108f));
            Beat(rd, CourseBeatKind.RunUp, "Leg 1 (to 12 m, 10°)", S(rd, 0f, 29f), LegX1 - LegX0, 0f);
            Beat(rd, CourseBeatKind.RunUp, "Leg 2 (to 24 m)", S(rd, 0f, 63f), LegX1 - LegX0, 0f);
            Beat(rd, CourseBeatKind.RunUp, "Leg 3 (to the top)", S(rd, 0f, 97f), LegX1 - LegX0, 0f);
            routes.Add(rd);

            // The garden stairs: flight 1, back lane 1 (east), the garden wall, flight 2, back lane 2 (west), the wall, flight 3.
            var st = NewRoute(RouteStairs, ChapterStairs, "stairs", 30f,
                (-75.5f, 28f), (-75.5f, 52f), (-75f, 54.5f), (-69f, 54.5f), (-68.5f, 57f), (-68.5f, 77f), (-69f, 79.5f), (-75f, 79.5f),
                (-75.5f, 82f), (-75.5f, 98f), (-75.5f, 100.5f), (-75.5f, 107f));
            Beat(st, CourseBeatKind.Mantle, "Back lane 1's garden wall", S(st, -68.5f, 56f), 3f, L1 - 10.5f);
            Beat(st, CourseBeatKind.Mantle, "Back lane 2's garden wall", S(st, -75.5f, 81f), 3f, L2 - 22.5f);
            Beat(st, CourseBeatKind.Mantle, "Back lane 3's garden wall (onto the top)", S(st, -75.5f, 102f), 3f, Top - 34.5f);
            routes.Add(st);

            // The rooftops: onto the crate, the shed, then roof to roof up the row (a double jump each), onto the top.
            var rf = NewRoute(RouteRoofs, ChapterRoofs, "roofs", 35f, (-54.5f, 26f), (-54.5f, 103f), (-54.5f, 107f));
            Cue(rf, LabCueKind.Jump, S(rf, -54.5f, 31.5f));
            Beat(rf, CourseBeatKind.Mantle, "The crate", S(rf, -54.5f, 29.5f), 2.5f, 1.4f);
            Beat(rf, CourseBeatKind.Mantle, "The bike shed (jump + mantle from the crate)", S(rf, -54.5f, Z1), 5f, ShedRoof - 1.4f - 1.25f);
            for (int j = 1; j <= RoofHouses; j++)
            {
                float edge = 32f + 7f * j;
                Cue(rf, LabCueKind.Jump, S(rf, -54.5f, edge - 0.6f));
                Cue(rf, LabCueKind.DoubleJumpAtApex, S(rf, -54.5f, edge + 0.2f));
                Beat(rf, CourseBeatKind.Mantle, "Row house " + j + " (double jump + mantle, " + RoofStep.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " m up)",
                    S(rf, -54.5f, edge + 3f), RoofDepth, RoofStep - 1.25f - 1f);
            }
            routes.Add(rf);

            // The creek ravine: the hub corridor to the creek lane, east along it, down into the ravine, through the culvert
            // (slide), up the falls.
            var rv = NewRoute(RouteRavine, ChapterRavine, "ravine", 40f,
                (-36f, 22f), (-36f, 5f), (40f, 4.5f), (47.5f, 5.5f), (54f, 11f), (54f, 23f), (54f, 35f), (54f, 150f), (54f, 156f));
            Cue(rv, LabCueKind.Crouch, S(rv, 54f, 22.6f), S(rv, 54f, 34.6f));
            Beat(rv, CourseBeatKind.Slide, "The culvert under the east hairpin", S(rv, 54f, 29f), Z1 - 24f, CulvertClear);
            for (int k = 0; k < 28; k++)
                Beat(rv, CourseBeatKind.Mantle, "The ravine, fall " + (k + 1), S(rv, 54f, 36f + 4f * k), 4f, (Top - RavineBed) / 28f);
            routes.Add(rv);
        }

        void MakeViews()
        {
            const float eye = 1.5f;
            views.Add(new HillView { name = "street-01-bottom-the-climb", eye = arrival + new Vector3(0f, eye, 0f), look = towerPoint + new Vector3(0f, -14f, -30f) });
            views.Add(new HillView { name = "street-02-bottom-the-shop", eye = new Vector3(-48f, eye, -12f), look = new Vector3(-53f, 2f, 6f) });
            views.Add(new HillView { name = "street-03-middle-up-to-the-tower", eye = new Vector3(42f, L1 + eye, 36f), look = towerPoint });
            views.Add(new HillView { name = "street-04-middle-down-the-road", eye = new Vector3(-36f, L2 + eye, 70f), look = new Vector3(5f, 2f, 12f) });
            views.Add(new HillView { name = "street-05-top-the-vista", eye = new Vector3(LookoutX0 + 4f, LookoutTop + eye, Z3 + 1.2f), look = new Vector3(-18f, 4f, 0f) });
            views.Add(new HillView { name = "street-06-top-the-house", eye = new Vector3(-2f, Top + eye, Z3 + 0.8f), look = houseOrigin + new Vector3(0f, 3f, 4f) });
            views.Add(new HillView { name = "street-07-the-rooftops", eye = new Vector3(-53f, ShedRoof + RoofStep * 2f + eye, 50f), look = new Vector3(-53f, ShedRoof + RoofStep * 6f, 90f) });
            views.Add(new HillView { name = "street-08-the-ravine-culvert", eye = new Vector3(54f, RavineBed + eye, 12f), look = new Vector3(54f, RavineBed + 0.6f, 30f) });
            float fallsEye = RavineBed + (Top - RavineBed) / 28f * 7f + eye;
            views.Add(new HillView { name = "street-09-the-ravine-falls", eye = new Vector3(54f, fallsEye, 60f), look = new Vector3(54f, fallsEye + 12f, 110f) });
        }
    }
}
