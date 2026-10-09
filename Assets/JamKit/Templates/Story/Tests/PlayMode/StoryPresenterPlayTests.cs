using System.Collections;
using JamKit.Presenting;
using JamKit.Shell;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static JamKit.Tests.Play.ShellTestUtil;

namespace JamKit.Narrative.Tests
{
    /// <summary>Presenter mode in the story template: a chapter picked on the title starts the story
    /// there; every chapter starts its knot on a clean stage (the office opens its hotspots, an ending
    /// reaches its card); attract mode reads by itself at the auto pace.</summary>
    public class StoryPresenterPlayTests
    {
        [UnitySetUp]
        public IEnumerator SetUp() => BootToTitle();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            PresenterMode.Close();
            PresenterMode.SetAttractScript(StoryPresenter.Attract());
            StoryGame.PendingLoad = null;
            if (GameRoot.Exists)
            {
                GameRoot.Dialogue.AutoMode = false;
                GameRoot.Dialogue.SkipMode = false;
            }
            yield return null;
        }

        static StoryGame Game => StoryGame.Current;
        static DialogueService D => GameRoot.Dialogue;

        static void StepUntil(System.Func<bool> until, int max = 800)
        {
            for (int i = 0; i < max && !until(); i++)
            {
                if (D.ChoiceCount > 0) D.Choose(0);
                else if (D.IsActive) D.AutoStep();
            }
        }

        static IEnumerator JumpFromTitle(string chapter)
        {
            StoryStorage.DeleteAll();
            Assert.That(PresenterMode.Jump(StoryPresenter.Scene, chapter), Is.EqualTo(JumpResult.Pending));
            yield return WaitFor(() => !PresenterMode.Chapters.HasPending && Game != null && SceneManager.GetActiveScene().name == StoryPresenter.Scene,
                "the story to load and jump", 20f);
            yield return Frames(2);
        }

        [UnityTest]
        public IEnumerator EveryChapterStartsItsKnotOnACleanStage()
        {
            yield return JumpFromTitle("The waiting room");
            Assert.That(D.IsActive, Is.True);
            foreach (var c in StoryPresenter.List)
            {
                Assert.That(PresenterMode.Jump(StoryPresenter.Scene, c.name), Is.EqualTo(JumpResult.Jumped), c.name);
                yield return Frames(2);
                Assert.That(GameRoot.Flow.InRun, Is.True, c.name);
                Assert.That(Game.Ended, Is.False, c.name + ": a fresh start");
                Assert.That(Game.EndingCardShown, Is.False, c.name + ": no ending card left");
                Assert.That(D.IsActive || D.IsParked, Is.True, c.name + ": the knot plays");
                if (c.knot == "office")
                {
                    StepUntil(() => Game.Investigation != null && D.IsParked);
                    Assert.That(Game.Investigation, Is.Not.Null, "the office opens its hotspots");
                }
                if (c.knot.StartsWith("ending_"))
                {
                    StepUntil(() => Game.EndingCardShown);
                    yield return null;
                    Assert.That(Game.EndingCardShown, Is.True, c.name + " reaches its card");
                }
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator AttractReadsByItself()
        {
            yield return JumpFromTitle("The platform");
            PresenterMode.SetAttractScript(new AttractScript(StoryPresenter.Scene).Jump("The platform", 0.2f).Drive("read", 30f));
            Assert.That(PresenterMode.StartAttract(), Is.True);
            yield return WaitFor(() => Game.History.Count >= 3, "three lines read with no input", 30f);
            Assert.That(D.AutoMode, Is.True, "at the auto-mode pace");
            PresenterMode.StopAttract();
            Assert.That(D.AutoMode, Is.False, "auto mode as it was");
            LogAssert.NoUnexpectedReceived();
        }
    }
}
