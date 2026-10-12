using System.Collections.Generic;
using JamKit.VisitingHours;
using NUnit.Framework;

namespace JamKit.Tests.VisitingHours
{
    /// <summary>The opening's cue list (docs/OPENING-AND-TITLE.md): anchored cues resolve against the
    /// recordings' markers, beats come in the shot list's order, a new take re-times by its markers (or
    /// stretches until it's re-timed), and skipping leaves the title's final state.</summary>
    public class OpeningTests
    {
        static List<VoiceTrack> Tracks() => new List<VoiceTrack> { SliceContent.VoicemailTrack(), SliceContent.NewsTrack() };

        static List<OpeningCue> Resolved(System.Func<string, float> len = null, bool subtitles = false, List<string> notes = null) =>
            OpeningScript.Resolve(SliceContent.OpeningCues(), Tracks(), len, subtitles, notes);

        static float At(List<OpeningCue> cues, OpeningCueKind kind, string text = null)
        {
            foreach (var c in cues)
                if (c.kind == kind && (text == null || c.text == text)) return c.at;
            Assert.Fail("no " + kind + " " + text);
            return 0f;
        }

        [Test]
        public void TheDefaultsHaveNoProblems()
        {
            var problems = OpeningScript.Problems(SliceContent.OpeningCues(), Tracks(), null);
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
            var notes = new List<string>();
            var cues = Resolved(null, false, notes);
            Assert.That(notes, Is.Empty, string.Join("\n", notes));
            Assert.That(cues.Count, Is.EqualTo(SliceContent.OpeningCues().Count), "every cue resolves");
        }

        [Test]
        public void CuesFireInOrderAndALongFrameDropsNone()
        {
            var t = new OpeningTimeline(Resolved());
            var fired = new List<OpeningCue>();
            t.Advance(0.6f, fired);
            Assert.That(fired.Exists(c => c.kind == OpeningCueKind.Ring), Is.True, "it opens on the phone ringing");
            Assert.That(fired.Exists(c => c.kind == OpeningCueKind.Fade), Is.False, "in the black");
            t.Advance(1000f, fired);
            Assert.That(t.Finished, Is.True);
            Assert.That(fired.Count, Is.EqualTo(SliceContent.OpeningCues().Count));
            for (int i = 1; i < fired.Count; i++) Assert.That(fired[i].at, Is.GreaterThanOrEqualTo(fired[i - 1].at));
            Assert.That(fired[fired.Count - 1].kind, Is.EqualTo(OpeningCueKind.End));
        }

        [Test]
        public void TheBeatsComeInTheShotListsOrder()
        {
            var cues = Resolved();
            float voice = At(cues, OpeningCueKind.Voice, "voicemail");
            var vm = SliceContent.VoicemailTrack();
            float drop = voice + vm.Find("drop").at - vm.trimStart;
            float night = voice + vm.Find("goodnight").at - vm.trimStart;
            float vmEnd = voice + vm.recordedLength - vm.trimStart;

            Assert.That(At(cues, OpeningCueKind.Ring), Is.LessThan(At(cues, OpeningCueKind.Fade, "in")), "rings in the black first");
            Assert.That(At(cues, OpeningCueKind.Fade, "in"), Is.InRange(2f, 4f), "fade up around 0:03");
            Assert.That(At(cues, OpeningCueKind.Camera, "nightstand"), Is.LessThan(At(cues, OpeningCueKind.Arm, "reach")));
            Assert.That(At(cues, OpeningCueKind.Arm, "grab"), Is.EqualTo(At(cues, OpeningCueKind.RingStop)).Within(0.05f), "she grabs it just as the ringing stops");
            Assert.That(At(cues, OpeningCueKind.Phone, "missed"), Is.LessThan(At(cues, OpeningCueKind.Arm, "tap")));
            Assert.That(At(cues, OpeningCueKind.Arm, "tap"), Is.LessThan(voice), "her thumb plays it");
            Assert.That(At(cues, OpeningCueKind.Arm, "drop"), Is.EqualTo(drop).Within(0.01f), "she drops the phone at \"three in the morning\"");
            // She drifts off: his voice fades, her eyes close (a blink, then shut), then the cut to the phone from above,
            // where his voice is back and clear (never through the phone's speaker).
            float overhead = At(cues, OpeningCueKind.Camera, "overhead");
            Assert.That(overhead, Is.InRange(drop + 2f, drop + 6f), "the cut to the phone from above");
            var fadeVoice = cues.Find(c => c.kind == OpeningCueKind.VoiceLevel && c.value < 0.01f);
            Assert.That(fadeVoice, Is.Not.Null, "his voice fades as she drifts off");
            Assert.That(fadeVoice.at + fadeVoice.seconds, Is.LessThanOrEqualTo(overhead + 0.01f), "faded out before the cut");
            Assert.That(cues.Exists(c => c.kind == OpeningCueKind.VoiceLevel && c.value > 0.99f && System.Math.Abs(c.at - overhead) < 0.01f), Is.True, "back at the cut");
            var closes = cues.FindAll(c => c.kind == OpeningCueKind.Fade && c.text == "out" && c.at > drop && c.at < overhead);
            Assert.That(closes.Count, Is.GreaterThanOrEqualTo(2), "her eyes close: a slow blink, then shut");
            Assert.That(cues.Exists(c => c.kind == OpeningCueKind.Fade && c.text == "in" && System.Math.Abs(c.at - overhead) < 0.01f), Is.True, "the view from above fades up");
            foreach (var c in cues)
                if (c.kind == OpeningCueKind.Speaker) Assert.That(c.value, Is.LessThan(0.01f), "no phone-speaker filter on his voice");
            float goodnightBlack = cues.FindLast(c => c.kind == OpeningCueKind.Fade && c.text == "out").at;
            Assert.That(goodnightBlack, Is.InRange(night, night + 4f), "to black on \"G'night\"");
            Assert.That(At(cues, OpeningCueKind.Phone, "off"), Is.LessThan(goodnightBlack), "the screen goes out last, then black");
            Assert.That(At(cues, OpeningCueKind.Phone, "off"), Is.GreaterThan(night), "the room goes before the screen");
            float music = At(cues, OpeningCueKind.Music);
            float news = At(cues, OpeningCueKind.Voice, "news");
            Assert.That(music, Is.GreaterThan(vmEnd + 1.5f), "black, held, before the music");
            Assert.That(news, Is.GreaterThan(music), "the music blooms, then the news");
            Assert.That(cues.FindLast(c => c.kind == OpeningCueKind.Fade && c.text == "in").at, Is.EqualTo(news).Within(0.01f), "out of the black as the report starts");
            // Then the pieces, one after another: coffee, badge, notepad, tablet, table, room.
            float prev = news;
            foreach (var piece in new[] { "mug", "badge", "notepad", "tablet", "table", "room" })
            {
                float t = At(cues, OpeningCueKind.Show, piece);
                Assert.That(t, Is.GreaterThan(prev + (piece == "mug" ? 0f : 1f)), piece + " arrives after the one before");
                prev = t;
            }
            Assert.That(prev, Is.LessThan(news + SliceContent.NewsTrack().recordedLength - 3f), "the room is up before the report ends");
            Assert.That(prev - At(cues, OpeningCueKind.Show, "mug"), Is.LessThan(SliceContent.NewsTrack().recordedLength * 0.7f), "no long gaps: paced by the report");
            Assert.That(At(cues, OpeningCueKind.Interactive), Is.EqualTo(news + SliceContent.NewsTrack().recordedLength).Within(0.01f), "interactive at the end of the report");
            Assert.That(voice + vm.Find("hey").at - vm.trimStart - At(cues, OpeningCueKind.Arm, "tap"), Is.InRange(0.3f, 2f), "Elias starts soon after her thumb");
        }

        [Test]
        public void TheTablePiecesArePacedByTheReportsLength()
        {
            // A shorter report (the new take) brings the pieces closer together, in the same order, all up before it ends.
            float shortLen = 18f;
            var cues = OpeningScript.Resolve(SliceContent.OpeningCues(), Tracks(), clip => clip == "vo_news_anchor" ? shortLen : 0f, false, null);
            float news = At(cues, OpeningCueKind.Voice, "news");
            float prev = news, first = -1f;
            foreach (var piece in new[] { "mug", "badge", "notepad", "tablet", "table", "room" })
            {
                float t = At(cues, OpeningCueKind.Show, piece);
                Assert.That(t, Is.GreaterThan(prev), piece);
                if (first < 0f) first = t;
                prev = t;
            }
            Assert.That(prev, Is.LessThan(news + shortLen - 2f), "all up before the report ends");
            Assert.That(prev - first, Is.LessThan(shortLen * 0.7f));
            Assert.That(At(cues, OpeningCueKind.Interactive), Is.EqualTo(news + shortLen).Within(0.01f));
            float share;
            Assert.That(OpeningScript.Percent("40%", out share) && System.Math.Abs(share - 0.4f) < 1e-4f, Is.True);
            Assert.That(OpeningScript.Percent("drop", out share), Is.False);
        }

        [Test]
        public void EveryTablePieceFadesInOnce()
        {
            var cues = Resolved();
            foreach (var piece in SliceContent.TablePieces)
                Assert.That(cues.FindAll(c => c.kind == OpeningCueKind.Show && c.text == piece).Count, Is.EqualTo(1), piece);
            foreach (var c in cues)
                if (c.kind == OpeningCueKind.Camera) Assert.That(SliceContent.CameraPoses, Does.Contain(c.text), "camera pose " + c.text);
        }

        [Test]
        public void ANewTakeRetimesByItsMarkers()
        {
            var tracks = Tracks();
            var vm = tracks[0];
            var taps = new List<float>();
            for (int i = 0; i < vm.markers.Count; i++) taps.Add(1f + i * 4f);
            Assert.That(OpeningScript.Retime(vm, taps, 40f), Is.True);
            Assert.That(vm.recordedLength, Is.EqualTo(40f));
            var cues = OpeningScript.Resolve(SliceContent.OpeningCues(), tracks, clip => clip == "vo_elias_voicemail" ? 40f : 0f, false, null);
            float voice = At(cues, OpeningCueKind.Voice, "voicemail");
            Assert.That(vm.Find("drop").at, Is.EqualTo(taps[1] + vm.trimStart).Within(1e-4f), "markers keep the recording's own seconds");
            Assert.That(At(cues, OpeningCueKind.Arm, "drop") - voice, Is.EqualTo(taps[1]).Within(1e-4f), "the drop follows the new tap");
            Assert.That(At(cues, OpeningCueKind.Voice, "news") - voice, Is.EqualTo(40f - vm.trimStart + 6f).Within(1e-3f), "later beats follow the new length");

            Assert.That(OpeningScript.Retime(vm, new List<float> { 1f, 2f }, 40f), Is.False, "one tap per marker");
            var backwards = new List<float>(taps);
            backwards[2] = 0.5f;
            Assert.That(OpeningScript.Retime(vm, backwards, 40f), Is.False, "taps can't go backwards");
        }

        [Test]
        public void ATakeOfAnotherLengthStretchesItsMarkersUntilRetimed()
        {
            var notes = new List<string>();
            float k = 45f / 32.32f;
            var vm = SliceContent.VoicemailTrack();
            float dropAt = vm.Find("drop").at;
            var cues = Resolved(clip => clip == "vo_elias_voicemail" ? 45f : 0f, false, notes);
            float voice = At(cues, OpeningCueKind.Voice, "voicemail");
            Assert.That(At(cues, OpeningCueKind.Arm, "drop") - voice, Is.EqualTo(dropAt * k - vm.trimStart).Within(1e-3f));
            Assert.That(notes.Exists(n => n.Contains("voicemail") && n.Contains("stretch")), Is.True, "the stretch is reported");
            // Within the tolerance nothing moves.
            cues = Resolved(clip => clip == "vo_elias_voicemail" ? 32.4f : 0f);
            Assert.That(At(cues, OpeningCueKind.Arm, "drop") - At(cues, OpeningCueKind.Voice, "voicemail"), Is.EqualTo(dropAt - vm.trimStart).Within(1e-4f));
        }

        [Test]
        public void SubtitlesComeFromTheMarkers()
        {
            var cues = Resolved(null, true);
            var subs = cues.FindAll(c => c.kind == OpeningCueKind.Subtitle);
            Assert.That(subs.Count, Is.EqualTo(SliceContent.VoicemailTrack().markers.Count));
            Assert.That(subs[1].text, Does.Contain("in the morning"));
            Assert.That(subs[subs.Count - 1].text, Does.Contain("G'night"));
            Assert.That(cues.Exists(c => c.kind == OpeningCueKind.ClearSubtitle), Is.True);
            Assert.That(Resolved().Exists(c => c.kind == OpeningCueKind.Subtitle), Is.False, "none when they're off");
        }

        [Test]
        public void BrokenAnchorsAndMarkersAreProblems()
        {
            var cues = SliceContent.OpeningCues();
            cues.Add(new OpeningCue { anchor = "voicemail:nope", kind = OpeningCueKind.Sfx, text = "vh_tap" });
            cues.Add(new OpeningCue { anchor = "ghost:start", kind = OpeningCueKind.Sfx, text = "vh_tap" });
            var tracks = Tracks();
            tracks[0].markers[3].at = 99f;
            var problems = OpeningScript.Problems(cues, tracks, null);
            Assert.That(problems.Exists(p => p.Contains("voicemail:nope")), Is.True);
            Assert.That(problems.Exists(p => p.Contains("ghost:start")), Is.True);
            Assert.That(problems.Exists(p => p.Contains("past the end")), Is.True);
            Assert.That(problems.Exists(p => p.Contains("comes before")), Is.True);
        }

        [Test]
        public void SkippingLeavesTheTitlesFinalState()
        {
            var t = new OpeningTimeline(Resolved());
            var fired = new List<OpeningCue>();
            t.Advance(2f, fired);
            fired.Clear();
            t.SkipToEnd(fired);
            Assert.That(t.Finished, Is.True);
            Assert.That(fired.Exists(c => c.kind == OpeningCueKind.RingStop), Is.True);
            Assert.That(fired.Exists(c => c.kind == OpeningCueKind.Interactive), Is.True);
            Assert.That(fired.Exists(c => c.kind == OpeningCueKind.End), Is.True);
            Assert.That(fired.FindAll(c => c.kind == OpeningCueKind.Show).Count, Is.EqualTo(SliceContent.TablePieces.Length), "every piece shows");
            Assert.That(fired.Exists(c => c.kind == OpeningCueKind.Voice), Is.False, "no voices start on a skip");
        }

        [Test]
        public void TheHeadlineNamesLarpacaNotWeWork()
        {
            Assert.That(SliceContent.Headline(""), Does.StartWith("ROUTE 9"));
            Assert.That(SliceContent.Headline("A CROSSFIRE INVOLVING THE WEWORK GANG"), Is.EqualTo("A CROSSFIRE INVOLVING THE LARPACA GANG"));
            Assert.That(SliceContent.Headline("the WeWork gang"), Is.EqualTo("the Larpaca gang"));
        }
    }
}
