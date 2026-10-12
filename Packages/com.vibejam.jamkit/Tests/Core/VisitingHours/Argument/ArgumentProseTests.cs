using System;
using System.IO;
using System.Linq;
using JamKit.Argument;
using NUnit.Framework;

namespace JamKit.Tests.Argument
{
    /// <summary>Finds the prose files (Assets/JamKit/Templates/VisitingHours/Argument/Resources/ArgumentText/) from the
    /// test run and makes the battles and the screen read them, as the game does from Resources. A copy waiting in
    /// docs/_pending/ArgumentText/ (edits made while the creator had the file open; it's swapped in once their tab is
    /// closed) is read instead: the tests check what the game will have.</summary>
    public static class ArgumentTestFiles
    {
        public const string Folder = "Assets/JamKit/Templates/VisitingHours/Argument/Resources/ArgumentText";
        public const string PendingFolder = "docs/_pending/ArgumentText";

        public static string Path(string name)
        {
            foreach (var folder in new[] { PendingFolder, Folder })
                foreach (var start in new[] { TestContext.CurrentContext.TestDirectory, Directory.GetCurrentDirectory() })
                {
                    for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
                    {
                        string p = System.IO.Path.Combine(dir.FullName, folder, name + ".txt");
                        if (File.Exists(p)) return p;
                    }
                }
            Assert.Fail("Can't find " + Folder + "/" + name + ".txt above " + TestContext.CurrentContext.TestDirectory);
            return null;
        }

        public static string Read(string name) => File.ReadAllText(Path(name));

        /// <summary>The demo's prose and the screen's words from the files on disk.</summary>
        public static void Use()
        {
            string prose = Read(DreamLenaDemo.ProseFile);
            string walt = Read(WaltNight1.ProseFile);
            string screen = Read("screen");
            DreamLenaDemo.ProseSource = () => prose;
            WaltNight1.ProseSource = () => walt;
            ArgumentStrings.SharedSource = () => screen;
        }


    }

    /// <summary>Every word of the battle is in the creator's files, not in code: the battle's file has a line for every
    /// id its structure asks for (and nothing it doesn't use), the screen's file has every id the screen asks for, and
    /// the format reads as documented.</summary>
    public class ArgumentProseTests
    {
        [OneTimeSetUp]
        public void Files() => ArgumentTestFiles.Use();

        [Test]
        public void TheBattleFileHasEveryLineAndNoStrayOnes()
        {
            var lines = ArgumentText.Parse(ArgumentTestFiles.Read(DreamLenaDemo.ProseFile));
            var def = DreamLenaDemo.Structure();
            var missing = ArgumentText.Apply(def, lines);
            Assert.That(missing, Is.Empty, "lines missing from lena_night2.txt");
            var keys = ArgumentText.Keys(DreamLenaDemo.Structure());
            var stray = lines.Keys.Where(k => !keys.Contains(k) && !k.EndsWith(".noTouchReply", StringComparison.Ordinal) && k.Count(c => c == '.') < 2).ToList();
            Assert.That(stray, Is.Empty, "ids in the file the battle doesn't use (a typo?)");
            // An empty line is the creator's to fill (L1c.askReply was left empty on purpose): never a failure, only listed
            // (in play it falls back: an empty ask reply repeats the premise).
            var empty = keys.Where(k => string.IsNullOrEmpty(lines[k])).ToList();
            if (empty.Count > 0) TestContext.WriteLine("Lines left empty in lena_night2.txt (the creator's to fill): " + string.Join(", ", empty));
            Assert.That(ArgumentText.ApplyVariants(def, ArgumentTestFiles.Read(DreamLenaDemo.ProseFile)), Is.Empty, "every conditional line reads and has a line to replace");
        }

        [Test]
        public void WaltsFileHasEveryLineAndIsMarkedAsAPlaceholder()
        {
            string text = ArgumentTestFiles.Read(WaltNight1.ProseFile);
            var lines = ArgumentText.Parse(text);
            var def = WaltNight1.Structure();
            Assert.That(ArgumentText.Apply(def, lines), Is.Empty, "lines missing from walt_night1.txt");
            var keys = ArgumentText.Keys(WaltNight1.Structure());
            Assert.That(lines.Keys.Where(k => !keys.Contains(k) && !k.StartsWith("face.", StringComparison.Ordinal)), Is.Empty, "ids Walt's battle doesn't use");
            Assert.That(def.Face("hurt"), Is.EqualTo("sad"), "his drawings stand in for the game's moods");
            Assert.That(def.Face("angry"), Is.EqualTo("antagonistic"));
            Assert.That(def.Face("rattled"), Is.EqualTo("rattled"), "a mood he has is used as it is");
            foreach (var k in keys) Assert.That(lines[k], Is.Not.Empty, k + " is empty");
            Assert.That(text, Does.Contain("WRITTEN BY THE BATTLE AGENT"), "written for real (2026-10-12), and marked as the agent's");
            Assert.That(def.placeholderLines, Is.False, "no placeholder badge on screen");
            Assert.That(def.coachName, Is.EqualTo("Mags"));
            foreach (var m in ArgumentCoach.Moments) Assert.That(def.coach[m], Is.Not.Empty, "Mags has a line for " + m);
        }

        [Test]
        public void TheScreenFileHasEveryLine()
        {
            var screen = new ArgumentStrings(ArgumentText.Parse(ArgumentTestFiles.Read("screen")));
            var missing = ArgumentStrings.Required.Where(k => !screen.Has(k)).ToList();
            Assert.That(missing, Is.Empty, "lines missing from screen.txt");
        }

        [Test]
        public void NoLineOfTheBattleIsInCode()
        {
            var bare = DreamLenaDemo.Build(null);
            Assert.That(bare.claim, Is.EqualTo("[claim]"), "without the file, a line shows as its id");
            Assert.That(bare.Premise("L1").text, Is.EqualTo("[L1.text]"));
            Assert.That(bare.Premise("L1b").challenges[0].reasons[0].text, Is.EqualTo("[L1b.sound]"));
            Assert.That(bare.Evidence(DreamLenaDemo.FourthChair).name, Is.EqualTo("[mem_fourth_chair.name]"));
            var full = DreamLenaDemo.Build();
            Assert.That(full.claim, Is.Not.Empty.And.Not.StartWith("["));
            Assert.That(full.Premise("L3c").challenges.All(c => c.reasons.All(r => !r.text.StartsWith("[", StringComparison.Ordinal))), Is.True);
        }

        [Test]
        public void TheFormatReadsAsDocumented()
        {
            var d = ArgumentText.Parse("# a heading\nclaim: He stays.\n  ? a note: ignored\n? also a note\nL1.text: Two\\nlines: with a colon\nbad line\n");
            Assert.That(d.Count, Is.EqualTo(2));
            Assert.That(d["claim"], Is.EqualTo("He stays."));
            Assert.That(d["L1.text"], Is.EqualTo("Two\nlines: with a colon"));

            // A line for one piece of evidence overrides the premise's shared reason.
            var def = DreamLenaDemo.Structure();
            var lines = ArgumentText.Parse(ArgumentTestFiles.Read(DreamLenaDemo.ProseFile) + "\nL3c.truth_walt.sound: Walt's own words.\n");
            ArgumentText.Apply(def, lines);
            var walt = def.Premise("L3c").ChallengeFor(DreamLenaDemo.TruthWalt).reasons.First(r => r.kind == ReasonKind.Sound);
            var same = def.Premise("L3c").ChallengeFor(DreamLenaDemo.SameSaturday).reasons.First(r => r.kind == ReasonKind.Sound);
            Assert.That(walt.text, Is.EqualTo("Walt's own words."));
            Assert.That(same.text, Is.EqualTo(lines["L3c.sound"]));
        }

        [Test]
        public void ExportWritesAFileThatReadsBackTheSame()
        {
            var def = DreamLenaDemo.Build();
            string text = ArgumentText.Export(def, ArgumentText.Capture(def));
            var again = DreamLenaDemo.Build(text);
            Assert.That(ArgumentText.Capture(again), Is.EquivalentTo(ArgumentText.Capture(def)));
            Assert.That(text, Does.Contain("  ? "), "notes say what each line must do");
        }

        [Test]
        public void TheTranscriptSentencesComeFromTheScreenFile()
        {
            var e = new ArgumentEngine(DreamLenaDemo.Build(), new ArgumentSetup { difficulty = ArgumentDifficulty.Story, allEvidence = true });
            e.Start();
            e.SkipPressure();
            e.Concede("L1");
            var screen = ArgumentStrings.Shared;
            string expected = screen.Format("logic.concede", "premise", e.Def.Premise("L1").text, "opponent", e.Def.opponent);
            Assert.That(e.Transcript.Any(x => x.speaker == Speaker.Logic && x.text == expected), Is.True);
        }
    }
}
