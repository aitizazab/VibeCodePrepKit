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

        /// <summary>The game's state as the ink asks it (vh_state): "round_done", "sat", "told_any"... (the Day agent's).</summary>
        static HashSet<string> _states = new HashSet<string>();

        static Story NewStory(HashSet<string> known)
        {
            var story = new Story(Compile());
            story.allowExternalFunctionFallbacks = true;
            story.BindExternalFunction("knows", (string id) => (object)known.Contains(id), true);
            story.BindExternalFunction("open_questions", () => (object)0, true);
            var states = _states;
            story.BindExternalFunction("vh_state", (string name) => (object)states.Contains(name), true);
            story.BindExternalFunction("vh_topic", (string id) => (object)true, true);
            return story;
        }

        [SetUp]
        public void ClearStates() => _states = new HashSet<string>();

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
        public void MagsSaysTheCreatorsSceneThenTheJournalThenDives()
        {
            var lines = new List<string>();
            var tags = new List<string>();
            Play(SliceContent.KnotBooth, lines, tags);
            string said = string.Join(" ", lines);
            // The creator's Mags scene (docs/STORY-PROSE.md 83-92), word for word.
            Assert.That(said, Does.Contain("Dream Walking isn't that easy, you know."));
            Assert.That(said, Does.Contain("Only way I'd have it. You gonna follow me in?"));
            Assert.That(said, Does.Contain("I'd give you Five Nights, maybe Six"));
            Assert.That(said, Does.Contain("only the Truth is on your side"));
            Assert.That(said, Does.Contain("stay calm"));
            Assert.That(said, Does.Not.Contain("charg").IgnoreCase, "the phone-charging ritual is dropped");
            Assert.That(tags, Does.Contain("event:vh:open_journal"));
            tags.Clear();
            Play(SliceContent.KnotBoothAfterJournal, lines, tags);
            Assert.That(tags, Does.Contain("event:vh:dive"));
            tags.Clear();
            Play(SliceContent.KnotBooth2, lines, tags);
            Assert.That(tags, Does.Contain("event:vh:dive"), "Day 2's booth dives too");
        }

        [Test]
        public void DayTwoAsksDidLenaGetOutAndTheVisitorAsksWhatHeSaid()
        {
            var lines = new List<string>();
            var tags = new List<string>();
            Play("day2_rafael", lines, tags);
            Assert.That(string.Join(" ", lines), Does.Contain("Did Lena get out?"));
            lines.Clear();
            tags.Clear();
            Play("day2_visitor", lines, tags);
            Assert.That(string.Join(" ", lines), Does.Contain("Community Partnerships").And.Contain("Has he said anything?"));
            Assert.That(string.Join(" ", lines), Does.Not.Contain("transfer").IgnoreCase, "no transfer clock");
            // LeBaget's representative is a woman now (the creator, 2026-10-13): Corinne Vasseur; the man is gone.
            Assert.That(string.Join(" ", lines), Does.Contain("Corinne Vasseur"));
            Assert.That(string.Join(" ", lines), Does.Not.Contain("Daniel Arce"));
            Assert.That(tags, Does.Contain("speaker:The woman from LeBaget"));
            Assert.That(tags, Does.Not.Contain("speaker:The man from LeBaget"));
            tags.Clear();
            Play("day2_stairwell", lines, tags);
            Assert.That(tags, Does.Not.Contain("event:vh:to_booth"), "the round first (the Day agent's redesign)");
            _states.Add("round_done");
            Play("day2_stairwell", lines, tags);
            Assert.That(tags, Does.Contain("event:vh:to_booth"));
            Play("day1_phone", lines, tags);
            Assert.That(string.Join(" ", lines), Does.Contain("Not now."));
        }

        /// <summary>The booth plays the House agent's thrown-out lines (DreamHouseLines.ThrownOutBooth), word for word, in the
        /// same order: they're read out of DreamHouseStory.cs and matched against the knot's lines.</summary>
        [Test]
        public void TheThrownOutBoothIsTheHousesLinesWordForWord()
        {
            var dir = new DirectoryInfo(Path.GetDirectoryName(InkFile()));
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Packages"))) dir = dir.Parent;
            Assert.That(dir, Is.Not.Null);
            string src = File.ReadAllText(Path.Combine(dir.FullName, "Packages", "com.vibejam.jamkit", "Runtime", "Core", "DreamEscape", "DreamHouseStory.cs"));
            int at = src.IndexOf("ThrownOutBooth =", StringComparison.Ordinal);
            Assert.That(at, Is.GreaterThan(0), "DreamHouseLines.ThrownOutBooth");
            int end = src.IndexOf("};", at, StringComparison.Ordinal);
            var house = new List<string>();
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(src.Substring(at, end - at), "new HouseLine\\((\\w+), \"((?:[^\"\\\\]|\\\\.)*)\"\\)"))
                house.Add(m.Groups[1].Value + "|" + m.Groups[2].Value.Replace("\\\"", "\""));
            Assert.That(house.Count, Is.EqualTo(4));

            var lines = new List<string>();
            var tags = new List<string>();
            var s = NewStory(new HashSet<string>());
            s.ChoosePathString(VisitingHoursGame_KnotThrownOutBooth);
            var said = new List<string>();
            string speaker = "";
            while (s.canContinue)
            {
                string line = s.Continue().Trim();
                foreach (var t in s.currentTags) if (t.StartsWith("speaker:")) speaker = t.Substring(8).Trim();
                if (line.Length > 0) said.Add(speaker + "|" + line);
            }
            Assert.That(said, Is.EqualTo(house), "the booth's knot is the house's lines, in order, with their speakers");
        }

        /// <summary>VisitingHoursGame.KnotThrownOutBooth (the game's Unity side isn't compiled here).</summary>
        const string VisitingHoursGame_KnotThrownOutBooth = "booth_thrown_out";

        [Test]
        public void TheStairwellWaitsForTheRound()
        {
            var lines = new List<string>();
            var tags = new List<string>();
            Play("stairwell", lines, tags);
            Assert.That(tags, Does.Not.Contain("event:vh:to_booth"), "the duty round first (docs/redesign/day.md)");
            _states.Add("round_done");
            Play("stairwell", lines, tags);
            Assert.That(tags, Does.Contain("event:vh:to_booth"));
        }

        [Test]
        public void EveryRoundChoiceEndsTheTaskOkOrWithASlip()
        {
            foreach (var day in new[] { 1, 2 })
                foreach (var task in DayContent.RoundTasks(day))
                {
                    bool sawOk = false, sawSlip = false;
                    for (int choice = 0; choice < 3; choice++)
                    {
                        var lines = new List<string>();
                        var tags = new List<string>();
                        Play(task.knot, lines, tags, null, choice);
                        bool ok = tags.Contains("event:vh:round:" + task.id + ":ok");
                        bool slip = tags.Contains("event:vh:round:" + task.id + ":slip");
                        Assert.That(ok ^ slip, Is.True, task.knot + " choice " + choice + ": exactly one of ok and slip");
                        sawOk |= ok;
                        sawSlip |= slip;
                    }
                    Assert.That(sawOk && sawSlip, Is.True, task.knot + ": a right answer and a slip to catch");
                }
        }

        [Test]
        public void EachTopicTellsHimAndHisBodyAnswers()
        {
            foreach (var topic in DayContent.Topics())
            {
                var lines = new List<string>();
                var tags = new List<string>();
                Play(topic.knot, lines, tags);
                string told = DayContent.IsSilence(topic) ? "silence" : topic.id;
                Assert.That(tags, Does.Contain("event:vh:told:" + told), topic.knot);
                string body = topic.response == BedsideResponse.Fingers ? "fingers" : topic.response == BedsideResponse.Heart ? "heart" : "calm";
                Assert.That(tags, Does.Contain("event:vh:elias:" + body), topic.knot + " should answer with " + body);
                Assert.That(lines.Count, Is.GreaterThanOrEqualTo(2), topic.knot);
            }
            var l2 = new List<string>();
            var t2 = new List<string>();
            Play("bedside_enough", l2, t2);
            Assert.That(t2, Does.Contain("event:vh:bedside:rise"));
        }

        [Test]
        public void TheBedsideOffersEveryOpenTopicAndMagsSaysWhatItDid()
        {
            var s = NewStory(new HashSet<string>());
            s.ChoosePathString("bedside_intro");
            while (s.canContinue) s.Continue();
            var choices = s.currentChoices.ConvertAll(c => c.text);
            foreach (var topic in DayContent.Topics())
                Assert.That(choices, Does.Contain(topic.label), "the choice for " + topic.id);
            var lines = new List<string>();
            var tags = new List<string>();
            _states.Add("told_any");
            Play("booth_heard", lines, tags);
            Assert.That(string.Join(" ", lines), Does.Contain("Whatever you told him"));
            Assert.That(tags, Does.Contain("event:vh:dive"));
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
