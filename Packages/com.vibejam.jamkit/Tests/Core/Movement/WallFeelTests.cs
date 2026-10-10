using System;
using System.Numerics;
using JamKit.Movement;
using NUnit.Framework;
using static JamKit.Tests.Movement.MoveTestKit;

namespace JamKit.Tests.Movement
{
    /// <summary>The creator's controller notes (2026-10-10): the first half second on a wall keeps all your speed and
    /// the friction after it is softer; a stick held at an angle (or the view along the wall) still counts as forward;
    /// a stick sweeping away for a moment doesn't drop you; the wall catch says why it refused (the F3 diagnostic),
    /// and a catch refused for the stick's direction is retried for 0.1 s; the double jump pushes you forward a
    /// little, never past sprint; sliding down the Gauntlet's ramp is felt.</summary>
    public class WallFeelTests
    {
        static BoxWorld WallWorld(float length = 60f)
        {
            var w = new BoxWorld();
            w.Add(new Vector3(2.5f, -10f, length * 0.5f), new Vector3(1f, 40f, length)); // face at x = 2, normal -X
            return w;
        }

        static MoveSim OnWall(float speed)
        {
            var sim = Standing(WallWorld(), new Vector3(1.3f, 3f, 1f), null, 0f, new Vector3(0f, 0f, speed));
            Run(sim, Forward(), 0.2f, s => s.Mode == MoveMode.Wallrun);
            Assert.That(sim.Mode, Is.EqualTo(MoveMode.Wallrun));
            return sim;
        }

        static MoveInput StickAt(float degreesIntoWall, float yaw = 0f)
        {
            float r = degreesIntoWall * (float)Math.PI / 180f;
            return new MoveInput { move = new Vector2((float)Math.Sin(r), (float)Math.Cos(r)), yaw = yaw, sprintHeld = true };
        }

        [Test]
        public void TheFirstHalfSecondOnAWallKeepsAllYourSpeed()
        {
            var def = new MoveDef();
            Assert.That(def.wallSpeedGrace, Is.EqualTo(0.5f).Within(0.01f));
            Assert.That(def.wallRunFriction, Is.EqualTo(1.2f).Within(0.01f));
            var sim = OnWall(14f);
            Run(sim, Forward(), def.wallSpeedGrace - 0.05f);
            Assert.That(Vector3.Dot(sim.Velocity, sim.WallTangent), Is.EqualTo(14f).Within(0.05f), "no wall friction in the grace");
            Run(sim, Forward(), 0.75f);
            float along = Vector3.Dot(sim.Velocity, sim.WallTangent);
            TestContext.Out.WriteLine($"after 1.2 s on the wall: {along:0.00} m/s");
            Assert.That(along, Is.LessThan(13f).And.GreaterThan(10.5f), "then it fades toward 8.64 (1.2/s of the excess)");
        }

        [Test]
        public void AWallKickInTheGraceKeepsTheEntrySpeed()
        {
            var sim = OnWall(14f);
            Run(sim, Forward(), 0.35f);
            sim.Step(Forward().With(jump: true), H);
            Assert.That(sim.Mode, Is.EqualTo(MoveMode.Air));
            Assert.That(sim.HorizontalSpeed, Is.GreaterThan(14f), "a kick 0.35 s in still gains on the entry speed");
        }

        [TestCase(50f, 0f)]
        [TestCase(-50f, 0f)]
        [TestCase(80f, 0f)]
        [TestCase(0f, -50f)]
        public void AStickHeldAtAnAngleKeepsTheRunAndItsSpeed(float stickIntoWall, float viewYaw)
        {
            var sim = OnWall(14f);
            Run(sim, StickAt(stickIntoWall, viewYaw), 0.45f);
            Assert.That(sim.Mode, Is.EqualTo(MoveMode.Wallrun), $"stick {stickIntoWall} degrees, view {viewYaw}: still on the wall");
            Assert.That(Vector3.Dot(sim.Velocity, sim.WallTangent), Is.EqualTo(14f).Within(0.1f), "and not braked");
        }

        [Test]
        public void OnlyClearBackwardInputOrNoStickBrakes()
        {
            var back = OnWall(10f);
            Run(back, StickAt(180f), 0.2f);
            Assert.That(Vector3.Dot(back.Velocity, back.WallTangent), Is.LessThan(8f), "the stick pulled back brakes");
            var none = OnWall(10f);
            Run(none, new MoveInput(), 0.2f);
            Assert.That(Vector3.Dot(none.Velocity, none.WallTangent), Is.LessThan(9.5f), "no stick brakes (Titanfall 2)");
        }

        [Test]
        public void AStickSweepingPastAwayForAMomentDoesntDropYou()
        {
            var sim = OnWall(12f);
            Run(sim, Forward(), 0.1f);
            Run(sim, StickAt(-90f), 0.03f); // straight away from the wall for 30 ms
            Run(sim, Forward(), 0.1f);
            Assert.That(sim.Mode, Is.EqualTo(MoveMode.Wallrun));
            Run(sim, StickAt(-90f), 0.1f);
            Assert.That(sim.Mode, Is.EqualTo(MoveMode.Air), "held away, you let go");
        }

        [Test]
        public void TheCatchSaysWhyItRefused()
        {
            // Too slow.
            var slow = Standing(WallWorld(), new Vector3(1.4f, 3f, 1f), null, 0f, new Vector3(0f, 0f, 3.5f));
            Run(slow, Forward(), 0.1f);
            Assert.That(slow.Mode, Is.EqualTo(MoveMode.Air));
            Assert.That(slow.LastCatchMiss, Is.EqualTo(WallCatchMiss.TooSlow));

            // The stick pointing away from the wall.
            var away = Standing(WallWorld(), new Vector3(1.4f, 3f, 1f), null, 0f, new Vector3(0f, 0f, 10f));
            Run(away, StickAt(-80f), 0.05f);
            Assert.That(away.Mode, Is.EqualTo(MoveMode.Air));
            Assert.That(away.LastCatchMiss, Is.EqualTo(WallCatchMiss.StickAway));

            // No stick.
            var none = Standing(WallWorld(), new Vector3(1.4f, 3f, 1f), null, 0f, new Vector3(0f, 0f, 10f));
            Run(none, new MoveInput(), 0.05f);
            Assert.That(none.LastCatchMiss, Is.EqualTo(WallCatchMiss.NoStick));

            // Head-on: running straight at the wall.
            var head = Standing(WallWorld(), new Vector3(1.2f, 3f, 10f), null, 90f, new Vector3(10f, 0f, 0f));
            Run(head, new MoveInput { move = new Vector2(0f, 1f), yaw = 90f, sprintHeld = true }, 0.1f, s => s.Mode == MoveMode.Wallrun || s.LastCatchMiss != WallCatchMiss.None);
            Assert.That(head.Mode, Is.Not.EqualTo(MoveMode.Wallrun));
            Assert.That(head.LastCatchMiss, Is.EqualTo(WallCatchMiss.HeadOn));

            // Just out of reach.
            var far = Standing(WallWorld(), new Vector3(0.3f, 3f, 1f), null, 0f, new Vector3(0f, 0f, 10f));
            far.Step(Forward(), H);
            Assert.That(far.LastCatchMiss, Is.EqualTo(WallCatchMiss.OutOfReach));

            // A catch clears it.
            var ok = OnWall(10f);
            Assert.That(ok.LastCatchMiss, Is.EqualTo(WallCatchMiss.None));
        }

        [Test]
        public void TheSameWallRuleIsReportedAndKept()
        {
            // As WallrunTests' same-wall case: kick off, double jump back at the wall while still higher than where you left.
            var sim = Standing(WallWorld(80f), new Vector3(1.3f, 3f, 1f), null, 0f, new Vector3(0f, 0f, 10f));
            Run(sim, Forward(), 0.2f, s => s.Mode == MoveMode.Wallrun);
            Run(sim, Forward(), 0.1f);
            sim.Step(Forward().With(jump: true), H);
            Run(sim, Forward(), 0.15f);
            var back = new MoveInput { move = Vector2.Normalize(new Vector2(1f, 1f)), yaw = 0f };
            sim.Step(back.With(jump: true), H);
            bool seen = false;
            Run(sim, back, 2.5f, s => { seen |= s.LastCatchMiss == WallCatchMiss.SameWall; return s.Events.wallrunStarted; });
            Assert.That(seen, Is.True, "touching it higher than where you left was refused, and the diagnostic says why");
            Assert.That(sim.LastCatchMiss, Is.EqualTo(WallCatchMiss.None), "the lower catch cleared it");
        }

        [Test]
        public void ACatchRefusedForTheStickIsRetriedAsItComesRound()
        {
            var def = new MoveDef();
            float reach = def.radius + def.wallProbeReach;
            // Just inside the reach, drifting away from the wall at 6 m/s, with the stick pointing away.
            float x = 2f - def.wallProbeRadius - reach + 0.25f;
            var sim = Standing(WallWorld(), new Vector3(x, 3f, 1f), null, 0f, new Vector3(-6f, 0f, 10f));
            Run(sim, StickAt(-80f), 0.06f);
            Assert.That(sim.Mode, Is.EqualTo(MoveMode.Air));
            Assert.That(sim.LastCatchMiss, Is.EqualTo(WallCatchMiss.StickAway));
            Run(sim, Forward(), 0.035f, s => s.Mode == MoveMode.Wallrun);
            TestContext.Out.WriteLine($"caught at x {sim.Position.X:0.00} (the plain reach ends at {2f - def.wallProbeRadius - reach:0.00})");
            Assert.That(sim.Mode, Is.EqualTo(MoveMode.Wallrun), "the stick came round within 0.1 s: caught");
            Assert.That(sim.Position.X, Is.LessThan(2f - def.wallProbeRadius - reach), "a little beyond the plain reach (the buffer reaches 0.4 m further)");
        }

        [Test]
        public void TheDoubleJumpPushesForwardALittleNeverPastSprint()
        {
            var def = new MoveDef();
            Assert.That(def.doubleJumpForwardBoost, Is.EqualTo(1f).Within(0.01f));
            float After(float speed, MoveInput stick)
            {
                var w = new BoxWorld();
                w.Floor(-50f, 50f, -50f, 500f, 0f);
                var sim = Standing(w, Vector3.Zero, null, 0f, new Vector3(0f, 0f, speed));
                sim.Step(Forward().With(jump: true), H);
                Run(sim, stick, 0.2f);
                var j = stick;
                j.jumpPressed = j.jumpHeld = true;
                sim.Step(j, H);
                Assert.That(sim.Events.doubleJumped, Is.True);
                return sim.HorizontalSpeed;
            }
            var fwd = new MoveInput { move = new Vector2(0f, 1f), sprintHeld = true };
            Assert.That(After(5f, fwd), Is.EqualTo(6f).Within(0.1f), "+1 m/s from a slow start");
            Assert.That(After(9f, fwd), Is.EqualTo(def.sprintSpeed).Within(0.1f), "up to sprint, not past it");
            Assert.That(After(12f, fwd), Is.EqualTo(12f - def.hopSpeedLoss * (12f - def.sprintSpeed)).Within(0.05f), "nothing above sprint (the ground jump's hop cost is all that changes)");
            Assert.That(After(5f, new MoveInput()), Is.EqualTo(6f).Within(0.1f), "stick neutral, moving forward: along the view");
        }

        [Test]
        public void SlidingDownTheGauntletsRampIsFelt()
        {
            var g = GauntletCourse.Build();
            var c = g.course;
            var ramp = c.pieces.Find(p => p.name == "G Slide ramp");
            var world = LabCourseTests.WorldOf(c, null);
            var sim = new MoveSim(world, new MoveDef());
            var pilot = new CourseAutopilot(c);
            var start = c.checkpoints[g.sections[1].checkpoint];
            sim.Teleport(start.position, 0f, new Vector3(0f, 0f, sim.def.sprintSpeed));
            pilot.Reset(start.s);
            float top = -1f, bottom = -1f;
            for (int i = 0; i < 600 && bottom < 0f; i++)
            {
                sim.Step(pilot.Next(sim), H);
                if (top < 0f && sim.Position.Z >= ramp.Min.Z) top = sim.HorizontalSpeed;
                if (bottom < 0f && sim.Position.Z >= ramp.Max.Z) bottom = sim.HorizontalSpeed;
            }
            TestContext.Out.WriteLine($"slide ramp: {top:0.00} m/s at the top, {bottom:0.00} at the bottom ({bottom - top:+0.00;-0.00})");
            Assert.That(sim.Mode == MoveMode.Slide || bottom > 0f, Is.True);
            Assert.That(bottom - top, Is.GreaterThanOrEqualTo(2f), "the ramp speeds a slide up (slideSlopeScale)");
        }
    }
}
