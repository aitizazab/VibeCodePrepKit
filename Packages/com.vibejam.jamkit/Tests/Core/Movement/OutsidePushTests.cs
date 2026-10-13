using System;
using System.Numerics;
using JamKit.Movement;
using NUnit.Framework;
using static JamKit.Tests.Movement.MoveTestKit;

namespace JamKit.Tests.Movement
{
    /// <summary>The opt-in outside push (Skyline Repo's Breacher; docs/SKYLINE-DESIGN.md 8): off by default; applied at the
    /// start of a step; an upward part takes off and ends a wall-run; ignored in a mantle; capped at MaxSpeed; never
    /// refreshes the double jump. AddPush says whether the push was queued (false: dropped).</summary>
    public class OutsidePushTests
    {
        static MoveSim Pushable(BoxWorld w, Vector3 at, Vector3 velocity = default)
        {
            var sim = Standing(w, at, null, 0f, velocity);
            sim.OutsidePushEnabled = true;
            return sim;
        }

        [Test]
        public void OffByDefaultAPushChangesNothing()
        {
            var a = Standing(Flat(), Vector3.Zero);
            var b = Standing(Flat(), Vector3.Zero);
            Assert.That(b.OutsidePushEnabled, Is.False);
            for (int i = 0; i < 240; i++)
            {
                if (i == 10) Assert.That(b.AddPush(new Vector3(3f, 9f, 2f)), Is.False, "refused while off");
                var input = Forward().With(jump: i == 30);
                a.Step(input, H);
                b.Step(input, H);
                Assert.That(b.Position, Is.EqualTo(a.Position), "step " + i);
                Assert.That(b.Velocity, Is.EqualTo(a.Velocity), "step " + i);
            }
        }

        [Test]
        public void AnUpwardPushOnTheGroundTakesOff()
        {
            var sim = Pushable(Flat(), Vector3.Zero);
            Assert.That(sim.Grounded, Is.True);
            Assert.That(sim.AddPush(new Vector3(0f, 7f, 0f)), Is.True, "queued");
            sim.Step(new MoveInput(), H);
            Assert.That(sim.Grounded, Is.False);
            Assert.That(sim.Mode, Is.EqualTo(MoveMode.Air));
            Assert.That(sim.Events.leftGround, Is.True);
            float top = 0f;
            Run(sim, new MoveInput(), 2f, s => { top = Math.Max(top, s.Position.Y); return s.Grounded; });
            Assert.That(top, Is.EqualTo(49f / (2f * sim.def.gravity)).Within(0.08f), "7 m/s up rises v^2 / 2g");
        }

        [Test]
        public void AFlatPushOnTheGroundStaysOnTheGround()
        {
            var sim = Pushable(Flat(), Vector3.Zero);
            Assert.That(sim.AddPush(new Vector3(0f, 0f, -6f)), Is.True);
            sim.Step(new MoveInput(), H);
            Assert.That(sim.Grounded, Is.True);
            Assert.That(sim.Velocity.Z, Is.LessThan(-4f));
        }

        [Test]
        public void AnUpwardPushEndsAWallrunWithNoWallJumpAfterIt()
        {
            var w = new BoxWorld();
            w.Add(new Vector3(2.5f, -10f, 30f), new Vector3(1f, 40f, 60f));
            var sim = Pushable(w, new Vector3(1.3f, 3f, 1f), new Vector3(0f, 0f, 10f));
            Run(sim, Forward(), 0.2f, s => s.Mode == MoveMode.Wallrun);
            Assert.That(sim.Mode, Is.EqualTo(MoveMode.Wallrun));
            Assert.That(sim.AddPush(new Vector3(0f, 5f, 0f)), Is.True);
            sim.Step(Forward(), H);
            Assert.That(sim.Mode, Is.EqualTo(MoveMode.Air));
            Assert.That(sim.Events.wallrunEnded, Is.True);
            sim.Step(Forward().With(jump: true), H);
            Assert.That(sim.Events.wallJumped, Is.False, "the push used up the wall");
        }

        [Test]
        public void APushDuringAMantleIsIgnored()
        {
            var w = Flat();
            w.Floor(-10f, 10f, 5f, 30f, 1.5f, 2.5f);
            var sim = Pushable(w, Vector3.Zero, new Vector3(0f, 0f, 8f));
            Run(sim, Forward(), 2f, s => s.Mode == MoveMode.Mantle);
            Assert.That(sim.Mode, Is.EqualTo(MoveMode.Mantle));
            Assert.That(sim.AddPush(new Vector3(0f, 12f, 0f)), Is.False, "dropped: the caller's boost must not be spent");
            float top = 0f;
            Run(sim, Forward(), 1.5f, s => { top = Math.Max(top, s.Position.Y); return false; });
            Assert.That(sim.Position.Y, Is.EqualTo(1.5f).Within(0.06f), "on top of the ledge");
            Assert.That(top, Is.LessThan(1.5f + 0.3f), "the push never launched it, then or later");
        }

        [Test]
        public void APushNeverTakesTheFlatSpeedPastTheCap()
        {
            var sim = Pushable(new BoxWorld(), new Vector3(0f, 50f, 0f), new Vector3(0f, 0f, 20f));
            Assert.That(sim.AddPush(new Vector3(0f, 0f, 15f)), Is.True);
            sim.Step(new MoveInput(), H);
            Assert.That(sim.HorizontalSpeed, Is.LessThanOrEqualTo(sim.MaxSpeed + 1e-3f));
            Assert.That(sim.MaxSpeed, Is.EqualTo(22f));
        }

        [Test]
        public void APushNeverRefreshesTheDoubleJump()
        {
            var sim = Pushable(Flat(), Vector3.Zero);
            sim.Step(Forward().With(jump: true), H);
            Run(sim, Forward(), 0.3f);
            sim.Step(Forward().With(jump: true), H);
            Assert.That(sim.Events.doubleJumped, Is.True);
            Assert.That(sim.DoubleJumpAvailable, Is.False, "spent");
            Assert.That(sim.AddPush(new Vector3(0f, 6f, 0f)), Is.True);
            sim.Step(Forward(), H);
            Assert.That(sim.DoubleJumpAvailable, Is.False);
        }

        [Test]
        public void PushesQueuedBeforeAStepAddUpAndApplyOnce()
        {
            var sim = Pushable(new BoxWorld(), new Vector3(0f, 50f, 0f));
            Assert.That(sim.AddPush(new Vector3(1f, 0f, 0f)), Is.True);
            Assert.That(sim.AddPush(new Vector3(2f, 0f, 0f)), Is.True);
            sim.Step(new MoveInput(), H);
            Assert.That(sim.Velocity.X, Is.EqualTo(3f).Within(1e-4f));
            sim.Step(new MoveInput(), H);
            Assert.That(sim.Velocity.X, Is.EqualTo(3f).Within(1e-4f), "not applied twice");
        }

        [Test]
        public void ABrokenPushIsIgnoredAndTeleportDropsAQueuedOne()
        {
            var sim = Pushable(new BoxWorld(), new Vector3(0f, 50f, 0f));
            Assert.That(sim.AddPush(new Vector3(float.NaN, 0f, 0f)), Is.False);
            Assert.That(sim.AddPush(new Vector3(0f, float.PositiveInfinity, 0f)), Is.False);
            sim.Step(new MoveInput(), H);
            Assert.That(sim.Velocity.X, Is.EqualTo(0f));
            Assert.That(float.IsNaN(sim.Velocity.Y), Is.False);
            Assert.That(sim.AddPush(new Vector3(5f, 0f, 0f)), Is.True);
            sim.Teleport(new Vector3(0f, 50f, 0f), 0f, Vector3.Zero);
            sim.Step(new MoveInput(), H);
            Assert.That(sim.Velocity.X, Is.EqualTo(0f), "a respawn drops a queued push");
        }

        [Test]
        public void TurningThePushOffDropsAQueuedOne()
        {
            var sim = Pushable(new BoxWorld(), new Vector3(0f, 50f, 0f));
            Assert.That(sim.AddPush(new Vector3(4f, 0f, 0f)), Is.True);
            sim.OutsidePushEnabled = false;
            sim.Step(new MoveInput(), H);
            Assert.That(sim.Velocity.X, Is.EqualTo(0f));
        }
    }
}
