using JamKit.Shell;
using UnityEngine;

namespace JamKit.Movement
{
    /// <summary>
    /// The camera's feel for <see cref="MovementController"/> (docs/MOVEMENT.md "Feel"): the field of
    /// view widens with speed, the view leans away from a wall you run on (a quick lean-in with a
    /// little overshoot on contact, the cue players time wall kicks by), rolls a touch while sliding,
    /// dips on landing in proportion to the fall, nods on a mantle and bobs while running. Every
    /// effect follows the player's comfort settings (Settings: fieldOfView, cameraRoll, headBob,
    /// motionEffects). Sits on the rig between the head and the camera; runs after the controller.
    /// </summary>
    [DefaultExecutionOrder(40)]
    public sealed class MovementCamera : MonoBehaviour
    {
        public MovementController body;
        [Tooltip("The transform this script moves (bob, roll, dip). Usually this one.")]
        public Transform rig;
        public Camera cam;

        [Header("Field of view")]
        [Tooltip("Extra vertical degrees at full speed (x Motion effects).")]
        public float speedFov = 8f;
        public float slideFov = 3f;
        public float fovFromSpeed = 9.5f;
        public float fovFullSpeed = 22f;
        public float fovRate = 6f;

        [Header("Roll")]
        [Tooltip("Lean while wallrunning, degrees (x Camera roll).")]
        public float wallRoll = 9f;
        [Tooltip("Extra lean the moment you catch a wall, settling within ~0.2 s.")]
        public float wallCatchKick = 4f;
        public float slideRoll = 2f;
        [Tooltip("Roll spring stiffness (rad/s) and damping ratio (under 1 overshoots a little).")]
        public float rollFrequency = 14f;
        public float rollDamping = 0.65f;

        [Header("Landing and bob (x Head bob)")]
        public float landDip = 0.12f;
        public float landDipFrom = 4f;
        public float landDipFull = 18f;
        public float dipFrequency = 20f;
        public float bobAmount = 0.035f;
        public float mantleNod = 3f;

        float _fov = -1f;
        float _roll, _rollVel;
        float _dip, _dipVel;
        float _bobAmp;
        float _nod;

        /// <summary>The current lean in degrees (tests, HUD).</summary>
        public float Roll => _roll;
        public float Fov => _fov;
        public float Dip => _dip;

        void Reset()
        {
            rig = transform;
            cam = GetComponentInChildren<Camera>();
        }

        void LateUpdate()
        {
            if (body == null || body.Sim == null) return;
            if (rig == null) rig = transform;
            var s = GameRoot.Exists ? GameRoot.Settings : null;
            float fovBase = s != null ? s.fieldOfView : 68f;
            float rollK = s != null ? s.cameraRoll : 1f;
            float bobK = s != null ? s.headBob : 1f;
            float motionK = s != null ? s.motionEffects : 1f;
            float dt = Time.deltaTime;
            float udt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            var sim = body.Sim;
            var mode = sim.Mode;
            var ev = body.FrameEvents;
            float speed = sim.HorizontalSpeed;

            // FOV: smoothstep of speed, plus a little while sliding.
            float kick = speedFov * MoveMath.SmoothStep(fovFromSpeed, fovFullSpeed, speed) + (mode == MoveMode.Slide ? slideFov : 0f);
            float targetFov = fovBase + kick * motionK;
            _fov = _fov < 0f ? targetFov : Mathf.Lerp(_fov, targetFov, 1f - Mathf.Exp(-fovRate * udt));
            if (cam != null) cam.fieldOfView = _fov;

            // Roll: away from the wall (wall on the right -> the top of the view tilts left), a spring so the
            // catch overshoots into a clear "lean-in".
            float targetRoll = 0f;
            if (mode == MoveMode.Wallrun) targetRoll = sim.WallSide * wallRoll;
            else if (mode == MoveMode.Slide)
            {
                Vector3 v = body.Velocity;
                float side = Vector3.Dot(v, body.transform.right);
                targetRoll = -Mathf.Clamp(side / 4f, -1f, 1f) * slideRoll;
            }
            targetRoll *= rollK;
            if (ev.wallrunStarted) _rollVel += ev.wallSide * wallCatchKick * rollFrequency * rollK;
            if (dt > 0f)
            {
                float w = rollFrequency;
                float acc = w * w * (targetRoll - _roll) - 2f * rollDamping * w * _rollVel;
                _rollVel += acc * dt;
                _roll += _rollVel * dt;
            }

            // Landing dip: a critically damped spring kicked down in proportion to the fall speed.
            if (ev.landed && !ev.slideStarted)
            {
                float amount = landDip * Mathf.Clamp01((ev.landImpact - landDipFrom) / (landDipFull - landDipFrom)) * bobK;
                _dipVel -= amount * dipFrequency * 2.718f;
            }
            if (dt > 0f)
            {
                float w = dipFrequency;
                _dipVel += (-w * w * _dip - 2f * w * _dipVel) * dt;
                _dip += _dipVel * dt;
            }

            // Head bob on the ground only, with the stride.
            bool ground = mode == MoveMode.Grounded || mode == MoveMode.Sprint;
            float speed01 = Mathf.Clamp01(speed / Mathf.Max(1f, body.Def.sprintSpeed));
            _bobAmp = Mathf.MoveTowards(_bobAmp, ground ? speed01 : 0f, udt * 4f);
            float b = bobAmount * _bobAmp * bobK;
            float ph = body.StridePhase * Mathf.PI;
            Vector3 bob = new Vector3(Mathf.Cos(ph) * b * 0.5f, Mathf.Abs(Mathf.Sin(ph)) * b - b * 0.5f, 0f);

            // A small nod down while mantling.
            float nodTarget = mode == MoveMode.Mantle ? mantleNod * bobK : 0f;
            _nod = Mathf.Lerp(_nod, nodTarget, 1f - Mathf.Exp(-14f * udt));

            rig.localPosition = bob + new Vector3(0f, _dip, 0f);
            rig.localRotation = Quaternion.Euler(_nod + _dip * -20f, 0f, _roll);
        }
    }
}
