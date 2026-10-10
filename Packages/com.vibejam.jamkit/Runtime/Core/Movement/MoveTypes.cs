using System.Numerics;

namespace JamKit.Movement
{
    /// <summary>What the body is doing. Sprint is running on the ground at sprint speed (auto-sprint).</summary>
    public enum MoveMode { Grounded, Sprint, Slide, Air, Wallrun, Mantle }

    /// <summary>Why the last runnable wall within reach (or just beyond it) in the air wasn't caught: the catch diagnostic
    /// the lab's F3 overlay shows. None after a catch.</summary>
    public enum WallCatchMiss { None, OutOfReach, TooSlow, TooLow, Crouching, HeadOn, NoStick, StickAway, StickNotForward, SameWall }

    /// <summary>
    /// One simulation step's input. Axes are Unity's: x right, y up, z forward; <see cref="yaw"/> in
    /// degrees like a Unity transform (0 looks along +Z, 90 along +X). "Pressed" flags are true on the
    /// one step that should react to a new press (<see cref="MoveDriver"/> latches them across frames).
    /// </summary>
    public struct MoveInput
    {
        /// <summary>Stick or keys: x strafe right, y forward, length 0..1.</summary>
        public Vector2 move;
        public float yaw;
        public bool jumpHeld, jumpPressed;
        public bool crouchHeld, crouchPressed;
        /// <summary>Sprint held (Left Shift, LB). Ignored when auto-sprint is on.</summary>
        public bool sprintHeld;
        /// <summary>The sprint toggle (pad L3) was clicked: sprint until you stop moving forward, crouch, slide or respawn.</summary>
        public bool sprintTogglePressed;
    }

    /// <summary>A surface a probe found. <see cref="distance"/> is along the probe; <see cref="id"/>
    /// tells colliders apart; <see cref="runnable"/> is false on surfaces marked "no wallrun".</summary>
    public struct SurfaceHit
    {
        public Vector3 point;
        public Vector3 normal;
        public float distance;
        public int id;
        public bool runnable;
    }

    /// <summary>
    /// The world as the movement core sees it: Unity implements it with a CharacterController and
    /// physics casts, the tests with boxes. Positions are the feet (bottom of the capsule).
    /// </summary>
    public interface IMoveWorld
    {
        /// <summary>Moves the capsule by <paramref name="delta"/>, sliding along what it hits. Writes the
        /// normals of what it touched into <paramref name="normals"/> (up to its length) and returns the new feet position.</summary>
        Vector3 Move(Vector3 feet, Vector3 delta, float radius, float height, Vector3[] normals, out int hitCount);

        /// <summary>Ground under the feet: <c>hit.distance</c> is from the feet down to the surface (about 0 when standing).</summary>
        bool CastDown(Vector3 feet, float radius, float maxDistance, out SurfaceHit hit);

        /// <summary>A sphere swept from <paramref name="origin"/> along the horizontal <paramref name="direction"/>.</summary>
        bool CastWall(Vector3 origin, Vector3 direction, float radius, float maxDistance, out SurfaceHit hit);

        /// <summary>A ray straight down from <paramref name="origin"/> (ledge tops).</summary>
        bool CastLedge(Vector3 origin, float maxDistance, out SurfaceHit hit);

        /// <summary>True when a capsule of this size fits here without touching anything.</summary>
        bool CapsuleFits(Vector3 feet, float radius, float height);
    }

    /// <summary>What happened during a step (or a frame, merged): the feel layer turns these into sound and camera.</summary>
    public struct MoveEvents
    {
        public bool jumped, doubleJumped, wallJumped, wallKick;
        public bool landed;
        /// <summary>Downward speed when landing (m/s, positive).</summary>
        public float landImpact;
        public bool slideStarted, slideBoosted, slideEnded;
        public bool wallrunStarted, wallrunEnded;
        /// <summary>+1 wall on the right, -1 on the left (with <see cref="wallrunStarted"/>).</summary>
        public int wallSide;
        public bool mantleStarted;
        public bool leftGround;
        /// <summary>A sprint kick: the push-off back to running speed.</summary>
        public bool sprintKick;
        /// <summary>Speed (m/s) a jump cost: the bunny-hop cost, and a hop-and-steer double jump's turn.</summary>
        public float hopSpeedLost;

        public bool Any => jumped || doubleJumped || wallJumped || landed || slideStarted || slideEnded || wallrunStarted || wallrunEnded || mantleStarted || leftGround || sprintKick;

        public void Merge(in MoveEvents e)
        {
            jumped |= e.jumped;
            doubleJumped |= e.doubleJumped;
            wallJumped |= e.wallJumped;
            wallKick |= e.wallKick;
            if (e.landed)
            {
                landed = true;
                if (e.landImpact > landImpact) landImpact = e.landImpact;
            }
            slideStarted |= e.slideStarted;
            slideBoosted |= e.slideBoosted;
            slideEnded |= e.slideEnded;
            hopSpeedLost += e.hopSpeedLost;
            if (e.wallrunStarted) { wallrunStarted = true; wallSide = e.wallSide; }
            wallrunEnded |= e.wallrunEnded;
            mantleStarted |= e.mantleStarted;
            leftGround |= e.leftGround;
            sprintKick |= e.sprintKick;
        }
    }
}
