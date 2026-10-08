using System;
using System.Collections.Generic;

namespace JamKit.Roguelite
{
    public enum StatOp
    {
        /// <summary>Added to the base: +5 damage.</summary>
        Flat,
        /// <summary>Summed with the other percents, then applied once: 0.1 = +10%. Two +10% make +20%, not +21%.</summary>
        PercentAdd,
        /// <summary>Multiplied in after everything else: 1.5 = x1.5. Stacks multiplicatively; use it sparingly.</summary>
        Multiply,
    }

    /// <summary>
    /// One change to one stat, tagged with where it came from so it can be taken off again
    /// (an upgrade stack, a buff, an item). Treat it as immutable once added to a <see cref="StatBlock"/>:
    /// the block caches results and won't notice edits.
    /// </summary>
    [Serializable]
    public class StatModifier
    {
        public string stat;
        public StatOp op;
        public float value;
        public string sourceId;

        public StatModifier() { }

        public StatModifier(string stat, StatOp op, float value, string sourceId = null)
        {
            this.stat = stat;
            this.op = op;
            this.value = value;
            this.sourceId = sourceId;
        }

        public StatModifier WithSource(string source) => new StatModifier(stat, op, value, source);

        public override string ToString() => $"{stat} {op} {value} ({sourceId})";
    }

    /// <summary>
    /// A character's numbers (damage, fire rate, move speed...): base values plus modifiers.
    /// <c>Get(key) = (base + sum Flat) * (1 + sum PercentAdd) * product Multiply</c>, then the optional clamp.
    /// Results are cached per stat and recomputed only after a change, so reading every frame is cheap.
    /// Unknown keys read as 0 (base 0, so a lone Flat modifier on an unset stat still counts).
    /// </summary>
    public sealed class StatBlock
    {
        struct Clamp { public float Min, Max; }

        readonly Dictionary<string, float> _base = new Dictionary<string, float>();
        readonly Dictionary<string, List<StatModifier>> _mods = new Dictionary<string, List<StatModifier>>();
        readonly Dictionary<string, float> _cache = new Dictionary<string, float>();
        readonly Dictionary<string, Clamp> _clamps = new Dictionary<string, Clamp>();
        readonly List<string> _scratchKeys = new List<string>();

        /// <summary>Raised with the stat key whenever that stat's inputs change (base, modifiers, clamp).</summary>
        public event Action<string> Changed;

        public float Get(string key)
        {
            if (key == null) return 0f;
            if (_cache.TryGetValue(key, out float cached)) return cached;
            float value = Compute(key);
            _cache[key] = value;
            return value;
        }

        public float GetBase(string key) => key != null && _base.TryGetValue(key, out float v) ? v : 0f;

        public bool HasBase(string key) => key != null && _base.ContainsKey(key);

        public void SetBase(string key, float value)
        {
            _base[key] = value;
            MarkDirty(key);
        }

        /// <summary>Keep a stat inside [min, max] after modifiers (fire rate never below a floor, crit &lt;= 1).</summary>
        public void SetClamp(string key, float min, float max)
        {
            if (max < min) throw new ArgumentException("max must be >= min");
            _clamps[key] = new Clamp { Min = min, Max = max };
            MarkDirty(key);
        }

        public void ClearClamp(string key)
        {
            if (_clamps.Remove(key)) MarkDirty(key);
        }

        public void Add(StatModifier mod)
        {
            if (mod == null || mod.stat == null) throw new ArgumentException("Modifier needs a stat key");
            if (!_mods.TryGetValue(mod.stat, out var list)) _mods[mod.stat] = list = new List<StatModifier>();
            list.Add(mod);
            MarkDirty(mod.stat);
        }

        /// <summary>Remove one specific modifier instance. Returns false if it wasn't there.</summary>
        public bool Remove(StatModifier mod)
        {
            if (mod == null || mod.stat == null || !_mods.TryGetValue(mod.stat, out var list)) return false;
            if (!list.Remove(mod)) return false;
            MarkDirty(mod.stat);
            return true;
        }

        /// <summary>Remove every modifier from one source (an upgrade stack, an expired buff). Returns how many.</summary>
        public int RemoveBySource(string sourceId)
        {
            int removed = 0;
            _scratchKeys.Clear();
            foreach (var pair in _mods)
            {
                int n = pair.Value.RemoveAll(m => m.sourceId == sourceId);
                if (n > 0) { removed += n; _scratchKeys.Add(pair.Key); }
            }
            for (int i = 0; i < _scratchKeys.Count; i++) MarkDirty(_scratchKeys[i]);
            return removed;
        }

        /// <summary>Drop all modifiers but keep base values and clamps (start of a new run).</summary>
        public void ClearModifiers()
        {
            _scratchKeys.Clear();
            foreach (var pair in _mods)
                if (pair.Value.Count > 0) { pair.Value.Clear(); _scratchKeys.Add(pair.Key); }
            for (int i = 0; i < _scratchKeys.Count; i++) MarkDirty(_scratchKeys[i]);
        }

        public int ModifierCount(string key) => key != null && _mods.TryGetValue(key, out var list) ? list.Count : 0;

        float Compute(string key)
        {
            float flat = GetBase(key), pct = 0f, mult = 1f;
            if (_mods.TryGetValue(key, out var list))
            {
                for (int i = 0; i < list.Count; i++)
                {
                    var m = list[i];
                    switch (m.op)
                    {
                        case StatOp.Flat: flat += m.value; break;
                        case StatOp.PercentAdd: pct += m.value; break;
                        case StatOp.Multiply: mult *= m.value; break;
                    }
                }
            }
            float value = flat * (1f + pct) * mult;
            if (_clamps.TryGetValue(key, out var c))
                value = value < c.Min ? c.Min : value > c.Max ? c.Max : value;
            return value;
        }

        void MarkDirty(string key)
        {
            _cache.Remove(key);
            Changed?.Invoke(key);
        }
    }
}
