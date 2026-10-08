using System;
using NUnit.Framework;

namespace JamKit.Combat.Tests
{
    public class ScreenShakeTests
    {
        [Test]
        public void TraumaClampsToZeroOne()
        {
            var s = new ScreenShake();
            s.AddTrauma(0.7f);
            s.AddTrauma(0.7f);
            Assert.That(s.Trauma, Is.EqualTo(1f));
            s.AddTrauma(-5f);
            Assert.That(s.Trauma, Is.EqualTo(0f));
        }

        [Test]
        public void TraumaDecaysLinearlyPerSecondAndStopsAtZero()
        {
            var s = new ScreenShake { decayPerSecond = 2f };
            s.AddTrauma(1f);
            s.Tick(0.25f);
            Assert.That(s.Trauma, Is.EqualTo(0.5f).Within(1e-6));
            Assert.That(s.Intensity, Is.EqualTo(0.25f).Within(1e-6));
            s.Tick(1f);
            Assert.That(s.Trauma, Is.EqualTo(0f));
        }

        [Test]
        public void NoTraumaMeansNoShake()
        {
            var s = new ScreenShake();
            s.Tick(0.37f);
            var sample = s.Sample();
            Assert.That(sample.offset.X, Is.EqualTo(0f));
            Assert.That(sample.offset.Y, Is.EqualTo(0f));
            Assert.That(sample.roll, Is.EqualTo(0f));
        }

        [Test]
        public void ShakeScalesWithTraumaSquared()
        {
            var full = new ScreenShake { decayPerSecond = 0f, seed = 9 };
            var half = new ScreenShake { decayPerSecond = 0f, seed = 9 };
            full.AddTrauma(1f);
            half.AddTrauma(0.5f);
            for (int i = 0; i < 20; i++)
            {
                full.Tick(0.013f);
                half.Tick(0.013f);
                var a = full.Sample();
                var b = half.Sample();
                Assert.That(b.offset.X, Is.EqualTo(a.offset.X * 0.25f).Within(1e-6));
                Assert.That(b.offset.Y, Is.EqualTo(a.offset.Y * 0.25f).Within(1e-6));
                Assert.That(b.roll, Is.EqualTo(a.roll * 0.25f).Within(1e-6));
            }
        }

        [Test]
        public void ShakeStaysWithinTheMaxValues()
        {
            var s = new ScreenShake { maxOffset = 0.5f, maxRoll = 5f, decayPerSecond = 0f };
            s.AddTrauma(1f);
            float peak = 0f;
            for (int i = 0; i < 2000; i++)
            {
                s.Tick(1f / 60f);
                var sample = s.Sample();
                Assert.That(Math.Abs(sample.offset.X), Is.LessThanOrEqualTo(0.5f));
                Assert.That(Math.Abs(sample.offset.Y), Is.LessThanOrEqualTo(0.5f));
                Assert.That(Math.Abs(sample.roll), Is.LessThanOrEqualTo(5f));
                peak = Math.Max(peak, Math.Abs(sample.offset.X));
            }
            Assert.That(peak, Is.GreaterThan(0.3f), "full trauma actually reaches near the max");
        }

        [Test]
        public void ShakeIsSmoothNotJittery()
        {
            var s = new ScreenShake { maxOffset = 1f, frequency = 15f, decayPerSecond = 0f };
            s.AddTrauma(1f);
            const float dt = 1f / 240f;
            s.Tick(dt);
            var prev = s.Sample();
            float worstStep = 0f;
            for (int i = 0; i < 2400; i++)
            {
                s.Tick(dt);
                var cur = s.Sample();
                worstStep = Math.Max(worstStep, Math.Abs(cur.offset.X - prev.offset.X));
                prev = cur;
            }
            // Smoothstep value noise moves at most 1.5 * 2 per unit; 15 Hz at 240 fps is 1/16 unit per frame.
            Assert.That(worstStep, Is.LessThanOrEqualTo(3f / 16f + 1e-4f));
        }

        [Test]
        public void SameSeedRepeatsAndDifferentSeedsDiffer()
        {
            var a = new ScreenShake { seed = 42, decayPerSecond = 0f };
            var b = new ScreenShake { seed = 42, decayPerSecond = 0f };
            var c = new ScreenShake { seed = 43, decayPerSecond = 0f };
            a.AddTrauma(1f); b.AddTrauma(1f); c.AddTrauma(1f);
            bool differs = false;
            for (int i = 0; i < 30; i++)
            {
                a.Tick(0.02f); b.Tick(0.02f); c.Tick(0.02f);
                Assert.That(b.Sample().offset, Is.EqualTo(a.Sample().offset));
                if (Math.Abs(c.Sample().offset.X - a.Sample().offset.X) > 1e-3f) differs = true;
            }
            Assert.That(differs, Is.True);
        }

        [Test]
        public void ValueNoiseIsBoundedContinuousAndHitsItsLatticeValues()
        {
            for (int i = -50; i < 50; i++)
            {
                Assert.That(ValueNoise.Sample(i, 5u), Is.EqualTo(ValueNoise.Lattice(i, 5u)).Within(1e-6));
                Assert.That(ValueNoise.Sample(i - 1e-7, 5u), Is.EqualTo(ValueNoise.Sample(i + 1e-7, 5u)).Within(1e-5));
                for (double f = 0; f < 1; f += 0.1)
                {
                    float v = ValueNoise.Sample(i + f, 5u);
                    Assert.That(v, Is.InRange(-1f, 1f));
                }
            }
            Assert.That(ValueNoise.Lattice(3, 1u), Is.Not.EqualTo(ValueNoise.Lattice(4, 1u)));
        }
    }
}
