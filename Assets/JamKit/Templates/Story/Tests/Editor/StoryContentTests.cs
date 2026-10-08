using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ink.UnityIntegration;
using JamKit.Narrative.EditorTools;
using JamKit.Shell;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace JamKit.Narrative.Tests
{
    /// <summary>The story template's content is complete and valid: the ink compiles, every tag and id
    /// resolves against the real data, the check catches a deliberately broken copy of the sample,
    /// every id has its art, the office hotspots are well formed and the scene, preset and Build
    /// Settings entry exist. If these fail after editing, read the messages: they name the file,
    /// line and problem. Run "JamKit/Rebuild Shell Scenes" if assets are missing.</summary>
    public class StoryContentTests
    {
        static StoryConfig Config()
        {
            var c = StoryInkCheck.LoadConfig();
            Assert.That(c, Is.Not.Null, "StoryConfig at " + StoryContent.ConfigPath);
            return c;
        }

        [Test]
        public void InkCompilesAndIncludesAreParts()
        {
            var c = Config();
            Assert.That(c.story, Is.Not.Null, "the config has an ink story");
            Assert.That(c.story.isCompiled, Is.True, "the main ink file compiles: " + string.Join("\n", c.story.errors.Select(e => e.content)));
            Assert.That(c.story.errors, Is.Empty);
            foreach (var name in new[] { "StoryKit.ink", "LastTrain_Office.ink", "LastTrain_Endings.ink" })
            {
                var inc = AssetDatabase.LoadAssetAtPath<InkFile>(StoryBuilder.Root + "/Dialogue/" + name);
                Assert.That(inc, Is.Not.Null, name);
                Assert.That(inc.isMaster, Is.False, name + " is INCLUDEd, not a story of its own");
            }
        }

        [Test]
        public void StoryCheckPasses()
        {
            var problems = StoryInkCheck.Check(Config());
            foreach (var w in problems.Warnings()) TestContext.Out.WriteLine(w);
            Assert.That(problems.Errors(), Is.Empty, problems.ToString());
        }

        [Test]
        public void CheckCatchesABrokenCopyOfTheSample()
        {
            var c = Config();
            var files = StoryInkCheck.InkFiles(c);
            int main = files.FindIndex(f => f.Key == "LastTrain.ink");
            Assert.That(main, Is.GreaterThanOrEqualTo(0));
            string broken = files[main].Value
                .Replace("#scene:platform_night fade 2", "#scnee:platform_night fade 2")
                .Replace("#show:iris neutral right fade", "#show:iris grumpy right fade")
                .Replace("#music:music_story_tense 2", "#music:music_story_tenze 2")
                .Replace("#cg:cg_train flash 0.4", "#cg:cg_trian flash 0.4");
            files[main] = new KeyValuePair<string, string>("LastTrain.ink", broken);
            var problems = StoryValidation.Check(c, AssetDatabase.LoadAssetAtPath<AudioRegistry>("Assets/JamKit/Audio/AudioRegistry.asset"), files);
            string all = problems.ToString();
            TestContext.Out.WriteLine(all);
            Assert.That(all, Does.Contain("unknown tag #scnee (did you mean #scene?)"));
            Assert.That(all, Does.Contain("iris has no expression \"grumpy\""));
            Assert.That(all, Does.Contain("no sound with id \"music_story_tenze\""));
            Assert.That(all, Does.Contain("no CG with id \"cg_trian\" (did you mean \"cg_train\"?)"));
            Assert.That(all, Does.Contain("LastTrain.ink:"), "problems name the file and line");
        }

        [Test]
        public void EveryIdHasItsArt()
        {
            var c = Config();
            foreach (var b in c.backgrounds)
                Assert.That(b.sprite != null || b.id == "black" || b.id == "white", Is.True, "background " + b.id + " has a picture");
            foreach (var g in c.cgs) Assert.That(g.sprite, Is.Not.Null, "CG " + g.id);
            foreach (var i in c.items)
            {
                Assert.That(i.icon, Is.Not.Null, "item " + i.id);
                Assert.That(i.displayName, Is.Not.Empty, "item " + i.id);
            }
            foreach (var ch in c.characters)
            {
                Assert.That(ch.portrait, Is.Not.Null, "character " + ch.id);
                Assert.That(ch.portrait.baseSprite, Is.Not.Null, "character " + ch.id + " base");
                Assert.That(ch.portrait.expressions.Count, Is.GreaterThanOrEqualTo(3), "character " + ch.id + " expressions");
                foreach (var e in ch.portrait.expressions) Assert.That(e.sprite, Is.Not.Null, ch.id + " " + e.name);
                Assert.That(ch.portrait.FindExpression(ch.portrait.defaultExpression), Is.Not.Null, ch.id + " default expression");
            }
            Assert.That(c.endings.Count, Is.GreaterThanOrEqualTo(3));
            Assert.That(c.endings.Count(e => e.secret), Is.EqualTo(1), "one secret ending");
            Assert.That(c.style, Is.Not.Null, "Story.uss");
            Assert.That(c.knotIndex, Does.Contain("platform"), "the debug knot list is filled");
        }

        [Test]
        public void OfficeHotspotsAreWellFormed()
        {
            var c = Config();
            var office = c.FindHotspotScene("office");
            Assert.That(office, Is.Not.Null);
            Assert.That(office.hotspots.Count, Is.GreaterThanOrEqualTo(5));
            foreach (var h in office.hotspots) Assert.That(HotspotScene.ShapeOf(h).Problem(), Is.Null, h.id);
            // The drawer sits inside the desk: a click on it picks the drawer (the smaller shape).
            var shapes = office.hotspots.Select(HotspotScene.ShapeOf).ToList();
            var drawer = office.Find("desk_drawer");
            int pick = HotspotPicker.Pick(shapes, null, drawer.rect.center.x, drawer.rect.center.y);
            Assert.That(office.hotspots[pick].id, Is.EqualTo("desk_drawer"));
            Assert.That(office.walkthrough, Is.Not.Empty);
            Assert.That(office.KnotForItem(drawer, "brass_key"), Is.EqualTo("office_drawer_key"));
            Assert.That(office.KnotForItem(drawer, "photograph"), Is.EqualTo(office.fallbackItemKnot));
        }

        [Test]
        public void SceneAndPresetAreReady()
        {
            Assert.That(File.Exists(StoryBuilder.ScenePath), Is.True, StoryBuilder.ScenePath);
            Assert.That(EditorBuildSettings.scenes.Any(s => s.path == StoryBuilder.ScenePath), Is.True, "the Story scene is in Build Settings");
            var preset = AssetDatabase.LoadAssetAtPath<GameConfig>(StoryBuilder.PresetPath);
            Assert.That(preset, Is.Not.Null);
            Assert.That(preset.firstGameplayScene, Is.EqualTo(StoryBuilder.SceneName));
            Assert.That(preset.showTextSettings, Is.True);
            Assert.That(preset.chooseDifficultyOnPlay, Is.False, "no run-start difficulty picker: the story has its own text settings");
            Assert.That(preset.AsksDifficultyOnPlay, Is.False);
            Assert.That(preset.howToPlay.Count, Is.InRange(2, 4));
            Assert.That(Resources.Load<StoryConfig>(StoryConfig.ResourcePath), Is.Not.Null, "the config loads from Resources");
        }
    }
}
