using UnityEngine;
using NVec3 = System.Numerics.Vector3;

namespace JamKit.Stealth
{
    /// <summary>
    /// The stealth kit's world in a Unity scene: line of sight by physics (solid, non-trigger colliders on
    /// <see cref="mask"/>; colliders under the ignored roots, such as the player and the seeker, never block; plus every
    /// <see cref="StealthSightBlocker"/>) and light
    /// from <see cref="StealthLightZone"/>s. Plain C#; no allocation per query.
    /// </summary>
    public sealed class PhysicsStealthWorld : IStealthWorld
    {
        public LayerMask mask = ~(1 << 2);
        readonly RaycastHit[] _hits = new RaycastHit[16];
        readonly Transform[] _ignore = new Transform[4];
        int _ignoreCount;

        /// <summary>Colliders under this root don't block sight (up to four roots).</summary>
        public void Ignore(Transform root)
        {
            if (root == null || _ignoreCount >= _ignore.Length) return;
            for (int i = 0; i < _ignoreCount; i++) if (_ignore[i] == root) return;
            _ignore[_ignoreCount++] = root;
        }

        public bool LineClear(NVec3 from, NVec3 to)
        {
            var a = new Vector3(from.X, from.Y, from.Z);
            var d = new Vector3(to.X - from.X, to.Y - from.Y, to.Z - from.Z);
            float len = d.magnitude;
            if (len < 1e-4f) return true;
            if (StealthSightBlocker.Blocks(from, to)) return false;
            int n = Physics.RaycastNonAlloc(a, d / len, _hits, len, mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                if (!Ignored(_hits[i].collider.transform)) return false;
            return true;
        }

        bool Ignored(Transform t)
        {
            for (int i = 0; i < _ignoreCount; i++)
                if (_ignore[i] != null && (t == _ignore[i] || t.IsChildOf(_ignore[i]))) return true;
            return false;
        }

        public float LightAt(NVec3 p) => StealthLightZone.LightAt(new Vector3(p.X, p.Y, p.Z));
    }
}
