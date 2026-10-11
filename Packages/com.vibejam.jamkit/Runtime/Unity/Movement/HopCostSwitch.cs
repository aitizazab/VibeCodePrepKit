using JamKit.Movement;
using JamKit.Shell;
using UnityEngine;
using UnityEngine.InputSystem;

namespace JamKit.Movement
{
    /// <summary>
    /// The in-game hop-cost switch (docs/MOVEMENT.md "Hop-cost presets"): F6, or LB held + d-pad up, cycles
    /// Off → Mild → Current; the choice is kept in the save (<see cref="HopCostPresets.SaveKey"/>) and set as the global
    /// override every movement sim reads (no tuning asset is written). Default Mild. The lab's debug HUD (F3 / d-pad down)
    /// calls <see cref="Tick"/> each frame and shows <see cref="HudLine"/>.
    /// </summary>
    public static class HopCostSwitch
    {
        public const string KeyName = "F6";
        public const string PadName = "LB + d-pad up";

        static bool _loaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _loaded = false;
            HopCostPresets.ClearOverride();
        }

        /// <summary>Reads the saved preset once (Mild when nothing is saved) and sets it as the override.</summary>
        public static void EnsureLoaded()
        {
            if (_loaded || !GameRoot.Exists) return;
            _loaded = true;
            HopCostPresets.TryParse(GameRoot.Save.Data.GetEntry(HopCostPresets.SaveKey), out var p);
            HopCostPresets.SetOverride(p);
        }

        /// <summary>Sets and saves a preset.</summary>
        public static void Set(HopCostPreset p)
        {
            EnsureLoaded();
            HopCostPresets.SetOverride(p);
            if (!GameRoot.Exists) return;
            GameRoot.Save.Data.SetEntry(HopCostPresets.SaveKey, p.ToString());
            GameRoot.Save.Save();
        }

        /// <summary>Call every frame: loads the saved preset, and cycles it on F6 or LB + d-pad up. True when it changed.</summary>
        public static bool Tick()
        {
            EnsureLoaded();
            if (!GameRoot.Exists || !GameRoot.Input.GameplayAllowed) return false;
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            bool pressed = (kb != null && kb.f6Key.wasPressedThisFrame)
                || (pad != null && pad.leftShoulder.isPressed && pad.dpad.up.wasPressedThisFrame);
            if (!pressed) return false;
            var next = HopCostPresets.Next(HopCostPresets.Active);
            Set(next);
            GameRoot.Hud.Message("Hop cost: " + HopCostPresets.Describe(next), 3f);
            return true;
        }

        public static string HudLine => "Hop cost: " + HopCostPresets.Active + "  (" + KeyName + " / " + PadName + ")";
    }
}
