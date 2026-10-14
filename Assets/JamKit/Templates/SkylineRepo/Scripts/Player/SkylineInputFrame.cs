using UnityEngine;

namespace JamKit.SkylineRepo
{
    /// <summary>One frame of the player's input (docs/SKYLINE-PLAN-M1.md Task 14), read once a frame by
    /// <see cref="SkylinePlayer"/> so movement and shooting see the same presses. From the devices
    /// (<see cref="SkylineDeviceInput"/>) or from a script (the range director's demo).</summary>
    public struct SkylineInputFrame
    {
        /// <summary>x strafe right, y forward, length 0..1.</summary>
        public Vector2 move;
        /// <summary>Degrees to turn this frame, already scaled by the device: x yaw right, y pitch DOWN (Unity's and
        /// AimAssist's convention: positive pitch looks down).</summary>
        public Vector2 look;
        /// <summary>The last look input came from the pad (aim assist applies only then).</summary>
        public bool lookFromPad;
        public bool jumpHeld, jumpPressed;
        public bool crouchHeld, crouchPressed;
        public bool sprintHeld;
        /// <summary>The pad's L3 click: sprint until you stop moving forward (MovementController's own pad scheme).</summary>
        public bool sprintTogglePressed;
        public bool fireHeld, firePressed;
        /// <summary>The Aim binding is held (right mouse / left trigger by default): aim down sights.</summary>
        public bool aimHeld;
        public bool reloadPressed, swapPressed, inspectPressed;
    }
}
