using System;

namespace JamKit.Movement
{
    /// <summary>How much bunny hopping costs (docs/MOVEMENT.md "Hop-cost presets"). Off: free, as before 2026-10-11.
    /// Mild (the default): clean hops and slide hops are free; only a late hop costs a little, and the double jump's
    /// steer right after a hop costs only on sharp turns. Current: the 2026-10-11 costs (8% of the speed above sprint
    /// per hop, 3% per slide hop, a late hop pays its ground time, the hop-and-steer pays every degree).</summary>
    public enum HopCostPreset { Off, Mild, Current }

    /// <summary>The numbers one preset sets (the MoveDef fields of the same names).</summary>
    public struct HopCosts
    {
        public float hopSpeedLoss, slideHopSpeedLoss, landingGraceDecay, doubleJumpHopWindow, doubleJumpHopTurnLoss, doubleJumpHopTurnFree;
    }

    /// <summary>
    /// The hop-cost presets and the one in play. A game (the Movement Lab, the dream escape: F6 or LB + d-pad up) can set
    /// a global <see cref="SetOverride"/> that every <see cref="MoveSim"/> reads instead of its MoveDef's own hop fields,
    /// so switching doesn't write into a tuning asset. Without an override each sim uses its def (whose defaults are Mild).
    /// </summary>
    public static class HopCostPresets
    {
        public const string SaveKey = "movement_hop_cost";
        public const HopCostPreset Default = HopCostPreset.Mild;

        static int _override = -1;

        public static bool Overridden => _override >= 0;
        /// <summary>The preset in play: the override, or the default when there is none.</summary>
        public static HopCostPreset Active => _override >= 0 ? (HopCostPreset)_override : Default;

        public static void SetOverride(HopCostPreset preset) => _override = (int)preset;
        public static void ClearOverride() => _override = -1;

        public static HopCostPreset Next(HopCostPreset p) => p == HopCostPreset.Off ? HopCostPreset.Mild : p == HopCostPreset.Mild ? HopCostPreset.Current : HopCostPreset.Off;

        public static HopCosts Of(HopCostPreset p)
        {
            switch (p)
            {
                case HopCostPreset.Off:
                    return new HopCosts { hopSpeedLoss = 0f, slideHopSpeedLoss = 0f, landingGraceDecay = 0f, doubleJumpHopWindow = 0.35f, doubleJumpHopTurnLoss = 0f, doubleJumpHopTurnFree = 0f };
                case HopCostPreset.Current:
                    return new HopCosts { hopSpeedLoss = 0.08f, slideHopSpeedLoss = 0.03f, landingGraceDecay = 1f, doubleJumpHopWindow = 0.35f, doubleJumpHopTurnLoss = 0.1f, doubleJumpHopTurnFree = 0f };
                default:
                    return new HopCosts { hopSpeedLoss = 0f, slideHopSpeedLoss = 0f, landingGraceDecay = 0.5f, doubleJumpHopWindow = 0.35f, doubleJumpHopTurnLoss = 0.1f, doubleJumpHopTurnFree = 45f };
            }
        }

        /// <summary>What a sim with this def uses: the override if one is set, else the def's own fields.</summary>
        public static HopCosts For(MoveDef d)
        {
            if (_override >= 0) return Of((HopCostPreset)_override);
            return new HopCosts
            {
                hopSpeedLoss = d.hopSpeedLoss, slideHopSpeedLoss = d.slideHopSpeedLoss, landingGraceDecay = d.landingGraceDecay,
                doubleJumpHopWindow = d.doubleJumpHopWindow, doubleJumpHopTurnLoss = d.doubleJumpHopTurnLoss, doubleJumpHopTurnFree = d.doubleJumpHopTurnFree,
            };
        }

        /// <summary>Writes a preset into a def's fields (tests, tools).</summary>
        public static MoveDef Apply(MoveDef d, HopCostPreset p)
        {
            var c = Of(p);
            d.hopSpeedLoss = c.hopSpeedLoss;
            d.slideHopSpeedLoss = c.slideHopSpeedLoss;
            d.landingGraceDecay = c.landingGraceDecay;
            d.doubleJumpHopWindow = c.doubleJumpHopWindow;
            d.doubleJumpHopTurnLoss = c.doubleJumpHopTurnLoss;
            d.doubleJumpHopTurnFree = c.doubleJumpHopTurnFree;
            return d;
        }

        public static bool TryParse(string s, out HopCostPreset p)
        {
            p = Default;
            if (string.IsNullOrEmpty(s)) return false;
            foreach (HopCostPreset v in Enum.GetValues(typeof(HopCostPreset)))
                if (string.Equals(v.ToString(), s.Trim(), StringComparison.OrdinalIgnoreCase)) { p = v; return true; }
            return false;
        }

        /// <summary>A few words for the HUD.</summary>
        public static string Describe(HopCostPreset p)
        {
            switch (p)
            {
                case HopCostPreset.Off: return "Off: hops are free (before 2026-10-11)";
                case HopCostPreset.Current: return "Current: 8% of the speed over sprint per hop, 3% per slide hop";
                default: return "Mild: clean hops free, a late hop costs a little";
            }
        }
    }
}
