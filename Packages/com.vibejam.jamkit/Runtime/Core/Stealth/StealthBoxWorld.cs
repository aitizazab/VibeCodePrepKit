using System;
using System.Collections.Generic;
using System.Numerics;

namespace JamKit.Stealth
{
    /// <summary>
    /// A plain stealth world of axis-aligned boxes that block sight and boxes of light: what the core tests (and any
    /// simple game without physics) see through. The Unity side uses physics and light zones instead.
    /// </summary>
    public sealed class StealthBoxWorld : IStealthWorld
    {
        public struct Box
        {
            public Vector3 min, max;
            public static Box Of(Vector3 center, Vector3 size) => new Box { min = center - size * 0.5f, max = center + size * 0.5f };
            public bool Contains(Vector3 p) => p.X >= min.X && p.X <= max.X && p.Y >= min.Y && p.Y <= max.Y && p.Z >= min.Z && p.Z <= max.Z;
        }

        struct LightBox { public Box box; public float level; }

        readonly List<Box> _blockers = new List<Box>();
        readonly List<LightBox> _lights = new List<LightBox>();

        /// <summary>Light everywhere no light box covers.</summary>
        public float ambient = 1f;

        public int BlockerCount => _blockers.Count;

        public StealthBoxWorld AddBlocker(Vector3 center, Vector3 size) { _blockers.Add(Box.Of(center, size)); return this; }

        /// <summary>A box of light at <paramref name="level"/> (0 dark, 1 lit). Where boxes overlap, the last one added wins.</summary>
        public StealthBoxWorld AddLight(Vector3 center, Vector3 size, float level) { _lights.Add(new LightBox { box = Box.Of(center, size), level = level }); return this; }

        public bool LineClear(Vector3 from, Vector3 to)
        {
            for (int i = 0; i < _blockers.Count; i++)
                if (SegmentHits(from, to, _blockers[i])) return false;
            return true;
        }

        public float LightAt(Vector3 point)
        {
            for (int i = _lights.Count - 1; i >= 0; i--)
                if (_lights[i].box.Contains(point)) return _lights[i].level;
            return ambient;
        }

        /// <summary>Slab test: does the segment from a to b pass through the box (touching its surface from inside counts)?</summary>
        public static bool SegmentHits(Vector3 a, Vector3 b, Box box)
        {
            float t0 = 0f, t1 = 1f;
            Vector3 d = b - a;
            if (!Slab(a.X, d.X, box.min.X, box.max.X, ref t0, ref t1)) return false;
            if (!Slab(a.Y, d.Y, box.min.Y, box.max.Y, ref t0, ref t1)) return false;
            if (!Slab(a.Z, d.Z, box.min.Z, box.max.Z, ref t0, ref t1)) return false;
            return t1 > t0;
        }

        static bool Slab(float start, float dir, float min, float max, ref float t0, ref float t1)
        {
            if (Math.Abs(dir) < 1e-8f) return start > min && start < max;
            float a = (min - start) / dir, b = (max - start) / dir;
            if (a > b) { float t = a; a = b; b = t; }
            if (a > t0) t0 = a;
            if (b < t1) t1 = b;
            return t1 > t0;
        }
    }
}
