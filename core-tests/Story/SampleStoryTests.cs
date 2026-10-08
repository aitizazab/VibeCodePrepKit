using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Path = System.IO.Path;
using Ink.Runtime;
using JamKit.Narrative;
using NUnit.Framework;

namespace JamKit.Tests
{
    /// <summary>
    /// The story template's sample ink, compiled by the real ink compiler and played headless (no
    /// Unity): it compiles without errors, every tag is a known story tag with valid arguments, a
    /// first-choices playthrough (with the office scene played by its walkthrough) reaches an ending,
    /// every ending is reachable, and the secret ending only appears on a second playthrough.
    /// Ids are checked against the sample's ids here; the EditMode tests check them against the real
    /// data assets. Another folder of ink: dotnet test ... -- TestRunParameters.Parameter(name=\"inkDir\", value=\"path\")
    /// </summary>
    public class SampleStoryTests
    {
        static string InkDir()
        {
            string given = TestContext.Parameters.Get("inkDir", "");
            if (!string.IsNullOrEmpty(given)) return given;
            var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets", "JamKit"))) dir = dir.Parent;
            Assert.That(dir, Is.Not.Null, "repo root not found");
            return Path.Combine(dir.FullName, "Assets", "JamKit", "Templates", "Story", "Dialogue");
        }

        sealed class FolderFiles : Ink.IFileHandler
        {
            readonly string _dir;
            public FolderFiles(string dir) { _dir = dir; }
            public string ResolveInkFilename(string includeName) => Path.Combine(_dir, includeName);
            public string LoadInkFileContents(string fullFilename) => File.ReadAllText(fullFilename);
        }

        static string _json;
        static readonly List<string> Warnings = new List<string>();

        static string Compile()
        {
            if (_json != null) return _json;
            string dir = InkDir();
            var errors = new List<string>();
            var compiler = new Ink.Compiler(File.ReadAllText(Path.Combine(dir, "LastTrain.ink")), new Ink.Compiler.Options
            {
                countAllVisits = true,
                sourceFilename = "LastTrain.ink",
                fileHandler = new FolderFiles(dir),
                errorHandler = (msg, type) => { if (type == Ink.ErrorType.Error) errors.Add(msg); else Warnings.Add(msg); },
            });
            var story = compiler.Compile();
            Assert.That(errors, Is.Empty, string.Join("\n", errors));
            Assert.That(story, Is.Not.Null);
            _json = story.ToJson();
            return _json;
        }

        /// <summary>The sample's ids (Templates/Story/Editor/StoryContent.cs makes the same).</summary>
        static StoryIds SampleIds()
        {
            var ids = new StoryIds { checkSounds = false };
            foreach (var b in new[] { "platform_night", "waiting_room", "office", "platform_dawn", "train_interior", "street_rain", "black", "white" }) ids.backgrounds.Add(b);
            foreach (var c in new[] { "cg_photo", "cg_train", "cg_umbrella", "cg_ticket" }) ids.cgs.Add(c);
            foreach (var e in new[] { "last_train", "missed_connection", "lost_property", "return_ticket" }) ids.endings.Add(e);
            foreach (var i in new[] { "brass_key", "ticket", "photograph" }) ids.items.Add(i);
            ids.hotspotScenes.Add("office");
            ids.palette.Add("accent");
            ids.AddCharacter("iris", "Iris", new[] { "neutral", "smile", "sad", "surprised", "knowing" });
            ids.AddCharacter("bram", "Bram", new[] { "neutral", "stern", "laugh" });
            foreach (var k in Walkthrough.Values) ids.dataKnots.Add(k);
            ids.dataKnots.Add("office_drawer");
            ids.dataKnots.Add("office_desk");
            ids.dataKnots.Add("office_board");
            ids.dataKnots.Add("office_window");
            ids.dataKnots.Add("office_no_use");
            return ids;
        }

        /// <summary>The office walkthrough (Office.asset), as the knots it runs.</summary>
        static readonly Dictionary<string, string> Walkthrough = new Dictionary<string, string>
        {
            { "coat", "office_coat" },
            { "brass_key>desk_drawer", "office_drawer_key" },
            { "shelves", "office_shelves" },
            { "door", "office_door" },
        };

        static IEnumerable<KeyValuePair<string, string>> Files()
        {
            foreach (var f in Directory.GetFiles(InkDir(), "*.ink"))
                yield return new KeyValuePair<string, string>(Path.GetFileName(f), File.ReadAllText(f));
        }

        sealed class Run
        {
            public readonly List<string> lines = new List<string>();
            public readonly List<string> tags = new List<string>();
            public readonly List<string> choicesSeen = new List<string>();
            public string ending = "";
            public int choices;
            public int words;
        }

        /// <summary>Plays the story like the game would: lines, tags, choices by <paramref name="choose"/>,
        /// the office by its walkthrough. Memory functions come from <paramref name="memory"/> (or ink's fallbacks).</summary>
        static Run Play(Func<List<Choice>, int> choose, StoryMemory memory = null, int maxSteps = 5000)
        {
            var story = new Story(Compile());
            story.allowExternalFunctionFallbacks = true;
            if (memory != null) BindMemory(story, memory);
            var run = new Run();
            bool investigating = false;
            var steps = new Queue<string>();
            for (int i = 0; i < maxSteps; i++)
            {
                if (story.canContinue)
                {
                    string line = story.Continue().Trim();
                    foreach (var t in story.currentTags)
                    {
                        run.tags.Add(t);
                        var cmd = StoryCommand.Parse(t);
                        if (cmd.kind == StoryCommandKind.Investigate) { investigating = true; steps = new Queue<string>(Walkthrough.Values); }
                        if (cmd.kind == StoryCommandKind.Leave) investigating = false;
                        if (cmd.kind == StoryCommandKind.Ending) run.ending = cmd.id;
                    }
                    if (line.Length > 0)
                    {
                        run.lines.Add(line);
                        run.words += line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length;
                    }
                    continue;
                }
                if (story.currentChoices.Count > 0)
                {
                    foreach (var c in story.currentChoices) run.choicesSeen.Add(c.text);
                    int pick = choose(story.currentChoices);
                    run.choices++;
                    story.ChooseChoiceIndex(pick);
                    continue;
                }
                if (investigating && steps.Count > 0)
                {
                    story.ChoosePathString(steps.Dequeue());
                    continue;
                }
                break;
            }
            return run;
        }

        static void BindMemory(Story story, StoryMemory m)
        {
            story.BindExternalFunction("playthrough_count", () => (object)Math.Max(1, m.playthroughs), true);
            story.BindExternalFunction("endings_seen", () => (object)m.DistinctEndings, true);
            story.BindExternalFunction("seen_ending", (string id) => (object)m.SeenEnding(id), true);
            story.BindExternalFunction("last_ending", () => (object)(m.lastEnding ?? ""), true);
            story.BindExternalFunction("remember", (string key, object value) => m.Remember(key, value));
            story.BindExternalFunction("recall", (string key) => m.Recall(key) ?? (object)0, true);
            story.BindExternalFunction("knows", (string key) => (object)m.Knows(key), true);
            story.BindExternalFunction("visited_ever", (string knot) => (object)m.VisitedEver(knot), true);
            story.BindExternalFunction("times_quit", () => (object)m.timesQuit, true);
            story.BindExternalFunction("loads", () => (object)m.loadsThisRun, true);
            story.BindExternalFunction("rewinds", () => (object)m.rewindsThisRun, true);
            story.BindExternalFunction("was_reloaded", () => (object)(m.loadsThisRun > 0), true);
        }

        static int First(List<Choice> c) => 0;

        static Func<List<Choice>, int> Prefer(params string[] words) => choices =>
        {
            foreach (var w in words)
                for (int i = 0; i < choices.Count; i++)
                    if (choices[i].text.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0) return i;
            return 0;
        };

        [Test]
        public void CompilesWithoutErrors()
        {
            Compile();
            foreach (var w in Warnings) TestContext.Out.WriteLine("ink warning: " + w);
        }

        [Test]
        public void ValidatesAgainstTheSampleIds()
        {
            var problems = StoryValidator.Validate(InkScan.Read(Files()), SampleIds());
            foreach (var w in problems.Warnings()) TestContext.Out.WriteLine(w);
            Assert.That(problems.Errors(), Is.Empty, problems.ToString());
        }

        [Test]
        public void FirstChoicesReachAnEndingThroughTheOffice()
        {
            var run = Play(First);
            Assert.That(run.ending, Is.EqualTo("last_train"), "first choices end on the train");
            Assert.That(run.tags, Does.Contain("investigate:office"), "the office scene opened");
            Assert.That(run.tags, Does.Contain("leave"), "and closed");
            foreach (var t in run.tags)
            {
                var cmd = StoryCommand.Parse(t);
                Assert.That(cmd.kind, Is.Not.EqualTo(StoryCommandKind.Unknown), "#" + t);
                Assert.That(cmd.error, Is.Null, "#" + t);
            }
            Assert.That(run.choicesSeen.Any(c => c.Contains("done this before")), Is.False, "no secret choice on a first playthrough");
            // Reading speed ~200 words a minute plus ~3 s a line for the text to type and the click,
            // plus the office (~2 minutes).
            double minutes = run.words / 200.0 + run.lines.Count * 3.0 / 60.0 + 2.0;
            TestContext.Out.WriteLine($"first-choices route: {run.lines.Count} lines, {run.words} words, {run.choices} choices, {run.tags.Count} tags, about {minutes:0.0} minutes");
            Assert.That(run.lines.Count, Is.GreaterThan(60), "a real story's worth of lines");
        }

        [Test]
        public void EveryNormalEndingIsReachable()
        {
            Assert.That(Play(Prefer("Get on the train")).ending, Is.EqualTo("last_train"));
            Assert.That(Play(Prefer("Stay on the platform")).ending, Is.EqualTo("missed_connection"));
            var lost = Play(Prefer("Show her the photograph", "Give her your ticket"));
            Assert.That(lost.ending, Is.EqualTo("lost_property"), "the photograph opens the third ending");
        }

        [Test]
        public void SecretEndingOnlyOnASecondPlaythrough()
        {
            var memory = new StoryMemory();
            memory.BeginPlaythrough();
            var first = Play(Prefer("done this before"), memory);
            Assert.That(first.choicesSeen.Any(c => c.Contains("done this before")), Is.False);
            memory.RecordEnding(first.ending);

            memory.BeginPlaythrough();
            var second = Play(Prefer("done this before", "change the ending"), memory);
            Assert.That(second.choicesSeen.Any(c => c.Contains("done this before")), Is.True, "offered the second time");
            Assert.That(second.ending, Is.EqualTo("return_ticket"));
            Assert.That(second.tags, Does.Contain("meta:title Next Train Home"));
            Assert.That(second.lines.Any(l => l.Contains("you on the train")), Is.True, "remembers how the last playthrough ended");
            Assert.That(memory.Recall("first_words"), Is.Not.Null, "remember() kept the first words");
        }

        [Test]
        public void SecondPlaythroughRemembersAndNoticesQuitting()
        {
            var memory = new StoryMemory();
            memory.BeginPlaythrough();
            Play(Prefer("Are you all right"), memory);
            memory.NoteQuit();
            memory.BeginPlaythrough();
            var again = Play(First, memory);
            Assert.That(again.lines.Any(l => l.Contains("You left so suddenly")), Is.True, "times_quit()");
            Assert.That(again.lines.Any(l => l.Contains("a kindness")), Is.True, "recall() of last time's first words");
            Assert.That(again.lines.Any(l => l.Contains("read this page before")), Is.True, "playthrough_count() in the prologue");
        }

        [Test]
        public void SnapshotSurvivesAJsonRoundTrip()
        {
            var story = new Story(Compile());
            story.allowExternalFunctionFallbacks = true;
            for (int i = 0; i < 12 && story.canContinue; i++) story.Continue();
            var stage = new StageState();
            stage.Apply(StoryCommand.Parse("bg:platform_night"));
            stage.Apply(StoryCommand.Parse("show:iris smile right"));
            var log = new Backlog();
            log.Add("Iris", "Is it nearly midnight?", LineKind.Dialogue);
            var snap = new StorySnapshot { slot = "slot1", inkState = story.state.ToJson(), stage = stage.Clone(), backlog = log.ToList(), lineSpeaker = "iris", lineText = "Is it nearly midnight?", turn = story.state.currentTurnIndex };
            snap.used.Add("coat");
            // Public fields only, like Unity's JsonUtility.
            var opts = new System.Text.Json.JsonSerializerOptions { IncludeFields = true };
            string json = System.Text.Json.JsonSerializer.Serialize(snap, opts);
            var back = System.Text.Json.JsonSerializer.Deserialize<StorySnapshot>(json, opts);
            back.Migrate();
            Assert.That(back.stage.SameAs(stage), Is.True);
            Assert.That(back.backlog.Count, Is.EqualTo(1));
            Assert.That(back.used, Is.EqualTo(new[] { "coat" }));
            var restored = new Story(Compile());
            restored.allowExternalFunctionFallbacks = true;
            restored.state.LoadJson(back.inkState);
            Assert.That(restored.state.currentPathString, Is.EqualTo(story.state.currentPathString), "ink resumes where it was");

            var memory = new StoryMemory();
            memory.BeginPlaythrough();
            memory.RecordEnding("lost_property");
            memory.Remember("first_words", "a kindness");
            memory.MarkRead(StoryMemory.LineHash("platform", "x"));
            var m2 = System.Text.Json.JsonSerializer.Deserialize<StoryMemory>(System.Text.Json.JsonSerializer.Serialize(memory, opts), opts);
            m2.Migrate();
            Assert.That(m2.SeenEnding("lost_property"), Is.True);
            Assert.That(m2.Recall("first_words"), Is.EqualTo("a kindness"));
            Assert.That(m2.IsRead(StoryMemory.LineHash("platform", "x")), Is.True);
        }

        [Test]
        public void ValidatorCatchesABrokenCopyOfTheSample()
        {
            var files = Files().ToList();
            int main = files.FindIndex(f => f.Key == "LastTrain.ink");
            string broken = files[main].Value.Replace("#scene:platform_night fade 2", "#scnee:platform_night fade 2").Replace("#show:iris neutral right fade", "#show:iris grumpy right fade");
            files[main] = new KeyValuePair<string, string>("LastTrain.ink", broken);
            var problems = StoryValidator.Validate(InkScan.Read(files), SampleIds());
            Assert.That(problems.ToString(), Does.Contain("unknown tag #scnee (did you mean #scene?)"));
            Assert.That(problems.ToString(), Does.Contain("has no expression \"grumpy\""));
        }
    }
}
