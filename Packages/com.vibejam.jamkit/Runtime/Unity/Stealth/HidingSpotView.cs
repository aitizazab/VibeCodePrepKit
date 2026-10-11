using System.Collections.Generic;
using UnityEngine;
using NVec3 = System.Numerics.Vector3;

namespace JamKit.Stealth
{
    /// <summary>
    /// A hiding spot in a scene (core <see cref="HidingSpot"/>): a box centred on this object (axis-aligned in the world)
    /// that hides whoever stands in it, and the point a seeker walks to when it checks the spot. Bush: hard to see
    /// unless the seeker is right next to it. Under and Closet: unseen, but a searching seeker opens it.
    /// Every enabled spot is in <see cref="Spots"/> (the list seekers and the player share, so an index means the same
    /// spot to both). Spots don't move once enabled.
    /// </summary>
    public sealed class HidingSpotView : MonoBehaviour
    {
        public HideKind kind = HideKind.Bush;
        public Vector3 size = new Vector3(2f, 1.6f, 2f);
        [Tooltip("Where a seeker stands to check the spot (on its floor). Empty: 1.2 m out from the box, behind this object (along -forward).")]
        public Transform approach;

        static readonly List<HidingSpotView> _views = new List<HidingSpotView>();
        static readonly List<HidingSpot> _spots = new List<HidingSpot>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _views.Clear();
            _spots.Clear();
        }

        /// <summary>Every enabled hiding spot (core data), in the same order as <see cref="Views"/>.</summary>
        public static List<HidingSpot> Spots => _spots;
        public static IReadOnlyList<HidingSpotView> Views => _views;

        /// <summary>The index of the spot around a point (feet), -1 for none.</summary>
        public static int IndexAt(Vector3 feet)
        {
            var p = new NVec3(feet.x, feet.y, feet.z);
            for (int i = 0; i < _spots.Count; i++) if (_spots[i].Contains(p)) return i;
            return -1;
        }

        void OnEnable()
        {
            if (_views.Contains(this)) return;
            _views.Add(this);
            _spots.Add(ToSpot());
        }

        void OnDisable()
        {
            int i = _views.IndexOf(this);
            if (i < 0) return;
            _views.RemoveAt(i);
            _spots.RemoveAt(i);
        }

        public HidingSpot ToSpot()
        {
            var c = transform.position;
            var a = approach != null ? approach.position : c - transform.forward * (size.z * 0.5f + 1.2f);
            return HidingSpot.Box(name, kind, new NVec3(c.x, c.y, c.z), new NVec3(size.x, size.y, size.z), new NVec3(a.x, a.y, a.z));
        }

        void OnDrawGizmos()
        {
            Gizmos.color = kind == HideKind.Bush ? new Color(0.3f, 0.8f, 0.3f, 0.6f) : new Color(0.6f, 0.4f, 0.9f, 0.6f);
            Gizmos.DrawWireCube(transform.position, size);
            if (approach != null) Gizmos.DrawLine(transform.position, approach.position);
        }
    }
}
