using System;
using System.Numerics;

namespace JamKit.Movement
{
    /// <summary>
    /// Every number of the first-person body's feel (docs/MOVEMENT.md "The body", RESEARCH-MOVEMENT 8.1 and 8.4).
    /// Lives in the movement tuning asset next to <see cref="MoveDef"/>, so it can be tuned in Play mode and
    /// changed when research says otherwise. Arm positions are in camera space (x right, y up, z forward,
    /// metres from the eye); leg positions in body space (x right, y up from the soles, z forward).
    /// "x Motion" marks the effects that scale with the player's Motion effects setting (0 turns them off);
    /// contact cues (the wall hand, the ledge hands, the slide pose and legs) never scale.
    /// </summary>
    [Serializable]
    public class BodyFeelDef
    {
        // ---- Arms at rest ----
        /// <summary>Where a hand rests (camera space, the right hand; the left mirrors x).</summary>
        public float restX = 0.21f, restY = -0.36f, restZ = 0.36f;
        /// <summary>Where the arms' placeholder shoulders sit (camera space), for the capsule fallback.</summary>
        public float shoulderWidth = 0.24f, shoulderDrop = 0.30f;
        /// <summary>The hands' spring toward their pose (rad/s, critically damped). Higher = snappier.</summary>
        public float armSpring = 18f;
        /// <summary>Hands in the air (camera space, the right hand).</summary>
        public float airX = 0.29f, airY = -0.27f, airZ = 0.33f;

        // ---- The pump (8.1 #1): in step with the footfalls, zero in the air. x Motion ----
        /// <summary>Forward and back swing of each hand at full run (m). 8.1: about 4 cm.</summary>
        public float pumpForward = 0.04f;
        /// <summary>Rise of the forward hand (m). 8.1: about 1.5 cm.</summary>
        public float pumpUp = 0.015f;
        /// <summary>Hands drift in towards the middle at speed (m).</summary>
        public float pumpInward = 0.02f;
        /// <summary>The pump tightens at the faster tiers (multipliers at "fast" and "flying").</summary>
        public float pumpScaleFast = 0.75f, pumpScaleFlying = 0.45f;

        // ---- Speed tiers in the posture (8.4: running under 8, fast 8-13, flying over 13 m/s). x Motion ----
        public float tierFast = 8f, tierFlying = 13f;
        /// <summary>Half the width of the blend around each tier boundary (m/s).</summary>
        public float tierBlend = 1f;
        /// <summary>Forward lean of the torso at "fast" and at "flying" (degrees).</summary>
        public float leanFast = 3f, leanFlying = 8f;
        /// <summary>At "flying" the arms sweep back and down (m).</summary>
        public float flyingSweepBack = 0.12f, flyingSweepDown = 0.04f;
        /// <summary>Lean into turns: degrees per m/s^2 of sideways acceleration, the limit, the smoothing rate (1/s).</summary>
        public float turnLeanPerAccel = 0.45f, turnLeanMax = 5f, turnLeanRate = 6f;

        // ---- Weight: the arms trail the view (x Motion) ----
        /// <summary>Sway degrees per degree-per-second of look turn, the limit (degrees), the spring (rad/s) and damping ratio.</summary>
        public float swayPerDegPerSec = 0.012f, swayMax = 4f, swayFrequency = 10f, swayDamping = 0.7f;

        // ---- Landing (8.1 #5): the body takes the camera's dip a moment later. x Motion ----
        /// <summary>How much later than the camera the body dips (s). 8.1: about 0.03 s.</summary>
        public float landLag = 0.03f;
        /// <summary>A hard landing (impact over this, m/s) dips the body a little further (m), on its own spring (rad/s).</summary>
        public float hardLanding = 12f, hardLandingExtra = 0.02f, hardLandingFrequency = 16f;

        // ---- Anticipation and follow-through (x Motion) ----
        /// <summary>Jump: the arms lift (m; 8.1: 3-5 cm), easing back at this rate (1/s).</summary>
        public float jumpLift = 0.04f, jumpLiftDecay = 3f;
        /// <summary>Double jump: the "push off the air" sweep down and back (8.1: 0.15 s) and its pose offsets (m).</summary>
        public float doubleJumpTime = 0.15f, doubleJumpDown = 0.08f, doubleJumpBack = 0.24f;
        /// <summary>Double jump: the legs tuck for this long (s).</summary>
        public float tuckTime = 0.35f;
        /// <summary>Sprint kick (plant and shove): the trailing hand is driven back and down (m/s), the leading one forward (m/s);
        /// the torso leans in (degrees) and recovers on a spring (rad/s).</summary>
        public float kickArmBack = 1.8f, kickArmDown = 0.3f, kickArmForward = 0.5f, kickLean = 6f, kickLeanFrequency = 9f;

        // ---- Contact (never scaled) ----
        /// <summary>The wall hand lands on its spot in this long (s): within 2 frames at 60 fps. The timing cue for wall kicks.</summary>
        public float wallCatchTime = 0.03f;
        /// <summary>The wall hand's spot: the wall point beside the shoulder plus this far ahead along the run (8.1: 0.35 m), at this camera height (m).</summary>
        public float wallHandAhead = 0.35f, wallHandHeight = -0.06f;
        /// <summary>Wall kick: the wall hand shoves off the wall on the kick (m/s, x Motion).</summary>
        public float wallPushOff = 2.2f;
        /// <summary>Slide: the hands drop and spread (camera space, the right hand).</summary>
        public float slideX = 0.36f, slideY = -0.38f, slideZ = 0.26f;
        /// <summary>Slide: above this speed (m/s) the left hand trails near the ground, fully by +4 m/s (camera space).</summary>
        public float slideTrailSpeed = 12f, slideTrailX = -0.42f, slideTrailY = -0.66f, slideTrailZ = 0.08f;
        /// <summary>Slide legs (body space): the feet rise into the bottom of the screen; the left (leading) leg further.</summary>
        public float slideFootForward = 0.95f, slideFootHeight = 0.3f, slideKneeForward = 0.5f, slideKneeHeight = 0.48f, slideLead = 0.06f;
        /// <summary>Slide: the torso reclines this far (degrees), so a real body's feet reach out in front.</summary>
        public float slideRecline = 30f;
        /// <summary>Mantle: the hands plant this far either side of the middle of the ledge lip (m).</summary>
        public float mantleHandSpread = 0.22f;

        // ---- Legs (the stride is the controller's: each whole step of its phase is a footfall) ----
        /// <summary>How much of each foot's two-step cycle it spends on the ground (phase units, 0..2).</summary>
        public float stanceFraction = 0.7f;
        /// <summary>Foot lift in the swing (m) and its growth with speed (m per m/s), up to the limit.</summary>
        public float footLift = 0.12f, footLiftPerSpeed = 0.012f, footLiftMax = 0.3f;
        /// <summary>How far the feet travel forward and back, as a share of the real stride (a real body's legs are
        /// shorter than the stride at speed; the plant timing stays the footsteps').</summary>
        public float gaitStrideScale = 0.55f;
        /// <summary>Half the distance between the feet (m).</summary>
        public float hipWidth = 0.1f;
        /// <summary>Double-jump tuck and mantle feet (body space).</summary>
        public float tuckFootHeight = 0.5f, tuckFootForward = 0.18f;

        // ---- Secondary motion (x Motion) ----
        /// <summary>Breathing at rest: the period (s) and how far the arms rise (m).</summary>
        public float breathPeriod = 3.6f, breathArms = 0.006f;
        /// <summary>The lanyard swings: degrees per m/s^2, the limit, the spring (rad/s) and a low damping ratio.</summary>
        public float lanyardPerAccel = 1.5f, lanyardMax = 25f, lanyardFrequency = 6f, lanyardDamping = 0.25f;
    }

    /// <summary>One frame of what the body needs to know (filled by the Unity side).</summary>
    public struct BodyFeelInput
    {
        public float dt;
        public MoveMode mode;
        public float speed;
        public float sprintSpeed;
        /// <summary>Velocity in the body's frame (x right, y up, z forward).</summary>
        public Vector3 velocityBody;
        /// <summary>How far the view turned this frame (degrees; yaw right +, pitch down +).</summary>
        public float lookYaw, lookPitch;
        public MoveEvents events;
        /// <summary>The controller's stride phase (each whole number is a footfall).</summary>
        public float stridePhase;
        public int wallSide;
        /// <summary>The wall hand's spot (camera space), when a wall is found beside the shoulder.</summary>
        public bool hasWallTarget;
        public Vector3 wallHandTarget;
        /// <summary>The ledge lip's hand spots (camera space, left then right), during a mantle.</summary>
        public bool hasLedge;
        public Vector3 ledgeLeft, ledgeRight;
        /// <summary>The player's Motion effects setting, 0..1.</summary>
        public float motion;
        /// <summary>The camera's own landing dip this frame (m, negative down).</summary>
        public float cameraDip;
    }

    /// <summary>The body's pose this frame. Arms in camera space, legs in body space.</summary>
    public sealed class BodyFeelPose
    {
        public readonly Vector3[] hand = new Vector3[2];
        /// <summary>Which way the elbow points (camera space, unit).</summary>
        public readonly Vector3[] elbowPole = new Vector3[2];
        /// <summary>The hand is on something (the wall, a ledge): its palm faces out and its position is exact.</summary>
        public readonly bool[] contact = new bool[2];
        /// <summary>Where each foot is (body space) and which way each knee points.</summary>
        public readonly Vector3[] foot = new Vector3[2];
        public readonly Vector3[] kneePole = new Vector3[2];
        public readonly bool[] footPlanted = new bool[2];
        /// <summary>The legs' slide (or tuck) pose weight, 0..1 (the placeholder shows its legs only then).</summary>
        public float legs;
        /// <summary>Torso lean forward and into turns (degrees).</summary>
        public float torsoPitch, torsoRoll;
        /// <summary>Where the body is relative to the camera's dip (m; the arms add it so they dip a moment later).</summary>
        public float armDipOffset;
        /// <summary>The body's own landing dip (m, world, negative down): the camera's, delayed.</summary>
        public float bodyDip;
        /// <summary>The arms' trailing sway (degrees).</summary>
        public float swayYaw, swayPitch;
        /// <summary>The lanyard's swing (degrees forward and sideways).</summary>
        public float lanyardPitch, lanyardRoll;
        /// <summary>Breath 0..1 at rest.</summary>
        public float breath;
        /// <summary>0 running, 1 fast, 2 flying (RESEARCH-MOVEMENT 8.4).</summary>
        public int tier;
        /// <summary>The pump's amplitude right now (m, forward and back), 0 in the air.</summary>
        public float pump;
        /// <summary>The tier blends: 0..1 into "fast" and into "flying".</summary>
        public float fast, flying;
    }

    /// <summary>
    /// The first-person body's feel as springs and poses, in plain C# (tested without Unity): the hands
    /// pump in step with the footfalls, lift on a jump, sweep down and back on a double jump, plant on the
    /// wall exactly as a wallrun is granted and drag along it, shove on a wall kick and a sprint kick,
    /// drop and spread on a slide with the feet rising into view, plant on a ledge; the arms trail the
    /// view on turns and take the landing a moment after the camera; the posture leans with the speed
    /// tier and into turns; the legs run on the same stride phase as the footstep sounds, so each foot
    /// plant lands on its sound. Numbers: <see cref="BodyFeelDef"/>.
    /// </summary>
    public sealed class BodyFeel
    {
        public BodyFeelDef def;
        public readonly BodyFeelPose pose = new BodyFeelPose();

        readonly Vector3[] _vel = new Vector3[2];
        readonly bool[] _wasContact = new bool[2];
        readonly Vector3[] _catchFrom = new Vector3[2];
        readonly float[] _catchT = new float[2];
        bool _started;
        float _time, _lift, _sweepT = -1f, _tuckT = -1f, _breathT;
        float _swayYaw, _swayYawV, _swayPitch, _swayPitchV;
        float _kickLean, _kickLeanV, _turnLean;
        float _hardDip, _hardDipV, _hardPending = -1f, _hardAmount;
        float _lanX, _lanXV, _lanZ, _lanZV, _slideLegs;
        Vector3 _lastVel;
        int _lastWallSide;
        // The camera's dip history, to replay it a moment later.
        readonly float[] _dipT = new float[64];
        readonly float[] _dipV = new float[64];
        int _dipHead, _dipCount;

        public BodyFeel(BodyFeelDef def)
        {
            this.def = def ?? new BodyFeelDef();
        }

        public static float SmoothStep(float e0, float e1, float x) => MoveMath.SmoothStep(e0, e1, x);

        Vector3 Rest(float side) => new Vector3(side * def.restX, def.restY, def.restZ);

        /// <summary>Where the hand would pump to for <paramref name="side"/> (-1 left, +1 right) at this phase (tests).</summary>
        public static float Swing(float side, float stridePhase) => side * (float)Math.Cos(stridePhase * Math.PI);

        public void Reset()
        {
            _started = false;
            _sweepT = _tuckT = -1f;
            _lift = 0f;
            _dipCount = 0;
        }

        public BodyFeelPose Tick(in BodyFeelInput i)
        {
            var d = def;
            float dt = Math.Max(0f, i.dt);
            float motion = Clamp01(i.motion);
            var ev = i.events;
            var mode = i.mode;
            _time += dt;
            if (!_started)
            {
                _started = true;
                for (int k = 0; k < 2; k++) pose.hand[k] = Rest(k == 0 ? -1f : 1f);
                _lastVel = i.velocityBody;
            }

            // Tiers (8.4).
            float fast = SmoothStep(d.tierFast - d.tierBlend, d.tierFast + d.tierBlend, i.speed);
            float flying = SmoothStep(d.tierFlying - d.tierBlend, d.tierFlying + d.tierBlend, i.speed);
            pose.fast = fast;
            pose.flying = flying;
            pose.tier = i.speed >= d.tierFlying ? 2 : (i.speed >= d.tierFast ? 1 : 0);
            bool ground = mode == MoveMode.Grounded || mode == MoveMode.Sprint;
            float run = Clamp01(i.speed / Math.Max(1f, i.sprintSpeed));

            // Accelerations in the body frame (turn lean, lanyard).
            float yawRate = dt > 0f ? i.lookYaw / dt : 0f;
            float pitchRate = dt > 0f ? i.lookPitch / dt : 0f;
            Vector3 acc = dt > 0f ? (i.velocityBody - _lastVel) / dt : Vector3.Zero;
            _lastVel = i.velocityBody;
            float lateral = i.speed * yawRate * (float)(Math.PI / 180.0) + acc.X;

            // Events.
            if (ev.jumped || ev.wallJumped) _lift = 1f;
            _lift = Math.Max(0f, _lift - dt * d.jumpLiftDecay);
            if (ev.doubleJumped)
            {
                _sweepT = 0f;
                _tuckT = 0f;
            }
            if (_sweepT >= 0f) { _sweepT += dt; if (_sweepT > d.doubleJumpTime * 2f) _sweepT = -1f; }
            if (_tuckT >= 0f) { _tuckT += dt; if (_tuckT > d.tuckTime) _tuckT = -1f; }
            if (ground || mode == MoveMode.Slide) _tuckT = -1f;
            float sweep = _sweepT < 0f ? 0f : (_sweepT < d.doubleJumpTime ? 1f : 1f - (_sweepT - d.doubleJumpTime) / Math.Max(0.01f, d.doubleJumpTime));
            float tuck = _tuckT < 0f ? 0f : (float)Math.Sin(Math.PI * Clamp01(_tuckT / Math.Max(0.01f, d.tuckTime)));

            // Sway: the arms trail look turns.
            float swayYawT = -Clamp(yawRate * d.swayPerDegPerSec, -d.swayMax, d.swayMax) * motion;
            float swayPitchT = Clamp(pitchRate * d.swayPerDegPerSec, -d.swayMax, d.swayMax) * motion;
            Spring(ref _swayYaw, ref _swayYawV, swayYawT, d.swayFrequency, d.swayDamping, dt);
            Spring(ref _swayPitch, ref _swayPitchV, swayPitchT, d.swayFrequency, d.swayDamping, dt);
            pose.swayYaw = _swayYaw;
            pose.swayPitch = _swayPitch;

            // Torso: tier lean, the kick's shove, the lean into turns.
            if (ev.sprintKick) _kickLeanV += d.kickLean * d.kickLeanFrequency * 2.718f * motion;
            Spring(ref _kickLean, ref _kickLeanV, 0f, d.kickLeanFrequency, 1f, dt);
            float tierLean = ground || mode == MoveMode.Air ? d.leanFast * fast + (d.leanFlying - d.leanFast) * flying : 0f;
            float turnT = ground ? Clamp(lateral * d.turnLeanPerAccel, -d.turnLeanMax, d.turnLeanMax) : 0f;
            _turnLean += (turnT - _turnLean) * (1f - (float)Math.Exp(-d.turnLeanRate * dt));
            pose.torsoPitch = (tierLean + _kickLean) * motion - d.slideRecline * _slideLegs;
            pose.torsoRoll = _turnLean * motion;

            // Landing: replay the camera's dip a moment later (+ a little more on hard landings).
            PushDip(i.cameraDip);
            float delayed = DipAt(_time - d.landLag);
            if (ev.landed && !ev.slideStarted && ev.landImpact > d.hardLanding)
            {
                _hardPending = d.landLag;
                _hardAmount = d.hardLandingExtra * Clamp01((ev.landImpact - d.hardLanding) / Math.Max(1f, d.hardLanding));
            }
            if (_hardPending >= 0f)
            {
                _hardPending -= dt;
                if (_hardPending < 0f) _hardDipV -= _hardAmount * d.hardLandingFrequency * 2.718f;
            }
            Spring(ref _hardDip, ref _hardDipV, 0f, d.hardLandingFrequency, 1f, dt);
            float lag = (delayed + _hardDip - i.cameraDip) * motion;
            pose.armDipOffset = lag;
            pose.bodyDip = i.cameraDip + lag;

            // Breath at rest; the lanyard.
            bool still = ground && i.speed < 0.5f;
            _breathT += dt;
            pose.breath = still ? 0.5f + 0.5f * (float)Math.Sin(_breathT * 2.0 * Math.PI / Math.Max(0.5f, d.breathPeriod)) : 0f;
            Spring(ref _lanX, ref _lanXV, Clamp(-acc.Z * d.lanyardPerAccel, -d.lanyardMax, d.lanyardMax) * motion, d.lanyardFrequency, d.lanyardDamping, dt);
            Spring(ref _lanZ, ref _lanZV, Clamp(-lateral * d.lanyardPerAccel, -d.lanyardMax, d.lanyardMax) * motion, d.lanyardFrequency, d.lanyardDamping, dt);
            pose.lanyardPitch = _lanX;
            pose.lanyardRoll = _lanZ;

            // The pump: in step with the stride, tighter at the faster tiers, zero off the ground.
            float pumpScale = 1f + (d.pumpScaleFast - 1f) * fast + (d.pumpScaleFlying - d.pumpScaleFast) * flying;
            float pumpAmp = ground ? d.pumpForward * run * pumpScale * motion : 0f;
            pose.pump = pumpAmp;

            int wallSide = mode == MoveMode.Wallrun ? i.wallSide : 0;
            for (int k = 0; k < 2; k++)
            {
                float side = k == 0 ? -1f : 1f;
                Vector3 target = Rest(side);
                bool contact = false;
                float stiff = d.armSpring;
                switch (mode)
                {
                    case MoveMode.Grounded:
                    case MoveMode.Sprint:
                    {
                        float swing = Swing(side, i.stridePhase);
                        float up = d.pumpForward > 0f ? pumpAmp / d.pumpForward * d.pumpUp : 0f;
                        target += new Vector3(-side * d.pumpInward * run * motion, up * Math.Max(0f, swing), pumpAmp * swing);
                        break;
                    }
                    case MoveMode.Air:
                        target = new Vector3(side * d.airX, d.airY, d.airZ);
                        break;
                    case MoveMode.Wallrun:
                        if ((int)side == wallSide && i.hasWallTarget)
                        {
                            target = i.wallHandTarget;
                            contact = true;
                        }
                        else
                        {
                            float swing = Swing(side, i.stridePhase);
                            target = new Vector3(side * (d.restX + 0.04f), d.restY + 0.03f, d.restZ + d.pumpForward * 2.5f * swing * motion);
                        }
                        break;
                    case MoveMode.Slide:
                    {
                        target = new Vector3(side * d.slideX, d.slideY, d.slideZ);
                        if (k == 0)
                        {
                            float trail = SmoothStep(d.slideTrailSpeed, d.slideTrailSpeed + 4f, i.speed);
                            target = Vector3.Lerp(target, new Vector3(d.slideTrailX, d.slideTrailY, d.slideTrailZ), trail);
                        }
                        break;
                    }
                    case MoveMode.Mantle:
                        if (i.hasLedge)
                        {
                            target = k == 0 ? i.ledgeLeft : i.ledgeRight;
                            contact = true;
                        }
                        else target = new Vector3(side * 0.24f, -0.22f, 0.48f);
                        break;
                }

                if (!contact)
                {
                    // Effects (x Motion): jump lift, the double jump's sweep, flying's sweep back, breath, the dip lag, the sway.
                    target.Y += d.jumpLift * _lift * motion;
                    target += new Vector3(0f, -d.doubleJumpDown, -d.doubleJumpBack) * (sweep * motion);
                    if (ground || mode == MoveMode.Air) target += new Vector3(0f, -d.flyingSweepDown, -d.flyingSweepBack) * (flying * motion);
                    target.Y += d.breathArms * pose.breath * motion + lag;
                    target = Sway(target, _swayYaw, _swayPitch);
                }

                // Impulses (x Motion): the sprint kick's plant and shove, the wall kick's push.
                if (ev.sprintKick)
                {
                    bool trailing = Swing(side, i.stridePhase) <= 0f;
                    _vel[k] += trailing ? new Vector3(0f, -d.kickArmDown, -d.kickArmBack) * motion : new Vector3(0f, 0f, d.kickArmForward) * motion;
                }
                if (ev.wallJumped && (int)side == _lastWallSide) _vel[k] += new Vector3(side * d.wallPushOff, 0f, 0.2f * d.wallPushOff) * motion;

                if (contact)
                {
                    // Exact: the hand lands on its spot within wallCatchTime of the catch, then stays on it (dragging along the wall).
                    if (!_wasContact[k])
                    {
                        _catchFrom[k] = pose.hand[k];
                        _catchT[k] = 0f;
                    }
                    _catchT[k] += dt;
                    float c = d.wallCatchTime <= 0f ? 1f : Clamp01(_catchT[k] / d.wallCatchTime);
                    pose.hand[k] = Vector3.Lerp(_catchFrom[k], target, c);
                    _vel[k] = Vector3.Zero;
                }
                else if (dt > 0f)
                {
                    float w = stiff;
                    _vel[k] += (w * w * (target - pose.hand[k]) - 2f * w * _vel[k]) * dt;
                    pose.hand[k] += _vel[k] * dt;
                }
                _wasContact[k] = contact;
                pose.contact[k] = contact;
                pose.elbowPole[k] = contact && mode == MoveMode.Wallrun
                    ? Vector3.Normalize(new Vector3(side * 0.3f, -1f, -0.6f))
                    : Vector3.Normalize(new Vector3(side, -1f, -0.4f));
            }
            if (mode == MoveMode.Wallrun) _lastWallSide = wallSide;
            else if (!ev.wallJumped) _lastWallSide = 0;

            Legs(i, tuck * motion);
            return pose;
        }

        void Legs(in BodyFeelInput i, float tuck)
        {
            var d = def;
            var mode = i.mode;
            bool slide = mode == MoveMode.Slide;
            _slideLegs = MoveToward(_slideLegs, slide ? 1f : 0f, i.dt * 7f);
            pose.legs = Math.Max(_slideLegs, Clamp01(tuck));
            float stride = (1.0f + 0.1f * i.speed) * d.gaitStrideScale;
            float lift = Math.Min(d.footLiftMax, d.footLift + d.footLiftPerSpeed * i.speed);
            for (int k = 0; k < 2; k++)
            {
                float side = k == 0 ? -1f : 1f;
                Vector3 foot;
                bool planted = false;
                bool moving = (mode == MoveMode.Grounded || mode == MoveMode.Sprint || mode == MoveMode.Wallrun) && i.speed >= 0.5f;
                if (moving)
                {
                    // Foot k touches down when the stride phase crosses k, k+2, k+4... (the controller's footstep).
                    float q = Repeat(i.stridePhase - k, 2f);
                    float s = Clamp(d.stanceFraction, 0.1f, 1.9f);
                    if (q < s)
                    {
                        foot = new Vector3(side * d.hipWidth, 0f, stride * (s * 0.5f - q));
                        planted = true;
                    }
                    else
                    {
                        float u = (q - s) / (2f - s);
                        float e = u * u * (3f - 2f * u);
                        foot = new Vector3(side * d.hipWidth, (float)Math.Sin(u * Math.PI) * lift, stride * s * (e - 0.5f));
                    }
                    if (mode == MoveMode.Wallrun && (int)side == i.wallSide) foot.X += side * 0.08f;
                }
                else if (mode == MoveMode.Air) foot = new Vector3(side * d.hipWidth, 0.15f, 0.05f);
                else if (mode == MoveMode.Mantle) foot = new Vector3(side * d.hipWidth, d.tuckFootHeight * 0.6f, d.tuckFootForward * 0.5f);
                else
                {
                    foot = new Vector3(side * d.hipWidth, 0f, 0f);
                    planted = true;
                }
                if (tuck > 0f) foot = Vector3.Lerp(foot, new Vector3(side * (d.hipWidth + 0.02f), d.tuckFootHeight, d.tuckFootForward), Clamp01(tuck));
                if (_slideLegs > 0f)
                {
                    float kk = _slideLegs;
                    Vector3 sf = new Vector3(side * (d.hipWidth + 0.09f), Lerp(0.1f, d.slideFootHeight, kk),
                        Lerp(0.2f, d.slideFootForward, kk) + (k == 0 ? d.slideLead : -d.slideLead));
                    foot = Vector3.Lerp(foot, sf, kk);
                    planted = false;
                }
                pose.foot[k] = foot;
                pose.footPlanted[k] = planted;
                pose.kneePole[k] = Vector3.Normalize(new Vector3(side * 0.15f, 0.2f, 1f));
            }
        }

        /// <summary>The slide pose's knee for the placeholder legs (body space).</summary>
        public Vector3 SlideKnee(int k)
        {
            var d = def;
            float side = k == 0 ? -1f : 1f;
            float kk = _slideLegs;
            return new Vector3(side * (d.hipWidth + 0.06f), Lerp(0.2f, d.slideKneeHeight, kk), Lerp(0.1f, d.slideKneeForward, kk) + (k == 0 ? d.slideLead + 0.02f : 0f));
        }

        static Vector3 Sway(Vector3 p, float yawDeg, float pitchDeg)
        {
            float ty = (float)Math.Tan(yawDeg * Math.PI / 180.0), tp = (float)Math.Tan(pitchDeg * Math.PI / 180.0);
            return new Vector3(p.X + p.Z * ty, p.Y + p.Z * tp, p.Z);
        }

        void PushDip(float v)
        {
            _dipHead = (_dipHead + 1) % _dipT.Length;
            _dipT[_dipHead] = _time;
            _dipV[_dipHead] = v;
            if (_dipCount < _dipT.Length) _dipCount++;
        }

        /// <summary>The camera's dip at an earlier moment: the last sample at or before it (the oldest if earlier still).</summary>
        float DipAt(float t)
        {
            if (_dipCount == 0) return 0f;
            int idx = _dipHead;
            for (int n = 0; n < _dipCount; n++)
            {
                if (_dipT[idx] <= t) return _dipV[idx];
                if (n == _dipCount - 1) break;
                idx = (idx - 1 + _dipT.Length) % _dipT.Length;
            }
            return _dipV[idx];
        }

        static void Spring(ref float x, ref float v, float target, float w, float zeta, float dt)
        {
            if (dt <= 0f) return;
            v += (w * w * (target - x) - 2f * zeta * w * v) * dt;
            x += v * dt;
        }

        static float Clamp(float v, float a, float b) => v < a ? a : (v > b ? b : v);
        static float Clamp01(float v) => Clamp(v, 0f, 1f);
        static float Lerp(float a, float b, float t) => a + (b - a) * t;
        static float Repeat(float v, float len) => v - (float)Math.Floor(v / len) * len;
        static float MoveToward(float a, float b, float step) => a < b ? Math.Min(b, a + step) : Math.Max(b, a - step);
    }
}
