using System.Collections.Generic;
using JamKit.Skyline;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    /// <summary>Task S0: the strippable wreck's outline strength over its 8 s strip window (docs/SKYLINE-STYLE.md rule 2).</summary>
    public class SkylineWreckOutlineTests
    {
        const float Window = 8f;

        [TestCase(8f)]
        [TestCase(7.9f)]
        [TestCase(5f)]
        [TestCase(3.01f)]
        [TestCase(3f)]
        public void ItIsFullUntilTheWindowsLastThreeSeconds(float left) =>
            Assert.That(SkylineWreckOutline.Strength(left, Window), Is.EqualTo(1f).Within(1e-5f));

        [Test]
        public void AFreshWreckBeyondTheWindowIsFull() =>
            Assert.That(SkylineWreckOutline.Strength(12f, Window), Is.EqualTo(1f));

        [Test]
        public void ItThinsMonotonicallyToFortyPercentAtPointFourSeconds()
        {
            float last = 1f;
            for (float left = 3f; left >= 0.4001f; left -= 0.05f)
            {
                float s = SkylineWreckOutline.Strength(left, Window);
                Assert.That(s, Is.LessThanOrEqualTo(last + 1e-6f), "left " + left);
                Assert.That(s, Is.GreaterThanOrEqualTo(0.4f - 1e-5f), "left " + left);
                last = s;
            }
            Assert.That(SkylineWreckOutline.Strength(0.4001f, Window), Is.EqualTo(0.4f).Within(0.01f));
            Assert.That(SkylineWreckOutline.Strength(1.7f, Window), Is.EqualTo(0.4f + 0.6f * (1.7f - 0.4f) / 2.6f).Within(1e-4f), "linear between");
        }

        [Test]
        public void ItBlinksOutInTheLastFourTenthsOfASecond_ThenIsGone()
        {
            var seen = new HashSet<float>();
            var changes = 0;
            float prev = -1f;
            for (float left = 0.4f; left > 0.0005f; left -= 0.005f)
            {
                float s = SkylineWreckOutline.Strength(left, Window);
                seen.Add(s);
                if (prev >= 0f && s != prev) changes++;
                prev = s;
            }
            Assert.That(seen, Is.EquivalentTo(new[] { 0.4f, 0f }), "only the minimum width and off");
            Assert.That(changes, Is.GreaterThanOrEqualTo(4), "on, off, on, off, on, off: at least four flips");
            Assert.That(SkylineWreckOutline.Strength(0.001f, Window), Is.EqualTo(0f), "the last slice is off");
            Assert.That(SkylineWreckOutline.Strength(0.3999f, Window), Is.EqualTo(0.4f), "it starts on, where the thinning ended");
        }

        [TestCase(0f)]
        [TestCase(-0.1f)]
        [TestCase(-100f)]
        public void ItIsZeroAfterTheWindow(float left) =>
            Assert.That(SkylineWreckOutline.Strength(left, Window), Is.EqualTo(0f));

        [Test]
        public void NaNAndUnusableWindowsGiveZero()
        {
            Assert.That(SkylineWreckOutline.Strength(float.NaN, Window), Is.EqualTo(0f));
            Assert.That(SkylineWreckOutline.Strength(5f, float.NaN), Is.EqualTo(0f));
            Assert.That(SkylineWreckOutline.Strength(5f, 0f), Is.EqualTo(0f));
            Assert.That(SkylineWreckOutline.Strength(5f, -3f), Is.EqualTo(0f));
        }

        [Test]
        public void AShortWindowStillThinsBeforeItFlashes()
        {
            Assert.That(SkylineWreckOutline.Strength(0.9f, 1f), Is.InRange(0.4f, 1f));
            Assert.That(SkylineWreckOutline.Strength(1f, 1f), Is.EqualTo(1f));
            Assert.That(SkylineWreckOutline.Strength(0.2f, 0.3f), Is.EqualTo(0.4f).Or.EqualTo(0f), "a window inside the flash only blinks");
        }

        [Test]
        public void StepsRunFromOffToFullAndRoundTrip()
        {
            Assert.That(SkylineWreckOutline.Step(0f), Is.EqualTo(0));
            Assert.That(SkylineWreckOutline.Step(float.NaN), Is.EqualTo(0));
            Assert.That(SkylineWreckOutline.Step(1f), Is.EqualTo(SkylineWreckOutline.Steps));
            Assert.That(SkylineWreckOutline.Step(0.4f), Is.EqualTo(1));
            for (int s = 1; s <= SkylineWreckOutline.Steps; s++)
            {
                float w = SkylineWreckOutline.WidthOfStep(s);
                Assert.That(w, Is.InRange(0.4f - 1e-5f, 1f + 1e-5f));
                Assert.That(SkylineWreckOutline.Step(w), Is.EqualTo(s), "step " + s);
            }
            Assert.That(SkylineWreckOutline.WidthOfStep(0), Is.EqualTo(0f));
            Assert.That(SkylineWreckOutline.WidthOfStep(SkylineWreckOutline.Steps), Is.EqualTo(1f));
        }

        [Test]
        public void TheStepNeverGrowsAsTheWindowRunsOut()
        {
            int last = SkylineWreckOutline.Steps;
            for (float left = 8f; left > 0.4f; left -= 0.01f)
            {
                int s = SkylineWreckOutline.Step(SkylineWreckOutline.Strength(left, Window));
                Assert.That(s, Is.LessThanOrEqualTo(last), "left " + left);
                last = s;
            }
        }

        [Test]
        public void LeftIsTheWindowMinusTheAge() =>
            Assert.That(SkylineWreckOutline.Left(13f, 10f, 8f), Is.EqualTo(5f).Within(1e-5f));
    }
}
