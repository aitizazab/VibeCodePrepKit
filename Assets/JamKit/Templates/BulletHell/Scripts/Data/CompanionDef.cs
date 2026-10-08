using UnityEngine;

namespace JamKit.BulletHell
{
    /// <summary>
    /// The companion in battle (docs/DESIGN-ROGUELITE.md, section 6): it orbits the ship and shoots
    /// (weaker than the ship), takes one bullet meant for you on a cooldown (its ring glows when it's
    /// ready) and calls out the boss's next pattern with a short bark from ink
    /// (companion_bark_&lt;boss&gt;_&lt;shape&gt;, companion_bark_&lt;shape&gt;, companion_bark_guard). The Rest
    /// room's line (rest) is its conversation between battles. Turn it off in BulletHellConfig.
    /// Upgrades can target it with the stats companion_damage and companion_shield_cooldown.
    /// </summary>
    [CreateAssetMenu(menuName = "JamKit/Bullet Hell/Companion", fileName = "Companion")]
    public sealed class CompanionDef : ScriptableObject
    {
        [Tooltip("Shown with its barks (and the name ink's companion_name() returns).")]
        public string displayName = "Companion";
        public Sprite sprite;
        public Color color = new Color(1f, 0.85f, 0.55f);
        public float size = 0.42f;
        [Tooltip("Index into the config's bullet styles for its shots.")]
        public int shotStyle = 7;
        [Tooltip("Orbit, gun, guard cooldown and bark timing.")]
        public CompanionRules rules = new CompanionRules();
    }
}
