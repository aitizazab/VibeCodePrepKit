using System;
using System.Collections.Generic;
using System.Numerics;
using JamKit.Movement;
using NUnit.Framework;
using static JamKit.Tests.Movement.MoveTestKit;

namespace JamKit.Tests.Movement
{
    /// <summary>The creator's first priority: momentum survives the chain slide -> jump -> wallrun ->
    /// wall kick -> double jump. And the fixed step: the same inputs at 30, 60, 144 and 240 fps give the
    /// same path.</summary>
    public class MomentumTests
    {
        /// <summary>Run-up floor to z 20, a pit after it, a wall on the right (face x = 3) from z 22.</summary>
        static BoxWorld ChainWorld()
        {
            var w = new BoxWorld();
            w.Floor(-10f, 10f, -80f, 20f, 0f);
            w.Add(new Vector3(3.5f, -11f, 46f), new Vector3(1f, 38f, 48f));
            return w;
        }

        public struct ChainResult
        {
            public float sprint, slideStart, slideJump, wallEntry, afterKick, afterDouble;
            public bool kick;
        }

        public static ChainResult RunChain()
        {
            var r = new ChainResult();
            var sim = Standing(ChainWorld(), new Vector3(2.1f, 0f, -40f));
            Run(sim, Forward(), 6f, s => s.Position.Z >= 12f);
            r.sprint = sim.HorizontalSpeed;
            sim.Step(Forward().With(crouchPressed: true), H);
            Assert.That(sim.Mode, Is.EqualTo(MoveMode.Slide), "slide");
            r.slideStart = sim.HorizontalSpeed;
            Run(sim, Forward().With(crouchHeld: true), 2f, s => s.Position.Z >= 18.5f);
            sim.Step(Forward().With(jump: true, crouchHeld: true), H);
            Assert.That(sim.Events.jumped, Is.True, "slide jump");
            r.slideJump = sim.HorizontalSpeed;
            bool onWall = false;
            Run(sim, Forward(), 1f, s => onWall = s.Mode == MoveMode.Wallrun);
            Assert.That(onWall, Is.True, "caught the wall");
            r.wallEntry = sim.HorizontalSpeed;
            Run(sim, Forward(), 0.05f);
            sim.Step(Forward().With(jump: true), H);
            Assert.That(sim.Events.wallJumped, Is.True, "wall jump");
            r.kick = sim.Events.wallKick;
            r.afterKick = sim.HorizontalSpeed;
            Run(sim, Forward(), 1f, s => s.Velocity.Y <= 0f);
            sim.Step(Forward().With(jump: true), H);
            Assert.That(sim.Events.doubleJumped, Is.True, "double jump");
            r.afterDouble = sim.HorizontalSpeed;
            return r;
        }

        [Test]
        public void SlideJumpWallrunWallKickDoubleJumpKeepsTheSpeed()
        {
            var r = RunChain();
            TestContext.Out.WriteLine($"sprint {r.sprint:0.00}, slide {r.slideStart:0.00}, slide jump {r.slideJump:0.00}, wall {r.wallEntry:0.00}, kick {r.afterKick:0.00}, double {r.afterDouble:0.00} m/s");
            Assert.That(r.slideStart, Is.GreaterThan(r.sprint + 2.9f), "the slide boost");
            Assert.That(r.slideJump, Is.GreaterThan(r.sprint + 1.5f), "the slide jump keeps the slide's speed");
            Assert.That(r.wallEntry, Is.EqualTo(r.slideJump).Within(0.35f), "the wall takes all of it");
            Assert.That(r.kick, Is.True, "a jump 50 ms after contact is a wall kick");
            Assert.That(r.afterKick, Is.GreaterThan(r.wallEntry + 1.5f), "the kick adds speed");
            Assert.That(r.afterDouble, Is.EqualTo(r.afterKick).Within(0.01f), "the double jump turns you without losing speed");
            Assert.That(r.afterDouble, Is.GreaterThan(r.sprint * 1.35f), "the chain ends well above sprint");
        }

        // ---------- Frame-rate independence ----------

        /// <summary>The input at time t: changes only at multiples of 1/6 s (frame boundaries at every tested rate).</summary>
        static MoveInput Timeline(double t, out bool jumpHeld, out bool crouchHeld)
        {
            var i = new MoveInput { move = new Vector2(0f, 1f), yaw = 0f };
            crouchHeld = t >= 3.0 - 1e-9 && t < 3.5 - 1e-9;
            jumpHeld = (t >= 3.5 - 1e-9 && t < 3.6667 - 1e-9) || (t >= 4.3333 - 1e-9 && t < 4.5 - 1e-9);
            if (t >= 4.0 - 1e-9) i.yaw = 8f;
            if (t >= 5.0 - 1e-9) i.move = new Vector2(0.6f, 0.8f);
            return i;
        }

        static List<Vector3> Trajectory(int fps)
        {
            var sim = Standing(ChainWorld(), new Vector3(0f, 0f, -70f));
            var driver = new MoveDriver(sim);
            var samples = new List<Vector3>();
            double dt = 1.0 / fps;
            bool lastJump = false, lastCrouch = false;
            for (int frame = 0; frame < fps * 6; frame++)
            {
                double t = frame * dt;
                var input = Timeline(t, out bool jump, out bool crouch);
                input.sprintHeld = true;
                input.jumpHeld = jump;
                input.jumpPressed = jump && !lastJump;
                input.crouchHeld = crouch;
                input.crouchPressed = crouch && !lastCrouch;
                lastJump = jump;
                lastCrouch = crouch;
                driver.Frame((float)dt, input);
                if ((frame + 1) % fps == 0) samples.Add(sim.Position);
            }
            return samples;
        }

        [Test]
        public void ThirtySixtyOneFortyFourAndTwoFortyFpsGiveTheSamePath()
        {
            var reference = Trajectory(120);
            Assert.That(reference[5].Z - reference[0].Z, Is.GreaterThan(30f), "the body really travelled");
            foreach (int fps in new[] { 30, 60, 144, 240 })
            {
                var path = Trajectory(fps);
                for (int s = 0; s < reference.Count; s++)
                    Assert.That(Vector3.Distance(path[s], reference[s]), Is.LessThan(1e-3f), fps + " fps at " + (s + 1) + " s: " + path[s] + " vs " + reference[s]);
            }
        }

        [Test]
        public void ATapIsNeverLostOnAFrameWithNoStepNorDoubledOnAFrameWithSeveral()
        {
            var sim = Standing(Flat(), Vector3.Zero);
            var driver = new MoveDriver(sim);
            // 240 fps: every other frame has no step. The tap lands on a no-step frame and still jumps once.
            driver.Frame(1f / 240f, new MoveInput());
            Assert.That(driver.Frame(1f / 240f, new MoveInput { jumpPressed = true, jumpHeld = true }), Is.EqualTo(1).Or.EqualTo(0));
            driver.Frame(1f / 240f, new MoveInput());
            driver.Frame(1f / 240f, new MoveInput());
            Assert.That(sim.Mode, Is.EqualTo(MoveMode.Air), "jumped");
            // 20 fps: one frame, several steps, one jump (no double jump straight after).
            var sim2 = Standing(Flat(), Vector3.Zero);
            var d2 = new MoveDriver(sim2);
            Assert.That(d2.Frame(1f / 20f, new MoveInput { jumpPressed = true, jumpHeld = true }), Is.EqualTo(6));
            Assert.That(d2.FrameEvents.jumped, Is.True);
            Assert.That(d2.FrameEvents.doubleJumped, Is.False);
            Assert.That(sim2.DoubleJumpAvailable, Is.True);
        }

        [Test]
        public void TheRenderedPositionIsInterpolatedBetweenTheLastTwoSteps()
        {
            var sim = Standing(Flat(), Vector3.Zero, velocity: new Vector3(0f, 0f, 9.5f));
            var driver = new MoveDriver(sim);
            driver.Frame(1f / 120f * 1.5f, Forward());
            Assert.That(driver.Alpha, Is.EqualTo(0.5f).Within(1e-3f));
            var p = driver.InterpolatedPosition;
            Assert.That(p.Z, Is.GreaterThan(driver.PreviousPosition.Z).And.LessThan(sim.Position.Z));
            // A long hitch runs at most 6 steps and drops the rest.
            Assert.That(driver.Frame(0.5f, Forward()), Is.EqualTo(6));
        }
    }
}
