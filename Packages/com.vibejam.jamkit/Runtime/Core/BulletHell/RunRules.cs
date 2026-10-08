using System;
using System.Globalization;
using JamKit.Roguelite;

namespace JamKit.BulletHell
{
    /// <summary>Prices and costs, as data (BulletHellConfig holds one).</summary>
    [Serializable]
    public class ShmupEconomy
    {
        /// <summary>Shop price per rarity: Common, Rare, Epic, Legendary.</summary>
        public int commonPrice = 25;
        public int rarePrice = 40;
        public int epicPrice = 60;
        public int legendaryPrice = 90;
        public int lifePrice = 45;
        public int bombPrice = 20;
        /// <summary>First reroll of a draft costs this; each further reroll in the run costs rerollStep more.</summary>
        public int rerollBaseCost = 10;
        public int rerollStep = 5;
        /// <summary>Upgrades on sale in a shop.</summary>
        public int shopUpgrades = 3;
        /// <summary>Cards in a draft.</summary>
        public int draftSize = 3;
        /// <summary>Grazes per graze-heal tick (divided by the upgrade's stacks).</summary>
        public int grazeHealEvery = 120;
        /// <summary>Luck bonus for drafts after elites and bosses.</summary>
        public float eliteLuck = 0.5f;
        public float bossLuck = 1f;

        public int PriceOf(Rarity r)
        {
            switch (r)
            {
                case Rarity.Rare: return rarePrice;
                case Rarity.Epic: return epicPrice;
                case Rarity.Legendary: return legendaryPrice;
                default: return commonPrice;
            }
        }

        public int RerollCost(int rerollsSoFar) => Math.Max(0, rerollBaseCost + rerollStep * Math.Max(0, rerollsSoFar));
    }

    /// <summary>Player-facing words and letters for room types (doors, the floor map).</summary>
    public static class RoomText
    {
        public static string Name(RoomType t)
        {
            switch (t)
            {
                case RoomType.Combat: return "Battle";
                case RoomType.Elite: return "Elite";
                case RoomType.Treasure: return "Treasure";
                case RoomType.Shop: return "Shop";
                case RoomType.Rest: return "Rest";
                case RoomType.Event: return "Mystery";
                case RoomType.Boss: return "Boss";
                default: return t.ToString();
            }
        }

        /// <summary>One letter for the HUD map.</summary>
        public static string Letter(RoomType t)
        {
            switch (t)
            {
                case RoomType.Combat: return "B";
                case RoomType.Elite: return "E";
                case RoomType.Treasure: return "T";
                case RoomType.Shop: return "$";
                case RoomType.Rest: return "R";
                case RoomType.Event: return "?";
                case RoomType.Boss: return "!";
                default: return "-";
            }
        }

        /// <summary>The line under a door.</summary>
        public static string Describe(RoomType t)
        {
            switch (t)
            {
                case RoomType.Combat: return "Waves of enemies. Coins.";
                case RoomType.Elite: return "A tough fight. Pick an upgrade after.";
                case RoomType.Treasure: return "A free upgrade.";
                case RoomType.Shop: return "Spend coins on upgrades, lives and bombs.";
                case RoomType.Rest: return "Heal a life, or train for an upgrade.";
                case RoomType.Event: return "Something unexpected.";
                case RoomType.Boss: return "The floor's guardian.";
                default: return "";
            }
        }
    }

    public enum RoomEventKind { None, Coins, Lives, Bombs, Score, Draft, PayForDraft }

    /// <summary>
    /// A gameplay effect from an event dialogue, written as an ink tag:
    /// <c>#event:bh:coins:40</c>, <c>bh:coins:-15</c>, <c>bh:life:1</c>, <c>bh:bomb:1</c>,
    /// <c>bh:score:500</c>, <c>bh:draft</c> (or <c>bh:draft:rare</c> for Rare and up),
    /// <c>bh:pay:20</c> (pay 20 coins, if you have them, for a draft).
    /// </summary>
    public struct RoomEventEffect
    {
        public RoomEventKind kind;
        public int amount;
        /// <summary>Draft: the lowest rarity offered (Common = anything).</summary>
        public Rarity minRarity;

        public const string Prefix = "bh:";

        public static bool TryParse(string value, out RoomEventEffect effect)
        {
            effect = default;
            if (string.IsNullOrEmpty(value)) return false;
            string s = value.Trim();
            if (!s.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)) return false;
            string[] parts = s.Substring(Prefix.Length).Split(':');
            string what = parts[0].Trim().ToLowerInvariant();
            string arg = parts.Length > 1 ? parts[1].Trim() : "";
            int n;
            switch (what)
            {
                case "coins":
                case "coin":
                    if (!ParseInt(arg, out n)) return false;
                    effect = new RoomEventEffect { kind = RoomEventKind.Coins, amount = n };
                    return true;
                case "life":
                case "lives":
                    if (!ParseInt(arg.Length == 0 ? "1" : arg, out n)) return false;
                    effect = new RoomEventEffect { kind = RoomEventKind.Lives, amount = n };
                    return true;
                case "bomb":
                case "bombs":
                    if (!ParseInt(arg.Length == 0 ? "1" : arg, out n)) return false;
                    effect = new RoomEventEffect { kind = RoomEventKind.Bombs, amount = n };
                    return true;
                case "score":
                    if (!ParseInt(arg, out n)) return false;
                    effect = new RoomEventEffect { kind = RoomEventKind.Score, amount = n };
                    return true;
                case "draft":
                {
                    var r = Rarity.Common;
                    if (arg.Length > 0 && !TryRarity(arg, out r)) return false;
                    effect = new RoomEventEffect { kind = RoomEventKind.Draft, amount = 1, minRarity = r };
                    return true;
                }
                case "pay":
                    if (!ParseInt(arg, out n) || n < 0) return false;
                    effect = new RoomEventEffect { kind = RoomEventKind.PayForDraft, amount = n };
                    return true;
                default:
                    return false;
            }
        }

        static bool ParseInt(string s, out int n) =>
            int.TryParse(s.StartsWith("+", StringComparison.Ordinal) ? s.Substring(1) : s, NumberStyles.Integer, CultureInfo.InvariantCulture, out n);

        static bool TryRarity(string s, out Rarity r)
        {
            switch (s.ToLowerInvariant())
            {
                case "common": r = Rarity.Common; return true;
                case "rare": r = Rarity.Rare; return true;
                case "epic": r = Rarity.Epic; return true;
                case "legendary": r = Rarity.Legendary; return true;
                default: r = Rarity.Common; return false;
            }
        }
    }
}
