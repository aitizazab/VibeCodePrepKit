using UnityEngine;

namespace JamKit.DreamEscape
{
    /// <summary>
    /// A loose thing in the escape moving on its own: a pendant lamp swinging on its cord, a picture frame rocking on its
    /// nail, the attic's things drifting over the stars (a slow turn and a bob). Pure decoration (no colliders); allocates
    /// nothing per frame.
    /// </summary>
    public sealed class EscapeSwing : MonoBehaviour
    {
        /// <summary>The swing's axis (local), its amplitude (degrees), its period (seconds) and phase (0..1).</summary>
        public Vector3 axis = Vector3.right;
        public float amplitude = 7f, period = 2.8f, phase;
        /// <summary>A steady turn (degrees a second, local) and a bob (metres along local up, over <see cref="period"/>).</summary>
        public Vector3 spin;
        public float bob;

        Quaternion _base;
        Vector3 _basePos;
        bool _init;

        void OnEnable()
        {
            if (_init) return;
            _base = transform.localRotation;
            _basePos = transform.localPosition;
            _init = true;
        }

        void Update()
        {
            float t = Time.time;
            float w = (t / Mathf.Max(0.1f, period) + phase) * 6.2831853f;
            var r = _base * Quaternion.AngleAxis(amplitude * Mathf.Sin(w), axis);
            if (spin.sqrMagnitude > 0f) r = r * Quaternion.Euler(spin * t);
            transform.localRotation = r;
            if (bob != 0f) transform.localPosition = _basePos + Vector3.up * (bob * Mathf.Sin(w * 0.5f));
        }
    }
}
