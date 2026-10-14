using System;

namespace JamKit.Skyline
{
    /// <summary>
    /// The strippable wreck's outline over its strip window (docs/SKYLINE-STYLE.md rule 2: "thins as the strip window runs out
    /// and flashes out over its last 0.4 s"; docs/SKYLINE-PLAN-FLOOR1.md Task S0). Pure: the Unity wreck calls
    /// <see cref="Strength"/> every frame and applies <see cref="Step"/> to a shared material, so the SRP Batcher keeps batching.
    /// <para>Strength is the outline's width as a fraction of the full thick outline: 1 while more than 3 s of an 8 s window are
    /// left (the same share for other windows), thinning linearly to <see cref="MinWidth"/> (40%) at 0.4 s left, then six
    /// equal slices of blink (on at the minimum width, off, on, off, on, off) over the last 0.4 s, then 0.</para>
    /// </summary>
    public static class SkylineWreckOutline
    {
        /// <summary>The last stretch of the window, in which the outline blinks out.</summary>
        public const float FlashSeconds = 0.4f;
        /// <summary>The width fraction the thinning ends at (and the blink's "on").</summary>
        public const float MinWidth = 0.4f;
        /// <summary>Thinning starts when this share of the window is left (3 s of 8 s).</summary>
        public const float FullShare = 3f / 8f;
        /// <summary>Slices of the flash (even slices on, odd slices off; the last slice is off).</summary>
        public const int FlashSlices = 6;
        /// <summary>The number of material steps: 1 is <see cref="MinWidth"/>, <see cref="Steps"/> is full width (0 is off).</summary>
        public const int Steps = 7;

        /// <summary>The outline's width fraction, 0 (none) to 1 (full), <paramref name="left"/> seconds before the strip window
        /// of <paramref name="window"/> seconds ends. NaN or an unusable window gives 0.</summary>
        public static float Strength(float left, float window)
        {
            if (float.IsNaN(left) || float.IsNaN(window) || window <= 0f || left <= 0f) return 0f;
            if (left >= window) return 1f;
            if (left <= FlashSeconds)
            {
                int slice = (int)((FlashSeconds - left) / (FlashSeconds / FlashSlices));
                if (slice < 0) slice = 0;
                if (slice > FlashSlices - 1) slice = FlashSlices - 1;
                return slice % 2 == 0 ? MinWidth : 0f;
            }
            float thinFrom = Math.Max(window * FullShare, FlashSeconds + 0.1f);
            if (left >= thinFrom) return 1f;
            return MinWidth + (1f - MinWidth) * (left - FlashSeconds) / (thinFrom - FlashSeconds);
        }

        /// <summary>The material step for a strength: 0 off, 1..<see cref="Steps"/> from the minimum to the full width.</summary>
        public static int Step(float strength)
        {
            if (float.IsNaN(strength) || strength <= 0f) return 0;
            int s = 1 + (int)Math.Round((strength - MinWidth) / ((1f - MinWidth) / (Steps - 1)));
            return s < 1 ? 1 : (s > Steps ? Steps : s);
        }

        /// <summary>The width fraction a material step stands for (0 for step 0).</summary>
        public static float WidthOfStep(int step)
        {
            if (step <= 0) return 0f;
            if (step >= Steps) return 1f;
            return MinWidth + (1f - MinWidth) * (step - 1) / (Steps - 1);
        }

        /// <summary>Seconds left of a wreck's strip window at <paramref name="now"/> (wrecked at <paramref name="wreckedAt"/>).</summary>
        public static float Left(float now, float wreckedAt, float window) => window - (now - wreckedAt);
    }
}
