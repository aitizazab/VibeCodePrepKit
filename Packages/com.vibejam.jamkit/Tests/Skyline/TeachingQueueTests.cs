using System;
using System.Collections.Generic;
using JamKit.Skyline;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    /// <summary>The teaching-line queue's policy (judge round 1 fix): lines last their reading time at 15 characters a
    /// second; reactions, then notices, then hints; a line whose moment has passed is dropped at once; a reaction cuts in.</summary>
    public class TeachingQueueTests
    {
        static TeachingBeat Beat(string id, BeatTrigger t, int chars = 30) =>
            new TeachingBeat { id = id, trigger = t, text = new string('a', chars) };

        [Test]
        public void ALineLastsItsReadingTimeAtFifteenCharactersASecond()
        {
            Assert.That(TeachingQueue.LineSeconds(new string('a', 30)), Is.EqualTo(2.5f).Within(1e-4f), "0.5 s lead + 2 s");
            Assert.That(TeachingQueue.LineSeconds("Ha!"), Is.EqualTo(TeachingQueue.MinLineSeconds));
            Assert.That(TeachingQueue.LineSeconds(new string('a', 120)), Is.EqualTo(8.5f).Within(1e-4f));
            Assert.That(TeachingQueue.LineSeconds(new string('a', 400)), Is.EqualTo(TeachingQueue.MaxLineSeconds));
        }

        [Test]
        public void EveryM1LineGetsItsFullReadingTime()
        {
            foreach (var b in TeachingBeats.M1())
            {
                float s = TeachingQueue.LineSeconds(b.text);
                Assert.That(b.text.Length / s, Is.LessThanOrEqualTo(TeachingQueue.CharsPerSecond), b.id + " reads too fast");
                Assert.That(s, Is.LessThan(TeachingQueue.MaxLineSeconds), b.id + " is cut by the cap");
            }
        }

        [Test]
        public void TheRouteLinesAreWhereTheyBelong()
        {
            var beats = TeachingBeats.M1();
            var nan = Array.Find(beats, b => b.id == "nan-double-jump-mantle");
            Assert.That(nan.trigger, Is.EqualTo(BeatTrigger.NearPracticeLedge), "Nan's double jump + mantle: the practice counter");
            var lift = Array.Find(beats, b => b.trigger == BeatTrigger.LiftOpened);
            Assert.That(lift, Is.Not.Null, "CONCIERGE announces the lift");
            Assert.That(lift.speaker, Is.EqualTo(Speaker.Concierge));
            Assert.That(TeachingQueue.UrgencyOf(BeatTrigger.LiftOpened), Is.EqualTo(LineUrgency.Notice));
            Assert.That(TeachingQueue.UrgencyOf(BeatTrigger.NearPracticeLedge), Is.EqualTo(LineUrgency.Lesson));
            Assert.That(Array.Find(beats, b => b.id == "concierge-next-fight").text, Does.Not.Contain("food court"));
        }

        [Test]
        public void TheNextLineWaitsForTheLineAndTheGap()
        {
            var q = new TeachingQueue();
            q.Enqueue(Beat("a", BeatTrigger.WarmupStart, 30), 0f);
            q.Enqueue(Beat("b", BeatTrigger.WarmupStart, 30), 0f);
            Assert.That(q.Next(0f, null, null).id, Is.EqualTo("a"));
            Assert.That(q.Next(2.85f, null, null), Is.Null, "2.5 s line + 0.4 s gap");
            Assert.That(q.Next(2.95f, null, null).id, Is.EqualTo("b"));
        }

        [Test]
        public void ReactionsThenNoticesThenLessonsThenTheOpenerAndSharedTriggersKeepTheirOrder()
        {
            var q = new TeachingQueue();
            q.Enqueue(Beat("nan-1", BeatTrigger.WarmupStart), 0f);
            q.Enqueue(Beat("dot-spots", BeatTrigger.PracticeBotSeen), 0f);
            q.Enqueue(Beat("nan-kiosk", BeatTrigger.NearBoostLedge), 0f);
            q.Enqueue(Beat("dot-kiosk", BeatTrigger.NearBoostLedge), 0f);
            q.Enqueue(Beat("concierge-drone", BeatTrigger.FirstSeen), 0f);
            q.Enqueue(Beat("dot-plating", BeatTrigger.FirstPlatingBreak), 0f);
            var said = new List<string>();
            for (float t = 0f; t < 60f && said.Count < 6; t += 0.1f)
            {
                var b = q.Next(t, null, null);
                if (b != null) said.Add(b.id);
            }
            Assert.That(said, Is.EqualTo(new[] { "dot-plating", "concierge-drone", "dot-spots", "nan-kiosk", "dot-kiosk", "nan-1" }));
        }

        [Test]
        public void ALineWhoseMomentHasPassedIsDroppedAtOnce()
        {
            var q = new TeachingQueue();
            q.Enqueue(Beat("long", BeatTrigger.FirstSeen, 120), 0f);
            Assert.That(q.Next(0f, null, null).id, Is.EqualTo("long"));
            q.Enqueue(Beat("kiosk", BeatTrigger.NearBoostLedge), 0.5f);
            var dropped = new List<TeachingBeat>();
            Assert.That(q.Next(1f, b => false, dropped), Is.Null, "still saying the notice");
            Assert.That(dropped, Is.Empty);
            Assert.That(q.Next(1.1f, b => b.id == "kiosk", dropped), Is.Null);
            Assert.That(dropped.Count, Is.EqualTo(1), "1 s late, but its moment is over");
            Assert.That(dropped[0].id, Is.EqualTo("kiosk"));
            Assert.That(q.Count, Is.EqualTo(0));
        }

        [Test]
        public void ALineThatWaitedPastItsKindsLimitIsDroppedEvenIfItsMomentHolds()
        {
            var q = new TeachingQueue();
            q.Enqueue(Beat("hint", BeatTrigger.PracticeBotSeen), 0f);
            q.Enqueue(Beat("reaction", BeatTrigger.BoostLedgeReached), 0f);
            var dropped = new List<TeachingBeat>();
            Assert.That(q.Next(TeachingQueue.ReactionMaxWait + 0.1f, b => false, dropped).id, Is.EqualTo("hint"));
            Assert.That(dropped.Count, Is.EqualTo(1));
            Assert.That(dropped[0].id, Is.EqualTo("reaction"), "a reaction 3 s late is no reaction");

            var q2 = new TeachingQueue();
            q2.Enqueue(Beat("long", BeatTrigger.FirstSeen, 140), 0f);
            q2.Next(0f, null, null);
            q2.Enqueue(Beat("hint", BeatTrigger.NearBoostLedge), 0f);
            dropped.Clear();
            Assert.That(q2.Next(TeachingQueue.HintMaxWait + 0.1f, b => false, dropped), Is.Null);
            Assert.That(dropped.Count, Is.EqualTo(1));
        }

        [Test]
        public void AReactionCutsTheLineBeingSaidWhichIsSaidAgainAfter()
        {
            var q = new TeachingQueue();
            q.Enqueue(Beat("notice", BeatTrigger.FirstSeen, 120), 0f);
            Assert.That(q.Next(0f, null, null).id, Is.EqualTo("notice"));
            q.Enqueue(Beat("spot", BeatTrigger.FirstSpotCreated, 40), 0.5f);
            var cut = new List<TeachingBeat>();
            Assert.That(q.Next(0.6f, null, null, cut), Is.Null, "up for less than the minimum: not cut yet");
            Assert.That(q.Next(TeachingQueue.MinShownBeforeCut, null, null, cut).id, Is.EqualTo("spot"));
            Assert.That(cut.Count, Is.EqualTo(1));
            Assert.That(cut[0].id, Is.EqualTo("notice"));
            Assert.That(q.Contains("notice"), Is.True, "back in the queue");
            float end = TeachingQueue.MinShownBeforeCut + TeachingQueue.LineSeconds(new string('a', 40)) + TeachingQueue.Gap;
            Assert.That(q.Next(end + 0.01f, null, null).id, Is.EqualTo("notice"));
        }

        [Test]
        public void ANoticeCutsALessonOnSightAndTheLessonIsSaidAgainAfter()
        {
            var q = new TeachingQueue();
            q.Enqueue(Beat("lesson", BeatTrigger.NearBoostLedge, 120), 0f);
            Assert.That(q.Next(0f, null, null).id, Is.EqualTo("lesson"));
            q.Enqueue(Beat("concierge-floor-care", BeatTrigger.FirstSeen, 87), 0.5f);
            var cut = new List<TeachingBeat>();
            Assert.That(q.Next(1f, b => false, null, cut), Is.Null, "the lesson has been up under the minimum");
            Assert.That(q.Next(TeachingQueue.MinShownBeforeCut, b => false, null, cut).id, Is.EqualTo("concierge-floor-care"));
            Assert.That(cut.Count, Is.EqualTo(1));
            Assert.That(q.Contains("lesson"), Is.True, "said again after, while its moment holds");
        }

        [Test]
        public void ALessonCutsTheOpener()
        {
            var q = new TeachingQueue();
            q.Enqueue(Beat("nan-warmup", BeatTrigger.WarmupStart, 70), 0f);
            Assert.That(q.Next(0f, null, null).id, Is.EqualTo("nan-warmup"));
            q.Enqueue(Beat("dot-boost", BeatTrigger.NearBoostLedge, 80), 1f);
            Assert.That(q.Next(TeachingQueue.MinShownBeforeCut + 0.1f, null, null).id, Is.EqualTo("dot-boost"));
        }

        [Test]
        public void ANoticeIsNeverDroppedForWaitingWhileItsMomentHolds()
        {
            var q = new TeachingQueue();
            q.Enqueue(Beat("concierge-drone", BeatTrigger.FirstSeen, 120), 0f);
            q.Enqueue(Beat("concierge-floor-care", BeatTrigger.FirstSeen, 87), 0f);
            var dropped = new List<TeachingBeat>();
            Assert.That(q.Next(0f, b => false, dropped).id, Is.EqualTo("concierge-drone"));
            float after = TeachingQueue.LineSeconds(new string('a', 120)) + TeachingQueue.Gap;
            Assert.That(q.Next(after, b => false, dropped).id, Is.EqualTo("concierge-floor-care"), "a notice waits a whole notice");
            Assert.That(dropped, Is.Empty);
        }

        [Test]
        public void ALineWhoseMomentPassesWhileSaidEndsAndTheNextStartsAtOnce()
        {
            var q = new TeachingQueue();
            q.Enqueue(Beat("concierge-spotlight", BeatTrigger.FirstSeen, 121), 0f);
            q.Enqueue(Beat("concierge-floor-care", BeatTrigger.FirstSeen, 87), 0f);
            Assert.That(q.Next(0f, null, null).id, Is.EqualTo("concierge-spotlight"));
            bool spotlightsWrecked = false;
            Func<TeachingBeat, bool> passed = b => b.id == "concierge-spotlight" && spotlightsWrecked;
            var dropped = new List<TeachingBeat>();
            var cut = new List<TeachingBeat>();
            Assert.That(q.Next(2f, passed, dropped, cut), Is.Null);
            spotlightsWrecked = true;
            Assert.That(q.Next(2.1f, passed, dropped, cut).id, Is.EqualTo("concierge-floor-care"), "no gap: the next notice at once");
            Assert.That(cut.Count, Is.EqualTo(1));
            Assert.That(dropped.Count, Is.EqualTo(1), "ended, not said again");
            Assert.That(q.Contains("concierge-spotlight"), Is.False);

            var q2 = new TeachingQueue();
            q2.Enqueue(Beat("concierge-spotlight", BeatTrigger.FirstSeen, 121), 0f);
            q2.Next(0f, null, null);
            cut.Clear();
            Assert.That(q2.Next(3f, b => true, null, cut), Is.Null);
            Assert.That(cut.Count, Is.EqualTo(1), "nothing replaces it: the caller takes the subtitle down");
            Assert.That(q2.Current(3.01f), Is.Null);
        }

        [Test]
        public void AReactionNeverCutsAReaction()
        {
            var q = new TeachingQueue();
            q.Enqueue(Beat("plating", BeatTrigger.FirstPlatingBreak, 40), 0f);
            Assert.That(q.Next(0f, null, null).id, Is.EqualTo("plating"));
            q.Enqueue(Beat("spot", BeatTrigger.FirstSpotCreated, 30), 1f);
            Assert.That(q.Next(2f, null, null), Is.Null);
            Assert.That(q.Next(TeachingQueue.LineSeconds(new string('a', 40)) + TeachingQueue.Gap, null, null).id, Is.EqualTo("spot"));
        }
    }
}
