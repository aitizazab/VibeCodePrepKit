using System;

namespace JamKit.Movement
{
    /// <summary>
    /// Every number of the Titanfall-style movement (docs/MOVEMENT.md explains each one, and
    /// docs/RESEARCH-MOVEMENT.md section 4 where the starting values come from). Metres, seconds,
    /// m/s and degrees. Serializable with public fields so a ScriptableObject or a component can
    /// hold it and the inspector can tune it live.
    /// </summary>
    [Serializable]
    public class MoveDef
    {
        // ---------- Simulation ----------
        /// <summary>Fixed simulation rate (steps per second). Air strafing depends on it, so it is fixed.</summary>
        public float tickRate = 120f;
        /// <summary>At most this many steps per rendered frame (a long hitch drops the rest).</summary>
        public int maxStepsPerFrame = 6;

        // ---------- Body ----------
        public float radius = 0.38f;
        public float standHeight = 1.7f;
        /// <summary>Capsule height while sliding or crouched (fits under 1.1 m bars).</summary>
        public float slideHeight = 1.0f;
        public float eyeHeight = 1.5f;
        public float slideEyeHeight = 0.8f;
        /// <summary>Seconds to blend the eye between standing and sliding heights.</summary>
        public float eyeBlendTime = 0.12f;
        /// <summary>Steepest walkable ground, degrees.</summary>
        public float slopeLimit = 50f;
        /// <summary>When grounded, ground within this distance below pulls you down (slopes, small drops).</summary>
        public float groundSnap = 0.4f;
        /// <summary>Ground closer than this counts as standing on it.</summary>
        public float groundEpsilon = 0.08f;

        // ---------- Ground ----------
        public float walkSpeed = 5f;
        /// <summary>Running speed (the DOOM-like template's run speed).</summary>
        public float sprintSpeed = 9.5f;
        public float crouchSpeed = 3f;
        /// <summary>Off (default, the creator's call 2026-10-10): you walk unless you sprint (hold Sprint, or click
        /// the sprint toggle). On: moving forward always sprints.</summary>
        public bool autoSprint = false;
        /// <summary>Sprint only runs forward-ish: the stick's forward part must be at least this share of its push.</summary>
        public float sprintForwardMin = 0.3f;
        /// <summary>The sprint toggle (pad L3, Titanfall-style) ends on the ground when the stick's forward part
        /// drops below this (released or pulled back); also on crouch, slide and respawn.</summary>
        public float sprintToggleStopForward = 0.2f;

        // ---------- Sprint kick (a push-off back to running after you lose your speed) ----------
        /// <summary>Sprinting on the ground below this share of sprint speed (a bad landing, a wall, a mantle)
        /// gives a sprint kick.</summary>
        public float kickBelow = 0.6f;
        /// <summary>The kick sets your speed along the stick to at least this share of sprint speed at once (never
        /// above sprint); ground acceleration does the rest in a few hundredths of a second.</summary>
        public float kickSpeed = 0.75f;
        /// <summary>Seconds between kicks, so stopping on purpose can't farm them.</summary>
        public float kickCooldown = 2f;
        /// <summary>The kick also readies the slide boost (its own 2 s cooldown is usually what makes a slide right
        /// after a stumble feel dead). The kick's cooldown equals the boost's, so this never boosts more often than
        /// sliding alone would; it only puts the boost where you need it.</summary>
        public bool kickRearmsSlideBoost = true;
        /// <summary>Source "accelerate": reaches full speed in about 1/accel seconds.</summary>
        public float groundAccel = 10f;
        /// <summary>Source friction (speed lost per second, proportional to speed).</summary>
        public float friction = 6f;
        /// <summary>Below this speed friction is constant, so you really stop.</summary>
        public float stopSpeed = 2.5f;
        /// <summary>Speed above sprint (from a slide boost, a wall jump) fades at this rate per second
        /// while you keep moving, instead of friction: the momentum lingers.</summary>
        public float overspeedDecay = 1.5f;
        /// <summary>Running faster than sprint, the velocity still turns toward the stick at this rate
        /// (degrees per second) without losing speed. Pure Source can't steer at overspeed, which feels like ice.</summary>
        public float groundTurnRate = 220f;
        /// <summary>No ground friction for this long after landing (a wide bunny-hop / chain window).</summary>
        public float landingGrace = 0.15f;
        /// <summary>How much of <see cref="overspeedDecay"/> still applies during the landing grace (0: the grace freezes your
        /// speed, as before 2026-10-11; 1: speed above sprint fades from the landing tick, so a late hop costs what the
        /// time on the ground cost). Only a jump on the landing tick (a buffered press) skips it. Default 0.5 (the Mild
        /// hop-cost preset, <see cref="HopCostPresets"/>; Current is 1).</summary>
        public float landingGraceDecay = 0.5f;
        /// <summary>A stick pushed this far counts as fully pushed (gamepad players reach sprint).</summary>
        public float stickFullAt = 0.7f;

        // ---------- Jumps ----------
        public float gravity = 24f;
        /// <summary>Ground jump height (7.75 m/s at gravity 24).</summary>
        public float jumpHeight = 1.25f;
        /// <summary>Extra height of the double jump.</summary>
        public float doubleJumpHeight = 1.0f;
        /// <summary>The double jump can't fire this soon after a jump.</summary>
        public float doubleJumpLockout = 0.1f;
        /// <summary>How far the double jump turns your flight toward the stick (0 none, 1 all the way). Speed is kept.</summary>
        public float doubleJumpRedirect = 0.5f;
        /// <summary>A double jump from a near standstill pushes you at least this fast toward the stick.</summary>
        public float doubleJumpMinSpeed = 4f;
        /// <summary>The double jump also pushes you this much along the stick (or the view, stick neutral and moving forward),
        /// never past sprint speed, nothing above it: getting started without a wall nearby (the creator, 2026-10-10).</summary>
        public float doubleJumpForwardBoost = 1f;
        /// <summary>Jump still works this long after walking off a ledge.</summary>
        public float coyoteTime = 0.12f;
        /// <summary>A jump pressed this long before landing (or reaching a wall) still fires.</summary>
        public float jumpBuffer = 0.12f;
        /// <summary>Bunny-hop cost: a ground jump keeps all speed up to sprint and loses this share of the speed above it
        /// (0.08: about 1 m/s at 22 m/s, 0.5 at 15; what a 0.4 s slide burns at speed). Jumps at sprint or below are free.
        /// 0 = the old free hop. Default 0 (the Mild preset; Current is 0.08). A game's hop-cost preset
        /// (<see cref="HopCostPresets"/>) overrides these hop fields while it's set.</summary>
        public float hopSpeedLoss = 0.02f;
        /// <summary>The same for a jump out of a slide (a slide hop): smaller, so the slide is the better speed tool (Current: 0.03).</summary>
        public float slideHopSpeedLoss = 0.01f;
        /// <summary>A double jump this soon after a ground jump (the hop-and-steer) pays for its turn...</summary>
        public float doubleJumpHopWindow = 0.35f;
        /// <summary>...this share of the speed above sprint for a 90 degree turn of the flight, in proportion (a 45 degree
        /// redirect, stick square to the flight, costs half). Later double jumps and wall jumps turn for free. 0 = free.</summary>
        public float doubleJumpHopTurnLoss = 0.1f;
        /// <summary>...but the first this-many degrees of the turn are free (Mild: 45, so the usual 45 degree redirect costs
        /// nothing and only sharper turns pay; Current: 0).</summary>
        public float doubleJumpHopTurnFree = 45f;

        // ---------- Air ----------
        /// <summary>Source air accelerate (air strafing).</summary>
        public float airAccel = 12f;
        /// <summary>Source air speed cap (0.8 m/s = 30 Quake units): small, so strafing gains slowly.</summary>
        public float airCap = 0.8f;
        /// <summary>Gentle extra air steering up to this speed, so standing jumps can be aimed.</summary>
        public float airControlSpeed = 5f;
        public float airControlAccel = 2f;
        /// <summary>Above <see cref="airControlSpeed"/>, speed gained in the air (the air strafe and the air control) needs
        /// strafe input: the move input must point at least this far sideways (the sideways share of the move vector; 0.5
        /// = 30 degrees off forward) for the full gain, less in proportion. Forward alone (W, or the stick straight up)
        /// still turns your flight toward the view but adds no speed: no more W-only strafing (the creator, 2026-10-11).
        /// 0 = pure Source, any input gains.</summary>
        public float airStrafeSideShare = 0.5f;
        /// <summary>The turn-rate equalizer (2026-10-12): air-strafe gains made while the view turns faster than this
        /// (deg/s) count only in proportion (at 640 deg/s, half). A stick tops out at 304 (190 x the full-tilt boost),
        /// so this only reins in a mouse sweeping faster than any stick can turn: one standard for both devices.
        /// 0 = no limit.</summary>
        public float airStrafeTurnCap = 320f;
        /// <summary>Whether the air control may add speed above <see cref="airControlSpeed"/> with strafe input (1, keeps
        /// controller and A/D strafing as strong as before) or only redirects (0: strafing gains 25-40% less, on a stick too).</summary>
        public float airControlStrafeGain = 1f;
        /// <summary>Horizontal speed never goes above this (about 2.3x sprint).</summary>
        public float maxHorizontalSpeed = 22f;
        public float terminalFall = 40f;

        // ---------- Slide ----------
        /// <summary>Crouch at or above this speed starts a slide.</summary>
        public float slideMinSpeed = 6.5f;
        /// <summary>Speed added when a slide starts (once per cooldown).</summary>
        public float slideBoost = 3f;
        public float slideBoostCooldown = 2f;
        /// <summary>The boost never takes you above this cap...</summary>
        public float slideBoostMaxSpeed = 13f;
        /// <summary>...and the cap drops by this much per second of recent sliding (Titanfall 2: the cap decays the
        /// longer you slide). Recent sliding wears off at 1 s per second off the slide.</summary>
        public float slideCapDecay = 1.5f;
        /// <summary>Slide deceleration for the first <see cref="slideEarlyTime"/> seconds, then <see cref="slideDecel"/>.</summary>
        public float slideDecelEarly = 2.5f;
        public float slideEarlyTime = 0.6f;
        public float slideDecel = 4f;
        /// <summary>Downhill pull while sliding on a slope (1 = gravity along the slope; 1.5 makes slide ramps clearly speed you up).</summary>
        public float slideSlopeScale = 1.5f;
        /// <summary>The slide ends below this speed.</summary>
        public float slideExitSpeed = 4f;
        /// <summary>Steering while sliding, degrees per second.</summary>
        public float slideTurnRate = 60f;
        /// <summary>A jump out of a slide is this fraction of a full jump (all horizontal speed is kept).</summary>
        public float slideJumpScale = 0.9f;
        /// <summary>Crouch pressed this long before landing turns the landing into a slide (slide hop).</summary>
        public float slideHopBuffer = 0.2f;

        // ---------- Wallrun ----------
        public float wallMinSpeed = 5f;
        /// <summary>Only this high above the ground or more.</summary>
        public float wallMinHeight = 0.6f;
        /// <summary>Walls must be near vertical: |normal.y| at most this.</summary>
        public float wallMaxNormalY = 0.25f;
        /// <summary>Moving along the wall, not into it: dot(direction, wall tangent) at least this (70 degrees). 75 was tried
        /// (2026-10-10) and caught the END faces of walls when you came in at a slant (the Gauntlet's combo wall, at
        /// 22 m/s: a "wallrun" at 4.7 m/s into the void), so the catch was loosened by reach, stick and a buffer instead.</summary>
        public float wallMinAlongDot = 0.34f;
        /// <summary>The stick may not push away from the wall more than this (dot(wish, -normal)); -0.2 forgives
        /// a view turned about 12 degrees away.</summary>
        public float wallMinTowardDot = -0.2f;
        /// <summary>Wall probes: from this height above the feet, sphere radius, reach beyond the body radius.</summary>
        public float wallProbeHeight = 1.0f;
        public float wallProbeRadius = 0.25f;
        public float wallProbeReach = 0.6f;
        /// <summary>Wallrun goal speed. Titanfall 2 measured 340 u/s = 8.64 m/s (docs/RESEARCH-MOVEMENT.md 9):
        /// slower entries accelerate up to it, faster ones are slowed toward it by wall friction, so a quick
        /// wall kick keeps the most speed.</summary>
        public float wallRunSpeed = 8.64f;
        /// <summary>Acceleration up to the goal while holding forward (m/s per second).</summary>
        public float wallRunAccel = 6f;
        /// <summary>Wall friction above the goal: the excess fades at this rate per second (exponential), after
        /// <see cref="wallSpeedGrace"/>. Research 9 measures only the goal (340 u/s) and that early kicks lose less, not a rate:
        /// 2 felt like the wall bled speed fast (the creator, 2026-10-10), so 1.2: a 14 m/s entry is about 11.0 at 1.2 s and 9.8 at 1.75 s.</summary>
        public float wallRunFriction = 1.2f;
        /// <summary>For this long after catching a wall, speed above the goal doesn't fade at all (a kick in this window keeps the entry speed).</summary>
        public float wallSpeedGrace = 0.5f;
        /// <summary>No stick at all brakes toward 0 at this Source friction (Titanfall 2), clear backward input twice as hard.
        /// Any stick held with a forward part along the wall, or the view along it, counts as forward (controller sticks held at an angle).</summary>
        public float wallNeutralFriction = 4f;
        /// <summary>To start a wallrun the stick must point at least this far along the wall (dot(wish, tangent)): forward input to start (Titanfall 2).</summary>
        public float wallForwardDot = 0.3f;
        /// <summary>Longest wallrun, seconds (Titanfall 2: 1.75 s).</summary>
        public float wallRunDuration = 1.75f;
        /// <summary>Gravity scale on the wall: nearly weightless at first, sinking later.</summary>
        public float wallGravityStart = 0.1f;
        public float wallGravityEnd = 0.8f;
        /// <summary>Fastest sink on the wall: from this at the start to <see cref="wallSinkEnd"/> at the end.</summary>
        public float wallSinkStart = 1f;
        public float wallSinkEnd = 6f;
        /// <summary>A small push into the wall keeps contact.</summary>
        public float wallStick = 0.5f;
        /// <summary>Losing the wall for longer than this ends the run (bridges seams).</summary>
        public float wallLostGrace = 0.1f;
        /// <summary>The run ends when the speed along the wall drops below this.</summary>
        public float wallExitSpeed = 4f;
        /// <summary>The stick pointing away from the wall (dot(wish, normal) over this, about 53 degrees off the wall) for
        /// <see cref="wallAwayTime"/> ends the run (Titanfall 2 lets go at once; was 0.5 / 30 degrees, loosened for controllers).</summary>
        public float wallAwayDot = 0.8f;
        public float wallAwayTime = 0.06f;
        /// <summary>Running along the wall: a stick with at least this forward part along it (or the view along it) holds the run.</summary>
        public float wallHoldForwardDot = 0.1f;
        /// <summary>A catch refused only because of the stick's direction is retried for this long (with a little more reach) as the stick comes round.</summary>
        public float wallCatchBuffer = 0.1f;
        /// <summary>Wall jump, Titanfall 2 measured: always this much along the wall normal (205 u/s)...</summary>
        public float wallJumpOut = 5.21f;
        /// <summary>...plus up to this along the wall, scaled by how much the stick points along it (75 u/s)...</summary>
        public float wallJumpAlong = 1.91f;
        /// <summary>...minus up to this along the normal when the stick points into the wall (172 u/s x dot(wish, -normal)).</summary>
        public float wallJumpInward = 4.37f;
        /// <summary>Crouch held during a wall jump adds this along the normal (crouch kick, 70 u/s).</summary>
        public float wallJumpCrouchKick = 1.78f;
        /// <summary>Upward speed of a wall jump (same as a ground jump by default).</summary>
        public float wallJumpUp = 7.75f;
        /// <summary>A wall jump this soon after touching the wall is a "wall kick" (5 ticks at 60 Hz): it has lost almost nothing to wall friction.</summary>
        public float wallKickWindow = 0.083f;
        /// <summary>Jump still counts as a wall jump this long after leaving a wall.</summary>
        public float wallCoyote = 0.2f;
        /// <summary>The same wall can be caught again only this much lower than where you left it (or after touching ground).</summary>
        public float sameWallDrop = 0.1f;
        /// <summary>Walls whose normals differ by less than this (degrees) count as the same wall (Titanfall 2: a 25 degree curve is a new wall).</summary>
        public float sameWallAngle = 25f;

        // ---------- Mantle ----------
        /// <summary>Ledges this high above the feet (and up to <see cref="mantleMaxHeight"/>) pull you up.</summary>
        public float mantleMinHeight = 0.4f;
        public float mantleMaxHeight = 1.9f;
        /// <summary>How close the ledge face must be (beyond the body radius).</summary>
        public float mantleReach = 0.6f;
        /// <summary>Not when falling faster than this.</summary>
        public float mantleMaxFall = 10f;
        /// <summary>Mantle time = base + perMetre x height.</summary>
        public float mantleBaseTime = 0.2f;
        public float mantlePerMetre = 0.07f;
        /// <summary>You leave a mantle at max(keep x entry speed, min).</summary>
        public float mantleSpeedKeep = 0.8f;
        public float mantleExitSpeed = 5f;

        public float JumpSpeed => (float)Math.Sqrt(2.0 * gravity * jumpHeight);
        public float DoubleJumpSpeed => (float)Math.Sqrt(2.0 * gravity * doubleJumpHeight);
        public float StepSeconds => 1f / (tickRate > 1f ? tickRate : 1f);
        public float WalkableMinNormalY => (float)Math.Cos(slopeLimit * Math.PI / 180.0);

        public MoveDef Clone() => (MoveDef)MemberwiseClone();
    }
}
