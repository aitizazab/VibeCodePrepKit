using System;
using System.Collections.Generic;

namespace JamKit.Skyline
{
    /// <summary>
    /// Every bot's visible damage stages (the creator, 2026-10-14, docs/SKYLINE-BOTS.md "Damage states"): "a hit that takes a drone from
    /// above 90% to less than 30% of its health should show that damage in the model, with an animation that shows that damage happening."
    /// The stage is how many of the thresholds (a fraction of the bot's health: its chassis, over what it started with) the bot is at or
    /// below: 0 undamaged, 1 at 70% or less, 2 at 30% or less. <see cref="Observe"/> reports each stage a drop crosses, in order, so one
    /// hit that crosses several plays each stage's animation back to back. A rise (a practice bot refilling) moves the stage up the
    /// other way without reporting anything: it is mended, not damaged.
    /// </summary>
    public sealed class DamageStates
    {
        /// <summary>The thresholds in every bot's starting set: 70% and 30%.</summary>
        public static readonly float[] DefaultThresholds = { 0.7f, 0.3f };

        readonly float[] _thresholds;

        public DamageStates(float[] thresholds = null)
        {
            _thresholds = thresholds != null && thresholds.Length > 0 ? (float[])thresholds.Clone() : (float[])DefaultThresholds.Clone();
            Array.Sort(_thresholds);
            Array.Reverse(_thresholds);   // highest first
        }

        /// <summary>How many stages there are besides undamaged.</summary>
        public int Count => _thresholds.Length;

        /// <summary>The stage the bot is at now.</summary>
        public int Stage { get; private set; }

        /// <summary>The stage a bot at <paramref name="fraction"/> of its integrity is at (a NaN counts as whole).</summary>
        public int StageAt(float fraction)
        {
            if (float.IsNaN(fraction)) return 0;
            int s = 0;
            for (int i = 0; i < _thresholds.Length; i++) if (fraction <= _thresholds[i]) s = i + 1;
            return s;
        }

        /// <summary>Notes the bot's integrity. Each stage newly reached by a drop is added to <paramref name="crossed"/> (1, then 2, in
        /// order); returns the stage now.</summary>
        public int Observe(float fraction, List<int> crossed)
        {
            int now = StageAt(fraction);
            if (now > Stage && crossed != null)
                for (int s = Stage + 1; s <= now; s++) crossed.Add(s);
            Stage = now;
            return now;
        }

        /// <summary>Back to undamaged, reporting nothing.</summary>
        public void Reset() => Stage = 0;

        /// <summary>A body's health for the stages: its chassis over what it started with (0 to 1). The plates are armour over it; their
        /// shedding is its own, separate show, and a bot is wrecked when the chassis is gone whatever plates are left, so this always
        /// reaches 0.</summary>
        public static float Integrity(EnemyBody b)
        {
            if (b == null) return 1f;
            float max = Math.Max(1f, b.Def.chassisHp);
            return Math.Max(0f, Math.Min(1f, b.Chassis / max));
        }
    }
}
