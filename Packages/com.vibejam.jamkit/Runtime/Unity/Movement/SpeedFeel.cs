using JamKit.Shell;
using UnityEngine;

namespace JamKit.Movement
{
    /// <summary>
    /// The hub of the speed visuals (docs/SPEED-FEEL.md "Speed feel (built)"): ticks the shared speed tiers
    /// (<see cref="SpeedTier"/>, through core <see cref="SpeedFeelState"/>) once per rendered frame with the body's
    /// horizontal speed and publishes <see cref="Levels"/>, already scaled by the Comfort screen's Motion effects and
    /// "reduce effects". The effects read it: <see cref="MovementCamera"/> (the boom's FOV punch),
    /// <see cref="SpeedScreenFx"/> (ring, chromatic pulse, edge blur, the dream's grade), <see cref="SpeedStreaks"/>,
    /// <see cref="SpeedFootfalls"/>, <see cref="SpeedLines"/>. The numbers live in the movement tuning asset
    /// (<see cref="MovementTuning.speedFeel"/>, <see cref="MovementTuning.speedTiers"/>). Put it on the camera.
    /// Runs after the controller and before the camera feel.
    /// </summary>
    [DefaultExecutionOrder(35)]
    public sealed class SpeedFeel : MonoBehaviour
    {
        public MovementController body;
        [Tooltip("Numbers from here; empty: the body's tuning asset (or the defaults).")]
        public MovementTuning tuning;
        [Tooltip("A jump of more than this (m) in one frame beyond the speed is a teleport (respawn, chapter jump): arriving at speed doesn't boom.")]
        public float teleportJump = 3f;

        SpeedFeelState _state;
        SpeedFeelLevels _levels;
        Vector3 _lastPos;
        bool _hasLast;
        int _boomFrame = -1;

        /// <summary>This frame's levels (zero before the first tick).</summary>
        public SpeedFeelLevels Levels => _levels;
        public SpeedFeelState State => _state;
        public SpeedFeelDef Def => _state != null ? _state.Def : TuningDef();
        /// <summary>Booms so far (real crossings into flying).</summary>
        public int Booms => _state != null ? _state.Booms : 0;
        /// <summary>The boom fired this frame.</summary>
        public bool BoomThisFrame => _boomFrame == Time.frameCount;

        /// <summary>The Comfort screen's Motion effects (1 without the shell).</summary>
        public static float Motion => GameRoot.Exists ? Mathf.Clamp01(GameRoot.Settings.motionEffects) : 1f;
        /// <summary>The Comfort screen's "reduce effects".</summary>
        public static bool Reduce => GameRoot.Exists && GameRoot.Settings.reduceEffects;

        MovementTuning Tuning => tuning != null ? tuning : body != null ? body.tuning : null;
        SpeedFeelDef TuningDef() => Tuning != null && Tuning.speedFeel != null ? Tuning.speedFeel : null;

        void Awake() => Build();

        void Build()
        {
            var t = Tuning;
            _state = new SpeedFeelState(t != null ? t.speedFeel : null, t != null ? t.speedTiers : null);
        }

        /// <summary>Forget the tier and any boom (a fresh run).</summary>
        public void ResetFeel()
        {
            if (_state == null) Build();
            _state.Reset();
            _levels = default;
            _hasLast = false;
        }

        void LateUpdate()
        {
            if (body == null || body.Sim == null) return;
            if (_state == null) Build();
            var t = Tuning;
            if (t != null && t.speedFeel != null && !ReferenceEquals(_state.Def, t.speedFeel)) _state.SetDef(t.speedFeel);
            float dt = Time.deltaTime;
            float speed = body.Speed;
            Vector3 pos = body.RenderPosition;
            if (_hasLast && (pos - _lastPos).magnitude > speed * Mathf.Max(dt, Time.unscaledDeltaTime) * 2f + teleportJump) _state.Teleported();
            _lastPos = pos;
            _hasLast = true;
            _levels = _state.Tick(speed, dt, Motion, Reduce);
            if (_levels.boom) _boomFrame = Time.frameCount;
        }
    }
}
