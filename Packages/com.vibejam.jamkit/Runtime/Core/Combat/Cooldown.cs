using System;

namespace JamKit.Combat
{
    /// <summary>
    /// "Can I do this again yet?" for dashes, abilities and melee swings. Starts ready.
    /// Serializable so a component can expose it as a field and tune <see cref="duration"/> in the inspector.
    /// </summary>
    [Serializable]
    public sealed class Cooldown
    {
        /// <summary>Seconds after a use before the next one.</summary>
        public float duration = 1f;

        float _remaining;

        public Cooldown() { }
        public Cooldown(float duration) { this.duration = duration; }

        public float Remaining => _remaining;
        public bool Ready => _remaining <= 0f;

        /// <summary>0 right after a use, 1 when ready: for radial cooldown icons.</summary>
        public float Progress01
        {
            get
            {
                if (_remaining <= 0f || duration <= 0f) return 1f;
                return Math.Max(0f, 1f - _remaining / duration);
            }
        }

        /// <summary>Uses it if ready and starts the cooldown. Returns whether it was used.</summary>
        public bool TryUse()
        {
            if (!Ready) return false;
            _remaining = duration;
            return true;
        }

        public void Tick(float dt)
        {
            if (_remaining > 0f) _remaining = Math.Max(0f, _remaining - dt);
        }

        /// <summary>Makes it ready right now (pickups, resets on kill).</summary>
        public void Reset() => _remaining = 0f;
    }
}
