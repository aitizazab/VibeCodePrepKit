using JamKit.Movement;
using NUnit.Framework;

namespace JamKit.Tests.Movement
{
    /// <summary>The speed visuals' levels (docs/SPEED-FEEL.md "Speed feel (built)"): the boom fires once per real
    /// crossing (not on arriving at speed after a teleport) and settles; streaks follow the tier; puffs from fast,
    /// footprints at flying; the edge blur at flying only; Motion effects scales everything; "reduce effects" turns
    /// the edge blur and the chromatic pulse off and keeps streaks and lines subtle.</summary>
    public class SpeedFeelTests
    {
        const float Dt = 1f / 60f;

        static SpeedFeelLevels Run(SpeedFeelState s, float speed, float seconds, out int booms, float motion = 1f, bool reduce = false)
        {
            booms = 0;
            var l = default(SpeedFeelLevels);
            for (float t = 0f; t < seconds; t += Dt)
            {
                l = s.Tick(speed, Dt, motion, reduce);
                if (l.boom) booms++;
            }
            return l;
        }

        static SpeedFeelState New() => new SpeedFeelState(new SpeedFeelDef(), new SpeedTierDef());

        [Test]
        public void RunningSlowlyShowsNothing()
        {
            var s = New();
            var l = Run(s, 6f, 2f, out int booms);
            Assert.That(booms, Is.EqualTo(0));
            Assert.That(l.streakRate, Is.EqualTo(0f));
            Assert.That(l.puffs, Is.EqualTo(0));
            Assert.That(l.footprints, Is.False);
            Assert.That(l.edgeBlur, Is.EqualTo(0f));
            Assert.That(l.boomFov, Is.EqualTo(0f));
            Assert.That(l.dream, Is.EqualTo(0f));
        }

        [Test]
        public void StreaksGrowWithTheTierAndFlyingAddsFootprintsAndTheEdgeBlur()
        {
            var s = New();
            var fast = Run(s, 10f, 2f, out _);
            Assert.That(fast.tier, Is.EqualTo(SpeedTier.Fast));
            Assert.That(fast.streakRate, Is.EqualTo(new SpeedFeelDef().streakRateFast).Within(0.01f));
            Assert.That(fast.streakStretch, Is.GreaterThan(0f));
            Assert.That(fast.puffs, Is.EqualTo(new SpeedFeelDef().puffCount), "puffs from fast");
            Assert.That(fast.footprints, Is.False);
            Assert.That(fast.edgeBlur, Is.EqualTo(0f), "no blur below flying");
            var fly = Run(s, 16f, 2f, out _);
            Assert.That(fly.tier, Is.EqualTo(SpeedTier.Flying));
            Assert.That(fly.streakRate, Is.GreaterThan(fast.streakRate * 2f));
            Assert.That(fly.streakStretch, Is.GreaterThan(fast.streakStretch));
            Assert.That(fly.footprints, Is.True);
            Assert.That(fly.edgeBlur, Is.EqualTo(new SpeedFeelDef().edgeBlur).Within(0.01f));
            Assert.That(fly.dream, Is.EqualTo(1f).Within(0.01f));
        }

        [Test]
        public void TheBoomFiresOncePerCrossingPunchesTheFovAndSettles()
        {
            var s = New();
            var def = new SpeedFeelDef();
            Run(s, 10f, 1f, out _);
            var first = s.Tick(15f, Dt, 1f, false);
            Assert.That(first.boom, Is.True);
            Assert.That(first.chroma, Is.EqualTo(def.chromaPulse).Within(1e-6f), "the chromatic pulse on the crossing frame");
            var next = s.Tick(15f, Dt, 1f, false);
            Assert.That(next.boom, Is.False, "once");
            Assert.That(next.chroma, Is.EqualTo(0f), "one frame");
            // Peak about +6 degrees right after the rise; the ring rippling out.
            var peak = Run(s, 15f, def.boomRise - Dt, out int more);
            Assert.That(more, Is.EqualTo(0));
            Assert.That(peak.boomFov, Is.EqualTo(def.boomFov).Within(1.5f));
            Assert.That(peak.shockRadius, Is.GreaterThan(0f));
            Assert.That(peak.shockStrength, Is.GreaterThan(0f));
            // Settled after rise + settle.
            var settled = Run(s, 15f, def.boomRise + def.boomSettle + 0.1f, out more);
            Assert.That(more, Is.EqualTo(0), "holding flying doesn't boom again");
            Assert.That(settled.boomFov, Is.EqualTo(0f));
            Assert.That(settled.shockStrength, Is.EqualTo(0f));
            // Drop to fast (past the 0.6 s delay) and cross again: a second boom.
            Run(s, 9f, 1.5f, out _);
            Assert.That(s.Tier.Tier, Is.LessThan(SpeedTier.Flying));
            Run(s, 15f, 0.5f, out more);
            Assert.That(more, Is.EqualTo(1));
            Assert.That(s.Booms, Is.EqualTo(2));
        }

        [Test]
        public void HoveringOnTheLineDoesNotBoomAgain()
        {
            var s = New();
            int booms = 0;
            for (int i = 0; i < 600; i++)
                if (s.Tick(i % 20 < 10 ? 13.2f : 12.4f, Dt, 1f, false).boom) booms++;
            Assert.That(booms, Is.EqualTo(1), "the tier's hysteresis holds flying");
        }

        [Test]
        public void ArrivingAtSpeedAfterATeleportIsNotACrossing()
        {
            var s = New();
            s.Teleported();
            var l = s.Tick(18f, Dt, 1f, false);
            Assert.That(l.boom, Is.False);
            Assert.That(l.tier, Is.EqualTo(SpeedTier.Flying));
            Assert.That(s.Booms, Is.EqualTo(0));
        }

        [Test]
        public void MotionEffectsScalesEverythingAndZeroTurnsItOff()
        {
            var full = New();
            var half = New();
            var off = New();
            Run(full, 10f, 1f, out _);
            Run(half, 10f, 1f, out _, 0.5f);
            Run(off, 10f, 1f, out _, 0f);
            var a = full.Tick(16f, Dt, 1f, false);
            var b = half.Tick(16f, Dt, 0.5f, false);
            var c = off.Tick(16f, Dt, 0f, false);
            Assert.That(b.chroma, Is.EqualTo(a.chroma * 0.5f).Within(1e-6f));
            Assert.That(c.chroma, Is.EqualTo(0f));
            var a2 = Run(full, 16f, 0.06f, out _);
            var b2 = Run(half, 16f, 0.06f, out _, 0.5f);
            var c2 = Run(off, 16f, 0.06f, out _, 0f);
            Assert.That(b2.boomFov, Is.EqualTo(a2.boomFov * 0.5f).Within(0.01f));
            Assert.That(b2.streakRate, Is.EqualTo(a2.streakRate * 0.5f).Within(0.01f));
            Assert.That(b2.edgeBlur, Is.EqualTo(a2.edgeBlur * 0.5f).Within(0.01f));
            Assert.That(c2.boomFov, Is.EqualTo(0f));
            Assert.That(c2.shockStrength, Is.EqualTo(0f));
            Assert.That(c2.streakRate, Is.EqualTo(0f));
            Assert.That(c2.streakStretch, Is.EqualTo(0f));
            Assert.That(c2.edgeBlur, Is.EqualTo(0f));
            Assert.That(c2.puffs, Is.EqualTo(0));
            Assert.That(c2.footprints, Is.False);
            Assert.That(c2.linesShare, Is.EqualTo(0f));
            Assert.That(c2.dream, Is.EqualTo(0f));
        }

        [Test]
        public void ReduceEffectsTurnsOffTheBlurAndTheChromaticPulseAndKeepsStreaksSubtle()
        {
            var normal = New();
            var reduced = New();
            Run(normal, 10f, 1f, out _);
            Run(reduced, 10f, 1f, out _, 1f, true);
            var a = normal.Tick(16f, Dt, 1f, false);
            var b = reduced.Tick(16f, Dt, 1f, true);
            Assert.That(a.chroma, Is.GreaterThan(0f));
            Assert.That(b.boom, Is.True, "the boom still fires");
            Assert.That(b.chroma, Is.EqualTo(0f), "no chromatic pulse");
            var a2 = Run(normal, 16f, 2f, out _);
            var b2 = Run(reduced, 16f, 2f, out _, 1f, true);
            Assert.That(a2.edgeBlur, Is.GreaterThan(0f));
            Assert.That(b2.edgeBlur, Is.EqualTo(0f), "no edge blur");
            float share = new SpeedFeelDef().reduceShare;
            Assert.That(b2.streakRate, Is.EqualTo(a2.streakRate * share).Within(0.01f), "streaks subtle");
            Assert.That(b2.linesShare, Is.EqualTo(share).Within(1e-6f), "speed lines subtle");
            Assert.That(b2.streakRate, Is.GreaterThan(0f), "but still there");
        }

        [Test]
        public void TheBoomPunchRisesQuicklyAndEasesBack()
        {
            Assert.That(SpeedFeelState.BoomPunch(-0.1f, 0.05f, 0.4f), Is.EqualTo(0f));
            Assert.That(SpeedFeelState.BoomPunch(0.025f, 0.05f, 0.4f), Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(SpeedFeelState.BoomPunch(0.05f, 0.05f, 0.4f), Is.EqualTo(1f).Within(1e-4f));
            Assert.That(SpeedFeelState.BoomPunch(0.25f, 0.05f, 0.4f), Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(SpeedFeelState.BoomPunch(0.45f, 0.05f, 0.4f), Is.EqualTo(0f));
        }
    }
}
