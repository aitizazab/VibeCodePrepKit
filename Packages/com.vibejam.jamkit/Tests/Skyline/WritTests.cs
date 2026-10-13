using System.Numerics;
using JamKit.Skyline;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    public class WritTests
    {
        static readonly Vector3 Lens = new Vector3(0f, 0.3f, 0.4f);
        const float WritWeakPoint = 2f;

        [Test]
        public void AWritHitOnAWeakPointCreatesASpotThere()
        {
            var spots = new WeakSpots();
            var r = Writ.Resolve(spots, HitZone.WeakPoint, Lens, 1f, true, WritWeakPoint, new WritTuning());
            Assert.That(r.multiplier, Is.EqualTo(2f));
            Assert.That(r.spotCreated, Is.True);
            Assert.That(spots.Count, Is.EqualTo(1));
            Assert.That(spots.All[0].local, Is.EqualTo(Lens));
        }

        [Test]
        public void ASpotHitIsConsumedForMoreThanAHeadshot()
        {
            var t = new WritTuning();
            var spots = new WeakSpots();
            Writ.Resolve(spots, HitZone.WeakPoint, Lens, 1f, true, WritWeakPoint, t);
            var r = Writ.Resolve(spots, HitZone.Body, Lens + new Vector3(0.1f, 0f, 0f), 1.5f, true, WritWeakPoint, t);
            Assert.That(r.spotHit, Is.True);
            Assert.That(r.multiplier, Is.EqualTo(t.spotMultiplier));
            Assert.That(r.multiplier, Is.GreaterThan(WritWeakPoint), "the identity: a spot beats a headshot");
            Assert.That(r.spotCreated, Is.False, "a spot hit makes no new spot");
            Assert.That(spots.Count, Is.EqualTo(0));
        }

        [Test]
        public void OnlyTheWritCreatesOrHitsSpots()
        {
            var t = new WritTuning();
            var spots = new WeakSpots();
            var other = Writ.Resolve(spots, HitZone.WeakPoint, Lens, 1f, false, 1.25f, t);
            Assert.That(other.multiplier, Is.EqualTo(1.25f), "the other gun's own headshot multiplier");
            Assert.That(spots.Count, Is.EqualTo(0));
            Writ.Resolve(spots, HitZone.WeakPoint, Lens, 1f, true, WritWeakPoint, t);
            var pellet = Writ.Resolve(spots, HitZone.Body, Lens, 1.2f, false, 1.25f, t);
            Assert.That(pellet.spotHit, Is.False);
            Assert.That(spots.Count, Is.EqualTo(1), "the Breacher's pellet left the spot alone (until Case Law)");
        }

        [Test]
        public void AHitAwayFromEverySpotIsAnOrdinaryHit()
        {
            var t = new WritTuning();
            var spots = new WeakSpots();
            Writ.Resolve(spots, HitZone.WeakPoint, Lens, 1f, true, WritWeakPoint, t);
            var r = Writ.Resolve(spots, HitZone.Body, new Vector3(0f, -0.4f, 0f), 1.2f, true, WritWeakPoint, t);
            Assert.That(r.multiplier, Is.EqualTo(1f));
            Assert.That(r.spotHit, Is.False);
            Assert.That(spots.Count, Is.EqualTo(1));
        }

        [Test]
        public void SpotsExpire()
        {
            var t = new WritTuning { spotLifetime = 6f };
            var spots = new WeakSpots();
            spots.Add(Lens, 0f, t);
            spots.Expire(5.9f, t);
            Assert.That(spots.Count, Is.EqualTo(1));
            Assert.That(spots.TryConsume(Lens, 6.1f, t, out _), Is.False, "gone after 6 s");
        }

        [Test]
        public void AtMostThreeSpotsTheOldestReplaced()
        {
            var t = new WritTuning();
            var spots = new WeakSpots();
            for (int i = 0; i < 4; i++) spots.Add(new Vector3(i, 0f, 0f), i * 0.1f, t);
            Assert.That(spots.Count, Is.EqualTo(3));
            Assert.That(spots.All[0].local.X, Is.EqualTo(1f), "the first spot was replaced");
        }

        [Test]
        public void TheNearestSpotWithinReachIsTheOneHit()
        {
            var t = new WritTuning { spotRadius = 0.2f };
            var spots = new WeakSpots();
            spots.Add(new Vector3(0f, 0f, 0f), 0f, t);
            spots.Add(new Vector3(0.15f, 0f, 0f), 0f, t);
            Assert.That(spots.TryConsume(new Vector3(0.12f, 0f, 0f), 0.5f, t, out var hit), Is.True);
            Assert.That(hit.local.X, Is.EqualTo(0.15f));
        }

        [Test]
        public void TheShippedWritKeepsItsIdentity()
        {
            Assert.That(new WritTuning().spotMultiplier, Is.GreaterThan(SkylineGuns.Writ().weakPointMultiplier));
        }

        [Test]
        public void EachPathIsNumbersThenAugmentThenCapstoneWithOnlyTheFirstBuilt()
        {
            foreach (var path in new[] { UpgradePaths.Writ(), UpgradePaths.Breacher() })
            {
                Assert.That(path.Length, Is.EqualTo(3));
                Assert.That(path[0].kind, Is.EqualTo(UpgradeKind.Numbers));
                Assert.That(path[1].kind, Is.EqualTo(UpgradeKind.Augment));
                Assert.That(path[2].kind, Is.EqualTo(UpgradeKind.Capstone));
                Assert.That(path[0].built, Is.True);
                Assert.That(path[1].built || path[2].built, Is.False);
                foreach (var u in path) Assert.That(u.description, Is.Not.Empty);
            }
        }

        [Test]
        public void DeeperInkRaisesTheSpotMultiplier()
        {
            var t = new WritTuning();
            var gun = new SkylineGun(SkylineGuns.Writ());
            Assert.That(UpgradePaths.Apply(UpgradePaths.Writ()[0], gun, t), Is.True);
            Assert.That(t.spotMultiplier, Is.EqualTo(3.25f));
            UpgradePaths.Apply(UpgradePaths.Writ()[0], gun, t);
            Assert.That(t.spotMultiplier, Is.EqualTo(3.25f), "applying it twice changes nothing");
        }

        [Test]
        public void QuickCellSpeedsTheBreachersReloadAndItsEvents()
        {
            var gun = new SkylineGun(SkylineGuns.Breacher());
            float before = gun.ReloadSeconds(true);
            Assert.That(UpgradePaths.Apply(UpgradePaths.Breacher()[0], gun, null), Is.True);
            Assert.That(gun.ReloadSeconds(true), Is.EqualTo(before / 1.25f).Within(1e-5f));
            UpgradePaths.Apply(UpgradePaths.Breacher()[0], gun, null);
            Assert.That(gun.ReloadSpeed, Is.EqualTo(1.25f), "applying it twice changes nothing");
        }

        [Test]
        public void UnbuiltOrForeignUpgradesDoNothing()
        {
            var t = new WritTuning();
            var writ = new SkylineGun(SkylineGuns.Writ());
            Assert.That(UpgradePaths.Apply(UpgradePaths.Writ()[1], writ, t), Is.False, "not built in M1");
            Assert.That(UpgradePaths.Apply(UpgradePaths.Breacher()[0], writ, t), Is.False, "another gun's");
            Assert.That(writ.ReloadSpeed, Is.EqualTo(1f));
            Assert.That(t.spotMultiplier, Is.EqualTo(2.5f));
        }

        // The creator's call (2026-10-13): "Writ Levels 1 and 2 have 3 max weak spots, Level 3 can have 5." The levels are the
        // three upgrade tiers; Case Law (the capstone, level 3) isn't built, so the cap comes from WritTuning.ForLevel.
        [TestCase(1, 3)]
        [TestCase(2, 3)]
        [TestCase(3, 5)]
        public void TheSpotCapByLevelIsThreeThreeFive(int level, int cap)
        {
            Assert.That(WritTuning.ForLevel(level).maxSpotsPerBot, Is.EqualTo(cap));
        }

        [Test]
        public void TheCapstoneCapIsDataAndTheBaseStaysThree()
        {
            Assert.That(WritTuning.CapstoneMaxSpots, Is.EqualTo(5));
            Assert.That(new WritTuning().maxSpotsPerBot, Is.EqualTo(3));
            Assert.That(WritTuning.ForLevel(0).maxSpotsPerBot, Is.EqualTo(3), "below level 1 is level 1");
            Assert.That(WritTuning.ForLevel(9).maxSpotsPerBot, Is.EqualTo(5), "above level 3 is level 3");
        }

        [TestCase(1)]
        [TestCase(2)]
        public void LevelsOneAndTwoReplaceTheOldestOnTheFourthSpot(int level)
        {
            var t = WritTuning.ForLevel(level);
            var spots = new WeakSpots();
            for (int i = 0; i < 4; i++) spots.Add(new Vector3(i, 0f, 0f), i * 0.1f, t);
            Assert.That(spots.Count, Is.EqualTo(3));
            Assert.That(spots.All[0].local.X, Is.EqualTo(1f), "the first spot was replaced");
        }

        [Test]
        public void LevelThreeHoldsFiveSpotsAndReplacesTheOldestOnTheSixth()
        {
            var t = WritTuning.ForLevel(3);
            var spots = new WeakSpots();
            for (int i = 0; i < 5; i++) spots.Add(new Vector3(i, 0f, 0f), i * 0.1f, t);
            Assert.That(spots.Count, Is.EqualTo(5));
            Assert.That(spots.All[0].local.X, Is.EqualTo(0f));
            spots.Add(new Vector3(5f, 0f, 0f), 0.5f, t);
            Assert.That(spots.Count, Is.EqualTo(5));
            Assert.That(spots.All[0].local.X, Is.EqualTo(1f), "the oldest was replaced");
        }

        [Test]
        public void ForLevelKeepsEveryOtherDefault()
        {
            var d = new WritTuning();
            var t = WritTuning.ForLevel(3);
            Assert.That(t.spotMultiplier, Is.EqualTo(d.spotMultiplier));
            Assert.That(t.spotLifetime, Is.EqualTo(d.spotLifetime));
            Assert.That(t.spotRadius, Is.EqualTo(d.spotRadius));
            Assert.That(t.consumeOnHit, Is.EqualTo(d.consumeOnHit));
        }

        // Amendment 13: the spot rules are tunable data defaults pending the creator, not hard-coded constants.
        [Test]
        public void TheSpotRulesAreTunableDataWithTheDecidedDefaults()
        {
            var t = new WritTuning();
            Assert.That(t.consumeOnHit, Is.True);
            Assert.That(t.maxSpotsPerBot, Is.EqualTo(3));
            Assert.That(t.spotLifetime, Is.EqualTo(6f));

            var keep = new WritTuning { consumeOnHit = false };
            var spots = new WeakSpots();
            Writ.Resolve(spots, HitZone.WeakPoint, Lens, 1f, true, WritWeakPoint, keep);
            var r = Writ.Resolve(spots, HitZone.Body, Lens, 1.5f, true, WritWeakPoint, keep);
            Assert.That(r.spotHit, Is.True);
            Assert.That(r.multiplier, Is.EqualTo(keep.spotMultiplier));
            Assert.That(spots.Count, Is.EqualTo(1), "with consumeOnHit off the spot stays");
            Assert.That(r.spotCreated, Is.False, "still no new spot from a spot hit");

            var two = new WritTuning { maxSpotsPerBot = 2 };
            var few = new WeakSpots();
            for (int i = 0; i < 3; i++) few.Add(new Vector3(i, 0f, 0f), i * 0.1f, two);
            Assert.That(few.Count, Is.EqualTo(2));
        }
    }
}
