using System;
using System.Collections.Generic;

namespace JamKit
{
    /// <summary>
    /// A small enum-keyed state machine: game flow (menu, playing, paused...), enemy AI (idle,
    /// chase, attack...), boss phases. Optional whitelist of transitions catches "paused -> boot"
    /// style bugs at the moment they happen instead of three screens later.
    /// </summary>
    public sealed class StateMachine<TState> where TState : struct, Enum
    {
        readonly Dictionary<TState, Action> _onEnter = new Dictionary<TState, Action>();
        readonly Dictionary<TState, Action> _onExit = new Dictionary<TState, Action>();
        readonly Dictionary<TState, Action<float>> _onTick = new Dictionary<TState, Action<float>>();
        Dictionary<TState, HashSet<TState>> _allowed;

        public TState Current { get; private set; }
        public TState Previous { get; private set; }
        /// <summary>Seconds since the last state change (advanced by <see cref="Tick"/>).</summary>
        public float TimeInState { get; private set; }

        /// <summary>Raised after every change, with (from, to).</summary>
        public event Action<TState, TState> Changed;

        public StateMachine(TState initial)
        {
            Current = initial;
            Previous = initial;
        }

        public StateMachine<TState> OnEnter(TState state, Action action) { _onEnter[state] = action; return this; }
        public StateMachine<TState> OnExit(TState state, Action action) { _onExit[state] = action; return this; }
        public StateMachine<TState> OnTick(TState state, Action<float> action) { _onTick[state] = action; return this; }

        /// <summary>Restrict transitions. Once any rule exists, unlisted transitions are refused.</summary>
        public StateMachine<TState> Allow(TState from, params TState[] to)
        {
            if (_allowed == null) _allowed = new Dictionary<TState, HashSet<TState>>();
            if (!_allowed.TryGetValue(from, out var set)) _allowed[from] = set = new HashSet<TState>();
            foreach (var t in to) set.Add(t);
            return this;
        }

        public bool CanGo(TState to)
        {
            if (_allowed == null) return true;
            return _allowed.TryGetValue(Current, out var set) && set.Contains(to);
        }

        /// <summary>Change state if allowed. Re-entering the current state is a no-op that returns true.</summary>
        public bool TryGo(TState to)
        {
            if (EqualityComparer<TState>.Default.Equals(Current, to)) return true;
            if (!CanGo(to)) return false;
            var from = Current;
            if (_onExit.TryGetValue(from, out var exit)) exit?.Invoke();
            Previous = from;
            Current = to;
            TimeInState = 0f;
            if (_onEnter.TryGetValue(to, out var enter)) enter?.Invoke();
            Changed?.Invoke(from, to);
            return true;
        }

        /// <summary>Like <see cref="TryGo"/> but throws on a refused transition.</summary>
        public void Go(TState to)
        {
            if (!TryGo(to))
                throw new InvalidOperationException($"Transition {Current} -> {to} is not allowed");
        }

        public void Tick(float dt)
        {
            TimeInState += dt;
            if (_onTick.TryGetValue(Current, out var tick)) tick?.Invoke(dt);
        }
    }
}
