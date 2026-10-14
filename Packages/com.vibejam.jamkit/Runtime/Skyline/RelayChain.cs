using System;
using System.Collections.Generic;
using System.Numerics;


namespace JamKit.Skyline
{
    /// <summary>The five steps of a Relay run, always in this order (docs/SKYLINE-RELAY.md).</summary>
    public enum RelayStepKind
    {
        /// <summary>Hold interact (0.5 s) at the button.</summary>
        Button,
        /// <summary>Interact at the cable reel to take the cable's end.</summary>
        Cable,
        /// <summary>Interact at the socket, carrying the cable: a keycard pops out.</summary>
        Plug,
        /// <summary>Interact at the card reader, holding the keycard: the door opens.</summary>
        Swipe,
        /// <summary>Enter the exit lift's zone through the open door: the run is won.</summary>
        Escape,
    }

    /// <summary>The stations as the generator and the run see them: positions and walking distances. The Unity director supplies
    /// one built on <see cref="ArenaNav"/>; the tests supply flat ones.</summary>
    public interface IRelayMap
    {
        int Count { get; }
        Vector3 Position(int station);
        /// <summary>The shortest walk (metres) between two stations; negative when none exists.</summary>
        float PathLength(int from, int to);
        /// <summary>The shortest walk from the player's start to a station; negative when none exists.</summary>
        float StartPathLength(int station);
    }

    /// <summary>The route lengths between the stations: the walk (by A* on the walkable graph, each end corrected by the distance
    /// from the station to its nearest node; negative where none exists, as to the catwalk) and the fast way
    /// (<see cref="RelayRoutes.Skilled"/>). <see cref="PathLength"/> is the walk where there is one and the fast way where there is not,
    /// so every hop has a length (the cable is sized by it). Built once from <see cref="ArenaNav"/>, or by hand in tests.</summary>
    public sealed class RelayStationMap : IRelayMap
    {
        readonly Vector3[] _pos;
        readonly float[,] _path, _walk;
        readonly float[] _start;

        public RelayStationMap(Vector3[] positions, float[,] path, float[] startPath) : this(positions, path, path, startPath) { }

        public RelayStationMap(Vector3[] positions, float[,] path, float[,] walk, float[] startPath)
        {
            _pos = positions;
            _path = path;
            _walk = walk;
            _start = startPath;
        }

        public int Count => _pos.Length;
        public Vector3 Position(int station) => _pos[station];
        public float PathLength(int from, int to) => _path[from, to];
        public float StartPathLength(int station) => _start[station];
        /// <summary>The walk-only length between two stations; negative when none exists.</summary>
        public float WalkLength(int from, int to) => _walk[from, to];

        public static RelayStationMap Build(ArenaNav nav)
        {
            int n = RelayArena.StationCount;
            var pos = (Vector3[])RelayArena.Stations.Clone();
            var path = new float[n, n];
            var walk = new float[n, n];
            var start = new float[n];
            var nodes = new int[n];
            for (int i = 0; i < n; i++) nodes[i] = nav.NodeAt(pos[i]);
            int startNode = nav.NodeAt(RelayArena.PlayerStart);
            for (int i = 0; i < n; i++)
            {
                float s = Walk(nav, RelayArena.PlayerStart, startNode, pos[i], nodes[i]);
                start[i] = s >= 0f ? s : RelayRoutes.Skilled(RelayArena.PlayerStart, pos[i]).Length;
                for (int j = 0; j < n; j++)
                {
                    float w = i == j ? 0f : Walk(nav, pos[i], nodes[i], pos[j], nodes[j]);
                    walk[i, j] = w;
                    path[i, j] = w >= 0f ? w : RelayRoutes.Skilled(pos[i], pos[j]).Length;
                }
            }
            return new RelayStationMap(pos, path, walk, start);
        }

        static float Walk(ArenaNav nav, Vector3 a, int na, Vector3 b, int nb)
        {
            if (na < 0 || nb < 0) return -1f;
            var p = nav.Path(na, nb);
            if (p == null) return -1f;
            var pts = nav.Simplify(p);
            float len = Vector3.Distance(a, nav.PositionOf(na)) + Vector3.Distance(nav.PositionOf(nb), b);
            for (int i = 1; i < pts.Count; i++) len += Vector3.Distance(pts[i - 1], pts[i]);
            return len;
        }
    }

    /// <summary>
    /// The random order of a run. <see cref="Generate"/> shuffles the six stations, with a seed, into the roles
    /// (button, cable reel, socket, card reader, door, lift) and keeps a shuffle whose every hop is walkable and at least
    /// <see cref="MinHop"/> metres (straight line) from the one before. Pure and deterministic for a seed.
    /// </summary>
    public static class RelayChain
    {
        public const int Roles = 6;
        public const float MinHop = 15f;
        const int Tries = 300;

        /// <summary>The five steps and the station each one is done at, in play order: kinds[i] happens at the station
        /// order[i] (button, cable reel), and the reel's cable plugs in at order[2], the card reads at order[3], the door is
        /// order[4]'s and the lift order[5].</summary>
        public static readonly RelayStepKind[] Steps =
            { RelayStepKind.Button, RelayStepKind.Cable, RelayStepKind.Plug, RelayStepKind.Swipe, RelayStepKind.Escape };

        /// <summary>The station each step is done at (the step's own prop): button, cable reel, socket, card reader, lift. The
        /// sixth role, the door, is order[4] and is opened by the swipe; the lift (the exit, order[5]) is the Escape step's.</summary>
        public static int StationOfStep(int[] order, int step)
        {
            // steps: 0 button=order[0], 1 cable=order[1], 2 plug=order[2], 3 swipe=order[3], 4 escape=order[5] (through order[4], the door)
            return step == 4 ? order[5] : order[step];
        }

        /// <summary>The order for <paramref name="seed"/>: <c>order[role]</c> is the station that plays that role.</summary>
        public static int[] Generate(IRelayMap map, int seed)
        {
            var rng = new Rng((ulong)(uint)seed);
            int[] best = null;
            float bestScore = float.NegativeInfinity;
            for (int t = 0; t < Tries; t++)
            {
                var order = Shuffle(rng, Roles);
                float score = Score(map, order);
                if (score > bestScore) { bestScore = score; best = order; }
                if (score >= MinHop) return order;
            }
            return best;
        }

        /// <summary>The shortest hop (straight line) of an order, or -1 when any hop (or the walk from the start) is unreachable.</summary>
        public static float Score(IRelayMap map, int[] order)
        {
            if (map.StartPathLength(order[0]) < 0f) return -1f;
            float min = float.MaxValue;
            for (int i = 0; i + 1 < order.Length; i++)
            {
                if (map.PathLength(order[i], order[i + 1]) < 0f) return -1f;
                min = Math.Min(min, Vector3.Distance(map.Position(order[i]), map.Position(order[i + 1])));
            }
            return min;
        }

        /// <summary>A permutation of 0..n-1 (Fisher-Yates).</summary>
        public static int[] Shuffle(Rng rng, int n)
        {
            var a = new int[n];
            for (int i = 0; i < n; i++) a[i] = i;
            for (int i = n - 1; i > 0; i--)
            {
                int j = (int)(rng.NextUInt() % (uint)(i + 1));
                int tmp = a[i]; a[i] = a[j]; a[j] = tmp;
            }
            return a;
        }

        /// <summary>The letters of an order in play order, e.g. "DBFACE".</summary>
        public static string Letters(int[] order)
        {
            var c = new char[order.Length];
            for (int i = 0; i < c.Length; i++) c[i] = RelayArena.Letter(order[i]);
            return new string(c);
        }
    }
}
