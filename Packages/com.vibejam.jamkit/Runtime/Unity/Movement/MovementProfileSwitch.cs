using JamKit.Shell;
using UnityEngine.InputSystem;

namespace JamKit.Movement
{
    /// <summary>
    /// The lab's movement-profile toggle (docs/MOVEMENT.md "Profiles: dream and daytime"): F7, or LB held + d-pad right,
    /// switches the player between the dream's movement and the daytime one, so the creator can feel the waking world's
    /// movement without a daytime scene. The lab's debug HUD (F3 / d-pad down) calls <see cref="Tick"/> and shows
    /// <see cref="HudLine"/>. Not saved: every scene starts in its own profile.
    /// </summary>
    public static class MovementProfileSwitch
    {
        public const string KeyName = "F7";
        public const string PadName = "LB + d-pad right";

        /// <summary>Call every frame. True when it switched.</summary>
        public static bool Tick(MovementController body)
        {
            if (body == null || !GameRoot.Exists || !GameRoot.Input.GameplayAllowed) return false;
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            bool pressed = (kb != null && kb.f7Key.wasPressedThisFrame)
                || (pad != null && pad.leftShoulder.isPressed && pad.dpad.right.wasPressedThisFrame);
            if (!pressed) return false;
            Toggle(body);
            return true;
        }

        public static void Toggle(MovementController body)
        {
            var next = body.Profile == MoveProfileKind.Dream ? MoveProfileKind.Daytime : MoveProfileKind.Dream;
            body.SetProfile(next);
            if (GameRoot.Exists) GameRoot.Hud.Message(next == MoveProfileKind.Daytime
                ? "Movement: daytime (no double jump, wallrun or slide; the air strafe down; jump fatigue after 3 quick jumps)"
                : "Movement: the dream (everything)", 3f);
        }

        public static string HudLine(MovementController body) =>
            "Movement: " + (body != null && body.Profile == MoveProfileKind.Daytime ? "Daytime" : "Dream") + "  (" + KeyName + " / " + PadName + ")";
    }
}
