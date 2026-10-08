using System.Collections.Generic;
using System.Numerics;
using JamKit.BulletHell;
using JamKit.Roguelite;
using NUnit.Framework;

namespace JamKit.Tests.BulletHell
{
    public class EnemyMoverTests
    {
        static readonly PlayfieldBounds Box = PlayfieldBounds.Centered(6f, 5f);

        [Test]
        public void HoverEntersThenStays()
        {
            var m = new EnemyMover();
            m.Begin(new EnemyMoveDef { kind = EnemyMoveKind.Hover, enterSeconds = 1f }, new Vector2(0f, 7f), new Vector2(1f, 3f));
            Assert.That(m.Phase, Is.EqualTo(MoverPhase.Entering));
            for (int i = 0; i < 12; i++) m.Tick(0.1f, Vector2.Zero, Box);
            Assert.That(m.Phase, Is.EqualTo(MoverPhase.Acting));
            Assert.That(Vector2.Distance(m.Position, new Vector2(1f, 3f)), Is.LessThan(0.2f));
            for (int i = 0; i < 100; i++) m.Tick(0.1f, Vector2.Zero, Box);
            Assert.That(m.IsGone, Is.False, "hover with no time limit stays");
        }

        [Test]
        public void HoverLeavesAfterItsTime()
        {
            var m = new EnemyMover();
            m.Begin(new EnemyMoveDef { kind = EnemyMoveKind.Hover, enterSeconds = 0.5f, actSeconds = 1f, speed = 6f }, new Vector2(0f, 7f), new Vector2(0f, 3f));
            for (int i = 0; i < 60; i++) m.Tick(0.1f, Vector2.Zero, Box);
            Assert.That(m.IsGone, Is.True);
        }

        [Test]
        public void DiveChargesAtThePlayer()
        {
            var m = new EnemyMover();
            m.Begin(new EnemyMoveDef { kind = EnemyMoveKind.Dive, enterSeconds = 0.5f, actSeconds = 0.2f, speed = 8f }, new Vector2(0f, 7f), new Vector2(0f, 3f));
            var player = new Vector2(0f, -4f);
            for (int i = 0; i < 9; i++) m.Tick(0.1f, player, Box);
            Assert.That(m.Phase, Is.EqualTo(MoverPhase.Leaving));
            float y = m.Position.Y;
            m.Tick(0.1f, player, Box);
            Assert.That(m.Position.Y, Is.LessThan(y), "moving down toward the player");
            for (int i = 0; i < 50; i++) m.Tick(0.1f, player, Box);
            Assert.That(m.IsGone, Is.True, "leaves through the bottom");
        }

        [Test]
        public void SweepCrossesFromItsSideAndIsGoneAfter()
        {
            var m = new EnemyMover();
            m.Begin(new EnemyMoveDef { kind = EnemyMoveKind.Sweep, speed = 6f, amplitude = 0.5f }, new Vector2(-7f, 3f), Vector2.Zero);
            m.Tick(0.1f, Vector2.Zero, Box);
            Assert.That(m.IsGone, Is.False, "spawned outside, not gone before it entered");
            Assert.That(m.Position.X, Is.GreaterThan(-7f));
            for (int i = 0; i < 40; i++) m.Tick(0.1f, Vector2.Zero, Box);
            Assert.That(m.IsGone, Is.True);
        }
    }

    public class BossPhaseTests
    {
        [Test]
        public void PhasesFollowHealthAndOnlyGoForward()
        {
            var t = new BossPhaseTracker(new[] { 1f, 0.66f, 0.33f });
            Assert.That(t.Phase, Is.EqualTo(0));
            Assert.That(t.Update(0.8f), Is.False);
            Assert.That(t.Update(0.6f), Is.True);
            Assert.That(t.Phase, Is.EqualTo(1));
            Assert.That(t.Update(0.9f), Is.False, "healing doesn't go back");
            Assert.That(t.Update(0.1f), Is.True);
            Assert.That(t.Phase, Is.EqualTo(2));
            Assert.That(t.IsLastPhase, Is.True);
        }

        [Test]
        public void BigHitsSkipPhases()
        {
            var t = new BossPhaseTracker(new[] { 1f, 0.7f, 0.4f });
            Assert.That(t.Update(0.2f), Is.True);
            Assert.That(t.Phase, Is.EqualTo(2));
        }

        [Test]
        public void ValidateCatchesBadThresholds()
        {
            Assert.That(BossPhaseTracker.Validate(new[] { 1f, 0.5f }), Is.Empty);
            Assert.That(BossPhaseTracker.Validate(new[] { 0.9f }), Is.Not.Empty);
            Assert.That(BossPhaseTracker.Validate(new[] { 1f, 0.5f, 0.6f }), Is.Not.Empty);
            Assert.That(BossPhaseTracker.Validate(new float[0]), Is.Not.Empty);
        }
    }

    public class LoadoutTests
    {
        static UpgradeDef Def(string id, params string[] effects)
        {
            var d = new UpgradeDef(id, Rarity.Common, 0);
            d.effects.AddRange(effects);
            return d;
        }

        [Test]
        public void BaseVolleyIsTwoStreamsUp()
        {
            var l = new ShmupLoadout();
            var s = l.Streams(false);
            Assert.That(s.Count, Is.EqualTo(2));
            Assert.That(s[0].angleDegrees, Is.EqualTo(90f));
            Assert.That(l.Damage, Is.EqualTo(1f));
            Assert.That(l.FireInterval(0.1f), Is.EqualTo(0.1f).Within(1e-6f));
        }

        [Test]
        public void StatUpgradesChangeTheNumbers()
        {
            var l = new ShmupLoadout();
            var rapid = new UpgradeDef("rapid", Rarity.Common, 3);
            rapid.modifiers.Add(new StatModifier(ShmupStats.FireRate, StatOp.PercentAdd, 0.25f));
            var sharp = new UpgradeDef("sharp", Rarity.Common, 3);
            sharp.modifiers.Add(new StatModifier(ShmupStats.Damage, StatOp.PercentAdd, 0.2f));
            l.Inventory.Take(rapid);
            l.Inventory.Take(rapid);
            l.Inventory.Take(sharp);
            Assert.That(l.FireRate, Is.EqualTo(1.5f).Within(1e-5f));
            Assert.That(l.FireInterval(0.09f), Is.EqualTo(0.06f).Within(1e-5f));
            Assert.That(l.Damage, Is.EqualTo(1.2f).Within(1e-5f));
            l.Reset();
            Assert.That(l.FireRate, Is.EqualTo(1f));
            Assert.That(l.Inventory.Owned.Count, Is.EqualTo(0));
        }

        [Test]
        public void EffectUpgradesChangeTheVolley()
        {
            var l = new ShmupLoadout();
            var spread = Def("spread_up", ShmupEffects.Spread);
            l.Inventory.Take(spread);
            Assert.That(l.Streams(false).Count, Is.EqualTo(4));
            l.Inventory.Take(spread);
            Assert.That(l.Streams(false).Count, Is.EqualTo(6));
            l.Inventory.Take(Def("homing", ShmupEffects.Homing));
            int homing = 0;
            foreach (var s in l.Streams(false)) if (s.homing) homing++;
            Assert.That(homing, Is.EqualTo(4), "the spread streams home");
            l.Inventory.Take(Def("rear", ShmupEffects.RearShot));
            Assert.That(l.Streams(false).Count, Is.EqualTo(8));
            l.Inventory.Take(Def("pierce", ShmupEffects.Pierce));
            Assert.That(l.Pierce, Is.EqualTo(1));
            l.Inventory.Take(Def("bombs", ShmupEffects.BiggerBombs));
            Assert.That(l.BombPower, Is.EqualTo(1.5f).Within(1e-5f));
        }

        [Test]
        public void FocusTightensTheSpread()
        {
            var l = new ShmupLoadout();
            l.Inventory.Take(Def("spread_up", ShmupEffects.Spread));
            float wide = l.Streams(false)[2].angleDegrees;
            float tight = l.Streams(true)[2].angleDegrees;
            Assert.That(tight - 90f, Is.LessThan(wide - 90f));
        }

        [Test]
        public void HomingAloneAddsTwoStreams()
        {
            var streams = new List<ShotStream>();
            PlayerShots.Build(0, 0, 1, false, streams);
            Assert.That(streams.Count, Is.EqualTo(4));
            Assert.That(streams[2].homing && streams[3].homing, Is.True);
        }

        [Test]
        public void EffectIdsParse()
        {
            Assert.That(ShmupEffects.TryParseAmount("coins:15", ShmupEffects.Coins, out int n), Is.True);
            Assert.That(n, Is.EqualTo(15));
            Assert.That(ShmupEffects.IsKnown("coins:15"), Is.True);
            Assert.That(ShmupEffects.IsKnown(ShmupEffects.Spread), Is.True);
            Assert.That(ShmupEffects.IsKnown("laser_beam"), Is.False);
        }
    }

    public class RunRulesTests
    {
        [Test]
        public void PricesAndRerolls()
        {
            var e = new ShmupEconomy();
            Assert.That(e.PriceOf(Rarity.Common), Is.LessThan(e.PriceOf(Rarity.Rare)));
            Assert.That(e.PriceOf(Rarity.Epic), Is.LessThan(e.PriceOf(Rarity.Legendary)));
            Assert.That(e.RerollCost(0), Is.EqualTo(10));
            Assert.That(e.RerollCost(2), Is.EqualTo(20));
        }

        [Test]
        public void EventTagsParse()
        {
            Assert.That(RoomEventEffect.TryParse("bh:coins:40", out var e), Is.True);
            Assert.That(e.kind, Is.EqualTo(RoomEventKind.Coins));
            Assert.That(e.amount, Is.EqualTo(40));
            Assert.That(RoomEventEffect.TryParse("bh:coins:-15", out e) && e.amount == -15, Is.True);
            Assert.That(RoomEventEffect.TryParse("bh:life", out e) && e.kind == RoomEventKind.Lives && e.amount == 1, Is.True);
            Assert.That(RoomEventEffect.TryParse("bh:bomb:+2", out e) && e.amount == 2, Is.True);
            Assert.That(RoomEventEffect.TryParse("bh:draft:rare", out e) && e.minRarity == Rarity.Rare, Is.True);
            Assert.That(RoomEventEffect.TryParse("bh:pay:20", out e) && e.kind == RoomEventKind.PayForDraft, Is.True);
            Assert.That(RoomEventEffect.TryParse("door_opens", out e), Is.False, "other games' events are ignored");
            Assert.That(RoomEventEffect.TryParse("bh:teleport", out e), Is.False);
            Assert.That(RoomEventEffect.TryParse("bh:draft:mythic", out e), Is.False);
        }

        [Test]
        public void EveryRoomTypeHasWords()
        {
            foreach (RoomType t in System.Enum.GetValues(typeof(RoomType)))
            {
                Assert.That(RoomText.Name(t), Is.Not.Empty);
                Assert.That(RoomText.Letter(t).Length, Is.EqualTo(1));
                Assert.That(RoomText.Describe(t), Is.Not.Empty);
            }
        }
    }
}
