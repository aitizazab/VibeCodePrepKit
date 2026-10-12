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
    /// <summary>
    /// Larch Hill, the one hill of Nights 1-3 (docs/redesign/world.md): the course autopilot runs every leg of Night 1's
    /// critical path (the way in, the back way, up the green, the garage roofs, the far end of the lane, the Rewind) through
    /// the same boxes the Unity builder makes, at 20, 60 and 144 fps; every beat fits the movement's reach; the Rewind's wave
    /// doesn't catch a clean run and does catch one that stops; the Old Mill's shutter is a real wait; the bands can't be
    /// skipped (nothing you can stand on near a band's edge reaches over it); the ground tiles never overlap; nothing to walk
    /// to sits inside a solid; the hospital is in sight from most of the night and the house from the arrival; the story.
    /// </summary>
    public class LarchHillTests
    {
        static LarchHill _hill;
        static LarchHill Hill => _hill ?? (_hill = LarchHill.Build());

        /// <summary>The hill as a movement world (only the pieces within <paramref name="near"/> m of
        /// <paramref name="route"/>, when given: the world has thousands of boxes).</summary>
        public static BoxWorld WorldOf(LarchHill h, bool waltAside = true, bool shutterOpen = true, BlockRoute route = null, float near = 30f, float skin = 0.04f)
        {
            var open = LarchHill.Gates(waltAside, shutterOpen);
            var w = new BoxWorld { skin = skin };
            foreach (var p in h.pieces)
            {
                if (!p.solid || !p.PresentOn(open)) continue;
                if (route != null && !NearRoute(p, route, near)) continue;
                BoxWorld.Box box;
                if (p.rampAxis == 1) box = w.RampX(p.Min.X, p.Max.X, p.Min.Z, p.Max.Z, p.rampLow, p.rampHigh, p.Min.Y);
                else if (p.rampAxis == 2) box = w.Ramp(p.Min.X, p.Max.X, p.Min.Z, p.Max.Z, p.rampLow, p.rampHigh, p.Min.Y);
                else box = w.Add(p.center, p.size, p.runnable);
                box.runnable = p.runnable;
            }
            return w;
        }

        static bool NearRoute(BlockPiece p, BlockRoute r, float near)
        {
            var line = r.course.route;
            int n = Math.Max(2, (int)(line.Length / 4f));
            for (int k = 0; k <= n; k++)
            {
                var q = line.PointAt(line.Length * k / n, 0f);
                float dx = Math.Max(0f, Math.Max(p.Min.X - q.X, q.X - p.Max.X));
                float dz = Math.Max(0f, Math.Max(p.Min.Z - q.Z, q.Z - p.Max.Z));
                if (dx * dx + dz * dz <= near * near) return true;
            }
            return false;
        }

        public sealed class Run
        {
            public bool finished;
            public float seconds, topSpeed;
            public string failure = "";
            public readonly StringBuilder log = new StringBuilder();
            public readonly List<Vector3> path = new List<Vector3>();
            /// <summary>Sampled with <see cref="path"/>: (s along the route, time).</summary>
            public readonly List<Vector2> sAtTime = new List<Vector2>();
        }

        /// <summary>The autopilot along a leg until she stands at its end at or above its finishing height.
        /// <paramref name="shutterOpenAt"/> (optional) works the shutter by the sim's clock; <paramref name="stopAt"/> makes
        /// her stand still for <paramref name="stopFor"/> s once she gets that far.</summary>
        public static Run Drive(LarchHill h, BlockRoute r, int fps = 60, float maxSeconds = 90f, bool waltAside = true,
            Func<float, bool> shutterOpenAt = null, float stopAt = -1f, float stopFor = 0f)
        {
            float finishY = h.finishY.TryGetValue(r, out var fy) ? fy : 0f;
            var world = WorldOf(h, waltAside, true, r);
            BoxWorld.Box shutter = null;
            if (shutterOpenAt != null)
            {
                var p = h.pieces.Find(x => x.switchIndex == LarchHill.ShutterSwitch);
                shutter = world.Add(p.center, p.size, false);
                shutter.enabled = false;
            }
            var sim = new MoveSim(world, new MoveDef());
            var driver = new MoveDriver(sim);
            var pilot = new CourseAutopilot(r.course);
            driver.Teleport(r.start, r.startYaw, Vector3.Zero);
            pilot.Reset(0f);
            var run = new Run();
            string lastMode = "";
            float dt = 1f / fps, lastS = 0f, stuckFor = 0f, sample = 0f, stopped = 0f;
            for (float t = 0f; t < maxSeconds; t += dt)
            {
                if (shutter != null) shutter.enabled = !shutterOpenAt(t);
                var p = sim.Position;
                float s = r.course.route.Project(p, out float lateral);
                MoveInput input;
                if (stopAt >= 0f && s >= stopAt && stopped < stopFor) { stopped += dt; input = new MoveInput { yaw = sim.Yaw }; }
                else input = pilot.Next(sim);
                driver.Frame(dt, input);
                p = sim.Position;
                s = r.course.route.Project(p, out lateral);
                run.topSpeed = Math.Max(run.topSpeed, sim.HorizontalSpeed);
                if ((sample -= dt) <= 0f) { sample = 0.25f; run.path.Add(p); run.sAtTime.Add(new Vector2(s, t)); }
                string mode = sim.Mode.ToString();
                if (mode != lastMode)
                {
                    run.log.AppendLine($"{t,6:0.00}s s {s,6:0.0} x {p.X,7:0.0} z {p.Z,6:0.0} y {p.Y,6:0.00} lat {lateral,5:0.0} {mode,-8} speed {sim.HorizontalSpeed,5:0.0}");
                    lastMode = mode;
                }
                if (p.Y < r.course.killY) { run.failure = $"fell out of the world at s {s:0.0} (x {p.X:0.0}, z {p.Z:0.0})"; return run; }
                if (s >= r.finishS - 0.6f && p.Y > finishY - 0.2f && sim.Grounded) { run.finished = true; run.seconds = t; return run; }
                stuckFor = s > lastS + 0.5f ? 0f : stuckFor + dt;
                if (s > lastS + 0.5f) lastS = s;
                if (stuckFor > 5f + stopFor) { run.failure = $"stuck at s {s:0.0} (x {p.X:0.0}, z {p.Z:0.0}, y {p.Y:0.0}) in {mode}"; return run; }
            }
            run.failure = "out of time";
            return run;
        }

        static IEnumerable<BlockRoute> Night1Legs(LarchHill h)
        {
            yield return h.wayIn;
            foreach (var r in h.night1) yield return r;
        }

        // ---------- The legs ----------

        [Test]
        public void EveryBeatFitsTheMovementsReach()
        {
            var h = Hill;
            var def = new MoveDef();
            foreach (var r in Night1Legs(h))
            {
                var problems = CourseReach.Check(r.beats, def, 0.04f, 0f);
                Assert.That(problems, Is.Empty, r.name + ": " + string.Join("; ", problems));
            }
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void TheAutopilotRunsTheLeg(int index)
        {
            var h = Hill;
            var r = index == 0 ? h.wayIn : h.night1[index - 1];
            var run = Drive(h, r, waltAside: index != 0);
            TestContext.Out.WriteLine(run.log.ToString());
            TestContext.Out.WriteLine($"{r.name}: {(run.finished ? $"{run.seconds:0.0} s" : run.failure)}, {r.course.route.Length:0} m, top speed {run.topSpeed:0.0} m/s");
            Assert.That(run.finished, Is.True, r.name + ": " + run.failure);
        }

        [TestCase(20)]
        [TestCase(144)]
        public void TheLegsHoldAtOtherFrameRates(int fps)
        {
            var h = Hill;
            int i = 0;
            foreach (var r in Night1Legs(h))
            {
                var run = Drive(h, r, fps, waltAside: i++ != 0);
                if (!run.finished) TestContext.Out.WriteLine(run.log.ToString());
                Assert.That(run.finished, Is.True, $"{r.name} at {fps} fps: {run.failure}");
            }
        }

        [Test]
        public void TheWayInNeedsTheDoubleJumpAndTheSlide()
        {
            var h = Hill;
            var run = Drive(h, h.wayIn, waltAside: false);
            Assert.That(run.finished, Is.True, run.failure);
            Assert.That(run.log.ToString(), Does.Contain("Slide"), "under the garage door: a slide");
            var c = h.wayIn.course;
            var saved = new List<LabCue>(c.cues);
            c.cues.RemoveAll(q => q.kind == LabCueKind.DoubleJumpAtApex);
            try
            {
                var fall = Drive(h, h.wayIn, waltAside: false, maxSeconds: 20f);
                Assert.That(fall.finished, Is.False, "a single jump doesn't clear the creek");
            }
            finally { c.cues.Clear(); c.cues.AddRange(saved); }
        }

        // ---------- The Rewind ----------

        /// <summary>The wave's front (X) at <paramref name="t"/> s after she sets foot on Rowan Street.</summary>
        static float WaveX(float t, float speed) => LarchHill.RewindFromX + Math.Max(0f, t - LarchHill.RewindDelay) * speed;

        [Test]
        public void TheRewindDoesntCatchACleanRunAndCatchesAStop()
        {
            var h = Hill;
            var r = h.night1.Find(x => x.chapter == LarchHill.ChapterRewind);
            var run = Drive(h, r);
            Assert.That(run.finished, Is.True, run.failure);
            float worst = float.MaxValue;
            for (int i = 0; i < run.path.Count; i++)
            {
                float t = run.sAtTime[i].Y, x = run.path[i].X;
                if (x >= LarchHill.RewindSafeX) break;
                worst = Math.Min(worst, x - WaveX(t, LarchHill.RewindSpeed));
            }
            float margin = (LarchHill.RewindSafeX - LarchHill.RewindFromX) / LarchHill.RewindSpeed + LarchHill.RewindDelay - run.seconds;
            TestContext.Out.WriteLine($"the Rewind: {run.seconds:0.0} s; the wave would take {run.seconds + margin:0.0} s; closest {worst:0.0} m");
            Assert.That(worst, Is.GreaterThan(4f), "a clean run stays well ahead of the wave");
            Assert.That(margin, Is.InRange(3f, 12f), "but not by so much that the wave doesn't matter");
            var slow = Drive(h, r, stopAt: r.course.route.Project(new Vector3(LarchHill.BalesX - 3f, 0f, 103.4f)), stopFor: 6f);
            bool caught = false;
            for (int i = 0; i < slow.path.Count; i++)
                if (slow.path[i].X < LarchHill.RewindSafeX && slow.path[i].X <= WaveX(slow.sAtTime[i].Y, LarchHill.RewindSpeed)) { caught = true; break; }
            Assert.That(caught, Is.True, "standing still for 6 s, the wave reaches her");
        }

        [Test]
        public void TheShutterIsARealWait()
        {
            var h = Hill;
            var r = h.night1.Find(x => x.name == "The far end of the lane");
            var shut = Drive(h, r, shutterOpenAt: t => false, maxSeconds: 30f);
            Assert.That(shut.finished, Is.False, "the shutter down blocks the lane");
            var timed = Drive(h, r, shutterOpenAt: t => LarchHill.ShutterGap(t + 7.2f) > 1.2f, maxSeconds: 60f);
            Assert.That(timed.finished, Is.True, "on the timer the arch opens: " + timed.failure);
            Assert.That(LarchHill.ShutterGap(0f), Is.EqualTo(LarchHill.ShutterOpening));
            Assert.That(LarchHill.ShutterGap(LarchHill.ShutterOpenFor + LarchHill.ShutterMoveFor + 1f), Is.EqualTo(0f));
        }

        // ---------- The bands can't be skipped ----------

        sealed class Edge
        {
            public string name;
            public float x0, x1, z, top, floor;
        }

        /// <summary>Within 10.5 m of a band's edge (below it), nothing you can stand on is high enough to get over the edge
        /// with a jump, a double jump and a mantle (4.15 m). The designed ways up (the slot, the oak) come through gaps.</summary>
        [Test]
        public void NothingNearABandsEdgeReachesOverIt()
        {
            var h = Hill;
            const float reachUp = 1.25f + 1.0f + 1.9f, reachAcross = 10.5f;
            var edges = new List<Edge>
            {
                new Edge { name = "Mill Street's fence (over the yards)", x0 = LarchHill.SlotX0, x1 = LarchHill.WaltX1, z = LarchHill.MillWallZ0, top = LarchHill.MillY + 0.15f + LarchHill.MillFence, floor = LarchHill.MillY },
                new Edge { name = "Rowan Street's hedge (over the lane)", x0 = -109f, x1 = 0f, z = LarchHill.LaneZ1, top = LarchHill.RowanY + 0.15f + LarchHill.RowanHedge, floor = LarchHill.RowanY },
                new Edge { name = "Rowan Street's railing (over the gardens)", x0 = 0f, x1 = LarchHill.HillWalkX0, z = LarchHill.LaneZ1, top = LarchHill.RowanY + 0.15f + 1.1f, floor = LarchHill.RowanY },
            };
            var report = new StringBuilder();
            foreach (var e in edges)
                foreach (var p in h.pieces)
                {
                    if (!p.solid || p.kind == BlockKind.Boundary) continue;
                    if (p.Max.Z > e.z + 0.05f) continue;
                    // The designed ways up (the old oak's limbs by the gap in the hedge) and tops you can't stand on (the
                    // shutter, under the Old Mill's bridge).
                    if (p.name.StartsWith("The old oak") || p.style == "shutter") continue;
                    float top = p.rampAxis == 0 ? p.Max.Y : Math.Max(p.rampLow, p.rampHigh);
                    if (top < e.top - reachUp) continue;
                    if (top >= e.floor - 0.2f) continue;   // the upper band's own ground (the public steps up to it)
                    float dx = Math.Max(0f, Math.Max(e.x0 - p.Max.X, p.Min.X - e.x1));
                    float dz = e.z - p.Max.Z;
                    if (dx * dx + dz * dz > reachAcross * reachAcross) continue;
                    report.AppendLine($"{e.name}: '{p.name}' (top {top:0.00}, {Math.Sqrt(dx * dx + dz * dz):0.0} m away) reaches its top {e.top:0.00}");
                }
            Assert.That(report.ToString(), Is.Empty, report.ToString());
        }

        // ---------- Integrity ----------

        [Test]
        public void TheGroundTilesNeverOverlap()
        {
            var h = Hill;
            var tiles = h.pieces.FindAll(p => p.kind == BlockKind.Ground && p.solid && p.style.Contains("|"));
            var report = new StringBuilder();
            for (int i = 0; i < tiles.Count; i++)
                for (int j = i + 1; j < tiles.Count; j++)
                {
                    var a = tiles[i];
                    var b = tiles[j];
                    float ox = Math.Min(a.Max.X, b.Max.X) - Math.Max(a.Min.X, b.Min.X);
                    float oz = Math.Min(a.Max.Z, b.Max.Z) - Math.Max(a.Min.Z, b.Min.Z);
                    if (ox > 0.01f && oz > 0.01f) report.AppendLine($"'{a.name}' and '{b.name}' overlap {ox:0.00} x {oz:0.00} m");
                }
            Assert.That(report.ToString(), Is.Empty, report.ToString());
        }

        [Test]
        public void TheGroundCoversTheWholeHill()
        {
            var h = Hill;
            var holes = new StringBuilder();
            int count = 0;
            for (float x = LarchHill.WestX + 0.5f; x < LarchHill.EastX; x += 2f)
                for (float z = LarchHill.SouthZ + 0.5f; z < LarchHill.NorthZ; z += 2f)
                    if (h.TopAt(x, z) < -7.5f) { if (count++ < 30) holes.AppendLine($"({x:0.0}, {z:0.0})"); }
            Assert.That(count, Is.EqualTo(0), count + " points with no ground: " + holes);
        }

        [Test]
        public void NothingToWalkToSitsInsideASolid()
        {
            var h = Hill;
            var open = LarchHill.Gates(true);
            string Inside(Vector3 p)
            {
                foreach (var b in h.pieces)
                {
                    if (!b.solid || !b.PresentOn(open) || b.kind == BlockKind.Boundary) continue;
                    if (p.X > b.Min.X + 0.05f && p.X < b.Max.X - 0.05f && p.Z > b.Min.Z + 0.05f && p.Z < b.Max.Z - 0.05f && p.Y > b.Min.Y && p.Y < b.TopAt(p.X, p.Z) - 0.05f)
                        return b.name;
                }
                return null;
            }
            Assert.That(Inside(h.arrival + new Vector3(0f, 0.5f, 0f)), Is.Null, "the arrival");
            foreach (var r in Night1Legs(h)) Assert.That(Inside(r.start + new Vector3(0f, 0.5f, 0f)), Is.Null, r.name + " start");
            foreach (var s in h.spots)
            {
                if (s.id == LarchHill.SpotElias || s.id == LarchHill.SpotWindow || s.id == LarchHill.SpotDoor || s.id == LarchHill.SpotPorchLight || s.id == LarchHill.SpotPatchedBike) continue;
                if (s.size.X > 0f) continue;
                Assert.That(Inside(s.position + new Vector3(0f, 0.3f, 0f)), Is.Null, s.name);
            }
            foreach (var b in h.bands) Assert.That(Inside(b.recover + new Vector3(0f, 0.5f, 0f)), Is.Null, b.name + "'s way back");
            foreach (var v in h.views) Assert.That(h.Solid(v.eye), Is.False, v.name + "'s camera is in the open");
        }

        // ---------- The landmarks ----------

        [Test]
        public void TheHospitalIsInSightFromMostOfTheNightAndTheHouseFromTheArrival()
        {
            var h = Hill;
            var eye = h.arrival + new Vector3(0f, 1.5f, 0f);
            Assert.That(h.SeesHouse(eye), Is.True, "from the arrival, the house over Walt's roof: " + h.Blocker(eye, h.housePoint));
            Assert.That(h.SeesHospital(eye), Is.True, "and the hospital crowning the hill");
            var report = new StringBuilder();
            int seen = 0, all = 0;
            int i = 0;
            foreach (var r in Night1Legs(h))
            {
                var run = Drive(h, r, waltAside: i++ != 0);
                int s = 0, hs = 0;
                foreach (var f in run.path)
                {
                    var e = f + new Vector3(0f, 1.5f, 0f);
                    if (h.SeesHospital(e)) s++;
                    if (h.SeesHouse(e)) hs++;
                }
                report.AppendLine($"{r.name}: the hospital from {s / (float)Math.Max(1, run.path.Count):P0}, the house from {hs / (float)Math.Max(1, run.path.Count):P0} ({run.path.Count} points)");
                seen += s;
                all += run.path.Count;
            }
            TestContext.Out.WriteLine(report.ToString());
            Assert.That(seen / (float)all, Is.GreaterThanOrEqualTo(0.6f), "the hospital is the landmark: " + report);
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
        public void BothHalvesOfTheSameSaturdayComeBeforeWalt()
        {
            var h = Hill;
            var yard = h.Spot(LarchHill.SpotYard);
            var paper = h.Spot(LarchHill.SpotPaper).position;
            Assert.That(paper.Z, Is.LessThan(yard.position.Z - yard.size.Z * 0.5f), "the paper: in the shop, before the yard");
            var loop = h.Spot(LarchHill.SpotLoopZone);
            Assert.That(loop.Contains(h.arrival + new Vector3(0f, 0.5f, 0f)), Is.True, "the loop is heard where she arrives");
            foreach (var id in new[] { LarchHill.SpotSprinkler, LarchHill.SpotNeighbour, LarchHill.SpotDog, LarchHill.SpotBallTo })
                Assert.That(loop.Contains(h.Spot(id).position + new Vector3(0f, 0.5f, 0f)), Is.True, id + " is in earshot");
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
            int said = 0;
            float prev = 0f;
            for (float t = 0.016f; t <= NeighbourLoop.Period * 3f; t += 0.016f) { if (a.LineBetween(prev, t)) said++; prev = t; }
            Assert.That(said, Is.EqualTo(3));
        }

        [Test]
        public void TheAnchorsAreWhereTheHillPutsThem()
        {
            var h = Hill;
            Assert.That(h.houseOrigin, Is.EqualTo(HillAnchors.HouseOrigin));
            Assert.That(h.TopAt(HillAnchors.FrontGate.X, HillAnchors.FrontGate.Z - 1f), Is.EqualTo(LarchHill.RowanY + 0.15f).Within(0.01f), "the gate is on Rowan Street's north footpath");
            Assert.That(h.TopAt(HillAnchors.Night2Start.X, HillAnchors.Night2Start.Z), Is.EqualTo(LarchHill.RowanY).Within(0.01f), "Night 2 starts on Rowan Street");
            Assert.That(h.TopAt(HillAnchors.EscapeExit.X, HillAnchors.EscapeExit.Z), Is.EqualTo(HillAnchors.M1Y).Within(0.01f), "the escape bursts out onto Chapel Row");
            Assert.That(h.TopAt(HillAnchors.EscapeEnd.X, HillAnchors.EscapeEnd.Z), Is.EqualTo(HillAnchors.M3Y + 0.15f).Within(0.01f), "the bus stop's footpath");
            Assert.That(h.TopAt(HillAnchors.HospitalEntrance.X, HillAnchors.HospitalEntrance.Z - 2f, HillAnchors.SummitY + 1f), Is.EqualTo(HillAnchors.SummitY).Within(0.01f), "the hospital's forecourt");
            Assert.That(h.TopAt(HillAnchors.Night1Arrival.X, HillAnchors.Night1Arrival.Z), Is.EqualTo(HillAnchors.Night1Arrival.Y).Within(0.01f), "the arrival's rise");
        }

        /// <summary>The plan of the hill (tools/art/hill-plan.py draws it): every piece's footprint and top, the trees, the
        /// routes, the spots, as JSON next to the test assembly (larch-hill.json). Runs only when asked by name.</summary>
        [Test, Explicit]
        public void ExportThePlan()
        {
            string path = System.IO.Path.Combine(TestContext.CurrentContext.TestDirectory, "larch-hill.json");
            var h = Hill;
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            string F(float v) => v.ToString("0.##", ci);
            string Q(string v) => "\"" + (v ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
            var sb = new StringBuilder();
            sb.Append("{\"pieces\":[");
            for (int i = 0; i < h.pieces.Count; i++)
            {
                var p = h.pieces[i];
                if (i > 0) sb.Append(',');
                float top = p.rampAxis == 0 ? p.Max.Y : Math.Max(p.rampLow, p.rampHigh);
                sb.Append("{\"n\":").Append(Q(p.name)).Append(",\"k\":").Append(Q(p.kind.ToString())).Append(",\"l\":").Append(Q(p.layer)).Append(",\"st\":").Append(Q(p.style))
                  .Append(",\"x0\":").Append(F(p.Min.X)).Append(",\"z0\":").Append(F(p.Min.Z)).Append(",\"x1\":").Append(F(p.Max.X)).Append(",\"z1\":").Append(F(p.Max.Z))
                  .Append(",\"top\":").Append(F(top)).Append(",\"ra\":").Append(p.rampAxis).Append('}');
            }
            sb.Append("],\"trees\":[");
            for (int i = 0; i < h.trees.Count; i++)
            {
                var t = h.trees[i];
                if (i > 0) sb.Append(',');
                sb.Append('[').Append(F(t.foot.X)).Append(',').Append(F(t.foot.Z)).Append(',').Append(F(t.canopy)).Append(']');
            }
            sb.Append("],\"routes\":[");
            var all = new List<BlockRoute> { h.wayIn };
            all.AddRange(h.night1);
            all.Add(h.escapeLine);
            for (int i = 0; i < all.Count; i++)
            {
                var r = all[i];
                if (i > 0) sb.Append(',');
                sb.Append("{\"name\":").Append(Q(r.name)).Append(",\"points\":[");
                var line = r.course.route;
                int n = Math.Max(2, (int)(line.Length / 2f));
                for (int k = 0; k <= n; k++)
                {
                    var pt = line.PointAt(line.Length * k / n, 0f);
                    if (k > 0) sb.Append(',');
                    sb.Append('[').Append(F(pt.X)).Append(',').Append(F(pt.Z)).Append(']');
                }
                sb.Append("]}");
            }
            sb.Append("],\"spots\":[");
            for (int i = 0; i < h.spots.Count; i++)
            {
                var s = h.spots[i];
                if (i > 0) sb.Append(',');
                sb.Append("{\"id\":").Append(Q(s.id)).Append(",\"name\":").Append(Q(s.name)).Append(",\"x\":").Append(F(s.position.X)).Append(",\"z\":").Append(F(s.position.Z)).Append('}');
            }
            sb.Append("],\"house\":[").Append(F(h.houseOrigin.X)).Append(',').Append(F(h.houseOrigin.Z)).Append("],\"arrival\":[").Append(F(h.arrival.X)).Append(',').Append(F(h.arrival.Z)).Append("]}");
            System.IO.File.WriteAllText(path, sb.ToString());
            TestContext.Out.WriteLine("plan: " + path);
        }
    }
}
