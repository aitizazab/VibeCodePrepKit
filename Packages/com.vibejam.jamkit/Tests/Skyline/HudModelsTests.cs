using System;
using System.Numerics;
using JamKit.Skyline;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    /// <summary>Task V1: the HUD models and the wreck sequence's timing (docs/SKYLINE-PLAN-FLOOR1.md).</summary>
    public class HudModelsTests
    {
        // ---- health bar ----

        [Test]
        public void TheBarStartsFullAndQuiet()
        {
            var m = new HealthBarModel(100f, 50f);
            Assert.That(m.ChassisFrac, Is.EqualTo(1f));
            Assert.That(m.PlatingFrac, Is.EqualTo(1f));
            Assert.That(m.LowPulse, Is.EqualTo(0f));
            Assert.That(m.EdgeTint, Is.EqualTo(0f));
            Assert.That(m.ShatterEvents, Is.EqualTo(0));
        }

        [Test]
        public void ThePulseRunsOnlyBelowThirtyFivePercentChassis()
        {
            var m = new HealthBarModel(100f, 50f);
            m.Set(36f, 50f);
            for (int i = 0; i < 40; i++) { m.Tick(0.02f); Assert.That(m.LowPulse, Is.EqualTo(0f)); }
            m.Set(34f, 50f);
            float max = 0f, min = 1f;
            for (int i = 0; i < 100; i++)
            {
                m.Tick(0.01f);
                max = Math.Max(max, m.LowPulse); min = Math.Min(min, m.LowPulse);
                Assert.That(m.LowPulse, Is.InRange(0f, 1f));
                Assert.That(m.EdgeTint, Is.EqualTo(m.LowPulse));
            }
            Assert.That(max, Is.GreaterThan(0.95f));
            Assert.That(min, Is.LessThan(0.05f));
            m.Set(80f, 50f);
            m.Tick(0.1f);
            Assert.That(m.LowPulse, Is.EqualTo(0f));
        }

        [Test]
        public void ThePulseRateIsOnePointSixHertz()
        {
            var m = new HealthBarModel(100f, 0f);
            m.Set(10f, 0f);
            m.Tick(0.5f / HealthBarModel.PulseHz);          // half a cycle: the peak
            Assert.That(m.LowPulse, Is.EqualTo(1f).Within(0.001f));
            m.Tick(0.5f / HealthBarModel.PulseHz);          // a full cycle: back to the trough
            Assert.That(m.LowPulse, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void ThePulseIsTheSameHoweverTheTimeIsCut()
        {
            var a = new HealthBarModel(100f, 0f);
            var b = new HealthBarModel(100f, 0f);
            a.Set(10f, 0f); b.Set(10f, 0f);
            a.Tick(0.3f);
            for (int i = 0; i < 30; i++) b.Tick(0.01f);
            Assert.That(a.LowPulse, Is.EqualTo(b.LowPulse).Within(0.001f));
        }

        [Test]
        public void ThePlatingShattersOncePerBreakAndAgainAfterARefill()
        {
            var m = new HealthBarModel(100f, 50f);
            m.Set(100f, 20f);
            Assert.That(m.ShatterEvents, Is.EqualTo(0));
            m.Set(100f, 0f);
            Assert.That(m.ShatterEvents, Is.EqualTo(1));
            m.Set(90f, 0f); m.Set(80f, 0f);                 // chassis hits do not shatter anything
            Assert.That(m.ShatterEvents, Is.EqualTo(1));
            m.Set(80f, 50f);                                 // refilled
            Assert.That(m.ShatterEvents, Is.EqualTo(1));
            m.Set(80f, 0f);
            Assert.That(m.ShatterEvents, Is.EqualTo(2));
        }

        [Test]
        public void ABarWithNoPlatingNeverShatters()
        {
            var m = new HealthBarModel(100f, 0f);
            m.Set(50f, 0f); m.Set(50f, 10f); m.Set(50f, 0f);
            Assert.That(m.ShatterEvents, Is.EqualTo(0));
            Assert.That(m.PlatingFrac, Is.EqualTo(0f));
        }

        [Test]
        public void SetClampsOutOfRangeAndNaN()
        {
            var m = new HealthBarModel(100f, 50f);
            m.Set(250f, -3f);
            Assert.That(m.ChassisFrac, Is.EqualTo(1f));
            Assert.That(m.PlatingFrac, Is.EqualTo(0f));
            m.Set(float.NaN, float.NaN);
            Assert.That(m.ChassisFrac, Is.EqualTo(0f));
            Assert.That(m.PlatingFrac, Is.EqualTo(0f));
            m.Tick(float.NaN);
            Assert.That(float.IsNaN(m.LowPulse), Is.False);
            var broken = new HealthBarModel(0f, float.NaN);
            broken.Set(5f, 5f);
            Assert.That(broken.ChassisFrac, Is.EqualTo(0f));
            Assert.That(broken.PlatingFrac, Is.EqualTo(0f));
        }

        // ---- edge marker ----

        static readonly Vector3 Cam = new Vector3(0f, 1.7f, 0f);
        static readonly Vector3 Fwd = new Vector3(0f, 0f, 1f);
        static readonly Vector3 Up = new Vector3(0f, 1f, 0f);
        static EdgeMarkerResult At(Vector3 p, float margin = 0.06f) => EdgeMarker.Compute(Cam, Fwd, Up, 60f, 16f / 9f, p, margin);

        [Test]
        public void APointDeadAheadIsOnScreenAtTheCentre()
        {
            var r = At(new Vector3(0f, 1.7f, 20f));
            Assert.That(r.onScreen, Is.True);
            Assert.That(r.behind, Is.False);
            Assert.That(r.x01, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(r.y01, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(r.distance, Is.EqualTo(20f).Within(1e-4f));
        }

        [Test]
        public void APointInViewOffCentreIsAtItsOwnPlace()
        {
            // tan(30 deg) * 16/9 is the half-width ratio: a point at x = 0.5 * that * z sits 3/4 of the way across
            float half = (float)Math.Tan(30.0 * Math.PI / 180.0) * 16f / 9f;
            var r = At(new Vector3(0.5f * half * 10f, 1.7f, 10f));
            Assert.That(r.onScreen, Is.True);
            Assert.That(r.x01, Is.EqualTo(0.75f).Within(1e-3f));
            Assert.That(r.y01, Is.EqualTo(0.5f).Within(1e-3f));
        }

        [Test]
        public void APointAboveIsHigherOnTheScreen()
        {
            Assert.That(At(new Vector3(0f, 4f, 20f)).y01, Is.GreaterThan(0.5f));
            Assert.That(At(new Vector3(0f, 0f, 20f)).y01, Is.LessThan(0.5f));
        }

        [Test]
        public void APointToTheRightOffScreenClampsToTheRightMarginWithAngleZero()
        {
            var r = At(new Vector3(30f, 1.7f, 5f), 0.06f);
            Assert.That(r.onScreen, Is.False);
            Assert.That(r.behind, Is.False);
            Assert.That(r.x01, Is.EqualTo(1f - 0.06f).Within(1e-4f));
            Assert.That(r.y01, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(r.angleRad, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(r.distance, Is.EqualTo((float)Math.Sqrt(30f * 30f + 25f)).Within(1e-3f));
        }

        [Test]
        public void APointToTheLeftOffScreenPointsLeft()
        {
            var r = At(new Vector3(-30f, 1.7f, 5f));
            Assert.That(r.onScreen, Is.False);
            Assert.That(r.x01, Is.EqualTo(0.06f).Within(1e-4f));
            Assert.That(Math.Abs(r.angleRad), Is.EqualTo((float)Math.PI).Within(1e-3f));
        }

        [Test]
        public void APointBehindClampsToTheRingAndSaysSo()
        {
            var r = At(new Vector3(8f, 1.7f, -10f));
            Assert.That(r.behind, Is.True);
            Assert.That(r.onScreen, Is.False);
            Assert.That(r.x01, Is.InRange(0.06f - 1e-4f, 0.94f + 1e-4f));
            Assert.That(r.y01, Is.InRange(0.06f - 1e-4f, 0.94f + 1e-4f));
            bool onRing = Math.Abs(r.x01 - 0.94f) < 1e-3f || Math.Abs(r.x01 - 0.06f) < 1e-3f
                          || Math.Abs(r.y01 - 0.94f) < 1e-3f || Math.Abs(r.y01 - 0.06f) < 1e-3f;
            Assert.That(onRing, Is.True);
            Assert.That(r.x01, Is.GreaterThan(0.5f));        // it is to the right
            Assert.That(r.distance, Is.EqualTo((float)Math.Sqrt(64f + 100f)).Within(1e-3f));
        }

        [Test]
        public void APointStraightBehindPointsDownAndNeverReturnsNaN()
        {
            var r = At(new Vector3(0f, 1.7f, -10f));
            Assert.That(r.behind, Is.True);
            Assert.That(r.x01, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(r.y01, Is.EqualTo(0.06f).Within(1e-4f));
            Assert.That(r.angleRad, Is.EqualTo(-(float)Math.PI / 2f).Within(1e-4f));
            var atCam = At(Cam);
            Assert.That(float.IsNaN(atCam.x01) || float.IsNaN(atCam.y01) || float.IsNaN(atCam.angleRad), Is.False);
        }

        [Test]
        public void TheProjectionFollowsTheCameraTurning()
        {
            // facing east, the point north of the camera is on the left
            var r = EdgeMarker.Compute(Cam, new Vector3(1f, 0f, 0f), Up, 60f, 16f / 9f, new Vector3(0f, 1.7f, 30f));
            Assert.That(r.onScreen, Is.False);
            Assert.That(r.x01, Is.LessThan(0.5f));
        }

        // ---- subtitles ----

        [Test]
        public void AThirdLinePushesOutTheOldest()
        {
            var b = new SubtitleBuffer();
            b.Say("DOT", "one", false, false, 0f, 10f, SubtitleMode.Dialogue);
            b.Say("NAN", "two", false, false, 1f, 10f, SubtitleMode.Dialogue);
            b.Say("DOT", "three", false, false, 2f, 10f, SubtitleMode.Dialogue);
            var v = b.Visible(2.5f);
            Assert.That(v.Count, Is.EqualTo(2));
            Assert.That(v[0].text, Is.EqualTo("two"));
            Assert.That(v[1].text, Is.EqualTo("three"));
        }

        [Test]
        public void ExpiredLinesVanish()
        {
            var b = new SubtitleBuffer();
            b.Say("DOT", "short", false, false, 0f, 2f, SubtitleMode.Dialogue);
            b.Say("NAN", "long", false, false, 0f, 6f, SubtitleMode.Dialogue);
            Assert.That(b.Visible(1.9f).Count, Is.EqualTo(2));
            var v = b.Visible(2f);
            Assert.That(v.Count, Is.EqualTo(1));
            Assert.That(v[0].text, Is.EqualTo("long"));
            Assert.That(b.Visible(6f).Count, Is.EqualTo(0));
        }

        [Test]
        public void AnExpiredLineDoesNotCountAgainstTheTwoLineLimit()
        {
            var b = new SubtitleBuffer();
            b.Say("DOT", "a", false, false, 0f, 1f, SubtitleMode.Dialogue);
            b.Say("NAN", "b", false, false, 0f, 10f, SubtitleMode.Dialogue);
            b.Say("DOT", "c", false, false, 5f, 10f, SubtitleMode.Dialogue);
            var v = b.Visible(5f);
            Assert.That(v.Count, Is.EqualTo(2));
            Assert.That(v[0].text, Is.EqualTo("b"));
        }

        [Test]
        public void ABarkIsDroppedUnderDialogueAndKeptUnderBarks()
        {
            var b = new SubtitleBuffer();
            b.Say("", "BZZT", false, true, 0f, 3f, SubtitleMode.Dialogue);
            Assert.That(b.Visible(0.1f).Count, Is.EqualTo(0));
            b.Say("", "BZZT", false, true, 0f, 3f, SubtitleMode.DialogueAndBarks);
            Assert.That(b.Visible(0.1f).Count, Is.EqualTo(1));
        }

        [Test]
        public void OffShowsNothingNotEvenDialogue()
        {
            var b = new SubtitleBuffer();
            b.Say("DOT", "hello", false, false, 0f, 5f, SubtitleMode.Off);
            b.Say("", "grr", false, true, 0f, 5f, SubtitleMode.Off);
            Assert.That(b.Visible(0.1f).Count, Is.EqualTo(0));
        }

        [Test]
        public void PaLinesAreFlaggedForItalics()
        {
            var b = new SubtitleBuffer();
            b.Say("CONCIERGE", "Wave incoming.", true, false, 0f, 4f, SubtitleMode.Dialogue);
            b.Say("DOT", "Clamped!", false, false, 0f, 4f, SubtitleMode.Dialogue);
            var v = b.Visible(1f);
            Assert.That(v[0].pa, Is.True);
            Assert.That(v[1].pa, Is.False);
        }

        [Test]
        public void EmptyTextAndNullSpeakersAreHandled()
        {
            var b = new SubtitleBuffer();
            b.Say("DOT", "", false, false, 0f, 4f, SubtitleMode.Dialogue);
            b.Say("DOT", null, false, false, 0f, 4f, SubtitleMode.Dialogue);
            b.Say(null, "hi", false, false, 0f, 4f, SubtitleMode.Dialogue);
            var v = b.Visible(1f);
            Assert.That(v.Count, Is.EqualTo(1));
            Assert.That(v[0].speaker, Is.EqualTo(""));
            b.Clear();
            Assert.That(b.Visible(1f).Count, Is.EqualTo(0));
        }

        [Test]
        public void TheSizesAreTwentyEightThirtyFourFortyTwo()
        {
            Assert.That(SubtitleBuffer.FontPx(SubtitleSize.Small), Is.EqualTo(28f));
            Assert.That(SubtitleBuffer.FontPx(SubtitleSize.Medium), Is.EqualTo(34f));
            Assert.That(SubtitleBuffer.FontPx(SubtitleSize.Large), Is.EqualTo(42f));
        }

        // ---- formats ----

        [TestCase(21f, "NEXT 0:21")]
        [TestCase(20.2f, "NEXT 0:21")]
        [TestCase(5f, "NEXT 0:05")]
        [TestCase(0.01f, "NEXT 0:01")]
        [TestCase(0f, "NEXT 0:00")]
        [TestCase(-3f, "NEXT 0:00")]
        [TestCase(float.NaN, "NEXT 0:00")]
        [TestCase(30f, "NEXT 0:30")]
        [TestCase(75f, "NEXT 1:15")]
        public void TheWaveClockText(float seconds, string expected)
        {
            Assert.That(HudText.WaveClock(seconds), Is.EqualTo(expected));
        }

        [TestCase(34f, "34 m")]
        [TestCase(33.6f, "34 m")]
        [TestCase(0.2f, "0 m")]
        [TestCase(-5f, "0 m")]
        [TestCase(float.NaN, "0 m")]
        [TestCase(120f, "120 m")]
        public void TheDistanceText(float metres, string expected)
        {
            Assert.That(HudText.Distance(metres), Is.EqualTo(expected));
        }

        // ---- speaker colours ----

        static float HueDeg(float r, float g, float b)
        {
            float max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), d = max - min;
            if (d < 1e-6f) return 0f;
            float h;
            if (max == r) h = ((g - b) / d) % 6f;
            else if (max == g) h = (b - r) / d + 2f;
            else h = (r - g) / d + 4f;
            h *= 60f;
            return h < 0f ? h + 360f : h;
        }

        [TestCase("DOT")]
        [TestCase("NAN")]
        [TestCase("CONCIERGE")]
        [TestCase("dot")]
        [TestCase("somebody else")]
        [TestCase(null)]
        public void NoSpeakerColourLiesInTheHostileBand(string who)
        {
            var (r, g, b) = SpeakerColours.Of(who);
            Assert.That(r, Is.InRange(0f, 1f)); Assert.That(g, Is.InRange(0f, 1f)); Assert.That(b, Is.InRange(0f, 1f));
            float hostile = HueDeg(0xFF / 255f, 0x2E / 255f, 0x63 / 255f);
            float max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
            float sat = max <= 0f ? 0f : (max - min) / max;
            if (sat < 0.15f) return;                        // greys and white have no hue to clash
            float diff = Math.Abs(HueDeg(r, g, b) - hostile);
            if (diff > 180f) diff = 360f - diff;
            Assert.That(diff, Is.GreaterThan(20f));
        }

        [Test]
        public void TheCrewHaveDistinctColours()
        {
            Assert.That(SpeakerColours.Of("DOT"), Is.Not.EqualTo(SpeakerColours.Of("NAN")));
            Assert.That(SpeakerColours.Of("DOT"), Is.Not.EqualTo(SpeakerColours.Of("CONCIERGE")));
            Assert.That(SpeakerColours.Of("NAN"), Is.Not.EqualTo(SpeakerColours.Of("CONCIERGE")));
        }

        // ---- the wreck sequence ----

        [Test]
        public void TheQuoteHoldScalesWithLengthAndClamps()
        {
            Assert.That(WreckSequence.QuoteHold(0), Is.EqualTo(WreckSequence.HoldMin));
            Assert.That(WreckSequence.QuoteHold(-5), Is.EqualTo(WreckSequence.HoldMin));
            Assert.That(WreckSequence.QuoteHold(40), Is.EqualTo(1.5f + 0.06f * 40f).Within(1e-4f));
            Assert.That(WreckSequence.QuoteHold(80), Is.GreaterThan(WreckSequence.QuoteHold(40)));
            Assert.That(WreckSequence.QuoteHold(10000), Is.EqualTo(WreckSequence.HoldMax));
        }

        [Test]
        public void ALongerQuoteMakesALongerSequenceByTheSameMargin()
        {
            var a = new WreckSequence(30);
            var b = new WreckSequence(60);
            Assert.That(b.TotalSeconds - a.TotalSeconds, Is.EqualTo(0.06f * 30f).Within(1e-3f));
            Assert.That(new WreckSequence("abc").QuoteLength, Is.EqualTo(3));
            Assert.That(new WreckSequence((string)null).QuoteLength, Is.EqualTo(0));
        }

        [Test]
        public void ThePhasesRunInOrder()
        {
            var s = new WreckSequence(50);
            var order = new[]
            {
                WreckPhase.Stagger, WreckPhase.Fall, WreckPhase.Lie, WreckPhase.FadeOut, WreckPhase.Quote, WreckPhase.FadeIn, WreckPhase.Done,
            };
            int at = 0;
            for (float t = 0f; t <= s.TotalSeconds + 1f; t += 0.01f)
            {
                var ph = s.Sample(t).phase;
                if (ph != order[at])
                {
                    Assert.That(ph, Is.EqualTo(order[at + 1]), "skipped a phase at " + t);
                    at++;
                }
            }
            Assert.That(order[at], Is.EqualTo(WreckPhase.Done));
        }

        [Test]
        public void TheBodyStaggersThenFallsThenLies()
        {
            var s = new WreckSequence(50);
            var start = s.Sample(0f);
            Assert.That(start.stagger01, Is.EqualTo(0f));
            Assert.That(start.fall01, Is.EqualTo(0f));
            var stagger = s.Sample(WreckSequence.StaggerSeconds * 0.5f);
            Assert.That(stagger.stagger01, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(stagger.fall01, Is.EqualTo(0f));
            var fall = s.Sample(s.FallAt + WreckSequence.FallSeconds * 0.5f);
            Assert.That(fall.stagger01, Is.EqualTo(1f));
            Assert.That(fall.fall01, Is.EqualTo(0.5f).Within(1e-4f));
            var lie = s.Sample(s.LieAt + 0.1f);
            Assert.That(lie.fall01, Is.EqualTo(1f));
            Assert.That(lie.blackAlpha, Is.EqualTo(0f));
        }

        [Test]
        public void TheScreenFadesToBlackBeforeTheQuoteAndNeverBlinksBack()
        {
            var s = new WreckSequence(50);
            float prev = 0f;
            for (float t = 0f; t < s.FadeInAt; t += 0.01f)
            {
                float a = s.Sample(t).blackAlpha;
                Assert.That(a, Is.GreaterThanOrEqualTo(prev - 1e-5f), "black alpha dipped at " + t);
                prev = a;
            }
            Assert.That(s.Sample(s.QuoteAt).blackAlpha, Is.EqualTo(1f));
            Assert.That(s.Sample(s.FadeOutAt).blackAlpha, Is.EqualTo(0f));
        }

        [Test]
        public void TheQuoteShowsOnlyOverBlackAndFadesInAndOut()
        {
            var s = new WreckSequence(60);
            for (float t = 0f; t < s.QuoteAt; t += 0.05f) Assert.That(s.Sample(t).quoteAlpha, Is.EqualTo(0f));
            Assert.That(s.Sample(s.QuoteAt).quoteAlpha, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(s.Sample(s.QuoteAt + WreckSequence.QuoteFadeSeconds).quoteAlpha, Is.EqualTo(1f).Within(1e-4f));
            float mid = s.QuoteAt + s.QuoteHoldSeconds * 0.5f;
            Assert.That(s.Sample(mid).quoteAlpha, Is.EqualTo(1f));
            Assert.That(s.Sample(s.FadeInAt - 0.01f).quoteAlpha, Is.LessThan(0.1f));
            Assert.That(s.Sample(s.FadeInAt + 0.1f).quoteAlpha, Is.EqualTo(0f));
            // fully readable for the hold minus the two fades
            float readable = 0f;
            for (float t = s.QuoteAt; t < s.FadeInAt; t += 0.01f) if (s.Sample(t).quoteAlpha >= 0.999f) readable += 0.01f;
            Assert.That(readable, Is.EqualTo(s.QuoteHoldSeconds - 2f * WreckSequence.QuoteFadeSeconds).Within(0.1f));
        }

        [Test]
        public void TheWorldMayResetOnlyBehindBlackAndTheLiftRideStartsWithTheFadeIn()
        {
            var s = new WreckSequence(50);
            Assert.That(s.Sample(s.FadeOutAt + 0.1f).worldMayReset, Is.False);
            Assert.That(s.Sample(s.QuoteAt).worldMayReset, Is.True);
            Assert.That(s.Sample(s.QuoteAt).blackAlpha, Is.EqualTo(1f));
            Assert.That(s.Sample(s.FadeInAt - 0.01f).liftRideRunning, Is.False);
            Assert.That(s.Sample(s.FadeInAt).liftRideRunning, Is.True);
            var half = s.Sample(s.FadeInAt + WreckSequence.FadeInSeconds * 0.5f);
            Assert.That(half.blackAlpha, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(s.Sample(s.TotalSeconds).phase, Is.EqualTo(WreckPhase.Done));
            Assert.That(s.Sample(s.TotalSeconds).blackAlpha, Is.EqualTo(0f));
        }

        [Test]
        public void SamplingOutOfRangeIsSafe()
        {
            var s = new WreckSequence(10);
            Assert.That(s.Sample(-5f).phase, Is.EqualTo(WreckPhase.Stagger));
            Assert.That(s.Sample(float.NaN).phase, Is.EqualTo(WreckPhase.Stagger));
            Assert.That(s.Sample(1e9f).phase, Is.EqualTo(WreckPhase.Done));
        }
    }
}
