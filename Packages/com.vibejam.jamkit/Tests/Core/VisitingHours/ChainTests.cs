using JamKit.Flow;
using JamKit.VisitingHours;
using NUnit.Framework;

namespace JamKit.Tests.VisitingHours
{
    /// <summary>The chain from Day 1 to the end card (docs/NIGHTS-1-2.md): its order, which segments are waking or
    /// nights, and what it carries in the save (the journal, Force, the outcomes).</summary>
    public class ChainTests
    {
        [Test]
        public void TheChainRunsDayNightDayNightEnd()
        {
            var s = ChainSegment.Day1;
            var order = new[] { ChainSegment.Night1, ChainSegment.Day2, ChainSegment.Night2, ChainSegment.End, ChainSegment.End };
            foreach (var expect in order)
            {
                s = Chain.Next(s);
                Assert.That(s, Is.EqualTo(expect));
            }
            Assert.That(Chain.IsWaking(ChainSegment.Day1) && Chain.IsWaking(ChainSegment.Day2) && Chain.IsWaking(ChainSegment.End), Is.True);
            Assert.That(Chain.IsNight(ChainSegment.Night1) && Chain.IsNight(ChainSegment.Night2), Is.True);
            Assert.That(Chain.NightScenes(ChainSegment.Night2), Does.Contain("DreamHouse"));
            Assert.That(Chain.NightScenes(ChainSegment.Night1), Does.Contain("DreamEscape"), "the escape stands in until Larch Street is built");
            Assert.That(Chain.NightScenes(ChainSegment.Day1), Is.Empty);
            Assert.That(Chain.TryParse("night2", out var parsed) && parsed == ChainSegment.Night2, Is.True);
            Assert.That(Chain.TryParse("tuesday", out _), Is.False);
        }

        [Test]
        public void NightTwoStartsOnTheHillThenTheHouse()
        {
            Assert.That(Chain.NightScenes(ChainSegment.Night2), Is.EqualTo(new[] { "DreamStreet", "DreamHouse" }), "World's request: the hill first");
            Assert.That(Chain.NightOnTheHill(ChainSegment.Night2, "DreamStreet"), Is.EqualTo(2));
            Assert.That(Chain.NightOnTheHill(ChainSegment.Night1, "DreamStreet"), Is.EqualTo(1), "Night 1 still tells the hill it's Night 1");
            Assert.That(Chain.NightOnTheHill(ChainSegment.Night2, "DreamHouse"), Is.EqualTo(0), "the house plays its own night");
            Assert.That(Chain.NightOnTheHill(ChainSegment.Night1, "DreamEscape"), Is.EqualTo(0));
        }

        [Test]
        public void TheThrownOutFlagAndLenasWatchBelongToOneRun()
        {
            var save = new SaveData();
            Chain.Reset(save);
            Assert.That(Chain.ThrownOutOfNight2(save), Is.False);
            Assert.That(Chain.HasLenaWatch(save), Is.False);
            save.SetFlag(Chain.ThrownOutFlag);
            Assert.That(Chain.ThrownOutOfNight2(save), Is.True);
            Chain.Record(save, JamKit.Argument.DreamLenaDemo.Planisphere);
            Assert.That(Chain.HasLenaWatch(save), Is.True, "the keepsake's journal entry carries the watch");
            Chain.Reset(save);
            Assert.That(Chain.ThrownOutOfNight2(save), Is.False, "a new run forgets the last run's fall");
            Assert.That(Chain.HasLenaWatch(save), Is.False);
            save.SetFlag(Chain.KeepsakeWatchFlag);
            Assert.That(Chain.HasLenaWatch(save), Is.True, "or the flag itself");
        }

        [Test]
        public void TheSaveCarriesTheJournalAndTheTallies()
        {
            var save = new SaveData();
            Chain.Reset(save);
            Assert.That(Chain.Segment(save), Is.EqualTo(ChainSegment.Day1));
            Assert.That(Chain.Record(save, SliceContent.FactChart), Is.True);
            Assert.That(Chain.Record(save, SliceContent.FactChart), Is.False, "once");
            Assert.That(Chain.Record(save, SliceContent.MemWaltAlive), Is.True);
            Assert.That(Chain.Knows(save, SliceContent.MemWaltAlive), Is.True);
            Assert.That(Chain.Known(save), Is.EqualTo(new[] { SliceContent.FactChart, SliceContent.MemWaltAlive }));
            Chain.AddForce(save, 2);
            Chain.AddForce(save, 1);
            Assert.That(Chain.Force(save), Is.EqualTo(3));
            Chain.SetLenaOutcome(save, "Heard out");
            Chain.SetWaltOutcome(save, "Convinced");
            Chain.SetSegment(save, ChainSegment.Night2);
            Assert.That(Chain.Segment(save), Is.EqualTo(ChainSegment.Night2));
            Assert.That(Chain.LenaOutcome(save), Is.EqualTo("Heard out"));
            Assert.That(Chain.WaltOutcome(save), Is.EqualTo("Convinced"));
            Assert.That(save.GetEntry("vh_dream_journal").Contains(SliceContent.FactChart), Is.True);
            save.SetFlag(Chain.DespairFlag);
            Assert.That(Chain.Despair(save), Is.True);
            Chain.Reset(save);
            Assert.That(Chain.Despair(save), Is.False, "a new game doesn't inherit the last run's Despair");
            Assert.That(Chain.Known(save), Is.Empty, "a new game starts with an empty journal");
            Assert.That(Chain.Force(save), Is.EqualTo(0));
            Assert.That(Chain.LenaOutcome(save), Is.Empty);
            Assert.That(Chain.Join(new[] { "a", " b", "a", "" }), Is.EqualTo("a,b"));
        }

        [Test]
        public void ForceAndDespairAreTalliedFromTheBattlesAcrossTheNights()
        {
            var save = new SaveData();
            Chain.Reset(save);
            Chain.AddForce(save, 1);
            var ledger = new JamKit.Argument.ArgumentLedger();
            ledger.Set(new JamKit.Argument.ArgumentRecord { id = JamKit.Argument.ArgumentBattles.Walt, outcome = JamKit.Argument.ArgumentOutcome.Pressed, stance = JamKit.Argument.ArgumentStance.Unsure, presses = 2 });
            save.SetEntry(Chain.ArgumentsKey, ledger.Serialize());
            Assert.That(Chain.ForceTally(save), Is.EqualTo(3), "the slice's 1 and Walt's two presses");
            Assert.That(Chain.DespairTally(save), Is.EqualTo(0));
            ledger.Set(new JamKit.Argument.ArgumentRecord { id = JamKit.Argument.ArgumentBattles.Walt, outcome = JamKit.Argument.ArgumentOutcome.Convinced, stance = JamKit.Argument.ArgumentStance.Amenable, presses = 0 });
            ledger.Set(new JamKit.Argument.ArgumentRecord { id = JamKit.Argument.ArgumentBattles.Lena, outcome = JamKit.Argument.ArgumentOutcome.Pressed, stance = JamKit.Argument.ArgumentStance.Condemns, presses = 1 });
            save.SetEntry(Chain.ArgumentsKey, ledger.Serialize());
            Assert.That(Chain.ForceTally(save), Is.EqualTo(2), "Walt played again replaces his share");
            Assert.That(Chain.DespairTally(save), Is.EqualTo(JamKit.Argument.ArgumentBattles.DespairOnCondemn), "Lena condemning");
            Assert.That(Chain.BattleResult(save, JamKit.Argument.ArgumentBattles.Lena).Condemns, Is.True);
            save.SetFlag(Chain.DespairFlag);
            Assert.That(Chain.DespairTally(save), Is.EqualTo(JamKit.Argument.ArgumentMeters.DespairMax), "into the rain without the umbrella");
            Chain.Record(save, SliceContent.FactChart);
            var ctx = Chain.BattleContext(save, new[] { "mem_x" });
            Assert.That(ctx.force, Is.EqualTo(2));
            Assert.That(ctx.despair, Is.EqualTo(JamKit.Argument.ArgumentMeters.DespairMax));
            Assert.That(ctx.found, Is.EqualTo(new[] { SliceContent.FactChart, "mem_x" }));
            Chain.Reset(save);
            Assert.That(Chain.ForceTally(save), Is.EqualTo(0));
            Assert.That(Chain.DespairTally(save), Is.EqualTo(0));
        }

        [Test]
        public void AnySceneCanSayItsSegmentIsDone()
        {
            Chain.ClearListeners();
            int heard = 0;
            System.Action listen = () => heard++;
            Chain.SegmentDone += listen;
            Chain.Done();
            Chain.SegmentDone -= listen;
            Chain.Done();
            Assert.That(heard, Is.EqualTo(1));
        }
    }
}
