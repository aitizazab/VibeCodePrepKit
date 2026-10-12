using System.Collections;
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
    /// <summary>Presenter mode in Visiting Hours: a chapter picked on the title starts the slice there;
    /// each place, the argument and every end card jump cleanly with a fresh journal; the opening plays
    /// on the title from inside a run; attract mode walks Nadia to a thing and charts it.</summary>
    public class VisitingHoursPresenterPlayTests
    {
        [UnitySetUp]
        public IEnumerator SetUp() => BootToTitle();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            PresenterMode.Close();
            PresenterMode.SetAttractScript(VhPresenter.Attract());
            VisitingHoursGame.FastTransitions = false;
            if (VhBoot.Opening != null) VhBoot.Opening.Finish(true);
            yield return null;
        }

        static VisitingHoursGame Game => VisitingHoursGame.Current;

        static IEnumerator FinishLines()
        {
            for (int i = 0; i < 400 && GameRoot.Dialogue.IsActive; i++)
            {
                GameRoot.Dialogue.AutoStep();
                yield return null;
            }
            yield return null;
        }

        static IEnumerator JumpFromTitle(string chapter)
        {
            VisitingHoursGame.FastTransitions = true;
            Assert.That(PresenterMode.Jump(VhPresenter.Scene, chapter), Is.EqualTo(JumpResult.Pending));
            yield return WaitFor(() => !PresenterMode.Chapters.HasPending && Game != null && SceneManager.GetActiveScene().name == VhPresenter.Scene,
                "the slice to load and jump", 20f);
            yield return WaitFor(() => GameRoot.Ui.FadeAlpha == 0f, "the scene's fade-in", 3f);
            yield return Frames(2);
        }

        [UnityTest]
        public IEnumerator EveryChapterJumpsCleanly()
        {
            yield return JumpFromTitle(VhPresenter.Street);
            Assert.That(Game.Area, Is.EqualTo(SliceArea.Street));
            foreach (var name in VhPresenter.Chapters)
            {
                if (System.Array.IndexOf(VhPresenter.AnyTimeChapters, name) >= 0) continue; // the opening, its editor, the chain: their own tests
                Assert.That(PresenterMode.Jump(VhPresenter.Scene, name), Is.EqualTo(JumpResult.Jumped), name);
                yield return Frames(3);
                Assert.That(GameRoot.Flow.InRun, Is.True, name);
                Assert.That(GameRoot.Ui.FadeAlpha, Is.EqualTo(0f), name);
                Assert.That(GameRoot.Ui.Top, Is.Null, name + ": the journal and menus closed");
                switch (name)
                {
                    case VhPresenter.Day1:
                        Assert.That(Game.Area, Is.EqualTo(SliceArea.Hospital));
                        Assert.That(Game.Journal.Count, Is.EqualTo(0), "a fresh journal");
                        break;
                    case VhPresenter.Booth: Assert.That(Game.Area, Is.EqualTo(SliceArea.Booth)); break;
                    case VhPresenter.Street: Assert.That(Game.Area, Is.EqualTo(SliceArea.Street)); break;
                    case VhPresenter.Argument: Assert.That(Game.CurrentMode, Is.EqualTo(VisitingHoursGame.Mode.Battle)); break;
                    default:
                        Assert.That(Game.CurrentMode, Is.EqualTo(VisitingHoursGame.Mode.End), name);
                        Assert.That(Game.Hud.EndShown, Is.True, name);
                        break;
                }
                yield return FinishLines();
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator TheOpeningPlaysOnTheTitleFromInsideARun()
        {
            yield return JumpFromTitle(VhPresenter.Day1);
            Assert.That(PresenterMode.Jump(VhPresenter.Scene, VhPresenter.Opening), Is.EqualTo(JumpResult.Jumped), "the opening works from any state");
            yield return WaitFor(() => GameRoot.Flow.Current == GameState.Title && !GameRoot.IsLoadingScene && VhBoot.Opening != null && VhBoot.Opening.Running,
                "the opening over the title", 15f);
            VhBoot.Opening.Finish(true);
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator AttractWalksNadiaToSomethingAndChartsIt()
        {
            yield return JumpFromTitle(VhPresenter.Day1);
            PresenterMode.SetAttractScript(new AttractScript(VhPresenter.Scene).Jump(VhPresenter.Day1, 0.5f).Drive("walk", 40f));
            Assert.That(PresenterMode.StartAttract(), Is.True);
            yield return WaitFor(() => Game.Journal != null && Game.Journal.Count > 0, "a fact charted with no hands on the keys", 45f);
            PresenterMode.StopAttract();
            Assert.That(GameRoot.Input.IsSynthetic, Is.False);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
