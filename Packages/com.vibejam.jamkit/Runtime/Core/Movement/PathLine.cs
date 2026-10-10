using System;
using System.Collections.Generic;
using System.Numerics;

namespace JamKit.Movement
{
    /// <summary>
    /// A route laid down a level as a polyline on the ground plane (XZ; heights are ignored), measured
    /// by distance along it ("s"). Chase pursuers, checkpoints and scripted beats use s rather than
    /// straight-line distance (docs/RESEARCH-MOVEMENT.md 6.3). No allocation after construction.
    /// </summary>
    public sealed class PathLine
    {
        readonly List<Vector2> _points = new List<Vector2>();
        readonly List<float> _s = new List<float>();

        public PathLine(IList<Vector3> points)
        {
            float s = 0f;
            for (int i = 0; i < points.Count; i++)
            {
                var p = new Vector2(points[i].X, points[i].Z);
                if (i > 0) s += Vector2.Distance(_points[i - 1], p);
                _points.Add(p);
                _s.Add(s);
            }
            if (_points.Count == 1) { _points.Add(_points[0] + new Vector2(0f, 1f)); _s.Add(1f); }
        }

        public float Length => _s[_s.Count - 1];
        public int Count => _points.Count;

        /// <summary>Distance along the route of the nearest point to <paramref name="p"/>, and the signed
        /// sideways offset (positive = right of the route's direction).</summary>
        public float Project(Vector3 p, out float lateral)
        {
            var q = new Vector2(p.X, p.Z);
            float best = float.MaxValue, bestS = 0f;
            lateral = 0f;
            for (int i = 0; i + 1 < _points.Count; i++)
            {
                Vector2 a = _points[i], b = _points[i + 1];
                Vector2 ab = b - a;
                float len2 = ab.LengthSquared();
                float t = len2 > 1e-8f ? Vector2.Dot(q - a, ab) / len2 : 0f;
                if (i > 0 && t < 0f) t = 0f;
                if (i + 2 < _points.Count && t > 1f) t = 1f;
                Vector2 c = a + ab * t;
                float d = Vector2.DistanceSquared(q, c);
                if (d < best)
                {
                    best = d;
                    float segLen = (float)Math.Sqrt(len2);
                    bestS = _s[i] + t * segLen;
                    Vector2 dir = segLen > 1e-6f ? ab / segLen : new Vector2(0f, 1f);
                    // Right of the direction in Unity's axes: (dir.y, -dir.x) as (x, z).
                    lateral = Vector2.Dot(q - c, new Vector2(dir.Y, -dir.X));
                }
            }
            return bestS;
        }

        public float Project(Vector3 p) => Project(p, out _);

        /// <summary>The point at distance <paramref name="s"/> (extended straight past both ends), moved
        /// <paramref name="lateral"/> to the right. Y is 0.</summary>
        public Vector3 PointAt(float s, float lateral = 0f)
        {
            int i = Segment(s);
            Vector2 a = _points[i], b = _points[i + 1];
            float segLen = _s[i + 1] - _s[i];
            Vector2 dir = segLen > 1e-6f ? (b - a) / segLen : new Vector2(0f, 1f);
            Vector2 p = a + dir * (s - _s[i]) + new Vector2(dir.Y, -dir.X) * lateral;
            return new Vector3(p.X, 0f, p.Y);
        }

        /// <summary>The route's horizontal direction at <paramref name="s"/>.</summary>
        public Vector3 DirectionAt(float s)
        {
            int i = Segment(s);
            Vector2 d = _points[i + 1] - _points[i];
            float l = d.Length();
            return l > 1e-6f ? new Vector3(d.X / l, 0f, d.Y / l) : new Vector3(0f, 0f, 1f);
        }

        int Segment(float s)
        {
            for (int i = 0; i + 2 < _points.Count; i++)
                if (s < _s[i + 1]) return i;
            return _points.Count - 2;
        }
    }

    /// <summary>
    /// A chase pursuer on a <see cref="PathLine"/> that rubber-bands to keep a gap
    /// (docs/RESEARCH-MOVEMENT.md 6.3): speed = clamp(player's average speed + k x (gap - target), min, max).
    /// It catches the player only when the gap stays under <see cref="catchGap"/> for <see cref="catchTime"/>
    /// seconds, which only happens when the player stops or falls back, never as a surprise burst.
    /// Speed buys a bigger gap: the cap is a little above sprint.
    /// </summary>
    [Serializable]
    public sealed class Pursuer
    {
        public float targetGap = 12f;
        public float k = 0.6f;
        public float minSpeed = 6f;
        public float maxSpeed = 11.5f;
        public float catchGap = 2f;
        public float catchTime = 0.75f;
        /// <summary>Seconds of the player's speed the pursuer averages.</summary>
        public float averageTime = 1f;

        float _s, _speed, _avg, _close;

        public float S => _s;
        public float Speed => _speed;
        public float Gap(float playerS) => playerS - _s;
        /// <summary>0..1: how close it is to catching (for sound and screen effects).</summary>
        public float Threat(float playerS) => MoveMath.Clamp01(1f - (Gap(playerS) - catchGap) / Math.Max(0.01f, targetGap - catchGap));

        /// <summary>Puts it <see cref="targetGap"/> behind the player (chase start, respawn).</summary>
        public void Reset(float playerS, float playerSpeed = 0f)
        {
            _s = playerS - targetGap;
            _avg = playerSpeed;
            _speed = playerSpeed;
            _close = 0f;
        }

        /// <summary>Advances it; returns true on the step it catches the player.</summary>
        public bool Step(float playerS, float playerSpeedAlong, float dt)
        {
            if (!(dt > 0f)) return false;
            float a = averageTime > 1e-3f ? 1f - (float)Math.Exp(-dt / averageTime) : 1f;
            _avg += (Math.Max(0f, playerSpeedAlong) - _avg) * a;
            float gap = playerS - _s;
            float v = _avg + k * (gap - targetGap);
            _speed = v < minSpeed ? minSpeed : v > maxSpeed ? maxSpeed : v;
            _s += _speed * dt;
            if (playerS - _s < catchGap) _close += dt;
            else _close = 0f;
            if (_close >= catchTime)
            {
                _close = 0f;
                return true;
            }
            return false;
        }
    }
}
