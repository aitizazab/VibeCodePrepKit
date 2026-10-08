using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using JamKit.BulletHell;
using JamKit.Dialogue;
using JamKit.Patterns;
using JamKit.Roguelite;
using NUnit.Framework;

namespace JamKit.Tests.BulletHell
{
    /// <summary>The round-2 roguelite rules (docs/DESIGN-ROGUELITE.md): where power comes from, trials and
    /// wagers, the forge and altar, remnants, the Bomb Bay, the boosts and the companion.</summary>
    public class RewardEconomyTests
    {
        static UpgradeDef U(string id, Rarity r, int stacks = 1, params string[] tags)
        {
            var d = new UpgradeDef(id, r, stacks) { displayName = id };
            d.modifiers.Add(new StatModifier(ShmupStats.Damage, StatOp.PercentAdd, 0.1f));
            d.tags.AddRange(tags);
            return d;
        }

        static List<UpgradeDef> Pool()
        {
            var pool = new List<UpgradeDef>();
            for (int i = 0; i < 6; i++) pool.Add(U("c" + i, Rarity.Common));
            for (int i = 0; i < 4; i++) pool.Add(U("r" + i, Rarity.Rare));
            for (int i = 0; i < 4; i++) pool.Add(U("e" + i, Rarity.Epic));
            pool.Add(U("l0", Rarity.Legendary));
            return pool;
        }

        [Test]
        public void BattleScreensOfferCommonsAndRaresOnly()
        {
            var draw = new RewardDraw();
            var table = new RewardTable(100f, 35f, 0f, 0f, 0f);
            var cards = new List<UpgradeDef>();
            var rng = new Rng(3);
            for (int i = 0; i < 200; i++)
            {
                Assert.That(draw.Offer(table, Pool(), new UpgradeInventory(), 3, rng, cards, 2f), Is.EqualTo(3));
                Assert.That(cards.All(c => c.rarity <= Rarity.Rare), Is.True, "no epic without the epic chance, even with luck");
                Assert.That(cards.Select(c => c.id).Distinct().Count(), Is.EqualTo(3), "distinct");
            }
        }

        [Test]
        public void TheSmallEpicChanceSwapsInOneEpic()
        {
            var draw = new RewardDraw();
            var always = new RewardTable(100f, 35f, 0f, 0f, 1f);
            var cards = new List<UpgradeDef>();
            draw.Offer(always, Pool(), new UpgradeInventory(), 3, new Rng(1), cards, 0f);
            Assert.That(cards.Count(c => c.rarity == Rarity.Epic), Is.EqualTo(1), "exactly one epic");

            var small = new RewardTable(100f, 35f, 0f, 0f, 0.05f);
            int epics = 0, screens = 2000;
            var rng = new Rng(9);
            for (int i = 0; i < screens; i++)
            {
                draw.Offer(small, Pool(), new UpgradeInventory(), 3, rng, cards, 0f);
                if (cards.Any(c => c.rarity == Rarity.Epic)) epics++;
            }
            Assert.That(epics / (float)screens, Is.InRange(0.03f, 0.08f), "about 5% of screens");
        }

        [Test]
        public void BossScreensAreEpicsAndTopUpFromRaresWhenThePoolRunsDry()
        {
            var draw = new RewardDraw(U("fallback", Rarity.Common, 0));
            var boss = new ShmupRewards().boss;
            var cards = new List<UpgradeDef>();
            var inv = new UpgradeInventory();
            draw.Offer(boss, Pool(), inv, 3, new Rng(5), cards, 0f);
            Assert.That(cards.Count, Is.EqualTo(3));
            Assert.That(cards.All(c => c.rarity >= Rarity.Epic), Is.True, "a guaranteed choice of epics");

            // Own every epic and legendary: the screen tops up with rares, never shows nothing.
            foreach (var d in Pool().Where(d => d.rarity >= Rarity.Epic)) inv.ForceTake(d);
            draw.Offer(boss, Pool(), inv, 3, new Rng(5), cards, 0f);
            Assert.That(cards.Count, Is.EqualTo(3));
            Assert.That(cards.All(c => c.rarity == Rarity.Rare), Is.True, "topped up from the next rarity down");
        }

        [Test]
        public void ARerollAvoidsTheLastScreen()
        {
            var draw = new RewardDraw();
            var table = new RewardTable(100f, 35f, 0f, 0f);
            var a = new List<UpgradeDef>();
            var b = new List<UpgradeDef>();
            var rng = new Rng(11);
            draw.Offer(table, Pool(), null, 3, rng, a, 0f);
            draw.Offer(table, Pool(), null, 3, rng, b, 0f, reroll: true);
            Assert.That(a.Select(x => x.id).Intersect(b.Select(x => x.id)), Is.Empty);
        }

        [Test]
        public void DefaultTablesFollowTheDesignRules()
        {
            var r = new ShmupRewards();
            Assert.That(r.Validate(), Is.Empty);
            r.boss.rareWeight = 10f;
            r.battle.epicWeight = 5f;
            var errors = r.Validate();
            Assert.That(errors.Any(e => e.Contains("Boss")), Is.True);
            Assert.That(errors.Any(e => e.Contains("Battle")), Is.True);
        }
    }

    public class TrialAndWagerTests
    {
        static TrialDef T(TrialKind kind, float amount = 0f, int rooms = 1, string tag = "") =>
            new TrialDef { id = kind.ToString(), displayName = kind.ToString(), description = "x", kind = kind, amount = amount, rooms = rooms, tag = tag };

        [Test]
        public void NoHitPassesUnlessHit()
        {
            var ok = new TrialTracker(T(TrialKind.NoHit));
            ok.RoomStarted();
            ok.Sprinted();
            Assert.That(ok.RoomCleared(null), Is.EqualTo(TrialState.Passed));

            var hit = new TrialTracker(T(TrialKind.NoHit, rooms: 2));
            hit.RoomStarted();
            Assert.That(hit.RoomCleared(null), Is.EqualTo(TrialState.Running), "one of two rooms done");
            hit.PlayerHit();
            Assert.That(hit.State, Is.EqualTo(TrialState.Failed));
            Assert.That(hit.FailReason, Does.Contain("hit"));
        }

        [Test]
        public void NoSprintAndNoBombFailOnTheirVerb()
        {
            var s = new TrialTracker(T(TrialKind.NoSprint));
            s.PlayerHit();
            s.BombUsed();
            Assert.That(s.Running, Is.True);
            s.Sprinted();
            Assert.That(s.State, Is.EqualTo(TrialState.Failed));
            var b = new TrialTracker(T(TrialKind.NoBomb));
            b.BombUsed();
            Assert.That(b.State, Is.EqualTo(TrialState.Failed));
        }

        [Test]
        public void TimerCountsEachRoom()
        {
            var t = new TrialTracker(T(TrialKind.Timer, 30f, 2));
            t.RoomStarted();
            for (int i = 0; i < 25; i++) t.Tick(1f);
            Assert.That(t.RoomCleared(null), Is.EqualTo(TrialState.Running));
            t.RoomStarted();
            for (int i = 0; i < 31; i++) t.Tick(1f);
            Assert.That(t.RoomCleared(null), Is.EqualTo(TrialState.Failed), "the second room took 31 s");
        }

        [Test]
        public void OwnTaggedPassesAsSoonAsMet()
        {
            var inv = new UpgradeInventory();
            var gun = new UpgradeDef("gun_a", Rarity.Common, 3);
            gun.tags.Add("gun");
            var t = new TrialTracker(T(TrialKind.OwnTagged, 3f, 2, "gun"));
            inv.ForceTake(gun);
            Assert.That(t.RoomCleared(inv), Is.EqualTo(TrialState.Running));
            inv.ForceTake(gun);
            inv.ForceTake(gun);
            Assert.That(TrialTracker.TaggedStacks(inv, "gun"), Is.EqualTo(3));
            Assert.That(t.RoomCleared(inv), Is.EqualTo(TrialState.Passed));

            var miss = new TrialTracker(T(TrialKind.OwnTagged, 5f, 1, "gun"));
            Assert.That(miss.RoomCleared(inv), Is.EqualTo(TrialState.Failed));
        }

        [Test]
        public void GrazeCountsAcrossTheWindow()
        {
            var t = new TrialTracker(T(TrialKind.Graze, 50f, 2));
            t.Grazed(30);
            Assert.That(t.RoomCleared(null), Is.EqualTo(TrialState.Running));
            t.Grazed(25);
            Assert.That(t.RoomCleared(null), Is.EqualTo(TrialState.Passed));
            Assert.That(t.Status(), Does.Contain("done"));
        }

        [Test]
        public void TrialsAndWagersValidate()
        {
            var errors = new List<string>();
            new TrialDef().Validate("default", errors);
            new WagerDef().Validate("default", errors);
            Assert.That(errors, Is.Empty);
            T(TrialKind.Timer, 3f).Validate("short timer", errors);
            new WagerDef { stake = WagerStake.Curse, curse = new StatModifier("nope", StatOp.Flat, 1f) }.Validate("bad curse", errors);
            Assert.That(errors.Count, Is.EqualTo(2));
        }

        [Test]
        public void RequirementsGateTheOffer()
        {
            var t = new TrialDef { requires = ShmupEffects.BombBay };
            var inv = new UpgradeInventory();
            Assert.That(t.Available(inv), Is.False);
            var bay = new UpgradeDef("bomb_bay", Rarity.Epic);
            bay.effects.Add(ShmupEffects.BombBay);
            inv.ForceTake(bay);
            Assert.That(t.Available(inv), Is.True);
        }
    }

    public class InventoryRemovalTests
    {
        static UpgradeDef Sharp()
        {
            var d = new UpgradeDef("sharp", Rarity.Common, 3);
            d.modifiers.Add(new StatModifier(ShmupStats.Damage, StatOp.PercentAdd, 0.2f));
            d.effects.Add(ShmupEffects.Pierce);
            return d;
        }

        [Test]
        public void RemovingStacksTakesTheirStatsAndEffectsOff()
        {
            var l = new ShmupLoadout();
            var d = Sharp();
            l.Inventory.ForceTake(d);
            l.Inventory.ForceTake(d);
            Assert.That(l.Damage, Is.EqualTo(1.4f).Within(1e-4f));
            Assert.That(l.Pierce, Is.EqualTo(2));
            Assert.That(l.Inventory.Remove("sharp", 1), Is.EqualTo(1));
            Assert.That(l.Damage, Is.EqualTo(1.2f).Within(1e-4f));
            Assert.That(l.Pierce, Is.EqualTo(1));
            Assert.That(l.Inventory.Owned.Count, Is.EqualTo(1));
            Assert.That(l.Inventory.Remove("sharp"), Is.EqualTo(1));
            Assert.That(l.Damage, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(l.Inventory.Has("sharp"), Is.False);
            Assert.That(l.Inventory.Owned, Is.Empty);
            Assert.That(l.Inventory.Remove("sharp"), Is.EqualTo(0));
        }

        [Test]
        public void BonusLevelsBelongToTheirUpgrade()
        {
            var l = new ShmupLoadout();
            var d = Sharp();
            Assert.That(l.Inventory.AddBonus("sharp", null, null), Is.EqualTo(0), "not owned: no bonus");
            l.Inventory.ForceTake(d);
            var bonus = new List<StatModifier> { new StatModifier(ShmupStats.Damage, StatOp.PercentAdd, 0.1f) };
            Assert.That(l.Inventory.AddBonus("sharp", bonus, new[] { ShmupEffects.Pierce }), Is.EqualTo(1));
            Assert.That(l.Inventory.AddBonus("sharp", bonus, null), Is.EqualTo(2));
            Assert.That(l.Damage, Is.EqualTo(1.4f).Within(1e-4f));
            Assert.That(l.Pierce, Is.EqualTo(2));
            Assert.That(l.Inventory.BonusLevels("sharp"), Is.EqualTo(2));
            l.Inventory.Remove("sharp");
            Assert.That(l.Damage, Is.EqualTo(1f).Within(1e-4f), "the bonus left with it");
            Assert.That(l.Pierce, Is.EqualTo(0));
            Assert.That(l.Inventory.BonusLevels("sharp"), Is.EqualTo(0));

            l.Inventory.ForceTake(d);
            l.Inventory.AddBonus("sharp", bonus, null);
            l.Reset();
            Assert.That(l.Damage, Is.EqualTo(1f).Within(1e-4f), "a new run clears bonuses");
        }
    }

    public class ForgeAndAltarTests
    {
        [Test]
        public void TheFirstPushIsSafeAndTheRiskGrows()
        {
            var f = new ForgeRules();
            Assert.That(f.BreakChance(0), Is.EqualTo(0f));
            Assert.That(f.BreakChance(1), Is.EqualTo(0.25f).Within(1e-5f));
            Assert.That(f.BreakChance(2), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(f.BreakChance(10), Is.EqualTo(f.maxBreakChance).Within(1e-5f));
            Assert.That(ForgeRules.Percent(f.BreakChance(1)), Is.EqualTo("25%"));
            var rng = new Rng(1);
            for (int i = 0; i < 100; i++) Assert.That(f.Push(0, rng), Is.True, "push 1 never breaks");
            int broke = 0;
            for (int i = 0; i < 4000; i++) if (!f.Push(2, rng)) broke++;
            Assert.That(broke / 4000f, Is.InRange(0.45f, 0.55f));
            Assert.That(f.CanPush(3), Is.True);
            Assert.That(f.CanPush(4), Is.False);
        }

        [Test]
        public void TheDefaultBonusIsHalfTheModifiers()
        {
            var d = new UpgradeDef("od", Rarity.Rare);
            d.modifiers.Add(new StatModifier(ShmupStats.Damage, StatOp.Multiply, 1.4f));
            d.modifiers.Add(new StatModifier(ShmupStats.FireRate, StatOp.PercentAdd, 0.2f));
            var into = new List<StatModifier>();
            ForgeRules.DefaultBonus(d, 0.5f, into);
            Assert.That(into.Count, Is.EqualTo(2));
            Assert.That(into[0].value, Is.EqualTo(1.2f).Within(1e-5f));
            Assert.That(into[1].value, Is.EqualTo(0.1f).Within(1e-5f));
        }

        [Test]
        public void EncounterRulesValidate()
        {
            var errors = new List<string>();
            new ForgeRules().Validate(errors);
            new AltarRules().Validate(errors);
            new TraderRules().Validate(errors);
            new RemnantRules().Validate(errors);
            new CompanionRules().Validate(errors);
            Assert.That(errors, Is.Empty);
            new ForgeRules { maxBreakChance = 2f }.Validate(errors);
            new AltarRules { staminaKept = 1.5f }.Validate(errors);
            new CompanionRules { damageScale = 1.2f }.Validate(errors);
            Assert.That(errors.Count, Is.EqualTo(3));
        }

        [Test]
        public void CommonStacksForTheTrader()
        {
            var inv = new UpgradeInventory();
            var c = new UpgradeDef("c", Rarity.Common, 3);
            c.modifiers.Add(new StatModifier(ShmupStats.Damage, StatOp.PercentAdd, 0.1f));
            var r = new UpgradeDef("r", Rarity.Rare, 3);
            var coins = new UpgradeDef("pocket", Rarity.Common, 0);
            coins.effects.Add("coins:15");
            inv.ForceTake(c);
            inv.ForceTake(c);
            inv.ForceTake(r);
            inv.ForceTake(coins);
            Assert.That(TraderRules.CommonStacks(inv), Is.EqualTo(2), "an instant pickup can't be traded");
            Assert.That(Remnants.HasLastingEffect(coins), Is.False);
            Assert.That(Remnants.HasLastingEffect(c), Is.True);
        }

        [Test]
        public void EncounterRoomsAreRolledWhenWeighted()
        {
            var config = new RunConfig { floors = 3, roomsPerFloorMin = 6, roomsPerFloorMax = 7, forgeWeight = 10f, altarWeight = 10f, wagerWeight = 10f, trialWeight = 10f, traderWeight = 10f };
            var seen = new HashSet<RoomType>();
            for (ulong seed = 1; seed <= 30; seed++)
            {
                var plan = RunGenerator.Generate(config, new Rng(seed));
                Assert.That(plan.Validate(config), Is.Empty);
                foreach (var f in plan.floors) foreach (var s in f.steps) foreach (var o in s.options) seen.Add(o);
            }
            foreach (var t in new[] { RoomType.Forge, RoomType.Altar, RoomType.Wager, RoomType.Trial, RoomType.Trader })
            {
                Assert.That(seen, Does.Contain(t), t + " is rolled");
                Assert.That(RoomText.IsEncounter(t), Is.True);
                Assert.That(RoomText.Name(t), Is.Not.EqualTo(t.ToString()).Or.EqualTo("Forge").Or.EqualTo("Altar").Or.EqualTo("Wager").Or.EqualTo("Trader"));
                Assert.That(RoomText.HookKind(t), Is.Not.Empty);
            }

            var off = new RunConfig();
            for (ulong seed = 1; seed <= 30; seed++)
                foreach (var f in RunGenerator.Generate(off, new Rng(seed)).floors)
                    foreach (var s in f.steps)
                        Assert.That(s.options.Any(RoomText.IsEncounter), Is.False, "weights default to 0: other games never see them");
        }
    }

    public class RemnantTests
    {
        static UpgradeDef Def(string id, Rarity r, float damage = 0f, string effect = null)
        {
            var d = new UpgradeDef(id, r, 3) { displayName = id.ToUpperInvariant() };
            if (damage != 0f) d.modifiers.Add(new StatModifier(ShmupStats.Damage, StatOp.PercentAdd, damage));
            if (effect != null) d.effects.Add(effect);
            return d;
        }

        [Test]
        public void TheBestUpgradeWeakenedPlusHowYouDied()
        {
            var inv = new UpgradeInventory();
            inv.ForceTake(Def("sharp", Rarity.Common, 0.2f));
            inv.ForceTake(Def("sharp", Rarity.Common, 0.2f));
            inv.ForceTake(Def("coins", Rarity.Epic, 0f, "coins:15")); // instant: never the remnant
            var big = Def("overdrive", Rarity.Rare, 0.4f);
            inv.ForceTake(big);
            var rules = new RemnantRules();
            var r = Remnants.Build(inv, "gatekeeper", 1, 2, rules);
            Assert.That(r.sourceId, Is.EqualTo("overdrive"), "the rarest real upgrade");
            Assert.That(r.id, Is.EqualTo("remnant_2"));
            Assert.That(r.displayName, Does.Contain("OVERDRIVE"));
            Assert.That(r.modifiers[0].value, Is.EqualTo(0.2f).Within(1e-5f), "half strength");
            Assert.That(r.modifiers[1].stat, Is.EqualTo(rules.bossDeathStat), "died to a boss");
            Assert.That(r.description, Does.Contain("gatekeeper"));

            var card = r.ToUpgrade();
            Assert.That(Remnants.IsRemnant(card), Is.True);
            Assert.That(card.maxStacks, Is.EqualTo(1));

            var none = Remnants.Build(new UpgradeInventory(), "battle", 1, 1, rules);
            Assert.That(none.sourceId, Is.Empty);
            Assert.That(none.modifiers.Single().stat, Is.EqualTo(rules.battleDeathStat), "an empty run still leaves a stat");
            Assert.That(Remnants.Build(inv, "battle", 1, 1, new RemnantRules { enabled = false }), Is.Null);
        }

        [Test]
        public void TheBookKeepsTheNewestAndDraftsShowOne()
        {
            var book = new RemnantBook();
            var rules = new RemnantRules { maxKept = 2 };
            for (int run = 1; run <= 3; run++) book.Add(Remnants.Build(null, "battle", 1, run, rules), rules.maxKept);
            Assert.That(book.Count, Is.EqualTo(2));
            Assert.That(book.Find("remnant_1"), Is.Null, "the oldest went");

            var cards = new List<UpgradeDef> { new UpgradeDef("a"), new UpgradeDef("b"), new UpgradeDef("c") };
            var shown = Remnants.MaybeOffer(book, rules, new UpgradeInventory(), cards, null, new Rng(1), true);
            Assert.That(shown, Is.Not.Null);
            Assert.That(cards[2].id, Is.EqualTo("remnant_3"), "the newest takes the last card");
            Assert.That(cards.Count, Is.EqualTo(3));

            var never = new RemnantRules { offerChance = 0f };
            cards = new List<UpgradeDef> { new UpgradeDef("a") };
            Assert.That(Remnants.MaybeOffer(book, never, null, cards, null, new Rng(1), false), Is.Null);
            Assert.That(book.Remove("remnant_3"), Is.True);
            Assert.That(book.Count, Is.EqualTo(1));
        }
    }

    public class BombBayAndBoostTests
    {
        [Test]
        public void NoBayNoBombsAndTheBayRecharges()
        {
            var l = new ShmupLoadout();
            Assert.That(l.BombCapacity(0, 2, 6), Is.EqualTo(0), "the starting kit has no bombs");
            Assert.That(l.BombRechargeSeconds(20f), Is.EqualTo(0f));
            var bay = new UpgradeDef("bomb_bay", Rarity.Epic, 2);
            bay.effects.Add(ShmupEffects.BombBay);
            var plus = new UpgradeDef("spare_bomb", Rarity.Common, 2);
            plus.effects.Add(ShmupEffects.BombPlus);
            l.Inventory.ForceTake(bay);
            Assert.That(l.BombCapacity(0, 2, 6), Is.EqualTo(2));
            l.Inventory.ForceTake(bay);
            l.Inventory.ForceTake(plus);
            Assert.That(l.BombCapacity(0, 2, 6), Is.EqualTo(4), "2 + an extra Bay stack + a spare bomb");
            Assert.That(l.BombCapacity(0, 2, 3), Is.EqualTo(3), "capped");
            l.Stats.Add(new StatModifier(ShmupStats.BombRecharge, StatOp.PercentAdd, 1f));
            Assert.That(l.BombRechargeSeconds(20f), Is.EqualTo(10f).Within(1e-4f));

            var b = new BombBay();
            b.Configure(2, 10f);
            Assert.That(b.Bombs, Is.EqualTo(0));
            Assert.That(b.Tick(9f, true), Is.False);
            Assert.That(b.Tick(2f, false), Is.False, "no recharge outside a fight");
            Assert.That(b.Tick(1f, true), Is.True);
            Assert.That(b.Bombs, Is.EqualTo(1));
            Assert.That(b.TryUse(), Is.True);
            Assert.That(b.TryUse(), Is.False);
            b.Fill();
            Assert.That(b.Full, Is.True);
            Assert.That(b.RechargeFraction, Is.EqualTo(0f));
            b.Configure(1, 10f);
            Assert.That(b.Bombs, Is.EqualTo(1), "a smaller bay drops the extra");
        }

        [Test]
        public void EmberHungerHeatsAndCoolsAndRedLineNeedsTheLastLife()
        {
            var rules = new PowerBoostRules();
            var p = new PowerBoosts();
            p.Tick(0.016f, rules, 1, 0, 3, false);
            Assert.That(p.DamageMultiplier, Is.GreaterThan(1.3f), "three grazes heat the guns");
            for (int i = 0; i < 100; i++) p.Tick(0.1f, rules, 1, 0, 50, false);
            Assert.That(p.DamageMultiplier, Is.EqualTo(1f + rules.heatMax).Within(1e-4f), "capped");
            for (int i = 0; i < 100; i++) p.Tick(0.1f, rules, 1, 0, 0, false);
            Assert.That(p.DamageMultiplier, Is.EqualTo(1f).Within(1e-4f), "cooled off");
            p.Tick(0.1f, rules, 0, 1, 0, false);
            Assert.That(p.DamageMultiplier, Is.EqualTo(1f));
            p.Tick(0.1f, rules, 0, 1, 0, true);
            Assert.That(p.DamageMultiplier, Is.EqualTo(1f + rules.lastStandDamage).Within(1e-4f));
            Assert.That(p.FireRateMultiplier, Is.EqualTo(1f + rules.lastStandFireRate).Within(1e-4f));

            var l = new ShmupLoadout();
            l.FireRateBoost = 2f;
            Assert.That(l.FireInterval(0.1f), Is.EqualTo(0.05f).Within(1e-5f));
            l.DamageBoost = 1.5f;
            Assert.That(l.ShotDamageScale, Is.EqualTo(1.5f).Within(1e-5f));
            l.Reset();
            Assert.That(l.ShotDamageScale, Is.EqualTo(1f));
        }

        [Test]
        public void ReboundShotsBounceOffTheFloorHotterAndTimeDilationSlowsNearBullets()
        {
            var streams = new List<ShotStream>();
            PlayerShots.Build(0, 0, 0, 1, false, streams);
            var down = streams.Where(s => s.source == ShotSource.Rebound).ToList();
            Assert.That(down.Count, Is.EqualTo(2));
            Assert.That(down.All(s => s.bounce && s.angleDegrees > 180f), Is.True, "they fire down");

            var pool = new BulletPool(8) { bounceDamageScale = 1.6f };
            var box = PlayfieldBounds.Centered(6f, 5f);
            pool.Spawn(new Vector2(0f, -4.8f), -1.2f, 10f, 0, 0.1f, 3f, 0f, 0f, 1f, 0, BulletFlags.Bounce);
            for (int i = 0; i < 10; i++) pool.Step(0.02f, box, 0.5f);
            Assert.That(pool.Count, Is.EqualTo(1), "bounced, not culled");
            Assert.That(pool[0].position.Y, Is.GreaterThan(-5f));
            Assert.That(System.Math.Sin(pool[0].angle), Is.GreaterThan(0.5), "flying up now");
            Assert.That(pool[0].damage, Is.EqualTo(1.6f).Within(1e-5f), "hotter after the bounce");
            Assert.That(pool[0].flags & BulletFlags.Bounce, Is.EqualTo(BulletFlags.None), "only once");

            var slow = new BulletPool(4);
            slow.Spawn(new Vector2(0f, 1f), -1.5707964f, 2f, 0, 0.1f);
            slow.Spawn(new Vector2(4f, 1f), -1.5707964f, 2f, 0, 0.1f);
            slow.Step(1f, box);
            slow.SlowNear(Vector2.Zero, 1.6f, 0.5f, 1f);
            Assert.That(slow[0].position.Y, Is.EqualTo(0f).Within(1e-4f), "near: half speed (moved 1, not 2)");
            Assert.That(slow[1].position.Y, Is.EqualTo(-1f).Within(1e-4f), "far: full speed");
        }
    }

    public class CompanionTests
    {
        [Test]
        public void ItShootsGuardsOnACooldownAndRateLimitsBarks()
        {
            var c = new CompanionState();
            var rules = new CompanionRules { fireInterval = 0.5f, guardCooldown = 10f, barkCooldown = 4f };
            c.Begin(rules);
            Assert.That(c.Tick(0.4f, true, 1f), Is.EqualTo(0));
            Assert.That(c.Tick(0.2f, true, 1f), Is.EqualTo(1));
            Assert.That(c.Tick(1f, false, 1f), Is.EqualTo(0), "no shots out of a fight");
            Assert.That(c.GuardReady, Is.True);
            Assert.That(c.TryGuard(), Is.True);
            Assert.That(c.TryGuard(), Is.False, "one bullet per cooldown");
            Assert.That(c.GuardFraction, Is.EqualTo(0f).Within(1e-4f));
            c.Tick(5f, false, 1f);
            Assert.That(c.GuardFraction, Is.EqualTo(0.5f).Within(1e-3f));
            c.Tick(5.1f, false, 1f);
            Assert.That(c.GuardReady, Is.True);
            Assert.That(c.Guards, Is.EqualTo(1));
            c.TryGuard();
            c.Tick(5.1f, false, 0.5f);
            Assert.That(c.GuardReady, Is.True, "companion_shield_cooldown 0.5 halves the wait");

            Assert.That(c.CanBark, Is.True);
            c.Barked();
            Assert.That(c.CanBark, Is.False);
            c.Tick(4.1f, false, 1f);
            Assert.That(c.CanBark, Is.True);
            Assert.That(c.Offset.Length(), Is.GreaterThan(0.2f), "it orbits off the ship");
        }

        [Test]
        public void WithHealthItCanBeKnockedDownAndRevives()
        {
            var c = new CompanionState();
            c.Begin(new CompanionRules());
            Assert.That(c.HasHealth, Is.False, "maxHealth 0 (the default): it can't be hurt");
            Assert.That(c.Hurt(5f), Is.False);

            c.Begin(new CompanionRules { maxHealth = 2f, reviveSeconds = 4f, fireInterval = 0.5f, joinAfterBosses = 1 });
            Assert.That(c.Health, Is.EqualTo(2f));
            Assert.That(c.Hurt(1f), Is.False);
            Assert.That(c.Hurt(1f), Is.True, "knocked down");
            Assert.That(c.Down, Is.True);
            Assert.That(c.TryGuard(), Is.False, "no guard while down");
            Assert.That(c.Tick(1f, true, 1f), Is.EqualTo(0), "no shots while down");
            Assert.That(c.ReviveFraction, Is.EqualTo(0.25f).Within(1e-4f));
            c.Tick(3.1f, true, 1f);
            Assert.That(c.Down, Is.False, "back after its revive time");
            Assert.That(c.Health, Is.EqualTo(2f));
            Assert.That(c.Knockdowns, Is.EqualTo(1));
            var errors = new List<string>();
            new CompanionRules { joinAfterBosses = -1, maxHealth = -1f }.Validate(errors);
            Assert.That(errors.Count, Is.EqualTo(2));
        }

        [Test]
        public void TheCalloutFindsTheNextStepIncludingTheNextLoop()
        {
            var seq = new PatternSequence { loop = true, loopDelay = 1f };
            seq.Add(0f, new PatternDef { shape = PatternShape.Ring, count = 8, shots = 1, speed = 2f });
            seq.Add(2f, new PatternDef { shape = PatternShape.Spiral, count = 3, shots = 1, speed = 2f });
            Assert.That(PatternCallout.Upcoming(seq, 1.2f, 1f), Is.EqualTo(1), "the spiral at 2 s");
            Assert.That(PatternCallout.Upcoming(seq, 0.2f, 1f), Is.EqualTo(-1), "nothing within a second");
            float period = seq.Duration + seq.loopDelay;
            Assert.That(PatternCallout.Upcoming(seq, period - 0.5f, 1f), Is.EqualTo(0), "the ring of the next pass");
            Assert.That(PatternCallout.ShapeWord(PatternShape.Line), Is.EqualTo("wall"));
        }

        [Test]
        public void HookNamesForEncountersAndBarks()
        {
            Assert.That(StoryHooks.Encounter("forge", 2), Is.EqualTo(new[] { "forge_floor_2", "forge" }));
            Assert.That(StoryHooks.Encounter("trial_board", 0), Is.EqualTo(new[] { "trial_board" }));
            Assert.That(StoryHooks.CompanionBark("spiral", "The Last Light"), Is.EqualTo(new[] { "companion_bark_the_last_light_spiral", "companion_bark_spiral" }));
            Assert.That(StoryHooks.CompanionBark("guard"), Is.EqualTo(new[] { "companion_bark_guard" }));
            Assert.That(StoryHooks.CompanionBark(""), Is.Empty);
        }
    }

    public class EpicRubricTests
    {
        [Test]
        public void AnEpicNeedsAVerbTwoSituationsAPlaceAndAnIdea()
        {
            var def = new UpgradeDef("rebound", Rarity.Epic);
            var good = new EpicRubric { verb = UpgradeVerb.Aim, situations = new List<string> { "waves", "sides" }, wherePlayerIs = "low", idea = "bellows" };
            Assert.That(EpicRubric.Problems(def, good), Is.Empty);
            var bad = new EpicRubric { situations = new List<string> { "waves", "waves", "space" } };
            var errors = EpicRubric.Problems(def, bad);
            Assert.That(errors.Count, Is.EqualTo(5), string.Join("\n", errors));
            Assert.That(EpicRubric.Problems(def, null), Is.Not.Empty);
        }
    }

    /// <summary>The balance report's simulation, on its built-in rooms (the editor tool uses the real ones).</summary>
    public class BalanceReportTests
    {
        static UpgradeDef Effect(string id, Rarity r, string effect)
        {
            var d = new UpgradeDef(id, r) { displayName = id };
            d.effects.Add(effect);
            return d;
        }

        [Test]
        public void ItFlagsTheRearGunAndADeliberatelyUselessUpgrade()
        {
            var useless = new UpgradeDef("useless", Rarity.Common) { displayName = "Useless" };
            useless.modifiers.Add(new StatModifier(ShmupStats.ShotSpeed, StatOp.PercentAdd, 0.01f));
            var coins = new UpgradeDef("pocket", Rarity.Common) { displayName = "Pocket" };
            coins.effects.Add("coins:15");
            var pool = new List<UpgradeDef>
            {
                Effect("rear_guard", Rarity.Epic, ShmupEffects.RearShot),
                useless,
                Effect("twin_spread", Rarity.Rare, ShmupEffects.Spread),
                Effect("rebound", Rarity.Epic, ShmupEffects.Rebound),
                coins,
            };
            var sim = new BalanceSim { dt = 1f / 30f };
            var rooms = BalanceReport.DefaultRooms();
            var rows = BalanceReport.Run(sim, rooms, new SimPlayer(), pool, 2);
            var byId = rows.ToDictionary(r => r.id);
            TestContext.Out.WriteLine(BalanceReport.ToMarkdown(rows, BalanceReport.Baseline(sim, rooms, new SimPlayer(), 2), rooms.Count, 2, BalanceReport.DefaultThreshold));
            Assert.That(byId["rear_guard"].verdict, Is.EqualTo(BalanceVerdict.Flagged), "players sit low: nothing is ever behind them");
            Assert.That(byId["rear_guard"].damageShare, Is.LessThan(0.03f));
            Assert.That(byId["useless"].verdict, Is.EqualTo(BalanceVerdict.Flagged));
            Assert.That(byId["twin_spread"].verdict, Is.EqualTo(BalanceVerdict.Useful));
            Assert.That(byId["rebound"].verdict, Is.EqualTo(BalanceVerdict.Useful), "the rear gun's replacement fits where players are");
            Assert.That(byId["rebound"].damageShare, Is.GreaterThan(0.08f));
            Assert.That(byId["pocket"].verdict, Is.EqualTo(BalanceVerdict.NotSimulated));
            string csv = BalanceReport.ToCsv(rows);
            Assert.That(csv.Split('\n').Length, Is.GreaterThanOrEqualTo(pool.Count + 1));
        }

        [Test]
        public void FewerHitsCountOncePastTheNoiseOfTheSample()
        {
            // The real report's Time Dilation: 61 hits down to 50 over 51 rooms is 18% fewer, not "0%"
            // (the old rule wanted a quarter of a hit per room and missed it).
            Assert.That(BalanceReport.HitGain(61, 50), Is.EqualTo(11f / 61f).Within(1e-4f));
            Assert.That(BalanceReport.HitGain(6, 4), Is.EqualTo(0f), "2 saved of 6 is under the noise (sqrt 6 = 2.4)");
            Assert.That(BalanceReport.HitGain(4, 1), Is.EqualTo(0.75f).Within(1e-4f));
            Assert.That(BalanceReport.HitGain(3, 2), Is.EqualTo(0f), "a single hit is never enough");
            Assert.That(BalanceReport.HitGain(0, 0), Is.EqualTo(0f));
            Assert.That(BalanceReport.HitGain(20, 25), Is.EqualTo(0f), "more hits isn't a gain");
        }

        [Test]
        public void TimeDilationRegistersAndGrazeOnlyUpgradesAreMeasuredNotFlagged()
        {
            var halo = new UpgradeDef("wide_halo", Rarity.Common) { displayName = "Wide Halo" };
            halo.modifiers.Add(new StatModifier(ShmupStats.GrazeRadius, StatOp.PercentAdd, 0.35f));
            var pool = new List<UpgradeDef> { Effect("time_dilation", Rarity.Legendary, ShmupEffects.FocusSlow), halo };
            Assert.That(BalanceReport.IsGrazeOnly(halo), Is.True);
            Assert.That(BalanceReport.IsGrazeOnly(pool[0]), Is.False);
            var sim = new BalanceSim(); // 1/60 s steps, as in the report on the real rooms
            var rows = BalanceReport.Run(sim, BalanceReport.DefaultRooms(), new SimPlayer(), pool, 6);
            var byId = rows.ToDictionary(r => r.id);
            TestContext.Out.WriteLine(BalanceReport.ToMarkdown(rows, BalanceReport.Baseline(sim, BalanceReport.DefaultRooms(), new SimPlayer(), 6), 5, 6, BalanceReport.DefaultThreshold));
            var td = byId["time_dilation"];
            Assert.That(td.hitsPerRoom, Is.LessThan(td.baselineHitsPerRoom));
            Assert.That(td.verdict, Is.EqualTo(BalanceVerdict.Useful), "fewer hits register");
            Assert.That(td.usefulness, Is.GreaterThan(0.05f));
            var h = byId["wide_halo"];
            Assert.That(h.verdict, Is.EqualTo(BalanceVerdict.NotSimulated), "score and graze synergies only: not flagged");
            Assert.That(h.grazesPerRoom, Is.GreaterThan(h.baselineGrazesPerRoom), "but its grazes are measured");
            Assert.That(h.note, Does.Contain("grazes x"));
        }

        [Test]
        public void TheSimulationIsDeterministic()
        {
            var sim = new BalanceSim { dt = 1f / 30f };
            var room = BalanceReport.DefaultRooms()[1];
            var a = sim.Play(room, new ShmupLoadout(), new SimPlayer(), 42);
            var b = sim.Play(room, new ShmupLoadout(), new SimPlayer(), 42);
            Assert.That(a.damage, Is.EqualTo(b.damage));
            Assert.That(a.hits, Is.EqualTo(b.hits));
            Assert.That(a.clearSeconds, Is.EqualTo(b.clearSeconds));
            Assert.That(a.damage, Is.GreaterThan(0f), "the bot shoots things");
        }
    }
}
