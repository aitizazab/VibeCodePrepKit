using System;
using System.Collections.Generic;
using System.Numerics;

namespace JamKit.Skyline
{
    /// <summary>
    /// The Relay's stage as DATA (docs/SKYLINE-RELAY.md): the six stations A to F, the player's start, the ramp up to the
    /// mezzanine (the catwalk), the wave spawn points, and the hall's solids (for the navigation graph and the headless sim).
    /// It sits in M1's main hall (<see cref="GalleriaArena"/>: 50 x 40 m, the mezzanine's deck at 5 m along the north side).
    /// The mezzanine has no way up in M1 (the deck is reached only with the movement kit's tricks), so the Relay adds a
    /// walkable ramp up the west wall: <see cref="RampSolids"/>. The Unity director builds exactly those pieces.
    /// Metres; +Z is north.
    /// </summary>
    public static class RelayArena
    {
        public const int StationCount = 6;

        /// <summary>The station letters: A to F.</summary>
        public static char Letter(int station) => (char)('A' + station);

        /// <summary>The mezzanine's walkable top (GalleriaArena.MezzanineY).</summary>
        public const float DeckTop = GalleriaArena.MezzanineY;

        /// <summary>Where each station stands (the feet-level centre of its pad). A and B are the hall's south corners, C is the
        /// middle of the hall, D the north-west floor under the mezzanine's edge, E the north-east floor, F the
        /// catwalk (the mezzanine deck) in the north. Every pair is at least 15 m apart, so no order puts two consecutive
        /// stations close together.</summary>
        public static readonly Vector3[] Stations =
        {
            new Vector3(-21f, 0f, -14f),    // A: south-west corner
            new Vector3(21f, 0f, -14f),     // B: south-east corner
            new Vector3(0f, 0f, 3.5f),      // C: the middle of the hall, north of the stall backs, clear of the kiosks
            new Vector3(-15f, 0f, 6f),      // D: north-west floor, in front of the mezzanine's edge
            new Vector3(21f, 0f, 8f),       // E: north-east floor, the far corner from the entrance
            new Vector3(0f, DeckTop, 18f),  // F: the catwalk (mezzanine deck), in the north
        };

        /// <summary>Where the Unit starts a run: just inside the hall's south doorway, facing north.</summary>
        public static readonly Vector3 PlayerStart = new Vector3(0f, 0f, -17f);

        /// <summary>Which stations are high (on the catwalk).</summary>
        public static bool IsHigh(int station) => Stations[station].Y > 1f;

        // ------------------------------------------------------------------ the ramp up to the catwalk

        public const float RampMinX = -25f, RampMaxX = -19f, RampStartZ = -2f, RampUpEndZ = 12f, RampLandingEndZ = 14f, RampDownEndZ = 18f,
            RampCrestY = 6.4f;

        /// <summary>The walkable ramp up the west wall to the mezzanine: an up ramp from the floor at z -2 to a landing 6.4 m up
        /// (z 12..14, well over the deck's glass upstand, 5.55 m, so the climb never meets its face), then a ramp down onto the
        /// deck at z 18. About 24 degrees up and 19 down; the landing is flat so the walkable graph can follow it.</summary>
        public static FloorSolid[] RampSolids() => new[]
        {
            new FloorSolid
            {
                id = "relay-ramp-up", role = "ramp", ramp = true, rampAlongX = false, rampTopStart = 0f, rampTopEnd = RampCrestY,
                box = new CoverBox { min = new Vector3(RampMinX, 0f, RampStartZ), max = new Vector3(RampMaxX, RampCrestY, RampUpEndZ) },
            },
            new FloorSolid
            {
                id = "relay-landing", role = "deck",
                box = new CoverBox { min = new Vector3(RampMinX, 0f, RampUpEndZ), max = new Vector3(RampMaxX, RampCrestY, RampLandingEndZ) },
            },
            new FloorSolid
            {
                id = "relay-ramp-down", role = "ramp", ramp = true, rampAlongX = false, rampTopStart = RampCrestY, rampTopEnd = DeckTop,
                box = new CoverBox { min = new Vector3(RampMinX, 0f, RampLandingEndZ), max = new Vector3(RampMaxX, RampCrestY, RampDownEndZ) },
            },
        };

        // ------------------------------------------------------------------ the hall's solids

        const float HallHeight = 14f, Thick = 0.4f;

        /// <summary>The main hall as solids, for <see cref="ArenaNav"/> and the headless sim: the floor slab, the four walls, the
        /// cover (<see cref="GalleriaArena.HallCover"/>), the mezzanine's deck with its glass upstand, the lift lobby and the
        /// Relay's ramp. The warm-up hall is not included (the Relay stays in the hall).</summary>
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
            var cover = GalleriaArena.HallCover();
            for (int i = 0; i < cover.Length; i++)
            {
                float h = cover[i].max.Y;
                string role = h <= 1.1f ? "planter" : h <= 2.6f ? "kiosk" : "pillar";
                l.Add(Solid("cover-" + i, role, cover[i].min, cover[i].max, h >= 4f));
            }
            var deck = GalleriaArena.MezzanineDeck;
            l.Add(Solid("mezzanine-deck", "deck", deck.min, deck.max, false));
            var rail = GalleriaArena.MezzanineRail()[0];
            var glass = Solid("mezzanine-balustrade", "balustrade", rail.min, rail.max, false);
            glass.glass = true;
            l.Add(glass);
            var lobby = GalleriaArena.LiftLobby;
            l.Add(Solid("lift-lobby", "lift", lobby.min, lobby.max, false));
            l.AddRange(RampSolids());
            return l;
        }

        static FloorSolid Solid(string id, string role, Vector3 min, Vector3 max, bool runnable) => new FloorSolid
        {
            id = id, role = role, runnable = runnable, box = new CoverBox { min = min, max = max },
        };

        // ------------------------------------------------------------------ the waves' spawn points

        /// <summary>Where drones may appear (hover height), along the side walls and the middle of the hall (x +-21 and +-12);
        /// each is at least 4 m from every station and well clear of the cover.</summary>
        public static readonly Vector3[] AirSpawns =
        {
            new Vector3(-21f, GalleriaArena.DroneY, -8f), new Vector3(-16f, GalleriaArena.DroneY, -2f), new Vector3(-12f, GalleriaArena.DroneY, 2f),
            new Vector3(-21f, GalleriaArena.DroneY, -18f),
            new Vector3(21f, GalleriaArena.DroneY, -8f), new Vector3(21f, GalleriaArena.DroneY, -1f), new Vector3(21f, GalleriaArena.DroneY, 3f),
            new Vector3(21f, GalleriaArena.DroneY, -18f),
            new Vector3(-12f, GalleriaArena.DroneY, -17f), new Vector3(12f, GalleriaArena.DroneY, -17f),
            new Vector3(-9f, 4.2f, 9f), new Vector3(9f, 4.2f, 9f),
            new Vector3(-12f, GalleriaArena.DroneY, 0f), new Vector3(12f, GalleriaArena.DroneY, -1f),
        };

        /// <summary>Where floor-care units may appear (on the floor), away from the stations and the ramp.</summary>
        public static readonly Vector3[] FloorSpawns =
        {
            new Vector3(-21f, 0f, -18f), new Vector3(21f, 0f, -18f), new Vector3(-21f, 0f, -8f), new Vector3(21f, 0f, -8f),
            new Vector3(-10f, 0f, 9f), new Vector3(10f, 0f, 9f), new Vector3(-12f, 0f, -5f), new Vector3(12f, 0f, -5f),
        };
    }
}
