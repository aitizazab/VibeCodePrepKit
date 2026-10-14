using System.Collections.Generic;
using System.Numerics;
using JamKit.Skyline;

namespace JamKit.Tests.Skyline
{
    /// <summary>Test-only helpers for the Relay: the real station map over the hall's walkable graph, and a flat stand-in.</summary>
    public static class RelayTestKit
    {
        public static readonly ArenaNav Nav = ArenaNav.Build(RelayArena.Solids(), 2f, 1.1f);
        public static readonly RelayStationMap Real = RelayStationMap.Build(Nav);

        /// <summary>The stations with straight-line distances (x1.1); pairs in <paramref name="unreachable"/> have no walk.</summary>
        public sealed class FlatMap : IRelayMap
        {
            readonly HashSet<long> _no = new HashSet<long>();
            public FlatMap(params int[] unreachablePairs)
            {
                for (int i = 0; i + 1 < unreachablePairs.Length; i += 2)
                {
                    _no.Add(unreachablePairs[i] * 100L + unreachablePairs[i + 1]);
                    _no.Add(unreachablePairs[i + 1] * 100L + unreachablePairs[i]);
                }
            }
            public int Count => RelayArena.StationCount;
            public Vector3 Position(int station) => RelayArena.Stations[station];
            public float PathLength(int from, int to) =>
                _no.Contains(from * 100L + to) ? -1f : Vector3.Distance(Position(from), Position(to)) * 1.1f;
            public float StartPathLength(int station) => Vector3.Distance(RelayArena.PlayerStart, Position(station)) * 1.1f;
        }

        /// <summary>The run's steps done in order, as a perfect player would (the lift entered after the door).</summary>
        public static void DoAll(RelayRun r)
        {
            r.Interact(r.ButtonStation, RelayRun.ButtonHold);
            r.Interact(r.ReelStation, 0f);
            r.Interact(r.SocketStation, 0f);
            r.Tick(RelayRun.KeycardPop + 0.01f);
            r.PlayerAt(r.SocketStation);
            r.Interact(r.ReaderStation, 0f);
            r.PlayerAt(r.DoorStation);
            r.PlayerAt(r.LiftStation);
        }
    }
}
