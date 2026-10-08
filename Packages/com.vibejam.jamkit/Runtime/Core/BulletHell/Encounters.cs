using System;
using System.Collections.Generic;
using JamKit.Roguelite;

namespace JamKit.BulletHell
{
    /// <summary>
    /// The Forge (Inscryption's campfire): improve an owned upgrade, again and again; the first push is
    /// safe, each further push on the same visit has a growing chance to break the upgrade (it's lost,
    /// with everything forged into it). The card shows the odds before the player pushes.
    /// </summary>
    [Serializable]
    public class ForgeRules
    {
        /// <summary>Break chance of the first push of a visit (0 = always safe).</summary>
        public float firstPushBreakChance;
        /// <summary>Added to the break chance by each push already made this visit.</summary>
        public float breakChancePerPush = 0.25f;
        public float maxBreakChance = 0.85f;
        /// <summary>Most pushes on one visit.</summary>
        public int maxPushes = 4;
        /// <summary>When an upgrade has no forge bonus of its own: one push adds this fraction of its stat
        /// modifiers again (0.5 = half a stack).</summary>
        public float defaultBonusScale = 0.5f;
        /// <summary>Coins for walking away with nothing forgeable.</summary>
        public int scrapCoins = 15;

        /// <summary>The chance that push number <paramref name="pushesSoFar"/> + 1 breaks the upgrade.</summary>
        public float BreakChance(int pushesSoFar)
        {
            if (pushesSoFar <= 0) return Clamp01(firstPushBreakChance);
            return Math.Min(Clamp01(maxBreakChance), Clamp01(firstPushBreakChance + breakChancePerPush * pushesSoFar));
        }

        /// <summary>Rolls one push: true = it held (apply the bonus), false = it broke.</summary>
        public bool Push(int pushesSoFar, Rng rng)
        {
            float chance = BreakChance(pushesSoFar);
            return !(chance > 0f && rng.NextFloat() < chance);
        }

        public bool CanPush(int pushesSoFar) => pushesSoFar < Math.Max(1, maxPushes);

        /// <summary>"25%" for a card.</summary>
        public static string Percent(float chance) => Math.Round(Clamp01(chance) * 100f) + "%";

        public void Validate(List<string> errors)
        {
            if (firstPushBreakChance < 0f || firstPushBreakChance > 1f || breakChancePerPush < 0f || breakChancePerPush > 1f)
                errors.Add("Forge: break chances must be between 0 and 1.");
            if (maxBreakChance <= 0f || maxBreakChance > 1f) errors.Add("Forge: the max break chance must be above 0 and at most 1.");
            if (maxPushes < 1) errors.Add("Forge: at least one push per visit.");
            if (defaultBonusScale <= 0f) errors.Add("Forge: the default bonus scale must be positive.");
        }

        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

        /// <summary>The modifiers one push adds when an upgrade has no authored bonus: each of its modifiers
        /// scaled by <paramref name="scale"/> (<see cref="Scale"/>). Empty for effect-only upgrades.</summary>
        public static void DefaultBonus(UpgradeDef def, float scale, List<StatModifier> into)
        {
            into.Clear();
            if (def == null || def.modifiers == null) return;
            for (int i = 0; i < def.modifiers.Count; i++)
                if (def.modifiers[i] != null) into.Add(Scale(def.modifiers[i], scale));
        }

        /// <summary>A modifier at a fraction of its strength: Flat and PercentAdd values times
        /// <paramref name="scale"/>; a Multiply's distance from 1 times it (x1.4 at half = x1.2).</summary>
        public static StatModifier Scale(StatModifier m, float scale)
        {
            float v = m.op == StatOp.Multiply ? 1f + (m.value - 1f) * scale : m.value * scale;
            return new StatModifier(m.stat, m.op, v);
        }
    }

    /// <summary>
    /// The Altar: one offering per visit. Give up an upgrade, a life or your breath (max stamina) for a
    /// choice of epics, or feed one upgrade to another (it gains forge levels without the risk).
    /// </summary>
    [Serializable]
    public class AltarRules
    {
        /// <summary>Lives offered for an epic (the player must keep at least one).</summary>
        public int lifeCost = 1;
        /// <summary>Max stamina kept after offering your breath (0.7 = 30% less), for the rest of the run.</summary>
        public float staminaKept = 0.7f;
        /// <summary>Forge levels the fed upgrade gains.</summary>
        public int empowerLevels = 2;
        public bool allowUpgradeOffering = true;
        public bool allowLifeOffering = true;
        public bool allowStaminaOffering = true;
        public bool allowFeeding = true;

        public void Validate(List<string> errors)
        {
            if (lifeCost < 1) errors.Add("Altar: a life offering costs at least 1 life.");
            if (staminaKept <= 0.2f || staminaKept >= 1f) errors.Add("Altar: stamina kept must be between 0.2 and 1.");
            if (empowerLevels < 1) errors.Add("Altar: feeding adds at least one level.");
            if (!allowUpgradeOffering && !allowLifeOffering && !allowStaminaOffering && !allowFeeding)
                errors.Add("Altar: every offering is off.");
        }
    }

    /// <summary>The Trader: swap two commons (stacks) for a choice of rares, or sell an upgrade.</summary>
    [Serializable]
    public class TraderRules
    {
        public int commonsForRare = 2;
        /// <summary>Selling pays this fraction of the shop price.</summary>
        public float sellFraction = 0.5f;

        public void Validate(List<string> errors)
        {
            if (commonsForRare < 1) errors.Add("Trader: trade at least one common.");
            if (sellFraction < 0f || sellFraction > 1f) errors.Add("Trader: the sell fraction must be between 0 and 1.");
        }

        /// <summary>Stacks of owned Common upgrades with a lasting effect (what the trade can take).</summary>
        public static int CommonStacks(UpgradeInventory inventory, string skipTag = null)
        {
            if (inventory == null) return 0;
            int n = 0;
            var owned = inventory.Owned;
            for (int i = 0; i < owned.Count; i++)
            {
                var d = owned[i];
                if (d.rarity != Rarity.Common || !Remnants.HasLastingEffect(d)) continue;
                if (skipTag != null && d.tags != null && d.tags.Contains(skipTag)) continue;
                n += inventory.Count(d.id);
            }
            return n;
        }
    }

    /// <summary>
    /// The four-part epic rubric (docs/DESIGN-ROGUELITE.md, section 2) as data on each epic, so a
    /// validator and the EditMode tests can hold every epic to it.
    /// </summary>
    [Serializable]
    public class EpicRubric
    {
        /// <summary>1. The verb it changes: how you move, aim, dodge or take risks (not just a number).</summary>
        public UpgradeVerb verb;
        /// <summary>2. At least two situations it's useful in, from <see cref="Situations"/>.</summary>
        public List<string> situations = new List<string>();
        /// <summary>3. Where the player is when it works (it must match where players actually are).</summary>
        public string wherePlayerIs = "";
        /// <summary>4. The in-universe idea behind its name (placeholder until the theme is known).</summary>
        public string idea = "";

        /// <summary>The situation words the rubric understands.</summary>
        public static readonly string[] Situations = { "boss", "waves", "open", "crowded", "divers", "sides", "turrets", "low_life", "long_fights" };

        /// <summary>Everything an epic's rubric is missing, in plain words. Empty = it passes.</summary>
        public static List<string> Problems(UpgradeDef def, EpicRubric r)
        {
            var errors = new List<string>();
            string name = def != null ? def.id : "?";
            if (r == null) { errors.Add(name + ": no epic rubric."); return errors; }
            if (r.verb == UpgradeVerb.None) errors.Add(name + ": the rubric names no verb it changes (move, aim, dodge, risk).");
            int known = 0;
            var seen = new List<string>();
            if (r.situations != null)
                foreach (var s in r.situations)
                {
                    if (Array.IndexOf(Situations, s) < 0) { errors.Add(name + ": unknown situation \"" + s + "\"."); continue; }
                    if (seen.Contains(s)) continue;
                    seen.Add(s);
                    known++;
                }
            if (known < 2) errors.Add(name + ": useful in fewer than two situations.");
            if (string.IsNullOrEmpty(r.wherePlayerIs)) errors.Add(name + ": the rubric doesn't say where the player is when it works.");
            if (string.IsNullOrEmpty(r.idea)) errors.Add(name + ": no in-universe idea for its name.");
            return errors;
        }
    }

    public enum UpgradeVerb { None, Move, Aim, Dodge, Risk }
}
