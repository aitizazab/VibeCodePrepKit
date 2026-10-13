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
    /// per airtime or limited only by the fire rate (the creator's rule, 2026-10-13; <see cref="BreacherBoostDef.oneBoostPerAirtime"/>).</summary>
    public class BreacherBoostSimTests
    {
        static readonly Vector3 Down = new Vector3(0f, -1f, 0f);

        /// <summary>A body that can be boosted, wired the way the game wires it: each step the charge observes the body
        /// first, then a shot requests its push (<see cref="BreacherKnockback.RequestSelfPush"/>), the engine accepts or
        /// drops it (<see cref="MoveSim.AddPush"/>), and the charge settles on that answer (<see cref="BoostCharge.Commit"/>).
        /// The take-off guard: after a committed lift off the ground the body is still Grounded until the engine's next
        /// step applies the push (in the game a frame can pass with no fixed step), so Observe would re-arm the charge
        /// before the body ever left; until the body has been seen off the ground, "grounded" is not passed on.</summary>
        sealed class Rig
        {
            public readonly MoveSim sim;
            public readonly BoostCharge charge = new BoostCharge();
            public readonly BreacherBoostDef def;
            bool _awaitingTakeoff;

            public Rig(BoxWorld w, Vector3 at, BreacherBoostDef def = null)
            {
                sim = Standing(w, at);
                sim.OutsidePushEnabled = true;
                this.def = def ?? new BreacherBoostDef();
            }

            public void Observe()
            {
                bool grounded = sim.Grounded;
                if (_awaitingTakeoff)
                {
                    if (grounded) grounded = false;
                    else _awaitingTakeoff = false;
                }
                charge.Observe(grounded, sim.Mode == MoveMode.Wallrun);
            }

            /// <summary>Fires the Breacher along <paramref name="aim"/>; true when the push was used (it spent the charge,
            /// or would have) and the engine accepted it.</summary>
            public bool Fire(Vector3 aim)
            {
                Vector3 push = BreacherKnockback.RequestSelfPush(aim, sim.Velocity, sim.Grounded, sim.def.gravity, charge, def, out bool spends);
                bool ok = sim.AddPush(push);
                charge.Commit(spends, ok);
                if (spends && ok && sim.Grounded && push.Y > 0f) _awaitingTakeoff = true;
                return spends && ok;
            }

            /// <summary>One step: observe, maybe fire, then the engine's step. Returns whether a shot boosted.</summary>
            public bool Tick(MoveInput input, bool fire = false)
            {
                Observe();
                bool boosted = fire && Fire(Down);
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
        public void AStraightDownShotFromTheFloorRisesTheBoostHeight()
        {
            var rig = new Rig(Flat(), Vector3.Zero);
            Assert.That(rig.Tick(new MoveInput(), fire: true), Is.True);
            Assert.That(rig.RiseToApex(new MoveInput()), Is.EqualTo(rig.def.boostHeight).Within(0.08f));
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
        public void AGroundLiftIsNotReArmedBeforeTheBodyLeavesTheGround()
        {
            var rig = new Rig(Flat(), Vector3.Zero, OnePerAirtime(true));
            rig.Observe();
            Assert.That(rig.Fire(Down), Is.True);
            Assert.That(rig.charge.Ready, Is.False, "spent by the lift");
            rig.Observe();   // a frame with no engine step: the body is still on the ground
            Assert.That(rig.sim.Grounded, Is.True);
            Assert.That(rig.charge.Ready, Is.False, "not re-armed before take-off");
            rig.sim.Step(new MoveInput(), H);
            Assert.That(rig.sim.Grounded, Is.False);
            rig.Observe();
            Assert.That(rig.charge.Ready, Is.False, "in the air: still spent");
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
