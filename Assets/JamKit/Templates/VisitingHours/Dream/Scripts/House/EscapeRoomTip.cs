using UnityEngine;

namespace JamKit.DreamEscape
{
    /// <summary>
    /// An escape room's things tipping over as she comes to it (docs/redesign/house.md "The escape"): the room's dressing
    /// (no colliders in it: the collision is always upright, tested in dotnet) waits a few degrees short of its roll, then,
    /// when the camera gets to <see cref="triggerZ"/> along the escape (in <see cref="space"/>'s frame), swings round the
    /// route's axis into its roll and settles with a small wobble. Already past <see cref="snapZ"/> when first seen (a jump
    /// to a checkpoint): it's tipped. Re-armed whenever the group is enabled again (a new run). Built in its final pose, so
    /// the editor's stills show the room as it lies. Allocates nothing per frame.
    /// </summary>
    public sealed class EscapeRoomTip : MonoBehaviour
    {
        /// <summary>The escape's root (the route runs along its +z).</summary>
        public Transform space;
        /// <summary>The roll it ends at and the roll it waits at (degrees about the route).</summary>
        public float roll, fromRoll;
        /// <summary>Along the route (space's z): start tipping here; already tipped past here when first seen.</summary>
        public float triggerZ, snapZ;
        public float seconds = 1.4f;
        /// <summary>The settle's wobble (degrees) and how long it rings.</summary>
        public float wobble = 5f, wobbleSeconds = 1.2f;

        Transform _cam;
        float _t;
        int _state;   // 0 waiting, 1 tipping, 2 settled

        void OnEnable()
        {
            _state = 0;
            _t = 0f;
            Set(fromRoll);
        }

        void Update()
        {
            if (_state == 2) return;
            if (_cam == null)
            {
                var c = Camera.main;
                if (c == null) return;
                _cam = c.transform;
            }
            if (_state == 0)
            {
                var local = space != null ? space.InverseTransformPoint(_cam.position) : _cam.position;
                // Only in the escape (the house is far above it): within 40 m of the room's height.
                bool near = space == null || Mathf.Abs(local.y - transform.localPosition.y) < 40f;
                float z = local.z;
                if (!near || z < triggerZ) return;
                if (z >= snapZ) { Set(roll); _state = 2; return; }
                _state = 1;
                _t = 0f;
            }
            _t += Time.deltaTime;
            float total = seconds + wobbleSeconds;
            if (_t >= total) { Set(roll); _state = 2; return; }
            if (_t < seconds)
            {
                float x = _t / Mathf.Max(0.05f, seconds);
                float k = 1f - (1f - x) * (1f - x) * (1f - x);
                Set(Mathf.LerpUnclamped(fromRoll, roll, k));
            }
            else
            {
                float w = _t - seconds;
                float sign = roll >= fromRoll ? 1f : -1f;
                Set(roll + sign * wobble * Mathf.Exp(-4f * w) * Mathf.Sin(w * 14f));
            }
        }

        void Set(float a) => transform.localRotation = Quaternion.Euler(0f, 0f, a);
    }
}
