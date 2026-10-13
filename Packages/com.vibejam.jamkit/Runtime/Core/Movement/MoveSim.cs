using System;
using System.Numerics;
using static JamKit.Movement.MoveMath;

namespace JamKit.Movement
{
    /// <summary>
    /// The Titanfall-style movement simulation: one fixed step at a time (<see cref="MoveDriver"/>
    /// runs it at 120 Hz with an accumulator). States: Grounded/Sprint, Slide, Air, Wallrun, Mantle.
    /// Source accelerate/friction/air-accelerate, jump and double jump, a slide with a boost on a
    /// cooldown, wallruns with Titanfall 2's measured wall speed and wall-jump impulses, mantling,
    /// and the forgiveness timers (coyote, jump buffer, wall coyote, landing friction grace, slide-hop
    /// buffer). Collisions clip the velocity along the hit normals (they never replace it), so
    /// grazing a wall keeps your speed. All the world access goes through <see cref="IMoveWorld"/>.
    /// See docs/MOVEMENT.md. No allocation per step.
    /// </summary>
    public sealed class MoveSim
    {
        public MoveDef def;
        readonly IMoveWorld _world;
        readonly Vector3[] _normals = new Vector3[8];

        Vector3 _pos, _vel;
        MoveMode _mode = MoveMode.Air;
        float _height;
        bool _crouched;
        float _yaw;
        float _time;

        // Ground and jumps
        bool _grounded;
        Vector3 _groundNormal = Up;
        float _sinceGrounded = 999f;
        bool _jumpedSinceGround;
        float _jumpBuffer;
        float _crouchBuffer;
        float _landGrace;
        float _sinceJump = 999f;
        float _sinceHop = 999f;
        float _groundedFor = 999f;
        float _yawRate, _lastYaw;
        bool _hasLastYaw;
        int _jumpChain;
        MoveProfile _profile;
        bool _doubleJump;

        // Slide
        float _slideTime;
        float _lastBoost = -999f;
        float _slideFatigue;

        // Sprint
        bool _sprintToggled;
        float _lastKick = -999f;

        // Wall
        Vector3 _wallN, _wallT;
        int _wallSide;
        float _wallTime, _wallLost;
        float _sinceWall = 999f;
        bool _wallCoyote;
        bool _hasLastWall;
        Vector3 _lastWallN;
        float _lastWallOffset, _lastWallLeaveY;
        bool _groundSinceWall = true;
        float _wallAway, _catchBuffer = -1f;
        WallCatchMiss _catchMiss;
        float _catchMissTime;
        int _catchMisses;

        // Mantle
        Vector3 _mantleStart, _mantleUp, _mantleEnd, _mantleLip, _mantleNormal;
        bool _crouchHeld;
        float _mantleT, _mantleDur, _mantleExit, _mantleStuck;
        // Horizontal speed of the last moments before collisions clipped it: running into a ledge face clips
        // the forward speed a step before the mantle starts, and the mantle should keep the run's momentum.
        float _recentSpeed;

        MoveEvents _events;

        // The outside push (opt-in; Skyline Repo's Breacher, docs/SKYLINE-DESIGN.md 8)
        Vector3 _push;
        bool _pushPending, _pushEnabled;

        public MoveSim(IMoveWorld world, MoveDef def)
        {
            _world = world;
            this.def = def ?? new MoveDef();
            _height = this.def.standHeight;
        }

        // ---------- State, read by the feel layer, the HUD and tests ----------

        public Vector3 Position => _pos;
        public Vector3 Velocity => _vel;
        public float HorizontalSpeed => FlatLength(_vel);
        public MoveMode Mode => _mode;
        public bool Grounded => _grounded;
        public bool Crouched => _crouched || _mode == MoveMode.Slide;
        public float Height => _height;
        public float Yaw => _yaw;
        public float Time => _time;
        public bool DoubleJumpAvailable => _doubleJump;
        /// <summary>+1 wall on the right, -1 left, 0 not on a wall.</summary>
        public int WallSide => _mode == MoveMode.Wallrun ? _wallSide : 0;
        public float WallTime => _mode == MoveMode.Wallrun ? _wallTime : 0f;
        /// <summary>Why the last runnable wall nearby in the air wasn't caught (None after a catch): the catch diagnostic.</summary>
        public WallCatchMiss LastCatchMiss => _catchMiss;
        public float SinceCatchMiss => _time - _catchMissTime;
        /// <summary>Counts refused catches (to notice a new one).</summary>
        public int CatchMisses => _catchMisses;
        public Vector3 WallNormal => _wallN;
        public Vector3 WallTangent => _wallT;
        public float SlideTime => _mode == MoveMode.Slide ? _slideTime : 0f;
        public float SinceGrounded => _sinceGrounded;
        /// <summary>Seconds until the slide boost is ready again (0 = ready).</summary>
        public float SlideBoostCooldown => Math.Max(0f, def.slideBoostCooldown - (_time - _lastBoost));
        /// <summary>The sprint toggle (pad L3) is on.</summary>
        public bool SprintToggled => _sprintToggled;
        /// <summary>Seconds until the next sprint kick can fire (0 = ready).</summary>
        public float KickCooldown => Math.Max(0f, def.kickCooldown - (_time - _lastKick));
        /// <summary>The slide boost cap right now (lower after a lot of sliding).</summary>
        public float SlideBoostCap => Math.Max(def.sprintSpeed, def.slideBoostMaxSpeed - def.slideCapDecay * _slideFatigue);
        public float MantleProgress => _mode == MoveMode.Mantle && _mantleDur > 0f ? _mantleT / _mantleDur : 0f;
        /// <summary>The middle of the ledge's lip being mantled (where the hands plant), and the wall's normal (towards the player).</summary>
        public Vector3 MantleLip => _mantleLip;
        public Vector3 MantleNormal => _mantleNormal;
        /// <summary>Crouch was held on the last step (the body tilts the arms in the air: a slide is coming).</summary>
        public bool CrouchHeld => _crouchHeld;
        /// <summary>How high the ledge being mantled is above the feet where the mantle started (m).</summary>
        public float MantleRise => _mantleLip.Y - _mantleStart.Y;
        /// <summary>Where the eye should be above the feet (the camera blends to it).</summary>
        public float EyeHeightTarget => Crouched ? def.slideEyeHeight : def.eyeHeight;
        /// <summary>The events of the last step.</summary>
        public MoveEvents Events => _events;

        /// <summary>The section's movement profile (abilities, the air strafe, jump fatigue); null allows everything (the dream).</summary>
        public MoveProfile Profile => _profile;
        public void SetProfile(MoveProfile profile) => _profile = profile;
        /// <summary>How many fatigued jumps in a row (0: a fresh jump).</summary>
        public int JumpChain => _jumpChain;
        bool AllowDoubleJump => _profile == null || _profile.doubleJump;
        bool AllowWallrun => _profile == null || _profile.wallrun;
        bool AllowSlide => _profile == null || _profile.slide;
        float AirStrafeScale => _profile == null ? 1f : _profile.airStrafeScale;
        /// <summary>The horizontal speed cap: the def's, or the profile's if it sets a lower one.</summary>
        public float MaxSpeed => _profile != null && _profile.speedCap > 0f ? Math.Min(def.maxHorizontalSpeed, _profile.speedCap) : def.maxHorizontalSpeed;

        /// <summary>Opt-in (Skyline Repo): lets <see cref="AddPush"/> change the velocity. Off by default, so every game that
        /// doesn't turn it on moves exactly as before. Turning it off drops a queued push.</summary>
        public bool OutsidePushEnabled
        {
            get => _pushEnabled;
            set
            {
                _pushEnabled = value;
                if (!value) { _push = Vector3.Zero; _pushPending = false; }
            }
        }

        /// <summary>Queues a velocity change (m/s) from outside the body (the Breacher's self-knockback), applied at the start
        /// of the next step under these rules: an upward part forces leaving the ground and ends a wall-run (with no
        /// wall-coyote jump after it); it is ignored during a mantle; the horizontal speed stays within <see cref="MaxSpeed"/>
        /// (CapFlat); it never refreshes the double jump. Pushes queued before one step add up. Returns true when the push
        /// was queued, false when the engine drops it: <see cref="OutsidePushEnabled"/> is off, the push isn't finite, or
        /// the body is mantling (a caller spends its boost only on true).</summary>
        public bool AddPush(Vector3 deltaV)
        {
            if (!_pushEnabled) return false;
            if (!(IsFinite(deltaV.X) && IsFinite(deltaV.Y) && IsFinite(deltaV.Z))) return false;
            if (_mode == MoveMode.Mantle) return false;
            _push += deltaV;
            _pushPending = true;
            return true;
        }

        static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);

        /// <summary>Puts the body somewhere (spawn, respawn, tests), with a velocity, in the air until it finds ground.</summary>
        public void Teleport(Vector3 feet, float yaw, Vector3 velocity)
        {
            _pos = feet;
            _vel = velocity;
            _yaw = yaw;
            _mode = MoveMode.Air;
            _crouched = false;
            _height = def.standHeight;
            _grounded = false;
            _jumpBuffer = 0f;
            _crouchBuffer = 0f;
            _landGrace = 0f;
            _doubleJump = true;
            _jumpedSinceGround = true;
            _sinceGrounded = 999f;
            _sinceHop = 999f;
            _groundedFor = 999f;
            _jumpChain = 0;
            _hasLastYaw = false;
            _yawRate = 0f;
            _sinceWall = 999f;
            _wallCoyote = false;
            _hasLastWall = false;
            _groundSinceWall = true;
            _slideFatigue = 0f;
            _sprintToggled = false;
            _wallAway = 0f;
            _catchBuffer = -1f;
            _catchMiss = WallCatchMiss.None;
            _lastKick = -999f;
            _events = default;
            _push = Vector3.Zero;
            _pushPending = false;
            if (_world.CastDown(feet, def.radius * 0.9f, def.groundEpsilon + 0.05f, out var g) && g.normal.Y > 0.2f)
            {
                _grounded = true;
                _mode = MoveMode.Grounded;
                _groundNormal = g.normal;
                _sinceGrounded = 0f;
                _jumpedSinceGround = false;
                _vel.Y = 0f;
            }
        }

        // ---------- The step ----------

        public void Step(in MoveInput input, float h)
        {
            if (!(h > 0f)) return;
            _events = default;
            _time += h;
            if (_pushPending) ApplyPush();
            // How fast the view turns (deg/s, smoothed over about 0.06 s: a frame's turn lands on its first step).
            float dyaw = _hasLastYaw ? Math.Abs(DeltaYaw(_lastYaw, input.yaw)) / h : 0f;
            _yawRate += (dyaw - _yawRate) * (1f - (float)Math.Exp(-h / 0.06f));
            _lastYaw = input.yaw;
            _hasLastYaw = true;
            _yaw = input.yaw;
            _crouchHeld = input.crouchHeld;

            Vector3 fwd = Forward(_yaw);
            Vector3 right = RightOf(fwd);
            Vector2 mv = input.move;
            float mag = mv.Length();
            if (mag > 1f) { mv /= mag; mag = 1f; }
            Vector3 wishDir = mag > 1e-3f ? Vector3.Normalize(right * mv.X + fwd * mv.Y) : Vector3.Zero;
            float stick = mag < 1e-3f ? 0f : Math.Min(1f, mag / Math.Max(0.05f, def.stickFullAt));

            if (input.jumpPressed) _jumpBuffer = def.jumpBuffer + h * 0.5f;
            else _jumpBuffer -= h;
            if (input.crouchPressed) _crouchBuffer = def.slideHopBuffer + h * 0.5f;
            else _crouchBuffer -= h;
            _landGrace -= h;
            _sinceJump += h;
            _sinceHop += h;
            _sinceWall += h;
            if (!_grounded) _sinceGrounded += h;
            else _groundedFor += h;
            if (_mode != MoveMode.Slide) _slideFatigue = Math.Max(0f, _slideFatigue - h);
            // The sprint toggle: a click turns it on (clicking again keeps it on); crouching ends it.
            if (input.sprintTogglePressed) _sprintToggled = true;
            if (input.crouchHeld || input.crouchPressed) _sprintToggled = false;

            switch (_mode)
            {
                case MoveMode.Mantle:
                    StepMantle(input, h);
                    return;
                case MoveMode.Wallrun:
                    StepWallrun(input, wishDir, stick, fwd, h);
                    break;
                case MoveMode.Air:
                    StepAir(input, wishDir, stick, fwd, h);
                    break;
                default:
                    StepGround(input, wishDir, stick, mv, fwd, h);
                    break;
            }
            if (_mode == MoveMode.Mantle) return; // started this step; moves from the next

            Integrate(input, h);
        }

        // ---------- The outside push ----------

        void ApplyPush()
        {
            Vector3 p = _push;
            _push = Vector3.Zero;
            _pushPending = false;
            if (_mode == MoveMode.Mantle) return;
            bool doubleJump = _doubleJump;
            if (p.Y > 0f)
            {
                if (_mode == MoveMode.Wallrun)
                {
                    LeaveWall();
                    _wallCoyote = false;
                }
                else if (_mode == MoveMode.Grounded || _mode == MoveMode.Sprint || _mode == MoveMode.Slide)
                {
                    // On the ground StepGround zeroes vertical speed, so an upward push must take off here.
                    if (_mode == MoveMode.Slide) _events.slideEnded = true;
                    _mode = MoveMode.Air;
                    _grounded = false;
                    _jumpedSinceGround = true;   // no coyote jump on top of a push
                    _sinceJump = 0f;
                    _landGrace = 0f;
                    _events.leftGround = true;
                    if (_crouched) SetCrouched(false);
                }
            }
            _vel = CapFlat(_vel + p, MaxSpeed);
            _doubleJump = doubleJump;
        }

        // ---------- Ground ----------

        void StepGround(in MoveInput input, Vector3 wishDir, float stick, Vector2 mv, Vector3 fwd, float h)
        {
            if (_jumpBuffer > 0f)
            {
                GroundJump(_mode == MoveMode.Slide);
                return;
            }

            float speed = HorizontalSpeed;
            if (_mode != MoveMode.Slide && input.crouchPressed && speed >= def.slideMinSpeed && AllowSlide)
            {
                EnterSlide();
                speed = HorizontalSpeed;
            }

            if (_mode == MoveMode.Slide)
            {
                StepSlide(input, wishDir, stick, h);
                return;
            }

            // Crouched walk (crouch held below slide speed); stand up when there's room.
            if (input.crouchHeld) SetCrouched(true);
            else if (_crouched) SetCrouched(false);

            if (TryMantle(wishDir, stick, fwd)) return;

            // Sprint: forward-ish only, and only when asked (held, toggled) unless auto-sprint is on.
            bool forward = mv.Y > def.sprintForwardMin * Math.Max(mv.Length(), 1e-3f);
            if (_sprintToggled && mv.Y < def.sprintToggleStopForward) _sprintToggled = false; // stopped moving forward
            bool sprint = !_crouched && forward && (def.autoSprint || input.sprintHeld || _sprintToggled);
            float wishSpeed = (_crouched ? def.crouchSpeed : sprint ? def.sprintSpeed : def.walkSpeed) * stick;

            Vector3 vH = Flat(_vel);
            speed = vH.Length();

            // The sprint kick: sprinting from well under sprint speed (a stumble, a wall, a mantle) pushes you off
            // straight back toward running speed, never past it, on a cooldown. It also readies the slide boost.
            if (sprint && stick > 0f && speed < def.kickBelow * def.sprintSpeed && _time - _lastKick >= def.kickCooldown)
            {
                float along = Vector3.Dot(vH, wishDir);
                float to = Math.Min(def.kickSpeed, 1f) * def.sprintSpeed * stick;
                if (to > along)
                {
                    vH = wishDir * to; // push off along the stick (the sideways drift goes, so ground acceleration can't stack past sprint)
                    if (vH.Length() > def.sprintSpeed) vH = Vector3.Normalize(vH) * def.sprintSpeed;
                    speed = vH.Length();
                    _lastKick = _time;
                    _events.sprintKick = true;
                    if (def.kickRearmsSlideBoost) _lastBoost = -999f;
                }
            }
            if (stick > 0f && speed > def.sprintSpeed)
            {
                // During the landing grace the excess fades at landingGraceDecay x the rate (0: frozen, the old grace).
                float rate = _landGrace > 0f ? def.overspeedDecay * HopCostPresets.For(def).landingGraceDecay : def.overspeedDecay;
                float target = def.sprintSpeed + (speed - def.sprintSpeed) * (float)Math.Exp(-rate * h);
                Vector3 dir = vH / speed;
                if (Vector3.Dot(dir, wishDir) > 0f)
                    dir = RotateToward(dir, wishDir, def.groundTurnRate * (float)(Math.PI / 180.0) * h * stick);
                vH = dir * target;
            }
            else vH = Friction(vH, def.friction, def.stopSpeed, h);
            if (stick > 0f)
            {
                // Ground acceleration steers and brings you up to the wish speed, never past it: Source's accelerate
                // only caps the projection on the wish direction, so a view kept ahead of the run (a mouse sweep, or
                // a stick at full turn) kept adding speed sideways, to the 22 m/s cap (2026-10-12). Above the wish
                // speed it may turn you, not speed you up.
                float before = vH.Length();
                vH = Accelerate(vH, wishDir, wishSpeed, def.groundAccel, h);
                float limit = Math.Max(before, wishSpeed), after = vH.Length();
                if (after > limit && after > 1e-4f) vH *= limit / after;
            }
            _vel = CapFlat(new Vector3(vH.X, 0f, vH.Z), MaxSpeed);
            _mode = sprint && HorizontalSpeed > def.walkSpeed + 0.5f ? MoveMode.Sprint : MoveMode.Grounded;
        }

        void StepSlide(in MoveInput input, Vector3 wishDir, float stick, float h)
        {
            _slideTime += h;
            _slideFatigue += h;
            Vector3 vH = Flat(_vel);
            float s = vH.Length();
            Vector3 dir = s > 1e-4f ? vH / s : Forward(_yaw);
            float decel = _slideTime < def.slideEarlyTime ? def.slideDecelEarly : def.slideDecel;
            s = Math.Max(0f, s - decel * h);
            if (stick > 0f && Vector3.Dot(dir, wishDir) > -0.5f)
                dir = RotateToward(dir, wishDir, def.slideTurnRate * (float)(Math.PI / 180.0) * h * stick);
            vH = dir * s;
            // Downhill pull on slopes: the horizontal part of gravity along the ground.
            var n = _groundNormal;
            if (n.Y < 0.999f && n.Y > 0.1f)
                vH += new Vector3(n.X, 0f, n.Z) * (def.gravity * n.Y * def.slideSlopeScale * h);
            _vel = CapFlat(new Vector3(vH.X, 0f, vH.Z), MaxSpeed);

            if (!input.crouchHeld || HorizontalSpeed < def.slideExitSpeed)
            {
                _events.slideEnded = true;
                _mode = MoveMode.Grounded;
                _crouched = true;
                if (!input.crouchHeld) SetCrouched(false);
            }
        }

        void EnterSlide()
        {
            _mode = MoveMode.Slide;
            _slideTime = 0f;
            _crouched = true;
            SetHeight(def.slideHeight);
            _events.slideStarted = true;
            if (_time - _lastBoost >= def.slideBoostCooldown)
            {
                Vector3 vH = Flat(_vel);
                float s = vH.Length();
                float cap = SlideBoostCap;
                if (s > 1e-3f && s < cap)
                {
                    float ns = Math.Min(s + def.slideBoost, cap);
                    vH *= ns / s;
                    _vel = new Vector3(vH.X, _vel.Y, vH.Z);
                    _events.slideBoosted = true;
                }
                _lastBoost = _time;
            }
        }

        /// <summary>Jump fatigue (the profile's): a jump soon after landing from the last one is lower; the vertical speed
        /// goes with the square root of the height share. Updates the chain.</summary>
        float FatigueScale()
        {
            if (_profile == null || !_profile.jumpFatigue) { _jumpChain = 0; return 1f; }
            // _groundedFor is the time on the ground before this jump (it holds its value in the air: a coyote jump too).
            _jumpChain = _groundedFor <= _profile.jumpFatigueRecover ? _jumpChain + 1 : 0;
            return (float)Math.Sqrt(_profile.FatigueHeight(_jumpChain));
        }

        void GroundJump(bool fromSlide)
        {
            // The bunny-hop cost: a share of the speed above sprint (smaller out of a slide, so slide hops beat plain hops).
            var hc = HopCostPresets.For(def);
            float loss = fromSlide ? hc.slideHopSpeedLoss : hc.hopSpeedLoss;
            float s = HorizontalSpeed;
            if (loss > 0f && s > def.sprintSpeed)
            {
                float ns = def.sprintSpeed + (s - def.sprintSpeed) * (1f - Math.Min(loss, 1f));
                _vel = new Vector3(_vel.X * ns / s, _vel.Y, _vel.Z * ns / s);
                _events.hopSpeedLost = s - ns;
            }
            _sinceHop = 0f;
            _vel.Y = def.JumpSpeed * (fromSlide ? def.slideJumpScale : 1f) * FatigueScale();
            if (fromSlide) _events.slideEnded = true;
            LeaveGround(true);
            _jumpBuffer = 0f;
            _events.jumped = true;
            if (_crouched) SetCrouched(false);
        }

        void LeaveGround(bool jumped)
        {
            _mode = MoveMode.Air;
            _grounded = false;
            _doubleJump = true;
            if (jumped)
            {
                _jumpedSinceGround = true;
                _sinceJump = 0f;
                _landGrace = 0f;
            }
            else
            {
                _jumpedSinceGround = false;
                _events.leftGround = true;
            }
        }

        // ---------- Air ----------

        void StepAir(in MoveInput input, Vector3 wishDir, float stick, Vector3 fwd, float h)
        {
            if (_jumpBuffer > 0f)
            {
                if (_wallCoyote && _sinceWall <= def.wallCoyote) { WallJump(input, wishDir, stick, true); }
                else if (!_jumpedSinceGround && _sinceGrounded <= def.coyoteTime) { _vel.Y = def.JumpSpeed * FatigueScale(); LeaveGround(true); _jumpBuffer = 0f; _events.jumped = true; }
                else if (_doubleJump && _sinceJump >= def.doubleJumpLockout && AllowDoubleJump) DoubleJump(wishDir, stick, fwd);
            }

            Vector3 vH = Flat(_vel);
            if (stick > 0f)
            {
                // Above airControlSpeed, speed gained in the air needs strafe input (W alone only turns the flight):
                // the gain of each push is scaled by how far sideways the move input points (airStrafeSideShare).
                float share = StrafeShare(input.move) * TurnShare();
                float s0 = FlatLength(vH);
                float strafe = AirStrafeScale;
                vH = LimitGain(AirAccelerate(vH, wishDir, def.sprintSpeed, def.airAccel * strafe, def.airCap * strafe, h), Math.Max(s0, def.airControlSpeed), share);
                float s1 = FlatLength(vH);
                vH = LimitGain(Accelerate(vH, wishDir, def.airControlSpeed * stick, def.airControlAccel, h), Math.Max(s1, def.airControlSpeed), share * def.airControlStrafeGain);
            }
            float vy = Math.Max(_vel.Y - def.gravity * h, -def.terminalFall);
            _vel = CapFlat(new Vector3(vH.X, vy, vH.Z), MaxSpeed);

            // Stand up in the air when there's room (after a slide jump).
            if (_crouched && !input.crouchHeld) SetCrouched(false);

            if (TryMantle(wishDir, stick, fwd)) return;
            _catchBuffer = Math.Max(-1f, _catchBuffer - h);
            TryAttachWall(input, wishDir, stick);
        }

        /// <summary>0..1: how much of the air speed gain the move input earns. Full once it points
        /// <see cref="MoveDef.airStrafeSideShare"/> sideways (A/D, a stick pushed sideways or diagonally), none for forward alone.</summary>
        float StrafeShare(Vector2 move)
        {
            if (!(def.airStrafeSideShare > 0f)) return 1f;
            float m = move.Length();
            if (m < 1e-3f) return 0f;
            return Math.Min(1f, Math.Abs(move.X) / m / def.airStrafeSideShare);
        }

        /// <summary>The turn-rate equalizer: an air-strafe gain made while the view turns faster than
        /// <see cref="MoveDef.airStrafeTurnCap"/> (above any stick's turn) counts only in proportion, so a mouse sweep
        /// strafes no better than the fastest stick.</summary>
        float TurnShare() => def.airStrafeTurnCap > 0f && _yawRate > def.airStrafeTurnCap ? def.airStrafeTurnCap / _yawRate : 1f;

        /// <summary>The view's turn rate now (deg/s, smoothed).</summary>
        public float YawRate => _yawRate;

        static float DeltaYaw(float a, float b)
        {
            float d = (b - a) % 360f;
            if (d > 180f) d -= 360f;
            if (d < -180f) d += 360f;
            return d;
        }

        /// <summary>Keeps <paramref name="v"/>'s direction but lets its speed rise above <paramref name="free"/> only by
        /// <paramref name="share"/> of what it gained (share 1: unchanged; 0: a pure turn above <paramref name="free"/>).</summary>
        static Vector3 LimitGain(Vector3 v, float free, float share)
        {
            if (share >= 1f) return v;
            float s = FlatLength(v);
            if (s <= free) return v;
            float allowed = free + (s - free) * Math.Max(0f, share);
            return v * (allowed / s);
        }

        void DoubleJump(Vector3 wishDir, float stick, Vector3 fwd)
        {
            _vel.Y = Math.Max(_vel.Y, def.DoubleJumpSpeed);
            Vector3 vH = Flat(_vel);
            float s = vH.Length();
            if (stick > 0f)
            {
                Vector3 dir = s > 1e-3f ? vH / s : wishDir;
                Vector3 nd = NormalizeOr(dir + (wishDir - dir) * def.doubleJumpRedirect, dir);
                float ns = Math.Max(s, def.doubleJumpMinSpeed * stick);
                // Hop and steer: a double jump right after a ground jump pays for its turn (a share of the speed above sprint).
                var hc = HopCostPresets.For(def);
                if (_sinceHop <= hc.doubleJumpHopWindow && hc.doubleJumpHopTurnLoss > 0f && ns > def.sprintSpeed)
                {
                    float deg = (float)(Math.Acos(Math.Max(-1f, Math.Min(1f, Vector3.Dot(dir, nd)))) * 180.0 / Math.PI);
                    float turned = Math.Max(0f, deg - hc.doubleJumpHopTurnFree) / 90f;
                    float lost = (ns - def.sprintSpeed) * Math.Min(1f, hc.doubleJumpHopTurnLoss * Math.Min(turned, 1f));
                    ns -= lost;
                    _events.hopSpeedLost += lost;
                }
                // A little forward push along the new direction, up to sprint speed (getting started without a wall).
                if (ns < def.sprintSpeed) ns = Math.Min(def.sprintSpeed, ns + def.doubleJumpForwardBoost * stick);
                vH = nd * ns;
                _vel = new Vector3(vH.X, _vel.Y, vH.Z);
            }
            else if (s > 1e-3f && Vector3.Dot(vH, fwd) > 0f && s < def.sprintSpeed)
            {
                // Stick neutral but moving forward: the push goes along the view, still never past sprint.
                vH += fwd * def.doubleJumpForwardBoost;
                float ns = vH.Length();
                if (ns > def.sprintSpeed) vH *= Math.Max(s, def.sprintSpeed) / ns;
                _vel = new Vector3(vH.X, _vel.Y, vH.Z);
            }
            _doubleJump = false;
            _sinceJump = 0f;
            _jumpBuffer = 0f;
            _events.doubleJumped = true;
        }

        // ---------- Wallrun ----------

        /// <summary>How far past the catch reach a runnable wall is still noticed, so a near miss gets a reason.</summary>
        const float DiagnoseReach = 0.8f;

        /// <summary>Why this runnable wall can't be caught right now (None: it can).</summary>
        WallCatchMiss WallRefusal(in SurfaceHit hit, Vector3 dirH, float speed, float reach, bool low, in MoveInput input, Vector3 wishDir, float stick, out Vector3 t)
        {
            Vector3 n = Vector3.Normalize(Flat(hit.normal));
            t = Vector3.Normalize(Vector3.Cross(Up, n));
            if (Vector3.Dot(t, dirH) < 0f) t = -t;
            if (hit.distance > reach) return WallCatchMiss.OutOfReach;
            if (speed < def.wallMinSpeed) return WallCatchMiss.TooSlow;
            if (input.crouchHeld) return WallCatchMiss.Crouching;
            if (low) return WallCatchMiss.TooLow;
            if (Vector3.Dot(dirH, t) < def.wallMinAlongDot) return WallCatchMiss.HeadOn;
            if (stick <= 0f) return WallCatchMiss.NoStick;
            if (Vector3.Dot(wishDir, -n) < def.wallMinTowardDot) return WallCatchMiss.StickAway;
            if (Vector3.Dot(wishDir, t) < def.wallForwardDot) return WallCatchMiss.StickNotForward;
            return WallCatchMiss.None;
        }

        void NoteCatchMiss(WallCatchMiss why)
        {
            if (why != _catchMiss || _time - _catchMissTime > 0.25f) _catchMisses++;
            _catchMiss = why;
            _catchMissTime = _time;
        }

        void TryAttachWall(in MoveInput input, Vector3 wishDir, float stick)
        {
            if (!AllowWallrun) return;
            Vector3 vH = Flat(_vel);
            float s = vH.Length();
            if (s < 0.5f) return; // no direction to look along
            Vector3 d = vH / s;
            Vector3 r = RightOf(d);
            Vector3 origin = _pos + Up * def.wallProbeHeight;
            // While a catch refused for the stick's direction is buffered, reach a little further for it.
            float reach = def.radius + def.wallProbeReach + (_catchBuffer > 0f ? 0.4f : 0f);
            bool low = _world.CastDown(_pos, def.radius * 0.9f, def.wallMinHeight, out _);
            bool found = false;
            SurfaceHit best = default;
            Vector3 bestT = Vector3.Zero;
            var miss = WallCatchMiss.None;
            float missDist = float.MaxValue;
            for (int i = 0; i < 4; i++)
            {
                Vector3 dir = i == 0 ? r : i == 1 ? -r : i == 2 ? Vector3.Normalize(d + r) : Vector3.Normalize(d - r);
                if (!_world.CastWall(origin, dir, def.wallProbeRadius, reach + DiagnoseReach, out var hit)) continue;
                if (Math.Abs(hit.normal.Y) > def.wallMaxNormalY || !hit.runnable) continue;
                var why = WallRefusal(hit, d, s, reach, low, input, wishDir, stick, out var t);
                if (why != WallCatchMiss.None)
                {
                    if (hit.distance < missDist) { miss = why; missDist = hit.distance; }
                    continue;
                }
                if (found && hit.distance >= best.distance) continue;
                best = hit;
                bestT = t;
                found = true;
            }
            if (!found)
            {
                if (miss == WallCatchMiss.None) return;
                NoteCatchMiss(miss);
                // Refused only for the stick's direction: keep trying a moment longer as the stick comes round.
                bool stickOnly = miss == WallCatchMiss.NoStick || miss == WallCatchMiss.StickAway || miss == WallCatchMiss.StickNotForward;
                if (stickOnly && _catchBuffer < -0.4f) _catchBuffer = def.wallCatchBuffer; // armed at most once per half second
                return;
            }
            Vector3 n = Vector3.Normalize(Flat(best.normal));
            if (_hasLastWall && !_groundSinceWall
                && IsSameWall(def, _lastWallN, _lastWallOffset, n, Vector3.Dot(n, best.point))
                && _pos.Y > _lastWallLeaveY - def.sameWallDrop)
            {
                NoteCatchMiss(WallCatchMiss.SameWall);
                return; // same wall: only lower than where you left it
            }
            _catchMiss = WallCatchMiss.None;
            _catchBuffer = -1f;
            _wallAway = 0f;

            _mode = MoveMode.Wallrun;
            _wallN = n;
            _wallT = bestT;
            _wallTime = 0f;
            _wallLost = 0f;
            _wallSide = Vector3.Dot(n, RightOf(bestT)) < 0f ? 1 : -1;
            vH = bestT * s;
            float vy = _vel.Y > 0f ? _vel.Y : Math.Max(_vel.Y, -def.wallSinkStart);
            _vel = new Vector3(vH.X, vy, vH.Z);
            _doubleJump = true;
            _hasLastWall = true;
            _lastWallN = n;
            _lastWallOffset = Vector3.Dot(n, best.point);
            _lastWallLeaveY = _pos.Y;
            _groundSinceWall = false;
            _wallCoyote = false;
            _events.wallrunStarted = true;
            _events.wallSide = _wallSide;
            if (_jumpBuffer > 0f) WallJump(input, wishDir, stick, false);
        }

        /// <summary>Two wall contacts are the same wall when their normals are less than
        /// <see cref="MoveDef.sameWallAngle"/> apart and their planes within 0.5 m (Titanfall 2: a wall that
        /// curves 25 degrees or more can be caught again).</summary>
        public static bool IsSameWall(MoveDef def, Vector3 n1, float offset1, Vector3 n2, float offset2) =>
            Vector3.Dot(n1, n2) > (float)Math.Cos(def.sameWallAngle * Math.PI / 180.0) && Math.Abs(offset1 - offset2) < 0.5f;

        void StepWallrun(in MoveInput input, Vector3 wishDir, float stick, Vector3 fwd, float h)
        {
            _wallTime += h;
            if (_jumpBuffer > 0f) { WallJump(input, wishDir, stick, false); return; }
            if (input.crouchPressed) { LeaveWall(); return; }
            // Titanfall 2: pushing the stick away from the wall lets go (here after a few hundredths of a second, so
            // a controller stick sweeping round doesn't drop you).
            if (stick > 0f && Vector3.Dot(wishDir, _wallN) > def.wallAwayDot)
            {
                _wallAway += h;
                if (_wallAway >= def.wallAwayTime) { LeaveWall(); return; }
            }
            else _wallAway = 0f;

            Vector3 origin = _pos + Up * def.wallProbeHeight;
            if (_world.CastWall(origin, -_wallN, def.wallProbeRadius, def.radius + def.wallProbeReach + 0.2f, out var hit)
                && Math.Abs(hit.normal.Y) <= def.wallMaxNormalY && hit.runnable
                && Vector3.Dot(Vector3.Normalize(Flat(hit.normal)), _wallN) > 0.7f)
            {
                Vector3 n = Vector3.Normalize(Flat(hit.normal));
                Vector3 t = Vector3.Normalize(Vector3.Cross(Up, n));
                if (Vector3.Dot(t, _wallT) < 0f) t = -t;
                _wallN = n;
                _wallT = t;
                _wallLost = 0f;
                _lastWallLeaveY = Math.Min(_lastWallLeaveY, _pos.Y);
            }
            else _wallLost += h;

            float along = Vector3.Dot(_vel, _wallT);
            // Forward: any stick with a forward part along the wall, or held at an angle while the view looks along
            // it (a controller stick rarely points exactly along the wall). Only clear backward input brakes hard.
            float stickAlong = stick > 0f ? Vector3.Dot(wishDir, _wallT) : 0f;
            bool backward = stick > 0f && stickAlong < -0.3f;
            bool forwardHeld = stick > 0f && !backward
                && (stickAlong >= def.wallHoldForwardDot || Vector3.Dot(fwd, _wallT) >= def.wallHoldForwardDot);
            if (forwardHeld)
            {
                if (along < def.wallRunSpeed) along = Math.Min(def.wallRunSpeed, along + def.wallRunAccel * h);
                else if (_wallTime > def.wallSpeedGrace) // the grace: the first moments on a wall keep all your speed
                    along = def.wallRunSpeed + (along - def.wallRunSpeed) * (float)Math.Exp(-def.wallRunFriction * h);
            }
            else
            {
                // No stick: friction toward 0 (Titanfall 2); clear backward input: twice as hard.
                float k = backward ? 2f : 1f;
                float drop = Math.Max(Math.Abs(along), def.stopSpeed) * def.wallNeutralFriction * k * h;
                along = Math.Max(0f, along - drop);
            }

            float T = def.wallRunDuration;
            float vy = _vel.Y;
            if (vy > 0f) vy -= def.gravity * h; // a jump carries you up the wall at full gravity
            else
            {
                float g = Lerp(def.wallGravityStart, def.wallGravityEnd, SmoothStep(0.2f, T, _wallTime));
                vy = Math.Max(vy - def.gravity * g * h, -Lerp(def.wallSinkStart, def.wallSinkEnd, Clamp01(_wallTime / T)));
            }
            if (along > def.maxHorizontalSpeed) along = def.maxHorizontalSpeed;
            _vel = _wallT * along + Up * vy - _wallN * def.wallStick;

            if (TryMantle(wishDir, stick, fwd)) return;
            if (_wallTime >= T || _wallLost > def.wallLostGrace || along < def.wallExitSpeed) LeaveWall();
        }

        void LeaveWall()
        {
            _mode = MoveMode.Air;
            _sinceWall = 0f;
            _wallCoyote = true;
            _lastWallLeaveY = Math.Min(_lastWallLeaveY, _pos.Y);
            _events.wallrunEnded = true;
            _vel += _wallN * def.wallStick; // drop the push into the wall
        }

        /// <summary>Titanfall 2's measured wall jump: the run's velocity, plus 5.21 m/s along the normal,
        /// plus up to 1.91 m/s along the wall from the stick, minus up to 4.37 m/s when the stick points
        /// into the wall, plus the crouch kick.</summary>
        void WallJump(in MoveInput input, Vector3 wishDir, float stick, bool coyote)
        {
            Vector3 n = _wallN, t = _wallT;
            bool kick = !coyote && _wallTime <= def.wallKickWindow;
            if (_mode == MoveMode.Wallrun) _events.wallrunEnded = true;
            Vector3 vH = Flat(_vel);
            if (Vector3.Dot(vH, n) < 0f) vH -= n * Vector3.Dot(vH, n); // drop the push into the wall
            float outward = def.wallJumpOut;
            if (stick > 0f)
            {
                vH += t * (def.wallJumpAlong * Vector3.Dot(wishDir, t) * stick);
                float into = Vector3.Dot(wishDir, -n);
                if (into > 0f) outward -= def.wallJumpInward * into * stick;
            }
            if (input.crouchHeld) outward += def.wallJumpCrouchKick;
            vH += n * outward;
            _vel = CapFlat(new Vector3(vH.X, def.wallJumpUp, vH.Z), MaxSpeed);
            _mode = MoveMode.Air;
            _grounded = false;
            _lastWallLeaveY = Math.Min(_lastWallLeaveY, _pos.Y);
            _wallCoyote = false;
            _sinceWall = 999f;
            _doubleJump = true;
            _jumpedSinceGround = true;
            _sinceJump = 0f;
            _jumpBuffer = 0f;
            _events.wallJumped = true;
            _events.wallKick = kick;
        }

        // ---------- Mantle ----------

        bool TryMantle(Vector3 wishDir, float stick, Vector3 fwd)
        {
            if (stick <= 0f || Vector3.Dot(wishDir, fwd) < 0.5f) return false;
            if (_vel.Y < -def.mantleMaxFall) return false;
            float reach = def.radius + def.mantleReach;
            SurfaceHit face = default;
            bool hitFace = false;
            for (int i = 0; i < 2 && !hitFace; i++)
            {
                float y = i == 0 ? 0.45f : 1.25f;
                if (_world.CastWall(_pos + Up * y, fwd, def.wallProbeRadius, reach, out var hit)
                    && Math.Abs(hit.normal.Y) < 0.3f && Vector3.Dot(Flat(hit.normal), fwd) < -0.5f)
                {
                    face = hit;
                    hitFace = true;
                }
            }
            if (!hitFace) return false;
            Vector3 n = Vector3.Normalize(Flat(face.normal));
            Vector3 probe = new Vector3(face.point.X, _pos.Y + def.mantleMaxHeight + 0.25f, face.point.Z) - n * 0.35f;
            if (!_world.CastLedge(probe, def.mantleMaxHeight + 0.25f, out var top) || top.normal.Y < 0.7f) return false;
            float rise = top.point.Y - _pos.Y;
            if (rise < def.mantleMinHeight || rise > def.mantleMaxHeight) return false;
            Vector3 end = new Vector3(face.point.X, top.point.Y + 0.02f, face.point.Z) - n * (def.radius + 0.15f);
            if (!_world.CapsuleFits(end + Up * 0.03f, def.radius, def.standHeight)) return false;
            if (!_world.CapsuleFits(new Vector3(_pos.X, top.point.Y + 0.05f, _pos.Z), def.radius * 0.95f, Math.Max(_height, def.slideHeight))) return false;

            _mode = MoveMode.Mantle;
            _mantleStart = _pos;
            _mantleUp = new Vector3(_pos.X, top.point.Y + 0.05f, _pos.Z);
            _mantleEnd = end;
            _mantleLip = new Vector3(face.point.X, top.point.Y, face.point.Z);
            _mantleNormal = n;
            _mantleT = 0f;
            _mantleStuck = 0f;
            _mantleDur = def.mantleBaseTime + def.mantlePerMetre * rise;
            _mantleExit = Math.Max(Math.Max(HorizontalSpeed, _recentSpeed) * def.mantleSpeedKeep, def.mantleExitSpeed);
            _vel = Vector3.Zero;
            _grounded = false;
            _doubleJump = true;
            _events.mantleStarted = true;
            if (_crouched) SetCrouched(false);
            return true;
        }

        void StepMantle(in MoveInput input, float h)
        {
            _mantleT += h;
            float k = Clamp01(_mantleT / _mantleDur);
            const float split = 0.6f;
            Vector3 target = k < split
                ? Vector3.Lerp(_mantleStart, _mantleUp, SmoothStep(0f, 1f, k / split))
                : Vector3.Lerp(_mantleUp, _mantleEnd, SmoothStep(0f, 1f, (k - split) / (1f - split)));
            Vector3 before = _pos;
            _pos = _world.Move(_pos, target - _pos, def.radius, _height, _normals, out _);
            if ((target - _pos).Length() > 0.25f) _mantleStuck += h;
            Vector3 dirH = NormalizeOr(Flat(_mantleEnd - _mantleStart), Forward(_yaw));
            if (k >= 1f || _mantleStuck > 0.12f)
            {
                _vel = dirH * _mantleExit;
                bool ground = _world.CastDown(_pos, def.radius * 0.9f, def.groundEpsilon + 0.05f, out var g) && g.normal.Y > 0.2f;
                if (ground)
                {
                    _mode = MoveMode.Grounded;
                    _grounded = true;
                    _groundNormal = g.normal;
                    _sinceGrounded = 0f;
                    _jumpedSinceGround = false;
                    _groundSinceWall = true;
                    _landGrace = def.landingGrace;
                }
                else LeaveGround(false);
            }
            else _vel = (_pos - before) / h;
        }

        // ---------- Move and collide ----------

        void Integrate(in MoveInput input, float h)
        {
            bool wasGround = _mode == MoveMode.Grounded || _mode == MoveMode.Sprint || _mode == MoveMode.Slide;
            float preVy = _vel.Y;
            _recentSpeed = Math.Max(HorizontalSpeed, _recentSpeed * (float)Math.Exp(-h / 0.15f));
            _pos = _world.Move(_pos, _vel * h, def.radius, _height, _normals, out int hits);
            for (int i = 0; i < hits && i < _normals.Length; i++)
            {
                Vector3 n = _normals[i];
                if (wasGround && n.Y >= def.WalkableMinNormalY) continue; // the floor: velocity stays horizontal
                _vel = Clip(_vel, n);
            }

            float walk = def.WalkableMinNormalY;
            bool rising = _vel.Y > 0.5f;
            if (wasGround && !rising)
            {
                // Stay on the ground over slopes and small drops.
                if (_world.CastDown(_pos, def.radius * 0.9f, def.groundSnap, out var g) && g.normal.Y > 0.2f)
                {
                    if (g.distance > 0.002f) _pos = _world.Move(_pos, new Vector3(0f, -g.distance, 0f), def.radius, _height, _normals, out _);
                    _groundNormal = g.normal.Y >= walk ? g.normal : Up;
                    _grounded = true;
                    _sinceGrounded = 0f;
                    _vel.Y = 0f;
                }
                else
                {
                    if (_mode == MoveMode.Slide) _events.slideEnded = true;
                    LeaveGround(false);
                    _sinceGrounded = 0f;
                }
                return;
            }

            if (!rising && _world.CastDown(_pos, def.radius * 0.9f, def.groundEpsilon, out var land) && land.normal.Y > 0.2f)
                Land(input, land, -preVy);
        }

        void Land(in MoveInput input, in SurfaceHit ground, float impact)
        {
            if (_mode == MoveMode.Wallrun) _events.wallrunEnded = true;
            _grounded = true;
            _groundNormal = ground.normal.Y >= def.WalkableMinNormalY ? ground.normal : Up;
            _sinceGrounded = 0f;
            _jumpedSinceGround = false;
            _doubleJump = true;
            _groundSinceWall = true;
            _wallCoyote = false;
            _vel.Y = 0f;
            _events.landed = true;
            _events.landImpact = Math.Max(0f, impact);
            _groundedFor = 0f;
            if ((input.crouchHeld || _crouchBuffer > 0f) && HorizontalSpeed >= def.slideMinSpeed && AllowSlide)
            {
                EnterSlide(); // slide hop: no landing friction, a boost if it's ready
                return;
            }
            _mode = MoveMode.Grounded;
            _landGrace = def.landingGrace;
        }

        // ---------- Capsule ----------

        void SetCrouched(bool crouch)
        {
            if (crouch)
            {
                _crouched = true;
                SetHeight(def.slideHeight);
                return;
            }
            if (_world.CapsuleFits(_pos + Up * 0.02f, def.radius * 0.95f, def.standHeight))
            {
                _crouched = false;
                SetHeight(def.standHeight);
            }
        }

        void SetHeight(float h) => _height = h;
    }
}
