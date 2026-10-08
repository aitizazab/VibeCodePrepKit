using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using JamKit.BulletHell;
using JamKit.Roguelite;
using NUnit.Framework;

namespace JamKit.Tests.BulletHell
{
    /// <summary>The epic balance pass: Ghost Step's phases, Guardian Angel's cost, Last Gambit, Still Point,
    /// the close-range Scatter and Blade, Seeker Rounds' cadence, and the bot modes that measure them.</summary>
    public class EpicPassTests
    {
        static UpgradeDef Effect(string id, string effect)
        {
            var d = new UpgradeDef(id, Rarity.Epic) { displayName = id };
            d.effects.Add(effect);
            return d;
        }

        [Test]
        public void GhostStepPhasesOnlyAtTheStartOfASprintAFewTimesARoom()
        {
            var rules = new PowerBoostRules { ghostWindow = 0.5f, ghostCooldown = 5f, ghostPhasesPerRoom = 3 };
            var g = new GhostPhase();
            g.RoomStarted(rules);
            Assert.That(g.PhasesLeft, Is.EqualTo(3));
            Assert.That(g.Tick(0.1f, true, rules), Is.True, "a sprint starts: a phase");
            Assert.That(g.Phasing, Is.True);
            g.Tick(0.5f, true, rules);
            Assert.That(g.Phasing, Is.False, "only its first half second");
            g.Tick(1f, false, rules);
            Assert.That(g.Tick(0.1f, true, rules), Is.False, "a new sprint inside the cooldown: no phase");
            g.Tick(4f, false, rules);
            Assert.That(g.Tick(0.1f, true, rules), Is.True, "after the cooldown");
            g.Tick(6f, false, rules);
            g.Tick(0.1f, true, rules); // a tap
            Assert.That(g.PhasesLeft, Is.EqualTo(0), "tapping sprint spends a phase");
            g.Tick(6f, false, rules);
            Assert.That(g.Tick(0.1f, true, rules), Is.False, "none left this room");
            g.RoomStarted(rules, 2);
            Assert.That(g.PhasesLeft, Is.EqualTo(4), "an extra stack or forge level: one more a room");
        }

        [Test]
        public void GuardianAngelSpendsEveryBombThenCoolsDown()
        {
            var rules = new PowerBoostRules { guardianCooldown = 10f, guardianPowerPerBomb = 0.25f };
            var bay = new BombBay();
            bay.Configure(3, 20f);
            bay.Fill();
            var a = new GuardianAngel();
            Assert.That(a.TryTrigger(bay, rules), Is.EqualTo(3), "all three bombs");
            Assert.That(bay.Bombs, Is.EqualTo(0));
            Assert.That(a.CooldownLeft, Is.EqualTo(10f));
            bay.Add(1);
            Assert.That(a.TryTrigger(bay, rules), Is.EqualTo(0), "cooling down");
            a.Tick(10f);
            Assert.That(a.TryTrigger(bay, rules), Is.EqualTo(1));
            bay.Empty();
            a.Tick(10f);
            Assert.That(a.TryTrigger(bay, rules), Is.EqualTo(0), "no bomb, no save");
            Assert.That(GuardianAngel.PowerFor(3, rules), Is.EqualTo(1.5f).Within(1e-5f), "a bigger blast for more bombs");
        }

        [Test]
        public void StillPointRampsWhileStillAndResetsOnAnyMove()
        {
            var rules = new PowerBoostRules { stillRampSeconds = 0.6f, stillMax = 1f };
            var p = new PowerBoosts();
            for (int i = 0; i < 3; i++) p.Tick(0.1f, rules, 0, 0, 0, false, 1, false);
            Assert.That(p.DamageMultiplier, Is.EqualTo(1.5f).Within(1e-3f), "half way after 0.3 s");
            for (int i = 0; i < 10; i++) p.Tick(0.1f, rules, 0, 0, 0, false, 1, false);
            Assert.That(p.DamageMultiplier, Is.EqualTo(2f).Within(1e-3f), "double damage, capped");
            p.Tick(0.016f, rules, 0, 0, 0, false, 1, true);
            Assert.That(p.DamageMultiplier, Is.EqualTo(1f), "any movement resets it");
            p.Tick(0.1f, rules, 0, 1, 0, true);
            Assert.That(p.DamageMultiplier, Is.EqualTo(1.75f).Within(1e-4f), "Last Gambit: +75% on the last life");
            Assert.That(p.FireRateMultiplier, Is.EqualTo(1f));
        }

        [Test]
        public void LastGambitRicochetsToTheNearestOtherEnemy()
        {
            var targets = new[]
            {
                new HitTarget { position = new Vector2(0f, 3f), radius = 0.4f, id = 1, active = true },
                new HitTarget { position = new Vector2(1f, 3f), radius = 0.4f, id = 2, active = true },
                new HitTarget { position = new Vector2(0.5f, 3f), radius = 0.4f, id = 3, active = false },
                new HitTarget { position = new Vector2(9f, 3f), radius = 0.4f, id = 4, active = true },
            };
            Assert.That(LastGambit.RicochetTarget(targets, 4, 1, new Vector2(0f, 3f), 4f), Is.EqualTo(1), "not the one it hit, not an inactive one");
            Assert.That(LastGambit.RicochetTarget(targets, 4, 2, new Vector2(1f, 3f), 0.5f), Is.EqualTo(-1), "nothing in range");

            var pool = new BulletPool(4);
            pool.Spawn(new Vector2(0f, 2.9f), 0f, 0f, 3, 0.1f, 1f, 0f, 0f, 2f, 0, BulletFlags.Ricocheted);
            var hits = new List<BulletHit>();
            pool.CollideTargets(targets, 2, hits);
            Assert.That(hits.Single().flags & BulletFlags.Ricocheted, Is.EqualTo(BulletFlags.Ricocheted), "a hit knows it's a ricochet");
            Assert.That(hits[0].targetId, Is.EqualTo(1));
        }

        [Test]
        public void ScatterAndBladeReplaceTheMainGuns()
        {
            var rules = new ShotRules();
            var streams = new List<ShotStream>();
            PlayerShots.Build(0, 0, 0, 0, 1, 0, false, rules, streams);
            Assert.That(streams.Count, Is.EqualTo(rules.scatterPellets));
            Assert.That(streams.All(s => s.source == ShotSource.Scatter && s.falloff && s.lifetime == rules.scatterLifetime), Is.True);
            Assert.That(streams.Max(s => s.angleDegrees) - streams.Min(s => s.angleDegrees), Is.EqualTo(rules.scatterConeDegrees).Within(1e-3f));
            Assert.That(rules.ScatterFalloffAt(0f), Is.EqualTo(1f));
            Assert.That(rules.ScatterFalloffAt(rules.scatterLifetime), Is.EqualTo(rules.scatterFalloff).Within(1e-5f), "weak at the end of its range");

            PlayerShots.Build(1, 0, 0, 0, 0, 1, false, rules, streams);
            Assert.That(streams.Count(s => s.source == ShotSource.Main), Is.EqualTo(0), "the blade has no main streams");
            Assert.That(streams.Count(s => s.source == ShotSource.Spread), Is.EqualTo(2), "side streams still fire");

            var l = new ShmupLoadout();
            float normal = l.FireInterval(0.1f);
            l.Inventory.ForceTake(Effect("scatter", ShmupEffects.Scatter));
            Assert.That(l.FireInterval(0.1f), Is.EqualTo(normal * rules.scatterIntervalScale).Within(1e-5f), "the scatter fires slower");
            float pointBlankDps = rules.scatterPellets * rules.scatterDamage / l.FireInterval(0.085f);
            Assert.That(pointBlankDps / (2f / 0.085f), Is.InRange(2f, 4f), "point-blank: 2-4x the main guns");

            var ship = new Vector2(0f, -3f);
            Assert.That(BladeArc.Touches(ship, new Vector2(0f, -1.5f), 0.4f, rules.bladeRange, rules.bladeHalfAngle), Is.True, "right in front");
            Assert.That(BladeArc.Touches(ship, new Vector2(0f, 0.5f), 0.4f, rules.bladeRange, rules.bladeHalfAngle), Is.False, "out of reach");
            Assert.That(BladeArc.Touches(ship, new Vector2(0f, -4.2f), 0.3f, rules.bladeRange, rules.bladeHalfAngle), Is.False, "behind");
            float slash = BladeArc.SlashDamage(rules, 1f, 0.085f);
            Assert.That(slash / rules.bladeInterval / (2f / 0.085f), Is.EqualTo(rules.bladeDpsScale).Within(1e-3f));
        }

        [Test]
        public void SeekerRoundsFireEveryThirdVolleySlower()
        {
            var rules = new ShotRules { homingVolleyEvery = 3, homingSpeedScale = 0.7f, homingStreamDamage = 0.7f };
            var streams = new List<ShotStream>();
            PlayerShots.Build(0, 0, 1, 0, 0, 0, false, rules, streams);
            var homing = streams.Where(s => s.homing).ToList();
            Assert.That(homing.Count, Is.EqualTo(2));
            Assert.That(homing.All(s => s.speedScale == 0.7f && s.damageScale == 0.7f), Is.True);
            Assert.That(Enumerable.Range(0, 9).Count(v => homing[0].FiresOn(v)), Is.EqualTo(3), "one volley in three");
            Assert.That(streams.Where(s => !s.homing).All(s => s.FiresOn(1)), Is.True, "the main guns fire every volley");
        }

        [Test]
        public void TheBotPlaysToItsUpgradeBurstingAndClosingIn()
        {
            var sim = new BalanceSim { dt = 1f / 30f };
            var room = BalanceReport.DefaultRooms()[3]; // the boss-sized target
            var basic = sim.Play(room, new ShmupLoadout(), new SimPlayer(), 7);
            var still = new ShmupLoadout();
            still.Inventory.ForceTake(Effect("still_point", ShmupEffects.StillPoint));
            var s = sim.Play(room, still, new SimPlayer(), 7);
            Assert.That(s.stillSeconds, Is.GreaterThan(basic.stillSeconds), "Still Point: a burst mover holds still more");
            var close = new ShmupLoadout();
            close.Inventory.ForceTake(Effect("blade", ShmupEffects.Blade));
            var c = sim.Play(room, close, new SimPlayer(), 7);
            Assert.That(c.heightSeconds / c.clearSeconds, Is.GreaterThan(basic.heightSeconds / basic.clearSeconds + 1f), "Blade: an aggressive bot plays higher, near its target");
            Assert.That(c.damageBySource[(int)ShotSource.Blade], Is.GreaterThan(0f), "and the blade lands");
        }
    }
}
