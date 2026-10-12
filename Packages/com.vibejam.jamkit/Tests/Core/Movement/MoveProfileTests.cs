using System;
using System.Numerics;
using JamKit.Movement;
using NUnit.Framework;
using static JamKit.Tests.Movement.MoveTestKit;

namespace JamKit.Tests.Movement
{
    /// <summary>The movement profiles (docs/MOVEMENT.md "Profiles: dream and daytime"): the dream (no profile) allows
    /// everything; the daytime profile has no double jump, no wallrun, no slide (crouch only crouches), an air strafe
    /// well down, and jump fatigue (after three quick jumps the next ones get lower, standing a moment resets it) when
    /// its flag is on.</summary>
    public class MoveProfileTests
    {
        static MoveSim Sim(BoxWorld w, Vector3 at, MoveProfile profile, Vector3 velocity = default)
        {
            var sim = Standing(w, at, null, 0f, velocity);
            sim.SetProfile(profile);
            return sim;
        }

        [Test]
        public void TheDaytimeProfileIsGroundedAndTheDreamAllowsEverything()
        {
            var day = MoveProfile.Daytime();
            Assert.That(day.doubleJump || day.wallrun || day.slide, Is.False);
            Assert.That(day.jumpFatigue, Is.True, "on (the creator, 2026-10-12)");
            Assert.That(day.jumpFatigueFreeJumps, Is.EqualTo(3), "kicking in after 3 jumps");
            Assert.That(day.airStrafeScale, Is.InRange(0.1f, 0.5f));
            var dream = MoveProfile.Dream();
            Assert.That(dream.doubleJump && dream.wallrun && dream.slide, Is.True);
            Assert.That(dream.airStrafeScale, Is.EqualTo(1f));
            Assert.That(dream.jumpFatigue, Is.False);
        }

        [Test]
        public void NoDoubleJumpInDaytime()
        {
            foreach (var profile in new[] { null, MoveProfile.Daytime() })
            {
                var sim = Sim(Flat(), Vector3.Zero, profile);
                sim.Step(Forward().With(jump: true), H);
                Run(sim, Forward(), 0.3f);
                sim.Step(Forward().With(jump: true), H);
                bool doubled = sim.Events.doubleJumped;
                Run(sim, Forward(), 0.1f, s => { doubled |= s.Events.doubleJumped; return false; });
                Assert.That(doubled, Is.EqualTo(profile == null), profile == null ? "the dream double jumps" : "daytime doesn't");
            }
        }

        [Test]
        public void NoWallrunInDaytime()
        {
            foreach (var profile in new[] { null, MoveProfile.Daytime() })
            {
                var w = Flat(-50f, 200f);
                w.Add(new Vector3(2.4f, 2f, 20f), new Vector3(1f, 4f, 20f)); // a runnable wall on the right, 14 m on
                var sim = Sim(w, new Vector3(1.2f, 0f, 0f), profile, new Vector3(0f, 0f, 9f));
                Run(sim, Forward(), 0.6f);
                sim.Step(Forward().With(jump: true), H);
                bool ran = false;
                var hug = new MoveInput { move = Vector2.Normalize(new Vector2(0.3f, 1f)), sprintHeld = true };
                Run(sim, hug, 1.2f, s => { ran |= s.Mode == MoveMode.Wallrun; return false; });
                Assert.That(ran, Is.EqualTo(profile == null), profile == null ? "the dream wallruns" : "daytime doesn't");
            }
        }

        [Test]
        public void CrouchingAtSpeedOnlyCrouchesInDaytime()
        {
            var day = Sim(Flat(), Vector3.Zero, MoveProfile.Daytime(), new Vector3(0f, 0f, 9.5f));
            day.Step(Forward().With(crouchPressed: true), H);
            Run(day, Forward().With(crouchHeld: true), 0.3f);
            Assert.That(day.Mode, Is.Not.EqualTo(MoveMode.Slide));
            Assert.That(day.Crouched, Is.True, "a crouch, not a slide");
            Assert.That(day.HorizontalSpeed, Is.LessThan(day.def.crouchSpeed + 0.5f), "no slide boost: down to crouch speed");
            var dream = Sim(Flat(), Vector3.Zero, null, new Vector3(0f, 0f, 9.5f));
            dream.Step(Forward().With(crouchPressed: true), H);
            Assert.That(dream.Mode, Is.EqualTo(MoveMode.Slide));
        }

        [Test]
        public void TheDaytimeAirStrafeIsWellDown()
        {
            float Gain(MoveProfile p)
            {
                var w = new BoxWorld();
                w.Floor(-500f, 500f, -500f, 500f, -200f);
                var sim = Standing(w, Vector3.Zero, null, 0f, new Vector3(0f, 0f, 9.5f));
                sim.SetProfile(p);
                float best = 0f;
                for (float rate = 60f; rate <= 600f; rate += 60f)
                {
                    var s = Standing(w, Vector3.Zero, null, 0f, new Vector3(0f, 0f, 9.5f));
                    s.SetProfile(p);
                    float yaw = 0f;
                    for (int i = 0; i < 120; i++) { yaw += rate * H; s.Step(new MoveInput { move = new Vector2(1f, 0f), yaw = yaw }, H); }
                    best = Math.Max(best, s.HorizontalSpeed - 9.5f);
                }
                return best;
            }
            float dream = Gain(null), day = Gain(MoveProfile.Daytime());
            TestContext.Out.WriteLine($"best strafe gain in 1 s from 9.5 m/s: dream {dream:0.00} m/s, daytime {day:0.00} m/s");
            Assert.That(dream, Is.GreaterThan(2f));
            Assert.That(day, Is.LessThan(dream * 0.2f), "daytime keeps under a fifth of the dream's gain");
        }

        static float Apex(MoveSim sim)
        {
            float y0 = sim.Position.Y, top = y0;
            Run(sim, new MoveInput(), 2f, s => { top = Math.Max(top, s.Position.Y); return s.Grounded && s.Velocity.Y <= 0f && s.Position.Y < top - 0.05f; });
            return top - y0;
        }

        [Test]
        public void JumpFatigueSparesThreeQuickJumpsLowersTheFourthAndRestingResetsIt()
        {
            // Seven hops, each straight after landing from the last, except the seventh: after `rest` s standing.
            float[] Hops(MoveProfile p, float rest)
            {
                var sim = Sim(Flat(), Vector3.Zero, p);
                var h = new float[7];
                for (int i = 0; i < h.Length; i++)
                {
                    if (i == h.Length - 1) Run(sim, new MoveInput(), rest);
                    sim.Step(new MoveInput().With(jump: true), H);
                    h[i] = Apex(sim);
                }
                return h;
            }
            var day = Hops(MoveProfile.Daytime(), 1.5f);
            TestContext.Out.WriteLine("daytime hops: " + string.Join(", ", Array.ConvertAll(day, x => x.ToString("0.00"))) + " m (the last after 1.5 s standing)");
            Assert.That(day[0], Is.EqualTo(1.25f).Within(0.08f), "the first is a full jump");
            Assert.That(day[1], Is.EqualTo(day[0]).Within(0.05f), "the second, straight after landing, is still full");
            Assert.That(day[2], Is.EqualTo(day[0]).Within(0.05f), "and the third");
            Assert.That(day[3], Is.LessThan(day[0] - 0.2f), "the fourth quick jump is the first lower one");
            Assert.That(day[3], Is.EqualTo(day[0] * 0.75f).Within(0.08f), "by a quarter of the height");
            Assert.That(day[4], Is.LessThan(day[3] - 0.1f), "then lower again");
            Assert.That(day[5], Is.GreaterThanOrEqualTo(day[0] * MoveProfile.Daytime().jumpFatigueMin - 0.08f), "never below the minimum");
            Assert.That(day[6], Is.EqualTo(day[0]).Within(0.08f), "standing a moment resets it");

            var noFatigue = MoveProfile.Daytime();
            noFatigue.jumpFatigue = false;
            var flat = Hops(noFatigue, 0f);
            Assert.That(flat[5], Is.EqualTo(flat[0]).Within(0.05f), "with the flag off every jump is full");
            var dream = Hops(null, 0f);
            for (int i = 1; i < dream.Length; i++) Assert.That(dream[i], Is.EqualTo(dream[0]).Within(0.05f), "the dream has no fatigue (hop " + (i + 1) + ")");
        }

        [Test]
        public void TheFatigueChainCountsFreeJumpsFirst()
        {
            var day = MoveProfile.Daytime();
            Assert.That(day.FatigueHeight(0), Is.EqualTo(1f));
            Assert.That(day.FatigueHeight(2), Is.EqualTo(1f), "the third jump of a chain");
            Assert.That(day.FatigueHeight(3), Is.EqualTo(0.75f).Within(1e-5f), "the fourth");
            Assert.That(day.FatigueHeight(4), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(day.FatigueHeight(9), Is.EqualTo(day.jumpFatigueMin));
            day.jumpFatigueFreeJumps = 1;
            Assert.That(day.FatigueHeight(1), Is.EqualTo(0.75f).Within(1e-5f), "1 free jump is the old rule (the second is lower)");
            Assert.That(MoveProfile.Dream().FatigueHeight(5), Is.EqualTo(1f));
        }
    }
}
