using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using JamKit.DreamEscape;
using JamKit.Movement;
using JamKit.Tests.Movement;
using NUnit.Framework;

namespace JamKit.Tests.DreamEscape
{
    /// <summary>Night 2's escape (docs/redesign/house.md "The escape"): the course autopilot runs it start to finish at 20
    /// to 300 fps and with jitter, never falling and never caught; the corridor stretches the first time (the reveal) and
    /// lets a slide through the second; a run that only sprints is held in the corridor until the house closes on her
    /// (thrown out); every room is closed in (walls both sides, all runnable, a ceiling); no two solids overlap; the
    /// escape is long (a player's two to three minutes).</summary>
    public class HouseEscapeTests
    {
        public sealed class Run
        {
            public bool finished, thrownOut;
            public int stretches, breakThrough;
            public float seconds, topSpeed, corridorSeconds;
            public string thrownAt;
            public readonly StringBuilder log = new StringBuilder();
        }

        /// <summary>Drives the escape the way the game does: the course autopilot (or a sprint-only pilot), the house folding
        /// shut behind her, the corridor's rule (a stretch moves her and the fold back a bay; a break-through opens its
        /// door), and no second chances: a fall or a catch throws her out.</summary>
        public static Run Drive(HouseEscape e, int fromCheckpoint, float untilS, float maxSeconds, int fps = 60, int jitterSeed = 0, bool slide = true, ArgumentOutcome outcome = ArgumentOutcome.Unsure)
        {
            var c = e.course;
            var jitter = jitterSeed != 0 ? new Random(jitterSeed) : null;
            var boxes = new List<BoxWorld.Box>();
            var world = WorldOf(e, boxes);
            world.skin = 0.04f;
            var sim = new MoveSim(world, new MoveDef());
            var driver = new MoveDriver(sim);
            var pilot = new CourseAutopilot(c);
            var r = new Run();
            var cp = c.checkpoints[fromCheckpoint];
            driver.Teleport(cp.position, cp.yaw, new Vector3(0f, 0f, sim.def.sprintSpeed));
            pilot.Reset(cp.s);
            e.corridor.Reset();
            e.corridor.ApplyOutcome(outcome);
            var pursuer = new HouseFold { targetGap = c.pursuer.targetGap, k = c.pursuer.k, minSpeed = c.pursuer.minSpeed, maxSpeed = c.pursuer.maxSpeed };
            bool chasing = false;
            float prevS = c.route.Project(sim.Position);
            float dt = 1f / fps, baseDt = dt, inCorridor = 0f;
            string lastMode = "";
            for (float t = 0f; t < maxSeconds; t += dt)
            {
                if (jitter != null) dt = jitter.NextDouble() < 0.1 ? 0.05f : (float)(0.0025 + jitter.NextDouble() * 0.02);
                else dt = baseDt;
                var input = pilot.Next(sim);
                // The sprint-only run slides under the doors it must, but never into the corridor's end.
                if (!slide && c.route.Project(sim.Position) > e.corridor.triggerS - 8f) { input.crouchHeld = false; input.crouchPressed = false; }
                driver.Frame(dt, input);
                var p = sim.Position;
                float s = c.route.Project(p);
                r.topSpeed = Math.Max(r.topSpeed, sim.HorizontalSpeed);
                if (s > e.corridor.startS && s < e.corridor.doorS + 1f) inCorridor += dt;
                string mode = sim.Mode.ToString();
                if (mode != lastMode) { r.log.AppendLine($"{t,6:0.00}s z {p.Z,7:0.0} x {p.X,5:0.0} y {p.Y,6:0.00} {mode,-8} speed {sim.HorizontalSpeed,5:0.0}"); lastMode = mode; }
                // The corridor.
                switch (e.corridor.Check(prevS, s, sim.Velocity.Z))
                {
                    case StretchCorridor.Result.Stretch:
                        r.stretches++;
                        r.log.AppendLine($"{t,6:0.00}s STRETCH {r.stretches} at speed {sim.Velocity.Z:0.0}");
                        driver.Teleport(p + e.corridor.Shift, 0f, sim.Velocity);
                        s = c.route.Project(sim.Position);
                        pilot.Reset(s);
                        pursuer.Shift(-e.corridor.bay);
                        pursuer.targetGap = e.corridor.FoldGapAfterStretches(c.pursuer.targetGap);
                        break;
                    case StretchCorridor.Result.BreakThrough:
                        r.breakThrough++;
                        r.log.AppendLine($"{t,6:0.00}s BREAK THROUGH at speed {sim.Velocity.Z:0.0}");
                        boxes[e.corridorDoor].enabled = false;
                        pursuer.targetGap = c.pursuer.targetGap;
                        break;
                }
                prevS = s;
                if (!chasing && s >= c.chaseStartS && s < c.chaseEndS) { chasing = true; pursuer.Reset(s, sim.HorizontalSpeed); }
                if (chasing && s >= c.chaseEndS) chasing = false;
                bool caught = chasing && pursuer.Step(s, Vector3.Dot(sim.Velocity, c.route.DirectionAt(s)), dt);
                if (p.Y < c.killY || caught)
                {
                    r.thrownOut = true;
                    r.thrownAt = (caught ? "caught" : "fell") + $" at z {p.Z:0.0} ({c.SectionAt(s)})";
                    r.log.AppendLine($"{t,6:0.00}s THROWN OUT: {r.thrownAt}");
                    r.seconds = t;
                    break;
                }
                if (s >= untilS && sim.Grounded)
                {
                    r.finished = true;
                    r.seconds = t;
                    break;
                }
            }
            r.corridorSeconds = inCorridor;
            return r;
        }

        /// <summary>The course in the fake world; a ceiling that slopes is a thin slab (in steps), as the builder makes it,
        /// not the fake world's solid wedge.</summary>
        public static BoxWorld WorldOf(HouseEscape e, List<BoxWorld.Box> byPiece)
        {
            var w = new BoxWorld();
            for (int i = 0; i < e.course.pieces.Count; i++)
            {
                var p = e.course.pieces[i];
                BoxWorld.Box b = null;
                if (p.solid)
                {
                    if (p.kind == LabPieceKind.Ramp && e.looks[i] == HouseLook.EscCeiling)
                    {
                        const int n = 40;
                        for (int k = 0; k < n; k++)
                        {
                            float z0 = p.Min.Z + p.size.Z * k / n, z1 = p.Min.Z + p.size.Z * (k + 1) / n;
                            float top = p.rampTopStart + (p.rampTopEnd - p.rampTopStart) * (k + 0.5f) / n;
                            var s = w.Add(new Vector3(p.center.X, top - 0.15f, (z0 + z1) * 0.5f), new Vector3(p.size.X, 0.3f, z1 - z0), false);
                            if (b == null) b = s;
                        }
                    }
                    else b = p.kind == LabPieceKind.Ramp
                        ? w.Ramp(p.Min.X, p.Max.X, p.Min.Z, p.Max.Z, p.rampTopStart, p.rampTopEnd, p.Min.Y)
                        : w.Add(p.center, p.size, p.runnable);
                }
                byPiece?.Add(b);
            }
            return w;
        }

        [TestCase(20, 0)]
        [TestCase(60, 0)]
        [TestCase(144, 0)]
        [TestCase(300, 0)]
        [TestCase(0, 7)]
        [TestCase(0, 11)]
        public void TheAutopilotEscapesStretchOnceAndBreaksThrough(int fps, int jitter)
        {
            var e = HouseEscape.Build();
            var r = Drive(e, e.startCheckpoint, e.rain.rainEndS, 240f, fps == 0 ? 60 : fps, jitter);
            if (!r.finished) TestContext.Out.WriteLine(r.log.ToString());
            TestContext.Out.WriteLine($"{fps} fps: escape {r.seconds:0.0} s (the corridor {r.corridorSeconds:0.0} s), top speed {r.topSpeed:0.0} m/s, stretched {r.stretches}");
            Assert.That(r.finished, Is.True, fps + " fps: " + r.thrownAt);
            Assert.That(r.thrownOut, Is.False);
            Assert.That(r.stretches, Is.InRange(1, 2), "the reveal, then a slide gets through");
            Assert.That(r.breakThrough, Is.EqualTo(1));
            // The creator: "unexpectedly long for how small the house looks": the autopilot's near-perfect run over a
            // minute, so two to three minutes for a player.
            Assert.That(r.seconds, Is.InRange(60f, 120f), "the escape's length (autopilot)");
        }

        [Test]
        public void ASprintAloneNeverGetsThroughTheCorridorAndTheHouseThrowsHerOut()
        {
            var e = HouseEscape.Build();
            var r = Drive(e, e.corridorCheckpoint, e.rain.rainEndS, 200f, 60, 0, slide: false, outcome: ArgumentOutcome.Condemns);
            TestContext.Out.WriteLine(r.log.ToString());
            Assert.That(r.finished, Is.False);
            Assert.That(r.stretches, Is.GreaterThanOrEqualTo(3), "it kept getting longer");
            Assert.That(r.thrownOut, Is.True, "the house closed on her");
        }

        [Test]
        public void LenaUnsureTheoHoldsTheDoor()
        {
            var e = HouseEscape.Build();
            e.corridor.Reset();
            e.corridor.ApplyOutcome(ArgumentOutcome.Unsure);
            float trig = e.corridor.triggerS;
            int stretched = 0;
            for (int k = 0; k < 6 && !e.corridor.Open; k++)
                if (e.corridor.Check(trig - 1f, trig + 0.1f, 9.5f) == StretchCorridor.Result.Stretch) stretched++;
            Assert.That(e.corridor.Open, Is.True, "Theo holds it: a sprint gets through after a few tries");
            Assert.That(stretched, Is.EqualTo(3));
        }

        [Test]
        public void EveryCheckpointRunsToTheNext()
        {
            var e = HouseEscape.Build();
            var c = e.course;
            for (int i = e.startCheckpoint; i < c.checkpoints.Count; i++)
            {
                float until = i + 1 < c.checkpoints.Count ? c.checkpoints[i + 1].s + 1f : e.rain.rainEndS;
                var r = Drive(e, i, until, 60f);
                if (!r.finished) TestContext.Out.WriteLine(r.log.ToString());
                Assert.That(r.finished, Is.True, c.checkpoints[i].name + ": " + r.thrownAt);
            }
        }

        [Test]
        public void NoTwoSolidsOverlap()
        {
            var e = HouseEscape.Build();
            var parts = new List<HousePart>();
            for (int i = 0; i < e.course.pieces.Count; i++) parts.Add(new HousePart { piece = e.course.pieces[i], look = e.looks[i] });
            var hits = HouseLint.Check(parts);
            foreach (var h in hits.Take(30)) TestContext.Out.WriteLine(HouseLint.Describe(parts, h));
            Assert.That(hits, Is.Empty);
        }

        [Test]
        public void EveryClosedRoomHasWallsBothSidesAllRunnableAndACeiling()
        {
            var e = HouseEscape.Build();
            foreach (var room in e.rooms)
            {
                if (room.open) continue;
                var walls = e.course.pieces.Where(p => p.kind == LabPieceKind.Wall && p.runnable && p.Min.Z <= room.s0 + 0.31f && p.Max.Z >= room.s1 - 0.01f).ToList();
                Assert.That(walls.Any(p => p.Max.X <= -room.halfWidth + 0.01f), Is.True, room.name + ": a runnable wall on the left the whole way");
                Assert.That(walls.Any(p => p.Min.X >= room.halfWidth - 0.01f), Is.True, room.name + ": a runnable wall on the right the whole way");
                Assert.That(e.course.pieces.Any(p => p.name.StartsWith(room.name) && p.name.Contains("ceiling")), Is.True, room.name + ": a ceiling");
            }
        }

        [Test]
        public void TheRouteFitsTheMovementsReach()
        {
            var e = HouseEscape.Build();
            var problems = CourseReach.Check(e.reach, new MoveDef(), 0.04f);
            foreach (var p in problems) TestContext.Out.WriteLine(p);
            Assert.That(problems, Is.Empty);
        }
    }
}
