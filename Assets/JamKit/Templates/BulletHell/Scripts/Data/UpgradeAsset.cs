using System;
using System.Collections.Generic;
using JamKit.Combat;
using JamKit.Patterns;
using JamKit.Roguelite;
using UnityEngine;

namespace JamKit.BulletHell
{
    /// <summary>
    /// One upgrade (core UpgradeDef: id, name, rarity, stat modifiers, effect ids) plus its icon, what a
    /// Forge push adds to it, and for epics the rubric (docs/DESIGN-ROGUELITE.md, section 2): the verb it
    /// changes, the situations it's for, where the player is when it works and its in-universe idea.
    /// </summary>
    [CreateAssetMenu(menuName = "JamKit/Bullet Hell/Upgrade", fileName = "Upgrade")]
    public sealed class UpgradeAsset : ScriptableObject
    {
        public UpgradeDef def = new UpgradeDef();
        [Tooltip("2-3 letters on its HUD icon.")]
        public string iconText = "Up";
        public Color iconColor = Color.white;
        [Tooltip("Optional picture for the card and HUD (replaces the letters).")]
        public Sprite icon;

        [Header("Forge (one push adds this; empty = half of its stat modifiers)")]
        public List<StatModifier> forgeModifiers = new List<StatModifier>();
        public List<string> forgeEffects = new List<string>();
        [Tooltip("What a push does, in a few words for the Forge card (\"+10% damage\").")]
        public string forgeText = "";

        [Header("Epic rubric (epics and legendaries must fill it)")]
        public EpicRubric rubric = new EpicRubric();
        [Tooltip("Design notes: the situations it's for, why it passes the rubric, what the balance report said.")]
        [TextArea(2, 5)] public string designNotes = "";

        /// <summary>Has a forge bonus (authored, or derived from its stat modifiers).</summary>
        public bool CanForge =>
            (forgeModifiers != null && forgeModifiers.Count > 0) || (forgeEffects != null && forgeEffects.Count > 0)
            || (def != null && def.modifiers != null && def.modifiers.Count > 0);
    }
}
