using JamKit.Skyline;
using UnityEngine;

namespace JamKit.SkylineRepo
{
    // In its own file (Task 18 fix round 2): Unity can only save a MonoBehaviour that sits in a file named after it. Inside
    // SkylineContracts.cs the range builder's plate zones were saved as a scene-embedded script record and came back as
    // missing scripts when the scene loaded ("The referenced script (Unknown) on this Behaviour is missing!" x5 in the
    // capture log), so Writ hits on range plates never hit: no marker, no wobble, no spot.
    /// <summary>Marks a collider as part of a hittable: which zone, which plate, and whose. <see cref="owner"/> is
    /// serialized as a MonoBehaviour and cast to <see cref="ISkylineHittable"/> at hit time; an owner that is missing,
    /// destroyed or isn't one makes the hit a no-op (hit = false). <see cref="TakeHit"/> fills the hit's zone and plate
    /// from this component before passing it on.</summary>
    public sealed class SkylineHitZone : MonoBehaviour
    {
        public MonoBehaviour owner;
        public HitZone zone;
        public int plate = -1;

        public SkylineHitResult TakeHit(in SkylineHitInfo hit)
        {
            if (owner == null) return default(SkylineHitResult);      // Unity's null: also true for a destroyed owner
            var hittable = owner as ISkylineHittable;
            if (hittable == null) return default(SkylineHitResult);
            var info = hit;
            info.zone = zone;
            info.plate = plate;
            return hittable.TakeHit(in info);
        }
    }
}
