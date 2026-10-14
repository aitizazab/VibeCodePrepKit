using System.Collections.Generic;
using JamKit.Skyline;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    /// <summary>The bots' damage stages (docs/SKYLINE-BOTS.md "Damage states"): thresholds at 70% and 30% of a bot's whole integrity, one
    /// stage reported per threshold crossed, in order, however many one hit crosses.</summary>
    public class DamageStatesTests
    {
        [Test]
        public void TheStagesAreUndamagedThenAtOrBelowSeventyThenAtOrBelowThirtyPercent()
        {
            var s = new DamageStates();
            Assert.That(s.Count, Is.EqualTo(2));
            Assert.That(s.StageAt(1f), Is.EqualTo(0));
            Assert.That(s.StageAt(0.9f), Is.EqualTo(0));
            Assert.That(s.StageAt(0.71f), Is.EqualTo(0));
            Assert.That(s.StageAt(0.69f), Is.EqualTo(1));
            Assert.That(s.StageAt(0.31f), Is.EqualTo(1));
            Assert.That(s.StageAt(0.29f), Is.EqualTo(2));
            Assert.That(s.StageAt(0f), Is.EqualTo(2));
            Assert.That(s.StageAt(float.NaN), Is.EqualTo(0));
        }

        [Test]
        public void AHitFromAboveNinetyToUnderThirtyReportsBothStagesInOrder()
        {
            var s = new DamageStates();
            var crossed = new List<int>();
            s.Observe(0.95f, crossed);
            Assert.That(crossed, Is.Empty);
            int stage = s.Observe(0.2f, crossed);
            Assert.That(stage, Is.EqualTo(2));
            Assert.That(crossed, Is.EqualTo(new[] { 1, 2 }), "the creator's example: both damages play, back to back");
        }

        [Test]
        public void EachStageIsReportedOnceAndStaysPut()
        {
            var s = new DamageStates();
            var crossed = new List<int>();
            s.Observe(0.6f, crossed);
            s.Observe(0.5f, crossed);
            s.Observe(0.4f, crossed);
            Assert.That(crossed, Is.EqualTo(new[] { 1 }));
            s.Observe(0.25f, crossed);
            s.Observe(0.1f, crossed);
            Assert.That(crossed, Is.EqualTo(new[] { 1, 2 }));
        }

        [Test]
        public void AMendedBotMovesBackUpWithoutReportingAndCanBeDamagedAgain()
        {
            var s = new DamageStates();
            var crossed = new List<int>();
            s.Observe(0.2f, crossed);
            crossed.Clear();
            Assert.That(s.Observe(1f, crossed), Is.EqualTo(0), "a practice bot refilling");
            Assert.That(crossed, Is.Empty);
            s.Observe(0.5f, crossed);
            Assert.That(crossed, Is.EqualTo(new[] { 1 }));
        }

        [Test]
        public void IntegrityIsTheChassisAndThePlatesAreOnlyArmourOverIt()
        {
            var d = SkylineEnemies.MallCopDrone();           // chassis 70, plates 30 and 20
            var b = new EnemyBody(d);
            Assert.That(DamageStates.Integrity(b), Is.EqualTo(1f).Within(1e-4f));
            b.Damage(30f, 0);                                  // the shell plate, gone: the chassis is untouched
            b.Damage(20f, 1);
            Assert.That(DamageStates.Integrity(b), Is.EqualTo(1f).Within(1e-4f));
            b.Damage(49f, -1);
            Assert.That(DamageStates.Integrity(b), Is.EqualTo(21f / 70f).Within(1e-4f));
            b.Damage(50f, -1);
            Assert.That(DamageStates.Integrity(b), Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void ADroneHitFromWholeToTheLastSliverShowsBothDamages()
        {
            var d = SkylineEnemies.MallCopDrone();
            var b = new EnemyBody(d);
            var s = new DamageStates();
            var crossed = new List<int>();
            s.Observe(DamageStates.Integrity(b), crossed);
            b.Damage(100f, 0);                                 // 30 off the shell, 70 into the chassis: a wreck, 0 of 70 left
            s.Observe(DamageStates.Integrity(b), crossed);
            Assert.That(crossed, Is.EqualTo(new[] { 1, 2 }));
        }

        [Test]
        public void EveryMainBotHasBothStagesToShow()
        {
            foreach (var d in SkylineEnemies.All())
            {
                var b = new EnemyBody(d);
                var s = new DamageStates();
                var crossed = new List<int>();
                for (int i = 0; i < 200 && !b.Wrecked; i++) { b.Damage(5f, -1); s.Observe(DamageStates.Integrity(b), crossed); }
                Assert.That(crossed, Is.EqualTo(new[] { 1, 2 }), d.id);
            }
        }
    }
}
