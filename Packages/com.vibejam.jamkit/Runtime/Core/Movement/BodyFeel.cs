using System;
using System.Numerics;

namespace JamKit.Movement
{
    /// <summary>
    /// Every number of the first-person body's feel (docs/MOVEMENT.md "The body"; evidence in RESEARCH-MOVEMENT
    /// 8.7, cited per field: TF2 = Titanfall 2's game files or runners, Apex = Apex Legends' scripts, Destiny /
    /// Overwatch / Dying Light = GDC talks; "ours" = no source, our own choice, kept subtle). Lives in the
    /// movement tuning asset next to <see cref="MoveDef"/>. Arm positions are in camera space (x right, y up,
    /// z forward, metres from the eye); leg positions in body space (x right, y up from the soles, z forward).
    /// "x Motion" marks effects that scale with the player's Motion effects setting (0 turns them off);
    /// contact cues (the wall hand and its reach, the ledge hands, the slide cant and legs) never scale.
    /// </summary>
    [Serializable]
    public class BodyFeelDef
    {
        // ---- Arms at rest ----
        /// <summary>Where a hand rests (camera space, the right hand; the left mirrors x).</summary>
        public float restX = 0.21f, restY = -0.36f, restZ = 0.36f;
        /// <summary>Where the placeholder capsules' shoulders sit (camera space).</summary>
        public float shoulderWidth = 0.24f, shoulderDrop = 0.30f;
        /// <summary>The hands' spring toward their pose (rad/s, critically damped).</summary>
        public float armSpring = 18f;
        /// <summary>Hands in the air (camera space, the right hand).</summary>
        public float airX = 0.27f, airY = -0.3f, airZ = 0.34f;
        /// <summary>The height the arms roll around (camera space): the sway's roll, the strafe roll, the slide cant.</summary>
        public float rollPivotY = -0.28f;

        // ---- The pump, in step with the footfalls (TF2 sprint cycle 0.55 s = two steps; the arms carry it, the camera stays
        //      still). The amplitudes are ours (no game publishes them). x Motion ----
        /// <summary>Forward and back swing of each hand at a full run (m).</summary>
        public float pumpForward = 0.04f;
        /// <summary>Rise of the forward hand (m).</summary>
        public float pumpUp = 0.015f;
        /// <summary>Hands drift in towards the middle at speed (m).</summary>
        public float pumpInward = 0.02f;
        /// <summary>The pump tightens at the faster tiers (ours: multipliers at "fast" and "flying").</summary>
        public float pumpScaleFast = 0.85f, pumpScaleFlying = 0.7f;

        // ---- Speed tiers in the posture (ours, no source: TF2 only lowers the weapon in sprint). Subtle. x Motion ----
        public float tierFast = 8f, tierFlying = 13f;
        /// <summary>Half the width of the blend around each tier boundary (m/s).</summary>
        public float tierBlend = 1f;
        /// <summary>Torso lean forward at "fast" and at "flying" (degrees).</summary>
        public float leanFast = 1.5f, leanFlying = 3f;
        /// <summary>At "flying" the arms draw back and down a little (m).</summary>
        public float flyingSweepBack = 0.04f, flyingSweepDown = 0.015f;
        /// <summary>Torso lean into turns (ours): degrees per m/s^2 sideways, the limit, the smoothing rate (1/s).</summary>
        public float turnLeanPerAccel = 0.3f, turnLeanMax = 3f, turnLeanRate = 6f;

        // ---- Look sway: the arms lag the view (TF2 R-201: yaw 2.5, pitch 3, roll 4 degrees, translation 1.3-1.5 cm, follow
        //      rates rotation 7 and movement 2.5 /s; Apex fists: 3.5 degrees, rates 12 and 10; bare hands between). x Motion ----
        /// <summary>Degrees of lag per degree-per-second of look turn, and the clamps (degrees).</summary>
        public float swayPerDegPerSec = 0.012f, swayYawMax = 3f, swayPitchMax = 3f;
        /// <summary>Turning also rolls the arms: degrees per degree-per-second, up to the clamp.</summary>
        public float swayRollPerDegPerSec = 0.016f, swayRollMax = 4f;
        /// <summary>And slides them sideways: metres per degree-per-second, up to the clamp (1.3 cm).</summary>
        public float swayMovePerDegPerSec = 0.00006f, swayMoveMax = 0.013f;
        /// <summary>How fast the rotation and the translation follow (1/s).</summary>
        public float swayRotateRate = 9f, swayMoveRate = 6f;
        /// <summary>Strafing rolls the arms (TF2: 4 degrees) at this sideways speed (m/s) and above.</summary>
        public float strafeRoll = 4f, strafeRollSpeed = 5f;

        // ---- Vertical inertia (TF2 R-201: the view model sinks while rising and floats while falling, about 1.5 cm at most).
        //      Replaces a jump lift. x Motion ----
        /// <summary>Metres of offset per m/s of vertical speed (down while rising), the clamp (m).</summary>
        public float verticalSwayPerSpeed = 0.0025f, verticalSwayMax = 0.015f;

        // ---- Landing: the camera dips first, the body about one frame at 30 fps later (Destiny: the head leads by a frame),
        //      plus a view-punch spring scaled by the fall (TF2 springs.txt punch_pilot: constant 65, damping 9). x Motion ----
        /// <summary>How much later than the camera the body takes the dip (s).</summary>
        public float landLag = 0.03f;
        /// <summary>The punch: its depth (m) from impact speed landPunchFrom (none) to landPunchFull (all), and its spring.</summary>
        public float landPunch = 0.02f, landPunchFrom = 4f, landPunchFull = 18f, punchConstant = 65f, punchDamping = 9f;

        // ---- Double jump (Apex has its own overlay for it; Destiny holds key poses 7-10 frames so they read). x Motion ----
        /// <summary>The "push off the air" pose: held this long (s), then released over this long (s); its offsets (m).</summary>
        public float doubleJumpHold = 0.27f, doubleJumpRelease = 0.12f, doubleJumpDown = 0.06f, doubleJumpBack = 0.18f;
        /// <summary>The legs tuck for this long (s) (Mirror's Edge coils the legs in the air).</summary>
        public float tuckTime = 0.35f;

        // ---- Sprint kick: plant and shove (ours). x Motion ----
        public float kickArmBack = 1.8f, kickArmDown = 0.3f, kickArmForward = 0.5f, kickLean = 6f, kickLeanFrequency = 9f;

        // ---- The wall hand (TF2's "lean-in": it reaches out BEFORE contact, telegraphing almost exactly when you'll touch
        //      the wall, and attaches on contact: the cue runners time the 83 ms wall kick by). Never scaled ----
        /// <summary>How far ahead the reach starts (s before predicted contact; Momentum Mod traces 0.25 s).</summary>
        public float wallLookAhead = 0.25f;
        /// <summary>The planted hand: the wall point beside the shoulder plus this far ahead along the run, at this camera height (m).</summary>
        public float wallHandAhead = 0.35f, wallHandHeight = -0.06f;
        /// <summary>The run pose blends to one-handed in this long and back in this long (TF2: 0.2 s in, 0.35 s out).</summary>
        public float wallBlendIn = 0.2f, wallBlendOut = 0.35f;
        /// <summary>The free arm while on the wall (camera space, mirrored to its side).</summary>
        public float wallFreeX = 0.24f, wallFreeY = -0.31f, wallFreeZ = 0.4f;
        /// <summary>Wall kick: the wall hand shoves off the wall on the kick (m/s, x Motion).</summary>
        public float wallPushOff = 2.2f;

        // ---- Slide (TF2: the arms tilt sideways, already in the air while crouch is held: "a slide is coming"; the camera
        //      dips sharply on entry and the FOV widens x1.1, both in MovementCamera). Never scaled ----
        /// <summary>The arms' tilt (degrees, top to the right) and how far they drop (m), blended at this rate (1/s).</summary>
        public float slideCant = 18f, slideDrop = 0.03f, slideCantRate = 10f;
        /// <summary>Show the legs while sliding (no source shows TF2's; Mirror's Edge shows them). The creator decides.</summary>
        public bool slideLegs = true;
        /// <summary>Slide legs (body space): the feet rise into the bottom of the screen; the left (leading) leg further.</summary>
        public float slideFootForward = 0.95f, slideFootHeight = 0.3f, slideKneeForward = 0.5f, slideKneeHeight = 0.48f, slideLead = 0.06f;
        /// <summary>The torso reclines this far (degrees) so a real body's feet reach out in front.</summary>
        public float slideRecline = 30f;

        // ---- Mantle (TF2: poses for 4 ledge heights with 0.2 s blends; Dying Light: the hand placed by IK on the edge with
        //      about 40 cm of correction, the reach time scaled to the approach speed; the view stays free). Never scaled ----
        /// <summary>The hands plant this far either side of the middle of the ledge lip (m).</summary>
        public float mantleHandSpread = 0.22f;
        /// <summary>Ledge heights (m above the feet) of the low and the high pose; the poses (camera space, the right hand).</summary>
        public float mantleLowHeight = 0.5f, mantleHighHeight = 1.5f;
        public float mantleLowX = 0.24f, mantleLowY = -0.5f, mantleLowZ = 0.5f, mantleHighX = 0.26f, mantleHighY = -0.08f, mantleHighZ = 0.42f;
        /// <summary>How far IK may move the hand off its pose onto the lip (m).</summary>
        public float mantleIkRange = 0.4f;
        /// <summary>The hands reach the lip in (this distance / the approach speed) seconds, between these limits.</summary>
        public float mantleReachDistance = 0.3f, mantleReachMin = 0.06f, mantleReachMax = 0.22f;

        // ---- Legs (the stride is the controller's: each whole step of its phase is a footfall) ----
        /// <summary>How much of each foot's two-step cycle it spends on the ground (phase units, 0..2).</summary>
        public float stanceFraction = 0.7f;
        /// <summary>Foot lift in the swing (m) and its growth with speed (m per m/s), up to the limit.</summary>
        public float footLift = 0.12f, footLiftPerSpeed = 0.012f, footLiftMax = 0.3f;
        /// <summary>How far the feet travel as a share of the real stride (a real body's legs can't reach a full stride).</summary>
        public float gaitStrideScale = 0.55f;
        /// <summary>Half the distance between the feet (m).</summary>
        public float hipWidth = 0.1f;
        /// <summary>Double-jump tuck and mantle feet (body space).</summary>
        public float tuckFootHeight = 0.5f, tuckFootForward = 0.18f;

        // ---- Gestures (a gameplay action lifting a hand towards a point, e.g. the dream tool's pulse). Never scaled;
        //      contact cues win ----
        /// <summary>How far from the shoulder the hand reaches towards the point (m), and the share of the gesture spent
        /// raising it (the rest blends back).</summary>
        public float gestureReach = 0.5f, gestureRise = 0.3f;
        /// <summary>The hand's spring while it gestures (x armSpring: a quick raise).</summary>
        public float gestureSpring = 2f;

        // ---- Secondary motion (ours). x Motion ----
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
        /// <summary>Crouch is held (in the air: a slide is coming).</summary>
        public bool crouchHeld;
        public int wallSide;
        /// <summary>On the wall: the wall hand's spot (camera space).</summary>
        public bool hasWallTarget;
        public Vector3 wallHandTarget;
        /// <summary>Before the wall: a runnable wall is coming on <see cref="approachSide"/>, <see cref="approachCloseness"/> 0
        /// (wallLookAhead away) .. 1 (contact), the hand's spot there (camera space, relative to where she'll be).</summary>
        public bool approaching;
        public int approachSide;
        public float approachCloseness;
        public Vector3 approachTarget;
        /// <summary>During a mantle: the lip's hand spots (camera space, left then right) and the ledge's height above her feet (m).</summary>
        public bool hasLedge;
        public Vector3 ledgeLeft, ledgeRight;
        public float ledgeHeight;
        /// <summary>The gesture's point (camera space), while a gesture runs (<see cref="BodyFeel.Gesture"/>).</summary>
        public Vector3 gestureTarget;
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
        /// <summary>The hand is on something (the wall, a ledge): its position is exact and the palm faces it.</summary>
        public readonly bool[] contact = new bool[2];
        /// <summary>How far the wall hand has reached out ahead of contact (0..1), per hand.</summary>
        public readonly float[] reach = new float[2];
        /// <summary>How far into a gesture each hand is (0..1; 0 when a contact cue holds it).</summary>
        public readonly float[] gesture = new float[2];
        /// <summary>Where each foot is (body space) and which way each knee points.</summary>
        public readonly Vector3[] foot = new Vector3[2];
        public readonly Vector3[] kneePole = new Vector3[2];
        public readonly bool[] footPlanted = new bool[2];
        /// <summary>The legs' slide (or tuck) pose weight, 0..1 (the placeholder shows its legs only then).</summary>
        public float legs;
        /// <summary>Torso lean forward (negative reclines) and into turns (degrees).</summary>
        public float torsoPitch, torsoRoll;
        /// <summary>The arms' offset against the camera's dip (m): they take the landing a moment later.</summary>
        public float armDipOffset;
        /// <summary>The body's own landing dip (m, world, negative down).</summary>
        public float bodyDip;
        /// <summary>The arms' sway (degrees) and its sideways slide (m); their roll (degrees: sway, strafe and the slide cant).</summary>
        public float swayYaw, swayPitch, swayMove, armRoll;
        /// <summary>The slide cant's weight 0..1 (sliding, or crouch held in the air).</summary>
        public float cant;
        /// <summary>The arms' vertical inertia (m: down while rising, up while falling).</summary>
        public float vertical;
        /// <summary>The double jump's pose weight 0..1 (held, then released).</summary>
        public float doubleJump;
        /// <summary>How far into the one-handed wall pose (0..1; TF2's 0.2 s in, 0.35 s out).</summary>
        public float wallBlend;
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
    /// The first-person body's feel as springs and poses, in plain C# (tested without Unity). The hands pump in
    /// step with the footfalls; the wall hand reaches out before a wallrun and plants on the contact frame, then
    /// drags along the wall while the other arm goes one-handed; the wall kick shoves it off; the arms tilt
    /// for a slide (already in the air with crouch held); the hands plant on a ledge's lip by its height; the
    /// arms lag look turns, roll when strafing, sink while rising and float while falling, hold the double
    /// jump's pose long enough to read, and take the landing a frame after the camera. The legs run on the
    /// same stride phase as the footstep sounds. Numbers and their sources: <see cref="BodyFeelDef"/>.
    /// </summary>
    public sealed class BodyFeel
    {
        public BodyFeelDef def;
        public readonly BodyFeelPose pose = new BodyFeelPose();

        readonly Vector3[] _vel = new Vector3[2];
        readonly bool[] _wasContact = new bool[2];
        readonly Vector3[] _catchFrom = new Vector3[2];
        readonly float[] _catchT = new float[2];
        readonly float[] _catchTime = new float[2];
        readonly Vector3[] _lastContact = new Vector3[2];
        bool _started;
        float _time, _djT = -1f, _tuckT = -1f, _breathT;
        float _swayYaw, _swayPitch, _swayRoll, _swayMove, _strafe, _vertical, _cant;
        float _kickLean, _kickLeanV, _turnLean;
        float _punch, _punchV, _punchPending = -1f, _punchAmount;
        float _lanX, _lanXV, _lanZ, _lanZV, _slideLegs, _wallBlend, _lastSpeed;
        Vector3 _lastVel;
        int _lastWallSide, _wallArm;
        int _gestureHand = -1;
        float _gestureT, _gestureDur;
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

        /// <summary>The pump's swing for <paramref name="side"/> (-1 left, +1 right) at this stride phase: each arm at its
        /// furthest on each footfall (the right hand forward when the left foot lands).</summary>
        public static float Swing(float side, float stridePhase) => side * (float)Math.Cos(stridePhase * Math.PI);

        /// <summary>
        /// Lifts <paramref name="hand"/> (0 left, 1 right) towards a point for <paramref name="duration"/> seconds: a quick raise,
        /// then a blend back. The point comes in each frame as <see cref="BodyFeelInput.gestureTarget"/>. A contact cue on that
        /// hand (the wall, a ledge) wins: the gesture waits it out silently.
        /// </summary>
        public void Gesture(int hand, float duration)
        {
            _gestureHand = hand == 0 ? 0 : 1;
            _gestureT = 0f;
            _gestureDur = Math.Max(0.05f, duration);
        }

        /// <summary>The gesture's weight now (0..1): up over gestureRise of it, then back down.</summary>
        float GestureWeight()
        {
            if (_gestureHand < 0) return 0f;
            float u = Clamp01(_gestureT / _gestureDur);
            float rise = Clamp(def.gestureRise, 0.05f, 0.95f);
            float w = u < rise ? u / rise : 1f - (u - rise) / (1f - rise);
            w = Clamp01(w);
            return w * w * (3f - 2f * w);
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
                _lastSpeed = i.speed;
            }
            float approachSpeed = Math.Max(_lastSpeed, i.speed);
            if (_gestureHand >= 0)
            {
                _gestureT += dt;
                if (_gestureT >= _gestureDur) _gestureHand = -1;
            }

            // Tiers (8.4; the posture part is ours).
            float fast = SmoothStep(d.tierFast - d.tierBlend, d.tierFast + d.tierBlend, i.speed);
            float flying = SmoothStep(d.tierFlying - d.tierBlend, d.tierFlying + d.tierBlend, i.speed);
            pose.fast = fast;
            pose.flying = flying;
            pose.tier = i.speed >= d.tierFlying ? 2 : (i.speed >= d.tierFast ? 1 : 0);
            bool ground = mode == MoveMode.Grounded || mode == MoveMode.Sprint;
            float run = Clamp01(i.speed / Math.Max(1f, i.sprintSpeed));

            float yawRate = dt > 0f ? i.lookYaw / dt : 0f;
            float pitchRate = dt > 0f ? i.lookPitch / dt : 0f;
            Vector3 acc = dt > 0f ? (i.velocityBody - _lastVel) / dt : Vector3.Zero;
            _lastVel = i.velocityBody;
            float lateral = i.speed * yawRate * (float)(Math.PI / 180.0) + acc.X;

            // The double jump: hold the pose, then release it; the legs tuck.
            if (ev.doubleJumped)
            {
                _djT = 0f;
                _tuckT = 0f;
            }
            if (_djT >= 0f) { _djT += dt; if (_djT > d.doubleJumpHold + d.doubleJumpRelease) _djT = -1f; }
            if (_tuckT >= 0f) { _tuckT += dt; if (_tuckT > d.tuckTime) _tuckT = -1f; }
            if (ground || mode == MoveMode.Slide || mode == MoveMode.Wallrun) _djT = _tuckT = -1f;
            float dj = _djT < 0f ? 0f : (_djT <= d.doubleJumpHold ? 1f : 1f - (_djT - d.doubleJumpHold) / Math.Max(0.01f, d.doubleJumpRelease));
            pose.doubleJump = dj;
            float tuck = _tuckT < 0f ? 0f : (float)Math.Sin(Math.PI * Clamp01(_tuckT / Math.Max(0.01f, d.tuckTime)));

            // Look sway (x Motion): rotation follows fast, translation slower; strafing rolls the arms; vertical inertia in the air.
            float fr = 1f - (float)Math.Exp(-d.swayRotateRate * dt), fm = 1f - (float)Math.Exp(-d.swayMoveRate * dt);
            _swayYaw += (-Clamp(yawRate * d.swayPerDegPerSec, -d.swayYawMax, d.swayYawMax) * motion - _swayYaw) * fr;
            _swayPitch += (Clamp(pitchRate * d.swayPerDegPerSec, -d.swayPitchMax, d.swayPitchMax) * motion - _swayPitch) * fr;
            _swayRoll += (Clamp(yawRate * d.swayRollPerDegPerSec, -d.swayRollMax, d.swayRollMax) * motion - _swayRoll) * fr;
            _swayMove += (-Clamp(yawRate * d.swayMovePerDegPerSec, -d.swayMoveMax, d.swayMoveMax) * motion - _swayMove) * fm;
            _strafe += (-Clamp(i.velocityBody.X / Math.Max(0.1f, d.strafeRollSpeed), -1f, 1f) * d.strafeRoll * (ground ? motion : 0f) - _strafe) * fm;
            _vertical += (-Clamp(i.velocityBody.Y * d.verticalSwayPerSpeed, -d.verticalSwayMax, d.verticalSwayMax) * (ground ? 0f : motion) - _vertical) * fm;
            pose.swayYaw = _swayYaw;
            pose.swayPitch = _swayPitch;
            pose.swayMove = _swayMove;
            pose.vertical = _vertical;

            // The slide cant (a cue, never scaled): sliding, or crouch held in the air.
            bool cantNow = mode == MoveMode.Slide || (mode == MoveMode.Air && i.crouchHeld);
            _cant += ((cantNow ? 1f : 0f) - _cant) * (1f - (float)Math.Exp(-d.slideCantRate * dt));
            pose.cant = _cant;
            pose.armRoll = _swayRoll + _strafe + d.slideCant * _cant;

            // Torso: the tier lean (ours), the kick's shove, the lean into turns (ours), the slide's recline.
            if (ev.sprintKick) _kickLeanV += d.kickLean * d.kickLeanFrequency * 2.718f * motion;
            Spring(ref _kickLean, ref _kickLeanV, 0f, d.kickLeanFrequency, 1f, dt);
            float tierLean = ground || mode == MoveMode.Air ? d.leanFast * fast + (d.leanFlying - d.leanFast) * flying : 0f;
            float turnT = ground ? Clamp(lateral * d.turnLeanPerAccel, -d.turnLeanMax, d.turnLeanMax) : 0f;
            _turnLean += (turnT - _turnLean) * (1f - (float)Math.Exp(-d.turnLeanRate * dt));
            pose.torsoPitch = (tierLean + _kickLean) * motion - d.slideRecline * _slideLegs;
            pose.torsoRoll = _turnLean * motion;

            // Landing: the camera's dip a moment later, plus TF2's view-punch spring scaled by the fall.
            PushDip(i.cameraDip);
            float delayed = DipAt(_time - d.landLag);
            if (_punchPending >= 0f)
            {
                _punchPending -= dt;
                if (_punchPending < 0f) _punchV -= _punchAmount * (float)Math.Sqrt(Math.Max(1f, d.punchConstant)) * 2.2f;
            }
            if (ev.landed && !ev.slideStarted)
            {
                _punchPending = d.landLag;
                _punchAmount = d.landPunch * Clamp01((ev.landImpact - d.landPunchFrom) / Math.Max(0.1f, d.landPunchFull - d.landPunchFrom));
            }
            if (dt > 0f)
            {
                // In pieces of at most 1/240 s, so a long frame steps it the same as short ones.
                int n = Math.Min(64, Math.Max(1, (int)Math.Ceiling(dt * 240f)));
                float h = dt / n;
                for (int k = 0; k < n; k++)
                {
                    _punchV += (-d.punchConstant * _punch - d.punchDamping * _punchV) * h;
                    _punch += _punchV * h;
                }
            }
            float lag = (delayed + _punch - i.cameraDip) * motion;
            pose.armDipOffset = lag;
            pose.bodyDip = i.cameraDip + lag;

            // Breath at rest; the lanyard (ours).
            bool still = ground && i.speed < 0.5f;
            _breathT += dt;
            pose.breath = still ? 0.5f + 0.5f * (float)Math.Sin(_breathT * 2.0 * Math.PI / Math.Max(0.5f, d.breathPeriod)) : 0f;
            Spring(ref _lanX, ref _lanXV, Clamp(-acc.Z * d.lanyardPerAccel, -d.lanyardMax, d.lanyardMax) * motion, d.lanyardFrequency, d.lanyardDamping, dt);
            Spring(ref _lanZ, ref _lanZV, Clamp(-lateral * d.lanyardPerAccel, -d.lanyardMax, d.lanyardMax) * motion, d.lanyardFrequency, d.lanyardDamping, dt);
            pose.lanyardPitch = _lanX;
            pose.lanyardRoll = _lanZ;

            // The pump: in step with the stride, a touch tighter at the faster tiers, zero off the ground.
            float pumpScale = 1f + (d.pumpScaleFast - 1f) * fast + (d.pumpScaleFlying - d.pumpScaleFast) * flying;
            float pumpAmp = ground ? d.pumpForward * run * pumpScale * motion : 0f;
            pose.pump = pumpAmp;

            // The one-handed wall pose: 0.2 s in, 0.35 s out.
            bool onWall = mode == MoveMode.Wallrun;
            _wallBlend = onWall ? Math.Min(1f, _wallBlend + dt / Math.Max(0.01f, d.wallBlendIn)) : Math.Max(0f, _wallBlend - dt / Math.Max(0.01f, d.wallBlendOut));
            if (onWall && i.wallSide != 0) _wallArm = i.wallSide;
            pose.wallBlend = _wallBlend;

            int wallSide = onWall ? i.wallSide : 0;
            float ledge01 = Clamp01((i.ledgeHeight - d.mantleLowHeight) / Math.Max(0.1f, d.mantleHighHeight - d.mantleLowHeight));
            for (int k = 0; k < 2; k++)
            {
                float side = k == 0 ? -1f : 1f;
                Vector3 target = Rest(side);
                bool contact = false;
                float catchTime = 0f;
                float reach = 0f;
                float swing = Swing(side, i.stridePhase);
                Vector3 pump = new Vector3(-side * d.pumpInward * run * motion, (d.pumpForward > 0f ? pumpAmp / d.pumpForward * d.pumpUp : 0f) * Math.Max(0f, swing), pumpAmp * swing);
                switch (mode)
                {
                    case MoveMode.Grounded:
                    case MoveMode.Sprint:
                        target += pump;
                        break;
                    case MoveMode.Air:
                        target = new Vector3(side * d.airX, d.airY, d.airZ);
                        break;
                    case MoveMode.Wallrun:
                        if ((int)side == wallSide && i.hasWallTarget)
                        {
                            // Plant on the contact frame (it reached out before), then drag along the wall.
                            target = i.wallHandTarget;
                            contact = true;
                        }
                        else
                        {
                            Vector3 free = new Vector3(side * d.wallFreeX, d.wallFreeY, d.wallFreeZ + d.pumpForward * swing * motion);
                            target = Vector3.Lerp(Rest(side) + pump, free, _wallBlend);
                        }
                        break;
                    case MoveMode.Slide:
                        target = Rest(side) + new Vector3(0f, -d.slideDrop, 0f);
                        break;
                    case MoveMode.Mantle:
                        if (i.hasLedge)
                        {
                            // The pose for the ledge's height, the hand moved onto the lip by IK (up to mantleIkRange).
                            Vector3 low = new Vector3(side * d.mantleLowX, d.mantleLowY, d.mantleLowZ);
                            Vector3 high = new Vector3(side * d.mantleHighX, d.mantleHighY, d.mantleHighZ);
                            Vector3 nominal = Vector3.Lerp(low, high, ledge01);
                            Vector3 lip = k == 0 ? i.ledgeLeft : i.ledgeRight;
                            Vector3 off = lip - nominal;
                            float len = off.Length();
                            if (len > d.mantleIkRange && len > 1e-5f) off *= d.mantleIkRange / len;
                            target = nominal + off;
                            contact = true;
                            catchTime = Clamp(d.mantleReachDistance / Math.Max(0.5f, approachSpeed), d.mantleReachMin, d.mantleReachMax);
                        }
                        else target = new Vector3(side * 0.24f, -0.22f, 0.48f);
                        break;
                }

                // The lean-in: before a wallrun, the wall-side hand reaches out towards where the wall will be.
                if (!contact && i.approaching && (int)side == i.approachSide && !onWall)
                {
                    float c = Clamp01(i.approachCloseness);
                    reach = c * (2f - c);
                    target = Vector3.Lerp(target, i.approachTarget, reach);
                }
                // Coming off the wall: the wall hand peels away over wallBlendOut.
                if (!contact && !onWall && _wallBlend > 0f && (int)side == _wallArm)
                    target = Vector3.Lerp(target, _lastContact[k], _wallBlend);
                pose.reach[k] = reach;
                // A gesture: the hand lifts towards the point at gestureReach from her shoulder (never over a contact cue).
                float gw = !contact && k == _gestureHand ? GestureWeight() : 0f;
                pose.gesture[k] = gw;
                if (gw > 0f)
                {
                    Vector3 shoulder = new Vector3(side * d.shoulderWidth, -d.shoulderDrop, 0.02f);
                    Vector3 to = i.gestureTarget - shoulder;
                    float len = to.Length();
                    Vector3 point = len > 1e-4f ? shoulder + to * (Math.Min(len, d.gestureReach) / len) : target;
                    target = Vector3.Lerp(target, point, gw);
                }

                if (!contact)
                {
                    // Effects (x Motion): the double jump's pose, flying's draw-back (ours), breath, the dip lag, vertical inertia, the sway.
                    target += new Vector3(0f, -d.doubleJumpDown, -d.doubleJumpBack) * (dj * motion);
                    if (ground || mode == MoveMode.Air) target += new Vector3(0f, -d.flyingSweepDown, -d.flyingSweepBack) * (flying * motion);
                    target.Y += d.breathArms * pose.breath * motion + lag + _vertical;
                    target.X += _swayMove;
                    target = Sway(target, _swayYaw, _swayPitch);
                    target = Roll(target, pose.armRoll, d);
                }

                // Impulses (x Motion): the sprint kick's plant and shove, the wall kick's push.
                if (ev.sprintKick)
                {
                    bool trailing = swing <= 0f;
                    _vel[k] += trailing ? new Vector3(0f, -d.kickArmDown, -d.kickArmBack) * motion : new Vector3(0f, 0f, d.kickArmForward) * motion;
                }
                if (ev.wallJumped && (int)side == _lastWallSide) _vel[k] += new Vector3(side * d.wallPushOff, 0f, 0.2f * d.wallPushOff) * motion;

                if (contact)
                {
                    if (!_wasContact[k])
                    {
                        _catchFrom[k] = pose.hand[k];
                        _catchT[k] = 0f;
                        _catchTime[k] = catchTime;
                    }
                    _catchT[k] += dt;
                    float c = _catchTime[k] <= 0f ? 1f : Clamp01(_catchT[k] / _catchTime[k]);
                    pose.hand[k] = Vector3.Lerp(_catchFrom[k], target, c);
                    _vel[k] = Vector3.Zero;
                    _lastContact[k] = pose.hand[k];
                }
                else if (dt > 0f)
                {
                    float w = reach > 0f ? d.armSpring * 1.6f : d.armSpring;
                    if (pose.gesture[k] > 0f) w = d.armSpring * Math.Max(1f, d.gestureSpring);
                    _vel[k] += (w * w * (target - pose.hand[k]) - 2f * w * _vel[k]) * dt;
                    pose.hand[k] += _vel[k] * dt;
                }
                _wasContact[k] = contact;
                pose.contact[k] = contact;
                pose.elbowPole[k] = (contact && onWall) || reach > 0.5f
                    ? Vector3.Normalize(new Vector3(side * 0.3f, -1f, -0.6f))
                    : Vector3.Normalize(new Vector3(side, -1f, -0.4f));
            }
            if (onWall) _lastWallSide = wallSide;
            else if (!ev.wallJumped) _lastWallSide = 0;
            _lastSpeed = i.speed;

            Legs(i, tuck * motion);
            return pose;
        }

        void Legs(in BodyFeelInput i, float tuck)
        {
            var d = def;
            var mode = i.mode;
            bool slide = mode == MoveMode.Slide && d.slideLegs;
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

        /// <summary>Rolls a camera-space point about the arms' pivot height (degrees, positive tilts the top to the right).</summary>
        static Vector3 Roll(Vector3 p, float degrees, BodyFeelDef d)
        {
            if (degrees == 0f) return p;
            double a = -degrees * Math.PI / 180.0;
            float cy = d.rollPivotY;
            float x = p.X, y = p.Y - cy;
            float c = (float)Math.Cos(a), s = (float)Math.Sin(a);
            return new Vector3(x * c - y * s, x * s + y * c + cy, p.Z);
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

        // Stepped in short pieces (MoveMath.Spring), so a long frame can't overshoot it.
        static void Spring(ref float x, ref float v, float target, float w, float zeta, float dt)
            => MoveMath.Spring(ref x, ref v, target, w, zeta, dt);

        static float Clamp(float v, float a, float b) => v < a ? a : (v > b ? b : v);
        static float Clamp01(float v) => Clamp(v, 0f, 1f);
        static float Lerp(float a, float b, float t) => a + (b - a) * t;
        static float Repeat(float v, float len) => v - (float)Math.Floor(v / len) * len;
        static float MoveToward(float a, float b, float step) => a < b ? Math.Min(b, a + step) : Math.Max(b, a - step);
    }
}
