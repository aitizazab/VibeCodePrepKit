using System;
using System.Collections;
using JamKit.Flow;
using JamKit.Shell;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JamKit.Tests.Play
{
    /// <summary>Shared steps for the shell's PlayMode tests: boot to the title on a fresh save slot,
    /// start the sandbox, wait with a timeout.</summary>
    public static class ShellTestUtil
    {
        public const string Slot = "playmode-tests";

        public static IEnumerator WaitFor(Func<bool> condition, string what, float timeoutSeconds = 15f)
        {
            float end = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > end)
                    Assert.Fail($"Timed out after {timeoutSeconds} s waiting for {what} (state {GameRoot.Flow.Current}, scene {SceneManager.GetActiveScene().name})");
                yield return null;
            }
        }

        public static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        /// <summary>Loads Boot like a fresh launch (from whatever state the last test left) and waits for the title.</summary>
        public static IEnumerator BootToTitle()
        {
            Assert.That(GameRoot.Exists, Is.True, "GameRoot should create itself before the first scene");
            yield return WaitFor(() => !GameRoot.IsLoadingScene, "any running load to finish");
            DebugMenu.GodMode = false;
            DebugMenu.TimeScale = 1f;
            // Fresh save for every test (settings, flags, records, first-run cards).
            GameRoot.Save.UseSlot(Slot, true);
            UseSandboxConfig();
            // Wait for Boot itself to load first: the previous test may already have left us on
            // the title, and a "title reached" check that passes before Boot loads races the boot.
            var op = SceneManager.LoadSceneAsync("Boot");
            while (!op.isDone) yield return null;
            yield return WaitFor(() => GameRoot.Flow.Current == GameState.Title && !GameRoot.IsLoadingScene
                && SceneManager.GetActiveScene().name == "Title", "the title screen");
            Assert.That(GameRoot.Ui.Title.IsVisible, Is.True, "title screen visible");
        }

        public const string SandboxPresetPath = "Assets/JamKit/Sandbox/SandboxGameConfig.asset";

        /// <summary>The shell tests play the sandbox. When another template is the active one
        /// ("JamKit/Use Template/..."), run on an in-memory copy of the config pointed at the sandbox
        /// (with the sandbox's How to Play cards), so the asset on disk is never changed.</summary>
        public static void UseSandboxConfig()
        {
            var active = Resources.Load<GameConfig>(GameConfig.ResourcePath);
            if (active == null) return;
            if (active.firstGameplayScene == "Sandbox") { GameRoot.UseConfig(active); return; }
            var copy = UnityEngine.Object.Instantiate(active);
            copy.firstGameplayScene = "Sandbox";
#if UNITY_EDITOR
            var preset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameConfig>(SandboxPresetPath);
            if (preset != null)
            {
                copy.gameTitle = preset.gameTitle;
                copy.tagline = preset.tagline;
                copy.howToPlay = preset.howToPlay;
                copy.showHowToPlayOnFirstRun = preset.showHowToPlayOnFirstRun;
            }
#endif
            GameRoot.UseConfig(copy);
        }

        /// <summary>From the title: Play (skipping the first-run cards) and finish the intro dialogue.</summary>
        public static IEnumerator StartSandbox(bool skipIntro = true)
        {
            GameRoot.Save.Data.seenHowToPlay = true;
            GameRoot.Ui.Title.Activate("play");
            yield return WaitFor(() => GameRoot.Flow.InRun && !GameRoot.IsLoadingScene && SceneManager.GetActiveScene().name == "Sandbox", "the sandbox");
            yield return WaitFor(() => GameRoot.Dialogue.IsActive, "the intro dialogue");
            if (skipIntro) yield return FinishDialogue();
        }

        public static IEnumerator FinishDialogue()
        {
            for (int i = 0; i < 200 && GameRoot.Dialogue.IsActive; i++)
            {
                GameRoot.Dialogue.AutoStep();
                yield return null;
            }
            Assert.That(GameRoot.Dialogue.IsActive, Is.False, "dialogue finished");
            yield return null;
        }
    }
}
