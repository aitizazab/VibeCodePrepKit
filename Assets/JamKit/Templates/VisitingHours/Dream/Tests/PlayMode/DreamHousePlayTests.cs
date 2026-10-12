using System.Collections;
using JamKit.Presenting;
using JamKit.Shell;
using JamKit.Stealth;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static JamKit.Tests.Play.ShellTestUtil;

namespace JamKit.DreamEscape.Tests
{
    /// <summary>Night 2's house on the real controller: the autopilot gets through the way in (the hallway stretches, the
    /// slide into the kitchen) and the pancakes start hide-and-seek; Theo finds a player standing in the open and counts
    /// again; the memories count down and the last one says Nadia's line; the argument's debug choice "condemns" is kept
    /// and brings the rain's point of no return nearer; the autopilot escapes the house tipping, out of the front door
    /// under the umbrella. The save's Despair and condemns flags are cleared before and after each test.</summary>
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
            GameRoot.Save.Data.SetEntry("vh_arguments", null);
            GameRoot.Save.Data.ClearFlag(DreamRain.DespairFlag);
            GameRoot.Save.Data.ClearFlag(DreamHouseStory.CondemnsFlag);
            GameRoot.Save.Data.SetEntry(DreamHouseStory.TheoAnswerKey, null);
            GameRoot.Save.Data.SetEntry(DreamJournal.Key, null);
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

        [UnityTest]
        public IEnumerator TheWayInLeadsToPancakesAndHideAndSeek()
        {
            yield return StartHouse();
            Assert.That(Game.Phase, Is.EqualTo(HousePhase.WayIn));
            Assert.That(Game.Checkpoint, Is.EqualTo(Game.House.arrivalCheckpoint));
            DebugMenu.TimeScale = 2f;
            int respawns = Game.Respawns;
            Game.SetAutopilot(true);
            yield return WaitFor(() => Game.Phase != HousePhase.WayIn, "the autopilot down the slide into the kitchen", 40f);
            Game.SetAutopilot(false);
            Assert.That(Game.HallwayStretched, Is.True, "the hallway stretched as she entered");
            Assert.That(Game.hallwayEnd.position.z, Is.GreaterThan(Game.House.stretchFrom + 1f));
            Assert.That(Game.Respawns - respawns, Is.LessThanOrEqualTo(1), "at most one slip");
            Assert.That(Game.Phase, Is.EqualTo(HousePhase.Pancakes));
            Assert.That(Game.LineText, Does.Contain(DreamHouseLines.Placeholder), "the pancake lines, marked placeholders");
            Assert.That(Game.theo.Frozen, Is.True, "Theo waits at the table");
            Game.SkipLines();
            yield return Frames(2);
            Assert.That(Game.Phase, Is.EqualTo(HousePhase.HideAndSeek));
            Assert.That(Game.theo.Counting, Is.True, "Theo counts");
            Assert.That(StealthPlayer.Current.findable, Is.True);
        }

        [UnityTest]
        public IEnumerator TheoFindsAPlayerStandingInTheOpenAndCountsAgain()
        {
            yield return StartHouse();
            Assert.That(PresenterMode.Jump(DreamHousePresenter.Scene, DreamHouse.ChapterHideAndSeek), Is.EqualTo(JumpResult.Jumped));
            yield return Frames(3);
            Assert.That(Game.Phase, Is.EqualTo(HousePhase.HideAndSeek));
            var open = Game.House.openFloor[0];
            Game.player.Teleport(new Vector3(open.X, open.Y + 0.02f, open.Z), 0f, Vector3.zero);
            DebugMenu.TimeScale = 3f;
            yield return WaitFor(() => Game.Story.TimesFound > 0, "Theo to find her standing in the living room", 60f);
            Assert.That(Game.Phase, Is.EqualTo(HousePhase.HideAndSeek), "found before every memory: another round");
            Assert.That(Game.LineText, Does.Contain(DreamHouseLines.Theo));
            yield return WaitFor(() => !Game.Speaking, "his line", 10f);
            // The first time, he asks his question: answer it, and he counts again.
            if (Game.AskingTheo) Game.AnswerTheo(TheoAnswer.Comfort);
            yield return WaitFor(() => !Game.Speaking, "the answer and his reply", 12f);
            yield return Frames(2);
            Assert.That(Game.theo.Counting, Is.True, "he counts again");
            Assert.That(Game.Story.Rounds, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator TheMemoriesCountDownAndTheLastOneSpeaks()
        {
            yield return StartHouse();
            Assert.That(PresenterMode.Jump(DreamHousePresenter.Scene, DreamHouse.ChapterHideAndSeek), Is.EqualTo(JumpResult.Jumped));
            yield return Frames(3);
            var h = Game.House;
            Assert.That(Game.memories.Total, Is.EqualTo(5));
            Assert.That(Game.memories.Remaining, Is.EqualTo(5));
            for (int i = 0; i < h.memories.Count; i++)
            {
                var m = h.memories[i].standAt;   // where she stands to take it (one is on Theo's desk)
                Game.player.Teleport(new Vector3(m.X, m.Y + 0.02f, m.Z), 0f, Vector3.zero);
                int left = 5 - i - 1;
                yield return WaitFor(() => Game.memories.Remaining == left, h.memories[i].name + " picked up", 5f);
            }
            Assert.That(Game.memories.AllCollected, Is.True);
            Assert.That(Game.Story.AllMemoriesFound, Is.True);
            Assert.That(Game.LineText, Does.Contain(DreamHouseLines.AllFoundStart), "Nadia's line on the last memory");
            Assert.That(Game.LineText, Does.EndWith(DreamHouseLines.AllFoundEndings[1 - DreamHouseLines.DespairEnding]), "the normal route's ending (no Despair yet)");
        }

        [UnityTest]
        public IEnumerator TheCounterReadsTwoOfFive()
        {
            yield return StartHouse();
            Assert.That(PresenterMode.Jump(DreamHousePresenter.Scene, DreamHouse.ChapterHideAndSeek), Is.EqualTo(JumpResult.Jumped));
            yield return Frames(3);
            Assert.That(Game.memories.Summary, Is.EqualTo("Memories: 0 of 5"));
            var h = Game.House;
            for (int i = 0; i < 2; i++)
            {
                var m = h.memories[i].standAt;
                Game.player.Teleport(new Vector3(m.X, m.Y + 0.02f, m.Z), 0f, Vector3.zero);
                int left = 5 - i - 1;
                yield return WaitFor(() => Game.memories.Remaining == left, h.memories[i].name + " picked up", 5f);
            }
            Assert.That(Game.memories.Summary, Is.EqualTo("Memories: 2 of 5"), "the on-screen counter");
            Assert.That(Game.Evidence.Count, Is.EqualTo(2), "what she'd carry into the battle so far");
        }

        [UnityTest]
        public IEnumerator TheLastMemoryLineOnTheDespairRouteLongsForHisSmile()
        {
            GameRoot.Save.Data.SetFlag(DreamRain.DespairFlag);
            yield return StartHouse();
            Assert.That(PresenterMode.Jump(DreamHousePresenter.Scene, DreamHouse.ChapterHideAndSeek), Is.EqualTo(JumpResult.Jumped));
            yield return Frames(3);
            var h = Game.House;
            for (int i = 0; i < h.memories.Count; i++)
            {
                var m = h.memories[i].standAt;
                Game.player.Teleport(new Vector3(m.X, m.Y + 0.02f, m.Z), 0f, Vector3.zero);
                int left = 5 - i - 1;
                yield return WaitFor(() => Game.memories.Remaining == left, h.memories[i].name + " picked up", 5f);
            }
            Assert.That(Game.LineText, Does.Contain(DreamHouseLines.AllFoundStart));
            Assert.That(Game.LineText, Does.EndWith("it would be nice to see his smile again."), "the Despair route's ending");
            Assert.That(Game.LineText, Does.Not.Contain(DreamHouseLines.Placeholder), "the creator's line as written");
        }

        [UnityTest]
        public IEnumerator TheoAsksIfDadWillBeSadAndRemembersTheAnswer()
        {
            yield return StartHouse();
            Assert.That(PresenterMode.Jump(DreamHousePresenter.Scene, DreamHouse.ChapterHideAndSeek), Is.EqualTo(JumpResult.Jumped));
            yield return Frames(3);
            var open = Game.House.openFloor[0];
            Game.player.Teleport(new Vector3(open.X, open.Y + 0.02f, open.Z), 0f, Vector3.zero);
            DebugMenu.TimeScale = 3f;
            yield return WaitFor(() => Game.Story.TimesFound > 0, "Theo to find her", 60f);
            Assert.That(Game.LineText, Does.Contain(DreamHouseLines.TheoQuestion), "his question");
            yield return WaitFor(() => Game.AskingTheo, "the answer prompt", 10f);
            Game.AnswerTheo(TheoAnswer.Honest);
            Assert.That(Game.Story.TheoAnswered, Is.EqualTo(TheoAnswer.Honest));
            Assert.That(DreamHouseGame.SavedTheoAnswer, Is.EqualTo("honest"), "kept in the save: he remembers");
            Assert.That(GameRoot.Save.Data.GetEntry(DreamHouseStory.TheoAnswerKey), Is.EqualTo("honest"));
            yield return WaitFor(() => !Game.Speaking, "her answer and his reply", 12f);
            yield return Frames(2);
            Assert.That(Game.theo.Counting, Is.True, "he counts again");
            Assert.That(Game.AskingTheo, Is.False);
        }

        [UnityTest]
        public IEnumerator TheSaturdayDeductionNeedsNightOnesPaperOrTheDebugToggle()
        {
            yield return StartHouse();
            Assert.That(PresenterMode.Jump(DreamHousePresenter.Scene, DreamHouse.ChapterHideAndSeek), Is.EqualTo(JumpResult.Jumped));
            yield return Frames(3);
            var cal = Game.House.memories.Find(m => m.id == DreamHouseStory.MemCalendar).standAt;
            Game.player.Teleport(new Vector3(cal.X, cal.Y + 0.02f, cal.Z), 0f, Vector3.zero);
            yield return WaitFor(() => Game.memories.Remaining == 4, "the calendar picked up", 5f);
            Assert.That(Game.Evidence, Does.Contain(DreamHouseStory.MemCalendar).And.Not.Contain(DreamHouseStory.DedSaturdayOff), "Night 2 alone: just the calendar");
            DreamHouseGame.DebugSaturdayPaper = true;
            Assert.That(Game.Evidence, Does.Contain(DreamHouseStory.DedSaturdayOff), "the debug toggle stands in for Night 1");
            DreamHouseGame.DebugSaturdayPaper = false;
            GameRoot.Save.Data.SetEntry(DreamJournal.Key, DreamJournal.Add(null, DreamJournal.SaturdayPaper));
            Assert.That(Game.Evidence, Does.Contain(DreamHouseStory.DedSaturdayOff), "Night 1's paper in the shared journal");
        }

        [UnityTest]
        public IEnumerator TheDreamOffersAShortcutForAMemoryAtTheHardestSpot()
        {
            yield return StartHouse();
            GameRoot.Save.Data.SetEntry(DreamHouseStory.MemoryGivenKey, null);
            Game.JumpToEscapeAt(Game.House.hardCheckpoint);
            yield return Frames(3);
            Assert.That(Game.Phase, Is.EqualTo(HousePhase.Escape));
            Game.NoteMemory(DreamHouseStory.MemPlaceCard);
            Game.FellAtTheHardSpot();
            Assert.That(Game.Offering, Is.False, "not after one fall");
            Game.FellAtTheHardSpot();
            Assert.That(Game.Offering, Is.True, "two falls at the hardest spot: the dream's offer");
            Game.AnswerOffer(true);
            yield return Frames(2);
            Assert.That(Game.Checkpoint, Is.EqualTo(Game.House.hardSkipCheckpoint), "let through");
            Assert.That(DreamHouseGame.MemoryGiven, Is.EqualTo(DreamHouseStory.MemPlaceCard), "it keeps what she gave it");
            Assert.That(Game.Evidence, Does.Not.Contain(DreamHouseStory.MemPlaceCard), "gone from what she carries");
            GameRoot.Save.Data.SetEntry(DreamHouseStory.MemoryGivenKey, null);
        }

        [UnityTest]
        public IEnumerator TheHouseFoldsShutRoomByRoomBehindHer()
        {
            yield return StartHouse();
            Assert.That(PresenterMode.Jump(DreamHousePresenter.Scene, DreamHouse.ChapterTips), Is.EqualTo(JumpResult.Jumped));
            yield return Frames(3);
            var first = GameObject.Find(DreamHouseGame.RoomGroupPrefix + Game.House.escapeRooms[0].name);
            Assert.That(first, Is.Not.Null, "the first room's group");
            DebugMenu.TimeScale = 3f;
            Game.SetAutopilot(true);
            yield return WaitFor(() => Game.Room >= 3, "three rooms in", 30f);
            yield return WaitFor(() => !first.activeSelf, "the first room folded shut behind her", 10f);
            Game.SetAutopilot(false);
            Assert.That(RenderSettings.fogColor, Is.Not.EqualTo(Color.clear));
        }

        [UnityTest]
        public IEnumerator CondemnsIsKeptAndBringsTheDespairChoiceNearer()
        {
            yield return StartHouse();
            float normal = Game.RainSpot.despairS;
            Assert.That(PresenterMode.Jump(DreamHousePresenter.Scene, DreamHouse.ChapterArgument), Is.EqualTo(JumpResult.Jumped));
            yield return Frames(3);
            Assert.That(Game.Phase, Is.EqualTo(HousePhase.Argument));
            Assert.That(Game.WaitingForChoice, Is.True, "the argument is up");
            Assert.That(Game.ArgumentRunning, Is.True, "the argument battle runs over the house");
            Assert.That(JamKit.Argument.ArgumentHost.Running, Is.True);
            // The debug skip (what F8 does): Lena condemns; the battle closes.
            Game.Choose(ArgumentOutcome.Condemns);
            Assert.That(Game.ArgumentRunning, Is.False, "the skip closed the battle");
            Assert.That(JamKit.Argument.ArgumentHost.Running, Is.False);
            Assert.That(DreamHouseGame.LenaCondemned, Is.True, "kept in the save");
            Assert.That(Game.LineText, Does.Contain(DreamHouseLines.Lena));
            Game.SkipLines();
            yield return WaitFor(() => Game.Checkpoint == Game.House.escapeCheckpoint && !Game.Respawning, "the house kicking her out", 5f);
            Assert.That(Game.Phase, Is.EqualTo(HousePhase.Escape));
            Assert.That(Game.Chasing, Is.True, "the house folds shut behind her");
            Assert.That(Game.RainSpot.despairS, Is.LessThan(normal), "the point of no return is nearer");
            Assert.That(Game.umbrella.Rain.Spot.despairS, Is.EqualTo(Game.RainSpot.despairS), "the umbrella's rain follows");
        }

        [UnityTest]
        public IEnumerator TheArgumentBattlePlayedThroughDecidesTheOutcome()
        {
            yield return StartHouse();
            Assert.That(PresenterMode.Jump(DreamHousePresenter.Scene, DreamHouse.ChapterArgument), Is.EqualTo(JumpResult.Jumped));
            yield return Frames(3);
            var battle = JamKit.Argument.ArgumentHost.Current;
            Assert.That(battle, Is.Not.Null, "the battle is up");
            Assert.That(GameRoot.Input.GameplayAllowed, Is.False, "the house waits while it runs");
            battle.Autopilot = true;
            yield return WaitFor(() => battle == null || battle.Ended, "the battle's autopilot to finish the argument", 150f);
            Assert.That(battle.Ended, Is.True);
            // The host builds its end card (and the record) on its next frame: Continue on the card, as a player would.
            yield return Frames(3);
            Assert.That(JamKit.Argument.ArgumentHost.Current, Is.SameAs(battle), "the end card waits for Continue");
            battle.Continue();
            yield return Frames(3);
            Assert.That(Game.Phase, Is.EqualTo(HousePhase.Escape), "its end card closed: the house kicks her out");
            var stance = JamKit.Argument.ArgumentHost.LoadLedger().StanceOf(JamKit.Argument.DreamLenaDemo.Id);
            TestContext.Out.WriteLine("the battle ended with Lena " + stance + "; the house: " + Game.Story.Outcome);
            Assert.That(Game.Story.Outcome, Is.EqualTo(DreamHouseGame.OutcomeOf(stance)), "the stance decides");
        }

        [UnityTest]
        public IEnumerator TheAutopilotEscapesTheTippingHouseUnderTheUmbrella()
        {
            yield return StartHouse();
            Assert.That(PresenterMode.Jump(DreamHousePresenter.Scene, DreamHouse.ChapterTips), Is.EqualTo(JumpResult.Jumped));
            yield return Frames(3);
            DebugMenu.TimeScale = 3f;
            int respawns = Game.Respawns, caught = Game.Caught, finishes = Game.Finishes;
            Game.SetAutopilot(true);
            // The escape is long now (the night's peak: about a minute for the autopilot), room by room.
            int rooms = 0, last = -1;
            float until = Time.realtimeSinceStartup + 90f;
            while (Game.Finishes == finishes && Time.realtimeSinceStartup < until)
            {
                if (Game.Room != last && Game.Room >= 0) { rooms++; last = Game.Room; }
                yield return null;
            }
            Assert.That(Game.Finishes, Is.GreaterThan(finishes), "out of the front door to the end of the rain");
            Assert.That(rooms, Is.GreaterThanOrEqualTo(Game.House.escapeRooms.Count - 1), "through the escape's rooms");
            Game.SetAutopilot(false);
            Assert.That(Game.Caught - caught, Is.EqualTo(0), "the house folding shut never caught it");
            Assert.That(Game.Respawns - respawns, Is.LessThanOrEqualTo(1), "at most one slip on the real CharacterController");
            Assert.That(Game.umbrella.HasUmbrella, Is.True, "the dream tool took the umbrella by the door");
            Assert.That(DreamEscapeGame.Despair, Is.False);
        }
    }
}
