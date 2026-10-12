using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using JamKit.DreamEscape;
using JamKit.Movement;
using JamKit.Stealth;
using JamKit.Tests.Movement;
using NUnit.Framework;

namespace JamKit.Tests.DreamEscape
{
    /// <summary>Night 2's house (the redesign, docs/redesign/house.md): the geometry (no two solids overlap; every room
    /// closed in; doors she and Theo fit through; stairs at walkable slopes), the size (two floors, a basement, the attic,
    /// the garage, the yard), on foot (she can walk from the table to every memory, up and down every stair), Theo's map
    /// (one connected graph over every floor), his search (he counts, peeks, searches room by room over the whole house,
    /// hears, checks hiding places, sees her and finds her), and the round's rules (the catch's cost, three catches end it).</summary>
    public class DreamHouseTests
    {
        static DreamHouse Build() => DreamHouse.Build();

        // =====================================================================================================
        // Geometry
        // =====================================================================================================

        [Test]
        public void NoTwoSolidsOverlap()
        {
            var h = Build();
            var hits = HouseLint.Check(h.parts);
            foreach (var hit in hits.Take(40)) TestContext.Out.WriteLine(HouseLint.Describe(h.parts, hit));
            Assert.That(hits.Count, Is.EqualTo(0), "interpenetrating solids (first 40 above)");
        }

        [Test]
        public void TheHouseHasTwoFloorsABasementAnAtticAGarageAndAYard()
        {
            var h = Build();
            var storeys = h.rooms.Where(r => !r.outside).Select(r => r.storey).Distinct().ToList();
            Assert.That(storeys, Is.EquivalentTo(new[] { 0, 1, 2, 3 }), "basement, ground, upper, attic");
            foreach (var id in new[] { "garage", "yard", "treehouse", "observatory", "rec", "workshop", "kitchen", "dining", "living", "theo", "parents", "guest" })
                Assert.That(h.Room(id), Is.Not.Null, id);
            float area = h.rooms.Where(r => !r.outside).Sum(r => r.Area);
            TestContext.Out.WriteLine($"{h.rooms.Count} rooms, {area:0} m2 indoors, {h.parts.Count} parts, {h.spots.Count} hiding spots, {h.nodes.Count} walking points");
            Assert.That(area, Is.GreaterThan(400f), "a whole house, not a few small rooms");
        }

        [Test]
        public void StairsAreWalkable()
        {
            var h = Build();
            foreach (var s in h.stairs)
            {
                float run = s.alongX ? s.rect.x1 - s.rect.x0 : s.rect.z1 - s.rect.z0;
                float angle = (float)(Math.Atan2(Math.Abs(s.topY - s.bottomY), run) * 180.0 / Math.PI);
                TestContext.Out.WriteLine($"{s.name}: {angle:0.0} degrees");
                Assert.That(angle, Is.LessThan(new MoveDef().slopeLimit - 2f), s.name);
                float width = s.alongX ? s.rect.z1 - s.rect.z0 : s.rect.x1 - s.rect.x0;
                Assert.That(width, Is.GreaterThanOrEqualTo(0.85f), s.name + " is wide enough");
            }
        }

        [Test]
        public void DoorsFitHerAndTheo()
        {
            var h = Build();
            foreach (var d in h.doors)
            {
                float w = d.b - d.a, ht = d.y1 - d.y0;
                Assert.That(w, Is.GreaterThanOrEqualTo(0.79f), d.name);
                if (d.name.Contains("Low") || d.name.Contains("Cupboard") || d.name.Contains("Treehouse")) continue;
                Assert.That(ht, Is.GreaterThanOrEqualTo(1.95f), d.name + " is tall enough to walk through");
            }
        }

        [Test]
        public void EveryRoomHasLight()
        {
            var h = Build();
            foreach (var r in h.rooms)
            {
                if (r.outside || r.id == "understairs" || r.id == "atticstairs") continue;
                Assert.That(h.lamps.Any(l => r.Contains(l.position + new Vector3(0f, -0.5f, 0f)) || r.Contains(new Vector3(l.position.X, r.floorY + 0.5f, l.position.Z))), Is.True, r.name + " has a lamp");
            }
        }

        // =====================================================================================================
        // On foot
        // =====================================================================================================

        public static BoxWorld WorldOf(DreamHouse h)
        {
            var w = new BoxWorld();
            foreach (var part in h.parts)
            {
                var p = part.piece;
                if (!p.solid || part.look == HouseLook.Roof && p.kind == LabPieceKind.Ramp) continue;
                if (p.kind == LabPieceKind.Ramp)
                {
                    float bottom = Math.Min(p.rampTopStart, p.rampTopEnd) - 0.3f;
                    if (part.rampAlongX) w.RampX(p.Min.X, p.Max.X, p.Min.Z, p.Max.Z, p.rampTopStart, p.rampTopEnd, bottom);
                    else w.Ramp(p.Min.X, p.Max.X, p.Min.Z, p.Max.Z, p.rampTopStart, p.rampTopEnd, bottom);
                }
                else w.Add(p.center, p.size, p.runnable);
            }
            return w;
        }

        /// <summary>Walks her (the movement sim, walking pace) along Theo's graph from one point to another: true when she
        /// gets within <paramref name="arrive"/> of it.</summary>
        public static bool Walk(DreamHouse h, BoxWorld world, WaypointGraph g, Vector3 from, Vector3 to, out string log, float arrive = 0.7f, float seconds = 120f)
        {
            var sim = new MoveSim(world, new MoveDef());
            var driver = new MoveDriver(sim);
            driver.Teleport(from + new Vector3(0f, 0.02f, 0f), 0f, Vector3.Zero);
            var path = new List<int>();
            int a = NearestOnFloor(h, from, null), b = NearestOnFloor(h, to, h.StealthWorld());
            log = "";
            if (!g.FindPath(a, b, path)) { log = "no path"; return false; }
            var pts = path.Select(i => g[i]).ToList();
            pts.Add(to);
            int k = 0;
            const float dt = 1f / 60f;
            for (float t = 0f; t < seconds; t += dt)
            {
                var p = sim.Position;
                var target = pts[k];
                var d = new Vector2(target.X - p.X, target.Z - p.Z);
                if (d.Length() < (k == pts.Count - 1 ? arrive : 0.45f) && Math.Abs(target.Y - p.Y) < 1.0f)
                {
                    k++;
                    if (k >= pts.Count) return true;
                    continue;
                }
                float yaw = (float)(Math.Atan2(d.X, d.Y) * 180.0 / Math.PI);
                // Crouch through the low doors (the cupboard, the treehouse).
                bool low = h.doors.Any(dr => dr.y1 - dr.y0 < 1.6f && Vector2.Distance(new Vector2(p.X, p.Z), dr.alongX ? new Vector2((dr.a + dr.b) * 0.5f, dr.c) : new Vector2(dr.c, (dr.a + dr.b) * 0.5f)) < 1.6f && Math.Abs(p.Y - dr.y0) < 0.5f);
                driver.Frame(dt, new MoveInput { move = new Vector2(0f, 1f), yaw = yaw, crouchHeld = low });
                if (p.Y < -20f) { log = $"fell at {p}"; return false; }
            }
            log = $"stuck at {sim.Position} heading for point {k}/{pts.Count} {pts[k]}";
            return false;
        }

        static int NearestOnFloor(DreamHouse h, Vector3 p, IStealthWorld clear)
        {
            var room = h.RoomAt(p + new Vector3(0f, 0.1f, 0f));
            int best = -1;
            float bd = float.MaxValue;
            for (int i = 0; i < h.nodes.Count; i++)
            {
                var n = h.nodes[i].p;
                float dy = Math.Abs(n.Y - p.Y);
                if (dy > 1.6f) continue;
                float d = Vector3.Distance(n, p) + (room != null && h.nodes[i].room != room.id ? 20f : 0f);
                if (clear != null && Math.Abs(n.Y - p.Y) < 0.3f && !DreamHouse.WalkClear(clear, n, p, 0.3f)) d += 10f;
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        [Test]
        public void SheCanWalkFromHerChairToEveryMemoryAndEveryPlaceItCanBeMovedTo()
        {
            var h = Build();
            var world = WorldOf(h);
            var g = h.Graph(h.StealthWorld());
            var start = h.nadiaChair + new Vector3(0f, 0f, -1.0f);
            var fails = new List<string>();
            foreach (var m in h.memories)
            {
                var places = new List<Vector3> { m.standAt };
                places.AddRange(m.altStandAt);
                foreach (var stand in places)
                {
                    if (m.atTable && stand == m.standAt) continue;
                    bool ok = Walk(h, world, g, start, stand, out var log, 0.9f);
                    if (!ok) fails.Add($"{m.name} at {stand}: {log}");
                }
            }
            foreach (var f in fails) TestContext.Out.WriteLine(f);
            Assert.That(fails, Is.Empty);
        }

        [Test]
        public void EveryHidingSpotHasRoomForHerAndSheCanReachIt()
        {
            var h = Build();
            var world = WorldOf(h);
            var g = h.Graph(h.StealthWorld());
            var start = h.nadiaChair + new Vector3(0f, 0f, -1.0f);
            foreach (var s in h.spots)
            {
                var feet = new Vector3(s.cx, s.cy - s.sy * 0.5f, s.cz);
                Assert.That(h.Free(feet, 0.3f, Math.Min(1.0f, s.sy - 0.1f)), Is.True, s.name + ": room to stand (or crouch) in it");
                Assert.That(Walk(h, world, g, start, s.Approach, out var log, 0.9f), Is.True, s.name + "'s approach: " + log);
            }
        }

        // =====================================================================================================
        // Theo's map and his search
        // =====================================================================================================

        [Test]
        public void TheosMapIsOneGraphOverEveryFloor()
        {
            var h = Build();
            var g = h.Graph(h.StealthWorld());
            var seen = new bool[g.Count];
            var stack = new Stack<int>();
            stack.Push(0); seen[0] = true;
            while (stack.Count > 0) { int u = stack.Pop(); foreach (int v in g.Neighbours(u)) if (!seen[v]) { seen[v] = true; stack.Push(v); } }
            var lost = Enumerable.Range(0, g.Count).Where(i => !seen[i]).Select(i => h.nodes[i].room + " " + h.nodes[i].p).ToList();
            foreach (var l in lost) TestContext.Out.WriteLine("unreachable: " + l);
            Assert.That(lost, Is.Empty);
            foreach (var r in h.rooms)
            {
                if (r.id == "street" || r.id == "understairs" || r.id == "atticstairs") continue;
                Assert.That(h.nodes.Any(n => n.room == r.id), Is.True, r.name + " has walking points");
            }
        }

        static TheoBrain Theo(DreamHouse h, out StealthBoxWorld world, int seed = 10)
        {
            world = h.StealthWorld();
            var g = h.Graph(world);
            return new TheoBrain(h, world, g, null, seed);
        }

        [Test]
        public void TheoSearchesTheWholeHouseUpstairsAndDownAndOutside()
        {
            var h = Build();
            var theo = Theo(h, out _);
            theo.StartCounting(h.theoStart, h.theoCountYaw, 3f, 0);
            var visited = new HashSet<string>();
            var storeys = new HashSet<int>();
            const float dt = 1f / 30f;
            for (float t = 0f; t < 600f; t += dt)
            {
                theo.Tick(dt, default, default);
                var room = h.RoomAt(theo.Position + new Vector3(0f, 0.1f, 0f));
                if (room != null) { visited.Add(room.id); storeys.Add(room.outside ? 4 : room.storey); }
                if (theo.Sweeps >= 1) break;
            }
            TestContext.Out.WriteLine("visited: " + string.Join(", ", visited));
            Assert.That(storeys, Is.SupersetOf(new[] { 0, 1, 2, 3, 4 }), "every floor and the yard");
            var missing = h.rooms.Where(r => r.id != "street" && r.id != "understairs" && r.id != "atticstairs" && r.id != "frontgarden" && !visited.Contains(r.id)).Select(r => r.name).ToList();
            Assert.That(missing, Is.Empty, "rooms he never went into");
        }

        [Test]
        public void TheoFindsHerStandingInTheOpenAndItCostsAMemory()
        {
            var h = Build();
            var theo = Theo(h, out _);
            var round = new HideAndSeekRound(h);
            round.Collect(DreamHouseStory.MemPlaceCard, out _);
            round.Collect(DreamHouseStory.MemCalendar, out _);
            var her = h.openFloor[0];
            theo.StartCounting(h.theoStart, h.theoCountYaw, 2f, 0);
            var target = StealthTarget.At(her);
            float foundAt = -1f;
            const float dt = 1f / 30f;
            for (float t = 0f; t < 400f && foundAt < 0f; t += dt)
            {
                theo.Tick(dt, target, default);
                if (theo.Events.found) foundAt = t;
            }
            TestContext.Out.WriteLine($"found her in the living room after {foundAt:0.0} s");
            Assert.That(foundAt, Is.GreaterThan(0f), "he found her");
            var cost = round.Caught(her);
            Assert.That(cost.catchNumber, Is.EqualTo(1));
            Assert.That(cost.memoryTaken, Is.EqualTo(DreamHouseStory.MemCalendar), "the last memory she found");
            Assert.That(round.IsFound(DreamHouseStory.MemCalendar), Is.False, "it's no longer found");
            Assert.That(Vector3.Distance(round.PositionOf(DreamHouseStory.MemCalendar), h.Memory(DreamHouseStory.MemCalendar).position), Is.GreaterThan(3f), "hidden somewhere new");
            Assert.That(round.FoundCount, Is.EqualTo(1));
        }

        [Test]
        public void TheoOpensTheCupboardSheHidesIn()
        {
            var h = Build();
            var theo = Theo(h, out _);
            int spot = h.spots.FindIndex(s => s.name.Contains("pantry"));
            var sp = h.spots[spot];
            var feet = new Vector3(sp.cx, sp.cy - sp.sy * 0.5f, sp.cz);
            var target = StealthTarget.At(feet);
            target.hidingSpot = spot;
            theo.SetHerSpot(spot);
            theo.StartCounting(h.theoStart, h.theoCountYaw, 2f, 2);
            const float dt = 1f / 30f;
            bool found = false;
            int foundIn = -1;
            for (float t = 0f; t < 900f && !found; t += dt)
            {
                theo.Tick(dt, target, default);
                if (theo.Events.found) { found = true; foundIn = theo.Events.foundInSpot; }
            }
            Assert.That(found, Is.True, "he found her in the pantry");
            Assert.That(foundIn, Is.EqualTo(spot), "by opening it (he can't see into a cupboard)");
        }

        [Test]
        public void TheoHearsARunAndGoesToLook()
        {
            var h = Build();
            var theo = Theo(h, out _);
            theo.StartCounting(h.theoStart, h.theoCountYaw, 0.5f, 0);
            const float dt = 1f / 30f;
            for (float t = 0f; t < 1f; t += dt) theo.Tick(dt, default, default);
            var noise = StealthNoise.Make(new Vector3(-4.6f, 0f, 1.5f), 22f, NoiseKind.Landing);   // the living room
            theo.Tick(dt, default, noise);
            Assert.That(theo.Events.say, Is.EqualTo(TheoSay.Heard));
            Assert.That(theo.State, Is.EqualTo(TheoState.Investigating));
        }

        [Test]
        public void TheoPeeksWhileHeCountsAndSaysWhereHeSawHerGo()
        {
            var h = Build();
            bool saw = false;
            for (int seed = 1; seed < 40 && !saw; seed++)
            {
                var theo = Theo(h, out _, seed);
                theo.StartCounting(h.theoStart, h.theoCountYaw, 10f, 0);
                var target = StealthTarget.At(new Vector3(4.6f, 0f, 4.2f));   // the dining room's far side, by the kitchen opening
                const float dt = 1f / 30f;
                for (float t = 0f; t < 10f; t += dt)
                {
                    theo.Tick(dt, target, default);
                    if (theo.Events.say == TheoSay.SawYouGo) { saw = true; Assert.That(theo.Events.arg, Is.EqualTo(1), "the ground floor"); }
                    Assert.That(theo.Events.found, Is.False, "a peek never finds her");
                }
            }
            Assert.That(saw, Is.True, "with her in view he sees her when he peeks (some seed)");
        }

        /// <summary>The whole round in the fake world: she (the walker, sprinting) goes for the nearest memory she hasn't got,
        /// Theo seeks her with his senses, a catch costs what the rules say; it ends with every memory found and him finding
        /// her, or three catches. Prints how long it took.</summary>
        [TestCase(10)]
        [TestCase(22)]
        public void TheWholeRoundPlaysOut(int seed)
        {
            var h = Build();
            var world = WorldOf(h);
            var sw = h.StealthWorld();
            var g = h.Graph(sw);
            var theo = new TheoBrain(h, sw, g, null, seed);
            var round = new HideAndSeekRound(h);
            round.Collect(DreamHouseStory.MemPlaceCard, out _);
            var sim = new MoveSim(world, new MoveDef());
            var driver = new MoveDriver(sim);
            driver.Teleport(h.nadiaChair + new Vector3(0f, 0.02f, -1.0f), 0f, Vector3.Zero);
            var walker = new HouseWalker(h, sw, g);
            theo.StartCounting(h.theoStart, h.theoCountYaw, round.CountTime, 0);
            string goingFor = null;
            const float dt = 1f / 30f;
            float t = 0f, fleeing = 0f;
            var log = new List<string>();
            int catches = 0;
            for (; t < 900f && !round.Over; t += dt)
            {
                var p = sim.Position;
                // A sensible player: seen, she breaks away (to the reachable point furthest from him) for a few seconds.
                if (theo.Visibility > 0.05f && fleeing <= 0f && !round.AllFound)
                {
                    fleeing = 4f;
                    var far = h.nodes.Where(n => Math.Abs(n.p.Y - p.Y) < 0.3f && Vector3.Distance(n.p, p) < 12f).OrderByDescending(n => Vector3.Distance(n.p, theo.Position)).FirstOrDefault();
                    walker.Go(p, far.p);
                    goingFor = null;
                }
                if (fleeing > 0f) { fleeing -= dt; if (fleeing > 0f) { driver.Frame(dt, walker.Next(sim, dt)); var tg = StealthTarget.At(sim.Position); tg.speed = sim.HorizontalSpeed; theo.Tick(dt, tg, default); if (theo.Events.found) { var c0 = round.Caught(sim.Position); if (!c0.roundOver) theo.StartCounting(theo.Position, theo.Yaw, round.CountTime, round.Catches); } continue; } }
                if (goingFor == null || round.IsFound(goingFor) || walker.Arrived && !round.IsFound(goingFor) && Vector3.Distance(p, round.StandAtOf(goingFor)) > 1.5f || walker.Stuck)
                {
                    goingFor = null;
                    float best = float.MaxValue;
                    foreach (var m in h.memories)
                    {
                        if (round.IsFound(m.id)) continue;
                        float d = Vector3.Distance(p, round.StandAtOf(m.id)) + Math.Abs(p.Y - round.StandAtOf(m.id).Y) * 6f;
                        if (d < best) { best = d; goingFor = m.id; }
                    }
                    if (goingFor != null && !walker.Go(p, round.StandAtOf(goingFor))) TestContext.Out.WriteLine($"{t:0}s no way from {p} to {goingFor}");
                    else if (goingFor != null && log.Count < 60) log.Add($"{t:0}s at {p} going for {goingFor} ({h.RoomAt(p + new Vector3(0, 0.1f, 0))?.id})");
                }
                if (goingFor != null && walker.Arrived && Vector3.Distance(p, round.StandAtOf(goingFor)) < 1.2f)
                {
                    round.Collect(goingFor, out bool last);
                    if (last) theo.ComeFind(p);
                    goingFor = null;
                }
                driver.Frame(dt, walker.Next(sim, dt));
                var target = StealthTarget.At(sim.Position);
                target.speed = sim.HorizontalSpeed;
                theo.Tick(dt, target, default);
                if (round.AllFound && theo.State != TheoState.Coming && theo.State != TheoState.Celebrating) theo.ComeFind(sim.Position);
                if (theo.Events.found)
                {
                    var cost = round.Caught(sim.Position);
                    if (!cost.roundOver) { catches++; theo.StartCounting(theo.Position, theo.Yaw, round.CountTime, round.Catches); }
                }
            }
            foreach (var l in log) TestContext.Out.WriteLine(l);
            TestContext.Out.WriteLine($"seed {seed}: the round took {t:0} s ({round.End}), {round.Catches} catches, {round.FoundCount} of {round.Total} memories");
            Assert.That(round.Over, Is.True, "the round ends");
        }

        // =====================================================================================================
        // The round
        // =====================================================================================================

        [Test]
        public void ThreeCatchesEndTheRoundAndEachTakesTheLastMemory()
        {
            var h = Build();
            var r = new HideAndSeekRound(h);
            foreach (var id in new[] { DreamHouseStory.MemPlaceCard, DreamHouseStory.MemCalendar, DreamHouseStory.FactPhoto, DreamHouseStory.MemBadDream }) r.Collect(id, out _);
            Assert.That(r.Counter, Is.EqualTo("Memories 4 of 8"));
            var c1 = r.Caught(Vector3.Zero);
            Assert.That(c1.memoryTaken, Is.EqualTo(DreamHouseStory.MemBadDream));
            Assert.That(r.CountTime, Is.LessThan(HideAndSeekRound.CountSeconds[0]), "he counts less after a catch");
            var c2 = r.Caught(Vector3.Zero);
            Assert.That(c2.memoryTaken, Is.EqualTo(DreamHouseStory.FactPhoto));
            Assert.That(r.Over, Is.False);
            var c3 = r.Caught(Vector3.Zero);
            Assert.That(c3.roundOver, Is.True);
            Assert.That(r.End, Is.EqualTo(RoundEnd.CaughtThreeTimes));
            Assert.That(r.FoundCount, Is.EqualTo(2), "she goes into the battle with what she's holding");
        }

        [Test]
        public void ATakenMemoryCanBeFoundAgainWhereTheHouseMovedIt()
        {
            var h = Build();
            var r = new HideAndSeekRound(h);
            r.Collect(DreamHouseStory.MemRadio, out _);
            var c = r.Caught(new Vector3(3.6f, -2.8f, 1f));
            Assert.That(c.newRoom, Is.Not.EqualTo("workshop"));
            Assert.That(r.Collect(DreamHouseStory.MemRadio, out _), Is.True, "found again");
        }

        [Test]
        public void FindingEveryMemoryPlaysTheCreatorsLineAndHisFindingHerEndsTheRound()
        {
            var h = Build();
            var r = new HideAndSeekRound(h);
            bool last = false;
            foreach (var m in h.memories) { r.Collect(m.id, out bool l); last |= l; }
            Assert.That(last, Is.True);
            Assert.That(DreamHouseLines.AllFound(false), Does.StartWith("I'm not sure if there's anything left to find. I could let Theo find me here..."));
            Assert.That(DreamHouseLines.AllFound(true), Does.EndWith("it would be nice to see his smile again."));
            Assert.That(DreamHouseLines.AllFound(false), Does.EndWith("but what does it matter, when he's not even real?"));
            var c = r.Caught(Vector3.Zero);
            Assert.That(c.roundOver, Is.True);
            Assert.That(c.memoryTaken, Is.Null, "she let him: no cost");
            Assert.That(r.End, Is.EqualTo(RoundEnd.AllFoundAndFound));
        }

        [Test]
        public void TheMemoriesBecomeTheBattlesCards()
        {
            var cards = DreamHouseStory.EvidenceFor(new[] { DreamHouseStory.MemCalendar, DreamHouseStory.MemSaturdayPaper, DreamHouseStory.MemRadio, DreamHouseStory.MemPlaceCard }, false);
            Assert.That(cards, Is.EquivalentTo(new[] { DreamHouseStory.DedSaturdayOff, DreamHouseStory.DedSameSaturday, DreamHouseStory.MemPlaceCard }));
            Assert.That(DreamHouseStory.EvidenceFor(new[] { DreamHouseStory.MemCalendar }, false), Is.EquivalentTo(new[] { DreamHouseStory.MemCalendar }));
            Assert.That(DreamHouseStory.EvidenceFor(new[] { DreamHouseStory.MemCalendar }, true), Is.EquivalentTo(new[] { DreamHouseStory.DedSaturdayOff }));
        }

        [Test]
        public void TheStoryRunsInOrderAndThrownOutHardensLena()
        {
            var s = new DreamHouseStory();
            s.Restart();
            Assert.That(s.SitDown(), Is.True);
            Assert.That(s.StartHideAndSeek(), Is.True);
            Assert.That(s.RoundOver(), Is.True);
            Assert.That(s.AnswerTheo(TheoAnswer.Honest), Is.True);
            Assert.That(s.Choose(ArgumentOutcome.Unsure), Is.True);
            Assert.That(s.Thrown(), Is.True);
            Assert.That(s.Phase, Is.EqualTo(HousePhase.ThrownOut));
            Assert.That(s.Outcome, Is.EqualTo(ArgumentOutcome.Condemns), "thrown out, Lena's mind is made up");
            Assert.That(DreamHouseLines.TheoQuestion, Is.EqualTo("Is Dad going to be sad?"));
            Assert.That(DreamHouseLines.Street[0].text, Is.EqualTo("Morning, Nadia!"));
        }

        [Test]
        public void NoLineIsAPlaceholder()
        {
            var all = new List<HouseLine>();
            foreach (var f in typeof(DreamHouseLines).GetFields())
            {
                if (f.FieldType == typeof(HouseLine[])) all.AddRange((HouseLine[])f.GetValue(null));
                if (f.FieldType == typeof(HouseLine[][])) foreach (var a in (HouseLine[][])f.GetValue(null)) all.AddRange(a);
            }
            Assert.That(all.Count, Is.GreaterThan(30));
            foreach (var l in all)
            {
                Assert.That(l.text, Does.Not.Contain("placeholder").IgnoreCase);
                Assert.That(l.text, Does.Not.Contain("["));
            }
        }
    }
}
