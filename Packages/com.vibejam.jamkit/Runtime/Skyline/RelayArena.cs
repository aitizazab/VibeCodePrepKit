using System;
using System.Collections.Generic;
using System.Numerics;

namespace JamKit.Skyline
{
    /// <summary>
    /// The Relay's stage as DATA (docs/SKYLINE-RELAY.md): the six stations A to F, the player's start, the lane walls and the
    /// climbing blocks that make the movement kit the fast way, the wave spawn points, and the hall's solids (for the navigation
    /// graph and the headless sim). It sits in M1's main hall (<see cref="GalleriaArena"/>: 50 x 40 m, the mezzanine's deck at 5 m
    /// along the north side). Metres; +Z is north. The Unity director builds exactly the pieces listed here and turns off M1's
    /// own hall cover and the mezzanine's glass upstand (the Relay replaces them).
    /// <para><b>The idea</b> (the creator, 2026-10-14: "the entire point is that you need movement to get between points fast. Any
    /// straight path point should be quite hard, and any easy pathway should be one of the longer routes"): the hall is cut into
    /// five east-west lanes by four <see cref="WallHeight"/> m walls. Each wall stops 5 m short of one end of the hall (the
    /// openings alternate sides, see <see cref="OpeningEast"/>), so walking from lane to lane means going the long way round to
    /// an opening, while the straight line is a jump and a mantle over the wall. The catwalk (station F, on the mezzanine's deck)
    /// has no walk route at all: it is reached by two mantles up the climbing blocks and a third onto the deck's edge. No ramps
    /// and no stairs.</para>
    /// </summary>
    public static class RelayArena
    {
        public const int StationCount = 6;

        /// <summary>The station letters: A to F.</summary>
        public static char Letter(int station) => (char)('A' + station);

        /// <summary>The mezzanine's walkable top (GalleriaArena.MezzanineY).</summary>
        public const float DeckTop = GalleriaArena.MezzanineY;

        /// <summary>Where each station stands (the feet-level centre of its pad). A to E are in lanes 1 to 5 (south to north), F is
        /// the catwalk. Every pair is at least 15 m apart (3D), and the places are chosen so that for every pair the straight
        /// line crosses lane walls while the walk goes round their openings (see <c>RelaySimTests</c> for the numbers).</summary>
        public static readonly Vector3[] Stations =
        {
            new Vector3(16f, 0f, -16f),     // A: lane 1, east
            new Vector3(-2f, 0f, -8f),      // B: lane 2, middle
            new Vector3(-20f, 0f, 0f),      // C: lane 3, west end
            new Vector3(-4f, 0f, 8f),       // D: lane 4, middle
            new Vector3(16f, 0f, 16f),      // E: lane 5, under the mezzanine, east
            new Vector3(-18f, DeckTop, 17f),// F: the catwalk (mezzanine deck), north-west
        };

        /// <summary>Where the Unit starts a run: just inside the hall's south doorway, in lane 1, facing north.</summary>
        public static readonly Vector3 PlayerStart = new Vector3(0f, 0f, -17f);

        /// <summary>Which stations are high (on the catwalk).</summary>
        public static bool IsHigh(int station) => Stations[station].Y > 1f;

        // ------------------------------------------------------------------ the lane walls

        /// <summary>The walls' z (their centre lines), south to north: lane 1 is z -20..-12, lane 5 z 12..20.</summary>
        public static readonly float[] LaneWallZ = { -12f, -4f, 4f, 12f };

        /// <summary>Per wall: true when the opening is at the east end (the wall runs from the west wall to x 20), false at the west end.</summary>
        public static readonly bool[] OpeningEast = { false, true, true, false };

        public const float WallHeight = 2.2f, WallThickness = 1.0f, OpeningWidth = 5f;

        /// <summary>Wall <paramref name="k"/> as a box (its opening is the stretch of the hall it leaves clear).</summary>
        public static CoverBox LaneWall(int k)
        {
            float a = OpeningEast[k] ? GalleriaArena.HallMinX : GalleriaArena.HallMinX + OpeningWidth;
            float b = OpeningEast[k] ? GalleriaArena.HallMaxX - OpeningWidth : GalleriaArena.HallMaxX;
            return new CoverBox
            {
                min = new Vector3(a, 0f, LaneWallZ[k] - WallThickness * 0.5f),
                max = new Vector3(b, WallHeight, LaneWallZ[k] + WallThickness * 0.5f),
            };
        }

        // ------------------------------------------------------------------ the climbing blocks to the catwalk

        /// <summary>Two blocks in lane 4 against the mezzanine's south edge: the first top is a jump and a mantle from the floor, the
        /// second a mantle from the first, and the deck's edge a mantle from the second (the deck's glass upstand is turned off in
        /// the Relay). Walkers cannot climb any of it.</summary>
        public const float Block1Top = 2.4f, Block2Top = 4.0f, ChainMinX = -14.5f, ChainMaxX = -11.5f, ChainZ0 = 7.0f, ChainZMid = 10.0f, ChainZ1 = 12.0f;

        public static CoverBox Block1() => new CoverBox { min = new Vector3(ChainMinX, 0f, ChainZ0), max = new Vector3(ChainMaxX, Block1Top, ChainZMid) };
        public static CoverBox Block2() => new CoverBox { min = new Vector3(ChainMinX, 0f, ChainZMid), max = new Vector3(ChainMaxX, Block2Top, ChainZ1) };

        /// <summary>The pieces the director builds on top of the hall (the lane walls and the two blocks).</summary>
        public static List<FloorSolid> StageSolids()
        {
            var l = new List<FloorSolid>();
            for (int k = 0; k < LaneWallZ.Length; k++) l.Add(Solid("lane-wall-" + k, "wall", LaneWall(k).min, LaneWall(k).max, false));
            l.Add(Solid("climb-1", "wall", Block1().min, Block1().max, false));
            l.Add(Solid("climb-2", "wall", Block2().min, Block2().max, false));
            return l;
        }

        // ------------------------------------------------------------------ the hall's solids

        const float HallHeight = 14f, Thick = 0.4f;

        /// <summary>The main hall as solids, for <see cref="ArenaNav"/> and the headless sim: the floor slab, the four walls, the
        /// mezzanine's deck, the lift lobby, and <see cref="StageSolids"/>. M1's own cover and the deck's glass upstand are not
        /// here (the Relay turns them off). The warm-up hall is not included.</summary>
        public static List<FloorSolid> Solids()
        {
            float x0 = GalleriaArena.HallMinX, x1 = GalleriaArena.HallMaxX, z0 = GalleriaArena.HallMinZ, z1 = GalleriaArena.HallMaxZ;
            var l = new List<FloorSolid>
            {
                Solid("hall-floor", "floor", new Vector3(x0, -0.5f, z0), new Vector3(x1, 0f, z1), false),
                Solid("wall-west", "wall", new Vector3(x0 - Thick, 0f, z0 - Thick), new Vector3(x0, HallHeight, z1 + Thick), true),
                Solid("wall-east", "wall", new Vector3(x1, 0f, z0 - Thick), new Vector3(x1 + Thick, HallHeight, z1 + Thick), true),
                Solid("wall-north", "wall", new Vector3(x0, 0f, z1), new Vector3(x1, HallHeight, z1 + Thick), true),
                Solid("wall-south", "wall", new Vector3(x0, 0f, z0 - Thick), new Vector3(x1, HallHeight, z0), true),
            };
            var deck = GalleriaArena.MezzanineDeck;
            l.Add(Solid("mezzanine-deck", "deck", deck.min, deck.max, false));
            var lobby = GalleriaArena.LiftLobby;
            l.Add(Solid("lift-lobby", "lift", lobby.min, lobby.max, false));
            l.AddRange(StageSolids());
            return l;
        }

        static FloorSolid Solid(string id, string role, Vector3 min, Vector3 max, bool runnable) => new FloorSolid
        {
            id = id, role = role, runnable = runnable, box = new CoverBox { min = min, max = max },
        };

        // ------------------------------------------------------------------ the waves' spawn points

        /// <summary>Where drones may appear (hover height, above the walls); each is at least 4 m from every station and clear of the blocks.</summary>
        public static readonly Vector3[] AirSpawns =
        {
            new Vector3(-22f, GalleriaArena.DroneY, -18f), new Vector3(-8f, GalleriaArena.DroneY, -18f), new Vector3(6f, GalleriaArena.DroneY, -18f),
            new Vector3(22f, GalleriaArena.DroneY, -10f), new Vector3(-22f, GalleriaArena.DroneY, -8f), new Vector3(10f, GalleriaArena.DroneY, -8f),
            new Vector3(-12f, GalleriaArena.DroneY, -2f), new Vector3(6f, GalleriaArena.DroneY, 0f), new Vector3(22f, GalleriaArena.DroneY, 2f),
            new Vector3(-22f, GalleriaArena.DroneY, 5f), new Vector3(10f, GalleriaArena.DroneY, 8f), new Vector3(22f, GalleriaArena.DroneY, 9f),
        };

        /// <summary>Where floor-care units may appear (on the floor, each in a lane), away from the stations.</summary>
        public static readonly Vector3[] FloorSpawns =
        {
            new Vector3(-20f, 0f, -16f), new Vector3(-6f, 0f, -16f), new Vector3(12f, 0f, -8f), new Vector3(-20f, 0f, -8f),
            new Vector3(8f, 0f, 0f), new Vector3(-8f, 0f, 0f), new Vector3(10f, 0f, 8f), new Vector3(-8f, 0f, 16f),
        };
    }
}
