using System;
using System.Collections.Generic;

namespace JamKit.BulletHell
{
    /// <summary>
    /// Which phase a boss is in, from its health fraction. Phase i starts when health drops to
    /// thresholds[i] or below (phase 0's threshold should be 1). Phases only move forward, and a
    /// big hit can skip one. Thresholds are read once, in the order given.
    /// </summary>
    public sealed class BossPhaseTracker
    {
        readonly float[] _thresholds;
        int _phase;

        public BossPhaseTracker(IReadOnlyList<float> thresholds)
        {
            if (thresholds == null || thresholds.Count == 0) throw new ArgumentException("A boss needs at least one phase");
            _thresholds = new float[thresholds.Count];
            for (int i = 0; i < thresholds.Count; i++) _thresholds[i] = thresholds[i];
        }

        public int Phase => _phase;
        public int PhaseCount => _thresholds.Length;
        public bool IsLastPhase => _phase >= _thresholds.Length - 1;

        /// <summary>The phase health says it should be in (ignoring the forward-only rule).</summary>
        public int PhaseFor(float healthFraction)
        {
            int p = 0;
            for (int i = 0; i < _thresholds.Length; i++)
                if (healthFraction <= _thresholds[i]) p = i;
            return p;
        }

        /// <summary>Moves to the right phase for this health. Returns true when the phase changed.</summary>
        public bool Update(float healthFraction)
        {
            int p = PhaseFor(healthFraction);
            if (p <= _phase) return false;
            _phase = p;
            return true;
        }

        /// <summary>Thresholds must start at 1 (or more) and go strictly down, each above 0.</summary>
        public static List<string> Validate(IReadOnlyList<float> thresholds)
        {
            var errors = new List<string>();
            if (thresholds == null || thresholds.Count == 0) { errors.Add("no phases"); return errors; }
            if (thresholds[0] < 1f) errors.Add("the first phase must start at full health (threshold 1)");
            for (int i = 1; i < thresholds.Count; i++)
            {
                if (thresholds[i] >= thresholds[i - 1]) errors.Add($"phase {i + 1} threshold {thresholds[i]} isn't below phase {i}'s");
                if (thresholds[i] <= 0f) errors.Add($"phase {i + 1} threshold must be above 0");
            }
            return errors;
        }
    }
}
