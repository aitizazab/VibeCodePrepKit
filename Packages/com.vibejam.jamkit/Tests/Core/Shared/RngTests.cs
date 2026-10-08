using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace JamKit.Tests
{
    public class RngTests
    {
        [Test]
        public void SameSeedGivesSameSequence()
        {
            var a = new Rng(42);
            var b = new Rng(42);
            for (int i = 0; i < 1000; i++)
                Assert.That(a.NextUInt(), Is.EqualTo(b.NextUInt()));
        }

        [Test]
        public void DifferentSeedsDiverge()
        {
            var a = new Rng(1);
            var b = new Rng(2);
            int same = 0;
            for (int i = 0; i < 100; i++)
                if (a.NextUInt() == b.NextUInt()) same++;
            Assert.That(same, Is.LessThan(3));
        }

        [Test]
        public void RangeStaysInBoundsAndCoversThem()
        {
            var rng = new Rng(7);
            var seen = new HashSet<int>();
            for (int i = 0; i < 10000; i++)
            {
                int v = rng.Range(-3, 4);
                Assert.That(v, Is.InRange(-3, 3));
                seen.Add(v);
            }
            Assert.That(seen.Count, Is.EqualTo(7));
        }

        [Test]
        public void NextFloatIsInUnitInterval()
        {
            var rng = new Rng(9);
            for (int i = 0; i < 10000; i++)
            {
                float f = rng.NextFloat();
                Assert.That(f, Is.GreaterThanOrEqualTo(0f));
                Assert.That(f, Is.LessThan(1f));
            }
        }

        [Test]
        public void WeightedIndexFollowsWeightsAndSkipsZero()
        {
            var rng = new Rng(11);
            var weights = new List<float> { 1f, 0f, 3f };
            var counts = new int[3];
            for (int i = 0; i < 40000; i++) counts[rng.WeightedIndex(weights)]++;
            Assert.That(counts[1], Is.EqualTo(0));
            Assert.That(counts[2] / (float)counts[0], Is.EqualTo(3f).Within(0.25f));
        }

        [Test]
        public void WeightedIndexWithNoPositiveWeightReturnsMinusOne()
        {
            Assert.That(new Rng(1).WeightedIndex(new List<float> { 0f, -1f }), Is.EqualTo(-1));
        }

        [Test]
        public void ShuffleKeepsElements()
        {
            var list = Enumerable.Range(0, 50).ToList();
            new Rng(5).Shuffle(list);
            Assert.That(list.OrderBy(x => x), Is.EqualTo(Enumerable.Range(0, 50)));
            Assert.That(list, Is.Not.EqualTo(Enumerable.Range(0, 50)));
        }

        [Test]
        public void ForkIsDeterministicAndIndependent()
        {
            var a = new Rng(3).Fork();
            var b = new Rng(3).Fork();
            Assert.That(a.NextUInt(), Is.EqualTo(b.NextUInt()));
            var parent = new Rng(3);
            var child = parent.Fork();
            Assert.That(child.NextUInt(), Is.Not.EqualTo(parent.NextUInt()));
        }
    }
}
