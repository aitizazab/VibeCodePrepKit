using System.Collections;
using System.Linq;
using JamKit.Flow;
using JamKit.Shell;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static JamKit.Tests.Play.ShellTestUtil;

namespace JamKit.Doomlike.Tests
{
    /// <summary>The DOOM-like in its real scene, seeded: the player moves, an enemy notices and
    /// attacks, a locked door opens only with its key, the guns kill, dying ends the run, a skip
    /// through every level (god mode, drafts taken) reaches Victory, and 40 awake enemies step
    /// without trouble (timing reported through TestContext).</summary>
    public class DoomlikePlayTests
    {
        [UnitySetUp]
        public IEnumerator SetUp() => BootToTitle();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            DebugMenu.GodMode = false;
            DebugMenu.TimeScale = 1f;
            DoomGame.SeedOverride = 0;
            DoomGame.SkipClickToPlay = false;
            DoomGame.StartLevel = 0;
            yield return null;
        }

        static IEnumerator StartDoomlike(ulong seed)
        {
            DoomGame.SeedOverride = seed;
            DoomGame.SkipClickToPlay = true;
            Assert.That(GameRoot.Instance.SceneService.Load("Doomlike"), Is.True, "Doomlike is in Build Settings");
            yield return WaitFor(() => GameRoot.Flow.InRun && !GameRoot.IsLoadingScene && SceneManager.GetActiveScene().name == "Doomlike"
                && DoomGame.Current != null && DoomGame.Current.Level != null && DoomGame.Current.CurrentPhase == DoomGame.Phase.Playing, "the DOOM-like scene");
            Assert.That(DoomGame.Current.Seed, Is.EqualTo(seed));
            yield return Frames(2);
        }

        [UnityTest]
        public IEnumerator PlayerMovesAndAnEnemySeesAndAttacks()
        {
            yield return StartDoomlike(101);
            var game = DoomGame.Current;
            var player = game.player;
            DebugMenu.GodMode = true;
            Assert.That(GameRoot.Hud.IsShown, Is.True, "HUD on");
            Assert.That(game.Level.navMeshBuilt, Is.True, "the level has a NavMesh");
            Assert.That(game.Enemies.Count, Is.GreaterThan(5));
            Assert.That(game.Hud.HealthText, Is.EqualTo("100"));

            // The controller really moves.
            Vector3 start = player.transform.position;
            player.UseScriptedMove = true;
            player.ScriptedMove = new Vector2(0f, 1f);
            yield return Seconds(0.6f);
            player.UseScriptedMove = false;
            Assert.That(player.DistanceMoved, Is.GreaterThan(1.5f), "ran forward");
            Assert.That(Vector3.Distance(start, player.transform.position), Is.GreaterThan(1.5f));
            Assert.That(player.Speed, Is.GreaterThan(0f));
            yield return Seconds(0.3f);

            // A sleeping husk a few metres away, facing the player: it notices, closes in and swings.
            var husk = game.config.enemies.First(e => e.attack.kind == EnemyAttackKind.Melee && !e.isBoss);
            int before = game.Enemies.Count;
            Assert.That(game.DebugSpawnEnemies(1, player.transform.position + player.transform.forward * 0.01f, 5f, husk, false), Is.EqualTo(1));
            var enemy = game.Enemies[before];
            Assert.That(enemy.State, Is.EqualTo(EnemyState.Idle));
            yield return WaitFor(() => enemy.State != EnemyState.Idle, "the husk to notice the player", 5f);
            int hitsBefore = game.PlayerHitsTaken;
            yield return WaitFor(() => enemy.AttacksMade > 0 && game.PlayerHitsTaken > hitsBefore, "the husk's attack to land", 12f);
            Assert.That(player.Health.Current, Is.EqualTo(player.Health.Max), "god mode: no damage");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator LockedDoorOpensOnlyWithItsKey()
        {
            yield return StartDoomlike(202);
            var game = DoomGame.Current;
            var player = game.player;
            DebugMenu.GodMode = true;
            var level = game.Level;
            var door = level.doors.First(d => d.kind == DoomDoor.DoorKind.Locked);
            Assert.That(player.HasKey(door.keyColor), Is.False);

            // Stand right in front of it, on its open side.
            var map = level.map;
            Vector3 c = level.CellCenter(door.cellX, door.cellY);
            int[] dx = { 1, -1, 0, 0 }, dy = { 0, 0, 1, -1 };
            Vector3 approach = c;
            for (int i = 0; i < 4; i++)
                if (map.IsOpen(door.cellX + dx[i], door.cellY + dy[i]) && map.KindAt(door.cellX + dx[i], door.cellY + dy[i]) != CellKind.LockedDoor)
                {
                    approach = Vector3.Lerp(c, level.CellCenter(door.cellX + dx[i], door.cellY + dy[i]), 0.62f);
                    break;
                }
            player.Teleport(approach + Vector3.up * 0.05f);
            yield return Seconds(0.8f);
            Assert.That(door.IsOpening, Is.False, "no key: stays shut");
            Assert.That(GameRoot.Hud.MessageText, Does.Contain(MapLegend.KeyName(door.keyColor) + " key"), "the message says which key");

            player.GiveKey(door.keyColor);
            yield return Frames(3);
            Assert.That(door.IsOpening, Is.True, "with the key it opens");
            yield return Seconds(1f);
            Assert.That(door.IsOpen, Is.True);
            Assert.That(door.obstacle == null || !door.obstacle.enabled, Is.True, "enemies can path through now");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator GunsKillAndCountTheKill()
        {
            yield return StartDoomlike(303);
            var game = DoomGame.Current;
            var player = game.player;
            DebugMenu.GodMode = true;
            game.GiveAll();
            var shotgun = game.config.weapons.First(w => w.pellets >= 6);
            var gunner = game.config.enemies.First(e => e.attack.kind == EnemyAttackKind.Hitscan);
            int before = game.Enemies.Count;
            Vector3 front = player.transform.position + player.transform.forward * 3.5f;
            Assert.That(game.DebugSpawnEnemies(1, front, 0.01f, gunner, false), Is.EqualTo(1));
            var target = game.Enemies[before];
            yield return Frames(2);
            player.FaceYaw(Quaternion.LookRotation(target.transform.position - player.transform.position).eulerAngles.y);
            yield return Frames(2);
            int kills = game.Tally.kills;
            int markers = game.Hud.HitMarkers;
            for (int i = 0; i < 6 && target.Alive; i++)
            {
                game.Shoot(shotgun);
                yield return Frames(2);
            }
            Assert.That(target.Alive, Is.False, "point-blank scattergun blasts kill a gunner");
            Assert.That(game.Hud.HitMarkers, Is.GreaterThan(markers), "hit markers");
            yield return Frames(2);
            Assert.That(game.Tally.kills, Is.EqualTo(kills + 1), "the kill counts");
            Assert.That(game.Score, Is.GreaterThan(0));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator DyingEndsTheRun()
        {
            yield return StartDoomlike(404);
            var game = DoomGame.Current;
            game.DamagePlayer(9999f, game.player.transform.position + Vector3.forward, JamKit.Combat.DamageKind.Generic);
            Assert.That(game.player.Alive, Is.False);
            Assert.That(game.CurrentPhase, Is.EqualTo(DoomGame.Phase.Dying));
            yield return WaitFor(() => GameRoot.Flow.Current == GameState.GameOver, "game over", 6f);
            Assert.That(GameRoot.Run.Extra.Any(s => s.label == "Reached"), Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator SkipThroughEveryLevelReachesVictory()
        {
            yield return StartDoomlike(12345);
            var game = DoomGame.Current;
            DebugMenu.GodMode = true;
            int levels = game.config.LevelCount;
            Assert.That(levels, Is.EqualTo(3));
            int seenLevel = -1, levelsSeen = 0, picked = 0;
            float end = Time.realtimeSinceStartup + 90f;
            while (GameRoot.Flow.Current != GameState.Victory)
            {
                Assert.That(GameRoot.Flow.Current, Is.Not.EqualTo(GameState.GameOver), "god mode: no game over");
                if (Time.realtimeSinceStartup > end)
                    Assert.Fail($"No victory after 90 s: level {game.LevelIndex + 1}, phase {game.CurrentPhase}, state {GameRoot.Flow.Current}");
                if (GameRoot.Flow.Current == GameState.Playing)
                {
                    if (game.CurrentPhase == DoomGame.Phase.Playing && game.LevelIndex != seenLevel)
                    {
                        seenLevel = game.LevelIndex;
                        levelsSeen++;
                        Assert.That(game.Level.navMeshBuilt, Is.True, "level " + (seenLevel + 1) + " NavMesh");
                        Assert.That(game.Enemies.Count, Is.GreaterThan(0), "level " + (seenLevel + 1) + " enemies");
                        yield return Seconds(0.5f); // a few frames of real play on each level
                    }
                    if (game.CurrentPhase == DoomGame.Phase.Draft)
                    {
                        Assert.That(game.Choice.CardCount, Is.InRange(1, 3), "1 to 3 cards");
                        int owned = game.Inventory.Owned.Sum(d => game.Inventory.Count(d.id));
                        Assert.That(game.Choice.Choose(0), Is.True);
                        picked++;
                        Assert.That(game.Inventory.Owned.Sum(d => game.Inventory.Count(d.id)), Is.EqualTo(owned + 1), "the pick added one stack");
                    }
                    else game.DebugSkip();
                }
                yield return null;
            }
            Assert.That(levelsSeen, Is.EqualTo(levels), "played every level");
            Assert.That(game.LevelsCleared, Is.EqualTo(levels));
            Assert.That(picked, Is.EqualTo(levels - 1), "a draft between levels");
            Assert.That(game.DraftsTaken, Is.EqualTo(picked));
            Assert.That(GameRoot.Ui.Victory.IsVisible, Is.True);
            Assert.That(GameRoot.Run.Extra.Any(s => s.label == "Levels cleared" && s.value == "3 / 3"), Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator FortyAwakeEnemiesStepWithoutErrors()
        {
            yield return StartDoomlike(777);
            var game = DoomGame.Current;
            DebugMenu.GodMode = true;
            // Half biters, half shooters (fireball spitters), all awake: walking, pathing, sight checks
            // and a stream of projectiles at the player.
            var biter = game.config.enemies.First(e => e.attack.kind == EnemyAttackKind.Melee && !e.isBoss);
            var shooter = game.config.enemies.First(e => e.attack.kind == EnemyAttackKind.Projectile && !e.isBoss);
            Vector3 centre = game.player.transform.position;
            int placed = game.DebugSpawnEnemies(20, centre, 4f, biter) + game.DebugSpawnEnemies(20, centre, 6.5f, shooter);
            Assert.That(placed, Is.GreaterThanOrEqualTo(30), "most of the rings fit on the NavMesh");
            int alive = game.Enemies.Count(e => e.Alive);
            // At least 120 frames and 4 seconds (batch mode can run frames very fast).
            float start = Time.realtimeSinceStartup;
            int frames = 0, maxShots = 0;
            while (frames < 120 || Time.realtimeSinceStartup - start < 4f)
            {
                frames++;
                maxShots = Mathf.Max(maxShots, game.projectiles.Count);
                yield return null;
            }
            float avgMs = (Time.realtimeSinceStartup - start) * 1000f / frames;
            int fired = game.Enemies.Sum(e => e.AttacksMade);
            TestContext.Out.WriteLine($"DOOM-like perf: {frames} frames with {alive} enemies ({placed} spawned awake around the player: half biters, half shooters), "
                + $"{fired} attacks, up to {maxShots} shots in flight, {game.PlayerHitsTaken} hits on the player, average {avgMs:0.00} ms per frame (simulation; batch mode draws nothing)");
            Assert.That(game.Enemies.Count(e => e.Alive), Is.EqualTo(alive), "god mode, nobody shooting back: all still alive");
            Assert.That(maxShots, Is.GreaterThan(0), "the shooters fired projectiles");
            Assert.That(game.projectiles.Dropped, Is.EqualTo(0), "the shot pool had room");
            Assert.That(avgMs, Is.LessThan(100f), "no frame-killing work per enemy");
            Assert.That(game.PlayerHitsTaken, Is.GreaterThan(0), "they attacked");
            LogAssert.NoUnexpectedReceived();
        }

        static IEnumerator Seconds(float s)
        {
            float end = Time.realtimeSinceStartup + s;
            while (Time.realtimeSinceStartup < end) yield return null;
        }
    }
}
