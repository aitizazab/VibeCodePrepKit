using System;
using System.Collections.Generic;
using System.IO;
using Path = System.IO.Path;
using Ink.Runtime;
using JamKit.VisitingHours;
using NUnit.Framework;

namespace JamKit.Tests
{
    /// <summary>
    /// The slice's ink, compiled by the real ink compiler and played headless (no Unity), the way
    /// VisitingHoursGame plays it: every knot the game plays exists, each examinable writes the
    /// journal entry it should, every entry the ink records is in the journal's book, the outcome
    /// knots give what the battle promises, and the rushing line and the battle cue are where the
    /// game expects them.
    /// </summary>
    public class VisitingHoursInkTests
    {
        static string InkFile()
        {
            var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets", "JamKit"))) dir = dir.Parent;
            Assert.That(dir, Is.Not.Null, "repo root not found");
            return Path.Combine(dir.FullName, "Assets", "JamKit", "Templates", "VisitingHours", "Dialogue", "VisitingHours.ink");
        }

        static string _json;

        static string Compile()
        {
            if (_json != null) return _json;
            string file = InkFile();
            var errors = new List<string>();
            var compiler = new Ink.Compiler(File.ReadAllText(file), new Ink.Compiler.Options
            {
                countAllVisits = true,
                sourceFilename = Path.GetFileName(file),
                errorHandler = (msg, type) => { if (type == Ink.ErrorType.Error) errors.Add(msg); },
            });
            var story = compiler.Compile();
            Assert.That(errors, Is.Empty, string.Join("\n", errors));
            Assert.That(story, Is.Not.Null);
            _json = story.ToJson();
            return _json;
        }

        static Story NewStory(HashSet<string> known)
        {
            var story = new Story(Compile());
            story.allowExternalFunctionFallbacks = true;
            story.BindExternalFunction("knows", (string id) => (object)known.Contains(id), true);
            story.BindExternalFunction("open_questions", () => (object)0, true);
            return story;
        }

        static bool Has(Story s, string knot)
        {
            try { return s.ContentAtPath(new Ink.Runtime.Path(knot)).correctObj != null; }
            catch (Exception) { return false; }
        }

        /// <summary>Plays a knot to its end (choice <paramref name="choice"/> at each choice); collects lines and tags.</summary>
        static void Play(string knot, List<string> lines, List<string> tags, HashSet<string> known = null, int choice = 0)
        {
            var s = NewStory(known ?? new HashSet<string>());
            s.ChoosePathString(knot);
            for (int guard = 0; guard < 500; guard++)
            {
                while (s.canContinue)
                {
                    string line = s.Continue().Trim();
                    if (line.Length > 0) lines.Add(line);
                    tags.AddRange(s.currentTags);
                }
                if (s.currentChoices.Count == 0) return;
                s.ChooseChoiceIndex(Math.Min(choice, s.currentChoices.Count - 1));
            }
            Assert.Fail(knot + " never ends");
        }

        [Test]
        public void EveryKnotTheGamePlaysExists()
        {
            var s = NewStory(new HashSet<string>());
            foreach (var knot in SliceContent.AllKnots()) Assert.That(Has(s, knot), Is.True, "missing knot " + knot);
        }

        [Test]
        public void EachExaminableWritesItsEntry()
        {
            foreach (var e in SliceContent.Examinables())
            {
                if (string.IsNullOrEmpty(e.records)) continue;
                var lines = new List<string>();
                var tags = new List<string>();
                Play(e.knot, lines, tags);
                Assert.That(tags, Does.Contain("event:vh:record:" + e.records), e.knot);
                Assert.That(lines, Is.Not.Empty, e.knot);
            }
        }

        [Test]
        public void EveryRecordedEntryIsInTheBook()
        {
            var book = new JournalBook(SliceContent.Entries(), SliceContent.Pairs());
            var known = new HashSet<string>(SliceContent.KnownBeforeWalt());
            foreach (var knot in SliceContent.AllKnots())
                for (int choice = 0; choice < 2; choice++)
                {
                    var lines = new List<string>();
                    var tags = new List<string>();
                    Play(knot, lines, tags, known, choice);
                    foreach (var t in tags)
                    {
                        if (t.StartsWith("event:vh:record:", StringComparison.Ordinal))
                            Assert.That(book.Find(t.Substring("event:vh:record:".Length)), Is.Not.Null, knot + ": " + t);
                        if (t.StartsWith("portrait:", StringComparison.Ordinal))
                        {
                            var parts = t.Substring("portrait:".Length).Split(' ');
                            Assert.That(parts[0], Is.EqualTo("Mags").Or.EqualTo("Walt"), knot + ": " + t);
                            if (parts.Length > 1 && parts[1] != "hide" && parts[1] != "left" && parts[1] != "right")
                                Assert.That(parts[1], Is.EqualTo("neutral").Or.EqualTo("sad").Or.EqualTo("firm"), knot + ": " + t);
                        }
                    }
                }
        }

        [Test]
        public void TheOutcomeKnotsGiveWhatTheBattlePromises()
        {
            var battle = SliceContent.WaltBattle();
            var lines = new List<string>();
            var tags = new List<string>();
            Play(SliceContent.OutcomeKnot(BattleOutcome.Convinced), lines, tags);
            Assert.That(tags, Does.Contain("event:vh:record:" + battle.convincedEntry));
            tags.Clear();
            Play(SliceContent.OutcomeKnot(BattleOutcome.HeardOut), lines, tags);
            Assert.That(tags, Does.Contain("event:vh:record:" + battle.heardOutEntry));
            tags.Clear();
            Play(SliceContent.OutcomeKnot(BattleOutcome.Pressed), lines, tags);
            Assert.That(tags.Exists(t => t.StartsWith("event:vh:record:", StringComparison.Ordinal)), Is.False, "pressing gives nothing");
            Assert.That(SliceContent.OutcomeKnot(BattleOutcome.ComposureLost), Is.EqualTo(SliceContent.KnotLost));
        }

        [Test]
        public void TheRushingLineLetsYouGoAnyway()
        {
            var lines = new List<string>();
            var tags = new List<string>();
            Play(SliceContent.KnotWaltRushing, lines, tags, null, 0);
            Assert.That(lines[0], Does.Contain("I don't know enough yet."));
            Assert.That(tags, Does.Contain("event:vh:battle"), "the first choice goes to him anyway: never a hard block");
            lines.Clear();
            tags.Clear();
            Play(SliceContent.KnotWaltRushing, lines, tags, null, 1);
            Assert.That(tags, Does.Contain("event:vh:questions"), "\"Not yet\" opens the open questions");
            Assert.That(tags, Does.Not.Contain("event:vh:battle"));
        }

        [Test]
        public void MagsTeachesTheRigAndTheJournalThenDives()
        {
            var lines = new List<string>();
            var tags = new List<string>();
            Play(SliceContent.KnotBooth, lines, tags);
            Assert.That(tags, Does.Contain("event:vh:open_journal"));
            Assert.That(string.Join(" ", lines), Does.Contain("WASD").And.Contain("E to look"));
            tags.Clear();
            Play(SliceContent.KnotBoothAfterJournal, lines, tags);
            Assert.That(tags, Does.Contain("event:vh:dive"));
            Assert.That(string.Join(" ", lines), Does.Contain("Let's not do that tonight."));
        }

        [Test]
        public void TheStairwellWaitsForTheChart()
        {
            var lines = new List<string>();
            var tags = new List<string>();
            Play("stairwell", lines, tags);
            Assert.That(tags, Does.Not.Contain("event:vh:to_booth"), "chart him first");
            Play("stairwell", lines, tags, new HashSet<string> { SliceContent.FactChart });
            Assert.That(tags, Does.Contain("event:vh:to_booth"));
        }

        [Test]
        public void ThePoliceReportKnotSaysNothingAboutTheCalls()
        {
            var lines = new List<string>();
            var tags = new List<string>();
            Play("examine_police_report", lines, tags);
            string all = string.Join(" ", lines);
            Assert.That(all, Does.Not.Contain("5:52").And.Not.Contain("Lena").And.Not.Contain("call"), "no comment on the call log, from anyone");
        }
    }
}
