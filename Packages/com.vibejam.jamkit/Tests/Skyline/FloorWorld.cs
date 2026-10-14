using System.Collections.Generic;
using System.Numerics;
using JamKit.Skyline;
using JamKit.Tests.Movement;

namespace JamKit.Tests.Skyline
{
    /// <summary>Test-only: the Food Court's <see cref="RepossessArena"/> solids as a <see cref="BoxWorld"/>, so the real
    /// <c>MoveSim</c> walks the same geometry the Unity builder will make colliders for.</summary>
    public static class FloorWorld
    {
        public static BoxWorld Build() => Build(null);

        /// <summary>Builds the world; <paramref name="boxes"/>, when given, receives each solid's box by id (so a test can
        /// disable one, like a broken pane or an opened shutter, with <c>box.enabled = false</c>).</summary>
        public static BoxWorld Build(Dictionary<string, BoxWorld.Box> boxes)
        {
            var w = new BoxWorld();
            foreach (var s in RepossessArena.Solids())
            {
                var b = s.box;
                BoxWorld.Box made;
                if (s.ramp)
                {
                    made = s.rampAlongX
                        ? w.RampX(b.min.X, b.max.X, b.min.Z, b.max.Z, s.rampTopStart, s.rampTopEnd, b.min.Y)
                        : w.Ramp(b.min.X, b.max.X, b.min.Z, b.max.Z, s.rampTopStart, s.rampTopEnd, b.min.Y);
                    made.runnable = s.runnable;
                }
                else made = w.Add((b.min + b.max) * 0.5f, b.max - b.min, s.runnable);
                if (boxes != null) boxes[s.id] = made;
            }
            return w;
        }
    }
}
