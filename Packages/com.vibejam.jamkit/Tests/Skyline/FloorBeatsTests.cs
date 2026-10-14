using System;
using System.Collections.Generic;
using JamKit.Skyline;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    /// <summary>Task L1: floor 1's teaching beats. The verbatim lines the specs quote are exact and not drafts; the M1 lessons
    /// are kept with the same words and triggers; every line fits the subtitle box; every trigger has an urgency.</summary>
    public class FloorBeatsTests
    {
        static TeachingBeat Beat(string id)
        {
            foreach (var b in FloorBeats.Floor1()) if (b.id == id) return b;
            Assert.Fail("no beat '" + id + "'");
            return null;
        }

        static TeachingBeat M1Beat(string id)
        {
            foreach (var b in TeachingBeats.M1()) if (b.id == id) return b;
            Assert.Fail("no M1 beat '" + id + "'");
            return null;
        }

        [Test]
        public void IdsAreUniqueAndEveryTextFitsTheSubtitleBox()
        {
            var ids = new HashSet<string>();
            foreach (var b in FloorBeats.Floor1())
            {
                Assert.That(ids.Add(b.id), Is.True, "unique id " + b.id);
                Assert.That(string.IsNullOrWhiteSpace(b.text), Is.False, b.id);
                Assert.That(b.text.Length, Is.InRange(1, TeachingBeats.MaxChars), b.id);
                Assert.That(b.text, Is.EqualTo(b.text.Trim()), b.id);
                Assert.That(Enum.IsDefined(typeof(Speaker), b.speaker), Is.True, b.id);
            }
        }

        [Test]
        public void TheVerbatimLinesAreExactAndNotDrafts()
        {
            var concede = Beat("concierge-concede");
            Assert.That(concede.text, Is.EqualTo("Asset transfer... acknowledged. Under protest."));
            Assert.That(concede.draft, Is.False);
            Assert.That(concede.speaker, Is.EqualTo(Speaker.Concierge));
            Assert.That(concede.trigger, Is.EqualTo(BeatTrigger.AllTowed));

            var clamped = Beat("dot-clamped");
            Assert.That(clamped.text, Is.EqualTo("Clamped! Get Moving!"));
            Assert.That(clamped.draft, Is.False, "the creator's line (2026-10-14)");

            var stay = Beat("dot-stay-near");
            Assert.That(stay.text, Is.EqualTo("Don't go too far, you need to stay nearby to transmat!"));
            Assert.That(stay.draft, Is.False);
            Assert.That(stay.speaker, Is.EqualTo(Speaker.Dot));
            Assert.That(stay.trigger, Is.EqualTo(BeatTrigger.LeftCircleMidTow));
            Assert.That(stay.repeats, Is.False);
            Assert.That(clamped.speaker, Is.EqualTo(Speaker.Dot));
            Assert.That(clamped.trigger, Is.EqualTo(BeatTrigger.AssetTowed));

            var plating = Beat("dot-plating");
            Assert.That(plating.text, Is.EqualTo("Strip it! Scrap's armour!"));
            Assert.That(plating.draft, Is.False);

            var floorCare = Beat("concierge-floor-care");
            Assert.That(floorCare.text, Is.EqualTo(M1Beat("concierge-floor-care").text), "verbatim as M1");
            Assert.That(floorCare.draft, Is.False);
            Assert.That(floorCare.enemy, Is.EqualTo(SkylineEnemies.FloorCareId));

            // the drone notice is M1's words too
            Assert.That(Beat("concierge-drone").text, Is.EqualTo(M1Beat("concierge-drone").text));
        }

        [Test]
        public void EveryOtherLineIsADraftForTheCreator()
        {
            var verbatim = new HashSet<string> { "concierge-concede", "dot-clamped", "dot-stay-near", "dot-plating", "concierge-floor-care" };
            foreach (var b in FloorBeats.Floor1())
                Assert.That(b.draft, Is.EqualTo(!verbatim.Contains(b.id)), b.id);
        }

        [TestCase("dot-writ-spots", BeatTrigger.PracticeBotSeen)]
        [TestCase("dot-writ-spot-made", BeatTrigger.FirstSpotCreated)]
        [TestCase("dot-breacher-boost", BeatTrigger.NearBoostLedge)]
        [TestCase("dot-boost-done", BeatTrigger.BoostLedgeReached)]
        [TestCase("nan-double-jump-mantle", BeatTrigger.NearPracticeLedge)]
        public void TheM1LessonsAreKeptWithTheSameWordsAndTriggers(string id, BeatTrigger trigger)
        {
            var b = Beat(id);
            Assert.That(b.trigger, Is.EqualTo(trigger));
            Assert.That(b.text, Is.EqualTo(M1Beat(id).text));
            Assert.That(b.speaker, Is.EqualTo(M1Beat(id).speaker));
        }

        [Test]
        public void EachBeatIsOnItsPlansTriggerAndSpeaker()
        {
            void Check(string id, Speaker who, BeatTrigger t, bool repeats = false)
            {
                var b = Beat(id);
                Assert.That(b.speaker, Is.EqualTo(who), id);
                Assert.That(b.trigger, Is.EqualTo(t), id);
                Assert.That(b.repeats, Is.EqualTo(repeats), id);
            }
            Check("concierge-greeting", Speaker.Concierge, BeatTrigger.LobbyDesk);
            Check("nan-lobby", Speaker.Nan, BeatTrigger.LobbyArrive);
            Check("nan-job-sheet", Speaker.Nan, BeatTrigger.LiftRiding);
            Check("concierge-first-wave", Speaker.Concierge, BeatTrigger.FloorStart);
            Check("concierge-alarm", Speaker.Concierge, BeatTrigger.AlarmRaised);
            Check("dot-towed", Speaker.Dot, BeatTrigger.AssetTowed, repeats: true);
            Check("concierge-wave", Speaker.Concierge, BeatTrigger.WaveAnnounce, repeats: true);
            Check("dot-special-ammo", Speaker.Dot, BeatTrigger.FirstSpecialAmmo);
            Check("dot-plating", Speaker.Dot, BeatTrigger.FirstPlatingBreak);
            // only the tow call and the wave notice repeat
            foreach (var b in FloorBeats.Floor1())
                Assert.That(b.repeats, Is.EqualTo(b.id == "dot-towed" || b.id == "concierge-wave"), b.id);
        }

        [Test]
        public void TheBotNoticesCoverTheFloorsBotsAndNotTheSpotlight()
        {
            var seen = new HashSet<string>();
            foreach (var b in FloorBeats.Floor1())
            {
                if (b.trigger != BeatTrigger.FirstSeen) { Assert.That(b.enemy, Is.Null.Or.Empty, b.id); continue; }
                Assert.That(b.speaker, Is.EqualTo(Speaker.Concierge), b.id);
                Assert.That(seen.Add(b.enemy), Is.True, "one notice per bot: " + b.enemy);
            }
            Assert.That(seen, Is.EquivalentTo(new[] { SkylineEnemies.DroneId, SkylineEnemies.FloorCareId, SkylineEnemies.GondolaId }),
                "drone, floor-care, gondola; the spotlight is excluded from this floor");
            var g = Beat("concierge-gondola");
            Assert.That(g.text, Is.Not.EqualTo(M1Beat("concierge-gondola").text), "re-worded for 'outside the glass'");
            Assert.That(g.text, Does.Contain("glass"));
        }

        [Test]
        public void EveryTriggerHasAnUrgencyAndTheClassesFollowThePlan()
        {
            foreach (BeatTrigger t in Enum.GetValues(typeof(BeatTrigger)))
                Assert.DoesNotThrow(() => TeachingQueue.UrgencyOf(t), t.ToString());
            foreach (var b in FloorBeats.Floor1())
                Assert.That(Enum.IsDefined(typeof(LineUrgency), TeachingQueue.UrgencyOf(b.trigger)), Is.True, b.id);
            Assert.That(TeachingQueue.UrgencyOf(Beat("dot-clamped").trigger), Is.EqualTo(LineUrgency.Reaction));
            Assert.That(TeachingQueue.UrgencyOf(Beat("dot-towed").trigger), Is.EqualTo(LineUrgency.Reaction));
            Assert.That(TeachingQueue.UrgencyOf(Beat("dot-special-ammo").trigger), Is.EqualTo(LineUrgency.Reaction));
            Assert.That(TeachingQueue.UrgencyOf(Beat("concierge-concede").trigger), Is.EqualTo(LineUrgency.Notice));
            Assert.That(TeachingQueue.UrgencyOf(Beat("concierge-alarm").trigger), Is.EqualTo(LineUrgency.Notice));
            Assert.That(TeachingQueue.UrgencyOf(Beat("concierge-wave").trigger), Is.EqualTo(LineUrgency.Notice));
            Assert.That(TeachingQueue.UrgencyOf(Beat("concierge-first-wave").trigger), Is.EqualTo(LineUrgency.Notice));
            Assert.That(TeachingQueue.UrgencyOf(Beat("concierge-greeting").trigger), Is.EqualTo(LineUrgency.Lesson));
            Assert.That(TeachingQueue.UrgencyOf(Beat("nan-lobby").trigger), Is.EqualTo(LineUrgency.Hint));
            Assert.That(TeachingQueue.UrgencyOf(Beat("nan-job-sheet").trigger), Is.EqualTo(LineUrgency.Hint));
        }

        /// <summary>The lift ride's riser plus the pre-drop, the least it can last: the placeholder sections' numbers
        /// (docs/SKYLINE-MUSIC.md: the muzak/pre-drop at 86 BPM, 4 bars of 4 beats = about 11.2 s; the reference riser alone
        /// is about 16.1 s). The job sheet is said whole inside it, with a second to spare.</summary>
        const float RiserAndPreDropSeconds = 4 * 4 * 60f / 86f;

        [Test]
        public void NansJobSheetFitsInsideTheLiftRide()
        {
            var b = Beat("nan-job-sheet");
            Assert.That(TeachingQueue.LineSeconds(b.text) + 1f, Is.LessThanOrEqualTo(RiserAndPreDropSeconds));
            Assert.That(b.text, Does.Contain("The clock's running, love"), "the spec's phrase, verbatim");
            Assert.That(TeachingQueue.LineSeconds(b.text), Is.LessThan(TeachingQueue.MaxLineSeconds), "not clipped by the reading-time cap");
        }

        [Test]
        public void WavesAreAnnouncedOnlyOnEveryThird()
        {
            var announced = new List<int>();
            for (int n = 1; n <= 10; n++) if (FloorBeats.AnnouncesWave(n)) announced.Add(n);
            Assert.That(announced, Is.EqualTo(new[] { 3, 6, 9 }));
            Assert.That(FloorBeats.AnnouncesWave(0), Is.False);
            Assert.That(FloorBeats.AnnouncesWave(-3), Is.False);
        }

        [Test]
        public void NoBeatIsQueuedTwiceAndTheQueueAcceptsThemAll()
        {
            // a smoke run through the real queue: every beat can be queued, a non-repeating one only once
            var q = new TeachingQueue();
            float now = 0f;
            foreach (var b in FloorBeats.Floor1())
            {
                Assert.That(q.Enqueue(b, now), Is.True, b.id);
                var got = q.Next(now, null, new List<TeachingBeat>());
                Assert.That(got, Is.SameAs(b), b.id);
                now += 100f;
                Assert.That(q.Enqueue(b, now), Is.EqualTo(b.repeats), b.id + " (repeats: " + b.repeats + ")");
                q.Clear();
            }
        }
    }
}
