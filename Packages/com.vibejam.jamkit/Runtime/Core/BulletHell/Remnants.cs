using System;
using System.Collections.Generic;
using JamKit.Roguelite;

namespace JamKit.BulletHell
{
    /// <summary>
    /// How a dead run leaves something behind (docs/DESIGN-ROGUELITE.md, section 5): the run's best
    /// upgrade, weakened, plus a small stat from how you died, saved as a remnant that later drafts can
    /// offer (marked REMNANT). Kept in the save; a remnant is used up when taken.
    /// </summary>
    [Serializable]
    public class RemnantRules
    {
        public bool enabled = true;
        /// <summary>Remnants the save keeps (the oldest goes when a new one would exceed it).</summary>
        public int maxKept = 3;
        /// <summary>The best upgrade's stat modifiers at this strength (0.5 = half).</summary>
        public float statScale = 0.5f;
        /// <summary>Chance (0..1) that a draft shows one remnant (one at most per draft).</summary>
        public float offerChance = 0.4f;
        /// <summary>The first draft of each run always shows one, if any are saved.</summary>
        public bool firstDraftGuaranteed = true;
        /// <summary>The stat from how you died (PercentAdd), by who did it.</summary>
        public string bossDeathStat = ShmupStats.Damage;
        public string eliteDeathStat = ShmupStats.FireRate;
        public string battleDeathStat = ShmupStats.MoveSpeed;
        public float deathStatValue = 0.06f;

        public void Validate(List<string> errors)
        {
            if (maxKept < 1 || maxKept > 10) errors.Add("Remnants: keep 1 to 10.");
            if (statScale <= 0f || statScale > 1f) errors.Add("Remnants: the stat scale must be above 0 and at most 1.");
            if (offerChance < 0f || offerChance > 1f) errors.Add("Remnants: the offer chance must be between 0 and 1.");
            foreach (var s in new[] { bossDeathStat, eliteDeathStat, battleDeathStat })
                if (!string.IsNullOrEmpty(s) && Array.IndexOf(ShmupStats.All, s) < 0) errors.Add("Remnants: unknown death stat \"" + s + "\".");
        }
    }

    /// <summary>One saved remnant: everything needed to rebuild it as an upgrade card.</summary>
    [Serializable]
    public class RemnantRecord
    {
        public string id;
        public string displayName;
        public string description;
        public Rarity rarity;
        /// <summary>The upgrade it echoes ("" when the run had none).</summary>
        public string sourceId = "";
        /// <summary>Who ended the run ("battle", "elite" or a boss id) and where (floor from 1).</summary>
        public string killer = "";
        public int floor;
        /// <summary>The run it came from (the save's runs started at the time).</summary>
        public int run;
        public List<StatModifier> modifiers = new List<StatModifier>();
        public List<string> effects = new List<string>();
        public List<string> tags = new List<string>();

        /// <summary>The card: a one-stack upgrade tagged "remnant".</summary>
        public UpgradeDef ToUpgrade()
        {
            var d = new UpgradeDef(id, rarity, 1) { displayName = displayName, description = description };
            if (modifiers != null) foreach (var m in modifiers) if (m != null) d.modifiers.Add(new StatModifier(m.stat, m.op, m.value));
            if (effects != null) d.effects.AddRange(effects);
            if (tags != null) d.tags.AddRange(tags);
            if (!d.tags.Contains(Remnants.Tag)) d.tags.Add(Remnants.Tag);
            return d;
        }
    }

    /// <summary>The save's remnants (newest last). Serialized as JSON by the Unity side.</summary>
    [Serializable]
    public class RemnantBook
    {
        public List<RemnantRecord> remnants = new List<RemnantRecord>();

        public int Count => remnants != null ? remnants.Count : 0;

        /// <summary>Adds one, dropping the oldest past <paramref name="maxKept"/>.</summary>
        public void Add(RemnantRecord r, int maxKept)
        {
            if (r == null) return;
            if (remnants == null) remnants = new List<RemnantRecord>();
            remnants.Add(r);
            while (remnants.Count > Math.Max(1, maxKept)) remnants.RemoveAt(0);
        }

        public bool Remove(string id)
        {
            if (remnants == null) return false;
            for (int i = 0; i < remnants.Count; i++)
                if (remnants[i] != null && remnants[i].id == id) { remnants.RemoveAt(i); return true; }
            return false;
        }

        public RemnantRecord Find(string id)
        {
            if (remnants == null) return null;
            for (int i = 0; i < remnants.Count; i++)
                if (remnants[i] != null && remnants[i].id == id) return remnants[i];
            return null;
        }
    }

    public static class Remnants
    {
        /// <summary>The tag every remnant card carries (drafts mark it, the HUD and the forge read it).</summary>
        public const string Tag = "remnant";

        public static bool IsRemnant(UpgradeDef d) => d != null && d.tags != null && d.tags.Contains(Tag);

        /// <summary>
        /// The remnant a dead run leaves. The best upgrade is the rarest owned (remnants and instant
        /// pickups such as coins skipped), then the one with the most stacks, then the latest taken: its
        /// stat modifiers at <see cref="RemnantRules.statScale"/>, its effects kept. Then the stat from how
        /// you died. Null when remnants are off. <paramref name="lookupName"/> may be null.
        /// </summary>
        public static RemnantRecord Build(UpgradeInventory inventory, string killer, int floor, int run, RemnantRules rules)
        {
            if (rules == null || !rules.enabled) return null;
            UpgradeDef best = null;
            int bestStacks = 0;
            if (inventory != null)
            {
                var owned = inventory.Owned;
                for (int i = 0; i < owned.Count; i++)
                {
                    var d = owned[i];
                    if (IsRemnant(d) || !HasLastingEffect(d)) continue;
                    int stacks = inventory.Count(d.id);
                    if (best == null || d.rarity > best.rarity || (d.rarity == best.rarity && stacks >= bestStacks))
                    {
                        best = d;
                        bestStacks = stacks;
                    }
                }
            }
            string k = string.IsNullOrEmpty(killer) ? "battle" : killer;
            var r = new RemnantRecord
            {
                id = "remnant_" + Math.Max(1, run),
                killer = k,
                floor = floor,
                run = run,
                rarity = best != null ? best.rarity : Rarity.Common,
                sourceId = best != null ? best.id : "",
            };
            var parts = new List<string>();
            if (best != null)
            {
                if (best.modifiers != null)
                    foreach (var m in best.modifiers)
                        if (m != null) r.modifiers.Add(ForgeRules.Scale(m, rules.statScale));
                if (best.effects != null)
                    foreach (var e in best.effects)
                        if (!IsInstant(e)) r.effects.Add(e);
                if (best.tags != null) foreach (var t in best.tags) if (t != Tag) r.tags.Add(t);
                parts.Add(r.effects.Count > 0 ? best.displayName + "'s effect" : "half of " + best.displayName);
            }
            string stat = DeathStat(k, rules);
            if (!string.IsNullOrEmpty(stat) && rules.deathStatValue != 0f)
            {
                r.modifiers.Add(new StatModifier(stat, StatOp.PercentAdd, rules.deathStatValue));
                parts.Add("+" + Math.Round(rules.deathStatValue * 100f) + "% " + StatWords(stat));
            }
            r.displayName = best != null ? "Echo of " + best.displayName : "Last Breath";
            r.description = "From run " + Math.Max(1, run) + ", lost on floor " + Math.Max(1, floor) + " (" + KillerWords(k) + "): "
                + string.Join(", ", parts) + ".";
            return r;
        }

        /// <summary>An upgrade that keeps doing something once owned: stat modifiers or a lasting effect. Not
        /// "+15 coins" or "+1 life", which pay out once when taken: the encounters (altar offerings, trades,
        /// sales) and remnants ignore those, so they can't be cashed in twice.</summary>
        public static bool HasLastingEffect(UpgradeDef d)
        {
            if (d == null) return false;
            if (d.modifiers != null && d.modifiers.Count > 0) return true;
            if (d.effects != null)
                foreach (var e in d.effects) if (!IsInstant(e)) return true;
            return false;
        }

        /// <summary>Effects applied once when taken (they'd do nothing on a remnant): coins, extra lives.</summary>
        static bool IsInstant(string e) =>
            e == ShmupEffects.LifeUp || ShmupEffects.TryParseAmount(e, ShmupEffects.Coins, out _);

        public static string DeathStat(string killer, RemnantRules rules)
        {
            if (killer == "battle") return rules.battleDeathStat;
            if (killer == "elite") return rules.eliteDeathStat;
            return rules.bossDeathStat;
        }

        static string KillerWords(string killer)
        {
            if (killer == "battle") return "a battle";
            if (killer == "elite") return "an elite";
            return killer.Replace('_', ' ');
        }

        /// <summary>A stat id in words for card text.</summary>
        public static string StatWords(string stat)
        {
            switch (stat)
            {
                case ShmupStats.Damage: return "damage";
                case ShmupStats.FireRate: return "fire rate";
                case ShmupStats.MoveSpeed: return "move speed";
                case ShmupStats.ShotSpeed: return "shot speed";
                case ShmupStats.GrazeRadius: return "graze ring";
                case ShmupStats.StaminaMax: return "stamina";
                default: return stat != null ? stat.Replace('_', ' ') : "";
            }
        }

        /// <summary>
        /// Swaps one card of a draft for a remnant (the last real card), if the book has one not already
        /// shown or owned. <paramref name="guaranteed"/> skips the chance roll. Returns the remnant shown, or null.
        /// </summary>
        public static UpgradeDef MaybeOffer(RemnantBook book, RemnantRules rules, UpgradeInventory inventory, List<UpgradeDef> cards,
            UpgradeDef fallback, Rng rng, bool guaranteed)
        {
            if (book == null || rules == null || !rules.enabled || book.Count == 0 || cards == null) return null;
            if (!guaranteed && !rng.Chance(rules.offerChance)) return null;
            for (int i = book.remnants.Count - 1; i >= 0; i--) // newest first
            {
                var rec = book.remnants[i];
                if (rec == null || string.IsNullOrEmpty(rec.id)) continue;
                if (inventory != null && inventory.Has(rec.id)) continue;
                bool shown = false;
                foreach (var c in cards) if (c != null && c.id == rec.id) shown = true;
                if (shown) continue;
                var def = rec.ToUpgrade();
                int slot = -1;
                for (int c = cards.Count - 1; c >= 0; c--)
                    if (!ReferenceEquals(cards[c], fallback)) { slot = c; break; }
                if (slot >= 0) cards[slot] = def;
                else cards.Add(def);
                return def;
            }
            return null;
        }
    }
}
