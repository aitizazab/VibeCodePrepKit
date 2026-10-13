using System;
using System.Numerics;
using JamKit.Movement;
using JamKit.Skyline;
using JamKit.Tests.Movement;
using NUnit.Framework;
using static JamKit.Tests.Movement.MoveTestKit;

namespace JamKit.Tests.Skyline
{
    /// <summary>The arena demo's boost onto the warm-up kiosk (Task 16), step for step as SkylineArenaDemo drives the player:
    /// standing 0.8 m short of the kiosk's south face, facing it, sprint and forward held throughout; jump; at the jump's apex
    /// the double jump; at the double jump's apex the view pitched down as far as the player allows (SkylinePlayer.MaxPitch,
    /// 85 degrees, not straight down: the aim keeps a little forward part, so the push also nudges the body back) and one
    /// Breacher shot; then forward until it mantles onto the top. The real MoveSim, the real MoveDef and the Breacher's
    /// shipped boost data; the push goes through the order the player uses (Request, AddPush, Commit).</summary>
    public class ArenaDemoBoostTests
    {
        /// <summary>SkylinePlayer.MaxPitch (Unity side): the most the view pitches down.</summary>
        const float DemoPitch = 85f;

        static Vector3 AimAt(float yawDeg, float pitchDeg)
        {
            float y = yawDeg * MathF.PI / 180f, p = pitchDeg * MathF.PI / 180f;
            // Unity's convention, as SkylinePlayer.AimDirection: positive pitch looks down, yaw 0 looks along +Z.
            return new Vector3(MathF.Sin(y) * MathF.Cos(p), -MathF.Sin(p), MathF.Cos(y) * MathF.Cos(p));
        }

        static BoxWorld WarmupWithKiosk()
        {
            var w = new BoxWorld();
            w.Floor(GalleriaArena.WarmupMinX, GalleriaArena.WarmupMaxX, GalleriaArena.WarmupMinZ, GalleriaArena.WarmupMaxZ, 0f);
            var top = GalleriaArena.BoostLedgeTop;
            float s = GalleriaArena.BoostLedgeSize;
            w.Add(new Vector3(top.X, top.Y * 0.5f, top.Z), new Vector3(s, top.Y, s), runnable: false);
            return w;
        }

        /// <summary>Runs the demo's sequence; returns the feet's height once it has mantled and stands (or NaN), and whether
        /// the shot boosted.
        /// <paramref name="lateSteps"/>: engine steps (1/120 s) the double jump and the shot come after each apex (the demo sees
        /// the apex a frame late and fires a frame after setting the pitch: about 2 and 4 steps at 60 fps).
        /// <paramref name="shoot"/> false is the control: the same jumps without the shot.</summary>
        static float RunSequence(bool onePerAirtime, int lateSteps, bool shoot, out bool boosted, out bool mantled)
        {
            var top = GalleriaArena.BoostLedgeTop;
            float s = GalleriaArena.BoostLedgeSize;
            var sim = Standing(WarmupWithKiosk(), new Vector3(top.X, 0f, top.Z - s * 0.5f - 0.8f));
            sim.OutsidePushEnabled = true;
            var charge = new BoostCharge();
            var def = new BreacherBoostDef { oneBoostPerAirtime = onePerAirtime };

            void Observe() => charge.Observe(sim.Grounded, sim.Mode == MoveMode.Wallrun);

            void StepUntilApex()
            {
                for (int i = 0; i < 600; i++)
                {
                    Observe();
                    sim.Step(Forward(), H);
                    if (!sim.Grounded && sim.Velocity.Y <= 0f) return;
                }
            }

            Observe();
            sim.Step(Forward().With(jump: true), H);
            void Late()
            {
                for (int i = 0; i < lateSteps; i++)
                {
                    Observe();
                    sim.Step(Forward(), H);
                }
            }

            StepUntilApex();
            Late();
            Observe();
            sim.Step(Forward().With(jump: true), H);   // the double jump at the jump's apex
            Assert.That(sim.Events.doubleJumped, Is.True, "double jumped");
            StepUntilApex();
            Late();

            // the shot, at the double jump's apex
            Observe();
            boosted = false;
            if (shoot)
            {
                bool braced = sim.Grounded || sim.Mode == MoveMode.Wallrun;
                Assert.That(braced, Is.False, "the shot is mid-air");
                Vector3 push = BreacherKnockback.RequestSelfPush(AimAt(0f, DemoPitch), sim.Velocity, braced, sim.def.gravity, charge, def, out bool spends);
                bool ok = sim.AddPush(push);
                charge.Commit(spends, ok);
                boosted = spends && ok && push.Y > 0f;
            }

            mantled = false;
            for (float t = 0f; t < 3f; t += H)
            {
                Observe();
                sim.Step(Forward(), H);
                mantled |= sim.Mode == MoveMode.Mantle;
                if (mantled && sim.Grounded) break;
            }
            return mantled && sim.Grounded ? sim.Position.Y : float.NaN;
        }

        [TestCase(true, 0)]
        [TestCase(false, 0)]
        [TestCase(true, 4)]
        [TestCase(false, 4)]
        [TestCase(false, 6)]
        public void TheDemosJumpDoubleJumpAndPitchedDownShotLandItOnTheKiosk(bool onePerAirtime, int lateSteps)
        {
            float y = RunSequence(onePerAirtime, lateSteps, true, out bool boosted, out bool mantled);
            Assert.That(boosted, Is.True, "the 85-degree shot lifts (its reverse points up by sin 85 > minLift)");
            Assert.That(mantled, Is.True, "it mantles onto the top");
            Assert.That(y, Is.EqualTo(GalleriaArena.BoostLedgeTop.Y).Within(0.1f), "standing on the kiosk's top");
        }

        [TestCase(0)]
        [TestCase(4)]
        public void WithoutTheShotTheSameJumpsDontReachTheTop(int lateSteps)
        {
            float y = RunSequence(false, lateSteps, false, out _, out _);
            Assert.That(float.IsNaN(y) || y < GalleriaArena.BoostLedgeTop.Y - 0.5f, Is.True, "jump + double jump + mantle fall short");
        }
    }
}
