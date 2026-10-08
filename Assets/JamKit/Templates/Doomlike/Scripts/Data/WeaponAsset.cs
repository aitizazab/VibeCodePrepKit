using JamKit.Combat;
using UnityEngine;

namespace JamKit.Doomlike
{
    public enum WeaponKind
    {
        /// <summary>A short-range hit in front (fists, a blade).</summary>
        Melee,
        /// <summary>Instant hits along the aim, one ray per pellet (pistol, shotgun, chaingun).</summary>
        Hitscan,
        /// <summary>A flying shot that explodes on impact (a launcher).</summary>
        Projectile,
    }

    /// <summary>
    /// One gun: its timing and ammo use (core <see cref="WeaponDef"/>, magazine-less: shots draw from
    /// the shared ammo pool named by <see cref="ammoType"/>), its damage and spread, how it feels and
    /// how it looks in your hands. Duplicate one to make a new gun and add it to the config's Weapons.
    /// </summary>
    [CreateAssetMenu(menuName = "JamKit/DOOM-like/Weapon", fileName = "Weapon")]
    public sealed class WeaponAsset : ScriptableObject
    {
        public string displayName = "Pistol";
        [Tooltip("The number key that selects it (1-9). Two weapons can't share a slot.")]
        public int slot = 2;
        [Tooltip("The map character for this weapon lying on the floor (empty = it can't be placed).")]
        public string mapChar = "";
        public WeaponKind kind = WeaponKind.Hitscan;
        [Tooltip("Fire mode and speed. Keep Magazine Size 0: ammo comes from the shared pool.")]
        public WeaponDef def = new WeaponDef { magazineSize = 0, startingReserve = 0, fireMode = FireMode.Semi, fireInterval = 0.35f, autoReloadOnEmpty = false };
        [Tooltip("Id of the ammo it uses (config > Ammo Types). Empty = needs no ammo (fists).")]
        public string ammoType = "bullets";

        [Header("Damage")]
        [Tooltip("Per pellet (hitscan), per hit (melee) or at the centre of the blast (projectile).")]
        public float damage = 12f;
        [Tooltip("Rays per shot: 1 for a pistol, 7-9 for a shotgun.")]
        public int pellets = 1;
        [Tooltip("Full width of the spread, degrees left to right.")]
        public float spreadDegrees = 2f;
        public float verticalSpreadDegrees = 1f;
        [Tooltip("The first pellet flies dead centre (accurate single shots).")]
        public bool firstPelletCentered = true;
        [Tooltip("Metres. Hitscan hits nothing beyond this; melee reaches this far.")]
        public float range = 80f;
        [Tooltip("Full damage up to this distance, then it falls off to Min Falloff at Range.")]
        public float falloffStart = 25f;
        [Range(0f, 1f)] public float minFalloff = 0.5f;
        [Tooltip("Random damage per pellet: 0.15 = 85% to 115%.")]
        [Range(0f, 0.5f)] public float damageVariance = 0.15f;

        [Header("Projectile (Kind = Projectile)")]
        public float projectileSpeed = 26f;
        public float projectileRadius = 0.25f;
        public float splashRadius = 3.5f;
        [Tooltip("Your own blast hurts you this much (0 = never).")]
        [Range(0f, 1f)] public float selfDamageScale = 0.4f;
        public Color projectileColor = new Color(1f, 0.6f, 0.2f);

        [Header("Feel")]
        [Tooltip("Degrees the view kicks up per shot (it settles back).")]
        public float recoilDegrees = 1.2f;
        [Range(0f, 1f)] public float shake = 0.08f;
        [Tooltip("Enemies within this many metres hear the shot and wake up.")]
        public float noiseRadius = 26f;
        public string fireSound = "pistol";
        [Tooltip("Ammo it gives when picked up from the floor.")]
        public int pickupAmmo = 8;
        [Tooltip("Higher = preferred when picking it up or running dry.")]
        public int priority = 2;

        [Header("Look")]
        [Tooltip("A horizontal strip: frame 0 idle, then the firing frames.")]
        public Texture2D viewTexture;
        public int viewFrames = 3;
        [Tooltip("Seconds each firing frame shows.")]
        public float fireFrameSeconds = 0.07f;
        [Tooltip("Size of the gun on screen (1 = a third of the screen height).")]
        public float viewScale = 1f;
        [Tooltip("Shift on screen: x right, y up, as a fraction of the screen height.")]
        public Vector2 viewOffset;
        [Tooltip("How it looks lying on the floor.")]
        public Texture2D pickupTexture;
        public float pickupSize = 0.9f;

        public bool UsesAmmo => !string.IsNullOrEmpty(ammoType);
        public char MapChar => string.IsNullOrEmpty(mapChar) ? '\0' : mapChar[0];
    }
}
