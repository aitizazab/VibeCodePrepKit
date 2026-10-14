using System;
using System.Collections.Generic;
using System.Numerics;

namespace JamKit.Skyline
{
    /// <summary>
    /// The Food Court (floor 1's arena) as DATA: every collider of the hall as a <see cref="FloorSolid"/>, the named floor
    /// zones, the exit-lift zone and a ground-height query. The Unity builder makes exactly one collider per solid; the layout
    /// lint, the navigation graph, the headless sim and the pilot read the same list. The numbers are the layout sheet of
    /// docs/SKYLINE-PLAN-FLOOR1.md (metres, +Z north, +X east; the hall interior is x -35..35, z -28..28 and the walls stand
    /// 0.4 m outside it).
    ///
    /// Roles used here (a free string, see <see cref="FloorSolid.role"/>): floor wall rim shopfront column pilaster escalator
    /// ramp deck balustrade table planter kiosk lift facade-pane ceiling skylight. Notes for the tasks that read this:
    /// <list type="bullet">
    /// <item>Ground floors (role floor) are 1 m slabs under the walkable top; decks are 0.4 m (5.6..6.0).</item>
    /// <item>The east façade is individual glass panes (<see cref="PaneId"/>), each its own solid: a later task disables one
    /// without touching its neighbours (a shot through a pane breaks it).</item>
    /// <item><see cref="LiftShutterId"/> (the west service lift's shutter) and <see cref="CabDoorsId"/> (the arrival cab's
    /// doors onto the alcove) are CLOSED leaves: the sim/Unity disables them when the floor opens them.</item>
    /// <item>Closed shops (north 1, 3, 5; south 1, 5) are solid blocks (the shutter is a spawn door drawn on their front); the
    /// four walk-in shops are hollow, with a 4 m front doorway and a 2.4 m back doorway.</item>
    /// <item>Hatch ends of the service corridor are open openings (the rolled-up shutter is decoration).</item>
    /// </list>
    /// </summary>
    public static class RepossessArena
    {
        public const float MinX = -35f, MaxX = 35f, MinZ = -28f, MaxZ = 28f, PitFloorY = -4f, RingY = 0f, GalleryY = 6f,
            SkylightY = 14f, RoofY = 16f, WallThick = 0.4f;

        /// <summary>The pit's interior (the retaining walls stand just outside it).</summary>
        public const float ChuteMinX = -11f, ChuteMaxX = -8f;      // moved 1 m east on 2026-10-14 (D23): the pit's NW corner is no longer a pocket
        public const float PitMinX = -16f, PitMaxX = 12f, PitMinZ = -10f, PitMaxZ = 10f;
        /// <summary>The underside of the gallery decks and the top of the shops' roofs (the decks are 5.6..6.0).</summary>
        public const float DeckBottomY = 5.6f;
        /// <summary>The hall's clear height; the roof slab sits on top of it (15.6..16).</summary>
        public const float HallClearY = 15.6f;
        public const float FrontDoorWidth = 4f, BackDoorWidth = 2.4f, DoorHeight = 3f;

        /// <summary>The facade is <see cref="PaneRows"/> x <see cref="PaneCols"/> glass panes, 2.8 m wide and 4 m tall (the top row 3.6 m).</summary>
        public const int PaneRows = 4, PaneCols = 20;
        public const float PaneWidth = 2.8f;
        public const string PanePrefix = "facade-pane-";
        public const string LiftShutterId = "lift-shutter";
        /// <summary>The solid ledge against the east façade, x 30.5..35, z 9..14.8, top 2.8: the wall-run route's way up to the north gallery (D23).</summary>
        public const string FacadeLandingId = "facade-landing";
        public const float FacadeLandingMinX = 30.5f, FacadeLandingMinZ = 9f, FacadeLandingMaxZ = 14.8f, FacadeLandingTop = 2.8f;
        public const string CabDoorsId = "cab-doors";

        /// <summary>The alcove, where the drop lands (the lift cab's mouth opens onto it).</summary>
        public static readonly Vector3 ArrivalPoint = new Vector3(0f, 0f, -21f);

        /// <summary>The Unit's feet inside this box end the floor (the west service lift's interior).</summary>
        public static readonly CoverBox ExitLiftZone = new CoverBox { min = new Vector3(-31f, 0f, -2f), max = new Vector3(-27f, 2.5f, 2f) };

        static readonly float[] Joints = { -26f, -15.6f, -5.2f, 5.2f, 15.6f, 26f };
        /// <summary>Where the gallery columns stand (x; both rows, z +-16.5; 16.1..16.9): -14 and 14 flank the walk-in shops, -5.2 and 5.2 are
        /// the joints on each side of the middle shop and the arrival alcove, 26 the east joint. None is in front of a shutter (the
        /// shutters span x -24.8..-16.8, -4..4 and 16.8..24.8; <see cref="ShutterClearance"/>), the deck is never more than 12 m from a
        /// column along its front edge (the deck ends at x -26 and the facade), and none is on an escalator's landing. Moved on
        /// 2026-10-14: the old -20, 20 and (north) 0 columns stood 1.1 m in front of the N1, N5 and N3 shutters.</summary>
        public static readonly float[] ColumnXs = { -14f, -5.2f, 5.2f, 14f, 26f };
        /// <summary>Every shop shutter (<see cref="ShutterWidth"/> wide, in the face of its shop at z +-18) has this much clear floor in
        /// front of it (its full width, out from the shop face into the ring): no column, pilaster or rim.</summary>
        public const float ShutterClearance = 2.5f, ShutterWidth = 8f;
        static readonly string[] WalkableRoles = { "floor", "deck", "ramp", "escalator", "table", "planter", "kiosk" };

        static FloorSolid[] _solids;
        static CoverBox[] _sight, _shot;
        static FloorZone[] _zones;

        /// <summary>The id of the facade pane at <paramref name="row"/> (0 = bottom) and <paramref name="col"/> (0 = south end).</summary>
        public static string PaneId(int row, int col) => PanePrefix + row + "-" + col;

        public static bool IsPane(string id) => id != null && id.StartsWith(PanePrefix, StringComparison.Ordinal);

        /// <summary>Every collider of the hall, once (the array is a copy; the solids are shared and must not be edited).</summary>
        public static FloorSolid[] Solids()
        {
            if (_solids == null) _solids = Build();
            return (FloorSolid[])_solids.Clone();
        }

        public static FloorSolid Find(string id)
        {
            foreach (var s in Solids()) if (s.id == id) return s;
            return null;
        }

        /// <summary>True for the surfaces a person stands on (floors, decks, ramps, escalators, and the low props' tops).</summary>
        public static bool IsWalkableRole(string role) => Array.IndexOf(WalkableRoles, role) >= 0;

        /// <summary>The solids that stop sight (and shots): everything except glass and the ground slabs; each ramp is
        /// replaced by stepped boxes under its lowest edge per 2 m slice (never a wedge that would overreach the slope).</summary>
        public static CoverBox[] SightBlockers()
        {
            if (_sight == null)
            {
                var l = new List<CoverBox>();
                foreach (var s in Solids())
                {
                    if (s.glass || s.role == "floor") continue;
                    if (s.ramp) AddRampSlices(s, l); else l.Add(s.box);
                }
                _sight = l.ToArray();
            }
            return (CoverBox[])_sight.Clone();
        }

        /// <summary>What stops a bullet: the sight blockers plus the glass balustrades (the bridge's and the galleries' rails
        /// block shots). Façade panes, the bridge floor and the skylight are NOT here: a shot through a pane breaks that pane.</summary>
        public static CoverBox[] ShotBlockers()
        {
            if (_shot == null)
            {
                var l = new List<CoverBox>(SightBlockers());
                foreach (var s in Solids()) if (s.glass && s.role == "balustrade") l.Add(s.box);
                _shot = l.ToArray();
            }
            return (CoverBox[])_shot.Clone();
        }

        /// <summary>The highest walkable top at (x, z) that is at or below <paramref name="fromY"/> (a 5 cm tolerance), ramps
        /// interpolated. False when nothing walkable lies below.</summary>
        public static bool TryGroundY(float x, float z, float fromY, out float y)
        {
            y = 0f;
            if (_solids == null) _solids = Build();
            bool found = false;
            float best = float.NegativeInfinity;
            foreach (var s in _solids)
            {
                if (!IsWalkableRole(s.role)) continue;
                var b = s.box;
                if (x < b.min.X || x > b.max.X || z < b.min.Z || z > b.max.Z) continue;
                float top = s.TopAt(x, z);
                if (top > fromY + 0.05f || top <= best) continue;
                best = top;
                found = true;
            }
            if (found) y = best;
            return found;
        }

        public static FloorZone[] Zones()
        {
            if (_zones == null) _zones = BuildZones();
            return (FloorZone[])_zones.Clone();
        }

        // ---------------------------------------------------------------- ramps for the sight list

        static void AddRampSlices(FloorSolid s, List<CoverBox> l)
        {
            var b = s.box;
            float lo = s.rampAlongX ? b.min.X : b.min.Z, hi = s.rampAlongX ? b.max.X : b.max.Z;
            float len = hi - lo;
            int n = Math.Max(1, (int)Math.Ceiling(len / 2f));
            for (int i = 0; i < n; i++)
            {
                float a = lo + len * i / n, c = lo + len * (i + 1) / n;
                float ta = s.rampTopStart + (s.rampTopEnd - s.rampTopStart) * (a - lo) / len;
                float tc = s.rampTopStart + (s.rampTopEnd - s.rampTopStart) * (c - lo) / len;
                float top = Math.Min(ta, tc);
                if (top - b.min.Y < 0.05f) continue;
                var min = b.min; var max = b.max;
                if (s.rampAlongX) { min.X = a; max.X = c; } else { min.Z = a; max.Z = c; }
                max.Y = top;
                l.Add(new CoverBox { min = min, max = max });
            }
        }

        // ---------------------------------------------------------------- the data

        static List<FloorSolid> _l;

        static FloorSolid B(string id, string role, float x0, float x1, float y0, float y1, float z0, float z1, bool run = false, bool glass = false)
        {
            var s = new FloorSolid
            {
                id = id, role = role, runnable = run, glass = glass,
                box = new CoverBox { min = new Vector3(Math.Min(x0, x1), Math.Min(y0, y1), Math.Min(z0, z1)), max = new Vector3(Math.Max(x0, x1), Math.Max(y0, y1), Math.Max(z0, z1)) },
            };
            _l.Add(s);
            return s;
        }

        /// <summary>A box in a mirrored row: <paramref name="r"/> +1 north, -1 south; the two depths are positive distances from the origin.</summary>
        static FloorSolid R(string id, string role, float x0, float x1, float y0, float y1, int r, float d0, float d1, bool run = false) =>
            B(id, role, x0, x1, y0, y1, d0 * r, d1 * r, run);

        static void Ramp(string id, string role, float x0, float x1, float z0, float z1, float bottom, float topStart, float topEnd, bool alongX)
        {
            var s = B(id, role, x0, x1, bottom, Math.Max(topStart, topEnd), z0, z1);
            s.ramp = true; s.rampAlongX = alongX; s.rampTopStart = topStart; s.rampTopEnd = topEnd;
        }

        static FloorSolid[] Build()
        {
            _l = new List<FloorSolid>();
            BuildShell();
            BuildPit();
            BuildShops();
            BuildCorridors();
            BuildUpper();
            BuildFacade();
            var r = _l.ToArray();
            _l = null;
            return r;
        }

        static void BuildShell()
        {
            const float top = HallClearY;
            // roof slab (a hole over the skylight) and the glazing at 14
            B("roof-w", "ceiling", -35.4f, -30f, top, RoofY, -28.4f, 28.4f);
            B("roof-e", "ceiling", 30f, 35.4f, top, RoofY, -28.4f, 28.4f);
            B("roof-n", "ceiling", -30f, 30f, top, RoofY, 22f, 28.4f);
            B("roof-s", "ceiling", -30f, 30f, top, RoofY, -28.4f, -22f);
            B("skylight", "skylight", -30f, 30f, SkylightY, SkylightY + 0.2f, -22f, 22f, false, true);

            // ground slabs round the pit (the hole is the pit with its retaining walls)
            B("floor-w", "floor", MinX, PitMinX - WallThick, -1f, 0f, MinZ, MaxZ);
            B("floor-e", "floor", PitMaxX + WallThick, MaxX, -1f, 0f, MinZ, MaxZ);
            B("floor-n", "floor", PitMinX - WallThick, PitMaxX + WallThick, -1f, 0f, PitMaxZ + WallThick, MaxZ);
            B("floor-s", "floor", PitMinX - WallThick, PitMaxX + WallThick, -1f, 0f, MinZ, PitMinZ - WallThick);
            // the cab's threshold through the south wall
            B("floor-cab-threshold", "floor", -2.1f, 2.1f, -1f, 0f, -28.4f, MinZ);

            // outer walls (the corridors line the west, north and south walls; the promenade ends are runnable)
            B("wall-west", "wall", -35.4f, MinX, 0f, top, -28.4f, 28.4f);
            B("wall-north-a", "wall", MinX, 26f, 0f, top, 27.5f, 28.4f);
            B("wall-north-b", "wall", 26f, MaxX, 0f, top, MaxZ, 28.4f, true);
            B("wall-south-a", "wall", MinX, -2.5f, 0f, top, -28.4f, -27.5f);
            B("wall-south-b", "wall", 2.5f, 26f, 0f, top, -28.4f, -27.5f);
            B("wall-south-c", "wall", 26f, MaxX, 0f, top, -28.4f, MinZ, true);
        }

        static void BuildPit()
        {
            const float t = WallThick;
            B("pit-floor", "floor", PitMinX, PitMaxX, -5f, PitFloorY, PitMinZ, PitMaxZ);
            // retaining walls, -4..0, runnable, with gaps where the ramps and the chute come in
            B("pit-wall-n1", "rim", PitMinX - t, ChuteMinX, PitFloorY, 0f, PitMaxZ, PitMaxZ + t, true);
            B("pit-wall-n2", "rim", ChuteMaxX, -4f, PitFloorY, 0f, PitMaxZ, PitMaxZ + t, true);
            B("pit-wall-n3", "rim", 0f, PitMaxX + t, PitFloorY, 0f, PitMaxZ, PitMaxZ + t, true);
            B("pit-wall-s1", "rim", PitMinX - t, -4f, PitFloorY, 0f, PitMinZ - t, PitMinZ, true);
            B("pit-wall-s2", "rim", 0f, PitMaxX + t, PitFloorY, 0f, PitMinZ - t, PitMinZ, true);
            B("pit-wall-w1", "rim", PitMinX - t, PitMinX, PitFloorY, 0f, PitMinZ, -2f, true);
            B("pit-wall-w2", "rim", PitMinX - t, PitMinX, PitFloorY, 0f, 2f, PitMaxZ, true);
            B("pit-wall-e1", "rim", PitMaxX, PitMaxX + t, PitFloorY, 0f, PitMinZ, -2f, true);
            B("pit-wall-e2", "rim", PitMaxX, PitMaxX + t, PitFloorY, 0f, 2f, PitMaxZ, true);
            // the floor through each ramp mouth
            B("floor-mouth-w", "floor", PitMinX - t, PitMinX, -1f, 0f, -2f, 2f);
            B("floor-mouth-e", "floor", PitMaxX, PitMaxX + t, -1f, 0f, -2f, 2f);
            B("floor-mouth-n", "floor", -4f, 0f, -1f, 0f, PitMaxZ, PitMaxZ + t);
            B("floor-mouth-chute", "floor", ChuteMinX, ChuteMaxX, -1f, 0f, PitMaxZ, PitMaxZ + t);
            B("floor-mouth-s", "floor", -4f, 0f, -1f, 0f, PitMinZ - t, PitMinZ);
            // ramps (6 m long, 4 m wide, 34 degrees) and the slide chute (5 m, 39 degrees)
            Ramp("ramp-w", "ramp", -16f, -10f, -2f, 2f, PitFloorY, 0f, PitFloorY, true);
            Ramp("ramp-e", "ramp", 6f, 12f, -2f, 2f, PitFloorY, PitFloorY, 0f, true);
            Ramp("ramp-n", "ramp", -4f, 0f, 4f, 10f, PitFloorY, PitFloorY, 0f, false);
            Ramp("ramp-s", "ramp", -4f, 0f, -10f, -4f, PitFloorY, 0f, PitFloorY, false);
            Ramp("chute", "ramp", ChuteMinX, ChuteMaxX, 5f, 10f, PitFloorY, PitFloorY, 0f, false);

            // the kiosk, and crouch cover only: 8 tables, 4 planters
            B("kiosk", "kiosk", -3.5f, -0.5f, PitFloorY, -1.5f, -1.5f, 1.5f);
            float[][] tables =
            {
                new[] { -14.2f, 5.0f }, new[] { -14.2f, 6.6f }, new[] { -14.2f, 8.2f }, new[] { -7.0f, 7.0f }, new[] { -7.0f, 5.4f },
                new[] { 4.8f, -6.4f }, new[] { 7.2f, -6.4f }, new[] { 4.8f, -8.6f },
            };
            for (int i = 0; i < tables.Length; i++)
                B("table-" + (i + 1), "table", tables[i][0] - 0.8f, tables[i][0] + 0.8f, PitFloorY, PitFloorY + 1f, tables[i][1] - 0.45f, tables[i][1] + 0.45f);
            float[][] planters = { new[] { -7.0f, 8.9f }, new[] { -14.0f, 9.3f }, new[] { 9.8f, -8.8f }, new[] { 9.8f, -6.6f } };
            for (int i = 0; i < planters.Length; i++)
                B("planter-" + (i + 1), "planter", planters[i][0] - 1f, planters[i][0] + 1f, PitFloorY, PitFloorY + 1f, planters[i][1] - 0.4f, planters[i][1] + 0.4f);
        }

        static void BuildShops()
        {
            const float ceil = 4.6f, top = DeckBottomY;
            foreach (int r in new[] { 1, -1 })
            {
                string row = r > 0 ? "n" : "s";
                for (int i = 1; i <= 5; i++)
                {
                    if (r < 0 && i == 3) continue;     // the arrival alcove
                    float x0 = Joints[i - 1], x1 = Joints[i], cx = (x0 + x1) * 0.5f;
                    string id = row + i;
                    bool walkIn = i == 2 || i == 4;
                    if (!walkIn)
                    {
                        R("shopfront-" + id, "shopfront", x0, x1, 0f, top, r, 18f, 24f, true);
                        continue;
                    }
                    float fd = FrontDoorWidth * 0.5f, bd = BackDoorWidth * 0.5f;
                    R("shopfront-" + id + "-l", "shopfront", x0, cx - fd, 0f, top, r, 18f, 18.4f, true);
                    R("shopfront-" + id + "-r", "shopfront", cx + fd, x1, 0f, top, r, 18f, 18.4f, true);
                    R("shopfront-" + id + "-lintel", "shopfront", cx - fd, cx + fd, DoorHeight, top, r, 18f, 18.4f, true);
                    R("shop-" + id + "-back-l", "wall", x0, cx - bd, 0f, ceil, r, 23.6f, 24f);
                    R("shop-" + id + "-back-r", "wall", cx + bd, x1, 0f, ceil, r, 23.6f, 24f);
                    R("shop-" + id + "-back-lintel", "wall", cx - bd, cx + bd, DoorHeight, ceil, r, 23.6f, 24f);
                    R("shop-" + id + "-side-w", "wall", x0, x0 + 0.4f, 0f, ceil, r, 18.4f, 23.6f);
                    R("shop-" + id + "-side-e", "wall", x1 - 0.4f, x1, 0f, ceil, r, 18.4f, 23.6f);
                    R("shop-" + id + "-roof", "ceiling", x0, x1, ceil, top, r, 18.4f, 24f);
                }
                // pilasters at every shop joint (0.6 x 0.6, in the ring)
                for (int j = 0; j < Joints.Length; j++)
                    R("pilaster-" + row + "-" + j, "pilaster", Joints[j] - 0.3f, Joints[j] + 0.3f, 0f, top, r, 17.4f, 18f);
            }
            // (no alcove back walls: the corridor stubs beside the cab join the alcove; decision D23)
            // the arrival cab: side walls, roof, the doors onto the alcove (closed until the drop) and their lintel
            B("cab-side-w", "lift", -2.5f, -2.1f, 0f, 4f, -28.4f, -23.6f);
            B("cab-side-e", "lift", 2.1f, 2.5f, 0f, 4f, -28.4f, -23.6f);
            B("cab-roof", "ceiling", -2.5f, 2.5f, 4f, 4.4f, -28.4f, -23.6f);
            B("cab-lintel", "lift", -2.1f, 2.1f, 3.2f, 4f, -24f, -23.6f);
            B(CabDoorsId, "lift", -2.1f, 2.1f, 0f, 3.2f, -24f, -23.6f);
        }

        static void BuildCorridors()
        {
            const float h = 4.6f;
            // the U's ring-side walls: the west leg's east wall (two doors and the lift), and the two corner returns
            B("leg-east-1", "wall", -31.5f, -31.1f, 0f, h, -24f, -13.2f);
            B("leg-east-2", "wall", -31.5f, -31.1f, 0f, h, -10.8f, -4f);
            B("leg-east-3", "wall", -31.5f, -31.1f, 0f, h, 4f, 10.8f);
            B("leg-east-4", "wall", -31.5f, -31.1f, 0f, h, 13.2f, 24f);
            B("leg-door-n-lintel", "wall", -31.5f, -31.1f, DoorHeight, h, 10.8f, 13.2f);
            B("leg-door-s-lintel", "wall", -31.5f, -31.1f, DoorHeight, h, -13.2f, -10.8f);
            B("corr-n-return", "wall", -31.1f, -26f, 0f, h, 23.6f, 24f);
            B("corr-s-return", "wall", -31.1f, -26f, 0f, h, -24f, -23.6f);
            // ceilings
            B("ceiling-leg", "ceiling", -35f, -31.5f, h, 5f, -27.5f, 27.5f);
            B("ceiling-n", "ceiling", -31.5f, 26f, h, 5f, 24f, 27.5f);
            B("ceiling-s-w", "ceiling", -31.5f, -2.5f, h, 5f, -27.5f, -24f);
            B("ceiling-s-e", "ceiling", 2.5f, 26f, h, 5f, -27.5f, -24f);
            // the west service lift: back wall, side walls, front wall with a 3.2 m doorway, the shutter, the roof
            B("lift-back", "lift", -31.5f, -31.1f, 0f, h, -4f, 4f);
            B("lift-side-n", "lift", -31.1f, -24f, 0f, h, 3.6f, 4f);
            B("lift-side-s", "lift", -31.1f, -24f, 0f, h, -4f, -3.6f);
            B("lift-front-n", "lift", -24.4f, -24f, 0f, h, 2f, 3.6f);
            B("lift-front-s", "lift", -24.4f, -24f, 0f, h, -3.6f, -2f);
            B("lift-front-lintel", "lift", -24.4f, -24f, 3.2f, h, -2f, 2f);
            B(LiftShutterId, "lift", -24.4f, -24f, 0f, 3.2f, -2f, 2f);
            B("lift-roof", "ceiling", -31.5f, -24f, h, 5f, -4f, 4f);
        }

        static void BuildUpper()
        {
            // gallery decks (5.6..6.0) over the shops and the ring's edge
            B("deck-n", "deck", -26f, MaxX, DeckBottomY, GalleryY, 15f, 24f);
            B("deck-s", "deck", -26f, MaxX, DeckBottomY, GalleryY, -24f, -15f);
            // glass rails, 1 m, on the deck edges; gaps at the escalator, the bridge and the east lip (the mantle up)
            float[][] inner = { new[] { -26f, -22f }, new[] { -19.5f, 6f }, new[] { 10f, 31f } };
            float[][] innerS = { new[] { -26f, 6f }, new[] { 10f, 18f }, new[] { 20.5f, 31f } };
            for (int i = 0; i < inner.Length; i++) B("rail-n-edge-" + i, "balustrade", inner[i][0], inner[i][1], GalleryY, GalleryY + 1f, 15f, 15.1f, false, true);
            for (int i = 0; i < innerS.Length; i++) B("rail-s-edge-" + i, "balustrade", innerS[i][0], innerS[i][1], GalleryY, GalleryY + 1f, -15.1f, -15f, false, true);
            B("rail-n-back", "balustrade", -26f, MaxX, GalleryY, GalleryY + 1f, 23.9f, 24f, false, true);
            B("rail-s-back", "balustrade", -26f, MaxX, GalleryY, GalleryY + 1f, -24f, -23.9f, false, true);
            B("rail-n-west", "balustrade", -26f, -25.9f, GalleryY, GalleryY + 1f, 15.1f, 23.9f, false, true);
            B("rail-s-west", "balustrade", -26f, -25.9f, GalleryY, GalleryY + 1f, -23.9f, -15.1f, false, true);
            // columns under the decks
            // at the shop joints (behind each stands a pilaster) and never in front of a shutter: every shutter (8 m wide) keeps
            // ShutterClearance m clear in front of it, so the columns skip x -24.8..-16.8, -4..4 and 16.8..24.8 (see ColumnXs)
            for (int i = 0; i < ColumnXs.Length; i++)
            {
                float x = ColumnXs[i];
                B("column-n-" + i, "column", x - 0.4f, x + 0.4f, 0f, DeckBottomY, 16.1f, 16.9f);
                B("column-s-" + i, "column", x - 0.4f, x + 0.4f, 0f, DeckBottomY, -16.9f, -16.1f);
            }
            // the bridge: glass floor, glass balustrades on each side (they block shots)
            B("bridge-floor", "deck", 6f, 10f, DeckBottomY, GalleryY, -15f, 15f, false, true);
            B("bridge-rail-w", "balustrade", 5.9f, 6f, DeckBottomY, GalleryY + 1f, -15f, 15f, false, true);
            B("bridge-rail-e", "balustrade", 10f, 10.1f, DeckBottomY, GalleryY + 1f, -15f, 15f, false, true);
            // the two escalators (static ramps; the moving steps are visuals)
            Ramp("escalator-nw", "escalator", -22f, -19.5f, 4f, 15f, 0f, 0f, GalleryY, false);
            Ramp("escalator-se", "escalator", 18f, 20.5f, -15f, -4f, 0f, GalleryY, 0f, false);
        }

        static void BuildFacade()
        {
            // the east wall: glass panes, each a solid of its own (runnable, glass); posts close the two corners
            float[] rows = { 0f, 4f, 8f, 12f, HallClearY };
            for (int r = 0; r < PaneRows; r++)
                for (int c = 0; c < PaneCols; c++)
                    B(PaneId(r, c), "facade-pane", MaxX, MaxX + WallThick, rows[r], rows[r + 1], MinZ + PaneWidth * c, MinZ + PaneWidth * (c + 1), true, true);
            B("facade-post-n", "wall", MaxX, MaxX + WallThick, 0f, HallClearY, MaxZ, 28.4f);
            B("facade-post-s", "wall", MaxX, MaxX + WallThick, 0f, HallClearY, -28.4f, MinZ);
            // the landing: the ledge (2.8 m) that makes the wall-run -> north gallery route work (decision D23). A jump and a mantle
            // put the Unit on it, a jump and a double jump from its top put the deck face at z 15 within the longest mantle.
            B(FacadeLandingId, "deck", FacadeLandingMinX, MaxX, 0f, FacadeLandingTop, FacadeLandingMinZ, FacadeLandingMaxZ);
        }

        // ---------------------------------------------------------------- zones

        static FloorZone Z(string id, SurfaceLevel level, float x0, float x1, float z0, float z1, float y) =>
            new FloorZone { id = id, level = level, minX = x0, maxX = x1, minZ = z0, maxZ = z1, y = y };

        static FloorZone[] BuildZones()
        {
            var z = new List<FloorZone>
            {
                Z("pit", SurfaceLevel.Pit, PitMinX, PitMaxX, PitMinZ, PitMaxZ, PitFloorY),
                Z("ring-n", SurfaceLevel.Ring, -31.1f, MaxX, 10.4f, 18f, RingY),
                Z("ring-s", SurfaceLevel.Ring, -31.1f, MaxX, -18f, -10.4f, RingY),
                Z("ring-w", SurfaceLevel.Ring, -31.1f, -16.4f, -10.4f, 10.4f, RingY),
                Z("ring-e", SurfaceLevel.Ring, 12.4f, MaxX, -10.4f, 10.4f, RingY),
                Z("ring-nw", SurfaceLevel.Ring, -31.1f, -26f, 18f, 23.6f, RingY),
                Z("ring-sw", SurfaceLevel.Ring, -31.1f, -26f, -23.6f, -18f, RingY),
                Z("ring-ne", SurfaceLevel.Ring, 26f, MaxX, 18f, MaxZ, RingY),
                Z("ring-se", SurfaceLevel.Ring, 26f, MaxX, MinZ, -18f, RingY),
                Z("alcove", SurfaceLevel.Ring, -5.2f, 5.2f, -23.6f, -18f, RingY),
                Z("cab", SurfaceLevel.Lobby, -2.1f, 2.1f, -28f, -24f, RingY),
                Z("gallery-n", SurfaceLevel.Gallery, -26f, MaxX, 15f, 24f, GalleryY),
                Z("gallery-s", SurfaceLevel.Gallery, -26f, MaxX, -24f, -15f, GalleryY),
                Z("bridge", SurfaceLevel.Gallery, 6f, 10f, -15f, 15f, GalleryY),
                Z("corridor-n", SurfaceLevel.Corridor, -31.5f, 26f, 24f, 27.5f, RingY),
                Z("corridor-s", SurfaceLevel.Corridor, -31.5f, -2.5f, -27.5f, -24f, RingY),
                Z("corridor-s-east", SurfaceLevel.Corridor, 2.5f, 26f, -27.5f, -24f, RingY),
                Z("corridor-w", SurfaceLevel.Corridor, MinX, -31.5f, -27.5f, 27.5f, RingY),
                Z("exit-lift", SurfaceLevel.Corridor, -31.1f, -24.4f, -3.6f, 3.6f, RingY),
            };
            foreach (int r in new[] { 1, -1 })
                for (int i = 1; i <= 5; i++)
                {
                    if (r < 0 && i == 3) continue;
                    float za = r > 0 ? 18.4f : -23.6f, zb = r > 0 ? 23.6f : -18.4f;
                    z.Add(Z("shop-" + (r > 0 ? "n" : "s") + i, SurfaceLevel.ShopInterior, Joints[i - 1], Joints[i], za, zb, RingY));
                }
            return z.ToArray();
        }
    }
}
