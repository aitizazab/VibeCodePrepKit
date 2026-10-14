using JamKit.Skyline;
using NUnit.Framework;
using UnityEngine;

namespace JamKit.SkylineRepo.Tests
{
    /// <summary>
    /// EditMode checks for special ammo in play (plan task A2; docs/SKYLINE-HUD.md "Ammo") and for what glass does to shots
    /// (D21). The Unity lifecycle (Awake, OnEnable) is not relied on: the component builds its ledger on first use, and the
    /// tests call <see cref="SkylineAmmo.Poll"/> and <see cref="SkylineAmmo.SyncLoadout"/> themselves.
    /// </summary>
    public class SkylineAmmoTests
    {
        GameObject _go;
        SkylineAmmo _ammo;
        readonly System.Collections.Generic.List<GameObject> _made = new System.Collections.Generic.List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("AmmoTest");
            _ammo = _go.AddComponent<SkylineAmmo>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var g in _made) if (g != null) Object.DestroyImmediate(g);
            _made.Clear();
            Object.DestroyImmediate(_go);
        }

        SkylineWreck NewWreck(float wreckedAt)
        {
            var g = new GameObject("TestWreck");
            _made.Add(g);
            var w = g.AddComponent<SkylineWreck>();
            w.Data = new Wreck(SkylineEnemies.MallCopDrone(), wreckedAt);
            return w;
        }

        static SpecialAmmoLedger EmptyLedger() => new SpecialAmmoLedger(new SpecialAmmoDef { start = 0 });

        // ---------------------------------------------------------------- the claims

        [Test]
        public void AKillByTheWritPaysOneShot()
        {
            var ledger = EmptyLedger();
            var claims = new SkylineAmmoClaims(ledger);
            var key = new object();
            Assert.That(claims.Add(key, SkylineGuns.Writ()), Is.True);
            Assert.That(ledger.Shots, Is.EqualTo(0), "nothing is paid before the wreck is stripped");
            Assert.That(claims.Collect(key, out string gun, out bool special), Is.EqualTo(1));
            Assert.That(gun, Is.EqualTo(SkylineGuns.WritId));
            Assert.That(special, Is.False);
            Assert.That(ledger.Shots, Is.EqualTo(1));
        }

        [Test]
        public void EveryThirdKillByTheBreacherPaysOneShot()
        {
            var ledger = EmptyLedger();
            var claims = new SkylineAmmoClaims(ledger);
            var breacher = SkylineGuns.Breacher();
            var paid = new int[6];
            for (int i = 0; i < paid.Length; i++)
            {
                var key = new object();
                claims.Add(key, breacher);
                paid[i] = claims.Collect(key, out _, out bool special);
                Assert.That(special, Is.True);
            }
            Assert.That(paid, Is.EqualTo(new[] { 0, 0, 1, 0, 0, 1 }));
            Assert.That(ledger.Shots, Is.EqualTo(2));
        }

        [Test]
        public void AClaimPaysOnlyOnce()
        {
            var ledger = EmptyLedger();
            var claims = new SkylineAmmoClaims(ledger);
            var key = new object();
            claims.Add(key, SkylineGuns.Writ());
            Assert.That(claims.Collect(key, out _, out _), Is.EqualTo(1));
            Assert.That(claims.Collect(key, out _, out _), Is.EqualTo(-1), "no claim is left");
            Assert.That(ledger.Shots, Is.EqualTo(1));
        }

        [Test]
        public void ThereIsOneClaimPerWreckAndNoneForAGunlessKill()
        {
            var claims = new SkylineAmmoClaims(EmptyLedger());
            var key = new object();
            Assert.That(claims.Add(key, SkylineGuns.Writ()), Is.True);
            Assert.That(claims.Add(key, SkylineGuns.Breacher()), Is.False, "the same wreck cannot carry two");
            Assert.That(claims.Add(new object(), null), Is.False, "a kill by no gun leaves no ammo");
            Assert.That(claims.Add(null, SkylineGuns.Writ()), Is.False);
            Assert.That(claims.Count, Is.EqualTo(1));
        }

        [Test]
        public void ForgetAndClearDropClaimsUnpaid()
        {
            var ledger = EmptyLedger();
            var claims = new SkylineAmmoClaims(ledger);
            var a = new object();
            claims.Add(a, SkylineGuns.Writ());
            claims.Add(new object(), SkylineGuns.Writ());
            claims.Forget(a);
            Assert.That(claims.Has(a), Is.False);
            Assert.That(claims.Count, Is.EqualTo(1));
            claims.Clear();
            Assert.That(claims.Count, Is.EqualTo(0));
            Assert.That(ledger.Shots, Is.EqualTo(0));
        }

        [Test]
        public void TheReserveIsCappedAndTheExcessIsLost()
        {
            var ledger = new SpecialAmmoLedger(new SpecialAmmoDef { start = 0, max = 2 });
            var claims = new SkylineAmmoClaims(ledger);
            for (int i = 0; i < 4; i++)
            {
                var key = new object();
                claims.Add(key, SkylineGuns.Writ());
                claims.Collect(key, out _, out _);
            }
            Assert.That(ledger.Shots, Is.EqualTo(2));
        }

        // ---------------------------------------------------------------- the component

        [Test]
        public void AStrippedWreckPaysItsClaimOnceAndRaisesTheEvent()
        {
            var wreck = NewWreck(100f);
            int before = _ammo.Ledger.Shots, raised = 0, last = 0;
            System.Action<int> on = n => { raised++; last = n; };
            SkylineAmmo.SpecialGained += on;
            try
            {
                Assert.That(_ammo.RegisterKill(wreck, SkylineGuns.Writ()), Is.True);
                _ammo.Poll(101f);
                Assert.That(_ammo.Ledger.Shots, Is.EqualTo(before), "an unstripped wreck pays nothing yet");
                wreck.Data.stripped = true;
                _ammo.Poll(102f);
                Assert.That(_ammo.Ledger.Shots, Is.EqualTo(before + 1));
                Assert.That(raised, Is.EqualTo(1));
                Assert.That(last, Is.EqualTo(1));
                _ammo.Poll(103f);
                Assert.That(_ammo.Ledger.Shots, Is.EqualTo(before + 1), "a stripped wreck cannot give twice");
                Assert.That(raised, Is.EqualTo(1));
            }
            finally { SkylineAmmo.SpecialGained -= on; }
        }

        [Test]
        public void ASpecialKillThatDoesNotCompleteAThreePaysNoShotAndRaisesNoEvent()
        {
            int raised = 0;
            System.Action<int> on = n => raised++;
            SkylineAmmo.SpecialGained += on;
            try
            {
                int before = _ammo.Ledger.Shots;
                var wreck = NewWreck(100f);
                _ammo.RegisterKill(wreck, SkylineGuns.Breacher());
                wreck.Data.stripped = true;
                _ammo.Poll(101f);
                Assert.That(_ammo.Ledger.Shots, Is.EqualTo(before));
                Assert.That(_ammo.Ledger.KillProgress, Is.EqualTo(1));
                Assert.That(raised, Is.EqualTo(0));
            }
            finally { SkylineAmmo.SpecialGained -= on; }
        }

        [Test]
        public void AWreckThatGoesStaleUnstrippedPaysNothing()
        {
            var wreck = NewWreck(100f);
            int before = _ammo.Ledger.Shots;
            _ammo.RegisterKill(wreck, SkylineGuns.Writ());
            _ammo.Poll(100f + wreck.Data.freshSeconds + 1f);
            Assert.That(_ammo.Claims.Count, Is.EqualTo(0), "the claim lapses with the strip window");
            Assert.That(_ammo.Ledger.Shots, Is.EqualTo(before));
        }

        [Test]
        public void ADestroyedWreckLosesItsClaim()
        {
            var wreck = NewWreck(100f);
            int before = _ammo.Ledger.Shots;
            _ammo.RegisterKill(wreck, SkylineGuns.Writ());
            Object.DestroyImmediate(wreck.gameObject);
            _ammo.Poll(101f);
            Assert.That(_ammo.Claims.Count, Is.EqualTo(0));
            Assert.That(_ammo.Ledger.Shots, Is.EqualTo(before));
        }

        [Test]
        public void ResetRunRestoresTheStartingShotsAndDropsClaims()
        {
            var wreck = NewWreck(100f);
            _ammo.RegisterKill(wreck, SkylineGuns.Writ());
            wreck.Data.stripped = true;
            _ammo.Poll(101f);
            Assert.That(_ammo.Ledger.Shots, Is.EqualTo(_ammo.Ledger.Def.start + 1));
            _ammo.RegisterKill(NewWreck(100f), SkylineGuns.Writ());
            _ammo.ResetRun();
            Assert.That(_ammo.Ledger.Shots, Is.EqualTo(_ammo.Ledger.Def.start));
            Assert.That(_ammo.Claims.Count, Is.EqualTo(0));
        }

        [Test]
        public void TheBreachersReserveFollowsTheLedgerAndSurvivesARestart()
        {
            var first = new Loadout(new SkylineGun(SkylineGuns.Writ()), new SkylineGun(SkylineGuns.Breacher()), null);
            _ammo.SyncLoadout(first);
            Assert.That(first[1].State.Reserve, Is.EqualTo(_ammo.Ledger.Shots));
            _ammo.Ledger.Credit(false);
            Assert.That(first[1].State.Reserve, Is.EqualTo(_ammo.Ledger.Shots));

            // the player restarts: a new loadout with a fresh Breacher (empty reserve) is bound, the old one let go
            var second = new Loadout(new SkylineGun(SkylineGuns.Writ()), new SkylineGun(SkylineGuns.Breacher()), null);
            int shots = _ammo.Ledger.Shots;
            _ammo.SyncLoadout(second);
            Assert.That(second[1].State.Reserve, Is.EqualTo(shots));
            _ammo.Ledger.Credit(false);
            Assert.That(second[1].State.Reserve, Is.EqualTo(shots + 1));
            Assert.That(first[1].State.Reserve, Is.EqualTo(shots), "the old gun is no longer fed");
        }

        // ---------------------------------------------------------------- glass and shots (D21)

        [Test]
        public void OnlyFacadePanesAreShotOut()
        {
            // D21: a shot breaks and passes façade panes (SkylineFacade.IsPane); every other glass stops it
            foreach (var pair in new[] { ("facade-pane-0-3", true), ("bridge-rail-w", false), ("bridge-floor", false), ("skylight", false), ("facade-post-n", false) })
            {
                var g = new GameObject(pair.Item1);
                _made.Add(g);
                var c = g.AddComponent<BoxCollider>();
                Assert.That(SkylineFacade.IsPane(c), Is.EqualTo(pair.Item2), pair.Item1);
            }
            Assert.That(SkylineFacade.IsPane(null), Is.False);
        }

        [Test]
        public void SightAndAimRaysSkipTheGlassLayerAndShotsDoNot()
        {
            int ignore = 1 << 2;
            Assert.That(SkylineShooting.Mask & ignore, Is.EqualTo(0), "the player's own body is never hit");
            Assert.That(SkylineShooting.ShotMask & ignore, Is.EqualTo(0));
            int glass = LayerMask.NameToLayer(SkylineBot.GlassLayerName);
            if (glass < 0) Assert.Ignore("the project has no " + SkylineBot.GlassLayerName + " layer until the floor is first built");
            Assert.That(SkylineShooting.Mask & (1 << glass), Is.EqualTo(0));
            Assert.That(SkylineShooting.ShotMask & (1 << glass), Is.Not.EqualTo(0));
            Assert.That(SkylineShooting.Mask & 1, Is.EqualTo(1), "the default layer is still seen");
        }
    }
}
