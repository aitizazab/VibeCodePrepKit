using System;
using System.Collections.Generic;

namespace JamKit.Combat
{
    /// <summary>
    /// Hit stop (hit freeze): briefly slows or stops game time on impacts so hits feel heavy.
    /// Overlapping requests resolve to the slowest one still running. A cap on unbroken stopped time
    /// keeps a shotgun blast or a bullet-hell graze storm from freezing the game.
    /// Each frame: Tick with the unscaled frame time, then apply <see cref="CurrentTimeScale"/> to game time.
    /// </summary>
    [Serializable]
    public sealed class HitStop
    {
        /// <summary>Longest unbroken stretch of slowed time, in real seconds; then the stop is cut short.</summary>
        public float maxConsecutiveStop = 0.25f;
        /// <summary>After the cap cuts a stop short, requests are refused until time has run normally this long.</summary>
        public float recoveryTime = 0.1f;

        struct Stop
        {
            public float remaining;
            public float timeScale;
        }

        readonly List<Stop> _stops = new List<Stop>(8);
        float _stoppedFor;
        float _recoveringFor;

        /// <summary>The slowest time scale among running stops; 1 when none are running.</summary>
        public float CurrentTimeScale { get; private set; } = 1f;
        public bool IsStopped => _stops.Count > 0;
        /// <summary>True while requests are being refused after hitting the cap.</summary>
        public bool IsRecovering => _recoveringFor > 0f;

        /// <summary>Slows time to <paramref name="timeScale"/> (0 = full freeze) for
        /// <paramref name="durationSeconds"/> of real time. Returns false if refused (recovering
        /// from the cap, or a zero duration).</summary>
        public bool Request(float durationSeconds, float timeScale = 0f)
        {
            if (!(durationSeconds > 0f) || _recoveringFor > 0f) return false;
            if (!(timeScale > 0f)) timeScale = 0f;
            else if (timeScale > 1f) timeScale = 1f;
            _stops.Add(new Stop { remaining = durationSeconds, timeScale = timeScale });
            if (timeScale < CurrentTimeScale) CurrentTimeScale = timeScale;
            return true;
        }

        public void Tick(float unscaledDt)
        {
            if (unscaledDt <= 0f) return;
            if (_stops.Count == 0)
            {
                _stoppedFor = 0f;
                if (_recoveringFor > 0f) _recoveringFor = Math.Max(0f, _recoveringFor - unscaledDt);
                return;
            }

            _stoppedFor += unscaledDt;
            float scale = 1f;
            for (int i = _stops.Count - 1; i >= 0; i--)
            {
                var s = _stops[i];
                s.remaining -= unscaledDt;
                if (s.remaining <= 0f)
                {
                    _stops[i] = _stops[_stops.Count - 1];
                    _stops.RemoveAt(_stops.Count - 1);
                    continue;
                }
                _stops[i] = s;
                if (s.timeScale < scale) scale = s.timeScale;
            }
            CurrentTimeScale = scale;

            if (_stops.Count == 0) _stoppedFor = 0f;
            else if (_stoppedFor >= maxConsecutiveStop)
            {
                _stops.Clear();
                _stoppedFor = 0f;
                _recoveringFor = recoveryTime;
                CurrentTimeScale = 1f;
            }
        }

        /// <summary>Ends every stop and any recovery (scene change, pause menu).</summary>
        public void Clear()
        {
            _stops.Clear();
            _stoppedFor = 0f;
            _recoveringFor = 0f;
            CurrentTimeScale = 1f;
        }
    }
}
