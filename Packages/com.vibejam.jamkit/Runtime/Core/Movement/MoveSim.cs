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
        bool _doubleJump;

        // Slide
        float _slideTime;
        float _lastBoost = -999f;
        float _slideFatigue;

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

        // Mantle
        Vector3 _mantleStart, _mantleUp, _mantleEnd;
        float _mantleT, _mantleDur, _mantleExit, _mantleStuck;

        MoveEvents _events;

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
        public Vector3 WallNormal => _wallN;
        public Vector3 WallTangent => _wallT;
        public float SlideTime => _mode == MoveMode.Slide ? _slideTime : 0f;
        public float SinceGrounded => _sinceGrounded;
        /// <summary>Seconds until the slide boost is ready again (0 = ready).</summary>
        public float SlideBoostCooldown => Math.Max(0f, def.slideBoostCooldown - (_time - _lastBoost));
        /// <summary>The slide boost cap right now (lower after a lot of sliding).</summary>
        public float SlideBoostCap => Math.Max(def.sprintSpeed, def.slideBoostMaxSpeed - def.slideCapDecay * _slideFatigue);
        public float MantleProgress => _mode == MoveMode.Mantle && _mantleDur > 0f ? _mantleT / _mantleDur : 0f;
        /// <summary>Where the eye should be above the feet (the camera blends to it).</summary>
        public float EyeHeightTarget => Crouched ? def.slideEyeHeight : def.eyeHeight;
        /// <summary>The events of the last step.</summary>
        public MoveEvents Events => _events;

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
            _sinceWall = 999f;
            _wallCoyote = false;
            _hasLastWall = false;
            _groundSinceWall = true;
            _slideFatigue = 0f;
            _events = default;
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
            _yaw = input.yaw;

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
            _sinceWall += h;
            if (!_grounded) _sinceGrounded += h;
            if (_mode != MoveMode.Slide) _slideFatigue = Math.Max(0f, _slideFatigue - h);

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

        // ---------- Ground ----------

        void StepGround(in MoveInput input, Vector3 wishDir, float stick, Vector2 mv, Vector3 fwd, float h)
        {
            if (_jumpBuffer > 0f)
            {
                GroundJump(_mode == MoveMode.Slide);
                return;
            }

            float speed = HorizontalSpeed;
            if (_mode != MoveMode.Slide && input.crouchPressed && speed >= def.slideMinSpeed)
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

            bool sprint = !_crouched && (def.autoSprint ? mv.Y > 0.3f * Math.Max(mv.Length(), 1e-3f) : input.sprintHeld);
            float wishSpeed = (_crouched ? def.crouchSpeed : sprint ? def.sprintSpeed : def.walkSpeed) * stick;

            Vector3 vH = Flat(_vel);
            speed = vH.Length();
            if (_landGrace > 0f && stick > 0f) { }
            else if (stick > 0f && speed > def.sprintSpeed)
            {
                float target = def.sprintSpeed + (speed - def.sprintSpeed) * (float)Math.Exp(-def.overspeedDecay * h);
                vH *= target / speed;
            }
            else vH = Friction(vH, def.friction, def.stopSpeed, h);
            if (stick > 0f) vH = Accelerate(vH, wishDir, wishSpeed, def.groundAccel, h);
            _vel = CapFlat(new Vector3(vH.X, 0f, vH.Z), def.maxHorizontalSpeed);
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
            _vel = CapFlat(new Vector3(vH.X, 0f, vH.Z), def.maxHorizontalSpeed);

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

        void GroundJump(bool fromSlide)
        {
            _vel.Y = def.JumpSpeed * (fromSlide ? def.slideJumpScale : 1f);
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
                else if (!_jumpedSinceGround && _sinceGrounded <= def.coyoteTime) { _vel.Y = def.JumpSpeed; LeaveGround(true); _jumpBuffer = 0f; _events.jumped = true; }
                else if (_doubleJump && _sinceJump >= def.doubleJumpLockout) DoubleJump(wishDir, stick);
            }

            Vector3 vH = Flat(_vel);
            if (stick > 0f)
            {
                vH = AirAccelerate(vH, wishDir, def.sprintSpeed, def.airAccel, def.airCap, h);
                vH = Accelerate(vH, wishDir, def.airControlSpeed * stick, def.airControlAccel, h);
            }
            float vy = Math.Max(_vel.Y - def.gravity * h, -def.terminalFall);
            _vel = CapFlat(new Vector3(vH.X, vy, vH.Z), def.maxHorizontalSpeed);

            // Stand up in the air when there's room (after a slide jump).
            if (_crouched && !input.crouchHeld) SetCrouched(false);

            if (TryMantle(wishDir, stick, fwd)) return;
            TryAttachWall(input, wishDir, stick);
        }

        void DoubleJump(Vector3 wishDir, float stick)
        {
            _vel.Y = Math.Max(_vel.Y, def.DoubleJumpSpeed);
            Vector3 vH = Flat(_vel);
            float s = vH.Length();
            if (stick > 0f)
            {
                Vector3 dir = s > 1e-3f ? vH / s : wishDir;
                Vector3 nd = NormalizeOr(dir + (wishDir - dir) * def.doubleJumpRedirect, dir);
                float ns = Math.Max(s, def.doubleJumpMinSpeed * stick);
                vH = nd * ns;
                _vel = new Vector3(vH.X, _vel.Y, vH.Z);
            }
            _doubleJump = false;
            _sinceJump = 0f;
            _jumpBuffer = 0f;
            _events.doubleJumped = true;
        }

        // ---------- Wallrun ----------

        bool WallOk(in SurfaceHit hit, Vector3 dirH, Vector3 wishDir, float stick, out Vector3 t)
        {
            t = Vector3.Zero;
            if (Math.Abs(hit.normal.Y) > def.wallMaxNormalY || !hit.runnable) return false;
            Vector3 n = Vector3.Normalize(Flat(hit.normal));
            t = Vector3.Normalize(Vector3.Cross(Up, n));
            if (Vector3.Dot(t, dirH) < 0f) t = -t;
            if (Vector3.Dot(dirH, t) < def.wallMinAlongDot) return false;
            if (stick <= 0f) return false;
            if (Vector3.Dot(wishDir, -n) < def.wallMinTowardDot) return false;
            if (Vector3.Dot(wishDir, t) < def.wallForwardDot) return false;
            return true;
        }

        void TryAttachWall(in MoveInput input, Vector3 wishDir, float stick)
        {
            Vector3 vH = Flat(_vel);
            float s = vH.Length();
            if (s < def.wallMinSpeed || input.crouchHeld) return;
            if (_world.CastDown(_pos, def.radius * 0.9f, def.wallMinHeight, out _)) return;
            Vector3 d = vH / s;
            Vector3 r = RightOf(d);
            Vector3 origin = _pos + Up * def.wallProbeHeight;
            float reach = def.radius + def.wallProbeReach;
            bool found = false;
            SurfaceHit best = default;
            Vector3 bestT = Vector3.Zero;
            for (int i = 0; i < 4; i++)
            {
                Vector3 dir = i == 0 ? r : i == 1 ? -r : i == 2 ? Vector3.Normalize(d + r) : Vector3.Normalize(d - r);
                if (!_world.CastWall(origin, dir, def.wallProbeRadius, reach, out var hit)) continue;
                if (!WallOk(hit, d, wishDir, stick, out var t)) continue;
                if (found && hit.distance >= best.distance) continue;
                best = hit;
                bestT = t;
                found = true;
            }
            if (!found) return;
            Vector3 n = Vector3.Normalize(Flat(best.normal));
            if (_hasLastWall && !_groundSinceWall
                && IsSameWall(def, _lastWallN, _lastWallOffset, n, Vector3.Dot(n, best.point))
                && _pos.Y > _lastWallLeaveY - def.sameWallDrop)
                return; // same wall: only lower than where you left it

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
            // Titanfall 2: pushing the stick away from the wall lets go at once.
            if (stick > 0f && Vector3.Dot(wishDir, _wallN) > def.wallAwayDot) { LeaveWall(); return; }

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
            float forwardInput = stick > 0f ? Vector3.Dot(wishDir, _wallT) * stick : 0f;
            if (forwardInput >= def.wallForwardDot * 0.5f)
            {
                if (along < def.wallRunSpeed) along = Math.Min(def.wallRunSpeed, along + def.wallRunAccel * h);
                else along = def.wallRunSpeed + (along - def.wallRunSpeed) * (float)Math.Exp(-def.wallRunFriction * h);
            }
            else
            {
                // Neutral or into the wall: friction toward 0; backwards: twice as hard.
                float k = forwardInput < -def.wallForwardDot * 0.5f ? 2f : 1f;
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
            _vel = CapFlat(new Vector3(vH.X, def.wallJumpUp, vH.Z), def.maxHorizontalSpeed);
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
            _mantleT = 0f;
            _mantleStuck = 0f;
            _mantleDur = def.mantleBaseTime + def.mantlePerMetre * rise;
            _mantleExit = Math.Max(HorizontalSpeed * def.mantleSpeedKeep, def.mantleExitSpeed);
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
            if ((input.crouchHeld || _crouchBuffer > 0f) && HorizontalSpeed >= def.slideMinSpeed)
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
