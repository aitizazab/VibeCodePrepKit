using System;

namespace JamKit.Combat
{
    /// <summary>
    /// Fires every <see cref="interval"/> seconds: enemy bullet patterns, spawners, damage-over-time.
    /// Tick returns how many times it fired, keeping the remainder, so a long frame fires several
    /// times instead of dropping beats and 30 fps fires exactly as often as 240 fps.
    /// </summary>
    [Serializable]
    public sealed class RepeatingTimer
    {
        /// <summary>Absorbs float error in summed frame times, so 240 x (1/240) s counts as a full second.</summary>
        const double Epsilon = 1e-6;

        /// <summary>Seconds between firings. 0 or less never fires.</summary>
        public float interval = 1f;

        double _elapsed;

        public RepeatingTimer() { }
        public RepeatingTimer(float interval) { this.interval = interval; }

        /// <summary>Seconds since the last firing.</summary>
        public float Elapsed => (float)Math.Max(0.0, _elapsed);
        public float Progress01 => interval > 0f ? (float)Math.Min(1.0, Math.Max(0.0, _elapsed / interval)) : 0f;

        public int Tick(float dt)
        {
            if (interval <= 0f || dt <= 0f) return 0;
            _elapsed += dt;
            if (_elapsed + Epsilon < interval) return 0;
            double count = Math.Floor((_elapsed + Epsilon) / interval);
            if (count > int.MaxValue) count = int.MaxValue;
            _elapsed -= count * interval;
            return (int)count;
        }

        public void Reset() => _elapsed = 0.0;
    }
}
