using System.Collections;
using JamKit.Presenting;
using JamKit.Shell;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static JamKit.Tests.Play.ShellTestUtil;

namespace JamKit.Doomlike.Tests
{
    /// <summary>Presenter mode in the DOOM-like: a chapter picked on the title starts the run; each
    /// level, the boss (close, facing it) and the end tally jump cleanly; attract mode walks the level
    /// by its synthetic sticks.</summary>
    public class DoomlikePresenterPlayTests
    {
        [UnitySetUp]
        public IEnumerator SetUp() => BootToTitle();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            PresenterMode.Close();
            PresenterMode.SetAttractScript(DoomPresenter.Attract());
            DoomGame.SeedOverride = 0;
            DoomGame.SkipClickToPlay = false;
            yield return null;
        }

        static DoomGame Game => DoomGame.Current;

        static IEnumerator JumpFromTitle(string chapter)
        {
            DoomGame.SeedOverride = 77;
            DoomGame.SkipClickToPlay = true;
            Assert.That(PresenterMode.Jump(DoomPresenter.Scene, chapter), Is.EqualTo(JumpResult.Pending));
            yield return WaitFor(() => !PresenterMode.Chapters.HasPending && Game != null && SceneManager.GetActiveScene().name == DoomPresenter.Scene,
                "the DOOM-like to load and jump", 20f);
            yield return WaitFor(() => GameRoot.Ui.FadeAlpha == 0f, "the fade-in", 3f);
        }

        [UnityTest]
        public IEnumerator EveryChapterJumpsCleanly()
        {
            yield return JumpFromTitle(DoomPresenter.Level2);
            Assert.That(Game.LevelIndex, Is.EqualTo(1));
            foreach (var name in DoomPresenter.Chapters)
            {
                Assert.That(PresenterMode.Jump(DoomPresenter.Scene, name), Is.EqualTo(JumpResult.Jumped), name);
                yield return Frames(3);
                Assert.That(GameRoot.Flow.InRun, Is.True, name);
                Assert.That(GameRoot.Ui.FadeAlpha, Is.EqualTo(0f), name);
                Assert.That(Game.player.Alive, Is.True, name);
                switch (name)
                {
                    case DoomPresenter.Level1: Assert.That(Game.LevelIndex, Is.EqualTo(0)); Assert.That(Game.CurrentPhase, Is.EqualTo(DoomGame.Phase.Playing)); break;
                    case DoomPresenter.Level3: Assert.That(Game.LevelIndex, Is.EqualTo(2)); break;
                    case DoomPresenter.Boss:
                        DoomEnemy boss = null;
                        foreach (var e in Game.Enemies) if (e.asset.isBoss) boss = e;
                        Assert.That(boss, Is.Not.Null, "a level has the boss");
                        Assert.That(Vector3.Distance(boss.transform.position, Game.player.transform.position), Is.LessThan(16f), "close to the boss");
                        break;
                    case DoomPresenter.Tally:
                        Assert.That(Game.Hud.TallyShown, Is.True, "the tally");
                        Assert.That(Game.CurrentPhase, Is.EqualTo(DoomGame.Phase.Tally));
                        break;
                }
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator AttractWalksTheLevelBySyntheticInput()
        {
            yield return JumpFromTitle(DoomPresenter.Level1);
            PresenterMode.SetAttractScript(new AttractScript(DoomPresenter.Scene).Jump(DoomPresenter.Level1, 0.5f).Drive("explore", 10f));
            Assert.That(PresenterMode.StartAttract(), Is.True);
            yield return WaitFor(() => Game.player.DistanceMoved > 2f, "the player to walk", 10f);
            Assert.That(GameRoot.Input.IsSynthetic, Is.True);
            PresenterMode.StopAttract();
            Assert.That(GameRoot.Input.IsSynthetic, Is.False);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
