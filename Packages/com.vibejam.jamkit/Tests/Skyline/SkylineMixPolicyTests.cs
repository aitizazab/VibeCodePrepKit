using System;
using JamKit.Skyline;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    /// <summary>The mix pass's pure half (SkylineMixPolicy.cs): the voice gate, the loop budget and the EQ, with a scripted fight.</summary>
    public class SkylineMixPolicyTests
    {
        static SkylineVoiceGate Gate()
        {
            var g = new SkylineVoiceGate();
            SkylineMixRules.Configure(g);
            return g;
        }

        [Test]
        public void FiveBotsWakingTogetherGiveTwoAlertStingersAndTheNearestWin()
        {
            var g = Gate();
            // five alerts in one frame, the farthest first (audibility = loudness where the listener stands)
            float[] audible = { 0.01f, 0.02f, 0.05f, 0.03f, 0.04f };
            int admitted = 0;
            foreach (var a in audible) if (g.Request("bot.alert", 1f, 0.78f, a).admit) admitted++;
            // 0.01 starts; 0.02 is inside the 0.2 s gap and 2x louder (>1.5x) so it replaces it; 0.05 replaces 0.02; the rest lose to it
            Assert.That(g.ActiveIn("bot.alert", 1.01f), Is.EqualTo(1));
            Assert.That(admitted, Is.EqualTo(3));
            Assert.That(g.Displaced, Is.EqualTo(2));
            Assert.That(g.DroppedGap, Is.EqualTo(2));
        }

        [Test]
        public void AtMostTwoAlertsAtOnceAcrossTheGap()
        {
            var g = Gate();
            Assert.That(g.Request("bot.alert", 0f, 0.78f, 0.05f).admit, Is.True);
            Assert.That(g.Request("bot.alert", 0.25f, 0.78f, 0.04f).admit, Is.True);     // past the gap: a second voice
            var third = g.Request("bot.alert", 0.5f, 0.78f, 0.03f);                        // full, and not louder
            Assert.That(third.admit, Is.False);
            Assert.That(g.ActiveIn("bot.alert", 0.5f), Is.EqualTo(2));
            var louder = g.Request("bot.alert", 0.5f, 0.78f, 0.09f);                       // much nearer: displaces the weakest
            Assert.That(louder.admit, Is.True);
            Assert.That(louder.displaced, Has.Count.EqualTo(1));
            Assert.That(g.ActiveIn("bot.alert", 0.5f), Is.EqualTo(2));
        }

        [Test]
        public void ASlotFreesWhenItsSoundEnds()
        {
            var g = Gate();
            g.Request("bot.alert", 0f, 0.78f, 0.05f);
            g.Request("bot.alert", 0.3f, 0.78f, 0.05f);
            Assert.That(g.Request("bot.alert", 0.5f, 0.78f, 0.05f).admit, Is.False);
            Assert.That(g.Request("bot.alert", 0.9f, 0.78f, 0.05f).admit, Is.True);       // the first has ended at 0.78
        }

        [Test]
        public void ASoundNobodyCanHearDoesNotTakeAVoice()
        {
            var g = Gate();
            Assert.That(g.Request("bot.attack", 0f, 0.4f, 0.001f).admit, Is.False);
            Assert.That(g.DroppedQuiet, Is.EqualTo(1));
            Assert.That(g.ActiveIn("bot.attack", 0f), Is.EqualTo(0));
        }

        [Test]
        public void UnruledGroupsAndTheirNullAreLeftAlone()
        {
            var g = Gate();
            Assert.That(g.Request("player.shot", 0f, 0.4f, 1f).admit, Is.True);
            Assert.That(g.Request(null, 0f, 0.4f, 1f).admit, Is.True);
        }

        [Test]
        public void TheBotFamilyCapsAtSixAcrossGroups()
        {
            var g = Gate();
            string[] groups = { "bot.alert", "bot.telegraph", "bot.attack", "bot.hurt", "bot.break", "bot.minor" };
            int ok = 0;
            foreach (var grp in groups) { if (g.Request(grp, 0f, 1f, 0.05f).admit) ok++; if (g.Request(grp, 0.3f, 1f, 0.05f).admit) ok++; }
            Assert.That(ok, Is.EqualTo(6));   // 12 asked, six bot voices at once
        }

        [Test]
        public void AFailedFamilyCheckPutsBackWhatItDisplaced()
        {
            var g = Gate();
            // six distinct louder bot sounds fill the family; a quiet alert fits its own group but not the family
            for (int i = 0; i < 6; i++) g.Request("bot.attack" + "", 0f + i * 0.0f, 1f, 0.5f);   // attack allows 3
            string[] fill = { "bot.alert", "bot.telegraph", "bot.hurt", "bot.break", "bot.minor" };
            foreach (var grp in fill) g.Request(grp, 0.3f, 1f, 0.5f);
            int before = g.ActiveIn("bot.attack", 0.31f) + g.ActiveIn("bot.alert", 0.31f) + g.ActiveIn("bot.telegraph", 0.31f)
                         + g.ActiveIn("bot.hurt", 0.31f) + g.ActiveIn("bot.break", 0.31f) + g.ActiveIn("bot.minor", 0.31f);
            var v = g.Request("bot.alert", 0.6f, 1f, 0.01f);
            int after = g.ActiveIn("bot.attack", 0.61f) + g.ActiveIn("bot.alert", 0.61f) + g.ActiveIn("bot.telegraph", 0.61f)
                        + g.ActiveIn("bot.hurt", 0.61f) + g.ActiveIn("bot.break", 0.61f) + g.ActiveIn("bot.minor", 0.61f);
            Assert.That(v.admit, Is.False);
            Assert.That(after, Is.EqualTo(before));
        }

        [Test]
        public void OneBarkAtATimeAndTheNearestWins()
        {
            var g = Gate();
            Assert.That(g.Request("bark", 0f, 1.5f, 0.2f).admit, Is.True);
            Assert.That(g.Request("bark", 0.1f, 1.5f, 0.15f).admit, Is.False);   // a quieter one: silent
            Assert.That(g.Request("bark", 0.7f, 1.5f, 0.25f).admit, Is.False);   // louder but not 1.5x
            var near = g.Request("bark", 0.7f, 1.5f, 0.4f);                        // much nearer: it takes over
            Assert.That(near.admit, Is.True);
            Assert.That(near.displaced, Has.Count.EqualTo(1));
            Assert.That(g.ActiveIn("bark", 0.8f), Is.EqualTo(1));
        }

        [Test]
        public void AScriptedFoodCourtFightStaysUnderTheVoiceBudget()
        {
            var g = Gate();
            var rng = new Random(7);
            int asked = 0, played = 0, peakBots = 0, peakBarks = 0;
            string[] kinds = { "bot.attack", "bot.telegraph", "bot.alert", "bot.hurt", "bot.break", "bark" };
            float[] len = { 0.5f, 0.4f, 0.78f, 0.3f, 0.8f, 1.5f };
            for (int step = 0; step < 60 * 30; step++)   // 30 s at 60 fps, eight bots, ~2 sounds a second each
            {
                float now = step / 60f;
                for (int bot = 0; bot < 8; bot++)
                {
                    if (rng.NextDouble() > 2.0 / 60.0) continue;
                    int k = rng.Next(kinds.Length);
                    float audible = 0.02f + 0.1f * (float)rng.NextDouble();
                    asked++;
                    if (g.Request(kinds[k], now, len[k], audible).admit) played++;
                }
                int bots = 0;
                for (int k = 0; k < 5; k++) bots += g.ActiveIn(kinds[k], now);
                peakBots = Math.Max(peakBots, bots);
                peakBarks = Math.Max(peakBarks, g.ActiveIn("bark", now));
            }
            Assert.That(peakBots, Is.LessThanOrEqualTo(6));
            Assert.That(peakBarks, Is.LessThanOrEqualTo(1));
            Assert.That(played, Is.LessThan(asked * 0.75));   // the crowd is thinned out
        }

        [Test]
        public void OnlyTheNearestThreeLoopsAreHeardAndTheRestFadeOut()
        {
            const int n = 32;
            var audible = new float[n];
            var gains = new float[n];
            for (int i = 0; i < n; i++) audible[i] = 0.01f + 0.01f * i;   // loop 31 is nearest
            int on = 0;
            for (int f = 0; f < 120; f++) on = SkylineLoopBudget.Step(audible, gains, n, 3, 1f / 60f, 0.6f);
            Assert.That(on, Is.EqualTo(3));
            int loud = 0;
            for (int i = 0; i < n; i++) if (gains[i] > 0.99f) { loud++; Assert.That(i, Is.GreaterThanOrEqualTo(29)); } else Assert.That(gains[i], Is.EqualTo(0f).Within(1e-6f));
            Assert.That(loud, Is.EqualTo(3));
        }

        [Test]
        public void LoopsFadeInsteadOfCutting()
        {
            var audible = new[] { 1f, 0.5f };
            var gains = new[] { 1f, 0f };
            SkylineLoopBudget.Step(audible, gains, 2, 1, 0.1f, 0.6f);
            Assert.That(gains[0], Is.EqualTo(1f));
            audible[0] = 0f; audible[1] = 1f;   // the first goes far away
            SkylineLoopBudget.Step(audible, gains, 2, 1, 0.1f, 0.6f);
            Assert.That(gains[0], Is.EqualTo(1f - 0.1f / 0.6f).Within(1e-5f));
            Assert.That(gains[1], Is.EqualTo(0.1f / 0.6f).Within(1e-5f));
        }

        [Test]
        public void TwoLoopsOfAboutTheSameLoudnessDoNotTradePlaces()
        {
            var audible = new[] { 1f, 1.1f };
            var gains = new[] { 1f, 0f };
            for (int f = 0; f < 100; f++) SkylineLoopBudget.Step(audible, gains, 2, 1, 1f / 60f, 0.6f);
            Assert.That(gains[0], Is.EqualTo(1f));   // 10% louder is inside the stickiness margin
            Assert.That(gains[1], Is.EqualTo(0f));
        }

        // ---------------------------------------------------------------- the EQ

        static float RmsAt(Func<float, float> process, double hz, int sr = 48000)
        {
            double sum = 0; int n = sr;
            for (int i = 0; i < n; i++)
            {
                float y = process((float)Math.Sin(2 * Math.PI * hz * i / sr));
                if (i > sr / 2) sum += y * y;
            }
            return (float)Math.Sqrt(sum / (n / 2) * 2);   // a unit sine is 1/sqrt(2) RMS; normalise to 1
        }

        static double Db(double ratio) => 20 * Math.Log10(ratio);

        [Test]
        public void ThePeakingCutTakesTheHarshBandDownAndLeavesTheLowsAlone()
        {
            var f = new SkylineBiquad();
            f.SetPeaking(48000, 3400f, 0.6f, -6f);
            Assert.That(Db(RmsAt(f.Process, 3400)), Is.EqualTo(-6.0).Within(0.3));
            f.Reset();
            Assert.That(Db(RmsAt(f.Process, 2000)), Is.LessThan(-3.0));
            f.Reset();
            Assert.That(Db(RmsAt(f.Process, 5000)), Is.LessThan(-3.0));
            f.Reset();
            Assert.That(Db(RmsAt(f.Process, 300)), Is.GreaterThan(-0.7));
            f.Reset();
            Assert.That(Db(RmsAt(f.Process, 12000)), Is.GreaterThan(-1.5));
        }

        [Test]
        public void AZeroCutIsFlat()
        {
            var f = new SkylineBiquad();
            f.SetPeaking(48000, 3400f, 0.6f, 0f);
            Assert.That(f.IsFlat, Is.True);
            Assert.That(f.Process(0.5f), Is.EqualTo(0.5f));
        }

        [Test]
        public void TheLowPassTakesTheBabblesUpperHarmonicsDown()
        {
            var f = new SkylineBiquad();
            f.SetLowPass(44100, 3000f);
            Assert.That(Db(RmsAt(f.Process, 500, 44100)), Is.GreaterThan(-0.2));
            f.Reset();
            Assert.That(Db(RmsAt(f.Process, 3000, 44100)), Is.EqualTo(-3.0).Within(0.4));
            f.Reset();
            Assert.That(Db(RmsAt(f.Process, 6000, 44100)), Is.LessThan(-11.0));
        }
    }
}
