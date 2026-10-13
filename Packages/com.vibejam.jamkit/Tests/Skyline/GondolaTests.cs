using System;
using System.Collections.Generic;
using System.Numerics;
using JamKit.Skyline;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    public class GondolaTests
    {
        static readonly Vector3 Far = new Vector3(0f, 0f, 100f);

        // A mine at the origin that has finished arming (0.6 s), so the next tick can trigger it.
        static WallMine ArmedMine(EnemyDef def)
        {
            var m = new WallMine(Vector3.Zero, def);
            m.Tick(def.mineArmSeconds + 0.01f, Far, out _);
            Assert.That(m.State, Is.EqualTo(MineState.Armed));
            return m;
        }

        [Test]
        public void TheGondolaCarriesTheSpecsTagsAndJoinsTheFourBots()
        {
            var g = SkylineEnemies.Gondola();
            Assert.That(g.id, Is.EqualTo(SkylineEnemies.GondolaId));
            Assert.That(SkylineEnemies.GondolaId, Is.EqualTo("window-washer-gondola"));
            Assert.That(g.displayName, Is.EqualTo("Window-washer gondola"));
            Assert.That(g.aim, Is.EqualTo(AimModel.Area));
            Assert.That(g.layer, Is.EqualTo(ThreatLayer.Walls));
            Assert.That(g.chassisHp, Is.EqualTo(60f));
            Assert.That(g.plates.Length, Is.EqualTo(2));
            Assert.That(g.plates[0].id, Is.EqualTo("bucket-left"));
            Assert.That(g.plates[0].hp, Is.EqualTo(20f));
            Assert.That(g.plates[1].id, Is.EqualTo("bucket-right"));
            Assert.That(g.plates[1].hp, Is.EqualTo(20f));
            Assert.That(g.attackDamage, Is.EqualTo(25f));
            Assert.That(g.range, Is.EqualTo(30f));
            Assert.That(g.scrapPlating, Is.EqualTo(25f));
            Assert.That(g.railSpeed, Is.EqualTo(3f));
            Assert.That(g.mineSpacing, Is.EqualTo(4f));
            Assert.That(g.mineMax, Is.EqualTo(6));
            Assert.That(g.mineArmSeconds, Is.EqualTo(0.6f));
            Assert.That(g.mineFuseSeconds, Is.EqualTo(0.35f));
            Assert.That(g.mineTriggerRadius, Is.EqualTo(1.5f));
            Assert.That(g.mineBlastRadius, Is.EqualTo(2.5f));
            Assert.That(g.mineLifetime, Is.EqualTo(12f));

            var all = SkylineEnemies.All();
            Assert.That(all.Length, Is.EqualTo(4));
            Assert.That(Array.Exists(all, d => d.id == SkylineEnemies.GondolaId), Is.True);
            var a = SkylineEnemies.Find(SkylineEnemies.GondolaId);
            var b = SkylineEnemies.Find(SkylineEnemies.GondolaId);
            Assert.That(a, Is.Not.Null);
            Assert.That(a.id, Is.EqualTo(SkylineEnemies.GondolaId));
            Assert.That(ReferenceEquals(a, b), Is.False, "a fresh instance each call");
        }

        [Test]
        public void TheGondolaSharesAnEncounterWithEitherAimedBot()
        {
            var area = SkylineEnemies.Gondola().aim;
            Assert.That(AimRules.CanShareEncounter(area, AimModel.LeadsYou), Is.True);
            Assert.That(AimRules.CanShareEncounter(area, AimModel.FiresAtWhereYouAre), Is.True);
            Assert.That(AimRules.CanShareEncounter(AimModel.LeadsYou, AimModel.FiresAtWhereYouAre), Is.False, "the old rule stands");
        }

        [Test]
        public void TheRailPingPongsAndTravelledNeverDecreases()
        {
            var r = new GondolaRail(new Vector3(0f, 10f, 0f), new Vector3(10f, 10f, 0f), 4f);
            Assert.That(r.Position, Is.EqualTo(new Vector3(0f, 10f, 0f)));
            r.Tick(1f);
            Assert.That(r.Position.X, Is.EqualTo(4f).Within(1e-4f));
            Assert.That(r.Travelled, Is.EqualTo(4f).Within(1e-4f));
            r.Tick(2f); // 12 m along: 2 m past b, back to x = 8
            Assert.That(r.Position.X, Is.EqualTo(8f).Within(1e-4f));
            Assert.That(r.Position.Y, Is.EqualTo(10f).Within(1e-4f));
            Assert.That(r.Travelled, Is.EqualTo(12f).Within(1e-4f));
            float last = r.Travelled;
            for (int i = 0; i < 60; i++)
            {
                r.Tick(0.25f);
                Assert.That(r.Travelled, Is.GreaterThanOrEqualTo(last));
                Assert.That(r.Position.X, Is.InRange(-1e-3f, 10f + 1e-3f));
                last = r.Travelled;
            }
            Assert.That(r.Travelled, Is.EqualTo(12f + 60f).Within(1e-2f));
            r.Tick(0.5f); // 74 m: 74 mod 20 = 14 -> 6 m back from b -> x = 6
            Assert.That(r.Position.X, Is.EqualTo(6f).Within(1e-2f));
        }

        [Test]
        public void AZeroLengthRailStaysPutAndAZeroSpeedRailDoesNotMove()
        {
            var p = new Vector3(1f, 2f, 3f);
            var r = new GondolaRail(p, p, 3f);
            r.Tick(5f);
            Assert.That(r.Position, Is.EqualTo(p));
            Assert.That(float.IsNaN(r.Position.X), Is.False);
            var still = new GondolaRail(Vector3.Zero, Vector3.UnitX * 5f, 0f);
            still.Tick(5f);
            Assert.That(still.Position, Is.EqualTo(Vector3.Zero));
        }

        [Test]
        public void AMineDoesNotTriggerWhileArming()
        {
            var def = SkylineEnemies.Gondola();
            var m = new WallMine(Vector3.Zero, def);
            Assert.That(m.State, Is.EqualTo(MineState.Arming));
            bool boom = m.Tick(0.3f, Vector3.Zero, out float dmg); // the chest is on top of it, but it's still arming
            Assert.That(boom, Is.False);
            Assert.That(dmg, Is.EqualTo(0f));
            Assert.That(m.State, Is.EqualTo(MineState.Arming));
            Assert.That(m.Age, Is.EqualTo(0.3f).Within(1e-5f));
        }

        [Test]
        public void AnArmedMineTriggersInsideTheTriggerRadiusOnly()
        {
            var def = SkylineEnemies.Gondola();
            var m = ArmedMine(def);
            m.Tick(0.05f, new Vector3(0f, 0f, def.mineTriggerRadius + 0.5f), out _);
            Assert.That(m.State, Is.EqualTo(MineState.Armed), "outside the trigger radius");
            m.Tick(0.05f, new Vector3(0f, 0f, def.mineTriggerRadius - 0.1f), out _);
            Assert.That(m.State, Is.EqualTo(MineState.Fusing));
        }

        [Test]
        public void AFusedMineExplodesExactlyOnceAndHurtsAChestStillInTheBlast()
        {
            var def = SkylineEnemies.Gondola();
            var m = ArmedMine(def);
            var chest = new Vector3(1f, 0f, 0f);
            Assert.That(m.Tick(0.05f, chest, out _), Is.False);
            Assert.That(m.State, Is.EqualTo(MineState.Fusing));
            Assert.That(m.Tick(0.2f, chest, out float d1), Is.False, "the fuse is 0.35 s");
            Assert.That(d1, Is.EqualTo(0f));
            int blasts = 0;
            float damage = 0f;
            for (int i = 0; i < 20; i++)
            {
                if (m.Tick(0.05f, chest, out float dmg))
                {
                    blasts++;
                    damage += dmg;
                }
            }
            Assert.That(blasts, Is.EqualTo(1));
            Assert.That(damage, Is.EqualTo(def.attackDamage));
            Assert.That(m.State, Is.EqualTo(MineState.Exploded));
            Assert.That(m.Tick(1f, chest, out float after), Is.False, "final");
            Assert.That(after, Is.EqualTo(0f));
        }

        [Test]
        public void ALeavingChestDodgesTheBlast()
        {
            var def = SkylineEnemies.Gondola();
            var m = ArmedMine(def);
            m.Tick(0.05f, new Vector3(1f, 0f, 0f), out _); // triggered
            Assert.That(m.State, Is.EqualTo(MineState.Fusing));
            bool boom = m.Tick(def.mineFuseSeconds + 0.01f, new Vector3(def.mineBlastRadius + 1f, 0f, 0f), out float dmg);
            Assert.That(boom, Is.True, "it still goes off");
            Assert.That(dmg, Is.EqualTo(0f), "but the chest left the blast radius");
            Assert.That(m.State, Is.EqualTo(MineState.Exploded));
        }

        [Test]
        public void AnUntouchedMineExpiresAtItsLifetimeAndNeverExplodes()
        {
            var def = SkylineEnemies.Gondola();
            var m = new WallMine(Vector3.Zero, def);
            bool boom = false;
            for (int i = 0; i < 110; i++) boom |= m.Tick(0.1f, Far, out _);
            Assert.That(m.State, Is.EqualTo(MineState.Armed), "11 s in");
            for (int i = 0; i < 20; i++) boom |= m.Tick(0.1f, Far, out _);
            Assert.That(m.State, Is.EqualTo(MineState.Expired));
            Assert.That(boom, Is.False);
            Assert.That(m.Tick(1f, Vector3.Zero, out float dmg), Is.False, "expired is final: a chest on it does nothing");
            Assert.That(dmg, Is.EqualTo(0f));
            Assert.That(m.State, Is.EqualTo(MineState.Expired));
        }

        [Test]
        public void TheLayerDropsMinesOneSpacingApartAlongTheRail()
        {
            var def = SkylineEnemies.Gondola();
            var rail = new GondolaRail(Vector3.Zero, new Vector3(100f, 0f, 0f), 4f);
            var layer = new MineLayer(def);
            var blasts = new List<float>();
            Assert.That(layer.Mines.Count, Is.EqualTo(0));
            for (int i = 0; i < 25; i++)
            {
                rail.Tick(0.1f); // 0.4 m per tick: 10 m after 25 ticks
                layer.Tick(0.1f, rail, true, Far, blasts);
            }
            Assert.That(layer.Mines.Count, Is.EqualTo(2), "mines at 4 m and 8 m of travel");
            Assert.That(layer.Mines[0].Position.X, Is.EqualTo(4f).Within(0.5f));
            Assert.That(layer.Mines[1].Position.X, Is.EqualTo(8f).Within(0.5f));
            Assert.That(layer.Mines[1].Position.X - layer.Mines[0].Position.X, Is.EqualTo(def.mineSpacing).Within(0.5f));
            Assert.That(blasts, Is.Empty);
        }

        [Test]
        public void TheFirstMineWaitsOneSpacingAfterLayingStarts()
        {
            var def = SkylineEnemies.Gondola();
            var rail = new GondolaRail(Vector3.Zero, new Vector3(100f, 0f, 0f), 4f);
            var layer = new MineLayer(def);
            var blasts = new List<float>();
            rail.Tick(5f); // 20 m of travel before laying starts
            layer.Tick(0.1f, rail, false, Far, blasts);
            layer.Tick(0.1f, rail, true, Far, blasts);
            Assert.That(layer.Mines.Count, Is.EqualTo(0), "no back-laying for the 20 m already travelled");
            rail.Tick(0.5f); // 2 m after laying started
            layer.Tick(0.1f, rail, true, Far, blasts);
            Assert.That(layer.Mines.Count, Is.EqualTo(0));
            rail.Tick(0.6f); // 4.4 m after
            layer.Tick(0.1f, rail, true, Far, blasts);
            Assert.That(layer.Mines.Count, Is.EqualTo(1));
        }

        [Test]
        public void TheLayerCapsLiveMinesAndTheOldestGoes()
        {
            var def = SkylineEnemies.Gondola();
            def.mineLifetime = 1000f; // nothing expires in this test
            var rail = new GondolaRail(Vector3.Zero, new Vector3(1000f, 0f, 0f), 4f);
            var layer = new MineLayer(def);
            var blasts = new List<float>();
            for (int i = 0; i < 110; i++) // 44 m: ten spacings (mines at 4.4 m, 8.4 m ... 40.4 m)
            {
                rail.Tick(0.1f);
                layer.Tick(0.1f, rail, true, Far, blasts);
            }
            Assert.That(layer.Mines.Count, Is.EqualTo(def.mineMax));
            Assert.That(layer.Mines[0].Position.X, Is.EqualTo(20f).Within(0.5f), "mines 5-10 live: the first four were removed");
            Assert.That(layer.Mines[def.mineMax - 1].Position.X, Is.EqualTo(40f).Within(0.5f));
        }

        [Test]
        public void TheLayerLaysNothingWhileNotLayingAndKeepsTickingMinesAfterwards()
        {
            var def = SkylineEnemies.Gondola();
            var rail = new GondolaRail(Vector3.Zero, new Vector3(100f, 0f, 0f), 4f);
            var layer = new MineLayer(def);
            var blasts = new List<float>();
            for (int i = 0; i < 25; i++)
            {
                rail.Tick(0.1f);
                layer.Tick(0.1f, rail, false, Far, blasts);
            }
            Assert.That(layer.Mines.Count, Is.EqualTo(0));
            for (int i = 0; i < 25; i++)
            {
                rail.Tick(0.1f);
                layer.Tick(0.1f, rail, true, Far, blasts);
            }
            int laid = layer.Mines.Count;
            Assert.That(laid, Is.GreaterThan(0));
            float age = layer.Mines[0].Age;
            // The gondola is wrecked: no more laying, but the wall stays dangerous.
            for (int i = 0; i < 20; i++) layer.Tick(0.1f, rail, false, Far, blasts);
            Assert.That(layer.Mines.Count, Is.EqualTo(laid), "no new mines");
            Assert.That(layer.Mines[0].Age, Is.EqualTo(age + 2f).Within(1e-3f), "the old mines keep ticking");
            // Walk into one: it still blasts.
            var target = layer.Mines[0].Position;
            for (int i = 0; i < 20; i++) layer.Tick(0.05f, rail, false, target, blasts);
            Assert.That(blasts.Count, Is.GreaterThan(0));
            Assert.That(blasts[0], Is.EqualTo(def.attackDamage));
        }

        [Test]
        public void TheLayerReportsBlastsAndDropsFinishedMines()
        {
            var def = SkylineEnemies.Gondola();
            var rail = new GondolaRail(Vector3.Zero, new Vector3(100f, 0f, 0f), 4f);
            var layer = new MineLayer(def);
            var blasts = new List<float>();
            for (int i = 0; i < 12; i++)
            {
                rail.Tick(0.1f);
                layer.Tick(0.1f, rail, true, Far, blasts);
            }
            Assert.That(layer.Mines.Count, Is.EqualTo(1));
            Vector3 chest = layer.Mines[0].Position;
            for (int i = 0; i < 40; i++) layer.Tick(0.05f, rail, false, chest, blasts); // 2 s: armed, triggered, blown
            Assert.That(blasts.Count, Is.EqualTo(1), "one blast, once");
            Assert.That(blasts[0], Is.EqualTo(def.attackDamage));
            Assert.That(layer.Mines.Count, Is.EqualTo(0), "an exploded mine leaves the list");

            // And an untouched one is dropped at its lifetime.
            var layer2 = new MineLayer(def);
            var rail2 = new GondolaRail(Vector3.Zero, new Vector3(100f, 0f, 0f), 4f);
            for (int i = 0; i < 12; i++)
            {
                rail2.Tick(0.1f);
                layer2.Tick(0.1f, rail2, true, Far, blasts);
            }
            Assert.That(layer2.Mines.Count, Is.EqualTo(1));
            for (int i = 0; i < 130; i++) layer2.Tick(0.1f, rail2, false, Far, blasts);
            Assert.That(layer2.Mines.Count, Is.EqualTo(0), "expired mines leave the list");
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        [TestCase(0f)]
        [TestCase(-0.5f)]
        public void BadDtChangesNothingAndNeverThrows(float dt)
        {
            var def = SkylineEnemies.Gondola();
            var rail = new GondolaRail(Vector3.Zero, new Vector3(10f, 0f, 0f), 3f);
            rail.Tick(1f);
            Vector3 pos = rail.Position;
            float travelled = rail.Travelled;
            Assert.DoesNotThrow(() => rail.Tick(dt));
            Assert.That(rail.Position, Is.EqualTo(pos));
            Assert.That(rail.Travelled, Is.EqualTo(travelled));

            var m = new WallMine(Vector3.Zero, def);
            bool boom = true;
            float dmg = -1f;
            Assert.DoesNotThrow(() => boom = m.Tick(dt, Vector3.Zero, out dmg));
            Assert.That(boom, Is.False);
            Assert.That(dmg, Is.EqualTo(0f));
            Assert.That(m.State, Is.EqualTo(MineState.Arming));
            Assert.That(m.Age, Is.EqualTo(0f));

            var layer = new MineLayer(def);
            var blasts = new List<float>();
            Assert.DoesNotThrow(() => layer.Tick(dt, rail, true, Far, blasts));
            Assert.That(layer.Mines.Count, Is.EqualTo(0));
            Assert.That(blasts, Is.Empty);
        }

        [Test]
        public void ANonFiniteChestNeverTriggersOrDamages()
        {
            var def = SkylineEnemies.Gondola();
            var bad = new[]
            {
                new Vector3(float.NaN, 0f, 0f), new Vector3(0f, float.NaN, 0f),
                new Vector3(float.PositiveInfinity, 0f, 0f), new Vector3(0f, 0f, float.NegativeInfinity),
            };
            foreach (var chest in bad)
            {
                var armed = ArmedMine(def);
                for (int i = 0; i < 5; i++) Assert.DoesNotThrow(() => armed.Tick(0.1f, chest, out _));
                Assert.That(armed.State, Is.EqualTo(MineState.Armed), "never triggered by " + chest);

                // A mine already fusing: a bad chest at the blast does 0 damage.
                var fusing = ArmedMine(def);
                fusing.Tick(0.05f, new Vector3(1f, 0f, 0f), out _);
                Assert.That(fusing.State, Is.EqualTo(MineState.Fusing));
                bool boom = fusing.Tick(def.mineFuseSeconds + 0.01f, chest, out float dmg);
                Assert.That(boom, Is.True);
                Assert.That(dmg, Is.EqualTo(0f));
            }
        }

        [Test]
        public void TheLayerSurvivesNullsAndABadChest()
        {
            var def = SkylineEnemies.Gondola();
            var layer = new MineLayer(def);
            var rail = new GondolaRail(Vector3.Zero, new Vector3(100f, 0f, 0f), 4f);
            var blasts = new List<float>();
            Assert.DoesNotThrow(() => layer.Tick(0.1f, null, true, Far, blasts));
            Assert.DoesNotThrow(() => layer.Tick(0.1f, rail, true, Far, null));
            for (int i = 0; i < 30; i++)
            {
                rail.Tick(0.1f);
                layer.Tick(0.1f, rail, true, new Vector3(float.NaN, float.NaN, float.NaN), blasts);
            }
            Assert.That(layer.Mines.Count, Is.GreaterThan(0));
            Assert.That(blasts, Is.Empty);
        }
    }
}
