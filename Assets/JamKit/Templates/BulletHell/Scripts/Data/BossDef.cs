using System;
using System.Collections.Generic;
using JamKit.Combat;
using JamKit.Patterns;
using JamKit.Roguelite;
using UnityEngine;

namespace JamKit.BulletHell
{
    /// <summary>A floor's boss: health, look and phases (each a pattern sequence and a movement).</summary>
    [CreateAssetMenu(menuName = "JamKit/Bullet Hell/Boss", fileName = "Boss")]
    public sealed class BossDef : ScriptableObject
    {
        public string displayName = "Gatekeeper";
        [Tooltip("The line under the name on its intro card.")]
        public string title = "Warden of the first floor";
        [Min(1f)] public float hp = 900f;

        [Header("Look")]
        public Sprite sprite;
        public Color color = new Color(1f, 0.55f, 0.4f);
        public float size = 2.4f;
        public float radius = 0.95f;
        public Vector2 homePosition = new Vector2(0f, 3.1f);

        [Header("Phases (in order; each starts at its health fraction)")]
        public List<BossPhase> phases = new List<BossPhase>();
        [Tooltip("Seconds it can't be hurt after changing phase (its bullets are cleared too).")]
        public float phaseChangeInvulnerable = 1.5f;

        [Header("Rewards")]
        public int score = 10000;
        public int coins = 40;
    }
}
