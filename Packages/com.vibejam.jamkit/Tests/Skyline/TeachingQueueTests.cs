using System;
using System.Collections.Generic;
using JamKit.Skyline;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    /// <summary>The teaching-line queue's policy (Task 16 fix round 1): reading-time lines, combat notices first, shared
    /// triggers in order, stale hints dropped only when their moment has passed.</summary>
    public class TeachingQueueTests
    {
        static TeachingBeat Beat(string id, BeatTrigger t, int chars = 30) =>
            new TeachingBeat { id = id, trigger = t, text = new string('a', chars) };

        [Test]
        public void ALineLastsItsReadingTimeBetweenOneAndAHalfAndFourSeconds()
        {
            Assert.That(TeachingQueue.LineSeconds(new string('a', 30)), Is.EqualTo(2f).Within(1e-4f));
            Assert.That(TeachingQueue.LineSeconds("Ha!"), Is.EqualTo(TeachingQueue.MinLineSeconds));
            Assert.That(TeachingQueue.LineSeconds(new string('a', 140)), Is.EqualTo(TeachingQueue.MaxLineSeconds));
        }

        [Test]
        public void TheNextLineWaitsForTheLineAndTheGap()
        {
            var q = new TeachingQueue();
            q.Enqueue(Beat("a", BeatTrigger.WarmupStart, 30), 0f);
            q.Enqueue(Beat("b", BeatTrigger.WarmupStart, 30), 0f);
            Assert.That(q.Next(0f, null, null).id, Is.EqualTo("a"));
            Assert.That(q.Next(2.4f, null, null), Is.Null, "2 s line + 0.5 s gap");
            Assert.That(q.Next(2.5f, null, null).id, Is.EqualTo("b"));
        }

        [Test]
        public void CombatNoticesGoAheadOfPendingHintsAndSharedTriggersKeepTheirOrder()
        {
            var q = new TeachingQueue();
            q.Enqueue(Beat("nan-1", BeatTrigger.WarmupStart), 0f);
            q.Enqueue(Beat("nan-2", BeatTrigger.WarmupStart), 0f);
            q.Enqueue(Beat("dot-spots", BeatTrigger.PracticeBotSeen), 0f);
            q.Enqueue(Beat("concierge-drone", BeatTrigger.FirstSeen), 0f);
            q.Enqueue(Beat("dot-plating", BeatTrigger.FirstPlatingBreak), 0f);
            var said = new List<string>();
            for (float t = 0f; t < 60f && said.Count < 5; t += 0.1f)
            {
                var b = q.Next(t, null, null);
                if (b != null) said.Add(b.id);
            }
            Assert.That(said, Is.EqualTo(new[] { "concierge-drone", "dot-plating", "nan-1", "nan-2", "dot-spots" }));
        }

        [Test]
        public void AStaleHintIsDroppedOnlyOnceItsMomentHasPassed()
        {
            var q = new TeachingQueue();
            q.Enqueue(Beat("long", BeatTrigger.FirstSeen, 140), 0f);
            q.Next(0f, null, null);                               // busy until 4.5 s
            q.Enqueue(Beat("hint", BeatTrigger.NearBoostLedge), 0f);
            q.Enqueue(Beat("combat", BeatTrigger.FirstSeen), 0f);
            var dropped = new List<TeachingBeat>();
            Func<TeachingBeat, bool> passed = b => true;
            Func<TeachingBeat, bool> notPassed = b => false;
            Assert.That(q.Next(25f, notPassed, dropped).id, Is.EqualTo("combat"));
            Assert.That(dropped, Is.Empty, "waited 25 s but its moment is still on");
            Assert.That(q.Next(25.1f, passed, dropped), Is.Null, "still saying the combat line");
            Assert.That(dropped.Count, Is.EqualTo(1));
            Assert.That(dropped[0].id, Is.EqualTo("hint"));
            Assert.That(q.Count, Is.EqualTo(0));
        }

        [Test]
        public void AFreshHintIsNeverDroppedAndCombatNoticesNeverAre()
        {
            var q = new TeachingQueue();
            q.Enqueue(Beat("hint", BeatTrigger.PracticeBotSeen), 0f);
            q.Enqueue(Beat("combat", BeatTrigger.NextFightArmed), 0f);
            var dropped = new List<TeachingBeat>();
            Assert.That(q.Next(10f, b => true, dropped).id, Is.EqualTo("combat"));
            Assert.That(dropped, Is.Empty, "10 s is not stale");
        }
    }
}
