using UnityEngine;

namespace JamKit.Doomlike
{
    /// <summary>Scenery: a pillar, a lamp, junk on the floor. Solid ones block walking and shots.</summary>
    [CreateAssetMenu(menuName = "JamKit/DOOM-like/Decoration", fileName = "Decoration")]
    public sealed class DecorationAsset : ScriptableObject
    {
        public string displayName = "Pillar";
        public string mapChar = "T";
        public bool solid = true;
        [Tooltip("Blocking radius when solid, metres.")]
        public float radius = 0.4f;
        public Texture2D texture;
        public Color tint = Color.white;
        [Tooltip("Drawn height, metres.")]
        public float size = 2.2f;
        [Tooltip("Ignores the level's darkness and fog (a lamp).")]
        [Range(0f, 1f)] public float glow;

        public char MapChar => string.IsNullOrEmpty(mapChar) ? '\0' : mapChar[0];
    }
}
