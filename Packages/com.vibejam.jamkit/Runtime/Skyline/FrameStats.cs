using System;
using System.Collections.Generic;

namespace JamKit.Skyline
{
    public struct FrameSummary
    {
        public int frames;
        public float avgMs, onePercentLowMs, worstMs;
    }

    /// <summary>Frame-time numbers for the performance bar (docs/SKYLINE-DESIGN.md 10, item 12): the 1% low is the mean
    /// of the slowest 1% of frames (at least one frame), in milliseconds. Blocker: over 16.7 ms in a build.</summary>
    public static class FrameStats
    {
        public static FrameSummary Summarize(IReadOnlyList<float> frameSeconds)
        {
            int n = frameSeconds.Count;
            if (n == 0) return default;
            var sorted = new float[n];
            double sum = 0;
            for (int i = 0; i < n; i++) { sorted[i] = frameSeconds[i]; sum += frameSeconds[i]; }
            Array.Sort(sorted);
            int slow = Math.Max(1, n / 100);
            double slowSum = 0;
            for (int i = n - slow; i < n; i++) slowSum += sorted[i];
            return new FrameSummary
            {
                frames = n,
                avgMs = (float)(sum / n * 1000.0),
                onePercentLowMs = (float)(slowSum / slow * 1000.0),
                worstMs = sorted[n - 1] * 1000f,
            };
        }
    }
}
