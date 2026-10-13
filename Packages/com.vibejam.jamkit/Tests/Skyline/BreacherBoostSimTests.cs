using System;
using System.Numerics;
using JamKit.Movement;
using JamKit.Skyline;
using JamKit.Tests.Movement;
using NUnit.Framework;
using static JamKit.Tests.Movement.MoveTestKit;

namespace JamKit.Tests.Skyline
{
    /// <summary>The Breacher's boost in the real movement engine (Task 7's rules through Task 12's hook), and the arena's
    /// boost ledge (6.8: "an optional ledge reachable only with a boost"). The ledge checks hold whether the boost is one
    /// per airtime or limited only by the fire rate (the creator's rule, 2026-10-13; <see cref="BreacherBoostDef.oneBoostPerAirtime"/>).
    /// Braced shots (on the ground, crouched, or wall-running) are mostly eaten and never lift; jump and fire together give the
    /// shot first, then a normal ground jump (the creator's call, 2026-10-13 evening).</summary>
    public class BreacherBoostSimTests
    {
        static readonly Vector3 Down = new Vector3(0f, -1f, 0f);

        /// <summary>A body that can be boosted, wired the way the game wires it: each step the charge observes the body
        /// first, then a shot requests its push (<see cref="BreacherKnockback.RequestSelfPush"/>), the engine accepts or
        /// drops it (<see cref="MoveSim.AddPush"/>), and the charge settles on that answer (<see cref="BoostCharge.Commit"/>).
        /// Braced, as SkylinePlayer: grounded (crouching included) or wall-running. A shot fires before the engine's step, as
        /// SkylinePlayer runs before MovementController.</summary>
        sealed class Rig
        {
            public readonly MoveSim sim;
            public readonly BoostCharge charge = new BoostCharge();
            public readonly BreacherBoostDef def;
            public Vector3 lastPush;

            public Rig(BoxWorld w, Vector3 at, BreacherBoostDef def = null, Vector3 velocity = default)
            {
                sim = Standing(w, at, velocity: velocity);
                sim.OutsidePushEnabled = true;
                this.def = def ?? new BreacherBoostDef();
            }

            public bool Braced => sim.Grounded || sim.Mode == MoveMode.Wallrun;

            public void Observe() => charge.Observe(sim.Grounded, sim.Mode == MoveMode.Wallrun);

            /// <summary>Fires the Breacher along <paramref name="aim"/>; true when the push was used (it spent the charge,
            /// or would have) and the engine accepted it.</summary>
            public bool Fire(Vector3 aim)
            {
                Vector3 push = BreacherKnockback.RequestSelfPush(aim, sim.Velocity, Braced, sim.def.gravity, charge, def, out bool spends);
                lastPush = push;
                bool ok = sim.AddPush(push);
                charge.Commit(spends, ok);
                return spends && ok;
            }

            /// <summary>One step: observe, maybe fire, then the engine's step. Returns whether a shot boosted.</summary>
            public bool Tick(MoveInput input, bool fire = false) => Tick(input, fire, Down);

            public bool Tick(MoveInput input, bool fire, Vector3 aim)
            {
                Observe();
                bool boosted = fire && Fire(aim);
                sim.Step(input, H);
                return boosted;
            }

            /// <summary>Steps until the body starts falling (or 5 s); returns the highest feet height.</summary>
            public float RiseToApex(MoveInput input)
            {
                float top = sim.Position.Y;
                for (int i = 0; i < 600; i++)
                {
                    Tick(input);
                    top = Math.Max(top, sim.Position.Y);
                    if (!sim.Grounded && sim.Velocity.Y <= 0f) break;
                }
                return top;
            }

            /// <summary>Steps until <paramref name="until"/> or the time runs out.</summary>
            public void Run(MoveInput input, float seconds, Func<MoveSim, bool> until)
            {
                for (float t = 0f; t < seconds; t += H)
                {
                    Tick(input);
                    if (until(sim)) return;
                }
            }
        }

        static BreacherBoostDef OnePerAirtime(bool one) => new BreacherBoostDef { oneBoostPerAirtime = one };

        [Test]
        public void AStraightDownShotAtTheJumpsApexRisesTheBoostHeight()
        {
            var rig = new Rig(Flat(), Vector3.Zero);
            rig.Tick(new MoveInput().With(jump: true));
            float jumpTop = rig.RiseToApex(new MoveInput());
            Assert.That(rig.Tick(new MoveInput(), fire: true), Is.True);
            Assert.That(rig.RiseToApex(new MoveInput()) - jumpTop, Is.EqualTo(rig.def.boostHeight).Within(0.08f));
        }

        // The creator's call (2026-10-13 evening): braced shots are mostly eaten; the boost is felt only mid-air.

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void AShotOnTheGroundStandingOrCrouchedIsMostlyEatenAndNeverLifts(bool crouched, bool onePerAirtime)
        {
            var rig = new Rig(Flat(), Vector3.Zero, OnePerAirtime(onePerAirtime));
            var still = new MoveInput().With(crouchHeld: crouched);
            for (int i = 0; i < 30; i++) rig.Tick(still);
            Assert.That(rig.sim.Grounded, Is.True);
            foreach (var aim in new[] { Down, Vector3.Normalize(new Vector3(0f, -1f, 1f)), new Vector3(0f, 0f, 1f) })
            {
                Assert.That(rig.Tick(still, true, aim), Is.False, "a braced shot uses no boost");
                Assert.That(rig.lastPush.Y, Is.EqualTo(0f), "no lift");
                Assert.That(rig.lastPush.Length(), Is.LessThanOrEqualTo(rig.def.bracedScale * rig.def.selfPushSpeed + 1e-5f));
                Assert.That(rig.charge.Ready, Is.True, "the charge is untouched");
                for (int i = 0; i < 10; i++)
                {
                    Assert.That(rig.sim.Grounded, Is.True, "still on the floor");
                    rig.Tick(still);
                }
            }
        }

        [Test]
        public void AShotDuringAWallrunIsMostlyEatenAndKeepsTheRun()
        {
            var w = new BoxWorld();
            w.Add(new Vector3(2.5f, -10f, 30f), new Vector3(1f, 40f, 60f));
            var rig = new Rig(w, new Vector3(1.3f, 3f, 1f), velocity: new Vector3(0f, 0f, 10f));
            rig.Run(Forward(), 0.2f, s => s.Mode == MoveMode.Wallrun);
            Assert.That(rig.sim.Mode, Is.EqualTo(MoveMode.Wallrun));
            float vyBefore = rig.sim.Velocity.Y;
            // forward and down along the wall: in the air its reverse (up 0.6 > minLift) would lift
            var aim = Vector3.Normalize(new Vector3(0f, -0.6f, 0.8f));
            Assert.That(rig.Tick(Forward(), true, aim), Is.False, "a braced shot uses no boost");
            Assert.That(rig.lastPush.Y, Is.EqualTo(0f), "no lift");
            Assert.That(rig.lastPush.Length(), Is.LessThanOrEqualTo(rig.def.bracedScale * rig.def.selfPushSpeed + 1e-5f));
            Assert.That(rig.sim.Mode, Is.EqualTo(MoveMode.Wallrun), "the wall eats it: still running");
            Assert.That(rig.sim.Velocity.Y, Is.LessThanOrEqualTo(vyBefore + 1e-4f), "nothing pushed it up");
        }

        [Test]
        public void JumpAndFireTogetherGiveTheShotFirstThenANormalGroundJump()
        {
            var plain = new Rig(Flat(), Vector3.Zero);
            for (int i = 0; i < 30; i++) plain.Tick(new MoveInput());
            plain.Tick(new MoveInput().With(jump: true));
            Assert.That(plain.sim.Events.jumped, Is.True);
            float plainTop = plain.RiseToApex(new MoveInput());

            var rig = new Rig(Flat(), Vector3.Zero);
            for (int i = 0; i < 30; i++) rig.Tick(new MoveInput());
            Assert.That(rig.sim.Grounded, Is.True);
            // one step: the shot straight down, then the jump, as SkylinePlayer (-50) runs before MovementController (-40)
            Assert.That(rig.Tick(new MoveInput().With(jump: true), true, Down), Is.False, "the shot was braced: no boost");
            Assert.That(rig.sim.Events.jumped, Is.True, "a ground jump");
            Assert.That(rig.sim.Events.doubleJumped, Is.False);
            Assert.That(rig.sim.Grounded, Is.False);
            Assert.That(rig.sim.DoubleJumpAvailable, Is.True, "the double jump is kept");
            float top = rig.RiseToApex(new MoveInput());
            Assert.That(top, Is.EqualTo(plainTop).Within(0.02f), "the same apex as a plain jump");
            Assert.That(rig.sim.DoubleJumpAvailable, Is.True, "still there at the apex");

            // a mid-air shot afterwards still boosts
            Assert.That(rig.Tick(new MoveInput(), fire: true), Is.True, "mid-air: the boost");
            float boostTop = rig.RiseToApex(new MoveInput());
            Assert.That(boostTop - top, Is.EqualTo(rig.def.boostHeight).Within(0.1f));
            Assert.That(rig.sim.DoubleJumpAvailable, Is.True, "the boost doesn't use the double jump");
            rig.Tick(new MoveInput().With(jump: true));
            Assert.That(rig.sim.Events.doubleJumped, Is.True, "and it's still there to use");
        }

        [Test]
        public void OneBoostAnAirtimeAndLandingGivesItBack()
        {
            var rig = new Rig(Flat(), Vector3.Zero, OnePerAirtime(true));
            rig.Tick(Forward().With(jump: true));
            float jumpTop = rig.RiseToApex(Forward());
            Assert.That(rig.Tick(Forward(), fire: true), Is.True);
            float boostTop = rig.RiseToApex(Forward());
            Assert.That(boostTop - jumpTop, Is.EqualTo(rig.def.boostHeight).Within(0.1f));
            Assert.That(rig.Tick(Forward(), fire: true), Is.False, "with one boost an airtime, the second shot gives nothing");
            rig.Run(Forward(), 3f, s => s.Grounded);
            rig.Observe();
            Assert.That(rig.charge.Ready, Is.True);
        }

        [Test]
        public void LimitedOnlyByTheFireRateASecondAirShotLiftsAgain()
        {
            var rig = new Rig(Flat(), Vector3.Zero, OnePerAirtime(false));
            rig.Tick(Forward().With(jump: true));
            rig.RiseToApex(Forward());
            Assert.That(rig.Tick(Forward(), fire: true), Is.True);
            float firstTop = rig.RiseToApex(Forward());
            Assert.That(rig.Tick(Forward(), fire: true), Is.True, "no airtime limit");
            float secondTop = rig.RiseToApex(Forward());
            Assert.That(secondTop - firstTop, Is.EqualTo(rig.def.boostHeight).Within(0.1f));
        }

        [Test]
        public void TheBoostNeverGivesBackTheDoubleJump()
        {
            var rig = new Rig(Flat(), Vector3.Zero);
            rig.Tick(Forward().With(jump: true));
            for (int i = 0; i < 24; i++) rig.Tick(Forward());
            rig.Tick(Forward().With(jump: true));
            Assert.That(rig.sim.Events.doubleJumped, Is.True);
            Assert.That(rig.sim.DoubleJumpAvailable, Is.False);
            Assert.That(rig.Tick(Forward(), fire: true), Is.True);
            Assert.That(rig.sim.DoubleJumpAvailable, Is.False);
        }

        [Test]
        public void AShotDuringAMantleIsRefusedAndKeepsTheCharge()
        {
            var w = Flat();
            w.Floor(-10f, 10f, 5f, 30f, 1.5f, 2.5f);
            var rig = new Rig(w, Vector3.Zero, OnePerAirtime(true));
            rig.Run(Forward(), 2f, s => s.Mode == MoveMode.Mantle);
            Assert.That(rig.sim.Mode, Is.EqualTo(MoveMode.Mantle));
            Assert.That(rig.Fire(Down), Is.False, "the engine drops a push in a mantle");
            Assert.That(rig.charge.Ready, Is.True, "a refused push costs nothing");
        }

        [Test]
        public void WithOneBoostAnAirtimeAGroundShotSpendsNothingAndTheAirShotStillBoostsOnce()
        {
            var rig = new Rig(Flat(), Vector3.Zero, OnePerAirtime(true));
            rig.Observe();
            Assert.That(rig.Fire(Down), Is.False, "braced: no boost");
            Assert.That(rig.charge.Ready, Is.True, "nothing spent");
            rig.sim.Step(new MoveInput(), H);
            Assert.That(rig.sim.Grounded, Is.True, "no lift off the floor");
            rig.Tick(new MoveInput().With(jump: true));
            rig.RiseToApex(new MoveInput());
            Assert.That(rig.Tick(new MoveInput(), fire: true), Is.True, "the air shot boosts");
            Assert.That(rig.Tick(new MoveInput(), fire: true), Is.False, "one boost an airtime");
            rig.Run(new MoveInput(), 3f, s => s.Grounded);
            rig.Observe();
            Assert.That(rig.charge.Ready, Is.True, "a real landing gives it back");
        }

        /// <summary>The warm-up hall's kiosk, its top at <paramref name="topY"/> (the boost ledge's), sides not runnable; the
        /// body 0.8 m short of its south face, facing it. Jump, double jump at the jump's apex, and (with a boost) a
        /// straight-down Breacher shot at the double jump's apex; then forward until it mantles on top or 3 s pass.</summary>
        static bool ReachesTheLedge(bool boost, bool onePerAirtime, float topY)
        {
            var w = new BoxWorld();
            w.Floor(GalleriaArena.WarmupMinX, GalleriaArena.WarmupMaxX, GalleriaArena.WarmupMinZ, GalleriaArena.WarmupMaxZ, 0f);
            var top = new Vector3(GalleriaArena.BoostLedgeTop.X, topY, GalleriaArena.BoostLedgeTop.Z);
            float s = GalleriaArena.BoostLedgeSize;
            w.Add(new Vector3(top.X, top.Y * 0.5f, top.Z), new Vector3(s, top.Y, s), runnable: false);
            var rig = new Rig(w, new Vector3(top.X, 0f, top.Z - s * 0.5f - 0.8f), OnePerAirtime(onePerAirtime));
            rig.Tick(Forward().With(jump: true));
            rig.RiseToApex(Forward());
            rig.Tick(Forward().With(jump: true));   // the double jump at the jump's apex
            Assert.That(rig.sim.Events.doubleJumped, Is.True, "double jumped");
            rig.RiseToApex(Forward());
            if (boost) Assert.That(rig.Tick(Forward(), fire: true), Is.True, "boosted");
            bool mantled = false;
            rig.Run(Forward(), 3f, x => { mantled |= x.Mode == MoveMode.Mantle; return mantled && x.Grounded; });
            return mantled && rig.sim.Position.Y > top.Y - 0.1f;
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TheWarmupLedgeIsOutOfReachWithoutABoost(bool onePerAirtime)
        {
            Assert.That(ReachesTheLedge(false, onePerAirtime, GalleriaArena.BoostLedgeTop.Y), Is.False);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TheWarmupLedgeIsReachableWithABoostAtTheDoubleJumpsApex(bool onePerAirtime)
        {
            Assert.That(ReachesTheLedge(true, onePerAirtime, GalleriaArena.BoostLedgeTop.Y), Is.True);
        }
    }
}
