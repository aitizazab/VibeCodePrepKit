using System;
using System.Collections.Generic;
using System.Numerics;
using JamKit.DreamEscape;
using JamKit.Movement;
using JamKit.Stealth;
using JamKit.Tests.Movement;
using NUnit.Framework;

namespace JamKit.Tests.DreamEscape
{
    /// <summary>Night 2's house: the course autopilot gets through the way in (the stretched hallway's wall, the slide under
    /// the half-open door) and through the escape (the house tipping: the floor-wall, the doorframes, the staircase slide,
    /// out of the front door to the end of the rain) at 20 to 300 fps and with jitter, never caught by the house folding
    /// shut; the route fits the movement's reach; Theo's graph reaches every room, spot and memory; Theo finds a player
    /// standing still, and one hiding in the pantry; every memory can be reached; the story's phases, the creator's last-
    /// memory line in both its endings, and Lena condemning Nadia making the Despair route easier.</summary>
    public class DreamHouseTests
    {
        const float Dt = 1f / 60f;

        static DreamHouse Build() => DreamHouse.Build();

        [TestCase(20, 0)]
        [TestCase(60, 0)]
        [TestCase(144, 0)]
        [TestCase(300, 0)]
        [TestCase(0, 7)]
        public void TheAutopilotGetsThroughTheWayIntoTheKitchen(int fps, int jitter)
        {
            var h = Build();
            var r = LabCourseTests.Drive(h.course, h.arrivalCheckpoint, h.kitchenS + 1f, 40f, fps == 0 ? 60 : fps, false, jitter, 0.04f);
            if (!r.finished || r.falls > 0) TestContext.Out.WriteLine(r.log.ToString());
            Assert.That(r.finished, Is.True, fps + " fps");
            Assert.That(r.falls, Is.EqualTo(0), fps + " fps");
        }

        [TestCase(20, 0)]
        [TestCase(60, 0)]
        [TestCase(144, 0)]
        [TestCase(300, 0)]
        [TestCase(0, 7)]
        [TestCase(0, 11)]
        public void TheAutopilotEscapesTheTippingHouseIntoTheRain(int fps, int jitter)
        {
            var h = Build();
            var r = LabCourseTests.Drive(h.course, h.escapeCheckpoint, h.rain.rainEndS, 60f, fps == 0 ? 60 : fps, true, jitter, 0.04f);
            if (!r.finished || r.falls > 0 || r.caught > 0) TestContext.Out.WriteLine(r.log.ToString());
            TestContext.Out.WriteLine($"{fps} fps: escape {r.seconds:0.0} s, top speed {r.topSpeed:0.0} m/s");
            Assert.That(r.finished, Is.True, fps + " fps");
            Assert.That(r.falls, Is.EqualTo(0), fps + " fps");
            Assert.That(r.caught, Is.EqualTo(0), "the house folding shut never catches a good run");
        }

        [Test]
        public void EveryEscapeCheckpointRunsToTheNext()
        {
            var h = Build();
            var c = h.course;
            for (int i = h.escapeCheckpoint; i < c.checkpoints.Count; i++)
            {
                float until = i + 1 < c.checkpoints.Count ? c.checkpoints[i + 1].s + 1f : h.rain.rainEndS;
                var r = LabCourseTests.Drive(c, i, until, 40f, 60, true);
                if (!r.finished || r.falls > 0) TestContext.Out.WriteLine(r.log.ToString());
                Assert.That(r.finished, Is.True, c.checkpoints[i].name);
                Assert.That(r.falls, Is.EqualTo(0), c.checkpoints[i].name);
            }
        }

        [Test]
        public void TheSmokeRunsLegFromTheStairsFitsItsBudget()
        {
            // DreamHouseGame's smoke drive runs from "The stairs" (the whole smoke run, credits included, has 20 s).
            var h = Build();
            int stairs = h.course.checkpoints.FindIndex(c => c.name == "The stairs");
            Assert.That(stairs, Is.GreaterThan(h.escapeCheckpoint));
            var r = LabCourseTests.Drive(h.course, stairs, h.rain.rainEndS, 20f, 60, true);
            TestContext.Out.WriteLine("the stairs to the end of the rain: " + r.seconds.ToString("0.0") + " s");
            Assert.That(r.finished, Is.True);
            Assert.That(r.falls + r.caught, Is.EqualTo(0));
            Assert.That(r.seconds, Is.LessThan(9f), "leaves room in the 20 s smoke budget");
        }

        [Test]
        public void TheRouteFitsTheMovementsReach()
        {
            var h = Build();
            var problems = h.CheckReach(new MoveDef());
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
            var kinds = new HashSet<CourseBeatKind>();
            foreach (var b in h.beats) kinds.Add(b.kind);
            foreach (var k in new[] { CourseBeatKind.Wallrun, CourseBeatKind.WallToWall, CourseBeatKind.Slide, CourseBeatKind.SlideRamp })
                Assert.That(kinds, Does.Contain(k), k.ToString());
            Assert.That(h.looks.Count, Is.EqualTo(h.course.pieces.Count));
            foreach (var ch in new[] { DreamHouse.ChapterArrival, DreamHouse.ChapterHideAndSeek, DreamHouse.ChapterTips, DreamHouse.ChapterFrontDoor })
                Assert.That(h.CheckpointIndex(ch), Is.GreaterThanOrEqualTo(0), ch);
            for (int i = 1; i < h.course.checkpoints.Count; i++) Assert.That(h.course.checkpoints[i].s, Is.GreaterThan(h.course.checkpoints[i - 1].s));
            Assert.That(h.stretchTo, Is.GreaterThan(h.stretchFrom), "the hallway stretches away");
        }

        [Test]
        public void TheosGraphReachesEveryRoomSpotAndMemory()
        {
            var h = Build();
            var world = h.StealthWorld();
            var g = h.Graph(world);
            Assert.That(g.Connected(), Is.True, "one piece");
            var boxes = LabCourseTests.WorldOf(h.course, null);
            foreach (var n in h.nodes)
            {
                Assert.That(boxes.CapsuleFits(n + new Vector3(0f, 0.05f, 0f), 0.3f, 1.2f), Is.True, $"node {n} stands clear");
                Assert.That(DreamHouse.InHub(n), Is.True, $"node {n} is in the house");
            }
            foreach (var s in h.spots)
            {
                int near = g.NearestReachable(world, s.Approach);
                Assert.That(near, Is.GreaterThanOrEqualTo(0), s.name);
                Assert.That(Flat(g[near], s.Approach), Is.LessThan(h.connectDistance), s.name + "'s approach is walkable");
            }
            Assert.That(h.spots.Exists(s => s.kind == HideKind.Closet), Is.True);
            Assert.That(h.spots.Exists(s => s.kind == HideKind.Bush), Is.True);
            Assert.That(h.memories.Count, Is.EqualTo(5));
            foreach (var m in h.memories)
            {
                Assert.That(DreamHouse.InHub(m.position - new Vector3(0f, 1f, 0f)), Is.True, m.name);
                Assert.That(CanReach(boxes, m.position), Is.True, m.name + " can be picked up (1.1 m from somewhere she can stand)");
                Assert.That(g.NearestReachable(world, new Vector3(m.position.X, DreamHouse.Ground, m.position.Z)), Is.GreaterThanOrEqualTo(0), m.name);
            }
        }

        static float Flat(Vector3 a, Vector3 b) => (float)Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z));

        /// <summary>A standing spot on the ground floor (the capsule fits) whose body centre is within the pick-up radius.</summary>
        static bool CanReach(BoxWorld w, Vector3 item)
        {
            for (float dx = -1.2f; dx <= 1.2f; dx += 0.1f)
                for (float dz = -1.2f; dz <= 1.2f; dz += 0.1f)
                {
                    var feet = new Vector3(item.X + dx, DreamHouse.Ground + 0.02f, item.Z + dz);
                    if (Vector3.Distance(feet + new Vector3(0f, 0.9f, 0f), item) > 1.05f) continue;
                    if (w.CapsuleFits(feet + new Vector3(0f, 0.04f, 0f), 0.38f, 1.7f)) return true;
                }
            return false;
        }

        static SeekerBrain Theo(DreamHouse h, out StealthBoxWorld world)
        {
            world = h.StealthWorld();
            var g = h.Graph(world);
            var b = new SeekerBrain(new StealthDef(), world, g, h.spots, h.theoStart, h.theoYaw);
            b.StartHideAndSeek(h.theoStart, h.theoCountYaw);
            return b;
        }

        static float FindTime(SeekerBrain b, StealthTarget t, float limit, out int spot)
        {
            spot = -1;
            for (float s = 0f; s < limit; s += Dt)
            {
                b.Tick(Dt, t, default);
                if (b.Events.found) { spot = b.Events.foundInSpot; return s; }
            }
            return -1f;
        }

        [Test]
        public void TheoFindsAPlayerStandingStill()
        {
            var h = Build();
            foreach (var at in new[] { new Vector3(7f, DreamHouse.Ground, 82.5f), new Vector3(-8f, DreamHouse.Ground, 93.5f), new Vector3(4f, DreamHouse.Ground, 97f) })
            {
                var b = Theo(h, out _);
                float found = FindTime(b, StealthTarget.At(at), 120f, out int spot);
                TestContext.Out.WriteLine($"standing at {at}: found after {found:0.0} s (counting {b.def.countTime:0} s first)");
                Assert.That(found, Is.GreaterThan(b.def.countTime - 0.5f), "not while counting");
                Assert.That(spot, Is.EqualTo(-1), "by sight");
            }
        }

        [Test]
        public void TheoFindsAPlayerHidingInThePantryByCheckingIt()
        {
            var h = Build();
            int pantry = h.spots.FindIndex(s => s.name == "The pantry");
            var b = Theo(h, out _);
            var t = StealthTarget.At(h.spots[pantry].Center - new Vector3(0f, 1f, 0f));
            t.hidingSpot = h.SpotAt(t.feet);
            Assert.That(t.hidingSpot, Is.EqualTo(pantry));
            float found = FindTime(b, t, 240f, out int spot);
            TestContext.Out.WriteLine($"in the pantry: found after {found:0.0} s");
            Assert.That(found, Is.GreaterThan(0f), "the sweep opens the pantry");
            Assert.That(spot, Is.EqualTo(pantry));
        }

        [Test]
        public void TheStoryRunsFromPancakesToTheRain()
        {
            var s = new DreamHouseStory();
            s.Restart();
            Assert.That(s.Phase, Is.EqualTo(HousePhase.WayIn));
            Assert.That(s.StartHideAndSeek(), Is.False, "not before the pancakes");
            Assert.That(s.ReachKitchen(), Is.True);
            Assert.That(s.ReachKitchen(), Is.False, "once");
            Assert.That(s.Phase, Is.EqualTo(HousePhase.Pancakes));
            Assert.That(s.StartHideAndSeek(), Is.True);
            Assert.That(s.Rounds, Is.EqualTo(1));
            // Found before every memory: another round.
            Assert.That(s.TheoFoundHer(), Is.False);
            Assert.That(s.Phase, Is.EqualTo(HousePhase.HideAndSeek));
            Assert.That(s.Rounds, Is.EqualTo(2));
            string line = s.FoundEveryMemory(false);
            Assert.That(line, Does.StartWith(DreamHouseLines.Placeholder));
            Assert.That(line, Does.Contain(DreamHouseLines.AllFoundStart));
            // Found after the last memory: Elias hasn't come; the argument.
            Assert.That(s.TheoFoundHer(), Is.True);
            Assert.That(s.Phase, Is.EqualTo(HousePhase.Argument));
            Assert.That(s.Choose(ArgumentOutcome.None), Is.False);
            Assert.That(s.Choose(ArgumentOutcome.Condemns), Is.True);
            Assert.That(s.Phase, Is.EqualTo(HousePhase.Escape));
            Assert.That(s.LenaCondemned, Is.True);
            s.Escaped();
            Assert.That(s.Phase, Is.EqualTo(HousePhase.Out));
            s.JumpTo(HousePhase.Escape);
            Assert.That(s.Outcome, Is.EqualTo(ArgumentOutcome.Unsure), "a chapter jump to the escape assumes the gentler ending");
            Assert.That(s.AllMemoriesFound, Is.True);
        }

        [Test]
        public void TheLastMemoryLineKeepsBothEndingsAndEveryLineIsAPlaceholder()
        {
            string despair = DreamHouseLines.AllFound(true), normal = DreamHouseLines.AllFound(false);
            Assert.That(despair, Is.Not.EqualTo(normal), "the ending differs by route");
            Assert.That(DreamHouseLines.AllFoundEndings.Length, Is.EqualTo(2));
            foreach (var e in DreamHouseLines.AllFoundEndings) Assert.That(despair.EndsWith(e) || normal.EndsWith(e), Is.True, e);
            foreach (var group in new[] { DreamHouseLines.Arrival, DreamHouseLines.Pancakes, DreamHouseLines.FoundYou, DreamHouseLines.EliasNotHere, DreamHouseLines.Unsure, DreamHouseLines.Condemns, DreamHouseLines.KickedOut })
                foreach (var l in group)
                {
                    Assert.That(l.text, Does.StartWith(DreamHouseLines.Placeholder), l.text);
                    Assert.That(l.speaker, Is.Not.Empty);
                    Assert.That(l.seconds, Is.InRange(1.5f, 6f));
                }
            Assert.That(DreamHouseLines.ArgumentPrompt, Does.Contain("PLACEHOLDER"));
        }

        [Test]
        public void LenaCondemningNadiaMakesTheDespairRouteEasier()
        {
            var h = Build();
            var normal = DreamHouseStory.RainFor(h.rain, false);
            var condemned = DreamHouseStory.RainFor(h.rain, true);
            Assert.That(condemned.despairS, Is.LessThan(normal.despairS), "the point of no return comes sooner");
            Assert.That(condemned.rainStartS, Is.EqualTo(normal.rainStartS));
            Assert.That(h.rain.despairS, Is.EqualTo(normal.despairS), "the house's own spot is the normal one");
            Assert.That(DreamHouseStory.RainDefFor(true).soakTime, Is.LessThan(DreamHouseStory.RainDefFor(false).soakTime), "and the rain soaks faster");
            // Walking into the rain at the same pace: the condemned visit reaches the choice first.
            var a = new DreamRain(normal) { def = DreamHouseStory.RainDefFor(false) };
            var b = new DreamRain(condemned) { def = DreamHouseStory.RainDefFor(true) };
            float sa = -1f, sb = -1f;
            for (float s = normal.rainStartS - 2f; s < normal.rainEndS; s += 0.1f)
            {
                if (a.Tick(0.02f, s).despairChosen) sa = s;
                if (b.Tick(0.02f, s).despairChosen) sb = s;
            }
            Assert.That(sb, Is.GreaterThan(0f));
            Assert.That(sa, Is.GreaterThan(sb));
        }

        [Test]
        public void TheUmbrellaWaitsBesideTheFrontDoorOnTheHallFloor()
        {
            var h = Build();
            var r = h.rain;
            var boxes = LabCourseTests.WorldOf(h.course, null);
            Assert.That(r.rainStartS, Is.GreaterThan(h.chaseEndS), "the house stops folding before the door");
            Assert.That(Math.Abs(r.umbrella.Z - r.rainStartS), Is.LessThan(1.5f), "next to the door");
            Assert.That(r.umbrella.Z, Is.LessThan(r.rainStartS), "inside");
            Assert.That(Math.Abs(r.umbrella.X - h.course.LaneAt(r.umbrella.Z)), Is.InRange(1.5f, 4f), "beside the way out, not in it");
            Assert.That(boxes.CastDown(new Vector3(r.umbrella.X, r.umbrella.Y, r.umbrella.Z), 0.1f, 1f, out var hit), Is.True, "on the hall floor");
            Assert.That(hit.point.Y, Is.EqualTo(r.floorY).Within(0.01f));
            Assert.That(r.resetS, Is.LessThan(r.umbrella.Z));
            Assert.That(r.resetS, Is.GreaterThan(h.hubCheckpoint >= 0 ? h.course.checkpoints[h.hubCheckpoint].s : 0f), "a new visit puts it back");
            Assert.That(r.rainEndS, Is.GreaterThan(r.despairS + 10f));
        }
    }
}
