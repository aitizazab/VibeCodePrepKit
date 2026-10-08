using System.Collections.Generic;
using JamKit.Patterns;
using UnityEngine;

namespace JamKit.Doomlike
{
    /// <summary>
    /// Checks a DoomlikeConfig and everything it points to, in plain words: every level parses and
    /// can be finished (start, exit, keys before their doors), map characters don't clash, weapons
    /// have slots, damage and known ammo, enemies have health, a sprite sheet and a working attack,
    /// upgrades use known stats and effects, the builder's materials are there. The EditMode tests
    /// and "JamKit/DOOM-like/Validate Content" run it.
    /// </summary>
    public static class DoomlikeValidation
    {
        public sealed class Report
        {
            public readonly List<string> errors = new List<string>();
            public readonly List<string> warnings = new List<string>();
            public bool Ok => errors.Count == 0;
        }

        public static List<string> Validate(DoomlikeConfig c) => Check(c).errors;

        public static Report Check(DoomlikeConfig c)
        {
            var r = new Report();
            var e = r.errors;
            if (c == null) { e.Add("No DoomlikeConfig."); return r; }

            if (c.cellSize < 1.5f) e.Add("Config: Cell Size should be at least 1.5 metres (the player is 0.8 wide).");
            if (c.defaultCeiling < 2.5f) e.Add("Config: Default Ceiling should be at least 2.5 metres.");
            if (c.LevelCount == 0) e.Add("Config: no levels. Add map files to Levels.");

            var legendErrors = new List<string>();
            c.BuildLegend(legendErrors);
            e.AddRange(legendErrors);

            CheckAmmo(c, r);
            CheckWeapons(c, r);
            CheckEnemies(c, r);
            CheckThings(c, r);
            CheckUpgrades(c, r);
            CheckArt(c, r);
            if (c.themes == null || c.themes.Count == 0) e.Add("Config: no themes (level colours).");

            for (int i = 0; i < c.LevelCount; i++) CheckLevel(c, i, r);
            return r;
        }

        /// <summary>One level: parses with the config's legend and is finishable.</summary>
        public static void CheckLevel(DoomlikeConfig c, int index, Report r)
        {
            var asset = c.levels[index];
            string where = "Level " + (index + 1) + (asset != null ? " (" + asset.name + ")" : "");
            if (asset == null) { r.errors.Add(where + ": the map file is missing."); return; }
            var problems = new MapProblems();
            var map = MapParser.Parse(asset.text, c.BuildLegend(null), problems);
            foreach (var p in problems.errors) r.errors.Add(where + ": " + p);
            foreach (var p in problems.warnings) r.warnings.Add(where + ": " + p);
            if (!problems.Ok) return;
            var report = MapValidator.Validate(map, c.ambushRadius);
            foreach (var p in report.problems.errors) r.errors.Add(where + ": " + p);
            foreach (var p in report.problems.warnings) r.warnings.Add(where + ": " + p);
            if (map.ceiling > 0f && map.ceiling < 2.5f) r.errors.Add(where + ": ceiling " + map.ceiling + " is too low (2.5 metres at least, so enemies fit).");
            if (!string.IsNullOrEmpty(map.theme) && c.Theme(map.theme) == c.themes[0] && !string.Equals(c.themes[0].name, map.theme, System.StringComparison.OrdinalIgnoreCase)
                && !int.TryParse(map.theme, out _))
                r.warnings.Add(where + ": theme \"" + map.theme + "\" isn't in the config's Themes; the first one is used.");
        }

        static void CheckAmmo(DoomlikeConfig c, Report r)
        {
            var ids = new HashSet<string>();
            if (c.ammoTypes == null) return;
            foreach (var a in c.ammoTypes)
            {
                if (a == null || string.IsNullOrEmpty(a.id)) { r.errors.Add("Ammo Types: an entry has no id."); continue; }
                if (!ids.Add(a.id.ToLowerInvariant())) r.errors.Add($"Ammo Types: id \"{a.id}\" is used twice.");
                if (a.max <= 0) r.errors.Add($"Ammo \"{a.id}\": Max must be above 0.");
                if (a.start > a.max) r.warnings.Add($"Ammo \"{a.id}\": Start is above Max (it's capped).");
            }
        }

        static bool AmmoKnown(DoomlikeConfig c, string id)
        {
            if (string.IsNullOrEmpty(id) || c.ammoTypes == null) return false;
            foreach (var a in c.ammoTypes) if (a != null && string.Equals(a.id, id, System.StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        static void CheckWeapons(DoomlikeConfig c, Report r)
        {
            var e = r.errors;
            if (c.weapons == null || c.weapons.Count == 0) { e.Add("Config: no weapons."); return; }
            var slots = new Dictionary<int, string>();
            foreach (var w in c.weapons)
            {
                if (w == null) { e.Add("Weapons: an empty entry."); continue; }
                string n = "Weapon " + w.name;
                if (w.slot < 1 || w.slot > 9) e.Add($"{n}: Slot must be 1 to 9.");
                else if (slots.TryGetValue(w.slot, out var other)) e.Add($"{n}: slot {w.slot} is taken by {other}.");
                else slots[w.slot] = w.name;
                if (w.damage <= 0f) e.Add($"{n}: Damage must be above 0.");
                if (w.pellets < 1) e.Add($"{n}: Pellets must be at least 1.");
                if (w.range <= 0f) e.Add($"{n}: Range must be above 0.");
                if (w.def == null || w.def.fireInterval <= 0f) e.Add($"{n}: Fire Interval must be above 0.");
                if (w.def != null && w.def.magazineSize != 0) r.warnings.Add($"{n}: Magazine Size is ignored (ammo comes from the shared pool).");
                if (w.UsesAmmo && !AmmoKnown(c, w.ammoType)) e.Add($"{n}: ammo \"{w.ammoType}\" isn't in the config's Ammo Types.");
                if (w.kind == WeaponKind.Projectile && w.projectileSpeed <= 0f) e.Add($"{n}: Projectile Speed must be above 0.");
                if (w.viewTexture == null) r.warnings.Add($"{n}: no View Texture (nothing shows in your hands).");
                if (!string.IsNullOrEmpty(w.mapChar) && w.pickupTexture == null) r.warnings.Add($"{n}: it can lie on maps but has no Pickup Texture.");
            }
            if (c.startingWeapons == null || c.startingWeapons.Count == 0) e.Add("Config: no Starting Weapons.");
            else foreach (var w in c.startingWeapons)
                    if (w == null || !c.weapons.Contains(w)) e.Add("Config: a starting weapon isn't in the Weapons list.");
        }

        static void CheckEnemies(DoomlikeConfig c, Report r)
        {
            var e = r.errors;
            if (c.enemies == null || c.enemies.Count == 0) { e.Add("Config: no enemies."); return; }
            foreach (var en in c.enemies)
            {
                if (en == null) { e.Add("Enemies: an empty entry."); continue; }
                string n = "Enemy " + en.name;
                if (en.health <= 0f) e.Add($"{n}: Health must be above 0.");
                if (en.speed < 0f) e.Add($"{n}: Speed can't be negative.");
                if (en.radius <= 0f || en.height <= 0f) e.Add($"{n}: Radius and Height must be above 0.");
                if (en.sprite == null || en.sprite.texture == null) e.Add($"{n}: no sprite sheet texture.");
                else if (en.sprite.directions != 1 && en.sprite.directions != 5 && en.sprite.directions != 8)
                    e.Add($"{n}: sprite Directions must be 1, 5 or 8.");
                if (en.brain == null) e.Add($"{n}: no Brain.");
                else
                {
                    if (en.brain.attackRange <= 0f) e.Add($"{n}: Attack Range must be above 0.");
                    if (en.brain.sightRange <= 0f) e.Add($"{n}: Sight Range must be above 0.");
                }
                CheckAttack(n + " attack", en.attack, en, r);
                if (en.secondaryChance > 0f) CheckAttack(n + " secondary attack", en.secondaryAttack, en, r);
                if (en.drops != null)
                    foreach (var d in en.drops)
                        if (d != null && d.pickup == null && d.weapon == null) r.warnings.Add($"{n}: a drop has no pickup or weapon.");
            }
        }

        static void CheckAttack(string n, EnemyAttack a, EnemyAsset en, Report r)
        {
            if (a == null) { r.errors.Add($"{n}: missing."); return; }
            if (a.damage <= 0f) r.errors.Add($"{n}: Damage must be above 0.");
            switch (a.kind)
            {
                case EnemyAttackKind.Melee:
                    if (a.meleeRange <= 0f) r.errors.Add($"{n}: Melee Range must be above 0.");
                    if (en.brain != null && en.brain.attackRange > en.radius + a.meleeRange + 2f)
                        r.warnings.Add($"{n}: Attack Range {en.brain.attackRange} is far beyond the melee reach; it will swing at the air.");
                    break;
                case EnemyAttackKind.Hitscan:
                    if (a.accuracy <= 0f) r.errors.Add($"{n}: Accuracy must be above 0.");
                    if (en.brain != null && en.brain.windupSeconds < 0.2f) r.warnings.Add($"{n}: a hitscan wind-up under 0.2 s can't be dodged.");
                    break;
                case EnemyAttackKind.Projectile:
                    if (a.pattern == null || a.pattern.steps == null || a.pattern.steps.Count == 0) { r.errors.Add($"{n}: the pattern has no steps."); break; }
                    foreach (var s in a.pattern.steps)
                    {
                        if (s == null || s.pattern == null) { r.errors.Add($"{n}: an empty pattern step."); continue; }
                        if (s.pattern.count < 1) r.errors.Add($"{n}: a pattern step has Count 0.");
                        if (s.pattern.speed <= 0f) r.errors.Add($"{n}: a pattern step has Speed 0 (the shots would hang in the air).");
                        if (s.pattern.shots <= 0) r.errors.Add($"{n}: a pattern step fires forever (Shots 0); give it a number.");
                    }
                    if (a.projectileRadius <= 0f) r.errors.Add($"{n}: Projectile Radius must be above 0.");
                    break;
            }
        }

        static void CheckThings(DoomlikeConfig c, Report r)
        {
            if (c.pickups != null)
                foreach (var p in c.pickups)
                {
                    if (p == null) { r.errors.Add("Pickups: an empty entry."); continue; }
                    if (p.amount <= 0) r.errors.Add($"Pickup {p.name}: Amount must be above 0.");
                    if (p.kind == PickupKind.Ammo && !AmmoKnown(c, p.ammoType)) r.errors.Add($"Pickup {p.name}: ammo \"{p.ammoType}\" isn't in the config's Ammo Types.");
                    if (p.texture == null) r.warnings.Add($"Pickup {p.name}: no texture (it will be invisible).");
                }
            if (c.decorations != null)
                foreach (var d in c.decorations)
                {
                    if (d == null) { r.errors.Add("Decorations: an empty entry."); continue; }
                    if (d.texture == null) r.warnings.Add($"Decoration {d.name}: no texture.");
                    if (d.solid && d.radius <= 0f) r.errors.Add($"Decoration {d.name}: a solid decoration needs a Radius.");
                }
        }

        static void CheckUpgrades(DoomlikeConfig c, Report r)
        {
            var ids = new HashSet<string>();
            var all = new List<DoomUpgradeAsset>();
            if (c.upgrades != null) all.AddRange(c.upgrades);
            if (c.fallbackUpgrade != null) all.Add(c.fallbackUpgrade);
            if (c.upgradeDraft && (c.upgrades == null || c.upgrades.Count == 0)) r.warnings.Add("Config: the upgrade draft is on but there are no upgrades.");
            foreach (var u in all)
            {
                if (u == null) { r.errors.Add("Upgrades: an empty entry."); continue; }
                if (u.def == null || string.IsNullOrEmpty(u.def.id)) { r.errors.Add($"Upgrade {u.name}: no id."); continue; }
                if (!ids.Add(u.def.id)) r.errors.Add($"Upgrade {u.name}: id \"{u.def.id}\" is used twice.");
                if (string.IsNullOrEmpty(u.def.displayName)) r.errors.Add($"Upgrade {u.name}: no Display Name.");
                if (u.def.modifiers != null)
                    foreach (var m in u.def.modifiers)
                        if (m == null || !DoomStats.IsKnownStat(m.stat))
                            r.errors.Add($"Upgrade {u.name}: unknown stat \"{(m != null ? m.stat : "")}\" (known: {string.Join(", ", DoomStats.All)}).");
                if (u.def.effects != null)
                    foreach (var fx in u.def.effects)
                        if (!DoomStats.IsKnownEffect(fx)) r.errors.Add($"Upgrade {u.name}: unknown effect \"{fx}\" (known: heal_full, lifesteal, fill_ammo, heal:N).");
                bool does = (u.def.modifiers != null && u.def.modifiers.Count > 0) || (u.def.effects != null && u.def.effects.Count > 0);
                if (!does) r.warnings.Add($"Upgrade {u.name}: changes nothing (no modifiers or effects).");
            }
        }

        static void CheckArt(DoomlikeConfig c, Report r)
        {
            var e = r.errors;
            if (c.wallMaterials == null || c.wallMaterials.Count < 4 || c.wallMaterials.Contains(null)) e.Add("Config: needs 4 wall materials (Rebuild Shell Scenes makes them).");
            if (c.floorMaterials == null || c.floorMaterials.Count < 2 || c.floorMaterials.Contains(null)) e.Add("Config: needs 2 floor materials.");
            if (c.ceilingMaterial == null || c.hazardMaterial == null || c.exitMaterial == null || c.doorMaterial == null || c.lockedDoorMaterial == null)
                e.Add("Config: a ceiling, slime, exit or door material is missing.");
            if (c.spriteMaterial == null || c.weaponMaterial == null || c.screenMaterial == null || c.lineMaterial == null)
                e.Add("Config: the sprite, weapon, screen or line material is missing.");
            if (c.fxTexture == null || c.keyTexture == null || c.exitSignTexture == null) e.Add("Config: the effects, key or exit sign texture is missing.");
        }

        /// <summary>Logs every problem (errors as errors, warnings as warnings). True when there are no errors.</summary>
        public static bool LogProblems(DoomlikeConfig c)
        {
            var r = Check(c);
            foreach (var w in r.warnings) Debug.LogWarning("DOOM-like content: " + w);
            foreach (var err in r.errors) Debug.LogError("DOOM-like content: " + err);
            return r.Ok;
        }
    }
}
