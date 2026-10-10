using JamKit.Movement;
using NUnit.Framework;

namespace JamKit.Tests.Movement
{
    /// <summary>The shared speed tiers (docs/SPEED-FEEL.md): a crossing moment into flying, hysteresis and a drop
    /// delay so a stumble doesn't drop the tier, quick-in slow-out blends.</summary>
    public class SpeedTierTests
    {
        const float Dt = 1f / 60f;

        static SpeedTier Run(SpeedTier t, float speed, float seconds, out bool enteredFlying, out bool dropped)
        {
            enteredFlying = false;
            dropped = false;
            for (float s = 0f; s < seconds; s += Dt)
            {
                var ev = t.Tick(speed, Dt);
                enteredFlying |= ev.enteredFlying;
                dropped |= ev.down;
            }
            return t;
        }

        [Test]
        public void TiersFollowSpeedAndTheCrossingIntoFlyingFiresOnce()
        {
            var t = new SpeedTier(new SpeedTierDef());
            Run(t, 5f, 1f, out bool flew, out _);
            Assert.That(t.Tier, Is.EqualTo(SpeedTier.Running));
            Assert.That(flew, Is.False);
            Run(t, 9f, 1f, out flew, out _);
            Assert.That(t.Tier, Is.EqualTo(SpeedTier.Fast));
            Assert.That(flew, Is.False);
            int crossings = 0;
            for (int i = 0; i < 120; i++) if (t.Tick(14f, Dt).enteredFlying) crossings++;
            Assert.That(t.Tier, Is.EqualTo(SpeedTier.Flying));
            Assert.That(crossings, Is.EqualTo(1), "the boom fires once per crossing");
        }

        [Test]
        public void HoveringOnTheLineDoesNotFlicker()
        {
            var t = new SpeedTier(new SpeedTierDef());
            Run(t, 14f, 0.5f, out _, out _);
            int crossings = 0, drops = 0;
            for (int i = 0; i < 600; i++)
            {
                var ev = t.Tick(i % 2 == 0 ? 13.2f : 12.4f, Dt); // jitter around 13, never below the 11.5 exit
                if (ev.enteredFlying) crossings++;
                if (ev.down) drops++;
            }
            Assert.That(t.Tier, Is.EqualTo(SpeedTier.Flying));
            Assert.That(crossings + drops, Is.Zero);
        }

        [Test]
        public void AStumbleShorterThanTheDropDelayKeepsTheTier()
        {
            var t = new SpeedTier(new SpeedTierDef());
            Run(t, 14f, 0.5f, out _, out _);
            Run(t, 3f, 0.4f, out _, out bool dropped); // a bad landing, under the 0.6 s delay
            Assert.That(dropped, Is.False);
            Assert.That(t.Tier, Is.EqualTo(SpeedTier.Flying));
            Run(t, 14f, 0.2f, out bool flew, out _);
            Assert.That(flew, Is.False, "no second boom for a stumble");
        }

        [Test]
        public void SlowingForRealDropsTheTierAfterTheDelay()
        {
            var t = new SpeedTier(new SpeedTierDef());
            Run(t, 14f, 0.5f, out _, out _);
            Run(t, 4f, 2f, out _, out bool dropped);
            Assert.That(dropped, Is.True);
            Assert.That(t.Tier, Is.EqualTo(SpeedTier.Running));
        }

        [Test]
        public void BlendsRiseQuicklyAndFallSlowly()
        {
            var t = new SpeedTier(new SpeedTierDef());
            Run(t, 14f, 0.3f, out _, out _);
            Assert.That(t.FlyingBlend, Is.GreaterThan(0.9f), "about a quarter second in");
            Run(t, 4f, 1.2f, out _, out _); // dropped after 0.6 s, then fading
            Assert.That(t.FlyingBlend, Is.GreaterThan(0.3f).And.LessThan(1f), "still fading out");
        }
    }
}
