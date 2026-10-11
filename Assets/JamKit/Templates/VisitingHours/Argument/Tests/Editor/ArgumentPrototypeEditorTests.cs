using System.IO;
using System.Linq;
using JamKit.Argument.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace JamKit.Argument.Tests
{
    /// <summary>The argument prototype's screen builds without the shell, shows what a judge needs (the claim, the
    /// placeholder badge, each step as a sentence), and its scene is built and in Build Settings.</summary>
    public class ArgumentPrototypeEditorTests
    {
        [Test]
        public void TheScreenBuildsAndShowsTheBattle()
        {
            ArgumentContent.Ensure();
            var parent = new VisualElement();
            var view = new ArgumentView(parent);
            Assert.That(parent.Q<VisualElement>(ArgumentView.RootName), Is.Not.Null);

            var engine = new ArgumentEngine(DreamLenaDemo.Build(), new ArgumentSetup { difficulty = ArgumentDifficulty.Story, allEvidence = true });
            engine.Start();
            engine.SkipPressure();
            engine.Concede("L1");
            view.SetHeader(engine.Def.opponent, "softened", engine.Def.claim, engine.Def.placeholderLines);
            view.SetTranscript(engine.Transcript);
            view.SetVitals(engine.HeartRate, engine.HeartStep, engine.Heat, engine.Anger);
            view.SetPremises(new[] { "CLAIM", "L1", "?" }, new[] { engine.Def.claim, "Out there, Theo is dead.", "" },
                new[] { "", "conceded", "hidden" }, new[] { true, true, false }, 1);
            view.SetTray(new[] { "the fourth chair", "Retort" }, 0);
            view.SetReasons("Because...", new[] { "a", "b", "c" }, 2);
            view.SetVerb("ask", "Ask", true);

            var labels = parent.Query<Label>().ToList().Select(l => l.text).ToList();
            Assert.That(labels.Any(t => t.Contains(engine.Def.claim)), Is.True, "the claim");
            Assert.That(labels.Any(t => t.Contains("PLACEHOLDER")), Is.True, "placeholder lines are marked on screen");
            Assert.That(labels.Any(t => t.Contains("Nadia conceded")), Is.True, "steps written as sentences");
            Assert.That(labels.Any(t => t.Contains("70 bpm")), Is.True, "heart rate");
            Assert.That(parent.Q<Button>("argument-premise-2"), Is.Not.Null);
            Assert.That(parent.Q<Button>("argument-evidence-1"), Is.Not.Null);
            Assert.That(parent.Q<Button>("argument-reason-2"), Is.Not.Null);
            Assert.That(parent.Q<Button>("argument-verb-press"), Is.Not.Null);
            Assert.That(parent.Query<Button>().ToList().All(b => !b.focusable), Is.True, "keys drive the selection, not UI focus");

            view.ShowEnd("Convinced", "Lena is amenable.");
            Assert.That(view.EndShown, Is.True);
            Assert.That(parent.Q<Button>("argument-continue"), Is.Not.Null);
            view.Remove();
            Assert.That(parent.childCount, Is.EqualTo(0));
        }

        [Test]
        public void ThePrototypeSceneIsBuiltAndInBuildSettings()
        {
            Assert.That(File.Exists(ArgumentPrototypeBuilder.ScenePath), Is.True, "run JamKit > Rebuild Shell Scenes");
            Assert.That(EditorBuildSettings.scenes.Any(s => s.enabled && s.path == ArgumentPrototypeBuilder.ScenePath), Is.True);
            string text = File.ReadAllText(ArgumentPrototypeBuilder.ScenePath);
            Assert.That(text, Does.Contain("Argument prototype"), "the ArgumentPrototype object");
            Assert.That(text, Does.Contain("Pancake"), "the kitchen table");
        }

        [Test]
        public void TheChaptersAreNamedForEverySetting()
        {
            Assert.That(ArgumentPrototype.Chapters, Has.Length.EqualTo(5));
            Assert.That(ArgumentPrototype.SetupFor(ArgumentPrototype.LongNight).difficulty, Is.EqualTo(ArgumentDifficulty.ScriptedButBeatable));
            Assert.That(ArgumentPrototype.SetupFor(ArgumentPrototype.Story).difficulty, Is.EqualTo(ArgumentDifficulty.Story));
            var noWalt = ArgumentPrototype.SetupFor(ArgumentPrototype.NoWalt);
            Assert.That(noWalt.allEvidence, Is.False);
            Assert.That(noWalt.evidence, Does.Not.Contain(DreamLenaDemo.TruthWalt));
        }
    }
}
