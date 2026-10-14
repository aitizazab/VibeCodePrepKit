using System.Collections.Generic;
using System.Numerics;

namespace JamKit.Skyline
{
    /// <summary>
    /// The Halcyon Grand's lobby and the staff hallway behind it, as DATA (docs/SKYLINE-PLAN-FLOOR1.md, Task L1;
    /// docs/SKYLINE-FLOOR-REPOSSESS.md section 9): floor 1's fixed entrance, hand-made, replacing M1's warm-up hall. The
    /// Unity builder (B2), the lobby runtime (L2) and the tests read these numbers; nothing here hard-codes a scene.
    /// <para>Metres; +Z is north, the way the player faces at the start. The lobby's north wall is the Food Court's south
    /// wall (outer face z = -28.4): that wall belongs to <c>RepossessArena</c> and is NOT in <see cref="Solids"/>, so no
    /// collider is built twice. The lift cab stands against it; its far (north) doors are <c>cab-doors-n</c> here.</para>
    /// <para>Roles added to the floor's list (docs/SKYLINE-PLAN-FLOOR1.md D19e): <c>desk cart stack counter locker gantry
    /// stairs</c>; the lobby also uses <c>floor wall lift column deck balustrade</c>.</para>
    /// </summary>
    public static class HotelLobby
    {
        /// <summary>The lobby's interior (the walls stand 0.4 m outside it) and its height.</summary>
        public const float MinX = -22f, MaxX = 22f, MinZ = -60f, MaxZ = -28.4f, Height = 9f, WallThick = 0.4f;

        /// <summary>The staff hallway: 3 m wide (x -1.5..1.5), 18 m long, joining the lobby's south wall at z = -60.4 (that
        /// wall is 0.4 m thick, z -60.4..-60), 3.2 m to its ceiling.</summary>
        public const float HallwayHalfWidth = 1.5f, HallwayMinZ = -78.4f, HallwayMaxZ = -60.4f, HallwayHeight = 3.2f;

        /// <summary>The Unit's start: the far end of the hallway, facing +Z (yaw 0), the lobby glowing at the end.</summary>
        public static readonly Vector3 PlayerStart = new Vector3(0f, 0f, -76f);

        /// <summary>The practice drone (never attacks): the bellhop demo of the Writ's spots, hanging from a ceiling rail.</summary>
        public static readonly Vector3 PracticeBot = new Vector3(-12f, 1.4f, -50f);

        /// <summary>The rail the bellhop rig runs on: a beam under the ceiling (its top is <see cref="GantryTopY"/>, clear of
        /// the 9 m ceiling and out of every jump).</summary>
        public const float GantryBottomY = 7.6f, GantryTopY = 8f;

        /// <summary>The luggage stack (the boost ledge): its top is reachable only with a Breacher boost. 12 m or more
        /// (edge to edge) from every wall and from the other ledges (<see cref="GalleriaArena.LedgeWallClearance"/>).</summary>
        public static readonly Vector3 BoostStackTop = new Vector3(-4f, 4.6f, -46f);

        /// <summary>The practice counter: its top (3.5) is above a jump and a mantle and below a jump, a double jump and a
        /// mantle (4.15).</summary>
        public static readonly Vector3 PracticeCounterTop = new Vector3(14f, 3.5f, -56f);

        /// <summary>The front desk's centre, on its top: CONCIERGE's screen hangs above it; its first greeting plays when the
        /// player arrives within <see cref="DeskGreetRange"/> m of it in sight.</summary>
        public static readonly Vector3 DeskCenter = new Vector3(-10f, 1.1f, -32.25f);
        public const float DeskGreetRange = 8f;

        /// <summary>The chandelier (a light, not a solid): the light that takes the room over.</summary>
        public static readonly Vector3 ChandelierPosition = new Vector3(0f, 7f, -44f);

        /// <summary>The wall-run lockers, 1.5 m off the west wall: their top (4.6) is out of a jump, a double jump and a mantle
        /// (4.15). Run the west wall north from <see cref="WallRunStart"/>, kick off it toward the lockers and pull up.</summary>
        public static readonly CoverBox WallRunLockers = Box(-20.5f, -18f, 0f, 4.6f, -36f, -34f);
        public static readonly Vector3 WallRunStart = new Vector3(MinX + 0.6f, 0f, -52f);

        /// <summary>The lift cab's inside (4 x 4 m): its north face is the Food Court's south wall line (z = -28.4). It is open
        /// to the lobby on its south side; its north doors are <c>cab-doors-n</c>. The Unit's feet here (once the doors have
        /// opened for the player to walk in) are SteppedIntoLift, which starts the opening sequence on the next bar.</summary>
        public static readonly CoverBox LiftCab = Box(-2f, 2f, 0f, 3.2f, -32.4f, -28.4f);

        /// <summary>The cab's west/east walls are 0.4 m thick, outside the 4 m inside.</summary>
        public const float CabWallThick = 0.4f;

        static CoverBox Box(float x0, float x1, float y0, float y1, float z0, float z1) =>
            new CoverBox { min = new Vector3(x0, y0, z0), max = new Vector3(x1, y1, z1) };

        static FloorSolid S(string id, string role, float x0, float x1, float y0, float y1, float z0, float z1,
            bool runnable = false, bool glass = false) =>
            new FloorSolid { id = id, role = role, box = Box(x0, x1, y0, y1, z0, z1), runnable = runnable, glass = glass };

        /// <summary>Every collider of the hallway and the lobby, once: floors, walls (the west wall is the only runnable one),
        /// ceiling, the mezzanine lounge with its stairs, the front desk, the luggage carts (low cover), the boost stack, the
        /// practice counter, the wall-run lockers, the bellhop rig's rail and the lift cab. Never overlaps by more than
        /// 0.02 m. The Food Court's south wall (the lobby's north wall) is not here.</summary>
        public static FloorSolid[] Solids()
        {
            float hx = HallwayHalfWidth, ht = WallThick;
            var l = new List<FloorSolid>
            {
                // the hallway
                S("hallway-floor", "floor", -hx - ht, hx + ht, -1f, 0f, HallwayMinZ - ht, HallwayMaxZ),
                S("hallway-wall-w", "wall", -hx - ht, -hx, 0f, HallwayHeight, HallwayMinZ - ht, HallwayMaxZ),
                S("hallway-wall-e", "wall", hx, hx + ht, 0f, HallwayHeight, HallwayMinZ - ht, HallwayMaxZ),
                S("hallway-wall-end", "wall", -hx, hx, 0f, HallwayHeight, HallwayMinZ - ht, HallwayMinZ),
                S("hallway-ceiling", "wall", -hx - ht, hx + ht, HallwayHeight, HallwayHeight + ht, HallwayMinZ - ht, HallwayMaxZ),
                // the lobby's shell (its north wall is the Food Court's south wall)
                S("lobby-floor", "floor", MinX - ht, MaxX + ht, -1f, 0f, MinZ - ht, MaxZ),
                S("lobby-wall-w", "wall", MinX - ht, MinX, 0f, Height, MinZ - ht, MaxZ, runnable: true),
                S("lobby-wall-e", "wall", MaxX, MaxX + ht, 0f, Height, MinZ - ht, MaxZ),
                S("lobby-wall-s-w", "wall", MinX, -hx, 0f, Height, MinZ - ht, MinZ),
                S("lobby-wall-s-e", "wall", hx, MaxX, 0f, Height, MinZ - ht, MinZ),
                S("lobby-wall-s-lintel", "wall", -hx, hx, HallwayHeight, Height, MinZ - ht, MinZ),
                S("lobby-ceiling", "wall", MinX - ht, MaxX + ht, Height, Height + ht, MinZ - ht, MaxZ),
                // the mezzanine lounge (east side), its columns, its glass rails and the stairs at its south end
                S("mezzanine-deck", "deck", 14f, 22f, 4.1f, 4.5f, -52f, -34f),
                S("mezzanine-col-sw", "column", 14f, 14.6f, 0f, 4.1f, -52f, -51.4f),
                S("mezzanine-col-nw", "column", 14f, 14.6f, 0f, 4.1f, -34.6f, -34f),
                S("mezzanine-col-se", "column", 21.4f, 22f, 0f, 4.1f, -52f, -51.4f),
                S("mezzanine-col-ne", "column", 21.4f, 22f, 0f, 4.1f, -34.6f, -34f),
                S("mezzanine-rail-w", "balustrade", 14f, 14.15f, 4.5f, 5.5f, -52f, -34f, glass: true),
                S("mezzanine-rail-n", "balustrade", 14.15f, 22f, 4.5f, 5.5f, -34.15f, -34f, glass: true),
                new FloorSolid
                {
                    id = "mezzanine-stairs", role = "stairs", box = Box(17f, 21f, 0f, 4.5f, -59.5f, -52f),
                    ramp = true, rampAlongX = false, rampTopStart = 0f, rampTopEnd = 4.5f,
                },
                // the front desk (CONCIERGE's screen hangs above it) and the luggage carts (low cover, 1.1 m)
                S("front-desk", "desk", -14f, -6f, 0f, 1.1f, -33.5f, -31f),
                S("cart-1", "cart", -10.9f, -9.1f, 0f, 1.1f, -41.45f, -40.55f),
                S("cart-2", "cart", 8.1f, 9.9f, 0f, 1.1f, -45.45f, -44.55f),
                S("cart-3", "cart", 7.1f, 8.9f, 0f, 1.1f, -38.45f, -37.55f),
                S("cart-4", "cart", -15.9f, -14.1f, 0f, 1.1f, -55.45f, -54.55f),
                // the boost stack and the practice counter (squares of the M1 ledge sizes), the lockers, the bellhop rail
                S("boost-stack", "stack", BoostStackTop.X - GalleriaArena.BoostLedgeSize * 0.5f, BoostStackTop.X + GalleriaArena.BoostLedgeSize * 0.5f,
                    0f, BoostStackTop.Y, BoostStackTop.Z - GalleriaArena.BoostLedgeSize * 0.5f, BoostStackTop.Z + GalleriaArena.BoostLedgeSize * 0.5f),
                S("practice-counter", "counter", PracticeCounterTop.X - GalleriaArena.PracticeLedgeSize * 0.5f, PracticeCounterTop.X + GalleriaArena.PracticeLedgeSize * 0.5f,
                    0f, PracticeCounterTop.Y, PracticeCounterTop.Z - GalleriaArena.PracticeLedgeSize * 0.5f, PracticeCounterTop.Z + GalleriaArena.PracticeLedgeSize * 0.5f),
                S("wall-run-lockers", "locker", WallRunLockers.min.X, WallRunLockers.max.X, WallRunLockers.min.Y, WallRunLockers.max.Y,
                    WallRunLockers.min.Z, WallRunLockers.max.Z),
                S("bellhop-rail", "gantry", -16f, -8f, GantryBottomY, GantryTopY, PracticeBot.Z - 0.2f, PracticeBot.Z + 0.2f),
                // the lift cab: 4 x 4 inside, walls 0.4 m, a roof, and the north doors (the Food Court's wall behind them)
                S("cab-wall-w", "lift", LiftCab.min.X - CabWallThick, LiftCab.min.X, 0f, LiftCab.max.Y, LiftCab.min.Z, LiftCab.max.Z),
                S("cab-wall-e", "lift", LiftCab.max.X, LiftCab.max.X + CabWallThick, 0f, LiftCab.max.Y, LiftCab.min.Z, LiftCab.max.Z),
                S("cab-roof", "lift", LiftCab.min.X - CabWallThick, LiftCab.max.X + CabWallThick, LiftCab.max.Y, LiftCab.max.Y + CabWallThick,
                    LiftCab.min.Z, LiftCab.max.Z),
                S("cab-doors-n", "lift", LiftCab.min.X, LiftCab.max.X, 0f, LiftCab.max.Y, LiftCab.max.Z - 0.2f, LiftCab.max.Z),
            };
            return l.ToArray();
        }

        /// <summary>The named areas (level, y): the staff hallway (a service space: the colour-free zone's greys), the lobby,
        /// the mezzanine lounge and the lift cab.</summary>
        public static FloorZone[] Zones() => new[]
        {
            new FloorZone { id = "hallway", level = SurfaceLevel.Corridor, minX = -HallwayHalfWidth, maxX = HallwayHalfWidth, minZ = HallwayMinZ, maxZ = HallwayMaxZ, y = 0f },
            new FloorZone { id = "lobby", level = SurfaceLevel.Lobby, minX = MinX, maxX = MaxX, minZ = MinZ, maxZ = MaxZ, y = 0f },
            new FloorZone { id = "mezzanine", level = SurfaceLevel.Lobby, minX = 14f, maxX = 22f, minZ = -52f, maxZ = -34f, y = 4.5f },
            new FloorZone { id = "cab", level = SurfaceLevel.Lobby, minX = LiftCab.min.X, maxX = LiftCab.max.X, minZ = LiftCab.min.Z, maxZ = LiftCab.max.Z, y = 0f },
        };
    }
}
