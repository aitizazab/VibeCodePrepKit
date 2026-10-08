using System;
using System.Collections.Generic;
using JamKit.Combat;
using JamKit.Patterns;
using JamKit.Roguelite;
using UnityEngine;

namespace JamKit.BulletHell
{
    /// <summary>One upgrade (core UpgradeDef: id, name, rarity, stat modifiers, effect ids) plus its icon.</summary>
    [CreateAssetMenu(menuName = "JamKit/Bullet Hell/Upgrade", fileName = "Upgrade")]
    public sealed class UpgradeAsset : ScriptableObject
    {
        public UpgradeDef def = new UpgradeDef();
        [Tooltip("2-3 letters on its HUD icon.")]
        public string iconText = "Up";
        public Color iconColor = Color.white;
        [Tooltip("Optional picture for the card and HUD (replaces the letters).")]
        public Sprite icon;
    }
}
