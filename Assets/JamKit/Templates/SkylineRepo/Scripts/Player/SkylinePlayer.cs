using System;
using System.Collections.Generic;
using JamKit.Combat;
using JamKit.Movement;
using JamKit.Skyline;
using UnityEngine;
using NVec2 = System.Numerics.Vector2;
using NVec3 = System.Numerics.Vector3;

namespace JamKit.SkylineRepo
{
    /// <summary>
    /// The player (docs/SKYLINE-PLAN-M1.md Task 14): one input frame a frame drives both the body (through
    /// <see cref="MovementController.InputOverride"/>) and the two guns. In order, each Update: read the frame (a script's,
    /// else the devices'); look (pad aim assist over the live aimables in line of sight; none for the mouse), clamp the pitch
    /// and <see cref="MovementController.SetLook"/>; the Breacher's boost charge observes the body; swap / reload / inspect,
    /// then the active gun's tick; each cue is raised, sounded, logged as a decision, and a shot fires (hitscan, the ledger,
    /// the camera kick, shake, hit stop, the marker, tracers, impacts, and the Breacher's self-push through the engine's
    /// Request → AddPush → Commit); then the kick, the shake and hit stop tick and the pivot under the head takes the kick and
    /// shake, so the aim itself never moves. Runs before MovementController (-50 vs -40).
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class SkylinePlayer : MonoBehaviour, ISkylineTarget
    {
        /// <summary>Most degrees the view may pitch up or down.</summary>
        public const float MaxPitch = 85f;
        /// <summary>Look input (degrees this frame) below this doesn't count as turning, for aim assist's pull.</summary>
        const float TurningThreshold = 0.01f;
        const float ChestHeight = 1.25f;

        [Header("Rig (the builder fills these)")]
        public MovementController movement;
        public Camera cam;
        [Tooltip("Under the head, above the camera: takes the kick and shake so the aim never moves.")]
        public Transform kickPivot;
        public SkylineViewmodel viewmodel;
        [Tooltip("Seed for the pellet spread and the kick's jitter.")]
        public int seed = 1234;

        /// <summary>Drives the player instead of the devices (the range director's demo, tests). Called once a frame.</summary>
        public Func<SkylineInputFrame> ScriptedInput;
        /// <summary>Off for the scripted demo (amendment 4): its schedule is timed on unscaled time and must not stretch.</summary>
        public bool HitStopEnabled = true;

        public Loadout Loadout { get; private set; }
        public PlayerArmor Armor { get; private set; }
        public BoostCharge Boost { get; private set; }
        public BreacherBoostDef BoostDef { get; private set; }
        public WritTuning Writ { get; private set; }
        public DecisionLog Decisions { get; private set; }
        public LedgerRun Ledger { get; private set; }
        public MovementController Movement => movement;
        public Camera Camera => cam;
        public CameraKick Kick { get; } = new CameraKick();
        public ScreenShake Shake { get; } = new ScreenShake();
        public HitStop HitStop { get; } = new HitStop();
        public float Yaw { get; private set; }
        public float Pitch { get; private set; }
        public Vector3 EyePosition => movement != null ? movement.EyePosition : transform.position + Vector3.up * 1.6f;
        /// <summary>Where shots go: the yaw and pitch, never the kick or shake.</summary>
        public Vector3 AimDirection => Quaternion.Euler(Pitch, Yaw, 0f) * Vector3.forward;
        /// <summary>This frame's input (after it was read).</summary>
        public SkylineInputFrame Frame => _frame;

        /// <summary>Every cue the guns raise, with the loadout slot it came from.</summary>
        public event Action<GunCue, int> CueRaised;
        /// <summary>Every pellet that hit something hittable.</summary>
        public event Action<SkylineHitInfo, SkylineHitResult> HitLanded;
        /// <summary>The Unit was wrecked. With no listener the player restarts where it began (the arena restarts at the
        /// warm-up instead; the creator's death flow is M4).</summary>
        public event Action Wrecked;

        // ISkylineTarget
        public Vector3 Chest => transform.position + Vector3.up * ChestHeight;
        public Vector3 Velocity => movement != null ? movement.Velocity : Vector3.zero;
        public Transform Root => transform;

        readonly List<GunCue> _cues = new List<GunCue>();
        readonly List<AimTarget> _aimTargets = new List<AimTarget>();
        readonly List<ISkylineAimable> _aimables = new List<ISkylineAimable>();
        Rng _rng;
        SkylineInputFrame _frame;
        bool _awaitingTakeoff, _hitStopWasActive, _hitStopRequested;
        string _assistLogged = "";
        float _assistScaleLogged = 1f;
        Vector3 _spawnFeet;
        float _spawnYaw;

        void Awake()
        {
            if (movement == null) movement = GetComponent<MovementController>();
            if (cam == null) cam = GetComponentInChildren<Camera>();
            if (movement != null)
            {
                movement.managePointerLock = false;   // Skyline locks the cursor itself
                movement.onlyMarkedWalls = true;      // every runnable wall carries a MovementSurface (Task 16's lint)
                movement.InputOverride = BuildMoveInput;
            }
            _rng = new Rng((ulong)seed);
            Armor = new PlayerArmor();
            Boost = new BoostCharge();
            BoostDef = new BreacherBoostDef();
            Writ = new WritTuning();
            Decisions = new DecisionLog();
            Ledger = new LedgerRun();
            Loadout = new Loadout(new SkylineGun(SkylineGuns.Writ()), new SkylineGun(SkylineGuns.Breacher()), _cues);
            Yaw = transform.eulerAngles.y;
            Pitch = 0f;
            _spawnFeet = transform.position;
            _spawnYaw = Yaw;
        }

        void Start()
        {
            // After every Awake: MovementController's Awake builds a new MoveSim, which would drop a flag set earlier.
            EnsureOutsidePush();
        }

        void EnsureOutsidePush()
        {
            var sim = movement != null ? movement.Sim : null;
            if (sim != null && !sim.OutsidePushEnabled) sim.OutsidePushEnabled = true;
        }

        /// <summary>Takes the active gun out again (its equip clip from the first frame). Not a swap: its Equip cue is marked
        /// <see cref="GunCue.initial"/>, so no Swap decision is logged.</summary>
        public void ReEquip()
        {
            int from = _cues.Count;
            Loadout.Active.Holster();
            Loadout.Active.Equip(_cues);
            for (int i = from; i < _cues.Count; i++)
            {
                var c = _cues[i];
                if (c.kind == GunCueKind.ClipStarted && c.clip == HandlingClipKind.Equip) { c.initial = true; _cues[i] = c; }
            }
        }

        /// <summary>Points the view (degrees; positive pitch looks down) without any assist.</summary>
        public void SetAim(float yaw, float pitch)
        {
            Yaw = Mathf.Repeat(yaw, 360f);
            Pitch = Mathf.Clamp(pitch, -MaxPitch, MaxPitch);
            if (movement != null) movement.SetLook(Yaw, Pitch);
        }

        MoveInput BuildMoveInput(MoveSim sim, float dt) => new MoveInput
        {
            move = new NVec2(_frame.move.x, _frame.move.y),
            yaw = Yaw,
            jumpHeld = _frame.jumpHeld,
            jumpPressed = _frame.jumpPressed,
            crouchHeld = _frame.crouchHeld,
            crouchPressed = _frame.crouchPressed,
            sprintHeld = _frame.sprintHeld,
            sprintTogglePressed = _frame.sprintTogglePressed,
        };

        void Update()
        {
            float dt = Time.deltaTime;
            float udt = Time.unscaledDeltaTime;
            EnsureOutsidePush();
            // 1. One frame of input. Read before the cursor update, so the click that locks the cursor doesn't also fire
            //    (the device reader ignores the mouse while the cursor is free).
            _frame = ScriptedInput != null ? ScriptedInput() : SkylineDeviceInput.Read(udt);
            UpdateCursor();

            // 2. Look.
            Look(dt);

            // 3. The boost charge watches the body (the take-off guard: a lift committed on the ground doesn't re-arm the
            //    charge on the frames before the engine has applied it and the body has left).
            var sim = movement != null ? movement.Sim : null;
            if (sim != null)
            {
                bool grounded = sim.Grounded;
                if (_awaitingTakeoff)
                {
                    if (grounded) grounded = false;
                    else _awaitingTakeoff = false;
                }
                Boost.Observe(grounded, sim.Mode == MoveMode.Wallrun);
            }

            // 4. The loadout and the active gun.
            if (_frame.swapPressed) Loadout.Swap(_cues);
            if (_frame.reloadPressed) Loadout.Active.Reload(_cues);
            if (_frame.inspectPressed) Loadout.Active.Inspect(_cues);
            Loadout.Active.Tick(dt, _frame.fireHeld, _frame.firePressed, _cues);

            // 5. The cues.
            int slot = Loadout.ActiveIndex;
            for (int i = 0; i < _cues.Count; i++) HandleCue(_cues[i], slot);
            _cues.Clear();

            // 6. Kick, shake, hit stop; the pivot under the head.
            Kick.Tick(dt);
            Shake.Tick(dt);
            // A stop requested this frame keeps its first frame whole: it starts ticking next frame.
            if (!_hitStopRequested) HitStop.Tick(udt);
            _hitStopRequested = false;
            // Time.timeScale is touched only while a hit stop runs (and once after), so nothing else's scale is overridden.
            bool stopping = HitStop.IsStopped || HitStop.IsRecovering;
            if (stopping || _hitStopWasActive) Time.timeScale = HitStopEnabled ? HitStop.CurrentTimeScale : 1f;
            _hitStopWasActive = stopping;
            if (kickPivot != null)
            {
                var s = Shake.Sample();
                kickPivot.localRotation = Quaternion.Euler(Kick.Pitch + s.offset.Y, Kick.Yaw + s.offset.X, s.roll);
            }
        }

        void Look(float dt)
        {
            Vector2 look = _frame.look;
            if (_frame.lookFromPad)
            {
                bool moving = _frame.move.sqrMagnitude > 0.01f || look.sqrMagnitude > TurningThreshold * TurningThreshold;
                var r = ComputeAssist(moving, dt);
                look *= r.turnScale;
                look.x += r.yawNudge;
                look.y += r.pitchNudge;
                LogAssist(r);
            }
            SetAim(Yaw + look.x, Pitch + look.y);
        }

        AimAssistResult ComputeAssist(bool movingOrTurning, float dt)
        {
            _aimTargets.Clear();
            _aimables.Clear();
            Vector3 eye = EyePosition;
            var all = SkylineTargets.Aimables;
            for (int i = 0; i < all.Count; i++)
            {
                var a = all[i];
                if (a == null || !a.Alive) continue;
                var comp = a as Component;
                if ((object)comp != null && comp == null) continue;   // destroyed (Unity's null)
                Vector3 c = a.Center;
                if (Physics.Linecast(eye, c, out var hit, SkylineShooting.Mask, QueryTriggerInteraction.Ignore))
                {
                    bool own = comp != null && hit.collider.transform.IsChildOf(comp.transform);
                    if (!own) continue;
                }
                _aimTargets.Add(new AimTarget(new NVec3(c.x, c.y, c.z), a.Radius));
                _aimables.Add(a);
            }
            var def = Loadout.Active.Def.aimAssist;
            return AimAssist.Compute(new NVec3(eye.x, eye.y, eye.z), Yaw, Pitch, _aimTargets, movingOrTurning, dt, def);
        }

        void LogAssist(AimAssistResult r)
        {
            string id = r.target >= 0 && r.target < _aimables.Count ? _aimables[r.target].Id : "none";
            if (id == _assistLogged && Mathf.Approximately(r.turnScale, _assistScaleLogged)) return;
            _assistLogged = id;
            _assistScaleLogged = r.turnScale;
            Debug.Log("SKYLINE_AIMASSIST " + id + " " + r.turnScale.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
        }

        DecisionContext Context()
        {
            var c = DecisionContext.None;
            var sim = movement != null ? movement.Sim : null;
            if (sim != null)
            {
                if (!sim.Grounded) c |= DecisionContext.Airborne;
                if (sim.Mode == MoveMode.Wallrun) c |= DecisionContext.Wallrunning;
                if (sim.Mode == MoveMode.Slide) c |= DecisionContext.Sliding;
            }
            if (Ledger.InCombat) c |= DecisionContext.InCombat;
            return c;
        }

        NVec3 Feet() { var p = transform.position; return new NVec3(p.x, p.y, p.z); }

        void HandleCue(GunCue cue, int slot)
        {
            CueRaised?.Invoke(cue, slot);
            var gun = Loadout[slot];
            if (cue.kind == GunCueKind.Event && !string.IsNullOrEmpty(cue.ev.sound)) SkylineSounds.PlayViewmodel(cue.ev.sound);
            GunDecisions.Append(Decisions, in cue, slot, Time.time, Feet(), Context(), float.NaN, gun.Ammo);
            if (cue.kind == GunCueKind.Shot) Shoot(gun, slot);
        }

        void Shoot(SkylineGun gun, int slot)
        {
            var def = gun.Def;
            Vector3 aim = AimDirection;
            Vector3 muzzle = viewmodel != null ? viewmodel.MuzzleWorld() : EyePosition + aim * 0.5f;
            var o = SkylineShooting.Fire(def, EyePosition, aim, muzzle, _rng, OnPelletHit);
            Ledger.RecordShot(slot, o.hit, o.damage, o.range, o.killed);
            Kick.Kick(def.kick, _rng.NextFloat() * 2f - 1f);
            Shake.AddTrauma(def.shakePerShot);
            if (o.hit)
            {
                if (HitStopEnabled && HitStop.Request(o.killed ? def.killHitStopSeconds : def.hitStopSeconds)) _hitStopRequested = true;
                var hud = SkylineHud.Instance;
                if (hud != null) hud.Marker(o.marker, o.weakPoint);
            }
            if (def.id == SkylineGuns.BreacherId) SelfPush(aim);
        }

        void OnPelletHit(SkylineHitInfo info, SkylineHitResult r) => HitLanded?.Invoke(info, r);

        /// <summary>The Breacher's push on its shooter, in the order Task 12 built: request it (pure), let the engine accept or
        /// drop it, settle the charge on that answer, and arm the take-off guard after a lift from the ground.</summary>
        void SelfPush(Vector3 aim)
        {
            var sim = movement != null ? movement.Sim : null;
            if (sim == null) return;
            bool grounded = sim.Grounded;
            NVec3 push = BreacherKnockback.RequestSelfPush(new NVec3(aim.x, aim.y, aim.z), sim.Velocity, grounded, sim.def.gravity,
                Boost, BoostDef, out bool spends);
            bool ok = sim.AddPush(push);
            Boost.Commit(spends, ok);
            if (spends && ok && grounded && push.Y > 0f) _awaitingTakeoff = true;
            if (spends && ok)
                Decisions.Add(new DecisionEvent
                {
                    time = Time.time, kind = DecisionKind.Boost, subject = Loadout.ActiveIndex, context = Context(),
                    threatBearing = float.NaN, ammo = Loadout.Active.Ammo, position = Feet(),
                });
        }

        // ---------- ISkylineTarget ----------

        public void TakeAttack(float damage, string attackerId, Vector3 attackerPosition)
        {
            if (Armor.Wrecked) return;
            var hit = Armor.Damage(damage);
            var hud = SkylineHud.Instance;
            if (hud != null)
            {
                hud.DamageFrom(attackerPosition);
                if (hit.platingBroke) hud.FlashPlating();
            }
            if (hit.wrecked)
            {
                if (Wrecked != null) Wrecked();
                else Restart(_spawnFeet, _spawnYaw);
            }
        }

        /// <summary>Back at <paramref name="feet"/> facing <paramref name="yaw"/>, with fresh armour, a fresh boost charge, a new
        /// loadout (both guns full, the Writ equipped), and the kick, shake and hit stop cleared. The ledger, the decision log
        /// and the Writ tuning are kept: resetting the run's records is the arena's restart.</summary>
        public void Restart(Vector3 feet, float yaw)
        {
            Armor = new PlayerArmor();
            Boost = new BoostCharge();
            _awaitingTakeoff = false;
            Kick.Clear();
            Shake.Clear();
            HitStop.Clear();
            Time.timeScale = 1f;
            _cues.Clear();
            Loadout = new Loadout(new SkylineGun(SkylineGuns.Writ()), new SkylineGun(SkylineGuns.Breacher()), _cues);
            if (movement != null) movement.Teleport(feet, yaw, Vector3.zero);
            SetAim(yaw, 0f);
            EnsureOutsidePush();
        }

        // ---------- The cursor ----------

        void UpdateCursor()
        {
            if (Application.isBatchMode || ScriptedInput != null) return;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (Cursor.lockState != CursorLockMode.Locked && mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        void OnDisable()
        {
            if (Time.timeScale != 1f) Time.timeScale = 1f;
            if (Cursor.lockState != CursorLockMode.None)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
    }
}
