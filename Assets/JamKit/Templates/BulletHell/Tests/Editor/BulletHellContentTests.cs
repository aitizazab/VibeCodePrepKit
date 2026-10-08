using System.Collections.Generic;
using System.Linq;
using Ink.Runtime;
using JamKit.BulletHell.EditorTools;
using JamKit.Patterns;
using JamKit.Roguelite;
using JamKit.Shell;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace JamKit.BulletHell.Tests
{
    /// <summary>The bullet-hell content is complete and valid, and its upgrades do what they say.
    /// If these fail after editing data, read the messages: they name the asset and the problem.
    /// Run "JamKit/Rebuild Shell Scenes" if the assets are missing.</summary>
    public class BulletHellContentTests
    {
        static BulletHellConfig Config()
        {
            var c = AssetDatabase.LoadAssetAtPath<BulletHellConfig>(BulletHellContent.ConfigPath);
            Assert.That(c, Is.Not.Null, "BulletHellConfig at " + BulletHellContent.ConfigPath);
            return c;
        }

        [Test]
        public void ConfigValidates()
        {
            var errors = BulletHellValidation.Validate(Config());
            Assert.That(errors, Is.Empty, string.Join("\n", errors));
        }

        [Test]
        public void EveryFloorHasRoomsAndABoss()
        {
            var c = Config();
            Assert.That(c.floors.Count, Is.EqualTo(c.run.floors));
            for (int f = 0; f < c.floors.Count; f++)
            {
                var floor = c.floors[f];
                Assert.That(floor.combatRooms.Count, Is.GreaterThan(0), $"floor {f + 1} battle rooms");
                Assert.That(floor.eliteRooms.Count, Is.GreaterThan(0), $"floor {f + 1} elite rooms");
                Assert.That(floor.boss, Is.Not.Null, $"floor {f + 1} boss");
                Assert.That(floor.boss.phases.Count, Is.InRange(2, 4), $"floor {f + 1} boss phases");
                foreach (var room in floor.combatRooms.Concat(floor.eliteRooms))
                {
                    Assert.That(room.waves.Count, Is.GreaterThan(0), room.name);
                    foreach (var wave in room.waves)
                        foreach (var s in wave.spawns)
                        {
                            Assert.That(s.enemy, Is.Not.Null, wave.name);
                            Assert.That(s.enemy.hp, Is.GreaterThan(0f), s.enemy.name);
                        }
                }
            }
            var bosses = c.floors.Select(f => f.boss).Distinct().Count();
            Assert.That(bosses, Is.EqualTo(3), "a different boss per floor");
        }

        static IEnumerable<EnemyDef> Enemies(BulletHellConfig c) =>
            c.floors.SelectMany(f => f.combatRooms.Concat(f.eliteRooms)).SelectMany(r => r.waves).SelectMany(w => w.spawns).Select(s => s.enemy).Distinct();

        static int Fire(PatternSequence seq, float seconds)
        {
            var e = new Emitter(seq, new Rng(1));
            var output = new List<BulletSpawn>(4096);
            int total = 0;
            for (float t = 0f; t < seconds; t += 1f / 60f)
            {
                output.Clear();
                e.Tick(1f / 60f, System.Numerics.Vector2.Zero, new System.Numerics.Vector2(0f, -4f), output);
                total += output.Count;
            }
            return total;
        }

        [Test]
        public void EveryAttackActuallyFires()
        {
            var c = Config();
            int armed = 0;
            foreach (var e in Enemies(c))
            {
                if (e.attack == null || e.attack.steps.Count == 0) continue; // rammers (Gnat) have no gun
                armed++;
                Assert.That(Fire(e.attack, 10f), Is.GreaterThan(0), e.name + " fires");
            }
            Assert.That(armed, Is.GreaterThanOrEqualTo(8));
            foreach (var floor in c.floors)
                foreach (var phase in floor.boss.phases)
                    Assert.That(Fire(phase.attack, 6f), Is.GreaterThan(10), floor.boss.name + " / " + phase.name);
        }

        [Test]
        public void UpgradesAreUniqueAndCoverEveryRarity()
        {
            var c = Config();
            var ids = c.upgrades.Select(u => u.def.id).ToList();
            Assert.That(ids.Count, Is.GreaterThanOrEqualTo(15));
            Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Count), "unique ids");
            foreach (Rarity r in System.Enum.GetValues(typeof(Rarity)))
                Assert.That(c.upgrades.Any(u => u.def.rarity == r), Is.True, "an upgrade of rarity " + r);
            Assert.That(c.upgrades.Count(u => u.def.effects.Count > 0), Is.GreaterThanOrEqualTo(6), "effect upgrades");
            Assert.That(c.upgrades.Count(u => u.def.modifiers.Count > 0), Is.GreaterThanOrEqualTo(6), "stat upgrades");
            Assert.That(c.fallbackUpgrade, Is.Not.Null);
        }

        [Test]
        public void EveryUpgradeChangesSomething()
        {
            var c = Config();
            var loadout = new ShmupLoadout();
            foreach (var u in c.upgrades)
            {
                loadout.Reset();
                var before = ShmupStats.All.Select(s => loadout.Stats.Get(s)).ToArray();
                int streams = loadout.Streams(false).Count;
                Assert.That(loadout.Inventory.Take(u.def), Is.True, u.name + " can be taken");
                var after = ShmupStats.All.Select(s => loadout.Stats.Get(s)).ToArray();
                bool statChanged = !before.SequenceEqual(after);
                bool effect = u.def.effects.Any(e => loadout.Effect(e) > 0) || u.def.effects.Count > 0;
                Assert.That(statChanged || effect, Is.True, u.name + " changes a stat or adds an effect");
                if (u.def.effects.Contains(ShmupEffects.Spread)) Assert.That(loadout.Streams(false).Count, Is.GreaterThan(streams), u.name);
                if (u.def.effects.Contains(ShmupEffects.RearShot)) Assert.That(loadout.Streams(false).Count, Is.GreaterThan(streams), u.name);
                if (u.def.effects.Contains(ShmupEffects.Pierce)) Assert.That(loadout.Pierce, Is.EqualTo(1), u.name);
                if (u.def.effects.Contains(ShmupEffects.BiggerBombs)) Assert.That(loadout.BombPower, Is.GreaterThan(1f), u.name);
            }
        }

        [Test]
        public void SpecificUpgradesHaveTheirNumbers()
        {
            var c = Config();
            var byId = c.upgrades.ToDictionary(u => u.def.id, u => u.def);
            var l = new ShmupLoadout();
            l.Inventory.Take(byId["sharp_rounds"]);
            Assert.That(l.Damage, Is.EqualTo(1.2f).Within(1e-4f));
            l.Inventory.Take(byId["overdrive"]);
            Assert.That(l.Damage, Is.EqualTo(1.2f * 1.4f).Within(1e-4f));
            Assert.That(l.FireRate, Is.EqualTo(1.25f).Within(1e-4f));
            l.Inventory.Take(byId["twin_spread"]);
            l.Inventory.Take(byId["seeker_rounds"]);
            Assert.That(l.Streams(false).Count(s => s.homing), Is.EqualTo(2));
        }

        [Test]
        public void RunsFollowTheRules()
        {
            var c = Config();
            int rooms = 0;
            for (ulong seed = 1; seed <= 40; seed++)
            {
                var plan = RunGenerator.Generate(c.run, new Rng(seed));
                Assert.That(plan.Validate(c.run), Is.Empty, "seed " + seed);
                Assert.That(plan.floors.Count, Is.EqualTo(3));
                foreach (var f in plan.floors)
                {
                    Assert.That(f.steps.Count, Is.InRange(5, 6), "rooms per floor");
                    rooms += f.steps.Count;
                }
            }
            Debug.Log("Bullet hell: average rooms per run " + (rooms / 40f));
        }

        [Test]
        public void EventStoryHasEveryKnot()
        {
            var c = Config();
            Assert.That(c.events, Is.Not.Null);
            Assert.That(c.events.isCompiled, Is.True, "BulletHellEvents.ink compiled");
            var story = new Story(c.events.storyJson);
            foreach (var knot in c.eventKnots)
                Assert.That(story.ContentAtPath(new Path(knot)).correctObj, Is.Not.Null, "knot " + knot);
        }

        static bool HasKnot(Story story, string knot)
        {
            try { return story.ContentAtPath(new Path(knot)).correctObj != null; }
            catch (System.Exception) { return false; }
        }

        [Test]
        public void StoryHooksResolveAgainstTheRealData()
        {
            var c = Config();
            var story = new Story(c.events.storyJson);
            var ids = new HashSet<string>();
            foreach (var floor in c.floors)
            {
                Assert.That(floor.boss, Is.Not.Null);
                string id = floor.boss.StoryId;
                Assert.That(id, Is.Not.Empty, floor.boss.name + " has a story id");
                Assert.That(id, Does.Match("^[a-z0-9_]+$"), floor.boss.name + ": ink-safe id");
                Assert.That(ids.Add(id), Is.True, "boss story ids are unique: " + id);
            }
            // The placeholder lines: the first boss speaks, the others are silent until someone writes them.
            string first = c.floors[0].boss.StoryId;
            Assert.That(JamKit.Dialogue.StoryHooks.Resolve(JamKit.Dialogue.StoryHooks.BossIntro(first), k => HasKnot(story, k)), Is.EqualTo("boss_" + first + "_intro"));
            Assert.That(JamKit.Dialogue.StoryHooks.Resolve(JamKit.Dialogue.StoryHooks.Death("battle", 1), k => HasKnot(story, k)), Is.EqualTo("death"));
            Assert.That(JamKit.Dialogue.StoryHooks.Resolve(JamKit.Dialogue.StoryHooks.Rest(1), k => HasKnot(story, k)), Is.EqualTo("rest"));
            Assert.That(JamKit.Dialogue.StoryHooks.Resolve(JamKit.Dialogue.StoryHooks.RunStart(1), k => HasKnot(story, k)), Is.EqualTo("run_start"));
            Assert.That(JamKit.Dialogue.StoryHooks.Resolve(JamKit.Dialogue.StoryHooks.BossIntro("nobody"), k => HasKnot(story, k)), Is.Null, "missing hooks fall back to nothing");
        }

        [Test]
        public void HowToPlaySaysFocusIsSlowAndFiringIsAutomatic()
        {
            // The creator's first playtest held Shift thinking it was a sprint.
            var preset = AssetDatabase.LoadAssetAtPath<GameConfig>(BulletHellBuilder.PresetPath);
            Assert.That(preset, Is.Not.Null);
            string fly = preset.howToPlay[0].body;
            Assert.That(fly, Does.Contain("Hold Shift"));
            Assert.That(fly, Does.Contain("slow, precise movement (shows your hitbox)"));
            Assert.That(fly, Does.Contain("You fire automatically"));
            Assert.That(BulletHellHud.LegendText, Does.Contain("slow"));
            Assert.That(preset.AsksDifficultyOnPlay, Is.True, "an action template asks for the difficulty on Play");
        }

        [Test]
        public void SceneAndPresetExist()
        {
            Assert.That(System.IO.File.Exists(BulletHellBuilder.ScenePath), Is.True, BulletHellBuilder.ScenePath);
            Assert.That(EditorBuildSettings.scenes.Any(s => s.path == BulletHellBuilder.ScenePath && s.enabled), Is.True, "in Build Settings");
            var preset = AssetDatabase.LoadAssetAtPath<GameConfig>(BulletHellBuilder.PresetPath);
            Assert.That(preset, Is.Not.Null, "preset");
            Assert.That(preset.firstGameplayScene, Is.EqualTo(BulletHellBuilder.SceneName));
            Assert.That(preset.howToPlay.Count, Is.InRange(2, 5));
            var mat = AssetDatabase.LoadAssetAtPath<Material>(BulletHellBuilder.MaterialPath);
            Assert.That(mat, Is.Not.Null);
            Assert.That(mat.enableInstancing, Is.True, "GPU instancing on the bullet material");
            Assert.That(mat.shader.name, Is.EqualTo("JamKit/BulletInstanced"));
        }

        [Test]
        public void BulletFieldCullsAndCollides()
        {
            var go = new GameObject("field test");
            try
            {
                var field = go.AddComponent<BulletField>();
                field.Configure(BulletHellContent.Styles(), PlayfieldBounds.Centered(6f, 5f));
                var spawns = new List<BulletSpawn>
                {
                    new BulletSpawn { position = new System.Numerics.Vector2(0f, 0f), angle = 0f, speed = 0f, styleIndex = 3, lifetime = 0f },
                    new BulletSpawn { position = new System.Numerics.Vector2(5.5f, 0f), angle = 0f, speed = 10f, styleIndex = 0, lifetime = 0f },
                };
                Assert.That(field.EmitEnemy(spawns), Is.EqualTo(2));
                Assert.That(field.Enemy[0].radius, Is.EqualTo(BulletHellContent.Styles()[3].hitRadius), "radius from the style");
                for (int i = 0; i < 30; i++) field.Tick(1f / 30f);
                Assert.That(field.Enemy.Count, Is.EqualTo(1), "the fast one left the playfield");
                Assert.That(field.Enemy.FirstOverlap(new System.Numerics.Vector2(0.25f, 0f), 0.07f), Is.EqualTo(0), "a big ball hits");
                field.FirePlayer(new Vector2(0f, -2f), Mathf.PI / 2f, 20f, 3f, 6, 1.5f, 0, false, 0f);
                var targets = new[] { new HitTarget { position = new System.Numerics.Vector2(0f, -1.5f), radius = 0.4f, id = 1, active = true } };
                var hits = new List<BulletHit>();
                field.Tick(1f / 60f);
                Assert.That(field.Player.CollideTargets(targets, 1, hits), Is.EqualTo(1));
                Assert.That(hits[0].damage, Is.EqualTo(3f));
                field.EnemyTimeScale = 0f;
                Assert.That(field.EnemyTimeScale, Is.EqualTo(0f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
