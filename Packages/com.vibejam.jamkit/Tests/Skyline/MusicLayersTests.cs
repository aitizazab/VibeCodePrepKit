using System;
using System.Collections.Generic;
using JamKit.Skyline;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    /// <summary>Task M1: the threat score, the smoother and the vertical layers (docs/SKYLINE-MUSIC.md, adaptive music).</summary>
    public class MusicLayersTests
    {
        const string D = SkylineEnemies.DroneId, F = SkylineEnemies.FloorCareId, G = SkylineEnemies.GondolaId, S = SkylineEnemies.SpotlightId;

        static ThreatSample Bot(string id, float dist, bool alerted = true) => new ThreatSample(id, dist, alerted);

        // ---- the threat score ----

        [Test]
        public void NoBotsOrNoAlertedBotsIsNoThreat()
        {
            Assert.That(ThreatScore.Compute(new ThreatSample[0]), Is.EqualTo(0f));
            Assert.That(ThreatScore.Compute(null), Is.EqualTo(0f));
            Assert.That(ThreatScore.Compute(new[] { Bot(D, 5f, false), Bot(F, 3f, false) }), Is.EqualTo(0f), "asleep bots do not count");
        }

        [Test]
        public void DangerRisesWithMoreAlertedBotsNear()
        {
            float one = ThreatScore.Compute(new[] { Bot(D, 8f) });
            float two = ThreatScore.Compute(new[] { Bot(D, 8f), Bot(F, 8f) });
            float four = ThreatScore.Compute(new[] { Bot(D, 8f), Bot(D, 8f), Bot(F, 8f), Bot(F, 8f) });
            Assert.That(one, Is.GreaterThan(0f));
            Assert.That(two, Is.GreaterThan(one));
            Assert.That(four, Is.GreaterThan(two));
            Assert.That(four, Is.EqualTo(1f), "four near bots is full danger");
            Assert.That(ThreatScore.Compute(new[] { Bot(D, 8f), Bot(D, 8f), Bot(D, 8f), Bot(D, 8f), Bot(D, 8f), Bot(D, 8f) }), Is.EqualTo(1f), "capped");
        }

        [Test]
        public void NearerBotsWeighMoreAndFarOnesNothing()
        {
            float near = ThreatScore.Compute(new[] { Bot(D, 5f) });
            float mid = ThreatScore.Compute(new[] { Bot(D, 26f) });
            float far = ThreatScore.Compute(new[] { Bot(D, ThreatScore.FarRange) });
            Assert.That(near, Is.EqualTo(1f / ThreatScore.FullAt).Within(1e-6f));
            Assert.That(mid, Is.GreaterThan(0f).And.LessThan(near));
            Assert.That(far, Is.EqualTo(0f));
        }

        [Test]
        public void BotTypesAreWeighted()
        {
            float drone = ThreatScore.Compute(new[] { Bot(D, 5f) });
            float spot = ThreatScore.Compute(new[] { Bot(S, 5f) });
            float gondola = ThreatScore.Compute(new[] { Bot(G, 5f) });
            Assert.That(spot, Is.GreaterThan(drone));
            Assert.That(gondola, Is.LessThan(drone));
        }

        // ---- the smoother ----

        [TestCase(0.016f)]
        [TestCase(0.1f)]
        [TestCase(0.5f)]
        [TestCase(2f)]
        public void TheSmootherCoversSixtyThreePercentInTheGivenSecondsHoweverTheFramesAreCut(float dt)
        {
            var s = new LayerSmoother(0f);
            float seconds = 2f;
            int steps = (int)Math.Round(seconds / dt);
            for (int i = 0; i < steps; i++) s.Tick(1f, dt, seconds);
            Assert.That(s.Value, Is.EqualTo(1f - 1f / (float)Math.E).Within(0.01f), "dt " + dt);
        }

        [Test]
        public void OneBigStepEqualsManySmallOnes()
        {
            var a = new LayerSmoother(0f);
            var b = new LayerSmoother(0f);
            a.Tick(1f, 1f, 3f);
            for (int i = 0; i < 1000; i++) b.Tick(1f, 0.001f, 3f);
            Assert.That(a.Value, Is.EqualTo(b.Value).Within(1e-4f));
        }

        [Test]
        public void TheSmootherNeverOvershootsAndIgnoresNonsenseTimes()
        {
            var s = new LayerSmoother(0.2f);
            s.Tick(1f, 1000f, 1f);
            Assert.That(s.Value, Is.LessThanOrEqualTo(1f));
            s.Tick(0f, -1f, 1f);
            Assert.That(s.Value, Is.GreaterThan(0.99f), "a negative dt changes nothing");
            s.Tick(0f, 0.1f, 0f);
            Assert.That(s.Value, Is.EqualTo(0f), "zero seconds snaps");
        }

        // ---- the layers ----

        static MusicLayers Run(float speed, float threat, bool traversal, float seconds, MusicLayers l = null)
        {
            l = l ?? new MusicLayers();
            for (float t = 0; t < seconds; t += 0.02f) l.Tick(speed, threat, traversal, 0.02f);
            return l;
        }

        [Test]
        public void MotionPicksUpWithSpeedAndSettlesWhenTheyStop()
        {
            var l = Run(0f, 0f, false, 10f);
            Assert.That(l.motion, Is.EqualTo(0f).Within(0.01f));
            Run(9f, 0f, false, 10f, l);
            Assert.That(l.motion, Is.GreaterThan(0.95f));
            Run(0f, 0f, false, 1f, l);
            Assert.That(l.motion, Is.GreaterThan(0.5f), "it settles over seconds, not at once");
            Run(0f, 0f, false, 15f, l);
            Assert.That(l.motion, Is.LessThan(0.05f));
        }

        [Test]
        public void DangerRisesWithTheThreatAndLingersAfterIt()
        {
            var l = Run(0f, 0f, false, 5f);
            Assert.That(l.danger, Is.LessThan(0.01f));
            Run(0f, 1f, false, 8f, l);
            Assert.That(l.danger, Is.GreaterThan(0.9f));
            Run(0f, 0f, false, 2f, l);
            Assert.That(l.danger, Is.GreaterThan(0.5f), "it lingers");
            Run(0f, 0f, false, 20f, l);
            Assert.That(l.danger, Is.LessThan(0.05f));
        }

        [Test]
        public void LightComesUpWhenTheThreatIsLowOrTheSectionIsTraversal()
        {
            var calm = Run(0f, 0f, false, 15f);
            Assert.That(calm.light, Is.GreaterThan(0.95f), "low threat: light");
            var fight = Run(0f, 0.8f, false, 15f);
            Assert.That(fight.light, Is.LessThan(0.05f), "a fight: no light");
            var parkour = Run(0f, 0.8f, true, 15f);
            Assert.That(parkour.light, Is.GreaterThan(0.95f), "traversal: light even with a threat about");
        }

        [Test]
        public void ASingleBotFlickerNeverJumpsALayer()
        {
            var l = Run(0f, 0f, false, 15f);
            float dangerBefore = l.danger, lightBefore = l.light;
            // one bot alerted for a tenth of a second
            float maxDanger = 0f, minLight = 1f;
            for (int i = 0; i < 5; i++)
            {
                l.Tick(0f, 0.25f, false, 0.02f);
                maxDanger = Math.Max(maxDanger, l.danger);
                minLight = Math.Min(minLight, l.light);
            }
            Assert.That(maxDanger - dangerBefore, Is.LessThan(0.05f));
            Assert.That(lightBefore - minLight, Is.LessThan(0.05f));
            // and per frame no layer moves by more than its smoothing allows (a 60th of a second at its time constant)
            var m = new MusicLayers();
            float prevD = 0f;
            for (int i = 0; i < 600; i++)
            {
                m.Tick(0f, i % 2 == 0 ? 1f : 0f, false, 1f / 60f);   // threat flickering every frame
                Assert.That(Math.Abs(m.danger - prevD), Is.LessThan(0.02f));
                prevD = m.danger;
            }
        }

        [Test]
        public void ResetSettlesEverythingToSilence()
        {
            var l = Run(9f, 1f, true, 10f);
            l.Reset();
            Assert.That(l.motion + l.danger + l.light, Is.EqualTo(0f));
            l.Tick(0f, 0f, false, 0.02f);
            Assert.That(l.danger, Is.EqualTo(0f));
        }

        [Test]
        public void TheLayersFollowAFightFromTheListOfBots()
        {
            var bots = new List<ThreatSample> { Bot(D, 10f), Bot(D, 12f), Bot(F, 9f), Bot(F, 11f) };
            var l = Run(6f, ThreatScore.Compute(bots), false, 10f);
            Assert.That(l.danger, Is.GreaterThan(0.9f));
            Assert.That(l.motion, Is.GreaterThan(0.5f));
            Assert.That(l.light, Is.LessThan(0.1f));
        }
    }
}
