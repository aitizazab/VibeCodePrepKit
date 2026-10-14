using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using JamKit.Skyline;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    /// <summary>The Relay's random order and its stage data (docs/SKYLINE-RELAY.md).</summary>
    public class RelayChainTests
    {
        [Test]
        public void ASeededOrderIsAPermutationOfAToF()
        {
            for (int seed = 0; seed < 200; seed++)
            {
                var order = RelayChain.Generate(RelayTestKit.Real, seed);
                Assert.That(order.Length, Is.EqualTo(6));
                Assert.That(order.OrderBy(x => x).ToArray(), Is.EqualTo(new[] { 0, 1, 2, 3, 4, 5 }), "seed " + seed);
                Assert.That(RelayChain.Letters(order).OrderBy(c => c).ToArray(), Is.EqualTo("ABCDEF".ToCharArray()));
            }
        }

        [Test]
        public void TheSameSeedGivesTheSameOrderAndDifferentSeedsGiveVariety()
        {
            Assert.That(RelayChain.Generate(RelayTestKit.Real, 41), Is.EqualTo(RelayChain.Generate(RelayTestKit.Real, 41)));
            var seen = new HashSet<string>();
            for (int seed = 0; seed < 100; seed++) seen.Add(RelayChain.Letters(RelayChain.Generate(RelayTestKit.Real, seed)));
            Assert.That(seen.Count, Is.GreaterThan(40), "a good spread of orders");
        }

        [Test]
        public void EveryStationPairIsFarApartSoNoHopIsShort()
        {
            for (int a = 0; a < 6; a++)
                for (int b = a + 1; b < 6; b++)
                    Assert.That(Vector3.Distance(RelayArena.Stations[a], RelayArena.Stations[b]), Is.GreaterThanOrEqualTo(RelayChain.MinHop),
                        $"{RelayArena.Letter(a)} to {RelayArena.Letter(b)}");
        }

        [Test]
        public void EveryHopOfEveryOrderIsWalkable()
        {
            for (int a = 0; a < 6; a++)
            {
                Assert.That(RelayTestKit.Real.StartPathLength(a), Is.GreaterThan(0f), "the start to " + RelayArena.Letter(a));
                for (int b = 0; b < 6; b++)
                    if (a != b)
                        Assert.That(RelayTestKit.Real.PathLength(a, b), Is.GreaterThanOrEqualTo(Vector3.Distance(RelayArena.Stations[a], RelayArena.Stations[b]) - 0.5f),
                            $"{RelayArena.Letter(a)} to {RelayArena.Letter(b)} is a walk at least as long as the straight line");
            }
        }

        [Test]
        public void TheCatwalkHasNoWalkButTheFastRouteGivesEveryHopALength()
        {
            Assert.That(RelayArena.IsHigh(5), Is.True);
            Assert.That(RelayArena.IsHigh(0), Is.False);
            for (int a = 0; a < 5; a++)
            {
                Assert.That(RelayTestKit.Real.WalkLength(a, 5), Is.LessThan(0f), "no walk from " + RelayArena.Letter(a) + " to the catwalk");
                Assert.That(RelayTestKit.Real.WalkLength(5, a), Is.LessThan(0f), "no walk back either");
                Assert.That(RelayTestKit.Real.PathLength(a, 5), Is.GreaterThan(Vector3.Distance(RelayArena.Stations[a], RelayArena.Stations[5])));
            }
        }

        [Test]
        public void EveryOrderKeepsEveryHopAtLeastTheMinimum()
        {
            for (int seed = 0; seed < 100; seed++)
            {
                var order = RelayChain.Generate(RelayTestKit.Real, seed);
                Assert.That(RelayChain.Score(RelayTestKit.Real, order), Is.GreaterThanOrEqualTo(RelayChain.MinHop));
            }
        }

        [Test]
        public void AnOrderNeverUsesAHopThatCannotBeWalked()
        {
            var map = new RelayTestKit.FlatMap(2, 3, 0, 5);
            for (int seed = 0; seed < 100; seed++)
            {
                var o = RelayChain.Generate(map, seed);
                for (int i = 0; i + 1 < o.Length; i++)
                    Assert.That(map.PathLength(o[i], o[i + 1]), Is.GreaterThan(0f), "seed " + seed + " " + RelayChain.Letters(o));
            }
        }

        [Test]
        public void TheStepsAreFixedAndTheStationOfEachStepFollowsTheOrder()
        {
            Assert.That(RelayChain.Steps, Is.EqualTo(new[] { RelayStepKind.Button, RelayStepKind.Cable, RelayStepKind.Plug, RelayStepKind.Swipe, RelayStepKind.Escape }));
            var order = new[] { 3, 1, 4, 0, 5, 2 };
            Assert.That(RelayChain.StationOfStep(order, 0), Is.EqualTo(3));
            Assert.That(RelayChain.StationOfStep(order, 3), Is.EqualTo(0));
            Assert.That(RelayChain.StationOfStep(order, 4), Is.EqualTo(2), "the escape is the lift (the sixth), through the door (the fifth)");
        }

        [Test]
        public void TheSpawnPointsAreClearOfTheStationsTheWallsAndTheBlocks()
        {
            foreach (var p in RelayArena.AirSpawns.Concat(RelayArena.FloorSpawns))
            {
                foreach (var s in RelayArena.Stations)
                    Assert.That(System.Math.Sqrt((p.X - s.X) * (p.X - s.X) + (p.Z - s.Z) * (p.Z - s.Z)), Is.GreaterThanOrEqualTo(4.0), $"{p} is near a station");
                foreach (var s in RelayArena.StageSolids())
                {
                    var b = s.box;
                    bool inside = p.X > b.min.X - 1f && p.X < b.max.X + 1f && p.Z > b.min.Z - 1f && p.Z < b.max.Z + 1f && p.Y < b.max.Y + 0.8f;
                    Assert.That(inside, Is.False, $"{p} is in or by {s.id}");
                }
            }
        }
    }
}
