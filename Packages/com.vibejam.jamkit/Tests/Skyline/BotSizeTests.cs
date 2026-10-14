using System;
using System.Text;
using JamKit.Skyline;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    /// <summary>The larger bots (the creator, 2026-10-15, from the first playtest: the drone was "tiny. I could barely see it at the ranges
    /// I was fighting it" and "it was impossible to land a headshot on a drone"). The three main bots grow, and their weak parts more
    /// than proportionally; the numbers are in docs/SKYLINE-BOTS.md.</summary>
    public class BotSizeTests
    {
        static readonly string[] Main = { SkylineEnemies.DroneId, SkylineEnemies.FloorCareId, SkylineEnemies.SpotlightId };

        /// <summary>The body radii before the change (m).</summary>
        static float OldRadius(string id) => id == SkylineEnemies.DroneId ? 0.45f : id == SkylineEnemies.FloorCareId ? 0.5f : 0.6f;

        [Test]
        public void TheThreeMainBotsAreLargerAndTheirRadiusFollowsTheScale()
        {
            foreach (var id in Main)
            {
                var d = SkylineEnemies.Find(id);
                Assert.That(d.bodyScale, Is.InRange(1.2f, 1.6f), id + ": a bit larger, not a different bot");
                Assert.That(d.bodyRadius, Is.EqualTo(OldRadius(id) * d.bodyScale).Within(0.02f), id + ": the sim's radius is the old one times the scale");
            }
        }

        [Test]
        public void TheWeakPartsGrowMoreThanProportionally()
        {
            foreach (var id in Main)
            {
                var d = SkylineEnemies.Find(id);
                Assert.That(d.weakPointScale, Is.GreaterThan(1.2f), id + ": the weak part's own extra scale");
                Assert.That(BotSizes.WeakRadius(d) / BotSizes.OldWeakRadius(d), Is.GreaterThan(d.bodyScale * 1.2f), id + ": more than the body");
            }
        }

        [Test]
        public void TheDronesRotorsAreAboutAMetreAcrossAndEveryWeakPartLooksAtLeastHalfAgainAsWide()
        {
            var drone = SkylineEnemies.MallCopDrone();
            Assert.That(2f * BotSizes.WeakRadius(drone), Is.GreaterThanOrEqualTo(0.9f), "the creator: much larger rotors");
            foreach (var id in Main)
            {
                var d = SkylineEnemies.Find(id);
                foreach (float range in new[] { 10f, 20f, 30f })
                    Assert.That(BotSizes.AngularDegrees(BotSizes.WeakRadius(d), range), Is.GreaterThanOrEqualTo(1.5f * BotSizes.AngularDegrees(BotSizes.OldWeakRadius(d), range)),
                        id + " at " + range + " m");
            }
        }

        [Test]
        public void TheGondolaIsUnchanged()
        {
            var g = SkylineEnemies.Gondola();
            Assert.That(g.bodyScale, Is.EqualTo(1f));
            Assert.That(g.weakPointScale, Is.EqualTo(1f));
        }

        [Test]
        public void AngularSizeFallsWithRangeAndIsAboutRadiusOverDistanceWhenSmall()
        {
            Assert.That(BotSizes.AngularDegrees(0.5f, 10f), Is.GreaterThan(BotSizes.AngularDegrees(0.5f, 20f)));
            Assert.That(BotSizes.AngularDegrees(0.1f, 20f), Is.EqualTo(2f * 0.005f * 57.2958f).Within(0.01f));
            Assert.That(BotSizes.AngularDegrees(0.5f, 0f), Is.EqualTo(180f));
        }

        [Test, Explicit("prints the table of docs/SKYLINE-BOTS.md: the weak part's angular width before and after, at 10, 20 and 30 m")]
        public void PrintTheAngularSizes()
        {
            var sb = new StringBuilder();
            foreach (var id in Main)
            {
                var d = SkylineEnemies.Find(id);
                sb.Append($"{id}: weak radius {BotSizes.OldWeakRadius(d):0.000} -> {BotSizes.WeakRadius(d):0.000} m; width at");
                foreach (float r in new[] { 10f, 20f, 30f })
                    sb.Append($" {r:0} m {BotSizes.AngularDegrees(BotSizes.OldWeakRadius(d), r):0.00} -> {BotSizes.AngularDegrees(BotSizes.WeakRadius(d), r):0.00} deg;");
                sb.AppendLine();
            }
            TestContext.WriteLine(sb.ToString());
        }
    }
}
