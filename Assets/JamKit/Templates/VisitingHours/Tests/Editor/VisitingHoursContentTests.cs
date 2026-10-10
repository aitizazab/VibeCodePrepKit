using System.IO;
using System.Linq;
using JamKit.Shell;
using JamKit.VisitingHours.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace JamKit.VisitingHours.Tests
{
    /// <summary>The slice's built content: the config validates, every picture and voice is
    /// referenced (real or a stand-in), the portraits are in the active GameConfig, the preset and the
    /// scene are in place, the title's boot asset loads, and every knot the game plays is in the ink.</summary>
    public class VisitingHoursContentTests
    {
        static VisitingHoursConfig Config()
        {
            var c = AssetDatabase.LoadAssetAtPath<VisitingHoursConfig>(VisitingHoursContent.ConfigPath);
            Assert.That(c, Is.Not.Null, "Run JamKit > Rebuild Shell Scenes");
            return c;
        }

        [Test]
        public void TheConfigValidates()
        {
            var problems = VisitingHoursValidation.Problems(Config());
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [Test]
        public void EveryPictureIsReferencedRealOrStandIn()
        {
            var c = Config();
            int stand = 0;
            foreach (var (name, _) in VisitingHoursArt.All)
            {
                Assert.That(c.Art(name), Is.Not.Null, name);
                if (VisitingHoursArt.IsFallback(c.Art(name))) stand++;
                bool real = File.Exists(VisitingHoursArt.ArtDir + "/" + name + ".png");
                Assert.That(VisitingHoursArt.IsFallback(c.Art(name)), Is.EqualTo(!real), name + (real ? " exists but a stand-in is used" : " is missing but no stand-in is used"));
            }
            TestContext.WriteLine("Visiting Hours art: " + (VisitingHoursArt.All.Length - stand) + " real, " + stand + " stand-ins");
        }

        [Test]
        public void ThePaintingsAreSmoothNotPixelated()
        {
            foreach (var (name, kind) in VisitingHoursArt.All)
            {
                var path = AssetDatabase.GetAssetPath(Config().Art(name));
                var imp = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.That(imp.filterMode, Is.EqualTo(FilterMode.Bilinear), name);
                if (kind == VisitingHoursArt.Kind.Tile) Assert.That(imp.wrapMode, Is.EqualTo(TextureWrapMode.Repeat), name);
                else if (kind == VisitingHoursArt.Kind.Sky) Assert.That(imp.wrapModeU, Is.EqualTo(TextureWrapMode.Repeat), name);
                else Assert.That(imp.alphaIsTransparency, Is.True, name + " keeps its cut-out");
            }
        }

        [Test]
        public void WaltAndMagsHavePortraitsInTheSlicesOwnConfig()
        {
            // The slice carries its own portraits (registered with the dialogue when it starts),
            // so it works whatever the active GameConfig holds.
            var c = Config();
            var walt = c.FindPortrait("Walt");
            var mags = c.FindPortrait("Mags");
            Assert.That(walt, Is.Not.Null);
            Assert.That(mags, Is.Not.Null);
            foreach (var e in new[] { "neutral", "sad", "firm" }) Assert.That(walt.FindExpression(e), Is.Not.Null, "Walt " + e);
            Assert.That(mags.FindExpression(""), Is.Not.Null, "Mags's default expression");
            Assert.That(walt.baseSprite, Is.Not.Null, "a blank base (the expressions are whole paintings)");
        }

        [Test]
        public void ThePresetAndTheSceneAreInPlace()
        {
            var preset = AssetDatabase.LoadAssetAtPath<GameConfig>(VisitingHoursBuilder.PresetPath);
            Assert.That(preset, Is.Not.Null);
            Assert.That(preset.firstGameplayScene, Is.EqualTo(VisitingHoursBuilder.SceneName));
            Assert.That(preset.showPixelLookSetting, Is.False, "the pixel look stays off: the paintings must read");
            Assert.That(preset.titleMusic, Is.EqualTo("music_vh_sad_pad"));
            Assert.That(File.Exists(VisitingHoursBuilder.ScenePath), Is.True);
            Assert.That(EditorBuildSettings.scenes.Any(s => s.path == VisitingHoursBuilder.ScenePath), Is.True, "the slice's scene is in Build Settings");
        }

        [Test]
        public void TheTitleFindsItsBootAsset()
        {
            var boot = VisitingHoursBoot.Load();
            Assert.That(boot, Is.Not.Null);
            Assert.That(boot.sceneName, Is.EqualTo(VisitingHoursBuilder.SceneName));
            Assert.That(boot.cues.Count, Is.GreaterThan(20), "the opening's cues");
            Assert.That(boot.Track("voicemail"), Is.Not.Null);
            Assert.That(boot.Track("news"), Is.Not.Null);
            Assert.That(boot.style, Is.Not.Null);
            Assert.That(boot.wakingMaterial, Is.Not.Null);
            TestContext.WriteLine("Voices: voicemail " + (boot.voicemail != null) + ", news " + (boot.newsAnchor != null) + "; Nadia's model " + (boot.nadiaModel != null));
        }

        [Test]
        public void EveryKnotTheGamePlaysIsInTheInk()
        {
            var c = Config();
            Assert.That(c.story, Is.Not.Null);
            Assert.That(c.story.isCompiled, Is.True, "the ink compiles");
            var story = new Ink.Runtime.Story(c.story.storyJson);
            foreach (var knot in SliceContent.AllKnots()) Assert.That(DialogueService.PathExists(story, knot), Is.True, knot);
            foreach (var e in c.examinables) Assert.That(DialogueService.PathExists(story, e.knot), Is.True, e.id + ": " + e.knot);
        }
    }
}
