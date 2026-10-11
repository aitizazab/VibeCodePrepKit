using System.Collections;
using JamKit.Presenting;
using JamKit.Shell;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static JamKit.Tests.Play.ShellTestUtil;

namespace JamKit.DreamEscape.Tests
{
    /// <summary>The front door into the rain on the real controller: looking at the umbrella locks it and the Fire action
    /// takes it with the dream tool (it opens over her); the autopilot takes it and walks the rain dry, with no Despair;
    /// going on without it soaks her (heavier movement) and sets the save's Despair flag, which a new run's umbrella
    /// doesn't clear. The flag is cleared before and after each test.</summary>
    public class DreamUmbrellaPlayTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return BootToTitle();
            GameRoot.Save.Data.ClearFlag(DreamRain.DespairFlag);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            DebugMenu.TimeScale = 1f;
            if (GameRoot.Exists)
            {
                GameRoot.Input.Synthetic = null;
                GameRoot.Save.Data.ClearFlag(DreamRain.DespairFlag);
            }
            if (Game != null) Game.SetAutopilot(false);
            if (Umbrella != null) Umbrella.autoFire = true;
            PresenterMode.Close();
            yield return null;
        }

        static DreamEscapeGame Game => DreamEscapeGame.Current;
        static DreamUmbrella Umbrella => DreamUmbrella.Current;

        static IEnumerator StartAtTheFinalRun()
        {
            Assert.That(GameRoot.Instance.SceneService.Load(DreamEscapePresenter.Scene), Is.True, "DreamEscape is in Build Settings");
            yield return WaitFor(() => GameRoot.Flow.InRun && !GameRoot.IsLoadingScene && SceneManager.GetActiveScene().name == DreamEscapePresenter.Scene
                && Game != null && Game.Course != null && Umbrella != null && Umbrella.Rain != null, "the dream escape and its front door");
            yield return WaitFor(() => GameRoot.Ui.FadeAlpha == 0f, "the scene's fade-in", 5f);
            Game.JumpToChapter(DreamEscapeCourse.ChapterFinal);
            yield return Frames(3);
            Assert.That(Umbrella.HasUmbrella, Is.False, "the umbrella waits by the door");
            Assert.That(DreamEscapeGame.Despair, Is.False);
        }

        [UnityTest]
        public IEnumerator LookingAtTheUmbrellaAndFiringTakesIt()
        {
            yield return StartAtTheFinalRun();
            var d = Game.Dream;
            // Stand on the porch, 6 m short of the door, and look at the umbrella.
            var feet = new Vector3(0f, d.ExitTop + 0.02f, d.umbrella.Z - 6f);
            var target = new Vector3(d.umbrella.X, d.umbrella.Y, d.umbrella.Z);
            var eye = feet + Vector3.up * Game.player.Def.eyeHeight;
            var dir = target - eye;
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Asin(dir.normalized.y) * Mathf.Rad2Deg;
            Game.player.Teleport(feet, yaw, Vector3.zero, pitch);
            var hands = new SyntheticInput();
            GameRoot.Input.Synthetic = hands;
            yield return Frames(4);
            Assert.That(Umbrella.Locked, Is.True, "the dream tool's aim locks it");
            Assert.That(Umbrella.glow.gameObject.activeSelf, Is.True, "highlighted while it waits");
            hands.Tap(GameRoot.Input.Fire);
            yield return Frames(3);
            Assert.That(Umbrella.HasUmbrella, Is.True, "Fire took it");
            Assert.That(Umbrella.canopy.activeSelf, Is.True, "open over her");
            Assert.That(Umbrella.prop.gameObject.activeSelf, Is.False, "gone from the door");
            Assert.That(Umbrella.glow.gameObject.activeSelf, Is.False);

            // A new run (placed before the porch): the umbrella is back by the door.
            Game.JumpToChapter(DreamEscapeCourse.ChapterFinal);
            yield return Frames(3);
            Assert.That(Umbrella.HasUmbrella, Is.False);
            Assert.That(Umbrella.prop.gameObject.activeSelf, Is.True);
        }

        [UnityTest]
        public IEnumerator WithTheUmbrellaSheWalksTheRainDry()
        {
            yield return StartAtTheFinalRun();
            var d = Game.Dream;
            DebugMenu.TimeScale = 2f;
            Game.SetAutopilot(true);
            yield return WaitFor(() => Game.S > d.despairS + 4f || Game.Leaving, "the autopilot out into the rain", 30f);
            Assert.That(Umbrella.HasUmbrella, Is.True, "the autopilot took it with the tool on its way past");
            Assert.That(Umbrella.Rain.InRain, Is.True);
            Assert.That(Umbrella.Rain.Soak, Is.EqualTo(0f), "dry under it");
            Assert.That(Umbrella.Heavy, Is.False, "she continues normally");
            Assert.That(DreamEscapeGame.Despair, Is.False);
            yield return WaitFor(() => Game.Leaving, "the end of the rain path", 15f);
            Game.SetAutopilot(false);
            Assert.That(DreamEscapeGame.Despair, Is.False);
        }

        [UnityTest]
        public IEnumerator GoingOnWithoutItIsDespairAndItIsKept()
        {
            yield return StartAtTheFinalRun();
            var d = Game.Dream;
            float sprint = Game.player.Def.sprintSpeed;
            DebugMenu.TimeScale = 2f;
            Umbrella.autoFire = false;
            Game.SetAutopilot(true);
            yield return WaitFor(() => Game.S > d.rainStartS + 3f, "out of the door without the umbrella", 30f);
            Assert.That(Umbrella.HasUmbrella, Is.False);
            Assert.That(Umbrella.Rain.Exposed, Is.True);
            yield return WaitFor(() => Game.S > d.despairS + 1f, "past the point of no return", 15f);
            Assert.That(Umbrella.Rain.Soak, Is.GreaterThan(0f), "soaking");
            Assert.That(Umbrella.Heavy, Is.True, "heavier");
            Assert.That(Game.player.Def.sprintSpeed, Is.LessThan(sprint), "slower in the rain");
            Assert.That(Umbrella.Rain.DespairThisRun, Is.True);
            Assert.That(DreamEscapeGame.Despair, Is.True, "the Despair route is kept in the save");
            Game.SetAutopilot(false);

            // A new run: the umbrella is back, the movement is dry again, and the choice stays made.
            Game.JumpToChapter(DreamEscapeCourse.ChapterFinal);
            yield return Frames(3);
            Assert.That(Umbrella.HasUmbrella, Is.False);
            Assert.That(Umbrella.Rain.DespairThisRun, Is.False);
            yield return WaitFor(() => !Umbrella.Heavy, "the soak to dry off", 5f);
            Assert.That(Game.player.Def.sprintSpeed, Is.EqualTo(sprint).Within(1e-4f), "her own tuning again");
            Assert.That(DreamEscapeGame.Despair, Is.True, "a persistent flag");
        }
    }
}
