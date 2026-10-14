using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using JamKit.Movement;
using JamKit.Skyline;
using JamKit.Tests.Movement;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    /// <summary>
    /// The Relay's headless player: the real <see cref="MoveSim"/> walks the hall's solids (plus the Relay's ramp up to the
    /// catwalk) along the walkable graph through whole seeded chains (start, then the six stations in order), with the cable leg
    /// at 75% speed and no sprint, and the presses' holds and the keycard's pop added. It answers "is 100 seconds fair?": the
    /// perfect pilot's total, and the same with the movement stretched by what a person under fire loses (corners, strafing,
    /// shooting back). No enemies are simulated; the stretch stands for them.
    /// </summary>
    public class RelaySimTests
    {
        const float ZoneRadius = 2.0f;
        const float PressSeconds = 0.4f;     // the time between arriving and the press landing, per step

        static BoxWorld World()
        {
            var w = new BoxWorld();
            foreach (var s in RelayArena.Solids())
            {
                var b = s.box;
                if (s.ramp)
                {
                    var made = s.rampAlongX
                        ? w.RampX(b.min.X, b.max.X, b.min.Z, b.max.Z, s.rampTopStart, s.rampTopEnd, b.min.Y)
                        : w.Ramp(b.min.X, b.max.X, b.min.Z, b.max.Z, s.rampTopStart, s.rampTopEnd, b.min.Y);
                    made.runnable = s.runnable;
                }
                else w.Add((b.min + b.max) * 0.5f, b.max - b.min, s.runnable);
            }
            return w;
        }

        /// <summary>Walks from <paramref name="from"/> to within the station's zone; returns the seconds, or -1 if it didn't arrive.</summary>
        static float Leg(BoxWorld w, Vector3 from, Vector3 to, bool carrying, out Vector3 end)
        {
            var nav = RelayTestKit.Nav;
            int a = nav.NodeAt(from), b = nav.NodeAt(to);
            var path = nav.Path(a, b);
            Assert.That(path, Is.Not.Null, $"a walk {from} to {to}");
            var route = nav.Simplify(path);
            route.Add(to);
            var sim = MoveTestKit.Standing(w, from);
            var bot = new RouteWalker(route) { Sprint = !carrying };
            float t = 0f;
            while (t < 60f && !bot.Done)
            {
                float dx = to.X - sim.Position.X, dz = to.Z - sim.Position.Z;
                if (dx * dx + dz * dz <= ZoneRadius * ZoneRadius && Math.Abs(to.Y - sim.Position.Y) < 2f) break;
                var input = bot.Next(sim, t, MoveTestKit.H);
                if (carrying)
                {
                    input.move *= RelayRun.CarryScale;
                    input.sprintHeld = false;
                }
                sim.Step(input, MoveTestKit.H);
                t += MoveTestKit.H;
            }
            end = sim.Position;
            if (t >= 60f) Console.WriteLine("RELAY SIM STUCK at " + sim.Position + " mode " + sim.Mode + " waypoint " + bot.TargetIndex + "/" + route.Count + " route " + string.Join(" ", route.Select(p => "(" + p.X + "," + p.Y + "," + p.Z + ")")));
            return t < 60f ? t : -1f;
        }

        /// <summary>Total seconds for a seeded chain by the perfect pilot, with the presses' time; the legs in <paramref name="legs"/>.</summary>
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
                float t = Leg(w, at, to, carrying, out var end);
                Assert.That(t, Is.GreaterThanOrEqualTo(0f), $"seed {seed} leg {i} ({RelayChain.Letters(order)}) did not arrive from {at} to {to}");
                legs.Add(t);
                total += t;
                if (carrying) carrySeconds = t;
                at = to;
            }
            total += RelayRun.ButtonHold + 5 * PressSeconds + RelayRun.KeycardPop;
            return total;
        }

        [Test]
        public void TheCatwalkRampIsWalkableByTheRealMovement()
        {
            var w = World();
            var up = Leg(w, new Vector3(-21f, 0f, -14f), RelayArena.Stations[5], false, out var end);
            Assert.That(up, Is.GreaterThan(0f), "A to the catwalk on foot");
            Assert.That(end.Y, Is.EqualTo(RelayArena.DeckTop).Within(0.3f), "ends on the deck");
        }

        [Test]
        public void HowLongAChainTakesAndWhetherAHundredSecondsIsFair()
        {
            var w = World();
            var totals = new List<float>();
            var carry = new List<float>();
            var perHop = new List<float>();
            const int Seeds = 60;
            for (int seed = 0; seed < Seeds; seed++)
            {
                var legs = new List<float>();
                totals.Add(Chain(w, seed, legs, out float c));
                carry.Add(c);
                perHop.AddRange(legs);
            }
            totals.Sort();
            float mean = totals.Average(), p95 = totals[(int)(Seeds * 0.95f)], max = totals[Seeds - 1], min = totals[0];
            Console.WriteLine($"RELAY SIM perfect pilot over {Seeds} seeds: min {min:0.0} s, mean {mean:0.0} s, p95 {p95:0.0} s, max {max:0.0} s");
            Console.WriteLine($"RELAY SIM mean leg {perHop.Average():0.0} s, longest leg {perHop.Max():0.0} s, carry leg mean {carry.Average():0.0} s max {carry.Max():0.0} s");
            foreach (float stretch in new[] { 1.0f, 1.5f, 2.0f, 2.5f })
            {
                int fails = totals.Count(t => (t - (RelayRun.ButtonHold + 5 * PressSeconds + RelayRun.KeycardPop)) * stretch
                                              + (RelayRun.ButtonHold + 5 * PressSeconds + RelayRun.KeycardPop) > RelayRun.DefaultSeconds);
                Console.WriteLine($"RELAY SIM movement x{stretch:0.0}: {fails} of {Seeds} chains miss 100 s (worst {(max - 3.1f) * stretch + 3.1f:0.0} s)");
            }
            Assert.That(max, Is.LessThan(RelayRun.DefaultSeconds / 2f), "even the longest chain needs under half the clock for a perfect pilot");
        }
    }
}
