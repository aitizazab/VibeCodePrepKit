using System;
using System.Numerics;
using JamKit.Movement;
using NUnit.Framework;
using static JamKit.Tests.Movement.MoveTestKit;

namespace JamKit.Tests.Movement
{
    /// <summary>The Source formulas, the ground and air rules, jumps, the slide and the forgiveness timers.</summary>
    public class MoveRulesTests
    {
        // ---------- Formulas ----------

        [Test]
        public void AirStrafingAtTheBestAngleAddsAtMostCapSquaredPerTick()
        {
            var d = new MoveDef();
            float cap = d.airCap;
            var v = new Vector3(0f, 0f, 10f);
            for (int i = 0; i < 240; i++)
            {
                float s = MoveMath.FlatLength(v);
                // The best wish direction: its projection on v is cap - A (A = accel x wishspeed x h, clamped).
                float a = Math.Min(d.airAccel * d.sprintSpeed * H, cap);
                float p = cap - a;
                float ang = (float)Math.Acos(Math.Max(-1f, Math.Min(1f, p / s)));
                var dir = Vector3.Normalize(v);
                var right = MoveMath.RightOf(dir);
                var wish = dir * (float)Math.Cos(ang) + right * (float)Math.Sin(ang);
                var next = MoveMath.AirAccelerate(v, wish, d.sprintSpeed, d.airAccel, cap, H);
                float gain = next.LengthSquared() - v.LengthSquared();
                Assert.That(gain, Is.LessThanOrEqualTo(cap * cap + 1e-3f), "tick " + i);
                Assert.That(gain, Is.GreaterThan(0f), "strafing at the best angle gains speed");
                v = next;
            }
            // Two seconds at 120 Hz: |v| ~ sqrt(v0^2 + N cap^2) at best.
            float bound = (float)Math.Sqrt(100.0 + 240 * cap * cap);
            Assert.That(v.Length(), Is.LessThanOrEqualTo(bound + 0.01f));
            Assert.That(v.Length(), Is.GreaterThan(10.5f), "about +2.9 m/s per second at 10 m/s");
        }

        [Test]
        public void AirStrafeGainDependsOnTheTickRateSoTheSimulationIsFixedStep()
        {
            float Gain(float hz)
            {
                var d = new MoveDef();
                float h = 1f / hz;
                var v = new Vector3(0f, 0f, 10f);
                for (int i = 0; i < (int)hz; i++)
                {
                    float a = Math.Min(d.airAccel * d.sprintSpeed * h, d.airCap);
                    float s = v.Length();
                    float ang = (float)Math.Acos((d.airCap - a) / s);
                    var dir = Vector3.Normalize(v);
                    var wish = dir * (float)Math.Cos(ang) + MoveMath.RightOf(dir) * (float)Math.Sin(ang);
                    v = MoveMath.AirAccelerate(v, wish, d.sprintSpeed, d.airAccel, d.airCap, h);
                }
                return v.Length() - 10f;
            }
            Assert.That(Gain(120f), Is.GreaterThan(Gain(60f) * 1.6f), "per-tick cap: twice the ticks, about twice the gain");
        }

        [Test]
        public void AirStrafingStraightAheadAddsNothingAtSpeed()
        {
            var d = new MoveDef();
            var v = new Vector3(0f, 0f, 12f);
            var next = MoveMath.AirAccelerate(v, new Vector3(0f, 0f, 1f), d.sprintSpeed, d.airAccel, d.airCap, H);
            Assert.That(next, Is.EqualTo(v), "holding forward in the air never slows and never speeds you up past the cap");
        }

        [Test]
        public void FrictionIsProportionalAboveStopSpeedAndStopsBelowIt()
        {
            var v = MoveMath.Friction(new Vector3(10f, 0f, 0f), 6f, 2.5f, 0.01f);
            Assert.That(v.X, Is.EqualTo(10f * (1f - 0.06f)).Within(1e-4f));
            var slow = MoveMath.Friction(new Vector3(0.1f, 0f, 0f), 6f, 2.5f, 0.1f);
            Assert.That(slow.X, Is.EqualTo(0f), "constant drop below stop speed: you really stop");
        }

        [Test]
        public void ClipKeepsTheSpeedAlongAWall()
        {
            var v = MoveMath.Clip(new Vector3(3f, 0f, 10f), new Vector3(-1f, 0f, 0f));
            Assert.That(v, Is.EqualTo(new Vector3(0f, 0f, 10f)));
            var away = MoveMath.Clip(new Vector3(-3f, 0f, 10f), new Vector3(-1f, 0f, 0f));
            Assert.That(away, Is.EqualTo(new Vector3(-3f, 0f, 10f)), "moving away from the surface is untouched");
        }

        // ---------- Ground ----------

        [Test]
        public void InputAloneReachesSprintQuicklyAndNeverPassesIt()
        {
            var sim = Standing(Flat(), Vector3.Zero);
            Assert.That(sim.Mode, Is.EqualTo(MoveMode.Grounded));
            float max = 0f;
            float reached = -1f, t = 0f;
            for (int i = 0; i < 360; i++)
            {
                sim.Step(Forward(), H);
                t += H;
                max = Math.Max(max, sim.HorizontalSpeed);
                if (reached < 0f && sim.HorizontalSpeed > sim.def.sprintSpeed * 0.95f) reached = t;
            }
            Assert.That(max, Is.LessThanOrEqualTo(sim.def.sprintSpeed + 1e-3f));
            Assert.That(reached, Is.InRange(0.01f, 0.4f), "full speed fast (the sprint kick pushes off from a standstill, then 1/accel seconds)");
            Assert.That(sim.Mode, Is.EqualTo(MoveMode.Sprint), "sprint held");
        }

        [Test]
        public void SpeedAboveSprintLingersWhileMovingAndStopsWhenYouLetGo()
        {
            var sim = Standing(Flat(), Vector3.Zero, velocity: new Vector3(0f, 0f, 14f));
            Run(sim, Forward(), 0.5f);
            Assert.That(sim.HorizontalSpeed, Is.EqualTo(9.5f + 4.5f * (float)Math.Exp(-0.75)).Within(0.1f), "the excess over sprint fades at 1.5/s, not by friction");
            var sim2 = Standing(Flat(), Vector3.Zero, velocity: new Vector3(0f, 0f, 14f));
            Run(sim2, new MoveInput(), 0.5f);
            Assert.That(sim2.HorizontalSpeed, Is.LessThan(1.5f), "no input: Source friction stops you");
        }

        [Test]
        public void TheLandingGraceSkipsFrictionButSpeedAboveSprintStillFades()
        {
            var sim = Standing(Flat(), new Vector3(0f, 1f, 0f), velocity: new Vector3(0f, -2f, 14f));
            Run(sim, Forward(), 1f, s => s.Grounded);
            Assert.That(sim.Grounded, Is.True);
            float landed = sim.HorizontalSpeed;
            float t = Run(sim, Forward(), 0.12f);
            float excess = landed - sim.def.sprintSpeed;
            Assert.That(sim.HorizontalSpeed, Is.EqualTo(sim.def.sprintSpeed + excess * (float)Math.Exp(-sim.def.overspeedDecay * sim.def.landingGraceDecay * t)).Within(0.02f),
                "the excess over sprint fades from the landing tick (no frozen grace: a late hop costs its time on the ground)");
            Assert.That(sim.HorizontalSpeed, Is.GreaterThan(landed * (1f - sim.def.friction * t) + 1f), "but no friction");
        }

        // ---------- Jumps ----------

        [Test]
        public void JumpAndDoubleJumpReachTheirHeights()
        {
            var sim = Standing(Flat(), Vector3.Zero);
            Assert.That(sim.def.JumpSpeed, Is.EqualTo(7.746f).Within(0.01f));
            float top = 0f;
            Run(sim, new MoveInput().With(jump: true), 1f, s => { top = Math.Max(top, s.Position.Y); return s.Velocity.Y < -1f; });
            Assert.That(top, Is.EqualTo(1.25f).Within(0.08f), "jump height");
            Assert.That(sim.Events.jumped || true, Is.True);
            // At the apex, the double jump adds about 1 m more.
            var sim2 = Standing(Flat(), Vector3.Zero);
            Run(sim2, new MoveInput().With(jump: true), 1f, s => s.Velocity.Y <= 0f);
            Assert.That(sim2.DoubleJumpAvailable, Is.True);
            float apex = sim2.Position.Y;
            float top2 = 0f;
            Run(sim2, new MoveInput().With(jump: true), 1f, s => { top2 = Math.Max(top2, s.Position.Y); return s.Velocity.Y < -1f; });
            Assert.That(top2 - apex, Is.EqualTo(1.0f).Within(0.08f), "double jump height");
            Assert.That(sim2.DoubleJumpAvailable, Is.False, "one per airtime");
        }

        [Test]
        public void TheDoubleJumpWaitsOutItsLockoutAndRefreshesOnLanding()
        {
            var sim = Standing(Flat(), Vector3.Zero);
            sim.Step(new MoveInput().With(jump: true), H);
            Run(sim, new MoveInput(), 0.05f);
            Run(sim, new MoveInput().With(jump: true), H);
            Assert.That(sim.DoubleJumpAvailable, Is.True, "0.05 s after the jump: locked out");
            // The press stays buffered and fires once the lockout ends.
            Run(sim, new MoveInput(), 0.06f);
            Assert.That(sim.DoubleJumpAvailable, Is.False, "the buffered press double-jumped after the lockout");
            Run(sim, new MoveInput(), 3f, s => s.Grounded);
            Assert.That(sim.DoubleJumpAvailable, Is.True);
        }

        [Test]
        public void CoyoteTimeAllowsAJump011SecondsAfterTheEdgeButNot013()
        {
            bool JumpsAfter(float delay)
            {
                var w = new BoxWorld();
                w.Floor(-10f, 10f, -50f, 0f, 0f);
                var sim = Standing(w, new Vector3(0f, 0f, -3f), velocity: new Vector3(0f, 0f, 9.5f));
                Run(sim, Forward(), 2f, s => s.Mode == MoveMode.Air);
                Assert.That(sim.Mode, Is.EqualTo(MoveMode.Air), "ran off the edge");
                Run(sim, Forward(), delay - H);
                var before = sim.Velocity.Y;
                sim.Step(Forward().With(jump: true), H);
                return sim.Events.jumped;
            }
            Assert.That(JumpsAfter(0.11f), Is.True, "0.11 s after the edge: still a jump");
            Assert.That(JumpsAfter(0.13f), Is.False, "0.13 s: too late (it's a double jump's job now)");
        }

        [Test]
        public void ABufferedJumpFiresOnLanding()
        {
            var sim = Standing(Flat(), new Vector3(0f, 2f, 0f));
            Run(sim, new MoveInput(), 2f, s => s.Position.Y < 0.4f && s.Velocity.Y < 0f);
            sim.Step(new MoveInput().With(jump: true), H); // a double jump would fire here: use it up first
            Run(sim, new MoveInput(), 2f, s => s.Velocity.Y < 0f && s.Position.Y < 0.5f);
            sim.Step(new MoveInput().With(jump: true), H);
            Assert.That(sim.Events.jumped, Is.False, "in the air with no jump left: buffered");
            bool jumped = false;
            Run(sim, new MoveInput(), 0.2f, s => { jumped |= s.Events.jumped; return jumped; });
            Assert.That(jumped, Is.True, "the press 0.1 s before landing jumped on landing");
        }

        // ---------- Slide ----------

        [Test]
        public void SlideBoostHonoursItsCooldownAndCap()
        {
            var sim = Standing(Flat(), Vector3.Zero, velocity: new Vector3(0f, 0f, 9.5f));
            sim.Step(Forward().With(crouchPressed: true), H);
            Assert.That(sim.Mode, Is.EqualTo(MoveMode.Slide));
            Assert.That(sim.Events.slideBoosted, Is.True);
            Assert.That(sim.HorizontalSpeed, Is.EqualTo(9.5f + 3f).Within(0.05f), "+3 m/s");
            Run(sim, Forward(), 0.5f);
            Run(sim, Forward(), 0.6f); // stand and run
            Assert.That(sim.Mode, Is.Not.EqualTo(MoveMode.Slide));
            sim.Step(Forward().With(crouchPressed: true), H);
            Assert.That(sim.Mode, Is.EqualTo(MoveMode.Slide));
            Assert.That(sim.Events.slideBoosted, Is.False, "1.1 s later: still cooling down (2 s)");
            Assert.That(sim.SlideBoostCooldown, Is.GreaterThan(0.5f));

            var fast = Standing(Flat(), Vector3.Zero, velocity: new Vector3(0f, 0f, 12f));
            fast.Step(Forward().With(crouchPressed: true), H);
            Assert.That(fast.HorizontalSpeed, Is.EqualTo(13f).Within(0.05f), "never boosted past the 13 m/s cap");
        }

        [Test]
        public void TheSlideBoostCapDecaysTheLongerYouSlide()
        {
            var sim = Standing(Flat(), Vector3.Zero, velocity: new Vector3(0f, 0f, 9.5f));
            float fresh = sim.SlideBoostCap;
            sim.Step(Forward().With(crouchPressed: true), H);
            Run(sim, Forward().With(crouchHeld: true), 1.2f);
            Assert.That(sim.SlideBoostCap, Is.LessThan(fresh - 1.5f), "1.2 s of sliding lowers the cap");
            Run(sim, Forward(), 3f);
            Assert.That(sim.SlideBoostCap, Is.EqualTo(fresh).Within(1e-3f), "it recovers off the slide");
        }

        [Test]
        public void SlidingDeceleratesGentlyThenEndsAndJumpingKeepsAllHorizontalSpeed()
        {
            var sim = Standing(Flat(), Vector3.Zero, velocity: new Vector3(0f, 0f, 10f));
            sim.Step(Forward().With(crouchPressed: true), H);
            float start = sim.HorizontalSpeed;
            Run(sim, Forward().With(crouchHeld: true), 0.5f);
            Assert.That(sim.HorizontalSpeed, Is.EqualTo(start - 2.5f * 0.5f).Within(0.1f), "2.5 m/s^2 for the first 0.6 s");
            float before = sim.HorizontalSpeed;
            sim.Step(Forward().With(jump: true, crouchHeld: true), H);
            Assert.That(sim.Events.jumped, Is.True);
            Assert.That(sim.HorizontalSpeed, Is.EqualTo(before - sim.def.slideHopSpeedLoss * Math.Max(0f, before - sim.def.sprintSpeed)).Within(0.05f),
                "a slide jump keeps all its speed but 3% of what's above sprint");
            Assert.That(sim.Velocity.Y, Is.EqualTo(sim.def.JumpSpeed * 0.9f).Within(0.1f));

            var slow = Standing(Flat(), Vector3.Zero, velocity: new Vector3(0f, 0f, 10f));
            slow.Step(Forward().With(crouchPressed: true), H);
            Run(slow, Forward().With(crouchHeld: true), 4f, s => s.Mode != MoveMode.Slide);
            Assert.That(slow.Mode, Is.Not.EqualTo(MoveMode.Slide), "the slide ends below 4 m/s");
            Assert.That(slow.Crouched, Is.True, "still crouched while crouch is held");
        }

        [Test]
        public void SlidingDownASlopeGainsSpeed()
        {
            var w = new BoxWorld();
            w.Floor(-10f, 10f, -40f, 0f, 0f);
            w.Ramp(-10f, 10f, 0f, 20f, 0f, -4f, -10f);
            w.Floor(-10f, 10f, 20f, 60f, -4f);
            var sim = Standing(w, new Vector3(0f, 0f, 1f), velocity: new Vector3(0f, 0f, 9f));
            Run(sim, Forward(), 0.05f);
            sim.Step(Forward().With(crouchPressed: true), H);
            float start = sim.HorizontalSpeed;
            Run(sim, Forward().With(crouchHeld: true), 3f, s => s.Position.Z > 18f);
            Assert.That(sim.Mode, Is.EqualTo(MoveMode.Slide), "stayed on the slope sliding");
            Assert.That(sim.HorizontalSpeed, Is.GreaterThan(start + 1f), "the slope beats the slide's drag");
        }

        [Test]
        public void ASlideHopLandsStraightIntoASlideWithoutFriction()
        {
            var sim = Standing(Flat(), new Vector3(0f, 1.2f, 0f), velocity: new Vector3(0f, -1f, 11f));
            Run(sim, Forward(), 1f, s => s.Position.Y < 0.5f);
            sim.Step(Forward().With(crouchPressed: true), H); // crouch tapped just before landing
            Run(sim, Forward().With(crouchHeld: true), 1f, s => s.Grounded);
            Assert.That(sim.Mode, Is.EqualTo(MoveMode.Slide), "landed into a slide");
            Assert.That(sim.HorizontalSpeed, Is.GreaterThan(11f), "no landing friction, and the boost if ready");
        }

        [Test]
        public void SlidingFitsUnderALowBarAndStandingDoesNotStandUpInsideIt()
        {
            var w = Flat();
            w.Add(new Vector3(0f, 1.15f + 0.5f, 10f), new Vector3(20f, 1f, 1f)); // underside at 1.15 m
            var sim = Standing(w, Vector3.Zero, velocity: new Vector3(0f, 0f, 10f));
            sim.Step(Forward().With(crouchPressed: true), H);
            Run(sim, Forward().With(crouchHeld: true), 2f, s => s.Position.Z > 10.6f);
            Assert.That(sim.Position.Z, Is.GreaterThan(10.6f), "slid under the bar");
            Assert.That(sim.Height, Is.EqualTo(sim.def.slideHeight));

            var walker = Standing(w, Vector3.Zero, velocity: new Vector3(0f, 0f, 10f));
            Run(walker, Forward(), 1.5f);
            Assert.That(walker.Position.Z, Is.LessThan(10f), "a standing body is stopped by the bar");
        }

        // ---------- Mantle ----------

        [Test]
        public void RunningIntoALedgeUpTo19MetresHighMantlesOntoIt()
        {
            foreach (float height in new[] { 0.8f, 1.5f, 1.85f })
            {
                var w = Flat();
                w.Floor(-10f, 10f, 5f, 30f, height, height + 1f);
                var sim = Standing(w, Vector3.Zero, velocity: new Vector3(0f, 0f, 8f));
                bool mantled = false;
                Run(sim, Forward(), 2f, s => { mantled |= s.Events.mantleStarted; return s.Position.Z > 7f && s.Grounded; });
                Assert.That(mantled, Is.True, height + " m");
                Assert.That(sim.Position.Y, Is.EqualTo(height).Within(0.06f), height + " m: on top");
                Assert.That(sim.HorizontalSpeed, Is.GreaterThanOrEqualTo(5f), "leaves the mantle moving");
            }
            var high = Flat();
            high.Floor(-10f, 10f, 5f, 30f, 2.5f, 3.5f);
            var s2 = Standing(high, Vector3.Zero, velocity: new Vector3(0f, 0f, 8f));
            bool m2 = false;
            Run(s2, Forward(), 1.5f, s => { m2 |= s.Events.mantleStarted; return false; });
            Assert.That(m2, Is.False, "2.5 m is out of reach");
        }

        [Test]
        public void AJumpThatFallsShortOfALedgeIsRescuedByAMantle()
        {
            var w = new BoxWorld();
            w.Floor(-10f, 10f, -50f, 0f, 0f);
            w.Floor(-10f, 10f, 7.5f, 40f, 0f, 20f); // 7.5 m away: a single jump at 9.5 m/s covers about 6; the far side, its face going down into the pit
            var sim = Standing(w, new Vector3(0f, 0f, -0.5f), velocity: new Vector3(0f, 0f, 9.5f));
            sim.Step(Forward().With(jump: true), H);
            bool mantled = false;
            Run(sim, Forward(), 3f, s => { mantled |= s.Events.mantleStarted; return s.Grounded && s.Position.Z > 7.5f; });
            Assert.That(mantled, Is.True, "the mantle caught the ledge");
            Assert.That(sim.Position.Z, Is.GreaterThan(7.5f), "made it across");
            Assert.That(sim.Position.Y, Is.EqualTo(0f).Within(0.06f));
            Assert.That(sim.HorizontalSpeed, Is.GreaterThan(9.5f * 0.8f - 0.3f), "the mantle keeps 80 % of the run's speed (not of the speed left after hitting the face)");
        }
    }
}
