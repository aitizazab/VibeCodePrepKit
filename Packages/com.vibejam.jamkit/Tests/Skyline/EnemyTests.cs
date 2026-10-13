using JamKit.Skyline;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    public class EnemyTests
    {
        static EnemyDef TwoPlates() => new EnemyDef
        {
            id = "test", chassisHp = 50f, plates = new[] { new PlateDef("left", 20f), new PlateDef("right", 20f) },
        };

        [Test]
        public void TheThreeM1BotsCarryTheSpecsTags()
        {
            var drone = SkylineEnemies.MallCopDrone();
            var floor = SkylineEnemies.FloorCareUnit();
            var spot = SkylineEnemies.Spotlight();
            Assert.That(drone.aim, Is.EqualTo(AimModel.LeadsYou));
            Assert.That(drone.layer, Is.EqualTo(ThreatLayer.Air));
            Assert.That(floor.aim, Is.EqualTo(AimModel.Area));
            Assert.That(floor.layer, Is.EqualTo(ThreatLayer.Floor));
            Assert.That(spot.aim, Is.EqualTo(AimModel.FiresAtWhereYouAre));
            Assert.That(spot.layer, Is.EqualTo(ThreatLayer.Air));
            Assert.That(spot.projectileSpeed, Is.EqualTo(0f), "a telegraphed hitscan");
            Assert.That(spot.telegraphSeconds, Is.EqualTo(0.8f), "the spec's 0.8 s telegraph");
            Assert.That(spot.beamLockBefore, Is.EqualTo(0.25f), "locks 0.25 s before the shot");
            Assert.That(AimRules.CanShareEncounter(drone.aim, spot.aim), Is.False, "never in one encounter");
        }

        [Test]
        public void EveryBotHasPlatesAndAWayToHurtYou()
        {
            foreach (var d in SkylineEnemies.All())
            {
                Assert.That(d.plates.Length, Is.GreaterThanOrEqualTo(2), d.id);
                Assert.That(d.attackDamage, Is.GreaterThan(0f), d.id);
                Assert.That(d.fireInterval, Is.GreaterThan(d.telegraphSeconds), d.id + ": the cycle holds the telegraph");
                Assert.That(SkylineEnemies.Find(d.id).id, Is.EqualTo(d.id));
            }
            Assert.That(SkylineEnemies.Find("nope"), Is.Null);
        }

        [Test]
        public void APlateAbsorbsFirstAndShedsOnce()
        {
            var b = new EnemyBody(TwoPlates());
            var first = b.Damage(15f, 0);
            Assert.That(first.toPlate, Is.EqualTo(15f));
            Assert.That(first.toChassis, Is.EqualTo(0f));
            Assert.That(first.plateShed, Is.EqualTo(-1));
            var second = b.Damage(5f, 0);
            Assert.That(second.plateShed, Is.EqualTo(0), "shed where it was hit");
            Assert.That(b.PlateOn(0), Is.False);
            Assert.That(b.PlateOn(1), Is.True, "the other plate stays");
        }

        [Test]
        public void DamagePastAPlateCarriesIntoTheChassis()
        {
            var b = new EnemyBody(TwoPlates());
            var r = b.Damage(30f, 1);
            Assert.That(r.toPlate, Is.EqualTo(20f));
            Assert.That(r.toChassis, Is.EqualTo(10f));
            Assert.That(r.Applied, Is.EqualTo(30f));
            Assert.That(r.plateShed, Is.EqualTo(1));
            Assert.That(b.Chassis, Is.EqualTo(40f));
        }

        [Test]
        public void HitsWhereAPlateWasShedOrOnNoPlateGoToTheChassis()
        {
            var b = new EnemyBody(TwoPlates());
            b.Damage(20f, 0);
            var r = b.Damage(5f, 0);
            Assert.That(r.toChassis, Is.EqualTo(5f));
            Assert.That(r.plateShed, Is.EqualTo(-1), "a plate sheds once");
            Assert.That(b.Damage(5f, -1).toChassis, Is.EqualTo(5f));
            Assert.That(b.Damage(5f, 7).toChassis, Is.EqualTo(5f), "an index out of range is the chassis");
        }

        [Test]
        public void AWreckHappensOnceAndLaterHitsDoNothing()
        {
            var b = new EnemyBody(TwoPlates());
            var r = b.Damage(500f, -1);
            Assert.That(r.wrecked, Is.True);
            Assert.That(r.toChassis, Is.EqualTo(50f), "only what was left");
            var after = b.Damage(10f, 0);
            Assert.That(after.wrecked, Is.False);
            Assert.That(after.Applied, Is.EqualTo(0f));
        }

        [Test]
        public void ZeroNegativeNaNOrInfiniteDamageIsSafe()
        {
            var b = new EnemyBody(TwoPlates());
            Assert.That(b.Damage(0f, 0).Applied, Is.EqualTo(0f));
            Assert.That(b.Damage(-10f, 0).Applied, Is.EqualTo(0f));
            Assert.That(b.Damage(float.NaN, 0).Applied, Is.EqualTo(0f));
            Assert.That(b.Chassis, Is.EqualTo(50f));
            Assert.That(b.Damage(float.PositiveInfinity, -1).wrecked, Is.True);
            Assert.That(float.IsNaN(b.Chassis), Is.False);
        }

        [Test]
        public void PlatingAbsorbsFirstThenTheChassis()
        {
            var a = new PlayerArmor(100f, 100f);
            var r = a.Damage(130f);
            Assert.That(r.toPlating, Is.EqualTo(100f));
            Assert.That(r.toChassis, Is.EqualTo(30f));
            Assert.That(r.platingBroke, Is.True);
            Assert.That(a.PlatingBreaks, Is.EqualTo(1));
            Assert.That(a.Damage(10f).platingBroke, Is.False, "already broken");
            Assert.That(a.PlatingBreaks, Is.EqualTo(1));
        }

        [Test]
        public void ScrapFromAFreshWreckRestoresPlatingOnce()
        {
            var a = new PlayerArmor(100f, 100f);
            a.Damage(60f);
            var w = new Wreck(SkylineEnemies.MallCopDrone(), 10f);
            float got = a.Strip(w, 12f);
            Assert.That(got, Is.EqualTo(SkylineEnemies.MallCopDrone().scrapPlating));
            Assert.That(a.Plating, Is.EqualTo(40f + got));
            Assert.That(a.Strip(w, 12.5f), Is.EqualTo(0f), "a wreck is stripped once");
        }

        [Test]
        public void AStaleWreckOrFullPlatingGivesNothing()
        {
            var a = new PlayerArmor(100f, 100f);
            a.Damage(50f);
            Assert.That(a.Strip(new Wreck(SkylineEnemies.MallCopDrone(), 0f), 9f), Is.EqualTo(0f), "older than 8 s");
            var full = new PlayerArmor(100f, 100f);
            var w = new Wreck(SkylineEnemies.MallCopDrone(), 0f);
            Assert.That(full.Strip(w, 1f), Is.EqualTo(0f));
            Assert.That(w.stripped, Is.False, "left for later if you couldn't use it");
        }

        [Test]
        public void TheChassisNeverComesBackFromScrap()
        {
            var a = new PlayerArmor(100f, 100f);
            a.Damage(150f);
            a.Strip(new Wreck(SkylineEnemies.FloorCareUnit(), 0f), 1f);
            Assert.That(a.Chassis, Is.EqualTo(50f));
            Assert.That(a.Plating, Is.GreaterThan(0f));
        }

        [Test]
        public void AWreckedUnitTakesNoMoreDamage()
        {
            var a = new PlayerArmor(10f, 10f);
            Assert.That(a.Damage(25f).wrecked, Is.True);
            Assert.That(a.Damage(5f).toChassis, Is.EqualTo(0f));
            Assert.That(a.Damage(float.NaN).toPlating, Is.EqualTo(0f));
        }

        [Test]
        public void BadDamageToALiveUnitChangesNothingAndNeverMakesNaN()
        {
            var a = new PlayerArmor(100f, 100f);
            foreach (float bad in new[] { float.NaN, -5f, 0f })
            {
                var r = a.Damage(bad);
                Assert.That(r.toPlating, Is.EqualTo(0f), "damage " + bad);
                Assert.That(r.toChassis, Is.EqualTo(0f), "damage " + bad);
                Assert.That(r.platingBroke, Is.False);
                Assert.That(r.wrecked, Is.False);
                Assert.That(a.Plating, Is.EqualTo(100f));
                Assert.That(a.Chassis, Is.EqualTo(100f));
                Assert.That(a.PlatingBreaks, Is.EqualTo(0));
            }
            var inf = a.Damage(float.PositiveInfinity);
            Assert.That(inf.wrecked, Is.True, "a capped hit still wrecks");
            Assert.That(inf.toPlating, Is.EqualTo(100f));
            Assert.That(inf.toChassis, Is.EqualTo(100f));
            Assert.That(float.IsNaN(a.Plating), Is.False);
            Assert.That(float.IsNaN(a.Chassis), Is.False);
            Assert.That(a.Wrecked, Is.True);
        }

        [Test]
        public void BadBodyNumbersInTheConstructorsTakeSafeValues()
        {
            var nan = new EnemyBody(new EnemyDef
            {
                chassisHp = float.NaN,
                plates = new[] { new PlateDef("a", float.NaN), new PlateDef("b", -3f), null, new PlateDef("c", 20f) },
            });
            Assert.That(nan.Chassis, Is.EqualTo(1f), "a NaN chassis is 1 hp");
            Assert.That(nan.PlateOn(0), Is.False, "a NaN plate is already shed");
            Assert.That(nan.PlateOn(1), Is.False);
            Assert.That(nan.PlateOn(2), Is.False, "a null plate is already shed");
            Assert.That(nan.PlateHp(3), Is.EqualTo(20f));
            Assert.That(new EnemyBody(new EnemyDef { chassisHp = -4f }).Chassis, Is.EqualTo(1f));

            var huge = new EnemyBody(new EnemyDef
            {
                chassisHp = float.PositiveInfinity,
                plates = new[] { new PlateDef("a", float.PositiveInfinity) },
            });
            Assert.That(huge.Chassis, Is.EqualTo(1e6f), "+infinity is the 1e6 ceiling");
            Assert.That(huge.PlateHp(0), Is.EqualTo(1e6f));
            Assert.That(huge.Damage(float.PositiveInfinity, -1).wrecked, Is.True, "one capped hit can wreck it");

            var p = new PlayerArmor(float.NaN, float.NaN);
            Assert.That(p.PlatingMax, Is.EqualTo(0f));
            Assert.That(p.Plating, Is.EqualTo(0f));
            Assert.That(p.ChassisMax, Is.EqualTo(1f));
            Assert.That(p.Chassis, Is.EqualTo(1f));
            var neg = new PlayerArmor(-5f, -5f);
            Assert.That(neg.PlatingMax, Is.EqualTo(0f));
            Assert.That(neg.ChassisMax, Is.EqualTo(1f));

            var big = new PlayerArmor(float.PositiveInfinity, float.PositiveInfinity);
            Assert.That(big.PlatingMax, Is.EqualTo(1e6f));
            Assert.That(big.ChassisMax, Is.EqualTo(1e6f));
            var noPlating = new PlayerArmor(0f, float.PositiveInfinity);
            Assert.That(noPlating.Damage(float.PositiveInfinity).wrecked, Is.True, "one capped hit can wreck the Unit");
        }
    }
}
