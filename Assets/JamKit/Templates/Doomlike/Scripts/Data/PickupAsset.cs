using UnityEngine;

namespace JamKit.Doomlike
{
    public enum PickupKind
    {
        /// <summary>Restores Amount health (never above max).</summary>
        Health,
        /// <summary>Adds Amount of the Ammo Type.</summary>
        Ammo,
    }

    /// <summary>Something to walk over and take: health or ammo. Weapons on the floor are the
    /// weapon assets themselves (their Map Char), keys are r, b and y.</summary>
    [CreateAssetMenu(menuName = "JamKit/DOOM-like/Pickup", fileName = "Pickup")]
    public sealed class PickupAsset : ScriptableObject
    {
        public string displayName = "Medkit";
        [Tooltip("The character that places it on a map.")]
        public string mapChar = "H";
        public PickupKind kind = PickupKind.Health;
        public int amount = 25;
        [Tooltip("Ammo pickups: the ammo id (config > Ammo Types).")]
        public string ammoType = "";
        [Tooltip("Shown on the message line when taken.")]
        public string message = "Picked up a medkit.";
        public string sound = "item";
        [Tooltip("Counts toward the tally's items %.")]
        public bool countsAsItem = true;

        [Header("Look")]
        public Texture2D texture;
        public Color tint = Color.white;
        [Tooltip("Drawn height, metres.")]
        public float size = 0.7f;

        public char MapChar => string.IsNullOrEmpty(mapChar) ? '\0' : mapChar[0];
    }
}
