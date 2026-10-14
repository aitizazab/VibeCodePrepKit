using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using JamKit.Skyline;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    /// <summary>Task W4: how door-spawned bots arrive and engage (docs/SKYLINE-PLAN-FLOOR1.md, D6). A drone from every skylight
    /// vent and a floor-care unit from every shutter and hatch go to work against the real arena data.</summary>
    public class BotArrivalTests
    {
        const float H = 1f / 60f;
        const float AreaMinX = -35f, AreaMaxX = 35f, AreaMinZ = -28f, AreaMaxZ = 28f;

        static ArenaNav _nav;
        static ArenaNav Nav => _nav ?? (_nav = ArenaNav.Build(RepossessArena.Solids()));
        static CoverBox[] _blockers;
        static CoverBox[] Blockers => _blockers ?? (_blockers = RepossessArena.SightBlockers());

        static Vector3 Stand(float x, float y, float z)
        {
            int n = Nav.NodeAt(new Vector3(x, y, z));
            Assert.That(n, Is.GreaterThanOrEqualTo(0), "no nav node near " + x + "," + y + "," + z);
            return Nav.PositionOf(n);
        }

        /// <summary>Spots of the hall a player really stands on: the four rings, the pit, both galleries, the bridge, the east promenade.</summary>
        static Vector3[] Spots() => new[]
        {
            Stand(0, 0, 14), Stand(0, 0, -14), Stand(-22, 0, -8), Stand(25, 0, -6), Stand(-8, -4, 2), Stand(8, -4, 6),
            Stand(10, 6, 20), Stand(22, 6, -20), Stand(8, 6, 0), Stand(31, 0, 0),
        };

        static bool Strictly(Vector3 p, CoverBox b) =>
            p.X > b.min.X && p.X < b.max.X && p.Y > b.min.Y && p.Y < b.max.Y && p.Z > b.min.Z && p.Z < b.max.Z;

        static SpawnDoor Door(string id) => RepossessSites.Doors().First(d => d.id == id);

        static float Flat(Vector3 a, Vector3 b) => (float)Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z));

        static Vector3 Chest(Vector3 feet) => feet + new Vector3(0f, EngagePoints.ChestHeight, 0f);

        // ------------------------------------------------------------------ the data

        [Test]
        public void StartPositions_are_clear_of_cover()
        {
            var drone = SkylineEnemies.MallCopDrone();
            var care = SkylineEnemies.FloorCareUnit();
            foreach (var d in RepossessSites.Doors())
            {
                if (FloorRules.DoorAllows(d.kind, drone.id))
                {
                    var p = BotArrival.ClearStart(drone, d, Blockers);
                    foreach (var b in Blockers) Assert.That(Strictly(p, b), Is.False, "drone start of " + d.id + " is inside cover");
                }
                if (FloorRules.DoorAllows(d.kind, care.id))
                {
                    var p = BotArrival.ClearStart(care, d, Blockers);
                    Assert.That(EngagePoints.BodyInside(p.X, p.Z, p.Y, 0f, Blockers), Is.False, "floor-care start of " + d.id + " is inside cover");
                }
            }
        }

        [Test]
        public void Applies_only_to_the_drone_and_the_floor_care_unit()
        {
            Assert.That(BotArrival.Applies(SkylineEnemies.MallCopDrone()), Is.True);
            Assert.That(BotArrival.Applies(SkylineEnemies.FloorCareUnit()), Is.True);
            Assert.That(BotArrival.Applies(SkylineEnemies.Gondola()), Is.False);
            Assert.That(BotArrival.Applies(SkylineEnemies.Spotlight()), Is.False);
            Assert.That(BotArrival.Applies(null), Is.False);
        }

        [Test]
        public void The_gondola_and_the_spotlight_do_not_use_the_arrival()
        {
            foreach (var def in new[] { SkylineEnemies.Gondola(), SkylineEnemies.Spotlight() })
            {
                var a = new BotArrival(def, Door("rail-n"), 5);
                Assert.That(a.Engaged, Is.True);
                var p = new Vector3(3f, 2f, 1f);
                Assert.That(a.Step(p, new Vector3(0, 0, 0), Blockers, H, AreaMinX, AreaMaxX, AreaMinZ, AreaMaxZ), Is.EqualTo(p));
            }
        }

        [Test]
        public void Bad_input_is_ignored()
        {
            var a = new BotArrival(SkylineEnemies.MallCopDrone(), Door("vent-c-n"), 3);
            var p = new Vector3(1, 2, 3);
            Assert.That(a.Step(p, Vector3.Zero, Blockers, 0f, AreaMinX, AreaMaxX, AreaMinZ, AreaMaxZ), Is.EqualTo(p));
            Assert.That(a.Step(p, Vector3.Zero, Blockers, float.NaN, AreaMinX, AreaMaxX, AreaMinZ, AreaMaxZ), Is.EqualTo(p));
            Assert.That(a.Step(p, new Vector3(float.NaN, 0, 0), Blockers, H, AreaMinX, AreaMaxX, AreaMinZ, AreaMaxZ), Is.EqualTo(p));
            Assert.That(a.Elapsed, Is.EqualTo(0f));
        }

        // ------------------------------------------------------------------ the engage points

        [Test]
        public void RepickSeconds_runs_from_six_to_nine()
        {
            Assert.That(EngagePoints.RepickSeconds(0f), Is.EqualTo(6f));
            Assert.That(EngagePoints.RepickSeconds(1f), Is.EqualTo(9f).Within(1e-5f));
            Assert.That(EngagePoints.RepickSeconds(0.5f), Is.EqualTo(7.5f).Within(1e-5f));
            Assert.That(EngagePoints.RepickSeconds(float.NaN), Is.EqualTo(6f));
            Assert.That(EngagePoints.RepickSeconds(-3f), Is.EqualTo(6f));
            Assert.That(EngagePoints.RepickSeconds(7f), Is.EqualTo(9f));
        }

        [Test]
        public void Drone_engage_point_is_in_the_ring_with_a_clear_line_at_hover_height()
        {
            var def = SkylineEnemies.MallCopDrone();
            foreach (var spot in Spots())
                for (int s = 0; s < 6; s++)
                {
                    float seed01 = s / 6f, now = s * 7.3f;
                    var p = EngagePoints.For(def, spot, Blockers, seed01, now);
                    float flat = Flat(p, spot);
                    Assert.That(flat, Is.InRange(EngagePoints.MinRange, EngagePoints.MaxRange), "ring at " + spot);
                    Assert.That(Vector3.Distance(p, Chest(spot)), Is.InRange(EngagePoints.MinRange, EngagePoints.MaxRange), "3D ring at " + spot);
                    Assert.That(p.Y, Is.EqualTo(spot.Y + EngagePoints.DroneAltitude).Within(1e-4f));
                    Assert.That(CoverGeometry.Blocked(p, Chest(spot), Blockers), Is.False, "line from " + p + " to " + spot);
                    Assert.That(EngagePoints.Inside(p, def.bodyRadius, Blockers), Is.False);
                }
        }

        [Test]
        public void Engage_point_is_deterministic_and_changes_with_the_period()
        {
            var def = SkylineEnemies.MallCopDrone();
            var spot = Stand(0, 0, 14);
            var a = EngagePoints.For(def, spot, Blockers, 0.4f, 1f);
            Assert.That(EngagePoints.For(def, spot, Blockers, 0.4f, 1f), Is.EqualTo(a));
            // inside one period the point stays; across periods it moves (some period in the next minute differs)
            float period = EngagePoints.RepickSeconds(0.4f);
            Assert.That(EngagePoints.For(def, spot, Blockers, 0.4f, period * 0.5f), Is.EqualTo(a));
            bool moved = false;
            for (int k = 1; k < 8; k++) if (EngagePoints.For(def, spot, Blockers, 0.4f, 1f + k * period) != a) moved = true;
            Assert.That(moved, Is.True);
            Assert.That(EngagePoints.For(def, spot, Blockers, 0.9f, 1f), Is.Not.EqualTo(a), "another bot picks another point");
        }

        [Test]
        public void Engage_point_falls_back_closer_when_the_far_ring_has_no_clear_line()
        {
            var def = SkylineEnemies.MallCopDrone();
            // inside a walk-in shop: nothing 12 m away sees in, so the point comes from a closer band and still sees the chest
            var shop = Stand(-10.4f, 0f, 21f);
            var p = EngagePoints.For(def, shop, Blockers, 0.3f, 0f);
            Assert.That(CoverGeometry.Blocked(p, Chest(shop), Blockers), Is.False);
            Assert.That(EngagePoints.Inside(p, def.bodyRadius, Blockers), Is.False);
            Assert.That(Flat(p, shop), Is.LessThan(EngagePoints.MaxRange));
        }

        [Test]
        public void Floor_care_engage_point_is_the_players_spot_or_beside_it()
        {
            var def = SkylineEnemies.FloorCareUnit();
            var spot = Stand(0, 0, 14);
            Assert.That(EngagePoints.For(def, spot, Blockers, 0.5f, 0f), Is.EqualTo(spot));
            // a player standing inside a piece of cover: the nearest free spot round it
            var boxed = new[] { new CoverBox { min = new Vector3(-1, 0, -1), max = new Vector3(1, 1.2f, 1) } };
            var p = EngagePoints.For(def, new Vector3(0, 0, 0), boxed, 0.5f, 0f);
            Assert.That(EngagePoints.BodyInside(p.X, p.Z, p.Y, def.bodyRadius, boxed), Is.False);
            Assert.That(Flat(p, Vector3.Zero), Is.LessThan(4f));
        }

        // ------------------------------------------------------------------ the drone's arrival

        sealed class DroneRun
        {
            public bool engaged, failed;
            public float seconds, travelled, maxStep;
            public Vector3 end;
            public string problem;
        }

        static DroneRun FlyDrone(SpawnDoor door, Vector3 player, ulong seed, float maxSeconds, IReadOnlyList<CoverBox> blockers = null)
        {
            blockers = blockers ?? Blockers;
            var def = SkylineEnemies.MallCopDrone();
            var arrival = new BotArrival(def, door, seed, Nav, blockers);
            var pos = arrival.Start;
            var r = new DroneRun();
            for (float t = 0f; t < maxSeconds && !arrival.Engaged; t += H)
            {
                var next = arrival.Step(pos, player, blockers, H, AreaMinX, AreaMaxX, AreaMinZ, AreaMaxZ);
                float step = Vector3.Distance(next, pos);
                r.maxStep = Math.Max(r.maxStep, step);
                r.travelled += step;
                if (step > BotArrival.DroneTopSpeed * H + 1e-3f) r.problem = "step " + step + " at t=" + t;
                foreach (var b in blockers)
                    if (Strictly(next, b) && r.problem == null) r.problem = "inside cover " + b.min + ".." + b.max + " at " + next;
                pos = next;
                r.seconds = t + H;
            }
            r.engaged = arrival.Engaged;
            r.failed = arrival.Failed;
            r.end = pos;
            return r;
        }

        [Test]
        public void A_drone_from_every_vent_reaches_an_engage_point_in_six_seconds_within_forty_metres()
        {
            var vents = RepossessSites.Doors().Where(d => d.kind == DoorKind.SkylightVent).ToArray();
            Assert.That(vents.Length, Is.EqualTo(14));
            var problems = new List<string>();
            foreach (var v in vents)
                foreach (var spot in Spots())
                {
                    var r = FlyDrone(v, spot, 77, 8f);
                    string tag = v.id + " -> " + spot;
                    if (!r.engaged || r.failed) { problems.Add(tag + ": not engaged in 8 s"); continue; }
                    if (r.seconds > 6f) problems.Add(tag + ": took " + r.seconds + " s");
                    if (r.travelled > 48f) problems.Add(tag + ": travelled " + r.travelled + " m");
                    if (r.problem != null) problems.Add(tag + ": " + r.problem);
                    float flat = Flat(r.end, spot);
                    if (flat < 12f || flat > 28f) problems.Add(tag + ": ends " + flat + " m away");
                    if (CoverGeometry.Blocked(r.end, Chest(spot), Blockers)) problems.Add(tag + ": no clear line at the end");
                    if (Math.Abs(r.end.Y - (spot.Y + EngagePoints.DroneAltitude)) > 0.05f) problems.Add(tag + ": ends at height " + r.end.Y);
                }
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [Test]
        public void A_drone_from_a_shop_shutter_reaches_its_engage_point_too()
        {
            var problems = new List<string>();
            foreach (var d in RepossessSites.Doors().Where(d => d.kind == DoorKind.ShopShutter))
                foreach (var spot in Spots())
                {
                    var r = FlyDrone(d, spot, 31, 8f);
                    string tag = d.id + " -> " + spot;
                    if (!r.engaged || r.failed) { problems.Add(tag + ": not engaged"); continue; }
                    if (r.problem != null) problems.Add(tag + ": " + r.problem);
                    if (CoverGeometry.Blocked(r.end, Chest(spot), Blockers)) problems.Add(tag + ": no clear line at the end");
                    float flat = Flat(r.end, spot);
                    if (flat < 12f || flat > 28f) problems.Add(tag + ": ends " + flat + " m away");
                }
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [Test]
        public void A_drone_for_a_player_inside_a_shop_still_engages_with_a_clear_line()
        {
            var shop = Stand(-10.4f, 0f, 21f);
            var r = FlyDrone(Door("vent-c-n"), shop, 5, 10f);
            Assert.That(r.engaged && !r.failed, Is.True, "engaged " + r.engaged + " failed " + r.failed);
            Assert.That(r.problem, Is.Null, r.problem);
            Assert.That(CoverGeometry.Blocked(r.end, Chest(shop), Blockers), Is.False);
        }

        [Test]
        public void With_no_cover_at_all_a_drone_flies_straight()
        {
            var none = new CoverBox[0];
            var spot = new Vector3(0, 0, 0);
            var door = Door("vent-w-n");
            var r = FlyDrone(door, spot, 9, 8f, none);
            Assert.That(r.engaged, Is.True);
            // near the straight distance to the point it ends at (the preference may pick a farther one, but never a detour)
            Assert.That(r.travelled, Is.LessThan(Vector3.Distance(door.position, r.end) * 1.02f + 0.01f));
        }

        [Test]
        public void A_drone_is_not_engaged_until_it_gets_there()
        {
            var def = SkylineEnemies.MallCopDrone();
            var door = Door("vent-w-n");
            var a = new BotArrival(def, door, 12, Nav, Blockers);
            var spot = Stand(25, 0, -6);
            var pos = a.Start;
            Assert.That(a.Engaged, Is.False);
            for (int i = 0; i < 20; i++) pos = a.Step(pos, spot, Blockers, H, AreaMinX, AreaMaxX, AreaMinZ, AreaMaxZ);
            Assert.That(a.Engaged, Is.False, "a third of a second in");
            Assert.That(a.Velocity.Length(), Is.GreaterThan(1f));
            Assert.That(a.Target, Is.Not.EqualTo(Vector3.Zero));
        }

        [Test]
        public void An_engaged_drone_re_picks_but_never_jumps()
        {
            var def = SkylineEnemies.MallCopDrone();
            var a = new BotArrival(def, Door("vent-e-n"), 21, Nav, Blockers);
            var pos = a.Start;
            // the player walks a slow circle on the north ring and the pit's rim for a minute
            var targets = new List<Vector3>();
            float worst = 0f;
            for (int i = 0; i < 60 * 60; i++)
            {
                float ang = i * H * 0.35f;
                var player = Stand(12f * (float)Math.Sin(ang), 0f, 12f + 3f * (float)Math.Cos(ang));
                var next = a.Step(pos, player, Blockers, H, AreaMinX, AreaMaxX, AreaMinZ, AreaMaxZ);
                float step = Vector3.Distance(next, pos);
                worst = Math.Max(worst, step);
                foreach (var b in Blockers) if (Strictly(next, b)) Assert.Fail("inside cover at step " + i + ": " + next);
                pos = next;
                if (a.Engaged && (targets.Count == 0 || targets[targets.Count - 1] != a.Target)) targets.Add(a.Target);
            }
            Assert.That(a.Engaged, Is.True);
            Assert.That(worst, Is.LessThanOrEqualTo(BotArrival.DroneTopSpeed * H + 1e-3f));
            Assert.That(targets.Count, Is.GreaterThanOrEqualTo(4), "it re-picks over a minute");
            // once settled, the drift between points is at the repositioning speed
            Assert.That(a.RepickSeconds, Is.InRange(6f, 9f));
        }

        [Test]
        public void An_engaged_drone_repositions_at_the_slow_speed()
        {
            var def = SkylineEnemies.MallCopDrone();
            var a = new BotArrival(def, Door("vent-e-n"), 4, Nav, Blockers);
            var pos = a.Start;
            var spot = Stand(0, 0, -14);
            for (int i = 0; i < 60 * 12 && !a.Engaged; i++) pos = a.Step(pos, spot, Blockers, H, AreaMinX, AreaMaxX, AreaMinZ, AreaMaxZ);
            Assert.That(a.Engaged, Is.True);
            float worst = 0f;
            for (int i = 0; i < 60 * 40; i++)
            {
                var next = a.Step(pos, spot, Blockers, H, AreaMinX, AreaMaxX, AreaMinZ, AreaMaxZ);
                worst = Math.Max(worst, Vector3.Distance(next, pos));
                pos = next;
            }
            Assert.That(worst, Is.LessThanOrEqualTo(BotArrival.DroneRepositionSpeed * H + 1e-3f));
        }

        [Test]
        public void Arrival_is_deterministic_and_seeds_differ()
        {
            var spot = Stand(0, 0, 14);
            var a1 = FlyDrone(Door("vent-w-s"), spot, 100, 8f);
            var a2 = FlyDrone(Door("vent-w-s"), spot, 100, 8f);
            Assert.That(a2.end, Is.EqualTo(a1.end));
            Assert.That(a2.seconds, Is.EqualTo(a1.seconds));
            var ends = new HashSet<Vector3>();
            for (ulong s = 1; s <= 8; s++) ends.Add(FlyDrone(Door("vent-w-s"), spot, s * 977, 8f).end);
            Assert.That(ends.Count, Is.GreaterThanOrEqualTo(3), "a wave does not stack on one spot");
        }

        static string ZoneAt(Vector3 p)
        {
            string best = "?";
            float area = float.MaxValue;   // the smallest zone that holds the point (the lift lies inside the west ring's box)
            foreach (var z in RepossessArena.Zones())
            {
                float a = (z.maxX - z.minX) * (z.maxZ - z.minZ);
                if (p.X >= z.minX && p.X <= z.maxX && p.Z >= z.minZ && p.Z <= z.maxZ && Math.Abs(p.Y - z.y) < 1.5f && a < area) { area = a; best = z.id; }
            }
            return best;
        }

        static bool Enclosed(string zone) => zone.StartsWith("corridor") || zone.StartsWith("exit-lift") || zone.StartsWith("cab");

        /// <summary>The hollow shops are open-fronted, but a spot hard against a side wall (the corners 2 m inside the doorway, such as
        /// (6, -20) in S4) has no hover point with a line to the chest through the door at either doorway width (3 m or 4 m: checked when
        /// the doorway grew to 4 m, D23). Those corners are tolerated, a few of them, not a general hole.</summary>
        static bool ShopInterior(string zone) => zone.StartsWith("shop-");

        [Test]
        public void Every_open_spot_of_the_arena_gets_a_drone_with_a_clear_line()
        {
            // the service corridors, the west leg and the lift are closed boxes a drone cannot fly into (its doorways are lower than
            // its hover height): a player in there is simply not hunted by the air. Everywhere else a drone engages, and sees the chest.
            var doors = new[] { Door("vent-c-n"), Door("vent-promenade-s"), Door("shutter-n3"), Door("shutter-s1") };
            int total = 0, enclosed = 0, shopSpots = 0, shopHidden = 0;
            var bad = new List<string>();
            var farZones = new SortedSet<string>();
            int stride = Math.Max(1, Nav.Count / 150);
            for (int node = 0; node < Nav.Count; node += stride)
            {
                var spot = Nav.PositionOf(node);
                string zone = ZoneAt(spot);
                foreach (var d in doors)
                {
                    total++;
                    if (ShopInterior(zone)) shopSpots++;
                    var r = FlyDrone(d, spot, (ulong)(node * 31 + 7), 10f);
                    string tag = d.id + " -> " + spot + " (" + zone + ")";
                    if (r.problem != null) bad.Add(tag + ": " + r.problem);
                    if (!r.engaged || r.failed)
                    {
                        if (Enclosed(zone)) enclosed++;
                        else if (ShopInterior(zone)) shopHidden++;
                        else bad.Add(tag + ": no engage point");
                        continue;
                    }
                    if (CoverGeometry.Blocked(r.end, Chest(spot), Blockers)) bad.Add(tag + ": no clear line at the end");
                    float flat = Flat(r.end, spot);
                    if (flat < 12f || flat > 28f) farZones.Add(zone);
                }
            }
            TestContext.Out.WriteLine("spots x doors " + total + ", enclosed (no air engage) " + enclosed + "; closer than the ring in: " + string.Join(", ", farZones));
            TestContext.Out.WriteLine("shop spots x doors " + shopSpots + ", of them hidden from the air " + shopHidden);
            Assert.That(bad, Is.Empty, string.Join(" | ", bad.Take(30)));
            Assert.That(shopHidden, Is.LessThanOrEqualTo(shopSpots / 3), "a few shop corners are hidden from the air, not the shops");
        }

        // ------------------------------------------------------------------ the floor-care unit

        [Test]
        public void A_floor_care_unit_from_every_shutter_and_hatch_reaches_the_player_in_twenty_seconds()
        {
            var def = SkylineEnemies.FloorCareUnit();
            var doors = RepossessSites.Doors().Where(d => d.kind == DoorKind.ShopShutter || d.kind == DoorKind.ServiceHatch).ToArray();
            Assert.That(doors.Length, Is.EqualTo(11));
            var problems = new List<string>();
            foreach (var d in doors)
                foreach (var spot in Spots())
                {
                    var a = new BotArrival(def, d, 3, Nav, Blockers);
                    var pos = a.Start;
                    float t = 0f, worst = 0f;
                    for (; t < 22f && !a.Engaged; t += H)
                    {
                        var next = a.Step(pos, spot, Blockers, H, AreaMinX, AreaMaxX, AreaMinZ, AreaMaxZ);
                        worst = Math.Max(worst, Vector3.Distance(next, pos));
                        pos = next;
                    }
                    string tag = d.id + " -> " + spot;
                    if (!a.Engaged || t > 20f) { problems.Add(tag + ": engaged=" + a.Engaged + " after " + t + " s, " + Flat(pos, spot) + " m away"); continue; }
                    if (worst > def.moveSpeed * H + 1e-3f) problems.Add(tag + ": stepped " + worst);
                    if (Flat(pos, spot) > BotArrival.FloorEngageDistance + 0.05f) problems.Add(tag + ": ends " + Flat(pos, spot));
                }
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [Test]
        public void A_floor_care_unit_stops_half_its_sweep_radius_short()
        {
            var def = SkylineEnemies.FloorCareUnit();
            var spot = Stand(0, 0, -14);
            var a = new BotArrival(def, Door("shutter-s1"), 3, Nav, Blockers);
            var pos = a.Start;
            for (int i = 0; i < 60 * 30; i++) pos = a.Step(pos, spot, Blockers, H, AreaMinX, AreaMaxX, AreaMinZ, AreaMaxZ);
            Assert.That(a.Engaged, Is.True);
            Assert.That(Flat(pos, spot), Is.InRange(def.sweepRadius * 0.5f - 0.3f, def.sweepRadius * 0.5f + 0.3f));
            var before = pos;
            pos = a.Step(pos, spot, Blockers, H, AreaMinX, AreaMaxX, AreaMinZ, AreaMaxZ);
            Assert.That(pos, Is.EqualTo(before), "it stands once it is there");
        }

        [Test]
        public void A_floor_care_unit_follows_a_player_who_goes_up_the_escalator()
        {
            var def = SkylineEnemies.FloorCareUnit();
            var a = new BotArrival(def, Door("shutter-s5"), 8, Nav, Blockers);
            var pos = a.Start;
            var gallery = Stand(10, 6, -20);   // the south gallery: the way up is the south-east escalator
            float t = 0f;
            for (; t < 40f && !a.Engaged; t += H) pos = a.Step(pos, gallery, Blockers, H, AreaMinX, AreaMaxX, AreaMinZ, AreaMaxZ);
            Assert.That(a.Engaged, Is.True, "stuck " + Flat(pos, gallery) + " m away at y " + pos.Y);
            Assert.That(pos.Y, Is.GreaterThan(5f), "it climbed");
        }

        [Test]
        public void Without_a_nav_a_floor_care_unit_drives_straight_and_round_a_block()
        {
            var def = SkylineEnemies.FloorCareUnit();
            var door = new SpawnDoor { id = "t", kind = DoorKind.ShopShutter, position = new Vector3(-20, 0, 0), yawDeg = 90f };
            var wall = new[] { new CoverBox { min = new Vector3(-6, 0, -4), max = new Vector3(-5, 2, 4) } };
            var a = new BotArrival(def, door, 3, null);
            var pos = a.Start;
            var player = new Vector3(0, 0, 0);
            float t = 0f;
            for (; t < 20f && !a.Engaged; t += H)
            {
                pos = a.Step(pos, player, wall, H, -40, 40, -40, 40);
                Assert.That(EngagePoints.BodyInside(pos.X, pos.Z, 0f, 0f, wall), Is.False, "inside the block at t=" + t);
            }
            Assert.That(a.Engaged, Is.True, "stopped at " + pos);
        }

        [Test]
        public void A_floor_care_unit_in_the_open_goes_in_a_straight_line()
        {
            var def = SkylineEnemies.FloorCareUnit();
            var door = new SpawnDoor { id = "t", kind = DoorKind.ShopShutter, position = new Vector3(-20, 0, 0), yawDeg = 90f };
            var a = new BotArrival(def, door, 3, null);
            var pos = a.Start;
            var player = new Vector3(0, 0, 0);
            float expected = Flat(pos, player) - def.sweepRadius * 0.5f;
            float travelled = 0f;
            for (int i = 0; i < 60 * 10; i++)
            {
                var next = a.Step(pos, player, new CoverBox[0], H, -40, 40, -40, 40);
                travelled += Vector3.Distance(pos, next);
                pos = next;
            }
            Assert.That(travelled, Is.EqualTo(expected).Within(0.2f));
        }
    }
}
