using System;
using System.Collections;
using System.Linq;
using JamKit.Flow;
using JamKit.Roguelite;
using JamKit.Shell;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static JamKit.Tests.Play.ShellTestUtil;

namespace JamKit.BulletHell.Tests
{
    /// <summary>Round 2 in the real scene (docs/DESIGN-ROGUELITE.md): a boss's guaranteed epic choice, a
    /// trial passed and one failed, an altar sacrifice, a forge push, a remnant from a dead run showing up
    /// in the next run's first draft, the companion shooting, guarding and barking, and the Bomb Bay
    /// recharging. Rooms are entered with BulletHellGame.DebugEnterRoom.</summary>
    public class BulletHellRoguelitePlayTests
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
            Assert.That(GameRoot.Instance.SceneService.Load("BulletHell"), Is.True, "BulletHell is in Build Settings");
            yield return WaitFor(() => GameRoot.Flow.InRun && !GameRoot.IsLoadingScene && SceneManager.GetActiveScene().name == "BulletHell"
                && BulletHellGame.Current != null && BulletHellGame.Current.Run != null, "the bullet-hell scene");
            var game = BulletHellGame.Current;
            yield return WaitFor(() => !game.StoryLinePending, "the run-start line to be tried");
            yield return Until(() => game.Phase == BulletHellGame.RoomPhase.Waves, "the first fight");
        }

        /// <summary>Waits for <paramref name="condition"/>, reading through any dialogue on the way.</summary>
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

        static IEnumerator UntilChoice(BulletHellGame game, ChoicePanel.Kind kind, string what) =>
            Until(() => game.Choice.IsOpen && game.Choice.Current == kind, what);

        static UpgradeDef Def(BulletHellGame game, string id) => game.config.upgrades.First(u => u.def.id == id).def;

        static bool AllEpic(BulletHellGame game) => game.Offer.Count > 0 && game.Offer.All(d => d.rarity >= Rarity.Epic);

        [UnityTest]
        public IEnumerator ABossGivesAGuaranteedChoiceOfEpics()
        {
            yield return StartRun(12345);
            var game = BulletHellGame.Current;
            DebugMenu.GodMode = true;
            DebugMenu.TimeScale = 4f;
            Assert.That(game.Run.IsLastFloor, Is.False);
            Assert.That(game.DebugEnterRoom(RoomType.Boss), Is.True);
            yield return Until(() => game.boss.CanBeHurt, "the boss to start fighting");
            game.boss.DebugKill();
            yield return UntilChoice(game, ChoicePanel.Kind.Draft, "the boss reward");
            Assert.That(game.CurrentDraftSource, Is.EqualTo(RewardSource.Boss));
            Assert.That(game.Choice.CardCount, Is.EqualTo(3));
            Assert.That(AllEpic(game), Is.True, "a guaranteed choice of epics: " + string.Join(", ", game.Offer.Select(d => d.id + " " + d.rarity)));
            var pick = game.Offer[0];
            Assert.That(game.Choice.Choose(0), Is.True);
            Assert.That(game.Loadout.Inventory.Has(pick.id), Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ATrialPaysAnEpicAndAFailedOneDoesNot()
        {
            yield return StartRun(4242);
            var game = BulletHellGame.Current;
            DebugMenu.GodMode = true; // no hits: a no-hit, no-sprint or timer trial is met
            DebugMenu.TimeScale = 4f;

            // Take a trial at the board.
            Assert.That(game.DebugEnterRoom(RoomType.Trial), Is.True);
            yield return UntilChoice(game, ChoicePanel.Kind.Trial, "the trial board");
            Assert.That(game.Choice.CardCount, Is.InRange(1, 3));
            int card = -1;
            foreach (var name in new[] { "Untouched", "Feet on the Ground", "Swift Hand" })
                if (card < 0) card = game.Choice.FindCard(name);
            Assert.That(card, Is.GreaterThanOrEqualTo(0), "a trial a god-mode ship can meet is on the board");
            Assert.That(game.Choice.Choose(card), Is.True);
            Assert.That(game.Trials.Count, Is.EqualTo(1));
            Assert.That(game.Hud.TrialsText, Is.Not.Empty, "the HUD lists it");

            // The next fight: cleared at once, the trial passes, an epic choice comes before the battle draft.
            Assert.That(game.DebugEnterRoom(RoomType.Combat), Is.True);
            yield return Until(() => game.Phase == BulletHellGame.RoomPhase.Waves, "the fight");
            Assert.That(game.DebugSkip(), Is.True);
            yield return UntilChoice(game, ChoicePanel.Kind.Draft, "the trial's reward");
            Assert.That(game.CurrentDraftSource, Is.EqualTo(RewardSource.Trial));
            Assert.That(AllEpic(game), Is.True, "a trial pays an epic");
            Assert.That(game.Trials, Is.Empty);
            Assert.That(game.EncounterLog.Any(l => l.StartsWith("trial passed")), Is.True);
            game.Choice.Choose(0);
            yield return UntilChoice(game, ChoicePanel.Kind.Draft, "the battle's own draft");
            Assert.That(game.CurrentDraftSource, Is.EqualTo(RewardSource.Battle));
            Assert.That(game.Offer.All(d => d.rarity <= Rarity.Epic), Is.True);
            game.Choice.Choose(0);

            // A no-hit trial, and a hit (the companion's guard spent first): no epic this time.
            DebugMenu.GodMode = false;
            var t = game.DebugAddTrial(new TrialDef { id = "no_hit_test", displayName = "No hit", description = "x", kind = TrialKind.NoHit, rooms = 1 });
            Assert.That(game.DebugEnterRoom(RoomType.Combat), Is.True);
            yield return Until(() => game.Phase == BulletHellGame.RoomPhase.Waves && !game.player.IsInvulnerable, "the next fight");
            game.Companion.SpendGuard();
            game.field.Enemy.Clear();
            var p = game.player.Position;
            game.field.Enemy.Spawn(new System.Numerics.Vector2(p.x, p.y), 0f, 0f, 0, 0.1f, 5f);
            yield return Frames(3);
            Assert.That(t.State, Is.EqualTo(TrialState.Failed), "the hit failed it");
            DebugMenu.GodMode = true;
            game.DebugSkip();
            yield return UntilChoice(game, ChoicePanel.Kind.Draft, "the battle draft");
            Assert.That(game.CurrentDraftSource, Is.EqualTo(RewardSource.Battle), "no trial reward after a failure");
            Assert.That(game.EncounterLog.Any(l => l == "trial failed: no_hit_test"), Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator TheAltarTakesAnUpgradeOrALifeForAnEpic()
        {
            yield return StartRun(4242);
            var game = BulletHellGame.Current;
            DebugMenu.GodMode = true;
            game.TakeUpgrade(Def(game, "sharp_rounds"));
            Assert.That(game.Loadout.Damage, Is.EqualTo(1.2f).Within(1e-4f));

            Assert.That(game.DebugEnterRoom(RoomType.Altar), Is.True);
            yield return UntilChoice(game, ChoicePanel.Kind.Altar, "the altar");
            int offer = game.Choice.FindCard("Offer an upgrade");
            Assert.That(game.Choice.Choose(offer), Is.True);
            Assert.That(game.Choice.Current, Is.EqualTo(ChoicePanel.Kind.Pick), "pick what to give up");
            Assert.That(game.Choice.Choose(game.Choice.FindCard("Sharp Rounds")), Is.True);
            Assert.That(game.Loadout.Inventory.Has("sharp_rounds"), Is.False, "given up");
            Assert.That(game.Loadout.Damage, Is.EqualTo(1f).Within(1e-4f), "its stats went with it");
            Assert.That(game.Choice.Current, Is.EqualTo(ChoicePanel.Kind.Draft));
            Assert.That(game.CurrentDraftSource, Is.EqualTo(RewardSource.Sacrifice));
            Assert.That(AllEpic(game), Is.True);
            game.Choice.Choose(0);

            float lives = game.player.Health.Current;
            Assert.That(game.DebugEnterRoom(RoomType.Altar), Is.True);
            yield return UntilChoice(game, ChoicePanel.Kind.Altar, "the altar again");
            Assert.That(game.Choice.Choose(game.Choice.FindCard("Offer a life")), Is.True);
            Assert.That(game.player.Health.Current, Is.EqualTo(lives - game.config.altar.lifeCost).Within(1e-4f));
            Assert.That(game.CurrentDraftSource, Is.EqualTo(RewardSource.Sacrifice));
            Assert.That(AllEpic(game), Is.True);
            Assert.That(GameRoot.Flow.Current, Is.Not.EqualTo(GameState.GameOver));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator AForgePushImprovesAnUpgradeAndShowsTheOdds()
        {
            yield return StartRun(4242);
            var game = BulletHellGame.Current;
            DebugMenu.GodMode = true;
            game.TakeUpgrade(Def(game, "sharp_rounds"));
            Assert.That(game.DebugEnterRoom(RoomType.Forge), Is.True);
            yield return Until(() => game.Choice.IsOpen && game.Choice.Current == ChoicePanel.Kind.Pick && game.Choice.Heading == "Forge", "the forge's list");
            Assert.That(game.Choice.Choose(game.Choice.FindCard("Sharp Rounds")), Is.True);
            Assert.That(game.Loadout.Inventory.BonusLevels("sharp_rounds"), Is.EqualTo(1), "the first push is safe");
            Assert.That(game.Loadout.Damage, Is.EqualTo(1.3f).Within(1e-4f), "+half its damage");
            Assert.That(game.Choice.Current, Is.EqualTo(ChoicePanel.Kind.Forge));
            string odds = ForgeRules.Percent(game.config.forge.BreakChance(1));
            Assert.That(game.Choice.Note, Does.Contain(odds), "the next push's odds are shown");
            Assert.That(game.Choice.FindCard("Push again"), Is.GreaterThanOrEqualTo(0));
            Assert.That(game.Choice.Choose(game.Choice.FindCard("Stop here")), Is.True);
            Assert.That(game.Choice.Current, Is.Not.EqualTo(ChoicePanel.Kind.Forge), "left the forge");
            Assert.That(game.EncounterLog, Does.Contain("forge: sharp_rounds +1"));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator TheTraderSwapsTwoCommonsAndAWagerResolvesAfterTheNextFight()
        {
            yield return StartRun(4242);
            var game = BulletHellGame.Current;
            DebugMenu.GodMode = true;
            DebugMenu.TimeScale = 4f;
            var sharp = Def(game, "sharp_rounds");
            game.TakeUpgrade(sharp);
            game.TakeUpgrade(sharp);
            Assert.That(game.DebugEnterRoom(RoomType.Trader), Is.True);
            yield return UntilChoice(game, ChoicePanel.Kind.Trader, "the trader");
            Assert.That(game.Choice.Choose(game.Choice.FindCard("commons for a rare")), Is.True);
            Assert.That(game.Choice.Choose(game.Choice.FindCard("Sharp Rounds")), Is.True, "first common");
            Assert.That(game.Loadout.Inventory.Count("sharp_rounds"), Is.EqualTo(2), "nothing taken until the trade completes");
            Assert.That(game.Choice.Choose(game.Choice.FindCard("Sharp Rounds")), Is.True, "second common (its other stack)");
            Assert.That(game.Loadout.Inventory.Has("sharp_rounds"), Is.False, "both stacks traded");
            Assert.That(game.CurrentDraftSource, Is.EqualTo(RewardSource.Trader));
            Assert.That(game.Offer.All(d => d.rarity == Rarity.Rare), Is.True, "a choice of rares");
            game.Choice.Choose(0);

            Assert.That(game.DebugEnterRoom(RoomType.Wager), Is.True);
            yield return UntilChoice(game, ChoicePanel.Kind.Wager, "the wager");
            int card = game.Choice.FirstEnabledCard();
            Assert.That(card, Is.GreaterThanOrEqualTo(0));
            Assert.That(game.Choice.Choose(card), Is.True);
            Assert.That(game.Trials.Count, Is.EqualTo(1), "the wager's condition runs");
            Assert.That(game.EncounterLog.Any(l => l.StartsWith("wager: ")), Is.True);
            Assert.That(game.DebugEnterRoom(RoomType.Combat), Is.True);
            yield return Until(() => game.Phase == BulletHellGame.RoomPhase.Waves, "the fight");
            game.DebugSkip();
            yield return UntilChoice(game, ChoicePanel.Kind.Draft, "a draft after the fight");
            Assert.That(game.Trials, Is.Empty, "settled after the fight");
            Assert.That(game.Loadout.MoveSpeedScale, Is.EqualTo(1f).Within(1e-4f), "any curse is lifted");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ADeadRunLeavesARemnantThatTheNextRunCanDraft()
        {
            yield return StartRun(777);
            var game = BulletHellGame.Current;
            game.TakeUpgrade(Def(game, "seeker_rounds"));
            game.player.Health.Kill();
            yield return null;
            Assert.That(GameRoot.Flow.Current, Is.EqualTo(GameState.GameOver));
            Assert.That(game.SavedRemnants.Count, Is.EqualTo(1));
            Assert.That(GameRoot.Save.Data.GetEntry(BulletHellGame.RemnantSaveKey), Does.Contain("Echo of Seeker Rounds"), "saved");

            GameRoot.Instance.Retry();
            yield return WaitFor(() => GameRoot.Flow.InRun && !GameRoot.IsLoadingScene && BulletHellGame.Current != null
                && BulletHellGame.Current != game && BulletHellGame.Current.Run != null, "the second run");
            game = BulletHellGame.Current;
            yield return WaitFor(() => !game.StoryLinePending, "the run-start line");
            yield return Until(() => game.Phase == BulletHellGame.RoomPhase.Waves, "the first fight");
            Assert.That(game.SavedRemnants.Count, Is.EqualTo(1), "loaded from the save");
            Assert.That(game.DebugEnterRoom(RoomType.Treasure), Is.True);
            yield return UntilChoice(game, ChoicePanel.Kind.Draft, "the first draft");
            int index = -1;
            for (int i = 0; i < game.Offer.Count; i++) if (JamKit.BulletHell.Remnants.IsRemnant(game.Offer[i])) index = i;
            Assert.That(index, Is.GreaterThanOrEqualTo(0), "the first draft of a run shows a remnant");
            string id = game.Offer[index].id;
            Assert.That(game.Offer[index].effects, Does.Contain(ShmupEffects.Homing), "it echoes the dead run's best upgrade");
            Assert.That(game.Choice.Choose(index), Is.True);
            Assert.That(game.Loadout.Inventory.Has(id), Is.True);
            Assert.That(game.SavedRemnants.Count, Is.EqualTo(0), "used up");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator TheCompanionShootsGuardsAndCallsOutTheBoss()
        {
            yield return StartRun(4242);
            var game = BulletHellGame.Current;
            Assert.That(game.CompanionOn, Is.True);
            DebugMenu.GodMode = true;
            DebugMenu.TimeScale = 2f;
            yield return Until(() => game.CompanionShots > 0, "the companion's first shot", 20f);
            Assert.That(game.Hud.GuardShown, Is.True);

            // It takes a bullet meant for the ship.
            DebugMenu.GodMode = false;
            DebugMenu.TimeScale = 1f;
            yield return Until(() => !game.player.IsInvulnerable, "no i-frames");
            Assert.That(game.Companion.GuardReady, Is.True);
            float lives = game.player.Health.Current;
            game.field.Enemy.Clear();
            var p = game.player.Position;
            game.field.Enemy.Spawn(new System.Numerics.Vector2(p.x, p.y), 0f, 0f, 0, 0.1f, 5f);
            yield return Frames(3);
            Assert.That(game.player.Health.Current, Is.EqualTo(lives), "guarded");
            Assert.That(game.Companion.GuardReady, Is.False, "on its cooldown now");
            Assert.That(game.Companion.Guards, Is.EqualTo(1));
            Assert.That(game.BarksPlayed, Does.Contain("companion_bark_guard"));

            // In a boss fight it calls out the next pattern.
            DebugMenu.GodMode = true;
            DebugMenu.TimeScale = 4f;
            Assert.That(game.DebugEnterRoom(RoomType.Boss), Is.True);
            yield return Until(() => game.BarksPlayed.Any(b => b != "companion_bark_guard"), "a pattern call-out", 40f);
            string bark = game.BarksPlayed.Last();
            Assert.That(bark, Does.StartWith("companion_bark_"));
            Assert.That(game.Hud.BarkText, Is.Not.Empty, "shown on screen");
            Assert.That(GameRoot.Dialogue.IsActive, Is.False, "a bark doesn't stop the game");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator TheBombBayRechargesWhileFighting()
        {
            yield return StartRun(4242);
            var game = BulletHellGame.Current;
            var player = game.player;
            DebugMenu.GodMode = true;
            Assert.That(player.MaxBombs, Is.EqualTo(0), "no bombs in the starting kit");
            Assert.That(game.Bomb(), Is.False);
            game.TakeUpgrade(Def(game, "bomb_bay"));
            Assert.That(player.MaxBombs, Is.EqualTo(game.config.player.bayBombs));
            Assert.That(player.Bombs, Is.EqualTo(player.MaxBombs), "a new bay comes full");
            game.TakeUpgrade(Def(game, "spare_bomb"));
            Assert.That(player.MaxBombs, Is.EqualTo(game.config.player.bayBombs + 1), "Spare Bomb: one more");
            while (player.Bombs > 0) Assert.That(game.Bomb(), Is.True);
            Assert.That(game.Bomb(), Is.False, "empty");

            // A long fight: the bay recharges one bomb every 20 s of fighting.
            DebugMenu.TimeScale = 4f;
            Assert.That(game.DebugEnterRoom(RoomType.Boss), Is.True);
            yield return Until(() => game.boss.CanBeHurt, "the boss fight");
            yield return Until(() => player.Bombs >= 1, "a recharged bomb", 30f);
            Assert.That(player.Bay.RechargeFraction, Is.LessThan(1f));
            LogAssert.NoUnexpectedReceived();
        }
    }
}
