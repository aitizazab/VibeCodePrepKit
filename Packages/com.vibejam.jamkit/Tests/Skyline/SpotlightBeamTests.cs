using System;
using System.Numerics;
using JamKit.Skyline;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    /// <summary>The spotlight (docs/SKYLINE-DESIGN.md 2.5): dodged by changing direction in the last 0.25 s, not by
    /// running straight.</summary>
    public class SpotlightBeamTests
    {
        static bool RunShot(Func<float, Vector3> targetAt, float h = 1f / 120f)
        {
            var s = new TelegraphedShot();
            float t = 0f;
            s.Start(targetAt(0f));
            while (true)
            {
                t += h;
                if (s.Step(h, targetAt(t), out bool hit)) return hit;
                if (t > 5f) Assert.Fail("the shot never fired");
            }
        }

        static Vector3 Run(float x) => new Vector3(x, 0f, 20f);

        [Test]
        public void ASteadyRunIsCaught()
        {
            Assert.That(RunShot(t => Run(7f * t)), Is.True);
        }

        [Test]
        public void TurningAfterTheLockDodges()
        {
            Assert.That(RunShot(t => Run(t < 0.6f ? 7f * t : 4.2f - 7f * (t - 0.6f))), Is.False);
        }

        [Test]
        public void TurningLongBeforeTheLockIsStillCaught()
        {
            Assert.That(RunShot(t => Run(t < 0.2f ? 7f * t : 1.4f - 7f * (t - 0.2f))), Is.True);
        }

        [Test]
        public void TheBeamHoldsAt20Fps()
        {
            Assert.That(RunShot(t => Run(7f * t), 1f / 20f), Is.True);
            Assert.That(RunShot(t => Run(t < 0.6f ? 7f * t : 4.2f - 7f * (t - 0.6f)), 1f / 20f), Is.False);
        }

        [Test]
        public void ABrokenStepChangesNothing()
        {
            var s = new TelegraphedShot();
            s.Start(Vector3.Zero);
            Assert.That(s.Step(float.NaN, Vector3.Zero, out _), Is.False);
            Assert.That(s.Locked, Is.False);
            Assert.That(float.IsNaN(s.AimPoint.X), Is.False);
        }
    }
}
