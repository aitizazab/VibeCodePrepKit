using System.Collections.Generic;
using JamKit.VisitingHours;
using NUnit.Framework;

namespace JamKit.Tests.VisitingHours
{
    public class JournalTests
    {
        static Journal NewJournal() => new Journal(new JournalBook(SliceContent.Entries(), SliceContent.Pairs()));

        [Test]
        public void RecordAddsAnEntryOnceAndRefusesUnknownIds()
        {
            var j = NewJournal();
            Assert.That(j.Record(SliceContent.FactChart), Is.True);
            Assert.That(j.Record(SliceContent.FactChart), Is.False, "already there");
            Assert.That(j.Record("fact_nonsense"), Is.False, "not in the book");
            Assert.That(j.Count, Is.EqualTo(1));
            Assert.That(j.Knows(SliceContent.FactChart), Is.True);
            Assert.That(j.Knows(SliceContent.FactPhoto), Is.False);
        }

        [Test]
        public void TheRightPairMakesItsDeductionInEitherOrder()
        {
            var j = NewJournal();
            j.Record(SliceContent.MemWaltAlive);
            j.Record(SliceContent.FactEffects);
            Assert.That(j.Combine(SliceContent.MemWaltAlive, SliceContent.FactEffects, out string made), Is.EqualTo(CombineResult.Made));
            Assert.That(made, Is.EqualTo(SliceContent.DedWaltDead));
            Assert.That(j.Knows(SliceContent.DedWaltDead), Is.True);
            Assert.That(j.Combine(SliceContent.FactEffects, SliceContent.MemWaltAlive, out made), Is.EqualTo(CombineResult.AlreadyMade));
            Assert.That(made, Is.EqualTo(SliceContent.DedWaltDead));
        }

        [Test]
        public void WrongPairsUnknownEntriesAndTheSameEntryTwiceMakeNothing()
        {
            var j = NewJournal();
            j.Record(SliceContent.FactChart);
            j.Record(SliceContent.FactPhoto);
            int before = j.Count;
            Assert.That(j.Combine(SliceContent.FactChart, SliceContent.FactPhoto, out _), Is.EqualTo(CombineResult.NoMatch));
            Assert.That(j.Combine(SliceContent.FactChart, SliceContent.MemSaturday, out _), Is.EqualTo(CombineResult.NotKnown));
            Assert.That(j.Combine(SliceContent.FactChart, SliceContent.FactChart, out _), Is.EqualTo(CombineResult.Same));
            Assert.That(j.Count, Is.EqualTo(before));
        }

        [Test]
        public void OpenQuestionsAreThePairsWithBothHalvesKnownAndNotCombined()
        {
            var j = NewJournal();
            var open = new List<OpenQuestion>();
            j.Record(SliceContent.FactEffects);
            Assert.That(j.OpenQuestions(open), Is.EqualTo(0), "only one half known");
            Assert.That(j.HasOpenQuestions, Is.False);

            j.Record(SliceContent.MemWaltAlive);
            Assert.That(j.OpenQuestions(open), Is.EqualTo(1));
            Assert.That(open[0].result, Is.EqualTo(SliceContent.DedWaltDead));
            Assert.That(open[0].question, Does.Contain("1941"));
            Assert.That(j.HasOpenQuestions, Is.True, "the rushing check fires");
            Assert.That(j.IsInOpenQuestion(SliceContent.FactEffects), Is.True);
            Assert.That(j.IsInOpenQuestion(SliceContent.FactChart), Is.False);

            j.Record(SliceContent.MemNewspaper);
            j.Record(SliceContent.MemSaturday);
            Assert.That(j.OpenQuestions(open), Is.EqualTo(2));

            j.Combine(SliceContent.FactEffects, SliceContent.MemWaltAlive, out _);
            j.Combine(SliceContent.MemNewspaper, SliceContent.MemSaturday, out _);
            Assert.That(j.OpenQuestions(open), Is.EqualTo(0));
            Assert.That(j.HasOpenQuestions, Is.False, "nothing left to work out: no rushing line");
        }

        [Test]
        public void EntriesOfAKindComeInTheOrderFound()
        {
            var j = NewJournal();
            j.Record(SliceContent.FactPhoto);
            j.Record(SliceContent.MemSaturday);
            j.Record(SliceContent.FactChart);
            var list = new List<JournalEntryDef>();
            Assert.That(j.EntriesOfKind(EntryKind.Fact, list), Is.EqualTo(2));
            Assert.That(list[0].id, Is.EqualTo(SliceContent.FactPhoto));
            Assert.That(list[1].id, Is.EqualTo(SliceContent.FactChart));
            Assert.That(j.EntriesOfKind(EntryKind.Memory, list), Is.EqualTo(1));
            int rev = j.Revision;
            j.Record(SliceContent.FactEffects);
            Assert.That(j.Revision, Is.GreaterThan(rev), "the UI sees the change");
        }

        [Test]
        public void TheSlicesContentIsConsistent()
        {
            var book = new JournalBook(SliceContent.Entries(), SliceContent.Pairs());
            var problems = new List<string>();
            book.Validate(problems);
            Assert.That(problems, Is.Empty, string.Join("\n", problems));

            var battle = SliceContent.WaltBattle();
            Assert.That(book.Find(battle.convincedEntry).kind, Is.EqualTo(EntryKind.Truth));
            Assert.That(book.Find(battle.heardOutEntry).kind, Is.EqualTo(EntryKind.Personal));
            Assert.That(battle.statements.Count, Is.EqualTo(4));
            foreach (var s in battle.statements)
            {
                if (!string.IsNullOrEmpty(s.key))
                {
                    Assert.That(book.Find(s.key), Is.Not.Null, s.text);
                    Assert.That(s.offers, Does.Contain(s.key), "the key drifts in during its own statement: " + s.text);
                    Assert.That(s.crackLine, Is.Not.Empty, s.text);
                }
                foreach (var o in s.offers) Assert.That(book.Find(o), Is.Not.Null, o);
                Assert.That(s.volleys, Is.Not.Empty, s.text);
            }
            Assert.That(battle.statements[2].key, Is.EqualTo(SliceContent.DedWaltDead), "the key statement");
            Assert.That(battle.statements[2].optional, Is.False);
            Assert.That(battle.statements[3].optional, Is.True, "the spiral's crack is a bonus");

            Assert.That(book.Find(SliceContent.FactPoliceReport).scribble, Is.EqualTo("I should have"), "the one scribbled-out line");
            Assert.That(book.Find(SliceContent.FactPoliceReport).text, Does.Contain("3:03").And.Contain("3:06").And.Contain("Lasname"), "the call log is shown (3:03 Nadia, unanswered, as on her phone; 3:06 Lena, 2 minutes)");
            Assert.That(book.Find(SliceContent.FactChart).text, Does.StartWith("Charting:"), "Nadia charts");
        }
    }
}
