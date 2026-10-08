using System;

namespace JamKit.Patterns
{
    public enum PatternShape
    {
        /// <summary>count bullets evenly around the full circle.</summary>
        Ring,
        /// <summary>An arc: count bullets evenly across spreadDegrees, centred on the base (or aim) angle.</summary>
        Spread,
        /// <summary>Ring geometry (count = arms); reads as a spiral once rotatePerShotDegrees is set
        /// and shotInterval is short.</summary>
        Spiral,
        /// <summary>A spread that always centres on the target (count 1 = straight at the player).</summary>
        Aimed,
        /// <summary>Random angles within spreadDegrees, from the emitter's seeded Rng.</summary>
        Shotgun,
        /// <summary>A wall: count bullets evenly across lineWidth, side by side, all flying the same way.</summary>
        Line,
    }

    /// <summary>
    /// One attack, as designer data: what a volley looks like and how many volleys, how fast.
    /// Angles are degrees here (0 = +X, counter-clockwise; -90 is "down" in 2D); the emitter
    /// converts to radians. Speeds are world units per second.
    /// </summary>
    [Serializable]
    public class PatternDef
    {
        public PatternShape shape = PatternShape.Ring;
        /// <summary>Bullets per volley (before the density assist).</summary>
        public int count = 12;
        /// <summary>Arc width for Spread, Aimed and Shotgun.</summary>
        public float spreadDegrees = 60f;
        /// <summary>Wall width for Line, in world units.</summary>
        public float lineWidth = 4f;
        /// <summary>Centre direction, or the offset from the aim direction when aiming.</summary>
        public float baseAngleDegrees;
        /// <summary>Each volley turns by this much: spirals and rotating rings.</summary>
        public float rotatePerShotDegrees;
        /// <summary>Volleys to fire; 0 or less fires forever.</summary>
        public int shots = 1;
        public float shotInterval = 0.1f;
        public float speed = 5f;
        /// <summary>Added per bullet inside a volley (bullet i gets speed + i * speedStep): stacks.</summary>
        public float speedStep;
        public float acceleration;
        /// <summary>Turn rate of each bullet in flight: curving shots.</summary>
        public float angularVelocityDegrees;
        /// <summary>Centre each volley on the target (the player) instead of the fixed base angle.</summary>
        public bool aimAtTarget;
        /// <summary>Each bullet's angle is nudged by a seeded random amount up to this, either way.</summary>
        public float jitterDegrees;
        public float startDelay;
        /// <summary>Seconds each bullet lives; 0 or less means no limit.</summary>
        public float lifetime = 8f;
        /// <summary>Also fire the volley reflected across the base (or aim) direction: a spiral
        /// gains a counter-rotating twin, a curving shot a mirror-image partner.</summary>
        public bool mirror;
        public int styleIndex;
        /// <summary>Bullets start on a circle this big around the origin instead of at its centre.</summary>
        public float spawnRadius;

        /// <summary>Time of the last volley after the pattern starts; infinity when it fires forever.</summary>
        public float Duration =>
            shots <= 0 ? float.PositiveInfinity
                       : Math.Max(0f, startDelay) + (shots - 1) * Math.Max(0f, shotInterval);
    }
}
