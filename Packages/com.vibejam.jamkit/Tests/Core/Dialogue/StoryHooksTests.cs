using System;
using System.Collections.Generic;
using JamKit.Dialogue;
using NUnit.Framework;

namespace JamKit.Tests
{
    public class StoryHooksTests
    {
        static Func<string, bool> Has(params string[] knots)
        {
            var set = new HashSet<string>(knots);
            return set.Contains;
        }

        [Test]
        public void IdsAreInkSafe()
        {
            Assert.That(StoryHooks.Id("Gatekeeper"), Is.EqualTo("gatekeeper"));
            Assert.That(StoryHooks.Id("The Last Light"), Is.EqualTo("the_last_light"));
            Assert.That(StoryHooks.Id("  Forge-Mother's  Patience! "), Is.EqualTo("forge_mothers_patience"));
            Assert.That(StoryHooks.Id("boss__2"), Is.EqualTo("boss_2"));
            Assert.That(StoryHooks.Id(""), Is.EqualTo(""));
            Assert.That(StoryHooks.Id(null), Is.EqualTo(""));
        }

        [Test]
        public void BossKnotsGoFromSpecificToGeneric()
        {
            Assert.That(StoryHooks.BossIntro("Gatekeeper"), Is.EqualTo(new[] { "boss_gatekeeper_intro", "boss_intro" }));
            Assert.That(StoryHooks.BossPhase("The Last Light", 2), Is.EqualTo(new[] { "boss_the_last_light_phase_2", "boss_the_last_light_phase", "boss_phase" }));
            Assert.That(StoryHooks.BossDefeat("forgemother"), Is.EqualTo(new[] { "boss_forgemother_defeat", "boss_defeat" }));
            Assert.That(StoryHooks.BossIntro(""), Is.EqualTo(new[] { "boss_intro" }));
            Assert.That(StoryHooks.BossPhase(null, 3), Is.EqualTo(new[] { "boss_phase" }));
        }

        [Test]
        public void ResolvePicksTheFirstKnotTheStoryHas()
        {
            var phase = StoryHooks.BossPhase("Gatekeeper", 3);
            Assert.That(StoryHooks.Resolve(phase, Has("boss_gatekeeper_phase_3", "boss_phase")), Is.EqualTo("boss_gatekeeper_phase_3"));
            Assert.That(StoryHooks.Resolve(phase, Has("boss_gatekeeper_phase", "boss_phase")), Is.EqualTo("boss_gatekeeper_phase"), "any phase of this boss");
            Assert.That(StoryHooks.Resolve(phase, Has("boss_phase")), Is.EqualTo("boss_phase"), "any boss");
            Assert.That(StoryHooks.Resolve(phase, Has("boss_forgemother_phase_3")), Is.Null, "another boss's line never plays");
        }

        [Test]
        public void MissingKnotsFallBackSilently()
        {
            Assert.That(StoryHooks.Resolve(StoryHooks.BossIntro("Gatekeeper"), Has()), Is.Null);
            Assert.That(StoryHooks.Resolve(null, Has("death")), Is.Null);
            Assert.That(StoryHooks.Resolve(StoryHooks.Rest(1), null), Is.Null);
            Func<string, bool> throws = k => throw new InvalidOperationException("broken story");
            Assert.That(StoryHooks.Resolve(StoryHooks.Death("gatekeeper", 1), throws), Is.Null, "a throwing lookup is a miss, not a crash");
        }

        [Test]
        public void DeathLineByKillerThenFloorThenAnything()
        {
            var c = StoryHooks.Death("Gatekeeper", 1);
            Assert.That(c, Is.EqualTo(new[] { "death_gatekeeper", "death_floor_1", "death" }));
            Assert.That(StoryHooks.Resolve(c, Has("death_gatekeeper", "death")), Is.EqualTo("death_gatekeeper"));
            Assert.That(StoryHooks.Resolve(c, Has("death_floor_1", "death")), Is.EqualTo("death_floor_1"));
            Assert.That(StoryHooks.Resolve(StoryHooks.Death("battle", 2), Has("death_floor_1", "death")), Is.EqualTo("death"));
            Assert.That(StoryHooks.Death("", 0), Is.EqualTo(new[] { "death" }));
        }

        [Test]
        public void RestAndRunStartVaryByFloorAndRun()
        {
            Assert.That(StoryHooks.Resolve(StoryHooks.Rest(2), Has("rest_floor_2", "rest")), Is.EqualTo("rest_floor_2"));
            Assert.That(StoryHooks.Resolve(StoryHooks.Rest(3), Has("rest_floor_2", "rest")), Is.EqualTo("rest"));
            Assert.That(StoryHooks.Resolve(StoryHooks.RunStart(1), Has("run_start_2", "run_start")), Is.EqualTo("run_start"));
            Assert.That(StoryHooks.Resolve(StoryHooks.RunStart(2), Has("run_start_2", "run_start")), Is.EqualTo("run_start_2"), "the second run has its own line");
            Assert.That(StoryHooks.RunStart(0), Is.EqualTo(new[] { "run_start" }));
        }
    }
}
