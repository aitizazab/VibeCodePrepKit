using System;
using System.Collections.Generic;
using JamKit.Patterns;
using UnityEngine;

namespace JamKit.Doomlike
{
    public enum EnemyAttackKind
    {
        /// <summary>Bites or swipes within Melee Range.</summary>
        Melee,
        /// <summary>Instant shots after a visible wind-up (a red aim line): dodge out of sight to avoid them.</summary>
        Hitscan,
        /// <summary>Fires a bullet pattern (core PatternDef, laid flat on the floor plane) of slow, visible shots.</summary>
        Projectile,
    }

    /// <summary>One attack an enemy can make.</summary>
    [Serializable]
    public class EnemyAttack
    {
        public EnemyAttackKind kind = EnemyAttackKind.Projectile;
        [Tooltip("Per hit, per pellet or per projectile.")]
        public float damage = 10f;
        [Tooltip("Hitscan: shots per attack.")]
        public int pellets = 1;
        [Tooltip("Hitscan: chance to hit a standing target up close (it drops with distance and your speed).")]
        [Range(0f, 1f)] public float accuracy = 0.75f;
        [Tooltip("Melee: reach in metres (from its edge).")]
        public float meleeRange = 1.4f;
        [Tooltip("Projectile: the shots, as a bullet pattern. Speeds are metres per second; angles are around it on the floor (aim at target to fire at you).")]
        public PatternSequence pattern = new PatternSequence();
        public float projectileRadius = 0.3f;
        [Tooltip("Projectile: explodes with this radius on impact (0 = no blast).")]
        public float splashRadius;
        public Color projectileColor = new Color(1f, 0.45f, 0.2f);
        [Tooltip("Height the shots leave from, metres above its feet.")]
        public float muzzleHeight = 1.2f;
        public string sound = "enemy_shot";
    }

    /// <summary>Something it may leave behind when it dies.</summary>
    [Serializable]
    public class EnemyDrop
    {
        public PickupAsset pickup;
        public WeaponAsset weapon;
        [Range(0f, 1f)] public float chance = 0.5f;
    }

    /// <summary>
    /// One enemy type: health, speed, senses and attacks (core <see cref="EnemyBrainDef"/>), drops,
    /// score, and its 8-angle billboard sprite sheet. Place it on a map with its Map Char.
    /// </summary>
    [CreateAssetMenu(menuName = "JamKit/DOOM-like/Enemy", fileName = "Enemy")]
    public sealed class EnemyAsset : ScriptableObject
    {
        public string displayName = "Enemy";
        [Tooltip("The character that places it on a map (one letter; not one the map legend already uses).")]
        public string mapChar = "m";
        public float health = 30f;
        [Tooltip("Walking speed, metres per second (the player runs at about 9.5).")]
        public float speed = 4f;
        [Tooltip("Body radius for hits and walking, metres.")]
        public float radius = 0.45f;
        [Tooltip("Drawn height, metres (the sprite keeps its shape).")]
        public float height = 1.8f;
        [Tooltip("Floats this high above the floor (0 = walks).")]
        public float hoverHeight;

        [Header("Behaviour")]
        public EnemyBrainDef brain = new EnemyBrainDef();
        public EnemyAttack attack = new EnemyAttack();
        [Tooltip("Optional second attack (bosses), used with Secondary Chance.")]
        public EnemyAttack secondaryAttack = new EnemyAttack();
        [Range(0f, 1f)] public float secondaryChance;

        [Header("Death")]
        [Tooltip("Damage beyond what killed it that splatters it instead (0 = never).")]
        public float gibOverkill = 30f;
        public int score = 100;
        public List<EnemyDrop> drops = new List<EnemyDrop>();

        [Header("Boss")]
        [Tooltip("Shows a health bar while it's awake.")]
        public bool isBoss;
        public string bossTitle = "";

        [Header("Look")]
        public SpriteSheet sprite = new SpriteSheet();
        public Color tint = Color.white;

        [Header("Sounds")]
        public string alertSound = "alert";
        public string painSound = "pain";
        public string deathSound = "enemy_die";

        public char MapChar => string.IsNullOrEmpty(mapChar) ? '\0' : mapChar[0];
    }

    /// <summary>
    /// An enemy's billboard sprite sheet: 8 columns by 5 rows of equal cells. Rows from the top:
    /// walk A, walk B, attack, pain, death (death frames from the left, then gib frames). Columns
    /// are the viewing angles (see core AngleFrame): 0 front, 1 front-left, 2 left side, 3 back-left,
    /// 4 back, 5 back-right, 6 right side, 7 front-right. With Directions = 5 only columns 0-4 are
    /// drawn and 5-7 show 3-1 mirrored; with 1, column 0 is used from every side.
    /// </summary>
    [Serializable]
    public class SpriteSheet
    {
        public const int Columns = 8, Rows = 5;
        public const int WalkRowA = 0, WalkRowB = 1, AttackRow = 2, PainRow = 3, DeathRow = 4;

        public Texture2D texture;
        [Tooltip("8, 5 (the right side mirrors the left) or 1.")]
        public int directions = 8;
        [Tooltip("Death frames in the bottom row, from the left; the last one is the corpse.")]
        public int deathFrames = 4;
        [Tooltip("Gib frames in the bottom row, after the death frames.")]
        public int gibFrames = 3;
        public float walkFramesPerSecond = 5f;
        public float deathFramesPerSecond = 9f;

        /// <summary>The UV rect (offset xy, scale zw) of a cell, for the sprite shader's _Frame.</summary>
        public static Vector4 Cell(int column, int row, bool mirror)
        {
            float w = 1f / Columns, h = 1f / Rows;
            float x = column * w, y = 1f - (row + 1) * h;
            return mirror ? new Vector4(x + w, y, -w, h) : new Vector4(x, y, w, h);
        }

        /// <summary>Width / height of one cell (for sizing the billboard).</summary>
        public float CellAspect => texture == null ? 1f : (texture.width / (float)Columns) / (texture.height / (float)Rows);
    }
}
