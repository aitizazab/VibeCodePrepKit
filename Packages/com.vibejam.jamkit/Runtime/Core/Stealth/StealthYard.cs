using System;
using System.Collections.Generic;
using System.Numerics;

namespace JamKit.Stealth
{
    public enum YardPieceKind { Floor, Wall, RunWall, Cover, Ledge, Roof, Bar, Post, Bush, Canopy, Curtain }

    /// <summary>One box of the yard. Bushes, canopies and curtains are seen but not solid (no collider, no sight block:
    /// a bush hides through its <see cref="HidingSpot"/> instead).</summary>
    public sealed class YardPiece
    {
        public string name;
        public YardPieceKind kind;
        public Vector3 center, size;
        public bool Solid => kind != YardPieceKind.Bush && kind != YardPieceKind.Canopy && kind != YardPieceKind.Curtain;
        public Vector3 Min => center - size * 0.5f;
        public Vector3 Max => center + size * 0.5f;
    }

    public sealed class YardLight
    {
        public string name;
        public Vector3 center, size;
        public float level;
    }

    public sealed class YardCollectible
    {
        public string id, route;
        public Vector3 position;
    }

    /// <summary>
    /// The Stealth Lab's yard (docs/STEALTH.md "The Stealth Lab"), as data: a fenced 48 × 36 m garden with a house, a
    /// shed, a deck, a wardrobe-closet, three bushes, low cover walls, a lamp-lit middle and dark corners, a seeker's
    /// walking graph and patrol loop, and five collectibles each reached a different way (a mantle up crates, a wallrun,
    /// a slide under the deck, inside the closet, the dark alley). The builder makes the scene from it; the core tests run
    /// the seeker in it through <see cref="StealthBoxWorld"/>. Ground top at y 0; the seeker walks on it.
    /// </summary>
    public sealed class StealthYard
    {
        public readonly List<YardPiece> pieces = new List<YardPiece>();
        public readonly List<HidingSpot> spots = new List<HidingSpot>();
        public readonly List<YardLight> lights = new List<YardLight>();
        public readonly List<YardCollectible> collectibles = new List<YardCollectible>();
        public readonly List<Vector3> nodes = new List<Vector3>();
        public readonly List<int> patrol = new List<int>();
        /// <summary>Two more guards (the lab's three-guard option): overlapping loops of the west and east halves.</summary>
        public readonly List<int> patrol2 = new List<int>(), patrol3 = new List<int>();
        public Vector3 guard2Start, guard3Start;
        /// <summary>The checkpoints (docs/STEALTH.md "Being caught"): the start, the dark corner under the canopy and the
        /// alley behind the house. A patrol round's catch sends her back to the last one she reached.</summary>
        public readonly List<StealthCheckpoint> checkpoints = new List<StealthCheckpoint>();
        public float ambientLight = 0.75f;
        public Vector3 playerStart, seekerStart, lampPosition;
        public float playerYaw, seekerYaw, countYaw;
        /// <summary>Graph nodes within this distance with a clear walk are joined.</summary>
        public float connectDistance = 16f;

        public const float HalfX = 24f, HalfZ = 18f;

        public static StealthYard Build()
        {
            var y = new StealthYard();
            y.Make();
            return y;
        }

        void Piece(string name, YardPieceKind kind, Vector3 center, Vector3 size)
            => pieces.Add(new YardPiece { name = name, kind = kind, center = center, size = size });

        static Vector3 V(float x, float yy, float z) => new Vector3(x, yy, z);

        void Make()
        {
            // Ground and the fence around it (4 m: the perimeter is runnable).
            Piece("Ground", YardPieceKind.Floor, V(0f, -0.5f, 0f), V(HalfX * 2f + 4f, 1f, HalfZ * 2f + 4f));
            Piece("Fence N", YardPieceKind.RunWall, V(0f, 2f, HalfZ + 1.5f), V(HalfX * 2f + 2f, 4f, 1f));
            Piece("Fence S", YardPieceKind.RunWall, V(0f, 2f, -HalfZ - 1.5f), V(HalfX * 2f + 2f, 4f, 1f));
            Piece("Fence W", YardPieceKind.RunWall, V(-HalfX - 1.5f, 2f, 0f), V(1f, 4f, HalfZ * 2f + 4f));
            Piece("Fence E", YardPieceKind.RunWall, V(HalfX + 1.5f, 2f, 0f), V(1f, 4f, HalfZ * 2f + 4f));

            // The house (solid, 5 m; its 12 m south face is runnable) and the dark alley behind it.
            Piece("House", YardPieceKind.Wall, V(-16f, 2.5f, 12.5f), V(12f, 5f, 9f));

            // The wardrobe-closet against the house's east face: walk in through the 1 m door.
            Piece("Closet N", YardPieceKind.Wall, V(-9.05f, 1.2f, 11.25f), V(1.9f, 2.4f, 0.3f));
            Piece("Closet S", YardPieceKind.Wall, V(-9.05f, 1.2f, 9.35f), V(1.9f, 2.4f, 0.3f));
            Piece("Closet jamb S", YardPieceKind.Wall, V(-8.25f, 1.2f, 9.65f), V(0.3f, 2.4f, 0.3f));
            Piece("Closet jamb N", YardPieceKind.Wall, V(-8.25f, 1.2f, 10.95f), V(0.3f, 2.4f, 0.3f));
            Piece("Closet lintel", YardPieceKind.Wall, V(-8.25f, 2.1f, 10.3f), V(0.3f, 0.6f, 1f));
            Piece("Closet roof", YardPieceKind.Roof, V(-9.05f, 2.55f, 10.3f), V(1.9f, 0.3f, 2.2f));
            Piece("Closet curtain", YardPieceKind.Curtain, V(-8.25f, 0.9f, 10.3f), V(0.05f, 1.8f, 1f));
            spots.Add(HidingSpot.Box("Closet", HideKind.Closet, V(-9.2f, 0.9f, 10.3f), V(1.5f, 1.8f, 1.5f), V(-7.1f, 0f, 10.3f)));

            // The shed (3 m) and the crates that mantle up onto its roof.
            Piece("Shed", YardPieceKind.Wall, V(15f, 1.5f, 13f), V(6f, 3f, 6f));
            Piece("Crate", YardPieceKind.Ledge, V(11.2f, 0.6f, 11f), V(1.6f, 1.2f, 1.6f));

            // The long freestanding wall (wallrun along its east face) and the lookout past its north end.
            Piece("Run wall", YardPieceKind.RunWall, V(4f, 2f, -5f), V(0.6f, 4f, 14f));
            Piece("Lookout", YardPieceKind.Ledge, V(7.5f, 1.6f, 4.5f), V(3f, 3.2f, 3f));

            // The deck: slide (or crawl) under it; its top is a 1.3 m ledge.
            Piece("Deck", YardPieceKind.Roof, V(-4f, 1.225f, -13f), V(6f, 0.15f, 4f));
            Piece("Deck post SW", YardPieceKind.Post, V(-6.85f, 0.575f, -14.85f), V(0.2f, 1.15f, 0.2f));
            Piece("Deck post SE", YardPieceKind.Post, V(-1.15f, 0.575f, -14.85f), V(0.2f, 1.15f, 0.2f));
            Piece("Deck post NW", YardPieceKind.Post, V(-6.85f, 0.575f, -11.15f), V(0.2f, 1.15f, 0.2f));
            Piece("Deck post NE", YardPieceKind.Post, V(-1.15f, 0.575f, -11.15f), V(0.2f, 1.15f, 0.2f));
            spots.Add(HidingSpot.Box("Under the deck", HideKind.Under, V(-4f, 0.575f, -13f), V(5.4f, 1.15f, 3.4f), V(-4f, 0f, -9.6f)));

            // The garden fence with a slide-under gap (a bar at 1.15 m underside, like the Movement Lab's).
            Piece("Garden fence W", YardPieceKind.Wall, V(-10f, 1.25f, -6f), V(4f, 2.5f, 0.4f));
            Piece("Garden fence E", YardPieceKind.Wall, V(-3f, 1.25f, -6f), V(4f, 2.5f, 0.4f));
            Piece("Fence bar", YardPieceKind.Bar, V(-6.5f, 1.825f, -6f), V(3f, 1.35f, 0.4f));

            // Low cover (1.1 m): crouch behind it and the seeker's eye line passes over you.
            Piece("Low wall (middle)", YardPieceKind.Cover, V(-2f, 0.55f, 4f), V(5f, 1.1f, 0.5f));
            Piece("Low wall (east)", YardPieceKind.Cover, V(10f, 0.55f, -4f), V(0.5f, 1.1f, 5f));
            Piece("Low wall (west)", YardPieceKind.Cover, V(-15f, 0.55f, -1f), V(4f, 1.1f, 0.5f));

            // Bushes.
            Piece("Bush SW", YardPieceKind.Bush, V(-20f, 0.8f, -10f), V(2.4f, 1.6f, 2.4f));
            Piece("Bush SE", YardPieceKind.Bush, V(19f, 0.8f, -12f), V(2.4f, 1.6f, 2.4f));
            Piece("Bush N", YardPieceKind.Bush, V(0f, 0.8f, 12f), V(2.6f, 1.6f, 2.6f));
            spots.Add(HidingSpot.Box("Bush SW", HideKind.Bush, V(-20f, 0.8f, -10f), V(2.4f, 1.6f, 2.4f), V(-17.6f, 0f, -10f)));
            spots.Add(HidingSpot.Box("Bush SE", HideKind.Bush, V(19f, 0.8f, -12f), V(2.4f, 1.6f, 2.4f), V(16.6f, 0f, -12f)));
            spots.Add(HidingSpot.Box("Bush N", HideKind.Bush, V(0f, 0.8f, 12f), V(2.6f, 1.6f, 2.6f), V(0f, 0f, 9.4f)));

            // Light: a lamp-lit middle, a dark corner under a canopy (south-east), the dark alley behind the house.
            lampPosition = V(0f, 3.6f, -2f);
            lights.Add(new YardLight { name = "Lamp", center = V(0f, 2f, -2f), size = V(10f, 4f, 10f), level = 1f });
            lights.Add(new YardLight { name = "Canopy shade", center = V(18f, 2f, -13f), size = V(9f, 4f, 8f), level = 0.15f });
            lights.Add(new YardLight { name = "Alley", center = V(-16f, 2f, 18f), size = V(16f, 4f, 2.2f), level = 0.1f });
            Piece("Canopy", YardPieceKind.Canopy, V(18f, 3.8f, -13f), V(9f, 0.3f, 8f));

            // The collectibles, each a different route.
            collectibles.Add(new YardCollectible { id = "shed-roof", route = "mantle: crate, then the shed roof", position = V(15f, 3.5f, 13f) });
            collectibles.Add(new YardCollectible { id = "lookout", route = "wallrun the long wall, jump to the lookout", position = V(7.5f, 3.7f, 4.5f) });
            collectibles.Add(new YardCollectible { id = "under-deck", route = "slide under the deck", position = V(-4f, 0.45f, -13f) });
            collectibles.Add(new YardCollectible { id = "closet", route = "inside the closet", position = V(-9.2f, 0.45f, 10.3f) });
            collectibles.Add(new YardCollectible { id = "alley", route = "the dark alley behind the house", position = V(-21f, 0.45f, 18f) });

            // The seeker's walking graph (on the ground) and a patrol loop.
            nodes.Add(V(0f, 0f, -2f));      // 0 the lamp
            nodes.Add(V(-10f, 0f, -10f));   // 1 south-west lawn
            nodes.Add(V(8f, 0f, -10f));     // 2 south-east lawn
            nodes.Add(V(14f, 0f, -2f));     // 3 east
            nodes.Add(V(21f, 0f, 5f));      // 4 east, by the shed
            nodes.Add(V(8f, 0f, 9f));       // 5 north middle
            nodes.Add(V(-4f, 0f, 8f));      // 6 north of the low wall
            nodes.Add(V(-6f, 0f, 15f));     // 7 north-east of the house
            nodes.Add(V(-19f, 0f, 4f));     // 8 west, south of the house
            nodes.Add(V(-20f, 0f, -6f));    // 9 west
            nodes.Add(V(-16f, 0f, -14f));   // 10 south-west corner
            nodes.Add(V(5f, 0f, -16f));     // 11 south of the run wall
            nodes.Add(V(20f, 0f, -16f));    // 12 south-east corner
            nodes.Add(V(21.5f, 0f, 16.5f)); // 13 north-east corner
            nodes.Add(V(-23.5f, 0f, 6f));   // 14 the west gap, south end
            nodes.Add(V(-16f, 0f, 18f));    // 15 the alley
            nodes.Add(V(-8f, 0f, 18f));     // 16 the alley's east end
            nodes.Add(V(1f, 0f, -9f));      // 17 between the garden fence and the run wall
            nodes.Add(V(-23.5f, 0f, 18f));  // 18 the west gap's north end, into the alley
            patrol.AddRange(new[] { 0, 17, 11, 2, 3, 4, 5, 6, 8, 9, 1, 17 });
            patrol2.AddRange(new[] { 8, 9, 10, 1, 17, 0, 6 });
            patrol3.AddRange(new[] { 3, 12, 11, 2, 0, 5, 4, 13 });

            playerStart = V(-20f, 0f, -16f);
            playerYaw = 45f;
            seekerStart = V(0f, 0f, -2f);
            guard2Start = V(-19f, 0f, 4f);
            guard3Start = V(21f, 0f, 5f);
            seekerYaw = 0f;
            countYaw = 180f;

            Checkpoint("The start", playerStart, V(5f, 3f, 5f), playerYaw);
            Checkpoint("Under the canopy", V(21f, 0f, -14f), V(5f, 3f, 5f), -90f);
            Checkpoint("The alley", V(-12f, 0f, 18f), V(6f, 3f, 2f), 90f);
        }

        void Checkpoint(string name, Vector3 feet, Vector3 size, float yaw)
            => checkpoints.Add(new StealthCheckpoint { name = name, center = feet + V(0f, size.Y * 0.5f, 0f), size = size, spawn = feet, yaw = yaw });

        /// <summary>The yard as a <see cref="StealthBoxWorld"/>: every solid piece blocks sight; the light boxes.</summary>
        public StealthBoxWorld World()
        {
            var w = new StealthBoxWorld { ambient = ambientLight };
            foreach (var p in pieces)
                if (p.Solid && p.kind != YardPieceKind.Floor) w.AddBlocker(p.center, p.size);
            foreach (var l in lights) w.AddLight(l.center, l.size, l.level);
            return w;
        }

        /// <summary>The seeker's graph, joined where the walks are clear.</summary>
        public WaypointGraph Graph(IStealthWorld world)
        {
            var g = new WaypointGraph();
            foreach (var n in nodes) g.Add(n);
            g.AutoConnect(world, connectDistance);
            return g;
        }

        public YardPiece Find(string name) => pieces.Find(p => p.name == name);

        public int SpotAt(Vector3 feet)
        {
            for (int i = 0; i < spots.Count; i++) if (spots[i].Contains(feet)) return i;
            return -1;
        }
    }
}
