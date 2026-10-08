using System;
using System.Collections.Generic;
using System.Globalization;
using JamKit.Dialogue;
using JamKit.Roguelite;
using JamKit.Shell;
using UnityEngine;

namespace JamKit.BulletHell
{
    /// <summary>
    /// The encounter rooms between battles (docs/DESIGN-ROGUELITE.md, section 3): risk the player
    /// chooses, rewards for planning ahead. Each plays its ink hook line first (forge_floor_&lt;n&gt;, forge;
    /// altar...; trial_board...; none written = straight to the choice), then a ChoicePanel screen:
    /// <list type="bullet">
    /// <item>Forge: improve an upgrade; the first push is safe, each further push shows its odds of breaking it.</item>
    /// <item>Altar: one offering (an upgrade, a life or your breath) for a choice of epics, or feed one upgrade to another.</item>
    /// <item>Wager: stake a life, coins or a curse on the next fight's condition, for an epic.</item>
    /// <item>Trial board: take a condition for the next fight(s); meet it for an epic.</item>
    /// <item>Trader: two commons for a choice of rares, or sell an upgrade.</item>
    /// </list>
    /// Numbers and lists are data in BulletHellConfig (Forge, Altar, Trader, Trials, Wagers).
    /// </summary>
    public sealed partial class BulletHellGame
    {
        static readonly Color ForgeColor = new Color(1f, 0.6f, 0.25f);
        static readonly Color AltarColor = new Color(0.9f, 0.4f, 0.55f);
        static readonly Color WagerColor = new Color(0.95f, 0.78f, 0.25f);
        static readonly Color TrialColor = new Color(0.35f, 0.88f, 0.88f);
        static readonly Color TraderColor = new Color(0.6f, 0.85f, 0.45f);

        UpgradeDef _forgeTarget;
        int _forgePushes, _curseCount, _staminaOfferings;
        Action _encounterMenu;
        readonly List<StatModifier> _scratchMods = new List<StatModifier>();
        readonly List<UpgradeDef> _scratchDefs = new List<UpgradeDef>();
        readonly List<int> _scratchIndices = new List<int>();

        void StartEncounter(RoomType type)
        {
            _phase = RoomPhase.Dialogue;
            Action open;
            switch (type)
            {
                case RoomType.Forge: open = OpenForge; break;
                case RoomType.Altar: open = OpenAltar; break;
                case RoomType.Wager: open = OpenWager; break;
                case RoomType.Trial: open = OpenTrialBoard; break;
                default: open = OpenTrader; break;
            }
            // The room's line first (placeholder knots in BulletHellEvents.ink), then the choice.
            if (!PlayStoryLine(StoryHooks.Encounter(RoomText.HookKind(type), _run.floor + 1), open)) open();
        }

        void Log(string what) => _encounterLog.Add(what);

        void BeginEncounterPanel(ChoicePanel.Kind kind, string heading, string sub, Action menu, bool compact = false)
        {
            _phase = RoomPhase.Choosing;
            _afterChoice = Leave;
            _encounterMenu = menu;
            _choice.Begin(kind, heading, sub, compact);
        }

        // ---------- Pick one of your upgrades ----------

        /// <summary>A compact list of owned upgrades passing <paramref name="filter"/> (only ones with a lasting
        /// effect: "+15 coins" can't be offered or sold); picking one calls <paramref name="onPick"/>.
        /// "Back" returns to the encounter's own screen.</summary>
        void PickOwned(string heading, string sub, Func<UpgradeDef, bool> filter, Action<UpgradeDef> onPick)
        {
            var owned = _loadout.Inventory.Owned;
            _scratchDefs.Clear();
            for (int i = 0; i < owned.Count; i++)
                if (JamKit.BulletHell.Remnants.HasLastingEffect(owned[i]) && (filter == null || filter(owned[i]))) _scratchDefs.Add(owned[i]);
            var menu = _encounterMenu;
            BeginEncounterPanel(ChoicePanel.Kind.Pick, heading, sub, menu, true);
            for (int i = 0; i < _scratchDefs.Count; i++)
            {
                var d = _scratchDefs[i];
                int stacks = _loadout.Inventory.Count(d.id);
                int forged = _loadout.Inventory.BonusLevels(d.id);
                string body = (stacks > 1 ? "x" + stacks + "  " : "") + (forged > 0 ? "forged +" + forged + "\n" : "") + d.description;
                var asset = AssetFor(d.id);
                bool remnant = JamKit.BulletHell.Remnants.IsRemnant(d);
                _choice.AddCard(remnant ? "REMNANT" : d.rarity.ToString().ToUpperInvariant(), remnant ? RemnantColor : config.RarityColor(d.rarity),
                    d.displayName, body, "", true, () => onPick(d), asset != null ? asset.icon : null);
            }
            if (menu != null) _choice.AddFooter("Back", true, () => menu());
            _choice.AddFooter("Leave", true, FinishChoice);
            _choice.Show();
        }

        // ---------- Forge ----------

        bool CanForge(UpgradeDef d)
        {
            var a = AssetFor(d.id);
            if (a != null) return a.CanForge;
            return d.modifiers != null && d.modifiers.Count > 0; // a remnant: half of its stats again
        }

        /// <summary>What one push adds: the asset's forge bonus, or half of the upgrade's stat modifiers.</summary>
        void ForgeBonus(UpgradeDef d, List<StatModifier> mods, out List<string> effects)
        {
            mods.Clear();
            effects = null;
            var a = AssetFor(d.id);
            if (a != null && ((a.forgeModifiers != null && a.forgeModifiers.Count > 0) || (a.forgeEffects != null && a.forgeEffects.Count > 0)))
            {
                if (a.forgeModifiers != null) mods.AddRange(a.forgeModifiers);
                effects = a.forgeEffects;
                return;
            }
            ForgeRules.DefaultBonus(d, config.forge.defaultBonusScale, mods);
        }

        string ForgeText(UpgradeDef d)
        {
            var a = AssetFor(d.id);
            if (a != null && !string.IsNullOrEmpty(a.forgeText)) return a.forgeText;
            ForgeBonus(d, _scratchMods, out _);
            return DescribeModifiers(_scratchMods);
        }

        /// <summary>"+10% damage, x1.2 fire rate" for cards.</summary>
        static string DescribeModifiers(List<StatModifier> mods)
        {
            if (mods == null || mods.Count == 0) return "";
            var parts = new List<string>();
            foreach (var m in mods)
            {
                if (m == null) continue;
                string words = JamKit.BulletHell.Remnants.StatWords(m.stat);
                switch (m.op)
                {
                    case StatOp.PercentAdd: parts.Add((m.value >= 0f ? "+" : "") + Mathf.RoundToInt(m.value * 100f) + "% " + words); break;
                    case StatOp.Multiply: parts.Add("x" + m.value.ToString("0.##", CultureInfo.InvariantCulture) + " " + words); break;
                    default: parts.Add((m.value >= 0f ? "+" : "") + m.value.ToString("0.##", CultureInfo.InvariantCulture) + " " + words); break;
                }
            }
            return string.Join(", ", parts);
        }

        void OpenForge()
        {
            _forgePushes = 0;
            _forgeTarget = null;
            var owned = _loadout.Inventory.Owned;
            int forgeable = 0;
            for (int i = 0; i < owned.Count; i++) if (CanForge(owned[i])) forgeable++;
            if (forgeable == 0)
            {
                BeginEncounterPanel(ChoicePanel.Kind.Forge, "Forge", "Nothing here can be forged yet.", null);
                _choice.AddCard("SCRAP", ForgeColor, "Sell scrap", "Take " + config.forge.scrapCoins + " coins for the scraps on the anvil.", "", true, () =>
                {
                    AddCoins(config.forge.scrapCoins, true);
                    Log("forge: scrap");
                    FinishChoice();
                });
                _choice.AddFooter("Leave", true, FinishChoice);
                _choice.Show();
                return;
            }
            _encounterMenu = OpenForge;
            PickOwned("Forge", "Choose an upgrade to improve. The first push is safe; every push after it risks breaking the upgrade.",
                CanForge, ForgePush);
            _choice.SetNote("First push: safe. Then " + ForgeRules.Percent(config.forge.BreakChance(1)) + ", " + ForgeRules.Percent(config.forge.BreakChance(2)) + "...");
        }

        void ForgePush(UpgradeDef d)
        {
            if (d == null || !_loadout.Inventory.Has(d.id)) { FinishChoice(); return; }
            _forgeTarget = d;
            bool held = config.forge.Push(_forgePushes, _roomRng);
            _forgePushes++;
            Vector2 pp = player.Position + Vector2.up * 0.8f;
            if (!held)
            {
                _loadout.Inventory.Remove(d.id);
                InventoryChanged();
                Log("forge: " + d.id + " broke on push " + _forgePushes);
                GameRoot.Audio.Play("hurt");
                GameRoot.Juice.Shake(0.5f);
                GameRoot.Juice.FloatingText(pp, d.displayName + " broke!", new Color(1f, 0.4f, 0.35f));
                BeginEncounterPanel(ChoicePanel.Kind.Forge, "It broke", d.displayName + " is gone, with everything forged into it.", null);
                _choice.AddCard("FORGE", ForgeColor, "Walk away", "The anvil is cold.", "", true, FinishChoice);
                _choice.Show();
                return;
            }
            ForgeBonus(d, _scratchMods, out var effects);
            int level = _loadout.Inventory.AddBonus(d.id, _scratchMods, effects);
            InventoryChanged();
            Log("forge: " + d.id + " +" + level);
            GameRoot.Audio.Play("powerup");
            GameRoot.Juice.FloatingText(pp, d.displayName + " +" + level, ForgeColor);
            ShowForgeAgain();
        }

        void ShowForgeAgain()
        {
            var d = _forgeTarget;
            int level = _loadout.Inventory.BonusLevels(d.id);
            BeginEncounterPanel(ChoicePanel.Kind.Forge, "Forge: " + d.displayName + " +" + level, "Push your luck, or stop while it holds.", null);
            float chance = config.forge.BreakChance(_forgePushes);
            bool more = config.forge.CanPush(_forgePushes);
            _choice.SetNote(more
                ? "Next push: " + ForgeRules.Percent(chance) + " chance it breaks (you'd lose " + d.displayName + " and everything forged into it)."
                : "The anvil can't take another push this visit.");
            if (more)
                _choice.AddCard("PUSH", ForgeColor, "Push again", ForgeText(d), ForgeRules.Percent(chance) + " risk", true, () => ForgePush(d));
            _choice.AddCard("STOP", new Color(0.6f, 0.8f, 1f), "Stop here", "Keep " + d.displayName + " +" + level + ".", "", true, FinishChoice);
            _choice.Show();
        }

        // ---------- Altar ----------

        void OpenAltar()
        {
            var rules = config.altar;
            BeginEncounterPanel(ChoicePanel.Kind.Altar, "Altar", "One offering. Give something up for a choice of epics, or feed one upgrade to another.", OpenAltar);
            int owned = LastingOwned();
            int forgeable = 0;
            foreach (var d in _loadout.Inventory.Owned) if (CanForge(d)) forgeable++;
            var h = player.Health;
            if (rules.allowUpgradeOffering)
                _choice.AddCard("OFFER", AltarColor, "Offer an upgrade", "Give up one of your upgrades (every stack of it). Then choose an epic.", "", owned > 0, () =>
                    PickOwned("Offer which upgrade?", "It's gone for the rest of the run.", null, d =>
                    {
                        _loadout.Inventory.Remove(d.id);
                        InventoryChanged();
                        Log("altar: offered " + d.id);
                        OpenDraft("The altar accepts", "Choose an epic.", RewardSource.Sacrifice, Leave);
                    }));
            if (rules.allowLifeOffering)
                _choice.AddCard("OFFER", AltarColor, "Offer a life", "Lose " + rules.lifeCost + " life now (you keep at least one). Then choose an epic.", "",
                    h.Current - rules.lifeCost >= 0.5f, () =>
                    {
                        if (!player.PayLives(rules.lifeCost)) return;
                        Log("altar: offered a life");
                        GameRoot.Audio.Play("hurt", 0.6f);
                        OpenDraft("The altar accepts", "Choose an epic.", RewardSource.Sacrifice, Leave);
                    });
            if (rules.allowStaminaOffering)
                _choice.AddCard("OFFER", AltarColor, "Offer your breath", "Max stamina -" + Mathf.RoundToInt((1f - rules.staminaKept) * 100f) + "% for the rest of the run. Then choose an epic.", "",
                    true, () =>
                    {
                        _staminaOfferings++;
                        _loadout.Stats.Add(new StatModifier(ShmupStats.StaminaMax, StatOp.Multiply, rules.staminaKept, "altar:stamina#" + _staminaOfferings));
                        Log("altar: offered stamina");
                        OpenDraft("The altar accepts", "Choose an epic.", RewardSource.Sacrifice, Leave);
                    });
            if (rules.allowFeeding)
                _choice.AddCard("FEED", AltarColor, "Feed one to another", "Give up one upgrade; another gains " + rules.empowerLevels + " forge levels, with no risk.", "",
                    owned >= 2 && forgeable >= 1, () =>
                        PickOwned("Feed which upgrade?", "It's consumed.", null, food =>
                            PickOwned("Empower which?", "It gains " + rules.empowerLevels + " forge levels.", d => d.id != food.id && CanForge(d), target =>
                            {
                                _loadout.Inventory.Remove(food.id);
                                for (int i = 0; i < rules.empowerLevels; i++)
                                {
                                    ForgeBonus(target, _scratchMods, out var fx);
                                    _loadout.Inventory.AddBonus(target.id, _scratchMods, fx);
                                }
                                InventoryChanged();
                                Log("altar: fed " + food.id + " to " + target.id);
                                GameRoot.Audio.Play("powerup");
                                GameRoot.Juice.FloatingText(player.Position + Vector2.up * 0.8f, target.displayName + " +" + _loadout.Inventory.BonusLevels(target.id), AltarColor);
                                FinishChoice();
                            })));
            _choice.AddFooter("Leave", true, FinishChoice);
            _choice.Show();
        }

        // ---------- Wager ----------

        void OpenWager()
        {
            BeginEncounterPanel(ChoicePanel.Kind.Wager, "Wager", "Stake something now on the next fight. Win it and choose an epic.", OpenWager);
            PickSome(config.wagers != null ? config.wagers.Count : 0, 2, i => config.wagers[i] != null && config.wagers[i].condition != null && !HasTrial(config.wagers[i].condition.id));
            var h = player.Health;
            foreach (int i in _scratchIndices)
            {
                var w = config.wagers[i];
                bool can = w.stake == WagerStake.Life ? h.Current - w.amount >= 0.5f : w.stake == WagerStake.Coins ? _run.currency >= w.amount : true;
                string stake = w.stake == WagerStake.Life ? "-" + w.amount + " life now" : w.stake == WagerStake.Coins ? "-" + w.amount + " coins now" : "a curse until it ends";
                _choice.AddCard("WAGER", WagerColor, w.displayName, w.description, stake, can, () => TakeWager(w));
            }
            _choice.AddFooter("Walk away", true, FinishChoice);
            _choice.Show();
        }

        void TakeWager(WagerDef w)
        {
            switch (w.stake)
            {
                case WagerStake.Life:
                    if (!player.PayLives(w.amount)) return;
                    GameRoot.Audio.Play("hurt", 0.6f);
                    break;
                case WagerStake.Coins:
                    if (!_run.TrySpend(w.amount)) return;
                    break;
            }
            // The HUD shows the condition's name: give each wager's condition its own ("Blood Price: no hits").
            var t = new TrialTracker(w.condition ?? new TrialDef()) { reward = w.reward };
            if (w.stake == WagerStake.Curse && w.curse != null)
            {
                _curseCount++;
                t.curseSource = "curse:" + w.id + "#" + _curseCount;
                _loadout.Stats.Add(w.curse.WithSource(t.curseSource));
            }
            _trials.Add(t);
            _hud.SetTrials(_trials);
            Log("wager: " + w.id);
            GameRoot.Hud.Message("Wager: " + w.displayName, 2f);
            FinishChoice();
        }

        // ---------- Trial board ----------

        void OpenTrialBoard()
        {
            BeginEncounterPanel(ChoicePanel.Kind.Trial, "Trial board", "Take a challenge for the next fight. Meet it and choose an epic. Fail, and nothing is lost.", OpenTrialBoard);
            PickSome(config.trials != null ? config.trials.Count : 0, 3, i => config.trials[i] != null && config.trials[i].Available(_loadout.Inventory) && !HasTrial(config.trials[i].id));
            foreach (int i in _scratchIndices)
            {
                var def = config.trials[i];
                string window = def.rooms > 1 ? "next " + def.rooms + " fights" : "next fight";
                _choice.AddCard("TRIAL", TrialColor, def.displayName, def.description, window, true, () => TakeTrial(def));
            }
            _choice.AddFooter("No thanks", true, FinishChoice);
            _choice.Show();
        }

        void TakeTrial(TrialDef def)
        {
            _trials.Add(new TrialTracker(def) { reward = RewardSource.Trial });
            _hud.SetTrials(_trials);
            Log("trial: " + def.id);
            GameRoot.Hud.Message("Trial: " + def.displayName, 2f);
            FinishChoice();
        }

        /// <summary>Debug and tests: start a trial now, as if taken at a board (or a wager's condition, with
        /// <paramref name="reward"/> = Wager). Returns its tracker.</summary>
        public TrialTracker DebugAddTrial(TrialDef def, RewardSource reward = RewardSource.Trial)
        {
            var t = new TrialTracker(def) { reward = reward };
            _trials.Add(t);
            _hud.SetTrials(_trials);
            return t;
        }

        /// <summary>Owned upgrades the encounters can take (a lasting effect).</summary>
        int LastingOwned()
        {
            int n = 0;
            var owned = _loadout.Inventory.Owned;
            for (int i = 0; i < owned.Count; i++) if (JamKit.BulletHell.Remnants.HasLastingEffect(owned[i])) n++;
            return n;
        }

        bool HasTrial(string id)
        {
            for (int i = 0; i < _trials.Count; i++) if (_trials[i].Def.id == id) return true;
            return false;
        }

        /// <summary>Up to <paramref name="max"/> distinct indices of 0..count-1 passing <paramref name="ok"/>,
        /// in a seeded order (the room's rng), into _scratchIndices.</summary>
        void PickSome(int count, int max, Func<int, bool> ok)
        {
            _scratchIndices.Clear();
            for (int i = 0; i < count; i++) if (ok(i)) _scratchIndices.Add(i);
            _roomRng.Shuffle(_scratchIndices);
            while (_scratchIndices.Count > max) _scratchIndices.RemoveAt(_scratchIndices.Count - 1);
        }

        // ---------- Trader ----------

        void OpenTrader()
        {
            var rules = config.trader;
            BeginEncounterPanel(ChoicePanel.Kind.Trader, "Trader", "A fair swap, they say.", OpenTrader);
            int commons = TraderRules.CommonStacks(_loadout.Inventory);
            _choice.AddCard("TRADE", TraderColor, rules.commonsForRare + " commons for a rare", "Give " + rules.commonsForRare + " common upgrades (one stack each). Then choose a rare.",
                "you have " + commons, commons >= rules.commonsForRare, () =>
                {
                    _tradePicks.Clear();
                    TradeStep(rules.commonsForRare);
                });
            _choice.AddCard("SELL", TraderColor, "Sell an upgrade", "One stack, for " + Mathf.RoundToInt(rules.sellFraction * 100f) + "% of its shop price.", "",
                LastingOwned() > 0, () =>
                    PickOwned("Sell which?", "One stack goes.", null, d =>
                    {
                        int coins = Mathf.RoundToInt(config.economy.PriceOf(d.rarity) * rules.sellFraction);
                        _loadout.Inventory.Remove(d.id, 1);
                        InventoryChanged();
                        AddCoins(coins, true);
                        Log("trader: sold " + d.id);
                        FinishChoice();
                    }));
            _choice.AddFooter("Leave", true, FinishChoice);
            _choice.Show();
        }

        readonly List<string> _tradePicks = new List<string>();

        /// <summary>Picks the commons one at a time (a stack can be picked as often as it has stacks); nothing is
        /// taken until the last pick, so backing out costs nothing. Then the choice of rares.</summary>
        void TradeStep(int total)
        {
            if (_tradePicks.Count >= total)
            {
                foreach (var id in _tradePicks) _loadout.Inventory.Remove(id, 1);
                InventoryChanged();
                Log("trader: " + string.Join(" + ", _tradePicks) + " for a rare");
                _tradePicks.Clear();
                OpenDraft("The trader's rares", "Choose one.", RewardSource.Trader, Leave);
                return;
            }
            PickOwned("Give which common? (" + (_tradePicks.Count + 1) + " of " + total + ")", "One stack of each pick goes.",
                d => d.rarity == Rarity.Common && _loadout.Inventory.Count(d.id) > Picked(d.id), d =>
                {
                    _tradePicks.Add(d.id);
                    TradeStep(total);
                });
        }

        int Picked(string id)
        {
            int n = 0;
            for (int i = 0; i < _tradePicks.Count; i++) if (_tradePicks[i] == id) n++;
            return n;
        }
    }
}
