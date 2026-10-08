using System;
using System.Collections.Generic;
using JamKit.Combat;
using JamKit.Patterns;
using JamKit.Roguelite;
using UnityEngine;

namespace JamKit.BulletHell
{
    /// <summary>One enemy type: health, look, movement, attack and rewards.</summary>
    [CreateAssetMenu(menuName = "JamKit/Bullet Hell/Enemy", fileName = "Enemy")]
    public sealed class EnemyDef : ScriptableObject
    {
        public string displayName = "Drone";
        [Min(0.1f)] public float hp = 5f;

        [Header("Look")]
        public Sprite sprite;
        public Color color = new Color(0.4f, 1f, 0.8f);
        [Tooltip("Drawn size in world units.")]
        public float size = 0.8f;
        [Tooltip("Hit-test radius for the player's shots and for touching the player.")]
        public float radius = 0.35f;
        [Tooltip("Degrees per second the sprite spins (looks only).")]
        public float spin;
        [Tooltip("Bigger death explosion and a score popup.")]
        public bool big;

        [Header("Movement")]
        public EnemyMoveDef movement = new EnemyMoveDef();

        [Header("Attack (plays once it has arrived)")]
        public PatternSequence attack = new PatternSequence();
        [Tooltip("Seconds after arriving before the first shot.")]
        public float attackDelay = 0.4f;
        [Tooltip("Touching the player hurts them.")]
        public bool hurtsOnContact = true;

        [Header("Rewards")]
        public int score = 100;
        public int coins = 1;
    }
}
