using System;
using System.Numerics;
using JamKit.Movement;
using NUnit.Framework;
using static JamKit.Tests.Movement.MoveTestKit;

namespace JamKit.Tests.Movement
{
    /// <summary>The 2026-10-11 air and hop changes (docs/MOVEMENT.md, "Air strafing and bunny hops"). Air: above 5 m/s, speed
    /// gained in the air needs strafe input, so W alone with the mouse turning only turns the flight; strafing with A/D or
    /// a stick pushed sideways is exactly as before, at a pad's turn rates too. Hops: a ground jump loses 8% of the speed
    /// above sprint (a slide hop 3%), a late jump also pays its time on the ground, and a double jump right after a ground
    /// jump pays for its turn. The table test prints keyboard-and-mouse against a pad, before and after.</summary>
    public class AirAndHopTests
    {
        // ---------- Air ----------

        /// <summary>The 2026-10-11 costs (the Current preset; the default is now Mild, see HopPresetTests).</summary>
        static MoveDef Current() => HopCostPresets.Apply(new MoveDef(), HopCostPreset.Current);

        static MoveDef NoTurnCost() { var d = Current(); d.doubleJumpHopTurnLoss = 0f; return d; }

        static MoveDef Before() => new MoveDef { airStrafeSideShare = 0f, hopSpeedLoss = 0f, slideHopSpeedLoss = 0f, doubleJumpHopTurnLoss = 0f, landingGraceDecay = 0f };

        /// <summary>High in an empty sky: nothing to land on for the length of a test.</summary>
        static MoveSim InTheSky(MoveDef def, float speed)
        {
            var w = new BoxWorld();
            w.Floor(-5000f, 5000f, -5000f, 5000f, -2000f);
            return Standing(w, Vector3.Zero, def, 0f, new Vector3(0f, 0f, speed));
        }

        static float Heading(MoveSim s) => (float)(Math.Atan2(s.Velocity.X, s.Velocity.Z) * 180.0 / Math.PI);

        /// <summary>The view turns right at <paramref name="degPerSec"/> from along the flight; the move input stays
        /// <paramref name="move"/> (x right, y forward). Returns the horizontal speed after <paramref name="seconds"/>.</summary>
        static float TurnRun(MoveDef def, float v0, Vector2 move, float degPerSec, float seconds, out float heading)
        {
            var sim = InTheSky(def, v0);
            float yaw = 0f;
            for (int i = 0; i < (int)(seconds / H + 0.5f); i++)
            {
                yaw += degPerSec * H;
                sim.Step(new MoveInput { move = move, yaw = yaw }, H);
            }
            heading = Heading(sim);
            return sim.HorizontalSpeed;
        }

        /// <summary>A straight course along +z: the view swings +-<paramref name="amp"/> degrees at <paramref name="degPerSec"/>,
        /// strafing toward the turn (D turning right, A turning left). Returns the mean speed along the course.</summary>
        static float Zigzag(MoveDef def, float v0, float amp, float degPerSec, float seconds, out float end)
        {
            var sim = InTheSky(def, v0);
            float yaw = 0f, dir = 1f, z0 = sim.Position.Z;
            int n = (int)(seconds / H + 0.5f);
            for (int i = 0; i < n; i++)
            {
                yaw += dir * degPerSec * H;
                if (yaw > amp) { yaw = amp; dir = -1f; }
                if (yaw < -amp) { yaw = -amp; dir = 1f; }
                sim.Step(new MoveInput { move = new Vector2(dir, 0f), yaw = yaw }, H);
            }
            end = sim.HorizontalSpeed;
            return (sim.Position.Z - z0) / (n * H);
        }

        [Test]
        public void ForwardAloneTurnsTheFlightButAddsNoSpeedAtAnyMouseRate()
        {
            var now = new MoveDef();
            foreach (float rate in new[] { 45f, 90f, 180f, 270f, 360f, 540f, 720f, 1080f })
            {
                float after = TurnRun(now, 13f, new Vector2(0f, 1f), rate, 1f, out float heading);
                float before = TurnRun(Before(), 13f, new Vector2(0f, 1f), rate, 1f, out _);
                TestContext.Out.WriteLine($"W only, mouse {rate,5:0} deg/s: {before:0.00} -> {after:0.00} m/s, flight turned {heading:0} deg");
                Assert.That(after, Is.LessThanOrEqualTo(13f + 1e-3f), $"W only at {rate} deg/s gains nothing");
            }
            Assert.That(TurnRun(Before(), 13f, new Vector2(0f, 1f), 360f, 1f, out _), Is.GreaterThan(15f), "it used to gain well over 1 m/s a second");
            TurnRun(now, 13f, new Vector2(0f, 1f), 180f, 1f, out float turned);
            Assert.That(Math.Abs(turned), Is.GreaterThan(45f), "the flight still follows the view (redirect, no gain)");
        }

        [Test]
        public void SlowJumpsStillAimAsBefore()
        {
            foreach (var move in new[] { new Vector2(0f, 1f), new Vector2(0.7f, 0.7f), new Vector2(1f, 0f) })
            {
                float before = TurnRun(Before(), 1f, move, 0f, 0.6f, out float hb);
                float after = TurnRun(new MoveDef(), 1f, move, 0f, 0.6f, out float ha);
                Assert.That(after, Is.EqualTo(before).Within(0.05f), $"below 5 m/s the air control works as it did ({move})");
                Assert.That(ha, Is.EqualTo(hb).Within(1f));
            }
        }

        [Test]
        public void StrafingWithStrafeInputIsExactlyAsBeforeAtPadAndMouseRates()
        {
            foreach (float rate in new[] { 120f, 190f, 304f, 360f, 540f })
                foreach (var move in new[] { new Vector2(1f, 0f), new Vector2(0.71f, 0.71f), new Vector2(0.6f, 0.8f) })
                {
                    float before = TurnRun(Before(), 9.5f, move, rate, 1f, out _);
                    float after = TurnRun(new MoveDef(), 9.5f, move, rate, 1f, out _);
                    Assert.That(after, Is.EqualTo(before).Within(1e-4f), $"strafe input {move} at {rate} deg/s");
                }
            // A stick nudged only a little sideways earns part of the gain.
            float part = TurnRun(new MoveDef(), 9.5f, new Vector2(0.2f, 0.98f), 360f, 1f, out _);
            Assert.That(part, Is.InRange(9.5f, TurnRun(Before(), 9.5f, new Vector2(0.2f, 0.98f), 360f, 1f, out _)));
        }

        /// <summary>The numbers for the creator: the best a player gets at each look rate (mouse: unbounded; a pad: 190
        /// deg/s, 304 boosted), before and after. Strategies: W only with a steady turn; A/D (or the stick sideways) with
        /// a steady turn; and a straight course, swinging the view and switching sides.</summary>
        [Test]
        public void MouseAgainstPadBeforeAndAfter()
        {
            var after = new MoveDef { hopSpeedLoss = 0f, slideHopSpeedLoss = 0f, doubleJumpHopTurnLoss = 0f, landingGraceDecay = 0f };
            foreach (var (name, limit) in new[] { ("mouse", 1500f), ("pad 304", 304f), ("pad 190", 190f) })
            {
                float wB = 0f, wA = 0f, dB = 0f, dA = 0f, zB = 0f, zA = 0f, zeB = 0f, zeA = 0f;
                for (float r = 10f; r <= limit + 1e-3f; r += 10f)
                {
                    wB = Math.Max(wB, TurnRun(Before(), 9.5f, new Vector2(0f, 1f), r, 1f, out _));
                    wA = Math.Max(wA, TurnRun(after, 9.5f, new Vector2(0f, 1f), r, 1f, out _));
                    dB = Math.Max(dB, TurnRun(Before(), 13f, new Vector2(1f, 0f), r, 2f, out _));
                    dA = Math.Max(dA, TurnRun(after, 13f, new Vector2(1f, 0f), r, 2f, out _));
                }
                foreach (float amp in new[] { 10f, 20f, 30f, 45f, 60f })
                    for (float r = 40f; r <= limit + 1e-3f; r += 40f)
                    {
                        float b = Zigzag(Before(), 13f, amp, r, 3f, out float eb);
                        float a = Zigzag(after, 13f, amp, r, 3f, out float ea);
                        if (b > zB) { zB = b; zeB = eb; }
                        if (a > zA) { zA = a; zeA = ea; }
                    }
                TestContext.Out.WriteLine($"{name,-8} W only 1 s from 9.5: {wB:0.00} -> {wA:0.00} | A/D or stick sideways 2 s from 13: {dB:0.00} -> {dA:0.00} | straight course 3 s from 13: mean {zB:0.00} -> {zA:0.00} (end {zeB:0.00} -> {zeA:0.00})");
                Assert.That(wA, Is.LessThanOrEqualTo(9.5f + 1e-3f), name + ": W only gains nothing");
                Assert.That(dA, Is.EqualTo(dB).Within(1e-3f), name + ": strafing with strafe input is unchanged");
                Assert.That(zA, Is.EqualTo(zB).Within(1e-3f), name + ": the straight-course strafe is unchanged");
            }
        }

        // ---------- Hops ----------

        /// <summary>Flat ground, falling onto it at <paramref name="speed"/> along +z.</summary>
        static MoveSim Falling(MoveDef def, float speed)
        {
            var w = new BoxWorld();
            w.Floor(-50f, 50f, -100f, 2000f, 0f);
            return Standing(w, new Vector3(0f, 0.6f, 0f), def, 0f, new Vector3(0f, -2f, speed));
        }

        static readonly MoveInput Fwd = new MoveInput { move = new Vector2(0f, 1f), sprintHeld = true };

        /// <summary>Lands at <paramref name="speed"/>, jumps <paramref name="lateSeconds"/> after landing (0: pressed just
        /// before, so it fires on the landing tick) and returns the speed after the jump.</summary>
        static float HopAt(MoveDef def, float speed, float lateSeconds, bool slide = false)
        {
            var sim = Falling(def, speed);
            var jump = Fwd;
            jump.jumpPressed = jump.jumpHeld = true;
            // Spend the double jump first (a jump pressed in the air fires it), as a hopper does: then the press
            // before landing is buffered into the ground jump.
            sim.Step(jump, H);
            Assert.That(sim.Events.doubleJumped, Is.True);
            if (lateSeconds <= 0f)
            {
                var press = jump;
                if (slide) { press.crouchPressed = true; press.crouchHeld = true; }
                var hold = Fwd;
                hold.crouchHeld = slide;
                Run(sim, hold, 2f, s => s.Velocity.Y < 0f && s.Position.Y < 0.3f);
                Assert.That(sim.Grounded, Is.False, "still in the air");
                sim.Step(press, H); // pressed just before landing: buffered
                Run(sim, hold, 0.3f, s => s.Events.jumped);
                Assert.That(sim.Events.jumped, Is.True, "the buffered jump fired on landing");
            }
            else
            {
                Run(sim, Fwd, 2f, s => s.Grounded);
                Run(sim, Fwd, lateSeconds);
                sim.Step(jump, H);
                Assert.That(sim.Events.jumped, Is.True);
            }
            return sim.HorizontalSpeed;
        }

        [Test]
        public void AHopCostsALittleLikeASlideAndJumpsAtSprintAreFree()
        {
            var def = Current();
            float atSprint = HopAt(def, def.sprintSpeed, 0f);
            Assert.That(atSprint, Is.EqualTo(def.sprintSpeed).Within(0.02f), "a jump at running speed costs nothing");
            foreach (float speed in new[] { 13f, 16f, 20f })
            {
                float clean = HopAt(def, speed, 0f);
                float free = HopAt(Before(), speed, 0f);
                // A 0.4 s slide at the same speed, for scale.
                var slider = Standing(Flat(), Vector3.Zero, def, 0f, new Vector3(0f, 0f, speed));
                slider.Step(Fwd.With(crouchPressed: true), H);
                Run(slider, Fwd.With(crouchHeld: true), 0.4f);
                float slideBurn = speed + (slider.Events.slideBoosted ? def.slideBoost : 0f) - slider.HorizontalSpeed;
                TestContext.Out.WriteLine($"{speed:0} m/s: a clean hop costs {free - clean:0.00} m/s (before: {speed - free:0.00}); a 0.4 s slide burns {slideBurn:0.00}");
                Assert.That(free, Is.EqualTo(speed).Within(0.02f), "hops used to be free");
                Assert.That(free - clean, Is.EqualTo(def.hopSpeedLoss * (speed - def.sprintSpeed)).Within(0.03f), "8% of the speed above sprint");
                Assert.That(free - clean, Is.InRange(0.25f, 1.2f), "comparable to a short slide at speed (about 1 m/s)");
            }
        }

        [Test]
        public void ALateHopCostsMoreThanACleanOne()
        {
            var def = Current();
            float clean = HopAt(def, 18f, 0f);
            float late = HopAt(def, 18f, 0.08f);
            float later = HopAt(def, 18f, 0.25f);
            TestContext.Out.WriteLine($"18 m/s: clean {clean:0.00}, 0.08 s late {late:0.00}, 0.25 s late {later:0.00}; before the change a 0.08 s late hop kept {HopAt(Before(), 18f, 0.08f):0.00}");
            Assert.That(clean - late, Is.GreaterThan(0.6f), "a late jump pays its time on the ground (the grace no longer freezes speed)");
            Assert.That(clean - later, Is.GreaterThan(clean - late + 1f), "the later, the dearer");
            Assert.That(HopAt(Before(), 18f, 0.08f), Is.EqualTo(18f).Within(0.02f), "it used to be free inside the grace");
        }

        [Test]
        public void ASlideHopBeatsAPlainHop()
        {
            var def = Current();
            foreach (float speed in new[] { 12f, 16f, 20f })
            {
                float plain = HopAt(def, speed, 0f);
                float slide = HopAt(def, speed, 0f, slide: true);
                TestContext.Out.WriteLine($"{speed:0} m/s: plain hop {plain:0.00}, slide hop {slide:0.00}");
                Assert.That(slide, Is.GreaterThan(plain + 0.1f), "the slide is the better speed tool");
            }
        }

        [Test]
        public void ADoubleJumpRightAfterAGroundJumpPaysForItsTurn()
        {
            var def = Current();
            float After(float wait, Vector2 stick, MoveDef d)
            {
                var sim = Falling(d, 16f);
                var jump = Fwd;
                jump.jumpPressed = jump.jumpHeld = true;
                Run(sim, Fwd, 1f, s => s.Grounded);
                sim.Step(jump, H);
                float off = sim.HorizontalSpeed;
                Run(sim, new MoveInput { move = stick }, wait);
                var dj = new MoveInput { move = stick, jumpPressed = true, jumpHeld = true };
                float before = sim.HorizontalSpeed;
                sim.Step(dj, H);
                Assert.That(sim.Events.doubleJumped, Is.True);
                return before - sim.HorizontalSpeed;
            }
            var side = new Vector2(1f, 0f);
            float soon = After(0.15f, side, def);
            float late = After(0.4f, side, def);
            float straight = After(0.15f, new Vector2(0f, 1f), def);
            TestContext.Out.WriteLine($"16 m/s: a 45 degree double-jump redirect 0.15 s after the hop costs {soon:0.00} m/s; at 0.4 s {late:0.00}; straight on {straight:0.00}");
            Assert.That(soon, Is.InRange(0.15f, 0.6f), "about 5% of the speed above sprint for the 45 degree redirect");
            Assert.That(late, Is.LessThan(0.02f), "later in the flight it's free");
            Assert.That(straight, Is.LessThan(0.02f), "no turn, no cost");
            Assert.That(After(0.15f, side, NoTurnCost()), Is.LessThan(0.02f), "0 switches it off");
        }
    }
}
