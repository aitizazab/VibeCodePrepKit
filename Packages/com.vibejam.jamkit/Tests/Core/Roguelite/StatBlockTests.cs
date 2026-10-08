using System.Collections.Generic;
using JamKit.Roguelite;
using NUnit.Framework;

namespace JamKit.Tests
{
    public class StatBlockTests
    {
        [Test]
        public void FormulaIsFlatThenSummedPercentThenMultiply()
        {
            var s = new StatBlock();
            s.SetBase("dmg", 10f);
            s.Add(new StatModifier("dmg", StatOp.Multiply, 2f, "a"));
            s.Add(new StatModifier("dmg", StatOp.PercentAdd, 0.1f, "b"));
            s.Add(new StatModifier("dmg", StatOp.Flat, 5f, "c"));
            s.Add(new StatModifier("dmg", StatOp.PercentAdd, 0.4f, "d"));
            s.Add(new StatModifier("dmg", StatOp.Multiply, 1.5f, "e"));
            // (10 + 5) * (1 + 0.5) * (2 * 1.5) = 67.5, whatever order they were added in
            Assert.That(s.Get("dmg"), Is.EqualTo(67.5f).Within(1e-4));
        }

        [Test]
        public void UnknownKeyIsZeroAndFlatStillCounts()
        {
            var s = new StatBlock();
            Assert.That(s.Get("nothing"), Is.EqualTo(0f));
            s.Add(new StatModifier("pierce", StatOp.Flat, 2f, "x"));
            Assert.That(s.Get("pierce"), Is.EqualTo(2f));
        }

        [Test]
        public void RemoveBySourceTakesOnlyThatSource()
        {
            var s = new StatBlock();
            s.SetBase("spd", 4f);
            s.Add(new StatModifier("spd", StatOp.Flat, 1f, "boots"));
            s.Add(new StatModifier("spd", StatOp.PercentAdd, 0.5f, "boots"));
            s.Add(new StatModifier("spd", StatOp.Flat, 2f, "buff"));
            s.Add(new StatModifier("hp", StatOp.Flat, 10f, "boots"));
            Assert.That(s.Get("spd"), Is.EqualTo(10.5f).Within(1e-5));
            Assert.That(s.RemoveBySource("boots"), Is.EqualTo(3));
            Assert.That(s.Get("spd"), Is.EqualTo(6f).Within(1e-5));
            Assert.That(s.Get("hp"), Is.EqualTo(0f));
            Assert.That(s.RemoveBySource("boots"), Is.EqualTo(0));
        }

        [Test]
        public void ClampAppliesAfterModifiers()
        {
            var s = new StatBlock();
            s.SetBase("crit", 0.5f);
            s.SetClamp("crit", 0f, 1f);
            s.Add(new StatModifier("crit", StatOp.Flat, 0.8f, "a"));
            Assert.That(s.Get("crit"), Is.EqualTo(1f));
            s.Add(new StatModifier("crit", StatOp.Multiply, -1f, "curse"));
            Assert.That(s.Get("crit"), Is.EqualTo(0f));
            s.ClearClamp("crit");
            Assert.That(s.Get("crit"), Is.EqualTo(-1.3f).Within(1e-5));
        }

        [Test]
        public void CacheRefreshesAndChangedFiresPerKey()
        {
            var s = new StatBlock();
            var changed = new List<string>();
            s.Changed += k => changed.Add(k);
            s.SetBase("a", 1f);
            Assert.That(s.Get("a"), Is.EqualTo(1f));
            var m = new StatModifier("a", StatOp.Flat, 2f, "src");
            s.Add(m);
            Assert.That(s.Get("a"), Is.EqualTo(3f));
            Assert.That(s.Remove(m), Is.True);
            Assert.That(s.Get("a"), Is.EqualTo(1f));
            Assert.That(changed, Is.EqualTo(new[] { "a", "a", "a" }));
        }
    }
}
