using UnityEngine;

namespace JamKit.Movement
{
    /// <summary>
    /// A movement tuning asset: every number of the Titanfall-style movement (core <see cref="MoveDef"/>,
    /// explained in docs/MOVEMENT.md). Several scenes can share one; edits in Play mode apply at once.
    /// Create one with Assets > Create > JamKit > Movement Tuning.
    /// <para><b>The shared one</b> (<see cref="Shared"/>, <see cref="SharedAssetPath"/>): every game using the movement
    /// (the Movement Lab, the dream escape and house, the Stealth Lab, the Deep Block) uses this one asset, the
    /// Movement Lab's tuning since 2026-10-11, and a controller with no tuning falls back to it. It also holds the
    /// daytime profile (<see cref="daytime"/>).</para>
    /// </summary>
    [CreateAssetMenu(menuName = "JamKit/Movement Tuning", fileName = "MovementTuning")]
    public sealed class MovementTuning : ScriptableObject
    {
        /// <summary>The defaults this asset has been brought up to (see <see cref="UpgradeDefaults"/>).</summary>
        public const int CurrentDefaults = 2;

        /// <summary>The shared tuning's Resources name and its asset path.</summary>
        public const string SharedResource = "JamKitMovementTuning";
        public const string SharedAssetPath = "Packages/com.vibejam.jamkit/Runtime/Unity/Movement/Resources/" + SharedResource + ".asset";

        static MovementTuning _shared;
        static bool _sharedLoaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _shared = null; _sharedLoaded = false; }

        /// <summary>The one tuning every game shares (null only if the asset is missing).</summary>
        public static MovementTuning Shared
        {
            get
            {
                if (!_sharedLoaded || _shared == null) { _shared = Resources.Load<MovementTuning>(SharedResource); _sharedLoaded = true; }
                return _shared;
            }
        }

        public MoveDef def = new MoveDef();
        [Tooltip("The first-person body's feel (MovementBody): the pump, the tiers, the sway, the landing lag, the contact cues. Every number commented in BodyFeelDef.")]
        public BodyFeelDef body = new BodyFeelDef();
        [Tooltip("The speed tiers everything that reacts to speed shares (running under 8 m/s, fast, flying from 13; SpeedTierDef).")]
        public SpeedTierDef speedTiers = new SpeedTierDef();
        [Tooltip("The speed visuals (docs/SPEED-FEEL.md): the boom, streaks, puffs and footprints, the edge blur, the dream answering. Every number commented in SpeedFeelDef.")]
        public SpeedFeelDef speedFeel = new SpeedFeelDef();
        [Tooltip("The waking world's movement (MoveProfile): no double jump, wallrun or slide, the air strafe well down, jump fatigue. A controller set to the Daytime profile uses it.")]
        public MoveProfile daytime = MoveProfile.Daytime();
        [Tooltip("Which round of changed defaults this asset has taken (the builders upgrade it once; edit freely after).")]
        public int defaultsVersion;

        /// <summary>Applies changed defaults once to an asset made before them, keeping every other number:
        /// 1 (2026-10-10): sprint is opt-in (autoSprint off). 2 (2026-10-11): the wall feel of the creator's controller
        /// notes (2026-10-10), which assets made before it never took: wallRunFriction 1.2, wallProbeReach 0.6,
        /// wallMinTowardDot -0.2, wallAwayDot 0.8. Returns true when it changed the asset.</summary>
        public bool UpgradeDefaults()
        {
            if (defaultsVersion >= CurrentDefaults) return false;
            if (defaultsVersion < 1) def.autoSprint = false;
            if (defaultsVersion < 2)
            {
                def.wallRunFriction = 1.2f;
                def.wallProbeReach = 0.6f;
                def.wallMinTowardDot = -0.2f;
                def.wallAwayDot = 0.8f;
            }
            defaultsVersion = CurrentDefaults;
            return true;
        }
    }
}
