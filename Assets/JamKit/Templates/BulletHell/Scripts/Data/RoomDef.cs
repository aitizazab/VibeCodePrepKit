using System;
using System.Collections.Generic;
using JamKit.Combat;
using JamKit.Patterns;
using JamKit.Roguelite;
using UnityEngine;

namespace JamKit.BulletHell
{
    /// <summary>A fight: waves in order. Floors pick a random one from their pool for each Battle or Elite room.</summary>
    [CreateAssetMenu(menuName = "JamKit/Bullet Hell/Room", fileName = "Room")]
    public sealed class RoomDef : ScriptableObject
    {
        [Tooltip("Combat or Elite: which pool this room belongs in (checked by the validator).")]
        public RoomType type = RoomType.Combat;
        public List<WaveDef> waves = new List<WaveDef>();
        [Tooltip("Coins for clearing it.")]
        public int bonusCoins = 5;
    }
}
