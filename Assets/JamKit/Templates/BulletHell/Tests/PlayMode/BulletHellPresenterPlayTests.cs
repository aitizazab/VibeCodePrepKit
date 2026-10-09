using System.Collections;
using JamKit.Presenting;
using JamKit.Roguelite;
using JamKit.Shell;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static JamKit.Tests.Play.ShellTestUtil;

namespace JamKit.BulletHell.Tests
{
    /// <summary>Presenter mode in the bullet hell: a chapter picked on the title starts the run and
    /// lands there; every chapter jumps cleanly in a running game (the right floor and room, no fade
    /// left, still in the run); attract mode's fight segment flies the ship by its synthetic stick.</summary>
    public class BulletHellPresenterPlayTests
    {
        [UnitySetUp]
        public IEnumerator SetUp() => BootToTitle();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            PresenterMode.Close();
            PresenterMode.SetAttractScript(BulletHellPresenter.Attract());
            BulletHellGame.SeedOverride = 0;
            yield return null;
        }

        static BulletHellGame Game => BulletHellGame.Current;

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
            BulletHellGame.SeedOverride = 4242;
            Assert.That(PresenterMode.Jump(BulletHellPresenter.Scene, chapter), Is.EqualTo(JumpResult.Pending), "from the title the run starts first");
            yield return WaitFor(() => !PresenterMode.Chapters.HasPending && Game != null && SceneManager.GetActiveScene().name == BulletHellPresenter.Scene,
                "the bullet hell to load and jump", 20f);
            yield return WaitFor(() => GameRoot.Ui.FadeAlpha == 0f, "the fade-in", 3f);
        }

        [UnityTest]
        public IEnumerator EveryChapterJumpsCleanly()
        {
            yield return JumpFromTitle(BulletHellPresenter.Boss2);
            Assert.That(Game.Run.floor, Is.EqualTo(1));
            Assert.That(Game.RoomType, Is.EqualTo(RoomType.Boss));
            yield return FinishLines();

            foreach (var name in BulletHellPresenter.Chapters)
            {
                Assert.That(PresenterMode.Jump(BulletHellPresenter.Scene, name), Is.EqualTo(JumpResult.Jumped), name);
                yield return Frames(3);
                Assert.That(GameRoot.Flow.InRun, Is.True, name + ": still in the run");
                Assert.That(GameRoot.Ui.FadeAlpha, Is.EqualTo(0f), name + ": no fade left");
                Assert.That(GameRoot.Ui.Top, Is.Null, name + ": no menu left open");
                switch (name)
                {
                    case BulletHellPresenter.Floor1: Assert.That(Game.Run.floor, Is.EqualTo(0)); Assert.That(Game.RoomType, Is.EqualTo(RoomType.Combat)); break;
                    case BulletHellPresenter.Floor2: Assert.That(Game.Run.floor, Is.EqualTo(1)); Assert.That(Game.RoomType, Is.EqualTo(RoomType.Combat)); break;
                    case BulletHellPresenter.Floor3: Assert.That(Game.Run.floor, Is.EqualTo(2)); Assert.That(Game.RoomType, Is.EqualTo(RoomType.Combat)); break;
                    case BulletHellPresenter.Boss1: Assert.That(Game.Run.floor, Is.EqualTo(0)); Assert.That(Game.RoomType, Is.EqualTo(RoomType.Boss)); break;
                    case BulletHellPresenter.Boss3: Assert.That(Game.Run.floor, Is.EqualTo(2)); Assert.That(Game.RoomType, Is.EqualTo(RoomType.Boss)); break;
                    case BulletHellPresenter.Draft:
                        yield return WaitFor(() => Game.Choice.IsOpen && Game.Choice.Current == ChoicePanel.Kind.Draft, "the draft", 5f);
                        break;
                    case BulletHellPresenter.Encounter:
                        Assert.That(Game.RoomType, Is.EqualTo(RoomType.Forge));
                        break;
                }
                yield return FinishLines();
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator AttractFightFliesTheShipBySyntheticInput()
        {
            yield return JumpFromTitle(BulletHellPresenter.Floor1);
            yield return FinishLines();
            PresenterMode.SetAttractScript(new AttractScript(BulletHellPresenter.Scene).Jump(BulletHellPresenter.Floor1, 0.5f).Drive("fight", 8f));
            Vector2 start = Game.player.Position;
            float travelled = 0f;
            Vector2 last = start;
            Assert.That(PresenterMode.StartAttract(), Is.True);
            float end = Time.realtimeSinceStartup + 7f;
            while (Time.realtimeSinceStartup < end)
            {
                travelled += (Game.player.Position - last).magnitude;
                last = Game.player.Position;
                yield return null;
            }
            Assert.That(PresenterMode.AttractRunning, Is.True);
            Assert.That(travelled, Is.GreaterThan(1f), "the ship weaves about");
            Assert.That(GameRoot.Flow.InRun, Is.True, "invincible: the run goes on");
            PresenterMode.StopAttract();
            Assert.That(GameRoot.Input.IsSynthetic, Is.False);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
