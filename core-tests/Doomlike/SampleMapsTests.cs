using System.Collections.Generic;
using System.IO;
using JamKit.Doomlike;
using NUnit.Framework;

namespace JamKit.Tests.Doomlike
{
    /// <summary>dotnet only: the template's real level files parse and validate with the template's
    /// legend (the EditMode tests do the same inside Unity, with the legend built from the data).</summary>
    public class SampleMapsTests
    {
        static string LevelsDir()
        {
            var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets", "JamKit", "Templates", "Doomlike", "Levels")))
                dir = dir.Parent;
            return dir == null ? null : Path.Combine(dir.FullName, "Assets", "JamKit", "Templates", "Doomlike", "Levels");
        }

        /// <summary>The template's entity characters (kept in step with DoomlikeContent).</summary>
        public static MapLegend TemplateLegend()
        {
            var l = MapLegend.Default();
            foreach (var c in "mgshwW") l.AddEntity(c, EntityCategory.Enemy, 0, false, "enemy " + c);
            foreach (var c in "+HaAco") l.AddEntity(c, EntityCategory.Pickup, 0, false, "pickup " + c);
            foreach (var c in "345") l.AddEntity(c, EntityCategory.Weapon, 0, false, "weapon " + c);
            l.AddEntity('T', EntityCategory.Decoration, 0, true, "pillar");
            l.AddEntity('j', EntityCategory.Decoration, 1, false, "junk");
            return l;
        }

        static IEnumerable<string> Files()
        {
            string dir = LevelsDir();
            if (dir == null) yield break;
            foreach (var f in Directory.GetFiles(dir, "*.txt")) yield return f;
        }

        [Test]
        public void EverySampleLevelIsFinishable()
        {
            int count = 0;
            foreach (var file in Files())
            {
                count++;
                var problems = new MapProblems();
                var map = MapParser.Parse(File.ReadAllText(file), TemplateLegend(), problems);
                Assert.That(problems.errors, Is.Empty, Path.GetFileName(file) + ": " + problems);
                var report = MapValidator.Validate(map);
                TestContext.Out.WriteLine($"{Path.GetFileName(file)}: {map.name}, {map.width}x{map.height}, {report.enemies} enemies, "
                    + $"{report.pickups} pickups, {report.secrets} secrets, keys {string.Join(",", report.keyOrder)}; {report.problems}");
                Assert.That(report.problems.errors, Is.Empty, Path.GetFileName(file) + ": " + report.problems);
                Assert.That(report.problems.warnings, Is.Empty, Path.GetFileName(file) + ": " + report.problems);
                Assert.That(report.exitReachable, Is.True);
            }
            if (count == 0) Assert.Ignore("No level files found (run from the repo).");
        }
    }
}
