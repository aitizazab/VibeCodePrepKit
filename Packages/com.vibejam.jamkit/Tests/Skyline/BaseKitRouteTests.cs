using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using JamKit.Movement;
using JamKit.Skyline;
using JamKit.Tests.Movement;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    public class BaseKitRouteTests
    {
        // ---------------------------------------------------------------- on foot

        static readonly ArenaNav Nav = ArenaNav.Build(ArenaLint.OpenState(RepossessArena.Solids()), 2f);

        static MoveSim StandAt(BoxWorld w, Vector3 p)
        {
            RepossessArena.TryGroundY(p.X, p.Z, p.Y + 0.05f, out float g);
            return MoveTestKit.Standing(w, new Vector3(p.X, g, p.Z));
        }

        /// <summary>Runs the walker with the real MoveSim; true when it finished within the time. Seconds taken in <paramref name="took"/>.</summary>
        static bool Drive(MoveSim sim, RouteWalker bot, float maxSeconds, out float took)
        {
            float t = 0f;
            while (t < maxSeconds && !bot.Done)
            {
                sim.Step(bot.Next(sim, t, MoveTestKit.H), MoveTestKit.H);
                t += MoveTestKit.H;
            }
            took = t;
            return bot.Done;
        }

        static Vector3 GoalNear(SocketDef s)
        {
            int best = -1; float bd = float.MaxValue;
            for (int i = 0; i < Nav.Count; i++)
            {
                Vector3 p = Nav.PositionOf(i);
                if (Nav.ComponentOf(i) != 0 || Math.Abs(p.Y - s.position.Y) > Repossession.CircleHalfHeight) continue;
                float d = (p.X - s.position.X) * (p.X - s.position.X) + (p.Z - s.position.Z) * (p.Z - s.position.Z);
                if (d < bd) { bd = d; best = i; }
            }
            Assert.That(best, Is.GreaterThanOrEqualTo(0), s.id + ": a node in its circle");
            return Nav.PositionOf(best);
        }

        static List<Vector3> NavRoute(Vector3 from, Vector3 to)
        {
            int a = Nav.NodeAt(from), b = Nav.NodeAt(to);
            var path = Nav.Path(a, b);
            Assert.That(path, Is.Not.Null, $"a walk from {from} to {to}");
            return Nav.Simplify(path);
        }

        [Test]
        public void FromTheArrivalPointToEverySocketOnFoot()
        {
            var w = FloorWorld.Build();
            foreach (var s in RepossessSites.Sockets())
            {
                Vector3 goal = GoalNear(s);
                var route = NavRoute(RepossessArena.ArrivalPoint, goal);
                var sim = StandAt(w, RepossessArena.ArrivalPoint);
                var bot = new RouteWalker(route);
                Assert.That(Drive(sim, bot, 90f, out float took), Is.True, $"{s.id}: walker stopped at waypoint {bot.TargetIndex}/{route.Count} {sim.Position}");
                Assert.That(Vector2.Distance(new Vector2(sim.Position.X, sim.Position.Z), new Vector2(goal.X, goal.Z)), Is.LessThan(1.5f), s.id);
                Assert.That(sim.Position.Y, Is.EqualTo(goal.Y).Within(0.8f), s.id);
                Assert.That(took, Is.LessThan(90f), s.id);
            }
        }

        [Test]
        public void FromThePitToTheExitLiftOnceTheShutterIsUp()
        {
            var map = new Dictionary<string, BoxWorld.Box>();
            var w = FloorWorld.Build(map);
            map[RepossessArena.LiftShutterId].enabled = false;
            var start = GoalNear(RepossessSites.Sockets().First(s => s.id == "pit-ne"));
            var route = NavRoute(start, new Vector3(-29f, 0f, 0f));
            var sim = StandAt(w, start);
            var bot = new RouteWalker(route);
            Assert.That(Drive(sim, bot, 60f, out _), Is.True, "reached the lift: " + sim.Position);
            var e = RepossessArena.ExitLiftZone;
            Assert.That(sim.Position.X, Is.InRange(e.min.X, e.max.X));
            Assert.That(sim.Position.Z, Is.InRange(e.min.Z, e.max.Z));
        }

        [Test]
        public void EveryGroundRouteOfTheSitesIsWalkable()
        {
            var map = new Dictionary<string, BoxWorld.Box>();
            var w = FloorWorld.Build(map);
            map[RepossessArena.LiftShutterId].enabled = false;                // the finale route runs into the lift
            foreach (var r in RepossessSites.Routes())
            {
                if (r.id == "facade-wall-run") continue;         // the wall-run route is its own test below
                var sim = StandAt(w, r.waypoints[0]);
                var bot = new RouteWalker(r);
                Assert.That(Drive(sim, bot, 90f, out _), Is.True, $"{r.id}: stopped at waypoint {bot.TargetIndex}/{r.waypoints.Length} {sim.Position}");
                var end = r.waypoints[r.waypoints.Length - 1];
                Assert.That(Vector2.Distance(new Vector2(sim.Position.X, sim.Position.Z), new Vector2(end.X, end.Z)), Is.LessThan(1.5f), r.id);
                Assert.That(sim.Position.Y, Is.EqualTo(end.Y).Within(0.8f), r.id);
            }
        }

        [Test]
        public void TheSlideChuteIsSlidDownFromTheRingByAHintedRoute()
        {
            var w = FloorWorld.Build();
            var wp = new[] { new Vector3(-10.5f, 0f, 17f), new Vector3(-10.5f, 0f, 13f), new Vector3(-10.5f, -4f, 4.6f), new Vector3(-10.5f, -4f, 3.2f) };
            var sim = StandAt(w, wp[0]);
            bool slid = false;
            var bot = new RouteWalker(wp, new[] { new MoveHint { atWaypoint = 2, kind = MoveHintKind.Slide } });
            float t = 0f;
            while (t < 12f && !bot.Done)
            {
                sim.Step(bot.Next(sim, t, MoveTestKit.H), MoveTestKit.H);
                slid |= sim.Mode == MoveMode.Slide;
                t += MoveTestKit.H;
            }
            Assert.That(slid, Is.True, "slid");
            Assert.That(bot.Done, Is.True, "reached the pit floor: " + sim.Position);
            Assert.That(sim.Position.Y, Is.EqualTo(-4f).Within(0.3f));
        }

        // ---------------------------------------------------------------- the façade and the gallery lips

        /// <summary>Scripted: run north along the façade, jump at <paramref name="jumpZ"/> (a jump and a mantle onto a landing),
        /// on top run on, jump at <paramref name="jump2Z"/> and double jump at the apex (a mantle onto the deck face at z 15). With
        /// <paramref name="landingTop"/> 0 there is no landing. Returns the highest the feet got, or the height it landed on a gallery at.</summary>
        static float LandingClimb(float landingTop, float zMin, float zMax, float jumpZ, float jump2Z)
        {
            var w = WorldWithoutTheLanding();
            if (landingTop > 0f) w.Add(new Vector3(32.75f, landingTop / 2, (zMin + zMax) / 2), new Vector3(4.5f, landingTop, zMax - zMin), false);
            var sim = MoveTestKit.Standing(w, new Vector3(32.5f, 0f, zMin - 12f));
            bool j1 = false, j2 = false, dj = false;
            float best = 0f;
            for (float t = 0f; t < 10f; t += MoveTestKit.H)
            {
                var input = new MoveInput { move = new Vector2(0f, 1f), yaw = 0f, sprintHeld = true };
                if (!j1 && sim.Position.Z >= jumpZ && sim.Grounded) { input.jumpPressed = input.jumpHeld = true; j1 = true; }
                else if (j1 && !j2 && sim.Grounded && sim.Position.Y > landingTop - 0.2f && sim.Position.Z >= jump2Z) { input.jumpPressed = input.jumpHeld = true; j2 = true; }
                else if (j2 && !dj && !sim.Grounded && sim.Mode == MoveMode.Air && sim.Velocity.Y <= 0f) { input.jumpPressed = input.jumpHeld = true; dj = true; }
                sim.Step(input, MoveTestKit.H);
                best = Math.Max(best, sim.Position.Y);
                if (sim.Grounded && sim.Position.Y > 5.5f) return sim.Position.Y;
            }
            return best;
        }

        /// <summary>The arena as the movement tests see it but without <c>facade-landing</c> (the ledge decision D23 added), for the
        /// tests that ask what the wall alone, or a landing of another size, does.</summary>
        static BoxWorld WorldWithoutTheLanding()
        {
            var map = new Dictionary<string, BoxWorld.Box>();
            var w = FloorWorld.Build(map);
            map[RepossessArena.FacadeLandingId].enabled = false;
            return w;
        }

        public struct Facade { public float topY, maxY, endZ; public bool wallran, kicked, doubleJumped, mantled; }

        /// <summary>The warm-up ledge's sequence on the façade: start at (x0, z0) facing north, sprint with the stick a little into the
        /// east wall; jump once at jumpZ; on the wall, kick off it at kickZ (the stick turned kickYaw degrees); double jump at the
        /// apex. Returns what happened.</summary>
        static Facade WallRunSequence(float x0, float z0, float jumpZ, float kickZ, float kickYaw, float stickIn)
        {
            var sim = MoveTestKit.Standing(WorldWithoutTheLanding(), new Vector3(x0, 0f, z0));
            var f = new Facade();
            bool jumped = false, kicked = false, doubled = false;
            var along = new MoveInput { move = new Vector2(stickIn, 1f), yaw = 0f, sprintHeld = true };
            float k = kickYaw * MathF.PI / 180f;
            var toward = new MoveInput { move = new Vector2(MathF.Sin(k), MathF.Cos(k)), yaw = 0f, sprintHeld = true };
            for (float t = 0f; t < 6f; t += MoveTestKit.H)
            {
                var input = kicked ? toward : along;
                if (!jumped && sim.Position.Z >= jumpZ) { input.jumpPressed = input.jumpHeld = true; jumped = true; }
                else if (jumped && !kicked && sim.Mode == MoveMode.Wallrun && sim.Position.Z >= kickZ) { input = toward; input.jumpPressed = input.jumpHeld = true; kicked = true; }
                else if (kicked && !doubled && !sim.Grounded && sim.Mode == MoveMode.Air && sim.Velocity.Y <= 0f) { input.jumpPressed = input.jumpHeld = true; doubled = true; }
                sim.Step(input, MoveTestKit.H);
                f.wallran |= sim.Mode == MoveMode.Wallrun;
                f.kicked |= sim.Events.wallJumped;
                f.doubleJumped |= sim.Events.doubleJumped;
                f.mantled |= sim.Mode == MoveMode.Mantle;
                f.maxY = Math.Max(f.maxY, sim.Position.Y);
                if (sim.Grounded && sim.Position.Y > 1f) { f.topY = sim.Position.Y; break; }
            }
            f.endZ = sim.Position.Z;
            return f;
        }

        [Test]
        public void TheFacadeWallRunRunsTheWallAndKicksOffButTopsOutBelowTheGalleryLip()
        {
            // FINDING (task F3): the plan's route (b), wall-run + kick + mantle onto the north gallery, cannot reach it on today's data.
            // A wall-run keeps the height it is caught at, a wall kick and a double jump add about 1.25 and 1 m, a mantle takes a
            // ledge up to 1.9 m over the feet: about 5.4 m, and the deck is 6.0 m. The best of a sweep of sequences:
            float best = 0f; bool wallran = false, kicked = false, reached = false;
            foreach (float jz in new[] { -22f, -16f, -10f })
                foreach (float dk in new[] { 3f, 6f, 9f })
                    foreach (float yaw in new[] { 0f, 30f })
                        foreach (float stick in new[] { 0.2f, 0.4f })
                        {
                            var f = WallRunSequence(33f, -27.5f, jz, jz + dk, yaw, stick);
                            best = Math.Max(best, f.maxY);
                            wallran |= f.wallran; kicked |= f.kicked;
                            reached |= f.topY > 5.5f;
                        }
            Assert.That(wallran && kicked, Is.True, "the façade can be run and kicked off");
            Assert.That(best, Is.InRange(3.2f, 3.9f), "the best the base kit does on the façade, feet height");
            Assert.That(best + new MoveDef().mantleMaxHeight, Is.LessThan(RepossessArena.GalleryY), "plus the longest mantle, still under the deck");
            Assert.That(reached, Is.False, "if this fails the gallery IS reachable by the wall: delete this finding and the landing nudge");
            // F2's route ends with a mantle onto the lip: its last waypoint is at the deck
            var route = RepossessSites.Routes().First(r => r.id == "facade-wall-run");
            Assert.That(route.waypoints[route.waypoints.Length - 1].Y, Is.EqualTo(RepossessArena.GalleryY).Within(0.01f));
        }

        [Test]
        public void ALandingAgainstTheFacadeMakesTheNorthGalleryLipReachableWithTheBaseKit()
        {
            // the nudge: a 2.8 m high landing (role deck, x 30.5..35, z 9..14.8) against the façade. A jump and a mantle put the Unit on
            // it, a jump and a double jump from its top put the deck face (z 15) within the longest mantle. Without it: not.
            Assert.That(LandingClimb(0f, 9f, 14.8f, 8f, 12f), Is.LessThan(4f), "no landing: nowhere near");
            Assert.That(LandingClimb(2.8f, 9f, 14.8f, 6f, 11f), Is.EqualTo(RepossessArena.GalleryY).Within(0.1f), "the scripted climb lands on the gallery");
            int ok = 0, n = 0;
            for (float j1 = 5f; j1 <= 9f; j1 += 1f)
                for (float j2 = 10f; j2 <= 14.4f; j2 += 0.6f)
                {
                    n++;
                    if (LandingClimb(2.8f, 9f, 14.8f, j1, j2) > 5.5f) ok++;
                }
            Assert.That(ok, Is.GreaterThanOrEqualTo(n / 5), $"a forgiving window of timings, not a knife edge ({ok}/{n})");
        }

        [Test]
        public void TheFacadeWallRunRouteOfTheSitesEndsOnTheNorthGalleryWithTheLanding()
        {
            // the whole route of F2's data (the wall run, the landing, the double jump), driven by the RouteWalker on the real data
            var route = RepossessSites.Routes().First(r => r.id == "facade-wall-run");
            var w = FloorWorld.Build();
            var sim = StandAt(w, route.waypoints[0]);
            var bot = new RouteWalker(route) { JumpRange = 7f };
            Assert.That(Drive(sim, bot, 60f, out float took), Is.True, $"stopped at waypoint {bot.TargetIndex}/{route.waypoints.Length} {sim.Position}");
            var end = route.waypoints[route.waypoints.Length - 1];
            Assert.That(sim.Position.Y, Is.EqualTo(RepossessArena.GalleryY).Within(0.3f), "on the north gallery");
            Assert.That(Vector2.Distance(new Vector2(sim.Position.X, sim.Position.Z), new Vector2(end.X, end.Z)), Is.LessThan(1.5f));
            // and without the landing the same route does not get there
            var bare = WorldWithoutTheLanding();
            var sim2 = StandAt(bare, route.waypoints[0]);
            var bot2 = new RouteWalker(route) { JumpRange = 7f };
            Assert.That(Drive(sim2, bot2, 60f, out _), Is.False, "without the landing the route cannot be finished");
        }

        [Test]
        public void TheMantleAndDoubleJumpHintsOfARouteWalkerClimbTheLandingOntoTheGallery()
        {
            var w = FloorWorld.Build();                                                           // with the facade-landing in it
            var sim = MoveTestKit.Standing(w, new Vector3(32.5f, 0f, -3f));
            var wp = new[] { new Vector3(32.5f, 0f, -3f), new Vector3(32.5f, 2.8f, 12f), new Vector3(32.5f, 6f, 17.5f) };
            var bot = new RouteWalker(wp, new[] { new MoveHint { atWaypoint = 1, kind = MoveHintKind.Mantle }, new MoveHint { atWaypoint = 2, kind = MoveHintKind.DoubleJump } }) { JumpRange = 7f };
            Assert.That(Drive(sim, bot, 20f, out _), Is.True, "stopped at waypoint " + bot.TargetIndex + " " + sim.Position);
            Assert.That(sim.Position.Y, Is.EqualTo(RepossessArena.GalleryY).Within(0.3f));
            Assert.That(sim.Position.Z, Is.GreaterThan(15f));
        }

        [Test, Explicit("design aid: searches the façade wall-run sequence")]
        public void SearchFacade()
        {
            var sb = new StringBuilder();
            float bestMax = 0;
            foreach (float x0 in new[] { 33f, 34f })
                foreach (float stick in new[] { 0.2f, 0.4f })
                    for (float jz = -26f; jz <= -8f; jz += 2f)
                        for (float kz = jz + 2f; kz <= 14f; kz += 2f)
                            foreach (float yaw in new[] { 0f, 30f, -30f, 60f })
                            {
                                var r = WallRunSequence(x0, -27.5f, jz, kz, yaw, stick);
                                bestMax = Math.Max(bestMax, r.maxY);
                                if (r.topY > 1f) sb.AppendLine($"x0 {x0} stick {stick} jump {jz} kick {kz} yaw {yaw}: top {r.topY:0.00} max {r.maxY:0.00}");
                            }
            TestContext.WriteLine($"best max height {bestMax:0.00}");
            TestContext.WriteLine(sb.Length == 0 ? "no sequence reaches a gallery" : sb.ToString());
        }

        [Test, Explicit("design aid: searches the landing sizes")]
        public void SearchLanding()
        {
            foreach (float top in new[] { 2.0f, 2.4f, 2.8f })
                foreach (float zMin in new[] { 9f, 10f, 11f })
                {
                    int ok = 0, n = 0; float best = 0;
                    for (float j1 = zMin - 4f; j1 <= zMin; j1 += 0.5f)
                        for (float j2 = zMin + 1f; j2 <= 14.4f; j2 += 0.3f)
                        {
                            n++;
                            float y = LandingClimb(top, zMin, 14.8f, j1, j2);
                            best = Math.Max(best, y);
                            if (y > 5.5f) ok++;
                        }
                    TestContext.WriteLine($"landing top {top} from z {zMin}: best {best:0.00}, successes {ok}/{n}");
                }
        }
    }
}
