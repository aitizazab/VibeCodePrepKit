using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;

namespace JamKit.Argument.Tests
{
    /// <summary>
    /// The pressure phases are fair (docs/ARGUMENT-PROTOTYPE.md, "Fair attacks"): every attack pattern, at every
    /// difficulty, heat and anger, is survived untouched by a player that only reacts to what it has seen
    /// (<see cref="PlanningDodger"/>: it notices each attack a quarter of a second late, searches every spot it can
    /// reach for a way out, and between attacks waits in an awkward home spot: a corner, an edge, the middle). And no
    /// attack appears inside the box without a warning first (the standing rule, <see cref="PressureRule"/>).
    /// The Long Night is the only exception (its own tests: technically winnable by a perfect dodger).
    /// </summary>
    public class PressureFairnessTests
    {
        const float Dt = 1f / 60f;

        static readonly PressureShape[][] Patterns =
        {
            new[] { PressureShape.Wall },
            new[] { PressureShape.Bloom },
            new[] { PressureShape.Arc },
            new[] { PressureShape.Wall, PressureShape.Bloom, PressureShape.Arc },
        };

        static string Name(PressureShape[] p) => string.Join("+", p);

        /// <summary>Corners, edge middles and the middle (inset by the light's radius).</summary>
        static readonly Vector2[] Homes =
        {
            new Vector2(0f, 0f),
            new Vector2(-2.85f, 1.45f), new Vector2(2.85f, 1.45f), new Vector2(-2.85f, -1.45f), new Vector2(2.85f, -1.45f),
            new Vector2(0f, 1.45f), new Vector2(0f, -1.45f), new Vector2(-2.85f, 0f), new Vector2(2.85f, 0f),
        };

        // The test player: never laxer than the in-game check (a finer grid, 0.9 of her speed where the check allows
        // 0.8, a margin no bigger than the check's), and late to notice like it.
        static DodgePlanner Planner() => new DodgePlanner(PressureRule.TestCell, PressureRule.TestStep, PressureRule.PlannerHorizon, PressurePhase.LightSpeed * 0.9f, 0f);

        /// <summary>Every pattern on Normal from calm to furious (heat 3 with anger 3 is also the "painful" phase,
        /// the hardest), and every pattern at its hardest in Story (longer warnings; nothing hurts there, but a touch
        /// still counts here).</summary>
        public static IEnumerable<TestCaseData> Cases()
        {
            var settings = new[] { (0, 0), (3, 0), (3, 1), (3, 3) };
            for (int p = 0; p < Patterns.Length; p++)
                foreach (var (level, anger) in settings)
                    yield return new TestCaseData(ArgumentDifficulty.Normal, p, level, anger)
                        .SetName("Survivable_Normal_" + Name(Patterns[p]) + "_heat" + level + "_anger" + anger);
            for (int p = 0; p < Patterns.Length; p++)
                yield return new TestCaseData(ArgumentDifficulty.Story, p, 3, 3)
                    .SetName("Survivable_Story_" + Name(Patterns[p]) + "_heat3_anger3");
        }

        [TestCaseSource(nameof(Cases))]
        public void EveryAttackCanBeEscapedFromAnywhere(ArgumentDifficulty difficulty, int pattern, int level, int anger)
        {
            var planner = Planner();
            var failures = new List<string>();
            int phases = 0, skipped = 0, spawned = 0;
            // A different seed for every home: nine phases per case, each starting (and waiting) somewhere else.
            for (ulong seed = 1; seed <= 1; seed++)
                foreach (var home in Homes)
                {
                    var spec = PressureSpec.For(level, anger, difficulty, 1f, level == 3 && anger == 3, Patterns[pattern]);
                    var phase = new PressurePhase(spec, seed * 7919UL + (ulong)(home.X * 10f + home.Y * 100f + 1000f), null);
                    phase.PlaceLight(home);
                    var bot = new PlanningDodger(planner, 0f, home);
                    while (!phase.Finished) phase.Tick(Dt, bot.Steer(phase, Dt));
                    phases++;
                    skipped += phase.Skipped;
                    spawned += phase.Spawned;
                    Assert.That(phase.Refused, Is.Zero, "the rule refused a spawn");
                    if (phase.Touches > 0)
                        failures.Add("home " + home + ", seed " + seed + ": touched " + phase.Touches + " time(s), " + bot.Stuck + " stuck plan(s)");
                }
            Assert.That(failures, Is.Empty, "unfair: " + string.Join("; ", failures));
            Assert.That(spawned, Is.GreaterThan(phases * 2), "attacks still come (" + skipped + " dropped as unfair of " + (spawned + skipped) + ")");
        }

        [Test]
        public void NoAttackAppearsInsideTheBoxWithoutAWarning([Values(ArgumentDifficulty.Normal, ArgumentDifficulty.Story)] ArgumentDifficulty difficulty)
        {
            var planner = Planner();
            foreach (var pattern in Patterns)
                for (ulong seed = 1; seed <= 4; seed++)
                {
                    var spec = PressureSpec.For(3, (int)(seed % 4), difficulty, 1f, false, pattern);
                    var phase = new PressurePhase(spec, seed, null);
                    var bot = new PlanningDodger(planner, 0f, Vector2.Zero);
                    var seen = new HashSet<int>();
                    while (!phase.Finished)
                    {
                        phase.Tick(Dt, bot.Steer(phase, Dt));
                        foreach (var w in phase.Walls)
                            if (w.active && seen.Add(w.serial) && System.Math.Abs(w.x) - PressurePhase.PlateRadius < PressurePhase.HalfWidth)
                                Assert.That(w.warn, Is.GreaterThanOrEqualTo(PressureRule.MinTelegraph - Dt), "a wall appeared inside the box unwarned (" + Name(pattern) + ")");
                        foreach (var r in phase.Rings)
                            if (r.active && seen.Add(r.serial))
                                Assert.That(r.warn, Is.GreaterThanOrEqualTo(PressureRule.MinTelegraph - Dt), "a ring appeared unwarned (" + Name(pattern) + ")");
                        foreach (var b in phase.Blobs)
                            if (b.active && seen.Add(b.serial) && System.Math.Abs(b.position.X) - PressurePhase.BlobRadius < PressurePhase.HalfWidth)
                                Assert.That(b.warn, Is.GreaterThanOrEqualTo(PressureRule.MinTelegraph - Dt), "batter appeared inside the box unwarned (" + Name(pattern) + ")");
                    }
                    Assert.That(phase.Refused, Is.Zero);
                }
        }

        [Test]
        public void EveryAttackIsWarnedLongEnoughToReact()
        {
            Assert.That(PressureSpec.For(0, 0, ArgumentDifficulty.Normal, 1f, false, null).telegraph, Is.InRange(0.5f, 0.8f), "normal");
            Assert.That(PressureSpec.For(0, 0, ArgumentDifficulty.Story, 1f, false, null).telegraph,
                Is.GreaterThan(PressureSpec.For(0, 0, ArgumentDifficulty.Normal, 1f, false, null).telegraph), "longer in Story");
            var phase = new PressurePhase(PressureSpec.For(2, 1, ArgumentDifficulty.Normal, 1f, false, Patterns[3]), 3, null);
            int warnedTicks = 0;
            while (!phase.Finished)
            {
                phase.Tick(Dt, Vector2.Zero);
                foreach (var r in phase.Rings) if (r.active && r.warn > 0f) { warnedTicks++; Assert.That(r.radius, Is.Zero, "a warning doesn't grow"); }
                foreach (var w in phase.Walls) if (w.active && w.warn > 0f) Assert.That(System.Math.Abs(w.x), Is.GreaterThan(PressurePhase.HalfWidth), "a warned wall waits outside");
            }
            Assert.That(warnedTicks, Is.GreaterThan(0), "rings were warned");
            Assert.That(phase.Telegraphs, Is.GreaterThan(0));
        }

        [Test]
        public void TheRuleRefusesAnAttackInsideTheBoxWithoutAWarning()
        {
            var normal = PressureSpec.For(1, 0, ArgumentDifficulty.Normal, 1f, false, null);
            Assert.That(PressureRule.Admit(true, 0f, normal), Is.False, "inside, no warning: refused");
            Assert.That(PressureRule.Admit(true, 0.3f, normal), Is.False, "inside, too short a warning: refused");
            Assert.That(PressureRule.Admit(true, normal.telegraph, normal), Is.True, "inside, warned");
            Assert.That(PressureRule.Admit(false, 0f, normal), Is.True, "coming in from outside");
            var longNight = PressureSpec.For(1, 0, ArgumentDifficulty.ScriptedButBeatable, 1f, false, null);
            Assert.That(longNight.exemptFromRule, Is.True, "the Long Night is flagged");
            Assert.That(normal.exemptFromRule || PressureSpec.For(1, 0, ArgumentDifficulty.Story, 1f, false, null).exemptFromRule, Is.False, "nothing else is");
            Assert.That(PressureRule.Admit(true, 0f, longNight), Is.True, "the only exception");
        }

        [Test]
        public void RingsNeverBloomOnTopOfHerAndAimAGapHerWay()
        {
            for (ulong seed = 1; seed <= 12; seed++)
            {
                var spec = PressureSpec.For(3, 3, ArgumentDifficulty.Normal, 1f, true, Patterns[1]);
                var phase = new PressurePhase(spec, seed, null);
                var start = Homes[seed % (ulong)Homes.Length];
                phase.PlaceLight(start);
                var seen = new HashSet<int>();
                while (!phase.Finished)
                {
                    phase.Tick(Dt, Vector2.Zero);
                    foreach (var r in phase.Rings)
                    {
                        if (!r.active || !seen.Add(r.serial)) continue;
                        Assert.That(Vector2.Distance(r.center, phase.Light), Is.GreaterThanOrEqualTo(PressurePhase.RingMinDistance - 1e-3f), "not right beside her");
                        Assert.That(System.Math.Abs(r.center.X), Is.LessThan(PressurePhase.HalfWidth));
                        Assert.That(System.Math.Abs(r.center.Y), Is.LessThan(PressurePhase.HalfHeight));
                    }
                }
            }
        }
    }
}
