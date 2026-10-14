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
    /// <para>Aim down sights (the pause-menu plan, section 3): hold Aim and <see cref="AimAmount"/> eases to 1 over 0.15 s (back
    /// over 0.12 s). It narrows the cameras (<see cref="AimFovScale"/>, applied by the viewmodel), slows the look
    /// (<see cref="AimLookScale"/>) and the move (<see cref="AimMoveScale"/>, on the move vector given to the engine) and tightens
    /// the pellets (<see cref="AimSpreadScale"/>); the viewmodel moves the gun to its aim pose. There is no aim during a reload,
    /// an inspect or an equip, and sprint ends when aim starts and cannot start while aim is held. Each aim is logged as
    /// <c>SKYLINE_AIM &lt;gun&gt; in|out</c>. While the pause menu is open the player does nothing.</para>
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
        /// <summary>Off for the scripted demos (amendment 4): with no hit stop Time.timeScale stays 1, so the range's schedule,
        /// timed on the game clock (Time.time), never stretches against the recording.</summary>
        public bool HitStopEnabled = true;

        /// <summary>The Relay's cable (docs/SKYLINE-RELAY.md): scales the move vector given to the engine (1 = normal; 0.75 while
        /// carrying the cable) and, when set, stops the sprint (held or toggled) like aiming does. Both are set by
        /// <see cref="SkylineRelayDirector"/> each frame and are 1 and false in the normal mode.</summary>
        [NonSerialized] public float MoveScale = 1f;
        [NonSerialized] public bool SprintBlocked;

        public Loadout Loadout { get; private set; }
        public PlayerArmor Armor { get; private set; }
        public BoostCharge Boost { get; private set; }
        public BreacherBoostDef BoostDef { get; private set; }
        public WritTuning Writ { get; private set; }
        public DecisionLog Decisions { get; private set; }
        public LedgerRun Ledger { get; private set; }
        public MovementController Movement => movement;
        public Camera Camera => cam;
        /// <summary>How far the active gun is aimed, 0 (hip) to 1 (sights up), moved at 0.15 s in and 0.12 s out. Poses and
        /// scales use the eased value (<see cref="JamKit.Skyline.AimDownSights.Ease"/>).</summary>
        public float AimAmount { get; private set; }
        /// <summary>The eased aim, 0..1.</summary>
        public float AimEased => AimDownSights.Ease(AimAmount);
        /// <summary>Aim is held and the gun can be aimed this frame.</summary>
        public bool AimWanted => _aimWanted;
        /// <summary>The active gun's field-of-view scale at this aim (1 at the hip): the viewmodel applies it to the main and
        /// the viewmodel camera.</summary>
        public float AimFovScale => Loadout != null ? AimDownSights.Scale(Loadout.Active.Def.aimFovScale, AimAmount) : 1f;
        public float AimLookScale => Loadout != null ? AimDownSights.Scale(Loadout.Active.Def.aimLookScale, AimAmount) : 1f;
        public float AimMoveScale => Loadout != null ? AimDownSights.Scale(Loadout.Active.Def.aimMoveScale, AimAmount) : 1f;
        public float AimSpreadScale => Loadout != null ? AimDownSights.Scale(Loadout.Active.Def.aimSpreadScale, AimAmount) : 1f;
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
        bool _hitStopWasActive, _hitStopRequested;
        string _assistLogged = "";
        float _assistScaleLogged = 1f;
        Vector3 _spawnFeet;
        float _spawnYaw;
        bool _aimWanted, _cancelSprint;
        string _aimGun = "";

        /// <summary>Skyline's own values for the shared movement camera's speed effects (<see cref="SkylineViewFeel"/>): no
        /// view kick when a sprint starts, and the speed widening only well above sprint speed. Also applied by the player
        /// builder; done here too so an older generated scene gets them. The shared defaults (Visiting Hours) are untouched.</summary>
        public static void ApplyViewFeel(MovementCamera mc)
        {
            if (mc == null) return;
            mc.sprintKickFov = SkylineViewFeel.SprintKickFov;
            mc.fovFromSpeed = SkylineViewFeel.SpeedFovFrom;
            mc.fovFullSpeed = SkylineViewFeel.SpeedFovFull;
            mc.fovRate = SkylineViewFeel.SpeedFovRate;
        }

        void Awake()
        {
            if (movement == null) movement = GetComponent<MovementController>();
            if (cam == null) cam = GetComponentInChildren<Camera>();
            ApplyViewFeel(GetComponent<MovementCamera>());
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

        MoveInput BuildMoveInput(MoveSim sim, float dt)
        {
            // Aim slows the walk by scaling the move vector given to the engine (the engine itself is shared and untouched), and
            // ends a sprint: no sprint while aim is held, and a toggled sprint (the pad's L3) is stopped once by dropping the
            // forward push below the engine's stop line for one step.
            // The engine reads a stick as full from MoveDef.stickFullAt up, so a plain 0.75 would change nothing: the cable asks for
            // 75% of the walk, which is a stick of 0.75 x stickFullAt.
            float k = AimMoveScale * (MoveScale < 1f ? MoveScale * (sim != null ? sim.def.stickFullAt : 0.7f) : 1f);
            float mx = _frame.move.x * k, my = _frame.move.y * k;
            bool sprintHeld = _frame.sprintHeld, sprintToggle = _frame.sprintTogglePressed;
            if (_aimWanted || SprintBlocked) { sprintHeld = false; sprintToggle = false; }
            if (_cancelSprint || (SprintBlocked && sim != null && sim.SprintToggled))
            {
                _cancelSprint = false;
                if (sim != null && sim.SprintToggled) my = Mathf.Min(my, 0.19f);
            }
            return new MoveInput
            {
                move = new NVec2(mx, my),
                yaw = Yaw,
                jumpHeld = _frame.jumpHeld,
                jumpPressed = _frame.jumpPressed,
                crouchHeld = _frame.crouchHeld,
                crouchPressed = _frame.crouchPressed,
                sprintHeld = sprintHeld,
                sprintTogglePressed = sprintToggle,
            };
        }

        void Update()
        {
            float dt = Time.deltaTime;
            float udt = Time.unscaledDeltaTime;
            EnsureOutsidePush();
            // 1. One frame of input. Read before the cursor update, so the click that locks the cursor doesn't also fire
            //    (the device reader ignores the mouse while the cursor is free).
            if (ScriptedInput == null && SkylineDeviceInput.Paused)
            {
                // The pause menu is open: nothing moves, turns, fires or ticks (the menu holds the time scale at 0 and owns the cursor).
                _frame = default(SkylineInputFrame);
                return;
            }
            _frame = ScriptedInput != null ? ScriptedInput() : SkylineDeviceInput.Read(udt);
            UpdateCursor();
            TickAim(udt);

            // 2. Look.
            Look(dt);

            // 3. The boost charge watches the body. (No take-off guard is needed: a braced shot, on the ground or a wall,
            //    never lifts and never spends the charge, so the charge can't be re-armed before a lift has left the ground.)
            var sim = movement != null ? movement.Sim : null;
            if (sim != null) Boost.Observe(sim.Grounded, sim.Mode == MoveMode.Wallrun);

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

        /// <summary>Aim down sights: whether the aim is held and allowed (not while equipping, inspecting or reloading), the
        /// ease, the sprint it ends, and the <c>SKYLINE_AIM</c> log.</summary>
        void TickAim(float udt)
        {
            var gun = Loadout.Active;
            bool can = AimDownSights.CanAim(gun.Equipped, gun.Equipping, gun.Inspecting, gun.State.IsReloading) && !Armor.Wrecked;
            bool want = _frame.aimHeld && can;
            if (want && !_aimWanted)
            {
                _cancelSprint = true;
                _aimGun = gun.Def.id;
                Debug.Log("SKYLINE_AIM " + _aimGun + " in");
            }
            else if (!want && _aimWanted)
            {
                Debug.Log("SKYLINE_AIM " + _aimGun + " out");
            }
            _aimWanted = want;
            AimAmount = AimDownSights.Step(AimAmount, want, Mathf.Min(udt, 0.1f));
        }

        void Look(float dt)
        {
            Vector2 look = _frame.look * AimLookScale;
            if (_frame.lookFromPad && SkylineSettings.Options.aimAssist)
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
            return AimAssist.Compute(new NVec3(eye.x, eye.y, eye.z), Yaw, Pitch, _aimTargets, movingOrTurning, dt, def, AimEased);
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
            var o = SkylineShooting.Fire(def, EyePosition, aim, muzzle, _rng, OnPelletHit, AimSpreadScale);
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
        /// drop it, and settle the charge on that answer. Braced (on the ground, crouched included, or wall-running), the shot is
        /// mostly eaten (BreacherBoostDef.bracedScale of the flat push, no lift). This runs before MovementController (-50 vs
        /// -40) and the engine applies a push at the start of its next step, before it reads the jump: a jump pressed with a
        /// braced shot finds the body still grounded, so it is a normal ground jump and the double jump is kept.</summary>
        void SelfPush(Vector3 aim)
        {
            var sim = movement != null ? movement.Sim : null;
            if (sim == null) return;
            bool braced = sim.Grounded || sim.Mode == MoveMode.Wallrun;
            NVec3 push = BreacherKnockback.RequestSelfPush(new NVec3(aim.x, aim.y, aim.z), sim.Velocity, braced, sim.def.gravity,
                Boost, BoostDef, out bool spends);
            bool ok = sim.AddPush(push);
            Boost.Commit(spends, ok);
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
            SkylineDamageFeedback.Hurt(this, hit, attackerPosition);   // judge round 1: hurt sound, knock, vignette, plating-break beat
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
            Kick.Clear();
            Shake.Clear();
            HitStop.Clear();
            Time.timeScale = 1f;
            _cues.Clear();
            Loadout = new Loadout(new SkylineGun(SkylineGuns.Writ()), new SkylineGun(SkylineGuns.Breacher()), _cues);
            AimAmount = 0f;
            _aimWanted = false;
            if (movement != null) movement.Teleport(feet, yaw, Vector3.zero);
            SetAim(yaw, 0f);
            EnsureOutsidePush();
        }

        // ---------- The cursor ----------

        void UpdateCursor()
        {
            if (Application.isBatchMode || ScriptedInput != null) return;
            // Escape (the Pause binding) no longer just frees the cursor: the pause menu opens and frees it (SkylinePauseMenu).
            if (SkylinePauseMenu.IsOpen) return;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (Cursor.lockState != CursorLockMode.Locked && mouse != null && mouse.leftButton.wasPressedThisFrame)
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
