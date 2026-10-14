using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using JamKit.Skyline;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    /// <summary>
    /// The layout lint (task F3): the three measured rules and the door checks on small synthetic levels that fail each rule and
    /// pass it, then on the real Food Court. The real arena does NOT pass yet: <see cref="TheRealArenaFindingsAreExactlyTheDocumentedOnes"/>
    /// pins what the lint finds today, and <see cref="TheProposedNudgesClearTheirFindings"/> shows, on a patched copy of the data,
    /// which edits (in F1's and F2's files, which F3 does not touch) clear which finding. When the data absorbs a nudge, shrink
    /// <see cref="Known"/>; when it is empty, delete the pinning test and assert <c>Is.Empty</c> on <c>ArenaLint.Arena().problems</c>.
    /// </summary>
    public class ArenaLintTests
    {
        // ---------------------------------------------------------------- synthetic levels

        static FloorSolid Slab(string id, float x0, float x1, float z0, float z1, float top = 0f) =>
            new FloorSolid { id = id, role = "floor", box = new CoverBox { min = new Vector3(x0, top - 1f, z0), max = new Vector3(x1, top, z1) } };

        static FloorSolid Wall(string id, float x0, float x1, float z0, float z1, float h = 4f, string role = "wall") =>
            new FloorSolid { id = id, role = role, box = new CoverBox { min = new Vector3(x0, 0f, z0), max = new Vector3(x1, h, z1) } };

        static SpawnDoor Shutter(string id, float x, float z, float yaw = 0f) =>
            new SpawnDoor { id = id, kind = DoorKind.ShopShutter, position = new Vector3(x, 0f, z), yawDeg = yaw };

        static SocketDef Socket(string id, float x, float y, float z, SocketGroup g = SocketGroup.Pit) =>
            new SocketDef { id = id, group = g, position = new Vector3(x, y, z) };

        static List<FloorSolid> Open() => new List<FloorSolid> { Slab("floor", -30f, 30f, -30f, 30f) };

        /// <summary>A 2.4 m wide, 14 m long corridor of walls from z 0 northward; closed at its far end unless asked.</summary>
        static List<FloorSolid> WithCorridor(bool closed)
        {
            var l = Open();
            l.Add(Wall("corr-w", -1.6f, -1.2f, 0f, 14f));
            l.Add(Wall("corr-e", 1.2f, 1.6f, 0f, 14f));
            if (closed) l.Add(Wall("corr-end", -1.6f, 1.6f, 14f, 14.4f));
            return l;
        }

        static bool Found(LintResult r, string text) => r.problems.Any(p => p.Contains(text));

        // ---- rule 1: exits

        [Test]
        public void ExitsAreVertexDisjointWalksOutOfTheRadiusCappedAtTwo()
        {
            var open = ArenaNav.Build(Open(), 2f);
            Assert.That(ArenaLint.Exits(open, open.NodeAt(Vector3.Zero), 10f), Is.EqualTo(2), "the middle of an open floor");
            Assert.That(ArenaLint.Exits(open, open.NodeAt(new Vector3(-30f, 0f, -30f)), 10f), Is.EqualTo(2), "a corner of an open floor still has two ways out");
            var closet = ArenaNav.Build(WithCorridor(true), 2f);
            Assert.That(ArenaLint.Exits(closet, closet.NodeAt(new Vector3(0f, 0f, 12f)), 10f), Is.EqualTo(1), "the end of a closet");
            Assert.That(ArenaLint.Exits(closet, closet.NodeAt(new Vector3(0f, 0f, 4f)), 10f), Is.EqualTo(1), "near its mouth: one way out, the other leads in");
            var through = ArenaNav.Build(WithCorridor(false), 2f);
            Assert.That(ArenaLint.Exits(through, through.NodeAt(new Vector3(0f, 0f, 8f)), 10f), Is.EqualTo(2), "a corridor open at both ends");
            Assert.That(ArenaLint.Exits(open, -1, 10f), Is.EqualTo(0));
            Assert.Throws<ArgumentNullException>(() => ArenaLint.Exits(null, 0, 10f));
        }

        [Test]
        public void ADeadEndClosetFailsRule1AndAThroughCorridorAndAnOpenFloorPass()
        {
            var open = ArenaLint.Run(Open(), new SpawnDoor[0], new SocketDef[0]);
            Assert.That(open.problems, Is.Empty, string.Join("\n", open.problems));
            Assert.That(open.failedExits, Is.EqualTo(0));
            Assert.That(open.minExits, Is.EqualTo(2));

            var closed = ArenaLint.Run(WithCorridor(true), new SpawnDoor[0], new SocketDef[0]);
            Assert.That(closed.failedExits, Is.GreaterThanOrEqualTo(5), "the closet's nodes");
            Assert.That(Found(closed, "rule 1"), Is.True);
            Assert.That(closed.minExits, Is.EqualTo(1));

            var through = ArenaLint.Run(WithCorridor(false), new SpawnDoor[0], new SocketDef[0]);
            Assert.That(through.failedExits, Is.EqualTo(0), string.Join("\n", through.problems));
        }

        // ---- rule 2: seen share

        [Test]
        public void SeenShareCountsTheDoorsInSightWithinRange()
        {
            var doors = new List<SpawnDoor> { Shutter("a", 10f, 0f), Shutter("b", 10f, 10f), Shutter("c", -10f, 0f), Shutter("d", 100f, 0f) };
            var eye = new Vector3(0f, 1.6f, 0f);
            Assert.That(ArenaLint.SeenShare(new CoverBox[0], eye, doors), Is.EqualTo(0.75f).Within(1e-4f), "d is beyond 60 m");
            Assert.That(ArenaLint.SeenShare(new CoverBox[0], eye, doors, 200f), Is.EqualTo(1f).Within(1e-4f));
            var wall = new[] { new CoverBox { min = new Vector3(4f, 0f, -20f), max = new Vector3(5f, 6f, 20f) } };
            Assert.That(ArenaLint.SeenShare(wall, eye, doors), Is.EqualTo(0.25f).Within(1e-4f), "a wall hides a and b");
            Assert.That(ArenaLint.SeenShare(wall, eye, new SpawnDoor[0]), Is.EqualTo(0f));
            Assert.That(ArenaLint.SeenShare(wall, eye, null), Is.EqualTo(0f));
        }

        [Test]
        public void ADoorsTargetIsItsMouthNotItsFace()
        {
            var south = Shutter("s", 5f, 18f, 180f);
            var t = ArenaLint.DoorTarget(south);
            Assert.That(t.Y, Is.EqualTo(1.5f).Within(1e-4f));
            Assert.That(t.Z, Is.EqualTo(17.65f).Within(1e-3f), "0.35 m out along the way it faces (south)");
            Assert.That(t.X, Is.EqualTo(5f).Within(1e-3f));
            var vent = new SpawnDoor { id = "v", kind = DoorKind.SkylightVent, position = new Vector3(1f, 13.6f, 2f) };
            Assert.That(ArenaLint.DoorTarget(vent), Is.EqualTo(vent.position));
        }

        static List<SpawnDoor> SixDoors() => new List<SpawnDoor>
        {
            Shutter("w1", -20f, 0f), Shutter("w2", -10f, 5f), Shutter("w3", -10f, -5f),
            Shutter("e1", 20f, 0f), Shutter("e2", 10f, 5f), Shutter("e3", 10f, -5f),
        };

        [Test]
        public void ARoomThatSeesEveryDoorFailsRule2AndTwoRoomsThatEachSeeHalfPass()
        {
            var hall = new List<FloorSolid> { Slab("floor", -40f, 40f, -10f, 10f) };
            var open = ArenaLint.Run(hall, SixDoors(), new SocketDef[0]);
            Assert.That(open.failedSight, Is.GreaterThan(100), "every point sees all six doors");
            Assert.That(open.worstSeenShare, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(Found(open, "rule 2"), Is.True);

            var split = new List<FloorSolid>(hall) { Wall("divider", -0.2f, 0.2f, -10f, 10f) };
            var two = ArenaLint.Run(split, SixDoors(), new SocketDef[0]);
            Assert.That(two.failedSight, Is.EqualTo(0), string.Join("\n", two.problems));
            Assert.That(two.worstSeenShare, Is.EqualTo(0.5f).Within(1e-4f), "each side sees its three");
        }

        // ---- rule 3: circle exposure

        [Test]
        public void ACircleInTheOpenIsExposedFromEveryBearingAndOneAtTheEndOfAClosetIsNot()
        {
            var open = ArenaLint.Run(Open(), new SpawnDoor[0], new[] { Socket("middle", 0f, 0f, 0f) });
            Assert.That(open.circleExposure[0], Is.EqualTo(1f).Within(1e-4f));
            Assert.That(open.circleSectors[0], Is.EqualTo(8));
            Assert.That(open.failedCircles, Is.EqualTo(0));

            var closet = ArenaLint.Run(WithCorridor(true), new SpawnDoor[0], new[] { Socket("end", 0f, 0f, 10f) });
            Assert.That(closet.circleExposure[0], Is.LessThan(FloorRules.MinCircleExposure));
            Assert.That(closet.circleSectors[0], Is.LessThanOrEqualTo(1), "only along the closet's axis");
            Assert.That(closet.failedCircles, Is.EqualTo(1));
            Assert.That(Found(closet, "rule 3"), Is.True);
        }

        [Test]
        public void ACircleNeedsTwoExposingSectorsAtLeastNinetyDegreesApart()
        {
            // a long wall to the south of the circle and a long wall to the west leave a 90 degree quadrant (north to east): the
            // bearings at its two edges are exactly 90 degrees apart, so it passes; close a wall to the north and it cannot
            var corner = Open();
            corner.Add(Wall("south", -30f, 30f, -4f, -3.6f));
            corner.Add(Wall("west", -4f, -3.6f, -30f, 30f));
            var quadrant = ArenaLint.Run(corner, new SpawnDoor[0], new[] { Socket("corner", 0f, 0f, 0f) });
            Assert.That(quadrant.circleSectors[0], Is.GreaterThanOrEqualTo(2));
            var shut = new List<FloorSolid>(corner) { Wall("north", -30f, 30f, 3.6f, 4f) };
            var pocket = ArenaLint.Run(shut, new SpawnDoor[0], new[] { Socket("pocket", 0f, 0f, 0f) });
            Assert.That(pocket.circleExposure[0], Is.LessThan(quadrant.circleExposure[0]));
            Assert.That(pocket.circleSectors[0], Is.LessThanOrEqualTo(1), "open to the east only");
            Assert.That(pocket.failedCircles, Is.EqualTo(1));
        }

        // ---- the extra checks

        [Test]
        public void ADoorTooCloseToAWallFailsItsBodyCheckAndMovedItPasses()
        {
            var level = Open();
            level.Add(Wall("tower", 20f, 20.4f, -30f, 30f, 16f));
            var near = new SpawnDoor { id = "vent-near", kind = DoorKind.SkylightVent, position = new Vector3(19.9f, 13.6f, 0f), yawDeg = 270f };
            var bad = ArenaLint.Run(level, new[] { near }, new SocketDef[0]);
            Assert.That(bad.failedDoors, Is.EqualTo(1));
            Assert.That(bad.problems.Any(p => p.StartsWith("door vent-near") && p.Contains("tower")), Is.True, string.Join("\n", bad.problems));
            var moved = new SpawnDoor { id = "vent-near", kind = DoorKind.SkylightVent, position = new Vector3(19.3f, 13.6f, 0f), yawDeg = 270f };
            Assert.That(ArenaLint.Run(level, new[] { moved }, new SocketDef[0]).failedDoors, Is.EqualTo(0));
            // a floor-care hatch 0.1 m from a wall: its 0.5 m body is inside it
            var hatch = new SpawnDoor { id = "hatch-near", kind = DoorKind.ServiceHatch, position = new Vector3(19.9f, 0f, 5f), yawDeg = 270f };
            Assert.That(ArenaLint.Run(level, new[] { hatch }, new SocketDef[0]).failedDoors, Is.EqualTo(1));
            Assert.That(ArenaLint.DoorBodyRadius(hatch), Is.EqualTo(SkylineEnemies.Find(SkylineEnemies.FloorCareId).bodyRadius));
            Assert.That(ArenaLint.DoorBodyRadius(near), Is.EqualTo(SkylineEnemies.Find(SkylineEnemies.DroneId).bodyRadius));
            Assert.That(ArenaLint.DoorBodyRadius(new SpawnDoor { kind = DoorKind.RailEnd }), Is.EqualTo(SkylineEnemies.Find(SkylineEnemies.GondolaId).bodyRadius));
        }

        [Test]
        public void AShutterNeedsAWalkableMouthAndIsFineInFrontOfItsShop()
        {
            var level = Open();
            level.Add(Wall("shop", -5f, 5f, 18f, 24f));
            var ok = Shutter("shutter-ok", 0f, 18f, 180f);
            Assert.That(ArenaLint.Run(level, new[] { ok }, new SocketDef[0]).failedDoors, Is.EqualTo(0));
            // a column 1.1 m from the face and a pilaster each side: the body fits (barely) but no node is within reach of the mouth
            var blocked = new List<FloorSolid>(level) { Wall("column", -0.4f, 0.4f, 16.1f, 16.9f), Wall("pilaster-w", -4f, -3.4f, 17.4f, 18f), Wall("pilaster-e", 3.4f, 4f, 17.4f, 18f) };
            var r = ArenaLint.Run(blocked, new[] { ok }, new SocketDef[0]);
            Assert.That(r.problems.Any(p => p.StartsWith("door shutter-ok")), Is.True, string.Join("\n", r.problems));
        }

        [Test]
        public void ADoorUnderOrOverACircleOnAnotherLevelIsANoteNotAProblem()
        {
            var level = Open();
            level.Add(Slab("deck", -10f, 10f, -10f, 10f, 6f));
            var r = ArenaLint.Run(level, new[] { Shutter("under", 1f, 1f) }, new[] { Socket("high", 0f, 6f, 0f, SocketGroup.High) });
            Assert.That(r.notes.Any(n => n.Contains("door under") && n.Contains("high")), Is.True, string.Join("\n", r.notes));
            Assert.That(r.problems.Any(p => p.StartsWith("door under")), Is.False);
            var far = ArenaLint.Run(level, new[] { Shutter("far", 20f, 20f) }, new[] { Socket("high", 0f, 6f, 0f, SocketGroup.High) });
            Assert.That(far.notes, Is.Empty);
        }

        [Test]
        public void SurfacesTheSpecCallsRunnableMustBe()
        {
            var level = Open();
            level.Add(new FloorSolid { id = "facade-pane-0-0", role = "facade-pane", runnable = false, glass = true, box = new CoverBox { min = new Vector3(30f, 0f, 0f), max = new Vector3(30.4f, 4f, 2.8f) } });
            level.Add(new FloorSolid { id = "pit-wall-x", role = "rim", runnable = true, box = new CoverBox { min = new Vector3(-30f, -4f, 0f), max = new Vector3(-29.6f, 0f, 4f) } });
            var r = ArenaLint.Run(level, new SpawnDoor[0], new SocketDef[0]);
            Assert.That(r.problems.Any(p => p.StartsWith("runnable: facade-pane-0-0")), Is.True);
            Assert.That(r.problems.Any(p => p.Contains("pit-wall-x")), Is.False);
        }

        [Test]
        public void IslandsOfMoreThanFourNodesAreProblemsAndLedgesAreNot()
        {
            var cut = new List<FloorSolid> { Slab("a", -10f, 10f, -10f, 10f), Slab("b", 20f, 30f, -10f, 10f) };
            var r = ArenaLint.Run(cut, new SpawnDoor[0], new SocketDef[0]);
            Assert.That(r.islandNodes, Is.GreaterThan(4));
            Assert.That(r.problems.Any(p => p.StartsWith("nav:")), Is.True);
            var ledge = new List<FloorSolid>(Open()) { new FloorSolid { id = "landing", role = "deck", box = new CoverBox { min = new Vector3(4f, 0f, -4f), max = new Vector3(10f, 2.8f, 4f) } } };
            var ok = ArenaLint.Run(ledge, new SpawnDoor[0], new SocketDef[0]);
            Assert.That(ok.islandNodes, Is.EqualTo(0), "a jump and a mantle climb a 2.8 m landing");
            Assert.That(ok.exempt, Is.GreaterThan(0), "its top is left out of rules 1 and 2");
            Assert.That(ok.problems.Any(p => p.StartsWith("nav:") || p.StartsWith("rule 1")), Is.False, string.Join("\n", ok.problems));
        }

        [Test]
        public void BlockersAreEverythingButGlassAndTheGround()
        {
            var solids = new List<FloorSolid>
            {
                Slab("floor", -10f, 10f, -10f, 10f), Wall("wall", 0f, 1f, 0f, 5f),
                new FloorSolid { id = "pane", role = "facade-pane", glass = true, box = new CoverBox { min = new Vector3(5f, 0f, 0f), max = new Vector3(5.4f, 4f, 3f) } },
                new FloorSolid { id = "ramp", role = "ramp", ramp = true, rampTopStart = 0f, rampTopEnd = -4f, rampAlongX = true, box = new CoverBox { min = new Vector3(-8f, -4f, -2f), max = new Vector3(-2f, 0f, 2f) } },
            };
            var b = ArenaLint.BlockersOf(solids);
            Assert.That(b.Any(x => x.min.X < -9f), Is.False, "no ground slab");
            Assert.That(b.Count(x => x.min.Z == 0f && x.max.Z == 5f), Is.EqualTo(1), "the wall");
            Assert.That(b.Any(x => x.min.X == 5f), Is.False, "no glass");
            Assert.That(b.Count(x => x.min.X >= -8.001f && x.max.X <= -1.999f), Is.GreaterThanOrEqualTo(2), "the ramp as stepped slices");
            Assert.That(b.Where(x => x.min.X >= -8.001f && x.max.X <= -1.999f).All(x => x.max.Y <= 0.001f), Is.True, "never a wedge above the surface");
        }

        // ---------------------------------------------------------------- the real arena

        static readonly Lazy<LintResult> Real = new Lazy<LintResult>(() => ArenaLint.Arena());

        /// <summary>What the lint finds on the arena today, as a category and the number of points or items. Each carries the
        /// edit that clears it (verified on a patched copy by <see cref="TheProposedNudgesClearTheirFindings"/>, except the pit
        /// and rule 2 lines, which are open design calls).</summary>
        static readonly string[] Known =
        {
            // door bodies (F2's RepossessSites.Doors): move six vents and two hatches (a ninth, shutter-n3, was blocked by the column
            // at x 0 north; the column rows have since moved in the working tree and the finding is optional here)
            "doors: 8",
            // F1's FrontDoorWidth 3 -> 4
            "rule 3: inside-n2 inside-n4 inside-s2 inside-s4",
            // F1: delete alcove-back-w and alcove-back-e (the corridor stubs beside the arrival cab join the alcove)
            "rule 1 corridor stubs: 4",
            // open: prop clusters and the corner west of the chute in the pit
            "rule 1 pit: 7",
            // open: 14 of the 27 doors are roof vents that every open point sees (the count of points moves with every column or
            // pilaster edit, so only its presence and the worst share are pinned)
            "rule 2",
        };

        static string Category(string problem)
        {
            if (problem.StartsWith("door ")) return "doors";
            if (problem.StartsWith("rule 3")) return "rule 3";
            if (problem.StartsWith("rule 1") && problem.Contains("[corridor")) return "rule 1 corridor stubs";
            if (problem.StartsWith("rule 1")) return "rule 1 pit";
            if (problem.StartsWith("rule 2")) return "rule 2";
            return problem;
        }

        static int Points(string problem)
        {
            var m = Regex.Match(problem, @": (\d+) point\(s\)");
            return m.Success ? int.Parse(m.Groups[1].Value) : 1;
        }

        static Dictionary<string, int> Tally(LintResult r)
        {
            var t = new Dictionary<string, int>();
            foreach (var p in r.problems)
            {
                string c = Category(p);
                t[c] = (t.ContainsKey(c) ? t[c] : 0) + Points(p);
            }
            return t;
        }

        [Test]
        public void TheRealArenaLintIsFastAndMeasuresWhatItShould()
        {
            var r = Real.Value;
            Assert.That(r.seconds, Is.LessThan(10.0), "the plan's budget");
            Assert.That(r.standable, Is.InRange(900, 1100));
            Assert.That(r.exempt, Is.InRange(10, 30), "the arrival cab, the exit lift and a couple of stray nodes");
            Assert.That(r.islandNodes, Is.LessThanOrEqualTo(4));
            var sockets = RepossessSites.Sockets();
            for (int i = 0; i < sockets.Length; i++)
                if (sockets[i].group != SocketGroup.Inside)
                    Assert.That(r.circleExposure[i], Is.GreaterThanOrEqualTo(FloorRules.MinCircleExposure), sockets[i].id);
            Assert.That(r.circleSectors[Array.FindIndex(sockets, s => s.id == "high-bridge")], Is.EqualTo(8), "the bridge circle is seen from every side");
        }

        [Test]
        public void TheRealArenaFindingsAreExactlyTheDocumentedOnes()
        {
            var r = Real.Value;
            var tally = Tally(r);
            var got = new List<string>();
            int shutterN3 = r.problems.Count(p => p.StartsWith("door shutter-n3"));
            if (tally.ContainsKey("doors")) got.Add("doors: " + (tally["doors"] - shutterN3));
            if (tally.ContainsKey("rule 3")) got.Add("rule 3: " + string.Join(" ", r.problems.Where(p => p.StartsWith("rule 3")).Select(p => p.Split(' ')[4]).OrderBy(x => x)));
            if (tally.ContainsKey("rule 1 corridor stubs")) got.Add("rule 1 corridor stubs: " + tally["rule 1 corridor stubs"]);
            if (tally.ContainsKey("rule 1 pit")) got.Add("rule 1 pit: " + tally["rule 1 pit"]);
            if (tally.ContainsKey("rule 2")) got.Add("rule 2");
            foreach (var p in r.problems) if (Category(p) == p) got.Add("OTHER: " + p);
            Assert.That(got, Is.EqualTo(Known.ToList()),
                "the lint's findings changed. If a nudge landed in F1/F2, delete its line from Known; if a new finding appeared, read it:\n" + string.Join("\n", r.problems));
            Assert.That(r.failedDoors, Is.InRange(8, 9));
            Assert.That(r.failedCircles, Is.EqualTo(4));
            Assert.That(r.failedExits, Is.EqualTo(11));
            Assert.That(r.failedSight, Is.GreaterThan(100));
            Assert.That(r.worstSeenShare, Is.InRange(0.7f, 0.85f));
        }

        [Test]
        public void TheRealArenaNotesTheFloorCareShutterUnderTheSouthWestCircle()
        {
            var r = Real.Value;
            Assert.That(r.notes, Has.Count.EqualTo(1), string.Join("\n", r.notes));
            Assert.That(r.notes[0], Does.Contain("shutter-s1").And.Contain("high-south-west"));
        }

        // ---- the proposed nudges, verified on patched copies of the data

        static FloorSolid Clone(FloorSolid s) => new FloorSolid
        {
            id = s.id, role = s.role, box = s.box, runnable = s.runnable, glass = s.glass,
            ramp = s.ramp, rampAlongX = s.rampAlongX, rampTopStart = s.rampTopStart, rampTopEnd = s.rampTopEnd,
        };

        static List<SpawnDoor> NudgedDoors()
        {
            var l = RepossessSites.Doors().Select(d => new SpawnDoor { id = d.id, kind = d.kind, position = d.position, yawDeg = d.yawDeg }).ToList();
            foreach (var d in l)
            {
                var p = d.position;
                switch (d.id)
                {
                    case "vent-corridor-n": p.Z = 26.8f; break;
                    case "vent-corridor-s": p.Z = -26.8f; break;
                    case "vent-leg-n": case "vent-leg-s": p.X = -34.3f; break;
                    case "vent-promenade-n": case "vent-promenade-s": p.X = 34.3f; break;
                    case "hatch-corridor-n": p.Z = 26f; break;
                    case "hatch-corridor-s": p.Z = -26f; break;
                }
                d.position = p;
            }
            return l;
        }

        static List<FloorSolid> NudgedSolids(bool column = true, bool frontDoors = true, bool alcove = true, bool landing = false)
        {
            var l = new List<FloorSolid>();
            foreach (var s0 in RepossessArena.Solids())
            {
                if (column && s0.role == "column" && s0.box.min.Z > 0f && Math.Abs((s0.box.min.X + s0.box.max.X) * 0.5f) < 1f) continue;       // the column in front of shutter-n3, if one stands there
                if (alcove && (s0.id == "alcove-back-w" || s0.id == "alcove-back-e")) continue;
                var s = Clone(s0);
                if (frontDoors && s.id.StartsWith("shopfront-") && (s.id.EndsWith("-l") || s.id.EndsWith("-r") || s.id.EndsWith("-lintel")))
                {
                    bool walkIn = s.id.Contains("-n2") || s.id.Contains("-n4") || s.id.Contains("-s2") || s.id.Contains("-s4");
                    if (walkIn)
                    {
                        float cx = (s.id.Contains("n2") || s.id.Contains("s2")) ? -10.4f : 10.4f, half = 2f;     // FrontDoorWidth 4
                        var b = s.box;
                        if (s.id.EndsWith("-lintel")) { b.min.X = cx - half; b.max.X = cx + half; }
                        else if (s.id.EndsWith("-l")) b.max.X = cx - half;
                        else b.min.X = cx + half;
                        s.box = b;
                    }
                }
                l.Add(s);
            }
            if (landing)
                l.Add(new FloorSolid { id = "facade-landing", role = "deck", box = new CoverBox { min = new Vector3(30.5f, 0f, 9f), max = new Vector3(RepossessArena.MaxX, 2.8f, 14.8f) } });
            return l;
        }

        [Test]
        public void TheProposedNudgesClearTheirFindings()
        {
            var doorsOnly = ArenaLint.Arena(RepossessArena.Solids(), NudgedDoors());
            Assert.That(doorsOnly.problems.Where(p => p.StartsWith("door")).All(p => p.StartsWith("door shutter-n3")), Is.True, "moving the eight doors leaves at most shutter-n3 (the column in front of it)");

            var all = ArenaLint.Arena(NudgedSolids(), NudgedDoors());
            Assert.That(all.failedDoors, Is.EqualTo(0), string.Join("\n", all.problems.Where(p => p.StartsWith("door"))));
            Assert.That(all.failedCircles, Is.EqualTo(0), "front doorways 4 m wide expose the shop circles from two bearings");
            var sockets = RepossessSites.Sockets();
            for (int i = 0; i < sockets.Length; i++)
                Assert.That(all.circleExposure[i], Is.GreaterThanOrEqualTo(FloorRules.MinCircleExposure), sockets[i].id);
            Assert.That(all.problems.Any(p => p.StartsWith("rule 1") && p.Contains("[corridor")), Is.False, "the corridor stubs open onto the alcove");
            Assert.That(all.problems.Any(p => p.StartsWith("nav:") || p.StartsWith("runnable:") || p.StartsWith("circle:")), Is.False);
            var left = Tally(all);
            Assert.That(left.Keys.OrderBy(k => k), Is.EqualTo(new[] { "rule 1 pit", "rule 2" }), "only the open design calls are left: " + string.Join("\n", all.problems));
            Assert.That(left["rule 1 pit"], Is.EqualTo(7));
        }

        [Test]
        public void EachNudgeAloneClearsOnlyItsOwnFinding()
        {
            var column = ArenaLint.Arena(NudgedSolids(true, false, false), RepossessSites.Doors());
            Assert.That(column.problems.Any(p => p.StartsWith("door shutter-n3")), Is.False, "without the column the shutter has room");
            Assert.That(column.failedCircles, Is.EqualTo(4));
            var doors = ArenaLint.Arena(NudgedSolids(false, true, false), RepossessSites.Doors());
            Assert.That(doors.failedCircles, Is.EqualTo(0));
            Assert.That(doors.failedDoors, Is.EqualTo(Real.Value.failedDoors));
            var alcove = ArenaLint.Arena(NudgedSolids(false, false, true), RepossessSites.Doors());
            Assert.That(alcove.failedExits, Is.LessThan(Real.Value.failedExits));
            Assert.That(alcove.problems.Any(p => p.Contains("[corridor")), Is.False);
        }

        [Test]
        public void ALandingAgainstTheFacadeAddsNoFindingBeyondTheKnownOnes()
        {
            var r = ArenaLint.Arena(NudgedSolids(landing: true), NudgedDoors());
            Assert.That(r.problems.Any(p => p.StartsWith("nav:")), Is.False, "the landing is a ledge, not an island");
            Assert.That(Tally(r).Keys.OrderBy(k => k), Is.EqualTo(new[] { "rule 1 pit", "rule 2" }), string.Join("\n", r.problems));
        }

        [Test]
        public void RuleTwoUnderOtherReadings()
        {
            // as the plan words it (27 doors, 14 of them roof vents every open point sees) it fails on well over a hundred points, worst about 78%
            Assert.That(Real.Value.failedSight, Is.GreaterThan(100));
            // leaving the vents out of the count: 13 doors on the ground and the rail ends; the promenade still sees 9 of them
            var noVents = ArenaLint.Arena(RepossessArena.Solids(), RepossessSites.Doors(), new LintOptions { ventsCount = false });
            Assert.That(noVents.failedSight, Is.InRange(1, Real.Value.failedSight / 2), "still fails, on far fewer points");
            Assert.That(noVents.worstSeenShare, Is.InRange(0.6f, 0.8f));
            Assert.That(Real.Value.worstSeenShare, Is.LessThan(0.85f), "a limit of 85% would pass the arena as it stands");
        }

        [Test]
        public void RuleThreeUnderOtherViewerDistances()
        {
            // the plan puts the viewers 8 to 30 m from the circle's centre; a walk-in shop is 5.2 m deep and its corridor 3.5 m wide,
            // so nobody stands 8 m from its centre behind it. From 4 m the shop circles are exposed from the ring and the corridor
            var near = ArenaLint.Arena(RepossessArena.Solids(), RepossessSites.Doors(), new LintOptions { viewerMin = 4f });
            Assert.That(near.failedCircles, Is.EqualTo(0));
        }

        [Test]
        public void ALintOfTheSameDataTwiceGivesTheSameAnswer()
        {
            var a = ArenaLint.Arena();
            Assert.That(a.problems, Is.EqualTo(Real.Value.problems));
            Assert.That(a.circleExposure, Is.EqualTo(Real.Value.circleExposure));
        }
    }
}
