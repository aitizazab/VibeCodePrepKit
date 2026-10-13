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

    /// <summary>Runs the same Z lane as <see cref="StraightLineBot"/> (same speed, no strafe) but reverses its running
    /// direction at seeded random intervals of 0.3-0.7 s as well as at the lane ends: direction changes across the line
    /// of fire, the skill the design rewards. The only difference from the straight-line bot is the reversals.</summary>
    public sealed class JinkingBot : IPlayerBot
    {
        readonly float _half;
        readonly Rng _rng;
        float _yaw, _nextSwitch;

        public JinkingBot(ulong seed, float laneHalfLength = 15f)
        {
            _rng = new Rng(seed);
            _half = laneHalfLength;
        }

        public MoveInput Next(MoveSim sim, float time, float h)
        {
            if (time >= _nextSwitch)
            {
                _yaw = _yaw < 90f ? 180f : 0f;
                _nextSwitch = time + _rng.Range(0.3f, 0.7f);
            }
            // the lane ends win over a random reversal, so the bot never leaves the lane
            if (sim.Position.Z > _half) _yaw = 180f;
            else if (sim.Position.Z < -_half) _yaw = 0f;
            return new MoveInput { move = new Vector2(0f, 1f), yaw = _yaw, sprintHeld = true };
        }
    }
}
