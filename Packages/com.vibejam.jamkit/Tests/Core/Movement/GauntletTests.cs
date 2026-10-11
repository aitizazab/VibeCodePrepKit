using System;
using System.Collections.Generic;
using System.Numerics;
using JamKit.Movement;
using NUnit.Framework;

namespace JamKit.Tests.Movement
{
    /// <summary>The Movement Lab's Gauntlet in the fake world: the autopilot runs it from the start pad to the finish
    /// with no falls at 20 to 300 fps and with jittery frames, touching every marker; its time is the developer's
    /// time; every section reaches the next; the route fits the movement's reach and the controller's clearance and
    /// uses every move; the safe lines are hoppable and stay off the fast lane; the clock starts at the gate, splits,
    /// counts markers, adds the penalty for a missed one and resets on restart; the ghost records and replays.</summary>
    public class GauntletTests
    {
        static GauntletCourse Build() => GauntletCourse.Build();

        internal sealed class RunResult
        {
            public bool finished;
            public int falls;
            public float seconds;
            public GauntletClock clock;
            public GhostTrack ghost = new GhostTrack();
            public readonly System.Text.StringBuilder log = new System.Text.StringBuilder();
        }

        /// <summary>The autopilot from the start pad (standing) to the finish, with the run clock and a ghost recording.</summary>
        internal static RunResult Run(GauntletCourse g, int fps, int jitterSeed = 0, float skin = 0.04f)
        {
            var c = g.course;
            var world = LabCourseTests.WorldOf(c, null);
            world.skin = skin;
            var sim = new MoveSim(world, new MoveDef());
            var driver = new MoveDriver(sim);
            var pilot = new CourseAutopilot(c);
            var clock = new GauntletClock(g);
            var r = new RunResult { clock = clock };
            var jitter = jitterSeed != 0 ? new Random(jitterSeed) : null;
            var start = c.checkpoints[g.startCheckpoint];
            driver.Teleport(start.position, 0f, Vector3.Zero);
            pilot.Reset(start.s);
            int lastCp = g.startCheckpoint;
            string lastMode = "";
            float dt = 1f / fps;
            for (float t = 0f; t < 150f; t += dt)
            {
                if (jitter != null) dt = jitter.NextDouble() < 0.1 ? 0.05f : (float)(0.0025 + jitter.NextDouble() * 0.02);
                driver.Frame(dt, pilot.Next(sim));
                var p = sim.Position;
                float s = c.route.Project(p);
                for (int i = lastCp + 1; i < c.checkpoints.Count; i++) if (s >= c.checkpoints[i].s) lastCp = i;
                var ev = clock.Tick(dt, p, s, sim.def.radius, sim.Height);
                if (clock.State == GauntletState.Running) r.ghost.Record(dt, p, sim.Yaw);
                if (ev.markerMissed >= 0) r.log.AppendLine($"{t,6:0.00}s MISSED {g.markers[ev.markerMissed].name} at z {p.Z:0.0} x {p.X:0.0} y {p.Y:0.0}");
                string mode = sim.Mode.ToString();
                if (mode != lastMode) { r.log.AppendLine($"{t,6:0.00}s z {p.Z - GauntletCourse.Origin,7:0.0} x {p.X,5:0.0} y {p.Y,6:0.00} {mode,-8} speed {sim.HorizontalSpeed,5:0.0}"); lastMode = mode; }
                if (p.Y < c.killY)
                {
                    r.falls++;
                    r.log.AppendLine($"{t,6:0.00}s FELL at z {p.Z - GauntletCourse.Origin:0.0} x {p.X:0.0}");
                    var back = c.checkpoints[lastCp];
                    driver.Teleport(back.position, 0f, new Vector3(0f, 0f, sim.def.sprintSpeed));
                    pilot.Reset(back.s);
                    if (r.falls > 5) break;
                    continue;
                }
                if (ev.finished) { r.finished = true; r.seconds = clock.Time; break; }
            }
            return r;
        }

        [Test]
        public void TheAutopilotRunsTheGauntletCleanAndSetsTheDevelopersTime()
        {
            var g = Build();
            var r = Run(g, 60);
            TestContext.Out.WriteLine(r.log.ToString());
            TestContext.Out.WriteLine($"finished {r.finished} in {r.seconds:0.00} s (penalty {r.clock.Penalty:0.0}), falls {r.falls}, markers {r.clock.MarkersHit}/{r.clock.MarkerCount}");
            for (int i = 0; i < g.sections.Count; i++) TestContext.Out.WriteLine($"  split {g.sections[i].name}: {r.clock.Split(i):0.00}");
            Assert.That(r.finished, Is.True);
            Assert.That(r.falls, Is.EqualTo(0));
            Assert.That(r.clock.Missed, Is.EqualTo(0), "every marker touched");
            Assert.That(r.seconds, Is.InRange(28f, 45f), "a good run is 30-40 s");
            Assert.That(GauntletCourse.AutopilotTime, Is.EqualTo(r.seconds).Within(0.6f), "the autopilot's time: update GauntletCourse.AutopilotTime");
            Assert.That(r.ghost.Duration, Is.EqualTo(r.seconds).Within(0.2f), "the ghost covers the run");
        }

        [TestCase(20, 0)]
        [TestCase(144, 0)]
        [TestCase(300, 0)]
        [TestCase(0, 7)]
        [TestCase(0, 11)]
        public void TheGauntletFinishesAtAnyFrameRate(int fps, int jitterSeed)
        {
            var g = Build();
            var r = Run(g, fps == 0 ? 60 : fps, jitterSeed, 0.04f);
            TestContext.Out.WriteLine(r.log.ToString());
            Assert.That(r.finished, Is.True, fps + " fps, jitter " + jitterSeed);
            Assert.That(r.falls, Is.EqualTo(0), fps + " fps, jitter " + jitterSeed);
            Assert.That(r.clock.Missed, Is.EqualTo(0), fps + " fps, jitter " + jitterSeed);
        }

        [Test]
        public void EverySectionRunsToTheNext()
        {
            var g = Build();
            for (int i = 1; i < g.sections.Count; i++)
            {
                int cp = g.sections[i].checkpoint;
                float until = i + 1 < g.sections.Count ? g.sections[i + 1].startS + 1f : g.finishS;
                var r = LabCourseTests.Drive(g.course, cp, until, 40f, 60, false);
                if (!r.finished || r.falls > 0) TestContext.Out.WriteLine(r.log.ToString());
                Assert.That(r.finished, Is.True, g.sections[i].name);
                Assert.That(r.falls, Is.EqualTo(0), g.sections[i].name);
            }
        }

        [Test]
        public void TheRouteFitsTheReachAndTheControllersClearance()
        {
            var g = Build();
            var def = new MoveDef();
            var problems = g.CheckReach(def);
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
            var withStep = g.CheckReach(def, 0.04f, 0.35f);
            Assert.That(withStep.Exists(s => s.StartsWith("Under the slide bar", StringComparison.Ordinal)), Is.True, "the slide-gap guard catches a step offset while sliding");
            var bar = g.course.pieces.Find(p => p.name == "G Slide bar");
            Assert.That(bar.Min.Y - g.startFloorY, Is.GreaterThanOrEqualTo(CourseReach.SlideGapNeeded(def, 0.04f, 0f)));
        }

        [Test]
        public void EveryMoveIsTaughtAndThenChained()
        {
            var g = Build();
            var kinds = new HashSet<CourseBeatKind>();
            var mantles = new HashSet<float>();
            foreach (var b in g.beats)
            {
                kinds.Add(b.kind);
                if (b.kind == CourseBeatKind.Mantle) mantles.Add((float)Math.Round(b.height, 2));
            }
            foreach (var k in new[] { CourseBeatKind.Vault, CourseBeatKind.Slide, CourseBeatKind.SlideRamp, CourseBeatKind.Gap, CourseBeatKind.DoubleGap, CourseBeatKind.Wallrun, CourseBeatKind.WallToWall, CourseBeatKind.Chimney, CourseBeatKind.Mantle, CourseBeatKind.SlideHop, CourseBeatKind.Skip })
                Assert.That(kinds, Does.Contain(k), k.ToString());
            Assert.That(mantles.Count, Is.GreaterThanOrEqualTo(3), "mantles at three heights");
            for (int i = 1; i < g.beats.Count; i++) Assert.That(g.beats[i].s, Is.GreaterThanOrEqualTo(g.beats[i - 1].s - 0.01f), g.beats[i].name);
            Assert.That(g.sections.Count, Is.EqualTo(9));
            Assert.That(g.markers.Count, Is.EqualTo(g.sections.Count), "a marker in every section");
            for (int i = 0; i < g.markers.Count; i++)
            {
                var m = g.markers[i];
                var sec = g.sections[m.section];
                Assert.That(m.S, Is.InRange(sec.startS, sec.endS), m.name + " lies in its section");
            }
            foreach (var sec in g.sections)
            {
                Assert.That(sec.safeLine, Is.Not.Empty, sec.name);
                Assert.That(sec.fastLine, Is.Not.Empty, sec.name);
                Assert.That(sec.endS, Is.GreaterThan(sec.startS), sec.name);
            }
            foreach (var ch in new[] { GauntletCourse.ChapterStart, GauntletCourse.ChapterTeaching, GauntletCourse.ChapterCombo, GauntletCourse.ChapterFinish })
                Assert.That(g.course.CheckpointIndex(ch), Is.GreaterThanOrEqualTo(0), ch);
        }

        [Test]
        public void TheSafeLinesAreHoppableAndStayOffTheFastLane()
        {
            var g = Build();
            var c = g.course;
            float single = CourseReach.SingleJumpReach(new MoveDef(), new MoveDef().sprintSpeed);
            var chains = new Dictionary<string, List<LabPiece>>();
            for (int i = g.firstPiece; i < c.pieces.Count; i++)
            {
                var p = c.pieces[i];
                if (p.name.IndexOf("stone", StringComparison.OrdinalIgnoreCase) < 0 && p.name.IndexOf("Mantle stair", StringComparison.Ordinal) < 0 && p.name.IndexOf("Skip deck", StringComparison.Ordinal) < 0) continue;
                // Off the fast lane: at least 1.2 m beside it wherever it stands.
                for (float z = p.Min.Z; z <= p.Max.Z; z += 0.5f)
                {
                    float x = c.LaneAt(z);
                    float dx = x < p.Min.X ? p.Min.X - x : x > p.Max.X ? x - p.Max.X : 0f;
                    Assert.That(dx, Is.GreaterThan(1.2f), $"{p.name} is {dx:0.00} m from the fast lane at z {z - GauntletCourse.Origin:0.0}");
                }
                string key = p.name.Substring(0, p.name.LastIndexOf(' '));
                if (!chains.TryGetValue(key, out var list)) chains[key] = list = new List<LabPiece>();
                list.Add(p);
            }
            foreach (var kv in chains)
            {
                var list = kv.Value;
                for (int i = 1; i < list.Count; i++)
                {
                    float gap = list[i].Min.Z - list[i - 1].Max.Z;
                    Assert.That(gap, Is.LessThanOrEqualTo(single - 1.5f), $"{list[i].name}: a {gap:0.0} m hop on the safe line");
                }
            }
            Assert.That(chains.Count, Is.GreaterThanOrEqualTo(5), "stones beside the double gap, the wallrun, the alley, the combo walls and the long wall");
        }

        [Test]
        public void TheOldCourseIsKeptAsFreeRoamBeforeTheGauntlet()
        {
            var g = Build();
            var old = LabCourse.Build();
            Assert.That(g.firstPiece, Is.EqualTo(old.pieces.Count));
            for (int i = 0; i < old.pieces.Count; i++) Assert.That(g.course.pieces[i].name, Is.EqualTo(old.pieces[i].name));
            for (int i = 1; i < g.course.checkpoints.Count; i++) Assert.That(g.course.checkpoints[i].s, Is.GreaterThan(g.course.checkpoints[i - 1].s), g.course.checkpoints[i].name);
            Assert.That(g.course.checkpoints[g.startCheckpoint].s, Is.LessThan(g.gateS), "the start pad is before the gate");
            Assert.That(old.finishS + 40f, Is.LessThan(g.course.checkpoints[g.startCheckpoint].s), "the old finish is far from the Gauntlet");
            Assert.That(g.lowestFloor, Is.GreaterThan(g.course.killY + 6f));
        }

        [Test]
        public void TheClockStartsAtTheGateSplitsAndPenalisesAMissedMarker()
        {
            var g = Build();
            var clock = new GauntletClock(g);
            const float dt = 1f / 60f;
            // Walk the route far above every marker: the gate starts it, every section splits, every marker is missed.
            bool started = false, finished = false;
            int splits = 0, missed = 0;
            for (float s = g.gateS - 5f; s <= g.finishS + 1f; s += 0.2f)
            {
                var ev = clock.Tick(dt, new Vector3(0f, 50f, s), s, 0.38f, 1.7f);
                started |= ev.started;
                finished |= ev.finished;
                if (ev.split >= 0) splits++;
                if (ev.markerMissed >= 0) missed++;
                if (s < g.gateS) Assert.That(clock.State, Is.EqualTo(GauntletState.Ready), "nothing runs before the gate");
            }
            Assert.That(started && finished, Is.True);
            Assert.That(splits, Is.EqualTo(g.sections.Count));
            Assert.That(missed, Is.GreaterThanOrEqualTo(g.markers.Count - 1), "missed as you pass them (the last one only counts at the finish)");
            Assert.That(clock.Missed, Is.EqualTo(g.markers.Count));
            Assert.That(clock.Total, Is.EqualTo(clock.Time + g.markers.Count * GauntletCourse.MissPenalty).Within(1e-3f), "2 s a missed marker");
            Assert.That(clock.NewBest, Is.True);
            Assert.That(clock.record.best, Is.EqualTo(clock.Total));
            Assert.That(clock.record.bestSplits.Count, Is.EqualTo(g.sections.Count));

            // The restart: clock and markers back to zero; the best is kept.
            clock.Reset();
            Assert.That(clock.State, Is.EqualTo(GauntletState.Ready));
            Assert.That(clock.Time, Is.EqualTo(0f));
            Assert.That(clock.MarkersHit, Is.EqualTo(0));
            Assert.That(clock.SectionsDone, Is.EqualTo(0));
            Assert.That(clock.record.best, Is.GreaterThan(0f));

            // A slower second run: deltas are positive (red), and the best stays.
            float best = clock.record.best;
            for (float s = g.gateS - 1f; s <= g.finishS + 1f; s += 0.1f) clock.Tick(dt, new Vector3(0f, 50f, s), s, 0.38f, 1.7f);
            Assert.That(clock.SplitDelta(0), Is.GreaterThan(0f));
            Assert.That(clock.NewBest, Is.False);
            Assert.That(clock.record.best, Is.EqualTo(best));
            Assert.That(clock.record.runs, Is.EqualTo(2));
        }

        [Test]
        public void TouchingAMarkerCountsIt()
        {
            var g = Build();
            var clock = new GauntletClock(g);
            var m = g.markers[0];
            clock.Tick(0.01f, new Vector3(0f, 0f, g.gateS + 0.1f), g.gateS + 0.1f, 0.38f, 1.7f);
            var ev = clock.Tick(0.01f, new Vector3((m.min.X + m.max.X) * 0.5f, m.min.Y, m.S), m.S, 0.38f, 1.7f);
            Assert.That(ev.markerHit, Is.EqualTo(0));
            Assert.That(clock.Hit(0), Is.True);
            Assert.That(clock.MarkersHit, Is.EqualTo(1));
        }

        [Test]
        public void TheGhostRecordsAtAFixedRateAndReplaysSmoothly()
        {
            var ghost = new GhostTrack();
            float t = 0f;
            for (int i = 0; i < 200; i++)
            {
                float dt = i % 3 == 0 ? 0.013f : 0.021f;
                t += dt;
                ghost.Record(dt, new Vector3(0f, 0f, t * 10f), i < 100 ? 350f + i * 0.2f : 10f);
            }
            Assert.That(ghost.Count, Is.EqualTo((int)(t * GhostTrack.DefaultRate) + 1).Within(1));
            Assert.That(ghost.Sample(1f, out var p, out _), Is.True);
            Assert.That(p.Z, Is.EqualTo(10f).Within(0.6f), "about where the run was at 1 s");
            Assert.That(ghost.Sample(ghost.Duration + 5f, out _, out _), Is.False, "gone after the end");
            var list = new List<float>();
            ghost.CopyTo(list);
            var back = new GhostTrack();
            back.LoadFrom(list, GhostTrack.DefaultRate);
            Assert.That(back.Count, Is.EqualTo(ghost.Count));
            back.Sample(2f, out var q, out _);
            ghost.Sample(2f, out var q0, out _);
            Assert.That(Vector3.Distance(q, q0), Is.LessThan(1e-4f));
        }

        [Test]
        public void MagsTimeIsHalfAgainTheDevelopersRoundedToTwoAndAHalfSeconds()
        {
            Assert.That(DeveloperRecords.MagsTimeFor(31.42f), Is.EqualTo(47.5f), "the creator's Gauntlet best");
            Assert.That(DeveloperRecords.MagsTimeFor(28.17f), Is.EqualTo(42.5f), "the creator's dream tool best");
            Assert.That(DeveloperRecords.MagsTimeFor(20f), Is.EqualTo(30f));
            Assert.That(DeveloperRecords.MagsTimeFor(0f), Is.EqualTo(0f), "no developer time, no Mags time");
        }
    }
}
