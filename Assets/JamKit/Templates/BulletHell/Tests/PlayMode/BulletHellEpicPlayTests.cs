using System;
using System.Collections;
using System.Linq;
using JamKit.Roguelite;
using JamKit.Shell;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static JamKit.Tests.Play.ShellTestUtil;

namespace JamKit.BulletHell.Tests
{
    /// <summary>The epic balance pass in the real scene: Ghost Step's phase window, Guardian Angel spending
    /// every bomb and cooling down, Last Gambit's ricochets and blasts on the last life, Still Point's ramp,
    /// and Scatter and Blade replacing the main guns.</summary>
    public class BulletHellEpicPlayTests
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

        static IEnumerator StartRun(ulong seed)
        {
            BulletHellGame.SeedOverride = seed;
            Assert.That(GameRoot.Instance.SceneService.Load("BulletHell"), Is.True);
            yield return WaitFor(() => GameRoot.Flow.InRun && !GameRoot.IsLoadingScene && SceneManager.GetActiveScene().name == "BulletHell"
                && BulletHellGame.Current != null && BulletHellGame.Current.Run != null, "the bullet-hell scene");
            var game = BulletHellGame.Current;
            yield return WaitFor(() => !game.StoryLinePending, "the run-start line");
            yield return Until(() => game.Phase == BulletHellGame.RoomPhase.Waves, "the first fight");
        }

        static IEnumerator Until(Func<bool> condition, string what, float seconds = 30f)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > end) Assert.Fail("Timed out waiting for " + what);
                if (GameRoot.Dialogue.IsActive) GameRoot.Dialogue.AutoStep();
                yield return null;
            }
        }

        static UpgradeDef Def(BulletHellGame game, string id) => game.config.upgrades.First(u => u.def.id == id).def;

        static int ShipShots(BulletHellGame game, int style)
        {
            int n = 0;
            var pool = game.field.Player;
            for (int i = 0; i < pool.Count; i++) if (pool[i].style == style) n++;
            return n;
        }

        static IEnumerator BulletOnShip(BulletHellGame game)
        {
            game.Companion.SpendGuard(); // the companion would take it otherwise
            game.field.Enemy.Clear();
            var p = game.player.Position;
            game.field.Enemy.Spawn(new System.Numerics.Vector2(p.x, p.y), 0f, 0f, 0, 0.1f, 5f);
            yield return Frames(3);
        }

        [UnityTest]
        public IEnumerator GhostStepOnlyPhasesInItsWindow()
        {
            yield return StartRun(4242);
            var game = BulletHellGame.Current;
            game.TakeUpgrade(Def(game, "ghost_step"));
            Assert.That(game.DebugEnterRoom(RoomType.Combat), Is.True);
            yield return Until(() => game.Phase == BulletHellGame.RoomPhase.Waves && !game.player.IsInvulnerable, "the fight");
            Assert.That(game.GhostPhases.PhasesLeft, Is.EqualTo(game.config.boosts.ghostPhasesPerRoom), "a fresh room's phases");
            yield return null;
            Assert.That(game.Hud.AbilitiesText, Does.Contain("Ghost"), "shown as pips under SPRINT");

            // A phase (as when a sprint starts): a bullet passes through.
            game.GhostPhases.Tick(0.01f, true, game.config.boosts);
            Assert.That(game.GhostPhases.Phasing, Is.True);
            float lives = game.player.Health.Current;
            yield return BulletOnShip(game);
            Assert.That(game.player.Health.Current, Is.EqualTo(lives), "phasing");
            Assert.That(game.GhostPhases.PhasesLeft, Is.EqualTo(game.config.boosts.ghostPhasesPerRoom - 1));

            // After the window: bullets hit again.
            yield return Until(() => !game.GhostPhases.Phasing, "the window to close", 5f);
            game.field.Enemy.Clear();
            yield return BulletOnShip(game);
            Assert.That(game.player.Health.Current, Is.LessThan(lives), "outside the window it's a normal hit");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator GuardianAngelSpendsEveryBombThenWaits()
        {
            yield return StartRun(4242);
            var game = BulletHellGame.Current;
            var player = game.player;
            game.TakeUpgrade(Def(game, "bomb_bay"));
            game.TakeUpgrade(Def(game, "spare_bomb"));
            game.TakeUpgrade(Def(game, "guardian_angel"));
            Assert.That(player.Bombs, Is.EqualTo(3));
            yield return Until(() => !player.IsInvulnerable, "no i-frames", 10f);
            float lives = player.Health.Current;
            yield return BulletOnShip(game);
            Assert.That(player.Health.Current, Is.EqualTo(lives), "saved");
            Assert.That(player.Bombs, Is.EqualTo(0), "every bomb went");
            Assert.That(game.Guardian.CooldownLeft, Is.GreaterThan(game.config.boosts.guardianCooldown - 1f));
            yield return null;
            Assert.That(game.Hud.AbilitiesText, Does.Contain("Angel").And.Contain("s"), "the cooldown is shown");

            // Inside the cooldown (with a bomb back): the next hit costs a life.
            player.AddBombs(1);
            yield return Until(() => !player.IsInvulnerable, "no i-frames", 10f);
            yield return BulletOnShip(game);
            Assert.That(player.Health.Current, Is.LessThan(lives), "no second save while it recovers");
            Assert.That(player.Bombs, Is.EqualTo(1), "the bomb stays");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator LastGambitRicochetsAndBlastsOnTheLastLife()
        {
            yield return StartRun(12345);
            var game = BulletHellGame.Current;
            DebugMenu.GodMode = true;
            DebugMenu.TimeScale = 3f;
            game.TakeUpgrade(Def(game, "last_gambit"));
            Assert.That(game.GambitActive, Is.False, "not on a full life");
            Assert.That(game.player.PayLives(game.player.Health.Current - 1f), Is.True);
            Assert.That(game.DebugEnterRoom(RoomType.Combat), Is.True);
            yield return Until(() => game.Phase == BulletHellGame.RoomPhase.Waves, "the fight");
            yield return null;
            Assert.That(game.GambitActive, Is.True);
            Assert.That(game.Loadout.DamageBoost, Is.EqualTo(1f + game.config.boosts.lastStandDamage).Within(0.01f));
            Assert.That(game.player.Glow.a, Is.GreaterThan(0f), "the ship pulses red");
            yield return Until(() => game.Ricochets > 0 && game.Blasts > 0, "a ricochet and a kill blast", 40f);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator StillPointRampsWhileTheShipIsStill()
        {
            yield return StartRun(4242);
            var game = BulletHellGame.Current;
            DebugMenu.GodMode = true;
            game.TakeUpgrade(Def(game, "still_point"));
            yield return Frames(2);
            Assert.That(game.player.Moving, Is.False, "no input in a test: still");
            yield return Until(() => game.Boosts.StillCharge >= 1f, "the ramp", 10f);
            Assert.That(game.Loadout.DamageBoost, Is.EqualTo(1f + game.config.boosts.stillMax).Within(0.01f), "double damage");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ScatterAndBladeReplaceTheGuns()
        {
            yield return StartRun(4242);
            var game = BulletHellGame.Current;
            DebugMenu.GodMode = true;
            game.TakeUpgrade(Def(game, "scatter"));
            int shipStyle = game.config.player.shotStyle; // the companion's shots use another style
            game.field.Player.Clear();
            yield return Until(() => ShipShots(game, shipStyle) > 5, "pellets", 10f);
            var pool = game.field.Player;
            bool allPellets = true;
            for (int i = 0; i < pool.Count; i++)
                if (pool[i].style == shipStyle && (pool[i].flags & BulletFlags.Falloff) == 0) allPellets = false;
            Assert.That(allPellets, Is.True, "only scatter pellets (damage falls off with distance)");
            Assert.That(game.Loadout.Inventory.CanTake(Def(game, "blade")), Is.False, "Scatter rules out the Blade");

            // A new run with the Blade: no shots at all, slashes instead.
            GameRoot.Instance.Retry();
            yield return WaitFor(() => GameRoot.Flow.InRun && !GameRoot.IsLoadingScene && BulletHellGame.Current != null
                && BulletHellGame.Current != game && BulletHellGame.Current.Run != null, "the second run");
            game = BulletHellGame.Current;
            yield return WaitFor(() => !game.StoryLinePending, "the run-start line");
            yield return Until(() => game.Phase == BulletHellGame.RoomPhase.Waves, "the first fight");
            game.TakeUpgrade(Def(game, "blade"));
            game.field.Player.Clear();
            yield return Until(() => game.BladeSlashes > 2, "slashes", 10f);
            Assert.That(ShipShots(game, game.config.player.shotStyle), Is.EqualTo(0), "the blade fires no shots");
            LogAssert.NoUnexpectedReceived();
        }
    }
}
