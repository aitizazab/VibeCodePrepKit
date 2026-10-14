using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using JamKit.Skyline;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    /// <summary>Task F2: the floor's sockets, spawn doors, assets and clean-line routes (docs/SKYLINE-PLAN-FLOOR1.md). The tests
    /// that need F1's <c>RepossessArena</c> reach it by reflection, so this file compiles whether or not F1 has landed; they
    /// ignore themselves (with the reason) until the type exists, and run for real once it does.</summary>
    public class RepossessSitesTests
    {
        // ---- the arena, by reflection (F1) ----

        static readonly Type ArenaType = typeof(SocketDef).Assembly.GetType("JamKit.Skyline.RepossessArena");

        static void NeedArena()
        {
            if (ArenaType == null) Assert.Ignore("F1 (RepossessArena) has not landed yet; this check runs once it does.");
        }

        static bool TryGround(float x, float z, float fromY, out float y)
        {
            MethodInfo m = ArenaType.GetMethod("TryGroundY", BindingFlags.Public | BindingFlags.Static);
            Assert.That(m, Is.Not.Null, "RepossessArena.TryGroundY");
            var args = new object[] { x, z, fromY, 0f };
            bool ok = (bool)m.Invoke(null, args);
            y = (float)args[3];
            return ok;
        }

        static Vector3 ArenaArrival()
        {
            FieldInfo f = ArenaType.GetField("ArrivalPoint", BindingFlags.Public | BindingFlags.Static);
            Assert.That(f, Is.Not.Null, "RepossessArena.ArrivalPoint");
            return (Vector3)f.GetValue(null);
        }

        // ---- sockets ----

        [Test]
        public void TenSocketsInThreeGroupsOfThreeFourAndThree()
        {
            SocketDef[] all = RepossessSites.Sockets();
            Assert.That(all.Length, Is.EqualTo(10));
            Assert.That(RepossessSites.InGroup(SocketGroup.Pit).Length, Is.EqualTo(3));
            Assert.That(RepossessSites.InGroup(SocketGroup.Inside).Length, Is.EqualTo(4));
            Assert.That(RepossessSites.InGroup(SocketGroup.High).Length, Is.EqualTo(3));
            Assert.That(all.Select(s => s.id).Distinct().Count(), Is.EqualTo(10), "unique ids");
            Assert.That(all.Select(s => s.id), Is.EquivalentTo(new[]
            {
                "pit-kiosk", "pit-ne", "pit-sw", "inside-n2", "inside-n4", "inside-s2", "inside-s4",
                "high-bridge", "high-north-east", "high-south-west",
            }));
        }

        [Test]
        public void SocketsSitAtTheLayoutSheetsPoints()
        {
            var expect = new Dictionary<string, Vector3>
            {
                { "pit-kiosk", new Vector3(-2f, -1.5f, 0f) }, { "pit-ne", new Vector3(9f, -4f, 7f) },
                { "pit-sw", new Vector3(-13f, -4f, -7f) },
                { "inside-n2", new Vector3(-10.4f, 0f, 21f) }, { "inside-n4", new Vector3(10.4f, 0f, 21f) },
                { "inside-s2", new Vector3(-10.4f, 0f, -21f) }, { "inside-s4", new Vector3(10.4f, 0f, -21f) },
                { "high-bridge", new Vector3(8f, 6f, 0f) }, { "high-north-east", new Vector3(30f, 6f, 20f) },
                { "high-south-west", new Vector3(-22f, 6f, -20f) },
            };
            foreach (SocketDef s in RepossessSites.Sockets()) Assert.That(s.position, Is.EqualTo(expect[s.id]), s.id);
        }

        [Test]
        public void GroupsMatchTheIdPrefixes()
        {
            foreach (SocketDef s in RepossessSites.Sockets())
            {
                string prefix = s.group == SocketGroup.Pit ? "pit-" : s.group == SocketGroup.Inside ? "inside-" : "high-";
                Assert.That(s.id, Does.StartWith(prefix));
            }
        }

        [Test]
        public void CircleCentreIsExactlyTheSocketPositionWhichRepossessionAssumes()
        {
            foreach (SocketDef s in RepossessSites.Sockets())
                Assert.That(RepossessSites.CircleCenter(s), Is.EqualTo(s.position), s.id);
            Assert.Throws<ArgumentNullException>(() => RepossessSites.CircleCenter(null));
        }

        [Test]
        public void ARepossessionCircleHoldsTheFeetAtTheCircleCentreAndNotJustOutsideTheRadius()
        {
            var assets = RepossessSites.Assets();
            for (ulong seed = 0; seed < 20; seed++)
            {
                var r = new Repossession(assets, seed);
                foreach (JobEntry e in r.Sheet)
                {
                    Vector3 c = RepossessSites.CircleCenter(e.socket);
                    Assert.That(r.CircleAt(c), Is.GreaterThanOrEqualTo(0));
                    Assert.That(r.Sheet[r.CircleAt(c)], Is.SameAs(e));
                    Assert.That(r.CircleAt(c + new Vector3(FloorRules.CircleRadius + 0.05f, 0f, 0f)), Is.EqualTo(-1));
                    Assert.That(r.CircleAt(c + new Vector3(FloorRules.CircleRadius - 0.05f, 0f, 0f)), Is.EqualTo(r.CircleAt(c)));
                }
            }
        }

        [Test]
        public void TheConvenienceOverloadsDefaultToTheFloorsOwnSockets()
        {
            var all = RepossessSites.Sockets();
            for (ulong seed = 0; seed < 30; seed++)
            {
                SocketDef[] a = Repossession.Pick(seed);
                SocketDef[] b = Repossession.Pick(seed, all);
                Assert.That(a.Select(s => s.id), Is.EqualTo(b.Select(s => s.id)), "seed " + seed);
                Assert.That(a.Select(s => s.group), Is.EqualTo(new[] { SocketGroup.Pit, SocketGroup.Inside, SocketGroup.High }));
                var r = new Repossession(RepossessSites.Assets(), seed);
                Assert.That(r.Sheet.Count, Is.EqualTo(3));
                Assert.That(r.Sheet.Select(e => e.socket.id).OrderBy(x => x), Is.EqualTo(a.Select(s => s.id).OrderBy(x => x)));
            }
        }

        [Test]
        public void EverySocketGetsPickedBySomeSeed()
        {
            var seen = new HashSet<string>();
            for (ulong seed = 0; seed < 200; seed++) foreach (SocketDef s in Repossession.Pick(seed)) seen.Add(s.id);
            Assert.That(seen.Count, Is.EqualTo(10));
        }

        [Test]
        public void ReturnedArraysAreCopiesOfTheSameData()
        {
            SocketDef[] a = RepossessSites.Sockets();
            a[0] = null;
            Assert.That(RepossessSites.Sockets()[0], Is.Not.Null);
            Assert.That(RepossessSites.Sockets()[0], Is.SameAs(RepossessSites.Sockets()[0]));
            RepossessSites.Doors()[0] = null;
            Assert.That(RepossessSites.Doors()[0], Is.Not.Null);
            RepossessSites.Routes()[0].waypoints[0] = new Vector3(99f, 99f, 99f);
            Assert.That(RepossessSites.Routes()[0].waypoints[0].X, Is.Not.EqualTo(99f));
        }

        [Test]
        public void CirclesFitInsideTheirAreasWhenTheArenaExists()
        {
            NeedArena();
            foreach (SocketDef s in RepossessSites.Sockets())
            {
                Vector3 c = RepossessSites.CircleCenter(s);
                int total = 0, standable = 0;
                // 8 points on a ring of 1.5 m and 16 on one of 2.85 m: the disc, edge included, near the 3 m radius.
                for (int ring = 0; ring < 2; ring++)
                {
                    int n = ring == 0 ? 8 : 16;
                    float r = ring == 0 ? 1.5f : 2.85f;
                    for (int i = 0; i < n; i++)
                    {
                        double a = 2.0 * Math.PI * i / n;
                        float x = c.X + (float)(Math.Cos(a) * r), z = c.Z + (float)(Math.Sin(a) * r);
                        total++;
                        float y;
                        // Standable at the socket's height: ground at or just under the socket, within the circle's vertical reach.
                        if (TryGround(x, z, c.Y + 0.05f, out y) && Math.Abs(y - c.Y) <= Repossession.CircleHalfHeight) standable++;
                    }
                }
                // The bridge is 4 m wide and the circle 6 m: the 4 m strip across its middle is all there is to stand on (about 60%).
                float need = s.id == "high-bridge" ? 0.5f : 0.9f;
                Assert.That(standable / (float)total, Is.GreaterThanOrEqualTo(need), s.id + ": " + standable + " of " + total);
            }
        }

        [Test]
        public void EverySocketsOwnFloorPointIsStandableWhenTheArenaExists()
        {
            NeedArena();
            foreach (SocketDef s in RepossessSites.Sockets())
            {
                float y;
                // The kiosk's socket sits on a prop top (-1.5), not the pit floor: it must be the walkable top, not the floor under it.
                Assert.That(TryGround(s.position.X, s.position.Z, s.position.Y + 0.05f, out y), Is.True, s.id);
                Assert.That(y, Is.EqualTo(s.position.Y).Within(0.1f), s.id);
            }
        }

        // ---- doors ----

        [Test]
        public void TwentySevenDoorsWithUniqueIdsAndTheSheetsCounts()
        {
            SpawnDoor[] d = RepossessSites.Doors();
            Assert.That(d.Length, Is.EqualTo(27));
            Assert.That(d.Select(x => x.id).Distinct().Count(), Is.EqualTo(27));
            Assert.That(d.Count(x => x.kind == DoorKind.ShopShutter), Is.EqualTo(5));
            Assert.That(d.Count(x => x.kind == DoorKind.SkylightVent), Is.EqualTo(14));
            Assert.That(d.Count(x => x.kind == DoorKind.ServiceHatch), Is.EqualTo(6));
            Assert.That(d.Count(x => x.kind == DoorKind.RailEnd), Is.EqualTo(2));
        }

        [Test]
        public void DoorPositionsAreTheLayoutSheetsAndHeightsFitTheirKind()
        {
            foreach (SpawnDoor d in RepossessSites.Doors())
            {
                switch (d.kind)
                {
                    case DoorKind.SkylightVent: Assert.That(d.position.Y, Is.InRange(13f, 14f), d.id); break;
                    case DoorKind.ShopShutter: case DoorKind.ServiceHatch: Assert.That(d.position.Y, Is.EqualTo(0f), d.id); break;
                    case DoorKind.RailEnd:
                        Assert.That(d.position.X, Is.GreaterThan(35f), d.id + " is outside the façade");
                        Assert.That(d.position.Y, Is.EqualTo(3f), d.id);
                        Assert.That(Math.Abs(d.position.Z), Is.EqualTo(27f), d.id);
                        break;
                }
            }
            var vents = RepossessSites.Doors().Where(d => d.kind == DoorKind.SkylightVent).Select(d => (d.position.X, d.position.Z));
            Assert.That(vents.Take(6), Is.EquivalentTo(new[] { (-26f, 12f), (-26f, -12f), (26f, 12f), (26f, -12f), (-4f, 22f), (-4f, -22f) }), "the sheet's six");
            Assert.That(vents.Skip(6), Is.EquivalentTo(new[]
            {
                (10f, 26.85f), (10f, -26.85f), (-34.35f, 10f), (-34.35f, -10f), (34.35f, 10f), (34.35f, -10f), (-30f, 18f), (-30f, -18f),
            }), "the eight added for spawn coverage");
            var shutters = RepossessSites.Doors().Where(d => d.kind == DoorKind.ShopShutter).Select(d => (d.position.X, d.position.Z));
            Assert.That(shutters, Is.EquivalentTo(new[] { (-20.8f, 18f), (0f, 18f), (20.8f, 18f), (-20.8f, -18f), (20.8f, -18f) }));
        }

        static float Horizontal(Vector3 a, Vector3 b)
        {
            float dx = a.X - b.X, dz = a.Z - b.Z;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        // Across levels the plain distance counts: shutter S1 sits on the ring right under the south-west gallery socket.
        static float Distance3(Vector3 a, Vector3 b) => Vector3.Distance(a, b);

        [Test]
        public void NoDoorIsWithinSixMetresOfAnyCircleCentre()
        {
            foreach (SpawnDoor d in RepossessSites.Doors())
                foreach (SocketDef s in RepossessSites.Sockets())
                    Assert.That(Distance3(d.position, RepossessSites.CircleCenter(s)), Is.GreaterThanOrEqualTo(6f), d.id + " / " + s.id);
        }

        [Test]
        public void NoDoorIsInsideAWalkInShop()
        {
            // N2, N4, S2, S4 (layout sheet): x -15.6..-5.2 and 5.2..15.6, z 18..24 and -24..-18.
            foreach (SpawnDoor d in RepossessSites.Doors())
            {
                float ax = Math.Abs(d.position.X), az = Math.Abs(d.position.Z);
                bool inside = ax > 5.2f && ax < 15.6f && az > 18f && az < 24f;
                Assert.That(inside, Is.False, d.id);
            }
        }

        [Test]
        public void EveryDoorFacesIntoTheHall()
        {
            foreach (SpawnDoor d in RepossessSites.Doors())
            {
                Assert.That(d.yawDeg, Is.InRange(0f, 360f), d.id + " yaw is wrapped to 0..360");
                double r = d.yawDeg * Math.PI / 180.0;
                float fx = d.position.X + 2f * (float)Math.Sin(r), fz = d.position.Z + 2f * (float)Math.Cos(r);
                Assert.That(fx, Is.InRange(-35f, 35f), d.id + " two metres ahead is in the hall (x)");
                Assert.That(fz, Is.InRange(-28f, 28f), d.id + " two metres ahead is in the hall (z)");
                if (d.kind != DoorKind.ServiceHatch)
                {
                    // Shutters, vents and rail ends look toward the middle of the hall.
                    Assert.That(Horizontal(new Vector3(fx, 0f, fz), Vector3.Zero), Is.LessThan(Horizontal(d.position, Vector3.Zero)), d.id);
                }
            }
        }

        [Test]
        public void ShuttersFaceAlongTheRingAndRailEndsFaceTheGlass()
        {
            foreach (SpawnDoor d in RepossessSites.Doors())
            {
                if (d.kind == DoorKind.ShopShutter) Assert.That(d.yawDeg, Is.EqualTo(d.position.Z > 0f ? 180f : 0f), d.id);
                if (d.kind == DoorKind.RailEnd) Assert.That(d.yawDeg, Is.EqualTo(270f), d.id);
            }
        }

        [Test]
        public void DoorsForFollowsTheDoorRules()
        {
            Assert.That(RepossessSites.DoorsFor(SkylineEnemies.DroneId).Count(d => d.kind == DoorKind.ShopShutter), Is.EqualTo(5));
            Assert.That(RepossessSites.DoorsFor(SkylineEnemies.DroneId).Count(d => d.kind == DoorKind.SkylightVent), Is.EqualTo(14));
            Assert.That(RepossessSites.DoorsFor(SkylineEnemies.DroneId).Length, Is.EqualTo(19));
            Assert.That(RepossessSites.DoorsFor(SkylineEnemies.FloorCareId).Count(d => d.kind == DoorKind.ShopShutter), Is.EqualTo(5));
            Assert.That(RepossessSites.DoorsFor(SkylineEnemies.FloorCareId).Count(d => d.kind == DoorKind.ServiceHatch), Is.EqualTo(6));
            Assert.That(RepossessSites.DoorsFor(SkylineEnemies.FloorCareId).Length, Is.EqualTo(11));
            Assert.That(RepossessSites.DoorsFor(SkylineEnemies.GondolaId).Length, Is.EqualTo(2));
            Assert.That(RepossessSites.DoorsFor(SkylineEnemies.GondolaId).All(d => d.kind == DoorKind.RailEnd));
            Assert.That(RepossessSites.DoorsFor(SkylineEnemies.SpotlightId), Is.Empty);
            Assert.That(RepossessSites.DoorsFor("no-such-bot"), Is.Empty);
            Assert.That(RepossessSites.DoorsFor(null), Is.Empty);
        }

        [Test]
        public void NoVentIsAboveASocketsCircle()
        {
            foreach (SpawnDoor d in RepossessSites.Doors().Where(x => x.kind == DoorKind.SkylightVent))
                foreach (SocketDef s in RepossessSites.Sockets())
                    Assert.That(Horizontal(d.position, s.position), Is.GreaterThan(FloorRules.CircleRadius), d.id + " / " + s.id);
        }

        // ---- spawn coverage: the whole hall, against W2's SpawnBearings ----

        // A player's centre cannot be in the wall: the hall's interior is x -35..35, z -28..28 but the east/west walls and the
        // corridors' outer walls (z +-27.5) keep it inside x +-34.5, z +-27. Grid cells outside that are tested at the nearest
        // reachable point (no door pair can give a 90-degree spread to a point exactly in a corner of the box).
        static Vector3 Reachable(float x, float z) =>
            new Vector3(Math.Max(-34.5f, Math.Min(34.5f, x)), 0f, Math.Max(-27f, Math.Min(27f, z)));

        [Test]
        public void EveryPointOfTheHallGetsALegalSpawnChoiceForEveryWaveType()
        {
            const string D = SkylineEnemies.DroneId, F = SkylineEnemies.FloorCareId, G = SkylineEnemies.GondolaId;
            var doors = RepossessSites.Doors();
            var waves = new[]
            {
                new WaveDef { number = 1, enemies = new[] { D, D, D } },
                new WaveDef { number = 2, enemies = new[] { D, D, D, F } },
                new WaveDef { number = 3, enemies = new[] { D, D, F, F } },
                new WaveDef { number = 4, enemies = new[] { G, D, D } },
            };
            float[] yaws = { 0f, 72f, 144f, 216f, 288f };
            int total = 0;
            var refused = new List<string>();
            for (float x = -35f; x <= 35f; x += 2f)
                for (float z = -28f; z <= 28f; z += 2f)
                    foreach (float yaw in yaws)
                        foreach (WaveDef wave in waves)
                        {
                            var q = new SpawnQuery
                            {
                                player = Reachable(x, z), viewYawDeg = yaw, viewHalfFovDeg = 55f, wave = wave,
                                seed = (ulong)(total + 1), lastMeanBearing = (total % 7) * 51f - 180f,
                            };
                            total++;
                            if (!SpawnBearings.Choose(q, doors).ok)
                                refused.Add("(" + x + "," + z + ") yaw " + yaw + " wave " + wave.number);
                        }
            Assert.That(total, Is.EqualTo(36 * 29 * 5 * 4));
            Assert.That(refused, Is.Empty, refused.Count + " refused, first: " + string.Join("; ", refused.Take(8)));
        }

        // ---- assets ----

        [Test]
        public void ThreeDraftAssetsOfDistinctKinds()
        {
            AssetDef[] a = RepossessSites.Assets();
            Assert.That(a.Length, Is.EqualTo(3));
            Assert.That(a.Select(x => x.kind), Is.EquivalentTo(new[] { AssetKind.Mascot, AssetKind.MassageChair, AssetKind.ArcadeCabinet }));
            Assert.That(a.Select(x => x.id).Distinct().Count(), Is.EqualTo(3));
            foreach (AssetDef x in a)
            {
                Assert.That(x.id, Is.Not.Empty);
                Assert.That(x.jobSheetName, Is.Not.Empty);
                Assert.That(x.description, Is.Not.Empty);
            }
        }

        // ---- routes ----

        static readonly string[] RouteIds =
        {
            "arrival-pit-sw", "arrival-inside-n4", "arrival-high-bridge", "arrival-high-south-west", "arrival-high-north-east",
            "pit-to-exit-lift", "facade-wall-run",
        };

        [Test]
        public void TheSevenRoutesExist()
        {
            Assert.That(RepossessSites.Routes().Select(r => r.id), Is.EquivalentTo(RouteIds));
        }

        [Test]
        public void ArrivalRoutesStartAtTheArrivalPointAndEndOnTheirSocket()
        {
            var sockets = RepossessSites.Sockets().ToDictionary(s => s.id);
            var ends = new Dictionary<string, string>
            {
                { "arrival-pit-sw", "pit-sw" }, { "arrival-inside-n4", "inside-n4" }, { "arrival-high-bridge", "high-bridge" },
                { "arrival-high-south-west", "high-south-west" }, { "arrival-high-north-east", "high-north-east" },
            };
            foreach (RouteDef r in RepossessSites.Routes())
            {
                if (!ends.ContainsKey(r.id)) continue;
                Assert.That(r.waypoints[0], Is.EqualTo(new Vector3(0f, 0f, -21f)), r.id);
                Assert.That(r.waypoints[r.waypoints.Length - 1], Is.EqualTo(sockets[ends[r.id]].position), r.id);
            }
        }

        [Test]
        public void ArrivalPointMatchesTheArenasWhenItExists()
        {
            NeedArena();
            Assert.That(ArenaArrival(), Is.EqualTo(new Vector3(0f, 0f, -21f)));
        }

        [Test]
        public void ConsecutiveWaypointsAreWithinTwelveMetresUnlessAWallRunOrDoubleJumpCoversTheGap()
        {
            foreach (RouteDef r in RepossessSites.Routes())
                for (int i = 1; i < r.waypoints.Length; i++)
                {
                    MoveHintKind k = r.HintAt(i);
                    if (k == MoveHintKind.WallRun || k == MoveHintKind.DoubleJump) continue;
                    Assert.That(Vector3.Distance(r.waypoints[i - 1], r.waypoints[i]), Is.LessThanOrEqualTo(RepossessSites.MaxWaypointGap),
                        r.id + " waypoint " + i);
                }
        }

        [Test]
        public void HintsPointAtRealWaypointsAndEveryRouteIsNonTrivial()
        {
            foreach (RouteDef r in RepossessSites.Routes())
            {
                Assert.That(r.waypoints.Length, Is.GreaterThanOrEqualTo(2), r.id);
                foreach (MoveHint h in r.hints)
                {
                    Assert.That(h.atWaypoint, Is.InRange(1, r.waypoints.Length - 1), r.id);
                    Assert.That(Enum.IsDefined(typeof(MoveHintKind), h.kind), r.id);
                }
                Assert.That(r.hints.Select(h => h.atWaypoint).Distinct().Count(), Is.EqualTo(r.hints.Length), r.id + " one hint per waypoint");
                foreach (Vector3 w in r.waypoints)
                {
                    Assert.That(float.IsNaN(w.X) || float.IsNaN(w.Y) || float.IsNaN(w.Z), Is.False, r.id);
                    Assert.That(w.X, Is.InRange(-35f, 35f), r.id);
                    Assert.That(w.Z, Is.InRange(-28f, 28f), r.id);
                }
            }
        }

        [Test]
        public void TheFacadeRouteWallRunsNorthAlongTheGlassThenMantlesOntoTheNorthGalleryLip()
        {
            RouteDef r = RepossessSites.Routes().First(x => x.id == "facade-wall-run");
            Assert.That(Enumerable.Range(1, r.waypoints.Length - 1).Select(r.HintAt), Does.Contain(MoveHintKind.WallRun));
            Assert.That(Enumerable.Range(1, r.waypoints.Length - 1).Select(r.HintAt), Does.Contain(MoveHintKind.Mantle), "up onto the facade-landing");
            Assert.That(r.HintAt(r.waypoints.Length - 1), Is.EqualTo(MoveHintKind.DoubleJump), "from its top, a jump and a double jump put the deck lip within a mantle");
            Vector3 last = r.waypoints[r.waypoints.Length - 1];
            Assert.That(last.Y, Is.EqualTo(6f), "ends on the gallery deck");
            Assert.That(last.Z, Is.GreaterThan(15f), "past the lip at z 15");
            for (int i = 0; i < r.waypoints.Length; i++)
                if (r.HintAt(i) == MoveHintKind.WallRun) Assert.That(r.waypoints[i].X, Is.InRange(34f, 35f), "on the façade at x 35");
        }

        [Test]
        public void TheLiftRunEndsInsideTheExitLiftsFeetZone()
        {
            RouteDef r = RepossessSites.Routes().First(x => x.id == "pit-to-exit-lift");
            Vector3 last = r.waypoints[r.waypoints.Length - 1];
            Assert.That(last.X, Is.InRange(-31f, -27f));
            Assert.That(last.Z, Is.InRange(-2f, 2f));
            Assert.That(r.waypoints[0].Y, Is.EqualTo(-4f), "starts on the pit floor");
        }

        [Test]
        public void EveryStandingWaypointIsStandableWhenTheArenaExists()
        {
            NeedArena();
            foreach (RouteDef r in RepossessSites.Routes())
                for (int i = 0; i < r.waypoints.Length; i++)
                {
                    MoveHintKind k = r.HintAt(i);
                    if (k == MoveHintKind.WallRun || k == MoveHintKind.DoubleJump) continue;   // on a wall or in the air
                    Vector3 w = r.waypoints[i];
                    float y;
                    Assert.That(TryGround(w.X, w.Z, w.Y + 0.05f, out y), Is.True, r.id + " waypoint " + i + " " + w);
                    Assert.That(y, Is.EqualTo(w.Y).Within(0.3f), r.id + " waypoint " + i + " " + w);
                }
        }
    }
}
