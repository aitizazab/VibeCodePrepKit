using JamKit.Roguelite;
using UnityEngine;

namespace JamKit.Doomlike
{
    /// <summary>One card of the between-levels draft: a core <see cref="UpgradeDef"/> (stats it
    /// changes, effect ids) plus its icon. Stats and effects: see core DoomStats.</summary>
    [CreateAssetMenu(menuName = "JamKit/DOOM-like/Upgrade", fileName = "Upgrade")]
    public sealed class DoomUpgradeAsset : ScriptableObject
    {
        public UpgradeDef def = new UpgradeDef();
        [Tooltip("Two or three letters shown on the HUD icon.")]
        public string iconText = "UP";
        public Color iconColor = new Color(1f, 0.8f, 0.3f);
    }
}
