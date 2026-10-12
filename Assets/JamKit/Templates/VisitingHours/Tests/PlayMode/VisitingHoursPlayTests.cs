using System.Collections;
using JamKit.Shell;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static JamKit.Tests.Play.ShellTestUtil;

namespace JamKit.VisitingHours.Tests
{
    /// <summary>The slice in its real scene: charting writes a fact and the journal combines (and
    /// refuses gently), Day 1 leads through the booth (the journal opens itself) into the dream, the
    /// rushing line comes with open questions and never blocks, each of the argument's four
    /// outcomes (an autopilot plays it fast) and replaying Walt. The opening and the title: VisitingHoursOpeningPlayTests.</summary>
    public class VisitingHoursPlayTests
    {
        [UnitySetUp]
        public IEnumerator SetUp() => BootToTitle();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            VisitingHoursGame.FastTransitions = false;
            DebugMenu.GodMode = false;
            yield return null;
        }

        static VisitingHoursGame Game => VisitingHoursGame.Current;

        static IEnumerator StartSlice()
        {
            VisitingHoursGame.FastTransitions = true;
            GameRoot.Save.Data.seenHowToPlay = true;
            Assert.That(GameRoot.Instance.SceneService.Load("VisitingHours"), Is.True, "VisitingHours is in Build Settings");
            yield return WaitFor(() => GameRoot.Flow.InRun && !GameRoot.IsLoadingScene && SceneManager.GetActiveScene().name == "VisitingHours"
                && Game != null && Game.CurrentMode == VisitingHoursGame.Mode.Explore, "the slice's first place");
            yield return WaitFor(() => GameRoot.Dialogue.IsActive, "the Day 1 intro");
            yield return FinishDialogue();
            Assert.That(Game.Area, Is.EqualTo(SliceArea.Hospital));
        }

        static IEnumerator Look(string id)
        {
            Game.StandAt(id);
            yield return Frames(2);
            Assert.That(Game.Target, Is.Not.Null, "something in reach in front of " + id);
            Assert.That(Game.Target.def.id, Is.EqualTo(id), "the prompt picks what's in front of Nadia");
            Game.Examine(id);
            yield return WaitFor(() => GameRoot.Dialogue.IsActive, id + "'s lines");
            yield return FinishDialogue();
        }

        [UnityTest]
        public IEnumerator ChartingWritesAFactAndTheJournalCombines()
        {
            yield return StartSlice();
            var game = Game;
            var journal = game.Journal;
            yield return Look("chart");
            Assert.That(journal.Knows(SliceContent.FactChart), Is.True, "Nadia charts");
            Assert.That(game.Hud.ToastText, Does.StartWith("Charted:"));
            yield return Look("police_report");
            Assert.That(journal.Knows(SliceContent.FactPoliceReport), Is.True);

            game.DebugJumpTo(SliceArea.Street, SliceContent.FactEffects, SliceContent.MemWaltAlive);
            yield return Frames(2);
            var screen = game.JournalScreen;
            screen.Open(VhJournalScreen.Tab.Facts, false);
            yield return null;
            Assert.That(screen.IsOpen, Is.True);
            Assert.That(screen.OpenQuestionCount, Is.EqualTo(1), "the open question is listed");
            Assert.That(screen.Activate("journal-entry-fact_effects"), Is.True);
            Assert.That(screen.Activate("journal-tab-memories"), Is.True);
            Assert.That(screen.Activate("journal-entry-mem_walt_alive"), Is.True);
            Assert.That(screen.Picks.Count, Is.EqualTo(2));
            Assert.That(screen.Activate("journal-combine"), Is.True);
            Assert.That(journal.Knows(SliceContent.DedWaltDead), Is.True, "the key deduction");
            Assert.That(screen.CurrentTab, Is.EqualTo(VhJournalScreen.Tab.Deductions));
            Assert.That(screen.Shown, Is.EqualTo(SliceContent.DedWaltDead));
            Assert.That(screen.OpenQuestionCount, Is.EqualTo(0));

            // A pair that doesn't fit: a gentle line, nothing made.
            int before = journal.Count;
            screen.Pick(SliceContent.FactChart);
            screen.Pick(SliceContent.FactPoliceReport);
            Assert.That(screen.CombinePicked(), Is.EqualTo(CombineResult.NoMatch));
            Assert.That(game.config.gentleLines, Does.Contain(screen.LastResult));
            Assert.That(journal.Count, Is.EqualTo(before));

            // The scribbled-out line under the police report.
            screen.Read(SliceContent.FactPoliceReport);
            Assert.That(screen.ShownScribble, Is.EqualTo("I should have"));
            screen.Close();
            yield return null;
            Assert.That(screen.IsOpen, Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator DayOneLeadsThroughTheBoothIntoTheDream()
        {
            yield return StartSlice();
            var game = Game;
            game.StandAt("stairwell");
            yield return Frames(2);
            game.Examine("stairwell");
            yield return WaitFor(() => GameRoot.Dialogue.IsActive, "the stairwell");
            yield return FinishDialogue();
            Assert.That(game.Area, Is.EqualTo(SliceArea.Hospital), "not before the round (docs/redesign/day.md)");

            // The handover starts the round; the round's three tasks (the first choice of each is the right one).
            foreach (var id in new[] { "rafael", "wristband", "vitals", "okafor", "chart" })
                yield return Look(id);
            Assert.That(game.Round.Complete, Is.True, "the round is done");
            Assert.That(game.Round.Slips, Is.EqualTo(0));
            game.StandAt("stairwell");
            yield return Frames(2);
            game.Examine("stairwell");
            yield return WaitFor(() => GameRoot.Dialogue.IsActive, "the stairwell");
            yield return FinishDialogue();
            yield return WaitFor(() => game.Area == SliceArea.Booth && GameRoot.Dialogue.IsActive, "Mags in the booth");
            yield return FinishDialogue();
            yield return WaitFor(() => game.JournalScreen.IsOpen, "the journal to open the first time Mags explains it");
            game.JournalScreen.Close();
            yield return WaitFor(() => GameRoot.Dialogue.IsActive, "Mags again");
            yield return FinishDialogue();
            yield return WaitFor(() => game.Area == SliceArea.Street && game.CurrentMode == VisitingHoursGame.Mode.Explore, "the dream");
            Assert.That(game.BuiltArea.walt, Is.Not.Null, "Walt is on the street");
            Assert.That(GameRoot.Config.FindPortrait("Mags"), Is.Not.Null, "the slice registered its portraits");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator TheRushingLineComesWithOpenQuestionsAndNeverBlocks()
        {
            yield return StartSlice();
            var game = Game;
            game.DebugJumpTo(SliceArea.Street, SliceContent.FactEffects, SliceContent.MemWaltAlive);
            yield return Frames(2);
            Assert.That(game.Journal.HasOpenQuestions, Is.True);
            game.Examine("walt");
            yield return WaitFor(() => GameRoot.Dialogue.IsActive, "the rushing line");
            Assert.That(game.RushingShown, Is.EqualTo(1));
            Assert.That(GameRoot.Dialogue.CurrentLine, Does.Contain("I don't know enough yet."));
            Assert.That(GameRoot.Dialogue.SkipToChoice(), Is.True, "a choice, not a wall");
            Assert.That(GameRoot.Dialogue.ChoiceCount, Is.EqualTo(2));
            GameRoot.Dialogue.Choose(0);
            yield return FinishDialogue();
            yield return WaitFor(() => game.CurrentMode == VisitingHoursGame.Mode.Battle, "the argument, anyway");
            Assert.That(game.BattleView.IsShown, Is.True);

            // With nothing left to work out, there's no rushing line.
            game.DebugJumpTo(SliceArea.Street, SliceContent.KnownBeforeWalt());
            yield return Frames(2);
            game.Examine("walt");
            yield return WaitFor(() => GameRoot.Dialogue.IsActive, "Walt");
            Assert.That(game.RushingShown, Is.EqualTo(1));
            Assert.That(GameRoot.Dialogue.CurrentKnot, Is.EqualTo(SliceContent.KnotWaltIntro));
            LogAssert.NoUnexpectedReceived();
        }

        static IEnumerator Argue(PilotAction[] plan, bool pilotInvulnerable, params string[] known)
        {
            yield return StartSlice();
            var game = Game;
            game.DebugJumpTo(SliceArea.Street, known);
            game.Journal.Record(SliceContent.MemWaltAlive);
            yield return Frames(2);
            game.BattleSpeed = 10f;
            game.Pilot = plan;
            game.PilotInvulnerable = pilotInvulnerable;
            game.StartBattle();
            Assert.That(game.CurrentMode, Is.EqualTo(VisitingHoursGame.Mode.Battle));
            yield return WaitFor(() => game.Battle.Phase == BattlePhase.Done, "the argument to end", 40f);
            // The outcome's lines, the street dimming, Mags pulling her out: mash through them (by time, not frames).
            float end = Time.realtimeSinceStartup + 20f;
            while (game.CurrentMode != VisitingHoursGame.Mode.End && Time.realtimeSinceStartup < end)
            {
                if (GameRoot.Dialogue.IsActive) GameRoot.Dialogue.AutoStep();
                yield return null;
            }
            Assert.That(game.CurrentMode, Is.EqualTo(VisitingHoursGame.Mode.End), "the end card");
            Assert.That(game.Hud.EndShown, Is.True);
        }

        [UnityTest]
        public IEnumerator SayingTheKeyDeductionConvincesWalt()
        {
            yield return Argue(new[] { PilotAction.Listen, PilotAction.Listen, PilotAction.Touch, PilotAction.Touch }, true, SliceContent.KnownBeforeWalt());
            var game = Game;
            Assert.That(game.Battle.Outcome, Is.EqualTo(BattleOutcome.Convinced));
            Assert.That(game.State.waltOutcome, Is.EqualTo(BattleOutcome.Convinced));
            Assert.That(game.Journal.Knows(SliceContent.TruthWalt), Is.True, "a truth entry");
            Assert.That(game.Hud.EndBody, Does.Contain("Convinced"));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ListeningThroughHearsWaltOut()
        {
            yield return Argue(new[] { PilotAction.Listen, PilotAction.Listen, PilotAction.Listen, PilotAction.Listen }, true);
            var game = Game;
            Assert.That(game.Battle.Outcome, Is.EqualTo(BattleOutcome.HeardOut));
            Assert.That(game.Journal.Knows(SliceContent.PersWrench), Is.True, "Walt's wrench");
            Assert.That(game.State.force, Is.EqualTo(0));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator PressingPassesButHurts()
        {
            yield return Argue(new[] { PilotAction.Press, PilotAction.Press, PilotAction.Listen, PilotAction.Listen }, true);
            var game = Game;
            Assert.That(game.Battle.Outcome, Is.EqualTo(BattleOutcome.Pressed));
            Assert.That(game.State.force, Is.EqualTo(3), "two presses and the Pressed outcome");
            Assert.That(game.Journal.Knows(SliceContent.PersWrench) || game.Journal.Knows(SliceContent.TruthWalt), Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator StandingStillLosesComposureAndWaltCanBeArguedAgain()
        {
            // No pilot and no input: the light sits still and the walls find it.
            yield return Argue(null, false);
            var game = Game;
            Assert.That(game.Battle.Outcome, Is.EqualTo(BattleOutcome.ComposureLost));
            Assert.That(game.Battle.Composure, Is.EqualTo(0));
            Assert.That(game.Hud.EndBody, Does.Contain("The night ends"));
            Assert.That(game.Hud.PressEnd("retry"), Is.True);
            yield return Frames(2);
            Assert.That(game.CurrentMode, Is.EqualTo(VisitingHoursGame.Mode.Explore));
            Assert.That(game.Area, Is.EqualTo(SliceArea.Street));
            LogAssert.NoUnexpectedReceived();
        }
    }
}
