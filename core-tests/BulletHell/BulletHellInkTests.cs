using System;
using System.Collections.Generic;
using System.IO;
using Path = System.IO.Path;
using Ink.Runtime;
using JamKit.Dialogue;
using NUnit.Framework;

namespace JamKit.Tests
{
    /// <summary>
    /// The bullet-hell template's ink (Mystery rooms + story hooks), compiled by the real ink compiler
    /// and played headless (no Unity), the way BulletHellGame looks hooks up: it compiles, every
    /// Mystery knot exists, each placeholder hook resolves through <see cref="StoryHooks"/> and plays
    /// to text with the game's ink functions bound, hooks nobody wrote fall back silently, and the
    /// death line changes with the killer and the run number.
    /// </summary>
    public class BulletHellInkTests
    {
        static string InkFile()
        {
            var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets", "JamKit"))) dir = dir.Parent;
            Assert.That(dir, Is.Not.Null, "repo root not found");
            return Path.Combine(dir.FullName, "Assets", "JamKit", "Templates", "BulletHell", "Dialogue", "BulletHellEvents.ink");
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

        /// <summary>The game's state as ink sees it (what BulletHellGame and the shell bind).</summary>
        sealed class Facts
        {
            public int runs = 1, floor = 1, guards;
            public string killer = "battle";
            public readonly HashSet<string> flags = new HashSet<string>();
        }

        static Story NewStory(Facts f)
        {
            var story = new Story(Compile());
            story.BindExternalFunction("has_flag", (string n) => (object)f.flags.Contains(n), true);
            story.BindExternalFunction("set_flag", (string n) => { f.flags.Add(n); });
            story.BindExternalFunction("clear_flag", (string n) => { f.flags.Remove(n); });
            story.BindExternalFunction("run_count", () => (object)f.runs, true);
            story.BindExternalFunction("current_floor", () => (object)f.floor, true);
            story.BindExternalFunction("killer", () => (object)f.killer, true);
            story.BindExternalFunction("companion_name", () => (object)"Companion", true);
            story.BindExternalFunction("companion_guards", () => (object)f.guards, true);
            return story;
        }

        static bool Has(Story s, string knot)
        {
            try { return s.ContentAtPath(new Ink.Runtime.Path(knot)).correctObj != null; }
            catch (Exception) { return false; }
        }

        /// <summary>Plays a knot to its end (first choice at each choice); returns its non-empty lines.</summary>
        static List<string> Play(Facts f, string knot, List<string> tags = null)
        {
            var s = NewStory(f);
            s.ChoosePathString(knot);
            var lines = new List<string>();
            for (int guard = 0; guard < 200; guard++)
            {
                while (s.canContinue)
                {
                    string line = s.Continue().Trim();
                    if (tags != null) tags.AddRange(s.currentTags);
                    if (line.Length > 0) lines.Add(line);
                }
                if (s.currentChoices.Count == 0) break;
                s.ChooseChoiceIndex(0);
            }
            return lines;
        }

        static string Resolve(string[] candidates) => StoryHooks.Resolve(candidates, k => Has(NewStory(new Facts()), k));

        [Test]
        public void CompilesAndHasEveryMysteryKnot()
        {
            var s = NewStory(new Facts());
            foreach (var knot in new[] { "shrine", "gambler", "medic", "cache" })
                Assert.That(Has(s, knot), Is.True, knot);
        }

        [Test]
        public void PlaceholderHooksResolveAndPlay()
        {
            var f = new Facts();
            var expected = new Dictionary<string, string>
            {
                { "boss_gatekeeper_intro", Resolve(StoryHooks.BossIntro("Gatekeeper")) },
                { "boss_gatekeeper_phase_2", Resolve(StoryHooks.BossPhase("Gatekeeper", 2)) },
                { "boss_gatekeeper_defeat", Resolve(StoryHooks.BossDefeat("Gatekeeper")) },
                { "rest", Resolve(StoryHooks.Rest(2)) },
                { "run_start", Resolve(StoryHooks.RunStart(1)) },
                { "death", Resolve(StoryHooks.Death("battle", 1)) },
                { "forge", Resolve(StoryHooks.Encounter("forge", 2)) },
                { "altar", Resolve(StoryHooks.Encounter("altar", 1)) },
                { "wager", Resolve(StoryHooks.Encounter("wager", 3)) },
                { "trial_board", Resolve(StoryHooks.Encounter("trial_board", 1)) },
                { "trader", Resolve(StoryHooks.Encounter("trader", 2)) },
                { "companion_bark_spiral", Resolve(StoryHooks.CompanionBark("spiral", "Forgemother")) },
                { "companion_bark_gatekeeper_ring", Resolve(StoryHooks.CompanionBark("ring", "Gatekeeper")) },
                { "companion_bark_guard", Resolve(StoryHooks.CompanionBark("guard")) },
                { "companion_bark_low_life", Resolve(StoryHooks.CompanionBark("low_life")) },
            };
            foreach (var kv in expected)
            {
                Assert.That(kv.Value, Is.EqualTo(kv.Key), "hook resolves to " + kv.Key);
                var tags = new List<string>();
                var lines = Play(f, kv.Key, tags);
                Assert.That(lines, Is.Not.Empty, kv.Key + " has a line");
                foreach (var t in tags)
                {
                    Assert.That(DialogueTag.TryParse(t, out var tag), Is.True, kv.Key + ": tag " + t);
                    Assert.That(tag.key, Is.EqualTo("speaker").Or.EqualTo("portrait"), kv.Key + ": only shell tags in hooks, got " + t);
                }
                foreach (var l in lines) Assert.That(l, Does.Contain("Placeholder"), kv.Key + " is marked as placeholder text");
            }
        }

        [Test]
        public void UnwrittenHooksFallBackToNothing()
        {
            Assert.That(Resolve(StoryHooks.BossIntro("Forgemother")), Is.Null, "no intro for the second boss yet");
            Assert.That(Resolve(StoryHooks.BossPhase("The Last Light", 3)), Is.Null);
            Assert.That(Resolve(StoryHooks.BossPhase("Gatekeeper", 3)), Is.Null, "only phase 2 has a line");
            Assert.That(Resolve(StoryHooks.BossDefeat("Forgemother")), Is.Null);
            Assert.That(Resolve(StoryHooks.RunStart(2)), Is.EqualTo("run_start"), "run 2 uses the general knot (which reads run_count())");
            Assert.That(Resolve(StoryHooks.Death("gatekeeper", 1)), Is.EqualTo("death"), "the general death knot reads killer()");
        }

        [Test]
        public void DeathAndRunStartLinesReadTheirInputs()
        {
            var first = new Facts { runs = 1, killer = "battle" };
            var later = new Facts { runs = 3, floor = 2, killer = "elite" };
            var boss = new Facts { runs = 3, killer = "gatekeeper" };
            string a = Play(first, "death")[0], b = Play(later, "death")[0], c = Play(boss, "death")[0];
            Assert.That(a, Is.Not.EqualTo(b), "first death vs a later one");
            Assert.That(b, Does.Contain("Floor 2").And.Contain("run 3"));
            Assert.That(c, Does.Contain("Gatekeeper"), "the killer picks the line");
            Assert.That(Play(first, "run_start")[0], Is.Not.EqualTo(Play(later, "run_start")[0]), "the second run starts differently");
            Assert.That(Play(later, "rest")[0], Does.Contain("Floor 2"));
        }

        [Test]
        public void EveryPatternShapeHasABarkAndTheRestLineReadsTheCompanionsBattles()
        {
            foreach (var shape in new[] { "ring", "aimed", "spiral", "wall", "spread", "shotgun" })
            {
                string knot = Resolve(StoryHooks.CompanionBark(shape, "The Last Light"));
                Assert.That(knot, Is.EqualTo("companion_bark_" + shape), shape + " falls back to the general bark");
                Assert.That(Play(new Facts(), knot).Count, Is.EqualTo(1), knot + ": a bark is one line");
            }
            Assert.That(Resolve(StoryHooks.CompanionBark("nothing_written")), Is.Null, "an unwritten bark stays silent");
            string quiet = Play(new Facts { guards = 0 }, "rest")[0];
            string guarded = Play(new Facts { guards = 3 }, "rest")[0];
            Assert.That(guarded, Does.Contain("3"));
            Assert.That(guarded, Is.Not.EqualTo(quiet), "the companion's battles change its Rest line");
        }
    }
}
