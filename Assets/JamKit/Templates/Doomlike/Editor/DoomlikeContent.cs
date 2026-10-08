using System;
using System.Collections.Generic;
using JamKit.Combat;
using JamKit.Patterns;
using JamKit.Roguelite;
using UnityEditor;
using UnityEngine;

namespace JamKit.Doomlike.EditorTools
{
    /// <summary>
    /// The template's starting content, as code, so it rebuilds reproducibly: 5 weapons, 3 ammo
    /// types, 6 enemies (one a boss), 6 pickups, 2 decorations, 11 upgrades and a fallback, 4 colour
    /// themes, and the config pointing at the 3 sample maps in Templates/Doomlike/Levels. Normal
    /// rebuilds only create what's missing (team edits survive); "Reset Content to Defaults" rewrites
    /// every asset below to these values. Materials are always refreshed (they only hold textures).
    /// </summary>
    public static class DoomlikeContent
    {
        public const string DataDir = DoomlikeBuilder.Root + "/Data";
        public const string ConfigPath = DataDir + "/DoomlikeConfig.asset";
        public const string LevelsDir = DoomlikeBuilder.Root + "/Levels";

        static bool _reset;

        public sealed class Materials
        {
            public List<Material> walls = new List<Material>(), floors = new List<Material>();
            public Material ceiling, slime, exit, door, lockedDoor, sprite, weapon, screen, line;
        }

        public static DoomlikeConfig EnsureAll(DoomlikeArt.Set art, Materials mats, bool reset)
        {
            _reset = reset;
            foreach (var d in new[] { "Weapons", "Enemies", "Pickups", "Decorations", "Upgrades" })
                DoomlikeBuilder.EnsureFolder(DataDir + "/" + d);

            // ---------- Weapons ----------
            var fist = Weapon("Knuckles", w =>
            {
                w.displayName = "Knuckles";
                w.slot = 1;
                w.mapChar = "";
                w.kind = WeaponKind.Melee;
                w.def = Def(FireMode.Auto, 0.42f);
                w.ammoType = "";
                w.damage = 20f;
                w.range = 1.8f;
                w.damageVariance = 0.25f;
                w.recoilDegrees = 0f;
                w.shake = 0.05f;
                w.noiseRadius = 3f;
                w.fireSound = "punch";
                w.priority = 0;
                w.viewTexture = art.viewFist;
                w.fireFrameSeconds = 0.12f;
            });
            var pistol = Weapon("ServicePistol", w =>
            {
                w.displayName = "Service Pistol";
                w.slot = 2;
                w.mapChar = "";
                w.kind = WeaponKind.Hitscan;
                w.def = Def(FireMode.Auto, 0.34f);
                w.ammoType = "bullets";
                w.damage = 12f;
                w.pellets = 1;
                w.spreadDegrees = 2.5f;
                w.verticalSpreadDegrees = 1f;
                w.firstPelletCentered = true;
                w.range = 80f;
                w.falloffStart = 25f;
                w.minFalloff = 0.6f;
                w.recoilDegrees = 1.2f;
                w.shake = 0.06f;
                w.noiseRadius = 24f;
                w.fireSound = "pistol";
                w.pickupAmmo = 20;
                w.priority = 1;
                w.viewTexture = art.viewPistol;
            });
            var shotgun = Weapon("Scattergun", w =>
            {
                w.displayName = "Scattergun";
                w.slot = 3;
                w.mapChar = "3";
                w.kind = WeaponKind.Hitscan;
                w.def = Def(FireMode.Auto, 0.85f);
                w.ammoType = "shells";
                w.damage = 9f;
                w.pellets = 8;
                w.spreadDegrees = 11f;
                w.verticalSpreadDegrees = 3.5f;
                w.firstPelletCentered = true;
                w.range = 45f;
                w.falloffStart = 8f;
                w.minFalloff = 0.35f;
                w.recoilDegrees = 4f;
                w.shake = 0.18f;
                w.noiseRadius = 30f;
                w.fireSound = "shotgun";
                w.pickupAmmo = 8;
                w.priority = 3;
                w.viewTexture = art.viewShotgun;
                w.fireFrameSeconds = 0.1f;
                w.pickupTexture = art.shotgunPickup;
                w.pickupSize = 0.45f;
            });
            var rivet = Weapon("RivetGun", w =>
            {
                w.displayName = "Rivet Gun";
                w.slot = 4;
                w.mapChar = "4";
                w.kind = WeaponKind.Hitscan;
                w.def = Def(FireMode.Auto, 0.1f);
                w.ammoType = "bullets";
                w.damage = 9f;
                w.pellets = 1;
                w.spreadDegrees = 4.5f;
                w.verticalSpreadDegrees = 1.5f;
                w.firstPelletCentered = false;
                w.range = 70f;
                w.falloffStart = 20f;
                w.minFalloff = 0.5f;
                w.recoilDegrees = 0.6f;
                w.shake = 0.05f;
                w.noiseRadius = 28f;
                w.fireSound = "rapid";
                w.pickupAmmo = 30;
                w.priority = 2;
                w.viewTexture = art.viewRivet;
                w.fireFrameSeconds = 0.05f;
                w.pickupTexture = art.rivetPickup;
                w.pickupSize = 0.45f;
            });
            var launcher = Weapon("PulseLauncher", w =>
            {
                w.displayName = "Pulse Launcher";
                w.slot = 5;
                w.mapChar = "5";
                w.kind = WeaponKind.Projectile;
                w.def = Def(FireMode.Auto, 0.8f);
                w.ammoType = "charges";
                w.damage = 110f;
                w.projectileSpeed = 26f;
                w.projectileRadius = 0.25f;
                w.splashRadius = 3.6f;
                w.selfDamageScale = 0.4f;
                w.projectileColor = new Color(0.45f, 0.85f, 1f);
                w.recoilDegrees = 5f;
                w.shake = 0.25f;
                w.noiseRadius = 34f;
                w.fireSound = "launcher";
                w.pickupAmmo = 4;
                w.priority = 4;
                w.viewTexture = art.viewLauncher;
                w.fireFrameSeconds = 0.12f;
                w.pickupTexture = art.launcherPickup;
                w.pickupSize = 0.45f;
            });
            foreach (var w in new[] { fist, pistol, shotgun, rivet, launcher }) RefreshWeaponArt(w, art);

            // ---------- Pickups ----------
            var patch = Pickup("HealthPatch", "Health Patch", "+", PickupKind.Health, 10, "", "Picked up a health patch.", "item", art.patch, 0.5f);
            var medkit = Pickup("Medkit", "Medkit", "H", PickupKind.Health, 25, "", "Picked up a medkit.", "item", art.medkit, 0.55f);
            var clip = Pickup("BulletClip", "Bullet Clip", "a", PickupKind.Ammo, 15, "bullets", "Picked up a clip of bullets.", "item", art.clip, 0.45f);
            var box = Pickup("BulletBox", "Box of Bullets", "A", PickupKind.Ammo, 50, "bullets", "Picked up a box of bullets.", "item", art.ammoBox, 0.5f);
            var shells = Pickup("Shells", "Shells", "c", PickupKind.Ammo, 6, "shells", "Picked up some shells.", "item", art.shells, 0.45f);
            var charges = Pickup("Charges", "Charges", "o", PickupKind.Ammo, 3, "charges", "Picked up some charges.", "item", art.charges, 0.5f);

            // ---------- Decorations ----------
            var pillar = Deco("LampPillar", "Lamp pillar", "T", true, 0.35f, art.pillar, 2.4f, 0.8f);
            var junk = Deco("Junk", "Bones", "j", false, 0f, art.junk, 0.45f, 0f);

            // ---------- Enemies ----------
            var husk = Enemy("Husk", art.husk, e =>
            {
                e.displayName = "Husk";
                e.mapChar = "m";
                e.health = 30f;
                e.speed = 6.2f;
                e.radius = 0.4f;
                e.height = 1.7f;
                e.brain = Brain(sight: 24f, attackRange: 2.4f, cooldown: 0.9f, windup: 0.35f, pain: 0.6f, aggression: 4f);
                e.attack = new EnemyAttack { kind = EnemyAttackKind.Melee, damage = 10f, meleeRange = 1.2f, sound = "bite" };
                e.gibOverkill = 25f;
                e.score = 50;
            });
            var gunner = Enemy("Gunner", art.gunner, e =>
            {
                e.displayName = "Gunner";
                e.mapChar = "g";
                e.health = 40f;
                e.speed = 3.5f;
                e.radius = 0.4f;
                e.height = 1.8f;
                e.brain = Brain(sight: 30f, attackRange: 22f, cooldown: 1.8f, windup: 0.6f, pain: 0.5f, aggression: 1.4f);
                e.attack = new EnemyAttack { kind = EnemyAttackKind.Hitscan, damage = 6f, pellets = 2, accuracy = 0.65f, muzzleHeight = 1.3f, sound = "enemy_shot" };
                e.gibOverkill = 30f;
                e.score = 100;
                e.drops = new List<EnemyDrop> { new EnemyDrop { pickup = clip, chance = 0.6f } };
            });
            var spitter = Enemy("Spitter", art.spitter, e =>
            {
                e.displayName = "Spitter";
                e.mapChar = "s";
                e.health = 55f;
                e.speed = 3.8f;
                e.radius = 0.4f;
                e.height = 2f;
                e.brain = Brain(sight: 30f, attackRange: 26f, cooldown: 2f, windup: 0.5f, pain: 0.5f, aggression: 1.2f);
                e.attack = new EnemyAttack
                {
                    kind = EnemyAttackKind.Projectile,
                    damage = 10f,
                    projectileRadius = 0.3f,
                    projectileColor = new Color(0.55f, 1f, 0.35f),
                    muzzleHeight = 1.5f,
                    sound = "enemy_shot",
                    pattern = PatternSequence.Single(new PatternDef { shape = PatternShape.Aimed, count = 3, spreadDegrees = 18f, speed = 11f, lifetime = 5f }),
                };
                e.gibOverkill = 35f;
                e.score = 150;
                e.drops = new List<EnemyDrop> { new EnemyDrop { pickup = patch, chance = 0.25f } };
            });
            var brute = Enemy("Brute", art.brute, e =>
            {
                e.displayName = "Brute";
                e.mapChar = "h";
                e.health = 160f;
                e.speed = 4.6f;
                e.radius = 0.6f;
                e.height = 2.1f;
                e.brain = Brain(sight: 26f, attackRange: 2.8f, cooldown: 1.2f, windup: 0.5f, pain: 0.2f, aggression: 4f);
                e.attack = new EnemyAttack { kind = EnemyAttackKind.Melee, damage = 24f, meleeRange = 1.5f, sound = "bite" };
                e.gibOverkill = 60f;
                e.score = 300;
                e.drops = new List<EnemyDrop> { new EnemyDrop { pickup = shells, chance = 0.5f } };
            });
            var wisp = Enemy("Wisp", art.wisp, e =>
            {
                e.displayName = "Wisp";
                e.mapChar = "w";
                e.health = 25f;
                e.speed = 5.2f;
                e.radius = 0.4f;
                e.height = 1.1f;
                e.hoverHeight = 1.1f;
                e.brain = Brain(sight: 26f, attackRange: 18f, cooldown: 2.2f, windup: 0.4f, pain: 0.8f, aggression: 1.3f);
                e.brain.fovDegrees = 360f;
                e.attack = new EnemyAttack
                {
                    kind = EnemyAttackKind.Projectile,
                    damage = 7f,
                    projectileRadius = 0.25f,
                    projectileColor = new Color(0.4f, 0.95f, 1f),
                    muzzleHeight = 0.5f,
                    sound = "enemy_shot",
                    pattern = PatternSequence.Single(new PatternDef { shape = PatternShape.Ring, count = 8, speed = 8f, aimAtTarget = true, lifetime = 4f }),
                };
                e.gibOverkill = 0f;
                e.score = 120;
            });
            var warden = Enemy("Warden", art.warden, e =>
            {
                e.displayName = "Warden";
                e.mapChar = "W";
                e.health = 1100f;
                e.speed = 3.2f;
                e.radius = 0.9f;
                e.height = 3.4f;
                e.brain = Brain(sight: 45f, attackRange: 40f, cooldown: 1.7f, windup: 0.7f, pain: 0.06f, aggression: 1.5f);
                e.brain.fovDegrees = 360f;
                e.brain.hearingRange = 45f;
                e.brain.enrageBelow = 0.5f;
                e.attack = new EnemyAttack
                {
                    kind = EnemyAttackKind.Projectile,
                    damage = 10f,
                    projectileRadius = 0.35f,
                    projectileColor = new Color(1f, 0.4f, 0.2f),
                    muzzleHeight = 2.4f,
                    sound = "enemy_shot",
                    pattern = PatternSequence.Single(new PatternDef { shape = PatternShape.Spiral, count = 4, rotatePerShotDegrees = 11f, shots = 24, shotInterval = 0.09f, speed = 9f, lifetime = 6f }),
                };
                e.secondaryChance = 0.45f;
                e.secondaryAttack = new EnemyAttack
                {
                    kind = EnemyAttackKind.Projectile,
                    damage = 12f,
                    projectileRadius = 0.35f,
                    splashRadius = 1.6f,
                    projectileColor = new Color(1f, 0.85f, 0.25f),
                    muzzleHeight = 2.4f,
                    sound = "launcher",
                    pattern = PatternSequence.Single(new PatternDef { shape = PatternShape.Aimed, count = 5, spreadDegrees = 32f, shots = 3, shotInterval = 0.28f, speed = 14f, lifetime = 5f }),
                };
                e.gibOverkill = 0f;
                e.score = 5000;
                e.isBoss = true;
                e.bossTitle = "THE WARDEN";
                e.drops = new List<EnemyDrop>();
            });

            // ---------- Upgrades ----------
            var ups = new List<DoomUpgradeAsset>
            {
                Up("hollow_points", "Hollow Points", Rarity.Common, 3, "+15% damage with every weapon.", "DMG", new Color(1f, 0.45f, 0.35f), M(DoomStats.Damage, StatOp.PercentAdd, 0.15f)),
                Up("hair_trigger", "Hair Trigger", Rarity.Common, 3, "Guns fire 15% faster.", "ROF", new Color(1f, 0.8f, 0.3f), M(DoomStats.FireRate, StatOp.PercentAdd, 0.15f)),
                Up("light_boots", "Light Boots", Rarity.Common, 2, "Run 10% faster.", "SPD", new Color(0.45f, 0.95f, 1f), M(DoomStats.MoveSpeed, StatOp.PercentAdd, 0.1f)),
                Up("thick_skin", "Thick Skin", Rarity.Common, 3, "+25 max health (and heals it).", "HP+", new Color(1f, 0.4f, 0.55f), M(DoomStats.MaxHealth, StatOp.Flat, 25f)),
                Up("bandolier", "Bandolier", Rarity.Common, 2, "Carry 40% more ammo, and fill every pouch now.", "AMO", new Color(0.9f, 0.75f, 0.4f), M(DoomStats.AmmoCapacity, StatOp.PercentAdd, 0.4f), DoomStats.FillAmmo),
                Up("heavy_hands", "Heavy Hands", Rarity.Common, 2, "Punches hit twice as hard.", "FST", new Color(0.95f, 0.65f, 0.45f), M(DoomStats.MeleeDamage, StatOp.PercentAdd, 1f)),
                Up("field_medic", "Field Medic", Rarity.Rare, 0, "Heal to full right now.", "MED", new Color(0.5f, 1f, 0.6f), null, DoomStats.HealFull),
                Up("leech_rounds", "Leech Rounds", Rarity.Rare, 2, "Every kill heals 4 health.", "LCH", new Color(0.85f, 0.3f, 0.45f), null, DoomStats.Lifesteal),
                Up("big_bang", "Big Bang", Rarity.Rare, 2, "Explosions reach 35% further.", "BLS", new Color(0.45f, 0.8f, 1f), M(DoomStats.SplashRadius, StatOp.PercentAdd, 0.35f)),
                Up("scavenger", "Scavenger", Rarity.Rare, 2, "Health and ammo pickups give 50% more.", "SCV", new Color(0.7f, 0.9f, 0.4f), M(DoomStats.PickupBonus, StatOp.PercentAdd, 0.5f)),
                Up("plating", "Plating", Rarity.Epic, 2, "Take 15% less damage.", "ARM", new Color(0.75f, 0.75f, 0.85f), M(DoomStats.Armor, StatOp.Flat, 0.15f)),
            };
            var fallback = Up("second_wind", "Second Wind", Rarity.Common, 0, "Heal 30 health.", "+30", new Color(0.5f, 1f, 0.6f), null, DoomStats.HealPrefix + "30");

            // ---------- Config ----------
            var config = Asset<DoomlikeConfig>(ConfigPath, c =>
            {
                c.levels = new List<TextAsset>();
                c.cellSize = 2.5f;
                c.defaultCeiling = 3.6f;
                c.ambushRadius = 8;
                c.levelMusic = "music_game";
                c.bossMusic = "music_boss";
                c.player = new PlayerTuning();
                c.weapons = new List<WeaponAsset> { fist, pistol, shotgun, rivet, launcher };
                c.startingWeapons = new List<WeaponAsset> { fist, pistol };
                c.ammoTypes = new List<AmmoTypeDef>
                {
                    new AmmoTypeDef { id = "bullets", displayName = "Bullets", shortName = "BULL", start = 50, max = 200 },
                    new AmmoTypeDef { id = "shells", displayName = "Shells", shortName = "SHEL", start = 0, max = 50 },
                    new AmmoTypeDef { id = "charges", displayName = "Charges", shortName = "CHRG", start = 0, max = 30 },
                };
                c.enemies = new List<EnemyAsset> { husk, gunner, spitter, brute, wisp, warden };
                c.pickups = new List<PickupAsset> { patch, medkit, clip, box, shells, charges };
                c.decorations = new List<DecorationAsset> { pillar, junk };
                c.upgradeDraft = true;
                c.draftSize = 3;
                c.upgrades = ups;
                c.fallbackUpgrade = fallback;
                c.healthFloorPerLevel = 50f;
                c.themes = Themes();
                c.retro = new RetroLook();
                c.killHitStop = 0.045f;
                c.hurtShake = 0.35f;
                c.hurtFlashAlpha = 0.35f;
            });
            if (config.levels == null) config.levels = new List<TextAsset>();
            if (config.levels.Count == 0 || _reset)
            {
                config.levels.Clear();
                foreach (var n in new[] { "Level1", "Level2", "Level3" })
                {
                    var t = AssetDatabase.LoadAssetAtPath<TextAsset>(LevelsDir + "/" + n + ".txt");
                    if (t != null) config.levels.Add(t);
                    else Debug.LogError("JamKit: missing sample map " + LevelsDir + "/" + n + ".txt");
                }
            }

            // Art is always refreshed: a new build makes the textures, the config points at them.
            config.wallMaterials = mats.walls;
            config.floorMaterials = mats.floors;
            config.ceilingMaterial = mats.ceiling;
            config.hazardMaterial = mats.slime;
            config.exitMaterial = mats.exit;
            config.doorMaterial = mats.door;
            config.lockedDoorMaterial = mats.lockedDoor;
            config.spriteMaterial = mats.sprite;
            config.weaponMaterial = mats.weapon;
            config.screenMaterial = mats.screen;
            config.lineMaterial = mats.line;
            if (config.keyTexture == null) config.keyTexture = art.key;
            if (config.exitSignTexture == null) config.exitSignTexture = art.exitSign;
            if (config.fxTexture == null) config.fxTexture = art.fx;
            EditorUtility.SetDirty(config);
            return config;
        }

        static List<LevelTheme> Themes() => new List<LevelTheme>
        {
            new LevelTheme
            {
                name = "base",
                wallTints = new List<Color> { new Color(0.85f, 0.85f, 0.9f), new Color(0.8f, 0.85f, 0.95f), Color.white, new Color(0.75f, 0.7f, 0.65f) },
                floorTint = new Color(0.8f, 0.8f, 0.85f),
                ceilingTint = new Color(0.65f, 0.65f, 0.7f),
                fogColor = new Color(0.04f, 0.05f, 0.08f),
                fogStart = 8f, fogEnd = 48f, fogStrength = 0.9f,
            },
            new LevelTheme
            {
                name = "rust",
                wallTints = new List<Color> { new Color(1f, 0.78f, 0.6f), new Color(0.95f, 0.7f, 0.5f), new Color(1f, 0.9f, 0.8f), new Color(0.85f, 0.6f, 0.45f) },
                floorTint = new Color(0.85f, 0.7f, 0.55f),
                ceilingTint = new Color(0.6f, 0.5f, 0.42f),
                fogColor = new Color(0.12f, 0.06f, 0.03f),
                fogStart = 6f, fogEnd = 40f, fogStrength = 0.92f,
            },
            new LevelTheme
            {
                name = "toxic",
                wallTints = new List<Color> { new Color(0.7f, 0.85f, 0.65f), new Color(0.65f, 0.8f, 0.7f), new Color(0.8f, 1f, 0.85f), new Color(0.55f, 0.65f, 0.5f) },
                floorTint = new Color(0.65f, 0.75f, 0.6f),
                ceilingTint = new Color(0.45f, 0.55f, 0.45f),
                fogColor = new Color(0.03f, 0.08f, 0.04f),
                fogStart = 5f, fogEnd = 34f, fogStrength = 0.95f,
            },
            new LevelTheme
            {
                name = "ember",
                wallTints = new List<Color> { new Color(1f, 0.55f, 0.45f), new Color(0.9f, 0.5f, 0.45f), new Color(1f, 0.75f, 0.6f), new Color(0.8f, 0.4f, 0.3f) },
                floorTint = new Color(0.85f, 0.5f, 0.4f),
                ceilingTint = new Color(0.5f, 0.25f, 0.2f),
                fogColor = new Color(0.16f, 0.03f, 0.02f),
                fogStart = 6f, fogEnd = 44f, fogStrength = 0.9f,
            },
        };

        static void RefreshWeaponArt(WeaponAsset w, DoomlikeArt.Set art)
        {
            // Point unset art at the generated textures, without overriding art the team swapped in.
            bool dirty = false;
            if (w.viewTexture == null)
            {
                w.viewTexture = w.name == "Knuckles" ? art.viewFist : w.name == "ServicePistol" ? art.viewPistol : w.name == "Scattergun" ? art.viewShotgun
                    : w.name == "RivetGun" ? art.viewRivet : w.name == "PulseLauncher" ? art.viewLauncher : null;
                dirty = true;
            }
            if (dirty) EditorUtility.SetDirty(w);
        }

        // ---------- Helpers ----------

        static T Asset<T>(string path, Action<T> fill) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            bool created = a == null;
            if (created)
            {
                a = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(a, path);
            }
            if (created || _reset)
            {
                fill(a);
                EditorUtility.SetDirty(a);
            }
            return a;
        }

        static WeaponDef Def(FireMode mode, float interval) => new WeaponDef
        {
            magazineSize = 0,
            startingReserve = 0,
            maxReserve = 0,
            ammoPerShot = 1,
            fireMode = mode,
            fireInterval = interval,
            autoReloadOnEmpty = false,
        };

        static WeaponAsset Weapon(string file, Action<WeaponAsset> fill) =>
            Asset<WeaponAsset>(DataDir + "/Weapons/" + file + ".asset", fill);

        static EnemyBrainDef Brain(float sight, float attackRange, float cooldown, float windup, float pain, float aggression) => new EnemyBrainDef
        {
            sightRange = sight,
            fovDegrees = 200f,
            hearingRange = 24f,
            reactionSeconds = 0.45f,
            attackRange = attackRange,
            attackCooldown = cooldown,
            aggression = aggression,
            windupSeconds = windup,
            fireSeconds = 0.1f,
            recoverSeconds = 0.35f,
            painChance = pain,
            painSeconds = 0.3f,
        };

        static EnemyAsset Enemy(string file, Texture2D sheet, Action<EnemyAsset> fill)
        {
            var e = Asset<EnemyAsset>(DataDir + "/Enemies/" + file + ".asset", a =>
            {
                fill(a);
                a.sprite = new SpriteSheet { texture = sheet, directions = 8, deathFrames = 4, gibFrames = 3 };
                a.tint = Color.white;
            });
            if (e.sprite != null && e.sprite.texture == null)
            {
                e.sprite.texture = sheet;
                EditorUtility.SetDirty(e);
            }
            return e;
        }

        static PickupAsset Pickup(string file, string name, string mapChar, PickupKind kind, int amount, string ammo, string message, string sound, Texture2D tex, float size)
        {
            var p = Asset<PickupAsset>(DataDir + "/Pickups/" + file + ".asset", a =>
            {
                a.displayName = name;
                a.mapChar = mapChar;
                a.kind = kind;
                a.amount = amount;
                a.ammoType = ammo;
                a.message = message;
                a.sound = sound;
                a.countsAsItem = true;
                a.texture = tex;
                a.tint = Color.white;
                a.size = size;
            });
            if (p.texture == null) { p.texture = tex; EditorUtility.SetDirty(p); }
            return p;
        }

        static DecorationAsset Deco(string file, string name, string mapChar, bool solid, float radius, Texture2D tex, float size, float glow)
        {
            var d = Asset<DecorationAsset>(DataDir + "/Decorations/" + file + ".asset", a =>
            {
                a.displayName = name;
                a.mapChar = mapChar;
                a.solid = solid;
                a.radius = radius;
                a.texture = tex;
                a.tint = Color.white;
                a.size = size;
                a.glow = glow;
            });
            if (d.texture == null) { d.texture = tex; EditorUtility.SetDirty(d); }
            return d;
        }

        static StatModifier M(string stat, StatOp op, float value) => new StatModifier(stat, op, value);

        static DoomUpgradeAsset Up(string id, string name, Rarity rarity, int stacks, string description, string iconText, Color iconColor,
            StatModifier mod, params string[] effects)
        {
            return Asset<DoomUpgradeAsset>(DataDir + "/Upgrades/" + id + ".asset", u =>
            {
                u.def = new UpgradeDef(id, rarity, stacks) { displayName = name, description = description };
                if (mod != null) u.def.modifiers.Add(mod);
                if (effects != null) u.def.effects.AddRange(effects);
                u.iconText = iconText;
                u.iconColor = iconColor;
            });
        }
    }
}
