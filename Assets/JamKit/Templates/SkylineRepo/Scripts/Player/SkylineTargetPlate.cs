using JamKit.Skyline;
using UnityEngine;
using NVec3 = System.Numerics.Vector3;

namespace JamKit.SkylineRepo
{
    /// <summary>A firing-range plate: box colliders with SkylineHitZones (the board, and a bullseye that counts as a weak point,
    /// so the Writ leaves and consumes spots here too), tipping over backwards when hit and standing back up a moment later.
    /// It is also an aim-assist candidate while standing. It never breaks and has no health (no numbers on screen).</summary>
    public sealed class SkylineTargetPlate : MonoBehaviour, ISkylineHittable, ISkylineAimable
    {
        const float TipAngle = 80f, TipSpeed = 900f, RiseSpeed = 240f, DownSeconds = 0.8f;

        [Tooltip("The part that tips (rotates about this object's X axis, at its base).")]
        public Transform hinge;
        public string id = "plate";
        [Tooltip("The board's centre, relative to this object (for aim assist).")]
        public Vector3 centerOffset = new Vector3(0f, 1.2f, 0f);
        public float radius = 0.45f;

        readonly WeakSpots _spots = new WeakSpots();
        readonly WritTuning _writ = new WritTuning();
        float _angle, _downFor;
        bool _tipping;

        public string Id => id;
        public Vector3 Center => transform.TransformPoint(centerOffset);
        public float Radius => radius;
        public bool Alive => !_tipping && _angle < 1f;

        void OnEnable() { if (!SkylineTargets.Aimables.Contains(this)) SkylineTargets.Aimables.Add(this); }
        void OnDisable() => SkylineTargets.Aimables.Remove(this);

        public SkylineHitResult TakeHit(in SkylineHitInfo hit)
        {
            var r = new SkylineHitResult { hit = true };
            Vector3 local = (hinge != null ? hinge : transform).InverseTransformPoint(hit.point);
            r.writ = Writ.Resolve(_spots, hit.zone, new NVec3(local.x, local.y, local.z), Time.time, hit.fromWrit,
                hit.gun != null ? hit.gun.weakPointMultiplier : 1f, _writ);
            r.damage = new BotDamage { toChassis = hit.damage * r.writ.multiplier, plateShed = -1 };
            _tipping = true;
            _downFor = DownSeconds;
            return r;
        }

        void Update()
        {
            if (hinge == null) return;
            float dt = Time.deltaTime;
            if (_tipping)
            {
                _angle = Mathf.MoveTowards(_angle, TipAngle, TipSpeed * dt);
                if (_angle >= TipAngle)
                {
                    _downFor -= dt;
                    if (_downFor <= 0f) _tipping = false;
                }
            }
            else if (_angle > 0f) _angle = Mathf.MoveTowards(_angle, 0f, RiseSpeed * dt);
            hinge.localRotation = Quaternion.Euler(_angle, 0f, 0f);
        }
    }
}
