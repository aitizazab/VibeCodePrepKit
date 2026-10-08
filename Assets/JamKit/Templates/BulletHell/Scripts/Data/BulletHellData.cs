using System;
using System.Collections.Generic;
using JamKit.Combat;
using JamKit.Patterns;
using JamKit.Roguelite;
using UnityEngine;

namespace JamKit.BulletHell
{
    /// <summary>How one kind of bullet looks and how big it is to the hit test. PatternDef.styleIndex
    /// (and the player's shot style) pick an entry of BulletHellConfig.bulletStyles by position.</summary>
    [Serializable]
    public class BulletStyle
    {
        public string name = "Small";
        public Color color = new Color(1f, 0.35f, 0.45f);
        [Tooltip("Hit-test radius in world units. Keep it smaller than the drawn size: dodging feels fair.")]
        public float hitRadius = 0.08f;
        [Tooltip("Drawn diameter in world units.")]
        public float size = 0.26f;
        [Tooltip("1 = round. 2 or more = a long bullet that points where it flies.")]
        public float stretch = 1f;
    }

    /// <summary>A base stat value for the player (see ShmupStats for the keys).</summary>
    [Serializable]
    public class StatEntry
    {
        public string stat = ShmupStats.Damage;
        public float value = 1f;
    }

    public enum EnterSide { Top, Left, Right, TopLeft, TopRight }

    /// <summary>A group of identical enemies in a wave.</summary>
    [Serializable]
    public class WaveSpawn
    {
        [Tooltip("Seconds after the wave starts.")]
        public float time;
        public EnemyDef enemy;
        [Tooltip("Where it flies to (for Sweep and Drift: the line it travels on). The playfield is about x -6..6, y -5..5; enemies live up top (y 1..4.5).")]
        public Vector2 position = new Vector2(0f, 3f);
        public EnterSide from = EnterSide.Top;
        [Min(1)] public int count = 1;
        [Tooltip("Offset between enemies of the group (they're centred on Position).")]
        public Vector2 spacing = new Vector2(1.5f, 0f);
        [Tooltip("Seconds between enemies of the group.")]
        public float interval = 0.25f;
        [Tooltip("Also spawn a mirror copy on the other side (x flipped).")]
        public bool mirror;
    }

    public enum BossMoveKind { Hold, Sway, Figure8, Chase, Dash }

    [Serializable]
    public class BossPhase
    {
        public string name = "Phase 1";
        [Tooltip("Starts when the boss's health drops to this fraction (the first phase: 1).")]
        [Range(0.01f, 1f)] public float startsAtHealth = 1f;
        public PatternSequence attack = new PatternSequence();
        public BossMoveKind movement = BossMoveKind.Sway;
        [Tooltip("How far it moves from its home position.")]
        public float moveWidth = 3f;
        [Tooltip("How fast it moves (cycles per second for Sway/Figure8, units per second for Chase).")]
        public float moveSpeed = 0.35f;
        public Color tint = Color.white;
    }

    [Serializable]
    public class FloorDef
    {
        public string name = "Floor 1";
        public Color background = new Color(0.05f, 0.07f, 0.12f);
        public Color accent = new Color(0.35f, 0.9f, 0.85f);
        [Tooltip("Battle rooms: one is picked at random for each Battle door.")]
        public List<RoomDef> combatRooms = new List<RoomDef>();
        public List<RoomDef> eliteRooms = new List<RoomDef>();
        public BossDef boss;
        [Tooltip("Multiplies every enemy's (and the boss's) health on this floor.")]
        public float hpScale = 1f;
        [Tooltip("Multiplies coin drops on this floor.")]
        public float coinScale = 1f;
    }
}
