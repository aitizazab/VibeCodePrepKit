using JamKit.Skyline;
using UnityEngine;
using UnityEngine.InputSystem;

namespace JamKit.SkylineRepo
{
    /// <summary>Reads the keyboard, the mouse and the pad straight from the Input System (Skyline's scenes have no GameRoot).
    /// Moves and look are fixed: WASD or the left stick, the mouse or the right stick (the same response curve as
    /// MovementController). Every button comes from the player's bindings (<see cref="SkylineBindings"/>; the defaults: Space
    /// or A jump, Left Ctrl, C or R3 crouch and slide, Left Shift sprint, L3 sprint toggle, left mouse or right trigger fire,
    /// right mouse or left trigger aim, R or X reload, Q or the wheel or Y swap, F or D-pad up inspect, Escape or Start
    /// pause). Look sensitivity and invert Y come from the player's options (<see cref="SkylineSettings"/>). While the pause
    /// menu is open (<see cref="Paused"/>) the frame is empty.</summary>
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
        const float StickDeadzone = 0.12f;

        /// <summary>Wheel notches closer together than this (unscaled s) are one gesture: one swap.</summary>
        public const float ScrollGestureGap = 0.2f;

        /// <summary>Set by the pause menu while it is open: the game gets an empty frame.</summary>
        public static bool Paused;

        static bool _lookFromPad;
        static float _lastScroll = -1f;
        static float _turnHeld;

        public static SkylineInputFrame Read(float dt)
        {
            var f = new SkylineInputFrame();
            if (Paused) return f;
            if (!(dt > 0f)) dt = 0f;
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            var pad = Gamepad.current;
            var o = SkylineSettings.Options;
            // The mouse counts only while the cursor is locked (or in batch mode): with it free, a click or a move toward the
            // Inspector must not fire, aim or turn the view.
            bool mouseOk = mouse != null && (Application.isBatchMode || Cursor.lockState == CursorLockMode.Locked);
            float invert = o.invertY ? -1f : 1f;

            Vector2 move = Vector2.zero;
            if (kb != null)
            {
                if (kb.wKey.isPressed) move.y += 1f;
                if (kb.sKey.isPressed) move.y -= 1f;
                if (kb.dKey.isPressed) move.x += 1f;
                if (kb.aKey.isPressed) move.x -= 1f;
            }

            // The buttons: each action's keyboard-and-mouse binding or its pad binding.
            const BindDevice KM = BindDevice.KeyboardMouse, Pad = BindDevice.Pad;
            f.jumpHeld = SkylineBindings.Held(KM, BindAction.Jump, mouseOk) || SkylineBindings.Held(Pad, BindAction.Jump, mouseOk);
            f.jumpPressed = SkylineBindings.Pressed(KM, BindAction.Jump, mouseOk) || SkylineBindings.Pressed(Pad, BindAction.Jump, mouseOk);
            f.crouchHeld = SkylineBindings.Held(KM, BindAction.Crouch, mouseOk) || SkylineBindings.Held(Pad, BindAction.Crouch, mouseOk);
            f.crouchPressed = SkylineBindings.Pressed(KM, BindAction.Crouch, mouseOk) || SkylineBindings.Pressed(Pad, BindAction.Crouch, mouseOk);
            f.sprintHeld = SkylineBindings.Held(KM, BindAction.Sprint, mouseOk);                       // held on the keyboard...
            f.sprintTogglePressed = SkylineBindings.Pressed(Pad, BindAction.Sprint, mouseOk);          // ...a click on the pad (L3)
            f.fireHeld = SkylineBindings.Held(KM, BindAction.Fire, mouseOk) || SkylineBindings.Held(Pad, BindAction.Fire, mouseOk);
            f.firePressed = SkylineBindings.Pressed(KM, BindAction.Fire, mouseOk) || SkylineBindings.Pressed(Pad, BindAction.Fire, mouseOk);
            f.aimHeld = SkylineBindings.Held(KM, BindAction.Aim, mouseOk) || SkylineBindings.Held(Pad, BindAction.Aim, mouseOk);
            f.reloadPressed = SkylineBindings.Pressed(KM, BindAction.Reload, mouseOk) || SkylineBindings.Pressed(Pad, BindAction.Reload, mouseOk);
            f.swapPressed = SkylineBindings.Pressed(KM, BindAction.Swap, mouseOk) || SkylineBindings.Pressed(Pad, BindAction.Swap, mouseOk);
            f.inspectPressed = SkylineBindings.Pressed(KM, BindAction.Inspect, mouseOk) || SkylineBindings.Pressed(Pad, BindAction.Inspect, mouseOk);

            if (mouseOk)
            {
                if (Mathf.Abs(mouse.scroll.ReadValue().y) > 0.01f)
                {
                    float now = Time.unscaledTime;
                    if (_lastScroll < 0f || now - _lastScroll > ScrollGestureGap) f.swapPressed = true;
                    _lastScroll = now;
                }
                Vector2 d = mouse.delta.ReadValue();
                if (d.sqrMagnitude > 0f)
                {
                    float k = MouseDegreesPerPixel * o.mouseSensitivity;
                    f.look += new Vector2(d.x * k, -d.y * k * invert);
                    _lookFromPad = false;
                }
            }
            if (pad != null)
            {
                Vector2 ls = pad.leftStick.ReadValue();
                if (ls.magnitude > StickDeadzone) move += ls;

                Vector2 st = pad.rightStick.ReadValue();
                if (st.magnitude > StickDeadzone)
                {
                    float full = Mathf.Abs(st.x) > 0.9f ? 1f : 0f;
                    _turnHeld = full > 0f ? Mathf.Min(_turnHeld + dt, 0.6f) : 0f;
                    float boost = 1f + (PadTurnBoost - 1f) * Mathf.Clamp01((_turnHeld - 0.25f) / 0.35f);
                    float cx = Mathf.Sign(st.x) * Mathf.Pow(Mathf.Abs(st.x), PadCurve);
                    float cy = Mathf.Sign(st.y) * Mathf.Pow(Mathf.Abs(st.y), PadCurve);
                    float rate = PadDegreesPerSecond * o.padSensitivity;
                    f.look += new Vector2(cx * rate * boost * dt, -cy * rate * PadPitchShare * dt * invert);
                    _lookFromPad = true;
                }
                else _turnHeld = 0f;
            }
            f.move = Vector2.ClampMagnitude(move, 1f);
            f.lookFromPad = _lookFromPad;
            return f;
        }
    }
}
