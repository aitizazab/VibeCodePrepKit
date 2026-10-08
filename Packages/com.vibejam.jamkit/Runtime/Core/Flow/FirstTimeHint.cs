namespace JamKit.Flow
{
    public enum HintState { Waiting, Showing, Fading, Done }

    /// <summary>
    /// A one-time on-screen hint ("Shift = slow &amp; precise"): it shows once, on the first run only,
    /// and fades when the player has done the thing (after a short minimum, so it's never a flicker)
    /// or after <see cref="ShowSeconds"/>. Pure logic: the game ticks it with gameplay time (so it
    /// waits while paused or in dialogue), draws <see cref="Alpha"/>, and saves "seen" when
    /// <see cref="Tick"/> reports that it finished.
    /// <code>
    /// hint.Start(save.HasFlag("hint_focus"));   // at the moment it matters (the first fight)
    /// if (hint.Tick(dt, focusHeld)) save.SetFlag("hint_focus");
    /// label.opacity = hint.Alpha;
    /// </code>
    /// </summary>
    public sealed class FirstTimeHint
    {
        float _showSeconds = 8f, _minSeconds = 1.2f, _fadeSeconds = 0.6f, _fadeInSeconds = 0.25f;
        HintState _state = HintState.Waiting;
        float _time, _fade;
        bool _used;

        /// <summary>Longest time on screen before it fades by itself.</summary>
        public float ShowSeconds { get => _showSeconds; set => _showSeconds = value > 0f ? value : 0.01f; }
        /// <summary>Shortest time on screen, even if the player does the thing at once.</summary>
        public float MinSeconds { get => _minSeconds; set => _minSeconds = value > 0f ? value : 0f; }
        public float FadeSeconds { get => _fadeSeconds; set => _fadeSeconds = value > 0f ? value : 0.01f; }

        public HintState State => _state;
        public bool IsVisible => _state == HintState.Showing || _state == HintState.Fading;
        /// <summary>True once the player did the thing while the hint was up.</summary>
        public bool WasUsed => _used;

        /// <summary>0..1: fades in when it starts, out when it ends.</summary>
        public float Alpha
        {
            get
            {
                switch (_state)
                {
                    case HintState.Showing: return _time >= _fadeInSeconds ? 1f : _time / _fadeInSeconds;
                    case HintState.Fading:
                        float a = 1f - _fade / _fadeSeconds;
                        return a < 0f ? 0f : a;
                    default: return 0f;
                }
            }
        }

        /// <summary>Shows the hint unless it was <paramref name="alreadySeen"/> (then it's Done at once).
        /// Only works from Waiting: a hint shows once per object. Returns true if it is now showing.</summary>
        public bool Start(bool alreadySeen)
        {
            if (_state != HintState.Waiting) return false;
            _time = 0f;
            _fade = 0f;
            _used = false;
            _state = alreadySeen ? HintState.Done : HintState.Showing;
            return _state == HintState.Showing;
        }

        /// <summary>Back to Waiting (a new run in the same scene object).</summary>
        public void Reset()
        {
            _state = HintState.Waiting;
            _time = _fade = 0f;
            _used = false;
        }

        /// <summary>Advance by <paramref name="dt"/> seconds; <paramref name="usedNow"/> = the player is
        /// doing the thing this frame. Returns true on the one tick the hint finishes showing (save
        /// "seen" then): when the fade-out starts.</summary>
        public bool Tick(float dt, bool usedNow)
        {
            if (!(dt > 0f)) dt = 0f;
            switch (_state)
            {
                case HintState.Showing:
                    _time += dt;
                    if (usedNow) _used = true;
                    if ((_used && _time >= _minSeconds) || _time >= _showSeconds)
                    {
                        _state = HintState.Fading;
                        _fade = 0f;
                        return true;
                    }
                    return false;
                case HintState.Fading:
                    _fade += dt;
                    if (_fade >= _fadeSeconds) _state = HintState.Done;
                    return false;
                default:
                    return false;
            }
        }
    }
}
