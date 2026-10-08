using UnityEngine;

namespace JamKit.Shell
{
    /// <summary>
    /// Applies the shell's screen shake to this camera (added to the main camera automatically on the
    /// first shake). Runs late and undoes its own offset next frame, and if something else (a follow
    /// script, a Cinemachine brain) moved the camera in between, it leaves that position alone.
    /// For a first-person camera, put it on a child of the head so the offset doesn't fight mouse look.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(10000)]
    public sealed class CameraShaker : MonoBehaviour
    {
        [Tooltip("Multiplies the shake offset (world units at full trauma for a 2D camera).")]
        public float strength = 1f;
        [Tooltip("Also roll the camera a little.")]
        public bool roll = true;

        static CameraShaker _current;
        Vector3 _offset;
        Vector3 _writtenPosition;
        Quaternion _rollApplied = Quaternion.identity;
        Quaternion _writtenRotation;
        bool _applied;

        public static CameraShaker Current => _current != null && _current.isActiveAndEnabled ? _current : null;

        void OnEnable() => _current = this;

        void OnDisable()
        {
            Undo();
            if (_current == this) _current = null;
        }

        void LateUpdate()
        {
            Undo();
            if (!GameRoot.Exists) return;
            var s = GameRoot.Juice.CurrentShake;
            if (s.offset.X == 0f && s.offset.Y == 0f && s.roll == 0f) return;
            _offset = new Vector3(s.offset.X, s.offset.Y, 0f) * strength;
            _rollApplied = roll ? Quaternion.Euler(0f, 0f, s.roll) : Quaternion.identity;
            var t = transform;
            t.localPosition += t.localRotation * _offset;
            t.localRotation *= _rollApplied;
            _writtenPosition = t.localPosition;
            _writtenRotation = t.localRotation;
            _applied = true;
        }

        void Undo()
        {
            if (!_applied) return;
            _applied = false;
            var t = transform;
            // Only take back what we added if nobody else has set the transform since.
            if (t.localRotation == _writtenRotation) t.localRotation *= Quaternion.Inverse(_rollApplied);
            if (t.localPosition == _writtenPosition) t.localPosition -= t.localRotation * _offset;
        }

        public static void EnsureOnMainCamera()
        {
            if (Current != null) return;
            var cam = Camera.main;
            if (cam != null && cam.GetComponent<CameraShaker>() == null) cam.gameObject.AddComponent<CameraShaker>();
        }
    }
}
