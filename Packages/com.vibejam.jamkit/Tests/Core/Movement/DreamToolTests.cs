using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using JamKit.Movement;
using NUnit.Framework;

namespace JamKit.Tests.Movement
{
    /// <summary>The Movement Lab's dream tool chapter in the fake world: the autopilot (firing the tool from its cues)
    /// clears all three sections at 20 to 300 fps and with jittery frames, with no falls and every required target hit,
    /// in the developer's time; without the tool a glass tile stops it; every target can be shot from the route, in range
    /// and in sight; the built pieces close gaps nothing else crosses; the reach checks pass with them built; the colour
    /// flip makes the other colour solid; the aim assist locks the target nearest the crosshair, wider on a pad; the
    /// clock runs the chapter's own gate, splits and finish. It prints the seconds between required shots (the pacing).</summary>
    public class DreamToolTests
    {
        public sealed class RunResult
        {
            public bool finished;
            public int falls, firedRequired, firedExpert;
            public float seconds;
            public GauntletClock clock;
            public DreamToolState state;
            public readonly List<float> shotTimes = new List<float>();
            public readonly List<string> shotNames = new List<string>();
            public readonly StringBuilder log = new StringBuilder();
            public float maxS;
        }

        /// <summary>The fake world with the tool's pieces in it, each box on or off as the state says.</summary>
        public sealed class ToolWorld : IDreamToolSight
        {
            public readonly BoxWorld world;
            public readonly List<BoxWorld.Box> boxes = new List<BoxWorld.Box>();
            readonly DreamToolState _state;
            int _version = -1;

            public ToolWorld(DreamToolCourse d, DreamToolState state)
            {
                _state = state;
                world = LabCourseTests.WorldOf(d.course, null);
                foreach (var p in d.pieces)
                {
                    var b = world.Add(p.Center, p.Size, p.runnable);
                    boxes.Add(b);
                }
                Sync();
            }

            public void Sync()
            {
                if (_state.Version == _version) return;
                _version = _state.Version;
                for (int i = 0; i < boxes.Count; i++) boxes[i].enabled = _state.Solid(i);
            }

            public bool Blocked(Vector3 from, Vector3 to, float clearance)
            {
                Vector3 d = to - from;
                float len = d.Length();
                if (len < 1e-3f) return false;
                d /= len;
                foreach (var b in world.Boxes)
                {
                    if (!b.enabled) continue;
                    if (RayBox(from, d, b.min, b.max, out float t) && t < len - clearance) return true;
                }
                return false;
            }

            static bool RayBox(Vector3 o, Vector3 d, Vector3 mn, Vector3 mx, out float t)
            {
                float tmin = 0f, tmax = float.MaxValue;
                t = 0f;
                for (int a = 0; a < 3; a++)
                {
                    float oo = a == 0 ? o.X : a == 1 ? o.Y : o.Z, dd = a == 0 ? d.X : a == 1 ? d.Y : d.Z;
                    float lo = a == 0 ? mn.X : a == 1 ? mn.Y : mn.Z, hi = a == 0 ? mx.X : a == 1 ? mx.Y : mx.Z;
                    if (Math.Abs(dd) < 1e-8f) { if (oo < lo || oo > hi) return false; continue; }
                    float t1 = (lo - oo) / dd, t2 = (hi - oo) / dd;
                    if (t1 > t2) { float x = t1; t1 = t2; t2 = x; }
                    tmin = Math.Max(tmin, t1);
                    tmax = Math.Min(tmax, t2);
                    if (tmin > tmax) return false;
                }
                t = tmin;
                return true;
            }
        }

        static Vector3 Eye(MoveSim sim) => sim.Position + new Vector3(0f, sim.def.eyeHeight, 0f);

        /// <summary>The autopilot from the chapter's start pad (standing) to its finish, the tool fired by the pilot (or not).</summary>
        public static RunResult Run(DreamToolCourse d, int fps, int jitterSeed = 0, bool tool = true, float maxSeconds = 120f, int fromCheckpoint = -1, float untilS = float.MaxValue)
        {
            var c = d.course;
            var state = new DreamToolState(d);
            var tw = new ToolWorld(d, state);
            tw.world.skin = 0.04f;
            var sim = new MoveSim(tw.world, new MoveDef());
            var driver = new MoveDriver(sim);
            var pilot = new CourseAutopilot(c);
            var trigger = new DreamToolPilot(d);
            var def = new DreamToolDef();
            var clock = new GauntletClock(d.run);
            var r = new RunResult { clock = clock, state = state };
            var jitter = jitterSeed != 0 ? new Random(jitterSeed) : null;
            int cp0 = fromCheckpoint >= 0 ? fromCheckpoint : d.startCheckpoint;
            var start = c.checkpoints[cp0];
            driver.Teleport(start.position, 0f, cp0 == d.startCheckpoint ? Vector3.Zero : new Vector3(0f, 0f, sim.def.sprintSpeed));
            pilot.Reset(start.s);
            trigger.Reset(start.s);
            state.ResetFrom(cp0 == d.startCheckpoint ? float.MinValue : start.s, d.ColourAt(cp0));
            tw.Sync();
            int lastCp = cp0;
            string lastMode = "";
            float dt = 1f / fps;
            for (float t = 0f; t < maxSeconds; t += dt)
            {
                if (jitter != null) dt = jitter.NextDouble() < 0.1 ? 0.05f : (float)(0.0025 + jitter.NextDouble() * 0.02);
                driver.Frame(dt, pilot.Next(sim));
                state.Tick(dt);
                var p = sim.Position;
                float s = c.route.Project(p);
                r.maxS = Math.Max(r.maxS, s);
                if (tool)
                {
                    int target = trigger.Next(s, Eye(sim), state, def.range);
                    if (target >= 0)
                    {
                        var shot = state.Fire(target, def.cooldown);
                        tw.Sync();
                        var tg = d.targets[target];
                        if (tg.expert) r.firedExpert++; else { r.firedRequired++; r.shotTimes.Add(clock.Time); r.shotNames.Add(tg.name); }
                        r.log.AppendLine($"{t,6:0.00}s SHOT {tg.name} from z {s - DreamToolCourse.Origin:0.0} ({Vector3.Distance(Eye(sim), tg.center):0.0} m, {sim.Mode}) colour {shot.colour}");
                    }
                }
                for (int i = lastCp + 1; i < c.checkpoints.Count; i++) if (s >= c.checkpoints[i].s) lastCp = i;
                var ev = clock.Tick(dt, p, s, sim.def.radius, sim.Height, true);
                string mode = sim.Mode.ToString();
                if (mode != lastMode) { r.log.AppendLine($"{t,6:0.00}s z {p.Z - DreamToolCourse.Origin,7:0.0} x {p.X,5:0.0} y {p.Y,6:0.00} {mode,-8} speed {sim.HorizontalSpeed,5:0.0}"); lastMode = mode; }
                if (p.Y < c.killY)
                {
                    r.falls++;
                    r.log.AppendLine($"{t,6:0.00}s FELL at z {p.Z - DreamToolCourse.Origin:0.0} x {p.X:0.0}");
                    var back = c.checkpoints[lastCp];
                    driver.Teleport(back.position, 0f, new Vector3(0f, 0f, sim.def.sprintSpeed));
                    pilot.Reset(back.s);
                    trigger.Reset(back.s);
                    state.ResetFrom(back.s, d.ColourAt(lastCp));
                    tw.Sync();
                    if (r.falls > 5) break;
                    continue;
                }
                if (ev.finished || s >= untilS) { r.finished = true; r.seconds = ev.finished ? clock.Time : t; break; }
            }
            return r;
        }

        static string Pacing(RunResult r)
        {
            var sb = new StringBuilder("Required shots (clock time, seconds since the previous):\n");
            for (int i = 0; i < r.shotTimes.Count; i++)
                sb.AppendLine($"  {r.shotTimes[i],6:0.00} s  +{(i == 0 ? r.shotTimes[i] : r.shotTimes[i] - r.shotTimes[i - 1]),5:0.00}  {r.shotNames[i]}");
            return sb.ToString();
        }

        [Test]
        public void TheAutopilotClearsTheChapterAndSetsTheDevelopersTime()
        {
            var d = DreamToolCourse.Build();
            var r = Run(d, 60);
            TestContext.Out.WriteLine(r.log.ToString());
            TestContext.Out.WriteLine(Pacing(r));
            TestContext.Out.WriteLine($"finished {r.finished} in {r.seconds:0.00} s, falls {r.falls}, required {r.firedRequired}/{d.RequiredCount}, expert {r.firedExpert}");
            for (int i = 0; i < d.run.sections.Count; i++) TestContext.Out.WriteLine($"  split {d.run.sections[i].name}: {r.clock.Split(i):0.00}");
            Assert.That(r.finished, Is.True);
            Assert.That(r.falls, Is.EqualTo(0));
            Assert.That(r.state.RequiredDone, Is.EqualTo(d.RequiredCount), "every tile popped and every required socket built");
            Assert.That(r.firedExpert, Is.EqualTo(1), "the expert line takes the optional shot");
            Assert.That(DreamToolCourse.AutopilotTime, Is.EqualTo(r.seconds).Within(0.6f), "the autopilot's time: update DreamToolCourse.AutopilotTime");
            for (int i = 1; i < r.shotTimes.Count; i++)
                Assert.That(r.shotTimes[i] - r.shotTimes[i - 1], Is.GreaterThanOrEqualTo(2.5f), "required shots are spaced out: " + r.shotNames[i]);
        }

        [TestCase(20, 0)]
        [TestCase(144, 0)]
        [TestCase(300, 0)]
        [TestCase(0, 7)]
        [TestCase(0, 11)]
        public void TheChapterFinishesAtAnyFrameRate(int fps, int jitterSeed)
        {
            var d = DreamToolCourse.Build();
            var r = Run(d, fps == 0 ? 60 : fps, jitterSeed);
            if (!r.finished || r.falls > 0) TestContext.Out.WriteLine(r.log.ToString());
            Assert.That(r.finished, Is.True, fps + " fps, jitter " + jitterSeed);
            Assert.That(r.falls, Is.EqualTo(0), fps + " fps, jitter " + jitterSeed);
            Assert.That(r.state.RequiredDone, Is.EqualTo(d.RequiredCount), fps + " fps, jitter " + jitterSeed);
        }

        [Test]
        public void TheMainLineFinishesWithoutTheExpertShot()
        {
            var d = DreamToolCourse.Build(false);
            var r = Run(d, 60);
            TestContext.Out.WriteLine(r.log.ToString());
            TestContext.Out.WriteLine($"main line {r.seconds:0.00} s (developer's, expert line: {DreamToolCourse.AutopilotTime:0.0})");
            Assert.That(r.finished, Is.True);
            Assert.That(r.falls, Is.EqualTo(0));
            Assert.That(r.firedExpert, Is.EqualTo(0));
            Assert.That(r.seconds, Is.GreaterThan(DreamToolCourse.AutopilotTime), "the expert's wall saves time");
        }

        [Test]
        public void EveryCheckpointRunsToTheNext()
        {
            var d = DreamToolCourse.Build();
            var c = d.course;
            for (int i = d.startCheckpoint + 1; i < c.checkpoints.Count; i++)
            {
                float until = i + 1 < c.checkpoints.Count ? c.checkpoints[i + 1].s + 1f : d.run.finishS + 1f;
                var r = Run(d, 60, 0, true, 40f, i, until);
                if (!r.finished || r.falls > 0) TestContext.Out.WriteLine(r.log.ToString());
                Assert.That(r.finished, Is.True, c.checkpoints[i].name);
                Assert.That(r.falls, Is.EqualTo(0), c.checkpoints[i].name);
            }
        }

        [Test]
        public void WithoutTheToolTheFirstTileBlocksTheLane()
        {
            var d = DreamToolCourse.Build();
            var r = Run(d, 60, 0, false, 12f);
            var pane = d.targets[0];
            Assert.That(pane.kind, Is.EqualTo(DreamTargetKind.Tile));
            Assert.That(r.finished, Is.False);
            Assert.That(r.falls, Is.EqualTo(0), "it stops at the glass, it doesn't fall");
            Assert.That(r.maxS, Is.LessThan(pane.S), "never past the first pane");
            Assert.That(r.maxS, Is.GreaterThan(pane.S - 1.5f), "right up against it");
        }

        [Test]
        public void EveryTargetCanBeShotFromTheRouteInRangeAndInSight()
        {
            var d = DreamToolCourse.Build();
            var def = new DreamToolDef();
            var state = new DreamToolState(d);
            var tw = new ToolWorld(d, state);
            var c = d.course;
            float eye = new MoveDef().eyeHeight;
            foreach (var cue in d.pilotCues)
            {
                var t = d.targets[cue.target];
                // From the lane at the cue's s, at standing eye height over the floor (or the route's height there):
                float x = c.LaneAt(cue.s);
                float floor = FloorBelow(tw.world, x, cue.s, t.center.Y + 2f);
                var from = new Vector3(x, floor + eye, cue.s);
                float dist = Vector3.Distance(from, t.center);
                Assert.That(dist - t.radius, Is.LessThanOrEqualTo(def.range), t.name + " in range");
                Assert.That(t.S, Is.GreaterThan(cue.s), t.name + " is ahead");
                Assert.That(tw.Blocked(from, t.center, t.radius + 0.25f), Is.False, t.name + " in sight from z " + (cue.s - DreamToolCourse.Origin));
                state.Apply(cue.target);
                tw.Sync();
            }
            // Every required target has a cue (the autopilot shoots them all); the switches' cues alternate the colour.
            for (int i = 0; i < d.targets.Count; i++)
                Assert.That(d.pilotCues.Exists(q => q.target == i), Is.True, d.targets[i].name + " has an autopilot cue");
            int colour = DreamToolCourse.Pink;
            foreach (var q in d.pilotCues)
            {
                if (d.targets[q.target].kind != DreamTargetKind.Switch) continue;
                Assert.That(q.colour, Is.EqualTo(1 - colour), d.targets[q.target].name + " flips to the other colour");
                colour = q.colour;
            }
        }

        static float FloorBelow(BoxWorld w, float x, float z, float from)
        {
            float best = float.MinValue;
            foreach (var b in w.Boxes)
            {
                if (!b.enabled || x < b.min.X || x > b.max.X || z < b.min.Z || z > b.max.Z) continue;
                float top = b.TopAt(z);
                if (top <= from && top > best) best = top;
            }
            return best == float.MinValue ? from - 2f : best;
        }

        [Test]
        public void TheBuildsCloseGapsNothingElseCrossesAndTheRouteFitsTheReach()
        {
            var d = DreamToolCourse.Build();
            var def = new MoveDef();
            var problems = d.CheckReach(def);
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
            float best = CourseReach.DoubleJumpReach(def, def.slideBoostMaxSpeed);
            Assert.That(d.unbuiltGaps.Count, Is.GreaterThanOrEqualTo(2));
            foreach (var kv in d.unbuiltGaps)
                Assert.That(kv.Value, Is.GreaterThan(best + 2f), d.targets[kv.Key].name + ": unbuilt, nothing short of air strafing crosses it");
            var kinds = new HashSet<CourseBeatKind>();
            foreach (var b in d.beats) kinds.Add(b.kind);
            foreach (var k in new[] { CourseBeatKind.Gap, CourseBeatKind.Wallrun, CourseBeatKind.WallToWall, CourseBeatKind.Mantle }) Assert.That(kinds, Does.Contain(k), k.ToString());
            // Every built piece is reachable: a bridge joins the floors either side; a wall is long and tall enough to run.
            foreach (var p in d.pieces)
            {
                if (p.role != DreamPieceRole.Built) continue;
                if (p.rises)
                {
                    Assert.That(p.Size.Z, Is.InRange(8f, CourseReach.WallMax), p.name);
                    Assert.That(p.runnable, Is.True, p.name);
                }
                else
                {
                    Assert.That(HasFloorAt(d, p.min.Z - 0.5f, p.max.Y), Is.True, p.name + ": a floor at its near end");
                    Assert.That(HasFloorAt(d, p.max.Z + 0.5f, p.max.Y), Is.True, p.name + ": a floor at its far end");
                }
            }
        }

        static bool HasFloorAt(DreamToolCourse d, float z, float top)
        {
            foreach (var p in d.course.pieces)
                if (p.solid && z >= p.Min.Z && z <= p.Max.Z && Math.Abs(p.Max.Y - top) < 0.05f && p.Min.X <= 0f && p.Max.X >= 0f) return true;
            return false;
        }

        [Test]
        public void ThreeSectionsAfterTheGauntletWithTheirOwnClock()
        {
            var d = DreamToolCourse.Build();
            var g = GauntletCourse.Build();
            Assert.That(d.firstPiece, Is.EqualTo(g.course.pieces.Count), "the Gauntlet is untouched before it");
            for (int i = 0; i < g.course.pieces.Count; i++) Assert.That(d.course.pieces[i].name, Is.EqualTo(g.course.pieces[i].name));
            Assert.That(d.course.checkpoints[d.startCheckpoint].chapter, Is.EqualTo(DreamToolCourse.Chapter));
            Assert.That(d.course.checkpoints[d.startCheckpoint].s, Is.GreaterThanOrEqualTo(GauntletCourse.End));
            Assert.That(g.InGauntlet(d.run.gateS), Is.False, "the Gauntlet's HUD and restart stop before the chapter");
            Assert.That(DreamToolCourse.InChapter(d.course.checkpoints[d.startCheckpoint].s), Is.True);
            for (int i = 1; i < d.course.checkpoints.Count; i++) Assert.That(d.course.checkpoints[i].s, Is.GreaterThan(d.course.checkpoints[i - 1].s), d.course.checkpoints[i].name);
            Assert.That(d.run.sections.Count, Is.EqualTo(3));
            var kinds = new HashSet<DreamTargetKind>();
            foreach (var t in d.targets) kinds.Add(t.kind);
            Assert.That(kinds.Count, Is.EqualTo(3), "tiles, sockets and switches");
            for (int s = 0; s < 3; s++) Assert.That(d.targets.Exists(t => t.section == s && !t.expert), Is.True, "required shots in section " + s);
            Assert.That(d.targets.FindAll(t => t.expert).Count, Is.EqualTo(1), "one optional expert shot");
            Assert.That(d.lowestFloor, Is.GreaterThan(d.course.killY + 6f));
            // Every checkpoint stands on something solid in its own colour.
            var state = new DreamToolState(d);
            var tw = new ToolWorld(d, state);
            for (int i = d.startCheckpoint; i < d.course.checkpoints.Count; i++)
            {
                var cp = d.course.checkpoints[i];
                state.ResetFrom(cp.s, d.ColourAt(i));
                tw.Sync();
                Assert.That(tw.world.CastDown(cp.position, 0.38f, 0.2f, out _), Is.True, cp.name + " stands on something");
            }

            // The clock: the chapter's gate starts it, three splits, the finish stops it; no markers, no penalty.
            var clock = new GauntletClock(d.run);
            bool started = false, finished = false;
            int splits = 0;
            for (float s = d.run.gateS - 5f; s <= d.run.finishS + 1f; s += 0.2f)
            {
                var ev = clock.Tick(1f / 60f, new Vector3(0f, 50f, s), s, 0.38f, 1.7f);
                started |= ev.started; finished |= ev.finished;
                if (ev.split >= 0) splits++;
            }
            Assert.That(started && finished, Is.True);
            Assert.That(splits, Is.EqualTo(3));
            Assert.That(clock.Penalty, Is.EqualTo(0f));
        }

        [Test]
        public void TheSwitchFlipsWhichColourIsSolid()
        {
            var d = DreamToolCourse.Build();
            var st = new DreamToolState(d);
            int pink = d.pieces.FindIndex(p => p.role == DreamPieceRole.Pink);
            int blue = d.pieces.FindIndex(p => p.role == DreamPieceRole.Blue);
            int sw = d.targets.FindIndex(t => t.kind == DreamTargetKind.Switch);
            Assert.That(pink, Is.GreaterThanOrEqualTo(0));
            Assert.That(blue, Is.GreaterThanOrEqualTo(0));
            Assert.That(st.Colour, Is.EqualTo(DreamToolCourse.Pink));
            Assert.That(st.Solid(pink) && !st.Solid(blue), Is.True, "pink first");
            int v = st.Version;
            var shot = st.Fire(sw, 0.25f);
            Assert.That(shot.fired && shot.target == sw && shot.colour == DreamToolCourse.Blue, Is.True);
            Assert.That(!st.Solid(pink) && st.Solid(blue), Is.True, "a flip makes the other colour solid");
            Assert.That(st.Version, Is.Not.EqualTo(v));
            Assert.That(st.Fire(sw, 0.25f).fired, Is.False, "the cooldown");
            st.Tick(0.26f);
            st.Fire(sw, 0.25f);
            Assert.That(st.Solid(pink) && !st.Solid(blue), Is.True, "and back");
            Assert.That(st.Valid(sw), Is.True, "a switch can always be shot again");

            // Tiles pop and sockets build once; a respawn puts back what's ahead and keeps what's behind.
            int tile = d.targets.FindIndex(t => t.kind == DreamTargetKind.Tile);
            int socket = d.targets.FindIndex(t => t.kind == DreamTargetKind.Socket && !t.expert);
            int tilePiece = d.pieces.FindIndex(p => p.target == tile);
            int built = d.pieces.FindIndex(p => p.target == socket);
            Assert.That(st.Solid(tilePiece) && !st.Solid(built), Is.True);
            st.Apply(tile); st.Apply(socket);
            Assert.That(!st.Solid(tilePiece) && st.Solid(built), Is.True);
            Assert.That(st.Valid(tile), Is.False, "a popped tile can't be locked again");
            st.ResetFrom(d.targets[tile].resetS + 1f, DreamToolCourse.Blue);
            Assert.That(st.Done(tile), Is.True, "behind the checkpoint: stays popped");
            Assert.That(st.Done(socket), Is.False, "ahead: back to unbuilt");
            Assert.That(st.Colour, Is.EqualTo(DreamToolCourse.Blue), "the checkpoint's colour");
            st.ResetFrom(float.MinValue, DreamToolCourse.Pink);
            Assert.That(st.Done(tile), Is.False, "a fresh run");
            Assert.That(d.ColourAt(d.startCheckpoint), Is.EqualTo(DreamToolCourse.Pink));
            Assert.That(d.ColourAt(d.course.CheckpointIndex(LabCourse.ChapterStart)), Is.EqualTo(DreamToolCourse.Pink));
        }

        [Test]
        public void TheAimAssistLocksTheTargetNearestTheCrosshair()
        {
            var d = DreamToolCourse.Build();
            var st = new DreamToolState(d);
            var def = new DreamToolDef();
            var pane = d.targets[0];
            var eye = new Vector3(0f, pane.center.Y, pane.S - 20f);
            var fwd = new Vector3(0f, 0f, 1f);
            Assert.That(DreamToolAim.Pick(d, st, eye, fwd, def.coneMouse, def.range, null, out float a0), Is.EqualTo(0), "straight at it");
            Assert.That(a0, Is.EqualTo(0f));
            // Off to the side: 10 degrees off the middle locks (the pane is 5.4 degrees wide each side); 20 degrees doesn't.
            float yaw = 10f * (float)Math.PI / 180f;
            var off10 = new Vector3((float)Math.Sin(yaw), 0f, (float)Math.Cos(yaw));
            Assert.That(DreamToolAim.Pick(d, st, eye, off10, def.coneMouse, def.range, null, out float a1), Is.EqualTo(0), "10 degrees off the middle of a 1.9 m pane at 20 m: within its edge plus the cone");
            Assert.That(a1, Is.GreaterThan(0f).And.LessThanOrEqualTo(def.coneMouse));
            yaw = 20f * (float)Math.PI / 180f;
            var off20 = new Vector3((float)Math.Sin(yaw), 0f, (float)Math.Cos(yaw));
            Assert.That(DreamToolAim.Pick(d, st, eye, off20, def.coneMouse, def.range, null, out _), Is.EqualTo(-1), "20 degrees off: no lock on mouse");
            // The pad's cone is wider: a target the mouse misses, the pad locks.
            float pad = FindAngleBetween(d, st, eye, pane, def);
            Assert.That(pad, Is.GreaterThan(0f), "an angle that only the pad locks");
            Assert.That(def.conePad, Is.GreaterThan(def.coneMouse));
            // Out of range, behind, or done: no lock.
            Assert.That(DreamToolAim.Pick(d, st, new Vector3(0f, pane.center.Y, pane.S - 60f), fwd, def.conePad, def.range, null, out _), Is.EqualTo(-1), "out of range");
            Assert.That(DreamToolAim.Pick(d, st, new Vector3(0f, pane.center.Y, pane.S + 5f), fwd, def.conePad, def.range, null, out _), Is.Not.EqualTo(0), "behind you");
            st.Apply(0);
            Assert.That(DreamToolAim.Pick(d, st, eye, fwd, def.conePad, def.range, null, out _), Is.Not.EqualTo(0), "a popped tile");

            // Two targets in the cone: the one nearer the crosshair wins, not the nearer in metres.
            var two = DreamToolCourse.Build();
            var st2 = new DreamToolState(two);
            int a = 0, b = 1;
            two.targets[a].center = new Vector3(1.2f, 0f, 10f); two.targets[a].radius = 0.5f;   // near, 4.0 degrees off its edge
            two.targets[b].center = new Vector3(0.6f, 0f, 30f); two.targets[b].radius = 0.5f;   // far, nearly on the crosshair
            for (int i = 2; i < two.targets.Count; i++) two.targets[i].center = new Vector3(0f, 0f, -500f);
            Assert.That(DreamToolAim.Pick(two, st2, Vector3.Zero, fwd, def.coneMouse, def.range, null, out _), Is.EqualTo(b), "the far one sits on the crosshair");
            two.targets[b].center = new Vector3(4f, 0f, 30f);
            Assert.That(DreamToolAim.Pick(two, st2, Vector3.Zero, fwd, def.coneMouse, def.range, null, out _), Is.EqualTo(a), "now the near one is nearer the crosshair");
            // Hidden behind something solid: no lock.
            Assert.That(DreamToolAim.Pick(two, st2, Vector3.Zero, fwd, def.coneMouse, def.range, new AlwaysBlocked(), out _), Is.EqualTo(-1));
        }

        sealed class AlwaysBlocked : IDreamToolSight
        {
            public bool Blocked(Vector3 from, Vector3 to, float clearance) => true;
        }

        static float FindAngleBetween(DreamToolCourse d, DreamToolState st, Vector3 eye, DreamToolTarget pane, DreamToolDef def)
        {
            for (float deg = 1f; deg < 40f; deg += 0.25f)
            {
                float r = deg * (float)Math.PI / 180f;
                var f = new Vector3((float)Math.Sin(r), 0f, (float)Math.Cos(r));
                bool mouse = DreamToolAim.Pick(d, st, eye, f, def.coneMouse, def.range, null, out _) == 0;
                bool padLock = DreamToolAim.Pick(d, st, eye, f, def.conePad, def.range, null, out _) == 0;
                if (!mouse && padLock) return deg;
            }
            return -1f;
        }

        [Test]
        public void TheSoundsAreCleanAndDistinct()
        {
            foreach (var id in DreamToolSounds.Ids)
            {
                var s = DreamToolSounds.Make(id);
                Assert.That(s, Is.Not.Null, id);
                Assert.That(s.Length, Is.GreaterThan(DreamToolSounds.Rate / 20), id);
                float peak = 0f;
                foreach (var x in s) peak = Math.Max(peak, Math.Abs(x));
                Assert.That(peak, Is.LessThanOrEqualTo(0.891f), id + " under -1 dBFS");
                Assert.That(peak, Is.GreaterThan(0.2f), id + " audible");
                Assert.That(Math.Abs(s[0]), Is.LessThan(0.02f), id + " starts without a click");
                Assert.That(Math.Abs(s[s.Length - 1]), Is.LessThan(0.02f), id + " ends without a click");
            }
            Assert.That(DreamToolSounds.Make("nope"), Is.Null);
        }
    }
}
