using System;
using System.Collections.Generic;
using System.Numerics;
using JamKit.Movement;
using JamKit.Skyline;
using JamKit.Tests.Movement;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    /// <summary>Task L1: the Halcyon Grand's lobby and staff hallway as data. The ledges carry M1's rules over (the boost
    /// stack is 12 m from every wall and from the other ledges; the lockers and the counter keep their heights); the real
    /// MoveSim walks a BoxWorld built from <see cref="HotelLobby.Solids"/>.</summary>
    public class HotelLobbyTests
    {
        /// <summary>The sequence of M1's wall-run ledge test: the jump this far south of the lockers' south face, the kick this
        /// far, the stick turned this many degrees toward them.</summary>
        const float JumpBefore = 6f, KickBefore = 3f, KickYaw = 45f;

        static BoxWorld World()
        {
            var w = new BoxWorld();
            foreach (var s in HotelLobby.Solids())
            {
                var b = s.box;
                if (s.ramp)
                {
                    if (s.rampAlongX) w.RampX(b.min.X, b.max.X, b.min.Z, b.max.Z, s.rampTopStart, s.rampTopEnd, b.min.Y);
                    else w.Ramp(b.min.X, b.max.X, b.min.Z, b.max.Z, s.rampTopStart, s.rampTopEnd, b.min.Y);
                }
                else w.Add((b.min + b.max) * 0.5f, b.max - b.min, s.runnable);
            }
            return w;
        }

        static FloorSolid Find(string id)
        {
            foreach (var s in HotelLobby.Solids()) if (s.id == id) return s;
            Assert.Fail("no solid '" + id + "'");
            return null;
        }

        /// <summary>Distance in the plan (m) between two boxes' footprints, edge to edge.</summary>
        static float Gap(CoverBox a, CoverBox b)
        {
            float dx = Math.Max(0f, Math.Max(a.min.X - b.max.X, b.min.X - a.max.X));
            float dz = Math.Max(0f, Math.Max(a.min.Z - b.max.Z, b.min.Z - a.max.Z));
            return MathF.Sqrt(dx * dx + dz * dz);
        }

        static CoverBox Footprint(Vector3 top, float size) => new CoverBox
        {
            min = new Vector3(top.X - size * 0.5f, 0f, top.Z - size * 0.5f), max = new Vector3(top.X + size * 0.5f, top.Y, top.Z + size * 0.5f),
        };

        static CoverBox Stack() => Footprint(HotelLobby.BoostStackTop, GalleriaArena.BoostLedgeSize);
        static CoverBox Counter() => Footprint(HotelLobby.PracticeCounterTop, GalleriaArena.PracticeLedgeSize);

        // ---- the shell

        [Test]
        public void TheLobbyIsTwoStoreysAndTheHallwayIs3MWideAnd18MLong()
        {
            Assert.That(HotelLobby.MaxX - HotelLobby.MinX, Is.EqualTo(44f).Within(0.001f));
            Assert.That(HotelLobby.MaxZ - HotelLobby.MinZ, Is.EqualTo(31.6f).Within(0.001f), "z -60..-28.4");
            Assert.That(HotelLobby.Height, Is.EqualTo(9f), "two storeys");
            Assert.That(HotelLobby.MaxZ, Is.EqualTo(-28.4f), "its north wall is the hall's south wall (outer face)");

            var hall = Array.Find(HotelLobby.Zones(), z => z.id == "hallway");
            Assert.That(hall.maxX - hall.minX, Is.EqualTo(3f).Within(0.001f));
            Assert.That(hall.maxZ - hall.minZ, Is.EqualTo(18f).Within(0.001f));
            // the walls' inner faces agree with the zone
            Assert.That(Find("hallway-wall-e").box.min.X - Find("hallway-wall-w").box.max.X, Is.EqualTo(3f).Within(0.001f));
            Assert.That(hall.maxZ - Find("hallway-wall-end").box.max.Z, Is.EqualTo(18f).Within(0.001f));
            Assert.That(HotelLobby.PlayerStart.Z, Is.InRange(hall.minZ, hall.maxZ));
            Assert.That(HotelLobby.PlayerStart.X, Is.InRange(hall.minX, hall.maxX));
        }

        [Test]
        public void EverythingStandsInsideTheLobby()
        {
            foreach (var p in new[] { HotelLobby.PracticeBot, HotelLobby.BoostStackTop, HotelLobby.PracticeCounterTop, HotelLobby.DeskCenter, HotelLobby.ChandelierPosition })
            {
                Assert.That(p.X, Is.InRange(HotelLobby.MinX, HotelLobby.MaxX));
                Assert.That(p.Z, Is.InRange(HotelLobby.MinZ, HotelLobby.MaxZ));
                Assert.That(p.Y, Is.InRange(0f, HotelLobby.Height));
            }
            Assert.That(HotelLobby.ChandelierPosition.Y, Is.GreaterThan(HotelLobby.BoostStackTop.Y + 2f), "the chandelier hangs over the stack's top");
            Assert.That(HotelLobby.WallRunStart.Z, Is.LessThan(HotelLobby.WallRunLockers.min.Z));
        }

        [Test]
        public void NoTwoSolidsPenetrateByMoreThan2Cm()
        {
            var all = HotelLobby.Solids();
            Assert.That(all.Length, Is.GreaterThan(20));
            var ids = new HashSet<string>();
            foreach (var s in all) Assert.That(ids.Add(s.id), Is.True, "unique id " + s.id);
            for (int i = 0; i < all.Length; i++)
                for (int j = i + 1; j < all.Length; j++)
                {
                    var a = all[i].box; var b = all[j].box;
                    float ox = Math.Min(a.max.X, b.max.X) - Math.Max(a.min.X, b.min.X);
                    float oy = Math.Min(a.max.Y, b.max.Y) - Math.Max(a.min.Y, b.min.Y);
                    float oz = Math.Min(a.max.Z, b.max.Z) - Math.Max(a.min.Z, b.min.Z);
                    if (ox <= 0f || oy <= 0f || oz <= 0f) continue;
                    Assert.That(Math.Min(ox, Math.Min(oy, oz)), Is.LessThanOrEqualTo(0.02f), $"{all[i].id} penetrates {all[j].id}");
                }
        }

        [Test]
        public void OnlyTheWestWallRuns_AndEveryWallHasADeliberateValue()
        {
            // the lockers' lesson needs one runnable wall; the stack must not be reachable by any wall-run
            var runnable = new List<string>();
            foreach (var s in HotelLobby.Solids()) if (s.runnable) runnable.Add(s.id);
            Assert.That(runnable, Is.EqualTo(new[] { "lobby-wall-w" }));
            var west = Find("lobby-wall-w").box;
            Assert.That(west.max.X, Is.EqualTo(HotelLobby.MinX).Within(0.001f), "its face is the interior's west edge");
            Assert.That(west.max.Y - west.min.Y, Is.GreaterThanOrEqualTo(HotelLobby.Height), "tall enough to run");
        }

        [Test]
        public void ThereIsNoTallFreeStandingCoverInTheLobby()
        {
            // carts are low cover (<= 1.2 m); the rest are named architecture or the practice ledges
            foreach (var s in HotelLobby.Solids())
                if (s.role == "cart") Assert.That(s.box.max.Y - s.box.min.Y, Is.LessThanOrEqualTo(1.2f), s.id);
        }

        // ---- the ledges (M1's rules, carried over)

        [Test]
        public void TheBoostStackStands12MFromEveryWallAndFromTheOtherLedges()
        {
            var c = HotelLobby.BoostStackTop;
            float h = GalleriaArena.BoostLedgeSize * 0.5f;
            float clearance = Math.Min(Math.Min(c.X - h - HotelLobby.MinX, HotelLobby.MaxX - (c.X + h)),
                                       Math.Min(c.Z - h - HotelLobby.MinZ, HotelLobby.MaxZ - (c.Z + h)));
            Assert.That(clearance, Is.GreaterThanOrEqualTo(GalleriaArena.LedgeWallClearance), "no wall-run reaches it, so only a boost does (6.8)");
            // and from every wall that can be run
            foreach (var s in HotelLobby.Solids())
                if (s.runnable) Assert.That(Gap(Stack(), s.box), Is.GreaterThanOrEqualTo(GalleriaArena.LedgeWallClearance), s.id);
            Assert.That(Gap(Stack(), Counter()), Is.GreaterThanOrEqualTo(12f), "stack and counter, edge to edge");
            Assert.That(Gap(Stack(), HotelLobby.WallRunLockers), Is.GreaterThanOrEqualTo(12f), "stack and lockers, edge to edge");
            Assert.That(Gap(Counter(), HotelLobby.WallRunLockers), Is.GreaterThanOrEqualTo(12f), "counter and lockers");
            // the counter and the lockers keep the sizes M1's tests pin
            Assert.That(HotelLobby.WallRunLockers.max.Y, Is.EqualTo(4.6f));
            Assert.That(HotelLobby.PracticeCounterTop.Y, Is.EqualTo(3.5f));
            Assert.That(HotelLobby.WallRunLockers.min.X - HotelLobby.MinX, Is.EqualTo(1.5f).Within(0.001f), "1.5 m off the west wall");
        }

        [Test]
        public void ThePracticeCounterTakesADoubleJumpAndAMantleAndNoLess()
        {
            var top = HotelLobby.PracticeCounterTop;
            float s = GalleriaArena.PracticeLedgeSize;
            Func<bool, float> climb = doubleJump =>
            {
                // facing it (yaw 180, toward -Z) from 0.8 m north of its north face
                var sim = MoveTestKit.Standing(World(), new Vector3(top.X, 0f, top.Z + s * 0.5f + 0.8f), null, 180f);
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
        }

        /// <summary>The demo's boost onto the stack, as ArenaDemoBoostTests: 0.8 m short of its south face, facing it; jump; the
        /// double jump at the apex; at that apex one Breacher shot pitched 85 degrees down; forward until it mantles.</summary>
        static float BoostOntoTheStack(bool shoot, int lateSteps)
        {
            var top = HotelLobby.BoostStackTop;
            float s = GalleriaArena.BoostLedgeSize;
            var sim = MoveTestKit.Standing(World(), new Vector3(top.X, 0f, top.Z - s * 0.5f - 0.8f));
            sim.OutsidePushEnabled = true;
            var charge = new BoostCharge();
            var def = new BreacherBoostDef { oneBoostPerAirtime = false };
            void Observe() => charge.Observe(sim.Grounded, sim.Mode == MoveMode.Wallrun);
            void ToApex()
            {
                for (int i = 0; i < 600; i++)
                {
                    Observe();
                    sim.Step(MoveTestKit.Forward(), MoveTestKit.H);
                    if (!sim.Grounded && sim.Velocity.Y <= 0f) return;
                }
            }
            void Late() { for (int i = 0; i < lateSteps; i++) { Observe(); sim.Step(MoveTestKit.Forward(), MoveTestKit.H); } }
            Observe();
            sim.Step(MoveTestKit.Forward().With(jump: true), MoveTestKit.H);
            ToApex(); Late();
            Observe();
            sim.Step(MoveTestKit.Forward().With(jump: true), MoveTestKit.H);
            ToApex(); Late();
            Observe();
            if (shoot)
            {
                float p = 85f * MathF.PI / 180f;
                Vector3 push = BreacherKnockback.RequestSelfPush(new Vector3(0f, -MathF.Sin(p), MathF.Cos(p)), sim.Velocity, false, sim.def.gravity, charge, def, out bool spends);
                charge.Commit(spends, sim.AddPush(push));
            }
            bool mantled = false;
            for (float t = 0f; t < 3f; t += MoveTestKit.H)
            {
                Observe();
                sim.Step(MoveTestKit.Forward(), MoveTestKit.H);
                mantled |= sim.Mode == MoveMode.Mantle;
                if (mantled && sim.Grounded) break;
            }
            return mantled && sim.Grounded ? sim.Position.Y : float.NaN;
        }

        [TestCase(0)]
        [TestCase(4)]
        public void TheBoostLandsOnTheStack_AndWithoutTheShotItDoesNot(int lateSteps)
        {
            Assert.That(BoostOntoTheStack(true, lateSteps), Is.EqualTo(HotelLobby.BoostStackTop.Y).Within(0.1f), "boosted onto the top");
            float y = BoostOntoTheStack(false, lateSteps);
            Assert.That(float.IsNaN(y) || y < HotelLobby.BoostStackTop.Y - 0.5f, Is.True, "jump + double jump + mantle fall short");
        }

        /// <summary>WallRunLedgeTests.Run over the lobby: from the wall-run start facing north, sprint with the stick a little
        /// into the wall; jump at jumpZ; on the wall hold along it; at kickZ kick off it toward the lockers; double jump at the
        /// apex; mantle. Returns the feet's height once standing on top (0 if not).</summary>
        static float WallRunSequence(float jumpZ, float kickZ, float kickYaw, bool kick = true, bool doubleJump = true)
        {
            var sim = MoveTestKit.Standing(World(), HotelLobby.WallRunStart);
            bool jumped = false, kickedOff = false, dj = false;
            var along = new MoveInput { move = new Vector2(-0.2f, 1f), yaw = 0f, sprintHeld = true };
            float k = kickYaw * MathF.PI / 180f;
            var toward = new MoveInput { move = new Vector2(MathF.Sin(k), MathF.Cos(k)), yaw = 0f, sprintHeld = true };
            for (float t = 0f; t < 5f; t += MoveTestKit.H)
            {
                var input = kickedOff ? toward : along;
                if (!jumped && sim.Position.Z >= jumpZ) { input.jumpPressed = input.jumpHeld = true; jumped = true; }
                else if (kick && jumped && !kickedOff && sim.Mode == MoveMode.Wallrun && sim.Position.Z >= kickZ)
                {
                    input = toward;
                    input.jumpPressed = input.jumpHeld = true;
                    kickedOff = true;
                }
                else if (doubleJump && kickedOff && !dj && !sim.Grounded && sim.Mode == MoveMode.Air && sim.Velocity.Y <= 0f)
                {
                    input.jumpPressed = input.jumpHeld = true;
                    dj = true;
                }
                sim.Step(input, MoveTestKit.H);
                if (sim.Grounded && sim.Position.Y > 1f) return sim.Position.Y;
                if (jumped && sim.Grounded && sim.Position.Y < 0.1f && t > 1.5f) break;
            }
            return 0f;
        }

        [Test]
        public void RunningTheWestWallAndKickingOffItReachesTheLockersAndRobustly()
        {
            float face = HotelLobby.WallRunLockers.min.Z;
            foreach (float dj in new[] { 0f, -1f, 1f })
                foreach (float dk in new[] { 0f, -0.5f, 0.5f })
                {
                    float y = WallRunSequence(face - JumpBefore + dj, face - KickBefore + dk, KickYaw);
                    Assert.That(y, Is.EqualTo(HotelLobby.WallRunLockers.max.Y).Within(0.1f), $"jump {dj:+0;-0;0} m, kick {dk:+0.0;-0.0;0} m: on top");
                }
        }

        [Test]
        public void TheLockersFallShortWithoutTheWallOrTheKick()
        {
            float face = HotelLobby.WallRunLockers.min.Z;
            Assert.That(WallRunSequence(face - JumpBefore, face - KickBefore, KickYaw, kick: false, doubleJump: false),
                Is.LessThan(1f), "running the wall past them without the kick: not on top");
            // the floor: facing their east face from 0.8 m, jump, double jump at the apex, mantle (4.15 < 4.6)
            var t = HotelLobby.WallRunLockers;
            var sim = MoveTestKit.Standing(World(), new Vector3(t.max.X + 0.38f + 0.8f, 0f, (t.min.Z + t.max.Z) * 0.5f), null, -90f);
            var fwd = MoveTestKit.Forward(-90f);
            sim.Step(fwd.With(jump: true), MoveTestKit.H);
            for (int i = 0; i < 600 && !(!sim.Grounded && sim.Velocity.Y <= 0f); i++) sim.Step(fwd, MoveTestKit.H);
            sim.Step(fwd.With(jump: true), MoveTestKit.H);
            float best = 0f;
            for (float s = 0f; s < 3f; s += MoveTestKit.H)
            {
                sim.Step(fwd, MoveTestKit.H);
                if (sim.Grounded) best = Math.Max(best, sim.Position.Y);
            }
            Assert.That(best, Is.LessThan(1f), "a jump, a double jump and a mantle from the floor fall short of 4.6 m");
        }

        [Test]
        public void TheBellhopRigClearsTheCeiling()
        {
            Assert.That(HotelLobby.GantryTopY, Is.LessThan(HotelLobby.Height - 0.5f), "the rail's top clears the ceiling");
            var rail = Find("bellhop-rail").box;
            Assert.That(rail.max.Y, Is.EqualTo(HotelLobby.GantryTopY));
            Assert.That(rail.min.Y, Is.GreaterThan(HotelLobby.PracticeBot.Y + 2f), "the practice bot hangs well under it");
            Assert.That(HotelLobby.PracticeBot.X, Is.InRange(rail.min.X, rail.max.X), "under the rail");
            Assert.That(HotelLobby.PracticeBot.Z, Is.InRange(rail.min.Z, rail.max.Z));
        }

        // ---- the lift, the mezzanine, the walk

        [Test]
        public void TheLiftCabIs4By4AndItsNorthFaceIsTheHallsSouthWallLine()
        {
            var c = HotelLobby.LiftCab;
            Assert.That(c.max.X - c.min.X, Is.EqualTo(4f).Within(0.001f));
            Assert.That(c.max.Z - c.min.Z, Is.EqualTo(4f).Within(0.001f));
            Assert.That(c.max.Z, Is.EqualTo(HotelLobby.MaxZ).Within(0.001f), "against the hall's south wall (outer face -28.4)");
            Assert.That((c.min.X + c.max.X) * 0.5f, Is.EqualTo(0f).Within(0.001f), "centred on the lift line, x 0");
            var cab = Array.Find(HotelLobby.Zones(), z => z.id == "cab");
            Assert.That(cab.minX, Is.EqualTo(c.min.X)); Assert.That(cab.maxX, Is.EqualTo(c.max.X));
            Assert.That(cab.minZ, Is.EqualTo(c.min.Z)); Assert.That(cab.maxZ, Is.EqualTo(c.max.Z));
            // the walls bound the inside exactly and the doors close its north face
            Assert.That(Find("cab-wall-e").box.min.X - Find("cab-wall-w").box.max.X, Is.EqualTo(4f).Within(0.001f));
            Assert.That(Find("cab-doors-n").box.max.Z, Is.EqualTo(c.max.Z).Within(0.001f));
        }

        [Test]
        public void FromTheStartYouCanWalkTheHallwayAcrossTheLobbyAndIntoTheLift()
        {
            var sim = MoveTestKit.Standing(World(), HotelLobby.PlayerStart);
            var fwd = MoveTestKit.Forward();
            fwd.sprintHeld = false;
            float t = MoveTestKit.Run(sim, fwd, 20f, s => s.Position.Z > HotelLobby.LiftCab.min.Z + 1.5f);
            var p = sim.Position;
            Assert.That(t, Is.LessThan(19.9f), "got there");
            Assert.That(p.X, Is.InRange(HotelLobby.LiftCab.min.X, HotelLobby.LiftCab.max.X));
            Assert.That(p.Z, Is.InRange(HotelLobby.LiftCab.min.Z, HotelLobby.LiftCab.max.Z));
            Assert.That(p.Y, Is.EqualTo(0f).Within(0.05f));
        }

        [Test]
        public void TheMezzanineStairsAreWalkableAndLeadToTheLounge()
        {
            var st = Find("mezzanine-stairs");
            float run = st.box.max.Z - st.box.min.Z;
            float slope = MathF.Atan((st.rampTopEnd - st.rampTopStart) / run) * 180f / MathF.PI;
            Assert.That(slope, Is.LessThanOrEqualTo(40f), "stairs as a ramp");
            Assert.That(st.TopAt(19f, st.box.max.Z), Is.EqualTo(Find("mezzanine-deck").box.max.Y).Within(0.001f), "the top step is the deck");
            var sim = MoveTestKit.Standing(World(), new Vector3(19f, 0f, st.box.min.Z + 0.4f));
            MoveTestKit.Run(sim, MoveTestKit.Forward(), 12f, s => s.Position.Z > -46f);
            Assert.That(sim.Position.Y, Is.EqualTo(4.5f).Within(0.1f), "on the mezzanine deck");
            Assert.That(sim.Position.Z, Is.GreaterThan(-52f));
        }

        [Test]
        public void ZonesCoverTheAreasAndTheHallwayIsTheService()
        {
            var zones = HotelLobby.Zones();
            var byId = new Dictionary<string, FloorZone>();
            foreach (var z in zones) { Assert.That(byId.ContainsKey(z.id), Is.False, "unique " + z.id); byId[z.id] = z; }
            Assert.That(byId.Keys, Is.EquivalentTo(new[] { "hallway", "lobby", "mezzanine", "cab" }));
            Assert.That(byId["hallway"].level, Is.EqualTo(SurfaceLevel.Corridor), "dim, colour-free service space");
            Assert.That(byId["lobby"].level, Is.EqualTo(SurfaceLevel.Lobby));
            Assert.That(byId["mezzanine"].y, Is.EqualTo(4.5f));
            Assert.That(HotelLobby.DeskGreetRange, Is.EqualTo(8f));
            // the desk and the lockers stand in the lobby zone
            var lobby = byId["lobby"];
            Assert.That(HotelLobby.WallRunLockers.min.X, Is.GreaterThanOrEqualTo(lobby.minX));
            Assert.That(HotelLobby.WallRunLockers.max.Z, Is.LessThanOrEqualTo(lobby.maxZ));
        }
    }
}
