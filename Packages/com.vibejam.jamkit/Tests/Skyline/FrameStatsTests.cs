using System.Collections.Generic;
using JamKit.Skyline;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    public class FrameStatsTests
    {
        [Test]
        public void OnePercentLowIsTheMeanOfTheSlowestOnePercent()
        {
            var frames = new List<float>();
            for (int i = 0; i < 990; i++) frames.Add(0.008f);
            for (int i = 0; i < 10; i++) frames.Add(0.020f);
            var s = FrameStats.Summarize(frames);
            Assert.That(s.frames, Is.EqualTo(1000));
            Assert.That(s.onePercentLowMs, Is.EqualTo(20f).Within(1e-3f));
            Assert.That(s.worstMs, Is.EqualTo(20f).Within(1e-3f));
            Assert.That(s.avgMs, Is.EqualTo(8.12f).Within(1e-3f));
        }

        [Test]
        public void FewFramesStillGiveOneSlowestFrame()
        {
            var s = FrameStats.Summarize(new List<float> { 0.010f, 0.030f, 0.010f });
            Assert.That(s.onePercentLowMs, Is.EqualTo(30f).Within(1e-3f));
        }

        [Test]
        public void NoFramesGiveZeros()
        {
            var s = FrameStats.Summarize(new List<float>());
            Assert.That(s.frames, Is.EqualTo(0));
            Assert.That(s.avgMs, Is.EqualTo(0f));
        }
    }
}
