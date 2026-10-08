using System.Collections.Generic;
using Ink.UnityIntegration;
using JamKit.Roguelite;
using UnityEngine;

namespace JamKit.BulletHell
{
    /// <summary>
    /// The bullet-hell template's one config asset: the run's shape, the floors (room pools and
    /// bosses), the player, the upgrade pool, prices, event dialogues and the palette. The scene's
    /// BulletHellGame points at it. Everything here can be tuned without code.
    /// </summary>
    [CreateAssetMenu(menuName = "JamKit/Bullet Hell/Config", fileName = "BulletHellConfig")]
    public sealed class BulletHellConfig : ScriptableObject
    {
        [Header("Run")]
        public RunConfig run = new RunConfig
        {
            floors = 3,
            roomsPerFloorMin = 5,
            roomsPerFloorMax = 6,
            combatWeight = 46f,
            eliteWeight = 12f,
            treasureWeight = 12f,
            shopWeight = 10f,
            restWeight = 4f,
            eventWeight = 9f,
            forgeWeight = 6f,
            altarWeight = 5f,
            wagerWeight = 4f,
            trialWeight = 6f,
            traderWeight = 4f,
            branchChance = 0.55f,
        };
        [Tooltip("Bumped by the template when its default content changes; a rebuild below this revision rewrites the config, the player and the changed upgrades once (see BulletHellContent).")]
        public int contentRevision;
        [Tooltip("One entry per floor (as many as Run > Floors).")]
        public List<FloorDef> floors = new List<FloorDef>();

        [Header("Player")]
        public PlayerDef player;

        [Header("Gun")]
        [Tooltip("Side streams, Seeker Rounds' homing streams, Backdraft's rebound, and the close-range Scatter and Blade.")]
        public ShotRules shots = new ShotRules();

        [Header("Upgrades")]
        public List<UpgradeAsset> upgrades = new List<UpgradeAsset>();
        [Tooltip("Offered when the pool runs dry (coins, usually).")]
        public UpgradeAsset fallbackUpgrade;
        public JamKit.BulletHell.ShmupEconomy economy = new JamKit.BulletHell.ShmupEconomy();

        [Header("Where power comes from (docs/DESIGN-ROGUELITE.md, section 1)")]
        [Tooltip("A reward table per source: rarity weights (0 = never), the small epic chance, cards, luck. Bosses, trials, sacrifices and wagers offer epics.")]
        public ShmupRewards rewards = new ShmupRewards();
        [Tooltip("Ember Hunger, Red Line and Time Dilation numbers.")]
        public PowerBoostRules boosts = new PowerBoostRules();

        [Header("Encounters between battles")]
        public ForgeRules forge = new ForgeRules();
        public AltarRules altar = new AltarRules();
        public TraderRules trader = new TraderRules();
        [Tooltip("Conditions a Trial board offers (it shows up to 3 the player can take).")]
        public List<TrialDef> trials = new List<TrialDef>();
        [Tooltip("Risky deals a Wager room offers (it shows up to 2).")]
        public List<WagerDef> wagers = new List<WagerDef>();

        [Header("Dying (remnants)")]
        public RemnantRules remnants = new RemnantRules();

        [Header("Companion")]
        [Tooltip("Off: no companion in battle (its upgrades leave the pool too).")]
        public bool companionEnabled = true;
        public CompanionDef companion;

        [Header("Mystery rooms (ink)")]
        public InkFile events;
        [Tooltip("Knots in the ink file; a Mystery room plays one of them.")]
        public List<string> eventKnots = new List<string>();

        [Header("Playfield")]
        [Tooltip("Half the play area in world units (the camera shows about 10 x 5.6).")]
        public Vector2 playfieldHalfSize = new Vector2(6f, 5.3f);

        [Header("Look")]
        [Tooltip("Pattern styleIndex 0, 1, 2... picks these in order.")]
        public List<BulletStyle> bulletStyles = new List<BulletStyle>();
        [Tooltip("Card and icon colours for Common, Rare, Epic, Legendary.")]
        public List<Color> rarityColors = new List<Color>
        {
            new Color(0.82f, 0.85f, 0.9f), new Color(0.38f, 0.68f, 1f), new Color(0.78f, 0.5f, 1f), new Color(1f, 0.79f, 0.29f),
        };
        [Tooltip("Map and door colours by room type: Battle, Elite, Treasure, Shop, Rest, Mystery, Boss, Forge, Altar, Wager, Trial board, Trader.")]
        public List<Color> roomColors = DefaultRoomColors();

        public static List<Color> DefaultRoomColors() => new List<Color>
        {
            new Color(0.55f, 0.6f, 0.75f), new Color(1f, 0.45f, 0.4f), new Color(1f, 0.82f, 0.3f), new Color(0.4f, 0.9f, 0.55f),
            new Color(0.45f, 0.75f, 1f), new Color(0.8f, 0.55f, 1f), new Color(1f, 0.3f, 0.5f),
            new Color(1f, 0.55f, 0.2f), new Color(0.75f, 0.3f, 0.45f), new Color(0.95f, 0.75f, 0.2f), new Color(0.35f, 0.85f, 0.85f),
            new Color(0.6f, 0.8f, 0.4f),
        };
        public Color coinColor = new Color(1f, 0.83f, 0.25f);
        public Color sideColor = new Color(0.03f, 0.03f, 0.06f);

        [Header("Pacing (seconds)")]
        public float roomIntroSeconds = 1.4f;
        public float roomClearedSeconds = 1.6f;
        public float waveGapSeconds = 0.6f;

        [Header("Sounds (Audio Registry ids)")]
        public string bossMusic = "music_boss";

        public FloorDef Floor(int index) =>
            floors == null || floors.Count == 0 ? null : floors[Mathf.Clamp(index, 0, floors.Count - 1)];

        public Color RarityColor(Rarity r) =>
            rarityColors != null && (int)r < rarityColors.Count ? rarityColors[(int)r] : Color.white;

        public Color RoomColor(RoomType t) =>
            roomColors != null && (int)t < roomColors.Count ? roomColors[(int)t] : Color.gray;

        public BulletStyle Style(int index) =>
            bulletStyles != null && index >= 0 && index < bulletStyles.Count ? bulletStyles[index] : null;
    }
}
