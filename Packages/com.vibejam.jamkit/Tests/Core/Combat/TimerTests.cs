using NUnit.Framework;

namespace JamKit.Combat.Tests
{
    public class TimerTests
    {
        [Test]
        public void CooldownStartsReadyAndBlocksUntilElapsed()
        {
            var c = new Cooldown(0.5f);
            Assert.That(c.Ready, Is.True);
            Assert.That(c.Progress01, Is.EqualTo(1f));
            Assert.That(c.TryUse(), Is.True);
            Assert.That(c.TryUse(), Is.False);
            Assert.That(c.Progress01, Is.EqualTo(0f));
            c.Tick(0.25f);
            Assert.That(c.Progress01, Is.EqualTo(0.5f).Within(1e-6));
            Assert.That(c.Remaining, Is.EqualTo(0.25f).Within(1e-6));
            c.Tick(0.3f);
            Assert.That(c.Ready, Is.True);
            Assert.That(c.Remaining, Is.EqualTo(0f));
            Assert.That(c.TryUse(), Is.True);
        }

        [Test]
        public void CooldownResetMakesItReadyAndZeroDurationIsAlwaysReady()
        {
            var c = new Cooldown(10f);
            c.TryUse();
            c.Reset();
            Assert.That(c.Ready, Is.True);
            var instant = new Cooldown(0f);
            Assert.That(instant.TryUse() && instant.TryUse(), Is.True);
            Assert.That(instant.Progress01, Is.EqualTo(1f));
        }

        [Test]
        public void RepeatingTimerFiresSeveralTimesInALongFrameAndKeepsTheRemainder()
        {
            var t = new RepeatingTimer(0.1f);
            Assert.That(t.Tick(0.35f), Is.EqualTo(3));
            Assert.That(t.Elapsed, Is.EqualTo(0.05f).Within(1e-5));
            Assert.That(t.Progress01, Is.EqualTo(0.5f).Within(1e-4));
            Assert.That(t.Tick(0.04f), Is.EqualTo(0));
            Assert.That(t.Tick(0.01f), Is.EqualTo(1));
        }

        [TestCase(0.1f, 1f, 10)]
        [TestCase(0.25f, 2f, 8)]
        [TestCase(1f / 7f, 3f, 21)]
        public void RepeatingTimerIsFrameRateIndependent(float interval, float seconds, int expected)
        {
            foreach (int fps in new[] { 30, 60, 144, 240 })
            {
                var t = new RepeatingTimer(interval);
                int fired = 0;
                int frames = (int)System.Math.Round(seconds * fps);
                for (int i = 0; i < frames; i++) fired += t.Tick(1f / fps);
                Assert.That(fired, Is.EqualTo(expected), $"at {fps} fps");
            }
        }

        [Test]
        public void RepeatingTimerWithNoIntervalNeverFiresAndResetClearsProgress()
        {
            var never = new RepeatingTimer(0f);
            Assert.That(never.Tick(100f), Is.EqualTo(0));
            var t = new RepeatingTimer(1f);
            t.Tick(0.9f);
            t.Reset();
            Assert.That(t.Tick(0.9f), Is.EqualTo(0));
            Assert.That(t.Tick(0.1f), Is.EqualTo(1));
        }
    }
}
