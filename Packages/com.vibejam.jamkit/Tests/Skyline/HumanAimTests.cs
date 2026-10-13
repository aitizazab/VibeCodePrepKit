using System;
using JamKit.Skyline;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    /// <summary>The arena demo's hand on the mouse (fix round "the demo plays like a human"): no instant flicks, a new target
    /// acquired in roughly 0.15-0.3 s with a slight overshoot that settles, a capped turn rate, and a little lag on a target
    /// that moves.</summary>
    public class HumanAimTests
    {
        const float Frame = 1f / 60f;

        /// <summary>Seconds until the view is within <paramref name="tol"/> degrees of the target and stays there (to 1.5 s), and
        /// the largest overshoot past it on the way (degrees).</summary>
        static float Acquire(HumanAim a, float yaw, float pitch, float tol, out float overshoot, out float firstFrameError, out float topRate)
        {
            float startYaw = a.Yaw, sign = Math.Sign(HumanAim.DeltaAngle(startYaw, yaw));
            float settled = -1f;
            overshoot = 0f;
            topRate = 0f;
            firstFrameError = float.NaN;
            for (int i = 1; i <= 90; i++)
            {
                a.Step(Frame, yaw, pitch);
                if (i == 1) firstFrameError = a.ErrorTo(yaw, pitch);
                topRate = Math.Max(topRate, a.Rate);
                overshoot = Math.Max(overshoot, -sign * HumanAim.DeltaAngle(a.Yaw, yaw));
                float err = a.ErrorTo(yaw, pitch);
                if (err <= tol) { if (settled < 0f) settled = i * Frame; }
                else settled = -1f;
            }
            return settled;
        }

        [Test]
        public void ASixtyDegreeSwitchTakesAHumanBeatOvershootsALittleAndSettles()
        {
            var a = new HumanAim();
            a.Snap(0f, 0f);
            a.React(0.08f);
            float t = Acquire(a, 60f, 5f, 2f, out float over, out float first, out float top);
            TestContext.WriteLine($"60 degrees: on target at {t:0.000} s, overshoot {over:0.0} degrees, error after one frame {first:0.0}, top rate {top:0} deg/s");
            Assert.That(t, Is.InRange(0.15f, 0.4f), "acquired in roughly 0.15-0.3 s (plus the reaction beat)");
            Assert.That(first, Is.GreaterThan(55f), "no instant flick: one frame in, it has barely moved");
            Assert.That(over, Is.InRange(0.5f, 6f), "a slight overshoot, then it settles");
            Assert.That(top, Is.LessThanOrEqualTo(a.maxRate + 0.01f), "the turn rate is capped");
        }

        [TestCase(15f)]
        [TestCase(30f)]
        [TestCase(120f)]
        public void EverySwitchIsATurnNotAJump(float degrees)
        {
            var a = new HumanAim();
            a.Snap(10f, 0f);
            a.React(0.06f);
            float t = Acquire(a, 10f + degrees, 0f, 2f, out float over, out float first, out float top);
            TestContext.WriteLine($"{degrees} degrees: on target at {t:0.000} s, overshoot {over:0.0}, top rate {top:0} deg/s");
            Assert.That(t, Is.InRange(0.1f, 0.45f));
            Assert.That(first, Is.GreaterThan(degrees * 0.85f), "one frame in it has turned less than 15% of the way");
            Assert.That(top, Is.LessThanOrEqualTo(a.maxRate + 0.01f));
        }

        [Test]
        public void AMovingTargetIsFollowedALittleBehind()
        {
            var a = new HumanAim();
            a.Snap(0f, 0f);
            float want = 0f, lagSum = 0f;
            int n = 0;
            for (int i = 0; i < 180; i++)
            {
                want += 30f * Frame;   // a drone crossing at 30 degrees a second
                a.Step(Frame, want, 0f);
                if (i >= 60) { lagSum += HumanAim.DeltaAngle(a.Yaw, want); n++; }
            }
            float lag = lagSum / n;
            TestContext.WriteLine($"lag behind a 30 deg/s crossing: {lag:0.00} degrees");
            Assert.That(lag, Is.InRange(0.5f, 4f), "a little tracking lag, not perfect and not lost");
        }

        [Test]
        public void ItTurnsTheShortWayRoundAndRepeats()
        {
            var a = new HumanAim();
            var b = new HumanAim();
            a.Snap(350f, 0f);
            b.Snap(350f, 0f);
            for (int i = 0; i < 60; i++)
            {
                a.Step(Frame * (1f + (i % 3) * 0.1f), 10f, -20f);
                b.Step(Frame * (1f + (i % 3) * 0.1f), 10f, -20f);
                Assert.That(a.Yaw, Is.EqualTo(b.Yaw), "deterministic");
                Assert.That(a.Pitch, Is.EqualTo(b.Pitch));
                float d = HumanAim.DeltaAngle(350f, a.Yaw);
                Assert.That(d, Is.InRange(-1f, 25f), "it turns through 0, not the long way round");
            }
            Assert.That(a.ErrorTo(10f, -20f), Is.LessThan(1f));
        }
    }
}
