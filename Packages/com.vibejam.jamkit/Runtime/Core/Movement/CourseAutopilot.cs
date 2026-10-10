using System;
using System.Numerics;

namespace JamKit.Movement
{
    /// <summary>
    /// Plays a <see cref="LabCourse"/> by itself: always runs forward, steers its look toward the
    /// course's lane a few metres ahead, and fires the course's cues by distance along the route
    /// (jump, hold crouch, double jump at the apex, wall jump, zig-zag between walls). It reads only
    /// the sim's state, so the same autopilot drives the fake world in the dotnet tests and the real
    /// CharacterController in Unity (the smoke run, the PlayMode tests, attract mode).
    /// Call <see cref="Next"/> once per frame (or step); "pressed" flags are true on one call.
    /// </summary>
    public sealed class CourseAutopilot
    {
        readonly LabCourse _course;
        readonly bool[] _done;
        bool _crouching;
        bool _jumpWasHeld;
        float _zigTarget;
        bool _zigTargetSet;
        float _lastWallJumpSim = -1f;

        public float LookAhead = 4f;
        public float MaxSteerDegrees = 32f;

        public CourseAutopilot(LabCourse course)
        {
            _course = course;
            _done = new bool[course.cues.Count];
        }

        /// <summary>Forget fired cues behind <paramref name="fromS"/> (after a respawn or a chapter jump).</summary>
        public void Reset(float fromS)
        {
            for (int i = 0; i < _done.Length; i++) _done[i] = _course.cues[i].s < fromS - 0.5f;
            _crouching = false;
            _jumpWasHeld = false;
            _zigTargetSet = false;
        }

        public MoveInput Next(MoveSim sim)
        {
            var input = new MoveInput { move = new Vector2(0f, 1f) };
            Vector3 p = sim.Position;
            float s = _course.route.Project(p, out float lateral);
            float targetX = _course.LaneAt(s + LookAhead);
            // A wall ahead that hasn't been kicked off yet: hold its lane (looking further ahead would turn
            // the stick away from the wall just before contact, and the wall refuses you).
            for (int i = 0; i < _done.Length; i++)
            {
                var c = _course.cues[i];
                if (_done[i] || c.kind != LabCueKind.WallJump) continue;
                if (s >= c.s - 12f) targetX = _course.LaneAt(c.s);
                break;
            }
            bool jump = false;
            bool crouch = false;
            bool zig = false;

            for (int i = 0; i < _done.Length; i++)
            {
                var c = _course.cues[i];
                if (_done[i] || s < c.s) continue;
                switch (c.kind)
                {
                    case LabCueKind.Jump:
                        if (sim.Mode == MoveMode.Air && sim.Velocity.Y < -2f && sim.SinceGrounded > 0.3f) { _done[i] = true; break; }
                        if (sim.Mode == MoveMode.Wallrun) { _done[i] = true; break; }
                        if (sim.Mode == MoveMode.Air || sim.Mode == MoveMode.Mantle) break; // wait for the ground
                        jump = true;
                        _done[i] = true;
                        break;
                    case LabCueKind.Crouch:
                        if (s >= c.sEnd) { _done[i] = true; break; }
                        crouch = true;
                        break;
                    case LabCueKind.DoubleJumpAtApex:
                        if (sim.Mode == MoveMode.Grounded || sim.Mode == MoveMode.Sprint || sim.Mode == MoveMode.Slide) { if (s > c.s + 3f) _done[i] = true; break; }
                        if (sim.Mode == MoveMode.Air && sim.Velocity.Y <= 0.5f && sim.DoubleJumpAvailable && !jump)
                        {
                            jump = true;
                            _done[i] = true;
                        }
                        break;
                    case LabCueKind.WallJump:
                        if (sim.Mode == MoveMode.Wallrun) { jump = true; _done[i] = true; _lastWallJumpSim = sim.Time; }
                        else if (s > c.s + 6f) _done[i] = true;
                        break;
                    case LabCueKind.Zigzag:
                        if (s >= c.sEnd) { _done[i] = true; _zigTargetSet = false; break; }
                        zig = true;
                        if (sim.Mode == MoveMode.Wallrun)
                        {
                            // Hug this wall until it's time to kick, then aim at the other one.
                            _zigTarget = -sim.WallSide * c.x;
                            _zigTargetSet = true;
                            if (sim.WallTime >= 0.1f)
                            {
                                jump = true;
                                _lastWallJumpSim = sim.Time;
                            }
                            else targetX = sim.WallSide * (c.x + 1f);
                        }
                        break;
                }
            }
            if (zig && _zigTargetSet && !(sim.Mode == MoveMode.Wallrun && !jump)) targetX = _zigTarget;
            else if (sim.Mode == MoveMode.Wallrun && !jump) targetX = lateral + sim.WallSide * 1.5f; // hug the wall (away lets go)

            // Steer: look at the lane point ahead, within a limit.
            float dx = targetX - lateral;
            float yaw = MoveMath.YawOf(new Vector3(dx, 0f, LookAhead));
            if (yaw > MaxSteerDegrees) yaw = MaxSteerDegrees;
            if (yaw < -MaxSteerDegrees) yaw = -MaxSteerDegrees;
            input.yaw = MoveMath.YawOf(_course.route.DirectionAt(s)) + yaw;

            input.jumpPressed = jump && !_jumpWasHeld;
            input.jumpHeld = jump;
            _jumpWasHeld = jump;
            input.crouchHeld = crouch;
            input.crouchPressed = crouch && !_crouching;
            input.sprintHeld = true; // sprint is opt-in (MoveDef.autoSprint off): the autopilot holds it
            _crouching = crouch;
            return input;
        }
    }
}
