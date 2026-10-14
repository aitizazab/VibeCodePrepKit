using System;
using System.Collections.Generic;
using System.Numerics;

namespace JamKit.Skyline
{
    /// <summary>A route as a movement pilot follows it: waypoints with the move at each (a mantle onto a wall's top, a plain walk).</summary>
    public sealed class RelayRoute
    {
        public readonly List<Vector3> waypoints = new List<Vector3>();
        public readonly List<MoveHint> hints = new List<MoveHint>();
        /// <summary>The lane walls and blocks the route climbs.</summary>
        public int climbs;

        public float Length
        {
            get
            {
                float l = 0f;
                for (int i = 1; i < waypoints.Count; i++) l += Vector3.Distance(waypoints[i - 1], waypoints[i]);
                return l;
            }
        }

        public void Add(Vector3 p, MoveHintKind kind = MoveHintKind.Walk)
        {
            waypoints.Add(p);
            if (kind != MoveHintKind.Walk) hints.Add(new MoveHint { atWaypoint = waypoints.Count - 1, kind = kind });
        }
    }

    /// <summary>
    /// The fast way between two Relay stations (docs/SKYLINE-RELAY.md): the straight line, with a jump and a mantle onto the top of
    /// every lane wall it crosses (and a landing 1.6 m beyond), or, for the catwalk, to the foot of the climbing blocks, up
    /// the two blocks and onto the deck's edge. From the catwalk it is the way down: along the deck to its edge and the straight
    /// line from there. Pure; the simulated player and the cable's length read it.
    /// </summary>
    public static class RelayRoutes
    {
        const float LandingStep = 1.6f, EndSlack = 0.3f;

        public static RelayRoute Skilled(Vector3 from, Vector3 to)
        {
            var r = new RelayRoute();
            bool fromHigh = from.Y > 1f, toHigh = to.Y > 1f;
            float cx = (RelayArena.ChainMinX + RelayArena.ChainMaxX) * 0.5f;
            if (toHigh && !fromHigh)
            {
                var foot = new Vector3(cx, 0f, RelayArena.ChainZ0 - 1.0f);
                Ground(r, from, foot);
                r.Add(new Vector3(cx, RelayArena.Block1Top, (RelayArena.ChainZ0 + RelayArena.ChainZMid) * 0.5f), MoveHintKind.Mantle);
                r.Add(new Vector3(cx, RelayArena.Block2Top, (RelayArena.ChainZMid + RelayArena.ChainZ1) * 0.5f), MoveHintKind.Mantle);
                r.Add(new Vector3(cx, RelayArena.DeckTop, RelayArena.ChainZ1 + 1.2f), MoveHintKind.Mantle);
                r.climbs += 3;
                r.Add(to);
                return r;
            }
            if (fromHigh && !toHigh)
            {
                // along the deck to its south edge, and off it; the ground route goes on from the landing
                float edgeZ = RelayArena.ChainZ1 + 0.4f;
                float ex = to.Z < edgeZ ? from.X + (to.X - from.X) * ((from.Z - edgeZ) / Math.Max(0.01f, from.Z - to.Z)) : to.X;
                ex = Math.Max(GalleriaArena.HallMinX + 2f, Math.Min(GalleriaArena.HallMaxX - 2f, ex));
                r.Add(from);
                r.Add(new Vector3(ex, RelayArena.DeckTop, edgeZ));
                // a target under the deck is reached by dropping off the edge, running out and coming back over the wall below it
                if (to.Z >= edgeZ) r.Add(new Vector3(ex, 0f, RelayArena.ChainZ0 + 1.0f), MoveHintKind.Mantle);
                Ground(r, new Vector3(ex, 0f, to.Z >= edgeZ ? RelayArena.ChainZ0 + 1.0f : RelayArena.ChainZ1 - 1.6f), to, false);
                return r;
            }
            Ground(r, from, to);
            return r;
        }

        /// <summary>The straight line from <paramref name="from"/> to <paramref name="to"/> on the ground, climbing each lane wall it crosses.</summary>
        static void Ground(RelayRoute r, Vector3 from, Vector3 to, bool addFrom = true)
        {
            if (addFrom) r.Add(from);
            float dz = to.Z - from.Z;
            var crossings = new List<(float t, float x, int dir, int wall)>();
            for (int k = 0; k < RelayArena.LaneWallZ.Length; k++)
            {
                float z = RelayArena.LaneWallZ[k];
                if (Math.Abs(dz) < 1e-4f || (from.Z - z) * (to.Z - z) >= 0f) continue;
                float t = (z - from.Z) / dz;
                float x = from.X + (to.X - from.X) * t;
                var w = RelayArena.LaneWall(k);
                if (x < w.min.X - EndSlack || x > w.max.X + EndSlack) continue;     // through the opening: no climb
                crossings.Add((t, x, dz > 0f ? 1 : -1, k));
            }
            crossings.Sort((a, b) => a.t.CompareTo(b.t));
            Vector3 d = to - from;
            float flat = (float)Math.Sqrt(d.X * d.X + d.Z * d.Z);
            foreach (var c in crossings)
            {
                float z = RelayArena.LaneWallZ[c.wall];
                r.Add(new Vector3(c.x, RelayArena.WallHeight, z), MoveHintKind.Mantle);
                r.climbs++;
                float f = flat > 1e-3f ? LandingStep / flat : 0f;
                // the landing is reached on the way down (a Mantle-kind waypoint only for its looser height tolerance: the pilot is still falling)
                r.Add(new Vector3(c.x + d.X * f, 0f, z + c.dir * Math.Max(LandingStep * 0.8f, Math.Abs(d.Z) * f)), MoveHintKind.Mantle);
            }
            r.Add(to);
        }
    }
}
