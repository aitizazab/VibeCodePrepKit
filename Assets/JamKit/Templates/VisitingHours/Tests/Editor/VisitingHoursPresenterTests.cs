using System.Collections.Generic;
using System.IO;
using JamKit.Presenting;
using NUnit.Framework;
using UnityEditor;

namespace JamKit.VisitingHours.Tests
{
    /// <summary>Presenter mode for Visiting Hours: every declared chapter is registered (in the panel's order),
    /// the attract script only jumps to those chapters and drives segments the game plays, and the
    /// chapter scene is the template's scene. See docs/PRESENTING.md.</summary>
    public class VisitingHoursPresenterTests
    {
        static List<string> Registered()
        {
            var r = new ChapterRegistry();
            var owner = new object();
            VhPresenter.RegisterOpening(r);
            VhPresenter.Register(r, null, owner);
            var idx = new List<int>();
            r.InGroup(VhPresenter.Scene, idx);
            var names = new List<string>();
            foreach (int i in idx) names.Add(r.NameAt(i));
            return names;
        }

        [Test]
        public void EveryDeclaredChapterIsRegisteredInOrder()
        {
            var names = Registered();
            Assert.That(names, Is.EqualTo(VhPresenter.Chapters), "the game registers exactly the declared chapters, in the declared order");
            Assert.That(names.Count, Is.GreaterThanOrEqualTo(8));
            Assert.That(new HashSet<string>(names).Count, Is.EqualTo(names.Count), "chapter names are unique");
        }

        [Test]
        public void TheAttractScriptPlaysOnlyRealChaptersAndSegments()
        {
            var script = VhPresenter.Attract();
            Assert.That(script.scene, Is.EqualTo(VhPresenter.Scene));
            var problems = script.Validate(Registered(), VhPresenter.Segments);
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
            Assert.That(script.TotalSeconds, Is.InRange(30f, 180f), "a loop long enough for b-roll, short enough to see everything");
        }

        [Test]
        public void TheChapterSceneIsTheTemplateScene()
        {
            string path = "Assets/JamKit/Templates/VisitingHours/Scenes/" + VhPresenter.Scene + ".unity";
            Assert.That(File.Exists(path), Is.True, path);
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(path), Is.Not.Null, path);
        }

        [Test]
        public void TheOpeningWorksFromAnyStateAndTheRestNeedTheSlice()
        {
            var r = new ChapterRegistry();
            var owner = new object();
            VhPresenter.RegisterOpening(r);
            VhPresenter.Register(r, null, owner);
            Assert.That(r.OwnerAt(r.IndexOf(VhPresenter.Scene, VhPresenter.Opening)), Is.SameAs(JamKit.Shell.PresenterMode.AnyTime));
            Assert.That(r.Unregister(owner), Is.EqualTo(VhPresenter.Chapters.Length - VhPresenter.AnyTimeChapters.Length), "leaving the slice takes its jumps away, not the opening's (or its editor, or the chain's)");
            Assert.That(r.IsLive(VhPresenter.Scene, VhPresenter.ChainNight2), Is.True);
            Assert.That(r.IsLive(VhPresenter.Scene, VhPresenter.EditOpening), Is.True);
            Assert.That(r.IsLive(VhPresenter.Scene, VhPresenter.Opening), Is.True);
        }
    }
}
