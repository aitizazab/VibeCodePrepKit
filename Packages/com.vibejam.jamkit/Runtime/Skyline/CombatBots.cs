using System;
using System.Numerics;
using JamKit.Movement;

namespace JamKit.Skyline
{
    /// <summary>Stands still: the baseline every shot should hit.</summary>
    public sealed class StandStillBot : IPlayerBot
    {
        public MoveInput Next(MoveSim sim, float time, float h) => new MoveInput { yaw = 0f };
    }

    /// <summary>Sprints straight lanes along Z at full speed, turning at the ends: the habit leading shots punish.</summary>
    public sealed class StraightLineBot : IPlayerBot
    {
        readonly float _half;
        float _yaw;

        public StraightLineBot(float laneHalfLength = 15f) { _half = laneHalfLength; }

        public MoveInput Next(MoveSim sim, float time, float h)
        {
            if (sim.Position.Z > _half) _yaw = 180f;
            else if (sim.Position.Z < -_half) _yaw = 0f;
            return new MoveInput { move = new Vector2(0f, 1f), yaw = _yaw, sprintHeld = true };
        }
    }

    /// <summary>Runs the same lanes but flips its strafe every 0.3-0.7 s (seeded): the skill the design rewards.</summary>
    public sealed class JinkingBot : IPlayerBot
    {
        readonly float _half;
        readonly Rng _rng;
        float _yaw, _strafe = 1f, _nextSwitch;

        public JinkingBot(ulong seed, float laneHalfLength = 15f)
        {
            _rng = new Rng(seed);
            _half = laneHalfLength;
        }

        public MoveInput Next(MoveSim sim, float time, float h)
        {
            if (sim.Position.Z > _half) _yaw = 180f;
            else if (sim.Position.Z < -_half) _yaw = 0f;
            if (time >= _nextSwitch)
            {
                _strafe = -_strafe;
                _nextSwitch = time + _rng.Range(0.3f, 0.7f);
            }
            // keep inside the arena: when far from the centre line, strafe back towards it
            float facing = _yaw < 90f ? 1f : -1f;            // facing +Z, right is +X; facing -Z, right is -X
            if (MathF.Abs(sim.Position.X) > 12f) _strafe = -MathF.Sign(sim.Position.X) * facing;
            return new MoveInput { move = Vector2.Normalize(new Vector2(_strafe, 0.6f)), yaw = _yaw, sprintHeld = true };
        }
    }
}
