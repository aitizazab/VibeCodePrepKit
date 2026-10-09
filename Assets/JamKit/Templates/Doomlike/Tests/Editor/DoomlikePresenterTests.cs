using System.Collections.Generic;
using System.IO;
using JamKit.Presenting;
using JamKit.Doomlike.EditorTools;
using NUnit.Framework;
using UnityEditor;

namespace JamKit.Doomlike.Tests
{
    /// <summary>Presenter mode for the DOOM-like: every declared chapter is registered (in the panel's order),
    /// the attract script only jumps to those chapters and drives segments the game plays, and the
    /// chapter scene is the template's scene. See docs/PRESENTING.md.</summary>
    public class DoomlikePresenterTests
    {
        static List<string> Registered()
        {
            var r = new ChapterRegistry();
            var owner = new object();
            DoomPresenter.Register(r, null, owner);
            var idx = new List<int>();
            r.InGroup(DoomPresenter.Scene, idx);
            var names = new List<string>();
            foreach (int i in idx) names.Add(r.NameAt(i));
            return names;
        }

        [Test]
        public void EveryDeclaredChapterIsRegisteredInOrder()
        {
            var names = Registered();
            Assert.That(names, Is.EqualTo(DoomPresenter.Chapters), "the game registers exactly the declared chapters, in the declared order");
            Assert.That(names.Count, Is.GreaterThanOrEqualTo(5));
            Assert.That(new HashSet<string>(names).Count, Is.EqualTo(names.Count), "chapter names are unique");
        }

        [Test]
        public void TheAttractScriptPlaysOnlyRealChaptersAndSegments()
        {
            var script = DoomPresenter.Attract();
            Assert.That(script.scene, Is.EqualTo(DoomPresenter.Scene));
            var problems = script.Validate(Registered(), DoomPresenter.Segments);
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
            Assert.That(script.TotalSeconds, Is.InRange(30f, 180f), "a loop long enough for b-roll, short enough to see everything");
        }

        [Test]
        public void TheChapterSceneIsTheTemplateScene()
        {
            string path = "Assets/JamKit/Templates/Doomlike/Scenes/" + DoomPresenter.Scene + ".unity";
            Assert.That(File.Exists(path), Is.True, path);
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(path), Is.Not.Null, path);
        }

        [Test]
        public void TheGameHasTheLevelsTheChaptersName()
        {
            var c = AssetDatabase.LoadAssetAtPath<DoomlikeConfig>(DoomlikeContent.ConfigPath);
            Assert.That(c, Is.Not.Null);
            Assert.That(c.LevelCount, Is.GreaterThanOrEqualTo(3), "chapters go up to Level 3");
        }
    }
}
