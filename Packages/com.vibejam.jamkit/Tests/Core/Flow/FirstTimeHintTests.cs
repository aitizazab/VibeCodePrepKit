using JamKit.Flow;
using NUnit.Framework;

namespace JamKit.Tests
{
    public class FirstTimeHintTests
    {
        static FirstTimeHint Hint() => new FirstTimeHint { ShowSeconds = 8f, MinSeconds = 1f, FadeSeconds = 0.5f };

        [Test]
        public void ShowsOnceThenFadesAfterItsTime()
        {
            var h = Hint();
            Assert.That(h.State, Is.EqualTo(HintState.Waiting));
            Assert.That(h.Alpha, Is.EqualTo(0f));
            Assert.That(h.Start(false), Is.True);
            Assert.That(h.IsVisible, Is.True);
            h.Tick(0.5f, false);
            Assert.That(h.Alpha, Is.EqualTo(1f), "faded in");
            bool finished = false;
            for (int i = 0; i < 100 && !finished; i++) finished = h.Tick(0.1f, false);
            Assert.That(finished, Is.True, "it times out");
            Assert.That(h.State, Is.EqualTo(HintState.Fading));
            Assert.That(h.WasUsed, Is.False);
            h.Tick(0.25f, false);
            Assert.That(h.Alpha, Is.EqualTo(0.5f).Within(0.01f), "fading out");
            h.Tick(0.3f, false);
            Assert.That(h.State, Is.EqualTo(HintState.Done));
            Assert.That(h.IsVisible, Is.False);
            Assert.That(h.Start(false), Is.False, "never twice from the same object");
        }

        [Test]
        public void UsingItEndsItAfterTheMinimumTime()
        {
            var h = Hint();
            h.Start(false);
            Assert.That(h.Tick(0.2f, true), Is.False, "used at once: still shown for the minimum");
            Assert.That(h.Tick(0.5f, false), Is.False);
            Assert.That(h.Tick(0.4f, false), Is.True, "minimum reached, and it was used: done");
            Assert.That(h.WasUsed, Is.True);
        }

        [Test]
        public void AlreadySeenNeverShows()
        {
            var h = Hint();
            Assert.That(h.Start(true), Is.False);
            Assert.That(h.State, Is.EqualTo(HintState.Done));
            Assert.That(h.Tick(10f, true), Is.False, "no 'finished' tick for a hint that never showed");
            Assert.That(h.Alpha, Is.EqualTo(0f));
        }

        [Test]
        public void ZeroAndBadTimeStepsDoNothing()
        {
            var h = Hint();
            h.Start(false);
            h.Tick(float.NaN, false);
            h.Tick(-3f, false);
            h.Tick(0f, false);
            Assert.That(h.State, Is.EqualTo(HintState.Showing));
            Assert.That(h.Alpha, Is.EqualTo(0f), "no time passed: not faded in yet");
            h.Reset();
            Assert.That(h.State, Is.EqualTo(HintState.Waiting));
        }

        [Test]
        public void SaveDataRemembersAHintAsAFlag()
        {
            var save = new SaveData();
            var h = Hint();
            h.Start(save.HasFlag("hint_focus"));
            while (!h.Tick(0.5f, false)) { }
            save.SetFlag("hint_focus");
            var next = Hint();
            Assert.That(next.Start(save.HasFlag("hint_focus")), Is.False, "second run: no hint");
        }
    }
}
