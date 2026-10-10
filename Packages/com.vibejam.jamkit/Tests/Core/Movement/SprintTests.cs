using System;
using System.Numerics;
using JamKit.Movement;
using NUnit.Framework;
using static JamKit.Tests.Movement.MoveTestKit;

namespace JamKit.Tests.Movement
{
    /// <summary>Opt-in sprint (the creator, 2026-10-10): walking unless you sprint (Shift / LB held, or the pad's L3
    /// toggle, Titanfall-style), the toggle ending on stop, crouch and respawn; the sprint kick that pushes you back
    /// to running speed after a stumble (fast, never past sprint, on a cooldown, readying the slide boost); and air
    /// strafing left exactly as it was.</summary>
    public class SprintTests
    {
        static MoveInput Walk() => new MoveInput { move = new Vector2(0f, 1f) };
        static MoveInput Toggle() => new MoveInput { move = new Vector2(0f, 1f), sprintTogglePressed = true };

        [Test]
        public void SprintIsOffByDefaultSoForwardWalks()
        {
            var sim = Standing(Flat(), Vector3.Zero);
            Assert.That(sim.def.autoSprint, Is.False);
            float max = 0f;
            for (int i = 0; i < 240; i++) { sim.Step(Walk(), H); max = Math.Max(max, sim.HorizontalSpeed); }
            Assert.That(sim.HorizontalSpeed, Is.EqualTo(sim.def.walkSpeed).Within(0.05f), "walk speed");
            Assert.That(max, Is.LessThanOrEqualTo(sim.def.walkSpeed + 1e-3f));
            Assert.That(sim.Mode, Is.EqualTo(MoveMode.Grounded));
        }

        [Test]
        public void HoldingSprintOrClickingTheToggleReachesSprintSpeed()
        {
            var held = Standing(Flat(), Vector3.Zero);
            Run(held, Forward(), 1f);
            Assert.That(held.HorizontalSpeed, Is.EqualTo(held.def.sprintSpeed).Within(0.05f), "Shift / LB held");
            Assert.That(held.Mode, Is.EqualTo(MoveMode.Sprint));

            var toggled = Standing(Flat(), Vector3.Zero);
            toggled.Step(Toggle(), H);
            Run(toggled, Walk(), 1f);
            Assert.That(toggled.SprintToggled, Is.True);
            Assert.That(toggled.HorizontalSpeed, Is.EqualTo(toggled.def.sprintSpeed).Within(0.05f), "L3 clicked once, then just the stick");
            Assert.That(toggled.Mode, Is.EqualTo(MoveMode.Sprint));
            toggled.Step(Toggle(), H);
            Assert.That(toggled.SprintToggled, Is.True, "a second click keeps it on (it ends by stopping, crouching, sliding)");
        }

        [Test]
        public void SprintIsForwardOnly()
        {
            var sim = Standing(Flat(), Vector3.Zero);
            Run(sim, new MoveInput { move = new Vector2(1f, 0f), sprintHeld = true }, 1f);
            Assert.That(sim.HorizontalSpeed, Is.EqualTo(sim.def.walkSpeed).Within(0.05f), "strafing at walk speed even with Shift held");
        }

        [Test]
        public void TheToggleEndsWhenYouStopCrouchSlideOrRespawn()
        {
            // Stop: the stick released.
            var sim = Standing(Flat(), Vector3.Zero);
            sim.Step(Toggle(), H);
            Run(sim, Walk(), 0.5f);
            sim.Step(new MoveInput(), H);
            Assert.That(sim.SprintToggled, Is.False, "released the stick");
            Run(sim, Walk(), 1f);
            Assert.That(sim.HorizontalSpeed, Is.EqualTo(sim.def.walkSpeed).Within(0.05f), "walking again until the next click");

            // Pulled back.
            sim.Step(Toggle(), H);
            Run(sim, Walk(), 0.3f);
            sim.Step(new MoveInput { move = new Vector2(0f, -0.5f) }, H);
            Assert.That(sim.SprintToggled, Is.False, "pulled back");

            // Crouch (or a slide, which needs crouch).
            sim.Step(Toggle(), H);
            Run(sim, Walk(), 0.5f);
            sim.Step(Walk().With(crouchPressed: true), H);
            Assert.That(sim.SprintToggled, Is.False, "crouched / slid");

            // Respawn.
            sim.Step(Toggle(), H);
            Assert.That(sim.SprintToggled, Is.True);
            sim.Teleport(new Vector3(0f, 0f, 0f), 0f, Vector3.Zero);
            Assert.That(sim.SprintToggled, Is.False, "respawned");
        }

        [Test]
        public void TheToggleSurvivesAJumpWithTheStickSideways()
        {
            var sim = Standing(Flat(), Vector3.Zero);
            sim.Step(Toggle(), H);
            Run(sim, Walk(), 0.5f);
            sim.Step(Walk().With(jump: true), H);
            Run(sim, new MoveInput { move = new Vector2(1f, 0f) }, 0.3f); // air strafe: no forward
            Assert.That(sim.Mode, Is.EqualTo(MoveMode.Air));
            Assert.That(sim.SprintToggled, Is.True, "the toggle only ends on the ground");
        }

        [Test]
        public void TheKickReachesSprintFromANearStopFastAndNeverPassesIt()
        {
            var sim = Standing(Flat(), Vector3.Zero, velocity: new Vector3(0f, 0f, 0.5f));
            float t = 0f, reached = -1f, max = 0f;
            int kicks = 0;
            for (int i = 0; i < 240; i++)
            {
                sim.Step(Forward(), H);
                t += H;
                if (sim.Events.sprintKick) kicks++;
                max = Math.Max(max, sim.HorizontalSpeed);
                if (reached < 0f && sim.HorizontalSpeed >= sim.def.sprintSpeed * 0.97f) reached = t;
            }
            TestContext.Out.WriteLine($"sprint reached in {reached:0.000} s, top {max:0.00} m/s, kicks {kicks}");
            Assert.That(kicks, Is.EqualTo(1));
            Assert.That(reached, Is.GreaterThan(0f).And.LessThanOrEqualTo(0.4f));
            Assert.That(max, Is.LessThanOrEqualTo(sim.def.sprintSpeed + 1e-3f), "never past sprint");
        }

        [Test]
        public void TheKickNeverPushesPastSprintEvenSideways()
        {
            // Drifting sideways at 5 m/s, then sprinting forward: the kick turns you, it doesn't stack.
            var sim = Standing(Flat(), Vector3.Zero, velocity: new Vector3(5.5f, 0f, 0f));
            float max = 0f;
            for (int i = 0; i < 120; i++) { sim.Step(Forward(), H); max = Math.Max(max, sim.HorizontalSpeed); }
            Assert.That(max, Is.LessThanOrEqualTo(sim.def.sprintSpeed + 1e-3f));
        }

        [Test]
        public void TheKickOnlyFiresWellBelowSprintAndHasACooldown()
        {
            var sim = Standing(Flat(), Vector3.Zero, velocity: new Vector3(0f, 0f, 7f));
            sim.Step(Forward(), H);
            Assert.That(sim.Events.sprintKick, Is.False, "7 m/s is not a stumble");

            var k = Standing(Flat(), Vector3.Zero);
            k.Step(Forward(), H);
            Assert.That(k.Events.sprintKick, Is.True, "sprint from a standstill: push off");
            Assert.That(k.KickCooldown, Is.EqualTo(k.def.kickCooldown).Within(0.02f));
            // Stop dead and sprint again at once: no second kick within the cooldown.
            Run(k, new MoveInput(), 0.6f);
            Assert.That(k.HorizontalSpeed, Is.LessThan(0.5f));
            bool again = false;
            for (int i = 0; i < 12; i++) { k.Step(Forward(), H); again |= k.Events.sprintKick; }
            Assert.That(again, Is.False, "on cooldown");
            // After the cooldown it kicks again.
            Run(k, new MoveInput(), k.def.kickCooldown);
            k.Step(Forward(), H);
            Assert.That(k.Events.sprintKick, Is.True, "ready again");
        }

        [Test]
        public void TheKickReadiesTheSlideBoost()
        {
            var sim = Standing(Flat(), Vector3.Zero, velocity: new Vector3(0f, 0f, 9.5f));
            sim.Step(Forward().With(crouchPressed: true), H);
            Assert.That(sim.Events.slideBoosted, Is.True, "first slide boosts");
            Run(sim, Forward().With(crouchHeld: true), 0.3f);
            Run(sim, new MoveInput(), 0.6f); // stood up and stopped: the boost is still cooling down
            Assert.That(sim.SlideBoostCooldown, Is.GreaterThan(0.5f));
            bool kicked = false;
            for (int i = 0; i < 30; i++) { sim.Step(Forward(), H); kicked |= sim.Events.sprintKick; }
            Assert.That(kicked, Is.True);
            Assert.That(sim.SlideBoostCooldown, Is.EqualTo(0f), "the kick readied the boost");
            sim.Step(Forward().With(crouchPressed: true), H);
            Assert.That(sim.Mode, Is.EqualTo(MoveMode.Slide));
            Assert.That(sim.Events.slideBoosted, Is.True, "a slide right after the kick boosts");
        }

        /// <summary>Air strafing with strafe input stays exactly as the creator found it: bunny hopping with the stick
        /// sideways while turning 120 degrees a second gives the same speeds as before the sprint changes (recorded
        /// 2026-10-10), with sprint held or not, once the 2026-10-11 hop costs are switched off. The W-only rule
        /// (airStrafeSideShare) doesn't touch it: a stick pushed sideways earns the full gain.</summary>
        [TestCase(false)]
        [TestCase(true)]
        public void AirStrafeGainIsUnchanged(bool sprintHeld)
        {
            var w = new BoxWorld();
            w.Floor(-500f, 500f, -500f, 500f, 0f);
            var def = new MoveDef { hopSpeedLoss = 0f, slideHopSpeedLoss = 0f, doubleJumpHopTurnLoss = 0f, landingGraceDecay = 0f };
            Assert.That(def.airStrafeSideShare, Is.GreaterThan(0f), "the W-only rule stays on");
            var sim = Standing(w, Vector3.Zero, def, 0f, new Vector3(0f, 0f, 9.5f));
            float yaw = 0f;
            var expected = new[] { (60, 10.6274f), (120, 11.1801f), (240, 12.9537f), (480, 15.3884f) };
            int next = 0;
            for (int i = 1; i <= 480; i++)
            {
                yaw += 120f * H;
                sim.Step(new MoveInput { move = new Vector2(1f, 0f), yaw = yaw, jumpHeld = true, jumpPressed = true, sprintHeld = sprintHeld }, H);
                if (next < expected.Length && i == expected[next].Item1)
                {
                    Assert.That(sim.HorizontalSpeed, Is.EqualTo(expected[next].Item2).Within(2e-3f), "tick " + i);
                    next++;
                }
            }
            Assert.That(sim.HorizontalSpeed, Is.GreaterThan(sim.def.sprintSpeed * 1.5f), "air strafing still gains well past sprint");
        }
    }
}
