using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using JamKit.Movement;
using JamKit.Skyline;
using JamKit.Tests.Movement;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    /// <summary>The route through the main hall (judge round 1): the fights progress through the hall, each from its own
    /// anchor, and no one piece wins every fight.</summary>
    public class RouteTests
    {
        [Test]
        public void TheFightsProgressThroughTheHallEachFromItsOwnAnchor()
        {
            var es = GalleriaArena.Encounters();
            Assert.That(es.Length, Is.EqualTo(3));
            Assert.That(es[0].anchor, Is.EqualTo(GalleriaArena.HallEntry), "the first fight starts at the entrance");
            var zones = new List<float[]>();
            for (int i = 0; i < es.Length; i++)
            {
                if (i > 0) Assert.That(es[i].anchor.Z, Is.GreaterThanOrEqualTo(es[i - 1].anchor.Z + 8f), es[i].id + ": further in than the fight before");
                GalleriaArena.ZoneOf(es[i], out float x0, out float x1, out float z0, out float z1);
                Assert.That(x0, Is.GreaterThanOrEqualTo(GalleriaArena.HallMinX));
                Assert.That(x1, Is.LessThanOrEqualTo(GalleriaArena.HallMaxX));
                Assert.That(z0, Is.GreaterThanOrEqualTo(GalleriaArena.HallMinZ));
                Assert.That(z1, Is.LessThanOrEqualTo(GalleriaArena.HallMaxZ));
                Assert.That(es[i].anchor.X, Is.InRange(x0, x1), es[i].id + ": the anchor is in its zone");
                Assert.That(es[i].anchor.Z, Is.InRange(z0, z1), es[i].id + ": the anchor is in its zone");
                foreach (var z in zones)
                    Assert.That(x0 < z[1] && x1 > z[0] && z0 < z[3] && z1 > z[2], Is.False, es[i].id + ": its zone overlaps an earlier one");
                zones.Add(new[] { x0, x1, z0, z1 });
            }
            Assert.That(GalleriaArena.LiftLobby.min.Z, Is.GreaterThan(es[es.Length - 1].anchor.Z), "the far goal lies beyond the last fight");
        }

        static bool Seen(Vector3 eye, Vector3 p) => !CoverGeometry.BlockedThick(eye, p, GalleriaArena.FightCover(), 0.02f);

        [Test]
        public void TheNextStepAndTheFarGoalAreInSight()
        {
            var es = GalleriaArena.Encounters();
            var lift = GalleriaArena.LiftLobby;
            // the lift's sign: a band across the lobby's front, under the deck's soffit
            var sign = new List<Vector3>();
            for (float x = lift.min.X + 0.5f; x <= lift.max.X - 0.5f; x += 0.5f) sign.Add(new Vector3(x, lift.max.Y - 0.5f, lift.min.Z - 0.05f));
            for (int i = 0; i < es.Length; i++)
            {
                var eye = es[i].anchor + new Vector3(0f, 1.6f, 0f);
                if (i + 1 < es.Length)
                {
                    // from the best standing point of this fight's zone (the player looks round once it is cleared)
                    GalleriaArena.ZoneOf(es[i], out float a0, out float a1, out float b0, out float b1);
                    GalleriaArena.ZoneOf(es[i + 1], out float x0, out float x1, out float z0, out float z1);
                    int best = 0, all = 0;
                    for (float ax = a0 + 0.5f; ax < a1; ax += 1f)
                        for (float az = b0 + 0.5f; az < b1; az += 1f)
                        {
                            var from = new Vector3(ax, 1.6f, az);
                            if (CoverGeometry.DistanceToCover(from, GalleriaArena.HallCover()) < 0.5f) continue;
                            int seen = 0;
                            all = 0;
                            for (float x = x0 + 0.5f; x < x1; x += 1f)
                                for (float z = z0 + 0.5f; z < z1; z += 1f)
                                {
                                    all++;
                                    if (Seen(from, new Vector3(x, 0.05f, z))) seen++;
                                }
                            best = Math.Max(best, seen);
                        }
                    TestContext.WriteLine($"{es[i].id}: {best} of {all} metres of the next fight's marker in sight from the best point of its zone");
                    Assert.That(best * 4, Is.GreaterThanOrEqualTo(all), es[i].id + ": a quarter of the next fight's floor marker is in sight from its zone");
                }
                if (i >= 1)
                {
                    int s = 0;
                    foreach (var p in sign) if (Seen(eye, p)) s++;
                    TestContext.WriteLine($"{es[i].id}: {s} of {sign.Count} points of the lift's sign in sight");
                    Assert.That(s * 2, Is.GreaterThanOrEqualTo(sign.Count), es[i].id + ": the far goal's sign is in sight from the anchor");
                }
            }
        }

        static int Piece(Vector2 spot, CoverBox[] cover)
        {
            int best = -1;
            float bd = float.PositiveInfinity;
            for (int i = 0; i < cover.Length; i++)
            {
                float d = CoverGeometry.DistanceToCover(new Vector3(spot.X, 0f, spot.Y), new[] { cover[i] });
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        [Test]
        public void NoOnePieceIsTheHideOfEveryFight()
        {
            // the skilled line's clean hides in each fight (5 seeds), by the piece they stand beside: each fight's favourite is
            // its own, and the information booth is the favourite of the food court at most
            var cover = GalleriaArena.FightCover();
            var top = new List<int>();
            foreach (var e in GalleriaArena.Encounters())
            {
                var count = new int[cover.Length];
                for (ulong s = 101; s < 106; s++)
                {
                    var bot = new TacticalBot(s, FightTiming.Human());
                    var f = FightTests.Make(e.id, bot, s, e.anchor);
                    bot.HidePicked = (spot, clean) => { if (clean) count[Piece(spot, cover)]++; };
                    f.Run(180f);
                }
                int best = 0, total = 0;
                for (int i = 0; i < count.Length; i++) { total += count[i]; if (count[i] > count[best]) best = i; }
                var c = (cover[best].min + cover[best].max) * 0.5f;
                TestContext.WriteLine($"{e.id}: favourite hide beside the piece at ({c.X:0.0}, {c.Z:0.0}), {count[best]} of {total} clean hides");
                Assert.That(top.Contains(best), Is.False, e.id + ": its favourite hide is another fight's");
                if (e.anchor != GalleriaArena.HallEntry)
                    Assert.That(new Vector2(c.X, c.Z + 12f).Length(), Is.GreaterThan(3f), e.id + ": the information booth isn't its favourite hide");
                top.Add(best);
            }
        }

        // ---- the movement the floor asks for

        [Test]
        public void ThePracticeCounterTakesADoubleJumpAndAMantleAndNoLess()
        {
            var top = GalleriaArena.PracticeLedgeTop;
            float s = GalleriaArena.PracticeLedgeSize;
            Func<MoveSim> make = () =>
            {
                var w = new BoxWorld();
                w.Floor(GalleriaArena.WarmupMinX, GalleriaArena.WarmupMaxX, GalleriaArena.WarmupMinZ, GalleriaArena.WarmupMaxZ, 0f);
                w.Add(new Vector3(top.X, top.Y * 0.5f, top.Z), new Vector3(s, top.Y, s), runnable: false);
                // facing it (yaw 180, toward -Z) from 0.8 m north of its north face
                return MoveTestKit.Standing(w, new Vector3(top.X, 0f, top.Z + s * 0.5f + 0.8f), null, 180f);
            };
            Func<bool, float> climb = doubleJump =>
            {
                var sim = make();
                var fwd = MoveTestKit.Forward(180f);
                sim.Step(fwd.With(jump: true), MoveTestKit.H);
                for (int i = 0; i < 600 && !(!sim.Grounded && sim.Velocity.Y <= 0f); i++) sim.Step(fwd, MoveTestKit.H);
                if (doubleJump) sim.Step(fwd.With(jump: true), MoveTestKit.H);
                for (float t = 0f; t < 3f; t += MoveTestKit.H)
                {
                    sim.Step(fwd, MoveTestKit.H);
                    if (sim.Grounded && sim.Position.Y > 1f) break;
                }
                return sim.Grounded ? sim.Position.Y : float.NaN;
            };
            Assert.That(climb(true), Is.EqualTo(top.Y).Within(0.1f), "jump, double jump at the apex, mantle: on top");
            float single = climb(false);
            Assert.That(float.IsNaN(single) || single < 1f, Is.True, "a single jump and a mantle fall short");

            // far enough from the boost kiosk (edge to edge) that no jump from its top lands on the kiosk's: that one still
            // needs the boost
            var k = GalleriaArena.BoostLedgeTop;
            float h = GalleriaArena.BoostLedgeSize * 0.5f, p = s * 0.5f;
            float dx = Math.Max(0f, Math.Abs(top.X - k.X) - h - p), dz = Math.Max(0f, Math.Abs(top.Z - k.Z) - h - p);
            Assert.That(MathF.Sqrt(dx * dx + dz * dz), Is.GreaterThanOrEqualTo(12f));
            Assert.That(top.X + p, Is.LessThanOrEqualTo(GalleriaArena.WarmupMaxX - 0.8f), "inside the warm-up hall, a walkable gap to the wall");
            Assert.That(top.Z - p, Is.GreaterThanOrEqualTo(GalleriaArena.WarmupMinZ + 0.8f));
        }

        /// <summary>From the floor 0.8 m short of the deck's edge, facing it: jump, double jump at the apex, a Breacher boost
        /// pitched 85 degrees down at the double jump's apex, forward held: the highest the feet stand afterwards.</summary>
        static float BoostAtTheDeck(bool rail)
        {
            var w = new BoxWorld();
            w.Floor(-10f, 10f, 0f, 21f, 0f);
            var d = GalleriaArena.MezzanineDeck;
            w.Add(new Vector3(0f, (d.min.Y + d.max.Y) * 0.5f, (d.min.Z + d.max.Z) * 0.5f), new Vector3(20f, d.max.Y - d.min.Y, d.max.Z - d.min.Z), false);
            if (rail)
            {
                var r = GalleriaArena.MezzanineRail()[0];
                w.Add(new Vector3(0f, (r.min.Y + r.max.Y) * 0.5f, (r.min.Z + r.max.Z) * 0.5f), new Vector3(20f, r.max.Y - r.min.Y, r.max.Z - r.min.Z), false);
            }
            var sim = MoveTestKit.Standing(w, new Vector3(0f, 0f, d.min.Z - 0.38f - 0.8f));
            sim.OutsidePushEnabled = true;
            var charge = new BoostCharge();
            var def = new BreacherBoostDef();
            var fwd = MoveTestKit.Forward();
            System.Action step = () => { charge.Observe(sim.Grounded, sim.Mode == MoveMode.Wallrun); sim.Step(fwd, MoveTestKit.H); };
            charge.Observe(sim.Grounded, false);
            sim.Step(fwd.With(jump: true), MoveTestKit.H);
            for (int i = 0; i < 600 && !(!sim.Grounded && sim.Velocity.Y <= 0f); i++) step();
            sim.Step(fwd.With(jump: true), MoveTestKit.H);
            for (int i = 0; i < 600 && !(!sim.Grounded && sim.Velocity.Y <= 0f); i++) step();
            float p = 85f * MathF.PI / 180f;
            Vector3 push = BreacherKnockback.RequestSelfPush(new Vector3(0f, -MathF.Sin(p), MathF.Cos(p)), sim.Velocity, false, sim.def.gravity, charge, def, out bool spends);
            charge.Commit(spends, sim.AddPush(push));
            float best = 0f;
            for (float t = 0f; t < 3f; t += MoveTestKit.H)
            {
                step();
                if (sim.Grounded) best = Math.Max(best, sim.Position.Y);
            }
            return best;
        }

        [Test]
        public void TheBalustradeKeepsABoostedPlayerOffTheMezzanine()
        {
            float withRail = BoostAtTheDeck(true), bare = BoostAtTheDeck(false);
            TestContext.WriteLine($"highest the feet stood after the boost at the deck's edge: {withRail:0.00} m with the balustrade, {bare:0.00} m without");
            Assert.That(withRail, Is.LessThan(GalleriaArena.MezzanineY - 0.5f), "the balustrade's top is out of a boosted mantle's reach");
        }

        static BoxWorld LiftWorld(bool shutterRaised)
        {
            var w = new BoxWorld();
            w.Floor(-10f, 20f, 0f, 21f, 0f);
            var d = GalleriaArena.MezzanineDeck;
            w.Add(new Vector3(6f, (d.min.Y + d.max.Y) * 0.5f, (d.min.Z + d.max.Z) * 0.5f), new Vector3(30f, d.max.Y - d.min.Y, d.max.Z - d.min.Z), false);
            var l = GalleriaArena.LiftLobby;
            float t = GalleriaArena.LobbyWall, hgt = l.max.Y;
            w.Add(new Vector3(l.min.X + t * 0.5f, hgt * 0.5f, (l.min.Z + l.max.Z) * 0.5f), new Vector3(t, hgt, l.max.Z - l.min.Z));
            w.Add(new Vector3(l.max.X - t * 0.5f, hgt * 0.5f, (l.min.Z + l.max.Z) * 0.5f), new Vector3(t, hgt, l.max.Z - l.min.Z));
            w.Add(new Vector3((l.min.X + l.max.X) * 0.5f, 5f, l.max.Z + 0.5f), new Vector3(30f, 10f, 1f));   // the north wall
            float bottom = shutterRaised ? GalleriaArena.ShutterGap : 0f;
            w.Add(new Vector3((l.min.X + l.max.X) * 0.5f, (bottom + hgt) * 0.5f, l.min.Z + 0.1f), new Vector3(l.max.X - l.min.X - 2f * t, hgt - bottom, 0.2f), false);
            return w;
        }

        /// <summary>Sprints at the lobby from 9 m out; with <paramref name="slide"/>, crouch is pressed 3.5 m short of the shutter
        /// and held (the demo's input). Returns where it ends (z).</summary>
        public static float RunAtTheLift(bool shutterRaised, bool slide)
        {
            var l = GalleriaArena.LiftLobby;
            float cx = (l.min.X + l.max.X) * 0.5f, at = l.min.Z - 3.5f;
            var sim = MoveTestKit.Standing(LiftWorld(shutterRaised), new Vector3(cx, 0f, l.min.Z - 9f));
            var fwd = MoveTestKit.Forward();
            bool pressed = false;
            for (float t = 0f; t < 4f; t += MoveTestKit.H)
            {
                bool crouch = slide && sim.Position.Z > at;
                bool press = crouch && !pressed;
                pressed |= press;
                sim.Step(fwd.With(crouchHeld: crouch, crouchPressed: press), MoveTestKit.H);
            }
            return sim.Position.Z;
        }

        [Test]
        public void TheLiftIsReachedBySlidingUnderTheRaisedShutterAndNotStanding()
        {
            var l = GalleriaArena.LiftLobby;
            var def = new MoveDef();
            Assert.That(GalleriaArena.ShutterGap, Is.InRange(def.slideHeight + 0.1f, def.standHeight - 0.2f));
            Assert.That(RunAtTheLift(true, true), Is.GreaterThan(l.min.Z + 1f), "sprint and slide: under the shutter, into the lobby");
            Assert.That(RunAtTheLift(true, false), Is.LessThan(l.min.Z), "standing, the shutter stops it");
            Assert.That(RunAtTheLift(false, true), Is.LessThan(l.min.Z), "the shutter down (during the fights): shut");
        }

        // ---- design aid: try layout variants without editing the data

        static BoxWorld World(CoverBox[] cover)
        {
            var w = new BoxWorld();
            float x0 = GalleriaArena.HallMinX, x1 = GalleriaArena.HallMaxX, z0 = GalleriaArena.HallMinZ, z1 = GalleriaArena.HallMaxZ;
            w.Floor(x0 - 1f, x1 + 1f, z0 - 1f, z1 + 1f, 0f);
            w.Add(new Vector3(x0 - 0.5f, 5f, 0f), new Vector3(1f, 10f, z1 - z0 + 2f));
            w.Add(new Vector3(x1 + 0.5f, 5f, 0f), new Vector3(1f, 10f, z1 - z0 + 2f));
            w.Add(new Vector3(0f, 5f, z0 - 0.5f), new Vector3(x1 - x0 + 2f, 10f, 1f));
            w.Add(new Vector3(0f, 5f, z1 + 0.5f), new Vector3(x1 - x0 + 2f, 10f, 1f));
            foreach (var b in cover) w.Add((b.min + b.max) * 0.5f, b.max - b.min, false);
            return w;
        }

        static string Clean(EncounterDef e, CoverBox[] cover, ulong first)
        {
            int clean = 0, damaged = 0, cleared = 0;
            float clearSum = 0f;
            var lows = new List<float>();
            for (ulong s = first; s < first + 30; s++)
            {
                var r = new FightSim(e, World(cover), new TacticalBot(s, FightTiming.Human()), cover, e.anchor, s, Difficulties.Normal(), null, null, null, 0f).Run(180f);
                if (r.cleared && r.chassisLost == 0f) clean++;
                if (r.damageTaken > 0f) damaged++;
                if (r.cleared) { cleared++; clearSum += r.clearSeconds; }
                lows.Add(r.minPlating);
            }
            lows.Sort();
            return $"clean {clean}/30, median low {lows[15]:0}, damaged {damaged}, mean clear {clearSum / Math.Max(1, cleared):0} s";
        }

        static CoverBox Box(float x, float z, float sx, float sz, float h) =>
            new CoverBox { min = new Vector3(x - sx * 0.5f, 0f, z - sz * 0.5f), max = new Vector3(x + sx * 0.5f, h, z + sz * 0.5f) };

        static Spawn[] Moved(Spawn[] s, Func<Spawn, Spawn> f)
        {
            var r = new Spawn[s.Length];
            for (int i = 0; i < s.Length; i++) r[i] = f(s[i]);
            return r;
        }

        static Spawn D(float x, float z, int g) => new Spawn(SkylineEnemies.DroneId, new Vector3(x, GalleriaArena.DroneY, z), g);
        static Spawn F(float x, float z, int g) => new Spawn(SkylineEnemies.FloorCareId, new Vector3(x, 0f, z), g);
        static Spawn S(float x, int g) => new Spawn(SkylineEnemies.SpotlightId, new Vector3(x, 6.5f, 12.3f), g);

        [Test, Explicit("design aid: escalation variants of the later fights (gate seeds 101-130, held-out 201-230)")]
        public void SweepEscalation([Values(0, 1, 2, 3, 4, 5, 6, 7)] int variant)
        {
            var cover = new List<CoverBox>(GalleriaArena.FightCover());
            EncounterDef e;
            string what;
            var fa = FightTests.ById("facade");
            var sk = FightTests.ById("skylight");
            var g = new List<Spawn>();
            foreach (var x in fa.spawns) if (x.enemy == SkylineEnemies.GondolaId) g.Add(x);
            switch (variant)
            {
                case 0: e = fa; what = "facade as shipped"; break;
                case 1:
                    e = fa; what = "facade, 10 drones (one more each side)";
                    e.spawns = new List<Spawn>(fa.spawns) { D(-22f, -5f, 0), D(22f, -5f, 1) }.ToArray();
                    break;
                case 2:
                    e = fa; what = "facade, 12 drones (two more each side)";
                    e.spawns = new List<Spawn>(fa.spawns) { D(-22f, -5f, 0), D(22f, -5f, 1), D(-22f, 2f, 0), D(22f, 2f, 1) }.ToArray();
                    break;
                case 3:
                    e = fa; what = "facade, 12 drones, the extra pair a third group (south-west/south-east)";
                    e.spawns = new List<Spawn>(fa.spawns) { D(-22f, -5f, 0), D(22f, -5f, 1), D(-17f, -15f, 2), D(17f, -15f, 2) }.ToArray();
                    break;
                case 4: e = sk; what = "skylight as shipped"; break;
                default:
                {
                    e = sk;
                    var list = new List<Spawn>(sk.spawns);
                    list.Add(S(-4.5f, 2));
                    cover.Add(new CoverBox { min = new Vector3(-5f, GalleriaArena.MezzanineY, 12.05f), max = new Vector3(-4f, 6.5f - GalleriaArena.SpotlightBaseBelow, 12.7f) });
                    if (variant == 5) what = "skylight, a third spotlight (x -4.5) alone";
                    else if (variant == 6) { what = "skylight, a third spotlight with two floor-care units from the south"; list.Add(F(-10f, -15f, 2)); list.Add(F(10f, -15f, 2)); }
                    else { what = "skylight, a third spotlight with one floor-care unit from the south"; list.Add(F(10f, -15f, 2)); }
                    e.spawns = list.ToArray();
                    break;
                }
            }
            TestContext.WriteLine($"variant {variant} ({what}): spread {EncounterRules.BearingSpread(e):0}, rules [{string.Join("; ", EncounterRules.Check(e, SkylineEnemies.Find))}]; gate {Clean(e, cover.ToArray(), 101)}; held-out {Clean(e, cover.ToArray(), 201)}");
        }

        [Test, Explicit("design aid: the skylight's clean seeds for variants of the mezzanine's rail")]
        public void SweepSkylightVariants([Values(0, 1, 2, 3)] int variant)
        {
            var e = FightTests.ById("skylight");
            var all = new List<CoverBox>(GalleriaArena.FightCover());
            int n = GalleriaArena.MezzanineRail().Length;
            var cover = all.GetRange(0, all.Count - n);
            var rail = GalleriaArena.MezzanineRail();
            string what = "no rail, no plinths";
            if (variant == 1) { what = "rail only"; cover.Add(rail[0]); }
            if (variant == 2) { what = "plinths only"; for (int i = 1; i < rail.Length; i++) cover.Add(rail[i]); }
            if (variant == 3) { what = "rail and plinths"; cover.AddRange(rail); }
            TestContext.WriteLine($"skylight variant {variant} ({what}): gate {Clean(e, cover.ToArray(), 101)}; held-out {Clean(e, cover.ToArray(), 201)}");
        }

        [Test, Explicit("design aid: the food court's human-timed line for layout variants (gate seeds 101-130, held-out 201-230 and 301-330)")]
        public void SweepRouteVariants([Values(0, 1, 2, 3, 4, 5, 6, 7)] int variant)
        {
            var e = FightTests.ById("food-court");
            var cover = new List<CoverBox>(GalleriaArena.FightCover());
            string what = "as shipped";
            Func<Spawn, bool> north = s => s.group == 2 && s.enemy == SkylineEnemies.DroneId;
            Func<Spawn, bool> side = s => s.group < 2 && s.enemy == SkylineEnemies.DroneId;
            switch (variant)
            {
                case 1: what = "north group 0.75 m north"; e.spawns = Moved(e.spawns, s => { if (north(s)) s.position.Z += 0.75f; return s; }); break;
                case 2: what = "north group 1.5 m north"; e.spawns = Moved(e.spawns, s => { if (north(s)) s.position.Z += 1.5f; return s; }); break;
                case 3: what = "side groups 1 m nearer their walls"; e.spawns = Moved(e.spawns, s => { if (side(s)) s.position.X += s.position.X < 0f ? -1f : 1f; return s; }); break;
                case 4: what = "side groups' southernmost drones 1 m north"; e.spawns = Moved(e.spawns, s => { if (side(s) && s.position.Z < -6f) s.position.Z += 1f; return s; }); break;
                case 5: what = "floor-care units 3 m further north"; e.spawns = Moved(e.spawns, s => { if (s.enemy == SkylineEnemies.FloorCareId) s.position.Z += 3f; return s; }); break;
                case 6: what = "side groups' (19,-4) drones 1 m out"; e.spawns = Moved(e.spawns, s => { if (side(s) && Math.Abs(s.position.Z + 4f) < 0.1f) s.position.X += s.position.X < 0f ? -1f : 1f; return s; }); break;
                case 7: what = "north group at 4.0 m"; e.spawns = Moved(e.spawns, s => { if (north(s)) s.position.Y = 4f; return s; }); break;
            }
            TestContext.WriteLine($"variant {variant} ({what}): gate {Clean(e, cover.ToArray(), 101)}; held-out {Clean(e, cover.ToArray(), 201)}; {Clean(e, cover.ToArray(), 301)}");
        }

        [Test, Explicit("design aid: per hit taken on the human-timed line, where the player stood and who could see it")]
        public void PrintWhereTheSkilledLineIsHit([Values("food-court", "facade", "skylight")] string id)
        {
            var sb = new StringBuilder();
            for (ulong s = 101; s < 131; s++)
            {
                var bot = new TacticalBot(s, FightTiming.Human());
                var f = FightTests.Make(id, bot, s, FightTests.ById(id).anchor);
                float last = f.Armor.Plating + f.Armor.Chassis;
                var lines = new List<string>();
                while (!f.Over && f.Time < 180f)
                {
                    f.Tick();
                    float now = f.Armor.Plating + f.Armor.Chassis;
                    if (now < last - 0.01f)
                    {
                        var p = f.Player.Position;
                        var who = new StringBuilder();
                        foreach (var e in f.Enemies)
                        {
                            if (e.Wrecked) continue;
                            if (FightSim.IsShooter(e) && e.SeesPlayer) who.Append($" {e.Def.id[0]}{e.Index}g{e.Group}@({e.Position.X:0},{e.Position.Z:0})");
                            else if (!FightSim.IsShooter(e) && Vector3.Distance(new Vector3(e.Position.X, 0f, e.Position.Z), new Vector3(p.X, 0f, p.Z)) < 5f)
                                who.Append($" {e.Def.id[0]}{e.Index}near");
                        }
                        lines.Add($"  t {f.Time:0.0} -{last - now:0} at ({p.X:0.0},{p.Z:0.0}) {bot.Current}:{who}");
                    }
                    last = now;
                }
                var r = f.Result();
                if (r.chassisLost > 0f)
                {
                    sb.AppendLine($"{id} seed {s}: chassis lost {r.chassisLost:0}, cleared {r.cleared}");
                    foreach (var l in lines) sb.AppendLine(l);
                }
            }
            TestContext.WriteLine(sb.ToString());
        }
    }
}
