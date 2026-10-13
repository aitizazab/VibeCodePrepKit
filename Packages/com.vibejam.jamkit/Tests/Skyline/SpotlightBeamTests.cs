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

        [Test]
        public void AHugeStepKeepsTheBeamFinite()
        {
            foreach (float dt in new[] { 10f, 1000f, 1e30f })
            {
                var s = new TelegraphedShot();
                s.Start(Vector3.Zero);
                s.Step(dt, new Vector3(3f, 0f, 3f), out _);
                Assert.That(float.IsNaN(s.AimPoint.X) || float.IsInfinity(s.AimPoint.X), Is.False, "dt " + dt);
                Assert.That(float.IsNaN(s.AimPoint.Z) || float.IsInfinity(s.AimPoint.Z), Is.False, "dt " + dt);
            }
        }

        [Test]
        public void ANonFiniteTargetIsIgnoredAndTheShotStillResolves()
        {
            var s = new TelegraphedShot();
            s.Start(Run(0f));
            float t = 0f;
            bool fired = false, hit = true;
            for (int i = 0; i < 240 && !fired; i++)
            {
                t += 1f / 60f;
                Vector3 target = i == 20 ? new Vector3(float.NaN, 0f, float.PositiveInfinity) : Run(7f * t);
                fired = s.Step(1f / 60f, target, out hit);
                Assert.That(float.IsNaN(s.AimPoint.X + s.AimPoint.Y + s.AimPoint.Z), Is.False);
                Assert.That(float.IsInfinity(s.AimPoint.X + s.AimPoint.Y + s.AimPoint.Z), Is.False);
            }
            Assert.That(fired, Is.True);
            Assert.That(hit, Is.True, "one bad frame doesn't spoil a steady run's catch");
        }

        [Test]
        public void ATeleportDoesNotThrowTheBeamPastItsSpeedLimit()
        {
            var s = new TelegraphedShot();
            s.Start(Run(0f));
            s.Step(1f / 60f, Run(0.1f), out _);
            s.Step(1f / 60f, Run(50f), out _);
            Assert.That(s.AimVelocity.Length(), Is.LessThanOrEqualTo(TelegraphedShot.MaxBeamSpeed + 1e-3f));
            s.Step(1f / 60f, Run(50f), out _);
            Assert.That(s.AimVelocity.Length(), Is.LessThanOrEqualTo(TelegraphedShot.MaxBeamSpeed + 1e-3f));
        }
    }
}
