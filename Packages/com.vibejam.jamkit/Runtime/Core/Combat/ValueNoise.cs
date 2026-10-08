using System;

namespace JamKit.Combat
{
    /// <summary>
    /// Smooth, seedable 1D noise: a hashed random value at every integer, eased between neighbours.
    /// Drives screen shake so it wobbles instead of jittering, and repeats exactly for a given seed.
    /// </summary>
    public static class ValueNoise
    {
        /// <summary>In [-1, 1]. Continuous with a zero slope at whole numbers; one wiggle per unit of x.</summary>
        public static float Sample(double x, uint seed)
        {
            double floor = Math.Floor(x);
            long i = (long)floor;
            double f = x - floor;
            double t = f * f * (3.0 - 2.0 * f);
            float a = Lattice(i, seed);
            float b = Lattice(i + 1, seed);
            return (float)(a + (b - a) * t);
        }

        /// <summary>The random value at integer <paramref name="i"/>, in [-1, 1].</summary>
        public static float Lattice(long i, uint seed)
        {
            unchecked
            {
                uint h = (uint)i ^ ((uint)(i >> 32) * 0x27D4EB2Du);
                h *= 0xB5297A4Du;
                h += seed;
                h ^= h >> 8;
                h += 0x68E31DA4u;
                h ^= h << 8;
                h *= 0x1B56C4E9u;
                h ^= h >> 8;
                return h * (2f / 4294967295f) - 1f;
            }
        }
    }
}
