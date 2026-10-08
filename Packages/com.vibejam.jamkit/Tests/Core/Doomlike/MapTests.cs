using System.Linq;
using JamKit.Doomlike;
using NUnit.Framework;

namespace JamKit.Tests.Doomlike
{
    static class TestMaps
    {
        /// <summary>The default legend plus an enemy (m), a pickup (+), a weapon (3) and a solid pillar (T).</summary>
        public static MapLegend Legend()
        {
            var l = MapLegend.Default();
            l.AddEntity('m', EntityCategory.Enemy, 0, false, "enemy");
            l.AddEntity('+', EntityCategory.Pickup, 0, false, "health");
            l.AddEntity('3', EntityCategory.Weapon, 0, false, "shotgun");
            l.AddEntity('T', EntityCategory.Decoration, 0, true, "pillar");
            return l;
        }

        public static MapData Parse(string text, out MapProblems problems)
        {
            problems = new MapProblems();
            return MapParser.Parse(text, Legend(), problems);
        }

        public static MapReport Check(string text)
        {
            var map = Parse(text, out var problems);
            Assert.That(problems.errors, Is.Empty, "parse: " + problems);
            return MapValidator.Validate(map);
        }

        public static string Lines(params string[] rows) => string.Join("\n", rows);
    }

    public class MapParserTests
    {
        [Test]
        public void ReadsHeaderAndGrid()
        {
            string text = TestMaps.Lines(
                "// a comment",
                "name: Pump Station",
                "subtitle: Find the red key",
                "theme: rust",
                "ceiling: 4.5",
                "light: 0.8",
                "par: 1:30",
                "facing: south",
                "---",
                "#######",
                "#P.m.E#",
                "#######");
            var map = TestMaps.Parse(text, out var problems);
            Assert.That(problems.errors, Is.Empty, problems.ToString());
            Assert.That(problems.warnings, Is.Empty, problems.ToString());
            Assert.That(map.name, Is.EqualTo("Pump Station"));
            Assert.That(map.subtitle, Is.EqualTo("Find the red key"));
            Assert.That(map.theme, Is.EqualTo("rust"));
            Assert.That(map.ceiling, Is.EqualTo(4.5f));
            Assert.That(map.light, Is.EqualTo(0.8f));
            Assert.That(map.parSeconds, Is.EqualTo(90f));
            Assert.That(map.startFacingDegrees, Is.EqualTo(270f));
            Assert.That(map.width, Is.EqualTo(7));
            Assert.That(map.height, Is.EqualTo(3));
            Assert.That(map.startX, Is.EqualTo(1));
            Assert.That(map.startY, Is.EqualTo(1));
            Assert.That(map.exits.Count, Is.EqualTo(1));
            Assert.That(map.exits[0].x, Is.EqualTo(5));
            Assert.That(map.things.Count, Is.EqualTo(1));
            Assert.That(map.things[0].entry.category, Is.EqualTo(EntityCategory.Enemy));
            Assert.That(map.KindAt(3, 1), Is.EqualTo(CellKind.Entity));
            Assert.That(map.KindAt(0, 0), Is.EqualTo(CellKind.Wall));
            Assert.That(map.KindAt(-1, 0), Is.EqualTo(CellKind.Void), "outside the map is void");
        }

        [Test]
        public void NoHeaderMeansTheWholeFileIsTheMap()
        {
            var map = TestMaps.Parse(TestMaps.Lines("", "#####", "#P.E#", "#####", ""), out var problems);
            Assert.That(problems.Ok, Is.True, problems.ToString());
            Assert.That(map.height, Is.EqualTo(3), "blank lines around the map are dropped");
            Assert.That(map.name, Is.EqualTo("Untitled"));
        }

        [Test]
        public void ShortRowsArePaddedWithVoid()
        {
            var map = TestMaps.Parse(TestMaps.Lines("#######", "#P.E#", "#####  "), out var problems);
            Assert.That(problems.Ok, Is.True, problems.ToString());
            Assert.That(map.width, Is.EqualTo(7));
            Assert.That(map.KindAt(6, 1), Is.EqualTo(CellKind.Void));
        }

        [Test]
        public void UnknownCharactersAndTabsAreErrorsWithTheirPosition()
        {
            TestMaps.Parse(TestMaps.Lines("#####", "#PQE#", "#\t###"), out var problems);
            Assert.That(problems.errors.Count, Is.EqualTo(2), problems.ToString());
            Assert.That(problems.errors[0], Does.Contain("'Q'"));
            Assert.That(problems.errors[0], Does.Contain("row 2, column 3"));
            Assert.That(problems.errors[1], Does.Contain("tab").IgnoreCase);
            Assert.That(problems.errors[1], Does.Contain("row 3, column 2"));
        }

        [Test]
        public void StartAndExitAreRequired()
        {
            TestMaps.Parse(TestMaps.Lines("#####", "#...#", "#####"), out var none);
            Assert.That(none.errors.Any(e => e.Contains("No player start")), Is.True, none.ToString());
            Assert.That(none.errors.Any(e => e.Contains("No exit")), Is.True, none.ToString());

            TestMaps.Parse(TestMaps.Lines("#####", "#PPE#", "#####"), out var two);
            Assert.That(two.errors.Any(e => e.Contains("2 player starts")), Is.True, two.ToString());
        }

        [Test]
        public void BadHeaderValuesWarnAndKeepDefaults()
        {
            var map = TestMaps.Parse(TestMaps.Lines("ceiling: 100", "colour: red", "nonsense", "---", "#####", "#P.E#", "#####"), out var problems);
            Assert.That(problems.Ok, Is.True);
            Assert.That(problems.warnings.Count, Is.EqualTo(3), problems.ToString());
            Assert.That(map.ceiling, Is.EqualTo(0f));
        }

        [Test]
        public void EmptyAndOversizeMapsAreErrors()
        {
            TestMaps.Parse("", out var empty);
            Assert.That(empty.Ok, Is.False);
            TestMaps.Parse("name: x\n---\n\n", out var noRows);
            Assert.That(noRows.errors[0], Does.Contain("no map rows"));
            TestMaps.Parse(new string('#', MapParser.MaxSize + 1), out var wide);
            Assert.That(wide.errors[0], Does.Contain("the most is"));
        }

        [Test]
        public void AutomaticFacingLooksDownTheLongestCorridor()
        {
            var map = TestMaps.Parse(TestMaps.Lines(
                "#########",
                "#.#######",
                "#P......#",
                "#.#####E#",
                "#########"), out _);
            Assert.That(map.ResolveStartFacingDegrees(), Is.EqualTo(0f), "east: 6 open cells");
            var up = TestMaps.Parse(TestMaps.Lines("###", "#.#", "#.#", "#.#", "#P#", "#E#", "###"), out _);
            Assert.That(up.ResolveStartFacingDegrees(), Is.EqualTo(90f), "north is up in the file");
        }

        [Test]
        public void TimesParse()
        {
            Assert.That(MapParser.TryTime("75", out float a) && a == 75f, Is.True);
            Assert.That(MapParser.TryTime("2:05", out float b) && b == 125f, Is.True);
            Assert.That(MapParser.TryTime("x", out _), Is.False);
        }

        [Test]
        public void RowsRoundTrip()
        {
            string[] rows = { "#####", "#P.E#", "#####" };
            var map = TestMaps.Parse(TestMaps.Lines(rows), out _);
            Assert.That(MapParser.Rows(map), Is.EqualTo(rows));
        }

        [Test]
        public void LegendRefusesDuplicateCharacters()
        {
            var l = MapLegend.Default();
            Assert.That(l.AddEntity('#', EntityCategory.Enemy, 0, false, "x"), Is.False, "# is a wall");
            Assert.That(l.AddEntity('m', EntityCategory.Enemy, 0, false, "x"), Is.True);
            Assert.That(l.AddEntity('m', EntityCategory.Pickup, 1, false, "y"), Is.False);
        }
    }

    public class MapValidatorTests
    {
        [Test]
        public void SimpleMapIsValid()
        {
            var r = TestMaps.Check(TestMaps.Lines("#######", "#P.m.E#", "#######"));
            Assert.That(r.problems.errors, Is.Empty, r.problems.ToString());
            Assert.That(r.exitReachable, Is.True);
            Assert.That(r.enemies, Is.EqualTo(1));
            Assert.That(r.reachableEnemies, Is.EqualTo(1));
        }

        [Test]
        public void KeysOpenDoorsInOrder()
        {
            // Start -> red key -> red door -> blue key -> blue door -> exit.
            var r = TestMaps.Check(TestMaps.Lines(
                "###########",
                "#P.r#b.#.E#",
                "#...R..B..#",
                "###########"));
            Assert.That(r.problems.errors, Is.Empty, r.problems.ToString());
            Assert.That(r.exitReachable, Is.True);
            Assert.That(r.keyOrder, Is.EqualTo(new[] { KeyColor.Red, KeyColor.Blue }));
        }

        [Test]
        public void ExitBehindALockedDoorWithoutItsKeyIsAnError()
        {
            var map = TestMaps.Parse(TestMaps.Lines("#######", "#P.R.E#", "#######"), out _);
            var r = MapValidator.Validate(map);
            Assert.That(r.exitReachable, Is.False);
            Assert.That(r.problems.errors.Any(e => e.Contains("no red key")), Is.True, r.problems.ToString());
            Assert.That(r.problems.errors.Any(e => e.Contains("exit can't be reached")), Is.True, r.problems.ToString());
        }

        [Test]
        public void KeyBehindItsOwnDoorIsAnError()
        {
            var r = TestMaps.Check(TestMaps.Lines(
                "#########",
                "#P..R.r.#",
                "#.#######",
                "#E#######",
                "#########"));
            Assert.That(r.exitReachable, Is.True, "the exit itself is fine");
            Assert.That(r.problems.errors.Count, Is.EqualTo(1), r.problems.ToString());
            Assert.That(r.problems.errors[0], Does.Contain("red key"));
            Assert.That(r.problems.errors[0], Does.Contain("only reachable through a door"));
        }

        [Test]
        public void KeyInASealedRoomIsAnError()
        {
            var r = TestMaps.Check(TestMaps.Lines("#########", "#P.E##r.#", "#########"));
            Assert.That(r.problems.errors.Any(e => e.Contains("red key") && e.Contains("can't be reached at all")), Is.True, r.problems.ToString());
        }

        [Test]
        public void ChainedKeysNeedTheRightOrder()
        {
            // The blue key is behind the yellow door, the yellow key behind the blue door: a deadlock.
            var r = TestMaps.Check(TestMaps.Lines(
                "###########",
                "#P...B.y..#",
                "####Y######",
                "####b######",
                "####E######",
                "###########"));
            Assert.That(r.exitReachable, Is.False, r.problems.ToString());
            Assert.That(r.problems.errors.Any(e => e.Contains("blue key") && e.Contains("only reachable")), Is.True, r.problems.ToString());
        }

        [Test]
        public void DoorsNeedWallsOnBothSides()
        {
            var r = TestMaps.Check(TestMaps.Lines(
                "#######",
                "#P....#",
                "#..D..#",
                "#....E#",
                "#######"));
            Assert.That(r.problems.errors.Any(e => e.Contains("door at row 3, column 4") && e.Contains("walls on both sides")), Is.True, r.problems.ToString());
        }

        [Test]
        public void SolidDecorationsBlockTheWay()
        {
            var r = TestMaps.Check(TestMaps.Lines("#######", "#P.T.E#", "#######"));
            Assert.That(r.exitReachable, Is.False, "a pillar fills the corridor");
        }

        [Test]
        public void SecretAndClosetDoorsCountAsPassable()
        {
            var r = TestMaps.Check(TestMaps.Lines(
                "#########",
                "#P.!.?.E#",
                "####|####",
                "####m####",
                "#########"));
            Assert.That(r.problems.errors, Is.Empty, r.problems.ToString());
            Assert.That(r.exitReachable, Is.True);
            Assert.That(r.reachableEnemies, Is.EqualTo(1), "the closet's enemy counts as reachable");
        }

        [Test]
        public void UnreachableThingsAndLonelyClosetsWarn()
        {
            var r = TestMaps.Check(TestMaps.Lines(
                "############",
                "#P..E##.m+.#",
                "######|#####",
                "############"));
            Assert.That(r.problems.errors, Is.Empty, r.problems.ToString());
            Assert.That(r.problems.warnings.Any(w => w.Contains("1 enemies can't be reached")), Is.True, r.problems.ToString());
            Assert.That(r.problems.warnings.Any(w => w.Contains("1 pickups can't be reached")), Is.True, r.problems.ToString());
            Assert.That(r.problems.warnings.Any(w => w.Contains("closet wall") && w.Contains("never opens")), Is.True, r.problems.ToString());
        }

        [Test]
        public void CountsSecretsAndTriggers()
        {
            var r = TestMaps.Check(TestMaps.Lines("##########", "#P.S.!.mE#", "##########"));
            Assert.That(r.secrets, Is.EqualTo(1));
            Assert.That(r.triggers, Is.EqualTo(1));
        }
    }
}
