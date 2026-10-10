using UnityEngine;

namespace JamKit.Movement
{
    /// <summary>
    /// A movement tuning asset: every number of the Titanfall-style movement (core <see cref="MoveDef"/>,
    /// explained in docs/MOVEMENT.md). Several scenes can share one; edits in Play mode apply at once.
    /// Create one with Assets > Create > JamKit > Movement Tuning.
    /// </summary>
    [CreateAssetMenu(menuName = "JamKit/Movement Tuning", fileName = "MovementTuning")]
    public sealed class MovementTuning : ScriptableObject
    {
        /// <summary>The defaults this asset has been brought up to (see <see cref="UpgradeDefaults"/>).</summary>
        public const int CurrentDefaults = 1;

        public MoveDef def = new MoveDef();
        [Tooltip("Which round of changed defaults this asset has taken (the builders upgrade it once; edit freely after).")]
        public int defaultsVersion;

        /// <summary>Applies changed defaults once to an asset made before them, keeping every other number:
        /// 1 (2026-10-10): sprint is opt-in (autoSprint off). Returns true when it changed the asset.</summary>
        public bool UpgradeDefaults()
        {
            if (defaultsVersion >= CurrentDefaults) return false;
            if (defaultsVersion < 1) def.autoSprint = false;
            defaultsVersion = CurrentDefaults;
            return true;
        }
    }
}
