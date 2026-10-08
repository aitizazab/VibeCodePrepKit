using System.Collections.Generic;
using JamKit.Roguelite;
using NUnit.Framework;

namespace JamKit.Tests
{
    public class UpgradeTests
    {
        static UpgradeDef Def(string id, Rarity r = Rarity.Common, int maxStacks = 1)
            => new UpgradeDef(id, r, maxStacks);

        static List<UpgradeDef> Pool(int n)
        {
            var pool = new List<UpgradeDef>();
            for (int i = 0; i < n; i++) pool.Add(Def("u" + i, (Rarity)(i % 4), 3));
            return pool;
        }

        [Test]
        public void TakeAppliesModifiersPerStackAndClearRemovesThem()
        {
            var stats = new StatBlock();
            stats.SetBase("dmg", 10f);
            var inv = new UpgradeInventory(stats);
            var sharp = Def("sharp", Rarity.Common, 2);
            sharp.modifiers.Add(new StatModifier("dmg", StatOp.Flat, 5f));
            sharp.effects.Add("pierce");

            Assert.That(inv.Take(sharp), Is.True);
            Assert.That(inv.Take(sharp), Is.True);
            Assert.That(inv.Take(sharp), Is.False, "max stacks");
            Assert.That(inv.Count("sharp"), Is.EqualTo(2));
            Assert.That(stats.Get("dmg"), Is.EqualTo(20f));
            Assert.That(inv.EffectStacks("pierce"), Is.EqualTo(2));
            Assert.That(sharp.modifiers[0].sourceId, Is.Null, "the def's own data is untouched");

            stats.RemoveBySource(UpgradeInventory.SourceId("sharp", 2));
            Assert.That(stats.Get("dmg"), Is.EqualTo(15f), "one stack comes off alone");

            inv.Clear();
            Assert.That(inv.Has("sharp"), Is.False);
            Assert.That(inv.HasEffect("pierce"), Is.False);
            Assert.That(stats.Get("dmg"), Is.EqualTo(10f));
        }

        [Test]
        public void InventoryEnforcesRequiresAndExcludesBothWays()
        {
            var inv = new UpgradeInventory();
            var fire = Def("fire");
            var ice = Def("ice");
            fire.excludes.Add("ice"); // listed on one side only
            var inferno = Def("inferno");
            inferno.requires.Add("fire");

            Assert.That(inv.CanTake(inferno), Is.False);
            Assert.That(inv.Take(fire), Is.True);
            Assert.That(inv.CanTake(inferno), Is.True);
            Assert.That(inv.CanTake(ice), Is.False, "fire excludes ice, so ice excludes fire");

            var inv2 = new UpgradeInventory();
            inv2.Take(ice);
            Assert.That(inv2.CanTake(fire), Is.False);
        }

        [Test]
        public void OfferIsDistinctEligibleAndRespectsStacks()
        {
            var inv = new UpgradeInventory();
            var once = Def("once");
            var needsX = Def("needsX"); needsX.requires.Add("x");
            var blocked = Def("blocked"); blocked.excludes.Add("once");
            var open1 = Def("open1", Rarity.Common, 0);
            var open2 = Def("open2", Rarity.Rare, 0);
            inv.Take(once);
            var pool = new List<UpgradeDef> { once, needsX, blocked, open1, open2, open1 };
            var draft = new UpgradeDraft();
            var results = new List<UpgradeDef>();
            for (ulong seed = 0; seed < 50; seed++)
            {
                int found = draft.Offer(pool, inv, 3, new Rng(seed), results);
                Assert.That(found, Is.EqualTo(2));
                Assert.That(results, Is.EquivalentTo(new[] { open1, open2 }));
            }
        }

        [Test]
        public void OfferNeverDuplicatesAcrossManySeeds()
        {
            var pool = Pool(12);
            var draft = new UpgradeDraft();
            var results = new List<UpgradeDef>();
            for (ulong seed = 0; seed < 200; seed++)
            {
                draft.Offer(pool, new UpgradeInventory(), 4, new Rng(seed), results);
                Assert.That(results.Count, Is.EqualTo(4));
                Assert.That(results, Is.Unique);
            }
        }

        [Test]
        public void OfferIsDeterministicPerSeed()
        {
            var pool = Pool(20);
            var a = new List<UpgradeDef>();
            var b = new List<UpgradeDef>();
            new UpgradeDraft().Offer(pool, new UpgradeInventory(), 3, new Rng(1234), a, 0.5f);
            new UpgradeDraft().Offer(pool, new UpgradeInventory(), 3, new Rng(1234), b, 0.5f);
            Assert.That(a, Is.EqualTo(b));
        }

        [Test]
        public void LuckShiftsTowardHigherRarities()
        {
            var pool = new List<UpgradeDef>();
            for (int i = 0; i < 4; i++)
            {
                pool.Add(Def("c" + i, Rarity.Common, 0));
                pool.Add(Def("l" + i, Rarity.Legendary, 0));
            }
            int Legendaries(float luck)
            {
                var draft = new UpgradeDraft();
                var rng = new Rng(99);
                var results = new List<UpgradeDef>();
                int n = 0;
                for (int i = 0; i < 2000; i++)
                {
                    draft.Offer(pool, null, 1, rng, results, luck);
                    if (results[0].rarity == Rarity.Legendary) n++;
                }
                return n;
            }
            int none = Legendaries(0f), lucky = Legendaries(3f);
            // weights 100 vs 3 -> ~2.9%; luck 3 -> 3 * 10 = 30 vs 100 -> ~23%
            Assert.That(none, Is.LessThan(120));
            Assert.That(lucky, Is.GreaterThan(none * 4));
        }

        [Test]
        public void ExplicitWeightOverridesRarity()
        {
            var draft = new UpgradeDraft();
            var heavy = Def("heavy", Rarity.Legendary);
            heavy.weight = 1000f;
            Assert.That(draft.WeightOf(heavy), Is.EqualTo(1000f));
            Assert.That(draft.WeightOf(Def("plain", Rarity.Rare)), Is.EqualTo(draft.GetRarityWeight(Rarity.Rare)));
        }

        [Test]
        public void FallbackFillsOnceWhenPoolRunsDry()
        {
            var gold = Def("gold", Rarity.Common, 0);
            var draft = new UpgradeDraft(gold);
            var only = Def("only");
            var results = new List<UpgradeDef>();
            int found = draft.Offer(new List<UpgradeDef> { only }, new UpgradeInventory(), 3, new Rng(5), results);
            Assert.That(found, Is.EqualTo(1));
            Assert.That(results, Is.EqualTo(new[] { only, gold }));

            var inv = new UpgradeInventory();
            inv.Take(only);
            draft.Offer(new List<UpgradeDef> { only }, inv, 3, new Rng(5), results);
            Assert.That(results, Is.EqualTo(new[] { gold }));
        }

        [Test]
        public void RerollAvoidsLastOfferWhenItCan()
        {
            var pool = Pool(6);
            var draft = new UpgradeDraft();
            var first = new List<UpgradeDef>();
            var second = new List<UpgradeDef>();
            var rng = new Rng(77);
            draft.Offer(pool, new UpgradeInventory(), 3, rng, first);
            draft.Reroll(pool, new UpgradeInventory(), 3, rng, second);
            Assert.That(second.Count, Is.EqualTo(3));
            foreach (var d in second) Assert.That(first.Contains(d), Is.False, d.id);

            // only 4 in the pool: a reroll of 3 must bring back some of the last ones, still distinct
            var small = Pool(4);
            draft.Offer(small, new UpgradeInventory(), 3, rng, first);
            draft.Reroll(small, new UpgradeInventory(), 3, rng, second);
            Assert.That(second.Count, Is.EqualTo(3));
            Assert.That(second, Is.Unique);
        }
    }
}
