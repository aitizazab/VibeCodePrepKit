using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using JamKit.Movement;
using JamKit.Skyline;
using JamKit.Tests.Movement;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    /// <summary>
    /// The Relay's headless player (docs/SKYLINE-RELAY.md). The real <see cref="MoveSim"/> runs the hall's solids twice per pair of
    /// stations: the <b>skilled</b> route (<see cref="RelayRoutes.Skilled"/>: sprint, a jump and a mantle onto every lane wall in
    /// the straight line, the two blocks and the deck's edge for the catwalk) and the <b>walk-only</b> route (the shortest path on
    /// the walkable graph, sprinting, no jump or mantle at all). The creator's rule is that the straight way takes the movement
    /// kit and the easy way is the long way round, so every leg's skilled time must beat the walk by <see cref="Margin"/>.
    /// Not simulated: enemies (the "movement x" rows stretch the walking time to stand for fire), the Breacher's boost, the
    /// wall-run (neither is used; the routes need only sprint, jump and mantle).
    /// </summary>
    public class RelaySimTests
    {
        /// <summary>The walk-only route must take at least this many times the skilled route's time.</summary>
        public const float Margin = 1.6f;
        const float ZoneRadius = 2.0f;
        const float PressSeconds = 0.4f;     // the time between arriving and the press landing, per step

        static BoxWorld World()
        {
            var w = new BoxWorld();
            foreach (var s in RelayArena.Solids())
            {
                var b = s.box;
                w.Add((b.min + b.max) * 0.5f, b.max - b.min, s.runnable);
            }
            return w;
        }

        static bool InZone(Vector3 p, Vector3 to)
        {
            float dx = to.X - p.X, dz = to.Z - p.Z;
            return dx * dx + dz * dz <= ZoneRadius * ZoneRadius && Math.Abs(to.Y - p.Y) < 2f;
        }

        /// <summary>Runs a pilot along <paramref name="route"/> until it stands in <paramref name="to"/>'s zone; the seconds, or -1.</summary>
        static float Run(BoxWorld w, Vector3 from, Vector3 to, IReadOnlyList<Vector3> route, IReadOnlyList<MoveHint> hints, bool carrying, out Vector3 end)
        {
            var sim = MoveTestKit.Standing(w, from);
            var bot = new RouteWalker(route, hints) { Sprint = !carrying, JumpRange = carrying ? 2.2f : 4f };
            float t = 0f;
            while (t < 60f && !bot.Done)
            {
                if (InZone(sim.Position, to)) break;
                var input = bot.Next(sim, t, MoveTestKit.H);
                if (carrying)
                {
                    input.move *= RelayRun.CarryScale * sim.def.stickFullAt;   // the engine reads a stick as full from stickFullAt up: 0.75 of it is 75% of the walk
                    input.sprintHeld = false;
                }
                sim.Step(input, MoveTestKit.H);
                t += MoveTestKit.H;
            }
            end = sim.Position;

            return t < 60f ? t : -1f;
        }

        static float Skilled(BoxWorld w, Vector3 from, Vector3 to, bool carrying, out RelayRoute route, out Vector3 end)
        {
            route = RelayRoutes.Skilled(from, to);
            return Run(w, from, to, route.waypoints, route.hints, carrying, out end);
        }

        /// <summary>The walk-only time (no jump, no mantle: the shortest path on the walkable graph), -1 when it got stuck, or
        /// NaN when there is no walk at all.</summary>
        static float Walk(BoxWorld w, Vector3 from, Vector3 to, bool carrying)
        {
            var nav = RelayTestKit.Nav;
            var path = nav.Path(nav.NodeAt(from), nav.NodeAt(to));
            if (path == null) return float.NaN;
            var route = nav.Simplify(path);
            route.Add(to);
            return Run(w, from, to, route, null, carrying, out _);
        }

        // ------------------------------------------------------------------ every ordered pair of stations

        [Test]
        public void EveryOrderedPairTheStraightWayTakesTheMovementKitAndTheEasyWayIsTheLongOne()
        {
            var w = World();
            var sb = new StringBuilder();
            sb.AppendLine("RELAY SIM leg    climbs  skilled s   walk-only s  ratio");
            var problems = new List<string>();
            float minRatio = float.MaxValue;
            for (int a = 0; a < RelayArena.StationCount; a++)
                for (int b = 0; b < RelayArena.StationCount; b++)
                {
                    if (a == b) continue;
                    Vector3 pa = RelayArena.Stations[a], pb = RelayArena.Stations[b];
                    string name = $"{RelayArena.Letter(a)}->{RelayArena.Letter(b)}";
                    float fast = Skilled(w, pa, pb, false, out var route, out var end);
                    float walk = Walk(w, pa, pb, false);
                    string walkText = float.IsNaN(walk) ? "unreachable" : walk < 0f ? "stuck" : walk.ToString("0.0");
                    string ratioText = float.IsNaN(walk) ? "-" : (walk / fast).ToString("0.00");
                    sb.AppendLine($"RELAY SIM {name}     {route.climbs}       {fast,6:0.0}      {walkText,11}   {ratioText}");
                    if (fast < 0f) { problems.Add($"{name}: the skilled route was not completed (ended {end})"); continue; }
                    if (route.climbs == 0 && !float.IsNaN(walk)) problems.Add($"{name}: the straight way needs no movement and walks");
                    if (!float.IsNaN(walk))
                    {
                        if (walk < 0f) problems.Add($"{name}: the walk got stuck");
                        else
                        {
                            minRatio = Math.Min(minRatio, walk / fast);
                            if (walk / fast < Margin) problems.Add($"{name}: walking takes {walk:0.0} s against {fast:0.0} s skilled (x{walk / fast:0.00})");
                        }
                    }
                }
            Console.WriteLine(sb.ToString());
            Console.WriteLine($"RELAY SIM smallest walk/skilled ratio over the legs with a walk: {minRatio:0.00}");
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [Test]
        public void TheCatwalkHasNoWalkRouteAndOnlyTheMovementKitReachesIt()
        {
            var w = World();
            for (int a = 0; a < 5; a++)
            {
                Assert.That(float.IsNaN(Walk(w, RelayArena.Stations[a], RelayArena.Stations[5], false)), Is.True, "no walk to F from " + RelayArena.Letter(a));
                float fast = Skilled(w, RelayArena.Stations[a], RelayArena.Stations[5], false, out var route, out var end);
                Assert.That(fast, Is.GreaterThan(0f), $"{RelayArena.Letter(a)} to F: ended {end}");
                Assert.That(end.Y, Is.EqualTo(RelayArena.DeckTop).Within(0.3f));
                Assert.That(route.climbs, Is.GreaterThanOrEqualTo(3));
            }
        }

        [Test]
        public void NoWalkerStandsOnTheBlocksOrTheDeckWithoutTheMantle()
        {
            // the first block's top is 2.4 m, the second 4.0: no walker steps (0.35 m) and a plain jump (1.25 m) cannot reach either
            Assert.That(RelayArena.Block1Top, Is.GreaterThan(1.25f + 0.35f));
            var nav = RelayTestKit.Nav;
            for (int i = 0; i < nav.Count; i++)
                if (nav.PositionOf(i).Y > 1f && nav.PositionOf(i).Y < 4.9f)
                    Assert.Fail("a walkable node at " + nav.PositionOf(i));
        }

        // ------------------------------------------------------------------ whole chains: the clock

        /// <summary>Total seconds of a seeded chain for the skilled pilot (start, then the six stations in order; the cable leg at 75%
        /// speed without a sprint; the presses' and the keycard's time added).</summary>
        static float Chain(BoxWorld w, int seed, List<float> legs, out float carrySeconds)
        {
            var order = RelayChain.Generate(RelayTestKit.Real, seed);
            var at = RelayArena.PlayerStart;
            float total = 0f;
            carrySeconds = 0f;
            for (int i = 0; i < 6; i++)
            {
                var to = RelayArena.Stations[order[i]];
                bool carrying = i == 2;   // the leg from the reel (order[1]) to the socket (order[2])
                float t = Skilled(w, at, to, carrying, out _, out var end);
                Assert.That(t, Is.GreaterThanOrEqualTo(0f), $"seed {seed} leg {i} ({RelayChain.Letters(order)}) did not arrive from {at} to {to}: ended {end}");
                legs.Add(t);
                total += t;
                if (carrying) carrySeconds = t;
                at = to;
            }
            return total + RelayRun.ButtonHold + 5 * PressSeconds + RelayRun.KeycardPop;
        }

        [Test]
        public void HowLongAChainTakesForTheSkilledPilotAndWhatClockThatAsks()
        {
            var w = World();
            var totals = new List<float>();
            var carry = new List<float>();
            var allLegs = new List<float>();
            const int Seeds = 60;
            for (int seed = 0; seed < Seeds; seed++)
            {
                var legs = new List<float>();
                totals.Add(Chain(w, seed, legs, out float c));
                carry.Add(c);
                allLegs.AddRange(legs);
            }
            totals.Sort();
            float mean = totals.Average(), p95 = totals[(int)(Seeds * 0.95f)], max = totals[Seeds - 1], min = totals[0];
            Console.WriteLine($"RELAY SIM skilled pilot over {Seeds} seeded chains: min {min:0.0} s, mean {mean:0.0} s, p95 {p95:0.0} s, max {max:0.0} s");
            Console.WriteLine($"RELAY SIM mean leg {allLegs.Average():0.0} s, longest leg {allLegs.Max():0.0} s, carry leg mean {carry.Average():0.0} s max {carry.Max():0.0} s");
            const float Fixed = RelayRun.ButtonHold + 5 * PressSeconds + RelayRun.KeycardPop;
            foreach (float stretch in new[] { 1.0f, 1.3f, 1.6f, 2.0f })
            {
                int fails = totals.Count(t => (t - Fixed) * stretch + Fixed > RelayRun.DefaultSeconds);
                Console.WriteLine($"RELAY SIM movement x{stretch:0.0}: {fails} of {Seeds} chains miss {RelayRun.DefaultSeconds:0} s (worst {(max - Fixed) * stretch + Fixed:0.0} s)");
            }
            Assert.That(max, Is.LessThan(RelayRun.DefaultSeconds), "even the longest chain fits the clock for the skilled pilot");
        }
    }
}
