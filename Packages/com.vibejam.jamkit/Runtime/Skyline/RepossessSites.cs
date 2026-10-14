using System;
using System.Collections.Generic;
using System.Numerics;

namespace JamKit.Skyline
{
    /// <summary>What a clean-line route asks of the player at a waypoint (the base kit: walk, slide, mantle, wall-run, double jump).</summary>
    public enum MoveHintKind { Walk, Slide, Mantle, WallRun, DoubleJump }

    /// <summary>The move needed to REACH waypoint <see cref="atWaypoint"/> from the one before it.</summary>
    public struct MoveHint
    {
        public int atWaypoint;
        public MoveHintKind kind;
    }

    /// <summary>A clean line: waypoints with the moves between them (<see cref="hints"/> is sparse; a waypoint without a hint
    /// is reached by walking). Used by the layout lint and navigation graph (F3) and by the pilot (DM1).</summary>
    public sealed class RouteDef
    {
        public string id = "";
        public Vector3[] waypoints = new Vector3[0];
        public MoveHint[] hints = new MoveHint[0];

        /// <summary>The move that reaches waypoint <paramref name="i"/> (Walk when there is no hint for it).</summary>
        public MoveHintKind HintAt(int i)
        {
            for (int k = 0; k < hints.Length; k++) if (hints[k].atWaypoint == i) return hints[k].kind;
            return MoveHintKind.Walk;
        }
    }

    /// <summary>
    /// Floor 1's sites as DATA (docs/SKYLINE-PLAN-FLOOR1.md, task F2 and the layout sheet): the ten sockets, the 17 spawn doors,
    /// the three draft assets and the clean-line routes. Pure: the Unity builder, the layout lint, the headless sim and the
    /// pilot all read this one source. Every array is a fresh copy of shared data (the objects in it must not be edited).
    /// <para>A tow circle is centred on its socket's <c>position</c> (a floor point: the asset stands above it);
    /// <see cref="Repossession"/> relies on exactly that.</para>
    /// <para>Route waypoints are feet positions. A waypoint reached by a <see cref="MoveHintKind.WallRun"/> or
    /// <see cref="MoveHintKind.DoubleJump"/> is in the air or on a wall, so it is not a standing point.</para>
    /// </summary>
    public static class RepossessSites
    {
        /// <summary>The longest gap between consecutive waypoints, unless a wall-run or double jump covers it.</summary>
        public const float MaxWaypointGap = 12f;

        // The alcove, where the drop lands (RepossessArena.ArrivalPoint is the same point; a test pins that).
        static readonly Vector3 Arrival = new Vector3(0f, 0f, -21f);

        const float VentY = 13.6f;
        const float RailY = 3f;

        static SocketDef[] _sockets;
        static SpawnDoor[] _doors;
        static AssetDef[] _assets;
        static RouteDef[] _routes;

        static SocketDef Socket(string id, SocketGroup g, float x, float y, float z) =>
            new SocketDef { id = id, group = g, position = new Vector3(x, y, z) };

        /// <summary>The ten sockets: pit-kiosk, pit-ne, pit-sw; inside-n2, inside-n4, inside-s2, inside-s4; high-bridge,
        /// high-north-east, high-south-west (the pit's three, the four walk-in shops, the bridge's middle and the galleries' ends).</summary>
        public static SocketDef[] Sockets()
        {
            if (_sockets == null)
                _sockets = new[]
                {
                    Socket("pit-kiosk", SocketGroup.Pit, -2f, -1.5f, 0f),
                    Socket("pit-ne", SocketGroup.Pit, 9f, -4f, 7f),
                    Socket("pit-sw", SocketGroup.Pit, -13f, -4f, -7f),
                    Socket("inside-n2", SocketGroup.Inside, -10.4f, 0f, 21f),
                    Socket("inside-n4", SocketGroup.Inside, 10.4f, 0f, 21f),
                    Socket("inside-s2", SocketGroup.Inside, -10.4f, 0f, -21f),
                    Socket("inside-s4", SocketGroup.Inside, 10.4f, 0f, -21f),
                    Socket("high-bridge", SocketGroup.High, 8f, 6f, 0f),
                    Socket("high-north-east", SocketGroup.High, 30f, 6f, 20f),
                    Socket("high-south-west", SocketGroup.High, -22f, 6f, -20f),
                };
            return (SocketDef[])_sockets.Clone();
        }

        /// <summary>The sockets of one group, in <see cref="Sockets"/> order.</summary>
        public static SocketDef[] InGroup(SocketGroup g)
        {
            var l = new List<SocketDef>();
            foreach (var s in Sockets()) if (s.group == g) l.Add(s);
            return l.ToArray();
        }

        /// <summary>The tow circle's centre: the socket's floor point (the asset stands above it). Always equal to
        /// <c>s.position</c>; <see cref="Repossession"/> measures its circles from the socket's position.</summary>
        public static Vector3 CircleCenter(SocketDef s)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            return s.position;
        }

        static SpawnDoor Door(string id, DoorKind k, float x, float y, float z, float yaw) =>
            new SpawnDoor { id = id, kind = k, position = new Vector3(x, y, z), yawDeg = ((yaw % 360f) + 360f) % 360f };

        // Degrees clockwise from +Z (north) of the horizontal direction (dx, dz): the player's yaw convention (Door() wraps it to 0..360).
        static float YawToward(float dx, float dz) => (float)(Math.Atan2(dx, dz) * 180.0 / Math.PI);

        static SpawnDoor Vent(string id, float x, float z) =>
            Door(id, DoorKind.SkylightVent, x, VentY, z, YawToward(-x, -z));   // faces the hall's centre

        /// <summary>The 27 doors of the layout sheet: five shop shutters, 14 skylight/roof vents, six service hatches, two
        /// façade rail ends (the first 17 are the original sheet; ten were added for the spawn-pair coverage). <c>yawDeg</c> is the way a bot faces on arrival (into the hall; a hatch faces the way it opens).</summary>
        public static SpawnDoor[] Doors()
        {
            if (_doors == null)
                _doors = new[]
                {
                    // Shop shutters on the ring: north row faces south (180), south row faces north (0).
                    Door("shutter-n1", DoorKind.ShopShutter, -20.8f, 0f, 18f, 180f),
                    Door("shutter-n3", DoorKind.ShopShutter, 0f, 0f, 18f, 180f),
                    Door("shutter-n5", DoorKind.ShopShutter, 20.8f, 0f, 18f, 180f),
                    Door("shutter-s1", DoorKind.ShopShutter, -20.8f, 0f, -18f, 0f),
                    Door("shutter-s5", DoorKind.ShopShutter, 20.8f, 0f, -18f, 0f),
                    // Skylight vents on the frame (none above a socket).
                    Vent("vent-w-n", -26f, 12f),
                    Vent("vent-w-s", -26f, -12f),
                    Vent("vent-e-n", 26f, 12f),
                    Vent("vent-e-s", 26f, -12f),
                    Vent("vent-c-n", -4f, 22f),
                    Vent("vent-c-s", -4f, -22f),
                    // (Decision D23, 2026-10-14: the end vents and the corridor hatches sit as close to the wall as their bodies allow, 0.45 m and
                    // 0.5 m: the lint's door-body check wants them off the wall, and the corner points of the coverage grid below want them just
                    // outside the player's reachable box so that the bearings keep their 90 degrees.)
                    // Roof vents added so every spot of the hall has a legal spawn pair (W2's SpawnBearings: 15 m away, 90 degrees
                    // apart; the drone and gondola waves too). Without them the end bands (x beyond 21 m, the leg, the corridor ends)
                    // had none. Above the corridors, the west leg, the east promenade, and the skylight frame's west edge.
                    Vent("vent-corridor-n", 10f, 26.85f),
                    Vent("vent-corridor-s", 10f, -26.85f),
                    Vent("vent-leg-n", -34.35f, 10f),
                    Vent("vent-leg-s", -34.35f, -10f),
                    Vent("vent-promenade-n", 34.35f, 10f),
                    Vent("vent-promenade-s", 34.35f, -10f),
                    Vent("vent-w-frame-n", -30f, 18f),
                    Vent("vent-w-frame-s", -30f, -18f),
                    // Service hatches: the corridors' east ends open east onto the promenade; the west leg's open east into the hall.
                    Door("hatch-ne", DoorKind.ServiceHatch, 26f, 0f, 26f, 90f),
                    Door("hatch-se", DoorKind.ServiceHatch, 26f, 0f, -26f, 90f),
                    Door("hatch-w-n", DoorKind.ServiceHatch, -33f, 0f, 14f, 90f),
                    Door("hatch-w-s", DoorKind.ServiceHatch, -33f, 0f, -14f, 90f),
                    // Corridor hatches (added with the roof vents above): a floor-care unit starts in the corridor behind the shop row.
                    Door("hatch-corridor-n", DoorKind.ServiceHatch, -12f, 0f, 26.85f, 180f),
                    Door("hatch-corridor-s", DoorKind.ServiceHatch, -12f, 0f, -26.85f, 0f),
                    // The façade rail's ends, outside the glass, facing in (west).
                    Door("rail-n", DoorKind.RailEnd, 36.5f, RailY, 27f, 270f),
                    Door("rail-s", DoorKind.RailEnd, 36.5f, RailY, -27f, 270f),
                };
            return (SpawnDoor[])_doors.Clone();
        }

        /// <summary>The doors a bot (an <see cref="EnemyDef.id"/>) may arrive through, by <see cref="FloorRules.DoorAllows"/>.
        /// An unknown id gets none.</summary>
        public static SpawnDoor[] DoorsFor(string enemyId)
        {
            var l = new List<SpawnDoor>();
            foreach (var d in Doors()) if (FloorRules.DoorAllows(d.kind, enemyId)) l.Add(d);
            return l.ToArray();
        }

        /// <summary>The three draft assets (names and descriptions are drafts for the creator to rewrite), in sheet order.</summary>
        public static AssetDef[] Assets()
        {
            if (_assets == null)
                _assets = new[]
                {
                    new AssetDef { kind = AssetKind.Mascot, id = "mascot", jobSheetName = "Animatronic mascot",
                        description = "The Galleria's smiling food-court mascot, repossessed mid-wave." },
                    new AssetDef { kind = AssetKind.MassageChair, id = "massage-chair", jobSheetName = "Massage-chair display",
                        description = "A demo massage chair, still on its trial setting." },
                    new AssetDef { kind = AssetKind.ArcadeCabinet, id = "arcade-cabinet", jobSheetName = "Arcade cabinet",
                        description = "A coin-op cabinet with one credit left in it." },
                };
            return (AssetDef[])_assets.Clone();
        }

        static Vector3 P(float x, float y, float z) => new Vector3(x, y, z);

        static MoveHint H(int at, MoveHintKind k) => new MoveHint { atWaypoint = at, kind = k };

        static RouteDef Route(string id, Vector3[] wp, params MoveHint[] hints) =>
            new RouteDef { id = id, waypoints = wp, hints = hints };

        // The start of every arrival route: out of the alcove and onto the south ring, steering round the gallery column at x 0.
        static List<Vector3> FromArrival() => new List<Vector3> { Arrival, P(2f, 0f, -18.5f), P(2f, 0f, -14f) };

        // The south-east escalator up to the south gallery, then the deck: shared by the three high routes.
        static List<Vector3> ToSouthDeck()
        {
            var l = FromArrival();
            l.Add(P(8f, 0f, -13f));
            l.Add(P(14f, 0f, -12f));
            l.Add(P(18.8f, 0f, -3f));          // the escalator's foot (it climbs to the north-west as z falls)
            l.Add(P(18.8f, 3f, -9.5f));        // halfway up
            l.Add(P(18.8f, 6f, -17.5f));       // the south deck, past the lip
            return l;
        }

        /// <summary>The clean lines (no resources spent): from the arrival point to the pit's deepest socket, to a walk-in
        /// shop, to each high socket; the lift run from the pit to the exit lift; the façade wall-run route.</summary>
        public static RouteDef[] Routes()
        {
            if (_routes == null)
            {
                var pit = FromArrival();
                pit.AddRange(new[]
                {
                    P(-2f, 0f, -12f), P(-2f, 0f, -10f),           // the south ramp's high end
                    P(-2f, -2f, -7f), P(-2f, -4f, -4f),           // down it
                    P(-8f, -4f, -5f), P(-13f, -4f, -7f),          // pit-sw
                });

                var shop = FromArrival();
                shop.AddRange(new[]
                {
                    P(7f, 0f, -13.5f), P(14f, 0f, -12f), P(14f, 0f, -6f), P(14f, 0f, 0f), P(14f, 0f, 6f), P(14f, 0f, 13f),
                    P(11.3f, 0f, 16f), P(11.3f, 0f, 19.5f),       // through N4's front doorway
                    P(10.4f, 0f, 21f),                            // inside-n4
                });

                var bridge = ToSouthDeck();
                bridge.AddRange(new[] { P(8f, 6f, -17.5f), P(8f, 6f, -8f), P(8f, 6f, 0f) });     // high-bridge

                var southWest = ToSouthDeck();
                southWest.AddRange(new[]
                {
                    P(10f, 6f, -19.5f), P(1f, 6f, -19.5f), P(-8f, 6f, -19.5f), P(-16f, 6f, -19.5f), P(-22f, 6f, -20f),
                });

                var northEast = ToSouthDeck();
                northEast.AddRange(new[]
                {
                    P(8f, 6f, -17.5f), P(8f, 6f, -8f), P(8f, 6f, 2f), P(8f, 6f, 12f),             // across the bridge
                    P(8f, 6f, 17.5f), P(18f, 6f, 19f), P(28f, 6f, 19f), P(30f, 6f, 20f),          // north deck east
                });

                _routes = new[]
                {
                    Route("arrival-pit-sw", pit.ToArray()),
                    Route("arrival-inside-n4", shop.ToArray()),
                    Route("arrival-high-bridge", bridge.ToArray()),
                    Route("arrival-high-south-west", southWest.ToArray()),
                    Route("arrival-high-north-east", northEast.ToArray()),
                    // The lift run: out of the pit by the west ramp, across the west ring, into the exit lift (feet zone x -31..-27).
                    Route("pit-to-exit-lift", new[]
                    {
                        P(-6f, -4f, 0f), P(-10f, -4f, 0f), P(-13f, -2f, 0f), P(-17f, 0f, 0f), P(-22f, 0f, 0f),
                        P(-26f, 0f, 0f), P(-29f, 0f, 0f),
                    }),
                    // The façade: from the east ring up the glass north along x 35 (30 m, so a second run after a kick off the
                    // wall), then onto the facade-landing (a jump and a mantle, top 2.8, decision D23) and from its top a jump
                    // and a double jump to the north gallery's lip at z 15 (a wall-run alone tops out under the deck: F3's finding).
                    Route("facade-wall-run", new[]
                    {
                        P(28f, 0f, -10f), P(34f, 0f, -14f), P(34.6f, 2.5f, -14f), P(34.6f, 2.5f, 1f), P(32.5f, 2.8f, 12f),
                        P(32.5f, 6f, 17.5f),
                    },
                    H(2, MoveHintKind.WallRun), H(3, MoveHintKind.WallRun), H(4, MoveHintKind.Mantle),
                    H(5, MoveHintKind.DoubleJump)),
                };
            }
            // Deep-ish copy: the arrays are new, the waypoint and hint values are structs.
            var copy = new RouteDef[_routes.Length];
            for (int i = 0; i < copy.Length; i++)
                copy[i] = new RouteDef
                {
                    id = _routes[i].id,
                    waypoints = (Vector3[])_routes[i].waypoints.Clone(),
                    hints = (MoveHint[])_routes[i].hints.Clone(),
                };
            return copy;
        }
    }
}
