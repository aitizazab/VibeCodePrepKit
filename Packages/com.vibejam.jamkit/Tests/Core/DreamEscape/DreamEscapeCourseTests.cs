using System;
using System.Collections.Generic;
using System.Numerics;
using JamKit.DreamEscape;
using JamKit.Movement;
using JamKit.Tests.Movement;
using NUnit.Framework;

namespace JamKit.Tests.DreamEscape
{
    /// <summary>Visiting Hours' first escape (the dream street) in the fake world: the Movement Lab's autopilot
    /// runs it start to finish without falling or being caught, at any frame rate; every checkpoint reaches the
    /// next; the route's gaps, walls, hops and climbs fit the movement's measured reach; checkpoints are ordered
    /// and carry the presenter chapters; the two collapses go behind or beside the player; nothing decorative
    /// stands on the route; standing still gets you caught.</summary>
    public class DreamEscapeCourseTests
    {
        static DreamEscapeCourse Build() => DreamEscapeCourse.Build();

        [Test]
        public void TheAutopilotEscapesWithoutFallingOrBeingCaught()
        {
            var d = Build();
            var r = LabCourseTests.Drive(d.course, 0, d.course.finishS, 150f);
            TestContext.Out.WriteLine(r.log.ToString());
            foreach (var kv in r.sectionAverage) TestContext.Out.WriteLine($"{kv.Key}: average {kv.Value:0.0} m/s");
            TestContext.Out.WriteLine($"finished {r.finished} in {r.seconds:0.0} s, top speed {r.topSpeed:0.0} m/s, falls {r.falls}, caught {r.caught}");
            Assert.That(r.finished, Is.True, "reached the light");
            Assert.That(r.falls, Is.EqualTo(0), "no falls");
            Assert.That(r.caught, Is.EqualTo(0), "the static never caught a good run");
            Assert.That(r.seconds, Is.InRange(38f, 90f), "a short chase: about a minute for a player");
            Assert.That(r.topSpeed, Is.GreaterThan(12f), "the slides and wall kicks go well past sprint");
        }

        [TestCase(20, 0)]
        [TestCase(30, 0)]
        [TestCase(144, 0)]
        [TestCase(300, 0)]
        [TestCase(0, 7)]
        [TestCase(0, 11)]
        public void TheEscapeFinishesAtAnyFrameRate(int fps, int jitterSeed)
        {
            var d = Build();
            // With a CharacterController's 4 cm skin, as in Unity.
            var r = LabCourseTests.Drive(d.course, 0, d.course.finishS, 150f, fps == 0 ? 60 : fps, true, jitterSeed, 0.04f);
            TestContext.Out.WriteLine(r.log.ToString());
            Assert.That(r.finished, Is.True, fps + " fps, jitter " + jitterSeed);
            Assert.That(r.falls, Is.EqualTo(0), fps + " fps, jitter " + jitterSeed);
            Assert.That(r.caught, Is.EqualTo(0), fps + " fps, jitter " + jitterSeed);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void EveryCheckpointRunsToTheNext(int index)
        {
            var d = Build();
            var c = d.course;
            float until = index + 1 < c.checkpoints.Count ? c.checkpoints[index + 1].s + 1f : c.finishS;
            var r = LabCourseTests.Drive(c, index, until, 60f);
            TestContext.Out.WriteLine(r.log.ToString());
            Assert.That(r.finished, Is.True, c.checkpoints[index].name);
            Assert.That(r.falls + r.caught, Is.EqualTo(0), c.checkpoints[index].name);
        }

        [Test]
        public void TheRouteFitsTheMovementsMeasuredReach()
        {
            var d = Build();
            var def = new MoveDef();
            var problems = d.CheckReach(def);
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
            float single = DreamEscapeCourse.SingleJumpReach(def, def.sprintSpeed);
            float dbl = DreamEscapeCourse.DoubleJumpReach(def, def.sprintSpeed);
            TestContext.Out.WriteLine($"single jump {single:0.00} m, double {dbl:0.00} m at sprint {def.sprintSpeed} m/s");
            Assert.That(single, Is.InRange(5.5f, 6.5f), "about 6 m (RESEARCH-MOVEMENT 4.3)");
            Assert.That(dbl, Is.InRange(9f, 11f), "about 10 m");
            var kinds = new HashSet<CourseBeatKind>();
            foreach (var b in d.beats) kinds.Add(b.kind);
            foreach (var k in new[] { CourseBeatKind.FenceHop, CourseBeatKind.CarHop, CourseBeatKind.Wallrun, CourseBeatKind.Slide, CourseBeatKind.Mantle, CourseBeatKind.Gap, CourseBeatKind.DoubleGap, CourseBeatKind.WallToWall, CourseBeatKind.SlideRamp })
                Assert.That(kinds, Does.Contain(k), "the escape uses " + k);
            for (int i = 1; i < d.beats.Count; i++)
                Assert.That(d.beats[i].s, Is.GreaterThanOrEqualTo(d.beats[i - 1].s), d.beats[i].name + " comes after " + d.beats[i - 1].name);
        }

        [Test]
        public void TheGarageDoorClearsTheSlidingCapsuleOnTheRealController()
        {
            var d = Build();
            var def = new MoveDef();
            LabPiece P(string name) => d.course.pieces.Find(p => p.name == name);
            float gap = P("Garage door").Min.Y - P("Street D").Max.Y;
            // The Unity controller: 4 cm skin, no step offset while crouched (MovementController defaults).
            float need = DreamEscapeCourse.SlideGapNeeded(def, 0.04f, 0f);
            TestContext.Out.WriteLine($"gap {gap:0.00} m, the sliding capsule needs {need:0.00} m");
            Assert.That(gap, Is.GreaterThanOrEqualTo(need));
            Assert.That(gap, Is.LessThan(def.standHeight), "standing, you can't pass");
            // With the 0.35 m step offset the controller used to keep while sliding, its step-up pass lifted the capsule
            // into the door's face and the slide stopped dead (Unity PlayMode, 2026-10-10): the check must catch that.
            var withStep = d.CheckReach(def, 0.04f, 0.35f);
            Assert.That(withStep.Exists(s => s.StartsWith("Under the garage door", StringComparison.Ordinal)), Is.True, string.Join("\n", withStep));
        }

        [Test]
        public void TheGapsBetweenSurfacesAreWhatTheBeatsSay()
        {
            var d = Build();
            LabPiece P(string name) => d.course.pieces.Find(p => p.name == name);
            Assert.That(P("Roof 2").Min.Z - P("Roof 1").Max.Z, Is.EqualTo(5.5f).Within(0.01f));
            Assert.That(P("Roof 3").Min.Z - P("Roof 2").Max.Z, Is.EqualTo(8.5f).Within(0.01f));
            Assert.That(P("Car 2").Min.Z - P("Car 1").Max.Z, Is.EqualTo(3f).Within(0.01f));
            Assert.That(P("Car 3").Min.Z - P("Car 2").Max.Z, Is.EqualTo(3f).Within(0.01f));
            Assert.That(P("Car 1").Max.Y - P("Street B").Max.Y, Is.EqualTo(DreamEscapeCourse.CarRoof).Within(0.01f));
            Assert.That(P("The last wall").size.Z, Is.EqualTo(21f).Within(0.01f));
            Assert.That(P("Garage door").Min.Y - P("Street D").Max.Y, Is.EqualTo(1.15f).Within(0.01f), "slide under it");
            Assert.That(P("Street B").Max.Y, Is.LessThan(P("Street A").Max.Y), "the first wall jump lands down");
            Assert.That(P("Street D").Max.Y, Is.LessThan(P("Street C").Max.Y), "the garden wall's jump lands down");
            Assert.That(P("Roof 4").Max.Y, Is.LessThan(P("Roof 3").Max.Y), "the paper front's jump lands down");
            Assert.That(P("The lit porch").Max.Y, Is.LessThan(P("Garage roof (low)").Max.Y), "the last jump lands down into the light");
            Assert.That(P("Porch").Max.Y - P("Street D").Max.Y, Is.EqualTo(1f).Within(0.01f));
            Assert.That(P("Porch roof").Max.Y - P("Porch").Max.Y, Is.EqualTo(1.6f).Within(0.01f));
            Assert.That(P("Roof 1").Max.Y - P("Porch roof").Max.Y, Is.EqualTo(1.6f).Within(0.01f));
            foreach (var p in d.course.pieces)
                if (p.runnable) Assert.That(p.size.Z, Is.InRange(8f, 21.5f), p.name + ": a runnable wall the movement can use");
        }

        [Test]
        public void CheckpointsAreOrderedSpacedAndCarryTheChapters()
        {
            var d = Build();
            var cps = d.course.checkpoints;
            Assert.That(cps.Count, Is.InRange(3, 4), "3-4 checkpoints");
            for (int i = 1; i < cps.Count; i++)
            {
                Assert.That(cps[i].s, Is.GreaterThan(cps[i - 1].s), cps[i].name);
                float gap = cps[i].s - cps[i - 1].s;
                Assert.That(gap, Is.InRange(80f, 200f), cps[i].name + ": a checkpoint every 10-20 s of running");
            }
            Assert.That(d.course.finishS - cps[cps.Count - 1].s, Is.InRange(40f, 200f));
            Assert.That(d.course.CheckpointIndex(DreamEscapeCourse.ChapterStart), Is.EqualTo(0));
            Assert.That(d.course.CheckpointIndex(DreamEscapeCourse.ChapterRooftops), Is.GreaterThan(0));
            Assert.That(d.course.CheckpointIndex(DreamEscapeCourse.ChapterFinal), Is.EqualTo(cps.Count - 1));
            foreach (var cp in cps)
            {
                Assert.That(d.FloorTopAt(cp.position.X, cp.position.Z, cp.position.Y + 0.1f, out float top), Is.True, cp.name + " stands on something");
                Assert.That(top, Is.EqualTo(cp.position.Y).Within(0.05f), cp.name);
                Assert.That(Math.Abs(cp.position.X - d.course.LaneAt(cp.s)), Is.LessThan(0.01f), cp.name + " is on the lane");
                Assert.That(cp.s, Is.GreaterThanOrEqualTo(d.course.chaseStartS - 2f).And.LessThan(d.course.chaseEndS), cp.name + " is in the chase");
            }
        }

        [Test]
        public void TwoCollapsesGoBehindOrBesideThePlayer()
        {
            var d = Build();
            var c = d.course;
            Assert.That(c.collapses.Count, Is.EqualTo(2));
            Assert.That(d.waves.Count, Is.EqualTo(2));
            Assert.That(d.waves[0].style, Is.EqualTo(DreamCollapseStyle.FoldFlat), "a fence row folding");
            Assert.That(d.waves[1].style, Is.EqualTo(DreamCollapseStyle.Peel), "house fronts peeling away like paper");
            Assert.That(d.looks.Count, Is.EqualTo(c.pieces.Count));
            Assert.That(d.tints.Count, Is.EqualTo(c.pieces.Count));
            Assert.That(d.collapseStyles.Count, Is.EqualTo(c.pieces.Count));
            for (int k = 0; k < c.collapses.Count; k++)
            {
                int members = 0;
                for (int i = 0; i < c.pieces.Count; i++)
                {
                    var p = c.pieces[i];
                    if (p.collapse != k) continue;
                    members++;
                    if (p.solid) Assert.That(p.Max.Z, Is.LessThan(c.collapses[k].triggerS), p.name + ": a solid piece only collapses behind you");
                    else Assert.That(Math.Abs(p.center.X - c.LaneAt(p.center.Z)), Is.GreaterThan(4f), p.name + ": beside the route, not on it");
                    Assert.That(d.CollapseDelay(i), Is.InRange(0f, 6f), p.name);
                }
                Assert.That(members, Is.GreaterThan(3), c.collapses[k].name);
            }
        }

        [Test]
        public void NothingDecorativeStandsOnTheRoute()
        {
            var d = Build();
            var c = d.course;
            for (int i = 0; i < c.pieces.Count; i++)
            {
                var p = c.pieces[i];
                if (!DreamEscapeCourse.IsDecor(d.looks[i])) continue;
                for (float z = p.Min.Z; z <= p.Max.Z; z += 0.5f)
                {
                    if (z < -5f || z > c.finishS + 5f) continue;
                    float x = c.LaneAt(z);
                    float dx = x < p.Min.X ? p.Min.X - x : x > p.Max.X ? x - p.Max.X : 0f;
                    Assert.That(dx, Is.GreaterThan(1.2f), $"{p.name} is {dx:0.00} m from the lane at z {z:0.0}");
                }
            }
            foreach (var b in d.billboards)
            {
                float dx = Math.Abs(b.position.X - c.LaneAt(b.position.Z));
                Assert.That(dx, Is.GreaterThan(2f), b.name + " stands beside the route");
                Assert.That(d.FloorTopAt(b.position.X, b.position.Z, b.position.Y + 0.1f, out float top), Is.True, b.name + " stands on something");
                Assert.That(top, Is.EqualTo(b.position.Y).Within(0.05f), b.name);
            }
            int dogs = 0;
            foreach (var b in d.billboards) if (b.who == DreamCast.Dog) dogs++;
            Assert.That(dogs, Is.GreaterThanOrEqualTo(2), "the dog is always there first");
            Assert.That(d.billboards.Exists(b => b.who == DreamCast.Walt), Is.True);
        }

        [Test]
        public void StandingStillOnTheRoofsGetsYouCaught()
        {
            var d = Build();
            var c = d.course;
            var world = LabCourseTests.WorldOf(c, null);
            var sim = new MoveSim(world, new MoveDef());
            var cp = c.checkpoints[c.CheckpointIndex(DreamEscapeCourse.ChapterRooftops)];
            sim.Teleport(cp.position, 0f, Vector3.Zero);
            var p = new Pursuer { targetGap = c.pursuer.targetGap, k = c.pursuer.k, minSpeed = c.pursuer.minSpeed, maxSpeed = c.pursuer.maxSpeed };
            p.Reset(cp.s, sim.def.sprintSpeed);
            float t = 0f;
            bool caught = false;
            const float h = 1f / 60f;
            while (t < 10f && !caught)
            {
                sim.Step(new MoveInput(), h);
                caught = p.Step(c.route.Project(sim.Position), 0f, h);
                t += h;
            }
            Assert.That(caught, Is.True, "standing still: the static catches you");
            Assert.That(t, Is.InRange(1.2f, 4f), "not at once, but soon");
        }

        [Test]
        public void TheExitLightIsOnTheLastPorchPastTheFinish()
        {
            var d = Build();
            Assert.That(d.exitLight.Z, Is.GreaterThan(d.course.finishS));
            Assert.That(d.FloorTopAt(d.exitLight.X, d.exitLight.Z, d.exitLight.Y + 0.1f, out float top), Is.True);
            Assert.That(top, Is.EqualTo(d.exitLight.Y).Within(0.01f));
            Assert.That(d.course.SectionAt(0f), Is.EqualTo("Walt's driveway"));
            Assert.That(d.course.SectionAt(d.course.finishS), Is.EqualTo("The light"));
        }
    }
}
