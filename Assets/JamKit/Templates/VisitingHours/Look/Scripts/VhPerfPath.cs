using UnityEngine;

namespace JamKit.Look
{
    /// <summary>
    /// A camera path for the frame-time probe: waypoints (eye and look-at) the probe flies through at a steady pace. LookLab
    /// has one along its capture shots; World and House can add their own (<c>VhLook.AddPerfPath</c>).
    /// </summary>
    public sealed class VhPerfPath : MonoBehaviour
    {
        public Vector3[] eyes = new Vector3[0];
        public Vector3[] looks = new Vector3[0];
        [Tooltip("Seconds spent going from one waypoint to the next.")]
        public float secondsPerLeg = 4f;
        [Tooltip("Vertical field of view during the probe.")]
        public float fieldOfView = 60f;
    }
}
