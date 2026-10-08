using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;

namespace JamKit.Combat.Tests
{
    public class HealthTests
    {
        static DamageInfo Hit(float amount) => new DamageInfo(amount, DamageKind.Bullet, sourceId: 7);

        [Test]
        public void DamageInfoCarriesEveryField()
        {
            var d = new DamageInfo(12f, DamageKind.Explosion, 3, true, new Vector3(1, 2, 3));
            Assert.That(d.amount, Is.EqualTo(12f));
            Assert.That(d.kind, Is.EqualTo(DamageKind.Explosion));
            Assert.That(d.sourceId, Is.EqualTo(3));
            Assert.That(d.isCrit, Is.True);
            Assert.That(d.knockback, Is.EqualTo(new Vector3(1, 2, 3)));
        }

        [Test]
        public void StartsFullAndRejectsNonPositiveMax()
        {
            var h = new Health(50f);
            Assert.That(h.Current, Is.EqualTo(50f));
            Assert.That(h.Fraction01, Is.EqualTo(1f));
            Assert.That(h.IsDead, Is.False);
            Assert.Throws<System.ArgumentException>(() => new Health(0f));
        }

        [Test]
        public void DamageRemovesHealthAndRaisesDamaged()
        {
            var h = new Health(100f);
            DamageInfo seenInfo = default;
            DamageResult seenResult = default;
            int calls = 0;
            h.Damaged += (i, r) => { seenInfo = i; seenResult = r; calls++; };
            var result = h.Damage(Hit(30f));
            Assert.That(result.applied, Is.EqualTo(30f));
            Assert.That(result.killed || result.blocked, Is.False);
            Assert.That(h.Current, Is.EqualTo(70f));
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(seenInfo.sourceId, Is.EqualTo(7));
            Assert.That(seenResult.applied, Is.EqualTo(30f));
        }

        [Test]
        public void OverkillClampsAppliedAndReportsTheExcess()
        {
            var h = new Health(10f);
            int died = 0;
            h.Died += _ => died++;
            var r = h.Damage(Hit(25f));
            Assert.That(r.applied, Is.EqualTo(10f));
            Assert.That(r.overkill, Is.EqualTo(15f));
            Assert.That(r.killed, Is.True);
            Assert.That(h.Current, Is.EqualTo(0f));
            Assert.That(h.IsDead, Is.True);
            Assert.That(died, Is.EqualTo(1));
        }

        [Test]
        public void DeadEntitiesIgnoreDamageAndHealing()
        {
            var h = new Health(10f);
            h.Damage(Hit(10f));
            int events = 0;
            h.Damaged += (i, r) => events++;
            h.Died += _ => events++;
            h.Healed += _ => events++;
            var r = h.Damage(Hit(5f));
            Assert.That(r.applied, Is.EqualTo(0f));
            Assert.That(r.killed || r.blocked, Is.False);
            Assert.That(h.Heal(5f), Is.EqualTo(0f));
            Assert.That(h.Current, Is.EqualTo(0f));
            Assert.That(events, Is.EqualTo(0));
        }

        [Test]
        public void InvulnerabilityFramesBlockHitsUntilTickedAway()
        {
            var h = new Health(100f, invulnerabilitySeconds: 0.5f);
            int damaged = 0;
            h.Damaged += (i, r) => damaged++;
            h.Damage(Hit(10f));
            Assert.That(h.IsInvulnerable, Is.True);

            var blocked = h.Damage(Hit(10f));
            Assert.That(blocked.blocked, Is.True);
            Assert.That(blocked.applied, Is.EqualTo(0f));

            h.Tick(0.4f);
            Assert.That(h.Damage(Hit(10f)).blocked, Is.True);
            h.Tick(0.1f);
            Assert.That(h.IsInvulnerable, Is.False);
            Assert.That(h.Damage(Hit(10f)).applied, Is.EqualTo(10f));
            Assert.That(h.Current, Is.EqualTo(80f));
            Assert.That(damaged, Is.EqualTo(2), "blocked hits don't raise Damaged");
        }

        [Test]
        public void ZeroDamageAndKillingBlowsDontStartInvulnerability()
        {
            var h = new Health(10f, 1f);
            Assert.That(h.Damage(Hit(0f)).applied, Is.EqualTo(0f));
            Assert.That(h.IsInvulnerable, Is.False);
            h.Damage(Hit(50f));
            Assert.That(h.IsInvulnerable, Is.False);
        }

        [Test]
        public void HealClampsToMaxAndReportsWhatItRestored()
        {
            var h = new Health(100f);
            var healed = new List<float>();
            h.Healed += healed.Add;
            h.Damage(Hit(30f));
            Assert.That(h.Heal(50f), Is.EqualTo(30f));
            Assert.That(h.Current, Is.EqualTo(100f));
            Assert.That(h.Heal(10f), Is.EqualTo(0f));
            Assert.That(h.Heal(-5f), Is.EqualTo(0f));
            Assert.That(healed, Is.EqualTo(new[] { 30f }));
        }

        [Test]
        public void DamageTakenScaleMultipliesAndNeverGoesNegative()
        {
            var h = new Health(100f, 1f) { DamageTakenScale = 0.5f };
            Assert.That(h.Damage(Hit(20f)).applied, Is.EqualTo(10f));
            h.Tick(1f);
            h.DamageTakenScale = 2f;
            Assert.That(h.Damage(Hit(20f)).applied, Is.EqualTo(40f));
            h.Tick(1f);
            h.DamageTakenScale = -3f;
            Assert.That(h.DamageTakenScale, Is.EqualTo(0f));
            Assert.That(h.Damage(Hit(20f)).applied, Is.EqualTo(0f));
            Assert.That(h.IsInvulnerable, Is.False, "an immune hit isn't a hit");
            Assert.That(h.Current, Is.EqualTo(50f));
        }

        [Test]
        public void FloatDustStillKills()
        {
            var h = new Health(1f);
            for (int i = 0; i < 10; i++) h.Damage(Hit(0.1f));
            Assert.That(h.IsDead, Is.True);
            Assert.That(h.Current, Is.EqualTo(0f));
        }

        [Test]
        public void KillBypassesInvulnerabilityAndRaisesDamagedThenDied()
        {
            var h = new Health(40f, 5f);
            h.Damage(Hit(10f));
            var log = new List<string>();
            h.Damaged += (i, r) => log.Add($"damaged {r.applied} dead={h.IsDead}");
            h.Died += i => log.Add($"died {i.kind}");
            Assert.That(h.Kill(new DamageInfo(0f, DamageKind.Hazard)), Is.True);
            Assert.That(log, Is.EqualTo(new[] { "damaged 30 dead=True", "died Hazard" }));
            Assert.That(h.Kill(), Is.False);
        }

        [Test]
        public void ReviveRestoresFullHealthOnlyWhenDead()
        {
            var h = new Health(30f, 1f);
            int revived = 0;
            h.Revived += () => revived++;
            Assert.That(h.Revive(), Is.False);
            h.Damage(Hit(100f));
            Assert.That(h.Revive(), Is.True);
            Assert.That(h.IsDead, Is.False);
            Assert.That(h.Current, Is.EqualTo(30f));
            Assert.That(h.IsInvulnerable, Is.False);
            Assert.That(revived, Is.EqualTo(1));
            Assert.That(h.Damage(Hit(5f)).applied, Is.EqualTo(5f));
        }

        [Test]
        public void SetMaxClampsCurrentDownButNeverHeals()
        {
            var h = new Health(100f);
            h.Damage(Hit(20f));
            h.SetMax(150f);
            Assert.That(h.Current, Is.EqualTo(80f));
            h.SetMax(50f);
            Assert.That(h.Current, Is.EqualTo(50f));
            Assert.That(h.Fraction01, Is.EqualTo(1f));
            Assert.Throws<System.ArgumentException>(() => h.SetMax(-1f));
        }

        [Test]
        public void GrantedInvulnerabilityNeverShortensAWindow()
        {
            var h = new Health(100f, 1f);
            h.Damage(Hit(1f));
            h.GrantInvulnerability(0.2f);
            Assert.That(h.InvulnerableRemaining, Is.EqualTo(1f));
            h.GrantInvulnerability(2f);
            Assert.That(h.InvulnerableRemaining, Is.EqualTo(2f));
            h.Tick(1.9f);
            Assert.That(h.Damage(Hit(1f)).blocked, Is.True);
            h.Tick(0.2f);
            Assert.That(h.InvulnerableRemaining, Is.EqualTo(0f));
        }
    }
}
