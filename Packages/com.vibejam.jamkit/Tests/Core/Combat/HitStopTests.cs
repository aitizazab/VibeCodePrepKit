using NUnit.Framework;

namespace JamKit.Combat.Tests
{
    public class HitStopTests
    {
        [Test]
        public void SingleStopLastsItsRealTimeDuration()
        {
            var hs = new HitStop();
            Assert.That(hs.CurrentTimeScale, Is.EqualTo(1f));
            Assert.That(hs.Request(0.1f, 0f), Is.True);
            Assert.That(hs.CurrentTimeScale, Is.EqualTo(0f), "applies at once, before the next Tick");
            hs.Tick(0.05f);
            Assert.That(hs.CurrentTimeScale, Is.EqualTo(0f));
            hs.Tick(0.06f);
            Assert.That(hs.CurrentTimeScale, Is.EqualTo(1f));
            Assert.That(hs.IsStopped, Is.False);
        }

        [Test]
        public void OverlappingStopsResolveToTheSlowestStillRunning()
        {
            var hs = new HitStop();
            hs.Request(0.2f, 0.5f);
            hs.Request(0.05f, 0.1f);
            hs.Request(0.1f, 0.3f);
            Assert.That(hs.CurrentTimeScale, Is.EqualTo(0.1f));
            hs.Tick(0.06f);
            Assert.That(hs.CurrentTimeScale, Is.EqualTo(0.3f));
            hs.Tick(0.05f);
            Assert.That(hs.CurrentTimeScale, Is.EqualTo(0.5f));
            hs.Tick(0.1f);
            Assert.That(hs.CurrentTimeScale, Is.EqualTo(1f));
        }

        [Test]
        public void RequestsAreClampedOrIgnoredWhenMeaningless()
        {
            var hs = new HitStop();
            Assert.That(hs.Request(0f, 0f), Is.False);
            Assert.That(hs.Request(-1f, 0f), Is.False);
            Assert.That(hs.IsStopped, Is.False);
            hs.Request(0.1f, -2f);
            Assert.That(hs.CurrentTimeScale, Is.EqualTo(0f));
            hs.Clear();
            hs.Request(0.1f, 3f);
            Assert.That(hs.CurrentTimeScale, Is.EqualTo(1f));
        }

        [Test]
        public void CapCutsALongStopShortThenRefusesRequestsDuringRecovery()
        {
            var hs = new HitStop { maxConsecutiveStop = 0.25f, recoveryTime = 0.1f };
            hs.Request(5f, 0f);
            hs.Tick(0.2f);
            Assert.That(hs.CurrentTimeScale, Is.EqualTo(0f));
            hs.Tick(0.06f);
            Assert.That(hs.CurrentTimeScale, Is.EqualTo(1f));
            Assert.That(hs.IsRecovering, Is.True);
            Assert.That(hs.Request(0.1f, 0f), Is.False);
            hs.Tick(0.1f);
            Assert.That(hs.IsRecovering, Is.False);
            Assert.That(hs.Request(0.1f, 0f), Is.True);
        }

        [Test]
        public void AStopEndingExactlyAtTheCapDoesNotTriggerRecovery()
        {
            var hs = new HitStop { maxConsecutiveStop = 0.25f };
            hs.Request(0.25f, 0f);
            hs.Tick(0.25f);
            Assert.That(hs.IsRecovering, Is.False);
            Assert.That(hs.Request(0.1f, 0f), Is.True);
        }

        [Test]
        public void SpammedRequestsCannotFreezeTheGame()
        {
            var hs = new HitStop();
            const float dt = 1f / 60f;
            float longestStop = 0f, stretch = 0f, normalTime = 0f;
            for (int frame = 0; frame < 120; frame++)
            {
                hs.Tick(dt);
                if (hs.CurrentTimeScale < 1f) stretch += dt;
                else { stretch = 0f; normalTime += dt; }
                if (stretch > longestStop) longestStop = stretch;
                hs.Request(0.08f, 0f); // every frame, like a minigun on a crowd
            }
            Assert.That(longestStop, Is.LessThanOrEqualTo(hs.maxConsecutiveStop + 1e-4f));
            Assert.That(normalTime, Is.GreaterThan(0.4f), "time keeps running between capped stops");
        }

        [Test]
        public void ClearEndsStopsAndRecovery()
        {
            var hs = new HitStop();
            hs.Request(1f, 0f);
            hs.Tick(0.3f);
            Assert.That(hs.IsRecovering, Is.True);
            hs.Clear();
            Assert.That(hs.IsRecovering, Is.False);
            Assert.That(hs.Request(0.1f, 0.2f), Is.True);
            Assert.That(hs.CurrentTimeScale, Is.EqualTo(0.2f));
        }
    }
}
