using UnityEngine;

namespace JamKit.SkylineRepo
{
    /// <summary>
    /// The ink pass's pure maths, kept apart from the renderer feature so the EditMode tests can pin it down (docs/SKYLINE-STYLE.md,
    /// "How it's built": the cost analysis). Everything here is arithmetic on numbers; nothing touches the GPU.
    /// </summary>
    public static class SkylineInkMath
    {
        /// <summary>The occupancy texture holds one texel per 4 x 4 block of the mask (see <see cref="OccupancySize"/>).</summary>
        public const int OccupancyBlock = 4;

        /// <summary>How many mask taps the thick outline's circle search takes per pixel that is near the mask: 12 + 8 + 4.</summary>
        public const int OutlineTaps = 24;

        /// <summary>Widths are given at 1080p and scale with the render height (never below half).</summary>
        public static float PixelScale(float renderHeight) => Mathf.Max(0.5f, renderHeight / 1080f);

        /// <summary>The outline's search radius in pixels at this render height.</summary>
        public static float OutlineRadiusPx(float widthAt1080, float renderHeight) => widthAt1080 * PixelScale(renderHeight);

        /// <summary>The occupancy texture's size along one axis: a block of 4 pixels per texel, rounded up, at least 1.</summary>
        public static int OccupancySize(int pixels) => Mathf.Max(1, (pixels + OccupancyBlock - 1) / OccupancyBlock);

        /// <summary>How many blocks the occupancy mask is dilated by on each side, so that "no occupancy here" guarantees "no
        /// mask within the outline radius": a pixel is at most 3 pixels from its block's far edge, so k blocks reach at least
        /// 4 k pixels from it, and k = ceil(radius / 4) covers the radius.</summary>
        public static int DilationBlocks(float radiusPx) => Mathf.Max(1, Mathf.CeilToInt(radiusPx / OccupancyBlock - 1e-4f));

        /// <summary>Texture loads of the dilation pass per occupancy texel (a square window).</summary>
        public static int DilationLoads(int blocks) => (2 * blocks + 1) * (2 * blocks + 1);

        /// <summary>Mask taps the old composite took for every pixel of the frame.</summary>
        public static long OldMaskTapsPerFrame(int width, int height) => (long)width * height * OutlineTaps;

        /// <summary>The occupancy passes' texture reads for the whole frame (4 bilinear taps per block, the dilation window per
        /// block, one load per pixel in the composite), the figure the docs compare with <see cref="OldMaskTapsPerFrame"/>.</summary>
        public static long NewOccupancyReadsPerFrame(int width, int height, int blocks)
        {
            long texels = (long)OccupancySize(width) * OccupancySize(height);
            return texels * 4 + texels * DilationLoads(blocks) + (long)width * height;
        }

        /// <summary>The angle one pixel spans at the image centre, in radians, from the projection matrix's m11 (1 / tan(fov / 2))
        /// and the render height. The depth-curvature creases measure a depth plane's bend against it.</summary>
        public static float PixelAngle(float projectionM11, float renderHeight)
        {
            float m = Mathf.Max(Mathf.Abs(projectionM11), 1e-4f);
            return 2f / (m * Mathf.Max(renderHeight, 1f));
        }

        /// <summary>The wobble's amplitude in pixels at this render height for a strength of 0..1 (about 1.2 px at 1080p at full).</summary>
        public static float WobbleAmplitudePx(float strength, float renderHeight) => Mathf.Clamp01(strength) * 1.2f * PixelScale(renderHeight);
    }
}
