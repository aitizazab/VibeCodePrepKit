using JamKit.Combat;
using UnityEngine;

namespace JamKit.Doomlike
{
    /// <summary>
    /// The first-person body: a CharacterController with fast, slidey movement (core
    /// <see cref="PlayerMotion"/>: acceleration, friction, no fall damage), mouse and stick look,
    /// head bob, footsteps, core <see cref="Health"/> and the key cards. DoomGame feeds it input and
    /// ticks it; the camera sits on <see cref="head"/> (pitch) under the body (yaw).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class DoomPlayer : MonoBehaviour
    {
        public CharacterController controller;
        [Tooltip("Pitches up and down; the camera rig hangs under it.")]
        public Transform head;
        [Tooltip("Bobs and kicks; the camera sits on it.")]
        public Transform rig;
        public Camera cam;

        PlayerTuning _t = new PlayerTuning();
        Health _health;
        float _yaw, _pitch;
        Vector2 _vel;
        float _vy;
        float _bob, _bobAmount, _stepDistance;
        float _kick;
        float _deathT;
        int _keys;
        float _speedScale = 1f;
        float _hazardTimer;
        Vector2 _wish;
        float _lean, _leanRoll;

        /// <summary>Tests and the smoke run drive the player with these instead of the input.</summary>
        public Vector2 ScriptedMove;
        public bool UseScriptedMove;

        public Health Health => _health;
        public bool Alive => _health != null && !_health.IsDead;
        public float Yaw => _yaw;
        public float Pitch => _pitch;
        public float Radius => controller != null ? controller.radius : 0.4f;
        public float Height => controller != null ? controller.height : 1.7f;
        public float Speed => _vel.magnitude;
        public Vector2 Velocity => _vel;
        /// <summary>Where the input pushes this frame, world XZ (length 0..1): walking into a wall still counts.</summary>
        public Vector2 Wish => _wish;
        public Vector3 EyePosition => transform.position + Vector3.up * _t.eyeHeight;
        public Vector3 Forward => cam != null ? cam.transform.forward : transform.forward;
        public int Keys => _keys;
        /// <summary>Metres walked (tests check the player really moved).</summary>
        public float DistanceMoved;

        public void Setup(PlayerTuning tuning)
        {
            _t = tuning ?? new PlayerTuning();
            if (controller == null) controller = GetComponent<CharacterController>();
            controller.radius = _t.radius;
            controller.height = _t.height;
            controller.center = new Vector3(0f, _t.height * 0.5f, 0f);
            controller.stepOffset = 0.35f;
            controller.slopeLimit = 50f;
            controller.skinWidth = 0.04f;
            controller.minMoveDistance = 0f;
            gameObject.layer = 2; // Ignore Raycast: world line-of-sight checks pass through the player
            if (head != null) head.localPosition = new Vector3(0f, _t.eyeHeight, 0f);
        }

        /// <summary>A fresh body for a new run.</summary>
        public void NewRun(float maxHealth)
        {
            _health = new Health(Mathf.Max(1f, maxHealth));
            _keys = 0;
            _deathT = 0f;
        }

        /// <summary>Places the player at a level's start (keys are per level).</summary>
        public void Spawn(Vector3 position, float yaw)
        {
            controller.enabled = false;
            transform.position = position;
            controller.enabled = true;
            _yaw = yaw;
            _pitch = 0f;
            _vel = Vector2.zero;
            _vy = 0f;
            _kick = 0f;
            _keys = 0;
            _deathT = 0f;
            ApplyRotation(0f);
        }

        public void Teleport(Vector3 position)
        {
            controller.enabled = false;
            transform.position = position;
            controller.enabled = true;
            _vel = Vector2.zero;
        }

        public void SetSpeedScale(float k) => _speedScale = Mathf.Clamp(k, 0.3f, 3f);

        public void Look(float yawDelta, float pitchDelta)
        {
            _yaw = Mathf.Repeat(_yaw + yawDelta, 360f);
            _pitch = Mathf.Clamp(_pitch + pitchDelta, -_t.maxPitch, _t.maxPitch);
        }

        public void FaceYaw(float yaw)
        {
            _yaw = yaw;
            ApplyRotation(0f);
        }

        /// <summary>A peek (Visiting Hours' waking world): the view slides <paramref name="sideways"/> metres to the right (-
        /// left) and rolls <paramref name="rollDegrees"/> toward it (+ right). The caller keeps it clear of walls.</summary>
        public void SetLean(float sideways, float rollDegrees)
        {
            if (_lean == sideways && _leanRoll == rollDegrees) return;
            _lean = sideways;
            _leanRoll = rollDegrees;
            ApplyRotation(0f);
        }

        /// <summary>The peek's sideways offset now (m, + right).</summary>
        public float Lean => _lean;

        /// <summary>Recoil: the view kicks up and settles.</summary>
        public void Kick(float degrees) => _kick = Mathf.Min(_kick + degrees, 12f);

        public void Tick(float dt, Vector2 moveInput, bool walk, DoomGame game)
        {
            if (TickMove(dt, moveInput, walk)) game.PlaySound("step", 0.35f);
        }

        /// <summary>The body's frame without a DoomGame (other first-person templates reuse the
        /// controller): moves, bobs and looks. Returns true on the frame a footstep lands.</summary>
        public bool TickMove(float dt, Vector2 moveInput, bool walk)
        {
            if (!Alive)
            {
                TickDeath(dt);
                return false;
            }
            bool stepped = false;
            if (UseScriptedMove) moveInput = ScriptedMove;
            var motion = _t.motion ?? new PlayerMotionDef();
            float max = (walk ? motion.walkSpeed : motion.runSpeed) * _speedScale;
            var wish = PlayerMotion.WishFromInput(new System.Numerics.Vector2(moveInput.x, moveInput.y), _yaw);
            _wish = new Vector2(wish.X, wish.Y);
            var v = PlayerMotion.Step(new System.Numerics.Vector2(_vel.x, _vel.y), wish, max, motion, dt);
            _vel = new Vector2(v.X, v.Y);

            if (controller.isGrounded && _vy < 0f) _vy = -2f;
            else _vy -= _t.gravity * dt;
            Vector3 before = transform.position;
            var flags = controller.Move(new Vector3(_vel.x, _vy, _vel.y) * dt);
            Vector3 moved = transform.position - before;
            moved.y = 0f;
            // Running into a wall stops you along it (no speed builds up against walls).
            if (dt > 0f && (flags & CollisionFlags.Sides) != 0)
            {
                var real = new Vector2(moved.x, moved.z) / dt;
                if (real.magnitude < _vel.magnitude) _vel = real;
            }
            float d = moved.magnitude;
            DistanceMoved += d;

            // Bob and footsteps scale with real speed.
            float speed01 = Mathf.Clamp01((dt > 0f ? d / dt : 0f) / Mathf.Max(1f, motion.runSpeed));
            _bobAmount = Mathf.MoveTowards(_bobAmount, speed01, dt * 4f);
            _bob += d / Mathf.Max(0.5f, _t.footstepStride) * Mathf.PI;
            _stepDistance += d;
            if (_stepDistance >= _t.footstepStride && controller.isGrounded)
            {
                _stepDistance = 0f;
                stepped = true;
            }

            _kick = Mathf.MoveTowards(_kick, 0f, dt * (6f + _kick * 8f));
            _health.Tick(dt);
            ApplyRotation(dt);
            return stepped;
        }

        void ApplyRotation(float dt)
        {
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            if (head != null) head.localRotation = Quaternion.Euler(_pitch - _kick, 0f, 0f);
            if (rig != null)
            {
                float b = _t.bobAmount * _bobAmount;
                rig.localPosition = new Vector3(Mathf.Cos(_bob * 0.5f) * b * 0.6f + _lean, Mathf.Abs(Mathf.Sin(_bob * 0.5f)) * b - b * 0.5f, 0f);
                rig.localRotation = _leanRoll != 0f ? Quaternion.Euler(0f, 0f, -_leanRoll) : Quaternion.identity;
            }
        }

        /// <summary>The weapon view follows the same bob: x/y offset in metres.</summary>
        public Vector2 BobOffset
        {
            get
            {
                float b = _t.bobAmount * _bobAmount;
                return new Vector2(Mathf.Cos(_bob * 0.5f) * b * 1.2f, -Mathf.Abs(Mathf.Sin(_bob * 0.5f)) * b * 1.1f);
            }
        }

        void TickDeath(float dt)
        {
            _deathT = Mathf.Min(1f, _deathT + dt * 1.6f);
            float s = _deathT * _deathT * (3f - 2f * _deathT);
            if (head != null)
            {
                head.localPosition = new Vector3(0f, Mathf.Lerp(_t.eyeHeight, 0.3f, s), 0f);
                head.localRotation = Quaternion.Euler(Mathf.Lerp(_pitch, -10f, s), 0f, Mathf.Lerp(0f, 25f, s));
            }
        }

        /// <summary>Resets the camera after death (a new run).</summary>
        public void ResetView()
        {
            if (head != null) head.localPosition = new Vector3(0f, _t.eyeHeight, 0f);
            _deathT = 0f;
            ApplyRotation(0f);
        }

        // ---------- Keys, hazards ----------

        public bool HasKey(int color) => (_keys & (1 << color)) != 0;
        public void GiveKey(int color) => _keys |= 1 << color;
        public void GiveAllKeys() => _keys = 7;

        /// <summary>Slime: damage in ticks of a quarter second while standing in it.</summary>
        public float HazardTick(float dt, bool inHazard, float damagePerSecond)
        {
            if (!inHazard) { _hazardTimer = 0f; return 0f; }
            _hazardTimer += dt;
            if (_hazardTimer < 0.25f) return 0f;
            _hazardTimer -= 0.25f;
            return damagePerSecond * 0.25f;
        }
    }
}
