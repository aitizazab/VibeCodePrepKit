using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using JamKit.DeepBlock;
using JamKit.Movement;
using JamKit.Stealth;
using JamKit.Tests.Movement;
using NUnit.Framework;

namespace JamKit.Tests.DeepBlock
{
    /// <summary>The deep block (layout A of docs/DENSE-STREET.md, docs/DEEP-BLOCK.md): every beat of the four routes fits
    /// the movement's reach (<see cref="CourseReach"/>); the course autopilot runs each route from its start to the hill
    /// gate's apron through the same boxes the Unity builder makes; nothing to walk to sits inside a solid; the nights
    /// open and shut the switches; and in the garden zone Theo counts on the trampoline, sees over the fences only at
    /// the top of a bounce, doesn't look up at a roof, finds a player standing in the open and one under a deck.</summary>
    public class DeepBlockTests
    {
        const float Dt = 1f / 60f;

        /// <summary>The block as a movement world on night <paramref name="night"/>.</summary>
        public static BoxWorld WorldOf(JamKit.DeepBlock.DeepBlock b, int night = 1, float skin = 0.04f)
        {
            var open = JamKit.DeepBlock.DeepBlock.Night(night);
            var w = new BoxWorld { skin = skin };
            foreach (var p in b.pieces)
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

        public sealed class RouteRun
        {
            public bool finished;
            public float seconds, topSpeed;
            public string failure = "";
            public readonly StringBuilder log = new StringBuilder();
        }

        /// <summary>The course autopilot along a route from its start (standing) until it stands on the hill gate's apron.</summary>
        public static RouteRun Drive(JamKit.DeepBlock.DeepBlock b, BlockRoute r, int fps = 60, float maxSeconds = 70f)
        {
            var world = WorldOf(b);
            var sim = new MoveSim(world, new MoveDef());
            var driver = new MoveDriver(sim);
            var pilot = new CourseAutopilot(r.course);
            driver.Teleport(r.start, r.startYaw, Vector3.Zero);
            pilot.Reset(0f);
            var run = new RouteRun();
            string lastMode = "";
            float dt = 1f / fps, lastS = 0f, stuckFor = 0f;
            for (float t = 0f; t < maxSeconds; t += dt)
            {
                driver.Frame(dt, pilot.Next(sim));
                var p = sim.Position;
                float s = r.course.route.Project(p, out float lateral);
                run.topSpeed = Math.Max(run.topSpeed, sim.HorizontalSpeed);
                string mode = sim.Mode.ToString();
                if (mode != lastMode)
                {
                    run.log.AppendLine($"{t,6:0.00}s s {s,6:0.0} map x {p.Z,6:0.0} y {p.X,6:0.0} h {p.Y,6:0.00} lat {lateral,5:0.0} {mode,-8} speed {sim.HorizontalSpeed,5:0.0}");
                    lastMode = mode;
                }
                if (p.Y < r.course.killY) { run.failure = $"fell at s {s:0.0} (map x {p.Z:0.0}, y {p.X:0.0})"; return run; }
                if (s >= r.finishS - 0.5f && p.Y > 5.9f && sim.Grounded) { run.finished = true; run.seconds = t; return run; }
                stuckFor = s > lastS + 0.5f ? 0f : stuckFor + dt;
                if (s > lastS + 0.5f) lastS = s;
                if (stuckFor > 4f) { run.failure = $"stuck at s {s:0.0} (map x {p.Z:0.0}, y {p.X:0.0}, h {p.Y:0.0}) in {mode}"; return run; }
            }
            run.failure = "out of time";
            return run;
        }

        // ---------- The routes ----------

        [Test]
        public void EveryRouteBeatFitsTheMovementsReach()
        {
            var b = JamKit.DeepBlock.DeepBlock.Build();
            Assert.That(b.routes.Count, Is.EqualTo(4), "street, back, roof, under");
            var def = new MoveDef();
            foreach (var r in b.routes)
            {
                Assert.That(r.beats.Count, Is.GreaterThan(1), r.name);
                var problems = CourseReach.Check(r.beats, def, 0.04f, 0f);
                Assert.That(problems, Is.Empty, r.name + ": " + string.Join("; ", problems));
            }
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void TheAutopilotRunsTheRouteToTheHillGate(int index)
        {
            var b = JamKit.DeepBlock.DeepBlock.Build();
            var r = b.routes[index];
            var run = Drive(b, r);
            TestContext.Out.WriteLine(run.log.ToString());
            TestContext.Out.WriteLine($"{r.name}: {(run.finished ? $"{run.seconds:0.0} s" : run.failure)} (the doc's rough time {r.docSeconds:0} s), {r.course.route.Length:0} m, top speed {run.topSpeed:0.0} m/s");
            Assert.That(run.finished, Is.True, r.name + ": " + run.failure);
        }

        [TestCase(20)]
        [TestCase(144)]
        public void TheRoutesHoldAtOtherFrameRates(int fps)
        {
            var b = JamKit.DeepBlock.DeepBlock.Build();
            foreach (var r in b.routes)
            {
                var run = Drive(b, r, fps);
                if (!run.finished) TestContext.Out.WriteLine(run.log.ToString());
                Assert.That(run.finished, Is.True, $"{r.name} at {fps} fps: {run.failure}");
            }
        }

        [Test]
        public void TheRouteStartsAndTheirLinesAreClear()
        {
            var b = JamKit.DeepBlock.DeepBlock.Build();
            foreach (var r in b.routes)
            {
                Assert.That(r.start.Y, Is.InRange(-0.1f, 0.1f), r.name + " starts on the ground");
                Assert.That(InsideSolid(b, r.start + new Vector3(0f, 0.5f, 0f), 1), Is.Null, r.name + " start");
            }
        }

        // ---------- The block's parts ----------

        static string InsideSolid(JamKit.DeepBlock.DeepBlock b, Vector3 p, int night, float margin = 0.25f)
        {
            var open = JamKit.DeepBlock.DeepBlock.Night(night);
            foreach (var piece in b.pieces)
            {
                if (!piece.solid || !piece.PresentOn(open)) continue;
                var mn = piece.Min - new Vector3(margin, 0f, margin);
                var mx = piece.Max + new Vector3(margin, 0f, margin);
                float top = piece.rampAxis != 0 ? piece.TopAt(p.X, p.Z) : mx.Y;
                if (p.X > mn.X && p.X < mx.X && p.Z > mn.Z && p.Z < mx.Z && p.Y > mn.Y && p.Y < top) return piece.name;
            }
            return null;
        }

        [Test]
        public void NothingToWalkToOrFindIsInsideASolid()
        {
            var b = JamKit.DeepBlock.DeepBlock.Build();
            foreach (var n in b.theoNodes) Assert.That(InsideSolid(b, n + new Vector3(0f, 0.5f, 0f), 2), Is.Null, "Theo's point " + n);
            foreach (var s in b.spots) Assert.That(InsideSolid(b, s.Approach + new Vector3(0f, 0.5f, 0f), 2), Is.Null, s.name + " check point");
            foreach (var m in b.memories) Assert.That(InsideSolid(b, m, 2, 0.05f), Is.Null, "memory " + m);
            Assert.That(b.memories.Count, Is.EqualTo(6));
            Assert.That(b.spots.Count, Is.EqualTo(8));
            var kinds = new HashSet<HideKind>();
            foreach (var s in b.spots) kinds.Add(s.kind);
            Assert.That(kinds.Count, Is.EqualTo(3), "bush-like, under and closet-like places");
        }

        [Test]
        public void TheKitSizesAreTheDocs()
        {
            var b = JamKit.DeepBlock.DeepBlock.Build();
            BlockPiece Find(string name) => b.pieces.Find(p => p.name == name);
            Assert.That(Find("Back fence (x 32-100)").size.Y, Is.EqualTo(1.8f).Within(0.01f), "garden fences 1.8");
            Assert.That(Find("Garage 34").size.Y, Is.EqualTo(2.7f).Within(0.01f), "garage roofs 2.7");
            Assert.That(Find("House 3").Min.Z - Find("House 1").Max.Z, Is.EqualTo(4.5f).Within(0.01f), "passage A 4.5 m");
            Assert.That(Find("House 5").Min.Z - Find("House 3").Max.Z, Is.EqualTo(4.5f).Within(0.01f), "passage B 4.5 m");
            Assert.That(Find("House 3").size.X, Is.InRange(8f, 15f), "house side walls are one wallrun (8-15 m)");
            Assert.That(Find("Unfinished wing (block wall)").size.Z, Is.EqualTo(14f).Within(0.01f), "the 14 m block wall");
            Assert.That(Find("Deck, house 1").Min.Y, Is.EqualTo(1.15f).Within(0.01f), "crawl or slide under a deck");
            Assert.That(Find("Culvert (the street over the channel)").Min.Y - (-2f), Is.EqualTo(1.15f).Within(0.01f), "the culvert's 1.15 m");
            Assert.That(Find("Creek bed").Max.Y, Is.EqualTo(-2f), "the creek 2 m deep");
            Assert.That(Find("House 1 porch roof").Max.Y, Is.EqualTo(2.9f).Within(0.01f));
            Assert.That(Find("Picket (north, west)").size.Y, Is.EqualTo(0.85f).Within(0.01f));
        }

        [Test]
        public void TheNightsOpenAndShutTheSwitches()
        {
            var b = JamKit.DeepBlock.DeepBlock.Build();
            Assert.That(b.switches.Count, Is.EqualTo(8));
            for (int s = 0; s < 8; s++)
            {
                bool any = false;
                foreach (var p in b.pieces) if (p.switchIndex == s) any = true;
                Assert.That(any, Is.True, b.switches[s].id + " has a prop");
            }
            var n1 = JamKit.DeepBlock.DeepBlock.Night(1);
            var n3 = JamKit.DeepBlock.DeepBlock.Night(3);
            var n4 = JamKit.DeepBlock.DeepBlock.Night(4);
            for (int s = 0; s < 8; s++) Assert.That(n1[s], Is.True, "night 1: everything open");
            Assert.That(n1[JamKit.DeepBlock.DeepBlock.SheetsSwitch], Is.False);
            Assert.That(n3[5], Is.False, "night 3: the lane's top gate is shut");
            Assert.That(n3[JamKit.DeepBlock.DeepBlock.SheetsSwitch], Is.True, "and the sheets are up");
            Assert.That(n4[3] || n4[4] || n4[7], Is.False, "night 4: no ladder, no plank, Walt's door shut");
            // Night 4's shut lane gate blocks the back route where night 1's doesn't.
            var gate = b.pieces.Find(p => p.name.StartsWith("Lane top gate"));
            Assert.That(gate.PresentOn(n1), Is.False);
            Assert.That(gate.PresentOn(n4), Is.True);
            Assert.That(gate.size.Y, Is.GreaterThan(new MoveDef().mantleMaxHeight + new MoveDef().jumpHeight), "too tall to climb");
        }

        // ---------- Hide-and-seek ----------

        static StealthTarget Standing(float mx, float my, float h = 0f, int spot = -1)
        {
            var t = StealthTarget.At(JamKit.DeepBlock.DeepBlock.W(mx, my, h));
            t.speed = 0f;
            t.hidingSpot = spot;
            return t;
        }

        [Test]
        public void AtTheTopOfABounceTheoSeesOverTheFenceAndAtTheBottomHeDoesnt()
        {
            var b = JamKit.DeepBlock.DeepBlock.Build();
            var world = b.StealthWorld(JamKit.DeepBlock.DeepBlock.Night(2));
            var def = JamKit.DeepBlock.DeepBlock.TheoDef();
            var target = Standing(19f, 24f); // in garden 1, 3 m behind the 1.8 m fence from Theo's garden
            float yaw = 180f;                  // facing garden 1 (map -x = world -Z)
            var low = b.trampoline + new Vector3(0f, def.eyeHeight + b.trampolineHeight, 0f);
            var high = low + new Vector3(0f, b.bounceHeight, 0f);
            float vLow = StealthSight.Visibility(def, world, low, yaw, target, null, out _);
            float vHigh = StealthSight.Visibility(def, world, high, yaw, target, null, out _);
            TestContext.Out.WriteLine($"over the fence: {vLow:0.00} on the mat, {vHigh:0.00} at the top of the bounce");
            Assert.That(vLow, Is.EqualTo(0f), "the fence hides you from the mat");
            Assert.That(vHigh, Is.GreaterThan(0f), "the top of the bounce sees over it");

            // The brain does it by itself: on the lookout its sight flickers with the bounce.
            var brain = b.Theo(JamKit.DeepBlock.DeepBlock.Night(2), out _);
            brain.StartHideAndSeek(b.trampoline, 180f, 0f);
            int seen = 0, unseen = 0;
            for (float s = 0f; s < 3f; s += Dt)
            {
                bool bouncing = brain.OnLookout; // the eye's height this tick comes from the task it starts in
                brain.Tick(Dt, target, default);
                if (!bouncing) continue;
                if (brain.Visibility > 0f) seen++; else unseen++;
                if (seen > 0 && unseen > 0) break; // seen at a bounce's top: it goes to look
            }
            TestContext.Out.WriteLine($"on the lookout: seen {seen} ticks, unseen {unseen}");
            Assert.That(seen, Is.GreaterThan(0));
            Assert.That(unseen, Is.GreaterThan(0));
        }

        [Test]
        public void TheoDoesntLookUpAtTheGarageRoof()
        {
            var b = JamKit.DeepBlock.DeepBlock.Build();
            var world = b.StealthWorld(JamKit.DeepBlock.DeepBlock.Night(2));
            var def = JamKit.DeepBlock.DeepBlock.TheoDef();
            var eye = JamKit.DeepBlock.DeepBlock.W(29f, 19.5f, def.eyeHeight);
            var onRoof = Standing(29f, 13f, 2.7f);
            float v = StealthSight.Visibility(def, world, eye, -90f, onRoof, null, out _); // facing it (map -y = world -X)
            Assert.That(v, Is.EqualTo(0f), "6.5 m off and 2.7 m up: above a ±15° child's gaze");
            var adult = new StealthDef();
            Assert.That(StealthSight.Visibility(adult, world, eye, -90f, onRoof, null, out _), Is.GreaterThan(0f), "a grown-up would see you");
        }

        static float FindTime(SeekerBrain brain, StealthTarget t, float limit, out int inSpot)
        {
            inSpot = -1;
            for (float s = 0f; s < limit; s += Dt)
            {
                brain.Tick(Dt, t, default);
                if (brain.Events.found) { inSpot = brain.Events.foundInSpot; return s; }
            }
            return -1f;
        }

        [Test]
        public void TheoFindsAPlayerStandingInTheOpenGarden()
        {
            var b = JamKit.DeepBlock.DeepBlock.Build();
            var brain = b.Theo(JamKit.DeepBlock.DeepBlock.Night(2), out _);
            brain.StartHideAndSeek(b.trampoline, b.theoCountYaw);
            var t = Standing(12f, 25f);
            float found = FindTime(brain, t, 150f, out int spot);
            TestContext.Out.WriteLine($"found in the open after {found:0.0} s (10 s of counting included)");
            Assert.That(found, Is.GreaterThan(brain.def.countTime - 0.1f), "not while counting");
            Assert.That(spot, Is.EqualTo(-1));
        }

        [Test]
        public void TheoChecksTheHidingPlacesAndFindsYouUnderTheDeck()
        {
            var b = JamKit.DeepBlock.DeepBlock.Build();
            var brain = b.Theo(JamKit.DeepBlock.DeepBlock.Night(2), out _);
            brain.StartHideAndSeek(b.trampoline, b.theoCountYaw, 0f);
            int deck = b.spots.FindIndex(s => s.name.StartsWith("H1"));
            var at = JamKit.DeepBlock.DeepBlock.W(9f, 32.5f);
            var t = Standing(9f, 32.5f, 0f, b.SpotAt(at));
            t.crouched = true;
            t.height = 1f;
            Assert.That(t.hidingSpot, Is.EqualTo(deck));
            float found = FindTime(brain, t, 300f, out int spot);
            TestContext.Out.WriteLine($"found under the deck after {found:0.0} s (sweeps {brain.Sweeps})");
            Assert.That(found, Is.GreaterThan(0f));
            Assert.That(spot, Is.EqualTo(deck), "by checking it");
        }

        [Test]
        public void TheosWalkingMapIsOnePieceAndReachesEveryHidingPlace()
        {
            var b = JamKit.DeepBlock.DeepBlock.Build();
            var world = b.StealthWorld(JamKit.DeepBlock.DeepBlock.Night(2));
            var g = b.TheoGraph(world);
            Assert.That(g.Connected(), Is.True);
            foreach (var s in b.spots)
            {
                int n = g.NearestReachable(world, s.Approach);
                Assert.That(WaypointGraph.WalkClear(world, s.Approach, g[n]), Is.True, s.name);
            }
        }
    }
}
