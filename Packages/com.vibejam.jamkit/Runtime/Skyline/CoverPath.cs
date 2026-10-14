using System;
using System.Collections.Generic;
using System.Numerics;

namespace JamKit.Skyline
{
    /// <summary>
    /// The arena demo's way round the cover (fix "stuck", 2026-10-14): the shortest path on the floor plan between the
    /// corners of the cover boxes, each box grown by a walking margin. It replaces a greedy "head for the best corner of the
    /// first box in the way", which had two faults that trapped the demo in the information booth (three screens in a U
    /// open to the south, the food court's hide) for the rest of the recording (passes 20261014-0700 and -0735):
    /// <list type="bullet">
    /// <item>a box whose margin the walker was already standing in didn't count at all, so from a duck spot near the booth's
    /// north screen the way north ran straight into it;</item>
    /// <item>one corner at a time can't leave a U from inside: the north screen's best corner lay behind a side screen, and
    /// the side screen's best corner back behind the north screen.</item>
    /// </list>
    /// Here a walker inside a box's margin is still kept out of the box itself (grown by <c>inner</c>), and the path is a
    /// real shortest path (Dijkstra over the corners, their sight lines worked out once). A goal it can't reach (inside a
    /// piece) sends it to the reachable corner nearest the goal. Pure and allocation-free per query after construction.
    /// </summary>
    public sealed class CoverPath
    {
        readonly Vector2[] _min, _max;
        readonly float _margin, _inner;
        readonly Vector2[] _nodes;
        readonly bool[] _see;   // node-to-node sight lines, n x n
        readonly float[] _dist;
        readonly int[] _prev;
        readonly bool[] _done;

        /// <param name="boxes">The cover boxes on the floor plan (x, z).</param>
        /// <param name="margin">How far from a box a straight walk keeps.</param>
        /// <param name="cornerMargin">How far out from a box's corners the waypoints sit (more than <paramref name="margin"/>).</param>
        /// <param name="inner">The margin kept from a box whose <paramref name="margin"/> the walker (or its goal) is already inside.</param>
        public CoverPath(IReadOnlyList<(Vector2 min, Vector2 max)> boxes, float margin, float cornerMargin, float inner = 0.3f)
        {
            if (boxes == null) throw new ArgumentNullException(nameof(boxes));
            if (!(cornerMargin > margin)) throw new ArgumentException("the corners must sit outside the margin", nameof(cornerMargin));
            int n = boxes.Count;
            _min = new Vector2[n];
            _max = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                _min[i] = Vector2.Min(boxes[i].min, boxes[i].max);
                _max[i] = Vector2.Max(boxes[i].min, boxes[i].max);
            }
            _margin = margin;
            _inner = Math.Min(inner, margin);
            var nodes = new List<Vector2>();
            for (int i = 0; i < n; i++)
            {
                float m = cornerMargin;
                foreach (var c in new[]
                {
                    new Vector2(_min[i].X - m, _min[i].Y - m), new Vector2(_max[i].X + m, _min[i].Y - m),
                    new Vector2(_min[i].X - m, _max[i].Y + m), new Vector2(_max[i].X + m, _max[i].Y + m),
                })
                {
                    bool inside = false;
                    for (int k = 0; k < n && !inside; k++) inside = Inside(c, k, _margin);
                    if (!inside) nodes.Add(c);
                }
            }
            _nodes = nodes.ToArray();
            int count = _nodes.Length;
            _see = new bool[count * count];
            for (int a = 0; a < count; a++)
                for (int b = a + 1; b < count; b++)
                    _see[a * count + b] = _see[b * count + a] = !Blocked(_nodes[a], _nodes[b], false);
            _dist = new float[count];
            _prev = new int[count];
            _done = new bool[count];
        }

        /// <summary>The waypoints (corners) the path may use.</summary>
        public IReadOnlyList<Vector2> Nodes => _nodes;

        /// <summary>Whether a walk straight from <paramref name="from"/> to <paramref name="to"/> keeps clear of the cover (a box
        /// whose margin an end is inside counts with the inner margin; a start inside even that doesn't count).</summary>
        public bool Clear(Vector2 from, Vector2 to) => !Blocked(from, to, true);

        /// <summary>Where to head next on the way from <paramref name="from"/> to <paramref name="goal"/>: the goal itself when
        /// the way is straight, else the next corner of the shortest way round; with no way there, the next corner toward the
        /// reachable corner nearest the goal (or the goal itself when the walker is already the nearest it can get).</summary>
        public Vector2 Next(Vector2 from, Vector2 goal)
        {
            if (Clear(from, goal)) return goal;
            int n = _nodes.Length;
            for (int k = 0; k < n; k++)
            {
                _done[k] = false;
                _prev[k] = -1;
                _dist[k] = Blocked(from, _nodes[k], true) ? float.PositiveInfinity : Vector2.Distance(from, _nodes[k]);
            }
            float bestTotal = float.PositiveInfinity;
            int bestEnd = -1;
            while (true)
            {
                int u = -1;
                float du = float.PositiveInfinity;
                for (int k = 0; k < n; k++)
                    if (!_done[k] && _dist[k] < du) { du = _dist[k]; u = k; }
                if (u < 0 || du >= bestTotal) break;
                _done[u] = true;
                if (!Blocked(_nodes[u], goal, true))
                {
                    float total = du + Vector2.Distance(_nodes[u], goal);
                    if (total < bestTotal) { bestTotal = total; bestEnd = u; }
                }
                for (int v = 0; v < n; v++)
                {
                    if (_done[v] || !_see[u * n + v]) continue;
                    float dv = du + Vector2.Distance(_nodes[u], _nodes[v]);
                    if (dv < _dist[v]) { _dist[v] = dv; _prev[v] = u; }
                }
            }
            if (bestEnd < 0)
            {
                // no way to the goal: toward the reachable corner nearest it, unless the walker is nearer already
                float near = Vector2.Distance(from, goal);
                for (int k = 0; k < n; k++)
                {
                    if (float.IsPositiveInfinity(_dist[k])) continue;
                    float d = Vector2.Distance(_nodes[k], goal);
                    if (d < near - 0.05f) { near = d; bestEnd = k; }
                }
                if (bestEnd < 0) return goal;
            }
            // the first corner on the way that the walker isn't already standing on
            int pick = -1;
            for (int cur = bestEnd; cur >= 0; cur = _prev[cur])
                if (Vector2.Distance(from, _nodes[cur]) > 0.05f) pick = cur;
            return pick >= 0 ? _nodes[pick] : goal;
        }

        bool Inside(Vector2 p, int i, float grow) =>
            p.X > _min[i].X - grow && p.X < _max[i].X + grow && p.Y > _min[i].Y - grow && p.Y < _max[i].Y + grow;

        /// <summary>The segment passes through a box grown by the margin (by the inner margin where an end is within the
        /// margin; with <paramref name="endRule"/>, a start inside even that ignores the box).</summary>
        bool Blocked(Vector2 a, Vector2 b, bool endRule)
        {
            for (int i = 0; i < _min.Length; i++)
            {
                float grow = _margin;
                if (endRule && (Inside(a, i, _margin) || Inside(b, i, _margin)))
                {
                    grow = _inner;
                    if (Inside(a, i, grow)) continue;
                }
                if (Crosses(a, b, _min[i] - new Vector2(grow), _max[i] + new Vector2(grow))) return true;
            }
            return false;
        }

        /// <summary>The segment a-b passes through the open box lo-hi (slab test).</summary>
        static bool Crosses(Vector2 a, Vector2 b, Vector2 lo, Vector2 hi)
        {
            float t0 = 0f, t1 = 1f;
            Vector2 d = b - a;
            for (int axis = 0; axis < 2; axis++)
            {
                float p = axis == 0 ? a.X : a.Y, dv = axis == 0 ? d.X : d.Y;
                float l = axis == 0 ? lo.X : lo.Y, h = axis == 0 ? hi.X : hi.Y;
                if (MathF.Abs(dv) < 1e-6f)
                {
                    if (p <= l || p >= h) return false;
                    continue;
                }
                float u0 = (l - p) / dv, u1 = (h - p) / dv;
                if (u0 > u1) { float t = u0; u0 = u1; u1 = t; }
                t0 = MathF.Max(t0, u0);
                t1 = MathF.Min(t1, u1);
                if (t0 >= t1) return false;
            }
            return true;
        }
    }
}
