using System;
using JamKit.Movement;
using NUnit.Framework;

namespace JamKit.Tests.Movement
{
    /// <summary>The speed audio (docs/SPEED-FEEL.md A1, A2, A4, A5, A6): stems chosen by speed tier, switched only on
    /// bar lines, fading in over 1 s and out over 2 s; the placeholder stems one length and seamless; the muffled
    /// copies; the opening by speed; the duck; the breath's exertion.</summary>
    public class SpeedMusicTests
    {
        const float Dt = 1f / 60f;

        /// <summary>Runs the mixer for some seconds at a tempo from a beat position; returns the end position.</summary>
        static double Run(StemMixer m, double beat, int tier, float seconds, float bpm = 104f)
        {
            for (float t = 0f; t < seconds; t += Dt)
            {
                beat += Dt * bpm / 60.0;
                m.Tick(beat, tier, Dt);
            }
            return beat;
        }

        [Test]
        public void TheTierPicksTheStems()
        {
            Assert.That(StemMixer.Wants(StemMixer.SlotTiers[StemMixer.Base], SpeedTier.Running), Is.True, "base always");
            Assert.That(StemMixer.Wants(StemMixer.SlotTiers[StemMixer.Drive], SpeedTier.Running), Is.False);
            Assert.That(StemMixer.Wants(StemMixer.SlotTiers[StemMixer.Drive], SpeedTier.Fast), Is.True, "fast unmutes drive");
            Assert.That(StemMixer.Wants(StemMixer.SlotTiers[StemMixer.Peak], SpeedTier.Fast), Is.False);
            Assert.That(StemMixer.Wants(StemMixer.SlotTiers[StemMixer.Peak], SpeedTier.Flying), Is.True, "flying unmutes peak");
            Assert.That(StemMixer.Wants(StemMixer.SlotTiers[StemMixer.Tension], SpeedTier.Flying), Is.False, "tension is never speed's");

            var m = new StemMixer(new StemMixDef());
            double beat = Run(m, 0.0, SpeedTier.Running, 1f);
            Assert.That(m.ActiveCount, Is.EqualTo(1));
            beat = Run(m, beat, SpeedTier.Fast, 3f);
            Assert.That(m.ActiveCount, Is.EqualTo(2));
            Run(m, beat, SpeedTier.Flying, 3f);
            Assert.That(m.ActiveCount, Is.EqualTo(3));
            Assert.That(m.IsOn(StemMixer.Peak), Is.True);
        }

        [Test]
        public void StemsSwitchOnlyOnBarLines()
        {
            var m = new StemMixer(new StemMixDef { quantizeBeats = 4 });
            m.Tick(0.0, SpeedTier.Running, Dt);
            // Fast from beat 1.3: nothing until beat 4.
            double beat = 1.3;
            bool switchedEarly = false;
            while (beat < 3.99)
            {
                beat += 0.05;
                if (m.Tick(Math.Min(beat, 3.99), SpeedTier.Fast, Dt)) switchedEarly = true;
            }
            Assert.That(switchedEarly, Is.False, "no switch inside the bar");
            Assert.That(m.IsOn(StemMixer.Drive), Is.False);
            Assert.That(m.Tick(4.02, SpeedTier.Fast, Dt), Is.True, "switches as the music crosses the bar line");
            Assert.That(m.IsOn(StemMixer.Drive), Is.True);
            Assert.That(m.LastSwitchLine, Is.EqualTo(4.0));
            Assert.That(m.LastSwitchAt, Is.EqualTo(4.02).Within(1e-9));
            // Back to running mid-bar: the drive stays on to the next line (8), then goes.
            Assert.That(m.Tick(6.0, SpeedTier.Running, Dt), Is.False);
            Assert.That(m.IsOn(StemMixer.Drive), Is.True);
            Assert.That(m.Tick(8.1, SpeedTier.Running, Dt), Is.True);
            Assert.That(m.IsOn(StemMixer.Drive), Is.False);
            Assert.That(m.SwitchCount, Is.EqualTo(2));
            // Quantised to the beat instead: switches at the next beat line.
            var b = new StemMixer(new StemMixDef { quantizeBeats = 1 });
            b.Tick(0.2, SpeedTier.Running, Dt);
            Assert.That(b.Tick(0.9, SpeedTier.Flying, Dt), Is.False);
            Assert.That(b.Tick(1.05, SpeedTier.Flying, Dt), Is.True);
            Assert.That(b.LastSwitchLine, Is.EqualTo(1.0));
        }

        [Test]
        public void StemsFadeInOverOneSecondAndOutOverTwo()
        {
            var m = new StemMixer(new StemMixDef { fadeIn = 1f, fadeOut = 2f, quantizeBeats = 4 });
            // Start on a bar line at fast: the drive is on from the first tick.
            m.Tick(0.0, SpeedTier.Fast, 0f);
            double beat = 0.0;
            beat = Run(m, beat, SpeedTier.Fast, 0.5f);
            Assert.That(m.Gain(StemMixer.Drive), Is.EqualTo(0.5f).Within(0.03f), "half way in after 0.5 s");
            beat = Run(m, beat, SpeedTier.Fast, 0.52f);
            Assert.That(m.Gain(StemMixer.Drive), Is.EqualTo(1f), "fully in after 1 s");
            // Drop to running; wait for the next bar line, then time the fade out.
            while (m.IsOn(StemMixer.Drive)) { beat += Dt * 104.0 / 60.0; m.Tick(beat, SpeedTier.Running, Dt); }
            float g0 = m.Gain(StemMixer.Drive);
            beat = Run(m, beat, SpeedTier.Running, 1f);
            Assert.That(m.Gain(StemMixer.Drive), Is.EqualTo(g0 - 0.5f).Within(0.03f), "half way out after 1 s");
            Run(m, beat, SpeedTier.Running, 1.05f);
            Assert.That(m.Gain(StemMixer.Drive), Is.EqualTo(0f), "fully out after 2 s");
        }

        [Test]
        public void AFloorHoldsAStemUpWhateverTheTier()
        {
            var m = new StemMixer(new StemMixDef());
            m.SetFloor(StemMixer.Tension, 0.6f);
            Run(m, 0.0, SpeedTier.Running, 2f);
            Assert.That(m.Gain(StemMixer.Tension), Is.EqualTo(0.6f).Within(1e-4f));
            Assert.That(m.IsOn(StemMixer.Tension), Is.False, "a floor is not a switch");
            m.SetFloor(StemMixer.Tension, 3f);
            Assert.That(m.Floor(StemMixer.Tension), Is.EqualTo(1f));
        }

        [Test]
        public void ThePlaceholderStemsAreOneLengthSeamlessAndUnderMinusOneDbfs()
        {
            int len = MoveMusicSynth.LoopSamples;
            Assert.That(len, Is.EqualTo((int)(60f / 104f * 4 * 4 * 32000)), "104 bpm, 4 bars (the escape's chase loop length)");
            for (int slot = StemMixer.Base; slot <= StemMixer.Peak; slot++)
            {
                var s = MoveMusicSynth.Make(slot);
                Assert.That(s, Is.Not.Null);
                Assert.That(s.Length, Is.EqualTo(len), StemMixer.SlotNames[slot]);
                float pk = MoveSounds.Peak(s);
                Assert.That(pk, Is.LessThan(MoveSounds.MaxPeak));
                Assert.That(pk, Is.GreaterThan(0.3f));
                double sum = 0;
                for (int i = 1; i < s.Length; i++) sum += (s[i] - s[i - 1]) * (s[i] - s[i - 1]);
                float rmsStep = (float)Math.Sqrt(sum / (s.Length - 1));
                Assert.That(Math.Abs(s[0] - s[s.Length - 1]), Is.LessThanOrEqualTo(4f * rmsStep), StemMixer.SlotNames[slot] + " wraps without a seam");
            }
            Assert.That(MoveMusicSynth.Make(StemMixer.Tension), Is.Null);
        }

        [Test]
        public void TheMuffledCopyIsDarkerKeepsItsLengthAndHasNoSeam()
        {
            var s = MoveMusicSynth.Make(StemMixer.Peak);
            var m = (float[])s.Clone();
            StemMixer.LowPassZeroPhase(m, MoveMusicSynth.Rate, 600f);
            Assert.That(m.Length, Is.EqualTo(s.Length));
            Assert.That(Brightness(m), Is.LessThan(Brightness(s) * 0.7f), "darker");
            Assert.That(Math.Abs(m[0] - m[m.Length - 1]), Is.LessThan(0.01f), "no seam");
            Assert.That(MoveSounds.Peak(m), Is.LessThan(MoveSounds.MaxPeak));
            // Zero phase: the muffled copy lines up with the original (a crossfade doesn't comb).
            var sine = new float[3200];
            for (int i = 0; i < sine.Length; i++) sine[i] = (float)Math.Sin(2.0 * Math.PI * 10 * i / sine.Length); // 100 Hz
            var lp = (float[])sine.Clone();
            StemMixer.LowPassZeroPhase(lp, 32000, 600f);
            int peakAt = 0, lpPeakAt = 0;
            for (int i = 0; i < 320; i++) { if (sine[i] > sine[peakAt]) peakAt = i; if (lp[i] > lp[lpPeakAt]) lpPeakAt = i; }
            Assert.That(Math.Abs(peakAt - lpPeakAt), Is.LessThanOrEqualTo(1));
        }

        [Test]
        public void TheMusicOpensWithSpeed()
        {
            Assert.That(StemMixer.Open(0f, 13f, 0f), Is.EqualTo(0f), "muffled at a standstill");
            Assert.That(StemMixer.Open(13f, 13f, 0f), Is.EqualTo(1f), "full at flying speed");
            Assert.That(StemMixer.Open(9f, 13f, 1f), Is.EqualTo(1f), "held full while the flying tier lasts");
            float last = -1f;
            for (float v = 0f; v <= 22f; v += 0.5f)
            {
                float o = StemMixer.Open(v, 13f, 0f);
                Assert.That(o, Is.GreaterThanOrEqualTo(last));
                last = o;
            }
        }

        [Test]
        public void FlyingDucksTheWorldByTheTunedAmount()
        {
            Assert.That(MoveSoundMix.Duck(0f, 0.45f), Is.EqualTo(1f), "no duck below flying");
            Assert.That(MoveSoundMix.Duck(1f, 0.45f), Is.EqualTo(0.55f).Within(1e-5f));
            Assert.That(MoveSoundMix.Duck(0.5f, 0.45f), Is.EqualTo(0.775f).Within(1e-5f), "smooth with the flying blend");
            Assert.That(MoveSoundMix.Duck(1f, 0f), Is.EqualTo(1f), "tunable to off");
            Assert.That(MoveSoundMix.Duck(1f, 2f), Is.EqualTo(0f));
        }

        [Test]
        public void TheBreathQuickensWithTimeSpentFast()
        {
            float e = 0f;
            for (int i = 0; i < 120; i++) e = MoveSoundMix.Exertion(e, SpeedTier.Running, Dt, 8f, 10f);
            Assert.That(e, Is.EqualTo(0f));
            for (int i = 0; i < 240; i++) e = MoveSoundMix.Exertion(e, SpeedTier.Fast, Dt, 8f, 10f);
            Assert.That(e, Is.EqualTo(0.25f).Within(0.01f), "4 s fast = a quarter");
            for (int i = 0; i < 240; i++) e = MoveSoundMix.Exertion(e, SpeedTier.Flying, Dt, 8f, 10f);
            Assert.That(e, Is.EqualTo(0.75f).Within(0.01f), "flying builds twice as fast");
            for (int i = 0; i < 300; i++) e = MoveSoundMix.Exertion(e, SpeedTier.Running, Dt, 8f, 10f);
            Assert.That(e, Is.EqualTo(0.25f).Within(0.01f), "recovers over 10 s");
            Assert.That(MoveSoundMix.SupersonicPitch(22f, 13f), Is.GreaterThan(MoveSoundMix.SupersonicPitch(13f, 13f)));
        }

        /// <summary>How bright a sound is: the RMS of its sample-to-sample steps over its RMS (grows with frequency).</summary>
        static double Brightness(float[] s)
        {
            double d = 0, a = 0;
            for (int i = 1; i < s.Length; i++) { d += (s[i] - s[i - 1]) * (s[i] - s[i - 1]); a += s[i] * s[i]; }
            return Math.Sqrt(d / a);
        }
    }
}
