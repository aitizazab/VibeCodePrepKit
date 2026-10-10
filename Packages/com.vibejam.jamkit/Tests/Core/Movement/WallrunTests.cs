using System;
using System.Numerics;
using JamKit.Movement;
using NUnit.Framework;
using static JamKit.Tests.Movement.MoveTestKit;

namespace JamKit.Tests.Movement
{
    /// <summary>Wallruns and wall jumps, against Titanfall 2's measured numbers (docs/RESEARCH-MOVEMENT.md 9):
    /// 8.64 m/s goal speed from above or below, 1.75 s maximum, forward input required, away ends it at once,
    /// wall jump 5.21 m/s out + up to 1.91 along + up to -4.37 inward, crouch kick +1.78, the 83 ms wall kick,
    /// and the same-wall rule.</summary>
    public class WallrunTests
    {
        /// <summary>A wall on the right (face at x = 2, normal -X) from z 0 to <paramref name="length"/>, over a deep pit.</summary>
        static BoxWorld WallWorld(float length = 60f)
        {
            var w = new BoxWorld();
            w.Add(new Vector3(2.5f, -10f, length * 0.5f), new Vector3(1f, 40f, length));
            return w;
        }

        static MoveSim OnWall(BoxWorld w, float speed, MoveDef def = null)
        {
            var sim = Standing(w, new Vector3(1.3f, 3f, 1f), def, 0f, new Vector3(0f, 0f, speed));
            Run(sim, Forward(), 0.2f, s => s.Mode == MoveMode.Wallrun);
            Assert.That(sim.Mode, Is.EqualTo(MoveMode.Wallrun), "caught the wall at " + speed + " m/s");
            return sim;
        }

        static float Along(MoveSim s) => Vector3.Dot(s.Velocity, s.WallTangent);

        [Test]
        public void CatchingAWallKeepsTheSpeedAndRefreshesTheDoubleJump()
        {
            var sim = Standing(WallWorld(), new Vector3(1.3f, 3f, -3f), velocity: new Vector3(0f, 0f, 11f));
            sim.Step(Forward().With(jump: true), H); // spend the double jump before the wall starts
            Assert.That(sim.DoubleJumpAvailable, Is.False);
            bool started = false;
            Run(sim, Forward(), 0.6f, s => { started |= s.Events.wallrunStarted; return started; });
            Assert.That(started, Is.True);
            Assert.That(sim.WallSide, Is.EqualTo(1), "the wall is on the right");
            Assert.That(sim.HorizontalSpeed, Is.EqualTo(11f).Within(0.3f), "entry keeps the speed, turned along the wall");
            Assert.That(sim.DoubleJumpAvailable, Is.True);
        }

        [Test]
        public void TheGoalSpeedIs864ApproachedFromAboveAndBelow()
        {
            Assert.That(new MoveDef().wallRunSpeed, Is.EqualTo(8.64f).Within(0.01f), "340 u/s");
            var fast = OnWall(WallWorld(), 13f);
            Run(fast, Forward(), 1.6f); // 0.5 s of grace, then the friction
            Assert.That(Along(fast), Is.LessThan(10f).And.GreaterThan(8.64f), "wall friction pulls a fast run down toward 8.64");
            var slow = OnWall(WallWorld(), 6f);
            Run(slow, Forward(), 1.2f);
            Assert.That(Along(slow), Is.EqualTo(8.64f).Within(0.05f), "a slow run accelerates up to 8.64 and holds it");
        }

        [Test]
        public void AWallrunLastsAtMost175Seconds()
        {
            Assert.That(new MoveDef().wallRunDuration, Is.EqualTo(1.75f));
            var sim = OnWall(WallWorld(80f), 9f);
            float t = Run(sim, Forward(), 3f, s => s.Mode != MoveMode.Wallrun);
            Assert.That(sim.Mode, Is.EqualTo(MoveMode.Air));
            Assert.That(t, Is.EqualTo(1.75f).Within(0.02f), "ends at the time limit");
        }

        [Test]
        public void PushingAwayFromTheWallLetsGoAndNoInputBrakesToAFall()
        {
            var sim = OnWall(WallWorld(), 10f);
            Run(sim, Forward(), 0.2f);
            var away = new MoveInput { move = new Vector2(-1f, 0.2f), yaw = 0f };
            sim.Step(away, H);
            Assert.That(sim.Mode, Is.EqualTo(MoveMode.Wallrun), "a flick away doesn't drop you on the first step");
            Run(sim, away, 0.1f, s => s.Mode != MoveMode.Wallrun);
            Assert.That(sim.Mode, Is.EqualTo(MoveMode.Air), "the stick held away from the wall ends the run within wallAwayTime");

            var idle = OnWall(WallWorld(), 10f);
            float t = Run(idle, new MoveInput(), 2f, s => s.Mode != MoveMode.Wallrun);
            Assert.That(t, Is.LessThan(0.6f), "no forward input: friction to 0, off the wall well before 1.75 s");
        }

        [Test]
        public void AWallJumpAddsTheMeasuredImpulses()
        {
            // At the goal speed, stick along the wall: +5.21 out, +1.91 along -> a ~3.1 m/s gain (Titanfall: +123 u/s).
            var sim = OnWall(WallWorld(), 8.64f);
            Run(sim, Forward(), 0.3f);
            Vector3 n = sim.WallNormal, t = sim.WallTangent;
            float along = Along(sim), speed = sim.HorizontalSpeed;
            sim.Step(Forward().With(jump: true), H);
            Assert.That(sim.Events.wallJumped, Is.True);
            Assert.That(Vector3.Dot(sim.Velocity, n), Is.EqualTo(5.21f).Within(0.01f), "205 u/s along the normal");
            Assert.That(Vector3.Dot(sim.Velocity, t) - along, Is.EqualTo(1.91f).Within(0.01f), "75 u/s along the wall");
            Assert.That(sim.HorizontalSpeed - speed, Is.EqualTo(3.1f).Within(0.15f), "best wall-kick gain at wallrun speed");
            Assert.That(sim.Velocity.Y, Is.EqualTo(sim.def.wallJumpUp).Within(0.01f));

            // At high speed the gain tends to the along-the-wall part (1.91).
            var high = OnWall(WallWorld(), 20f);
            float hs = high.HorizontalSpeed;
            high.Step(Forward().With(jump: true), H);
            Assert.That(high.HorizontalSpeed - hs, Is.InRange(1.9f, 2.6f));
        }

        [Test]
        public void SteeringIntoTheWallTakesFromThePushAndCrouchKicksAdd()
        {
            var into = OnWall(WallWorld(), 9f);
            Vector3 n = into.WallNormal;
            var stickIn = new MoveInput { move = new Vector2(1f, 1f) / (float)Math.Sqrt(2), yaw = 0f };
            into.Step(stickIn.With(jump: true), H);
            float d = (float)(1.0 / Math.Sqrt(2));
            Assert.That(Vector3.Dot(into.Velocity, n), Is.EqualTo(5.21f - 4.37f * d).Within(0.02f), "-172 u/s x dot(wish, -normal)");

            var crouch = OnWall(WallWorld(), 9f);
            Vector3 n2 = crouch.WallNormal;
            crouch.Step(Forward().With(jump: true, crouchHeld: true), H);
            Assert.That(Vector3.Dot(crouch.Velocity, n2), Is.EqualTo(5.21f + 1.78f).Within(0.02f), "crouch kick +70 u/s");
        }

        [Test]
        public void AJumpWithin83MsOfContactIsAWallKickThatKeepsTheSpeed()
        {
            Assert.That(new MoveDef().wallKickWindow, Is.EqualTo(0.083f).Within(0.001f), "5 ticks at 60 Hz");
            var quick = OnWall(WallWorld(), 13f);
            Run(quick, Forward(), 0.05f);
            quick.Step(Forward().With(jump: true), H);
            Assert.That(quick.Events.wallKick, Is.True);
            Assert.That(quick.HorizontalSpeed, Is.GreaterThan(13f + 1f), "a kick at once gains speed");

            var late = OnWall(WallWorld(), 13f);
            Run(late, Forward(), 1.1f); // past the 0.5 s speed grace
            late.Step(Forward().With(jump: true), H);
            Assert.That(late.Events.wallKick, Is.False);
            Assert.That(late.HorizontalSpeed, Is.LessThan(quick.HorizontalSpeed - 1.5f), "staying on the wall lost speed to wall friction");
        }

        [Test]
        public void TheSameWallCanOnlyBeCaughtAgainLower()
        {
            // Wall jump off a long wall, double jump back toward it: touching it higher than where you left
            // doesn't catch it; sliding down below that height does.
            var sim = OnWall(WallWorld(80f), 10f);
            Run(sim, Forward(), 0.1f);
            float leaveY = sim.Position.Y;
            sim.Step(Forward().With(jump: true), H);
            Assert.That(sim.Events.wallJumped, Is.True);
            Run(sim, Forward(), 0.15f);
            var back = new MoveInput { move = Vector2.Normalize(new Vector2(1f, 1f)), yaw = 0f };
            sim.Step(back.With(jump: true), H);
            Assert.That(sim.Events.doubleJumped, Is.True);
            bool refused = false, caught = false;
            float caughtY = 0f;
            Run(sim, back, 2.5f, s =>
            {
                if (s.Events.wallrunStarted) { caught = true; caughtY = s.Position.Y; return true; }
                if (s.Position.X > 1.5f && s.Position.Y > leaveY) refused = true;
                return false;
            });
            Assert.That(refused, Is.True, "touched the wall above where it left: no regrab");
            Assert.That(caught, Is.True, "lower than where it left: the wall takes it again");
            Assert.That(caughtY, Is.LessThan(leaveY - 0.1f + 0.01f));
        }

        [Test]
        public void AWallTurned25DegreesOrMoreIsANewWall()
        {
            var def = new MoveDef();
            Assert.That(def.sameWallAngle, Is.EqualTo(25f));
            Vector3 n = new Vector3(-1f, 0f, 0f);
            Vector3 Turned(double deg) => new Vector3(-(float)Math.Cos(deg * Math.PI / 180), 0f, (float)Math.Sin(deg * Math.PI / 180));
            Assert.That(MoveSim.IsSameWall(def, n, -2f, Turned(20), -2f), Is.True, "20 degrees: the same wall");
            Assert.That(MoveSim.IsSameWall(def, n, -2f, Turned(30), -2f), Is.False, "30 degrees: a new wall");
            Assert.That(MoveSim.IsSameWall(def, n, -2f, n, -3f), Is.False, "a parallel wall a metre further: a new wall");
        }

        [Test]
        public void WallCoyoteTurnsAJumpJustAfterTheWallEndsIntoAWallJump()
        {
            var sim = OnWall(WallWorld(6f), 9f);
            Run(sim, Forward(), 2f, s => s.Mode != MoveMode.Wallrun);
            Assert.That(sim.Mode, Is.EqualTo(MoveMode.Air), "ran off the end of the wall");
            Run(sim, Forward(), 0.1f);
            sim.Step(Forward().With(jump: true), H);
            Assert.That(sim.Events.wallJumped, Is.True, "0.1 s after the wall: still a wall jump");
            Assert.That(sim.DoubleJumpAvailable, Is.True, "and the double jump is still there");
        }

        [Test]
        public void HeadOnIntoAWallIsNotAWallrun()
        {
            var w = new BoxWorld();
            w.Add(new Vector3(0f, -5f, 5f), new Vector3(20f, 40f, 1f)); // facing -Z
            var sim = Standing(w, new Vector3(0f, 3f, 2f), velocity: new Vector3(0f, 0f, 10f));
            bool started = false;
            Run(sim, Forward(), 0.5f, s => { started |= s.Events.wallrunStarted; return started; });
            Assert.That(started, Is.False);
        }
    }
}
