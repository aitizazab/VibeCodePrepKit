using UnityEngine;

namespace JamKit.SkylineRepo
{
    /// <summary>Where an encounter starts (docs/SKYLINE-PLAN-M1.md Task 16): an axis-aligned box in world space (this object's
    /// position is its centre, <see cref="size"/> its extent). No collider: the <see cref="SkylineArenaDirector"/> checks the
    /// player's feet against it each frame, so the zone never blocks, never needs a rigidbody, and never trips the lint.
    /// <see cref="encounterIndex"/> indexes <c>GalleriaArena.Encounters()</c>; the director arms the zones in that order.</summary>
    public sealed class SkylineEncounterZone : MonoBehaviour
    {
        [Tooltip("Index into GalleriaArena.Encounters(): 0 food-court, 1 facade, 2 skylight (-1: the lift's exit zone).")]
        public int encounterIndex;
        public string encounterId = "";
        [Tooltip("World-space size of the box (metres), centred on this object.")]
        public Vector3 size = new Vector3(10f, 10f, 10f);

        public Bounds Bounds => new Bounds(transform.position, size);

        public bool Contains(Vector3 point) => Bounds.Contains(point);

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.35f);
            Gizmos.DrawWireCube(transform.position, size);
        }
    }
}
