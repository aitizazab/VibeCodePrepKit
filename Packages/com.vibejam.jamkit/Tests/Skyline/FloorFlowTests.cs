using System.Collections.Generic;
using System.Linq;
using JamKit.Skyline;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    /// <summary>Task R1: the floor's phase machine (docs/SKYLINE-PLAN-FLOOR1.md, decision D19d).</summary>
    public class FloorFlowTests
    {
        static readonly string[] Calls = { "lift", "doors", "all", "exit", "wreck" };

        static bool Call(FloorFlow f, string c)
        {
            switch (c)
            {
                case "lift": return f.SteppedIntoLift();
                case "doors": return f.DoorsOpened();
                case "all": return f.AllTowed();
                case "exit": return f.ReachedExit();
                default: return f.Wrecked();
            }
        }

        static FloorFlow At(FloorPhase p)
        {
            var f = new FloorFlow();
            foreach (var c in new[] { "lift", "doors", "all", "exit" })
            {
                if (f.Phase == p) break;
                Call(f, c);
            }
            return f;
        }

        [Test]
        public void StartsInTheLobbyAndRaisesNothing()
        {
            var seen = new List<FloorPhase>();
            var f = new FloorFlow();
            f.Entered += seen.Add;
            Assert.That(f.Phase, Is.EqualTo(FloorPhase.Lobby));
            Assert.That(seen, Is.Empty);
        }

        [Test]
        public void TheLegalSequence()
        {
            var f = new FloorFlow();
            var seen = new List<FloorPhase>();
            f.Entered += seen.Add;
            Assert.That(f.SteppedIntoLift(), Is.True);
            Assert.That(f.Phase, Is.EqualTo(FloorPhase.LiftRide));
            Assert.That(f.DoorsOpened(), Is.True);
            Assert.That(f.Phase, Is.EqualTo(FloorPhase.Floor));
            Assert.That(f.AllTowed(), Is.True);
            Assert.That(f.Phase, Is.EqualTo(FloorPhase.Finale));
            Assert.That(f.ReachedExit(), Is.True);
            Assert.That(f.Phase, Is.EqualTo(FloorPhase.Done));
            Assert.That(seen, Is.EqualTo(new[] { FloorPhase.LiftRide, FloorPhase.Floor, FloorPhase.Finale, FloorPhase.Done }));
        }

        // the one legal call per phase; every other call must be refused and change nothing
        [TestCase(FloorPhase.Lobby, "lift", FloorPhase.LiftRide)]
        [TestCase(FloorPhase.LiftRide, "doors", FloorPhase.Floor)]
        [TestCase(FloorPhase.Floor, "all", FloorPhase.Finale)]
        [TestCase(FloorPhase.Finale, "exit", FloorPhase.Done)]
        public void EveryIllegalCallIsIgnored(FloorPhase phase, string legal, FloorPhase next)
        {
            foreach (var c in Calls)
            {
                if (c == legal || (c == "wreck" && phase != FloorPhase.Lobby)) continue;
                var f = At(phase);
                var seen = new List<FloorPhase>();
                f.Entered += seen.Add;
                Assert.That(Call(f, c), Is.False, phase + " + " + c);
                Assert.That(f.Phase, Is.EqualTo(phase), phase + " + " + c + " must not move the phase");
                Assert.That(seen, Is.Empty);
            }
            var g = At(phase);
            Assert.That(Call(g, legal), Is.True);
            Assert.That(g.Phase, Is.EqualTo(next));
        }

        [Test]
        public void DoneIgnoresEverythingIncludingAWreck()
        {
            var f = At(FloorPhase.Done);
            foreach (var c in Calls) Assert.That(Call(f, c), Is.False, c);
            Assert.That(f.Phase, Is.EqualTo(FloorPhase.Done));
        }

        [Test]
        public void WreckedInTheLobbyDoesNothing()
        {
            var f = new FloorFlow();
            Assert.That(f.Wrecked(), Is.False);
            Assert.That(f.Phase, Is.EqualTo(FloorPhase.Lobby));
        }

        [TestCase(FloorPhase.LiftRide)]
        [TestCase(FloorPhase.Floor)]
        [TestCase(FloorPhase.Finale)]
        public void WreckedFromEveryLivePhaseReturnsToTheLobbyAndTellsTheDirector(FloorPhase phase)
        {
            var f = At(phase);
            var seen = new List<FloorPhase>();
            f.Entered += seen.Add;
            Assert.That(f.Wrecked(), Is.True);
            Assert.That(f.Phase, Is.EqualTo(FloorPhase.Lobby));
            Assert.That(seen, Is.EqualTo(new[] { FloorPhase.Lobby }), "Entered(Lobby) is the restart signal");
        }

        [Test]
        public void TheRestartPathEndsInTheLobbyAndTheFloorCanBePlayedAgain()
        {
            var f = new FloorFlow();
            f.SteppedIntoLift(); f.DoorsOpened();
            Assert.That(f.Wrecked(), Is.True);
            Assert.That(f.Phase, Is.EqualTo(FloorPhase.Lobby));
            Assert.That(f.DoorsOpened(), Is.False, "the old ride is gone");
            Assert.That(f.SteppedIntoLift(), Is.True);
            Assert.That(f.DoorsOpened() && f.AllTowed() && f.ReachedExit(), Is.True);
            Assert.That(f.Phase, Is.EqualTo(FloorPhase.Done));
        }

        [Test]
        public void EnteredSeesTheNewPhaseAlreadySet()
        {
            var f = new FloorFlow();
            FloorPhase atEvent = FloorPhase.Done;
            f.Entered += p => atEvent = f.Phase;
            f.SteppedIntoLift();
            Assert.That(atEvent, Is.EqualTo(FloorPhase.LiftRide));
        }

        [Test]
        public void RandomCallSequencesNeverBreakTheMachine()
        {
            var rng = new Rng(5);
            var legalNext = new Dictionary<FloorPhase, FloorPhase[]>
            {
                { FloorPhase.Lobby, new[] { FloorPhase.LiftRide } },
                { FloorPhase.LiftRide, new[] { FloorPhase.Floor, FloorPhase.Lobby } },
                { FloorPhase.Floor, new[] { FloorPhase.Finale, FloorPhase.Lobby } },
                { FloorPhase.Finale, new[] { FloorPhase.Done, FloorPhase.Lobby } },
                { FloorPhase.Done, new FloorPhase[0] },
            };
            for (int run = 0; run < 200; run++)
            {
                var f = new FloorFlow();
                for (int i = 0; i < 40; i++)
                {
                    var before = f.Phase;
                    bool moved = Call(f, Calls[rng.Range(0, Calls.Length)]);
                    Assert.That(moved, Is.EqualTo(f.Phase != before));
                    if (moved) Assert.That(legalNext[before], Does.Contain(f.Phase));
                }
            }
        }
    }
}
