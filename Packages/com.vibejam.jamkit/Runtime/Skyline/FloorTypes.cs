using System.Numerics;

namespace JamKit.Skyline
{
    /// <summary>The kind of in-world door a wave can arrive through (docs/SKYLINE-FLOOR-REPOSSESS.md, spawn doors).</summary>
    public enum DoorKind { ShopShutter, SkylightVent, ServiceHatch, RailEnd }

    /// <summary>Where an asset can sit: one asset per group per run (one low, one inside, one high).</summary>
    public enum SocketGroup { Pit, Inside, High }

    /// <summary>The three assets to repossess (draft set).</summary>
    public enum AssetKind { Mascot, MassageChair, ArcadeCabinet }

    /// <summary>The floor's phases. Lobby: from the start in the staff hallway until the player steps into the lift.
    /// LiftRide: the opening sequence, ends at the drop (the doors open and the wave clock starts). Floor: the fight, until
    /// the third asset is towed. Finale: waves stopped, the west service lift open, until the Unit's feet reach it. Done:
    /// the floor is over.</summary>
    public enum FloorPhase { Lobby, LiftRide, Floor, Finale, Done }

    /// <summary>Which kind of space a point is in (the layout lint and the colour-free zones read it).</summary>
    public enum SurfaceLevel { Pit, Ring, Gallery, Corridor, ShopInterior, Lobby }

    /// <summary>
    /// One solid of the floor's geometry as DATA: the Unity builder makes one collider per solid, the layout lint, the
    /// navigation graph and the headless sim read the same list. <see cref="role"/> is a free string with these starting
    /// values: floor wall rim shopfront column pilaster escalator ramp deck balustrade table planter lift facade (more
    /// may be added by the task that owns the data; a test forbids tall free-standing cover by role).
    /// </summary>
    public sealed class FloorSolid
    {
        public string id = "", role = "";
        public CoverBox box;
        /// <summary>A wall-run surface (the spec: the façade and the pit's retaining walls are runnable).</summary>
        public bool runnable;
        /// <summary>Glass: blocks movement, bullets and sight pass through.</summary>
        public bool glass;
        /// <summary>A sloped walkable surface (a ramp or an escalator): the top is <see cref="TopAt"/>, not box.max.Y.</summary>
        public bool ramp;
        /// <summary>For a ramp: the slope runs along X (false: along Z).</summary>
        public bool rampAlongX;
        /// <summary>For a ramp: the walkable top height at the box's minimum edge on the slope axis, and at its maximum
        /// edge (so a ramp that descends toward +Z has rampTopStart above rampTopEnd).</summary>
        public float rampTopStart, rampTopEnd;

        /// <summary>The walkable top at (x, z): <c>box.max.Y</c> for a flat solid; for a ramp the height interpolated from
        /// <see cref="rampTopStart"/> (at the box's minimum on the slope axis) to <see cref="rampTopEnd"/> (at its
        /// maximum), clamped to the box.</summary>
        public float TopAt(float x, float z)
        {
            if (!ramp) return box.max.Y;
            float lo = rampAlongX ? box.min.X : box.min.Z, hi = rampAlongX ? box.max.X : box.max.Z;
            float v = rampAlongX ? x : z;
            float span = hi - lo;
            float t = span <= 1e-6f ? 0f : (v - lo) / span;
            if (t < 0f) t = 0f; else if (t > 1f) t = 1f;
            return rampTopStart + (rampTopEnd - rampTopStart) * t;
        }
    }

    /// <summary>A place an asset can be clamped (the tow circle is centred here).</summary>
    public sealed class SocketDef
    {
        public string id = "";
        public SocketGroup group;
        public Vector3 position;
    }

    /// <summary>An in-world door a wave arrives through. <see cref="yawDeg"/> is the way the bot faces on arrival (degrees,
    /// 0 = +Z north, clockwise seen from above like the player's yaw).</summary>
    public sealed class SpawnDoor
    {
        public string id = "";
        public DoorKind kind;
        public Vector3 position;
        public float yawDeg;
    }

    /// <summary>One asset to repossess. The text is a draft for the creator to rewrite.</summary>
    public sealed class AssetDef
    {
        public AssetKind kind;
        public string id = "", jobSheetName = "", description = "";
    }

    /// <summary>An axis-aligned region of the floor plan at one height: the floor level it is on and where.</summary>
    public sealed class FloorZone
    {
        public string id = "";
        public SurfaceLevel level;
        public float minX, maxX, minZ, maxZ, y;
    }

    /// <summary>The floor's tuning numbers, copied from the spec (docs/SKYLINE-PLAN-FLOOR1.md, Global constraints). A test
    /// pins each value, so a later edit that drifts from the spec fails.</summary>
    public static class FloorRules
    {
        /// <summary>Seconds inside a tow circle that take an asset; the circle's radius (a 6 m circle); the wave interval;
        /// the warning a spawn door gives before it opens.</summary>
        /// <summary>The floor song's tempo (docs/SKYLINE-MUSIC.md). A wave every 16 bars (31.35 s), so each one lands on a phrase
        /// with a fill in the bar before it (the creator agreed to stretch it from 30 s, 2026-10-14).</summary>
        public const float MusicBpm = 122.5f;
        public const float TowSeconds = 10f, CircleRadius = 3f, WaveSeconds = 16 * 4 * 60f / MusicBpm, DoorTelegraphSeconds = 2f;
        /// <summary>At most this many living bots; a wave that would pass it waits.</summary>
        public const int WaveCap = 10;
        /// <summary>Every wave spawns from two points at least this many degrees apart as seen from the player; the first
        /// is at least <see cref="MinFromLastWave"/> degrees from the previous wave's mean bearing; never closer than
        /// <see cref="MinSpawnDistance"/> m; never a point in the player's view within <see cref="ViewExclusionRange"/> m.</summary>
        public const float MinBearingSpread = 90f, MinFromLastWave = 120f, MinSpawnDistance = 15f, ViewExclusionRange = 25f;
        /// <summary>The slack (degrees) the spawn choice allows on <see cref="MinBearingSpread"/>. The larger bots (2026-10-15) need their end doors
        /// 0.63 m off the walls, so a player in a hall corner sees the two end doors 89.3 degrees apart instead of 90.2; nobody can see 0.7 degrees.</summary>
        public const float BearingSlack = 1.5f;
        /// <summary>Layout rule 1: every standable point has at least this many exits that leave a 10 m radius
        /// (<see cref="ExitRadius"/>) by different routes.</summary>
        public const int MinExits = 2;
        /// <summary>Layout rules 2 and 3: no standable point sees more than <see cref="MaxSeenShare"/> of the ground-level spawn doors (shutters, hatches, rail ends; roof vents excluded, decision D23);
        /// every tow circle is at least <see cref="MinCircleExposure"/> exposed from two bearings
        /// <see cref="ExposureBearingSpread"/> degrees apart.</summary>
        public const float ExitRadius = 10f, MaxSeenShare = 0.7f, MinCircleExposure = 0.5f, ExposureBearingSpread = 90f;

        /// <summary>Which doors a bot may arrive through: the drone by shutter or vent, floor-care by shutter or hatch, the
        /// gondola only at a rail end, the spotlight never (it is not on this floor). <paramref name="enemyId"/> is an
        /// <see cref="EnemyDef.id"/> (<see cref="SkylineEnemies"/>); an unknown id is allowed nowhere.</summary>
        public static bool DoorAllows(DoorKind k, string enemyId)
        {
            switch (enemyId)
            {
                case SkylineEnemies.DroneId: return k == DoorKind.ShopShutter || k == DoorKind.SkylightVent;
                case SkylineEnemies.FloorCareId: return k == DoorKind.ShopShutter || k == DoorKind.ServiceHatch;
                case SkylineEnemies.GondolaId: return k == DoorKind.RailEnd;
                default: return false;
            }
        }
    }
}
