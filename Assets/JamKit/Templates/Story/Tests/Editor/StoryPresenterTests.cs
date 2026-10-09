using System.Collections.Generic;
using System.IO;
using JamKit.Presenting;
using JamKit.Narrative.EditorTools;
using JamKit.Shell;
using NUnit.Framework;
using UnityEditor;

namespace JamKit.Narrative.Tests
{
    /// <summary>Presenter mode for the story template: every declared chapter is registered (in the panel's order),
    /// the attract script only jumps to those chapters and drives segments the game plays, and the
    /// chapter scene is the template's scene. See docs/PRESENTING.md.</summary>
    public class StoryPresenterTests
    {
        static List<string> Registered()
        {
            var r = new ChapterRegistry();
            var owner = new object();
            StoryPresenter.Register(r, null, owner);
            var idx = new List<int>();
            r.InGroup(StoryPresenter.Scene, idx);
            var names = new List<string>();
            foreach (int i in idx) names.Add(r.NameAt(i));
            return names;
        }

        [Test]
        public void EveryDeclaredChapterIsRegisteredInOrder()
        {
            var names = Registered();
            Assert.That(names, Is.EqualTo(StoryPresenter.Chapters), "the game registers exactly the declared chapters, in the declared order");
            Assert.That(names.Count, Is.GreaterThanOrEqualTo(6));
            Assert.That(new HashSet<string>(names).Count, Is.EqualTo(names.Count), "chapter names are unique");
        }

        [Test]
        public void TheAttractScriptPlaysOnlyRealChaptersAndSegments()
        {
            var script = StoryPresenter.Attract();
            Assert.That(script.scene, Is.EqualTo(StoryPresenter.Scene));
            var problems = script.Validate(Registered(), StoryPresenter.Segments);
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
            Assert.That(script.TotalSeconds, Is.InRange(30f, 180f), "a loop long enough for b-roll, short enough to see everything");
        }

        [Test]
        public void TheChapterSceneIsTheTemplateScene()
        {
            string path = "Assets/JamKit/Templates/Story/Scenes/" + StoryPresenter.Scene + ".unity";
            Assert.That(File.Exists(path), Is.True, path);
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(path), Is.Not.Null, path);
        }

        [Test]
        public void EveryChapterStartsAtAKnotTheStoryHas()
        {
            var c = StoryInkCheck.LoadConfig();
            Assert.That(c, Is.Not.Null);
            Assert.That(c.story != null && c.story.isCompiled, Is.True, "the ink compiles");
            var story = new Ink.Runtime.Story(c.story.storyJson);
            foreach (var knot in StoryPresenter.Knots())
                Assert.That(DialogueService.PathExists(story, knot), Is.True, "no knot \"" + knot + "\" in the story");
        }
    }
}
