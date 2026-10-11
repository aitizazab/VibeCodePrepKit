using UnityEngine;

namespace JamKit.Stealth
{
    /// <summary>
    /// A checkpoint in a stealth encounter (docs/STEALTH.md "Being caught"): its transform is the spawn point (feet, facing
    /// <see cref="yaw"/>), and the player's feet inside the <see cref="size"/> box above it reach it. The encounter's game
    /// script hands these to a core <see cref="CheckpointLog"/>, which takes one only while no guard is chasing her. Needs
    /// no collider.
    /// </summary>
    public sealed class StealthCheckpointZone : MonoBehaviour
    {
        public string label = "";
        [Tooltip("The trigger box, standing on this point (metres).")]
        public Vector3 size = new Vector3(4f, 3f, 4f);
        [Tooltip("Which way she faces when a restart puts her here (degrees).")]
        public float yaw;

        public StealthCheckpoint ToCore()
        {
            var p = transform.position;
            return new StealthCheckpoint
            {
                name = string.IsNullOrEmpty(label) ? name : label,
                center = new System.Numerics.Vector3(p.x, p.y + size.y * 0.5f, p.z),
                size = new System.Numerics.Vector3(size.x, size.y, size.z),
                spawn = new System.Numerics.Vector3(p.x, p.y, p.z),
                yaw = yaw,
            };
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 0.95f, 0.75f, 0.6f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * (size.y * 0.5f), size);
            Gizmos.DrawRay(transform.position + Vector3.up * 0.1f, Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * 1.5f);
        }
    }
}
