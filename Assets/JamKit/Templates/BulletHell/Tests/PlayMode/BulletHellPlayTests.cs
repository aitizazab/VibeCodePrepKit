using System.Collections;
using System.Linq;
using JamKit.Flow;
using JamKit.Shell;
using JamKit.Tests.Play;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static JamKit.Tests.Play.ShellTestUtil;

namespace JamKit.BulletHell.Tests
{
    /// <summary>The bullet hell in its real scene: a whole seeded run (god mode, rooms skipped with
    /// the debug skip, drafts picked) up to the final boss and Victory; getting hit, bombing and
    /// dying; and 3000 bullets on screen.</summary>
    public class BulletHellPlayTests
    {
        [UnitySetUp]
        public IEnumerator SetUp() => BootToTitle();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            DebugMenu.GodMode = false;
            DebugMenu.TimeScale = 1f;
            BulletHellGame.SeedOverride = 0;
            yield return null;
        }

        /// <summary>Every stack of every owned upgrade (a repeat pick adds a stack, not a new entry).</summary>
        static int Stacks(JamKit.Roguelite.UpgradeInventory inv)
        {
            int n = 0;
            foreach (var d in inv.Owned) n += inv.Count(d.id);
            return n;
        }

        static IEnumerator StartBulletHell(ulong seed)
        {
            BulletHellGame.SeedOverride = seed;
            Assert.That(GameRoot.Instance.SceneService.Load("BulletHell"), Is.True, "BulletHell is in Build Settings");
            yield return WaitFor(() => GameRoot.Flow.InRun && !GameRoot.IsLoadingScene && SceneManager.GetActiveScene().name == "BulletHell"
                && BulletHellGame.Current != null && BulletHellGame.Current.Run != null, "the bullet-hell scene");
            Assert.That(BulletHellGame.Current.Run.seed, Is.EqualTo(seed));
        }

        [UnityTest]
        public IEnumerator SeededRunReachesVictory()
        {
            yield return StartBulletHell(12345);
            var game = BulletHellGame.Current;
            DebugMenu.GodMode = true;
            DebugMenu.TimeScale = 4f;
            Assert.That(GameRoot.Hud.IsShown, Is.True, "HUD on");
            Assert.That(game.Run.Plan.floors.Count, Is.EqualTo(3));

            // The first room really plays: enemies arrive, both sides shoot.
            yield return WaitFor(() => game.Phase == BulletHellGame.RoomPhase.Waves && game.EnemiesAlive > 0, "the first enemies", 20f);
            yield return WaitFor(() => game.field.Player.Count > 0, "player shots", 10f);
            yield return WaitFor(() => game.Kills > 0 || game.field.Enemy.Count > 0, "a kill or an enemy bullet", 20f);

            int picked = 0;
            float end = Time.realtimeSinceStartup + 240f;
            while (GameRoot.Flow.Current != GameState.Victory)
            {
                Assert.That(GameRoot.Flow.Current, Is.Not.EqualTo(GameState.GameOver), "god mode: no game over");
                if (Time.realtimeSinceStartup > end)
                    Assert.Fail($"No victory after 240 s: floor {game.Run.floor + 1} room {game.Run.step + 1}, phase {game.Phase}, state {GameRoot.Flow.Current}");
                if (GameRoot.Dialogue.IsActive) GameRoot.Dialogue.AutoStep();
                else if (GameRoot.Flow.Current == GameState.Playing)
                {
                    if (game.Choice.IsOpen && game.Choice.Current == ChoicePanel.Kind.Draft)
                    {
                        int owned = Stacks(game.Loadout.Inventory);
                        Assert.That(game.Choice.CardCount, Is.InRange(1, 3), "1 to 3 cards");
                        Assert.That(game.Choice.Choose(0), Is.True, "take the first card");
                        picked++;
                        Assert.That(Stacks(game.Loadout.Inventory), Is.EqualTo(owned + 1), "the pick added one stack");
                    }
                    else game.DebugSkip();
                }
                yield return null;
            }

            Assert.That(game.BossesDefeated, Is.EqualTo(3), "all three bosses");
            Assert.That(game.Run.floor, Is.EqualTo(2), "finished on the last floor");
            Assert.That(picked, Is.GreaterThanOrEqualTo(2), "a draft after each of the first two bosses");
            Assert.That(game.DraftsTaken, Is.EqualTo(picked));
            Assert.That(GameRoot.Ui.Victory.IsVisible, Is.True);
            Assert.That(GameRoot.Run.score, Is.EqualTo(game.Score));
            Assert.That(GameRoot.Run.Extra.Any(s => s.label == "Floors cleared" && s.value == "3 / 3"), Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator HitsCostLivesBombsClearAndDeathEndsTheRun()
        {
            yield return StartBulletHell(4242);
            var game = BulletHellGame.Current;
            var player = game.player;
            Assert.That(player.Health.Current, Is.EqualTo(game.config.player.lives + GameRoot.Settings.extraLives), "lives + the difficulty's extra lives");
            yield return WaitFor(() => game.Phase == BulletHellGame.RoomPhase.Waves, "the first wave", 20f);
            yield return WaitFor(() => !player.IsInvulnerable, "no i-frames left", 10f);
            game.field.Enemy.Clear();
            var health = player.Health;
            float lives = health.Current;

            // A bullet right on the hitbox.
            var p = player.Position;
            game.field.Enemy.Spawn(new System.Numerics.Vector2(p.x, p.y), 0f, 0f, 0, 0.1f, 5f);
            yield return Frames(3);
            Assert.That(health.Current, Is.EqualTo(lives - 1f), "one life lost");
            Assert.That(player.IsInvulnerable, Is.True, "i-frames after a hit");

            // A bomb wipes the screen.
            for (int i = 0; i < 300; i++)
                game.field.Enemy.Spawn(new System.Numerics.Vector2(-5f + (i % 30) * 0.33f, 1f + (i / 30) * 0.3f), -1.57f, 0.5f, i % 5, 0.08f, 10f);
            int bombs = player.Bombs;
            Assert.That(bombs, Is.GreaterThan(0));
            Assert.That(game.Bomb(), Is.True);
            yield return null;
            Assert.That(game.field.Enemy.Count, Is.EqualTo(0), "bomb cleared the bullets");
            Assert.That(player.Bombs, Is.EqualTo(bombs - 1));

            // Out of lives: game over, with where the run ended.
            health.Kill();
            yield return null;
            Assert.That(GameRoot.Flow.Current, Is.EqualTo(GameState.GameOver));
            Assert.That(GameRoot.Run.Extra.Any(s => s.label == "Reached"), Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ThreeThousandBulletsStepWithoutErrors()
        {
            yield return StartBulletHell(777);
            var game = BulletHellGame.Current;
            DebugMenu.GodMode = true;
            yield return WaitFor(() => game.Phase == BulletHellGame.RoomPhase.Waves, "the first wave", 20f);
            var pool = game.field.Enemy;
            pool.Clear();
            var b = game.Bounds;
            for (int i = 0; i < 3000; i++)
            {
                float x = b.minX + 0.5f + (i % 60) * ((b.Width - 1f) / 60f);
                float y = b.minY + 1f + (i / 60) * ((b.Height - 2f) / 50f);
                pool.Spawn(new System.Numerics.Vector2(x, y), i * 0.37f, 0.2f, i % 6, 0.08f, 0f, 0f, (i % 3) * 0.3f);
            }
            Assert.That(pool.Count, Is.EqualTo(3000));
            float start = Time.realtimeSinceStartup;
            for (int f = 0; f < 120; f++) yield return null;
            float avgMs = (Time.realtimeSinceStartup - start) * 1000f / 120f;
            TestContext.Out.WriteLine($"Bullet hell perf: 120 frames with {pool.Count} enemy bullets and {game.field.Player.Count} player shots, average {avgMs:0.00} ms per frame (simulation; batch mode draws nothing)");
            Assert.That(pool.Count, Is.GreaterThan(2500), "slow bullets stay on screen");
            Assert.That(pool.Dropped, Is.EqualTo(0), "the pool had room");
            Assert.That(avgMs, Is.LessThan(100f), "no frame-killing work per bullet");
            LogAssert.NoUnexpectedReceived();
        }
    }
}
