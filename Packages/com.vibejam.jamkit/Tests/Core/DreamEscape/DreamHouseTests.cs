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
    /// memory line in both its endings, and Lena condemning Nadia making the Despair route easier. The architecture
    /// (2026-10-12): real proportions, rooms of their own, ceilings of different heights, the attic as a vertical loop the
    /// movement can do, Theo's sight stopped by walls, ceilings and the attic's floor, loops between rooms, doors that fit.</summary>
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
                Assert.That(CanReach(boxes, m), Is.True, m.name + " can be picked up from where she stands to take it");
                Assert.That(g.NearestReachable(world, new Vector3(m.position.X, DreamHouse.Ground, m.position.Z)), Is.GreaterThanOrEqualTo(0), m.name);
            }
        }

        [Test]
        public void TheHouseHasRealProportionsRoomsOfItsOwnAndAnAttic()
        {
            var h = Build();
            Assert.That(DreamHouse.HubX1 - DreamHouse.HubX0, Is.InRange(10f, 16f), "a family home's width");
            Assert.That(DreamHouse.HubZ1 - DreamHouse.HubZ0, Is.InRange(9f, 13f), "and depth");
            foreach (var name in new[] { "Kitchen and dining", "Living room", "Hall", "Pantry", "Parents' room", "Bath", "Theo's room", "Attic" })
                Assert.That(h.rooms.Exists(r => r.name == name), Is.True, name);
            foreach (var r in h.rooms)
            {
                float area = (r.x1 - r.x0) * (r.z1 - r.z0);
                if (r.name == "Attic") continue;
                Assert.That(area, Is.LessThan(65f), r.name + " is a room, not a hall");
            }
            var hall = h.rooms.Find(r => r.name == "Hall");
            Assert.That(hall.z1 - hall.z0, Is.LessThan(2.6f), "the hall is narrow");
            // Ceilings of different heights (at least three), the attic over them.
            var heights = new HashSet<float>();
            foreach (var r in h.rooms) if (r.name != "Attic") heights.Add(r.ceiling);
            Assert.That(heights.Count, Is.GreaterThanOrEqualTo(3));
            var attic = h.rooms.Find(r => r.name == "Attic");
            Assert.That(attic.floorY, Is.GreaterThan(DreamHouse.Ground + 2.5f));
            Assert.That(DreamHouse.RoofAt(DreamHouse.RidgeZ), Is.EqualTo(DreamHouse.RidgeHeight).Within(0.01f), "the ridge");
            Assert.That(DreamHouse.RoofAt(DreamHouse.HubZ0), Is.EqualTo(DreamHouse.KneeWall).Within(0.01f), "the eaves: a sloped ceiling");
        }

        [Test]
        public void TheAtticIsAVerticalLoopTheMovementCanDo()
        {
            var h = Build();
            var d = new MoveDef();
            var boxes = LabCourseTests.WorldOf(h.course, null);
            LabPiece Find(string n) => h.course.pieces.Find(p => p.name == n);
            // Up: the stairs climb from Theo's room to the attic's floor, walkable (under the slope limit).
            var stairs = Find("The attic stairs");
            float rise = Math.Abs(stairs.rampTopStart - stairs.rampTopEnd), run = stairs.size.Z;
            Assert.That(stairs.rampTopStart, Is.EqualTo(DreamHouse.AtticFloor).Within(0.01f), "they reach the attic");
            Assert.That(stairs.rampTopEnd, Is.EqualTo(DreamHouse.Ground).Within(0.01f), "from the floor");
            Assert.That(Math.Atan2(rise, run) * 180.0 / Math.PI, Is.LessThan(d.slopeLimit - 2f), "steep, but walkable");
            // Their foot has a door from Theo's room, and the way in under the low ceiling fits a person.
            var door = Find("Door (Theo | the attic stairs)");
            Assert.That(door.Min.Y - DreamHouse.Ground, Is.GreaterThan(d.standHeight + 0.2f));
            // Headroom all the way up (the slot is open to the attic over the stairs, the roof is high there).
            for (float z = DreamHouse.StairZ0 + 0.4f; z < DreamHouse.StairZ1 - 0.3f; z += 0.2f)
            {
                float t = (z - stairs.Min.Z) / run;
                float top = stairs.rampTopStart + (stairs.rampTopEnd - stairs.rampTopStart) * t;
                float x = (DreamHouse.StairX0 + DreamHouse.StairX1) * 0.5f;
                Assert.That(boxes.CapsuleFits(new Vector3(x, top + 0.08f, z), 0.3f, d.standHeight), Is.True, $"headroom on the stairs at z {z:0.0}");
            }
            // Down: the hatch over the hall is open from the attic's floor to the hall's floor.
            float hx = (DreamHouse.HatchX0 + DreamHouse.HatchX1) * 0.5f, hz = (DreamHouse.HatchZ0 + DreamHouse.HatchZ1) * 0.5f;
            Assert.That(boxes.CastDown(new Vector3(hx, DreamHouse.AtticFloor + 1f, hz), 0.3f, 6f, out var hit), Is.True, "the hatch is open");
            Assert.That(hit.point.Y, Is.EqualTo(DreamHouse.Ground).Within(0.05f), "through the hatch to the hall's floor");
            // The attic has room to stand along its middle, and the dormer.
            Assert.That(boxes.CapsuleFits(new Vector3(0f, DreamHouse.AtticFloor + 0.05f, DreamHouse.RidgeZ), 0.38f, d.standHeight), Is.True, "standing under the ridge");
            Assert.That(boxes.CapsuleFits(new Vector3(2.2f, DreamHouse.AtticFloor + 0.05f, 80.5f), 0.38f, d.standHeight), Is.True, "standing at the top of the stairs, under the dormer");
            // The observatory: the roof cut and raised over it, so it's high all the way across, even by its walls; a double
            // jump's peak fits under its ceiling, and the ceiling stops anything higher.
            float peak = d.jumpHeight + d.doubleJumpHeight;
            foreach (float z in new[] { DreamHouse.HubZ0 + 0.75f, DreamHouse.RidgeZ, DreamHouse.HubZ1 - 0.75f })
                Assert.That(boxes.CapsuleFits(new Vector3(-4.6f, DreamHouse.AtticFloor + peak, z), 0.38f, d.standHeight), Is.True, $"a double jump fits in the observatory at z {z:0.0}");
            Assert.That(DreamHouse.ObsHeight, Is.GreaterThan(DreamHouse.RidgeHeight + 0.5f), "higher than the rest of the attic");
            Assert.That(boxes.CapsuleFits(new Vector3(-4.6f, DreamHouse.AtticFloor + DreamHouse.ObsHeight - 1.0f, DreamHouse.RidgeZ), 0.38f, d.standHeight), Is.False, "its ceiling stops a jump");
            Assert.That(boxes.CapsuleFits(new Vector3(-5.2f, DreamHouse.AtticFloor + 0.35f, 78f), 0.38f, d.standHeight), Is.True, "standing on the observatory's platform");
            // A jump in the attic stays inside (the roof's solid steps).
            Assert.That(boxes.CapsuleFits(new Vector3(0f, DreamHouse.AtticFloor + 2.6f, 73f), 0.38f, d.standHeight), Is.False, "the roof stops a jump near the eaves");
            // The stored things wall the attic into two, and the only way through on foot is the gap hung with sheets: the
            // observatory is a room you find, not the far end of one big floor.
            float wallX = -2.55f;
            for (float z = DreamHouse.HubZ0 + 0.5f; z < DreamHouse.HubZ1 - 0.4f; z += 0.25f)
            {
                bool gap = z > 77.4f && z < 79.6f;
                if (z > 76.9f && z < 80.1f && !gap) continue;   // the gap's edges
                Assert.That(boxes.CapsuleFits(new Vector3(wallX, DreamHouse.AtticFloor + 0.05f, z), 0.3f, 1.0f), Is.EqualTo(gap),
                    gap ? $"the way through the sheets at z {z:0.00}" : $"the wall of stored things at z {z:0.00}");
            }
        }

        [Test]
        public void TheosSightStopsAtWallsCeilingsAndTheAttic()
        {
            var h = Build();
            var w = h.StealthWorld();
            var F = DreamHouse.Ground;
            var eye = new Vector3(0f, 1.3f, 0f);
            // In the same room: clear. Through a wall into the next room: blocked.
            Assert.That(w.LineClear(new Vector3(5.2f, F, 73.4f) + eye, new Vector3(6.5f, F, 76.8f) + eye), Is.True, "across the living room");
            Assert.That(w.LineClear(new Vector3(-1f, F, 73.4f) + eye, new Vector3(-3f, F, 82.4f) + eye), Is.False, "the kitchen can't see into the parents' room");
            Assert.That(w.LineClear(new Vector3(-2.5f, F, 79f) + eye, new Vector3(4.5f, F, 81.6f) + eye), Is.False, "the hall can't see into Theo's room through the wall");
            // Over the half wall (the pass-through): clear at eye height, blocked at the knee.
            Assert.That(w.LineClear(new Vector3(1f, F, 76f) + eye, new Vector3(4f, F, 76f) + eye), Is.True, "over the pass-through");
            Assert.That(w.LineClear(new Vector3(1f, F + 0.5f, 76f), new Vector3(4f, F + 0.5f, 76f)), Is.False, "not through the half wall");
            // The attic: Theo on the main floor never sees her up there except through the open hatch and stairwell.
            var atticPoint = new Vector3(4.5f, DreamHouse.AtticFloor + 1.2f, 77f);
            foreach (var n in h.nodes)
                Assert.That(w.LineClear(n + eye, atticPoint), Is.False, $"from {n} into the attic");
            // The hiding spots' insides can't be seen from Theo's counting place.
            foreach (var s in h.spots)
                if (s.kind == HideKind.Closet)
                    Assert.That(w.LineClear(h.theoStart + eye, s.Center), Is.False, s.name + " can't be seen into from the table");
        }

        [Test]
        public void NothingToWalkToOrTakeIsInsideASolidBox()
        {
            var h = Build();
            var boxes = LabCourseTests.WorldOf(h.course, null);
            foreach (var n in h.nodes) Assert.That(boxes.CapsuleFits(n + new Vector3(0f, 0.05f, 0f), 0.3f, 1.2f), Is.True, "node " + n);
            foreach (var s in h.spots) Assert.That(boxes.CapsuleFits(s.Approach + new Vector3(0f, 0.05f, 0f), 0.3f, 1.2f), Is.True, s.name + " check point");
            foreach (var m in h.memories) Assert.That(boxes.CapsuleFits(m.standAt + new Vector3(0f, 0.04f, 0f), 0.38f, 1.7f), Is.True, m.name + ": where she stands");
            foreach (var p in h.openFloor) Assert.That(boxes.CapsuleFits(p + new Vector3(0f, 0.04f, 0f), 0.38f, 1.7f), Is.True, "open floor " + p);
            Assert.That(boxes.CapsuleFits(h.theoStart + new Vector3(0f, 0.04f, 0f), 0.3f, 1.2f), Is.True, "Theo at the table");
            Assert.That(boxes.CapsuleFits(h.kitchenStart + new Vector3(0f, 0.04f, 0f), 0.38f, 1.7f), Is.True, "where she arrives");
        }

        [Test]
        public void TheHouseHasLoopsAndEveryDoorFitsAPerson()
        {
            var h = Build();
            var d = new MoveDef();
            var world = h.StealthWorld();
            var g = h.Graph(world);
            // Loops: the kitchen to the living room through the archway, or round through the hall; the pantry's two doors.
            int Near(float x, float z) => g.Nearest(new Vector3(x, DreamHouse.Ground, z));
            var path = new List<int>();
            int kitchen = Near(-1f, 73.4f), living = Near(5.2f, 73.4f), hall = Near(-2.5f, 79f), pantryHall = Near(-4.7f, 79.4f);
            Assert.That(g.FindPath(kitchen, living, path), Is.True);
            Assert.That(path.Count, Is.LessThanOrEqualTo(3), "through the archway: a short way");
            Assert.That(g.FindPath(kitchen, hall, path) && g.FindPath(hall, living, path), Is.True, "and round by the hall");
            Assert.That(g.FindPath(Near(-5.3f, 77.6f), pantryHall, path), Is.True);
            // Every door's header leaves room to walk under it (the cupboard under the stairs is a low door: crouch).
            foreach (var p in h.course.pieces)
            {
                if (p.Max.Z < DreamHouse.HubZ0 - 1f || p.Min.Z > DreamHouse.HubZ1 + 1f) continue;   // the house, not the escape
                if (!p.name.StartsWith("Door") && !p.name.StartsWith("Archway") && !p.name.StartsWith("Pantry door")) continue;
                Assert.That(p.Min.Y - DreamHouse.Ground, Is.GreaterThan(d.standHeight + 0.2f), p.name);
            }
            var cupboard = h.course.pieces.Find(p => p.name.StartsWith("Cupboard under the stairs"));
            Assert.That(cupboard.Min.Y - DreamHouse.Ground, Is.InRange(d.slideHeight + 0.1f, d.standHeight), "the cupboard's low door: crouch in");
            // Theo's drawing is on top of his bookcase: climbing his desk reaches it (the desk is a mantle).
            var desk = h.course.pieces.Find(p => p.name == "Theo's desk");
            Assert.That(desk.Max.Y - DreamHouse.Ground, Is.InRange(d.mantleMinHeight, d.mantleMaxHeight), "the desk mantles from the floor");
            var drawing = h.memories.Find(m => m.id == "memory_drawing");
            Assert.That(drawing.standAt.Y, Is.EqualTo(desk.Max.Y).Within(0.01f), "she stands on the desk to take it");
        }

        static float Flat(Vector3 a, Vector3 b) => (float)Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z));

        /// <summary>Its stand point is somewhere she can stand (the capsule fits, on a surface), with her body centre within the
        /// pick-up radius.</summary>
        static bool CanReach(BoxWorld w, HouseMemory m)
        {
            var feet = m.standAt + new Vector3(0f, 0.02f, 0f);
            if (Vector3.Distance(feet + new Vector3(0f, 0.9f, 0f), m.position) > 1.05f) return false;
            if (!w.CapsuleFits(feet + new Vector3(0f, 0.04f, 0f), 0.38f, 1.7f)) return false;
            return w.CastDown(feet + new Vector3(0f, 0.3f, 0f), 0.2f, 1f, out var hit) && Math.Abs(hit.point.Y - m.standAt.Y) < 0.06f;
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
            foreach (var at in h.openFloor)
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
