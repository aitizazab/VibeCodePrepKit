using JamKit.Skyline;
using UnityEngine;
using NVec3 = System.Numerics.Vector3;

namespace JamKit.SkylineRepo
{
    /// <summary>A firing-range plate: box colliders with SkylineHitZones (the board, and a bullseye that counts as a weak point,
    /// so the Writ leaves and consumes spots here too). A hit knocks it back into a short wobble that settles within about
    /// 0.25 s, so it is always standing for the next shot (the showcase fires 0.34-1.0 s apart) and the Writ's spot marker
    /// shows. Always an aim-assist candidate. It never breaks and has no health (no numbers on screen).</summary>
    public sealed class SkylineTargetPlate : MonoBehaviour, ISkylineHittable, ISkylineAimable
    {
        /// <summary>How far a hit knocks the board back (degrees) and how long the wobble takes to settle (s).</summary>
        const float WobbleAngle = 18f, WobbleSeconds = 0.25f, WobbleCycles = 1.5f;

        [Tooltip("The part that tips (rotates about this object's X axis, at its base).")]
        public Transform hinge;
        public string id = "plate";
        [Tooltip("The board's centre, relative to this object (for aim assist).")]
        public Vector3 centerOffset = new Vector3(0f, 1.2f, 0f);
        public float radius = 0.45f;

        readonly WeakSpots _spots = new WeakSpots();
        readonly WritTuning _writ = new WritTuning();
        float _wobbleLeft;

        public string Id => id;
        public Vector3 Center => transform.TransformPoint(centerOffset);
        public float Radius => radius;
        public bool Alive => true;

        void OnEnable() { if (!SkylineTargets.Aimables.Contains(this)) SkylineTargets.Aimables.Add(this); }
        void OnDisable() => SkylineTargets.Aimables.Remove(this);

        public SkylineHitResult TakeHit(in SkylineHitInfo hit)
        {
            var r = new SkylineHitResult { hit = true };
            Vector3 local = (hinge != null ? hinge : transform).InverseTransformPoint(hit.point);
            r.writ = Writ.Resolve(_spots, hit.zone, new NVec3(local.x, local.y, local.z), Time.time, hit.fromWrit,
                hit.gun != null ? hit.gun.weakPointMultiplier : 1f, _writ);
            r.damage = new BotDamage { toChassis = hit.damage * r.writ.multiplier, plateShed = -1 };
            _wobbleLeft = WobbleSeconds;
            return r;
        }

        void Update()
        {
            if (hinge == null || _wobbleLeft <= 0f) return;
            _wobbleLeft = Mathf.Max(0f, _wobbleLeft - Time.deltaTime);
            float k = _wobbleLeft / WobbleSeconds;                    // 1 at the hit, 0 settled
            float angle = WobbleAngle * k * Mathf.Cos((1f - k) * WobbleCycles * Mathf.PI * 2f);
            hinge.localRotation = Quaternion.Euler(angle, 0f, 0f);
        }
    }
}
