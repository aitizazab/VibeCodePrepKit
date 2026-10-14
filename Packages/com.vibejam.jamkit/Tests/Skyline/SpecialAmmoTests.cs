using System.Collections.Generic;
using JamKit.Combat;
using JamKit.Skyline;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    public class SpecialAmmoTests
    {
        static SpecialAmmoLedger Ledger(int start = 0, int max = 8) =>
            new SpecialAmmoLedger(new SpecialAmmoDef { start = start, max = max });

        static SkylineGun Breacher(List<GunCue> cues)
        {
            var g = new SkylineGun(SkylineGuns.Breacher());
            g.Equip(cues);
            g.Tick(2f, false, false, cues);   // the equip clip ends
            return g;
        }

        /// <summary>Fires one shell and lets the fire interval pass.</summary>
        static void FireOne(SkylineGun g, List<GunCue> cues)
        {
            g.Tick(0.02f, true, true, cues);
            g.Tick(1.5f, false, false, cues);
        }

        [Test]
        public void TheDraftNumbersAreTheCreatorsStartingValues()
        {
            var d = new SpecialAmmoDef();
            Assert.That(d.perPrimaryKill, Is.EqualTo(1));
            Assert.That(d.killsPerShot, Is.EqualTo(3));
            Assert.That(d.start, Is.EqualTo(2));
            Assert.That(d.max, Is.EqualTo(8));
            Assert.That(new SpecialAmmoLedger().Shots, Is.EqualTo(2), "the run starts with the start value");
        }

        [Test]
        public void AKillByAPrimaryWeaponGivesOneShot()
        {
            var l = Ledger();
            Assert.That(l.Credit(false), Is.EqualTo(1));
            Assert.That(l.Credit(false), Is.EqualTo(1));
            Assert.That(l.Shots, Is.EqualTo(2));
        }

        [Test]
        public void SpecialKillsGiveOneShotPerThreeAndTheCounterPersistsAcrossWrecks()
        {
            var l = Ledger();
            Assert.That(l.Credit(true), Is.EqualTo(0));
            Assert.That(l.Credit(true), Is.EqualTo(0));
            Assert.That(l.KillProgress, Is.EqualTo(2));
            Assert.That(l.Credit(false), Is.EqualTo(1), "a primary kill in between neither adds to nor resets the count");
            Assert.That(l.KillProgress, Is.EqualTo(2));
            Assert.That(l.Credit(true), Is.EqualTo(1), "the third special kill pays");
            Assert.That(l.KillProgress, Is.EqualTo(0));
            Assert.That(l.Shots, Is.EqualTo(2));
            for (int i = 0; i < 6; i++) l.Credit(true);
            Assert.That(l.Shots, Is.EqualTo(4), "six more special kills, two more shots");
        }

        [Test]
        public void TheMaximumCapsTheReserveAndTheExcessIsNotBanked()
        {
            var l = Ledger(start: 7, max: 8);
            Assert.That(l.Credit(false), Is.EqualTo(1));
            Assert.That(l.Shots, Is.EqualTo(8));
            Assert.That(l.Credit(false), Is.EqualTo(0));
            Assert.That(l.Credit(false), Is.EqualTo(0));
            Assert.That(l.TryConsume(), Is.True);
            Assert.That(l.Shots, Is.EqualTo(7), "no hidden bank of the lost shots");
            // A special kill's third shot is lost at the cap too, and the counter still restarts.
            var m = Ledger(start: 8);
            m.Credit(true); m.Credit(true);
            Assert.That(m.Credit(true), Is.EqualTo(0));
            Assert.That(m.KillProgress, Is.EqualTo(0));
            Assert.That(m.Shots, Is.EqualTo(8));
            Assert.That(Ledger(start: 99, max: 8).Shots, Is.EqualTo(8), "a start above the max is clamped");
        }

        [Test]
        public void GainedReportsTheShotsAKillAdded()
        {
            var l = Ledger(start: 7);
            var seen = new List<int>();
            l.Gained += n => seen.Add(n);
            l.Credit(true);    // 0 shots: no event
            l.Credit(false);   // 7 -> 8
            l.Credit(false);   // full: no event
            Assert.That(seen, Is.EqualTo(new[] { 1 }));
        }

        [Test]
        public void TryConsumeSpendsShotsUntilEmpty()
        {
            var l = Ledger(start: 2);
            Assert.That(l.TryConsume(), Is.True);
            Assert.That(l.TryConsume(), Is.True);
            Assert.That(l.TryConsume(), Is.False);
            Assert.That(l.Shots, Is.EqualTo(0));
        }

        [Test]
        public void ResetRestoresTheStartAndZeroesTheCounter()
        {
            var l = Ledger(start: 2);
            l.Credit(false); l.Credit(false); l.Credit(true); l.Credit(true);
            l.TryConsume();
            Assert.That(l.KillProgress, Is.EqualTo(2));
            l.Reset();
            Assert.That(l.Shots, Is.EqualTo(2));
            Assert.That(l.KillProgress, Is.EqualTo(0));
            Assert.That(l.Credit(true), Is.EqualTo(0), "the count starts again from one");
        }

        [Test]
        public void ThePrimaryGunKeepsItsInfiniteReserveAndTheBreacherDoesNot()
        {
            var writ = new SkylineGun(SkylineGuns.Writ());
            var breacher = new SkylineGun(SkylineGuns.Breacher());
            Assert.That(SkylineGuns.Writ().special, Is.False);
            Assert.That(SkylineGuns.Breacher().special, Is.True);
            Assert.That(writ.State.Def.infiniteReserve, Is.True);
            Assert.That(breacher.State.Def.infiniteReserve, Is.False);
            Assert.That(breacher.State.Reserve, Is.EqualTo(0), "nothing until a ledger is bound");
        }

        [Test]
        public void AddReserveOnAnInfiniteReserveGunDoesNothing()
        {
            var writ = new SkylineGun(SkylineGuns.Writ());
            int before = writ.State.Reserve;
            Assert.That(writ.State.AddReserve(5), Is.EqualTo(0));
            Assert.That(writ.State.Reserve, Is.EqualTo(before));
            var l = Ledger();
            Assert.Throws<System.ArgumentException>(() => l.Bind(writ), "only a special gun draws from the ledger");
        }

        [Test]
        public void ABreacherWithNoReserveDryFiresAndCannotReload()
        {
            var cues = new List<GunCue>();
            var g = Breacher(cues);
            for (int i = 0; i < 4; i++) FireOne(g, cues);
            Assert.That(g.Ammo, Is.EqualTo(0));
            cues.Clear();
            g.Tick(0.02f, true, true, cues);
            Assert.That(cues.Exists(c => c.kind == GunCueKind.ClipStarted && c.clip == HandlingClipKind.DryFire), Is.True, "dry fire");
            Assert.That(cues.Exists(c => c.kind == GunCueKind.Shot), Is.False);
            g.Tick(3f, false, false, cues);
            Assert.That(g.State.IsReloading, Is.False, "nothing to reload from");
            Assert.That(g.Ammo, Is.EqualTo(0));
            Assert.That(g.Reload(cues), Is.False);
        }

        [Test]
        public void BindingFeedsTheGunsReserveAndTheLedgerFollowsItsReloads()
        {
            var cues = new List<GunCue>();
            var g = Breacher(cues);
            var l = Ledger(start: 2);
            l.Bind(g);
            Assert.That(g.State.Reserve, Is.EqualTo(2));
            l.Bind(g);   // a second bind is ignored
            l.Credit(false);
            Assert.That(g.State.Reserve, Is.EqualTo(3));
            for (int i = 0; i < 3; i++) FireOne(g, cues);
            Assert.That(g.Ammo, Is.EqualTo(1));
            Assert.That(g.Reload(cues), Is.True);
            g.Tick(5f, false, false, cues);   // the partial reload ends: 3 shells leave the reserve
            Assert.That(g.Ammo, Is.EqualTo(4));
            Assert.That(g.State.Reserve, Is.EqualTo(0));
            Assert.That(l.Shots, Is.EqualTo(0), "the ledger took back what the reload left");
            l.Credit(false);
            Assert.That(g.State.Reserve, Is.EqualTo(1), "a later wreck feeds the gun again");
            l.Reset();
            Assert.That(g.State.Reserve, Is.EqualTo(2));
            l.Unbind(g);
            l.Credit(false);
            Assert.That(g.State.Reserve, Is.EqualTo(2), "an unbound gun is left alone");
        }

        [Test]
        public void ALedgerAtItsCapKeepsTheGunsReserveAtTheCap()
        {
            var g = Breacher(new List<GunCue>());
            var l = Ledger(start: 8, max: 8);
            l.Bind(g);
            Assert.That(g.State.Reserve, Is.EqualTo(8));
            l.Credit(false);
            Assert.That(g.State.Reserve, Is.EqualTo(8));
        }
    }
}
