using System;
using System.Numerics;
using System.Text;
using JamKit.Movement;
using JamKit.Skyline;
using JamKit.Tests.Movement;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    /// <summary>The warm-up's wall-run ledge (judge round 2: nothing asked for a wall-run): a tower by the west wall whose top
    /// is out of a jump, a double jump and a mantle from the floor, reached by running the wall, kicking off it toward the
    /// tower and mantling. The real MoveSim and MoveDef; the demo drives the same sequence.</summary>
    public class WallRunLedgeTests
    {
        static BoxWorld Warmup()
        {
            var w = new BoxWorld();
            w.Floor(GalleriaArena.WarmupMinX, GalleriaArena.WarmupMaxX, GalleriaArena.WarmupMinZ, GalleriaArena.WarmupMaxZ, 0f);
            w.Add(new Vector3(GalleriaArena.WarmupMinX - 0.2f, 4f, (GalleriaArena.WarmupMinZ + GalleriaArena.WarmupMaxZ) * 0.5f),
                new Vector3(0.4f, 8f, GalleriaArena.WarmupMaxZ - GalleriaArena.WarmupMinZ), runnable: true);
            var t = GalleriaArena.WallRunLedge;
            w.Add((t.min + t.max) * 0.5f, t.max - t.min, runnable: false);
            return w;
        }

        public struct Result
        {
            public float topY;
            public bool wallran, kicked, doubleJumped, mantled;
        }

        /// <summary>The sequence: from <see cref="GalleriaArena.WallRunStart"/> facing north, sprint with the stick a little into
        /// the wall; jump once at <paramref name="jumpZ"/>; on the wall, hold along it; at <paramref name="kickZ"/> jump off it
        /// (the stick turned <paramref name="kickYaw"/> degrees toward the lockers, the view still along the wall); at that jump's apex, double jump; hold
        /// toward the tower. Returns where it stands.</summary>
        public static Result Run(float jumpZ, float kickZ, float kickYaw, bool kick = true, bool doubleJump = true)
        {
            var sim = MoveTestKit.Standing(Warmup(), GalleriaArena.WallRunStart);
            var r = new Result();
            bool jumped = false, kickedOff = false, dj = false;
            var along = new MoveInput { move = new Vector2(-0.2f, 1f), yaw = 0f, sprintHeld = true };
            // the view stays along the wall (yaw 0), the stick turns toward the lockers, as the demo's hands do
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
                r.wallran |= sim.Mode == MoveMode.Wallrun;
                r.kicked |= sim.Events.wallJumped;
                r.doubleJumped |= sim.Events.doubleJumped;
                r.mantled |= sim.Mode == MoveMode.Mantle;
                if (sim.Grounded && sim.Position.Y > 1f) { r.topY = sim.Position.Y; break; }
                if (jumped && sim.Grounded && sim.Position.Y < 0.1f && t > 1.5f) break;
            }
            return r;
        }

        /// <summary>The demo's sequence (SkylineArenaDemo): the jump this far south of the lockers' south face, the kick this far,
        /// the stick turned this many degrees toward them.</summary>
        public const float JumpBefore = 6f, KickBefore = 3f, KickYaw = 45f;

        [Test]
        public void RunningTheWallAndKickingOffItReachesTheTopAndRobustly()
        {
            float face = GalleriaArena.WallRunLedge.min.Z;
            foreach (float dj in new[] { 0f, -1f, 1f })
                foreach (float dk in new[] { 0f, -0.5f, 0.5f })
                {
                    var r = Run(face - JumpBefore + dj, face - KickBefore + dk, KickYaw);
                    Assert.That(r.wallran && r.kicked, Is.True, $"jump {dj:+0;-0;0} m, kick {dk:+0.0;-0.0;0} m: ran the wall and kicked off it");
                    Assert.That(r.topY, Is.EqualTo(GalleriaArena.WallRunLedge.max.Y).Within(0.1f), $"jump {dj:+0;-0;0} m, kick {dk:+0.0;-0.0;0} m: on top");
                }
        }

        [Test]
        public void TheFloorAloneOrTheWallWithoutTheKickFallShort()
        {
            float face = GalleriaArena.WallRunLedge.min.Z;
            var noKick = Run(face - JumpBefore, face - KickBefore, KickYaw, kick: false, doubleJump: false);
            Assert.That(noKick.wallran, Is.True);
            Assert.That(noKick.topY, Is.LessThan(1f), "running the wall past it without the kick: not on top");
            // the floor: facing its east face from 0.8 m, jump, double jump at the apex, mantle
            var t = GalleriaArena.WallRunLedge;
            var sim = MoveTestKit.Standing(Warmup(), new Vector3(t.max.X + 0.38f + 0.8f, 0f, (t.min.Z + t.max.Z) * 0.5f), null, -90f);
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
            Assert.That(best, Is.LessThan(1f), "a jump, a double jump and a mantle from the floor fall short");
            // far enough from the boost kiosk that its top doesn't lead there
            var k = GalleriaArena.BoostLedgeTop;
            float h = GalleriaArena.BoostLedgeSize * 0.5f;
            float dx = Math.Max(0f, Math.Max(t.min.X - (k.X + h), (k.X - h) - t.max.X)), dz = Math.Max(0f, Math.Max(t.min.Z - (k.Z + h), (k.Z - h) - t.max.Z));
            Assert.That(MathF.Sqrt(dx * dx + dz * dz), Is.GreaterThanOrEqualTo(12f));
        }

        [Test, Explicit("design aid: searches the wall-run sequence's timings")]
        public void SearchTheSequence()
        {
            var sb = new StringBuilder();
            var t = GalleriaArena.WallRunLedge;
            for (float jz = t.min.Z - 9f; jz <= t.min.Z - 3f; jz += 1f)
                for (float kz = jz + 1f; kz <= t.max.Z; kz += 0.5f)
                    foreach (float yaw in new[] { 20f, 45f, 70f, 90f })
                    {
                        var r = Run(jz, kz, yaw);
                        if (r.topY > 1f) sb.AppendLine($"jump at z {jz:0.0}, kick at z {kz:0.0}, yaw {yaw:0}: on top at {r.topY:0.00} (wall {r.wallran}, kick {r.kicked}, dj {r.doubleJumped}, mantle {r.mantled})");
                    }
            TestContext.WriteLine(sb.Length == 0 ? "no sequence reaches the top" : sb.ToString());
        }
    }
}
