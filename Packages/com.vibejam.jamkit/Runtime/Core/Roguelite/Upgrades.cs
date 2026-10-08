using System;
using System.Collections.Generic;

namespace JamKit.Roguelite
{
    /// <summary>Ordered low to high: luck and the draft weights use the order.</summary>
    public enum Rarity { Common, Rare, Epic, Legendary }

    /// <summary>
    /// One pickable upgrade, authored as data (a ScriptableObject wraps a list of these in Unity).
    /// Stat changes go in <see cref="modifiers"/>; anything the stat system can't express
    /// ("pierce", "orbiting blade") goes in <see cref="effects"/> as an id the game code looks up.
    /// </summary>
    [Serializable]
    public class UpgradeDef
    {
        public string id;
        public string displayName;
        public string description;
        public Rarity rarity;
        /// <summary>Draft weight. 0 (or less) = the rarity's default weight from <see cref="UpgradeDraft"/>.</summary>
        public float weight;
        /// <summary>How many times it can be taken. 0 (or less) = unlimited.</summary>
        public int maxStacks = 1;
        public List<string> tags = new List<string>();
        /// <summary>Ids that must all be owned before this is offered (an evolution needs its base).</summary>
        public List<string> requires = new List<string>();
        /// <summary>Ids that can't be owned together with this one. Checked both ways, so listing it on one side is enough.</summary>
        public List<string> excludes = new List<string>();
        public List<StatModifier> modifiers = new List<StatModifier>();
        public List<string> effects = new List<string>();

        public UpgradeDef() { }

        public UpgradeDef(string id, Rarity rarity = Rarity.Common, int maxStacks = 1)
        {
            this.id = id;
            displayName = id;
            this.rarity = rarity;
            this.maxStacks = maxStacks;
        }

        public bool Unlimited => maxStacks <= 0;

        public override string ToString() => id;
    }

    /// <summary>
    /// What the player owns this run: stack counts, the stat modifiers those stacks put on a
    /// <see cref="StatBlock"/>, and effect ids. Each stack gets its own source id
    /// (<c>"upgrade:id#n"</c>) so one stack can come off without touching the others.
    /// </summary>
    public sealed class UpgradeInventory
    {
        readonly Dictionary<string, int> _counts = new Dictionary<string, int>();
        readonly Dictionary<string, int> _effects = new Dictionary<string, int>();
        readonly List<UpgradeDef> _owned = new List<UpgradeDef>();

        /// <summary>Where modifiers are applied. May be null for games that only use effects.</summary>
        public StatBlock Stats { get; }

        /// <summary>Owned upgrades in the order they were first taken (for a build summary screen).</summary>
        public IReadOnlyList<UpgradeDef> Owned => _owned;

        /// <summary>Raised after a successful take, with the def and its new stack count.</summary>
        public event Action<UpgradeDef, int> Taken;

        public UpgradeInventory(StatBlock stats = null) { Stats = stats; }

        public static string SourceId(string upgradeId, int stack) => "upgrade:" + upgradeId + "#" + stack;

        public int Count(string id) => id != null && _counts.TryGetValue(id, out int n) ? n : 0;

        public bool Has(string id) => Count(id) > 0;

        /// <summary>Total stacks of upgrades granting this effect id. Cheap enough to call per frame.</summary>
        public int EffectStacks(string effect) => effect != null && _effects.TryGetValue(effect, out int n) ? n : 0;

        public bool HasEffect(string effect) => EffectStacks(effect) > 0;

        /// <summary>Stacks left, requirements owned, and no exclusion either way.</summary>
        public bool CanTake(UpgradeDef def)
        {
            if (def == null || def.id == null) return false;
            int owned = Count(def.id);
            if (!def.Unlimited && owned >= def.maxStacks) return false;
            if (def.requires != null)
                for (int i = 0; i < def.requires.Count; i++)
                    if (!Has(def.requires[i])) return false;
            if (def.excludes != null)
                for (int i = 0; i < def.excludes.Count; i++)
                    if (Has(def.excludes[i])) return false;
            for (int i = 0; i < _owned.Count; i++)
            {
                var ex = _owned[i].excludes;
                if (ex != null && _owned[i].id != def.id && ex.Contains(def.id)) return false;
            }
            return true;
        }

        /// <summary>Add one stack if <see cref="CanTake"/> allows it. Returns false (and changes nothing) otherwise.</summary>
        public bool Take(UpgradeDef def)
        {
            if (!CanTake(def)) return false;
            ForceTake(def);
            return true;
        }

        /// <summary>Add one stack skipping the rules (debug menu, the draft's fallback, scripted rewards).</summary>
        public int ForceTake(UpgradeDef def)
        {
            if (def == null || def.id == null) throw new ArgumentException("Upgrade needs an id");
            int stack = Count(def.id) + 1;
            _counts[def.id] = stack;
            if (stack == 1) _owned.Add(def);
            if (Stats != null && def.modifiers != null)
            {
                string source = SourceId(def.id, stack);
                for (int i = 0; i < def.modifiers.Count; i++)
                    Stats.Add(def.modifiers[i].WithSource(source));
            }
            if (def.effects != null)
                for (int i = 0; i < def.effects.Count; i++)
                    _effects[def.effects[i]] = EffectStacks(def.effects[i]) + 1;
            Taken?.Invoke(def, stack);
            return stack;
        }

        /// <summary>Forget everything and take its modifiers off the stats (new run).</summary>
        public void Clear()
        {
            if (Stats != null)
                foreach (var pair in _counts)
                    for (int s = 1; s <= pair.Value; s++)
                        Stats.RemoveBySource(SourceId(pair.Key, s));
            _scratchIds.Clear();
            foreach (var pair in _bonuses) _scratchIds.Add(pair.Key);
            for (int i = 0; i < _scratchIds.Count; i++) RemoveBonuses(_scratchIds[i]);
            _counts.Clear();
            _effects.Clear();
            _owned.Clear();
            _bonuses.Clear();
        }

        /// <summary>Every stack of every owned upgrade.</summary>
        public int TotalStacks
        {
            get
            {
                int n = 0;
                foreach (var pair in _counts) n += pair.Value;
                return n;
            }
        }

        /// <summary>The owned def with this id, or null.</summary>
        public UpgradeDef Find(string id)
        {
            for (int i = 0; i < _owned.Count; i++)
                if (_owned[i].id == id) return _owned[i];
            return null;
        }

        /// <summary>
        /// Takes up to <paramref name="stacks"/> stacks of an owned upgrade away (the newest first): their
        /// modifiers come off the stats and their effects stop counting. When the last stack goes, the
        /// upgrade leaves <see cref="Owned"/> and its bonus levels (<see cref="AddBonus"/>) go with it.
        /// Sacrifices, trades and a forge that breaks use this. Returns how many stacks were removed.
        /// </summary>
        public int Remove(string id, int stacks = int.MaxValue)
        {
            int owned = Count(id);
            if (owned <= 0 || stacks <= 0) return 0;
            var def = Find(id);
            int n = Math.Min(owned, stacks);
            for (int s = owned; s > owned - n; s--)
            {
                if (Stats != null) Stats.RemoveBySource(SourceId(id, s));
                if (def != null && def.effects != null)
                    for (int i = 0; i < def.effects.Count; i++) DropEffect(def.effects[i]);
            }
            int left = owned - n;
            if (left > 0) _counts[id] = left;
            else
            {
                _counts.Remove(id);
                if (def != null) _owned.Remove(def);
                RemoveBonuses(id);
            }
            Removed?.Invoke(def, left);
            return n;
        }

        /// <summary>Raised after <see cref="Remove"/>, with the def and the stacks left (0 = gone).</summary>
        public event Action<UpgradeDef, int> Removed;

        // ---------- Bonus levels (a forge push, an altar's blessing) ----------

        sealed class BonusLevel
        {
            public readonly List<string> effects = new List<string>();
        }

        readonly Dictionary<string, List<BonusLevel>> _bonuses = new Dictionary<string, List<BonusLevel>>();
        readonly List<string> _scratchIds = new List<string>();

        public static string BonusSourceId(string upgradeId, int level) => "bonus:" + upgradeId + "#" + level;

        /// <summary>Bonus levels on an owned upgrade (0 = none).</summary>
        public int BonusLevels(string id) => id != null && _bonuses.TryGetValue(id, out var list) ? list.Count : 0;

        /// <summary>
        /// One more level on an owned upgrade: extra modifiers and effects that belong to it, so they
        /// leave with it (sacrificed, broken, traded). Returns the new level, or 0 if it isn't owned.
        /// </summary>
        public int AddBonus(string id, IReadOnlyList<StatModifier> modifiers, IReadOnlyList<string> effects)
        {
            if (!Has(id)) return 0;
            if (!_bonuses.TryGetValue(id, out var list)) _bonuses[id] = list = new List<BonusLevel>();
            var level = new BonusLevel();
            list.Add(level);
            string source = BonusSourceId(id, list.Count);
            if (Stats != null && modifiers != null)
                for (int i = 0; i < modifiers.Count; i++)
                    if (modifiers[i] != null) Stats.Add(modifiers[i].WithSource(source));
            if (effects != null)
                for (int i = 0; i < effects.Count; i++)
                {
                    if (string.IsNullOrEmpty(effects[i])) continue;
                    level.effects.Add(effects[i]);
                    _effects[effects[i]] = EffectStacks(effects[i]) + 1;
                }
            return list.Count;
        }

        /// <summary>Takes every bonus level off an upgrade. Returns how many there were.</summary>
        public int RemoveBonuses(string id)
        {
            if (id == null || !_bonuses.TryGetValue(id, out var list)) return 0;
            int n = list.Count;
            for (int l = 0; l < n; l++)
            {
                if (Stats != null) Stats.RemoveBySource(BonusSourceId(id, l + 1));
                var fx = list[l].effects;
                for (int i = 0; i < fx.Count; i++) DropEffect(fx[i]);
            }
            _bonuses.Remove(id);
            return n;
        }

        void DropEffect(string effect)
        {
            if (effect == null) return;
            int left = EffectStacks(effect) - 1;
            if (left > 0) _effects[effect] = left;
            else _effects.Remove(effect);
        }
    }

    /// <summary>
    /// The "pick 1 of 3" screen: distinct, eligible upgrades weighted by rarity. Luck multiplies
    /// each rarity's weight by <c>1 + luck * tier</c> (Common 0 .. Legendary 3), so luck 1 makes a
    /// Legendary 4x as likely relative to a Common; negative luck makes rare things rarer.
    /// </summary>
    public sealed class UpgradeDraft
    {
        readonly float[] _rarityWeights = { 100f, 35f, 12f, 3f };
        readonly List<UpgradeDef> _candidates = new List<UpgradeDef>();
        readonly List<float> _weights = new List<float>();
        readonly List<UpgradeDef> _previous = new List<UpgradeDef>();

        /// <summary>Offered once when the pool runs dry ("+25 gold", "heal"), so the screen never shows
        /// an empty slot. It skips the eligibility rules; null = just offer fewer.</summary>
        public UpgradeDef Fallback;

        public UpgradeDraft(UpgradeDef fallback = null) { Fallback = fallback; }

        public float GetRarityWeight(Rarity r) => _rarityWeights[(int)r];

        public void SetRarityWeight(Rarity r, float weight) => _rarityWeights[(int)r] = weight;

        /// <summary>The weight this def gets in a draw at the given luck.</summary>
        public float WeightOf(UpgradeDef def, float luck = 0f)
        {
            float w = def.weight > 0f ? def.weight : _rarityWeights[(int)def.rarity];
            float factor = 1f + luck * (int)def.rarity;
            return factor > 0f ? w * factor : 0f;
        }

        /// <summary>
        /// Clear <paramref name="results"/> and fill it with up to <paramref name="count"/> distinct
        /// choices the inventory can take; tops up with <see cref="Fallback"/> (once) if the pool runs
        /// dry. Returns how many real (non-fallback) choices were found.
        /// </summary>
        public int Offer(IReadOnlyList<UpgradeDef> pool, UpgradeInventory inventory, int count, Rng rng,
            List<UpgradeDef> results, float luck = 0f)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            results.Clear();
            int found = Draw(pool, inventory, count, rng, results, luck, null, true);
            Remember(results);
            return found;
        }

        /// <summary>
        /// Same rules as <see cref="Offer"/>, but avoids what was shown last time (by the last Offer or
        /// Reroll) when the pool has enough other choices, so a reroll actually changes the cards.
        /// </summary>
        public int Reroll(IReadOnlyList<UpgradeDef> pool, UpgradeInventory inventory, int count, Rng rng,
            List<UpgradeDef> results, float luck = 0f)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            results.Clear();
            var avoid = new List<UpgradeDef>(_previous);
            int found = Draw(pool, inventory, count, rng, results, luck, avoid, false);
            if (found < count) // not enough fresh cards: let last time's back in before the fallback
                found += Draw(pool, inventory, count - found, rng, results, luck, results, true);
            Remember(results);
            return found;
        }

        int Draw(IReadOnlyList<UpgradeDef> pool, UpgradeInventory inv, int count, Rng rng,
            List<UpgradeDef> results, float luck, List<UpgradeDef> avoid, bool allowFallback)
        {
            Collect(pool, inv, luck, avoid);
            return Pick(count, rng, results, allowFallback);
        }

        void Collect(IReadOnlyList<UpgradeDef> pool, UpgradeInventory inv, float luck, List<UpgradeDef> avoid)
        {
            _candidates.Clear();
            _weights.Clear();
            if (pool == null) return;
            for (int i = 0; i < pool.Count; i++)
            {
                var def = pool[i];
                if (def == null || def.id == null || ReferenceEquals(def, Fallback)) continue;
                if (ContainsId(_candidates, def.id) || (avoid != null && ContainsId(avoid, def.id))) continue;
                if (inv != null && !inv.CanTake(def)) continue;
                float w = WeightOf(def, luck);
                if (w <= 0f) continue;
                _candidates.Add(def);
                _weights.Add(w);
            }
        }

        int Pick(int count, Rng rng, List<UpgradeDef> results, bool allowFallback)
        {
            int found = 0;
            while (found < count && _candidates.Count > 0)
            {
                int idx = rng.WeightedIndex(_weights);
                if (idx < 0) break;
                results.Add(_candidates[idx]);
                int last = _candidates.Count - 1;
                _candidates[idx] = _candidates[last];
                _weights[idx] = _weights[last];
                _candidates.RemoveAt(last);
                _weights.RemoveAt(last);
                found++;
            }
            if (allowFallback && found < count && Fallback != null && !results.Contains(Fallback))
                results.Add(Fallback);
            return found;
        }

        void Remember(List<UpgradeDef> results)
        {
            _previous.Clear();
            _previous.AddRange(results);
        }

        static bool ContainsId(List<UpgradeDef> list, string id)
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null && list[i].id == id) return true;
            return false;
        }
    }
}
