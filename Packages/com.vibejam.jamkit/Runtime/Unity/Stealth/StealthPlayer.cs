using JamKit.Movement;
using UnityEngine;
using NVec3 = System.Numerics.Vector3;

namespace JamKit.Stealth
{
    /// <summary>
    /// Makes a movement-kit player findable: each frame it reads the <see cref="MovementController"/> into a
    /// <see cref="StealthTarget"/> (feet, height, speed, crouched, sliding, the <see cref="HidingSpotView"/> it stands in)
    /// and the frame's noise (core <see cref="StealthNoiseRules"/>: hard landings, sprint steps, wallruns, slides).
    /// Seekers read <see cref="Current"/>. Runs after the movement, before the seekers.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public sealed class StealthPlayer : MonoBehaviour
    {
        public MovementController body;
        [Tooltip("The noise numbers (StealthDef's hearing section). Empty: the defaults.")]
        public StealthTuning tuning;
        [Tooltip("Off: the player is never seen or heard (a cutscene, a safe room, a debug toggle).")]
        public bool findable = true;

        static StealthPlayer _current;
        readonly StealthNoiseRules _noiseRules = new StealthNoiseRules();
        readonly StealthDef _fallback = new StealthDef();
        StealthTarget _target;
        StealthNoise _noise;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _current = null;

        public static StealthPlayer Current => _current;
        public StealthDef Def => tuning != null ? tuning.def : _fallback;
        /// <summary>This frame's target (not present when not findable).</summary>
        public StealthTarget Target => _target;
        /// <summary>This frame's loudest noise (none most frames).</summary>
        public StealthNoise Noise => _noise;
        /// <summary>The hiding spot the player is in (index into <see cref="HidingSpotView.Spots"/>), -1 for none.</summary>
        public int HidingSpot => _target.present ? _target.hidingSpot : -1;
        public bool Hidden => HidingSpot >= 0;
        public HideKind? HiddenKind => Hidden ? HidingSpotView.Spots[HidingSpot].kind : (HideKind?)null;

        void Awake()
        {
            if (body == null) body = GetComponent<MovementController>();
        }

        void OnEnable() => _current = this;

        void OnDisable()
        {
            if (_current == this) _current = null;
        }

        void Update()
        {
            _target = default;
            _noise = default;
            if (body == null || body.Sim == null || !findable) return;
            var sim = body.Sim;
            var p = transform.position;
            _target.present = true;
            _target.feet = new NVec3(p.x, p.y, p.z);
            _target.height = sim.Height;
            _target.speed = sim.HorizontalSpeed;
            _target.sliding = sim.Mode == MoveMode.Slide;
            _target.crouched = sim.Crouched;
            var lean = body.LeanWorld; // the peek: sight looks for her head where it is
            _target.lean = new NVec3(lean.x, 0f, lean.z);
            _target.hidingSpot = HidingSpotView.IndexAt(p);
            _noise = _noiseRules.Tick(Def, sim.Mode, body.FrameEvents, _target.feet, Time.deltaTime, _target.speed, _target.crouched, StealthRules.Shared.loudMovement);
        }
    }
}
