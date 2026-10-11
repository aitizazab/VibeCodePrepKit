using System;
using System.Numerics;
using JamKit.Movement;
using NUnit.Framework;
using static JamKit.Tests.Movement.MoveTestKit;

namespace JamKit.Tests.Movement
{
    /// <summary>The keyboard-and-mouse exploits the creator found (2026-10-12; docs/MOVEMENT.md "One standard for mouse and
    /// stick"): sprinting on the ground while sweeping the view gained speed up to the 22 m/s cap (Source's ground
    /// accelerate caps only the projection on the wish direction), and the speed then carried for free through clean hops.
    /// Pinned here at every sweep rate, flicks included, the fastest stick's too; hops bleed; and the air-strafe turn-rate
    /// equalizer reins in sweeps faster than a stick can turn while leaving stick rates exactly as they were.</summary>
    public class GroundStrafeTests
    {
        static MoveSim OnFlat(float speed, MoveDef def = null)
        {
            var w = new BoxWorld();
            w.Floor(-3000f, 3000f, -3000f, 3000f, 0f);
            return Standing(w, Vector3.Zero, def, 0f, new Vector3(0f, 0f, speed));
        }

        static readonly MoveInput Sprint = new MoveInput { move = new Vector2(0f, 1f), sprintHeld = true };

        [TestCase(0f)]
        [TestCase(190f)]
        [TestCase(304f)]
        [TestCase(360f)]
        [TestCase(720f)]
        [TestCase(1440f)]
        public void SweepingTheViewWhileSprintingNeverPassesSprintSpeed(float degPerSec)
        {
            var sim = OnFlat(9.5f);
            float yaw = 0f, top = 0f;
            for (int i = 0; i < 1200; i++)
            {
                yaw += degPerSec * H;
                var input = Sprint;
                input.yaw = yaw;
                sim.Step(input, H);
                top = Math.Max(top, sim.HorizontalSpeed);
            }
            Assert.That(top, Is.LessThanOrEqualTo(sim.def.sprintSpeed + 1e-3f), $"sweeping at {degPerSec} deg/s for 10 s (was up to the 22 m/s cap from 304 deg/s)");
        }

        [Test]
        public void HoldingTheViewAheadOfTheRunOrFlickingItGainsNothing()
        {
            foreach (float ahead in new[] { 30f, 45f, 55f, 70f })
            {
                var sim = OnFlat(9.5f);
                float top = 0f;
                for (int i = 0; i < 1200; i++)
                {
                    var v = sim.Velocity;
                    var input = Sprint;
                    input.yaw = (float)(Math.Atan2(v.X, v.Z) * 180.0 / Math.PI) + ahead;
                    sim.Step(input, H);
                    top = Math.Max(top, sim.HorizontalSpeed);
                }
                Assert.That(top, Is.LessThanOrEqualTo(sim.def.sprintSpeed + 1e-3f), $"view held {ahead} deg ahead of the run (was 15.7 m/s at 55)");
            }
            // Flicks: the view jumps 30-150 degrees in a single step, back and forth.
            var flick = OnFlat(9.5f);
            float yaw = 0f, peak = 0f;
            var rng = new Random(7);
            for (int i = 0; i < 1200; i++)
            {
                if (i % 9 == 0) yaw += (rng.Next(2) == 0 ? 1 : -1) * (30f + 120f * (float)rng.NextDouble());
                var input = Sprint;
                input.yaw = yaw;
                flick.Step(input, H);
                peak = Math.Max(peak, flick.HorizontalSpeed);
            }
            Assert.That(peak, Is.LessThanOrEqualTo(flick.def.sprintSpeed + 1e-3f), "flicks");
            // Above sprint (after a slide or a wall kick) a sweep turns you but never speeds you up.
            var fast = OnFlat(15f);
            float y2 = 0f, prev = fast.HorizontalSpeed;
            for (int i = 0; i < 240; i++)
            {
                y2 += 360f * H;
                var input = Sprint;
                input.yaw = y2;
                fast.Step(input, H);
                Assert.That(fast.HorizontalSpeed, Is.LessThanOrEqualTo(prev + 1e-3f), "never rising above sprint on the ground");
                prev = fast.HorizontalSpeed;
            }
        }

        /// <summary>Ten clean hops straight on from 19 m/s, W held (the view swept or not): the fair carry is that most of
        /// the speed survives a chain (it took skill to earn) but not for free: at least 1 m/s goes over ten hops (Mild's
        /// 2% of the speed above sprint per hop: 19 to about 17.3).</summary>
        [TestCase(0f)]
        [TestCase(360f)]
        public void HopsAfterAnySpeedBleedARealAmount(float sweep)
        {
            var sim = OnFlat(19f);
            int hops = 0;
            bool dj = false;
            float yaw = 0f, t = 0f;
            while (hops < 10 && t < 20f)
            {
                yaw += sweep * H * (((int)(t * 2f)) % 2 == 0 ? 1f : -1f); // sweep back and forth
                var input = Sprint;
                input.yaw = yaw;
                if (sim.Grounded) { input.jumpPressed = input.jumpHeld = true; }
                else if (!dj && sim.Velocity.Y < 0f) { input.jumpPressed = input.jumpHeld = true; dj = true; }
                sim.Step(input, H);
                t += H;
                if (sim.Events.jumped) { hops++; dj = false; }
            }
            for (int i = 0; i < 240 && !sim.Grounded; i++) sim.Step(Sprint, H);
            TestContext.Out.WriteLine($"ten hops from 19 m/s (sweep {sweep} deg/s): {sim.HorizontalSpeed:0.00} m/s");
            Assert.That(sim.HorizontalSpeed, Is.LessThanOrEqualTo(18f), "a hop chain doesn't keep its speed for free");
            Assert.That(sim.HorizontalSpeed, Is.GreaterThan(15.5f), "but it keeps most of it (the controller's Mild feel)");
        }

        static float BestStrafe(float degPerSec, MoveDef def)
        {
            var w = new BoxWorld();
            w.Floor(-3000f, 3000f, -3000f, 3000f, -500f);
            var sim = Standing(w, Vector3.Zero, def, 0f, new Vector3(0f, 0f, 13f));
            float yaw = 0f;
            for (int i = 0; i < 240; i++) { yaw += degPerSec * H; sim.Step(new MoveInput { move = new Vector2(1f, 0f), yaw = yaw }, H); }
            return sim.HorizontalSpeed - 13f;
        }

        [Test]
        public void TheTurnEqualizerLeavesStickRatesAloneAndReinsInFasterSweeps()
        {
            var off = new MoveDef { airStrafeTurnCap = 0f };
            var on = new MoveDef();
            foreach (float stick in new[] { 120f, 190f, 304f })
                Assert.That(BestStrafe(stick, on), Is.EqualTo(BestStrafe(stick, off)).Within(1e-3f), $"a stick's {stick} deg/s strafes exactly as before");
            // The best a mouse can find (any rate) against the best a stick can (up to its 304 deg/s).
            float Best(MoveDef d, float maxRate) { float best = 0f; for (float r = 20f; r <= maxRate; r += 20f) best = Math.Max(best, BestStrafe(r, d)); return best; }
            float stickBest = Best(on, 304f), mouseBefore = Best(off, 1500f), mouseNow = Best(on, 1500f);
            TestContext.Out.WriteLine($"best 2 s strafe from 13 m/s: a stick (to 304 deg/s) +{stickBest:0.00} m/s; a mouse (any rate) +{mouseBefore:0.00} before, +{mouseNow:0.00} now");
            Assert.That(mouseNow, Is.LessThanOrEqualTo(stickBest + 0.15f), "a mouse sweep strafes no better than the fastest stick");
            Assert.That(mouseNow, Is.LessThanOrEqualTo(mouseBefore + 1e-3f));
        }
    }
}
