using UnityEngine;
using UnityEngine.InputSystem;

namespace JamKit.SkylineRepo
{
    /// <summary>Reads the keyboard, the mouse and the pad straight from the Input System (Skyline's scenes have no GameRoot).
    /// Keyboard and mouse: WASD, mouse look, Space jump, Left Ctrl or C crouch, Left Shift sprint, left button fire, R reload,
    /// Q or the wheel swap, F inspect. Pad: left stick move, right stick look (the same response curve as MovementController),
    /// A jump, B crouch, L3 sprint, right trigger fire (past 0.5), X reload, Y swap, D-pad up inspect.</summary>
    public static class SkylineDeviceInput
    {
        /// <summary>Degrees per pixel of mouse movement.</summary>
        public const float MouseDegreesPerPixel = 0.09f;
        /// <summary>The right stick's full turn rate (degrees a second).</summary>
        public const float PadDegreesPerSecond = 190f;
        /// <summary>Holding the stick fully over for a moment turns up to this much faster (MovementController's boost).</summary>
        public const float PadTurnBoost = 1.6f;
        /// <summary>The stick's response curve exponent (MovementController's).</summary>
        public const float PadCurve = 1.6f;
        /// <summary>Vertical look is this share of the horizontal rate on the pad (MovementController's).</summary>
        public const float PadPitchShare = 0.7f;
        public const float TriggerThreshold = 0.5f;
        const float StickDeadzone = 0.12f;

        static bool _lookFromPad, _triggerLast;
        static float _turnHeld;

        public static SkylineInputFrame Read(float dt)
        {
            var f = new SkylineInputFrame();
            if (!(dt > 0f)) dt = 0f;
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            var pad = Gamepad.current;

            Vector2 move = Vector2.zero;
            if (kb != null)
            {
                if (kb.wKey.isPressed) move.y += 1f;
                if (kb.sKey.isPressed) move.y -= 1f;
                if (kb.dKey.isPressed) move.x += 1f;
                if (kb.aKey.isPressed) move.x -= 1f;
                f.jumpHeld = kb.spaceKey.isPressed;
                f.jumpPressed = kb.spaceKey.wasPressedThisFrame;
                f.crouchHeld = kb.leftCtrlKey.isPressed || kb.cKey.isPressed;
                f.crouchPressed = kb.leftCtrlKey.wasPressedThisFrame || kb.cKey.wasPressedThisFrame;
                f.sprintHeld = kb.leftShiftKey.isPressed;
                f.reloadPressed = kb.rKey.wasPressedThisFrame;
                f.swapPressed = kb.qKey.wasPressedThisFrame;
                f.inspectPressed = kb.fKey.wasPressedThisFrame;
            }
            if (mouse != null)
            {
                f.fireHeld = mouse.leftButton.isPressed;
                f.firePressed = mouse.leftButton.wasPressedThisFrame;
                if (Mathf.Abs(mouse.scroll.ReadValue().y) > 0.01f) f.swapPressed = true;
                Vector2 d = mouse.delta.ReadValue();
                if (d.sqrMagnitude > 0f)
                {
                    f.look += new Vector2(d.x * MouseDegreesPerPixel, -d.y * MouseDegreesPerPixel);
                    _lookFromPad = false;
                }
            }
            if (pad != null)
            {
                Vector2 ls = pad.leftStick.ReadValue();
                if (ls.magnitude > StickDeadzone) move += ls;
                f.jumpHeld |= pad.buttonSouth.isPressed;
                f.jumpPressed |= pad.buttonSouth.wasPressedThisFrame;
                f.crouchHeld |= pad.buttonEast.isPressed;
                f.crouchPressed |= pad.buttonEast.wasPressedThisFrame;
                f.sprintTogglePressed = pad.leftStickButton.wasPressedThisFrame;
                f.reloadPressed |= pad.buttonWest.wasPressedThisFrame;
                f.swapPressed |= pad.buttonNorth.wasPressedThisFrame;
                f.inspectPressed |= pad.dpad.up.wasPressedThisFrame;
                bool trig = pad.rightTrigger.ReadValue() > TriggerThreshold;
                f.fireHeld |= trig;
                f.firePressed |= trig && !_triggerLast;
                _triggerLast = trig;

                Vector2 st = pad.rightStick.ReadValue();
                if (st.magnitude > StickDeadzone)
                {
                    float full = Mathf.Abs(st.x) > 0.9f ? 1f : 0f;
                    _turnHeld = full > 0f ? Mathf.Min(_turnHeld + dt, 0.6f) : 0f;
                    float boost = 1f + (PadTurnBoost - 1f) * Mathf.Clamp01((_turnHeld - 0.25f) / 0.35f);
                    float cx = Mathf.Sign(st.x) * Mathf.Pow(Mathf.Abs(st.x), PadCurve);
                    float cy = Mathf.Sign(st.y) * Mathf.Pow(Mathf.Abs(st.y), PadCurve);
                    f.look += new Vector2(cx * PadDegreesPerSecond * boost * dt, -cy * PadDegreesPerSecond * PadPitchShare * dt);
                    _lookFromPad = true;
                }
                else _turnHeld = 0f;
            }
            else _triggerLast = false;
            f.move = Vector2.ClampMagnitude(move, 1f);
            f.lookFromPad = _lookFromPad;
            return f;
        }
    }
}
