using System;
using JamKit.Movement;
using NUnit.Framework;

namespace JamKit.Tests.Movement
{
    /// <summary>The movement's generated sounds (the fallback for every event, and the real loops) and the mix
    /// rules: every event has variants, nothing peaks above -1 dBFS, loops have no seam, and the mix follows speed.</summary>
    public class MoveSoundTests
    {
        [Test]
        public void EveryOneShotHasDistinctVariantsUnderMinusOneDbfs()
        {
            foreach (var id in MoveSounds.OneShots)
            {
                Assert.That(MoveSounds.VariantCount(id), Is.GreaterThanOrEqualTo(3), id);
                float[] first = null;
                bool differs = false;
                for (int v = 0; v < MoveSounds.VariantCount(id); v++)
                {
                    var s = MoveSounds.Make(id, v);
                    Assert.That(s, Is.Not.Null, id);
                    Assert.That(s.Length, Is.GreaterThan(MoveSounds.Rate / 50), id + " lasts at least 20 ms");
                    Assert.That(s.Length, Is.LessThan(MoveSounds.Rate), id + " is short");
                    float pk = MoveSounds.Peak(s);
                    Assert.That(pk, Is.LessThan(MoveSounds.MaxPeak), id + " peaks under -1 dBFS");
                    Assert.That(pk, Is.GreaterThan(0.3f), id + " is not silent");
                    Assert.That(Math.Abs(s[0]), Is.LessThan(0.02f), id + " starts without a click");
                    Assert.That(Math.Abs(s[s.Length - 1]), Is.LessThan(0.02f), id + " ends without a click");
                    if (first == null) first = s;
                    else if (s.Length != first.Length || s[s.Length / 3] != first[first.Length / 3]) differs = true;
                }
                Assert.That(differs, Is.True, id + " variants differ");
            }
        }

        [Test]
        public void LoopsAreSeamlessAndSteady()
        {
            foreach (var id in MoveSounds.Loops)
            {
                var s = MoveSounds.Make(id, 0);
                Assert.That(s, Is.Not.Null, id);
                Assert.That(s.Length, Is.GreaterThanOrEqualTo(MoveSounds.Rate), id + " is at least 1 s");
                Assert.That(MoveSounds.Peak(s), Is.LessThan(MoveSounds.MaxPeak), id);
                // The wrap (last sample into the first) is no bigger a step than the clip's own steps.
                float wrap = Math.Abs(s[0] - s[s.Length - 1]);
                Assert.That(wrap, Is.LessThanOrEqualTo(4f * RmsStep(s)), id + " wrap is a typical step (" + wrap + " vs " + RmsStep(s) + ")");
                Assert.That(wrap, Is.LessThanOrEqualTo(MaxStep(s) + 1e-4f), id + " wrap vs steps");
                // No level jump across the seam: the last and first 50 ms match within 4 dB.
                int w = MoveSounds.Rate / 20;
                float head = Rms(s, 0, w), tail = Rms(s, s.Length - w, w);
                // (Breathing loops start and end in a pause: both quiet is fine.)
                float quiet = MoveSounds.Peak(s) * 0.003f;
                if (head > quiet && tail > quiet)
                    Assert.That(20f * Math.Log10((head + 1e-6f) / (tail + 1e-6f)), Is.InRange(-4.0, 4.0), id + " level across the seam");
            }
        }

        [Test]
        public void TheSoundsAreTheSameEveryRun()
        {
            var a = MoveSounds.Make("mv_step", 2);
            var b = MoveSounds.Make("mv_step", 2);
            Assert.That(a, Is.EqualTo(b));
            Assert.That(MoveSounds.Make("nope", 0), Is.Null);
            Assert.That(MoveSounds.Knows("mv_wind_high"), Is.True);
            Assert.That(MoveSounds.IsLoop("mv_step"), Is.False);
        }

        [Test]
        public void TheDoubleJumpIsBrighterThanTheJump()
        {
            // The two must sound clearly different (RESEARCH-MOVEMENT 8.3): the puff is a hiss, the jump a cloth swish.
            Assert.That(Centroid(MoveSounds.Make("mv_djump", 0)), Is.GreaterThan(Centroid(MoveSounds.Make("mv_jump", 0)) * 1.3f));
        }

        [Test]
        public void FootstepsGetHeavierWithSpeed()
        {
            float walk = MoveSoundMix.StepWeight(5f, 5f, 9.5f), sprint = MoveSoundMix.StepWeight(9.5f, 5f, 9.5f);
            Assert.That(walk, Is.EqualTo(0f));
            Assert.That(sprint, Is.EqualTo(1f));
            Assert.That(MoveSoundMix.StepVolume(sprint), Is.GreaterThan(MoveSoundMix.StepVolume(walk)));
            Assert.That(MoveSoundMix.StepPitch(sprint), Is.LessThan(MoveSoundMix.StepPitch(walk)));
            Assert.That(MoveSoundMix.StepHeavy(walk), Is.EqualTo(0f));
            Assert.That(MoveSoundMix.StepHeavy(sprint), Is.GreaterThan(0.3f));
        }

        [Test]
        public void LandingsPickATierByFallSpeed()
        {
            Assert.That(MoveSoundMix.Landing(2f), Is.EqualTo(MoveSoundMix.LandTier.Step));
            Assert.That(MoveSoundMix.Landing(7.75f), Is.EqualTo(MoveSoundMix.LandTier.Soft), "a plain jump lands soft");
            Assert.That(MoveSoundMix.Landing(10.4f), Is.EqualTo(MoveSoundMix.LandTier.Normal), "jump + double jump");
            Assert.That(MoveSoundMix.Landing(15f), Is.EqualTo(MoveSoundMix.LandTier.Hard));
            Assert.That(MoveSoundMix.LandGain(18f), Is.GreaterThan(MoveSoundMix.LandGain(8f)));
            Assert.That(MoveSoundMix.LandGain(40f), Is.LessThanOrEqualTo(1f));
        }

        [Test]
        public void WindRisesWithSpeedAndTheSlideFadesAsItSlows()
        {
            Assert.That(MoveSoundMix.WindLow(4f), Is.EqualTo(0f));
            Assert.That(MoveSoundMix.WindHigh(9.5f), Is.EqualTo(0f), "no air rush at sprint");
            Assert.That(MoveSoundMix.WindHigh(22f), Is.EqualTo(1f));
            float last = -1f;
            for (float v = 0f; v <= 22f; v += 0.5f)
            {
                float w = MoveSoundMix.WindLow(v) + MoveSoundMix.WindHigh(v);
                Assert.That(w, Is.GreaterThanOrEqualTo(last));
                last = w;
            }
            Assert.That(MoveSoundMix.WindPitch(22f), Is.GreaterThan(MoveSoundMix.WindPitch(5f)));
            Assert.That(MoveSoundMix.SlideGain(4f), Is.EqualTo(0f));
            Assert.That(MoveSoundMix.SlideGain(12f), Is.EqualTo(1f));
        }

        [Test]
        public void RoundRobinNeverRepeatsAndReachesEveryVariant()
        {
            var seen = new bool[4];
            int last = -1;
            var r = new Random(5);
            for (int i = 0; i < 200; i++)
            {
                int k = MoveSoundMix.NextVariant(4, last, (float)r.NextDouble());
                Assert.That(k, Is.InRange(0, 3));
                Assert.That(k, Is.Not.EqualTo(last));
                seen[k] = true;
                last = k;
            }
            Assert.That(seen, Is.All.True);
            Assert.That(MoveSoundMix.NextVariant(1, 0, 0.9f), Is.EqualTo(0));
            Assert.That(MoveSoundMix.NextVariant(3, 2, 1f), Is.InRange(0, 1));
        }

        static float MaxStep(float[] s)
        {
            float m = 0f;
            for (int i = 1; i < s.Length; i++) m = Math.Max(m, Math.Abs(s[i] - s[i - 1]));
            return m;
        }

        static float RmsStep(float[] s)
        {
            double sum = 0;
            for (int i = 1; i < s.Length; i++) sum += (s[i] - s[i - 1]) * (s[i] - s[i - 1]);
            return (float)Math.Sqrt(sum / (s.Length - 1));
        }

        static float Rms(float[] s, int from, int count)
        {
            double sum = 0;
            for (int i = from; i < from + count; i++) sum += s[i] * s[i];
            return (float)Math.Sqrt(sum / count);
        }

        /// <summary>Brightness: the mean rate of zero crossings in Hz (enough to tell a hiss from a swish).</summary>
        static float Centroid(float[] s)
        {
            int z = 0;
            for (int i = 1; i < s.Length; i++) if ((s[i] >= 0f) != (s[i - 1] >= 0f)) z++;
            return z * 0.5f * MoveSounds.Rate / s.Length;
        }
    }
}
