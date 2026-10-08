using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace JamKit.Combat.Tests
{
    public class WeaponStateTests
    {
        static WeaponDef Def(FireMode mode, int mag = 100, int reserve = 100, float interval = 0.1f)
        {
            return new WeaponDef
            {
                fireMode = mode,
                magazineSize = mag,
                startingReserve = reserve,
                fireInterval = interval,
                burstCount = 3,
                burstInterval = 0.05f,
                reloadTime = 1f,
                autoReloadOnEmpty = true,
            };
        }

        /// <summary>Runs a weapon at a fixed frame rate with the trigger held from the first frame,
        /// and returns the absolute time of every shot.</summary>
        static List<double> ShotTimes(WeaponDef def, int fps, float seconds, bool holdTrigger = true)
        {
            var w = new WeaponState(def);
            var ages = new List<float>();
            var times = new List<double>();
            float dt = 1f / fps;
            double clock = 0;
            int frames = (int)Math.Round(seconds * fps);
            for (int i = 0; i < frames; i++)
            {
                clock += dt;
                bool held = holdTrigger || i == 0;
                w.Tick(dt, held, i == 0, ages);
                foreach (float age in ages) times.Add(clock - age);
            }
            return times;
        }

        [Test]
        public void StartsWithAFullMagazineAndTheStartingReserve()
        {
            var w = new WeaponState(Def(FireMode.Semi, mag: 12, reserve: 48));
            Assert.That(w.AmmoInMagazine, Is.EqualTo(12));
            Assert.That(w.Reserve, Is.EqualTo(48));
            Assert.That(w.IsReloading, Is.False);
            Assert.Throws<ArgumentNullException>(() => new WeaponState(null));
        }

        [Test]
        public void SemiFiresOncePerPressAndDropsPressesDuringTheInterval()
        {
            var w = new WeaponState(Def(FireMode.Semi, interval: 0.2f));
            Assert.That(w.Tick(0.01f, true, true), Is.EqualTo(1));
            Assert.That(w.Tick(0.5f, true, false), Is.EqualTo(0), "holding doesn't refire");
            Assert.That(w.Tick(0.01f, false), Is.EqualTo(0));
            Assert.That(w.Tick(0.01f, true), Is.EqualTo(1), "a new press after the interval fires");
            Assert.That(w.Tick(0.05f, false), Is.EqualTo(0));
            Assert.That(w.Tick(0.05f, true), Is.EqualTo(0), "too soon: dropped, not queued");
            Assert.That(w.Tick(0.2f, true), Is.EqualTo(0));
            Assert.That(w.AmmoInMagazine, Is.EqualTo(98));
        }

        [Test]
        public void AutoFireIsFrameRateIndependent()
        {
            var def = Def(FireMode.Auto, interval: 0.1f);
            var slow = ShotTimes(def, 30, 1f);
            var fast = ShotTimes(def, 240, 1f);
            Assert.That(slow.Count, Is.EqualTo(10));
            Assert.That(fast.Count, Is.EqualTo(10));
            for (int i = 1; i < 10; i++)
            {
                Assert.That(slow[i] - slow[i - 1], Is.EqualTo(0.1).Within(1e-5));
                Assert.That(fast[i] - fast[i - 1], Is.EqualTo(0.1).Within(1e-5));
            }
        }

        [Test]
        public void ALongFrameFiresEveryShotItCoveredWithTheirAges()
        {
            var w = new WeaponState(Def(FireMode.Auto, interval: 0.1f));
            var ages = new List<float>();
            Assert.That(w.Tick(0.01f, true, true, ages), Is.EqualTo(1));
            Assert.That(ages[0], Is.EqualTo(0f), "the first shot fires on the press");
            Assert.That(w.Tick(0.35f, true, false, ages), Is.EqualTo(3));
            Assert.That(ages[0], Is.EqualTo(0.25f).Within(1e-5));
            Assert.That(ages[1], Is.EqualTo(0.15f).Within(1e-5));
            Assert.That(ages[2], Is.EqualTo(0.05f).Within(1e-5));
        }

        [Test]
        public void AutoDoesNotBankShotsWhileTheTriggerIsUp()
        {
            var w = new WeaponState(Def(FireMode.Auto, interval: 0.1f));
            w.Tick(5f, false);
            Assert.That(w.Tick(0.05f, true), Is.EqualTo(1));
            Assert.That(w.Tick(0.04f, true), Is.EqualTo(0));
            Assert.That(w.Tick(0.07f, true), Is.EqualTo(1));
        }

        [Test]
        public void AutoTapWithinOneFrameFiresOnce()
        {
            var w = new WeaponState(Def(FireMode.Auto));
            Assert.That(w.Tick(0.016f, false, true), Is.EqualTo(1));
            Assert.That(w.Tick(1f, false), Is.EqualTo(0));
        }

        [Test]
        public void BurstFiresItsCountAtTheBurstIntervalEvenIfReleased()
        {
            var def = Def(FireMode.Burst, interval: 0.3f);
            var w = new WeaponState(def);
            var times = new List<double>();
            var ages = new List<float>();
            double clock = 0;
            for (int i = 0; i < 100; i++)
            {
                clock += 0.01;
                w.Tick(0.01f, i == 0, i == 0, ages);
                foreach (float a in ages) times.Add(clock - a);
            }
            Assert.That(times.Count, Is.EqualTo(3));
            Assert.That(times[1] - times[0], Is.EqualTo(0.05).Within(1e-5));
            Assert.That(times[2] - times[1], Is.EqualTo(0.05).Within(1e-5));
            Assert.That(w.IsBursting, Is.False);
        }

        [Test]
        public void HoldingBurstDoesNotStartAnotherBurstAndPressesMidBurstAreIgnored()
        {
            var w = new WeaponState(Def(FireMode.Burst, interval: 0.3f));
            int shots = w.Tick(0.01f, true, true);
            shots += w.Tick(0.03f, true, true); // pressed again mid-burst
            for (int i = 0; i < 100; i++) shots += w.Tick(0.01f, true);
            Assert.That(shots, Is.EqualTo(3));
        }

        [Test]
        public void NextBurstWaitsFireIntervalAfterTheLastShot()
        {
            var w = new WeaponState(Def(FireMode.Burst, interval: 0.3f));
            w.Tick(0.01f, true, true);       // shot at 0.01, then 0.06 and 0.11
            w.Tick(0.15f, false);            // t = 0.16, burst done
            Assert.That(w.Tick(0.2f, true, true), Is.EqualTo(0), "0.36 < 0.11 + 0.3");
            Assert.That(w.Tick(0.1f, true, true), Is.EqualTo(1), "0.46 is past 0.41");
        }

        [Test]
        public void BurstIsFrameRateIndependent()
        {
            var def = Def(FireMode.Burst, interval: 0.3f);
            var slow = ShotTimes(def, 30, 2f);
            var fast = ShotTimes(def, 240, 2f);
            Assert.That(slow.Count, Is.EqualTo(fast.Count));
            Assert.That(slow.Count, Is.EqualTo(3), "one press, one burst");
            for (int i = 1; i < slow.Count; i++)
                Assert.That(slow[i] - slow[i - 1], Is.EqualTo(fast[i] - fast[i - 1]).Within(1e-5));
        }

        [Test]
        public void EmptyMagazineAutoReloadsFromReserve()
        {
            var def = Def(FireMode.Auto, mag: 5, reserve: 7, interval: 0.1f);
            var w = new WeaponState(def);
            var log = new List<string>();
            w.ReloadStarted += () => log.Add("start");
            w.ReloadCompleted += () => log.Add("done");
            int shots = 0;
            for (int i = 0; i < 50; i++) shots += w.Tick(0.01f, true);  // 0.5 s: 5 shots, reload begins at 0.41
            Assert.That(shots, Is.EqualTo(5));
            Assert.That(w.IsReloading, Is.True);
            Assert.That(w.AmmoInMagazine, Is.EqualTo(0));
            Assert.That(w.ReloadProgress01, Is.EqualTo(0.1f).Within(0.02f));
            for (int i = 0; i < 90; i++) shots += w.Tick(0.01f, true);  // to 1.40: reload done at 1.41
            Assert.That(shots, Is.EqualTo(5));
            shots += w.Tick(0.01f, true);
            Assert.That(shots, Is.EqualTo(6), "held trigger resumes the moment the reload ends");
            Assert.That(w.AmmoInMagazine, Is.EqualTo(4));
            Assert.That(w.Reserve, Is.EqualTo(2));
            Assert.That(log, Is.EqualTo(new[] { "start", "done" }));
        }

        [Test]
        public void ReloadFinishingMidTickLetsHeldFireStartOnTheExactBeat()
        {
            var def = Def(FireMode.Auto, mag: 1, reserve: 10, interval: 0.1f);
            def.reloadTime = 0.5f;
            var w = new WeaponState(def);
            var ages = new List<float>();
            w.Tick(0.1f, true, true, ages);   // shot at 0.1, reload 0.1 -> 0.6
            Assert.That(w.Tick(0.6f, true, false, ages), Is.EqualTo(1));
            Assert.That(ages[0], Is.EqualTo(0.1f).Within(1e-5), "fired at 0.6, tick ended at 0.7");
        }

        [Test]
        public void PartialReserveGivesAPartialMagazineThenRunsDry()
        {
            var def = Def(FireMode.Semi, mag: 6, reserve: 2, interval: 0.01f);
            def.reloadTime = 0f;
            var w = new WeaponState(def);
            int dry = 0;
            w.DryFired += () => dry++;
            int shots = 0;
            for (int i = 0; i < 20; i++) { shots += w.Tick(0.05f, true, true); w.Tick(0.05f, false); }
            Assert.That(shots, Is.EqualTo(8));
            Assert.That(w.AmmoInMagazine + w.Reserve, Is.EqualTo(0));
            Assert.That(dry, Is.EqualTo(12));
        }

        [Test]
        public void WithoutAutoReloadAnEmptyGunDryFiresUntilReloadedByHand()
        {
            var def = Def(FireMode.Semi, mag: 1, reserve: 5);
            def.autoReloadOnEmpty = false;
            def.reloadTime = 0.5f;
            var w = new WeaponState(def);
            int dry = 0;
            w.DryFired += () => dry++;
            Assert.That(w.Tick(0.2f, true, true), Is.EqualTo(1));
            Assert.That(w.Tick(0.2f, true, true), Is.EqualTo(0));
            Assert.That(dry, Is.EqualTo(1));
            Assert.That(w.IsReloading, Is.False);
            Assert.That(w.StartReload(), Is.True);
            Assert.That(w.Tick(0.3f, true, true), Is.EqualTo(0), "can't fire while reloading");
            Assert.That(dry, Is.EqualTo(1), "no empty click during a reload");
            w.Tick(0.2f, false);
            Assert.That(w.AmmoInMagazine, Is.EqualTo(1));
            Assert.That(w.Tick(0.2f, true, true), Is.EqualTo(1));
        }

        [Test]
        public void ReloadIsRefusedWhenFullOrOutOfReserve()
        {
            var w = new WeaponState(Def(FireMode.Semi, mag: 5, reserve: 0));
            Assert.That(w.StartReload(), Is.False, "full");
            w.Tick(0.2f, true, true);
            Assert.That(w.StartReload(), Is.False, "no reserve");
            var noMag = new WeaponState(Def(FireMode.Semi, mag: 0, reserve: 5));
            Assert.That(noMag.StartReload(), Is.False, "nothing to reload");
        }

        [Test]
        public void CancelReloadMovesNoAmmo()
        {
            var def = Def(FireMode.Semi, mag: 5, reserve: 10);
            def.autoReloadOnEmpty = false;
            var w = new WeaponState(def);
            int canceled = 0;
            w.ReloadCanceled += () => canceled++;
            w.Tick(0.2f, true, true);
            w.StartReload();
            w.Tick(0.9f, false);
            Assert.That(w.CancelReload(), Is.True);
            Assert.That(w.CancelReload(), Is.False);
            w.Tick(1f, false);
            Assert.That(w.AmmoInMagazine, Is.EqualTo(4));
            Assert.That(w.Reserve, Is.EqualTo(10));
            Assert.That(canceled, Is.EqualTo(1));
            Assert.That(w.Tick(0.1f, true, true), Is.EqualTo(1), "can fire the rest of the magazine");
        }

        [Test]
        public void ManualReloadCancelsABurst()
        {
            var w = new WeaponState(Def(FireMode.Burst, mag: 10, reserve: 10, interval: 0.3f));
            Assert.That(w.Tick(0.01f, true, true), Is.EqualTo(1));
            Assert.That(w.StartReload(), Is.True);
            Assert.That(w.IsBursting, Is.False);
            w.Tick(1.5f, false);
            Assert.That(w.AmmoInMagazine, Is.EqualTo(10));
            Assert.That(w.Reserve, Is.EqualTo(9));
        }

        [Test]
        public void BurstEndsWhenTheMagazineRunsDry()
        {
            var w = new WeaponState(Def(FireMode.Burst, mag: 2, reserve: 10, interval: 0.3f));
            int shots = 0;
            for (int i = 0; i < 20; i++) shots += w.Tick(0.01f, i == 0, i == 0);
            Assert.That(shots, Is.EqualTo(2));
            Assert.That(w.IsReloading, Is.True);
            for (int i = 0; i < 200; i++) shots += w.Tick(0.01f, false);
            Assert.That(shots, Is.EqualTo(2), "the cut-off burst doesn't resume after the reload");
        }

        [Test]
        public void MagazinelessGunsDrawFromReserveAndPickupsRefillThem()
        {
            var def = Def(FireMode.Auto, mag: 0, reserve: 3, interval: 0.1f);
            def.maxReserve = 5;
            var w = new WeaponState(def);
            int dry = 0;
            w.DryFired += () => dry++;
            int shots = 0;
            for (int i = 0; i < 100; i++) shots += w.Tick(0.01f, true);
            Assert.That(shots, Is.EqualTo(3));
            Assert.That(w.IsReloading, Is.False);
            Assert.That(w.Tick(0.01f, true, true), Is.EqualTo(0));
            Assert.That(dry, Is.EqualTo(1));
            Assert.That(w.AddReserve(10), Is.EqualTo(5), "capped at maxReserve");
            Assert.That(w.AddReserve(1), Is.EqualTo(0));
            Assert.That(w.Tick(0.01f, true), Is.EqualTo(1), "held fire resumes on pickup");
        }

        [Test]
        public void InfiniteReserveNeverRunsOut()
        {
            var def = Def(FireMode.Auto, mag: 0, reserve: 0, interval: 0.05f);
            def.infiniteReserve = true;
            var w = new WeaponState(def);
            int shots = 0;
            for (int i = 0; i < 600; i++) shots += w.Tick(1f / 60f, true);
            Assert.That(shots, Is.EqualTo(200));
            Assert.That(w.AddReserve(10), Is.EqualTo(0));

            var magDef = Def(FireMode.Semi, mag: 2, reserve: 0);
            magDef.infiniteReserve = true;
            magDef.reloadTime = 0f;
            var m = new WeaponState(magDef);
            for (int i = 0; i < 10; i++) { m.Tick(0.2f, true, true); m.Tick(0.2f, false); }
            Assert.That(m.AmmoInMagazine, Is.EqualTo(2), "reloads always fill");
        }

        [Test]
        public void AmmoPerShotMustBeAffordable()
        {
            var def = Def(FireMode.Semi, mag: 0, reserve: 50);
            def.ammoPerShot = 40;
            var w = new WeaponState(def);
            Assert.That(w.Tick(0.2f, true, true), Is.EqualTo(1));
            Assert.That(w.Reserve, Is.EqualTo(10));
            Assert.That(w.HasAmmoForShot, Is.False);
            Assert.That(w.Tick(0.2f, true, true), Is.EqualTo(0));
        }

        [Test]
        public void ZeroIntervalsCannotHangTheFireLoop()
        {
            var def = Def(FireMode.Auto, mag: 0, reserve: 0, interval: 0f);
            def.infiniteReserve = true;
            var w = new WeaponState(def);
            w.Tick(0.01f, true, true);
            int shots = w.Tick(0.1f, true);
            Assert.That(shots, Is.InRange(90, 110));
        }

        [Test]
        public void FiredRaisesOncePerShot()
        {
            var w = new WeaponState(Def(FireMode.Auto, interval: 0.1f));
            int fired = 0;
            w.Fired += () => fired++;
            int shots = w.Tick(0.01f, true) + w.Tick(0.5f, true);
            Assert.That(fired, Is.EqualTo(shots));
            Assert.That(shots, Is.EqualTo(6));
        }
    }
}
