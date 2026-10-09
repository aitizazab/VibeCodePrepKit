using System.Collections;
using System.IO;
using JamKit.Flow;
using JamKit.Presenting;
using JamKit.Sandbox;
using JamKit.Shell;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static JamKit.Tests.Play.ShellTestUtil;

namespace JamKit.Tests.Play
{
    /// <summary>Presenter mode on the sandbox: the panel opens and stops time behind it, chapters jump
    /// (from the title too), Invincible blocks damage, slow motion and freeze stack with pause and hit
    /// stop, Hide UI hides the HUD, Photo writes a PNG, attract mode plays by itself, and Exit puts
    /// everything back.</summary>
    public class PresenterPlayTests
    {
        [UnitySetUp]
        public IEnumerator SetUp() => BootToTitle();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            PresenterMode.Close();
            PresenterMode.PhotoFolder = null;
            yield return null;
        }

        static SandboxGame Game => Object.FindAnyObjectByType<SandboxGame>();

        [Test]
        public void TheSandboxChaptersAndAttractScriptLineUp()
        {
            var r = new ChapterRegistry();
            SandboxGame.RegisterChapters(r, null, new object());
            var idx = new System.Collections.Generic.List<int>();
            r.InGroup(SandboxGame.Scene, idx);
            var names = new System.Collections.Generic.List<string>();
            foreach (int i in idx) names.Add(r.NameAt(i));
            Assert.That(names, Is.EqualTo(SandboxGame.Chapters), "every declared chapter is registered, in order");
            var problems = SandboxGame.Attract().Validate(names, SandboxGame.Segments);
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [UnityTest]
        public IEnumerator ThePanelListsChaptersStopsTimeAndJumps()
        {
            yield return StartSandbox();
            var ui = GameRoot.Ui;
            Assert.That(PresenterMode.IsOpen, Is.False, "nothing shows until it's opened");
            PresenterMode.Open();
            yield return null;
            yield return null;
            Assert.That(PresenterMode.IsOpen, Is.True);
            Assert.That(ui.Top, Is.SameAs(ui.Presenter), "an overlay on top");
            Assert.That(ui.Presenter.Chapters, Is.EqualTo(SandboxGame.Chapters), "the running game's chapters");
            Assert.That(Time.timeScale, Is.EqualTo(0f), "game time stops behind the open panel");
            Assert.That(GameRoot.Input.GameplayAllowed, Is.False);

            Assert.That(ui.Presenter.Activate(PresenterScreen.ChapterButton(1)), Is.True, "press The sign");
            yield return null;
            Assert.That(PresenterMode.IsOpen, Is.False, "jumping closes the panel");
            Assert.That(PresenterMode.Chapters.LastJumped, Is.EqualTo(SandboxGame.ChapterSign));
            Assert.That(GameRoot.Dialogue.IsActive, Is.True, "reading the sign");
            yield return FinishDialogue();
            yield return null;
            Assert.That(Time.timeScale, Is.EqualTo(1f), "time runs again");

            Assert.That(PresenterMode.Jump(SandboxGame.ChapterStart), Is.EqualTo(JumpResult.Jumped));
            Assert.That(Game.Collected, Is.EqualTo(0));
            Assert.That(PresenterMode.NextChapter(), Is.EqualTo(JumpResult.Jumped), "next after Start is The sign");
            Assert.That(PresenterMode.Chapters.LastJumped, Is.EqualTo(SandboxGame.ChapterSign));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator AChapterChosenOnTheTitleStartsTheRunThenJumps()
        {
            Assert.That(GameRoot.Flow.Current, Is.EqualTo(GameState.Title));
            Assert.That(PresenterMode.CurrentGroup, Is.EqualTo("Sandbox"), "the active game's chapters are listed on the title");
            var r = PresenterMode.Jump("Sandbox", SandboxGame.ChapterSign);
            Assert.That(r, Is.EqualTo(JumpResult.Pending));
            yield return WaitFor(() => !PresenterMode.Chapters.HasPending, "the scene to load and the jump to run", 20f);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Sandbox"));
            Assert.That(GameRoot.Flow.InRun, Is.True);
            Assert.That(PresenterMode.Chapters.LastJumped, Is.EqualTo(SandboxGame.ChapterSign));
            yield return WaitFor(() => GameRoot.Ui.FadeAlpha == 0f, "no half-faded screen once the scene's fade-in ends", 2f);
            yield return FinishDialogue();
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator InvincibleBlocksDamageAndExitUndoesIt()
        {
            yield return StartSandbox();
            var player = Game.player;
            var health = player.Damageable.Health;
            PresenterMode.Invincible = true;
            Assert.That(DebugMenu.PlayerInvincible, Is.True);
            player.Damageable.Hit(1f);
            Assert.That(health.Current, Is.EqualTo(health.Max), "no damage while invincible");
            Assert.That(GameRoot.Ui.PresenterIndicator.style.display.value, Is.EqualTo(DisplayStyle.None), "no visible sign of it");
            PresenterMode.Close();
            Assert.That(PresenterMode.Invincible, Is.False);
            player.Damageable.Hit(1f);
            Assert.That(health.Current, Is.EqualTo(health.Max - 1f), "damage again after Exit");
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator SlowMotionAndFreezeStackWithPauseAndHitStop()
        {
            yield return StartSandbox();
            Assert.That(PresenterMode.CycleSlowMotion(), Is.EqualTo(0.5f));
            yield return null;
            Assert.That(Time.timeScale, Is.EqualTo(0.5f), "slow motion");
            GameRoot.Instance.Pause();
            yield return null;
            Assert.That(Time.timeScale, Is.EqualTo(0f), "paused stays stopped");
            GameRoot.Instance.Resume();
            yield return null;
            Assert.That(Time.timeScale, Is.EqualTo(0.5f), "back to slow motion after the pause");
            DebugMenu.TimeScale = 2f;
            yield return null;
            Assert.That(Time.timeScale, Is.EqualTo(1f), "debug x2 times presenter x0.5");
            DebugMenu.TimeScale = 1f;
            GameRoot.Juice.HitStop(0.1f);
            yield return null;
            Assert.That(Time.timeScale, Is.EqualTo(0f), "a hit stop still freezes");
            yield return WaitFor(() => Time.timeScale == 0.5f, "the hit stop to end (real time)", 2f);
            PresenterMode.Frozen = true;
            yield return null;
            Assert.That(Time.timeScale, Is.EqualTo(0f), "freeze frame");
            Assert.That(GameRoot.Input.GameplayAllowed, Is.False, "no input moves a frozen frame");
            PresenterMode.Close();
            yield return null;
            Assert.That(Time.timeScale, Is.EqualTo(1f), "Exit restores normal time");
            Assert.That(PresenterMode.Active, Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator HideUiHidesTheHudAndExitShowsItAgain()
        {
            yield return StartSandbox();
            var hudLayer = GameRoot.Ui.Root.Q<VisualElement>("hud-layer");
            Assert.That(hudLayer, Is.Not.Null);
            var before = hudLayer.style.display;
            PresenterMode.UiHidden = true;
            yield return null;
            Assert.That(hudLayer.style.display.value, Is.EqualTo(DisplayStyle.None), "the HUD layer is hidden");
            Assert.That(GameRoot.Ui.UiHidden, Is.True);
            PresenterMode.Close();
            yield return null;
            Assert.That(PresenterMode.UiHidden, Is.False);
            Assert.That(hudLayer.style.display, Is.EqualTo(before), "restored exactly");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator PhotoWritesAPngAndPutsTheUiBack()
        {
            yield return StartSandbox();
            string folder = Path.Combine(Application.temporaryCachePath, "presenter-photo-test");
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
            PresenterMode.PhotoFolder = folder;
            string seen = "";
            System.Action<string> onSaved = p => seen = p;
            PresenterMode.PhotoSaved += onSaved;
            try
            {
                PresenterMode.Open();
                yield return null;
                Assert.That(GameRoot.Ui.Presenter.Activate("presenter-photo"), Is.True);
                Assert.That(PresenterMode.IsOpen, Is.False, "the panel gets out of the picture");
                yield return WaitFor(() => seen.Length > 0, "the photo", 10f);
                Assert.That(File.Exists(seen), Is.True, seen);
                Assert.That(new FileInfo(seen).Length, Is.GreaterThan(0));
                Assert.That(Path.GetFileName(seen), Does.EndWith(".png"));
                Assert.That(Path.GetFullPath(Path.GetDirectoryName(seen)), Is.EqualTo(Path.GetFullPath(folder)));
                Assert.That(PresenterMode.UiHidden, Is.False, "the UI comes back after the shot");
                Assert.That(PresenterMode.LastPhotoPath, Is.EqualTo(seen));
            }
            finally
            {
                PresenterMode.PhotoSaved -= onSaved;
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator AttractModePlaysBySyntheticInputAndStopsCleanly()
        {
            yield return StartSandbox();
            bool auto = GameRoot.Dialogue.AutoMode;
            Assert.That(PresenterMode.StartAttract(), Is.True);
            Assert.That(PresenterMode.AttractRunning, Is.True);
            Assert.That(GameRoot.Input.IsSynthetic, Is.True, "attract mode drives the game through a synthetic pad");
            Assert.That(PresenterMode.InvincibleNow, Is.True, "and plays invincible");
            Vector3 from = Game.player.transform.position;
            yield return WaitFor(() => Game != null && (Game.Collected > 0 || (Game.player.transform.position - from).sqrMagnitude > 0.25f), "the sandbox to be played", 15f);
            PresenterMode.StopAttract();
            Assert.That(PresenterMode.AttractRunning, Is.False);
            Assert.That(GameRoot.Input.IsSynthetic, Is.False, "real input again");
            Assert.That(GameRoot.Dialogue.AutoMode, Is.EqualTo(auto), "auto mode as it was");
            Assert.That(PresenterMode.InvincibleNow, Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ExitRestoresEverything()
        {
            yield return StartSandbox();
            PresenterMode.Invincible = true;
            PresenterMode.SetSlowMotion(0.25f);
            PresenterMode.Frozen = true;
            PresenterMode.UiHidden = true;
            PresenterMode.ShowIndicator = true;
            PresenterMode.Open();
            yield return null;
            Assert.That(GameRoot.Ui.PresenterIndicator.style.display.value, Is.EqualTo(DisplayStyle.Flex), "the indicator when asked for");
            Assert.That(GameRoot.Ui.Presenter.Activate("presenter-exit"), Is.True);
            yield return null;
            Assert.That(PresenterMode.Active, Is.False, "nothing presenter mode did is left");
            Assert.That(PresenterMode.IsOpen, Is.False);
            Assert.That(PresenterMode.Invincible, Is.False);
            Assert.That(PresenterMode.SlowMotion, Is.EqualTo(1f));
            Assert.That(PresenterMode.Frozen, Is.False);
            Assert.That(PresenterMode.UiHidden, Is.False);
            Assert.That(GameRoot.Ui.PresenterIndicator.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(GameRoot.Input.GameplayAllowed, Is.True);
            Assert.That(GameRoot.Ui.Top, Is.Null, "back to the game");
            LogAssert.NoUnexpectedReceived();
        }
    }
}
