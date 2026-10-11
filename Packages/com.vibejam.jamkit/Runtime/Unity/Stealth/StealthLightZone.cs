using System.Collections.Generic;
using UnityEngine;

namespace JamKit.Stealth
{
    /// <summary>
    /// A box of light or shade for the stealth kit's sight (0 dark, 1 lit): stand in a dark one and seekers see you
    /// about a quarter as well (<see cref="StealthDef.shadowScale"/>). Where zones overlap the smallest one wins; outside
    /// every zone the light is <see cref="Ambient"/>. Purely a gameplay volume: light the scene to match.
    /// </summary>
    public sealed class StealthLightZone : MonoBehaviour
    {
        [Range(0f, 1f)] public float level = 0.2f;
        [Tooltip("The box, in this object's local space, centred on it.")]
        public Vector3 size = new Vector3(6f, 4f, 6f);

        static readonly List<StealthLightZone> _all = new List<StealthLightZone>();
        static float _ambient = 0.75f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _all.Clear();
            _ambient = 0.75f;
        }

        /// <summary>The light outside every zone (a scene sets it; 0.75 by default).</summary>
        public static float Ambient
        {
            get => _ambient;
            set => _ambient = Mathf.Clamp01(value);
        }

        void OnEnable() { if (!_all.Contains(this)) _all.Add(this); }
        void OnDisable() => _all.Remove(this);

        public bool Contains(Vector3 world)
        {
            var p = transform.InverseTransformPoint(world);
            var h = size * 0.5f;
            return Mathf.Abs(p.x) <= h.x && Mathf.Abs(p.y) <= h.y && Mathf.Abs(p.z) <= h.z;
        }

        float Volume => Mathf.Abs(size.x * size.y * size.z * transform.lossyScale.x * transform.lossyScale.y * transform.lossyScale.z);

        /// <summary>The light at a point: the smallest zone around it, else the ambient.</summary>
        public static float LightAt(Vector3 world)
        {
            StealthLightZone best = null;
            float bv = float.MaxValue;
            for (int i = 0; i < _all.Count; i++)
            {
                var z = _all[i];
                if (z == null || !z.Contains(world)) continue;
                float v = z.Volume;
                if (v < bv) { bv = v; best = z; }
            }
            return best != null ? best.level : _ambient;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = Color.Lerp(new Color(0.1f, 0.1f, 0.3f, 0.5f), new Color(1f, 0.9f, 0.4f, 0.5f), level);
            Gizmos.DrawWireCube(Vector3.zero, size);
        }
    }
}
