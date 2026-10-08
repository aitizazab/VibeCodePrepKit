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
            // The run-start story line (if the ink has one) plays on the first frame: read through it.
            var game = BulletHellGame.Current;
            yield return WaitFor(() => !game.StoryLinePending, "the run-start line to be tried");
            for (int i = 0; i < 200 && GameRoot.Dialogue.IsActive; i++)
            {
                GameRoot.Dialogue.AutoStep();
                yield return null;
            }
            Assert.That(GameRoot.Dialogue.IsActive, Is.False, "run-start line finished");
        }

        /// <summary>Mashes through any dialogue on screen.</summary>
        static IEnumerator FinishLines()
        {
            for (int i = 0; i < 400 && GameRoot.Dialogue.IsActive; i++)
            {
                GameRoot.Dialogue.AutoStep();
                yield return null;
            }
            yield return null;
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
            // The placeholder story hooks played, and the ones nobody wrote stayed silent.
            var knots = game.StoryKnotsPlayed;
            Assert.That(knots, Does.Contain("run_start"));
            Assert.That(knots, Does.Contain("boss_gatekeeper_intro"));
            Assert.That(knots, Does.Contain("boss_gatekeeper_defeat"));
            Assert.That(knots.Any(k => k.StartsWith("boss_forgemother") || k.StartsWith("boss_the_last_light")), Is.False, "no lines written for the other bosses yet");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator BossLinesPauseTheGameAndLeaveNoBulletsBehind()
        {
            yield return StartBulletHell(12345);
            var game = BulletHellGame.Current;
            var player = game.player;
            DebugMenu.GodMode = true;
            DebugMenu.TimeScale = 4f;

            // Skip ahead to the first boss's intro line.
            float end = Time.realtimeSinceStartup + 120f;
            while (!(game.Phase == BulletHellGame.RoomPhase.Boss && game.InStoryLine))
            {
                if (Time.realtimeSinceStartup > end) Assert.Fail("never reached the first boss's line (phase " + game.Phase + ")");
                if (GameRoot.Dialogue.IsActive) GameRoot.Dialogue.AutoStep();
                else if (GameRoot.Flow.Current == GameState.Playing)
                {
                    if (game.Choice.IsOpen && game.Choice.Current == ChoicePanel.Kind.Draft) game.Choice.Choose(0);
                    else game.DebugSkip();
                }
                yield return null;
            }
            DebugMenu.GodMode = false;
            Assert.That(game.StoryKnotsPlayed[game.StoryKnotsPlayed.Count - 1], Is.EqualTo("boss_gatekeeper_intro"));
            yield return null;
            Assert.That(GameRoot.Flow.Current, Is.EqualTo(GameState.Dialogue), "the line stops the game");
            Assert.That(Time.timeScale, Is.EqualTo(0f));

            // A bullet sitting on the ship during the line: nothing moves, nothing hits...
            float lives = player.Health.Current;
            var p = player.Position;
            game.field.Enemy.Spawn(new System.Numerics.Vector2(p.x, p.y), 0f, 0f, 0, 0.2f, 30f);
            game.field.Enemy.Spawn(new System.Numerics.Vector2(p.x, p.y + 1f), -1.57f, 2f, 0, 0.2f, 30f);
            yield return Frames(5);
            Assert.That(player.Health.Current, Is.EqualTo(lives), "frozen during the line");
            Assert.That(game.field.Enemy.Count, Is.EqualTo(2));

            // ...and when the line ends, the bullets are gone and the ship has a moment.
            yield return FinishLines();
            Assert.That(GameRoot.Flow.Current, Is.EqualTo(GameState.Playing));
            Assert.That(game.field.Enemy.Count, Is.EqualTo(0), "bullets cleared after a boss line");
            Assert.That(player.IsInvulnerable, Is.True, "a moment of safety after the line");
            Assert.That(player.Health.Current, Is.EqualTo(lives));

            // A phase change has its own line (boss_gatekeeper_phase_2), also cleanly paused.
            DebugMenu.GodMode = true;
            yield return WaitFor(() => game.boss.CanBeHurt, "the boss to start fighting", 20f);
            var def = game.boss.Def;
            Assert.That(def.phases.Count, Is.GreaterThan(1));
            float target = def.phases[1].startsAtHealth;
            for (int i = 0; i < 400 && !game.InStoryLine && game.boss.HealthFraction > target - 0.01f; i++)
            {
                game.boss.Damage(game.boss.Health.Max * 0.02f);
                yield return null;
            }
            Assert.That(game.InStoryLine, Is.True, "the phase change played a line");
            Assert.That(game.StoryKnotsPlayed[game.StoryKnotsPlayed.Count - 1], Is.EqualTo("boss_gatekeeper_phase_2"));
            yield return FinishLines();
            Assert.That(game.field.Enemy.Count, Is.EqualTo(0));

            // Last words, then the room clears.
            game.boss.DebugKill();
            yield return WaitFor(() => game.InStoryLine || game.Phase != BulletHellGame.RoomPhase.Boss, "the boss to fall", 20f);
            Assert.That(game.StoryKnotsPlayed[game.StoryKnotsPlayed.Count - 1], Is.EqualTo("boss_gatekeeper_defeat"));
            yield return FinishLines();
            yield return WaitFor(() => game.Phase != BulletHellGame.RoomPhase.Boss, "the room to clear after the last words", 10f);
            Assert.That(game.BossesDefeated, Is.EqualTo(1));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator SprintBudgetRefillsEachFightRoomAndTheMeterShowsIt()
        {
            yield return StartBulletHell(4242);
            var game = BulletHellGame.Current;
            var sprint = game.player.Sprint;
            DebugMenu.GodMode = true;
            yield return WaitFor(() => game.Phase == BulletHellGame.RoomPhase.Waves, "the first fight", 20f);
            yield return null;
            Assert.That(sprint.BudgetFraction, Is.EqualTo(1f), "a fight room starts with its full sprint budget");
            Assert.That(sprint.RoomBudgetMax, Is.EqualTo(game.config.player.sprint.roomBudgetSeconds).Within(1e-4));
            Assert.That(game.Hud.SprintTankShown, Is.EqualTo(1f).Within(0.01f));
            Assert.That(game.player.SprintMultiplier, Is.EqualTo(game.config.player.sprint.speedMultiplier).Within(1e-4));

            // Spend the whole room's budget (as if Shift had been held in bursts).
            for (int i = 0; i < 40 && !sprint.BudgetEmpty; i++)
            {
                sprint.Tick(sprint.Stamina, true, true);
                sprint.Tick(5f, false, false);
            }
            Assert.That(sprint.BudgetEmpty, Is.True);
            Assert.That(sprint.CanSprint, Is.False, "no sprint left in this room");
            yield return null;
            Assert.That(game.Hud.SprintTankShown, Is.EqualTo(0f).Within(0.01f), "the meter's tank is empty");

            // The next fight room fills it again.
            int room = game.Run.step, floor = game.Run.floor;
            float end = Time.realtimeSinceStartup + 60f;
            while (!((game.Run.step != room || game.Run.floor != floor) && game.Phase == BulletHellGame.RoomPhase.Waves))
            {
                if (Time.realtimeSinceStartup > end) Assert.Fail("never reached the next fight room");
                if (GameRoot.Dialogue.IsActive) GameRoot.Dialogue.AutoStep();
                else if (GameRoot.Flow.Current == GameState.Playing)
                {
                    if (game.Choice.IsOpen && game.Choice.Current == ChoicePanel.Kind.Draft) game.Choice.Choose(0);
                    else if (game.Phase != BulletHellGame.RoomPhase.Waves || game.Run.step == room) game.DebugSkip();
                }
                yield return null;
            }
            Assert.That(sprint.BudgetFraction, Is.EqualTo(1f), "refilled for the new room");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator TheSprintHintShowsOnTheFirstRunOnly()
        {
            yield return StartBulletHell(4242);
            var game = BulletHellGame.Current;
            Assert.That(GameRoot.Save.Data.HasFlag(BulletHellGame.SprintHintFlag), Is.False, "fresh save");
            Assert.That(game.Hud.LegendShown, Is.True, "the control legend is on by default");
            yield return WaitFor(() => game.Phase == BulletHellGame.RoomPhase.Waves, "the first fight", 20f);
            yield return null;
            Assert.That(game.SprintHint.IsVisible, Is.True, "the hint shows in the first fight");
            Assert.That(game.Hud.SprintHintShown, Is.True);
            Assert.That(GameRoot.Save.Data.HasFlag(BulletHellGame.SprintHintFlag), Is.True, "remembered at once");
            DebugMenu.GodMode = true;
            DebugMenu.TimeScale = 4f;
            yield return WaitFor(() => !game.SprintHint.IsVisible, "the hint to time out (~8 s of game time)", 15f);
            Assert.That(game.Hud.SprintHintShown, Is.False);

            // The next run (same save): no hint. And the legend follows GameConfig.
            GameRoot.Config.showControlLegend = false; // an in-memory copy (ShellTestUtil.UseSandboxConfig)
            GameRoot.Instance.Retry();
            yield return WaitFor(() => GameRoot.Flow.InRun && !GameRoot.IsLoadingScene && BulletHellGame.Current != null
                && BulletHellGame.Current != game && BulletHellGame.Current.Run != null, "the second run");
            game = BulletHellGame.Current;
            yield return WaitFor(() => !game.StoryLinePending, "the run-start line");
            yield return FinishLines();
            Assert.That(game.Hud.LegendShown, Is.False, "GameConfig.showControlLegend off");
            yield return WaitFor(() => game.Phase == BulletHellGame.RoomPhase.Waves, "the first fight", 20f);
            yield return null;
            Assert.That(game.SprintHint.IsVisible, Is.False, "second run: no hint");
            Assert.That(game.Hud.SprintHintShown, Is.False);
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
            game.Companion.SpendGuard(); // otherwise the companion takes the first bullet

            // A bullet right on the hitbox.
            var p = player.Position;
            game.field.Enemy.Spawn(new System.Numerics.Vector2(p.x, p.y), 0f, 0f, 0, 0.1f, 5f);
            yield return Frames(3);
            Assert.That(health.Current, Is.EqualTo(lives - 1f), "one life lost");
            Assert.That(player.IsInvulnerable, Is.True, "i-frames after a hit");

            // A bomb wipes the screen (bombs come from the Bomb Bay epic now).
            Assert.That(player.Bombs, Is.EqualTo(0), "no bombs in the starting kit");
            game.TakeUpgrade(game.config.upgrades.First(u => u.def.id == "bomb_bay").def);
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
            // The death screen's line comes from ink (death_<killer> -> death_floor_<n> -> death).
            Assert.That(game.Killer, Is.EqualTo("battle"), "died in a battle room");
            Assert.That(game.StoryKnotsPlayed, Does.Contain("death"));
            Assert.That(GameRoot.Run.quote, Does.Contain("Placeholder"), "the placeholder death line");
            Assert.That(GameRoot.Ui.GameOver.QuoteText, Does.Contain(GameRoot.Run.quote), "shown on the Game Over screen");
            Assert.That(GameRoot.Dialogue.IsActive, Is.False, "read, not played as a dialogue");
            // ...and the run leaves a remnant (its best upgrade, weakened, plus a stat from how it died).
            Assert.That(game.SavedRemnants.Count, Is.EqualTo(1));
            Assert.That(GameRoot.Save.Data.GetEntry(BulletHellGame.RemnantSaveKey), Does.Contain("Echo of Bomb Bay"));
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
