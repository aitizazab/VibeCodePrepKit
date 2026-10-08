using System;
using System.Collections.Generic;

namespace JamKit
{
    /// <summary>
    /// Deterministic, seedable random numbers (PCG32). The same seed gives the same run on every
    /// machine, so a bug report can name a seed and tests can pin exact outcomes.
    /// Use one Rng per concern (map, loot, enemy AI) via <see cref="Fork"/> so adding a roll in one
    /// place doesn't reshuffle everything else.
    /// </summary>
    public sealed class Rng
    {
        const ulong Multiplier = 6364136223846793005UL;

        ulong _state;
        readonly ulong _inc;

        public ulong Seed { get; }

        public Rng(ulong seed, ulong stream = 0x14057B7EF767814FUL)
        {
            Seed = seed;
            _inc = (stream << 1) | 1UL;
            _state = 0UL;
            NextUInt();
            _state += seed;
            NextUInt();
        }

        /// <summary>A seed from the clock, for runs the player didn't pick a seed for.</summary>
        public static ulong TimeSeed() => (ulong)DateTime.UtcNow.Ticks * 0x9E3779B97F4A7C15UL;

        public uint NextUInt()
        {
            ulong old = _state;
            _state = unchecked(old * Multiplier + _inc);
            uint xorShifted = (uint)(((old >> 18) ^ old) >> 27);
            int rot = (int)(old >> 59);
            return (xorShifted >> rot) | (xorShifted << ((-rot) & 31));
        }

        /// <summary>Uniform in [0, 1).</summary>
        public float NextFloat() => (NextUInt() >> 8) * (1f / 16777216f);

        /// <summary>Uniform in [0, 1) with double precision.</summary>
        public double NextDouble() => NextUInt() * (1.0 / 4294967296.0);

        /// <summary>Uniform integer in [minInclusive, maxExclusive). Unbiased.</summary>
        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
                throw new ArgumentException("maxExclusive must be greater than minInclusive");
            uint bound = (uint)(maxExclusive - minInclusive);
            uint threshold = (uint)(-(int)bound) % bound;
            uint r;
            do { r = NextUInt(); } while (r < threshold);
            return minInclusive + (int)(r % bound);
        }

        /// <summary>Uniform float in [min, max).</summary>
        public float Range(float min, float max) => min + (max - min) * NextFloat();

        public bool Chance(float probability) => NextFloat() < probability;

        /// <summary>-1 or +1.</summary>
        public int Sign() => (NextUInt() & 1) == 0 ? -1 : 1;

        public T Pick<T>(IReadOnlyList<T> items)
        {
            if (items == null || items.Count == 0) throw new ArgumentException("Pick from an empty list");
            return items[Range(0, items.Count)];
        }

        /// <summary>Index chosen with probability proportional to its weight. Weights &lt;= 0 never win.
        /// Returns -1 if no weight is positive.</summary>
        public int WeightedIndex(IReadOnlyList<float> weights)
        {
            double total = 0;
            for (int i = 0; i < weights.Count; i++)
                if (weights[i] > 0) total += weights[i];
            if (total <= 0) return -1;
            double roll = NextDouble() * total;
            int last = -1;
            for (int i = 0; i < weights.Count; i++)
            {
                if (weights[i] <= 0) continue;
                last = i;
                roll -= weights[i];
                if (roll < 0) return i;
            }
            return last; // floating-point edge: roll landed exactly on the total
        }

        /// <summary>Fisher-Yates, in place.</summary>
        public void Shuffle<T>(IList<T> items)
        {
            for (int i = items.Count - 1; i > 0; i--)
            {
                int j = Range(0, i + 1);
                T tmp = items[i];
                items[i] = items[j];
                items[j] = tmp;
            }
        }

        /// <summary>An independent generator derived from this one (and advancing it once).</summary>
        public Rng Fork()
        {
            ulong seed = ((ulong)NextUInt() << 32) | NextUInt();
            ulong stream = ((ulong)NextUInt() << 32) | NextUInt();
            return new Rng(seed, stream);
        }
    }
}
