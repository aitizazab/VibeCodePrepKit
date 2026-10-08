using System;
using System.Collections.Generic;
using JamKit.Combat;
using JamKit.Patterns;
using JamKit.Roguelite;
using UnityEngine;

namespace JamKit.BulletHell
{
    /// <summary>The player's ship: movement, body, lives, bombs and gun.</summary>
    [CreateAssetMenu(menuName = "JamKit/Bullet Hell/Player", fileName = "Player")]
    public sealed class PlayerDef : ScriptableObject
    {
        [Header("Movement (world units per second)")]
        public float moveSpeed = 7.5f;
        [Tooltip("Speed while Focus (Shift / LB) is held.")]
        public float focusSpeed = 3.2f;

        [Header("Body")]
        public Sprite sprite;
        public Color color = new Color(0.55f, 0.95f, 1f);
        public float size = 0.75f;
        [Tooltip("Only this tiny circle can be hit (shown while focusing).")]
        public float hitboxRadius = 0.07f;
        [Tooltip("Bullets passing inside this ring (but missing) count as grazes.")]
        public float grazeRadius = 0.6f;

        [Header("Lives and bombs")]
        [Min(1)] public int lives = 3;
        [Tooltip("Bombs at the start, and refilled to this after losing a life.")]
        public int bombsPerLife = 2;
        public int maxBombs = 5;
        public float invulnerableAfterHit = 2f;
        public float bombInvulnerable = 2.5f;
        [Tooltip("Seconds the bomb keeps erasing bullets.")]
        public float bombDuration = 1f;
        public float bombRadius = 5f;
        public float bombDamage = 40f;

        [Header("Gun")]
        public WeaponDef weapon = new WeaponDef
        {
            magazineSize = 0,
            infiniteReserve = true,
            startingReserve = 0,
            fireMode = FireMode.Auto,
            fireInterval = 0.085f,
        };
        [Tooltip("Fire without holding a button (friendlier for first-time players).")]
        public bool autoFire = true;
        public float shotSpeed = 20f;
        public float shotDamage = 1f;
        public float shotLifetime = 1.5f;
        [Tooltip("Index into the config's bullet styles.")]
        public int shotStyle = 6;
        public int homingShotStyle = 7;
        [Tooltip("Radians per second a homing shot can turn.")]
        public float homingTurnRate = 7f;

        [Header("Base stats (override the defaults: damage 1, fire_rate 1, ...)")]
        public List<StatEntry> baseStats = new List<StatEntry>();
    }
}
