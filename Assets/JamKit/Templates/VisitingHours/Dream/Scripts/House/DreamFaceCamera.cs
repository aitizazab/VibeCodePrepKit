using UnityEngine;

namespace JamKit.DreamEscape
{
    /// <summary>A cut-out (Theo on his seeker, Lena at the stove) that turns about its up axis to face the camera.</summary>
    public sealed class DreamFaceCamera : MonoBehaviour
    {
        Camera _cam;

        void LateUpdate()
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;
            var d = _cam.transform.position - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude < 1e-4f) return;
            transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
        }
    }
}
