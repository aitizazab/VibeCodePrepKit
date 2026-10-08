using System;
using System.Collections.Generic;
using UnityEngine;

namespace JamKit.Doomlike
{
    /// <summary>The player's body and feel.</summary>
    [Serializable]
    public class PlayerTuning
    {
        public float maxHealth = 100f;
        public PlayerMotionDef motion = new PlayerMotionDef();
        public float gravity = 30f;
        public float radius = 0.38f;
        public float height = 1.7f;
        public float eyeHeight = 1.5f;
        [Tooltip("Mouse look: degrees per pixel at sensitivity x1 (Settings > Mouse sensitivity scales it).")]
        public float mouseDegreesPerPixel = 0.09f;
        [Tooltip("Gamepad look: degrees per second at full stick.")]
        public float gamepadDegreesPerSecond = 170f;
        public float maxPitch = 80f;
        [Tooltip("Head bob height in metres at full run (0 = off).")]
        public float bobAmount = 0.045f;
        [Tooltip("Metres walked per footstep sound.")]
        public float footstepStride = 2.3f;
        [Tooltip("Slime damage per second.")]
        public float hazardDamagePerSecond = 12f;
        [Tooltip("Reach for E / Space (doors, secret walls), metres.")]
        public float useRange = 2.2f;
        [Tooltip("Helps aim up and down: shots bend toward an enemy this close to the crosshair, degrees (0 = off).")]
        public float verticalAutoAimDegrees = 6f;
    }

    /// <summary>A level's colours: wall tints per texture, floor and ceiling tints, fog.
    /// A map picks one by name or number in its header (theme: rust).</summary>
    [Serializable]
    public class LevelTheme
    {
        public string name = "base";
        [Tooltip("Tints for wall textures 1-4 (#, %, &, =).")]
        public List<Color> wallTints = new List<Color> { Color.white, Color.white, Color.white, Color.white };
        public Color floorTint = Color.white;
        public Color ceilingTint = new Color(0.7f, 0.7f, 0.75f);
        [Tooltip("Far things fade to this (also the background).")]
        public Color fogColor = new Color(0.05f, 0.05f, 0.08f);
        public float fogStart = 6f;
        public float fogEnd = 42f;
        [Tooltip("How much the fog shows at Fog End (0..1).")]
        [Range(0f, 1f)] public float fogStrength = 0.92f;

        public Color WallTint(int variant) =>
            wallTints != null && variant >= 0 && variant < wallTints.Count ? wallTints[variant] : Color.white;
    }

    /// <summary>The low-resolution "pixel look": the world renders small and is scaled up with hard
    /// pixels, optionally reduced to fewer colours with a dither. Players switch it in Settings >
    /// Pixel look (on unless they turn it off).</summary>
    [Serializable]
    public class RetroLook
    {
        [Tooltip("Rows of pixels the world renders at (270 = a quarter of 1080p). The width follows the window.")]
        public int pixelHeight = 270;
        [Tooltip("Colour steps per channel (the palette lock). 0 = no colour reduction.")]
        public int colorLevels = 12;
        [Tooltip("Ordered dither between colour steps (0 = off, 1 = full).")]
        [Range(0f, 1f)] public float dither = 0.6f;
    }

    /// <summary>
    /// The DOOM-like template's one config asset: the levels (text maps), the player, weapons, ammo,
    /// enemies, pickups, decorations, the upgrade draft, colours and the materials the builder made.
    /// The scene's DoomGame points at it.
    /// </summary>
    [CreateAssetMenu(menuName = "JamKit/DOOM-like/Config", fileName = "DoomlikeConfig")]
    public sealed class DoomlikeConfig : ScriptableObject
    {
        [Header("Levels (played in order)")]
        [Tooltip("Text maps: see docs/TEMPLATE-DOOMLIKE.md for the legend.")]
        public List<TextAsset> levels = new List<TextAsset>();
        [Tooltip("Width of one map character in metres.")]
        public float cellSize = 2.5f;
        [Tooltip("Ceiling height when a map doesn't say (metres).")]
        public float defaultCeiling = 3.6f;
        [Tooltip("Cells around an ambush trigger (!) whose closet walls (|) open and enemies wake.")]
        public int ambushRadius = 8;
        public string levelMusic = "music_game";
        [Tooltip("Music for a level whose map has a boss.")]
        public string bossMusic = "music_boss";

        [Header("Player")]
        public PlayerTuning player = new PlayerTuning();

        [Header("Weapons and ammo")]
        public List<WeaponAsset> weapons = new List<WeaponAsset>();
        [Tooltip("Owned at the start of a run.")]
        public List<WeaponAsset> startingWeapons = new List<WeaponAsset>();
        public List<JamKit.Doomlike.AmmoTypeDef> ammoTypes = new List<JamKit.Doomlike.AmmoTypeDef>();

        [Header("Things on the map")]
        public List<EnemyAsset> enemies = new List<EnemyAsset>();
        public List<PickupAsset> pickups = new List<PickupAsset>();
        public List<DecorationAsset> decorations = new List<DecorationAsset>();

        [Header("Between levels")]
        [Tooltip("After each level but the last, pick 1 of 3 upgrades.")]
        public bool upgradeDraft = true;
        public int draftSize = 3;
        public List<DoomUpgradeAsset> upgrades = new List<DoomUpgradeAsset>();
        [Tooltip("Offered when nothing else can be.")]
        public DoomUpgradeAsset fallbackUpgrade;
        [Tooltip("Health tops up to at least this much at the start of each level (0 = keep what you had).")]
        public float healthFloorPerLevel = 50f;

        [Header("Look")]
        public List<LevelTheme> themes = new List<LevelTheme> { new LevelTheme() };
        public RetroLook retro = new RetroLook();
        [Tooltip("Red, blue, yellow.")]
        public List<Color> keyColors = new List<Color> { new Color(1f, 0.25f, 0.25f), new Color(0.3f, 0.5f, 1f), new Color(1f, 0.85f, 0.2f) };

        [Header("Feel")]
        [Tooltip("Game freeze on a kill, seconds.")]
        public float killHitStop = 0.045f;
        [Tooltip("Screen shake when you're hit, 0..1 at 25 damage.")]
        public float hurtShake = 0.35f;
        public float hurtFlashAlpha = 0.35f;

        [Header("Art (set by the builder; swap textures freely)")]
        public List<Material> wallMaterials = new List<Material>();
        public List<Material> floorMaterials = new List<Material>();
        public Material ceilingMaterial;
        public Material hazardMaterial;
        public Material exitMaterial;
        public Material doorMaterial;
        public Material lockedDoorMaterial;
        [Tooltip("Template for every billboard (enemies, pickups, decorations, effects).")]
        public Material spriteMaterial;
        [Tooltip("Template for the weapon in your hands (drawn on top of everything).")]
        public Material weaponMaterial;
        [Tooltip("Scales the low-res picture up to the screen (pixel look).")]
        public Material screenMaterial;
        [Tooltip("Enemies' aim lines (a plain white sprite material).")]
        public Material lineMaterial;
        public Texture2D keyTexture;
        public Texture2D exitSignTexture;
        [Tooltip("Effects sheet: 8 columns x 4 rows (puff, blood, blast, shot).")]
        public Texture2D fxTexture;

        // ---------- Lookups ----------

        public int LevelCount => levels != null ? levels.Count : 0;

        public LevelTheme Theme(string nameOrIndex)
        {
            if (themes == null || themes.Count == 0) return new LevelTheme();
            if (!string.IsNullOrEmpty(nameOrIndex))
            {
                foreach (var t in themes)
                    if (t != null && string.Equals(t.name, nameOrIndex.Trim(), StringComparison.OrdinalIgnoreCase)) return t;
                if (int.TryParse(nameOrIndex, out int i) && i >= 0 && i < themes.Count && themes[i] != null) return themes[i];
            }
            return themes[0] ?? new LevelTheme();
        }

        public Color KeyColor(int color) =>
            keyColors != null && color >= 0 && color < keyColors.Count ? keyColors[color] : Color.white;

        public float CeilingFor(MapData map) => map != null && map.ceiling > 0f ? map.ceiling : defaultCeiling;

        public WeaponAsset WeaponInSlot(int slot)
        {
            if (weapons == null) return null;
            foreach (var w in weapons) if (w != null && w.slot == slot) return w;
            return null;
        }

        /// <summary>The map legend: the shared geometry characters plus one per enemy, pickup, weapon
        /// and decoration. Clashing characters are reported in <paramref name="errors"/> (may be null).</summary>
        public MapLegend BuildLegend(List<string> errors)
        {
            var legend = MapLegend.Default();
            if (enemies != null)
                for (int i = 0; i < enemies.Count; i++)
                    if (enemies[i] != null) Add(legend, enemies[i].mapChar, EntityCategory.Enemy, i, false, "enemy " + enemies[i].displayName, enemies[i].name, errors);
            if (pickups != null)
                for (int i = 0; i < pickups.Count; i++)
                    if (pickups[i] != null) Add(legend, pickups[i].mapChar, EntityCategory.Pickup, i, false, pickups[i].displayName, pickups[i].name, errors);
            if (weapons != null)
                for (int i = 0; i < weapons.Count; i++)
                    if (weapons[i] != null && !string.IsNullOrEmpty(weapons[i].mapChar))
                        Add(legend, weapons[i].mapChar, EntityCategory.Weapon, i, false, weapons[i].displayName, weapons[i].name, errors);
            if (decorations != null)
                for (int i = 0; i < decorations.Count; i++)
                    if (decorations[i] != null) Add(legend, decorations[i].mapChar, EntityCategory.Decoration, i, decorations[i].solid, decorations[i].displayName, decorations[i].name, errors);
            return legend;
        }

        static void Add(MapLegend legend, string mapChar, EntityCategory cat, int index, bool solid, string description, string assetName, List<string> errors)
        {
            if (string.IsNullOrEmpty(mapChar) || mapChar.Length != 1)
            {
                errors?.Add($"{assetName}: Map Char must be exactly one character (it's \"{mapChar}\").");
                return;
            }
            char c = mapChar[0];
            if (c == '\t' || char.IsWhiteSpace(c))
            {
                errors?.Add($"{assetName}: Map Char can't be a space or a tab.");
                return;
            }
            if (!legend.AddEntity(c, cat, index, solid, description))
            {
                legend.TryGet(c, out var taken);
                errors?.Add($"{assetName}: Map Char '{c}' is already used ({taken.description}). Pick another.");
            }
        }

        /// <summary>Parses level <paramref name="index"/> with this config's legend.</summary>
        public MapData ParseLevel(int index, MapProblems problems)
        {
            var legend = BuildLegend(problems != null ? problems.errors : null);
            if (levels == null || index < 0 || index >= levels.Count || levels[index] == null)
            {
                problems?.Error($"Level {index + 1} has no map file.");
                return null;
            }
            return MapParser.Parse(levels[index].text, legend, problems);
        }
    }
}
