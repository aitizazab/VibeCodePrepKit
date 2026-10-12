using JamKit.Shell;
using UnityEngine;

namespace JamKit.Movement
{
    /// <summary>
    /// The camera's feel for <see cref="MovementController"/> (docs/MOVEMENT.md "Feel"): the field of
    /// view widens with speed (and x1.1 while sliding), the view leans away from a wall you run on (a quick lean-in with a
    /// little overshoot on contact, the cue players time wall kicks by), rolls a touch while sliding,
    /// dips sharply as a slide starts and on landing in proportion to the fall, nods on a mantle and bobs while running, and
    /// punches out on the boom (crossing into flying, from <see cref="SpeedFeel"/>; never a roll or a shake), and rolls a
    /// few degrees toward a peek (<see cref="MovementController.LeanRoll"/>). Every
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
        [Tooltip("Extra degrees while sliding (0: the slide widens by slideFovScale instead).")]
        public float slideFov = 0f;
        [Tooltip("Sliding widens the view x this (Titanfall 2's slideFOVScale 1.1), in and out over these times (s), x Motion effects.")]
        public float slideFovScale = 1.1f, slideFovIn = 0.25f, slideFovOut = 0.25f;
        public float fovFromSpeed = 9.5f;
        public float fovFullSpeed = 22f;
        public float fovRate = 6f;
        [Tooltip("The sprint kick (the push-off back to running): a short FOV punch, degrees (x Motion effects), and how long it lasts.")]
        public float sprintKickFov = 4f;
        public float sprintKickTime = 0.35f;
        [Tooltip("The boom (docs/SPEED-FEEL.md V1): crossing into flying punches the FOV ~+6 degrees, settling in ~0.4 s, on top of the speed FOV (numbers in the tuning's speedFeel, x Motion effects). Empty: no boom.")]
        public SpeedFeel speedFeel;

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
        [Tooltip("The sudden dip as a slide starts (m, x Head bob): Titanfall 2's 'camera will do a sudden dip'.")]
        public float slideEntryDip = 0.05f;
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
        float _kickT = -1f;
        float _slideK;
        float _boomFov;
        float _leanRoll;

        /// <summary>The current lean in degrees (tests, HUD).</summary>
        public float Roll => _roll;
        public float Fov => _fov;
        public float Dip => _dip;
        /// <summary>The peek's roll this frame, degrees (z: + tilts the view's top left; a right lean is negative).</summary>
        public float LeanRoll => _leanRoll;
        /// <summary>The boom's FOV punch this frame, degrees (tests).</summary>
        public float BoomFov => _boomFov;

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
            // The sprint kick: a quick punch out and back.
            if (ev.sprintKick) _kickT = 0f;
            float punch = 0f;
            if (_kickT >= 0f)
            {
                _kickT += udt;
                const float rise = 0.06f;
                punch = _kickT < rise ? _kickT / rise : 1f - (_kickT - rise) / Mathf.Max(0.01f, sprintKickTime - rise);
                if (punch <= 0f) { punch = 0f; _kickT = -1f; }
            }
            // The slide widens the view x1.1, in and out over 0.25 s.
            bool sliding = mode == MoveMode.Slide;
            _slideK = Mathf.MoveTowards(_slideK, sliding ? 1f : 0f, udt / Mathf.Max(0.01f, sliding ? slideFovIn : slideFovOut));
            float slideScale = Mathf.Lerp(1f, slideFovScale, MoveMath.SmoothStep(0f, 1f, _slideK) * motionK);
            // The boom: SpeedFeel's punch, already x Motion effects.
            float boom = speedFeel != null ? speedFeel.Levels.boomFov : 0f;
            _boomFov = boom;
            if (cam != null) cam.fieldOfView = (_fov + sprintKickFov * punch * motionK + boom) * slideScale;

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
            MoveMath.Spring(ref _roll, ref _rollVel, targetRoll, rollFrequency, rollDamping, dt);

            // Landing dip: a critically damped spring kicked down in proportion to the fall speed.
            if (ev.slideStarted) _dipVel -= slideEntryDip * dipFrequency * 2.718f * bobK;
            if (ev.landed && !ev.slideStarted)
            {
                float amount = landDip * Mathf.Clamp01((ev.landImpact - landDipFrom) / (landDipFull - landDipFrom)) * bobK;
                _dipVel -= amount * dipFrequency * 2.718f;
            }
            // Stepped exactly: a long frame on a hard landing used to fling the camera up to 0.3 m off the body (out of the
            // head's cut-away, 2026-10-12).
            MoveMath.CriticalSpring(ref _dip, ref _dipVel, 0f, dipFrequency, dt);

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

            // The peek rolls the view a few degrees toward the lean (the controller slides the eyes): top of the view toward
            // the lean, x Camera roll and Motion effects, none with Reduce effects.
            _leanRoll = (s != null && s.reduceEffects) ? 0f : -body.LeanRoll * rollK * motionK;

            rig.localPosition = bob + new Vector3(0f, _dip, 0f);
            rig.localRotation = Quaternion.Euler(_nod + _dip * -20f, 0f, _roll + _leanRoll);
        }
    }
}
