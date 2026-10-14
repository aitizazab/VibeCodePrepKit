using System;
using System.Collections.Generic;
using JamKit.Skyline;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    /// <summary>Task SD1 (pure part): what each kind of spawn door does in its 2 s telegraph.</summary>
    public class DoorTelegraphPlanTests
    {
        static readonly DoorKind[] Kinds = (DoorKind[])Enum.GetValues(typeof(DoorKind));

        [Test]
        public void EveryKindHasASoundALightAndAMotion()
        {
            foreach (var k in Kinds)
            {
                var look = DoorTelegraphPlan.For(k);
                Assert.That(look.kind, Is.EqualTo(k));
                Assert.That(look.startSound, Does.Contain("/"), k + " sound");
                Assert.That(look.lightIntensity, Is.GreaterThan(0f), k + " light");
                Assert.That(Math.Max(look.lightR, Math.Max(look.lightG, look.lightB)), Is.GreaterThan(0.5f), k + " light colour");
                Assert.That(look.travel, Is.GreaterThan(0f), k + " motion");
            }
        }

        [Test]
        public void TheMotionsAreDistinctPerKind()
        {
            var seen = new HashSet<DoorMotion>();
            foreach (var k in Kinds) Assert.That(seen.Add(DoorTelegraphPlan.For(k).motion), Is.True, k.ToString());
        }

        [Test]
        public void ABlinkingDoorHasABlinkSound()
        {
            foreach (var k in Kinds)
            {
                var look = DoorTelegraphPlan.For(k);
                if (look.blinkHz > 0f) Assert.That(look.blinkSound, Does.Contain("/"), k.ToString());
            }
        }

        [Test]
        public void ProgressRunsOverTwoSecondsAndClamps()
        {
            Assert.That(DoorTelegraphPlan.Progress(-1f), Is.EqualTo(0f));
            Assert.That(DoorTelegraphPlan.Progress(0f), Is.EqualTo(0f));
            Assert.That(DoorTelegraphPlan.Progress(1f), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(DoorTelegraphPlan.Progress(2f), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(DoorTelegraphPlan.Progress(9f), Is.EqualTo(1f));
            Assert.That(DoorTelegraphPlan.Progress(float.NaN), Is.EqualTo(0f));
        }

        [Test]
        public void OpenIsMonotoneFromShutToOpenAndReachesOneAtTheEnd()
        {
            Assert.That(DoorTelegraphPlan.Open(0f), Is.EqualTo(0f));
            Assert.That(DoorTelegraphPlan.Open(0.2f), Is.EqualTo(0f));
            Assert.That(DoorTelegraphPlan.Open(1f), Is.EqualTo(1f));
            float prev = 0f;
            for (int i = 0; i <= 100; i++)
            {
                float o = DoorTelegraphPlan.Open(i / 100f);
                Assert.That(o, Is.GreaterThanOrEqualTo(prev - 1e-6f));
                prev = o;
            }
            Assert.That(DoorTelegraphPlan.Open(0.6f), Is.InRange(0.3f, 0.7f));
        }

        [Test]
        public void TheRattleFadesAndTheTwangStaysInsideItsTravel()
        {
            float early = 0f, late = 0f;
            for (int i = 0; i < 200; i++)
            {
                early = Math.Max(early, Math.Abs(DoorTelegraphPlan.Rattle(i * 0.001f)));
                late = Math.Max(late, Math.Abs(DoorTelegraphPlan.Rattle(1.8f + i * 0.001f)));
            }
            Assert.That(early, Is.GreaterThan(late));
            for (int i = 0; i <= 40; i++)
                Assert.That(Math.Abs(DoorTelegraphPlan.Twang(i * 0.05f, 0.35f)), Is.LessThanOrEqualTo(0.35f + 1e-5f));
        }

        [Test]
        public void ABeaconBlinksFasterTowardTheEndAndASteadyDoorBrightens()
        {
            var vent = DoorTelegraphPlan.For(DoorKind.SkylightVent);
            int firstHalf = DoorTelegraphPlan.BlinksStarted(vent, 1f);
            int secondHalf = DoorTelegraphPlan.BlinksStarted(vent, 2f) - firstHalf;
            Assert.That(secondHalf, Is.GreaterThan(firstHalf - 1), "the rate climbs, so the second second has more blinks");
            Assert.That(DoorTelegraphPlan.BlinksStarted(vent, 2f), Is.InRange(6, 10));
            var shutter = DoorTelegraphPlan.For(DoorKind.ShopShutter);
            Assert.That(DoorTelegraphPlan.BlinksStarted(shutter, 2f), Is.EqualTo(0));
            Assert.That(DoorTelegraphPlan.Beacon(shutter, 2f), Is.GreaterThan(DoorTelegraphPlan.Beacon(shutter, 0f)));
        }

        [Test]
        public void TheBeaconPulseIsEitherBrightOrDim()
        {
            var vent = DoorTelegraphPlan.For(DoorKind.SkylightVent);
            for (int i = 0; i <= 40; i++)
            {
                float b = DoorTelegraphPlan.Beacon(vent, i * 0.05f);
                Assert.That(b == 1f || Math.Abs(b - 0.15f) < 1e-5f, Is.True, "t=" + i * 0.05f);
            }
        }

        [Test]
        public void TheTelegraphLengthIsTheFloorRule()
        {
            Assert.That(FloorRules.DoorTelegraphSeconds, Is.EqualTo(2f));
        }
    }
}
