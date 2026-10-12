using JamKit.Movement;
using UnityEngine;

namespace JamKit.DreamEscape
{
    /// <summary>
    /// The house's camera beats, on top of the movement camera (it runs after <see cref="MovementCamera"/> each frame and
    /// adds to what that set): the dolly zoom of the corridor stretching (and the hall breathing as she comes in), a roll
    /// as a room turns over, sitting down at the table (the eye drops), and a held look (a look back at the window). All
    /// additive and short; nothing here moves the player.
    /// </summary>
    [DefaultExecutionOrder(55)]
    public sealed class HouseCameraFx : MonoBehaviour
    {
        public Camera cam;
        /// <summary>The movement camera's rig (its roll and dip are set there; ours go on top).</summary>
        public Transform rig;

        float _fov, _fovFrom, _fovTo, _fovT = 1f, _fovLen = 1f;
        float _roll, _rollFrom, _rollTo, _rollT = 1f, _rollLen = 1f;
        float _sit, _sitTarget;
        bool _hold;
        Vector3 _holdTarget;

        public float FovOffset => _fov;
        public float RollOffset => _roll;
        public bool Sitting => _sitTarget > 0f;

        /// <summary>A dolly zoom out (the end of the corridor pulls away): the view widens fast, then settles back.</summary>
        public void Stretch(float degrees = 34f, float seconds = 0.9f) => FovPulse(degrees, seconds);

        /// <summary>The front hall breathing as she comes in (slower, gentler).</summary>
        public void Breathe() => FovPulse(26f, 2.6f);

        void FovPulse(float degrees, float seconds)
        {
            _fovFrom = degrees;
            _fovTo = 0f;
            _fovT = 0f;
            _fovLen = Mathf.Max(0.1f, seconds);
        }

        /// <summary>A roll: to <paramref name="degrees"/> and back over <paramref name="seconds"/>.</summary>
        public void Roll(float degrees, float seconds)
        {
            _rollFrom = degrees;
            _rollTo = 0f;
            _rollT = 0f;
            _rollLen = Mathf.Max(0.1f, seconds);
        }

        /// <summary>Sitting (the eye drops 0.45 m) or standing.</summary>
        public void Sit(bool on) => _sitTarget = on ? 0.45f : 0f;

        /// <summary>Holds the look on a point (the cut-scene beats), or lets go.</summary>
        public void Hold(bool on, Vector3 at = default)
        {
            _hold = on;
            _holdTarget = at;
        }

        public void ResetAll()
        {
            _fov = 0f; _fovT = 1f;
            _roll = 0f; _rollT = 1f;
            _sit = 0f; _sitTarget = 0f;
            _hold = false;
        }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            // The zoom: up fast (a third of the time), down slow.
            if (_fovT < 1f)
            {
                _fovT = Mathf.Min(1f, _fovT + dt / _fovLen);
                float k = _fovT < 0.3f ? Mathf.SmoothStep(0f, 1f, _fovT / 0.3f) : 1f - Mathf.SmoothStep(0f, 1f, (_fovT - 0.3f) / 0.7f);
                _fov = Mathf.Lerp(_fovTo, _fovFrom, k);
            }
            else _fov = 0f;
            if (_rollT < 1f)
            {
                _rollT = Mathf.Min(1f, _rollT + dt / _rollLen);
                float k = _rollT < 0.25f ? Mathf.SmoothStep(0f, 1f, _rollT / 0.25f) : 1f - Mathf.SmoothStep(0f, 1f, (_rollT - 0.25f) / 0.75f);
                _roll = Mathf.Lerp(_rollTo, _rollFrom, k);
            }
            else _roll = 0f;
            _sit = Mathf.MoveTowards(_sit, _sitTarget, dt * 0.9f);
            if (cam != null && Mathf.Abs(_fov) > 0.01f) cam.fieldOfView = Mathf.Clamp(cam.fieldOfView + _fov, 20f, 140f);
            if (rig != null)
            {
                if (Mathf.Abs(_roll) > 0.01f) rig.localRotation *= Quaternion.Euler(0f, 0f, _roll);
                if (_sit > 0.001f) rig.localPosition = new Vector3(rig.localPosition.x, -_sit, rig.localPosition.z);
                else if (Mathf.Abs(rig.localPosition.y) > 0.0001f && _sitTarget <= 0f) rig.localPosition = new Vector3(rig.localPosition.x, 0f, rig.localPosition.z);
                if (_hold)
                {
                    var d = _holdTarget - rig.position;
                    if (d.sqrMagnitude > 0.01f) rig.rotation = Quaternion.Slerp(rig.rotation, Quaternion.LookRotation(d, Vector3.up), 1f - Mathf.Exp(-dt * 4f));
                }
            }
        }
    }
}
