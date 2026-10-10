using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using JamKit.Movement;
using NUnit.Framework;

namespace JamKit.Tests.Movement
{
    /// <summary>The Movement Lab's course in the fake world: the autopilot runs it start to finish with no
    /// falls (so the smoke run and the PlayMode test can), the gaps and walls have the research's sizes, the
    /// chase's pursuer keeps its distance from a good run and catches a player who stops.</summary>
    public class LabCourseTests
    {
        public static BoxWorld WorldOf(LabCourse c, List<BoxWorld.Box> byPiece)
        {
            var w = new BoxWorld();
            foreach (var p in c.pieces)
            {
                BoxWorld.Box b = null;
                if (p.solid)
                {
                    b = p.kind == LabPieceKind.Ramp
                        ? w.Ramp(p.Min.X, p.Max.X, p.Min.Z, p.Max.Z, p.rampTopStart, p.rampTopEnd, p.Min.Y)
                        : w.Add(p.center, p.size, p.runnable);
                }
                byPiece?.Add(b);
            }
            return w;
        }

        public sealed class RunReport
        {
            public bool finished;
            public int falls, caught;
            public float seconds, topSpeed;
            public readonly StringBuilder log = new StringBuilder();
            public readonly Dictionary<string, float> sectionAverage = new Dictionary<string, float>();
        }

        /// <summary>Runs the autopilot from a checkpoint until the finish (or <paramref name="untilS"/>) at
        /// <paramref name="fps"/> through a <see cref="MoveDriver"/>, the way the Unity controller does.</summary>
        public static RunReport Drive(LabCourse c, int fromCheckpoint, float untilS, float maxSeconds, int fps = 60, bool chase = true, int jitterSeed = 0, float skin = 0f)
        {
            var jitter = jitterSeed != 0 ? new Random(jitterSeed) : null;
            var pieces = new List<BoxWorld.Box>();
            var world = WorldOf(c, pieces);
            world.skin = skin;
            var sim = new MoveSim(world, new MoveDef());
            var driver = new MoveDriver(sim);
            var pilot = new CourseAutopilot(c);
            var r = new RunReport();
            var cp = c.checkpoints[fromCheckpoint];
            driver.Teleport(cp.position, cp.yaw, new Vector3(0f, 0f, sim.def.sprintSpeed * (fromCheckpoint == 0 ? 0f : 1f)));
            pilot.Reset(cp.s);
            var pursuer = new Pursuer { targetGap = c.pursuer.targetGap, k = c.pursuer.k, minSpeed = c.pursuer.minSpeed, maxSpeed = c.pursuer.maxSpeed };
            bool chasing = false;
            var fired = new bool[c.collapses.Count];
            int lastCp = fromCheckpoint;
            float dt = 1f / fps;
            float baseDt = dt;
            string lastMode = "";
            var sums = new Dictionary<string, Vector2>();
            for (float t = 0f; t < maxSeconds; t += dt)
            {
                // Uneven frames, like a busy editor: anywhere from 400 fps to 20 fps.
                if (jitter != null) dt = jitter.NextDouble() < 0.1 ? 0.05f : (float)(0.0025 + jitter.NextDouble() * 0.02);
                else dt = baseDt;
                var input = pilot.Next(sim);
                driver.Frame(dt, input);
                var p = sim.Position;
                float s = c.route.Project(p);
                r.topSpeed = Math.Max(r.topSpeed, sim.HorizontalSpeed);
                string sec = c.SectionAt(s);
                sums.TryGetValue(sec, out var acc);
                sums[sec] = acc + new Vector2(sim.HorizontalSpeed, 1f);
                string mode = sim.Mode.ToString();
                if (mode != lastMode)
                {
                    r.log.AppendLine($"{t,6:0.00}s z {p.Z,7:0.0} x {p.X,5:0.0} y {p.Y,6:0.00} {mode,-8} speed {sim.HorizontalSpeed,5:0.0}");
                    lastMode = mode;
                }
                for (int i = 0; i < c.checkpoints.Count; i++)
                    if (i > lastCp && s >= c.checkpoints[i].s) lastCp = i;
                for (int i = 0; i < fired.Length; i++)
                {
                    if (fired[i] || s < c.collapses[i].triggerS) continue;
                    fired[i] = true;
                    for (int k = 0; k < c.pieces.Count; k++)
                        if (c.pieces[k].collapse == i && pieces[k] != null)
                        {
                            Assert.That(c.pieces[k].Max.Z, Is.LessThan(s), c.pieces[k].name + " collapses behind the player");
                            pieces[k].enabled = false;
                        }
                    r.log.AppendLine($"{t,6:0.00}s collapse: {c.collapses[i].name}");
                }
                if (chase && !chasing && s >= c.chaseStartS && s < c.chaseEndS) { chasing = true; pursuer.Reset(s, sim.HorizontalSpeed); }
                if (chasing && s >= c.chaseEndS) chasing = false;
                bool caught = chasing && pursuer.Step(s, Vector3.Dot(sim.Velocity, c.route.DirectionAt(s)), dt);
                if (p.Y < c.killY || caught)
                {
                    if (caught) r.caught++; else r.falls++;
                    r.log.AppendLine($"{t,6:0.00}s {(caught ? "CAUGHT" : "FELL")} at z {p.Z:0.0} x {p.X:0.0}");
                    var back = c.checkpoints[lastCp];
                    driver.Teleport(back.position, back.yaw, new Vector3(0f, 0f, sim.def.sprintSpeed));
                    pilot.Reset(back.s);
                    for (int i = 0; i < fired.Length; i++)
                        if (fired[i] && c.collapses[i].triggerS > back.s)
                        {
                            fired[i] = false;
                            for (int k = 0; k < c.pieces.Count; k++) if (c.pieces[k].collapse == i && pieces[k] != null) pieces[k].enabled = true;
                        }
                    if (chasing) pursuer.Reset(back.s, sim.def.sprintSpeed);
                    if (r.falls + r.caught > 5) break;
                    continue;
                }
                if (s >= untilS && sim.Grounded)
                {
                    r.finished = true;
                    r.seconds = t;
                    break;
                }
            }
            foreach (var kv in sums) r.sectionAverage[kv.Key] = kv.Value.X / Math.Max(1f, kv.Value.Y);
            return r;
        }

        [Test]
        public void TheAutopilotFinishesTheWholeCourseWithoutFalling()
        {
            var c = LabCourse.Build();
            var r = Drive(c, 0, c.finishS, 120f);
            TestContext.Out.WriteLine(r.log.ToString());
            foreach (var kv in r.sectionAverage) TestContext.Out.WriteLine($"{kv.Key}: average {kv.Value:0.0} m/s");
            TestContext.Out.WriteLine($"finished {r.finished} in {r.seconds:0.0} s, top speed {r.topSpeed:0.0} m/s, falls {r.falls}, caught {r.caught}");
            Assert.That(r.finished, Is.True, "reached the finish");
            Assert.That(r.falls, Is.EqualTo(0), "no falls");
            Assert.That(r.caught, Is.EqualTo(0), "the pursuer never caught a good run");
            Assert.That(r.seconds, Is.InRange(35f, 90f));
        }

        [TestCase(20, 0)]
        [TestCase(144, 0)]
        [TestCase(300, 0)]
        [TestCase(1000, 0)]
        [TestCase(0, 7)]
        [TestCase(0, 11)]
        public void TheCourseFinishesAtAnyFrameRate(int fps, int jitterSeed)
        {
            var c = LabCourse.Build();
            // With a CharacterController's 4 cm skin, as in Unity.
            var r = Drive(c, 0, c.finishS, 150f, fps == 0 ? 60 : fps, true, jitterSeed, 0.04f);
            TestContext.Out.WriteLine(r.log.ToString());
            Assert.That(r.finished, Is.True, fps + " fps, jitter " + jitterSeed);
            Assert.That(r.falls, Is.EqualTo(0), fps + " fps, jitter " + jitterSeed);
            Assert.That(r.caught, Is.EqualTo(0));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(5)]
        public void EveryCheckpointCanBeRunFromToTheNextOne(int index)
        {
            var c = LabCourse.Build();
            float until = index + 1 < c.checkpoints.Count ? c.checkpoints[index + 1].s + 1f : c.finishS;
            var r = Drive(c, index, until, 60f);
            TestContext.Out.WriteLine(r.log.ToString());
            Assert.That(r.finished, Is.True, c.checkpoints[index].name);
            Assert.That(r.falls, Is.EqualTo(0), c.checkpoints[index].name);
        }

        [Test]
        public void TheChainSectionRunsAtSpeedAt30And144Fps()
        {
            var c = LabCourse.Build();
            int chain = c.CheckpointIndex(LabCourse.ChapterChain);
            foreach (int fps in new[] { 30, 144 })
            {
                var r = Drive(c, chain, 236f, 10f, fps);
                TestContext.Out.WriteLine(fps + " fps:\n" + r.log);
                Assert.That(r.finished, Is.True, fps + " fps");
                Assert.That(r.topSpeed, Is.GreaterThan(13f), fps + " fps: the chain goes well past sprint");
            }
        }

        [Test]
        public void GapsAndWallsHaveTheResearchSizes()
        {
            var c = LabCourse.Build();
            float Gap(string landing)
            {
                var land = c.pieces.Find(p => p.name == landing);
                float edge = float.MinValue;
                foreach (var p in c.pieces)
                    if (p.solid && p.kind != LabPieceKind.Wall && p.kind != LabPieceKind.Bar && p.Max.Z <= land.Min.Z + 0.01f) edge = Math.Max(edge, p.Max.Z);
                return land.Min.Z - edge;
            }
            Assert.That(Gap("Gap landing 1"), Is.InRange(5f, 6.2f), "a single jump's gap (about 6 m at sprint)");
            Assert.That(Gap("Gap landing 2"), Is.InRange(8f, 10f), "a double jump's gap (about 10 m)");
            var walls = c.pieces.FindAll(p => p.kind == LabPieceKind.Wall && p.name.StartsWith("Wall", StringComparison.Ordinal));
            Assert.That(walls.Count, Is.EqualTo(3));
            Assert.That(walls[0].size.Z, Is.LessThan(walls[1].size.Z));
            Assert.That(walls[1].size.Z, Is.LessThan(walls[2].size.Z));
            Assert.That(walls[2].size.Z, Is.EqualTo(21f).Within(0.5f), "the longest wall: a full 1.75 s run at the old 12 m/s");
            Assert.That(c.checkpoints.Count, Is.GreaterThanOrEqualTo(6));
            foreach (var ch in new[] { LabCourse.ChapterStart, LabCourse.ChapterWallruns, LabCourse.ChapterChase })
                Assert.That(c.CheckpointIndex(ch), Is.GreaterThanOrEqualTo(0), ch);
            Assert.That(c.collapses.Count, Is.EqualTo(2));
        }

        [Test]
        public void ThePursuerKeepsItsGapAndCatchesAPlayerWhoStops()
        {
            var p = new Pursuer();
            float s = 0f;
            p.Reset(s, 10f);
            for (int i = 0; i < 600; i++) { s += 10f / 60f; Assert.That(p.Step(s, 10f, 1f / 60f), Is.False); }
            Assert.That(p.Gap(s), Is.EqualTo(12f).Within(1.5f), "steady running: the gap settles near the target");
            float fast = s;
            for (int i = 0; i < 300; i++) { fast += 14f / 60f; p.Step(fast, 14f, 1f / 60f); }
            Assert.That(p.Gap(fast), Is.GreaterThan(15f), "faster than its cap: you pull away");
            bool caught = false;
            float t = 0f;
            for (int i = 0; i < 1200 && !caught; i++) { caught = p.Step(fast, 0f, 1f / 60f); t += 1f / 60f; }
            Assert.That(caught, Is.True, "standing still: caught");
            Assert.That(t, Is.GreaterThan(1.5f), "not instantly: the gap closes at its own speed, then 0.75 s close");
        }

        [Test]
        public void TheRouteProjectsDistanceAndSide()
        {
            var line = new PathLine(new[] { new Vector3(0, 0, 0), new Vector3(0, 0, 10), new Vector3(10, 0, 10) });
            Assert.That(line.Length, Is.EqualTo(20f).Within(1e-4f));
            Assert.That(line.Project(new Vector3(1f, 5f, 5f), out float lat), Is.EqualTo(5f).Within(1e-4f));
            Assert.That(lat, Is.EqualTo(1f).Within(1e-4f), "x+ is to the right going +Z");
            Assert.That(line.Project(new Vector3(5f, 0f, 9f)), Is.EqualTo(15f).Within(1e-3f));
            Assert.That(line.PointAt(15f, 0f).X, Is.EqualTo(5f).Within(1e-4f));
        }
    }
}
