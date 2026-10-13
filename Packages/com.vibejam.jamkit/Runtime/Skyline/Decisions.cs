using System;
using System.Numerics;
using JamKit.Movement;

namespace JamKit.Skyline
{
    /// <summary>A choice the player made (docs/SKYLINE-DESIGN.md 4.1). The Ledger and, later, Baja's predictors read these.</summary>
    public enum DecisionKind { Jump, DoubleJump, WallJump, WallrunStart, SlideStart, MantleStart, Fire, Reload, Swap, Ability, Boost }

    /// <summary>The situation a decision was made in.</summary>
    [Flags]
    public enum DecisionContext { None = 0, Airborne = 1, UnderFire = 2, Wallrunning = 4, Sliding = 8, InCombat = 16 }

    /// <summary>One decision in the shared format every gun, ability and movement option reports.</summary>
    public struct DecisionEvent
    {
        public float time;
        public DecisionKind kind;
        /// <summary>The gun, ability or hire this is about; -1 for movement.</summary>
        public int subject;
        /// <summary>+1 right, -1 left, 0 none (a wall's side, a dash's side).</summary>
        public int side;
        public DecisionContext context;
        /// <summary>Degrees clockwise from the player's facing to the nearest threat; NaN when nothing is shooting.</summary>
        public float threatBearing;
        /// <summary>Rounds in the magazine at the moment of the decision; -1 when it isn't a gun decision.</summary>
        public int ammo;
        public Vector3 position;
    }

    /// <summary>The newest decisions, in order, in a fixed-size ring (oldest dropped first).</summary>
    public sealed class DecisionLog
    {
        readonly DecisionEvent[] _items;
        int _start, _count;

        public DecisionLog(int capacity = 4096)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _items = new DecisionEvent[capacity];
        }

        public int Count => _count;
        public int Capacity => _items.Length;
        /// <summary>Every decision ever added, including the ones the ring has dropped.</summary>
        public long Total { get; private set; }

        /// <summary>0 is the oldest kept decision, Count - 1 the newest.</summary>
        public DecisionEvent this[int i]
        {
            get
            {
                if ((uint)i >= (uint)_count) throw new ArgumentOutOfRangeException(nameof(i));
                return _items[(_start + i) % _items.Length];
            }
        }

        public void Add(in DecisionEvent e)
        {
            if (_count < _items.Length)
            {
                _items[(_start + _count) % _items.Length] = e;
                _count++;
            }
            else
            {
                _items[_start] = e;
                _start = (_start + 1) % _items.Length;
            }
            Total++;
        }

        public void Clear()
        {
            _start = 0;
            _count = 0;
        }
    }

    /// <summary>Turns the movement engine's events into decisions (the engine itself knows nothing about threats).</summary>
    public static class MoveDecisions
    {
        /// <summary>Adds one decision per movement choice in <paramref name="e"/> (one step's or one frame's merged
        /// events) and returns how many it added. Landing and leaving the ground are outcomes, not choices.</summary>
        public static int Append(DecisionLog log, in MoveEvents e, float time, Vector3 position, DecisionContext context, float threatBearing)
        {
            int n = 0;
            if (e.jumped) n += Add(log, DecisionKind.Jump, 0, time, position, context, threatBearing);
            if (e.doubleJumped) n += Add(log, DecisionKind.DoubleJump, 0, time, position, context, threatBearing);
            if (e.wallJumped) n += Add(log, DecisionKind.WallJump, 0, time, position, context, threatBearing);
            if (e.wallrunStarted) n += Add(log, DecisionKind.WallrunStart, e.wallSide, time, position, context, threatBearing);
            if (e.slideStarted) n += Add(log, DecisionKind.SlideStart, 0, time, position, context, threatBearing);
            if (e.mantleStarted) n += Add(log, DecisionKind.MantleStart, 0, time, position, context, threatBearing);
            return n;
        }

        static int Add(DecisionLog log, DecisionKind kind, int side, float time, Vector3 position, DecisionContext context, float threatBearing)
        {
            log.Add(new DecisionEvent
            {
                time = time, kind = kind, subject = -1, side = side, context = context,
                threatBearing = threatBearing, ammo = -1, position = position,
            });
            return 1;
        }
    }
}
