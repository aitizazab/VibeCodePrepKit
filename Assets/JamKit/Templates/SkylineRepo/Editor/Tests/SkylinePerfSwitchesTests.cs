using System.Collections.Generic;
using JamKit.Skyline;
using NUnit.Framework;

namespace JamKit.SkylineRepo.Tests
{
    /// <summary>
    /// EditMode checks for the perf A/B switches (-skyline-no-lineweight, -skyline-no-damage-fx, -skyline-old-bot-sizes): each parses
    /// from the argument list, reaches the setting it controls, and is recorded in perf.json next to <c>inkOff</c>. The pass perf10 to
    /// perf11 regression is priced by running stage D with each switch alone (docs/SKYLINE-LOOK.md, perf round 5's pattern).
    /// </summary>
    public class SkylinePerfSwitchesTests
    {
        [TearDown]
        public void Reset() => SkylinePerfSwitches.ResetForTest();

        static void Parse(params string[] args)
        {
            var set = new HashSet<string>(args);
            SkylinePerfSwitches.Apply(set.Contains);
        }

        [Test]
        public void NoSwitchChangesNothing()
        {
            Parse();
            Assert.That(SkylinePerfSwitches.NoLineWeight, Is.False);
            Assert.That(SkylinePerfSwitches.NoDamageFx, Is.False);
            Assert.That(SkylinePerfSwitches.OldBotSizes, Is.False);
        }

        [Test]
        public void EachSwitchParsesAlone()
        {
            Parse("-skyline-no-lineweight");
            Assert.That(new[] { SkylinePerfSwitches.NoLineWeight, SkylinePerfSwitches.NoDamageFx, SkylinePerfSwitches.OldBotSizes }, Is.EqualTo(new[] { true, false, false }));
            Parse("-skyline-no-damage-fx");
            Assert.That(new[] { SkylinePerfSwitches.NoLineWeight, SkylinePerfSwitches.NoDamageFx, SkylinePerfSwitches.OldBotSizes }, Is.EqualTo(new[] { false, true, false }));
            Parse("-skyline-old-bot-sizes");
            Assert.That(new[] { SkylinePerfSwitches.NoLineWeight, SkylinePerfSwitches.NoDamageFx, SkylinePerfSwitches.OldBotSizes }, Is.EqualTo(new[] { false, false, true }));
        }

        [Test]
        public void TheSwitchesAreRecordedForPerfJson()
        {
            Parse("-skyline-no-damage-fx");
            Assert.That(SkylinePerfSwitches.Json(), Is.EqualTo("\"noLineWeight\":false,\"noDamageFx\":true,\"oldBotSizes\":false"));
        }

        [Test]
        public void OldBotSizesGiveUnitScalesAndTheDefsOtherwise()
        {
            var drone = SkylineEnemies.MallCopDrone();
            Parse();
            Assert.That(SkylinePerfSwitches.BodyScale(drone), Is.EqualTo(drone.bodyScale));
            Assert.That(SkylinePerfSwitches.WeakPointScale(drone), Is.EqualTo(drone.weakPointScale));
            Assert.That(drone.bodyScale, Is.GreaterThan(1f), "the def is the perf11 size");
            Parse("-skyline-old-bot-sizes");
            foreach (var d in new[] { SkylineEnemies.MallCopDrone(), SkylineEnemies.FloorCareUnit(), SkylineEnemies.Spotlight() })
            {
                Assert.That(SkylinePerfSwitches.BodyScale(d), Is.EqualTo(1f), d.id);
                Assert.That(SkylinePerfSwitches.WeakPointScale(d), Is.EqualTo(1f), d.id);
            }
        }

        [Test]
        public void NoLineWeightZeroesTheResolvedLineWeightOnly()
        {
            var ink = UnityEngine.ScriptableObject.CreateInstance<SkylineInk>();
            try
            {
                ink.lineWeightVariation.Override(1f);
                ink.halftoneDots.Override(0.5f);
                Parse();
                var on = SkylineInkStyle.Resolve(ink);
                Assert.That(on.lineWeight, Is.EqualTo(1f).Within(1e-6f));
                Parse("-skyline-no-lineweight");
                var off = SkylineInkStyle.Resolve(ink);
                Assert.That(off.lineWeight, Is.EqualTo(0f));
                Assert.That(off.halftone, Is.EqualTo(0.5f).Within(1e-6f), "the other options are untouched");
            }
            finally { UnityEngine.Object.DestroyImmediate(ink); }
        }
    }
}
