using System.Collections.Generic;
using UnityEngine;
using NVec3 = System.Numerics.Vector3;

namespace JamKit.Stealth
{
    /// <summary>
    /// A box that blocks seekers' sight but not movement: sheets on a washing line, grill smoke, a hedge you can push
    /// through. Needs no collider; <see cref="PhysicsStealthWorld"/> checks every enabled one. Axis-aligned in the world,
    /// centred on this object.
    /// </summary>
    public sealed class StealthSightBlocker : MonoBehaviour
    {
        public Vector3 size = new Vector3(4f, 1.8f, 0.1f);

        static readonly List<StealthSightBlocker> _all = new List<StealthSightBlocker>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _all.Clear();

        void OnEnable() { if (!_all.Contains(this)) _all.Add(this); }
        void OnDisable() => _all.Remove(this);

        /// <summary>True when an enabled blocker lies across the segment.</summary>
        public static bool Blocks(NVec3 from, NVec3 to)
        {
            for (int i = 0; i < _all.Count; i++)
            {
                var b = _all[i];
                if (b == null || !b.isActiveAndEnabled) continue;
                var c = b.transform.position;
                var box = StealthBoxWorld.Box.Of(new NVec3(c.x, c.y, c.z), new NVec3(b.size.x, b.size.y, b.size.z));
                if (StealthBoxWorld.SegmentHits(from, to, box)) return true;
            }
            return false;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.9f, 0.9f, 1f, 0.5f);
            Gizmos.DrawWireCube(transform.position, size);
        }
    }
}
