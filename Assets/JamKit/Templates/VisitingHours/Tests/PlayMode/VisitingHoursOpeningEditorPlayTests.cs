using System.Collections;
using JamKit.Flow;
using JamKit.Presenting;
using JamKit.Shell;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static JamKit.Tests.Play.ShellTestUtil;

namespace JamKit.VisitingHours.Tests
{
    /// <summary>The opening editor in play (docs/OPENING-EDITOR.md): it opens on the title, scrubs to the beats, moves the
    /// phone (her grab follows it, back to her palm, no turn-over), undoes, and closes back to the opening; the presenter
    /// panel's "Edit the opening" opens it from inside a run. Nothing is saved (SaveOnClose off).</summary>
    public class VisitingHoursOpeningEditorPlayTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return BootToTitle();
            VhOpeningEditor.SaveOnClose = false;
            var c = Object.Instantiate(GameRoot.Config);
            c.firstGameplayScene = VhPresenter.Scene;
            c.chooseDifficultyOnPlay = false;
            c.showHowToPlayOnFirstRun = false;
            GameRoot.UseConfig(c);
            VhBoot.Refresh();
            yield return WaitFor(() => VhBoot.Stage != null && VhBoot.Stage.Interactive, "the title table", 10f);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (VhOpeningEditor.Instance != null && VhOpeningEditor.Instance.IsOpen) VhOpeningEditor.Instance.Close();
            VhOpeningEditor.SaveOnClose = true;
            if (VhBoot.Opening != null) VhBoot.Opening.Finish(true);
            PresenterMode.Close();
            yield return WaitFor(() => !GameRoot.IsLoadingScene, "any load to finish");
            UseSandboxConfig();
            VhBoot.Refresh();
            yield return null;
        }

        /// <summary>Her palm faces the phone's back (its normal points to the screen's side) and her knuckles are behind it.</summary>
        static void AssertBackToPalm(VhStage stage, string when)
        {
            var model = stage.nadia as VhModelNadia;
            if (model == null) return; // the stand-in arm has no fingers to check
            Vector3 up = stage.phone.rotation * Vector3.up;
            Vector3 centre = stage.phone.position + up * (stage.phoneSize.y * 0.5f);
            var middle = VhModelNadia.Bone(model.model, "RightHandMiddle1");
            var index = VhModelNadia.Bone(model.model, "RightHandIndex1");
            var pinky = VhModelNadia.Bone(model.model, "RightHandPinky1");
            Assert.That(middle != null && index != null && pinky != null, Is.True, "her finger bones");
            Vector3 palm = Vector3.Cross(middle.position - model.rHand.position, index.position - pinky.position).normalized;
            Assert.That(Vector3.Dot(palm, up), Is.GreaterThan(0.3f), when + ": her palm faces the phone's back (no turn-over)");
            Assert.That(Vector3.Dot(middle.position - centre, up), Is.LessThan(0f), when + ": her knuckles are behind the phone");
            Assert.That(Vector3.Distance(model.rHand.position, stage.phone.position), Is.LessThan(0.3f), when + ": her hand is at the phone");
        }

        [UnityTest]
        public IEnumerator MovingThePhoneTheGrabStillTakesItBackToPalm()
        {
            var ed = VhOpeningEditor.Instance;
            Assert.That(ed, Is.Not.Null, "the editor lives on the GameRoot");
            Assert.That(ed.Open(), Is.True, "it opens on the title");
            yield return null;
            var stage = ed.Stage;
            Assert.That(ed.Opening, Is.Not.Null);
            Assert.That(ed.Opening.Paused, Is.True, "the opening holds still while editing");
            Assert.That(ed.BeatNames, Does.Contain("The grab"));

            Assert.That(ed.SeekBeat("The phone on the nightstand"), Is.True);
            Assert.That(ed.Select("phone"), Is.True);
            Vector3 rest = stage.phoneRestPos;
            ed.MoveSelected(Matrix4x4.Translate(new Vector3(0f, 0f, 0.04f)));
            Assert.That(Vector3.Distance(stage.phoneRestPos, rest + new Vector3(0f, 0f, 0.04f)), Is.LessThan(1e-3f), "the phone rests where it was put");
            Assert.That(Vector3.Distance(stage.phone.position, stage.phoneRestPos), Is.LessThan(1e-3f), "and lies there");

            Assert.That(ed.SeekBeat("The grab"), Is.True);
            yield return null;
            yield return null;
            AssertBackToPalm(stage, "the grab");
            Assert.That(ed.SeekBeat("Holding it: the voicemail"), Is.True);
            yield return null;
            yield return null;
            AssertBackToPalm(stage, "holding it up");

            Assert.That(ed.Undo(), Is.True);
            Assert.That(Vector3.Distance(stage.phoneRestPos, rest), Is.LessThan(1e-3f), "undo puts it back");
            ed.Close();
            Assert.That(ed.IsOpen, Is.False);
            yield return null;
            Assert.That(stage.editorCamera, Is.False, "the shots have the camera again");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator TheLaunchFlagOpensItOnTheTitle()
        {
            var ed = VhOpeningEditor.Instance;
            Assert.That(ed, Is.Not.Null);
            Assert.That(ed.ApplyLaunchArgs(new[] { "VisitingHours.exe" }), Is.False, "no flag, nothing");
            Assert.That(ed.IsOpen, Is.False);
            Assert.That(ed.ApplyLaunchArgs(new[] { "VisitingHours.exe", VhOpeningEditor.LaunchFlag }), Is.True);
            yield return WaitFor(() => ed.IsOpen, "the editor to open on the title", 5f);
            Assert.That(GameRoot.Flow.Current, Is.EqualTo(GameState.Title));
            Assert.That(ed.Opening != null && ed.Opening.Paused, Is.True, "the opening held for editing");
            ed.Close();
            yield return null;
        }

        [UnityTest]
        public IEnumerator ThePresenterPanelOpensItFromARun()
        {
            VisitingHoursGame.FastTransitions = true;
            Assert.That(PresenterMode.Jump(VhPresenter.Scene, VhPresenter.Day1), Is.EqualTo(JumpResult.Pending));
            yield return WaitFor(() => !PresenterMode.Chapters.HasPending && VisitingHoursGame.Current != null, "the slice", 20f);
            Assert.That(PresenterMode.Jump(VhPresenter.Scene, VhPresenter.EditOpening), Is.EqualTo(JumpResult.Jumped));
            yield return WaitFor(() => VhOpeningEditor.Instance != null && VhOpeningEditor.Instance.IsOpen, "the editor on the title", 20f);
            Assert.That(GameRoot.Flow.Current, Is.EqualTo(GameState.Title));
            VhOpeningEditor.Instance.Close();
            VisitingHoursGame.FastTransitions = false;
            yield return null;
        }
    }
}
