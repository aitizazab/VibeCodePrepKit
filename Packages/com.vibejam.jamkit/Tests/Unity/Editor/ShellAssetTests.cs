using System.Linq;
using Ink.Runtime;
using Ink.UnityIntegration;
using JamKit.Shell;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace JamKit.Tests.Editor
{
    /// <summary>The generated shell is complete: config, UI and input assets, scenes in Build
    /// Settings, the sample story compiles and runs. Rerun "JamKit/Rebuild Shell Scenes" if these fail.</summary>
    public class ShellAssetTests
    {
        [Test]
        public void ConfigAndShellAssetsAreInResources()
        {
            var config = Resources.Load<GameConfig>(GameConfig.ResourcePath);
            Assert.That(config, Is.Not.Null, "GameConfig");
            Assert.That(config.gameTitle, Is.Not.Empty);
            Assert.That(config.credits, Is.Not.Null, "credits text");
            Assert.That(config.audio, Is.Not.Null, "audio registry");
            Assert.That(config.howToPlay.Count, Is.GreaterThan(0));

            var a = Resources.Load<ShellAssets>(ShellAssets.ResourcePath);
            Assert.That(a, Is.Not.Null, "ShellAssets");
            Assert.That(a.panelSettings, Is.Not.Null);
            Assert.That(a.panelSettings.themeStyleSheet, Is.Not.Null, "theme");
            Assert.That(a.panelSettings.referenceResolution, Is.EqualTo(new Vector2Int(1920, 1080)));
            Assert.That(a.styleSheets.Count, Is.EqualTo(2));
            Assert.That(a.styleSheets.All(s => s != null), Is.True);
            Assert.That(a.input, Is.Not.Null);
            foreach (var tree in new[] { a.title, a.settings, a.pause, a.gameOver, a.victory, a.credits, a.howToPlay, a.hud, a.dialogue, a.debugMenu })
                Assert.That(tree, Is.Not.Null);
        }

        [Test]
        public void BuildSettingsStartWithTheShellScenes()
        {
            var config = Resources.Load<GameConfig>(GameConfig.ResourcePath);
            var names = EditorBuildSettings.scenes.Where(s => s.enabled)
                .Select(s => System.IO.Path.GetFileNameWithoutExtension(s.path)).ToList();
            Assert.That(names.Take(2), Is.EqualTo(new[] { "Boot", "Title" }));
            Assert.That(names, Does.Contain("Sandbox"));
            Assert.That(names, Does.Contain(config.bootScene));
            Assert.That(names, Does.Contain(config.titleScene));
            Assert.That(names, Does.Contain(config.firstGameplayScene));
            Assert.That(names, Does.Not.Contain("SampleScene"));
        }

        [Test]
        public void InputAssetHasEveryAction()
        {
            var a = Resources.Load<ShellAssets>(ShellAssets.ResourcePath);
            foreach (var name in new[] { "Gameplay/Move", "Gameplay/Look", "Gameplay/Fire", "Gameplay/AltFire", "Gameplay/Sprint",
                         "Gameplay/Dash", "Gameplay/Interact", "Gameplay/Pause", "UI/Navigate", "UI/Submit", "UI/Cancel",
                         "UI/Point", "UI/Click", "UI/ScrollWheel" })
            {
                var action = a.input.FindAction(name);
                Assert.That(action, Is.Not.Null, name);
                Assert.That(action.bindings.Count, Is.GreaterThan(0), name + " has bindings");
            }
            // Shift / LB is Sprint ("Shift = sprint" is what players expect); Focus stays, without keys.
            var sprint = a.input.FindAction("Gameplay/Sprint");
            Assert.That(sprint.bindings.Any(b => b.path == "<Keyboard>/leftShift"), Is.True, "Sprint on Left Shift");
            Assert.That(sprint.bindings.Any(b => b.path == "<Gamepad>/leftShoulder"), Is.True, "Sprint on LB");
            var focus = a.input.FindAction("Gameplay/Focus");
            Assert.That(focus, Is.Not.Null, "Focus still exists (bind a key to bring it back)");
            Assert.That(focus.bindings.Count, Is.EqualTo(0), "Focus has no default keys");
        }

        [Test]
        public void SampleStoryCompilesAndRuns()
        {
            var file = AssetDatabase.LoadAssetAtPath<InkFile>("Assets/JamKit/Dialogue/Sandbox.ink");
            Assert.That(file, Is.Not.Null);
            Assert.That(file.isCompiled, Is.True, "compiled (no ink errors)");
            var story = new Story(file.storyJson);
            story.BindExternalFunction("has_flag", (string n) => (object)false, true);
            story.BindExternalFunction("set_flag", (string n) => { });
            story.BindExternalFunction("clear_flag", (string n) => { });
            var tags = new System.Collections.Generic.List<string>();
            // A line holding only tags comes back empty; the shell skips it but keeps its tags.
            Assert.That(NextLine(story, tags), Does.Contain("Welcome"));
            Assert.That(tags, Does.Contain("speaker:Guide"));
            Assert.That(tags, Does.Contain("portrait:Guide happy left"));
            story.ChoosePathString("sign");
            tags.Clear();
            Assert.That(NextLine(story, tags), Does.Contain("sign"));
            Assert.That(tags, Does.Contain("speaker:Sign"));
        }

        static string NextLine(Story story, System.Collections.Generic.List<string> tags)
        {
            while (story.canContinue)
            {
                string line = story.Continue().Trim();
                tags.AddRange(story.currentTags);
                if (line.Length > 0) return line;
            }
            return "";
        }

        [Test]
        public void EveryRegisteredSoundHasAClip()
        {
            var config = Resources.Load<GameConfig>(GameConfig.ResourcePath);
            foreach (var e in config.audio.entries)
            {
                var clip = e.clip != null ? e.clip : ProceduralAudio.Get(e.id);
                Assert.That(clip, Is.Not.Null, e.id);
                Assert.That(clip.samples, Is.GreaterThan(0), e.id);
            }
        }

        [Test]
        public void PortraitsHaveTheirSprites()
        {
            var config = Resources.Load<GameConfig>(GameConfig.ResourcePath);
            Assert.That(config.FindPortrait("guide"), Is.Not.Null, "lookup ignores case");
            foreach (var p in config.portraits)
            {
                Assert.That(p.baseSprite, Is.Not.Null, p.characterId);
                Assert.That(p.FindExpression(""), Is.Not.Null, p.characterId + " default expression");
            }
        }
    }
}
