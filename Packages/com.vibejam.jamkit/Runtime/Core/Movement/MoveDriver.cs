using System.Numerics;

namespace JamKit.Movement
{
    /// <summary>
    /// Runs a <see cref="MoveSim"/> on a fixed step from variable frame times ("Fix Your Timestep"):
    /// the frame adds its time to an accumulator and the sim takes whole steps out of it (at most
    /// <see cref="MoveDef.maxStepsPerFrame"/>; a long hitch drops the rest). Presses are latched until
    /// a step consumes them, so a tap is never lost on a frame with no step, nor doubled on a frame
    /// with several. The renderer draws <see cref="InterpolatedPosition"/>: between the last two steps
    /// by <see cref="Alpha"/>. The same input timeline at 30, 60, 144 or 240 fps gives the same path.
    /// </summary>
    public sealed class MoveDriver
    {
        public readonly MoveSim Sim;
        double _acc;
        Vector3 _prev;
        bool _jumpLatch, _crouchLatch, _sprintToggleLatch;
        MoveEvents _frameEvents;
        int _lastSteps;

        public MoveDriver(MoveSim sim)
        {
            Sim = sim;
            _prev = sim.Position;
        }

        /// <summary>0..1: how far the render time is past the last step, toward the next.</summary>
        public float Alpha => (float)(_acc * Sim.def.tickRate);
        public Vector3 PreviousPosition => _prev;
        public Vector3 InterpolatedPosition => Vector3.Lerp(_prev, Sim.Position, Alpha < 0f ? 0f : Alpha > 1f ? 1f : Alpha);
        /// <summary>Everything that happened in the steps of the last frame.</summary>
        public MoveEvents FrameEvents => _frameEvents;
        public int LastSteps => _lastSteps;

        /// <summary>Advances by one rendered frame of <paramref name="dt"/> seconds. Returns the steps taken.</summary>
        public int Frame(float dt, MoveInput input)
        {
            _frameEvents = default;
            _lastSteps = 0;
            if (input.jumpPressed) _jumpLatch = true;
            if (input.crouchPressed) _crouchLatch = true;
            if (input.sprintTogglePressed) _sprintToggleLatch = true;
            if (!(dt > 0f)) return 0;
            float h = Sim.def.StepSeconds;
            int max = Sim.def.maxStepsPerFrame > 0 ? Sim.def.maxStepsPerFrame : 1;
            _acc += dt;
            if (_acc > max * (double)h) _acc = max * (double)h;
            while (_acc >= h - 1e-7)
            {
                _acc -= h;
                var step = input;
                step.jumpPressed = _jumpLatch;
                step.crouchPressed = _crouchLatch;
                step.sprintTogglePressed = _sprintToggleLatch;
                // A tap that was released before its step still counts as held for that one step.
                step.jumpHeld |= _jumpLatch;
                step.crouchHeld |= _crouchLatch;
                _jumpLatch = _crouchLatch = _sprintToggleLatch = false;
                _prev = Sim.Position;
                Sim.Step(step, h);
                _frameEvents.Merge(Sim.Events);
                _lastSteps++;
            }
            if (_acc < 0) _acc = 0;
            return _lastSteps;
        }

        /// <summary>Moves the body without interpolating across the jump (spawn, respawn).</summary>
        public void Teleport(Vector3 feet, float yaw, Vector3 velocity)
        {
            Sim.Teleport(feet, yaw, velocity);
            _prev = Sim.Position;
            _acc = 0;
            _jumpLatch = _crouchLatch = _sprintToggleLatch = false;
        }
    }
}
