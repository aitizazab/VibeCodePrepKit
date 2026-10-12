using UnityEngine;

namespace JamKit.DreamEscape
{
    /// <summary>
    /// Where one of Larch Hill's Saturday people stands and what they do (docs/redesign/world.md "Saturday's life"): the
    /// People agent's character prefabs go here (the prefab named by <see cref="role"/>, playing the animation named by
    /// <see cref="activity"/>, walking <see cref="path"/> on the loop if it has one). Until they land the hill shows
    /// cut-outs where it has art and nothing where it doesn't. Draws a gizmo in the editor.
    /// </summary>
    public sealed class HillPersonSpot : MonoBehaviour
    {
        [Tooltip("Who stands here (a People agent prefab id, e.g. \"okonkwo\", \"kid_bike_1\", \"party_3\").")]
        public string role = "";
        [Tooltip("What they do, as an animation name (WashCar, Mow, YardSale, RideBike, Garden, WaterRoses, SitOnWall, Barbecue, Dance, ChatStanding, SitAtTable, Stall, SitInBus).")]
        public string activity = "";
        [Tooltip("A loop they walk or ride, in world points (empty: they stay put).")]
        public Vector3[] path = new Vector3[0];
        [Tooltip("Seconds one lap of the path takes (the loop repeats exactly, like everything else on the hill).")]
        public float lapSeconds = 24f;

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.7f, 0.3f, 0.8f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.85f, new Vector3(0.5f, 1.7f, 0.5f));
            Gizmos.DrawLine(transform.position + Vector3.up * 1.5f, transform.position + Vector3.up * 1.5f + transform.forward * 0.6f);
            for (int i = 0; i + 1 < path.Length; i++) Gizmos.DrawLine(path[i], path[i + 1]);
        }
    }
}
