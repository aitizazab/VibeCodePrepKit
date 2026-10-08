using System;
using System.Collections.Generic;
using JamKit.Roguelite;

namespace JamKit.BulletHell
{
    /// <summary>Where a reward screen comes from. Each has its own <see cref="RewardTable"/>.</summary>
    public enum RewardSource { Battle, Elite, Treasure, Rest, Event, Shop, Boss, Trial, Sacrifice, Wager, Trader }

    /// <summary>
    /// What one reward screen can offer: a weight per rarity (0 = never), the number of cards, a small
    /// chance that one card is upgraded to an epic, and a luck bonus. Bosses, trials and sacrifices
    /// offer epics; normal battles offer commons and rares (docs/DESIGN-ROGUELITE.md, section 1).
    /// </summary>
    [Serializable]
    public class RewardTable
    {
        /// <summary>Off: this source gives no reward screen (for Battle: no draft after normal fights).</summary>
        public bool enabled = true;
        public float commonWeight = 100f;
        public float rareWeight = 35f;
        public float epicWeight;
        public float legendaryWeight;
        /// <summary>Chance (0..1) that one card of the screen is swapped for an epic (only when the screen
        /// has none). Luck raises it: chance x (1 + luck).</summary>
        public float epicChance;
        /// <summary>Cards on the screen. 0 = the economy's draft size.</summary>
        public int cards;
        /// <summary>Added to the player's luck for this screen.</summary>
        public float luckBonus;
        /// <summary>A remnant of an earlier run may take one card (see <see cref="RemnantRules"/>).</summary>
        public bool includeRemnants = true;

        public RewardTable() { }

        public RewardTable(float common, float rare, float epic, float legendary, float epicChance = 0f, float luckBonus = 0f)
        {
            commonWeight = common;
            rareWeight = rare;
            epicWeight = epic;
            legendaryWeight = legendary;
            this.epicChance = epicChance;
            this.luckBonus = luckBonus;
        }

        public float WeightOf(Rarity r)
        {
            switch (r)
            {
                case Rarity.Common: return commonWeight;
                case Rarity.Rare: return rareWeight;
                case Rarity.Epic: return epicWeight;
                default: return legendaryWeight;
            }
        }

        public bool Allows(Rarity r) => WeightOf(r) > 0f;

        /// <summary>The lowest rarity this table offers (Legendary if it offers nothing).</summary>
        public Rarity LowestRarity
        {
            get
            {
                for (int r = 0; r <= (int)Rarity.Legendary; r++) if (Allows((Rarity)r)) return (Rarity)r;
                return Rarity.Legendary;
            }
        }

        /// <summary>Problems in plain words (negative weights, nothing to offer, a chance outside 0..1).</summary>
        public void Validate(string where, List<string> errors)
        {
            if (commonWeight < 0f || rareWeight < 0f || epicWeight < 0f || legendaryWeight < 0f)
                errors.Add(where + ": rarity weights can't be negative.");
            if (enabled && commonWeight <= 0f && rareWeight <= 0f && epicWeight <= 0f && legendaryWeight <= 0f)
                errors.Add(where + ": every rarity weight is 0, so it can offer nothing.");
            if (epicChance < 0f || epicChance > 1f) errors.Add(where + ": the epic chance must be between 0 and 1.");
            if (cards < 0 || cards > 5) errors.Add(where + ": cards must be 0 (the draft size) to 5.");
        }
    }

    /// <summary>The reward table for every source, as data (BulletHellConfig holds one).</summary>
    [Serializable]
    public class ShmupRewards
    {
        public RewardTable battle = new RewardTable(100f, 35f, 0f, 0f, 0.05f);
        public RewardTable elite = new RewardTable(60f, 60f, 0f, 0f, 0.15f, 0.3f);
        public RewardTable treasure = new RewardTable(100f, 45f, 0f, 0f, 0.1f);
        public RewardTable rest = new RewardTable(100f, 35f, 0f, 0f, 0.05f);
        public RewardTable eventDraft = new RewardTable(100f, 40f, 0f, 0f, 0.05f);
        public RewardTable shop = new RewardTable(100f, 40f, 0f, 0f, 0.1f);
        public RewardTable boss = new RewardTable(0f, 0f, 100f, 12f);
        public RewardTable trial = new RewardTable(0f, 0f, 100f, 8f);
        public RewardTable sacrifice = new RewardTable(0f, 0f, 100f, 8f);
        public RewardTable wager = new RewardTable(0f, 0f, 100f, 10f);
        public RewardTable trader = new RewardTable(0f, 100f, 0f, 0f);

        public RewardTable Get(RewardSource source)
        {
            switch (source)
            {
                case RewardSource.Battle: return battle;
                case RewardSource.Elite: return elite;
                case RewardSource.Treasure: return treasure;
                case RewardSource.Rest: return rest;
                case RewardSource.Event: return eventDraft;
                case RewardSource.Shop: return shop;
                case RewardSource.Boss: return boss;
                case RewardSource.Trial: return trial;
                case RewardSource.Sacrifice: return sacrifice;
                case RewardSource.Wager: return wager;
                default: return trader;
            }
        }

        public static readonly RewardSource[] AllSources =
        {
            RewardSource.Battle, RewardSource.Elite, RewardSource.Treasure, RewardSource.Rest, RewardSource.Event, RewardSource.Shop,
            RewardSource.Boss, RewardSource.Trial, RewardSource.Sacrifice, RewardSource.Wager, RewardSource.Trader,
        };

        /// <summary>Every table's problems, plus the design rules: bosses, trials, sacrifices and wagers
        /// offer only epics (or better); normal battles never offer epics except through the small chance.</summary>
        public List<string> Validate()
        {
            var errors = new List<string>();
            foreach (var s in AllSources)
            {
                var t = Get(s);
                if (t == null) { errors.Add("Rewards: no table for " + s + "."); continue; }
                t.Validate("Rewards " + s, errors);
            }
            foreach (var s in new[] { RewardSource.Boss, RewardSource.Trial, RewardSource.Sacrifice, RewardSource.Wager })
            {
                var t = Get(s);
                if (t != null && t.LowestRarity < Rarity.Epic) errors.Add("Rewards " + s + ": must offer only epics or better (its common/rare weights should be 0).");
            }
            if (battle != null && (battle.epicWeight > 0f || battle.legendaryWeight > 0f))
                errors.Add("Rewards Battle: epics come from the epic chance only (keep its epic and legendary weights at 0).");
            if (battle != null && battle.epicChance > 0.25f)
                errors.Add("Rewards Battle: an epic chance over 25% makes epics common.");
            return errors;
        }
    }

    /// <summary>
    /// Fills a reward screen from a <see cref="RewardTable"/>: distinct cards the player can take, at
    /// the table's rarity weights, the small epic chance, and a top-up from the next rarity down when an
    /// epic-only screen runs out of epics (then the fallback). A reroll avoids the last screen's cards
    /// when there are enough others. Allocation-free after warm-up.
    /// </summary>
    public sealed class RewardDraw
    {
        readonly UpgradeDraft _draft;
        readonly List<UpgradeDef> _allowed = new List<UpgradeDef>();
        readonly List<UpgradeDef> _tmp = new List<UpgradeDef>();
        readonly List<UpgradeDef> _lastShown = new List<UpgradeDef>();

        public RewardDraw(UpgradeDef fallback = null) { _draft = new UpgradeDraft(fallback); }

        public UpgradeDef Fallback => _draft.Fallback;

        /// <summary>
        /// Clears <paramref name="results"/> and fills it with up to <paramref name="cards"/> cards. Returns
        /// how many are real (not the fallback). <paramref name="reroll"/> avoids the last screen's cards.
        /// </summary>
        public int Offer(RewardTable table, IReadOnlyList<UpgradeDef> pool, UpgradeInventory inventory, int cards, Rng rng,
            List<UpgradeDef> results, float luck, bool reroll = false)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            results.Clear();
            if (table == null || pool == null || cards <= 0) { Remember(results); return 0; }
            float l = luck + table.luckBonus;

            // The table's rarities (minus last screen's cards on a reroll, if enough others remain).
            Collect(pool, inventory, table, reroll ? _lastShown : null, _allowed);
            if (reroll && _allowed.Count < cards) Collect(pool, inventory, table, null, _allowed);
            for (int r = 0; r <= (int)Rarity.Legendary; r++) _draft.SetRarityWeight((Rarity)r, table.WeightOf((Rarity)r));
            int found = DrawInto(_allowed, inventory, cards, rng, results, l);

            // Short on cards (an epic screen late in the run): top up from the next rarity down.
            for (int r = (int)table.LowestRarity - 1; r >= 0 && found < cards; r--)
            {
                _allowed.Clear();
                for (int i = 0; i < pool.Count; i++)
                {
                    var d = pool[i];
                    if (d != null && (int)d.rarity == r && !Contains(results, d.id) && (inventory == null || inventory.CanTake(d))) _allowed.Add(d);
                }
                if (_allowed.Count == 0) continue;
                _draft.SetRarityWeight((Rarity)r, 100f);
                found += DrawInto(_allowed, inventory, cards - found, rng, results, l);
            }

            // The small chance of an epic on a common screen: it takes the last card's place.
            if (table.epicChance > 0f && found > 0 && !HasEpic(results))
            {
                float chance = Math.Min(1f, table.epicChance * (1f + Math.Max(0f, l)));
                if (rng.Chance(chance))
                {
                    _allowed.Clear();
                    for (int i = 0; i < pool.Count; i++)
                    {
                        var d = pool[i];
                        if (d != null && d.rarity == Rarity.Epic && !Contains(results, d.id) && (inventory == null || inventory.CanTake(d))) _allowed.Add(d);
                    }
                    if (_allowed.Count > 0) results[results.Count - 1] = _allowed[rng.Range(0, _allowed.Count)];
                }
            }

            if (found < cards && _draft.Fallback != null && !results.Contains(_draft.Fallback)) results.Add(_draft.Fallback);
            Remember(results);
            return found;
        }

        /// <summary>Cards drawn (no fallback, no duplicates) appended to <paramref name="results"/>.</summary>
        int DrawInto(List<UpgradeDef> candidates, UpgradeInventory inventory, int count, Rng rng, List<UpgradeDef> results, float luck)
        {
            _tmp.Clear();
            _draft.Offer(candidates, inventory, count, rng, _tmp, luck);
            int added = 0;
            for (int i = 0; i < _tmp.Count; i++)
            {
                var d = _tmp[i];
                if (ReferenceEquals(d, _draft.Fallback) || Contains(results, d.id)) continue;
                results.Add(d);
                added++;
            }
            _tmp.Clear();
            return added;
        }

        static void Collect(IReadOnlyList<UpgradeDef> pool, UpgradeInventory inventory, RewardTable table, List<UpgradeDef> avoid, List<UpgradeDef> into)
        {
            into.Clear();
            for (int i = 0; i < pool.Count; i++)
            {
                var d = pool[i];
                if (d == null || !table.Allows(d.rarity)) continue;
                if (avoid != null && Contains(avoid, d.id)) continue;
                if (inventory != null && !inventory.CanTake(d)) continue;
                into.Add(d);
            }
        }

        void Remember(List<UpgradeDef> results)
        {
            _lastShown.Clear();
            _lastShown.AddRange(results);
        }

        static bool HasEpic(List<UpgradeDef> list)
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null && list[i].rarity >= Rarity.Epic) return true;
            return false;
        }

        static bool Contains(List<UpgradeDef> list, string id)
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null && list[i].id == id) return true;
            return false;
        }
    }
}
