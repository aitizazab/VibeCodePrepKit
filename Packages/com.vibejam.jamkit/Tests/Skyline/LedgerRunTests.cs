using System.Numerics;
using JamKit.Movement;
using JamKit.Skyline;
using JamKit.Tests.Movement;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    public class LedgerRunTests
    {
        [Test]
        public void OnlyCombatTimeCounts()
        {
            var l = new LedgerRun();
            l.Tick(1f, MoveMode.Wallrun, true);
            Assert.That(l.CombatSeconds, Is.EqualTo(0f));
            l.BeginCombat();
            l.Tick(2f, MoveMode.Wallrun, true);
            l.Tick(2f, MoveMode.Sprint, false);
            l.EndCombat();
            l.Tick(5f, MoveMode.Air, false);
            Assert.That(l.CombatSeconds, Is.EqualTo(4f));
            Assert.That(l.SecondsIn(MoveMode.Wallrun), Is.EqualTo(2f));
            Assert.That(l.ShareOf(MoveMode.Sprint), Is.EqualTo(0.5f).Within(1e-5f));
        }

        [Test]
        public void WallUseIsMeasuredByOpportunity()
        {
            var l = new LedgerRun();
            l.BeginCombat();
            l.Tick(1f, MoveMode.Wallrun, true);   // on a wall: in reach by definition
            l.Tick(3f, MoveMode.Air, true);       // a wall was in reach, not used
            l.Tick(6f, MoveMode.Sprint, false);   // no wall nearby: doesn't count against you
            Assert.That(l.WallReachSeconds, Is.EqualTo(4f));
            Assert.That(l.WallUse, Is.EqualTo(0.25f).Within(1e-5f));
        }

        [Test]
        public void NoWallInReachMeansNoWallUse()
        {
            var l = new LedgerRun();
            l.BeginCombat();
            l.Tick(3f, MoveMode.Sprint, false);
            Assert.That(l.WallUse, Is.EqualTo(0f));
        }

        [Test]
        public void DamageShareIsPerWeapon()
        {
            var l = new LedgerRun();
            l.BeginCombat();
            l.RecordShot(0, true, 30f, 12f, false);
            l.RecordShot(0, false, 0f, 0f, false);
            l.RecordShot(1, true, 10f, 4f, true);
            Assert.That(l.DamageShare(0), Is.EqualTo(0.75f).Within(1e-5f));
            Assert.That(l.Weapon(0).shots, Is.EqualTo(2));
            Assert.That(l.Weapon(0).hits, Is.EqualTo(1));
            Assert.That(l.Weapon(1).kills, Is.EqualTo(1));
            Assert.That(l.Weapon(0).MeanHitRange, Is.EqualTo(12f));
            Assert.That(l.DamageShare(7), Is.EqualTo(0f));
        }

        [Test]
        public void ShotsOutsideCombatAreIgnoredAndResetClears()
        {
            var l = new LedgerRun();
            l.RecordShot(0, true, 30f, 12f, false);
            Assert.That(l.Weapon(0).shots, Is.EqualTo(0));
            l.BeginCombat();
            l.Tick(1f, MoveMode.Air, true);
            l.RecordShot(0, true, 30f, 12f, false);
            l.Reset();
            Assert.That(l.CombatSeconds, Is.EqualTo(0f));
            Assert.That(l.Weapon(0).shots, Is.EqualTo(0));
            Assert.That(l.InCombat, Is.False);
        }

        static BoxWorld Room(float wallX, bool runnable)
        {
            var w = new BoxWorld();
            w.Floor(-20f, 20f, -20f, 20f, 0f);
            w.Add(new Vector3(wallX + 0.25f, 2f, 0f), new Vector3(0.5f, 4f, 10f), runnable);
            return w;
        }

        [Test]
        public void ARunnableWallWithinThreeMetresIsInReachWhileAirborne()
        {
            Assert.That(WallReach.InReach(Room(2.2f, true), Vector3.Zero, 1.2f, true), Is.True);
        }

        [Test]
        public void WallReachNeedsAirOrSprintAndARunnableWallInRange()
        {
            Assert.That(WallReach.InReach(Room(2.2f, true), Vector3.Zero, 1.2f, false), Is.False);
            Assert.That(WallReach.InReach(Room(4.5f, true), Vector3.Zero, 1.2f, true), Is.False);
            Assert.That(WallReach.InReach(Room(2.2f, false), Vector3.Zero, 1.2f, true), Is.False);
        }
    }
}
