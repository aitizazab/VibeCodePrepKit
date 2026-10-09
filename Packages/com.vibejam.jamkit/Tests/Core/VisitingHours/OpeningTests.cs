using System.Collections.Generic;
using JamKit.VisitingHours;
using NUnit.Framework;

namespace JamKit.Tests.VisitingHours
{
    public class OpeningTests
    {
        [Test]
        public void CuesFireInOrderAndALongFrameDropsNone()
        {
            var t = new OpeningTimeline(SliceContent.Opening());
            var fired = new List<OpeningCue>();
            t.Advance(0.5f, fired);
            Assert.That(fired.Count, Is.EqualTo(1));
            Assert.That(fired[0].kind, Is.EqualTo(OpeningCueKind.Ring), "it opens on the phone ringing");
            t.Advance(1000f, fired);
            Assert.That(t.Finished, Is.True);
            Assert.That(fired.Count, Is.EqualTo(SliceContent.Opening().Count));
            for (int i = 1; i < fired.Count; i++) Assert.That(fired[i].at, Is.GreaterThanOrEqualTo(fired[i - 1].at));
            Assert.That(fired[fired.Count - 1].kind, Is.EqualTo(OpeningCueKind.End));
        }

        [Test]
        public void TheOpeningsBeatsComeInTheSpecsOrder()
        {
            var cues = new OpeningTimeline(SliceContent.Opening()).Cues;
            float At(OpeningCueKind kind, string text = null)
            {
                foreach (var c in cues)
                    if (c.kind == kind && (text == null || c.text.Contains(text))) return c.at;
                Assert.Fail("no " + kind + " " + text);
                return 0f;
            }
            Assert.That(At(OpeningCueKind.PhoneText, "calling"), Is.LessThan(At(OpeningCueKind.PhoneText, "Missed call")));
            Assert.That(At(OpeningCueKind.PhoneText, "Missed call: Elias, 5:52 AM"), Is.LessThan(At(OpeningCueKind.Voice, "vo_elias_voicemail")));
            Assert.That(At(OpeningCueKind.SitUp), Is.LessThan(At(OpeningCueKind.SinkBack)));
            float five = At(OpeningCueKind.Subtitle, "five in the morning");
            Assert.That(At(OpeningCueKind.SinkBack) - five, Is.InRange(0f, 1f), "she sinks back at \"five in the morning\"");
            Assert.That(At(OpeningCueKind.Subtitle, "sleep in"), Is.GreaterThan(five));
            Assert.That(At(OpeningCueKind.FadeOut), Is.GreaterThan(At(OpeningCueKind.Subtitle, "sleep in")));
        }

        [Test]
        public void TheVoicemailsSubtitlesStretchToTheRecording()
        {
            var t = new OpeningTimeline(SliceContent.Opening());
            float voice = 0f, hide = 0f, end = t.Length, five = 0f;
            foreach (var c in t.Cues)
            {
                if (c.kind == OpeningCueKind.Voice && c.text == "vo_elias_voicemail") voice = c.at;
                if (c.kind == OpeningCueKind.PhoneHide) hide = c.at;
                if (c.kind == OpeningCueKind.Subtitle && c.text.Contains("five in the morning")) five = c.at;
            }
            float old = hide - voice;
            Assert.That(t.ScaleSpan("vo_elias_voicemail", OpeningCueKind.PhoneHide, old * 2f), Is.True);
            float newFive = 0f, newHide = 0f;
            foreach (var c in t.Cues)
            {
                if (c.kind == OpeningCueKind.PhoneHide) newHide = c.at;
                if (c.kind == OpeningCueKind.Subtitle && c.text.Contains("five in the morning")) newFive = c.at;
            }
            Assert.That(newHide - voice, Is.EqualTo(old * 2f).Within(1e-3f));
            Assert.That(newFive - voice, Is.EqualTo((five - voice) * 2f).Within(1e-3f));
            Assert.That(t.Length, Is.EqualTo(end + old).Within(1e-3f), "later cues shift by the difference");
            Assert.That(t.ScaleSpan("vo_missing", OpeningCueKind.PhoneHide, 10f), Is.False);
        }

        [Test]
        public void SkippingLeavesTheFinalState()
        {
            var t = new OpeningTimeline(SliceContent.Opening());
            var fired = new List<OpeningCue>();
            t.Advance(2f, fired);
            fired.Clear();
            t.SkipToEnd(fired);
            Assert.That(t.Finished, Is.True);
            Assert.That(fired.Exists(c => c.kind == OpeningCueKind.RingStop), Is.True);
            Assert.That(fired.Exists(c => c.kind == OpeningCueKind.End), Is.True);
            Assert.That(fired.Exists(c => c.kind == OpeningCueKind.Voice), Is.False, "no voices start on a skip");
        }
    }
}
