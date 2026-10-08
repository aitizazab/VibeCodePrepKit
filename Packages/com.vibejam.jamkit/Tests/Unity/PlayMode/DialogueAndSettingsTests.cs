using System.Collections;
using System.Collections.Generic;
using JamKit.Dialogue;
using JamKit.Flow;
using JamKit.Sandbox;
using JamKit.Shell;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static JamKit.Tests.Play.ShellTestUtil;

namespace JamKit.Tests.Play
{
    public class DialogueTests
    {
        readonly List<string> _tags = new List<string>();
        readonly List<string> _events = new List<string>();
        readonly List<string> _ended = new List<string>();

        void OnTag(DialogueTag t) => _tags.Add(t.key);
        void OnEvent(string e) => _events.Add(e);
        void OnEnded(string knot) => _ended.Add(knot);

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return BootToTitle();
            _tags.Clear();
            _events.Clear();
            _ended.Clear();
            GameRoot.Dialogue.TagReceived += OnTag;
            GameRoot.Dialogue.EventRaised += OnEvent;
            GameRoot.Dialogue.Ended += OnEnded;
        }

        [TearDown]
        public void TearDown()
        {
            if (!GameRoot.Exists) return;
            GameRoot.Dialogue.TagReceived -= OnTag;
            GameRoot.Dialogue.EventRaised -= OnEvent;
            GameRoot.Dialogue.Ended -= OnEnded;
        }

        [UnityTest]
        public IEnumerator TypesAdvancesChoosesAndHandlesTags()
        {
            yield return StartSandbox(skipIntro: false);
            var d = GameRoot.Dialogue;
            var view = GameRoot.Ui.Dialogue;

            Assert.That(d.Speaker, Is.EqualTo("Guide"));
            Assert.That(view.SpeakerText, Is.EqualTo("Guide"));
            Assert.That(view.LeftCharacter, Is.EqualTo("Guide"), "#portrait:Guide happy left");
            Assert.That(_tags, Does.Contain("sfx"));

            // Typewriter: the first press completes the line, the second goes on.
            Assert.That(d.IsTyping, Is.True);
            Assert.That(view.BodyText.Length, Is.LessThan(d.CurrentLine.Length));
            d.Advance();
            Assert.That(d.IsTyping, Is.False);
            Assert.That(view.BodyText, Is.EqualTo(d.CurrentLine));
            string first = d.CurrentLine;
            d.Advance();
            Assert.That(d.CurrentLine, Is.Not.EqualTo(first));

            // On to the Rival's line: right portrait, shake.
            for (int i = 0; i < 10 && d.Speaker != "Rival"; i++) { d.Advance(); d.Advance(); }
            Assert.That(d.Speaker, Is.EqualTo("Rival"));
            Assert.That(view.RightCharacter, Is.EqualTo("Rival"));
            Assert.That(_tags, Does.Contain("shake"));
            Assert.That(GameRoot.Juice.ShakeState.Trauma, Is.GreaterThan(0f));

            // Through to the choice.
            for (int i = 0; i < 10 && d.ChoiceCount == 0; i++)
            {
                d.Advance();
                yield return null;
            }
            Assert.That(d.ChoiceCount, Is.EqualTo(2));
            Assert.That(view.VisibleChoiceCount, Is.EqualTo(2));
            Assert.That(d.Choices[1], Does.Contain("how do I play"));
            d.Advance();
            Assert.That(d.ChoiceCount, Is.EqualTo(2), "Advance doesn't skip a choice");

            d.Choose(1);
            Assert.That(d.ChoiceCount, Is.EqualTo(0));
            Assert.That(d.CurrentLine, Does.Contain("WASD"));
            d.Advance();
            d.Advance();
            Assert.That(d.CurrentLine, Does.Contain("pauses"));
            Assert.That(_tags, Does.Contain("wait"));
            Assert.That(d.IsTyping, Is.True, "#wait holds the line back");

            yield return FinishDialogue();
            Assert.That(_events, Does.Contain("intro_done"), "#event tag");
            Assert.That(_ended, Is.EqualTo(new[] { "intro" }), "Ended names the knot");
            Assert.That(_tags, Does.Contain("portrait"));
            Assert.That(GameRoot.Flow.Current, Is.EqualTo(GameState.Playing));
            Assert.That(GameRoot.Save.Data.HasFlag("met_guide"), Is.True);

            // Again, taking the other choice: the flag changes the first line, and #flash flashes.
            Assert.That(d.Play(Object.FindAnyObjectByType<SandboxGame>().story, "intro"), Is.True);
            Assert.That(GameRoot.Flow.Current, Is.EqualTo(GameState.Dialogue));
            Assert.That(d.CurrentLine, Does.Contain("back"));
            for (int i = 0; i < 20 && d.ChoiceCount == 0; i++) d.AutoStep();
            d.Choose(0);
            Assert.That(d.CurrentLine, Does.Contain("Go get them"));
            Assert.That(GameRoot.Ui.IsFlashing, Is.True, "#flash");
            yield return FinishDialogue();

            // The sign knot hides both portraits.
            Assert.That(d.Play(Object.FindAnyObjectByType<SandboxGame>().story, "sign"), Is.True);
            Assert.That(d.Speaker, Is.EqualTo("Sign"));
            Assert.That(view.LeftCharacter, Is.EqualTo(""));
            Assert.That(view.RightCharacter, Is.EqualTo(""));
            yield return FinishDialogue();
            Assert.That(_ended[_ended.Count - 1], Is.EqualTo("sign"));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator UnknownKnotFailsCleanly()
        {
            yield return StartSandbox();
            var story = Object.FindAnyObjectByType<SandboxGame>().story;
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("no knot \"nope\""));
            Assert.That(GameRoot.Dialogue.Play(story, "nope"), Is.False);
            Assert.That(GameRoot.Dialogue.IsActive, Is.False);
            Assert.That(GameRoot.Flow.Current, Is.EqualTo(GameState.Playing));
        }
    }

    public class SettingsTests
    {
        [UnitySetUp]
        public IEnumerator SetUp() => BootToTitle();

        [UnityTest]
        public IEnumerator SettingsScreenChangesApplyAndPersist()
        {
            var ui = GameRoot.Ui;
            ui.Title.Activate("settings");
            yield return null;
            Assert.That(ui.IsOverlayOpen(ui.Settings), Is.True);
            Assert.That(ui.Title.IsVisible, Is.False, "the overlay replaces the title while open");

            var root = ui.Settings.Root;
            root.Q<Slider>("music").value = 20f;
            root.Q<Slider>("master").value = 50f;
            root.Q<Toggle>("invert-y").value = true;
            Assert.That(GameRoot.Settings.musicVolume, Is.EqualTo(0.2f).Within(0.001f));
            Assert.That(AudioListener.volume, Is.EqualTo(0.5f).Within(0.001f), "master volume applies at once");
            Assert.That(GameRoot.Settings.invertY, Is.True);

            Assert.That(GameRoot.Settings.difficulty, Is.EqualTo(Difficulty.Normal));
            ui.Settings.Activate("difficulty");
            Assert.That(GameRoot.Settings.difficulty, Is.EqualTo(Difficulty.Hard));
            Assert.That(GameRoot.Settings.damageTakenScale, Is.EqualTo(1.5f));
            Assert.That(root.Q<Label>("difficulty-info").text, Is.EqualTo(DifficultyText.Describe(Difficulty.Hard)));
            Assert.That(root.Q<Button>("difficulty").text, Does.Contain("Hard"));

            ui.Settings.Activate("back");
            yield return null;
            Assert.That(ui.IsOverlayOpen(ui.Settings), Is.False);
            Assert.That(ui.Title.IsVisible, Is.True);

            var reloaded = new SaveService(GameRoot.Save.Slot);
            reloaded.Load();
            Assert.That(reloaded.Data.settings.musicVolume, Is.EqualTo(0.2f).Within(0.001f), "saved on close");
            Assert.That(reloaded.Data.settings.difficulty, Is.EqualTo(Difficulty.Hard));

            // How to Play from the title pages through and closes with Back.
            ui.Title.Activate("howto");
            Assert.That(ui.HowToPlay.Page, Is.EqualTo(0));
            ui.HowToPlay.Activate("next");
            Assert.That(ui.HowToPlay.Page, Is.EqualTo(1));
            ui.HowToPlay.Activate("prev");
            Assert.That(ui.HowToPlay.Page, Is.EqualTo(0));
            ui.Back();
            Assert.That(ui.IsOverlayOpen(ui.HowToPlay), Is.False);
            Assert.That(GameRoot.Flow.Current, Is.EqualTo(GameState.Title), "Back from How to Play doesn't start the game");
            LogAssert.NoUnexpectedReceived();
        }
    }

    /// <summary>Play's run-start difficulty picker: Normal preselected, a choice is the Settings
    /// difficulty (assists applied, saved, remembered next time), Back returns to the title, and the
    /// GameConfig flag (or a game without action settings) skips it.</summary>
    public class DifficultyPickerTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return BootToTitle();
            GameRoot.Save.Data.seenHowToPlay = true; // straight from the picker into the run
        }

        [UnityTest]
        public IEnumerator ChoosingHardSetsTheDifficultyAndItsAssists()
        {
            var ui = GameRoot.Ui;
            Assert.That(GameRoot.Config.AsksDifficultyOnPlay, Is.True);
            ui.Title.Activate("play");
            yield return null;
            Assert.That(ui.IsOverlayOpen(ui.DifficultyPicker), Is.True, "Play opens the picker");
            Assert.That(GameRoot.Flow.Current, Is.EqualTo(GameState.Title), "nothing started yet");
            Assert.That(ui.DifficultyPicker.Preselected, Is.EqualTo(Difficulty.Normal), "Normal the first time");
            var root = ui.DifficultyPicker.Root;
            foreach (var d in DifficultyText.All)
            {
                var card = root.Q<Button>(DifficultyScreen.ButtonName(d));
                Assert.That(card, Is.Not.Null, d + " card");
                Assert.That(card.Q<Label>(className: "jk-difficulty-desc").text, Is.EqualTo(DifficultyText.Describe(d)), "the shared one-line description");
            }

            ui.DifficultyPicker.Activate(DifficultyScreen.ButtonName(Difficulty.Hard));
            yield return null;
            Assert.That(ui.IsOverlayOpen(ui.DifficultyPicker), Is.False);
            var s = GameRoot.Settings;
            Assert.That(s.difficulty, Is.EqualTo(Difficulty.Hard), "the picker writes the Settings difficulty");
            Assert.That(s.extraLives, Is.EqualTo(0), "Hard's assists applied");
            Assert.That(s.enemyBulletSpeedScale, Is.EqualTo(1.2f).Within(0.001f));
            Assert.That(s.damageTakenScale, Is.EqualTo(1.5f).Within(0.001f));
            var reloaded = new SaveService(GameRoot.Save.Slot);
            reloaded.Load();
            Assert.That(reloaded.Data.settings.difficulty, Is.EqualTo(Difficulty.Hard), "saved at once");
            yield return WaitFor(() => GameRoot.Flow.InRun && !GameRoot.IsLoadingScene, "the run");
            yield return FinishDialogue();

            // Back on the title, Play remembers Hard, and Settings shows the same value.
            GameRoot.Instance.QuitToTitle();
            yield return WaitFor(() => GameRoot.Flow.Current == GameState.Title && !GameRoot.IsLoadingScene, "the title");
            ui.Title.Activate("play");
            yield return null;
            Assert.That(ui.DifficultyPicker.Preselected, Is.EqualTo(Difficulty.Hard), "last choice remembered");
            var badge = root.Q<Button>(DifficultyScreen.ButtonName(Difficulty.Hard)).Q<Label>(className: "jk-difficulty-badge");
            Assert.That(badge.text, Is.EqualTo("Last played"));
            ui.Back();
            yield return null;
            Assert.That(ui.IsOverlayOpen(ui.DifficultyPicker), Is.False, "Back closes the picker");
            Assert.That(GameRoot.Flow.Current, Is.EqualTo(GameState.Title), "and goes back to the title");
            Assert.That(ui.Title.IsVisible, Is.True);
            ui.Title.Activate("settings");
            yield return null;
            Assert.That(ui.Settings.Root.Q<Button>("difficulty").text, Does.Contain("Hard"), "Settings has the same value");
            ui.Back();
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator BackButtonReturnsToTheTitle()
        {
            var ui = GameRoot.Ui;
            ui.Title.Activate("play");
            yield return null;
            Assert.That(ui.DifficultyPicker.Activate("back"), Is.True);
            yield return null;
            Assert.That(ui.IsOverlayOpen(ui.DifficultyPicker), Is.False);
            Assert.That(GameRoot.Flow.Current, Is.EqualTo(GameState.Title));
            Assert.That(GameRoot.Settings.difficulty, Is.EqualTo(Difficulty.Normal), "nothing changed");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator TheSkipFlagStartsTheRunAtOnce()
        {
            var ui = GameRoot.Ui;
            GameRoot.Config.chooseDifficultyOnPlay = false; // an in-memory copy (ShellTestUtil.UseSandboxConfig)
            ui.Title.Activate("play");
            yield return null;
            Assert.That(ui.IsOverlayOpen(ui.DifficultyPicker), Is.False, "no picker");
            yield return WaitFor(() => GameRoot.Flow.InRun && !GameRoot.IsLoadingScene, "the run");
            Assert.That(GameRoot.Settings.difficulty, Is.EqualTo(Difficulty.Normal), "the Settings difficulty is used as it is");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator AGameWithoutActionSettingsNeverAsks()
        {
            var ui = GameRoot.Ui;
            GameRoot.Config.showActionSettings = false; // a pure story game: no difficulty anywhere
            Assert.That(GameRoot.Config.AsksDifficultyOnPlay, Is.False);
            ui.Title.Activate("play");
            yield return null;
            Assert.That(ui.IsOverlayOpen(ui.DifficultyPicker), Is.False);
            yield return WaitFor(() => GameRoot.Flow.InRun && !GameRoot.IsLoadingScene, "the run");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator TheEndScreenShowsTheRunsQuote()
        {
            yield return StartSandbox();
            GameRoot.Run.SetQuote("Guide", "Again? Fine.");
            GameRoot.Lose();
            yield return null;
            Assert.That(GameRoot.Ui.GameOver.QuoteText, Does.Contain("Guide").And.Contain("Again? Fine."));
            GameRoot.Ui.GameOver.Activate("retry");
            yield return WaitFor(() => GameRoot.Dialogue.IsActive && !GameRoot.IsLoadingScene, "the retried run's dialogue");
            Assert.That(GameRoot.Run.quote, Is.Empty, "a new run starts without a quote");
            yield return FinishDialogue();
            GameRoot.Lose();
            yield return null;
            Assert.That(GameRoot.Ui.GameOver.QuoteText, Is.Empty, "no quote: nothing shown");
            LogAssert.NoUnexpectedReceived();
        }
    }

    /// <summary>SaveService on its own slots; no shell set-up needed.</summary>
    public class SaveServiceTests
    {
        [Test]
        public void SaveLoadRoundTrip()
        {
            var save = new SaveService("playmode-roundtrip");
            save.Delete();
            var s = save.Data.settings;
            s.masterVolume = 0.3f;
            s.musicVolume = 0.25f;
            s.sfxVolume = 0.9f;
            s.mouseSensitivity = 2.5f;
            s.invertY = true;
            s.screenShake = 0f;
            s.fullscreen = false;
            s.SetDifficulty(Difficulty.Story);
            save.Data.SetFlag("door_open");
            save.Data.seenHowToPlay = true;
            save.Save();

            var again = new SaveService("playmode-roundtrip");
            again.Load();
            var l = again.Data.settings;
            Assert.That(l.masterVolume, Is.EqualTo(0.3f));
            Assert.That(l.musicVolume, Is.EqualTo(0.25f));
            Assert.That(l.sfxVolume, Is.EqualTo(0.9f));
            Assert.That(l.mouseSensitivity, Is.EqualTo(2.5f));
            Assert.That(l.invertY, Is.True);
            Assert.That(l.screenShake, Is.EqualTo(0f));
            Assert.That(l.fullscreen, Is.False);
            Assert.That(l.difficulty, Is.EqualTo(Difficulty.Story));
            Assert.That(l.damageTakenScale, Is.EqualTo(0.5f));
            Assert.That(again.Data.HasFlag("door_open"), Is.True);
            Assert.That(again.Data.seenHowToPlay, Is.True);
            Assert.That(again.Data.version, Is.EqualTo(SaveData.CurrentVersion));
            again.Delete();
        }

        [Test]
        public void DamagedSaveStartsFresh()
        {
            var save = new SaveService("playmode-damaged");
            save.Delete();
            System.IO.File.WriteAllText(save.FilePath, "{ this is not json");
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("damaged"));
            save.Load();
            Assert.That(save.Data.settings.masterVolume, Is.EqualTo(new Settings().masterVolume));
            save.Delete();
        }
    }
}
