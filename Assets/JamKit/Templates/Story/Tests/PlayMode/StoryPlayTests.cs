using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JamKit.Flow;
using JamKit.Shell;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static JamKit.Tests.Play.ShellTestUtil;

namespace JamKit.Narrative.Tests
{
    /// <summary>The story template in its real scene: tags drive the stage, first choices play through
    /// the office to an ending and Victory, a save then a load puts the line, stage and ink variables
    /// back (and the game notices the load), a second playthrough offers and reaches the secret
    /// ending, clicking a hotspot runs its knot and items work on hotspots, the backlog shows past
    /// lines, and an edited story hot-swaps in. Each test starts with a fresh memory.</summary>
    public class StoryPlayTests
    {
        [UnitySetUp]
        public IEnumerator SetUp() => BootToTitle();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            StoryGame.PendingLoad = null;
            if (GameRoot.Exists)
            {
                GameRoot.Dialogue.AutoMode = false;
                GameRoot.Dialogue.SkipMode = false;
            }
            yield return null;
        }

        static DialogueService D => GameRoot.Dialogue;

        static IEnumerator StartStory()
        {
            StoryStorage.DeleteAll();
            StoryGame.PendingLoad = null;
            Assert.That(GameRoot.Instance.SceneService.Load("Story"), Is.True, "the Story scene is in Build Settings");
            yield return WaitFor(() => GameRoot.Flow.InRun && !GameRoot.IsLoadingScene && SceneManager.GetActiveScene().name == "Story"
                && StoryGame.Current != null && D.IsActive, "the story scene");
            yield return Frames(2);
        }

        /// <summary>Mashes on until <paramref name="until"/> (or a choice, when <paramref name="stopAtChoice"/>).</summary>
        static void StepUntil(System.Func<bool> until, int max = 600, bool stopAtChoice = false)
        {
            for (int i = 0; i < max && !until(); i++)
            {
                if (stopAtChoice && D.ChoiceCount > 0) return;
                if (D.IsActive) D.AutoStep();
            }
        }

        static int ChoiceWith(string words)
        {
            for (int i = 0; i < D.ChoiceCount; i++)
                if (D.Choices[i].Contains(words)) return i;
            return -1;
        }

        [UnityTest]
        public IEnumerator TagsDriveTheStage()
        {
            yield return StartStory();
            var game = StoryGame.Current;
            Assert.That(game.State.nvl, Is.True, "the prologue is full-screen text (#nvl)");
            Assert.That(game.Text.IsNvl, Is.True);
            Assert.That(game.State.music, Is.EqualTo("music_story_calm"));
            Assert.That(GameRoot.Audio.CurrentAmbience, Is.EqualTo("amb_rain"), "#ambience");
            Assert.That(game.Memory.playthroughs, Is.EqualTo(1));

            StepUntil(() => game.Stage.HasCharacter("iris"));
            Assert.That(game.State.nvl, Is.False, "#adv");
            Assert.That(game.State.background, Is.EqualTo("platform_night"));
            Assert.That(game.Stage.ShownBackground, Is.EqualTo("platform_night"));
            Assert.That(game.Chapter, Is.EqualTo("One - The Last Platform"), "#chapter");
            StepUntil(() => D.Line.speaker == "Iris");
            Assert.That(game.Text.ShownSpeaker, Is.EqualTo("Iris"), "\"Iris: ...\" sets the speaker");
            Assert.That(game.Text.NamePlateShown, Is.True);
            Assert.That(game.State.Find("iris").expression, Is.EqualTo("neutral"));
            D.SkipToChoice();
            Assert.That(D.ChoiceCount, Is.EqualTo(3), "three choices on a first playthrough (the fourth is for later)");
            Assert.That(game.Text.VisibleChoiceCount, Is.EqualTo(3));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator FirstChoicesPlayToAnEnding()
        {
            yield return StartStory();
            var game = StoryGame.Current;
            yield return game.PlayToEnding();
            yield return WaitFor(() => GameRoot.Flow.Current == GameState.Victory, "the victory screen");
            Assert.That(game.EndedWith, Is.EqualTo("last_train"));
            var m = StoryStorage.Memory;
            Assert.That(m.SeenEnding("last_train"), Is.True, "the ending is remembered");
            Assert.That(m.finished, Is.EqualTo(1));
            Assert.That(m.inProgress, Is.False);
            Assert.That(StoryStorage.Exists(StorySlots.Auto), Is.False, "a finished story leaves no autosave");
            Assert.That(GameRoot.Run.Extra.Any(e => e.label == "Ending" && e.value == "The Last Train"), Is.True, "the victory screen names the ending");
            Assert.That(m.VisitedEver("office"), Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator SaveThenLoadRestoresTheLineStageAndVariables()
        {
            yield return StartStory();
            var game = StoryGame.Current;
            D.SkipToChoice();
            int kind = ChoiceWith("Are you all right");
            Assert.That(kind, Is.GreaterThanOrEqualTo(0));
            D.Choose(kind);
            for (int i = 0; i < 3; i++) D.AutoStep();
            string line = D.Line.text;
            string bg = game.State.background;
            object trust = D.Story.variablesState["trust"];
            Assert.That(trust, Is.EqualTo(2));
            Assert.That(game.SaveTo(StorySlots.Manual(0)), Is.True);
            int historyAtSave = game.History.Count;

            // Play on: another choice, a new room.
            D.SkipToChoice();
            D.Choose(0);
            StepUntil(() => game.State.background == "waiting_room");
            Assert.That(game.State.background, Is.EqualTo("waiting_room"));
            Assert.That(D.Story.variablesState["trust"], Is.Not.EqualTo(trust));

            Assert.That(game.LoadNow(StorySlots.Manual(0)), Is.True);
            yield return null;
            Assert.That(D.IsActive, Is.True);
            Assert.That(D.Line.text, Is.EqualTo(line), "the line on screen is back");
            Assert.That(game.State.background, Is.EqualTo(bg));
            Assert.That(game.Stage.ShownBackground, Is.EqualTo(bg));
            Assert.That(game.Stage.HasCharacter("iris"), Is.True);
            Assert.That(D.Story.variablesState["trust"], Is.EqualTo(trust), "ink variables are back");
            Assert.That(game.History.Count, Is.EqualTo(historyAtSave), "the backlog is back");
            Assert.That(game.Memory.loadsThisRun, Is.EqualTo(1), "the game knows it was reloaded");
            Assert.That(game.Memory.rewindsThisRun, Is.EqualTo(1), "and that it went back in time");
            Assert.That(StoryStorage.Memory.Recall("first_words"), Is.EqualTo("a kindness"), "memory isn't undone by a load");

            // The story carries on from there, and later notices the reload (was_reloaded()).
            D.SkipToChoice();
            Assert.That(D.ChoiceCount, Is.GreaterThan(0));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator SecondPlaythroughUnlocksTheSecretEnding()
        {
            yield return StartStory();
            var first = StoryGame.Current;
            D.SkipToChoice();
            Assert.That(ChoiceWith("done this before"), Is.EqualTo(-1), "no secret choice the first time");
            yield return first.PlayToEnding();
            yield return WaitFor(() => GameRoot.Flow.Current == GameState.Victory, "the first ending");

            // Play again (Try again from the end screens).
            GameRoot.Instance.Retry();
            yield return WaitFor(() => StoryGame.Current != null && StoryGame.Current != first && !GameRoot.IsLoadingScene && D.IsActive, "the second playthrough");
            var second = StoryGame.Current;
            Assert.That(second.Memory.playthroughs, Is.EqualTo(2));
            D.SkipToChoice();
            int secret = ChoiceWith("done this before");
            Assert.That(secret, Is.GreaterThanOrEqualTo(0), "the secret choice appears on the second playthrough");
            D.Choose(secret);
            D.SkipToChoice();
            int change = ChoiceWith("change the ending");
            Assert.That(change, Is.GreaterThanOrEqualTo(0));
            D.Choose(change);
            yield return second.PlayToEnding();
            yield return WaitFor(() => GameRoot.Flow.Current == GameState.Victory, "the secret ending");
            Assert.That(second.EndedWith, Is.EqualTo("return_ticket"));
            var m = StoryStorage.Memory;
            Assert.That(m.titleOverride, Is.EqualTo("Next Train Home"), "#meta:title changed the title screen");
            Assert.That(m.DistinctEndings, Is.EqualTo(2));
            Assert.That(GameRoot.Save.Data.HasFlag("iris_left_the_station"), Is.True, "set_flag");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator HotspotClickRunsItsKnotAndItemsWork()
        {
            yield return StartStory();
            var game = StoryGame.Current;
            StepUntil(() => D.IsParked, 800);
            Assert.That(D.IsParked, Is.True, "the story waits in the office");
            Assert.That(game.Investigation.id, Is.EqualTo("office"));
            Assert.That(game.Hotspots.Active, Is.True);
            Assert.That(GameRoot.Flow.Current, Is.EqualTo(GameState.Playing));
            Assert.That(game.State.background, Is.EqualTo("office"));

            // A click on the coat: the picker finds the coat polygon at its middle, and that runs its knot.
            var office = game.Investigation;
            int coat = office.IndexOf("coat");
            var shapes = office.hotspots.Select(HotspotScene.ShapeOf).ToList();
            HotspotGeometry.Centroid(shapes[coat], out float cx, out float cy);
            Assert.That(HotspotPicker.Pick(shapes, null, cx, cy), Is.EqualTo(coat));
            game.Hotspots.Activated(coat);
            Assert.That(D.IsActive, Is.True, "the coat's knot runs");
            Assert.That(D.Line.text, Does.Contain("coat"));
            StepUntil(() => D.IsParked);
            Assert.That(game.HasItem("brass_key"), Is.True, "give(brass_key)");
            Assert.That(game.Inventory.Items, Does.Contain("brass_key"));
            Assert.That(game.Inventory.Visible, Is.True);

            // The locked drawer, then the key on it.
            Assert.That(game.UseHotspot("desk_drawer"), Is.True);
            Assert.That(D.Line.text, Does.Contain("locked"));
            StepUntil(() => D.IsParked);
            Assert.That(game.UseHotspot("desk_drawer", "brass_key"), Is.True);
            Assert.That(D.Line.text, Does.Contain("key turns"));
            StepUntil(() => D.IsParked);
            Assert.That(game.HasItem("ticket"), Is.True);

            // An item where it does nothing: the fallback knot.
            Assert.That(game.UseHotspot("window", "brass_key"), Is.True);
            Assert.That(D.Line.text, Is.Not.Empty);
            StepUntil(() => D.IsParked);

            // The door with the ticket: #leave, and the story goes on.
            Assert.That(game.UseHotspot("door"), Is.True);
            StepUntil(() => game.Investigation == null);
            Assert.That(game.Investigation, Is.Null);
            Assert.That(game.Hotspots.Active, Is.False);
            StepUntil(() => game.State.background == "waiting_room");
            Assert.That(D.IsActive, Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator BacklogShowsPastLines()
        {
            yield return StartStory();
            var game = StoryGame.Current;
            for (int i = 0; i < 8; i++) D.AutoStep();
            Assert.That(game.History.Count, Is.GreaterThanOrEqualTo(6));
            game.OpenBacklog();
            yield return null;
            Assert.That(GameRoot.Ui.IsOverlayOpen(StoryScreens.Backlog), Is.True);
            var screen = StoryScreens.Backlog;
            Assert.That(screen.EntryCount, Is.EqualTo(game.History.Count));
            bool found = false;
            for (int i = 0; i < screen.EntryCount; i++) if (screen.EntryText(i).Contains("Hollin Cross")) found = true;
            Assert.That(found, Is.True, "the first page of the prologue is in the history");
            GameRoot.Ui.Back();
            yield return null;
            Assert.That(GameRoot.Ui.IsOverlayOpen(StoryScreens.Backlog), Is.False);
            LogAssert.NoUnexpectedReceived();
        }

#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator EditedInkHotSwapsIntoTheRunningStory()
        {
            yield return StartStory();
            var game = StoryGame.Current;
            StepUntil(() => game.Knot == "platform");
            var errors = new List<string>();
            string json = StoryInkReloader.Compile(UnityEditor.AssetDatabase.GetAssetPath(game.config.story), errors);
            Assert.That(errors, Is.Empty);
            Assert.That(json, Is.Not.Null);
            Assert.That(D.HotSwap(json, "platform"), Is.True);
            Assert.That(D.IsActive, Is.True);
            Assert.That(D.CurrentKnot, Is.EqualTo("platform"), "the knot restarts with the new text");
            LogAssert.NoUnexpectedReceived();
        }
#endif
    }
}
