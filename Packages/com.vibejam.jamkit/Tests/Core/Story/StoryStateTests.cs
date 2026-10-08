using System.Collections.Generic;
using JamKit.Narrative;
using NUnit.Framework;

namespace JamKit.Tests
{
    public class BacklogTests
    {
        [Test]
        public void KeepsTheLastLinesInOrder()
        {
            var b = new Backlog(3);
            b.Add("Iris", "One.", LineKind.Dialogue);
            b.Add("", "Two.", LineKind.Narration);
            b.AddChoice("Stay.");
            b.Add("Iris", "Four.", LineKind.Dialogue);
            Assert.That(b.Count, Is.EqualTo(3));
            Assert.That(b[0].text, Is.EqualTo("Two."));
            Assert.That(b[1].kind, Is.EqualTo(LineKind.Choice));
            Assert.That(b[2].speaker, Is.EqualTo("Iris"));
        }

        [Test]
        public void SameLineTwiceIsKeptOnceAndEmptyIsIgnored()
        {
            var b = new Backlog();
            int v = b.Version;
            b.Add("Iris", "Hello.", LineKind.Dialogue);
            b.Add("Iris", "Hello.", LineKind.Dialogue);
            b.Add("", "", LineKind.Narration);
            Assert.That(b.Count, Is.EqualTo(1));
            Assert.That(b.Version, Is.Not.EqualTo(v));
        }

        [Test]
        public void SaveAndLoadCopies()
        {
            var b = new Backlog();
            b.Add("", "Rain.", LineKind.Narration);
            var list = b.ToList();
            list[0].text = "changed";
            Assert.That(b[0].text, Is.EqualTo("Rain."), "ToList copies");
            var c = new Backlog();
            c.Load(b.ToList());
            Assert.That(c.Count, Is.EqualTo(1));
            Assert.That(c[0].text, Is.EqualTo("Rain."));
        }
    }

    public class StoryMemoryTests
    {
        [Test]
        public void CountsPlaythroughsEndingsAndAbandonedRuns()
        {
            var m = new StoryMemory();
            Assert.That(m.BeginPlaythrough(), Is.False);
            Assert.That(m.playthroughs, Is.EqualTo(1));
            Assert.That(m.RecordEnding("last_train"), Is.True, "first time");
            Assert.That(m.inProgress, Is.False);
            Assert.That(m.lastEnding, Is.EqualTo("last_train"));

            m.BeginPlaythrough();
            m.NoteQuit();
            Assert.That(m.timesQuit, Is.EqualTo(1));
            Assert.That(m.BeginPlaythrough(), Is.True, "the second playthrough was left unfinished");
            Assert.That(m.abandoned, Is.EqualTo(1));
            Assert.That(m.playthroughs, Is.EqualTo(3));
            Assert.That(m.RecordEnding("last_train"), Is.False, "seen before");
            Assert.That(m.FindEnding("LAST_TRAIN").times, Is.EqualTo(2));
            Assert.That(m.FindEnding("last_train").firstPlaythrough, Is.EqualTo(1));
            Assert.That(m.DistinctEndings, Is.EqualTo(1));
            m.NoteQuit();
            Assert.That(m.timesQuit, Is.EqualTo(1), "quitting after the ending isn't quitting mid-story");
        }

        [Test]
        public void LoadsAndRewinds()
        {
            var m = new StoryMemory();
            m.BeginPlaythrough();
            m.NoteTurn(5);
            m.RecordLoad(5);
            Assert.That(m.loadsThisRun, Is.EqualTo(1));
            Assert.That(m.rewindsThisRun, Is.EqualTo(0));
            m.RecordLoad(2);
            Assert.That(m.rewindsThisRun, Is.EqualTo(1), "an earlier save goes back in time");
            m.BeginPlaythrough();
            Assert.That(m.loadsThisRun, Is.EqualTo(0), "per playthrough");
            Assert.That(m.totalLoads, Is.EqualTo(2), "ever");
        }

        [Test]
        public void RemembersTypedValues()
        {
            var m = new StoryMemory();
            m.Remember("drink", "tea");
            m.Remember("trust", 3);
            m.Remember("warmth", 0.5f);
            m.Remember("kind", true);
            Assert.That(m.Recall("drink"), Is.EqualTo("tea"));
            Assert.That(m.Recall("trust"), Is.EqualTo(3));
            Assert.That(m.Recall("warmth"), Is.EqualTo(0.5f));
            Assert.That(m.Recall("kind"), Is.EqualTo(true));
            Assert.That(m.Recall("nothing"), Is.Null);
            m.Remember("trust", 4);
            Assert.That(m.Recall("trust"), Is.EqualTo(4), "overwrites");
            Assert.That(m.values.Count, Is.EqualTo(4));
            Assert.That(m.Forget("drink"), Is.True);
            Assert.That(m.Knows("drink"), Is.False);
        }

        [Test]
        public void VisitedKnotsAndReadLines()
        {
            var m = new StoryMemory();
            Assert.That(m.Visit("platform"), Is.True);
            Assert.That(m.Visit("platform"), Is.False);
            Assert.That(m.VisitedEver("platform"), Is.True);
            int h = StoryMemory.LineHash("platform", "Rain again.");
            Assert.That(h, Is.EqualTo(StoryMemory.LineHash("platform", "Rain again.")), "stable");
            Assert.That(h, Is.Not.EqualTo(StoryMemory.LineHash("office", "Rain again.")));
            Assert.That(m.IsRead(h), Is.False);
            Assert.That(m.MarkRead(h), Is.True);
            Assert.That(m.IsRead(h), Is.True);
            // As after loading from a file: the set is rebuilt from the list.
            var copy = new StoryMemory { readLines = new List<int>(m.readLines) };
            copy.Migrate();
            Assert.That(copy.IsRead(h), Is.True);
        }

        [Test]
        public void MigrateRepairsNullsAndResetForgets()
        {
            var m = new StoryMemory { version = 0, endings = null, values = null, visitedKnots = null, readLines = null, lastEnding = null };
            Assert.That(m.Migrate(), Is.True);
            Assert.That(m.endings, Is.Not.Null);
            m.BeginPlaythrough();
            m.RecordEnding("x");
            m.titleOverride = "Changed";
            m.Reset();
            Assert.That(m.playthroughs, Is.EqualTo(0));
            Assert.That(m.endings, Is.Empty);
            Assert.That(m.titleOverride, Is.EqualTo(""));
        }
    }

    public class EndingsRegistryTests
    {
        static List<EndingInfo> Endings() => new List<EndingInfo>
        {
            new EndingInfo { id = "last_train", title = "The Last Train", description = "You went home.", hint = "Get on board." },
            new EndingInfo { id = "stayed", title = "Missed Connection", description = "You stayed." },
            new EndingInfo { id = "return_ticket", title = "Return Ticket", description = "You broke the loop.", secret = true },
        };

        [Test]
        public void CardsShowFoundHintAndSecret()
        {
            var m = new StoryMemory();
            m.BeginPlaythrough();
            m.RecordEnding("stayed");
            var cards = EndingsRegistry.Cards(Endings(), m);
            Assert.That(cards.Count, Is.EqualTo(3));
            Assert.That(cards[0].found, Is.False);
            Assert.That(cards[0].title, Is.EqualTo(EndingsRegistry.LockedTitle));
            Assert.That(cards[0].description, Is.EqualTo("Get on board."), "the hint");
            Assert.That(cards[1].found, Is.True);
            Assert.That(cards[1].title, Is.EqualTo("Missed Connection"));
            Assert.That(cards[1].times, Is.EqualTo(1));
            Assert.That(cards[2].title, Is.EqualTo(EndingsRegistry.SecretTitle));
            Assert.That(cards[2].number, Is.EqualTo(3));
            Assert.That(EndingsRegistry.Found(Endings(), m), Is.EqualTo(1));
            Assert.That(EndingsRegistry.Total(Endings()), Is.EqualTo(3));
            Assert.That(EndingsRegistry.Number(Endings(), "stayed"), Is.EqualTo(2));
        }

        [Test]
        public void UnlockAllMarksEverything()
        {
            var m = new StoryMemory();
            EndingsRegistry.UnlockAll(Endings(), m);
            Assert.That(EndingsRegistry.Found(Endings(), m), Is.EqualTo(3));
            Assert.That(EndingsRegistry.Cards(Endings(), m)[2].title, Is.EqualTo("Return Ticket"));
        }
    }

    public class StorySnapshotTests
    {
        [Test]
        public void SnapshotRestoresStageAndHistory()
        {
            var stage = new StageState();
            stage.Apply(StoryCommand.Parse("bg:platform"));
            stage.Apply(StoryCommand.Parse("show:iris smile left"));
            stage.Apply(StoryCommand.Parse("music:music_story_rain"));
            var backlog = new Backlog();
            backlog.Add("", "Rain.", LineKind.Narration);
            backlog.Add("Iris", "Hello.", LineKind.Dialogue);

            var snap = new StorySnapshot { slot = StorySlots.Manual(0), inkState = "{\"ink\":1}", stage = stage.Clone(), backlog = backlog.ToList(), lineSpeaker = "Iris", lineText = "Hello.", turn = 3 };
            // Later changes don't leak into the snapshot.
            stage.Apply(StoryCommand.Parse("hide:all"));
            backlog.Add("", "More.", LineKind.Narration);

            var restoredStage = new StageState();
            restoredStage.CopyFrom(snap.stage);
            var restoredLog = new Backlog();
            restoredLog.Load(snap.backlog);
            Assert.That(restoredStage.Find("iris").expression, Is.EqualTo("smile"));
            Assert.That(restoredStage.background, Is.EqualTo("platform"));
            Assert.That(restoredLog.Count, Is.EqualTo(2));
            Assert.That(snap.IsValid, Is.True);
            Assert.That(StorySlots.Manual(0), Is.EqualTo("slot1"));
            Assert.That(StorySlots.DisplayName(StorySlots.Auto), Is.EqualTo("Autosave"));
        }

        [Test]
        public void MigrateRepairsAnOldFile()
        {
            var snap = new StorySnapshot { stage = null, backlog = null, nvlPage = null, lineText = null, version = 0 };
            Assert.That(snap.Migrate(), Is.True);
            Assert.That(snap.stage, Is.Not.Null);
            Assert.That(snap.lineText, Is.EqualTo(""));
            Assert.That(snap.IsValid, Is.False, "no ink state");
            Assert.That(StorySnapshot.FormatPlayTime(125f), Is.EqualTo("2:05"));
            Assert.That(StorySnapshot.FormatPlayTime(3725f), Is.EqualTo("1:02:05"));
        }
    }

    public class StoryConditionTests
    {
        sealed class Facts : IStoryFacts
        {
            public readonly Dictionary<string, object> values = new Dictionary<string, object>();
            public bool TryGet(string name, out object value) => values.TryGetValue(name, out value);
        }

        [Test]
        public void NamesNotAndOr()
        {
            var f = new Facts();
            f.values["item:brass_key"] = true;
            f.values["door_open"] = false;
            f.values["trust"] = 2;
            f.values["last_ending"] = "stayed";
            Assert.That(StoryCondition.Evaluate("", f), Is.True, "empty is true");
            Assert.That(StoryCondition.Evaluate("item:brass_key", f), Is.True);
            Assert.That(StoryCondition.Evaluate("!door_open", f), Is.True);
            Assert.That(StoryCondition.Evaluate("item:brass_key && door_open", f), Is.False);
            Assert.That(StoryCondition.Evaluate("item:brass_key and not door_open", f), Is.True);
            Assert.That(StoryCondition.Evaluate("door_open || trust >= 2", f), Is.True);
            Assert.That(StoryCondition.Evaluate("trust > 2", f), Is.False);
            Assert.That(StoryCondition.Evaluate("last_ending == \"stayed\"", f), Is.True);
            Assert.That(StoryCondition.Evaluate("(door_open || item:brass_key) && trust == 2", f), Is.True);
            Assert.That(StoryCondition.Evaluate("unknown_name", f), Is.False, "unknown is false");
        }

        [Test]
        public void SyntaxErrorsAreReported()
        {
            Assert.That(StoryCondition.Check("a &&"), Is.Not.Null);
            Assert.That(StoryCondition.Check("(a || b"), Is.Not.Null);
            Assert.That(StoryCondition.Check("a @ b"), Is.Not.Null);
            Assert.That(StoryCondition.Check("a && !b"), Is.Null);
            Assert.That(StoryCondition.Names("item:key && trust > 1"), Is.EqualTo(new[] { "item:key", "trust" }));
        }
    }

    public class HotspotTests
    {
        [Test]
        public void RectAndPolygonContainment()
        {
            var rect = HotspotShape.Rect(0.1f, 0.1f, 0.2f, 0.3f);
            Assert.That(HotspotGeometry.Contains(rect, 0.2f, 0.2f), Is.True);
            Assert.That(HotspotGeometry.Contains(rect, 0.35f, 0.2f), Is.False);
            // A triangle: (0.5,0.1) (0.9,0.9) (0.1,0.9)
            var tri = HotspotShape.Polygon(0.5f, 0.1f, 0.9f, 0.9f, 0.1f, 0.9f);
            Assert.That(HotspotGeometry.Contains(tri, 0.5f, 0.6f), Is.True);
            Assert.That(HotspotGeometry.Contains(tri, 0.15f, 0.2f), Is.False);
            Assert.That(HotspotGeometry.Area(tri), Is.EqualTo(0.32f).Within(1e-4f));
            HotspotGeometry.Centroid(tri, out float cx, out float cy);
            Assert.That(cx, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(cy, Is.EqualTo(0.6333f).Within(1e-3f));
        }

        [Test]
        public void ShapeProblems()
        {
            Assert.That(HotspotShape.Rect(0.1f, 0.1f, 0f, 0.2f).Problem(), Is.Not.Null);
            Assert.That(HotspotShape.Rect(0.9f, 0.1f, 0.3f, 0.2f).Problem(), Does.Contain("off the picture"));
            Assert.That(HotspotShape.Polygon(0.1f, 0.1f, 0.2f, 0.2f).Problem(), Does.Contain("3 points"));
            Assert.That(HotspotShape.Polygon(0.1f, 0.1f, 0.2f, 0.2f, 0.3f, 0.3f).Problem(), Does.Contain("no area"));
            Assert.That(HotspotShape.Rect(0.1f, 0.1f, 0.2f, 0.2f).Problem(), Is.Null);
        }

        [Test]
        public void SmallestHotspotWinsAndDisabledAreSkipped()
        {
            var shapes = new List<HotspotShape>
            {
                HotspotShape.Rect(0f, 0f, 0.6f, 0.6f),      // desk
                HotspotShape.Rect(0.2f, 0.2f, 0.1f, 0.1f),  // drawer, inside the desk
                HotspotShape.Rect(0.7f, 0.1f, 0.2f, 0.2f),  // window
            };
            Assert.That(HotspotPicker.Pick(shapes, null, 0.25f, 0.25f), Is.EqualTo(1));
            Assert.That(HotspotPicker.Pick(shapes, null, 0.5f, 0.5f), Is.EqualTo(0));
            Assert.That(HotspotPicker.Pick(shapes, new List<bool> { true, false, true }, 0.25f, 0.25f), Is.EqualTo(0));
            Assert.That(HotspotPicker.Pick(shapes, null, 0.95f, 0.95f), Is.EqualTo(-1));
        }

        [Test]
        public void KeyboardCyclingAndDirections()
        {
            var shapes = new List<HotspotShape>
            {
                HotspotShape.Rect(0.7f, 0.1f, 0.1f, 0.1f),  // 0: top right
                HotspotShape.Rect(0.1f, 0.1f, 0.1f, 0.1f),  // 1: top left
                HotspotShape.Rect(0.1f, 0.7f, 0.1f, 0.1f),  // 2: bottom left
                HotspotShape.Rect(0.4f, 0.12f, 0.1f, 0.1f), // 3: top middle (same row)
            };
            Assert.That(HotspotPicker.ReadingOrder(shapes, null), Is.EqualTo(new[] { 1, 3, 0, 2 }));
            Assert.That(HotspotPicker.Cycle(shapes, null, -1, 1), Is.EqualTo(1));
            Assert.That(HotspotPicker.Cycle(shapes, null, 2, 1), Is.EqualTo(1), "wraps");
            Assert.That(HotspotPicker.Cycle(shapes, null, 1, -1), Is.EqualTo(2));
            Assert.That(HotspotPicker.Cycle(shapes, new List<bool> { true, true, true, false }, 1, 1), Is.EqualTo(0), "skips disabled");
            Assert.That(HotspotPicker.Step(shapes, null, 1, 1f, 0f), Is.EqualTo(3), "right: the nearest ahead");
            Assert.That(HotspotPicker.Step(shapes, null, 1, 0f, 1f), Is.EqualTo(2), "down");
            Assert.That(HotspotPicker.Step(shapes, null, 2, 0f, 1f), Is.EqualTo(2), "nothing below: stays");
            Assert.That(HotspotPicker.Step(shapes, null, -1, 1f, 0f), Is.EqualTo(1), "nothing selected: the first");
        }
    }
}
