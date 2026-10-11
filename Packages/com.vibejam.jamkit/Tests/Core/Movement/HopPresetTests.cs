using System;
using System.Numerics;
using JamKit.Movement;
using NUnit.Framework;
using static JamKit.Tests.Movement.MoveTestKit;

namespace JamKit.Tests.Movement
{
    /// <summary>The hop-cost presets (docs/MOVEMENT.md "Hop-cost presets"): Off (free, before 2026-10-11), Mild (the
    /// default: clean and slide hops free, a late hop costs a little, the hop-and-steer only on sharp turns), Current
    /// (2026-10-11). The table tests print what each costs over ten hops from 20 m/s and on the autopilot's Gauntlet and
    /// dream tool runs, for the creator to set against the feel. The override every sim reads is cleared after each test.</summary>
    public class HopPresetTests
    {
        [TearDown]
        public void ClearOverride() => HopCostPresets.ClearOverride();

        static readonly HopCostPreset[] All = { HopCostPreset.Off, HopCostPreset.Mild, HopCostPreset.Current };

        static MoveDef Def(HopCostPreset p) => HopCostPresets.Apply(new MoveDef(), p);

        /// <summary>Ten hops in a straight line from <paramref name="v0"/>: each flight spends the double jump at the top
        /// (straight on), then the next jump is pressed <paramref name="late"/> seconds after landing (0: on the landing
        /// tick, a clean hop), or crouch and jump are pressed just before landing (<paramref name="slide"/>).</summary>
        static float TenHops(MoveDef def, float v0, float late, bool slide, out float seconds)
        {
            var w = new BoxWorld();
            w.Floor(-20f, 20f, -10f, 3000f, 0f);
            var sim = Standing(w, Vector3.Zero, def, 0f, new Vector3(0f, 0f, v0));
            var fwd = new MoveInput { move = new Vector2(0f, 1f), sprintHeld = true };
            int hops = 0;
            bool dj = false, armed = false;
            float onGround = 0f;
            seconds = 0f;
            float h = 1f / 120f;
            while (hops < 10 && seconds < 30f)
            {
                var input = fwd;
                if (sim.Grounded)
                {
                    onGround += h;
                    if (hops == 0 || onGround >= late - 1e-4f) { input.jumpPressed = input.jumpHeld = true; }
                }
                else
                {
                    onGround = 0f;
                    if (!dj && sim.Velocity.Y < 0f) { input.jumpPressed = input.jumpHeld = true; dj = true; }
                    else if (slide && dj && !armed && sim.Velocity.Y < 0f && sim.Position.Y < 0.35f)
                    {
                        input.crouchPressed = input.crouchHeld = true;
                        input.jumpPressed = input.jumpHeld = true; // buffered onto the landing
                        armed = true;
                    }
                    else if (slide && armed) input.crouchHeld = true;
                }
                sim.Step(input, h);
                seconds += h;
                if (sim.Events.jumped) { hops++; dj = false; armed = false; }
            }
            // Land the last flight and read the speed on the ground.
            for (int i = 0; i < 240 && !sim.Grounded; i++) { sim.Step(fwd, h); seconds += h; }
            return sim.HorizontalSpeed;
        }

        [Test]
        public void TenHopsFrom20MetresASecondUnderEachPreset()
        {
            float clean0 = 0f, late0 = 0f;
            foreach (var p in All)
            {
                float clean = TenHops(Def(p), 20f, 0f, false, out float t);
                float late = TenHops(Def(p), 20f, 0.08f, false, out _);
                float slide = TenHops(Def(p), 20f, 0f, true, out _);
                TestContext.Out.WriteLine($"{p,-8} ten hops from 20 m/s ({t:0.0} s): clean {clean:0.00}, each 0.08 s late {late:0.00}, slide hops {slide:0.00}");
                switch (p)
                {
                    case HopCostPreset.Off:
                        Assert.That(clean, Is.EqualTo(20f).Within(0.05f), "Off: free");
                        clean0 = clean; late0 = late;
                        break;
                    case HopCostPreset.Mild:
                        // Mild (2026-10-12): a chain of clean hops bleeds a little, never keeps the speed for free.
                        Assert.That(clean, Is.InRange(16.5f, clean0 - 1f), "Mild: ten clean hops from 20 m/s lose 1-3.5 m/s");
                        Assert.That(slide, Is.GreaterThan(clean), "slide hops bleed less");
                        Assert.That(late, Is.LessThan(clean - 1f), "a late hop costs");
                        Assert.That(late, Is.LessThan(late0 - 1f), "more than with hops Off");
                        break;
                    case HopCostPreset.Current:
                        Assert.That(clean, Is.LessThan(17f), "Current: ten clean hops bleed several m/s");
                        Assert.That(slide, Is.GreaterThan(clean), "slide hops beat plain ones");
                        break;
                }
            }
        }

        [Test]
        public void TheHopAndSteerCostsOnlySharpTurnsUnderMild()
        {
            float Cost(HopCostPreset p, Vector2 stick)
            {
                var w = new BoxWorld();
                w.Floor(-50f, 50f, -100f, 2000f, 0f);
                var sim = Standing(w, new Vector3(0f, 0.6f, 0f), Def(p), 0f, new Vector3(0f, -2f, 16f));
                var fwd = new MoveInput { move = new Vector2(0f, 1f), sprintHeld = true };
                Run(sim, fwd, 2f, s => s.Grounded);
                var jump = fwd;
                jump.jumpPressed = jump.jumpHeld = true;
                sim.Step(jump, H);
                Run(sim, fwd, 0.15f); // straight on until the double jump
                float before = sim.HorizontalSpeed;
                sim.Step(new MoveInput { move = stick, jumpPressed = true, jumpHeld = true }, H);
                Assert.That(sim.Events.doubleJumped, Is.True);
                return before - sim.HorizontalSpeed;
            }
            var square = new Vector2(1f, 0f);                 // turns the flight 45 degrees
            var back = new Vector2(0.5f, -0.866f);           // 150 degrees off: turns it 75
            foreach (var p in All)
                TestContext.Out.WriteLine($"{p,-8} double jump 0.15 s after a hop at 16 m/s: a 45 degree redirect costs {Cost(p, square):0.00} m/s, a sharper one {Cost(p, back):0.00}");
            Assert.That(Cost(HopCostPreset.Off, back), Is.LessThan(0.02f));
            Assert.That(Cost(HopCostPreset.Mild, square), Is.LessThan(0.02f), "Mild: the usual redirect is free");
            Assert.That(Cost(HopCostPreset.Mild, back), Is.InRange(0.01f, Cost(HopCostPreset.Current, back) * 0.5f), "only a sharp one costs, and less");
        }

        [Test]
        public void TheAutopilotsTimesUnderEachPreset()
        {
            foreach (var p in All)
            {
                HopCostPresets.SetOverride(p);
                var g = GauntletTests.Run(GauntletCourse.Build(), 60);
                var d = DreamToolTests.Run(DreamToolCourse.Build(), 60);
                TestContext.Out.WriteLine($"{p,-8} autopilot: Gauntlet {g.seconds:0.00} s (falls {g.falls}), dream tool {d.seconds:0.00} s (falls {d.falls})");
                Assert.That(g.finished && d.finished, Is.True, p.ToString());
            }
            HopCostPresets.ClearOverride();
        }

        [Test]
        public void TheOverrideWinsOverTheDefAndClearsBack()
        {
            var d = new MoveDef();
            Assert.That(HopCostPresets.For(d).hopSpeedLoss, Is.EqualTo(HopCostPresets.Of(HopCostPreset.Mild).hopSpeedLoss), "the def's defaults are Mild");
            Assert.That(HopCostPresets.Of(HopCostPreset.Mild).hopSpeedLoss, Is.EqualTo(0.02f));
            Assert.That(HopCostPresets.Active, Is.EqualTo(HopCostPreset.Mild));
            HopCostPresets.SetOverride(HopCostPreset.Current);
            Assert.That(HopCostPresets.For(d).hopSpeedLoss, Is.EqualTo(0.08f));
            Assert.That(HopCostPresets.Next(HopCostPreset.Current), Is.EqualTo(HopCostPreset.Off));
            Assert.That(HopCostPresets.TryParse("mild", out var m) && m == HopCostPreset.Mild, Is.True);
            Assert.That(HopCostPresets.TryParse("", out var none), Is.False);
            Assert.That(none, Is.EqualTo(HopCostPreset.Mild), "nothing saved: Mild");
            HopCostPresets.ClearOverride();
            Assert.That(HopCostPresets.For(d).hopSpeedLoss, Is.EqualTo(0.02f));
        }
    }
}
