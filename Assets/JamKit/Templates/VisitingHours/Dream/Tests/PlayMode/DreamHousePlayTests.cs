using System.Collections;
using JamKit.Presenting;
using JamKit.Shell;
using JamKit.Stealth;
using JamKit.VisitingHours;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static JamKit.Tests.Play.ShellTestUtil;

namespace JamKit.DreamEscape.Tests
{
    /// <summary>Night 2's house on the real controller (docs/redesign/house.md): hide-and-seek starts with the place card
    /// found and Theo counting; being found costs the last memory she found (the house hides it again somewhere else);
    /// the third catch ends the round and Theo asks his question, and the answer is kept; every memory found ends on the
    /// creator's line; the escape's autopilot gets out of the front door onto Chapel Row; a fall throws her out of the
    /// dream with its costs. The save's flags this night writes are cleared before and after each test.</summary>
    public class DreamHousePlayTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return BootToTitle();
            ClearFlags();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            DebugMenu.TimeScale = 1f;
            if (Game != null) Game.SetAutopilot(false);
            if (GameRoot.Exists) ClearFlags();
            PresenterMode.Close();
            yield return null;
        }

        static void ClearFlags()
        {
            var save = GameRoot.Save.Data;
            save.SetEntry("vh_arguments", null);
            save.ClearFlag(DreamRain.DespairFlag);
            save.ClearFlag(DreamHouseStory.CondemnsFlag);
            save.ClearFlag(DreamHouseStory.ThrownOutFlag);
            save.SetEntry(DreamHouseStory.TheoAnswerKey, null);
            save.SetEntry(DreamHouseStory.CatchesKey, null);
            save.SetEntry(DreamHouseStory.CardsKey, null);
            save.SetEntry(DreamJournal.Key, null);
            save.SetEntry(Chain.LenaKey, null);
            DreamHouseGame.DebugSaturdayPaper = false;
        }

        static DreamHouseGame Game => DreamHouseGame.Current;

        static IEnumerator StartHouse()
        {
            Assert.That(GameRoot.Instance.SceneService.Load(DreamHousePresenter.Scene), Is.True, "DreamHouse is in Build Settings");
            yield return WaitFor(() => GameRoot.Flow.InRun && !GameRoot.IsLoadingScene && SceneManager.GetActiveScene().name == DreamHousePresenter.Scene
                && Game != null && Game.House != null, "the house");
            yield return WaitFor(() => GameRoot.Ui.FadeAlpha == 0f, "the scene's fade-in", 5f);
            yield return Frames(3);
        }

        static IEnumerator HideAndSeek()
        {
            yield return StartHouse();
            Assert.That(PresenterMode.Jump(DreamHousePresenter.Scene, DreamHouse.ChapterHideAndSeek), Is.EqualTo(JumpResult.Jumped));
            yield return Frames(3);
            Assert.That(Game.Phase, Is.EqualTo(HousePhase.HideAndSeek));
        }

        [UnityTest]
        public IEnumerator HideAndSeekStartsWithThePlaceCardAndTheoCounting()
        {
            yield return HideAndSeek();
            Assert.That(Game.Theo.Counting, Is.True, "Theo counts at the table");
            Assert.That(Game.Round.IsFound(DreamHouseStory.MemPlaceCard), Is.True, "the place card, found at the table");
            Assert.That(Game.Round.FoundCount, Is.EqualTo(1));
            Assert.That(Game.Hud.CounterText, Is.EqualTo("Memories 1 of " + Game.House.memories.Count), "the counter on screen");
            Assert.That(StealthPlayer.Current, Is.Not.Null);
            Assert.That(StealthPlayer.Current.findable, Is.True);
        }

        [UnityTest]
        public IEnumerator BeingFoundCostsTheLastMemoryFound()
        {
            yield return HideAndSeek();
            Game.Collect(DreamHouseStory.MemCalendar, true);
            Assert.That(Game.Round.IsFound(DreamHouseStory.MemCalendar), Is.True);
            HouseMemoryView view = null;
            foreach (var v in Game.memoryViews) if (v.id == DreamHouseStory.MemCalendar) view = v;
            Assert.That(view, Is.Not.Null);
            Assert.That(view.Found, Is.True, "its glow goes out");
            var was = view.transform.position;
            Game.FoundNow();
            yield return Frames(2);
            Assert.That(Game.Round.Catches, Is.EqualTo(1));
            Assert.That(Game.Round.IsFound(DreamHouseStory.MemCalendar), Is.False, "the house took it back");
            Assert.That(Game.Round.IsFound(DreamHouseStory.MemPlaceCard), Is.True, "not the place card: the last one she found");
            Assert.That(view.Found, Is.False, "glowing again");
            Assert.That(Vector3.Distance(view.transform.position, was), Is.GreaterThan(1f), "hidden somewhere else");
            Assert.That(Game.Theo.Counting, Is.True, "he counts again");
            Assert.That(Game.Hud.CounterText, Is.EqualTo("Memories 1 of " + Game.House.memories.Count));
        }

        [UnityTest]
        public IEnumerator TheThirdCatchEndsTheRoundAndTheoAsksIfDadWillBeSad()
        {
            yield return HideAndSeek();
            DebugMenu.TimeScale = 3f;
            for (int i = 0; i < HideAndSeekRound.MaxCatches; i++)
            {
                Game.FoundNow();
                yield return Frames(2);
            }
            Assert.That(Game.Round.End, Is.EqualTo(RoundEnd.CaughtThreeTimes));
            yield return WaitFor(() => Game.Phase == HousePhase.TheoAsks, "the round over", 10f);
            yield return WaitFor(() => Game.Asking, "his question", 40f);
            Game.AnswerTheo(TheoAnswer.Honest);
            Assert.That(Game.Story.TheoAnswered, Is.EqualTo(TheoAnswer.Honest));
            Assert.That(DreamHouseGame.SavedTheoAnswer, Is.EqualTo("honest"), "kept in the save: he remembers");
        }

        [UnityTest]
        public IEnumerator EveryMemoryFoundEndsOnTheCreatorsLine()
        {
            yield return HideAndSeek();
            foreach (var m in Game.House.memories)
                if (!Game.Round.IsFound(m.id)) Game.Collect(m.id, false);
            Assert.That(Game.Round.AllFound, Is.True);
            Assert.That(Game.Hud.CounterText, Is.EqualTo("Memories " + Game.House.memories.Count + " of " + Game.House.memories.Count));
            string line = DreamHouseLines.AllFound(false);
            Assert.That(line, Does.StartWith(DreamHouseLines.AllFoundStart));
            yield return WaitFor(() => Game.Hud.LineText.Contains(DreamHouseLines.AllFoundStart), "Nadia's line on the last memory", 30f);
            Assert.That(Game.Hud.LineText, Does.Contain(line), "the normal route's ending (no Despair yet)");
            Assert.That(Game.Phase, Is.EqualTo(HousePhase.HideAndSeek), "Theo still has to find her");
        }

        [UnityTest]
        public IEnumerator TheEscapeAutopilotGetsOutOntoChapelRow()
        {
            yield return StartHouse();
            Assert.That(PresenterMode.Jump(DreamHousePresenter.Scene, DreamHouse.ChapterTips), Is.EqualTo(JumpResult.Jumped));
            yield return Frames(3);
            Assert.That(Game.Phase, Is.EqualTo(HousePhase.Escape));
            Assert.That(Game.Chasing, Is.True, "the house folds shut behind her");
            Game.SetAutopilot(true);
            yield return WaitFor(() => Game.Outside || Game.Phase == HousePhase.ThrownOut, "the autopilot out of the front door", 150f);
            Assert.That(Game.Phase, Is.Not.EqualTo(HousePhase.ThrownOut), "not thrown out");
            Assert.That(Game.Outside, Is.True);
            Assert.That(Game.lenaInWindow.activeSelf, Is.True, "Lena at the tower's window");
            yield return WaitFor(() => Game.Phase == HousePhase.Out, "up Chapel Row in the rain", 60f);
            Assert.That(DreamHouseGame.WasThrownOut, Is.False);
        }

        [UnityTest]
        public IEnumerator BeingThrownOutCostsLenaAndTheEnding()
        {
            yield return StartHouse();
            Game.JumpToEscape(Game.Escape.startCheckpoint, ArgumentOutcome.Unsure);
            yield return Frames(3);
            Assert.That(Game.Phase, Is.EqualTo(HousePhase.Escape));
            Game.ThrowOutNow("a test fall");
            yield return Frames(2);
            Assert.That(Game.Phase, Is.EqualTo(HousePhase.ThrownOut));
            Assert.That(Game.Story.Outcome, Is.EqualTo(ArgumentOutcome.Condemns), "Lena's mind is made up");
            Assert.That(DreamHouseGame.WasThrownOut, Is.True);
            Assert.That(DreamHouseGame.LenaCondemned, Is.True);
            Assert.That(Chain.LenaOutcome(GameRoot.Save.Data), Is.EqualTo("Condemns"), "the chain hears it");
            Assert.That(Game.player.InputEnabled, Is.False, "Mags has her");
            yield return WaitFor(() => Game.Hud.LineText.Length > 0, "the fall's line", 5f);
        }
    }
}
