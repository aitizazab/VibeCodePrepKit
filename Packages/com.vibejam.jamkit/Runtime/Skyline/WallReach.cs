using System;
using System.Numerics;
using JamKit.Movement;

namespace JamKit.Skyline
{
    /// <summary>"A wall is in reach" (docs/SKYLINE-DESIGN.md 4.1): a tagged wall-runnable surface within 3 m of the
    /// player while airborne or sprinting. Probed in 8 horizontal directions at chest height.</summary>
    public static class WallReach
    {
        public const float Reach = 3f;
        static readonly Vector3[] Directions = MakeDirections();

        static Vector3[] MakeDirections()
        {
            var d = new Vector3[8];
            for (int i = 0; i < 8; i++)
            {
                float a = i * MathF.PI / 4f;
                d[i] = new Vector3(MathF.Sin(a), 0f, MathF.Cos(a));
            }
            return d;
        }

        public static bool InReach(IMoveWorld world, Vector3 feet, float chestHeight, bool airborneOrSprinting)
        {
            if (!airborneOrSprinting) return false;
            var origin = feet + new Vector3(0f, chestHeight, 0f);
            foreach (var dir in Directions)
                if (world.CastWall(origin, dir, 0.1f, Reach, out var hit) && hit.runnable) return true;
            return false;
        }
    }
}
