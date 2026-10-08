using System;
using System.Numerics;

namespace JamKit.Doomlike
{
    /// <summary>
    /// Fast, slidey first-person movement on the ground plane: accelerate toward the wished velocity,
    /// brake with friction when there's no input, never exceed the wished speed by input alone
    /// (knockback can push past it and bleeds off by friction). Frame-rate independent enough that
    /// 30 and 144 fps feel the same. Speeds in m/s, rates per second.
    /// </summary>
    [Serializable]
    public class PlayerMotionDef
    {
        public float runSpeed = 9.5f;
        public float walkSpeed = 4.5f;
        /// <summary>How fast it reaches full speed (m/s per second).</summary>
        public float acceleration = 70f;
        /// <summary>How fast it stops when you let go (m/s per second).</summary>
        public float friction = 45f;
        /// <summary>Extra grip when reversing direction (multiplies acceleration).</summary>
        public float turnGrip = 1.6f;
    }

    public static class PlayerMotion
    {
        /// <summary>The next ground velocity. <paramref name="wish"/> is the input direction in world XZ
        /// (length 0..1, analog sticks give less than 1).</summary>
        public static Vector2 Step(Vector2 velocity, Vector2 wish, float maxSpeed, PlayerMotionDef def, float dt)
        {
            if (!(dt > 0f)) return velocity;
            float wishLen = wish.Length();
            if (wishLen > 1f) { wish /= wishLen; wishLen = 1f; }
            Vector2 target = wishLen > 0.0001f ? wish * maxSpeed : Vector2.Zero;

            float speed = velocity.Length();
            // Faster than allowed (knockback, a slower walk): friction pulls it back down first.
            if (speed > maxSpeed + 0.01f || wishLen <= 0.0001f)
            {
                float drop = def.friction * dt;
                float newSpeed = Math.Max(0f, speed - drop);
                velocity = speed > 0f ? velocity * (newSpeed / speed) : Vector2.Zero;
                if (wishLen <= 0.0001f) return velocity;
            }

            Vector2 delta = target - velocity;
            float dLen = delta.Length();
            if (dLen < 1e-5f) return target;
            float grip = Vector2.Dot(velocity, target) < 0f ? def.turnGrip : 1f;
            float step = def.acceleration * grip * dt;
            if (step >= dLen) return target;
            return velocity + delta * (step / dLen);
        }

        /// <summary>World XZ wish direction from stick/WASD input (x = strafe, y = forward) and yaw.</summary>
        public static Vector2 WishFromInput(Vector2 input, float yawDegrees)
        {
            float a = yawDegrees * GridMath.Deg;
            // Unity yaw: forward = (sin yaw, cos yaw) on XZ, right = (cos yaw, -sin yaw).
            var forward = new Vector2((float)Math.Sin(a), (float)Math.Cos(a));
            var right = new Vector2((float)Math.Cos(a), -(float)Math.Sin(a));
            return forward * input.Y + right * input.X;
        }
    }
}
