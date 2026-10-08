using System.Collections.Generic;
using JamKit.Patterns;
using JamKit.Roguelite;
using UnityEngine;

namespace JamKit.BulletHell
{
    /// <summary>
    /// Checks a BulletHellConfig and everything it points to, in plain words: empty pools, enemies
    /// without health, attacks with no bullets, bad boss thresholds, duplicate upgrade ids...
    /// The EditMode tests and "JamKit/Bullet Hell/Validate Content" run it.
    /// </summary>
    public static class BulletHellValidation
    {
        public static List<string> Validate(BulletHellConfig c)
        {
            var errors = new List<string>();
            if (c == null) { errors.Add("No BulletHellConfig."); return errors; }

            if (c.run == null) errors.Add("Config: Run is empty.");
            else
            {
                var plan = RunGenerator.Generate(c.run, new Rng(1));
                foreach (var e in plan.Validate(c.run)) errors.Add("Run rules: " + e);
            }
            int floorCount = c.run != null ? c.run.floors : 0;
            if (c.floors == null || c.floors.Count < floorCount)
                errors.Add($"Config: {floorCount} floors in Run, but {(c.floors == null ? 0 : c.floors.Count)} floor entries.");
            if (c.bulletStyles == null || c.bulletStyles.Count == 0) errors.Add("Config: no bullet styles.");
            if (c.playfieldHalfSize.x <= 1f || c.playfieldHalfSize.y <= 1f) errors.Add("Config: the playfield is too small.");
            if (c.economy == null) errors.Add("Config: Economy is empty.");
            if (c.events == null) errors.Add("Config: no ink file for Mystery rooms.");
            if (c.eventKnots == null || c.eventKnots.Count == 0) errors.Add("Config: no event knots for Mystery rooms.");

            int styles = c.bulletStyles != null ? c.bulletStyles.Count : 0;
            if (c.bulletStyles != null)
                for (int i = 0; i < c.bulletStyles.Count; i++)
                {
                    var s = c.bulletStyles[i];
                    if (s == null || s.size <= 0f || s.hitRadius <= 0f) errors.Add($"Bullet style {i}: size and hit radius must be positive.");
                }

            if (c.player == null) errors.Add("Config: no Player.");
            else ValidatePlayer(c.player, styles, errors);

            var enemies = new HashSet<EnemyDef>();
            if (c.floors != null)
            {
                for (int f = 0; f < c.floors.Count; f++)
                {
                    var floor = c.floors[f];
                    string where = $"Floor {f + 1}";
                    if (floor == null) { errors.Add(where + ": empty entry."); continue; }
                    ValidatePool(floor.combatRooms, RoomType.Combat, where + " battle rooms", styles, enemies, errors);
                    ValidatePool(floor.eliteRooms, RoomType.Elite, where + " elite rooms", styles, enemies, errors);
                    if (floor.boss == null) errors.Add(where + ": no boss.");
                    else ValidateBoss(floor.boss, styles, errors);
                    if (floor.hpScale <= 0f) errors.Add(where + ": hp scale must be positive.");
                }
            }
            foreach (var e in enemies) ValidateEnemy(e, styles, errors);

            var ids = new HashSet<string>();
            if (c.upgrades == null || c.upgrades.Count == 0) errors.Add("Config: no upgrades.");
            else
                foreach (var u in c.upgrades)
                {
                    if (u == null) { errors.Add("Upgrades: an empty entry."); continue; }
                    ValidateUpgrade(u, errors);
                    if (u.def != null && !string.IsNullOrEmpty(u.def.id) && !ids.Add(u.def.id))
                        errors.Add($"Upgrades: the id \"{u.def.id}\" is used twice.");
                }
            if (c.fallbackUpgrade != null)
            {
                ValidateUpgrade(c.fallbackUpgrade, errors);
                if (c.fallbackUpgrade.def != null && ids.Contains(c.fallbackUpgrade.def.id))
                    errors.Add("Fallback upgrade: its id is also in the pool.");
            }
            if (c.upgrades != null)
                foreach (var u in c.upgrades)
                {
                    if (u == null || u.def == null || u.def.requires == null) continue;
                    foreach (var r in u.def.requires)
                        if (!ids.Contains(r)) errors.Add($"{u.name}: it requires \"{r}\", which isn't in the pool, so it can never be offered.");
                }
            ValidateRoguelite(c, ids, styles, errors);
            return errors;
        }

        /// <summary>Round 2 (docs/DESIGN-ROGUELITE.md): reward tables, encounters, trials and wagers, remnants,
        /// the companion, room colours, and the epic rubric.</summary>
        static void ValidateRoguelite(BulletHellConfig c, HashSet<string> ids, int styles, List<string> errors)
        {
            if (c.rewards == null) errors.Add("Config: no reward tables.");
            else errors.AddRange(c.rewards.Validate());
            if (c.boosts == null) errors.Add("Config: no boost rules.");
            else
            {
                if (c.boosts.heatPerGraze < 0f || c.boosts.heatMax < 0f || c.boosts.heatDecay < 0f) errors.Add("Boosts: Ember Hunger numbers can't be negative.");
                if (c.boosts.slowScale <= 0f || c.boosts.slowScale > 1f || c.boosts.slowRadius <= 0f) errors.Add("Boosts: Time Dilation needs a radius and a speed between 0 and 1.");
            }
            if (c.forge == null) errors.Add("Config: no forge rules."); else c.forge.Validate(errors);
            if (c.altar == null) errors.Add("Config: no altar rules."); else c.altar.Validate(errors);
            if (c.trader == null) errors.Add("Config: no trader rules."); else c.trader.Validate(errors);
            if (c.remnants == null) errors.Add("Config: no remnant rules."); else c.remnants.Validate(errors);

            var run = c.run;
            if (run != null && run.trialWeight > 0f && (c.trials == null || c.trials.Count == 0)) errors.Add("Config: Trial boards can come up but there are no trials.");
            if (run != null && run.wagerWeight > 0f && (c.wagers == null || c.wagers.Count == 0)) errors.Add("Config: Wager rooms can come up but there are no wagers.");
            var trialIds = new HashSet<string>();
            if (c.trials != null)
                for (int i = 0; i < c.trials.Count; i++)
                {
                    var t = c.trials[i];
                    if (t == null) { errors.Add($"Trials: entry {i} is empty."); continue; }
                    t.Validate("Trial " + (string.IsNullOrEmpty(t.id) ? i.ToString() : t.id), errors);
                    if (!string.IsNullOrEmpty(t.id) && !trialIds.Add(t.id)) errors.Add($"Trials: the id \"{t.id}\" is used twice.");
                }
            if (c.wagers != null)
                for (int i = 0; i < c.wagers.Count; i++)
                {
                    var w = c.wagers[i];
                    if (w == null) { errors.Add($"Wagers: entry {i} is empty."); continue; }
                    w.Validate("Wager " + (string.IsNullOrEmpty(w.id) ? i.ToString() : w.id), errors);
                    if (w.condition != null && !string.IsNullOrEmpty(w.condition.id) && !trialIds.Add(w.condition.id))
                        errors.Add($"Wager {w.id}: its condition's id \"{w.condition.id}\" is also a trial's (or another wager's).");
                }

            int roomKinds = System.Enum.GetValues(typeof(RoomType)).Length;
            if (c.roomColors == null || c.roomColors.Count < roomKinds)
                errors.Add($"Config: {roomKinds} room types but {(c.roomColors == null ? 0 : c.roomColors.Count)} room colours.");

            if (c.companionEnabled)
            {
                if (c.companion == null) errors.Add("Config: the companion is on but there's no Companion asset.");
                else
                {
                    if (c.companion.sprite == null) errors.Add($"{c.companion.name}: no sprite.");
                    if (c.companion.shotStyle < 0 || c.companion.shotStyle >= styles) errors.Add($"{c.companion.name}: shot style {c.companion.shotStyle} isn't a bullet style.");
                    if (c.companion.rules == null) errors.Add($"{c.companion.name}: no rules.");
                    else c.companion.rules.Validate(errors);
                }
            }
            if (c.upgrades == null) return;
            int epics = 0;
            foreach (var u in c.upgrades)
            {
                if (u == null || u.def == null) continue;
                if (u.def.rarity >= Rarity.Epic)
                {
                    epics++;
                    foreach (var e in EpicRubric.Problems(u.def, u.rubric)) errors.Add("Epic rubric: " + e);
                }
                if (u.def.tags != null && u.def.tags.Contains(BulletHellGame.CompanionTag) && !TargetsCompanion(u.def))
                    errors.Add($"{u.name}: tagged companion, but it changes no companion stat.");
                if (u.forgeModifiers != null)
                    foreach (var m in u.forgeModifiers)
                        if (m == null || System.Array.IndexOf(ShmupStats.All, m.stat) < 0)
                            errors.Add($"{u.name}: unknown forge stat \"{(m == null ? "" : m.stat)}\".");
                if (u.forgeEffects != null)
                    foreach (var e in u.forgeEffects)
                        if (!ShmupEffects.IsKnown(e)) errors.Add($"{u.name}: unknown forge effect \"{e}\".");
            }
            if (c.rewards != null && c.rewards.boss != null && c.rewards.boss.enabled && epics < 3)
                errors.Add($"Upgrades: a boss offers 3 epics but the pool has {epics}.");
        }

        static bool TargetsCompanion(UpgradeDef d)
        {
            if (d.modifiers == null) return false;
            foreach (var m in d.modifiers)
                if (m != null && (m.stat == ShmupStats.CompanionDamage || m.stat == ShmupStats.CompanionShieldCooldown)) return true;
            return false;
        }

        static void ValidatePlayer(PlayerDef p, int styles, List<string> errors)
        {
            if (p.moveSpeed <= 0f || p.focusSpeed <= 0f) errors.Add("Player: speeds must be positive.");
            if (p.sprint == null) errors.Add("Player: no sprint settings.");
            else
            {
                if (p.sprint.speedMultiplier < 1f) errors.Add("Player: the sprint speed multiplier should be at least 1 (1.6 = 60% faster).");
                if (p.sprint.staminaSeconds <= 0f || p.sprint.refillSeconds <= 0f) errors.Add("Player: sprint stamina and refill seconds must be positive.");
                if (p.sprint.regenDelay < 0f || p.sprint.roomBudgetSeconds < 0f) errors.Add("Player: sprint delay and room budget can't be negative.");
            }
            if (p.hitboxRadius <= 0f) errors.Add("Player: the hitbox radius must be positive.");
            if (p.grazeRadius <= p.hitboxRadius) errors.Add("Player: the graze radius must be bigger than the hitbox.");
            if (p.lives < 1) errors.Add("Player: at least one life.");
            if (p.weapon == null) errors.Add("Player: no weapon.");
            else if (p.weapon.fireInterval <= 0f) errors.Add("Player: fire interval must be positive.");
            if (p.shotStyle < 0 || p.shotStyle >= styles) errors.Add($"Player: shot style {p.shotStyle} isn't a bullet style.");
            if (p.homingShotStyle < 0 || p.homingShotStyle >= styles) errors.Add($"Player: homing shot style {p.homingShotStyle} isn't a bullet style.");
            if (p.baseStats != null)
                foreach (var s in p.baseStats)
                    if (s == null || System.Array.IndexOf(ShmupStats.All, s.stat) < 0)
                        errors.Add($"Player: unknown base stat \"{(s == null ? "" : s.stat)}\".");
        }

        static void ValidatePool(List<RoomDef> pool, RoomType type, string where, int styles, HashSet<EnemyDef> enemies, List<string> errors)
        {
            if (pool == null || pool.Count == 0) { errors.Add(where + ": the pool is empty."); return; }
            foreach (var room in pool)
            {
                if (room == null) { errors.Add(where + ": an empty entry."); continue; }
                if (room.type != type) errors.Add($"{where}: {room.name} is marked {room.type}.");
                if (room.waves == null || room.waves.Count == 0) { errors.Add($"{where}: {room.name} has no waves."); continue; }
                foreach (var wave in room.waves)
                {
                    if (wave == null) { errors.Add($"{room.name}: an empty wave entry."); continue; }
                    if (wave.spawns == null || wave.spawns.Count == 0) { errors.Add($"{wave.name}: no spawns."); continue; }
                    foreach (var s in wave.spawns)
                    {
                        if (s == null || s.enemy == null) { errors.Add($"{wave.name}: a spawn with no enemy."); continue; }
                        if (s.count < 1) errors.Add($"{wave.name}: count must be at least 1.");
                        if (s.time < 0f) errors.Add($"{wave.name}: negative spawn time.");
                        enemies.Add(s.enemy);
                    }
                }
            }
        }

        static void ValidateEnemy(EnemyDef e, int styles, List<string> errors)
        {
            if (e.hp <= 0f) errors.Add($"{e.name}: hp must be positive.");
            if (e.radius <= 0f || e.size <= 0f) errors.Add($"{e.name}: size and radius must be positive.");
            if (e.sprite == null) errors.Add($"{e.name}: no sprite.");
            if (e.movement == null) errors.Add($"{e.name}: no movement.");
            ValidateSequence(e.attack, e.name, styles, errors, allowEmpty: true);
        }

        static void ValidateBoss(BossDef b, int styles, List<string> errors)
        {
            if (b.hp <= 0f) errors.Add($"{b.name}: hp must be positive.");
            if (b.sprite == null) errors.Add($"{b.name}: no sprite.");
            if (b.phases == null || b.phases.Count == 0) { errors.Add($"{b.name}: no phases."); return; }
            var thresholds = new List<float>();
            foreach (var p in b.phases)
            {
                if (p == null) { errors.Add($"{b.name}: an empty phase."); continue; }
                thresholds.Add(p.startsAtHealth);
                ValidateSequence(p.attack, b.name + " / " + p.name, styles, errors, allowEmpty: false);
            }
            foreach (var e in BossPhaseTracker.Validate(thresholds)) errors.Add($"{b.name}: {e}.");
        }

        public static void ValidateSequence(PatternSequence seq, string where, int styles, List<string> errors, bool allowEmpty)
        {
            if (seq == null || seq.steps == null || seq.steps.Count == 0)
            {
                if (!allowEmpty) errors.Add(where + ": the attack has no patterns.");
                return;
            }
            for (int i = 0; i < seq.steps.Count; i++)
            {
                var step = seq.steps[i];
                if (step == null || step.pattern == null) { errors.Add($"{where}: pattern step {i} is empty."); continue; }
                var p = step.pattern;
                if (p.count < 1) errors.Add($"{where}: pattern step {i} fires no bullets (count {p.count}).");
                if (p.speed <= 0f && p.acceleration <= 0f) errors.Add($"{where}: pattern step {i} bullets never move.");
                if (p.styleIndex < 0 || p.styleIndex >= styles) errors.Add($"{where}: pattern step {i} uses bullet style {p.styleIndex}, which doesn't exist.");
                if (p.shots <= 0 && p.shotInterval <= 0f) errors.Add($"{where}: pattern step {i} fires forever with no interval.");
            }
        }

        static void ValidateUpgrade(UpgradeAsset u, List<string> errors)
        {
            var d = u.def;
            if (d == null) { errors.Add($"{u.name}: no upgrade data."); return; }
            if (string.IsNullOrEmpty(d.id)) errors.Add($"{u.name}: no id.");
            if (string.IsNullOrEmpty(d.displayName)) errors.Add($"{u.name}: no name.");
            if (string.IsNullOrEmpty(d.description)) errors.Add($"{u.name}: no description.");
            bool hasSomething = (d.modifiers != null && d.modifiers.Count > 0) || (d.effects != null && d.effects.Count > 0);
            if (!hasSomething) errors.Add($"{u.name}: does nothing (no modifiers, no effects).");
            if (d.modifiers != null)
                foreach (var m in d.modifiers)
                    if (m == null || System.Array.IndexOf(ShmupStats.All, m.stat) < 0)
                        errors.Add($"{u.name}: unknown stat \"{(m == null ? "" : m.stat)}\".");
            if (d.effects != null)
                foreach (var e in d.effects)
                    if (!ShmupEffects.IsKnown(e)) errors.Add($"{u.name}: unknown effect \"{e}\".");
        }

        /// <summary>Logs every problem as an error. Returns true when there are none.</summary>
        public static bool LogProblems(BulletHellConfig c)
        {
            var errors = Validate(c);
            foreach (var e in errors) Debug.LogError("Bullet hell content: " + e, c);
            return errors.Count == 0;
        }
    }
}
