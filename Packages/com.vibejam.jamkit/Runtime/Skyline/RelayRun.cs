using System;
using System.Collections.Generic;
using System.Numerics;

namespace JamKit.Skyline
{
    public enum RelayOutcome { Running, Won, Wiped }

    /// <summary>What an interact press did: nothing (wrong station, wrong state), progress (a hold that is not long enough
    /// yet), or finished the current step.</summary>
    public enum RelayResult { NoEffect, Progress, Done }

    /// <summary>The cable's rule: a tether from the reel that pulls taut at a length and stops you (it never breaks).</summary>
    public static class RelayTether
    {
        /// <summary>The slack over the shortest walk from the reel to the socket (+20%).</summary>
        public const float Slack = 1.2f;

        public static float LengthFor(float shortestWalk) => shortestWalk * Slack;

        public static bool IsTaut(Vector3 anchor, Vector3 feet, float length) => Vector3.Distance(anchor, feet) > length;

        /// <summary>The nearest place to <paramref name="feet"/> that the tether allows (the feet themselves while it is slack).</summary>
        public static Vector3 Clamp(Vector3 anchor, Vector3 feet, float length)
        {
            Vector3 d = feet - anchor;
            float len = d.Length();
            if (len <= length || len < 1e-5f) return feet;
            return anchor + d * (length / len);
        }
    }

    /// <summary>
    /// A Relay run (docs/SKYLINE-RELAY.md): five steps in a fixed order across the six stations, whose roles are shuffled per
    /// run: <b>Button</b> (hold interact 0.5 s) at the first, <b>Cable</b> (take the reel's end) at the second, <b>Plug</b> it in
    /// at the third (a keycard pops out and is picked up by standing at the socket), <b>Swipe</b> the keycard at the fourth (opens the
    /// door), then <b>Escape</b>: pass the open door (the fifth station) and enter the exit lift (the sixth). The clock runs from
    /// the start; at zero the floor is wiped. Carrying the cable costs 25% speed and the sprint, and the cable stops you at its
    /// length. Pure and deterministic: the caller feeds time, interact presses and the station the Unit stands in.
    /// </summary>
    public sealed class RelayRun
    {
        public const float DefaultSeconds = 60f, ButtonHold = 0.5f, CarryScale = 0.75f, KeycardPop = 0.6f;
        public const int StepCount = 5;

        readonly IRelayMap _map;
        readonly float _seconds;
        int[] _order;
        int _seed, _step;
        float _elapsed, _popAt;
        bool _carrying, _plugged, _popped, _hasCard, _doorOpen, _doorPassed;
        RelayOutcome _outcome;
        readonly float[] _doneAt = new float[StepCount];
        float _tether;

        /// <summary>A step was completed: (index 0..4, kind, run time).</summary>
        public event Action<int, RelayStepKind, float> StepDone;
        /// <summary>The run ended: won or wiped, at this run time.</summary>
        public event Action<RelayOutcome, float> Ended;
        /// <summary>The socket's keycard was picked up.</summary>
        public event Action<float> KeycardTaken;
        /// <summary>The Unit passed the open door.</summary>
        public event Action<float> DoorPassedEvent;

        public RelayRun(IRelayMap map, int seed, float seconds = DefaultSeconds)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (!(seconds > 0f)) throw new ArgumentOutOfRangeException(nameof(seconds));
            _map = map;
            _seconds = seconds;
            Begin(seed);
        }

        void Begin(int seed)
        {
            _seed = seed;
            _order = RelayChain.Generate(_map, seed);
            _step = 0;
            _elapsed = 0f;
            _popAt = 0f;
            _carrying = _plugged = _popped = _hasCard = _doorOpen = _doorPassed = false;
            _outcome = RelayOutcome.Running;
            Array.Clear(_doneAt, 0, _doneAt.Length);
            float walk = _map.PathLength(_order[1], _order[2]);
            if (walk < 0f) walk = Vector3.Distance(_map.Position(_order[1]), _map.Position(_order[2]));
            _tether = RelayTether.LengthFor(walk);
        }

        /// <summary>Starts again with the order for <paramref name="seed"/> and a full clock.</summary>
        public void Restart(int seed) => Begin(seed);

        /// <summary>Starts again with a new random order: the next seed whose order differs from this one's.</summary>
        public void RestartNew()
        {
            int[] old = _order;
            int s = _seed;
            for (int i = 0; i < 64; i++)
            {
                s = unchecked(s * 1103515245 + 12345 + i);
                var o = RelayChain.Generate(_map, s);
                if (!SameOrder(o, old)) break;
            }
            Begin(s);
        }

        static bool SameOrder(int[] a, int[] b)
        {
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        public int Seed => _seed;
        /// <summary>order[role] is the station playing that role: button, cable reel, socket, card reader, door, lift.</summary>
        public IReadOnlyList<int> Order => _order;
        public int ButtonStation => _order[0];
        public int ReelStation => _order[1];
        public int SocketStation => _order[2];
        public int ReaderStation => _order[3];
        public int DoorStation => _order[4];
        public int LiftStation => _order[5];
        public string Letters => RelayChain.Letters(_order);

        public float Seconds => _seconds;
        public float Elapsed => _elapsed;
        public float Remaining => Math.Max(0f, _seconds - _elapsed);
        public RelayOutcome Outcome => _outcome;
        public bool Running => _outcome == RelayOutcome.Running;

        /// <summary>The step the Unit is on (0..4); 5 once won.</summary>
        public int Step => _step;
        public RelayStepKind? CurrentKind => _step < StepCount ? RelayChain.Steps[_step] : (RelayStepKind?)null;
        /// <summary>The station the current step is done at, or -1 when there is none (won, wiped).</summary>
        public int CurrentStation => Running && _step < StepCount ? RelayChain.StationOfStep(_order, _step) : -1;
        /// <summary>The station to head for: the current step's, except that the Escape goes through the door first.</summary>
        public int TargetStation => CurrentStation < 0 ? -1 : (_step == 4 && !_doorPassed ? _order[4] : CurrentStation);
        public float DoneAt(int step) => _doneAt[step];

        public bool Carrying => _carrying;
        public bool Plugged => _plugged;
        public bool KeycardPopped => _popped;
        public bool HasKeycard => _hasCard;
        public bool DoorOpen => _doorOpen;
        public bool DoorPassed => _doorPassed;

        // ------------------------------------------------------------------ the cable's costs

        /// <summary>Carrying the cable: 75% of the speed, no sprint.</summary>
        public float MoveScale => _carrying ? CarryScale : 1f;
        public bool SprintAllowed => !_carrying;
        public float TetherLength => _tether;
        public Vector3 TetherAnchor => _map.Position(_order[1]);
        /// <summary>Where the cable pulls the Unit up: the feet themselves while the cable is slack or not carried.</summary>
        public Vector3 ClampFeet(Vector3 feet) => _carrying ? RelayTether.Clamp(TetherAnchor, feet, _tether) : feet;

        // ------------------------------------------------------------------ the clock and the presses

        /// <summary>Advances the clock; at zero the run is wiped.</summary>
        public void Tick(float dt)
        {
            if (!Running || !(dt > 0f) || float.IsInfinity(dt)) return;
            _elapsed += dt;
            if (_elapsed >= _seconds)
            {
                _elapsed = _seconds;
                _outcome = RelayOutcome.Wiped;
                Ended?.Invoke(RelayOutcome.Wiped, _elapsed);
            }
        }

        /// <summary>The Unit was wrecked (by the bots): the run is wiped at once, as when the clock runs out.</summary>
        public void Wipe()
        {
            if (!Running) return;
            _outcome = RelayOutcome.Wiped;
            Ended?.Invoke(RelayOutcome.Wiped, _elapsed);
        }

        /// <summary>The longest time a press must be held for the current step (the button: 0.5 s; the others act at once).</summary>
        public float HoldFor(int station)
        {
            return Running && _step == 0 && station == _order[0] ? ButtonHold : 0f;
        }

        /// <summary>An interact press at <paramref name="station"/> (the one the Unit stands at, -1 for none), held for
        /// <paramref name="heldSeconds"/>. Only the current step's own station reacts, and only when its precondition holds.</summary>
        public RelayResult Interact(int station, float heldSeconds)
        {
            if (!Running || station < 0 || _step >= StepCount) return RelayResult.NoEffect;
            switch (_step)
            {
                case 0:
                    if (station != _order[0]) return RelayResult.NoEffect;
                    if (heldSeconds < ButtonHold) return RelayResult.Progress;
                    return Complete();
                case 1:
                    if (station != _order[1] || _carrying) return RelayResult.NoEffect;
                    _carrying = true;
                    return Complete();
                case 2:
                    if (station != _order[2] || !_carrying) return RelayResult.NoEffect;
                    _carrying = false;
                    _plugged = true;
                    _popped = true;
                    _popAt = _elapsed;
                    return Complete();
                case 3:
                    if (station != _order[3] || !_hasCard) return RelayResult.NoEffect;
                    _doorOpen = true;
                    return Complete();
                default:
                    return RelayResult.NoEffect;
            }
        }

        /// <summary>Where the Unit stands now (the station whose zone holds its feet, or -1): picks up the socket's keycard once it
        /// has popped, notes the pass through the open door, and ends the run in the exit lift (only after the door was passed).</summary>
        public void PlayerAt(int station)
        {
            if (!Running) return;
            if (_popped && !_hasCard && station == _order[2] && _elapsed - _popAt >= KeycardPop)
            {
                _hasCard = true;
                KeycardTaken?.Invoke(_elapsed);
            }
            if (_doorOpen && !_doorPassed && station == _order[4])
            {
                _doorPassed = true;
                DoorPassedEvent?.Invoke(_elapsed);
            }
            if (_step == 4 && _doorOpen && _doorPassed && station == _order[5])
            {
                Complete();
                _step = StepCount;
                _outcome = RelayOutcome.Won;
                Ended?.Invoke(RelayOutcome.Won, _elapsed);
            }
        }

        RelayResult Complete()
        {
            int i = _step;
            _doneAt[i] = _elapsed;
            StepDone?.Invoke(i, RelayChain.Steps[i], _elapsed);
            _step = i + 1;
            return RelayResult.Done;
        }
    }
}
