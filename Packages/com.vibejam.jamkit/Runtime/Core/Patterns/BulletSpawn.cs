using System.Numerics;

namespace JamKit.Patterns
{
    /// <summary>
    /// One bullet for the pool to create. Position, angle and speed are already its state at the
    /// end of the tick that emitted it (pre-advanced by <see cref="age"/>), so the pool just copies
    /// them in and steps it from the next frame on with <see cref="BulletMotion.Step"/>.
    /// </summary>
    public struct BulletSpawn
    {
        public Vector2 position;
        /// <summary>Heading in radians, counter-clockwise from +X.</summary>
        public float angle;
        public float speed;
        /// <summary>Units per second squared, along the heading.</summary>
        public float acceleration;
        /// <summary>Radians per second; non-zero curves the shot.</summary>
        public float angularVelocity;
        /// <summary>Total seconds to live; 0 or less means no limit (cull it off-screen).</summary>
        public float lifetime;
        /// <summary>Which sprite / colour / projectile prefab to use.</summary>
        public int styleIndex;
        /// <summary>Seconds it has already lived: how late in the tick it was scheduled. Can exceed
        /// <see cref="lifetime"/> after a long hitch; the pool should drop those.</summary>
        public float age;

        public Vector2 Direction => BulletMotion.Direction(angle);
    }
}
