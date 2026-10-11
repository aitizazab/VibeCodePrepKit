using System.Collections;
using System.Collections.Generic;
using JamKit.Flow;
using JamKit.Presenting;
using JamKit.Shell;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static JamKit.Tests.Play.ShellTestUtil;

namespace JamKit.VisitingHours.Tests
{
    /// <summary>The opening and the title table in play (docs/OPENING-AND-TITLE.md): the opening plays
    /// to an interactive title (fast) and holding Escape skips it; each thing on the table chooses its
    /// action (the tablet Play, the mug Quit, the badge Settings, the notepad and pen Credits), by code,
    /// keys/pad and through the shell; presenter mode's "Opening" plays it from the title.</summary>
    public class VisitingHoursOpeningPlayTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return BootToTitle();
            UseVisitingHours();
            VhBoot.Refresh();
            yield return WaitFor(() => VhBoot.Stage != null && VhBoot.Stage.Interactive, "the title table", 10f);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            VhTitleTable.Intercept = null;
            if (GameRoot.Exists) GameRoot.Input.Synthetic = null;
            if (VhBoot.Opening != null) VhBoot.Opening.Finish(true);
            VisitingHoursGame.FastTransitions = false;
            PresenterMode.Close();
            yield return WaitFor(() => !GameRoot.IsLoadingScene, "any load to finish");
            if (GameRoot.Flow.Current == GameState.Credits) GameRoot.Instance.CloseCredits();
            GameRoot.Ui.CloseOverlays();
            UseSandboxConfig();
            VhBoot.Refresh();
            yield return null;
        }

        /// <summary>An in-memory copy of the config with Visiting Hours as the game (the asset on disk never changes).</summary>
        static void UseVisitingHours()
        {
            var c = Object.Instantiate(GameRoot.Config);
            c.firstGameplayScene = VhPresenter.Scene;
            c.titleMusic = "music_vh_sad_pad";
            c.chooseDifficultyOnPlay = false;
            c.showHowToPlayOnFirstRun = false;
            GameRoot.UseConfig(c);
        }

        static VhStage Stage => VhBoot.Stage;

        [UnityTest]
        public IEnumerator TheOpeningPlaysToAnInteractiveTitle()
        {
            var opening = VhBoot.PlayOpeningNow(40f);
            Assert.That(opening, Is.Not.Null, "the boot asset is in Resources and the title's sets build");
            yield return null;
            var stage = Stage;
            Assert.That(GameRoot.Ui.Title.Root.enabledSelf, Is.False, "no input until the title");
            Assert.That(stage.Interactive, Is.False);
            Assert.That(stage.InBedroom, Is.True, "it opens in her bedroom");
            Assert.That(stage.table.Find("tablet").Fade, Is.EqualTo(0f), "the table comes later");

            yield return WaitFor(() => stage.Pose == "overhead", "the cut to the phone from above", 10f);
            Assert.That(opening.SpeakerOn, Is.False, "from above his voice is clear, not through the phone's speaker");
            Assert.That(stage.PhoneHeld, Is.False, "she dropped the phone");
            Assert.That(stage.Breathing, Is.True, "asleep, breathing");
            Assert.That(stage.nadia, Is.Not.Null);

            yield return WaitFor(() => !opening.Running, "the opening to end", 15f);
            foreach (var kind in new[] { OpeningCueKind.Ring, OpeningCueKind.RingStop, OpeningCueKind.Arm, OpeningCueKind.Voice, OpeningCueKind.Speaker,
                         OpeningCueKind.VoiceLevel, OpeningCueKind.Fade, OpeningCueKind.Lights, OpeningCueKind.Music, OpeningCueKind.Show, OpeningCueKind.Interactive, OpeningCueKind.End })
                Assert.That(opening.Seen, Does.Contain(kind), kind.ToString());
            Assert.That(opening.Skipped, Is.False);
            Assert.That(GameRoot.Ui.Title.Root.enabledSelf, Is.True, "the title takes input again");
            Assert.That(stage.Interactive, Is.True);
            Assert.That(stage.Pose, Is.EqualTo("table"));
            foreach (var p in stage.table.pieces) Assert.That(p.Visible, Is.True, p.id + " is on the table");
            if (stage.tabletUi != null)
            {
                Assert.That(stage.tabletUi.Paused, Is.True, "the report is over: the play button is up");
                Assert.That(stage.tabletUi.Headline, Does.StartWith("ROUTE 9"));
            }
            Assert.That(GameRoot.Flow.Current, Is.EqualTo(GameState.Title));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator HoldingEscapeSkipsToTheTitle()
        {
            var opening = VhBoot.PlayOpeningNow(1f);
            Assert.That(opening, Is.Not.Null);
            yield return Frames(3);
            Assert.That(opening.Running, Is.True);
            var hands = new SyntheticInput();
            GameRoot.Input.Synthetic = hands;
            hands.Hold(GameRoot.Input.Cancel, true);
            yield return WaitFor(() => !opening.Running, "the skip (hold Escape)", 6f);
            hands.Hold(GameRoot.Input.Cancel, false);
            GameRoot.Input.Synthetic = null;
            Assert.That(opening.Skipped, Is.True);
            Assert.That(Stage.Interactive, Is.True);
            Assert.That(Stage.Pose, Is.EqualTo("table"));
            yield return null;
            foreach (var p in Stage.table.pieces) Assert.That(p.Visible, Is.True, p.id);
            Assert.That(GameRoot.Flow.Current, Is.EqualTo(GameState.Title), "skipping doesn't leave the title");
            Assert.That(GameRoot.Ui.Title.Root.enabledSelf, Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator EachThingOnTheTableChoosesItsAction()
        {
            var table = Stage.table;
            Assert.That(table.Live, Is.True, "the table takes input on the title");
            Assert.That(GameRoot.Ui.Title.IsVisible, Is.True);
            var chosen = new List<VhTitleAction>();
            VhTitleTable.Intercept = a => { chosen.Add(a); return true; };
            Assert.That(table.Choose("tablet"), Is.True);
            Assert.That(table.Choose("badge"), Is.True);
            Assert.That(table.Choose("notepad"), Is.True);
            Assert.That(table.Choose("pen"), Is.True);
            Assert.That(table.Choose("table"), Is.False, "the table itself isn't a choice");
            var expected = new List<VhTitleAction> { VhTitleAction.Play, VhTitleAction.Settings, VhTitleAction.Credits, VhTitleAction.Credits };
            Assert.That(table.Choose("mug"), Is.EqualTo(VhTitleTable.QuitAvailable), "the mug quits (not in a browser)");
            if (VhTitleTable.QuitAvailable) expected.Add(VhTitleAction.Quit);
            Assert.That(chosen, Is.EqualTo(expected));
            Assert.That(table.CurrentGroup, Is.EqualTo(VhTitleTable.QuitAvailable ? "mug" : "notepad"), "the chosen thing is highlighted");
            VhTitleTable.Intercept = null;

            // For real, through the shell: the badge opens Settings over the table, which pauses.
            Assert.That(table.Choose("badge"), Is.True);
            yield return null;
            Assert.That(GameRoot.Ui.IsOverlayOpen(GameRoot.Ui.Settings), Is.True, "the badge is Settings");
            Assert.That(table.Live, Is.False, "the table waits under Settings");
            GameRoot.Ui.CloseOverlay();
            yield return null;
            Assert.That(table.Live, Is.True);

            // The notepad is the credits.
            Assert.That(table.Choose("notepad"), Is.True);
            yield return WaitFor(() => GameRoot.Flow.Current == GameState.Credits, "the credits", 5f);
            GameRoot.Instance.CloseCredits();
            yield return WaitFor(() => GameRoot.Flow.Current == GameState.Title && !GameRoot.IsLoadingScene && Stage != null && Stage.Interactive, "back to the table", 10f);

            // The tablet's play button is Play.
            VisitingHoursGame.FastTransitions = true;
            GameRoot.Save.Data.seenHowToPlay = true;
            Assert.That(Stage.table.Choose("tablet"), Is.True);
            yield return WaitFor(() => GameRoot.Flow.InRun && !GameRoot.IsLoadingScene && SceneManager.GetActiveScene().name == VhPresenter.Scene, "Play starts the slice", 20f);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator KeysAndThePadMoveAroundTheTable()
        {
            var table = Stage.table;
            var hands = new SyntheticInput();
            GameRoot.Input.Synthetic = hands;
            yield return null;
            table.Highlight(null);
            hands.Tap(GameRoot.Input.Submit);
            yield return Frames(3);
            Assert.That(table.CurrentGroup, Is.EqualTo("tablet"), "the first press only highlights (the tablet)");
            var seen = new HashSet<string> { table.CurrentGroup };
            foreach (var dir in new[] { Vector2.right, Vector2.down, Vector2.left, Vector2.left, Vector2.up })
            {
                hands.move = dir;
                hands.Tap(GameRoot.Input.Navigate);
                yield return Frames(3);
                Assert.That(table.Current, Is.Not.Null);
                Assert.That(table.Selectable(table.Current), Is.True, table.CurrentGroup);
                seen.Add(table.CurrentGroup);
            }
            hands.move = Vector2.zero;
            Assert.That(seen.Count, Is.GreaterThanOrEqualTo(3), "the arrows reach the other things: " + string.Join(", ", seen));
            Assert.That(seen, Does.Not.Contain("pen"), "the pen is part of the notepad");
            var chosen = new List<VhTitleAction>();
            VhTitleTable.Intercept = a => { chosen.Add(a); return true; };
            var expect = table.Current.action;
            hands.Tap(GameRoot.Input.Submit);
            yield return Frames(3);
            Assert.That(chosen, Is.EqualTo(new List<VhTitleAction> { expect }), "Enter or (A) chooses the highlighted thing");
            GameRoot.Input.Synthetic = null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator PresenterOpeningPlaysFromTheTitle()
        {
            Assert.That(PresenterMode.Jump(VhPresenter.Scene, VhPresenter.Opening), Is.EqualTo(JumpResult.Jumped));
            yield return WaitFor(() => VhBoot.Opening != null && VhBoot.Opening.Running, "the opening", 5f);
            Assert.That(Stage.Interactive, Is.False, "the table waits for the opening");
            VhBoot.Opening.Finish(true);
            yield return null;
            Assert.That(Stage.Interactive, Is.True);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
