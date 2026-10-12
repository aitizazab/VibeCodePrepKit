using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using JamKit.DeepBlock;
using JamKit.Movement;
using JamKit.Tests.Movement;
using NUnit.Framework;

namespace JamKit.Tests.DeepBlock
{
    /// <summary>Night 1's street on a hill (docs/LARCH-HILL.md): every beat of the way in and the four routes fits the
    /// movement's reach; the course autopilot runs the way in (the creek's double jump, the garage door's slide) to Walt and
    /// each route from the foot square to the top, through the same boxes the Unity builder makes; Walt's gate shuts the
    /// way up until his argument; nothing to walk to sits inside a solid; the tower is seen from the bottom, the route starts
    /// and most of every route; the top sees the bottom; the story's deductions, phases and the neighbours' loop.</summary>
    public class LarchHillTests
    {
        public static BoxWorld WorldOf(LarchHill h, bool waltAside = true, float skin = 0.04f)
        {
            var open = LarchHill.Gates(waltAside);
            var w = new BoxWorld { skin = skin };
            foreach (var p in h.pieces)
            {
                if (!p.solid || !p.PresentOn(open)) continue;
                BoxWorld.Box box;
                if (p.rampAxis == 1) box = w.RampX(p.Min.X, p.Max.X, p.Min.Z, p.Max.Z, p.rampLow, p.rampHigh, p.Min.Y);
                else if (p.rampAxis == 2) box = w.Ramp(p.Min.X, p.Max.X, p.Min.Z, p.Max.Z, p.rampLow, p.rampHigh, p.Min.Y);
                else box = w.Add(p.center, p.size, p.runnable);
                box.runnable = p.runnable;
            }
            return w;
        }

        public sealed class Run
        {
            public bool finished;
            public float seconds, topSpeed;
            public string failure = "";
            public readonly StringBuilder log = new StringBuilder();
            public readonly List<Vector3> path = new List<Vector3>();
        }

        /// <summary>The autopilot from a route's start until she stands at its end at or above <paramref name="finishY"/>.</summary>
        public static Run Drive(LarchHill h, BlockRoute r, float finishY, int fps = 60, float maxSeconds = 120f, bool waltAside = true)
        {
            var sim = new MoveSim(WorldOf(h, waltAside), new MoveDef());
            var driver = new MoveDriver(sim);
            var pilot = new CourseAutopilot(r.course);
            driver.Teleport(r.start, r.startYaw, Vector3.Zero);
            pilot.Reset(0f);
            var run = new Run();
            string lastMode = "";
            float dt = 1f / fps, lastS = 0f, stuckFor = 0f, sample = 0f;
            for (float t = 0f; t < maxSeconds; t += dt)
            {
                driver.Frame(dt, pilot.Next(sim));
                var p = sim.Position;
                float s = r.course.route.Project(p, out float lateral);
                run.topSpeed = Math.Max(run.topSpeed, sim.HorizontalSpeed);
                if ((sample -= dt) <= 0f) { sample = 0.5f; run.path.Add(p); }
                string mode = sim.Mode.ToString();
                if (mode != lastMode)
                {
                    run.log.AppendLine($"{t,6:0.00}s s {s,6:0.0} x {p.X,6:0.0} z {p.Z,6:0.0} y {p.Y,6:0.00} lat {lateral,5:0.0} {mode,-8} speed {sim.HorizontalSpeed,5:0.0}");
                    lastMode = mode;
                }
                if (p.Y < r.course.killY) { run.failure = $"fell at s {s:0.0} (x {p.X:0.0}, z {p.Z:0.0})"; return run; }
                if (s >= r.finishS - 0.5f && p.Y > finishY - 0.15f && sim.Grounded) { run.finished = true; run.seconds = t; return run; }
                stuckFor = s > lastS + 0.5f ? 0f : stuckFor + dt;
                if (s > lastS + 0.5f) lastS = s;
                if (stuckFor > 4f) { run.failure = $"stuck at s {s:0.0} (x {p.X:0.0}, z {p.Z:0.0}, y {p.Y:0.0}) in {mode}"; return run; }
            }
            run.failure = "out of time";
            return run;
        }

        static IEnumerable<BlockRoute> AllRoutes(LarchHill h)
        {
            yield return h.wayIn;
            foreach (var r in h.routes) yield return r;
        }

        // ---------- The routes ----------

        [Test]
        public void EveryBeatFitsTheMovementsReach()
        {
            var h = LarchHill.Build();
            Assert.That(h.routes.Count, Is.EqualTo(4), "the road, the garden stairs, the rooftops, the creek ravine");
            var def = new MoveDef();
            foreach (var r in AllRoutes(h))
            {
                var problems = CourseReach.Check(r.beats, def, 0.04f, 0f);
                Assert.That(problems, Is.Empty, r.name + ": " + string.Join("; ", problems));
            }
        }

        [Test]
        public void TheWayInCrossesTheCreekAndTheShopToWalt()
        {
            var h = LarchHill.Build();
            var run = Drive(h, h.wayIn, 0f, waltAside: false);
            TestContext.Out.WriteLine(run.log.ToString());
            TestContext.Out.WriteLine($"the way in: {(run.finished ? $"{run.seconds:0.0} s" : run.failure)}, {h.wayIn.course.route.Length:0} m");
            Assert.That(run.finished, Is.True, run.failure);
            Assert.That(run.log.ToString(), Does.Contain("Slide"), "under the garage door: a slide");
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void TheAutopilotClimbsTheRouteToTheTop(int index)
        {
            var h = LarchHill.Build();
            var r = h.routes[index];
            var run = Drive(h, r, LarchHill.Top);
            TestContext.Out.WriteLine(run.log.ToString());
            TestContext.Out.WriteLine($"{r.name}: {(run.finished ? $"{run.seconds:0.0} s" : run.failure)}, {r.course.route.Length:0} m, top speed {run.topSpeed:0.0} m/s");
            Assert.That(run.finished, Is.True, r.name + ": " + run.failure);
        }

        [TestCase(20)]
        [TestCase(144)]
        public void TheRoutesHoldAtOtherFrameRates(int fps)
        {
            var h = LarchHill.Build();
            var wayIn = Drive(h, h.wayIn, 0f, fps, waltAside: false);
            Assert.That(wayIn.finished, Is.True, $"the way in at {fps} fps: {wayIn.failure}");
            foreach (var r in h.routes)
            {
                var run = Drive(h, r, LarchHill.Top, fps);
                if (!run.finished) TestContext.Out.WriteLine(run.log.ToString());
                Assert.That(run.finished, Is.True, $"{r.name} at {fps} fps: {run.failure}");
            }
        }

        [Test]
        public void WaltsGateShutsTheWayUpUntilHisArgument()
        {
            var h = LarchHill.Build();
            var c = new LabCourse();
            c.route = new PathLine(new List<Vector3> { new Vector3(-52.5f, 0f, 19f), new Vector3(-52.5f, 0f, 30f) });
            c.lane.Add(new Vector2(0f, 0f));
            c.killY = -6f;
            var r = new BlockRoute { name = "through the gate", course = c, start = new Vector3(-52.5f, 0.02f, 19f), startYaw = 0f, finishS = 10.5f };
            c.finishS = r.finishS;
            Assert.That(Drive(h, r, 0f, waltAside: false).finished, Is.False, "shut: the gate stops her");
            Assert.That(Drive(h, r, 0f, waltAside: true).finished, Is.True, "open once Walt steps aside");
        }

        [Test]
        public void NothingToWalkToSitsInsideASolid()
        {
            var h = LarchHill.Build();
            var open = LarchHill.Gates(true);
            string Inside(Vector3 p)
            {
                foreach (var b in h.pieces)
                {
                    if (!b.solid || !b.PresentOn(open)) continue;
                    if (p.X > b.Min.X + 0.05f && p.X < b.Max.X - 0.05f && p.Z > b.Min.Z + 0.05f && p.Z < b.Max.Z - 0.05f && p.Y > b.Min.Y && p.Y < b.TopAt(p.X, p.Z) - 0.05f)
                        return b.name;
                }
                return null;
            }
            Assert.That(Inside(h.arrival + new Vector3(0f, 0.5f, 0f)), Is.Null, "the arrival");
            foreach (var r in AllRoutes(h)) Assert.That(Inside(r.start + new Vector3(0f, 0.5f, 0f)), Is.Null, r.name + " start");
            foreach (var s in h.spots)
            {
                if (s.id == LarchHill.SpotElias || s.id == LarchHill.SpotWindow || s.id == LarchHill.SpotDoor || s.id == LarchHill.SpotPorchLight) continue;   // in the house's face
                if (s.size.X > 0f) continue;   // trigger boxes
                Assert.That(Inside(s.position + new Vector3(0f, 0.3f, 0f)), Is.Null, s.name);
            }
        }

        // ---------- The views ----------

        static float SeenShare(LarchHill h, IList<Vector3> feet, Vector3 target, List<Vector3> hidden = null)
        {
            int seen = 0;
            foreach (var f in feet)
            {
                if (h.SeesLandmark(f + new Vector3(0f, 1.5f, 0f))) seen++;
                else hidden?.Add(f);
            }
            return feet.Count == 0 ? 0f : seen / (float)feet.Count;
        }

        [Test]
        public void TheTowerIsSeenFromTheBottomTheStartsAndMostOfEveryRoute()
        {
            var h = LarchHill.Build();
            Assert.That(h.Blocker(h.arrival + new Vector3(0f, 1.5f, 0f), h.towerPoint), Is.Null, "from the arrival: the whole climb, the tower at the top");
            var vista = h.views.Find(v => v.name.Contains("vista")).eye;
            Assert.That(h.Blocker(vista, h.towerPoint), Is.Null, "from the lookout, turning round");
            var report = new StringBuilder();
            foreach (var r in AllRoutes(h))
            {
                var run = Drive(h, r, r == h.wayIn ? 0f : LarchHill.Top, waltAside: r != h.wayIn ? true : false);
                var hidden = new List<Vector3>();
                float share = SeenShare(h, run.path, h.towerPoint, hidden);
                int tower = 0;
                foreach (var f in run.path) if (h.LineClear(f + new Vector3(0f, 1.5f, 0f), h.towerPoint)) tower++;
                report.AppendLine($"{r.name}: the landmark seen from {share:P0} of the way, the tower itself from {tower / (float)Math.Max(1, run.path.Count):P0} ({run.path.Count} points)");
                if (hidden.Count > 0) report.AppendLine("  hidden at " + string.Join(", ", hidden.ConvertAll(p => $"({p.X:0},{p.Y:0},{p.Z:0})").GetRange(0, Math.Min(8, hidden.Count))));
                float need = r == h.wayIn ? 0.35f : 0.7f;   // the way in goes through the shop
                Assert.That(share, Is.GreaterThanOrEqualTo(need), r.name + " (" + report + ")");
            }
            TestContext.Out.WriteLine(report.ToString());
        }

        [Test]
        public void TheTopSeesTheWholeStreet()
        {
            var h = LarchHill.Build();
            var vista = h.views.Find(v => v.name.Contains("vista")).eye;
            Assert.That(h.Blocker(vista, h.arrival + new Vector3(0f, 1f, 0f)), Is.Null, "from the top: the arrival on the far bank");
            Assert.That(h.Blocker(vista, new Vector3(-53f, 5f, 9f)), Is.Null, "the shop's roof");
            Assert.That(h.Blocker(vista, new Vector3(9.5f, LarchHill.HouseTop + 0.1f, 13f)), Is.Null, "the roofs of the houses at the foot");
            foreach (var v in h.views) Assert.That(h.Solid(v.eye), Is.False, v.name + "'s camera is in the open");
        }

        // ---------- The story ----------

        [Test]
        public void TheDeductionsMakeThemselvesWhenBothHalvesAreKnown()
        {
            var s = new LarchStreetStory();
            var made = new List<string>();
            Assert.That(s.Learn(LarchStreetStory.MemWaltAlive, made), Is.True);
            Assert.That(made, Is.Empty, "Walt alive alone isn't the deduction");
            Assert.That(s.Learn(LarchStreetStory.FactEffects, made), Is.True, "the memorial card (Day 1)");
            Assert.That(made, Is.EqualTo(new[] { LarchStreetStory.DedWaltDead }));
            Assert.That(s.Learn(LarchStreetStory.FactEffects, made), Is.False, "once");
            made.Clear();
            s.Learn(LarchStreetStory.MemNeighboursLoop, made);
            s.Learn(LarchStreetStory.MemSaturdayPaper, made);
            Assert.That(made, Is.EqualTo(new[] { LarchStreetStory.DedSameSaturday }));
            Assert.That(LarchStreetStory.MemSaturdayPaper, Is.EqualTo("mem_saturday_paper"), "the id Night 2's journal looks for");
            Assert.That(LarchStreetStory.WaltOutcomeValue(WaltOutcome.HeardOut), Is.EqualTo("heardout"));
        }

        [Test]
        public void TheNightOnlyGoesForwardAndTheClimbWaitsForWalt()
        {
            var s = new LarchStreetStory();
            Assert.That(s.Advance(StreetPhase.Shop), Is.True);
            Assert.That(s.Advance(StreetPhase.Arrival), Is.False, "never back");
            Assert.That(s.Advance(StreetPhase.Arguing), Is.True);
            Assert.That(s.Advance(StreetPhase.Top), Is.False, "not up the hill before Walt steps aside");
            Assert.That(s.EndArgument(WaltOutcome.Pressed), Is.True);
            Assert.That(s.Phase, Is.EqualTo(StreetPhase.Climb));
            Assert.That(s.WaltAside, Is.True, "he steps aside whatever the outcome");
            Assert.That(s.EndArgument(WaltOutcome.Persuaded), Is.False, "once");
            Assert.That(s.Advance(StreetPhase.Top) && s.Advance(StreetPhase.Ending) && s.Advance(StreetPhase.Ended), Is.True);
        }

        [Test]
        public void TheNeighboursLoopIsTheSameEveryTimeAndNightTwoCanReplayIt()
        {
            var a = new NeighbourLoop(1);
            var b = new NeighbourLoop(1);
            for (float t = 0f; t < 100f; t += 0.37f)
            {
                Assert.That(b.SprinklerAngle(t), Is.EqualTo(a.SprinklerAngle(t)));
                Assert.That(b.BallFlight(t), Is.EqualTo(a.BallFlight(t)));
                Assert.That(b.DogOut(t), Is.EqualTo(a.DogOut(t)));
            }
            Assert.That(a.Phase(NeighbourLoop.Period * 3f + 2f), Is.EqualTo(a.Phase(2f)).Within(1e-3f), "it loops");
            Assert.That(NeighbourLoop.StartOffset(1), Is.EqualTo(NeighbourLoop.StartOffset(1)), "fixed per night");
            Assert.That(NeighbourLoop.StartOffset(2), Is.Not.EqualTo(NeighbourLoop.StartOffset(1)), "a different night starts elsewhere in it");
            // The line: once per loop, wherever the frames fall.
            int said = 0;
            float prev = 0f;
            for (float t = 0.016f; t <= NeighbourLoop.Period * 3f; t += 0.016f) { if (a.LineBetween(prev, t)) said++; prev = t; }
            Assert.That(said, Is.EqualTo(3));
            int coarse = 0;
            prev = 0f;
            for (float t = 0.5f; t <= NeighbourLoop.Period * 3f; t += 0.5f) { if (a.LineBetween(prev, t)) coarse++; prev = t; }
            Assert.That(coarse, Is.EqualTo(3), "at any frame rate");
            float min = 999f, max = -999f;
            for (float t = 0f; t < NeighbourLoop.Period; t += 0.05f) { min = Math.Min(min, a.SprinklerAngle(t)); max = Math.Max(max, a.SprinklerAngle(t)); }
            Assert.That(max - min, Is.EqualTo(NeighbourLoop.SprinklerSweep).Within(3f));
        }
    }
}
