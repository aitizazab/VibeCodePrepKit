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

        [Header("Sprint (hold Shift / LB)")]
        [Tooltip("Speed multiplier, stamina bar, recharge, and the seconds of sprint each fight room allows. " +
                 "Upgrades scale them with the stats sprint_speed, stamina_max, stamina_regen, room_sprint_budget.")]
        public SprintDef sprint = new SprintDef();

        [Header("Focus (off by default)")]
        [Tooltip("The classic shmup Focus: slower movement, a tighter volley and the hitbox shown while held. Off by " +
                 "default (Shift is Sprint). To use it, tick this and give the Focus action a key in JamKitControls.inputactions.")]
        public bool focusEnabled;
        [Tooltip("Speed while Focus is held.")]
        public float focusSpeed = 3.2f;

        [Header("Body")]
        public Sprite sprite;
        public Color color = new Color(0.55f, 0.95f, 1f);
        public float size = 0.75f;
        [Tooltip("Only this tiny circle can be hit (always shown; with Focus on, only while focusing).")]
        public float hitboxRadius = 0.07f;
        [Tooltip("Bullets passing inside this ring (but missing) count as grazes.")]
        public float grazeRadius = 0.6f;

        [Header("Lives and bombs")]
        [Min(1)] public int lives = 3;
        [Tooltip("Bombs in the starting kit (0: bombs come from the Bomb Bay epic). Refilled to this after losing a life.")]
        public int bombsPerLife = 0;
        [Tooltip("Most bombs the ship can hold, Bay and upgrades included.")]
        public int maxBombs = 6;
        [Tooltip("Bombs the Bomb Bay epic holds (each extra Bay stack and each Spare Bomb adds one).")]
        public int bayBombs = 2;
        [Tooltip("Seconds for the Bay to recharge one bomb while fighting (the stat bomb_recharge speeds it up).")]
        public float bombRechargeSeconds = 20f;
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
        public float homingTurnRate = 4f;

        [Header("Base stats (override the defaults: damage 1, fire_rate 1, ...)")]
        public List<StatEntry> baseStats = new List<StatEntry>();
    }
}
