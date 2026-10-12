using System.Collections.Generic;
using JamKit.Flow;
using JamKit.VisitingHours;
using NUnit.Framework;

namespace JamKit.Tests.VisitingHours
{
    /// <summary>The waking days' redesign (docs/redesign/day.md): the duty rounds, the bedside topics, what the dream hears,
    /// and the forced-out hook the nights call.</summary>
    public class DayContentTests
    {
        [Test]
        public void DayOnesTutorialRoundIsShorterThanDayTwos()
        {
            var d1 = DayContent.RoundTasks(1);
            var d2 = DayContent.RoundTasks(2);
            Assert.That(d1.Count, Is.LessThan(d2.Count), "the creator: Day 1's tutorial round a bit shorter than later ones");
            Assert.That(d1.Count, Is.GreaterThanOrEqualTo(2));
            var ids = new HashSet<string>();
            foreach (var t in d1) Assert.That(ids.Add(t.id), Is.True, t.id);
            foreach (var t in d2) Assert.That(ids.Add(t.id), Is.True, t.id);
            foreach (var t in d1) Assert.That(t.day, Is.EqualTo(1));
            foreach (var t in d2) Assert.That(t.day, Is.EqualTo(2));
        }

        [Test]
        public void EveryRoundTaskHasAPlaceToDoIt()
        {
            var spots = new HashSet<string>();
            foreach (var e in SliceContent.Examinables()) spots.Add(e.id);
            foreach (var d in new[] { 1, 2 })
                foreach (var t in DayContent.RoundTasks(d))
                {
                    Assert.That(spots.Contains(t.spot), Is.True, t.id + " is done at " + t.spot + ", which isn't an examinable");
                    Assert.That(t.knot, Does.StartWith("round_"));
                }
        }

        [Test]
        public void TheRoundCountsTasksAndSlips()
        {
            var r = new DutyRound(1);
            Assert.That(r.Complete, Is.False);
            Assert.That(r.Mark("r1_band", false), Is.True);
            Assert.That(r.Mark("r1_band", true), Is.False, "once");
            Assert.That(r.Mark("r2_band", false), Is.False, "not today's");
            Assert.That(r.Mark("r1_obs", true), Is.True);
            Assert.That(r.Slips, Is.EqualTo(1));
            Assert.That(r.Slipped("r1_obs"), Is.True);
            Assert.That(r.Complete, Is.False);
            var lines = new List<string>();
            r.Lines(lines);
            Assert.That(lines.Count, Is.EqualTo(r.Tasks.Count));
            Assert.That(lines[0], Does.EndWith(DutyRound.DoneMark));
            Assert.That(lines[2], Does.Not.EndWith(DutyRound.DoneMark));
            Assert.That(r.Mark("r1_meds", false), Is.True);
            Assert.That(r.Complete, Is.True);
            Assert.That(DayContent.TaskAt(1, "vitals", r), Is.Null, "done: the spot is ordinary again");
            Assert.That(DayContent.TaskAt(1, "vitals", new DutyRound(1)).id, Is.EqualTo("r1_obs"));
        }

        [Test]
        public void TopicsOpenWithTheirFindsAndJustSittingIsAlwaysThere()
        {
            var known = new HashSet<string>();
            var told = new List<string>();
            var open = DayContent.AvailableTopics(1, known.Contains, told);
            Assert.That(open.Count, Is.EqualTo(1));
            Assert.That(DayContent.IsSilence(open[0]), Is.True, "with nothing found, she can still sit with him");
            known.Add(SliceContent.FactEffects);
            known.Add(DayContent.FactLockerPhoto);
            open = DayContent.AvailableTopics(1, known.Contains, told);
            Assert.That(open.ConvertAll(t => t.id), Is.EqualTo(new[] { "walt", "creek", "silence1" }), "found ones first, sitting last");
            told.Add("walt");
            open = DayContent.AvailableTopics(1, known.Contains, told);
            Assert.That(open.ConvertAll(t => t.id), Is.EqualTo(new[] { "creek" }), "once she's spoken, \"just sit\" is gone");
            Assert.That(DayContent.AvailableTopics(2, known.Contains, new List<string>()).Exists(t => t.id == "walt"), Is.False, "Day 1's topics are Day 1's");
        }

        [Test]
        public void EveryTopicChangesTheDreamAndHasItsEntry()
        {
            var book = new JournalBook(SliceContent.Entries(), SliceContent.Pairs());
            var changes = new HashSet<string>();
            foreach (var t in DayContent.Topics())
            {
                Assert.That(DreamChanges.Find(t.change), Is.Not.Null, t.id + ": a choice with no consequence");
                Assert.That(changes.Add(t.change), Is.True, t.id + " makes the same change as another topic");
                Assert.That(book.Find(DayContent.ToldEntry(t.id)), Is.Not.Null, t.id + " has no \"told him\" entry");
                if (!string.IsNullOrEmpty(t.requires)) Assert.That(book.Find(t.requires), Is.Not.Null, t.id + " needs " + t.requires);
            }
            foreach (var d in new[] { 1, 2 })
            {
                int real = 0;
                foreach (var t in DayContent.Topics()) if (t.day == d && !DayContent.IsSilence(t)) real++;
                Assert.That(real, Is.GreaterThan(DayContent.MaxTopicsPerDay), "day " + d + ": more to tell than time to tell it (a real choice)");
            }
        }

        [Test]
        public void TellingHimIsKeptAndTheNightCanAskForIt()
        {
            var save = new SaveData();
            Chain.Reset(save);
            var walt = DayContent.FindTopic("walt");
            Assert.That(Chain.Tell(save, walt), Is.True);
            Assert.That(Chain.Tell(save, walt), Is.False, "once");
            Assert.That(Chain.HasTold(save, "walt"), Is.True);
            Assert.That(Chain.HasDreamChange(save, DreamChanges.WaltLedger), Is.True);
            Assert.That(Chain.Knows(save, DayContent.ToldEntry("walt")), Is.True, "the journal shows what the dream heard");
            Chain.Tell(save, DayContent.FindTopic("sorry"));
            Assert.That(Chain.DreamChangesFor(save, ChainSegment.Night1), Is.EqualTo(new[] { DreamChanges.WaltLedger }));
            Assert.That(Chain.DreamChangesFor(save, ChainSegment.Night2), Is.EqualTo(new[] { DreamChanges.LenaSorry }));
            Chain.Reset(save);
            Assert.That(Chain.DreamChanges(save), Is.Empty, "a new game: the dream hasn't heard anything");
            Assert.That(Chain.Told(save), Is.Empty);
        }

        [Test]
        public void SlipsAndForcedExitsWearHerDown()
        {
            var save = new SaveData();
            Chain.Reset(save);
            Assert.That(Chain.DespairTally(save), Is.EqualTo(0));
            Chain.AddSlips(save, Chain.SlipsForDespair - 1);
            Assert.That(Chain.DespairTally(save), Is.EqualTo(0));
            Chain.AddSlips(save, 1);
            Assert.That(Chain.DespairTally(save), Is.EqualTo(1), "a run's worth of slips: one step down the Long Night's slope");
            Assert.That(Chain.NightsLeft(save), Is.EqualTo(6));
            Chain.RecordForcedOut(save, ChainSegment.Night1, "fell");
            Assert.That(Chain.WasForcedOut(save, ChainSegment.Night1), Is.True);
            Assert.That(Chain.WasForcedOut(save, ChainSegment.Night2), Is.False);
            Assert.That(Chain.ForcedOutReason(save), Is.EqualTo("fell"));
            Assert.That(Chain.NightsLeft(save), Is.EqualTo(5), "maybe six is gone");
            Assert.That(Chain.DespairTally(save), Is.EqualTo(2));
            Chain.RecordForcedOut(save, ChainSegment.Night1, "fell again");
            Assert.That(Chain.ForcedOutCount(save, ChainSegment.Night1), Is.EqualTo(2));
            Chain.Reset(save);
            Assert.That(Chain.Slips(save), Is.EqualTo(0));
            Assert.That(Chain.ForcedOutCount(save), Is.EqualTo(0));
        }

        [Test]
        public void ForceOutReachesTheRunner()
        {
            string heard = null;
            System.Action<string> listener = why => heard = why;
            Chain.ForcedOutRaised += listener;
            try
            {
                Chain.ForceOut("the house tipped her out");
                Assert.That(heard, Is.EqualTo("the house tipped her out"));
            }
            finally
            {
                Chain.ForcedOutRaised -= listener;
            }
        }
    }
}
