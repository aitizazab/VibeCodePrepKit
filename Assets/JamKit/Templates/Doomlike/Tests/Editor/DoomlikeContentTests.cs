using System.Collections.Generic;
using System.IO;
using System.Linq;
using JamKit.Doomlike.EditorTools;
using JamKit.Patterns;
using JamKit.Shell;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UIElements;

namespace JamKit.Doomlike.Tests
{
    /// <summary>The DOOM-like content is complete and valid: every sample level parses, can be
    /// finished, builds into geometry with a NavMesh that joins its start to its exit; weapons and
    /// enemies have sane numbers; the sprite sheets really change with the viewing angle.
    /// If these fail after editing data, read the messages: they name the asset and the problem.
    /// Run "JamKit/Rebuild Shell Scenes" if the assets are missing.</summary>
    public class DoomlikeContentTests
    {
        static DoomlikeConfig Config()
        {
            var c = AssetDatabase.LoadAssetAtPath<DoomlikeConfig>(DoomlikeContent.ConfigPath);
            Assert.That(c, Is.Not.Null, "DoomlikeConfig at " + DoomlikeContent.ConfigPath);
            return c;
        }

        [Test]
        public void ConfigValidates()
        {
            var r = DoomlikeValidation.Check(Config());
            foreach (var w in r.warnings) TestContext.Out.WriteLine("warning: " + w);
            Assert.That(r.errors, Is.Empty, string.Join("\n", r.errors));
        }

        [Test]
        public void ThreeLevelsOfRisingSize()
        {
            var c = Config();
            Assert.That(c.LevelCount, Is.EqualTo(3));
            int lastEnemies = 0;
            for (int i = 0; i < c.LevelCount; i++)
            {
                var problems = new MapProblems();
                var map = c.ParseLevel(i, problems);
                Assert.That(problems.errors, Is.Empty, "level " + (i + 1) + ": " + problems);
                var report = MapValidator.Validate(map, c.ambushRadius);
                Assert.That(report.problems.errors, Is.Empty, "level " + (i + 1) + ": " + report.problems);
                Assert.That(report.exitReachable, Is.True, "level " + (i + 1) + " exit");
                Assert.That(report.keyOrder.Count, Is.GreaterThanOrEqualTo(1), "level " + (i + 1) + " uses a key");
                Assert.That(report.enemies, Is.GreaterThanOrEqualTo(lastEnemies), "level " + (i + 1) + " has at least as many enemies as the one before");
                Assert.That(report.triggers, Is.GreaterThanOrEqualTo(1), "level " + (i + 1) + " has an ambush");
                lastEnemies = report.enemies;
                TestContext.Out.WriteLine($"Level {i + 1} {map.name}: {map.width}x{map.height}, {report.enemies} enemies, {report.pickups} pickups, "
                    + $"{report.secrets} secret cells, keys {string.Join(",", report.keyOrder)}");
            }
            var boss = c.ParseLevel(c.LevelCount - 1, null);
            Assert.That(boss.things.Any(t => t.entry.category == EntityCategory.Enemy && c.enemies[t.entry.variant].isBoss), Is.True, "the last level has the boss");
        }

        [Test]
        public void EveryLevelBuildsWithAWalkableNavMesh()
        {
            var c = Config();
            for (int i = 0; i < c.LevelCount; i++)
            {
                var map = c.ParseLevel(i, new MapProblems());
                var root = new GameObject("Doomlike test level");
                try
                {
                    var level = LevelBuilder.Build(map, c, root.transform, true);
                    string n = "level " + (i + 1);
                    Assert.That(level.navMeshBuilt, Is.True, n + " NavMesh");
                    int doors = map.Count(CellKind.Door) + map.Count(CellKind.LockedDoor) + map.Count(CellKind.SecretDoor) + map.Count(CellKind.ClosetWall);
                    Assert.That(level.doors.Count, Is.EqualTo(doors), n + " doors");
                    Assert.That(level.enemySpawns.Count, Is.EqualTo(map.CountThings(EntityCategory.Enemy)), n + " enemies");
                    Assert.That(level.geometry.GetComponentsInChildren<MeshRenderer>().Length, Is.GreaterThanOrEqualTo(4), n + " floor, wall meshes");
                    Assert.That(level.geometry.GetComponentsInChildren<BoxCollider>().Length, Is.GreaterThan(4), n + " colliders");

                    // Carving only happens in play mode; open every door so the path test sees the layout.
                    foreach (var d in level.doors) if (d.obstacle != null) d.obstacle.enabled = false;
                    Assert.That(NavMesh.SamplePosition(level.startPosition, out var start, 1f, NavMesh.AllAreas), Is.True, n + " start on the NavMesh");
                    Assert.That(NavMesh.SamplePosition(level.ExitPosition, out var exit, 1f, NavMesh.AllAreas), Is.True, n + " exit on the NavMesh");
                    var path = new NavMeshPath();
                    NavMesh.CalculatePath(start.position, exit.position, NavMesh.AllAreas, path);
                    Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete), n + ": enemies can walk from the start to the exit (doors open)");
                    int off = level.enemySpawns.Count(s => !NavMesh.SamplePosition(s.position, out _, 1.6f, NavMesh.AllAreas));
                    Assert.That(off, Is.EqualTo(0), n + ": every enemy starts on the NavMesh");
                }
                finally
                {
                    Object.DestroyImmediate(root);
                }
            }
        }

        [Test]
        public void WeaponsHaveSlotsAndSaneDamage()
        {
            var c = Config();
            Assert.That(c.weapons.Count, Is.EqualTo(5));
            Assert.That(c.weapons.Select(w => w.slot).Distinct().Count(), Is.EqualTo(5), "one weapon per slot");
            Assert.That(c.weapons.Select(w => w.slot).OrderBy(s => s), Is.EqualTo(new[] { 1, 2, 3, 4, 5 }));
            Assert.That(c.weapons.Count(w => w.kind == WeaponKind.Melee), Is.EqualTo(1), "fists");
            Assert.That(c.weapons.Count(w => w.kind == WeaponKind.Projectile), Is.EqualTo(1), "one heavy projectile weapon");
            Assert.That(c.weapons.Any(w => w.pellets >= 6), Is.True, "a shotgun");
            Assert.That(c.startingWeapons.Select(w => w.slot), Is.EquivalentTo(new[] { 1, 2 }), "fists and pistol to start");
            foreach (var w in c.weapons)
            {
                float dps = w.damage * w.pellets / Mathf.Max(0.05f, w.def.fireInterval);
                Assert.That(dps, Is.InRange(20f, 200f), w.name + " damage per second");
                Assert.That(w.viewTexture, Is.Not.Null, w.name + " view");
                Assert.That(w.def.magazineSize, Is.EqualTo(0), w.name + " pooled ammo");
                if (w.slot > 2) Assert.That(w.MapChar, Is.Not.EqualTo('\0'), w.name + " can be found on maps");
            }
            // Guns that share ammo share it: the pistol and the rivet gun eat bullets.
            Assert.That(c.weapons.Count(w => w.ammoType == "bullets"), Is.EqualTo(2));
        }

        [Test]
        public void EnemiesHaveWorkingAttacksAndABoss()
        {
            var c = Config();
            Assert.That(c.enemies.Count, Is.EqualTo(6));
            Assert.That(c.enemies.Count(e => e.isBoss), Is.EqualTo(1));
            var boss = c.enemies.First(e => e.isBoss);
            Assert.That(boss.health, Is.GreaterThan(c.enemies.Where(e => !e.isBoss).Max(e => e.health) * 4f), "the boss is much tougher");
            foreach (var kind in new[] { EnemyAttackKind.Melee, EnemyAttackKind.Hitscan, EnemyAttackKind.Projectile })
                Assert.That(c.enemies.Any(e => e.attack.kind == kind), Is.True, "some enemy attacks with " + kind);
            foreach (var e in c.enemies)
            {
                Assert.That(e.sprite.texture, Is.Not.Null, e.name);
                if (e.attack.kind == EnemyAttackKind.Hitscan) Assert.That(e.brain.windupSeconds, Is.GreaterThanOrEqualTo(0.4f), e.name + " telegraphs its shot");
                if (e.attack.kind == EnemyAttackKind.Projectile) Assert.That(Fire(e.attack.pattern), Is.GreaterThan(0), e.name + " fires");
                if (e.secondaryChance > 0f && e.secondaryAttack.kind == EnemyAttackKind.Projectile) Assert.That(Fire(e.secondaryAttack.pattern), Is.GreaterThan(0), e.name + " secondary");
                Assert.That(e.attack.damage, Is.LessThanOrEqualTo(30f), e.name + " can't one-shot a full-health player");
            }
        }

        static int Fire(PatternSequence seq)
        {
            var em = new Emitter(seq, new Rng(1));
            var output = new List<BulletSpawn>(256);
            int total = 0;
            for (int i = 0; i < 180; i++)
            {
                output.Clear();
                em.Tick(1f / 60f, System.Numerics.Vector2.Zero, new System.Numerics.Vector2(5f, 0f), output);
                total += output.Count;
            }
            return total;
        }

        [Test]
        public void SpriteSheetsChangeWithTheViewingAngle()
        {
            var c = Config();
            foreach (var e in c.enemies)
            {
                string path = AssetDatabase.GetAssetPath(e.sprite.texture);
                var tex = new Texture2D(2, 2);
                try
                {
                    Assert.That(tex.LoadImage(File.ReadAllBytes(path)), Is.True, path);
                    Assert.That(tex.width % SpriteSheet.Columns, Is.EqualTo(0), e.name + " width is 8 cells");
                    Assert.That(tex.height % SpriteSheet.Rows, Is.EqualTo(0), e.name + " height is 5 cells");
                    int cw = tex.width / SpriteSheet.Columns, ch = tex.height / SpriteSheet.Rows;
                    var all = tex.GetPixels32();
                    var front = Cell(all, tex, 0, 0, cw, ch);
                    var side = Cell(all, tex, 2, 0, cw, ch);
                    var back = Cell(all, tex, 4, 0, cw, ch);
                    var right = Cell(all, tex, 6, 0, cw, ch);
                    Assert.That(Differ(front, back), Is.GreaterThan(30), e.name + ": front and back look different");
                    Assert.That(Differ(front, side), Is.GreaterThan(30), e.name + ": front and side look different");
                    Assert.That(Differ(side, Mirror(right, cw)), Is.LessThan(Differ(side, right)), e.name + ": the right side is (about) the mirrored left side");
                    Assert.That(Opaque(Cell(all, tex, 0, SpriteSheet.DeathRow, cw, ch)), Is.GreaterThan(20), e.name + " has death frames");
                }
                finally
                {
                    Object.DestroyImmediate(tex);
                }
            }
        }

        /// <summary>One sheet cell (rows counted from the top, as the sheet is laid out).</summary>
        static Color32[] Cell(Color32[] all, Texture2D t, int col, int row, int cw, int ch) =>
            Slice(all, t.width, col * cw, t.height - (row + 1) * ch, cw, ch);

        static Color32[] Slice(Color32[] all, int width, int x0, int y0, int w, int h)
        {
            var o = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    o[y * w + x] = all[(y0 + y) * width + x0 + x];
            return o;
        }

        static Color32[] Mirror(Color32[] a, int w)
        {
            var o = new Color32[a.Length];
            int h = a.Length / w;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    o[y * w + x] = a[y * w + (w - 1 - x)];
            return o;
        }

        static int Differ(Color32[] a, Color32[] b)
        {
            int n = 0;
            for (int i = 0; i < a.Length; i++)
                if (Mathf.Abs(a[i].r - b[i].r) + Mathf.Abs(a[i].g - b[i].g) + Mathf.Abs(a[i].b - b[i].b) + Mathf.Abs(a[i].a - b[i].a) > 40) n++;
            return n;
        }

        static int Opaque(Color32[] a) => a.Count(p => p.a > 128);

        [Test]
        public void UpgradesAreUniqueAndKnown()
        {
            var c = Config();
            var ids = c.upgrades.Select(u => u.def.id).ToList();
            Assert.That(ids.Count, Is.GreaterThanOrEqualTo(8));
            Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Count));
            Assert.That(c.fallbackUpgrade, Is.Not.Null);
            Assert.That(c.upgradeDraft, Is.True, "the draft is on by default");
            foreach (var u in c.upgrades)
            {
                foreach (var m in u.def.modifiers) Assert.That(DoomStats.IsKnownStat(m.stat), Is.True, u.name + " " + m.stat);
                foreach (var e in u.def.effects) Assert.That(DoomStats.IsKnownEffect(e), Is.True, u.name + " " + e);
            }
        }

        [Test]
        public void PresetAndSceneAreInPlace()
        {
            var preset = AssetDatabase.LoadAssetAtPath<GameConfig>(DoomlikeBuilder.PresetPath);
            Assert.That(preset, Is.Not.Null, DoomlikeBuilder.PresetPath);
            Assert.That(preset.firstGameplayScene, Is.EqualTo(DoomlikeBuilder.SceneName));
            Assert.That(preset.howToPlay.Count, Is.InRange(2, 4), "2-4 How to Play cards");
            Assert.That(preset.showPixelLookSetting, Is.True);
            Assert.That(File.Exists(DoomlikeBuilder.ScenePath), Is.True);
            Assert.That(EditorBuildSettings.scenes.Any(s => s.path == DoomlikeBuilder.ScenePath), Is.True, "the scene is in Build Settings");
            Assert.That(AssetDatabase.LoadAssetAtPath<StyleSheet>(DoomlikeBuilder.StylePath), Is.Not.Null);
        }

        [Test]
        public void LegendCharactersDontClash()
        {
            var errors = new List<string>();
            var legend = Config().BuildLegend(errors);
            Assert.That(errors, Is.Empty, string.Join("\n", errors));
            foreach (var ch in "mgshwW+HaAco345Tj") Assert.That(legend.Contains(ch), Is.True, "'" + ch + "' is on the legend");
        }
    }
}
