using System.Collections;
using JamKit.Flow;
using JamKit.Sandbox;
using JamKit.Shell;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static JamKit.Tests.Play.ShellTestUtil;

namespace JamKit.Tests.Play
{
    /// <summary>The whole shell, end to end, in the real scenes: title, first-run cards, play,
    /// dialogue, pause, lose, retry, win, credits and back to the title.</summary>
    public class ShellFlowTests
    {
        [UnitySetUp]
        public IEnumerator SetUp() => BootToTitle();

        [UnityTest]
        public IEnumerator FullFlow()
        {
            var ui = GameRoot.Ui;
            var flow = GameRoot.Flow;

            // Play asks for the difficulty (Normal preselected), then, on the first run, shows the
            // How to Play cards, and the last card starts the game.
            ui.Title.Activate("play");
            yield return null;
            Assert.That(ui.IsOverlayOpen(ui.DifficultyPicker), Is.True, "the difficulty picker");
            Assert.That(ui.DifficultyPicker.Preselected, Is.EqualTo(Difficulty.Normal));
            Assert.That(ui.DifficultyPicker.Activate(DifficultyScreen.ButtonName(Difficulty.Normal)), Is.True);
            yield return null;
            Assert.That(ui.IsOverlayOpen(ui.DifficultyPicker), Is.False, "choosing closes the picker");
            Assert.That(ui.IsOverlayOpen(ui.HowToPlay), Is.True, "first-run How to Play");
            Assert.That(ui.HowToPlay.PageCount, Is.EqualTo(3));
            for (int i = 0; i < 3; i++)
            {
                ui.HowToPlay.Activate("next");
                yield return null;
            }
            Assert.That(ui.IsOverlayOpen(ui.HowToPlay), Is.False);
            Assert.That(GameRoot.Save.Data.seenHowToPlay, Is.True);

            yield return WaitFor(() => flow.InRun && !GameRoot.IsLoadingScene && SceneManager.GetActiveScene().name == "Sandbox", "the sandbox");
            yield return WaitFor(() => GameRoot.Dialogue.IsActive, "the intro dialogue");
            yield return null;
            Assert.That(flow.Current, Is.EqualTo(GameState.Dialogue));
            Assert.That(Time.timeScale, Is.EqualTo(0f), "game time stops during dialogue");
            Assert.That(GameRoot.Dialogue.CurrentLine, Does.Contain("Welcome"), "no flag yet: the first-visit line");
            yield return FinishDialogue();
            Assert.That(flow.Current, Is.EqualTo(GameState.Playing));
            Assert.That(GameRoot.Save.Data.HasFlag("met_guide"), Is.True, "set_flag ran");
            Assert.That(GameRoot.Hud.IsShown, Is.True);
            Assert.That(GameRoot.Hud.ScoreText, Is.EqualTo("Coins 0 / 3"));
            yield return null;
            Assert.That(Time.timeScale, Is.EqualTo(1f));

            // Pause and resume.
            ui.PressPause();
            yield return null;
            Assert.That(flow.Current, Is.EqualTo(GameState.Paused));
            Assert.That(ui.Pause.IsVisible, Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(0f));
            ui.Pause.Activate("settings");
            Assert.That(ui.IsOverlayOpen(ui.Settings), Is.True);
            ui.Back();
            Assert.That(ui.IsOverlayOpen(ui.Settings), Is.False, "Back closes the overlay first");
            Assert.That(ui.Pause.IsVisible, Is.True);
            ui.Pause.Activate("resume");
            yield return null;
            Assert.That(flow.Current, Is.EqualTo(GameState.Playing));
            Assert.That(ui.Pause.IsVisible, Is.False);

            // Lose, then retry: a fresh run, and the dialogue now knows the flag.
            GameRoot.Lose();
            yield return null;
            Assert.That(flow.Current, Is.EqualTo(GameState.GameOver));
            Assert.That(ui.GameOver.IsVisible, Is.True);
            Assert.That(GameRoot.Save.Data.runsStarted, Is.EqualTo(1));
            ui.GameOver.Activate("retry");
            yield return WaitFor(() => GameRoot.Dialogue.IsActive && !GameRoot.IsLoadingScene, "the retried run's dialogue");
            Assert.That(GameRoot.Dialogue.CurrentLine, Does.Contain("back"), "has_flag sees the saved flag");
            Assert.That(GameRoot.Save.Data.runsStarted, Is.EqualTo(2));
            yield return FinishDialogue();

            // Win for real: walk the player onto each coin.
            var game = Object.FindAnyObjectByType<SandboxGame>();
            Assert.That(game, Is.Not.Null);
            Assert.That(game.Total, Is.EqualTo(3));
            for (int i = 0; i < game.Coins.Count; i++)
            {
                game.player.Teleport(game.Coins[i].transform.position);
                yield return Frames(2);
                Assert.That(game.Collected, Is.EqualTo(i + 1), "coin " + i + " collected");
            }
            yield return null;
            Assert.That(flow.Current, Is.EqualTo(GameState.Victory));
            Assert.That(ui.Victory.IsVisible, Is.True);
            Assert.That(GameRoot.Run.score, Is.EqualTo(300));
            Assert.That(GameRoot.Save.Data.runsWon, Is.EqualTo(1));
            Assert.That(GameRoot.Save.Data.bestTime, Is.GreaterThan(0f));

            ui.Victory.Activate("continue");
            yield return WaitFor(() => flow.Current == GameState.Credits && !GameRoot.IsLoadingScene && SceneManager.GetActiveScene().name == "Title", "credits");
            Assert.That(ui.Credits.IsVisible, Is.True);
            yield return Frames(5);
            ui.Back();
            yield return WaitFor(() => flow.Current == GameState.Title && !GameRoot.IsLoadingScene, "the title");
            Assert.That(ui.Title.IsVisible, Is.True);
            Assert.That(ui.Credits.IsVisible, Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator HazardHurtsWithFeedbackUntilGameOver()
        {
            yield return StartSandbox();
            var game = Object.FindAnyObjectByType<SandboxGame>();
            var hazard = game.Hazards[0];
            var player = game.player;
            var health = player.Damageable.Health;
            Assert.That(health.Current, Is.EqualTo(3f));

            // God mode: touching the hazard does nothing.
            DebugMenu.GodMode = true;
            player.Teleport(hazard.transform.position);
            yield return Frames(3);
            Assert.That(health.Current, Is.EqualTo(3f), "god mode");
            DebugMenu.GodMode = false;

            player.Teleport(hazard.transform.position);
            yield return null;
            yield return null;
            Assert.That(health.Current, Is.EqualTo(2f), "one hit");
            Assert.That(health.IsInvulnerable, Is.True, "i-frames after a hit");
            Assert.That(player.GetComponent<SpriteFlash>().IsFlashing, Is.True, "sprite flash");
            Assert.That(GameRoot.Juice.ShakeState.Trauma, Is.GreaterThan(0f), "screen shake");
            Assert.That(GameRoot.Audio.LastSfx, Is.EqualTo("hurt"));
            Assert.That(GameRoot.Ui.FloatingTextCount, Is.GreaterThan(0), "damage number");
            Assert.That(GameRoot.Hud.HealthFraction, Is.EqualTo(2f / 3f).Within(0.01f));
            Assert.That(CameraShaker.Current, Is.Not.Null);

            // Stay on the hazard: i-frames space the hits out, and at 0 HP it's game over.
            float start = Time.realtimeSinceStartup;
            while (GameRoot.Flow.Current == GameState.Playing && Time.realtimeSinceStartup - start < 10f)
            {
                player.Teleport(hazard.transform.position);
                yield return null;
            }
            Assert.That(GameRoot.Flow.Current, Is.EqualTo(GameState.GameOver));
            Assert.That(health.IsDead, Is.True);
            Assert.That(Time.realtimeSinceStartup - start, Is.GreaterThan(1.5f), "two more hits needed the i-frames to run out twice");
            Assert.That(GameRoot.Ui.GameOver.IsVisible, Is.True);

            GameRoot.Ui.GameOver.Activate("title");
            yield return WaitFor(() => GameRoot.Flow.Current == GameState.Title && !GameRoot.IsLoadingScene, "the title");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator HitStopFreezesTimeBriefly()
        {
            yield return StartSandbox();
            GameRoot.Juice.HitStop(0.1f);
            yield return null;
            Assert.That(Time.timeScale, Is.EqualTo(0f), "frozen");
            yield return WaitFor(() => Time.timeScale == 1f, "time to resume", 2f);
            DebugMenu.TimeScale = 0.25f;
            yield return null;
            Assert.That(Time.timeScale, Is.EqualTo(0.25f), "debug time scale");
            DebugMenu.TimeScale = 1f;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator PauseMenuRestartAndQuit()
        {
            yield return StartSandbox();
            var ui = GameRoot.Ui;
            GameRoot.Run.score = 50;
            ui.PressPause();
            yield return null;
            ui.Pause.Activate("restart");
            yield return WaitFor(() => GameRoot.Flow.InRun && !GameRoot.IsLoadingScene, "the restarted run");
            Assert.That(GameRoot.Run.score, Is.EqualTo(0), "fresh run");
            yield return WaitFor(() => GameRoot.Dialogue.IsActive, "the intro again");

            // Pause works during dialogue too, and Resume goes back to the dialogue.
            ui.PressPause();
            yield return null;
            Assert.That(GameRoot.Flow.Current, Is.EqualTo(GameState.Paused));
            ui.PressPause();
            yield return null;
            Assert.That(GameRoot.Flow.Current, Is.EqualTo(GameState.Dialogue));

            ui.PressPause();
            yield return null;
            ui.Pause.Activate("quit");
            yield return WaitFor(() => GameRoot.Flow.Current == GameState.Title && !GameRoot.IsLoadingScene
                && SceneManager.GetActiveScene().name == "Title", "the title");
            Assert.That(GameRoot.Dialogue.IsActive, Is.False);
            Assert.That(GameRoot.Hud.IsShown, Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator DebugMenuWinAndNextScene()
        {
            yield return StartSandbox();
            DebugMenu.Win();
            yield return null;
            Assert.That(GameRoot.Flow.Current, Is.EqualTo(GameState.Victory));
            GameRoot.Ui.Victory.Activate("continue");
            yield return WaitFor(() => GameRoot.Flow.Current == GameState.Credits && !GameRoot.IsLoadingScene, "credits");
            GameRoot.Ui.Credits.Activate("back");
            yield return WaitFor(() => GameRoot.Flow.Current == GameState.Title && !GameRoot.IsLoadingScene, "the title");

            // Title -> credits -> back, without a scene change.
            GameRoot.Ui.Title.Activate("credits");
            yield return null;
            Assert.That(GameRoot.Flow.Current, Is.EqualTo(GameState.Credits));
            GameRoot.Ui.Back();
            yield return null;
            Assert.That(GameRoot.Flow.Current, Is.EqualTo(GameState.Title));
            LogAssert.NoUnexpectedReceived();
        }
    }
}
