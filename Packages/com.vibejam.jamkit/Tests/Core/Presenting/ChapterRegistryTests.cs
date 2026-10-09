using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace JamKit.Presenting.Tests
{
    public class ChapterRegistryTests
    {
        [Test]
        public void RegisteredChaptersJumpInOrderOfRegistration()
        {
            var r = new ChapterRegistry();
            var log = new List<string>();
            r.Register("Game", "Floor 1", () => log.Add("f1"));
            r.Register("Game", "Floor 2", () => log.Add("f2"));
            Assert.That(r.Count, Is.EqualTo(2));
            Assert.That(r.NameAt(0), Is.EqualTo("Floor 1"));
            Assert.That(r.Jump("Game", "Floor 2"), Is.EqualTo(JumpResult.Jumped));
            Assert.That(log, Is.EqualTo(new[] { "f2" }));
            Assert.That(r.LastJumped, Is.EqualTo("Floor 2"));
        }

        [Test]
        public void UnknownNamesAndOtherGroupsDoNothing()
        {
            var r = new ChapterRegistry();
            bool ran = false;
            r.Register("A", "Boss", () => ran = true);
            Assert.That(r.Jump("A", "Nope"), Is.EqualTo(JumpResult.Unknown));
            Assert.That(r.Jump("B", "Boss"), Is.EqualTo(JumpResult.Unknown), "groups keep same-named chapters apart");
            Assert.That(ran, Is.False);
        }

        [Test]
        public void RegisteringTheSameNameReplacesTheJumpAndKeepsItsPlace()
        {
            var r = new ChapterRegistry();
            int which = 0;
            r.Register("G", "One", () => which = 1);
            r.Register("G", "Two", () => which = 2);
            r.Register("G", "One", () => which = 3);
            Assert.That(r.Count, Is.EqualTo(2));
            Assert.That(r.NameAt(0), Is.EqualTo("One"));
            r.Jump("G", "One");
            Assert.That(which, Is.EqualTo(3));
        }

        [Test]
        public void DeclaredChaptersAreListedBeforeTheyAreLiveAndJumpingThereIsPending()
        {
            var r = new ChapterRegistry();
            r.Declare("Game", new[] { "Intro", "Boss" });
            Assert.That(r.Count, Is.EqualTo(2));
            Assert.That(r.IsLive(0), Is.False);
            Assert.That(r.Jump("Game", "Boss"), Is.EqualTo(JumpResult.Pending));
            Assert.That(r.HasPending, Is.True);
            Assert.That(r.PendingName, Is.EqualTo("Boss"));
            Assert.That(r.PendingReady, Is.False);
            Assert.That(r.RunPending(), Is.EqualTo(JumpResult.Pending), "still waiting for the scene");

            bool ran = false;
            r.Register("Game", "Intro", () => { });
            r.Register("Game", "Boss", () => ran = true);
            Assert.That(ran, Is.False, "registering never runs the pending jump by itself (the template may still be starting)");
            Assert.That(r.PendingReady, Is.True);
            Assert.That(r.RunPending(), Is.EqualTo(JumpResult.Jumped));
            Assert.That(ran, Is.True);
            Assert.That(r.HasPending, Is.False);
            Assert.That(r.RunPending(), Is.EqualTo(JumpResult.Unknown), "nothing pending any more");
        }

        [Test]
        public void DeclaringAgainKeepsOrderAndLiveJumps()
        {
            var r = new ChapterRegistry();
            r.Declare("G", new[] { "A", "B" });
            r.Register("G", "B", () => { });
            r.Declare("G", new[] { "A", "B", "C" });
            Assert.That(r.Count, Is.EqualTo(3));
            Assert.That(r.NameAt(2), Is.EqualTo("C"));
            Assert.That(r.IsLive("G", "B"), Is.True);
        }

        [Test]
        public void UnregisteringAnOwnerKeepsTheNamesButNotTheJumps()
        {
            var r = new ChapterRegistry();
            var sceneA = new object();
            var global = new object();
            r.Register("G", "One", () => { }, sceneA);
            r.Register("G", "Two", () => { }, sceneA);
            r.Register("G", "Opening", () => { }, global);
            Assert.That(r.Unregister(sceneA), Is.EqualTo(2));
            Assert.That(r.Count, Is.EqualTo(3), "names stay listed");
            Assert.That(r.IsLive("G", "One"), Is.False);
            Assert.That(r.IsLive("G", "Opening"), Is.True);
            Assert.That(r.Unregister(sceneA), Is.EqualTo(0));
            Assert.That(r.Unregister(null), Is.EqualTo(0));
        }

        [Test]
        public void ANewSceneRegisteringBeforeTheOldOneUnregistersKeepsTheNewJumps()
        {
            var r = new ChapterRegistry();
            var oldScene = new object();
            var newScene = new object();
            int which = 0;
            r.Register("G", "One", () => which = 1, oldScene);
            r.Register("G", "One", () => which = 2, newScene);
            r.Unregister(oldScene);
            Assert.That(r.Jump("G", "One"), Is.EqualTo(JumpResult.Jumped));
            Assert.That(which, Is.EqualTo(2));
        }

        [Test]
        public void GroupsListTheirOwnChaptersInOrder()
        {
            var r = new ChapterRegistry();
            r.Declare("A", new[] { "a1", "a2" });
            r.Declare("B", new[] { "b1" });
            r.Register("A", "a3", () => { });
            var idx = new List<int>();
            Assert.That(r.InGroup("A", idx), Is.EqualTo(3));
            Assert.That(r.NameAt(idx[2]), Is.EqualTo("a3"));
            Assert.That(r.InGroup("B", idx), Is.EqualTo(1));
            Assert.That(r.InGroup("C", idx), Is.EqualTo(0));
            Assert.That(r.FirstIn("A"), Is.EqualTo("a1"));
            Assert.That(r.FirstIn("C"), Is.EqualTo(""));
        }

        [Test]
        public void NextAfterStaysInTheGroupAndStopsAtTheEnd()
        {
            var r = new ChapterRegistry();
            r.Declare("A", new[] { "a1" });
            r.Declare("B", new[] { "b1" });
            r.Declare("A", new[] { "a2" });
            Assert.That(r.NextAfter("A", "a1"), Is.EqualTo("a2"), "skips the other group's chapter");
            Assert.That(r.NextAfter("A", "a2"), Is.EqualTo(""));
            Assert.That(r.NextAfter("A", "zz"), Is.EqualTo(""));
        }

        [Test]
        public void AThrowingJumpReportsFailedWithTheMessage()
        {
            var r = new ChapterRegistry();
            r.Register("G", "Bad", () => throw new InvalidOperationException("boom"));
            Assert.That(r.Jump("G", "Bad"), Is.EqualTo(JumpResult.Failed));
            Assert.That(r.LastError, Is.EqualTo("boom"));
        }

        [Test]
        public void RegisterRejectsMissingNameOrJump()
        {
            var r = new ChapterRegistry();
            Assert.Throws<ArgumentException>(() => r.Register("G", "", () => { }));
            Assert.Throws<ArgumentNullException>(() => r.Register("G", "X", null));
        }

        [Test]
        public void ClearForgetsEverythingIncludingThePendingJump()
        {
            var r = new ChapterRegistry();
            r.Declare("G", new[] { "A" });
            r.Jump("G", "A");
            r.Clear();
            Assert.That(r.Count, Is.EqualTo(0));
            Assert.That(r.HasPending, Is.False);
        }

        [Test]
        public void SetPendingMarksAKnownChapterWithoutRunningIt()
        {
            var r = new ChapterRegistry();
            var owner = new object();
            bool ran = false;
            r.Register("G", "Live", () => ran = true, owner);
            Assert.That(r.SetPending("G", "Live"), Is.True);
            Assert.That(ran, Is.False);
            Assert.That(r.PendingReady, Is.True);
            Assert.That(r.OwnerAt(r.IndexOf("G", "Live")), Is.SameAs(owner));
            Assert.That(r.SetPending("G", "Nope"), Is.False);
            Assert.That(r.PendingName, Is.EqualTo("Live"), "an unknown name leaves the pending one alone");
        }

        [Test]
        public void JumpingToALiveChapterCancelsAnOlderPendingOne()
        {
            var r = new ChapterRegistry();
            r.Declare("G", new[] { "Later" });
            r.Register("G", "Now", () => { });
            r.Jump("G", "Later");
            Assert.That(r.HasPending, Is.True);
            r.Jump("G", "Now");
            Assert.That(r.HasPending, Is.False);
            r.Jump("G", "Later");
            r.CancelPending();
            Assert.That(r.HasPending, Is.False);
        }
    }
}
