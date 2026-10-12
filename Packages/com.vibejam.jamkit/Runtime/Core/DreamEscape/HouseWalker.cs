using System;
using System.Collections.Generic;
using System.Numerics;
using JamKit.Movement;
using JamKit.Stealth;

namespace JamKit.DreamEscape
{
    /// <summary>
    /// Walks her round the house by herself (the hide-and-seek's autopilot: the recorded runs, attract mode, the tests):
    /// plans a path over Theo's walking map from where she is to a target, follows it point by point (turning toward the
    /// next, crouching through the low doors), and says when she's there. Reads only the sim, like the course autopilot, so
    /// it drives the fake world and the real controller alike. No allocation per frame.
    /// </summary>
    public sealed class HouseWalker
    {
        readonly DreamHouse _h;
        readonly IStealthWorld _world;
        readonly WaypointGraph _g;
        readonly List<int> _path = new List<int>(64);
        readonly List<Vector3> _points = new List<Vector3>(64);
        int _k;
        float _stuck, _lastDist;
        bool _jumped;
        public bool sprint = true;
        /// <summary>How close counts as there.</summary>
        public float arrive = 0.6f;

        public HouseWalker(DreamHouse house, IStealthWorld world, WaypointGraph graph)
        {
            _h = house;
            _world = world;
            _g = graph;
        }

        public bool Arrived => _points.Count == 0 || _k >= _points.Count;
        public Vector3 Target => _points.Count > 0 ? _points[_points.Count - 1] : Vector3.Zero;
        /// <summary>No progress for two seconds (blocked).</summary>
        public bool Stuck => _stuck > 2f;

        /// <summary>Plans a walk from <paramref name="from"/> to <paramref name="to"/>; false when there's no way.</summary>
        public bool Go(Vector3 from, Vector3 to)
        {
            _points.Clear();
            _k = 0;
            _stuck = 0f;
            _lastDist = float.MaxValue;
            int a = Nearest(from, false), b = Nearest(to, true);
            if (a < 0 || b < 0 || !_g.FindPath(a, b, _path)) return false;
            foreach (int n in _path) _points.Add(_g[n]);
            _points.Add(to);
            return true;
        }

        public void Stop()
        {
            _points.Clear();
            _k = 0;
        }

        int Nearest(Vector3 p, bool clearToIt)
        {
            var room = _h.RoomAt(p + new Vector3(0f, 0.1f, 0f));
            int best = -1;
            float bd = float.MaxValue;
            for (int i = 0; i < _h.nodes.Count; i++)
            {
                var n = _h.nodes[i].p;
                float dy = Math.Abs(n.Y - p.Y);
                if (dy > 1.6f) continue;
                float d = Vector3.Distance(n, p) + (room != null && _h.nodes[i].room != room.id ? 20f : 0f);
                if (Math.Abs(n.Y - p.Y) < 0.3f && !DreamHouse.WalkClear(_world, n, p, 0.3f)) d += 10f;
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        /// <summary>This frame's input (walking or sprinting toward the next point).</summary>
        public MoveInput Next(MoveSim sim, float dt)
        {
            var input = new MoveInput();
            if (Arrived) return input;
            var p = sim.Position;
            var target = _points[_k];
            var d = new Vector2(target.X - p.X, target.Z - p.Z);
            bool last = _k == _points.Count - 1;
            if (d.Length() < (last ? arrive : 0.45f) && Math.Abs(target.Y - p.Y) < 1.0f)
            {
                _k++;
                _lastDist = float.MaxValue;
                if (Arrived) return input;
                target = _points[_k];
                d = new Vector2(target.X - p.X, target.Z - p.Z);
            }
            float dist = d.Length();
            if (dist < _lastDist - 0.02f) { _lastDist = dist; _stuck = 0f; } else _stuck += dt;
            input.yaw = (float)(Math.Atan2(d.X, d.Y) * 180.0 / Math.PI);
            input.move = new Vector2(0f, 1f);
            input.sprintHeld = sprint && !last;
            input.crouchHeld = NearLowDoor(p);
            // Up onto something (the desk under Theo's bookcase, a step): jump at it from close by; the mantle does the rest.
            if (target.Y > p.Y + 0.35f && dist < 1.4f && sim.Grounded && !input.crouchHeld)
            {
                input.jumpPressed = !_jumped;
                input.jumpHeld = true;
                _jumped = true;
            }
            else _jumped = false;
            return input;
        }

        bool NearLowDoor(Vector3 p)
        {
            foreach (var dr in _h.doors)
            {
                if (dr.y1 - dr.y0 >= 1.6f || Math.Abs(p.Y - dr.y0) > 0.5f) continue;
                var c = dr.alongX ? new Vector2((dr.a + dr.b) * 0.5f, dr.c) : new Vector2(dr.c, (dr.a + dr.b) * 0.5f);
                if (Vector2.Distance(new Vector2(p.X, p.Z), c) < 1.6f) return true;
            }
            foreach (var r in _h.rooms)
                if (r.ceiling < 1.75f && r.Contains(p + new Vector3(0f, 0.1f, 0f))) return true;
            return false;
        }
    }
}
