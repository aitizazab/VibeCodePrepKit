using System.Numerics;
using JamKit.Skyline;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    /// <summary>The Relay's step machine, the cable's costs, the clock and the wipe (docs/SKYLINE-RELAY.md).</summary>
    public class RelayRunTests
    {
        static RelayRun Run(int seed = 7, float seconds = RelayRun.DefaultSeconds) => new RelayRun(RelayTestKit.Real, seed, seconds);

        static int Other(RelayRun r, params int[] not)
        {
            for (int s = 0; s < 6; s++)
            {
                bool used = false;
                foreach (int n in not) if (n == s) used = true;
                if (!used) return s;
            }
            return -1;
        }

        [Test]
        public void ARunStartsOnTheButtonWithTheClockFull()
        {
            var r = Run();
            Assert.That(r.Step, Is.EqualTo(0));
            Assert.That(r.CurrentKind, Is.EqualTo(RelayStepKind.Button));
            Assert.That(r.CurrentStation, Is.EqualTo(r.ButtonStation));
            Assert.That(r.Remaining, Is.EqualTo(RelayRun.DefaultSeconds));
            Assert.That(r.Outcome, Is.EqualTo(RelayOutcome.Running));
        }

        [Test]
        public void StepsHappenInOrderAndAPerfectRunWins()
        {
            var r = Run();
            var kinds = new System.Collections.Generic.List<RelayStepKind>();
            r.StepDone += (i, k, t) => kinds.Add(k);
            RelayTestKit.DoAll(r);
            Assert.That(kinds, Is.EqualTo(RelayChain.Steps));
            Assert.That(r.Outcome, Is.EqualTo(RelayOutcome.Won));
            Assert.That(r.Step, Is.EqualTo(5));
        }

        [Test]
        public void InteractingAtTheWrongStationDoesNothing()
        {
            var r = Run();
            int wrong = Other(r, r.ButtonStation);
            Assert.That(r.Interact(wrong, 5f), Is.EqualTo(RelayResult.NoEffect));
            Assert.That(r.Step, Is.EqualTo(0));
            // a later step's station, pressed early, also does nothing
            Assert.That(r.Interact(r.ReelStation, 5f), Is.EqualTo(RelayResult.NoEffect));
            Assert.That(r.Interact(r.SocketStation, 5f), Is.EqualTo(RelayResult.NoEffect));
            Assert.That(r.Interact(r.ReaderStation, 5f), Is.EqualTo(RelayResult.NoEffect));
            Assert.That(r.Interact(-1, 5f), Is.EqualTo(RelayResult.NoEffect));
            Assert.That(r.Step, Is.EqualTo(0));
            Assert.That(r.Carrying, Is.False);
        }

        [Test]
        public void TheButtonNeedsAHalfSecondHold()
        {
            var r = Run();
            Assert.That(r.HoldFor(r.ButtonStation), Is.EqualTo(0.5f));
            Assert.That(r.Interact(r.ButtonStation, 0.2f), Is.EqualTo(RelayResult.Progress));
            Assert.That(r.Step, Is.EqualTo(0));
            Assert.That(r.Interact(r.ButtonStation, 0.5f), Is.EqualTo(RelayResult.Done));
            Assert.That(r.Step, Is.EqualTo(1));
        }

        [Test]
        public void TheCableCannotBePluggedWithoutBeingCarried()
        {
            var r = Run();
            r.Interact(r.ButtonStation, 1f);
            // the plug socket is the third station: pressing it before taking the cable does nothing
            Assert.That(r.Interact(r.SocketStation, 0f), Is.EqualTo(RelayResult.NoEffect));
            Assert.That(r.Plugged, Is.False);
            Assert.That(r.Interact(r.ReelStation, 0f), Is.EqualTo(RelayResult.Done));
            Assert.That(r.Carrying, Is.True);
            Assert.That(r.Interact(r.ReelStation, 0f), Is.EqualTo(RelayResult.NoEffect), "the reel's end is already in hand");
            Assert.That(r.Interact(r.SocketStation, 0f), Is.EqualTo(RelayResult.Done));
            Assert.That(r.Plugged, Is.True);
            Assert.That(r.Carrying, Is.False);
        }

        [Test]
        public void CarryingCostsSpeedAndTheSprint()
        {
            var r = Run();
            Assert.That(r.MoveScale, Is.EqualTo(1f));
            Assert.That(r.SprintAllowed, Is.True);
            r.Interact(r.ButtonStation, 1f);
            r.Interact(r.ReelStation, 0f);
            Assert.That(r.MoveScale, Is.EqualTo(0.75f));
            Assert.That(r.SprintAllowed, Is.False);
            r.Interact(r.SocketStation, 0f);
            Assert.That(r.MoveScale, Is.EqualTo(1f));
            Assert.That(r.SprintAllowed, Is.True);
        }

        [Test]
        public void TheKeycardIsNeededForTheSwipe()
        {
            var r = Run();
            r.Interact(r.ButtonStation, 1f);
            r.Interact(r.ReelStation, 0f);
            r.Interact(r.SocketStation, 0f);
            Assert.That(r.KeycardPopped, Is.True);
            Assert.That(r.HasKeycard, Is.False);
            Assert.That(r.Interact(r.ReaderStation, 0f), Is.EqualTo(RelayResult.NoEffect), "no card in hand yet");
            Assert.That(r.DoorOpen, Is.False);
            r.PlayerAt(r.SocketStation);
            Assert.That(r.HasKeycard, Is.False, "the card is still popping out");
            r.Tick(RelayRun.KeycardPop);
            r.PlayerAt(r.ReaderStation);
            Assert.That(r.HasKeycard, Is.False, "picked up only at the socket");
            r.PlayerAt(r.SocketStation);
            Assert.That(r.HasKeycard, Is.True);
            Assert.That(r.Interact(r.ReaderStation, 0f), Is.EqualTo(RelayResult.Done));
            Assert.That(r.DoorOpen, Is.True);
        }

        [Test]
        public void TheDoorOpensOnlyAfterTheSwipe()
        {
            var r = Run();
            r.Interact(r.ButtonStation, 1f);
            r.Interact(r.ReelStation, 0f);
            r.Interact(r.SocketStation, 0f);
            r.Tick(1f);
            r.PlayerAt(r.SocketStation);
            Assert.That(r.DoorOpen, Is.False);
            r.PlayerAt(r.DoorStation);
            Assert.That(r.DoorPassed, Is.False, "a closed door is not passed");
            r.Interact(r.ReaderStation, 0f);
            Assert.That(r.DoorOpen, Is.True);
        }

        [Test]
        public void WinningNeedsTheOpenDoorPassedAndThenTheLift()
        {
            var r = Run();
            r.Interact(r.ButtonStation, 1f);
            r.Interact(r.ReelStation, 0f);
            r.Interact(r.SocketStation, 0f);
            r.Tick(1f);
            r.PlayerAt(r.SocketStation);
            r.PlayerAt(r.LiftStation);
            Assert.That(r.Outcome, Is.EqualTo(RelayOutcome.Running), "the lift before the swipe does nothing");
            r.Interact(r.ReaderStation, 0f);
            r.PlayerAt(r.LiftStation);
            Assert.That(r.Outcome, Is.EqualTo(RelayOutcome.Running), "the lift without passing the open door does nothing");
            r.PlayerAt(r.DoorStation);
            Assert.That(r.DoorPassed, Is.True);
            r.PlayerAt(r.LiftStation);
            Assert.That(r.Outcome, Is.EqualTo(RelayOutcome.Won));
        }

        [Test]
        public void TheTetherIsTwentyPercentLongerThanTheWalkAndPullsTaut()
        {
            var r = Run();
            float walk = RelayTestKit.Real.PathLength(r.ReelStation, r.SocketStation);
            Assert.That(r.TetherLength, Is.EqualTo(walk * 1.2f).Within(0.01f));
            // not carrying: free to go anywhere
            var far = new Vector3(500f, 0f, 500f);
            Assert.That(r.ClampFeet(far), Is.EqualTo(far));
            r.Interact(r.ButtonStation, 1f);
            r.Interact(r.ReelStation, 0f);
            var anchor = r.TetherAnchor;
            Assert.That(r.ClampFeet(anchor + new Vector3(1f, 0f, 0f)), Is.EqualTo(anchor + new Vector3(1f, 0f, 0f)), "slack");
            var clamped = r.ClampFeet(anchor + new Vector3(r.TetherLength + 30f, 0f, 0f));
            Assert.That(Vector3.Distance(anchor, clamped), Is.EqualTo(r.TetherLength).Within(0.01f), "taut: it stops you at its length");
            Assert.That(r.Outcome, Is.EqualTo(RelayOutcome.Running), "it never breaks");
            // the socket is always inside the tether
            Assert.That(Vector3.Distance(anchor, RelayTestKit.Real.Position(r.SocketStation)), Is.LessThan(r.TetherLength));
        }

        [Test]
        public void TheWipeFiresAtZeroAndNothingWorksAfterIt()
        {
            var r = Run(3, 10f);
            RelayOutcome got = RelayOutcome.Running;
            float at = -1f;
            r.Ended += (o, t) => { got = o; at = t; };
            r.Tick(9.9f);
            Assert.That(r.Outcome, Is.EqualTo(RelayOutcome.Running));
            r.Tick(0.2f);
            Assert.That(r.Outcome, Is.EqualTo(RelayOutcome.Wiped));
            Assert.That(got, Is.EqualTo(RelayOutcome.Wiped));
            Assert.That(at, Is.EqualTo(10f));
            Assert.That(r.Remaining, Is.EqualTo(0f));
            Assert.That(r.Interact(r.ButtonStation, 1f), Is.EqualTo(RelayResult.NoEffect));
            RelayTestKit.DoAll(r);
            Assert.That(r.Outcome, Is.EqualTo(RelayOutcome.Wiped), "a wiped run cannot be won");
        }

        [Test]
        public void BeingWreckedWipesTheRunAtOnce()
        {
            var r = Run();
            r.Tick(12.5f);
            RelayOutcome got = RelayOutcome.Running;
            float at = -1f;
            r.Ended += (o, t) => { got = o; at = t; };
            r.Wipe();
            Assert.That(got, Is.EqualTo(RelayOutcome.Wiped));
            Assert.That(at, Is.EqualTo(12.5f).Within(0.001f));
            r.Wipe();
            Assert.That(r.Outcome, Is.EqualTo(RelayOutcome.Wiped));
            r.RestartNew();
            Assert.That(r.Outcome, Is.EqualTo(RelayOutcome.Running));
        }

        [Test]
        public void ARestartMakesANewRandomOrderAndRefillsTheClock()
        {
            var r = Run(11, 20f);
            string first = r.Letters;
            r.Interact(r.ButtonStation, 1f);
            r.Interact(r.ReelStation, 0f);
            r.Tick(25f);
            Assert.That(r.Outcome, Is.EqualTo(RelayOutcome.Wiped));
            for (int i = 0; i < 20; i++)
            {
                string before = r.Letters;
                r.RestartNew();
                Assert.That(r.Letters, Is.Not.EqualTo(before), "a different order every time");
                Assert.That(r.Outcome, Is.EqualTo(RelayOutcome.Running));
                Assert.That(r.Step, Is.EqualTo(0));
                Assert.That(r.Remaining, Is.EqualTo(20f));
                Assert.That(r.Carrying, Is.False);
                Assert.That(r.HasKeycard, Is.False);
                Assert.That(r.DoorOpen, Is.False);
            }
            r.Restart(11);
            Assert.That(r.Letters, Is.EqualTo(first), "the same seed brings the same order back");
        }

        [Test]
        public void EventsReportTheStepAndItsTime()
        {
            var r = Run();
            var log = new System.Collections.Generic.List<string>();
            r.StepDone += (i, k, t) => log.Add($"{i + 1} {k} {t:0.0}");
            r.Tick(4.25f);
            r.Interact(r.ButtonStation, 1f);
            Assert.That(log, Is.EqualTo(new[] { "1 Button 4.3" }).Or.EqualTo(new[] { "1 Button 4.2" }));
        }
    }
}
